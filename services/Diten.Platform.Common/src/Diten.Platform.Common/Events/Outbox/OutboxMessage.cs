namespace Diten.Platform.Common.Events.Outbox;

using Diten.Platform.Common.Persistence;

/// <summary>
/// Represents a message stored in the database to be published eventually.
/// </summary>
public sealed class OutboxMessage : TenantScopedEntity
{
    public required string EventName { get; init; }
    public required string Payload { get; init; }
    public required string ContentType { get; init; } = "application/json";
    
    public DateTimeOffset? PublishedAt { get; set; }
    public int RetryCount { get; set; } = 0;
    public string? Error { get; set; }
    
    public bool IsProcessed => PublishedAt.HasValue;
}
