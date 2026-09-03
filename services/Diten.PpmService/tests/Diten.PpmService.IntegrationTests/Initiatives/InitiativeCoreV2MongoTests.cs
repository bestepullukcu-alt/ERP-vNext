using Diten.PpmService.Domain.Entities;
using Diten.PpmService.Domain.Exceptions;
using Diten.PpmService.Persistence;
using Diten.PpmService.Persistence.Mongo;
using Diten.PpmService.Persistence.Repositories;
using Diten.PpmService.IntegrationTests.GateI.DecisionTrace;
using MongoDB.Driver;
using Xunit;

namespace Diten.PpmService.IntegrationTests.Initiatives;

public sealed class InitiativeCoreV2MongoTests : IAsyncLifetime
{
    private readonly GateIDisposableMongoReplicaSet _mongo = new();
    private PpmMongoContext _context = null!;
    private InitiativeRepository _repository = null!;
    private PpmUnitOfWork _unitOfWork = null!;

    public async Task InitializeAsync()
    {
        await _mongo.InitializeAsync();
        var client = new MongoClient(_mongo.ConnectionString);
        _context = new(client, client.GetDatabase(_mongo.DatabaseName));
        _repository = new(_context);
        _unitOfWork = new(_context);
        await _context.EnsureInitiativeV2IndexesAsync(default);
    }

    public Task DisposeAsync() => _mongo.DisposeAsync();

    [Fact]
    public async Task Tenant_soft_delete_and_stale_CAS_are_enforced()
    {
        var tenant = Guid.NewGuid();
        var actor = Guid.NewGuid();
        var initiative = New(tenant, actor, "I-1");
        await _unitOfWork.ExecuteInTransactionAsync(async ct => { await _repository.AddAsync(initiative, ct); return 0; }, default);
        Assert.Null(await _repository.GetByIdAsync(Guid.NewGuid(), initiative.Id, default));

        initiative.Update(actor, "I-1", "Changed", null, null, null, null, null, null);
        await Assert.ThrowsAsync<OptimisticConcurrencyException>(() => _unitOfWork.ExecuteInTransactionAsync(async ct =>
        {
            await _repository.ReplaceAsync(initiative, 99, ct);
            return 0;
        }, default));

        var current = await _repository.GetByIdAsync(tenant, initiative.Id, default);
        current!.SoftDelete(actor);
        await _unitOfWork.ExecuteInTransactionAsync(async ct => { await _repository.ReplaceAsync(current, 1, ct); return 0; }, default);
        Assert.Null(await _repository.GetByIdAsync(tenant, initiative.Id, default));
    }

    [Fact]
    public async Task Closure_and_completed_transition_commit_atomically_and_duplicate_closure_rolls_back()
    {
        var tenant = Guid.NewGuid();
        var actor = Guid.NewGuid();
        var initiative = New(tenant, actor, "I-2", ready: true);
        await _unitOfWork.ExecuteInTransactionAsync(async ct => { await _repository.AddAsync(initiative, ct); return 0; }, default);
        initiative.Transition(actor, InitiativeLifecycleState.Active);
        await _unitOfWork.ExecuteInTransactionAsync(async ct => { await _repository.ReplaceAsync(initiative, 1, ct); return 0; }, default);
        initiative.Transition(actor, InitiativeLifecycleState.Completed);
        var closure = Closure(tenant, actor, initiative);
        await _unitOfWork.ExecuteInTransactionAsync(async ct =>
        {
            await _repository.ReplaceAsync(initiative, 2, ct);
            await _repository.AddClosureAsync(closure, ct);
            return 0;
        }, default);
        Assert.Equal(InitiativeLifecycleState.Completed,
            (await _repository.GetByIdAsync(tenant, initiative.Id, default))!.LifecycleState);
        Assert.Single(await _context.InitiativeClosures.Find(x => x.TenantId == tenant && x.InitiativeId == initiative.Id).ToListAsync());

        await Assert.ThrowsAnyAsync<Exception>(() => _unitOfWork.ExecuteInTransactionAsync(async ct =>
        {
            await _repository.AddClosureAsync(Closure(tenant, actor, initiative), ct);
            return 0;
        }, default));
        Assert.Single(await _context.InitiativeClosures.Find(x => x.TenantId == tenant && x.InitiativeId == initiative.Id).ToListAsync());
    }

    [Fact]
    public async Task One_active_successor_per_terminal_is_enforced_by_tenant_first_unique_index()
    {
        var tenant = Guid.NewGuid();
        var actor = Guid.NewGuid();
        var old = New(tenant, actor, "OLD", ready: true);
        old.Transition(actor, InitiativeLifecycleState.Active);
        old.Transition(actor, InitiativeLifecycleState.Completed);
        await _unitOfWork.ExecuteInTransactionAsync(async ct => { await _repository.AddAsync(old, ct); return 0; }, default);
        var first = new Initiative(tenant, actor, "NEW-1", "First", null, null, null, null, null, null, old.Id);
        await _unitOfWork.ExecuteInTransactionAsync(async ct => { await _repository.AddAsync(first, ct); return 0; }, default);
        var second = new Initiative(tenant, actor, "NEW-2", "Second", null, null, null, null, null, null, old.Id);
        await Assert.ThrowsAsync<OptimisticConcurrencyException>(() => _unitOfWork.ExecuteInTransactionAsync(async ct =>
        {
            await _repository.AddAsync(second, ct);
            return 0;
        }, default));
        Assert.Equal(first.Id, (await _repository.GetActiveSuccessorAsync(tenant, old.Id, default))!.Id);
    }

    [Theory]
    [InlineData(true)]
    [InlineData(false)]
    public async Task Completion_rolls_back_state_version_and_closure_when_closure_or_audit_fails(bool closureFails)
    {
        var tenant = Guid.NewGuid();
        var actor = Guid.NewGuid();
        var initiative = New(tenant, actor, closureFails ? "CLOSE-FAIL" : "AUDIT-FAIL", ready: true);
        initiative.Transition(actor, InitiativeLifecycleState.Active);
        await _unitOfWork.ExecuteInTransactionAsync(async ct =>
        {
            await _repository.AddAsync(initiative, ct);
            if (closureFails) await _repository.AddClosureAsync(Closure(tenant, actor, initiative), ct);
            return 0;
        }, default);
        var before = (await _repository.GetByIdAsync(tenant, initiative.Id, default))!;
        var beforeVersion = before.Version;
        var beforeClosureCount = await _context.InitiativeClosures
            .CountDocumentsAsync(x => x.TenantId == tenant && x.InitiativeId == initiative.Id);

        await Assert.ThrowsAnyAsync<Exception>(() => _unitOfWork.ExecuteInTransactionAsync(async ct =>
        {
            before.Transition(actor, InitiativeLifecycleState.Completed);
            await _repository.ReplaceAsync(before, beforeVersion, ct);
            await _repository.AddClosureAsync(Closure(tenant, actor, before), ct);
            if (!closureFails) throw new InvalidOperationException("audit persistence failed");
            return 0;
        }, default));

        var persisted = (await _repository.GetByIdAsync(tenant, initiative.Id, default))!;
        Assert.Equal(InitiativeLifecycleState.Active, persisted.LifecycleState);
        Assert.Equal(beforeVersion, persisted.Version);
        Assert.Equal(beforeClosureCount, await _context.InitiativeClosures
            .CountDocumentsAsync(x => x.TenantId == tenant && x.InitiativeId == initiative.Id));
    }

    [Fact]
    public async Task Successor_claim_and_insert_are_atomic_and_duplicate_or_stale_claims_fail_closed()
    {
        var tenant = Guid.NewGuid();
        var actor = Guid.NewGuid();
        var terminal = New(tenant, actor, "TERMINAL", ready: true);
        terminal.Transition(actor, InitiativeLifecycleState.Active);
        terminal.Transition(actor, InitiativeLifecycleState.Completed);
        await _unitOfWork.ExecuteInTransactionAsync(async ct => { await _repository.AddAsync(terminal, ct); return 0; }, default);
        var terminalBeforeClaim = (await _repository.GetByIdAsync(tenant, terminal.Id, default))!;
        var expectedVersion = terminalBeforeClaim.Version;
        var expectedUpdatedAt = terminalBeforeClaim.UpdatedAtUtc;
        var expectedUpdatedBy = terminalBeforeClaim.UpdatedBy;
        var expectedName = terminalBeforeClaim.Name;
        var successor = new Initiative(tenant, actor, "SUCCESSOR", "Successor", null, null,
            null, null, null, null, terminal.Id);

        await _unitOfWork.ExecuteInTransactionAsync(async ct =>
        {
            await _repository.ClaimTerminalForSuccessorAsync(tenant, terminal.Id, successor.Id, expectedVersion, ct);
            await _repository.AddAsync(successor, ct);
            return 0;
        }, default);

        var persistedTerminal = (await _repository.GetByIdAsync(tenant, terminal.Id, default))!;
        Assert.Equal(InitiativeLifecycleState.Completed, persistedTerminal.LifecycleState);
        Assert.Equal(expectedVersion, persistedTerminal.Version);
        Assert.Equal(expectedUpdatedAt, persistedTerminal.UpdatedAtUtc);
        Assert.Equal(expectedUpdatedBy, persistedTerminal.UpdatedBy);
        Assert.Equal(expectedName, persistedTerminal.Name);
        Assert.Equal(successor.Id, (await _repository.GetActiveSuccessorAsync(tenant, terminal.Id, default))!.Id);

        var duplicate = new Initiative(tenant, actor, "DUPLICATE", "Duplicate", null, null,
            null, null, null, null, terminal.Id);
        await Assert.ThrowsAsync<OptimisticConcurrencyException>(() => _unitOfWork.ExecuteInTransactionAsync(async ct =>
        {
            await _repository.ClaimTerminalForSuccessorAsync(tenant, terminal.Id, duplicate.Id, expectedVersion, ct);
            await _repository.AddAsync(duplicate, ct);
            return 0;
        }, default));
        Assert.Single(await _context.Initiatives.Find(x => x.TenantId == tenant && x.SupersedesInitiativeId == terminal.Id).ToListAsync());
    }

    [Fact]
    public async Task Successor_claim_and_insert_roll_back_together_on_later_failure()
    {
        var tenant = Guid.NewGuid();
        var actor = Guid.NewGuid();
        var terminal = New(tenant, actor, "ROLLBACK", ready: true);
        terminal.Transition(actor, InitiativeLifecycleState.Active);
        terminal.Transition(actor, InitiativeLifecycleState.Completed);
        await _unitOfWork.ExecuteInTransactionAsync(async ct => { await _repository.AddAsync(terminal, ct); return 0; }, default);
        var successor = new Initiative(tenant, actor, "ROLLBACK-NEXT", "Next", null, null,
            null, null, null, null, terminal.Id);

        await Assert.ThrowsAsync<InvalidOperationException>(() => _unitOfWork.ExecuteInTransactionAsync(async ct =>
        {
            await _repository.ClaimTerminalForSuccessorAsync(tenant, terminal.Id, successor.Id, terminal.Version, ct);
            await _repository.AddAsync(successor, ct);
            throw new InvalidOperationException("audit persistence failed");
#pragma warning disable CS0162
            return 0;
#pragma warning restore CS0162
        }, default));

        var persistedTerminal = (await _repository.GetByIdAsync(tenant, terminal.Id, default))!;
        Assert.Equal(terminal.Version, persistedTerminal.Version);
        Assert.Null(await _repository.GetByIdAsync(tenant, successor.Id, default));
    }

    private static Initiative New(Guid tenant, Guid actor, string code, bool ready = false) => new(tenant, actor,
        code, code, null, null, ready ? "type" : null, ready ? "priority" : null,
        ready ? new DateOnly(2026, 9, 1) : null, ready ? new DateOnly(2026, 9, 2) : null);

    private static InitiativeClosure Closure(Guid tenant, Guid actor, Initiative initiative) => new(tenant, actor,
        initiative.Id, "delivered-as-planned", "scope-completed", DateTime.UtcNow,
        "Completed safely.", [], [], "tracking-required", initiative.CreatedAtUtc);
}
