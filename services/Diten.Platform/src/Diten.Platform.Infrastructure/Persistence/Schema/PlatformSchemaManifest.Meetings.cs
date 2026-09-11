using Diten.Platform.Domain.Entities.Meetings;
using MongoDB.Driver;

namespace Diten.Platform.Infrastructure.Persistence.Schema;

public static partial class PlatformSchemaManifest
{
    /// <summary>
    /// MOD-0357 S1 — the one bridge collection. Two lookup indexes (source-first, target-first) so a batched
    /// read from either direction (<c>ListBySourceAsync</c>/<c>ListByTargetAsync</c>) stays index-covered, and
    /// one UNIQUE compound index so the same six-value link can never exist twice at the storage level — the
    /// idempotency service's own check is a courtesy; this is the guarantee.
    /// </summary>
    private static readonly SchemaCollection[] MeetingsCollections =
    {
        Collection<RecordLink>(
            SchemaProfile.Meetings,
            PlatformCollections.MeetingRecordLinks,
            () => new CreateIndexModel<RecordLink>[]
            {
                new CreateIndexModel<RecordLink>(
                    Builders<RecordLink>.IndexKeys
                        .Ascending(x => x.TenantId)
                        .Ascending(x => x.SourceRecordId)
                        .Ascending(x => x.IsDeleted),
                    new CreateIndexOptions { Name = "ix_meeting_record_links_tenant_source" }),
                new CreateIndexModel<RecordLink>(
                    Builders<RecordLink>.IndexKeys
                        .Ascending(x => x.TenantId)
                        .Ascending(x => x.TargetRecordId)
                        .Ascending(x => x.IsDeleted),
                    new CreateIndexOptions { Name = "ix_meeting_record_links_tenant_target" }),
                // A soft-deleted row must not collide with a freshly re-created identical link, so IsDeleted
                // joins the unique key — exactly the same reasoning every other tenant-unique index in this
                // manifest already applies (e.g. WorkingCalendar's scope+country+year+code).
                new CreateIndexModel<RecordLink>(
                    Builders<RecordLink>.IndexKeys
                        .Ascending(x => x.TenantId)
                        .Ascending(x => x.SourceModuleCode)
                        .Ascending(x => x.SourceRecordId)
                        .Ascending(x => x.TargetModuleCode)
                        .Ascending(x => x.TargetRecordId)
                        .Ascending(x => x.LinkType)
                        .Ascending(x => x.IsDeleted),
                    new CreateIndexOptions
                    {
                        Name = "ux_meeting_record_links_tenant_source_target_type",
                        Unique = true
                    })
            })
    };
}
