using Diten.Platform.Application.Common;
using MediatR;

namespace Diten.Platform.Application.Features.PersonReferenceDirectory.Commands;

public sealed record UpdatePersonReferenceProjectionCommand(Guid Id, PersonReferenceProjectionUpdateRequest Request) : IRequest<Response<NoContent>>;
