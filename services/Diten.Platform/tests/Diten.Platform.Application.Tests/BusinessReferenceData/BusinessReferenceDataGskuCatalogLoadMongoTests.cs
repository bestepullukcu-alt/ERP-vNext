using Diten.Platform.Application.Features.BusinessReferenceData.Services;
using Diten.Platform.Common.Tenancy;
using Diten.Platform.Domain.Entities;
using Diten.Platform.Infrastructure.Persistence;
using Diten.Platform.Infrastructure.Persistence.Configurations;
using Diten.Platform.Infrastructure.Persistence.Repositories.BusinessReferenceData;
using Diten.Platform.Infrastructure.Persistence.Settings;
using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.Extensions.Options;
using MongoDB.Driver;
using System.Security.Cryptography;
using Xunit;

namespace Diten.Platform.Application.Tests.BusinessReferenceData;

public sealed class BusinessReferenceDataGskuCatalogLoadMongoTests : IAsyncLifetime
{
    private BusinessReferenceDataTestHarness _harness = null!;

    public async Task InitializeAsync()
    {
        _harness = await BusinessReferenceDataTestHarness.CreateAsync();
    }

    public Task DisposeAsync() => _harness.DisposeAsync().AsTask();

    [Fact]
    public async Task ExactArtifact_LoadsTwoSetsSixValuesAndReplaysWithoutDuplicates()
    {
        var loader = _harness.CreateLoader();
        var artifact = BusinessReferenceDataTestHarness.GetArtifactPath();

        var first = await loader.LoadVerifiedGskuCatalogFromFileAsync(
            artifact,
            "test-publisher",
            ["pack-applicability", "uom"]);
        var replay = await loader.LoadVerifiedGskuCatalogFromFileAsync(
            artifact,
            "test-publisher",
            ["pack-applicability", "uom"]);

        Assert.Empty(first.BlockedConflicts);
        Assert.Equal(2, first.SetsLoaded);
        Assert.Equal(6, first.ValuesInserted);
        Assert.Equal(2, replay.SetsAlreadyLoaded);
        Assert.Equal(first.CatalogFingerprint, replay.CatalogFingerprint);

        var sets = await _harness.Database
            .GetCollection<BusinessReferenceDataSet>("business_reference_data_sets")
            .Find(x => x.TenantId == _harness.ReferenceTenantId && !x.IsDeleted)
            .ToListAsync();
        var versions = await _harness.Database
            .GetCollection<BusinessReferenceDataVersion>("business_reference_data_versions")
            .Find(x => x.TenantId == _harness.ReferenceTenantId && !x.IsDeleted)
            .ToListAsync();
        var operations = await _harness.Database
            .GetCollection<BusinessReferenceDataPublishOperation>("business_reference_data_publish_operations")
            .Find(x => x.TenantId == _harness.ReferenceTenantId && !x.IsDeleted)
            .ToListAsync();

        Assert.Equal(2, sets.Count);
        Assert.Equal(2, versions.Count);
        Assert.Equal(2, operations.Count);
        Assert.All(sets, set => Assert.NotNull(set.PublishedVersionId));
        Assert.All(versions, version =>
        {
            Assert.Equal(BusinessReferenceDataVersionStatus.Published, version.Status);
            Assert.True(version.IsImmutable);
        });
        Assert.All(operations, operation =>
        {
            Assert.Equal(BusinessReferenceDataPublishOperationState.COMPLETED, operation.OperationState);
            Assert.Equal(BusinessReferenceDataPublishCheckpoint.COMPLETION_VERIFIED, operation.PublishCheckpoint);
            Assert.Equal("1.0.0", operation.CatalogVersion);
            Assert.Equal(first.CatalogFingerprint, operation.CatalogFingerprint);
        });

        var uomSet = Assert.Single(sets, x => x.SetCode == "uom");
        var uom = Assert.Single(versions, x => x.BusinessReferenceDataSetId == uomSet.BusinessReferenceDataSetId);
        Assert.Equal(2, uom.AttributeDefinitions.Count);
        Assert.Equal("COUNT", Assert.Single(uom.Values, x => x.ValueCode == "C62").Attributes!["DimensionCode"]);
        Assert.Equal("0", Assert.Single(uom.Values, x => x.ValueCode == "C62").Attributes!["MaximumDecimalPrecision"]);
        Assert.All(uom.Values.Where(x => x.ValueCode != "C62"), value =>
            Assert.Equal("3", value.Attributes!["MaximumDecimalPrecision"]));
    }

    [Fact]
    public async Task VerifiedPath_MissingProviderOptionFailsClosedBeforeAnyWrite()
    {
        await using var withoutProvider = await BusinessReferenceDataTestHarness.CreateAsync(configureProvider: false);
        var exception = await Assert.ThrowsAsync<InvalidOperationException>(() => withoutProvider.CreateLoader()
            .LoadVerifiedGskuCatalogFromFileAsync(
                BusinessReferenceDataTestHarness.GetArtifactPath(),
                "test-publisher",
                ["pack-applicability", "uom"]));

        Assert.Equal("REFERENCE_PROVIDER_CONFIGURATION_INVALID", exception.Message);
        Assert.Equal(0, await withoutProvider.Database.GetCollection<BusinessReferenceDataSet>("business_reference_data_sets")
            .CountDocumentsAsync(FilterDefinition<BusinessReferenceDataSet>.Empty));
    }

    [Fact]
    public async Task AlteredLockedContent_IsRejectedWithoutPublicationOrOverwrite()
    {
        var source = await File.ReadAllTextAsync(BusinessReferenceDataTestHarness.GetArtifactPath());
        var altered = source.Replace("\"MaximumDecimalPrecision\": \"3\"", "\"MaximumDecimalPrecision\": \"4\"", StringComparison.Ordinal);
        var path = Path.Combine(Path.GetTempPath(), $"brd-gsku-{Guid.NewGuid():N}.json");
        await File.WriteAllTextAsync(path, altered);
        try
        {
            var exception = await Assert.ThrowsAsync<InvalidOperationException>(() => _harness.CreateLoader().LoadVerifiedGskuCatalogFromFileAsync(
                path,
                "test-publisher",
                ["pack-applicability", "uom"]));

            Assert.Equal("gsku_uom_value_contract_mismatch", exception.Message);
            Assert.Equal(0, await _harness.Database.GetCollection<BusinessReferenceDataPublishOperation>("business_reference_data_publish_operations")
                .CountDocumentsAsync(FilterDefinition<BusinessReferenceDataPublishOperation>.Empty));
        }
        finally
        {
            File.Delete(path);
        }
    }

    [Fact]
    public async Task SameCatalogVersionWithDifferentByteFingerprint_ConflictsWithoutRepublish()
    {
        var loader = _harness.CreateLoader();
        var artifact = BusinessReferenceDataTestHarness.GetArtifactPath();
        await loader.LoadVerifiedGskuCatalogFromFileAsync(
            artifact,
            "test-publisher",
            ["pack-applicability", "uom"]);
        var source = await File.ReadAllTextAsync(artifact);
        var path = Path.Combine(Path.GetTempPath(), $"brd-gsku-{Guid.NewGuid():N}.json");
        await File.WriteAllTextAsync(path, "{ " + source[1..]);
        try
        {
            var conflict = await loader.LoadVerifiedGskuCatalogFromFileAsync(
                path,
                "test-publisher",
                ["pack-applicability", "uom"]);

            Assert.Equal(2, conflict.BlockedConflicts.Count);
            Assert.All(conflict.BlockedConflicts, message => Assert.Contains("catalog fingerprint conflict", message));
            Assert.Equal(2, await _harness.Database
                .GetCollection<BusinessReferenceDataPublishOperation>("business_reference_data_publish_operations")
                .CountDocumentsAsync(x => !x.IsDeleted));
        }
        finally
        {
            File.Delete(path);
        }
    }

    [Fact]
    public async Task MissingUnknownDuplicateTypeInvalidAndOutOfContractMetadata_AreBlocking()
    {
        var source = await File.ReadAllTextAsync(BusinessReferenceDataTestHarness.GetArtifactPath());
        var variants = new[]
        {
            source.Replace("\"DimensionCode\": \"COUNT\",", string.Empty, StringComparison.Ordinal),
            source.Replace("\"DimensionCode\": \"COUNT\"", "\"Unknown\": \"COUNT\"", StringComparison.Ordinal),
            source.Replace("\"DimensionCode\": \"COUNT\"", "\"DimensionCode\": \"COUNT\", \"DimensionCode\": \"COUNT\"", StringComparison.Ordinal),
            source.Replace("\"MaximumDecimalPrecision\": \"0\"", "\"MaximumDecimalPrecision\": 0", StringComparison.Ordinal),
            source.Replace("\"MaximumDecimalPrecision\": \"0\"", "\"MaximumDecimalPrecision\": \"00\"", StringComparison.Ordinal)
        };

        foreach (var variant in variants)
        {
            var path = Path.Combine(Path.GetTempPath(), $"brd-gsku-{Guid.NewGuid():N}.json");
            await File.WriteAllTextAsync(path, variant);
            try
            {
                await Assert.ThrowsAnyAsync<Exception>(() => _harness.CreateLoader().LoadVerifiedGskuCatalogFromFileAsync(
                    path,
                    "test-publisher",
                    ["pack-applicability", "uom"]));
            }
            finally
            {
                File.Delete(path);
            }
        }

        Assert.Equal(0, await _harness.Database.GetCollection<BusinessReferenceDataPublishOperation>("business_reference_data_publish_operations")
            .CountDocumentsAsync(FilterDefinition<BusinessReferenceDataPublishOperation>.Empty));
    }

    [Fact]
    public async Task GenericLegacyLoader_LoadsQmsAndLegalEntityWithoutProviderOrEligibilityAndCreatesNoVerifiedClaim()
    {
        await using var legacy = await BusinessReferenceDataTestHarness.CreateAsync(configureProvider: false);
        var loader = legacy.CreateLoader(eligibility: new RuntimeBusinessReferenceDataPublicationEligibility());

        var qms = await loader.LoadFromFileAsync(
            BusinessReferenceDataTestHarness.GetSeedPath("document-management-qms.json"),
            legacy.ReferenceTenantId,
            "legacy-seed",
            ["qms-document-class", "qms-document-classification", "qms-document-retention"]);
        var legal = await loader.LoadFromFileAsync(
            BusinessReferenceDataTestHarness.GetSeedPath("legal-entity-reference.json"),
            legacy.ReferenceTenantId,
            "legacy-seed",
            ["legal-form", "country", "base-currency"]);

        Assert.Empty(qms.BlockedConflicts);
        Assert.Empty(legal.BlockedConflicts);
        Assert.Equal(3, qms.SetsLoaded);
        Assert.Equal(3, legal.SetsLoaded);
        Assert.Equal(6, await legacy.Database.GetCollection<BusinessReferenceDataSet>("business_reference_data_sets")
            .CountDocumentsAsync(x => x.TenantId == legacy.ReferenceTenantId && x.PublishedVersionId != null));
        Assert.Equal(0, await legacy.Database.GetCollection<BusinessReferenceDataPublishOperation>("business_reference_data_publish_operations")
            .CountDocumentsAsync(FilterDefinition<BusinessReferenceDataPublishOperation>.Empty));
    }

    [Fact]
    public async Task LegacyAndVerifiedPaths_KeepTenantAndOperationEvidenceSeparate()
    {
        var legacyTenantId = Guid.NewGuid();
        var loader = _harness.CreateLoader(eligibility: new EligiblePublicationForTests());
        await loader.LoadFromFileAsync(
            BusinessReferenceDataTestHarness.GetSeedPath("document-management-qms.json"),
            legacyTenantId,
            "legacy-seed",
            ["qms-document-class"]);
        await loader.LoadVerifiedGskuCatalogFromFileAsync(
            BusinessReferenceDataTestHarness.GetArtifactPath(),
            "verified-gsku",
            ["pack-applicability", "uom"]);

        var sets = _harness.Database.GetCollection<BusinessReferenceDataSet>("business_reference_data_sets");
        var operations = _harness.Database.GetCollection<BusinessReferenceDataPublishOperation>("business_reference_data_publish_operations");
        Assert.Equal(3, await sets.CountDocumentsAsync(x => x.TenantId == legacyTenantId));
        Assert.Equal(2, await sets.CountDocumentsAsync(x => x.TenantId == _harness.ReferenceTenantId));
        Assert.Equal(0, await operations.CountDocumentsAsync(x => x.TenantId == legacyTenantId));
        Assert.Equal(2, await operations.CountDocumentsAsync(x => x.TenantId == _harness.ReferenceTenantId
                                                                    && x.OperationState == BusinessReferenceDataPublishOperationState.COMPLETED));
    }

    [Fact]
    public async Task OperationalPublisher_LowercaseIdempotencyKey_RecoversExistingPendingOperation()
    {
        var artifact = BusinessReferenceDataTestHarness.GetArtifactPath();
        var fingerprint = Convert.ToHexString(SHA256.HashData(await File.ReadAllBytesAsync(artifact))).ToLowerInvariant();
        var facts = new VerifiedGskuOperationalFacts(
            Path.GetFullPath(artifact),
            "1.0.0",
            fingerprint,
            _harness.ReferenceTenantId,
            Guid.NewGuid(),
            "operational-recovery-test",
            "operational-recovery",
            ["pack-applicability", "uom"]);
        var interruptedEligibility = new GskuOperationalEligibilityForTests(facts, denyAuthorizationCheck: 2);

        var exception = await Assert.ThrowsAsync<InvalidOperationException>(() =>
            _harness.CreateLoader(operationalEligibility: interruptedEligibility)
                .LoadVerifiedGskuCatalogFromFileAsync(
                    artifact,
                    facts.ActorId,
                    facts.IdempotencyNamespace,
                    facts.RequiredSetCodes,
                    interruptedEligibility.Authorization,
                    facts));

        Assert.Equal("REFERENCE_GOVERNANCE_NOT_PRODUCTION_SAFE", exception.Message);
        var pending = await _harness.Database
            .GetCollection<BusinessReferenceDataPublishOperation>("business_reference_data_publish_operations")
            .Find(x => x.TenantId == _harness.ReferenceTenantId && !x.IsDeleted)
            .SingleAsync();
        Assert.StartsWith("businessreferencedata-catalog-v", pending.IdempotencyKey, StringComparison.Ordinal);
        Assert.Equal(BusinessReferenceDataPublishOperationState.PENDING, pending.OperationState);
        Assert.Equal(BusinessReferenceDataPublishCheckpoint.INITIALIZED, pending.PublishCheckpoint);

        var recoveryEligibility = new GskuOperationalEligibilityForTests(facts);
        var replay = await _harness.CreateLoader(operationalEligibility: recoveryEligibility)
            .LoadVerifiedGskuCatalogFromFileAsync(
                artifact,
                facts.ActorId,
                facts.IdempotencyNamespace,
                facts.RequiredSetCodes,
                recoveryEligibility.Authorization,
                facts);
        var recovered = await _harness.Repository.GetPublishOperationByIdAsync(pending.PublishOperationId);
        var operations = await _harness.Database
            .GetCollection<BusinessReferenceDataPublishOperation>("business_reference_data_publish_operations")
            .Find(x => x.TenantId == _harness.ReferenceTenantId && !x.IsDeleted)
            .ToListAsync();

        Assert.Empty(replay.BlockedConflicts);
        Assert.NotNull(recovered);
        Assert.Equal(pending.PublishOperationId, recovered.PublishOperationId);
        Assert.Equal(BusinessReferenceDataPublishOperationState.COMPLETED, recovered.OperationState);
        Assert.Equal(BusinessReferenceDataPublishCheckpoint.COMPLETION_VERIFIED, recovered.PublishCheckpoint);
        Assert.Equal(2, operations.Count);
        Assert.All(operations, operation =>
        {
            Assert.Equal(BusinessReferenceDataPublishOperationState.COMPLETED, operation.OperationState);
            Assert.Equal(BusinessReferenceDataPublishCheckpoint.COMPLETION_VERIFIED, operation.PublishCheckpoint);
        });
    }

    [Fact]
    public async Task GenericLoader_CannotTreatGskuArtifactAsVerifiedPublication()
    {
        var exception = await Assert.ThrowsAsync<InvalidOperationException>(() => _harness.CreateLoader().LoadFromFileAsync(
            BusinessReferenceDataTestHarness.GetArtifactPath(),
            Guid.NewGuid(),
            "legacy-seed",
            ["pack-applicability", "uom"]));

        Assert.Equal("VERIFIED_GSKU_CATALOG_CONTRACT_REQUIRED", exception.Message);
        Assert.Equal(0, await _harness.Database.GetCollection<BusinessReferenceDataPublishOperation>("business_reference_data_publish_operations")
            .CountDocumentsAsync(FilterDefinition<BusinessReferenceDataPublishOperation>.Empty));
    }
}

internal sealed class BusinessReferenceDataTestHarness : IAsyncDisposable
{
    private readonly MongoClient _client;
    private readonly string _databaseName;

    private BusinessReferenceDataTestHarness(
        MongoClient client,
        string databaseName,
        IMongoDatabase database,
        bool configureProvider)
    {
        _client = client;
        _databaseName = databaseName;
        Database = database;
        ReferenceTenantId = Guid.NewGuid();
        TenantContext = new TenantContext();
        TenantContext.SetTenant(ReferenceTenantId);
        Repository = configureProvider
            ? new BusinessReferenceDataStewardshipRepository(
                new PlatformDbContext(client, database),
                TenantContext,
                Options.Create(new BusinessReferenceDataProviderOptions { ReferenceTenantId = ReferenceTenantId }))
            : new BusinessReferenceDataStewardshipRepository(
                new PlatformDbContext(client, database),
                TenantContext);
    }

    public Guid ReferenceTenantId { get; }
    public IMongoDatabase Database { get; }
    public TenantContext TenantContext { get; }
    public BusinessReferenceDataStewardshipRepository Repository { get; }

    public static async Task<BusinessReferenceDataTestHarness> CreateAsync(bool configureProvider = true)
    {
        var settings = MongoClientSettings.FromConnectionString("mongodb://127.0.0.1:27017");
        settings.ServerSelectionTimeout = TimeSpan.FromSeconds(5);
        var client = new MongoClient(settings);
        var databaseName = $"diten_brd_gsku_{Guid.NewGuid():N}";
        var database = client.GetDatabase(databaseName);
        await database.RunCommandAsync<object>("{ ping: 1 }");
        await MongoDbIndexConfigurations.EnsureIndexesAsync(database);
        return new BusinessReferenceDataTestHarness(client, databaseName, database, configureProvider);
    }

    public BusinessReferenceDataCatalogLoaderService CreateLoader(
        IBusinessReferenceDataPublishCheckpointObserver? observer = null,
        IBusinessReferenceDataPublicationEligibility? eligibility = null,
        IBusinessReferenceDataVerifiedMarketOperationalEligibility? marketEligibility = null,
        IBusinessReferenceDataVerifiedGskuOperationalEligibility? operationalEligibility = null)
    {
        return new BusinessReferenceDataCatalogLoaderService(
            Repository,
            CreatePublishService(observer, eligibility, marketEligibility, operationalEligibility),
            TenantContext,
            operationalEligibility,
            marketOperationalEligibility: marketEligibility);
    }

    public BusinessReferenceDataPublishService CreatePublishService(
        IBusinessReferenceDataPublishCheckpointObserver? observer = null,
        IBusinessReferenceDataPublicationEligibility? eligibility = null,
        IBusinessReferenceDataVerifiedMarketOperationalEligibility? marketEligibility = null,
        IBusinessReferenceDataVerifiedGskuOperationalEligibility? operationalEligibility = null)
    {
        return new BusinessReferenceDataPublishService(
            Repository,
            new BusinessReferenceDataValidationService(Repository, NullLogger<BusinessReferenceDataValidationService>.Instance),
            new DefaultBusinessReferenceDataEvidenceAdapter(),
            new NoOpBusinessReferenceDataGovernanceAuditAdapter(),
            new DbBusinessReferenceDataEventPublisher(Repository),
            new MockBusinessReferenceDataPostPublicationReviewHook(),
            eligibility ?? new EligiblePublicationForTests(),
            observer ?? new NoOpBusinessReferenceDataPublishCheckpointObserver(),
            operationalEligibility,
            marketOperationalEligibility: marketEligibility);
    }

    public static string GetArtifactPath()
        => GetSeedPath("mod-0290-gsku-reference.json");

    public static string GetSeedPath(string fileName)
    {
        var directory = new DirectoryInfo(AppContext.BaseDirectory);
        while (directory is not null && !File.Exists(Path.Combine(directory.FullName, "AGENTS.md")))
        {
            directory = directory.Parent;
        }

        if (directory is null)
        {
            throw new DirectoryNotFoundException("Repository root was not found.");
        }

        return Path.Combine(directory.FullName, "services", "Diten.Platform", "src", "Diten.Platform.API", "Seed",
            "business-reference-data", fileName);
    }

    public ValueTask DisposeAsync() => new(_client.DropDatabaseAsync(_databaseName));
}

internal sealed class EligiblePublicationForTests : IBusinessReferenceDataPublicationEligibility
{
    public BusinessReferenceDataPublicationEligibilityDecision Evaluate()
        => new(true, "TEST_ONLY_ELIGIBLE", "TestOnly");
}

internal sealed class GskuOperationalEligibilityForTests : IBusinessReferenceDataVerifiedGskuOperationalEligibility
{
    private readonly VerifiedGskuOperationalFacts _facts;
    private readonly int? _denyAuthorizationCheck;
    private int _authorizationChecks;

    public GskuOperationalEligibilityForTests(
        VerifiedGskuOperationalFacts facts,
        int? denyAuthorizationCheck = null)
    {
        _facts = facts;
        _denyAuthorizationCheck = denyAuthorizationCheck;
        Authorization = new TestAuthorization();
    }

    public IBusinessReferenceDataVerifiedGskuOperationalAuthorization Authorization { get; }

    public Task<VerifiedGskuOperationalEligibilityDecision> EvaluateAsync(CancellationToken ct = default)
        => Task.FromResult(new VerifiedGskuOperationalEligibilityDecision(
            true,
            "VERIFIED_GSKU_OPERATIONAL_ELIGIBLE",
            _facts,
            Authorization));

    public Task<VerifiedGskuEnumerationEligibilityDecision> EvaluateEnumerationAsync(CancellationToken ct = default)
        => Task.FromResult(new VerifiedGskuEnumerationEligibilityDecision(
            true,
            "VERIFIED_GSKU_ENUMERATION_ELIGIBLE",
            new VerifiedGskuEnumerationFacts(
                _facts.CatalogPath,
                _facts.CatalogVersion,
                _facts.CatalogFingerprint,
                _facts.ReferenceTenantId,
                _facts.ConsumerTenantId,
                _facts.RequiredSetCodes)));

    public bool IsAuthorized(
        IBusinessReferenceDataVerifiedGskuOperationalAuthorization authorization,
        VerifiedGskuOperationalFacts facts)
    {
        var check = Interlocked.Increment(ref _authorizationChecks);
        return ReferenceEquals(authorization, Authorization)
               && facts == _facts
               && check != _denyAuthorizationCheck;
    }

    private sealed class TestAuthorization : IBusinessReferenceDataVerifiedGskuOperationalAuthorization;
}
