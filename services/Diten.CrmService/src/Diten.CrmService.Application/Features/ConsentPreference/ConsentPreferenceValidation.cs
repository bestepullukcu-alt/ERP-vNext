using Diten.CrmService.Domain.Entities;

namespace Diten.CrmService.Application.Features.ConsentPreference;

/// <summary>
/// MOD-0164 FU02 structural validation. Every rule returns an error string (400 message) or null. The consent /
/// preference vocabulary is validated in-domain against the <c>Consent*</c> / <c>Preference*</c> constants — it is
/// structural, not tenant vocabulary, so it never fails open on an unpublished MOD-0048 set. Nothing here invents a
/// default: an absent consent state must be authored as <c>unknown</c>, and an absent preference stays absent.
/// </summary>
public static class ConsentPreferenceValidation
{
    public static string? ValidateSubjectType(string? subjectType)
        => ConsentSubjectType.IsValid(subjectType)
            ? null
            : $"SubjectType is required and must be one of: {string.Join(", ", ConsentSubjectType.All)}.";

    public static string? ValidateSubjectId(Guid subjectId)
        => subjectId == Guid.Empty ? "SubjectId is required and cannot be empty." : null;

    public static string? ValidateConsentChannel(string? channel)
        => ConsentChannel.IsValid(channel)
            ? null
            : $"Channel is required and must be one of: {string.Join(", ", ConsentChannel.All)}.";

    public static string? ValidatePreferenceChannel(string? channel)
        => PreferenceChannel.IsValid(channel)
            ? null
            : $"Channel is required and must be one of: {string.Join(", ", PreferenceChannel.All)} " +
              $"('{PreferenceChannel.AnyChannel}' applies the preference to every channel).";

    public static string? ValidatePurpose(string? purpose)
        => ConsentPurpose.IsValid(purpose)
            ? null
            : $"Purpose is required and must be one of: {string.Join(", ", ConsentPurpose.All)}.";

    public static string? ValidateLegalBasis(string? legalBasis)
        => ConsentLegalBasis.IsValid(legalBasis)
            ? null
            : $"LegalBasis is required and must be one of: {string.Join(", ", ConsentLegalBasis.All)}.";

    /// <summary>ConsentStatus is REQUIRED (never defaulted). An unknown state is authored explicitly as
    /// <c>unknown</c> — and <c>unknown</c> is never evaluated as allowed.</summary>
    public static string? ValidateConsentStatus(string? status)
        => ConsentStatuses.IsValid(status)
            ? null
            : $"ConsentStatus is required and must be one of: {string.Join(", ", ConsentStatuses.All)}. " +
              "A consent record is never hard-deleted; closing it is the archive endpoint.";

    public static string? ValidateSource(string? source)
        => ConsentSource.IsValid(source)
            ? null
            : $"Source is required and must be one of: {string.Join(", ", ConsentSource.All)}.";

    public static string? ValidatePreferenceType(string? preferenceType)
        => PreferenceType.IsValid(preferenceType)
            ? null
            : $"PreferenceType is required and must be one of: {string.Join(", ", PreferenceType.All)}.";

    public static string? ValidateEffectiveFrom(DateTimeOffset effectiveFrom)
        => effectiveFrom == default ? "EffectiveFrom is required." : null;

    public static string? ValidateEffectiveRange(DateTimeOffset effectiveFrom, DateTimeOffset? effectiveTo)
        => effectiveTo is { } to && to < effectiveFrom
            ? "EffectiveTo cannot be earlier than EffectiveFrom."
            : null;

    public static string? ValidatePriority(int priority)
        => priority < 1 ? "Priority is required and must be a positive number (smaller wins)." : null;

    /// <summary>ScopeType must be a known value when supplied; a ScopeId without a ScopeType is meaningless.</summary>
    public static string? ValidateScope(string? scopeType, Guid? scopeId)
    {
        var hasScopeId = scopeId is { } id && id != Guid.Empty;
        if (string.IsNullOrWhiteSpace(scopeType))
        {
            return hasScopeId ? "ScopeId requires a ScopeType." : null;
        }

        return ConsentScopeType.IsValid(scopeType)
            ? null
            : $"ScopeType must be one of: {string.Join(", ", ConsentScopeType.All)}.";
    }

    /// <summary>A withdrawal must record why — the reason is preserved forever (legacy withdrawal history is never lost).</summary>
    public static string? ValidateWithdrawal(string? status, string? withdrawalReason)
        => ConsentStatuses.Normalize(status) == ConsentStatuses.Withdrawn
           && string.IsNullOrWhiteSpace(withdrawalReason)
            ? "A withdrawn consent requires a WithdrawalReason."
            : null;

    /// <summary>
    /// Format-level evidence validation. FU02 does NOT resolve the reference against the MOD-0028/MOD-0029 document
    /// master (no cross-module fetch, no file copy, no render) — it only guarantees the pointer is well formed and
    /// attributed to a document module.
    /// </summary>
    public static string? ValidateEvidenceRef(ConsentEvidenceRefInput? evidence)
    {
        if (evidence is null)
        {
            return null;
        }

        if (!ConsentEvidenceRefType.IsValid(evidence.RefType))
        {
            return $"EvidenceRef.RefType must be one of: {string.Join(", ", ConsentEvidenceRefType.All)}.";
        }

        if (evidence.RefId == Guid.Empty)
        {
            return "EvidenceRef.RefId is required and cannot be empty.";
        }

        return ConsentEvidenceSourceModule.IsValid(evidence.SourceModule)
            ? null
            : "EvidenceRef.SourceModule must be a document module reference " +
              $"({string.Join(" or ", ConsentEvidenceSourceModule.All)}); MOD-0164 stores the pointer only.";
    }

    /// <summary>
    /// Restrictive boolean preferences must carry a boolean literal, and <c>frequency-cap</c> must carry a positive
    /// integer. Anything else would make a restriction (or a cap) ambiguous, and an ambiguous restriction must never
    /// be guessed at evaluation time.
    /// </summary>
    public static string? ValidatePreferenceValue(string? preferenceType, string? preferenceValue)
    {
        if (string.IsNullOrWhiteSpace(preferenceValue))
        {
            return "PreferenceValue is required.";
        }

        var type = PreferenceType.Normalize(preferenceType);
        var value = preferenceValue.Trim();

        if (PreferenceType.IsBooleanRestriction(type) && !bool.TryParse(value, out _))
        {
            return $"PreferenceValue for '{type}' must be 'true' or 'false' " +
                   "(only 'true' restricts; 'false' never grants consent).";
        }

        if (type == PreferenceType.FrequencyCap && !(int.TryParse(value, out var cap) && cap > 0))
        {
            return "PreferenceValue for 'frequency-cap' must be a positive integer. " +
                   "The cap is an advisory upper-bound signal; the frequency policy SoR stays MOD-0165.";
        }

        if (type == PreferenceType.PreferredChannel && !ConsentChannel.IsValid(value))
        {
            return $"PreferenceValue for 'preferred-channel' must be one of: {string.Join(", ", ConsentChannel.All)}.";
        }

        return null;
    }

    /// <summary>
    /// External references: SourceSystem + ExternalId are mandatory per line, at most one line may be primary, and a
    /// duplicate (SourceSystem, ExternalId) inside the same payload is a conflict — silent merge is forbidden.
    /// Returns (error, isConflict); the caller maps a conflict to 409 and everything else to 400.
    /// </summary>
    public static (string? Error, bool IsConflict) ValidateExternalReferences(
        IReadOnlyList<ConsentExternalReferenceInput>? references)
    {
        if (references is null || references.Count == 0)
        {
            return (null, false);
        }

        foreach (var reference in references)
        {
            if (string.IsNullOrWhiteSpace(reference.SourceSystem))
            {
                return ("ExternalReferences[].SourceSystem is required.", false);
            }

            if (string.IsNullOrWhiteSpace(reference.ExternalId))
            {
                return ("ExternalReferences[].ExternalId is required.", false);
            }
        }

        if (references.Count(r => r.IsPrimary) > 1)
        {
            return ("At most one external reference may be marked IsPrimary.", false);
        }

        var duplicate = references
            .GroupBy(r => (r.SourceSystem.Trim().ToLowerInvariant(), r.ExternalId.Trim()))
            .FirstOrDefault(g => g.Count() > 1);

        return duplicate is null
            ? (null, false)
            : ($"Duplicate external mapping '{duplicate.Key.Item1}/{duplicate.Key.Item2}' in the payload. " +
               "Silent merge is not performed; resolve the conflict explicitly.", true);
    }
}
