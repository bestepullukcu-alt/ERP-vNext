using Diten.ManagementGovernanceService.Application.Features.Dws;
using Diten.ManagementGovernanceService.Domain.Modules.Dws;
using Diten.ManagementGovernanceService.Persistence.Modules.Dws;
using MongoDB.Driver;
using Xunit;

namespace Diten.ManagementGovernanceService.IntegrationTests.Modules.Dws;

[Collection(DwsMongoCollection.Name)]
public sealed class DwsFunctionalTransactionFailureMongoTests(DisposableDwsMongo mongo)
{
    [Fact]
    public async Task Unknown_commit_is_commit_only_and_produces_exact_one_functional_state()
    {
        await using var scope = await DwsFunctionalMongoScope.CreateAsync(mongo);
        var committer = new UnknownThenCommit(2);
        var queryStore = new DwsFunctionalQueryStore(scope.Context);
        var port = new DwsFunctionalCommandPort(queryStore, new DwsMongoAtomicWriter(scope.Context, committer), TimeProvider.System);
        var tenant = Guid.NewGuid();

        await port.CreateStructureAsync(
            new(DwsFunctionalMongoScope.Reference(), "Unknown", null),
            scope.CommandActor(tenant, "unknown"),
            default);

        Assert.Equal(3, committer.Attempts);
        var counts = await scope.CountsAsync(tenant);
        Assert.Equal(1, counts["definitions"]);
        Assert.Equal(1, counts["revisions"]);
        Assert.Equal(1, counts["receipts"]);
        Assert.Equal(1, counts["audit-intents"]);
        Assert.Equal(1, counts["outbox"]);
    }

    [Fact]
    public async Task Cancellation_propagates_and_leaves_zero_functional_residue()
    {
        await using var scope = await DwsFunctionalMongoScope.CreateAsync(mongo);
        var tenant = Guid.NewGuid();
        using var cancellation = new CancellationTokenSource();
        cancellation.Cancel();

        await Assert.ThrowsAnyAsync<OperationCanceledException>(() => scope.Commands.CreateStructureAsync(
            new(DwsFunctionalMongoScope.Reference(), "Cancelled", null),
            scope.CommandActor(tenant, "cancelled"),
            cancellation.Token));
        Assert.Equal(0, await scope.CountTenantAsync(tenant));
    }

    [Fact]
    public async Task Standalone_Mongo_rejects_functional_command_with_zero_residue()
    {
        await using var standalone = await DisposableDwsMongo.StartStandaloneAsync();
        Assert.True(standalone.Port >= 27022);
        Assert.DoesNotContain(standalone.Port, new[] { 27017, 27018, 27019 });
        var database = "mod0354_functional_standalone_" + Guid.NewGuid().ToString("N");
        var context = new DwsMongoContext(standalone.Client, database);
        await new DwsMongoIndexInitializer(context).InitializeAsync();
        var queryStore = new DwsFunctionalQueryStore(context);
        var port = new DwsFunctionalCommandPort(queryStore, new DwsMongoAtomicWriter(context), TimeProvider.System);
        var tenant = Guid.NewGuid();
        try
        {
            var error = await Assert.ThrowsAsync<DwsValidationException>(() => port.CreateStructureAsync(
                new(DwsFunctionalMongoScope.Reference(), "Standalone", null),
                new(tenant, Guid.NewGuid(), Guid.NewGuid(), Guid.NewGuid(), "standalone"),
                default));
            Assert.Equal(DwsErrors.TransactionUnavailable, error.Code);
            var filter = Builders<MongoDB.Bson.BsonDocument>.Filter.Eq(
                "TenantId", new MongoDB.Bson.BsonBinaryData(tenant, MongoDB.Bson.GuidRepresentation.Standard));
            long residue = 0;
            foreach (var alias in DwsMongoContext.CollectionAliases.Keys)
                residue += await context.Collection(alias).CountDocumentsAsync(filter);
            Assert.Equal(0, residue);
        }
        finally
        {
            await standalone.Client.DropDatabaseAsync(database);
        }
    }

    private sealed class UnknownThenCommit(int unknownAttempts) : IDwsMongoCommitter
    {
        private readonly DwsMongoCommitter _inner = new();
        public int Attempts { get; private set; }
        public Task CommitAsync(IClientSessionHandle session, CancellationToken cancellationToken)
        {
            Attempts++;
            return Attempts <= unknownAttempts
                ? Task.FromException(new DwsUnknownCommitResultException())
                : _inner.CommitAsync(session, cancellationToken);
        }
    }
}
