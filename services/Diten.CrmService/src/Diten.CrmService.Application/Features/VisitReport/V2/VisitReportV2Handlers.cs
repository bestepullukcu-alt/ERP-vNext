using System.Globalization;
using Diten.CrmService.Application.Common;
using Diten.CrmService.Application.Common.Models;
using Diten.CrmService.Application.Common.ReferenceValidation;
using Diten.CrmService.Application.Features.DoctorInsights;
using Diten.CrmService.Application.Features.PlannedVisit;
using Diten.CrmService.Application.Features.VisitReport.Contract;
using Diten.CrmService.Domain.Entities;
using Diten.CrmService.Domain.Repositories;
using MediatR;
using VisitReportEntity = Diten.CrmService.Domain.Entities.VisitReport;

namespace Diten.CrmService.Application.Features.VisitReport.V2;

// ---------------- wire ----------------

public sealed record VisitReportOptionDto(string Code, string? Label, IReadOnlyDictionary<string, string>? Labels);

public sealed record VisitReportV2PreviousDto(Guid VisitReportId, DateTimeOffset ExecutedAt,
    IReadOnlyList<VisitReportProductSection> ProductSections, VisitReportDoctorLevel? Doctor);

public sealed record VisitReportV2CompetitorOptionsDto(Guid ProductId, bool Available, IReadOnlyList<CompetitorOptionDto> Items);

public sealed record VisitReportV2PrefillDto(
    Guid PlannedVisitId,
    string ReportKind,
    Guid? ContactId,
    Guid? AccountId,
    Guid? VisitReportId,
    string? ReportStatus,
    int Version,
    string? ExecutionStatus,
    DateTimeOffset? StartedAt,
    DateTimeOffset? FinishedAt,
    bool SampleSigned,
    VisitReportV2Body Body,
    VisitReportV2PreviousDto? Previous,
    DoctorProfileDto? Profile,
    IReadOnlyList<DoctorPotentialDto> Potentials,
    IReadOnlyList<ProductIndicationProfilesDto> IndicationRows,
    IReadOnlyList<VisitReportV2CompetitorOptionsDto> Competitors,
    bool CompetitorsUnavailable,
    IReadOnlyDictionary<string, IReadOnlyList<VisitReportOptionDto>> OptionSets);

/// <summary>A draft / submit answer: on a refused submit (400 <c>report_incomplete</c>) <see cref="Missing"/> lists what
/// is not given; <see cref="Warnings"/> are the side effects that could not run (the report itself is saved).</summary>
public sealed record VisitReportV2SaveResultDto(
    Guid? VisitReportId, int Version, string? ReportStatus, IReadOnlyList<VisitReportMissingDto> Missing, IReadOnlyList<string> Warnings);

public sealed record CompetitorUsageCellDto(Guid ProductId, string Count);

public sealed record CompetitorUsageCountryDto(string Country, string Count);

public sealed record CompetitorUsageItemDto(Guid CompetitorProductId, string DoctorCount, string ReportCount,
    IReadOnlyList<CompetitorUsageCellDto> ByProduct, IReadOnlyList<CompetitorUsageCountryDto> ByCountry);

public sealed record CompetitorUsageDto(int Months, bool MergeResolved, IReadOnlyList<CompetitorUsageItemDto> Items);

// ---------------- requests ----------------

public sealed record GetVisitReportV2PrefillQuery(Guid PlannedVisitId) : IRequest<Response<VisitReportV2PrefillDto>>;

public sealed record SaveVisitReportV2DraftCommand(Guid PlannedVisitId, VisitReportV2Body Body, int? ExpectedVersion)
    : IRequest<Response<VisitReportV2SaveResultDto>>;

public sealed record SubmitVisitReportV2Command(Guid PlannedVisitId, VisitReportV2Body Body, int? ExpectedVersion, string? ReportedByResourceId = null)
    : IRequest<Response<VisitReportV2SaveResultDto>>;

public sealed record AmendVisitReportV2Command(Guid VisitReportId, string? Reason, VisitReportV2Body Body, int? ExpectedVersion, string? ReportedByResourceId = null)
    : IRequest<Response<VisitReportV2SaveResultDto>>;

public sealed record GetCompetitorUsageQuery(IReadOnlyList<Guid> CompetitorIds, int? Months) : IRequest<Response<CompetitorUsageDto>>;

public static class VisitReportV2Errors
{
    public const string Incomplete = "report_incomplete";
    public const string CodeInvalid = "report_code_invalid";
    public const string Invalid = "report_v2_invalid";
    public const string NotV2 = "report_not_v2";
    public const string ReferenceDataUnavailable = "reference_data_unavailable";
}

/// <summary>WP-VW-W4 (W4-BE-a) — the new key: competitor usage counts without any doctor identity (G2).</summary>
public static class VisitReportV2Permissions
{
    public const string ReadAggregate = "crm.visit-report.read-aggregate";
}

// ---------------- the service ----------------

/// <summary>
/// WP-VW-W4 (W4-BE-a, §2.5) — the report v2: pre-fill from the W3 execution (+ previous values, the doctor's profile and
/// potentials with <c>isDue</c>, the indication rows, the MDM competitor options, the option sets), the draft (free),
/// the submit (completeness → <c>missing[]</c>, codes, the 60-minute window, the doctor insights recorder, the journey
/// move by adoption stage, the competitor proposals to MDM) and the amendment with old / new values. Ownership is the
/// planned visit's (own, or <c>crm.planned-visit.read-all</c>); TenantId is server-resolved.
/// </summary>
public sealed class VisitReportV2Service
{
    public const int MaxPrevious = 200;

    private readonly ITenantContext _tenant;
    private readonly IActorContext _actor;
    private readonly ICallerScope _caller;
    private readonly IVisitReportRepository _reports;
    private readonly IPlannedVisitRepository _plans;
    private readonly IVisitExecutionRepository _executions;
    private readonly IReferenceDataValidator _codes;
    private readonly IDoctorProfileRepository _profiles;
    private readonly IDoctorProductPotentialRepository _potentials;
    private readonly IProductPotentialThresholdRepository _thresholds;
    private readonly ProductIndicationProfileReader _indications;
    private readonly IDoctorInsightsRecorder _recorder;
    private readonly IJourneyProgressRepository _progress;
    private readonly IContentEngagementJourneyRepository _journeys;
    private readonly IContactRepository _contacts;
    private readonly IAccountRepository _accounts;
    private readonly IMdmCompetitorClient _competitors;
    private readonly IReferenceDataCatalogReader? _catalog;
    private readonly TimeProvider _clock;

    public VisitReportV2Service(ITenantContext tenant, IActorContext actor, ICallerScope caller, IVisitReportRepository reports,
        IPlannedVisitRepository plans, IVisitExecutionRepository executions, IReferenceDataValidator codes,
        IDoctorProfileRepository profiles, IDoctorProductPotentialRepository potentials, IProductPotentialThresholdRepository thresholds,
        ProductIndicationProfileReader indications, IDoctorInsightsRecorder recorder, IJourneyProgressRepository progress,
        IContentEngagementJourneyRepository journeys, IContactRepository contacts, IAccountRepository accounts,
        IMdmCompetitorClient? competitors = null, IReferenceDataCatalogReader? catalog = null, TimeProvider? clock = null)
    {
        _tenant = tenant;
        _actor = actor;
        _caller = caller;
        _reports = reports;
        _plans = plans;
        _executions = executions;
        _codes = codes;
        _profiles = profiles;
        _potentials = potentials;
        _thresholds = thresholds;
        _indications = indications;
        _recorder = recorder;
        _progress = progress;
        _journeys = journeys;
        _contacts = contacts;
        _accounts = accounts;
        _competitors = competitors ?? new UnavailableMdmCompetitorClient();
        _catalog = catalog;
        _clock = clock ?? TimeProvider.System;
    }

    public Guid? TenantId => _tenant.TenantId;
    public ICallerScope Caller => _caller;
    public DateTimeOffset Now => _clock.GetUtcNow();

    public static Response<T> Fail<T>(string code, string message, int status) => Response<T>.Fail(new[] { message, code }, status);

    /// <summary>The caller's planned visit, else null (another rep's plan is as absent as a missing one).</summary>
    public async Task<Domain.Entities.PlannedVisit?> PlanAsync(Guid tenantId, Guid plannedVisitId, CancellationToken ct)
    {
        var plan = plannedVisitId == Guid.Empty ? null : await _plans.GetByIdAsync(tenantId, plannedVisitId, ct);
        return plan is not null && _caller.MayAccess(PlannedVisitPermissions.ReadAll, plan.Resource.ResourceId) ? plan : null;
    }

    // ---------------- pre-fill ----------------

    public async Task<VisitReportV2PrefillDto> PrefillAsync(Guid tenantId, Domain.Entities.PlannedVisit plan, CancellationToken ct)
    {
        var now = Now;
        var kind = VisitReportV2Rules.KindOf(plan);
        var execution = await _executions.GetByPlannedVisitIdAsync(tenantId, plan.Id, ct);
        var existing = await _reports.GetByPlannedVisitIdAsync(tenantId, plan.Id, ct);
        var body = existing is { } r && r.IsV2() ? VisitReportV2Rules.BodyOf(r) : VisitReportV2Rules.Prefill(plan, execution);
        if (kind == VisitReportKinds.Doctor && body.ProductSections is { } sections)
        {
            foreach (var section in sections) VisitReportV2Rules.ApplyServerFields(section, plan, execution);
        }

        var productIds = (body.ProductSections ?? Array.Empty<VisitReportProductSection>()).Select(s => s.ProductId)
            .Concat((plan.ContentItems ?? new List<PlannedVisitContentItem>()).Select(i => i.ProductId)).Distinct().ToList();
        var market = await MarketAsync(tenantId, plan, ct);

        DoctorProfileDto? profile = null;
        var potentials = new List<DoctorPotentialDto>();
        var indications = new List<ProductIndicationProfilesDto>();
        VisitReportV2PreviousDto? previous = null;
        if (kind == VisitReportKinds.Doctor && plan.ContactId is { } contactId)
        {
            profile = DoctorInsightsReader.ToDto(contactId, await _profiles.GetByContactAsync(tenantId, contactId, ct), now);
            var stored = await _potentials.ListByContactAsync(tenantId, contactId, ct);
            var thresholds = await _thresholds.ListAsync(tenantId, ct);
            potentials.AddRange(productIds.Select(id => DoctorInsightsReader.ToDto(id, stored.FirstOrDefault(p => p.GlobalProductId == id), thresholds, now)));
            foreach (var id in productIds) indications.Add(await _indications.ReadAsync(tenantId, id, ct));
            previous = await PreviousAsync(tenantId, plan, contactId, ct);
        }

        var competitorOptions = new List<VisitReportV2CompetitorOptionsDto>();
        foreach (var id in productIds)
        {
            var lookup = await _competitors.LookupAsync(id, null, market, null, ct);
            competitorOptions.Add(new VisitReportV2CompetitorOptionsDto(id, lookup.Available, lookup.Items));
        }

        return new VisitReportV2PrefillDto(
            plan.Id, kind, plan.ContactId, plan.AccountId, existing?.Id, existing?.ReportStatus, existing?.Version ?? 0,
            execution?.Status, execution?.StartedAt, execution?.FinishedAt,
            execution?.Signature is { Confirmed: true, Context: VisitSignatureContexts.Sample },
            body, previous, profile, potentials, indications, competitorOptions,
            competitorOptions.Count > 0 && competitorOptions.All(c => !c.Available),
            await OptionSetsAsync(kind, ct));
    }

    /// <summary>The doctor's last finalised v2 report by this rep before this visit (own history; never another rep's).</summary>
    private async Task<VisitReportV2PreviousDto?> PreviousAsync(Guid tenantId, Domain.Entities.PlannedVisit plan, Guid contactId, CancellationToken ct)
    {
        var plans = (await _plans.ListByResourceAndContactsAsync(tenantId, plan.Resource.ResourceId, new[] { contactId }, ct))
            .Where(p => p.Id != plan.Id && p.PlannedDate <= plan.PlannedDate)
            .OrderByDescending(p => p.PlannedDate).Take(MaxPrevious).Select(p => p.Id).ToList();
        if (plans.Count == 0) return null;
        var last = (await _reports.ListByPlannedVisitIdsAsync(tenantId, plans, ct))
            .Where(r => r.IsV2() && r.IsFinalised())
            .OrderByDescending(r => r.ExecutedAt)
            .FirstOrDefault();
        return last is null ? null : new VisitReportV2PreviousDto(last.Id, last.ExecutedAt, last.ProductSections, last.Doctor);
    }

    private async Task<string?> MarketAsync(Guid tenantId, Domain.Entities.PlannedVisit plan, CancellationToken ct)
    {
        if (plan.ContactId is { } contactId && await _contacts.GetByIdAsync(tenantId, contactId, ct) is { CountryRef: { Length: > 0 } c }) return c;
        return plan.AccountId is { } accountId && await _accounts.GetByIdAsync(tenantId, accountId, ct) is { CountryRef: { Length: > 0 } a } ? a : null;
    }

    private async Task<IReadOnlyDictionary<string, IReadOnlyList<VisitReportOptionDto>>> OptionSetsAsync(string kind, CancellationToken ct)
    {
        var sets = kind == VisitReportKinds.Pharmacy
            ? new[] { VisitReportV2ReferenceSets.PharmacyMetRole, VisitReportV2ReferenceSets.PharmacyStockLevel, VisitReportV2ReferenceSets.ShelfPosition,
                      VisitReportV2ReferenceSets.SalesTrend, VisitReportV2ReferenceSets.RecommendationLevel, VisitReportV2ReferenceSets.VisitRequestType,
                      VisitReportV2ReferenceSets.CompetitorAdvantage }
            : new[] { VisitReportV2ReferenceSets.VisitSuccess, VisitReportV2ReferenceSets.DoctorCommitment, VisitReportV2ReferenceSets.VisitInterest,
                      VisitReportV2ReferenceSets.VisitRequestType, VisitReportV2ReferenceSets.CompetitorAdvantage, DoctorInsightsReferenceSets.AdoptionStage,
                      DoctorInsightsReferenceSets.ShareBucket, DoctorInsightsReferenceSets.AdoptionTypology, DoctorInsightsReferenceSets.CommunicationStyle,
                      DoctorInsightsReferenceSets.KolLevel, "visit-objection-category" };
        var result = new Dictionary<string, IReadOnlyList<VisitReportOptionDto>>(StringComparer.Ordinal);
        foreach (var set in sets)
        {
            try
            {
                var snapshot = _catalog is null ? null : await _catalog.GetPublishedValuesAsync(set, ct);
                result[set] = snapshot is { IsPublished: true }
                    ? snapshot.Values.Where(v => v.IsActive && !v.IsDeprecated).Select(v => new VisitReportOptionDto(v.ValueCode, v.DisplayName, v.Attributes)).ToList()
                    : Array.Empty<VisitReportOptionDto>();
            }
            catch (Exception ex) when (ex is not OperationCanceledException)
            {
                result[set] = Array.Empty<VisitReportOptionDto>(); // fail-open: the wizard says the list is unavailable
            }
        }

        return result;
    }

    // ---------------- checks ----------------

    /// <summary>The completeness context: planned products, due potentials / total, the products whose competitor list came.</summary>
    public async Task<VisitReportV2CheckContext> CheckContextAsync(Guid tenantId, Domain.Entities.PlannedVisit plan, VisitReportV2Body body, CancellationToken ct)
    {
        var now = Now;
        var kind = VisitReportV2Rules.KindOf(plan);
        var planned = (plan.ContentItems ?? new List<PlannedVisitContentItem>()).Select(i => i.ProductId).ToHashSet();
        var market = await MarketAsync(tenantId, plan, ct);
        var sectionIds = (body.ProductSections ?? Array.Empty<VisitReportProductSection>()).Select(s => s.ProductId).Distinct().ToList();

        var due = new HashSet<Guid>();
        var totalDue = false;
        if (kind == VisitReportKinds.Doctor && plan.ContactId is { } contactId)
        {
            var stored = await _potentials.ListByContactAsync(tenantId, contactId, ct);
            due = sectionIds.Where(id => PotentialRules.IsDue(stored.FirstOrDefault(p => p.GlobalProductId == id)?.AsOf, now)).ToHashSet();
            totalDue = PotentialRules.IsDue((await _profiles.GetByContactAsync(tenantId, contactId, ct))?.TotalPatientsAsOf, now);
        }

        var listed = new HashSet<Guid>();
        if (kind == VisitReportKinds.Doctor)
        {
            foreach (var id in sectionIds)
            {
                if ((await _competitors.LookupAsync(id, null, market, null, ct)).Available) listed.Add(id);
            }
        }

        var pharmacyListed = kind == VisitReportKinds.Pharmacy && planned.FirstOrDefault() is { } probe && probe != Guid.Empty
                             && (await _competitors.LookupAsync(probe, null, market, null, ct)).Available;
        return new VisitReportV2CheckContext(kind, planned, due, totalDue, listed, pharmacyListed);
    }

    /// <summary>Every code of the body is an active value of its BRD set (submit / amend). Null = all valid.</summary>
    public async Task<Response<T>?> CodesAsync<T>(VisitReportV2Body body, CancellationToken ct)
    {
        foreach (var (set, code, path) in VisitReportV2Rules.Codes(body).Distinct())
        {
            var verdict = await _codes.ValidateAsync(set, code, ct);
            if (verdict.Status == ReferenceValidationStatus.SetMissing)
            {
                return Fail<T>(VisitReportV2Errors.ReferenceDataUnavailable, $"The list '{set}' could not be read. Nothing was saved.", 503);
            }

            if (verdict.Status == ReferenceValidationStatus.InvalidValue)
            {
                return Fail<T>(VisitReportV2Errors.CodeInvalid, $"{path}: '{code}' is not an active '{set}' value.", 400);
            }
        }

        return null;
    }

    /// <summary>The server members of every section (steps shown, plan match, suggestion) and the kind's shape.</summary>
    public async Task<VisitReportV2Body> NormalizeAsync(Guid tenantId, Domain.Entities.PlannedVisit plan, VisitReportV2Body body, CancellationToken ct)
    {
        var execution = await _executions.GetByPlannedVisitIdAsync(tenantId, plan.Id, ct);
        foreach (var section in body.ProductSections ?? Array.Empty<VisitReportProductSection>())
        {
            VisitReportV2Rules.ApplyServerFields(section, plan, execution);
        }

        return body;
    }

    // ---------------- side effects of a submit ----------------

    /// <summary>
    /// The product a PHARMACY competitor proposal is filed under: the plan's first product — the same product the pharmacy
    /// competitor list is looked up with (<see cref="CheckContextAsync"/>). MDM requires a product on every proposal; a plan
    /// without products has none (CT, W4 K13 I2: the pharmacy proposals went with an empty id and MDM refused them all).
    /// </summary>
    public static Guid? PharmacyProposalProduct(Domain.Entities.PlannedVisit plan)
        => (plan.ContentItems ?? new List<PlannedVisitContentItem>()).Select(i => i.ProductId).FirstOrDefault(id => id != Guid.Empty) is var first
           && first != Guid.Empty ? first : null;

    /// <summary>Forwards the rep's new competitor proposals to MDM (before the save: the ids land in the same write). A
    /// proposal without a product (a pharmacy plan without products) is not sent: it stays on the report, pending.</summary>
    public async Task<List<string>> ForwardProposalsAsync(VisitReportV2Body body, Guid reportId, Guid? pharmacyProductId, CancellationToken ct)
    {
        var warnings = new List<string>();
        var lists = (body.ProductSections ?? Array.Empty<VisitReportProductSection>()).Select(s => (Product: s.ProductId, s.Competitors)).ToList();
        if (body.Pharmacy is { } pharmacy) lists.Add((pharmacyProductId ?? Guid.Empty, pharmacy.Competitors));
        foreach (var (product, competitors) in lists)
        {
            foreach (var c in competitors.Where(c => c.Proposed && c.CompetitorProductId is null))
            {
                if (product == Guid.Empty)
                {
                    c.ProposalForwarded = false;
                    warnings.Add("competitor_proposal_pending:" + c.NameSnapshot.Trim());
                    continue;
                }

                var result = await _competitors.ProposeAsync(c.NameSnapshot.Trim(), (c.Company ?? string.Empty).Trim(), product, reportId, ct);
                c.ProposalForwarded = result.Available && result.Id is not null;
                c.CompetitorProductId = result.Id;
                if (!c.ProposalForwarded) warnings.Add("competitor_proposal_pending:" + c.NameSnapshot.Trim());
            }
        }

        return warnings;
    }

    /// <summary>The doctor level + per product potential / share to the doctor insights (source <c>report:{id}</c>).</summary>
    public static (DoctorProfileSuggestion? Profile, int? Total, IReadOnlyList<DoctorProductInsightInput> Products) InsightsOf(
        VisitReportV2Body body, VisitReportV2CheckContext context)
    {
        var doctor = body.Doctor;
        var profile = doctor is null ? null : new DoctorProfileSuggestion(doctor.AdoptionTypologyCode, doctor.CommunicationStyleCode, doctor.KolLevelCode);
        var products = (body.ProductSections ?? Array.Empty<VisitReportProductSection>()).Select(s =>
        {
            var asked = context.DuePotentialProductIds.Contains(s.ProductId) || s.Potential?.Updated == true;
            var rows = asked && s.Potential?.NoPatientsInIndications != true
                ? s.Potential?.Rows.Select(r => new DoctorPotentialRowInput(r.IndicationNodeId, r.ProfileNodeId, r.MonthlyPatients)).ToList()
                : null;
            return new DoctorProductInsightInput(s.ProductId, rows, asked && s.Potential?.NoPatientsInIndications == true ? true : null, s.ShareBucketCode);
        }).ToList();
        return (profile, doctor?.TotalMonthlyPatients, products);
    }

    public Task<DoctorInsightsResult> CheckInsightsAsync(Guid tenantId, Guid contactId, VisitReportV2Body body, VisitReportV2CheckContext context, CancellationToken ct)
    {
        var (profile, total, products) = InsightsOf(body, context);
        return _recorder.CheckFromReportAsync(tenantId, contactId, profile, total, products, ct);
    }

    public Task<DoctorInsightsResult> RecordInsightsAsync(Guid tenantId, Guid contactId, Guid reportId, VisitReportV2Body body, VisitReportV2CheckContext context, CancellationToken ct)
    {
        var (profile, total, products) = InsightsOf(body, context);
        return _recorder.RecordFromReportAsync(tenantId, contactId, reportId, _actor.ActorName, profile, total, products, ct);
    }

    /// <summary>
    /// The journey move (K-W4): for every PLANNED product with a journey line, the doctor's progress goes to the first stage
    /// mapped after the reported adoption stage; no mapped stage (or no stage reported) ⇒ <see cref="JourneyProgress.Advance"/>
    /// from the presented stage; mapped but nothing after ⇒ stays. An unplanned product never moves a journey. The same
    /// report never moves the same progress twice (re-submit inside the window).
    /// </summary>
    public async Task<List<string>> AdvanceJourneysAsync(Guid tenantId, Domain.Entities.PlannedVisit plan, VisitReportEntity report, CancellationToken ct)
    {
        var warnings = new List<string>();
        if (plan.ContactId is not { } contactId) return warnings;
        var now = Now;
        foreach (var section in report.ProductSections.Where(s => !s.Unplanned))
        {
            var item = (plan.ContentItems ?? new List<PlannedVisitContentItem>()).FirstOrDefault(i => i.ProductId == section.ProductId);
            if (item is null || item.JourneyId == Guid.Empty) continue;
            var journey = await _journeys.GetByIdAsync(tenantId, item.JourneyId, ct);
            var stages = journey?.OrderedActiveStages() ?? Array.Empty<ContentEngagementJourneyStage>();
            if (stages.Count == 0) continue;

            var progress = await _progress.GetByKeyAsync(tenantId, contactId, section.ProductId, item.JourneyId, ct);
            if (progress?.LastVisitReportId == report.Id) continue;
            var expected = progress?.Version ?? 0;
            progress ??= new JourneyProgress
            {
                TenantId = tenantId, ContactId = contactId, ProductId = section.ProductId, JourneyId = item.JourneyId,
                CurrentStageIndex = item.StageIndex, CreatedBy = _actor.ActorName
            };
            progress.CurrentStageIndex = Math.Clamp(item.StageIndex, 0, stages.Count - 1); // the stage PRESENTED in this visit

            switch (VisitReportV2Rules.JourneyTarget(stages, section.AdoptionStageCode))
            {
                case null:
                    progress.Advance(stages.Count, report.Id, now);
                    break;
                case -1:
                    progress.MoveTo(progress.CurrentStageIndex, stages.Count, report.Id, now);
                    break;
                case { } target:
                    progress.MoveTo(target, stages.Count, report.Id, now);
                    break;
            }

            progress.UpdatedBy = _actor.ActorName;
            if (!await _progress.UpsertAsync(progress, expected, ct)) warnings.Add("journey_progress_conflict:" + section.ProductId.ToString("D"));
        }

        return warnings;
    }

    public IVisitReportRepository Reports => _reports;
    public IVisitExecutionRepository Executions => _executions;
    public IPlannedVisitRepository Plans => _plans;
    public IContactRepository Contacts => _contacts;
    public IAccountRepository Accounts => _accounts;
    public IMdmCompetitorClient Competitors => _competitors;
    public string? Actor => _actor.ActorName;
}

// ---------------- handlers ----------------

/// <summary>The v2 writes (draft, submit, amend). The reads live in <see cref="VisitReportV2QueryHandlers"/> (a query never writes).</summary>
public sealed class VisitReportV2Handlers :
    IRequestHandler<SaveVisitReportV2DraftCommand, Response<VisitReportV2SaveResultDto>>,
    IRequestHandler<SubmitVisitReportV2Command, Response<VisitReportV2SaveResultDto>>,
    IRequestHandler<AmendVisitReportV2Command, Response<VisitReportV2SaveResultDto>>
{
    private static readonly IReadOnlyList<VisitReportMissingDto> NoMissing = Array.Empty<VisitReportMissingDto>();

    private readonly VisitReportV2Service _s;

    public VisitReportV2Handlers(VisitReportV2Service service) => _s = service;

    private static Response<VisitReportV2SaveResultDto> NotFound()
        => VisitReportV2Service.Fail<VisitReportV2SaveResultDto>(VisitReportErrorCodes.PlannedVisitNotFound, "The planned visit does not exist.", 404);

    private static Response<VisitReportV2SaveResultDto> Conflict()
        => VisitReportV2Service.Fail<VisitReportV2SaveResultDto>(VisitReportErrorCodes.ConcurrencyConflict, "The report changed since it was loaded. Reload and try again.", 409);

    public async Task<Response<VisitReportV2SaveResultDto>> Handle(SaveVisitReportV2DraftCommand request, CancellationToken ct)
    {
        if (_s.TenantId is not { } tenantId) return Response<VisitReportV2SaveResultDto>.Fail("Tenant context is required.", 400);
        var plan = await _s.PlanAsync(tenantId, request.PlannedVisitId, ct);
        if (plan is null) return NotFound();
        if (VisitReportValidation.ValidatePlanNotCancelled(plan) is { } cancelled)
        {
            return VisitReportV2Service.Fail<VisitReportV2SaveResultDto>(cancelled.Code ?? VisitReportV2Errors.Invalid, cancelled.Message, cancelled.StatusCode);
        }

        if (VisitReportV2Rules.Shape(request.Body) is { } shape) return VisitReportV2Service.Fail<VisitReportV2SaveResultDto>(VisitReportV2Errors.Invalid, shape, 400);

        var existing = await _s.Reports.GetByPlannedVisitIdAsync(tenantId, plan.Id, ct);
        if (existing is not null && (existing.IsFinalised() || existing.RescheduledToPlannedVisitId is not null))
        {
            return VisitReportV2Service.Fail<VisitReportV2SaveResultDto>(VisitReportErrorCodes.EditWindowClosed,
                "The report is already submitted; correct it with an amendment.", 409);
        }

        if (request.ExpectedVersion is { } expected && expected != (existing?.Version ?? 0)) return Conflict();

        var body = await _s.NormalizeAsync(tenantId, plan, request.Body, ct);
        var now = _s.Now;
        var execution = await _s.Executions.GetByPlannedVisitIdAsync(tenantId, plan.Id, ct);
        var report = existing ?? new VisitReportEntity
        {
            Id = Guid.NewGuid(), TenantId = tenantId, PlannedVisitId = plan.Id, CreatedAt = now, CreatedBy = _s.Actor,
            ReportedByResourceId = plan.Resource.ResourceId, ExecutedAt = execution?.StartedAt ?? now
        };
        var version = report.Version;
        report.ExecutionOutcome = VisitExecutionOutcome.Completed;
        report.ReasonCode = null;
        report.ReportStatus = VisitReportStatus.Draft;
        VisitReportV2Rules.Apply(report, body, VisitReportV2Rules.KindOf(plan));
        report.UpdatedAt = now;
        report.UpdatedBy = _s.Actor;

        if (existing is null)
        {
            await _s.Reports.InsertAsync(report, ct);
            return Response<VisitReportV2SaveResultDto>.Success(new VisitReportV2SaveResultDto(report.Id, report.Version, report.ReportStatus, NoMissing, Array.Empty<string>()), 201);
        }

        return await _s.Reports.ReplaceAsync(report, version, ct)
            ? Response<VisitReportV2SaveResultDto>.Success(new VisitReportV2SaveResultDto(report.Id, report.Version, report.ReportStatus, NoMissing, Array.Empty<string>()))
            : Conflict();
    }

    public async Task<Response<VisitReportV2SaveResultDto>> Handle(SubmitVisitReportV2Command request, CancellationToken ct)
    {
        if (_s.TenantId is not { } tenantId) return Response<VisitReportV2SaveResultDto>.Fail("Tenant context is required.", 400);
        var plan = await _s.PlanAsync(tenantId, request.PlannedVisitId, ct);
        if (plan is null) return NotFound();

        var readAll = _s.Caller.HasPermission(PlannedVisitPermissions.ReadAll);
        var (allowed, reporter) = _s.Caller.ResolveWriteResource(PlannedVisitPermissions.ReadAll, request.ReportedByResourceId);
        if (!allowed)
        {
            return VisitReportV2Service.Fail<VisitReportV2SaveResultDto>(VisitOwnership.ResourceNotCaller, "A visit report can only be submitted by the signed-in resource.", 403);
        }

        if (VisitReportValidation.ValidatePlanNotCancelled(plan) is { } cancelled)
        {
            return VisitReportV2Service.Fail<VisitReportV2SaveResultDto>(cancelled.Code ?? VisitReportV2Errors.Invalid, cancelled.Message, cancelled.StatusCode);
        }

        var execution = await _s.Executions.GetByPlannedVisitIdAsync(tenantId, plan.Id, ct);
        if (Features.VisitExecution.VisitExecutionReportBinding.RefuseSubmit(execution) is { } unfinished)
        {
            return VisitReportV2Service.Fail<VisitReportV2SaveResultDto>(unfinished.Code, unfinished.Message, 409);
        }

        var now = _s.Now;
        if (VisitReportValidation.ValidateDue(plan.PlannedDate, DateOnly.FromDateTime(now.UtcDateTime)) is { } notDue)
        {
            return VisitReportV2Service.Fail<VisitReportV2SaveResultDto>(notDue.Code ?? VisitReportV2Errors.Invalid, notDue.Message, notDue.StatusCode);
        }

        var existing = await _s.Reports.GetByPlannedVisitIdAsync(tenantId, plan.Id, ct);
        if (existing?.RescheduledToPlannedVisitId is not null)
        {
            return VisitReportV2Service.Fail<VisitReportV2SaveResultDto>(Features.VisitWorkspace.VisitWorkspaceErrorCodes.RescheduleAlreadyApplied,
                "This visit was rescheduled and its new visit exists; correct it with an amendment.", 409);
        }

        if (existing?.IsFinalised() != true && VisitReportValidation.ValidateDeadline(plan.PlannedDate, now, readAll) is { } late)
        {
            return VisitReportV2Service.Fail<VisitReportV2SaveResultDto>(late.Code ?? VisitReportV2Errors.Invalid, late.Message, late.StatusCode);
        }

        // The 60-minute rule (unchanged): a finalised report is re-submitted in place only inside the correction window.
        if (existing is not null && existing.IsFinalised() && !existing.IsWithinEditWindow(now))
        {
            return VisitReportV2Service.Fail<VisitReportV2SaveResultDto>(VisitReportErrorCodes.EditWindowClosed,
                "The correction window has closed; file an append-only amendment instead of editing in place.", 409);
        }

        if (request.ExpectedVersion is { } expected && existing is not null && expected != existing.Version) return Conflict();
        if (VisitReportV2Rules.Shape(request.Body) is { } shape) return VisitReportV2Service.Fail<VisitReportV2SaveResultDto>(VisitReportV2Errors.Invalid, shape, 400);

        var body = await _s.NormalizeAsync(tenantId, plan, request.Body, ct);
        var context = await _s.CheckContextAsync(tenantId, plan, body, ct);
        var missing = VisitReportV2Rules.Missing(body, context);
        if (missing.Count > 0)
        {
            return new Response<VisitReportV2SaveResultDto>
            {
                Data = new VisitReportV2SaveResultDto(existing?.Id, existing?.Version ?? 0, existing?.ReportStatus, missing, Array.Empty<string>()),
                Errors = new[] { $"{missing.Count} required answer(s) are missing.", VisitReportV2Errors.Incomplete },
                StatusCode = 400,
                IsSuccessful = false
            };
        }

        if (await _s.CodesAsync<VisitReportV2SaveResultDto>(body, ct) is { } badCode) return badCode;

        var doctorId = context.Kind == VisitReportKinds.Doctor ? plan.ContactId : null;
        if (doctorId is { } contactId && await _s.CheckInsightsAsync(tenantId, contactId, body, context, ct) is { Ok: false } refused)
        {
            return VisitReportV2Service.Fail<VisitReportV2SaveResultDto>(refused.Code ?? VisitReportV2Errors.Invalid, refused.Message ?? "Refused.", refused.Status);
        }

        var report = existing ?? new VisitReportEntity
        {
            Id = Guid.NewGuid(), TenantId = tenantId, PlannedVisitId = plan.Id, CreatedAt = now, CreatedBy = _s.Actor
        };
        var warnings = await _s.ForwardProposalsAsync(body, report.Id, VisitReportV2Service.PharmacyProposalProduct(plan), ct);
        var version = report.Version;
        report.ExecutionOutcome = VisitExecutionOutcome.Completed;
        report.ReasonCode = null;
        report.RescheduleToDate = null;
        report.RescheduleNotes = null;
        report.ReportedByResourceId = reporter ?? existing?.ReportedByResourceId ?? plan.Resource.ResourceId;
        report.ExecutedAt = Features.VisitExecution.VisitExecutionReportBinding.ExecutedAt(execution, existing?.ExecutedAt ?? now);
        VisitReportV2Rules.Apply(report, body, context.Kind);
        report.ReportStatus = VisitReportStatus.Submitted;
        report.SubmittedAt ??= now;
        report.UpdatedAt = now;
        report.UpdatedBy = _s.Actor;

        if (existing is null)
        {
            await _s.Reports.InsertAsync(report, ct);
        }
        else if (!await _s.Reports.ReplaceAsync(report, version, ct))
        {
            return Conflict();
        }

        // After the report is stored: the doctor's insights (source report:{id}) and the journeys.
        if (doctorId is { } doctor)
        {
            var recorded = await _s.RecordInsightsAsync(tenantId, doctor, report.Id, body, context, ct);
            if (!recorded.Ok) warnings.Add("doctor_insights:" + recorded.Code);
            warnings.AddRange(await _s.AdvanceJourneysAsync(tenantId, plan, report, ct));
        }

        return Response<VisitReportV2SaveResultDto>.Success(
            new VisitReportV2SaveResultDto(report.Id, report.Version, report.ReportStatus, NoMissing, warnings), existing is null ? 201 : 200);
    }

    public async Task<Response<VisitReportV2SaveResultDto>> Handle(AmendVisitReportV2Command request, CancellationToken ct)
    {
        if (_s.TenantId is not { } tenantId) return Response<VisitReportV2SaveResultDto>.Fail("Tenant context is required.", 400);
        var reason = VisitReportValidation.Trim(request.Reason);
        if (reason is null || reason.Length > VisitReportLimits.MaxReasonLength)
        {
            return VisitReportV2Service.Fail<VisitReportV2SaveResultDto>(VisitReportErrorCodes.AmendmentReasonRequired, "An amendment reason (≤ 500) is required.", 400);
        }

        var report = await _s.Reports.GetByIdAsync(tenantId, request.VisitReportId, ct);
        var plan = report is null ? null : await _s.PlanAsync(tenantId, report.PlannedVisitId, ct);
        if (report is null || plan is null)
        {
            return VisitReportV2Service.Fail<VisitReportV2SaveResultDto>(VisitReportErrorCodes.ReportNotFound, "Visit report not found.", 404);
        }

        if (!report.IsFinalised())
        {
            return VisitReportV2Service.Fail<VisitReportV2SaveResultDto>(VisitReportErrorCodes.NotFinalised, "Only a submitted report can be amended.", 409);
        }

        if (!report.IsV2()) return VisitReportV2Service.Fail<VisitReportV2SaveResultDto>(VisitReportV2Errors.NotV2, "A v1 report is amended with the v1 body.", 409);
        var (allowed, reporter) = _s.Caller.ResolveWriteResource(PlannedVisitPermissions.ReadAll, request.ReportedByResourceId);
        if (!allowed)
        {
            return VisitReportV2Service.Fail<VisitReportV2SaveResultDto>(VisitOwnership.ResourceNotCaller, "A visit report can only be amended by the signed-in resource.", 403);
        }

        if (request.ExpectedVersion is { } expected && expected != report.Version) return Conflict();
        if (VisitReportV2Rules.Shape(request.Body) is { } shape) return VisitReportV2Service.Fail<VisitReportV2SaveResultDto>(VisitReportV2Errors.Invalid, shape, 400);

        var body = await _s.NormalizeAsync(tenantId, plan, request.Body, ct);
        if (await _s.CodesAsync<VisitReportV2SaveResultDto>(body, ct) is { } badCode) return badCode;

        var warnings = await _s.ForwardProposalsAsync(body, report.Id, VisitReportV2Service.PharmacyProposalProduct(plan), ct);
        var before = VisitReportV2Rules.BodyOf(report);
        var changes = VisitReportV2Rules.DiffBodies(before, body);
        var now = _s.Now;
        var version = report.Version;
        VisitReportV2Rules.Apply(report, body, report.ReportKind ?? VisitReportV2Rules.KindOf(plan));
        report.Amendments.Add(new VisitReportAmendment
        {
            At = now,
            ByResourceId = reporter ?? report.ReportedByResourceId,
            Reason = reason,
            ChangedFields = changes.Select(c => c.Path.Split('.')[0]).Distinct(StringComparer.Ordinal).ToList(),
            Changes = changes.ToList()
        });
        report.ReportStatus = VisitReportStatus.Amended;
        report.AmendedAt = now;
        report.UpdatedAt = now;
        report.UpdatedBy = _s.Actor;
        return await _s.Reports.ReplaceAsync(report, version, ct)
            ? Response<VisitReportV2SaveResultDto>.Success(new VisitReportV2SaveResultDto(report.Id, report.Version, report.ReportStatus, NoMissing, warnings))
            : Conflict();
    }
}

/// <summary>The v2 reads: the pre-fill and the competitor usage counts. Neither writes.</summary>
public sealed class VisitReportV2QueryHandlers :
    IRequestHandler<GetVisitReportV2PrefillQuery, Response<VisitReportV2PrefillDto>>,
    IRequestHandler<GetCompetitorUsageQuery, Response<CompetitorUsageDto>>
{
    private readonly VisitReportV2Service _s;

    public VisitReportV2QueryHandlers(VisitReportV2Service service) => _s = service;

    public async Task<Response<VisitReportV2PrefillDto>> Handle(GetVisitReportV2PrefillQuery request, CancellationToken ct)
    {
        if (_s.TenantId is not { } tenantId) return Response<VisitReportV2PrefillDto>.Fail("Tenant context is required.", 400);
        var plan = await _s.PlanAsync(tenantId, request.PlannedVisitId, ct);
        return plan is null
            ? VisitReportV2Service.Fail<VisitReportV2PrefillDto>(VisitReportErrorCodes.PlannedVisitNotFound, "The planned visit does not exist.", 404)
            : Response<VisitReportV2PrefillDto>.Success(await _s.PrefillAsync(tenantId, plan, ct));
    }

    public async Task<Response<CompetitorUsageDto>> Handle(GetCompetitorUsageQuery request, CancellationToken ct)
    {
        if (_s.TenantId is not { } tenantId) return Response<CompetitorUsageDto>.Fail("Tenant context is required.", 400);
        var months = Math.Clamp(request.Months ?? 3, 1, 24);
        var asked = request.CompetitorIds.Where(id => id != Guid.Empty).Distinct().ToList();
        if (asked.Count == 0 || asked.Count > 100)
        {
            return VisitReportV2Service.Fail<CompetitorUsageDto>(VisitReportV2Errors.Invalid, "competitorIds: 1–100 MDM competitor ids.", 400);
        }

        var since = _s.Now.AddMonths(-months);
        var reports = (await _s.Reports.ListAsync(tenantId, ct)).Where(r => r.TenantId == tenantId && r.IsV2() && r.IsFinalised() && r.ExecutedAt >= since).ToList();
        var uses = reports.SelectMany(r => r.ProductSections.SelectMany(s => s.Competitors.Select(c => (Report: r, Product: (Guid?)s.ProductId, c.CompetitorProductId)))
                .Concat((r.Pharmacy?.Competitors ?? new List<VisitReportCompetitor>()).Select(c => (Report: r, Product: (Guid?)null, c.CompetitorProductId))))
            .Where(u => u.CompetitorProductId is not null)
            .ToList();

        // A merged record counts as its survivor (MDM mergedIntoId); MDM unreachable ⇒ counted as recorded.
        var referenced = uses.Select(u => u.CompetitorProductId!.Value).Concat(asked).Distinct().ToList();
        var survivors = await _s.Competitors.ResolveSurvivorsAsync(referenced, ct);
        Guid Survivor(Guid id) => survivors is not null && survivors.TryGetValue(id, out var s) ? s : id;

        var plans = (await _s.Plans.ListByIdsAsync(tenantId, uses.Select(u => u.Report.PlannedVisitId).Distinct().ToList(), ct)).ToDictionary(p => p.Id);
        var contactIds = plans.Values.Where(p => VisitReportV2Rules.KindOf(p) == VisitReportKinds.Doctor && p.ContactId is not null).Select(p => p.ContactId!.Value).Distinct().ToList();
        var countryOfContact = (await _s.Contacts.ListByIdsAsync(tenantId, contactIds, ct)).ToDictionary(c => c.Id, c => c.CountryRef);
        var countryOfAccount = new Dictionary<Guid, string?>();
        foreach (var accountId in plans.Values.Where(p => p.ContactId is null && p.AccountId is not null).Select(p => p.AccountId!.Value).Distinct())
        {
            countryOfAccount[accountId] = (await _s.Accounts.GetByIdAsync(tenantId, accountId, ct))?.CountryRef;
        }

        string Country(VisitReportEntity r)
        {
            if (!plans.TryGetValue(r.PlannedVisitId, out var p)) return "unknown";
            var c = p.ContactId is { } contact ? countryOfContact.GetValueOrDefault(contact)
                : p.AccountId is { } account ? countryOfAccount.GetValueOrDefault(account) : null;
            return string.IsNullOrWhiteSpace(c) ? "unknown" : c.Trim();
        }

        var items = asked.Select(Survivor).Distinct().Select(target =>
        {
            var mine = uses.Where(u => Survivor(u.CompetitorProductId!.Value) == target).ToList();
            var doctors = mine.Select(u => plans.GetValueOrDefault(u.Report.PlannedVisitId)).Where(p => p is not null && VisitReportV2Rules.KindOf(p) == VisitReportKinds.Doctor && p.ContactId is not null)
                .Select(p => p!.ContactId!.Value).Distinct().Count();
            return new CompetitorUsageItemDto(target, Count(doctors), Count(mine.Select(u => u.Report.Id).Distinct().Count()),
                mine.Where(u => u.Product is not null).GroupBy(u => u.Product!.Value)
                    .Select(g => new CompetitorUsageCellDto(g.Key, Count(g.Select(u => u.Report.Id).Distinct().Count()))).OrderBy(c => c.ProductId).ToList(),
                mine.GroupBy(u => Country(u.Report))
                    .Select(g => new CompetitorUsageCountryDto(g.Key, Count(g.Select(u => u.Report.Id).Distinct().Count()))).OrderBy(c => c.Country, StringComparer.Ordinal).ToList());
        }).ToList();
        return Response<CompetitorUsageDto>.Success(new CompetitorUsageDto(months, survivors is not null, items));
    }

    /// <summary>A count, or "&lt;5" for 1–4 (no small cell identifies a doctor).</summary>
    public static string Count(int n)
        => n is > 0 and < VisitReportV2Limits.MinAggregateCell ? "<" + VisitReportV2Limits.MinAggregateCell : n.ToString(CultureInfo.InvariantCulture);
}
