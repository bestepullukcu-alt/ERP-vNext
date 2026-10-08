using System.Text.Json;
using Diten.InventoryService.Application.Common;
using Diten.InventoryService.Application.Common.Models;
using Diten.InventoryService.Application.Features.Traceability;
using Diten.InventoryService.Application.Features.Traceability.Handlers.CommandHandlers;
using Diten.InventoryService.Tests.TestInfrastructure;
using MediatR;
using Microsoft.Extensions.DependencyInjection;
using Xunit;

namespace Diten.InventoryService.Tests.Quarantine;

/// <summary>
/// MOD-0175 Q4 — KQ-I lot release gate (pack §8 KQ-I, §13 #20, §16 Q4; CT-Q5, CT-Q10), through the REAL TRACE + QUARANTINE APIs:
/// <list type="bullet">
/// <item>gate on (the company default): Draft → Released and Blocked → Released answer 202 <c>PendingApproval {lotId, pendingRequestId,
/// contractVersion}</c>; the lot does not change; the pending request is a LotRelease decision in Draft; Released → Blocked stays
/// immediate; gate off → 200 as before;</item>
/// <item>the requester submits, another user approves → the lot is Released through <c>ApplyApprovedLotStatusChange</c> with a lot
/// history row "MOD-0175 approval QKR-…" (actor = the approver), and a Blocked lot's AVAILABLE stock comes back;</item>
/// <item>the command past the gate refuses: no decision, an unapproved decision, another lot's decision, a status the approval does not
/// cover;</item>
/// <item>CT-Q5: a hold release on a Draft lot releases the lot in the same approval.</item>
/// </list>
/// </summary>
public sealed class QuarantineLotGateTests : IClassFixture<QuarantineApiFactory>
{
    private readonly QuarantineApiFactory _factory;

    public QuarantineLotGateTests(QuarantineApiFactory factory) => _factory = factory;

    private static string Code(JsonElement e) => e.GetProperty("error").GetProperty("code").GetString()!;

    private static QuarantineScenario GateOn(QuarantineApiFactory factory)
    {
        var q = new QuarantineScenario(factory);
        LotGate.On(factory.Database, q.Tenant, q.Ledger.LegalEntity);
        return q;
    }

    private static string LotStatus(QuarantineScenario q, string lot) => q.Lots.LotDocument(lot)["Status"].AsString;

    private async Task<Response<LotRefDto>> ApplyPastTheGateAsync(QuarantineScenario q, Guid decisionId, Guid lotId)
    {
        using var scope = _factory.Services.CreateScope();
        scope.ServiceProvider.GetRequiredService<ITenantContext>().SetScope(q.Tenant, q.Ledger.LegalEntity, q.Ledger.User);
        return await scope.ServiceProvider.GetRequiredService<ISender>().Send(new ApplyApprovedLotStatusChangeCommand(decisionId, lotId));
    }

    [Fact]
    public async Task Gate_on_Draft_to_Released_answers_202_PendingApproval_the_lot_stays_Draft_and_the_request_is_a_LotRelease_draft()
    {
        var q = GateOn(_factory);
        var lot = await q.Lots.CreateLotAsync();

        var pending = await q.Lots.SetStatusAsync(lot, "Released", expected: 202);
        Assert.Equal(lot, pending.GetProperty("lotId").GetString());
        Assert.Equal("v1", pending.GetProperty("contractVersion").GetString());
        Assert.Equal(3, pending.EnumerateObject().Count()); // exactly the TRACE PendingApproval members
        var decisionId = pending.GetProperty("pendingRequestId").GetString()!;
        Assert.Equal("Draft", LotStatus(q, lot));
        Assert.DoesNotContain(q.Lots.Persisted("trace_lot_history"), h => h["LotId"].AsString == lot && h["Action"].AsString == "StatusChanged");

        var decision = await q.ExpectAsync(HttpMethod.Get, $"/api/quarantine/decisions/{decisionId}", null, 200);
        Assert.Equal("LotRelease", decision.GetProperty("kind").GetString());
        Assert.Equal("Draft", decision.GetProperty("state").GetString());
        Assert.Equal(lot, decision.GetProperty("lotId").GetString());
        Assert.Equal("Released", decision.GetProperty("lotStatusTarget").GetString());
        Assert.Equal("quarantine.lot.release", decision.GetProperty("esignActionCode").GetString());
        Assert.Empty(decision.GetProperty("plannedMovements").EnumerateArray());

        // a second request for the same lot answers the same open request (one per lot)
        var again = await q.Lots.SetStatusAsync(lot, "Released", expected: 202);
        Assert.Equal(decisionId, again.GetProperty("pendingRequestId").GetString());
        Assert.Single(q.Decisions());
    }

    [Fact]
    public async Task Gate_on_Released_to_Blocked_is_immediate_and_Blocked_to_Released_waits()
    {
        var q = new QuarantineScenario(_factory); // gate off for the set-up release
        var lot = await q.ReleasedLotAsync();
        await q.ReceiveAsync("12", lot, "AVAILABLE");
        LotGate.On(_factory.Database, q.Tenant, q.Ledger.LegalEntity);

        await q.Lots.BlockAsync(lot); // safety direction: 200, not gated
        Assert.Equal("Blocked", LotStatus(q, lot));
        Assert.Equal(0m, q.Lots.SkuPositionAvailableOnHand());

        var pending = await q.Lots.SetStatusAsync(lot, "Released", expected: 202);
        Assert.Equal("Blocked", LotStatus(q, lot));
        Assert.Equal(0m, q.Lots.SkuPositionAvailableOnHand());
        var decisionId = pending.GetProperty("pendingRequestId").GetString()!;

        await q.SubmitAsync(decisionId);
        var applied = await q.OutcomeAsync(decisionId);
        Assert.Equal("Applied", applied.GetProperty("state").GetString());
        Assert.Equal("Released", LotStatus(q, lot));
        Assert.Equal(12m, q.Lots.SkuPositionAvailableOnHand()); // the lot's AVAILABLE stock is back (LotHeldStock in the same transaction)
        await q.Lots.AssertConsistentAsync("approved Blocked → Released");
    }

    [Fact]
    public async Task Approved_lot_release_sets_Released_with_a_history_row_of_the_approval()
    {
        var q = GateOn(_factory);
        var lot = await q.Lots.CreateLotAsync();
        var decisionId = (await q.Lots.SetStatusAsync(lot, "Released", expected: 202)).GetProperty("pendingRequestId").GetString()!;

        var submitted = await q.SubmitAsync(decisionId);
        Assert.Equal("PendingApproval", submitted.GetProperty("state").GetString());
        Assert.Equal("Draft", LotStatus(q, lot));
        var applied = await q.OutcomeAsync(decisionId);
        Assert.Equal("Applied", applied.GetProperty("state").GetString());

        Assert.Equal("Released", LotStatus(q, lot));
        var number = applied.GetProperty("decisionNumber").GetString()!;
        var history = await q.Lots.GetTraceAsync($"/api/trace/lots/{lot}/history?pageSize=50");
        var row = Assert.Single(history.GetProperty("items").EnumerateArray(), e => e.GetProperty("action").GetString() == "StatusChanged");
        Assert.Equal("Draft", row.GetProperty("oldValue").GetString());
        Assert.Equal("Released", row.GetProperty("newValue").GetString());
        Assert.StartsWith($"MOD-0175 approval {number}: ", row.GetProperty("reason").GetString());
        Assert.Equal(q.Approver.ToString("D"), row.GetProperty("actorUserId").GetString());
        Assert.Empty(await q.LedgerMovementsAsync(number)); // a lot release moves no stock

        // the approval is used up: the same command again changes nothing and is refused (Applied is not "approved, to apply")
        var repeat = await ApplyPastTheGateAsync(q, Guid.Parse(decisionId), Guid.Parse(lot));
        Assert.Equal(ApplyApprovedLotStatusChangeHandler.ApprovalRequired, repeat.ErrorCode);
    }

    [Fact]
    public async Task Gate_off_releases_at_once_as_today()
    {
        var q = new QuarantineScenario(_factory); // LotReleaseRequiresApproval = false for this company
        var lot = await q.Lots.CreateLotAsync();
        var released = await q.Lots.ReleaseAsync(lot);
        Assert.Equal("Released", released.GetProperty("status").GetString());
        Assert.Empty(q.Decisions());
    }

    [Fact]
    public async Task The_command_past_the_gate_refuses_without_an_approved_decision_of_this_lot()
    {
        var q = GateOn(_factory);
        var lotA = await q.Lots.CreateLotAsync();
        var lotB = await q.Lots.CreateLotAsync();

        // no decision at all
        var none = await ApplyPastTheGateAsync(q, Guid.NewGuid(), Guid.Parse(lotA));
        Assert.Equal(ApplyApprovedLotStatusChangeHandler.ApprovalRequired, none.ErrorCode);

        // a Draft (unapproved) and a submitted (pending) decision
        var decisionA = (await q.Lots.SetStatusAsync(lotA, "Released", expected: 202)).GetProperty("pendingRequestId").GetString()!;
        Assert.Equal(ApplyApprovedLotStatusChangeHandler.ApprovalRequired, (await ApplyPastTheGateAsync(q, Guid.Parse(decisionA), Guid.Parse(lotA))).ErrorCode);
        await q.SubmitAsync(decisionA);
        Assert.Equal(ApplyApprovedLotStatusChangeHandler.ApprovalRequired, (await ApplyPastTheGateAsync(q, Guid.Parse(decisionA), Guid.Parse(lotA))).ErrorCode);
        Assert.Equal("Draft", LotStatus(q, lotA));

        // an APPROVED decision whose apply failed (the lot was released and blocked meanwhile; the approval covers Draft only)
        LotGate.Off(_factory.Database, q.Tenant, q.Ledger.LegalEntity);
        await q.Lots.ReleaseAsync(lotA);
        await q.Lots.BlockAsync(lotA);
        var failed = await q.OutcomeAsync(decisionA);
        Assert.Equal("ApplyFailed", failed.GetProperty("state").GetString());
        Assert.Equal("LOT_STATUS_TRANSITION_INVALID", failed.GetProperty("error").GetProperty("code").GetString());
        Assert.Equal("Blocked", LotStatus(q, lotA)); // an approval of a Draft release never unblocks

        // … sent for ANOTHER lot: refused, that lot untouched
        var other = await ApplyPastTheGateAsync(q, Guid.Parse(decisionA), Guid.Parse(lotB));
        Assert.Equal(ApplyApprovedLotStatusChangeHandler.ApprovalRequired, other.ErrorCode);
        Assert.Equal("Draft", LotStatus(q, lotB));
        // … for its own lot: the status it covers is gone → 422
        var own = await ApplyPastTheGateAsync(q, Guid.Parse(decisionA), Guid.Parse(lotA));
        Assert.Equal(422, own.StatusCode);
        Assert.Equal("Blocked", LotStatus(q, lotA));
    }

    [Fact]
    public async Task CT_Q5_a_hold_release_on_a_Draft_lot_releases_the_lot_in_the_same_approval()
    {
        var q = GateOn(_factory);
        var lot = await q.Lots.CreateLotAsync(expiryDate: "2028-01-31T00:00:00Z");
        var (_, hold) = await q.HoldAsync("9", lot);
        var draft = await q.DecideAsync(hold.GetProperty("holdId").GetString()!, [QuarantineScenario.Line(1, "Release", 9m)]);
        Assert.Equal("Released", draft.GetProperty("lotStatusTarget").GetString());

        var applied = await q.DecideAndApproveAsync(Task.FromResult(draft));
        Assert.Equal("Applied", applied.GetProperty("state").GetString());
        Assert.Equal("Released", LotStatus(q, lot));
        Assert.Equal(9m, await q.OnHandAsync("AVAILABLE"));
        var history = await q.Lots.GetTraceAsync($"/api/trace/lots/{lot}/history?pageSize=50");
        Assert.Contains(history.GetProperty("items").EnumerateArray(), e => e.GetProperty("action").GetString() == "StatusChanged"
            && e.GetProperty("reason").GetString()!.StartsWith("MOD-0175 approval " + applied.GetProperty("decisionNumber").GetString(), StringComparison.Ordinal));
        Assert.Single(q.Decisions()); // one approval for the batch, no LotRelease decision
        await q.Lots.AssertConsistentAsync("CT-Q5");
    }
}
