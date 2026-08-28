using Diten.Platform.Application.Contracts;
using Diten.Platform.Common.Tenancy;
using Diten.Platform.Domain.Entities.DocumentManagement;
using Diten.Platform.Domain.Enums.DocumentManagement;
using Diten.Platform.Domain.Repositories;

namespace Diten.Platform.Application.Features.DocumentManagementGovernancePolicyPack;

/// <summary>
/// MOD-0029-FU31 — applies the <see cref="DocumentGovernancePolicyPackManifest"/> to a tenant. Tenant-scoped
/// (server-resolved via <see cref="TenantGuard"/> — never from a client payload), idempotent (a policy whose key
/// already exists is skipped), and non-destructive (an existing policy with divergent core fields is reported as a
/// CONFLICT and left untouched — never overwritten). Apply CREATES ONLY missing policies; it evaluates no subjects,
/// mutates no existing record, grants no permission, and starts no workflow / signature / CAPA event.
///
/// Seeded policies are created <c>Active</c> so the SOP baseline is immediately usable by the evaluators (the whole
/// point of the pack — an empty tenant otherwise falls through to safe defaults and shows empty screens).
/// </summary>
public sealed class DocumentGovernancePolicyPackSeeder
{
    private readonly IDocumentRetentionPolicyRepository _retention;
    private readonly IDocumentGDocPCorrectionPolicyRepository _gdocp;
    private readonly IDocumentSignaturePolicyRepository _signatures;
    private readonly ITenantContext _tenantContext;
    private readonly ICurrentUserContext _currentUser;

    public DocumentGovernancePolicyPackSeeder(
        IDocumentRetentionPolicyRepository retention,
        IDocumentGDocPCorrectionPolicyRepository gdocp,
        IDocumentSignaturePolicyRepository signatures,
        ITenantContext tenantContext,
        ICurrentUserContext currentUser)
    {
        _retention = retention;
        _gdocp = gdocp;
        _signatures = signatures;
        _tenantContext = tenantContext;
        _currentUser = currentUser;
    }

    /// <summary>Read-only: reports what an apply WOULD do (missing / skipped-existing / conflict). Writes nothing.</summary>
    public Task<GovernancePolicyPackApplicationResult> PreviewDefaultPolicyPackAsync(string correlationId, CancellationToken ct = default) =>
        RunAsync(apply: false, correlationId, ct);

    /// <summary>Creates only the missing default policies for the current tenant. Idempotent and non-destructive.</summary>
    public Task<GovernancePolicyPackApplicationResult> ApplyDefaultPolicyPackAsync(string correlationId, CancellationToken ct = default) =>
        RunAsync(apply: true, correlationId, ct);

    private async Task<GovernancePolicyPackApplicationResult> RunAsync(bool apply, string correlationId, CancellationToken ct)
    {
        var tenantId = TenantGuard.RequireTenant(_tenantContext);
        var manifest = DocumentGovernancePolicyPackManifest.Get();

        var items = new List<PolicyPackItemOutcome>();
        var warnings = new List<string>();
        var createdRetention = new List<Guid>();
        var createdGDocP = new List<Guid>();
        var createdSignature = new List<Guid>();

        foreach (var def in manifest.RetentionPolicies)
        {
            var key = Normalize(def.PolicyKey);
            var existing = await _retention.GetByKeyAsync(key, ct);
            if (existing is not null)
            {
                RecordExisting(items, warnings, "Retention", key, RetentionConflicts(existing, def));
                continue;
            }

            if (!apply)
            {
                items.Add(new PolicyPackItemOutcome("Retention", key, PolicyPackItemStatus.Missing, null, "Would be created."));
                continue;
            }

            var created = await _retention.CreateAsync(BuildRetention(tenantId, key, def, correlationId), ct);
            createdRetention.Add(created.Id);
            items.Add(new PolicyPackItemOutcome("Retention", key, PolicyPackItemStatus.Created, created.Id, null));
        }

        foreach (var def in manifest.GDocPCorrectionPolicies)
        {
            var key = Normalize(def.PolicyKey);
            var existing = await _gdocp.GetByKeyAsync(key, ct);
            if (existing is not null)
            {
                RecordExisting(items, warnings, "GDocPCorrection", key, GDocPConflicts(existing, def));
                continue;
            }

            if (!apply)
            {
                items.Add(new PolicyPackItemOutcome("GDocPCorrection", key, PolicyPackItemStatus.Missing, null, "Would be created."));
                continue;
            }

            var created = await _gdocp.CreateAsync(BuildGDocP(tenantId, key, def, correlationId), ct);
            createdGDocP.Add(created.Id);
            items.Add(new PolicyPackItemOutcome("GDocPCorrection", key, PolicyPackItemStatus.Created, created.Id, null));
        }

        foreach (var def in manifest.SignaturePolicies)
        {
            var key = Normalize(def.PolicyKey);
            var existing = await _signatures.GetByKeyAsync(key, ct);
            if (existing is not null)
            {
                RecordExisting(items, warnings, "Signature", key, SignatureConflicts(existing, def));
                continue;
            }

            if (!apply)
            {
                items.Add(new PolicyPackItemOutcome("Signature", key, PolicyPackItemStatus.Missing, null, "Would be created."));
                continue;
            }

            var created = await _signatures.CreateAsync(BuildSignature(tenantId, key, def, correlationId), ct);
            createdSignature.Add(created.Id);
            items.Add(new PolicyPackItemOutcome("Signature", key, PolicyPackItemStatus.Created, created.Id, null));
        }

        var createdCount = createdRetention.Count + createdGDocP.Count + createdSignature.Count;
        var skipped = items.Count(i => i.Status == PolicyPackItemStatus.SkippedExisting);
        var conflicts = items.Count(i => i.Status == PolicyPackItemStatus.Conflict);
        var status = !apply ? "Preview" : (conflicts > 0 || warnings.Count > 0 ? "AppliedWithWarnings" : "Applied");

        return new GovernancePolicyPackApplicationResult(
            DocumentGovernancePolicyPackManifest.PackKey, DocumentGovernancePolicyPackManifest.PackVersion, tenantId,
            status, createdCount, skipped, conflicts, warnings, createdRetention, createdGDocP, createdSignature, items);
    }

    private static void RecordExisting(
        List<PolicyPackItemOutcome> items, List<string> warnings, string family, string key, string? conflictReason)
    {
        if (conflictReason is null)
        {
            items.Add(new PolicyPackItemOutcome(family, key, PolicyPackItemStatus.SkippedExisting, null, "Already exists; skipped."));
            return;
        }

        var message = $"{family} policy '{key}' already exists with different core fields ({conflictReason}); left unchanged.";
        warnings.Add(message);
        items.Add(new PolicyPackItemOutcome(family, key, PolicyPackItemStatus.Conflict, null, message));
    }

    private static string Normalize(string key) => key.Trim().ToUpperInvariant();

    // ── conflict detection (core fields only; presentation fields like PolicyName are ignored) ─────────
    private static string? RetentionConflicts(DocumentRetentionPolicy e, RetentionPolicyDefinition d)
    {
        var diffs = new List<string>();
        if (e.SubjectType != d.SubjectType) diffs.Add("SubjectType");
        if (e.IsPermanentRetention != d.IsPermanentRetention) diffs.Add("IsPermanentRetention");
        if (!e.IsPermanentRetention && e.MinimumRetentionYears != d.MinimumRetentionYears) diffs.Add("MinimumRetentionYears");
        return diffs.Count == 0 ? null : string.Join(", ", diffs);
    }

    private static string? GDocPConflicts(DocumentGDocPCorrectionPolicy e, GDocPPolicyDefinition d)
    {
        var diffs = new List<string>();
        if (e.SubjectType != d.SubjectType) diffs.Add("SubjectType");
        if (!string.Equals(e.FieldPathPattern, d.FieldPathPattern, StringComparison.OrdinalIgnoreCase)) diffs.Add("FieldPathPattern");
        return diffs.Count == 0 ? null : string.Join(", ", diffs);
    }

    private static string? SignatureConflicts(DocumentSignaturePolicy e, SignaturePolicyDefinition d)
    {
        var diffs = new List<string>();
        if (e.SignableSubjectType != d.SignableSubjectType) diffs.Add("SignableSubjectType");
        if (e.SignatureMeaning != d.SignatureMeaning) diffs.Add("SignatureMeaning");
        return diffs.Count == 0 ? null : string.Join(", ", diffs);
    }

    // ── entity builders (Active status; TenantId server-resolved; mirrors the FU15/21/23 create services) ──
    private DocumentRetentionPolicy BuildRetention(Guid tenantId, string key, RetentionPolicyDefinition d, string correlationId) => new()
    {
        Id = Guid.NewGuid(),
        TenantId = tenantId,
        PolicyKey = key,
        PolicyName = d.PolicyName,
        PolicyStatus = RetentionPolicyStatus.Active,
        SubjectType = d.SubjectType,
        RetentionClass = d.RetentionClass,
        MinimumRetentionYears = d.MinimumRetentionYears,
        RetentionTrigger = d.RetentionTrigger,
        RetainWhileEffective = d.RetainWhileEffective,
        RetainAfterRetirementYears = d.RetainAfterRetirementYears,
        RetainAfterSupersessionYears = d.RetainAfterSupersessionYears,
        IsPermanentRetention = d.IsPermanentRetention,
        RegulatoryBasis = d.RegulatoryBasis,
        IsLongestApplicableCandidate = true,
        CorrelationId = correlationId,
        CreatedBy = _currentUser.ActorName
    };

    private DocumentGDocPCorrectionPolicy BuildGDocP(Guid tenantId, string key, GDocPPolicyDefinition d, string correlationId) => new()
    {
        Id = Guid.NewGuid(),
        TenantId = tenantId,
        PolicyKey = key,
        PolicyName = d.PolicyName,
        PolicyStatus = GDocPCorrectionPolicyStatus.Active,
        SubjectType = d.SubjectType,
        FieldPathPattern = d.FieldPathPattern,
        RequiresCorrectionReason = d.RequiresCorrectionReason,
        RequiresEvidenceReference = d.RequiresEvidenceReference,
        RequiresReview = d.RequiresReview,
        RequiresDeviationReferenceForHighRisk = d.RequiresDeviationReferenceForHighRisk,
        AllowCorrectionAfterApproval = d.AllowCorrectionAfterApproval,
        AllowCorrectionAfterEffective = d.AllowCorrectionAfterEffective,
        IsBackdatingSensitive = d.IsBackdatingSensitive,
        IsStatusSensitive = d.IsStatusSensitive,
        IsEvidenceSensitive = d.IsEvidenceSensitive,
        Notes = d.Notes,
        CorrelationId = correlationId,
        CreatedBy = _currentUser.ActorName
    };

    private DocumentSignaturePolicy BuildSignature(Guid tenantId, string key, SignaturePolicyDefinition d, string correlationId) => new()
    {
        Id = Guid.NewGuid(),
        TenantId = tenantId,
        PolicyKey = key,
        PolicyName = d.PolicyName,
        PolicyStatus = SignaturePolicyStatus.Active,
        SignableSubjectType = d.SignableSubjectType,
        SignatureMeaning = d.SignatureMeaning,
        RequiresReAuthentication = d.RequiresReAuthentication,
        RequiresSecondFactor = d.RequiresSecondFactor,
        RequiresMeaningStatement = d.RequiresMeaningStatement,
        RequiresRepositoryAssessment = d.RequiresRepositoryAssessment,
        RequiresObjectFingerprint = d.RequiresObjectFingerprint,
        RequiresManifestation = d.RequiresManifestation,
        AllowedRepositoryTypes = [.. d.AllowedRepositoryTypes],
        AllowInterimRepositorySignature = d.AllowInterimRepositorySignature,
        InterimRepositoryBoundaryStatement = d.InterimRepositoryBoundaryStatement,
        CorrelationId = correlationId,
        CreatedBy = _currentUser.ActorName
    };
}
