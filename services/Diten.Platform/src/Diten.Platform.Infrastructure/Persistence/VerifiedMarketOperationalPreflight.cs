using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using Diten.Platform.Application.Features.BusinessReferenceData.Services;
using Diten.Platform.Domain.Entities;
using Diten.Platform.Domain.Enums;
using Diten.Platform.Infrastructure.Persistence.Schema;
using MongoDB.Bson;
using MongoDB.Driver;

namespace Diten.Platform.Infrastructure.Persistence;

public sealed class VerifiedMarketOperationalPreflight : IVerifiedMarketOperationalPreflight
{
    private const string MarketSetCode = "market";
    private const string AuditRequestType = "BusinessReferenceData.publish";
    private const string AuditEntityType = "BusinessReferenceDataVersion";
    private const string LockedActorId = "11111111-1111-1111-1111-111111111111";
    private static readonly Guid LockedActorGuid = Guid.Parse(LockedActorId);
    private readonly IMongoDatabase _database;

    public VerifiedMarketOperationalPreflight(IMongoDatabase database)
    {
        _database = database ?? throw new ArgumentNullException(nameof(database));
    }

    public async Task<VerifiedMarketOperationalTargetPreflightResult> VerifyBeforeWriteAsync(
        VerifiedMarketOperationalFacts facts,
        CancellationToken ct = default)
    {
        ValidateLockedFacts(facts);
        await VerifyTopologyAsync(ct);
        await VerifyRequiredIndexesAsync(ct);
        return await InspectTargetAsync(facts, requireAuditProof: true, ct);
    }

    public async Task<VerifiedMarketOperationalTargetPreflightResult> VerifyCompletionAsync(
        VerifiedMarketOperationalFacts facts,
        CancellationToken ct = default)
    {
        ValidateLockedFacts(facts);
        await VerifyTopologyAsync(ct);
        return await InspectTargetAsync(facts, requireAuditProof: true, ct);
    }

    public async Task<VerifiedMarketOperationalAuditProofResult> VerifyAuditOutboxAsync(
        VerifiedMarketOperationalAuditProofRequest request,
        CancellationToken ct = default)
    {
        if (request.TenantId != Guid.Parse("00000000-0000-0000-0000-000000000001")
            || request.CorrelationId == Guid.Empty
            || request.EntityId == Guid.Empty
            || string.IsNullOrWhiteSpace(request.AuditIdempotencyKey)
            || string.IsNullOrWhiteSpace(request.PublicationCorrelationId)
            || !string.Equals(request.ActorId, LockedActorId, StringComparison.Ordinal)
            || !string.Equals(request.RequestType, AuditRequestType, StringComparison.Ordinal)
            || !string.Equals(request.EntityType, AuditEntityType, StringComparison.Ordinal)
            || request.Operation != AuditOperation.Activate
            || !string.Equals(request.GovernanceEvent, "publish", StringComparison.Ordinal)
            || !string.Equals(request.PublishMode, "Immediate", StringComparison.Ordinal)
            || !string.Equals(request.CatalogVersion, "UNSD-M49-2026-08-08", StringComparison.Ordinal)
            || !string.Equals(request.SetCode, MarketSetCode, StringComparison.Ordinal)
            || !request.PublicationOperationKey.EndsWith(":businessreferencedata-catalog-vunsd-m49-2026-08-08:market", StringComparison.Ordinal))
        {
            return new(false, "VERIFIED_MARKET_OPERATIONAL_AUDIT_PROOF_INVALID", 0);
        }

        var collection = _database.GetCollection<BsonDocument>(AuditCollectionNames.AuditOutbox);
        var filter = Builders<BsonDocument>.Filter.Eq(nameof(Persistence.Models.AuditOutboxMessage.IdempotencyKey), request.AuditIdempotencyKey);
        var matches = await collection.Find(filter).ToListAsync(ct);
        if (matches.Count != 1)
        {
            return new(false, "VERIFIED_MARKET_OPERATIONAL_AUDIT_PROOF_AMBIGUOUS", matches.Count);
        }

        return IsExactAuditDocument(matches[0], request)
            ? new VerifiedMarketOperationalAuditProofResult(true, "VERIFIED_MARKET_OPERATIONAL_AUDIT_PROOF_EXACT", 1)
            : new VerifiedMarketOperationalAuditProofResult(false, "VERIFIED_MARKET_OPERATIONAL_AUDIT_PROOF_MISMATCH", 1);
    }

    private async Task VerifyTopologyAsync(CancellationToken ct)
    {
        var hello = await _database.RunCommandAsync<BsonDocument>(new BsonDocument("hello", 1), cancellationToken: ct);
        var isWritablePrimary = hello.TryGetValue("isWritablePrimary", out var writable) && writable.IsBoolean && writable.AsBoolean;
        var hasReplicaSet = hello.TryGetValue("setName", out var setName) && setName.IsString && !string.IsNullOrWhiteSpace(setName.AsString);
        var hasSessions = hello.TryGetValue("logicalSessionTimeoutMinutes", out var sessionTimeout) && sessionTimeout.IsNumeric && sessionTimeout.ToInt32() > 0;
        if (!isWritablePrimary || !hasReplicaSet || !hasSessions)
        {
            throw new InvalidOperationException("VERIFIED_MARKET_OPERATIONAL_WRITABLE_PRIMARY_REQUIRED");
        }
    }

    private async Task VerifyRequiredIndexesAsync(CancellationToken ct)
    {
        var required = new[]
        {
            new RequiredIndex(
                PlatformCollections.BusinessReferenceDataSets,
                "ux_business_reference_data_sets_tenant_code",
                new BsonDocument { ["TenantId"] = 1, ["SetCode"] = 1 },
                true,
                new BsonDocument("IsDeleted", false)),
            new RequiredIndex(
                PlatformCollections.BusinessReferenceDataVersions,
                "ux_business_reference_data_versions_set_number",
                new BsonDocument { ["TenantId"] = 1, ["BusinessReferenceDataSetId"] = 1, ["VersionNumber"] = 1 },
                true,
                new BsonDocument("IsDeleted", false)),
            new RequiredIndex(
                PlatformCollections.BusinessReferenceDataIntegrationEvents,
                "ux_business_reference_data_events_idempotency",
                new BsonDocument { ["TenantId"] = 1, ["BusinessReferenceDataVersionId"] = 1, ["EventName"] = 1, ["IdempotencyKey"] = 1 },
                true,
                new BsonDocument("IsDeleted", false)),
            new RequiredIndex(
                PlatformCollections.BusinessReferenceDataPublishOperations,
                "ux_business_reference_data_publish_operations_idempotency",
                new BsonDocument { ["TenantId"] = 1, ["IdempotencyKey"] = 1 },
                true,
                new BsonDocument("IsDeleted", false)),
            new RequiredIndex(
                AuditCollectionNames.AuditOutbox,
                "ux_audit_outbox_idempotency_key",
                new BsonDocument("IdempotencyKey", 1),
                true,
                null)
        };

        foreach (var expected in required)
        {
            List<BsonDocument> indexes;
            try
            {
                using var cursor = await _database.GetCollection<BsonDocument>(expected.CollectionName)
                    .Indexes.ListAsync(cancellationToken: ct);
                indexes = await cursor.ToListAsync(ct);
            }
            catch (MongoCommandException exception) when (exception.CodeName == "NamespaceNotFound")
            {
                throw new InvalidOperationException("VERIFIED_MARKET_OPERATIONAL_REQUIRED_INDEX_MISSING", exception);
            }

            var actual = indexes.SingleOrDefault(index =>
                index.TryGetValue("name", out var name)
                && name.IsString
                && string.Equals(name.AsString, expected.Name, StringComparison.Ordinal));
            if (actual is null || !IndexMatches(actual, expected))
            {
                throw new InvalidOperationException("VERIFIED_MARKET_OPERATIONAL_REQUIRED_INDEX_MISMATCH");
            }
        }
    }

    private async Task<VerifiedMarketOperationalTargetPreflightResult> InspectTargetAsync(
        VerifiedMarketOperationalFacts facts,
        bool requireAuditProof,
        CancellationToken ct)
    {
        var operationKey = BuildOperationKey(facts);
        var setFilter = Builders<BusinessReferenceDataSet>.Filter.And(
            Builders<BusinessReferenceDataSet>.Filter.Eq(set => set.TenantId, facts.ReferenceTenantId),
            Builders<BusinessReferenceDataSet>.Filter.Regex(
                set => set.SetCode,
                new BsonRegularExpression("^market$", "i")));
        var sets = await _database.GetCollection<BusinessReferenceDataSet>(PlatformCollections.BusinessReferenceDataSets)
            .Find(setFilter)
            .ToListAsync(ct);
        var operationFilter = Builders<BusinessReferenceDataPublishOperation>.Filter.And(
            Builders<BusinessReferenceDataPublishOperation>.Filter.Eq(operation => operation.TenantId, facts.ReferenceTenantId),
            Builders<BusinessReferenceDataPublishOperation>.Filter.Regex(
                operation => operation.IdempotencyKey,
                new BsonRegularExpression(":businessreferencedata-catalog-vunsd-m49-2026-08-08:market$", "i")));
        var operations = await _database.GetCollection<BusinessReferenceDataPublishOperation>(PlatformCollections.BusinessReferenceDataPublishOperations)
            .Find(operationFilter)
            .ToListAsync(ct);
        var orphanVersionFilter = Builders<BusinessReferenceDataVersion>.Filter.And(
            Builders<BusinessReferenceDataVersion>.Filter.Eq(version => version.TenantId, facts.ReferenceTenantId),
            Builders<BusinessReferenceDataVersion>.Filter.Regex(
                version => version.LastPublishIdempotencyKey,
                new BsonRegularExpression(":businessreferencedata-catalog-vunsd-m49-2026-08-08:market$", "i")));
        var marketVersions = await _database.GetCollection<BusinessReferenceDataVersion>(PlatformCollections.BusinessReferenceDataVersions)
            .Find(orphanVersionFilter)
            .ToListAsync(ct);

        if (sets.Count == 0 && operations.Count == 0 && marketVersions.Count == 0)
        {
            return new(VerifiedMarketOperationalTargetDisposition.Fresh, "VERIFIED_MARKET_OPERATIONAL_TARGET_FRESH");
        }

        if (sets.Count != 1 || operations.Count != 1 || marketVersions.Count != 1)
        {
            return Manual("VERIFIED_MARKET_OPERATIONAL_TARGET_AMBIGUOUS");
        }

        var set = sets[0];
        var operation = operations[0];
        var versions = await _database.GetCollection<BusinessReferenceDataVersion>(PlatformCollections.BusinessReferenceDataVersions)
            .Find(version => version.TenantId == facts.ReferenceTenantId && version.BusinessReferenceDataSetId == set.BusinessReferenceDataSetId)
            .ToListAsync(ct);

        if (set.IsDeleted
            || versions.Count != 1
            || operation.IsDeleted
            || !string.Equals(operation.IdempotencyKey, operationKey, StringComparison.Ordinal)
            || operation.BusinessReferenceDataSetId != set.BusinessReferenceDataSetId)
        {
            return Manual("VERIFIED_MARKET_OPERATIONAL_TARGET_PREEXISTING");
        }

        var version = versions[0];
        var artifactMatches = await ArtifactMatchesPersistedAsync(facts, set, version, ct);
        var exact = !version.IsDeleted
                    && string.Equals(set.SetCode, MarketSetCode, StringComparison.Ordinal)
                    && marketVersions[0].BusinessReferenceDataVersionId == version.BusinessReferenceDataVersionId
                    && set.PublishedVersionId == version.BusinessReferenceDataVersionId
                    && set.ActiveDraftVersionId is null
                    && set.Status == BusinessReferenceDataSetStatus.Active
                    && version.Status == BusinessReferenceDataVersionStatus.Published
                    && version.IsImmutable
                    && string.Equals(version.LastPublishIdempotencyKey, operationKey, StringComparison.Ordinal)
                    && !string.IsNullOrWhiteSpace(version.LastCorrelationId)
                    && version.Values.Count == 249
                    && operation.BusinessReferenceDataVersionId == version.BusinessReferenceDataVersionId
                    && operation.OperationState == BusinessReferenceDataPublishOperationState.COMPLETED
                    && operation.PublishCheckpoint == BusinessReferenceDataPublishCheckpoint.COMPLETION_VERIFIED
                    && string.Equals(operation.CatalogVersion, facts.CatalogVersion, StringComparison.Ordinal)
                    && string.Equals(operation.CatalogFingerprint, facts.CatalogFingerprint, StringComparison.Ordinal)
                    && artifactMatches;
        if (!exact)
        {
            return Manual("VERIFIED_MARKET_OPERATIONAL_TARGET_PARTIAL_OR_MISMATCHED");
        }

        if (requireAuditProof)
        {
            var auditProof = await FindReplayAuditProofAsync(facts, version, operationKey, ct);
            if (!auditProof.IsExact || auditProof.MatchCount != 1)
            {
                return Manual(auditProof.ReasonCode);
            }
        }

        return new(VerifiedMarketOperationalTargetDisposition.ExactReplay, "VERIFIED_MARKET_OPERATIONAL_TARGET_EXACT_REPLAY");
    }

    private static async Task<bool> ArtifactMatchesPersistedAsync(
        VerifiedMarketOperationalFacts facts,
        BusinessReferenceDataSet persistedSet,
        BusinessReferenceDataVersion persistedVersion,
        CancellationToken ct)
    {
        try
        {
            var bytes = await File.ReadAllBytesAsync(facts.CatalogPath, ct);
            var actualHash = Convert.ToHexString(SHA256.HashData(bytes)).ToLowerInvariant();
            if (!string.Equals(actualHash, facts.CatalogFingerprint, StringComparison.Ordinal))
            {
                return false;
            }

            var catalog = JsonSerializer.Deserialize<BusinessReferenceDataCatalogDocument>(bytes);
            if (catalog is null
                || !string.Equals(catalog.CatalogVersion?.Trim(), facts.CatalogVersion, StringComparison.Ordinal)
                || catalog.Sets is null
                || catalog.Sets.Count != 1)
            {
                return false;
            }

            var expectedSet = catalog.Sets[0];
            var expectedCode = expectedSet.SetCode?.Trim();
            var expectedName = expectedSet.SetName?.Trim();
            var expectedScope = expectedSet.ScopeType?.Trim();
            var expectedDescription = string.IsNullOrWhiteSpace(expectedSet.Description)
                ? null
                : expectedSet.Description.Trim();
            if (!string.Equals(expectedCode, MarketSetCode, StringComparison.Ordinal)
                || string.IsNullOrWhiteSpace(expectedName)
                || string.IsNullOrWhiteSpace(expectedScope)
                || !string.Equals(persistedSet.SetCode, expectedCode, StringComparison.Ordinal)
                || !string.Equals(persistedSet.Name, expectedName, StringComparison.Ordinal)
                || !string.Equals(persistedSet.ScopeType, expectedScope, StringComparison.Ordinal)
                || !string.Equals(persistedSet.Description, expectedDescription, StringComparison.Ordinal)
                || persistedSet.Status != ParseExpectedSetStatus(expectedSet.Status))
            {
                return false;
            }

            var expectedDefinitions = (expectedSet.AttributeDefinitions ?? [])
                .Select(definition => new BusinessReferenceDataAttributeDefinition
                {
                    AttributeCode = definition.AttributeCode?.Trim() ?? string.Empty,
                    DisplayName = definition.DisplayName?.Trim() ?? string.Empty,
                    DataType = definition.DataType?.Trim().ToLowerInvariant() ?? string.Empty,
                    IsRequired = definition.IsRequired
                })
                .ToList();
            if (expectedDefinitions.Any(definition =>
                    string.IsNullOrWhiteSpace(definition.AttributeCode)
                    || string.IsNullOrWhiteSpace(definition.DisplayName)
                    || string.IsNullOrWhiteSpace(definition.DataType))
                || expectedDefinitions.GroupBy(definition => definition.AttributeCode, StringComparer.OrdinalIgnoreCase)
                    .Any(group => group.Count() != 1)
                || persistedVersion.AttributeDefinitions.Count != expectedDefinitions.Count
                || persistedVersion.AttributeDefinitions.Any(actual => !expectedDefinitions.Any(expected =>
                    string.Equals(actual.AttributeCode, expected.AttributeCode, StringComparison.Ordinal)
                    && string.Equals(actual.DisplayName, expected.DisplayName, StringComparison.Ordinal)
                    && string.Equals(actual.DataType, expected.DataType, StringComparison.Ordinal)
                    && actual.IsRequired == expected.IsRequired)))
            {
                return false;
            }

            var expectedValues = new List<BusinessReferenceDataValue>(expectedSet.Values?.Count ?? 0);
            foreach (var value in expectedSet.Values ?? [])
            {
                var code = value.ValueCode?.Trim();
                var displayName = value.DisplayName?.Trim();
                if (string.IsNullOrWhiteSpace(code) || string.IsNullOrWhiteSpace(displayName))
                {
                    return false;
                }

                var attributes = new Dictionary<string, string>(StringComparer.Ordinal);
                foreach (var attribute in value.Attributes ?? new Dictionary<string, string>())
                {
                    var key = attribute.Key.Trim();
                    if (string.IsNullOrWhiteSpace(key) || !attributes.TryAdd(key, attribute.Value.Trim()))
                    {
                        return false;
                    }
                }

                expectedValues.Add(new BusinessReferenceDataValue
                {
                    ValueCode = code,
                    DisplayName = displayName,
                    Description = string.IsNullOrWhiteSpace(value.Description) ? null : value.Description.Trim(),
                    IsActive = value.IsActive,
                    SortOrder = value.SortOrder,
                    Attributes = attributes.Count == 0 ? null : attributes
                });
            }

            return expectedValues.Count == 249
                   && expectedValues.GroupBy(value => value.ValueCode, StringComparer.OrdinalIgnoreCase)
                       .All(group => group.Count() == 1)
                   && persistedVersion.Values.Count == expectedValues.Count
                   && persistedVersion.Values.All(actual => expectedValues.Any(expected =>
                       string.Equals(actual.ValueCode, expected.ValueCode, StringComparison.Ordinal)
                       && string.Equals(actual.DisplayName, expected.DisplayName, StringComparison.Ordinal)
                       && string.Equals(actual.Description, expected.Description, StringComparison.Ordinal)
                       && actual.IsActive == expected.IsActive
                       && actual.SortOrder == expected.SortOrder
                       && DictionaryEquals(actual.Attributes, expected.Attributes)));
        }
        catch (Exception exception) when (exception is IOException or UnauthorizedAccessException or JsonException)
        {
            return false;
        }
    }

    private static BusinessReferenceDataSetStatus ParseExpectedSetStatus(string? raw) =>
        !string.IsNullOrWhiteSpace(raw)
        && Enum.TryParse<BusinessReferenceDataSetStatus>(raw.Trim(), true, out var parsed)
            ? parsed
            : BusinessReferenceDataSetStatus.Active;

    private static bool DictionaryEquals(
        IReadOnlyDictionary<string, string>? left,
        IReadOnlyDictionary<string, string>? right)
    {
        left ??= new Dictionary<string, string>();
        right ??= new Dictionary<string, string>();
        return left.Count == right.Count
               && left.All(pair => right.TryGetValue(pair.Key, out var value)
                                   && string.Equals(pair.Value, value, StringComparison.Ordinal));
    }

    private async Task<VerifiedMarketOperationalAuditProofResult> FindReplayAuditProofAsync(
        VerifiedMarketOperationalFacts facts,
        BusinessReferenceDataVersion version,
        string operationKey,
        CancellationToken ct)
    {
        var auditCorrelationId = BuildDeterministicAuditCorrelationId(operationKey);
        var collection = _database.GetCollection<BsonDocument>(AuditCollectionNames.AuditOutbox);
        var filter = Builders<BsonDocument>.Filter.And(
            Builders<BsonDocument>.Filter.Eq("TenantId", facts.ReferenceTenantId),
            Builders<BsonDocument>.Filter.Eq("CorrelationId", auditCorrelationId),
            Builders<BsonDocument>.Filter.Eq("RequestType", AuditRequestType),
            Builders<BsonDocument>.Filter.Eq("EntityType", AuditEntityType),
            Builders<BsonDocument>.Filter.Eq("EntityId", version.BusinessReferenceDataVersionId),
            Builders<BsonDocument>.Filter.Eq("Operation", (int)AuditOperation.Activate));
        var matches = await collection.Find(filter).ToListAsync(ct);
        if (matches.Count != 1)
        {
            return new(false, "VERIFIED_MARKET_OPERATIONAL_AUDIT_PROOF_AMBIGUOUS", matches.Count);
        }

        var idempotencyKey = matches[0].GetValue("IdempotencyKey", BsonNull.Value);
        if (!idempotencyKey.IsString)
        {
            return new(false, "VERIFIED_MARKET_OPERATIONAL_AUDIT_PROOF_MISMATCH", 1);
        }

        return await VerifyAuditOutboxAsync(
            new VerifiedMarketOperationalAuditProofRequest(
                facts.ReferenceTenantId,
                auditCorrelationId,
                version.LastCorrelationId!,
                idempotencyKey.AsString,
                AuditRequestType,
                AuditEntityType,
                version.BusinessReferenceDataVersionId,
                AuditOperation.Activate,
                "publish",
                "Immediate",
                version.PublishedBy ?? version.UpdatedBy ?? version.CreatedBy,
                operationKey,
                facts.CatalogVersion,
                MarketSetCode),
            ct);
    }

    private static bool IsExactAuditDocument(BsonDocument document, VerifiedMarketOperationalAuditProofRequest expected)
    {
        if (!DocumentGuidEquals(document, "TenantId", expected.TenantId)
            || !DocumentGuidEquals(document, "CorrelationId", expected.CorrelationId)
            || !DocumentGuidEquals(document, "EntityId", expected.EntityId)
            || !DocumentStringEquals(document, "IdempotencyKey", expected.AuditIdempotencyKey)
            || !DocumentStringEquals(document, "RequestType", expected.RequestType)
            || !DocumentStringEquals(document, "EntityType", expected.EntityType)
            || !DocumentIntEquals(document, "Operation", (int)expected.Operation)
            || !document.TryGetValue("Payload", out var payloadValue)
            || !payloadValue.IsBsonDocument)
        {
            return false;
        }

        var payload = payloadValue.AsBsonDocument;
        if (!DocumentGuidEquals(payload, "TargetTenantId", expected.TenantId)
            || !DocumentEnumEquals(payload, "ActorType", AuditActorType.PlatformAdministrator)
            || !DocumentGuidEquals(payload, "ActorId", LockedActorGuid)
            || !DocumentStringEquals(payload, "SourceService", "Diten.Platform")
            || !DocumentStringEquals(payload, "SourceModule", "MOD-0048-FU01")
            || !DocumentEnumEquals(payload, "Outcome", AuditOutcome.Succeeded)
            || !payload.TryGetValue("Metadata", out var metadataValue)
            || !TryUnwrapExactMetadata(metadataValue, out var metadata))
        {
            return false;
        }

        return DocumentStringEquals(metadata, "governanceEvent", expected.GovernanceEvent)
               && DocumentStringEquals(metadata, "publishMode", expected.PublishMode)
               && DocumentStringEquals(metadata, "publicationCorrelationId", expected.PublicationCorrelationId)
               && DocumentStringEquals(metadata, "publicationOperationKey", expected.PublicationOperationKey)
               && DocumentStringEquals(metadata, "catalogVersion", expected.CatalogVersion)
               && DocumentStringEquals(metadata, "setCode", expected.SetCode)
               && DocumentStringEquals(metadata, "actor", expected.ActorId)
               && DocumentStringEquals(
                   metadata,
                   "actorType",
                   AuditActorType.PlatformAdministrator.ToString());
    }

    private static bool TryUnwrapExactMetadata(BsonValue value, out BsonDocument metadata)
    {
        metadata = null!;
        if (!value.IsBsonDocument)
        {
            return false;
        }

        var envelope = value.AsBsonDocument;
        const string dictionaryDiscriminator =
            "System.Collections.Generic.Dictionary`2[System.String,System.Object]";
        if (envelope.ElementCount != 2
            || !DocumentStringEquals(envelope, "_t", dictionaryDiscriminator)
            || !envelope.TryGetValue("_v", out var dictionaryValue)
            || !dictionaryValue.IsBsonDocument)
        {
            return false;
        }

        var candidate = dictionaryValue.AsBsonDocument;
        string[] exactKeys =
        [
            "governanceEvent",
            "publishMode",
            "reasonCode",
            "publicationCorrelationId",
            "publicationOperationKey",
            "catalogVersion",
            "setCode",
            "actor",
            "actorType"
        ];
        if (candidate.ElementCount != exactKeys.Length
            || exactKeys.Any(key => !candidate.Contains(key)))
        {
            return false;
        }

        var reasonCode = candidate["reasonCode"];
        if (!reasonCode.IsBsonNull && !reasonCode.IsString)
        {
            return false;
        }

        metadata = candidate;
        return true;
    }

    private static bool IndexMatches(BsonDocument actual, RequiredIndex expected)
    {
        if (!actual.TryGetValue("key", out var key) || !key.IsBsonDocument || !key.AsBsonDocument.Equals(expected.Keys))
        {
            return false;
        }

        var unique = actual.TryGetValue("unique", out var uniqueValue) && uniqueValue.IsBoolean && uniqueValue.AsBoolean;
        if (unique != expected.Unique
            || actual.Contains("expireAfterSeconds")
            || actual.Contains("collation")
            || actual.Contains("wildcardProjection")
            || (actual.TryGetValue("hidden", out var hidden) && hidden.ToBoolean())
            || (actual.TryGetValue("sparse", out var sparse) && sparse.ToBoolean()))
        {
            return false;
        }

        if (expected.PartialFilter is null)
        {
            return !actual.Contains("partialFilterExpression");
        }

        return actual.TryGetValue("partialFilterExpression", out var partial)
               && partial.IsBsonDocument
               && partial.AsBsonDocument.Equals(expected.PartialFilter);
    }

    private static string BuildOperationKey(VerifiedMarketOperationalFacts facts) =>
        $"{facts.IdempotencyNamespace}:businessreferencedata-catalog-v{facts.CatalogVersion}:market".ToLowerInvariant();

    private static Guid BuildDeterministicAuditCorrelationId(string operationKey)
    {
        Span<byte> bytes = stackalloc byte[16];
        SHA256.HashData(Encoding.UTF8.GetBytes($"verified-market-audit|{operationKey}"))
            .AsSpan(0, 16)
            .CopyTo(bytes);
        return new Guid(bytes);
    }

    private static VerifiedMarketOperationalTargetPreflightResult Manual(string reasonCode) =>
        new(VerifiedMarketOperationalTargetDisposition.ManualReconciliationRequired, reasonCode);

    private static bool DocumentStringEquals(BsonDocument document, string name, string expected) =>
        document.TryGetValue(name, out var value) && value.IsString && string.Equals(value.AsString, expected, StringComparison.Ordinal);

    private static bool DocumentIntEquals(BsonDocument document, string name, int expected) =>
        document.TryGetValue(name, out var value) && value.IsNumeric && value.ToInt32() == expected;

    private static bool DocumentEnumEquals<TEnum>(BsonDocument document, string name, TEnum expected)
        where TEnum : struct, Enum
    {
        if (!document.TryGetValue(name, out var value))
        {
            return false;
        }

        return value.IsString
            ? string.Equals(value.AsString, expected.ToString(), StringComparison.Ordinal)
            : value.IsNumeric && value.ToInt32() == Convert.ToInt32(expected);
    }

    private static bool DocumentGuidEquals(BsonDocument document, string name, Guid expected)
    {
        if (!document.TryGetValue(name, out var value))
        {
            return false;
        }

        try
        {
            return value switch
            {
                BsonBinaryData binary => binary.ToGuid() == expected,
                BsonString text => Guid.TryParse(text.Value, out var parsed) && parsed == expected,
                _ => false
            };
        }
        catch
        {
            return false;
        }
    }

    private static void ValidateLockedFacts(VerifiedMarketOperationalFacts facts)
    {
        ArgumentNullException.ThrowIfNull(facts);
        if (facts.ReferenceTenantId != Guid.Parse("00000000-0000-0000-0000-000000000001")
            || !string.Equals(facts.CatalogVersion, "UNSD-M49-2026-08-08", StringComparison.Ordinal)
            || !string.Equals(facts.CatalogFingerprint, "b94c45280195b0cb5faa155656c4690938790144d148fba279d2232204360039", StringComparison.Ordinal)
            || string.IsNullOrWhiteSpace(facts.CatalogPath)
            || !string.Equals(Path.GetFileName(facts.CatalogPath), "mod-0290-market-reference.json", StringComparison.Ordinal)
            || !string.Equals(facts.ActorId, LockedActorId, StringComparison.Ordinal)
            || string.IsNullOrWhiteSpace(facts.IdempotencyNamespace)
            || !string.Equals(facts.IdempotencyNamespace, facts.IdempotencyNamespace.Trim(), StringComparison.Ordinal))
        {
            throw new InvalidOperationException("VERIFIED_MARKET_OPERATIONAL_FACTS_INVALID");
        }
    }

    private sealed record RequiredIndex(
        string CollectionName,
        string Name,
        BsonDocument Keys,
        bool Unique,
        BsonDocument? PartialFilter);
}
