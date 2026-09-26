using TherapuHubAPI.DTOs.Requests.BehaviorFunctions;
using TherapuHubAPI.DTOs.Responses.BehaviorFunctions;
using TherapuHubAPI.Models;
using TherapuHubAPI.Repositorio;
using TherapuHubAPI.Services.IServices;

namespace TherapuHubAPI.Services;

public class BehaviorFunctionsService : IBehaviorFunctionsService
{
    private readonly IUnitOfWork _unitOfWork;
    private readonly ILogger<BehaviorFunctionsService> _logger;

    public BehaviorFunctionsService(IUnitOfWork unitOfWork, ILogger<BehaviorFunctionsService> logger)
    {
        _unitOfWork = unitOfWork;
        _logger = logger;
    }

    public async Task<IEnumerable<BehaviorFunctionResponseDto>> GetAllAsync()
    {
        var functions = await _unitOfWork.BehaviorFunctions.FindAsync(f => f.IsDelete != true);
        return functions.OrderBy(f => f.Id).Select(MapToDto);
    }

    public async Task<IEnumerable<BehaviorFunctionResponseDto>> GetActiveAsync()
    {
        var functions = await _unitOfWork.BehaviorFunctions.FindAsync(f => f.IsDelete != true && f.IsActive);
        return functions.OrderBy(f => f.Id).Select(MapToDto);
    }

    public async Task<BehaviorFunctionResponseDto?> GetByIdAsync(int id)
    {
        var function = await _unitOfWork.BehaviorFunctions.GetByIdAsync(id);
        if (function == null || function.IsDelete == true) return null;
        return MapToDto(function);
    }

    public async Task<BehaviorFunctionResponseDto> CreateAsync(CreateBehaviorFunctionRequestDto dto, int actorId)
    {
        var entity = new BehaviorFunctions
        {
            Name = dto.Name.Trim(),
            IsActive = dto.IsActive,
            ActorCreatedId = actorId,
            CreatedAt = DateTime.UtcNow,
        };
        await _unitOfWork.BehaviorFunctions.AddAsync(entity);
        await _unitOfWork.SaveChangesAsync();
        return MapToDto(entity);
    }

    public async Task<BehaviorFunctionResponseDto?> UpdateAsync(int id, UpdateBehaviorFunctionRequestDto dto)
    {
        var entity = await _unitOfWork.BehaviorFunctions.GetByIdAsync(id);
        if (entity == null || entity.IsDelete == true) return null;

        entity.Name = dto.Name.Trim();
        entity.IsActive = dto.IsActive;
        _unitOfWork.BehaviorFunctions.Update(entity);
        await _unitOfWork.SaveChangesAsync();
        return MapToDto(entity);
    }

    public async Task<bool> DeleteAsync(int id, int actorId)
    {
        var entity = await _unitOfWork.BehaviorFunctions.GetByIdAsync(id);
        if (entity == null || entity.IsDelete == true) return false;

        entity.IsDelete = true;
        entity.DeleteAt = DateTime.UtcNow;
        entity.DeletedActorId = actorId;
        _unitOfWork.BehaviorFunctions.Update(entity);
        await _unitOfWork.SaveChangesAsync();
        return true;
    }

    public async Task<BehaviorFunctionResponseDto?> ToggleActiveAsync(int id)
    {
        var entity = await _unitOfWork.BehaviorFunctions.GetByIdAsync(id);
        if (entity == null || entity.IsDelete == true) return null;

        entity.IsActive = !entity.IsActive;
        _unitOfWork.BehaviorFunctions.Update(entity);
        await _unitOfWork.SaveChangesAsync();
        return MapToDto(entity);
    }

    private static BehaviorFunctionResponseDto MapToDto(BehaviorFunctions f) => new()
    {
        Id = f.Id,
        Name = f.Name,
        IsActive = f.IsActive,
        CreatedAt = f.CreatedAt,
    };
}
