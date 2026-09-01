using Diten.ManagementGovernanceService.Application.Modules.ProcessModeling;
using Diten.ManagementGovernanceService.Persistence.Modules.ProcessModeling;

namespace Diten.ManagementGovernanceService.IntegrationTests.Modules.ProcessModeling;

public sealed class AtomicPersistenceTests
{
    [Fact] public async Task Success_persists_exact_four_participants_and_replays()
    {
        var store = new TestOnlyInMemoryAtomicMutationStore(); var request = Request(); var body = 0;
        var first = await store.ExecuteAsync(request, _ => Task.FromResult("created:" + ++body), default);
        var replay = await store.ExecuteAsync(request, _ => Task.FromResult("wrong:" + ++body), default);
        Assert.True(first.Accepted); Assert.True(replay.Accepted); Assert.Equal(1, body); Assert.Equal((1,1,1,1), store.Counts);
    }

    [Theory] [InlineData(1)] [InlineData(2)] [InlineData(3)] [InlineData(4)]
    public async Task Every_participant_fault_rolls_back_all_four(int participant)
    {
        var store = new TestOnlyInMemoryAtomicMutationStore { FailAfterParticipant = participant };
        await Assert.ThrowsAsync<InvalidOperationException>(() => store.ExecuteAsync(Request(), _ => Task.FromResult("created"), default));
        Assert.Equal((0,0,0,0), store.Counts);
    }

    [Fact] public async Task Changed_payload_or_subject_is_conflict()
    {
        var store = new TestOnlyInMemoryAtomicMutationStore(); var request = Request();
        await store.ExecuteAsync(request, _ => Task.FromResult("created"), default);
        var changed = await store.ExecuteAsync(request with { CanonicalPayloadHash = "sha256:" + new string('b',64) }, _ => Task.FromResult("wrong"), default);
        Assert.Equal(409, changed.HttpStatus); Assert.Equal((1,1,1,1), store.Counts);
    }

    [Fact] public async Task Cancellation_has_zero_residue()
    {
        var store = new TestOnlyInMemoryAtomicMutationStore(); using var cts = new CancellationTokenSource(); cts.Cancel();
        await Assert.ThrowsAsync<OperationCanceledException>(() => store.ExecuteAsync(Request(), _ => Task.FromResult("created"), cts.Token));
        Assert.Equal((0,0,0,0), store.Counts);
    }

    [Fact] public async Task Publish_is_terminal_fail_closed_before_business_or_technical_participants()
    {
        var store=new TestOnlyInMemoryAtomicMutationStore();var body=0;var result=await store.ExecuteAsync(Request() with{CommandFamily=PublishProcessModelVersionContract.CommandName},_=>Task.FromResult("mutated:"+(++body)),default);
        Assert.Equal(503,result.HttpStatus);Assert.Equal(0,body);Assert.Equal((0,0,0,0),store.Counts);
    }

    [Fact] public void Persistence_manifest_is_exact_tenant_first_and_non_ttl()
    {
        Assert.Equal(12, ProcessModelingPersistenceManifest.Collections.Count); Assert.Equal(16, ProcessModelingPersistenceManifest.Indexes.Count);
        Assert.Equal(12, ProcessModelingPersistenceManifest.Collections.Distinct(StringComparer.Ordinal).Count());
        Assert.All(ProcessModelingPersistenceManifest.Indexes, x => { Assert.Equal("TenantId", x.Keys[0]); Assert.False(x.Ttl); });
        var open = Assert.Single(ProcessModelingPersistenceManifest.Indexes, x => x.Name == "ux_pm_open_version");
        Assert.Equal(new[] { "TenantId", "ProcessModelId" }, open.Keys); Assert.Contains("Draft", open.PartialFilterJson); Assert.Contains("Review", open.PartialFilterJson);
    }

    private static CoreMutationRequest Request() => new(Guid.NewGuid(), Guid.NewGuid(), "CreateProcessModelCommand", "idempotency-1", "sha256:" + new string('a',64), 0, Guid.NewGuid());
}
