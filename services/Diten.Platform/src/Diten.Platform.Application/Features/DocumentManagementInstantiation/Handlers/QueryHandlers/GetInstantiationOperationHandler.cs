using Diten.Platform.Application.Common;
using Diten.Platform.Application.Features.DocumentManagementInstantiation.Queries;
using Diten.Platform.Application.Features.DocumentManagementInstantiation.Services;
using Diten.Platform.Domain.Repositories;
using MediatR;

namespace Diten.Platform.Application.Features.DocumentManagementInstantiation.Handlers.QueryHandlers;

public sealed class GetInstantiationOperationHandler
    : IRequestHandler<GetInstantiationOperationQuery, Response<InstantiationResultModel>>
{
    private readonly IInstantiationOperationRepository _operationRepository;
    private readonly IInstantiationOutcomeRepository _outcomeRepository;

    public GetInstantiationOperationHandler(
        IInstantiationOperationRepository operationRepository,
        IInstantiationOutcomeRepository outcomeRepository)
    {
        _operationRepository = operationRepository;
        _outcomeRepository = outcomeRepository;
    }

    public async Task<Response<InstantiationResultModel>> Handle(GetInstantiationOperationQuery request, CancellationToken ct)
    {
        var operation = await _operationRepository.GetByOperationIdAsync(request.OperationId, ct);
        if (operation is null)
        {
            return Response<InstantiationResultModel>.Fail(
                "Instantiation operation not found.",
                404,
                DocumentManagementInstantiationReasonCodes.NotFoundNonLeakage,
                request.CorrelationId);
        }

        var outcomes = await _outcomeRepository.GetByOperationIdAsync(operation.OperationId, ct);
        var data = new InstantiationResultModel(
            operation.OperationId,
            operation.BaselineReleaseId,
            operation.CompanyId,
            operation.InstanceToken,
            operation.OperationType.ToWire(),
            operation.Status.ToWire(),
            operation.Created,
            operation.Skipped,
            operation.Failed,
            operation.Total,
            operation.CorrelationId,
            outcomes.Select(InstantiationMapping.ToModel).ToList());

        return Response<InstantiationResultModel>.Success(data, correlationId: request.CorrelationId);
    }
}
