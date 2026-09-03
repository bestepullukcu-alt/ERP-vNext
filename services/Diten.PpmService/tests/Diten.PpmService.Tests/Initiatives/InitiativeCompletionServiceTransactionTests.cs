using Diten.PpmService.Application.Common;
using Diten.PpmService.Application.Features.Initiatives;
using Diten.PpmService.Domain.Entities;
using Diten.PpmService.Domain.Repositories;
using Xunit;

namespace Diten.PpmService.Tests.Initiatives;

public sealed class InitiativeCompletionServiceTransactionTests
{
    [Fact]
    public async Task Completion_commits_replace_closure_and_audit_in_order_and_returns_atomic_result()
    {
        var fixture = Fixture.Create();

        var response = await fixture.Service.Transition(fixture.Command(), default);

        Assert.Equal(200, response.StatusCode);
        Assert.Equal(InitiativeLifecycleState.Completed, response.Data!.Initiative.LifecycleState);
        Assert.NotNull(response.Data.Closure);
        Assert.Equal(["replace", "closure", "audit"], fixture.Operations);
        Assert.Single(fixture.Repository.Closures);
        Assert.Single(fixture.Audit.Intents);
    }

    [Theory]
    [InlineData(true, false)]
    [InlineData(false, true)]
    public async Task Completion_failure_rolls_back_entity_closure_and_audit(bool closureFails, bool auditFails)
    {
        var fixture = Fixture.Create(closureFails, auditFails);
        var version = fixture.Entity.Version;

        await Assert.ThrowsAsync<InvalidOperationException>(() => fixture.Service.Transition(fixture.Command(), default));

        Assert.Equal(InitiativeLifecycleState.Active, fixture.Entity.LifecycleState);
        Assert.Equal(version, fixture.Entity.Version);
        Assert.Empty(fixture.Repository.Closures);
        Assert.Empty(fixture.Audit.Intents);
        Assert.Equal(1, fixture.UnitOfWork.Calls);
    }

    private sealed class Fixture
    {
        private Fixture(Initiative entity, Repository repository, Audit audit, RollbackUnit unit,
            InitiativeService service, List<string> operations) =>
            (Entity, Repository, Audit, UnitOfWork, Service, Operations) =
            (entity, repository, audit, unit, service, operations);
        public Initiative Entity { get; }
        public Repository Repository { get; }
        public Audit Audit { get; }
        public RollbackUnit UnitOfWork { get; }
        public InitiativeService Service { get; }
        public List<string> Operations { get; }
        public TransitionInitiativeLifecycleCommand Command() => new(Entity.Id, InitiativeLifecycleState.Completed,
            Entity.Version, Closure: new("delivered-as-planned", "scope-completed", "Completed safely.", [], [], "tracking-required"));

        public static Fixture Create(bool closureFails = false, bool auditFails = false)
        {
            var tenant = Guid.NewGuid();
            var actor = Guid.NewGuid();
            var entity = new Initiative(tenant, actor, "I-1", "Initiative", null, null,
                "type", "priority", new DateOnly(2026, 9, 1), new DateOnly(2026, 9, 2));
            entity.Transition(actor, InitiativeLifecycleState.Active);
            var operations = new List<string>();
            var repository = new Repository(entity, operations, closureFails);
            var audit = new Audit(operations, auditFails);
            var unit = new RollbackUnit(entity, repository, audit);
            var context = new Context(tenant, actor);
            var service = new InitiativeService(repository, new Portfolios(), audit, unit, context, context,
                new Correlation(), new Access(), Classification.Valid);
            return new(entity, repository, audit, unit, service, operations);
        }
    }

    private sealed class RollbackUnit(Initiative entity, Repository repository, Audit audit) : IPpmUnitOfWork
    {
        public int Calls { get; private set; }
        public async Task<T> ExecuteInTransactionAsync<T>(Func<CancellationToken, Task<T>> operation, CancellationToken ct)
        {
            Calls++;
            var state = entity.LifecycleState;
            var version = entity.Version;
            var updatedAt = entity.UpdatedAtUtc;
            var updatedBy = entity.UpdatedBy;
            try { return await operation(ct); }
            catch
            {
                Set(nameof(Initiative.LifecycleState), state);
                Set(nameof(Initiative.Version), version);
                Set(nameof(Initiative.UpdatedAtUtc), updatedAt);
                Set(nameof(Initiative.UpdatedBy), updatedBy);
                repository.Closures.Clear();
                audit.Intents.Clear();
                throw;
            }
        }
        private void Set(string name, object? value) => typeof(Initiative).GetProperty(name)!.SetValue(entity, value);
    }

    private sealed class Repository(Initiative entity, List<string> operations, bool closureFails)
        : IInitiativeRepository, IInitiativeV2Repository
    {
        public List<InitiativeClosure> Closures { get; } = [];
        public Task<Initiative?> GetByIdAsync(Guid tenantId, Guid id, CancellationToken ct) =>
            Task.FromResult(entity.TenantId == tenantId && entity.Id == id ? entity : null);
        public Task<IReadOnlyList<Initiative>> ListAsync(Guid tenantId, CancellationToken ct) => Task.FromResult<IReadOnlyList<Initiative>>([entity]);
        public Task<bool> CodeExistsAsync(Guid tenantId, string code, Guid? excludingId, CancellationToken ct) => Task.FromResult(false);
        public Task AddAsync(Initiative value, CancellationToken ct) => Task.CompletedTask;
        public Task ReplaceAsync(Initiative value, int version, CancellationToken ct) { operations.Add("replace"); return Task.CompletedTask; }
        public Task<Initiative?> GetActiveSuccessorAsync(Guid tenantId, Guid terminalId, CancellationToken ct) => Task.FromResult<Initiative?>(null);
        public Task ClaimTerminalForSuccessorAsync(Guid tenantId, Guid terminalId, Guid successorId, int version, CancellationToken ct) => Task.CompletedTask;
        public Task AddClosureAsync(InitiativeClosure closure, CancellationToken ct)
        {
            operations.Add("closure");
            if (closureFails) throw new InvalidOperationException("closure persistence failed");
            Closures.Add(closure);
            return Task.CompletedTask;
        }
    }

    private sealed class Audit(List<string> operations, bool fails) : IAuditIntentRepository
    {
        public List<AuditIntent> Intents { get; } = [];
        public Task AddAsync(AuditIntent intent, CancellationToken ct)
        {
            operations.Add("audit");
            if (fails) throw new InvalidOperationException("audit persistence failed");
            Intents.Add(intent);
            return Task.CompletedTask;
        }
    }
    private sealed record Context(Guid TenantId, Guid ActorId) : ITenantContext, ICurrentActorContext;
    private sealed class Correlation : ICorrelationContext { public Guid CorrelationId { get; } = Guid.NewGuid(); }
    private sealed class Access : IPpmAccessAuthorizer
    { public Task<PpmAccessDecision> AuthorizeAsync(string permission, CancellationToken ct) => Task.FromResult(PpmAccessDecision.Allowed); }
    private sealed class Classification : IInitiativeClassificationAuthority
    {
        public static Classification Valid { get; } = new();
        public Task<InitiativeClassificationResult> GetTypesAsync(CancellationToken ct) => Task.FromResult(new InitiativeClassificationResult(InitiativeAuthorityDisposition.Valid, [new("type", "Type")]));
        public Task<InitiativeClassificationResult> GetPrioritiesAsync(CancellationToken ct) => Task.FromResult(new InitiativeClassificationResult(InitiativeAuthorityDisposition.Valid, [new("priority", "Priority")]));
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
