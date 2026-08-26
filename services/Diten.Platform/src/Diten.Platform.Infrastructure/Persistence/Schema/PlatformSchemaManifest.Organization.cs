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
    /// MOD-0288 organization units, positions, assignments and person references.
    /// </summary>
    private static readonly SchemaCollection[] OrganizationCollections =
    {
        Collection<OrganizationUnit>(
            SchemaProfile.Organization,
            PlatformCollections.OrganizationUnits,
            () => new CreateIndexModel<OrganizationUnit>[]
            {
                    new CreateIndexModel<OrganizationUnit>(
                        Builders<OrganizationUnit>.IndexKeys
                            .Ascending(x => x.TenantId)
                            .Ascending(x => x.Code),
                        new CreateIndexOptions<OrganizationUnit>
                        {
                            Unique = true,
                            Name = "ux_organization_units_tenant_code_active",
                            PartialFilterExpression = Builders<OrganizationUnit>.Filter.Eq(x => x.IsDeleted, false)
                        }),
                    new CreateIndexModel<OrganizationUnit>(
                        Builders<OrganizationUnit>.IndexKeys
                            .Ascending(x => x.TenantId)
                            .Ascending(x => x.LegalEntityId)
                            .Ascending(x => x.ParentOrganizationUnitId)
                            .Ascending(x => x.IsDeleted)
                            .Ascending(x => x.IsArchived),
                        new CreateIndexOptions { Name = "ix_organization_units_tree_scope" })

            }),
        Collection<Position>(
            SchemaProfile.Organization,
            PlatformCollections.Positions,
            () => new CreateIndexModel<Position>[]
            {
                    new CreateIndexModel<Position>(
                        Builders<Position>.IndexKeys
                            .Ascending(x => x.TenantId)
                            .Ascending(x => x.Code),
                        new CreateIndexOptions<Position>
                        {
                            Unique = true,
                            Name = "ux_positions_tenant_code_active",
                            PartialFilterExpression = Builders<Position>.Filter.Eq(x => x.IsDeleted, false)
                        }),
                    new CreateIndexModel<Position>(
                        Builders<Position>.IndexKeys
                            .Ascending(x => x.TenantId)
                            .Ascending(x => x.OrganizationUnitId)
                            .Ascending(x => x.ReportsToPositionId)
                            .Ascending(x => x.IsDeleted)
                            .Ascending(x => x.IsArchived),
                        new CreateIndexOptions { Name = "ix_positions_org_reporting_scope" })

            }),
        Collection<PositionAssignment>(
            SchemaProfile.Organization,
            PlatformCollections.PositionAssignments,
            () => new CreateIndexModel<PositionAssignment>[]
            {
                    new CreateIndexModel<PositionAssignment>(
                        Builders<PositionAssignment>.IndexKeys
                            .Ascending(x => x.TenantId)
                            .Ascending(x => x.PositionId)
                            .Ascending(x => x.EffectiveFrom)
                            .Ascending(x => x.EffectiveTo)
                            .Ascending(x => x.IsDeleted),
                        new CreateIndexOptions { Name = "ix_position_assignments_position_interval" }),
                    new CreateIndexModel<PositionAssignment>(
                        Builders<PositionAssignment>.IndexKeys
                            .Ascending(x => x.TenantId)
                            .Ascending(x => x.UserId)
                            .Ascending(x => x.EffectiveFrom)
                            .Ascending(x => x.EffectiveTo)
                            .Ascending(x => x.IsDeleted),
                        new CreateIndexOptions { Name = "ix_position_assignments_user_interval" })

            }),
        Collection<PersonReference>(
            SchemaProfile.Organization,
            PersonReferenceRepository.CollectionName,
            () => new CreateIndexModel<PersonReference>[]
            {
                    new CreateIndexModel<PersonReference>(
                        Builders<PersonReference>.IndexKeys
                            .Ascending(x => x.TenantId)
                            .Ascending(x => x.Id),
                        new CreateIndexOptions<PersonReference>
                        {
                            Unique = true,
                            Name = "ux_person_references_tenant_person_id_active",
                            PartialFilterExpression = Builders<PersonReference>.Filter.Eq(x => x.IsDeleted, false)
                        }),
                    new CreateIndexModel<PersonReference>(
                        Builders<PersonReference>.IndexKeys
                            .Ascending(x => x.TenantId)
                            .Ascending(x => x.DisplayName)
                            .Ascending(x => x.IsDeleted),
                        new CreateIndexOptions { Name = "ix_person_references_tenant_display_name" }),
                    new CreateIndexModel<PersonReference>(
                        Builders<PersonReference>.IndexKeys
                            .Ascending(x => x.TenantId)
                            .Ascending(x => x.Status)
                            .Ascending(x => x.IsDeleted),
                        new CreateIndexOptions { Name = "ix_person_references_tenant_status" }),
                    new CreateIndexModel<PersonReference>(
                        Builders<PersonReference>.IndexKeys
                            .Ascending(x => x.TenantId)
                            .Ascending(x => x.ReferenceCode)
                            .Ascending(x => x.IsDeleted),
                        new CreateIndexOptions { Name = "ix_person_references_tenant_reference_code" })

            }),
    };
}
