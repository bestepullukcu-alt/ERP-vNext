using Diten.InventoryService.Application.Features.CycleCounts.Commands;
using FluentValidation;

namespace Diten.InventoryService.Application.Features.CycleCounts.Validators;

/// <summary>Pack §12 — the shape of the Draft form on update (the same rules as create).</summary>
public sealed class UpdateCycleCountValidator : AbstractValidator<UpdateCycleCountCommand>
{
    public UpdateCycleCountValidator() => RuleFor(x => x.Count).Custom((form, context) => CycleCountFormShape.Check(form, context));
}
