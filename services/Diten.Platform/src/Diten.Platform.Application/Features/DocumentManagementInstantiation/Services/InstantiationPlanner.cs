using Diten.Platform.Application.Common;
using Diten.Platform.Application.Contracts;
using Diten.Platform.Application.Features.DocumentManagementContract;
using Diten.Platform.Application.Features.TenantOrganization.Services;
using Diten.Platform.Common.Tenancy;
using Diten.Platform.Domain.Entities.DocumentManagement;
using Diten.Platform.Domain.Enums.DocumentManagement;
using Diten.Platform.Domain.Repositories;
using Microsoft.Extensions.Options;

namespace Diten.Platform.Application.Features.DocumentManagementInstantiation.Services;

public sealed class InstantiationPlanner : IInstantiationPlanner
{
    private readonly IBaselineReleaseRepository _baselineRepository;
    private readonly ICollectionDefinitionRepository _definitionRepository;
    private readonly ICollectionInstanceRepository _instanceRepository;
    private readonly ILegalEntityReferenceValidator _legalEntityValidator;
    private readonly ITenantContext _tenantContext;
    private readonly CompanyInstanceKeyFactory _keyFactory;
    private readonly DocumentManagementFeatureFlagOptions _featureFlags;

    public InstantiationPlanner(
        IBaselineReleaseRepository baselineRepository,
        ICollectionDefinitionRepository definitionRepository,
        ICollectionInstanceRepository instanceRepository,
        ILegalEntityReferenceValidator legalEntityValidator,
        ITenantContext tenantContext,
        CompanyInstanceKeyFactory keyFactory,
        IOptions<DocumentManagementFeatureFlagOptions> featureFlags)
    {
        _baselineRepository = baselineRepository;
        _definitionRepository = definitionRepository;
        _instanceRepository = instanceRepository;
        _legalEntityValidator = legalEntityValidator;
        _tenantContext = tenantContext;
        _keyFactory = keyFactory;
        _featureFlags = featureFlags.Value;
    }

    public async Task<Response<InstantiationPlan>> PlanAsync(
        Guid baselineReleaseId,
        InstantiationScopeRequest scope,
        InstantiationSelectionRequest selection,
        string correlationId,
        CancellationToken ct = default)
    {
        ArgumentNullException.ThrowIfNull(scope);
        ArgumentNullException.ThrowIfNull(selection);
        var tenantId = TenantGuard.RequireTenant(_tenantContext);

        if (!_featureFlags.CompanyProvisioningEnabled)
        {
            return Fail("Company provisioning is disabled.", 400, DocumentManagementInstantiationReasonCodes.ValidationFailed, correlationId);
        }

        if (baselineReleaseId == Guid.Empty || scope.CompanyId == Guid.Empty)
        {
            return Fail("Baseline release and company are required.", 400, DocumentManagementInstantiationReasonCodes.ValidationFailed, correlationId);
        }

        var baseline = await _baselineRepository.GetByIdAsync(baselineReleaseId, ct);
        if (baseline is null)
        {
            return Fail("Baseline not found.", 404, DocumentManagementInstantiationReasonCodes.NotFoundNonLeakage, correlationId);
        }

        if (baseline.Status != BaselineReleaseStatus.Published)
        {
            return Fail("Only published baselines can be instantiated.", 400, DocumentManagementInstantiationReasonCodes.ValidationFailed, correlationId);
        }

        var companyValidation = await ValidateCompanyAsync(scope.CompanyId, correlationId, ct);
        if (!companyValidation.IsSuccessful)
        {
            return Fail(companyValidation.Errors.FirstOrDefault() ?? "Company is not referenceable.",
                companyValidation.StatusCode,
                DocumentManagementInstantiationReasonCodes.NotFoundNonLeakage,
                correlationId);
        }

        var definitions = (await _definitionRepository.GetByBaselineAsync(baseline.Id, ct))
            .Where(x => x.Status == CollectionDefinitionStatus.Active)
            .OrderBy(x => x.DisplayOrder)
            .ThenBy(x => x.FullPath, StringComparer.Ordinal)
            .ToList();
        if (definitions.Count == 0)
        {
            return Fail("Published baseline has no collection definitions.", 400, DocumentManagementInstantiationReasonCodes.ValidationFailed, correlationId);
        }

        var selectionPlan = BuildSelectionPlan(definitions, selection);
        if (selectionPlan.StatusCode != 0)
        {
            return Fail(selectionPlan.Errors[0], selectionPlan.StatusCode, DocumentManagementInstantiationReasonCodes.ValidationFailed, correlationId);
        }

        var nodes = new List<InstantiationPlanNode>(selectionPlan.IncludedCanonicalIds.Count);
        foreach (var definition in definitions.Where(x => selectionPlan.IncludedCanonicalIds.Contains(x.CanonicalId)))
        {
            var instanceKey = _keyFactory.Create(tenantId, scope.CompanyId, baseline.Id, definition.CanonicalId, scope.InstanceToken);
            var existing = await _instanceRepository.GetByInstanceKeyAsync(instanceKey, ct);
            nodes.Add(new InstantiationPlanNode(
                definition.CanonicalId,
                instanceKey,
                definition.CanonicalId,
                definition.ParentCanonicalId,
                definition.Name,
                definition.FullPath,
                definition.DisplayOrder,
                definition.DefinitionHash,
                existing is not null));
        }

        var plan = new InstantiationPlan(
            Guid.NewGuid(),
            tenantId,
            baseline.Id,
            scope.CompanyId,
            scope.PlantId,
            scope.BusinessUnitId,
            NormalizeToken(scope.InstanceToken),
            selection.SelectionMode,
            selectionPlan.SelectedCanonicalIds,
            selection.IncludeDescendants,
            selection.IncludeRequiredAncestors,
            selectionPlan.Blocked,
            _featureFlags.Mod0220FallbackEnabled ? ["Local-smoke LegalEntity fallback is enabled. Do not use this mode in production."] : [],
            selectionPlan.Errors,
            selectionPlan.IncludedAncestors,
            selectionPlan.IncludedDescendants,
            definitions.Count - selectionPlan.IncludedCanonicalIds.Count,
            selectionPlan.BlockedSelections,
            nodes,
            correlationId);

        return Response<InstantiationPlan>.Success(plan, correlationId: correlationId);
    }

    private static SelectionPlan BuildSelectionPlan(
        IReadOnlyList<CollectionDefinition> definitions,
        InstantiationSelectionRequest selection)
    {
        var byId = definitions.ToDictionary(x => x.CanonicalId, StringComparer.Ordinal);
        var children = definitions
            .Where(x => !string.IsNullOrWhiteSpace(x.ParentCanonicalId))
            .GroupBy(x => x.ParentCanonicalId!, StringComparer.Ordinal)
            .ToDictionary(x => x.Key, x => x.Select(y => y.CanonicalId).ToList(), StringComparer.Ordinal);

        if (selection.SelectionMode == InstantiationSelectionMode.FullTree)
        {
            return new SelectionPlan(
                definitions.Select(x => x.CanonicalId).ToHashSet(StringComparer.Ordinal),
                [],
                [],
                [],
                [],
                [],
                false,
                0);
        }

        if (selection.SelectionMode != InstantiationSelectionMode.SelectedBranches)
        {
            return SelectionPlan.Fail("Unsupported selection mode.", 400);
        }

        var selected = selection.SelectedCanonicalIds
            .Where(x => !string.IsNullOrWhiteSpace(x))
            .Select(x => x.Trim())
            .Distinct(StringComparer.Ordinal)
            .ToList();

        if (selected.Count == 0)
        {
            return SelectionPlan.Fail("At least one selected canonical id is required.", 400);
        }

        var invalid = selected.Where(x => !byId.ContainsKey(x)).ToList();
        if (invalid.Count > 0)
        {
            return SelectionPlan.Fail($"Invalid selected canonical id: {invalid[0]}.", 400);
        }

        var included = new HashSet<string>(StringComparer.Ordinal);
        var descendants = new HashSet<string>(StringComparer.Ordinal);
        var ancestors = new HashSet<string>(StringComparer.Ordinal);
        var blockedSelections = new List<string>();
        var errors = new List<string>();

        foreach (var canonicalId in selected)
        {
            included.Add(canonicalId);
            if (selection.IncludeDescendants)
            {
                AddDescendants(canonicalId, children, included, descendants);
            }
            else
            {
                blockedSelections.Add(canonicalId);
                errors.Add("includeDescendants=false is deferred for SELECTED_BRANCHES; use the default branch/subtree mode.");
            }
        }

        foreach (var canonicalId in included.ToList())
        {
            var current = byId[canonicalId];
            while (!string.IsNullOrWhiteSpace(current.ParentCanonicalId))
            {
                if (!byId.TryGetValue(current.ParentCanonicalId, out var parent))
                {
                    blockedSelections.Add(canonicalId);
                    errors.Add($"Parent canonical id '{current.ParentCanonicalId}' is missing for '{canonicalId}'.");
                    break;
                }

                if (!included.Contains(parent.CanonicalId))
                {
                    if (!selection.IncludeRequiredAncestors)
                    {
                        blockedSelections.Add(canonicalId);
                        errors.Add($"Required ancestor '{parent.CanonicalId}' is not included for '{canonicalId}'.");
                        break;
                    }

                    included.Add(parent.CanonicalId);
                    ancestors.Add(parent.CanonicalId);
                }

                current = parent;
            }
        }

        var hasCycle = HasCycle(included, byId);
        if (hasCycle)
        {
            errors.Add("Selected branch plan is cyclic.");
        }

        return new SelectionPlan(
            included,
            selected,
            ancestors.ToList(),
            descendants.ToList(),
            blockedSelections,
            errors.Distinct(StringComparer.Ordinal).ToList(),
            errors.Count > 0 || blockedSelections.Count > 0 || hasCycle,
            0);
    }

    private static void AddDescendants(
        string canonicalId,
        IReadOnlyDictionary<string, List<string>> children,
        ISet<string> included,
        ISet<string> descendants)
    {
        if (!children.TryGetValue(canonicalId, out var childIds))
        {
            return;
        }

        foreach (var childId in childIds)
        {
            included.Add(childId);
            descendants.Add(childId);
            AddDescendants(childId, children, included, descendants);
        }
    }

    private static bool HasCycle(
        IReadOnlySet<string> included,
        IReadOnlyDictionary<string, CollectionDefinition> byId)
    {
        foreach (var canonicalId in included)
        {
            var seen = new HashSet<string>(StringComparer.Ordinal);
            var current = canonicalId;
            while (byId.TryGetValue(current, out var node) && !string.IsNullOrWhiteSpace(node.ParentCanonicalId))
            {
                if (!seen.Add(current))
                {
                    return true;
                }

                current = node.ParentCanonicalId!;
            }
        }

        return false;
    }

    private async Task<Response<LegalEntityReferenceDto>> ValidateCompanyAsync(Guid companyId, string correlationId, CancellationToken ct)
    {
        var validation = await _legalEntityValidator.ValidateAsync(companyId, ct);
        if (validation.IsSuccessful)
        {
            return validation;
        }

        if (_featureFlags.Mod0220FallbackEnabled)
        {
            return Response<LegalEntityReferenceDto>.Success(
                new LegalEntityReferenceDto(companyId, "LOCAL-SMOKE", "LOCAL-SMOKE", "ACTIVE", true),
                correlationId: correlationId);
        }

        return validation;
    }

    private static string? NormalizeToken(string? token) =>
        string.IsNullOrWhiteSpace(token) ? null : token.Trim();

    private static Response<InstantiationPlan> Fail(string error, int status, string reasonCode, string correlationId) =>
        Response<InstantiationPlan>.Fail(error, status, reasonCode, correlationId);

    private sealed record SelectionPlan(
        HashSet<string> IncludedCanonicalIds,
        IReadOnlyList<string> SelectedCanonicalIds,
        IReadOnlyList<string> IncludedAncestors,
        IReadOnlyList<string> IncludedDescendants,
        IReadOnlyList<string> BlockedSelections,
        IReadOnlyList<string> Errors,
        bool Blocked,
        int StatusCode)
    {
        public static SelectionPlan Fail(string error, int statusCode) =>
            new(new HashSet<string>(StringComparer.Ordinal), [], [], [], [], [error], true, statusCode);
    }
}
