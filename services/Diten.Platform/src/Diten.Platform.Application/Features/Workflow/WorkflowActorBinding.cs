using Diten.Platform.Application.Common;
using Diten.Platform.Application.Contracts;

namespace Diten.Platform.Application.Features.Workflow;

/// <summary>
/// WP-0023-FX — the acting person on the direct task API is the authenticated caller, never the request body.
///
/// <para>The task request DTOs carry an <c>ActorId</c> because they were written for in-process callers, which pass
/// a server-resolved id (WorkCenter: the JWT user id; the Tasks services: the current user). Over HTTP that field is
/// caller-controlled, and the transition handlers only check it against the assignment snapshot — so a holder of the
/// approve key could act as any other assignee by typing that person's id. That breaks attribution, and an
/// electronic signature (MOD-0022 E4) cannot be bound to an approval whose actor is chosen by the client.</para>
///
/// <para>Rules: the caller must have a user id (JWT <c>sub</c>). A blank body actor means "me". A body actor that
/// names the caller — by id in any GUID spelling, or by the caller's own e-mail — is accepted. Anything else is
/// refused with 403 <c>WORKFLOW_ACTOR_MISMATCH</c> before a command is sent. The bound value is always the caller's
/// user id (lower-case "D"), so logs and audit name the real person.</para>
///
/// <para>Not covered here, on purpose: in-process callers construct the commands themselves with a server-resolved
/// id (measured: <c>WorkflowApprovalWorkItemActionDispatcher</c>, <c>TaskApprovalService</c>,
/// <c>TaskReviewService</c>), and the escalation run acts as the system and carries no actor in its request.</para>
/// </summary>
public static class WorkflowActorBinding
{
    public static (string? ActorId, Response<WorkflowTaskTransitionResponse>? Failure) Bind(
        string? bodyActorId,
        ICurrentUserContext caller,
        string correlationId)
    {
        if (caller.UserId == Guid.Empty)
        {
            return (null, Mismatch("The authenticated caller has no user id; a workflow task cannot be acted on.", correlationId));
        }

        var callerId = caller.UserId.ToString();
        if (string.IsNullOrWhiteSpace(bodyActorId))
        {
            return (callerId, null);
        }

        var named = bodyActorId.Trim();
        var namesCaller = WorkflowPrincipal.Same(named, callerId)
            || (!string.IsNullOrWhiteSpace(caller.Email) && string.Equals(named, caller.Email.Trim(), StringComparison.OrdinalIgnoreCase));

        return namesCaller
            ? (callerId, null)
            : (null, Mismatch("The actor in the request is not the authenticated caller.", correlationId));
    }

    private static Response<WorkflowTaskTransitionResponse> Mismatch(string message, string correlationId) =>
        Response<WorkflowTaskTransitionResponse>.Fail(message, 403, WorkflowReasonCodes.WorkflowActorMismatch, correlationId);
}
