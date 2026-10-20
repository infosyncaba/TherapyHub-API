using TherapuHubAPI.DTOs.Requests.Auth;
using TherapuHubAPI.DTOs.Responses.Auth;

namespace TherapuHubAPI.Services.IServices;

public interface IAuthService
{
    Task<LoginResponseDto?> LoginAsync(LoginRequestDto request);
    Task<LoginResponseDto?> RefreshTokenAsync(int userId);
}
