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
    IReadOnlyList<StrategyTemplateContentBindingInput>? ContentBindings) : IRequest<Response<Guid>>;
