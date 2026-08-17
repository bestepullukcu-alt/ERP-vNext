namespace Diten.Platform.Common.Authorization;

public interface IEntitlementAuditSink
{
    Task LogDeniedAsync(EntitlementAuditDenyContext context, CancellationToken cancellationToken);
}
