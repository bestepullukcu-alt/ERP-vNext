using System.Net.Http.Json;
using System.Text.Json;
using Diten.InventoryService.Application.Common;
using Diten.InventoryService.Application.Features.Quarantine.Services;
using Diten.InventoryService.Domain.Quarantine.Entities;
using Diten.InventoryService.Domain.Quarantine.Repositories;
using Diten.InventoryService.Domain.Traceability.Enums;
using Diten.InventoryService.Domain.Traceability.Repositories;
using Diten.InventoryService.Tests.TestInfrastructure;
using Microsoft.Extensions.DependencyInjection;
using Xunit;

namespace Diten.InventoryService.Tests.Quarantine;

/// <summary>
/// MOD-0175 WP-0175-Q4R — the two Q4 opens, measured red-first on the REAL service and Mongo:
/// <list type="bullet">
/// <item><b>CT-Q17 (O-Q4-1):</b> <c>updateLot</c> on a non-Draft lot moves <c>retestDate</c> only EARLIER; later or null → 400
/// <c>LOT_KEY_IMMUTABLE</c>; a Draft lot stays free; an approved retest release still moves it later (it does not pass updateLot).
/// K3 sabotage (a): drop the direction check → red.</item>
/// <item><b>O-Q4-3:</b> one open LotRelease per company + lot as a DATABASE rule (claim + unique index): parallel requests → one
/// decision, every 202 names it; the race staged deterministically (both requests see no claim) → the second loses the unique index and
/// answers the first; after the decision closed a new request opens a new one. K3 sabotage (b): drop the claim → red.</item>
/// </list>
/// </summary>
public sealed class QuarantineQ4RTests : IClassFixture<QuarantineApiFactory>
{
    private static readonly DateOnly Due = new(2026, 9, 15);
    private static readonly DateOnly Future = DateOnly.FromDateTime(DateTime.UtcNow).AddDays(60);

    private readonly QuarantineApiFactory _factory;

    public QuarantineQ4RTests(QuarantineApiFactory factory) => _factory = factory;

    private static string Code(JsonElement e) => e.GetProperty("error").GetProperty("code").GetString()!;

    private static async Task<(int Status, JsonElement Body)> PatchRetestAsync(QuarantineScenario q, string lot, string? retestDate)
    {
        var (_, _, get) = await q.SendAsync(HttpMethod.Get, $"/api/trace/lots/{lot}");
        var body = new Dictionary<string, object?> { ["retestDate"] = retestDate, ["reason"] = QuarantineScenario.Reason };
        var (status, json, _) = await q.SendAsync(HttpMethod.Patch, $"/api/trace/lots/{lot}", body, ifMatch: get.Headers.ETag!.ToString());
        return (status, json);
    }

    private static string RetestOf(QuarantineScenario q, string lot) => q.Lots.LotDocument(lot)["RetestDate"].ToUniversalTime().ToString("yyyy-MM-dd");

    // ── CT-Q17 ───────────────────────────────────────────────────────────────────────────────────────────

    [Theory]
    [InlineData("Released")]
    [InlineData("Blocked")]
    public async Task A_non_Draft_lots_retest_date_cannot_move_later_or_be_removed_by_an_edit(string status)
    {
        var q = new QuarantineScenario(_factory);
        var lot = await q.Lots.CreateLotAsync(expiryDate: "2028-01-31T00:00:00Z", retestDate: LotLedgerScenario.Midnight(Future));
        await q.Lots.ReleaseAsync(lot);
        if (status == "Blocked") await q.Lots.BlockAsync(lot);

        var later = await PatchRetestAsync(q, lot, LotLedgerScenario.Midnight(Future.AddDays(30)));
        Assert.Equal(400, later.Status);
        Assert.Equal("LOT_KEY_IMMUTABLE", Code(later.Body));
        var removed = await PatchRetestAsync(q, lot, null);
        Assert.Equal(400, removed.Status);
        Assert.Equal("LOT_KEY_IMMUTABLE", Code(removed.Body));
        Assert.Equal(Future.ToString("yyyy-MM-dd"), RetestOf(q, lot));
        Assert.DoesNotContain(q.Lots.Persisted("trace_lot_history"), h => h["LotId"].AsString == lot && h["Action"].AsString == "Updated");
    }

    [Fact]
    public async Task Moving_it_earlier_is_allowed_and_a_now_due_lot_refuses_a_sales_issue_at_once()
    {
        var q = new QuarantineScenario(_factory);
        var lot = await q.Lots.CreateLotAsync(expiryDate: "2028-01-31T00:00:00Z", retestDate: LotLedgerScenario.Midnight(Future));
        await q.Lots.ReleaseAsync(lot);
        await q.Lots.PostOkAsync(q.Lots.Receipt("10", lot));

        var earlier = await PatchRetestAsync(q, lot, LotLedgerScenario.Midnight(Due));
        Assert.Equal(200, earlier.Status);
        Assert.Equal("2026-09-15", RetestOf(q, lot));
        await q.Lots.RefusedAsync(q.Lots.Issue("1", lot), "LOT_RETEST_DUE"); // the posting rule reads the date: no sweep needed

        // the stored hold (availability) follows on the sweeper's run (CT-Q16: mark + position in one transaction)
        Assert.False(LotLedgerScenario.RetestHeld(q.Lots.LotDocument(lot)));
        Assert.Equal(1, (await q.Lots.SweepRetestAsync()).Held);
        Assert.Equal(0m, q.Lots.SkuPositionAvailableOnHand());
        await q.Lots.AssertConsistentAsync("earlier + swept");
    }

    [Fact]
    public async Task A_Draft_lots_retest_date_stays_free()
    {
        var q = new QuarantineScenario(_factory);
        var lot = await q.Lots.CreateLotAsync(expiryDate: "2028-01-31T00:00:00Z", retestDate: LotLedgerScenario.Midnight(Future));
        Assert.Equal(200, (await PatchRetestAsync(q, lot, LotLedgerScenario.Midnight(Future.AddDays(30)))).Status);
        Assert.Equal(Future.AddDays(30).ToString("yyyy-MM-dd"), RetestOf(q, lot));
        Assert.Equal(200, (await PatchRetestAsync(q, lot, null)).Status);
        Assert.True(q.Lots.LotDocument(lot)["RetestDate"].IsBsonNull);
    }

    [Fact]
    public async Task The_approved_retest_release_still_moves_a_Released_lots_retest_date_later()
    {
        var q = new QuarantineScenario(_factory);
        var lot = await q.Lots.CreateLotAsync(expiryDate: "2028-01-31T00:00:00Z", retestDate: LotLedgerScenario.Midnight(Future));
        await q.Lots.ReleaseAsync(lot);
        await q.Lots.PostOkAsync(q.Lots.Receipt("6", lot));
        Assert.Equal(200, (await PatchRetestAsync(q, lot, LotLedgerScenario.Midnight(Due))).Status);
        await q.Lots.SweepRetestAsync();
        var key = QuarantineScenario.Key("rt");
        var hold = (await q.ExpectAsync(HttpMethod.Post, "/api/quarantine/holds", new { mode = "Retest", lotId = lot, reason = QuarantineScenario.Reason, idempotencyKey = key }, 201, key))
            .EnumerateArray().Single().GetProperty("holdId").GetString()!;

        var newDate = DateOnly.FromDateTime(DateTime.UtcNow).AddDays(120);
        var draftKey = QuarantineScenario.Key("dec");
        var draft = await q.ExpectAsync(HttpMethod.Post, "/api/quarantine/decisions", new Dictionary<string, object?>
        {
            ["kind"] = "HoldDecision", ["targetId"] = hold, ["lines"] = new[] { QuarantineScenario.Line(1, "Release", 6m) }, ["reason"] = QuarantineScenario.Reason,
            ["coaChecked"] = true, ["newRetestDate"] = newDate.ToString("yyyy-MM-dd"), ["idempotencyKey"] = draftKey
        }, 201, draftKey);
        var applied = await q.DecideAndApproveAsync(Task.FromResult(draft));

        Assert.Equal("Applied", applied.GetProperty("state").GetString());
        Assert.Equal(newDate.ToString("yyyy-MM-dd"), RetestOf(q, lot)); // later than before — only this path may do it
        Assert.Equal(6m, await q.OnHandAsync("AVAILABLE"));
    }

    // ── O-Q4-3 ───────────────────────────────────────────────────────────────────────────────────────────

    private QuarantineScenario GateOn()
    {
        var q = new QuarantineScenario(_factory);
        LotGate.On(_factory.Database, q.Tenant, q.Ledger.LegalEntity);
        return q;
    }

    private static List<MongoDB.Bson.BsonDocument> Claims(QuarantineScenario q) => q.Ledger.Persisted("quarantine_lot_release_claims");

    [Fact]
    public async Task Parallel_release_requests_for_one_lot_open_one_decision_and_every_202_names_it()
    {
        var q = GateOn();
        for (var round = 0; round < 3; round++)
        {
            var lot = await q.Lots.CreateLotAsync();
            var answers = await Task.WhenAll(Enumerable.Range(0, 4).Select(_ => Task.Run(() => q.Lots.SetStatusAsync(lot, "Released", expected: 202))));

            var ids = answers.Select(a => a.GetProperty("pendingRequestId").GetString()).Distinct().ToList();
            Assert.Single(ids);
            Assert.Single(q.Decisions(), d => d["LotId"].AsString == lot);
            var claim = Assert.Single(Claims(q), c => c["LotId"].AsString == lot);
            Assert.Equal(ids[0], claim["DecisionId"].AsString);
        }
    }

    /// <summary>
    /// The race staged deterministically: the second request reads NO claim and NO open decision (the window between the read and the
    /// write), so it inserts a decision + claim — the unique index refuses the claim, the transaction (decision included) rolls back,
    /// the re-read answers the first decision. Without the claim (sabotage (b)) the second decision commits → red.
    /// </summary>
    [Fact]
    public async Task The_staged_race_loses_on_the_unique_index_and_answers_the_open_decision()
    {
        var q = GateOn();
        var lot = await q.Lots.CreateLotAsync();
        var first = (await q.Lots.SetStatusAsync(lot, "Released", expected: 202)).GetProperty("pendingRequestId").GetString();

        using var scope = _factory.Services.CreateScope();
        var sp = scope.ServiceProvider;
        sp.GetRequiredService<ITenantContext>().SetScope(q.Tenant, q.Ledger.LegalEntity, q.Ledger.User);
        var claimReads = 0;
        var staleRepository = Stale<IQuarantineRepository>.Wrap(sp.GetRequiredService<IQuarantineRepository>(), (m, _, _) =>
            m.Name == nameof(IQuarantineRepository.FindLotReleaseClaimAsync) && Interlocked.Increment(ref claimReads) == 1
                ? Task.FromResult<LotReleaseClaim?>(null)
                : Stale.Pass);
        var staleDecisions = Stale<IQuarantineDecisionRepository>.Wrap(sp.GetRequiredService<IQuarantineDecisionRepository>(), (m, _, _) =>
            m.Name == nameof(IQuarantineDecisionRepository.ListLotReleasesAsync) ? Task.FromResult<IReadOnlyList<QuarantineDecision>>([]) : Stale.Pass);
        var approvals = new QuarantineLotReleaseApprovals(staleRepository, staleDecisions, sp.GetRequiredService<QuarantineSupport>());
        var entity = (await sp.GetRequiredService<ILotRepository>().FindAsync(Guid.Parse(lot)))!;

        var second = await approvals.OpenAsync(entity, LotStatus.Released, QuarantineScenario.Reason, "staged-" + Guid.NewGuid().ToString("N"));

        Assert.True(claimReads >= 2, "the staging did not reach the write (no re-read after the refused claim)");
        Assert.Equal(first, second);
        Assert.Single(q.Decisions(), d => d["LotId"].AsString == lot); // the loser's decision insert rolled back with its claim
        Assert.Single(Claims(q), c => c["LotId"].AsString == lot);
    }

    [Fact]
    public async Task After_the_decision_closed_a_new_request_opens_a_new_decision()
    {
        var q = GateOn();
        var lot = await q.Lots.CreateLotAsync();
        var first = (await q.Lots.SetStatusAsync(lot, "Released", expected: 202)).GetProperty("pendingRequestId").GetString()!;
        await q.SubmitAsync(first);
        Assert.Equal("Rejected", (await q.OutcomeAsync(first, "Rejected")).GetProperty("state").GetString());

        var second = (await q.Lots.SetStatusAsync(lot, "Released", expected: 202)).GetProperty("pendingRequestId").GetString()!;
        Assert.NotEqual(first, second);
        Assert.Equal(2, q.Decisions().Count(d => d["LotId"].AsString == lot));
        Assert.Equal(second, Assert.Single(Claims(q), c => c["LotId"].AsString == lot)["DecisionId"].AsString);

        // the open one answers again; the closed one is not revived
        Assert.Equal(second, (await q.Lots.SetStatusAsync(lot, "Released", expected: 202)).GetProperty("pendingRequestId").GetString());
    }
}
