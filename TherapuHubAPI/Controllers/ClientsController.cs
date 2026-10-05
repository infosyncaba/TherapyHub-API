using System.Security.Claims;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using TherapuHubAPI.DTOs.Common;
using TherapuHubAPI.DTOs.Requests.Clients;
using TherapuHubAPI.DTOs.Responses.Clients;
using TherapuHubAPI.Services.IServices;

namespace TherapuHubAPI.Controllers;

[Authorize]
[ApiController]
[Route("api/[controller]")]
[Produces("application/json")]
public class ClientsController : ControllerBase
{
    private readonly IClientService _clientService;
    private readonly ILogger<ClientsController> _logger;

    public ClientsController(IClientService clientService, ILogger<ClientsController> logger)
    {
        _clientService = clientService;
        _logger = logger;
    }

    private int? GetCompanyId()
    {
        var claim = User.FindFirst("CompanyId");
        if (claim == null || !int.TryParse(claim.Value, out var companyId))
            return null;
        return companyId;
    }

    private int? GetUserId()
    {
        var claim = User.FindFirst(ClaimTypes.NameIdentifier);
        if (claim == null || !int.TryParse(claim.Value, out var id)) return null;
        return id;
    }

    /// <summary>Get all clients for the current company. System users see all; others see only clients with no RBT or where the RBT is related to them.</summary>
    [HttpGet]
    [ProducesResponseType(typeof(ApiResponse<IEnumerable<ClientResponseDto>>), StatusCodes.Status200OK)]
    public async Task<ActionResult<ApiResponse<IEnumerable<ClientResponseDto>>>> GetAll()
    {
        var companyId = GetCompanyId();
        if (companyId == null)
            return Unauthorized(ApiResponse<IEnumerable<ClientResponseDto>>.ErrorResponse("CompanyId not found", null, 401));

        var userId = GetUserId();
        if (userId == null)
            return Unauthorized(ApiResponse<IEnumerable<ClientResponseDto>>.ErrorResponse("UserId not found", null, 401));

        var result = await _clientService.GetByCompanyIdAsync(companyId.Value, userId.Value);
        return Ok(ApiResponse<IEnumerable<ClientResponseDto>>.SuccessResponse(result, "Clients retrieved successfully", 200));
    }

    /// <summary>Get client by id.</summary>
    [HttpGet("{id}")]
    [ProducesResponseType(typeof(ApiResponse<ClientResponseDto>), StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(ApiResponse<object>), StatusCodes.Status404NotFound)]
    public async Task<ActionResult<ApiResponse<ClientResponseDto>>> GetById(int id)
    {
        var companyId = GetCompanyId();
        if (companyId == null)
            return Unauthorized(ApiResponse<ClientResponseDto>.ErrorResponse("CompanyId not found", null, 401));

        var result = await _clientService.GetByIdAsync(id, companyId.Value);
        if (result == null)
            return NotFound(ApiResponse<ClientResponseDto>.NotFoundResponse($"Client with Id {id} not found"));
        return Ok(ApiResponse<ClientResponseDto>.SuccessResponse(result, "Client retrieved successfully", 200));
    }

    /// <summary>Get all client statuses (for dropdowns).</summary>
    [HttpGet("statuses")]
    [ProducesResponseType(typeof(ApiResponse<IEnumerable<ClientStatusResponseDto>>), StatusCodes.Status200OK)]
    public async Task<ActionResult<ApiResponse<IEnumerable<ClientStatusResponseDto>>>> GetStatuses()
    {
        var result = await _clientService.GetAllStatusesAsync();
        return Ok(ApiResponse<IEnumerable<ClientStatusResponseDto>>.SuccessResponse(result, "Statuses retrieved", 200));
    }

    /// <summary>Create a new client.</summary>
    [HttpPost]
    [ProducesResponseType(typeof(ApiResponse<ClientResponseDto>), StatusCodes.Status201Created)]
    [ProducesResponseType(typeof(ApiResponse<ClientResponseDto>), StatusCodes.Status400BadRequest)]
    public async Task<ActionResult<ApiResponse<ClientResponseDto>>> Create([FromBody] CreateClientRequestDto request)
    {
        var companyId = GetCompanyId();
        if (companyId == null)
            return Unauthorized(ApiResponse<ClientResponseDto>.ErrorResponse("CompanyId not found", null, 401));

        try
        {
            var result = await _clientService.CreateAsync(request, companyId.Value);
            return CreatedAtAction(nameof(GetById), new { id = result.Id },
                ApiResponse<ClientResponseDto>.SuccessResponse(result, "Client created successfully", 201));
        }
        catch (InvalidOperationException ex)
        {
            _logger.LogWarning(ex, "Validation error creating client");
            return BadRequest(ApiResponse<ClientResponseDto>.ErrorResponse(ex.Message, new List<string> { ex.Message }, 400));
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error creating client");
            return StatusCode(500, ApiResponse<ClientResponseDto>.ErrorResponse(
                "Internal server error", new List<string> { "An error occurred while processing the request" }, 500));
        }
    }

    /// <summary>Update an existing client.</summary>
    [HttpPut("{id}")]
    [ProducesResponseType(typeof(ApiResponse<ClientResponseDto>), StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(ApiResponse<object>), StatusCodes.Status404NotFound)]
    public async Task<ActionResult<ApiResponse<ClientResponseDto>>> Update(int id, [FromBody] UpdateClientRequestDto request)
    {
        var companyId = GetCompanyId();
        if (companyId == null)
            return Unauthorized(ApiResponse<ClientResponseDto>.ErrorResponse("CompanyId not found", null, 401));

        try
        {
            var result = await _clientService.UpdateAsync(id, request, companyId.Value);
            if (result == null)
                return NotFound(ApiResponse<ClientResponseDto>.NotFoundResponse($"Client with Id {id} not found"));
            return Ok(ApiResponse<ClientResponseDto>.SuccessResponse(result, "Client updated successfully", 200));
        }
        catch (InvalidOperationException ex)
        {
            _logger.LogWarning(ex, "Validation error updating client");
            return BadRequest(ApiResponse<ClientResponseDto>.ErrorResponse(ex.Message, new List<string> { ex.Message }, 400));
        }
    }

    /// <summary>Delete a client.</summary>
    [HttpDelete("{id}")]
    [ProducesResponseType(typeof(ApiResponse<object>), StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(ApiResponse<object>), StatusCodes.Status404NotFound)]
    public async Task<ActionResult<ApiResponse<object>>> Delete(int id)
    {
        var companyId = GetCompanyId();
        if (companyId == null)
            return Unauthorized(ApiResponse<object>.ErrorResponse("CompanyId not found", null, 401));

        var success = await _clientService.DeleteAsync(id, companyId.Value);
        if (!success)
            return NotFound(ApiResponse<object>.NotFoundResponse($"Client with Id {id} not found"));
        return Ok(ApiResponse<object>.SuccessResponse(null, "Client deleted successfully", 200));
    }
}
