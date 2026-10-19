using System.Globalization;
using Diten.CrmService.Application.Common;
using Diten.CrmService.Application.Common.Models;
using Diten.CrmService.Application.Features.StrategyTemplate.Binding;
using Diten.CrmService.Domain.Entities;
using Diten.CrmService.Domain.Repositories;
using MediatR;

namespace Diten.CrmService.Application.Features.DoctorInsights;

// WP-VW-W4 (W4-BE-c, §2.2–2.4) — api/crm/doctor-insights: the doctor profile (+ history, + the availability and the
// preferred channel / visit window READ from their owners), the product potentials (isDue, class, opportunity, the
// threshold used), a product's indication × profile rows (Concept Graph), the thresholds (+ history), the opportunity
// list (own / team) and the marketing aggregate (no doctor identity, cells under 5 hidden). TenantId is server-resolved;
// a doctor the caller may not see answers 404 like a missing one.

// ---------------- wire ----------------

public sealed record DoctorProfileDto(
    Guid ContactId, string? AdoptionTypologyCode, string? CommunicationStyleCode, string? KolLevelCode,
    int? TotalMonthlyPatients, DateTimeOffset? TotalPatientsAsOf, bool TotalIsDue, int Version, string? UpdatedBy, DateTimeOffset? UpdatedAt);

public sealed record DoctorInsightChangeDto(DateTimeOffset At, string? By, string Field, string? OldValue, string? NewValue, string Source, Guid? ProductId = null);

public sealed record DoctorAvailabilityDto(
    string Weekday, string StartTime, string EndTime, string AvailabilityType, string? PreferredVisitStartTime,
    string? PreferredVisitEndTime, bool AppointmentRequired, string? PreferredContactMethod);

public sealed record DoctorPreferenceDto(string PreferenceType, string Channel, string PreferenceValue, int Priority);

public sealed record DoctorProfileReadDto(
    DoctorProfileDto Profile,
    IReadOnlyList<DoctorInsightChangeDto> History,
    IReadOnlyList<DoctorAvailabilityDto> Availability,
    IReadOnlyList<DoctorPreferenceDto> Preferences,
    IReadOnlyDictionary<string, string>? DisplayNames);

public sealed record DoctorProfileInput(string? AdoptionTypologyCode, string? CommunicationStyleCode, string? KolLevelCode,
    int? TotalMonthlyPatients, int? ExpectedVersion = null);

public sealed record DoctorPotentialRowDto(Guid IndicationNodeId, Guid? ProfileNodeId, int MonthlyPatients);

public sealed record DoctorPotentialDto(
    Guid ProductId, IReadOnlyList<DoctorPotentialRowDto> IndicationRows, bool NoPatientsInIndications, DateTimeOffset? AsOf,
    bool IsDue, string? ShareBucketCode, DateTimeOffset? ShareAsOf, int? IndicationTotal, string? PotentialClass,
    string? OpportunityCode, EffectiveThreshold Threshold, int Version);

public sealed record DoctorPotentialsDto(Guid ContactId, int? TotalMonthlyPatients, bool TotalIsDue, IReadOnlyList<DoctorPotentialDto> Items);

public sealed record PotentialThresholdDto(Guid? ProductId, int LowBelow, int HighAbove, string Source, int Version, string? UpdatedBy, DateTimeOffset? UpdatedAt);

public sealed record PotentialThresholdsDto(PotentialThresholdDto Default, IReadOnlyList<PotentialThresholdDto> Products);

public sealed record PotentialThresholdInput(int? LowBelow, int? HighAbove, int? ExpectedVersion = null);

public sealed record PotentialThresholdChangeDto(DateTimeOffset At, string? By, Guid? ProductId, string Field, string? OldValue, string? NewValue);

public sealed record PotentialThresholdHistoryDto(IReadOnlyList<PotentialThresholdChangeDto> Items, long Total, int Page, int PageSize,
    IReadOnlyDictionary<string, string>? DisplayNames);

public sealed record DoctorOpportunityDto(Guid ContactId, string? DisplayName, Guid ProductId, int? IndicationTotal, string? PotentialClass,
    string? ShareBucketCode, string OpportunityCode, bool IsDue);

public sealed record DoctorOpportunitiesDto(string Scope, IReadOnlyList<DoctorOpportunityDto> Items);

/// <summary>A count on the aggregate: the number, or <c>"&lt;5"</c> for 1–4 doctors (never a doctor's identity).</summary>
public sealed record DoctorAggregateCellDto(string PotentialClass, string ShareBucketCode, string Count);

public sealed record DoctorAggregateOpportunityDto(string OpportunityCode, string Count);

public sealed record DoctorInsightAggregatesDto(Guid? ProductId, string DoctorCount, IReadOnlyList<DoctorAggregateCellDto> Cells,
    IReadOnlyList<DoctorAggregateOpportunityDto> Opportunities);

// ---------------- requests ----------------

public sealed record GetDoctorProfileQuery(Guid ContactId) : IRequest<Response<DoctorProfileReadDto>>;

public sealed record UpdateDoctorProfileCommand(Guid ContactId, DoctorProfileInput Input) : IRequest<Response<DoctorProfileReadDto>>;

public sealed record GetDoctorPotentialsQuery(Guid ContactId, IReadOnlyList<Guid>? ProductIds) : IRequest<Response<DoctorPotentialsDto>>;

public sealed record GetProductIndicationProfilesQuery(Guid ProductId) : IRequest<Response<ProductIndicationProfilesDto>>;

public sealed record GetPotentialThresholdsQuery : IRequest<Response<PotentialThresholdsDto>>;

/// <summary><see cref="ProductId"/> null = the tenant default.</summary>
public sealed record UpdatePotentialThresholdCommand(Guid? ProductId, PotentialThresholdInput Input) : IRequest<Response<PotentialThresholdDto>>;

public sealed record GetPotentialThresholdHistoryQuery(int? Page = null, int? PageSize = null) : IRequest<Response<PotentialThresholdHistoryDto>>;

public sealed record ListDoctorOpportunitiesQuery(string? Scope, Guid? ProductId) : IRequest<Response<DoctorOpportunitiesDto>>;

public sealed record GetDoctorInsightAggregatesQuery(Guid? ProductId) : IRequest<Response<DoctorInsightAggregatesDto>>;

// ---------------- the shared read plumbing ----------------

public sealed class DoctorInsightsReader
{
    public const int HistoryTake = 50;
    public const int MaxOpportunities = 500;

    private readonly ITenantContext _tenant;
    private readonly DoctorInsightsVisibility _visibility;
    private readonly IContactRepository _contacts;
    private readonly IDoctorProfileRepository _profiles;
    private readonly IDoctorProfileChangeRepository _profileHistory;
    private readonly IDoctorProductPotentialRepository _potentials;
    private readonly IDoctorProductPotentialChangeRepository _potentialHistory;
    private readonly IProductPotentialThresholdRepository _thresholds;
    private readonly IContactAvailabilityRepository _availability;
    private readonly IPreferenceRecordRepository _preferences;
    private readonly IUserDisplayNameResolver? _names;
    private readonly TimeProvider _clock;

    public DoctorInsightsReader(ITenantContext tenant, DoctorInsightsVisibility visibility, IContactRepository contacts,
        IDoctorProfileRepository profiles, IDoctorProfileChangeRepository profileHistory, IDoctorProductPotentialRepository potentials,
        IDoctorProductPotentialChangeRepository potentialHistory, IProductPotentialThresholdRepository thresholds,
        IContactAvailabilityRepository availability, IPreferenceRecordRepository preferences,
        IUserDisplayNameResolver? names = null, TimeProvider? clock = null)
    {
        _tenant = tenant;
        _visibility = visibility;
        _contacts = contacts;
        _profiles = profiles;
        _profileHistory = profileHistory;
        _potentials = potentials;
        _potentialHistory = potentialHistory;
        _thresholds = thresholds;
        _availability = availability;
        _preferences = preferences;
        _names = names;
        _clock = clock ?? TimeProvider.System;
    }

    public Guid? TenantId => _tenant.TenantId;
    public DoctorInsightsVisibility Visibility => _visibility;
    public DateTimeOffset Now => _clock.GetUtcNow();

    public static Response<T> NoTenant<T>() => Response<T>.Fail("Tenant context is required.", 400);

    public static Response<T> NotFound<T>()
        => DoctorInsightsResponses.Fail<T>(DoctorInsightsErrors.NotFound, "The doctor does not exist or is not in your scope.", 404);

    /// <summary>The doctor exists in the tenant AND the caller may see them; anything else is the same 404.</summary>
    public async Task<bool> VisibleAsync(Guid tenantId, Guid contactId, CancellationToken ct)
    {
        var contact = await _contacts.GetByIdAsync(tenantId, contactId, ct);
        if (contact is null || contact.TenantId != tenantId || contact.IsDeleted) return false;
        var scope = await _visibility.ScopeAsync(tenantId, ct);
        return await _visibility.CanSeeAsync(tenantId, scope, contactId, ct);
    }

    public async Task<DoctorProfileReadDto> ProfileAsync(Guid tenantId, Guid contactId, CancellationToken ct)
    {
        var now = Now;
        var profile = await _profiles.GetByContactAsync(tenantId, contactId, ct);
        var history = (await _profileHistory.ListByContactAsync(tenantId, contactId, HistoryTake, ct))
            .Select(h => new DoctorInsightChangeDto(h.At, h.By, h.Field, h.OldValue, h.NewValue, h.Source))
            .Concat((await _potentialHistory.ListByContactAsync(tenantId, contactId, HistoryTake, ct))
                .Select(h => new DoctorInsightChangeDto(h.At, h.By, h.Field, h.OldValue, h.NewValue, h.Source, h.GlobalProductId)))
            .OrderByDescending(h => h.At)
            .Take(HistoryTake)
            .ToList();

        // Read from their owners, never copied (§2.2).
        var availability = (await _availability.ListByContactAsync(tenantId, contactId, ct))
            .Where(a => !a.IsDeleted && !AvailabilityLifecycle.IsClosed(a.Status)
                        && (a.EffectiveFrom is null || a.EffectiveFrom <= now) && (a.EffectiveTo is null || now <= a.EffectiveTo))
            .OrderBy(a => AvailabilityWeekday.All.ToList().IndexOf(a.Weekday)).ThenBy(a => a.StartTime, StringComparer.Ordinal)
            .Select(a => new DoctorAvailabilityDto(a.Weekday, a.StartTime, a.EndTime, a.AvailabilityType, a.Preference.PreferredVisitStartTime,
                a.Preference.PreferredVisitEndTime, a.Preference.AppointmentRequired, a.Preference.PreferredContactMethod))
            .ToList();
        var preferences = (await _preferences.ListForEvaluationAsync(tenantId, ConsentSubjectType.Contact, contactId, ct))
            .Where(p => !p.IsDeleted && !p.IsArchived() && p.IsEffectiveAt(now)
                        && (p.PreferenceType == PreferenceType.PreferredChannel || p.PreferenceType == PreferenceType.PreferredVisitWindow))
            .OrderBy(p => p.PreferenceType, StringComparer.Ordinal).ThenBy(p => p.Priority)
            .Select(p => new DoctorPreferenceDto(p.PreferenceType, p.Channel, p.PreferenceValue, p.Priority))
            .ToList();

        var names = await UserDisplayNames.MapAsync(_names, history.Select(h => h.By).Append(profile?.UpdatedBy), ct);
        return new DoctorProfileReadDto(ToDto(contactId, profile, now), history, availability, preferences, names);
    }

    public static DoctorProfileDto ToDto(Guid contactId, DoctorProfile? p, DateTimeOffset now)
        => new(contactId, p?.AdoptionTypologyCode, p?.CommunicationStyleCode, p?.KolLevelCode, p?.TotalMonthlyPatients,
            p?.TotalPatientsAsOf, PotentialRules.IsDue(p?.TotalPatientsAsOf, now), p?.Version ?? 0, p?.UpdatedBy, p?.UpdatedAt);

    public Task<IReadOnlyList<ProductPotentialThreshold>> ThresholdsAsync(Guid tenantId, CancellationToken ct) => _thresholds.ListAsync(tenantId, ct);

    public static DoctorPotentialDto ToDto(Guid productId, DoctorProductPotential? p, IReadOnlyList<ProductPotentialThreshold> thresholds, DateTimeOffset now)
    {
        var threshold = PotentialRules.Resolve(thresholds, productId);
        var total = p?.IndicationTotal();
        var potentialClass = PotentialRules.Classify(total, threshold);
        return new DoctorPotentialDto(
            productId,
            (p?.IndicationRows ?? new List<DoctorPotentialRow>()).Select(r => new DoctorPotentialRowDto(r.IndicationNodeId, r.ProfileNodeId, r.MonthlyPatients)).ToList(),
            p?.NoPatientsInIndications ?? false, p?.AsOf, PotentialRules.IsDue(p?.AsOf, now), p?.ShareBucketCode, p?.ShareAsOf, total,
            potentialClass, OpportunityRules.Classify(potentialClass, p?.ShareBucketCode), threshold, p?.Version ?? 0);
    }

    public async Task<DoctorPotentialsDto> PotentialsAsync(Guid tenantId, Guid contactId, IReadOnlyList<Guid>? productIds, CancellationToken ct)
    {
        var now = Now;
        var stored = await _potentials.ListByContactAsync(tenantId, contactId, ct);
        var thresholds = await _thresholds.ListAsync(tenantId, ct);
        var products = productIds is { Count: > 0 } asked ? asked.Distinct().ToList() : stored.Select(p => p.GlobalProductId).Distinct().ToList();
        var profile = await _profiles.GetByContactAsync(tenantId, contactId, ct);
        return new DoctorPotentialsDto(contactId, profile?.TotalMonthlyPatients, PotentialRules.IsDue(profile?.TotalPatientsAsOf, now),
            products.Select(id => ToDto(id, stored.FirstOrDefault(p => p.GlobalProductId == id), thresholds, now)).ToList());
    }

    public IDoctorProductPotentialRepository Potentials => _potentials;
    public IContactRepository Contacts => _contacts;
    public IProductPotentialThresholdRepository ThresholdStore => _thresholds;
    public IUserDisplayNameResolver? Names => _names;
}

public static class DoctorInsightsResponses
{
    public static Response<T> Fail<T>(string code, string message, int status) => Response<T>.Fail(new[] { code, message }, status);

    public static Response<T> From<T>(DoctorInsightsResult result)
        => Fail<T>(result.Code ?? DoctorInsightsErrors.Invalid, result.Message ?? "The request was refused.", result.Status);

    /// <summary>The aggregate count: the number, or "&lt;5" for 1–4.</summary>
    public static string Count(int n)
        => n is > 0 and < DoctorInsightsLimits.MinAggregateCell ? "<" + DoctorInsightsLimits.MinAggregateCell : n.ToString(CultureInfo.InvariantCulture);
}

// ---------------- handlers ----------------

public sealed class DoctorInsightsQueryHandlers :
    IRequestHandler<GetDoctorProfileQuery, Response<DoctorProfileReadDto>>,
    IRequestHandler<GetDoctorPotentialsQuery, Response<DoctorPotentialsDto>>,
    IRequestHandler<GetProductIndicationProfilesQuery, Response<ProductIndicationProfilesDto>>,
    IRequestHandler<GetPotentialThresholdsQuery, Response<PotentialThresholdsDto>>,
    IRequestHandler<GetPotentialThresholdHistoryQuery, Response<PotentialThresholdHistoryDto>>,
    IRequestHandler<ListDoctorOpportunitiesQuery, Response<DoctorOpportunitiesDto>>,
    IRequestHandler<GetDoctorInsightAggregatesQuery, Response<DoctorInsightAggregatesDto>>
{
    private readonly DoctorInsightsReader _reader;
    private readonly ProductIndicationProfileReader _indications;
    private readonly IProductPotentialThresholdChangeRepository _thresholdHistory;

    public DoctorInsightsQueryHandlers(DoctorInsightsReader reader, ProductIndicationProfileReader indications,
        IProductPotentialThresholdChangeRepository thresholdHistory)
    {
        _reader = reader;
        _indications = indications;
        _thresholdHistory = thresholdHistory;
    }

    public async Task<Response<DoctorProfileReadDto>> Handle(GetDoctorProfileQuery request, CancellationToken ct)
    {
        if (_reader.TenantId is not { } tenantId) return DoctorInsightsReader.NoTenant<DoctorProfileReadDto>();
        return await _reader.VisibleAsync(tenantId, request.ContactId, ct)
            ? Response<DoctorProfileReadDto>.Success(await _reader.ProfileAsync(tenantId, request.ContactId, ct))
            : DoctorInsightsReader.NotFound<DoctorProfileReadDto>();
    }

    public async Task<Response<DoctorPotentialsDto>> Handle(GetDoctorPotentialsQuery request, CancellationToken ct)
    {
        if (_reader.TenantId is not { } tenantId) return DoctorInsightsReader.NoTenant<DoctorPotentialsDto>();
        return await _reader.VisibleAsync(tenantId, request.ContactId, ct)
            ? Response<DoctorPotentialsDto>.Success(await _reader.PotentialsAsync(tenantId, request.ContactId, request.ProductIds, ct))
            : DoctorInsightsReader.NotFound<DoctorPotentialsDto>();
    }

    public async Task<Response<ProductIndicationProfilesDto>> Handle(GetProductIndicationProfilesQuery request, CancellationToken ct)
    {
        if (_reader.TenantId is not { } tenantId) return DoctorInsightsReader.NoTenant<ProductIndicationProfilesDto>();
        return Response<ProductIndicationProfilesDto>.Success(await _indications.ReadAsync(tenantId, request.ProductId, ct));
    }

    public async Task<Response<PotentialThresholdsDto>> Handle(GetPotentialThresholdsQuery request, CancellationToken ct)
    {
        if (_reader.TenantId is not { } tenantId) return DoctorInsightsReader.NoTenant<PotentialThresholdsDto>();
        var stored = await _reader.ThresholdsAsync(tenantId, ct);
        var tenantDefault = stored.FirstOrDefault(t => t.GlobalProductId is null);
        var defaultDto = tenantDefault is null
            ? new PotentialThresholdDto(null, ProductPotentialThresholdDefaults.LowBelow, ProductPotentialThresholdDefaults.HighAbove, ThresholdSources.BuiltIn, 0, null, null)
            : ToDto(tenantDefault, ThresholdSources.TenantDefault);
        return Response<PotentialThresholdsDto>.Success(new PotentialThresholdsDto(defaultDto,
            stored.Where(t => t.GlobalProductId is not null).OrderBy(t => t.GlobalProductId).Select(t => ToDto(t, ThresholdSources.Product)).ToList()));
    }

    public static PotentialThresholdDto ToDto(ProductPotentialThreshold t, string source)
        => new(t.GlobalProductId, t.LowBelow, t.HighAbove, source, t.Version, t.UpdatedBy, t.UpdatedAt);

    public async Task<Response<PotentialThresholdHistoryDto>> Handle(GetPotentialThresholdHistoryQuery request, CancellationToken ct)
    {
        if (_reader.TenantId is not { } tenantId) return DoctorInsightsReader.NoTenant<PotentialThresholdHistoryDto>();
        var page = Math.Max(1, request.Page ?? 1);
        var size = Math.Clamp(request.PageSize ?? 50, 1, 200);
        var (items, total) = await _thresholdHistory.ListAsync(tenantId, (page - 1) * size, size, ct);
        var names = await UserDisplayNames.MapAsync(_reader.Names, items.Select(i => i.By), ct);
        return Response<PotentialThresholdHistoryDto>.Success(new PotentialThresholdHistoryDto(
            items.Select(i => new PotentialThresholdChangeDto(i.At, i.By, i.GlobalProductId, i.Field, i.OldValue, i.NewValue)).ToList(),
            total, page, size, names));
    }

    public async Task<Response<DoctorOpportunitiesDto>> Handle(ListDoctorOpportunitiesQuery request, CancellationToken ct)
    {
        if (_reader.TenantId is not { } tenantId) return DoctorInsightsReader.NoTenant<DoctorOpportunitiesDto>();
        var kind = string.IsNullOrWhiteSpace(request.Scope) ? DoctorScopeKinds.Mine : request.Scope.Trim().ToLowerInvariant();
        if (kind is not (DoctorScopeKinds.Mine or DoctorScopeKinds.Team))
        {
            return DoctorInsightsResponses.Fail<DoctorOpportunitiesDto>(DoctorInsightsErrors.Invalid, "scope is 'mine' or 'team'.", 400);
        }

        var scope = await _reader.Visibility.ScopeAsync(tenantId, ct);
        var now = _reader.Now;
        var potentials = scope.All
            ? await _reader.Potentials.ListAsync(tenantId, request.ProductId, ct)
            : await _reader.Potentials.ListByContactsAsync(tenantId,
                await _reader.Visibility.ContactIdsAsync(tenantId, kind == DoctorScopeKinds.Team ? scope.TeamNodes.ToList() : scope.OwnNodes.ToList(), ct),
                request.ProductId, ct);
        var thresholds = await _reader.ThresholdsAsync(tenantId, ct);

        var rows = potentials
            .Select(p => (Potential: p, Dto: DoctorInsightsReader.ToDto(p.GlobalProductId, p, thresholds, now)))
            .Where(x => x.Dto.OpportunityCode is not null)
            .OrderBy(x => OpportunityRules.Rank(x.Dto.OpportunityCode)).ThenByDescending(x => x.Dto.IndicationTotal ?? 0)
            .Take(DoctorInsightsReader.MaxOpportunities)
            .ToList();
        var contacts = (await _reader.Contacts.ListByIdsAsync(tenantId, rows.Select(r => r.Potential.ContactId).Distinct().ToList(), ct))
            .ToDictionary(c => c.Id);
        return Response<DoctorOpportunitiesDto>.Success(new DoctorOpportunitiesDto(kind, rows
            .Where(r => contacts.ContainsKey(r.Potential.ContactId))
            .Select(r => new DoctorOpportunityDto(r.Potential.ContactId, Name(contacts[r.Potential.ContactId]), r.Potential.GlobalProductId,
                r.Dto.IndicationTotal, r.Dto.PotentialClass, r.Dto.ShareBucketCode, r.Dto.OpportunityCode!, r.Dto.IsDue))
            .ToList()));
    }

    private static string? Name(Domain.Entities.Contact c)
        => !string.IsNullOrWhiteSpace(c.DisplayName) ? c.DisplayName.Trim()
            : string.Join(' ', new[] { c.FirstName, c.LastName }.Where(v => !string.IsNullOrWhiteSpace(v))).Trim() is { Length: > 0 } n ? n : null;

    public async Task<Response<DoctorInsightAggregatesDto>> Handle(GetDoctorInsightAggregatesQuery request, CancellationToken ct)
    {
        if (_reader.TenantId is not { } tenantId) return DoctorInsightsReader.NoTenant<DoctorInsightAggregatesDto>();
        var now = _reader.Now;
        var thresholds = await _reader.ThresholdsAsync(tenantId, ct);
        var rows = (await _reader.Potentials.ListAsync(tenantId, request.ProductId, ct))
            .Where(p => p.TenantId == tenantId)
            .Select(p => (p.ContactId, Dto: DoctorInsightsReader.ToDto(p.GlobalProductId, p, thresholds, now)))
            .ToList();

        var cells = PotentialClasses.All
            .SelectMany(c => ShareBuckets.All.Select(s => new DoctorAggregateCellDto(c, s,
                DoctorInsightsResponses.Count(rows.Count(r => r.Dto.PotentialClass == c && r.Dto.ShareBucketCode == s)))))
            .ToList();
        var opportunities = OpportunityCodes.All
            .Select(o => new DoctorAggregateOpportunityDto(o, DoctorInsightsResponses.Count(rows.Count(r => r.Dto.OpportunityCode == o))))
            .ToList();
        return Response<DoctorInsightAggregatesDto>.Success(new DoctorInsightAggregatesDto(
            request.ProductId, DoctorInsightsResponses.Count(rows.Select(r => r.ContactId).Distinct().Count()), cells, opportunities));
    }
}

public sealed class DoctorInsightsCommandHandlers :
    IRequestHandler<UpdateDoctorProfileCommand, Response<DoctorProfileReadDto>>,
    IRequestHandler<UpdatePotentialThresholdCommand, Response<PotentialThresholdDto>>
{
    private readonly DoctorInsightsReader _reader;
    private readonly DoctorInsightsRecorder _recorder;
    private readonly IActorContext _actor;
    private readonly IProductPotentialThresholdChangeRepository _thresholdHistory;
    private readonly IStrategyTemplateProductReferenceValidator? _products;

    public DoctorInsightsCommandHandlers(DoctorInsightsReader reader, DoctorInsightsRecorder recorder, IActorContext actor,
        IProductPotentialThresholdChangeRepository thresholdHistory, IStrategyTemplateProductReferenceValidator? products = null)
    {
        _reader = reader;
        _recorder = recorder;
        _actor = actor;
        _thresholdHistory = thresholdHistory;
        _products = products;
    }

    public async Task<Response<DoctorProfileReadDto>> Handle(UpdateDoctorProfileCommand request, CancellationToken ct)
    {
        if (_reader.TenantId is not { } tenantId) return DoctorInsightsReader.NoTenant<DoctorProfileReadDto>();
        if (!await _reader.VisibleAsync(tenantId, request.ContactId, ct)) return DoctorInsightsReader.NotFound<DoctorProfileReadDto>();

        var input = request.Input;
        var result = await _recorder.UpdateProfileAsync(tenantId, request.ContactId,
            new DoctorProfileSuggestion(input.AdoptionTypologyCode, input.CommunicationStyleCode, input.KolLevelCode),
            input.TotalMonthlyPatients, input.ExpectedVersion, _actor.ActorName, ct);
        return result.Ok
            ? Response<DoctorProfileReadDto>.Success(await _reader.ProfileAsync(tenantId, request.ContactId, ct))
            : DoctorInsightsResponses.From<DoctorProfileReadDto>(result);
    }

    public async Task<Response<PotentialThresholdDto>> Handle(UpdatePotentialThresholdCommand request, CancellationToken ct)
    {
        if (_reader.TenantId is not { } tenantId) return DoctorInsightsReader.NoTenant<PotentialThresholdDto>();
        if (request.Input.LowBelow is not { } low || request.Input.HighAbove is not { } high || !PotentialRules.ValidThreshold(low, high))
        {
            return DoctorInsightsResponses.Fail<PotentialThresholdDto>(DoctorInsightsErrors.ThresholdInvalid,
                $"0 < lowBelow ≤ highAbove ≤ {DoctorInsightsLimits.MaxMonthlyPatients}.", 400);
        }

        if (request.ProductId is { } productId && _products is not null)
        {
            switch (await _products.ValidateAsync(IStrategyTemplateProductReferenceValidator.ReferenceKind.GlobalProduct, productId, ct))
            {
                case IStrategyTemplateProductReferenceValidator.Outcome.NotFound:
                    return DoctorInsightsResponses.Fail<PotentialThresholdDto>(DoctorInsightsErrors.ProductInvalid, $"{productId:D} is not an MDM Global Product.", 400);
                case IStrategyTemplateProductReferenceValidator.Outcome.Unavailable:
                    return DoctorInsightsResponses.Fail<PotentialThresholdDto>(DoctorInsightsErrors.ReferenceDataUnavailable,
                        "The product master could not be read. Nothing was saved.", 503);
            }
        }

        var now = _reader.Now;
        var store = _reader.ThresholdStore;
        var existing = await store.GetAsync(tenantId, request.ProductId, ct);
        if (request.Input.ExpectedVersion is { } expected && expected != (existing?.Version ?? 0))
        {
            return DoctorInsightsResponses.Fail<PotentialThresholdDto>(DoctorInsightsErrors.VersionConflict, "The thresholds were changed by someone else; reload them.", 409);
        }

        var old = existing is null ? PotentialRules.Resolve(await store.ListAsync(tenantId, ct), request.ProductId ?? Guid.Empty) : null;
        var record = existing ?? new ProductPotentialThreshold { TenantId = tenantId, GlobalProductId = request.ProductId, Version = 0 };
        var oldLow = existing?.LowBelow ?? old!.LowBelow;
        var oldHigh = existing?.HighAbove ?? old!.HighAbove;
        var version = record.Version;
        record.LowBelow = low;
        record.HighAbove = high;
        record.UpdatedBy = _actor.ActorName;
        record.UpdatedAt = now;
        if (existing is null)
        {
            record.Version = 1;
            await store.InsertAsync(record, ct);
        }
        else if (!await store.ReplaceAsync(record, version, ct))
        {
            return DoctorInsightsResponses.Fail<PotentialThresholdDto>(DoctorInsightsErrors.VersionConflict, "The thresholds were changed by someone else; reload them.", 409);
        }

        var changes = new List<ProductPotentialThresholdChange>();
        if (oldLow != low || existing is null)
            changes.Add(new ProductPotentialThresholdChange { TenantId = tenantId, GlobalProductId = request.ProductId, Field = "lowBelow",
                OldValue = existing is null ? null : oldLow.ToString(CultureInfo.InvariantCulture), NewValue = low.ToString(CultureInfo.InvariantCulture), By = _actor.ActorName, At = now });
        if (oldHigh != high || existing is null)
            changes.Add(new ProductPotentialThresholdChange { TenantId = tenantId, GlobalProductId = request.ProductId, Field = "highAbove",
                OldValue = existing is null ? null : oldHigh.ToString(CultureInfo.InvariantCulture), NewValue = high.ToString(CultureInfo.InvariantCulture), By = _actor.ActorName, At = now });
        await _thresholdHistory.InsertManyAsync(changes, ct);

        return Response<PotentialThresholdDto>.Success(DoctorInsightsQueryHandlers.ToDto(record,
            request.ProductId is null ? ThresholdSources.TenantDefault : ThresholdSources.Product));
    }
}
