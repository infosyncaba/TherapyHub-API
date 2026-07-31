using Microsoft.EntityFrameworkCore;
using TherapuHubAPI.DTOs.Requests.SessionNotes;
using TherapuHubAPI.DTOs.Responses.SessionNotes;
using TherapuHubAPI.Models;
using TherapuHubAPI.Services.IServices;

namespace TherapuHubAPI.Services;

public class SessionNotesService : ISessionNotesService
{
    private readonly ContextDB _context;
    private readonly ILogger<SessionNotesService> _logger;

    public SessionNotesService(ContextDB context, ILogger<SessionNotesService> logger)
    {
        _context = context;
        _logger = logger;
    }

    public async Task<IEnumerable<RbtForSessionResponseDto>> GetRbtsForUserAsync(int userId, int companyId)
    {
        var user = await _context.Users.FindAsync(userId);
        if (user == null) return Enumerable.Empty<RbtForSessionResponseDto>();

        var isSystem = await _context.UserTypes
            .Where(t => t.Id == user.UserTypeId)
            .Select(t => t.IsSystem)
            .FirstOrDefaultAsync();

        IQueryable<Staff> staffQuery;

        if (isSystem)
        {
            staffQuery = _context.Staff
                .Where(s => s.RoleId == 1)
                .Where(s => _context.Actors.Any(a => a.Id == s.ActorId && a.CompanyId == companyId && !a.IsDeleted && a.IsActive));
        }
        else
        {
            var userActorId = user.ActorId;
            var targetActorIds = await _context.ActorRelationships
                .Where(r => r.SourceActorId == userActorId && r.RelationshipTypeId == 1)
                .Select(r => r.TargetActorId)
                .ToListAsync();

            staffQuery = _context.Staff
                .Where(s => s.RoleId == 1 && targetActorIds.Contains(s.ActorId))
                .Where(s => _context.Actors.Any(a => a.Id == s.ActorId && !a.IsDeleted && a.IsActive));
        }

        return await staffQuery
            .Join(_context.Actors, s => s.ActorId, a => a.Id, (s, a) => new RbtForSessionResponseDto
            {
                StaffId = s.Id,
                ActorId = s.ActorId,
                Name = a.FullName,
            })
            .ToListAsync();
    }

    public async Task<IEnumerable<SessionNoteStatusResponseDto>> GetStatusesAsync()
    {
        return await _context.SessionNotesStatus
            .Where(s => s.IsActive && s.IsDelete != true)
            .Select(s => new SessionNoteStatusResponseDto
            {
                Id = s.Id,
                Name = s.Name,
                Color = s.Color,
                IsActive = s.IsActive,
            })
            .ToListAsync();
    }

    public async Task<IEnumerable<SessionNoteResponseDto>> GetByWeekAsync(int companyId, DateTime weekStart, DateTime weekEnd)
    {
        var statuses = await _context.SessionNotesStatus.ToDictionaryAsync(s => s.Id, s => s);

        var dayEnd = weekEnd.Date.AddDays(1);
        var notes = await _context.SessionsNotes
            .Where(n => !n.IsDeleted
                && n.SessionDate >= weekStart.Date
                && n.SessionDate < dayEnd
                && _context.Actors.Any(a => a.Id == n.RbtActorId && a.CompanyId == companyId))
            .ToListAsync();

        return notes.Select(n =>
        {
            statuses.TryGetValue(n.StatusId, out var status);
            return MapToDto(n, status);
        }).ToList();
    }

    public async Task<SessionNoteResponseDto> CreateAsync(CreateSessionNoteRequestDto dto, int actorId)
    {
        var note = new SessionsNotes
        {
            RbtActorId = dto.RbtActorId,
            ClientActorId = dto.ClientActorId,
            SessionDate = dto.SessionDate,
            StatusId = dto.StatusId,
            Notes = dto.Notes,
            Actions = dto.Actions,
            CreatedByActorId = actorId,
            CreatedAt = DateTime.UtcNow,
            IsDeleted = false,
        };

        _context.SessionsNotes.Add(note);
        await _context.SaveChangesAsync();

        var status = await _context.SessionNotesStatus.FindAsync(note.StatusId);
        return MapToDto(note, status);
    }

    public async Task<SessionNoteResponseDto?> UpdateAsync(long id, UpdateSessionNoteRequestDto dto, int actorId)
    {
        var note = await _context.SessionsNotes.FindAsync(id);
        if (note == null || note.IsDeleted) return null;

        note.ClientActorId = dto.ClientActorId;
        note.StatusId = dto.StatusId;
        note.Notes = dto.Notes;
        note.Actions = dto.Actions;

        await _context.SaveChangesAsync();

        var status = await _context.SessionNotesStatus.FindAsync(note.StatusId);
        return MapToDto(note, status);
    }

    public async Task<bool> DeleteAsync(long id, int actorId)
    {
        var note = await _context.SessionsNotes.FindAsync(id);
        if (note == null || note.IsDeleted) return false;

        note.IsDeleted = true;
        note.DeletedAt = DateTime.UtcNow;
        note.DeletedByActorId = actorId;

        await _context.SaveChangesAsync();
        return true;
    }

    private static SessionNoteResponseDto MapToDto(SessionsNotes note, SessionNotesStatus? status) => new()
    {
        Id = note.Id,
        RbtActorId = note.RbtActorId,
        ClientActorId = note.ClientActorId,
        SessionDate = note.SessionDate,
        StatusId = note.StatusId,
        StatusName = status?.Name,
        StatusColor = status?.Color,
        Notes = note.Notes,
        Actions = note.Actions,
        CreatedByActorId = note.CreatedByActorId,
        CreatedAt = note.CreatedAt,
    };
}
