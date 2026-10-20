using Diten.CrmService.Domain.Entities.Commercial;

namespace Diten.CrmService.Application.Features.Commercial.PreOrders;

/// <summary>
/// WP-VW-W6 W6-BE-a — MOD-0168 §12: the ONE place a pre-order changes state (two copies are forbidden, §10). Every
/// change appends its event row; the only way back is <c>pending_approval → draft</c> (request changes).
/// </summary>
public static class PreOrderStateMachine
{
    public static class Actions
    {
        public const string Edit = "edit";
        public const string Discard = "discard";
        public const string Submit = "submit";
        public const string AutoApprove = "auto-approve";
        public const string RequestApproval = "request-approval";
        public const string Approve = "approve";
        public const string Reject = "reject";
        public const string RequestChanges = "request-changes";
        public const string TransmitOk = "transmit-ok";
        public const string TransmitFailed = "transmit-failed";
        public const string Cancel = "cancel";
        public const string RecordDepotResponse = "record-depot-response";
        public const string Copy = "copy";
    }

    private static readonly Dictionary<(string From, string Action), string> Transitions = new()
    {
        [(PreOrderStatuses.Draft, Actions.Discard)] = PreOrderStatuses.Cancelled,
        [(PreOrderStatuses.Draft, Actions.AutoApprove)] = PreOrderStatuses.Approved,
        [(PreOrderStatuses.Draft, Actions.RequestApproval)] = PreOrderStatuses.PendingApproval,
        [(PreOrderStatuses.PendingApproval, Actions.Approve)] = PreOrderStatuses.Approved,
        [(PreOrderStatuses.PendingApproval, Actions.Reject)] = PreOrderStatuses.Rejected,
        [(PreOrderStatuses.PendingApproval, Actions.RequestChanges)] = PreOrderStatuses.Draft,
        [(PreOrderStatuses.Approved, Actions.TransmitOk)] = PreOrderStatuses.Transmitted,
        [(PreOrderStatuses.Approved, Actions.TransmitFailed)] = PreOrderStatuses.TransmissionFailed,
        [(PreOrderStatuses.TransmissionFailed, Actions.TransmitOk)] = PreOrderStatuses.Transmitted,
        [(PreOrderStatuses.TransmissionFailed, Actions.TransmitFailed)] = PreOrderStatuses.TransmissionFailed,
        [(PreOrderStatuses.Approved, Actions.Cancel)] = PreOrderStatuses.Cancelled,
        [(PreOrderStatuses.Transmitted, Actions.Cancel)] = PreOrderStatuses.Cancelled,
        [(PreOrderStatuses.TransmissionFailed, Actions.Cancel)] = PreOrderStatuses.Cancelled
    };

    /// <summary>Only the draft is edited (lines, depot, date, note, signature) — never a pending one (DESIGN-GAP §2).</summary>
    public static bool IsEditable(string status) => status == PreOrderStatuses.Draft;

    /// <summary>Submit starts from a draft only.</summary>
    public static bool CanSubmit(string status) => status == PreOrderStatuses.Draft;

    /// <summary>A rejected or cancelled order is copied into a new draft (D6).</summary>
    public static bool CanCopy(string status) => status is PreOrderStatuses.Rejected or PreOrderStatuses.Cancelled;

    /// <summary>The depot's answer lands on a transmitted order only (it is a field, not a state).</summary>
    public static bool CanRecordDepotResponse(string status) => status == PreOrderStatuses.Transmitted;

    public static bool CanTransition(string from, string action) => Transitions.ContainsKey((from, action));

    /// <summary>Moves the order and appends the event; false (nothing changed) when the transition is not allowed.</summary>
    public static bool TryApply(PreOrder order, string action, string eventType, string? by, DateTimeOffset at,
        Dictionary<string, string?>? payload = null)
    {
        if (!Transitions.TryGetValue((order.Status, action), out var to))
        {
            return false;
        }

        order.Status = to;
        Append(order, eventType, by, at, payload);
        return true;
    }

    /// <summary>An event row without a state change (submitted, signature captured, depot response …).</summary>
    public static void Append(PreOrder order, string eventType, string? by, DateTimeOffset at, Dictionary<string, string?>? payload = null)
        => order.Events.Add(new PreOrderEvent { At = at, Type = eventType, ByUserId = by, Payload = payload ?? new Dictionary<string, string?>() });
}
