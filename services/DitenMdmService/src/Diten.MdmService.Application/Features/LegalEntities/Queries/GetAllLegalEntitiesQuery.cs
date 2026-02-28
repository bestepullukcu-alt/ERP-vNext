using Diten.MdmService.Domain.Entities;
using MediatR;

namespace Diten.MdmService.Application.Features.LegalEntities.Queries;

public sealed record GetAllLegalEntitiesQuery() : IRequest<IEnumerable<LegalEntity>>;
