using System.Text.Json;
using Diten.InventoryService.Application.Features.Recall.Ports;
using Diten.InventoryService.Application.Features.Recall.Services;
using Diten.InventoryService.Domain.Recall.Entities;
using Diten.InventoryService.Persistence.Recall;
using Microsoft.Extensions.DependencyInjection;
using MongoDB.Bson;
using MongoDB.Driver;
using Xunit;

namespace Diten.InventoryService.Tests.Recall;

/// <summary>
/// WP-0177-R5 (pack §4.11, §4.12, §8.8 dr-v1, §8.10 KR-G, CT-R17) — the drills through the real host, the real Mongo stores and the PRODUCTION
/// MOD-0022 gate over the in-memory MOD-0022: plans, the server-timed run, the step order, the single running run, the Auto / Manual pick,
/// pass / fail at the target, the target at start, the dr-v1 signature rules, the provider, cancel and the reads. The read-only write count is
/// <see cref="RecallDrillReadOnlyTests"/>. Tests of one class run one after another (the clock and the seed are the factory's).
/// </summary>
public sealed class RecallDrillApiTests : IClassFixture<RecallDrillApiFactory>
{
    private readonly RecallDrillApiFactory _factory;

    public RecallDrillApiTests(RecallDrillApiFactory factory) => _factory = factory;

    private static void Refused((int Status, JsonElement Body, HttpResponseMessage Raw) answer, int status, string code, string because)
        => Assert.True(answer.Status == status && RecallScenario.Code(answer.Body) == code, $"{because}: {answer.Status} {answer.Body}");

    // ── plans ────────────────────────────────────────────────────────────────────────────────────────────

    [Fact]
    public async Task Plans_are_created_listed_and_updated_with_If_Match_bound_to_the_company_and_the_keys()
    {
        var w = await DrillWorld.CreateAsync(_factory);
        var body = w.PlanBody(frequency: "CustomDays", customDays: 45, name: "Hammadde tatbikati");
        var (s1, plan, raw) = await w.CreatePlanAsync(body);
        Assert.True(s1 == 201, plan.ToString());
        Assert.Equal("DP-01", plan.GetProperty("code").GetString());
        Assert.Equal(("CustomDays", 45, "RandomFinishedGoodLot", "2026-12-31", true, 1),
            (plan.GetProperty("frequency").GetString(), plan.GetProperty("customDays").GetInt32(), plan.GetProperty("scopeKind").GetString(),
             plan.GetProperty("nextDueOn").GetString(), plan.GetProperty("active").GetBoolean(), plan.GetProperty("version").GetInt32()));
        Assert.Equal("W/\"1\"", raw.Headers.ETag!.ToString());
        Assert.Equal(w.S.Qa1.ToString("D"), plan.GetProperty("owner").GetProperty("userId").GetString());

        // the same key + the same body → the first plan (200); another body → 400
        var (s2, again, _) = await w.CreatePlanAsync(body);
        Assert.True(s2 == 200 && again.GetProperty("planId").GetString() == DrillWorld.PlanId(plan), again.ToString());
        var reused = new Dictionary<string, object?>(body) { ["name"] = "Baska ad" };
        Refused(await w.CreatePlanAsync(reused), 400, "IDEMPOTENCY_KEY_REUSED", "same key, other body");
        Refused(await w.CreatePlanAsync(w.PlanBody(frequency: "CustomDays")), 400, "INVALID_REQUEST", "CustomDays needs customDays");
        Refused(await w.CreatePlanAsync(w.PlanBody(frequency: "Quarterly", customDays: 10)), 400, "INVALID_REQUEST", "customDays only for CustomDays");
        Refused(await w.CreatePlanAsync(w.PlanBody(frequency: "Monthly")), 400, "INVALID_REQUEST", "frequency is the contract enum");
        Assert.Equal("DP-02", (await w.PlanAsync("RawMaterialLot")).GetProperty("code").GetString());

        var list = await w.S.ExpectAsync(HttpMethod.Get, "/api/recall/drill-plans", w.Runner, null, 200);
        Assert.Equal(["DP-01", "DP-02"], list.EnumerateArray().Select(p => p.GetProperty("code").GetString()));

        // update: If-Match, the code never changes; a stale ETag → 409
        var id = DrillWorld.PlanId(plan);
        var update = w.PlanBody(frequency: "Yearly2", name: "Yilda iki", active: false);
        var (s3, updated, _) = await w.S.SendAsync(HttpMethod.Put, $"/api/recall/drill-plans/{id}", w.Runner, Clean(update), (string)update["idempotencyKey"]!, "W/\"1\"");
        Assert.True(s3 == 200, updated.ToString());
        Assert.Equal(("DP-01", "Yearly2", false, 2), (updated.GetProperty("code").GetString(), updated.GetProperty("frequency").GetString(),
            updated.GetProperty("active").GetBoolean(), updated.GetProperty("version").GetInt32()));
        var stale = w.PlanBody(name: "Eski surum");
        Refused(await w.S.SendAsync(HttpMethod.Put, $"/api/recall/drill-plans/{id}", w.Runner, Clean(stale), (string)stale["idempotencyKey"]!, "W/\"1\""), 409, "RECALL_CONFLICT", "stale If-Match");
        Refused(await w.S.SendAsync(HttpMethod.Put, $"/api/recall/drill-plans/{Guid.NewGuid():D}", w.Runner, Clean(stale), (string)stale["idempotencyKey"]!, "W/\"1\""),
            404, "RECALL_DRILL_NOT_FOUND", "unknown plan");
        Assert.Single(w.Audit("recall.drill-plan.saved"), a => a["SubjectNumber"] == "DP-01" && a["Detail"]["version"] == "2");

        // an inactive plan cannot start
        Refused(await w.StartAsync(id), 400, "INVALID_REQUEST", "inactive plan");

        // company scope: another company of the tenant → 403 CROSS_LE_FORBIDDEN, another tenant → 404; its list is empty
        var otherCompany = w.S.Mint(w.S.Qa2, DrillWorld.RunnerKeys, Guid.NewGuid());
        var otherTenant = w.S.Mint(Guid.NewGuid(), DrillWorld.RunnerKeys, Guid.NewGuid(), Guid.NewGuid());
        Refused(await w.S.SendAsync(HttpMethod.Put, $"/api/recall/drill-plans/{id}", otherCompany, Clean(stale), (string)stale["idempotencyKey"]!, "W/\"2\""),
            403, "CROSS_LE_FORBIDDEN", "another company");
        Refused(await w.S.SendAsync(HttpMethod.Put, $"/api/recall/drill-plans/{id}", otherTenant, Clean(stale), (string)stale["idempotencyKey"]!, "W/\"2\""),
            404, "RECALL_DRILL_NOT_FOUND", "another tenant");
        Assert.Empty((await w.S.ExpectAsync(HttpMethod.Get, "/api/recall/drill-plans", otherCompany, null, 200)).EnumerateArray());

        // the keys: read / manage / run (no key → 403)
        var reader = w.S.Mint(w.S.Management, ["recall.drills.read"]);
        var nobody = w.S.Mint(w.S.Management, ["recall.cases.read"]);
        Assert.Equal(403, (await w.S.SendAsync(HttpMethod.Get, "/api/recall/drill-plans", nobody)).Status);
        Assert.Equal(403, (await w.S.SendAsync(HttpMethod.Get, "/api/recall/drill-runs", nobody)).Status);
        Assert.Equal(403, (await w.CreatePlanAsync(w.PlanBody(), reader)).Status);
        Assert.Equal(403, (await w.StartAsync(DrillWorld.PlanId(await w.PlanAsync()), token: reader)).Status);
        Assert.Equal(200, (await w.S.SendAsync(HttpMethod.Get, "/api/recall/drill-runs", reader)).Status);
    }

    // ── the server clock ─────────────────────────────────────────────────────────────────────────────────

    [Fact]
    public async Task The_server_clock_times_the_run_and_a_client_time_member_is_refused()
    {
        var w = await DrillWorld.CreateAsync(_factory);
        var planId = DrillWorld.PlanId(await w.PlanAsync());

        // K3 sabotage (b1): take StartedAtUtc / EndedAtUtc from the body → these are accepted and the exact-time assertions below are red
        Refused(await w.StartAsync(planId, extra: new() { ["startedAtUtc"] = "2020-01-01T00:00:00Z" }), 400, "INVALID_REQUEST", "a client start time");
        _factory.Clock.Advance(TimeSpan.FromMinutes(7)); // the server clock is NOT the system clock (b2 would land 7 minutes off)
        var started = _factory.Clock.Freeze();
        var run = await w.StartOkAsync(planId);
        // K3 sabotage (b2): the system clock instead of the injected server clock → StartedAtUtc is not the frozen instant
        Assert.Equal(RecallSupport.Wire(started), run.GetProperty("startedAtUtc").GetString());
        Assert.Equal("Running", run.GetProperty("status").GetString());
        Assert.False(run.TryGetProperty("endedAtUtc", out _));

        run = await w.StepOkAsync(run, "Trace");
        run = await w.StepOkAsync(run, "Scope");
        Refused(await w.StepAsync(run, "CustomerList", extra: new() { ["endedAtUtc"] = RecallSupport.Wire(started.AddMinutes(5)) }), 400, "INVALID_REQUEST", "a client end time");
        _factory.Clock.Advance(TimeSpan.FromMinutes(90) + TimeSpan.FromSeconds(30));
        run = await w.StepOkAsync(run, "CustomerList");
        Assert.Equal(RecallSupport.Wire(started.AddMinutes(90).AddSeconds(30)), run.GetProperty("endedAtUtc").GetString());
        Assert.Equal(91, run.GetProperty("durationMinutes").GetInt32()); // ceil(90.5)
        Assert.Equal(RecallSupport.Wire(started.AddMinutes(90).AddSeconds(30)), run.GetProperty("steps")[2].GetProperty("completedAtUtc").GetString());
        _factory.Clock.Reset();
    }

    // ── steps ────────────────────────────────────────────────────────────────────────────────────────────

    [Fact]
    public async Task The_steps_run_strictly_Trace_Scope_CustomerList_and_the_trace_is_sealed_as_a_Drill_snapshot()
    {
        var w = await DrillWorld.CreateAsync(_factory);
        var run = await w.StartOkAsync(DrillWorld.PlanId(await w.PlanAsync()), "Manual", w.Trace.Fg);
        Assert.Equal("Trace", run.GetProperty("nextStep").GetString());

        // K3 sabotage (d): drop the step-order check → Scope / CustomerList before Trace pass
        Refused(await w.StepAsync(run, "Scope"), 409, "RECALL_DRILL_STEP_ORDER", "Scope before Trace");
        Refused(await w.StepAsync(run, "CustomerList"), 409, "RECALL_DRILL_STEP_ORDER", "CustomerList before Trace");
        Refused(await w.StepAsync(run, "Trace", ifMatch: "W/\"9\""), 409, "RECALL_CONFLICT", "stale If-Match");
        Assert.Equal(404, (await w.StepAsync(run, "Kapsam")).Status);

        var traced = await w.StepOkAsync(run, "Trace");
        var snapshot = traced.GetProperty("snapshot");
        Assert.Equal(("Drill", "rt-v1"), (snapshot.GetProperty("purpose").GetString(), snapshot.GetProperty("canonicalVersion").GetString()));
        var sealedRow = Assert.Single(w.Rows(RecallCollections.TraceSnapshots));
        Assert.Equal(("Drill", DrillWorld.RunId(run), Guid.Empty.ToString("D")), (sealedRow["Purpose"].AsString, sealedRow["DrillRunId"].AsString, sealedRow["CaseId"].AsString));
        Assert.Equal(snapshot.GetProperty("sha256").GetString(), sealedRow["Sha256"].AsString);
        Assert.Equal("Both", sealedRow["Direction"].AsString);
        Assert.Equal(sealedRow["GapCount"].AsInt32, traced.GetProperty("gapCount").GetInt32());
        Assert.True(traced.GetProperty("scopeSummary").GetProperty("lotCount").GetInt32() >= 2); // Fg + its raw-material input (both directions)
        Assert.False(traced.TryGetProperty("customers", out _));
        Assert.Single(w.Audit("recall.trace.run"), a => a["Detail"]["purpose"] == "Drill");

        Refused(await w.StepAsync(traced, "Trace"), 409, "RECALL_DRILL_STEP_ORDER", "Trace twice");
        Refused(await w.StepAsync(traced, "CustomerList"), 409, "RECALL_DRILL_STEP_ORDER", "CustomerList before Scope");
        var scoped = await w.StepOkAsync(traced, "Scope");
        Assert.Empty(w.Rows(RecallCollections.ScopeItems)); // the Scope step writes no case scope
        var listed = await w.StepOkAsync(scoped, "CustomerList");
        Assert.Single(listed.GetProperty("customers").EnumerateArray());
        Assert.Equal(["Trace", "Scope", "CustomerList"], listed.GetProperty("steps").EnumerateArray().Select(s => s.GetProperty("step").GetString()));
        Assert.False(listed.TryGetProperty("nextStep", out _));
        Refused(await w.StepAsync(listed, "CustomerList"), 409, "RECALL_DRILL_STEP_ORDER", "all three done");
        Assert.Equal(3, w.Audit("recall.drill.step").Count);
    }

    // ── one running run ──────────────────────────────────────────────────────────────────────────────────

    [Fact]
    public async Task One_running_drill_per_company_and_a_cancelled_one_frees_the_slot()
    {
        var w = await DrillWorld.CreateAsync(_factory);
        var planId = DrillWorld.PlanId(await w.PlanAsync());
        var first = await w.StartOkAsync(planId);
        Assert.Matches(@"^RC-\d{4}-D001$", first.GetProperty("number").GetString());

        // K3 sabotage (h): drop the running check and the partial unique index → a second Running run opens
        Refused(await w.StartAsync(planId), 409, "RECALL_DRILL_ALREADY_RUNNING", "a second running drill");
        var racers = await Task.WhenAll(Enumerable.Range(0, 3).Select(_ => w.StartAsync(planId)));
        Assert.All(racers, r => Refused(r, 409, "RECALL_DRILL_ALREADY_RUNNING", "concurrent starts"));
        Assert.Single(w.Rows(RecallCollections.DrillRuns, Builders<BsonDocument>.Filter.Eq("Status", "Running")));

        // another company of the tenant runs its own drill
        var otherCompany = w.S.Mint(w.S.Qa2, DrillWorld.RunnerKeys, Guid.NewGuid());
        var otherPlan = await w.CreatePlanAsync(w.PlanBody(scopeKind: "SelectedLot"), otherCompany);
        Assert.Equal(201, otherPlan.Status);

        var (sc, cancelled, _) = await w.CancelAsync(first);
        Assert.True(sc == 200, cancelled.ToString());
        var second = await w.StartOkAsync(planId);
        Assert.Matches(@"^RC-\d{4}-D002$", second.GetProperty("number").GetString());

        // the same key + the same body → the first run (200), another body → 400
        var key = RecallScenario.Key("dr");
        await w.CancelAsync(second);
        var (s1, once, _) = await w.StartAsync(planId, key: key);
        Assert.Equal(201, s1);
        var (s2, twice, _) = await w.StartAsync(planId, key: key);
        Assert.True(s2 == 200 && DrillWorld.RunId(twice) == DrillWorld.RunId(once), twice.ToString());
        Refused(await w.StartAsync(planId, "Manual", w.Trace.Fg, key: key), 400, "IDEMPOTENCY_KEY_REUSED", "same key, other body");
    }

    // ── the pick ─────────────────────────────────────────────────────────────────────────────────────────

    [Fact]
    public async Task Auto_is_the_seeded_random_pick_of_the_candidates_audited_and_Manual_is_the_users_lot()
    {
        var w = await DrillWorld.CreateAsync(_factory);
        var finished = new List<string> { w.Trace.Fg, await w.FinishedGoodAsync(), await w.FinishedGoodAsync(), await w.FinishedGoodAsync() };
        finished.Sort(StringComparer.Ordinal);
        var planId = DrillWorld.PlanId(await w.PlanAsync());

        _factory.Random.Seed(20261014);
        var expected = finished[new Random(20261014).Next(finished.Count)];
        var run = await w.StartOkAsync(planId);
        Assert.Equal(("Auto", expected, 4), (run.GetProperty("selectionMode").GetString(), run.GetProperty("lot").GetProperty("lotId").GetString(),
            run.GetProperty("candidateCount").GetInt32()));
        var audit = Assert.Single(w.Audit("recall.drill.started"));
        Assert.Equal(("4", finished.IndexOf(expected).ToString(System.Globalization.CultureInfo.InvariantCulture), expected),
            (audit["Detail"]["candidateCount"].AsString, audit["Detail"]["pickIndex"].AsString, audit["Detail"]["lotId"].AsString));
        await w.CancelAsync(run);

        // raw material: the only received lot with genealogy children is Rm
        var raw = await w.StartOkAsync(DrillWorld.PlanId(await w.PlanAsync("RawMaterialLot")));
        Assert.Equal((w.Trace.Rm, 1), (raw.GetProperty("lot").GetProperty("lotId").GetString(), raw.GetProperty("candidateCount").GetInt32()));
        await w.CancelAsync(raw);

        // Manual: the user's lot; an unknown lot → 404; Auto never takes a lot; a SelectedLot plan needs Manual
        var manual = await w.StartOkAsync(planId, "Manual", w.Trace.Rm);
        Assert.Equal(("Manual", w.Trace.Rm), (manual.GetProperty("selectionMode").GetString(), manual.GetProperty("lot").GetProperty("lotId").GetString()));
        Assert.False(manual.TryGetProperty("candidateCount", out _));
        await w.CancelAsync(manual);
        Refused(await w.StartAsync(planId, "Manual", Guid.NewGuid().ToString("D")), 404, "UNKNOWN_LOT", "an unknown lot");
        Refused(await w.StartAsync(planId, "Manual"), 400, "INVALID_REQUEST", "Manual without a lot");
        Refused(await w.StartAsync(planId, "Auto", w.Trace.Fg), 400, "INVALID_REQUEST", "Auto with a lot");
        Refused(await w.StartAsync(DrillWorld.PlanId(await w.PlanAsync("SelectedLot"))), 400, "INVALID_REQUEST", "SelectedLot plan with Auto");
        Refused(await w.StartAsync(Guid.NewGuid().ToString("D")), 404, "RECALL_DRILL_NOT_FOUND", "an unknown plan");

        // a company with no movements has no candidate → 422
        var empty = w.S.Mint(w.S.Qa1, DrillWorld.RunnerKeys, Guid.NewGuid());
        var (sp, emptyPlan, _) = await w.CreatePlanAsync(w.PlanBody(), empty);
        Assert.Equal(201, sp);
        Refused(await w.StartAsync(DrillWorld.PlanId(emptyPlan), token: empty), 422, "RECALL_DRILL_NO_CANDIDATE", "no candidate");
    }

    // ── pass / fail ──────────────────────────────────────────────────────────────────────────────────────

    [Fact]
    public async Task Pass_is_duration_within_the_target_minutes_inclusive_and_one_minute_more_fails()
    {
        var w = await DrillWorld.CreateAsync(_factory);
        var planId = DrillWorld.PlanId(await w.PlanAsync());
        _factory.Clock.Freeze();
        var atTarget = await w.StepsAsync(await w.StartOkAsync(planId), () => _factory.Clock.Advance(TimeSpan.FromHours(4)));
        Assert.Equal((4, 240, true), (atTarget.GetProperty("targetHours").GetInt32(), atTarget.GetProperty("durationMinutes").GetInt32(), atTarget.GetProperty("passed").GetBoolean()));
        await w.CancelAsync(atTarget);

        var over = await w.StepsAsync(await w.StartOkAsync(planId), () => _factory.Clock.Advance(TimeSpan.FromMinutes(241)));
        Assert.Equal((241, false), (over.GetProperty("durationMinutes").GetInt32(), over.GetProperty("passed").GetBoolean()));
        await w.CancelAsync(over);

        var justOver = await w.StepsAsync(await w.StartOkAsync(planId), () => _factory.Clock.Advance(TimeSpan.FromMinutes(240) + TimeSpan.FromSeconds(1)));
        Assert.Equal((241, false), (justOver.GetProperty("durationMinutes").GetInt32(), justOver.GetProperty("passed").GetBoolean())); // ceil
        _factory.Clock.Reset();
    }

    [Fact]
    public async Task The_target_is_the_settings_value_when_the_run_started()
    {
        var w = await DrillWorld.CreateAsync(_factory);
        var planId = DrillWorld.PlanId(await w.PlanAsync());
        _factory.Clock.Freeze();
        var run = await w.StartOkAsync(planId);
        Assert.Equal(4, run.GetProperty("targetHours").GetInt32());

        // the company lowers the target to 1 h while the run is open
        var settings = await w.S.ExpectAsync(HttpMethod.Get, "/api/recall/settings", w.Admin, null, 200);
        var key = RecallScenario.Key("st");
        await w.S.ExpectAsync(HttpMethod.Put, "/api/recall/settings", w.Admin, new Dictionary<string, object?>
        {
            ["idempotencyKey"] = key, ["drillTargetHours"] = 1, ["drillFrequencyWarningDays"] = 180,
            ["effectivenessTargetPercent"] = settings.GetProperty("effectivenessTargetPercent"), ["traceMaxDepth"] = 20, ["traceMaxNodes"] = 5000,
            ["reason"] = "Tatbikat hedefi bir saate indirildi"
        }, 200, key, "\"0\"");

        // K3 sabotage (e): read TargetHours from the current settings → 1 h, 120 minutes fail
        var done = await w.StepsAsync(run, () => _factory.Clock.Advance(TimeSpan.FromMinutes(120)));
        Assert.Equal((4, 120, true), (done.GetProperty("targetHours").GetInt32(), done.GetProperty("durationMinutes").GetInt32(), done.GetProperty("passed").GetBoolean()));
        await w.CancelAsync(done);
        Assert.Equal(1, (await w.StartOkAsync(planId)).GetProperty("targetHours").GetInt32()); // a NEW run takes the new value
        _factory.Clock.Reset();
    }

    // ── dr-v1 signature (F3-A over the R4 port) ───────────────────────────────────────────────────────────

    [Fact]
    public async Task The_result_is_signed_as_dr_v1_and_each_binding_rule_refuses()
    {
        var w = await DrillWorld.CreateAsync(_factory);
        var planId = DrillWorld.PlanId(await w.PlanAsync());

        // run B of the same company: finished and cancelled (one running run per company); signed below on its CURRENT subject
        var other = await w.FinishedRunAsync(planId);
        Assert.Equal(200, (await w.CancelAsync(other)).Status);

        var run = await w.FinishedRunAsync(planId);
        var id = DrillWorld.RunId(run);

        Refused(await w.ResultAsync(id, RecallScenario.ETag(run), null), 400, "RECALL_SIGNATURE_REQUIRED", "required by the policy");
        Refused(await w.ResultAsync(id, RecallScenario.ETag(run), "sig-x"), 409, "RECALL_PENDING_ACTION_MISMATCH", "not prepared");
        Refused(await w.PrepareAsync(run, "kisa"), 400, "REASON_REQUIRED", "note < 10");
        Refused(await w.PrepareAsync(run, new string('n', 2001)), 400, "REASON_REQUIRED", "note > 2000");

        // stale hash: signed on the subject BEFORE the preparation (version + note differ)
        var beforePrepare = await w.SignAsync(id);
        var prepared = await w.PrepareOkAsync(run);
        Assert.Equal(("SignDrillResult", "recall.drill.result", true, id), (prepared.GetProperty("action").GetString(), prepared.GetProperty("actionCode").GetString(),
            prepared.GetProperty("signatureRequired").GetBoolean(), prepared.GetProperty("caseId").GetString()));
        Assert.Equal(("recall", "recall.drill-run", id), (prepared.GetProperty("subject").GetProperty("module").GetString(),
            prepared.GetProperty("subject").GetProperty("subjectType").GetString(), prepared.GetProperty("subject").GetProperty("subjectId").GetString()));
        var etag = RecallScenario.ETag(prepared);
        Refused(await w.ResultAsync(id, etag, beforePrepare), 409, "RECALL_SIGNATURE_MISMATCH", "stale subject hash");
        Refused(await w.ResultAsync(id, etag, await w.SignAsync(id, hash: new string('0', 64))), 409, "RECALL_SIGNATURE_MISMATCH", "another hash");

        // another run's correct signature — B's current dr-v1 hash, signed after A's preparation by the sender (K3 sabotage (g): verify against the
        // signature's own run → it passes)
        var otherSignature = await w.SignAsync(DrillWorld.RunId(other));
        Refused(await w.ResultAsync(id, etag, otherSignature), 409, "RECALL_SIGNATURE_MISMATCH", "another run's signature");

        // signer ≠ sender; signed before the preparation; another meaning; invalidated
        Refused(await w.ResultAsync(id, etag, await w.SignAsync(id, signer: w.S.Qa2, token: w.Runner2)), 409, "RECALL_SIGNATURE_MISMATCH", "signer is not the sender");
        Refused(await w.ResultAsync(id, etag, await w.SignAsync(id, signedAt: _factory.Clock.GetUtcNow().AddMinutes(-1))), 409, "RECALL_SIGNATURE_MISMATCH", "signed before the preparation");
        Refused(await w.ResultAsync(id, etag, await w.SignAsync(id, meaning: "REVIEW")), 409, "RECALL_SIGNATURE_MISMATCH", "meaning ≠ the policy's");
        var invalidated = await w.SignAsync(id);
        _factory.ESign.Invalidate(invalidated);
        Refused(await w.ResultAsync(id, etag, invalidated), 409, "RECALL_SIGNATURE_MISMATCH", "invalidated");
        Refused(await w.ResultAsync(id, etag, await w.SignAsync(id), note: "Baska bir sonuc notu yazildi"), 409, "RECALL_PENDING_ACTION_MISMATCH", "another note than prepared");

        // single use: a correct signature already recorded on a drill event of the company is refused
        var used = await w.SignAsync(id);
        _factory.Database.GetCollection<RecallDrillEvent>(RecallCollections.DrillEvents).InsertOne(new RecallDrillEvent
        {
            Id = Guid.NewGuid(), TenantId = w.Tenant, LegalEntityId = w.S.Ledger.LegalEntity, SubjectId = Guid.NewGuid(), Kind = Diten.InventoryService.Domain.Recall.Enums.RecallDrillEventKind.Completed,
            ActorUserId = w.S.Qa1, AtUtc = DateTimeOffset.UtcNow, SignatureId = used
        });
        Refused(await w.ResultAsync(id, etag, used), 409, "RECALL_SIGNATURE_MISMATCH", "already used");

        // the correct signature completes the run; 0177 keeps only the id
        var good = await w.SignAsync(id);
        var (sc, completed, _) = await w.ResultAsync(id, etag, good);
        Assert.True(sc == 200, completed.ToString());
        Assert.Equal(("Completed", good, DrillWorld.Note), (completed.GetProperty("status").GetString(), completed.GetProperty("resultSignatureId").GetString(),
            completed.GetProperty("resultNote").GetString()));
        Assert.Equal(good, Assert.Single(w.Rows(RecallCollections.DrillEvents, Builders<BsonDocument>.Filter.Eq("Kind", "Completed")
            & Builders<BsonDocument>.Filter.Eq("SubjectId", id)))["SignatureId"].AsString);
        Assert.Single(w.Audit("recall.drill.completed"), a => a["Detail"]["signatureId"] == good);

        // the used signature is refused on the next run (single use, by the records)
        var next = await w.FinishedRunAsync(planId);
        var nextPrepared = await w.PrepareOkAsync(next);
        Refused(await w.ResultAsync(DrillWorld.RunId(next), RecallScenario.ETag(nextPrepared), good), 409, "RECALL_SIGNATURE_MISMATCH", "used again");
        // no four-eyes rule: the runner who prepared signs
        var (sn, nextDone, _) = await w.ResultAsync(DrillWorld.RunId(next), RecallScenario.ETag(nextPrepared), await w.SignAsync(DrillWorld.RunId(next)));
        Assert.True(sn == 200, nextDone.ToString());
    }

    [Fact]
    public async Task Not_required_by_the_policy_the_result_completes_unsigned()
    {
        var w = await DrillWorld.CreateAsync(_factory, resultRequired: false);
        var run = await w.FinishedRunAsync();
        var prepared = await w.PrepareOkAsync(run);
        Assert.False(prepared.GetProperty("signatureRequired").GetBoolean());
        var (sc, completed, _) = await w.ResultAsync(DrillWorld.RunId(run), RecallScenario.ETag(prepared), null);
        Assert.True(sc == 200, completed.ToString());
        Assert.Equal("Completed", completed.GetProperty("status").GetString());
        Assert.False(completed.TryGetProperty("resultSignatureId", out _));
        Refused(await w.ResultAsync(DrillWorld.RunId(run), RecallScenario.ETag(completed), null), 409, "RECALL_INVALID_TRANSITION", "a completed run never changes");
    }

    // ── the subject provider ─────────────────────────────────────────────────────────────────────────────

    [Fact]
    public async Task The_provider_answers_dr_v1_of_the_stored_run_and_another_tenant_gets_404()
    {
        var w = await DrillWorld.CreateAsync(_factory);
        var run = await w.FinishedRunAsync();
        var id = DrillWorld.RunId(run);
        var prepared = await w.PrepareOkAsync(run);

        var subject = await w.SubjectAsync(id);
        var fields = subject.GetProperty("fields");
        Assert.Equal(["durationMinutes", "endedAtUtc", "gapCount", "lotId", "lotNumber", "number", "passed", "planCode", "resultNote", "snapshotSha256", "startedAtUtc",
            "targetHours", "version"], fields.EnumerateObject().Select(p => p.Name).OrderBy(n => n, StringComparer.Ordinal));
        Assert.Equal((DrillWorld.Note, prepared.GetProperty("version").GetInt32().ToString(System.Globalization.CultureInfo.InvariantCulture), run.GetProperty("number").GetString()),
            (fields.GetProperty("resultNote").GetString(), fields.GetProperty("version").GetString(), fields.GetProperty("number").GetString()));
        Assert.Equal(w.S.Ledger.LegalEntity.ToString("D"), subject.GetProperty("legalEntityId").GetString());

        // the provider's fields are exactly what the verification hashes (F3-A)
        await using (var scope = _factory.Services.CreateAsyncScope())
        {
            var stored = await _factory.Database.GetCollection<RecallDrillRun>(RecallCollections.DrillRuns).Find(r => r.Id == Guid.Parse(id)).SingleAsync();
            Assert.Equal(RecallSignableSubjects.DrillRunHash(stored), RecallESignTests.PlatformHash(RecallSignableActions.DrillRunSubjectType, id, fields));
        }

        // another tenant → 404; another company of the tenant naming its company → 404; no key → 404 (one answer, no oracle)
        var otherTenant = w.S.Mint(Guid.NewGuid(), DrillWorld.RunnerKeys, Guid.NewGuid(), Guid.NewGuid());
        await w.SubjectAsync(id, otherTenant, 404);
        await w.SubjectAsync(id, w.S.Mint(w.S.Qa1, ["recall.drills.read"]), 404);
        await w.SubjectAsync(Guid.NewGuid().ToString("D"), w.Runner, 404);

        // K3 sabotage (g) of R4 for the drill type: the directory is tenant-bound (defense in depth beside the company scope)
        await using var direct = _factory.Services.CreateAsyncScope();
        var directory = direct.ServiceProvider.GetRequiredService<IRecallSubjectDirectory>();
        Assert.NotNull(await directory.SubjectAsync(w.Tenant, RecallSignableActions.DrillRunSubjectType, Guid.Parse(id)));
        Assert.Null(await directory.SubjectAsync(Guid.NewGuid(), RecallSignableActions.DrillRunSubjectType, Guid.Parse(id)));
    }

    // ── cancel + closed runs ─────────────────────────────────────────────────────────────────────────────

    [Fact]
    public async Task A_cancelled_run_is_kept_and_a_closed_run_never_changes()
    {
        var w = await DrillWorld.CreateAsync(_factory);
        var planId = DrillWorld.PlanId(await w.PlanAsync());
        var run = await w.StepOkAsync(await w.StartOkAsync(planId), "Trace");
        Refused(await w.CancelAsync(run, null), 400, "REASON_REQUIRED", "the reason is mandatory");
        Refused(await w.CancelAsync(run, "kisa"), 400, "REASON_REQUIRED", "reason < 10");
        var (sc, cancelled, _) = await w.CancelAsync(run);
        Assert.True(sc == 200, cancelled.ToString());
        Assert.Equal(("Cancelled", DrillWorld.CancelReason, w.S.Qa1.ToString("D")), (cancelled.GetProperty("status").GetString(),
            cancelled.GetProperty("cancelReason").GetString(), cancelled.GetProperty("cancelled").GetProperty("userId").GetString()));
        Assert.Single(w.Rows(RecallCollections.DrillRuns, Builders<BsonDocument>.Filter.Eq("Status", "Cancelled"))); // CT-R17: kept

        Refused(await w.CancelAsync(cancelled), 409, "RECALL_INVALID_TRANSITION", "cancel twice");
        Refused(await w.StepAsync(cancelled, "Scope"), 409, "RECALL_INVALID_TRANSITION", "a step on a cancelled run");
        Refused(await w.PrepareAsync(cancelled), 409, "RECALL_INVALID_TRANSITION", "prepare on a cancelled run");

        var listed = await w.S.ExpectAsync(HttpMethod.Get, "/api/recall/drill-runs?status=Cancelled", w.Runner, null, 200);
        Assert.Equal(DrillWorld.RunId(run), Assert.Single(listed.GetProperty("items").EnumerateArray()).GetProperty("runId").GetString());

        // a Completed run: no step, no cancel, no second result
        var done = await w.FinishedRunAsync(planId);
        var prepared = await w.PrepareOkAsync(done);
        var (sd, completed, _) = await w.ResultAsync(DrillWorld.RunId(done), RecallScenario.ETag(prepared), await w.SignAsync(DrillWorld.RunId(done)));
        Assert.True(sd == 200, completed.ToString());
        Refused(await w.CancelAsync(completed), 409, "RECALL_INVALID_TRANSITION", "cancel a completed run");
        Refused(await w.StepAsync(completed, "Trace"), 409, "RECALL_INVALID_TRANSITION", "a step on a completed run");
        Refused(await w.PrepareAsync(completed), 409, "RECALL_INVALID_TRANSITION", "prepare on a completed run");
        var before = Assert.Single(w.Rows(RecallCollections.DrillRuns, Builders<BsonDocument>.Filter.Eq("_id", DrillWorld.RunId(done))));
        Assert.Equal(completed.GetProperty("version").GetInt32(), before["Version"].AsInt32);
    }

    // ── reads ────────────────────────────────────────────────────────────────────────────────────────────

    [Fact]
    public async Task Runs_are_listed_by_status_with_paging_and_the_frequency_warning_follows_the_last_completed_run()
    {
        var w = await DrillWorld.CreateAsync(_factory);
        var planId = DrillWorld.PlanId(await w.PlanAsync());
        var page = await w.S.ExpectAsync(HttpMethod.Get, "/api/recall/drill-runs", w.Runner, null, 200);
        Assert.Equal((0, true, 180), (page.GetProperty("total").GetInt32(), page.GetProperty("frequencyWarning").GetProperty("warning").GetBoolean(),
            page.GetProperty("frequencyWarning").GetProperty("warningDays").GetInt32())); // no completed run yet

        var cancelled = await w.StartOkAsync(planId);
        await w.CancelAsync(cancelled);
        var done = await w.FinishedRunAsync(planId);
        var prepared = await w.PrepareOkAsync(done);
        Assert.Equal(200, (await w.ResultAsync(DrillWorld.RunId(done), RecallScenario.ETag(prepared), await w.SignAsync(DrillWorld.RunId(done)))).Status);
        var running = await w.StartOkAsync(planId);

        page = await w.S.ExpectAsync(HttpMethod.Get, "/api/recall/drill-runs?pageSize=2", w.Runner, null, 200);
        Assert.Equal((3, 2, 1, 2), (page.GetProperty("total").GetInt32(), page.GetProperty("items").GetArrayLength(), page.GetProperty("page").GetInt32(),
            page.GetProperty("pageSize").GetInt32()));
        Assert.Equal(DrillWorld.RunId(running), page.GetProperty("items")[0].GetProperty("runId").GetString()); // newest first
        Assert.Equal(1, (await w.S.ExpectAsync(HttpMethod.Get, "/api/recall/drill-runs?page=2&pageSize=2", w.Runner, null, 200)).GetProperty("items").GetArrayLength());
        foreach (var status in new[] { "Running", "Completed", "Cancelled" })
        {
            var only = await w.S.ExpectAsync(HttpMethod.Get, $"/api/recall/drill-runs?status={status}", w.Runner, null, 200);
            Assert.Equal(status, Assert.Single(only.GetProperty("items").EnumerateArray()).GetProperty("status").GetString());
        }

        Assert.Equal(400, (await w.S.SendAsync(HttpMethod.Get, "/api/recall/drill-runs?status=running", w.Runner)).Status);
        Assert.Equal(400, (await w.S.SendAsync(HttpMethod.Get, "/api/recall/drill-runs?from=14.10.2026", w.Runner)).Status);
        var warning = page.GetProperty("frequencyWarning");
        Assert.Equal((false, 0), (warning.GetProperty("warning").GetBoolean(), warning.GetProperty("daysSinceLastCompleted").GetInt32()));

        _factory.Clock.Advance(TimeSpan.FromDays(181));
        warning = (await w.S.ExpectAsync(HttpMethod.Get, "/api/recall/drill-runs", w.Runner, null, 200)).GetProperty("frequencyWarning");
        Assert.Equal((true, 181), (warning.GetProperty("warning").GetBoolean(), warning.GetProperty("daysSinceLastCompleted").GetInt32()));
        _factory.Clock.Reset();

        // get: 404 unknown, 403 another company, 404 another tenant
        await w.GetRunAsync(Guid.NewGuid().ToString("D"), expected: 404);
        var (sx, cross, _) = await w.S.SendAsync(HttpMethod.Get, $"/api/recall/drill-runs/{DrillWorld.RunId(done)}", w.S.Mint(w.S.Qa2, DrillWorld.RunnerKeys, Guid.NewGuid()));
        Assert.True(sx == 403 && RecallScenario.Code(cross) == "CROSS_LE_FORBIDDEN", cross.ToString());
        await w.GetRunAsync(DrillWorld.RunId(done), w.S.Mint(Guid.NewGuid(), DrillWorld.RunnerKeys, Guid.NewGuid(), Guid.NewGuid()), 404);
        Assert.Equal("Completed", (await w.GetRunAsync(DrillWorld.RunId(done))).GetProperty("status").GetString());
    }

    private static Dictionary<string, object?> Clean(Dictionary<string, object?> body) => body.Where(p => p.Value is not null).ToDictionary(p => p.Key, p => p.Value);
}
