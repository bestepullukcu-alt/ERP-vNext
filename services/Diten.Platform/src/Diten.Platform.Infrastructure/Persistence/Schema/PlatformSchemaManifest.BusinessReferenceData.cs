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
/// (see <see cref="SchemaProfileBudget.BusinessReferenceData"/>) — 8 collections today against a ceiling of 8.
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
         * ⚠ STILL NO DECLARED INDEX — BUT NO LONGER FOR WANT OF SIZING. BL-279 measured this collection with
         * the other eight and it is the ONE that came back needing an index this round could not spend.
         *
         * BusinessReferenceDataStewardshipRepository reads and writes it two ways, both full scans today
         * (250 rows in diten_personalization_dev; COLLSCAN, and SORT->COLLSCAN on the read):
         *
         *   GetValidationResultsByVersionAsync {TenantId, BusinessReferenceDataVersionId, IsDeleted:false} sort RuleId
         *   ReplaceValidationResultsAsync      {TenantId, BusinessReferenceDataVersionId}  (DeleteMany, then InsertMany)
         *
         * The index is not in doubt: {TenantId, BusinessReferenceDataVersionId, RuleId} is ESR-exact — equality
         * on the two scoping columns, RuleId as the sort — and it serves the delete leg on its prefix. Built
         * against a copy of the live data it takes the read from 250 documents examined with a blocking SORT
         * to 25 with neither. One index, both call sites, no second candidate.
         *
         * ⚠ WHAT BLOCKS IT IS A BUDGET, AND THE BUDGET IS NOT OURS TO RAISE. SchemaProfileBudget declares this
         * profile at MaxLogicalIndexes: 18 — the number the GSKU owners set on 2026-08-26 — and the profile
         * already carries exactly 18 (10 declared + the implicit _id on each of 8 collections). Adding this
         * index makes 19 and turns DeclaredBudgetsAreRespected red. Editing the ceiling to fit the change is
         * the move SchemaProfileBudget's own header warns about: it "would produce a test that goes red on the
         * next legitimate index and teaches the reader to raise the number instead of looking at it". So the
         * measurement is recorded here and the ceiling goes back to the owners who set it — see BL-279.
         */
        Collection<BusinessReferenceDataValidationResult>(
            SchemaProfile.BusinessReferenceData,
            PlatformCollections.BusinessReferenceDataValidationResults,
            () => Array.Empty<CreateIndexModel<BusinessReferenceDataValidationResult>>()),
    };
}
