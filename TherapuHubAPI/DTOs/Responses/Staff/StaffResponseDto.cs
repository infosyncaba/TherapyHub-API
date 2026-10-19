using System.Text.Json.Serialization;

namespace TherapuHubAPI.DTOs.Responses.Staff;

public class StaffResponseDto
{
    public int Id { get; set; }
    public int ActorId { get; set; }
    public string FirstName { get; set; } = null!;
    public string LastName { get; set; } = null!;
    public string Name => $"{FirstName} {LastName}".Trim();
    public short RoleId { get; set; }
    public string? RoleName { get; set; }
    public int CompanyId { get; set; }
    public DateOnly? DateOfBirth { get; set; }
    public string Phone { get; set; } = null!;
    public string Email { get; set; } = null!;
    public byte StatusId { get; set; }
    public string? StatusName { get; set; }
    public string? Certifications { get; set; }
    public DateOnly? ContractDate { get; set; }
    public DateTime CreatedAt { get; set; }
    public bool IsActive { get; set; }
    public int AssignedClientCount { get; set; }
}
