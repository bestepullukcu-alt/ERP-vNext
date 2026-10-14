using Diten.CrmService.Application.Common;
using Diten.CrmService.Application.Features.DoctorInsights;
using Diten.CrmService.Application.Features.VisitEvidenceFlags;
using Diten.CrmService.Application.Tests.PlannedVisit;
using Diten.CrmService.Application.Tests.Territory;
using Diten.CrmService.Application.Tests.VisitExecution;
using Diten.CrmService.Application.Tests.VisitScope;
using Diten.CrmService.Domain.Entities;
using Diten.CrmService.Domain.Repositories;
using Diten.CrmService.Persistence.Repositories;
using MongoDB.Driver;
using Xunit;
using ExecutionEntity = Diten.CrmService.Domain.Entities.VisitExecution;
using PlanAtom = Diten.CrmService.Domain.Entities.PlannedVisit;

namespace Diten.CrmService.Application.Tests.VisitWorkspace;

/// <summary>
/// WP-VW-W3E · W3E-BE-b (§2.6) — the manager's visit evidence flags on the PRODUCTION handlers: the scope (own / team /
/// a stranger 404 / read-all), the 92-day window (92 ok, 93 and from &gt; to 400), the filters (onlyFlagged, any of the
/// flags, an unknown flag 400, the institution), paging, one read per page for plans / institutions / doctors (no N+1),
/// the summary consistent with the list (rate guarded at 0 starts), the detail (+ exception, outside 404), tenant isolation;
/// and the Mongo filter itself on a real mongod (CRM_TEST_MONGO_URI, never the dev port).
/// </summary>
public sealed class VwW3EEvidenceFlagsTests
{
    private static readonly Guid Tenant = Guid.Parse("97c50000-0000-0000-0000-0000000000e3");
    private static readonly Guid OtherTenant = Guid.Parse("11110000-0000-0000-0000-0000000000e3");
    private const string Rep = "rep-1", Rep2 = "rep-2", Stranger = "stranger-1", Manager = "manager-1";
    private static readonly DateTimeOffset Now = new(2026, 10, 14, 12, 0, 0, TimeSpan.Zero);
    private static readonly DateOnly Today = DateOnly.FromDateTime(Now.UtcDateTime);

    // ═══ scope ═══════════════════════════════════════════════════════════════════════════════════════════

    [Fact]
    public async Task A_manager_sees_the_team_and_self_a_rep_only_self_a_stranger_is_404_and_read_all_sees_the_tenant()
    {
        var w = new World();
        w.Start(Rep, 0, VisitExecutionFlags.LocationFar);
        w.Start(Rep2, 0);
        w.Start(Stranger, 0, VisitExecutionFlags.MockLocation);
        w.Start(Manager, 0);

        var team = await w.List(Manager, VisitEvidenceFlagsPermissions.Read);
        Assert.Equal(new[] { Manager, Rep, Rep2 }, team.Data!.Items.Select(r => r.ResourceId).OrderBy(x => x).ToArray());
        Assert.Equal("Rep One", team.Data.Items.Single(r => r.ResourceId == Rep).RepName);

        var own = await w.List(Rep, VisitEvidenceFlagsPermissions.Read);                 // exact territory: no team
        Assert.Equal(new[] { Rep }, own.Data!.Items.Select(r => r.ResourceId).ToArray());

        Assert.Equal(404, (await w.List(Manager, VisitEvidenceFlagsPermissions.Read, resourceId: Stranger)).StatusCode);
        Assert.Equal(404, (await w.List(Rep, VisitEvidenceFlagsPermissions.Read, resourceId: Rep2)).StatusCode);
        Assert.Equal(404, (await w.Summary(Manager, VisitEvidenceFlagsPermissions.Read, resourceId: Stranger)).StatusCode);

        var all = await w.List(Manager, VisitEvidenceFlagsPermissions.ReadAll);
        Assert.Equal(4, all.Data!.Total);
        Assert.Contains(all.Data.Items, r => r.ResourceId == Stranger);

        // the detail: outside the scope is exactly a missing one
        var strangerExec = w.Executions.Items.Single(e => e.ResourceId == Stranger).Id;
        var outside = await w.Detail(Manager, strangerExec, VisitEvidenceFlagsPermissions.Read);
        var missing = await w.Detail(Manager, Guid.NewGuid(), VisitEvidenceFlagsPermissions.Read);
        Assert.Equal((404, 404), (outside.StatusCode, missing.StatusCode));
        Assert.Equal(outside.Errors, missing.Errors);
        Assert.Equal(200, (await w.Detail(Manager, strangerExec, VisitEvidenceFlagsPermissions.ReadAll)).StatusCode);
    }

    [Fact]
    public async Task Another_tenants_starts_are_never_read()
    {
        var w = new World();
        w.Start(Rep, 0);
        w.Executions.Items.Add(new ExecutionEntity { Id = Guid.NewGuid(), TenantId = OtherTenant, ResourceId = Rep, PlannedVisitId = Guid.NewGuid(), StartedAt = Now, Flags = { "mock_location" } });
        var all = await w.List(Manager, VisitEvidenceFlagsPermissions.ReadAll);
        Assert.Equal(1, all.Data!.Total);
        Assert.Equal(1, (await w.Summary(Manager, VisitEvidenceFlagsPermissions.ReadAll)).Data!.Started);
        Assert.Equal(404, (await w.Detail(Manager, w.Executions.Items.Last().Id, VisitEvidenceFlagsPermissions.ReadAll)).StatusCode);
    }

    // ═══ window, filters, paging ═════════════════════════════════════════════════════════════════════════

    [Fact]
    public async Task The_window_is_at_most_92_days_both_included()
    {
        var w = new World();
        w.Start(Rep, -91);                                           // the first day of a 92-day window ending today
        w.Start(Rep, -92);                                           // just outside it
        var ok = await w.List(Manager, VisitEvidenceFlagsPermissions.ReadAll, from: Today.AddDays(-91), to: Today);
        Assert.Equal((200, 1L), (ok.StatusCode, ok.Data!.Total));

        var tooLong = await w.List(Manager, VisitEvidenceFlagsPermissions.ReadAll, from: Today.AddDays(-92), to: Today);   // 93 days
        Assert.Equal(400, tooLong.StatusCode);
        Assert.Contains(VisitEvidenceFlagsErrors.RangeInvalid, tooLong.Errors!);
        Assert.Equal(400, (await w.List(Manager, VisitEvidenceFlagsPermissions.ReadAll, from: Today, to: Today.AddDays(-1))).StatusCode);
        Assert.Equal(400, (await w.Summary(Manager, VisitEvidenceFlagsPermissions.ReadAll, from: Today.AddDays(-92), to: Today)).StatusCode);

        // no window given: the last 30 days to today
        var dflt = await w.List(Manager, VisitEvidenceFlagsPermissions.ReadAll);
        Assert.Equal((Today.AddDays(-29), Today), (dflt.Data!.From, dflt.Data.To));
    }

    [Fact]
    public async Task Only_flagged_any_of_the_flags_the_institution_and_an_unknown_flag()
    {
        var w = new World();
        var far = w.Start(Rep, 0, VisitExecutionFlags.LocationFar);
        var mock = w.Start(Rep, -1, VisitExecutionFlags.MockLocation, VisitExecutionFlags.DeviceUnverified);
        var clean = w.Start(Rep, -2);

        var flagged = await w.List(Manager, VisitEvidenceFlagsPermissions.ReadAll, onlyFlagged: true);
        Assert.Equal(new[] { far.Id, mock.Id }, flagged.Data!.Items.Select(r => r.ExecutionId).ToArray());   // newest first

        var anyOf = await w.List(Manager, VisitEvidenceFlagsPermissions.ReadAll, flags: new[] { VisitExecutionFlags.MockLocation, VisitExecutionFlags.ShortVisit });
        Assert.Equal(new[] { mock.Id }, anyOf.Data!.Items.Select(r => r.ExecutionId).ToArray());

        var unknown = await w.List(Manager, VisitEvidenceFlagsPermissions.ReadAll, flags: new[] { "teleport" });
        Assert.Equal(400, unknown.StatusCode);
        Assert.Contains(VisitEvidenceFlagsErrors.FilterInvalid, unknown.Errors!);

        var account = w.Plans.Items.Single(p => p.Id == clean.PlannedVisitId).AccountId!.Value;
        var atAccount = await w.List(Manager, VisitEvidenceFlagsPermissions.ReadAll, accountId: account);
        Assert.Equal(new[] { clean.Id }, atAccount.Data!.Items.Select(r => r.ExecutionId).ToArray());
        Assert.Equal(0, (await w.List(Manager, VisitEvidenceFlagsPermissions.ReadAll, accountId: Guid.NewGuid())).Data!.Total);
    }

    [Fact]
    public async Task Pages_and_one_read_each_for_plans_institutions_and_doctors()
    {
        var w = new World();
        for (var i = 0; i < 7; i++) w.Start(Rep, -i, VisitExecutionFlags.ShortVisit);

        var plansBefore = w.Plans.ListCalls;
        var first = await w.List(Manager, VisitEvidenceFlagsPermissions.ReadAll, page: 1, pageSize: 3);
        Assert.Equal((7L, 3, 1, 3), (first.Data!.Total, first.Data.Items.Count, first.Data.Page, first.Data.PageSize));
        // no N+1: one plan read, one institution read, one doctor read for the whole page
        Assert.Equal((1, 1, 1), (w.Plans.ListCalls - plansBefore, w.Accounts.ListByIdsCalls, w.Contacts.ListByIdsCalls));
        Assert.All(first.Data.Items, r => Assert.NotNull(r.Target.Name));
        Assert.All(first.Data.Items, r => Assert.NotNull(r.Target.AccountName));

        var third = await w.List(Manager, VisitEvidenceFlagsPermissions.ReadAll, page: 3, pageSize: 3);
        Assert.Single(third.Data!.Items);
        Assert.Equal(100, (await w.List(Manager, VisitEvidenceFlagsPermissions.ReadAll, pageSize: 1000)).Data!.PageSize); // capped
        Assert.Empty(first.Data.Items.Intersect(third.Data.Items));
    }

    // ═══ summary ═════════════════════════════════════════════════════════════════════════════════════════

    [Fact]
    public async Task The_summary_agrees_with_the_list_and_the_rate_is_guarded_at_zero()
    {
        var w = new World();
        w.Start(Rep, 0, VisitExecutionFlags.LocationFar, VisitExecutionFlags.DeviceUnverified);
        w.Start(Rep, -1, VisitExecutionFlags.LocationFar);
        w.Start(Rep, -2);
        w.Start(Rep2, 0, VisitExecutionFlags.MockLocation);

        var summary = (await w.Summary(Manager, VisitEvidenceFlagsPermissions.Read)).Data!;
        var list = (await w.List(Manager, VisitEvidenceFlagsPermissions.Read)).Data!;
        var flaggedList = (await w.List(Manager, VisitEvidenceFlagsPermissions.Read, onlyFlagged: true)).Data!;
        Assert.Equal(list.Total, summary.Started);
        Assert.Equal(flaggedList.Total, summary.Flagged);
        Assert.Equal(0.75, summary.FlaggedRate);
        Assert.Equal(2, summary.ByFlag[VisitExecutionFlags.LocationFar]);
        Assert.Equal(1, summary.ByFlag[VisitExecutionFlags.MockLocation]);
        var rep = summary.Reps.Single(r => r.ResourceId == Rep);
        Assert.Equal((3, 2, VisitExecutionFlags.LocationFar, "Rep One"), (rep.Started, rep.Flagged, rep.TopFlag, rep.RepName));

        var empty = (await w.Summary(Manager, VisitEvidenceFlagsPermissions.Read, from: Today.AddDays(-60), to: Today.AddDays(-50))).Data!;
        Assert.Equal((0, 0, 0d), (empty.Started, empty.Flagged, empty.FlaggedRate));     // no division by zero
    }

    [Fact]
    public async Task The_detail_carries_the_device_and_the_exception_but_never_photo_content()
    {
        var w = new World();
        var exception = new VisitEvidenceException
        {
            TenantId = Tenant, ResourceId = Rep, Rules = { VisitEvidenceExceptionRules.LocationInfo }, StartDate = Today, EndDate = Today.AddDays(2),
            ApprovedBy = "approver-1", ApprovedAt = Now.AddDays(-1), Status = VisitEvidenceExceptionStatuses.Approved
        };
        w.Exceptions.Items.Add(exception);
        var e = w.Start(Rep, 0, VisitExecutionFlags.StartedWithException, VisitExecutionFlags.DeviceUnverified);
        e.ExceptionId = exception.Id;
        e.Device = new VisitExecutionDevice { Platform = "web" };
        e.Photo = new VisitExecutionEvidence { ContentId = Guid.NewGuid(), MediaType = "image/jpeg", ByteSize = 3 };

        var detail = (await w.Detail(Manager, e.Id, VisitEvidenceFlagsPermissions.Read)).Data!;
        Assert.Equal((exception.Id, "approver-1"), (detail.Exception!.Id, detail.Exception.ApprovedBy));
        Assert.Equal(new[] { VisitEvidenceExceptionRules.LocationInfo }, detail.Exception.Rules);
        Assert.Equal("web", detail.Row.Device!.Platform);
        Assert.True(detail.Row.HasPhoto);
        Assert.DoesNotContain(e.Photo.ContentId.ToString(), System.Text.Json.JsonSerializer.Serialize(detail)); // no content id
    }

    // ═══ W3E-BE-c (CT-2, D-6) — the QR summary on the rows, the drawer's slides and photo time ═══════════════════

    [Fact]
    public async Task CT2_a_row_carries_the_qr_verdict_with_the_scanned_institutions_name_in_the_same_institution_read()
    {
        var w = new World();
        var ok = w.Start(Rep, 0);
        var own = w.Plans.Items.Single(p => p.Id == ok.PlannedVisitId).AccountId!.Value;
        ok.Qr = new VisitExecutionQr { Status = VisitQrScanStatuses.Ok, AccountId = own, Version = 3, ScannedAt = Now.AddMinutes(-1), MatchedVia = VisitQrMatchedVia.Self };
        var elsewhere = new Account { Id = Guid.NewGuid(), TenantId = Tenant, AccountName = "Ankara Şehir Hastanesi" };
        w.Accounts.Items.Add(elsewhere);
        var wrong = w.Start(Rep, -1, VisitExecutionFlags.QrWrongAccount);
        wrong.Qr = new VisitExecutionQr { Status = VisitQrScanStatuses.WrongAccount, AccountId = elsewhere.Id, Version = 1 };
        var missing = w.Start(Rep, -2);
        missing.Qr = new VisitExecutionQr { Status = VisitQrScanStatuses.Missing };
        var old = w.Start(Rep, -3);                                                   // a start before W3E: no QR at all

        var page = (await w.List(Manager, VisitEvidenceFlagsPermissions.ReadAll)).Data!;
        Assert.Equal(1, w.Accounts.ListByIdsCalls);                                   // the plans' + the scanned institutions: ONE read
        var row = page.Items.Single(r => r.ExecutionId == ok.Id).Qr!;
        Assert.Equal((VisitQrScanStatuses.Ok, own, "Hastane 0", 3, VisitQrMatchedVia.Self), (row.Status, row.AccountId!.Value, row.AccountName, row.Version!.Value, row.MatchedVia));
        Assert.Equal(Now.AddMinutes(-1), row.ScannedAt);
        var wrongRow = page.Items.Single(r => r.ExecutionId == wrong.Id).Qr!;
        Assert.Equal((elsewhere.Id, "Ankara Şehir Hastanesi"), (wrongRow.AccountId!.Value, wrongRow.AccountName));
        Assert.Equal((VisitQrScanStatuses.Missing, (Guid?)null, (string?)null), (page.Items.Single(r => r.ExecutionId == missing.Id).Qr!.Status,
            page.Items.Single(r => r.ExecutionId == missing.Id).Qr!.AccountId, page.Items.Single(r => r.ExecutionId == missing.Id).Qr!.AccountName));
        Assert.Null(page.Items.Single(r => r.ExecutionId == old.Id).Qr);

        // "QR okutulmadı" is not a flag: the flagged filter never matches a plain missing
        var flagged = (await w.List(Manager, VisitEvidenceFlagsPermissions.ReadAll, onlyFlagged: true)).Data!;
        Assert.Equal(new[] { wrong.Id }, flagged.Items.Select(r => r.ExecutionId).ToArray());
    }

    [Fact]
    public async Task CT2_another_tenants_institution_is_never_named_even_if_the_store_answered_it()
    {
        var w = new World();
        var foreign = new Account { Id = Guid.NewGuid(), TenantId = OtherTenant, AccountName = "Yabancı Kurum" };
        w.Accounts.Items.Add(foreign);
        var e = w.Start(Rep, 0, VisitExecutionFlags.QrWrongAccount);
        e.Qr = new VisitExecutionQr { Status = VisitQrScanStatuses.WrongAccount, AccountId = foreign.Id, Version = 1 };
        w.AccountReader = new LeakyAccounts(w.Accounts);

        var row = (await w.List(Manager, VisitEvidenceFlagsPermissions.ReadAll)).Data!.Items.Single();
        Assert.Equal(foreign.Id, row.Qr!.AccountId);
        Assert.Null(row.Qr.AccountName);
        var own = w.Accounts.Items.Single(a => a.Id == w.Plans.Items.Single().AccountId);
        Assert.Equal(own.AccountName, row.Target.AccountName);                        // this tenant's institution still named
        var detail = (await w.Detail(Manager, e.Id, VisitEvidenceFlagsPermissions.ReadAll)).Data!;
        Assert.Null(detail.Row.Qr!.AccountName);
    }

    [Fact]
    public async Task CT2_the_drawer_has_the_presentation_seconds_the_slides_shown_and_the_photo_time()
    {
        var w = new World();
        var e = w.Start(Rep, 0);
        Guid productA = Guid.NewGuid(), stepA = Guid.NewGuid(), stepB = Guid.NewGuid();
        e.SlideTimings = new List<VisitExecutionSlideTiming>
        {
            new() { ProductId = productA, StepId = stepA, Seconds = 40 },
            new() { ProductId = productA, StepId = stepB, Seconds = 25 },
            new() { ProductId = productA, StepId = stepA, Seconds = 15 }               // the same slide again
        };
        e.Photo = new VisitExecutionEvidence { ContentId = Guid.NewGuid(), MediaType = "image/jpeg", ByteSize = 3, CapturedAt = Now.AddMinutes(-2) };

        var detail = (await w.Detail(Manager, e.Id, VisitEvidenceFlagsPermissions.Read)).Data!;
        Assert.Equal((80, 2, Now.AddMinutes(-2)), (detail.SlideSeconds, detail.SlideCount, detail.PhotoCapturedAt!.Value));

        var bare = w.Start(Rep, -1);
        var none = (await w.Detail(Manager, bare.Id, VisitEvidenceFlagsPermissions.Read)).Data!;
        Assert.Equal((0, 0, (DateTimeOffset?)null), (none.SlideSeconds, none.SlideCount, none.PhotoCapturedAt));
    }

    /// <summary>A store that answers an id read across tenants (what the handler's own tenant guard must survive).</summary>
    private sealed class LeakyAccounts(FakeAccountRepository inner) : IAccountRepository
    {
        public Task<IReadOnlyList<Account>> ListByIdsAsync(Guid tenantId, IReadOnlyCollection<Guid> ids, CancellationToken ct)
            => Task.FromResult<IReadOnlyList<Account>>(inner.Items.Where(a => ids.Contains(a.Id)).ToList());
        public Task<Account?> GetByIdAsync(Guid tenantId, Guid id, CancellationToken ct) => inner.GetByIdAsync(tenantId, id, ct);
        public Task<Account?> GetByCodeAsync(Guid tenantId, string accountCode, CancellationToken ct) => throw new NotImplementedException();
        public Task<bool> ExistsByCodeAsync(Guid tenantId, string accountCode, Guid? excludeId, CancellationToken ct) => throw new NotImplementedException();
        public Task<(IReadOnlyList<Account> Items, long Total, long UnfilteredTotal)> ListAsync(
            Guid tenantId, string? search, int page, int pageSize, string? sortBy, string? sortDir,
            IReadOnlyCollection<string>? statuses, IReadOnlyCollection<string>? accountTypes, IReadOnlyCollection<Guid>? accountIdScope, CancellationToken ct)
            => throw new NotImplementedException();
        public Task<IReadOnlyList<Account>> GetChildrenAsync(Guid tenantId, Guid parentId, CancellationToken ct) => throw new NotImplementedException();
        public Task<bool> WouldCreateCycleAsync(Guid tenantId, Guid accountId, Guid candidateParentId, CancellationToken ct) => throw new NotImplementedException();
        public Task InsertAsync(Account account, CancellationToken ct) => throw new NotImplementedException();
        public Task UpdateAsync(Account account, CancellationToken ct) => throw new NotImplementedException();
    }

    // ═══ the Mongo filter (a real mongod on the lane's own port; skipped without CRM_TEST_MONGO_URI) ═════════════

    [Fact]
    public async Task The_mongo_filter_reads_the_window_the_reps_the_flags_and_pages_newest_first()
    {
        var uri = Environment.GetEnvironmentVariable("CRM_TEST_MONGO_URI");
        if (string.IsNullOrWhiteSpace(uri) || uri.Contains(":27017", StringComparison.Ordinal)) return; // never the dev database
        Diten.CrmService.Persistence.DependencyInjection.EnsureClassMapsForTests();
        var client = new MongoClient(uri);
        var name = "W3E_FLAGS_" + Guid.NewGuid().ToString("N");
        try
        {
            var repo = new VisitExecutionRepository(client.GetDatabase(name));
            ExecutionEntity Make(string rep, int days, params string[] flags) => new()
            {
                Id = Guid.NewGuid(), TenantId = Tenant, PlannedVisitId = Guid.NewGuid(), ResourceId = rep, StartedAt = Now.AddDays(days), Flags = flags.ToList()
            };
            var a = Make(Rep, 0, VisitExecutionFlags.MockLocation);
            var b = Make(Rep, -3);
            var c = Make(Rep2, -1, VisitExecutionFlags.LocationFar);
            var old = Make(Rep, -40, VisitExecutionFlags.MockLocation);
            var foreign = Make(Rep, 0, VisitExecutionFlags.MockLocation);
            foreign.TenantId = OtherTenant;
            foreach (var e in new[] { a, b, c, old, foreign }) Assert.True(await repo.InsertAsync(e, default));

            var window = new VisitEvidenceFlagFilter(Now.AddDays(-10), Now.AddDays(1), null, null, false, null);
            var (items, total) = await repo.ListForEvidenceFlagsAsync(Tenant, window, 0, 10, default);
            Assert.Equal(3, total);
            Assert.Equal(new[] { a.Id, c.Id, b.Id }, items.Select(x => x.Id).ToArray());          // newest first, old + foreign out
            Assert.Equal(new[] { a.Id }, (await repo.ListForEvidenceFlagsAsync(Tenant, window with { AnyFlags = new[] { "mock_location" } }, 0, 10, default)).Items.Select(x => x.Id));
            Assert.Equal(2, (await repo.ListForEvidenceFlagsAsync(Tenant, window with { OnlyFlagged = true }, 0, 10, default)).Total);
            Assert.Equal(new[] { c.Id }, (await repo.ListForEvidenceFlagsAsync(Tenant, window with { ResourceIds = new[] { Rep2 } }, 0, 10, default)).Items.Select(x => x.Id));
            Assert.Equal(new[] { b.Id }, (await repo.ListForEvidenceFlagsAsync(Tenant, window with { PlannedVisitIds = new[] { b.PlannedVisitId } }, 0, 10, default)).Items.Select(x => x.Id));
            Assert.Equal(new[] { c.Id }, (await repo.ListForEvidenceFlagsAsync(Tenant, window, 1, 1, default)).Items.Select(x => x.Id));
            var summary = await repo.ListForEvidenceSummaryAsync(Tenant, window, default);
            Assert.Equal(3, summary.Count);
            Assert.Contains(summary, x => x.ResourceId == Rep2 && x.Flags.Contains(VisitExecutionFlags.LocationFar));
        }
        finally
        {
            await client.DropDatabaseAsync(name);
        }
    }

    /// <summary>W3E-BE-c (D-5) — the QR list's 30-day scan count of a whole page in ONE read, on a real mongod: only "ok"
    /// starts of this tenant since the instant count; the event's paper / copies round-trip.</summary>
    [Fact]
    public async Task The_mongo_scan_count_reads_a_page_of_institutions_at_once()
    {
        Diten.CrmService.Persistence.DependencyInjection.EnsureClassMapsForTests();
        var printed = new InstitutionQrEvent { Id = Guid.NewGuid(), TenantId = Tenant, AccountId = Guid.NewGuid(), Kind = InstitutionQrEventKinds.Printed, Paper = "labels8", Copies = 3 };
        var back = MongoDB.Bson.Serialization.BsonSerializer.Deserialize<InstitutionQrEvent>(MongoDB.Bson.BsonExtensionMethods.ToBsonDocument(printed));
        Assert.Equal(("labels8", 3), (back.Paper, back.Copies));

        var uri = Environment.GetEnvironmentVariable("CRM_TEST_MONGO_URI");
        if (string.IsNullOrWhiteSpace(uri) || uri.Contains(":27017", StringComparison.Ordinal)) return; // never the dev database
        var client = new MongoClient(uri);
        var name = "W3EC_SCANS_" + Guid.NewGuid().ToString("N");
        try
        {
            var db = client.GetDatabase(name);
            var executions = new VisitExecutionRepository(db);
            var codes = new InstitutionQrCodeRepository(db);
            Guid a = Guid.NewGuid(), b = Guid.NewGuid(), c = Guid.NewGuid();
            ExecutionEntity Scan(Guid account, int days, string status = VisitQrScanStatuses.Ok, Guid? tenant = null) => new()
            {
                Id = Guid.NewGuid(), TenantId = tenant ?? Tenant, PlannedVisitId = Guid.NewGuid(), ResourceId = Rep, StartedAt = Now.AddDays(days),
                Qr = new VisitExecutionQr { Status = status, AccountId = account, Version = 1 }
            };
            foreach (var e in new[]
                     {
                         Scan(a, 0), Scan(a, -10), Scan(a, -40),                       // 2 inside 30 days
                         Scan(b, -1), Scan(b, -2, VisitQrScanStatuses.WrongAccount),  // only the ok one
                         Scan(c, 0, tenant: OtherTenant)                              // another tenant's
                     })
            {
                Assert.True(await executions.InsertAsync(e, default));
            }

            var counts = await codes.CountScansSinceByAccountsAsync(Tenant, new[] { a, b, c }, Now.AddDays(-30), default);
            Assert.Equal(2, counts[a]);
            Assert.Equal(1, counts[b]);
            Assert.False(counts.ContainsKey(c));
            for (var i = 0; i < counts.Count; i++)
            {
                Assert.Equal(await codes.CountScansSinceAsync(Tenant, counts.Keys.ElementAt(i), Now.AddDays(-30), default), counts.Values.ElementAt(i)); // = the per-institution read
            }
        }
        finally
        {
            await client.DropDatabaseAsync(name);
        }
    }

    // ═══ helpers ═════════════════════════════════════════════════════════════════════════════════════════

    private sealed class World
    {
        public readonly Guid Model = Guid.NewGuid(), Root = Guid.NewGuid(), NodeA = Guid.NewGuid(), NodeB = Guid.NewGuid(), NodeC = Guid.NewGuid();
        public FakeTerritoryModelRepo Models { get; } = new();
        public FakeTerritoryNodeRepo Nodes { get; } = new();
        public FakeTerritoryResourceAssignmentRepo Assignments { get; } = new();
        public FakeVisitExecutionRepository Executions { get; } = new();
        public FakePlannedVisitRepository Plans { get; } = new();
        public FakeAccountRepository Accounts { get; } = new();

        /// <summary>W3E-BE-c — the institution store the handlers read (default <see cref="Accounts"/>; a leaky one for the
        /// tenant guard test).</summary>
        public IAccountRepository? AccountReader { get; set; }
        public FakeContactRepository Contacts { get; } = new();
        public Exceptions Exceptions { get; } = new();
        private sealed class Clock(DateTimeOffset now) : TimeProvider { public override DateTimeOffset GetUtcNow() => now; }

        public World()
        {
            var past = Now.AddYears(-1);
            Models.Items.Add(new TerritoryModel { Id = Model, TenantId = Tenant, Status = "active", EffectiveFrom = past });
            TerritoryNode Node(Guid id, Guid? parent) => new() { Id = id, TenantId = Tenant, ModelId = Model, ParentTerritoryId = parent, Status = "active", EffectiveFrom = past, Name = id.ToString("N")[..4] };
            Nodes.Items.AddRange(new[] { Node(Root, null), Node(NodeA, Root), Node(NodeB, Root), Node(NodeC, null) });
            Assignments.Items.Add(Assign(Rep, "Rep One", NodeA, "exact-territory", past));
            Assignments.Items.Add(Assign(Rep2, "Rep Two", NodeB, "exact-territory", past));
            Assignments.Items.Add(Assign(Stranger, "Stranger", NodeC, "exact-territory", past));
            Assignments.Items.Add(Assign(Manager, "Manager", Root, "territory-subtree", past));
        }

        private TerritoryResourceAssignment Assign(string resource, string name, Guid node, string scope, DateTimeOffset from) => new()
        {
            TenantId = Tenant, ModelId = Model, TerritoryId = node, Resource = new TerritoryResourceRef { ResourceId = resource, DisplayName = name },
            CoverageScope = scope, Status = "active", IsPrimary = true, ValidFrom = from
        };

        /// <summary>A start <paramref name="days"/> from today (negative = past) on its own plan, institution and doctor.</summary>
        public ExecutionEntity Start(string rep, int days, params string[] flags)
        {
            var account = new Account { Id = Guid.NewGuid(), TenantId = Tenant, AccountName = "Hastane " + Accounts.Items.Count };
            var contact = new Contact { Id = Guid.NewGuid(), TenantId = Tenant, DisplayName = "Dr. " + Contacts.Items.Count };
            Accounts.Items.Add(account);
            Contacts.Items.Add(contact);
            var plan = new PlanAtom
            {
                Id = Guid.NewGuid(), TenantId = Tenant, VisitCode = "E3-" + Plans.Items.Count, TargetType = PlannedVisitTargetType.Contact,
                TargetId = contact.Id, ContactId = contact.Id, AccountId = account.Id, PlannedDate = Today.AddDays(days),
                PlanStatus = PlannedVisitStatus.Planned, Resource = new PlannedVisitResourceRef { ResourceId = rep, ResourceType = "user" }
            };
            Plans.Items.Add(plan);
            var e = new ExecutionEntity
            {
                Id = Guid.NewGuid(), TenantId = Tenant, PlannedVisitId = plan.Id, ResourceId = rep, StartedAt = Now.AddDays(days).AddMinutes(-Executions.Items.Count),
                Kind = VisitExecutionKinds.InPerson, Flags = flags.ToList(), Location = new VisitExecutionLocation { DistanceMeters = 40, RadiusMeters = 300, Status = VisitLocationStatus.Ok }
            };
            Executions.Items.Add(e);
            return e;
        }

        private VisitEvidenceFlagsQueryHandlers Handlers(string caller, params string[] perms)
        {
            var ctx = new TenantContext();
            ctx.SetTenant(Tenant);
            var clock = new Clock(Now);
            var scope = new VisitEvidenceFlagScopeResolver(new TestCallerScope(caller, perms), new TeamScopeResolver(Assignments, Models, Nodes), Models, Assignments);
            return new VisitEvidenceFlagsQueryHandlers(ctx, scope, Executions, Plans, AccountReader ?? Accounts, Contacts, Exceptions, clock);
        }

        public Task<Diten.CrmService.Application.Common.Models.Response<VisitEvidenceFlagPageDto>> List(string caller, string perm,
            DateOnly? from = null, DateOnly? to = null, string? resourceId = null, string[]? flags = null, Guid? accountId = null,
            bool onlyFlagged = false, int? page = null, int? pageSize = null)
            => Handlers(caller, perm).Handle(new GetVisitEvidenceFlagsQuery(from, to, resourceId, flags, accountId, onlyFlagged, page, pageSize), default);

        public Task<Diten.CrmService.Application.Common.Models.Response<VisitEvidenceFlagSummaryDto>> Summary(string caller, string perm,
            DateOnly? from = null, DateOnly? to = null, string? resourceId = null)
            => Handlers(caller, perm).Handle(new GetVisitEvidenceFlagsSummaryQuery(from, to, resourceId), default);

        public Task<Diten.CrmService.Application.Common.Models.Response<VisitEvidenceFlagDetailDto>> Detail(string caller, Guid id, string perm)
            => Handlers(caller, perm).Handle(new GetVisitEvidenceFlagDetailQuery(id), default);
    }

    private sealed class Exceptions : IVisitEvidenceExceptionRepository
    {
        public List<VisitEvidenceException> Items { get; } = new();
        public Task<VisitEvidenceException?> GetByIdAsync(Guid tenantId, Guid id, CancellationToken ct) => Task.FromResult(Items.FirstOrDefault(e => e.TenantId == tenantId && e.Id == id));
        public Task<IReadOnlyList<VisitEvidenceException>> ListAsync(Guid tenantId, CancellationToken ct) => Task.FromResult<IReadOnlyList<VisitEvidenceException>>(Items.ToList());
        public Task<IReadOnlyList<VisitEvidenceException>> ListByResourceAsync(Guid tenantId, string resourceId, CancellationToken ct)
            => Task.FromResult<IReadOnlyList<VisitEvidenceException>>(Items.Where(e => e.ResourceId == resourceId).ToList());
        public Task InsertAsync(VisitEvidenceException entity, CancellationToken ct) { Items.Add(entity); return Task.CompletedTask; }
        public Task<bool> ReplaceAsync(VisitEvidenceException entity, int expectedVersion, CancellationToken ct) => Task.FromResult(true);
    }
}
