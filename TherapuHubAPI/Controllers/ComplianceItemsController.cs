using System.Security.Claims;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using TherapuHubAPI.DTOs.Common;
using TherapuHubAPI.DTOs.Requests.ComplianceItems;
using TherapuHubAPI.DTOs.Responses.ComplianceItems;
using TherapuHubAPI.Services.IServices;

namespace TherapuHubAPI.Controllers;

[Authorize]
[ApiController]
[Route("api/[controller]")]
[Produces("application/json")]
public class ComplianceItemsController : ControllerBase
{
    private readonly IComplianceItemsService _service;
    private readonly ILogger<ComplianceItemsController> _logger;

    public ComplianceItemsController(
        IComplianceItemsService service,
        ILogger<ComplianceItemsController> logger)
    {
        _service = service;
        _logger = logger;
    }

    private int? GetUserId()
    {
        var claim = User.FindFirst(ClaimTypes.NameIdentifier);
        if (claim == null || !int.TryParse(claim.Value, out var id)) return null;
        return id;
    }

    private int? GetCompanyId()
    {
        var claim = User.FindFirst("CompanyId");
        if (claim == null || !int.TryParse(claim.Value, out var id)) return null;
        return id;
    }

    private static string? Validate(string? description, DateTime registrationDate, DateTime expirationDate)
    {
        if (string.IsNullOrWhiteSpace(description)) return "Description is required";
        if (description.Trim().Length > 500) return "Description must be 500 characters or less";
        if (registrationDate == default) return "Registration date is required";
        if (expirationDate == default) return "Expiration date is required";
        if (expirationDate.Date < registrationDate.Date) return "Expiration date cannot be before registration date";
        return null;
    }

    [HttpGet]
    [ProducesResponseType(typeof(ApiResponse<IEnumerable<ComplianceItemResponseDto>>), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status401Unauthorized)]
    public async Task<ActionResult<ApiResponse<IEnumerable<ComplianceItemResponseDto>>>> GetMine()
    {
        var userId = GetUserId();
        if (userId == null)
            return Unauthorized(ApiResponse<IEnumerable<ComplianceItemResponseDto>>.ErrorResponse("Unauthorized", null, 401));

        var result = await _service.GetMineAsync(userId.Value);
        return Ok(ApiResponse<IEnumerable<ComplianceItemResponseDto>>.SuccessResponse(result, "Records retrieved successfully"));
    }

    [HttpGet("{id:int}")]
    [ProducesResponseType(typeof(ApiResponse<ComplianceItemResponseDto>), StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(ApiResponse<ComplianceItemResponseDto>), StatusCodes.Status404NotFound)]
    public async Task<ActionResult<ApiResponse<ComplianceItemResponseDto>>> GetById(int id)
    {
        var userId = GetUserId();
        if (userId == null)
            return Unauthorized(ApiResponse<ComplianceItemResponseDto>.ErrorResponse("Unauthorized", null, 401));

        var result = await _service.GetByIdAsync(id, userId.Value);
        if (result == null)
            return NotFound(ApiResponse<ComplianceItemResponseDto>.ErrorResponse("Record not found", null, 404));
        return Ok(ApiResponse<ComplianceItemResponseDto>.SuccessResponse(result));
    }

    [HttpPost]
    [ProducesResponseType(typeof(ApiResponse<ComplianceItemResponseDto>), StatusCodes.Status201Created)]
    [ProducesResponseType(typeof(ApiResponse<ComplianceItemResponseDto>), StatusCodes.Status400BadRequest)]
    [ProducesResponseType(StatusCodes.Status401Unauthorized)]
    public async Task<ActionResult<ApiResponse<ComplianceItemResponseDto>>> Create([FromBody] CreateComplianceItemRequestDto request)
    {
        var userId = GetUserId();
        var companyId = GetCompanyId();
        if (userId == null || companyId == null)
            return Unauthorized(ApiResponse<ComplianceItemResponseDto>.ErrorResponse("Unauthorized", null, 401));

        var error = Validate(request.Description, request.RegistrationDate, request.ExpirationDate);
        if (error != null)
            return BadRequest(ApiResponse<ComplianceItemResponseDto>.ErrorResponse(error, null, 400));

        try
        {
            var created = await _service.CreateAsync(request, userId.Value, companyId.Value);
            return CreatedAtAction(nameof(GetById), new { id = created.Id },
                ApiResponse<ComplianceItemResponseDto>.SuccessResponse(created, "Record created successfully", 201));
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error creating compliance record");
            return StatusCode(500, ApiResponse<ComplianceItemResponseDto>.ErrorResponse("Internal server error", null, 500));
        }
    }

    [HttpPut("{id:int}")]
    [ProducesResponseType(typeof(ApiResponse<ComplianceItemResponseDto>), StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(ApiResponse<ComplianceItemResponseDto>), StatusCodes.Status400BadRequest)]
    [ProducesResponseType(typeof(ApiResponse<ComplianceItemResponseDto>), StatusCodes.Status404NotFound)]
    public async Task<ActionResult<ApiResponse<ComplianceItemResponseDto>>> Update(int id, [FromBody] UpdateComplianceItemRequestDto request)
    {
        var userId = GetUserId();
        if (userId == null)
            return Unauthorized(ApiResponse<ComplianceItemResponseDto>.ErrorResponse("Unauthorized", null, 401));

        var error = Validate(request.Description, request.RegistrationDate, request.ExpirationDate);
        if (error != null)
            return BadRequest(ApiResponse<ComplianceItemResponseDto>.ErrorResponse(error, null, 400));

        try
        {
            var updated = await _service.UpdateAsync(id, request, userId.Value);
            if (updated == null)
                return NotFound(ApiResponse<ComplianceItemResponseDto>.ErrorResponse("Record not found", null, 404));
            return Ok(ApiResponse<ComplianceItemResponseDto>.SuccessResponse(updated, "Record updated successfully"));
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error updating compliance record {Id}", id);
            return StatusCode(500, ApiResponse<ComplianceItemResponseDto>.ErrorResponse("Internal server error", null, 500));
        }
    }

    [HttpDelete("{id:int}")]
    [ProducesResponseType(typeof(ApiResponse<object>), StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(ApiResponse<object>), StatusCodes.Status404NotFound)]
    [ProducesResponseType(StatusCodes.Status401Unauthorized)]
    public async Task<ActionResult<ApiResponse<object>>> Delete(int id)
    {
        var userId = GetUserId();
        if (userId == null)
            return Unauthorized(ApiResponse<object>.ErrorResponse("Unauthorized", null, 401));

        try
        {
            var deleted = await _service.DeleteAsync(id, userId.Value);
            if (!deleted)
                return NotFound(ApiResponse<object>.ErrorResponse("Record not found", null, 404));
            return Ok(ApiResponse<object>.SuccessResponse(null, "Record deleted successfully"));
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error deleting compliance record {Id}", id);
            return StatusCode(500, ApiResponse<object>.ErrorResponse("Internal server error", null, 500));
        }
    }
}
