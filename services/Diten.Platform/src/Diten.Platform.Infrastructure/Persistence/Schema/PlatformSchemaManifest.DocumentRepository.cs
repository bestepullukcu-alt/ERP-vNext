using Diten.Platform.Domain.Entities.DocumentRepository;
using MongoDB.Driver;

namespace Diten.Platform.Infrastructure.Persistence.Schema;

public static partial class PlatformSchemaManifest
{
    /// <summary>
    /// MOD-0262-FU01 — Internal Document Repository Service collections.
    /// <para>
    /// Index constraints carried from the module pack:
    /// <list type="bullet">
    /// <item>Partial-index filters use <c>$eq</c> on <c>IsDeleted</c>. <c>$ne</c> / <c>$not</c> are unsupported in
    /// a partial filter expression and make the service crash-loop at startup, so they are never used here.</item>
    /// <item>No index sorts or combines two <c>DateTimeOffset</c> fields — those persist as BSON arrays in this
    /// codebase and a two-field index over them raises a "cannot index parallel arrays" failure.</item>
    /// </list>
    /// </para>
    /// </summary>
    private static readonly SchemaCollection[] DocumentRepositoryCollections =
    {
        Collection<RepositoryObject>(
            SchemaProfile.DocumentManagement,
            PlatformCollections.DocumentRepositoryObjects,
            () => new CreateIndexModel<RepositoryObject>[]
            {
                // Client-facing identity. Unique per tenant among live rows, so a content id can never be
                // ambiguous and a cross-tenant id simply does not match.
                new CreateIndexModel<RepositoryObject>(
                    Builders<RepositoryObject>.IndexKeys
                        .Ascending(x => x.TenantId)
                        .Ascending(x => x.ContentId),
                    new CreateIndexOptions<RepositoryObject>
                    {
                        Unique = true,
                        Name = "ux_document_repository_objects_tenant_content_active",
                        PartialFilterExpression = Builders<RepositoryObject>.Filter.Eq(x => x.IsDeleted, false)
                    }),

                // Physical resolution pair. Unique so two live rows can never claim the same stored object,
                // and it is the index MOD-0262-FU03 (Repository Health) reconciles orphans against.
                new CreateIndexModel<RepositoryObject>(
                    Builders<RepositoryObject>.IndexKeys
                        .Ascending(x => x.TenantId)
                        .Ascending(x => x.StorageProvider)
                        .Ascending(x => x.ObjectKey),
                    new CreateIndexOptions<RepositoryObject>
                    {
                        Unique = true,
                        Name = "ux_document_repository_objects_tenant_provider_key_active",
                        PartialFilterExpression = Builders<RepositoryObject>.Filter.Eq(x => x.IsDeleted, false)
                    }),

                // Consumer back-reference lookup (which objects belong to this item/version?).
                new CreateIndexModel<RepositoryObject>(
                    Builders<RepositoryObject>.IndexKeys
                        .Ascending(x => x.TenantId)
                        .Ascending(x => x.OwningItemId)
                        .Ascending(x => x.OwningVersionId),
                    new CreateIndexOptions { Name = "ix_document_repository_objects_owning_item" }),

                // Byte-identity lookup for future de-duplication and integrity checks.
                new CreateIndexModel<RepositoryObject>(
                    Builders<RepositoryObject>.IndexKeys
                        .Ascending(x => x.TenantId)
                        .Ascending(x => x.Checksum),
                    new CreateIndexOptions { Name = "ix_document_repository_objects_checksum" })
            })
    };
}
