using Diten.Platform.Application.Common;
using Diten.Platform.Application.Features.Tasks.Commands;
using Diten.Platform.Application.Features.WorkAggregation;
using Diten.Platform.Application.Features.WorkAggregation.Dispatch;
using Diten.Platform.Domain.Enums.Tasks;
using MediatR;

namespace Diten.Platform.Application.Features.Tasks.Providers;

/// <summary>
/// WC-D2 — MOD-0024's write half. Every action code <see cref="TaskWorkItemProvider"/> projects is forwarded to
/// the command TasksController ALREADY sends for the same verb.
///
/// <para><b>NO NEW BUSINESS LOGIC.</b> Not one lifecycle rule, permission rule or validation lives here: the
/// eleven task endpoints keep their handlers, their validators and their refusal codes, and this class is a
/// translation from one wire shape to another. That is deliberate — a second copy of "may this task start" is
/// how the two answers start disagreeing.</para>
///
/// <para><b>/Tasks keeps its own path.</b> The Tasks screens still POST to <c>/api/v1/tasks/{id}/{verb}</c> and
/// nothing here changes that. This slice ADDS an address; it does not migrate one.</para>
/// </summary>
public sealed class TaskWorkItemActionDispatcher : IWorkItemActionDispatcher
{
    private readonly IMediator _mediator;

    public TaskWorkItemActionDispatcher(IMediator mediator) => _mediator = mediator;

    public string ProviderCode => WorkItemContract.ProviderCodeTasks;

    /*
     * The action code IS the verb, in both directions: the projection emits `submitReview`, the controller route
     * is `{id}/submitReview`, and this map's key is the same word. Where MOD-0024 already learned that lesson
     * ("the code doubles as the URL segment on the client, so the two names are one name"), this map keeps the
     * third copy in step.
     *
     * The PERMISSION beside each verb is the one its endpoint's [HasPermission] attribute carries — read off
     * TasksController, not chosen here. WorkItemActionDispatchTests asserts every key in this map is also
     * declared by TaskWorkItemProvider.RequiredActionPermissions, so this cannot drift into a private list.
     */
    private static readonly IReadOnlyDictionary<string, string> Permissions =
        new Dictionary<string, string>(StringComparer.Ordinal)
        {
            ["accept"] = TaskPermissions.Update,
            ["claim"] = TaskPermissions.Claim,
            ["release"] = TaskPermissions.Claim,
            ["plan"] = TaskPermissions.Update,
            ["start"] = TaskPermissions.Update,
            ["submitReview"] = TaskPermissions.Update,
            ["complete"] = TaskPermissions.Complete,
            ["inquire"] = TaskPermissions.Update,
            ["return"] = TaskPermissions.Update,
            ["reassign"] = TaskPermissions.Assign,
            ["cancel"] = TaskPermissions.Cancel
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
        var version = payload.ExpectedVersion ?? 0;

        // The generic body three quarters of these take. `reason` is folded onto `note` exactly as the client's
        // __default vocabulary entry does, so a free-text explanation still reaches the audit trail.
        var transition = new TaskTransitionRequest(version, payload.ReasonCode, payload.Note ?? payload.Reason);

        switch (request.ActionCode)
        {
            case "accept":
                return Map(await _mediator.Send(
                    new AcceptTaskItemCommand(request.ItemId, transition, request.CorrelationId), ct), request);

            case "claim":
                return Map(await _mediator.Send(
                    new ClaimTaskItemCommand(
                        request.ItemId, new ClaimTaskItemRequest(version), request.CorrelationId), ct), request);

            case "release":
                return Map(await _mediator.Send(
                    new ReleaseTaskItemCommand(request.ItemId, transition, request.CorrelationId), ct), request);

            case "plan":
                // The one action with a REQUIRED field of its own. Refused here rather than sent on as a default
                // date: a plan nobody chose is worse than a refusal that says what is missing.
                if (payload.PlannedDate is not { } plannedDate)
                {
                    return WorkItemActionDispatchResults.PayloadInvalid(request, nameof(payload.PlannedDate));
                }

                return Map(await _mediator.Send(
                    new PlanTaskItemCommand(
                        request.ItemId,
                        new PlanTaskItemRequest(version, plannedDate),
                        request.CorrelationId), ct), request);

            case "start":
                return Map(await _mediator.Send(
                    new TransitionTaskItemCommand(
                        request.ItemId, TaskLifecycle.InProgress, transition, request.CorrelationId), ct), request);

            case "submitReview":
                return Map(await _mediator.Send(
                    new SubmitTaskForReviewCommand(request.ItemId, transition, request.CorrelationId), ct), request);

            case "complete":
                return Map(await _mediator.Send(
                    new TransitionTaskItemCommand(
                        request.ItemId, TaskLifecycle.Done, transition, request.CorrelationId), ct), request);

            case "inquire":
                if (string.IsNullOrWhiteSpace(payload.Reason))
                {
                    return WorkItemActionDispatchResults.PayloadInvalid(request, nameof(payload.Reason));
                }

                return Map(await _mediator.Send(
                    new InquireTaskItemCommand(
                        request.ItemId,
                        new InquireTaskItemRequest(version, payload.Reason!, payload.WaitingOnUserId),
                        request.CorrelationId), ct), request);

            case "return":
                if (string.IsNullOrWhiteSpace(payload.Reason))
                {
                    return WorkItemActionDispatchResults.PayloadInvalid(request, nameof(payload.Reason));
                }

                return Map(await _mediator.Send(
                    new ReturnTaskItemCommand(
                        request.ItemId,
                        new ReturnTaskItemRequest(version, payload.Reason!),
                        request.CorrelationId), ct), request);

            case "reassign":
                if (payload.AssigneeUserId is not { } assignee || assignee == Guid.Empty)
                {
                    return WorkItemActionDispatchResults.PayloadInvalid(request, nameof(payload.AssigneeUserId));
                }

                if (string.IsNullOrWhiteSpace(payload.Reason))
                {
                    return WorkItemActionDispatchResults.PayloadInvalid(request, nameof(payload.Reason));
                }

                return Map(await _mediator.Send(
                    new ReassignTaskItemCommand(
                        request.ItemId,
                        new ReassignTaskItemRequest(version, assignee, payload.Reason!),
                        request.CorrelationId), ct), request);

            case "cancel":
                /*
                 * Administrative authority over ANY task, passed to the handler as DATA — exactly what
                 * TasksController does with PermissionClaimEvaluator. It is read off the ACTOR, not re-derived
                 * from claims: the actor's granted set was built by WorkItemsController from the providers'
                 * declared keys through that same evaluator, and TaskPermissions.Delete is one of them. A
                 * second derivation here is the drift the controller's own comment warns about.
                 */
                return Map(await _mediator.Send(
                    new TransitionTaskItemCommand(
                        request.ItemId,
                        TaskLifecycle.Cancelled,
                        transition,
                        request.CorrelationId,
                        request.Actor.Has(TaskPermissions.Delete)), ct), request);

            default:
                return WorkItemActionDispatchResults.ActionUnknown(request);
        }
    }

    private Response<WorkItemActionResultDto> Map<T>(Response<T> inner, WorkItemActionDispatchRequest request)
        => WorkItemActionDispatchResults.From(inner, request, ProviderCode);
}
