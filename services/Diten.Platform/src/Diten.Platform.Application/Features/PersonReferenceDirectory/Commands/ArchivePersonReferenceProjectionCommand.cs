using Diten.Platform.Application.Common;
using MediatR;

namespace Diten.Platform.Application.Features.PersonReferenceDirectory.Commands;

public sealed record ArchivePersonReferenceProjectionCommand(Guid Id) : IRequest<Response<NoContent>>;
