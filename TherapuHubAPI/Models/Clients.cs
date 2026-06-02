using System;
using System.Collections.Generic;

namespace TherapuHubAPI.Models;

public partial class Clients
{
    public int Id { get; set; }

    public string ClientCode { get; set; } = null!;

    public DateOnly? BirthDate { get; set; }

    public string? GuardianName { get; set; }

    public int ClientStatusId { get; set; }

    public DateTime CreatedAt { get; set; }

    public int? RBTId { get; set; }

    public string? Emoji { get; set; }

    public string? Diagnosis { get; set; }

    public int ActorId { get; set; }
}
