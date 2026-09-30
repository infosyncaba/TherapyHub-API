namespace TherapuHubAPI.DTOs.Responses.GoalTracker;

public class GoalTrackerItemResponseDto
{
    public long Id { get; set; }
    public long GoalTrackerId { get; set; }
    public int CategoryId { get; set; }
    public string Name { get; set; } = null!;
    public string? MasteryCriteria { get; set; }
    public int StatusId { get; set; }
    public string StatusName { get; set; } = null!;
    public string StatusColor { get; set; } = null!;
    public List<int> FunctionIds { get; set; } = [];
    public List<string> FunctionNames { get; set; } = [];
    public DateTime CreatedAt { get; set; }
}
