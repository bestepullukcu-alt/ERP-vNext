using Diten.CrmService.Application.Common.Models;
using Diten.CrmService.Domain.Entities;
using MediatR;

namespace Diten.CrmService.Application.Features.Knowledge.Path.Review;

// WP-KP-2 — the path review surface. TenantId / actor are server-resolved, never in a payload.

/// <summary>Freezes the path into a new revision and starts its KP-MLR-{country} round with the caller's token.</summary>
public sealed record SubmitKnowledgePathReviewCommand(Guid PathId) : IRequest<Response<KnowledgePathRevisionDto>>;

/// <summary>Cancels the open round (Platform cancel); the path goes back to draft.</summary>
public sealed record WithdrawKnowledgePathReviewCommand(Guid PathId) : IRequest<Response<KnowledgePathRevisionDto>>;

/// <summary>A reviewer's decision on the SAME MOD-0023 task the Work Center shows (one channel, K1). A rejection needs a
/// comment; the submitter can never decide (person-based SoD).</summary>
public sealed record DecideKnowledgePathRevisionCommand(Guid PathId, Guid RevisionId, string? Decision, string? Comment)
    : IRequest<Response<KnowledgePathDecisionDto>>;

public sealed record AddKnowledgePathRevisionNoteCommand(
    Guid PathId, Guid RevisionId, string? PageRef, string? BlockRef, string? StepRef, double? X, double? Y, string? Text)
    : IRequest<Response<KnowledgePathRevisionNoteDto>>;

/// <summary>Resolves a note — its author, or a path manager (<paramref name="CanManage"/>, resolved by the API from the
/// caller's permissions, never from a payload).</summary>
public sealed record ResolveKnowledgePathRevisionNoteCommand(Guid PathId, Guid RevisionId, Guid NoteId, bool CanManage)
    : IRequest<Response<KnowledgePathRevisionNoteDto>>;

public sealed record ListKnowledgePathRevisionsQuery(Guid PathId) : IRequest<Response<IReadOnlyList<KnowledgePathRevisionSummaryDto>>>;

public sealed record GetKnowledgePathRevisionQuery(Guid PathId, Guid RevisionId) : IRequest<Response<KnowledgePathRevisionDto>>;

public sealed record GetKnowledgePathReviewHistoryQuery(Guid PathId)
    : IRequest<Response<IReadOnlyList<KnowledgePathReviewHistoryDto>>>;

public static class KnowledgePathDecisions
{
    public const string Approve = "approve";
    public const string Reject = "reject";
}

public sealed record KnowledgePathReviewRoundDto(
    Guid WorkflowInstanceId,
    string? TemplateCode,
    DateTimeOffset SubmittedAt,
    string? SubmittedBy,
    string? Outcome,
    DateTimeOffset? ClosedAt,
    string? CompletedBy,
    string? ReasonCode,
    bool IsOpen);

public sealed record KnowledgePathRevisionNoteDto(
    Guid NoteId,
    string? PageRef,
    string? BlockRef,
    string? StepRef,
    double? X,
    double? Y,
    string Text,
    string? Author,
    DateTimeOffset CreatedAt,
    DateTimeOffset? ResolvedAt,
    string? ResolvedBy,
    int? CarriedFromRevision,
    bool IsResolved);

public sealed record KnowledgePathChangeDto(string Kind, string Ref, string? Label, string? From, string? To);

public sealed record KnowledgePathChangeSummaryDto(int? ComparedToRevision, IReadOnlyList<KnowledgePathChangeDto> Items);

public sealed record KnowledgePathRevisionSummaryDto(
    Guid RevisionId,
    Guid PathId,
    string PathCode,
    string PathVersion,
    int RevisionNumber,
    string Status,
    string? SubmittedBy,
    KnowledgePathReviewRoundDto Round,
    int OpenNoteCount,
    int ChangeCount);

/// <summary>A revision with its frozen snapshot (as written), round, notes and change summary.</summary>
public sealed record KnowledgePathRevisionDto(
    Guid RevisionId,
    Guid PathId,
    string PathCode,
    string PathVersion,
    int RevisionNumber,
    string Status,
    string? SubmittedBy,
    DateTimeOffset CreatedAt,
    KnowledgePathReviewRoundDto Round,
    KnowledgePathRevisionSnapshot Snapshot,
    IReadOnlyList<KnowledgePathRevisionNoteDto> Notes,
    KnowledgePathChangeSummaryDto ChangeSummary,
    int Version);

public sealed record KnowledgePathDecisionDto(Guid RevisionId, Guid TaskId, string Decision);

public sealed record KnowledgePathReviewHistoryEntryDto(
    string Action, string? StepCode, string? StepName, string? Comment, string? ActorId, string? ActorDisplay,
    string? ReasonCode, DateTimeOffset OccurredAt);

/// <summary>A revision's MOD-0023 history: step NAME + comment + person + time. <c>Available</c> false when MOD-0023
/// could not be read (the entries are then empty — never invented).</summary>
public sealed record KnowledgePathReviewHistoryDto(
    Guid RevisionId, int RevisionNumber, Guid WorkflowInstanceId, bool Available,
    IReadOnlyList<KnowledgePathReviewHistoryEntryDto> Entries);

public static class KnowledgePathReviewMapper
{
    public static KnowledgePathReviewRoundDto ToDto(ClaimReviewRound r) => new(
        r.WorkflowInstanceId, r.TemplateCode, r.SubmittedAt, r.SubmittedBy, r.Outcome, r.ClosedAt, r.CompletedBy,
        r.ReasonCode, r.IsOpen());

    public static KnowledgePathRevisionNoteDto ToDto(KnowledgePathRevisionNote n) => new(
        n.NoteId, n.PageRef, n.BlockRef, n.StepRef, n.X, n.Y, n.Text, n.Author, n.CreatedAt, n.ResolvedAt, n.ResolvedBy,
        n.CarriedFromRevision, n.IsResolved());

    public static KnowledgePathRevisionSummaryDto ToSummary(KnowledgePathRevision r) => new(
        r.Id, r.PathId, r.PathCode, r.PathVersion, r.RevisionNumber, r.Status, r.CreatedBy, ToDto(r.ReviewRound),
        r.Notes.Count(n => !n.IsResolved()), r.ChangeSummary.Items.Count);

    public static KnowledgePathRevisionDto ToDto(KnowledgePathRevision r) => new(
        r.Id, r.PathId, r.PathCode, r.PathVersion, r.RevisionNumber, r.Status, r.CreatedBy, r.CreatedAt,
        ToDto(r.ReviewRound), r.Snapshot, r.Notes.Select(ToDto).ToList(),
        new KnowledgePathChangeSummaryDto(r.ChangeSummary.ComparedToRevision,
            r.ChangeSummary.Items.Select(i => new KnowledgePathChangeDto(i.Kind, i.Ref, i.Label, i.From, i.To)).ToList()),
        r.Version);
}
