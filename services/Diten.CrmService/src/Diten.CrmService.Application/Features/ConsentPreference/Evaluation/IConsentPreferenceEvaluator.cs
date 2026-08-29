using Diten.CrmService.Application.Common;
using Diten.CrmService.Domain.Entities;
using Diten.CrmService.Domain.Repositories;
using Microsoft.Extensions.Logging;

namespace Diten.CrmService.Application.Features.ConsentPreference.Evaluation;

/// <summary>
/// MOD-0164 FU02 read-only consent/preference evaluation provider — the <b>single source of truth</b> for the question
/// "may this subject be reached on this channel, for this purpose, within this scope, at this instant?".
/// <para>
/// Both the FU02 HTTP endpoint (via its query handler) and future in-process consumers (MOD-0155 visit planning,
/// MOD-0165 FU04 campaign runtime, MOD-0167 segment consent filter) call THIS. No consumer re-implements or copies the
/// engine, and no consumer needs raw consent read access — the provider reports, it does not enforce.
/// </para>
/// <para>
/// <b>The provider never writes and never throws into a consumer.</b> An internal failure is returned as a controlled
/// <c>unknown</c> with the <c>consent_evaluation_error</c> reason code, because a 500 would tempt a caller to fall back
/// to "allowed". Unknown is never allowed.
/// </para>
/// </summary>
public interface IConsentPreferenceEvaluator
{
    Task<ConsentEvaluationResult> EvaluateAsync(ConsentEvaluationRequest request, CancellationToken cancellationToken);
}

public sealed class ConsentPreferenceEvaluator : IConsentPreferenceEvaluator
{
    private readonly ITenantContext _tenant;
    private readonly IConsentRecordRepository _consents;
    private readonly IPreferenceRecordRepository _preferences;
    private readonly ILogger<ConsentPreferenceEvaluator>? _logger;

    public ConsentPreferenceEvaluator(
        ITenantContext tenant,
        IConsentRecordRepository consents,
        IPreferenceRecordRepository preferences,
        ILogger<ConsentPreferenceEvaluator>? logger = null)
    {
        _tenant = tenant;
        _consents = consents;
        _preferences = preferences;
        _logger = logger;
    }

    public async Task<ConsentEvaluationResult> EvaluateAsync(
        ConsentEvaluationRequest request, CancellationToken cancellationToken)
    {
        var now = DateTimeOffset.UtcNow;

        // No tenant → evaluate against zero candidates → deterministic "unknown" (never a fabricated default).
        if (_tenant.TenantId is not { } tenantId)
        {
            return ConsentEvaluationEngine.Evaluate(
                request, Array.Empty<ConsentRecord>(), Array.Empty<PreferenceRecord>(), now);
        }

        try
        {
            var subjectType = ConsentSubjectType.Normalize(request.SubjectType);
            var channel = ConsentChannel.Normalize(request.Channel);

            var consents = await _consents.ListForEvaluationAsync(
                tenantId, subjectType, request.SubjectId, channel, cancellationToken);
            var preferences = await _preferences.ListForEvaluationAsync(
                tenantId, subjectType, request.SubjectId, cancellationToken);

            return ConsentEvaluationEngine.Evaluate(request, consents, preferences, now);
        }
        catch (Exception ex) when (ex is not OperationCanceledException)
        {
            // Controlled degradation: unknown + explicit error reason, never a 500 and never "allowed".
            _logger?.LogError(
                ex,
                "MOD-0164 consent evaluation failed for subject {SubjectType}/{SubjectId} channel {Channel} purpose {Purpose}; returning controlled unknown.",
                request.SubjectType, request.SubjectId, request.Channel, request.Purpose);

            return ConsentEvaluationEngine.ControlledUnknown(
                request, now,
                "Consent evaluation could not be completed; eligibility is reported as unknown (fail-closed). " +
                "Unknown is NOT allowed.");
        }
    }
}
