using Diten.Platform.Application.Features.BusinessReferenceData.Models;

namespace Diten.Platform.Application.Features.BusinessReferenceData.Services;

public interface IBusinessReferenceDataImportService
{
    Task<BusinessReferenceDataImportPreviewModel> PreviewAsync(
        Guid targetDraftVersionId,
        string fileName,
        string format,
        string contentBase64,
        string actorId,
        string correlationId,
        CancellationToken ct = default);

    Task<BusinessReferenceDataImportCommitResultModel> CommitAsync(
        Guid previewId,
        string idempotencyKey,
        string actorId,
        string correlationId,
        CancellationToken ct = default);
}
