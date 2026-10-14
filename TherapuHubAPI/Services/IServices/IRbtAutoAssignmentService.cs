namespace TherapuHubAPI.Services.IServices;

/// <summary>
/// Keeps the Supervisor → RBT relationships (type 1) that are created automatically
/// for users whose user type has <c>AutoAssignAllRbts</c> enabled.
/// All methods are idempotent: they add missing auto relationships and remove the ones that no longer apply.
/// Manual relationships (IsAutoAssigned = false) are never touched.
/// </summary>
public interface IRbtAutoAssignmentService
{
    /// <summary>Syncs a single user (after create, update, activate/deactivate or login).</summary>
    Task SyncForUserAsync(int userId);

    /// <summary>Syncs every user of a user type, across all companies (after the flag is toggled).</summary>
    Task SyncForUserTypeAsync(int userTypeId);

    /// <summary>Syncs a staff member (after create or role change): links it to every eligible user if it is an RBT, otherwise removes its auto links.</summary>
    Task SyncForStaffAsync(int staffId);

    /// <summary>Removes the auto relationships pointing to a staff actor (before the staff is deleted).</summary>
    Task RemoveForRbtActorAsync(int rbtActorId);
}
