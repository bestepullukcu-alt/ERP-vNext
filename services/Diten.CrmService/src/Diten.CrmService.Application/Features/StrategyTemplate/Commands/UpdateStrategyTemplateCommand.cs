using Diten.CrmService.Application.Common.Models;
using MediatR;

namespace Diten.CrmService.Application.Features.StrategyTemplate.Commands;

/// <summary>
/// Updates a strategy template. <c>TemplateCode</c> and <c>SubjectType</c> are absent because they are immutable, and
/// <c>TemplateStatus</c> is absent because the lifecycle moves only through activate / archive.
/// <para>Each binding list is NULLABLE and null means "leave this binding alone". That is what makes editing the
/// metadata of an ACTIVE (frozen) template possible without tripping the freeze guard; sending the same bindings back
/// is also fine, because the guard compares what the play BINDS, not the ids the payload arrived with.</para>
/// </summary>
public sealed record UpdateStrategyTemplateCommand(
    Guid TemplateId,
    string TemplateName,
    DateTimeOffset EffectiveFrom,
    DateTimeOffset? EffectiveTo,
    string? BusinessUnitId,
    string? Description,
    string? Notes,
    IReadOnlyList<StrategyTemplateSegmentBindingInput>? SegmentBindings,
    StrategyTemplateFrequencyIntentInput? FrequencyIntent,
    IReadOnlyList<StrategyTemplateProductLineInput>? ProductLines,
    IReadOnlyList<StrategyTemplateContentBindingInput>? ContentBindings,
    int? ExpectedVersion) : IRequest<Response<bool>>;
