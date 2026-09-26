using System.Security.Claims;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using TherapuHubAPI.DTOs.Common;
using TherapuHubAPI.DTOs.Requests.BehaviorFunctions;
using TherapuHubAPI.DTOs.Responses.BehaviorFunctions;
using TherapuHubAPI.Models;
using TherapuHubAPI.Services.IServices;

namespace TherapuHubAPI.Controllers;

[Authorize]
[ApiController]
[Route("api/[controller]")]
[Produces("application/json")]
public class BehaviorFunctionsController : ControllerBase
{
    private readonly IBehaviorFunctionsService _service;
    private readonly ContextDB _context;
    private readonly ILogger<BehaviorFunctionsController> _logger;

    public BehaviorFunctionsController(
        IBehaviorFunctionsService service,
        ContextDB context,
        ILogger<BehaviorFunctionsController> logger)
    {
        _service = service;
        _context = context;
        _logger = logger;
    }

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

    [HttpGet]
    [ProducesResponseType(typeof(ApiResponse<IEnumerable<BehaviorFunctionResponseDto>>), StatusCodes.Status200OK)]
    public async Task<ActionResult<ApiResponse<IEnumerable<BehaviorFunctionResponseDto>>>> GetAll()
    {
        var result = await _service.GetAllAsync();
        return Ok(ApiResponse<IEnumerable<BehaviorFunctionResponseDto>>.SuccessResponse(result, "Functions retrieved successfully"));
    }

    [HttpGet("active")]
    [ProducesResponseType(typeof(ApiResponse<IEnumerable<BehaviorFunctionResponseDto>>), StatusCodes.Status200OK)]
    public async Task<ActionResult<ApiResponse<IEnumerable<BehaviorFunctionResponseDto>>>> GetActive()
    {
        var result = await _service.GetActiveAsync();
        return Ok(ApiResponse<IEnumerable<BehaviorFunctionResponseDto>>.SuccessResponse(result, "Active functions retrieved successfully"));
    }

    [HttpGet("{id:int}")]
    [ProducesResponseType(typeof(ApiResponse<BehaviorFunctionResponseDto>), StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(ApiResponse<BehaviorFunctionResponseDto>), StatusCodes.Status404NotFound)]
    public async Task<ActionResult<ApiResponse<BehaviorFunctionResponseDto>>> GetById(int id)
    {
        var result = await _service.GetByIdAsync(id);
        if (result == null)
            return NotFound(ApiResponse<BehaviorFunctionResponseDto>.ErrorResponse("Function not found", null, 404));
        return Ok(ApiResponse<BehaviorFunctionResponseDto>.SuccessResponse(result));
    }

    [HttpPost]
    [ProducesResponseType(typeof(ApiResponse<BehaviorFunctionResponseDto>), StatusCodes.Status201Created)]
    [ProducesResponseType(typeof(ApiResponse<BehaviorFunctionResponseDto>), StatusCodes.Status400BadRequest)]
    [ProducesResponseType(StatusCodes.Status401Unauthorized)]
    public async Task<ActionResult<ApiResponse<BehaviorFunctionResponseDto>>> Create([FromBody] CreateBehaviorFunctionRequestDto request)
    {
        var actorId = await GetActorIdAsync();
        if (actorId == null)
            return Unauthorized(ApiResponse<BehaviorFunctionResponseDto>.ErrorResponse("Unauthorized", null, 401));

        try
        {
            var created = await _service.CreateAsync(request, actorId.Value);
            return CreatedAtAction(nameof(GetById), new { id = created.Id },
                ApiResponse<BehaviorFunctionResponseDto>.SuccessResponse(created, "Function created successfully", 201));
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error creating function");
            return StatusCode(500, ApiResponse<BehaviorFunctionResponseDto>.ErrorResponse("Internal server error", null, 500));
        }
    }

    [HttpPut("{id:int}")]
    [ProducesResponseType(typeof(ApiResponse<BehaviorFunctionResponseDto>), StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(ApiResponse<BehaviorFunctionResponseDto>), StatusCodes.Status404NotFound)]
    public async Task<ActionResult<ApiResponse<BehaviorFunctionResponseDto>>> Update(int id, [FromBody] UpdateBehaviorFunctionRequestDto request)
    {
        try
        {
            var updated = await _service.UpdateAsync(id, request);
            if (updated == null)
                return NotFound(ApiResponse<BehaviorFunctionResponseDto>.ErrorResponse("Function not found", null, 404));
            return Ok(ApiResponse<BehaviorFunctionResponseDto>.SuccessResponse(updated, "Function updated successfully"));
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error updating function {Id}", id);
            return StatusCode(500, ApiResponse<BehaviorFunctionResponseDto>.ErrorResponse("Internal server error", null, 500));
        }
    }

    [HttpDelete("{id:int}")]
    [ProducesResponseType(typeof(ApiResponse<object>), StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(ApiResponse<object>), StatusCodes.Status404NotFound)]
    [ProducesResponseType(StatusCodes.Status401Unauthorized)]
    public async Task<ActionResult<ApiResponse<object>>> Delete(int id)
    {
        var actorId = await GetActorIdAsync();
        if (actorId == null)
            return Unauthorized(ApiResponse<object>.ErrorResponse("Unauthorized", null, 401));

        try
        {
            var deleted = await _service.DeleteAsync(id, actorId.Value);
            if (!deleted)
                return NotFound(ApiResponse<object>.ErrorResponse("Function not found", null, 404));
            return Ok(ApiResponse<object>.SuccessResponse(null, "Function deleted successfully"));
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error deleting function {Id}", id);
            return StatusCode(500, ApiResponse<object>.ErrorResponse("Internal server error", null, 500));
        }
    }

    [HttpPatch("{id:int}/toggle-active")]
    [ProducesResponseType(typeof(ApiResponse<BehaviorFunctionResponseDto>), StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(ApiResponse<BehaviorFunctionResponseDto>), StatusCodes.Status404NotFound)]
    public async Task<ActionResult<ApiResponse<BehaviorFunctionResponseDto>>> ToggleActive(int id)
    {
        try
        {
            var result = await _service.ToggleActiveAsync(id);
            if (result == null)
                return NotFound(ApiResponse<BehaviorFunctionResponseDto>.ErrorResponse("Function not found", null, 404));
            return Ok(ApiResponse<BehaviorFunctionResponseDto>.SuccessResponse(result, "Function toggled successfully"));
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error toggling function {Id}", id);
            return StatusCode(500, ApiResponse<BehaviorFunctionResponseDto>.ErrorResponse("Internal server error", null, 500));
        }
    }
}
