using AutoMapper;
using TherapuHubAPI.DTOs.Requests.UserTypes;
using TherapuHubAPI.DTOs.Responses.UserTypes;
using TherapuHubAPI.Repositorio;
using TherapuHubAPI.Repositorio.IRepositorio;
using TherapuHubAPI.Services.IServices;

namespace TherapuHubAPI.Services.Implementations;

public class TipoUsuarioService : ITipoUsuarioService
{
    private readonly ITipoUsuarioRepositorio _tipoUsuarioRepositorio;
    private readonly IUsuarioRepositorio _usuarioRepositorio;
    private readonly IUnitOfWork _unitOfWork;
    private readonly IMapper _mapper;
    private readonly ILogger<TipoUsuarioService> _logger;

    public TipoUsuarioService(
        ITipoUsuarioRepositorio tipoUsuarioRepositorio,
        IUsuarioRepositorio usuarioRepositorio,
        IUnitOfWork unitOfWork,
        IMapper mapper,
        ILogger<TipoUsuarioService> logger)
    {
        _tipoUsuarioRepositorio = tipoUsuarioRepositorio;
        _usuarioRepositorio = usuarioRepositorio;
        _unitOfWork = unitOfWork;
        _mapper = mapper;
        _logger = logger;
    }

    public async Task<IEnumerable<TipoUsuarioResponseDto>> GetAllAsync()
    {
        _logger.LogInformation("Obteniendo todos los tipos de usuario");
        var tiposUsuario = await _tipoUsuarioRepositorio.GetAllAsync();
        return _mapper.Map<IEnumerable<TipoUsuarioResponseDto>>(tiposUsuario.Where(x => x.IsSystem == false));
    }

    public async Task<TipoUsuarioResponseDto?> GetByIdAsync(int id)
    {
        _logger.LogInformation("Obteniendo tipo de usuario con Id: {Id}", id);
        var tipoUsuario = await _tipoUsuarioRepositorio.GetByIdAsync(id);

        if (tipoUsuario == null)
        {
            _logger.LogWarning("Tipo de usuario no encontrado con Id: {Id}", id);
            return null;
        }

        return _mapper.Map<TipoUsuarioResponseDto>(tipoUsuario);
    }

    public async Task<TipoUsuarioResponseDto> CreateAsync(CreateTipoUsuarioRequestDto request)
    {
        _logger.LogInformation("Creando nuevo tipo de usuario: {Nombre}", request.Nombre);

        // Validar que el nombre no exista
        if (await _tipoUsuarioRepositorio.ExistsByNombreAsync(request.Nombre))
        {
            _logger.LogWarning("Intento de crear tipo de usuario con nombre duplicado: {Nombre}", request.Nombre);
            throw new InvalidOperationException($"A user type with name '{request.Nombre}' already exists");
        }

        var tipoUsuario = _mapper.Map<Models.UserTypes>(request);
        tipoUsuario.CreatedAt = DateTime.UtcNow;

        await _tipoUsuarioRepositorio.AddAsync(tipoUsuario);
        await _unitOfWork.SaveChangesAsync();

        _logger.LogInformation("Tipo de usuario creado exitosamente con Id: {Id}", tipoUsuario.Id);

        return _mapper.Map<TipoUsuarioResponseDto>(tipoUsuario);
    }

    public async Task<TipoUsuarioResponseDto?> UpdateAsync(int id, UpdateTipoUsuarioRequestDto request)
    {
        _logger.LogInformation("Updating user type with Id: {Id}", id);

        var tipoUsuario = await _tipoUsuarioRepositorio.GetByIdAsync(id);

        if (tipoUsuario == null)
        {
            _logger.LogWarning("User type not found for update with Id: {Id}", id);
            return null;
        }

        // Validar que el nombre no exista en otro registro
        if (await _tipoUsuarioRepositorio.ExistsByNombreAsync(request.Nombre, id))
        {
            _logger.LogWarning("Attempt to update user type with duplicate name: {Nombre}", request.Nombre);
            throw new InvalidOperationException($"Another user type with name '{request.Nombre}' already exists");
        }

        // Validar si se está intentando desactivar un tipo de usuario que tiene usuarios asignados
        if (tipoUsuario.IsActive && !request.IsActive)
        {
            var usuariosCount = await _usuarioRepositorio.CountByTipoUsuarioIdAsync(id);
            if (usuariosCount > 0)
            {
                _logger.LogWarning("Attempt to deactivate user type with {Count} associated users. Id: {Id}", usuariosCount, id);
                throw new InvalidOperationException($"Cannot deactivate this user type. There are {usuariosCount} user(s) assigned to this type. Please change the user type of these users first before deactivating.");
            }
        }

        _mapper.Map(request, tipoUsuario);
        _tipoUsuarioRepositorio.Update(tipoUsuario);
        await _unitOfWork.SaveChangesAsync();

        _logger.LogInformation("User type updated successfully with Id: {Id}", id);

        return _mapper.Map<TipoUsuarioResponseDto>(tipoUsuario);
    }

    public async Task<bool> DeleteAsync(int id)
    {
        _logger.LogInformation("Deactivating user type with Id: {Id}", id);

        var tipoUsuario = await _tipoUsuarioRepositorio.GetByIdAsync(id);

        if (tipoUsuario == null)
        {
            _logger.LogWarning("User type not found for deactivation with Id: {Id}", id);
            return false;
        }

        // Verificar si hay usuarios asociados
        var usuariosCount = await _usuarioRepositorio.CountByTipoUsuarioIdAsync(id);
        if (usuariosCount > 0)
        {
            _logger.LogWarning("Attempt to deactivate user type with {Count} associated users. Id: {Id}", usuariosCount, id);
            throw new InvalidOperationException($"Cannot deactivate this user type. There are {usuariosCount} user(s) assigned to this type. Please change the user type of these users first before deactivating.");
        }

        // Solo marcamos como inactivo en lugar de eliminar físicamente
        tipoUsuario.IsActive = false;
        _tipoUsuarioRepositorio.Update(tipoUsuario);
        await _unitOfWork.SaveChangesAsync();

        _logger.LogInformation("User type deactivated successfully with Id: {Id}", id);

        return true;
    }
}
