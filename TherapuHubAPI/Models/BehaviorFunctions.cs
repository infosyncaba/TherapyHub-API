using System;
using System.Collections.Generic;

namespace TherapuHubAPI.Models;

public partial class BehaviorFunctions
{
    public int Id { get; set; }

    public string Name { get; set; } = null!;

    public bool IsActive { get; set; }

    public int ActorCreatedId { get; set; }

    public DateTime CreatedAt { get; set; }

    public bool? IsDelete { get; set; }

    public DateTime? DeleteAt { get; set; }

    public int? DeletedActorId { get; set; }
}
