using Diten.InventoryService.Domain.Enums;

namespace Diten.InventoryService.Domain.Entities;

/// <summary>
/// Append-only history of a location (K-11): one row for the creation and one per changed field, written in the
/// SAME transaction as the change. Nothing updates or deletes a row.
/// </summary>
public sealed class LocationChangeEntry : EntityBase
{
    public Guid LocationId { get; set; }

    public LocationChangeAction Action { get; set; }

    /// <summary>Wire field name (<c>code</c>, <c>name</c>, <c>parentId</c>, <c>status</c> …); null for a creation.</summary>
    public string? Field { get; set; }

    public string? OldValue { get; set; }

    public string? NewValue { get; set; }

    public Guid ActorUserId { get; set; }

    public Guid CorrelationId { get; set; }

    public string? LeSource { get; set; }

    public DateTimeOffset OccurredAt { get; set; }
}
