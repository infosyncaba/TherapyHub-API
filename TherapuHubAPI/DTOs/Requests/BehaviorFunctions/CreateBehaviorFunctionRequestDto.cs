namespace TherapuHubAPI.DTOs.Requests.BehaviorFunctions;

public class CreateBehaviorFunctionRequestDto
{
    public string Name { get; set; } = null!;
    public bool IsActive { get; set; } = true;
}
