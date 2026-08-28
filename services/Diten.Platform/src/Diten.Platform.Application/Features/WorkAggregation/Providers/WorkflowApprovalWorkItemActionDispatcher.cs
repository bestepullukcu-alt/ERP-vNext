using Diten.Platform.Application.Common;
using Diten.Platform.Application.Features.WorkAggregation.Dispatch;
using Diten.Platform.Application.Features.Workflow;
using Diten.Platform.Application.Features.Workflow.Commands;
using MediatR;

namespace Diten.Platform.Application.Features.WorkAggregation.Providers;

/// <summary>
/// WC-D2 — MOD-0023's write half, and the reason this slice needed TWO dispatchers.
///
/// <para><b>One implementation proves nothing.</b> A dispatch seam demonstrated on the single provider that
/// already worked would be the defect DCP-004 §2 D2 documents, re-shipped with a nicer URL: the approval
/// provider has been on the board since WC-1 with four live endpoints behind it, and not one of its buttons ever
/// reached them. This class is the measurement — an action from a provider that is NOT <c>tasks</c> arriving at
/// the server that owns it.</para>
///
/// <para><b>The approval boundary is untouched.</b> MOD-0024 reports and forwards; it never decides. Each verb
/// here goes to the MOD-0023 command TasksController's workflow sibling already sends — the same handler, the
/// same assignment check (only the RESOLVED principal may act), the same segregation-of-duties rule, the same
/// refusal codes. No local <c>if (ApprovalRequired)</c> exists here and none may be added: that is the mistake
/// this repository has already made once.</para>
/// </summary>
public sealed class WorkflowApprovalWorkItemActionDispatcher : IWorkItemActionDispatcher
{
    private readonly IMediator _mediator;

    public WorkflowApprovalWorkItemActionDispatcher(IMediator mediator) => _mediator = mediator;

    public string ProviderCode => WorkItemContract.ProviderCodeWorkflow;

    /*
     * The four codes WorkItemProjectionService emits for an actionable approval, paired with the permission its
     * endpoint's [HasPermission] attribute carries. Read off WorkflowDefinitionsController; asserted against
     * WorkflowApprovalWorkItemProvider.RequiredActionPermissions by WorkItemActionDispatchTests.
     */
    private static readonly IReadOnlyDictionary<string, string> Permissions =
        new Dictionary<string, string>(StringComparer.Ordinal)
        {
            ["approve"] = WorkflowPermissions.TasksApprove,
            ["reject"] = WorkflowPermissions.TasksReject,
            ["requestInfo"] = WorkflowPermissions.TasksRequestInfo,
            ["delegate"] = WorkflowPermissions.TasksDelegate
        };

    public IReadOnlyCollection<string> SupportedActionCodes { get; } = Permissions.Keys.ToArray();

    public bool CanDispatch(string actionCode) => Permissions.ContainsKey(actionCode ?? string.Empty);

    public string? RequiredPermission(string actionCode)
        => Permissions.TryGetValue(actionCode ?? string.Empty, out var key) ? key : null;

    public async Task<Response<WorkItemActionResultDto>> DispatchAsync(
        WorkItemActionDispatchRequest request,
        CancellationToken ct = default)
    {
        var payload = request.Payload;

        /*
         * ACTOR FROM THE SERVER, never from the body.
         *
         * MOD-0023's request DTOs carry an ActorId because they were written for service-to-service callers, and
         * WorkflowTaskTransitionSupport compares it against the assignment snapshot's resolved principal. Passing
         * a browser-supplied value through would let a caller decide on somebody else's behalf simply by typing
         * their id. The identity used here is the one the API layer resolved from the JWT.
         */
        var actorId = request.Actor.UserId.ToString();

        /*
         * The transition log wants a reason CODE and MOD-0023 requires one. When the caller has not sent a code,
         * the surface it was pressed on is the honest answer — the log then says a decision came from the Task
         * Center rather than carrying an invented business reason. The user's own sentence travels as `comment`,
         * which is where free text belongs.
         */
        var reasonCode = string.IsNullOrWhiteSpace(payload.ReasonCode)
            ? $"WORKCENTER_{request.ActionCode.ToUpperInvariant()}"
            : payload.ReasonCode!.Trim();

        // A retried click must not become a second decision. The caller supplies a key when it has one; otherwise
        // one is minted, because the endpoint requires the field and refusing the write would be worse.
        var idempotencyKey = string.IsNullOrWhiteSpace(payload.IdempotencyKey)
            ? Guid.NewGuid().ToString("N")
            : payload.IdempotencyKey!.Trim();

        var comment = string.IsNullOrWhiteSpace(payload.Comment) ? payload.Reason : payload.Comment;

        switch (request.ActionCode)
        {
            case "approve":
                return Map(await _mediator.Send(
                    new ApproveWorkflowTaskCommand(
                        request.ItemId,
                        new ApproveWorkflowTaskRequest(actorId, reasonCode, idempotencyKey, comment, payload.EvidenceRef),
                        request.CorrelationId), ct), request);

            case "reject":
                return Map(await _mediator.Send(
                    new RejectWorkflowTaskCommand(
                        request.ItemId,
                        new RejectWorkflowTaskRequest(actorId, reasonCode, idempotencyKey, comment, payload.EvidenceRef),
                        request.CorrelationId), ct), request);

            case "requestInfo":
                return Map(await _mediator.Send(
                    new RequestInfoWorkflowTaskCommand(
                        request.ItemId,
                        new RequestInfoWorkflowTaskRequest(
                            actorId, payload.TargetPrincipalId, reasonCode, idempotencyKey, comment, payload.EvidenceRef),
                        request.CorrelationId), ct), request);

            case "delegate":
                // The one field with nothing to fall back on: a delegation with no delegate is not a delegation.
                if (string.IsNullOrWhiteSpace(payload.TargetPrincipalId))
                {
                    return WorkItemActionDispatchResults.PayloadInvalid(request, nameof(payload.TargetPrincipalId));
                }

                return Map(await _mediator.Send(
                    new DelegateWorkflowTaskCommand(
                        request.ItemId,
                        new DelegateWorkflowTaskRequest(
                            actorId, payload.TargetPrincipalId!.Trim(), reasonCode, idempotencyKey, comment),
                        request.CorrelationId), ct), request);

            default:
                return WorkItemActionDispatchResults.ActionUnknown(request);
        }
    }

    private Response<WorkItemActionResultDto> Map<T>(Response<T> inner, WorkItemActionDispatchRequest request)
        => WorkItemActionDispatchResults.From(inner, request, ProviderCode);
}
