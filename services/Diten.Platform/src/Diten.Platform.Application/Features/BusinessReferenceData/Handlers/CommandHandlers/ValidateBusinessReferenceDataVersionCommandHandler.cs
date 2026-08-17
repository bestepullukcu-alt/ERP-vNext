using Diten.Platform.Application.Common;
using Diten.Platform.Application.Contracts;
using Diten.Platform.Application.Features.BusinessReferenceData.Commands;
using Diten.Platform.Application.Features.BusinessReferenceData.Models;
using Diten.Platform.Application.Features.BusinessReferenceData.Services;
using Diten.Platform.Domain.Entities;
using Diten.Platform.Domain.Repositories;
using MediatR;

namespace Diten.Platform.Application.Features.BusinessReferenceData.Handlers.CommandHandlers;

public sealed class ValidateBusinessReferenceDataVersionCommandHandler : IRequestHandler<ValidateBusinessReferenceDataVersionCommand, Response<BusinessReferenceDataValidationRunModel>>
{
    private readonly IBusinessReferenceDataValidationService _validationService;

    public ValidateBusinessReferenceDataVersionCommandHandler(IBusinessReferenceDataValidationService validationService)
    {
        _validationService = validationService;
    }

    public async Task<Response<BusinessReferenceDataValidationRunModel>> Handle(ValidateBusinessReferenceDataVersionCommand request, CancellationToken ct)
    {
        var result = await _validationService.ValidateDraftVersionAsync(request.VersionId, request.CorrelationId, ct);
        return Response<BusinessReferenceDataValidationRunModel>.Success(result);
    }
}
