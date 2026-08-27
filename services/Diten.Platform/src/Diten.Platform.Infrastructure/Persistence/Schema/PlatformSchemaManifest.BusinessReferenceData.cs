using Diten.Platform.Application.Contracts.Eventing;
using Diten.Platform.Domain.Entities;
using Diten.Platform.Domain.Entities.Notifications;
using Diten.Platform.Domain.Entities.Audit;
using Diten.Platform.Domain.Entities.DocumentManagement;
using Diten.Platform.Domain.Entities.InterfaceRegistry;
using Diten.Platform.Domain.Entities.Organization;
using Diten.Platform.Domain.Entities.Tasks;
using Diten.Platform.Domain.Entities.Workflow;
using Diten.Platform.Domain.Features.SubscriptionFeatures;
using Diten.Platform.Domain.Repositories;
using Diten.Platform.Infrastructure.Persistence.Models;
using Diten.Platform.Infrastructure.Persistence.Repositories;
using Diten.Platform.Infrastructure.Persistence.Configurations;
using MongoDB.Bson;
using MongoDB.Driver;

namespace Diten.Platform.Infrastructure.Persistence.Schema;

public static partial class PlatformSchemaManifest
{
    /// <summary>
    /// MOD-0290 business reference data. This is the profile with a declared budget
/// (see <see cref="SchemaProfileBudget.BusinessReferenceData"/>) — 8 collections against a ceiling of 8,
/// and 19 logical indexes (11 declared + the implicit _id on each of the 8) against a ceiling of 19.
    /// </summary>
    private static readonly SchemaCollection[] BusinessReferenceDataCollections =
    {
        Collection<BusinessReferenceDataSet>(
            SchemaProfile.BusinessReferenceData,
            PlatformCollections.BusinessReferenceDataSets,
            () => new CreateIndexModel<BusinessReferenceDataSet>[]
            {
                    new CreateIndexModel<BusinessReferenceDataSet>(
                        Builders<BusinessReferenceDataSet>.IndexKeys
                            .Ascending(x => x.TenantId)
                            .Ascending(x => x.SetCode),
                        new CreateIndexOptions<BusinessReferenceDataSet>
                        {
                            Unique = true,
                            Name = "ux_business_reference_data_sets_tenant_code",
                            PartialFilterExpression = Builders<BusinessReferenceDataSet>.Filter.Eq(x => x.IsDeleted, false)
                        }),
                    new CreateIndexModel<BusinessReferenceDataSet>(
                        Builders<BusinessReferenceDataSet>.IndexKeys
                            .Ascending(x => x.TenantId)
                            .Ascending(x => x.Status)
                            .Ascending(x => x.IsDeleted),
                        new CreateIndexOptions { Name = "ix_business_reference_data_sets_tenant_status_deleted" })

            }),
        Collection<BusinessReferenceDataVersion>(
            SchemaProfile.BusinessReferenceData,
            PlatformCollections.BusinessReferenceDataVersions,
            () => new CreateIndexModel<BusinessReferenceDataVersion>[]
            {
                    new CreateIndexModel<BusinessReferenceDataVersion>(
                        Builders<BusinessReferenceDataVersion>.IndexKeys
                            .Ascending(x => x.TenantId)
                            .Ascending(x => x.BusinessReferenceDataSetId)
                            .Ascending(x => x.VersionNumber),
                        new CreateIndexOptions<BusinessReferenceDataVersion>
                        {
                            Unique = true,
                            Name = "ux_business_reference_data_versions_set_number",
                            PartialFilterExpression = Builders<BusinessReferenceDataVersion>.Filter.Eq(x => x.IsDeleted, false)
                        }),
                    new CreateIndexModel<BusinessReferenceDataVersion>(
                        Builders<BusinessReferenceDataVersion>.IndexKeys
                            .Ascending(x => x.TenantId)
                            .Ascending(x => x.Status)
                            .Ascending(x => x.IsDeleted),
                        new CreateIndexOptions { Name = "ix_business_reference_data_versions_tenant_status_deleted" })

            }),
        Collection<BusinessReferenceDataUsageRegistration>(
            SchemaProfile.BusinessReferenceData,
            PlatformCollections.BusinessReferenceDataUsageRegistrations,
            () => new CreateIndexModel<BusinessReferenceDataUsageRegistration>[]
            {
                    new CreateIndexModel<BusinessReferenceDataUsageRegistration>(
                        Builders<BusinessReferenceDataUsageRegistration>.IndexKeys
                            .Ascending(x => x.TenantId)
                            .Ascending(x => x.SetCode)
                            .Ascending(x => x.ConsumerModule)
                            .Ascending(x => x.ConsumerName),
                        new CreateIndexOptions<BusinessReferenceDataUsageRegistration>
                        {
                            Unique = true,
                            Name = "ux_business_reference_data_usage_consumer",
                            PartialFilterExpression = Builders<BusinessReferenceDataUsageRegistration>.Filter.Eq(x => x.IsDeleted, false)
                        })

            }),
        Collection<BusinessReferenceDataImportPreview>(
            SchemaProfile.BusinessReferenceData,
            PlatformCollections.BusinessReferenceDataImportPreviews,
            () => new CreateIndexModel<BusinessReferenceDataImportPreview>[]
            {
                    new CreateIndexModel<BusinessReferenceDataImportPreview>(
                        Builders<BusinessReferenceDataImportPreview>.IndexKeys
                            .Ascending(x => x.TenantId)
                            .Ascending(x => x.PreviewId),
                        new CreateIndexOptions<BusinessReferenceDataImportPreview>
                        {
                            Unique = true,
                            Name = "ux_business_reference_data_import_previews_tenant_id",
                            PartialFilterExpression = Builders<BusinessReferenceDataImportPreview>.Filter.Eq(x => x.IsDeleted, false)
                        }),
                    new CreateIndexModel<BusinessReferenceDataImportPreview>(
                        Builders<BusinessReferenceDataImportPreview>.IndexKeys
                            .Ascending(x => x.TenantId)
                            .Ascending(x => x.ExpiresAt),
                        new CreateIndexOptions { Name = "ix_business_reference_data_import_previews_expiry" })

            }),
        Collection<BusinessReferenceDataIntegrationEvent>(
            SchemaProfile.BusinessReferenceData,
            PlatformCollections.BusinessReferenceDataIntegrationEvents,
            () => new CreateIndexModel<BusinessReferenceDataIntegrationEvent>[]
            {
                    new CreateIndexModel<BusinessReferenceDataIntegrationEvent>(
                        Builders<BusinessReferenceDataIntegrationEvent>.IndexKeys
                            .Ascending(x => x.TenantId)
                            .Ascending(x => x.BusinessReferenceDataVersionId)
                            .Ascending(x => x.EventName)
                            .Ascending(x => x.IdempotencyKey),
                        new CreateIndexOptions<BusinessReferenceDataIntegrationEvent>
                        {
                            Unique = true,
                            Name = "ux_business_reference_data_events_idempotency",
                            PartialFilterExpression = Builders<BusinessReferenceDataIntegrationEvent>.Filter.Eq(x => x.IsDeleted, false)
                        })

            }),
        Collection<BusinessReferenceDataTenantAssignment>(
            SchemaProfile.BusinessReferenceData,
            PlatformCollections.BusinessReferenceDataTenantAssignments,
            () => new CreateIndexModel<BusinessReferenceDataTenantAssignment>[]
            {
                    new CreateIndexModel<BusinessReferenceDataTenantAssignment>(
                        Builders<BusinessReferenceDataTenantAssignment>.IndexKeys
                            .Ascending(x => x.TenantId)
                            .Ascending(x => x.ConsumerTenantId)
                            .Ascending(x => x.SetCode),
                        new CreateIndexOptions<BusinessReferenceDataTenantAssignment>
                        {
                            Unique = true,
                            Name = "ux_business_reference_data_tenant_assignments_active",
                            PartialFilterExpression = Builders<BusinessReferenceDataTenantAssignment>.Filter.Eq(x => x.IsDeleted, false)
                        })

            }),
        Collection<BusinessReferenceDataPublishOperation>(
            SchemaProfile.BusinessReferenceData,
            PlatformCollections.BusinessReferenceDataPublishOperations,
            () => new CreateIndexModel<BusinessReferenceDataPublishOperation>[]
            {
                    new CreateIndexModel<BusinessReferenceDataPublishOperation>(
                        Builders<BusinessReferenceDataPublishOperation>.IndexKeys
                            .Ascending(x => x.TenantId)
                            .Ascending(x => x.IdempotencyKey),
                        new CreateIndexOptions<BusinessReferenceDataPublishOperation>
                        {
                            Unique = true,
                            Name = "ux_business_reference_data_publish_operations_idempotency",
                            PartialFilterExpression = Builders<BusinessReferenceDataPublishOperation>.Filter.Eq(x => x.IsDeleted, false)
                        })

            }),
        /*
         * ⚠ THE ONE INDEX BL-279 SIZED AND COULD NOT SPEND. It is here now because the GSKU owners raised the
         * profile's index ceiling 18 → 19 on 2026-08-28 (BL-298) — the ceiling moved first, on a measurement,
         * and the index followed. The collection ceiling did NOT move: still 8.
         *
         * BusinessReferenceDataStewardshipRepository reads and writes this collection two ways, and one index
         * on {TenantId, BusinessReferenceDataVersionId, RuleId} serves both:
         *
         *   GetValidationResultsByVersionAsync {TenantId, VersionId, IsDeleted:false} sort RuleId
         *   ReplaceValidationResultsAsync      {TenantId, VersionId}  (DeleteMany, then InsertMany)
         *
         * ESR-exact: equality on the two scoping columns, RuleId as the sort. Measured against a copy of the
         * live 250 rows, the read goes from SORT->COLLSCAN examining 250 documents to FETCH->IXSCAN examining
         * 25, with no blocking SORT; the delete leg goes from COLLSCAN over 250 to IXSCAN over 25.
         *
         * ⚠ NO PARTIAL FILTER, AND THAT IS A MEASUREMENT, NOT AN OVERSIGHT. Every other index in this profile
         * carries PartialFilterExpression IsDeleted=false, and the read here does filter on IsDeleted=false,
         * so the house style argued for one. It was measured both ways before being rejected: the partial
         * variant serves the READ identically (25 examined, no SORT) but the DELETE leg regresses all the way
         * back to COLLSCAN over 250. ReplaceValidationResultsAsync deletes on {TenantId, VersionId} with NO
         * IsDeleted predicate, so Mongo cannot prove that query is a subset of the partial filter and refuses
         * the index. Adding the partial to match the neighbours would buy a smaller index and hand back half
         * the win. PlatformSchemaContractMongoTests pins the delete leg for exactly this reason.
         */
        Collection<BusinessReferenceDataValidationResult>(
            SchemaProfile.BusinessReferenceData,
            PlatformCollections.BusinessReferenceDataValidationResults,
            () => new CreateIndexModel<BusinessReferenceDataValidationResult>[]
            {
                    new CreateIndexModel<BusinessReferenceDataValidationResult>(
                        Builders<BusinessReferenceDataValidationResult>.IndexKeys
                            .Ascending(x => x.TenantId)
                            .Ascending(x => x.BusinessReferenceDataVersionId)
                            .Ascending(x => x.RuleId),
                        new CreateIndexOptions { Name = "ix_business_reference_data_validation_results_tenant_version_rule" })

            }),
    };
}
