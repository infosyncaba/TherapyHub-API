using System.Security.Claims;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using TherapuHubAPI.DTOs.Common;
using TherapuHubAPI.DTOs.Requests.Auth;
using TherapuHubAPI.DTOs.Responses.Auth;
using TherapuHubAPI.Services.IServices;

namespace TherapuHubAPI.Controllers;

[ApiController]
[Route("api/[controller]")]
[Produces("application/json")]
public class AuthController : ControllerBase
{
    private readonly IAuthService _authService;
    private readonly IUsuarioService _usuarioService;
    private readonly ILogger<AuthController> _logger;

    public AuthController(
        IAuthService authService,
        IUsuarioService usuarioService,
        ILogger<AuthController> logger)
    {
        _authService = authService;
        _usuarioService = usuarioService;
        _logger = logger;
    }

    /// <summary>
    /// Autentica un usuario con correo y contraseña
    /// </summary>
    /// <param name="request">Credenciales de login</param>
    /// <returns>Token JWT y datos del usuario</returns>
    [HttpPost("login")]
    [ProducesResponseType(typeof(ApiResponse<LoginResponseDto>), StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(ApiResponse<LoginResponseDto>), StatusCodes.Status401Unauthorized)]
    [ProducesResponseType(typeof(ApiResponse<LoginResponseDto>), StatusCodes.Status400BadRequest)]
    public async Task<ActionResult<ApiResponse<LoginResponseDto>>> Login([FromBody] LoginRequestDto request)
    {
        try
        {
            var result = await _authService.LoginAsync(request);

            if (result == null)
            {
                return Unauthorized(ApiResponse<LoginResponseDto>.ErrorResponse(
                    "Invalid credentials",
                    new List<string> { "Email or password is incorrect" },
                    401));
            }

            return Ok(ApiResponse<LoginResponseDto>.SuccessResponse(
                result,
                "Login successful",
                200));
        }
        catch (UnauthorizedAccessException ex)
        {
            _logger.LogWarning(ex, "Intento de login con usuario inactivo: {Correo}", request.Correo);
            return Unauthorized(ApiResponse<LoginResponseDto>.ErrorResponse(
                ex.Message,
                new List<string> { ex.Message },
                401));
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error processing login for email: {Correo}", request.Correo);
            return StatusCode(500, ApiResponse<LoginResponseDto>.ErrorResponse(
                "Internal server error",
                new List<string> { "An error occurred while processing the request" },
                500));
        }
    }

    /// <summary>
    /// Issues a new short-lived token while the user is active (sliding session for idle logout).
    /// </summary>
    [HttpPost("refresh")]
    [Authorize]
    [ProducesResponseType(typeof(ApiResponse<LoginResponseDto>), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status401Unauthorized)]
    public async Task<ActionResult<ApiResponse<LoginResponseDto>>> Refresh()
    {
        try
        {
            var userIdClaim = User.FindFirst(ClaimTypes.NameIdentifier)?.Value;
            if (string.IsNullOrEmpty(userIdClaim) || !int.TryParse(userIdClaim, out int userId))
            {
                return Unauthorized();
            }

            var result = await _authService.RefreshTokenAsync(userId);
            if (result == null)
            {
                return Unauthorized(ApiResponse<LoginResponseDto>.ErrorResponse(
                    "Session is no longer valid",
                    new List<string> { "User or company is inactive" },
                    401));
            }

            return Ok(ApiResponse<LoginResponseDto>.SuccessResponse(
                result,
                "Token refreshed",
                200));
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error refreshing token");
            return StatusCode(500, ApiResponse<LoginResponseDto>.ErrorResponse(
                "Internal server error",
                new List<string> { "An error occurred while processing the request" },
                500));
        }
    }

    /// <summary>
    /// Sets the initial password when user has logged in with default password (123456) and MustResetPassword is true.
    /// </summary>
    [HttpPost("set-initial-password")]
    [Authorize]
    [ProducesResponseType(typeof(ApiResponse<object>), StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(ApiResponse<object>), StatusCodes.Status400BadRequest)]
    [ProducesResponseType(StatusCodes.Status401Unauthorized)]
    public async Task<ActionResult<ApiResponse<object>>> SetInitialPassword([FromBody] SetInitialPasswordRequestDto request)
    {
        try
        {
            var userIdClaim = User.FindFirst(ClaimTypes.NameIdentifier)?.Value;
            if (string.IsNullOrEmpty(userIdClaim) || !int.TryParse(userIdClaim, out int userId))
            {
                return Unauthorized();
            }

            await _usuarioService.SetInitialPasswordAsync(userId, request);
            return Ok(ApiResponse<object>.SuccessResponse(
                null,
                "Password has been set successfully",
                200));
        }
        catch (InvalidOperationException ex)
        {
            _logger.LogWarning(ex, "Validation error setting initial password");
            return BadRequest(ApiResponse<object>.ErrorResponse(
                ex.Message,
                new List<string> { ex.Message },
                400));
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error setting initial password");
            return StatusCode(500, ApiResponse<object>.ErrorResponse(
                "Internal server error",
                new List<string> { "An error occurred while processing the request" },
                500));
        }
    }
}
