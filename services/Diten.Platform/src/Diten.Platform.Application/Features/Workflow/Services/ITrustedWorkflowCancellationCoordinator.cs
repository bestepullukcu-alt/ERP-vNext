using Diten.Platform.Application.Common;

namespace Diten.Platform.Application.Features.Workflow.Services;

public interface ITrustedWorkflowCancellationCoordinator
{
    Task<Response<TrustedWorkflowCancellationPreflight>> PreflightAsync(
        Guid serviceClientId,
        Guid workflowInstanceId,
        Guid approvalTaskId,
        string expectedObjectType,
        string expectedObjectId,
        Guid expectedMakerSubjectId,
        string correlationId,
        CancellationToken ct);

    Task<Response<TrustedWorkflowCancellationEvidence>> CancelAsync(
        Guid serviceClientId,
        Guid delegatedRequesterUserId,
        TrustedWorkflowCancellationRequest request,
        string correlationId,
        CancellationToken ct);
}
