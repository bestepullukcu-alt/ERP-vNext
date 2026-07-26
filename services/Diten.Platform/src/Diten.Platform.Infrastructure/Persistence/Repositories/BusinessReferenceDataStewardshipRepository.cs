using Diten.Platform.Common.Persistence;
using Diten.Platform.Common.Tenancy;
using Diten.Platform.Domain.Entities;
using Diten.Platform.Domain.Repositories;
using Diten.Platform.Infrastructure.Persistence;
using MongoDB.Bson;
using MongoDB.Bson.Serialization;
using MongoDB.Driver;

namespace Diten.Platform.Infrastructure.Persistence.Repositories.BusinessReferenceData;

public sealed class BusinessReferenceDataStewardshipRepository : TenantRepository<BusinessReferenceDataSet>, IBusinessReferenceDataStewardshipRepository
{
    private const string CatalogPublishIdempotencyPrefix = "BusinessReferenceData-catalog-v";
    private readonly IMongoCollection<BusinessReferenceDataVersion> _versions;
    private readonly IMongoCollection<BsonDocument> _versionDocuments;
    private readonly IMongoCollection<BsonDocument> _setDocuments;
    private readonly IMongoCollection<BusinessReferenceDataValidationResult> _validationResults;
    private readonly IMongoCollection<BusinessReferenceDataIntegrationEvent> _integrationEvents;
    private readonly IMongoCollection<BusinessReferenceDataUsageRegistration> _usageRegistrations;
    private readonly IMongoCollection<BusinessReferenceDataImportPreview> _importPreviews;

    public BusinessReferenceDataStewardshipRepository(IPlatformDbContext dbContext, ITenantContext tenantContext)
        : base(dbContext.Database, tenantContext, "business_reference_data_sets")
    {
        _setDocuments = dbContext.Database.GetCollection<BsonDocument>("business_reference_data_sets");
        _versionDocuments = dbContext.Database.GetCollection<BsonDocument>("business_reference_data_versions");
        _versions = dbContext.GetCollection<BusinessReferenceDataVersion>("business_reference_data_versions");
        _validationResults = dbContext.GetCollection<BusinessReferenceDataValidationResult>("business_reference_data_validation_results");
        _integrationEvents = dbContext.GetCollection<BusinessReferenceDataIntegrationEvent>("business_reference_data_integration_events");
        _usageRegistrations = dbContext.GetCollection<BusinessReferenceDataUsageRegistration>("business_reference_data_usage_registrations");
        _importPreviews = dbContext.GetCollection<BusinessReferenceDataImportPreview>("business_reference_data_import_previews");
    }

    public async Task<(IReadOnlyList<BusinessReferenceDataSet> Items, long TotalCount)> QuerySetsAsync(BusinessReferenceDataSetListQuery query, CancellationToken ct = default)
    {
        var filters = new List<FilterDefinition<BusinessReferenceDataSet>>
        {
            Builders<BusinessReferenceDataSet>.Filter.Eq(x => x.TenantId, TenantContext.TenantId),
            Builders<BusinessReferenceDataSet>.Filter.Eq(x => x.IsDeleted, false)
        };

        if (!string.IsNullOrWhiteSpace(query.Search))
        {
            var regex = new BsonRegularExpression(System.Text.RegularExpressions.Regex.Escape(query.Search.Trim()), "i");
            filters.Add(Builders<BusinessReferenceDataSet>.Filter.Or(
                Builders<BusinessReferenceDataSet>.Filter.Regex(x => x.SetCode, regex),
                Builders<BusinessReferenceDataSet>.Filter.Regex(x => x.Name, regex),
                Builders<BusinessReferenceDataSet>.Filter.Regex(x => x.Description, regex)));
        }

        if (!string.IsNullOrWhiteSpace(query.Status) && Enum.TryParse<BusinessReferenceDataSetStatus>(query.Status, true, out var parsedStatus))
        {
            filters.Add(Builders<BusinessReferenceDataSet>.Filter.Eq(x => x.Status, parsedStatus));
        }

        if (!string.IsNullOrWhiteSpace(query.ScopeType))
        {
            filters.Add(Builders<BusinessReferenceDataSet>.Filter.Eq(x => x.ScopeType, query.ScopeType.Trim()));
        }

        if (query.CatalogGovernedOnly)
        {
            var governedVersionFilter = Builders<BusinessReferenceDataVersion>.Filter.And(
                Builders<BusinessReferenceDataVersion>.Filter.Eq(x => x.TenantId, TenantContext.TenantId),
                Builders<BusinessReferenceDataVersion>.Filter.Eq(x => x.IsDeleted, false),
                Builders<BusinessReferenceDataVersion>.Filter.Regex(
                    x => x.LastPublishIdempotencyKey,
                    new BsonRegularExpression($"^{CatalogPublishIdempotencyPrefix}", "i")));

            var governedSetIdsCursor = await _versions.DistinctAsync<Guid>(
                "BusinessReferenceDataSetId",
                governedVersionFilter,
                cancellationToken: ct);
            var governedSetIds = await governedSetIdsCursor.ToListAsync(ct);

            if (governedSetIds.Count == 0)
            {
                return (Array.Empty<BusinessReferenceDataSet>(), 0);
            }

            filters.Add(Builders<BusinessReferenceDataSet>.Filter.In(x => x.BusinessReferenceDataSetId, governedSetIds));
        }

        var renderedFilter = RenderSetFilter(Builders<BusinessReferenceDataSet>.Filter.And(filters));
        var totalCount = await _setDocuments.CountDocumentsAsync(renderedFilter, cancellationToken: ct);
        var sort = BuildSortDocument(query.Sort);
        var page = Math.Max(query.Page, 1);
        var pageSize = Math.Clamp(query.PageSize, 1, 200);

        var documents = await _setDocuments.Find(renderedFilter)
            .Sort(sort)
            .Skip((page - 1) * pageSize)
            .Limit(pageSize)
            .Project(Builders<BsonDocument>.Projection.Exclude("_id"))
            .ToListAsync(ct);

        var items = documents.Select(DeserializeSetDocument).ToList();
        return (items, totalCount);
    }

    public Task<bool> IsCatalogGovernedSetAsync(Guid setId, CancellationToken ct = default)
    {
        var filter = Builders<BusinessReferenceDataVersion>.Filter.And(
            Builders<BusinessReferenceDataVersion>.Filter.Eq(x => x.TenantId, TenantContext.TenantId),
            Builders<BusinessReferenceDataVersion>.Filter.Eq(x => x.BusinessReferenceDataSetId, setId),
            Builders<BusinessReferenceDataVersion>.Filter.Eq(x => x.IsDeleted, false),
            Builders<BusinessReferenceDataVersion>.Filter.Regex(
                x => x.LastPublishIdempotencyKey,
                new BsonRegularExpression($"^{CatalogPublishIdempotencyPrefix}", "i")));

        return _versions.Find(filter).Limit(1).AnyAsync(ct);
    }

    public async Task<bool> IsCatalogGovernedVersionAsync(Guid versionId, CancellationToken ct = default)
    {
        var version = await GetVersionByIdAsync(versionId, ct);
        if (version is null)
        {
            return false;
        }

        return await IsCatalogGovernedSetAsync(version.BusinessReferenceDataSetId, ct);
    }

    public async Task<BusinessReferenceDataSet?> GetSetByIdAsync(Guid setId, CancellationToken ct = default)
    {
        var filter = Builders<BusinessReferenceDataSet>.Filter.And(
            Builders<BusinessReferenceDataSet>.Filter.Eq(x => x.TenantId, TenantContext.TenantId),
            Builders<BusinessReferenceDataSet>.Filter.Eq(x => x.BusinessReferenceDataSetId, setId),
            Builders<BusinessReferenceDataSet>.Filter.Eq(x => x.IsDeleted, false));
        var document = await _setDocuments.Find(RenderSetFilter(filter))
            .Project(Builders<BsonDocument>.Projection.Exclude("_id"))
            .FirstOrDefaultAsync(ct);
        return document is null ? null : DeserializeSetDocument(document);
    }

    public async Task<BusinessReferenceDataSet?> GetSetByCodeAsync(string setCode, CancellationToken ct = default)
    {
        var normalized = setCode.Trim();
        var filter = Builders<BusinessReferenceDataSet>.Filter.And(
            Builders<BusinessReferenceDataSet>.Filter.Eq(x => x.TenantId, TenantContext.TenantId),
            Builders<BusinessReferenceDataSet>.Filter.Eq(x => x.SetCode, normalized),
            Builders<BusinessReferenceDataSet>.Filter.Eq(x => x.IsDeleted, false));
        var document = await _setDocuments.Find(RenderSetFilter(filter))
            .Project(Builders<BsonDocument>.Projection.Exclude("_id"))
            .FirstOrDefaultAsync(ct);
        return document is null ? null : DeserializeSetDocument(document);
    }

    public Task CreateSetAsync(BusinessReferenceDataSet entity, CancellationToken ct = default)
    {
        return CreateAsync(entity, ct);
    }

    public async Task<bool> UpdateSetAsync(BusinessReferenceDataSet entity, long expectedRowVersion, CancellationToken ct = default)
    {
        var updatedAt = DateTimeOffset.UtcNow;
        var nextRowVersion = expectedRowVersion + 1;

        var filter = Builders<BusinessReferenceDataSet>.Filter.And(
            Builders<BusinessReferenceDataSet>.Filter.Eq(x => x.TenantId, TenantContext.TenantId),
            Builders<BusinessReferenceDataSet>.Filter.Eq(x => x.BusinessReferenceDataSetId, entity.BusinessReferenceDataSetId),
            Builders<BusinessReferenceDataSet>.Filter.Eq(x => x.IsDeleted, false),
            Builders<BusinessReferenceDataSet>.Filter.Eq(x => x.RowVersion, expectedRowVersion));

        var update = Builders<BusinessReferenceDataSet>.Update
            .Set(x => x.SetCode, entity.SetCode)
            .Set(x => x.Name, entity.Name)
            .Set(x => x.ScopeType, entity.ScopeType)
            .Set(x => x.Description, entity.Description)
            .Set(x => x.Status, entity.Status)
            .Set(x => x.ActiveDraftVersionId, entity.ActiveDraftVersionId)
            .Set(x => x.PublishedVersionId, entity.PublishedVersionId)
            .Set(x => x.LastCorrelationId, entity.LastCorrelationId)
            .Set(x => x.UsageRegistrationCount, entity.UsageRegistrationCount)
            .Set(x => x.CriticalUsageCount, entity.CriticalUsageCount)
            .Set(x => x.LastUsageRegisteredAt, entity.LastUsageRegisteredAt)
            .Set(x => x.RowVersion, nextRowVersion)
            .Set(x => x.UpdatedAt, updatedAt)
            .Set(x => x.UpdatedBy, entity.UpdatedBy);

        var result = await Collection.UpdateOneAsync(filter, update, cancellationToken: ct);
        if (result.ModifiedCount > 0)
        {
            entity.RowVersion = nextRowVersion;
            entity.UpdatedAt = updatedAt;
        }

        return result.ModifiedCount > 0;
    }

    public async Task<BusinessReferenceDataVersion?> GetVersionByIdAsync(Guid versionId, CancellationToken ct = default)
    {
        var filter = Builders<BusinessReferenceDataVersion>.Filter.And(
            Builders<BusinessReferenceDataVersion>.Filter.Eq(x => x.TenantId, TenantContext.TenantId),
            Builders<BusinessReferenceDataVersion>.Filter.Eq(x => x.BusinessReferenceDataVersionId, versionId),
            Builders<BusinessReferenceDataVersion>.Filter.Eq(x => x.IsDeleted, false));
        var document = await _versionDocuments.Find(RenderVersionFilter(filter))
            .Project(Builders<BsonDocument>.Projection.Exclude("_id"))
            .FirstOrDefaultAsync(ct);
        return document is null ? null : DeserializeVersionDocument(document);
    }

    public async Task<IReadOnlyList<BusinessReferenceDataVersion>> GetVersionsByIdsAsync(IReadOnlyCollection<Guid> versionIds, CancellationToken ct = default)
    {
        if (versionIds is null || versionIds.Count == 0)
        {
            return [];
        }

        var distinctIds = versionIds.Where(id => id != Guid.Empty).Distinct().ToList();
        if (distinctIds.Count == 0)
        {
            return [];
        }

        var filter = Builders<BusinessReferenceDataVersion>.Filter.And(
            Builders<BusinessReferenceDataVersion>.Filter.Eq(x => x.TenantId, TenantContext.TenantId),
            Builders<BusinessReferenceDataVersion>.Filter.In(x => x.BusinessReferenceDataVersionId, distinctIds),
            Builders<BusinessReferenceDataVersion>.Filter.Eq(x => x.IsDeleted, false));

        var documents = await _versionDocuments.Find(RenderVersionFilter(filter))
            .Project(Builders<BsonDocument>.Projection.Exclude("_id"))
            .ToListAsync(ct);

        return documents.Select(DeserializeVersionDocument).ToList();
    }

    public async Task<IReadOnlyList<BusinessReferenceDataVersion>> GetVersionsBySetIdAsync(Guid setId, CancellationToken ct = default)
    {
        var filter = Builders<BusinessReferenceDataVersion>.Filter.And(
            Builders<BusinessReferenceDataVersion>.Filter.Eq(x => x.TenantId, TenantContext.TenantId),
            Builders<BusinessReferenceDataVersion>.Filter.Eq(x => x.BusinessReferenceDataSetId, setId),
            Builders<BusinessReferenceDataVersion>.Filter.Eq(x => x.IsDeleted, false));

        var documents = await _versionDocuments.Find(RenderVersionFilter(filter))
            .Sort(Builders<BsonDocument>.Sort
                .Descending(nameof(BusinessReferenceDataVersion.VersionNumber))
                .Descending(nameof(BusinessReferenceDataVersion.CreatedAt)))
            .Project(Builders<BsonDocument>.Projection.Exclude("_id"))
            .ToListAsync(ct);

        return documents.Select(DeserializeVersionDocument).ToList();
    }

    public async Task<int> GetNextVersionNumberAsync(Guid setId, CancellationToken ct = default)
    {
        var filter = Builders<BusinessReferenceDataVersion>.Filter.And(
            Builders<BusinessReferenceDataVersion>.Filter.Eq(x => x.TenantId, TenantContext.TenantId),
            Builders<BusinessReferenceDataVersion>.Filter.Eq(x => x.BusinessReferenceDataSetId, setId),
            Builders<BusinessReferenceDataVersion>.Filter.Eq(x => x.IsDeleted, false));

        var latest = await _versions.Find(filter)
            .SortByDescending(x => x.VersionNumber)
            .Limit(1)
            .FirstOrDefaultAsync(ct);

        return latest is null ? 1 : latest.VersionNumber + 1;
    }

    public async Task<IReadOnlyList<BusinessReferenceDataVersion>> GetPublishedVersionsBySetAsync(Guid setId, Guid excludingVersionId, CancellationToken ct = default)
    {
        var filter = Builders<BusinessReferenceDataVersion>.Filter.And(
            Builders<BusinessReferenceDataVersion>.Filter.Eq(x => x.TenantId, TenantContext.TenantId),
            Builders<BusinessReferenceDataVersion>.Filter.Eq(x => x.BusinessReferenceDataSetId, setId),
            Builders<BusinessReferenceDataVersion>.Filter.Ne(x => x.BusinessReferenceDataVersionId, excludingVersionId),
            Builders<BusinessReferenceDataVersion>.Filter.Eq(x => x.Status, BusinessReferenceDataVersionStatus.Published),
            Builders<BusinessReferenceDataVersion>.Filter.Eq(x => x.IsDeleted, false));

        return await _versions.Find(filter).ToListAsync(ct);
    }

    public async Task<bool> HasActiveDraftVersionAsync(Guid setId, CancellationToken ct = default)
    {
        var filter = Builders<BusinessReferenceDataVersion>.Filter.And(
            Builders<BusinessReferenceDataVersion>.Filter.Eq(x => x.TenantId, TenantContext.TenantId),
            Builders<BusinessReferenceDataVersion>.Filter.Eq(x => x.BusinessReferenceDataSetId, setId),
            Builders<BusinessReferenceDataVersion>.Filter.Eq(x => x.Status, BusinessReferenceDataVersionStatus.Draft),
            Builders<BusinessReferenceDataVersion>.Filter.Eq(x => x.IsDeleted, false));

        return await _versions.Find(filter).Limit(1).AnyAsync(ct);
    }

    public Task CreateVersionAsync(BusinessReferenceDataVersion version, CancellationToken ct = default)
    {
        if (string.IsNullOrWhiteSpace(version.ConcurrencyToken))
        {
            version.ConcurrencyToken = Guid.NewGuid().ToString("N");
        }

        return _versions.InsertOneAsync(version, cancellationToken: ct);
    }

    public async Task<bool> UpdateVersionAsync(BusinessReferenceDataVersion version, string expectedConcurrencyToken, CancellationToken ct = default)
    {
        var updatedAt = DateTimeOffset.UtcNow;
        var nextToken = Guid.NewGuid().ToString("N");

        var filter = Builders<BusinessReferenceDataVersion>.Filter.And(
            Builders<BusinessReferenceDataVersion>.Filter.Eq(x => x.TenantId, TenantContext.TenantId),
            Builders<BusinessReferenceDataVersion>.Filter.Eq(x => x.BusinessReferenceDataVersionId, version.BusinessReferenceDataVersionId),
            Builders<BusinessReferenceDataVersion>.Filter.Eq(x => x.IsDeleted, false),
            Builders<BusinessReferenceDataVersion>.Filter.Eq(x => x.ConcurrencyToken, expectedConcurrencyToken));

        var update = Builders<BusinessReferenceDataVersion>.Update
            .Set(x => x.VersionNumber, version.VersionNumber)
            .Set(x => x.Status, version.Status)
            .Set(x => x.ConcurrencyToken, nextToken)
            .Set(x => x.IsImmutable, version.IsImmutable)
            .Set(x => x.PublishedAt, version.PublishedAt)
            .Set(x => x.PublishedBy, version.PublishedBy)
            .Set(x => x.SourceVersionId, version.SourceVersionId)
            .Set(x => x.TargetDraftVersionId, version.TargetDraftVersionId)
            .Set(x => x.CopyActor, version.CopyActor)
            .Set(x => x.CopiedAt, version.CopiedAt)
            .Set(x => x.LastCorrelationId, version.LastCorrelationId)
            .Set(x => x.RequiresEvidence, version.RequiresEvidence)
            .Set(x => x.EvidenceAttached, version.EvidenceAttached)
            .Set(x => x.RequiresApproval, version.RequiresApproval)
            .Set(x => x.ApprovedAt, version.ApprovedAt)
            .Set(x => x.PublishWindowStart, version.PublishWindowStart)
            .Set(x => x.PublishWindowEnd, version.PublishWindowEnd)
            .Set(x => x.BusinessReferenceDataGovernanceState, version.BusinessReferenceDataGovernanceState)
            .Set(x => x.BusinessReferenceDataApprovalState, version.BusinessReferenceDataApprovalState)
            .Set(x => x.IsEditable, version.IsEditable)
            .Set(x => x.SubmittedAt, version.SubmittedAt)
            .Set(x => x.SubmittedBy, version.SubmittedBy)
            .Set(x => x.DecisionAt, version.DecisionAt)
            .Set(x => x.DecisionBy, version.DecisionBy)
            .Set(x => x.RejectionReason, version.RejectionReason)
            .Set(x => x.WorkflowTemplateCode, version.WorkflowTemplateCode)
            .Set(x => x.WorkflowInstanceId, version.WorkflowInstanceId)
            .Set(x => x.WorkflowState, version.WorkflowState)
            .Set(x => x.IsOverrideAction, version.IsOverrideAction)
            .Set(x => x.OverrideReason, version.OverrideReason)
            .Set(x => x.LastEvidenceRef, version.LastEvidenceRef)
            .Set(x => x.EvidenceLinkId, version.EvidenceLinkId)
            .Set(x => x.EvidenceEvaluationId, version.EvidenceEvaluationId)
            .Set(x => x.EvidenceDocumentVersionId, version.EvidenceDocumentVersionId)
            .Set(x => x.EvidenceRequirementCode, version.EvidenceRequirementCode)
            .Set(x => x.EvidenceDecisionCode, version.EvidenceDecisionCode)
            .Set(x => x.EvidenceReasonCode, version.EvidenceReasonCode)
            .Set(x => x.LastPublishIdempotencyKey, version.LastPublishIdempotencyKey)
            .Set(x => x.LastPublishMode, version.LastPublishMode)
            .Set(x => x.PublishedSnapshotJson, version.PublishedSnapshotJson)
            .Set(x => x.DeprecatedValuesEffectiveCount, version.DeprecatedValuesEffectiveCount)
            .Set(x => x.EffectiveFrom, version.EffectiveFrom)
            .Set(x => x.EffectiveTo, version.EffectiveTo)
            .Set(x => x.ScopeKey, version.ScopeKey)
            .Set(x => x.Values, version.Values)
            .Set(x => x.AttributeDefinitions, version.AttributeDefinitions)
            .Set(x => x.Mappings, version.Mappings)
            .Set(x => x.SupersededByVersionId, version.SupersededByVersionId)
            .Set(x => x.UpdatedAt, updatedAt)
            .Set(x => x.UpdatedBy, version.UpdatedBy);

        var result = await _versions.UpdateOneAsync(filter, update, cancellationToken: ct);
        if (result.ModifiedCount > 0)
        {
            version.ConcurrencyToken = nextToken;
            version.UpdatedAt = updatedAt;
        }

        return result.ModifiedCount > 0;
    }

    public async Task<int> DeprecatePublishedVersionsAsync(Guid setId, Guid keepVersionId, Guid supersededByVersionId, CancellationToken ct = default)
    {
        var filter = Builders<BusinessReferenceDataVersion>.Filter.And(
            Builders<BusinessReferenceDataVersion>.Filter.Eq(x => x.TenantId, TenantContext.TenantId),
            Builders<BusinessReferenceDataVersion>.Filter.Eq(x => x.BusinessReferenceDataSetId, setId),
            Builders<BusinessReferenceDataVersion>.Filter.Ne(x => x.BusinessReferenceDataVersionId, keepVersionId),
            Builders<BusinessReferenceDataVersion>.Filter.Eq(x => x.Status, BusinessReferenceDataVersionStatus.Published),
            Builders<BusinessReferenceDataVersion>.Filter.Eq(x => x.IsDeleted, false));

        var update = Builders<BusinessReferenceDataVersion>.Update
            .Set(x => x.Status, BusinessReferenceDataVersionStatus.Deprecated)
            .Set(x => x.SupersededByVersionId, supersededByVersionId)
            .Set(x => x.UpdatedAt, DateTimeOffset.UtcNow);

        var result = await _versions.UpdateManyAsync(filter, update, cancellationToken: ct);
        return (int)result.ModifiedCount;
    }

    public async Task ReplaceValidationResultsAsync(Guid versionId, IReadOnlyList<BusinessReferenceDataValidationResult> results, CancellationToken ct = default)
    {
        var deleteFilter = Builders<BusinessReferenceDataValidationResult>.Filter.And(
            Builders<BusinessReferenceDataValidationResult>.Filter.Eq(x => x.TenantId, TenantContext.TenantId),
            Builders<BusinessReferenceDataValidationResult>.Filter.Eq(x => x.BusinessReferenceDataVersionId, versionId));

        await _validationResults.DeleteManyAsync(deleteFilter, ct);

        if (results.Count == 0)
        {
            return;
        }

        var docs = results.Select(x => new BusinessReferenceDataValidationResult
        {
            TenantId = TenantContext.TenantId,
            BusinessReferenceDataVersionId = x.BusinessReferenceDataVersionId,
            RuleId = x.RuleId,
            Severity = x.Severity,
            IsBlocking = x.IsBlocking,
            Message = x.Message,
            IsStubbed = x.IsStubbed,
            StubReason = x.StubReason,
            ExecutedAt = x.ExecutedAt,
            CorrelationId = x.CorrelationId,
            CreatedBy = string.IsNullOrWhiteSpace(x.CreatedBy) ? "system" : x.CreatedBy
        }).ToList();

        await _validationResults.InsertManyAsync(docs, cancellationToken: ct);
    }

    public async Task<IReadOnlyList<BusinessReferenceDataValidationResult>> GetValidationResultsByVersionAsync(Guid versionId, CancellationToken ct = default)
    {
        var filter = Builders<BusinessReferenceDataValidationResult>.Filter.And(
            Builders<BusinessReferenceDataValidationResult>.Filter.Eq(x => x.TenantId, TenantContext.TenantId),
            Builders<BusinessReferenceDataValidationResult>.Filter.Eq(x => x.BusinessReferenceDataVersionId, versionId),
            Builders<BusinessReferenceDataValidationResult>.Filter.Eq(x => x.IsDeleted, false));

        return await _validationResults.Find(filter)
            .SortBy(x => x.RuleId)
            .ToListAsync(ct);
    }

    public async Task<bool> IntegrationEventExistsAsync(Guid versionId, string eventName, string idempotencyKey, CancellationToken ct = default)
    {
        var filter = Builders<BusinessReferenceDataIntegrationEvent>.Filter.And(
            Builders<BusinessReferenceDataIntegrationEvent>.Filter.Eq(x => x.TenantId, TenantContext.TenantId),
            Builders<BusinessReferenceDataIntegrationEvent>.Filter.Eq(x => x.BusinessReferenceDataVersionId, versionId),
            Builders<BusinessReferenceDataIntegrationEvent>.Filter.Eq(x => x.EventName, eventName),
            Builders<BusinessReferenceDataIntegrationEvent>.Filter.Eq(x => x.IdempotencyKey, idempotencyKey),
            Builders<BusinessReferenceDataIntegrationEvent>.Filter.Eq(x => x.IsDeleted, false));

        return await _integrationEvents.Find(filter).Limit(1).AnyAsync(ct);
    }

    public Task SaveIntegrationEventAsync(BusinessReferenceDataIntegrationEvent integrationEvent, CancellationToken ct = default)
    {
        var doc = new BusinessReferenceDataIntegrationEvent
        {
            TenantId = TenantContext.TenantId,
            BusinessReferenceDataVersionId = integrationEvent.BusinessReferenceDataVersionId,
            EventName = integrationEvent.EventName,
            EventVersion = integrationEvent.EventVersion,
            IdempotencyKey = integrationEvent.IdempotencyKey,
            PayloadJson = integrationEvent.PayloadJson,
            EmittedAt = integrationEvent.EmittedAt,
            CreatedBy = string.IsNullOrWhiteSpace(integrationEvent.CreatedBy) ? "system" : integrationEvent.CreatedBy
        };

        return _integrationEvents.InsertOneAsync(doc, cancellationToken: ct);
    }

    public async Task<IReadOnlyList<BusinessReferenceDataVersion>> GetPublishedVersionsBySetCodeAsync(string setCode, CancellationToken ct = default)
    {
        var normalized = setCode.Trim();
        var setFilter = Builders<BusinessReferenceDataSet>.Filter.And(
            Builders<BusinessReferenceDataSet>.Filter.Eq(x => x.TenantId, TenantContext.TenantId),
            Builders<BusinessReferenceDataSet>.Filter.Eq(x => x.SetCode, normalized),
            Builders<BusinessReferenceDataSet>.Filter.Eq(x => x.IsDeleted, false));

        var setDocument = await _setDocuments.Find(RenderSetFilter(setFilter))
            .Project(Builders<BsonDocument>.Projection.Exclude("_id"))
            .FirstOrDefaultAsync(ct);
        var set = setDocument is null ? null : DeserializeSetDocument(setDocument);
        if (set is null)
        {
            return [];
        }

        var versionFilter = Builders<BusinessReferenceDataVersion>.Filter.And(
            Builders<BusinessReferenceDataVersion>.Filter.Eq(x => x.TenantId, TenantContext.TenantId),
            Builders<BusinessReferenceDataVersion>.Filter.Eq(x => x.BusinessReferenceDataSetId, set.BusinessReferenceDataSetId),
            Builders<BusinessReferenceDataVersion>.Filter.Eq(x => x.Status, BusinessReferenceDataVersionStatus.Published),
            Builders<BusinessReferenceDataVersion>.Filter.Eq(x => x.IsDeleted, false));

        return await _versions.Find(versionFilter)
            .SortByDescending(x => x.VersionNumber)
            .ToListAsync(ct);
    }

    public async Task<BusinessReferenceDataUsageRegistration> UpsertUsageRegistrationAsync(BusinessReferenceDataUsageRegistration registration, CancellationToken ct = default)
    {
        var now = DateTimeOffset.UtcNow;
        var normalizedSetCode = registration.SetCode.Trim();
        var normalizedModule = registration.ConsumerModule.Trim();
        var normalizedName = registration.ConsumerName.Trim();
        var normalizedScopeType = string.IsNullOrWhiteSpace(registration.ScopeType)
            ? null
            : registration.ScopeType.Trim();
        var normalizedScopeKey = string.IsNullOrWhiteSpace(registration.ScopeKey)
            ? null
            : registration.ScopeKey.Trim();

        var filter = Builders<BusinessReferenceDataUsageRegistration>.Filter.And(
            Builders<BusinessReferenceDataUsageRegistration>.Filter.Eq(x => x.TenantId, TenantContext.TenantId),
            Builders<BusinessReferenceDataUsageRegistration>.Filter.Eq(x => x.SetCode, normalizedSetCode),
            Builders<BusinessReferenceDataUsageRegistration>.Filter.Eq(x => x.ConsumerModule, normalizedModule),
            Builders<BusinessReferenceDataUsageRegistration>.Filter.Eq(x => x.ConsumerName, normalizedName),
            Builders<BusinessReferenceDataUsageRegistration>.Filter.Eq(x => x.ScopeType, normalizedScopeType),
            Builders<BusinessReferenceDataUsageRegistration>.Filter.Eq(x => x.ScopeKey, normalizedScopeKey),
            Builders<BusinessReferenceDataUsageRegistration>.Filter.Eq(x => x.IsDeleted, false));

        var update = Builders<BusinessReferenceDataUsageRegistration>.Update
            .SetOnInsert(x => x.UsageRegistrationId, registration.UsageRegistrationId == Guid.Empty ? Guid.NewGuid() : registration.UsageRegistrationId)
            .SetOnInsert(x => x.CreatedAt, now)
            .SetOnInsert(x => x.CreatedBy, string.IsNullOrWhiteSpace(registration.CreatedBy) ? "system" : registration.CreatedBy)
            .Set(x => x.SetCode, normalizedSetCode)
            .Set(x => x.ConsumerModule, normalizedModule)
            .Set(x => x.ConsumerName, normalizedName)
            .Set(x => x.ConsumerEndpoint, string.IsNullOrWhiteSpace(registration.ConsumerEndpoint) ? null : registration.ConsumerEndpoint.Trim())
            .Set(x => x.ScopeType, normalizedScopeType)
            .Set(x => x.ScopeKey, normalizedScopeKey)
            .Set(x => x.VersionPin, registration.VersionPin)
            .Set(x => x.AsOfDate, registration.AsOfDate)
            .Set(x => x.ResolutionMode, registration.ResolutionMode.Trim())
            .Set(x => x.Criticality, registration.Criticality.Trim().ToLowerInvariant())
            .Set(x => x.Notes, string.IsNullOrWhiteSpace(registration.Notes) ? null : registration.Notes.Trim())
            .Set(x => x.LastResolvedVersionId, registration.LastResolvedVersionId)
            .Set(x => x.LastResolvedAt, registration.LastResolvedAt)
            .Set(x => x.IsActive, registration.IsActive)
            .Set(x => x.UpdatedAt, now)
            .Set(x => x.UpdatedBy, string.IsNullOrWhiteSpace(registration.UpdatedBy) ? registration.CreatedBy : registration.UpdatedBy);

        var options = new FindOneAndUpdateOptions<BusinessReferenceDataUsageRegistration>
        {
            IsUpsert = true,
            ReturnDocument = ReturnDocument.After
        };

        var doc = await _usageRegistrations.FindOneAndUpdateAsync(filter, update, options, ct);
        return doc;
    }

    public async Task<BusinessReferenceDataUsageImpactSummary> GetUsageImpactSummaryAsync(string setCode, CancellationToken ct = default)
    {
        var normalized = setCode.Trim();
        var filter = Builders<BusinessReferenceDataUsageRegistration>.Filter.And(
            Builders<BusinessReferenceDataUsageRegistration>.Filter.Eq(x => x.TenantId, TenantContext.TenantId),
            Builders<BusinessReferenceDataUsageRegistration>.Filter.Eq(x => x.SetCode, normalized),
            Builders<BusinessReferenceDataUsageRegistration>.Filter.Eq(x => x.IsDeleted, false),
            Builders<BusinessReferenceDataUsageRegistration>.Filter.Eq(x => x.IsActive, true));

        var items = await _usageRegistrations.Find(filter).ToListAsync(ct);
        var total = items.Count;
        var high = items.Count(x => string.Equals(x.Criticality, "high", StringComparison.OrdinalIgnoreCase));
        var medium = items.Count(x => string.Equals(x.Criticality, "medium", StringComparison.OrdinalIgnoreCase));
        var low = items.Count(x => string.Equals(x.Criticality, "low", StringComparison.OrdinalIgnoreCase));
        var critical = high;
        var last = items.MaxBy(x => x.UpdatedAt ?? x.CreatedAt)?.UpdatedAt
            ?? items.MaxBy(x => x.CreatedAt)?.CreatedAt;

        return new BusinessReferenceDataUsageImpactSummary(normalized, total, critical, high, medium, low, last);
    }

    public async Task<IReadOnlyList<BusinessReferenceDataUsageRegistration>> GetUsageRegistrationsAsync(string setCode, CancellationToken ct = default)
    {
        var normalized = setCode.Trim();
        var filter = Builders<BusinessReferenceDataUsageRegistration>.Filter.And(
            Builders<BusinessReferenceDataUsageRegistration>.Filter.Eq(x => x.TenantId, TenantContext.TenantId),
            Builders<BusinessReferenceDataUsageRegistration>.Filter.Eq(x => x.SetCode, normalized),
            Builders<BusinessReferenceDataUsageRegistration>.Filter.Eq(x => x.IsDeleted, false),
            Builders<BusinessReferenceDataUsageRegistration>.Filter.Eq(x => x.IsActive, true));

        // Ordered in memory, not by the server, for the same reason as
        // WorkflowInstanceRepository.GetLatestByObjectRefAsync: with no DateTimeOffsetSerializer registered
        // (BL-030) every DateTimeOffset is stored as a BSON array [ticks, offsetMinutes], and sorting on two
        // of them makes MongoDB reject the query with "cannot sort with keys that are parallel arrays". This
        // was verified broken against a real server, not inferred — a registration with a non-null UpdatedAt
        // was enough to kill the whole listing. Do NOT restore the server-side sort until BL-030 lands.
        // Ordering intent is unchanged (UpdatedAt desc, then CreatedAt desc); a never-updated registration
        // has a null UpdatedAt and sorts LAST, which is where MongoDB put it too.
        var rows = await _usageRegistrations.Find(filter).ToListAsync(ct);
        return rows
            .OrderByDescending(x => x.UpdatedAt.HasValue)
            .ThenByDescending(x => x.UpdatedAt ?? DateTimeOffset.MinValue)
            .ThenByDescending(x => x.CreatedAt)
            .ToList();
    }

    public async Task<BusinessReferenceDataUsageRegistration?> GetUsageRegistrationByIdAsync(Guid usageRegistrationId, CancellationToken ct = default)
    {
        var filter = Builders<BusinessReferenceDataUsageRegistration>.Filter.And(
            Builders<BusinessReferenceDataUsageRegistration>.Filter.Eq(x => x.TenantId, TenantContext.TenantId),
            Builders<BusinessReferenceDataUsageRegistration>.Filter.Eq(x => x.UsageRegistrationId, usageRegistrationId),
            Builders<BusinessReferenceDataUsageRegistration>.Filter.Eq(x => x.IsDeleted, false));

        return await _usageRegistrations.Find(filter).FirstOrDefaultAsync(ct);
    }

    public async Task<bool> DeactivateUsageRegistrationAsync(Guid usageRegistrationId, string actorId, CancellationToken ct = default)
    {
        var filter = Builders<BusinessReferenceDataUsageRegistration>.Filter.And(
            Builders<BusinessReferenceDataUsageRegistration>.Filter.Eq(x => x.TenantId, TenantContext.TenantId),
            Builders<BusinessReferenceDataUsageRegistration>.Filter.Eq(x => x.UsageRegistrationId, usageRegistrationId),
            Builders<BusinessReferenceDataUsageRegistration>.Filter.Eq(x => x.IsDeleted, false));

        var update = Builders<BusinessReferenceDataUsageRegistration>.Update
            .Set(x => x.IsActive, false)
            .Set(x => x.UpdatedAt, DateTimeOffset.UtcNow)
            .Set(x => x.UpdatedBy, actorId);

        var result = await _usageRegistrations.UpdateOneAsync(filter, update, cancellationToken: ct);
        return result.ModifiedCount > 0;
    }

    public async Task<bool> UpdateSetUsageSummaryAsync(string setCode, int totalRegistrations, int criticalRegistrations, DateTimeOffset updatedAt, CancellationToken ct = default)
    {
        var normalized = setCode.Trim();
        var filter = Builders<BusinessReferenceDataSet>.Filter.And(
            Builders<BusinessReferenceDataSet>.Filter.Eq(x => x.TenantId, TenantContext.TenantId),
            Builders<BusinessReferenceDataSet>.Filter.Eq(x => x.SetCode, normalized),
            Builders<BusinessReferenceDataSet>.Filter.Eq(x => x.IsDeleted, false));

        var update = Builders<BusinessReferenceDataSet>.Update
            .Set(x => x.UsageRegistrationCount, totalRegistrations)
            .Set(x => x.CriticalUsageCount, criticalRegistrations)
            .Set(x => x.LastUsageRegisteredAt, updatedAt)
            .Set(x => x.UpdatedAt, updatedAt);

        var result = await Collection.UpdateOneAsync(filter, update, cancellationToken: ct);
        return result.ModifiedCount > 0;
    }

    public Task CreateImportPreviewAsync(BusinessReferenceDataImportPreview preview, CancellationToken ct = default)
    {
        return _importPreviews.InsertOneAsync(preview, cancellationToken: ct);
    }

    public async Task<BusinessReferenceDataImportPreview?> GetImportPreviewByIdAsync(Guid previewId, CancellationToken ct = default)
    {
        var filter = Builders<BusinessReferenceDataImportPreview>.Filter.And(
            Builders<BusinessReferenceDataImportPreview>.Filter.Eq(x => x.TenantId, TenantContext.TenantId),
            Builders<BusinessReferenceDataImportPreview>.Filter.Eq(x => x.PreviewId, previewId),
            Builders<BusinessReferenceDataImportPreview>.Filter.Eq(x => x.IsDeleted, false));

        return await _importPreviews.Find(filter).FirstOrDefaultAsync(ct);
    }

    public async Task<bool> UpdateImportPreviewAsync(BusinessReferenceDataImportPreview preview, CancellationToken ct = default)
    {
        preview.UpdatedAt = DateTimeOffset.UtcNow;
        var filter = Builders<BusinessReferenceDataImportPreview>.Filter.And(
            Builders<BusinessReferenceDataImportPreview>.Filter.Eq(x => x.TenantId, TenantContext.TenantId),
            Builders<BusinessReferenceDataImportPreview>.Filter.Eq(x => x.PreviewId, preview.PreviewId),
            Builders<BusinessReferenceDataImportPreview>.Filter.Eq(x => x.IsDeleted, false));

        var result = await _importPreviews.ReplaceOneAsync(filter, preview, cancellationToken: ct);
        return result.ModifiedCount > 0;
    }

    private static BsonDocument BuildSortDocument(string? sort)
    {
        var normalized = string.IsNullOrWhiteSpace(sort) ? "-createdAt" : sort.Trim();
        var descending = normalized.StartsWith("-", StringComparison.Ordinal);
        var field = descending || normalized.StartsWith("+", StringComparison.Ordinal)
            ? normalized[1..]
            : normalized;

        var fieldName = field.ToLowerInvariant() switch
        {
            "setcode" => nameof(BusinessReferenceDataSet.SetCode),
            "name" => nameof(BusinessReferenceDataSet.Name),
            "status" => nameof(BusinessReferenceDataSet.Status),
            "scope" or "scopetype" => nameof(BusinessReferenceDataSet.ScopeType),
            "updatedat" => nameof(BusinessReferenceDataSet.UpdatedAt),
            _ => nameof(BusinessReferenceDataSet.CreatedAt)
        };

        return new BsonDocument(fieldName, descending ? -1 : 1);
    }

    private static BsonDocument RenderSetFilter(FilterDefinition<BusinessReferenceDataSet> filter)
    {
        return filter.Render(
            BsonSerializer.LookupSerializer<BusinessReferenceDataSet>(),
            BsonSerializer.SerializerRegistry);
    }

    private static BsonDocument RenderVersionFilter(FilterDefinition<BusinessReferenceDataVersion> filter)
    {
        return filter.Render(
            BsonSerializer.LookupSerializer<BusinessReferenceDataVersion>(),
            BsonSerializer.SerializerRegistry);
    }

    private static BusinessReferenceDataSet DeserializeSetDocument(BsonDocument document)
    {
        var copy = new BsonDocument(document);
        copy.Remove("_id");
        return BsonSerializer.Deserialize<BusinessReferenceDataSet>(copy);
    }

    private static BusinessReferenceDataVersion DeserializeVersionDocument(BsonDocument document)
    {
        var copy = new BsonDocument(document);
        copy.Remove("_id");
        return BsonSerializer.Deserialize<BusinessReferenceDataVersion>(copy);
    }
}
