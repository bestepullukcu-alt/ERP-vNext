using Diten.Platform.Application.Common;
using MediatR;

namespace Diten.Platform.Application.Features.TimeEntry.Queries;

/// <summary>MOD-0280-FU01 D11 — submitted weeks routed to the caller. Never drafts, never another approver's weeks.</summary>
/// <para>T2b — also the Golden Reference server-mode list protocol (the approvals page's <c>createList</c>): <see cref="Start"/>
/// / <see cref="Length"/> (take precedence over page/pageSize when given), a free-text <see cref="Search"/> over the
/// person's name and the week, and a whitelisted <see cref="OrderBy"/> (anything else is refused, never guessed).</para>
public sealed record GetApprovalListQuery(
    int Page,
    int PageSize,
    string CorrelationId,
    int? Start = null,
    int? Length = null,
    string? Search = null,
    string? OrderBy = null,
    string? OrderDir = null) : IRequest<Response<ApprovalWeekListDto>>;
