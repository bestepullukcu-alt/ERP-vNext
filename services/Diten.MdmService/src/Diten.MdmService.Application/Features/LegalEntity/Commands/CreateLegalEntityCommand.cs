using Diten.Shared.Core;
using MediatR;

namespace Diten.MdmService.Application.Features.LegalEntity.Commands;

public sealed record CreateLegalEntityCommand(
    string Code,
    string LegalName,
    string? DisplayName) : IRequest<Response<Guid>>;
