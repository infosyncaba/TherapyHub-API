using TherapuHubAPI.DTOs.Requests.Staff;
using TherapuHubAPI.DTOs.Responses.Staff;
using TherapuHubAPI.Models;
using TherapuHubAPI.Repositorio;
using TherapuHubAPI.Repositorio.IRepositorio;
using TherapuHubAPI.Services.IServices;

namespace TherapuHubAPI.Services;

public class StaffService : IStaffService
{
    private readonly IStaffRepositorio _staffRepositorio;
    private readonly IStaffStatusRepositorio _statusRepositorio;
    private readonly IStaffRolesRepositorio _rolesRepositorio;
    private readonly IUnitOfWork _unitOfWork;
    private readonly ContextDB _context;
    private readonly ILogger<StaffService> _logger;

    public StaffService(
        IStaffRepositorio staffRepositorio,
        IStaffStatusRepositorio statusRepositorio,
        IStaffRolesRepositorio rolesRepositorio,
        IUnitOfWork unitOfWork,
        ContextDB context,
        ILogger<StaffService> logger)
    {
        _staffRepositorio = staffRepositorio;
        _statusRepositorio = statusRepositorio;
        _rolesRepositorio = rolesRepositorio;
        _unitOfWork = unitOfWork;
        _context = context;
        _logger = logger;
    }

    public async Task<IEnumerable<StaffResponseDto>> GetByCompanyIdAsync(int companyId)
    {
        var staff = await _staffRepositorio.GetByCompanyIdAsync(companyId);
        var statuses = (await _statusRepositorio.GetAllAsync()).ToList();
        var roles = (await _rolesRepositorio.GetAllAsync()).ToList();
        return staff.Select(s => MapToDto(s, statuses, roles));
    }

    public async Task<StaffResponseDto?> GetByIdAsync(int id, int companyId)
    {
        var staff = await _staffRepositorio.GetByIdAndCompanyAsync(id, companyId);
        if (staff == null) return null;
        var statuses = (await _statusRepositorio.GetAllAsync()).ToList();
        var roles = (await _rolesRepositorio.GetAllAsync()).ToList();
        return MapToDto(staff, statuses, roles);
    }

    public async Task<StaffResponseDto> CreateAsync(CreateStaffRequestDto request, int companyId)
    {
        _logger.LogInformation("Creating staff {FirstName} {LastName} for company {CompanyId}", request.FirstName, request.LastName, companyId);

        var contractDate = request.ContractDate ?? DateOnly.FromDateTime(DateTime.UtcNow);

        var actor = new Actors
        {
            ActorType = "STAFF",
            FullName = $"{request.FirstName.Trim()} {request.LastName.Trim()}",
            Email = request.Email.Trim(),
            Phone = request.Phone.Trim(),
            CompanyId = companyId,
            IsActive = true,
            CreatedAt = DateTime.UtcNow,
        };

        var staff = new Staff
        {
            RoleId = request.RoleId,
            DateOfBirth = request.DateOfBirth,
            StatusId = request.StatusId,
            ContractDate = contractDate,
            CreatedAt = DateTime.UtcNow,
            Actor = actor,
        };

        _context.Actors.Add(actor);
        await _staffRepositorio.AddAsync(staff);
        await _unitOfWork.SaveChangesAsync();

        _logger.LogInformation("Staff created with Id: {Id}", staff.Id);

        if (request.DateOfBirth.HasValue)
        {
            try
            {
                await CreateBirthdayEventAsync(staff, request.DateOfBirth.Value, companyId);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Failed to create birthday event for staff Id: {Id}", staff.Id);
            }
        }

        var statuses = (await _statusRepositorio.GetAllAsync()).ToList();
        var roles = (await _rolesRepositorio.GetAllAsync()).ToList();
        return MapToDto(staff, statuses, roles);
    }

    private async Task CreateBirthdayEventAsync(Staff staff, DateOnly dateOfBirth, int companyId)
    {
        var birthdayType = await _unitOfWork.EventTypes.FirstOrDefaultAsync(
            t => t.Name.ToLower() == "birthday");

        if (birthdayType == null)
        {
            birthdayType = new EventTypes
            {
                Name = "Birthday",
                Color = "#ec4899",
                Icon = "cake",
                IsActive = true
            };
            await _unitOfWork.EventTypes.AddAsync(birthdayType);
            await _unitOfWork.SaveChangesAsync();
            _logger.LogInformation("Created 'Birthday' event type with Id: {Id}", birthdayType.Id);
        }

        var birthdayDate = GetNextBirthdayDate(dateOfBirth);

        var birthdayEvent = new Events
        {
            Title = $"{staff.Actor.FullName}'s Birthday",
            Description = $"Birthday of {staff.Actor.FullName}",
            StartDate = birthdayDate,
            EndDate = birthdayDate.AddDays(1).AddSeconds(-1),
            IsAllDay = true,
            EventTypeId = birthdayType.Id,
            IsGlobal = true,
            CompanyId = companyId,
            CreatedAt = DateTime.UtcNow,
            StaffId = staff.Id,
            IsPrivate = false
        };

        await _unitOfWork.Events.AddAsync(birthdayEvent);
        await _unitOfWork.SaveChangesAsync();

        _logger.LogInformation(
            "Birthday event created for staff Id: {StaffId}, EventId: {EventId}, next birthday: {Date}",
            staff.Id, birthdayEvent.Id, birthdayDate.ToString("yyyy-MM-dd"));
    }

    private async Task UpdateBirthdayEventAsync(Staff staff, DateOnly? newDateOfBirth, int companyId)
    {
        var existingEvent = await _unitOfWork.Events.FirstOrDefaultAsync(
            e => e.StaffId == staff.Id && e.CompanyId == companyId);

        if (existingEvent == null)
        {
            if (newDateOfBirth.HasValue)
            {
                await CreateBirthdayEventAsync(staff, newDateOfBirth.Value, companyId);
            }
            return;
        }

        existingEvent.Title = $"{staff.Actor.FullName}'s Birthday";
        existingEvent.Description = $"Birthday of {staff.Actor.FullName}";

        if (newDateOfBirth.HasValue)
        {
            var birthdayDate = GetNextBirthdayDate(newDateOfBirth.Value);
            existingEvent.StartDate = birthdayDate;
            existingEvent.EndDate = birthdayDate.AddDays(1).AddSeconds(-1);
        }

        _unitOfWork.Events.Update(existingEvent);
        await _unitOfWork.SaveChangesAsync();

        _logger.LogInformation("Birthday event updated for staff Id: {StaffId}, EventId: {EventId}", staff.Id, existingEvent.Id);
    }

    private static DateTime GetNextBirthdayDate(DateOnly dateOfBirth)
    {
        var today = DateOnly.FromDateTime(DateTime.UtcNow);
        var year = today.Year;

        var day = dateOfBirth.Day;
        if (dateOfBirth.Month == 2 && day == 29 && !DateTime.IsLeapYear(year))
            day = 28;

        var thisYearBirthday = new DateOnly(year, dateOfBirth.Month, day);

        if (thisYearBirthday >= today)
            return thisYearBirthday.ToDateTime(TimeOnly.MinValue, DateTimeKind.Utc);

        year++;
        day = dateOfBirth.Day;
        if (dateOfBirth.Month == 2 && day == 29 && !DateTime.IsLeapYear(year))
            day = 28;

        return new DateOnly(year, dateOfBirth.Month, day).ToDateTime(TimeOnly.MinValue, DateTimeKind.Utc);
    }

    public async Task<StaffResponseDto?> UpdateAsync(int id, UpdateStaffRequestDto request, int companyId)
    {
        _logger.LogInformation("Updating staff Id: {Id}", id);

        var staff = await _staffRepositorio.GetByIdAndCompanyAsync(id, companyId);
        if (staff == null)
        {
            _logger.LogWarning("Staff not found Id: {Id}", id);
            return null;
        }

        staff.Actor.FullName = $"{request.FirstName.Trim()} {request.LastName.Trim()}";
        staff.Actor.Email = request.Email.Trim();
        staff.Actor.Phone = request.Phone.Trim();
        staff.RoleId = request.RoleId;
        staff.DateOfBirth = request.DateOfBirth ?? staff.DateOfBirth;
        staff.StatusId = request.StatusId;
        staff.ContractDate = request.ContractDate ?? staff.ContractDate;

        _staffRepositorio.Update(staff);
        await _unitOfWork.SaveChangesAsync();

        try
        {
            await UpdateBirthdayEventAsync(staff, request.DateOfBirth, companyId);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to update birthday event for staff Id: {Id}", staff.Id);
        }

        var statuses = (await _statusRepositorio.GetAllAsync()).ToList();
        var roles = (await _rolesRepositorio.GetAllAsync()).ToList();
        return MapToDto(staff, statuses, roles);
    }

    public async Task<bool> DeleteAsync(int id, int companyId)
    {
        var staff = await _staffRepositorio.GetByIdAndCompanyAsync(id, companyId);
        if (staff == null) return false;
        _staffRepositorio.Remove(staff);
        _context.Actors.Remove(staff.Actor);
        await _unitOfWork.SaveChangesAsync();
        _logger.LogInformation("Staff deleted Id: {Id}", id);
        return true;
    }

    public async Task<bool> ToggleActiveAsync(int id, int companyId)
    {
        var staff = await _staffRepositorio.GetByIdAndCompanyAsync(id, companyId);
        if (staff == null) return false;
        staff.Actor.IsActive = !staff.Actor.IsActive;
        _staffRepositorio.Update(staff);
        await _unitOfWork.SaveChangesAsync();
        _logger.LogInformation("Staff IsActive toggled Id: {Id}, IsActive: {IsActive}", id, staff.Actor.IsActive);
        return true;
    }

    public async Task<IEnumerable<StaffStatusResponseDto>> GetAllStatusesAsync()
    {
        var list = await _statusRepositorio.GetAllAsync();
        return list.Select(s => new StaffStatusResponseDto { Id = s.Id, Name = s.Name, IsActive = s.IsActive });
    }

    public async Task<IEnumerable<StaffRoleResponseDto>> GetAllRolesAsync()
    {
        var list = await _rolesRepositorio.GetAllAsync();
        return list.Select(r => new StaffRoleResponseDto { Id = r.Id, Name = r.Name, IsActive = r.IsActive });
    }

    private static StaffResponseDto MapToDto(Staff s, List<StaffStatus> statuses, List<StaffRoles> roles)
    {
        var status = statuses.FirstOrDefault(x => x.Id == s.StatusId);
        var role = roles.FirstOrDefault(x => x.Id == s.RoleId);

        var nameParts = s.Actor.FullName.Split(' ', 2);
        var firstName = nameParts[0];
        var lastName = nameParts.Length > 1 ? nameParts[1] : string.Empty;

        return new StaffResponseDto
        {
            Id = s.Id,
            ActorId = s.ActorId,
            FirstName = firstName,
            LastName = lastName,
            RoleId = s.RoleId,
            RoleName = role?.Name,
            CompanyId = s.Actor.CompanyId,
            DateOfBirth = s.DateOfBirth,
            Phone = s.Actor.Phone ?? string.Empty,
            Email = s.Actor.Email ?? string.Empty,
            StatusId = s.StatusId,
            StatusName = status?.Name,
            ContractDate = s.ContractDate,
            CreatedAt = s.CreatedAt,
            IsActive = s.Actor.IsActive,
        };
    }
}
