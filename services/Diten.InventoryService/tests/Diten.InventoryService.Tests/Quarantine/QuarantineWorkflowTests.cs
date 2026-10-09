using System.Text.Json;
using System.Text.Json.Nodes;
using Diten.InventoryService.Application.Features.Quarantine.Ports;
using Diten.InventoryService.Infrastructure.Eventing;
using Diten.InventoryService.Tests.TestInfrastructure;
using Diten.Platform.Application.Contracts.Eventing;
using Microsoft.Extensions.DependencyInjection;
using Xunit;

namespace Diten.InventoryService.Tests.Quarantine;

/// <summary>
/// MOD-0175 pack §16 Q2 — the Workflow integration on the PRODUCTION approval port (the Q1 test port is off): the real
/// <c>GatewayWorkflowClient</c> + <c>ForwardCallerHeadersHandler</c> talk to an in-memory MOD-0023 at the HTTP edge
/// (<see cref="FakeWorkflowGateway"/>). Measured: submit starts ONE instance with the decider's own token, the right object,
/// candidates and DisplayContext (a repeat gets the same instance); an approved outcome is applied ONCE at read time, also
/// under parallel reads; reject / cancel / time-out move nothing and reopen the record; the starter approving is refused by
/// Workflow (SoD) and 0175 writes nothing; withdraw cancels with the caller's token; "Durumu yenile"; template preparation
/// is idempotent; the completion-event consumer (broker-independent) applies once and only for the decision's own round.
/// </summary>
public sealed class QuarantineWorkflowTests : IClassFixture<WorkflowQuarantineApiFactory>
{
    private readonly WorkflowQuarantineApiFactory _factory;

    public QuarantineWorkflowTests(WorkflowQuarantineApiFactory factory) => _factory = factory;

    private FakeWorkflowGateway Workflow => _factory.Workflow;

    private static string Id(JsonElement e, string name) => e.GetProperty(name).GetString()!;

    /// <summary>A world whose company has the approver as candidate, reconciles on every read, and has the template published.</summary>
    private async Task<QuarantineScenario> WorldAsync(bool serialControlled = false, int reconcileAfterSeconds = 0, bool template = true, bool approvers = true)
    {
        var s = new QuarantineScenario(_factory, serialControlled: serialControlled);
        if (approvers) // a fresh company has no candidates, and settings refuse an empty list (1-10)
        {
            await s.SaveSettingsAsync(new { approverCandidates = new[] { "user:" + s.Approver.ToString("D") }, reconcileAfterSeconds });
        }

        if (template) await PrepareTemplateAsync(s);
        return s;
    }

    private static Task<JsonElement> PrepareTemplateAsync(QuarantineScenario s, int expected = 200)
    {
        var key = QuarantineScenario.Key("tpl");
        return s.ExpectAsync(HttpMethod.Post, "/api/quarantine/settings/workflow-template", new { idempotencyKey = key }, expected, key);
    }

    private async Task<(QuarantineScenario S, JsonElement Hold, string DecisionId, string Instance)> SubmittedReleaseAsync(
        string quantity = "800", int reconcileAfterSeconds = 0)
    {
        var s = await WorldAsync(reconcileAfterSeconds: reconcileAfterSeconds);
        var lot = await s.ReleasedLotAsync();
        var (_, hold) = await s.HoldAsync(quantity, lot);
        var draft = await s.DecideAsync(Id(hold, "holdId"), [QuarantineScenario.Line(1, "Release", decimal.Parse(quantity))]);
        var submitted = await s.SubmitAsync(Id(draft, "decisionId"));
        return (s, hold, Id(draft, "decisionId"), Id(submitted.GetProperty("workflow"), "instanceId"));
    }

    // ── start ───────────────────────────────────────────────────────────────────────────────────────────

    /// <summary>K3 (b): an empty Authorization instead of the decider's token → MOD-0023 401 → nothing starts → this test is red.</summary>
    [Fact]
    public async Task Submit_starts_one_instance_with_the_deciders_token_object_candidates_and_display_context()
    {
        var (s, hold, decisionId, instance) = await SubmittedReleaseAsync();
        Assert.True(Guid.TryParse(instance, out _));
        Assert.Equal("PendingApproval", Id(await s.ExpectAsync(HttpMethod.Get, $"/api/quarantine/decisions/{decisionId}", null, 200), "state"));
        Assert.Equal("DecisionPending", Id(await s.GetHoldAsync(Id(hold, "holdId")), "status"));

        var start = Assert.Single(Workflow.Requests, r => r.Method == "POST" && r.Path == "/api/v1/workflow/instances" && r.Authorization == "Bearer " + s.Token);
        Assert.Equal(s.Tenant.ToString("D"), start.Tenant);
        var body = Assert.Single(Workflow.StartBodies, b => b["objectId"]!.GetValue<string>() == decisionId);
        Assert.Equal("INV-QUARANTINE-RELEASE", body["templateCode"]!.GetValue<string>());
        Assert.Equal("quarantine.decision", body["objectType"]!.GetValue<string>());
        Assert.Equal($"quarantine:quarantine.decision:{decisionId}:r1", body["idempotencyKey"]!.GetValue<string>());
        Assert.Equal("QUARANTINE_HOLD_DECISION", body["reasonCode"]!.GetValue<string>());
        Assert.Equal(["user:" + s.Approver.ToString("D")], body["candidatePrincipalIds"]!.AsArray().Select(c => c!.GetValue<string>()));
        Assert.StartsWith("QKR-", body["objectRef"]!.GetValue<string>());
        Assert.NotNull(body["dueAt"]);

        var display = body["displayContext"]!.AsObject();
        Assert.Equal($"Karantina kararı {Id(hold, "holdNumber")} · Serbest bırak", display["title"]!.GetValue<string>());
        Assert.Contains("800", display["subtitle"]!.GetValue<string>());
        Assert.Equal("MOD-0175", display["sourceModule"]!.GetValue<string>());
        Assert.Equal($"/Quarantine/Details/{Id(hold, "holdId")}", display["deepLinkUrl"]!.GetValue<string>());
        var chips = display["chips"]!.AsArray().Select(c => c!.GetValue<string>()).ToList();
        Assert.InRange(chips.Count, 1, 5);
        Assert.All(chips, c => Assert.True(c.Length <= 32));
        // Q5 (O-5 / O-Q2-4): readable labels from the existing resolvers — the lot NUMBER and the warehouse / location CODES, not ids
        var lotNumber = s.Lots.LotDocument(Id(hold.GetProperty("lot"), "lotId"))["LotNumber"].AsString;
        Assert.Contains("Lot " + lotNumber, chips);
        Assert.Contains("Depo " + s.Ledger.WarehouseNode.Code, chips);
        Assert.Contains(chips, c => c.StartsWith("Lokasyon ", StringComparison.Ordinal));
        Assert.Contains("Lot " + lotNumber, display["subtitle"]!.GetValue<string>());

        // the instance is the decider's: MOD-0023 sees the starter = the decider (what makes its SoD work)
        Assert.Equal(s.Ledger.User, Assert.Single(Workflow.InstancesOf(decisionId)).StartedBy);
    }

    /// <summary>Q5 runtime finding: a disposition's WorkCenter title read "Disposition · Destroy" (enum names) — it carries the kind's label.</summary>
    [Fact]
    public async Task A_disposition_approval_carries_a_readable_kind_and_links_to_its_blocked_record()
    {
        var s = await WorldAsync();
        var lot = await s.ReleasedLotAsync();
        var (_, hold) = await s.HoldAsync("100", lot);
        var reject = await s.DecideAsync(Id(hold, "holdId"), [QuarantineScenario.Line(1, "Reject", 100m)], "QUALITY_REJECT", coa: false);
        Workflow.Approve(Id((await s.SubmitAsync(Id(reject, "decisionId"))).GetProperty("workflow"), "instanceId"), s.Approver);
        Assert.Equal("Applied", Id(await s.ExpectAsync(HttpMethod.Get, $"/api/quarantine/decisions/{Id(reject, "decisionId")}", null, 200), "state"));
        var blocked = await s.BlockedOfDecisionAsync(Id(reject, "decisionId"), 1);

        var draft = await s.DisposeAsync(Id(blocked, "blockedId"),
            new { kind = "Destroy", quantity = 40m, destructionMethod = "Yakma (lisanslı firma)", destructionDate = DateTime.UtcNow.ToString("yyyy-MM-dd") });
        await s.SubmitAsync(Id(draft, "decisionId"));
        var display = Assert.Single(Workflow.StartBodies, b => b["objectId"]!.GetValue<string>() == Id(draft, "decisionId"))["displayContext"]!.AsObject();
        Assert.Equal($"Karantina kararı {Id(draft, "decisionNumber")} · İmha", display["title"]!.GetValue<string>());
        Assert.Contains("İmha", display["chips"]!.AsArray().Select(c => c!.GetValue<string>()));
        Assert.Equal($"/Quarantine/Blocked?q={Id(blocked, "blockedNumber")}", display["deepLinkUrl"]!.GetValue<string>());
    }

    [Fact]
    public async Task A_repeated_submit_gets_the_same_instance_and_starts_nothing_new()
    {
        var (s, _, decisionId, instance) = await SubmittedReleaseAsync();
        var again = await s.SubmitAsync(decisionId);
        Assert.Equal(instance, Id(again.GetProperty("workflow"), "instanceId"));
        Assert.Single(Workflow.InstancesOf(decisionId));
        Assert.Single(s.Events(), e => e["DecisionId"].AsString == decisionId && e["Kind"].AsString == "WorkflowStarted");
    }

    [Fact]
    public async Task No_approver_candidates_is_409_and_nothing_is_started()
    {
        var s = await WorldAsync(approvers: false);
        var (_, hold) = await s.HoldAsync("100", await s.ReleasedLotAsync());
        var draft = await s.DecideAsync(Id(hold, "holdId"), [QuarantineScenario.Line(1, "Release", 100m)]);
        var refused = await s.SubmitAsync(Id(draft, "decisionId"), expected: 409);
        Assert.Equal("QUARANTINE_APPROVERS_NOT_CONFIGURED", QuarantineScenario.Code(refused));
        Assert.Empty(Workflow.InstancesOf(Id(draft, "decisionId")));
        Assert.DoesNotContain(Workflow.StartBodies, b => b["objectId"]!.GetValue<string>() == Id(draft, "decisionId"));
        var reopened = await s.GetHoldAsync(Id(hold, "holdId"));
        Assert.Equal("Open", Id(reopened, "status"));
        Assert.Empty(await s.LedgerMovementsAsync(Id(draft, "decisionNumber")));
    }

    [Fact]
    public async Task A_missing_template_is_409_template_missing_and_the_decision_is_StartFailed_with_no_movement()
    {
        var s = await WorldAsync(template: false);
        var (_, hold) = await s.HoldAsync("100", await s.ReleasedLotAsync());
        var draft = await s.DecideAsync(Id(hold, "holdId"), [QuarantineScenario.Line(1, "Release", 100m)]);
        var refused = await s.SubmitAsync(Id(draft, "decisionId"), expected: 409);
        Assert.Equal("QUARANTINE_WORKFLOW_TEMPLATE_MISSING", QuarantineScenario.Code(refused));
        Assert.Equal("StartFailed", Id(await s.ExpectAsync(HttpMethod.Get, $"/api/quarantine/decisions/{Id(draft, "decisionId")}", null, 200), "state"));
        Assert.Empty(await s.LedgerMovementsAsync(Id(draft, "decisionNumber")));
    }

    [Fact]
    public async Task Workflow_down_at_submit_is_503_and_nothing_moves()
    {
        var s = await WorldAsync();
        var (_, hold) = await s.HoldAsync("100", await s.ReleasedLotAsync());
        var draft = await s.DecideAsync(Id(hold, "holdId"), [QuarantineScenario.Line(1, "Release", 100m)]);
        Workflow.Down = true;
        try
        {
            Assert.Equal("QUARANTINE_WORKFLOW_UNAVAILABLE", QuarantineScenario.Code(await s.SubmitAsync(Id(draft, "decisionId"), expected: 503)));
        }
        finally
        {
            Workflow.Down = false;
        }

        Assert.Equal("Open", Id(await s.GetHoldAsync(Id(hold, "holdId")), "status"));
        Assert.Empty(await s.LedgerMovementsAsync(Id(draft, "decisionNumber")));
    }

    // ── outcome at read time ────────────────────────────────────────────────────────────────────────────

    /// <summary>
    /// §16 Q2: approved in WorkCenter (by B) → A opens the queue → the stock moves ONCE, even when sixteen reads race. K3 (a): a
    /// reconciler without the "already applied" guards posts / records twice → this test is red.
    /// </summary>
    [Fact]
    public async Task An_approved_outcome_is_applied_once_at_read_time_even_under_parallel_reads()
    {
        var (s, hold, decisionId, instance) = await SubmittedReleaseAsync("800");
        Assert.Equal((200, (string?)null), Workflow.Approve(instance, s.Approver));
        var number = Id(await s.ExpectAsync(HttpMethod.Get, $"/api/quarantine/decisions/{decisionId}", null, 200), "decisionNumber");

        var holdId = Id(hold, "holdId");
        var reads = Enumerable.Range(0, 16).Select(i => (i % 4) switch
        {
            0 => s.ExpectAsync(HttpMethod.Get, $"/api/quarantine/holds/{holdId}", null, 200),
            1 => s.ExpectAsync(HttpMethod.Get, "/api/quarantine/holds?pageSize=50", null, 200),
            2 => s.ExpectAsync(HttpMethod.Get, $"/api/quarantine/decisions/{decisionId}", null, 200),
            _ => s.ExpectAsync(HttpMethod.Post, $"/api/quarantine/decisions/{decisionId}/refresh", null, 200)
        }).ToList();
        await Task.WhenAll(reads);

        var decision = await s.ExpectAsync(HttpMethod.Get, $"/api/quarantine/decisions/{decisionId}", null, 200);
        Assert.Equal("Applied", Id(decision, "state"));
        Assert.Equal("approved", Id(decision.GetProperty("workflow"), "outcome"));
        Assert.Equal(s.Approver.ToString("D"), Id(decision.GetProperty("workflow").GetProperty("approver"), "userId"));
        var move = Assert.Single(await s.LedgerMovementsAsync(number));
        Assert.Equal("STATUS_TRANSFER", Id(move, "movementType"));
        Assert.Equal(800m, await s.OnHandAsync("AVAILABLE"));
        Assert.Equal(0m, await s.OnHandAsync("QUARANTINE"));
        Assert.Single(s.Events(), e => e["DecisionId"].AsString == decisionId && e["Kind"].AsString == "Applied");
        Assert.Single(s.Events(), e => e["DecisionId"].AsString == decisionId && e["Kind"].AsString == "Approved");
        // runtime probe finding: the losers of the race must not record ApplyFailed over the winner (state would read ApplyFailed)
        Assert.DoesNotContain(s.Events(), e => e["DecisionId"].AsString == decisionId && e["Kind"].AsString == "ApplyFailed");
        Assert.Equal("Decided", Id(await s.GetHoldAsync(holdId), "status"));

        // a later read changes nothing — and an applied decision is not even asked about again (the reconciler only picks
        // decisions still waiting for their outcome)
        var count = await s.AllMovementCountAsync();
        var asked = Workflow.Requests.Count(r => r.Path.Contains(decisionId, StringComparison.OrdinalIgnoreCase));
        await s.ExpectAsync(HttpMethod.Post, $"/api/quarantine/decisions/{decisionId}/refresh", null, 200);
        await s.ExpectAsync(HttpMethod.Get, $"/api/quarantine/holds/{holdId}", null, 200);
        Assert.Equal(count, await s.AllMovementCountAsync());
        Assert.Equal(asked, Workflow.Requests.Count(r => r.Path.Contains(decisionId, StringComparison.OrdinalIgnoreCase)));
    }

    /// <summary>K3 (c): an engine that posts on a reject → movement present → this test is red.</summary>
    [Theory]
    [InlineData("reject", "Rejected")]
    [InlineData("timeout", "TimedOut")]
    public async Task Reject_and_timeout_move_nothing_and_reopen_the_record(string how, string state)
    {
        var (s, hold, decisionId, instance) = await SubmittedReleaseAsync("300");
        if (how == "reject") Assert.Equal(200, Workflow.Reject(instance, s.Approver).Status);
        else Workflow.TimeOut(instance);

        var holdId = Id(hold, "holdId");
        var reopened = await s.GetHoldAsync(holdId); // the read reconciles
        Assert.Equal("Open", Id(reopened, "status"));
        var decision = await s.ExpectAsync(HttpMethod.Get, $"/api/quarantine/decisions/{decisionId}", null, 200);
        Assert.Equal(state, Id(decision, "state"));
        Assert.Empty(await s.LedgerMovementsAsync(Id(decision, "decisionNumber")));
        Assert.Equal(300m, await s.OnHandAsync("QUARANTINE"));
        Assert.Equal(0m, await s.OnHandAsync("AVAILABLE"));

        // the record is decidable again
        var next = await s.DecideAsync(holdId, [QuarantineScenario.Line(1, "Release", 300m)]);
        Assert.Equal("Draft", Id(next, "state"));
    }

    /// <summary>§16 Q2 / CT-Q13: A approving their own decision is refused by MOD-0023 (SoD 409) — and 0175 writes nothing.</summary>
    [Fact]
    public async Task The_starter_approving_is_refused_by_workflow_and_0175_writes_nothing()
    {
        var (s, hold, decisionId, instance) = await SubmittedReleaseAsync("200");
        var events = s.Events().Count(e => e["DecisionId"].AsString == decisionId);
        Assert.Equal((409, "SOD_VIOLATION"), Workflow.Approve(instance, s.Ledger.User));

        var decision = await s.ExpectAsync(HttpMethod.Post, $"/api/quarantine/decisions/{decisionId}/refresh", null, 200);
        Assert.Equal("PendingApproval", Id(decision, "state"));
        Assert.Equal("DecisionPending", Id(await s.GetHoldAsync(Id(hold, "holdId")), "status"));
        Assert.Equal(events, s.Events().Count(e => e["DecisionId"].AsString == decisionId));
        Assert.Empty(await s.LedgerMovementsAsync(Id(decision, "decisionNumber")));
    }

    [Fact]
    public async Task Refresh_applies_before_the_reconcile_delay_and_reads_do_not()
    {
        var (s, _, decisionId, instance) = await SubmittedReleaseAsync("50", reconcileAfterSeconds: 3600);
        Workflow.Approve(instance, s.Approver);
        Assert.Equal("PendingApproval", Id(await s.ExpectAsync(HttpMethod.Get, $"/api/quarantine/decisions/{decisionId}", null, 200), "state"));
        Assert.Equal("Applied", Id(await s.ExpectAsync(HttpMethod.Post, $"/api/quarantine/decisions/{decisionId}/refresh", null, 200), "state"));
    }

    [Fact]
    public async Task Refresh_with_workflow_down_is_503_and_changes_nothing()
    {
        var (s, _, decisionId, instance) = await SubmittedReleaseAsync("50", reconcileAfterSeconds: 3600);
        Workflow.Approve(instance, s.Approver);
        Workflow.Down = true;
        try
        {
            var refused = await s.ExpectAsync(HttpMethod.Post, $"/api/quarantine/decisions/{decisionId}/refresh", null, 503);
            Assert.Equal("QUARANTINE_WORKFLOW_UNAVAILABLE", QuarantineScenario.Code(refused));
        }
        finally
        {
            Workflow.Down = false;
        }

        Assert.DoesNotContain(s.Events(), e => e["DecisionId"].AsString == decisionId && e["Kind"].AsString == "Applied");
    }

    // ── withdraw ────────────────────────────────────────────────────────────────────────────────────────

    [Fact]
    public async Task The_decider_withdraws_the_task_is_cancelled_with_their_token_and_nothing_moves()
    {
        var (s, hold, decisionId, instance) = await SubmittedReleaseAsync("120");
        var path = $"/api/quarantine/decisions/{decisionId}/withdraw";

        var key = QuarantineScenario.Key("wd");
        var notDecider = await s.ExpectAsync(HttpMethod.Post, path, new { reason = QuarantineScenario.Reason, idempotencyKey = key }, 403, key, token: s.ApproverToken);
        Assert.Equal("QUARANTINE_NOT_DECIDER", QuarantineScenario.Code(notDecider));

        key = QuarantineScenario.Key("wd");
        var withdrawn = await s.ExpectAsync(HttpMethod.Post, path, new { reason = QuarantineScenario.Reason, idempotencyKey = key }, 200, key);
        Assert.Equal("Withdrawn", Id(withdrawn, "state"));
        var cancel = Assert.Single(Workflow.Requests, r => r.Path.EndsWith("/cancel", StringComparison.Ordinal) && r.Tenant == s.Tenant.ToString("D"));
        Assert.Equal("Bearer " + s.Token, cancel.Authorization);
        var wf = Assert.Single(Workflow.InstancesOf(decisionId));
        Assert.Equal(("cancel", s.Ledger.User.ToString("D")), wf.History.Last());

        Assert.Equal("Open", Id(await s.GetHoldAsync(Id(hold, "holdId")), "status"));
        Assert.Empty(await s.LedgerMovementsAsync(Id(withdrawn, "decisionNumber")));
        // the cancelled instance read later does not turn Withdrawn into Cancelled
        Assert.Equal("Withdrawn", Id(await s.ExpectAsync(HttpMethod.Post, $"/api/quarantine/decisions/{decisionId}/refresh", null, 200), "state"));
        _ = instance;
    }

    // ── template ────────────────────────────────────────────────────────────────────────────────────────

    [Fact]
    public async Task Preparing_the_template_is_idempotent_and_publishes_the_shipped_json_with_the_callers_token()
    {
        var s = new QuarantineScenario(_factory);
        var first = await PrepareTemplateAsync(s);
        Assert.Equal("INV-QUARANTINE-RELEASE", Id(first, "code"));
        Assert.True(first.GetProperty("created").GetBoolean());
        Assert.Equal(1, first.GetProperty("publishedVersion").GetInt32());

        var second = await PrepareTemplateAsync(s);
        Assert.False(second.GetProperty("created").GetBoolean());
        Assert.Equal(Id(first, "templateId"), Id(second, "templateId"));
        Assert.Equal(1, second.GetProperty("publishedVersion").GetInt32());

        var definition = Workflow.DefinitionOf(s.Tenant, "INV-QUARANTINE-RELEASE")!;
        Assert.Equal(1, definition.Version);
        Assert.Equal("workflow_schema_v1", definition.SchemaVersion);
        Assert.True(JsonNode.DeepEquals(JsonNode.Parse(QuarantineWorkflowTemplate.DefinitionJson), JsonNode.Parse(definition.PublishedJson!)));
        Assert.All(Workflow.Requests.Where(r => r.Tenant == s.Tenant.ToString("D") && r.Path.Contains("/definitions")),
            r => Assert.Equal("Bearer " + s.Token, r.Authorization));
    }

    [Fact]
    public async Task A_caller_without_the_design_keys_is_403_workflow_forbidden()
    {
        var s = new QuarantineScenario(_factory);
        Workflow.DesignForbidden.Add(s.Ledger.User);
        Assert.Equal("QUARANTINE_WORKFLOW_FORBIDDEN", QuarantineScenario.Code(await PrepareTemplateAsync(s, 403)));
    }

    // ── serial-tracked stock (FU-0175-Q1-SER) ──────────────────────────────────────────────────────────

    /// <summary>
    /// FU-0175-Q1-SER: the hold carries the intake's serial numbers; the release carries them to the ledger, which keeps the
    /// serials Active at the same bin. K3 (d): an engine that drops the serials → the ledger refuses (SERIAL_COUNT_MISMATCH) →
    /// ApplyFailed → this test is red.
    /// </summary>
    [Fact]
    public async Task A_serial_tracked_release_carries_the_holds_serials_and_applies()
    {
        var s = await WorldAsync(serialControlled: true);
        var lot = await s.ReleasedLotAsync();
        var serials = new[] { "QS-0001", "QS-0002", "QS-0003" };
        var (_, hold) = await s.HoldAsync("3", lot, serials: serials);
        var draft = await s.DecideAsync(Id(hold, "holdId"), [QuarantineScenario.Line(1, "Release", 3m)]);
        var submitted = await s.SubmitAsync(Id(draft, "decisionId"));
        Workflow.Approve(Id(submitted.GetProperty("workflow"), "instanceId"), s.Approver);

        var decision = await s.ExpectAsync(HttpMethod.Get, $"/api/quarantine/decisions/{Id(draft, "decisionId")}", null, 200);
        Assert.True(Id(decision, "state") == "Applied", decision.ToString());
        Assert.Single(await s.LedgerMovementsAsync(Id(draft, "decisionNumber")));
        Assert.Equal(serials, LedgerSerials(s, Id(draft, "decisionNumber")).Single());
        Assert.Equal(3m, await s.OnHandAsync("AVAILABLE"));
        Assert.All(serials, n => Assert.Equal("Active", s.Lots.Serial(n)["Status"].AsString));
    }

    [Fact]
    public async Task A_partial_serial_decision_splits_the_serials_in_intake_order_and_the_blocked_record_keeps_its_part()
    {
        var s = await WorldAsync(serialControlled: true);
        var lot = await s.ReleasedLotAsync();
        var (_, hold) = await s.HoldAsync("3", lot, serials: ["PS-0001", "PS-0002", "PS-0003"]);
        var draft = await s.DecideAsync(Id(hold, "holdId"), [QuarantineScenario.Line(1, "Release", 2m), QuarantineScenario.Line(2, "Reject", 1m)], "DEVIATION");
        var submitted = await s.SubmitAsync(Id(draft, "decisionId"));
        Workflow.Approve(Id(submitted.GetProperty("workflow"), "instanceId"), s.Approver);
        Assert.Equal("Applied", Id(await s.ExpectAsync(HttpMethod.Get, $"/api/quarantine/decisions/{Id(draft, "decisionId")}", null, 200), "state"));

        Assert.Equal(2, (await s.LedgerMovementsAsync(Id(draft, "decisionNumber"))).Count);
        var serials = LedgerSerials(s, Id(draft, "decisionNumber"));
        Assert.Equal(["PS-0001", "PS-0002"], serials[0]);
        Assert.Equal(["PS-0003"], serials[1]);
        var blocked = Assert.Single(s.Blocked(), b => b["OriginDecisionId"].AsString == Id(draft, "decisionId"));
        Assert.Equal(["PS-0003"], blocked["SerialIds"].AsBsonArray.Select(v => v.AsString));
    }

    /// <summary>The serial numbers the ledger stored per posted line (the transaction read model does not expose them).</summary>
    private static List<List<string>> LedgerSerials(QuarantineScenario s, string decisionNumber)
        => s.Ledger.Persisted("inventory_transactions")
            .Where(t => t.Contains("SourceDocumentId") && t["SourceDocumentId"].IsString && t["SourceDocumentId"].AsString == decisionNumber)
            .OrderBy(t => t["SourceLineId"].AsString)
            .Select(t => t["SerialIds"].AsBsonArray.Select(v => v.AsString).ToList())
            .ToList();

    [Fact]
    public async Task A_fractional_part_of_a_serial_tracked_hold_is_refused_at_draft()
    {
        var s = await WorldAsync(serialControlled: true);
        var (_, hold) = await s.HoldAsync("2", await s.ReleasedLotAsync(), serials: ["FR-0001", "FR-0002"]);
        var refused = await s.DecideAsync(Id(hold, "holdId"), [QuarantineScenario.Line(1, "Release", 1.5m), QuarantineScenario.Line(2, "Reject", 0.5m)], "DEVIATION", expected: 400);
        Assert.Equal("QUARANTINE_DECISION_INVALID", QuarantineScenario.Code(refused));
    }

    // ── completion event (consumer, broker-independent) ─────────────────────────────────────────────────

    private static EventTransportMessage Completed(Guid tenant, string decisionId, string instance, string outcome, string? by, string objectType = "quarantine.decision")
        => new(Guid.NewGuid(), QuarantineWorkflowOutcomeConsumer.CompletedEventName, 1, Guid.NewGuid(), null, tenant, "platform",
            DateTimeOffset.UtcNow, JsonSerializer.Serialize(new
            {
                tenantId = tenant, workflowInstanceId = instance, objectType, objectId = decisionId, outcome,
                completedAt = DateTimeOffset.UtcNow, completedBy = by, reasonCode = (string?)null
            }));

    private QuarantineWorkflowOutcomeConsumer Consumer() => _factory.Services.GetRequiredService<QuarantineWorkflowOutcomeConsumer>();

    [Fact]
    public async Task The_completion_event_applies_once_and_a_redelivery_is_a_duplicate()
    {
        var (s, _, decisionId, instance) = await SubmittedReleaseAsync("40", reconcileAfterSeconds: 3600);
        Workflow.Approve(instance, s.Approver);
        var message = Completed(s.Tenant, decisionId, instance, "approved", s.Approver.ToString("D"));

        Assert.Equal(QuarantineWorkflowOutcomeConsumer.Outcome.Processed, await Consumer().ConsumeAsync(message));
        Assert.Equal(QuarantineWorkflowOutcomeConsumer.Outcome.Duplicate, await Consumer().ConsumeAsync(message));
        var decision = await s.ExpectAsync(HttpMethod.Get, $"/api/quarantine/decisions/{decisionId}", null, 200);
        Assert.Equal("Applied", Id(decision, "state"));
        Assert.Single(await s.LedgerMovementsAsync(Id(decision, "decisionNumber")));
    }

    [Fact]
    public async Task An_unsigned_event_that_does_not_bind_to_the_open_round_is_never_applied()
    {
        var (s, _, decisionId, _) = await SubmittedReleaseAsync("40", reconcileAfterSeconds: 3600);
        var c = Consumer();

        // another instance id (forged / another round): reaches the applier, which refuses it
        await c.ConsumeAsync(Completed(s.Tenant, decisionId, Guid.NewGuid().ToString("D"), "approved", s.Approver.ToString("D")));
        // another tenant: the decision is not found there
        Assert.Equal(QuarantineWorkflowOutcomeConsumer.Outcome.Rejected,
            await c.ConsumeAsync(Completed(Guid.NewGuid(), decisionId, Guid.NewGuid().ToString("D"), "approved", s.Approver.ToString("D"))));
        // approved without a readable approver
        Assert.Equal(QuarantineWorkflowOutcomeConsumer.Outcome.Rejected,
            await c.ConsumeAsync(Completed(s.Tenant, decisionId, Guid.NewGuid().ToString("D"), "approved", "system")));
        // another module's object type
        Assert.Equal(QuarantineWorkflowOutcomeConsumer.Outcome.Ignored,
            await c.ConsumeAsync(Completed(s.Tenant, decisionId, Guid.NewGuid().ToString("D"), "approved", s.Approver.ToString("D"), "crm.claim")));

        Assert.Equal("PendingApproval", Id(await s.ExpectAsync(HttpMethod.Get, $"/api/quarantine/decisions/{decisionId}", null, 200), "state"));
        Assert.DoesNotContain(s.Events(), e => e["DecisionId"].AsString == decisionId && e["Kind"].AsString is "Approved" or "Applied");
    }

    [Fact]
    public void Without_RabbitMQ_no_bus_is_registered()
    {
        Assert.Null(_factory.Services.GetService(typeof(MassTransit.IBus)));
        Assert.Equal("InMemory", _factory.Services.GetRequiredService<Microsoft.Extensions.Options.IOptions<QuarantineEventingOptions>>().Value.Transport);
    }

    // ── Q4 (KQ-I): a gated lot release on the production Workflow port ─────────────────────────────────

    [Fact]
    public async Task A_gated_lot_release_starts_its_approval_with_the_lot_page_link_and_is_applied_at_read_time()
    {
        var s = await WorldAsync();
        LotGate.On(_factory.Database, s.Tenant, s.Ledger.LegalEntity);
        var lot = await s.Lots.CreateLotAsync();
        var decisionId = Id(await s.Lots.SetStatusAsync(lot, "Released", expected: 202), "pendingRequestId");

        var submitted = await s.SubmitAsync(decisionId);
        var body = Assert.Single(Workflow.StartBodies, b => b["objectId"]!.GetValue<string>() == decisionId);
        Assert.Equal("QUARANTINE_LOT_RELEASE", body["reasonCode"]!.GetValue<string>());
        var display = body["displayContext"]!.AsObject();
        Assert.Equal($"/Lots/Details?lotId={lot}", display["deepLinkUrl"]!.GetValue<string>());
        Assert.Contains("Lot serbest bırakma", display["title"]!.GetValue<string>());
        Assert.Equal("Draft", s.Lots.LotDocument(lot)["Status"].AsString);

        Workflow.Approve(Id(submitted.GetProperty("workflow"), "instanceId"), s.Approver);
        // Q5 runtime finding: the MOD-0174 lot page reads only quality-status — that read alone applies the approved release
        var status = await s.ExpectAsync(HttpMethod.Get, $"/api/quarantine/lots/{lot}/quality-status", null, 200);
        Assert.False(status.TryGetProperty("pendingLotRelease", out _));
        Assert.Equal("Released", s.Lots.LotDocument(lot)["Status"].AsString);
        var decision = await s.ExpectAsync(HttpMethod.Get, $"/api/quarantine/decisions/{decisionId}", null, 200);
        Assert.Equal("Applied", Id(decision, "state"));
    }
}
