using Diten.Platform.Application.Features.BusinessReferenceData.Services;
using Diten.Platform.Common.Tenancy;
using Diten.Platform.Domain.Entities;
using Diten.Platform.Infrastructure.Persistence;
using Diten.Platform.Infrastructure.Persistence.Repositories.BusinessReferenceData;
using Diten.Platform.Infrastructure.Persistence.Schema;
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
        _harness = await BusinessReferenceDataTestHarness.CreateAsync("gsku_catalog");
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
        await using var withoutProvider = await BusinessReferenceDataTestHarness.CreateAsync(
            "gsku_catalog_noprovider",
            configureProvider: false);
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
        await using var legacy = await BusinessReferenceDataTestHarness.CreateAsync(
            "gsku_catalog_legacy",
            configureProvider: false);
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

        // BL-482: the loader loads EVERY set in the file (the code list is a presence check, not a filter), and
        // both seed files are shared with other modules and grow. The count is read from the file, not retyped:
        // "3" was true on 2026-08-24, the QMS seed reached 5 on 2026-08-28 and 6 on 2026-09-16, and nobody saw this
        // go stale because the harness was already red.
        var qmsSets = BusinessReferenceDataTestHarness.SeedSetCodes("document-management-qms.json");
        var legalSets = BusinessReferenceDataTestHarness.SeedSetCodes("legal-entity-reference.json");
        Assert.Superset(
            new HashSet<string> { "qms-document-class", "qms-document-classification", "qms-document-retention" },
            qmsSets.ToHashSet());
        Assert.Superset(new HashSet<string> { "legal-form", "country", "base-currency" }, legalSets.ToHashSet());

        Assert.Empty(qms.BlockedConflicts);
        Assert.Empty(legal.BlockedConflicts);
        Assert.Equal(qmsSets.Count, qms.SetsLoaded);
        Assert.Equal(legalSets.Count, legal.SetsLoaded);
        Assert.Equal(qmsSets.Count + legalSets.Count, await legacy.Database.GetCollection<BusinessReferenceDataSet>("business_reference_data_sets")
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
        // BL-482: every set in the QMS seed lands under the legacy tenant; the count is read from the file (see
        // GenericLegacyLoader_… above for why it is not a literal).
        var qmsSets = BusinessReferenceDataTestHarness.SeedSetCodes("document-management-qms.json");
        Assert.Contains("qms-document-class", qmsSets);
        Assert.Equal(qmsSets.Count, await sets.CountDocumentsAsync(x => x.TenantId == legacyTenantId));
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

/*
 * BL-482 (BRD half) — WHAT THIS HARNESS USED TO DO, AND WHY IT WAS RED ON EVERY RUN (measured 2026-10-01).
 *
 * It built its own MongoClient, named a database with a fresh Guid per TEST, pinged it with
 * `RunCommandAsync<object>("{ ping: 1 }")`, and dropped the database on dispose. Two defects fed each other:
 *
 *   1. The local mongod is a replica set, so the ping reply is { ok, $clusterTime, operationTime } — and
 *      operationTime / $clusterTime.clusterTime are BSON Timestamps. Reading the reply as `object` goes through
 *      ObjectSerializer, which has no CLR type for a Timestamp: "ObjectSerializer does not support BSON type
 *      'Timestamp'". The Timestamp is in the COMMAND REPLY — not in a stored document and not in a field production
 *      code writes. InitializeAsync threw AFTER the per-test database had been created and marked, and xUnit does
 *      not call DisposeAsync when InitializeAsync throws, so every test left a database behind (46 on this machine).
 *   2. A minute later that residue was "stale", and every test class swept it at once from its own thread:
 *      "Command dropDatabase failed: The database is currently being dropped" — raised in the SWEEP, in whichever
 *      class lost the race, naming a database that belonged to another class.
 *
 * So one run was red on the Timestamp (45 of 50) and the next on the drop (52 of 53), in turn.
 *
 * WHAT IT DOES NOW. It is a thin layer over MongoIntegrationHarness.CreateIsolatedAsync: a FIXED-name database per
 * scope, emptied (documents deleted, collections and indexes kept) before each test, never dropped. It pings with
 * a BsonDocument reply, takes the machine-wide lock, and stamps the marker — all in the one place that already
 * does those correctly.
 *
 * ⚠ WHY A SCOPED DATABASE AND NOT THE SHARED ONE + A FRESH TENANT. These tests assert on the WHOLE collection:
 * "the loader wrote nothing at all" (CountDocuments(Empty) == 0), "exactly one operation exists" (Single over an
 * empty filter), "no OTHER tenant got a market set". In the shared database those are false the moment a parallel
 * class writes, and narrowing them to one tenant would stop them proving what they are there to prove. Their
 * subject is database-global, which is the stated exception in MongoIntegrationHarness.CreateIsolatedAsync.
 * Every BRD unique index IS tenant-keyed (measured), so the tests that only need tenant isolation —
 * TenantAssignment, PublishOperation — did move to the shared database.
 *
 * ⚠ ONE SCOPE, ONE OWNER AT A TIME. Opening a scope EMPTIES it. xUnit runs test classes in parallel, so two classes
 * on the same scope would empty each other mid-assertion and fail at random. A scope that is already open in this
 * process is therefore refused, loudly, instead of being emptied under its owner
 * (BusinessReferenceDataTestHarnessScopeTests). Across processes the machine-wide lock already excludes.
 */
internal sealed class BusinessReferenceDataTestHarness : IAsyncDisposable
{
    private static readonly System.Collections.Concurrent.ConcurrentDictionary<string, byte> OpenScopes =
        new(StringComparer.Ordinal);

    private readonly Persistence.MongoIntegrationHarness _mongo;
    private readonly string _scope;
    private int _disposed;

    private BusinessReferenceDataTestHarness(
        Persistence.MongoIntegrationHarness mongo,
        string scope,
        bool configureProvider)
    {
        _mongo = mongo;
        _scope = scope;
        Database = mongo.Database;
        ReferenceTenantId = mongo.TenantId;
        TenantContext = mongo.TenantContext;
        Repository = configureProvider
            ? new BusinessReferenceDataStewardshipRepository(
                mongo.DbContext,
                TenantContext,
                Options.Create(new BusinessReferenceDataProviderOptions { ReferenceTenantId = ReferenceTenantId }))
            : new BusinessReferenceDataStewardshipRepository(
                mongo.DbContext,
                TenantContext);
    }

    public Guid ReferenceTenantId { get; }
    public IMongoDatabase Database { get; }
    public TenantContext TenantContext { get; }
    public BusinessReferenceDataStewardshipRepository Repository { get; }

    /// <summary>The fixed database a scope maps to: <c>diten_platform_itest_brd_{scope}</c>.</summary>
    internal static string DatabaseNameFor(string scope)
        => Persistence.MongoIntegrationHarness.IsolatedDatabaseName($"brd_{scope}");

    /// <param name="scope">
    /// A FIXED name (lowercase letters, digits, underscores) owned by exactly one test class — or by one role
    /// inside one class, for a test that needs a second blank database. Never a Guid.
    /// </param>
    public static async Task<BusinessReferenceDataTestHarness> CreateAsync(string scope, bool configureProvider = true)
    {
        var databaseName = DatabaseNameFor(scope); // refuses a scope outside the owned grammar before Mongo is touched
        if (!OpenScopes.TryAdd(scope, 0))
        {
            throw new InvalidOperationException(
                $"BRD test scope '{scope}' ({databaseName}) is already open in this process. Opening a scope empties "
                + "its database, so a second owner would wipe the first one's data mid-test. Give each test class "
                + "(and each extra database inside a test) its own fixed scope.");
        }

        try
        {
            var mongo = await Persistence.MongoIntegrationHarness.CreateIsolatedAsync(
                $"brd_{scope}",
                SchemaProfile.BusinessReferenceData);
            return new BusinessReferenceDataTestHarness(mongo, scope, configureProvider);
        }
        catch
        {
            OpenScopes.TryRemove(scope, out _);
            throw;
        }
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

    /// <summary>
    /// The set codes a seed catalog declares, read straight from the JSON — independent of the loader under test.
    /// </summary>
    public static IReadOnlyList<string> SeedSetCodes(string fileName)
    {
        using var document = System.Text.Json.JsonDocument.Parse(File.ReadAllText(GetSeedPath(fileName)));
        return document.RootElement.GetProperty("sets").EnumerateArray()
            .Select(set => set.GetProperty("set_code").GetString()!)
            .ToArray();
    }

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

    /// <summary>Releases the scope. Nothing is dropped: the next owner of the scope empties it on the way in.</summary>
    public async ValueTask DisposeAsync()
    {
        if (Interlocked.Exchange(ref _disposed, 1) != 0)
        {
            return;
        }

        try
        {
            await _mongo.DisposeAsync();
        }
        finally
        {
            OpenScopes.TryRemove(_scope, out _);
        }
    }
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
