using Diten.Platform.Domain.Entities.EvidenceLinking;
using MongoDB.Driver;

namespace Diten.Platform.Infrastructure.Persistence.Schema;

public static partial class PlatformSchemaManifest
{
    /// <summary>
    /// MOD-0031 slice 1 — evidence links. Three tenant-first lookup indexes (by object, by document, by document
    /// version — the forward and the two reverse reads) and one partial UNIQUE index that makes a second identical
    /// ACTIVE link impossible at the storage level. The partial filter is an equality on Status (never <c>$ne</c>,
    /// which partial indexes reject), so a removed link leaves the unique space and the same evidence can be re-linked.
    /// </summary>
    private static readonly SchemaCollection[] EvidenceLinkingCollections =
    {
        Collection<EvidenceLink>(
            SchemaProfile.EvidenceLinking,
            PlatformCollections.EvidenceLinks,
            () => new CreateIndexModel<EvidenceLink>[]
            {
                new CreateIndexModel<EvidenceLink>(
                    Builders<EvidenceLink>.IndexKeys
                        .Ascending(x => x.TenantId)
                        .Ascending(x => x.ObjectRef.Module)
                        .Ascending(x => x.ObjectRef.ObjectType)
                        .Ascending(x => x.ObjectRef.ObjectId),
                    new CreateIndexOptions { Name = "ix_evidence_links_tenant_object" }),
                new CreateIndexModel<EvidenceLink>(
                    Builders<EvidenceLink>.IndexKeys.Ascending(x => x.TenantId).Ascending(x => x.DocumentId),
                    new CreateIndexOptions { Name = "ix_evidence_links_tenant_document" }),
                new CreateIndexModel<EvidenceLink>(
                    Builders<EvidenceLink>.IndexKeys.Ascending(x => x.TenantId).Ascending(x => x.DocumentVersionId),
                    new CreateIndexOptions { Name = "ix_evidence_links_tenant_document_version" }),
                new CreateIndexModel<EvidenceLink>(
                    Builders<EvidenceLink>.IndexKeys.Ascending(x => x.TenantId).Ascending(x => x.ActiveDedupKey),
                    new CreateIndexOptions<EvidenceLink>
                    {
                        Unique = true,
                        Name = "ux_evidence_links_tenant_active_dedup",
                        PartialFilterExpression = Builders<EvidenceLink>.Filter.Eq(x => x.Status, EvidenceLinkStatuses.Active)
                    })
            })
    };
}
