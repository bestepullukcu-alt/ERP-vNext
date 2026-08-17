using Diten.Platform.Application.Common;
using MediatR;

namespace Diten.Platform.Application.Features.PersonReferenceDirectory.Commands;

public sealed record UpsertPersonReferenceExternalCorrelationCommand(Guid ProjectionId, Guid? CorrelationId, PersonReferenceExternalCorrelationRequest Request) : IRequest<Response<Guid>>;
