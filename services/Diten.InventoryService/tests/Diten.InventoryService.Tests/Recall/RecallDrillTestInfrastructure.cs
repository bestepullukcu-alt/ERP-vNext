using System.Text.Json;
using Diten.InventoryService.Application.Common;
using Diten.InventoryService.Application.Features.Recall.Drills;
using Diten.InventoryService.Application.Features.Recall.Ports;
using Diten.InventoryService.Application.Features.Recall.Services;
using Diten.InventoryService.Domain.Recall.Entities;
using Diten.InventoryService.Persistence.Recall;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.TestHost;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;
using MongoDB.Bson;
using MongoDB.Driver;
using Xunit;

namespace Diten.InventoryService.Tests.Recall;

/// <summary>
/// WP-0177-R5 — the SERVER clock of the recall handlers (<see cref="RecallSupport.Now"/>): real time + an offset, or frozen at a whole second so a
/// test can assert the exact <c>StartedAtUtc</c> / <c>EndedAtUtc</c>. Only <see cref="RecallSupport"/> gets it — the ledger, the JWT check and
/// MOD-0022 keep the real clock.
/// </summary>
public sealed class DrillClock : TimeProvider
{
    private readonly object _gate = new();
    private DateTimeOffset? _frozen;
    private TimeSpan _offset;

    public override DateTimeOffset GetUtcNow()
    {
        lock (_gate) return _frozen ?? DateTimeOffset.UtcNow + _offset;
    }

    /// <summary>Freezes at the current (offset) time, truncated to the second (the wire precision); returns it.</summary>
    public DateTimeOffset Freeze()
    {
        lock (_gate)
        {
            var now = DateTimeOffset.UtcNow + _offset;
            _frozen = new DateTimeOffset(now.UtcTicks - now.UtcTicks % TimeSpan.TicksPerSecond, TimeSpan.Zero);
            return _frozen.Value;
        }
    }

    public void Advance(TimeSpan by)
    {
        lock (_gate)
        {
            if (_frozen is { } frozen) _frozen = frozen + by;
            else _offset += by;
        }
    }

    public void Reset()
    {
        lock (_gate)
        {
            _frozen = null;
            _offset = TimeSpan.Zero;
        }
    }
}

/// <summary>The Auto pick's randomness with a settable seed (the production one is <see cref="SharedRecallDrillRandom"/>).</summary>
public sealed class ResettableDrillRandom : IRecallDrillRandom
{
    private readonly object _gate = new();
    private Random _random = new(1);

    public void Seed(int seed)
    {
        lock (_gate) _random = new Random(seed);
    }

    public int Next(int count)
    {
        lock (_gate) return _random.Next(count);
    }
}

/// <summary>
/// The REAL host with the PRODUCTION MOD-0022 signature gate (<see cref="ESignRecallSignature"/> over the suite's in-memory
/// <see cref="FakeESignGateway"/>), the server clock <see cref="Clock"/> under <see cref="RecallSupport"/> and the seedable Auto randomness.
/// </summary>
public sealed class RecallDrillApiFactory : RecallApiFactory
{
    protected override bool UseTestSignature => false;

    public DrillClock Clock { get; } = new();

    public ResettableDrillRandom Random { get; } = new();

    protected override void ConfigureWebHost(IWebHostBuilder builder)
    {
        base.ConfigureWebHost(builder);
        builder.ConfigureTestServices(services =>
        {
            services.RemoveAll<RecallSupport>();
            services.AddScoped(sp => new RecallSupport(sp.GetRequiredService<ITenantContext>(), sp.GetRequiredService<ICorrelationContext>(), Clock));
            services.RemoveAll<IRecallDrillRandom>();
            services.AddSingleton<IRecallDrillRandom>(Random);
        });
    }
}

/// <summary>
/// A drill world: the R2 trace world (raw material <c>Rm</c> → finished good <c>Fg</c>, received, produced, sold) of a fresh tenant + company,
/// the MOD-0022 policies of the five recall actions, and the drill roles. Every call goes through the real API and every drill answer is
/// checked against RECALL 1.4.0.
/// </summary>
public sealed class DrillWorld
{
    public const string Note = "Tatbikat hedef surede tamamlandi, bosluk yok";
    public const string CancelReason = "Tatbikat ekip toplantisi nedeniyle durduruldu";

    public static readonly string[] RunnerKeys =
        ["recall.drills.read", "recall.drills.manage", "recall.drills.run", "recall.cases.read", "esign.signatures.sign", "esign.signatures.read"];

    private DrillWorld(RecallDrillApiFactory factory, RecallTraceWorld trace)
    {
        Factory = factory;
        Trace = trace;
        Runner = S.Mint(S.Qa1, RunnerKeys);
        Runner2 = S.Mint(S.Qa2, RunnerKeys);
        Admin = S.Mint(S.Admin, [.. RecallScenario.AdminKeys, "esign.signatures.sign", "esign.signatures.read"]);
    }

    public RecallDrillApiFactory Factory { get; }
    public RecallTraceWorld Trace { get; }
    public RecallScenario S => Trace.S;
    public Guid Tenant => S.Tenant;
    public string Runner { get; }
    public string Runner2 { get; }
    public string Admin { get; }

    public static async Task<DrillWorld> CreateAsync(RecallDrillApiFactory factory, bool resultRequired = true)
    {
        factory.Clock.Reset();
        var w = new DrillWorld(factory, await RecallTraceWorld.CreateAsync(factory));
        foreach (var action in RecallSignableActions.All)
        {
            var required = action.Code == RecallSignableActions.DrillResult ? resultRequired : action.Code != RecallSignableActions.SettingsUpdate;
            factory.ESign.Policy(w.Tenant, action.Code, required, "APPROVE");
        }

        return w;
    }

    /// <summary>One more finished good of the company: produced (GOODS_RECEIPT_PROD) and sold (GOODS_ISSUE_SALES) now.</summary>
    public async Task<string> FinishedGoodAsync()
    {
        var lot = await S.LotAsync();
        await Trace.PostAsync(Trace.Lots.Receipt("6", lot, movementType: "GOODS_RECEIPT_PROD"), "MOD-0194", "MO-" + Guid.NewGuid().ToString("N")[..8]);
        await Trace.SellAsync(lot, "2");
        return lot;
    }

    // ── plans ────────────────────────────────────────────────────────────────────────────────────────────

    public Dictionary<string, object?> PlanBody(string scopeKind = "RandomFinishedGoodLot", string frequency = "Quarterly", int? customDays = null,
        string name = "Ceyrek tatbikat", string? key = null, bool? active = null) => new()
    {
        ["idempotencyKey"] = key ?? RecallScenario.Key("dp"),
        ["name"] = name,
        ["frequency"] = frequency,
        ["customDays"] = customDays,
        ["scopeKind"] = scopeKind,
        ["nextDueOn"] = "2026-12-31",
        ["ownerUserId"] = S.Qa1.ToString("D"),
        ["active"] = active
    };

    public Task<(int Status, JsonElement Body, HttpResponseMessage Raw)> CreatePlanAsync(Dictionary<string, object?> body, string? token = null)
        => S.SendAsync(HttpMethod.Post, "/api/recall/drill-plans", token ?? Runner, Clean(body), (string?)body["idempotencyKey"]);

    public async Task<JsonElement> PlanAsync(string scopeKind = "RandomFinishedGoodLot")
    {
        var (status, plan, _) = await CreatePlanAsync(PlanBody(scopeKind));
        Assert.True(status == 201, plan.ToString());
        return plan;
    }

    public static string PlanId(JsonElement plan) => plan.GetProperty("planId").GetString()!;

    // ── runs ─────────────────────────────────────────────────────────────────────────────────────────────

    public Task<(int Status, JsonElement Body, HttpResponseMessage Raw)> StartAsync(string planId, string mode = "Auto", string? lotId = null, string? token = null,
        string? key = null, Dictionary<string, object?>? extra = null)
    {
        var body = new Dictionary<string, object?> { ["idempotencyKey"] = key ?? RecallScenario.Key("dr"), ["planId"] = planId, ["selectionMode"] = mode };
        if (lotId is not null) body["lotId"] = lotId;
        foreach (var (k, v) in extra ?? []) body[k] = v;
        return S.SendAsync(HttpMethod.Post, "/api/recall/drill-runs", token ?? Runner, body, (string)body["idempotencyKey"]!);
    }

    public async Task<JsonElement> StartOkAsync(string planId, string mode = "Auto", string? lotId = null)
    {
        var (status, run, _) = await StartAsync(planId, mode, lotId);
        Assert.True(status == 201, run.ToString());
        return run;
    }

    public static string RunId(JsonElement run) => run.GetProperty("runId").GetString()!;

    public Task<(int Status, JsonElement Body, HttpResponseMessage Raw)> StepAsync(JsonElement run, string step, string? token = null, string? ifMatch = null,
        Dictionary<string, object?>? extra = null)
    {
        var body = new Dictionary<string, object?> { ["idempotencyKey"] = RecallScenario.Key("ds") };
        foreach (var (k, v) in extra ?? []) body[k] = v;
        return S.SendAsync(HttpMethod.Post, $"/api/recall/drill-runs/{RunId(run)}/steps/{step}", token ?? Runner, body, (string)body["idempotencyKey"]!,
            ifMatch ?? RecallScenario.ETag(run));
    }

    public async Task<JsonElement> StepOkAsync(JsonElement run, string step)
    {
        var (status, after, _) = await StepAsync(run, step);
        Assert.True(status == 200, $"{step}: {after}");
        return after;
    }

    /// <summary>Trace → Scope → CustomerList; <paramref name="before"/> runs before the last step (the clock moves there).</summary>
    public async Task<JsonElement> StepsAsync(JsonElement run, Action? before = null)
    {
        run = await StepOkAsync(run, "Trace");
        run = await StepOkAsync(run, "Scope");
        before?.Invoke();
        return await StepOkAsync(run, "CustomerList");
    }

    public Task<(int Status, JsonElement Body, HttpResponseMessage Raw)> PrepareAsync(JsonElement run, string note = Note, string? token = null, string? ifMatch = null)
    {
        var key = RecallScenario.Key("dp");
        return S.SendAsync(HttpMethod.Post, $"/api/recall/drill-runs/{RunId(run)}/pending-result", token ?? Runner,
            new Dictionary<string, object?> { ["idempotencyKey"] = key, ["resultNote"] = note }, key, ifMatch ?? RecallScenario.ETag(run));
    }

    public async Task<JsonElement> PrepareOkAsync(JsonElement run, string note = Note, string? token = null)
    {
        var (status, prepared, _) = await PrepareAsync(run, note, token);
        Assert.True(status == 200, prepared.ToString());
        return prepared;
    }

    public Task<(int Status, JsonElement Body, HttpResponseMessage Raw)> ResultAsync(string runId, string ifMatch, string? signatureId, string note = Note,
        string? token = null)
    {
        var key = RecallScenario.Key("dz");
        var body = new Dictionary<string, object?> { ["idempotencyKey"] = key, ["resultNote"] = note };
        if (signatureId is not null) body["signatureId"] = signatureId;
        return S.SendAsync(HttpMethod.Post, $"/api/recall/drill-runs/{runId}/result", token ?? Runner, body, key, ifMatch);
    }

    public Task<(int Status, JsonElement Body, HttpResponseMessage Raw)> CancelAsync(JsonElement run, string? reason = CancelReason, string? token = null)
    {
        var key = RecallScenario.Key("dc");
        var body = new Dictionary<string, object?> { ["idempotencyKey"] = key };
        if (reason is not null) body["reason"] = reason;
        return S.SendAsync(HttpMethod.Post, $"/api/recall/drill-runs/{RunId(run)}/cancel", token ?? Runner, body, key, RecallScenario.ETag(run));
    }

    public Task<JsonElement> GetRunAsync(string runId, string? token = null, int expected = 200)
        => S.ExpectAsync(HttpMethod.Get, $"/api/recall/drill-runs/{runId}", token ?? Runner, null, expected);

    // ── MOD-0022 ─────────────────────────────────────────────────────────────────────────────────────────

    public Task<JsonElement> SubjectAsync(string runId, string? token = null, int expected = 200)
        => S.ExpectAsync(HttpMethod.Get, $"/api/recall/esign-subjects/recall.drill-run/{runId}", token ?? Runner, null, expected);

    /// <summary>
    /// What MOD-0022 does at signing: read the provider with the signer's token, hash the fields (Platform's own canonical code), record the
    /// signature — signed one second after the server clock unless <paramref name="signedAt"/> says otherwise.
    /// </summary>
    public async Task<string> SignAsync(string runId, Guid? signer = null, string? token = null, DateTimeOffset? signedAt = null, string? hash = null,
        string meaning = "APPROVE", string action = RecallSignableActions.DrillResult)
    {
        var subject = await SubjectAsync(runId, token ?? Runner);
        var computed = RecallESignTests.PlatformHash(RecallSignableActions.DrillRunSubjectType, runId, subject.GetProperty("fields"));
        return Factory.ESign.Sign(Tenant, signer ?? S.Qa1, runId, meaning, hash ?? computed, "recall", RecallSignableActions.DrillRunSubjectType, action,
            signedAt ?? Factory.Clock.GetUtcNow().AddSeconds(1), S.Ledger.LegalEntity);
    }

    /// <summary>A Running run with all three steps done (the timer stopped).</summary>
    public async Task<JsonElement> FinishedRunAsync(string? planId = null)
        => await StepsAsync(await StartOkAsync(planId ?? PlanId(await PlanAsync())));

    // ── stored rows ──────────────────────────────────────────────────────────────────────────────────────

    public List<BsonDocument> Rows(string collection, FilterDefinition<BsonDocument>? filter = null)
        => S.Factory.Collection(collection).Find(Builders<BsonDocument>.Filter.Eq("TenantId", Tenant.ToString()) & (filter ?? FilterDefinition<BsonDocument>.Empty)).ToList();

    public List<BsonDocument> Audit(string action) => Rows(Diten.InventoryService.Persistence.InventoryCollections.Audit, Builders<BsonDocument>.Filter.Eq("Action", action));

    private static Dictionary<string, object?> Clean(Dictionary<string, object?> body) => body.Where(p => p.Value is not null).ToDictionary(p => p.Key, p => p.Value);
}
