using Diten.CrmService.Application.Common;
using Diten.CrmService.Application.Common.Models;
using Diten.CrmService.Application.Common.ReferenceValidation;
using Diten.CrmService.Application.Features.ContentComposition.Claims;
using Diten.CrmService.Application.Features.Knowledge.Path.Review;
using Diten.CrmService.Domain.Entities;
using Diten.CrmService.Domain.Repositories;
using MediatR;

namespace Diten.CrmService.Application.Features.Knowledge.Regulatory;

// ---------------- WP-KP-5a — country legal profile (country × language) ----------------

public sealed record ListCountryLegalProfilesQuery(
    string? CountryCode = null, string? LanguageCode = null, string? Status = null, bool IncludeArchived = false)
    : IRequest<Response<IReadOnlyList<CountryLegalProfileDto>>>;

public sealed record GetCountryLegalProfileQuery(Guid Id) : IRequest<Response<CountryLegalProfileDto>>;

/// <summary>The active legal profile of a country + language, or 404 <c>legal_profile_missing</c>.</summary>
public sealed record ResolveCountryLegalProfileQuery(string? CountryCode, string? LanguageCode)
    : IRequest<Response<CountryLegalProfileDto>>;

public sealed record CreateCountryLegalProfileCommand(
    string? CountryCode, string? LanguageCode, string? LegalFooterText, string? MarketingAuthorizationHolder = null,
    string? AdverseEventReportingText = null, string? PromotionalNotice = null, string? PageApprovalCodeFormat = null)
    : IRequest<Response<CountryLegalProfileDto>>;

/// <summary>Content of a draft only (country + language are fixed at creation).</summary>
public sealed record UpdateCountryLegalProfileCommand(
    Guid Id, string? LegalFooterText, string? MarketingAuthorizationHolder = null, string? AdverseEventReportingText = null,
    string? PromotionalNotice = null, string? PageApprovalCodeFormat = null, int? ExpectedVersion = null)
    : IRequest<Response<CountryLegalProfileDto>>;

public sealed record NewCountryLegalProfileVersionCommand(Guid Id) : IRequest<Response<CountryLegalProfileDto>>;

public sealed record SubmitCountryLegalProfileCommand(Guid Id) : IRequest<Response<CountryLegalProfileDto>>;

public sealed record WithdrawCountryLegalProfileCommand(Guid Id) : IRequest<Response<CountryLegalProfileDto>>;

public sealed record DecideCountryLegalProfileCommand(Guid Id, string? Decision, string? Comment)
    : IRequest<Response<CountryLegalProfileDto>>;

public sealed record ArchiveCountryLegalProfileCommand(Guid Id) : IRequest<Response<CountryLegalProfileDto>>;

/// <summary>WP-KP-5a — every legal profile request; the lifecycle is the shared <see cref="RegulatoryTextLifecycle{T}"/>.</summary>
public sealed class CountryLegalProfileHandlers :
    IRequestHandler<ListCountryLegalProfilesQuery, Response<IReadOnlyList<CountryLegalProfileDto>>>,
    IRequestHandler<GetCountryLegalProfileQuery, Response<CountryLegalProfileDto>>,
    IRequestHandler<ResolveCountryLegalProfileQuery, Response<CountryLegalProfileDto>>,
    IRequestHandler<CreateCountryLegalProfileCommand, Response<CountryLegalProfileDto>>,
    IRequestHandler<UpdateCountryLegalProfileCommand, Response<CountryLegalProfileDto>>,
    IRequestHandler<NewCountryLegalProfileVersionCommand, Response<CountryLegalProfileDto>>,
    IRequestHandler<SubmitCountryLegalProfileCommand, Response<CountryLegalProfileDto>>,
    IRequestHandler<WithdrawCountryLegalProfileCommand, Response<CountryLegalProfileDto>>,
    IRequestHandler<DecideCountryLegalProfileCommand, Response<CountryLegalProfileDto>>,
    IRequestHandler<ArchiveCountryLegalProfileCommand, Response<CountryLegalProfileDto>>
{
    private readonly ICountryLegalProfileRepository _repository;
    private readonly RegulatoryTextLifecycle<CountryLegalProfile> _lifecycle;
    private readonly IReferenceDataCatalogReader? _catalog;

    public CountryLegalProfileHandlers(ITenantContext tenant, IActorContext actor, ICountryLegalProfileRepository repository,
        IClaimWorkflowClient workflow, IWorkflowDecisionClient decisions, RegulatoryTextOutcomeApplier applier,
        RegulatoryTextReviewReconciler? reconciler = null, IRegulatoryTextReviewSettings? settings = null,
        IReferenceDataCatalogReader? catalog = null)
    {
        _repository = repository;
        _catalog = catalog;
        _lifecycle = new RegulatoryTextLifecycle<CountryLegalProfile>(RegulatoryTextKind.LegalProfile, tenant, actor,
            repository, workflow, decisions, applier, reconciler, settings);
    }

    // ---------------- reads ----------------

    public async Task<Response<IReadOnlyList<CountryLegalProfileDto>>> Handle(
        ListCountryLegalProfilesQuery request, CancellationToken ct)
    {
        if (_lifecycle.TenantId is not { } tenantId)
        {
            return Response<IReadOnlyList<CountryLegalProfileDto>>.Fail("Tenant context is required.", 400);
        }

        var country = RegulatoryTextRules.NormalizeCountry(request.CountryCode);
        var language = RegulatoryTextRules.NormalizeLanguage(request.LanguageCode);
        var status = request.Status?.Trim().ToLowerInvariant();
        var rows = (await _lifecycle.ListAsync(tenantId, ct))
            .Where(p => request.IncludeArchived || !p.IsArchived())
            .Where(p => country.Length == 0 || p.CountryCode == country)
            .Where(p => language.Length == 0 || p.LanguageCode == language)
            .Where(p => string.IsNullOrEmpty(status) || p.Status == status)
            .OrderBy(p => p.CountryCode, StringComparer.Ordinal).ThenBy(p => p.LanguageCode, StringComparer.Ordinal)
            .ThenByDescending(p => p.VersionNumber)
            .ToList();
        var can = await _lifecycle.AbilitiesAsync(rows, ct);
        return Response<IReadOnlyList<CountryLegalProfileDto>>.Success(
            rows.Select(p => RegulatoryTextMapper.ToDto(p, can(p))).ToList());
    }

    public async Task<Response<CountryLegalProfileDto>> Handle(GetCountryLegalProfileQuery request, CancellationToken ct)
    {
        var (profile, error) = await _lifecycle.LoadAsync<CountryLegalProfileDto>(request.Id, ct);
        return error ?? await Detail(profile!, ct);
    }

    public async Task<Response<CountryLegalProfileDto>> Handle(ResolveCountryLegalProfileQuery request, CancellationToken ct)
    {
        if (_lifecycle.TenantId is not { } tenantId)
        {
            return Response<CountryLegalProfileDto>.Fail("Tenant context is required.", 400);
        }

        var country = RegulatoryTextRules.NormalizeCountry(request.CountryCode);
        var language = RegulatoryTextRules.NormalizeLanguage(request.LanguageCode);
        var active = (await _repository.ListAsync(tenantId, ct)).FirstOrDefault(p =>
            p.IsActive() && !p.IsArchived() && p.CountryCode == country && p.LanguageCode == language);
        return active is null
            ? RegulatoryTextRules.Fail<CountryLegalProfileDto>(RegulatoryTextErrors.LegalProfileMissing,
                $"No active legal profile for {country} / {language}.", 404)
            : Response<CountryLegalProfileDto>.Success(
                RegulatoryTextMapper.ToDto(active, new RegulatoryTextAbilities(false, false, false)));
    }

    // ---------------- writes ----------------

    public async Task<Response<CountryLegalProfileDto>> Handle(CreateCountryLegalProfileCommand request, CancellationToken ct)
    {
        if (_lifecycle.TenantId is not { } tenantId)
        {
            return Response<CountryLegalProfileDto>.Fail("Tenant context is required.", 400);
        }

        if (CheckContent(request.LegalFooterText, request.MarketingAuthorizationHolder, request.AdverseEventReportingText,
                request.PromotionalNotice, request.PageApprovalCodeFormat) is { } invalid)
        {
            return invalid;
        }

        var context = await RegulatoryTextLifecycle<CountryLegalProfile>.ValidateContextAsync(
            _catalog, request.CountryCode, request.LanguageCode, ct);
        if (!context.IsValid)
        {
            return Response<CountryLegalProfileDto>.Fail(context.Errors!, context.StatusCode);
        }

        var all = await _repository.ListAsync(tenantId, ct);
        var draft = new CountryLegalProfile { CountryCode = context.Country!, LanguageCode = context.Language! };
        var sameKey = all.Where(p => p.Key() == draft.Key()).ToList();
        if (_lifecycle.CheckNoOpenVersion<CountryLegalProfileDto>(sameKey) is { } open)
        {
            return open;
        }

        Apply(draft, request.LegalFooterText, request.MarketingAuthorizationHolder, request.AdverseEventReportingText,
            request.PromotionalNotice, request.PageApprovalCodeFormat);
        await _lifecycle.InsertDraftAsync(draft, tenantId, _lifecycle.CodeFor(all, sameKey, draft.CountryCode),
            RegulatoryTextLifecycle<CountryLegalProfile>.NextVersionNumber(sameKey), ct);
        return Response<CountryLegalProfileDto>.Success(
            RegulatoryTextMapper.ToDto(draft, new RegulatoryTextAbilities(true, true, false)), 201);
    }

    public async Task<Response<CountryLegalProfileDto>> Handle(UpdateCountryLegalProfileCommand request, CancellationToken ct)
    {
        var (profile, error) = await _lifecycle.LoadAsync<CountryLegalProfileDto>(request.Id, ct);
        if (error is not null)
        {
            return error;
        }

        if (_lifecycle.CheckEditable<CountryLegalProfileDto>(profile!, request.ExpectedVersion) is { } locked)
        {
            return locked;
        }

        if (CheckContent(request.LegalFooterText, request.MarketingAuthorizationHolder, request.AdverseEventReportingText,
                request.PromotionalNotice, request.PageApprovalCodeFormat) is { } invalid)
        {
            return invalid;
        }

        Apply(profile!, request.LegalFooterText, request.MarketingAuthorizationHolder, request.AdverseEventReportingText,
            request.PromotionalNotice, request.PageApprovalCodeFormat);
        var (saved, conflict) = await _lifecycle.SaveAsync<CountryLegalProfileDto>(profile!, ct);
        return conflict ?? await Detail(saved!, ct);
    }

    public async Task<Response<CountryLegalProfileDto>> Handle(NewCountryLegalProfileVersionCommand request, CancellationToken ct)
    {
        var (source, _, next, error) = await _lifecycle.PrepareNewVersionAsync<CountryLegalProfileDto>(request.Id, ct);
        if (error is not null)
        {
            return error;
        }

        var draft = new CountryLegalProfile { CountryCode = source!.CountryCode, LanguageCode = source.LanguageCode };
        Apply(draft, source.LegalFooterText, source.MarketingAuthorizationHolder, source.AdverseEventReportingText,
            source.PromotionalNotice, source.PageApprovalCodeFormat);
        await _lifecycle.InsertDraftAsync(draft, source.TenantId, source.Code, next, ct);
        return Response<CountryLegalProfileDto>.Success(
            RegulatoryTextMapper.ToDto(draft, new RegulatoryTextAbilities(true, true, false)), 201);
    }

    public async Task<Response<CountryLegalProfileDto>> Handle(SubmitCountryLegalProfileCommand request, CancellationToken ct)
        => await Result(await _lifecycle.SubmitAsync<CountryLegalProfileDto>(request.Id,
            p => $"{p.CountryCode}/{p.LanguageCode}", ct), ct);

    public async Task<Response<CountryLegalProfileDto>> Handle(WithdrawCountryLegalProfileCommand request, CancellationToken ct)
        => await Result(await _lifecycle.WithdrawAsync<CountryLegalProfileDto>(request.Id, ct), ct);

    public async Task<Response<CountryLegalProfileDto>> Handle(DecideCountryLegalProfileCommand request, CancellationToken ct)
        => await Result(await _lifecycle.DecideAsync<CountryLegalProfileDto>(request.Id, request.Decision, request.Comment, ct), ct);

    public async Task<Response<CountryLegalProfileDto>> Handle(ArchiveCountryLegalProfileCommand request, CancellationToken ct)
        => await Result(await _lifecycle.ArchiveAsync<CountryLegalProfileDto>(request.Id, ct), ct);

    // ---------------- helpers ----------------

    private async Task<Response<CountryLegalProfileDto>> Result(
        (CountryLegalProfile? Profile, Response<CountryLegalProfileDto>? Error) outcome, CancellationToken ct)
        => outcome.Error ?? await Detail(outcome.Profile!, ct);

    private async Task<Response<CountryLegalProfileDto>> Detail(CountryLegalProfile profile, CancellationToken ct)
    {
        var sameKey = (await _repository.ListAsync(profile.TenantId, ct)).Where(p => p.Key() == profile.Key()).ToList();
        var can = await _lifecycle.AbilitiesAsync(new[] { profile }, ct);
        return Response<CountryLegalProfileDto>.Success(
            RegulatoryTextMapper.ToDto(profile, can(profile), RegulatoryTextMapper.History(sameKey)));
    }

    private static void Apply(CountryLegalProfile profile, string? footer, string? holder, string? adverseEvent,
        string? promotionalNotice, string? approvalCodeFormat)
    {
        profile.LegalFooterText = footer!.Trim();
        profile.MarketingAuthorizationHolder = RegulatoryTextRules.Clean(holder);
        profile.AdverseEventReportingText = RegulatoryTextRules.Clean(adverseEvent);
        profile.PromotionalNotice = RegulatoryTextRules.Clean(promotionalNotice);
        profile.PageApprovalCodeFormat = RegulatoryTextRules.Clean(approvalCodeFormat);
    }

    private static Response<CountryLegalProfileDto>? CheckContent(
        string? footer, string? holder, string? adverseEvent, string? promotionalNotice, string? approvalCodeFormat)
    {
        var failure = RegulatoryTextRules.CheckText("LegalFooterText", footer, RegulatoryTextLimits.LegalText, required: true)
                      ?? RegulatoryTextRules.CheckText("MarketingAuthorizationHolder", holder, RegulatoryTextLimits.LegalText, false)
                      ?? RegulatoryTextRules.CheckText("AdverseEventReportingText", adverseEvent, RegulatoryTextLimits.LegalText, false)
                      ?? RegulatoryTextRules.CheckText("PromotionalNotice", promotionalNotice, RegulatoryTextLimits.LegalText, false)
                      ?? RegulatoryTextRules.CheckText("PageApprovalCodeFormat", approvalCodeFormat,
                          RegulatoryTextLimits.PageApprovalCodeFormat, false);
        return failure is { } f ? RegulatoryTextRules.Fail<CountryLegalProfileDto>(f.Code, f.Message, 400) : null;
    }
}
