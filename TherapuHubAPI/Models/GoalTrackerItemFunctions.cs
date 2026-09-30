using System;
using System.Collections.Generic;

namespace TherapuHubAPI.Models;

public partial class GoalTrackerItemFunctions
{
    public long Id { get; set; }

    public long GoalTrackerItemId { get; set; }

    public int FunctionId { get; set; }
}
