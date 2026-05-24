using System.IdentityModel.Tokens.Jwt;
using System.Security.Claims;
using System.Text;
using Microsoft.Extensions.Configuration;
using Microsoft.IdentityModel.Tokens;
using TherapuHubAPI.DTOs.Requests.Auth;
using TherapuHubAPI.DTOs.Responses.Auth;
using TherapuHubAPI.Repositorio;
using TherapuHubAPI.Repositorio.IRepositorio;
using TherapuHubAPI.Models;
using TherapuHubAPI.Services.IServices;

namespace TherapuHubAPI.Services.Implementations;

public class AuthService : IAuthService
{
    private readonly IUsuarioRepositorio _usuarioRepositorio;
    private readonly ITipoUsuarioRepositorio _tipoUsuarioRepositorio;
    private readonly ICompaniaRepositorio _companiaRepositorio;
    private readonly IConfiguration _configuration;
    private readonly ILogger<AuthService> _logger;

    public AuthService(
        IUsuarioRepositorio usuarioRepositorio,
        ITipoUsuarioRepositorio tipoUsuarioRepositorio,
        ICompaniaRepositorio companiaRepositorio,
        IConfiguration configuration,
        ILogger<AuthService> logger)
    {
        _usuarioRepositorio = usuarioRepositorio;
        _tipoUsuarioRepositorio = tipoUsuarioRepositorio;
        _companiaRepositorio = companiaRepositorio;
        _configuration = configuration;
        _logger = logger;
    }

    public async Task<LoginResponseDto?> LoginAsync(LoginRequestDto request)
    {
        _logger.LogInformation("Intento de login para correo: {Correo}", request.Correo);

        var usuario = await _usuarioRepositorio.GetByCorreoAsync(request.Correo);

        if (usuario == null)
        {
            _logger.LogWarning("Intento de login fallido: Usuario no encontrado - {Correo}", request.Correo);
            return null;
        }

        if (!usuario.IsActive)
        {
            _logger.LogWarning("Intento de login fallido: Usuario inactivo - {Correo}", request.Correo);
            throw new UnauthorizedAccessException("The user is not active in the system");
        }

        var compania = await _companiaRepositorio.GetByIdCompaniaAsync(usuario.Actor.CompanyId);
        if (compania != null && !compania.IsActive)
        {
            _logger.LogWarning("Login failed: Company is inactive for user {Correo}. CompanyId: {CompanyId}", request.Correo, usuario.Actor.CompanyId);
            throw new UnauthorizedAccessException("The company associated with this account is not active");
        }

        if (!BCrypt.Net.BCrypt.Verify(request.Contrasena, usuario.PasswordHash))
        {
            _logger.LogWarning("Login failed: incorrect password - {Correo}", request.Correo);
            return null;
        }

        var tipoUsuario = await _tipoUsuarioRepositorio.GetByIdAsync(usuario.UserTypeId);
        var tipoUsuarioNombre = tipoUsuario?.Name ?? string.Empty;

        var token = GenerateJwtToken(usuario, tipoUsuarioNombre, tipoUsuario?.IsSystem == true);

        const string defaultPassword = "123456";
        bool requiresPasswordReset = usuario.MustResetPassword && request.Contrasena == defaultPassword;

        _logger.LogInformation("Login successful for user: {Correo}", request.Correo);

        return new LoginResponseDto
        {
            Id = usuario.Id,
            Token = token,
            Correo = usuario.Actor.Email ?? string.Empty,
            Nombre = usuario.Actor.FullName,
            UserTypeId = usuario.UserTypeId,
            TipoUsuarioNombre = tipoUsuarioNombre,
            ExpiraEn = DateTime.UtcNow.AddHours(24),
            RequiresPasswordReset = requiresPasswordReset,
            EsSistema = tipoUsuario?.IsSystem == true,
            ActorId = usuario.ActorId
        };
    }

    private string GenerateJwtToken(Models.Users usuario, string tipoUsuarioNombre, bool isSystem)
    {
        var jwtKey = _configuration["Jwt:Key"] ?? throw new InvalidOperationException("JWT Key not configured");
        var jwtIssuer = _configuration["Jwt:Issuer"] ?? "TherapuHubAPI";
        var jwtAudience = _configuration["Jwt:Audience"] ?? "TherapuHubAPI";
        var jwtExpirationHours = int.Parse(_configuration["Jwt:ExpirationHours"] ?? "24");

        var key = new SymmetricSecurityKey(Encoding.UTF8.GetBytes(jwtKey));
        var credentials = new SigningCredentials(key, SecurityAlgorithms.HmacSha256);

        var claims = new[]
        {
            new Claim(ClaimTypes.NameIdentifier, usuario.Id.ToString()),
            new Claim(ClaimTypes.Email, usuario.Actor.Email ?? string.Empty),
            new Claim(ClaimTypes.Name, usuario.Actor.FullName),
            new Claim("UserTypeId", usuario.UserTypeId.ToString()),
            new Claim("TipoUsuario", tipoUsuarioNombre),
            new Claim(ClaimTypes.Role, tipoUsuarioNombre),
            new Claim("CompanyId", usuario.Actor.CompanyId.ToString()),
            new Claim("IsSystem", isSystem.ToString().ToLower()),
            new Claim(JwtRegisteredClaimNames.Jti, Guid.NewGuid().ToString())
        };

        var token = new JwtSecurityToken(
            issuer: jwtIssuer,
            audience: jwtAudience,
            claims: claims,
            expires: DateTime.UtcNow.AddHours(jwtExpirationHours),
            signingCredentials: credentials
        );

        return new JwtSecurityTokenHandler().WriteToken(token);
    }
}
