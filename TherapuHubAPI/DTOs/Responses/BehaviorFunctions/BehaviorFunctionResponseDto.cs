namespace TherapuHubAPI.DTOs.Responses.BehaviorFunctions;

public class BehaviorFunctionResponseDto
{
    public int Id { get; set; }
    public string Name { get; set; } = null!;
    public bool IsActive { get; set; }
    public DateTime CreatedAt { get; set; }
}
