using Diten.CrmService.Application.Common;
using Diten.CrmService.Application.Common.Models;
using Diten.CrmService.Application.Common.ReferenceValidation;
using Diten.CrmService.Application.Features.Territory.ResourceAssignments;
using Diten.CrmService.Domain.Entities;
using Diten.CrmService.Domain.Repositories;
using MediatR;

namespace Diten.CrmService.Application.Features.Territory.Models.Handlers;

internal static class TerritoryLifecycle
{
    public const string Draft = "draft";
    public const string Active = "active";
    public const string Inactive = "inactive";
    public const string Archived = "archived";

    public static bool Is(string? value, string expected)
        => string.Equals(value, expected, StringComparison.OrdinalIgnoreCase);

    public static bool IsExpired(TerritoryModel model, DateTimeOffset now)
        => model.EffectiveTo is { } end && end < now;

    public static bool SameScope(TerritoryModel left, TerritoryModel right)
        => Normalize(left.CountryScope) == Normalize(right.CountryScope)
           && ScopeSet(left).SetEquals(ScopeSet(right));

    public static bool Overlaps(TerritoryModel left, TerritoryModel right)
    {
        var leftEnd = left.EffectiveTo ?? DateTimeOffset.MaxValue;
        var rightEnd = right.EffectiveTo ?? DateTimeOffset.MaxValue;
        return left.EffectiveFrom <= rightEnd && right.EffectiveFrom <= leftEnd;
    }

    private static string Normalize(string? value) => (value ?? string.Empty).Trim().ToUpperInvariant();

    private static HashSet<string> ScopeSet(TerritoryModel model)
        => (model.BusinessScopes ?? [])
            .Where(s => string.Equals(s.ScopeType, TerritoryReferenceSets.BusinessUnitScopeType, StringComparison.OrdinalIgnoreCase))
            .Select(s => Normalize(s.ScopeCode))
            .Where(s => s.Length > 0)
            .ToHashSet(StringComparer.Ordinal);
}

public abstract class TerritoryLifecycleHandlerBase
{
    protected readonly ITenantContext Tenant;
    protected readonly ITerritoryModelRepository Models;
    protected readonly ITerritoryNodeRepository Nodes;
    protected readonly ITerritoryReferenceValidator References;
    protected readonly ITerritoryLifecycleAuditPublisher Audit;

    protected TerritoryLifecycleHandlerBase(
        ITenantContext tenant,
        ITerritoryModelRepository models,
        ITerritoryNodeRepository nodes,
        ITerritoryReferenceValidator references,
        ITerritoryLifecycleAuditPublisher audit)
    {
        Tenant = tenant;
        Models = models;
        Nodes = nodes;
        References = references;
        Audit = audit;
    }

    protected static TerritoryLifecycleAuditPayload Payload(
        Guid tenantId, TerritoryModel model, string previous, string next, string? reason, string? correlationId,
        string? computedStatus = null, Guid? nodeId = null)
        => new(tenantId, model.Id, nodeId, previous, next, computedStatus, "authenticated-user",
            reason?.Trim(), correlationId?.Trim(), DateTimeOffset.UtcNow);

    protected async Task<bool> PublishedAsync(string setCode, string value, CancellationToken cancellationToken)
        => await References.ValidateValueAsync(setCode, value, cancellationToken) == ReferenceValidationStatus.Valid;
}

public sealed class ActivateTerritoryModelHandler : TerritoryLifecycleHandlerBase,
    IRequestHandler<ActivateTerritoryModelCommand, Response<bool>>
{
    /// <summary>FU04B: the actor is not carried into the Application layer today (same convention the lifecycle
    /// audit payload already uses). The real actor lives in the MOD-0021 audit event.</summary>
    internal const string ActivationActor = "authenticated-user";

    private readonly ITerritoryResourceAssignmentRepository _assignments;
    private readonly ITerritoryActivationUnitOfWork _unitOfWork;
    private readonly ITerritoryResourceAssignmentPlanSnapshotRepository _planSnapshots;
    private readonly IAccountTerritoryAssignmentRepository? _accountAssignments;
    private readonly ITerritoryAssignmentRuleRepository? _rules;

    public ActivateTerritoryModelHandler(
        ITenantContext tenant, ITerritoryModelRepository models, ITerritoryNodeRepository nodes,
        ITerritoryReferenceValidator references, ITerritoryLifecycleAuditPublisher audit,
        ITerritoryResourceAssignmentRepository assignments, ITerritoryActivationUnitOfWork unitOfWork,
        ITerritoryResourceAssignmentPlanSnapshotRepository planSnapshots,
        IAccountTerritoryAssignmentRepository? accountAssignments = null,
        ITerritoryAssignmentRuleRepository? rules = null)
        : base(tenant, models, nodes, references, audit)
        => (_assignments, _unitOfWork, _planSnapshots, _accountAssignments, _rules)
            = (assignments, unitOfWork, planSnapshots, accountAssignments, rules);

    public async Task<Response<bool>> Handle(ActivateTerritoryModelCommand request, CancellationToken cancellationToken)
    {
        if (Tenant.TenantId is not { } tenantId)
            return Response<bool>.Fail("Tenant context is required.", 400);

        var model = await Models.GetByIdAsync(tenantId, request.Id, cancellationToken);
        if (model is null)
            return Response<bool>.Fail("Territory model not found.", 404);

        var previous = model.Status;
        async Task<Response<bool>> Reject(string message, int status)
        {
            await Audit.PublishAsync(TerritoryLifecycleAuditEvents.ModelActivationRejected,
                Payload(tenantId, model, previous, previous, request.Reason, request.CorrelationId,
                    TerritoryLifecycle.IsExpired(model, DateTimeOffset.UtcNow) ? "expired" : previous), cancellationToken);
            return Response<bool>.Fail(message, status);
        }

        if (!TerritoryLifecycle.Is(previous, TerritoryLifecycle.Draft)
            && !TerritoryLifecycle.Is(previous, TerritoryLifecycle.Inactive))
            return await Reject("Only a draft or inactive territory model can be activated.", 409);

        if (model.EffectiveTo is { } end && end < DateTimeOffset.UtcNow)
            return await Reject("An expired territory model cannot be activated.", 409);

        if (model.EffectiveTo is { } invalidEnd && invalidEnd < model.EffectiveFrom)
            return await Reject("Territory model effective date window is invalid.", 400);

        if (!await PublishedAsync(TerritoryReferenceSets.TerritoryModelStatus, TerritoryLifecycle.Active, cancellationToken)
            || !await PublishedAsync(TerritoryReferenceSets.TerritoryNodeStatus, TerritoryLifecycle.Active, cancellationToken))
            return await Reject("Lifecycle reference values are not published.", 400);

        var readiness = await References.GetReadinessAsync(cancellationToken);
        if (readiness.Any(r => r.Required && !r.Ready))
            return await Reject("Territory contract reference readiness is false.", 409);

        var nodes = await Nodes.ListByModelAsync(tenantId, model.Id, cancellationToken);
        if (nodes.Count == 0)
            return await Reject("At least one valid territory node is required for activation.", 409);

        TerritoryModel? sourceModel = null;
        IReadOnlyList<TerritoryNode> sourceNodes = [];
        IReadOnlyList<AccountTerritoryAssignment> sourceAccountAssignments = [];
        IReadOnlyList<TerritoryAssignmentRule> targetRules = [];
        if (model.BasedOnModelId is { } sourceModelId)
        {
            if (_accountAssignments is null || _rules is null)
                return await Reject("Territory model versioning services are not available.", 500);

            sourceModel = await Models.GetByIdAsync(tenantId, sourceModelId, cancellationToken);
            if (sourceModel is null)
                return await Reject("The source territory model no longer exists.", 409);
            if (!TerritoryLifecycle.Is(sourceModel.Status, TerritoryLifecycle.Active))
                return await Reject("The source territory model must still be active when its draft version is activated.", 409);
            if (model.EffectiveFrom > DateTimeOffset.UtcNow)
                return await Reject("A versioned draft can only be activated on or after its effective start date.", 409);

            sourceNodes = await Nodes.ListByModelAsync(tenantId, sourceModel.Id, cancellationToken);
            var existingTargetAssignments = await _accountAssignments.ListByModelAsync(tenantId, model.Id, cancellationToken);
            if (existingTargetAssignments.Any(a => !a.IsDeleted && !string.Equals(a.AssignmentStatus, "ended", StringComparison.OrdinalIgnoreCase)))
                return await Reject("The draft version already has operational account assignments; automatic carry-forward would conflict.", 409);

            sourceAccountAssignments = (await _accountAssignments.ListByModelAsync(tenantId, sourceModel.Id, cancellationToken))
                .Where(a => !a.IsDeleted
                            && string.Equals(a.AssignmentStatus, "active", StringComparison.OrdinalIgnoreCase)
                            && a.EndedAt is null
                            && a.EffectiveFrom <= model.EffectiveFrom
                            && (a.EffectiveTo is null || a.EffectiveTo >= model.EffectiveFrom))
                .ToList();
            targetRules = await _rules.ListByModelAsync(tenantId, model.Id, cancellationToken);

            var duplicateNodeCode = nodes.GroupBy(n => n.TerritoryCode.Trim(), StringComparer.OrdinalIgnoreCase)
                .FirstOrDefault(group => group.Count() > 1);
            if (duplicateNodeCode is not null)
                return await Reject($"Target territory code '{duplicateNodeCode.Key}' is duplicated; account coverage cannot be mapped safely.", 409);
            var targetNodeCodes = nodes.ToDictionary(n => n.TerritoryCode.Trim(), StringComparer.OrdinalIgnoreCase);
            var missingNodeCode = sourceAccountAssignments.Select(a => a.TerritoryNodeCode.Trim())
                .FirstOrDefault(code => !targetNodeCodes.ContainsKey(code));
            if (missingNodeCode is not null)
                return await Reject($"Account coverage targets territory code '{missingNodeCode}', which is missing from the new version.", 409);

            var duplicateRuleCode = targetRules.GroupBy(r => r.RuleCode.Trim(), StringComparer.OrdinalIgnoreCase)
                .FirstOrDefault(group => group.Count() > 1);
            if (duplicateRuleCode is not null)
                return await Reject($"Target assignment rule code '{duplicateRuleCode.Key}' is duplicated; provenance cannot be mapped safely.", 409);
            var targetRuleCodes = targetRules.ToDictionary(r => r.RuleCode.Trim(), StringComparer.OrdinalIgnoreCase);
            var missingRuleCode = sourceAccountAssignments
                .Where(a => a.AppliedRuleId is not null && !string.IsNullOrWhiteSpace(a.AppliedRuleCode))
                .Select(a => a.AppliedRuleCode!.Trim())
                .FirstOrDefault(code => !targetRuleCodes.ContainsKey(code));
            if (missingRuleCode is not null)
                return await Reject($"Account coverage references assignment rule '{missingRuleCode}', which is missing from the new version.", 409);
            if (sourceAccountAssignments.Any(a => a.AppliedRuleId is not null && string.IsNullOrWhiteSpace(a.AppliedRuleCode)))
                return await Reject("Account coverage has rule provenance without a rule code; it cannot be mapped safely.", 409);
        }

        var candidates = await Models.ListActiveAsync(tenantId, model.Id, cancellationToken);
        candidates = candidates.Where(other => other.Id != sourceModel?.Id).ToList();
        if (candidates.Any(other => TerritoryLifecycle.SameScope(model, other) && TerritoryLifecycle.Overlaps(model, other)))
            return await Reject("An overlapping active territory model already exists for the same country and business-unit scope.", 409);

        var resourceAssignments = await _assignments.ListByModelAsync(tenantId, model.Id, cancellationToken);
        foreach (var assignment in resourceAssignments.Where(a =>
                     TerritoryLifecycle.Is(a.Status, TerritoryResourceAssignmentValidation.DefaultStatus)))
        {
            if (!TerritoryPositionPolicy.TryResolve(assignment.EffectivePositionCode, out _)
                || !string.Equals(assignment.Position.PolicySource, TerritoryPositionPolicy.BuiltInSource, StringComparison.Ordinal))
            {
                return await Reject(
                    $"Resource assignment position '{assignment.EffectivePositionCode}' has no verified operational policy.", 409);
            }
        }

        var nodeMap = nodes.ToDictionary(n => n.Id);
        var conflictReport = TerritoryResourceConflictEngine.Report(resourceAssignments, nodeMap);
        if (conflictReport.Conflicts.Count > 0)
            return await Reject($"Resource assignment activation conflict: {conflictReport.Conflicts[0].Message}", 409);

        // FU04B — capture the plan baseline from the PROPOSED records, before they are flipped to active. Every
        // fail-closed guard above has already run, so reaching this line means the activation will be committed;
        // the snapshot then rides the same unit of work and dies with it if the commit rolls back.
        var proposed = resourceAssignments
            .Where(a => TerritoryLifecycle.Is(a.Status, TerritoryResourceAssignmentValidation.DefaultStatus))
            .ToList();
        var planSnapshot = await BuildPlanSnapshotAsync(tenantId, model, proposed, nodes, request, cancellationToken);

        model.Status = TerritoryLifecycle.Active;
        model.ChangeReason = request.Reason?.Trim();
        model.CorrelationId = request.CorrelationId?.Trim();
        model.UpdatedAt = DateTimeOffset.UtcNow;
        foreach (var node in nodes.Where(n => TerritoryLifecycle.Is(n.Status, TerritoryLifecycle.Draft)
                                              || TerritoryLifecycle.Is(n.Status, TerritoryLifecycle.Inactive)))
        {
            node.Status = TerritoryLifecycle.Active;
            node.CorrelationId = request.CorrelationId?.Trim();
            node.UpdatedAt = DateTimeOffset.UtcNow;
        }

        foreach (var assignment in resourceAssignments.Where(a =>
                     TerritoryLifecycle.Is(a.Status, TerritoryResourceAssignmentValidation.DefaultStatus)))
        {
            assignment.Status = TerritoryResourceAssignmentValidation.ActiveStatus;
            assignment.CorrelationId = request.CorrelationId?.Trim();
            assignment.ChangeReason = request.Reason?.Trim() ?? assignment.ChangeReason;
            assignment.UpdatedAt = DateTimeOffset.UtcNow;
        }

        if (sourceModel is null)
        {
            await _unitOfWork.CommitAsync(model, nodes, resourceAssignments, planSnapshot, cancellationToken);
        }
        else
        {
            var now = DateTimeOffset.UtcNow;
            var targetNodeCodes = nodes.ToDictionary(n => n.TerritoryCode.Trim(), StringComparer.OrdinalIgnoreCase);
            var targetRuleCodes = targetRules.ToDictionary(r => r.RuleCode.Trim(), StringComparer.OrdinalIgnoreCase);
            var endedSource = new List<AccountTerritoryAssignment>();
            var createdTarget = new List<AccountTerritoryAssignment>();
            foreach (var sourceAssignment in sourceAccountAssignments)
            {
                var targetNode = targetNodeCodes[sourceAssignment.TerritoryNodeCode.Trim()];
                var carriedEffectiveTo = Earlier(sourceAssignment.EffectiveTo, model.EffectiveTo);
                var targetRule = !string.IsNullOrWhiteSpace(sourceAssignment.AppliedRuleCode)
                    ? targetRuleCodes.GetValueOrDefault(sourceAssignment.AppliedRuleCode.Trim())
                    : null;

                sourceAssignment.AssignmentStatus = "ended";
                sourceAssignment.EffectiveTo = model.EffectiveFrom;
                sourceAssignment.EndedAt = now;
                sourceAssignment.EndedBy = ActivationActor;
                sourceAssignment.UpdatedBy = ActivationActor;
                sourceAssignment.UpdatedAt = now;
                sourceAssignment.CorrelationId = request.CorrelationId?.Trim();
                endedSource.Add(sourceAssignment);

                createdTarget.Add(new AccountTerritoryAssignment
                {
                    TenantId = tenantId,
                    AccountId = sourceAssignment.AccountId,
                    AccountCode = sourceAssignment.AccountCode,
                    AccountDisplayName = sourceAssignment.AccountDisplayName,
                    TerritoryModelId = model.Id,
                    TerritoryNodeId = targetNode.Id,
                    TerritoryNodeCode = targetNode.TerritoryCode,
                    TerritoryNodeName = targetNode.Name,
                    BusinessScopes = sourceAssignment.BusinessScopes.Select(s => new TerritoryBusinessScope
                    {
                        ScopeType = s.ScopeType,
                        ScopeCode = s.ScopeCode
                    }).ToList(),
                    AssignmentSource = "model-version-carry-forward",
                    AssignmentStatus = "active",
                    EffectiveFrom = model.EffectiveFrom,
                    EffectiveTo = carriedEffectiveTo,
                    AppliedFromPreviewRunId = sourceAssignment.AppliedFromPreviewRunId,
                    AppliedRuleId = targetRule?.Id,
                    AppliedRuleCode = targetRule?.RuleCode ?? sourceAssignment.AppliedRuleCode,
                    MigratedFromAssignmentId = sourceAssignment.Id,
                    MigratedFromModelId = sourceModel.Id,
                    ConflictPolicy = sourceAssignment.ConflictPolicy,
                    OverrideReason = sourceAssignment.OverrideReason,
                    CreatedBy = ActivationActor,
                    CorrelationId = request.CorrelationId?.Trim()
                });
            }

            sourceModel.Status = TerritoryLifecycle.Inactive;
            sourceModel.ChangeReason = $"Superseded by territory model {model.ModelCode}.";
            sourceModel.CorrelationId = request.CorrelationId?.Trim();
            sourceModel.UpdatedAt = now;
            foreach (var sourceNode in sourceNodes.Where(node => TerritoryLifecycle.Is(node.Status, TerritoryLifecycle.Active)))
            {
                sourceNode.Status = TerritoryLifecycle.Inactive;
                sourceNode.CorrelationId = request.CorrelationId?.Trim();
                sourceNode.UpdatedAt = now;
            }
            await _unitOfWork.CommitVersionCutoverAsync(model, nodes, resourceAssignments, planSnapshot,
                sourceModel, sourceNodes, endedSource, createdTarget, cancellationToken);
        }

        await Audit.PublishAsync(TerritoryLifecycleAuditEvents.ModelActivated,
            Payload(tenantId, model, previous, TerritoryLifecycle.Active, request.Reason, request.CorrelationId),
            cancellationToken);
        return Response<bool>.Success(true);
    }

    private static DateTimeOffset? Earlier(DateTimeOffset? left, DateTimeOffset? right)
        => left is null ? right : right is null ? left : left < right ? left : right;

    /// <summary>
    /// FU04B plan baseline (pack §7.5a / §22.4). Write-once: a re-activation gets the next
    /// <c>SnapshotVersion</c> and the earlier baseline is kept. The legacy flat RoleCode is deliberately NOT copied —
    /// the baseline is position-based only.
    /// </summary>
    private async Task<TerritoryResourceAssignmentPlanSnapshot?> BuildPlanSnapshotAsync(
        Guid tenantId,
        TerritoryModel model,
        IReadOnlyList<TerritoryResourceAssignment> proposed,
        IReadOnlyList<TerritoryNode> nodes,
        ActivateTerritoryModelCommand request,
        CancellationToken cancellationToken)
    {
        var previousVersion = (await _planSnapshots.GetLatestAsync(tenantId, model.Id, cancellationToken))?.SnapshotVersion ?? 0;
        var nodeMap = nodes.ToDictionary(n => n.Id);

        return new TerritoryResourceAssignmentPlanSnapshot
        {
            TenantId = tenantId,
            TerritoryModelId = model.Id,
            CapturedAt = DateTimeOffset.UtcNow,
            CapturedBy = ActivationActor,
            ActivationCorrelationId = request.CorrelationId?.Trim(),
            SnapshotVersion = previousVersion + 1,
            Lines = proposed.Select(a =>
            {
                var node = a.TerritoryId is { } id ? nodeMap.GetValueOrDefault(id) : null;
                return new TerritoryResourceAssignmentPlanSnapshotLine
                {
                    TerritoryNodeId = a.TerritoryId,
                    TerritoryNodeCode = node?.TerritoryCode ?? string.Empty,
                    TerritoryNodeName = node?.Name ?? string.Empty,
                    BusinessScopes = a.BusinessScopes.Select(s => s.ScopeCode).ToList(),
                    PositionCode = a.EffectivePositionCode,
                    PositionTitle = a.EffectivePositionTitle,
                    PositionType = a.Position.PositionType,
                    ResourceId = a.Resource.ResourceId,
                    ResourceType = a.Resource.ResourceType,
                    ResourceDisplayName = a.Resource.DisplayName,
                    PlannedEffectiveFrom = a.ValidFrom,
                    PlannedEffectiveTo = a.ValidTo,
                    IsPrimary = a.IsPrimary,
                    SourceAssignmentId = a.Id
                };
            }).ToList()
        };
    }
}

public sealed class DeactivateTerritoryModelHandler : TerritoryLifecycleHandlerBase,
    IRequestHandler<DeactivateTerritoryModelCommand, Response<bool>>
{
    public DeactivateTerritoryModelHandler(
        ITenantContext tenant, ITerritoryModelRepository models, ITerritoryNodeRepository nodes,
        ITerritoryReferenceValidator references, ITerritoryLifecycleAuditPublisher audit)
        : base(tenant, models, nodes, references, audit) { }

    public async Task<Response<bool>> Handle(DeactivateTerritoryModelCommand request, CancellationToken cancellationToken)
    {
        if (Tenant.TenantId is not { } tenantId)
            return Response<bool>.Fail("Tenant context is required.", 400);
        var model = await Models.GetByIdAsync(tenantId, request.Id, cancellationToken);
        if (model is null)
            return Response<bool>.Fail("Territory model not found.", 404);
        if (!TerritoryLifecycle.Is(model.Status, TerritoryLifecycle.Active))
            return Response<bool>.Fail("Only an active territory model can be deactivated.", 409);
        if (!await PublishedAsync(TerritoryReferenceSets.TerritoryModelStatus, TerritoryLifecycle.Inactive, cancellationToken)
            || !await PublishedAsync(TerritoryReferenceSets.TerritoryNodeStatus, TerritoryLifecycle.Inactive, cancellationToken))
            return Response<bool>.Fail("Lifecycle reference values are not published.", 400);

        var previous = model.Status;
        model.Status = TerritoryLifecycle.Inactive;
        model.ChangeReason = request.Reason?.Trim();
        model.CorrelationId = request.CorrelationId?.Trim();
        model.UpdatedAt = DateTimeOffset.UtcNow;
        await Models.UpdateAsync(model, cancellationToken);

        foreach (var node in (await Nodes.ListByModelAsync(tenantId, model.Id, cancellationToken))
                     .Where(n => TerritoryLifecycle.Is(n.Status, TerritoryLifecycle.Active)))
        {
            node.Status = TerritoryLifecycle.Inactive;
            node.CorrelationId = request.CorrelationId?.Trim();
            node.UpdatedAt = DateTimeOffset.UtcNow;
            await Nodes.UpdateAsync(node, cancellationToken);
        }

        await Audit.PublishAsync(TerritoryLifecycleAuditEvents.ModelDeactivated,
            Payload(tenantId, model, previous, TerritoryLifecycle.Inactive, request.Reason, request.CorrelationId),
            cancellationToken);
        return Response<bool>.Success(true);
    }
}

public sealed class ArchiveTerritoryModelHandler : TerritoryLifecycleHandlerBase,
    IRequestHandler<ArchiveTerritoryModelCommand, Response<bool>>
{
    public ArchiveTerritoryModelHandler(
        ITenantContext tenant, ITerritoryModelRepository models, ITerritoryNodeRepository nodes,
        ITerritoryReferenceValidator references, ITerritoryLifecycleAuditPublisher audit)
        : base(tenant, models, nodes, references, audit) { }

    public async Task<Response<bool>> Handle(ArchiveTerritoryModelCommand request, CancellationToken cancellationToken)
    {
        if (Tenant.TenantId is not { } tenantId)
            return Response<bool>.Fail("Tenant context is required.", 400);
        var model = await Models.GetByIdAsync(tenantId, request.Id, cancellationToken);
        if (model is null)
            return Response<bool>.Fail("Territory model not found.", 404);

        var expired = TerritoryLifecycle.IsExpired(model, DateTimeOffset.UtcNow);
        if (!TerritoryLifecycle.Is(model.Status, TerritoryLifecycle.Inactive) && !expired)
            return Response<bool>.Fail("Only an inactive or computed-expired territory model can be archived.", 409);
        if (!await PublishedAsync(TerritoryReferenceSets.TerritoryModelStatus, TerritoryLifecycle.Archived, cancellationToken)
            || !await PublishedAsync(TerritoryReferenceSets.TerritoryNodeStatus, TerritoryLifecycle.Archived, cancellationToken))
            return Response<bool>.Fail("Lifecycle reference values are not published.", 400);

        var previous = model.Status;
        model.Status = TerritoryLifecycle.Archived;
        model.ChangeReason = request.Reason?.Trim();
        model.CorrelationId = request.CorrelationId?.Trim();
        model.UpdatedAt = DateTimeOffset.UtcNow;
        await Models.UpdateAsync(model, cancellationToken);

        foreach (var node in await Nodes.ListByModelAsync(tenantId, model.Id, cancellationToken))
        {
            node.Status = TerritoryLifecycle.Archived;
            node.CorrelationId = request.CorrelationId?.Trim();
            node.UpdatedAt = DateTimeOffset.UtcNow;
            await Nodes.UpdateAsync(node, cancellationToken);
        }

        await Audit.PublishAsync(TerritoryLifecycleAuditEvents.ModelArchived,
            Payload(tenantId, model, previous, TerritoryLifecycle.Archived, request.Reason, request.CorrelationId,
                expired ? "expired" : null), cancellationToken);
        return Response<bool>.Success(true);
    }
}

public sealed class SoftDeleteDraftTerritoryModelHandler : TerritoryLifecycleHandlerBase,
    IRequestHandler<SoftDeleteDraftTerritoryModelCommand, Response<bool>>
{
    public SoftDeleteDraftTerritoryModelHandler(
        ITenantContext tenant, ITerritoryModelRepository models, ITerritoryNodeRepository nodes,
        ITerritoryReferenceValidator references, ITerritoryLifecycleAuditPublisher audit)
        : base(tenant, models, nodes, references, audit) { }

    public async Task<Response<bool>> Handle(SoftDeleteDraftTerritoryModelCommand request, CancellationToken cancellationToken)
    {
        if (Tenant.TenantId is not { } tenantId)
            return Response<bool>.Fail("Tenant context is required.", 400);
        var model = await Models.GetByIdAsync(tenantId, request.Id, cancellationToken);
        if (model is null)
            return Response<bool>.Fail("Territory model not found.", 404);
        if (!TerritoryLifecycle.Is(model.Status, TerritoryLifecycle.Draft))
        {
            await Audit.PublishAsync(TerritoryLifecycleAuditEvents.ModelDeleteRejected,
                Payload(tenantId, model, model.Status, model.Status, request.Reason, request.CorrelationId), cancellationToken);
            return Response<bool>.Fail("Only a draft territory model can be soft-deleted.", 409);
        }

        var now = DateTimeOffset.UtcNow;
        model.IsDeleted = true;
        model.DeletedAt = now;
        model.UpdatedAt = now;
        model.CorrelationId = request.CorrelationId?.Trim();
        await Models.UpdateAsync(model, cancellationToken);
        foreach (var node in await Nodes.ListByModelAsync(tenantId, model.Id, cancellationToken))
        {
            node.IsDeleted = true;
            node.DeletedAt = now;
            node.UpdatedAt = now;
            node.CorrelationId = request.CorrelationId?.Trim();
            await Nodes.UpdateAsync(node, cancellationToken);
        }

        await Audit.PublishAsync(TerritoryLifecycleAuditEvents.ModelSoftDeleted,
            Payload(tenantId, model, TerritoryLifecycle.Draft, "soft-deleted", request.Reason, request.CorrelationId),
            cancellationToken);
        return Response<bool>.Success(true);
    }
}
