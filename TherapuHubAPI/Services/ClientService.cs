using Microsoft.EntityFrameworkCore;
using TherapuHubAPI.DTOs.Requests.Clients;
using TherapuHubAPI.DTOs.Responses.Clients;
using TherapuHubAPI.Models;
using TherapuHubAPI.Repositorio;
using TherapuHubAPI.Repositorio.IRepositorio;
using TherapuHubAPI.Services.IServices;

namespace TherapuHubAPI.Services;

public class ClientService : IClientService
{
    private readonly IClientRepositorio _clientRepositorio;
    private readonly IClientStatusRepositorio _statusRepositorio;
    private readonly IStaffRepositorio _staffRepositorio;
    private readonly IUnitOfWork _unitOfWork;
    private readonly ContextDB _context;
    private readonly ILogger<ClientService> _logger;

    public ClientService(
        IClientRepositorio clientRepositorio,
        IClientStatusRepositorio statusRepositorio,
        IStaffRepositorio staffRepositorio,
        IUnitOfWork unitOfWork,
        ContextDB context,
        ILogger<ClientService> logger)
    {
        _clientRepositorio = clientRepositorio;
        _statusRepositorio = statusRepositorio;
        _staffRepositorio = staffRepositorio;
        _unitOfWork = unitOfWork;
        _context = context;
        _logger = logger;
    }

    public async Task<IEnumerable<ClientResponseDto>> GetByCompanyIdAsync(int companyId, int userId)
    {
        // Determine if the user's type is flagged as system (sees all clients)
        var isSystem = await _context.Users
            .Where(u => u.Id == userId)
            .Join(_context.UserTypes, u => u.UserTypeId, ut => ut.Id, (u, ut) => ut.IsSystem)
            .FirstOrDefaultAsync();

        IEnumerable<Clients> clients;

        if (isSystem)
        {
            clients = await _clientRepositorio.GetByCompanyIdAsync(companyId);
        }
        else
        {
            // Get the current user's ActorId
            var userActorId = await _context.Users
                .Where(u => u.Id == userId)
                .Select(u => u.ActorId)
                .FirstOrDefaultAsync();

            // Find all actor IDs related to this user via ActorRelationships
            var relatedActorIds = await _context.ActorRelationships
                .Where(r => r.SourceActorId == userActorId || r.TargetActorId == userActorId)
                .Select(r => r.SourceActorId == userActorId ? r.TargetActorId : r.SourceActorId)
                .Distinct()
                .ToListAsync();

            // Get Staff IDs whose ActorId is among the related actors
            var allowedRbtIds = await _context.Staff
                .Where(s => relatedActorIds.Contains(s.ActorId))
                .Select(s => s.Id)
                .ToListAsync();

            // Return clients with no RBT assigned, or whose RBT is related to this user
            clients = await _context.Clients
                .Include(c => c.Actor)
                .Where(c => c.Actor.CompanyId == companyId
                         && !c.Actor.IsDeleted
                         && (c.RBTId == null || allowedRbtIds.Contains(c.RBTId.Value)))
                .OrderBy(c => c.Actor.FullName)
                .ToListAsync();
        }

        var statuses = (await _statusRepositorio.GetAllAsync()).ToList();
        var staff = (await _staffRepositorio.GetByCompanyIdAsync(companyId)).ToList();
        return clients.Select(c => MapToDto(c, statuses, staff));
    }

    public async Task<ClientResponseDto?> GetByIdAsync(int id, int companyId)
    {
        var client = await _clientRepositorio.GetByIdAndCompanyAsync(id, companyId);
        if (client == null) return null;
        var statuses = (await _statusRepositorio.GetAllAsync()).ToList();
        var staff = (await _staffRepositorio.GetByCompanyIdAsync(companyId)).ToList();
        return MapToDto(client, statuses, staff);
    }

    public async Task<ClientResponseDto> CreateAsync(CreateClientRequestDto request, int companyId)
    {
        _logger.LogInformation("Creating client {FullName} for company {CompanyId}", request.FullName, companyId);

        var clientCode = await _clientRepositorio.GenerateClientCodeAsync(companyId);

        var actor = new Actors
        {
            ActorType = "CLIENT",
            FullName = request.FullName.Trim(),
            Email = request.Email?.Trim(),
            Phone = request.Phone?.Trim(),
            CompanyId = companyId,
            IsActive = true,
            CreatedAt = DateTime.UtcNow,
        };

        var client = new Clients
        {
            ClientCode = clientCode,
            BirthDate = request.BirthDate,
            GuardianName = request.GuardianName?.Trim(),
            ClientStatusId = request.ClientStatusId,
            RBTId = request.RBTId,
            Emoji = request.Emoji,
            Diagnosis = request.Diagnosis?.Trim(),
            CreatedAt = DateTime.UtcNow,
            Actor = actor,
        };

        _context.Actors.Add(actor);
        await _clientRepositorio.AddAsync(client);
        await _unitOfWork.SaveChangesAsync();

        _logger.LogInformation("Client created with Id: {Id}, Code: {Code}", client.Id, client.ClientCode);

        if (request.BirthDate.HasValue)
            await UpsertBirthdayEventAsync(client.Id, actor.FullName, request.BirthDate.Value, companyId);

        var statuses = (await _statusRepositorio.GetAllAsync()).ToList();
        var staff = (await _staffRepositorio.GetByCompanyIdAsync(companyId)).ToList();
        return MapToDto(client, statuses, staff);
    }

    public async Task<ClientResponseDto?> UpdateAsync(int id, UpdateClientRequestDto request, int companyId)
    {
        var client = await _clientRepositorio.GetByIdAndCompanyAsync(id, companyId);
        if (client == null) return null;

        var previousBirthDate = client.BirthDate;

        client.Actor.FullName = request.FullName.Trim();
        client.Actor.Email = request.Email?.Trim();
        client.Actor.Phone = request.Phone?.Trim();
        client.BirthDate = request.BirthDate ?? client.BirthDate;
        client.GuardianName = request.GuardianName?.Trim();
        client.ClientStatusId = request.ClientStatusId;
        client.RBTId = request.RBTId;
        client.Emoji = request.Emoji;
        client.Diagnosis = request.Diagnosis?.Trim();

        _clientRepositorio.Update(client);
        await _unitOfWork.SaveChangesAsync();

        // Sync birthday event when birth date or name changes
        var newBirthDate = client.BirthDate;
        if (newBirthDate.HasValue)
            await UpsertBirthdayEventAsync(id, client.Actor.FullName, newBirthDate.Value, companyId);
        else if (previousBirthDate.HasValue && !newBirthDate.HasValue)
            await DeleteBirthdayEventAsync(id, companyId);

        var statuses = (await _statusRepositorio.GetAllAsync()).ToList();
        var staff = (await _staffRepositorio.GetByCompanyIdAsync(companyId)).ToList();
        return MapToDto(client, statuses, staff);
    }

    public async Task<bool> DeleteAsync(int id, int companyId)
    {
        var client = await _clientRepositorio.GetByIdAndCompanyAsync(id, companyId);
        if (client == null) return false;
        _clientRepositorio.Remove(client);
        _context.Actors.Remove(client.Actor);
        await _unitOfWork.SaveChangesAsync();
        await DeleteBirthdayEventAsync(id, companyId);
        return true;
    }

    public async Task<IEnumerable<ClientStatusResponseDto>> GetAllStatusesAsync()
    {
        var list = await _statusRepositorio.GetAllAsync();
        return list.Select(s => new ClientStatusResponseDto
        {
            Id = s.Id,
            Name = s.Name,
            Code = s.Code,
            IsActive = s.IsActive
        });
    }

    // ─── Birthday event helpers ─────────────────────────────────────────────────

    private static string BirthdayTag(int clientId) => $"BIRTHDAY:{clientId}";

    // Store the birthday event on this year's date at UTC noon.
    // UTC noon avoids timezone shifts that would move the event to the wrong calendar day in the frontend.
    // We intentionally skip "next occurrence" logic — the event stays on the fixed date for the current year.
    private static DateTime BirthdayDateForCalendar(DateOnly birthDate)
    {
        var year = DateTime.UtcNow.Year;
        return new DateTime(year, birthDate.Month, birthDate.Day, 12, 0, 0, DateTimeKind.Utc);
    }

    private async Task<int> GetBirthdayEventTypeIdAsync()
    {
        var birthdayType = await _context.EventTypes
            .Where(t => t.IsActive && t.Name.ToLower().Contains("birthday"))
            .Select(t => (int?)t.Id)
            .FirstOrDefaultAsync();

        if (birthdayType.HasValue) return birthdayType.Value;

        // Fallback: use any active type
        var fallback = await _context.EventTypes
            .Where(t => t.IsActive)
            .Select(t => (int?)t.Id)
            .FirstOrDefaultAsync();

        return fallback ?? 1;
    }

    private async Task UpsertBirthdayEventAsync(int clientId, string clientName, DateOnly birthDate, int companyId)
    {
        try
        {
            var tag = BirthdayTag(clientId);
            var birthdayDate = BirthdayDateForCalendar(birthDate);
            var title = clientName;

            var existing = await _context.Events
                .FirstOrDefaultAsync(e => e.OtherType == tag && e.CompanyId == companyId);

            if (existing != null)
            {
                existing.Title = title;
                existing.StartDate = birthdayDate;
                existing.EndDate = birthdayDate;
            }
            else
            {
                var eventTypeId = await GetBirthdayEventTypeIdAsync();
                var newEvent = new Events
                {
                    Title = title,
                    Description = $"Birthday of {clientName}",
                    StartDate = birthdayDate,
                    EndDate = birthdayDate,
                    IsAllDay = true,
                    IsGlobal = true,
                    IsPrivate = false,
                    EventTypeId = eventTypeId,
                    CompanyId = companyId,
                    CreatedAt = DateTime.UtcNow,
                    OtherType = tag,
                };
                _context.Events.Add(newEvent);
            }

            await _context.SaveChangesAsync();
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error upserting birthday event for client {ClientId}", clientId);
        }
    }

    private async Task DeleteBirthdayEventAsync(int clientId, int companyId)
    {
        try
        {
            var tag = BirthdayTag(clientId);
            var existing = await _context.Events
                .FirstOrDefaultAsync(e => e.OtherType == tag && e.CompanyId == companyId);
            if (existing != null)
            {
                _context.Events.Remove(existing);
                await _context.SaveChangesAsync();
            }
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error deleting birthday event for client {ClientId}", clientId);
        }
    }

    private static ClientResponseDto MapToDto(Clients c, List<ClientStatuses> statuses, List<Staff> staff)
    {
        var status = statuses.FirstOrDefault(x => x.Id == c.ClientStatusId);
        var rbt = c.RBTId.HasValue ? staff.FirstOrDefault(x => x.Id == c.RBTId.Value) : null;
        return new ClientResponseDto
        {
            Id = c.Id,
            ActorId = c.ActorId,
            ClientCode = c.ClientCode,
            FullName = c.Actor.FullName,
            BirthDate = c.BirthDate,
            Email = c.Actor.Email,
            Phone = c.Actor.Phone,
            GuardianName = c.GuardianName,
            ClientStatusId = c.ClientStatusId,
            ClientStatusName = status?.Name,
            CompanyId = c.Actor.CompanyId,
            RBTId = c.RBTId,
            RBTName = rbt?.Actor.FullName,
            Emoji = c.Emoji,
            Diagnosis = c.Diagnosis,
            CreatedAt = c.CreatedAt,
        };
    }
}
