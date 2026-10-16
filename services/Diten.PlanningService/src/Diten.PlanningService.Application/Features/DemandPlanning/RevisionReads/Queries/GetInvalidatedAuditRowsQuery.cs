using MediatR;

namespace Diten.PlanningService.Application.Features.DemandPlanning;

public sealed record GetInvalidatedAuditRowsQuery(Guid TenantId, Guid LegalEntityId,
    Guid RevisionId, Guid ActorId, int PageSize, string? Cursor)
    : IRequest<Response<InvalidatedAuditRowPageView>>;
