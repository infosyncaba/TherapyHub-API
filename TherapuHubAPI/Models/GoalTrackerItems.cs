using System;
using System.Collections.Generic;

namespace TherapuHubAPI.Models;

public partial class GoalTrackerItems
{
    public long Id { get; set; }

    public long GoalTrackerId { get; set; }

    public byte CategoryId { get; set; }

    public string Name { get; set; } = null!;

    public string? MasteryCriteria { get; set; }

    public byte StatusId { get; set; }

    public DateTime CreatedAt { get; set; }
}
