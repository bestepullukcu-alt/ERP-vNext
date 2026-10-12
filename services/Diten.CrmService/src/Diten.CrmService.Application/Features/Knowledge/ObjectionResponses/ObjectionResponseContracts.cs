using Diten.CrmService.Application.Common.Models;
using Diten.CrmService.Domain.Entities;
using MediatR;

namespace Diten.CrmService.Application.Features.Knowledge.ObjectionResponses;

/// <summary>WP-VW-W3 (W3-BE-b) — the objection library keys (PKS-001). <see cref="Approve"/> is separate from
/// <see cref="Manage"/> (author-vs-approver SoD).</summary>
public static class ObjectionResponsePermissions
{
    public const string Read = "crm.objection-response.read";
    public const string Manage = "crm.objection-response.manage";
    public const string Approve = "crm.objection-response.approve";

    public static readonly IReadOnlyList<string> All = new[] { Read, Manage, Approve };
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
    int Version)
{
    public static ObjectionResponseDto From(ObjectionResponse e) => new(
        e.Id, e.ProductId, e.CategoryCode, e.Question, e.Answer, e.ClaimIds.ToList(), e.Status,
        e.CreatedBy, e.CreatedAt, e.UpdatedBy, e.UpdatedAt, e.ApprovedBy, e.ApprovedAt, e.RetiredBy, e.RetiredAt, e.Version);
}

public sealed record CreateObjectionResponseCommand(
    Guid ProductId,
    string? CategoryCode,
    string? Question,
    string? Answer,
    IReadOnlyList<Guid>? ClaimIds) : IRequest<Response<Guid>>;

/// <summary>Only a draft is edited (an approved record never changes — retire + a new record).</summary>
public sealed record UpdateObjectionResponseCommand(
    Guid Id,
    Guid ProductId,
    string? CategoryCode,
    string? Question,
    string? Answer,
    IReadOnlyList<Guid>? ClaimIds,
    int? ExpectedVersion = null) : IRequest<Response<bool>>;

/// <summary>draft → approved, by someone who is not its author (SoD); its claims must still be approved.</summary>
public sealed record ApproveObjectionResponseCommand(Guid Id, int? ExpectedVersion = null) : IRequest<Response<bool>>;

/// <summary>draft / approved → retired (the end of the record; a change is a new record).</summary>
public sealed record RetireObjectionResponseCommand(Guid Id, int? ExpectedVersion = null) : IRequest<Response<bool>>;

public sealed record GetObjectionResponseQuery(Guid Id) : IRequest<Response<ObjectionResponseDto>>;

public sealed record ListObjectionResponsesQuery(Guid? ProductId = null, string? CategoryCode = null, string? Status = null)
    : IRequest<Response<IReadOnlyList<ObjectionResponseDto>>>;
