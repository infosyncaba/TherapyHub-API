using System.Security.Claims;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using TherapuHubAPI.DTOs.Common;
using TherapuHubAPI.DTOs.Requests.Library;
using TherapuHubAPI.DTOs.Responses.Library;
using TherapuHubAPI.Models;
using TherapuHubAPI.Services.IServices;

namespace TherapuHubAPI.Controllers;

[Authorize]
[ApiController]
[Route("api/library")]
[Produces("application/json")]
public class LibraryController : ControllerBase
{
    private readonly ILibraryItemService _service;
    private readonly ContextDB _context;
    private readonly ILogger<LibraryController> _logger;

    public LibraryController(ILibraryItemService service, ContextDB context, ILogger<LibraryController> logger)
    {
        _service = service;
        _context = context;
        _logger = logger;
    }

    // ── Auth helpers ─────────────────────────────────────────────────────────

    private int? GetUserId()
    {
        var claim = User.FindFirst(ClaimTypes.NameIdentifier);
        if (claim == null || !int.TryParse(claim.Value, out var id)) return null;
        return id;
    }

    private async Task<int?> GetActorIdAsync()
    {
        var userId = GetUserId();
        if (userId == null) return null;
        return await _context.Users
            .Where(u => u.Id == userId.Value)
            .Select(u => (int?)u.ActorId)
            .FirstOrDefaultAsync();
    }

    private bool IsSystemUser()
    {
        var claim = User.FindFirst("IsSystem");
        return claim != null && claim.Value.Equals("true", StringComparison.OrdinalIgnoreCase);
    }

    /// <summary>
    /// Returns true if the current user can perform the requested action.
    /// System users always pass. Other users are checked against library_permissions.
    /// </summary>
    private async Task<bool> HasPermissionAsync(int actorId, string action)
    {
        if (IsSystemUser()) return true;

        var perm = await _context.library_permissions
            .FirstOrDefaultAsync(p => p.actorId == actorId);

        if (perm == null) return false;

        return action switch
        {
            "create" => perm.canCreate,
            "edit"   => perm.canEdit,
            "delete" => perm.canDelete,
            _        => false
        };
    }

    // ── Categories ──────────────────────────────────────────────────────────────

    /// <summary>Get all library categories.</summary>
    [HttpGet("categories")]
    [ProducesResponseType(typeof(ApiResponse<IEnumerable<LibraryCategoryResponseDto>>), StatusCodes.Status200OK)]
    public async Task<ActionResult<ApiResponse<IEnumerable<LibraryCategoryResponseDto>>>> GetCategories()
    {
        var result = await _service.GetCategoriesAsync();
        return Ok(ApiResponse<IEnumerable<LibraryCategoryResponseDto>>.SuccessResponse(result));
    }

    // ── Items ───────────────────────────────────────────────────────────────────

    /// <summary>Get all items for a category by slug (e.g. "interventions").</summary>
    [HttpGet("categories/{slug}/items")]
    [ProducesResponseType(typeof(ApiResponse<IEnumerable<LibraryItemResponseDto>>), StatusCodes.Status200OK)]
    public async Task<ActionResult<ApiResponse<IEnumerable<LibraryItemResponseDto>>>> GetByCategory(string slug)
    {
        var result = await _service.GetByCategorySlugAsync(slug);
        return Ok(ApiResponse<IEnumerable<LibraryItemResponseDto>>.SuccessResponse(result));
    }

    /// <summary>Get a single library item with its files.</summary>
    [HttpGet("items/{id:int}")]
    [ProducesResponseType(typeof(ApiResponse<LibraryItemResponseDto>), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public async Task<ActionResult<ApiResponse<LibraryItemResponseDto>>> GetById(int id)
    {
        var item = await _service.GetByIdAsync(id);
        if (item == null)
            return NotFound(ApiResponse<LibraryItemResponseDto>.NotFoundResponse($"Item {id} not found."));
        return Ok(ApiResponse<LibraryItemResponseDto>.SuccessResponse(item));
    }

    /// <summary>Create a library item. Requires canCreate permission (system users always allowed).</summary>
    [HttpPost("items")]
    [ProducesResponseType(typeof(ApiResponse<LibraryItemResponseDto>), StatusCodes.Status201Created)]
    public async Task<ActionResult<ApiResponse<LibraryItemResponseDto>>> Create([FromBody] CreateLibraryItemRequestDto dto)
    {
        var actorId = await GetActorIdAsync();
        if (actorId == null)
            return Unauthorized(ApiResponse<LibraryItemResponseDto>.ErrorResponse("Unauthorized", null, 401));

        if (!await HasPermissionAsync(actorId.Value, "create"))
            return StatusCode(403, ApiResponse<LibraryItemResponseDto>.ErrorResponse("You don't have permission to create library items.", null, 403));

        try
        {
            var item = await _service.CreateAsync(dto, actorId.Value);
            return StatusCode(201, ApiResponse<LibraryItemResponseDto>.SuccessResponse(item, "Item created successfully", 201));
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error creating library item");
            return StatusCode(500, ApiResponse<LibraryItemResponseDto>.ErrorResponse("Internal server error", null, 500));
        }
    }

    /// <summary>Update a library item. Requires canEdit permission.</summary>
    [HttpPut("items/{id:int}")]
    [ProducesResponseType(typeof(ApiResponse<LibraryItemResponseDto>), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public async Task<ActionResult<ApiResponse<LibraryItemResponseDto>>> Update(int id, [FromBody] UpdateLibraryItemRequestDto dto)
    {
        var actorId = await GetActorIdAsync();
        if (actorId == null)
            return Unauthorized(ApiResponse<LibraryItemResponseDto>.ErrorResponse("Unauthorized", null, 401));

        if (!await HasPermissionAsync(actorId.Value, "edit"))
            return StatusCode(403, ApiResponse<LibraryItemResponseDto>.ErrorResponse("You don't have permission to edit library items.", null, 403));

        try
        {
            var item = await _service.UpdateAsync(id, dto);
            if (item == null)
                return NotFound(ApiResponse<LibraryItemResponseDto>.NotFoundResponse($"Item {id} not found."));
            return Ok(ApiResponse<LibraryItemResponseDto>.SuccessResponse(item, "Item updated successfully"));
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error updating library item {Id}", id);
            return StatusCode(500, ApiResponse<LibraryItemResponseDto>.ErrorResponse("Internal server error", null, 500));
        }
    }

    /// <summary>Soft-delete a library item. Requires canDelete permission.</summary>
    [HttpDelete("items/{id:int}")]
    [ProducesResponseType(typeof(ApiResponse<object>), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public async Task<ActionResult<ApiResponse<object>>> Delete(int id)
    {
        var actorId = await GetActorIdAsync();
        if (actorId == null)
            return Unauthorized(ApiResponse<object>.ErrorResponse("Unauthorized", null, 401));

        if (!await HasPermissionAsync(actorId.Value, "delete"))
            return StatusCode(403, ApiResponse<object>.ErrorResponse("You don't have permission to delete library items.", null, 403));

        var deleted = await _service.DeleteAsync(id, actorId.Value);
        if (!deleted)
            return NotFound(ApiResponse<object>.NotFoundResponse($"Item {id} not found."));

        return Ok(ApiResponse<object>.SuccessResponse(new { }, "Item deleted successfully"));
    }

    // ── Files ───────────────────────────────────────────────────────────────────

    /// <summary>Upload a file for a library item. Requires canCreate permission.</summary>
    [HttpPost("items/{id:int}/files")]
    [Consumes("multipart/form-data")]
    [ProducesResponseType(typeof(ApiResponse<LibraryItemFileResponseDto>), StatusCodes.Status201Created)]
    public async Task<ActionResult<ApiResponse<LibraryItemFileResponseDto>>> UploadFile(int id, IFormFile file)
    {
        var actorId = await GetActorIdAsync();
        if (actorId == null)
            return Unauthorized(ApiResponse<LibraryItemFileResponseDto>.ErrorResponse("Unauthorized", null, 401));

        if (!await HasPermissionAsync(actorId.Value, "create"))
            return StatusCode(403, ApiResponse<LibraryItemFileResponseDto>.ErrorResponse("You don't have permission to upload files.", null, 403));

        if (file == null || file.Length == 0)
            return BadRequest(ApiResponse<LibraryItemFileResponseDto>.ErrorResponse("No file provided."));

        try
        {
            var result = await _service.UploadFileAsync(id, file, actorId.Value);
            return StatusCode(201, ApiResponse<LibraryItemFileResponseDto>.SuccessResponse(result, "File uploaded successfully", 201));
        }
        catch (KeyNotFoundException ex)
        {
            return NotFound(ApiResponse<LibraryItemFileResponseDto>.NotFoundResponse(ex.Message));
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error uploading file for library item {Id}", id);
            return StatusCode(500, ApiResponse<LibraryItemFileResponseDto>.ErrorResponse("Internal server error", null, 500));
        }
    }

    /// <summary>Download a file attached to a library item.</summary>
    [HttpGet("items/{id:int}/files/{associationId:int}/download")]
    public async Task<IActionResult> DownloadFile(int id, int associationId)
    {
        try
        {
            var result = await _service.DownloadFileAsync(id, associationId);
            if (result == null) return NotFound();
            return File(result.Value.Stream, result.Value.ContentType, result.Value.FileName);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error downloading file {AssociationId} for library item {Id}", associationId, id);
            return StatusCode(500, "Error retrieving file from storage.");
        }
    }

    /// <summary>Delete a file from a library item. Requires canDelete permission.</summary>
    [HttpDelete("items/{id:int}/files/{associationId:int}")]
    [ProducesResponseType(typeof(ApiResponse<object>), StatusCodes.Status200OK)]
    public async Task<ActionResult<ApiResponse<object>>> DeleteFile(int id, int associationId)
    {
        var actorId = await GetActorIdAsync();
        if (actorId == null)
            return Unauthorized(ApiResponse<object>.ErrorResponse("Unauthorized", null, 401));

        if (!await HasPermissionAsync(actorId.Value, "delete"))
            return StatusCode(403, ApiResponse<object>.ErrorResponse("You don't have permission to delete files.", null, 403));

        var deleted = await _service.DeleteFileAsync(id, associationId, actorId.Value);
        if (!deleted)
            return NotFound(ApiResponse<object>.NotFoundResponse("File association not found."));

        return Ok(ApiResponse<object>.SuccessResponse(new { }, "File deleted successfully"));
    }

    // ── Permissions ──────────────────────────────────────────────────────────────

    /// <summary>
    /// Returns the current user's library permissions.
    /// System users always receive full access (canCreate/canEdit/canDelete = true).
    /// </summary>
    [HttpGet("my-permissions")]
    [ProducesResponseType(typeof(ApiResponse<LibraryMyPermissionsResponseDto>), StatusCodes.Status200OK)]
    public async Task<ActionResult<ApiResponse<LibraryMyPermissionsResponseDto>>> GetMyPermissions()
    {
        if (IsSystemUser())
        {
            return Ok(ApiResponse<LibraryMyPermissionsResponseDto>.SuccessResponse(
                new LibraryMyPermissionsResponseDto { CanCreate = true, CanEdit = true, CanDelete = true }));
        }

        var actorId = await GetActorIdAsync();
        if (actorId == null)
            return Unauthorized(ApiResponse<LibraryMyPermissionsResponseDto>.ErrorResponse("Unauthorized", null, 401));

        var perm = await _context.library_permissions.FirstOrDefaultAsync(p => p.actorId == actorId.Value);

        return Ok(ApiResponse<LibraryMyPermissionsResponseDto>.SuccessResponse(new LibraryMyPermissionsResponseDto
        {
            CanCreate = perm?.canCreate ?? false,
            CanEdit   = perm?.canEdit   ?? false,
            CanDelete = perm?.canDelete ?? false,
        }));
    }

    /// <summary>List all library permission assignments. System users only.</summary>
    [HttpGet("permissions")]
    [ProducesResponseType(typeof(ApiResponse<IEnumerable<LibraryPermissionResponseDto>>), StatusCodes.Status200OK)]
    public async Task<ActionResult<ApiResponse<IEnumerable<LibraryPermissionResponseDto>>>> GetPermissions()
    {
        if (!IsSystemUser())
            return StatusCode(403, ApiResponse<IEnumerable<LibraryPermissionResponseDto>>.ErrorResponse("System users only.", null, 403));

        var perms = await _context.library_permissions.ToListAsync();
        var actorIds = perms.Select(p => p.actorId)
            .Concat(perms.Select(p => p.assignedBy))
            .Distinct()
            .ToList();

        var actors = await _context.Actors
            .Where(a => actorIds.Contains(a.Id))
            .Select(a => new { a.Id, a.FullName, a.Email })
            .ToDictionaryAsync(a => a.Id);

        var result = perms.Select(p =>
        {
            actors.TryGetValue(p.actorId,    out var actor);
            actors.TryGetValue(p.assignedBy, out var assigner);
            return new LibraryPermissionResponseDto
            {
                Id             = p.id,
                ActorId        = p.actorId,
                ActorName      = actor?.FullName ?? "Unknown",
                ActorEmail     = actor?.Email,
                CanCreate      = p.canCreate,
                CanEdit        = p.canEdit,
                CanDelete      = p.canDelete,
                AssignedBy     = p.assignedBy,
                AssignedByName = assigner?.FullName ?? "Unknown",
                AssignedAt     = p.assignedAt,
            };
        });

        return Ok(ApiResponse<IEnumerable<LibraryPermissionResponseDto>>.SuccessResponse(result));
    }

    /// <summary>
    /// Create or update library permissions for a user. System users only.
    /// If a record already exists for the actorId it is updated; otherwise a new one is created.
    /// </summary>
    [HttpPost("permissions")]
    [ProducesResponseType(typeof(ApiResponse<LibraryPermissionResponseDto>), StatusCodes.Status200OK)]
    public async Task<ActionResult<ApiResponse<LibraryPermissionResponseDto>>> UpsertPermission(
        [FromBody] UpsertLibraryPermissionRequestDto dto)
    {
        if (!IsSystemUser())
            return StatusCode(403, ApiResponse<LibraryPermissionResponseDto>.ErrorResponse("System users only.", null, 403));

        var assignedByActorId = await GetActorIdAsync();
        if (assignedByActorId == null)
            return Unauthorized(ApiResponse<LibraryPermissionResponseDto>.ErrorResponse("Unauthorized", null, 401));

        var existing = await _context.library_permissions.FirstOrDefaultAsync(p => p.actorId == dto.ActorId);

        if (existing != null)
        {
            existing.canCreate  = dto.CanCreate;
            existing.canEdit    = dto.CanEdit;
            existing.canDelete  = dto.CanDelete;
            existing.assignedBy = assignedByActorId.Value;
            existing.assignedAt = DateTime.UtcNow;
        }
        else
        {
            existing = new library_permissions
            {
                actorId    = dto.ActorId,
                canCreate  = dto.CanCreate,
                canEdit    = dto.CanEdit,
                canDelete  = dto.CanDelete,
                assignedBy = assignedByActorId.Value,
                assignedAt = DateTime.UtcNow,
            };
            _context.library_permissions.Add(existing);
        }

        await _context.SaveChangesAsync();

        var actor = await _context.Actors.FindAsync(dto.ActorId);
        var assignedByActor = await _context.Actors.FindAsync(assignedByActorId.Value);

        return Ok(ApiResponse<LibraryPermissionResponseDto>.SuccessResponse(new LibraryPermissionResponseDto
        {
            Id             = existing.id,
            ActorId        = existing.actorId,
            ActorName      = actor?.FullName ?? "Unknown",
            ActorEmail     = actor?.Email,
            CanCreate      = existing.canCreate,
            CanEdit        = existing.canEdit,
            CanDelete      = existing.canDelete,
            AssignedBy     = existing.assignedBy,
            AssignedByName = assignedByActor?.FullName ?? "Unknown",
            AssignedAt     = existing.assignedAt,
        }));
    }

    /// <summary>Revoke all library permissions for a user. System users only.</summary>
    [HttpDelete("permissions/{actorId:int}")]
    [ProducesResponseType(typeof(ApiResponse<object>), StatusCodes.Status200OK)]
    public async Task<ActionResult<ApiResponse<object>>> RevokePermission(int actorId)
    {
        if (!IsSystemUser())
            return StatusCode(403, ApiResponse<object>.ErrorResponse("System users only.", null, 403));

        var perm = await _context.library_permissions.FirstOrDefaultAsync(p => p.actorId == actorId);
        if (perm == null)
            return NotFound(ApiResponse<object>.NotFoundResponse("Permission record not found."));

        _context.library_permissions.Remove(perm);
        await _context.SaveChangesAsync();

        return Ok(ApiResponse<object>.SuccessResponse(new { }, "Permissions revoked successfully"));
    }
}
