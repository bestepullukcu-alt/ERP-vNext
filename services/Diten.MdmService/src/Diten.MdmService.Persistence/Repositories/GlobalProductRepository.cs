using Diten.MdmService.Application.Common;
using Diten.MdmService.Domain.Entities;
using Diten.MdmService.Domain.Enums;
using Diten.MdmService.Domain.Repositories;
using Diten.MdmService.Domain.ValueObjects;
using MongoDB.Bson;
using MongoDB.Bson.Serialization;
using MongoDB.Driver;

namespace Diten.MdmService.Persistence.Repositories;

public sealed class GlobalProductRepository : IGlobalProductRepository
{
    private readonly IMongoCollection<GlobalProduct> _globalProducts;
    private readonly IMongoCollection<CodeReservation> _reservations;
    private readonly IMongoCollection<BsonDocument> _globalProductDocuments;
    private readonly IMongoCollection<ProductDefinitionRevision> _revisions;
    private readonly IMongoCollection<Gsku> _gskus;
    private readonly Guid _tenantId;

    public GlobalProductRepository(IMongoDatabase database, ITenantContext tenantContext)
    {
        _globalProducts = database.GetCollection<GlobalProduct>("mdm_global_products");
        _reservations = database.GetCollection<CodeReservation>("mdm_code_reservations");
        _globalProductDocuments = database.GetCollection<BsonDocument>("mdm_global_products");
        _revisions = database.GetCollection<ProductDefinitionRevision>("mdm_product_definition_revisions");
        _gskus = database.GetCollection<Gsku>("mdm_gskus");
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

    public async Task<GlobalProductLifecycleWriteResult> SubmitIdentityAsync(
        Guid id,
        int expectedVersion,
        ProductIdentityWorkflowBinding workflowBinding,
        LocalAuditIntent auditIntent,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(workflowBinding);
        ArgumentNullException.ThrowIfNull(auditIntent);
        cancellationToken.ThrowIfCancellationRequested();

        var contractError = ValidateWorkflowBinding(id, workflowBinding)
                            ?? ValidateAuditIntent(
                                id,
                                expectedVersion,
                                ProductAuditOperation.GlobalProductIdentitySubmitted,
                                auditIntent);
        if (contractError is not null)
        {
            return new(false, null, contractError);
        }

        var replay = await FindReplayAsync(id, auditIntent, cancellationToken);
        if (replay is not null)
        {
            return replay.Succeeded
                   && !SameWorkflowBinding(replay.GlobalProduct?.WorkflowBinding, workflowBinding)
                ? new(false, replay.GlobalProduct, "PRODUCT_IDENTITY_IDEMPOTENCY_CONFLICT")
                : replay;
        }

        var filter = ActiveTenantFilter
                     & Builders<GlobalProduct>.Filter.Eq(x => x.Id, id)
                     & Builders<GlobalProduct>.Filter.Eq(x => x.Version, expectedVersion)
                     & Builders<GlobalProduct>.Filter.Eq(
                         x => x.LifecycleStatus,
                         ProductIdentityLifecycleStatus.Draft)
                     & Builders<GlobalProduct>.Filter.Where(
                         x => x.AuditIntents.Count < AuditIntentLimits.MaxPerAggregate);
        var update = Builders<GlobalProduct>.Update
            .Set(x => x.LifecycleStatus, ProductIdentityLifecycleStatus.PendingIdentityApproval)
            .Set(x => x.WorkflowBinding, workflowBinding)
            .Set(x => x.UpdatedAt, DateTimeOffset.UtcNow)
            .Inc(x => x.Version, 1)
            .Push(x => x.AuditIntents, auditIntent);
        var updated = await _globalProducts.FindOneAndUpdateAsync(
            filter,
            update,
            new FindOneAndUpdateOptions<GlobalProduct> { ReturnDocument = ReturnDocument.After },
            cancellationToken);
        return updated is not null
            ? new(true, updated)
            : await ClassifyLifecycleFailureAsync(id, expectedVersion, auditIntent, cancellationToken);
    }

    public async Task<GlobalProductLifecycleWriteResult> ReconcileIdentityDecisionAsync(
        Guid id,
        int expectedVersion,
        ProductIdentityWorkflowDecisionEvidence decisionEvidence,
        LocalAuditIntent auditIntent,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(decisionEvidence);
        ArgumentNullException.ThrowIfNull(auditIntent);
        cancellationToken.ThrowIfCancellationRequested();

        var operation = decisionEvidence.Decision switch
        {
            ProductIdentityDecisionKind.Approved => ProductAuditOperation.GlobalProductIdentityApproved,
            ProductIdentityDecisionKind.Rejected => ProductAuditOperation.GlobalProductIdentityRejected,
            _ => (ProductAuditOperation?)null
        };
        var contractError = operation is null
            ? "PRODUCT_IDENTITY_DECISION_INVALID"
            : ValidateDecisionEvidence(id, decisionEvidence)
              ?? ValidateAuditIntent(id, expectedVersion, operation.Value, auditIntent);
        if (contractError is not null)
        {
            return new(false, null, contractError);
        }

        var replay = await FindReplayAsync(id, auditIntent, cancellationToken);
        if (replay is not null)
        {
            return replay.Succeeded
                   && !SameDecisionEvidence(
                       replay.GlobalProduct?.WorkflowBinding?.TerminalDecision,
                       decisionEvidence)
                ? new(false, replay.GlobalProduct, "PRODUCT_IDENTITY_IDEMPOTENCY_CONFLICT")
                : replay;
        }

        var targetStatus = decisionEvidence.Decision == ProductIdentityDecisionKind.Approved
            ? ProductIdentityLifecycleStatus.IdentityApproved
            : ProductIdentityLifecycleStatus.Draft;
        var filter = ActiveTenantFilter
                     & Builders<GlobalProduct>.Filter.Eq(x => x.Id, id)
                     & Builders<GlobalProduct>.Filter.Eq(x => x.Version, expectedVersion)
                     & Builders<GlobalProduct>.Filter.Eq(
                         x => x.LifecycleStatus,
                         ProductIdentityLifecycleStatus.PendingIdentityApproval)
                     & Builders<GlobalProduct>.Filter.Eq(
                         x => x.WorkflowBinding!.WorkflowInstanceId,
                         decisionEvidence.WorkflowInstanceId)
                     & Builders<GlobalProduct>.Filter.Eq(
                         x => x.WorkflowBinding!.ApprovalTaskId,
                         decisionEvidence.ApprovalTaskId)
                     & Builders<GlobalProduct>.Filter.Eq(
                         x => x.WorkflowBinding!.WorkflowTemplateId,
                         decisionEvidence.WorkflowTemplateId)
                     & Builders<GlobalProduct>.Filter.Eq(
                         x => x.WorkflowBinding!.WorkflowTemplateVersionId,
                         decisionEvidence.WorkflowTemplateVersionId)
                     & Builders<GlobalProduct>.Filter.Eq(
                         x => x.WorkflowBinding!.ObjectType,
                         decisionEvidence.ObjectType)
                     & Builders<GlobalProduct>.Filter.Eq(
                         x => x.WorkflowBinding!.ObjectId,
                         decisionEvidence.ObjectId)
                     & Builders<GlobalProduct>.Filter.Where(
                         x => x.AuditIntents.Count < AuditIntentLimits.MaxPerAggregate);
        var update = Builders<GlobalProduct>.Update
            .Set(x => x.LifecycleStatus, targetStatus)
            .Set(x => x.WorkflowBinding!.TerminalDecision, decisionEvidence)
            .Set(x => x.UpdatedAt, DateTimeOffset.UtcNow)
            .Inc(x => x.Version, 1)
            .Push(x => x.AuditIntents, auditIntent);
        var updated = await _globalProducts.FindOneAndUpdateAsync(
            filter,
            update,
            new FindOneAndUpdateOptions<GlobalProduct> { ReturnDocument = ReturnDocument.After },
            cancellationToken);
        if (updated is not null)
        {
            return new(true, updated);
        }

        var current = await GetByIdAsync(id, cancellationToken);
        if (current is not null
            && current.Version == expectedVersion
            && current.LifecycleStatus == ProductIdentityLifecycleStatus.PendingIdentityApproval)
        {
            return new(false, current, "WORKFLOW_BINDING_CONFLICT");
        }

        return await ClassifyLifecycleFailureAsync(id, expectedVersion, auditIntent, cancellationToken);
    }

    public async Task<GlobalProductLifecycleWriteResult> RetireIdentityAsync(
        Guid id,
        int expectedVersion,
        LocalAuditIntent auditIntent,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(auditIntent);
        cancellationToken.ThrowIfCancellationRequested();
        var contractError = ValidateAuditIntent(
            id,
            expectedVersion,
            ProductAuditOperation.GlobalProductIdentityRetired,
            auditIntent);
        if (contractError is not null)
        {
            return new(false, null, contractError);
        }

        var replay = await FindReplayAsync(id, auditIntent, cancellationToken);
        if (replay is not null)
        {
            return replay;
        }

        var blocker = await FindRetirementBlockerAsync(id, cancellationToken);
        if (blocker is not null)
        {
            return new(false, await GetByIdAsync(id, cancellationToken), blocker);
        }

        var filter = ActiveTenantFilter
                     & Builders<GlobalProduct>.Filter.Eq(x => x.Id, id)
                     & Builders<GlobalProduct>.Filter.Eq(x => x.Version, expectedVersion)
                     & Builders<GlobalProduct>.Filter.Eq(
                         x => x.LifecycleStatus,
                         ProductIdentityLifecycleStatus.IdentityApproved)
                     & Builders<GlobalProduct>.Filter.Size(
                         x => x.ChildCreationAdmissions,
                         0)
                     & Builders<GlobalProduct>.Filter.Where(
                         x => x.AuditIntents.Count < AuditIntentLimits.MaxPerAggregate);
        var update = Builders<GlobalProduct>.Update
            .Set(x => x.LifecycleStatus, ProductIdentityLifecycleStatus.Retired)
            .Set(x => x.UpdatedAt, DateTimeOffset.UtcNow)
            .Inc(x => x.Version, 1)
            .Push(x => x.AuditIntents, auditIntent);
        var updated = await _globalProducts.FindOneAndUpdateAsync(
            filter,
            update,
            new FindOneAndUpdateOptions<GlobalProduct> { ReturnDocument = ReturnDocument.After },
            cancellationToken);
        if (updated is not null)
        {
            return new(true, updated);
        }

        var current = await GetByIdAsync(id, cancellationToken);
        if (current?.ChildCreationAdmissions.Count > 0)
        {
            return new(false, current, "PRODUCT_CHILD_CREATION_IN_PROGRESS");
        }

        return await ClassifyLifecycleFailureAsync(id, expectedVersion, auditIntent, cancellationToken);
    }

    public async Task<ProductChildCreationAdmissionResult> AcquireChildCreationAdmissionAsync(
        Guid id,
        string creationCommandId,
        string requestFingerprint,
        DateTimeOffset acquiredAtUtc,
        CancellationToken cancellationToken = default)
    {
        cancellationToken.ThrowIfCancellationRequested();
        var inputError = ValidateAdmissionInput(creationCommandId, requestFingerprint, acquiredAtUtc);
        if (inputError is not null)
        {
            return new(false, null, inputError);
        }

        var current = await GetByIdAsync(id, cancellationToken);
        if (current is null)
        {
            return new(false, null, "PRODUCT_IDENTITY_NOT_FOUND");
        }

        var existing = current.ChildCreationAdmissions.SingleOrDefault(
            admission => string.Equals(admission.CreationCommandId, creationCommandId, StringComparison.Ordinal));
        if (existing is not null)
        {
            return string.Equals(existing.RequestFingerprint, requestFingerprint, StringComparison.Ordinal)
                ? new(true, current, IsReplay: true)
                : new(false, current, "PRODUCT_CHILD_ADMISSION_CONFLICT");
        }
        if (current.LifecycleStatus != ProductIdentityLifecycleStatus.IdentityApproved)
        {
            return new(false, current, "PARENT_NOT_IDENTITY_APPROVED");
        }
        if (current.ChildCreationAdmissions.Count >= ProductChildCreationAdmission.MaximumActiveAdmissions)
        {
            return new(false, current, "PRODUCT_CHILD_ADMISSION_LIMIT_REACHED");
        }

        var admission = new ProductChildCreationAdmission
        {
            CreationCommandId = creationCommandId,
            RequestFingerprint = requestFingerprint,
            AcquiredAtUtc = acquiredAtUtc
        };
        var filter = ActiveTenantFilter
                     & Builders<GlobalProduct>.Filter.Eq(x => x.Id, id)
                     & Builders<GlobalProduct>.Filter.Eq(
                         x => x.LifecycleStatus,
                         ProductIdentityLifecycleStatus.IdentityApproved)
                     & Builders<GlobalProduct>.Filter.Not(
                         Builders<GlobalProduct>.Filter.ElemMatch(
                             x => x.ChildCreationAdmissions,
                             item => item.CreationCommandId == creationCommandId))
                     & Builders<GlobalProduct>.Filter.Where(
                         x => x.ChildCreationAdmissions.Count
                              < ProductChildCreationAdmission.MaximumActiveAdmissions);
        var updated = await _globalProducts.FindOneAndUpdateAsync(
            filter,
            Builders<GlobalProduct>.Update
                .Push(x => x.ChildCreationAdmissions, admission)
                .Set(x => x.UpdatedAt, DateTimeOffset.UtcNow),
            new FindOneAndUpdateOptions<GlobalProduct> { ReturnDocument = ReturnDocument.After },
            cancellationToken);
        if (updated is not null)
        {
            return new(true, updated);
        }

        current = await GetByIdAsync(id, cancellationToken);
        existing = current?.ChildCreationAdmissions.SingleOrDefault(
            item => string.Equals(item.CreationCommandId, creationCommandId, StringComparison.Ordinal));
        if (existing is not null)
        {
            return string.Equals(existing.RequestFingerprint, requestFingerprint, StringComparison.Ordinal)
                ? new(true, current, IsReplay: true)
                : new(false, current, "PRODUCT_CHILD_ADMISSION_CONFLICT");
        }
        if (current?.LifecycleStatus != ProductIdentityLifecycleStatus.IdentityApproved)
        {
            return new(false, current, "PARENT_NOT_IDENTITY_APPROVED");
        }

        return new(false, current, "PRODUCT_CHILD_ADMISSION_LIMIT_REACHED");
    }

    public async Task<ProductChildCreationAdmissionResult> CompleteChildCreationAdmissionAsync(
        Guid id,
        string creationCommandId,
        string requestFingerprint,
        CancellationToken cancellationToken = default)
    {
        cancellationToken.ThrowIfCancellationRequested();
        var inputError = ValidateAdmissionInput(
            creationCommandId,
            requestFingerprint,
            DateTimeOffset.UtcNow);
        if (inputError is not null)
        {
            return new(false, null, inputError);
        }

        var current = await GetByIdAsync(id, cancellationToken);
        if (current is null)
        {
            return new(false, null, "PRODUCT_IDENTITY_NOT_FOUND");
        }

        var durableFingerprint = await GetDurableChildPairFingerprintAsync(
            id,
            creationCommandId,
            cancellationToken);
        if (durableFingerprint is null)
        {
            return new(false, current, "PRODUCT_CHILD_BINDING_NOT_DURABLE");
        }
        if (!string.Equals(durableFingerprint, requestFingerprint, StringComparison.Ordinal))
        {
            return new(false, current, "PRODUCT_CHILD_ADMISSION_CONFLICT");
        }

        var filter = ActiveTenantFilter
                     & Builders<GlobalProduct>.Filter.Eq(x => x.Id, id)
                     & Builders<GlobalProduct>.Filter.ElemMatch(
                         x => x.ChildCreationAdmissions,
                         item => item.CreationCommandId == creationCommandId
                                 && item.RequestFingerprint == requestFingerprint);
        var updated = await _globalProducts.FindOneAndUpdateAsync(
            filter,
            Builders<GlobalProduct>.Update
                .PullFilter(
                    x => x.ChildCreationAdmissions,
                    item => item.CreationCommandId == creationCommandId
                            && item.RequestFingerprint == requestFingerprint)
                .Set(x => x.UpdatedAt, DateTimeOffset.UtcNow),
            new FindOneAndUpdateOptions<GlobalProduct> { ReturnDocument = ReturnDocument.After },
            cancellationToken);
        if (updated is not null)
        {
            return new(true, updated);
        }

        current = await GetByIdAsync(id, cancellationToken);
        if (current is null)
        {
            return new(false, null, "PRODUCT_IDENTITY_NOT_FOUND");
        }
        var existing = current.ChildCreationAdmissions.SingleOrDefault(
            item => string.Equals(item.CreationCommandId, creationCommandId, StringComparison.Ordinal));
        return existing is null
            ? new(true, current, IsReplay: true)
            : new(false, current, "PRODUCT_CHILD_ADMISSION_CONFLICT");
    }

    private async Task<string?> GetDurableChildPairFingerprintAsync(
        Guid globalProductId,
        string creationCommandId,
        CancellationToken cancellationToken)
    {
        var revision = await _revisions.Find(
                Builders<ProductDefinitionRevision>.Filter.Eq(x => x.TenantId, _tenantId)
                & Builders<ProductDefinitionRevision>.Filter.Eq(x => x.IsDeleted, false)
                & Builders<ProductDefinitionRevision>.Filter.Eq(x => x.GlobalProductId, globalProductId)
                & Builders<ProductDefinitionRevision>.Filter.Eq(x => x.CreationCommandId, creationCommandId))
            .Project(x => x.Id)
            .FirstOrDefaultAsync(cancellationToken);
        if (revision == Guid.Empty)
        {
            return null;
        }

        var gsku = await _gskus.Find(
                Builders<Gsku>.Filter.Eq(x => x.TenantId, _tenantId)
                & Builders<Gsku>.Filter.Eq(x => x.IsDeleted, false)
                & Builders<Gsku>.Filter.Eq(x => x.ProductDefinitionRevisionId, revision)
                & Builders<Gsku>.Filter.Eq(x => x.CreationCommandId, creationCommandId))
            .Project(x => new
            {
                x.CodeReservationId,
                x.PackQuantity,
                x.PackUomCode
            })
            .FirstOrDefaultAsync(cancellationToken);
        return gsku is null
            ? null
            : ProductChildCreationAdmission.ComputeRequestFingerprint(
                globalProductId,
                creationCommandId,
                gsku.CodeReservationId,
                gsku.PackQuantity,
                gsku.PackUomCode);
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

    private async Task<GlobalProductLifecycleWriteResult?> FindReplayAsync(
        Guid id,
        LocalAuditIntent auditIntent,
        CancellationToken cancellationToken)
    {
        var current = await GetByIdAsync(id, cancellationToken);
        if (current is null) return null;

        var existing = current.AuditIntents.SingleOrDefault(
            item => string.Equals(item.IdempotencyKey, auditIntent.IdempotencyKey, StringComparison.Ordinal));
        if (existing is null) return null;

        return existing.Operation == auditIntent.Operation
               && string.Equals(existing.EvidenceHash, auditIntent.EvidenceHash, StringComparison.Ordinal)
               && string.Equals(existing.CommandId, auditIntent.CommandId, StringComparison.Ordinal)
               && existing.PreVersion == auditIntent.PreVersion
               && existing.PostVersion == auditIntent.PostVersion
            ? new(true, current, IsReplay: true)
            : new(false, current, "PRODUCT_IDENTITY_IDEMPOTENCY_CONFLICT");
    }

    private async Task<GlobalProductLifecycleWriteResult> ClassifyLifecycleFailureAsync(
        Guid id,
        int expectedVersion,
        LocalAuditIntent auditIntent,
        CancellationToken cancellationToken)
    {
        var replay = await FindReplayAsync(id, auditIntent, cancellationToken);
        if (replay is not null) return replay;

        var current = await GetByIdAsync(id, cancellationToken);
        if (current is null) return new(false, null, "PRODUCT_IDENTITY_NOT_FOUND");
        if (current.Version != expectedVersion)
            return new(false, current, "PRODUCT_IDENTITY_CONCURRENCY_CONFLICT");
        if (current.AuditIntents.Count >= AuditIntentLimits.MaxPerAggregate)
            return new(false, current, "AUDIT_INTENT_CAPACITY_EXCEEDED");
        return new(false, current, "PRODUCT_IDENTITY_STATE_CONFLICT");
    }

    private async Task<string?> FindRetirementBlockerAsync(Guid globalProductId, CancellationToken cancellationToken)
    {
        var revisionFilter = Builders<ProductDefinitionRevision>.Filter.Eq(x => x.TenantId, _tenantId)
                             & Builders<ProductDefinitionRevision>.Filter.Eq(x => x.GlobalProductId, globalProductId);
        var revisions = await _revisions.Find(revisionFilter)
            .Project(x => new { x.Id, x.IsDeleted, x.LifecycleStatus })
            .ToListAsync(cancellationToken);
        var activeRevisions = revisions.Where(item => !item.IsDeleted).ToArray();
        if (activeRevisions.Any(item => item.LifecycleStatus is ProductIdentityLifecycleStatus.Draft
                or ProductIdentityLifecycleStatus.PendingIdentityApproval))
            return "DRAFT_CHILD_CANCELLATION_REQUIRED";
        if (activeRevisions.Any(item => item.LifecycleStatus == ProductIdentityLifecycleStatus.IdentityApproved))
            return "DEPENDENT_IDENTITIES_EXIST";

        var revisionIds = revisions.Select(item => item.Id).ToArray();
        if (revisionIds.Length == 0) return null;

        var gskuFilter = Builders<Gsku>.Filter.Eq(x => x.TenantId, _tenantId)
                         & Builders<Gsku>.Filter.Eq(x => x.IsDeleted, false)
                         & Builders<Gsku>.Filter.In(x => x.ProductDefinitionRevisionId, revisionIds)
                         & Builders<Gsku>.Filter.Ne(x => x.LifecycleStatus, ProductIdentityLifecycleStatus.Retired);
        var gskuStatus = await _gskus.Find(gskuFilter)
            .Project(x => x.LifecycleStatus)
            .FirstOrDefaultAsync(cancellationToken);
        return gskuStatus switch
        {
            ProductIdentityLifecycleStatus.Draft or ProductIdentityLifecycleStatus.PendingIdentityApproval
                => "DRAFT_CHILD_CANCELLATION_REQUIRED",
            ProductIdentityLifecycleStatus.IdentityApproved => "DEPENDENT_IDENTITIES_EXIST",
            _ => null
        };
    }

    private string? ValidateAuditIntent(
        Guid aggregateId,
        int expectedVersion,
        ProductAuditOperation operation,
        LocalAuditIntent auditIntent)
    {
        if (aggregateId == Guid.Empty || expectedVersion < 0
            || auditIntent.TenantId != _tenantId
            || auditIntent.AggregateType != AuditAggregateType.GlobalProduct
            || auditIntent.AggregateId != aggregateId
            || auditIntent.Operation != operation
            || auditIntent.PreVersion != expectedVersion
            || auditIntent.PostVersion != expectedVersion + 1
            || auditIntent.IntentId == Guid.Empty
            || auditIntent.Sequence != expectedVersion + 1L
            || !Guid.TryParse(auditIntent.ActorId, out var actorId) || actorId == Guid.Empty
            || string.IsNullOrWhiteSpace(auditIntent.IdempotencyKey)
            || string.IsNullOrWhiteSpace(auditIntent.EvidenceHash)
            || string.IsNullOrWhiteSpace(auditIntent.CommandId)
            || auditIntent.TimestampUtc.Offset != TimeSpan.Zero)
            return "AUDIT_INTENT_CONTRACT_INVALID";
        return null;
    }

    private static string? ValidateWorkflowBinding(Guid aggregateId, ProductIdentityWorkflowBinding binding)
    {
        if (binding.WorkflowInstanceId == Guid.Empty
            || binding.WorkflowTemplateId == Guid.Empty
            || binding.WorkflowTemplateVersionId == Guid.Empty
            || binding.ApprovalTaskId == Guid.Empty
            || binding.AssignmentSnapshotId == Guid.Empty
            || binding.StartTransitionLogId == Guid.Empty
            || !string.Equals(binding.ObjectType, "global-product", StringComparison.Ordinal)
            || binding.ObjectId != aggregateId
            || string.IsNullOrWhiteSpace(binding.ObjectRef)
            || binding.SubmitterSubjectId == Guid.Empty
            || string.IsNullOrWhiteSpace(binding.StartIdempotencyKey)
            || string.IsNullOrWhiteSpace(binding.StartRequestFingerprint)
            || binding.SubmittedAtUtc.Offset != TimeSpan.Zero
            || (binding.DueAtUtc is { } dueAtUtc && dueAtUtc.Offset != TimeSpan.Zero)
            || binding.TerminalDecision is not null)
            return "WORKFLOW_BINDING_CONTRACT_INVALID";
        return null;
    }

    private static string? ValidateDecisionEvidence(Guid aggregateId, ProductIdentityWorkflowDecisionEvidence evidence)
    {
        if (evidence.WorkflowInstanceId == Guid.Empty
            || evidence.ApprovalTaskId == Guid.Empty
            || evidence.WorkflowTemplateId == Guid.Empty
            || evidence.WorkflowTemplateVersionId == Guid.Empty
            || !string.Equals(evidence.ObjectType, "global-product", StringComparison.Ordinal)
            || evidence.ObjectId != aggregateId
            || string.IsNullOrWhiteSpace(evidence.ObjectRef)
            || evidence.DecisionActorSubjectId == Guid.Empty
            || evidence.DecisionAtUtc.Offset != TimeSpan.Zero
            || evidence.TransitionSequence < 1
            || string.IsNullOrWhiteSpace(evidence.TaskStatus)
            || string.IsNullOrWhiteSpace(evidence.InstanceStatus)
            || (evidence.Decision == ProductIdentityDecisionKind.Rejected
                && string.IsNullOrWhiteSpace(evidence.ReasonCode)))
            return "WORKFLOW_DECISION_EVIDENCE_INVALID";
        return null;
    }

    private static string? ValidateAdmissionInput(
        string creationCommandId,
        string requestFingerprint,
        DateTimeOffset acquiredAtUtc)
    {
        if (string.IsNullOrWhiteSpace(creationCommandId) || creationCommandId.Length > 200
            || string.IsNullOrWhiteSpace(requestFingerprint) || requestFingerprint.Length > 256
            || acquiredAtUtc.Offset != TimeSpan.Zero)
            return "PRODUCT_CHILD_ADMISSION_CONTRACT_INVALID";
        return null;
    }

    private static bool SameWorkflowBinding(
        ProductIdentityWorkflowBinding? left,
        ProductIdentityWorkflowBinding right) =>
        left is not null
        && left.WorkflowInstanceId == right.WorkflowInstanceId
        && left.WorkflowTemplateId == right.WorkflowTemplateId
        && left.WorkflowTemplateVersionId == right.WorkflowTemplateVersionId
        && left.ApprovalTaskId == right.ApprovalTaskId
        && left.AssignmentSnapshotId == right.AssignmentSnapshotId
        && left.StartTransitionLogId == right.StartTransitionLogId
        && string.Equals(left.ObjectType, right.ObjectType, StringComparison.Ordinal)
        && left.ObjectId == right.ObjectId
        && string.Equals(left.ObjectRef, right.ObjectRef, StringComparison.Ordinal)
        && left.SubmitterSubjectId == right.SubmitterSubjectId
        && string.Equals(left.StartIdempotencyKey, right.StartIdempotencyKey, StringComparison.Ordinal)
        && string.Equals(left.StartRequestFingerprint, right.StartRequestFingerprint, StringComparison.Ordinal)
        && left.SubmittedAtUtc == right.SubmittedAtUtc
        && left.DueAtUtc == right.DueAtUtc;

    private static bool SameDecisionEvidence(
        ProductIdentityWorkflowDecisionEvidence? left,
        ProductIdentityWorkflowDecisionEvidence right) =>
        left is not null
        && left.Decision == right.Decision
        && left.WorkflowInstanceId == right.WorkflowInstanceId
        && left.ApprovalTaskId == right.ApprovalTaskId
        && left.WorkflowTemplateId == right.WorkflowTemplateId
        && left.WorkflowTemplateVersionId == right.WorkflowTemplateVersionId
        && string.Equals(left.ObjectType, right.ObjectType, StringComparison.Ordinal)
        && left.ObjectId == right.ObjectId
        && string.Equals(left.ObjectRef, right.ObjectRef, StringComparison.Ordinal)
        && left.DecisionActorSubjectId == right.DecisionActorSubjectId
        && string.Equals(left.ReasonCode, right.ReasonCode, StringComparison.Ordinal)
        && left.DecisionAtUtc == right.DecisionAtUtc
        && left.TransitionSequence == right.TransitionSequence
        && string.Equals(left.TaskStatus, right.TaskStatus, StringComparison.Ordinal)
        && string.Equals(left.InstanceStatus, right.InstanceStatus, StringComparison.Ordinal);

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
