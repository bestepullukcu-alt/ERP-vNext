using Diten.Platform.Application.Common;
using MediatR;

namespace Diten.Platform.Application.Features.DocumentManagementCorporateCollectionInstances.Commands;

public sealed record ProvisionCorporateCollectionInstanceCommand(
    Guid BaselineReleaseId,
    Guid CorporateOwnerId,
    string IdempotencyKey,
    string? DisplayName,
    string? Description,
    string CorrelationId) : IRequest<Response<CorporateCollectionProvisioningResult>>;
