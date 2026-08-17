using Diten.Platform.Application.Features.BusinessReferenceData.Models;

namespace Diten.Platform.Application.Features.BusinessReferenceData.Services;

public interface IBusinessReferenceDataGovernanceService
{
    Task<BusinessReferenceDataVersionDetailModel> SubmitAsync(Guid versionId, string actorId, string correlationId, string? expectedConcurrencyToken, BusinessReferenceDataEvidenceInput evidence, bool overrideAction, string? overrideReason, CancellationToken ct = default);
    Task<BusinessReferenceDataVersionDetailModel> ApproveAsync(Guid versionId, string actorId, string correlationId, string? expectedConcurrencyToken, BusinessReferenceDataWorkflowTransitionAction action, string? rejectionReason, bool overrideAction, string? overrideReason, BusinessReferenceDataEvidenceInput evidence, string? requestInfoComment, string? requestInfoTargetStep, string? idempotencyKey, CancellationToken ct = default);
}
