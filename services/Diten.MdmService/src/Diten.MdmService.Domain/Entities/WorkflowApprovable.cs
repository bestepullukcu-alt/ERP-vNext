namespace Diten.MdmService.Domain.Entities;

// WP-VW-W4 (W4-BE-b1) — copied from the CrmService (Domain/Entities/WorkflowApprovable.cs, ClaimReviewRound,
// RegulatoryTextDecision, ClaimReviewOutcomes), NOT shared: the MDM keeps its own copy so neither service depends on the
// other's domain. The shapes (round / decision / outcome words) are kept identical so the MOD-0023 contract reads the same.

/// <summary>
/// A record approved through ONE MOD-0023 workflow round at a time (W4-BE-b2: the competitor proposal decision). The rounds
/// MDM started (<see cref="WorkflowApprovalRound"/>: the instance id MDM stored when MOD-0023 accepted the start, the outcome
/// when it closed) and the decisions taken on them (<see cref="WorkflowApprovalDecision"/>: who, when, approve | return, the
/// comment).
/// </summary>
public interface IWorkflowApprovable
{
    Guid Id { get; }
    Guid TenantId { get; }
    int Version { get; }
    string Status { get; set; }
    string? CreatedBy { get; }
    string? UpdatedBy { get; }
    DateTimeOffset? UpdatedAt { get; set; }
    List<WorkflowApprovalRound> ReviewRounds { get; }
    List<WorkflowApprovalDecision> Decisions { get; }
}

/// <summary>One approval round (one MOD-0023 workflow instance).</summary>
public sealed class WorkflowApprovalRound
{
    public Guid WorkflowInstanceId { get; set; }
    public DateTimeOffset SubmittedAt { get; set; }
    public string? SubmittedBy { get; set; }

    /// <summary><see cref="WorkflowApprovalOutcomes"/>; null while the round is open.</summary>
    public string? Outcome { get; set; }
    public DateTimeOffset? ClosedAt { get; set; }

    /// <summary>1-based (also part of the workflow idempotency key <c>{type}:{id}:r{round}</c>).</summary>
    public int RoundNo { get; set; }
    public string? TemplateCode { get; set; }

    /// <summary>Who closed it (principal id from the workflow; null for a system timeout).</summary>
    public string? CompletedBy { get; set; }
    public string? ReasonCode { get; set; }

    public bool IsOpen() => Outcome is null && ClosedAt is null;
}

/// <summary>One decision on a round (the MDM decision endpoint, or the MOD-0023 outcome when it was taken in the Work
/// Center — then without a comment).</summary>
public sealed class WorkflowApprovalDecision
{
    public int RoundNo { get; set; }
    public string? By { get; set; }

    /// <summary><see cref="WorkflowApprovalDecisions"/> (a cancelled / timed-out round leaves no decision).</summary>
    public string Outcome { get; set; } = string.Empty;

    public string? Comment { get; set; }
    public DateTimeOffset At { get; set; }
}

/// <summary>How a round closed (the MOD-0023 completion outcomes).</summary>
public static class WorkflowApprovalOutcomes
{
    public const string Approved = "approved";
    public const string Rejected = "rejected";
    public const string Cancelled = "cancelled";
    public const string TimedOut = "timed-out";

    public static bool IsValid(string? value) => value is Approved or Rejected or Cancelled or TimedOut;
}

/// <summary>The two decisions a reviewer takes.</summary>
public static class WorkflowApprovalDecisions
{
    public const string Approve = "approve";
    public const string Return = "return";
}

public static class WorkflowApprovableExtensions
{
    /// <summary>The round MOD-0023 still runs (null when none).</summary>
    public static WorkflowApprovalRound? OpenRound(this IWorkflowApprovable record) => record.ReviewRounds.LastOrDefault(r => r.IsOpen());

    /// <summary>The latest round (open or closed).</summary>
    public static WorkflowApprovalRound? CurrentRound(this IWorkflowApprovable record)
        => record.ReviewRounds.OrderBy(r => r.RoundNo).LastOrDefault();

    public static int NextRoundNo(this IWorkflowApprovable record)
        => record.ReviewRounds.Select(r => r.RoundNo).DefaultIfEmpty(0).Max() + 1;
}
