namespace Diten.Application.EnterpriseStrategy.Shared;

public static class EnterpriseStrategyAuditExtensions
{
    public static Task WriteMutationAsync(
        this IEnterpriseStrategyAuditSink auditSink,
        string actor,
        string objectType,
        string objectId,
        string action,
        string correlationId,
        string sourceModule,
        string beforeSummary,
        string afterSummary,
        CancellationToken cancellationToken = default)
    {
        return auditSink.WriteAsync(new AuditEvent
        {
            Actor = actor,
            ObjectType = objectType,
            ObjectId = objectId,
            Action = action,
            CorrelationId = correlationId,
            SourceModule = sourceModule,
            BeforeSummary = beforeSummary,
            AfterSummary = afterSummary
        }, cancellationToken);
    }
}
