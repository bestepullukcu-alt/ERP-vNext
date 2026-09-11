using Diten.CrmService.Application.Features.ContentComposition.Eligibility;
using Microsoft.Extensions.Logging;

namespace Diten.CrmService.Infrastructure.ContentComposition;

/// <summary>
/// SCMM-11 (CAND-CAP-0011, RM4) — default evaluation-log writer: emits a structured log entry carrying the policy,
/// version, resolved state, blocking level and reasons. CAND-CAP-0011 owns no durable eligibility-log store yet; a
/// persistent sink (or the governed audit append contract) is a follow. Fail-soft by contract — the caller (resolver)
/// already swallows a throw here, but this default never throws on its own.
/// </summary>
public sealed class LoggingEligibilityEvaluationLogWriter : IEligibilityEvaluationLogWriter
{
    private readonly ILogger<LoggingEligibilityEvaluationLogWriter> _logger;

    public LoggingEligibilityEvaluationLogWriter(ILogger<LoggingEligibilityEvaluationLogWriter> logger)
        => _logger = logger;

    public Task WriteAsync(EligibilityEvaluationLogEntry entry, CancellationToken cancellationToken)
    {
        _logger.LogInformation(
            "SCMM-11 eligibility evaluation module=CAND-CAP-0011 tenant={TenantId} policy={PolicyId} version={PolicyVersion} "
            + "state={State} blockingLevel={BlockingLevel} reasons={Reasons} pinned={PinnedCount} at={At} actor={Actor}",
            entry.TenantId, entry.PolicyId, entry.PolicyVersion, entry.State, entry.BlockingLevel,
            string.Join(",", entry.Reasons), entry.PinnedSelections.Count, entry.EvaluatedAtUtc, entry.Actor);
        return Task.CompletedTask;
    }
}
