namespace TherapuHubAPI.DTOs.Requests.BehaviorFunctions;

public class UpdateBehaviorFunctionRequestDto
{
    public string Name { get; set; } = null!;
    public bool IsActive { get; set; }
}
