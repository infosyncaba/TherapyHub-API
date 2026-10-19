using TherapuHubAPI.DTOs.Requests.ComplianceItems;
using TherapuHubAPI.DTOs.Responses.ComplianceItems;

namespace TherapuHubAPI.Services.IServices;

public interface IComplianceItemsService
{
    Task<IEnumerable<ComplianceItemResponseDto>> GetMineAsync(int userId);
    Task<ComplianceItemResponseDto?> GetByIdAsync(int id, int userId);
    Task<ComplianceItemResponseDto> CreateAsync(CreateComplianceItemRequestDto dto, int userId, int companyId);
    Task<ComplianceItemResponseDto?> UpdateAsync(int id, UpdateComplianceItemRequestDto dto, int userId);
    Task<bool> DeleteAsync(int id, int userId);
}
