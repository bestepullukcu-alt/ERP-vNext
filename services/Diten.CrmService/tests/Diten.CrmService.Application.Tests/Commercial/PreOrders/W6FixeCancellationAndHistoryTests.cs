using Diten.CrmService.Application.Features.Commercial.PreOrders;
using Diten.CrmService.Application.Features.Commercial.PreOrders.Commands;
using Diten.CrmService.Application.Features.Commercial.PreOrders.Queries;
using Diten.CrmService.Application.Tests.Commercial.Pricing;
using Diten.CrmService.Domain.Entities.Commercial;
using Xunit;

namespace Diten.CrmService.Application.Tests.Commercial.PreOrders;

/// <summary>
/// WP-VW-W6 W6-FIX-e — (G-15) a cancellation records the depot / the pharmacy as notified only when the order had been
/// transmitted AND that party has an e-mail (the notifier's own rule), and the mail goes exactly to those; (G-18) the
/// approval screen's pharmacy history leaves the order under decision out of the rows and the months, while other pending
/// orders stay and cancelled / rejected ones stay out of the months. Runs the PRODUCTION notifier over the fake Platform.
/// </summary>
public sealed class W6FixeCancellationAndHistoryTests : PreOrderTestBase
{
    private static readonly PreOrderMailTests.DedupingPlatform Platform = new();

    public W6FixeCancellationAndHistoryTests() : base(PreOrderMailTests.Wire(Platform, new PreOrderMailTests.FakeDocuments()))
    {
        Platform.Reset();
    }

    private async Task<PreOrderCancellation> CancelTransmitted(Action<PreOrder>? withoutEmail = null)
    {
        var transmitted = await Approved();
        Assert.Equal(PreOrderStatuses.Transmitted, transmitted.Status);
        withoutEmail?.Invoke(W.Orders.Store.Single(o => o.Id == transmitted.Id));
        Platform.Reset();
        Ok(await W.Send(new CancelPreOrderCommand(transmitted.Id, new PreOrderCancelInput("Eczane kapandı")), PreOrderWorld.Rep));
        return W.Stored(transmitted.Id).Cancellation!;
    }

    private static List<string?> ExternalMails()
        => Platform.Requests.Where(r => r.EventCode == PreOrderNotificationEvents.Cancelled)
            .SelectMany(r => r.To).Where(t => t.Email is not null).Select(t => t.Email).ToList();

    // ── G-15: "notified" only when the mail goes ─────────────────────────────────────────────────────────────────

    [Fact]
    public async Task N1_a_transmitted_order_with_both_e_mails_records_both_notified_and_mails_both()
    {
        var c = await CancelTransmitted();

        Assert.Equal((true, true), (c.NotifiedDepot, c.NotifiedPharmacy));
        Assert.Equal(new[] { "siparis@selcuk.test", "info@sifa.test" }, ExternalMails());
    }

    [Fact]
    public async Task N2_a_pharmacy_without_an_e_mail_is_not_recorded_as_notified_the_depot_still_is()
    {
        var c = await CancelTransmitted(o => o.Pharmacy.Email = "  ");

        Assert.Equal((true, false), (c.NotifiedDepot, c.NotifiedPharmacy));
        Assert.Equal(new[] { "siparis@selcuk.test" }, ExternalMails());
    }

    [Fact]
    public async Task N3_a_depot_without_an_e_mail_is_not_recorded_as_notified_and_the_pharmacy_still_gets_its_mail()
    {
        var c = await CancelTransmitted(o => o.Depot!.Email = null);

        Assert.Equal((false, true), (c.NotifiedDepot, c.NotifiedPharmacy));
        Assert.Equal(new[] { "info@sifa.test" }, ExternalMails());
    }

    [Fact]
    public async Task N4_an_order_never_transmitted_notifies_nobody_outside_even_with_both_e_mails()
    {
        Platform.FailEvent = PreOrderNotificationEvents.TransmittedDepot;
        var failed = await Approved();
        Assert.NotEqual(PreOrderStatuses.Transmitted, W.Stored(failed.Id).Status);
        Platform.Reset();

        Ok(await W.Send(new CancelPreOrderCommand(failed.Id, new PreOrderCancelInput("Yanlış depo")), PreOrderWorld.Rep));

        Assert.Equal((false, false), (W.Stored(failed.Id).Cancellation!.NotifiedDepot, W.Stored(failed.Id).Cancellation!.NotifiedPharmacy));
        Assert.Empty(ExternalMails());
    }

    [Fact]
    public void N5_the_e_mail_rule_is_one_non_blank_and_trimmed()
    {
        Assert.Null(PreOrderExternalEmail.Of(null));
        Assert.Null(PreOrderExternalEmail.Of(" \t"));
        Assert.Equal("a@b.test", PreOrderExternalEmail.Of(" a@b.test "));
    }

    // ── G-18: the order under decision is not its own history ────────────────────────────────────────────────────

    [Fact]
    public async Task H1_the_history_leaves_out_the_order_under_decision_rows_and_months_and_keeps_another_pending_one()
    {
        var other = await Pending();                                   // another pending order of the same pharmacy
        var transmitted = await Approved();
        var rejected = await Pending();
        Ok(await Decide(rejected.Id, PreOrderDecisions.Reject, "hayır"));
        var looked = await Pending();

        var history = Ok(await W.Send(new GetPharmacyOrderHistoryQuery(looked.Id), PreOrderWorld.Manager2));

        var ids = history.Items.Select(i => i.Id).ToList();
        Assert.DoesNotContain(looked.Id, ids);                        // not in the rows
        Assert.Contains(other.Id, ids);                               // another pending order stays, with its status
        Assert.Equal(PreOrderStatuses.PendingApproval, history.Items.Single(i => i.Id == other.Id).Status);
        Assert.Contains(transmitted.Id, ids);
        Assert.Contains(rejected.Id, ids);                            // listed with its status …
        var month = history.Months!.Last();
        Assert.Equal(2, month.Orders);                                // … but the month counts only the other pending + the transmitted one
        Assert.Equal(W.Stored(other.Id).Lines.Sum(l => l.Quantity) + W.Stored(transmitted.Id).Lines.Sum(l => l.Quantity), month.Boxes);
    }

    [Fact]
    public async Task H2_alone_the_order_under_decision_leaves_an_empty_six_months()
    {
        var looked = await Pending();

        var history = Ok(await W.Send(new GetPharmacyOrderHistoryQuery(looked.Id), PreOrderWorld.Manager));

        Assert.Empty(history.Items);
        Assert.Equal(6, history.Months!.Count);
        Assert.All(history.Months!, m => Assert.Equal((0, 0, (decimal?)null), (m.Orders, m.Boxes, m.EffectiveDiscountPct)));
    }
}
