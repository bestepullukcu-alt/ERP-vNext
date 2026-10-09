using Diten.InventoryService.Domain.Enums;
using Diten.InventoryService.Domain.Traceability.Enums;
using Diten.InventoryService.Domain.Traceability.Rules;
using Xunit;

namespace Diten.InventoryService.Tests.Trace;

/// <summary>
/// MOD-0174 domain rules, clock-free (pack §17 "Domain unit tests"): <see cref="LotIdentityPolicy"/> (K-G),
/// <see cref="LotStatusRules"/> (K-E, B-3, CT-D8), <see cref="LotDateRules"/> (pack §12 rows 5–7, warnings),
/// <see cref="Gs1CodeParser"/> (K-B: bracketed + raw FNC1, century rule, DD = 00).
/// </summary>
public sealed class TraceDomainRulesTests
{
    private static readonly DateOnly Today = new(2026, 10, 5);

    // ── K-G ──────────────────────────────────────────────────────────────────────────────────────────────

    [Fact]
    public void The_lot_key_is_tenant_plus_sku_plus_normalised_lot_number_so_the_same_number_differs_per_sku()
    {
        var tenant = Guid.NewGuid();
        var skuA = Guid.NewGuid();
        var skuB = Guid.NewGuid();

        Assert.Equal(LotIdentityPolicy.LotKey(tenant, skuA, SkuLevel.Lsku, " prs-26a014 "), LotIdentityPolicy.LotKey(tenant, skuA, SkuLevel.Lsku, "PRS-26A014"));
        Assert.NotEqual(LotIdentityPolicy.LotKey(tenant, skuA, SkuLevel.Lsku, "PRS-26A014"), LotIdentityPolicy.LotKey(tenant, skuB, SkuLevel.Lsku, "PRS-26A014"));
        Assert.NotEqual(LotIdentityPolicy.LotKey(tenant, skuA, SkuLevel.Lsku, "PRS-26A014"), LotIdentityPolicy.LotKey(tenant, skuA, SkuLevel.Gsku, "PRS-26A014"));
        Assert.NotEqual(LotIdentityPolicy.LotKey(tenant, skuA, SkuLevel.Lsku, "X"), LotIdentityPolicy.LotKey(Guid.NewGuid(), skuA, SkuLevel.Lsku, "X"));
        // WP-0174-ITEM (TRACE 1.3.0): a lot may also bind to an Item-level SKU (skuId == itemId)
        Assert.Equal([SkuLevel.Gsku, SkuLevel.Lsku, SkuLevel.FinishedGood, SkuLevel.Item], LotIdentityPolicy.AllowedSkuLevels);
    }

    [Theory]
    [InlineData("PRS-26A014", true)]
    [InlineData("L2026/0455.1", true)]
    [InlineData("MCC 2602", false)]
    [InlineData("", false)]
    [InlineData("ÇAĞ-1", false)]
    public void Lot_number_shape_is_1_to_40_of_A_Z_0_9_dot_slash_dash(string normalised, bool valid)
        => Assert.Equal(valid, LotIdentityPolicy.IsValidLotNumber(normalised));

    [Fact]
    public void Lot_number_longer_than_40_is_refused() => Assert.False(LotIdentityPolicy.IsValidLotNumber(new string('A', 41)));

    [Theory]
    [InlineData("SN-0001", true)]
    [InlineData("AB", false)]
    [InlineData("ABCDEFGHIJKLMNOPQRSTUVWXY", false)]
    [InlineData("SN 01", false)]
    public void Serial_shape_is_3_to_24_of_A_Z_0_9_dash(string normalised, bool valid) => Assert.Equal(valid, LotIdentityPolicy.IsValidSerial(normalised));

    // ── K-E / B-3 / CT-D8 ────────────────────────────────────────────────────────────────────────────────

    [Theory]
    [InlineData(LotStatus.Draft, LotStatus.Released, true)]
    [InlineData(LotStatus.Released, LotStatus.Blocked, true)]
    [InlineData(LotStatus.Blocked, LotStatus.Released, true)]
    [InlineData(LotStatus.Draft, LotStatus.Blocked, false)]
    [InlineData(LotStatus.Released, LotStatus.Draft, false)]
    [InlineData(LotStatus.Draft, LotStatus.Expired, false)]
    [InlineData(LotStatus.Released, LotStatus.Expired, false)]
    [InlineData(LotStatus.Blocked, LotStatus.Expired, false)]
    [InlineData(LotStatus.Expired, LotStatus.Released, false)]
    [InlineData(LotStatus.Expired, LotStatus.Blocked, false)]
    [InlineData(LotStatus.Released, LotStatus.Released, false)]
    public void Only_three_manual_transitions_exist_and_Expired_is_never_manual(LotStatus from, LotStatus to, bool allowed)
        => Assert.Equal(allowed, LotStatusRules.CanTransition(from, to));

    [Fact]
    public void Reason_is_at_least_ten_characters_after_trim_and_Blocked_needs_acknowledgement()
    {
        Assert.False(LotStatusRules.IsReasonValid("123456789"));
        Assert.False(LotStatusRules.IsReasonValid("   123456789   "));
        Assert.True(LotStatusRules.IsReasonValid("1234567890"));
        Assert.True(LotStatusRules.RequiresAcknowledgement(LotStatus.Blocked));
        Assert.False(LotStatusRules.RequiresAcknowledgement(LotStatus.Released));
        Assert.False(LotStatusRules.EditNeedsReason(LotStatus.Draft));
        Assert.All([LotStatus.Released, LotStatus.Blocked, LotStatus.Expired], s => Assert.True(LotStatusRules.EditNeedsReason(s)));
        Assert.All([LotStatus.Blocked, LotStatus.Expired], s => Assert.False(LotStatusRules.IsUsable(s)));
    }

    [Fact]
    public void A_lot_expires_when_its_expiry_date_is_before_today_in_any_status_but_Expired()
    {
        var yesterday = Today.AddDays(-1);
        Assert.All([LotStatus.Draft, LotStatus.Released, LotStatus.Blocked], s => Assert.True(LotDateRules.ShouldExpire(s, yesterday, Today)));
        Assert.False(LotDateRules.ShouldExpire(LotStatus.Expired, yesterday, Today));
        Assert.False(LotDateRules.ShouldExpire(LotStatus.Released, Today, Today));
        Assert.False(LotDateRules.ShouldExpire(LotStatus.Released, null, Today));
    }

    // ── dates ────────────────────────────────────────────────────────────────────────────────────────────

    [Fact]
    public void Date_order_rules()
    {
        Assert.Null(LotDateRules.Validate(Today.AddDays(-10), Today.AddDays(100), Today.AddDays(50), Today));
        Assert.NotNull(LotDateRules.Validate(Today.AddDays(1), null, null, Today));
        Assert.NotNull(LotDateRules.Validate(Today, Today, null, Today));
        Assert.NotNull(LotDateRules.Validate(Today.AddDays(-5), Today.AddDays(5), Today.AddDays(-6), Today));
        Assert.NotNull(LotDateRules.Validate(null, Today.AddDays(5), Today.AddDays(6), Today));
    }

    [Fact]
    public void Expiry_state_and_retest_warning()
    {
        Assert.Equal("None", LotDateRules.ExpiryState(null, Today));
        Assert.Equal("Expired", LotDateRules.ExpiryState(Today.AddDays(-1), Today));
        Assert.Equal("ExpiringSoon", LotDateRules.ExpiryState(Today, Today));
        Assert.Equal("ExpiringSoon", LotDateRules.ExpiryState(Today.AddDays(90), Today));
        Assert.Equal("Ok", LotDateRules.ExpiryState(Today.AddDays(91), Today));
        Assert.True(LotDateRules.RetestDue(Today.AddDays(-1), Today));
        Assert.False(LotDateRules.RetestDue(Today, Today));
        Assert.True(LotDateRules.HasWarning(Today.AddDays(400), Today.AddDays(-1), Today));
        Assert.False(LotDateRules.HasWarning(Today.AddDays(400), Today.AddDays(1), Today));
    }

    // ── K-B GS1 ──────────────────────────────────────────────────────────────────────────────────────────

    [Fact]
    public void Bracketed_GS1_code_reads_all_four_identifiers()
    {
        Assert.True(Gs1CodeParser.TryParse("(01)08699512340407(21)26P001005000(17)280630(10)DKS-26P001", Today, out var code));
        Assert.Equal("08699512340407", code.Gtin);
        Assert.Equal("26P001005000", code.Serial);
        Assert.Equal(new DateOnly(2028, 6, 30), code.Expiry);
        Assert.Equal("DKS-26P001", code.Lot);
    }

    [Fact]
    public void Raw_GS1_code_with_symbology_prefix_and_FNC1_separators_reads_the_same()
    {
        const char gs = '\u001D';
        Assert.True(Gs1CodeParser.TryParse("]d2010869951234040721SN0001" + gs + "17280600" + "10DKS-26P001", Today, out var code));
        Assert.Equal("08699512340407", code.Gtin);
        Assert.Equal("SN0001", code.Serial);
        Assert.Equal(new DateOnly(2028, 6, 30), code.Expiry); // DD = 00 → last day of the month
        Assert.Equal("DKS-26P001", code.Lot);
    }

    [Theory]
    [InlineData("SN-0001")]
    [InlineData("(01)123(21)X")]
    [InlineData("(21)A(21)B")]
    [InlineData("(01)08699512340407 trailing")]
    [InlineData("99ABC")]
    public void Unreadable_codes_are_refused(string raw) => Assert.False(Gs1CodeParser.TryParse(raw, Today, out _));

    [Fact]
    public void Expiry_century_follows_the_GS1_rule_and_an_impossible_date_is_null()
    {
        Assert.Equal(new DateOnly(2076, 1, 31), Gs1CodeParser.ExpiryDate("760131", Today)); // 50 years ahead: this century
        Assert.Equal(new DateOnly(1977, 1, 31), Gs1CodeParser.ExpiryDate("770131", Today)); // 51 years ahead: previous century
        Assert.Equal(new DateOnly(2029, 2, 28), Gs1CodeParser.ExpiryDate("290200", Today));
        Assert.Null(Gs1CodeParser.ExpiryDate("280230", Today));
        Assert.Null(Gs1CodeParser.ExpiryDate("281300", Today));
    }

    [Fact]
    public void Gtin_compares_as_14_digits()
    {
        Assert.Equal("08699512340407", Gs1CodeParser.Gtin14("8699512340407"));
        Assert.Equal("08699512340407", Gs1CodeParser.Gtin14("08699512340407"));
        Assert.Null(Gs1CodeParser.Gtin14("12345"));
        Assert.Null(Gs1CodeParser.Gtin14(null));
    }
}
