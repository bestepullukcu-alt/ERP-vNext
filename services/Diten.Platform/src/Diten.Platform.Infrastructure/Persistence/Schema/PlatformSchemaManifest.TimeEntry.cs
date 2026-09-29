using Diten.Platform.Domain.Entities.TimeEntry;
using MongoDB.Driver;

namespace Diten.Platform.Infrastructure.Persistence.Schema;

public static partial class PlatformSchemaManifest
{
    /// <summary>
    /// MOD-0280-FU01 T1a (pack §4, ADR-004) — the time-entry module's collections. Every index is tenant-first; every
    /// uniqueness rule the pack states is a UNIQUE index here, so it holds under a race and not only in a handler.
    /// Uniqueness is over LIVE rows (partial on <c>IsDeleted == false</c>): a discarded correction draft or a removed
    /// draft row must not reserve its key forever.
    /// </summary>
    private static readonly SchemaCollection[] TimeEntryCollections =
    {
        Collection<TimesheetWeek>(
            SchemaProfile.TimeEntry,
            PlatformCollections.TimeEntryTimesheetWeeks,
            () => new CreateIndexModel<TimesheetWeek>[]
            {
                // One row per (person, ISO week, revision).
                new CreateIndexModel<TimesheetWeek>(
                    Builders<TimesheetWeek>.IndexKeys
                        .Ascending(x => x.TenantId)
                        .Ascending(x => x.UserId)
                        .Ascending(x => x.WeekKey)
                        .Ascending(x => x.RevisionNumber),
                    new CreateIndexOptions<TimesheetWeek>
                    {
                        Unique = true,
                        Name = "ux_time_entry_weeks_tenant_user_week_revision",
                        PartialFilterExpression = Builders<TimesheetWeek>.Filter.Eq(x => x.IsDeleted, false)
                    }),
                // D5 — exactly ONE approved revision in force per week. The flag joins the key (rather than two
                // indexes sharing one key pattern) so the rule does not depend on the server accepting duplicate
                // key patterns that differ only in their partial filter.
                new CreateIndexModel<TimesheetWeek>(
                    Builders<TimesheetWeek>.IndexKeys
                        .Ascending(x => x.TenantId)
                        .Ascending(x => x.UserId)
                        .Ascending(x => x.WeekKey)
                        .Ascending(x => x.InForce),
                    new CreateIndexOptions<TimesheetWeek>
                    {
                        Unique = true,
                        Name = "ux_time_entry_weeks_tenant_user_week_in_force",
                        PartialFilterExpression = Builders<TimesheetWeek>.Filter.And(
                            Builders<TimesheetWeek>.Filter.Eq(x => x.InForce, true),
                            Builders<TimesheetWeek>.Filter.Eq(x => x.IsDeleted, false))
                    }),
                // §12 — at most ONE open revision (Draft or Submitted) per week: "one open correction per week".
                new CreateIndexModel<TimesheetWeek>(
                    Builders<TimesheetWeek>.IndexKeys
                        .Ascending(x => x.TenantId)
                        .Ascending(x => x.UserId)
                        .Ascending(x => x.WeekKey)
                        .Ascending(x => x.IsOpen),
                    new CreateIndexOptions<TimesheetWeek>
                    {
                        Unique = true,
                        Name = "ux_time_entry_weeks_tenant_user_week_open",
                        PartialFilterExpression = Builders<TimesheetWeek>.Filter.And(
                            Builders<TimesheetWeek>.Filter.Eq(x => x.IsOpen, true),
                            Builders<TimesheetWeek>.Filter.Eq(x => x.IsDeleted, false))
                    }),
                // The approvals page: submitted weeks MOD-0023 assigned to one approver, oldest first.
                new CreateIndexModel<TimesheetWeek>(
                    Builders<TimesheetWeek>.IndexKeys
                        .Ascending(x => x.TenantId)
                        .Ascending(x => x.Status)
                        .Ascending(x => x.AssignedApproverUserId)
                        .Ascending(x => x.SubmittedAtUtcTicks),
                    new CreateIndexOptions { Name = "ix_time_entry_weeks_tenant_status_approver" }),
                // The decision sweep: submitted weeks, oldest submission first (ticks — a sortable scalar, BL-030).
                new CreateIndexModel<TimesheetWeek>(
                    Builders<TimesheetWeek>.IndexKeys
                        .Ascending(x => x.TenantId)
                        .Ascending(x => x.Status)
                        .Ascending(x => x.SubmittedAtUtcTicks),
                    new CreateIndexOptions { Name = "ix_time_entry_weeks_tenant_status_submitted" })
            }),

        Collection<TimeEntry>(
            SchemaProfile.TimeEntry,
            PlatformCollections.TimeEntryEntries,
            () => new CreateIndexModel<TimeEntry>[]
            {
                // One live row per (revision, day, task-or-category, source) — pack §4.2. Its prefix also serves
                // "every row of this revision".
                new CreateIndexModel<TimeEntry>(
                    Builders<TimeEntry>.IndexKeys
                        .Ascending(x => x.TenantId)
                        .Ascending(x => x.TimesheetWeekId)
                        .Ascending(x => x.LocalDate)
                        .Ascending(x => x.TaskItemId)
                        .Ascending(x => x.CategoryCode)
                        .Ascending(x => x.Source),
                    new CreateIndexOptions<TimeEntry>
                    {
                        Unique = true,
                        Name = "ux_time_entry_entries_tenant_week_date_target_source",
                        PartialFilterExpression = Builders<TimeEntry>.Filter.Eq(x => x.IsDeleted, false)
                    }),
                // The finalizer's recomputation input: every row of one task.
                new CreateIndexModel<TimeEntry>(
                    Builders<TimeEntry>.IndexKeys
                        .Ascending(x => x.TenantId)
                        .Ascending(x => x.TaskItemId),
                    new CreateIndexOptions { Name = "ix_time_entry_entries_tenant_task" })
            }),

        // D7 — the single source of a task's spent time; one row per task.
        Collection<TaskTimeTotal>(
            SchemaProfile.TimeEntry,
            PlatformCollections.TimeEntryTaskTotals,
            () => new CreateIndexModel<TaskTimeTotal>[]
            {
                new CreateIndexModel<TaskTimeTotal>(
                    Builders<TaskTimeTotal>.IndexKeys
                        .Ascending(x => x.TenantId)
                        .Ascending(x => x.TaskItemId),
                    new CreateIndexOptions { Name = "ux_time_entry_task_totals_tenant_task", Unique = true })
            }),

        Collection<WorkCategory>(
            SchemaProfile.TimeEntry,
            PlatformCollections.TimeEntryWorkCategories,
            () => new CreateIndexModel<WorkCategory>[]
            {
                new CreateIndexModel<WorkCategory>(
                    Builders<WorkCategory>.IndexKeys
                        .Ascending(x => x.TenantId)
                        .Ascending(x => x.Code),
                    new CreateIndexOptions<WorkCategory>
                    {
                        Unique = true,
                        Name = "ux_time_entry_work_categories_tenant_code",
                        PartialFilterExpression = Builders<WorkCategory>.Filter.Eq(x => x.IsDeleted, false)
                    })
            }),

        Collection<TimeEntrySettings>(
            SchemaProfile.TimeEntry,
            PlatformCollections.TimeEntrySettings,
            () => new CreateIndexModel<TimeEntrySettings>[]
            {
                new CreateIndexModel<TimeEntrySettings>(
                    Builders<TimeEntrySettings>.IndexKeys.Ascending(x => x.TenantId),
                    new CreateIndexOptions<TimeEntrySettings>
                    {
                        Unique = true,
                        Name = "ux_time_entry_settings_tenant",
                        PartialFilterExpression = Builders<TimeEntrySettings>.Filter.Eq(x => x.IsDeleted, false)
                    })
            }),

        // D12 — one switch row per legal entity; no row = off.
        Collection<LegalEntityTimeSetting>(
            SchemaProfile.TimeEntry,
            PlatformCollections.TimeEntryLegalEntitySettings,
            () => new CreateIndexModel<LegalEntityTimeSetting>[]
            {
                new CreateIndexModel<LegalEntityTimeSetting>(
                    Builders<LegalEntityTimeSetting>.IndexKeys
                        .Ascending(x => x.TenantId)
                        .Ascending(x => x.LegalEntityId),
                    new CreateIndexOptions<LegalEntityTimeSetting>
                    {
                        Unique = true,
                        Name = "ux_time_entry_legal_entity_settings_tenant_legal_entity",
                        PartialFilterExpression = Builders<LegalEntityTimeSetting>.Filter.Eq(x => x.IsDeleted, false)
                    })
            })
    };
}
