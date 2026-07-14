using Diten.Platform.Application.Common;
using Diten.Platform.Application.Contracts;
using Diten.Platform.Common.Tenancy;
using Diten.Platform.Domain.Entities.DocumentManagement;
using Diten.Platform.Domain.Enums.DocumentManagement;
using Diten.Platform.Domain.Repositories;

namespace Diten.Platform.Application.Features.DocumentManagementReconciliation;

/// <summary>
/// MOD-0028-FU09 — orchestrates a read-back reconciliation: builds the expected set from the register-backed
/// CollectionDefinitions, reads the live tree via a provider, runs the pure engine, and (on apply) persists the
/// findings as deviations idempotently. Read-only over MOD-0028; it never provisions, renames, moves or deletes
/// folders. Available for any baseline status (Draft dry-run, Effective, Superseded); apply only records findings
/// (non-destructive).
/// </summary>
public sealed class CollectionTreeReconciliationService
{
    private readonly IBaselineReleaseRepository _baselines;
    private readonly ICollectionDefinitionRepository _definitions;
    private readonly IReadOnlyList<ICollectionTreeReadBackProvider> _providers;
    private readonly IDocumentCollectionDeviationRepository _deviations;
    private readonly ITenantContext _tenantContext;
    private readonly ICurrentUserContext _currentUser;

    public CollectionTreeReconciliationService(
        IBaselineReleaseRepository baselines,
        ICollectionDefinitionRepository definitions,
        IEnumerable<ICollectionTreeReadBackProvider> providers,
        IDocumentCollectionDeviationRepository deviations,
        ITenantContext tenantContext,
        ICurrentUserContext currentUser)
    {
        _baselines = baselines;
        _definitions = definitions;
        _providers = providers.ToList();
        _deviations = deviations;
        _tenantContext = tenantContext;
        _currentUser = currentUser;
    }

    public async Task<Response<ReconciliationResult>> RunAsync(
        ReconciliationRequest request, bool apply, string correlationId, CancellationToken ct)
    {
        TenantGuard.RequireTenant(_tenantContext);

        var baseline = await _baselines.GetByIdAsync(request.BaselineReleaseId, ct);
        if (baseline is null)
        {
            return Response<ReconciliationResult>.Fail(
                "Baseline not found.", 404, ReconciliationReasonCodes.NotFoundNonLeakage, correlationId);
        }

        var provider = _providers.FirstOrDefault(p => p.Provider == request.Provider);
        if (provider is null)
        {
            return Response<ReconciliationResult>.Fail(
                $"No read-back provider registered for '{request.Provider}'.", 400,
                ReconciliationReasonCodes.ProviderUnavailable, correlationId);
        }

        var expected = (await _definitions.GetByBaselineAsync(baseline.Id, ct))
            .Where(d => d.Status == CollectionDefinitionStatus.Active)
            .Select(ToExpected)
            .ToList();

        IReadOnlyList<ReadBackNode> actual;
        try
        {
            actual = await provider.ReadAsync(baseline.Id, ct);
        }
        catch (ReadBackProviderUnavailableException ex)
        {
            return Response<ReconciliationResult>.Fail(
                ex.Message, 400, ReconciliationReasonCodes.ProviderUnavailable, correlationId);
        }

        var detected = CollectionTreeReconciliationEngine.Compare(expected, actual);
        var summary = CollectionTreeReconciliationEngine.Summarize(expected, actual, detected);

        if (apply)
        {
            await PersistDeviationsAsync(baseline.Id, detected, correlationId, ct);
        }

        var result = new ReconciliationResult(
            baseline.Id,
            baseline.Status.ToString().ToUpperInvariant(),
            request.Scope.ToString(),
            provider.Provider.ToString(),
            !apply,
            summary,
            detected);

        return Response<ReconciliationResult>.Success(result, 200, correlationId);
    }

    /// <summary>
    /// Idempotent persistence: an already-OPEN deviation with the same (type, register-id/expected-path) key is
    /// updated in place rather than duplicated. A previously CLOSED/RESOLVED deviation that reappears is recorded as a
    /// NEW open detection (the closed row is retained as history — decision documented in the FU report).
    /// </summary>
    private async Task PersistDeviationsAsync(Guid baselineReleaseId, IReadOnlyList<DeviationDetail> detected, string correlationId, CancellationToken ct)
    {
        var open = await _deviations.GetOpenByBaselineAsync(baselineReleaseId, ct);
        var openByKey = open
            .GroupBy(d => DeviationKey(d.DeviationType, d.RegisterFolderId, d.ExpectedFullPath), StringComparer.OrdinalIgnoreCase)
            .ToDictionary(g => g.Key, g => g.First(), StringComparer.OrdinalIgnoreCase);

        var now = DateTimeOffset.UtcNow;
        var actor = _currentUser.ActorName;

        foreach (var d in detected)
        {
            var key = DeviationKey(d.DeviationType, d.RegisterFolderId, d.ExpectedFullPath);
            if (openByKey.TryGetValue(key, out var existing))
            {
                existing.ActualFullPath = d.ActualFullPath;
                existing.Severity = d.Severity;
                existing.Description = d.Description;
                existing.DetectedAt = now;
                existing.DetectedBy = actor;
                existing.CorrelationId = correlationId;
                await _deviations.UpdateAsync(existing, ct);
                continue;
            }

            await _deviations.CreateAsync(new DocumentCollectionDeviation
            {
                TenantId = _tenantContext.TenantId,
                BaselineReleaseId = baselineReleaseId,
                CollectionInstanceId = d.CollectionInstanceId,
                RegisterFolderId = d.RegisterFolderId,
                ExpectedFullPath = d.ExpectedFullPath,
                ActualFullPath = d.ActualFullPath,
                DeviationType = d.DeviationType,
                Severity = d.Severity,
                Status = DeviationStatus.Open,
                Description = d.Description,
                DetectedAt = now,
                DetectedBy = actor,
                CorrelationId = correlationId
            }, ct);
        }
    }

    private static ExpectedNode ToExpected(CollectionDefinition d) => new(
        d.RegisterFolderId,
        d.RegisterParentFolderId,
        d.Name,
        d.FullPath,
        ParentPath(d.FullPath),
        d.AccessProfile,
        d.FolderType,
        d.RetentionClass,
        d.Id,
        null);

    private static string DeviationKey(CollectionDeviationType type, string? registerFolderId, string expectedFullPath) =>
        $"{type}|{(string.IsNullOrWhiteSpace(registerFolderId) ? expectedFullPath : registerFolderId).Trim().ToLowerInvariant()}";

    private static string? ParentPath(string fullPath)
    {
        var idx = (fullPath ?? string.Empty).LastIndexOf('/');
        return idx <= 0 ? null : fullPath![..idx];
    }
}
