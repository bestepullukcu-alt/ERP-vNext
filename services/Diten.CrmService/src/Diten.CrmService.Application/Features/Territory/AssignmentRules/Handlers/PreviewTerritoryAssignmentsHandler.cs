using Diten.CrmService.Application.Common;
using Diten.CrmService.Application.Common.Models;
using Diten.CrmService.Application.Common.ReferenceValidation;
using Diten.CrmService.Domain.Entities;
using Diten.CrmService.Domain.Repositories;
using MediatR;

namespace Diten.CrmService.Application.Features.Territory.AssignmentRules.Handlers;

/// <summary>
/// FU03 assignment preview. Reads rules + accounts, evaluates them and returns candidates and conflicts.
///
/// <para><b>Side-effect free by construction.</b> The only account seam injected here is
/// <see cref="ITerritoryAccountReader"/>, which has no mutating member, and no AccountTerritoryAssignment repository
/// exists in the codebase yet — so this handler cannot persist an assignment or touch the MOD-0149 master even if a
/// future edit tried to. Apply + history are FU05.</para>
/// </summary>
public sealed class PreviewTerritoryAssignmentsHandler
    : IRequestHandler<PreviewTerritoryAssignmentsCommand, Response<TerritoryAssignmentPreviewDto>>
{
    /// <summary>Upper bound on the account base a single preview scans, so a large tenant cannot turn preview into an
    /// unbounded scan. Reported back via <c>scannedAccounts</c> + a warning when the cap truncates the base.</summary>
    public const int DefaultMaxAccounts = 2000;
    public const int HardMaxAccounts = 10000;

    private readonly ITenantContext _tenant;
    private readonly ITerritoryModelRepository _models;
    private readonly ITerritoryNodeRepository _nodes;
    private readonly ITerritoryAssignmentRuleRepository _rules;
    private readonly ITerritoryAccountReader _accounts;
    private readonly ITerritoryReferenceValidator _references;

    public PreviewTerritoryAssignmentsHandler(
        ITenantContext tenant,
        ITerritoryModelRepository models,
        ITerritoryNodeRepository nodes,
        ITerritoryAssignmentRuleRepository rules,
        ITerritoryAccountReader accounts,
        ITerritoryReferenceValidator references)
    {
        _tenant = tenant;
        _models = models;
        _nodes = nodes;
        _rules = rules;
        _accounts = accounts;
        _references = references;
    }

    public async Task<Response<TerritoryAssignmentPreviewDto>> Handle(
        PreviewTerritoryAssignmentsCommand request, CancellationToken cancellationToken)
    {
        if (_tenant.TenantId is not { } tenantId)
        {
            return Response<TerritoryAssignmentPreviewDto>.Fail("Tenant context is required.", 400);
        }

        var model = await _models.GetByIdAsync(tenantId, request.ModelId, cancellationToken);
        if (model is null)
        {
            return Response<TerritoryAssignmentPreviewDto>.Fail("Territory model not found.", 404);
        }

        // Preview is a read: it is allowed while the model is being built (draft) and while it is operational
        // (active/inactive). An archived model is history — previewing it would suggest it can still be applied.
        if (string.Equals(model.Status, "archived", StringComparison.OrdinalIgnoreCase))
        {
            return Response<TerritoryAssignmentPreviewDto>.Fail(
                "Assignment preview is not available for an archived territory model.", 409);
        }

        // Fail-closed on the reference sets the rules themselves depend on.
        var readiness = await _references.GetReadinessAsync(cancellationToken);
        foreach (var setCode in new[] { TerritoryReferenceSets.TerritoryRuleType, TerritoryReferenceSets.TerritoryConflictPolicy })
        {
            if (readiness.Any(r => string.Equals(r.SetCode, setCode, StringComparison.OrdinalIgnoreCase) && !r.Ready))
            {
                return Response<TerritoryAssignmentPreviewDto>.Fail(
                    $"Required reference set '{setCode}' is not published yet (MOD-0048 authoring pending).", 400);
            }
        }

        var now = DateTimeOffset.UtcNow;

        // Evaluate "as of" a point INSIDE the model's own window. A planner building next year's model would
        // otherwise get an empty preview: every rule sits inside a future window and would be skipped as "not yet
        // effective". Clamping to the model window answers the question actually being asked — "what would this
        // model produce when it is in force?" — while a model that is currently in force still previews as of today.
        var effectiveAt = now;
        if (now < model.EffectiveFrom)
        {
            effectiveAt = model.EffectiveFrom;
        }
        else if (model.EffectiveTo is { } modelEnd && now > modelEnd)
        {
            effectiveAt = modelEnd;
        }

        var allRules = await _rules.ListByModelAsync(tenantId, request.ModelId, cancellationToken);
        if (request.RuleId is { } singleRuleId)
        {
            allRules = allRules.Where(r => r.Id == singleRuleId).ToList();
            if (allRules.Count == 0)
            {
                return Response<TerritoryAssignmentPreviewDto>.Fail("Assignment rule not found.", 404);
            }
        }

        var warnings = new List<string>();
        var skipped = new Dictionary<Guid, string>();
        var evaluable = new List<TerritoryAssignmentRule>();

        foreach (var rule in allRules)
        {
            if (!rule.IsEnabled)
            {
                skipped[rule.Id] = "rule disabled";
                continue;
            }

            if (rule.EffectiveFrom > effectiveAt)
            {
                skipped[rule.Id] = "not yet effective";
                continue;
            }

            if (rule.EffectiveTo is { } end && end < effectiveAt)
            {
                skipped[rule.Id] = "expired";
                continue;
            }

            if (!TerritoryRuleTypes.IsSupported(rule.RuleType))
            {
                skipped[rule.Id] = $"rule type '{rule.RuleType}' is not evaluated in FU03";
                continue;
            }

            evaluable.Add(rule);
        }

        var maxAccounts = Math.Clamp(request.MaxAccounts ?? DefaultMaxAccounts, 1, HardMaxAccounts);
        var totalAccounts = await _accounts.CountAsync(tenantId, cancellationToken);
        var accounts = evaluable.Count == 0
            ? Array.Empty<TerritoryAccountSnapshot>()
            : await _accounts.ListForPreviewAsync(tenantId, maxAccounts, cancellationToken);

        if (totalAccounts > accounts.Count && evaluable.Count > 0)
        {
            warnings.Add($"Only the first {accounts.Count} of {totalAccounts} accounts were scanned (preview cap).");
        }

        if (allRules.Count == 0)
        {
            warnings.Add("This model has no assignment rules yet.");
        }
        else if (evaluable.Count == 0)
        {
            warnings.Add("No rule was evaluable (all rules are disabled, outside their effective window, or of a type FU03 does not evaluate).");
        }

        if (effectiveAt != now)
        {
            warnings.Add($"Rules were evaluated as of {effectiveAt:yyyy-MM-dd} (the model window), not today.");
        }

        var nodes = (await _nodes.ListByModelAsync(tenantId, request.ModelId, cancellationToken)).ToDictionary(n => n.Id);
        var outcome = TerritoryAssignmentPreviewEngine.Evaluate(evaluable, accounts);

        var matched = new List<TerritoryAssignmentPreviewMatchDto>();
        var conflicts = new List<TerritoryAssignmentPreviewConflictDto>();

        foreach (var result in outcome.Results)
        {
            foreach (var match in result.Matches)
            {
                var isWinner = ReferenceEquals(match, result.Winner);
                var conflictStatus = !result.HasConflict
                    ? TerritoryPreviewConflictStatus.None
                    : isWinner
                        ? TerritoryPreviewConflictStatus.ConflictWinner
                        : TerritoryPreviewConflictStatus.ConflictLoser;

                var node = nodes.GetValueOrDefault(match.Rule.TerritoryId);
                matched.Add(new TerritoryAssignmentPreviewMatchDto(
                    result.Account.AccountId,
                    result.Account.AccountCode,
                    result.Account.AccountName,
                    match.Rule.TerritoryId,
                    node?.TerritoryCode,
                    node?.Name,
                    node?.TerritoryLevel,
                    match.Rule.Id,
                    match.Rule.RuleCode,
                    match.Rule.RuleType,
                    match.Rule.Priority,
                    match.MatchReason,
                    conflictStatus));
            }

            if (!result.HasConflict)
            {
                continue;
            }

            var candidates = result.Matches.Select(m =>
            {
                var node = nodes.GetValueOrDefault(m.Rule.TerritoryId);
                return new TerritoryAssignmentPreviewCandidateDto(
                    m.Rule.TerritoryId, node?.TerritoryCode, node?.Name, m.Rule.Id, m.Rule.RuleCode, m.Rule.Priority,
                    ReferenceEquals(m, result.Winner));
            }).ToList();

            conflicts.Add(new TerritoryAssignmentPreviewConflictDto(
                result.Account.AccountId,
                result.Account.AccountCode,
                result.Account.AccountName,
                candidates,
                result.Matches.Select(m => m.Rule.Id).Distinct().ToList(),
                result.Winner.Rule.ConflictPolicy,
                TerritoryAssignmentPreviewEngine.ResolutionSuggestion(result.Winner.Rule.ConflictPolicy, result.Winner.Rule.RuleCode)));
        }

        var summary = allRules.Select(r => new TerritoryAssignmentPreviewRuleSummaryDto(
            r.Id, r.RuleCode, r.RuleType, r.Priority, r.IsEnabled,
            Evaluated: !skipped.ContainsKey(r.Id),
            SkipReason: skipped.GetValueOrDefault(r.Id),
            CriteriaSummary: TerritoryAssignmentRuleMapper.Summarize(r.Criteria),
            MatchCount: outcome.MatchCountByRule.GetValueOrDefault(r.Id))).ToList();

        var dto = new TerritoryAssignmentPreviewDto(
            ModelId: model.Id,
            ModelStatus: model.Status,
            PreviewRunId: Guid.NewGuid(),
            GeneratedAt: now,
            EffectiveAt: effectiveAt,
            CorrelationId: request.CorrelationId?.Trim(),
            PersistedAssignments: false,
            EvaluatedRuleCount: evaluable.Count,
            SkippedRuleCount: skipped.Count,
            TotalTenantAccounts: totalAccounts,
            ScannedAccounts: accounts.Count,
            TotalCandidateAccounts: outcome.Results.Count,
            UnmatchedAccountsCount: outcome.UnmatchedAccounts,
            ConflictCount: conflicts.Count,
            MatchedAccounts: matched,
            Conflicts: conflicts,
            Warnings: warnings,
            CriteriaSummary: summary);

        return Response<TerritoryAssignmentPreviewDto>.Success(dto);
    }
}
