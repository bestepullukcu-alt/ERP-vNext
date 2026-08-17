using Diten.Platform.Application.Features.BusinessReferenceData.Models;

namespace Diten.Platform.Application.Features.BusinessReferenceData.Services;

public interface IBusinessReferenceDataValidationService
{
    Task<BusinessReferenceDataValidationRunModel> ValidateDraftVersionAsync(Guid versionId, string? correlationId, CancellationToken ct = default);
}
