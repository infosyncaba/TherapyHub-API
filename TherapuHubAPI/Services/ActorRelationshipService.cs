using Microsoft.EntityFrameworkCore;
using TherapuHubAPI.DTOs.Requests.ActorRelationships;
using TherapuHubAPI.DTOs.Responses.ActorRelationships;
using TherapuHubAPI.Models;
using TherapuHubAPI.Services.IServices;

namespace TherapuHubAPI.Services;

public class ActorRelationshipService : IActorRelationshipService
{
    // Must match RelationshipType table rows
    private const int TypeSupervisorRbt = 1;
    private const int TypeUserDelegate  = 2;
    private const int TypeRbtClient     = 3;

    private readonly ContextDB _context;
    private readonly ILogger<ActorRelationshipService> _logger;

    public ActorRelationshipService(ContextDB context, ILogger<ActorRelationshipService> logger)
    {
        _context = context;
        _logger = logger;
    }

    // ─── Supervisor → RBT (type 1) ───────────────────────────────────────────

    public async Task<IEnumerable<SupervisorResponseDto>> GetSupervisorsAsync(int companyId)
    {
        var userTypes = await _context.UserTypes.ToDictionaryAsync(t => t.Id);

        var users = await _context.Users
            .Join(_context.Actors, u => u.ActorId, a => a.Id, (u, a) => new { User = u, Actor = a })
            .Where(x => x.Actor.CompanyId == companyId && !x.Actor.IsDeleted && x.Actor.IsActive)
            .ToListAsync();

        var nonSystem = users
            .Where(x => userTypes.TryGetValue(x.User.UserTypeId, out var ut) && !ut.IsSystem)
            .ToList();

        var actorIds = nonSystem.Select(x => x.Actor.Id).ToList();
        var counts = await _context.ActorRelationships
            .Where(r => actorIds.Contains(r.SourceActorId) && r.RelationshipTypeId == TypeSupervisorRbt)
            .GroupBy(r => r.SourceActorId)
            .Select(g => new { ActorId = g.Key, Count = g.Count() })
            .ToDictionaryAsync(x => x.ActorId, x => x.Count);

        return nonSystem.Select(x =>
        {
            userTypes.TryGetValue(x.User.UserTypeId, out var ut);
            counts.TryGetValue(x.Actor.Id, out var count);
            return new SupervisorResponseDto
            {
                UserId = x.User.Id,
                ActorId = x.Actor.Id,
                FullName = x.Actor.FullName,
                Email = x.Actor.Email,
                UserTypeName = ut?.Name ?? string.Empty,
                AssignedRbtCount = count,
            };
        }).OrderBy(s => s.FullName).ToList();
    }

    public async Task<IEnumerable<RbtAssignmentResponseDto>> GetRbtsForSupervisorAsync(int supervisorActorId, int companyId)
    {
        var rbts = await _context.Staff
            .Where(s => s.RoleId == 1)
            .Join(_context.Actors, s => s.ActorId, a => a.Id, (s, a) => new { Staff = s, Actor = a })
            .Where(x => x.Actor.CompanyId == companyId && !x.Actor.IsDeleted)
            .ToListAsync();

        var assignments = await _context.ActorRelationships
            .Where(r => r.SourceActorId == supervisorActorId && r.RelationshipTypeId == TypeSupervisorRbt)
            .ToListAsync();

        var byActorId = assignments.ToDictionary(r => r.TargetActorId);

        return rbts.Select(x =>
        {
            byActorId.TryGetValue(x.Actor.Id, out var rel);
            return new RbtAssignmentResponseDto
            {
                StaffId = x.Staff.Id,
                ActorId = x.Actor.Id,
                Name = x.Actor.FullName,
                IsAssigned = rel != null,
                RelationshipId = rel?.Id,
                IsAutoAssigned = rel?.IsAutoAssigned == true,
            };
        }).OrderBy(r => r.Name).ToList();
    }

    public async Task<ActorRelationshipResponseDto> AssignRbtAsync(AssignRbtRequestDto request, int companyId)
    {
        var supervisorActor = await _context.Actors
            .FirstOrDefaultAsync(a => a.Id == request.SupervisorActorId && a.CompanyId == companyId && !a.IsDeleted);
        if (supervisorActor == null)
            throw new InvalidOperationException("Supervisor not found in this company.");

        var rbtStaff = await _context.Staff
            .Where(s => s.ActorId == request.RbtActorId && s.RoleId == 1)
            .Join(_context.Actors, s => s.ActorId, a => a.Id, (s, a) => new { Staff = s, Actor = a })
            .FirstOrDefaultAsync(x => x.Actor.CompanyId == companyId && !x.Actor.IsDeleted);
        if (rbtStaff == null)
            throw new InvalidOperationException("RBT not found in this company.");

        var exists = await _context.ActorRelationships
            .AnyAsync(r => r.SourceActorId == request.SupervisorActorId
                        && r.TargetActorId == request.RbtActorId
                        && r.RelationshipTypeId == TypeSupervisorRbt);
        if (exists)
            throw new InvalidOperationException("This RBT is already assigned to the supervisor.");

        var relationship = new ActorRelationships
        {
            SourceActorId = request.SupervisorActorId,
            TargetActorId = request.RbtActorId,
            RelationshipTypeId = TypeSupervisorRbt,
            CreatedAt = DateTime.UtcNow,
        };

        _context.ActorRelationships.Add(relationship);
        await _context.SaveChangesAsync();

        _logger.LogInformation("Assigned RBT ActorId={RbtId} to Supervisor ActorId={SupId}",
            request.RbtActorId, request.SupervisorActorId);

        return new ActorRelationshipResponseDto
        {
            Id = relationship.Id,
            SupervisorActorId = relationship.SourceActorId,
            RbtActorId = relationship.TargetActorId,
            RbtName = rbtStaff.Actor.FullName,
            RbtStaffId = rbtStaff.Staff.Id,
            CreatedAt = relationship.CreatedAt,
        };
    }

    // ─── User → User (type 2) ────────────────────────────────────────────────

    public async Task<IEnumerable<SupervisorResponseDto>> GetUserSourcesAsync(int companyId)
    {
        var userTypes = await _context.UserTypes.ToDictionaryAsync(t => t.Id);

        var users = await _context.Users
            .Join(_context.Actors, u => u.ActorId, a => a.Id, (u, a) => new { User = u, Actor = a })
            .Where(x => x.Actor.CompanyId == companyId && !x.Actor.IsDeleted && x.Actor.IsActive)
            .ToListAsync();

        // All users can be sources (system and non-system)
        var actorIds = users.Select(x => x.Actor.Id).ToList();
        var counts = await _context.ActorRelationships
            .Where(r => actorIds.Contains(r.SourceActorId) && r.RelationshipTypeId == TypeUserDelegate)
            .GroupBy(r => r.SourceActorId)
            .Select(g => new { ActorId = g.Key, Count = g.Count() })
            .ToDictionaryAsync(x => x.ActorId, x => x.Count);

        return users.Select(x =>
        {
            userTypes.TryGetValue(x.User.UserTypeId, out var ut);
            counts.TryGetValue(x.Actor.Id, out var count);
            return new SupervisorResponseDto
            {
                UserId = x.User.Id,
                ActorId = x.Actor.Id,
                FullName = x.Actor.FullName,
                Email = x.Actor.Email,
                UserTypeName = ut?.Name ?? string.Empty,
                AssignedRbtCount = count,
            };
        }).OrderBy(s => s.FullName).ToList();
    }

    public async Task<IEnumerable<UserAssignmentResponseDto>> GetUsersForSourceAsync(int sourceActorId, int companyId)
    {
        var userTypes = await _context.UserTypes.ToDictionaryAsync(t => t.Id);

        // All users in company except the source itself
        var targets = await _context.Users
            .Join(_context.Actors, u => u.ActorId, a => a.Id, (u, a) => new { User = u, Actor = a })
            .Where(x => x.Actor.CompanyId == companyId
                      && !x.Actor.IsDeleted
                      && x.Actor.Id != sourceActorId)
            .ToListAsync();

        var assignments = await _context.ActorRelationships
            .Where(r => r.SourceActorId == sourceActorId && r.RelationshipTypeId == TypeUserDelegate)
            .ToListAsync();

        var byActorId = assignments.ToDictionary(r => r.TargetActorId);

        return targets.Select(x =>
        {
            byActorId.TryGetValue(x.Actor.Id, out var rel);
            userTypes.TryGetValue(x.User.UserTypeId, out var ut);
            return new UserAssignmentResponseDto
            {
                UserId = x.User.Id,
                ActorId = x.Actor.Id,
                FullName = x.Actor.FullName,
                Email = x.Actor.Email,
                UserTypeName = ut?.Name ?? string.Empty,
                IsAssigned = rel != null,
                RelationshipId = rel?.Id,
            };
        }).OrderBy(u => u.FullName).ToList();
    }

    public async Task<ActorRelationshipResponseDto> AssignUserAsync(AssignUserRequestDto request, int companyId)
    {
        var sourceActor = await _context.Actors
            .FirstOrDefaultAsync(a => a.Id == request.SourceActorId && a.CompanyId == companyId && !a.IsDeleted);
        if (sourceActor == null)
            throw new InvalidOperationException("Source user not found in this company.");

        var targetActor = await _context.Actors
            .FirstOrDefaultAsync(a => a.Id == request.TargetActorId && a.CompanyId == companyId && !a.IsDeleted);
        if (targetActor == null)
            throw new InvalidOperationException("Target user not found in this company.");

        if (request.SourceActorId == request.TargetActorId)
            throw new InvalidOperationException("A user cannot be assigned to themselves.");

        var exists = await _context.ActorRelationships
            .AnyAsync(r => r.SourceActorId == request.SourceActorId
                        && r.TargetActorId == request.TargetActorId
                        && r.RelationshipTypeId == TypeUserDelegate);
        if (exists)
            throw new InvalidOperationException("This user is already assigned.");

        var relationship = new ActorRelationships
        {
            SourceActorId = request.SourceActorId,
            TargetActorId = request.TargetActorId,
            RelationshipTypeId = TypeUserDelegate,
            CreatedAt = DateTime.UtcNow,
        };

        _context.ActorRelationships.Add(relationship);
        await _context.SaveChangesAsync();

        _logger.LogInformation("Assigned User ActorId={TargetId} to Source ActorId={SourceId}",
            request.TargetActorId, request.SourceActorId);

        return new ActorRelationshipResponseDto
        {
            Id = relationship.Id,
            SupervisorActorId = relationship.SourceActorId,
            RbtActorId = relationship.TargetActorId,
            RbtName = targetActor.FullName,
            RbtStaffId = 0,
            CreatedAt = relationship.CreatedAt,
        };
    }

    // ─── RBT → Client (type 3) ──────────────────────────────────────────────

    public async Task<IEnumerable<ClientAssignmentResponseDto>> GetClientsForRbtAsync(int rbtActorId, int companyId)
    {
        var clients = await _context.Clients
            .Join(_context.Actors, c => c.ActorId, a => a.Id, (c, a) => new { Client = c, Actor = a })
            .Where(x => x.Actor.CompanyId == companyId && !x.Actor.IsDeleted)
            .ToListAsync();

        var assignments = await _context.ActorRelationships
            .Where(r => r.SourceActorId == rbtActorId && r.RelationshipTypeId == TypeRbtClient)
            .ToListAsync();

        var byActorId = assignments.ToDictionary(r => r.TargetActorId);

        return clients.Select(x =>
        {
            byActorId.TryGetValue(x.Actor.Id, out var rel);
            return new ClientAssignmentResponseDto
            {
                ClientId = x.Client.Id,
                ActorId = x.Actor.Id,
                FullName = x.Actor.FullName,
                IsAssigned = rel != null,
                RelationshipId = rel?.Id,
            };
        }).OrderBy(c => c.FullName).ToList();
    }

    public async Task<ActorRelationshipResponseDto> AssignClientAsync(AssignClientRequestDto request, int companyId)
    {
        var rbtActor = await _context.Staff
            .Where(s => s.ActorId == request.RbtActorId && s.RoleId == 1)
            .Join(_context.Actors, s => s.ActorId, a => a.Id, (s, a) => new { Staff = s, Actor = a })
            .FirstOrDefaultAsync(x => x.Actor.CompanyId == companyId && !x.Actor.IsDeleted);
        if (rbtActor == null)
            throw new InvalidOperationException("RBT not found in this company.");

        var clientActor = await _context.Clients
            .Join(_context.Actors, c => c.ActorId, a => a.Id, (c, a) => new { Client = c, Actor = a })
            .FirstOrDefaultAsync(x => x.Client.ActorId == request.ClientActorId
                                   && x.Actor.CompanyId == companyId
                                   && !x.Actor.IsDeleted);
        if (clientActor == null)
            throw new InvalidOperationException("Client not found in this company.");

        var exists = await _context.ActorRelationships
            .AnyAsync(r => r.SourceActorId == request.RbtActorId
                        && r.TargetActorId == request.ClientActorId
                        && r.RelationshipTypeId == TypeRbtClient);
        if (exists)
            throw new InvalidOperationException("This client is already assigned to the RBT.");

        var relationship = new ActorRelationships
        {
            SourceActorId = request.RbtActorId,
            TargetActorId = request.ClientActorId,
            RelationshipTypeId = TypeRbtClient,
            CreatedAt = DateTime.UtcNow,
        };

        _context.ActorRelationships.Add(relationship);
        await _context.SaveChangesAsync();

        _logger.LogInformation("Assigned Client ActorId={ClientId} to RBT ActorId={RbtId}",
            request.ClientActorId, request.RbtActorId);

        return new ActorRelationshipResponseDto
        {
            Id = relationship.Id,
            SupervisorActorId = relationship.SourceActorId,
            RbtActorId = relationship.TargetActorId,
            RbtName = clientActor.Actor.FullName,
            RbtStaffId = 0,
            CreatedAt = relationship.CreatedAt,
        };
    }

    // ─── Shared ───────────────────────────────────────────────────────────────

    public async Task<bool> RemoveAssignmentAsync(long relationshipId, int companyId)
    {
        var relationship = await _context.ActorRelationships
            .FirstOrDefaultAsync(r => r.Id == relationshipId);
        if (relationship == null) return false;

        var sourceInCompany = await _context.Actors
            .AnyAsync(a => a.Id == relationship.SourceActorId && a.CompanyId == companyId && !a.IsDeleted);
        if (!sourceInCompany) return false;

        _context.ActorRelationships.Remove(relationship);
        await _context.SaveChangesAsync();

        _logger.LogInformation("Removed ActorRelationship Id={Id}", relationshipId);
        return true;
    }
}
