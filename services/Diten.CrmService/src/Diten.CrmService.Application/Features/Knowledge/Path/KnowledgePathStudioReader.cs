using Diten.CrmService.Application.Features.ContentComposition.Claims;
using Diten.CrmService.Application.Features.Knowledge.Chain;
using Diten.CrmService.Application.Features.Knowledge.Content;
using Diten.CrmService.Domain.Entities;
using Diten.CrmService.Domain.Repositories;

namespace Diten.CrmService.Application.Features.Knowledge.Path;

/// <summary>WP-KP-1 — the studio part of a path detail read (chain, derived context, claims, chain conformance).</summary>
public sealed record KnowledgePathStudioView(
    KnowledgePathChainTemplateDto? ChainTemplate,
    ChainDerivedContextDto? DerivedContext,
    IReadOnlyList<KnowledgePathClaimDto> Claims,
    IReadOnlyList<KnowledgePathChainConformanceDto> ChainConformance)
{
    public static KnowledgePathStudioView Legacy { get; } = new(
        null, null, Array.Empty<KnowledgePathClaimDto>(), Array.Empty<KnowledgePathChainConformanceDto>());
}

/// <summary>
/// WP-KP-1 — builds <see cref="KnowledgePathStudioView"/> for a chain-bound path. Read-only: it never writes a path, a
/// chain, a claim or a country version. A legacy path reads as <see cref="KnowledgePathStudioView.Legacy"/>.
/// <list type="bullet">
/// <item>claims — the country version is resolved from the path country with the coverage priority
/// (<c>ClaimLines.CellVersion</c>: approved › review-required › in-review › draft); text / qualifier in the path
/// language; <c>usable</c> exactly when that version is approved / review-required and has path-language text;</item>
/// <item>conformance — per chain slot (branch-first order): active steps on it against MinSelection / MaxSelection.</item>
/// </list>
/// </summary>
public sealed class KnowledgePathStudioReader
{
    private readonly IConceptChainTemplateRepository _templates;
    private readonly IChainContextResolver _chainContext;
    private readonly IClaimRepository? _claims;
    private readonly IClaimCountryVersionRepository? _claimVersions;
    private readonly IConceptTypeRepository? _types;

    public KnowledgePathStudioReader(
        IConceptChainTemplateRepository templates, IChainContextResolver chainContext,
        IClaimRepository? claims = null, IClaimCountryVersionRepository? claimVersions = null,
        IConceptTypeRepository? types = null)
    {
        _templates = templates;
        _chainContext = chainContext;
        _claims = claims;
        _claimVersions = claimVersions;
        _types = types;
    }

    public async Task<KnowledgePathStudioView> ReadAsync(Guid tenantId, KnowledgePath path, CancellationToken ct)
    {
        if (path.ChainTemplate is not { } chain)
        {
            return KnowledgePathStudioView.Legacy;
        }

        var template = await _templates.GetByIdAsync(tenantId, chain.ConceptChainTemplateId, ct);
        var derived = await _chainContext.ResolveAsync(tenantId, template, ct);
        return new KnowledgePathStudioView(
            KnowledgePathMapper.ChainRefDto(path, template),
            derived,
            await ClaimsAsync(tenantId, path, template, ct),
            template is null ? Array.Empty<KnowledgePathChainConformanceDto>() : await ConformanceAsync(tenantId, path, template, ct));
    }

    private async Task<IReadOnlyList<KnowledgePathClaimDto>> ClaimsAsync(
        Guid tenantId, KnowledgePath path, ConceptChainTemplate? template, CancellationToken ct)
    {
        var placed = template is null
            ? path.Claims
            : ChainArrangementOrder.Order(template, path.Claims, c => ChainSlots.SlotOf(c.Arrangement));

        var result = new List<KnowledgePathClaimDto>(placed.Count);
        foreach (var item in placed)
        {
            var claim = _claims is null ? null : await _claims.GetByIdAsync(tenantId, item.ClaimId, ct);
            ClaimCountryVersion? version = null;
            if (claim is not null && _claimVersions is not null && !string.IsNullOrWhiteSpace(path.CountryCode))
            {
                var versions = (await _claimVersions.ListByClaimCodeAsync(tenantId, claim.ClaimCode, ct))
                    .Where(v => v.ClaimId == claim.Id);
                version = ClaimLines.CellVersion(versions, path.CountryCode);
            }

            var text = InLanguage(version?.Texts, path.LanguageCode);
            var qualifier = InLanguage(version?.Qualifiers, path.LanguageCode);
            var reason = claim is null ? KnowledgePathClaimReasons.NotApproved
                : version is null ? KnowledgePathClaimReasons.NoCountryVersion
                : !KnowledgeContentClaimLinks.IsUsable(version.Status) ? KnowledgePathClaimReasons.NotApproved
                : text is null ? KnowledgePathClaimReasons.LanguageMismatch
                : null;

            result.Add(new KnowledgePathClaimDto(
                item.ClaimId,
                item.ClaimCode,
                claim?.ClaimName,
                text,
                qualifier,
                version?.Id,
                version?.CountryVersion,
                version?.Status,
                reason is null,
                reason,
                KnowledgePathMapper.ToDto(item.Arrangement)!));
        }

        return result;
    }

    private async Task<IReadOnlyList<KnowledgePathChainConformanceDto>> ConformanceAsync(
        Guid tenantId, KnowledgePath path, ConceptChainTemplate template, CancellationToken ct)
    {
        var active = path.ActiveSteps().ToList();
        var result = new List<KnowledgePathChainConformanceDto>();
        foreach (var branch in ChainArrangementOrder.OrderedBranches(template))
        {
            foreach (var step in branch.Steps)
            {
                var slot = new KnowledgePathArrangement { BranchCode = branch.BranchCode, ChainStepId = step.ConceptTypeId };
                var count = active.Count(s => ChainSlots.SameSlot(s.Arrangement, slot));
                var status = count < step.MinSelection ? KnowledgePathConformanceStatuses.Under
                    : step.MaxSelection is { } max && count > max ? KnowledgePathConformanceStatuses.Over
                    : KnowledgePathConformanceStatuses.Ok;
                var type = _types is null ? null : await _types.GetByIdAsync(tenantId, step.ConceptTypeId, ct);
                result.Add(new KnowledgePathChainConformanceDto(
                    branch.BranchCode, step.ConceptTypeId, type?.ConceptTypeName, count, step.MinSelection,
                    step.MaxSelection, status));
            }
        }

        return result;
    }

    private static string? InLanguage(IEnumerable<ClaimLocalizedText>? texts, string? language)
        => texts?.FirstOrDefault(t => !string.IsNullOrWhiteSpace(t.Text)
                                      && ChainContextValidation.SameLanguage(t.LanguageCode, language))?.Text;
}
