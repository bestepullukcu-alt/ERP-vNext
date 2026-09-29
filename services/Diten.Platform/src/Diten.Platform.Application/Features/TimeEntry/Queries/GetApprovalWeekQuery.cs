using Diten.Platform.Application.Common;
using MediatR;

namespace Diten.Platform.Application.Features.TimeEntry.Queries;

/// <summary>MOD-0280-FU01 D6/D11 — one submitted week, read-only, for an approver it is routed to. Anything else is 404.</summary>
public sealed record GetApprovalWeekQuery(Guid WeekId, string CorrelationId) : IRequest<Response<ApprovalWeekDto>>;
