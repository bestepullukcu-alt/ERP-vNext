namespace Diten.AuthService.Application.Common.Interfaces;

public interface IIntegrationEventInboxRepository
{
    Task<bool> TryInsertAsync(Guid eventId, string eventName, Guid tenantId, CancellationToken ct = default);
}
