using Diten.Platform.API.Models.Workflow;
using Microsoft.AspNetCore.Mvc;

namespace Diten.Platform.API.Security;

public interface ITrustedWorkflowConsumerRequestExecutor
{
    Task<IActionResult> ExecuteStartAsync(
        HttpContext httpContext,
        CancellationToken cancellationToken,
        Func<TrustedWorkflowStartTransportRequest, string, TrustedWorkflowConsumerServiceIdentity,
            TrustedWorkflowDelegatedUserIdentity, CancellationToken, Task<IActionResult>> dispatch,
        Func<int, string, IActionResult> failure);

    Task<IActionResult> ExecuteEvidenceAsync(
        HttpContext httpContext,
        CancellationToken cancellationToken,
        Func<TrustedWorkflowTerminalDecisionEvidenceTransportRequest, TrustedWorkflowConsumerServiceIdentity,
            CancellationToken, Task<IActionResult>> dispatch,
        Func<int, string, IActionResult> failure);

    Task<IActionResult> ExecuteStartResultAsync(
        HttpContext httpContext,
        CancellationToken cancellationToken,
        Func<TrustedWorkflowStartResultTransportRequest, string, TrustedWorkflowConsumerServiceIdentity,
            CancellationToken, Task<IActionResult>> dispatch,
        Func<int, string, IActionResult> failure);
}
