using Diten.CrmService.Application.Common;
using Diten.CrmService.Application.Common.Models;
using Diten.CrmService.Application.Common.ReferenceValidation;
using Diten.CrmService.Application.Features.Knowledge.ObjectionResponses;
using Diten.CrmService.Application.Features.PlannedVisit;
using Diten.CrmService.Application.Features.VisitWorkspace.Handlers.QueryHandlers;
using Diten.CrmService.Domain.Entities;
using Diten.CrmService.Domain.Repositories;
using MediatR;
using PlannedVisitEntity = Diten.CrmService.Domain.Entities.PlannedVisit;

namespace Diten.CrmService.Application.Features.VisitPresentation;

/// <summary>WP-VW-W3 (W3-BE-b, §2.3 — FIXED shape, W3-WEB-b binds to it) — the slides of one visit.</summary>
public sealed record VisitPresentationDto(
    Guid PlannedVisitId,
    IReadOnlyList<VisitPresentationSlideDto> Slides,
    IReadOnlyList<VisitObjectionCategoryDto> ObjectionCategories,
    IReadOnlyList<string> Warnings);

public sealed record VisitPresentationSlideDto(
    int Index,
    Guid ProductId,
    string? ProductCode,
    string? ProductName,
    string Role,
    Guid StepId,
    Guid ContentId,
    string Title,
    string? Subtitle,
    IReadOnlyList<string> Bullets,
    IReadOnlyList<string> References,
    VisitPresentationAssetDto Asset,
    VisitPresentationNotesDto Presentation,
    IReadOnlyList<VisitPresentationObjectionDto> Objections);

public sealed record VisitPresentationAssetDto(string Kind, string? Url);

public sealed record VisitPresentationNotesDto(string? KeyMessage, string? TalkTrack, int? TargetSeconds, string? Transition);

public sealed record VisitPresentationObjectionDto(
    Guid Id, string CategoryCode, string CategoryLabel, string Question, string Answer, IReadOnlyList<string> References);

public sealed record VisitObjectionCategoryDto(string Code, string Label);

public sealed record GetVisitPresentationQuery(Guid PlannedVisitId, string? Language) : IRequest<Response<VisitPresentationDto>>;

/// <summary>The presentation warnings (codes; the screen words them).</summary>
public static class VisitPresentationWarnings
{
    /// <summary>The visit carries no product content (nothing to show).</summary>
    public const string NoContent = "no_content";

    /// <summary>A product line carries no frozen step.</summary>
    public const string NoSteps = "no_steps";

    /// <summary>The path version the plan froze cannot be read (gone / another version): the slide shows the frozen
    /// title and time only, without notes.</summary>
    public const string PathVersionMissing = "path_version_missing";

    /// <summary>A step's content cannot be read: the slide has no bullets.</summary>
    public const string ContentMissing = "content_missing";

    /// <summary>The objection category list cannot be read: categories are not labelled.</summary>
    public const string ObjectionCategoriesUnavailable = "objection_categories_unavailable";

    /// <summary>The content image is not shown to a rep (decision 8 — no new Platform permission; follow-up).</summary>
    public const string AssetNotAvailable = "asset_not_available";
}

public static class VisitPresentationErrors
{
    public const string NotFound = "visit_presentation_not_found";
    public const string AssetUnavailable = "presentation_asset_unavailable";
}

/// <summary>
/// WP-VW-W3 (W3-BE-b, §2.3) — <c>GET api/crm/visit-presentation/{plannedVisitId}?lang=</c>: the slides a rep shows the
/// doctor and the notes the rep reads, from what the PLAN FROZE.
/// <list type="bullet">
/// <item><b>Who:</b> <c>crm.visit-report.read</c> + ownership (<see cref="ICallerScope"/>; read-all holders see all) —
/// another rep's visit is 404 like a missing one.</item>
/// <item><b>Which slides:</b> the visit's product lines in their <c>Order</c>; a PROMO line shows every frozen step, a
/// reminder (non-promo) line only its FIRST step, and a PHARMACY visit the first step of every product.</item>
/// <item><b>Step fields</b> come from the path version the plan froze (<c>PathId</c> + <c>PathVersion</c>): title, notes,
/// estimated time. Gone / another version ⇒ the frozen title + minutes, no notes, warning
/// <c>path_version_missing</c>.</item>
/// <item><b>Bullets / references:</b> the step content's claims that are APPROVED (a bound country version must itself be
/// approved; its text in the request language) — a draft / withdrawn / archived claim is NEVER returned. References =
/// those claims' evidence references.</item>
/// <item><b>Objections:</b> the step's linked answers that are APPROVED (draft / retired never), in the linked order,
/// labelled in the request language; their references from their approved claims only.</item>
/// <item><b>Target time:</b> the note's seconds, else the step's estimated minutes × 60, else null.</item>
/// <item><b>Image:</b> decision 8 — the content image is read through the Platform document repository AS THE CALLER, and
/// a rep holds no <c>platform.document-repository.read</c> (granted to nobody new): it cannot be read without a new
/// Platform permission, so every slide says <c>asset.kind = "none"</c> (warning <c>asset_not_available</c>; follow-up).</item>
/// </list>
/// Reads only — nothing written, nothing audited.
/// </summary>
public sealed class GetVisitPresentationHandler : IRequestHandler<GetVisitPresentationQuery, Response<VisitPresentationDto>>
{
    private readonly ITenantContext _tenant;
    private readonly ICallerScope _caller;
    private readonly IPlannedVisitRepository _plans;
    private readonly IKnowledgePathRepository _paths;
    private readonly IKnowledgeContentRepository _contents;
    private readonly IClaimRepository _claims;
    private readonly IObjectionResponseRepository _objections;
    private readonly IReferenceDataCatalogReader _catalog;
    private readonly IClaimCountryVersionRepository? _countryVersions;

    public GetVisitPresentationHandler(
        ITenantContext tenant,
        ICallerScope caller,
        IPlannedVisitRepository plans,
        IKnowledgePathRepository paths,
        IKnowledgeContentRepository contents,
        IClaimRepository claims,
        IObjectionResponseRepository objections,
        IReferenceDataCatalogReader catalog,
        IClaimCountryVersionRepository? countryVersions = null)
    {
        _tenant = tenant;
        _caller = caller;
        _plans = plans;
        _paths = paths;
        _contents = contents;
        _claims = claims;
        _objections = objections;
        _catalog = catalog;
        _countryVersions = countryVersions;
    }

    public async Task<Response<VisitPresentationDto>> Handle(GetVisitPresentationQuery request, CancellationToken cancellationToken)
    {
        if (_tenant.TenantId is not { } tenantId)
        {
            return Response<VisitPresentationDto>.Fail("Tenant context is required.", 400);
        }

        var plan = await _plans.GetByIdAsync(tenantId, request.PlannedVisitId, cancellationToken);
        if (plan is null || plan.TenantId != tenantId || plan.IsDeleted
            || !_caller.MayAccess(PlannedVisitPermissions.ReadAll, plan.Resource?.ResourceId))
        {
            return Response<VisitPresentationDto>.Fail(new[] { VisitPresentationErrors.NotFound, "Planned visit not found." }, 404);
        }

        var language = GetVisitReasonsHandler.NormalizeLanguage(request.Language);
        var warnings = new List<string>();
        var categories = await CategoriesAsync(language, warnings, cancellationToken);
        var labelOf = categories.ToDictionary(c => c.Code, c => c.Label, StringComparer.Ordinal);
        var ctx = new Reads(this, tenantId, language);

        var slides = new List<VisitPresentationSlideDto>();
        var items = plan.ContentItems
            .Select((item, i) => (Item: item, Position: i))
            .OrderBy(x => x.Item.Order <= 0 ? int.MaxValue : x.Item.Order)
            .ThenBy(x => x.Position)
            .Select(x => x.Item)
            .ToList();
        if (items.Count == 0)
        {
            warnings.Add(VisitPresentationWarnings.NoContent);
        }

        var pharmacy = string.Equals(plan.TargetType, PlannedVisitTargetType.Pharmacy, StringComparison.Ordinal);
        foreach (var item in items)
        {
            if (item.Steps.Count == 0)
            {
                warnings.Add(VisitPresentationWarnings.NoSteps);
                continue;
            }

            var role = string.Equals(item.Role, StrategyProductLineRoles.NonPromo, StringComparison.Ordinal)
                ? StrategyProductLineRoles.NonPromo
                : StrategyProductLineRoles.Promo;
            // a reminder line and a pharmacy visit show the FIRST step only; a promo line every frozen step
            var steps = role == StrategyProductLineRoles.Promo && !pharmacy ? item.Steps.ToList() : item.Steps.Take(1).ToList();

            var path = await ctx.PathAsync(item.PathId);
            var sameVersion = path is not null
                              && (string.IsNullOrWhiteSpace(item.PathVersion)
                                  || string.Equals(path.PathVersion, item.PathVersion, StringComparison.OrdinalIgnoreCase));
            if (!sameVersion)
            {
                warnings.Add(VisitPresentationWarnings.PathVersionMissing);
            }

            foreach (var frozen in steps)
            {
                var pathStep = sameVersion ? path!.Steps.FirstOrDefault(s => s.StepId == frozen.StepId) : null;
                var content = await ctx.ContentAsync(frozen.ContentId);
                if (content is null)
                {
                    warnings.Add(VisitPresentationWarnings.ContentMissing);
                }

                var (bullets, references) = await ctx.ClaimTextsAsync(content?.ClaimRefs ?? new List<KnowledgeContentClaimRef>());
                var notes = pathStep?.Presentation;
                var minutes = pathStep?.EstimatedDurationMinutes ?? frozen.Minutes;
                var objections = await ctx.ObjectionsAsync(notes?.ObjectionResponseIds ?? new List<Guid>(), labelOf);

                slides.Add(new VisitPresentationSlideDto(
                    slides.Count,
                    item.ProductId,
                    item.ProductCode,
                    item.ProductName,
                    role,
                    frozen.StepId,
                    frozen.ContentId,
                    pathStep?.StepTitle ?? frozen.Title ?? content?.ContentTitle ?? string.Empty,
                    content?.Summary,
                    bullets,
                    references,
                    new VisitPresentationAssetDto("none", null),
                    new VisitPresentationNotesDto(
                        notes?.KeyMessage, notes?.TalkTrack,
                        notes?.TargetSeconds ?? (minutes is { } m && m > 0 ? m * 60 : null),
                        notes?.Transition),
                    objections));
            }
        }

        if (slides.Count > 0)
        {
            warnings.Add(VisitPresentationWarnings.AssetNotAvailable);
        }

        return Response<VisitPresentationDto>.Success(new VisitPresentationDto(
            plan.Id, slides, categories, warnings.Distinct(StringComparer.Ordinal).ToList()));
    }

    private async Task<IReadOnlyList<VisitObjectionCategoryDto>> CategoriesAsync(
        string language, List<string> warnings, CancellationToken cancellationToken)
    {
        var set = await _catalog.GetPublishedValuesAsync(VisitObjectionCategories.SetCode, cancellationToken);
        if (!set.IsPublished)
        {
            warnings.Add(VisitPresentationWarnings.ObjectionCategoriesUnavailable);
            return Array.Empty<VisitObjectionCategoryDto>();
        }

        return set.Values
            .Where(v => v.IsActive && !v.IsDeprecated)
            .Select(v => new VisitObjectionCategoryDto(v.ValueCode, GetVisitReasonsHandler.LabelOf(v, language)))
            .ToList();
    }

    /// <summary>One request's reads, each id read once.</summary>
    private sealed class Reads(GetVisitPresentationHandler owner, Guid tenantId, string language)
    {
        private readonly Dictionary<Guid, KnowledgePath?> _paths = new();
        private readonly Dictionary<Guid, KnowledgeContent?> _contents = new();
        private readonly Dictionary<Guid, Claim?> _claims = new();

        public async Task<KnowledgePath?> PathAsync(Guid id)
        {
            if (!_paths.TryGetValue(id, out var path))
            {
                path = id == Guid.Empty ? null : await owner._paths.GetByIdAsync(tenantId, id, CancellationToken.None);
                _paths[id] = path is not null && path.TenantId == tenantId ? path : null;
            }

            return _paths[id];
        }

        public async Task<KnowledgeContent?> ContentAsync(Guid id)
        {
            if (!_contents.TryGetValue(id, out var content))
            {
                content = id == Guid.Empty ? null : await owner._contents.GetByIdAsync(tenantId, id, CancellationToken.None);
                _contents[id] = content is not null && content.TenantId == tenantId ? content : null;
            }

            return _contents[id];
        }

        /// <summary>The claim only when it is this tenant's APPROVED, not archived claim — never anything else.</summary>
        public async Task<Claim?> ApprovedClaimAsync(Guid id)
        {
            if (!_claims.TryGetValue(id, out var claim))
            {
                claim = id == Guid.Empty ? null : await owner._claims.GetByIdAsync(tenantId, id, CancellationToken.None);
                _claims[id] = claim is not null && claim.TenantId == tenantId && claim.IsApproved() && !claim.IsArchived() ? claim : null;
            }

            return _claims[id];
        }

        public async Task<(IReadOnlyList<string> Bullets, IReadOnlyList<string> References)> ClaimTextsAsync(
            IEnumerable<KnowledgeContentClaimRef> refs)
        {
            var bullets = new List<string>();
            var references = new List<string>();
            foreach (var claimRef in refs)
            {
                var claim = await ApprovedClaimAsync(claimRef.ClaimId);
                if (claim is null)
                {
                    continue; // a draft / withdrawn / archived claim never reaches a rep
                }

                string? text = claim.ClaimText;
                if (claimRef.CountryVersionId is { } versionId)
                {
                    // a bound country version must itself be approved; its text in the request language
                    var version = owner._countryVersions is null
                        ? null
                        : await owner._countryVersions.GetByIdAsync(tenantId, versionId, CancellationToken.None);
                    if (version is null || version.TenantId != tenantId || version.IsArchived()
                        || !string.Equals(version.Status, ClaimStatuses.Approved, StringComparison.OrdinalIgnoreCase))
                    {
                        continue;
                    }

                    text = version.Texts.FirstOrDefault(t => string.Equals(t.LanguageCode, language, StringComparison.OrdinalIgnoreCase))?.Text
                           ?? version.Texts.FirstOrDefault()?.Text
                           ?? claim.ClaimText;
                }

                if (!string.IsNullOrWhiteSpace(text))
                {
                    bullets.Add(text.Trim());
                }

                references.AddRange(claim.EvidenceRefs.Where(r => !string.IsNullOrWhiteSpace(r)).Select(r => r.Trim()));
            }

            return (bullets, references.Distinct(StringComparer.Ordinal).ToList());
        }

        /// <summary>The linked answers that are APPROVED, in the linked order (draft / retired never).</summary>
        public async Task<IReadOnlyList<VisitPresentationObjectionDto>> ObjectionsAsync(
            IReadOnlyList<Guid> ids, IReadOnlyDictionary<string, string> labelOf)
        {
            if (ids.Count == 0)
            {
                return Array.Empty<VisitPresentationObjectionDto>();
            }

            var byId = (await owner._objections.ListByIdsAsync(tenantId, ids, CancellationToken.None))
                .Where(o => o.TenantId == tenantId && !o.IsDeleted && o.IsApproved())
                .ToDictionary(o => o.Id);
            var result = new List<VisitPresentationObjectionDto>();
            foreach (var id in ids.Distinct())
            {
                if (!byId.TryGetValue(id, out var answer))
                {
                    continue;
                }

                var references = new List<string>();
                foreach (var claimId in answer.ClaimIds)
                {
                    if (await ApprovedClaimAsync(claimId) is { } claim)
                    {
                        references.AddRange(claim.EvidenceRefs.Where(r => !string.IsNullOrWhiteSpace(r)).Select(r => r.Trim()));
                    }
                }

                result.Add(new VisitPresentationObjectionDto(
                    answer.Id, answer.CategoryCode,
                    labelOf.TryGetValue(answer.CategoryCode, out var label) ? label : answer.CategoryCode,
                    answer.Question, answer.Answer, references.Distinct(StringComparer.Ordinal).ToList()));
            }

            return result;
        }
    }
}
