using Diten.Platform.Application.Common;
using Diten.Platform.Application.Contracts;
using Diten.Platform.Common.Tenancy;
using Diten.Platform.Domain.Entities.DocumentManagement;
using Diten.Platform.Domain.Enums.DocumentManagement;
using Diten.Platform.Domain.Repositories;

namespace Diten.Platform.Application.Features.DocumentManagementGovernancePolicyPack;

/// <summary>
/// MOD-0029-FU31A — the API-facing orchestration over the FU31 seeder.
///
/// HISTORY-WRITER PLACEMENT (Option 2): the FU31 <see cref="DocumentGovernancePolicyPackSeeder"/> stays PURE — its
/// constructor is unchanged, so every FU31 test keeps passing and the seeder remains usable without a history
/// store. This service calls the seeder and then persists the application-history row. Preview never writes.
///
/// NON-DESTRUCTIVE: apply creates only missing policies (seeder contract), never overwrites an existing policy,
/// evaluates no retention subject and mutates no subject state. History is append-only — a repeat apply writes a
/// NEW row (0 created, all skipped) rather than updating the previous one.
/// </summary>
public sealed class DocumentGovernancePolicyPackApplicationService
{
    private readonly DocumentGovernancePolicyPackSeeder _seeder;
    private readonly IDocumentGovernancePolicyPackApplicationRepository _applications;
    private readonly ITenantContext _tenantContext;
    private readonly ICurrentUserContext _currentUser;

    public DocumentGovernancePolicyPackApplicationService(
        DocumentGovernancePolicyPackSeeder seeder,
        IDocumentGovernancePolicyPackApplicationRepository applications,
        ITenantContext tenantContext,
        ICurrentUserContext currentUser)
    {
        _seeder = seeder;
        _applications = applications;
        _tenantContext = tenantContext;
        _currentUser = currentUser;
    }

    /// <summary>Computes what an apply would do. Writes NOTHING — no policy row and no history row.</summary>
    public async Task<Response<GovernancePolicyPackPreviewModel>> PreviewAsync(string correlationId, CancellationToken ct = default)
    {
        if (!_tenantContext.IsResolved)
        {
            return Response<GovernancePolicyPackPreviewModel>.Fail(
                "Tenant context is required.", 400, GovernancePolicyPackReasonCodes.TenantRequired, correlationId);
        }

        GovernancePolicyPackApplicationResult result;
        try
        {
            result = await _seeder.PreviewDefaultPolicyPackAsync(correlationId, ct);
        }
        catch (Exception ex)
        {
            return Response<GovernancePolicyPackPreviewModel>.Fail(
                $"Policy pack preview failed: {ex.Message}", 500, GovernancePolicyPackReasonCodes.PreviewFailed, correlationId);
        }

        var manifest = DocumentGovernancePolicyPackManifest.Get();
        var definitions = result.Items
            .Select(i => new GovernancePolicyPackDefinitionSummary(i.Family, i.PolicyKey, NameOf(manifest, i.Family, i.PolicyKey), i.Status.ToString()))
            .ToList();

        var model = new GovernancePolicyPackPreviewModel(
            manifest.PackKey, manifest.PackName, manifest.PackVersion, manifest.SopReference,
            result.Items.Count,
            manifest.RetentionPolicies.Count, manifest.GDocPCorrectionPolicies.Count, manifest.SignaturePolicies.Count,
            ExistingCount: result.SkippedExistingCount,
            MissingCount: result.Items.Count(i => i.Status == PolicyPackItemStatus.Missing),
            ConflictCount: result.ConflictCount,
            result.WarningMessages,
            definitions);

        return Response<GovernancePolicyPackPreviewModel>.Success(model, 200, correlationId);
    }

    /// <summary>Creates the missing default policies and records an append-only history row.</summary>
    public async Task<Response<GovernancePolicyPackApplyModel>> ApplyAsync(string correlationId, CancellationToken ct = default)
    {
        if (!_tenantContext.IsResolved)
        {
            return Response<GovernancePolicyPackApplyModel>.Fail(
                "Tenant context is required.", 400, GovernancePolicyPackReasonCodes.TenantRequired, correlationId);
        }

        var tenantId = TenantGuard.RequireTenant(_tenantContext);
        var manifest = DocumentGovernancePolicyPackManifest.Get();

        GovernancePolicyPackApplicationResult result;
        try
        {
            result = await _seeder.ApplyDefaultPolicyPackAsync(correlationId, ct);
        }
        catch (Exception ex)
        {
            // Best-effort Failed history. There is no distributed transaction here: any policy the seeder already
            // created stays created (it is additive and non-destructive), and this row records that the run failed.
            var failed = NewApplication(tenantId, manifest, correlationId);
            failed.ApplicationStatus = DocumentGovernancePolicyPackApplicationStatus.Failed;
            failed.WarningMessages.Add($"Apply failed: {ex.Message}");
            try { await _applications.CreateAsync(failed, ct); } catch { /* history is evidence, never the failure path */ }

            return Response<GovernancePolicyPackApplyModel>.Fail(
                $"Policy pack apply failed: {ex.Message}", 500, GovernancePolicyPackReasonCodes.ApplyFailed, correlationId);
        }

        var createdKeys = KeysWith(result, PolicyPackItemStatus.Created);
        var skippedKeys = KeysWith(result, PolicyPackItemStatus.SkippedExisting);
        var conflictKeys = KeysWith(result, PolicyPackItemStatus.Conflict);

        var application = NewApplication(tenantId, manifest, correlationId);
        application.ApplicationStatus = result.ConflictCount > 0 || result.WarningMessages.Count > 0
            ? DocumentGovernancePolicyPackApplicationStatus.AppliedWithWarnings
            : DocumentGovernancePolicyPackApplicationStatus.Applied;
        application.CreatedPolicyCount = result.CreatedCount;
        application.SkippedExistingCount = result.SkippedExistingCount;
        application.ConflictCount = result.ConflictCount;
        application.WarningMessages = [.. result.WarningMessages];
        application.ConflictMessages = [.. result.Items.Where(i => i.Status == PolicyPackItemStatus.Conflict && i.Message is not null).Select(i => i.Message!)];
        application.CreatedRetentionPolicyIds = [.. result.CreatedRetentionPolicyIds];
        application.CreatedGDocPPolicyIds = [.. result.CreatedGDocPPolicyIds];
        application.CreatedSignaturePolicyIds = [.. result.CreatedSignaturePolicyIds];
        application.CreatedPolicyKeys = createdKeys;
        application.SkippedPolicyKeys = skippedKeys;
        application.ConflictPolicyKeys = conflictKeys;

        var saved = await _applications.CreateAsync(application, ct);

        var model = new GovernancePolicyPackApplyModel(
            manifest.PackKey, manifest.PackVersion, saved.Id, saved.ApplicationStatus,
            result.CreatedCount, result.SkippedExistingCount, result.ConflictCount,
            createdKeys, skippedKeys, conflictKeys, result.WarningMessages);

        return Response<GovernancePolicyPackApplyModel>.Success(model, 200, correlationId);
    }

    public async Task<Response<IReadOnlyList<GovernancePolicyPackApplicationSummaryModel>>> ListApplicationsAsync(
        string correlationId, CancellationToken ct = default)
    {
        if (!_tenantContext.IsResolved)
        {
            return Response<IReadOnlyList<GovernancePolicyPackApplicationSummaryModel>>.Fail(
                "Tenant context is required.", 400, GovernancePolicyPackReasonCodes.TenantRequired, correlationId);
        }

        var rows = await _applications.GetAllForTenantAsync(ct);
        IReadOnlyList<GovernancePolicyPackApplicationSummaryModel> models = rows
            .Select(a => new GovernancePolicyPackApplicationSummaryModel(
                a.Id, a.PackKey, a.PackVersion, a.ApplicationStatus, a.AppliedAt, a.CreatedBy,
                a.CreatedPolicyCount, a.SkippedExistingCount, a.ConflictCount))
            .ToList();

        return Response<IReadOnlyList<GovernancePolicyPackApplicationSummaryModel>>.Success(models, 200, correlationId);
    }

    /// <summary>Tenant-scoped read — a cross-tenant id resolves to not-found (no existence leakage).</summary>
    public async Task<Response<GovernancePolicyPackApplicationDetailModel>> GetApplicationAsync(
        Guid id, string correlationId, CancellationToken ct = default)
    {
        if (!_tenantContext.IsResolved)
        {
            return Response<GovernancePolicyPackApplicationDetailModel>.Fail(
                "Tenant context is required.", 400, GovernancePolicyPackReasonCodes.TenantRequired, correlationId);
        }

        var a = await _applications.GetByIdAsync(id, ct);
        if (a is null)
        {
            return Response<GovernancePolicyPackApplicationDetailModel>.Fail(
                "Governance policy pack application not found.", 404,
                GovernancePolicyPackReasonCodes.ApplicationNotFound, correlationId);
        }

        var model = new GovernancePolicyPackApplicationDetailModel(
            a.Id, a.PackKey, a.PackName, a.PackVersion, a.SopReference, a.ApplicationStatus, a.AppliedAt,
            a.CreatedBy, a.AppliedByUserId, a.AppliedByRole,
            a.CreatedPolicyCount, a.SkippedExistingCount, a.ConflictCount,
            a.WarningMessages, a.ConflictMessages,
            a.CreatedRetentionPolicyIds, a.CreatedGDocPPolicyIds, a.CreatedSignaturePolicyIds,
            a.CreatedPolicyKeys, a.SkippedPolicyKeys, a.ConflictPolicyKeys, a.PreviewOnly);

        return Response<GovernancePolicyPackApplicationDetailModel>.Success(model, 200, correlationId);
    }

    private DocumentGovernancePolicyPackApplication NewApplication(
        Guid tenantId, GovernancePolicyPackManifestModel manifest, string correlationId) => new()
    {
        Id = Guid.NewGuid(),
        TenantId = tenantId,
        PackKey = manifest.PackKey,
        PackName = manifest.PackName,
        PackVersion = manifest.PackVersion,
        SopReference = manifest.SopReference,
        AppliedAt = DateTimeOffset.UtcNow,
        AppliedByUserId = _currentUser.UserId == Guid.Empty ? null : _currentUser.UserId,
        PreviewOnly = false,
        CorrelationId = correlationId,
        CreatedBy = _currentUser.ActorName
    };

    private static List<string> KeysWith(GovernancePolicyPackApplicationResult r, PolicyPackItemStatus status) =>
        r.Items.Where(i => i.Status == status).Select(i => i.PolicyKey).ToList();

    private static string NameOf(GovernancePolicyPackManifestModel m, string family, string key) => family switch
    {
        "Retention" => m.RetentionPolicies.FirstOrDefault(p => p.PolicyKey == key)?.PolicyName ?? key,
        "GDocPCorrection" => m.GDocPCorrectionPolicies.FirstOrDefault(p => p.PolicyKey == key)?.PolicyName ?? key,
        "Signature" => m.SignaturePolicies.FirstOrDefault(p => p.PolicyKey == key)?.PolicyName ?? key,
        _ => key
    };
}
