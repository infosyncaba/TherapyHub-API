using System;
using System.Collections.Generic;

namespace TherapuHubAPI.Models;

public partial class Staff
{
    public int Id { get; set; }

    public short RoleId { get; set; }

    public DateOnly? DateOfBirth { get; set; }

    public byte StatusId { get; set; }

    public DateOnly ContractDate { get; set; }

    public DateTime CreatedAt { get; set; }

    public int ActorId { get; set; }

    public string? Certifications { get; set; }
}
