using TherapuHubAPI.DTOs.Requests.BehaviorFunctions;
using TherapuHubAPI.DTOs.Responses.BehaviorFunctions;

namespace TherapuHubAPI.Services.IServices;

public interface IBehaviorFunctionsService
{
    Task<IEnumerable<BehaviorFunctionResponseDto>> GetAllAsync();
    Task<IEnumerable<BehaviorFunctionResponseDto>> GetActiveAsync();
    Task<BehaviorFunctionResponseDto?> GetByIdAsync(int id);
    Task<BehaviorFunctionResponseDto> CreateAsync(CreateBehaviorFunctionRequestDto dto, int actorId);
    Task<BehaviorFunctionResponseDto?> UpdateAsync(int id, UpdateBehaviorFunctionRequestDto dto);
    Task<bool> DeleteAsync(int id, int actorId);
    Task<BehaviorFunctionResponseDto?> ToggleActiveAsync(int id);
}
