using Diten.CrmService.Application.Common;
using Diten.CrmService.Application.Common.Artifacts;
using Diten.CrmService.Application.Common.Models;
using Diten.CrmService.Application.Features.VisitEvidence;
using Diten.CrmService.Application.Features.VisitExecution;
using Diten.CrmService.Application.Features.VisitQr;
using Diten.CrmService.Domain.Entities;
using Diten.CrmService.Domain.Repositories;
using MediatR;
using Microsoft.Extensions.DependencyInjection;
using Xunit;
using PlanAtom = Diten.CrmService.Domain.Entities.PlannedVisit;

namespace Diten.CrmService.Application.Tests.VisitExecution;

/// <summary>
/// WP-VW-W3E · W3E-BE-c on the PRODUCTION handlers, with the W3 fixture (today = 2026-10-15 UTC): CT-1 (no verifier + a QR
/// mode → 503; off → 201) and the container that hands the start handler its verifier (no Mongo: fakes only); the
/// <c>qr-check</c> pre-check against the start over the whole §2.4 table (same verdict / refusal / flag, nothing written),
/// its plan rules, <c>expectedHasActiveCode</c>, the scanned institution's name, the hidden payload; D-4 print paper /
/// copies on the printed event; D-5 the QR list's parent / inherited code / 30-day scans in a constant number of reads.
/// </summary>
public sealed partial class VisitExecutionW3Tests
{
    private CheckVisitQrHandler QrChecker(IVisitQrVerifier? verifier = null, bool withVerifier = true, ICallerScope? caller = null)
        => new(TenantCtx(), caller ?? RepCaller(), _plans, _reports, _executions, _policies, _accounts, _clock,
            new VisitEvidencePolicyResolver(_policies, _exceptionStore),
            withVerifier ? verifier ?? new VisitQrVerifier(_qrCodes, _accounts, _qrKeys) : null);

    /// <summary>The §2.4 table's scenarios (the BE-a start table's set-up): the payload and the kind to send.</summary>
    private async Task<(string? Payload, string Kind)> QrScenarioAsync(string scenario, PlanAtom plan)
    {
        var account = plan.AccountId!.Value;
        switch (scenario)
        {
            case "off":
                return ("not-even-a-payload", "in_person");
            case "remote":
                return (null, "remote");
            case "ok":
                await IssueAsync(account);
                return (await PayloadAsync(account), "in_person");
            case "missing":
                await IssueAsync(account);
                return (null, "in_person");
            case "invalid":
                await IssueAsync(account);
                return (Tamper(await PayloadAsync(account), 60), "in_person");
            case "wrong":
                await IssueAsync(account);
                var other = NewAccount();
                await IssueAsync(other.Id);
                return (await PayloadAsync(other.Id), "in_person");
            case "revoked":
                await IssueAsync(account);
                var old = await PayloadAsync(account);
                await IssueAsync(account, expected: 1);
                return (old, "in_person");
            case "not_issued":
                return (null, "in_person");
            case "unverifiable":
                await IssueAsync(account);
                var payload = await PayloadAsync(account);
                _qrKeys.Keys = VisitQrKeys.None;
                return (payload, "in_person");
            default:
                throw new ArgumentOutOfRangeException(nameof(scenario));
        }
    }

    // ═══ CT-1 — fail-closed without a verifier ══════════════════════════════════════════════════════════════════

    [Theory]
    [InlineData(VisitQrModes.Required)]
    [InlineData(VisitQrModes.Optional)]
    public async Task CT1_a_start_without_a_verifier_under_a_qr_mode_is_503_and_writes_nothing(string mode)
    {
        QrMode(mode);
        var res = await Starter().Handle(StartAt(Seed()), default); // Starter(): no verifier
        AssertCode(res, 503, VisitExecutionErrorCodes.QrSigningUnavailable);
        Assert.Empty(_executions.Items);
        Assert.Empty(_store.Stored);
    }

    [Fact]
    public async Task CT1_a_start_without_a_verifier_in_off_mode_starts_as_before()
    {
        QrMode(VisitQrModes.Off);
        var res = await Starter().Handle(StartAt(Seed()), default);
        Assert.Equal(201, res.StatusCode);
        Assert.Null(res.Data!.Qr);
        Assert.Equal(201, (await Starter().Handle(StartAt(Seed()), default)).StatusCode); // no policy document: off too
    }

    [Fact]
    public async Task CT1_the_container_hands_the_mediator_start_handler_its_qr_verifier()
    {
        QrMode(VisitQrModes.Required);
        var plan = Seed();
        await IssueAsync(plan.AccountId!.Value); // an active code: no payload is qr_required (the verifier ran), not 503

        var services = new ServiceCollection();
        services.AddLogging();
        services.AddApplication(); // the production registrations; every store below is a fake (no Mongo, no 27017)
        services.AddSingleton<ITenantContext>(TenantCtx());
        services.AddSingleton<IActorContext>(new NullActorContext());
        services.AddSingleton(RepCaller());
        services.AddSingleton<IPlannedVisitRepository>(_plans);
        services.AddSingleton<IVisitReportRepository>(_reports);
        services.AddSingleton<IVisitExecutionRepository>(_executions);
        services.AddSingleton<IVisitEvidencePolicyRepository>(_policies);
        services.AddSingleton<IVisitEvidenceExceptionRepository>(_exceptionStore);
        services.AddSingleton<IAccountRepository>(_accounts);
        services.AddSingleton<IVisitEvidenceStore>(_store);
        services.AddSingleton<TimeProvider>(_clock);
        services.AddSingleton<IInstitutionQrCodeRepository>(_qrCodes);
        services.AddSingleton<IVisitQrKeyRing>(_qrKeys);
        // WP-VW-W3L — the resolver now reads the evidence layers and the rep's territory (business unit) too
        services.AddSingleton<IVisitEvidencePolicyLayerRepository>(new VisitWorkspace.FakeLayerRepository());
        services.AddSingleton<ITerritoryModelRepository>(new Territory.FakeTerritoryModelRepo());
        services.AddSingleton<ITerritoryResourceAssignmentRepository>(new Territory.FakeTerritoryResourceAssignmentRepo());
        using var provider = services.BuildServiceProvider(new ServiceProviderOptions { ValidateScopes = true });
        using var scope = provider.CreateScope();

        Assert.IsType<VisitQrVerifier>(scope.ServiceProvider.GetService<IVisitQrVerifier>());
        var handler = scope.ServiceProvider.GetRequiredService<IRequestHandler<StartVisitExecutionCommand, Response<VisitExecutionDto>>>();
        var field = typeof(StartVisitExecutionHandler).GetField("_qr", System.Reflection.BindingFlags.Instance | System.Reflection.BindingFlags.NonPublic)!;
        Assert.NotNull(field.GetValue(handler));

        var res = await scope.ServiceProvider.GetRequiredService<IMediator>().Send(StartQr(plan, null));
        AssertCode(res, 400, VisitExecutionErrorCodes.QrRequired);
        var check = await scope.ServiceProvider.GetRequiredService<IMediator>().Send(new CheckVisitQrQuery(plan.Id, null));
        Assert.Equal((200, VisitQrScanStatuses.Missing, true), (check.StatusCode, check.Data!.Status, check.Data.WouldRefuse));
    }

    // ═══ §2.10 — qr-check: the start's verdict, written nowhere ═══════════════════════════════════════════════

    [Theory]
    [InlineData(VisitQrModes.Off, "off")]
    [InlineData(VisitQrModes.Required, "remote")]
    [InlineData(VisitQrModes.Required, "ok")]
    [InlineData(VisitQrModes.Required, "missing")]
    [InlineData(VisitQrModes.Required, "invalid")]
    [InlineData(VisitQrModes.Required, "wrong")]
    [InlineData(VisitQrModes.Required, "revoked")]
    [InlineData(VisitQrModes.Required, "not_issued")]
    [InlineData(VisitQrModes.Required, "unverifiable")]
    [InlineData(VisitQrModes.Optional, "ok")]
    [InlineData(VisitQrModes.Optional, "missing")]
    [InlineData(VisitQrModes.Optional, "invalid")]
    [InlineData(VisitQrModes.Optional, "wrong")]
    [InlineData(VisitQrModes.Optional, "revoked")]
    [InlineData(VisitQrModes.Optional, "not_issued")]
    [InlineData(VisitQrModes.Optional, "unverifiable")]
    public async Task QrCheck_answers_every_table_row_as_the_start_does_and_writes_nothing(string mode, string scenario)
    {
        QrMode(mode);
        var plan = Seed();
        var (payload, kind) = await QrScenarioAsync(scenario, plan);
        var scans = _qrCodes.Items.Select(c => (c.Id, c.ScanCount, c.LastScannedAt)).ToList();
        var events = _qrEvents.Items.Count;

        var check = await QrChecker().Handle(new CheckVisitQrQuery(plan.Id, payload, kind), default);

        Assert.Equal(200, check.StatusCode);
        Assert.Empty(_executions.Items);                                                   // no execution
        Assert.Equal(scans, _qrCodes.Items.Select(c => (c.Id, c.ScanCount, c.LastScannedAt)).ToList()); // no scan counter
        Assert.Equal(events, _qrEvents.Items.Count);                                       // no event

        var start = await QrStarter().Handle(StartQr(plan, payload, kind), default);
        var dto = check.Data!;
        Assert.Equal(mode, dto.QrMode);
        Assert.Equal(start.StatusCode != 201, dto.WouldRefuse);
        if (dto.WouldRefuse)
        {
            Assert.Contains(dto.ErrorCode!, start.Errors!);
            Assert.Null(dto.Flag);
            return;
        }

        Assert.Null(dto.ErrorCode);
        Assert.Equal(start.Data!.Qr!.Status, dto.Status);
        var startFlags = start.Data.Flags.Where(f => f.StartsWith("qr_", StringComparison.Ordinal)).ToList();
        Assert.Equal(startFlags, dto.Flag is null ? new List<string>() : new List<string> { dto.Flag });
        Assert.Equal(start.Data.Qr.AccountId, dto.AccountId);
        Assert.Equal(start.Data.Qr.MatchedVia, dto.MatchedVia);
    }

    [Fact]
    public async Task QrCheck_uses_the_starts_plan_rules_and_errors()
    {
        QrMode(VisitQrModes.Required);
        async Task Same(PlanAtom plan, ICallerScope? caller = null)
        {
            var check = await QrChecker(caller: caller).Handle(new CheckVisitQrQuery(plan.Id, null), default);
            var start = await (caller is null ? QrStarter() : new StartVisitExecutionHandler(TenantCtx(), new NullActorContext(), caller,
                _plans, _reports, _executions, _policies, _accounts, _store, _clock, new VisitEvidencePolicyResolver(_policies, _exceptionStore),
                new VisitQrVerifier(_qrCodes, _accounts, _qrKeys))).Handle(StartQr(plan, null), default);
            Assert.NotEqual(201, start.StatusCode);
            Assert.Equal(start.StatusCode, check.StatusCode);
            Assert.Equal(start.Errors, check.Errors);
        }

        await Same(Seed(resource: OtherRep));                                   // another rep's plan → 404 (as absent)
        await Same(Seed(resource: OtherRep), ReadAllManager());                 // read-all is not the rep → 403
        await Same(Seed(Today.AddDays(1)));                                     // not today
        await Same(Seed(status: PlannedVisitStatus.Draft));                     // not approved
        var started = Seed();
        await IssueAsync(started.AccountId!.Value);
        Assert.Equal(201, (await QrStarter().Handle(StartQr(started, await PayloadAsync(started.AccountId!.Value)), default)).StatusCode);
        await Same(started);                                                    // already started
        var missing = await QrChecker().Handle(new CheckVisitQrQuery(Guid.NewGuid(), null), default);
        Assert.Equal(404, missing.StatusCode);
    }

    [Fact]
    public async Task QrCheck_tells_not_issued_from_missing_and_names_only_the_scanned_institution_of_this_tenant()
    {
        QrMode(VisitQrModes.Required);
        var parent = NewAccount();
        var plan = Seed();
        AccountOf(plan).ParentAccountId = parent.Id;

        var none = (await QrChecker().Handle(new CheckVisitQrQuery(plan.Id, null), default)).Data!;
        Assert.Equal((VisitQrScanStatuses.NotIssued, false, false, VisitExecutionFlags.QrNotIssued), (none.Status, none.ExpectedHasActiveCode, none.WouldRefuse, none.Flag));

        await IssueAsync(parent.Id); // only the parent has a code: the visit expects one (K-W3E-5)
        var missing = (await QrChecker().Handle(new CheckVisitQrQuery(plan.Id, null), default)).Data!;
        Assert.Equal((VisitQrScanStatuses.Missing, true, true, VisitExecutionErrorCodes.QrRequired), (missing.Status, missing.ExpectedHasActiveCode, missing.WouldRefuse, missing.ErrorCode));

        var viaParent = (await QrChecker().Handle(new CheckVisitQrQuery(plan.Id, await PayloadAsync(parent.Id)), default)).Data!;
        Assert.Equal((VisitQrScanStatuses.Ok, parent.Id, parent.AccountName, VisitQrMatchedVia.Parent), (viaParent.Status, viaParent.AccountId, viaParent.AccountName, viaParent.MatchedVia));

        var stranger = NewAccount();
        await IssueAsync(stranger.Id);
        var wrong = (await QrChecker().Handle(new CheckVisitQrQuery(plan.Id, await PayloadAsync(stranger.Id)), default)).Data!;
        Assert.Equal((VisitQrScanStatuses.WrongAccount, stranger.Id, stranger.AccountName, VisitExecutionErrorCodes.QrWrongAccount),
            (wrong.Status, wrong.AccountId, wrong.AccountName, wrong.ErrorCode));

        // another tenant's code never verifies here (its key is that tenant's) → invalid, no institution named
        var foreign = VisitQrSigner.Sign(_qrKeys.Keys, OtherTenant, stranger.Id, 1);
        var invalid = (await QrChecker().Handle(new CheckVisitQrQuery(plan.Id, foreign), default)).Data!;
        Assert.Equal((VisitQrScanStatuses.Invalid, (Guid?)null, (string?)null), (invalid.Status, invalid.AccountId, invalid.AccountName));
    }

    [Fact]
    public async Task QrCheck_without_a_verifier_is_fail_closed_like_the_start_and_the_payload_is_never_printed()
    {
        QrMode(VisitQrModes.Required);
        var plan = Seed();
        AssertCode(await QrChecker(withVerifier: false).Handle(new CheckVisitQrQuery(plan.Id, null), default), 503, VisitExecutionErrorCodes.QrSigningUnavailable);
        QrMode(VisitQrModes.Off);
        var off = await QrChecker(withVerifier: false).Handle(new CheckVisitQrQuery(plan.Id, "x"), default);
        Assert.Equal((200, VisitQrScanStatuses.NotApplicable, false), (off.StatusCode, off.Data!.Status, off.Data.WouldRefuse));

        const string secret = "DQR1.SECRET-PAYLOAD";
        Assert.DoesNotContain("SECRET", new CheckVisitQrQuery(plan.Id, secret).ToString());
        Assert.DoesNotContain("SECRET", new VisitQrCheckInput(plan.Id, secret).ToString());
    }

    [Fact]
    public void QrCheck_route_needs_the_record_key()
    {
        var method = typeof(Api.Controllers.CRM.VisitExecutionController).GetMethod(nameof(Api.Controllers.CRM.VisitExecutionController.QrCheck))!;
        var route = Assert.Single(method.GetCustomAttributes(typeof(Microsoft.AspNetCore.Mvc.HttpPostAttribute), false).Cast<Microsoft.AspNetCore.Mvc.HttpPostAttribute>());
        Assert.Equal("api/crm/visit-execution/qr-check", route.Template);
        var permission = Assert.Single(method.GetCustomAttributes(typeof(Infrastructure.Authorization.HasPermissionAttribute), false)
            .Cast<Infrastructure.Authorization.HasPermissionAttribute>());
        Assert.Equal(VisitExecutionPermissions.Record, permission.Permission);
    }

    // ═══ D-4 — print paper / copies on the printed event ═════════════════════════════════════════════════════

    [Fact]
    public async Task Qr_print_records_paper_and_copies_and_refuses_bad_values_without_an_event()
    {
        var account = NewAccount();
        await IssueAsync(account.Id);
        var service = QrService();

        var ok = await new GetVisitQrPayloadHandler(service).Handle(new GetVisitQrPayloadCommand(account.Id, " A4 ", 2), default);
        Assert.Equal(200, ok.StatusCode);
        var printed = _qrEvents.Items.Single(e => e.Kind == InstitutionQrEventKinds.Printed);
        Assert.Equal(("a4", 2), (printed.Paper, printed.Copies));

        foreach (var (paper, copies) in new (string?, int?)[] { ("a3", null), (null, 0), (null, 51), ("labels8", -1) })
        {
            var bad = await new GetVisitQrPayloadHandler(service).Handle(new GetVisitQrPayloadCommand(account.Id, paper, copies), default);
            AssertCode(bad, 400, VisitExecutionErrorCodes.QrRequestInvalid);
        }

        Assert.Single(_qrEvents.Items, e => e.Kind == InstitutionQrEventKinds.Printed);       // the refused prints wrote nothing

        await new GetVisitQrPayloadHandler(service).Handle(new GetVisitQrPayloadCommand(account.Id), default); // an older client
        var history = (await new GetVisitQrHistoryHandler(service).Handle(new GetVisitQrHistoryQuery(account.Id), default)).Data!;
        Assert.Contains(history, h => h.Kind == InstitutionQrEventKinds.Printed && h.Paper == "a4" && h.Copies == 2);
        Assert.Contains(history, h => h.Kind == InstitutionQrEventKinds.Printed && h.Paper is null && h.Copies is null);
        Assert.Equal(new[] { "a4", "a5", "labels4", "labels8" }, VisitQrPrint.Papers);
    }

    [Fact]
    public async Task Qr_bulk_print_records_paper_and_copies_on_each_event()
    {
        var a = NewAccount();
        var b = NewAccount();
        await IssueAsync(a.Id);
        await IssueAsync(b.Id);
        var handler = new BulkVisitQrPayloadHandler(QrService());

        AssertCode(await handler.Handle(new BulkVisitQrPayloadCommand(new VisitQrBulkInput(new[] { a.Id, b.Id }, "letter", 1)), default),
            400, VisitExecutionErrorCodes.QrRequestInvalid);
        Assert.DoesNotContain(_qrEvents.Items, e => e.Kind == InstitutionQrEventKinds.Printed);

        var res = await handler.Handle(new BulkVisitQrPayloadCommand(new VisitQrBulkInput(new[] { a.Id, b.Id }, "labels8", 3)), default);
        Assert.Equal(2, res.Data!.Count);
        Assert.All(_qrEvents.Items.Where(e => e.Kind == InstitutionQrEventKinds.Printed), e => Assert.Equal(("labels8", 3), (e.Paper, e.Copies)));
    }

    // ═══ D-5 — the QR list's parent, inherited code and 30-day scans ════════════════════════════════════════

    [Fact]
    public async Task Qr_list_rows_carry_the_parent_the_inherited_code_and_recent_scans_in_constant_reads()
    {
        var listing = new ListingAccounts();
        var grand = listing.Add("Ana Hastane");
        var mid = listing.Add("Poliklinik", grand.Id);
        var unit = listing.Add("Birim", mid.Id);
        var orphan = listing.Add("Tek Kurum");
        var looped = listing.Add("Döngü");
        looped.ParentAccountId = looped.Id;                      // a self-loop ends the walk
        listing.Add("Başka kiracı", tenant: OtherTenant);         // never listed nor named
        var keys = _qrKeys;
        var service = new VisitQrService(TenantCtx(), new NullActorContext(), listing, _qrCodes, _qrEvents, keys, _clock);
        Assert.Equal(201, (await new IssueVisitQrHandler(service).Handle(new IssueVisitQrCommand(grand.Id, new VisitQrIssueInput(null, null)), default)).StatusCode);
        _qrCodes.ScansByAccount[grand.Id] = 7;

        async Task<VisitQrListDto> Page(int size)
        {
            var before = (listing.ListByIdsCalls, _qrCodes.ActiveByAccountsCalls, _qrCodes.ScansByAccountsCalls);
            var res = await new ListVisitQrAccountsHandler(service, null!, null!, null!)
                .Handle(new ListVisitQrAccountsQuery(null, null, null, null, null, 1, size), default);
            var after = (listing.ListByIdsCalls, _qrCodes.ActiveByAccountsCalls, _qrCodes.ScansByAccountsCalls);
            // ≤ one institution read + one active-code read per parent level (5), one scan count — whatever the page size
            Assert.True(after.Item1 - before.Item1 <= VisitQrLimits.MaxParentDepth);
            Assert.True(after.Item2 - before.Item2 <= VisitQrLimits.MaxParentDepth);
            Assert.Equal(1, after.Item3 - before.Item3);
            return res.Data!;
        }

        var list = await Page(25);
        Assert.Equal(5, list.Total);
        VisitQrListItemDto Row(Account a) => list.Items.Single(r => r.AccountId == a.Id);
        Assert.Equal((InstitutionQrCodeStatuses.Active, (Guid?)null, 7), (Row(grand).Status, Row(grand).ParentAccountId, Row(grand).RecentScanCount));
        Assert.Equal((grand.Id, "Ana Hastane"), (Row(mid).ParentAccountId!.Value, Row(mid).ParentAccountName));
        Assert.Equal(new VisitQrInheritedDto(grand.Id, "Ana Hastane", 1), Row(mid).InheritedFrom);
        Assert.Equal((mid.Id, "Poliklinik"), (Row(unit).ParentAccountId!.Value, Row(unit).ParentAccountName));
        Assert.Equal(new VisitQrInheritedDto(grand.Id, "Ana Hastane", 1), Row(unit).InheritedFrom);     // two levels up
        Assert.Equal((InstitutionQrCodeStatuses.None, (VisitQrInheritedDto?)null, 0), (Row(orphan).Status, Row(orphan).InheritedFrom, Row(orphan).RecentScanCount));
        Assert.Null(Row(looped).InheritedFrom);

        // the institution read answers the same inherited code (one rule)
        var state = await service.StateAsync(Tenant, unit, default);
        Assert.Equal(state.InheritedFrom, Row(unit).InheritedFrom);

        // the reads stay constant on a bigger page
        for (var i = 0; i < 20; i++) listing.Add("Ek " + i, unit.Id);
        await Page(50);
    }

    [Fact]
    public async Task Qr_list_never_names_another_tenants_parent()
    {
        var listing = new ListingAccounts();
        var foreignParent = listing.Add("Yabancı Ana", tenant: OtherTenant);
        var child = listing.Add("Çocuk", foreignParent.Id);
        listing.LeakAcrossTenants = true; // even a repository that answered another tenant's row
        var service = new VisitQrService(TenantCtx(), new NullActorContext(), listing, _qrCodes, _qrEvents, _qrKeys, _clock);
        var row = (await new ListVisitQrAccountsHandler(service, null!, null!, null!)
            .Handle(new ListVisitQrAccountsQuery(null, null, null, null, null, 1, 25), default)).Data!.Items.Single();
        Assert.Equal((child.Id, foreignParent.Id, (string?)null, (VisitQrInheritedDto?)null), (row.AccountId, row.ParentAccountId!.Value, row.ParentAccountName, row.InheritedFrom));
    }

    /// <summary>An account store that lists (search / paging / scope / exclusion) and counts its id reads; it can be told
    /// to leak another tenant's rows on an id read (the handler's own tenant guard is what is tested then).</summary>
    private sealed class ListingAccounts : IAccountRepository
    {
        public List<Account> Items { get; } = new();
        public int ListByIdsCalls { get; private set; }
        public bool LeakAcrossTenants { get; set; }

        public Account Add(string name, Guid? parent = null, Guid? tenant = null)
        {
            var a = new Account { Id = Guid.NewGuid(), TenantId = tenant ?? Tenant, AccountName = name, AccountType = "hospital", ParentAccountId = parent };
            Items.Add(a);
            return a;
        }

        public Task<IReadOnlyList<Account>> ListByIdsAsync(Guid tenantId, IReadOnlyCollection<Guid> ids, CancellationToken ct)
        {
            ListByIdsCalls++;
            return Task.FromResult<IReadOnlyList<Account>>(Items.Where(a => (LeakAcrossTenants || a.TenantId == tenantId) && ids.Contains(a.Id)).ToList());
        }

        public Task<Account?> GetByIdAsync(Guid tenantId, Guid id, CancellationToken ct)
            => Task.FromResult(Items.FirstOrDefault(a => a.TenantId == tenantId && a.Id == id));

        public Task<(IReadOnlyList<Account> Items, long Total, long UnfilteredTotal)> ListAsync(
            Guid tenantId, string? search, int page, int pageSize, string? sortBy, string? sortDir,
            IReadOnlyCollection<string>? statuses, IReadOnlyCollection<string>? accountTypes, IReadOnlyCollection<Guid>? accountIdScope, CancellationToken ct)
            => ListAsync(tenantId, search, page, pageSize, sortBy, sortDir, statuses, accountTypes, accountIdScope, null, ct);

        public Task<(IReadOnlyList<Account> Items, long Total, long UnfilteredTotal)> ListAsync(
            Guid tenantId, string? search, int page, int pageSize, string? sortBy, string? sortDir,
            IReadOnlyCollection<string>? statuses, IReadOnlyCollection<string>? accountTypes, IReadOnlyCollection<Guid>? accountIdScope,
            IReadOnlyCollection<Guid>? excludedAccountIds, CancellationToken ct)
        {
            var all = Items.Where(a => a.TenantId == tenantId).ToList();
            var rows = all.Where(a => accountIdScope is null || accountIdScope.Contains(a.Id))
                .Where(a => excludedAccountIds is null || !excludedAccountIds.Contains(a.Id))
                .OrderBy(a => a.AccountName, StringComparer.Ordinal).ToList();
            return Task.FromResult<(IReadOnlyList<Account>, long, long)>((rows.Skip((page - 1) * pageSize).Take(pageSize).ToList(), rows.Count, all.Count));
        }

        public Task<Account?> GetByCodeAsync(Guid tenantId, string accountCode, CancellationToken ct) => throw new NotImplementedException();
        public Task<bool> ExistsByCodeAsync(Guid tenantId, string accountCode, Guid? excludeId, CancellationToken ct) => throw new NotImplementedException();
        public Task<IReadOnlyList<Account>> GetChildrenAsync(Guid tenantId, Guid parentId, CancellationToken ct) => throw new NotImplementedException();
        public Task<bool> WouldCreateCycleAsync(Guid tenantId, Guid accountId, Guid candidateParentId, CancellationToken ct) => throw new NotImplementedException();
        public Task InsertAsync(Account account, CancellationToken ct) => throw new NotImplementedException();
        public Task UpdateAsync(Account account, CancellationToken ct) => throw new NotImplementedException();
    }
}
