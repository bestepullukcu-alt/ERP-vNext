using Diten.Platform.Application.Common;
using MediatR;

namespace Diten.Platform.Application.Features.BusinessReferenceData.Commands;

public sealed record EnsureVerifiedGskuTenantAssignmentsCommand(
    Guid ConsumerTenantId,
    string ActorId,
    string IdempotencyNamespace)
    : IRequest<Response<NoContent>>, IBusinessReferenceDataRequest;
