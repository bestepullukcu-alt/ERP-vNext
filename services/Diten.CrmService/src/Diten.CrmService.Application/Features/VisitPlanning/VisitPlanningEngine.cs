using System.Globalization;
using Diten.CrmService.Application.Common;
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
        IVisitReportRepository? reports = null)
    {
        _reports = reports;
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
        foreach (var placed in placedToWrite)
        {
            atoms.Add(await BuildAtomAsync(session, placed, ++index, cancellationToken));
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
        var fixedVisits = allPlans
            .Where(p => storedIds.Contains(p.Id) && !p.IsCancelled() && !p.IsArchived())
            .OrderBy(p => p.PlannedDate).ThenBy(p => p.Slot.SequenceOrder ?? 0)
            .ToList();
        var doneVisits = await CompletedInPeriodAsync(tenantId, session, allPlans, periodStart, periodEnd, cancellationToken);
        var accountCache = new Dictionary<Guid, AccountEntity?>();
        var candidates = new List<Candidate>();
        var contentPreviews = new List<DoctorContentPreview>();

        foreach (var doctor in assessments)
        {
            var pending = PendingBefore(pendingPlans, doctor.ContactId, horizonStart);
            var content = await ResolveContentAsync(session, doctor.ContactId, pending, at, cancellationToken);
            // WP-VP-2 (B-3) — the play was derived from the doctor's own segments; a non-unique choice is said.
            var play = await _deriver.DerivePlayAsync(doctor.ContactId, at, cancellationToken);
            var contentReasons = string.Equals(play.ReasonCode, VisitProvenanceDeriver.MultiplePlays, StringComparison.Ordinal)
                ? content.ReasonCodes.Append(VisitProvenanceDeriver.MultiplePlays).Distinct(StringComparer.Ordinal).ToList()
                : content.ReasonCodes;

            var duration = content.VisitDurationMinutes > 0
                ? content.VisitDurationMinutes
                : DefaultDuration(capacity);

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
                ContentPending: pending));

            contentPreviews.Add(new DoctorContentPreview(
                doctor.ContactId, doctor.AccountId, content.Status, content.JourneyId, content.StageId,
                content.StageIndex, content.StageDisplayName, content.PromoItemCount, content.NonPromoItemCount,
                duration, contentReasons, doctor.ConsentStatus, doctor.ConsentBlocked, doctor.ConsentReason,
                content.Items ?? Array.Empty<VisitContentItem>()));
        }

        // Pharmacy targets (first-class; report-only duration) + bare account targets (no doctor selected under them).
        var accountsWithDoctor = candidates
            .Where(c => c.AccountId is not null)
            .Select(c => c.AccountId!.Value)
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

            perCandidateWeeks[candidate.TargetId] = FrequencyExtendPlanner
                .Distribute(remaining, targetWeeks.Count)
                .Select(k => targetWeeks[k])
                .ToList();
        }

        // ⑤ PACK + ROUTE — one Optimize call PER DRAFT WEEK over that week's visit subset (cross-day continuous per week).
        var placed = new List<PlacedVisit>();
        var unscheduled = new List<UnscheduledPreview>();

        foreach (var weekIndex in draftWeekIndexes)
        {
            var span = periodWeeks[weekIndex];
            var window = new WeekWindow(span.From > today ? span.From : today, span.To);
            // A target appears once per visit it owes this week (twice only when it owes more visits than draft weeks).
            var weekCandidates = candidates
                .SelectMany(c => Enumerable.Repeat(
                    c, perCandidateWeeks.TryGetValue(c.TargetId, out var w) ? w.Count(x => x == weekIndex) : 0))
                .ToList();
            if (weekCandidates.Count == 0)
            {
                continue;
            }

            var visitRefs = new Dictionary<Guid, Candidate>();
            var routeVisits = new List<RouteVisitInput>(weekCandidates.Count);
            foreach (var candidate in weekCandidates)
            {
                var visitRef = Guid.NewGuid();
                visitRefs[visitRef] = candidate;
                routeVisits.Add(new RouteVisitInput(
                    visitRef, candidate.Lat, candidate.Long, Math.Max(1, candidate.DurationMinutes),
                    candidate.Windows, candidate.TargetId));
            }

            var input = new RouteOptimizationInput(
                routeVisits,
                new RepWorkingHours(null, ResolveStartLocation(options)),
                new OptimizationPeriod(window.From, window.To, nonWorking),
                betweenVisit,
                new TravelModelSpec(),
                // Manual sequence (target ids) applies WITHIN this week's visit set; null ⇒ the greedy optimum. Frequency
                // is preserved — the same target may recur in another week; each week is ordered independently here.
                options.ManualVisitOrder);

            var output = _optimizer.Optimize(input);

            foreach (var scheduled in output.Scheduled)
            {
                if (!visitRefs.TryGetValue(scheduled.VisitId, out var candidate))
                {
                    continue;
                }

                placed.Add(new PlacedVisit(
                    candidate, weekIndex, scheduled.AssignedDate, scheduled.StartTime, scheduled.EndTime,
                    scheduled.SequenceOrder));
            }

            foreach (var missed in output.Unscheduled)
            {
                if (!visitRefs.TryGetValue(missed.VisitId, out var candidate))
                {
                    continue;
                }

                unscheduled.Add(new UnscheduledPreview(
                    weekIndex, candidate.TargetType, candidate.TargetId, candidate.ContactId, missed.Reason));
            }
        }

        // WP-SB-3b — a doctor recurs across weeks (frequency-extend), so each placed visit tells what comes AFTER the
        // doctor's earlier visits: the stored pending plans before its date plus this run's earlier visits.
        placed = await ProjectContentAsync(session, placed, pendingPlans, at, cancellationToken);

        // ⑥ SUPPLY-vs-DEMAND — TRANSIENT summary (warning, never a block). WP-VP-3A — fixed visits are demand too.
        var supplyDemand = await BuildSupplyDemandAsync(
            capacity, period, placed.Count + fixedVisits.Count, unscheduled.Count, cancellationToken);

        // WP-VP-3A — the doctor's cadence travels on the candidate preview too (3D shows it).
        var contentWithFrequency = contentPreviews
            .Select(c => frequencyByTarget.TryGetValue(c.ContactId, out var f)
                ? c with { FrequencyStatus = f.FrequencyStatus, RequiredVisitCount = f.RequiredInPeriod }
                : c)
            .ToList();

        return GenerationResult.Succeeded(new GenerationOutput(
            session, period, periodStart, periodEnd, periodWeeks.Count, placed, unscheduled, contentWithFrequency,
            territoryWarnings, supplyDemand, calendar, periodWeeks, today, fixedVisits, frequencyByTarget));
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
                DecidedBy = actor
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
        CancellationToken cancellationToken)
        => await _content.ResolveAsync(
            new VisitContentSequenceRequest(
                SubjectType: PlannedVisitTargetType.Contact,
                SubjectId: contactId,
                SegmentId: null,
                StrategyTemplateId: (await _deriver.DerivePlayAsync(contactId, at, cancellationToken)).StrategyTemplateId,
                CyclePeriodId: session.CyclePeriodId,
                PriorStageIndex: null, // WP-SB-3b — the stage comes from JourneyProgress (+ pending), not the last plan
                EffectiveAt: at,
                PendingExposures: pending),
            cancellationToken);

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
        DateTimeOffset at, CancellationToken cancellationToken)
    {
        var earlierInRun = new Dictionary<Guid, List<(Guid ProductId, Guid JourneyId)>>();
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
            var content = pending.SequenceEqual(visit.Candidate.ContentPending ?? Array.Empty<VisitContentPendingExposure>())
                ? baseline
                : await ResolveContentAsync(session, contactId, pending, at, cancellationToken);

            told.AddRange((content.Items ?? Array.Empty<VisitContentItem>()).Select(i => (i.ProductId, i.JourneyId)));
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
        Warnings = item.Warnings.ToList()
    };

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

    private sealed record WeekWindow(DateOnly From, DateOnly To);

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
        IReadOnlyList<VisitContentPendingExposure>? ContentPending);

    /// <summary><see cref="Content"/> — WP-SB-3b: the visit's own content (projected over the doctor's earlier visits);
    /// null for a non-doctor visit.</summary>
    private sealed record PlacedVisit(
        Candidate Candidate,
        int WeekNumber,
        DateOnly Date,
        string StartTime,
        string EndTime,
        int SequenceOrder,
        VisitContentSequenceResult? Content = null);

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
        IReadOnlyDictionary<Guid, FrequencyRequirement> Frequency);

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

        return ToPreview(g, names);
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
                RequiredVisitCount: g.Frequency.TryGetValue(p.Candidate.TargetId, out var f2) ? f2.RequiredInPeriod : null))
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
                null,
                WeekStart: g.PeriodWeeks[x.Week].WeekStart,
                IsFixed: true,
                FrequencyStatus: g.Frequency.TryGetValue(TargetKeyOf(x.Visit), out var f3) ? f3.FrequencyStatus : null,
                RequiredVisitCount: g.Frequency.TryGetValue(TargetKeyOf(x.Visit), out var f4) ? f4.RequiredInPeriod : null))
            .ToList();

        var allSlots = scheduled.Concat(fixedSlots)
            .OrderBy(p => p.WeekNumber).ThenBy(p => p.PlannedDate, StringComparer.Ordinal).ThenBy(p => p.SequenceOrder)
            .ToList();

        // WP-VP-3A — every week of the period with its derived status.
        var weeks = g.PeriodWeeks
            .Select((w, i) =>
            {
                var stored = g.Session.WeekOf(w.WeekStart);
                var count = allSlots.Count(s => s.WeekNumber == i);
                return PlanningWeekCalendar.ToDto(w, PlanningWeekCalendar.Derive(w, g.Today, stored, count), count, stored);
            })
            .ToList();

        return new VisitPlanPreview(
            g.Session.Id, g.Session.CyclePeriodId, g.Session.ResourceId,
            g.PeriodStart.ToString("yyyy-MM-dd"), g.PeriodEnd.ToString("yyyy-MM-dd"), g.WeekCount,
            allSlots, g.Unscheduled, g.Content, g.TerritoryWarnings, g.SupplyDemand, DateTimeOffset.UtcNow,
            new PlanningCalendarStatusDto(g.Calendar.Status, g.Calendar.ReasonCode, g.Calendar.Reason),
            g.Calendar.NonWorkingDates.OrderBy(d => d).Select(d => d.ToString("yyyy-MM-dd")).ToList(),
            weeks);
    }
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
