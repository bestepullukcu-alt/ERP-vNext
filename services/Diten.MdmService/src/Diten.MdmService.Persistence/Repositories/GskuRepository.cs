using Diten.MdmService.Application.Common;
using Diten.MdmService.Domain.Entities;
using Diten.MdmService.Domain.Enums;
using Diten.MdmService.Domain.Repositories;
using Diten.MdmService.Domain.ValueObjects;
using MongoDB.Bson;
using MongoDB.Bson.Serialization;
using MongoDB.Driver;

namespace Diten.MdmService.Persistence.Repositories;

public sealed class GskuRepository : IGskuRepository
{
    private readonly IMongoCollection<Gsku> _gskus;
    private readonly IMongoCollection<BsonDocument> _documents;
    private readonly IMongoCollection<CodeReservation> _reservations;
    private readonly IMongoCollection<ProductDefinitionRevision> _revisions;
    private readonly IMongoCollection<Lsku> _lskus;
    private readonly IMongoCollection<FinishedGood> _finishedGoods;
    private readonly Guid _tenantId;

    public GskuRepository(IMongoDatabase database, ITenantContext tenantContext)
    {
        _gskus = database.GetCollection<Gsku>("mdm_gskus");
        _documents = database.GetCollection<BsonDocument>("mdm_gskus");
        _reservations = database.GetCollection<CodeReservation>("mdm_code_reservations");
        _revisions = database.GetCollection<ProductDefinitionRevision>("mdm_product_definition_revisions");
        _lskus = database.GetCollection<Lsku>("mdm_lskus");
        _finishedGoods = database.GetCollection<FinishedGood>("mdm_finished_goods");
        _tenantId = tenantContext.TenantId;
        EnsureIndexes();
    }

    public Task<Gsku?> GetByIdAsync(Guid id, CancellationToken cancellationToken = default)
        => _gskus.Find(ActiveFilter & Builders<Gsku>.Filter.Eq(x => x.Id, id)).FirstOrDefaultAsync(cancellationToken);

    public async Task<Gsku?> GetReferenceableByIdAsync(Guid id, CancellationToken cancellationToken = default)
    {
        var gsku = await _gskus.Find(ReferenceableFilter & Builders<Gsku>.Filter.Eq(x => x.Id, id))
            .FirstOrDefaultAsync(cancellationToken);
        if (gsku is null) return null;
        var revisionApproved = await _revisions.Find(
                Builders<ProductDefinitionRevision>.Filter.Eq(x => x.TenantId, _tenantId)
                & Builders<ProductDefinitionRevision>.Filter.Eq(x => x.IsDeleted, false)
                & Builders<ProductDefinitionRevision>.Filter.Eq(x => x.Id, gsku.ProductDefinitionRevisionId)
                & Builders<ProductDefinitionRevision>.Filter.Eq(
                    x => x.LifecycleStatus, ProductIdentityLifecycleStatus.IdentityApproved))
            .AnyAsync(cancellationToken);
        return revisionApproved ? gsku : null;
    }

    public async Task<IReadOnlyList<Gsku>> GetByIdsAsync(
        IReadOnlyCollection<Guid> ids,
        CancellationToken cancellationToken = default)
    {
        if (ids.Count == 0)
        {
            return [];
        }

        return await _gskus.Find(ActiveFilter & Builders<Gsku>.Filter.In(x => x.Id, ids))
            .ToListAsync(cancellationToken);
    }

    public async Task<GskuPage> GetReferenceablePageAsync(
        int pageNumber,
        int pageSize,
        string? canonicalCodeSearch,
        CancellationToken cancellationToken = default)
    {
        var match = new BsonDocument
        {
            { nameof(Gsku.TenantId), ProductLegalEntityScopeAggregation.GuidBson(_tenantId) },
            { nameof(Gsku.IsDeleted), false },
            { nameof(Gsku.LifecycleStatus), (int)ProductIdentityLifecycleStatus.IdentityApproved }
        };
        if (!string.IsNullOrWhiteSpace(canonicalCodeSearch))
        {
            match[nameof(Gsku.CanonicalCode)] = new BsonRegularExpression(
                "^" + System.Text.RegularExpressions.Regex.Escape(canonicalCodeSearch));
        }

        var pipeline = new[]
        {
            new BsonDocument("$match", match),
            new BsonDocument("$lookup", new BsonDocument
            {
                { "from", "mdm_product_definition_revisions" },
                { "let", new BsonDocument("revisionId", "$" + nameof(Gsku.ProductDefinitionRevisionId)) },
                { "pipeline", new BsonArray
                    {
                        new BsonDocument("$match", new BsonDocument("$expr", new BsonDocument("$and", new BsonArray
                        {
                            new BsonDocument("$eq", new BsonArray
                            {
                                "$" + nameof(ProductDefinitionRevision.TenantId),
                                ProductLegalEntityScopeAggregation.GuidBson(_tenantId)
                            }),
                            new BsonDocument("$eq", new BsonArray
                            {
                                "$" + nameof(ProductDefinitionRevision.IsDeleted), false
                            }),
                            new BsonDocument("$eq", new BsonArray { "$_id", "$$revisionId" }),
                            new BsonDocument("$eq", new BsonArray
                            {
                                "$" + nameof(ProductDefinitionRevision.LifecycleStatus),
                                (int)ProductIdentityLifecycleStatus.IdentityApproved
                            })
                        })))
                    }
                },
                { "as", "ReferenceableRevision" }
            }),
            new BsonDocument("$match", new BsonDocument("$expr", new BsonDocument("$eq", new BsonArray
            {
                new BsonDocument("$size", "$ReferenceableRevision"), 1
            }))),
            new BsonDocument("$unset", "ReferenceableRevision"),
            new BsonDocument("$sort", new BsonDocument
            {
                { nameof(Gsku.CanonicalCode), 1 },
                { "_id", 1 }
            }),
            new BsonDocument("$facet", new BsonDocument
            {
                { "items", new BsonArray
                    {
                        new BsonDocument("$skip", (pageNumber - 1) * pageSize),
                        new BsonDocument("$limit", pageSize)
                    }
                },
                { "summary", new BsonArray { new BsonDocument("$count", "total") } }
            })
        };

        var result = await _documents.Aggregate<BsonDocument>(pipeline).FirstOrDefaultAsync(cancellationToken);
        var items = result?["items"].AsBsonArray
            .Select(item => BsonSerializer.Deserialize<Gsku>(item.AsBsonDocument))
            .ToArray() ?? [];
        var total = result?["summary"].AsBsonArray.FirstOrDefault()?.AsBsonDocument["total"].ToInt64() ?? 0;
        return new(items, total);
    }

    public async Task<GskuPage> GetEnforcedLegalEntityScopePageAsync(
        int pageNumber,
        int pageSize,
        string? canonicalCodeSearch,
        bool referenceableOnly,
        IReadOnlyCollection<Guid> effectiveCandidateLegalEntityIds,
        DateTimeOffset serverNowUtc,
        CancellationToken cancellationToken = default)
        => await GetEnforcedLegalEntityScopePageAsync(pageNumber, pageSize, canonicalCodeSearch,
            referenceableOnly, lifecycleStatus: null, effectiveCandidateLegalEntityIds, serverNowUtc,
            cancellationToken);

    public async Task<GskuPage> GetEnforcedLegalEntityScopePageAsync(
        int pageNumber,
        int pageSize,
        string? canonicalCodeSearch,
        bool referenceableOnly,
        ProductIdentityLifecycleStatus? lifecycleStatus,
        IReadOnlyCollection<Guid> effectiveCandidateLegalEntityIds,
        DateTimeOffset serverNowUtc,
        CancellationToken cancellationToken = default)
    {
        cancellationToken.ThrowIfCancellationRequested();
        var match = new BsonDocument
        {
            { nameof(Gsku.TenantId), ProductLegalEntityScopeAggregation.GuidBson(_tenantId) },
            { nameof(Gsku.IsDeleted), false }
        };
        if (referenceableOnly)
        {
            match[nameof(Gsku.LifecycleStatus)] = new BsonDocument("$in", new BsonArray
            {
                (int)ProductIdentityLifecycleStatus.IdentityApproved
            });
        }
        else if (lifecycleStatus.HasValue)
        {
            match[nameof(Gsku.LifecycleStatus)] = (int)lifecycleStatus.Value;
        }
        if (!string.IsNullOrWhiteSpace(canonicalCodeSearch))
        {
            match[nameof(Gsku.CanonicalCode)] = new BsonRegularExpression(
                "^" + System.Text.RegularExpressions.Regex.Escape(canonicalCodeSearch));
        }

        var revisionPredicates = new BsonArray
        {
            new BsonDocument("$eq", new BsonArray
            {
                "$" + nameof(ProductDefinitionRevision.TenantId),
                ProductLegalEntityScopeAggregation.GuidBson(_tenantId)
            }),
            new BsonDocument("$eq", new BsonArray
            {
                "$" + nameof(ProductDefinitionRevision.IsDeleted), false
            }),
            new BsonDocument("$eq", new BsonArray { "$_id", "$$revisionId" })
        };
        if (referenceableOnly)
        {
            revisionPredicates.Add(new BsonDocument("$eq", new BsonArray
            {
                "$" + nameof(ProductDefinitionRevision.LifecycleStatus),
                (int)ProductIdentityLifecycleStatus.IdentityApproved
            }));
        }

        var pipeline = new List<BsonDocument>
        {
            new("$match", match),
            new("$lookup", new BsonDocument
            {
                { "from", "mdm_product_definition_revisions" },
                { "let", new BsonDocument("revisionId", "$" + nameof(Gsku.ProductDefinitionRevisionId)) },
                { "pipeline", new BsonArray
                    {
                        new BsonDocument("$match", new BsonDocument("$expr", new BsonDocument("$and", revisionPredicates)))
                    }
                },
                { "as", "ScopeRevisions" }
            }),
            new("$match", new BsonDocument("$expr", new BsonDocument("$eq", new BsonArray
            {
                new BsonDocument("$size", "$ScopeRevisions"),
                1
            }))),
            new("$set", new BsonDocument(
                ProductLegalEntityScopeAggregation.ResolvedGlobalProductIdField,
                new BsonDocument("$getField", new BsonDocument
                {
                    { "field", nameof(ProductDefinitionRevision.GlobalProductId) },
                    { "input", new BsonDocument("$arrayElemAt", new BsonArray { "$ScopeRevisions", 0 }) }
                })))
        };
        pipeline.AddRange(ProductLegalEntityScopeAggregation.CreateAccessStages(
            _tenantId,
            effectiveCandidateLegalEntityIds,
            serverNowUtc));
        pipeline.Add(ProductLegalEntityScopeAggregation.CleanupStage("ScopeRevisions"));
        pipeline.Add(new BsonDocument("$sort", new BsonDocument
        {
            { nameof(Gsku.CanonicalCode), 1 },
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
            { "summary", new BsonArray { new BsonDocument("$count", "total") } }
        }));

        var result = await _documents.Aggregate<BsonDocument>(pipeline).FirstOrDefaultAsync(cancellationToken);
        var items = result?["items"].AsBsonArray
            .Select(item => BsonSerializer.Deserialize<Gsku>(item.AsBsonDocument))
            .ToArray() ?? [];
        var total = result?["summary"].AsBsonArray.FirstOrDefault()?.AsBsonDocument["total"].ToInt64() ?? 0;
        return new(items, total);
    }

    public async Task<GskuPage> GetPageAsync(
        int pageNumber,
        int pageSize,
        string? canonicalCodeSearch,
        CancellationToken cancellationToken = default)
        => await GetPageAsync(pageNumber, pageSize, canonicalCodeSearch, lifecycleStatus: null, cancellationToken);

    public async Task<GskuPage> GetPageAsync(
        int pageNumber,
        int pageSize,
        string? canonicalCodeSearch,
        ProductIdentityLifecycleStatus? lifecycleStatus,
        CancellationToken cancellationToken = default)
    {
        var filter = ActiveFilter;
        if (lifecycleStatus.HasValue)
        {
            filter &= Builders<Gsku>.Filter.Eq(x => x.LifecycleStatus, lifecycleStatus.Value);
        }
        if (!string.IsNullOrWhiteSpace(canonicalCodeSearch))
        {
            filter &= Builders<Gsku>.Filter.Regex(
                x => x.CanonicalCode,
                new BsonRegularExpression("^" + System.Text.RegularExpressions.Regex.Escape(canonicalCodeSearch)));
        }

        var totalCount = await _gskus.CountDocumentsAsync(filter, cancellationToken: cancellationToken);
        var items = await _gskus.Find(filter)
            .SortBy(x => x.CanonicalCode)
            .ThenBy(x => x.Id)
            .Skip((pageNumber - 1) * pageSize)
            .Limit(pageSize)
            .ToListAsync(cancellationToken);
        return new(items, totalCount);
    }

    public async Task<IReadOnlyList<Guid>> FindIdsByCanonicalCodeAsync(
        string canonicalCodeSearch,
        CancellationToken cancellationToken = default)
        => await _gskus.Find(
                ActiveFilter & Builders<Gsku>.Filter.Regex(
                    x => x.CanonicalCode,
                    new BsonRegularExpression("^" + System.Text.RegularExpressions.Regex.Escape(canonicalCodeSearch))))
            .Project(x => x.Id)
            .ToListAsync(cancellationToken);

    public Task<Gsku?> GetByCreationCommandIdAsync(string creationCommandId, CancellationToken cancellationToken = default)
        => _gskus.Find(TenantFilter & Builders<Gsku>.Filter.Eq(x => x.CreationCommandId, creationCommandId))
            .FirstOrDefaultAsync(cancellationToken);

    public async Task<GskuCreateResult> CreateDraftAsync(Gsku gsku, CancellationToken cancellationToken = default)
    {
        var existing = await GetByCreationCommandIdAsync(gsku.CreationCommandId, cancellationToken);
        if (existing is not null)
        {
            return SameFacts(existing, gsku) ? new(true, existing) : new(false, existing, "CREATION_COMMAND_PAIR_CONFLICT");
        }

        var reservationFilter = Builders<CodeReservation>.Filter.Eq(x => x.TenantId, _tenantId)
            & Builders<CodeReservation>.Filter.Eq(x => x.IsDeleted, false)
            & Builders<CodeReservation>.Filter.Eq(x => x.Id, gsku.CodeReservationId)
            & Builders<CodeReservation>.Filter.Eq(x => x.EntityType, CodeBearingEntityType.Gsku)
            & Builders<CodeReservation>.Filter.Eq(x => x.ReservationState, CodeReservationState.Consumed)
            & Builders<CodeReservation>.Filter.Eq(x => x.ConsumedEntityId, gsku.Id)
            & Builders<CodeReservation>.Filter.Eq(x => x.ReservedCode, gsku.CanonicalCode)
            & Builders<CodeReservation>.Filter.In(x => x.BindingState,
                [CodeReservationBindingState.PendingIdentityWrite, CodeReservationBindingState.Confirmed]);
        if (!await _reservations.Find(reservationFilter).AnyAsync(cancellationToken))
        {
            return new(false, null, "CODE_RESERVATION_REQUIRED");
        }

        if (gsku.AuditIntents.Count is 0 or > AuditIntentLimits.MaxPerAggregate
            || gsku.AuditIntents.Any(x => x.TenantId != _tenantId))
        {
            return new(false, null, "AUDIT_INTENT_CONTRACT_INVALID");
        }

        gsku.TenantId = _tenantId;
        gsku.CreatedAt = DateTimeOffset.UtcNow;
        gsku.UpdatedAt = gsku.CreatedAt;
        gsku.IsDeleted = false;
        gsku.Version = 0;
        try
        {
            await _gskus.InsertOneAsync(gsku, cancellationToken: cancellationToken);
            return new(true, gsku);
        }
        catch (MongoWriteException exception) when (exception.WriteError?.Category == ServerErrorCategory.DuplicateKey)
        {
            existing = await GetByCreationCommandIdAsync(gsku.CreationCommandId, cancellationToken);
            return existing is not null && SameFacts(existing, gsku)
                ? new(true, existing)
                : new(false, existing, "CREATION_COMMAND_PAIR_CONFLICT");
        }
    }

    public async Task<GskuUpdateResult> UpdateDraftAsync(
        Gsku gsku,
        int expectedVersion,
        CancellationToken cancellationToken = default)
    {
        if (gsku.AuditIntents.Count == 0)
        {
            return new(false, null, "AUDIT_INTENT_CONTRACT_INVALID");
        }

        var newIntent = gsku.AuditIntents[^1];
        var filter = ActiveFilter
            & Builders<Gsku>.Filter.Eq(x => x.Id, gsku.Id)
            & Builders<Gsku>.Filter.Eq(x => x.Version, expectedVersion)
            & Builders<Gsku>.Filter.Eq(x => x.LifecycleStatus, ProductIdentityLifecycleStatus.Draft)
            & Builders<Gsku>.Filter.Where(x => x.AuditIntents.Count < AuditIntentLimits.MaxPerAggregate);
        var update = Builders<Gsku>.Update
            .Set(x => x.PackQuantity, gsku.PackQuantity)
            .Set(x => x.PackUomCode, gsku.PackUomCode)
            .Set(x => x.PackApplicabilitySelection, gsku.PackApplicabilitySelection)
            .Set(x => x.PackUomSelection, gsku.PackUomSelection)
            .Set(x => x.UpdatedAt, DateTimeOffset.UtcNow)
            .Inc(x => x.Version, 1)
            .Push(x => x.AuditIntents, newIntent);
        var updated = await _gskus.FindOneAndUpdateAsync(
            filter,
            update,
            new FindOneAndUpdateOptions<Gsku> { ReturnDocument = ReturnDocument.After },
            cancellationToken);
        if (updated is not null)
        {
            return new(true, updated);
        }

        var current = await GetByIdAsync(gsku.Id, cancellationToken);
        var isExactExternalReplay = Guid.TryParseExact(
                newIntent.IdempotencyKey,
                "D",
                out _)
            && current is not null
            && current.Version == expectedVersion + 1
            && current.LifecycleStatus == ProductIdentityLifecycleStatus.Draft
            && current.PackQuantity == gsku.PackQuantity
            && string.Equals(current.PackUomCode, gsku.PackUomCode, StringComparison.Ordinal)
            && current.AuditIntents.Count(intent =>
                intent.Operation == ProductAuditOperation.GskuDraftUpdated
                && string.Equals(intent.IdempotencyKey, newIntent.IdempotencyKey, StringComparison.Ordinal)
                && string.Equals(intent.EvidenceHash, newIntent.EvidenceHash, StringComparison.Ordinal)) == 1;
        return isExactExternalReplay
            ? new(true, current)
            : new(false, current, "CONCURRENCY_CONFLICT");
    }

    public Task<FirstGskuIdentityLifecycleMutationResult<Gsku>> MarkIdentityPendingAsync(
        Guid id, int expectedVersion, FirstGskuIdentityWorkflowBinding binding, LocalAuditIntent auditIntent,
        CancellationToken cancellationToken = default) => MutateLifecycleAsync(
            id, expectedVersion, ProductIdentityLifecycleStatus.Draft,
            ProductIdentityLifecycleStatus.PendingIdentityApproval, binding, auditIntent,
            ProductAuditOperation.GskuIdentitySubmitted, cancellationToken);

    public Task<FirstGskuIdentityLifecycleMutationResult<Gsku>> ApproveIdentityAsync(
        Guid id, int expectedVersion, FirstGskuIdentityWorkflowBinding binding, LocalAuditIntent auditIntent,
        CancellationToken cancellationToken = default) => MutateLifecycleAsync(
            id, expectedVersion, ProductIdentityLifecycleStatus.PendingIdentityApproval,
            ProductIdentityLifecycleStatus.IdentityApproved, binding, auditIntent,
            ProductAuditOperation.GskuIdentityApproved, cancellationToken);

    public Task<FirstGskuIdentityLifecycleMutationResult<Gsku>> RestoreDraftAfterRejectionAsync(
        Guid id, int expectedVersion, FirstGskuIdentityWorkflowBinding binding, LocalAuditIntent auditIntent,
        CancellationToken cancellationToken = default) => MutateLifecycleAsync(
            id, expectedVersion, ProductIdentityLifecycleStatus.PendingIdentityApproval,
            ProductIdentityLifecycleStatus.Draft, binding, auditIntent,
            ProductAuditOperation.GskuIdentityRejected, cancellationToken);

    public async Task<GskuChildCreationAdmissionResult> AcquireChildCreationAdmissionAsync(
        Guid id, GskuChildIdentityKind childKind, string creationCommandId,
        string requestFingerprint, DateTimeOffset acquiredAtUtc,
        CancellationToken cancellationToken = default)
    {
        var error = ValidateAdmission(childKind, creationCommandId, requestFingerprint, acquiredAtUtc);
        if (error is not null) return new(false, false, null, error);
        var current = await GetByIdAsync(id, cancellationToken);
        if (current is null) return new(false, false, null, "GSKU_NOT_FOUND");
        var existing = current.ChildCreationAdmissions.SingleOrDefault(x =>
            x.ChildKind == childKind && x.CreationCommandId == creationCommandId);
        if (existing is not null)
            return existing.RequestFingerprint == requestFingerprint
                ? new(true, true, current, null)
                : new(false, false, current, "GSKU_CHILD_ADMISSION_CONFLICT");
        if (current.LifecycleStatus != ProductIdentityLifecycleStatus.IdentityApproved)
            return new(false, false, current, "PARENT_NOT_IDENTITY_APPROVED");
        if (current.RetirementOperationId.HasValue)
            return new(false, false, current, "GSKU_RETIREMENT_IN_PROGRESS");
        if (current.ChildCreationAdmissions.Count >= GskuChildCreationAdmission.MaximumActiveAdmissions)
            return new(false, false, current, "GSKU_CHILD_ADMISSION_LIMIT_REACHED");

        var admission = new GskuChildCreationAdmission
        {
            ChildKind = childKind, CreationCommandId = creationCommandId,
            RequestFingerprint = requestFingerprint, AcquiredAtUtc = acquiredAtUtc
        };
        var filter = ActiveFilter & Builders<Gsku>.Filter.Eq(x => x.Id, id)
            & Builders<Gsku>.Filter.Eq(x => x.LifecycleStatus, ProductIdentityLifecycleStatus.IdentityApproved)
            & Builders<Gsku>.Filter.Eq(x => x.RetirementOperationId, null)
            & Builders<Gsku>.Filter.Not(Builders<Gsku>.Filter.ElemMatch(x => x.ChildCreationAdmissions,
                x => x.ChildKind == childKind && x.CreationCommandId == creationCommandId))
            & Builders<Gsku>.Filter.Where(x => x.ChildCreationAdmissions.Count < GskuChildCreationAdmission.MaximumActiveAdmissions);
        var updated = await _gskus.FindOneAndUpdateAsync(filter,
            Builders<Gsku>.Update.Push(x => x.ChildCreationAdmissions, admission).Set(x => x.UpdatedAt, DateTimeOffset.UtcNow),
            new FindOneAndUpdateOptions<Gsku> { ReturnDocument = ReturnDocument.After }, cancellationToken);
        if (updated is not null) return new(true, false, updated, null);
        current = await GetByIdAsync(id, cancellationToken);
        existing = current?.ChildCreationAdmissions.SingleOrDefault(x =>
            x.ChildKind == childKind && x.CreationCommandId == creationCommandId);
        return existing?.RequestFingerprint == requestFingerprint
            ? new(true, true, current, null)
            : new(false, false, current, existing is not null ? "GSKU_CHILD_ADMISSION_CONFLICT" : "GSKU_CHILD_ADMISSION_REJECTED");
    }

    public async Task<GskuChildCreationAdmissionResult> CompleteChildCreationAdmissionAsync(
        Guid id, GskuChildIdentityKind childKind, string creationCommandId,
        string requestFingerprint, CancellationToken cancellationToken = default)
    {
        var error = ValidateAdmission(childKind, creationCommandId, requestFingerprint, DateTimeOffset.UtcNow);
        if (error is not null) return new(false, false, null, error);
        var durable = await GetDurableChildFingerprintAsync(id, childKind, creationCommandId, cancellationToken);
        if (durable is null) return new(false, false, await GetByIdAsync(id, cancellationToken), "GSKU_CHILD_BINDING_NOT_DURABLE");
        if (durable != requestFingerprint)
            return new(false, false, await GetByIdAsync(id, cancellationToken), "GSKU_CHILD_ADMISSION_CONFLICT");
        var filter = ActiveFilter & Builders<Gsku>.Filter.Eq(x => x.Id, id)
            & Builders<Gsku>.Filter.ElemMatch(x => x.ChildCreationAdmissions,
                x => x.ChildKind == childKind && x.CreationCommandId == creationCommandId
                     && x.RequestFingerprint == requestFingerprint);
        var updated = await _gskus.FindOneAndUpdateAsync(filter,
            Builders<Gsku>.Update.PullFilter(x => x.ChildCreationAdmissions,
                x => x.ChildKind == childKind && x.CreationCommandId == creationCommandId
                     && x.RequestFingerprint == requestFingerprint).Set(x => x.UpdatedAt, DateTimeOffset.UtcNow),
            new FindOneAndUpdateOptions<Gsku> { ReturnDocument = ReturnDocument.After }, cancellationToken);
        if (updated is not null) return new(true, false, updated, null);
        var current = await GetByIdAsync(id, cancellationToken);
        var remaining = current?.ChildCreationAdmissions.SingleOrDefault(x =>
            x.ChildKind == childKind && x.CreationCommandId == creationCommandId);
        return remaining is null
            ? new(true, true, current, null)
            : new(false, false, current, "GSKU_CHILD_ADMISSION_CONFLICT");
    }

    public async Task<FirstGskuIdentityRetirementWriteResult<Gsku>> CloseChildAdmissionFenceAsync(
        Guid id, int expectedVersion, Guid operationId, string operationFingerprint,
        CancellationToken cancellationToken = default)
    {
        if (id == Guid.Empty || operationId == Guid.Empty || expectedVersion < 0 || !Exact(operationFingerprint, 256))
            return new(false, false, null, "FIRST_GSKU_RETIREMENT_CONTRACT_INVALID");
        var current = await GetByIdAsync(id, cancellationToken);
        if (current?.RetirementOperationId == operationId)
            return current.RetirementOperationFingerprint == operationFingerprint
                ? new(true, true, current, null)
                : new(false, false, current, "FIRST_GSKU_RETIREMENT_IDEMPOTENCY_CONFLICT");
        if (current?.ChildCreationAdmissions.Count > 0)
            return new(false, false, current, "GSKU_CHILD_CREATION_IN_PROGRESS");
        var lifecycleBinding = current?.ActiveLifecycleOperation;
        var fromRequest = lifecycleBinding is { Kind: GskuLifecycleOperationKind.Retirement }
            && lifecycleBinding.OperationId == operationId
            && lifecycleBinding.BaseGskuVersion + 1 == expectedVersion;
        if (lifecycleBinding is not null && !fromRequest)
            return new(false, false, current, "GSKU_LIFECYCLE_OPERATION_ACTIVE");
        var filter = ActiveFilter & Builders<Gsku>.Filter.Eq(x => x.Id, id)
            & Builders<Gsku>.Filter.Eq(x => x.Version, expectedVersion)
            & Builders<Gsku>.Filter.Eq(x => x.LifecycleStatus, ProductIdentityLifecycleStatus.IdentityApproved)
            & Builders<Gsku>.Filter.Eq(x => x.RetirementOperationId, null)
            & (fromRequest
                ? Builders<Gsku>.Filter.Eq(x => x.ActiveLifecycleOperation, lifecycleBinding)
                : Builders<Gsku>.Filter.Eq(x => x.ActiveLifecycleOperation, null))
            & Builders<Gsku>.Filter.Size(x => x.ChildCreationAdmissions, 0);
        var updated = await _gskus.FindOneAndUpdateAsync(filter,
            Builders<Gsku>.Update.Set(x => x.RetirementOperationId, operationId)
                .Set(x => x.RetirementOperationFingerprint, operationFingerprint)
                .Set(x => x.ActiveLifecycleOperation, null)
                .Set(x => x.UpdatedAt, DateTimeOffset.UtcNow).Inc(x => x.Version, 1),
            new FindOneAndUpdateOptions<Gsku> { ReturnDocument = ReturnDocument.After }, cancellationToken);
        return updated is not null ? new(true, false, updated, null)
            : new(false, false, current, current is null ? "GSKU_NOT_FOUND" : "FIRST_GSKU_RETIREMENT_FENCE_CONFLICT");
    }

    public async Task<string?> FindRetirementBlockerAsync(Guid id, CancellationToken cancellationToken = default)
    {
        var current = await GetByIdAsync(id, cancellationToken);
        if (current is null) return "GSKU_NOT_FOUND";
        if (current.ChildCreationAdmissions.Count > 0) return "GSKU_CHILD_CREATION_IN_PROGRESS";
        if (await HasNonRetiredSiblingAsync(current.ProductDefinitionRevisionId, id, cancellationToken))
            return "DEPENDENT_IDENTITIES_EXIST";
        var childFilter = Builders<Lsku>.Filter.Eq(x => x.TenantId, _tenantId)
            & Builders<Lsku>.Filter.Eq(x => x.IsDeleted, false)
            & Builders<Lsku>.Filter.Eq(x => x.GskuId, id)
            & Builders<Lsku>.Filter.Ne(x => x.LifecycleStatus, ProductIdentityLifecycleStatus.Retired);
        if (await _lskus.Find(childFilter).AnyAsync(cancellationToken)) return "DEPENDENT_IDENTITIES_EXIST";
        var fgFilter = Builders<FinishedGood>.Filter.Eq(x => x.TenantId, _tenantId)
            & Builders<FinishedGood>.Filter.Eq(x => x.IsDeleted, false)
            & Builders<FinishedGood>.Filter.Eq(x => x.GskuId, id)
            & Builders<FinishedGood>.Filter.Ne(x => x.LifecycleStatus, ProductIdentityLifecycleStatus.Retired);
        return await _finishedGoods.Find(fgFilter).AnyAsync(cancellationToken) ? "DEPENDENT_IDENTITIES_EXIST" : null;
    }

    public Task<bool> HasNonRetiredSiblingAsync(
        Guid productDefinitionRevisionId, Guid excludingGskuId,
        CancellationToken cancellationToken = default) =>
        _gskus.Find(ActiveFilter
                    & Builders<Gsku>.Filter.Eq(x => x.ProductDefinitionRevisionId, productDefinitionRevisionId)
                    & Builders<Gsku>.Filter.Ne(x => x.Id, excludingGskuId)
                    & Builders<Gsku>.Filter.Ne(x => x.LifecycleStatus, ProductIdentityLifecycleStatus.Retired))
            .AnyAsync(cancellationToken);

    public async Task<FirstGskuIdentityRetirementWriteResult<Gsku>> RetireIdentityAsync(
        Guid id, int expectedVersion, Guid operationId, string operationFingerprint,
        LocalAuditIntent auditIntent, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(auditIntent);
        if (await FindRetirementBlockerAsync(id, cancellationToken) is { } blocker)
            return new(false, false, await GetByIdAsync(id, cancellationToken), blocker);
        var current = await GetByIdAsync(id, cancellationToken);
        var replay = current?.AuditIntents.SingleOrDefault(x => x.IdempotencyKey == auditIntent.IdempotencyKey);
        if (replay is not null)
            return replay.Operation == ProductAuditOperation.GskuIdentityRetired && replay.EvidenceHash == auditIntent.EvidenceHash
                   && current!.LifecycleStatus == ProductIdentityLifecycleStatus.Retired
                   && current.RetirementOperationId == operationId
                   && current.RetirementOperationFingerprint == operationFingerprint
                ? new(true, true, current, null)
                : new(false, false, current, "FIRST_GSKU_RETIREMENT_IDEMPOTENCY_CONFLICT");
        if (!ValidRetirementAudit(id, expectedVersion, auditIntent, ProductAuditOperation.GskuIdentityRetired))
            return new(false, false, current, "AUDIT_INTENT_CONTRACT_INVALID");
        var filter = ActiveFilter & Builders<Gsku>.Filter.Eq(x => x.Id, id)
            & Builders<Gsku>.Filter.Eq(x => x.Version, expectedVersion)
            & Builders<Gsku>.Filter.Eq(x => x.LifecycleStatus, ProductIdentityLifecycleStatus.IdentityApproved)
            & Builders<Gsku>.Filter.Eq(x => x.RetirementOperationId, operationId)
            & Builders<Gsku>.Filter.Eq(x => x.RetirementOperationFingerprint, operationFingerprint)
            & Builders<Gsku>.Filter.Size(x => x.ChildCreationAdmissions, 0)
            & Builders<Gsku>.Filter.Where(x => x.AuditIntents.Count < AuditIntentLimits.MaxPerAggregate);
        var updated = await _gskus.FindOneAndUpdateAsync(filter,
            Builders<Gsku>.Update.Set(x => x.LifecycleStatus, ProductIdentityLifecycleStatus.Retired)
                .Set(x => x.UpdatedAt, DateTimeOffset.UtcNow).Inc(x => x.Version, 1).Push(x => x.AuditIntents, auditIntent),
            new FindOneAndUpdateOptions<Gsku> { ReturnDocument = ReturnDocument.After }, cancellationToken);
        return updated is not null ? new(true, false, updated, null)
            : new(false, false, current, current is null ? "GSKU_NOT_FOUND" : "FIRST_GSKU_RETIREMENT_CONCURRENCY_CONFLICT");
    }

    public async Task<GskuRetirementRequestWriteResult> AcquireRetirementRequestAsync(
        Guid id, int expectedVersion, GskuActiveLifecycleOperationBinding binding,
        LocalAuditIntent auditIntent, CancellationToken cancellationToken = default)
    {
        var error = ValidateRetirementRequest(id, expectedVersion, binding, auditIntent,
            ProductAuditOperation.GskuRetirementRequested, false);
        if (error is not null) return new(false, false, null, error);
        var current = await GetByIdAsync(id, cancellationToken);
        var replay = RetirementRequestReplay(current, auditIntent, binding, false);
        if (replay is not null) return replay;
        if (await FindRetirementBlockerAsync(id, cancellationToken) is { } blocker)
            return new(false, false, current, blocker);
        var filter = ActiveFilter & Builders<Gsku>.Filter.Eq(x => x.Id, id)
            & Builders<Gsku>.Filter.Eq(x => x.Version, expectedVersion)
            & Builders<Gsku>.Filter.Eq(x => x.LifecycleStatus, ProductIdentityLifecycleStatus.IdentityApproved)
            & Builders<Gsku>.Filter.Eq(x => x.ActiveLifecycleOperation, null)
            & Builders<Gsku>.Filter.Eq(x => x.RetirementOperationId, null)
            & Builders<Gsku>.Filter.Size(x => x.ChildCreationAdmissions, 0)
            & Builders<Gsku>.Filter.Where(x => x.AuditIntents.Count < AuditIntentLimits.MaxPerAggregate);
        var updated = await _gskus.FindOneAndUpdateAsync(filter,
            Builders<Gsku>.Update.Set(x => x.ActiveLifecycleOperation, binding)
                .Set(x => x.UpdatedAt, DateTimeOffset.UtcNow).Inc(x => x.Version, 1)
                .Push(x => x.AuditIntents, auditIntent),
            new() { ReturnDocument = ReturnDocument.After }, cancellationToken);
        return updated is not null ? new(true, false, updated, null)
            : new(false, false, current, current is null ? "GSKU_NOT_FOUND"
                : current.ActiveLifecycleOperation is not null ? "GSKU_LIFECYCLE_OPERATION_ACTIVE"
                : "GSKU_RETIREMENT_REQUEST_ADMISSION_CONFLICT");
    }

    public async Task<GskuRetirementRequestWriteResult> RejectRetirementRequestAsync(
        Guid id, int expectedVersion, GskuActiveLifecycleOperationBinding binding,
        LocalAuditIntent auditIntent, CancellationToken cancellationToken = default)
    {
        var error = ValidateRetirementRequest(id, expectedVersion, binding, auditIntent,
            ProductAuditOperation.GskuRetirementRejected, true);
        if (error is not null) return new(false, false, null, error);
        var current = await GetByIdAsync(id, cancellationToken);
        var replay = RetirementRequestReplay(current, auditIntent, binding, true);
        if (replay is not null) return replay;
        var filter = ActiveFilter & Builders<Gsku>.Filter.Eq(x => x.Id, id)
            & Builders<Gsku>.Filter.Eq(x => x.Version, expectedVersion)
            & Builders<Gsku>.Filter.Eq(x => x.LifecycleStatus, ProductIdentityLifecycleStatus.IdentityApproved)
            & Builders<Gsku>.Filter.Eq(x => x.ActiveLifecycleOperation!.Kind, GskuLifecycleOperationKind.Retirement)
            & Builders<Gsku>.Filter.Eq(x => x.ActiveLifecycleOperation!.OperationId, binding.OperationId)
            & Builders<Gsku>.Filter.Eq(x => x.ActiveLifecycleOperation!.BaseGskuVersion, binding.BaseGskuVersion)
            & Builders<Gsku>.Filter.Where(x => x.AuditIntents.Count < AuditIntentLimits.MaxPerAggregate);
        var updated = await _gskus.FindOneAndUpdateAsync(filter,
            Builders<Gsku>.Update.Set(x => x.ActiveLifecycleOperation, null)
                .Set(x => x.UpdatedAt, DateTimeOffset.UtcNow).Inc(x => x.Version, 1)
                .Push(x => x.AuditIntents, auditIntent),
            new() { ReturnDocument = ReturnDocument.After }, cancellationToken);
        return updated is not null ? new(true, false, updated, null)
            : new(false, false, current, current is null ? "GSKU_NOT_FOUND"
                : "GSKU_RETIREMENT_REQUEST_CONCURRENCY_CONFLICT");
    }

    public async Task<GskuRetirementRequestWriteResult> RecordRetirementRequestConflictAsync(
        Guid id, int expectedVersion, GskuActiveLifecycleOperationBinding binding,
        LocalAuditIntent auditIntent, CancellationToken cancellationToken = default)
    {
        if (_tenantId == Guid.Empty || id == Guid.Empty || expectedVersion < 0
            || binding.Kind != GskuLifecycleOperationKind.Retirement || binding.OperationId == Guid.Empty
            || binding.BaseGskuVersion >= expectedVersion
            || auditIntent.TenantId != _tenantId || auditIntent.AggregateType != AuditAggregateType.Gsku
            || auditIntent.AggregateId != id || auditIntent.PreVersion != expectedVersion
            || auditIntent.PostVersion != expectedVersion + 1
            || auditIntent.Operation != ProductAuditOperation.GskuRetirementManualReconciliationRequired
            || !Exact(auditIntent.IdempotencyKey, 256) || !Exact(auditIntent.EvidenceHash, 256))
            return new(false, false, null, "GSKU_RETIREMENT_REQUEST_CONTRACT_INVALID");
        var current = await GetByIdAsync(id, cancellationToken);
        var persisted = current?.AuditIntents.SingleOrDefault(x => x.IdempotencyKey == auditIntent.IdempotencyKey);
        if (persisted is not null)
            return persisted.Operation == auditIntent.Operation && persisted.EvidenceHash == auditIntent.EvidenceHash
                ? new(true, true, current, null)
                : new(false, false, current, "GSKU_RETIREMENT_REQUEST_IDEMPOTENCY_CONFLICT");
        var activeBinding = Builders<Gsku>.Filter.Eq(x => x.ActiveLifecycleOperation!.Kind, binding.Kind)
            & Builders<Gsku>.Filter.Eq(x => x.ActiveLifecycleOperation!.OperationId, binding.OperationId)
            & Builders<Gsku>.Filter.Eq(x => x.ActiveLifecycleOperation!.BaseGskuVersion, binding.BaseGskuVersion);
        var pairFence = Builders<Gsku>.Filter.Eq(x => x.RetirementOperationId, binding.OperationId);
        var filter = ActiveFilter & Builders<Gsku>.Filter.Eq(x => x.Id, id)
            & Builders<Gsku>.Filter.Eq(x => x.Version, expectedVersion)
            & (activeBinding | pairFence)
            & Builders<Gsku>.Filter.Where(x => x.AuditIntents.Count < AuditIntentLimits.MaxPerAggregate);
        var updated = await _gskus.FindOneAndUpdateAsync(filter,
            Builders<Gsku>.Update.Set(x => x.UpdatedAt, DateTimeOffset.UtcNow).Inc(x => x.Version, 1)
                .Push(x => x.AuditIntents, auditIntent),
            new() { ReturnDocument = ReturnDocument.After }, cancellationToken);
        return updated is not null ? new(true, false, updated, null)
            : new(false, false, current, current is null ? "GSKU_NOT_FOUND"
                : "GSKU_RETIREMENT_REQUEST_CONCURRENCY_CONFLICT");
    }

    private string? ValidateRetirementRequest(Guid id, int expectedVersion,
        GskuActiveLifecycleOperationBinding binding, LocalAuditIntent auditIntent,
        ProductAuditOperation operation, bool terminal)
    {
        if (_tenantId == Guid.Empty || id == Guid.Empty || expectedVersion < 0
            || binding.Kind != GskuLifecycleOperationKind.Retirement || binding.OperationId == Guid.Empty
            || binding.BaseGskuVersion + (terminal ? 1 : 0) != expectedVersion
            || auditIntent.TenantId != _tenantId || auditIntent.AggregateType != AuditAggregateType.Gsku
            || auditIntent.AggregateId != id || auditIntent.PreVersion != expectedVersion
            || auditIntent.PostVersion != expectedVersion + 1 || auditIntent.Operation != operation
            || !Exact(auditIntent.IdempotencyKey, 256) || !Exact(auditIntent.EvidenceHash, 256))
            return "GSKU_RETIREMENT_REQUEST_CONTRACT_INVALID";
        return null;
    }

    private static GskuRetirementRequestWriteResult? RetirementRequestReplay(Gsku? current,
        LocalAuditIntent auditIntent, GskuActiveLifecycleOperationBinding binding, bool terminal)
    {
        var persisted = current?.AuditIntents.SingleOrDefault(x => x.IdempotencyKey == auditIntent.IdempotencyKey);
        if (persisted is null) return null;
        var bindingMatches = terminal ? current!.ActiveLifecycleOperation is null
            : current!.ActiveLifecycleOperation == binding;
        return persisted.Operation == auditIntent.Operation && persisted.EvidenceHash == auditIntent.EvidenceHash
               && bindingMatches
            ? new(true, true, current, null)
            : new(false, false, current, "GSKU_RETIREMENT_REQUEST_IDEMPOTENCY_CONFLICT");
    }

    public async Task<GskuCorrectionWriteResult> AcquireCorrectionAsync(
        Guid id, int expectedVersion, GskuActiveLifecycleOperationBinding binding,
        LocalAuditIntent auditIntent, CancellationToken cancellationToken = default)
    {
        var error = ValidateCorrection(id, expectedVersion, binding, auditIntent,
            ProductAuditOperation.GskuCorrectionRequested, false);
        if (error is not null) return new(false, false, null, error);
        var current = await GetByIdAsync(id, cancellationToken);
        var replay = CorrectionReplay(current, auditIntent, binding, false);
        if (replay is not null) return replay;
        if (await FindRetirementBlockerAsync(id, cancellationToken) is { } blocker)
            return new(false, false, current, blocker);
        var filter = ActiveFilter & Builders<Gsku>.Filter.Eq(x => x.Id, id)
            & Builders<Gsku>.Filter.Eq(x => x.Version, expectedVersion)
            & Builders<Gsku>.Filter.Eq(x => x.LifecycleStatus, ProductIdentityLifecycleStatus.IdentityApproved)
            & Builders<Gsku>.Filter.Eq(x => x.ActiveLifecycleOperation, null)
            & Builders<Gsku>.Filter.Size(x => x.ChildCreationAdmissions, 0)
            & Builders<Gsku>.Filter.Where(x => x.AuditIntents.Count < AuditIntentLimits.MaxPerAggregate);
        var updated = await _gskus.FindOneAndUpdateAsync(filter,
            Builders<Gsku>.Update.Set(x => x.ActiveLifecycleOperation, binding)
                .Set(x => x.UpdatedAt, DateTimeOffset.UtcNow).Inc(x => x.Version, 1)
                .Push(x => x.AuditIntents, auditIntent),
            new() { ReturnDocument = ReturnDocument.After }, cancellationToken);
        if (updated is not null) return new(true, false, updated, null);
        current = await GetByIdAsync(id, cancellationToken);
        return new(false, false, current, current is null ? "GSKU_NOT_FOUND"
            : current.ActiveLifecycleOperation is not null ? "GSKU_LIFECYCLE_OPERATION_ACTIVE"
            : current.Version != expectedVersion ? "GSKU_CONCURRENCY_CONFLICT"
            : "GSKU_CORRECTION_ADMISSION_CONFLICT");
    }

    public async Task<GskuCorrectionWriteResult> ApplyCorrectionDecisionAsync(
        Guid id, int expectedVersion, GskuActiveLifecycleOperationBinding binding,
        decimal? approvedPackQuantity, string? approvedPackUomCode,
        ReferenceCatalogSelection? approvedPackApplicabilitySelection,
        ReferenceCatalogSelection? approvedPackUomSelection,
        LocalAuditIntent auditIntent, CancellationToken cancellationToken = default)
    {
        var approved = approvedPackQuantity.HasValue || approvedPackUomCode is not null
            || approvedPackApplicabilitySelection is not null || approvedPackUomSelection is not null;
        var expectedOperation = approved ? ProductAuditOperation.GskuCorrectionApplied
            : ProductAuditOperation.GskuCorrectionRejected;
        var error = ValidateCorrection(id, expectedVersion, binding, auditIntent, expectedOperation, true);
        if (error is not null || approved && (approvedPackQuantity is not > 0
                || string.IsNullOrWhiteSpace(approvedPackUomCode)
                || approvedPackApplicabilitySelection is null || approvedPackUomSelection is null))
            return new(false, false, null, error ?? "GSKU_CORRECTION_CONTRACT_INVALID");
        var current = await GetByIdAsync(id, cancellationToken);
        var replay = CorrectionReplay(current, auditIntent, binding, true);
        if (replay is not null) return replay;
        if (approved && await FindRetirementBlockerAsync(id, cancellationToken) is { } blocker)
            return new(false, false, current, blocker);
        var filter = ActiveFilter & Builders<Gsku>.Filter.Eq(x => x.Id, id)
            & Builders<Gsku>.Filter.Eq(x => x.Version, expectedVersion)
            & Builders<Gsku>.Filter.Eq(x => x.LifecycleStatus, ProductIdentityLifecycleStatus.IdentityApproved)
            & Builders<Gsku>.Filter.Eq(x => x.ActiveLifecycleOperation!.Kind, GskuLifecycleOperationKind.Correction)
            & Builders<Gsku>.Filter.Eq(x => x.ActiveLifecycleOperation!.OperationId, binding.OperationId)
            & Builders<Gsku>.Filter.Eq(x => x.ActiveLifecycleOperation!.BaseGskuVersion, binding.BaseGskuVersion)
            & Builders<Gsku>.Filter.Size(x => x.ChildCreationAdmissions, 0)
            & Builders<Gsku>.Filter.Where(x => x.AuditIntents.Count < AuditIntentLimits.MaxPerAggregate);
        var update = Builders<Gsku>.Update.Set(x => x.ActiveLifecycleOperation, null)
            .Set(x => x.UpdatedAt, DateTimeOffset.UtcNow).Inc(x => x.Version, 1)
            .Push(x => x.AuditIntents, auditIntent);
        if (approved)
            update = update.Set(x => x.PackQuantity, approvedPackQuantity!.Value)
                .Set(x => x.PackUomCode, approvedPackUomCode!)
                .Set(x => x.PackApplicabilitySelection, approvedPackApplicabilitySelection!)
                .Set(x => x.PackUomSelection, approvedPackUomSelection!);
        var updated = await _gskus.FindOneAndUpdateAsync(filter, update,
            new() { ReturnDocument = ReturnDocument.After }, cancellationToken);
        return updated is not null ? new(true, false, updated, null)
            : new(false, false, current, current is null ? "GSKU_NOT_FOUND" : "GSKU_CORRECTION_CONCURRENCY_CONFLICT");
    }

    private string? ValidateCorrection(Guid id, int expectedVersion,
        GskuActiveLifecycleOperationBinding binding, LocalAuditIntent auditIntent,
        ProductAuditOperation operation, bool terminal)
    {
        if (_tenantId == Guid.Empty || id == Guid.Empty || expectedVersion < 0
            || binding.Kind != GskuLifecycleOperationKind.Correction || binding.OperationId == Guid.Empty
            || binding.BaseGskuVersion + (terminal ? 1 : 0) != expectedVersion
            || auditIntent.TenantId != _tenantId || auditIntent.AggregateType != AuditAggregateType.Gsku
            || auditIntent.AggregateId != id || auditIntent.PreVersion != expectedVersion
            || auditIntent.PostVersion != expectedVersion + 1 || auditIntent.Operation != operation
            || !Exact(auditIntent.IdempotencyKey, 256) || !Exact(auditIntent.EvidenceHash, 256))
            return "GSKU_CORRECTION_CONTRACT_INVALID";
        return null;
    }

    private static GskuCorrectionWriteResult? CorrectionReplay(Gsku? current,
        LocalAuditIntent auditIntent, GskuActiveLifecycleOperationBinding binding, bool terminal)
    {
        var persisted = current?.AuditIntents.SingleOrDefault(x => x.IdempotencyKey == auditIntent.IdempotencyKey);
        if (persisted is null) return null;
        var bindingMatches = terminal ? current!.ActiveLifecycleOperation is null
            : current!.ActiveLifecycleOperation == binding;
        return persisted.Operation == auditIntent.Operation && persisted.EvidenceHash == auditIntent.EvidenceHash
               && bindingMatches
            ? new(true, true, current, null)
            : new(false, false, current, "GSKU_CORRECTION_IDEMPOTENCY_CONFLICT");
    }

    private async Task<FirstGskuIdentityLifecycleMutationResult<Gsku>> MutateLifecycleAsync(
        Guid id, int expectedVersion, ProductIdentityLifecycleStatus sourceStatus,
        ProductIdentityLifecycleStatus targetStatus, FirstGskuIdentityWorkflowBinding binding,
        LocalAuditIntent auditIntent, ProductAuditOperation operation, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(binding);
        ArgumentNullException.ThrowIfNull(auditIntent);
        var contractError = ValidateLifecycleContract(id, expectedVersion, binding, auditIntent, operation);
        if (contractError is not null)
        {
            return new(false, false, null, contractError);
        }

        var current = await GetByIdAsync(id, cancellationToken);
        var replayIntent = current?.AuditIntents.SingleOrDefault(x => x.IdempotencyKey == auditIntent.IdempotencyKey);
        if (replayIntent is not null)
        {
            var exact = replayIntent.Operation == operation
                        && replayIntent.EvidenceHash == auditIntent.EvidenceHash
                        && current!.LifecycleStatus == targetStatus
                        && SameBinding(current.IdentityWorkflowBinding, binding);
            return exact
                ? new(true, true, current, null)
                : new(false, false, current, "FIRST_GSKU_IDENTITY_IDEMPOTENCY_CONFLICT");
        }

        var filter = ActiveFilter
                     & Builders<Gsku>.Filter.Eq(x => x.Id, id)
                     & Builders<Gsku>.Filter.Eq(x => x.Version, expectedVersion)
                     & Builders<Gsku>.Filter.Eq(x => x.LifecycleStatus, sourceStatus)
                     & Builders<Gsku>.Filter.Where(x => x.AuditIntents.Count < AuditIntentLimits.MaxPerAggregate);
        if (sourceStatus == ProductIdentityLifecycleStatus.PendingIdentityApproval)
        {
            filter &= BindingFence(binding);
        }

        var update = Builders<Gsku>.Update
            .Set(x => x.LifecycleStatus, targetStatus)
            .Set(x => x.IdentityWorkflowBinding, binding)
            .Set(x => x.UpdatedAt, DateTimeOffset.UtcNow)
            .Inc(x => x.Version, 1)
            .Push(x => x.AuditIntents, auditIntent);
        var updated = await _gskus.FindOneAndUpdateAsync(
            filter, update, new FindOneAndUpdateOptions<Gsku> { ReturnDocument = ReturnDocument.After },
            cancellationToken);
        if (updated is not null)
        {
            return new(true, false, updated, null);
        }

        current = await GetByIdAsync(id, cancellationToken);
        return new(false, false, current, current is null
            ? "FIRST_GSKU_NOT_FOUND"
            : current.Version != expectedVersion
                ? "FIRST_GSKU_CONCURRENCY_CONFLICT"
                : current.LifecycleStatus != sourceStatus
                    ? "FIRST_GSKU_STATE_CONFLICT"
                    : "FIRST_GSKU_WORKFLOW_BINDING_CONFLICT");
    }

    private string? ValidateLifecycleContract(
        Guid id, int expectedVersion, FirstGskuIdentityWorkflowBinding binding,
        LocalAuditIntent auditIntent, ProductAuditOperation operation)
    {
        if (_tenantId == Guid.Empty || id == Guid.Empty || expectedVersion < 0
            || binding.GskuId != id || binding.ProductDefinitionRevisionId == Guid.Empty
            || binding.WorkflowInstanceId == Guid.Empty || binding.WorkflowTemplateId == Guid.Empty
            || binding.WorkflowTemplateVersionId == Guid.Empty || binding.ApprovalTaskId == Guid.Empty
            || binding.AssignmentSnapshotId == Guid.Empty || binding.StartTransitionLogId == Guid.Empty
            || binding.SubmitterSubjectId == Guid.Empty || binding.ObjectType != "gsku"
            || binding.SubmittedAtUtc.Offset != TimeSpan.Zero
            || binding.DueAtUtc.HasValue && binding.DueAtUtc.Value.Offset != TimeSpan.Zero
            || string.IsNullOrWhiteSpace(binding.ObjectRef)
            || string.IsNullOrWhiteSpace(binding.StartIdempotencyKey)
            || string.IsNullOrWhiteSpace(binding.StartRequestFingerprint))
        {
            return "FIRST_GSKU_WORKFLOW_BINDING_INVALID";
        }
        var expectedDecision = operation switch
        {
            ProductAuditOperation.GskuIdentityApproved => ProductIdentityDecisionKind.Approved,
            ProductAuditOperation.GskuIdentityRejected => ProductIdentityDecisionKind.Rejected,
            _ => (ProductIdentityDecisionKind?)null
        };
        if (expectedDecision.HasValue
                ? !ValidTerminalDecision(binding, expectedDecision.Value)
                : binding.TerminalDecision is not null)
        {
            return "FIRST_GSKU_WORKFLOW_DECISION_INVALID";
        }
        if (auditIntent.TenantId != _tenantId || auditIntent.AggregateId != id
            || auditIntent.PreVersion != expectedVersion || auditIntent.PostVersion != expectedVersion + 1
            || auditIntent.Operation != operation || string.IsNullOrWhiteSpace(auditIntent.IdempotencyKey)
            || string.IsNullOrWhiteSpace(auditIntent.EvidenceHash))
        {
            return "AUDIT_INTENT_CONTRACT_INVALID";
        }
        return null;
    }

    private FilterDefinition<Gsku> BindingFence(FirstGskuIdentityWorkflowBinding binding) =>
        Builders<Gsku>.Filter.Eq(x => x.IdentityWorkflowBinding!.WorkflowInstanceId, binding.WorkflowInstanceId)
        & Builders<Gsku>.Filter.Eq(x => x.IdentityWorkflowBinding!.ApprovalTaskId, binding.ApprovalTaskId)
        & Builders<Gsku>.Filter.Eq(x => x.IdentityWorkflowBinding!.StartRequestFingerprint, binding.StartRequestFingerprint);

    private static bool SameBinding(FirstGskuIdentityWorkflowBinding? left, FirstGskuIdentityWorkflowBinding right) =>
        left is not null && left.WorkflowInstanceId == right.WorkflowInstanceId
        && left.WorkflowTemplateId == right.WorkflowTemplateId
        && left.WorkflowTemplateVersionId == right.WorkflowTemplateVersionId
        && left.ApprovalTaskId == right.ApprovalTaskId
        && left.AssignmentSnapshotId == right.AssignmentSnapshotId
        && left.StartTransitionLogId == right.StartTransitionLogId
        && left.ObjectType == right.ObjectType
        && left.GskuId == right.GskuId
        && left.ProductDefinitionRevisionId == right.ProductDefinitionRevisionId
        && left.ObjectRef == right.ObjectRef
        && left.SubmitterSubjectId == right.SubmitterSubjectId
        && left.StartIdempotencyKey == right.StartIdempotencyKey
        && left.StartRequestFingerprint == right.StartRequestFingerprint
        && left.SubmittedAtUtc == right.SubmittedAtUtc
        && left.DueAtUtc == right.DueAtUtc
        && SameTerminalDecision(left.TerminalDecision, right.TerminalDecision);

    private static bool ValidTerminalDecision(
        FirstGskuIdentityWorkflowBinding binding, ProductIdentityDecisionKind expected)
    {
        var value = binding.TerminalDecision;
        return value is not null && value.Decision == expected
            && value.WorkflowInstanceId == binding.WorkflowInstanceId
            && value.ApprovalTaskId == binding.ApprovalTaskId
            && value.WorkflowTemplateId == binding.WorkflowTemplateId
            && value.WorkflowTemplateVersionId == binding.WorkflowTemplateVersionId
            && value.ObjectType == binding.ObjectType && value.ObjectId == binding.GskuId
            && value.ObjectRef == binding.ObjectRef && value.DecisionActorSubjectId != Guid.Empty
            && value.DecisionActorSubjectId != binding.SubmitterSubjectId
            && value.DecisionAtUtc.Offset == TimeSpan.Zero && value.TransitionSequence > 0
            && (expected == ProductIdentityDecisionKind.Approved
                ? value.TaskStatus == "Approved" && value.InstanceStatus == "Completed"
                : value.TaskStatus == "Rejected" && value.InstanceStatus == "Rejected");
    }

    private static bool SameTerminalDecision(
        ProductIdentityWorkflowDecisionEvidence? left, ProductIdentityWorkflowDecisionEvidence? right) =>
        left is null && right is null
        || left is not null && right is not null
        && left.Decision == right.Decision && left.WorkflowInstanceId == right.WorkflowInstanceId
        && left.ApprovalTaskId == right.ApprovalTaskId && left.WorkflowTemplateId == right.WorkflowTemplateId
        && left.WorkflowTemplateVersionId == right.WorkflowTemplateVersionId
        && left.ObjectType == right.ObjectType && left.ObjectId == right.ObjectId
        && left.ObjectRef == right.ObjectRef && left.DecisionActorSubjectId == right.DecisionActorSubjectId
        && left.ReasonCode == right.ReasonCode && left.DecisionAtUtc == right.DecisionAtUtc
        && left.TransitionSequence == right.TransitionSequence && left.TaskStatus == right.TaskStatus
        && left.InstanceStatus == right.InstanceStatus;

    private static bool SameFacts(Gsku left, Gsku right)
        => left.Id == right.Id
           && left.ProductDefinitionRevisionId == right.ProductDefinitionRevisionId
           && left.CodeReservationId == right.CodeReservationId
           && left.CanonicalCode == right.CanonicalCode
           && left.CreationCommandId == right.CreationCommandId;

    private async Task<string?> GetDurableChildFingerprintAsync(
        Guid gskuId, GskuChildIdentityKind childKind, string creationCommandId,
        CancellationToken cancellationToken)
    {
        if (childKind == GskuChildIdentityKind.Lsku)
        {
            var child = await _lskus.Find(
                    Builders<Lsku>.Filter.Eq(x => x.TenantId, _tenantId)
                    & Builders<Lsku>.Filter.Eq(x => x.IsDeleted, false)
                    & Builders<Lsku>.Filter.Eq(x => x.GskuId, gskuId)
                    & Builders<Lsku>.Filter.Eq(x => x.CreationCommandId, creationCommandId))
                .Project(x => new { x.Id, x.CodeReservationId, x.CanonicalCode, x.MarketCode })
                .FirstOrDefaultAsync(cancellationToken);
            if (child is null || !await HasConfirmedReservationAsync(
                    child.CodeReservationId, CodeBearingEntityType.Lsku, child.Id, child.CanonicalCode, cancellationToken))
                return null;
            return GskuChildCreationAdmission.ComputeRequestFingerprint(
                gskuId, childKind, creationCommandId, child.MarketCode);
        }
        var finishedGood = await _finishedGoods.Find(
                Builders<FinishedGood>.Filter.Eq(x => x.TenantId, _tenantId)
                & Builders<FinishedGood>.Filter.Eq(x => x.IsDeleted, false)
                & Builders<FinishedGood>.Filter.Eq(x => x.GskuId, gskuId)
                & Builders<FinishedGood>.Filter.Eq(x => x.CreationCommandId, creationCommandId))
            .Project(x => new { x.Id, x.CodeReservationId, x.CanonicalCode }).FirstOrDefaultAsync(cancellationToken);
        if (finishedGood is null || !await HasConfirmedReservationAsync(
                finishedGood.CodeReservationId, CodeBearingEntityType.FinishedGood,
                finishedGood.Id, finishedGood.CanonicalCode, cancellationToken))
            return null;
        return GskuChildCreationAdmission.ComputeRequestFingerprint(gskuId, childKind, creationCommandId);
    }

    private Task<bool> HasConfirmedReservationAsync(
        Guid reservationId, CodeBearingEntityType entityType, Guid entityId,
        string canonicalCode, CancellationToken cancellationToken) =>
        _reservations.Find(
            Builders<CodeReservation>.Filter.Eq(x => x.TenantId, _tenantId)
            & Builders<CodeReservation>.Filter.Eq(x => x.IsDeleted, false)
            & Builders<CodeReservation>.Filter.Eq(x => x.Id, reservationId)
            & Builders<CodeReservation>.Filter.Eq(x => x.EntityType, entityType)
            & Builders<CodeReservation>.Filter.Eq(x => x.ReservationState, CodeReservationState.Consumed)
            & Builders<CodeReservation>.Filter.Eq(x => x.BindingState, CodeReservationBindingState.Confirmed)
            & Builders<CodeReservation>.Filter.Eq(x => x.ConsumedEntityId, entityId)
            & Builders<CodeReservation>.Filter.Eq(x => x.ReservedCode, canonicalCode))
        .AnyAsync(cancellationToken);

    private static string? ValidateAdmission(
        GskuChildIdentityKind childKind, string creationCommandId,
        string requestFingerprint, DateTimeOffset acquiredAtUtc)
    {
        if (!Enum.IsDefined(childKind) || !Exact(creationCommandId, 200)
            || requestFingerprint is not { Length: 64 }
            || requestFingerprint.Any(c => !Uri.IsHexDigit(c))
            || acquiredAtUtc.Offset != TimeSpan.Zero)
            return "GSKU_CHILD_ADMISSION_CONTRACT_INVALID";
        return null;
    }

    private bool ValidRetirementAudit(
        Guid id, int expectedVersion, LocalAuditIntent auditIntent, ProductAuditOperation operation) =>
        _tenantId != Guid.Empty && id != Guid.Empty && expectedVersion >= 0
        && auditIntent.TenantId == _tenantId && auditIntent.AggregateId == id
        && auditIntent.PreVersion == expectedVersion && auditIntent.PostVersion == expectedVersion + 1
        && auditIntent.Operation == operation && Exact(auditIntent.IdempotencyKey, 256)
        && Exact(auditIntent.EvidenceHash, 256);

    private static bool Exact(string? value, int maximum) =>
        value is { Length: > 0 } && value.Length <= maximum
        && value == value.Trim() && !value.Any(char.IsControl);

    private void EnsureIndexes()
    {
        _gskus.Indexes.CreateMany([
            new CreateIndexModel<Gsku>(Builders<Gsku>.IndexKeys.Ascending(x => x.TenantId).Ascending(x => x.CreationCommandId),
                new CreateIndexOptions { Unique = true, Name = "ux_mdm_gskus_tenant_command" }),
            new CreateIndexModel<Gsku>(Builders<Gsku>.IndexKeys.Ascending(x => x.TenantId).Ascending(x => x.CodeReservationId),
                new CreateIndexOptions { Unique = true, Name = "ux_mdm_gskus_tenant_reservation" }),
            new CreateIndexModel<Gsku>(Builders<Gsku>.IndexKeys.Ascending(x => x.TenantId).Ascending(x => x.CanonicalCode),
                new CreateIndexOptions { Unique = true, Name = "ux_mdm_gskus_tenant_code" }),
            new CreateIndexModel<Gsku>(Builders<Gsku>.IndexKeys.Ascending(x => x.TenantId).Ascending(x => x.ProductDefinitionRevisionId),
                new CreateIndexOptions { Name = "ix_mdm_gskus_tenant_revision" })
        ]);
    }

    private FilterDefinition<Gsku> TenantFilter => Builders<Gsku>.Filter.Eq(x => x.TenantId, _tenantId);
    private FilterDefinition<Gsku> ActiveFilter => TenantFilter & Builders<Gsku>.Filter.Eq(x => x.IsDeleted, false);
    private FilterDefinition<Gsku> ReferenceableFilter =>
        ActiveFilter & Builders<Gsku>.Filter.Eq(
            x => x.LifecycleStatus, ProductIdentityLifecycleStatus.IdentityApproved);
}
