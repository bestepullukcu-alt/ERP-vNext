using Diten.Platform.Application.Common;
using Diten.Platform.Application.Contracts;
using Diten.Platform.Application.Features.BusinessReferenceData.Commands;
using Diten.Platform.Application.Features.BusinessReferenceData.Models;
using Diten.Platform.Application.Features.BusinessReferenceData.Services;
using Diten.Platform.Domain.Entities;
using Diten.Platform.Domain.Repositories;
using MediatR;

namespace Diten.Platform.Application.Features.BusinessReferenceData.Handlers.CommandHandlers;

public sealed class DeactivateBusinessReferenceDataUsageRegistrationCommandHandler : IRequestHandler<DeactivateBusinessReferenceDataUsageRegistrationCommand, Response<bool>>
{
    private readonly IBusinessReferenceDataConsumerQueryService _consumerQueryService;

    public DeactivateBusinessReferenceDataUsageRegistrationCommandHandler(IBusinessReferenceDataConsumerQueryService consumerQueryService)
    {
        _consumerQueryService = consumerQueryService;
    }

    public async Task<Response<bool>> Handle(DeactivateBusinessReferenceDataUsageRegistrationCommand request, CancellationToken ct)
    {
        var result = await _consumerQueryService.DeactivateUsageRegistrationAsync(
            request.UsageRegistrationId,
            request.ActorId,
            request.CorrelationId,
            ct);
        return Response<bool>.Success(result);
    }
}
