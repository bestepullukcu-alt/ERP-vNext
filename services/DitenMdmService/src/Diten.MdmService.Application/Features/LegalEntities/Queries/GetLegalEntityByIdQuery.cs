using Diten.MdmService.Domain.Entities;
using MediatR;

namespace Diten.MdmService.Application.Features.LegalEntities.Queries;

public sealed record GetLegalEntityByIdQuery(Guid Id) : IRequest<LegalEntity?>;
