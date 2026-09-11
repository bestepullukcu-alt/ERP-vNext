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
                // MOD-0357 S4 (K11) — lookup support for the bridge's own idempotency check, NOT unique: unlike
                // Meeting.IdempotencyKey (always set), this field is null on every non-bridge link, and Mongo's
                // unique index treats explicit null as a colliding value across documents. A partial-filter
                // unique index would fix that but is not added this slice — see RecordLink.IdempotencyKey's own
                // doc comment for the accepted trade-off (check-before-create is the only guarantee here).
                new CreateIndexModel<RecordLink>(
                    Builders<RecordLink>.IndexKeys
                        .Ascending(x => x.TenantId)
                        .Ascending(x => x.IdempotencyKey),
                    new CreateIndexOptions { Name = "ix_meeting_record_links_tenant_idempotency_key" }),
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
            }),

        // MOD-0357 S2 — the meeting aggregate root. StartAt/OrganizerUserId are the two list filters §9's
        // DataTable needs (date range; "meetings I organize"); the unique key is the idempotency guarantee
        // (K11) — the service's own check is a courtesy, this is the guarantee, same reasoning as RecordLink's.
        Collection<Meeting>(
            SchemaProfile.Meetings,
            PlatformCollections.MeetingMeetings,
            () => new CreateIndexModel<Meeting>[]
            {
                new CreateIndexModel<Meeting>(
                    Builders<Meeting>.IndexKeys
                        .Ascending(x => x.TenantId)
                        .Ascending(x => x.StartAt),
                    new CreateIndexOptions { Name = "ix_meeting_meetings_tenant_start" }),
                new CreateIndexModel<Meeting>(
                    Builders<Meeting>.IndexKeys
                        .Ascending(x => x.TenantId)
                        .Ascending(x => x.OrganizerUserId),
                    new CreateIndexOptions { Name = "ix_meeting_meetings_tenant_organizer" }),
                new CreateIndexModel<Meeting>(
                    Builders<Meeting>.IndexKeys
                        .Ascending(x => x.TenantId)
                        .Ascending(x => x.IdempotencyKey),
                    new CreateIndexOptions { Name = "ux_meeting_meetings_tenant_idempotency_key", Unique = true })
            }),

        // MOD-0357 S2 — one row per invited person. The unique key is the "no duplicate attendee row per
        // meeting" rule (pack §12); IsDeleted joins it for the same reason RecordLink's own unique index does —
        // a removed-then-re-invited attendee must not collide with their own archived row.
        Collection<MeetingAttendee>(
            SchemaProfile.Meetings,
            PlatformCollections.MeetingAttendees,
            () => new CreateIndexModel<MeetingAttendee>[]
            {
                new CreateIndexModel<MeetingAttendee>(
                    Builders<MeetingAttendee>.IndexKeys
                        .Ascending(x => x.TenantId)
                        .Ascending(x => x.MeetingId)
                        .Ascending(x => x.UserId)
                        .Ascending(x => x.IsDeleted),
                    new CreateIndexOptions { Name = "ux_meeting_attendees_tenant_meeting_user", Unique = true }),
                new CreateIndexModel<MeetingAttendee>(
                    Builders<MeetingAttendee>.IndexKeys
                        .Ascending(x => x.TenantId)
                        .Ascending(x => x.UserId),
                    new CreateIndexOptions { Name = "ix_meeting_attendees_tenant_user" })
            }),

        // MOD-0357 S2 — one ordered agenda line. No unique index: SortOrder is server-assigned and re-typed
        // lines are expected to share no natural key.
        Collection<AgendaItem>(
            SchemaProfile.Meetings,
            PlatformCollections.MeetingAgendaItems,
            () => new CreateIndexModel<AgendaItem>[]
            {
                new CreateIndexModel<AgendaItem>(
                    Builders<AgendaItem>.IndexKeys
                        .Ascending(x => x.TenantId)
                        .Ascending(x => x.MeetingId)
                        .Ascending(x => x.SortOrder),
                    new CreateIndexOptions { Name = "ix_meeting_agenda_items_tenant_meeting_sort" })
            }),

        // MOD-0357 S2 — the tenant setting a meeting is created against. Name is tenant-unique (pack §12);
        // IsDeleted joins the unique key so a deleted type's name can be reused.
        Collection<MeetingType>(
            SchemaProfile.Meetings,
            PlatformCollections.MeetingTypes,
            () => new CreateIndexModel<MeetingType>[]
            {
                new CreateIndexModel<MeetingType>(
                    Builders<MeetingType>.IndexKeys
                        .Ascending(x => x.TenantId)
                        .Ascending(x => x.Name)
                        .Ascending(x => x.IsDeleted),
                    new CreateIndexOptions { Name = "ux_meeting_types_tenant_name", Unique = true })
            })
    };
}
