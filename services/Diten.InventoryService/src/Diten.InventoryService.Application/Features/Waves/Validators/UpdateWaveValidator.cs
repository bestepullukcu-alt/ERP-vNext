using Diten.InventoryService.Application.Features.Waves.Commands;
using FluentValidation;

namespace Diten.InventoryService.Application.Features.Waves.Validators;

/// <summary>Pack §12 — the shape of the Draft form on update (the same rules as create) and the optional reason.</summary>
public sealed class UpdateWaveValidator : AbstractValidator<UpdateWaveCommand>
{
    public UpdateWaveValidator() => RuleFor(x => x).Custom((command, context) =>
    {
        WaveFormShape.Check(command.Wave, context);
        WaveFormShape.OptionalReason(command.Reason, context);
    });
}
