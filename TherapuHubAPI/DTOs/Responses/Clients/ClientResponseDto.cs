using System.Text.Json.Serialization;

namespace TherapuHubAPI.DTOs.Responses.Clients;

public class ClientResponseDto
{
    public int Id { get; set; }
    public int ActorId { get; set; }
    public string ClientCode { get; set; } = null!;
    public string FullName { get; set; } = null!;
    public DateOnly? BirthDate { get; set; }
    public string? Email { get; set; }
    public string? Phone { get; set; }
    public string? GuardianName { get; set; }
    public int ClientStatusId { get; set; }
    public string? ClientStatusName { get; set; }
    public int CompanyId { get; set; }

    [JsonPropertyName("rbtId")]
    public int? RBTId { get; set; }

    [JsonPropertyName("rbtName")]
    public string? RBTName { get; set; }

    public string? Emoji { get; set; }
    public string? Diagnosis { get; set; }
    public DateTime CreatedAt { get; set; }
}

public class ClientStatusResponseDto
{
    public int Id { get; set; }
    public string Name { get; set; } = null!;
    public string Code { get; set; } = null!;
    public bool IsActive { get; set; }
}
