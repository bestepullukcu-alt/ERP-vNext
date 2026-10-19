namespace Diten.CrmService.Domain.Entities;

/// <summary>
/// WP-VW-W3 (W3-BE-c) — a record approved through ONE MOD-0023 workflow round at a time (the objection answer, the
/// time-boxed evidence exception). The KP-5a regulatory pattern: the rounds CRM started (<see cref="ClaimReviewRound"/>:
/// the instance id CRM stored when MOD-0023 accepted the start, the outcome when it closed) and the decisions taken on
/// them (<see cref="RegulatoryTextDecision"/>: who, when, <c>approve</c> | <c>return</c>, the comment).
/// </summary>
public interface IWorkflowApprovable
{
    Guid Id { get; }
    Guid TenantId { get; }
    int Version { get; }
    string Status { get; set; }
    string? CreatedBy { get; }
    string? UpdatedBy { get; }
    List<ClaimReviewRound> ReviewRounds { get; }
    List<RegulatoryTextDecision> Decisions { get; }
}

public static class WorkflowApprovableExtensions
{
    /// <summary>The round MOD-0023 still runs (null when none).</summary>
    public static ClaimReviewRound? OpenRound(this IWorkflowApprovable record) => record.ReviewRounds.LastOrDefault(r => r.IsOpen());

    /// <summary>The latest round (open or closed).</summary>
    public static ClaimReviewRound? CurrentRound(this IWorkflowApprovable record)
        => record.ReviewRounds.OrderBy(r => r.RoundNo).LastOrDefault();

    public static int NextRoundNo(this IWorkflowApprovable record)
        => record.ReviewRounds.Select(r => r.RoundNo).DefaultIfEmpty(0).Max() + 1;
}

/// <summary>The two decisions a reviewer takes (a cancelled / timed-out round leaves no decision).</summary>
public static class WorkflowApprovalDecisions
{
    public const string Approve = "approve";
    public const string Return = "return";
}
