using Diten.Platform.Application.Common;
using Diten.Platform.Application.Features.DocumentManagementCorporateCollectionInstances.Commands;
using MediatR;

namespace Diten.Platform.Application.Features.DocumentManagementCorporateCollectionInstances.Handlers.CommandHandlers;

public sealed class ProvisionCorporateCollectionInstanceHandler
    : IRequestHandler<ProvisionCorporateCollectionInstanceCommand, Response<CorporateCollectionProvisioningResult>>
{
    private readonly CorporateCollectionInstanceProvisioningService _service;

    public ProvisionCorporateCollectionInstanceHandler(CorporateCollectionInstanceProvisioningService service) =>
        _service = service;

    public Task<Response<CorporateCollectionProvisioningResult>> Handle(
        ProvisionCorporateCollectionInstanceCommand request,
        CancellationToken ct) =>
        _service.ProvisionAsync(request.BaselineReleaseId, request.CorporateOwnerId, request.IdempotencyKey,
            request.DisplayName, request.Description, request.CorrelationId, ct);
}
