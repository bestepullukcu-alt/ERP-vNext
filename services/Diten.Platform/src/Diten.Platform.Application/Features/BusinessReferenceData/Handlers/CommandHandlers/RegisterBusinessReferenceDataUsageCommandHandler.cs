using Diten.Platform.Application.Common;
using Diten.Platform.Application.Contracts;
using Diten.Platform.Application.Features.BusinessReferenceData.Commands;
using Diten.Platform.Application.Features.BusinessReferenceData.Models;
using Diten.Platform.Application.Features.BusinessReferenceData.Services;
using Diten.Platform.Domain.Entities;
using Diten.Platform.Domain.Repositories;
using MediatR;

namespace Diten.Platform.Application.Features.BusinessReferenceData.Handlers.CommandHandlers;

public sealed class RegisterBusinessReferenceDataUsageCommandHandler : IRequestHandler<RegisterBusinessReferenceDataUsageCommand, Response<BusinessReferenceDataUsageRegistrationResultModel>>
{
    private readonly IBusinessReferenceDataConsumerQueryService _consumerQueryService;

    public RegisterBusinessReferenceDataUsageCommandHandler(IBusinessReferenceDataConsumerQueryService consumerQueryService)
    {
        _consumerQueryService = consumerQueryService;
    }

    public async Task<Response<BusinessReferenceDataUsageRegistrationResultModel>> Handle(RegisterBusinessReferenceDataUsageCommand request, CancellationToken ct)
    {
        var result = await _consumerQueryService.RegisterUsageAsync(
            request.SetCode,
            request.ConsumerModule,
            request.ConsumerName,
            request.ConsumerEndpoint,
            request.ScopeType,
            request.ScopeKey,
            request.VersionPin,
            request.AsOfDate,
            request.ResolutionMode,
            request.Criticality,
            request.Notes,
            request.ActorId,
            request.CorrelationId,
            ct);
        return Response<BusinessReferenceDataUsageRegistrationResultModel>.Success(result, 201);
    }
}
