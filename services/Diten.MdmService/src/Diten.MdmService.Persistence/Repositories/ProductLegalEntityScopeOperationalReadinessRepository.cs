using Diten.MdmService.Application.Common;
using Diten.MdmService.Domain.Entities;
using Diten.MdmService.Domain.Enums;
using Diten.MdmService.Domain.Repositories;
using MongoDB.Bson;
using MongoDB.Driver;
using System.Security.Cryptography;
using System.Text;

namespace Diten.MdmService.Persistence.Repositories;

public sealed class ProductLegalEntityScopeOperationalReadinessRepository
    : IProductLegalEntityScopeOperationalReadinessRepository
{
    private const string GlobalProducts = "mdm_global_products";
    private const string Revisions = "mdm_product_definition_revisions";
    private const string Gskus = "mdm_gskus";
    private const string Lskus = "mdm_lskus";
    private const string FinishedGoods = "mdm_finished_goods";
    private const string AbbreviationRegister = "mdm_product_abbreviation_register";
    private const string AbbreviationLedger = "mdm_product_abbreviation_allocation_ledger";
    private const string AbbreviationHistory = "mdm_product_abbreviation_history";
    private const string ScopePolicies = "mdm_product_legal_entity_scope_policies";
    private const string RolloutStates = "mdm_product_legal_entity_scope_rollout_states";

    private readonly IMongoDatabase _database;
    private readonly Guid _tenantId;
    private readonly ITenantContext _tenantContext;

    public ProductLegalEntityScopeOperationalReadinessRepository(
        IMongoDatabase database,
        ITenantContext tenantContext)
    {
        ArgumentNullException.ThrowIfNull(database);
        ArgumentNullException.ThrowIfNull(tenantContext);
        if (tenantContext.TenantId == Guid.Empty)
        {
            throw new InvalidOperationException("A trusted tenant is required for product scope operational inspection.");
        }

        _database = database;
        _tenantId = tenantContext.TenantId;
        _tenantContext = tenantContext;
    }

    public async Task<ProductLegalEntityScopeOperationalReadiness> InspectAsync(
        int maximumSampleSize,
        CancellationToken cancellationToken = default)
    {
        cancellationToken.ThrowIfCancellationRequested();
        if (maximumSampleSize is < 1 or > ProductLegalEntityScopePolicy.MaximumLegalEntityIdsPerSnapshot)
        {
            throw new ArgumentOutOfRangeException(nameof(maximumSampleSize));
        }

        var rollout = await InspectRolloutAsync(cancellationToken);
        var descendantIntegrity = new[]
        {
            await InspectChainAsync("ProductDefinitionRevision", Revisions,
                [("GlobalProductId", GlobalProducts)], maximumSampleSize, cancellationToken),
            await InspectChainAsync("Gsku", Gskus,
                [("ProductDefinitionRevisionId", Revisions), ("GlobalProductId", GlobalProducts)],
                maximumSampleSize, cancellationToken),
            await InspectChainAsync("Lsku", Lskus,
                [("GskuId", Gskus), ("ProductDefinitionRevisionId", Revisions), ("GlobalProductId", GlobalProducts)],
                maximumSampleSize, cancellationToken),
            await InspectChainAsync("FinishedGood", FinishedGoods,
                [("GskuId", Gskus), ("ProductDefinitionRevisionId", Revisions), ("GlobalProductId", GlobalProducts)],
                maximumSampleSize, cancellationToken),
            await InspectChainAsync("ProductAbbreviationRegister", AbbreviationRegister,
                [("GlobalProductId", GlobalProducts)], maximumSampleSize, cancellationToken),
            await InspectChainAsync("ProductAbbreviationAllocationLedger", AbbreviationLedger,
                [("GlobalProductId", GlobalProducts)], maximumSampleSize, cancellationToken),
            await InspectChainAsync("ProductAbbreviationHistory", AbbreviationHistory,
                [("GlobalProductId", GlobalProducts)], maximumSampleSize, cancellationToken)
        };

        var audit = new[]
        {
            await InspectAuditAsync(
                ScopePolicies,
                AuditAggregateType.ProductLegalEntityScopePolicy,
                maximumSampleSize,
                cancellationToken),
            await InspectAuditAsync(
                RolloutStates,
                AuditAggregateType.ProductLegalEntityScopeRolloutState,
                maximumSampleSize,
                cancellationToken)
        };

        return new ProductLegalEntityScopeOperationalReadiness(rollout, descendantIntegrity, audit);
    }

    public async Task<string> CaptureMutationStateHashAsync(
        CancellationToken cancellationToken = default)
    {
        using var hash = IncrementalHash.CreateHash(HashAlgorithmName.SHA256);
        foreach (var collection in MutationCollections)
        {
            Append(hash, $"collection={collection}\n");
            await AppendCollectionAsync(hash, collection, MutationProjection, cancellationToken);
        }
        return Convert.ToHexString(hash.GetHashAndReset());
    }

    public async Task<ProductLegalEntityScopeInventorySnapshot> CaptureInventorySnapshotAsync(
        ProductLegalEntityScopeInventorySnapshotRequest request,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(request);
        if (request.TenantId != _tenantId || request.RolloutStateId == Guid.Empty
            || request.CommandId == Guid.Empty || request.ActorId == Guid.Empty
            || request.RolloutVersion < 0 || request.ObservedAtUtc.Offset != TimeSpan.Zero
            || request.Action is not ("ActivateEnforced" or "SuspendFailClosed")
            || !ProductLegalEntityScopeActivationFence.Reason(request.ReasonCode))
        {
            throw new InvalidOperationException("PRODUCT_SCOPE_INVENTORY_SNAPSHOT_REQUEST_INVALID");
        }

        var completeness = await new GlobalProductRepository(_database, _tenantContext)
            .GetProductLegalEntityScopeCompletenessInventoryAsync(request.ObservedAtUtc, 200, cancellationToken);
        var readiness = await InspectAsync(200, cancellationToken);
        var eligibleHash = await HashEligibleGlobalProductIdsAsync(cancellationToken);
        var missingHash = await HashMissingGlobalProductIdsAsync(request.ObservedAtUtc, cancellationToken);
        var policyHash = await HashCollectionAsync(ScopePolicies, MutationProjection, cancellationToken);
        var policyCount = await CountActiveAsync(ScopePolicies, cancellationToken);

        var relationshipHashes = new Dictionary<string, string>(StringComparer.Ordinal);
        foreach (var category in DescendantCollections)
            relationshipHashes[category.Category] = await HashCollectionAsync(
                category.Collection,
                MutationProjection,
                cancellationToken);
        var auditHashes = new Dictionary<AuditAggregateType, string>();
        auditHashes[AuditAggregateType.ProductLegalEntityScopePolicy] = await HashCollectionAsync(
            ScopePolicies,
            AuditProjection,
            cancellationToken);
        auditHashes[AuditAggregateType.ProductLegalEntityScopeRolloutState] = await HashCollectionAsync(
            RolloutStates,
            AuditProjection,
            cancellationToken);

        var stable = BuildSnapshotPayload(
            request,
            completeness,
            readiness,
            eligibleHash,
            missingHash,
            policyCount,
            policyHash,
            relationshipHashes,
            auditHashes,
            includeObservedAt: false);
        var payload = BuildSnapshotPayload(
            request,
            completeness,
            readiness,
            eligibleHash,
            missingHash,
            policyCount,
            policyHash,
            relationshipHashes,
            auditHashes,
            includeObservedAt: true);
        var snapshot = new ProductLegalEntityScopeInventorySnapshot
        {
            ObservedAtUtc = request.ObservedAtUtc,
            CanonicalPayload = payload,
            StableFactsHash = Sha(stable),
            SnapshotHash = Sha(payload)
        };
        snapshot.EnsureValid();
        return snapshot;
    }

    private async Task AppendCollectionAsync(
        IncrementalHash hash,
        string collectionName,
        Func<BsonDocument, BsonDocument> projection,
        CancellationToken cancellationToken)
    {
        using var cursor = await _database.GetCollection<BsonDocument>(collectionName)
            .Find(ActiveTenantDocumentFilter)
            .Sort(new BsonDocument("_id", 1))
            .ToCursorAsync(cancellationToken);
        while (await cursor.MoveNextAsync(cancellationToken))
        {
            foreach (var document in cursor.Current)
                Append(hash, Canonicalize(projection(document)) + "\n");
        }
    }

    private async Task<string> HashCollectionAsync(
        string collectionName,
        Func<BsonDocument, BsonDocument> projection,
        CancellationToken cancellationToken)
    {
        using var hash = IncrementalHash.CreateHash(HashAlgorithmName.SHA256);
        await AppendCollectionAsync(hash, collectionName, projection, cancellationToken);
        return Convert.ToHexString(hash.GetHashAndReset());
    }

    private async Task<string> HashEligibleGlobalProductIdsAsync(CancellationToken cancellationToken)
    {
        var tenant = new BsonBinaryData(_tenantId, GuidRepresentation.Standard);
        var filter = new BsonDocument
        {
            { "TenantId", tenant },
            { "IsDeleted", false },
            { "LifecycleStatus", new BsonDocument("$ne", (int)ProductIdentityLifecycleStatus.Retired) }
        };
        using var cursor = await _database.GetCollection<BsonDocument>(GlobalProducts)
            .Find(filter)
            .Project(new BsonDocument("_id", 1))
            .Sort(new BsonDocument("_id", 1))
            .ToCursorAsync(cancellationToken);
        return await HashIdentityCursorAsync(cursor, cancellationToken);
    }

    private async Task<string> HashMissingGlobalProductIdsAsync(
        DateTimeOffset serverNowUtc,
        CancellationToken cancellationToken)
    {
        var tenant = new BsonBinaryData(_tenantId, GuidRepresentation.Standard);
        var policy = new BsonDocument("$arrayElemAt", new BsonArray { "$ScopePolicies", 0 });
        var policyPeriods = ProductLegalEntityScopeAggregation.ArrayOrEmpty(
            new BsonDocument("$getField", new BsonDocument
            {
                { "field", nameof(ProductLegalEntityScopePolicy.ScopePeriods) },
                { "input", policy }
            }));
        var currentPeriods = new BsonDocument("$filter", new BsonDocument
        {
            { "input", policyPeriods },
            { "as", "period" },
            { "cond", ProductLegalEntityScopeAggregation.CreateCurrentPeriodExpression(serverNowUtc, "$$period") }
        });
        var isConfigured = new BsonDocument("$and", new BsonArray
        {
            new BsonDocument("$eq", new BsonArray { new BsonDocument("$size", "$ScopePolicies"), 1 }),
            new BsonDocument("$let", new BsonDocument
            {
                { "vars", new BsonDocument("policy", policy) },
                { "in", new BsonDocument("$and", new BsonArray
                    {
                        ProductLegalEntityScopeAggregation.CreatePolicyValidityExpression(serverNowUtc),
                        new BsonDocument("$eq", new BsonArray { new BsonDocument("$size", currentPeriods), 1 })
                    })
                }
            })
        });
        var pipeline = new[]
        {
            new BsonDocument("$match", new BsonDocument
            {
                { "TenantId", tenant },
                { "IsDeleted", false },
                { "LifecycleStatus", new BsonDocument("$ne", (int)ProductIdentityLifecycleStatus.Retired) }
            }),
            ProductLegalEntityScopeAggregation.LookupSingle(
                ProductLegalEntityScopePolicyRepository.CollectionName,
                "ScopePolicies",
                new BsonArray
                {
                    new BsonDocument("$eq", new BsonArray { "$TenantId", tenant }),
                    new BsonDocument("$eq", new BsonArray { "$IsDeleted", false }),
                    new BsonDocument("$eq", new BsonArray { "$GlobalProductId", "$$globalProductId" })
                },
                "$_id"),
            new BsonDocument("$set", new BsonDocument("IsConfigured", isConfigured)),
            new BsonDocument("$match", new BsonDocument("IsConfigured", false)),
            new BsonDocument("$sort", new BsonDocument("_id", 1)),
            new BsonDocument("$project", new BsonDocument("_id", 1))
        };
        using var cursor = await _database.GetCollection<BsonDocument>(GlobalProducts)
            .AggregateAsync<BsonDocument>(pipeline, cancellationToken: cancellationToken);
        return await HashIdentityCursorAsync(cursor, cancellationToken);
    }

    private static async Task<string> HashIdentityCursorAsync(
        IAsyncCursor<BsonDocument> cursor,
        CancellationToken cancellationToken)
    {
        using var hash = IncrementalHash.CreateHash(HashAlgorithmName.SHA256);
        while (await cursor.MoveNextAsync(cancellationToken))
        {
            foreach (var document in cursor.Current)
            {
                if (!document.TryGetValue("_id", out var value) || !value.IsGuid || value.AsGuid == Guid.Empty)
                    throw new InvalidOperationException("PRODUCT_SCOPE_INVENTORY_IDENTITY_INVALID");
                Append(hash, value.AsGuid.ToString("D") + "\n");
            }
        }
        return Convert.ToHexString(hash.GetHashAndReset());
    }

    private async Task<long> CountActiveAsync(string collectionName, CancellationToken cancellationToken) =>
        await _database.GetCollection<BsonDocument>(collectionName)
            .CountDocumentsAsync(ActiveTenantDocumentFilter, cancellationToken: cancellationToken);

    private static BsonDocument MutationProjection(BsonDocument source)
    {
        var document = source.DeepClone().AsBsonDocument;
        document.Remove(nameof(ProductLegalEntityScopeRolloutState.ActiveWriterLease));
        document.Remove(nameof(ProductLegalEntityScopeRolloutState.ActiveFence));
        document.Remove(nameof(ProductLegalEntityScopeRolloutState.AuditIntentReceipts));
        if (document.TryGetValue(nameof(ProductLegalEntityScopeRolloutState.AuditIntents), out var intents)
            && intents.IsBsonArray)
        {
            document[nameof(ProductLegalEntityScopeRolloutState.AuditIntents)] = new BsonArray(
                intents.AsBsonArray.Where(value => value.IsBsonDocument)
                    .Select(value => ProjectImmutableIntent(value.AsBsonDocument))
                    .OrderBy(value => value.GetValue("IntentId", BsonNull.Value).ToString(), StringComparer.Ordinal));
        }
        return document;
    }

    private static BsonDocument AuditProjection(BsonDocument source)
    {
        var projected = new BsonDocument { { "_id", source.GetValue("_id", BsonNull.Value) } };
        foreach (var field in new[] { "Version", "AuditIntents", "AuditIntentReceipts" })
            if (source.TryGetValue(field, out var value)) projected[field] = value.DeepClone();
        return projected;
    }

    private static BsonDocument ProjectImmutableIntent(BsonDocument intent)
    {
        var projected = new BsonDocument();
        foreach (var field in ImmutableIntentFields)
            if (intent.TryGetValue(field, out var value)) projected[field] = value.DeepClone();
        return projected;
    }

    private static string BuildSnapshotPayload(
        ProductLegalEntityScopeInventorySnapshotRequest request,
        GlobalProductScopeCompletenessInventory completeness,
        ProductLegalEntityScopeOperationalReadiness readiness,
        string eligibleHash,
        string missingHash,
        long policyCount,
        string policyHash,
        IReadOnlyDictionary<string, string> relationshipHashes,
        IReadOnlyDictionary<AuditAggregateType, string> auditHashes,
        bool includeObservedAt)
    {
        var builder = new StringBuilder();
        Line(builder, "schemaVersion", "1");
        Line(builder, "tenantId", request.TenantId.ToString("D"));
        Line(builder, "rolloutStateId", request.RolloutStateId.ToString("D"));
        Line(builder, "rolloutMode", ((int)request.RolloutMode).ToString(System.Globalization.CultureInfo.InvariantCulture));
        Line(builder, "rolloutVersion", request.RolloutVersion.ToString(System.Globalization.CultureInfo.InvariantCulture));
        Line(builder, "action", request.Action);
        Line(builder, "commandId", request.CommandId.ToString("D"));
        Line(builder, "actorId", request.ActorId.ToString("D"));
        Line(builder, "reasonCodeBase64", Convert.ToBase64String(Encoding.UTF8.GetBytes(request.ReasonCode)));
        if (includeObservedAt) Line(builder, "observedUtcTicks", request.ObservedAtUtc.UtcTicks.ToString(System.Globalization.CultureInfo.InvariantCulture));
        Line(builder, "eligibleGlobalProductCount", completeness.EligibleGlobalProductCount.ToString(System.Globalization.CultureInfo.InvariantCulture));
        Line(builder, "configuredGlobalProductCount", completeness.ConfiguredGlobalProductCount.ToString(System.Globalization.CultureInfo.InvariantCulture));
        Line(builder, "missingGlobalProductCount", (completeness.EligibleGlobalProductCount - completeness.ConfiguredGlobalProductCount).ToString(System.Globalization.CultureInfo.InvariantCulture));
        Line(builder, "eligibleGlobalProductHash", eligibleHash);
        Line(builder, "missingGlobalProductHash", missingHash);
        Line(builder, "policyCount", policyCount.ToString(System.Globalization.CultureInfo.InvariantCulture));
        Line(builder, "policyInventoryHash", policyHash);
        foreach (var category in readiness.DescendantIntegrity)
        {
            Line(builder, $"descendant.{category.Category}.total", category.TotalCount.ToString(System.Globalization.CultureInfo.InvariantCulture));
            Line(builder, $"descendant.{category.Category}.orphan", category.OrphanCount.ToString(System.Globalization.CultureInfo.InvariantCulture));
            Line(builder, $"descendant.{category.Category}.hash", relationshipHashes[category.Category]);
        }
        foreach (var audit in readiness.Audit.OrderBy(item => (int)item.AggregateType))
        {
            var prefix = $"audit.{(int)audit.AggregateType}";
            Line(builder, prefix + ".pending", audit.PendingCount.ToString(System.Globalization.CultureInfo.InvariantCulture));
            Line(builder, prefix + ".processing", audit.ProcessingCount.ToString(System.Globalization.CultureInfo.InvariantCulture));
            Line(builder, prefix + ".delivered", audit.DeliveredCount.ToString(System.Globalization.CultureInfo.InvariantCulture));
            Line(builder, prefix + ".deadLetter", audit.DeadLetterCount.ToString(System.Globalization.CultureInfo.InvariantCulture));
            Line(builder, prefix + ".malformed", audit.MalformedCount.ToString(System.Globalization.CultureInfo.InvariantCulture));
            Line(builder, prefix + ".unacknowledged", audit.UnacknowledgedCount.ToString(System.Globalization.CultureInfo.InvariantCulture));
            Line(builder, prefix + ".receipt", audit.CompactedReceiptCount.ToString(System.Globalization.CultureInfo.InvariantCulture));
            Line(builder, prefix + ".hash", auditHashes[audit.AggregateType]);
        }
        return builder.ToString();
    }

    private static string Canonicalize(BsonValue value) => value switch
    {
        BsonDocument document => "{" + string.Join(",", document.Elements
            .OrderBy(element => element.Name, StringComparer.Ordinal)
            .Select(element => new BsonString(element.Name).ToJson() + ":" + Canonicalize(element.Value))) + "}",
        BsonArray array => "[" + string.Join(",", array.Select(Canonicalize)) + "]",
        _ => value.ToJson()
    };

    private static string Sha(string value) => Convert.ToHexString(SHA256.HashData(Encoding.UTF8.GetBytes(value)));
    private static void Append(IncrementalHash hash, string value) => hash.AppendData(Encoding.UTF8.GetBytes(value));
    private static void Line(StringBuilder builder, string key, string value) => builder.Append(key).Append('=').Append(value).Append('\n');

    private FilterDefinition<BsonDocument> ActiveTenantDocumentFilter => new BsonDocument
    {
        { "TenantId", new BsonBinaryData(_tenantId, GuidRepresentation.Standard) },
        { "IsDeleted", false }
    };

    private static readonly string[] MutationCollections =
    [
        "mdm_code_reservations", GlobalProducts, Revisions, Gskus, Lskus, FinishedGoods,
        AbbreviationRegister, AbbreviationLedger, AbbreviationHistory, ScopePolicies, RolloutStates
    ];

    private static readonly (string Category, string Collection)[] DescendantCollections =
    [
        ("ProductDefinitionRevision", Revisions), ("Gsku", Gskus), ("Lsku", Lskus),
        ("FinishedGood", FinishedGoods), ("ProductAbbreviationRegister", AbbreviationRegister),
        ("ProductAbbreviationAllocationLedger", AbbreviationLedger), ("ProductAbbreviationHistory", AbbreviationHistory)
    ];

    private static readonly string[] ImmutableIntentFields =
    [
        "SourceService", "SchemaVersion", "ContractVersion", "IntentId", "TenantId", "AggregateType", "AggregateId",
        "PreVersion", "PostVersion", "Operation", "ActorId", "CorrelationId", "CausationId", "CommandId", "Sequence",
        "TimestampUtc", "TimestampUtcTicksV1", "TemporalStorageVersion", "EvidenceHash", "SnapshotReference", "IdempotencyKey"
    ];

    private async Task<ProductLegalEntityScopeOperationalRolloutFact?> InspectRolloutAsync(
        CancellationToken cancellationToken)
    {
        var tenant = new BsonBinaryData(_tenantId, GuidRepresentation.Standard);
        var documents = await _database.GetCollection<BsonDocument>(RolloutStates)
            .Find(new BsonDocument
            {
                { "TenantId", tenant },
                { "IsDeleted", false }
            })
            .Limit(2)
            .ToListAsync(cancellationToken);
        if (documents.Count > 1)
        {
            throw new InvalidOperationException("PRODUCT_SCOPE_OPERATIONAL_ROLLOUT_STATE_AMBIGUOUS");
        }
        if (documents.Count == 0)
        {
            return null;
        }

        var document = documents[0];
        if (!document.TryGetValue("_id", out var idValue)
            || !idValue.IsGuid
            || idValue.AsGuid == Guid.Empty
            || !document.TryGetValue("Version", out var versionValue)
            || !versionValue.IsInt32
            || versionValue.AsInt32 < 0
            || !document.TryGetValue("CreationCommandId", out var commandValue)
            || !commandValue.IsGuid
            || commandValue.AsGuid == Guid.Empty
            || !document.TryGetValue("CreatedByActorId", out var actorValue)
            || !actorValue.IsGuid
            || actorValue.AsGuid == Guid.Empty
            || !document.TryGetValue("Mode", out var modeValue)
            || !modeValue.IsInt32)
        {
            throw new InvalidOperationException("PRODUCT_SCOPE_OPERATIONAL_ROLLOUT_STATE_INVALID");
        }
        var mode = (ProductLegalEntityScopeRolloutMode)modeValue.AsInt32;
        if (!Enum.IsDefined(mode))
        {
            throw new InvalidOperationException("PRODUCT_SCOPE_OPERATIONAL_ROLLOUT_STATE_INVALID");
        }
        return new(
            idValue.AsGuid,
            mode,
            versionValue.AsInt32,
            commandValue.AsGuid,
            actorValue.AsGuid);
    }

    private async Task<ProductLegalEntityScopeIntegrityCategory> InspectChainAsync(
        string category,
        string sourceCollection,
        IReadOnlyList<(string LocalField, string ParentCollection)> chain,
        int maximumSampleSize,
        CancellationToken cancellationToken)
    {
        var tenant = new BsonBinaryData(_tenantId, GuidRepresentation.Standard);
        var stages = new List<BsonDocument>
        {
            new("$match", ActiveTenantMatch(tenant))
        };
        var currentPath = string.Empty;
        for (var index = 0; index < chain.Count; index++)
        {
            var (localField, parentCollection) = chain[index];
            var parentAlias = $"Parent{index}";
            var localPath = string.IsNullOrEmpty(currentPath) ? $"${localField}" : $"${currentPath}.{localField}";
            stages.Add(new BsonDocument("$lookup", new BsonDocument
            {
                { "from", parentCollection },
                { "let", new BsonDocument("parentId", localPath) },
                { "pipeline", new BsonArray
                    {
                        new BsonDocument("$match", new BsonDocument("$expr", new BsonDocument("$and", new BsonArray
                        {
                            new BsonDocument("$eq", new BsonArray { "$_id", "$$parentId" }),
                            new BsonDocument("$eq", new BsonArray { "$TenantId", tenant }),
                            new BsonDocument("$eq", new BsonArray { "$IsDeleted", false })
                        })))
                    }
                },
                { "as", parentAlias }
            }));
            stages.Add(new BsonDocument("$set", new BsonDocument(parentAlias,
                new BsonDocument("$arrayElemAt", new BsonArray { $"${parentAlias}", 0 }))));
            currentPath = parentAlias;
        }

        var finalParent = $"${currentPath}";
        stages.Add(new BsonDocument("$set", new BsonDocument("IsOrphan",
            new BsonDocument("$eq", new BsonArray
            {
                new BsonDocument("$type", finalParent),
                "missing"
            }))));
        stages.Add(new BsonDocument("$facet", new BsonDocument
        {
            { "summary", new BsonArray
                {
                    new BsonDocument("$group", new BsonDocument
                    {
                        { "_id", BsonNull.Value },
                        { "total", new BsonDocument("$sum", 1) },
                        { "orphans", new BsonDocument("$sum", new BsonDocument("$cond",
                            new BsonArray { "$IsOrphan", 1, 0 })) }
                    })
                }
            },
            { "samples", new BsonArray
                {
                    new BsonDocument("$match", new BsonDocument("IsOrphan", true)),
                    new BsonDocument("$sort", new BsonDocument("_id", 1)),
                    new BsonDocument("$limit", maximumSampleSize),
                    new BsonDocument("$project", new BsonDocument("_id", 1))
                }
            }
        }));

        var result = await _database.GetCollection<BsonDocument>(sourceCollection)
            .Aggregate<BsonDocument>(stages)
            .FirstOrDefaultAsync(cancellationToken);
        var summary = result?["summary"].AsBsonArray.FirstOrDefault()?.AsBsonDocument;
        var total = summary?["total"].ToInt64() ?? 0;
        var orphanCount = summary?["orphans"].ToInt64() ?? 0;
        var orphanIds = result?["samples"].AsBsonArray
            .Select(value => value.AsBsonDocument["_id"].AsGuid)
            .ToArray() ?? [];
        return new(category, total, orphanCount, orphanIds, orphanCount > orphanIds.Length);
    }

    private async Task<ProductLegalEntityScopeAuditCategory> InspectAuditAsync(
        string collectionName,
        AuditAggregateType aggregateType,
        int maximumSampleSize,
        CancellationToken cancellationToken)
    {
        var tenant = new BsonBinaryData(_tenantId, GuidRepresentation.Standard);
        var collection = _database.GetCollection<BsonDocument>(collectionName);
        var intentResult = await collection.Aggregate<BsonDocument>(
            BuildIntentAuditPipeline(tenant, aggregateType, maximumSampleSize).ToArray())
            .FirstOrDefaultAsync(cancellationToken);
        var receiptResult = await collection.Aggregate<BsonDocument>(
            BuildReceiptAuditPipeline(tenant, maximumSampleSize).ToArray())
            .FirstOrDefaultAsync(cancellationToken);

        var intentSummary = intentResult?["summary"].AsBsonArray.FirstOrDefault()?.AsBsonDocument;
        var receiptSummary = receiptResult?["summary"].AsBsonArray.FirstOrDefault()?.AsBsonDocument;
        var malformedIntentCount = intentSummary?["malformed"].ToInt64() ?? 0;
        var malformedReceiptCount = receiptSummary?["malformed"].ToInt64() ?? 0;
        var malformedIntentIds = ReadGuidSamples(intentResult, "malformedSamples");
        var malformedReceiptIds = ReadGuidSamples(receiptResult, "malformedSamples");
        var malformedSamples = malformedIntentIds.Concat(malformedReceiptIds)
            .Distinct()
            .OrderBy(id => id.ToString("D"), StringComparer.Ordinal)
            .Take(maximumSampleSize)
            .ToArray();
        var unacknowledgedCount = intentSummary?["unacknowledged"].ToInt64() ?? 0;
        var unacknowledged = ReadGuidSamples(intentResult, "unacknowledgedSamples");

        return new(
            aggregateType,
            intentSummary?["pending"].ToInt64() ?? 0,
            intentSummary?["processing"].ToInt64() ?? 0,
            intentSummary?["delivered"].ToInt64() ?? 0,
            intentSummary?["deadLetter"].ToInt64() ?? 0,
            receiptSummary?["valid"].ToInt64() ?? 0,
            malformedIntentCount + malformedReceiptCount,
            unacknowledgedCount,
            malformedSamples,
            malformedIntentCount + malformedReceiptCount > malformedSamples.Length,
            unacknowledged,
            unacknowledgedCount > unacknowledged.Count);
    }

    private static IReadOnlyList<BsonDocument> BuildIntentAuditPipeline(
        BsonBinaryData tenant,
        AuditAggregateType aggregateType,
        int maximumSampleSize)
    {
        var validDeliveryStates = new BsonArray(Enum.GetValues<AuditIntentDeliveryState>()
            .Select(value => (BsonValue)(int)value));
        var validOperations = new BsonArray((aggregateType == AuditAggregateType.ProductLegalEntityScopePolicy
                ? new[]
                {
                    ProductAuditOperation.ProductLegalEntityScopePolicyCreated,
                    ProductAuditOperation.ProductLegalEntityScopePolicyReplaced,
                    ProductAuditOperation.ProductLegalEntityScopePolicyEnded
                }
                : new[]
                {
                    ProductAuditOperation.ProductLegalEntityScopeEnforcementActivated,
                    ProductAuditOperation.ProductLegalEntityScopeEnforcementSuspended
                })
            .Select(value => (BsonValue)(int)value));
        var valid = new BsonDocument("$and", new BsonArray
        {
            new BsonDocument("$eq", new BsonArray { "$AuditIntents.TenantId", tenant }),
            new BsonDocument("$eq", new BsonArray { "$AuditIntents.AggregateType", (int)aggregateType }),
            IsNonEmptyGuid("$AuditIntents.AggregateId"),
            new BsonDocument("$eq", new BsonArray { "$AuditIntents.AggregateId", "$_id" }),
            IsNonEmptyGuid("$AuditIntents.IntentId"),
            new BsonDocument("$eq", new BsonArray { "$AuditIntents.SourceService", AuditIntentContract.SourceService }),
            new BsonDocument("$eq", new BsonArray { "$AuditIntents.SchemaVersion", 1 }),
            IsNonNegativeInt("$AuditIntents.PreVersion"),
            IsNonNegativeInt("$AuditIntents.PostVersion"),
            new BsonDocument("$eq", new BsonArray
            {
                IntOrDefault("$AuditIntents.PostVersion"),
                new BsonDocument("$add", new BsonArray { IntOrDefault("$AuditIntents.PreVersion"), 1 })
            }),
            IsLong("$AuditIntents.Sequence"),
            new BsonDocument("$eq", new BsonArray
            {
                LongOrDefault("$AuditIntents.Sequence"),
                new BsonDocument("$add", new BsonArray { IntOrDefault("$AuditIntents.PostVersion"), 1 })
            }),
            new BsonDocument("$in", new BsonArray { "$AuditIntents.Operation", validOperations }),
            new BsonDocument("$in", new BsonArray { "$AuditIntents.DeliveryState", validDeliveryStates }),
            IsCanonicalGuidString("$AuditIntents.ActorId"),
            IsCanonicalGuidString("$AuditIntents.CommandId"),
            IsNonEmptyString("$AuditIntents.CorrelationId"),
            IsNonEmptyString("$AuditIntents.CausationId"),
            IsNonEmptyString("$AuditIntents.EvidenceHash"),
            IsNonEmptyString("$AuditIntents.SnapshotReference"),
            IsNonEmptyString("$AuditIntents.IdempotencyKey"),
            IsCanonicalUtcDateTimeOffset("$AuditIntents.TimestampUtc"),
            IsNonNegativeInt("$AuditIntents.AttemptCount")
        });
        var malformed = Not(valid);
        var unacknowledged = new BsonDocument("$and", new BsonArray
        {
            valid,
            new BsonDocument("$or", new BsonArray
            {
                new BsonDocument("$ne", new BsonArray { "$AuditIntents.DeliveryState", (int)AuditIntentDeliveryState.Delivered }),
                Not(IsNonEmptyString("$AuditIntents.CentralAcknowledgement")),
                Not(IsNonEmptyString("$AuditIntents.CentralIdempotencyKey")),
                Not(IsNonEmptyString("$AuditIntents.AcknowledgedContractVersion"))
            })
        });

        return
        [
            new("$match", new BsonDocument("TenantId", tenant)),
            new("$unwind", "$AuditIntents"),
            new("$set", new BsonDocument
            {
                { "IsMalformed", malformed },
                { "IsUnacknowledged", unacknowledged }
            }),
            new("$facet", new BsonDocument
            {
                { "summary", new BsonArray
                    {
                        new BsonDocument("$group", new BsonDocument
                        {
                            { "_id", BsonNull.Value },
                            { "pending", SumWhenValid("$AuditIntents.DeliveryState", (int)AuditIntentDeliveryState.Pending) },
                            { "processing", SumWhenValid("$AuditIntents.DeliveryState", (int)AuditIntentDeliveryState.Processing) },
                            { "delivered", SumWhenValid("$AuditIntents.DeliveryState", (int)AuditIntentDeliveryState.Delivered) },
                            { "deadLetter", SumWhenValid("$AuditIntents.DeliveryState", (int)AuditIntentDeliveryState.DeadLetter) },
                            { "malformed", new BsonDocument("$sum", new BsonDocument("$cond", new BsonArray { "$IsMalformed", 1, 0 })) },
                            { "unacknowledged", new BsonDocument("$sum", new BsonDocument("$cond", new BsonArray { "$IsUnacknowledged", 1, 0 })) }
                        })
                    }
                },
                { "malformedSamples", SamplePipeline("IsMalformed", "$AuditIntents.IntentId", maximumSampleSize) },
                { "unacknowledgedSamples", SamplePipeline("IsUnacknowledged", "$AuditIntents.IntentId", maximumSampleSize) }
            })
        ];
    }

    private static IReadOnlyList<BsonDocument> BuildReceiptAuditPipeline(
        BsonBinaryData tenant,
        int maximumSampleSize)
    {
        var valid = new BsonDocument("$and", new BsonArray
        {
            new BsonDocument("$eq", new BsonArray { "$AuditIntentReceipts.TenantId", tenant }),
            IsNonEmptyGuid("$AuditIntentReceipts.IntentId"),
            new BsonDocument("$eq", new BsonArray { "$AuditIntentReceipts.SourceService", AuditIntentContract.SourceService }),
            IsNonEmptyString("$AuditIntentReceipts.IdempotencyKey"),
            IsNonEmptyString("$AuditIntentReceipts.CentralAcknowledgement"),
            IsNonEmptyString("$AuditIntentReceipts.CentralIdempotencyKey"),
            IsNonEmptyString("$AuditIntentReceipts.ContractVersion"),
            IsCanonicalUtcDateTimeOffset("$AuditIntentReceipts.AcknowledgedAt"),
            IsCanonicalUtcDateTimeOffset("$AuditIntentReceipts.DeliveredAt"),
            IsCanonicalUtcDateTimeOffset("$AuditIntentReceipts.CompactedAt"),
            new BsonDocument("$lte", new BsonArray
            {
                DateTimeOffsetTicks("$AuditIntentReceipts.AcknowledgedAt"),
                DateTimeOffsetTicks("$AuditIntentReceipts.DeliveredAt")
            }),
            new BsonDocument("$lte", new BsonArray
            {
                DateTimeOffsetTicks("$AuditIntentReceipts.DeliveredAt"),
                DateTimeOffsetTicks("$AuditIntentReceipts.CompactedAt")
            }),
            IsNonEmptyString("$AuditIntentReceipts.CompactReceiptReference"),
            IsNonEmptyString("$AuditIntentReceipts.EvidenceHash")
        });
        var malformed = Not(valid);
        return
        [
            new("$match", new BsonDocument("TenantId", tenant)),
            new("$unwind", "$AuditIntentReceipts"),
            new("$set", new BsonDocument("IsMalformed", malformed)),
            new("$facet", new BsonDocument
            {
                { "summary", new BsonArray
                    {
                        new BsonDocument("$group", new BsonDocument
                        {
                            { "_id", BsonNull.Value },
                            { "valid", new BsonDocument("$sum", new BsonDocument("$cond", new BsonArray { Not("$IsMalformed"), 1, 0 })) },
                            { "malformed", new BsonDocument("$sum", new BsonDocument("$cond", new BsonArray { "$IsMalformed", 1, 0 })) }
                        })
                    }
                },
                { "malformedSamples", SamplePipeline("IsMalformed", "$AuditIntentReceipts.IntentId", maximumSampleSize) }
            })
        ];
    }

    private static BsonDocument ActiveTenantMatch(BsonBinaryData tenant) => new()
    {
        { "TenantId", tenant },
        { "IsDeleted", false }
    };

    private static BsonDocument SumWhen(string field, int value)
        => new("$sum", new BsonDocument("$cond", new BsonArray
        {
            new BsonDocument("$eq", new BsonArray { field, value }),
            1,
            0
        }));

    private static BsonDocument SumWhenValid(string field, int value)
        => new("$sum", new BsonDocument("$cond", new BsonArray
        {
            new BsonDocument("$and", new BsonArray
            {
                Not("$IsMalformed"),
                new BsonDocument("$eq", new BsonArray { field, value })
            }),
            1,
            0
        }));

    private static BsonDocument TypeIs(BsonValue expression, string type)
        => new("$eq", new BsonArray { new BsonDocument("$type", expression), type });

    private static BsonDocument Not(BsonValue expression)
        => new("$not", new BsonArray { expression });

    private static BsonDocument IsNonEmptyGuid(BsonValue expression)
        => new("$and", new BsonArray
        {
            TypeIs(expression, "binData"),
            new BsonDocument("$ne", new BsonArray
            {
                expression,
                new BsonBinaryData(Guid.Empty, GuidRepresentation.Standard)
            })
        });

    private static BsonDocument IsNonEmptyString(BsonValue expression)
    {
        var safe = new BsonDocument("$cond", new BsonArray { TypeIs(expression, "string"), expression, "" });
        return new BsonDocument("$and", new BsonArray
        {
            TypeIs(expression, "string"),
            new BsonDocument("$gt", new BsonArray { new BsonDocument("$strLenCP", safe), 0 })
        });
    }

    private static BsonDocument IsCanonicalGuidString(BsonValue expression)
    {
        var safe = new BsonDocument("$cond", new BsonArray { TypeIs(expression, "string"), expression, "" });
        return new BsonDocument("$and", new BsonArray
        {
            TypeIs(expression, "string"),
            new BsonDocument("$regexMatch", new BsonDocument
            {
                { "input", safe },
                { "regex", "^[0-9A-Fa-f]{8}-[0-9A-Fa-f]{4}-[0-9A-Fa-f]{4}-[0-9A-Fa-f]{4}-[0-9A-Fa-f]{12}$" }
            })
        });
    }

    private static BsonDocument IsNonNegativeInt(BsonValue expression)
        => new("$and", new BsonArray
        {
            TypeIs(expression, "int"),
            new BsonDocument("$gte", new BsonArray { IntOrDefault(expression), 0 })
        });

    private static BsonDocument IsLong(BsonValue expression) => TypeIs(expression, "long");

    private static BsonDocument IntOrDefault(BsonValue expression)
        => new("$cond", new BsonArray { TypeIs(expression, "int"), expression, -1 });

    private static BsonDocument LongOrDefault(BsonValue expression)
        => new("$cond", new BsonArray { TypeIs(expression, "long"), expression, -1L });

    private static BsonDocument IsCanonicalUtcDateTimeOffset(BsonValue expression)
    {
        var safe = ArrayOrEmpty(expression);
        return new BsonDocument("$and", new BsonArray
        {
            TypeIs(expression, "array"),
            new BsonDocument("$eq", new BsonArray { new BsonDocument("$size", safe), 2 }),
            TypeIs(new BsonDocument("$arrayElemAt", new BsonArray { safe, 0 }), "long"),
            TypeIs(new BsonDocument("$arrayElemAt", new BsonArray { safe, 1 }), "int"),
            new BsonDocument("$gt", new BsonArray
            {
                new BsonDocument("$arrayElemAt", new BsonArray { safe, 0 }),
                0L
            }),
            new BsonDocument("$eq", new BsonArray
            {
                new BsonDocument("$arrayElemAt", new BsonArray { safe, 1 }),
                0
            })
        });
    }

    private static BsonDocument DateTimeOffsetTicks(BsonValue expression)
        => new("$arrayElemAt", new BsonArray { ArrayOrEmpty(expression), 0 });

    private static BsonDocument ArrayOrEmpty(BsonValue expression)
        => new("$cond", new BsonArray { TypeIs(expression, "array"), expression, new BsonArray() });

    private static BsonArray SamplePipeline(string flag, string identity, int maximumSampleSize)
        => new()
        {
            new BsonDocument("$match", new BsonDocument(flag, true)),
            new BsonDocument("$sort", new BsonDocument(identity[1..], 1)),
            new BsonDocument("$limit", maximumSampleSize),
            new BsonDocument("$project", new BsonDocument("_id", identity))
        };

    private static IReadOnlyList<Guid> ReadGuidSamples(BsonDocument? result, string field)
        => result?[field].AsBsonArray
            .Select(value => value.AsBsonDocument["_id"])
            .Where(value => value.IsGuid)
            .Select(value => value.AsGuid)
            .ToArray() ?? [];
}
