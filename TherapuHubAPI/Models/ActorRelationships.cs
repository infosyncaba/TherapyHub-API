using System;
using System.Collections.Generic;

namespace TherapuHubAPI.Models;

public partial class ActorRelationships
{
    public long Id { get; set; }

    public int SourceActorId { get; set; }

    public int TargetActorId { get; set; }

    public DateTime CreatedAt { get; set; }

    public int RelationshipTypeId { get; set; }

    /// <summary>True when the relationship was created by the system (UserTypes.AutoAssignAllRbts), not by a user.</summary>
    public bool IsAutoAssigned { get; set; }
}
