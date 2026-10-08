using Diten.PlanningService.Application.Features.DemandPlanning;
using Diten.PlanningService.Persistence.Features.DemandPlanning;
using MongoDB.Bson;
using MongoDB.Driver;
using Xunit;

namespace Diten.PlanningService.Cycles.Tests;

public sealed partial class PublishedRevisionMongoIntegrationTests
{
    [ManualDraftMongoFact]
    public async Task LegacyBsonManifest_WithoutPreparedAt_UsesVerifiedDraftCreationAudit()
    {
        var (draft, _, published, context) = await PublishedFixture(secondSeries: false);
        var before = (await published.ReadSnapshotAsync(draft.TenantId,
            draft.LegalEntityId, draft.Id, default))!.Value;
        var checksum = before.Manifest.Checksum;
        var stateVersion = before.Manifest.StateVersion;
        var auditCount = await context.ManualDraftAudit.CountDocumentsAsync(x =>
            x.TenantId == draft.TenantId && x.LegalEntityId == draft.LegalEntityId &&
            x.RevisionId == draft.Id);

        var uri = Environment.GetEnvironmentVariable("MOD0188_TEST_MONGO_URI")!;
        var raw = new MongoClient(uri).GetDatabase("mod0188_tests")
            .GetCollection<BsonDocument>("mod0188_published_revision_manifests");
        var filter = Builders<BsonDocument>.Filter.Eq("_id",
            new BsonBinaryData(before.Manifest.Id, GuidRepresentation.Standard));
        Assert.Equal(1, (await raw.UpdateOneAsync(filter,
            new BsonDocument("$unset", new BsonDocument("PreparedAt", "")))).ModifiedCount);
        Assert.False((await raw.Find(filter).SingleAsync()).Contains("PreparedAt"));

        var after = (await published.ReadSnapshotAsync(draft.TenantId,
            draft.LegalEntityId, draft.Id, default))!.Value;
        Assert.Equal(checksum, after.Manifest.Checksum);
        Assert.Equal(checksum, PublishedSnapshotIntegrity.Calculate(after.Manifest, after.Parts));
        Assert.Equal(stateVersion, after.Manifest.StateVersion);
        Assert.Equal(draft.CreatedAt, after.Manifest.PreparedAt);
        Assert.False((await raw.Find(filter).SingleAsync()).Contains("PreparedAt"));
        Assert.Equal(auditCount, await context.ManualDraftAudit.CountDocumentsAsync(x =>
            x.TenantId == draft.TenantId && x.LegalEntityId == draft.LegalEntityId &&
            x.RevisionId == draft.Id));

        var api = ReadController(draft, published, context, Guid.NewGuid());
        var status = ReadResponse<RevisionStatusView>(await api.Status(draft.Id,
            draft.LegalEntityId, default), 200).Data!;
        Assert.Equal(draft.CreatedAt, status.ActorTrace.PreparedAt);
        var manifest = ReadResponse<RevisionManifestView>(await api.Manifest(draft.Id,
            draft.LegalEntityId, default), 200).Data!;
        Assert.Equal(draft.CreatedAt, manifest.ActorTrace.PreparedAt);

        var creation = await context.ManualDraftAudit.Find(x =>
            x.TenantId == draft.TenantId && x.LegalEntityId == draft.LegalEntityId &&
            x.RevisionId == draft.Id && x.Action == "Created").SingleAsync();
        creation.OccurredAt = creation.OccurredAt.AddMinutes(1);
        await context.ManualDraftAudit.ReplaceOneAsync(x => x.Id == creation.Id &&
            x.TenantId == draft.TenantId && x.LegalEntityId == draft.LegalEntityId,
            creation);
        ReadResponse<RevisionStatusView>(await api.Status(draft.Id,
            draft.LegalEntityId, default), 503);
        ReadResponse<RevisionManifestView>(await api.Manifest(draft.Id,
            draft.LegalEntityId, default), 503);
    }
}
