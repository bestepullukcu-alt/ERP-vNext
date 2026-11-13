using Diten.InventoryService.Application.Features.Waves.Commands;
using Diten.InventoryService.Domain.WavePlanning.Enums;
using FluentValidation;

namespace Diten.InventoryService.Application.Features.Waves.Validators;

/// <summary>Pack §12 — the shape of an add: 1..200 distinct outbound ids, <c>via</c> Manual | Suggestion, an optional reason.</summary>
public sealed class AddWaveOrdersValidator : AbstractValidator<AddWaveOrdersCommand>
{
    public AddWaveOrdersValidator() => RuleFor(x => x).Custom((command, context) =>
    {
        WaveFormShape.OutboundIds(command.OutboundIds, context);
        if (command.Via is not null && (!Enum.TryParse<WaveOrderAddedVia>(command.Via, false, out var via) || !Enum.IsDefined(via) || command.Via != via.ToString()))
            WaveFormShape.Fail(context, "via must be Manual or Suggestion.");
        WaveFormShape.OptionalReason(command.Reason, context);
    });
}
