using Diten.Platform.Application.Common;
using Diten.Platform.Application.Features.DocumentManagementCorporateCollectionInstances.Queries;
using Diten.Platform.Domain.Repositories;
using MediatR;

namespace Diten.Platform.Application.Features.DocumentManagementCorporateCollectionInstances.Handlers.QueryHandlers;

public sealed class GetCorporateCollectionProvisioningOperationHandler
    : IRequestHandler<GetCorporateCollectionProvisioningOperationQuery, Response<CorporateCollectionProvisioningOperationModel>>
{
    private readonly ICorporateCollectionProvisioningOperationRepository _operations;

    public GetCorporateCollectionProvisioningOperationHandler(ICorporateCollectionProvisioningOperationRepository operations) =>
        _operations = operations;

    public async Task<Response<CorporateCollectionProvisioningOperationModel>> Handle(
        GetCorporateCollectionProvisioningOperationQuery request,
        CancellationToken ct)
    {
        var operation = await _operations.GetByIdAsync(request.OperationId, ct);
        return operation is null
            ? Response<CorporateCollectionProvisioningOperationModel>.Fail(
                "Provisioning operation not found.", 404,
                CorporateCollectionInstanceReasonCodes.NotFoundNonLeakage, request.CorrelationId)
            : Response<CorporateCollectionProvisioningOperationModel>.Success(
                CorporateCollectionMapping.ToModel(operation), correlationId: request.CorrelationId);
    }
}
