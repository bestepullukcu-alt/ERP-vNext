using FluentValidation;

namespace Diten.CrmService.Application.Features.Territory.Nodes;

public sealed class CreateTerritoryNodeCommandValidator : AbstractValidator<CreateTerritoryNodeCommand>
{
    public CreateTerritoryNodeCommandValidator()
    {
        RuleFor(x => x.ModelId).NotEmpty();
        RuleFor(x => x.TerritoryCode).NotEmpty().MaximumLength(100);
        RuleFor(x => x.Name).NotEmpty().MaximumLength(200);
        RuleFor(x => x.TerritoryLevel).NotEmpty();
        RuleFor(x => x.EffectiveTo)
            .Must((cmd, to) => to is null || to.Value >= cmd.EffectiveFrom)
            .WithMessage("EffectiveTo must be on or after EffectiveFrom.");
    }
}

public sealed class UpdateTerritoryNodeCommandValidator : AbstractValidator<UpdateTerritoryNodeCommand>
{
    public UpdateTerritoryNodeCommandValidator()
    {
        RuleFor(x => x.ModelId).NotEmpty();
        RuleFor(x => x.Id).NotEmpty();
        RuleFor(x => x.TerritoryCode).NotEmpty().MaximumLength(100);
        RuleFor(x => x.Name).NotEmpty().MaximumLength(200);
        RuleFor(x => x.TerritoryLevel).NotEmpty();
        RuleFor(x => x.EffectiveTo)
            .Must((cmd, to) => to is null || to.Value >= cmd.EffectiveFrom)
            .WithMessage("EffectiveTo must be on or after EffectiveFrom.");
    }
}
