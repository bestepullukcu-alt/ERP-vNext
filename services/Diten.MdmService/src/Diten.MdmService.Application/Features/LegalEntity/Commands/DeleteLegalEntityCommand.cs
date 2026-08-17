using Diten.Shared.Core;
using MediatR;

namespace Diten.MdmService.Application.Features.LegalEntity.Commands;

public sealed record DeleteLegalEntityCommand(Guid LegalEntityId) : IRequest<Response<NoContent>>;
