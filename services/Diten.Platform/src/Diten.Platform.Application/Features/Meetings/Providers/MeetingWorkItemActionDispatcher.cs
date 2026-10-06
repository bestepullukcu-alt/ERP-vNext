using Diten.Platform.Application.Common;
using Diten.Platform.Application.Features.Meetings.Commands;
using Diten.Platform.Application.Features.WorkAggregation;
using Diten.Platform.Application.Features.WorkAggregation.Dispatch;
using MediatR;

namespace Diten.Platform.Application.Features.Meetings.Providers;

/// <summary>
/// MOD-0357 S5c — the write half of <see cref="MeetingWorkItemProvider"/>. Forwards to the SAME
/// <c>RespondToInvitationCommand</c> the S5 <c>POST api/v1/meetings/{id}/respond</c> endpoint already sends
/// (K5's 404/409/200 semantics — this dispatcher decides nothing new).
///
/// <para>The action CODE carries the choice (<c>acceptInvite</c> → "Accept", <c>declineInvite</c> → "Decline"),
/// so no payload field is needed the way <c>reassign</c>'s <c>AssigneeUserId</c> is: the two actions ARE the
/// two answers.</para>
/// </summary>
public sealed class MeetingWorkItemActionDispatcher : IWorkItemActionDispatcher
{
    private readonly IMediator _mediator;

    public MeetingWorkItemActionDispatcher(IMediator mediator) => _mediator = mediator;

    public string ProviderCode => WorkItemContract.ProviderCodeMeetings;

    private static readonly IReadOnlyDictionary<string, string> ResponseByActionCode =
        new Dictionary<string, string>(StringComparer.Ordinal)
        {
            ["acceptInvite"] = "Accept",
            ["declineInvite"] = "Decline"
        };

    public IReadOnlyCollection<string> SupportedActionCodes { get; } = ResponseByActionCode.Keys.ToArray();

    public bool CanDispatch(string actionCode) => ResponseByActionCode.ContainsKey(actionCode ?? string.Empty);

    /// <summary>The SAME key <see cref="MeetingWorkItemProvider.RequiredActionPermissions"/> declares — the
    /// guard test asserts exactly that containment, so this cannot invent a key the provider never granted.</summary>
    public string? RequiredPermission(string actionCode)
        => ResponseByActionCode.ContainsKey(actionCode ?? string.Empty) ? MeetingPermissions.Read : null;

    public async Task<Response<WorkItemActionResultDto>> DispatchAsync(
        WorkItemActionDispatchRequest request, CancellationToken ct = default)
    {
        if (!ResponseByActionCode.TryGetValue(request.ActionCode, out var response))
        {
            return WorkItemActionDispatchResults.ActionUnknown(request);
        }

        var result = await _mediator.Send(
            new RespondToInvitationCommand(request.ItemId, new RespondToInvitationRequest(response), request.CorrelationId), ct);
        return WorkItemActionDispatchResults.From(result, request, ProviderCode);
    }
}
