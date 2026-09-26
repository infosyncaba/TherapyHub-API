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

    // Function only applies to "Maladaptive Behaviors" items
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

        var items = new List<GoalTrackerItems>();
        foreach (var i in dto.Items)
        {
            items.Add(new GoalTrackerItems
            {
                GoalTrackerId = tracker.Id,
                CategoryId = (byte)i.CategoryId,
                Name = i.Name.Trim(),
                MasteryCriteria = i.MasteryCriteria?.Trim(),
                StatusId = (byte)i.StatusId,
                FunctionId = i.FunctionId.HasValue && await IsMaladaptiveCategoryAsync(i.CategoryId) ? i.FunctionId : null,
                CreatedAt = DateTime.UtcNow,
            });
        }

        _context.GoalTrackerItems.AddRange(items);
        await _context.SaveChangesAsync();

        return await BuildResponseAsync(tracker);
    }

    public async Task<GoalTrackerItemResponseDto?> UpdateItemAsync(long itemId, UpdateGoalTrackerItemRequestDto dto)
    {
        var item = await _context.GoalTrackerItems.FindAsync(itemId);
        if (item == null) return null;

        item.Name = dto.Name.Trim();
        item.MasteryCriteria = dto.MasteryCriteria?.Trim();
        item.StatusId = (byte)dto.StatusId;
        if (await IsMaladaptiveCategoryAsync(item.CategoryId))
            item.FunctionId = dto.FunctionId;

        await _context.SaveChangesAsync();

        var status = await _context.GoalTrackerStatus.FindAsync(dto.StatusId);
        var function = item.FunctionId.HasValue
            ? await _context.BehaviorFunctions.FindAsync(item.FunctionId.Value)
            : null;
        return MapItemToDto(item, status, function?.Name);
    }

    public async Task<bool> DeleteItemAsync(long itemId)
    {
        var item = await _context.GoalTrackerItems.FindAsync(itemId);
        if (item == null) return false;

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

        var functionIds = items.Where(i => i.FunctionId.HasValue).Select(i => i.FunctionId!.Value).Distinct().ToList();
        var functions = await _context.BehaviorFunctions
            .Where(f => functionIds.Contains(f.Id))
            .ToDictionaryAsync(f => f.Id, f => f.Name);

        return new GoalTrackerResponseDto
        {
            TrackerId = tracker.Id,
            OwnerActorId = tracker.OwnerActorId,
            Items = items.Select(i => MapItemToDto(
                i,
                statuses.GetValueOrDefault((int)i.StatusId),
                i.FunctionId.HasValue ? functions.GetValueOrDefault(i.FunctionId.Value) : null)),
        };
    }

    private static GoalTrackerItemResponseDto MapItemToDto(GoalTrackerItems item, GoalTrackerStatus? status, string? functionName) => new()
    {
        Id = item.Id,
        GoalTrackerId = item.GoalTrackerId,
        CategoryId = item.CategoryId,
        Name = item.Name,
        MasteryCriteria = item.MasteryCriteria,
        StatusId = item.StatusId,
        StatusName = status?.Name ?? "",
        StatusColor = status?.Color ?? "#6b7280",
        FunctionId = item.FunctionId,
        FunctionName = functionName,
        CreatedAt = item.CreatedAt,
    };
}
