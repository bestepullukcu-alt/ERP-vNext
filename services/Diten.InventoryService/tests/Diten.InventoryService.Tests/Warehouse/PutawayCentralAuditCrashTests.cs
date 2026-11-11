using Diten.BuildingBlocks.CentralAudit;
using Diten.InventoryService.Application.Common.Models;
using Diten.InventoryService.Application.Features.Inventory;
using Diten.InventoryService.Tests.CentralAudit;
using Microsoft.Extensions.DependencyInjection;
using MongoDB.Driver;
using Xunit;
using static Diten.InventoryService.Tests.Warehouse.PutawayRecoveryTests;
using static Diten.InventoryService.Tests.Warehouse.PutawayScenario;
using static Diten.InventoryService.Tests.Warehouse.WarehouseScenario;

namespace Diten.InventoryService.Tests.Warehouse;

/// <summary>
/// The dispatcher's orphan release touches EVERY <c>AwaitingCommand</c> row of the shared outbox older than its cut-off: this class runs
/// alone (xUnit starts a <c>DisableParallelization</c> collection after the parallel ones), so no other test's in-flight row is released.
/// </summary>
[CollectionDefinition(Name, DisableParallelization = true)]
public sealed class PutawayCentralAuditAloneCollection
{
    public const string Name = "MOD-0178 central-audit orphan release (alone)";
}

/// <summary>
/// MOD-0178 AC-15 — the crash case: the confirmation's transaction 1 commits (its central row waits <c>AwaitingCommand</c>) and the command
/// never reaches <c>ConfirmAsync</c> / <c>AbandonAsync</c> (here: it is held inside the ledger call — the process "stopped"). The host's
/// clock passes <c>CentralAudit:OrphanAfter</c> (10 min); the dispatcher's <c>RunOnceAsync</c> releases exactly that row as <c>Pending</c>
/// with <c>ConfirmedByCommand = false</c> (Platform: Unconfirmed). When the reconciler later posts the confirmation, the second row is the
/// <c>ReconcilePendingConfirmationCommand</c>'s, <c>ConfirmedByCommand = true</c>.
/// </summary>
[Collection(PutawayCentralAuditAloneCollection.Name)]
public sealed class PutawayCentralAuditCrashTests : IClassFixture<WarehouseApiFactory>
{
    private readonly WarehouseApiFactory _factory;

    public PutawayCentralAuditCrashTests(WarehouseApiFactory factory) => _factory = factory;

    private List<CentralAuditOutboxRecord> Rows(PutawayScenario s, string requestType)
        => CentralAuditInventoryTests.Outbox(_factory).Find(x => x.TenantId == s.Tenant && x.RequestType == requestType).ToList();

    [Fact]
    public async Task AC15_a_command_stopped_after_transaction_1_leaves_one_unconfirmed_row_released_by_the_dispatcher()
    {
        var s = await new PutawayScenario(_factory).ReadyAsync();
        await s.ReceiveAsync(100);
        var task = await s.TaskAsync(60);
        var id = TaskId(task);

        var stopped = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
        _factory.AroundPosting[s.Tenant] = async (_, _) =>
        {
            await stopped.Task.WaitAsync(TimeSpan.FromSeconds(60));       // the process "stopped" inside the ledger call
            return Response<MovementResponseDto>.Fail("INVENTORY_UNAVAILABLE", "test: no answer", 503);
        };
        Task<HttpResponseMessage> confirm;
        try
        {
            confirm = s.ConfirmAsync(task, 20, s.A01);
            await WaitForAsync(() => Rows(s, "ConfirmPutawayTaskCommand") is [{ Status: CentralAuditOutboxStatus.AwaitingCommand }]);
            Assert.Single(s.StoredTask(id)["Confirmations"].AsBsonArray);   // transaction 1 committed

            _factory.Clock.Advance(TimeSpan.FromMinutes(11));             // past OrphanAfter (10 min)
            await _factory.Services.GetRequiredService<CentralAuditDispatcher>().RunOnceAsync(CancellationToken.None);

            var released = Assert.Single(Rows(s, "ConfirmPutawayTaskCommand"));
            Assert.Equal(CentralAuditOutboxStatus.Pending, released.Status);
            Assert.False(released.ConfirmedByCommand);
            Assert.Equal(Guid.Parse(id), released.EntityId);
            Assert.Equal("Execute", released.Operation);
        }
        finally
        {
            stopped.TrySetResult();
        }

        await Expect(confirm, 503);                                       // the stopped command never confirms nor abandons the released row
        s.LedgerHealthy();
        var after = Assert.Single(Rows(s, "ConfirmPutawayTaskCommand"));
        Assert.Equal(CentralAuditOutboxStatus.Pending, after.Status);
        Assert.False(after.ConfirmedByCommand);

        // the ledger had committed the movement: the reconciler posts the confirmation → its own row, confirmed by its command
        await s.LateCommitAsync(id, 1);
        await s.ReconcileAsync();
        Assert.Equal("Posted", ConfirmationStatus(await s.GetTaskAsync(id), 1));
        var reconciled = Assert.Single(Rows(s, "ReconcilePendingConfirmationCommand"));
        Assert.Equal(CentralAuditOutboxStatus.Pending, reconciled.Status);
        Assert.True(reconciled.ConfirmedByCommand);
        Assert.Equal(Guid.Parse(id), reconciled.EntityId);
    }
}
