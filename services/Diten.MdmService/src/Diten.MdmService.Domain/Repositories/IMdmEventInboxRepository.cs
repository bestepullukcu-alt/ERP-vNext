namespace Diten.MdmService.Domain.Repositories;

/// <summary>WP-VW-W4 (W4-BE-b1) — the integration event inbox (<c>mdm_event_inbox</c>, the CRM <c>crm_event_inbox</c>
/// copy): one row per EventId, so a redelivered event is applied once.</summary>
public interface IMdmEventInboxRepository
{
    /// <summary>True on the first delivery of <paramref name="eventId"/>, false when it was already processed.</summary>
    Task<bool> TryInsertAsync(Guid eventId, string eventName, Guid tenantId, CancellationToken ct = default);
}
