using Diten.CrmService.Application.Common;
using Diten.CrmService.Application.Common.Models;
using Diten.CrmService.Application.Features.ContentComposition.Eligibility;
using Diten.CrmService.Domain.Entities;
using Diten.CrmService.Domain.Repositories;
using MediatR;
using Xunit;

namespace Diten.CrmService.Application.Tests;

/// <summary>
/// SCMM-11 (CAND-CAP-0011) — eligibility policy + deterministic evaluator tests. In-memory fakes; the resolver reads the
/// policy and maps a context to a disjoint state, fail-closed. Covers policy versioned CRUD + publish-freeze, the three
/// disjoint outcomes, missing-required-context ⇒ Unresolved, repo-throws ⇒ propagate, the RM4 eval-log, determinism and
/// the port == query contract.
/// </summary>
public sealed class EligibilityEvaluationTests
{
    private static readonly Guid TenantA = Guid.Parse("cccccccc-cccc-cccc-cccc-cccccccccccc");
    private static readonly DateTimeOffset Jan1 = new(2026, 1, 1, 0, 0, 0, TimeSpan.Zero);
    private static readonly DateTimeOffset Jun1 = new(2026, 6, 1, 0, 0, 0, TimeSpan.Zero);

    private static TenantContext Tenant(Guid id)
    {
        var ctx = new TenantContext();
        ctx.SetTenant(id);
        return ctx;
    }

    private sealed class Fixture
    {
        public FakeEligibilityRepo Policies { get; } = new();
        public CapturingLogWriter Log { get; } = new();
        public Guid TenantId { get; }

        public Fixture(Guid tenant) => TenantId = tenant;

        public CreateEligibilityPolicyHandler Create() => new(Tenant(TenantId), new NullActorContext(), Policies);
        public UpdateEligibilityPolicyHandler Update() => new(Tenant(TenantId), new NullActorContext(), Policies);
        public ArchiveEligibilityPolicyHandler Archive() => new(Tenant(TenantId), new NullActorContext(), Policies);
        public GetEligibilityPolicyHandler Get() => new(Tenant(TenantId), Policies);
        public ResolveEligibilityHandler Resolve() => new(Tenant(TenantId), new NullActorContext(), Policies, Log);

        public async Task<Guid> SeedPublished(params EligibilityConditionInput[] conditions)
        {
            var r = await Create().Handle(new CreateEligibilityPolicyCommand(
                "EP-" + Guid.NewGuid().ToString("N")[..6], "Policy", Jan1, conditions,
                Status: EligibilityPolicyStatuses.Published), default);
            Assert.Equal(201, r.StatusCode);
            return r.Data;
        }
    }

    private static EligibilityContext Ctx(params (string Dim, string[] Vals)[] dims)
        => new(dims.Select(d => new EligibilityContextDimension(d.Dim, d.Vals)).ToList());

    // ---------------- policy CRUD + publish-freeze ----------------

    [Fact]
    public async Task Policy_create_and_get_round_trip()
    {
        var fx = new Fixture(TenantA);
        var created = await fx.Create().Handle(new CreateEligibilityPolicyCommand(
            "EP-1", "Cardiology gate", Jan1,
            new[] { new EligibilityConditionInput("specialty", new[] { "cardiology" }, EligibilityMatchKinds.Includes, true) },
            PolicyVersion: "1.0"), default);
        Assert.Equal(201, created.StatusCode);

        var dto = (await fx.Get().Handle(new GetEligibilityPolicyQuery(created.Data), default)).Data!;
        Assert.Equal("EP-1", dto.PolicyCode);
        Assert.Equal("1.0", dto.PolicyVersion);
        var cond = Assert.Single(dto.Conditions);
        Assert.Equal("specialty", cond.Dimension);
        Assert.True(cond.Required);
    }

    [Fact]
    public async Task Published_policy_condition_change_returns_409()
    {
        var fx = new Fixture(TenantA);
        var id = await fx.SeedPublished(new EligibilityConditionInput("specialty", new[] { "cardiology" }));
        var upd = await fx.Update().Handle(new UpdateEligibilityPolicyCommand(
            id, "Policy", Jan1,
            new[] { new EligibilityConditionInput("specialty", new[] { "oncology" }) },
            Status: EligibilityPolicyStatuses.Published), default);
        Assert.Equal(409, upd.StatusCode);
    }

    [Fact]
    public async Task Duplicate_active_code_returns_409()
    {
        var fx = new Fixture(TenantA);
        var first = await fx.Create().Handle(new CreateEligibilityPolicyCommand(
            "EP-DUP", "A", Jan1, Array.Empty<EligibilityConditionInput>()), default);
        Assert.Equal(201, first.StatusCode);
        var second = await fx.Create().Handle(new CreateEligibilityPolicyCommand(
            "EP-DUP", "B", Jan1, Array.Empty<EligibilityConditionInput>()), default);
        Assert.Equal(409, second.StatusCode);
    }

    [Fact]
    public async Task Condition_shape_is_validated()
    {
        var fx = new Fixture(TenantA);
        var emptyValues = await fx.Create().Handle(new CreateEligibilityPolicyCommand(
            "EP-EV", "EV", Jan1, new[] { new EligibilityConditionInput("specialty", Array.Empty<string>()) }), default);
        Assert.Equal(400, emptyValues.StatusCode);
    }

    // ---------------- evaluator: disjoint outcomes ----------------

    [Fact]
    public async Task Eligible_when_includes_condition_matches()
    {
        var fx = new Fixture(TenantA);
        var id = await fx.SeedPublished(new EligibilityConditionInput("specialty", new[] { "cardiology" }, EligibilityMatchKinds.Includes, true));
        var r = await fx.Resolve().Handle(new ResolveEligibilityQuery(
            id, Ctx(("specialty", new[] { "cardiology" })), At: Jun1), default);
        Assert.True(r.IsSuccessful);
        Assert.Equal(EligibilityState.Eligible, r.Data!.State);
        Assert.Null(r.Data.BlockingLevel);
    }

    [Fact]
    public async Task Blocked_when_includes_condition_not_met()
    {
        var fx = new Fixture(TenantA);
        var id = await fx.SeedPublished(new EligibilityConditionInput("specialty", new[] { "cardiology" }, EligibilityMatchKinds.Includes, true));
        var r = await fx.Resolve().Handle(new ResolveEligibilityQuery(
            id, Ctx(("specialty", new[] { "oncology" })), At: Jun1), default);
        Assert.Equal(EligibilityState.Blocked, r.Data!.State);
        Assert.Equal("policy", r.Data.BlockingLevel);
        Assert.StartsWith(EligibilityReasonCodes.NotIncluded, r.Data.Reason);
    }

    [Fact]
    public async Task Blocked_when_excludes_condition_hit()
    {
        var fx = new Fixture(TenantA);
        var id = await fx.SeedPublished(new EligibilityConditionInput("market", new[] { "us" }, EligibilityMatchKinds.Excludes, true));
        var r = await fx.Resolve().Handle(new ResolveEligibilityQuery(
            id, Ctx(("market", new[] { "us" })), At: Jun1), default);
        Assert.Equal(EligibilityState.Blocked, r.Data!.State);
        Assert.StartsWith(EligibilityReasonCodes.Excluded, r.Data.Reason);
    }

    [Fact]
    public async Task Unresolved_when_required_context_missing()
    {
        var fx = new Fixture(TenantA);
        var id = await fx.SeedPublished(new EligibilityConditionInput("specialty", new[] { "cardiology" }, EligibilityMatchKinds.Includes, true));
        var r = await fx.Resolve().Handle(new ResolveEligibilityQuery(
            id, Ctx(("market", new[] { "de" })), At: Jun1), default); // no specialty in context
        Assert.Equal(EligibilityState.Unresolved, r.Data!.State);
        Assert.Equal("context", r.Data.BlockingLevel);
        Assert.StartsWith(EligibilityReasonCodes.MissingRequiredContext, r.Data.Reason);
    }

    [Fact]
    public async Task Unresolved_when_policy_not_published()
    {
        var fx = new Fixture(TenantA);
        var created = await fx.Create().Handle(new CreateEligibilityPolicyCommand(
            "EP-DRAFT", "Draft", Jan1,
            new[] { new EligibilityConditionInput("specialty", new[] { "cardiology" }) },
            Status: EligibilityPolicyStatuses.Draft), default);
        var r = await fx.Resolve().Handle(new ResolveEligibilityQuery(
            created.Data, Ctx(("specialty", new[] { "cardiology" })), At: Jun1), default);
        Assert.Equal(EligibilityState.Unresolved, r.Data!.State);
        Assert.Equal(EligibilityReasonCodes.PolicyNotEffective, r.Data.Reason);
    }

    [Fact]
    public async Task Policy_not_found_returns_404()
    {
        var fx = new Fixture(TenantA);
        var r = await fx.Resolve().Handle(new ResolveEligibilityQuery(Guid.NewGuid(), Ctx(), At: Jun1), default);
        Assert.Equal(404, r.StatusCode);
    }

    // ---------------- fail-closed: infra error propagates (never Unresolved) ----------------

    [Fact]
    public async Task Repository_failure_propagates_and_is_not_folded_into_unresolved()
    {
        var throwingRepo = new ThrowingEligibilityRepo();
        var handler = new ResolveEligibilityHandler(
            Tenant(TenantA), new NullActorContext(), throwingRepo, new CapturingLogWriter());
        await Assert.ThrowsAsync<InvalidOperationException>(() =>
            handler.Handle(new ResolveEligibilityQuery(Guid.NewGuid(), Ctx(), At: Jun1), default));
    }

    // ---------------- RM4 eval-log + determinism + port==query ----------------

    [Fact]
    public async Task Evaluation_writes_rm4_log_entry()
    {
        var fx = new Fixture(TenantA);
        var id = await fx.SeedPublished(new EligibilityConditionInput("specialty", new[] { "cardiology" }, EligibilityMatchKinds.Includes, true));
        await fx.Resolve().Handle(new ResolveEligibilityQuery(
            id, Ctx(("specialty", new[] { "oncology" })), PinnedSelections: new[] { "content-1" }, At: Jun1), default);

        var entry = Assert.Single(fx.Log.Entries);
        Assert.Equal(id, entry.PolicyId);
        Assert.Equal("1.0", entry.PolicyVersion);
        Assert.Equal(EligibilityState.Blocked, entry.State);
        Assert.NotEmpty(entry.Reasons);
        Assert.Contains("content-1", entry.PinnedSelections);
        Assert.Equal(Jun1, entry.EvaluatedAtUtc);
    }

    [Fact]
    public async Task Evaluation_is_deterministic()
    {
        var fx = new Fixture(TenantA);
        var id = await fx.SeedPublished(new EligibilityConditionInput("specialty", new[] { "cardiology" }, EligibilityMatchKinds.Includes, true));
        var q = new ResolveEligibilityQuery(id, Ctx(("specialty", new[] { "cardiology" })), At: Jun1);
        var a = (await fx.Resolve().Handle(q, default)).Data!;
        var b = (await fx.Resolve().Handle(q, default)).Data!;
        Assert.Equal(a.State, b.State);
        Assert.Equal(a.PolicyVersion, b.PolicyVersion);
        Assert.Equal(a.EvaluatedAtUtc, b.EvaluatedAtUtc);
    }

    [Fact]
    public async Task Port_forwards_to_the_resolver_query()
    {
        var canned = Response<EligibilityResult>.Success(
            new EligibilityResult(EligibilityState.Eligible, null, null, Guid.NewGuid(), "1.0",
                Array.Empty<EligibilityConditionOutcome>(), Jun1));
        var mediator = new StubMediator(canned);
        var port = new EligibilityEvaluationPort(mediator);
        var query = new ResolveEligibilityQuery(Guid.NewGuid(), Ctx(), At: Jun1);

        var result = await port.EvaluateAsync(query, default);

        Assert.Same(canned, result);              // port result IS the query result (pass-through)
        Assert.Same(query, mediator.LastRequest);  // the same query object is routed to the resolver
    }

    // ---------------- fakes ----------------

    private sealed class FakeEligibilityRepo : IEligibilityPolicyRepository
    {
        public List<EligibilityPolicy> Items { get; } = new();

        public Task<EligibilityPolicy?> GetByIdAsync(Guid t, Guid id, CancellationToken ct)
            => Task.FromResult(Items.FirstOrDefault(x => x.TenantId == t && x.Id == id && !x.IsDeleted));

        public Task<IReadOnlyList<EligibilityPolicy>> ListAsync(Guid t, CancellationToken ct)
            => Task.FromResult((IReadOnlyList<EligibilityPolicy>)Items.Where(x => x.TenantId == t && !x.IsDeleted).ToList());

        public Task<IReadOnlyList<EligibilityPolicy>> ListByCodeAsync(Guid t, string code, CancellationToken ct)
            => Task.FromResult((IReadOnlyList<EligibilityPolicy>)Items
                .Where(x => x.TenantId == t && !x.IsDeleted && x.PolicyCode == code).ToList());

        public Task<EligibilityPolicy?> GetActiveByCodeAsync(Guid t, string code, CancellationToken ct)
            => Task.FromResult(Items.FirstOrDefault(x =>
                x.TenantId == t && !x.IsDeleted && x.PolicyCode == code && !x.IsArchived()));

        public Task InsertAsync(EligibilityPolicy e, CancellationToken ct) { Items.Add(e); return Task.CompletedTask; }
        public Task UpdateAsync(EligibilityPolicy e, CancellationToken ct) => Task.CompletedTask;
    }

    private sealed class ThrowingEligibilityRepo : IEligibilityPolicyRepository
    {
        public Task<EligibilityPolicy?> GetByIdAsync(Guid t, Guid id, CancellationToken ct)
            => throw new InvalidOperationException("register unavailable");
        public Task<IReadOnlyList<EligibilityPolicy>> ListAsync(Guid t, CancellationToken ct) => throw new InvalidOperationException();
        public Task<IReadOnlyList<EligibilityPolicy>> ListByCodeAsync(Guid t, string code, CancellationToken ct) => throw new InvalidOperationException();
        public Task<EligibilityPolicy?> GetActiveByCodeAsync(Guid t, string code, CancellationToken ct) => throw new InvalidOperationException();
        public Task InsertAsync(EligibilityPolicy e, CancellationToken ct) => throw new InvalidOperationException();
        public Task UpdateAsync(EligibilityPolicy e, CancellationToken ct) => throw new InvalidOperationException();
    }

    private sealed class CapturingLogWriter : IEligibilityEvaluationLogWriter
    {
        public List<EligibilityEvaluationLogEntry> Entries { get; } = new();
        public Task WriteAsync(EligibilityEvaluationLogEntry entry, CancellationToken ct)
        {
            Entries.Add(entry);
            return Task.CompletedTask;
        }
    }

    private sealed class StubMediator : IMediator
    {
        private readonly object _canned;
        public object? LastRequest { get; private set; }
        public StubMediator(object canned) => _canned = canned;

        public Task<TResponse> Send<TResponse>(IRequest<TResponse> request, CancellationToken ct = default)
        {
            LastRequest = request;
            return Task.FromResult((TResponse)_canned);
        }

        public Task<object?> Send(object request, CancellationToken ct = default) => throw new NotSupportedException();
        public Task Send<TRequest>(TRequest request, CancellationToken ct = default) where TRequest : IRequest => throw new NotSupportedException();
        public IAsyncEnumerable<TResponse> CreateStream<TResponse>(IStreamRequest<TResponse> request, CancellationToken ct = default) => throw new NotSupportedException();
        public IAsyncEnumerable<object?> CreateStream(object request, CancellationToken ct = default) => throw new NotSupportedException();
        public Task Publish(object notification, CancellationToken ct = default) => throw new NotSupportedException();
        public Task Publish<TNotification>(TNotification notification, CancellationToken ct = default) where TNotification : INotification => throw new NotSupportedException();
    }
}
