using Diten.CrmService.Application.Common.Models;
using MediatR;

namespace Diten.CrmService.Application.Features.StrategyTemplate.Commands;

/// <summary>
/// Creates a strategy template. It is always born <c>draft</c> at business version 1 with its own lineage id, and it is
/// never born active: putting a play live is a separate endpoint and a separate permission (SoD). There is no TenantId
/// here — it is resolved server-side from the claim.
/// <para><c>TemplateStatus</c> is absent on purpose: the lifecycle moves only through the activate / archive endpoints,
/// so a status can never be set as a side effect of an edit.</para>
/// </summary>
public sealed record CreateStrategyTemplateCommand(
    string TemplateCode,
    string TemplateName,
    string SubjectType,
    DateTimeOffset EffectiveFrom,
    DateTimeOffset? EffectiveTo,
    string? BusinessUnitId,
    string? Description,
    string? Notes,
    IReadOnlyList<StrategyTemplateSegmentBindingInput>? SegmentBindings,
    StrategyTemplateFrequencyIntentInput? FrequencyIntent,
    IReadOnlyList<StrategyTemplateProductLineInput>? ProductLines,
    IReadOnlyList<StrategyTemplateContentBindingInput>? ContentBindings,
    // WP-ST-SCOPE — the play's scope (tenant / country / legal-entity / business-unit). Optional with defaults so a
    // caller written against the pre-scope contract keeps compiling: an absent ScopeType with a BusinessUnitId derives
    // to business-unit and with nothing to tenant, exactly the context those callers already had.
    string? ScopeType = null,
    string? CountryScope = null,
    Guid? LegalEntityId = null) : IRequest<Response<Guid>>;
