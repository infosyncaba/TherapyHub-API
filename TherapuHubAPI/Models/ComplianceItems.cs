using System;
using System.Collections.Generic;

namespace TherapuHubAPI.Models;

public partial class ComplianceItems
{
    public int Id { get; set; }

    public int UserId { get; set; }

    public int CompanyId { get; set; }

    public string Description { get; set; } = null!;

    public DateTime RegistrationDate { get; set; }

    public DateTime ExpirationDate { get; set; }

    public DateTime CreatedAt { get; set; }

    public DateTime? UpdatedAt { get; set; }

    public bool? IsDelete { get; set; }

    public DateTime? DeleteAt { get; set; }
}
