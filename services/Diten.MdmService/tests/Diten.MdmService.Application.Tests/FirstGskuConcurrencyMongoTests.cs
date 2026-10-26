using Diten.MdmService.Application.Features.ProductItemSkuMaster;
using Diten.MdmService.Application.Features.ProductItemSkuMaster.Commands;
using Diten.MdmService.Application.Tests.Audit;
using Diten.MdmService.Domain.Entities;
using Diten.MdmService.Domain.Enums;
using MongoDB.Driver;
using Xunit;
using Xunit.Abstractions;

namespace Diten.MdmService.Application.Tests;

/// <summary>
/// Isolated home for the historical first-GSKU concurrency test. The class, not the legacy test class,
/// owns the test fixture lifetime and fixed-DB tenant cleanup contract.
/// </summary>
[Collection(ProductLegalEntityScopeMongoCollection.Name)]
public sealed class FirstGskuConcurrencyMongoTests(
    AuditIntentTemporalMongoFixture mongo,
    ITestOutputHelper output) : IClassFixture<AuditIntentTemporalMongoFixture>
{
    [Fact]
    public async Task Concurrent_first_gsku_commands_allocate_unique_parent_ordinals_and_soft_delete_never_reuses()
    {
        var scope = await ProductItemSkuMasterMongoTests.MongoTestScope.CreateReplicaAsync(mongo);
        try
        {
            output.WriteLine("GSKU_CONCURRENCY_INPUT tenant={0:D}; contenders=6; revisions=REV-001..REV-006; postDelete=REV-007",
                scope.TenantA);
            output.WriteLine("GSKU_CONCURRENCY_TOPOLOGY {0}", scope.TopologyEvidence);

            var parent = await ProductItemSkuMasterMongoTests.InsertParentAsync(scope, scope.TenantA);
            var reservations = new List<CodeReservation>();
            for (var index = 0; index < 6; index++)
            {
                reservations.Add(await scope.Reservations(scope.TenantA).ReserveAsync(
                    CodeBearingEntityType.Gsku, $"gsku-reserve-{index}", "actor", "corr"));
            }

            var tasks = reservations.Select((reservation, index) =>
                    ProductItemSkuMasterMongoTests.CreateFirstGskuHandler(
                            scope, scope.TenantA, new ProductItemSkuMasterMongoTests.VerifiedResolver())
                        .Handle(new CreateFirstGskuDraftCommand(new ProductItemSkuMasterModels.CreateFirstGskuDraftRequest
                        {
                            GlobalProductId = parent.Id,
                            GskuReservationId = reservation.Id,
                            ExpectedReservationVersion = reservation.Version,
                            CreationCommandId = $"concurrent-{index}",
                            PackQuantity = 1.250m,
                            PackUomCode = "KGM"
                        }), CancellationToken.None))
                .ToArray();
            var results = await Task.WhenAll(tasks);

            Assert.All(results, result => Assert.True(result.IsSuccessful, string.Join(',', result.Errors)));
            Assert.Equal(6, results.Select(x => x.Data!.RevisionIdentifier).Distinct().Count());
            Assert.Equal(Enumerable.Range(1, 6).Select(x => $"REV-{x:D3}"),
                results.Select(x => x.Data!.RevisionIdentifier).OrderBy(x => x));
            var firstId = results.Single(x => x.Data!.RevisionIdentifier == "REV-001").Data!.ProductDefinitionRevisionId;
            var revisionFilter = Builders<ProductDefinitionRevision>.Filter.Eq(x => x.TenantId, scope.TenantA)
                                 & Builders<ProductDefinitionRevision>.Filter.Eq(x => x.Id, firstId);
            await scope.Database.GetCollection<ProductDefinitionRevision>("mdm_product_definition_revisions").UpdateOneAsync(
                revisionFilter,
                Builders<ProductDefinitionRevision>.Update.Set(x => x.IsDeleted, true).Set(x => x.DeletedAt, DateTimeOffset.UtcNow));
            var nextReservation = await scope.Reservations(scope.TenantA).ReserveAsync(
                CodeBearingEntityType.Gsku, "gsku-reserve-next", "actor", "corr");
            var next = await ProductItemSkuMasterMongoTests.CreateFirstGskuHandler(
                    scope, scope.TenantA, new ProductItemSkuMasterMongoTests.VerifiedResolver())
                .Handle(new CreateFirstGskuDraftCommand(new ProductItemSkuMasterModels.CreateFirstGskuDraftRequest
                {
                    GlobalProductId = parent.Id,
                    GskuReservationId = nextReservation.Id,
                    ExpectedReservationVersion = nextReservation.Version,
                    CreationCommandId = "after-soft-delete",
                    PackQuantity = 1m,
                    PackUomCode = "C62"
                }), CancellationToken.None);
            Assert.Equal("REV-007", next.Data!.RevisionIdentifier);
        }
        finally
        {
            try
            {
                await scope.DisposeAsync();
            }
            finally
            {
                output.WriteLine("GSKU_CONCURRENCY_CLEANUP{0}{1}", Environment.NewLine, scope.CleanupEvidence);
            }
        }
    }

    /// <summary>
    /// TEST-HIZ-01 — the losing side of the allocator's upsert race, made deterministic. When no allocator document
    /// exists yet, concurrent first commands race the upsert; MongoDB answers the losers with a duplicate key on _id and
    /// does not retry an upsert whose filter is more than the unique key, so the loser's own allocation is NOT written.
    /// A failCommand fail point gives this command exactly that answer (11000, nothing written) on its first
    /// findAndModify: the command must still get its ordinal, not REVISION_ORDINAL_CONFLICT (seen on CI in
    /// Concurrent_first_gsku_commands_allocate_unique_parent_ordinals_and_soft_delete_never_reuses).
    /// </summary>
    [Fact]
    public async Task A_first_gsku_whose_allocator_upsert_loses_the_insert_race_still_gets_its_ordinal()
    {
        var scope = await ProductItemSkuMasterMongoTests.MongoTestScope.CreateReplicaAsync(mongo);
        var admin = scope.Database.Client.GetDatabase("admin");
        try
        {
            var parent = await ProductItemSkuMasterMongoTests.InsertParentAsync(scope, scope.TenantA);
            var reservation = await scope.Reservations(scope.TenantA).ReserveAsync(
                CodeBearingEntityType.Gsku, "gsku-race-loser", "actor", "corr");
            var handler = ProductItemSkuMasterMongoTests.CreateFirstGskuHandler(
                scope, scope.TenantA, new ProductItemSkuMasterMongoTests.VerifiedResolver());
            var command = new CreateFirstGskuDraftCommand(new ProductItemSkuMasterModels.CreateFirstGskuDraftRequest
            {
                GlobalProductId = parent.Id,
                GskuReservationId = reservation.Id,
                ExpectedReservationVersion = reservation.Version,
                CreationCommandId = "race-loser",
                PackQuantity = 1.250m,
                PackUomCode = "KGM"
            });

            await admin.RunCommandAsync<MongoDB.Bson.BsonDocument>(new MongoDB.Bson.BsonDocument
            {
                { "configureFailPoint", "failCommand" },
                { "mode", new MongoDB.Bson.BsonDocument("times", 1) },
                { "data", new MongoDB.Bson.BsonDocument
                    {
                        { "failCommands", new MongoDB.Bson.BsonArray { "findAndModify" } },
                        { "errorCode", 11000 },
                        // the shape a real duplicate key carries (the server refuses a DuplicateKey without it)
                        { "errorExtraInfo", new MongoDB.Bson.BsonDocument
                            {
                                { "keyPattern", new MongoDB.Bson.BsonDocument("_id", 1) },
                                { "keyValue", new MongoDB.Bson.BsonDocument("_id", $"{scope.TenantA:N}:{parent.Id:N}") }
                            }
                        }
                    }
                }
            });
            try
            {
                var result = await handler.Handle(command, CancellationToken.None);

                Assert.True(result.IsSuccessful, string.Join(',', result.Errors));
                Assert.Equal("REV-001", result.Data!.RevisionIdentifier);
            }
            finally
            {
                await admin.RunCommandAsync<MongoDB.Bson.BsonDocument>(new MongoDB.Bson.BsonDocument
                {
                    { "configureFailPoint", "failCommand" }, { "mode", "off" }
                });
            }
        }
        finally
        {
            await scope.DisposeAsync();
        }
    }

}
