namespace Diten.MdmService.Domain.Entities;

// WP-VW-W4 (W4-BE-b2, §2.6, K-W6) — the marketing / medical team's DECISION on a rep proposal, approved through ONE
// MOD-0023 round at a time (template MDM-COMP-PROPOSAL, approver POS-SALES-OPS). The decision is prepared as a draft, sent
// for approval and, once approved, APPLIED to the proposal: complete → active; merge → merged into the target (the name
// becomes the target's alias, the chosen country rows move); reject → rejected. The preparer / last editor / submitter
// never approves (SoD — the b1 core). One open decision per proposal; an applied decision is kept as the trace.
public sealed class CompetitorProposalDecision : EntityBase, IWorkflowApprovable
{
    public Guid ProposalId { get; set; }

    /// <summary><see cref="CompetitorDecisionActions"/>.</summary>
    public string Action { get; set; } = CompetitorDecisionActions.Complete;

    /// <summary>complete — the fields the reviewer filled in (the record the proposal becomes).</summary>
    public CompetitorProductFields? Complete { get; set; }

    /// <summary>merge — the surviving record and the markets whose rows move to it.</summary>
    public Guid? TargetId { get; set; }
    public List<string> MoveMarketCodes { get; set; } = [];

    /// <summary>reject — why (≤ 500); old reports keep the proposal as plain text.</summary>
    public string? Reason { get; set; }

    public string Status { get; set; } = CompetitorDecisionStatuses.Draft;
    public string? CreatedBy { get; set; }
    public string? UpdatedBy { get; set; }
    public string? AppliedBy { get; set; }
    public DateTimeOffset? AppliedAt { get; set; }

    public List<WorkflowApprovalRound> ReviewRounds { get; set; } = [];
    public List<WorkflowApprovalDecision> Decisions { get; set; } = [];
}

public static class CompetitorDecisionActions
{
    public const string Complete = "complete";
    public const string Merge = "merge";
    public const string Reject = "reject";

    public static bool IsKnown(string? value) => value is Complete or Merge or Reject;
}

/// <summary><c>draft</c> → <c>in_review</c> → <c>applied</c>; a returned / withdrawn / timed-out / refused round →
/// <c>returned</c> (editable again, like a draft).</summary>
public static class CompetitorDecisionStatuses
{
    public const string Draft = "draft";
    public const string InReview = "in_review";
    public const string Applied = "applied";
    public const string Returned = "returned";

    public static bool IsEditable(string? status) => status is Draft or Returned;
}

/// <summary>The editable fields of a competitor record (create / update / the "complete" decision).</summary>
public sealed class CompetitorProductFields
{
    public string Name { get; set; } = string.Empty;
    public List<string> Aliases { get; set; } = [];
    public string Company { get; set; } = string.Empty;
    public List<string> ActiveIngredients { get; set; } = [];
    public string? FormCode { get; set; }
    public string? Strength { get; set; }
    public string? AtcCode { get; set; }
    public List<Guid> CompetesWithGlobalProductIds { get; set; } = [];
    public List<Guid> IndicationNodeIds { get; set; } = [];
    public List<CompetitorCountryPrice> CountryPrices { get; set; } = [];
}
