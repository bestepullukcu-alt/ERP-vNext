using Diten.SafetyStockService.Application;
using Diten.SafetyStockService.Domain;
using Xunit;

namespace Diten.SafetyStockService.Read.Tests;

public sealed class PolicyReadQueryHandlerTests
{
    private readonly Guid _tenant = Guid.NewGuid();
    private readonly Guid _company = Guid.NewGuid();
    private readonly Guid _actor = Guid.NewGuid();
    private readonly Guid _policy = Guid.NewGuid();
    private readonly SeriesKey _series = new(Guid.NewGuid(), Guid.NewGuid());

    [Fact]
    public async Task MdmUnavailable_StopsBeforeScopeAndStore()
    {
        var scope = new ScopeReader(new ActorSeriesScope(new HashSet<SeriesKey> { _series }));
        var store = new Store();
        var result = await Handler(ReferenceCheck.Unavailable, scope, store).Handle(Query(PolicyReadKind.List), default);
        Assert.Equal(ReadStatus.Unavailable, result.Status);
        Assert.Equal(0, scope.Calls);
        Assert.Equal(0, store.Calls);
    }

    [Fact]
    public async Task InvalidCompany_ReturnsNoRecords()
    {
        var store = new Store();
        var result = await Handler(ReferenceCheck.NotFound, new ScopeReader(null), store).Handle(Query(PolicyReadKind.List), default);
        Assert.Equal(ReadStatus.NotFound, result.Status);
        Assert.Equal(0, store.Calls);
    }

    [Fact]
    public async Task MissingAuthoritativeScope_DoesNotQueryStore()
    {
        var store = new Store();
        var result = await Handler(ReferenceCheck.Valid, new ScopeReader(null), store).Handle(Query(PolicyReadKind.List), default);
        Assert.Equal(ReadStatus.Unavailable, result.Status);
        Assert.Equal(0, store.Calls);
    }

    [Fact]
    public async Task EmptyScope_ReturnsEmptyListButNoDetail()
    {
        var store = new Store();
        var handler = Handler(ReferenceCheck.Valid, new ScopeReader(new ActorSeriesScope(new HashSet<SeriesKey>())), store);
        Assert.Empty(Assert.IsType<PolicyReadModel[]>((await handler.Handle(Query(PolicyReadKind.List), default)).Data));
        Assert.Equal(ReadStatus.NotFound, (await handler.Handle(Query(PolicyReadKind.Policy, _policy), default)).Status);
        Assert.Equal(0, store.Calls);
    }

    [Fact]
    public async Task DetailOutsideAuthorizedSeries_IsNotFound()
    {
        var store = new Store { Policy = new(_policy, _tenant, _company, new SeriesKey(Guid.NewGuid(), _series.WarehouseId), "Draft", 1) };
        var result = await Handler(ReferenceCheck.Valid, Scope(), store).Handle(Query(PolicyReadKind.Policy, _policy), default);
        Assert.Equal(ReadStatus.NotFound, result.Status);
    }

    [Fact]
    public async Task DetailFromAnotherTenant_IsNotFound()
    {
        var store = new Store { Policy = new(_policy, Guid.NewGuid(), _company, _series, "Draft", 1) };
        var result = await Handler(ReferenceCheck.Valid, Scope(), store).Handle(Query(PolicyReadKind.Policy, _policy), default);
        Assert.Equal(ReadStatus.NotFound, result.Status);
    }

    [Fact]
    public async Task DetailFromAnotherLegalEntity_IsNotFoundEvenInTemporaryMdmOnlyMode()
    {
        var record = new PolicyReadModel(_policy, _tenant, Guid.NewGuid(), _series, "Draft", 1);
        var store = new Store { Policy = record };
        var companyWide = new ScopeReader(new ActorSeriesScope(new HashSet<SeriesKey>(), AllSeriesInLegalEntity: true));
        var result = await Handler(ReferenceCheck.Valid, companyWide, store).Handle(Query(PolicyReadKind.Policy, _policy), default);
        Assert.Equal(ReadStatus.NotFound, result.Status);
    }

    [Fact]
    public async Task ListWithOutOfScopeRecord_FailsClosedRatherThanPartialPage()
    {
        var store = new Store { Policies = [new(_policy, _tenant, Guid.NewGuid(), _series, "Draft", 1)] };
        var result = await Handler(ReferenceCheck.Valid, Scope(), store).Handle(Query(PolicyReadKind.List), default);
        Assert.Equal(ReadStatus.Unavailable, result.Status);
    }

    [Fact]
    public async Task AuthorizedEvaluation_ReturnsRecord()
    {
        var evaluationId = Guid.NewGuid();
        var record = new PolicyEvaluationReadModel(evaluationId, _policy, _tenant, _company, _series, Guid.NewGuid(), "MethodA", 5, "v1");
        var store = new Store { Evaluation = record };
        var result = await Handler(ReferenceCheck.Valid, Scope(), store).Handle(Query(PolicyReadKind.Evaluation, _policy, evaluationId), default);
        Assert.Equal(ReadStatus.Found, result.Status);
        Assert.Same(record, result.Data);
    }

    [Fact]
    public async Task MdmOnlyTemporaryScope_AdmitsOtherSeriesWithinSelectedCompany()
    {
        var otherSeries = new SeriesKey(Guid.NewGuid(), Guid.NewGuid());
        var record = new PolicyReadModel(_policy, _tenant, _company, otherSeries, "Draft", 1);
        var store = new Store { Policy = record };
        var companyWide = new ScopeReader(new ActorSeriesScope(new HashSet<SeriesKey>(), AllSeriesInLegalEntity: true));
        var result = await Handler(ReferenceCheck.Valid, companyWide, store).Handle(Query(PolicyReadKind.Policy, _policy), default);
        Assert.Equal(ReadStatus.Found, result.Status);
        Assert.Same(record, result.Data);
    }

    [Fact]
    public async Task StoreFailure_IsUnavailable()
    {
        var store = new Store { Fail = true };
        var result = await Handler(ReferenceCheck.Valid, Scope(), store).Handle(Query(PolicyReadKind.List), default);
        Assert.Equal(ReadStatus.Unavailable, result.Status);
    }

    private PolicyReadQuery Query(PolicyReadKind kind, Guid? policyId = null, Guid? childId = null)
        => new(kind, _tenant, _actor, _company, policyId, childId);

    private ScopeReader Scope() => new(new ActorSeriesScope(new HashSet<SeriesKey> { _series }));

    private static PolicyReadQueryHandler Handler(ReferenceCheck result, ScopeReader scope, Store store)
        => new(new ReferenceValidator(result), scope, store);

    private sealed class ReferenceValidator(ReferenceCheck result) : ILegalEntityReferenceValidator
    {
        public Task<ReferenceCheck> ValidateAsync(Guid legalEntityId, CancellationToken cancellationToken) => Task.FromResult(result);
    }

    private sealed class ScopeReader(ActorSeriesScope? scope) : IActorSeriesScopeReader
    {
        public int Calls { get; private set; }
        public Task<ActorSeriesScope?> ResolveAsync(Guid tenantId, Guid actorId, Guid legalEntityId, CancellationToken cancellationToken)
        {
            Calls++;
            return Task.FromResult(scope);
        }
    }

    private sealed class Store : IPolicyReadStore
    {
        public int Calls { get; private set; }
        public bool Fail { get; init; }
        public IReadOnlyList<PolicyReadModel> Policies { get; init; } = [];
        public PolicyReadModel? Policy { get; init; }
        public PolicyEvaluationReadModel? Evaluation { get; init; }

        public Task<IReadOnlyList<PolicyReadModel>> ListAsync(Guid tenantId, Guid legalEntityId, ActorSeriesScope scope, CancellationToken cancellationToken)
        {
            Calls++;
            if (Fail) throw new InvalidOperationException("Unavailable");
            return Task.FromResult(Policies);
        }

        public Task<PolicyReadModel?> GetPolicyAsync(Guid tenantId, Guid legalEntityId, Guid policyId, ActorSeriesScope scope, CancellationToken cancellationToken)
        {
            Calls++;
            return Task.FromResult(Policy);
        }

        public Task<PolicyRevisionReadModel?> GetRevisionAsync(Guid tenantId, Guid legalEntityId, Guid policyId, Guid revisionId, ActorSeriesScope scope, CancellationToken cancellationToken)
        {
            Calls++;
            return Task.FromResult<PolicyRevisionReadModel?>(null);
        }

        public Task<PolicyEvaluationReadModel?> GetEvaluationAsync(Guid tenantId, Guid legalEntityId, Guid policyId, Guid evaluationId, ActorSeriesScope scope, CancellationToken cancellationToken)
        {
            Calls++;
            return Task.FromResult(Evaluation);
        }
    }
}
