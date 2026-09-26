using System.ComponentModel.DataAnnotations;

namespace TherapuHubAPI.DTOs.Requests.GoalTracker;

public class CreateGoalTrackerItemDto
{
    [Required]
    public int CategoryId { get; set; }

    [Required]
    [MaxLength(255)]
    public string Name { get; set; } = null!;

    [MaxLength(100)]
    public string? MasteryCriteria { get; set; }

    [Required]
    public int StatusId { get; set; }

    // Only applies to the "Maladaptive Behaviors" category; ignored for the others
    public int? FunctionId { get; set; }
}
