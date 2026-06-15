using Diten.Platform.Application.Features.BusinessReferenceData.Models;

namespace Diten.Platform.Application.Features.BusinessReferenceData.Services;

public interface IBusinessReferenceDataPublishService
{
    Task<BusinessReferenceDataVersionDetailModel> PublishAsync(
        Guid versionId,
        string actorId,
        string correlationId,
        string idempotencyKey,
        string publishMode,
        DateTimeOffset? publishAt,
        string? expectedConcurrencyToken,
        bool overrideAction,
        string? overrideReason,
        CancellationToken ct = default);
}
