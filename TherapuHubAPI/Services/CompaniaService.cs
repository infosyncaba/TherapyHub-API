using AutoMapper;
using TherapuHubAPI.DTOs.Requests.Companies;
using TherapuHubAPI.DTOs.Responses.Companies;
using TherapuHubAPI.Repositorio;
using TherapuHubAPI.Repositorio.IRepositorio;
using TherapuHubAPI.Services.IServices;

namespace TherapuHubAPI.Services;

public class CompaniaService : ICompaniaService
{
    private readonly ICompaniaRepositorio _companiaRepositorio;
    private readonly IUsuarioRepositorio _usuarioRepositorio;
    private readonly IUnitOfWork _unitOfWork;
    private readonly IMapper _mapper;
    private readonly ILogger<CompaniaService> _logger;

    public CompaniaService(
        ICompaniaRepositorio companiaRepositorio,
        IUsuarioRepositorio usuarioRepositorio,
        IUnitOfWork unitOfWork,
        IMapper mapper,
        ILogger<CompaniaService> logger)
    {
        _companiaRepositorio = companiaRepositorio;
        _usuarioRepositorio = usuarioRepositorio;
        _unitOfWork = unitOfWork;
        _mapper = mapper;
        _logger = logger;
    }

    public async Task<CompaniaResponseDto> CreateAsync(CreateCompaniaRequestDto request)
    {
        _logger.LogInformation("Creating new company: {Nombre}", request.Nombre);

        var existente = await _companiaRepositorio.GetByNombreAsync(request.Nombre);
        if (existente != null)
        {
            _logger.LogWarning("Attempt to create company with duplicate name: {Nombre}", request.Nombre);
            throw new InvalidOperationException($"A company with the name '{request.Nombre.Trim()}' already exists.");
        }

        var compania = _mapper.Map<Models.Companies>(request);
        compania.CreatedAt = DateTime.UtcNow;
        compania.IsActive = request.IsActive;

        await _companiaRepositorio.AddAsync(compania);
        await _unitOfWork.SaveChangesAsync();

        _logger.LogInformation("Company created with Id: {Id}", compania.Id);
        return _mapper.Map<CompaniaResponseDto>(compania);
    }

    public async Task<CompaniaResponseDto?> GetByIdAsync(int id)
    {
        var compania = await _companiaRepositorio.GetByIdCompaniaAsync(id);
        return compania == null ? null : _mapper.Map<CompaniaResponseDto>(compania);
    }

    public async Task<IEnumerable<CompaniaResponseDto>> GetAllAsync()
    {
        var companias = await _companiaRepositorio.GetAllAsync();
        return _mapper.Map<IEnumerable<CompaniaResponseDto>>(companias);
    }

    public async Task<CompaniaResponseDto?> UpdateAsync(int id, UpdateCompaniaRequestDto request)
    {
        _logger.LogInformation("Updating company Id: {Id}", id);

        var compania = await _companiaRepositorio.GetByIdCompaniaAsync(id);
        if (compania == null)
        {
            _logger.LogWarning("Company not found with Id: {Id}", id);
            return null;
        }

        if (compania.Name.Trim().ToLower() != request.Nombre.Trim().ToLower())
        {
            var existente = await _companiaRepositorio.GetByNombreAsync(request.Nombre);
            if (existente != null && existente.Id != id)
            {
                _logger.LogWarning("Intento de actualizar compañía con nombre duplicado: {Nombre}", request.Nombre);
                throw new InvalidOperationException($"Ya existe otra compañía con el nombre '{request.Nombre.Trim()}'.");
            }
        }

        compania.Name = request.Nombre;
        compania.TaxId = request.Nit;
        compania.IsActive = request.IsActive;
        compania.UserLimit = request.UserLimit;

        _companiaRepositorio.Update(compania);
        await _unitOfWork.SaveChangesAsync();

        _logger.LogInformation("Company updated Id: {Id}", id);
        return _mapper.Map<CompaniaResponseDto>(compania);
    }

    public async Task<bool> DeleteAsync(int id, int deleteUserId)
    {
        _logger.LogInformation("Eliminando compañía Id: {Id}", id);

        var compania = await _companiaRepositorio.GetByIdCompaniaAsync(id);
        if (compania == null)
        {
            _logger.LogWarning("Compañía no encontrada con Id: {Id}", id);
            return false;
        }

        var tieneUsuarios = await _usuarioRepositorio.HasUsersInCompanyAsync(id);
        if (tieneUsuarios)
        {
            _logger.LogWarning("Cannot delete company Id: {Id} because it has active users assigned.", id);
            throw new InvalidOperationException("Cannot delete the company because it has users assigned. Delete the users first.");
        }

        compania.IsDeleted = true;
        compania.DeletedAt = DateTime.UtcNow;
        compania.DeleteUserId = deleteUserId;

        _companiaRepositorio.Update(compania);
        await _unitOfWork.SaveChangesAsync();

        _logger.LogInformation("Company soft deleted. Id: {Id}", id);
        return true;
    }

    public async Task<bool> ToggleActivoAsync(int id)
    {
        _logger.LogInformation("Cambiando estado activo de compañía Id: {Id}", id);

        var compania = await _companiaRepositorio.GetByIdCompaniaAsync(id);
        if (compania == null)
        {
            _logger.LogWarning("Compañía no encontrada con Id: {Id}", id);
            return false;
        }

        compania.IsActive = !compania.IsActive;
        _companiaRepositorio.Update(compania);
        await _unitOfWork.SaveChangesAsync();

        _logger.LogInformation("Company status updated. Id: {Id}, IsActive: {IsActive}", id, compania.IsActive);
        return true;
    }
}
