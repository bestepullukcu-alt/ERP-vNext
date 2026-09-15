using System.Globalization;
using System.Reflection;
using System.Text;
using Diten.Platform.Application.Features.Audit.Services;
using Diten.Platform.Application.Features.Tasks;
using Diten.Platform.Application.Features.Tasks.Handlers.QueryHandlers;
using Diten.Platform.Application.Features.Tasks.Services;
using Diten.Platform.Common.Authorization;
using Diten.Platform.Domain.Enums.Tasks;
using Microsoft.Extensions.Logging.Abstractions;
using Xunit;

namespace Diten.Platform.Application.Tests.Tasks;

using Task = System.Threading.Tasks.Task;

/// <summary>
/// DILIM 1e — THE REPORT'S ROWS, AS A FILE.
///
/// <para><b>⚠ THE ACCEPTANCE CRITERION: 12 ON SCREEN, 12 IN THE FILE.</b> Downloading 13 where the screen said 12
/// is worse than a wrong number, because nobody notices. Four things have to hold, and each has its own guard:</para>
/// <list type="number">
/// <item>Every number on screen is the count of its column in the file — the same <c>Select</c> fills both.</item>
/// <item>The export's scope comes from the resolver, as the report's does — a scoped caller exports scoped rows.</item>
/// <item>The export's filter is the one the caller sent — never dropped on the way to the read.</item>
/// <item>The repository does not query on its own — it calls the report's read, which carries the scope.</item>
/// </list>
/// <para>Too many rows is refused rather than cut, and the file itself is checked for what a spreadsheet does
/// to it.</para>
/// </summary>
public sealed class WorkReportExportTests
{
    private static readonly DateTimeOffset From = new(2026, 6, 1, 0, 0, 0, TimeSpan.Zero);
    private static readonly DateTimeOffset To = new(2026, 7, 1, 0, 0, 0, TimeSpan.Zero);

    private static readonly Guid Tenant = Guid.Parse("11111111-2222-3333-4444-555555555555");
    private static readonly Guid Caller = Guid.Parse("99999999-8888-7777-6666-555555555555");
    private static readonly Guid MyUnit = Guid.Parse("aaaaaaaa-bbbb-cccc-dddd-eeeeeeeeeeee");
    private static readonly Guid UnitB = Guid.Parse("bbbbbbbb-0000-0000-0000-00000000000b");

    private static WorkReportRow Row(
        int id,
        DateTimeOffset created,
        DateTimeOffset? completed = null,
        DateTimeOffset? cancelled = null,
        DateTimeOffset? due = null,
        string? outcome = null,
        Guid? unit = null,
        decimal? estimate = null,
        decimal spent = 0m) => new(
        Guid.Parse($"00000000-0000-0000-0000-{id:D12}"),
        TaskTypeId: null,
        OrganizationUnitId: unit ?? MyUnit,
        AssigneeUserId: null,
        CreatedByUserId: null,
        PoolPositionId: null,
        Priority: TaskPriority.Medium,
        created,
        completed,
        cancelled,
        due,
        estimate,
        spent,
        outcome,
        completed is not null ? TaskLifecycle.Done
            : cancelled is not null ? TaskLifecycle.Cancelled
            : TaskLifecycle.Open);

    private static WorkReportCriteria Criteria() => new(From, To, WorkReportScope.TenantWideScope());

    /*
     * ONE PERIOD WITH SOMETHING IN EVERY CELL, and with the OVERLAPS the file has to de-duplicate:
     *
     *   #1  opened, completed on time, CORRECTED
     *   #2  opened, completed LATE, CORRECTED
     *   #3  opened, cancelled, no deadline, DROPPED
     *   #4  opened, still open — ALSO open at the period's end (0-7 band) and returned twice
     *   #5  opened in MAY, completed LATE in the period
     *   #10 #11 #12  open at the end only — one per ageing band
     *   #20  unattended only
     *   #4   is ALSO in the unattended set — three copies of one task, one row in the file
     */
    private static readonly WorkReportRow Four = Row(4, To.AddDays(-2));

    private static WorkReportRowSet Set() => new(
        [
            Row(1, From.AddDays(1), completed: From.AddDays(9), due: From.AddDays(14), outcome: "CORRECTED",
                estimate: 4m, spent: 5.5m),
            Row(2, From.AddDays(2), completed: From.AddDays(19), due: From.AddDays(11), outcome: "CORRECTED"),
            Row(3, From.AddDays(3), cancelled: From.AddDays(8), outcome: "DROPPED"),
            Four,
            Row(5, From.AddDays(-20), completed: From.AddDays(5), due: From.AddDays(0), unit: UnitB)
        ],
        [
            Four,
            Row(10, To.AddDays(-3)),
            Row(11, To.AddDays(-20)),
            Row(12, To.AddDays(-200), unit: UnitB)
        ],
        [Row(20, From.AddDays(6)), Four],
        new Dictionary<Guid, int> { [Four.Id] = 2 });

    /// <summary>The column of a row that belongs to a bucket kind — found by NAME, so a kind with no column fails.</summary>
    private static bool Flag(WorkReportExportRow row, WorkReportBucketKind kind)
    {
        var property = typeof(WorkReportExportRow).GetProperty(kind.ToString(), BindingFlags.Public | BindingFlags.Instance);
        Assert.True(property is not null && property.PropertyType == typeof(bool),
            $"The export has no bool column named {kind} — every bucket the report publishes needs one, or the "
            + "file cannot reproduce that number.");
        return (bool)property!.GetValue(row)!;
    }

    private static int Published(WorkReportBucket totals, WorkReportBucketKind kind) => kind switch
    {
        WorkReportBucketKind.Opened => totals.Flow.Opened,
        WorkReportBucketKind.Closed => totals.Flow.Closed,
        WorkReportBucketKind.Completed => totals.Flow.Completed,
        WorkReportBucketKind.Cancelled => totals.Flow.Cancelled,
        WorkReportBucketKind.Unattended => totals.Flow.Unattended,
        WorkReportBucketKind.OnTime => totals.Timeliness.OnTime,
        WorkReportBucketKind.Late => totals.Timeliness.Late,
        WorkReportBucketKind.WithoutDueDate => totals.Timeliness.WithoutDueDate,
        WorkReportBucketKind.AgingUpTo7Days => totals.Aging.UpTo7Days,
        WorkReportBucketKind.AgingFrom8To30Days => totals.Aging.From8To30Days,
        WorkReportBucketKind.AgingOlderThan30Days => totals.Aging.OlderThan30Days,
        WorkReportBucketKind.Returned => totals.Rework.TasksReturned,
        _ => throw new InvalidOperationException($"{kind} is not a single published number.")
    };

    // ── (1) EVERY NUMBER ON SCREEN IS A COLUMN'S COUNT IN THE FILE ───────────────────────────────────────

    [Fact]
    public void Summing_a_column_of_the_file_gives_exactly_the_number_the_screen_published_for_it()
    {
        /*
         * ⚠ BOTH SIDES ARE PRODUCTION. The left is WorkReportTally.Build — what the report endpoint returns; the
         * right is WorkReportTally.Export — what the file is written from. Walked over EVERY bucket kind, so a
         * kind added later without a column fails here rather than shipping a file that cannot reproduce it.
         */
        var criteria = Criteria();
        var set = Set();
        var totals = WorkReportTally.Build(criteria, set).Totals;
        var rows = WorkReportTally.Export(criteria, set);

        foreach (var kind in Enum.GetValues<WorkReportBucketKind>().Where(kind => kind != WorkReportBucketKind.Outcome))
        {
            var published = Published(totals, kind);
            Assert.True(published > 0, $"{kind} is empty in the fixture — an identity between two zeroes proves nothing.");
            Assert.Equal(published, rows.Count(row => Flag(row, kind)));
        }

        // The outcome chart: rows touched in the period that carry the code.
        Assert.NotEmpty(totals.Outcomes);
        foreach (var outcome in totals.Outcomes)
        {
            Assert.Equal(outcome.Count, rows.Count(row => row.InPeriod && row.ClosureReasonCode == outcome.Code));
        }
    }

    [Fact]
    public void One_row_per_task_however_many_of_the_report_s_sets_it_sits_in()
    {
        // #4 is in all three sets. Three rows for it would make every column sum wrong by two.
        var rows = WorkReportTally.Export(Criteria(), Set());

        Assert.Equal(rows.Count, rows.Select(row => row.Id).Distinct().Count());
        Assert.Equal(9, rows.Count); // #1-#5, #10-#12, #20

        var four = Assert.Single(rows, row => row.Id == Four.Id);
        Assert.True(four.InPeriod && four.Opened && four.AgingUpTo7Days && four.Unattended && four.Returned);
        Assert.Equal(2, four.ReturnCount);

        // Rows that are in the file only because ageing or the backlog counts them say so.
        Assert.False(Assert.Single(rows, row => row.Id == Row(12, To).Id).InPeriod);
    }

    // ── (2) THE SCOPE COMES FROM THE RESOLVER ────────────────────────────────────────────────────────────

    private sealed class RecordingExports(WorkReportExportSet answer) : IWorkReportRepository
    {
        public WorkReportCriteria? LastCriteria { get; private set; }
        public int? LastMaxRows { get; private set; }
        public int Calls { get; private set; }

        public System.Threading.Tasks.Task<WorkReportExportSet> ExportAsync(
            WorkReportCriteria criteria, int maxRows, CancellationToken ct = default)
        {
            Calls++;
            LastCriteria = criteria;
            LastMaxRows = maxRows;
            return System.Threading.Tasks.Task.FromResult(answer);
        }

        public System.Threading.Tasks.Task<WorkReportDto> AggregateAsync(
            WorkReportCriteria criteria, CancellationToken ct = default) =>
            throw new NotSupportedException("The export must not run the report's aggregation.");

        public System.Threading.Tasks.Task<WorkReportItemsDto> ItemsAsync(
            WorkReportItemsCriteria criteria, CancellationToken ct = default) =>
            throw new NotSupportedException("The export must not page a cell.");
    }

    private static (WorkReportExportQueryHandler Handler, RecordingExports Reports) Build(
        WorkReportExportSet answer,
        params EntitlementDataScope[] scopes)
        => BuildAudited(answer, new RecordingExportAudit(), scopes);

    /// <summary>BL-347 — the same handler, with the audit writer a test wants to watch or to fail.</summary>
    private static (WorkReportExportQueryHandler Handler, RecordingExports Reports) BuildAudited(
        WorkReportExportSet answer,
        RecordingExportAudit audit,
        params EntitlementDataScope[] scopes)
    {
        var reports = new RecordingExports(answer);
        /*
         * ⚠ THE REAL SCOPE SOURCE over a stand-in resolver — the same seam WorkReportQueryHandlerTests drives.
         * The resolver is MOD-0018-FU15's IDataScopeResolver; nothing here re-derives what it would answer.
         */
        var source = new WorkReportScopeSource(
            new FakeDataScopeResolver(scopes),
            new FakeCurrentUserContext(Caller),
            new FakeTenantContext(Tenant),
            TaskActors.Holding(TaskPermissions.WorkReportRead),
            NullLogger<WorkReportScopeSource>.Instance);

        return (new WorkReportExportQueryHandler(reports, source, audit), reports);
    }

    private static EntitlementDataScope Unit(Guid id) => new(EntitlementDataScopeKind.OrgUnit, id, scopeCode: null);

    private static WorkReportExportQuery Query(WorkReportFilter? filter = null, string? format = "csv") =>
        new(From, To, format, "corr", filter);

    [Fact]
    public async Task A_scoped_caller_exports_their_scope_and_nothing_wider()
    {
        /*
         * ⚠ SABOTAGE 1's GUARD. Wire the export to anything but IWorkReportScopeSource — a tenant-wide scope, a
         * query of its own that never asks the resolver — and the criteria below stop carrying the caller's unit.
         */
        var (handler, reports) = Build(WorkReportExportSet.Empty, Unit(MyUnit));

        var response = await handler.Handle(Query(), CancellationToken.None);

        Assert.True(response.IsSuccessful);
        Assert.Equal(1, reports.Calls);
        var scope = reports.LastCriteria!.Scope;
        Assert.False(scope.TenantWide, "a caller without the tenant-wide permission was exported the whole tenant");
        Assert.Equal(new[] { MyUnit }, scope.OrganizationUnitIds);
    }

    [Fact]
    public async Task A_caller_whose_scope_resolves_to_nothing_gets_an_empty_file_and_the_read_never_runs()
    {
        // "No scopes" read as "no restrictions" is how an export becomes a leak. Empty — never unfiltered.
        var (handler, reports) = Build(new WorkReportExportSet(3, []));

        var response = await handler.Handle(Query(), CancellationToken.None);

        Assert.True(response.IsSuccessful);
        Assert.Equal(0, reports.Calls);
        Assert.Equal(0, response.Data!.RowCount);
        var lines = Encoding.UTF8.GetString(response.Data.Content).TrimStart('\uFEFF')
            .Split("\r\n", StringSplitOptions.RemoveEmptyEntries);
        Assert.Single(lines); // the header, and nothing under it
    }

    // ── (3) THE FILTER IS THE ONE THE CALLER SENT ────────────────────────────────────────────────────────

    [Fact]
    public async Task The_filter_the_screen_sent_reaches_the_read_unchanged()
    {
        // ⚠ SABOTAGE 2's GUARD, at the handler. Dropping the filter here exports the unfiltered report while the
        // screen shows the filtered one — more rows in the file than on screen.
        var filter = new WorkReportFilter(OrganizationUnitId: MyUnit, Priority: TaskPriority.High);
        var (handler, reports) = Build(WorkReportExportSet.Empty, Unit(MyUnit));

        await handler.Handle(Query(filter), CancellationToken.None);

        Assert.Equal(filter, reports.LastCriteria!.Filter);
        Assert.Equal(From, reports.LastCriteria.From);
        Assert.Equal(To, reports.LastCriteria.To);
        Assert.Equal(WorkReportExportLimits.MaxRows, reports.LastMaxRows);
    }

    // ── TOO MANY ROWS IS REFUSED, NOT CUT ────────────────────────────────────────────────────────────────

    [Fact]
    public async Task Past_the_limit_the_export_is_refused_with_its_own_reason_and_no_file()
    {
        var (handler, _) = Build(new WorkReportExportSet(WorkReportExportLimits.MaxRows + 1, []), Unit(MyUnit));

        var response = await handler.Handle(Query(), CancellationToken.None);

        Assert.False(response.IsSuccessful);
        Assert.Equal(400, response.StatusCode);
        Assert.Equal(TaskReasonCodes.WorkReportExportTooLarge, response.ReasonCode);
        Assert.Null(response.Data);
    }

    [Fact]
    public async Task An_unknown_format_and_an_inverted_period_are_refused()
    {
        var (handler, reports) = Build(WorkReportExportSet.Empty, Unit(MyUnit));

        Assert.Equal(400, (await handler.Handle(Query(format: "xlsx"), CancellationToken.None)).StatusCode);
        Assert.Equal(400, (await handler.Handle(
            new WorkReportExportQuery(To, From, "csv", "corr"), CancellationToken.None)).StatusCode);
        Assert.Equal(0, reports.Calls);
    }

    // ── BL-347 — ONE AUDIT RECORD PER FILE HANDED OUT, OR NO FILE ────────────────────────────────────────
    //
    // The writer itself (actor from the token, tenant from the request) is DataExportAuditWriterTests'; the
    // real append on a real database is WorkReportExportAuditTrailMongoTests'. These guard the HANDLER's half:
    // it asks, it asks once, it asks only for a file it hands out, and it does not hand one out unrecorded.

    private sealed class RecordingExportAudit(bool recorded = true) : IDataExportAuditWriter
    {
        public List<DataExportAuditEntry> Entries { get; } = [];

        public System.Threading.Tasks.Task<DataExportAuditResult> RecordAsync(
            DataExportAuditEntry entry, CancellationToken ct = default)
        {
            Entries.Add(entry);
            return System.Threading.Tasks.Task.FromResult(new DataExportAuditResult(
                recorded,
                global::Diten.Platform.Domain.Enums.AuditActorType.TenantUser,
                recorded
                    ? global::Diten.Platform.Application.Contracts.Audit.AuditAppendStatus.Queued
                    : global::Diten.Platform.Application.Contracts.Audit.AuditAppendStatus.EnqueueFailed,
                recorded ? null : "test: the audit outbox refused the record"));
        }
    }

    [Fact]
    public async Task A_file_handed_out_leaves_exactly_one_export_record_naming_its_format_and_row_count()
    {
        var audit = new RecordingExportAudit();
        var rows = WorkReportTally.Export(Criteria(), Set());
        var (handler, _) = BuildAudited(new WorkReportExportSet(rows.Count, rows), audit, Unit(MyUnit));

        var response = await handler.Handle(Query(format: "json"), CancellationToken.None);

        Assert.True(response.IsSuccessful);
        var entry = Assert.Single(audit.Entries);
        Assert.Equal("MOD-0024", entry.SourceModule);
        Assert.Equal("Tasks.WorkReportExportQuery", entry.RequestType);
        Assert.Equal("WorkReport", entry.EntityType);
        Assert.Equal(WorkReportExportFormats.Json, entry.Format);
        Assert.Equal(rows.Count, entry.RowCount);
        Assert.Equal(response.Data!.RowCount, entry.RowCount);
        Assert.Equal("corr", entry.RequestCorrelationId);
        Assert.Null(entry.Dataset);
    }

    [Fact]
    public async Task An_empty_file_is_still_a_file_handed_out_and_is_recorded_with_zero_rows()
    {
        // A caller whose scope resolves to nothing downloads a header — that download is still an export.
        var audit = new RecordingExportAudit();
        var (handler, _) = BuildAudited(new WorkReportExportSet(3, []), audit);

        var response = await handler.Handle(Query(), CancellationToken.None);

        Assert.True(response.IsSuccessful);
        Assert.Equal(0, Assert.Single(audit.Entries).RowCount);
    }

    [Fact]
    public async Task A_person_filter_is_recorded_as_applied_and_never_as_whose()
    {
        var audit = new RecordingExportAudit();
        var person = Guid.Parse("12345678-aaaa-4bbb-8ccc-1234567890ab");
        var (handler, _) = BuildAudited(WorkReportExportSet.Empty, audit, Unit(MyUnit));

        await handler.Handle(
            Query(new WorkReportFilter(OrganizationUnitId: MyUnit, AssigneeUserId: person, Priority: TaskPriority.High)),
            CancellationToken.None);

        var filters = Assert.Single(audit.Entries).Filters;
        Assert.Equal(DataExportFilterSummary.Applied, filters["assignee"]);
        Assert.DoesNotContain(filters.Values, value =>
            value is not null && value.Contains(person.ToString(), StringComparison.OrdinalIgnoreCase));
        Assert.Equal(MyUnit.ToString(), filters["organizationUnitId"]);
        Assert.Equal(nameof(TaskPriority.High), filters["priority"]);
        Assert.Equal(From.ToString("O", CultureInfo.InvariantCulture), filters["from"]);
    }

    [Fact]
    public async Task When_the_audit_record_cannot_be_written_the_export_is_refused_and_carries_no_file()
    {
        // ⚠ SABOTAGE 3's GUARD at the handler: swallowing the answer and returning the file turns this red.
        var audit = new RecordingExportAudit(recorded: false);
        var rows = WorkReportTally.Export(Criteria(), Set());
        var (handler, _) = BuildAudited(new WorkReportExportSet(rows.Count, rows), audit, Unit(MyUnit));

        var response = await handler.Handle(Query(), CancellationToken.None);

        Assert.False(response.IsSuccessful);
        Assert.Equal(503, response.StatusCode);
        Assert.Equal(DataExportAuditReasonCodes.NotRecorded, response.ReasonCode);
        Assert.Null(response.Data);
        Assert.Single(audit.Entries);
    }

    [Fact]
    public async Task A_refused_export_hands_out_no_file_and_therefore_writes_no_record()
    {
        var audit = new RecordingExportAudit();
        var (tooLarge, _) = BuildAudited(
            new WorkReportExportSet(WorkReportExportLimits.MaxRows + 1, []), audit, Unit(MyUnit));
        var (plain, _) = BuildAudited(WorkReportExportSet.Empty, audit, Unit(MyUnit));

        Assert.False((await tooLarge.Handle(Query(), CancellationToken.None)).IsSuccessful);
        Assert.False((await plain.Handle(Query(format: "xlsx"), CancellationToken.None)).IsSuccessful);
        Assert.False((await plain.Handle(new WorkReportExportQuery(To, From, "csv", "corr"), CancellationToken.None)).IsSuccessful);

        Assert.Empty(audit.Entries);
    }

    // ── (4) THE REPOSITORY CALLS THE REPORT'S OWN READ ───────────────────────────────────────────────────

    [Fact]
    public void The_repository_export_goes_through_the_report_s_read_and_issues_no_query_of_its_own()
    {
        /*
         * ⚠ SABOTAGE 1 ONE LAYER DOWN. The handler can hand the right scope to a repository that then ignores
         * it — `_tasks.Find(tenant)` in place of `ReadAsync(criteria, ...)` would export every task in the tenant
         * and every test above would stay green. ReadAsync is where the scope, the period and the filter become
         * a Mongo match (BuildMatchFilter, guarded by WorkReportQueryCompositionTests), so the export has to
         * call it — with the criteria it was given — and must not reach a collection directly.
         */
        var body = MethodBody(RepositorySource(), "public async Task<WorkReportExportSet> ExportAsync(");

        Assert.Contains("await ReadAsync(criteria, ct", body, StringComparison.Ordinal);
        Assert.Contains("WorkReportTally.Export(criteria, readout.Set)", body, StringComparison.Ordinal);
        Assert.DoesNotContain("_tasks.", body, StringComparison.Ordinal);
        Assert.DoesNotContain("_transitions.", body, StringComparison.Ordinal);
        Assert.DoesNotContain("criteria with", body, StringComparison.Ordinal);
    }

    private static string RepositorySource()
    {
        var dir = new DirectoryInfo(AppContext.BaseDirectory);
        while (dir is not null && !Directory.Exists(Path.Combine(dir.FullName, "services", "Diten.Platform")))
        {
            dir = dir.Parent;
        }

        Assert.NotNull(dir);
        return File.ReadAllText(Path.Combine(
            dir!.FullName, "services", "Diten.Platform", "src", "Diten.Platform.Infrastructure",
            "Persistence", "Repositories", "WorkReportRepository.cs"));
    }

    /// <summary>From the signature to the brace that closes it — the method's own text and nothing after it.</summary>
    private static string MethodBody(string source, string signature)
    {
        var start = source.IndexOf(signature, StringComparison.Ordinal);
        Assert.True(start >= 0, $"'{signature}' is gone from WorkReportRepository.cs");

        var open = source.IndexOf('{', start);
        var depth = 0;
        for (var i = open; i < source.Length; i++)
        {
            depth += source[i] == '{' ? 1 : source[i] == '}' ? -1 : 0;
            if (depth == 0)
            {
                return source[start..(i + 1)];
            }
        }

        throw new InvalidOperationException("unbalanced braces");
    }

    // ── THE FILE ITSELF ──────────────────────────────────────────────────────────────────────────────────

    private static WorkReportExportRow Written(string title, decimal spent = 1.5m) =>
        WorkReportTally.Export(Criteria(), Set())[0] with { Title = title, SpentHours = spent };

    [Fact]
    public void The_csv_has_the_header_then_one_line_per_row_and_opens_as_utf8()
    {
        var rows = WorkReportTally.Export(Criteria(), Set());
        var bytes = WorkReportExportSerializer.ToCsv(rows);

        // The BOM is what makes Excel read "Görev" as Görev rather than as the machine's legacy code page.
        Assert.Equal(Encoding.UTF8.GetPreamble(), bytes[..3]);

        var lines = Encoding.UTF8.GetString(bytes[3..]).Split("\r\n", StringSplitOptions.RemoveEmptyEntries);
        Assert.Equal(string.Join(',', WorkReportExportSerializer.CsvHeaders), lines[0]);
        Assert.Equal(rows.Count + 1, lines.Length);
    }

    [Fact]
    public void A_typed_title_cannot_become_a_formula_and_a_comma_cannot_split_a_cell()
    {
        var csv = Encoding.UTF8.GetString(WorkReportExportSerializer.ToCsv(
            [Written("=HYPERLINK(\"http://x\")"), Written("Ödeme, Şubat")]));

        Assert.Contains("\"'=HYPERLINK(\"\"http://x\"\")\"", csv, StringComparison.Ordinal);
        Assert.Contains("\"Ödeme, Şubat\"", csv, StringComparison.Ordinal);
    }

    [Fact]
    public void Numbers_are_written_the_same_under_a_Turkish_culture_and_flags_are_1_or_0()
    {
        var before = CultureInfo.CurrentCulture;
        try
        {
            CultureInfo.CurrentCulture = new CultureInfo("tr-TR");
            var line = Encoding.UTF8.GetString(WorkReportExportSerializer.ToCsv([Written("t", 1.5m)]))
                .Split("\r\n")[1];

            Assert.Contains(",1.5,", line, StringComparison.Ordinal);
            Assert.DoesNotContain("1,5", line, StringComparison.Ordinal);
            Assert.DoesNotContain("True", line, StringComparison.Ordinal);
        }
        finally
        {
            CultureInfo.CurrentCulture = before;
        }
    }

    [Fact]
    public void The_json_carries_the_same_rows()
    {
        var rows = WorkReportTally.Export(Criteria(), Set());
        using var doc = System.Text.Json.JsonDocument.Parse(WorkReportExportSerializer.ToJson(rows));

        Assert.Equal(rows.Count, doc.RootElement.GetArrayLength());
        Assert.True(doc.RootElement[0].TryGetProperty("opened", out _));
    }

    [Fact]
    public void The_file_name_says_the_last_day_counted_not_the_exclusive_end()
    {
        Assert.Equal(
            "work-report_2026-06-01_2026-06-30.csv",
            WorkReportExportFormats.FileName(From, To, WorkReportExportFormats.Csv));
    }
}
