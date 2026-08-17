using Diten.Shared.Core;
using MediatR;

namespace Diten.MdmService.Application.Features.LegalEntity.Commands;

public sealed record ActivateLegalEntityCommand(Guid LegalEntityId) : IRequest<Response<NoContent>>;
