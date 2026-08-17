using Diten.Platform.Application.Common;
using Diten.Platform.Application.Contracts;
using Diten.Platform.Application.Features.BusinessReferenceData.Commands;
using Diten.Platform.Application.Features.BusinessReferenceData.Models;
using Diten.Platform.Application.Features.BusinessReferenceData.Services;
using Diten.Platform.Domain.Entities;
using Diten.Platform.Domain.Repositories;
using MediatR;

namespace Diten.Platform.Application.Features.BusinessReferenceData.Handlers.CommandHandlers;

public sealed class PublishBusinessReferenceDataVersionCommandHandler : IRequestHandler<PublishBusinessReferenceDataVersionCommand, Response<BusinessReferenceDataVersionDetailModel>>
{
    private readonly IBusinessReferenceDataPublishService _publishService;

    public PublishBusinessReferenceDataVersionCommandHandler(IBusinessReferenceDataPublishService publishService)
    {
        _publishService = publishService;
    }

    public async Task<Response<BusinessReferenceDataVersionDetailModel>> Handle(PublishBusinessReferenceDataVersionCommand request, CancellationToken ct)
    {
        var result = await _publishService.PublishAsync(
            request.VersionId,
            request.ActorId,
            request.CorrelationId,
            request.IdempotencyKey,
            request.PublishMode,
            request.PublishAt,
            request.ExpectedConcurrencyToken,
            request.OverrideAction,
            request.OverrideReason,
            ct);
        return Response<BusinessReferenceDataVersionDetailModel>.Success(result);
    }
}
