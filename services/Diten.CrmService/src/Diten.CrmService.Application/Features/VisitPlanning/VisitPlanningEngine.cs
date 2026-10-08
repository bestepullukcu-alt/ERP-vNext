using System.Globalization;
using Diten.CrmService.Application.Common;
using Diten.CrmService.Application.Features.ConsentPreference.Evaluation;
using Diten.CrmService.Application.Features.CycleCapacity.Rules;
using Diten.CrmService.Application.Features.CycleCapacity.Services;
using Diten.CrmService.Application.Features.CyclePeriod.Read;
using Diten.CrmService.Application.Features.PlannedVisit;
using Diten.CrmService.Application.Features.PlannedVisit.Provenance;
using Diten.CrmService.Application.Features.RouteOptimization;
using Diten.CrmService.Application.Features.VisitContentSequence;
using Diten.CrmService.Application.Features.VisitPlanning.Handlers.CommandHandlers;
using Diten.CrmService.Domain.Entities;
using Diten.CrmService.Domain.Repositories;
using AccountEntity = Diten.CrmService.Domain.Entities.Account;
using CapacityEntity = Diten.CrmService.Domain.Entities.CycleCapacity;
using PlannedVisitEntity = Diten.CrmService.Domain.Entities.PlannedVisit;

namespace Diten.CrmService.Application.Features.VisitPlanning;

/// <summary>
/// MOD-0155 FU05 — the <b>MicroTarget Visit Planning Engine</b>: the coordinator that turns a rep's selection into a
/// scheduled plan (§4). It holds <b>no algorithm of its own (D8)</b> — every number comes from a shipped seam:
/// content + duration from FU04 (<see cref="VisitContentSequenceResolver"/>), order + slots from FU03
/// (<see cref="IRouteOptimizer"/>), cadence from MOD-0165 (<see cref="FrequencyExtendPlanner"/>), supply from FU06/FU06B
/// (<see cref="CycleCapacityEstimator"/>), consent from MOD-0164, availability from MOD-0150, territory from MOD-0151.
/// Its own logic is assembly + selection state + apply.
/// <para><b>Preview persists nothing</b> (dry-run). <b>Apply</b> writes FU01 PlannedVisit atoms through the atomic
/// <see cref="IPlanningSessionApplyUnitOfWork"/> (transaction + standalone fallback + compensation, D-APPLY-ATOMICITY =
/// C) and flips the session to <c>committed</c>. <b>Re-plan</b> updates the affected atoms in place (D-REPLAN = A).</para>
/// </summary>
public sealed class VisitPlanningEngine
{
    // The config-placeholder rep working day used until an HR seam exists (D-WORKINGHOURS). FU03 fills the concrete
    // window from its defaults provider when PerDay is null; StartLocation is the only per-run override we pass.
    private const int DefaultVisitDurationMinutes = 30;
    // WP-VP-3A (B2) — MaxWeeks (6) is gone: the horizon is the whole period.

    private readonly ITenantContext _tenant;
    private readonly IActorContext _actor;
    private readonly ICyclePeriodReader _periods;
    private readonly ICycleCapacityRepository _capacities;
    private readonly CycleCapacityEstimator _estimator;
    private readonly VisitContentSequenceResolver _content;
    private readonly IRouteOptimizer _optimizer;
    private readonly EligibleContactSelector _contacts;
    private readonly FrequencyExtendPlanner _frequencyExtend;
    private readonly TerritoryGate _territory;
    private readonly IAccountRepository _accounts;
    private readonly IContactRepository _contactRepo;
    private readonly IPlannedVisitRepository _plannedVisits;
    private readonly PlannedVisitJourneyProbe _journeyProbe;
    private readonly PlannedVisitFrequencyProbe _frequencyProbe;
    private readonly PlannedVisitConsentProbe _consentProbe;
    private readonly PlannedVisitAvailabilityProbe _availabilityProbe;
    private readonly PlanningWorkingCalendar _calendar;
    private readonly IVisitProvenanceDeriver _deriver;
    private readonly IVisitReportRepository? _reports;
    private readonly IRouteOptimizationDefaultsProvider? _routeDefaults;
    private readonly IAccountRelationshipRepository? _relationships;
    private readonly IProductNameReader? _productNames;

    // WP-VP-4G (F4-4) — the names read in THIS request (the engine is scoped): one bulk read, reused by the approval
    // snapshot and the preview of the same request.
    private readonly Dictionary<Guid, string> _productNameMemo = new();
    private readonly HashSet<Guid> _productNamesAsked = new();

    public VisitPlanningEngine(
        ITenantContext tenant,
        IActorContext actor,
        ICyclePeriodReader periods,
        ICycleCapacityRepository capacities,
        CycleCapacityEstimator estimator,
        VisitContentSequenceResolver content,
        IRouteOptimizer optimizer,
        EligibleContactSelector contacts,
        FrequencyExtendPlanner frequencyExtend,
        TerritoryGate territory,
        IAccountRepository accounts,
        IContactRepository contactRepo,
        IPlannedVisitRepository plannedVisits,
        PlannedVisitJourneyProbe journeyProbe,
        PlannedVisitFrequencyProbe frequencyProbe,
        PlannedVisitConsentProbe consentProbe,
        PlannedVisitAvailabilityProbe availabilityProbe,
        PlanningWorkingCalendar calendar,
        IVisitProvenanceDeriver deriver,
        // WP-VP-3A — "done" = a visit with a COMPLETED report; without the reader nothing counts as done.
        IVisitReportRepository? reports = null,
        // WP-VP-3B — the configured route day (the day budget's default hours + the per-day route window) and the
        // pharmacy → institution links (a pharmacy rides on its institution's day). Both optional: without them the
        // documented defaults apply and a pharmacy is its own group.
        IRouteOptimizationDefaultsProvider? routeDefaults = null,
        IAccountRelationshipRepository? relationships = null,
        // WP-VP-4G (F4-4) — product names for the preview and the approval snapshot (fail-open; none ⇒ codes).
        IProductNameReader? productNames = null)
    {
        _productNames = productNames;
        _reports = reports;
        _routeDefaults = routeDefaults;
        _relationships = relationships;
        _deriver = deriver;
        _tenant = tenant;
        _actor = actor;
        _periods = periods;
        _capacities = capacities;
        _estimator = estimator;
        _content = content;
        _optimizer = optimizer;
        _contacts = contacts;
        _frequencyExtend = frequencyExtend;
        _territory = territory;
        _accounts = accounts;
        _contactRepo = contactRepo;
        _plannedVisits = plannedVisits;
        _journeyProbe = journeyProbe;
        _frequencyProbe = frequencyProbe;
        _consentProbe = consentProbe;
        _availabilityProbe = availabilityProbe;
        _calendar = calendar;
    }

    /// <summary>Run the full ①–⑦ flow as a dry-run and return the transient preview. Persists NOTHING.</summary>
    public async Task<EngineOutcome> PreviewAsync(
        PlanningSession session, VisitPlanGenerationOptions options, CancellationToken cancellationToken)
    {
        var generation = await GenerateAsync(session, options, cancellationToken);
        if (generation.Error is { } error || generation.Output is null)
        {
            return EngineOutcome.Fail(generation.Error ?? "Generation failed.");
        }

        return EngineOutcome.Ok(await BuildPreviewAsync(generation.Output, cancellationToken));
    }

    /// <summary>Run the flow and produce the FU01 atoms + the committed session, ready for the atomic write. Builds the
    /// atoms (Slot / Content / Availability / Frequency / Selection all filled) but does NOT write — the handler calls
    /// the unit of work so the write + the session flip stay one all-or-nothing operation.</summary>
    /// <para>WP-VP-3A — with <paramref name="weekStart"/> (a Monday yyyy-MM-dd) only THAT week's visits become atoms
    /// ("approve the week"): the whole period is still generated so the week gets exactly what the preview showed for
    /// it. 400 <c>invalid_week</c> (not a Monday / outside the period), 409 <c>week_in_past</c>, 409
    /// <c>week_already_approved</c>. Without it, every draft week (today's behaviour).</para></summary>
    public async Task<ApplyBuildOutcome> BuildApplyAsync(
        PlanningSession session, VisitPlanGenerationOptions options, CancellationToken cancellationToken,
        string? weekStart = null)
    {
        var generation = await GenerateAsync(session, options, cancellationToken);
        if (generation.Error is { } error || generation.Output is null)
        {
            return ApplyBuildOutcome.Fail(generation.Error ?? "Generation failed.");
        }

        var output = generation.Output;
        var placedToWrite = output.Placed;
        string? weekKey = null;
        IReadOnlyList<Guid> kept = Array.Empty<Guid>();
        if (weekStart is not null)
        {
            if (!PlanningWeekCalendar.TryParseWeek(weekStart, output.PeriodStart, output.PeriodEnd, out var week))
            {
                return ApplyBuildOutcome.Fail(
                    PlanningSessionErrorCodes.InvalidWeek, "weekStart must be a Monday (yyyy-MM-dd) of a week of the plan's period.", 400);
            }

            if (PlanningWeekCalendar.IsPast(week, output.Today))
            {
                return ApplyBuildOutcome.Fail(PlanningSessionErrorCodes.WeekInPast, "A past week cannot be approved.", 409);
            }

            if (session.WeekOf(week.WeekStart)?.IsApproved() == true)
            {
                return ApplyBuildOutcome.Fail(PlanningSessionErrorCodes.WeekAlreadyApproved, "This week is already approved.", 409);
            }

            weekKey = week.WeekStart;
            var weekIndex = IndexOfWeek(output.PeriodWeeks, week.Monday);
            placedToWrite = output.Placed.Where(p => p.WeekNumber == weekIndex).ToList();
            // A reopened week's visits that kept a report stay and are counted, never re-written.
            kept = output.Fixed
                .Where(v => PlanningWeekCalendar.MondayOf(v.PlannedDate) == week.Monday)
                .Select(v => v.Id)
                .ToList();
        }

        var atoms = new List<PlannedVisitEntity>(placedToWrite.Count);
        var index = session.Weeks.Sum(w => w.PlannedVisitIds.Count); // codes stay unique across week approvals
        // WP-VP-4G (F4-4) — every product name of the run in ONE read (the preview below reuses it); the approved items
        // keep the name as a snapshot.
        var productNames = await ProductNamesAsync(ProductIdsOf(output), cancellationToken);
        foreach (var placed in placedToWrite)
        {
            var atom = await BuildAtomAsync(session, placed, ++index, cancellationToken);
            foreach (var item in atom.ContentItems)
            {
                item.ProductName ??= productNames.GetValueOrDefault(item.ProductId);
            }

            atoms.Add(atom);
        }

        return ApplyBuildOutcome.Ok(await BuildPreviewAsync(output, cancellationToken), atoms) with
        {
            WeekStart = weekKey,
            KeptVisitIds = kept
        };
    }

    /// <summary>Re-plan a subset: re-generate for the affected contacts only and return the UPDATED existing atoms (their
    /// Slot re-packed) — the handler replaces them in place (D-REPLAN = A). Atoms not in the subset are untouched.</summary>
    public async Task<ReplanBuildOutcome> BuildReplanAsync(
        PlanningSession session,
        IReadOnlyCollection<Guid> affectedContactIds,
        VisitPlanGenerationOptions options,
        CancellationToken cancellationToken)
    {
        if (_tenant.TenantId is not { } tenantId)
        {
            return ReplanBuildOutcome.Fail("Tenant context is required.");
        }

        // Narrow the session's selection to just the affected contacts, then generate over that subset.
        var subset = new PlanningSession
        {
            Id = session.Id,
            TenantId = session.TenantId,
            CyclePeriodId = session.CyclePeriodId,
            ResourceId = session.ResourceId,
            ResourceType = session.ResourceType,
            ResourceDisplayName = session.ResourceDisplayName,
            Selection = new PlanningSessionSelection
            {
                SelectedContacts = session.Selection.SelectedContacts
                    .Where(c => affectedContactIds.Contains(c.ContactId)).ToList(),
                SegmentId = session.Selection.SegmentId,
                CampaignId = session.Selection.CampaignId
            },
            Provenance = session.Provenance
        };

        var generation = await GenerateAsync(subset, options, cancellationToken);
        if (generation.Error is { } error || generation.Output is null)
        {
            return ReplanBuildOutcome.Fail(generation.Error ?? "Generation failed.");
        }

        // Map the freshly-generated slots onto the EXISTING committed atoms for those contacts, in place.
        var existing = (await _plannedVisits.ListAsync(tenantId, cancellationToken))
            .Where(p => session.CommittedPlannedVisitIds.Contains(p.Id)
                        && p.ContactId is { } cid && affectedContactIds.Contains(cid)
                        && !p.IsArchived() && !p.IsCancelled())
            .OrderBy(p => p.PlannedDate)
            .ToList();

        var updated = new List<PlannedVisitEntity>();
        var queue = new Queue<PlacedVisit>(generation.Output.Placed.Where(p => p.Candidate.ContactId is not null));
        foreach (var atom in existing)
        {
            if (queue.Count == 0)
            {
                break;
            }

            var slot = queue.Dequeue();
            atom.PlannedDate = slot.Date;
            atom.PlannedStartTime = slot.StartTime;
            atom.PlannedEndTime = slot.EndTime;
            atom.PlannedDurationMinutes = slot.Candidate.DurationMinutes;
            atom.Slot = new PlannedVisitScheduleSlot
            {
                SequenceOrder = slot.SequenceOrder,
                SlotStartTime = slot.StartTime,
                SlotEndTime = slot.EndTime
            };
            atom.UpdatedAt = DateTimeOffset.UtcNow;
            atom.UpdatedBy = _actor.ActorName;
            updated.Add(atom);
        }

        return ReplanBuildOutcome.Ok(await BuildPreviewAsync(generation.Output, cancellationToken), updated);
    }

    // ─────────────────────────────────────────────────────────────────────────────────────────────────────────────
    // The core generation flow (①–⑦). Pure orchestration over the seams — no scoring, no routing, no duration math.
    // ─────────────────────────────────────────────────────────────────────────────────────────────────────────────

    private async Task<GenerationResult> GenerateAsync(
        PlanningSession session, VisitPlanGenerationOptions options, CancellationToken cancellationToken)
    {
        if (_tenant.TenantId is not { } tenantId)
        {
            return GenerationResult.Failed("Tenant context is required.");
        }

        var at = options.EffectiveAt ?? DateTimeOffset.UtcNow;
        var visitPurpose = PlannedVisitPurpose.Normalize(
            string.IsNullOrWhiteSpace(options.VisitPurpose) ? PlannedVisitPurpose.MedicalVisit : options.VisitPurpose);
        var visitType = PlannedVisitType.Normalize(
            string.IsNullOrWhiteSpace(options.VisitType) ? PlannedVisitType.FieldVisit : options.VisitType);

        // ① PERIOD + WEEKS — MOD-0165 CyclePeriod; weeks DERIVED from its calendar (no new period/week entity).
        var period = await _periods.GetByIdAsync(session.CyclePeriodId, cancellationToken);
        if (period is null)
        {
            return GenerationResult.Failed("The cycle period could not be resolved for this tenant.");
        }

        var periodStart = DateOnly.FromDateTime(period.StartDate.UtcDateTime);
        var periodEnd = DateOnly.FromDateTime(period.EndDate.UtcDateTime);
        if (periodEnd < periodStart)
        {
            return GenerationResult.Failed("The cycle period window is invalid (end before start).");
        }

        // WP-VP-3A (B2, K-2, S-1) — the horizon is the WHOLE period: every Monday-week touching it (MaxWeeks is gone).
        // A week already over is never generated and the current week starts today (max(period start, today's week));
        // an approved week is frozen — its written visits are only COUNTED (fixed), never re-generated. TargetWeekStart
        // no longer restricts generation: it is only the week the screen opens on.
        var today = PlanningWeekCalendar.Today(at);
        var periodWeeks = PlanningWeekCalendar.PeriodWeeks(periodStart, periodEnd);
        var draftWeekIndexes = new List<int>();
        for (var i = 0; i < periodWeeks.Count; i++)
        {
            if (!PlanningWeekCalendar.IsPast(periodWeeks[i], today)
                && periodWeeks[i].To >= today
                && session.WeekOf(periodWeeks[i].WeekStart)?.IsApproved() != true)
            {
                draftWeekIndexes.Add(i);
            }
        }

        var horizonStart = periodStart > today ? periodStart : today;

        // ② context: capacity (supply + between-visit buffer) + territory WARN (never a filter).
        var capacity = await _capacities.GetByCyclePeriodAsync(tenantId, session.CyclePeriodId, cancellationToken);
        var betweenVisit = capacity?.BetweenVisitTimeMinutes ?? 0;
        // WP-VP-3B (MK-8) — the day budget (capacity's working minutes − its daily fixed charge; default hours without a
        // capacity) and the configured route day it is laid on.
        var routeDay = _routeDefaults?.Current.WorkingDay ?? RouteOptimizationDefaults.WorkingDay;
        var dayBudget = PlanningDayBudget.From(capacity, routeDay);

        // WP-VP-FIX-1 (C2 + C3) — only WORKING days are candidate days: the platform working calendar (weekends,
        // holidays, closures) for the period's country, asked once per day for the whole run; when it cannot answer the
        // Sat/Sun fallback runs and the result says so (calendarStatus = unresolved).
        // WP-VP-3A — asked for the whole period: the frequency rule counts its working weeks / days.
        var calendar = await _calendar.ResolveAsync(
            period, capacity?.CalendarCountryCode, periodStart, periodEnd, cancellationToken);
        var nonWorking = calendar.NonWorkingDates.ToHashSet();
        var frame = new PlanningPeriodFrame(periodStart, periodEnd, nonWorking);

        // A draft week needs at least one working day left in its window (from today on); a week with none (e.g. today is
        // Saturday) can take no visit, so nothing is spread onto it.
        draftWeekIndexes.RemoveAll(i =>
        {
            var from = periodWeeks[i].From > today ? periodWeeks[i].From : today;
            for (var d = from; d <= periodWeeks[i].To; d = d.AddDays(1))
            {
                if (!nonWorking.Contains(d))
                {
                    return false;
                }
            }

            return true;
        });
        var territoryWarnings = await _territory.WarnAsync(session.Selection.SelectedAccountIds, cancellationToken);

        // ③ CONTACT (doctor) selection — segment filter + consent gate + availability windows.
        // WP-VP-2 (B-3, K-4) — no segment filter any more: every picked doctor is assessed (consent + availability).
        var assessments = await _contacts.AssessAsync(
            session.Selection.SelectedContacts, visitPurpose, at, cancellationToken);

        // ④ CONTENT + DURATION per doctor (FU04) + build the candidate visit set.
        // WP-SB-3b — the stage comes from the doctor's JourneyProgress per product (read by the resolver), projected over
        // the doctor's plans that are not cancelled / archived (nothing is "completed" before SB-3c) and fall before the
        // planning window: each of them that tells a product moves that product one stage (and one exposure) on.
        var allPlans = await _plannedVisits.ListAsync(tenantId, cancellationToken);
        var pendingPlans = allPlans
            .Where(p => p.ContactId is not null && p.ContentItems.Count > 0 && !p.IsCancelled() && !p.IsArchived())
            .ToList();

        // WP-VP-3A — FIXED visits: what this plan's stored weeks already wrote (approved weeks, and the visits a reopened
        // week kept), cancelled / archived excluded. DONE visits: the rep's visits in the period with a COMPLETED report.
        var storedIds = session.Weeks.SelectMany(w => w.PlannedVisitIds).ToHashSet();
        // WP-VP-4A (F3-2) — an OLD whole-period (committed) plan: its written visits are fixed too and NOTHING is generated
        // for it (its other weeks stay empty); the preview shows what was written (LegacyCommittedPlan).
        var legacy = LegacyCommittedPlan.IsLegacy(session);
        var legacyIds = LegacyCommittedPlan.FixedVisits(session, allPlans).Select(p => p.Id).ToHashSet();
        if (legacy)
        {
            draftWeekIndexes.Clear();
        }

        var fixedVisits = allPlans
            .Where(p => (storedIds.Contains(p.Id) || legacyIds.Contains(p.Id)) && !p.IsCancelled() && !p.IsArchived())
            .OrderBy(p => p.PlannedDate).ThenBy(p => p.Slot.SequenceOrder ?? 0)
            .ToList();
        var doneVisits = await CompletedInPeriodAsync(tenantId, session, allPlans, periodStart, periodEnd, cancellationToken);
        var accountCache = new Dictionary<Guid, AccountEntity?>();
        var candidates = new List<Candidate>();
        var contentPreviews = new List<DoctorContentPreview>();
        var consentBlocked = new List<EligibleContactAssessment>();

        foreach (var doctor in assessments)
        {
            var pending = PendingBefore(pendingPlans, doctor.ContactId, horizonStart);
            // WP-VP-3C (K-7) — the rep's pick for this doctor and which visit of the period the first planned one is.
            var picks = PicksOf(session, doctor.ContactId, doctor.AccountId);
            var baseOrdinal = PriorVisitsInPeriod(allPlans, session, doctor.ContactId, periodStart, horizonStart) + 1;
            var content = await ResolveContentAsync(
                session, doctor.ContactId, pending, at, cancellationToken, picks, baseOrdinal);
            // WP-VP-2 (B-3) — the play was derived from the doctor's own segments; a non-unique choice is said.
            var play = await _deriver.DerivePlayAsync(doctor.ContactId, at, cancellationToken);
            var contentReasons = string.Equals(play.ReasonCode, VisitProvenanceDeriver.MultiplePlays, StringComparison.Ordinal)
                ? content.ReasonCodes.Append(VisitProvenanceDeriver.MultiplePlays).Distinct(StringComparer.Ordinal).ToList()
                : content.ReasonCodes;
            // WP-VP-3B (4) — unknown consent is planned, with a warning on the doctor's preview.
            if (string.Equals(doctor.ConsentStatus, ConsentEligibilityStatus.Unknown, StringComparison.Ordinal))
            {
                contentReasons = contentReasons.Append(PlanningVisitReasons.ConsentUnknown).Distinct(StringComparer.Ordinal).ToList();
            }

            var duration = content.VisitDurationMinutes > 0
                ? content.VisitDurationMinutes
                : DefaultDuration(capacity);

            contentPreviews.Add(new DoctorContentPreview(
                doctor.ContactId, doctor.AccountId, content.Status, content.JourneyId, content.StageId,
                content.StageIndex, content.StageDisplayName, content.PromoItemCount, content.NonPromoItemCount,
                duration, contentReasons, doctor.ConsentStatus, doctor.ConsentBlocked, doctor.ConsentReason,
                content.Items ?? Array.Empty<VisitContentItem>(),
                Products: (content.Items ?? Array.Empty<VisitContentItem>())
                    .Select(i => new VisitProductPreview(i.ProductId, i.ProductCode, i.Role, i.Source))
                    .ToList(),
                DurationMinutes: duration));

            // WP-VP-3B (4) — a BLOCKED doctor is not planned (the campaign "blocked ⇒ excluded" rule, same MOD-0164
            // verdict): it is reported unscheduled (consent_blocked) and its institution is not visited in its place.
            if (doctor.ConsentBlocked)
            {
                consentBlocked.Add(doctor);
                continue;
            }

            var (lat, lng) = await ResolveCoordinatesAsync(tenantId, doctor.AccountId, accountCache, cancellationToken);

            candidates.Add(new Candidate(
                TargetType: PlannedVisitTargetType.Contact,
                TargetId: doctor.ContactId,
                AccountId: doctor.AccountId,
                ContactId: doctor.ContactId,
                AccountContactLinkId: doctor.AccountContactLinkId,
                Lat: lat,
                Long: lng,
                DurationMinutes: duration,
                JourneyId: content.JourneyId,
                StageId: content.StageId,
                StageIndex: content.StageIndex,
                PromoItemCount: content.PromoItemCount,
                NonPromoItemCount: content.NonPromoItemCount,
                ContentStatus: content.Status,
                ConsentBlocked: doctor.ConsentBlocked,
                Windows: doctor.AvailabilityWindows,
                Content: content,
                ContentPending: pending,
                RepPicks: picks,
                BaseOrdinal: baseOrdinal));
        }

        // Pharmacy targets (first-class; report-only duration) + bare account targets (no doctor selected under them).
        var accountsWithDoctor = candidates
            .Where(c => c.AccountId is not null)
            .Select(c => c.AccountId!.Value)
            .Concat(consentBlocked.Where(d => d.AccountId is not null).Select(d => d.AccountId!.Value))
            .ToHashSet();

        foreach (var pharmacyId in session.Selection.SelectedPharmacyIds.Distinct())
        {
            var (lat, lng) = await ResolveCoordinatesAsync(tenantId, pharmacyId, accountCache, cancellationToken);
            candidates.Add(NonDoctorCandidate(
                PlannedVisitTargetType.Pharmacy, pharmacyId, pharmacyId, lat, lng, DefaultDuration(capacity)));
        }

        foreach (var accountId in session.Selection.SelectedAccountIds.Distinct())
        {
            if (accountsWithDoctor.Contains(accountId))
            {
                continue; // the doctor visit already covers this account
            }

            var (lat, lng) = await ResolveCoordinatesAsync(tenantId, accountId, accountCache, cancellationToken);
            candidates.Add(NonDoctorCandidate(
                PlannedVisitTargetType.Account, accountId, accountId, lat, lng, DefaultDuration(capacity)));
        }

        // ⑦ FREQUENCY (WP-VP-3A) — the visits the WHOLE period needs (policy count × PeriodType units; unknown = 1), minus
        // the target's visits already counted (done with a completed report, or fixed in an approved week — cancelled
        // never), spread evenly over the draft weeks. The route is RE-RUN per week (⑤) below.
        var perCandidateWeeks = new Dictionary<Guid, List<int>>();
        var frequencyByTarget = new Dictionary<Guid, FrequencyRequirement>();
        foreach (var candidate in candidates)
        {
            // WP-VP-2 (B-3) — frequency: DET-P derives the doctor's segments itself; no session segment / campaign.
            var requirement = await _frequencyExtend.ResolveRequirementAsync(
                candidate.TargetType, candidate.TargetId, at, frame, cancellationToken);
            frequencyByTarget[candidate.TargetId] = requirement;
            var counted = fixedVisits.Concat(doneVisits)
                .Where(v => TargetKeyOf(v) == candidate.TargetId)
                .Select(v => v.Id)
                .Distinct()
                .Count();
            var remaining = Math.Max(0, requirement.RequiredInPeriod - counted);
            // A draft week that already holds a fixed visit of this target (a reopened week's reported visit) is skipped
            // while there are enough other weeks — so re-approving it only adds what is missing.
            var occupied = fixedVisits
                .Where(v => TargetKeyOf(v) == candidate.TargetId)
                .Select(v => PlanningWeekCalendar.MondayOf(v.PlannedDate))
                .ToHashSet();
            var targetWeeks = draftWeekIndexes.Where(i => !occupied.Contains(periodWeeks[i].Monday)).ToList();
            if (remaining > targetWeeks.Count)
            {
                targetWeeks = draftWeekIndexes;
            }

            // WP-VP-4L (1) — the weekly default (unknown cadence) is ONE visit a week: never more than one per draft week
            // (the period's past weeks are not caught up twice in a later week).
            if (requirement.IsWeeklyDefault)
            {
                remaining = Math.Min(remaining, targetWeeks.Count);
            }

            perCandidateWeeks[candidate.TargetId] = FrequencyExtendPlanner
                .Distribute(remaining, targetWeeks.Count)
                .Select(k => targetWeeks[k])
                .ToList();
        }

        // ⑤ WP-VP-3B — DAY BALANCING + ROUTE + OVERFLOW, per draft week in order. The week's visits (its frequency share
        // plus what an earlier week could not hold) first get a DAY from DayBalancer (institution groups → the emptiest
        // working day within the day budget); then the route optimizer orders and times ONE day at a time (the manual order
        // decides the order inside a day, never the day). What no day of the week can hold moves to the next DRAFT week
        // that does not already visit that target (shifted, with the reason); past the last draft week it is
        // period_exhausted. Approved weeks are not draft weeks, so nothing ever moves into them.
        var placed = new List<PlacedVisit>();
        var unscheduled = new List<UnscheduledPreview>();
        var shifts = new List<ShiftTrack>();
        var carried = draftWeekIndexes.ToDictionary(i => i, _ => new List<WeekItem>());
        var groupOf = await GroupKeysAsync(tenantId, candidates, cancellationToken);
        var fixedLoad = fixedVisits
            .GroupBy(v => v.PlannedDate)
            .ToDictionary(g => g.Key, g => g.Sum(v => Math.Max(0, v.PlannedDurationMinutes ?? 0) + betweenVisit));
        var startLocation = ResolveStartLocation(options);
        // WP-VP-4E — the route's own travel model decides what is "near" when days are clustered.
        var travel = _routeDefaults?.Current is { } rd
            ? new HaversineTravelModel(rd.RoadFactor, rd.AssumedSpeedKmPerMin)
            : new HaversineTravelModel(RouteOptimizationDefaults.RoadFactor, RouteOptimizationDefaults.AssumedSpeedKmPerMin);
        var dayPreviews = new List<PlanningDayPreview>();
        var pinMoves = new List<PinMove>();
        var pinWarnings = new List<PinWarningPreview>();
        var trackDates = new Dictionary<ShiftTrack, DateOnly>(ReferenceEqualityComparer.Instance);

        foreach (var weekIndex in draftWeekIndexes)
        {
            var span = periodWeeks[weekIndex];
            var from = span.From > today ? span.From : today;
            // A target appears once per visit it owes this week (twice only when it owes more visits than draft weeks).
            var items = carried[weekIndex]
                .Concat(candidates.SelectMany(c => Enumerable
                    .Repeat(c, perCandidateWeeks.TryGetValue(c.TargetId, out var w) ? w.Count(x => x == weekIndex) : 0)
                    .Select(x => new WeekItem(x, weekIndex, null))))
                .ToList();
            // WP-VP-4L (2) — the rep's EXTRA visits of this week join it (same day rules, the day pins apply). A target that
            // already has a visit this week (frequency, a carried one, or a kept fixed one) takes no extra — said on the
            // preview, never a silent second visit; a target no longer planned (left the selection, consent blocked) is
            // not a candidate and is skipped.
            foreach (var extra in session.WeekExtras.Where(e => string.Equals(e.WeekStart, span.WeekStart, StringComparison.Ordinal)))
            {
                var candidate = candidates.FirstOrDefault(c =>
                    string.Equals(c.TargetType, extra.TargetType, StringComparison.Ordinal) && c.TargetId == extra.TargetId);
                if (candidate is null)
                {
                    continue;
                }

                var already = items.Any(i => i.Candidate.TargetId == candidate.TargetId)
                              || fixedVisits.Any(v => TargetKeyOf(v) == candidate.TargetId
                                                      && PlanningWeekCalendar.MondayOf(v.PlannedDate) == span.Monday);
                if (already)
                {
                    pinWarnings.Add(new PinWarningPreview(
                        span.WeekStart, candidate.TargetType, candidate.TargetId, string.Empty, PlanningWeekExtras.ExtraAlreadyPlanned));
                    continue;
                }

                items.Add(new WeekItem(candidate, weekIndex, null, IsExtra: true));
            }

            if (items.Count == 0)
            {
                continue;
            }

            // WP-VP-4E — the rep's day pins of this week (an approved week is never a draft week, so its pins rest).
            var weekPins = session.DayPins
                .Where(p => string.Equals(p.WeekStart, span.WeekStart, StringComparison.Ordinal))
                .ToList();
            var week = PlanWeek(
                items, from, span.To, calendar, dayBudget, routeDay, betweenVisit, fixedLoad, groupOf, startLocation,
                options.ManualVisitOrder, weekPins, travel, lastDraftWeek: weekIndex == draftWeekIndexes[^1]);

            placed.AddRange(week.Placed.Select(p => new PlacedVisit(
                p.Item.Candidate, weekIndex, p.Date, p.Start, p.End, p.Sequence,
                IsPinned: week.Pinned.TryGetValue(p.Item, out var auto),
                AutoPinned: week.Pinned.ContainsKey(p.Item) && auto,
                GroupKey: GroupKeyOf(groupOf, p.Item.Candidate),
                IsExtra: p.Item.IsExtra)));
            foreach (var p in week.Placed)
            {
                if (p.Item.Shift is { } placedTrack)
                {
                    trackDates[placedTrack] = p.Date;
                }
            }

            dayPreviews.AddRange(week.Days.Select(d => d with { WeekStart = span.WeekStart }));
            pinWarnings.AddRange(week.PinWarnings.Select(w => w with { WeekStart = span.WeekStart }));
            pinMoves.AddRange(week.PinMoves);

            foreach (var (item, reason) in week.Unscheduled)
            {
                Unshift(shifts, item);
                unscheduled.Add(new UnscheduledPreview(
                    item.FromWeek, item.Candidate.TargetType, item.Candidate.TargetId, item.Candidate.ContactId, reason));
            }

            var reasonForWeek = ShiftReason(from, span.To, calendar);
            foreach (var item in week.Overflow)
            {
                // WP-VP-4L (2) — an extra visit belongs to ITS week: it never moves to another one.
                if (item.IsExtra)
                {
                    unscheduled.Add(new UnscheduledPreview(
                        item.FromWeek, item.Candidate.TargetType, item.Candidate.TargetId, item.Candidate.ContactId,
                        PlanningWeekExtras.ExtraNoRoom));
                    continue;
                }

                // WP-VP-4L (1) — a weekly default visit that did not fit its week is SKIPPED, not shifted: every later week
                // already has its own visit, so a shift would only start a chain (CT advice). A pinned visit that left its
                // week keeps the 4E rule.
                if (item.Shift is null && !week.PinOverflow.Contains(item)
                    && frequencyByTarget.TryGetValue(item.Candidate.TargetId, out var itemFrequency) && itemFrequency.IsWeeklyDefault)
                {
                    unscheduled.Add(new UnscheduledPreview(
                        item.FromWeek, item.Candidate.TargetType, item.Candidate.TargetId, item.Candidate.ContactId,
                        FrequencyDefaults.WeekFullSkipped));
                    continue;
                }

                var next = draftWeekIndexes
                    .Where(i => i > weekIndex
                                && !(perCandidateWeeks.TryGetValue(item.Candidate.TargetId, out var owned) && owned.Contains(i))
                                && carried[i].All(c => c.Candidate.TargetId != item.Candidate.TargetId))
                    .DefaultIfEmpty(-1)
                    .First();
                if (next < 0)
                {
                    Unshift(shifts, item);
                    unscheduled.Add(new UnscheduledPreview(
                        item.FromWeek, item.Candidate.TargetType, item.Candidate.TargetId, item.Candidate.ContactId,
                        RouteUnscheduledReasonCodes.PeriodExhausted));
                    continue;
                }

                var track = item.Shift;
                if (track is null)
                {
                    // WP-VP-4E — a pinned visit that found no room in its week moves on with the pin_overflow reason.
                    // WP-VP-4G (F4-1) — room was left but no day near (nor light): no_near_day, not "the week is full".
                    track = new ShiftTrack(item.Candidate, item.FromWeek,
                        week.PinOverflow.Contains(item) ? PlanningDayPins.PinOverflow
                        : week.NoNearDay.Contains(item) ? PlanningShiftReasons.NoNearDay
                        : reasonForWeek);
                    shifts.Add(track);
                }

                track.ToWeek = next;
                foreach (var move in pinMoves.Where(m => ReferenceEquals(m.Item, item)))
                {
                    move.Track = track;
                }

                carried[next].Add(item with { Shift = track });
            }
        }

        // WP-VP-3B (4) — a consent-blocked doctor is reported, never planned.
        var firstDraftWeek = draftWeekIndexes.Count > 0 ? draftWeekIndexes[0] : 0;
        if (!legacy) // WP-VP-4A — an old committed plan plans nothing, so nothing is "not planned" either
        {
            unscheduled.AddRange(consentBlocked.Select(d => new UnscheduledPreview(
                firstDraftWeek, PlannedVisitTargetType.Contact, d.ContactId, d.ContactId, PlanningVisitReasons.ConsentBlocked)));
        }

        // WP-SB-3b — a doctor recurs across weeks (frequency-extend), so each placed visit tells what comes AFTER the
        // doctor's earlier visits: the stored pending plans before its date plus this run's earlier visits.
        placed = await ProjectContentAsync(
            session, placed, pendingPlans, at, cancellationToken,
            (contactId, before) => PriorVisitsInPeriod(allPlans, session, contactId, periodStart, before));

        // WP-VP-3B (B-7, C5) — every week's capacity and the period, in minutes (supply and demand in one unit).
        var (weekCapacity, periodCapacity) = BuildCapacity(
            periodWeeks, calendar, dayBudget, betweenVisit, placed, fixedVisits);
        var shifted = shifts
            .Select(s => new ShiftedVisitPreview(
                s.Candidate.TargetType, s.Candidate.TargetId, s.Candidate.ContactId,
                s.Candidate.ContactId is null && accountCache.TryGetValue(s.Candidate.TargetId, out var acc) ? acc?.AccountName : null,
                s.FromWeek, s.ToWeek, s.Reason))
            .ToList();

        // ⑥ SUPPLY-vs-DEMAND — TRANSIENT summary (warning, never a block). WP-VP-3A — fixed visits are demand too.
        // WP-VP-3B — a consent-blocked doctor is not demand (it was never to be planned).
        var notPlaced = unscheduled.Count(u => u.Reason != PlanningVisitReasons.ConsentBlocked);
        var supplyDemand = await BuildSupplyDemandAsync(
            capacity, period, placed.Count + fixedVisits.Count, notPlaced, cancellationToken);

        // WP-VP-3A — the doctor's cadence travels on the candidate preview too (3D shows it).
        var contentWithFrequency = contentPreviews
            .Select(c => frequencyByTarget.TryGetValue(c.ContactId, out var f)
                ? c with { FrequencyStatus = f.FrequencyStatus, RequiredVisitCount = f.RequiredInPeriod, FrequencyDefault = f.FrequencyDefault }
                : c)
            .ToList();

        // WP-VP-4E — where each moved pinned visit landed (a later week: the day the carried visit was placed on).
        var pinOverflow = pinMoves
            .Select(m => new PinOverflowPreview(
                m.Item.Candidate.TargetType, m.Item.Candidate.TargetId, m.Item.Candidate.ContactId,
                m.Item.Candidate.ContactId is null && accountCache.TryGetValue(m.Item.Candidate.TargetId, out var pa) ? pa?.AccountName : null,
                m.From.ToString("yyyy-MM-dd"),
                (m.To ?? (m.Track is { } t && trackDates.TryGetValue(t, out var landed) ? landed : (DateOnly?)null))?.ToString("yyyy-MM-dd"),
                m.Reason))
            .ToList();

        return GenerationResult.Succeeded(new GenerationOutput(
            session, period, periodStart, periodEnd, periodWeeks.Count, placed, unscheduled, contentWithFrequency,
            territoryWarnings, supplyDemand, calendar, periodWeeks, today, fixedVisits, frequencyByTarget,
            shifted, weekCapacity, periodCapacity, dayPreviews, pinOverflow, pinWarnings, VisitModelDto.From(capacity)));
    }

    // ── WP-VP-3B — one week: day balancing, per-day route, overflow ──────────────────────────────────────────────────

    /// <summary>
    /// Plans one draft week: a missing location is reported at once (it cannot be routed); WP-VP-4E — the rep's PINNED
    /// visits go first onto their days (in route order, as many as the day's budget and window hold; the rest move to the
    /// week's next working day with room — auto-pinned — or, with none, to a later week as pin_overflow); every other visit
    /// gets a day from the geography-aware <see cref="DayBalancer"/> around them. Each day is then routed alone (its window
    /// = the day kind's working minutes from 09:00; a pinned day is built pinned-first, adding the others one by one so a
    /// pinned visit is never pushed out). A visit the route cannot fit on its day (the day ran out, or the doctor's
    /// availability excludes that weekday) is tried on the week's other days, emptiest first, by re-routing that day with
    /// it; a visit no day takes is OVERFLOW when the day ran out, unscheduled (availability) otherwise. The route's other
    /// reasons stand as they are. No visit is ever planned past the end of the working window.
    /// </summary>
    private WeekPlan PlanWeek(
        List<WeekItem> items,
        DateOnly from,
        DateOnly to,
        PlanningCalendarResult calendar,
        PlanningDayBudget budget,
        WorkingDayHours routeDay,
        int buffer,
        IReadOnlyDictionary<DateOnly, int> fixedLoad,
        IReadOnlyDictionary<Guid, string> groupOf,
        GeoPoint? startLocation,
        IReadOnlyList<Guid>? manualOrder,
        IReadOnlyList<PlanningDayPin> pins,
        ITravelModel travel,
        bool lastDraftWeek)
    {
        var unscheduled = new List<(WeekItem, string)>();
        var overflow = new List<WeekItem>();
        var weekDays = new List<(DateOnly Date, int Budget)>();
        for (var d = from; d <= to; d = d.AddDays(1))
        {
            weekDays.Add((d, budget.BudgetFor(calendar.KindOf(d))));
        }

        int Cost(WeekItem item) => Math.Max(1, item.Candidate.DurationMinutes) + buffer;
        int BudgetOf(DateOnly d) => weekDays.FirstOrDefault(w => w.Date == d).Budget;

        var byId = new Dictionary<int, WeekItem>();
        for (var i = 0; i < items.Count; i++)
        {
            if (!RouteTime.IsValidCoordinate(items[i].Candidate.Lat, items[i].Candidate.Long))
            {
                unscheduled.Add((items[i], RouteUnscheduledReasonCodes.MissingLocation));
                continue;
            }

            byId[i] = items[i];
        }

        // ── WP-VP-4E — pins: institution pins first, then visit pins (a visit pin wins for its visit) ──
        var pinned = new Dictionary<WeekItem, bool>(ReferenceEqualityComparer.Instance); // value = auto-pinned
        var pinDay = new Dictionary<WeekItem, DateOnly>(ReferenceEqualityComparer.Instance);
        var pinWarnings = new List<PinWarningPreview>();
        var pinMoves = new List<PinMove>();
        var pinOverflow = new HashSet<WeekItem>(ReferenceEqualityComparer.Instance);
        foreach (var pin in pins.OrderBy(p => p.Scope == PlanningDayPinScopes.Institution ? 0 : 1))
        {
            if (!DateOnly.TryParseExact(pin.Date, "yyyy-MM-dd", CultureInfo.InvariantCulture, DateTimeStyles.None, out var date)
                || date < from || date > to || BudgetOf(date) <= 0)
            {
                pinWarnings.Add(new PinWarningPreview(string.Empty, pin.TargetType, pin.TargetId, pin.Date, PlanningDayPins.PinNotWorkingDay));
                continue;
            }

            var matched = MatchPin(pin, byId.Values, groupOf);
            if (matched.Count == 0)
            {
                pinWarnings.Add(new PinWarningPreview(string.Empty, pin.TargetType, pin.TargetId, pin.Date, PlanningDayPins.PinTargetNotInWeek));
                continue;
            }

            foreach (var m in matched)
            {
                pinned[m] = false;
                pinDay[m] = date;
            }
        }

        // Place the pinned visits day by day: route order, within the budget and the window; the rest move forward.
        var dayPinned = new Dictionary<DateOnly, List<WeekItem>>();
        var queue = new SortedDictionary<DateOnly, List<WeekItem>>();
        foreach (var (item, date) in pinDay)
        {
            (queue.TryGetValue(date, out var q) ? q : queue[date] = new List<WeekItem>()).Add(item);
        }

        int PinnedLoad(DateOnly d) => fixedLoad.GetValueOrDefault(d)
                                      + (dayPinned.TryGetValue(d, out var kept) ? kept.Sum(Cost) : 0)
                                      + (queue.TryGetValue(d, out var waiting) ? waiting.Sum(Cost) : 0);
        while (queue.Count > 0)
        {
            var date = queue.Keys.First();
            var list = queue[date];
            queue.Remove(date);
            var (ok, missed) = RouteDay(date, calendar.KindOf(date), list, budget, routeDay, buffer, startLocation, manualOrder);
            var room = BudgetOf(date) - fixedLoad.GetValueOrDefault(date);
            var kept = new List<WeekItem>();
            var moved = new List<(WeekItem Item, string Reason)>();
            foreach (var p in ok.OrderBy(p => p.Sequence))
            {
                if (Cost(p.Item) <= room)
                {
                    kept.Add(p.Item);
                    room -= Cost(p.Item);
                }
                else
                {
                    moved.Add((p.Item, PlanningDayPins.PinDayFull));
                }
            }

            moved.AddRange(missed.Select(m => (m.Item, m.Reason == RouteUnscheduledReasonCodes.NoFeasibleAvailabilityWindow
                ? PlanningDayPins.PinOutsideAvailability : PlanningDayPins.PinDayFull)));
            dayPinned[date] = kept;
            foreach (var (item, reason) in moved)
            {
                var next = weekDays
                    .Where(w => w.Date > date && w.Budget > 0 && PinnedLoad(w.Date) + Cost(item) <= w.Budget)
                    .Select(w => (DateOnly?)w.Date)
                    .FirstOrDefault();
                if (next is { } nd)
                {
                    (queue.TryGetValue(nd, out var q) ? q : queue[nd] = new List<WeekItem>()).Add(item);
                    pinned[item] = true;
                    pinMoves.Add(new PinMove(item, date, nd, reason));
                }
                else
                {
                    pinned.Remove(item);
                    pinOverflow.Add(item);
                    overflow.Add(item);
                    pinMoves.Add(new PinMove(item, date, null, PlanningDayPins.PinOverflow));
                }
            }
        }

        // ── the others: geography-aware days around the pinned ones ──
        var days = weekDays.Select(w =>
        {
            var kept = dayPinned.TryGetValue(w.Date, out var k) ? k : new List<WeekItem>();
            var weight = kept.Sum(Cost);
            return new DayBalancer.Day(
                w.Date, w.Budget, fixedLoad.GetValueOrDefault(w.Date) + weight,
                weight > 0 ? kept.Sum(i => i.Candidate.Lat * Cost(i)) / weight : null,
                weight > 0 ? kept.Sum(i => i.Candidate.Long * Cost(i)) / weight : null,
                weight);
        }).ToList();
        var free = byId.Where(kv => !pinned.ContainsKey(kv.Value) && !pinOverflow.Contains(kv.Value)).ToList();
        DayBalancer.Visit ToVisit(KeyValuePair<int, WeekItem> kv) => new(
            kv.Key,
            GroupKeyOf(groupOf, kv.Value.Candidate),
            Cost(kv.Value),
            kv.Value.Candidate.Lat,
            kv.Value.Candidate.Long,
            Follower: kv.Value.Candidate.TargetType == PlannedVisitTargetType.Pharmacy
                      && GroupKeyOf(groupOf, kv.Value.Candidate) != kv.Value.Candidate.TargetId.ToString("N"));

        // WP-VP-4G (F4-5) — with pins, the STABLE placement: the base layout (no pins) keeps every free visit on its day;
        // only a day over its budget gives up its free visits farthest from its centre. Without pins: the 4E rule as is.
        DayBalancer.Result balance;
        if (pinned.Count == 0)
        {
            balance = DayBalancer.Assign(days, free.Select(ToVisit).ToList(), travel, farFill: lastDraftWeek);
        }
        else
        {
            var idOf = byId.ToDictionary(kv => kv.Value, kv => kv.Key, (IEqualityComparer<WeekItem>)ReferenceEqualityComparer.Instance);
            var keptPins = dayPinned
                .SelectMany(kv => kv.Value.Select(i => (Id: idOf[i], Date: kv.Key)))
                .ToDictionary(x => x.Id, x => x.Date);
            balance = DayBalancer.AssignAroundPins(
                weekDays.Select(w => new DayBalancer.Day(w.Date, w.Budget, fixedLoad.GetValueOrDefault(w.Date))).ToList(),
                byId.Select(ToVisit).ToList(),
                keptPins,
                byId.Where(kv => pinOverflow.Contains(kv.Value)).Select(kv => kv.Key).ToHashSet(),
                travel,
                farFill: lastDraftWeek);
        }

        overflow.AddRange(balance.Overflow.Select(id => byId[id]));
        var noNearDay = new HashSet<WeekItem>(
            (balance.NoNearDay ?? new HashSet<int>()).Select(id => byId[id]), ReferenceEqualityComparer.Instance);

        var load = new Dictionary<DateOnly, int>(balance.LoadMinutes);
        var members = balance.Assigned
            .GroupBy(kv => kv.Value)
            .ToDictionary(g => g.Key, g => g.OrderBy(kv => kv.Key).Select(kv => byId[kv.Key]).ToList());
        var routed = new Dictionary<DateOnly, IReadOnlyList<DayPlaced>>();
        var retry = new List<(WeekItem Item, string Reason, DateOnly Tried)>();

        foreach (var date in members.Keys.Concat(dayPinned.Where(kv => kv.Value.Count > 0).Select(kv => kv.Key))
                     .Distinct().OrderBy(d => d).ToList())
        {
            var others = members.TryGetValue(date, out var m) ? m : new List<WeekItem>();
            if (dayPinned.TryGetValue(date, out var keptPins) && keptPins.Count > 0)
            {
                // Pinned first (they fit alone), then each other visit only if the day still routes without a miss.
                var current = new List<WeekItem>(keptPins);
                var (ok, _) = RouteDay(date, calendar.KindOf(date), current, budget, routeDay, buffer, startLocation, manualOrder);
                foreach (var item in others)
                {
                    var trial = current.Append(item).ToList();
                    var (trialOk, trialMissed) = RouteDay(date, calendar.KindOf(date), trial, budget, routeDay, buffer, startLocation, manualOrder);
                    if (trialMissed.Count == 0)
                    {
                        current = trial;
                        ok = trialOk;
                    }
                    else
                    {
                        load[date] -= Cost(item);
                        retry.Add((item, trialMissed.FirstOrDefault(x => ReferenceEquals(x.Item, item)).Reason
                                         ?? RouteUnscheduledReasonCodes.PeriodExhausted, date));
                    }
                }

                members[date] = current;
                routed[date] = ok;
                continue;
            }

            var (dayOk, dayMissed) = RouteDay(date, calendar.KindOf(date), others, budget, routeDay, buffer, startLocation, manualOrder);
            routed[date] = dayOk;
            foreach (var (item, reason) in dayMissed)
            {
                others.Remove(item);
                load[date] -= Cost(item);
                retry.Add((item, reason, date));
            }
        }

        foreach (var (item, reason, tried) in retry)
        {
            var dayFull = reason == RouteUnscheduledReasonCodes.PeriodExhausted;
            if (!dayFull && reason != RouteUnscheduledReasonCodes.NoFeasibleAvailabilityWindow)
            {
                unscheduled.Add((item, reason));
                continue;
            }

            var placedElsewhere = false;
            foreach (var day in days
                         .Where(d => d.BudgetMinutes > 0 && d.Date != tried && load[d.Date] + Cost(item) <= d.BudgetMinutes)
                         .OrderBy(d => (double)load[d.Date] / d.BudgetMinutes)
                         .ThenBy(d => d.Date))
            {
                var trial = (members.TryGetValue(day.Date, out var list) ? list : new List<WeekItem>()).Append(item).ToList();
                var (ok, missed) = RouteDay(day.Date, calendar.KindOf(day.Date), trial, budget, routeDay, buffer, startLocation, manualOrder);
                if (missed.Count == 0)
                {
                    members[day.Date] = trial;
                    routed[day.Date] = ok;
                    load[day.Date] += Cost(item);
                    placedElsewhere = true;
                    break;
                }
            }

            if (placedElsewhere)
            {
                continue;
            }

            if (dayFull)
            {
                overflow.Add(item);
                // WP-VP-4I (2) — the route could not hold it (the travel), yet a day of the week still has minutes for its
                // visit: no_near_day. capacity_full stays for a week with no room left for the visit itself.
                if (days.Any(d => d.BudgetMinutes > 0 && load[d.Date] + Cost(item) <= d.BudgetMinutes))
                {
                    noNearDay.Add(item);
                }
            }
            else
            {
                unscheduled.Add((item, reason));
            }
        }

        var placed = routed.Values.SelectMany(v => v).ToList();
        // WP-VP-4E — every working day: its budget, what it plans (fixed + placed, with buffers) and the idle rest.
        var dayPreviews = weekDays
            .Where(w => w.Budget > 0)
            .Select(w =>
            {
                var planned = fixedLoad.GetValueOrDefault(w.Date) + placed.Where(p => p.Date == w.Date).Sum(p => Cost(p.Item));
                return new PlanningDayPreview(
                    w.Date.ToString("yyyy-MM-dd"), string.Empty, calendar.KindOf(w.Date), w.Budget, planned,
                    Math.Max(0, w.Budget - planned), planned > w.Budget);
            })
            .ToList();
        var stillPinned = new Dictionary<WeekItem, bool>(ReferenceEqualityComparer.Instance);
        foreach (var p in placed.Where(p => pinned.ContainsKey(p.Item)))
        {
            stillPinned[p.Item] = pinned[p.Item];
        }

        return new WeekPlan(placed, unscheduled, overflow, stillPinned, pinMoves, pinOverflow, pinWarnings, dayPreviews)
        {
            NoNearDay = noNearDay
        };
    }

    /// <summary>WP-VP-4E — the week's visits a pin moves. <c>visit</c>: the pinned target's own visit(s) (a doctor by its
    /// contact id, a pharmacy / institution by its account id). <c>institution</c>: every visit of the target's
    /// institution group — the day balancer's group rule (a doctor → its account; a linked pharmacy → its institution).</summary>
    private static List<WeekItem> MatchPin(PlanningDayPin pin, IEnumerable<WeekItem> items, IReadOnlyDictionary<Guid, string> groupOf)
    {
        var all = items.ToList();
        bool IsTarget(WeekItem i) => pin.TargetType == PlannedVisitTargetType.Contact
            ? i.Candidate.ContactId == (pin.ContactId ?? pin.TargetId)
            : i.Candidate.TargetId == pin.TargetId && i.Candidate.TargetType == pin.TargetType;
        if (pin.Scope != PlanningDayPinScopes.Institution)
        {
            return all.Where(IsTarget).ToList();
        }

        var self = all.FirstOrDefault(IsTarget);
        var key = self is not null ? GroupKeyOf(groupOf, self.Candidate)
            : groupOf.TryGetValue(pin.ContactId ?? pin.TargetId, out var g) ? g : pin.TargetId.ToString("N");
        return all.Where(i => GroupKeyOf(groupOf, i.Candidate) == key).ToList();
    }

    /// <summary>WP-VP-3B / 4E — a visit's institution group key (the same rule for balancing, pins and the preview).</summary>
    private static string GroupKeyOf(IReadOnlyDictionary<Guid, string> groupOf, Candidate candidate)
        => groupOf.TryGetValue(candidate.TargetId, out var g) ? g : candidate.TargetId.ToString("N");

    /// <summary>Routes ONE day: the optimizer gets only that day, the day kind's working window and the manual order.</summary>
    private (IReadOnlyList<DayPlaced> Placed, IReadOnlyList<(WeekItem Item, string Reason)> Missed) RouteDay(
        DateOnly date,
        string kind,
        IReadOnlyList<WeekItem> items,
        PlanningDayBudget budget,
        WorkingDayHours routeDay,
        int buffer,
        GeoPoint? startLocation,
        IReadOnlyList<Guid>? manualOrder)
    {
        var refs = new Dictionary<Guid, WeekItem>();
        var visits = new List<RouteVisitInput>(items.Count);
        foreach (var item in items)
        {
            var visitRef = Guid.NewGuid();
            refs[visitRef] = item;
            visits.Add(new RouteVisitInput(
                visitRef, item.Candidate.Lat, item.Candidate.Long, Math.Max(1, item.Candidate.DurationMinutes),
                item.Candidate.Windows, item.Candidate.TargetId));
        }

        var output = _optimizer.Optimize(new RouteOptimizationInput(
            visits,
            new RepWorkingHours(budget.WindowFor(kind, routeDay), startLocation),
            new OptimizationPeriod(date, date),
            buffer,
            new TravelModelSpec(),
            // Manual sequence (target ids) orders the visits WITHIN the day; null ⇒ the greedy optimum.
            manualOrder));

        var placed = output.Scheduled
            .Where(s => refs.ContainsKey(s.VisitId))
            .Select(s => new DayPlaced(refs[s.VisitId], s.AssignedDate, s.StartTime, s.EndTime, s.SequenceOrder))
            .ToList();
        var missed = output.Unscheduled
            .Where(u => refs.ContainsKey(u.VisitId))
            .Select(u => (refs[u.VisitId], u.Reason))
            .ToList();
        return (placed, missed);
    }

    /// <summary>WP-VP-3B — the institution group of every candidate: a doctor → its account; a bare account → itself; a
    /// pharmacy → the planned institution it is linked to (account relationship, smallest id when several), else itself.
    /// One relationship read per selected pharmacy.</summary>
    private async Task<IReadOnlyDictionary<Guid, string>> GroupKeysAsync(
        Guid tenantId, IReadOnlyList<Candidate> candidates, CancellationToken cancellationToken)
    {
        var institutions = candidates
            .Select(c => c.TargetType == PlannedVisitTargetType.Contact ? c.AccountId
                : c.TargetType == PlannedVisitTargetType.Account ? c.TargetId : (Guid?)null)
            .OfType<Guid>()
            .ToHashSet();
        var keys = new Dictionary<Guid, string>();
        foreach (var c in candidates)
        {
            Guid group;
            if (c.TargetType == PlannedVisitTargetType.Contact)
            {
                group = c.AccountId ?? c.TargetId;
            }
            else if (c.TargetType == PlannedVisitTargetType.Pharmacy && _relationships is not null)
            {
                var related = (await _relationships.ListByAccountAsync(tenantId, c.TargetId, cancellationToken))
                    .Where(r => r.TenantId == tenantId && !r.IsDeleted && !RelationshipLifecycle.IsClosed(r.Status))
                    .Select(r => r.SourceAccountId == c.TargetId ? r.TargetAccountId : r.SourceAccountId)
                    .Where(institutions.Contains)
                    .OrderBy(id => id)
                    .ToList();
                group = related.Count > 0 ? related[0] : c.TargetId;
            }
            else
            {
                group = c.TargetId;
            }

            keys[c.TargetId] = group.ToString("N");
        }

        return keys;
    }

    /// <summary>WP-VP-3B — why a week overflowed: it lost a weekday to a holiday, else to a half day, else it was simply
    /// full.</summary>
    private static string ShiftReason(DateOnly from, DateOnly to, PlanningCalendarResult calendar)
    {
        var kinds = new List<string>();
        for (var d = from; d <= to; d = d.AddDays(1))
        {
            if (d.DayOfWeek is not (DayOfWeek.Saturday or DayOfWeek.Sunday))
            {
                kinds.Add(calendar.KindOf(d));
            }
        }

        return kinds.Contains(PlanningDayKinds.Holiday) ? PlanningShiftReasons.Holiday
            : kinds.Contains(PlanningDayKinds.Half) ? PlanningShiftReasons.HalfDay
            : PlanningShiftReasons.CapacityFull;
    }

    private static void Unshift(List<ShiftTrack> shifts, WeekItem item)
    {
        if (item.Shift is { } track)
        {
            shifts.Remove(track);
        }
    }

    /// <summary>WP-VP-3B (B-7, C5) — per week: its working / half / holiday days inside the period, the day budgets
    /// summed (capacity), Σ(duration + buffer) of its planned + fixed visits (planned), the visit count and the full-day
    /// cap; and the period's totals.</summary>
    private static (IReadOnlyList<WeekCapacityDto> Weeks, PeriodCapacityDto Period) BuildCapacity(
        IReadOnlyList<PlanningWeekSpan> periodWeeks,
        PlanningCalendarResult calendar,
        PlanningDayBudget budget,
        int buffer,
        IReadOnlyList<PlacedVisit> placed,
        IReadOnlyList<PlannedVisitEntity> fixedVisits)
    {
        var weeks = new List<WeekCapacityDto>(periodWeeks.Count);
        for (var i = 0; i < periodWeeks.Count; i++)
        {
            var span = periodWeeks[i];
            int working = 0, half = 0, holidays = 0, capacityMinutes = 0;
            for (var d = span.From; d <= span.To; d = d.AddDays(1))
            {
                var kind = calendar.KindOf(d);
                working += kind == PlanningDayKinds.Working ? 1 : 0;
                half += kind == PlanningDayKinds.Half ? 1 : 0;
                holidays += kind == PlanningDayKinds.Holiday ? 1 : 0;
                capacityMinutes += budget.BudgetFor(kind);
            }

            var weekPlaced = placed.Where(p => p.WeekNumber == i).ToList();
            var weekFixed = fixedVisits.Where(v => PlanningWeekCalendar.MondayOf(v.PlannedDate) == span.Monday).ToList();
            var plannedMinutes = weekPlaced.Sum(p => Math.Max(1, p.Candidate.DurationMinutes) + buffer)
                                 + weekFixed.Sum(v => Math.Max(0, v.PlannedDurationMinutes ?? 0) + buffer);
            weeks.Add(new WeekCapacityDto(
                span.WeekStart, working, half, holidays, capacityMinutes, plannedMinutes,
                weekPlaced.Count + weekFixed.Count, budget.CapFor(PlanningDayKinds.Working)));
        }

        return (weeks, new PeriodCapacityDto(
            weeks.Sum(w => w.CapacityMinutes), weeks.Sum(w => w.PlannedMinutes), budget.BudgetMinutes,
            budget.CapFor(PlanningDayKinds.Working), budget.CapFor(PlanningDayKinds.Half), budget.Source));
    }

    /// <summary>WP-VP-3A — the target a visit counts for: the doctor for a doctor visit, else the visited account.</summary>
    private static Guid TargetKeyOf(PlannedVisitEntity visit)
        => string.Equals(visit.TargetType, PlannedVisitTargetType.Contact, StringComparison.Ordinal) && visit.ContactId is { } c
            ? c
            : visit.TargetId;

    /// <summary>WP-VP-3A — the rep's visits inside the period (not cancelled / archived) that have a COMPLETED report.
    /// One bulk report read.</summary>
    private async Task<IReadOnlyList<PlannedVisitEntity>> CompletedInPeriodAsync(
        Guid tenantId, PlanningSession session, IReadOnlyList<PlannedVisitEntity> allPlans, DateOnly periodStart,
        DateOnly periodEnd, CancellationToken cancellationToken)
    {
        if (_reports is null)
        {
            return Array.Empty<PlannedVisitEntity>();
        }

        var mine = allPlans
            .Where(p => string.Equals(p.Resource.ResourceId, session.ResourceId, StringComparison.OrdinalIgnoreCase)
                        && p.PlannedDate >= periodStart && p.PlannedDate <= periodEnd
                        && !p.IsCancelled() && !p.IsArchived())
            .ToList();
        if (mine.Count == 0)
        {
            return Array.Empty<PlannedVisitEntity>();
        }

        var completed = (await _reports.ListByPlannedVisitIdsAsync(tenantId, mine.Select(p => p.Id).ToList(), cancellationToken))
            .Where(r => r.IsCompleted())
            .Select(r => r.PlannedVisitId)
            .ToHashSet();
        return mine.Where(p => completed.Contains(p.Id)).ToList();
    }

    private static int IndexOfWeek(IReadOnlyList<PlanningWeekSpan> weeks, DateOnly monday)
    {
        for (var i = 0; i < weeks.Count; i++)
        {
            if (weeks[i].Monday == monday)
            {
                return i;
            }
        }

        return -1;
    }

    private async Task<SupplyDemandSummary> BuildSupplyDemandAsync(
        CapacityEntity? capacity, CyclePeriodSnapshot period, int scheduled, int unscheduledCount,
        CancellationToken cancellationToken)
    {
        int? supply = null;
        var reasons = new List<string>();
        if (capacity is not null)
        {
            var estimate = await _estimator.EstimateAsync(capacity, period, cancellationToken);
            supply = estimate.Calculation.TotalVisitNumber;
            if (supply is null)
            {
                reasons.Add("supply_unresolved");
            }
        }
        else
        {
            reasons.Add("capacity_not_pinned");
        }

        var demand = scheduled + unscheduledCount;
        var status = PlanningSessionSupplyDemandStatus.Unknown;
        if (supply is { } s)
        {
            status = demand > s || unscheduledCount > 0
                ? PlanningSessionSupplyDemandStatus.OverPlanned
                : PlanningSessionSupplyDemandStatus.Ok;
        }
        else if (unscheduledCount > 0)
        {
            status = PlanningSessionSupplyDemandStatus.OverPlanned;
        }

        if (unscheduledCount > 0)
        {
            reasons.Add("visits_unscheduled");
        }

        return new SupplyDemandSummary(supply, demand, scheduled, unscheduledCount, status, reasons);
    }

    private async Task<PlannedVisitEntity> BuildAtomAsync(
        PlanningSession session, PlacedVisit placed, int sequence, CancellationToken cancellationToken)
    {
        var candidate = placed.Candidate;
        var now = DateTimeOffset.UtcNow;
        var actor = _actor.ActorName;

        // WP-VP-2 (B-3, K-3) — the atom's play / campaign / segment are DERIVED from the doctor (and the visit date for the
        // campaign); the session's stored segment / campaign / strategy are no longer used.
        var play = await _deriver.DerivePlayAsync(candidate.ContactId, now, cancellationToken);
        var atomAccountId = candidate.TargetType == PlannedVisitTargetType.Contact ? candidate.AccountId : candidate.TargetId;
        var campaignId = await _deriver.DeriveCampaignAsync(candidate.ContactId, atomAccountId, placed.Date, cancellationToken);

        var entity = new PlannedVisitEntity
        {
            Id = Guid.NewGuid(),
            TenantId = session.TenantId,
            VisitCode = BuildVisitCode(session.Id, sequence),
            TargetType = candidate.TargetType,
            TargetId = candidate.TargetId,
            AccountId = candidate.TargetType == PlannedVisitTargetType.Contact ? candidate.AccountId : candidate.TargetId,
            ContactId = candidate.ContactId,
            AccountContactLinkId = candidate.AccountContactLinkId,
            PlannedDate = placed.Date,
            PlannedStartTime = placed.StartTime,
            PlannedEndTime = placed.EndTime,
            PlannedDurationMinutes = candidate.DurationMinutes,
            Resource = new PlannedVisitResourceRef
            {
                ResourceId = session.ResourceId.Trim(),
                ResourceType = PlannedVisitResourceTypes.Normalize(session.ResourceType),
                DisplayName = string.IsNullOrWhiteSpace(session.ResourceDisplayName) ? null : session.ResourceDisplayName
            },
            VisitPurpose = PlannedVisitPurpose.MedicalVisit,
            VisitType = PlannedVisitType.FieldVisit,
            BusinessUnit = null,
            CampaignId = campaignId,
            PlanStatus = PlannedVisitStatus.Planned,
            Source = PlannedVisitSource.RoutePlan, // FU05 is the route-plan producer (FU01 reserves this value for it)
            Slot = new PlannedVisitScheduleSlot
            {
                SequenceOrder = placed.SequenceOrder,
                SlotStartTime = placed.StartTime,
                SlotEndTime = placed.EndTime
            },
            Selection = new PlannedVisitSelectionProvenance
            {
                SegmentId = play.SegmentId,
                CampaignId = campaignId,
                StrategyTemplateId = play.StrategyTemplateId,
                SelectionMode = PlannedVisitSelectionMode.Recommended, // FU05 motor selection (FU01 reserves this)
                DecidedAt = now,
                DecidedBy = actor,
                Extra = placed.IsExtra // WP-VP-4L (2) — the rep's per-week extra visit
            },
            CreatedAt = now,
            CreatedBy = actor
        };

        // Content ref (FU04 result → FU01's own journey probe, so the same validation the create handler runs applies).
        // WP-SB-3b — the visit's own (projected) content; the singular ref is its first promo item (mobile contract).
        var content = placed.Content ?? candidate.Content;
        if (content?.JourneyId is { } journeyId && journeyId != Guid.Empty)
        {
            var journey = await _journeyProbe.ResolveAsync(
                journeyId, content.StageId, PlannedVisitContentSource.Strategy,
                play.StrategyTemplateId, cancellationToken);
            if (journey.ContentRef is { } contentRef)
            {
                contentRef.StageIndex = content.StageIndex;
                entity.Content = contentRef;
            }
        }

        // WP-SB-3b — the product list FROZEN at plan time (a later path release does not change this plan).
        entity.ContentItems = (content?.Items ?? Array.Empty<VisitContentItem>()).Select(ToContentItem).ToList();

        // Derived provenance — read-only, stored not enforced (mirrors the FU01 create handler exactly).
        entity.Frequency = await _frequencyProbe.ResolveAsync(entity, null, cancellationToken);
        entity.Consent = await _consentProbe.EvaluateAsync(entity, cancellationToken);
        entity.Availability = await _availabilityProbe.CaptureAsync(entity, cancellationToken);

        return entity;
    }

    // ── helpers ──────────────────────────────────────────────────────────────────────────────────────────────────

    /// <summary>WP-VP-2 (B-3) — the content of a visit to <paramref name="contactId"/> under the play DERIVED from the
    /// doctor's own active segments (never the session's / client's strategy or segment).</summary>
    private async Task<VisitContentSequenceResult> ResolveContentAsync(
        PlanningSession session, Guid contactId, IReadOnlyList<VisitContentPendingExposure> pending, DateTimeOffset at,
        CancellationToken cancellationToken,
        IReadOnlyList<VisitContentProductPick>? picks = null, int visitOrdinal = 1, IReadOnlyList<Guid>? carryOver = null)
        => await _content.ResolveAsync(
            new VisitContentSequenceRequest(
                SubjectType: PlannedVisitTargetType.Contact,
                SubjectId: contactId,
                SegmentId: null,
                StrategyTemplateId: (await _deriver.DerivePlayAsync(contactId, at, cancellationToken)).StrategyTemplateId,
                CyclePeriodId: session.CyclePeriodId,
                PriorStageIndex: null, // WP-SB-3b — the stage comes from JourneyProgress (+ pending), not the last plan
                EffectiveAt: at,
                PendingExposures: pending,
                // WP-VP-3C (K-7) — the rep's pick, the doctor's n-th visit (order shift) and the previous overflow.
                RepProducts: picks,
                VisitOrdinal: visitOrdinal,
                CarryOverProductIds: carryOver),
            cancellationToken);

    /// <summary>WP-VP-3C (K-7, S-4) — the rep's product pick for this doctor on the session (contact + account, else the
    /// contact).</summary>
    private static IReadOnlyList<VisitContentProductPick> PicksOf(PlanningSession session, Guid contactId, Guid? accountId)
    {
        var selected = session.Selection.SelectedContacts.FirstOrDefault(c => c.ContactId == contactId && c.AccountId == accountId)
                       ?? session.Selection.SelectedContacts.FirstOrDefault(c => c.ContactId == contactId);
        return (selected?.Products ?? new List<PlanningSessionSelectedProduct>())
            .Select(p => new VisitContentProductPick(p.ProductId, p.ProductCode, p.Role))
            .ToList();
    }

    /// <summary>WP-VP-3C (K-7e) — the rep's visits to the doctor already in the period before <paramref name="before"/>
    /// (stored, not cancelled / archived): the n of the next visit is this + 1.</summary>
    private static int PriorVisitsInPeriod(
        IReadOnlyList<PlannedVisitEntity> allPlans, PlanningSession session, Guid contactId, DateOnly periodStart,
        DateOnly before)
        => allPlans.Count(p => p.ContactId == contactId
                               && string.Equals(p.Resource.ResourceId, session.ResourceId, StringComparison.OrdinalIgnoreCase)
                               && p.PlannedDate >= periodStart && p.PlannedDate < before
                               && !p.IsCancelled() && !p.IsArchived());

    /// <summary>WP-SB-3b — the doctor's stored plans before <paramref name="before"/> (not cancelled / archived) that tell
    /// a product, counted per (product, journey).</summary>
    private static IReadOnlyList<VisitContentPendingExposure> PendingBefore(
        IReadOnlyList<PlannedVisitEntity> pendingPlans, Guid contactId, DateOnly before)
        => Count(pendingPlans
            .Where(p => p.ContactId == contactId && p.PlannedDate < before)
            .SelectMany(p => p.ContentItems.Select(i => (i.ProductId, i.JourneyId))));

    private static IReadOnlyList<VisitContentPendingExposure> Count(IEnumerable<(Guid ProductId, Guid JourneyId)> told)
        => told
            .GroupBy(t => t)
            .Select(g => new VisitContentPendingExposure(g.Key.ProductId, g.Key.JourneyId, g.Count()))
            .OrderBy(p => p.ProductId).ThenBy(p => p.JourneyId)
            .ToList();

    /// <summary>
    /// WP-SB-3b — the content of every placed doctor visit, in date order per doctor: the stored pending plans before the
    /// visit's date plus the doctor's earlier visits of THIS run project the stage / exposure on (CT rule). A visit whose
    /// projection equals the one the candidate was resolved with keeps that result; any other is resolved again. The
    /// slot duration stays the candidate's (the route was packed with it).
    /// </summary>
    private async Task<List<PlacedVisit>> ProjectContentAsync(
        PlanningSession session, List<PlacedVisit> placed, IReadOnlyList<PlannedVisitEntity> pendingPlans,
        DateTimeOffset at, CancellationToken cancellationToken, Func<Guid, DateOnly, int> priorVisits)
    {
        var earlierInRun = new Dictionary<Guid, List<(Guid ProductId, Guid JourneyId)>>();
        // WP-VP-3C (K-7e, S-2) — per doctor: this run's visits so far and the rep picks the previous one could not hold.
        var runCount = new Dictionary<Guid, int>();
        var carry = new Dictionary<Guid, IReadOnlyList<Guid>>();
        var ordered = placed
            .Select((p, i) => (Visit: p, Index: i))
            .OrderBy(x => x.Visit.Date).ThenBy(x => x.Visit.SequenceOrder).ThenBy(x => x.Index)
            .ToList();
        var projected = new PlacedVisit[placed.Count];

        foreach (var (visit, index) in ordered)
        {
            if (visit.Candidate.ContactId is not { } contactId || visit.Candidate.Content is not { } baseline
                || visit.Candidate.TargetType != PlannedVisitTargetType.Contact)
            {
                projected[index] = visit;
                continue;
            }

            if (!earlierInRun.TryGetValue(contactId, out var told))
            {
                told = new List<(Guid, Guid)>();
                earlierInRun[contactId] = told;
            }

            var pending = Count(pendingPlans
                .Where(p => p.ContactId == contactId && p.PlannedDate < visit.Date)
                .SelectMany(p => p.ContentItems.Select(i => (i.ProductId, i.JourneyId)))
                .Concat(told));
            var ordinal = priorVisits(contactId, visit.Date) + runCount.GetValueOrDefault(contactId) + 1;
            var carried = carry.GetValueOrDefault(contactId) ?? Array.Empty<Guid>();
            var content = pending.SequenceEqual(visit.Candidate.ContentPending ?? Array.Empty<VisitContentPendingExposure>())
                          && ordinal == visit.Candidate.BaseOrdinal && carried.Count == 0
                ? baseline
                : await ResolveContentAsync(
                    session, contactId, pending, at, cancellationToken, visit.Candidate.RepPicks, ordinal, carried);

            runCount[contactId] = runCount.GetValueOrDefault(contactId) + 1;
            carry[contactId] = (content.OverflowProducts ?? Array.Empty<VisitContentOverflow>())
                .Where(o => o.Source == PlannedVisitContentItemSources.RepPick)
                .Select(o => o.ProductId)
                .ToList();
            told.AddRange((content.Items ?? Array.Empty<VisitContentItem>())
                .Where(i => i.HasContent)
                .Select(i => (i.ProductId, i.JourneyId)));
            projected[index] = visit with { Content = content };
        }

        return projected.ToList();
    }

    private static PlannedVisitContentItem ToContentItem(VisitContentItem item) => new()
    {
        ProductId = item.ProductId,
        ProductCode = item.ProductCode,
        Role = item.Role,
        JourneyId = item.JourneyId,
        JourneyCode = item.JourneyCode,
        StageId = item.StageId,
        StageIndex = item.StageIndex,
        StageCode = item.StageCode,
        StageName = item.StageName,
        PathId = item.PathId,
        PathCode = item.PathCode,
        PathVersion = item.PathVersion,
        Steps = item.Steps.Select(s => new PlannedVisitContentStep
        {
            StepId = s.StepId, ContentId = s.ContentId, ContentCode = s.ContentCode, Title = s.Title, Type = s.Type,
            Minutes = s.Minutes
        }).ToList(),
        Claims = item.Claims.Select(c => new PlannedVisitContentClaim { ClaimId = c.ClaimId, ClaimCode = c.ClaimCode }).ToList(),
        Warnings = item.Warnings.ToList(),
        // WP-VP-3C (K-7) — where the product came from and its place in the list (no play id, S3-9).
        Source = item.Source,
        Order = item.Order,
        ProductName = item.ProductName
    };

    /// <summary>WP-VP-3C — a stored (frozen) item read back for the preview (an older item reads as <c>play</c>).</summary>
    private static VisitContentItem FromStored(PlannedVisitContentItem item) => new(
        item.ProductId, item.ProductCode, item.Role, item.JourneyId, item.JourneyCode ?? string.Empty, item.StageId,
        item.StageIndex, item.StageCode ?? string.Empty, item.StageName ?? string.Empty, item.PathId,
        item.PathCode ?? string.Empty, item.PathVersion ?? string.Empty,
        item.Steps.Select(st => new VisitContentStep(
            st.StepId, st.ContentId, st.ContentCode ?? string.Empty, st.Title ?? string.Empty, st.Type ?? string.Empty,
            st.Minutes)).ToList(),
        item.Claims.Select(c => new VisitContentClaim(c.ClaimId, c.ClaimCode ?? string.Empty)).ToList(),
        item.Warnings.ToList(),
        item.EffectiveSource(),
        item.Order,
        item.ProductName);

    private async Task<(double Lat, double Long)> ResolveCoordinatesAsync(
        Guid tenantId, Guid? accountId, Dictionary<Guid, AccountEntity?> cache, CancellationToken cancellationToken)
    {
        if (accountId is not { } id || id == Guid.Empty)
        {
            return (double.NaN, double.NaN);
        }

        if (!cache.TryGetValue(id, out var account))
        {
            account = await _accounts.GetByIdAsync(tenantId, id, cancellationToken);
            cache[id] = account;
        }

        return (account?.Latitude ?? double.NaN, account?.Longitude ?? double.NaN);
    }

    private static Candidate NonDoctorCandidate(
        string targetType, Guid targetId, Guid accountId, double lat, double lng, int duration)
        => new(
            targetType, targetId, accountId, null, null, lat, lng, duration,
            null, null, null, 0, 0, VisitContentSequenceStatus.NotApplicable, false,
            Array.Empty<AvailabilityWindow>(), null, null);

    /// <summary>WP-CAP-MODEL — a typical-model capacity plans an unsequenced visit at its TYPICAL visit length (the
    /// same minutes the capacity divides by); a legacy row keeps today's value (the report charge only).
    /// <para>Public only so the rule can be tested directly; it reads nothing but the capacity.</para></summary>
    public static int DefaultDuration(CapacityEntity? capacity)
        => capacity is null
            ? DefaultVisitDurationMinutes
            : Math.Max(1, capacity.UsesTypicalVisitModel()
                ? capacity.TypicalVisitMinutes()
                : ActivityTimeBudgetCalculator.VisitDuration(capacity, 0, 0));

    private static GeoPoint? ResolveStartLocation(VisitPlanGenerationOptions options)
        => options.StartLat is { } lat && options.StartLong is { } lng ? new GeoPoint(lat, lng) : null;

    private static string BuildVisitCode(Guid sessionId, int sequence)
        => $"VP-{sessionId.ToString("N")[..8]}-{sequence:D4}";

    // ── internal value types ─────────────────────────────────────────────────────────────────────────────────────

    /// <summary>WP-VP-3B — one visit a week has to place: its first week (<see cref="FromWeek"/>) and, once moved, its
    /// shift record.</summary>
    private sealed record WeekItem(Candidate Candidate, int FromWeek, ShiftTrack? Shift, bool IsExtra = false);

    /// <summary>WP-VP-3B — a visit moved to a later week (mutable target week while it keeps moving).</summary>
    private sealed class ShiftTrack(Candidate candidate, int fromWeek, string reason)
    {
        public Candidate Candidate { get; } = candidate;
        public int FromWeek { get; } = fromWeek;
        public string Reason { get; } = reason;
        public int ToWeek { get; set; } = fromWeek;
    }

    private sealed record DayPlaced(WeekItem Item, DateOnly Date, string Start, string End, int Sequence);

    private sealed record WeekPlan(
        IReadOnlyList<DayPlaced> Placed,
        IReadOnlyList<(WeekItem Item, string Reason)> Unscheduled,
        IReadOnlyList<WeekItem> Overflow,
        // WP-VP-4E — the placed pinned visits (value = auto-pinned), the pinned visits moved (in the week or out of it),
        // those that left the week, the pins ignored, and every working day's budget / planned / idle minutes.
        IReadOnlyDictionary<WeekItem, bool> Pinned,
        IReadOnlyList<PinMove> PinMoves,
        IReadOnlySet<WeekItem> PinOverflow,
        IReadOnlyList<PinWarningPreview> PinWarnings,
        IReadOnlyList<PlanningDayPreview> Days)
    {
        /// <summary>WP-VP-4G (F4-1) - overflow visits a day still had room for (no near / light day): no_near_day.</summary>
        public IReadOnlySet<WeekItem> NoNearDay { get; init; } = new HashSet<WeekItem>();
    }

    /// <summary>WP-VP-4E — a pinned visit that did not fit its day: moved to <see cref="To"/> in the week, or (To null) out
    /// of it; <see cref="Track"/> follows it into a later week.</summary>
    private sealed class PinMove(WeekItem item, DateOnly from, DateOnly? to, string reason)
    {
        public WeekItem Item { get; } = item;
        public DateOnly From { get; } = from;
        public DateOnly? To { get; } = to;
        public string Reason { get; } = reason;
        public ShiftTrack? Track { get; set; }
    }

    private sealed record Candidate(
        string TargetType,
        Guid TargetId,
        Guid? AccountId,
        Guid? ContactId,
        Guid? AccountContactLinkId,
        double Lat,
        double Long,
        int DurationMinutes,
        Guid? JourneyId,
        Guid? StageId,
        int? StageIndex,
        int PromoItemCount,
        int NonPromoItemCount,
        string ContentStatus,
        bool ConsentBlocked,
        IReadOnlyList<AvailabilityWindow> Windows,
        VisitContentSequenceResult? Content,
        IReadOnlyList<VisitContentPendingExposure>? ContentPending,
        // WP-VP-3C (K-7) — the rep's pick for the doctor and the n of its first planned visit in the period.
        IReadOnlyList<VisitContentProductPick>? RepPicks = null,
        int BaseOrdinal = 1);

    /// <summary><see cref="Content"/> — WP-SB-3b: the visit's own content (projected over the doctor's earlier visits);
    /// null for a non-doctor visit.</summary>
    private sealed record PlacedVisit(
        Candidate Candidate,
        int WeekNumber,
        DateOnly Date,
        string StartTime,
        string EndTime,
        int SequenceOrder,
        VisitContentSequenceResult? Content = null,
        // WP-VP-4E — on a pinned day (auto = moved there from a full pinned day) and the institution group.
        bool IsPinned = false,
        bool AutoPinned = false,
        string? GroupKey = null,
        // WP-VP-4L (2) — the rep's per-week extra visit.
        bool IsExtra = false);

    private sealed record GenerationOutput(
        PlanningSession Session,
        CyclePeriodSnapshot Period,
        DateOnly PeriodStart,
        DateOnly PeriodEnd,
        int WeekCount,
        IReadOnlyList<PlacedVisit> Placed,
        IReadOnlyList<UnscheduledPreview> Unscheduled,
        IReadOnlyList<DoctorContentPreview> Content,
        IReadOnlyList<TerritoryWarning> TerritoryWarnings,
        SupplyDemandSummary SupplyDemand,
        PlanningCalendarResult Calendar,
        // WP-VP-3A — the period's weeks, "today", the fixed (already written) visits and each target's cadence.
        IReadOnlyList<PlanningWeekSpan> PeriodWeeks,
        DateOnly Today,
        IReadOnlyList<PlannedVisitEntity> Fixed,
        IReadOnlyDictionary<Guid, FrequencyRequirement> Frequency,
        // WP-VP-3B — the visits moved to a later week, every week's capacity and the period in minutes.
        IReadOnlyList<ShiftedVisitPreview> Shifted,
        IReadOnlyList<WeekCapacityDto> WeekCapacity,
        PeriodCapacityDto PeriodCapacity,
        // WP-VP-4E — days, moved pinned visits, ignored pins, the per-visit model.
        IReadOnlyList<PlanningDayPreview> Days,
        IReadOnlyList<PinOverflowPreview> PinOverflow,
        IReadOnlyList<PinWarningPreview> PinWarnings,
        VisitModelDto VisitModel);

    private sealed record GenerationResult(string? Error, GenerationOutput? Output)
    {
        public static GenerationResult Failed(string error) => new(error, null);
        public static GenerationResult Succeeded(GenerationOutput output) => new(null, output);
    }

    /// <summary>Wrap <see cref="ToPreview"/> after resolving the placed contacts' display names / specialties in one
    /// batch read — so the UI never depends on an account↔contact link existing to show the doctor.</summary>
    private async Task<VisitPlanPreview> BuildPreviewAsync(GenerationOutput g, CancellationToken cancellationToken)
    {
        var ids = g.Placed.Select(p => p.Candidate.ContactId)
            .Concat(g.Fixed.Select(v => v.ContactId))
            .OfType<Guid>().Distinct().ToList();
        var names = new Dictionary<Guid, (string? Name, string? Specialty)>();
        if (_tenant.TenantId is { } tenantId && ids.Count > 0)
        {
            foreach (var c in await _contactRepo.ListByIdsAsync(tenantId, ids, cancellationToken))
            {
                names[c.Id] = (c.DisplayName, c.Specialty);
            }
        }

        var preview = ToPreview(g, names);

        // WP-VP-4G (F4-4) — product names in ONE bulk read (fail-open: no name ⇒ the code is shown).
        preview = WithProductNames(preview, await ProductNamesAsync(ProductIdsOf(g), cancellationToken));

        // WP-VP-4G (F4-9) — the report state of the approved (written) visits in ONE bulk report read.
        return await WithReportStatusAsync(preview, g, cancellationToken);
    }

    /// <summary>WP-VP-4G (F4-4) — the names of <paramref name="ids"/>, asking the master only for ids this request has not
    /// asked yet (one bulk call for the rest). Never throws (the reader is fail-open).</summary>
    private async Task<IReadOnlyDictionary<Guid, string>> ProductNamesAsync(IEnumerable<Guid> ids, CancellationToken cancellationToken)
    {
        var missing = ids.Where(id => id != Guid.Empty && !_productNamesAsked.Contains(id)).Distinct().ToList();
        if (missing.Count > 0 && _productNames is not null)
        {
            foreach (var (id, name) in await _productNames.ReadNamesAsync(missing, cancellationToken))
            {
                _productNameMemo[id] = name;
            }
        }

        missing.ForEach(id => _productNamesAsked.Add(id));
        return _productNameMemo;
    }

    /// <summary>WP-VP-4G — every product id a run shows: its placed visits' items, the doctors' candidate lists and
    /// overflows, the written visits' items.</summary>
    private static IEnumerable<Guid> ProductIdsOf(GenerationOutput g)
        => g.Placed.SelectMany(p => (p.Content?.Items ?? Array.Empty<VisitContentItem>()).Select(i => i.ProductId)
                .Concat((p.Content?.OverflowProducts ?? Array.Empty<VisitContentOverflow>()).Select(o => o.ProductId))
                .Concat((p.Candidate.Content?.Items ?? Array.Empty<VisitContentItem>()).Select(i => i.ProductId))
                .Concat((p.Candidate.Content?.OverflowProducts ?? Array.Empty<VisitContentOverflow>()).Select(o => o.ProductId)))
            .Concat(g.Fixed.SelectMany(v => v.ContentItems.Select(i => i.ProductId)))
            .Concat(g.Content.SelectMany(c => (c.Items ?? Array.Empty<VisitContentItem>()).Select(i => i.ProductId)))
            .Distinct();

    /// <summary>WP-VP-4G (F4-4) — the preview with every product item / overflow / distribution / weekly count named
    /// (a frozen snapshot name is kept as it is).</summary>
    private static VisitPlanPreview WithProductNames(VisitPlanPreview p, IReadOnlyDictionary<Guid, string> names)
    {
        if (names.Count == 0)
        {
            return p;
        }

        string? Name(Guid id, string? current) => current ?? names.GetValueOrDefault(id);
        IReadOnlyList<VisitContentItem>? Items(IReadOnlyList<VisitContentItem>? items)
            => items?.Select(i => i with { ProductName = Name(i.ProductId, i.ProductName) }).ToList();

        return p with
        {
            Scheduled = p.Scheduled.Select(s => s with
            {
                ContentItems = Items(s.ContentItems),
                OverflowProducts = s.OverflowProducts?.Select(o => o with { ProductName = Name(o.ProductId, o.ProductName) }).ToList()
            }).ToList(),
            Content = p.Content.Select(c => c with
            {
                Items = Items(c.Items),
                Products = c.Products?.Select(x => x with { ProductName = Name(x.ProductId, x.ProductName) }).ToList()
            }).ToList(),
            ProductDistribution = p.ProductDistribution?.Select(d => d with { ProductName = Name(d.ProductId, d.ProductName) }).ToList(),
            WeekCapacity = p.WeekCapacity?.Select(w => w with
            {
                ProductVisitCounts = w.ProductVisitCounts?.Select(c => c with { ProductName = Name(c.ProductId, c.ProductName) }).ToList()
            }).ToList()
        };
    }

    /// <summary>WP-VP-4G (F4-9) — a written (fixed) visit's slot says whether it was reported or cancelled; every week
    /// counts its reported visits. One bulk report read; without a report reader nothing is reported.</summary>
    private async Task<VisitPlanPreview> WithReportStatusAsync(VisitPlanPreview p, GenerationOutput g, CancellationToken cancellationToken)
    {
        if (_reports is null || g.Fixed.Count == 0 || _tenant.TenantId is not { } tenantId)
        {
            return p;
        }

        var reported = (await _reports.ListByPlannedVisitIdsAsync(tenantId, g.Fixed.Select(v => v.Id).ToList(), cancellationToken))
            .Where(r => r.TenantId == tenantId)
            .Select(r => r.PlannedVisitId)
            .ToHashSet();
        var cancelled = g.Fixed.Where(v => v.IsCancelled()).Select(v => v.Id).ToHashSet();
        var slots = p.Scheduled.Select(s => !s.IsFixed ? s
                : cancelled.Contains(s.VisitRef) ? s with { ReportStatus = PlannedSlotReportStatuses.Cancelled }
                : reported.Contains(s.VisitRef) ? s with { ReportStatus = PlannedSlotReportStatuses.Reported }
                : s)
            .ToList();
        return p with
        {
            Scheduled = slots,
            Weeks = p.Weeks?.Select(w => w with
            {
                ReportedVisitCount = slots.Count(s => s.WeekStart == w.WeekStart && s.ReportStatus == PlannedSlotReportStatuses.Reported)
            }).ToList()
        };
    }

    private static VisitPlanPreview ToPreview(GenerationOutput g, IReadOnlyDictionary<Guid, (string? Name, string? Specialty)> contactNames)
    {
        var scheduled = g.Placed
            .OrderBy(p => p.WeekNumber)
            .ThenBy(p => p.Date)
            .ThenBy(p => p.SequenceOrder)
            .Select(p => new PlannedSlotPreview(
                Guid.NewGuid(), p.WeekNumber, p.Candidate.TargetType, p.Candidate.TargetId,
                p.Candidate.AccountId, p.Candidate.ContactId, p.Candidate.AccountContactLinkId,
                p.Date.ToString("yyyy-MM-dd"), p.StartTime, p.EndTime, p.SequenceOrder,
                p.Candidate.DurationMinutes,
                p.Content?.JourneyId ?? p.Candidate.JourneyId,
                p.Content is { } c1 ? c1.StageId : p.Candidate.StageId,
                p.Content is { } c2 ? c2.StageIndex : p.Candidate.StageIndex,
                p.Content?.PromoItemCount ?? p.Candidate.PromoItemCount,
                p.Content?.NonPromoItemCount ?? p.Candidate.NonPromoItemCount,
                p.Content?.Status ?? p.Candidate.ContentStatus,
                p.Candidate.ContactId is { } cid && contactNames.TryGetValue(cid, out var info) ? info.Name : null,
                p.Candidate.ContactId is { } cid2 && contactNames.TryGetValue(cid2, out var info2) ? info2.Specialty : null,
                p.Content?.Items ?? p.Candidate.Content?.Items ?? Array.Empty<VisitContentItem>(),
                WeekStart: g.PeriodWeeks[p.WeekNumber].WeekStart,
                FrequencyStatus: g.Frequency.TryGetValue(p.Candidate.TargetId, out var f1) ? f1.FrequencyStatus : null,
                RequiredVisitCount: g.Frequency.TryGetValue(p.Candidate.TargetId, out var f2) ? f2.RequiredInPeriod : null,
                OverflowProducts: (p.Content?.OverflowProducts ?? p.Candidate.Content?.OverflowProducts ?? Array.Empty<VisitContentOverflow>())
                    .Select(o => new OverflowProductPreview(o.ProductId, o.ProductCode, o.Role, o.Reason))
                    .ToList(),
                ProductWarnings: ProductWarningsOf(p.Candidate.TargetType, p.Content ?? p.Candidate.Content),
                IsPinned: p.IsPinned,
                AutoPinned: p.AutoPinned,
                GroupKey: p.GroupKey,
                IsExtra: p.IsExtra,
                FrequencyDefault: g.Frequency.TryGetValue(p.Candidate.TargetId, out var f5) ? f5.FrequencyDefault : null))
            .ToList();

        // WP-VP-3A (S-1) — the already-written visits of the stored weeks, shown as they are (IsFixed): never re-generated.
        var fixedSlots = g.Fixed
            .Select(v => (Visit: v, Week: IndexOfWeek(g.PeriodWeeks, PlanningWeekCalendar.MondayOf(v.PlannedDate))))
            .Where(x => x.Week >= 0)
            .Select(x => new PlannedSlotPreview(
                x.Visit.Id, x.Week, x.Visit.TargetType, x.Visit.TargetId, x.Visit.AccountId, x.Visit.ContactId,
                x.Visit.AccountContactLinkId, x.Visit.PlannedDate.ToString("yyyy-MM-dd"), x.Visit.PlannedStartTime,
                x.Visit.PlannedEndTime, x.Visit.Slot.SequenceOrder ?? 0, x.Visit.PlannedDurationMinutes ?? 0,
                x.Visit.Content?.JourneyId, x.Visit.Content?.StageId, x.Visit.Content?.StageIndex,
                x.Visit.ContentItems.Count(i => !string.Equals(i.Role, StrategyProductLineRoles.NonPromo, StringComparison.Ordinal)),
                x.Visit.ContentItems.Count(i => string.Equals(i.Role, StrategyProductLineRoles.NonPromo, StringComparison.Ordinal)),
                "fixed",
                x.Visit.ContactId is { } fc && contactNames.TryGetValue(fc, out var fi) ? fi.Name : null,
                x.Visit.ContactId is { } fc2 && contactNames.TryGetValue(fc2, out var fi2) ? fi2.Specialty : null,
                // WP-VP-3C (S-1) — an approved visit's FROZEN product list, exactly as it was written.
                x.Visit.ContentItems.OrderBy(i => i.Order).Select(FromStored).ToList(),
                WeekStart: g.PeriodWeeks[x.Week].WeekStart,
                IsFixed: true,
                FrequencyStatus: g.Frequency.TryGetValue(TargetKeyOf(x.Visit), out var f3) ? f3.FrequencyStatus : null,
                RequiredVisitCount: g.Frequency.TryGetValue(TargetKeyOf(x.Visit), out var f4) ? f4.RequiredInPeriod : null,
                IsExtra: x.Visit.Selection?.Extra == true,
                FrequencyDefault: g.Frequency.TryGetValue(TargetKeyOf(x.Visit), out var f6) ? f6.FrequencyDefault : null))
            .ToList();

        var allSlots = scheduled.Concat(fixedSlots)
            .OrderBy(p => p.WeekNumber).ThenBy(p => p.PlannedDate, StringComparer.Ordinal).ThenBy(p => p.SequenceOrder)
            .ToList();

        // WP-VP-3A — every week of the period with its derived status.
        var weeks = g.PeriodWeeks
            .Select((w, i) =>
            {
                // WP-VP-4A — an old committed plan's week holding written visits reads approved (storedStatus = legacy).
                var stored = LegacyCommittedPlan.WeekOf(
                    g.Session, w, LegacyCommittedPlan.IsLegacy(g.Session) ? g.Fixed : Array.Empty<PlannedVisitEntity>());
                var count = allSlots.Count(s => s.WeekNumber == i);
                return PlanningWeekCalendar.ToDto(w, PlanningWeekCalendar.Derive(w, g.Today, stored, count), count, stored)
                    with { ExtraTargets = PlanningSessionMapper.ExtraTargetsOf(g.Session, w.WeekStart) }; // WP-VP-4L (2)
            })
            .ToList();

        return new VisitPlanPreview(
            g.Session.Id, g.Session.CyclePeriodId, g.Session.ResourceId,
            g.PeriodStart.ToString("yyyy-MM-dd"), g.PeriodEnd.ToString("yyyy-MM-dd"), g.WeekCount,
            allSlots, g.Unscheduled, g.Content, g.TerritoryWarnings, g.SupplyDemand, DateTimeOffset.UtcNow,
            new PlanningCalendarStatusDto(g.Calendar.Status, g.Calendar.ReasonCode, g.Calendar.Reason),
            g.Calendar.NonWorkingDates.OrderBy(d => d).Select(d => d.ToString("yyyy-MM-dd")).ToList(),
            weeks,
            HalfDayDates: g.Calendar.HalfDayDates.Select(d => d.ToString("yyyy-MM-dd")).ToList(),
            Shifted: g.Shifted
                .Select(s => s.ContactId is { } sc && contactNames.TryGetValue(sc, out var si) ? s with { DisplayName = si.Name } : s)
                .ToList(),
            WeekCapacity: g.WeekCapacity
                .Select((w, i) => w with { ProductVisitCounts = ProductVisitCounts(allSlots.Where(s => s.WeekNumber == i)) })
                .ToList(),
            PeriodCapacity: g.PeriodCapacity,
            ProductDistribution: ProductDistribution(allSlots),
            DoctorsWithoutProducts: g.Content
                .Where(c => !c.ConsentBlocked)
                .Count(c => (c.Products?.Count ?? 0) == 0
                            && !allSlots.Any(s => s.ContactId == c.ContactId && (s.ContentItems?.Count ?? 0) > 0)),
            PortfolioStatus: PortfolioStatuses.Undefined,
            Days: g.Days,
            PinOverflow: g.PinOverflow
                .Select(o => o.ContactId is { } oc && contactNames.TryGetValue(oc, out var on) ? o with { DisplayName = on.Name } : o)
                .ToList(),
            PinWarnings: g.PinWarnings,
            VisitModel: g.VisitModel);
    }

    /// <summary>WP-VP-3C — a doctor visit's product warnings: the items' own warnings, and <c>no_products</c> when the
    /// list is empty (K-7a). A pharmacy / account visit carries none.</summary>
    private static IReadOnlyList<string> ProductWarningsOf(string targetType, VisitContentSequenceResult? content)
    {
        if (targetType != PlannedVisitTargetType.Contact || content is null)
        {
            return Array.Empty<string>();
        }

        var items = content.Items ?? Array.Empty<VisitContentItem>();
        return items.Count == 0
            ? new[] { VisitContentSequenceReasonCodes.NoProducts }
            : items.SelectMany(i => i.Warnings).Distinct(StringComparer.Ordinal).ToList();
    }

    /// <summary>WP-VP-3C — per product, the doctors whose plan in the period (any planned or fixed visit) tells it.</summary>
    private static IReadOnlyList<ProductDistributionDto> ProductDistribution(IEnumerable<PlannedSlotPreview> slots)
        => slots
            .Where(s => s.ContactId is not null)
            .SelectMany(s => (s.ContentItems ?? Array.Empty<VisitContentItem>()).Select(i => (Item: i, Contact: s.ContactId!.Value)))
            .GroupBy(x => x.Item.ProductId)
            .Select(g => new ProductDistributionDto(
                g.Key, g.Select(x => x.Item.ProductCode).FirstOrDefault(c => !string.IsNullOrWhiteSpace(c)),
                g.Select(x => x.Contact).Distinct().Count()))
            .OrderByDescending(d => d.DoctorCount).ThenBy(d => d.ProductCode, StringComparer.Ordinal)
            .ToList();

    /// <summary>WP-VP-3C — per product, the week's visits telling it and how many of them as promo.</summary>
    private static IReadOnlyList<ProductVisitCountDto> ProductVisitCounts(IEnumerable<PlannedSlotPreview> weekSlots)
        => weekSlots
            .SelectMany(s => (s.ContentItems ?? Array.Empty<VisitContentItem>())
                .GroupBy(i => i.ProductId).Select(g => g.First()))
            .GroupBy(i => i.ProductId)
            .Select(g => new ProductVisitCountDto(
                g.Key, g.Select(i => i.ProductCode).FirstOrDefault(c => !string.IsNullOrWhiteSpace(c)),
                g.Count(), g.Count(i => i.Role == StrategyProductLineRoles.Promo)))
            .OrderByDescending(c => c.Visits).ThenBy(c => c.ProductCode, StringComparer.Ordinal)
            .ToList();
}

// ─────────────────────────────────────────────────────────────────────────────────────────────────────────────────
// Public engine outcomes — the private Candidate / PlacedVisit / GenerationOutput never escape these.
// ─────────────────────────────────────────────────────────────────────────────────────────────────────────────────

/// <summary>A preview outcome: the transient <see cref="VisitPlanPreview"/> or a validation error.</summary>
public sealed record EngineOutcome(bool Success, string? Error, VisitPlanPreview? Preview)
{
    public static EngineOutcome Ok(VisitPlanPreview preview) => new(true, null, preview);
    public static EngineOutcome Fail(string error) => new(false, error, null);
}

/// <summary>The build result for apply — the fully-formed FU01 atoms + the generation summary. The handler passes the
/// atoms to the atomic unit of work and flips the session using the summary; nothing is written here.</summary>
public sealed record ApplyBuildOutcome(
    bool Success,
    string? Error,
    IReadOnlyList<Diten.CrmService.Domain.Entities.PlannedVisit> Atoms,
    VisitPlanPreview? Preview,
    // WP-VP-3A — a refusal's machine code + status, the approved week's Monday and the visits a reopened week kept.
    string? ErrorCode = null,
    int StatusCode = 400,
    string? WeekStart = null,
    IReadOnlyList<Guid>? KeptVisitIds = null)
{
    public static ApplyBuildOutcome Ok(VisitPlanPreview preview, IReadOnlyList<Diten.CrmService.Domain.Entities.PlannedVisit> atoms)
        => new(true, null, atoms, preview);

    public static ApplyBuildOutcome Fail(string error)
        => new(false, error, Array.Empty<Diten.CrmService.Domain.Entities.PlannedVisit>(), null);

    public static ApplyBuildOutcome Fail(string code, string error, int statusCode)
        => new(false, error, Array.Empty<Diten.CrmService.Domain.Entities.PlannedVisit>(), null, code, statusCode);
}

/// <summary>The build result for re-plan — the UPDATED existing atoms (their slots re-packed) + the generation summary.</summary>
public sealed record ReplanBuildOutcome(
    bool Success,
    string? Error,
    IReadOnlyList<Diten.CrmService.Domain.Entities.PlannedVisit> UpdatedAtoms,
    VisitPlanPreview? Preview)
{
    public static ReplanBuildOutcome Ok(VisitPlanPreview preview, IReadOnlyList<Diten.CrmService.Domain.Entities.PlannedVisit> updated)
        => new(true, null, updated, preview);

    public static ReplanBuildOutcome Fail(string error)
        => new(false, error, Array.Empty<Diten.CrmService.Domain.Entities.PlannedVisit>(), null);
}
