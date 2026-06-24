using Diten.Shared.Core;
using MediatR;

namespace Diten.MdmService.Application.Features.LegalEntity.Queries;

// MOD-0288 lookup feed: read-only legal-entity list (id + code + name) consumed by the platform
// Organization Units screen so an OrgUnit can pick its owning Legal Entity.
public sealed record GetLegalEntitiesQuery() : IRequest<Response<IReadOnlyList<LegalEntityDetailDto>>>;
