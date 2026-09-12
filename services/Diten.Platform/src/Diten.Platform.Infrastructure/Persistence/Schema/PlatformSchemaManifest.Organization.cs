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
                        new CreateIndexOptions { Name = "ix_organization_units_tree_scope" }),
                    // MOD-0288-FU02 — the SECOND reporting line gets its own scan path. The tree index above
                    // is keyed on the functional parent, so a query for "who reports administratively to X"
                    // would have gone unindexed — and an unindexed query in Mongo raises no error, it is
                    // merely slow, so it ships.
                    new CreateIndexModel<OrganizationUnit>(
                        Builders<OrganizationUnit>.IndexKeys
                            .Ascending(x => x.TenantId)
                            .Ascending(x => x.AdministrativeParentOrganizationUnitId),
                        new CreateIndexOptions { Name = "ix_organization_units_administrative_parent" })

            }),
        // ── MOD-0288-FU02 — tenant-defined Organization Unit fields ────────────────────────────────────────
        Collection<OrganizationFieldDefinition>(
            SchemaProfile.Organization,
            PlatformCollections.OrganizationFieldDefinitions,
            () => new CreateIndexModel<OrganizationFieldDefinition>[]
            {
                    // Mirrors ux_organization_units_tenant_code_active exactly, including the partial filter:
                    // uniqueness applies to LIVE rows only, so a soft-deleted definition does not permanently
                    // reserve its code.
                    new CreateIndexModel<OrganizationFieldDefinition>(
                        Builders<OrganizationFieldDefinition>.IndexKeys
                            .Ascending(x => x.TenantId)
                            .Ascending(x => x.Code),
                        new CreateIndexOptions<OrganizationFieldDefinition>
                        {
                            Unique = true,
                            Name = "ux_organization_field_definitions_tenant_code_active",
                            PartialFilterExpression =
                                Builders<OrganizationFieldDefinition>.Filter.Eq(x => x.IsDeleted, false)
                        })

            }),
        Collection<OrganizationFieldValue>(
            SchemaProfile.Organization,
            PlatformCollections.OrganizationFieldValues,
            () => new CreateIndexModel<OrganizationFieldValue>[]
            {
                    /*
                     * ⚠ THIS IS WHAT ENFORCES "one active value per unit per definition" — in the DATABASE,
                     * not in a handler that a second writer can race past between its read and its write.
                     */
                    new CreateIndexModel<OrganizationFieldValue>(
                        Builders<OrganizationFieldValue>.IndexKeys
                            .Ascending(x => x.TenantId)
                            .Ascending(x => x.OrganizationUnitId)
                            .Ascending(x => x.DefinitionId),
                        new CreateIndexOptions<OrganizationFieldValue>
                        {
                            Unique = true,
                            Name = "ux_organization_field_values_tenant_unit_definition_active",
                            PartialFilterExpression =
                                Builders<OrganizationFieldValue>.Filter.Eq(x => x.IsDeleted, false)
                        }),
                    /*
                     * The filter index. ⚠ ONE index serves ALL fifty definitions, because values live in their
                     * own documents rather than in an array on the unit — so there is no $elemMatch here and
                     * no index per field. Measured ceiling context: organization_units carried 3 indexes and
                     * task_items 6, against Mongo's 64 per collection.
                     */
                    new CreateIndexModel<OrganizationFieldValue>(
                        Builders<OrganizationFieldValue>.IndexKeys
                            .Ascending(x => x.TenantId)
                            .Ascending(x => x.DefinitionId)
                            .Ascending(x => x.Value),
                        new CreateIndexOptions { Name = "ix_organization_field_values_tenant_definition_value" }),
                    new CreateIndexModel<OrganizationFieldValue>(
                        Builders<OrganizationFieldValue>.IndexKeys
                            .Ascending(x => x.TenantId)
                            .Ascending(x => x.OrganizationUnitId),
                        new CreateIndexOptions { Name = "ix_organization_field_values_tenant_unit" })

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
