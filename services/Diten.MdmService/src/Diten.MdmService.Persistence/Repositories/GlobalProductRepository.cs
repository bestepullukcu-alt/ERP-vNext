using Diten.MdmService.Application.Common;
using Diten.MdmService.Domain.Entities;
using Diten.MdmService.Domain.Enums;
using Diten.MdmService.Domain.Repositories;
using MongoDB.Bson;
using MongoDB.Bson.Serialization;
using MongoDB.Driver;

namespace Diten.MdmService.Persistence.Repositories;

public sealed class GlobalProductRepository : IGlobalProductRepository
{
    private readonly IMongoCollection<GlobalProduct> _globalProducts;
    private readonly IMongoCollection<CodeReservation> _reservations;
    private readonly IMongoCollection<BsonDocument> _globalProductDocuments;
    private readonly Guid _tenantId;

    public GlobalProductRepository(IMongoDatabase database, ITenantContext tenantContext)
    {
        _globalProducts = database.GetCollection<GlobalProduct>("mdm_global_products");
        _reservations = database.GetCollection<CodeReservation>("mdm_code_reservations");
        _globalProductDocuments = database.GetCollection<BsonDocument>("mdm_global_products");
        _tenantId = tenantContext.TenantId;
        EnsureIndexes();
    }

    public async Task<GlobalProduct?> GetByIdAsync(Guid id, CancellationToken cancellationToken = default)
        => await _globalProducts.Find(ActiveTenantFilter & Builders<GlobalProduct>.Filter.Eq(x => x.Id, id))
            .FirstOrDefaultAsync(cancellationToken);

    public async Task<IReadOnlyList<GlobalProduct>> GetByIdsAsync(
        IReadOnlyCollection<Guid> ids,
        CancellationToken cancellationToken = default)
    {
        if (ids.Count == 0)
        {
            return [];
        }

        return await _globalProducts.Find(
                ActiveTenantFilter & Builders<GlobalProduct>.Filter.In(x => x.Id, ids))
            .ToListAsync(cancellationToken);
    }

    public async Task<GlobalProduct?> GetByReservationIdAsync(
        Guid reservationId,
        CancellationToken cancellationToken = default)
        => await _globalProducts.Find(
                ActiveTenantFilter & Builders<GlobalProduct>.Filter.Eq(x => x.CodeReservationId, reservationId))
            .FirstOrDefaultAsync(cancellationToken);

    public async Task<bool> NameExistsAsync(
        string normalizedName,
        CancellationToken cancellationToken = default)
        => await _globalProducts.Find(
                TenantIncludingDeletedFilter
                & Builders<GlobalProduct>.Filter.Eq(x => x.GlobalProductNameNormalized, normalizedName))
            .AnyAsync(cancellationToken);

    public async Task<GlobalProductPage> GetPageAsync(
        int pageNumber,
        int pageSize,
        string? normalizedSearch,
        ProductIdentityLifecycleStatus? lifecycleStatus,
        CancellationToken cancellationToken = default)
    {
        var filter = ActiveTenantFilter;
        if (!string.IsNullOrWhiteSpace(normalizedSearch))
        {
            var escapedSearch = RegexEscape(normalizedSearch);
            filter &= Builders<GlobalProduct>.Filter.Or(
                Builders<GlobalProduct>.Filter.Regex(
                    x => x.GlobalProductNameNormalized,
                    new BsonRegularExpression("^" + escapedSearch)),
                Builders<GlobalProduct>.Filter.Regex(
                    x => x.CanonicalCode,
                    new BsonRegularExpression("^" + escapedSearch)));
        }

        if (lifecycleStatus.HasValue)
        {
            filter &= Builders<GlobalProduct>.Filter.Eq(x => x.LifecycleStatus, lifecycleStatus.Value);
        }

        var totalCount = await _globalProducts.CountDocumentsAsync(filter, cancellationToken: cancellationToken);
        var items = await _globalProducts.Find(filter)
            .SortBy(x => x.GlobalProductNameNormalized)
            .ThenBy(x => x.CanonicalCode)
            .ThenBy(x => x.Id)
            .Skip((pageNumber - 1) * pageSize)
            .Limit(pageSize)
            .ToListAsync(cancellationToken);

        return new GlobalProductPage(items, totalCount);
    }

    public async Task<GlobalProductPage> GetReferenceablePageAsync(
        int pageNumber,
        int pageSize,
        string? normalizedSearch,
        CancellationToken cancellationToken = default)
    {
        var filter = ActiveTenantFilter & Builders<GlobalProduct>.Filter.In(
            x => x.LifecycleStatus,
            new[] { ProductIdentityLifecycleStatus.Draft, ProductIdentityLifecycleStatus.IdentityApproved });
        if (!string.IsNullOrWhiteSpace(normalizedSearch))
        {
            var escapedSearch = RegexEscape(normalizedSearch);
            filter &= Builders<GlobalProduct>.Filter.Or(
                Builders<GlobalProduct>.Filter.Regex(
                    x => x.GlobalProductNameNormalized,
                    new BsonRegularExpression("^" + escapedSearch)),
                Builders<GlobalProduct>.Filter.Regex(
                    x => x.CanonicalCode,
                    new BsonRegularExpression("^" + escapedSearch)));
        }

        var totalCount = await _globalProducts.CountDocumentsAsync(filter, cancellationToken: cancellationToken);
        var items = await _globalProducts.Find(filter)
            .SortBy(x => x.GlobalProductNameNormalized)
            .ThenBy(x => x.CanonicalCode)
            .ThenBy(x => x.Id)
            .Skip((pageNumber - 1) * pageSize)
            .Limit(pageSize)
            .ToListAsync(cancellationToken);
        return new(items, totalCount);
    }

    public async Task<GlobalProductPage> GetEnforcedLegalEntityScopePageAsync(
        int pageNumber,
        int pageSize,
        string? normalizedSearch,
        ProductIdentityLifecycleStatus? lifecycleStatus,
        bool referenceableOnly,
        IReadOnlyCollection<Guid> effectiveCandidateLegalEntityIds,
        DateTimeOffset serverNowUtc,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(effectiveCandidateLegalEntityIds);
        cancellationToken.ThrowIfCancellationRequested();
        if (pageNumber < 1 || pageSize < 1 || pageSize > 100)
        {
            throw new ArgumentOutOfRangeException(nameof(pageSize));
        }
        if (serverNowUtc.Offset != TimeSpan.Zero)
        {
            throw new ArgumentException("Server time must be UTC.", nameof(serverNowUtc));
        }
        if (effectiveCandidateLegalEntityIds.Count is 0)
        {
            return new([], 0);
        }
        if (effectiveCandidateLegalEntityIds.Count > ProductLegalEntityScopePolicy.MaximumLegalEntityIdsPerSnapshot
            || effectiveCandidateLegalEntityIds.Any(id => id == Guid.Empty)
            || effectiveCandidateLegalEntityIds.Distinct().Count() != effectiveCandidateLegalEntityIds.Count)
        {
            throw new ArgumentException(
                "Effective Legal Entity candidates must be bounded, non-empty and unique.",
                nameof(effectiveCandidateLegalEntityIds));
        }

        var productMatch = new BsonDocument
        {
            { nameof(GlobalProduct.TenantId), GuidBson(_tenantId) },
            { nameof(GlobalProduct.IsDeleted), false }
        };
        if (!string.IsNullOrWhiteSpace(normalizedSearch))
        {
            var escapedSearch = RegexEscape(normalizedSearch);
            productMatch["$or"] = new BsonArray
            {
                new BsonDocument(nameof(GlobalProduct.GlobalProductNameNormalized),
                    new BsonRegularExpression("^" + escapedSearch)),
                new BsonDocument(nameof(GlobalProduct.CanonicalCode),
                    new BsonRegularExpression("^" + escapedSearch))
            };
        }
        if (lifecycleStatus.HasValue)
        {
            productMatch[nameof(GlobalProduct.LifecycleStatus)] = (int)lifecycleStatus.Value;
        }
        else if (referenceableOnly)
        {
            productMatch[nameof(GlobalProduct.LifecycleStatus)] = new BsonDocument(
                "$in",
                new BsonArray
                {
                    (int)ProductIdentityLifecycleStatus.Draft,
                    (int)ProductIdentityLifecycleStatus.IdentityApproved
                });
        }

        var pipeline = new List<BsonDocument>
        {
            new("$match", productMatch)
        };
        pipeline.AddRange(ProductLegalEntityScopeAggregation.CreatePolicyAccessStages(
            _tenantId,
            effectiveCandidateLegalEntityIds,
            serverNowUtc,
            "$_id",
            requireGlobalProductLookup: false));
        pipeline.Add(ProductLegalEntityScopeAggregation.CleanupStage());
        pipeline.Add(new BsonDocument("$sort", new BsonDocument
        {
            { nameof(GlobalProduct.GlobalProductNameNormalized), 1 },
            { nameof(GlobalProduct.CanonicalCode), 1 },
            { "_id", 1 }
        }));
        pipeline.Add(new BsonDocument("$facet", new BsonDocument
        {
            { "items", new BsonArray
                {
                    new BsonDocument("$skip", (pageNumber - 1) * pageSize),
                    new BsonDocument("$limit", pageSize)
                }
            },
            { "summary", new BsonArray
                {
                    new BsonDocument("$count", "total")
                }
            }
        }));

        var result = await _globalProductDocuments.Aggregate<BsonDocument>(pipeline)
            .FirstOrDefaultAsync(cancellationToken);
        var items = result?["items"].AsBsonArray
            .Select(item => BsonSerializer.Deserialize<GlobalProduct>(item.AsBsonDocument))
            .ToArray() ?? [];
        var totalCount = result?["summary"].AsBsonArray
            .FirstOrDefault()?.AsBsonDocument["total"].ToInt64() ?? 0;
        return new(items, totalCount);
    }

    public async Task<GlobalProductCreateResult> CreateDraftAsync(
        GlobalProduct globalProduct,
        CancellationToken cancellationToken = default)
    {
        var existing = await GetByReservationIdAsync(globalProduct.CodeReservationId, cancellationToken);
        if (existing is not null)
        {
            return existing.Id == globalProduct.Id
                   && existing.CanonicalCode == globalProduct.CanonicalCode
                   && existing.GlobalProductNameNormalized == globalProduct.GlobalProductNameNormalized
                ? new(true, existing)
                : new(false, existing, "CODE_RESERVATION_MISMATCH");
        }

        if (await NameExistsAsync(globalProduct.GlobalProductNameNormalized, cancellationToken))
        {
            return new(false, null, "GLOBAL_PRODUCT_NAME_DUPLICATE");
        }

        var reservationFilter = Builders<CodeReservation>.Filter.Eq(x => x.TenantId, _tenantId)
            & Builders<CodeReservation>.Filter.Eq(x => x.IsDeleted, false)
            & Builders<CodeReservation>.Filter.Eq(x => x.Id, globalProduct.CodeReservationId)
            & Builders<CodeReservation>.Filter.Eq(x => x.EntityType, CodeBearingEntityType.GlobalProduct)
            & Builders<CodeReservation>.Filter.Eq(x => x.ReservationState, CodeReservationState.Consumed)
            & Builders<CodeReservation>.Filter.Eq(x => x.ConsumedEntityId, globalProduct.Id)
            & Builders<CodeReservation>.Filter.Eq(x => x.ReservedCode, globalProduct.CanonicalCode)
            & Builders<CodeReservation>.Filter.In(
                x => x.BindingState,
                new[] { CodeReservationBindingState.PendingIdentityWrite, CodeReservationBindingState.Confirmed });
        var reservationExists = await _reservations.Find(reservationFilter).AnyAsync(cancellationToken);
        if (!reservationExists)
        {
            return new(false, null, "CODE_RESERVATION_REQUIRED");
        }

        if (globalProduct.AuditIntents.Count is 0 or > AuditIntentLimits.MaxPerAggregate
            || globalProduct.AuditIntents.Any(intent => intent.TenantId != _tenantId))
        {
            return new(false, null, "AUDIT_INTENT_CONTRACT_INVALID");
        }

        globalProduct.TenantId = _tenantId;
        globalProduct.CreatedAt = DateTimeOffset.UtcNow;
        globalProduct.UpdatedAt = globalProduct.CreatedAt;
        globalProduct.IsDeleted = false;
        globalProduct.Version = 0;

        try
        {
            await _globalProducts.InsertOneAsync(globalProduct, cancellationToken: cancellationToken);
            return new(true, globalProduct);
        }
        catch (MongoWriteException exception) when (exception.WriteError?.Category == ServerErrorCategory.DuplicateKey)
        {
            var sameName = await _globalProducts.Find(
                    TenantIncludingDeletedFilter
                    & Builders<GlobalProduct>.Filter.Eq(
                        x => x.GlobalProductNameNormalized,
                        globalProduct.GlobalProductNameNormalized))
                .FirstOrDefaultAsync(cancellationToken);
            if (sameName is not null)
            {
                if (sameName.Id == globalProduct.Id
                    && sameName.CodeReservationId == globalProduct.CodeReservationId
                    && sameName.CanonicalCode == globalProduct.CanonicalCode)
                {
                    return new(true, sameName);
                }

                return new(false, null, "GLOBAL_PRODUCT_NAME_DUPLICATE");
            }

            existing = await GetByReservationIdAsync(globalProduct.CodeReservationId, cancellationToken)
                ?? await GetByIdAsync(globalProduct.Id, cancellationToken);
            if (existing is not null
                && existing.Id == globalProduct.Id
                && existing.CodeReservationId == globalProduct.CodeReservationId
                && existing.CanonicalCode == globalProduct.CanonicalCode)
            {
                return new(true, existing);
            }

            throw new InvalidOperationException("GLOBAL_PRODUCT_DUPLICATE_CONFLICT", exception);
        }
    }

    public async Task<GlobalProductScopeCompletenessInventory> GetProductLegalEntityScopeCompletenessInventoryAsync(
        DateTimeOffset serverNowUtc,
        int maximumMissingItems,
        CancellationToken cancellationToken = default)
    {
        cancellationToken.ThrowIfCancellationRequested();
        if (serverNowUtc.Offset != TimeSpan.Zero)
        {
            throw new ArgumentException("Server time must be UTC.", nameof(serverNowUtc));
        }
        if (maximumMissingItems is < 1 or > ProductLegalEntityScopePolicy.MaximumLegalEntityIdsPerSnapshot)
        {
            throw new ArgumentOutOfRangeException(nameof(maximumMissingItems));
        }

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
            { "cond", ProductLegalEntityScopeAggregation.CreateCurrentPeriodExpression(
                serverNowUtc,
                "$$period") }
        });
        var isConfigured = new BsonDocument("$and", new BsonArray
        {
            new BsonDocument("$eq", new BsonArray
            {
                new BsonDocument("$size", "$ScopePolicies"), 1
            }),
            new BsonDocument("$let", new BsonDocument
            {
                { "vars", new BsonDocument("policy", policy) },
                { "in", new BsonDocument("$and", new BsonArray
                    {
                        ProductLegalEntityScopeAggregation.CreatePolicyValidityExpression(serverNowUtc),
                        new BsonDocument("$eq", new BsonArray
                        {
                            new BsonDocument("$size", currentPeriods), 1
                        })
                    })
                }
            })
        });
        var pipeline = new List<BsonDocument>
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
            new BsonDocument("$facet", new BsonDocument
            {
                { "summary", new BsonArray
                    {
                        new BsonDocument("$group", new BsonDocument
                        {
                            { "_id", BsonNull.Value },
                            { "eligible", new BsonDocument("$sum", 1) },
                            { "configured", new BsonDocument("$sum", new BsonDocument(
                                "$cond", new BsonArray { "$IsConfigured", 1, 0 })) }
                        })
                    }
                },
                { "missing", new BsonArray
                    {
                        new BsonDocument("$match", new BsonDocument("IsConfigured", false)),
                        new BsonDocument("$sort", new BsonDocument("_id", 1)),
                        new BsonDocument("$limit", maximumMissingItems),
                        new BsonDocument("$project", new BsonDocument("_id", 1))
                    }
                }
            })
        };

        var result = await _globalProductDocuments.Aggregate<BsonDocument>(pipeline)
            .FirstOrDefaultAsync(cancellationToken);
        var summary = result?["summary"].AsBsonArray.FirstOrDefault()?.AsBsonDocument;
        var eligible = summary?["eligible"].ToInt64() ?? 0;
        var configured = summary?["configured"].ToInt64() ?? 0;
        var missing = result?["missing"].AsBsonArray
            .Select(item => item.AsBsonDocument["_id"].AsGuid)
            .ToArray() ?? [];
        return new GlobalProductScopeCompletenessInventory(eligible, configured, missing);
    }

    private void EnsureIndexes()
    {
        var models = new[]
        {
            new CreateIndexModel<GlobalProduct>(
                Builders<GlobalProduct>.IndexKeys.Ascending(x => x.TenantId).Ascending(x => x.CanonicalCode),
                new CreateIndexOptions { Unique = true, Name = "ux_mdm_global_products_tenant_code" }),
            new CreateIndexModel<GlobalProduct>(
                Builders<GlobalProduct>.IndexKeys.Ascending(x => x.TenantId).Ascending(x => x.CodeReservationId),
                new CreateIndexOptions { Unique = true, Name = "ux_mdm_global_products_tenant_reservation" }),
            new CreateIndexModel<GlobalProduct>(
                Builders<GlobalProduct>.IndexKeys
                    .Ascending(x => x.TenantId)
                    .Ascending(x => x.GlobalProductNameNormalized),
                new CreateIndexOptions { Unique = true, Name = "ux_mdm_global_products_tenant_normalized_name" })
        };
        _globalProducts.Indexes.CreateMany(models);
    }

    private FilterDefinition<GlobalProduct> ActiveTenantFilter =>
        TenantIncludingDeletedFilter
        & Builders<GlobalProduct>.Filter.Eq(x => x.IsDeleted, false);

    private FilterDefinition<GlobalProduct> TenantIncludingDeletedFilter =>
        Builders<GlobalProduct>.Filter.Eq(x => x.TenantId, _tenantId);

    private static string RegexEscape(string value)
        => System.Text.RegularExpressions.Regex.Escape(value);

    private static BsonBinaryData GuidBson(Guid value)
        => new(value, GuidRepresentation.Standard);
}
