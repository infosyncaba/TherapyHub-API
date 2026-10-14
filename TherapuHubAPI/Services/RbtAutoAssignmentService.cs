using Microsoft.EntityFrameworkCore;
using TherapuHubAPI.Models;
using TherapuHubAPI.Services.IServices;

namespace TherapuHubAPI.Services;

public class RbtAutoAssignmentService : IRbtAutoAssignmentService
{
    // Must match RelationshipType table rows / ActorRelationshipService
    private const int TypeSupervisorRbt = 1;
    private const short RbtRoleId = 1;

    private readonly ContextDB _context;
    private readonly ILogger<RbtAutoAssignmentService> _logger;

    public RbtAutoAssignmentService(ContextDB context, ILogger<RbtAutoAssignmentService> logger)
    {
        _context = context;
        _logger = logger;
    }

    public Task SyncForUserAsync(int userId) =>
        SyncUsersAsync(_context.Users.Where(u => u.Id == userId));

    public Task SyncForUserTypeAsync(int userTypeId) =>
        SyncUsersAsync(_context.Users.Where(u => u.UserTypeId == userTypeId));

    public async Task SyncForStaffAsync(int staffId)
    {
        var staff = await _context.Staff
            .Where(s => s.Id == staffId)
            .Join(_context.Actors, s => s.ActorId, a => a.Id, (s, a) => new { Staff = s, Actor = a })
            .FirstOrDefaultAsync();
        if (staff == null) return;

        var isRbt = staff.Staff.RoleId == RbtRoleId && !staff.Actor.IsDeleted;
        if (!isRbt)
        {
            await RemoveForRbtActorAsync(staff.Actor.Id);
            return;
        }

        // Active, non-deleted users of the RBT's company whose user type has AutoAssignAllRbts enabled
        var companyId = staff.Actor.CompanyId;
        var eligibleUserActorIds = await (
            from u in _context.Users
            join a in _context.Actors on u.ActorId equals a.Id
            join t in _context.UserTypes on u.UserTypeId equals t.Id
            where t.AutoAssignAllRbts && u.IsActive && a.IsActive && !a.IsDeleted && a.CompanyId == companyId
            select a.Id).ToListAsync();

        var alreadyLinked = await _context.ActorRelationships
            .Where(r => r.TargetActorId == staff.Actor.Id && r.RelationshipTypeId == TypeSupervisorRbt)
            .Select(r => r.SourceActorId)
            .ToListAsync();

        var toAdd = eligibleUserActorIds.Except(alreadyLinked)
            .Select(userActorId => NewAutoRelationship(userActorId, staff.Actor.Id))
            .ToList();
        if (toAdd.Count == 0) return;

        _context.ActorRelationships.AddRange(toAdd);
        await _context.SaveChangesAsync();

        _logger.LogInformation("Auto-assigned RBT ActorId={RbtId} to {Count} user(s)", staff.Actor.Id, toAdd.Count);
    }

    public async Task RemoveForRbtActorAsync(int rbtActorId)
    {
        var toRemove = await _context.ActorRelationships
            .Where(r => r.TargetActorId == rbtActorId
                     && r.RelationshipTypeId == TypeSupervisorRbt
                     && r.IsAutoAssigned)
            .ToListAsync();
        if (toRemove.Count == 0) return;

        _context.ActorRelationships.RemoveRange(toRemove);
        await _context.SaveChangesAsync();

        _logger.LogInformation("Removed {Count} auto-assigned relationship(s) to RBT ActorId={RbtId}", toRemove.Count, rbtActorId);
    }

    // ─── Helpers ─────────────────────────────────────────────────────────────

    private async Task SyncUsersAsync(IQueryable<Users> usersQuery)
    {
        var users = await (
            from u in usersQuery
            join a in _context.Actors on u.ActorId equals a.Id
            join t in _context.UserTypes on u.UserTypeId equals t.Id
            select new
            {
                ActorId = a.Id,
                a.CompanyId,
                Eligible = t.AutoAssignAllRbts && u.IsActive && a.IsActive && !a.IsDeleted,
            }).ToListAsync();
        if (users.Count == 0) return;

        var userActorIds = users.Select(u => u.ActorId).ToList();
        var companyIds = users.Where(u => u.Eligible).Select(u => u.CompanyId).Distinct().ToList();

        var rbtsByCompany = (await _context.Staff
                .Where(s => s.RoleId == RbtRoleId)
                .Join(_context.Actors, s => s.ActorId, a => a.Id, (s, a) => a)
                .Where(a => companyIds.Contains(a.CompanyId) && !a.IsDeleted)
                .Select(a => new { a.Id, a.CompanyId })
                .ToListAsync())
            .GroupBy(a => a.CompanyId)
            .ToDictionary(g => g.Key, g => g.Select(a => a.Id).ToHashSet());

        var existingBySource = (await _context.ActorRelationships
                .Where(r => userActorIds.Contains(r.SourceActorId) && r.RelationshipTypeId == TypeSupervisorRbt)
                .ToListAsync())
            .GroupBy(r => r.SourceActorId)
            .ToDictionary(g => g.Key, g => g.ToList());

        var toAdd = new List<ActorRelationships>();
        var toRemove = new List<ActorRelationships>();

        foreach (var user in users)
        {
            existingBySource.TryGetValue(user.ActorId, out var existing);
            existing ??= new List<ActorRelationships>();

            var rbtActorIds = user.Eligible && rbtsByCompany.TryGetValue(user.CompanyId, out var ids)
                ? ids
                : new HashSet<int>();

            // Auto relationships that no longer apply (flag off, user inactive, or target no longer an RBT)
            toRemove.AddRange(existing.Where(r => r.IsAutoAssigned && !rbtActorIds.Contains(r.TargetActorId)));

            // Missing relationships (also recreates auto ones that were removed by hand)
            var linkedTargets = existing.Select(r => r.TargetActorId).ToHashSet();
            toAdd.AddRange(rbtActorIds
                .Where(rbtId => !linkedTargets.Contains(rbtId))
                .Select(rbtId => NewAutoRelationship(user.ActorId, rbtId)));
        }

        if (toAdd.Count == 0 && toRemove.Count == 0) return;

        _context.ActorRelationships.RemoveRange(toRemove);
        _context.ActorRelationships.AddRange(toAdd);
        await _context.SaveChangesAsync();

        _logger.LogInformation("RBT auto-assignment sync: {Users} user(s), +{Added} / -{Removed} relationship(s)",
            users.Count, toAdd.Count, toRemove.Count);
    }

    private static ActorRelationships NewAutoRelationship(int userActorId, int rbtActorId) => new()
    {
        SourceActorId = userActorId,
        TargetActorId = rbtActorId,
        RelationshipTypeId = TypeSupervisorRbt,
        IsAutoAssigned = true,
        CreatedAt = DateTime.UtcNow,
    };
}
