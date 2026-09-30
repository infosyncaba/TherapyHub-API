using Microsoft.EntityFrameworkCore;
using TherapuHubAPI.DTOs.Requests.GoalTracker;
using TherapuHubAPI.DTOs.Responses.GoalTracker;
using TherapuHubAPI.Models;
using TherapuHubAPI.Services.IServices;

namespace TherapuHubAPI.Services;

public class GoalTrackerService : IGoalTrackerService
{
    private readonly ContextDB _context;
    private readonly ILogger<GoalTrackerService> _logger;

    public GoalTrackerService(ContextDB context, ILogger<GoalTrackerService> logger)
    {
        _context = context;
        _logger = logger;
    }

    public async Task<IEnumerable<GoalTrackerCategoryResponseDto>> GetCategoriesAsync()
    {
        return await _context.GoalTrackerCategories
            .Where(c => c.IsActive != false)
            .OrderBy(c => c.DisplayOrder)
            .Select(c => new GoalTrackerCategoryResponseDto
            {
                Id = c.Id,
                Name = c.Name,
                DisplayOrder = c.DisplayOrder,
                IsActive = c.IsActive,
            })
            .ToListAsync();
    }

    public async Task<GoalTrackerResponseDto?> GetByOwnerAsync(int ownerActorId)
    {
        var tracker = await _context.GoalTrackers
            .Where(t => t.OwnerActorId == ownerActorId && t.IsDelete != true)
            .FirstOrDefaultAsync();

        if (tracker == null) return null;

        return await BuildResponseAsync(tracker);
    }

    private const string MaladaptiveCategoryName = "Maladaptive Behaviors";

    // Functions only apply to "Maladaptive Behaviors" items
    private async Task<bool> IsMaladaptiveCategoryAsync(int categoryId) =>
        await _context.GoalTrackerCategories
            .AnyAsync(c => c.Id == categoryId && c.Name == MaladaptiveCategoryName);

    public async Task<GoalTrackerResponseDto> CreateRowAsync(CreateGoalTrackerRowRequestDto dto, int actorId)
    {
        var tracker = await _context.GoalTrackers
            .Where(t => t.OwnerActorId == dto.OwnerActorId && t.IsDelete != true)
            .FirstOrDefaultAsync();

        if (tracker == null)
        {
            tracker = new GoalTrackers
            {
                OwnerActorId = dto.OwnerActorId,
                CreatedByActorId = actorId,
                CreatedAt = DateTime.UtcNow,
            };
            _context.GoalTrackers.Add(tracker);
            await _context.SaveChangesAsync();
        }

        await using var transaction = await _context.Database.BeginTransactionAsync();

        var created = new List<(GoalTrackerItems Item, List<int> FunctionIds)>();
        foreach (var i in dto.Items)
        {
            var item = new GoalTrackerItems
            {
                GoalTrackerId = tracker.Id,
                CategoryId = (byte)i.CategoryId,
                Name = i.Name.Trim(),
                MasteryCriteria = i.MasteryCriteria?.Trim(),
                StatusId = (byte)i.StatusId,
                CreatedAt = DateTime.UtcNow,
            };
            var functionIds = i.FunctionIds is { Count: > 0 } && await IsMaladaptiveCategoryAsync(i.CategoryId)
                ? i.FunctionIds.Distinct().ToList()
                : [];
            created.Add((item, functionIds));
        }

        _context.GoalTrackerItems.AddRange(created.Select(c => c.Item));
        await _context.SaveChangesAsync();

        // Item Ids are available after the first save
        _context.GoalTrackerItemFunctions.AddRange(created.SelectMany(c =>
            c.FunctionIds.Select(fid => new GoalTrackerItemFunctions
            {
                GoalTrackerItemId = c.Item.Id,
                FunctionId = fid,
            })));
        await _context.SaveChangesAsync();

        await transaction.CommitAsync();

        return await BuildResponseAsync(tracker);
    }

    public async Task<GoalTrackerItemResponseDto?> UpdateItemAsync(long itemId, UpdateGoalTrackerItemRequestDto dto)
    {
        var item = await _context.GoalTrackerItems.FindAsync(itemId);
        if (item == null) return null;

        item.Name = dto.Name.Trim();
        item.MasteryCriteria = dto.MasteryCriteria?.Trim();
        item.StatusId = (byte)dto.StatusId;

        // null = leave functions untouched; empty list = clear them
        if (dto.FunctionIds != null && await IsMaladaptiveCategoryAsync(item.CategoryId))
        {
            var requested = dto.FunctionIds.Distinct().ToList();
            var current = await _context.GoalTrackerItemFunctions
                .Where(f => f.GoalTrackerItemId == itemId)
                .ToListAsync();

            _context.GoalTrackerItemFunctions.RemoveRange(current.Where(c => !requested.Contains(c.FunctionId)));
            _context.GoalTrackerItemFunctions.AddRange(requested
                .Where(fid => current.All(c => c.FunctionId != fid))
                .Select(fid => new GoalTrackerItemFunctions { GoalTrackerItemId = itemId, FunctionId = fid }));
        }

        await _context.SaveChangesAsync();

        var status = await _context.GoalTrackerStatus.FindAsync(dto.StatusId);
        var functions = await GetItemFunctionsAsync([itemId]);
        return MapItemToDto(item, status, functions.GetValueOrDefault(itemId));
    }

    public async Task<bool> DeleteItemAsync(long itemId)
    {
        var item = await _context.GoalTrackerItems.FindAsync(itemId);
        if (item == null) return false;

        var links = await _context.GoalTrackerItemFunctions
            .Where(f => f.GoalTrackerItemId == itemId)
            .ToListAsync();
        _context.GoalTrackerItemFunctions.RemoveRange(links);
        _context.GoalTrackerItems.Remove(item);
        await _context.SaveChangesAsync();
        return true;
    }

    private async Task<GoalTrackerResponseDto> BuildResponseAsync(GoalTrackers tracker)
    {
        var items = await _context.GoalTrackerItems
            .Where(i => i.GoalTrackerId == tracker.Id)
            .OrderBy(i => i.CategoryId)
            .ThenBy(i => i.Id)
            .ToListAsync();

        var statusIds = items.Select(i => (int)i.StatusId).Distinct().ToList();
        var statuses = await _context.GoalTrackerStatus
            .Where(s => statusIds.Contains(s.Id))
            .ToDictionaryAsync(s => s.Id);

        var functions = await GetItemFunctionsAsync(items.Select(i => i.Id).ToList());

        return new GoalTrackerResponseDto
        {
            TrackerId = tracker.Id,
            OwnerActorId = tracker.OwnerActorId,
            Items = items.Select(i => MapItemToDto(
                i,
                statuses.GetValueOrDefault((int)i.StatusId),
                functions.GetValueOrDefault(i.Id))),
        };
    }

    // Functions per item, in catalog order. Includes deactivated/deleted functions so history is preserved.
    private async Task<Dictionary<long, List<(int Id, string Name)>>> GetItemFunctionsAsync(List<long> itemIds)
    {
        var rows = await (
            from link in _context.GoalTrackerItemFunctions
            join fn in _context.BehaviorFunctions on link.FunctionId equals fn.Id
            where itemIds.Contains(link.GoalTrackerItemId)
            orderby fn.Id
            select new { link.GoalTrackerItemId, fn.Id, fn.Name }
        ).ToListAsync();

        return rows
            .GroupBy(r => r.GoalTrackerItemId)
            .ToDictionary(g => g.Key, g => g.Select(r => (r.Id, r.Name)).ToList());
    }

    private static GoalTrackerItemResponseDto MapItemToDto(GoalTrackerItems item, GoalTrackerStatus? status, List<(int Id, string Name)>? functions) => new()
    {
        Id = item.Id,
        GoalTrackerId = item.GoalTrackerId,
        CategoryId = item.CategoryId,
        Name = item.Name,
        MasteryCriteria = item.MasteryCriteria,
        StatusId = item.StatusId,
        StatusName = status?.Name ?? "",
        StatusColor = status?.Color ?? "#6b7280",
        FunctionIds = functions?.Select(f => f.Id).ToList() ?? [],
        FunctionNames = functions?.Select(f => f.Name).ToList() ?? [],
        CreatedAt = item.CreatedAt,
    };
}
