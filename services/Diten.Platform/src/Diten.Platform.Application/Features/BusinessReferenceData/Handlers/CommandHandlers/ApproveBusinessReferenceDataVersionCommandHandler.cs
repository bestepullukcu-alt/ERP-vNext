using Diten.Platform.Application.Common;
using Diten.Platform.Application.Contracts;
using Diten.Platform.Application.Features.BusinessReferenceData.Commands;
using Diten.Platform.Application.Features.BusinessReferenceData.Models;
using Diten.Platform.Application.Features.BusinessReferenceData.Services;
using Diten.Platform.Domain.Entities;
using Diten.Platform.Domain.Repositories;
using MediatR;

namespace Diten.Platform.Application.Features.BusinessReferenceData.Handlers.CommandHandlers;

public sealed class ApproveBusinessReferenceDataVersionCommandHandler : IRequestHandler<ApproveBusinessReferenceDataVersionCommand, Response<BusinessReferenceDataVersionDetailModel>>
{
    private readonly IBusinessReferenceDataGovernanceService _governanceService;

    public ApproveBusinessReferenceDataVersionCommandHandler(IBusinessReferenceDataGovernanceService governanceService)
    {
        _governanceService = governanceService;
    }

    public async Task<Response<BusinessReferenceDataVersionDetailModel>> Handle(ApproveBusinessReferenceDataVersionCommand request, CancellationToken ct)
    {
        var result = await _governanceService.ApproveAsync(
            request.VersionId,
            request.ActorId,
            request.CorrelationId,
            request.ExpectedConcurrencyToken,
            request.Action,
            request.RejectionReason,
            request.OverrideAction,
            request.OverrideReason,
            request.Evidence,
            request.RequestInfoComment,
            request.RequestInfoTargetStep,
            request.IdempotencyKey,
            ct);
        return Response<BusinessReferenceDataVersionDetailModel>.Success(result);
    }
}
