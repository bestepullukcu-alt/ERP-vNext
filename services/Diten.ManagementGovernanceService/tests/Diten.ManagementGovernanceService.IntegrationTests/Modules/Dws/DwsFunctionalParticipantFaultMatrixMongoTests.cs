using Diten.ManagementGovernanceService.Persistence.Modules.Dws;
using MongoDB.Bson;
using MongoDB.Driver;
using Xunit;

namespace Diten.ManagementGovernanceService.IntegrationTests.Modules.Dws;

[Collection(DwsMongoCollection.Name)]
public sealed class DwsFunctionalParticipantFaultMatrixMongoTests(DisposableDwsMongo mongo)
{
    [Fact]
    public async Task Exact_fifty_three_family_participant_boundaries_roll_back_then_succeed_exact_once()
    {
        var matrix = DwsPersistenceOwnershipManifest.Transactions
            .SelectMany(family => Enumerable.Range(
                1,
                family.BusinessCollections.Count + DwsTransactionFamily.TechnicalParticipants.Count)
                .Select(point => (family, point)))
            .ToArray();
        Assert.Equal(53, matrix.Length);

        await using var scope = await DwsFunctionalMongoScope.CreateAsync(mongo);
        foreach (var (family, point) in matrix)
        {
            var tenant = Guid.NewGuid();
            var mutation = Mutation(family, tenant, point);
            var writer = new DwsMongoAtomicWriter(scope.Context);
            await Assert.ThrowsAsync<InjectedFault>(() => writer.ExecuteAsync(mutation, new ThrowAt(point)));
            Assert.Equal(0, await scope.CountTenantAsync(tenant));

            await writer.ExecuteAsync(mutation);
            Assert.Equal(mutation.Participants.Count, await scope.CountTenantAsync(tenant));
        }
    }

    private static DwsMongoMutation Mutation(DwsTransactionFamily family, Guid tenant, int discriminator)
    {
        var participants = family.BusinessCollections.Select((alias, index) =>
            new DwsMongoParticipant(alias, Guid.NewGuid(), 0, Values(alias, discriminator, index))).ToList();
        var key = $"{family.Name}-{discriminator}-{Guid.NewGuid():N}";
        participants.Add(new("receipts", Guid.NewGuid(), 0, new BsonDocument
        {
            ["SecuritySubjectId"] = Standard(Guid.NewGuid()), ["CommandFamily"] = family.Name,
            ["IdempotencyKey"] = key, ["RequestPayloadHash"] = new string('a', 64),
            ["RequestCanonicalizationVersion"] = "dws.request.v1", ["OutcomeSchemaVersion"] = "dws.idempotency-outcome.v1",
            ["OutcomeKind"] = "succeeded", ["DomainCode"] = "test_succeeded", ["StableOutcomeJson"] = "{}",
            ["CreatedAtUtc"] = DateTime.UtcNow
        }));
        var auditId = Guid.NewGuid();
        participants.Add(new("audit-intents", Guid.NewGuid(), 0, new BsonDocument
        {
            ["AuditIntentId"] = Standard(auditId), ["EffectiveActorId"] = Standard(Guid.NewGuid()),
            ["DelegatedActorId"] = BsonNull.Value, ["EntityType"] = "dws-structure", ["EntityId"] = Guid.NewGuid().ToString("D"),
            ["Mutation"] = family.Name, ["OccurredAtUtc"] = DateTime.UtcNow
        }));
        participants.Add(new("outbox", Guid.NewGuid(), 0, new BsonDocument
        {
            ["EventId"] = Standard(Guid.NewGuid()), ["AuditIntentId"] = Standard(auditId),
            ["DeliveryState"] = "NON-DELIVERABLE-LOCAL-TEST", ["NextAttemptAtUtc"] = BsonNull.Value,
            ["Payload"] = "{}", ["CreatedAtUtc"] = DateTime.UtcNow
        }));
        return new(tenant, family.Name, key, new string('a', 64), participants);
    }

    private static BsonDocument Values(string alias, int discriminator, int index)
    {
        var unique = Guid.NewGuid();
        return alias switch
        {
            "definitions" => new()
            {
                ["ExternalContextReference"] = new BsonDocument
                {
                    ["ContractName"] = "ppm.external-context-reference", ["ContractVersion"] = "1.0",
                    ["ContextKind"] = "Project", ["ContextId"] = Standard(unique)
                },
                ["CurrentWorkingRevisionNumber"] = 1, ["LatestRevisionNumber"] = 1,
                ["CreatedAtUtc"] = DateTime.UtcNow, ["UpdatedAtUtc"] = BsonNull.Value, ["DeletedAtUtc"] = BsonNull.Value
            },
            "revisions" => new()
            {
                ["StructureDefinitionId"] = Standard(unique), ["RevisionNumber"] = discriminator * 10 + index + 1,
                ["StructuralMetadata"] = new BsonDocument { ["Name"] = unique.ToString("N"), ["Description"] = BsonNull.Value },
                ["IsSealed"] = false, ["SealedAtUtc"] = BsonNull.Value, ["CreatedAtUtc"] = DateTime.UtcNow,
                ["UpdatedAtUtc"] = BsonNull.Value, ["DeletedAtUtc"] = BsonNull.Value
            },
            "nodes" => new()
            {
                ["StructureRevisionId"] = Standard(unique), ["LogicalNodeId"] = Standard(Guid.NewGuid()),
                ["ParentLogicalNodeId"] = BsonNull.Value, ["Code"] = unique.ToString("N"), ["Title"] = "Node",
                ["Description"] = BsonNull.Value, ["SiblingOrder"] = discriminator, ["CreatedAtUtc"] = DateTime.UtcNow,
                ["UpdatedAtUtc"] = BsonNull.Value, ["DeletedAtUtc"] = BsonNull.Value
            },
            "dependencies" => new()
            {
                ["StructureRevisionId"] = Standard(unique), ["FromLogicalNodeId"] = Standard(Guid.NewGuid()),
                ["ToLogicalNodeId"] = Standard(Guid.NewGuid()), ["CreatedAtUtc"] = DateTime.UtcNow,
                ["UpdatedAtUtc"] = BsonNull.Value, ["DeletedAtUtc"] = BsonNull.Value
            },
            "baselines" => new()
            {
                ["StructureDefinitionId"] = Standard(unique), ["SourceRevisionNumber"] = 1,
                ["BaselineNumber"] = discriminator, ["HashAlgorithm"] = "SHA-256",
                ["CanonicalizationVersion"] = "dws.structural-baseline.v1", ["ContentHash"] = new string('b', 64),
                ["Snapshot"] = "{}", ["CreatedAtUtc"] = DateTime.UtcNow, ["DeletedAtUtc"] = BsonNull.Value
            },
            _ => throw new InvalidOperationException(alias)
        };
    }

    private static BsonBinaryData Standard(Guid value) => new(value, GuidRepresentation.Standard);
    private sealed class InjectedFault : Exception;
    private sealed class ThrowAt(int point) : IDwsMongoFaultProbe
    {
        public Task AfterParticipantAsync(int participantNumber, CancellationToken cancellationToken) =>
            participantNumber == point ? Task.FromException(new InjectedFault()) : Task.CompletedTask;
    }
}
