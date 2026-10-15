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

    Task<IActionResult> ExecuteCancellationPreflightAsync(
        HttpContext httpContext,
        CancellationToken cancellationToken,
        Func<TrustedWorkflowCancellationPreflightTransportRequest, TrustedWorkflowConsumerServiceIdentity,
            CancellationToken, Task<IActionResult>> dispatch,
        Func<int, string, IActionResult> failure) =>
        Task.FromResult(failure(
            StatusCodes.Status503ServiceUnavailable,
            "WORKFLOW_TRUSTED_CONSUMER_UNAVAILABLE"));

    Task<IActionResult> ExecuteCancellationAsync(
        HttpContext httpContext,
        CancellationToken cancellationToken,
        Func<TrustedWorkflowCancellationTransportRequest, string, TrustedWorkflowConsumerServiceIdentity,
            TrustedWorkflowDelegatedUserIdentity, CancellationToken, Task<IActionResult>> dispatch,
        Func<int, string, IActionResult> failure) =>
        Task.FromResult(failure(
            StatusCodes.Status503ServiceUnavailable,
            "WORKFLOW_TRUSTED_CONSUMER_UNAVAILABLE"));
}
