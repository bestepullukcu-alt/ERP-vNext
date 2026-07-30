namespace Diten.Platform.Infrastructure.Persistence.Models;

internal sealed class PpmAuditInboxMessage
{
    public Guid Id { get; init; } = Guid.NewGuid();
    public string ConsumerName { get; init; } = string.Empty;
    public string EventId { get; init; } = string.Empty;
    public Guid TenantId { get; init; }
    public string PayloadSha256 { get; init; } = string.Empty;
    public string AuditOutboxIdempotencyKey { get; init; } = string.Empty;
    public DateTimeOffset AcceptedAtUtc { get; init; } = DateTimeOffset.UtcNow;
}
