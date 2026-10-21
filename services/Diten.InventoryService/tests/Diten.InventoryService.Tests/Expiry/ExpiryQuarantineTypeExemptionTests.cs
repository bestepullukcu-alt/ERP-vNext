using System.Text.Json;
using System.Text.RegularExpressions;
using Diten.InventoryService.Domain.Enums;
using Diten.InventoryService.Domain.Expiry.Rules;
using Diten.InventoryService.Domain.Quarantine.Entities;
using Diten.InventoryService.Domain.Quarantine.Enums;
using Diten.InventoryService.Domain.Quarantine.Rules;
using Diten.InventoryService.Tests.Architecture;
using Diten.InventoryService.Tests.TestInfrastructure;
using Xunit;

namespace Diten.InventoryService.Tests.Expiry;

/// <summary>
/// WP-0176-F2-FIX1 (CT finding O-F2-2) — <c>sourceModule</c> no longer exempts a posting from the expiry hooks; MOD-0175 stays unaffected
/// because EVERY movement it can post is exempt by its TYPE (<see cref="ExpiryHookScope"/>), whatever statuses it carries.
/// <para>The 0175 movements are derived from the production planner (<see cref="QuarantineStockEffects"/>, every enum value of the decision
/// kinds, line decisions, hold statuses and dispositions) and from the one no-workflow call site. The completeness guard pins the measured
/// call sites of <c>QuarantineMovementPoster.PostAsync</c> (the ONLY way 0175 posts, pack §10) and the movement types named where a 0175
/// movement is built — a new call site or a new type turns it red.</para>
/// </summary>
public sealed class ExpiryQuarantineTypeExemptionTests
{
    /// <summary>Measured 2026-10-13 at 120cf7666: the two callers of <c>QuarantineMovementPoster.PostAsync</c>.</summary>
    private static readonly string[] MeasuredPosterCalls =
    [
        "Diten.InventoryService.Application/Features/Quarantine/Services/QuarantineApplyEngine.cs:111",
        "Diten.InventoryService.Application/Features/Quarantine/Handlers/StockActionHandlers.cs:153"
    ];

    /// <summary>Where a 0175 movement is built (its type is chosen there).</summary>
    private static readonly string[] MovementBuilders =
    [
        "Diten.InventoryService.Domain/Quarantine/Rules/QuarantineStockEffects.cs",
        "Diten.InventoryService.Application/Features/Quarantine/Handlers/StockActionHandlers.cs"
    ];

    private static readonly Regex TypeToken = new(@"MovementType\.(\w+)", RegexOptions.Compiled);

    private static QuarantineDecision Decision(DecisionKind kind) => new()
    {
        Kind = kind,
        WarehouseId = "wh",
        LocationId = "bin",
        AvailableQuantityAtDecision = 5m
    };

    /// <summary>Every movement the production planner can produce, over every enum value it switches on.</summary>
    private static List<PlannedMovement> PlannedByProduction()
    {
        var planned = new List<PlannedMovement>
        {
            QuarantineStockEffects.ManualQuarantine("wh", "bin", 1m, 1),
            QuarantineStockEffects.ManualBlock("wh", "bin", 1m, 1)
        };

        foreach (var from in QuarantineStockEffects.HoldStatuses)
        {
            var hold = Decision(DecisionKind.HoldDecision);
            hold.FromStockStatus = from;
            hold.Lines = Enum.GetValues<LineDecision>().Select((d, i) => new DecisionLine { LineNo = i + 1, Decision = d, Quantity = 1m }).ToList();
            planned.AddRange(QuarantineStockEffects.Plan(hold));
        }

        planned.AddRange(QuarantineStockEffects.Plan(Decision(DecisionKind.Unblock)));
        planned.AddRange(QuarantineStockEffects.Plan(Decision(DecisionKind.LotRelease)));
        foreach (var kind in Enum.GetValues<DispositionKind>())
        {
            var disposition = Decision(DecisionKind.Disposition);
            disposition.Disposition = new DecisionDisposition { Kind = kind, Quantity = 1m, SampleLocationId = "sample-bin" };
            planned.AddRange(QuarantineStockEffects.Plan(disposition));
        }

        Assert.Equal(4, Enum.GetValues<DecisionKind>().Length); // a new decision kind must be added above
        return planned;
    }

    /// <summary>The type alone exempts: even the most "issue-like" (AVAILABLE → none) and "receipt-like" (none → AVAILABLE) shapes give no hook.</summary>
    private static void AssertExemptByType(MovementType type, StockStatus? from, StockStatus? to)
    {
        Assert.True(ExpiryHookScope.For(type, from, to, "MOD-0175", hasRegisteredLot: true) == ExpiryHookKind.None, $"{type} {from}→{to} is under a hook");
        Assert.True(ExpiryHookScope.For(type, StockStatus.Available, null, "MOD-0175", hasRegisteredLot: true) == ExpiryHookKind.None, $"{type} is not exempt by TYPE (issue shape)");
        Assert.True(ExpiryHookScope.For(type, null, StockStatus.Available, "MOD-0175", hasRegisteredLot: true) == ExpiryHookKind.None, $"{type} is not exempt by TYPE (receipt shape)");
    }

    [Fact]
    public void Every_movement_MOD_0175_plans_is_exempt_by_its_type()
    {
        var planned = PlannedByProduction();
        Assert.Equal(
            new[] { MovementType.StatusTransfer, MovementType.LocationTransfer, MovementType.SupplierReturn, MovementType.Scrap }.OrderBy(t => t),
            planned.Select(p => p.MovementType).Distinct().OrderBy(t => t));
        Assert.All(planned, p => AssertExemptByType(p.MovementType, p.FromStockStatus, p.ToStockStatus));

        // the no-workflow stock actions (StockActionHandlers.cs:150) post STATUS_TRANSFER AVAILABLE → QUARANTINE / BLOCKED
        AssertExemptByType(MovementType.StatusTransfer, StockStatus.Available, StockStatus.Quarantine);
        AssertExemptByType(MovementType.StatusTransfer, StockStatus.Available, StockStatus.Blocked);
    }

    [Fact]
    public void The_MOD_0175_posting_sites_and_types_are_the_measured_ones()
    {
        var src = Path.Combine(RepoPaths.ServiceRoot, "src");

        // every caller of the poster, as path:line of the raw source (comments included, so line numbers are the editor's)
        var calls = SourceScan.Production()
            .Where(f => f.Body.Contains("_poster.PostAsync(", StringComparison.Ordinal))
            .SelectMany(f => File.ReadAllLines(Path.Combine(src, f.RelativePath))
                .Select((line, i) => (line, no: i + 1))
                .Where(x => x.line.Contains("_poster.PostAsync(", StringComparison.Ordinal))
                .Select(x => $"{f.RelativePath}:{x.no}"))
            .OrderBy(x => x, StringComparer.Ordinal)
            .ToList();
        Assert.Equal(MeasuredPosterCalls.OrderBy(x => x, StringComparer.Ordinal), calls);

        // nothing else in 0175 reaches the ledger: the poster is the only user of the internal posting service outside the ledger
        var internalPosting = SourceScan.Production()
            .Where(f => f.Body.Contains("InternalMovementPostingService", StringComparison.Ordinal)
                        && !f.RelativePath.Contains("/Features/Inventory/", StringComparison.Ordinal)
                        && !f.RelativePath.EndsWith("/Program.cs", StringComparison.Ordinal))
            .Select(f => f.RelativePath)
            .ToList();
        Assert.Equal(["Diten.InventoryService.Application/Features/Quarantine/Services/QuarantineMovementPoster.cs"], internalPosting);

        // every PlannedMovement is built in the measured files, and every movement type named there is exempt by TYPE
        var builders = SourceScan.Production()
            .Where(f => f.Body.Contains("new PlannedMovement(", StringComparison.Ordinal))
            .Select(f => f.RelativePath)
            .OrderBy(x => x, StringComparer.Ordinal)
            .ToList();
        Assert.Equal(MovementBuilders.OrderBy(x => x, StringComparer.Ordinal), builders);
        var named = SourceScan.Production()
            .Where(f => MovementBuilders.Contains(f.RelativePath))
            .SelectMany(f => TypeToken.Matches(f.Body).Select(m => Enum.Parse<MovementType>(m.Groups[1].Value)))
            .Distinct()
            .ToList();
        Assert.NotEmpty(named);
        Assert.All(named, t => AssertExemptByType(t, null, null));
    }
}

/// <summary>
/// WP-0176-F2-FIX1 3(d) — the REAL MOD-0175 flows (no-workflow block, approved Destroy disposition) through the production host with the
/// expiry hooks on, on a LATER lot while an earlier lot is eligible: no FEFO check, no snapshot, no exception (no regression).
/// </summary>
public sealed class ExpiryQuarantineFlowRegressionTests : IClassFixture<QuarantineApiFactory>
{
    private readonly QuarantineApiFactory _factory;

    public ExpiryQuarantineFlowRegressionTests(QuarantineApiFactory factory) => _factory = factory;

    private static string Id(JsonElement e, string name) => e.GetProperty(name).GetString()!;

    [Fact]
    public async Task FIX1_d_blocking_and_destroying_a_later_lot_passes_without_a_FEFO_check()
    {
        var s = new QuarantineScenario(_factory);
        var early = await s.Lots.CreateLotAsync(expiryDate: "2027-01-31T00:00:00Z");
        await s.Lots.ReleaseAsync(early);
        await s.ReceiveAsync("10", early, "AVAILABLE");
        var late = await s.ReleasedLotAsync(); // expiry 2028-01-31
        await s.ReceiveAsync("30", late, "AVAILABLE");

        // a plain issue of the later lot IS checked (the hooks are on in this host)
        Assert.Equal("FEFO_BYPASS_REASON_REQUIRED", (await s.Lots.PostAsync(s.Lots.Issue("1", late), 422)).GetProperty("error").GetProperty("code").GetString());

        // 0175 no-workflow block: STATUS_TRANSFER AVAILABLE → BLOCKED of the later lot
        var key = QuarantineScenario.Key("blk");
        var blocked = await s.ExpectAsync(HttpMethod.Post, "/api/quarantine/blocked", new
        {
            buckets = new[] { new { itemId = s.Ledger.Item, skuId = s.Ledger.Sku, skuLevel = "Lsku", warehouseId = s.Ledger.Warehouse, locationId = s.Ledger.LocationA, quantity = 20m, lotId = late } },
            blockReasonCode = "QUALITY_REJECT", reason = QuarantineScenario.Reason, idempotencyKey = key
        }, 201, key);
        Assert.Equal(20m, await s.OnHandAsync("BLOCKED"));

        // 0175 approved disposition Destroy: SCRAP from BLOCKED
        var applied = await s.DecideAndApproveAsync(s.DisposeAsync(Id(blocked[0], "blockedId"),
            new { kind = "Destroy", quantity = 20m, destructionMethod = "Yakma (lisanslı firma)", destructionDate = DateTime.UtcNow.ToString("yyyy-MM-dd") }));
        Assert.Equal("Applied", Id(applied, "state"));
        Assert.Equal("SCRAP", Assert.Single(await s.LedgerMovementsAsync(Id(applied, "decisionNumber"))).GetProperty("movementType").GetString());
        Assert.Equal(0m, await s.OnHandAsync("BLOCKED"));

        Assert.Empty(s.Ledger.Persisted("expiry_fefo_issue_snapshots"));
        Assert.Empty(s.Ledger.Persisted("expiry_exceptions"));
        Assert.All(s.Ledger.Persisted("inventory_transactions").Where(t => t["SourceModule"].AsString == "MOD-0175"),
            t => Assert.False(t.Contains("ExpiryChecks") && !t["ExpiryChecks"].IsBsonNull, "a 0175 movement carries expiry checks"));
    }
}
