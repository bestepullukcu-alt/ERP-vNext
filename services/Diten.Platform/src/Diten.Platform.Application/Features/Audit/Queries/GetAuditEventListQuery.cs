using Diten.Platform.Application.Common;
using Diten.Platform.Application.Features.Tenants;
using MediatR;

namespace Diten.Platform.Application.Features.Audit.Queries;

public sealed record GetAuditEventListQuery(AuditEventFilterRequest Filter)
    : IRequest<Response<PagedResult<AuditEventListItemDto>>>;
