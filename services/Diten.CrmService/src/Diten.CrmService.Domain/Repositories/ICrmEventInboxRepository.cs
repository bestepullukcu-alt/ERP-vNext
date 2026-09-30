namespace Diten.CrmService.Domain.Repositories;

/// <summary>
/// WP-CL-BE-4 — consumed-integration-event inbox (<c>crm_event_inbox</c>), the AuthService
/// <c>IIntegrationEventInboxRepository</c> pattern: one row per EventId; a redelivered event is refused.
/// </summary>
public interface ICrmEventInboxRepository
{
    /// <summary>True on the first delivery of <paramref name="eventId"/>, false when it was already processed.</summary>
    Task<bool> TryInsertAsync(Guid eventId, string eventName, Guid tenantId, CancellationToken ct = default);
}
