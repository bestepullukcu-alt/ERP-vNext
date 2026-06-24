using Diten.Shared.Core;
using MediatR;

namespace Diten.MdmService.Application.Features.LegalEntity.Commands;

public sealed record CreateLegalEntityCommand(LegalEntityWriteRequest Request) : IRequest<Response<Guid>>;
