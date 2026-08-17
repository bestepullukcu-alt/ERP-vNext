using Diten.Platform.Application.Common;
using Diten.Platform.Application.Contracts;
using Diten.Platform.Application.Features.BusinessReferenceData.Commands;
using Diten.Platform.Application.Features.BusinessReferenceData.Models;
using Diten.Platform.Application.Features.BusinessReferenceData.Services;
using Diten.Platform.Domain.Entities;
using Diten.Platform.Domain.Repositories;
using MediatR;

namespace Diten.Platform.Application.Features.BusinessReferenceData.Handlers.CommandHandlers;

public sealed class PreviewBusinessReferenceDataImportCommandHandler : IRequestHandler<PreviewBusinessReferenceDataImportCommand, Response<BusinessReferenceDataImportPreviewModel>>
{
    private readonly IBusinessReferenceDataImportService _importService;

    public PreviewBusinessReferenceDataImportCommandHandler(IBusinessReferenceDataImportService importService)
    {
        _importService = importService;
    }

    public async Task<Response<BusinessReferenceDataImportPreviewModel>> Handle(PreviewBusinessReferenceDataImportCommand request, CancellationToken ct)
    {
        var result = await _importService.PreviewAsync(
            request.TargetDraftVersionId,
            request.FileName,
            request.Format,
            request.ContentBase64,
            request.ActorId,
            request.CorrelationId,
            ct);
        return Response<BusinessReferenceDataImportPreviewModel>.Success(result);
    }
}
