using Diten.Platform.Application.Common;
using Diten.Platform.Application.Features.WorkAggregation.Providers;

namespace Diten.Platform.Application.Features.WorkAggregation.Dispatch;

/// <summary>
/// WC-D2 — the WRITE half of a source, and a SIBLING of <see cref="IWorkItemProvider"/> rather than an addition
/// to it.
///
/// <para><b>Why a second interface and not two more methods.</b> <see cref="IWorkItemProvider"/> says of itself,
/// in its own words, "READ-ONLY: a provider must never write business state" — a rule the aggregation handler
/// leans on when it runs every provider inside a timeout and treats a failure as a missing SOURCE rather than a
/// failed write. Hanging a Dispatch method off that interface would make the sentence false and the isolation
/// argument with it. So the read seam is untouched, and a provider that also accepts writes implements this
/// one too. WorkItemActionDispatchTests pins that: <see cref="IWorkItemProvider"/> has no write method.</para>
///
/// <para><b>The pairing is a rule, not a convention.</b> A provider that publishes actions[] and has no
/// dispatcher can only ship dead buttons — which is the defect DCP-004 §2 D2 records. The guard test walks every
/// provider in the assembly, projects real items, and asserts every action code it emits is dispatchable.</para>
/// </summary>
public interface IWorkItemActionDispatcher
{
    /// <summary>The provider whose items this dispatcher writes. Matches <see cref="IWorkItemProvider.ProviderCode"/>.</summary>
    string ProviderCode { get; }

    /// <summary>Every action code this dispatcher can carry out — the set the guard test measures against.</summary>
    IReadOnlyCollection<string> SupportedActionCodes { get; }

    bool CanDispatch(string actionCode);

    /// <summary>
    /// The permission key the underlying endpoint requires for this action, or null when the code is unknown.
    ///
    /// <para>This is NOT a new permission list. Every key returned here is one of the module's own declared
    /// constants and one the matching provider already declares in
    /// <see cref="IWorkItemProvider.RequiredActionPermissions"/> — the guard test asserts exactly that
    /// containment, so a key cannot be invented at this seam. Enforcement stays where it always was: the API
    /// layer evaluates the key against the caller's CLAIMS through PermissionClaimEvaluator, and the module's
    /// own handler re-checks its own rules underneath. The browser is authority for nothing.</para>
    /// </summary>
    string? RequiredPermission(string actionCode);

    /// <summary>
    /// Forward the action to the endpoint that already owns it. NO NEW BUSINESS RULE LIVES HERE: a dispatcher
    /// translates a wire shape into the module's existing command and returns what the module answered, refusal
    /// codes and all.
    /// </summary>
    Task<Response<WorkItemActionResultDto>> DispatchAsync(
        WorkItemActionDispatchRequest request,
        CancellationToken ct = default);
}

/// <summary>Shared mapping from a module's own answer onto this endpoint's envelope.</summary>
public static class WorkItemActionDispatchResults
{
    /// <summary>
    /// Carry the module's verdict through UNCHANGED — status code, reason code and error text.
    ///
    /// <para>The reason code is the whole point: the Task Center's messages are resolved from stable codes in
    /// seven languages, so a dispatcher that flattened a 409 TASK_CONCURRENCY_CONFLICT into a generic failure
    /// would silently disconnect that bridge and every refusal would read "an error occurred".</para>
    /// </summary>
    public static Response<WorkItemActionResultDto> From<T>(
        Response<T> inner,
        WorkItemActionDispatchRequest request,
        string providerCode)
        => inner.IsSuccessful
            ? Response<WorkItemActionResultDto>.Success(
                new WorkItemActionResultDto(
                    request.ItemId.ToString(),
                    providerCode,
                    request.ActionCode),
                inner.StatusCode == 204 ? 200 : inner.StatusCode,
                request.CorrelationId)
            : Response<WorkItemActionResultDto>.Fail(
                inner.Errors.Count > 0 ? inner.Errors : ["The action was refused."],
                inner.StatusCode,
                inner.ReasonCode,
                request.CorrelationId);

    /// <summary>A field the action cannot proceed without. Named, so the caller learns WHICH one.</summary>
    public static Response<WorkItemActionResultDto> PayloadInvalid(
        WorkItemActionDispatchRequest request,
        string field)
        => Response<WorkItemActionResultDto>.Fail(
            $"'{field}' is required for action '{request.ActionCode}'.",
            400,
            WorkItemActionReasonCodes.PayloadInvalid,
            request.CorrelationId);

    public static Response<WorkItemActionResultDto> ActionUnknown(WorkItemActionDispatchRequest request)
        => Response<WorkItemActionResultDto>.Fail(
            $"Action '{request.ActionCode}' is not dispatchable for this work item.",
            400,
            WorkItemActionReasonCodes.ActionUnknown,
            request.CorrelationId);
}
