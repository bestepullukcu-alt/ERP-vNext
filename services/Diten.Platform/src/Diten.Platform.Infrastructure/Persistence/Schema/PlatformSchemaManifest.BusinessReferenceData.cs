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
/// (see <see cref="SchemaProfileBudget.BusinessReferenceData"/>) — 7 collections today against a ceiling of 8.
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
         * ⚠ NO DECLARED INDEX — and that is a FINDING, not a decision. BusinessReferenceDataStewardshipRepository reads this collection, but the
         * index configuration never named it, so every query against it is a collection scan. It is listed
         * here because the manifest is the registry of what EXISTS; leaving it out is what let it go
         * unindexed unnoticed in the first place. Sizing the right index is backlog, not this round.
         */
        Collection<BusinessReferenceDataValidationResult>(
            SchemaProfile.BusinessReferenceData,
            PlatformCollections.BusinessReferenceDataValidationResults,
            () => Array.Empty<CreateIndexModel<BusinessReferenceDataValidationResult>>()),
    };
}
