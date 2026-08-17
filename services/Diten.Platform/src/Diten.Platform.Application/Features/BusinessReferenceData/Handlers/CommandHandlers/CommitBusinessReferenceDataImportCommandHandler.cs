using Diten.Platform.Application.Common;
using Diten.Platform.Application.Contracts;
using Diten.Platform.Application.Features.BusinessReferenceData.Commands;
using Diten.Platform.Application.Features.BusinessReferenceData.Models;
using Diten.Platform.Application.Features.BusinessReferenceData.Services;
using Diten.Platform.Domain.Entities;
using Diten.Platform.Domain.Repositories;
using MediatR;

namespace Diten.Platform.Application.Features.BusinessReferenceData.Handlers.CommandHandlers;

public sealed class CommitBusinessReferenceDataImportCommandHandler : IRequestHandler<CommitBusinessReferenceDataImportCommand, Response<BusinessReferenceDataImportCommitResultModel>>
{
    private readonly IBusinessReferenceDataImportService _importService;

    public CommitBusinessReferenceDataImportCommandHandler(IBusinessReferenceDataImportService importService)
    {
        _importService = importService;
    }

    public async Task<Response<BusinessReferenceDataImportCommitResultModel>> Handle(CommitBusinessReferenceDataImportCommand request, CancellationToken ct)
    {
        var result = await _importService.CommitAsync(
            request.PreviewId,
            request.IdempotencyKey,
            request.ActorId,
            request.CorrelationId,
            ct);
        return Response<BusinessReferenceDataImportCommitResultModel>.Success(result);
    }
}
