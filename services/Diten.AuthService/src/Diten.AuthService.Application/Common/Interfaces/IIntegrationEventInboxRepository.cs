namespace Diten.AuthService.Application.Common.Interfaces;

public sealed record IntegrationEventInboxEntry(
    Guid EventId,
    string EventName,
    Guid TenantId,
    int? CompletionProtocolVersion);

public interface IIntegrationEventInboxRepository
{
    Task<IntegrationEventInboxEntry?> GetAsync(
        Guid eventId,
        string eventName,
        Guid tenantId,
        CancellationToken ct = default);

    Task MarkCompletedAsync(
        Guid eventId,
        string eventName,
        Guid tenantId,
        CancellationToken ct = default);

    // Atomically reserves the globally unique EventId with tenant-bound exact facts. The resulting row is
    // deliberately unconfirmed; only MarkCompletedAsync may publish the completion protocol marker.
    // The legacy tenant-activation endpoint also uses this seam and therefore remains unconfirmed.
    Task<bool> TryInsertAsync(Guid eventId, string eventName, Guid tenantId, CancellationToken ct = default);
}
