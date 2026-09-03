using System.Text.Json;
using System.Text.Json.Serialization;
using Diten.PpmService.Application.Common;
using Diten.PpmService.Application.Features.Initiatives;
using Diten.PpmService.Domain.Entities;
using Diten.PpmService.Domain.Repositories;
using Diten.Shared.Core;
using Xunit;

namespace Diten.PpmService.Tests.Initiatives;

public sealed class InitiativeLifecycleContractsV2Tests
{
    private static readonly InitiativeLifecycleState[] ExpectedStates =
    [
        InitiativeLifecycleState.Proposed,
        InitiativeLifecycleState.Active,
        InitiativeLifecycleState.OnHold,
        InitiativeLifecycleState.Completed,
        InitiativeLifecycleState.Cancelled
    ];

    [Fact]
    public async Task GetLifecycleContracts_WhenClassificationAuthorityIsUnavailable_ShouldReturnCanonicalContract()
    {
        var fixture = Fixture.Create(classificationDisposition: InitiativeAuthorityDisposition.Unavailable);

        var lifecycle = await fixture.Service.GetLifecycleContracts(default);

        Assert.Equal(200, lifecycle.StatusCode);
        Assert.Equal(0, fixture.Classifications.TypesCalls);
        Assert.Equal(0, fixture.Classifications.PrioritiesCalls);

        var classifications = await fixture.Service.GetContracts(default);
        Assert.Equal(503, classifications.StatusCode);
        Assert.Equal(1, fixture.Classifications.TypesCalls);
        Assert.Equal(1, fixture.Classifications.PrioritiesCalls);
    }

    [Fact]
    public async Task GetLifecycleContracts_WhenSuccessful_ShouldExposeExactVersionAndCanonicalVocabularies()
    {
        var response = await Fixture.Create().Service.GetLifecycleContracts(default);

        Assert.Equal("2", response.Data!.ContractVersion);
        Assert.Equal(InitiativeVocabularies.CancellationReasons, response.Data.CancellationReasons);
        Assert.Equal(InitiativeVocabularies.HoldReasons, response.Data.HoldReasons);
        Assert.Equal(InitiativeVocabularies.CompletionOutcomes, response.Data.CompletionOutcomes);
        Assert.Equal(InitiativeVocabularies.ClosureReasons, response.Data.ClosureReasons);
        Assert.Equal(InitiativeVocabularies.BenefitDispositions, response.Data.BenefitDispositions);
    }

    [Fact]
    public async Task GetLifecycleContracts_ForAllStates_ShouldExactlyMatchDomainTransitionBehavior()
    {
        var contract = (await Fixture.Create().Service.GetLifecycleContracts(default)).Data!;

        Assert.Equal(ExpectedStates, Enum.GetValues<InitiativeLifecycleState>());
        Assert.Equal(ExpectedStates, contract.AllowedTargetStatesBySource.Keys);
        foreach (var source in ExpectedStates)
        {
            var probe = New(source, ready: true);
            var expected = ExpectedStates.Where(probe.CanTransitionTo).ToArray();
            var grouped = contract.AllowedTargetStatesBySource[source];
            var flat = contract.Transitions.Where(x => x.SourceState == source).Select(x => x.TargetState);
            Assert.Equal(expected, grouped);
            Assert.Equal(expected, flat);
        }
    }

    [Fact]
    public async Task GetLifecycleContracts_WhenSuccessful_ShouldUseOnlyClosedCompanionAndApprovalWireSets()
    {
        var transitions = (await Fixture.Create().Service.GetLifecycleContracts(default)).Data!.Transitions;

        Assert.Equal(["cancellation-reason", "closure", "hold-reason", "none"],
            transitions.Select(x => x.RequiredCompanionDataKind).Distinct().Order().ToArray());
        Assert.Equal(["approval-authority-required", "direct"],
            transitions.Select(x => x.ApprovalDependencyDisposition).Distinct().Order().ToArray());
        Assert.All(transitions, transition =>
        {
            Assert.NotEqual(transition.SourceState, transition.TargetState);
            Assert.False(string.IsNullOrWhiteSpace(transition.RequiredCompanionDataKind));
            Assert.False(string.IsNullOrWhiteSpace(transition.ApprovalDependencyDisposition));
        });
        Assert.Equal(transitions.Count,
            transitions.DistinctBy(x => (x.SourceState, x.TargetState)).Count());
    }

    public static TheoryData<string> MalformedContractScenarios => new()
    {
        "duplicate-transition",
        "duplicate-vocabulary",
        "blank-vocabulary",
        "invalid-companion-data",
        "invalid-approval-disposition",
        "missing-lifecycle-state",
        "grouped-flat-mismatch"
    };

    [Theory]
    [MemberData(nameof(MalformedContractScenarios))]
    public async Task GetLifecycleContracts_WhenAuthorityReturnsMalformedContract_ShouldReturn503(string scenario)
    {
        var canonical = (await Fixture.Create().Service.GetLifecycleContracts(default)).Data!;
        var malformed = Malform(canonical, scenario);
        var fixture = Fixture.Create(lifecycleContractAuthority: new LifecycleContractAuthority(malformed));

        var response = await fixture.Service.GetLifecycleContracts(default);

        Assert.Equal(503, response.StatusCode);
        Assert.Null(response.Data);
    }

    [Fact]
    public async Task GetLifecycleContracts_WhenSerializedForWeb_ShouldPreserveExactEnumAndWireValues()
    {
        var contract = (await Fixture.Create().Service.GetLifecycleContracts(default)).Data!;
        var options = new JsonSerializerOptions(JsonSerializerDefaults.Web);
        options.Converters.Add(new JsonStringEnumConverter(namingPolicy: null, allowIntegerValues: false));

        using var json = JsonDocument.Parse(JsonSerializer.Serialize(contract, options));
        var root = json.RootElement;
        Assert.Equal("2", root.GetProperty("contractVersion").GetString());
        Assert.True(root.GetProperty("allowedTargetStatesBySource").TryGetProperty("Proposed", out var proposedTargets));
        Assert.Contains(proposedTargets.EnumerateArray(), value => value.GetString() == "Active");
        var proposedActive = root.GetProperty("transitions").EnumerateArray().Single(x =>
            x.GetProperty("sourceState").GetString() == "Proposed"
            && x.GetProperty("targetState").GetString() == "Active");
        Assert.Equal("none", proposedActive.GetProperty("requiredCompanionDataKind").GetString());
        Assert.Equal("approval-authority-required",
            proposedActive.GetProperty("approvalDependencyDisposition").GetString());
    }

    [Theory]
    [InlineData(PpmAccessDecision.Forbidden, 403)]
    [InlineData(PpmAccessDecision.DependencyUnavailable, 503)]
    public async Task GetLifecycleContracts_WhenReadAccessFails_ShouldPreserveHttpDistinction(
        PpmAccessDecision decision, int expectedStatus)
    {
        var fixture = Fixture.Create(readDecision: decision);

        var response = await fixture.Service.GetLifecycleContracts(default);

        Assert.Equal(expectedStatus, response.StatusCode);
        Assert.Equal(0, fixture.Classifications.TypesCalls);
        Assert.Equal(0, fixture.Repository.ListCalls);
        Assert.Equal(0, fixture.Repository.GetCalls);
    }

    [Fact]
    public async Task GetById_WhenActorIsReadOnly_ShouldMarkEveryCanonicalCandidateForbidden()
    {
        var entity = New(InitiativeLifecycleState.Active, ready: true);
        var fixture = Fixture.Create(entity: entity, lifecycleDecision: PpmAccessDecision.Forbidden);

        var response = await fixture.Service.GetById(new(entity.Id), default);

        Assert.Equal(200, response.StatusCode);
        Assert.Equal(3, response.Data!.AvailableActions.Count);
        Assert.All(response.Data.AvailableActions, action =>
        {
            Assert.Equal("forbidden", action.Availability);
            Assert.Equal("lifecycle-permission-denied", action.ReasonCode);
        });
    }

    [Theory]
    [InlineData(InitiativeLifecycleState.Completed)]
    [InlineData(InitiativeLifecycleState.Cancelled)]
    public async Task GetById_WhenRecordIsTerminal_ShouldExposeNoLifecycleActions(InitiativeLifecycleState state)
    {
        var entity = New(state, ready: true);
        var response = await Fixture.Create(entity: entity).Service.GetById(new(entity.Id), default);

        Assert.Empty(response.Data!.AvailableActions);
    }

    [Fact]
    public async Task GetById_WhenActivationDataIsIncomplete_ShouldPreferRecordNotReadyOverDependencyUnavailable()
    {
        var entity = New(InitiativeLifecycleState.Proposed, ready: false);
        var fixture = Fixture.Create(entity: entity, lifecycleDecision: PpmAccessDecision.DependencyUnavailable);

        var actions = (await fixture.Service.GetById(new(entity.Id), default)).Data!.AvailableActions;

        var activation = Assert.Single(actions, x => x.TargetState == InitiativeLifecycleState.Active);
        Assert.Equal("record-not-ready", activation.Availability);
        Assert.Equal("activation-data-incomplete", activation.ReasonCode);
    }

    [Fact]
    public async Task GetById_WhenActorIsForbiddenAndRecordIsNotReady_ShouldPreferForbidden()
    {
        var entity = New(InitiativeLifecycleState.Proposed, ready: false);
        var fixture = Fixture.Create(entity: entity, lifecycleDecision: PpmAccessDecision.Forbidden);

        var activation = Assert.Single((await fixture.Service.GetById(new(entity.Id), default)).Data!.AvailableActions,
            x => x.TargetState == InitiativeLifecycleState.Active);

        Assert.Equal("forbidden", activation.Availability);
    }

    [Theory]
    [InlineData(InitiativeLifecycleState.Proposed, InitiativeLifecycleState.Active)]
    [InlineData(InitiativeLifecycleState.OnHold, InitiativeLifecycleState.Active)]
    [InlineData(InitiativeLifecycleState.Active, InitiativeLifecycleState.Cancelled)]
    [InlineData(InitiativeLifecycleState.OnHold, InitiativeLifecycleState.Cancelled)]
    public async Task GetById_WhenTransitionRequiresApprovalAuthority_ShouldMarkDependencyUnavailable(
        InitiativeLifecycleState source, InitiativeLifecycleState target)
    {
        var entity = New(source, ready: true);

        var action = Assert.Single((await Fixture.Create(entity: entity).Service.GetById(new(entity.Id), default))
            .Data!.AvailableActions, x => x.TargetState == target);

        Assert.Equal("dependency-unavailable", action.Availability);
        Assert.Equal("approval-authority-unavailable", action.ReasonCode);
    }

    [Theory]
    [InlineData(InitiativeLifecycleState.Proposed, InitiativeLifecycleState.Cancelled, "cancellation-reason")]
    [InlineData(InitiativeLifecycleState.Active, InitiativeLifecycleState.OnHold, "hold-reason")]
    [InlineData(InitiativeLifecycleState.Active, InitiativeLifecycleState.Completed, "closure")]
    [InlineData(InitiativeLifecycleState.OnHold, InitiativeLifecycleState.Completed, "closure")]
    public async Task GetById_WhenTransitionIsDirect_ShouldMarkAvailable(
        InitiativeLifecycleState source, InitiativeLifecycleState target, string companionData)
    {
        var entity = New(source, ready: true);

        var action = Assert.Single((await Fixture.Create(entity: entity).Service.GetById(new(entity.Id), default))
            .Data!.AvailableActions, x => x.TargetState == target);

        Assert.Equal("available", action.Availability);
        Assert.Equal("available", action.ReasonCode);
        Assert.Equal(companionData, action.RequiredCompanionDataKind);
    }

    [Fact]
    public async Task ListAndGetById_ForSameRecord_ShouldExposeIdenticalActionProjection()
    {
        var entity = New(InitiativeLifecycleState.Active, ready: true);
        var fixture = Fixture.Create(entity: entity, lifecycleDecision: PpmAccessDecision.Forbidden);

        var list = await fixture.Service.List(new(), default);
        var detail = await fixture.Service.GetById(new(entity.Id), default);

        Assert.Equal(detail.Data!.AvailableActions, Assert.Single(list.Data!).AvailableActions);
    }

    [Fact]
    public async Task GetById_WhenIdBelongsToAnotherTenant_ShouldReturn404WithoutDisclosure()
    {
        var entity = New(InitiativeLifecycleState.Proposed, ready: true);
        var fixture = Fixture.Create(entity: entity, requestTenant: Guid.NewGuid());

        var response = await fixture.Service.GetById(new(entity.Id), default);

        Assert.Equal(404, response.StatusCode);
        Assert.Null(response.Data);
    }

    [Fact]
    public async Task Transition_WhenLifecyclePermissionIsDenied_ShouldNotReadOrMutateRecord()
    {
        var entity = New(InitiativeLifecycleState.Proposed, ready: true);
        var fixture = Fixture.Create(entity: entity, lifecycleDecision: PpmAccessDecision.Forbidden);

        var response = await fixture.Service.Transition(new(entity.Id, InitiativeLifecycleState.Cancelled,
            entity.Version, CancellationReasonCode: "strategic-realignment"), default);

        Assert.Equal(403, response.StatusCode);
        Assert.Equal(0, fixture.Repository.GetCalls);
        Assert.Equal(0, fixture.Repository.ReplaceCalls);
        Assert.Equal(InitiativeLifecycleState.Proposed, entity.LifecycleState);
    }

    [Fact]
    public async Task Transition_WhenApprovalDependencyIsUnavailable_ShouldPreserveStateVersionAndWrites()
    {
        var entity = New(InitiativeLifecycleState.Proposed, ready: true);
        var fixture = Fixture.Create(entity: entity);
        var version = entity.Version;

        var response = await fixture.Service.Transition(new(entity.Id, InitiativeLifecycleState.Active,
            version), default);

        Assert.Equal(503, response.StatusCode);
        Assert.Equal(InitiativeLifecycleState.Proposed, entity.LifecycleState);
        Assert.Equal(version, entity.Version);
        Assert.Equal(0, fixture.Repository.ReplaceCalls);
        Assert.Equal(0, fixture.UnitOfWorkCalls);
    }

    private static Initiative New(InitiativeLifecycleState state, bool ready)
    {
        var tenant = Guid.NewGuid();
        var actor = Guid.NewGuid();
        var entity = new Initiative(tenant, actor, "INIT-1", "Initiative", null, null,
            ready ? "type" : null, ready ? "priority" : null,
            ready ? new DateOnly(2026, 9, 1) : null, ready ? new DateOnly(2026, 9, 2) : null);
        if (state is InitiativeLifecycleState.Active or InitiativeLifecycleState.OnHold or InitiativeLifecycleState.Completed)
            entity.Transition(actor, InitiativeLifecycleState.Active);
        if (state == InitiativeLifecycleState.OnHold)
            entity.Transition(actor, InitiativeLifecycleState.OnHold);
        if (state == InitiativeLifecycleState.Completed)
            entity.Transition(actor, InitiativeLifecycleState.Completed);
        if (state == InitiativeLifecycleState.Cancelled)
            entity.Transition(actor, InitiativeLifecycleState.Cancelled);
        return entity;
    }

    private static InitiativeLifecycleContractsV2 Malform(
        InitiativeLifecycleContractsV2 canonical, string scenario)
    {
        var grouped = canonical.AllowedTargetStatesBySource.ToDictionary(
            entry => entry.Key, entry => entry.Value);
        var transitions = canonical.Transitions.ToList();
        var cancellationReasons = canonical.CancellationReasons.ToList();

        switch (scenario)
        {
            case "duplicate-transition":
                transitions.Add(transitions[0]);
                break;
            case "duplicate-vocabulary":
                cancellationReasons.Add(cancellationReasons[0]);
                break;
            case "blank-vocabulary":
                cancellationReasons[0] = " ";
                break;
            case "invalid-companion-data":
                transitions[0] = transitions[0] with { RequiredCompanionDataKind = "unknown" };
                break;
            case "invalid-approval-disposition":
                transitions[0] = transitions[0] with { ApprovalDependencyDisposition = "unknown" };
                break;
            case "missing-lifecycle-state":
                grouped.Remove(InitiativeLifecycleState.Completed);
                break;
            case "grouped-flat-mismatch":
                grouped[InitiativeLifecycleState.Proposed] = [];
                break;
            default:
                throw new ArgumentOutOfRangeException(nameof(scenario), scenario, null);
        }

        return canonical with
        {
            AllowedTargetStatesBySource = grouped,
            Transitions = transitions,
            CancellationReasons = cancellationReasons
        };
    }

    private sealed class Fixture
    {
        private Fixture(InitiativeService service, Repository repository, ClassificationAuthority classifications,
            UnitOfWork unitOfWork)
        {
            Service = service;
            Repository = repository;
            Classifications = classifications;
            UnitOfWork = unitOfWork;
        }

        public InitiativeService Service { get; }
        public Repository Repository { get; }
        public ClassificationAuthority Classifications { get; }
        public UnitOfWork UnitOfWork { get; }
        public int UnitOfWorkCalls => UnitOfWork.Calls;

        public static Fixture Create(Initiative? entity = null,
            InitiativeAuthorityDisposition classificationDisposition = InitiativeAuthorityDisposition.Valid,
            PpmAccessDecision readDecision = PpmAccessDecision.Allowed,
            PpmAccessDecision lifecycleDecision = PpmAccessDecision.Allowed,
            Guid? requestTenant = null,
            IInitiativeLifecycleContractAuthority? lifecycleContractAuthority = null)
        {
            entity ??= New(InitiativeLifecycleState.Proposed, ready: true);
            var repository = new Repository(entity);
            var classifications = new ClassificationAuthority(classificationDisposition);
            var unitOfWork = new UnitOfWork();
            var context = new Context(requestTenant ?? entity.TenantId, entity.CreatedBy);
            var access = new Access(readDecision, lifecycleDecision);
            var service = new InitiativeService(repository, new PortfolioRepository(), new AuditRepository(),
                unitOfWork, context, context, new Correlation(), access, classifications,
                lifecycleContracts: lifecycleContractAuthority);
            return new(service, repository, classifications, unitOfWork);
        }
    }

    private sealed class Repository(params Initiative[] entities) : IInitiativeRepository
    {
        public int GetCalls { get; private set; }
        public int ListCalls { get; private set; }
        public int ReplaceCalls { get; private set; }
        public Task<Initiative?> GetByIdAsync(Guid tenantId, Guid id, CancellationToken ct)
        {
            GetCalls++;
            return Task.FromResult(entities.SingleOrDefault(x => x.TenantId == tenantId && x.Id == id && !x.IsDeleted));
        }
        public Task<IReadOnlyList<Initiative>> ListAsync(Guid tenantId, CancellationToken ct)
        {
            ListCalls++;
            return Task.FromResult<IReadOnlyList<Initiative>>(entities.Where(x => x.TenantId == tenantId && !x.IsDeleted).ToArray());
        }
        public Task<bool> CodeExistsAsync(Guid tenantId, string code, Guid? excludingId, CancellationToken ct) => Task.FromResult(false);
        public Task AddAsync(Initiative entity, CancellationToken ct) => Task.CompletedTask;
        public Task ReplaceAsync(Initiative entity, int expectedVersion, CancellationToken ct)
        {
            ReplaceCalls++;
            return Task.CompletedTask;
        }
    }

    private sealed class ClassificationAuthority(InitiativeAuthorityDisposition disposition)
        : IInitiativeClassificationAuthority
    {
        public int TypesCalls { get; private set; }
        public int PrioritiesCalls { get; private set; }
        private InitiativeClassificationResult Result => new(disposition,
            disposition == InitiativeAuthorityDisposition.Valid
                ? [new("type", "Type"), new("priority", "Priority")]
                : []);
        public Task<InitiativeClassificationResult> GetTypesAsync(CancellationToken ct)
        {
            TypesCalls++;
            return Task.FromResult(Result);
        }
        public Task<InitiativeClassificationResult> GetPrioritiesAsync(CancellationToken ct)
        {
            PrioritiesCalls++;
            return Task.FromResult(Result);
        }
    }

    private sealed class Access(PpmAccessDecision read, PpmAccessDecision lifecycle) : IPpmAccessAuthorizer
    {
        public Task<PpmAccessDecision> AuthorizeAsync(string permission, CancellationToken ct) =>
            Task.FromResult(permission == PpmPermissions.InitiativesLifecycle ? lifecycle : read);
    }

    private sealed class LifecycleContractAuthority(InitiativeLifecycleContractsV2 contract)
        : IInitiativeLifecycleContractAuthority
    {
        public Task<InitiativeLifecycleContractsV2> GetLifecycleContractsAsync(CancellationToken cancellationToken) =>
            Task.FromResult(contract);
    }

    private sealed class UnitOfWork : IPpmUnitOfWork
    {
        public int Calls { get; private set; }
        public async Task<T> ExecuteInTransactionAsync<T>(Func<CancellationToken, Task<T>> operation, CancellationToken ct)
        {
            Calls++;
            return await operation(ct);
        }
    }

    private sealed record Context(Guid TenantId, Guid ActorId) : ITenantContext, ICurrentActorContext;
    private sealed class Correlation : ICorrelationContext { public Guid CorrelationId { get; } = Guid.NewGuid(); }
    private sealed class AuditRepository : IAuditIntentRepository
    {
        public Task AddAsync(AuditIntent intent, CancellationToken ct) => Task.CompletedTask;
    }
    private sealed class PortfolioRepository : IPortfolioRepository
    {
        public Task<Portfolio?> GetByIdAsync(Guid tenantId, Guid id, CancellationToken ct) => Task.FromResult<Portfolio?>(null);
        public Task<IReadOnlyList<Portfolio>> ListAsync(Guid tenantId, CancellationToken ct) => Task.FromResult<IReadOnlyList<Portfolio>>([]);
        public Task<bool> CodeExistsAsync(Guid tenantId, string code, Guid? excludingId, CancellationToken ct) => Task.FromResult(false);
        public Task AddAsync(Portfolio entity, CancellationToken ct) => Task.CompletedTask;
        public Task ReplaceAsync(Portfolio entity, int expectedVersion, CancellationToken ct) => Task.CompletedTask;
        public Task AdvanceInvestmentCaseCollectionFenceAsync(Portfolio portfolio, CancellationToken ct) => Task.CompletedTask;
    }
}
