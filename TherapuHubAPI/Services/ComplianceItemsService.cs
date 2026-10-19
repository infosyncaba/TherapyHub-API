using TherapuHubAPI.DTOs.Requests.ComplianceItems;
using TherapuHubAPI.DTOs.Responses.ComplianceItems;
using TherapuHubAPI.Models;
using TherapuHubAPI.Repositorio;
using TherapuHubAPI.Services.IServices;

namespace TherapuHubAPI.Services;

// Records are private: every query is scoped to the owner's UserId.
public class ComplianceItemsService : IComplianceItemsService
{
    private readonly IUnitOfWork _unitOfWork;
    private readonly ILogger<ComplianceItemsService> _logger;

    public ComplianceItemsService(IUnitOfWork unitOfWork, ILogger<ComplianceItemsService> logger)
    {
        _unitOfWork = unitOfWork;
        _logger = logger;
    }

    public async Task<IEnumerable<ComplianceItemResponseDto>> GetMineAsync(int userId)
    {
        var items = await _unitOfWork.ComplianceItems.FindAsync(i => i.UserId == userId && i.IsDelete != true);
        return items.OrderBy(i => i.ExpirationDate).ThenBy(i => i.Id).Select(MapToDto);
    }

    public async Task<ComplianceItemResponseDto?> GetByIdAsync(int id, int userId)
    {
        var item = await GetOwnedAsync(id, userId);
        return item == null ? null : MapToDto(item);
    }

    public async Task<ComplianceItemResponseDto> CreateAsync(CreateComplianceItemRequestDto dto, int userId, int companyId)
    {
        var entity = new ComplianceItems
        {
            UserId = userId,
            CompanyId = companyId,
            Description = dto.Description.Trim(),
            RegistrationDate = dto.RegistrationDate.Date,
            ExpirationDate = dto.ExpirationDate.Date,
            CreatedAt = DateTime.UtcNow,
        };
        await _unitOfWork.ComplianceItems.AddAsync(entity);
        await _unitOfWork.SaveChangesAsync();
        return MapToDto(entity);
    }

    public async Task<ComplianceItemResponseDto?> UpdateAsync(int id, UpdateComplianceItemRequestDto dto, int userId)
    {
        var entity = await GetOwnedAsync(id, userId);
        if (entity == null) return null;

        entity.Description = dto.Description.Trim();
        entity.RegistrationDate = dto.RegistrationDate.Date;
        entity.ExpirationDate = dto.ExpirationDate.Date;
        entity.UpdatedAt = DateTime.UtcNow;
        _unitOfWork.ComplianceItems.Update(entity);
        await _unitOfWork.SaveChangesAsync();
        return MapToDto(entity);
    }

    public async Task<bool> DeleteAsync(int id, int userId)
    {
        var entity = await GetOwnedAsync(id, userId);
        if (entity == null) return false;

        entity.IsDelete = true;
        entity.DeleteAt = DateTime.UtcNow;
        _unitOfWork.ComplianceItems.Update(entity);
        await _unitOfWork.SaveChangesAsync();
        return true;
    }

    private Task<ComplianceItems?> GetOwnedAsync(int id, int userId) =>
        _unitOfWork.ComplianceItems.FirstOrDefaultAsync(i => i.Id == id && i.UserId == userId && i.IsDelete != true);

    private static ComplianceItemResponseDto MapToDto(ComplianceItems i) => new()
    {
        Id = i.Id,
        Description = i.Description,
        RegistrationDate = i.RegistrationDate,
        ExpirationDate = i.ExpirationDate,
        CreatedAt = i.CreatedAt,
    };
}
