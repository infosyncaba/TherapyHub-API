using AutoMapper;
using Microsoft.EntityFrameworkCore;
using TherapuHubAPI.DTOs.Requests.Auth;
using TherapuHubAPI.DTOs.Requests.Users;
using TherapuHubAPI.DTOs.Responses.Users;
using TherapuHubAPI.Models;
using TherapuHubAPI.Repositorio;
using TherapuHubAPI.Repositorio.IRepositorio;
using TherapuHubAPI.Services.IServices;

namespace TherapuHubAPI.Services.Implementations;

public class UsuarioService : IUsuarioService
{
    private readonly IUsuarioRepositorio _usuarioRepositorio;
    private readonly ITipoUsuarioRepositorio _tipoUsuarioRepositorio;
    private readonly ICompaniaRepositorio _companiaRepositorio;
    private readonly IUnitOfWork _unitOfWork;
    private readonly ContextDB _context;
    private readonly IMapper _mapper;
    private readonly ILogger<UsuarioService> _logger;

    public UsuarioService(
        IUsuarioRepositorio usuarioRepositorio,
        ITipoUsuarioRepositorio tipoUsuarioRepositorio,
        ICompaniaRepositorio companiaRepositorio,
        IUnitOfWork unitOfWork,
        ContextDB context,
        IMapper mapper,
        ILogger<UsuarioService> logger)
    {
        _usuarioRepositorio = usuarioRepositorio;
        _tipoUsuarioRepositorio = tipoUsuarioRepositorio;
        _companiaRepositorio = companiaRepositorio;
        _unitOfWork = unitOfWork;
        _context = context;
        _mapper = mapper;
        _logger = logger;
    }

    public async Task<UsuarioResponseDto> CreateAsync(CreateUsuarioRequestDto request, int currentUserId)
    {
        var currentUser = await _usuarioRepositorio.GetByIdAsync(currentUserId);
        if (currentUser == null)
        {
            _logger.LogWarning("Current user not found. Id: {CurrentUserId}", currentUserId);
            throw new InvalidOperationException("Current user could not be determined. Please log in again.");
        }

        var currentUserTipo = await _tipoUsuarioRepositorio.GetByIdAsync(currentUser.UserTypeId);
        bool currentUserEsSistema = currentUserTipo?.IsSystem == true;

        int targetCompaniaId;
        if (currentUserEsSistema && request.CompanyId.HasValue && request.CompanyId.Value > 0)
        {
            var compania = await _companiaRepositorio.GetByIdCompaniaAsync(request.CompanyId.Value);
            if (compania == null)
            {
                _logger.LogWarning("Company not found for create user. CompanyId: {CompanyId}", request.CompanyId.Value);
                throw new InvalidOperationException($"Company with Id {request.CompanyId.Value} does not exist.");
            }
            targetCompaniaId = request.CompanyId.Value;
        }
        else
        {
            targetCompaniaId = currentUser.Actor.CompanyId;
        }

        _logger.LogInformation("Creating new user with email: {Correo}, company: {CompanyId}", request.Correo, targetCompaniaId);

        // Check UserLimit for the target company
        var targetCompania = await _companiaRepositorio.GetByIdCompaniaAsync(targetCompaniaId);
        if (targetCompania?.UserLimit.HasValue == true)
        {
            var currentUserCount = await _context.Users
                .Include(u => u.Actor)
                .CountAsync(u => u.Actor.CompanyId == targetCompaniaId && !u.Actor.IsDeleted);
            if (currentUserCount >= targetCompania.UserLimit.Value)
            {
                _logger.LogWarning("User limit reached for company {CompanyId}. Limit: {Limit}, Current: {Count}",
                    targetCompaniaId, targetCompania.UserLimit.Value, currentUserCount);
                throw new InvalidOperationException(
                    $"Cannot create user. Company '{targetCompania.Name}' has reached its user limit of {targetCompania.UserLimit.Value}.");
            }
        }

        var usuarioExistente = await _usuarioRepositorio.GetByCorreoAsync(request.Correo);
        if (usuarioExistente != null)
        {
            _logger.LogWarning("Attempt to create user with duplicate email: {Correo}", request.Correo);
            throw new InvalidOperationException($"A user with email '{request.Correo}' already exists");
        }

        var tipoUsuario = await _tipoUsuarioRepositorio.GetByIdAsync(request.UserTypeId);
        if (tipoUsuario == null)
        {
            _logger.LogWarning("User type not found for create user. UserTypeId: {UserTypeId}", request.UserTypeId);
            throw new InvalidOperationException($"User type with Id {request.UserTypeId} does not exist");
        }

        if (!tipoUsuario.IsActive)
        {
            _logger.LogWarning("Attempt to create user with inactive user type: {UserTypeId}", request.UserTypeId);
            throw new InvalidOperationException($"User type with Id {request.UserTypeId} is inactive");
        }

        const string defaultPassword = "123456";

        var actor = new Actors
        {
            ActorType = "USER",
            FullName = request.Nombre,
            Email = request.Correo,
            CompanyId = targetCompaniaId,
            IsActive = true,
            CreatedAt = DateTime.UtcNow,
        };

        var usuario = new Users
        {
            UserTypeId = request.UserTypeId,
            PasswordHash = BCrypt.Net.BCrypt.HashPassword(defaultPassword),
            CreatedAt = DateTime.UtcNow,
            IsActive = true,
            MustResetPassword = true,
            Actor = actor,
        };

        _context.Actors.Add(actor);
        await _usuarioRepositorio.AddAsync(usuario);
        await _unitOfWork.SaveChangesAsync();

        _logger.LogInformation("Usuario creado exitosamente con Id: {Id}", usuario.Id);

        var tipoUsuarioParaResponse = await _tipoUsuarioRepositorio.GetByIdAsync(usuario.UserTypeId);
        var response = MapToResponseDto(usuario);
        response.TipoUsuarioNombre = tipoUsuarioParaResponse?.Name ?? string.Empty;

        return response;
    }

    public async Task<UsuarioResponseDto?> GetByIdAsync(int id)
    {
        _logger.LogInformation("Obteniendo usuario con Id: {Id}", id);
        var usuario = await _usuarioRepositorio.GetByIdAsync(id);

        if (usuario == null)
        {
            _logger.LogWarning("Usuario no encontrado con Id: {Id}", id);
            return null;
        }

        var tipoUsuario = await _tipoUsuarioRepositorio.GetByIdAsync(usuario.UserTypeId);
        var response = MapToResponseDto(usuario);
        response.TipoUsuarioNombre = tipoUsuario?.Name ?? string.Empty;
        return response;
    }

    public async Task<IEnumerable<UsuarioResponseDto>> GetAllAsync(int currentUserId)
    {
        _logger.LogInformation("Getting users for current user Id: {CurrentUserId}", currentUserId);

        var currentUser = await _usuarioRepositorio.GetByIdAsync(currentUserId);
        if (currentUser == null)
        {
            _logger.LogWarning("Current user not found. Id: {CurrentUserId}", currentUserId);
            return Array.Empty<UsuarioResponseDto>();
        }

        var currentUserTipo = await _tipoUsuarioRepositorio.GetByIdAsync(currentUser.UserTypeId);
        bool currentUserEsSistema = currentUserTipo?.IsSystem == true;

        var usuarios = await _usuarioRepositorio.GetAllAsync();
        var tipoUsuarioIds = usuarios.Select(u => u.UserTypeId).Distinct().ToList();
        var tiposUsuarioNames = new Dictionary<int, string>();
        var tiposSistemaIds = new HashSet<int>();

        foreach (var tipoId in tipoUsuarioIds)
        {
            var tipo = await _tipoUsuarioRepositorio.GetByIdAsync(tipoId);
            if (tipo != null)
            {
                tiposUsuarioNames[tipoId] = tipo.Name;
                if (tipo.IsSystem)
                {
                    tiposSistemaIds.Add(tipoId);
                }
            }
        }

        IEnumerable<Users> usuariosFiltered;
        if (currentUserEsSistema)
        {
            usuariosFiltered = usuarios.Where(u => !tiposSistemaIds.Contains(u.UserTypeId)).ToList();
        }
        else
        {
            usuariosFiltered = usuarios.Where(u =>
                u.Actor?.CompanyId == currentUser.Actor?.CompanyId &&
                !tiposSistemaIds.Contains(u.UserTypeId)).ToList();
        }

        var response = usuariosFiltered.Select(MapToResponseDto).ToList();

        foreach (var item in response)
        {
            var usuario = usuariosFiltered.FirstOrDefault(u => u.Id == item.Id);
            if (usuario != null && tiposUsuarioNames.ContainsKey(usuario.UserTypeId))
            {
                item.TipoUsuarioNombre = tiposUsuarioNames[usuario.UserTypeId];
            }
        }

        return response;
    }

    public async Task<UsuarioResponseDto?> UpdateAsync(int id, UpdateUsuarioRequestDto request)
    {
        _logger.LogInformation("Actualizando usuario con Id: {Id}", id);

        var usuario = await _usuarioRepositorio.GetByIdAsync(id);
        if (usuario == null)
        {
            _logger.LogWarning("Usuario no encontrado con Id: {Id}", id);
            return null;
        }

        if (usuario.Actor.Email != request.Correo)
        {
            var usuarioExistente = await _usuarioRepositorio.GetByCorreoAsync(request.Correo);
            if (usuarioExistente != null && usuarioExistente.Id != id)
            {
                _logger.LogWarning("Intento de actualizar usuario con correo duplicado: {Correo}", request.Correo);
                throw new InvalidOperationException($"A user with email '{request.Correo}' already exists");
            }
        }

        var tipoUsuario = await _tipoUsuarioRepositorio.GetByIdAsync(request.UserTypeId);
        if (tipoUsuario == null)
        {
            _logger.LogWarning("Attempt to update user with non-existent user type: {UserTypeId}", request.UserTypeId);
            throw new InvalidOperationException($"User type with Id {request.UserTypeId} does not exist");
        }

        usuario.Actor.Email = request.Correo;
        usuario.Actor.FullName = request.Nombre;
        usuario.Actor.IsActive = request.IsActive;
        usuario.UserTypeId = request.UserTypeId;
        usuario.IsActive = request.IsActive;

        if (!string.IsNullOrWhiteSpace(request.Contrasena))
        {
            if (request.Contrasena != request.ConfirmarContrasena)
            {
                throw new InvalidOperationException("Passwords do not match");
            }
            usuario.PasswordHash = BCrypt.Net.BCrypt.HashPassword(request.Contrasena);
        }

        _usuarioRepositorio.Update(usuario);
        await _unitOfWork.SaveChangesAsync();

        _logger.LogInformation("Usuario actualizado exitosamente con Id: {Id}", id);

        var tipoUsuarioParaResponse = await _tipoUsuarioRepositorio.GetByIdAsync(usuario.UserTypeId);
        var response = MapToResponseDto(usuario);
        response.TipoUsuarioNombre = tipoUsuarioParaResponse?.Name ?? string.Empty;

        return response;
    }

    public async Task<bool> DeleteAsync(int id, int deleteUserId)
    {
        _logger.LogInformation("Eliminando usuario con Id: {Id}", id);

        var usuario = await _usuarioRepositorio.GetByIdAsync(id);
        if (usuario == null)
        {
            _logger.LogWarning("Usuario no encontrado con Id: {Id}", id);
            return false;
        }

        var deletingUser = await _usuarioRepositorio.GetByIdAsync(deleteUserId);

        usuario.Actor.IsDeleted = true;
        usuario.Actor.DeletedAt = DateTime.UtcNow;
        usuario.Actor.DeletedByActorId = deletingUser?.ActorId;

        _usuarioRepositorio.Update(usuario);
        await _unitOfWork.SaveChangesAsync();

        _logger.LogInformation("User soft deleted. Id: {Id}", id);
        return true;
    }

    public async Task<bool> ToggleActivoAsync(int id)
    {
        _logger.LogInformation("Cambiando estado activo del usuario con Id: {Id}", id);

        var usuario = await _usuarioRepositorio.GetByIdAsync(id);
        if (usuario == null)
        {
            _logger.LogWarning("Usuario no encontrado con Id: {Id}", id);
            return false;
        }

        usuario.IsActive = !usuario.IsActive;
        usuario.Actor.IsActive = usuario.IsActive;
        _usuarioRepositorio.Update(usuario);
        await _unitOfWork.SaveChangesAsync();

        _logger.LogInformation("Estado del usuario cambiado exitosamente. Id: {Id}, Nuevo estado: {IsActive}", id, usuario.IsActive);
        return true;
    }

    public async Task SetInitialPasswordAsync(int userId, SetInitialPasswordRequestDto request)
    {
        _logger.LogInformation("Setting initial password for user Id: {UserId}", userId);

        var usuario = await _usuarioRepositorio.GetByIdAsync(userId);
        if (usuario == null)
        {
            _logger.LogWarning("User not found for initial password set. Id: {UserId}", userId);
            throw new InvalidOperationException("User not found.");
        }

        if (!usuario.MustResetPassword)
        {
            _logger.LogWarning("User Id {UserId} does not require password reset.", userId);
            throw new InvalidOperationException("Password reset is not required for this user.");
        }

        if (string.IsNullOrWhiteSpace(request.NewPassword) || request.NewPassword.Length < 6)
        {
            throw new InvalidOperationException("New password must be at least 6 characters.");
        }

        if (request.NewPassword != request.ConfirmPassword)
        {
            throw new InvalidOperationException("New password and confirmation do not match.");
        }

        usuario.PasswordHash = BCrypt.Net.BCrypt.HashPassword(request.NewPassword);
        usuario.MustResetPassword = false;
        _usuarioRepositorio.Update(usuario);
        await _unitOfWork.SaveChangesAsync();

        _logger.LogInformation("Initial password set successfully for user Id: {UserId}", userId);
    }

    public async Task ResetPasswordAsync(int userId)
    {
        _logger.LogInformation("Resetting password for user Id: {UserId}", userId);

        var usuario = await _usuarioRepositorio.GetByIdAsync(userId);
        if (usuario == null)
        {
            _logger.LogWarning("User not found for password reset. Id: {UserId}", userId);
            throw new InvalidOperationException("User not found.");
        }

        const string defaultPassword = "123456";
        usuario.PasswordHash = BCrypt.Net.BCrypt.HashPassword(defaultPassword);
        usuario.MustResetPassword = true;
        _usuarioRepositorio.Update(usuario);
        await _unitOfWork.SaveChangesAsync();

        _logger.LogInformation("Password reset successfully for user Id: {UserId}. User must set new password on next login.", userId);
    }

    private static UsuarioResponseDto MapToResponseDto(Users u) => new()
    {
        Id = u.Id,
        ActorId = u.ActorId,
        Correo = u.Actor?.Email ?? string.Empty,
        Nombre = u.Actor?.FullName ?? string.Empty,
        UserTypeId = u.UserTypeId,
        CreatedAt = u.CreatedAt,
        IsActive = u.IsActive,
        CompanyId = u.Actor?.CompanyId ?? 0,
    };
}
