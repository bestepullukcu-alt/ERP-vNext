using Diten.CrmService.Application.Features.Commercial.PreOrders;
using Diten.CrmService.Application.Features.Commercial.PreOrders.Queries;
using Diten.CrmService.Application.Tests.Commercial.Pricing;
using Diten.CrmService.Domain.Entities.Commercial;
using Xunit;

namespace Diten.CrmService.Application.Tests.Commercial.PreOrders;

/// <summary>
/// WP-VW-W6 W6-FIX-d — screen C's data, computed by the CRM: (V-9) the pharmacy's six months, one bucket per month with an
/// empty month included, the gross per currency, a cancelled order outside, and the effective discount as the net-weighted
/// average of the month's PRICED orders (a quantity order never enters it, even one that carries totals; orders in another
/// currency weigh by their local net); (V-10) the approvers' seat NAME written at submission, null on an older order and
/// never resolved on read.
/// </summary>
public sealed class W6FixdHistoryAndApproverTests : PreOrderTestBase
{
    private int _no;

    /// <summary>A submitted order of the pharmacy, written straight into the store (the history reads what is stored).</summary>
    private void Seed(string at, string status, string mode, string? currency, decimal net, decimal gross, decimal effective, int boxes,
        decimal? fxRate = null, bool totals = true, int submission = 1)
        => W.Orders.Store.Add(new PreOrder
        {
            TenantId = PreOrderWorld.Tenant, Code = "MG-ORD-9" + (++_no).ToString("00000"), Status = status, PriceMode = mode, Currency = currency,
            LocalCurrencyCode = "TRY", CountryCode = "TR", SubmissionNo = submission, CreatedAt = DateTimeOffset.Parse(at + "T08:00:00Z"),
            Pharmacy = new PreOrderParty { AccountId = PreOrderWorld.Pharmacy, Name = "1 ETAP ECZANESİ" },
            Rep = new PreOrderRep { UserId = PreOrderWorld.OtherRep },
            Lines = [new PreOrderLine { LineNo = 1, SkuId = PricingWorld.Sku("TUT"), Quantity = boxes }],
            Totals = totals ? new PreOrderTotals { Net = net, Gross = gross, EffectiveDiscountPct = effective } : null,
            Fx = fxRate is { } rate ? new PreOrderFxSnapshot { LocalCurrency = "TRY", Rate = rate } : null
        });

    /// <summary>The order the approver looks at, moved out of the six months so only the seeded orders are counted.</summary>
    private async Task<PreOrderDto> Looked()
    {
        var pending = await Pending();
        W.Orders.Store.Single(o => o.Id == pending.Id).CreatedAt = DateTimeOffset.Parse("2026-01-02T08:00:00Z");
        return pending;
    }

    [Fact]
    public async Task H1_six_months_oldest_first_gross_per_currency_effective_net_weighted_quantity_and_cancelled_outside_empty_month_present()
    {
        var looked = await Looked();
        Seed("2026-10-02", PreOrderStatuses.Transmitted, PreOrderPriceModes.Priced, "TRY", net: 1000m, gross: 1180m, effective: 10m, boxes: 10);
        Seed("2026-10-05", PreOrderStatuses.Approved, PreOrderPriceModes.Priced, "USD", net: 100m, gross: 118m, effective: 20m, boxes: 5, fxRate: 40m); // local net 4 000
        Seed("2026-10-06", PreOrderStatuses.Transmitted, PreOrderPriceModes.Quantity, "TRY", net: 500m, gross: 0m, effective: 50m, boxes: 7);       // totals, yet no prices
        Seed("2026-10-07", PreOrderStatuses.Cancelled, PreOrderPriceModes.Priced, "TRY", net: 9999m, gross: 9999m, effective: 90m, boxes: 99);
        Seed("2026-10-08", PreOrderStatuses.Transmitted, PreOrderPriceModes.Priced, "EUR", net: 200m, gross: 236m, effective: 30m, boxes: 2);        // no FX snapshot
        Seed("2026-10-09", PreOrderStatuses.Draft, PreOrderPriceModes.Priced, "TRY", net: 7m, gross: 7m, effective: 70m, boxes: 70, submission: 0); // never submitted
        Seed("2026-08-14", PreOrderStatuses.Transmitted, PreOrderPriceModes.Priced, "TRY", net: 300m, gross: 354m, effective: 5m, boxes: 3);
        Seed("2026-05-03", PreOrderStatuses.Transmitted, PreOrderPriceModes.Quantity, null, net: 0m, gross: 0m, effective: 0m, boxes: 4, totals: false);
        Seed("2026-04-25", PreOrderStatuses.Transmitted, PreOrderPriceModes.Priced, "TRY", net: 100m, gross: 118m, effective: 1m, boxes: 1);         // in the window, before the first month

        var history = Ok(await W.Send(new GetPharmacyOrderHistoryQuery(looked.Id), PreOrderWorld.Manager2));
        var months = history.Months!;

        Assert.Equal(new[] { "2026-05", "2026-06", "2026-07", "2026-08", "2026-09", "2026-10" }, months.Select(m => m.Month));
        var october = months[5];
        Assert.Equal((4, 24), (october.Orders, october.Boxes));                                    // the cancelled one and the draft are not in it
        Assert.Equal(new[] { ("EUR", 236m), ("TRY", 1180m), ("USD", 118m) }, october.Gross.Select(g => (g.Currency, g.Amount)));
        Assert.Equal(18m, october.EffectiveDiscountPct);                                            // (10 × 1 000 + 20 × 4 000) / 5 000
        Assert.Equal((1, (decimal?)5m), (months[3].Orders, months[3].EffectiveDiscountPct));        // August
        Assert.Equal((0, 0, (decimal?)null), (months[4].Orders, months[4].Boxes, months[4].EffectiveDiscountPct)); // September: empty, still a month
        Assert.Empty(months[4].Gross);
        Assert.Equal((1, 4, (decimal?)null), (months[0].Orders, months[0].Boxes, months[0].EffectiveDiscountPct)); // May: quantity only → no %
        Assert.Empty(months[0].Gross);

        var items = history.Items.ToDictionary(i => i.Code);
        Assert.Equal(10m, items["MG-ORD-900001"].EffectiveDiscountPct);
        Assert.Null(items["MG-ORD-900003"].EffectiveDiscountPct);                                   // a quantity order has no effective discount
        Assert.DoesNotContain("MG-ORD-900006", items.Keys);                                         // a draft is not history
    }

    [Fact]
    public void H2_the_effective_discount_weighs_by_net_not_by_count()
    {
        var small = new PreOrder { PriceMode = PreOrderPriceModes.Priced, Currency = "TRY", LocalCurrencyCode = "TRY", Totals = new PreOrderTotals { Net = 100m, EffectiveDiscountPct = 40m } };
        var large = new PreOrder { PriceMode = PreOrderPriceModes.Priced, Currency = "TRY", LocalCurrencyCode = "TRY", Totals = new PreOrderTotals { Net = 900m, EffectiveDiscountPct = 10m } };

        Assert.Equal(13m, PreOrderPharmacyHistory.EffectiveDiscount([small, large]));               // (40 × 100 + 10 × 900) / 1 000, not (40 + 10) / 2
        Assert.Null(PreOrderPharmacyHistory.EffectiveDiscount([]));
    }

    [Fact]
    public async Task H3_the_history_is_still_only_for_the_approver_or_read_all()
    {
        var looked = await Looked();

        Assert.Equal(404, (await W.Send(new GetPharmacyOrderHistoryQuery(looked.Id), PreOrderWorld.Rep)).StatusCode);
        Assert.Equal(404, (await W.Send(new GetPharmacyOrderHistoryQuery(looked.Id), PreOrderWorld.Stranger)).StatusCode);
        Assert.Equal(6, Ok(await W.Send(new GetPharmacyOrderHistoryQuery(looked.Id), PreOrderWorld.Ops)).Months!.Count);
    }

    [Fact]
    public async Task A1_the_approvers_seat_name_is_written_at_submission_beside_its_code()
    {
        var pending = await Pending();

        Assert.Equal(("TR-AREA-MANAGER", "Bölge Müdürü"), (pending.ApprovalRequirement!.ApproverPositionCode, pending.ApprovalRequirement.ApproverPositionName));
        Assert.Equal("Bölge Müdürü", W.Orders.Peek(pending.Id).ApprovalRequirement!.ApproverPositionName); // in the stored snapshot
    }

    [Fact]
    public async Task A2_an_order_submitted_before_has_no_name_and_reading_it_neither_resolves_nor_writes_one()
    {
        var pending = await Pending();
        var stored = W.Orders.Store.Single(o => o.Id == pending.Id);
        stored.ApprovalRequirement!.ApproverPositionName = null;                                     // as an order submitted before W6-FIX-d
        var calls = W.Managers.Calls;
        var version = stored.Version;

        var read = Ok(await W.Send(new GetPreOrderByIdQuery(pending.Id), PreOrderWorld.Manager));

        Assert.Equal(("TR-AREA-MANAGER", (string?)null), (read.ApprovalRequirement!.ApproverPositionCode, read.ApprovalRequirement.ApproverPositionName));
        Assert.Equal(calls, W.Managers.Calls);                                                     // nobody was asked
        Assert.Equal(((string?)null, version), (W.Orders.Peek(pending.Id).ApprovalRequirement!.ApproverPositionName, W.Orders.Peek(pending.Id).Version));
    }
}
