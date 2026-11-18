using Diten.InventoryService.Application.Features.Waves.Commands;
using FluentValidation;

namespace Diten.InventoryService.Application.Features.Waves.Validators;

/// <summary>Pack §12 — the shape of a remove: 1..200 distinct outbound ids and an optional reason.</summary>
public sealed class RemoveWaveOrdersValidator : AbstractValidator<RemoveWaveOrdersCommand>
{
    public RemoveWaveOrdersValidator() => RuleFor(x => x).Custom((command, context) =>
    {
        WaveFormShape.OutboundIds(command.OutboundIds, context);
        WaveFormShape.OptionalReason(command.Reason, context);
    });
}
