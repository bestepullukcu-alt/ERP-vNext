using Diten.MdmService.Application.Features.Approvals;
using Diten.MdmService.Domain.Entities;
using Diten.MdmService.Domain.Repositories;

namespace Diten.MdmService.Application.Features.CompetitorProducts;

/// <summary>WP-VW-W4 (W4-BE-b2) — the permission keys (PKS-001; descriptions in the Auth catalog, English only).</summary>
public static class CompetitorPermissions
{
    public const string Read = "mdm.competitor-products.read";
    public const string Manage = "mdm.competitor-products.manage";
    public const string Propose = "mdm.competitor-products.propose";

    public static readonly IReadOnlyList<string> All = [Read, Manage, Propose];
}

/// <summary>
/// WP-VW-W4 (W4-BE-b2, K-W6) — a merge: the source joins the target and is never deleted. The source name (and its other
/// names) become the target's other names, so a rep typing the old name finds the survivor; the chosen markets the target
/// does not have yet move to it; the target keeps a trace; the source becomes <c>merged</c> → <c>MergedIntoId</c>.
/// Idempotent: a second run on the same pair changes nothing.
/// </summary>
public static class CompetitorMerge
{
    /// <summary>Returns whether the TARGET changed (a retry after a half-written merge finds the trace and leaves it).</summary>
    public static bool Apply(CompetitorProduct source, CompetitorProduct target, IEnumerable<string>? moveMarketCodes,
        string? actor, DateTime now, string kind)
    {
        var targetChanged = false;
        if (!target.MergedFrom.Any(t => t.SourceId == source.Id))
        {
            MergeIntoTarget(source, target, moveMarketCodes, actor, now, kind);
            targetChanged = true;
        }

        if (source.MergedIntoId != target.Id)
        {
            source.ChangeLog.Add(new CompetitorChange { At = now, By = actor, Field = "status", OldValue = source.Status, NewValue = CompetitorProductStatuses.Merged });
            source.Status = CompetitorProductStatuses.Merged;
            source.MergedIntoId = target.Id;
            source.MergedAt = now;
            source.MergedBy = actor;
            source.UpdatedBy = actor;
        }

        return targetChanged;
    }

    private static void MergeIntoTarget(CompetitorProduct source, CompetitorProduct target, IEnumerable<string>? moveMarketCodes,
        string? actor, DateTime now, string kind)
    {

        // The names: the source's name and other names join the target's other names (never the target's own name twice).
        foreach (var name in source.Aliases.Prepend(source.Name))
        {
            AddAlias(target, name, actor, now);
        }

        // The markets: a chosen row moves only when the target has no row for that market (the target's row wins).
        var moved = new List<string>();
        foreach (var code in (moveMarketCodes ?? []).Select(c => c.Trim().ToUpperInvariant()).Distinct())
        {
            var row = source.CountryPrices.FirstOrDefault(r => r.MarketCode == code);
            if (row is null || target.CountryPrices.Any(r => r.MarketCode == code))
            {
                continue;
            }

            target.CountryPrices.Add(new CompetitorCountryPrice
            {
                MarketCode = row.MarketCode, BoxPrice = row.BoxPrice, CurrencyCode = row.CurrencyCode,
                ReimbursementStatusCode = row.ReimbursementStatusCode, ValidFrom = row.ValidFrom, Source = row.Source
            });
            moved.Add(code);
            target.ChangeLog.Add(new CompetitorChange { At = now, By = actor, Field = $"countryPrices.{code}", OldValue = null, NewValue = $"moved from {source.Name}" });
        }

        target.MergedFrom.Add(new CompetitorMergeTrace
        {
            SourceId = source.Id, SourceName = source.Name, SourceCompany = source.Company, ProposedBy = source.ProposedBy,
            ProposedAt = source.ProposedAt, MergedAt = now, MergedBy = actor, Kind = kind, MovedMarketCodes = moved
        });
        target.UpdatedBy = actor;
    }

    private static void AddAlias(CompetitorProduct target, string? name, string? actor, DateTime now)
    {
        var folded = CompetitorNames.Fold(name);
        if (folded.Length == 0 || CompetitorNames.Fold(target.Name) == folded
            || target.Aliases.Any(a => CompetitorNames.Fold(a) == folded) || target.Aliases.Count >= CompetitorLimits.MaxAliases)
        {
            return;
        }

        target.Aliases.Add(name!.Trim());
        target.ChangeLog.Add(new CompetitorChange { At = now, By = actor, Field = "aliases", OldValue = null, NewValue = name.Trim() });
    }
}

/// <summary>
/// WP-VW-W4 (W4-BE-b2) — the competitor proposal decision as an approvable kind of the b1 core: SoD
/// (<c>competitor_proposal_sod</c>), then the decision is APPLIED to the proposal (complete / merge / reject) before the
/// decision itself is marked <c>applied</c>. A decision that can no longer be applied (the proposal was already decided,
/// the merge target is not active any more, the completed name now exists) goes back to <c>returned</c> with that reason.
/// </summary>
public static class CompetitorProposalApproval
{
    public static WorkflowApprovalTarget<CompetitorProposalDecision> Target(
        IWorkflowApprovalStore<CompetitorProposalDecision> store, ICompetitorProductRepository products)
        => new(WorkflowApprovalKind.CompetitorProposal, store, CompetitorDecisionStatuses.Returned, CompetitorErrors.Sod,
            approve: (d, by, at) =>
            {
                d.Status = CompetitorDecisionStatuses.Applied;
                d.AppliedBy = by;
                d.AppliedAt = at;
            },
            applyEffectsAsync: (d, by, at, ct) => ApplyAsync(products, d, by, at.UtcDateTime, ct));

    public static async Task<string?> ApplyAsync(ICompetitorProductRepository products, CompetitorProposalDecision decision,
        string approver, DateTime now, CancellationToken ct)
    {
        for (var attempt = 0; attempt < 2; attempt++)
        {
            var proposal = await products.GetAsync(decision.TenantId, decision.ProposalId, ct);
            if (proposal is null)
            {
                return CompetitorErrors.NotFound;
            }

            if (AlreadyApplied(decision, proposal))
            {
                return null; // a retried apply: the proposal already carries this decision
            }

            if (proposal.Status != CompetitorProductStatuses.Proposed)
            {
                return CompetitorErrors.NotProposal;
            }

            var expected = proposal.Version;
            switch (decision.Action)
            {
                case CompetitorDecisionActions.Complete:
                {
                    if (decision.Complete is not { } fields)
                    {
                        return CompetitorErrors.Invalid;
                    }

                    var key = CompetitorNames.Key(fields.Company, fields.Name);
                    var all = await products.ListAsync(decision.TenantId, ct);
                    if (all.Any(r => r.Id != proposal.Id && r.NormalizedKey == key && CompetitorProductService.CountsForUniqueness(r)))
                    {
                        return CompetitorErrors.Duplicate;
                    }

                    CompetitorProductService.ApplyFields(proposal, fields, approver, now, history: false);
                    proposal.ChangeLog.Add(new CompetitorChange { At = now, By = approver, Field = "status", OldValue = CompetitorProductStatuses.Proposed, NewValue = CompetitorProductStatuses.Active });
                    proposal.Status = CompetitorProductStatuses.Active;
                    if (await products.ReplaceAsync(proposal, expected, ct)) return null;
                    break;
                }

                case CompetitorDecisionActions.Merge:
                {
                    var target = decision.TargetId is { } targetId ? await products.GetAsync(decision.TenantId, targetId, ct) : null;
                    if (target is null || !target.IsSelectable || target.Id == proposal.Id)
                    {
                        return CompetitorErrors.NotSelectable;
                    }

                    var targetVersion = target.Version;
                    if (CompetitorMerge.Apply(proposal, target, decision.MoveMarketCodes, approver, now, "proposal")
                        && !await products.ReplaceAsync(target, targetVersion, ct)) break;
                    if (await products.ReplaceAsync(proposal, expected, ct)) return null;
                    break;
                }

                case CompetitorDecisionActions.Reject:
                    proposal.ChangeLog.Add(new CompetitorChange { At = now, By = approver, Field = "status", OldValue = CompetitorProductStatuses.Proposed, NewValue = CompetitorProductStatuses.Rejected });
                    proposal.Status = CompetitorProductStatuses.Rejected;
                    proposal.RejectedReason = decision.Reason;
                    proposal.UpdatedBy = approver;
                    if (await products.ReplaceAsync(proposal, expected, ct)) return null;
                    break;

                default:
                    return CompetitorErrors.Invalid;
            }
        }

        return CompetitorErrors.ConcurrencyConflict;
    }

    private static bool AlreadyApplied(CompetitorProposalDecision d, CompetitorProduct p) => d.Action switch
    {
        CompetitorDecisionActions.Complete => p.Status == CompetitorProductStatuses.Active && d.Complete is { } f
                                              && p.NormalizedKey == CompetitorNames.Key(f.Company, f.Name),
        CompetitorDecisionActions.Merge => p.Status == CompetitorProductStatuses.Merged && p.MergedIntoId == d.TargetId,
        CompetitorDecisionActions.Reject => p.Status == CompetitorProductStatuses.Rejected,
        _ => false
    };
}
