using Diten.Platform.Domain.Enums;

namespace Diten.Platform.Application.Contracts.Audit;

public sealed class AuditBehaviorOptions
{
    /// <summary>
    /// Audit categories that, when paired with <see cref="AuditAppendResult.RejectedCritical"/>,
    /// cause <c>AuditBehavior</c> to abort the business command after a successful handler.
    /// </summary>
    /// <remarks>
    /// <para>
    /// <b>WARNING: Populating this set without a unit-of-work / TransactionBehavior in the
    /// MediatR pipeline can leave business state inconsistent with audit state.</b>
    /// If the handler has already committed business writes when audit append fails critically,
    /// throwing here surfaces a failure to the caller while the business state is persisted —
    /// inconsistent client perception and potential duplicate writes on retry.
    /// </para>
    /// <para>
    /// Initial whitelist MUST remain empty until atomic outbox/transaction guarantees exist.
    /// Add categories only after explicit review and only for security-sensitive operations.
    /// </para>
    /// </remarks>
    public ISet<AuditCategory> CriticalCategories { get; } = new HashSet<AuditCategory>();

    /// <summary>
    /// Ultra-specific infrastructure/audit request name fragments that bypass <c>AuditBehavior</c>.
    /// Only short, unambiguous names should be added; broad business words
    /// (e.g. <c>Health</c>, <c>Retry</c>, <c>Internal</c>, <c>Seed</c>) are forbidden because they
    /// would accidentally exclude legitimate business commands such as
    /// <c>UpdateTenantHealthMetricsCommand</c> or <c>RetryFailedPaymentCommand</c>.
    /// </summary>
    /// <remarks>
    /// For broad exclusions of system/internal commands prefer the explicit
    /// <see cref="IAuditExcludedRequest"/> marker on the command itself.
    /// Matching uses <see cref="Type.Name"/> only (no namespace substring) to avoid
    /// accidental namespace-wide exclusion.
    /// </remarks>
    public IList<string> AutoExcludedRequestNameFragments { get; } =
    [
        "AuditOutbox",
        "AuditAppend",
        "EnqueueAuditEvent",
        "Heartbeat",
        "DeadLetter"
    ];

    /// <summary>
    /// When <c>true</c> (default), an <see cref="IAuditableCommand"/> that does not implement
    /// <see cref="IAuditMetadataProvider"/> causes <c>AuditBehavior</c> to throw at runtime,
    /// preventing silent audit gaps. Set to <c>false</c> only for legacy migration scenarios
    /// where a warn-and-skip is temporarily acceptable.
    /// </summary>
    public bool RequireMetadataProvider { get; set; } = true;

    public bool IsCritical(AuditCategory category)
    {
        return CriticalCategories.Contains(category);
    }

    public bool IsAutoExcluded(Type requestType)
    {
        var requestName = requestType.Name;
        return AutoExcludedRequestNameFragments.Any(fragment =>
            requestName.Contains(fragment, StringComparison.OrdinalIgnoreCase));
    }
}
