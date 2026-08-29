using Diten.Platform.Domain.Entities.WorkingCalendar;
using MongoDB.Driver;

namespace Diten.Platform.Infrastructure.Persistence.Schema;

public static partial class PlatformSchemaManifest
{
    /// <summary>
    /// CAND-CAP-0010 Working Calendar & Public Holidays (working_calendars + import batches). Ported from
    /// the pre-refactor MongoDbIndexConfigurations monolith on 2026-08-28 (F-WC-DOC-SCHEMA-PORT). The
    /// ux_working_calendars_scope_country_year_code partial-unique index is dropped-before-rebuild in
    /// PlatformSchemaMigrations (its options changed across versions).
    /// </summary>
    private static readonly SchemaCollection[] WorkingCalendarCollections =
    {
        Collection<WorkingCalendar>(
            SchemaProfile.WorkingCalendar,
            PlatformCollections.WorkingCalendars,
            () => new CreateIndexModel<WorkingCalendar>[]
{
            // Scope + country + year + code is the business key. TenantId participates so a country row (null) and a
            // tenant row can legitimately share a code.
            new CreateIndexModel<WorkingCalendar>(
                Builders<WorkingCalendar>.IndexKeys
                    .Ascending(x => x.TenantId)
                    .Ascending(x => x.CountryCode)
                    .Ascending(x => x.CalendarYear)
                    .Ascending(x => x.OrganizationUnitId)
                    .Ascending(x => x.LegalEntityId)
                    .Ascending(x => x.CalendarCode),
                new CreateIndexOptions<WorkingCalendar>
                {
                    Unique = true,
                    Name = "ux_working_calendars_scope_country_year_code",
                    // Uniqueness holds among LIVE rows only: an archived calendar releases its code so the same
                    // year can be re-entered (there is no delete endpoint). `$in` is used rather than "not
                    // archived" because a partialFilterExpression cannot contain $ne/$not — verified supported on
                    // this server (MongoDB 7.0). The list is shared with the repository guard so the two can never
                    // disagree; a guard looser than this index would surface as an E11000 500 instead of a 409.
                    PartialFilterExpression = Builders<WorkingCalendar>.Filter.And(
                        Builders<WorkingCalendar>.Filter.Eq(x => x.IsDeleted, false),
                        Builders<WorkingCalendar>.Filter.In(x => x.CalendarStatus, WorkingCalendarStatus.CodeHolding))
                }),
            // The provider's hot path: resolve the active calendar for a scope + country + year.
            new CreateIndexModel<WorkingCalendar>(
                Builders<WorkingCalendar>.IndexKeys
                    .Ascending(x => x.TenantId)
                    .Ascending(x => x.CountryCode)
                    .Ascending(x => x.CalendarYear)
                    .Ascending(x => x.CalendarStatus)
                    .Ascending(x => x.IsDeleted),
                new CreateIndexOptions { Name = "ix_working_calendars_resolution" }),
            new CreateIndexModel<WorkingCalendar>(
                Builders<WorkingCalendar>.IndexKeys
                    .Ascending(x => x.TenantId)
                    .Ascending(x => x.OrganizationUnitId)
                    .Ascending(x => x.CalendarYear),
                new CreateIndexOptions { Name = "ix_working_calendars_org_scope", Sparse = true }),
            new CreateIndexModel<WorkingCalendar>(
                Builders<WorkingCalendar>.IndexKeys
                    .Ascending(x => x.TenantId)
                    .Ascending(x => x.LegalEntityId)
                    .Ascending(x => x.CalendarYear),
                new CreateIndexOptions { Name = "ix_working_calendars_legal_entity_scope", Sparse = true })
        }),
        Collection<WorkingCalendarImportBatch>(
            SchemaProfile.WorkingCalendar,
            PlatformCollections.WorkingCalendarImportBatches,
            () => new CreateIndexModel<WorkingCalendarImportBatch>[]
{
            new CreateIndexModel<WorkingCalendarImportBatch>(
                Builders<WorkingCalendarImportBatch>.IndexKeys.Ascending(x => x.BatchCode),
                new CreateIndexOptions<WorkingCalendarImportBatch>
                {
                    Name = "ux_working_calendar_import_batch_code",
                    Unique = true,
                    PartialFilterExpression = Builders<WorkingCalendarImportBatch>.Filter.Eq(x => x.IsDeleted, false)
                }),
            new CreateIndexModel<WorkingCalendarImportBatch>(
                Builders<WorkingCalendarImportBatch>.IndexKeys.Ascending(x => x.CountryCode)
                    .Ascending(x => x.CalendarYear).Ascending(x => x.ImportStatus),
                new CreateIndexOptions { Name = "ix_working_calendar_import_list" }),
            new CreateIndexModel<WorkingCalendarImportBatch>(
                Builders<WorkingCalendarImportBatch>.IndexKeys.Ascending(x => x.TargetCalendarId)
                    .Ascending(x => x.ImportStatus),
                new CreateIndexOptions { Name = "ix_working_calendar_import_target_status" })
        }),
    };
}
