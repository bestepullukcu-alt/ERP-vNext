namespace Diten.Platform.Common.Authorization;

public sealed class NullEntitlementAuditSink : IEntitlementAuditSink
{
    public Task LogDeniedAsync(EntitlementAuditDenyContext context, CancellationToken cancellationToken)
    {
        return Task.CompletedTask;
    }
}
