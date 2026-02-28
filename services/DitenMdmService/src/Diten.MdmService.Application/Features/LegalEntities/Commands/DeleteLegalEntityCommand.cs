using MediatR;

namespace Diten.MdmService.Application.Features.LegalEntities.Commands;

public sealed record DeleteLegalEntityCommand(Guid Id) : IRequest<bool>;
