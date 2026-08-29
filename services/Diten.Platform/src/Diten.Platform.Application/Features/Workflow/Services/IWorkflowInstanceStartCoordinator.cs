using Diten.Platform.Application.Common;

namespace Diten.Platform.Application.Features.Workflow.Services;

public interface IWorkflowInstanceStartCoordinator
{
    Task<Response<TrustedWorkflowStartResult>> StartAsync(
        TrustedWorkflowStartRequest request,
        Guid serviceClientId,
        Guid delegatedMakerUserId,
        string correlationId,
        CancellationToken ct = default);
}
