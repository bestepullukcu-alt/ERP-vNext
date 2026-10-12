using System.Text.Json;
using Diten.CrmService.Application.Common;
using Diten.CrmService.Application.Features.PlannedVisit;
using Diten.CrmService.Application.Features.PlannedVisit.Commands;
using Diten.CrmService.Application.Features.PlannedVisit.Handlers.CommandHandlers;
using Diten.CrmService.Application.Features.VisitExecution;
using Diten.CrmService.Application.Features.VisitReport;
using Diten.CrmService.Application.Features.VisitReport.Commands;
using Diten.CrmService.Application.Features.VisitReport.Handlers.CommandHandlers;
using Diten.CrmService.Application.Features.VisitReport.Handlers.QueryHandlers;
using Diten.CrmService.Application.Features.VisitReport.Queries;
using Diten.CrmService.Application.Tests.PlannedVisit;
using Diten.CrmService.Application.Tests.VisitReport;
using Diten.CrmService.Application.Tests.VisitScope;
using Diten.CrmService.Domain.Entities;
using Xunit;
using PlanAtom = Diten.CrmService.Domain.Entities.PlannedVisit;
using VisitReportEntity = Diten.CrmService.Domain.Entities.VisitReport;

namespace Diten.CrmService.Application.Tests.VisitExecution;

/// <summary>
/// WP-VW-W3 (BE-a) — the visit execution on the PRODUCTION handlers (Acceptance): the start rules (day, plan, second
/// start, location required / info, institution without a location, photo), the evidence store order (photo first,
/// fail-closed; a record that cannot be written compensates), SERVER time, the capture checks, finish + short visit, the
/// signature, personal notes to the author only, foreign plan 404 / read-all, in_progress + calendar fields + step
/// minutes, the report binding (W1 / W2 commands) and the tenant policy (defaults, bounds, a read writes nothing).
/// The clock is fixed: "today" is Thursday 2026-10-15 (UTC).
/// </summary>
public sealed class VisitExecutionW3Tests
{
    private static readonly Guid Tenant = Guid.Parse("aaaaaaaa-aaaa-aaaa-aaaa-aaaaaaaaaaaa");
    private const string Rep = "11111111-1111-1111-1111-111111111111";
    private const string OtherRep = "22222222-2222-2222-2222-222222222222";
    private static readonly Guid Manager = Guid.Parse("33333333-3333-3333-3333-333333333333");

    private static readonly DateOnly Today = new(2026, 10, 15);
    private static readonly DateTimeOffset Now = new(2026, 10, 15, 9, 0, 0, TimeSpan.Zero);

    // the institution, and two fixes: ≈ 111 m (inside 300 m) and ≈ 1 112 m (outside)
    private const double AccLat = 41.0, AccLng = 29.0;
    private const double NearLat = 41.001, FarLat = 41.01;

    private static readonly Guid ProductA = Guid.Parse("a0000000-0000-0000-0000-00000000000a");
    private static readonly Guid ProductB = Guid.Parse("b0000000-0000-0000-0000-00000000000b");
    private static readonly Guid StepA1 = Guid.Parse("a1000000-0000-0000-0000-000000000001");
    private static readonly Guid ContentA1 = Guid.Parse("c1000000-0000-0000-0000-000000000001");

    private sealed class FixedClock(DateTimeOffset now) : TimeProvider
    {
        public DateTimeOffset Value { get; set; } = now;
        public override DateTimeOffset GetUtcNow() => Value;
    }

    private readonly FakePlannedVisitReadRepository _plans = new();
    private readonly FakeVisitReportRepository _reports = new();
    private readonly FakeVisitExecutionRepository _executions = new();
    private readonly FakeVisitEvidencePolicyRepository _policies = new();
    private readonly FakeAccountRepository _accounts = new();
    private readonly FakeContactRepository _contacts = new();
    private readonly FakeVisitEvidenceStore _store = new();
    private readonly FakeObjectionCategories _categories = new("price", "efficacy");
    private readonly FixedClock _clock = new(Now);

    private readonly FakeUserNames _users = new(new Dictionary<Guid, string> { [Manager] = "Ayşe Yönetici" });

    private static TenantContext TenantCtx()
    {
        var ctx = new TenantContext();
        ctx.SetTenant(Tenant);
        return ctx;
    }

    private static ICallerScope RepCaller(string resource = Rep) => new TestCallerScope(resource);
    private static ICallerScope ReadAllManager() => TestCallerScope.Unrestricted(Manager.ToString());

    // ── fixtures ─────────────────────────────────────────────────────────────────────────────────────────────────

    private PlanAtom Seed(
        DateOnly? date = null, string status = PlannedVisitStatus.Planned, string resource = Rep,
        double? accountLat = AccLat, double? accountLng = AccLng)
    {
        var account = new Account { Id = Guid.NewGuid(), TenantId = Tenant, Latitude = accountLat, Longitude = accountLng };
        _accounts.Items.Add(account);
        var plan = new PlanAtom
        {
            Id = Guid.NewGuid(),
            TenantId = Tenant,
            VisitCode = "W3-" + (_plans.Items.Count + 1),
            TargetType = PlannedVisitTargetType.Contact,
            TargetId = Guid.NewGuid(),
            ContactId = Guid.NewGuid(),
            AccountId = account.Id,
            PlannedDate = date ?? Today,
            PlanStatus = status,
            Resource = new PlannedVisitResourceRef { ResourceId = resource, ResourceType = "user" },
            ContentItems = new List<PlannedVisitContentItem>
            {
                new()
                {
                    ProductId = ProductA, Role = "promo", Order = 1,
                    Steps = new List<PlannedVisitContentStep>
                    {
                        new() { StepId = StepA1, ContentId = ContentA1, Title = "Opening", Type = "slide", Minutes = 5 }
                    }
                },
                new() { ProductId = ProductB, Role = "non-promo", Order = 2 }
            }
        };
        _plans.Items.Add(plan);
        return plan;
    }

    private StartVisitExecutionHandler Starter(ICallerScope? caller = null)
        => new(TenantCtx(), new NullActorContext(), caller ?? RepCaller(), _plans, _reports, _executions, _policies,
            _accounts, _store, _clock);

    private VisitExecutionCaptureHandler Capturer(ICallerScope? caller = null)
        => new(TenantCtx(), new NullActorContext(), caller ?? RepCaller(), _plans, _executions, _policies, _users,
            _categories, _clock);

    private SignVisitExecutionHandler Signer(ICallerScope? caller = null)
        => new(TenantCtx(), new NullActorContext(), caller ?? RepCaller(), _plans, _reports, _executions, _store, _clock);

    private static StartVisitExecutionCommand StartAt(
        PlanAtom plan, double? lat = NearLat, double? lng = AccLng, double? accuracy = 20,
        VisitEvidenceFile? photo = null, string? locationError = null, string? photoCapturedAt = null)
        => new(plan.Id, "in_person", lat, lng, accuracy, locationError, photo, photoCapturedAt);

    private static VisitEvidenceFile Jpeg() => new("start.jpg", "image/jpeg", new byte[] { 1, 2, 3 });
    private static VisitEvidenceFile Png() => new("sign.png", "image/png", new byte[] { 9, 9 });

    private static VisitCaptureInput Capture(
        IReadOnlyList<VisitProductTimingInput>? timings = null, IReadOnlyList<VisitSlideTimingInput>? slides = null,
        IReadOnlyList<VisitMarkInput>? marks = null, string? note = null, IReadOnlyList<VisitPersonalNoteInput>? personal = null,
        IReadOnlyList<string>? coVisitors = null, string? kind = null)
        => new(kind, timings, slides, marks, note, personal, coVisitors);

    private async Task<VisitExecutionDto> StartedAsync(PlanAtom plan)
    {
        var res = await Starter().Handle(StartAt(plan), default);
        Assert.Equal(201, res.StatusCode);
        return res.Data!;
    }

    private static void AssertCode<T>(Diten.CrmService.Application.Common.Models.Response<T> res, int status, string code)
    {
        Assert.Equal(status, res.StatusCode);
        Assert.Contains(code, res.Errors!);
    }

    // ═══ start rules ════════════════════════════════════════════════════════════════════════════════════════════

    [Fact]
    public async Task Start_on_another_day_on_a_draft_or_cancelled_plan_or_twice_is_409()
    {
        AssertCode(await Starter().Handle(StartAt(Seed(Today.AddDays(1))), default), 409, VisitExecutionErrorCodes.NotToday);
        AssertCode(await Starter().Handle(StartAt(Seed(status: PlannedVisitStatus.Draft)), default), 409, VisitExecutionErrorCodes.NotConfirmed);
        AssertCode(await Starter().Handle(StartAt(Seed(status: PlannedVisitStatus.Cancelled)), default), 409, VisitExecutionErrorCodes.NotConfirmed);

        var plan = Seed(status: PlannedVisitStatus.Confirmed);
        await StartedAsync(plan);
        AssertCode(await Starter().Handle(StartAt(plan), default), 409, VisitExecutionErrorCodes.AlreadyStarted);
        Assert.Single(_executions.Items);
    }

    [Fact]
    public async Task Start_on_a_reported_visit_is_409_already_reported()
    {
        var plan = Seed();
        _reports.Items.Add(new VisitReportEntity
        {
            TenantId = Tenant, PlannedVisitId = plan.Id, ExecutionOutcome = VisitExecutionOutcome.Missed,
            ReportStatus = VisitReportStatus.Draft
        });
        AssertCode(await Starter().Handle(StartAt(plan), default), 409, VisitExecutionErrorCodes.AlreadyReported);
    }

    [Fact]
    public async Task Required_location_missing_far_or_inaccurate_is_409_with_the_distance()
    {
        AssertCode(await Starter().Handle(StartAt(Seed(), lat: null, lng: null, locationError: "denied"), default),
            409, VisitExecutionErrorCodes.LocationRequired);

        var far = await Starter().Handle(StartAt(Seed(), lat: FarLat), default);
        AssertCode(far, 409, VisitExecutionErrorCodes.LocationTooFar);
        var distance = int.Parse(far.Errors!.Single(e => e.StartsWith(VisitExecutionErrorCodes.DistancePrefix))
            [VisitExecutionErrorCodes.DistancePrefix.Length..]);
        Assert.InRange(distance, 1100, 1125);

        AssertCode(await Starter().Handle(StartAt(Seed(), accuracy: 400), default), 409, VisitExecutionErrorCodes.LocationInaccurate);
        AssertCode(await Starter().Handle(StartAt(Seed(), accuracy: null), default), 409, VisitExecutionErrorCodes.LocationInaccurate);
        Assert.Empty(_executions.Items);
    }

    [Fact]
    public async Task A_good_fix_starts_with_the_distance_and_status_ok()
    {
        var dto = await StartedAsync(Seed());
        Assert.Equal(VisitLocationStatus.Ok, dto.Location.Status);
        Assert.InRange(dto.Location.DistanceMeters!.Value, 100, 125);
        Assert.Equal(300, dto.Location.RadiusMeters);
        Assert.Empty(dto.Flags);
        Assert.Equal(VisitExecutionStatus.Started, dto.Status);
    }

    [Fact]
    public async Task Info_mode_records_and_flags_instead_of_refusing()
    {
        _policies.Stored = new VisitEvidencePolicy { TenantId = Tenant, LocationMode = VisitLocationModes.Info };

        var far = await Starter().Handle(StartAt(Seed(), lat: FarLat), default);
        Assert.Equal(201, far.StatusCode);
        Assert.Equal(VisitLocationStatus.Far, far.Data!.Location.Status);
        Assert.Contains(VisitExecutionFlags.LocationFar, far.Data.Flags);

        var missing = await Starter().Handle(StartAt(Seed(), lat: null, lng: null, locationError: "timeout"), default);
        Assert.Equal(201, missing.StatusCode);
        Assert.Contains(VisitExecutionFlags.LocationMissing, missing.Data!.Flags);

        var inaccurate = await Starter().Handle(StartAt(Seed(), accuracy: 900), default);
        Assert.Contains(VisitExecutionFlags.LocationInaccurate, inaccurate.Data!.Flags);
    }

    [Fact]
    public async Task An_institution_without_a_location_starts_flagged_or_is_blocked()
    {
        var allowed = await Starter().Handle(StartAt(Seed(accountLat: null, accountLng: null)), default);
        Assert.Equal(201, allowed.StatusCode);
        Assert.Equal(VisitLocationStatus.AccountUnknown, allowed.Data!.Location.Status);
        Assert.Contains(VisitExecutionFlags.AccountLocationUnknown, allowed.Data.Flags);

        _policies.Stored = new VisitEvidencePolicy { TenantId = Tenant, AccountWithoutLocation = AccountWithoutLocationModes.Block };
        AssertCode(await Starter().Handle(StartAt(Seed(accountLat: null, accountLng: null)), default),
            409, VisitExecutionErrorCodes.AccountLocationUnknown);
    }

    [Fact]
    public async Task A_required_photo_missing_is_400_and_a_wrong_file_is_400()
    {
        _policies.Stored = new VisitEvidencePolicy { TenantId = Tenant, PhotoMode = VisitPhotoModes.Required };
        AssertCode(await Starter().Handle(StartAt(Seed()), default), 400, VisitExecutionErrorCodes.PhotoRequired);
        AssertCode(await Starter().Handle(StartAt(Seed(), photo: new VisitEvidenceFile("x.pdf", "application/pdf", new byte[] { 1 })), default),
            400, VisitExecutionErrorCodes.PhotoInvalid);
        Assert.Empty(_store.Stored);
        Assert.Empty(_executions.Items);
    }

    // ═══ evidence: photo first, fail-closed, compensation ═══════════════════════════════════════════════════════

    [Fact]
    public async Task The_photo_is_stored_first_and_a_store_failure_is_503_with_no_record()
    {
        _store.FailWith = 503;
        AssertCode(await Starter().Handle(StartAt(Seed(), photo: Jpeg()), default), 503, VisitExecutionErrorCodes.EvidenceStoreUnavailable);
        Assert.Empty(_executions.Items);

        _store.FailWith = null;
        var plan = Seed();
        var dto = (await Starter().Handle(StartAt(plan, photo: Jpeg()), default)).Data!;
        var stored = Assert.Single(_store.Stored);
        Assert.Equal(plan.Id, stored.Request.PlannedVisitId);   // owning item = the planned visit
        Assert.Equal(dto.Id, stored.Request.ExecutionId);         // owning version = the execution
        Assert.True(dto.Photo.Present);
        Assert.Equal("image/jpeg", dto.Photo.MediaType);
    }

    [Fact]
    public async Task A_record_that_cannot_be_written_compensates_the_stored_photo()
    {
        _executions.ThrowOnInsert = true;
        await Assert.ThrowsAsync<InvalidOperationException>(() => Starter().Handle(StartAt(Seed(), photo: Jpeg()), default));
        Assert.Equal(_store.Stored.Select(s => s.ContentId), _store.Compensated);

        _executions.ThrowOnInsert = false;
        _executions.RefuseInsert = true; // a concurrent start won the unique index
        AssertCode(await Starter().Handle(StartAt(Seed(), photo: Jpeg()), default), 409, VisitExecutionErrorCodes.AlreadyStarted);
        Assert.Equal(2, _store.Compensated.Count);
    }

    // ═══ server time ═════════════════════════════════════════════════════════════════════════════════════════════

    [Fact]
    public async Task Started_and_finished_are_the_server_clock_never_a_client_instant()
    {
        var plan = Seed();
        var started = (await Starter().Handle(
            StartAt(plan, photo: Jpeg(), photoCapturedAt: "2026-10-15T06:00:00Z"), default)).Data!;
        Assert.Equal(Now, started.StartedAt);
        Assert.Equal(new DateTimeOffset(2026, 10, 15, 6, 0, 0, TimeSpan.Zero), started.Photo.CapturedAt); // informative only

        _clock.Value = Now.AddMinutes(14);
        var finished = (await Capturer().Handle(new FinishVisitExecutionCommand(started.Id, null), default)).Data!;
        Assert.Equal(Now.AddMinutes(14), finished.FinishedAt);
        Assert.Equal(14 * 60, finished.DurationSeconds);
        Assert.False(finished.ShortVisit);
    }

    // ═══ capture ═════════════════════════════════════════════════════════════════════════════════════════════════

    [Fact]
    public async Task Capture_refuses_foreign_products_steps_bad_seconds_and_vocabularies()
    {
        var dto = await StartedAsync(Seed());
        var bad = new[]
        {
            Capture(timings: new[] { new VisitProductTimingInput(Guid.NewGuid(), 10) }),
            Capture(timings: new[] { new VisitProductTimingInput(ProductA, 86401) }),
            Capture(slides: new[] { new VisitSlideTimingInput(ProductA, Guid.NewGuid(), 5) }),
            Capture(slides: new[] { new VisitSlideTimingInput(ProductB, StepA1, 5) }),
            Capture(marks: new[] { new VisitMarkInput(ProductA, "bored", null, null) }),
            Capture(marks: new[] { new VisitMarkInput(ProductA, null, new[] { "gift" }, null) }),
            Capture(note: new string('x', 2001)),
            Capture(personal: new[] { new VisitPersonalNoteInput(ProductA, new string('y', 1001)) }),
            Capture(kind: "phone")
        };
        foreach (var capture in bad)
        {
            AssertCode(await Capturer().Handle(new CaptureVisitExecutionCommand(dto.Id, capture), default),
                400, VisitExecutionErrorCodes.CaptureInvalid);
        }
    }

    [Fact]
    public async Task The_objection_category_must_be_in_the_reference_set_and_a_missing_set_refuses_it()
    {
        var dto = await StartedAsync(Seed());
        var withCategory = Capture(marks: new[] { new VisitMarkInput(ProductA, "neutral", new[] { "objection" }, "price") });

        var ok = await Capturer().Handle(new CaptureVisitExecutionCommand(dto.Id, withCategory), default);
        Assert.Equal(200, ok.StatusCode);
        Assert.Equal("price", ok.Data!.Marks.Single().ObjectionCategory);

        AssertCode(await Capturer().Handle(new CaptureVisitExecutionCommand(dto.Id,
                Capture(marks: new[] { new VisitMarkInput(ProductA, null, null, "weather") })), default),
            400, VisitExecutionErrorCodes.CaptureInvalid);

        _categories.Published = false; // W3-BE-b adds the set; until it is published a category is refused
        AssertCode(await Capturer().Handle(new CaptureVisitExecutionCommand(dto.Id, withCategory), default),
            400, VisitExecutionErrorCodes.CaptureInvalid);
    }

    [Fact]
    public async Task Co_visitors_are_at_most_three_tenant_users_never_the_rep()
    {
        var dto = await StartedAsync(Seed());
        AssertCode(await Capturer().Handle(new CaptureVisitExecutionCommand(dto.Id, Capture(coVisitors: new[] { Rep })), default),
            400, VisitExecutionErrorCodes.CoVisitorInvalid);
        AssertCode(await Capturer().Handle(new CaptureVisitExecutionCommand(dto.Id, Capture(coVisitors: new[] { Guid.NewGuid().ToString() })), default),
            400, VisitExecutionErrorCodes.CoVisitorInvalid); // not resolvable by Auth
        AssertCode(await Capturer().Handle(new CaptureVisitExecutionCommand(dto.Id,
                Capture(coVisitors: Enumerable.Range(0, 4).Select(_ => Guid.NewGuid().ToString()).ToList())), default),
            400, VisitExecutionErrorCodes.CoVisitorInvalid);

        var ok = await Capturer().Handle(new CaptureVisitExecutionCommand(dto.Id, Capture(coVisitors: new[] { Manager.ToString() })), default);
        Assert.Equal(200, ok.StatusCode);
        Assert.Equal("Ayşe Yönetici", ok.Data!.CoVisitors.Single().DisplayName);
    }

    [Fact]
    public async Task Capture_is_a_full_replacement_and_after_the_finish_it_is_409()
    {
        var dto = await StartedAsync(Seed());
        await Capturer().Handle(new CaptureVisitExecutionCommand(dto.Id, Capture(
            timings: new[] { new VisitProductTimingInput(ProductA, 30) }, note: "first")), default);
        var second = (await Capturer().Handle(new CaptureVisitExecutionCommand(dto.Id, Capture(
            slides: new[] { new VisitSlideTimingInput(ProductA, StepA1, 12) }, kind: "remote")), default)).Data!;
        Assert.Empty(second.ProductTimings);
        Assert.Null(second.Note);
        Assert.Equal(12, second.SlideTimings.Single().Seconds);
        Assert.Equal("remote", second.Kind);

        _clock.Value = Now.AddMinutes(1);
        var finished = (await Capturer().Handle(new FinishVisitExecutionCommand(dto.Id, Capture(note: "done")), default)).Data!;
        Assert.Equal("done", finished.Note);
        AssertCode(await Capturer().Handle(new CaptureVisitExecutionCommand(dto.Id, Capture()), default),
            409, VisitExecutionErrorCodes.ExecutionFinished);
        AssertCode(await Capturer().Handle(new FinishVisitExecutionCommand(dto.Id, null), default),
            409, VisitExecutionErrorCodes.ExecutionFinished);
    }

    [Fact]
    public async Task A_visit_shorter_than_the_threshold_is_flagged_short_only_when_the_tenant_checks_it()
    {
        var quick = await StartedAsync(Seed());
        _clock.Value = Now.AddSeconds(90);
        var done = (await Capturer().Handle(new FinishVisitExecutionCommand(quick.Id, null), default)).Data!;
        Assert.True(done.ShortVisit);
        Assert.Contains(VisitExecutionFlags.ShortVisit, done.Flags);

        _clock.Value = Now;
        _policies.Stored = new VisitEvidencePolicy { TenantId = Tenant, DurationConsistency = false };
        var unchecked_ = await StartedAsync(Seed());
        _clock.Value = Now.AddSeconds(90);
        Assert.False((await Capturer().Handle(new FinishVisitExecutionCommand(unchecked_.Id, null), default)).Data!.ShortVisit);
    }

    // ═══ signature ═══════════════════════════════════════════════════════════════════════════════════════════════

    [Fact]
    public async Task The_signature_needs_the_finish_is_taken_once_and_its_context_follows_the_sample_request()
    {
        var dto = await StartedAsync(Seed());
        AssertCode(await Signer().Handle(new SignVisitExecutionCommand(dto.Id, Png(), true, "approval"), default),
            409, VisitExecutionErrorCodes.ExecutionNotFinished);

        await Capturer().Handle(new FinishVisitExecutionCommand(dto.Id, Capture(
            marks: new[] { new VisitMarkInput(ProductB, "interested", new[] { "sample_request" }, null) })), default);
        AssertCode(await Signer().Handle(new SignVisitExecutionCommand(dto.Id, Jpeg(), true, null), default),
            400, VisitExecutionErrorCodes.CaptureInvalid); // png only

        var signed = await Signer().Handle(new SignVisitExecutionCommand(dto.Id, Png(), true, "approval"), default);
        Assert.Equal(200, signed.StatusCode);
        Assert.True(signed.Data!.Signature.Present);
        Assert.Equal(VisitSignatureContexts.Sample, signed.Data.Signature.Context); // derived, not the client's word

        AssertCode(await Signer().Handle(new SignVisitExecutionCommand(dto.Id, Png(), true, null), default),
            409, VisitExecutionErrorCodes.SignatureExists);
    }

    [Fact]
    public async Task A_signature_whose_record_cannot_be_written_is_compensated()
    {
        var dto = await StartedAsync(Seed());
        await Capturer().Handle(new FinishVisitExecutionCommand(dto.Id, null), default);
        _executions.RefuseReplace = true;
        AssertCode(await Signer().Handle(new SignVisitExecutionCommand(dto.Id, Png(), true, null), default),
            409, VisitExecutionErrorCodes.ConcurrencyConflict);
        Assert.Equal(_store.Stored.Select(s => s.ContentId), _store.Compensated);
    }

    // ═══ reads: personal notes, ownership, evidence ═════════════════════════════════════════════════════════════

    [Fact]
    public async Task Personal_notes_go_to_their_author_only()
    {
        var plan = Seed();
        var dto = await StartedAsync(plan);
        await Capturer().Handle(new CaptureVisitExecutionCommand(dto.Id, Capture(
            personal: new[] { new VisitPersonalNoteInput(ProductA, "ask about the trial") })), default);

        var own = (await new GetVisitExecutionByPlanHandler(TenantCtx(), RepCaller(), _plans, _executions, _users)
            .Handle(new GetVisitExecutionByPlanQuery(plan.Id), default)).Data!;
        Assert.Equal("ask about the trial", own.PersonalNotes.Single().Text);

        var manager = (await new GetVisitExecutionByPlanHandler(TenantCtx(), ReadAllManager(), _plans, _executions, _users)
            .Handle(new GetVisitExecutionByPlanQuery(plan.Id), default)).Data!;
        Assert.Empty(manager.PersonalNotes);
    }

    [Fact]
    public async Task A_foreign_plan_is_404_read_all_reads_and_a_not_started_visit_is_data_null()
    {
        var plan = Seed();
        AssertCode(await Starter(RepCaller(OtherRep)).Handle(StartAt(plan), default), 404,
            Diten.CrmService.Application.Features.VisitReport.Contract.VisitReportErrorCodes.PlannedVisitNotFound);
        Assert.Equal(403, (await Starter(ReadAllManager()).Handle(StartAt(plan), default)).StatusCode); // never on someone's behalf

        var notStarted = await new GetVisitExecutionByPlanHandler(TenantCtx(), RepCaller(), _plans, _executions, _users)
            .Handle(new GetVisitExecutionByPlanQuery(plan.Id), default);
        Assert.Equal(200, notStarted.StatusCode);
        Assert.Null(notStarted.Data);

        var dto = await StartedAsync(plan);
        Assert.Equal(404, (await new GetVisitExecutionByPlanHandler(TenantCtx(), RepCaller(OtherRep), _plans, _executions, _users)
            .Handle(new GetVisitExecutionByPlanQuery(plan.Id), default)).StatusCode);
        Assert.Equal(dto.Id, (await new GetVisitExecutionByPlanHandler(TenantCtx(), ReadAllManager(), _plans, _executions, _users)
            .Handle(new GetVisitExecutionByPlanQuery(plan.Id), default)).Data!.Id);
        AssertCode(await Capturer(RepCaller(OtherRep)).Handle(new CaptureVisitExecutionCommand(dto.Id, Capture()), default),
            404, VisitExecutionErrorCodes.NotFound);
    }

    [Fact]
    public async Task The_evidence_streams_to_the_owner_and_read_all_and_no_content_id_ever_reaches_a_client()
    {
        var plan = Seed();
        var dto = (await Starter().Handle(StartAt(plan, photo: Jpeg()), default)).Data!;
        var contentId = _store.Stored.Single().ContentId;

        GetVisitEvidenceContentHandler Reader(ICallerScope caller) => new(TenantCtx(), caller, _plans, _executions, _store);
        var own = await Reader(RepCaller()).Handle(new GetVisitEvidenceContentQuery(dto.Id, VisitEvidenceKinds.Photo), default);
        Assert.Equal(200, own.StatusCode);
        Assert.Equal("image/jpeg", own.Data!.MediaType);
        Assert.Equal(404, (await Reader(RepCaller(OtherRep)).Handle(new GetVisitEvidenceContentQuery(dto.Id, VisitEvidenceKinds.Photo), default)).StatusCode);
        Assert.Equal(200, (await Reader(ReadAllManager()).Handle(new GetVisitEvidenceContentQuery(dto.Id, VisitEvidenceKinds.Photo), default)).StatusCode);
        Assert.Equal(404, (await Reader(RepCaller()).Handle(new GetVisitEvidenceContentQuery(dto.Id, VisitEvidenceKinds.Signature), default)).StatusCode);

        _store.Readable = false; // the Platform's scoped key refuses (e.g. a manager without visit-evidence.read-all)
        Assert.Equal(404, (await Reader(ReadAllManager()).Handle(new GetVisitEvidenceContentQuery(dto.Id, VisitEvidenceKinds.Photo), default)).StatusCode);

        var json = JsonSerializer.Serialize(dto);
        Assert.DoesNotContain(contentId.ToString(), json, StringComparison.OrdinalIgnoreCase);
        Assert.DoesNotContain("contentId", json, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public async Task The_brief_is_the_last_submitted_report_on_the_same_doctor()
    {
        var plan = Seed();
        var earlier = Seed(Today.AddDays(-14));
        earlier.TargetId = plan.TargetId;
        _reports.Items.Add(new VisitReportEntity
        {
            TenantId = Tenant, PlannedVisitId = earlier.Id, ExecutionOutcome = VisitExecutionOutcome.Completed,
            ReportStatus = VisitReportStatus.Submitted, ExecutedAt = new DateTimeOffset(2026, 10, 1, 10, 0, 0, TimeSpan.Zero),
            Feedback = new VisitReportFeedback { OutcomeCode = "positive", FollowUpRequired = true, FollowUpNotes = "send the study" },
            Samples = new List<VisitReportSample> { new() { ItemType = "sample", ItemId = ProductA, Quantity = 2 } }
        });

        var brief = (await new GetVisitBriefHandler(TenantCtx(), RepCaller(), _plans, _reports)
            .Handle(new GetVisitBriefQuery(plan.Id), default)).Data!;
        Assert.Equal("2026-10-01", brief.LastVisitDate);
        Assert.Equal("positive", brief.OutcomeCode);
        Assert.Equal("send the study", brief.FollowUpNotes);
        Assert.Equal(2, brief.Samples.Single().Quantity);

        var empty = (await new GetVisitBriefHandler(TenantCtx(), RepCaller(), _plans, _reports)
            .Handle(new GetVisitBriefQuery(Seed().Id), default)).Data!;
        Assert.Null(empty.LastVisitDate);
        Assert.Empty(empty.Samples);
    }

    // ═══ status + calendar ═══════════════════════════════════════════════════════════════════════════════════════

    [Fact]
    public async Task In_progress_sits_before_today_and_the_calendar_carries_the_execution_and_step_minutes()
    {
        var plan = Seed();
        Assert.Equal(VisitWorkStatus.Today, VisitWorkStatus.Derive(plan, null, Now));
        var dto = await StartedAsync(plan);
        Assert.Equal(VisitWorkStatus.InProgress, VisitWorkStatus.Derive(plan, null, Now, _executions.Items.Single()));
        Assert.Equal(VisitWorkStatus.All.ToList().IndexOf(VisitWorkStatus.Today) - 1,
            VisitWorkStatus.All.ToList().IndexOf(VisitWorkStatus.InProgress));

        var calendar = new GetVisitCalendarHandler(
            TenantCtx(), _plans, _reports, RepCaller(), new VisitTargetNameReader(_accounts, _contacts), null, _clock, _executions);
        var item = (await calendar.Handle(new GetVisitCalendarQuery("2026-10-15", "2026-10-15"), default)).Data!.Items.Single();
        Assert.Equal(VisitWorkStatus.InProgress, item.WorkStatus);
        Assert.Equal(dto.Id, item.ExecutionId);
        Assert.Equal(VisitExecutionStatus.Started, item.ExecutionStatus);
        Assert.Equal(Now, item.StartedAt);
        Assert.Null(item.FinishedAt);
        var step = item.PlannedContent!.First().Steps.Single();
        Assert.Equal(5, step.Minutes);
        Assert.Equal(ContentA1, step.ContentId);

        _clock.Value = Now.AddMinutes(20);
        await Capturer().Handle(new FinishVisitExecutionCommand(dto.Id, null), default);
        var after = (await calendar.Handle(new GetVisitCalendarQuery("2026-10-15", "2026-10-15"), default)).Data!.Items.Single();
        Assert.Equal(VisitWorkStatus.Today, after.WorkStatus); // finished: the earlier rules apply again
        Assert.Equal(Now.AddMinutes(20), after.FinishedAt);

        var onlyInProgress = await calendar.Handle(new GetVisitCalendarQuery("2026-10-15", "2026-10-15", null, "in_progress"), default);
        Assert.Equal(200, onlyInProgress.StatusCode);
    }

    // ═══ report binding (W1 / W2 commands) ═══════════════════════════════════════════════════════════════════════

    [Fact]
    public async Task A_started_visit_is_not_missed_rescheduled_or_cancelled()
    {
        var plan = Seed();
        await StartedAsync(plan);

        var outcome = new RecordVisitOutcomeHandler(TenantCtx(), new NullActorContext(), _reports, _plans, RepCaller(), _clock,
            FakeVisitReasonSet.Permissive().Validator(), null, _executions);
        var missed = await outcome.Handle(new RecordVisitOutcomeCommand(plan.Id, "missed", null, "doctor_unavailable", null, null, null, null), default);
        AssertCode(missed, 409, VisitExecutionErrorCodes.ExecutionStarted);
        var rescheduled = await outcome.Handle(new RecordVisitOutcomeCommand(plan.Id, "rescheduled", null, "doctor_unavailable", "2026-10-20", null, null, null), default);
        AssertCode(rescheduled, 409, VisitExecutionErrorCodes.ExecutionStarted);

        var cancel = new CancelPlannedVisitHandler(TenantCtx(), new NullActorContext(), _plans, RepCaller(), null, _clock, _executions);
        var cancelled = await cancel.Handle(new CancelPlannedVisitCommand(plan.Id, "changed my mind", null), default);
        Assert.Equal(409, cancelled.StatusCode);
        Assert.Contains(VisitExecutionErrorCodes.ExecutionStarted, cancelled.Errors!);
        Assert.Empty(_reports.Items);
    }

    [Fact]
    public async Task An_unfinished_visit_is_not_submitted_and_a_finished_one_reports_the_server_start()
    {
        var plan = Seed();
        var dto = await StartedAsync(plan);
        var submit = new SubmitVisitReportHandler(TenantCtx(), new NullActorContext(), _reports, _plans, RepCaller(), _clock,
            null, null, _executions);
        var command = new SubmitVisitReportCommand(plan.Id, null, null,
            new VisitReportFeedbackInput("fine", "positive", false, null), "2026-10-15T23:00:00Z", null, null);

        AssertCode(await submit.Handle(command, default), 409, VisitExecutionErrorCodes.ExecutionNotFinished);
        Assert.Empty(_reports.Items);

        _clock.Value = Now.AddMinutes(25);
        await Capturer().Handle(new FinishVisitExecutionCommand(dto.Id, null), default);
        Assert.Equal(201, (await submit.Handle(command, default)).StatusCode);
        Assert.Equal(Now, _reports.Items.Single().ExecutedAt); // the server startedAt, not the client's 23:00
    }

    [Fact]
    public async Task A_completed_outcome_recorded_after_the_finish_takes_the_server_start()
    {
        var plan = Seed();
        var dto = await StartedAsync(plan);
        _clock.Value = Now.AddMinutes(10);
        await Capturer().Handle(new FinishVisitExecutionCommand(dto.Id, null), default);

        var outcome = new RecordVisitOutcomeHandler(TenantCtx(), new NullActorContext(), _reports, _plans, RepCaller(), _clock,
            FakeVisitReasonSet.Permissive().Validator(), null, _executions);
        Assert.Equal(201, (await outcome.Handle(
            new RecordVisitOutcomeCommand(plan.Id, "completed", "2026-10-15T18:00:00Z", null, null, null, null, null), default)).StatusCode);
        Assert.Equal(Now, _reports.Items.Single().ExecutedAt);
    }

    // ═══ policy ══════════════════════════════════════════════════════════════════════════════════════════════════

    [Fact]
    public async Task The_policy_reads_the_defaults_without_writing_and_the_contract_publishes_it()
    {
        var policy = (await new GetVisitEvidencePolicyHandler(TenantCtx(), _policies).Handle(new GetVisitEvidencePolicyQuery(), default)).Data!;
        Assert.True(policy.IsDefault);
        Assert.Equal(VisitLocationModes.Required, policy.LocationMode);
        Assert.Equal(VisitPhotoModes.Optional, policy.PhotoMode);
        Assert.Equal(300, policy.RadiusMeters);
        Assert.Equal(150, policy.MaxAccuracyMeters);
        Assert.Equal(120, policy.ShortVisitSeconds);
        Assert.Equal(AccountWithoutLocationModes.AllowFlagged, policy.AccountWithoutLocation);
        Assert.True(policy.DoctorConfirmation && policy.DurationConsistency);

        var contract = (await new GetVisitExecutionContractHandler(TenantCtx(), _policies)
            .Handle(new GetVisitExecutionContractQuery(), default)).Data!;
        Assert.Equal("MOD-0155-VX", contract.ModuleId);
        Assert.Equal("visit-objection-category", contract.ObjectionCategorySet);
        Assert.Equal((2000, 1000, 3), (contract.MaxNoteLength, contract.MaxPersonalNoteLength, contract.MaxCoVisitors));
        Assert.Contains(VisitExecutionErrorCodes.LocationTooFar, contract.ErrorCodes);
        Assert.Equal(new[] { "crm.visit-execution.read", "crm.visit-execution.record", "crm.visit-evidence-policy.manage" }, contract.Permissions);
        Assert.Equal(0, _policies.Writes); // reads never write
    }

    [Theory]
    [InlineData("radius", 49)]
    [InlineData("radius", 5001)]
    [InlineData("accuracy", 9)]
    [InlineData("accuracy", 1001)]
    [InlineData("short", 29)]
    [InlineData("short", 1801)]
    public async Task The_policy_bounds_are_enforced(string field, int value)
    {
        var input = new VisitEvidencePolicyInput(null,
            field == "radius" ? value : null, field == "accuracy" ? value : null, null, null, null,
            field == "short" ? value : null, null, null);
        AssertCode(await new UpdateVisitEvidencePolicyHandler(TenantCtx(), new NullActorContext(), _policies)
            .Handle(new UpdateVisitEvidencePolicyCommand(input), default), 400, VisitExecutionErrorCodes.PolicyInvalid);
        Assert.Equal(0, _policies.Writes);
    }

    [Fact]
    public async Task A_policy_put_keeps_absent_fields_and_rejects_an_unknown_mode()
    {
        var handler = new UpdateVisitEvidencePolicyHandler(TenantCtx(), new NullActorContext(), _policies);
        var saved = (await handler.Handle(new UpdateVisitEvidencePolicyCommand(new VisitEvidencePolicyInput(
            "info", 500, null, "required", null, null, null, null, null)), default)).Data!;
        Assert.False(saved.IsDefault);
        Assert.Equal((VisitLocationModes.Info, 500, 150, VisitPhotoModes.Required),
            (saved.LocationMode, saved.RadiusMeters, saved.MaxAccuracyMeters, saved.PhotoMode));

        AssertCode(await handler.Handle(new UpdateVisitEvidencePolicyCommand(new VisitEvidencePolicyInput(
            "sometimes", null, null, null, null, null, null, null, null)), default), 400, VisitExecutionErrorCodes.PolicyInvalid);
    }
}
