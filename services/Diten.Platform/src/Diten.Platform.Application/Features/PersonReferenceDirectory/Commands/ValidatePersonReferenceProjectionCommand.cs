using Diten.Platform.Application.Common;
using MediatR;

namespace Diten.Platform.Application.Features.PersonReferenceDirectory.Commands;

public sealed record ValidatePersonReferenceProjectionCommand(Guid Id) : IRequest<Response<PersonReferenceProjectionDto>>;
