using Diten.CrmService.Application.Common.Models;
using Diten.CrmService.Application.Features.Approvals;
using Diten.CrmService.Domain.Entities;
using MediatR;

namespace Diten.CrmService.Application.Features.Knowledge.ObjectionResponses;

/// <summary>WP-VW-W3 (W3-BE-b) — the objection library keys (PKS-001).
/// <para>WP-VW-W3 (W3-BE-c) — approval moved to MOD-0023 + the Work Center: <see cref="Submit"/> sends a draft (and
/// withdraws it); the decision itself is the workflow task's (the decision endpoint needs only <see cref="Read"/>, the
/// KP-5a rule). <see cref="Approve"/> is kept for the old direct approve, which now answers 409.</para></summary>
public static class ObjectionResponsePermissions
{
    public const string Read = "crm.objection-response.read";
    public const string Manage = "crm.objection-response.manage";
    public const string Submit = "crm.objection-response.submit";

    /// <summary>The retired direct approve key (W3-BE-b); the endpoint answers 409 <c>approval_via_workflow_only</c>.</summary>
    public const string Approve = "crm.objection-response.approve";

    public static readonly IReadOnlyList<string> All = new[] { Read, Manage, Submit };
}

/// <summary>WP-VW-W3 (W3-BE-b) — the reference set the objection categories come from. The VALUES and their labels are
/// reference data (Platform catalog <c>crm-visit-reference.json</c>, read through the published-values seam); only the set
/// code and the label attribute prefix are code (the <c>visit-outcome-reason</c> pattern).</summary>
public static class VisitObjectionCategories
{
    public const string SetCode = "visit-objection-category";

    /// <summary>Per-language label attribute prefix (<c>label_tr</c> …); English is the value's display name.</summary>
    public const string LabelAttributePrefix = "label_";
}

/// <summary>WP-VW-W3 (W3-BE-c) — what the caller can do with an answer (computed per read).</summary>
public sealed record ObjectionResponseAbilities(
    bool CanEdit, bool CanSubmit, bool CanWithdraw, bool CanDecide, bool CanRetire, bool CanCopy)
{
    public static ObjectionResponseAbilities None { get; } = new(false, false, false, false, false, false);
}

/// <summary>WP-VW-W3 (W3-BE-c) — one place an answer is used: a knowledge path step's presentation notes.</summary>
public sealed record ObjectionResponseUsageDto(
    Guid PathId, string PathName, string PathVersion, string PathStatus, Guid StepId, int StepOrder, string StepTitle);

public sealed record ObjectionResponseDto(
    Guid Id,
    Guid ProductId,
    string CategoryCode,
    string Question,
    string Answer,
    IReadOnlyList<Guid> ClaimIds,
    string Status,
    string? CreatedBy,
    DateTimeOffset CreatedAt,
    string? UpdatedBy,
    DateTimeOffset? UpdatedAt,
    string? ApprovedBy,
    DateTimeOffset? ApprovedAt,
    string? RetiredBy,
    DateTimeOffset? RetiredAt,
    int Version,
    // WP-VW-W3 (W3-BE-c, additive)
    string? ProductName = null,
    string? ProductForm = null,
    int UsageCount = 0,
    IReadOnlyList<ApprovalRoundDto>? ReviewRounds = null,
    IReadOnlyList<ApprovalDecisionDto>? Decisions = null,
    string? SubmittedBy = null,
    DateTimeOffset? SubmittedAt = null,
    Guid? CopiedFromId = null,
    ObjectionResponseAbilities? Abilities = null,
    IReadOnlyDictionary<string, string>? DisplayNames = null)
{
    public static ObjectionResponseDto From(ObjectionResponse e) => new(
        e.Id, e.ProductId, e.CategoryCode, e.Question, e.Answer, e.ClaimIds.ToList(), e.Status,
        e.CreatedBy, e.CreatedAt, e.UpdatedBy, e.UpdatedAt, e.ApprovedBy, e.ApprovedAt, e.RetiredBy, e.RetiredAt, e.Version,
        ReviewRounds: ApprovalRoundDto.From(e),
        Decisions: ApprovalDecisionDto.From(e),
        SubmittedBy: e.OpenRound()?.SubmittedBy,
        SubmittedAt: e.OpenRound()?.SubmittedAt,
        CopiedFromId: e.CopiedFromId);
}

/// <summary>WP-VW-W3 (W3-BE-c) — one page of the library (<c>total</c> = after the filters).</summary>
public sealed record ObjectionResponseListDto(IReadOnlyList<ObjectionResponseDto> Items, int Total, int Page, int PageSize);

/// <summary>A draft may be saved half-written (K-A3): question / answer / category may be empty; the product is required.</summary>
public sealed record CreateObjectionResponseCommand(
    Guid ProductId,
    string? CategoryCode,
    string? Question,
    string? Answer,
    IReadOnlyList<Guid>? ClaimIds) : IRequest<Response<Guid>>;

/// <summary>Only a draft is edited (an answer under review is locked; an approved one never changes — retire + new).</summary>
public sealed record UpdateObjectionResponseCommand(
    Guid Id,
    Guid ProductId,
    string? CategoryCode,
    string? Question,
    string? Answer,
    IReadOnlyList<Guid>? ClaimIds,
    int? ExpectedVersion = null) : IRequest<Response<bool>>;

/// <summary>The W3-BE-b direct approve — retired: 409 <c>approval_via_workflow_only</c>.</summary>
public sealed record ApproveObjectionResponseCommand(Guid Id, int? ExpectedVersion = null) : IRequest<Response<bool>>;

/// <summary>draft / approved → retired (the end of the record; a change is a new record).</summary>
public sealed record RetireObjectionResponseCommand(Guid Id, int? ExpectedVersion = null) : IRequest<Response<bool>>;

/// <summary>WP-VW-W3 (W3-BE-c) — a complete draft → <c>in_review</c> (MOD-0023 round, template CRM-OBJ-RESP).</summary>
public sealed record SubmitObjectionResponseCommand(Guid Id) : IRequest<Response<ObjectionResponseDto>>;

/// <summary>WP-VW-W3 (W3-BE-c) — only the submitter withdraws (the round is cancelled; the answer → draft).</summary>
public sealed record WithdrawObjectionResponseCommand(Guid Id) : IRequest<Response<ObjectionResponseDto>>;

/// <summary>WP-VW-W3 (W3-BE-c) — <c>approve</c> | <c>return</c> (a return needs a comment) on the Work Center task.</summary>
public sealed record DecideObjectionResponseCommand(Guid Id, string? Decision, string? Comment) : IRequest<Response<ObjectionResponseDto>>;

/// <summary>WP-VW-W3 (W3-BE-c) — a NEW draft with the same content (<c>copiedFromId</c> = the source).</summary>
public sealed record CopyObjectionResponseCommand(Guid Id) : IRequest<Response<Guid>>;

public sealed record GetObjectionResponseQuery(Guid Id) : IRequest<Response<ObjectionResponseDto>>;

/// <summary>WP-VW-W3 (W3-BE-c) — <c>q</c> searches the question; <c>page</c> 1-based, <c>pageSize</c> 1–100 (default 20).</summary>
public sealed record ListObjectionResponsesQuery(
    Guid? ProductId = null, string? CategoryCode = null, string? Status = null, string? Q = null, int? Page = null, int? PageSize = null)
    : IRequest<Response<ObjectionResponseListDto>>;

/// <summary>WP-VW-W3 (W3-BE-c) — where an answer is used (path steps' presentation notes).</summary>
public sealed record GetObjectionResponseUsagesQuery(Guid Id) : IRequest<Response<IReadOnlyList<ObjectionResponseUsageDto>>>;
