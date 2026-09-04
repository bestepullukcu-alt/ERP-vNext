using Diten.PpmService.Application.Common;
using Diten.PpmService.Application.Features.Initiatives;
using Diten.PpmService.Domain.Entities;
using Diten.PpmService.Domain.Repositories;
using Xunit;

namespace Diten.PpmService.Tests.Initiatives;

public sealed class InitiativeCoreV2ServiceTests
{
    [Fact]
    public async Task Proposed_to_Active_is_503_and_has_zero_mutation()
    {
        var fixture = Fixture.With(InitiativeLifecycleState.Proposed);

        var response = await fixture.Service.Transition(
            new(fixture.Entity.Id, InitiativeLifecycleState.Active, fixture.Entity.Version), default);

        AssertZeroMutation503(fixture, response.StatusCode, InitiativeLifecycleState.Proposed);
    }

    [Fact]
    public async Task OnHold_to_Active_is_503_and_has_zero_mutation()
    {
        var fixture = Fixture.With(InitiativeLifecycleState.OnHold);

        var response = await fixture.Service.Transition(
            new(fixture.Entity.Id, InitiativeLifecycleState.Active, fixture.Entity.Version), default);

        AssertZeroMutation503(fixture, response.StatusCode, InitiativeLifecycleState.OnHold);
    }

    [Theory]
    [InlineData(InitiativeLifecycleState.Active)]
    [InlineData(InitiativeLifecycleState.OnHold)]
    public async Task Governed_cancellation_is_503_and_has_zero_mutation(InitiativeLifecycleState initialState)
    {
        var fixture = Fixture.With(initialState);

        var response = await fixture.Service.Transition(
            new(fixture.Entity.Id, InitiativeLifecycleState.Cancelled, fixture.Entity.Version,
                CancellationReasonCode: "strategic-realignment"), default);

        AssertZeroMutation503(fixture, response.StatusCode, initialState);
    }

    [Fact]
    public async Task Proposed_cancellation_succeeds_and_the_terminal_record_is_immutable()
    {
        var fixture = Fixture.With(InitiativeLifecycleState.Proposed);

        var cancelled = await fixture.Service.Transition(
            new(fixture.Entity.Id, InitiativeLifecycleState.Cancelled, fixture.Entity.Version,
                CancellationReasonCode: "strategic-realignment"), default);
        var update = await fixture.Service.Update(
            new(fixture.Entity.Id, "I-1", "Changed", null, null, null,
                fixture.Entity.Version, null, null, null), default);

        Assert.Equal(200, cancelled.StatusCode);
        Assert.Equal(InitiativeLifecycleState.Cancelled, fixture.Entity.LifecycleState);
        Assert.Equal(409, update.StatusCode);
        Assert.Single(fixture.Audit.Intents);
    }

    [Fact]
    public async Task Completion_without_v2_repository_is_503_and_has_zero_mutation()
    {
        var fixture = Fixture.With(InitiativeLifecycleState.Active);
        var initialVersion = fixture.Entity.Version;
        var closure = new InitiativeClosureRequest(
            "delivered-as-planned",
            "scope-completed",
            "Completed safely.",
            [],
            [],
            "tracking-required");

        var response = await fixture.Service.Transition(
            new(fixture.Entity.Id, InitiativeLifecycleState.Completed, fixture.Entity.Version,
                Closure: closure), default);

        Assert.Equal(503, response.StatusCode);
        Assert.Equal(InitiativeLifecycleState.Active, fixture.Entity.LifecycleState);
        Assert.Equal(initialVersion, fixture.Entity.Version);
        Assert.Equal(0, fixture.Repository.ReplaceCalls);
        Assert.Empty(fixture.Audit.Intents);
        Assert.Equal(0, fixture.UnitOfWork.Calls);
    }

    [Theory]
    [InlineData(InitiativeAuthorityDisposition.Unknown)]
    [InlineData(InitiativeAuthorityDisposition.Unavailable)]
    public async Task Nonvalid_classification_authority_is_503(InitiativeAuthorityDisposition disposition)
    {
        var authority = ClassificationAuthority.Types(new(disposition, []));
        var fixture = Fixture.Empty(authority);

        var response = await fixture.Service.Create(
            new("I-1", "Initiative", null, null, "submitted", null), default);

        Assert.Equal(503, response.StatusCode);
        Assert.Empty(fixture.Repository.Items);
        Assert.Equal(0, fixture.UnitOfWork.Calls);
    }

    public static TheoryData<IReadOnlyList<InitiativeContractOption>> MalformedOptions => new()
    {
        Array.Empty<InitiativeContractOption>(),
        new[] { new InitiativeContractOption("", "Label") },
        new[] { new InitiativeContractOption("code", "") },
        new[] { new InitiativeContractOption("duplicate", "One"), new InitiativeContractOption("duplicate", "Two") },
        new[] { new InitiativeContractOption(null!, "Label") },
        new[] { new InitiativeContractOption("code", null!) }
    };

    [Theory]
    [MemberData(nameof(MalformedOptions))]
    public async Task Malformed_classification_options_are_503(IReadOnlyList<InitiativeContractOption> options)
    {
        var fixture = Fixture.Empty(ClassificationAuthority.Types(
            new(InitiativeAuthorityDisposition.Valid, options)));

        var response = await fixture.Service.Create(
            new("I-1", "Initiative", null, null, "submitted", null), default);

        Assert.Equal(503, response.StatusCode);
        Assert.Empty(fixture.Repository.Items);
    }

    [Fact]
    public async Task Valid_authority_with_unknown_submitted_code_is_400()
    {
        var fixture = Fixture.Empty(ClassificationAuthority.Valid());

        var response = await fixture.Service.Create(
            new("I-1", "Initiative", null, null, "unknown", null), default);

        Assert.Equal(400, response.StatusCode);
        Assert.Empty(fixture.Repository.Items);
        Assert.Equal(0, fixture.UnitOfWork.Calls);
    }

    [Fact]
    public async Task Contracts_v2_is_200_only_when_both_authorities_are_valid_and_well_formed()
    {
        var fixture = Fixture.Empty(ClassificationAuthority.Valid());

        var response = await fixture.Service.GetContracts(default);

        Assert.Equal(200, response.StatusCode);
        Assert.Single(response.Data!.InitiativeTypes);
        Assert.Single(response.Data.Priorities);
    }

    [Fact]
    public async Task Contracts_v2_is_200_when_distinct_codes_share_the_same_nonempty_label()
    {
        var authority = ClassificationAuthority.Types(new(
            InitiativeAuthorityDisposition.Valid,
            [
                new InitiativeContractOption("strategic", "High"),
                new InitiativeContractOption("regulatory", "High")
            ]));
        var fixture = Fixture.Empty(authority);

        var response = await fixture.Service.GetContracts(default);

        Assert.Equal(200, response.StatusCode);
        Assert.Equal(["strategic", "regulatory"],
            response.Data!.InitiativeTypes.Select(option => option.Code));
        Assert.All(response.Data.InitiativeTypes, option => Assert.Equal("High", option.Label));
    }

    [Theory]
    [InlineData(InitiativeAuthorityDisposition.Unknown)]
    [InlineData(InitiativeAuthorityDisposition.Unavailable)]
    public async Task Contracts_v2_is_503_when_either_authority_is_nonvalid(
        InitiativeAuthorityDisposition disposition)
    {
        var authority = new ClassificationAuthority(
            ClassificationAuthority.ValidResult,
            new(disposition, []));
        var fixture = Fixture.Empty(authority);

        var response = await fixture.Service.GetContracts(default);

        Assert.Equal(503, response.StatusCode);
    }

    [Fact]
    public async Task Contracts_v2_is_503_when_either_payload_is_malformed()
    {
        var authority = new ClassificationAuthority(
            ClassificationAuthority.ValidResult,
            new(InitiativeAuthorityDisposition.Valid,
                [new InitiativeContractOption("duplicate", "One"), new InitiativeContractOption("duplicate", "Two")]));
        var fixture = Fixture.Empty(authority);

        var response = await fixture.Service.GetContracts(default);

        Assert.Equal(503, response.StatusCode);
    }

    [Fact]
    public async Task OnHold_with_unresolved_recipient_succeeds_and_writes_transactional_disposition()
    {
        var fixture = Fixture.With(InitiativeLifecycleState.Active);

        var response = await fixture.Service.Transition(
            new(fixture.Entity.Id, InitiativeLifecycleState.OnHold, fixture.Entity.Version,
                HoldReasonCode: "funding-paused"), default);

        Assert.Equal(200, response.StatusCode);
        Assert.Equal(InitiativeLifecycleState.OnHold, fixture.Entity.LifecycleState);
        Assert.Equal([InitiativeWarnings.RecipientUnresolved], response.Data!.Warnings);
        Assert.Single(fixture.Audit.Intents);
        Assert.Equal(InitiativeWarnings.RecipientUnresolved, fixture.Audit.Intents[0].Mutation);
        Assert.Equal(1, fixture.UnitOfWork.Calls);
        Assert.DoesNotContain(typeof(InitiativeService).GetConstructors().Single().GetParameters(),
            parameter => parameter.Name!.Contains("notification", StringComparison.OrdinalIgnoreCase)
                || parameter.Name.Contains("recipient", StringComparison.OrdinalIgnoreCase));
        Assert.DoesNotContain(typeof(InitiativeService).Assembly.GetTypes(),
            type => type.Name.Contains("InitiativeRecipientAuthority", StringComparison.Ordinal)
                || type.Name.Contains("InitiativeNotificationRequester", StringComparison.Ordinal));
    }

    private static void AssertZeroMutation503(
        Fixture fixture, int statusCode, InitiativeLifecycleState expectedState)
    {
        Assert.Equal(503, statusCode);
        Assert.Equal(expectedState, fixture.Entity.LifecycleState);
        Assert.Equal(fixture.InitialVersion, fixture.Entity.Version);
        Assert.Equal(0, fixture.Repository.ReplaceCalls);
        Assert.Empty(fixture.Audit.Intents);
        Assert.Equal(0, fixture.UnitOfWork.Calls);
    }

    private sealed class Fixture
    {
        private Fixture(
            Initiative entity,
            InitiativeRepository repository,
            AuditRepository audit,
            UnitOfWork unitOfWork,
            InitiativeService service)
        {
            Entity = entity;
            Repository = repository;
            Audit = audit;
            UnitOfWork = unitOfWork;
            Service = service;
            InitialVersion = entity.Version;
        }

        public Initiative Entity { get; }
        public InitiativeRepository Repository { get; }
        public AuditRepository Audit { get; }
        public UnitOfWork UnitOfWork { get; }
        public InitiativeService Service { get; }
        public int InitialVersion { get; }

        public static Fixture With(
            InitiativeLifecycleState state,
            IInitiativeClassificationAuthority? classifications = null)
        {
            var tenant = Guid.NewGuid();
            var actor = Guid.NewGuid();
            var entity = new Initiative(tenant, actor, "I-1", "Initiative", null, null,
                "type", "priority", new DateOnly(2026, 9, 1), new DateOnly(2026, 9, 2));
            if (state is InitiativeLifecycleState.Active or InitiativeLifecycleState.OnHold)
                entity.Transition(actor, InitiativeLifecycleState.Active);
            if (state == InitiativeLifecycleState.OnHold)
                entity.Transition(actor, InitiativeLifecycleState.OnHold);
            return Create(entity, classifications ?? ClassificationAuthority.Valid());
        }

        public static Fixture Empty(IInitiativeClassificationAuthority classifications)
        {
            var tenant = Guid.NewGuid();
            var actor = Guid.NewGuid();
            return Create(new Initiative(tenant, actor, "unused", "Unused", null, null,
                null, null, null, null), classifications, includeEntity: false);
        }

        private static Fixture Create(
            Initiative entity,
            IInitiativeClassificationAuthority classifications,
            bool includeEntity = true)
        {
            var repository = new InitiativeRepository();
            if (includeEntity) repository.Items.Add(entity);
            var audit = new AuditRepository();
            var unitOfWork = new UnitOfWork();
            var context = new RequestContext(entity.TenantId, entity.CreatedBy);
            var service = new InitiativeService(repository, new PortfolioRepository(), audit, unitOfWork,
                context, context, new CorrelationContext(), new Access(), classifications);
            return new(entity, repository, audit, unitOfWork, service);
        }
    }

    private sealed class InitiativeRepository : IInitiativeRepository
    {
        public List<Initiative> Items { get; } = [];
        public int ReplaceCalls { get; private set; }
        public Task<Initiative?> GetByIdAsync(Guid tenantId, Guid id, CancellationToken cancellationToken) =>
            Task.FromResult(Items.SingleOrDefault(x => x.TenantId == tenantId && x.Id == id && !x.IsDeleted));
        public Task<IReadOnlyList<Initiative>> ListAsync(Guid tenantId, CancellationToken cancellationToken) =>
            Task.FromResult<IReadOnlyList<Initiative>>(Items.Where(x => x.TenantId == tenantId && !x.IsDeleted).ToArray());
        public Task<bool> CodeExistsAsync(Guid tenantId, string normalizedCode, Guid? excludingId, CancellationToken cancellationToken) =>
            Task.FromResult(Items.Any(x => x.TenantId == tenantId && x.Id != excludingId && x.Code == normalizedCode));
        public Task AddAsync(Initiative entity, CancellationToken cancellationToken) { Items.Add(entity); return Task.CompletedTask; }
        public Task ReplaceAsync(Initiative entity, int expectedVersion, CancellationToken cancellationToken)
        {
            ReplaceCalls++;
            Assert.Equal(expectedVersion + 1, entity.Version);
            return Task.CompletedTask;
        }
    }

    private sealed class PortfolioRepository : IPortfolioRepository
    {
        public Task<Portfolio?> GetByIdAsync(Guid tenantId, Guid id, CancellationToken cancellationToken) => Task.FromResult<Portfolio?>(null);
        public Task<IReadOnlyList<Portfolio>> ListAsync(Guid tenantId, CancellationToken cancellationToken) => Task.FromResult<IReadOnlyList<Portfolio>>([]);
        public Task<bool> CodeExistsAsync(Guid tenantId, string normalizedCode, Guid? excludingId, CancellationToken cancellationToken) => Task.FromResult(false);
        public Task AddAsync(Portfolio entity, CancellationToken cancellationToken) => Task.CompletedTask;
        public Task ReplaceAsync(Portfolio entity, int expectedVersion, CancellationToken cancellationToken) => Task.CompletedTask;
        public Task AdvanceInvestmentCaseCollectionFenceAsync(Portfolio portfolio, CancellationToken cancellationToken) => Task.CompletedTask;
    }

    private sealed class AuditRepository : IAuditIntentRepository
    {
        public List<AuditIntent> Intents { get; } = [];
        public Task AddAsync(AuditIntent intent, CancellationToken cancellationToken) { Intents.Add(intent); return Task.CompletedTask; }
    }

    private sealed class UnitOfWork : IPpmUnitOfWork
    {
        public int Calls { get; private set; }
        public async Task<T> ExecuteInTransactionAsync<T>(Func<CancellationToken, Task<T>> operation, CancellationToken cancellationToken)
        {
            Calls++;
            return await operation(cancellationToken);
        }
    }

    private sealed record RequestContext(Guid TenantId, Guid ActorId) : ITenantContext, ICurrentActorContext;
    private sealed class CorrelationContext : ICorrelationContext { public Guid CorrelationId { get; } = Guid.NewGuid(); }
    private sealed class Access : IPpmAccessAuthorizer
    {
        public Task<PpmAccessDecision> AuthorizeAsync(string permission, CancellationToken cancellationToken) =>
            Task.FromResult(PpmAccessDecision.Allowed);
    }

    private sealed class ClassificationAuthority(
        InitiativeClassificationResult types,
        InitiativeClassificationResult priorities) : IInitiativeClassificationAuthority
    {
        public static InitiativeClassificationResult ValidResult { get; } = new(
            InitiativeAuthorityDisposition.Valid, [new InitiativeContractOption("type", "Type")]);
        public static ClassificationAuthority Valid() => new(ValidResult,
            new(InitiativeAuthorityDisposition.Valid, [new InitiativeContractOption("priority", "Priority")]));
        public static ClassificationAuthority Types(InitiativeClassificationResult types) => new(types,
            new(InitiativeAuthorityDisposition.Valid, [new InitiativeContractOption("priority", "Priority")]));
        public Task<InitiativeClassificationResult> GetTypesAsync(CancellationToken cancellationToken) => Task.FromResult(types);
        public Task<InitiativeClassificationResult> GetPrioritiesAsync(CancellationToken cancellationToken) => Task.FromResult(priorities);
    }

}
