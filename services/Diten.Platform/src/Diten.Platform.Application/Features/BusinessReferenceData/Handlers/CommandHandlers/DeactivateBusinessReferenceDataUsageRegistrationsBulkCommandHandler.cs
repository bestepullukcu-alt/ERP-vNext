using Diten.Platform.Application.Common;
using Diten.Platform.Application.Features.BusinessReferenceData.Commands;
using Diten.Platform.Application.Features.BusinessReferenceData.Services;
using MediatR;

namespace Diten.Platform.Application.Features.BusinessReferenceData.Handlers.CommandHandlers;

public sealed class DeactivateBusinessReferenceDataUsageRegistrationsBulkCommandHandler : IRequestHandler<DeactivateBusinessReferenceDataUsageRegistrationsBulkCommand, Response<int>>
{
    private readonly IBusinessReferenceDataConsumerQueryService _consumerQueryService;

    public DeactivateBusinessReferenceDataUsageRegistrationsBulkCommandHandler(IBusinessReferenceDataConsumerQueryService consumerQueryService)
    {
        _consumerQueryService = consumerQueryService;
    }

    public async Task<Response<int>> Handle(DeactivateBusinessReferenceDataUsageRegistrationsBulkCommand request, CancellationToken ct)
    {
        var result = await _consumerQueryService.DeactivateUsageRegistrationsBulkAsync(
            request.UsageRegistrationIds,
            request.ActorId,
            request.CorrelationId,
            ct);
        return Response<int>.Success(result, 200);
    }
}
