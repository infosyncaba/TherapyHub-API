using System.Text.Json.Serialization;

namespace TherapuHubAPI.DTOs.Requests.Clients;

public class CreateClientRequestDto
{
    public string FullName { get; set; } = null!;
    public DateOnly? BirthDate { get; set; }
    public string? Email { get; set; }
    public string? Phone { get; set; }
    public string? GuardianName { get; set; }
    public int ClientStatusId { get; set; }

    [JsonPropertyName("rbtId")]
    public int? RBTId { get; set; }

    public string? Emoji { get; set; }
    public string? Diagnosis { get; set; }
}

public class UpdateClientRequestDto
{
    public string FullName { get; set; } = null!;
    public DateOnly? BirthDate { get; set; }
    public string? Email { get; set; }
    public string? Phone { get; set; }
    public string? GuardianName { get; set; }
    public int ClientStatusId { get; set; }

    [JsonPropertyName("rbtId")]
    public int? RBTId { get; set; }

    public string? Emoji { get; set; }
    public string? Diagnosis { get; set; }
}
