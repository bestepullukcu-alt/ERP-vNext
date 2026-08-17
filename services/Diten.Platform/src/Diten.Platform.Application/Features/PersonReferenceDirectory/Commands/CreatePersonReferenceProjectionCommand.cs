using Diten.Platform.Application.Common;
using MediatR;

namespace Diten.Platform.Application.Features.PersonReferenceDirectory.Commands;

public sealed record CreatePersonReferenceProjectionCommand(PersonReferenceProjectionCreateRequest Request) : IRequest<Response<Guid>>;
