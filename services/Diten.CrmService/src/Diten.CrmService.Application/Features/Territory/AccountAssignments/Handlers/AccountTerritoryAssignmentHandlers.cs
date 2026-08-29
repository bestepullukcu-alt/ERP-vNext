using Diten.CrmService.Application.Common;
using Diten.CrmService.Application.Common.Models;
using Diten.CrmService.Application.Common.ReferenceValidation;
using Diten.CrmService.Application.Features.Territory.AssignmentRules;
using Diten.CrmService.Domain.Entities;
using Diten.CrmService.Domain.Repositories;
using MediatR;

namespace Diten.CrmService.Application.Features.Territory.AccountAssignments.Handlers;

public sealed class ApplyAccountTerritoryAssignmentsHandler
    : IRequestHandler<ApplyAccountTerritoryAssignmentsCommand, Response<AccountTerritoryAssignmentApplyResultDto>>
{
    private readonly ITenantContext _tenant;
    private readonly ITerritoryModelRepository _models;
    private readonly ITerritoryNodeRepository _nodes;
    private readonly ITerritoryAssignmentRuleRepository _rules;
    private readonly ITerritoryAccountReader _accounts;
    private readonly IAccountTerritoryAssignmentRepository _assignments;
    private readonly ITerritoryReferenceValidator _references;

    public ApplyAccountTerritoryAssignmentsHandler(
        ITenantContext tenant, ITerritoryModelRepository models, ITerritoryNodeRepository nodes,
        ITerritoryAssignmentRuleRepository rules, ITerritoryAccountReader accounts,
        IAccountTerritoryAssignmentRepository assignments, ITerritoryReferenceValidator references)
    {
        _tenant = tenant; _models = models; _nodes = nodes; _rules = rules; _accounts = accounts;
        _assignments = assignments; _references = references;
    }

    public async Task<Response<AccountTerritoryAssignmentApplyResultDto>> Handle(
        ApplyAccountTerritoryAssignmentsCommand request, CancellationToken cancellationToken)
    {
        if (_tenant.TenantId is not { } tenantId)
            return Response<AccountTerritoryAssignmentApplyResultDto>.Fail("Tenant context is required.", 400);
        if (request.SelectedRows is null || request.SelectedRows.Count == 0)
            return Response<AccountTerritoryAssignmentApplyResultDto>.Fail("At least one preview row must be selected.", 400);
        if (request.SelectedRows.Select(x => x.AccountId).Distinct().Count() != request.SelectedRows.Count)
            return Response<AccountTerritoryAssignmentApplyResultDto>.Fail("A selected account may appear only once in an apply batch.", 400);
        if (request.EffectiveTo is { } requestedEnd && requestedEnd < request.EffectiveFrom)
            return Response<AccountTerritoryAssignmentApplyResultDto>.Fail("EffectiveTo must be greater than or equal to EffectiveFrom.", 400);
        if (request.Override && string.IsNullOrWhiteSpace(request.OverrideReason))
            return Response<AccountTerritoryAssignmentApplyResultDto>.Fail("OverrideReason is required when override is requested.", 400);

        var model = await _models.GetByIdAsync(tenantId, request.ModelId, cancellationToken);
        if (model is null) return Response<AccountTerritoryAssignmentApplyResultDto>.Fail("Territory model not found.", 404);
        if (!string.Equals(model.Status, "active", StringComparison.OrdinalIgnoreCase))
            return Response<AccountTerritoryAssignmentApplyResultDto>.Fail("Account assignment apply is allowed only for an active territory model.", 409);
        // Compare by calendar date, not instant. The apply window comes from date-only pickers (bound to a
        // DateTimeOffset at the caller's local offset), while the model window carries its own stored offset.
        // An instant comparison rejects a same-date boundary when the two offsets differ (e.g. UTC vs +04),
        // so a full-model-window assignment would fail even though the displayed dates match. Dates match intent.
        if (request.EffectiveFrom.Date < model.EffectiveFrom.Date
            || model.EffectiveTo is { } modelEnd && (request.EffectiveTo is null || request.EffectiveTo.Value.Date > modelEnd.Date))
            return Response<AccountTerritoryAssignmentApplyResultDto>.Fail("Assignment effective window must stay inside the territory model window.", 400);

        foreach (var (set, value) in new[]
        {
            (TerritoryReferenceSets.TerritoryAssignmentStatus, "active"),
            (TerritoryReferenceSets.TerritoryAssignmentStatus, "ended"),
            (TerritoryReferenceSets.TerritoryAssignmentSource, request.Override ? "override" : "rule"),
            (TerritoryReferenceSets.TerritoryConflictPolicy, request.ConflictPolicy?.Trim() ?? string.Empty)
        })
        {
            var status = await _references.ValidateValueAsync(set, value, cancellationToken);
            if (status != ReferenceValidationStatus.Valid)
                return Response<AccountTerritoryAssignmentApplyResultDto>.Fail(
                    status == ReferenceValidationStatus.SetMissing
                        ? $"Required reference set '{set}' is not published yet."
                        : $"'{value}' is not a published value of reference set '{set}'.", 400);
        }

        var normalizedScopes = NormalizeScopes(request.BusinessScopes);
        var modelScopes = NormalizeScopes(model.BusinessScopes)
            .Select(ScopeKey).ToHashSet(StringComparer.OrdinalIgnoreCase);
        var outsideScopes = normalizedScopes.Where(s => !modelScopes.Contains(ScopeKey(s))).ToList();
        if (outsideScopes.Count > 0)
            return Response<AccountTerritoryAssignmentApplyResultDto>.Fail("BusinessScopes cannot exceed the territory model scope.", 400);

        var accountIds = request.SelectedRows.Select(x => x.AccountId).Distinct().ToArray();
        var accountMap = (await _accounts.GetByIdsAsync(tenantId, accountIds, cancellationToken))
            .ToDictionary(x => x.AccountId);
        if (accountMap.Count != accountIds.Length)
            return Response<AccountTerritoryAssignmentApplyResultDto>.Fail("One or more selected accounts were not found in this tenant.", 404);

        var nodeIds = request.SelectedRows.Select(x => x.TerritoryNodeId).Distinct().ToHashSet();
        var nodeMap = (await _nodes.ListByModelAsync(tenantId, model.Id, cancellationToken))
            .Where(n => nodeIds.Contains(n.Id)).ToDictionary(n => n.Id);
        if (nodeMap.Count != nodeIds.Count)
            return Response<AccountTerritoryAssignmentApplyResultDto>.Fail("One or more territory nodes were not found in this model.", 404);
        // Compare by calendar date, not instant (same reason as the model-window check above): the node's stored
        // offset vs the request's caller-local offset would otherwise reject a same-date boundary — e.g. a node
        // effective 2026-07-30 (+04) against an apply starting 2026-07-30 (UTC).
        if (nodeMap.Values.Any(n => !string.Equals(n.Status, "active", StringComparison.OrdinalIgnoreCase)
                                    || n.EffectiveFrom.Date > request.EffectiveFrom.Date
                                    || n.EffectiveTo is { } nodeEnd && nodeEnd.Date < request.EffectiveFrom.Date))
            return Response<AccountTerritoryAssignmentApplyResultDto>.Fail("Every target territory node must be active and effective.", 409);

        var ruleIds = request.SelectedRows.Where(x => x.RuleId.HasValue).Select(x => x.RuleId!.Value).Distinct().ToArray();
        var ruleMap = new Dictionary<Guid, TerritoryAssignmentRule>();
        foreach (var ruleId in ruleIds)
        {
            var rule = await _rules.GetByIdAsync(tenantId, model.Id, ruleId, cancellationToken);
            if (rule is null) return Response<AccountTerritoryAssignmentApplyResultDto>.Fail("Applied assignment rule not found.", 404);
            ruleMap[ruleId] = rule;
        }

        var existing = await _assignments.ListByModelAsync(tenantId, model.Id, cancellationToken);
        var conflicts = existing.Where(a =>
            accountIds.Contains(a.AccountId)
            && string.Equals(a.AssignmentStatus, "active", StringComparison.OrdinalIgnoreCase)
            && ScopesOverlap(a.BusinessScopes, normalizedScopes)
            && WindowsOverlap(a.EffectiveFrom, a.EffectiveTo, request.EffectiveFrom, request.EffectiveTo)).ToList();

        if (conflicts.Count > 0 && !request.Override)
            return Response<AccountTerritoryAssignmentApplyResultDto>.Fail(
                "One or more selected accounts already have an overlapping active assignment.", 409);

        // All validation and conflict checks above complete before the first persistence call (all-or-nothing for
        // controlled failures). Override closes history records; it never deletes or overwrites them.
        var now = DateTimeOffset.UtcNow;
        foreach (var conflict in conflicts)
        {
            conflict.AssignmentStatus = "ended";
            conflict.EffectiveTo = request.EffectiveFrom;
            conflict.EndedAt = now;
            conflict.UpdatedAt = now;
            conflict.OverrideReason = request.OverrideReason!.Trim();
            conflict.CorrelationId = request.CorrelationId?.Trim();
        }

        var created = request.SelectedRows.Select(row =>
        {
            var account = accountMap[row.AccountId];
            var node = nodeMap[row.TerritoryNodeId];
            var rule = row.RuleId is { } rid ? ruleMap.GetValueOrDefault(rid) : null;
            return new AccountTerritoryAssignment
            {
                TenantId = tenantId,
                AccountId = account.AccountId,
                AccountCode = account.AccountCode,
                AccountDisplayName = account.AccountName,
                TerritoryModelId = model.Id,
                TerritoryNodeId = node.Id,
                TerritoryNodeCode = node.TerritoryCode,
                TerritoryNodeName = node.Name,
                BusinessScopes = normalizedScopes.Select(CloneScope).ToList(),
                AssignmentSource = request.Override ? "override" : "rule",
                AssignmentStatus = "active",
                EffectiveFrom = request.EffectiveFrom,
                EffectiveTo = request.EffectiveTo,
                AppliedFromPreviewRunId = request.PreviewRunId,
                AppliedRuleId = rule?.Id,
                AppliedRuleCode = rule?.RuleCode,
                ConflictPolicy = request.ConflictPolicy.Trim(),
                OverrideReason = request.Override ? request.OverrideReason!.Trim() : null,
                CorrelationId = request.CorrelationId?.Trim()
            };
        }).ToList();

        await _assignments.CommitApplyAsync(conflicts, created, cancellationToken);
        return Response<AccountTerritoryAssignmentApplyResultDto>.Success(
            new(model.Id, request.PreviewRunId, created.Count, conflicts.Count, created.Select(AccountTerritoryAssignmentMapper.ToDto).ToList()), 201);
    }

    private static bool WindowsOverlap(DateTimeOffset aFrom, DateTimeOffset? aTo, DateTimeOffset bFrom, DateTimeOffset? bTo)
        => aFrom <= (bTo ?? DateTimeOffset.MaxValue) && bFrom <= (aTo ?? DateTimeOffset.MaxValue);

    private static bool ScopesOverlap(IReadOnlyList<TerritoryBusinessScope> a, IReadOnlyList<TerritoryBusinessScope> b)
    {
        if (a.Count == 0 || b.Count == 0) return true;
        var keys = a.Select(ScopeKey).ToHashSet(StringComparer.OrdinalIgnoreCase);
        return b.Any(x => keys.Contains(ScopeKey(x)));
    }

    private static List<TerritoryBusinessScope> NormalizeScopes(IReadOnlyList<TerritoryBusinessScope>? scopes)
        => (scopes ?? []).Where(s => s is not null && !string.IsNullOrWhiteSpace(s.ScopeType) && !string.IsNullOrWhiteSpace(s.ScopeCode))
            .GroupBy(ScopeKey, StringComparer.OrdinalIgnoreCase).Select(g => CloneScope(g.First())).ToList();
    private static string ScopeKey(TerritoryBusinessScope s) => $"{s.ScopeType.Trim()}::{s.ScopeCode.Trim()}";
    private static TerritoryBusinessScope CloneScope(TerritoryBusinessScope s)
        => new() { ScopeType = s.ScopeType.Trim(), ScopeCode = s.ScopeCode.Trim() };
}

public sealed class EndAccountTerritoryAssignmentHandler
    : IRequestHandler<EndAccountTerritoryAssignmentCommand, Response<bool>>
{
    private readonly ITenantContext _tenant;
    private readonly IAccountTerritoryAssignmentRepository _assignments;
    private readonly ITerritoryReferenceValidator _references;
    public EndAccountTerritoryAssignmentHandler(
        ITenantContext tenant, IAccountTerritoryAssignmentRepository assignments, ITerritoryReferenceValidator references)
        => (_tenant, _assignments, _references) = (tenant, assignments, references);

    public async Task<Response<bool>> Handle(EndAccountTerritoryAssignmentCommand request, CancellationToken cancellationToken)
    {
        if (_tenant.TenantId is not { } tenantId) return Response<bool>.Fail("Tenant context is required.", 400);
        if (string.IsNullOrWhiteSpace(request.Reason)) return Response<bool>.Fail("Reason is required.", 400);
        var assignment = await _assignments.GetByIdAsync(tenantId, request.ModelId, request.AssignmentId, cancellationToken);
        if (assignment is null) return Response<bool>.Fail("Account territory assignment not found.", 404);
        if (string.Equals(assignment.AssignmentStatus, "ended", StringComparison.OrdinalIgnoreCase))
            return Response<bool>.Fail("Account territory assignment is already ended.", 409);
        if (await _references.ValidateValueAsync(TerritoryReferenceSets.TerritoryAssignmentStatus, "ended", cancellationToken)
            != ReferenceValidationStatus.Valid)
            return Response<bool>.Fail("Required assignment status reference is not published.", 400);
        var end = request.EndDate ?? DateTimeOffset.UtcNow;
        if (end < assignment.EffectiveFrom) return Response<bool>.Fail("EndDate cannot be before EffectiveFrom.", 400);
        assignment.AssignmentStatus = "ended";
        assignment.EffectiveTo = end;
        assignment.EndedAt = DateTimeOffset.UtcNow;
        assignment.UpdatedAt = assignment.EndedAt;
        assignment.OverrideReason = request.Reason.Trim();
        assignment.CorrelationId = request.CorrelationId?.Trim();
        await _assignments.UpdateAsync(assignment, cancellationToken);
        return Response<bool>.Success(true);
    }
}
