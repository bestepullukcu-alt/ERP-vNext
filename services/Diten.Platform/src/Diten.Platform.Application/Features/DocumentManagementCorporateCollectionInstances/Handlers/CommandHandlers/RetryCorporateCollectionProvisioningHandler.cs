using Diten.Platform.Application.Common;
using Diten.Platform.Application.Features.DocumentManagementCorporateCollectionInstances.Commands;
using Diten.Platform.Domain.Repositories;
using MediatR;

namespace Diten.Platform.Application.Features.DocumentManagementCorporateCollectionInstances.Handlers.CommandHandlers;

public sealed class RetryCorporateCollectionProvisioningHandler
    : IRequestHandler<RetryCorporateCollectionProvisioningCommand, Response<CorporateCollectionProvisioningResult>>
{
    private readonly ICorporateCollectionProvisioningOperationRepository _operations;
    private readonly CorporateCollectionInstanceProvisioningService _service;

    public RetryCorporateCollectionProvisioningHandler(
        ICorporateCollectionProvisioningOperationRepository operations,
        CorporateCollectionInstanceProvisioningService service)
    {
        _operations = operations;
        _service = service;
    }

    public async Task<Response<CorporateCollectionProvisioningResult>> Handle(
        RetryCorporateCollectionProvisioningCommand request,
        CancellationToken ct)
    {
        var operation = await _operations.GetByIdAsync(request.OperationId, ct);
        if (operation is null)
        {
            return Response<CorporateCollectionProvisioningResult>.Fail(
                "Provisioning operation not found.", 404,
                CorporateCollectionInstanceReasonCodes.NotFoundNonLeakage, request.CorrelationId);
        }

        operation.AttemptCount++;
        operation.LastAttemptAt = DateTimeOffset.UtcNow;
        await _operations.UpdateAsync(operation, ct);
        return await _service.RetryAsync(operation, request.CorrelationId, ct);
    }
}
