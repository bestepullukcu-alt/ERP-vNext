using Diten.Platform.Application.Common;
using MediatR;

namespace Diten.Platform.Application.Features.PersonReferenceDirectory.Commands;

public sealed record RecordPersonReferenceDirectoryHealthCommand(PersonReferenceDirectoryHealthRequest Request) : IRequest<Response<Guid>>;
