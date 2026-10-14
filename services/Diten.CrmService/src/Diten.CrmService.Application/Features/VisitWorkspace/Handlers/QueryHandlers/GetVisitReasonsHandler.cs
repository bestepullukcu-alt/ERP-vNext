using Diten.CrmService.Application.Common;
using Diten.CrmService.Application.Common.Models;
using Diten.CrmService.Application.Common.ReferenceValidation;
using Diten.CrmService.Application.Features.VisitWorkspace.Queries;
using MediatR;

namespace Diten.CrmService.Application.Features.VisitWorkspace.Handlers.QueryHandlers;

/// <summary>
/// WP-VW-W2 (A1) — the reason list of the cancel / not-done / reschedule dialogs (Web and mobile), read from the
/// published <c>visit-outcome-reason</c> set in ONE read. Only active values whose <c>applies_to</c> lists the action;
/// the label is the value's <c>label_&lt;lang&gt;</c> attribute (English = its display name, the 4I rule). The set
/// unreadable ⇒ 503 (never a local fallback list).
/// </summary>
public sealed class GetVisitReasonsHandler : IRequestHandler<GetVisitReasonsQuery, Response<VisitReasonListDto>>
{
    private readonly ITenantContext _tenant;
    private readonly IReferenceDataCatalogReader _catalog;

    public GetVisitReasonsHandler(ITenantContext tenant, IReferenceDataCatalogReader catalog)
    {
        _tenant = tenant;
        _catalog = catalog;
    }

    public async Task<Response<VisitReasonListDto>> Handle(GetVisitReasonsQuery request, CancellationToken cancellationToken)
    {
        if (_tenant.TenantId is null)
        {
            return Response<VisitReasonListDto>.Fail("Tenant context is required.", 400);
        }

        var appliesTo = VisitReasonValidator.Trim(request.AppliesTo)?.ToLowerInvariant();
        if (appliesTo is not null && !VisitOutcomeReasons.IsKnownAppliesTo(appliesTo))
        {
            return Response<VisitReasonListDto>.Fail(
                new[]
                {
                    $"Unsupported appliesTo '{appliesTo}'. Known values: {string.Join(", ", VisitOutcomeReasons.AppliesToAll)}.",
                    VisitWorkspaceErrorCodes.AppliesToInvalid
                },
                400);
        }

        var language = NormalizeLanguage(request.Language);
        var set = await _catalog.GetPublishedValuesAsync(VisitOutcomeReasons.ReasonSet, cancellationToken);
        if (!set.IsPublished)
        {
            return Response<VisitReasonListDto>.Fail(
                new[]
                {
                    $"The reason list ('{VisitOutcomeReasons.ReasonSet}') could not be read.",
                    VisitWorkspaceErrorCodes.ReferenceDataUnavailable
                },
                503);
        }

        var items = set.Values
            .Where(v => v.IsActive && !v.IsDeprecated)
            .Where(v => appliesTo is null || VisitOutcomeReasons.Applies(v.Attributes, appliesTo))
            .Select(v => new VisitReasonDto(v.ValueCode, LabelOf(v, language), VisitOutcomeReasons.RequiresNote(v.Attributes)))
            .ToList();

        return Response<VisitReasonListDto>.Success(
            new VisitReasonListDto(VisitOutcomeReasons.ReasonSet, appliesTo, language, items));
    }

    /// <summary>The value's label in <paramref name="language"/>: <c>label_&lt;lang&gt;</c>, else the display name,
    /// else the code.</summary>
    public static string LabelOf(ReferenceValueSnapshot value, string language)
    {
        if (!string.Equals(language, "en", StringComparison.Ordinal)
            && value.Attributes is not null
            && value.Attributes.TryGetValue(VisitOutcomeReasons.LabelAttributePrefix + language, out var label)
            && !string.IsNullOrWhiteSpace(label))
        {
            return label.Trim();
        }

        return string.IsNullOrWhiteSpace(value.DisplayName) ? value.ValueCode : value.DisplayName.Trim();
    }

    /// <summary>The first two letters of a language tag ("tr-TR" → "tr"); en when absent.</summary>
    public static string NormalizeLanguage(string? language)
    {
        var raw = VisitReasonValidator.Trim(language);
        if (raw is null)
        {
            return "en";
        }

        var first = raw.Split(',', ';')[0].Trim();
        var tag = first.Split('-', '_')[0].Trim().ToLowerInvariant();
        return tag.Length == 2 && tag.All(char.IsLetter) ? tag : "en";
    }
}
