namespace TherapuHubAPI.DTOs.Responses.ComplianceItems;

public class ComplianceItemResponseDto
{
    public int Id { get; set; }
    public string Description { get; set; } = null!;
    public DateTime RegistrationDate { get; set; }
    public DateTime ExpirationDate { get; set; }
    public DateTime CreatedAt { get; set; }
}
