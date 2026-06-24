using Diten.Platform.Application.Common;
using Diten.Platform.Application.Contracts;
using Diten.Platform.Application.Features.DocumentManagementContract;
using Diten.Platform.Common.Tenancy;
using Diten.Platform.Domain.Entities.DocumentManagement;
using Diten.Platform.Domain.Enums.DocumentManagement;
using Diten.Platform.Domain.Repositories;
using Microsoft.Extensions.Options;

namespace Diten.Platform.Application.Features.DocumentManagementInstantiation.Services;

public sealed class InstantiationService
{
    private const string ScopeSourceModule = "MOD-0028-FU05";
    private readonly IInstantiationPlanner _planner;
    private readonly ICollectionInstanceRepository _instanceRepository;
    private readonly IInstantiationOperationRepository _operationRepository;
    private readonly IInstantiationOutcomeRepository _outcomeRepository;
    private readonly ICurrentUserContext _currentUser;
    private readonly ITenantContext _tenantContext;
    private readonly DocumentManagementFeatureFlagOptions _featureFlags;

    public InstantiationService(
        IInstantiationPlanner planner,
        ICollectionInstanceRepository instanceRepository,
        IInstantiationOperationRepository operationRepository,
        IInstantiationOutcomeRepository outcomeRepository,
        ICurrentUserContext currentUser,
        ITenantContext tenantContext,
        IOptions<DocumentManagementFeatureFlagOptions> featureFlags)
    {
        _planner = planner;
        _instanceRepository = instanceRepository;
        _operationRepository = operationRepository;
        _outcomeRepository = outcomeRepository;
        _currentUser = currentUser;
        _tenantContext = tenantContext;
        _featureFlags = featureFlags.Value;
    }

    public async Task<Response<InstantiationResultModel>> DryRunAsync(
        Guid baselineReleaseId,
        InstantiationScopeRequest scope,
        InstantiationSelectionRequest selection,
        string correlationId,
        CancellationToken ct = default)
    {
        var planResponse = await _planner.PlanAsync(baselineReleaseId, scope, selection, correlationId, ct);
        if (!planResponse.IsSuccessful)
        {
            return Response<InstantiationResultModel>.Fail(
                planResponse.Errors,
                planResponse.StatusCode,
                planResponse.ReasonCode,
                correlationId);
        }

        var plan = planResponse.Data!;
        var diagnostics = BuildDiagnostics(plan);
        var result = new InstantiationResultModel(
            plan.OperationId,
            plan.BaselineReleaseId,
            plan.CompanyId,
            plan.InstanceToken,
            InstantiationOperationType.DryRun.ToWire(),
            diagnostics.Blocked ? InstantiationOperationStatus.Blocked.ToWire() : InstantiationOperationStatus.Completed.ToWire(),
            0,
            diagnostics.NodesToSkip,
            0,
            plan.Nodes.Count,
            correlationId,
            diagnostics.Outcomes,
            diagnostics);

        return Response<InstantiationResultModel>.Success(result, correlationId: correlationId);
    }

    public async Task<Response<InstantiationResultModel>> ExecuteAsync(
        Guid baselineReleaseId,
        InstantiationScopeRequest scope,
        InstantiationSelectionRequest selection,
        string correlationId,
        CancellationToken ct = default)
    {
        var planResponse = await _planner.PlanAsync(baselineReleaseId, scope, selection, correlationId, ct);
        if (!planResponse.IsSuccessful)
        {
            return Response<InstantiationResultModel>.Fail(
                planResponse.Errors,
                planResponse.StatusCode,
                planResponse.ReasonCode,
                correlationId);
        }

        if (planResponse.Data!.Blocked)
        {
            return Response<InstantiationResultModel>.Fail(
                planResponse.Data.Errors,
                400,
                DocumentManagementInstantiationReasonCodes.ValidationFailed,
                correlationId);
        }

        return await MaterializeAsync(planResponse.Data!, InstantiationOperationType.Execute, null, correlationId, ct);
    }

    public async Task<Response<InstantiationResultModel>> RetryAsync(
        Guid operationId,
        IReadOnlyList<string> nodeKeys,
        string correlationId,
        CancellationToken ct = default)
    {
        if (!_featureFlags.InstantiationRetryEnabled)
        {
            return Response<InstantiationResultModel>.Fail(
                "Retry endpoint is not enabled.",
                405,
                DocumentManagementInstantiationReasonCodes.RetryUnavailable,
                correlationId);
        }

        var source = await _operationRepository.GetByOperationIdAsync(operationId, ct);
        if (source is null)
        {
            return Response<InstantiationResultModel>.Fail(
                "Instantiation operation not found.",
                404,
                DocumentManagementInstantiationReasonCodes.NotFoundNonLeakage,
                correlationId);
        }

        var failed = await _outcomeRepository.GetRetryableFailedByOperationIdAsync(operationId, ct);
        var selectedKeys = nodeKeys.Count == 0
            ? failed.Select(x => x.NodeKey).ToHashSet(StringComparer.Ordinal)
            : nodeKeys.ToHashSet(StringComparer.Ordinal);
        var retryable = failed.Where(x => selectedKeys.Contains(x.NodeKey)).ToList();
        if (retryable.Count != selectedKeys.Count)
        {
            return Response<InstantiationResultModel>.Fail(
                "Only failed retryable nodes can be retried.",
                400,
                DocumentManagementInstantiationReasonCodes.ValidationFailed,
                correlationId);
        }

        var planResponse = await _planner.PlanAsync(
            source.BaselineReleaseId,
            new InstantiationScopeRequest(source.CompanyId, null, null, source.InstanceToken),
            InstantiationSelectionRequest.Default,
            correlationId,
            ct);
        if (!planResponse.IsSuccessful)
        {
            return Response<InstantiationResultModel>.Fail(
                planResponse.Errors,
                planResponse.StatusCode,
                planResponse.ReasonCode,
                correlationId);
        }

        return await MaterializeAsync(planResponse.Data!, InstantiationOperationType.Retry, selectedKeys, correlationId, ct);
    }

    private async Task<Response<InstantiationResultModel>> MaterializeAsync(
        InstantiationPlan plan,
        InstantiationOperationType operationType,
        ISet<string>? selectedNodeKeys,
        string correlationId,
        CancellationToken ct)
    {
        var tenantId = TenantGuard.RequireTenant(_tenantContext);
        var now = DateTimeOffset.UtcNow;
        var targetNodes = selectedNodeKeys is null
            ? plan.Nodes
            : plan.Nodes.Where(x => selectedNodeKeys.Contains(x.NodeKey)).ToList();

        var instancesToCreate = new List<CollectionInstance>();
        var outcomes = new List<InstantiationOutcome>();
        foreach (var node in targetNodes)
        {
            var existing = await _instanceRepository.GetByInstanceKeyAsync(node.InstanceKey, ct);
            if (existing is not null)
            {
                outcomes.Add(Outcome(plan, node, InstantiationOutcomeStatus.Skipped, "INSTANCE_ALREADY_EXISTS", "Instance already exists.", false));
                continue;
            }

            instancesToCreate.Add(new CollectionInstance
            {
                TenantId = tenantId,
                InstanceKey = node.InstanceKey,
                CompanyId = plan.CompanyId,
                BaselineReleaseId = plan.BaselineReleaseId,
                CanonicalId = node.CanonicalId,
                ParentCanonicalId = node.ParentCanonicalId,
                Name = node.Name,
                FullPath = node.FullPath,
                DisplayOrder = node.DisplayOrder,
                CollectionScopeType = CollectionScopeType.Company,
                InstanceStatus = CollectionInstanceStatus.Active,
                ScopeBindings = BuildScopeBindings(plan, now),
                InstanceToken = plan.InstanceToken,
                SourceDefinitionHash = node.SourceDefinitionHash,
                LastChangeAt = now,
                CreatedBy = _currentUser.ActorName
            });
            outcomes.Add(Outcome(plan, node, InstantiationOutcomeStatus.Created, "INSTANCE_CREATED", "Instance created.", false));
        }

        try
        {
            await _instanceRepository.CreateManyAsync(instancesToCreate, ct);
        }
        catch
        {
            outcomes.Clear();
            foreach (var node in targetNodes)
            {
                outcomes.Add(Outcome(plan, node, InstantiationOutcomeStatus.Failed, DocumentManagementInstantiationReasonCodes.Conflict, "Instance creation conflicted.", true));
            }
        }

        var created = outcomes.Count(x => x.Status == InstantiationOutcomeStatus.Created);
        var skipped = outcomes.Count(x => x.Status == InstantiationOutcomeStatus.Skipped);
        var failed = outcomes.Count(x => x.Status == InstantiationOutcomeStatus.Failed);
        var status = failed > 0 && created + skipped > 0
            ? InstantiationOperationStatus.Partial
            : failed > 0 ? InstantiationOperationStatus.Failed : InstantiationOperationStatus.Completed;

        var operation = new InstantiationOperation
        {
            TenantId = tenantId,
            OperationId = plan.OperationId,
            CompanyId = plan.CompanyId,
            BaselineReleaseId = plan.BaselineReleaseId,
            InstanceToken = plan.InstanceToken,
            OperationType = operationType,
            Status = status,
            Created = created,
            Skipped = skipped,
            Failed = failed,
            Total = outcomes.Count,
            CorrelationId = correlationId,
            RequestedBy = _currentUser.ActorName,
            StartedAt = now,
            CompletedAt = DateTimeOffset.UtcNow,
            CreatedBy = _currentUser.ActorName
        };

        await _operationRepository.CreateAsync(operation, ct);
        await _outcomeRepository.CreateManyAsync(outcomes, ct);

        var result = new InstantiationResultModel(
            operation.OperationId,
            operation.BaselineReleaseId,
            operation.CompanyId,
            operation.InstanceToken,
            operation.OperationType.ToWire(),
            operation.Status.ToWire(),
            operation.Created,
            operation.Skipped,
            operation.Failed,
            operation.Total,
            correlationId,
            outcomes.Select(InstantiationMapping.ToModel).ToList());

        return Response<InstantiationResultModel>.Success(result, operationType == InstantiationOperationType.Execute ? 201 : 200, correlationId);
    }

    private static InstantiationDiagnosticsModel BuildDiagnostics(InstantiationPlan plan)
    {
        var outcomes = plan.Nodes
            .Select(node => new InstantiationOutcomeModel(
                node.NodeKey,
                node.CanonicalId,
                node.Exists ? InstantiationOutcomeStatus.Skipped.ToWire() : "WOULD_CREATE",
                node.Exists ? "INSTANCE_ALREADY_EXISTS" : "INSTANCE_WOULD_CREATE",
                node.Exists ? "Instance already exists." : "Instance would be created.",
                false))
            .ToList();

        return new InstantiationDiagnosticsModel(
            !plan.Blocked,
            plan.Blocked,
            plan.Warnings,
            plan.Errors,
            plan.Nodes.Count(x => !x.Exists),
            plan.Nodes.Count(x => x.Exists),
            0,
            plan.SelectionMode.ToWire(),
            plan.SelectedCanonicalIds,
            plan.Nodes.Select(x => x.CanonicalId).ToList(),
            plan.IncludedAncestors,
            plan.IncludedDescendants,
            plan.ExcludedCanonicalIdsCount,
            plan.BlockedSelections,
            outcomes);
    }

    private static List<ScopeBinding> BuildScopeBindings(InstantiationPlan plan, DateTimeOffset now)
    {
        var bindings = new List<ScopeBinding>
        {
            new()
            {
                OrgBindingScopeType = OrgBindingScopeType.Company,
                OrgBindingScopeId = plan.CompanyId,
                ScopeSourceModule = ScopeSourceModule,
                BindingStatus = ScopeBindingStatus.Active,
                EffectiveFrom = now,
                LastValidatedAt = now
            }
        };

        if (plan.PlantId.HasValue)
        {
            bindings.Add(new ScopeBinding
            {
                OrgBindingScopeType = OrgBindingScopeType.Plant,
                OrgBindingScopeId = plan.PlantId.Value,
                ScopeSourceModule = ScopeSourceModule,
                BindingStatus = ScopeBindingStatus.Unvalidated,
                EffectiveFrom = now
            });
        }

        if (plan.BusinessUnitId.HasValue)
        {
            bindings.Add(new ScopeBinding
            {
                OrgBindingScopeType = OrgBindingScopeType.BusinessUnit,
                OrgBindingScopeId = plan.BusinessUnitId.Value,
                ScopeSourceModule = ScopeSourceModule,
                BindingStatus = ScopeBindingStatus.Unvalidated,
                EffectiveFrom = now
            });
        }

        return bindings;
    }

    private static InstantiationOutcome Outcome(
        InstantiationPlan plan,
        InstantiationPlanNode node,
        InstantiationOutcomeStatus status,
        string reasonCode,
        string message,
        bool retryable) =>
        new()
        {
            TenantId = plan.TenantId,
            OperationId = plan.OperationId,
            NodeKey = node.NodeKey,
            CanonicalId = node.CanonicalId,
            Status = status,
            ReasonCode = reasonCode,
            Message = message,
            Retryable = retryable
        };
}
