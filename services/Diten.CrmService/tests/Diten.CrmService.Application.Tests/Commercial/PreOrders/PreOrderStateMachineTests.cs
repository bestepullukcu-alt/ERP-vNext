using Diten.CrmService.Application.Features.Commercial.PreOrders;
using Diten.CrmService.Domain.Entities.Commercial;
using Xunit;
using A = Diten.CrmService.Application.Features.Commercial.PreOrders.PreOrderStateMachine.Actions;
using S = Diten.CrmService.Domain.Entities.Commercial.PreOrderStatuses;

namespace Diten.CrmService.Application.Tests.Commercial.PreOrders;

/// <summary>WP-VW-W6 W6-BE-a — MOD-0168 §12 as a table: every allowed transition lands where the diagram says and appends
/// its event; every other (status, action) pair changes nothing. The only way back is pending → draft.</summary>
public sealed class PreOrderStateMachineTests
{
    private static readonly (string From, string Action, string To)[] Allowed =
    [
        (S.Draft, A.Discard, S.Cancelled), (S.Draft, A.AutoApprove, S.Approved), (S.Draft, A.RequestApproval, S.PendingApproval),
        (S.PendingApproval, A.Approve, S.Approved), (S.PendingApproval, A.Reject, S.Rejected), (S.PendingApproval, A.RequestChanges, S.Draft),
        (S.Approved, A.TransmitOk, S.Transmitted), (S.Approved, A.TransmitFailed, S.TransmissionFailed),
        (S.TransmissionFailed, A.TransmitOk, S.Transmitted), (S.TransmissionFailed, A.TransmitFailed, S.TransmissionFailed),
        (S.Approved, A.Cancel, S.Cancelled), (S.Transmitted, A.Cancel, S.Cancelled), (S.TransmissionFailed, A.Cancel, S.Cancelled)
    ];

    private static readonly string[] Actions =
        [A.Discard, A.AutoApprove, A.RequestApproval, A.Approve, A.Reject, A.RequestChanges, A.TransmitOk, A.TransmitFailed, A.Cancel];

    public static TheoryData<string, string> Every()
    {
        var data = new TheoryData<string, string>();
        foreach (var status in S.All)
        {
            foreach (var action in Actions)
            {
                data.Add(status, action);
            }
        }

        return data;
    }

    [Theory]
    [MemberData(nameof(Every))]
    public void T1_an_allowed_transition_moves_and_logs_and_any_other_changes_nothing(string from, string action)
    {
        var order = new PreOrder { Status = from };
        var expected = Allowed.Where(t => t.From == from && t.Action == action).Select(t => t.To).SingleOrDefault();

        var moved = PreOrderStateMachine.TryApply(order, action, "evt", "u", DateTimeOffset.UnixEpoch);

        if (expected is null)
        {
            Assert.False(moved);
            Assert.Equal(from, order.Status);
            Assert.Empty(order.Events);
        }
        else
        {
            Assert.True(moved);
            Assert.Equal(expected, order.Status);
            Assert.Equal("evt", Assert.Single(order.Events).Type);
        }
    }

    [Fact]
    public void T2_rejected_and_cancelled_are_final_only_a_draft_is_edited_and_the_depot_answer_needs_a_transmitted_order()
    {
        Assert.All(Actions, a => Assert.False(PreOrderStateMachine.CanTransition(S.Rejected, a)));
        Assert.All(Actions, a => Assert.False(PreOrderStateMachine.CanTransition(S.Cancelled, a)));
        Assert.Equal(new[] { S.Draft }, S.All.Where(PreOrderStateMachine.IsEditable));
        Assert.Equal(new[] { S.Rejected, S.Cancelled }, S.All.Where(PreOrderStateMachine.CanCopy));
        Assert.Equal(new[] { S.Transmitted }, S.All.Where(PreOrderStateMachine.CanRecordDepotResponse));
        Assert.Equal(new[] { (S.PendingApproval, A.RequestChanges) }, Allowed.Where(t => t.To == S.Draft).Select(t => (t.From, t.Action)));
        Assert.All(S.All, s => Assert.Equal(s == S.PendingApproval, PreOrderStateMachine.CanTransition(s, A.RequestChanges)));
    }
}
