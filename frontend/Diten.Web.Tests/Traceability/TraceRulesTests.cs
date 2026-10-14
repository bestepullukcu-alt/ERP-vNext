using Diten.Web.Models.Inventory;
using Diten.Web.Models.Traceability;
using Microsoft.AspNetCore.Http;
using Microsoft.Extensions.Primitives;
using Xunit;

namespace Diten.Web.Tests.Traceability;

/// <summary>
/// MOD-0174 P3 — the pure rules behind the screens (pack §11.4, §12, K-B, K-C, K-E, K-F, CT-D4): the Stock Ledger lot picker,
/// the GS1 answer (GTIN-null warning), the serial range, the lot form field rules, the status options, the list adapters,
/// and the FU01 lot normalisation that must keep a registered lot id intact.
/// </summary>
public sealed class TraceRulesTests
{
    private static readonly DateOnly Today = new(2026, 10, 6);

    private static LotRefDto Lot(string number, string status, string? expiry = null, string? retest = null, bool? retestDue = null) => new()
    {
        LotId = Guid.NewGuid().ToString("D"), LotNumber = number, Status = status,
        ExpiryDate = expiry is null ? null : expiry + "T00:00:00Z", RetestDate = retest is null ? null : retest + "T00:00:00Z", RetestDue = retestDue
    };

    // ── lot picker (pack §11.4, CT-D4, K-F) ──────────────────────────────────────────────────────────────

    [Fact]
    public void Blocked_and_Expired_lots_are_listed_but_disabled_with_their_reason()
    {
        var options = LotFieldRules.PickerOptions(
        [
            Lot("L-REL", "Released", "2027-01-01"), Lot("L-BLK", "Blocked", "2027-02-01"), Lot("L-EXP", "Expired", "2026-01-01"),
            Lot("L-DRAFT-PAST", "Draft", "2026-10-05"), Lot("L-DRAFT", "Draft")
        ], Today);

        Assert.Equal(5, options.Count);
        Assert.False(options.Single(o => o.LotNumber == "L-REL").Disabled);
        Assert.False(options.Single(o => o.LotNumber == "L-DRAFT").Disabled); // ASSUMPTION-0174-07: a Draft lot takes stock
        Assert.Equal("Lot.BlockedDisabled", options.Single(o => o.LotNumber == "L-BLK").DisabledReasonKey);
        Assert.True(options.Single(o => o.LotNumber == "L-BLK").Disabled);
        Assert.Equal("Lot.ExpiredDisabled", options.Single(o => o.LotNumber == "L-EXP").DisabledReasonKey);
        // Expired by DATE before the sweeper ran: disabled too (the posting would be LOT_EXPIRED).
        Assert.True(options.Single(o => o.LotNumber == "L-DRAFT-PAST").Disabled);
    }

    [Fact]
    public void The_picker_is_sorted_by_SKT_then_lot_number_and_flags_a_passed_retest()
    {
        var options = LotFieldRules.PickerOptions(
            [Lot("B", "Released", "2027-05-01"), Lot("A", "Released"), Lot("C", "Released", "2027-01-01", retest: "2026-09-01")], Today);

        Assert.Equal(["C", "B", "A"], options.Select(o => o.LotNumber));
        Assert.True(options[0].RetestDue);  // K-F: warning + explicit confirmation on selection
        Assert.False(options[1].RetestDue);
    }

    // ── GS1 (K-B) ────────────────────────────────────────────────────────────────────────────────────

    [Fact]
    public void A_GTIN_null_answer_is_accepted_with_the_warning_and_lot_and_expiry_still_checked()
    {
        var answer = LotFieldRules.Gs1(new Gs1CheckResultDto
        {
            Accepted = true, SerialNumber = "sn000001", GtinSkippedReason = "PRODUCT_MASTER_GTIN_NULL",
            Checks = [new() { Field = "Gtin", Result = "Skipped" }, new() { Field = "Lot", Result = "Ok" }, new() { Field = "Expiry", Result = "Ok" }]
        });
        Assert.True(answer.Accepted);
        Assert.Equal("SN000001", answer.SerialNumber);
        Assert.True(answer.GtinSkipped);
        Assert.Equal("Serial.GtinSkippedWarning", answer.WarningKey);
    }

    [Fact]
    public void A_code_with_another_lot_is_not_added_even_when_the_GTIN_is_null_and_names_the_field()
    {
        var answer = LotFieldRules.Gs1(new Gs1CheckResultDto
        {
            Accepted = false, SerialNumber = "SN2", GtinSkippedReason = "PRODUCT_MASTER_GTIN_NULL",
            Checks = [new() { Field = "Gtin", Result = "Skipped" }, new() { Field = "Lot", Result = "Mismatch" }, new() { Field = "Expiry", Result = "Mismatch" }]
        });
        Assert.False(answer.Accepted);
        Assert.Null(answer.SerialNumber);
        Assert.Equal(["Serial.Mismatch.Lot", "Serial.Mismatch.Expiry"], answer.MismatchKeys);
        Assert.Equal("Serial.GtinSkippedWarning", answer.WarningKey); // the warning stays visible on a refusal too
    }

    [Fact]
    public void With_a_GTIN_present_there_is_no_warning_and_a_GTIN_mismatch_is_named()
    {
        var answer = LotFieldRules.Gs1(new Gs1CheckResultDto
        {
            Accepted = false, SerialNumber = "SN3",
            Checks = [new() { Field = "Gtin", Result = "Mismatch" }, new() { Field = "Lot", Result = "Ok" }]
        });
        Assert.False(answer.GtinSkipped);
        Assert.Null(answer.WarningKey);
        Assert.Equal(["Serial.Mismatch.Gtin"], answer.MismatchKeys);
    }

    // ── serial range (K-B) ──────────────────────────────────────────────────────────────────────────

    [Theory]
    [InlineData("SN-0001", "SN-0003", null, 3)]
    [InlineData("sn-0098", "SN-0102", null, 5)]
    [InlineData("SN-0001", "AB-0003", "Serial.RangePrefix", 0)]
    [InlineData("SN-001", "SN-0003", "Serial.RangePrefix", 0)]
    [InlineData("SN-0005", "SN-0001", "Serial.RangeOrder", 0)]
    [InlineData("SN-00001", "SN-05001", "Serial.RangeMax", 0)]
    [InlineData("S1", "S9", "Serial.BadFormat", 0)]
    public void A_range_keeps_prefix_and_digits_and_holds_at_most_5000(string start, string end, string? error, int count)
    {
        var (serials, key) = LotFieldRules.ExpandRange(start, end);
        Assert.Equal(error, key);
        Assert.Equal(count, serials.Count);
        if (count > 0) Assert.Equal(start.ToUpperInvariant(), serials[0]);
    }

    [Fact]
    public void Exactly_5000_is_allowed()
    {
        var (serials, key) = LotFieldRules.ExpandRange("SN-00001", "SN-05000");
        Assert.Null(key);
        Assert.Equal(5000, serials.Count);
        Assert.Equal("SN-05000", serials[^1]);
    }

    // ── lot form (pack §12, K-C) ────────────────────────────────────────────────────────────────────

    private static LotFormInput Create(string? lot = "L-2026/01", string? mfg = "2026-01-10", string? exp = "2028-01-10", string? retest = null) => new()
    {
        ItemId = Guid.NewGuid().ToString(), Sku = "Lsku:" + Guid.NewGuid(), LotNumber = lot, ManufacturingDate = mfg, ExpiryDate = exp, RetestDate = retest
    };

    [Fact]
    public void A_create_needs_product_SKU_and_a_W_UI_02_shaped_lot_number()
    {
        Assert.Empty(LotFormRules.Check(Create(), create: true, null, Today));
        Assert.Equal("Validation.LotFormat", LotFormRules.Check(Create("LOT 1"), true, null, Today)["lotNumber"]);
        Assert.Equal("Validation.Required", LotFormRules.Check(Create(""), true, null, Today)["lotNumber"]);
        Assert.Equal("Validation.Required", LotFormRules.Check(Create() with { Sku = "Lsku:x" }, true, null, Today)["sku"]);
        Assert.Equal("L-2026/01", LotFormRules.NormalizeLotNumber(" l-2026/01 "));
    }

    [Fact]
    public void The_dates_follow_pack_12_rows_5_to_7()
    {
        Assert.Equal("Validation.DatesOrder", LotFormRules.Check(Create(mfg: "2026-10-07"), true, null, Today)["manufacturingDate"]);
        Assert.Equal("Validation.DatesOrder", LotFormRules.Check(Create(exp: "2026-01-10"), true, null, Today)["expiryDate"]);
        Assert.Equal("Validation.DatesOrder", LotFormRules.Check(Create(retest: "2029-01-01"), true, null, Today)["retestDate"]);
        Assert.Empty(LotFormRules.Check(Create(retest: "2027-01-10"), true, null, Today));
    }

    [Theory]
    [InlineData("Draft", null, false)]
    [InlineData("Released", "123456789", true)]
    [InlineData("Released", "QA kararı onay", false)]
    [InlineData("Blocked", "", true)]
    [InlineData("Expired", "short", true)]
    public void K_C_a_non_Draft_edit_needs_a_reason_of_ten(string status, string? reason, bool refused)
    {
        var errors = LotFormRules.Check(new LotFormInput { Reason = reason, CoaReference = "AR-1" }, create: false, status, Today);
        Assert.Equal(refused, errors.ContainsKey("reason"));
    }

    [Theory]
    [InlineData("Draft", "Released")]
    [InlineData("Released", "Blocked")]
    [InlineData("Blocked", "Released")]
    public void K_E_the_dialog_allows_exactly_the_three_transitions_and_never_Expired(string from, string allowed)
    {
        var options = LotFormRules.StatusOptions(from);
        Assert.Equal([allowed], options.Where(o => o.Allowed).Select(o => o.Status));
        Assert.Equal("Status.Dialog.SystemOnly", options.Single(o => o.Status == "Expired").ReasonKey);
        Assert.Equal(allowed == "Blocked", options.Single(o => o.Status == allowed).Danger);
    }

    [Fact]
    public void An_Expired_lot_has_no_allowed_target()
        => Assert.DoesNotContain(LotFormRules.StatusOptions("Expired"), o => o.Allowed);

    // ── list adapters ───────────────────────────────────────────────────────────────────────────────

    private static IQueryCollection Query(params (string Key, string Value)[] pairs) =>
        new QueryCollection(pairs.GroupBy(p => p.Key).ToDictionary(g => g.Key, g => new StringValues(g.Select(p => p.Value).ToArray())));

    [Fact]
    public void The_lot_list_translates_paging_filters_and_sort_and_always_asks_for_the_stock()
    {
        var item = Guid.NewGuid();
        var t = TraceListAdapter.LotsQuery(Query(("start", "20"), ("length", "10"), ("search", "L-7"), ("itemId", item.ToString()),
            ("status", "Blocked"), ("expiryBefore", "2027-01-01"), ("warn", "true"), ("orderBy", "expiryDate"), ("orderDir", "desc")));
        Assert.True(t.IsValid);
        Assert.Equal($"?page=3&pageSize=10&q=L-7&itemId={item:D}&status=Blocked&expiryBefore=2027-01-01&warn=true&sort=-expiryDate&includeStock=true", t.Query);
    }

    [Theory]
    [InlineData("status", "Archived")]
    [InlineData("expiryBefore", "01/01/2027")]
    [InlineData("orderBy", "status")]
    [InlineData("itemId", "x")]
    [InlineData("length", "500")]
    public void A_value_outside_the_contract_is_refused_before_the_gateway(string key, string value)
        => Assert.False(TraceListAdapter.LotsQuery(Query(new[] { ("start", "0"), ("length", "10") }.Where(p => p.Item1 != key).Append((key, value)).ToArray())).IsValid);

    [Fact]
    public void The_serial_list_translates_sku_lot_and_status()
    {
        var sku = Guid.NewGuid();
        var lot = Guid.NewGuid();
        var t = TraceListAdapter.SerialsQuery(Query(("start", "15"), ("length", "15"), ("sku", $"Gsku:{sku}"), ("lotId", lot.ToString()), ("status", "Consumed")));
        Assert.Equal($"?page=2&pageSize=15&skuId={sku:D}&skuLevel=Gsku&lotId={lot:D}&status=Consumed", t.Query);
        Assert.False(TraceListAdapter.SerialsQuery(Query(("status", "Lost"))).IsValid);
    }

    // ── the FU01 lot normalisation (pack §11.4) ─────────────────────────────────────────────────────

    [Fact]
    public void A_registered_lot_id_passes_the_movement_form_unchanged_and_a_typed_lot_keeps_W_UI_02()
    {
        var id = Guid.NewGuid().ToString("D");
        Assert.True(MovementRules.TryNormalizeLot(id, out var lot));
        Assert.Equal(id, lot);
        Assert.True(MovementRules.IsRegisteredLotId(id));
        Assert.False(MovementRules.IsRegisteredLotId(id.ToUpperInvariant()));
        Assert.True(MovementRules.TryNormalizeLot(" lot-1 ", out var typed));
        Assert.Equal("LOT-1", typed);
    }
}
