using Diten.Platform.Application.Common;
using Diten.Platform.Application.Contracts;
using Diten.Platform.Application.Features.BusinessReferenceData.Commands;
using Diten.Platform.Application.Features.BusinessReferenceData.Models;
using Diten.Platform.Application.Features.BusinessReferenceData.Services;
using Diten.Platform.Domain.Entities;
using Diten.Platform.Domain.Repositories;
using MediatR;

namespace Diten.Platform.Application.Features.BusinessReferenceData.Handlers.CommandHandlers;

public sealed class SubmitBusinessReferenceDataVersionCommandHandler : IRequestHandler<SubmitBusinessReferenceDataVersionCommand, Response<BusinessReferenceDataVersionDetailModel>>
{
    private readonly IBusinessReferenceDataGovernanceService _governanceService;

    public SubmitBusinessReferenceDataVersionCommandHandler(IBusinessReferenceDataGovernanceService governanceService)
    {
        _governanceService = governanceService;
    }

    public async Task<Response<BusinessReferenceDataVersionDetailModel>> Handle(SubmitBusinessReferenceDataVersionCommand request, CancellationToken ct)
    {
        var result = await _governanceService.SubmitAsync(
            request.VersionId,
            request.ActorId,
            request.CorrelationId,
            request.ExpectedConcurrencyToken,
            request.Evidence,
            request.OverrideAction,
            request.OverrideReason,
            ct);
        return Response<BusinessReferenceDataVersionDetailModel>.Success(result);
    }
}
