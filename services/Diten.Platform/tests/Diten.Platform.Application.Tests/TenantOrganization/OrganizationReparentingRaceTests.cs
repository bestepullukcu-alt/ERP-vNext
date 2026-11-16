using Diten.Platform.Application.Common;
using Diten.Platform.Application.Features.TenantOrganization;
using Diten.Platform.Application.Features.TenantOrganization.Commands;
using Diten.Platform.Application.Features.TenantOrganization.Handlers.CommandHandlers;
using Diten.Platform.Application.Features.TenantOrganization.Services;
using Diten.Platform.Application.Tests.Persistence;
using Diten.Platform.Domain.Entities.Organization;
using Diten.Platform.Domain.Repositories;
using Diten.Platform.Infrastructure.Persistence;
using Diten.Platform.Infrastructure.Persistence.Repositories;
using Diten.Platform.Infrastructure.Persistence.Schema;
using MongoDB.Driver;
using Xunit;

namespace Diten.Platform.Application.Tests.TenantOrganization;

/// <summary>
/// MOD-0288 — the re-parenting race, with the interleaving HELD rather than hoped for.
///
/// <para><c>OrganizationMatrixReportingTests.Two_concurrent_reparentings_cannot_both_land</c> releases two writers
/// together and catches the dangerous interleaving only when the scheduler happens to produce it (in CI, under load,
/// it did: "both re-parentings landed"). These tests stop one writer at the exact point of danger and run the other
/// writer to completion inside that gap, so every run exercises it.</para>
/// </summary>
[Trait("Category", "Mongo")]
public sealed class OrganizationReparentingRaceTests
{
    /*
     * ⚠ TWO HOLD POINTS, BECAUSE ONE IS NOT ENOUGH. Held BEFORE the commit, the second writer meets the first one's
     * uncommitted token. Held AFTER it, the second writer reads the committed token — and a fix that committed the
     * token without the unit write (the original bug, only moved) would let it validate against a graph without
     * the edge and close the cycle there. Each hold point catches a weakening the other one misses.
     */
    [Theory]
    [InlineData(HoldPoint.BeforeCommit)]
    [InlineData(HoldPoint.AfterCommit)]
    public async Task A_held_writer_cannot_let_a_second_writer_close_a_cycle(HoldPoint point)
    {
        await using var harness = await MongoIntegrationHarness.CreateAsync(SchemaProfile.Organization);
        var hold = new HoldFirstCommit(point);
        var heldRepository = new OrganizationUnitRepository(harness.DbContext, harness.TenantContext, hold);
        var repository = new OrganizationUnitRepository(harness.DbContext, harness.TenantContext);
        var legalEntityId = Guid.NewGuid();

        /*
         * ⚠ TWO ROUNDS. Round 0 is the tenant's first parent mutation (no token document yet: it is created holding
         * zero before the transaction); round 1 is every later mutation, racing on an existing document.
         */
        for (var round = 0; round < 2; round++)
        {
            var a = await CreateAsync(repository, harness.TenantId, $"HOLD-A{round}", legalEntityId);
            var b = await CreateAsync(repository, harness.TenantId, $"HOLD-B{round}", legalEntityId);
            hold.Arm();

            // Writer 1 has won the token and written A→B, and is stopped at the hold point.
            var writer1 = Task.Factory.StartNew(
                () => Handler(heldRepository).Handle(Reparent(a, b, legalEntityId), CancellationToken.None),
                CancellationToken.None, TaskCreationOptions.LongRunning, TaskScheduler.Default).Unwrap();
            await hold.Held.WaitAsync(TimeSpan.FromSeconds(30));

            // Writer 2 runs start to finish inside that gap. Its validation cannot see A→B yet; the token must stop it.
            var second = await Handler(repository).Handle(Reparent(b, a, legalEntityId), CancellationToken.None);
            hold.Release();
            var first = await writer1;

            var storedA = await repository.GetByIdAsync(a.Id);
            var storedB = await repository.GetByIdAsync(b.Id);
            Assert.False(
                storedA!.ParentOrganizationUnitId == b.Id && storedB!.ParentOrganizationUnitId == a.Id,
                $"round {round}: both edges are stored (writer 1 {first.StatusCode}, writer 2 {second.StatusCode}) — the graph is cyclic");
            Assert.Equal(204, first.StatusCode);
            Assert.Equal(409, second.StatusCode); // the CAS (before commit) or the cycle check (after commit)
            Assert.Equal(b.Id, storedA.ParentOrganizationUnitId);
            Assert.Null(storedB!.ParentOrganizationUnitId);
        }
    }

    [Fact]
    public async Task A_rename_that_read_the_unit_before_a_reparenting_cannot_write_the_old_line_back()
    {
        await using var harness = await MongoIntegrationHarness.CreateAsync(SchemaProfile.Organization);
        var repository = new OrganizationUnitRepository(harness.DbContext, harness.TenantContext);
        var legalEntityId = Guid.NewGuid();
        var (unit, moved) = await SeedMovedUnitAsync(repository, harness.TenantId, legalEntityId, "RENAME");

        var outcome = await RunAcrossReparentingAsync(repository, unit, moved, legalEntityId, stale =>
            Handler(stale).Handle(
                new UpdateOrganizationUnitCommand(
                    unit.Id,
                    new OrganizationUnitRequest(unit.Code, "Renamed", legalEntityId, unit.ParentOrganizationUnitId),
                    AllowReportingLineChange: false),
                CancellationToken.None));

        Assert.Equal(409, outcome.StatusCode);
        var stored = await repository.GetByIdAsync(unit.Id);
        Assert.Equal(moved.Id, stored!.ParentOrganizationUnitId);
    }

    [Fact]
    public async Task An_archive_that_read_the_unit_before_a_reparenting_cannot_write_the_old_line_back()
    {
        await using var harness = await MongoIntegrationHarness.CreateAsync(SchemaProfile.Organization);
        var repository = new OrganizationUnitRepository(harness.DbContext, harness.TenantContext);
        var legalEntityId = Guid.NewGuid();
        var (unit, moved) = await SeedMovedUnitAsync(repository, harness.TenantId, legalEntityId, "ARCHIVE");

        var outcome = await RunAcrossReparentingAsync(repository, unit, moved, legalEntityId, stale =>
            new ArchiveOrganizationUnitCommandHandler(stale).Handle(
                new ArchiveOrganizationUnitCommand(unit.Id), CancellationToken.None));

        Assert.Equal(409, outcome.StatusCode);
        var stored = await repository.GetByIdAsync(unit.Id);
        Assert.Equal(moved.Id, stored!.ParentOrganizationUnitId);
        Assert.False(stored.IsArchived);
    }

    [Fact]
    public async Task A_rename_that_read_the_unit_before_an_archive_cannot_bring_it_back()
    {
        await using var harness = await MongoIntegrationHarness.CreateAsync(SchemaProfile.Organization);
        var repository = new OrganizationUnitRepository(harness.DbContext, harness.TenantContext);
        var legalEntityId = Guid.NewGuid();
        var unit = await CreateAsync(repository, harness.TenantId, "REVIVE-U", legalEntityId);

        var paused = new PauseAfterFirstRead(repository, unit.Id);
        var rename = Task.Factory.StartNew(
            () => Handler(paused).Handle(
                new UpdateOrganizationUnitCommand(
                    unit.Id, new OrganizationUnitRequest(unit.Code, "Renamed", legalEntityId, null), AllowReportingLineChange: false),
                CancellationToken.None),
            CancellationToken.None, TaskCreationOptions.LongRunning, TaskScheduler.Default).Unwrap();
        await paused.Read.Task.WaitAsync(TimeSpan.FromSeconds(30));

        var archive = await new ArchiveOrganizationUnitCommandHandler(repository)
            .Handle(new ArchiveOrganizationUnitCommand(unit.Id), CancellationToken.None);
        Assert.Equal(204, archive.StatusCode);

        paused.Release.TrySetResult();
        Assert.Equal(409, (await rename).StatusCode);
        var stored = await Collection(harness).Find(x => x.Id == unit.Id).SingleAsync();
        Assert.True(stored.IsArchived);
        Assert.Equal("REVIVE-U", stored.Name);
    }

    [Fact]
    public async Task A_database_that_cannot_run_the_atomic_step_refuses_the_reporting_line_with_503()
    {
        var repo = new InMemoryOrganizationUnitRepository(InMemoryTenant);
        var parent = new OrganizationUnit { TenantId = InMemoryTenant, Code = "P", Name = "P", LegalEntityId = InMemoryLegalEntity };
        var child = new OrganizationUnit { TenantId = InMemoryTenant, Code = "C", Name = "C", LegalEntityId = InMemoryLegalEntity };
        repo.Add(parent);
        repo.Add(child);

        var response = await new UpdateOrganizationUnitCommandHandler(repo, new NoTransactions(repo), new AlwaysValid())
            .Handle(Reparent(child, parent, InMemoryLegalEntity), CancellationToken.None);

        Assert.Equal(503, response.StatusCode);
    }

    // ── helpers ──────────────────────────────────────────────────────────────────────────────────────────

    private static readonly Guid InMemoryTenant = Guid.NewGuid();
    private static readonly Guid InMemoryLegalEntity = Guid.NewGuid();

    private static UpdateOrganizationUnitCommandHandler Handler(OrganizationUnitRepository repository) =>
        new(repository, repository, new AlwaysValid());

    private static UpdateOrganizationUnitCommandHandler Handler(PauseAfterFirstRead stale) =>
        new(stale, stale.Inner, new AlwaysValid());

    private static UpdateOrganizationUnitCommand Reparent(OrganizationUnit child, OrganizationUnit parent, Guid legalEntityId) =>
        new(child.Id, new OrganizationUnitRequest(child.Code, child.Name, legalEntityId, parent.Id), AllowReportingLineChange: true);

    private static async Task<OrganizationUnit> CreateAsync(
        OrganizationUnitRepository repository, Guid tenantId, string code, Guid legalEntityId, Guid? parentId = null) =>
        await repository.CreateAsync(new OrganizationUnit
        {
            TenantId = tenantId, Code = code, Name = code, LegalEntityId = legalEntityId, ParentOrganizationUnitId = parentId
        });

    /// <summary>A unit under P, and Q — the parent a concurrent re-parenting will move it to.</summary>
    private static async Task<(OrganizationUnit Unit, OrganizationUnit Moved)> SeedMovedUnitAsync(
        OrganizationUnitRepository repository, Guid tenantId, Guid legalEntityId, string prefix)
    {
        var p = await CreateAsync(repository, tenantId, $"{prefix}-P", legalEntityId);
        var q = await CreateAsync(repository, tenantId, $"{prefix}-Q", legalEntityId);
        var unit = await CreateAsync(repository, tenantId, $"{prefix}-U", legalEntityId, p.Id);
        return (unit, q);
    }

    /// <summary>
    /// Runs <paramref name="staleWrite"/> so that it reads the unit, then a re-parenting of the unit to
    /// <paramref name="moved"/> lands, then the stale write continues. Returns the stale write's answer.
    /// </summary>
    private static async Task<Response<NoContent>> RunAcrossReparentingAsync(
        OrganizationUnitRepository repository,
        OrganizationUnit unit,
        OrganizationUnit moved,
        Guid legalEntityId,
        Func<PauseAfterFirstRead, Task<Response<NoContent>>> staleWrite)
    {
        var paused = new PauseAfterFirstRead(repository, unit.Id);
        var stale = Task.Factory.StartNew(() => staleWrite(paused),
            CancellationToken.None, TaskCreationOptions.LongRunning, TaskScheduler.Default).Unwrap();
        await paused.Read.Task.WaitAsync(TimeSpan.FromSeconds(30));

        var move = await Handler(repository).Handle(Reparent(unit, moved, legalEntityId), CancellationToken.None);
        Assert.Equal(204, move.StatusCode);

        paused.Release.TrySetResult();
        return await stale;
    }

    private static IMongoCollection<OrganizationUnit> Collection(MongoIntegrationHarness harness) =>
        harness.DbContext.Database.GetCollection<OrganizationUnit>(PlatformCollections.OrganizationUnits);

    public enum HoldPoint { BeforeCommit, AfterCommit }

    /// <summary>Holds the first transaction that reaches the hold point, once per <see cref="Arm"/>.</summary>
    private sealed class HoldFirstCommit(HoldPoint point) : IPlatformTransactionFaultProbe
    {
        private TaskCompletionSource _held = new(TaskCreationOptions.RunContinuationsAsynchronously);
        private TaskCompletionSource _release = new(TaskCreationOptions.RunContinuationsAsynchronously);
        private int _armed;

        public Task Held => _held.Task;

        public void Arm()
        {
            _held = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
            _release = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
            Volatile.Write(ref _armed, 1);
        }

        public void Release() => _release.TrySetResult();

        public Task BeforeCommitAsync(IPlatformTransactionSession session, int commitAttempt, CancellationToken ct) =>
            point == HoldPoint.BeforeCommit ? HoldOnceAsync(ct) : Task.CompletedTask;

        public Task AfterCommitAsync(IPlatformTransactionSession session, int commitAttempt, CancellationToken ct) =>
            point == HoldPoint.AfterCommit ? HoldOnceAsync(ct) : Task.CompletedTask;

        private async Task HoldOnceAsync(CancellationToken ct)
        {
            if (Interlocked.Exchange(ref _armed, 0) == 1)
            {
                _held.TrySetResult();
                await _release.Task.WaitAsync(TimeSpan.FromSeconds(30), ct);
            }
        }
    }

    /// <summary>Lets the first read of one unit through, then stops its caller until released.</summary>
    private sealed class PauseAfterFirstRead(OrganizationUnitRepository inner, Guid unitId) : IOrganizationUnitRepository
    {
        private int _paused;

        public OrganizationUnitRepository Inner => inner;
        public TaskCompletionSource Read { get; } = new(TaskCreationOptions.RunContinuationsAsynchronously);
        public TaskCompletionSource Release { get; } = new(TaskCreationOptions.RunContinuationsAsynchronously);

        public async Task<OrganizationUnit?> GetByIdAsync(Guid id, CancellationToken ct = default)
        {
            var unit = await inner.GetByIdAsync(id, ct);
            if (id == unitId && Interlocked.Exchange(ref _paused, 1) == 0)
            {
                Read.TrySetResult();
                await Release.Task.WaitAsync(TimeSpan.FromSeconds(30), ct);
            }

            return unit;
        }

        public Task<OrganizationUnit> CreateAsync(OrganizationUnit organizationUnit, CancellationToken ct = default) => inner.CreateAsync(organizationUnit, ct);
        public Task<IReadOnlyList<OrganizationUnit>> GetAllAsync(CancellationToken ct = default) => inner.GetAllAsync(ct);
        public Task<bool> ExistsByCodeAsync(string code, Guid? excludeId = null, CancellationToken ct = default) => inner.ExistsByCodeAsync(code, excludeId, ct);
        public Task UpdateAsync(OrganizationUnit organizationUnit, CancellationToken ct = default) => inner.UpdateAsync(organizationUnit, ct);
        public Task DeleteAsync(Guid id, CancellationToken ct = default) => inner.DeleteAsync(id, ct);
    }

    /// <summary>A graph whose atomic step cannot run, as on a standalone mongod.</summary>
    private sealed class NoTransactions(InMemoryOrganizationUnitRepository inner) : IOrganizationReportingGraphRepository
    {
        public Task<IReadOnlyList<OrganizationUnit>> GetByIdsAsync(IReadOnlyCollection<Guid> ids, CancellationToken ct = default) => inner.GetByIdsAsync(ids, ct);
        public Task<long> ReadStructureTokenAsync(CancellationToken ct = default) => inner.ReadStructureTokenAsync(ct);

        public Task<bool> TryAdvanceStructureTokenAndUpdateAsync(long expectedToken, OrganizationUnit unit, CancellationToken ct = default) =>
            throw new PlatformTransactionUnavailableException("MongoDB deployment does not support Platform transactions.");
    }

    private sealed class AlwaysValid : ILegalEntityReferenceValidator
    {
        public Task<Response<LegalEntityReferenceDto>> ValidateAsync(Guid legalEntityId, CancellationToken ct = default) =>
            Task.FromResult(Response<LegalEntityReferenceDto>.Success(
                new LegalEntityReferenceDto(legalEntityId, "Legal", "Legal", "ACTIVE", true)));
    }
}
