using System;
using System.Collections.Generic;

namespace TherapuHubAPI.Models;

public partial class UserTypes
{
    public int Id { get; set; }

    public string Name { get; set; } = null!;

    public string? Description { get; set; }

    public bool IsActive { get; set; }

    public DateTime CreatedAt { get; set; }

    public bool IsSystem { get; set; }

    /// <summary>When true, users of this type are automatically linked (Supervisor → RBT) to every RBT in their company.</summary>
    public bool AutoAssignAllRbts { get; set; }
}
