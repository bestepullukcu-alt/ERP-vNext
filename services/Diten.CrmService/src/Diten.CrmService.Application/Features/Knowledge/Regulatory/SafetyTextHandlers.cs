using Diten.CrmService.Application.Common;
using Diten.CrmService.Application.Common.Models;
using Diten.CrmService.Application.Common.ReferenceValidation;
using Diten.CrmService.Application.Features.ContentComposition.Claims;
using Diten.CrmService.Application.Features.Knowledge.Path.Review;
using Diten.CrmService.Application.Features.StrategyTemplate.Binding;
using Diten.CrmService.Domain.Entities;
using Diten.CrmService.Domain.Repositories;
using MediatR;

namespace Diten.CrmService.Application.Features.Knowledge.Regulatory;

// ---------------- WP-KP-5a — safety text (product × country × language) ----------------

public sealed record ListSafetyTextsQuery(
    Guid? ProductId = null, string? CountryCode = null, string? LanguageCode = null, string? Status = null,
    bool IncludeArchived = false) : IRequest<Response<IReadOnlyList<SafetyTextDto>>>;

public sealed record GetSafetyTextQuery(Guid Id) : IRequest<Response<SafetyTextDto>>;

/// <summary>The active safety text of a key, or 404 <c>safety_text_missing</c> (the page designer's locked block).</summary>
public sealed record ResolveSafetyTextQuery(Guid ProductId, string? CountryCode, string? LanguageCode)
    : IRequest<Response<SafetyTextDto>>;

public sealed record CreateSafetyTextCommand(
    Guid GlobalProductId, string? GlobalProductCodeDisplay, string? CountryCode, string? LanguageCode, string? Body,
    string? ShortBody = null, string? SourceDocumentRef = null, DateTimeOffset? SourceDate = null,
    string? ApprovalReference = null) : IRequest<Response<SafetyTextDto>>;

/// <summary>Content of a draft only (the key — product, country, language — is fixed at creation).</summary>
public sealed record UpdateSafetyTextCommand(
    Guid Id, string? Body, string? ShortBody = null, string? SourceDocumentRef = null, DateTimeOffset? SourceDate = null,
    string? ApprovalReference = null, int? ExpectedVersion = null) : IRequest<Response<SafetyTextDto>>;

public sealed record NewSafetyTextVersionCommand(Guid Id) : IRequest<Response<SafetyTextDto>>;

public sealed record SubmitSafetyTextCommand(Guid Id) : IRequest<Response<SafetyTextDto>>;

public sealed record WithdrawSafetyTextCommand(Guid Id) : IRequest<Response<SafetyTextDto>>;

public sealed record DecideSafetyTextCommand(Guid Id, string? Decision, string? Comment) : IRequest<Response<SafetyTextDto>>;

public sealed record ArchiveSafetyTextCommand(Guid Id) : IRequest<Response<SafetyTextDto>>;

/// <summary>WP-KP-5a — every safety text request; the lifecycle is the shared <see cref="RegulatoryTextLifecycle{T}"/>.</summary>
public sealed class SafetyTextHandlers :
    IRequestHandler<ListSafetyTextsQuery, Response<IReadOnlyList<SafetyTextDto>>>,
    IRequestHandler<GetSafetyTextQuery, Response<SafetyTextDto>>,
    IRequestHandler<ResolveSafetyTextQuery, Response<SafetyTextDto>>,
    IRequestHandler<CreateSafetyTextCommand, Response<SafetyTextDto>>,
    IRequestHandler<UpdateSafetyTextCommand, Response<SafetyTextDto>>,
    IRequestHandler<NewSafetyTextVersionCommand, Response<SafetyTextDto>>,
    IRequestHandler<SubmitSafetyTextCommand, Response<SafetyTextDto>>,
    IRequestHandler<WithdrawSafetyTextCommand, Response<SafetyTextDto>>,
    IRequestHandler<DecideSafetyTextCommand, Response<SafetyTextDto>>,
    IRequestHandler<ArchiveSafetyTextCommand, Response<SafetyTextDto>>
{
    private readonly ISafetyTextRepository _repository;
    private readonly RegulatoryTextLifecycle<SafetyText> _lifecycle;
    private readonly IReferenceDataCatalogReader? _catalog;
    private readonly IStrategyTemplateProductReferenceValidator? _products;

    public SafetyTextHandlers(ITenantContext tenant, IActorContext actor, ISafetyTextRepository repository,
        IClaimWorkflowClient workflow, IWorkflowDecisionClient decisions, RegulatoryTextOutcomeApplier applier,
        RegulatoryTextReviewReconciler? reconciler = null, IRegulatoryTextReviewSettings? settings = null,
        IReferenceDataCatalogReader? catalog = null, IStrategyTemplateProductReferenceValidator? products = null,
        IUserDisplayNameResolver? names = null)
    {
        _repository = repository;
        _catalog = catalog;
        _products = products;
        _lifecycle = new RegulatoryTextLifecycle<SafetyText>(RegulatoryTextKind.SafetyText, tenant, actor, repository,
            workflow, decisions, applier, reconciler, settings, names);
    }

    // ---------------- reads ----------------

    public async Task<Response<IReadOnlyList<SafetyTextDto>>> Handle(ListSafetyTextsQuery request, CancellationToken ct)
    {
        if (_lifecycle.TenantId is not { } tenantId)
        {
            return Response<IReadOnlyList<SafetyTextDto>>.Fail("Tenant context is required.", 400);
        }

        var country = RegulatoryTextRules.NormalizeCountry(request.CountryCode);
        var language = RegulatoryTextRules.NormalizeLanguage(request.LanguageCode);
        var status = request.Status?.Trim().ToLowerInvariant();
        var rows = (await _lifecycle.ListAsync(tenantId, ct))
            .Where(t => request.IncludeArchived || !t.IsArchived())
            .Where(t => request.ProductId is not { } p || t.GlobalProductId == p)
            .Where(t => country.Length == 0 || t.CountryCode == country)
            .Where(t => language.Length == 0 || t.LanguageCode == language)
            .Where(t => string.IsNullOrEmpty(status) || t.Status == status)
            .OrderBy(t => t.Code, StringComparer.Ordinal).ThenByDescending(t => t.VersionNumber)
            .ToList();
        var can = await _lifecycle.AbilitiesAsync(rows, ct);
        return Response<IReadOnlyList<SafetyTextDto>>.Success(rows.Select(t => RegulatoryTextMapper.ToDto(t, can(t))).ToList());
    }

    public async Task<Response<SafetyTextDto>> Handle(GetSafetyTextQuery request, CancellationToken ct)
    {
        var (text, error) = await _lifecycle.LoadAsync<SafetyTextDto>(request.Id, ct);
        return error ?? await Detail(text!, ct);
    }

    public async Task<Response<SafetyTextDto>> Handle(ResolveSafetyTextQuery request, CancellationToken ct)
    {
        if (_lifecycle.TenantId is not { } tenantId)
        {
            return Response<SafetyTextDto>.Fail("Tenant context is required.", 400);
        }

        var country = RegulatoryTextRules.NormalizeCountry(request.CountryCode);
        var language = RegulatoryTextRules.NormalizeLanguage(request.LanguageCode);
        var active = (await _repository.ListAsync(tenantId, ct)).FirstOrDefault(t =>
            t.IsActive() && !t.IsArchived() && t.GlobalProductId == request.ProductId && t.CountryCode == country
            && t.LanguageCode == language);
        return active is null
            ? RegulatoryTextRules.Fail<SafetyTextDto>(RegulatoryTextErrors.SafetyTextMissing,
                $"No active safety text for this product in {country} / {language}.", 404)
            : Response<SafetyTextDto>.Success(RegulatoryTextMapper.ToDto(active, RegulatoryTextAbilities.None));
    }

    // ---------------- writes ----------------

    public async Task<Response<SafetyTextDto>> Handle(CreateSafetyTextCommand request, CancellationToken ct)
    {
        if (_lifecycle.TenantId is not { } tenantId)
        {
            return Response<SafetyTextDto>.Fail("Tenant context is required.", 400);
        }

        if (CheckContent(request.Body, request.ShortBody, request.SourceDocumentRef, request.ApprovalReference) is { } invalid)
        {
            return invalid;
        }

        var context = await RegulatoryTextLifecycle<SafetyText>.ValidateContextAsync(
            _catalog, request.CountryCode, request.LanguageCode, ct);
        if (!context.IsValid)
        {
            return Response<SafetyTextDto>.Fail(context.Errors!, context.StatusCode);
        }

        if (await CheckProductAsync(request.GlobalProductId, ct) is { } product)
        {
            return product;
        }

        var all = await _repository.ListAsync(tenantId, ct);
        var draft = new SafetyText
        {
            GlobalProductId = request.GlobalProductId,
            GlobalProductCodeDisplay = RegulatoryTextRules.Clean(request.GlobalProductCodeDisplay),
            CountryCode = context.Country!,
            LanguageCode = context.Language!
        };
        var sameKey = all.Where(t => t.Key() == draft.Key()).ToList();
        if (_lifecycle.CheckNoOpenVersion<SafetyTextDto>(sameKey) is { } open)
        {
            return open;
        }

        Apply(draft, request.Body, request.ShortBody, request.SourceDocumentRef, request.SourceDate, request.ApprovalReference);
        if (await _lifecycle.InsertDraftAsync<SafetyTextDto>(draft, tenantId, _lifecycle.CodeFor(all, sameKey, draft.CountryCode),
            RegulatoryTextLifecycle<SafetyText>.NextVersionNumber(sameKey), ct) is { } conflict)
        {
            return conflict;
        }

        return Response<SafetyTextDto>.Success(RegulatoryTextMapper.ToDto(draft, RegulatoryTextAbilities.NewDraft), 201);
    }

    public async Task<Response<SafetyTextDto>> Handle(UpdateSafetyTextCommand request, CancellationToken ct)
    {
        var (text, error) = await _lifecycle.LoadAsync<SafetyTextDto>(request.Id, ct);
        if (error is not null)
        {
            return error;
        }

        if (_lifecycle.CheckEditable<SafetyTextDto>(text!, request.ExpectedVersion) is { } locked)
        {
            return locked;
        }

        if (CheckContent(request.Body, request.ShortBody, request.SourceDocumentRef, request.ApprovalReference) is { } invalid)
        {
            return invalid;
        }

        Apply(text!, request.Body, request.ShortBody, request.SourceDocumentRef, request.SourceDate, request.ApprovalReference);
        var (saved, conflict) = await _lifecycle.SaveAsync<SafetyTextDto>(text!, ct);
        return conflict ?? await Detail(saved!, ct);
    }

    public async Task<Response<SafetyTextDto>> Handle(NewSafetyTextVersionCommand request, CancellationToken ct)
    {
        var (source, _, next, error) = await _lifecycle.PrepareNewVersionAsync<SafetyTextDto>(request.Id, ct);
        if (error is not null)
        {
            return error;
        }

        var draft = new SafetyText
        {
            GlobalProductId = source!.GlobalProductId, GlobalProductCodeDisplay = source.GlobalProductCodeDisplay,
            CountryCode = source.CountryCode, LanguageCode = source.LanguageCode
        };
        Apply(draft, source.Body, source.ShortBody, source.SourceDocumentRef, source.SourceDate, source.ApprovalReference);
        if (await _lifecycle.InsertDraftAsync<SafetyTextDto>(draft, source.TenantId, source.Code, next, ct) is { } conflict)
        {
            return conflict;
        }

        return Response<SafetyTextDto>.Success(RegulatoryTextMapper.ToDto(draft, RegulatoryTextAbilities.NewDraft), 201);
    }

    public async Task<Response<SafetyTextDto>> Handle(SubmitSafetyTextCommand request, CancellationToken ct)
        => await Result(await _lifecycle.SubmitAsync<SafetyTextDto>(request.Id,
            t => $"{t.GlobalProductCodeDisplay ?? t.GlobalProductId.ToString("D")} · {t.CountryCode}/{t.LanguageCode}", ct), ct);

    public async Task<Response<SafetyTextDto>> Handle(WithdrawSafetyTextCommand request, CancellationToken ct)
        => await Result(await _lifecycle.WithdrawAsync<SafetyTextDto>(request.Id, ct), ct);

    public async Task<Response<SafetyTextDto>> Handle(DecideSafetyTextCommand request, CancellationToken ct)
        => await Result(await _lifecycle.DecideAsync<SafetyTextDto>(request.Id, request.Decision, request.Comment, ct), ct);

    public async Task<Response<SafetyTextDto>> Handle(ArchiveSafetyTextCommand request, CancellationToken ct)
        => await Result(await _lifecycle.ArchiveAsync<SafetyTextDto>(request.Id, ct), ct);

    // ---------------- helpers ----------------

    private async Task<Response<SafetyTextDto>> Result((SafetyText? Text, Response<SafetyTextDto>? Error) outcome, CancellationToken ct)
        => outcome.Error ?? await Detail(outcome.Text!, ct);

    private async Task<Response<SafetyTextDto>> Detail(SafetyText text, CancellationToken ct)
    {
        var sameKey = (await _repository.ListAsync(text.TenantId, ct)).Where(t => t.Key() == text.Key()).ToList();
        var can = await _lifecycle.AbilitiesAsync(new[] { text }, ct);
        var names = await _lifecycle.DisplayNamesAsync(text, ct);
        return Response<SafetyTextDto>.Success(
            RegulatoryTextMapper.ToDto(text, can(text), RegulatoryTextMapper.History(sameKey), names));
    }

    private static void Apply(SafetyText text, string? body, string? shortBody, string? sourceRef, DateTimeOffset? sourceDate,
        string? approvalReference)
    {
        // Plain text: paragraphs are kept as written (only the outer whitespace is trimmed).
        text.Body = body!.Trim();
        text.ShortBody = RegulatoryTextRules.Clean(shortBody);
        text.SourceDocumentRef = RegulatoryTextRules.Clean(sourceRef);
        text.SourceDate = sourceDate;
        text.ApprovalReference = RegulatoryTextRules.Clean(approvalReference);
    }

    private static Response<SafetyTextDto>? CheckContent(string? body, string? shortBody, string? sourceRef, string? approvalReference)
    {
        var failure = RegulatoryTextRules.CheckText("Body", body, RegulatoryTextLimits.SafetyBody, required: true)
                      ?? RegulatoryTextRules.CheckText("ShortBody", shortBody, RegulatoryTextLimits.SafetyShortBody, false)
                      ?? RegulatoryTextRules.CheckText("SourceDocumentRef", sourceRef, RegulatoryTextLimits.Reference, false)
                      ?? RegulatoryTextRules.CheckText("ApprovalReference", approvalReference, RegulatoryTextLimits.Reference, false);
        return failure is { } f ? RegulatoryTextRules.Fail<SafetyTextDto>(f.Code, f.Message, 400) : null;
    }

    /// <summary>MDM global product, fail-closed: no validator / no answer → 503, never accepted on trust.</summary>
    private async Task<Response<SafetyTextDto>?> CheckProductAsync(Guid productId, CancellationToken ct)
    {
        if (productId == Guid.Empty)
        {
            return RegulatoryTextRules.Fail<SafetyTextDto>(RegulatoryTextErrors.ProductNotFound, "GlobalProductId is required.", 400);
        }

        var outcome = _products is null
            ? IStrategyTemplateProductReferenceValidator.Outcome.Unavailable
            : await _products.ValidateAsync(IStrategyTemplateProductReferenceValidator.ReferenceKind.GlobalProduct, productId, ct);
        return outcome switch
        {
            IStrategyTemplateProductReferenceValidator.Outcome.Valid => null,
            IStrategyTemplateProductReferenceValidator.Outcome.NotFound => RegulatoryTextRules.Fail<SafetyTextDto>(
                RegulatoryTextErrors.ProductNotFound, $"Global product '{productId:D}' does not exist in MDM.", 400),
            _ => RegulatoryTextRules.Fail<SafetyTextDto>(RegulatoryTextErrors.DependencyUnavailable,
                "MDM cannot be reached; the product cannot be verified. Nothing was saved.", 503)
        };
    }
}
