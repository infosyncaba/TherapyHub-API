namespace TherapuHubAPI.DTOs.Requests.ComplianceItems;

public class CreateComplianceItemRequestDto
{
    public string Description { get; set; } = null!;
    public DateTime RegistrationDate { get; set; }
    public DateTime ExpirationDate { get; set; }
}
