using System;
using Diten.Domain.Common;

namespace Diten.Domain.Aggregates.Task;

public class TaskAggregate : BaseEntity
{
    public string Title { get; set; } = string.Empty;
    public string Description { get; set; } = string.Empty;
    public string Status { get; set; } = "Pending";
}
