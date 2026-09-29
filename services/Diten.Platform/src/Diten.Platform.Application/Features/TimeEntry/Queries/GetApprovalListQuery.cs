using Diten.Platform.Application.Common;
using MediatR;

namespace Diten.Platform.Application.Features.TimeEntry.Queries;

/// <summary>MOD-0280-FU01 D11 — submitted weeks routed to the caller. Never drafts, never another approver's weeks.</summary>
public sealed record GetApprovalListQuery(int Page, int PageSize, string CorrelationId) : IRequest<Response<ApprovalWeekListDto>>;
