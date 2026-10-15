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

}
