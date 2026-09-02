using Diten.PpmService.Application.Behaviors;
using Diten.PpmService.Application.Common;
using Diten.PpmService.Application.Features.Initiatives;
using Diten.PpmService.Domain.Entities;
using Diten.PpmService.Domain.Exceptions;
using Diten.PpmService.Domain.Repositories;
using Diten.Shared.Core;
using Microsoft.Extensions.Logging.Abstractions;
using Xunit;

namespace Diten.PpmService.Tests.Initiatives;

public sealed class InitiativeSupersessionServiceTests
{
    [Fact]
    public async Task Missing_and_cross_tenant_terminal_are_nondisclosing_404()
    {
        var fixture = Fixture.Create();
        var crossTenant = Terminal(Guid.NewGuid(), fixture.Actor, "OTHER");
        fixture.Repository.Items.Add(crossTenant);

        Assert.Equal(404, (await fixture.Service.CreateSuccessor(Command(Guid.NewGuid(), 1), default)).StatusCode);
        Assert.Equal(404, (await fixture.Service.CreateSuccessor(Command(crossTenant.Id, crossTenant.Version), default)).StatusCode);
        Assert.Empty(fixture.Repository.Claims);
    }

    [Fact]
    public async Task Nonterminal_and_stale_terminal_are_409_without_claim()
    {
        var fixture = Fixture.Create();
        var proposed = new Initiative(fixture.Tenant, fixture.Actor, "PROPOSED", "Proposed", null, null,
            null, null, null, null);
        var terminal = Terminal(fixture.Tenant, fixture.Actor, "TERMINAL");
        fixture.Repository.Items.AddRange([proposed, terminal]);

        Assert.Equal(409, (await fixture.Service.CreateSuccessor(Command(proposed.Id, proposed.Version), default)).StatusCode);
        Assert.Equal(409, (await fixture.Service.CreateSuccessor(Command(terminal.Id, terminal.Version - 1), default)).StatusCode);
        Assert.Empty(fixture.Repository.Claims);
    }

    [Fact]
    public async Task Duplicate_successor_is_409_without_second_claim()
    {
        var fixture = Fixture.Create();
        var terminal = Terminal(fixture.Tenant, fixture.Actor, "TERMINAL");
        fixture.Repository.Items.Add(terminal);
        fixture.Repository.Items.Add(new Initiative(fixture.Tenant, fixture.Actor, "EXISTING", "Existing", null,
            null, null, null, null, null, terminal.Id));

        var response = await fixture.Service.CreateSuccessor(Command(terminal.Id, terminal.Version), default);

        Assert.Equal(409, response.StatusCode);
        Assert.Empty(fixture.Repository.Claims);
    }

    [Fact]
    public async Task Transitive_cycle_is_409_without_claim_or_insert()
    {
        var fixture = Fixture.Create();
        var ancestor = new Initiative(fixture.Tenant, fixture.Actor, "ANCESTOR", "Ancestor", null, null,
            null, null, null, null);
        var terminal = Terminal(fixture.Tenant, fixture.Actor, "TERMINAL", ancestor.Id);
        typeof(Initiative).GetProperty(nameof(Initiative.SupersedesInitiativeId))!
            .SetValue(ancestor, terminal.Id);
        fixture.Repository.Items.AddRange([ancestor, terminal]);

        var response = await fixture.Service.CreateSuccessor(Command(terminal.Id, terminal.Version), default);

        Assert.Equal(409, response.StatusCode);
        Assert.Empty(fixture.Repository.Claims);
        Assert.Equal(2, fixture.Repository.Items.Count);
    }

    [Fact]
    public async Task Successful_successor_claims_terminal_and_inserts_proposed_record_atomically()
    {
        var fixture = Fixture.Create();
        var terminal = Terminal(fixture.Tenant, fixture.Actor, "TERMINAL");
        fixture.Repository.Items.Add(terminal);
        var originalVersion = terminal.Version;
        var originalUpdatedAt = terminal.UpdatedAtUtc;
        var originalUpdatedBy = terminal.UpdatedBy;
        var originalName = terminal.Name;

        var response = await fixture.Service.CreateSuccessor(Command(terminal.Id, terminal.Version), default);

        Assert.Equal(201, response.StatusCode);
        Assert.Equal(InitiativeLifecycleState.Proposed, response.Data!.LifecycleState);
        Assert.Equal(terminal.Id, response.Data.SupersedesInitiativeId);
        Assert.Equal(InitiativeLifecycleState.Completed, terminal.LifecycleState);
        Assert.Equal(originalVersion, terminal.Version);
        Assert.Equal(originalUpdatedAt, terminal.UpdatedAtUtc);
        Assert.Equal(originalUpdatedBy, terminal.UpdatedBy);
        Assert.Equal(originalName, terminal.Name);
        Assert.Single(fixture.Repository.Claims);
        Assert.Single(fixture.Audit.Intents);
        Assert.Equal(1, fixture.UnitOfWork.Calls);
    }

    [Fact]
    public async Task Unknown_commit_returns_503_and_is_never_automatically_retried()
    {
        var fixture = Fixture.Create(unknownCommit: true);
        var terminal = Terminal(fixture.Tenant, fixture.Actor, "TERMINAL");
        fixture.Repository.Items.Add(terminal);
        var command = Command(terminal.Id, terminal.Version);
        var behavior = new ExceptionHandlingBehavior<CreateInitiativeSuccessorCommand, Response<InitiativeV2Dto>>(
            NullLogger<ExceptionHandlingBehavior<CreateInitiativeSuccessorCommand, Response<InitiativeV2Dto>>>.Instance);

        var response = await behavior.Handle(command,
            () => fixture.Service.CreateSuccessor(command, default), default);

        Assert.Equal(503, response.StatusCode);
        Assert.Equal(1, fixture.UnitOfWork.Calls);
    }

    private static CreateInitiativeSuccessorCommand Command(Guid terminalId, int version) =>
        new(terminalId, "SUCCESSOR", "Successor", null, null, null, null, null, null, version);

    private static Initiative Terminal(Guid tenant, Guid actor, string code, Guid? supersedes = null)
    {
        var value = new Initiative(tenant, actor, code, code, null, null,
            "type", "priority", new DateOnly(2026, 9, 1), new DateOnly(2026, 9, 2), supersedes);
        value.Transition(actor, InitiativeLifecycleState.Active);
        value.Transition(actor, InitiativeLifecycleState.Completed);
        return value;
    }

    private sealed class Fixture
    {
        private Fixture(Guid tenant, Guid actor, Repository repository, Audit audit, UnitOfWork unitOfWork,
            InitiativeService service) => (Tenant, Actor, Repository, Audit, UnitOfWork, Service) =
            (tenant, actor, repository, audit, unitOfWork, service);
        public Guid Tenant { get; }
        public Guid Actor { get; }
        public Repository Repository { get; }
        public Audit Audit { get; }
        public UnitOfWork UnitOfWork { get; }
        public InitiativeService Service { get; }

        public static Fixture Create(bool unknownCommit = false)
        {
            var tenant = Guid.NewGuid();
            var actor = Guid.NewGuid();
            var repository = new Repository();
            var audit = new Audit();
            var unit = new UnitOfWork(unknownCommit);
            var context = new Context(tenant, actor);
            return new(tenant, actor, repository, audit, unit,
                new InitiativeService(repository, new Portfolios(), audit, unit, context, context,
                    new Correlation(), new Access(), Classification.Valid));
        }
    }

    private sealed class Repository : IInitiativeRepository, IInitiativeV2Repository
    {
        public List<Initiative> Items { get; } = [];
        public List<(Guid TerminalId, Guid SuccessorId)> Claims { get; } = [];
        public Task<Initiative?> GetByIdAsync(Guid tenantId, Guid id, CancellationToken ct) =>
            Task.FromResult(Items.SingleOrDefault(x => x.TenantId == tenantId && x.Id == id && !x.IsDeleted));
        public Task<IReadOnlyList<Initiative>> ListAsync(Guid tenantId, CancellationToken ct) =>
            Task.FromResult<IReadOnlyList<Initiative>>(Items.Where(x => x.TenantId == tenantId).ToArray());
        public Task<bool> CodeExistsAsync(Guid tenantId, string code, Guid? excludingId, CancellationToken ct) =>
            Task.FromResult(Items.Any(x => x.TenantId == tenantId && x.Id != excludingId && x.Code == code));
        public Task AddAsync(Initiative value, CancellationToken ct) { Items.Add(value); return Task.CompletedTask; }
        public Task ReplaceAsync(Initiative value, int version, CancellationToken ct) => Task.CompletedTask;
        public Task<Initiative?> GetActiveSuccessorAsync(Guid tenantId, Guid terminalId, CancellationToken ct) =>
            Task.FromResult(Items.SingleOrDefault(x => x.TenantId == tenantId && x.SupersedesInitiativeId == terminalId && !x.IsDeleted));
        public Task ClaimTerminalForSuccessorAsync(Guid tenantId, Guid terminalId, Guid successorId, int version, CancellationToken ct)
        {
            var terminal = Items.SingleOrDefault(x => x.TenantId == tenantId && x.Id == terminalId);
            if (terminal is null || !terminal.IsTerminal || terminal.Version != version)
                throw new OptimisticConcurrencyException("claim conflict");
            Claims.Add((terminalId, successorId));
            return Task.CompletedTask;
        }
        public Task AddClosureAsync(InitiativeClosure closure, CancellationToken ct) => Task.CompletedTask;
    }

    private sealed class UnitOfWork(bool unknownCommit) : IPpmUnitOfWork
    {
        public int Calls { get; private set; }
        public async Task<T> ExecuteInTransactionAsync<T>(Func<CancellationToken, Task<T>> operation, CancellationToken ct)
        {
            Calls++;
            if (unknownCommit) throw new TransactionUnavailableException("UnknownTransactionCommitResult");
            return await operation(ct);
        }
    }

    private sealed class Audit : IAuditIntentRepository
    {
        public List<AuditIntent> Intents { get; } = [];
        public Task AddAsync(AuditIntent intent, CancellationToken ct) { Intents.Add(intent); return Task.CompletedTask; }
    }
    private sealed record Context(Guid TenantId, Guid ActorId) : ITenantContext, ICurrentActorContext;
    private sealed class Correlation : ICorrelationContext { public Guid CorrelationId { get; } = Guid.NewGuid(); }
    private sealed class Access : IPpmAccessAuthorizer
    { public Task<PpmAccessDecision> AuthorizeAsync(string permission, CancellationToken ct) => Task.FromResult(PpmAccessDecision.Allowed); }
    private sealed class Classification : IInitiativeClassificationAuthority
    {
        public static Classification Valid { get; } = new();
        public Task<InitiativeClassificationResult> GetTypesAsync(CancellationToken ct) => Task.FromResult(
            new InitiativeClassificationResult(InitiativeAuthorityDisposition.Valid, [new("type", "Type")]));
        public Task<InitiativeClassificationResult> GetPrioritiesAsync(CancellationToken ct) => Task.FromResult(
            new InitiativeClassificationResult(InitiativeAuthorityDisposition.Valid, [new("priority", "Priority")]));
    }
    private sealed class Portfolios : IPortfolioRepository
    {
        public Task<Portfolio?> GetByIdAsync(Guid tenantId, Guid id, CancellationToken ct) => Task.FromResult<Portfolio?>(null);
        public Task<IReadOnlyList<Portfolio>> ListAsync(Guid tenantId, CancellationToken ct) => Task.FromResult<IReadOnlyList<Portfolio>>([]);
        public Task<bool> CodeExistsAsync(Guid tenantId, string code, Guid? excludingId, CancellationToken ct) => Task.FromResult(false);
        public Task AddAsync(Portfolio value, CancellationToken ct) => Task.CompletedTask;
        public Task ReplaceAsync(Portfolio value, int version, CancellationToken ct) => Task.CompletedTask;
        public Task AdvanceInvestmentCaseCollectionFenceAsync(Portfolio value, CancellationToken ct) => Task.CompletedTask;
    }
}
