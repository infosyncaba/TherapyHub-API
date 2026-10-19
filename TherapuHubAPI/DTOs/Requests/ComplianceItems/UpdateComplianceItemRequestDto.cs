namespace TherapuHubAPI.DTOs.Requests.ComplianceItems;

public class UpdateComplianceItemRequestDto
{
    public string Description { get; set; } = null!;
    public DateTime RegistrationDate { get; set; }
    public DateTime ExpirationDate { get; set; }
}
