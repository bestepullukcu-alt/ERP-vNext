using Diten.MdmService.Application.Common;
using System.Text.Json;
using Diten.MdmService.Application.Features.ProductItemSkuMaster;
using Diten.MdmService.Application.Features.ProductItemSkuMaster.Commands;
using Diten.MdmService.Application.Features.ProductItemSkuMaster.Validators;
using Diten.MdmService.Domain.Entities;
using Diten.MdmService.Domain.Enums;
using Diten.MdmService.Domain.ValueObjects;
using Diten.MdmService.Persistence.Repositories;
using MongoDB.Driver;
using Xunit;

namespace Diten.MdmService.Application.Tests;

[Collection(ProductLegalEntityScopeMongoCollection.Name)]
public sealed class GskuDraftEditLifecycleTests : IAsyncLifetime
{
    private readonly Guid _tenantId = Guid.NewGuid();
    private IMongoDatabase _database = null!;
    private IMongoCollection<Gsku> _gskus = null!;

    public async Task InitializeAsync()
    {
        var client = new MongoClient(Environment.GetEnvironmentVariable("MDM_TEST_MONGO")
            ?? Environment.GetEnvironmentVariable("MONGO_TEST_URI")
            ?? "mongodb://localhost:27017");
        _database = client.GetDatabase(ProductLegalEntityScopeMongoCollection.DatabaseName);
        await _database.RunCommandAsync<MongoDB.Bson.BsonDocument>(
            new MongoDB.Bson.BsonDocument("ping", 1));
        _gskus = _database.GetCollection<Gsku>("mdm_gskus");
    }

    [Fact]
    public void ExactDraftEditContract_AcceptsOnlyServerVerifiedMutableFields()
    {
        var command = new UpdateGskuDraftCommand(new ProductItemSkuMasterModels.UpdateGskuDraftRequest
        {
            GskuId = Guid.NewGuid(),
            ExpectedVersion = 0,
            PackQuantity = 24m,
            PackUomCode = "C62"
        }, Guid.NewGuid());

        var result = new UpdateGskuDraftValidator().Validate(command);

        Assert.True(result.IsValid);
        Assert.NotEqual(Guid.Empty, command.OperationId);
    }

    [Fact]
    public void ClientSuppliedCatalogEvidence_IsRejectedFailClosed()
    {
        var command = new UpdateGskuDraftCommand(new ProductItemSkuMasterModels.UpdateGskuDraftRequest
        {
            GskuId = Guid.NewGuid(),
            ExpectedVersion = 0,
            PackQuantity = 24m,
            PackUomCode = "C62",
            UnmappedFields = new Dictionary<string, JsonElement>
            {
                ["packUomSelection"] = JsonDocument.Parse("{}").RootElement.Clone()
            }
        }, Guid.NewGuid());

        var result = new UpdateGskuDraftValidator().Validate(command);

        Assert.False(result.IsValid);
        Assert.Contains(result.Errors, error =>
            error.ErrorMessage == "REFERENCE_CATALOG_EVIDENCE_CLIENT_OVERRIDE_FORBIDDEN");
    }

    [Fact]
    public async Task Repository_ExactExternalOperationReplay_IsStable_AndDriftFailsClosed()
    {
        var operationId = Guid.NewGuid();
        var now = DateTimeOffset.UtcNow;
        var gsku = new Gsku
        {
            Id = Guid.NewGuid(), TenantId = _tenantId,
            ProductDefinitionRevisionId = Guid.NewGuid(), CanonicalCode = "GS-EDIT-REPLAY",
            CreationCommandId = "CREATE-EDIT-REPLAY", PackApplicabilityCode = "SCALAR_QUANTITY_APPLIES",
            PackQuantity = 1, PackUomCode = "C62", Version = 0,
            LifecycleStatus = ProductIdentityLifecycleStatus.Draft,
            PackApplicabilitySelection = Selection("pack-applicability", "SCALAR_QUANTITY_APPLIES"),
            PackUomSelection = Selection("uom", "C62"), CreatedAt = now, UpdatedAt = now
        };
        await _gskus.InsertOneAsync(gsku);
        var intent = new LocalAuditIntent
        {
            IntentId = Guid.NewGuid(), TenantId = _tenantId,
            AggregateType = AuditAggregateType.Gsku, AggregateId = gsku.Id,
            PreVersion = 0, PostVersion = 1, Operation = ProductAuditOperation.GskuDraftUpdated,
            ActorId = "maker", CorrelationId = operationId.ToString("D"),
            CausationId = gsku.CreationCommandId, CommandId = operationId.ToString("D"),
            Sequence = 2, TimestampUtc = now, TimestampUtcTicksV1 = now.UtcTicks,
            EvidenceHash = "EXACT-EDIT-EVIDENCE", IdempotencyKey = operationId.ToString("D")
        };
        gsku.PackQuantity = 24;
        gsku.AuditIntents.Add(intent);
        var repository = new GskuRepository(_database, new Tenant(_tenantId));

        var first = await repository.UpdateDraftAsync(gsku, 0);
        var replay = await repository.UpdateDraftAsync(gsku, 0);
        gsku.PackQuantity = 25;
        var drift = await repository.UpdateDraftAsync(gsku, 0);

        Assert.True(first.Succeeded);
        Assert.True(replay.Succeeded);
        Assert.Equal(first.Gsku!.Id, replay.Gsku!.Id);
        Assert.False(drift.Succeeded);
        Assert.Equal("CONCURRENCY_CONFLICT", drift.ErrorCode);
        Assert.Equal(1, await _gskus.CountDocumentsAsync(x =>
            x.TenantId == _tenantId && x.Id == gsku.Id));
        Assert.Single(replay.Gsku.AuditIntents,
            x => x.IdempotencyKey == operationId.ToString("D"));
    }

    public async Task DisposeAsync() =>
        await _gskus.DeleteManyAsync(x => x.TenantId == _tenantId);

    private static ReferenceCatalogSelection Selection(string setCode, string valueCode) => new()
    {
        SetCode = setCode, ValueCode = valueCode, CatalogVersionId = Guid.NewGuid(),
        CatalogVersionNumber = 1, ResolutionMode = ReferenceCatalogResolutionMode.Latest,
        ResolvedAtUtc = DateTimeOffset.UtcNow
    };

    private sealed class Tenant(Guid tenantId) : ITenantContext
    {
        public Guid TenantId { get; private set; } = tenantId;
        public bool IsResolved => TenantId != Guid.Empty;
        public void SetTenant(Guid tenantId) => TenantId = tenantId;
    }
}
