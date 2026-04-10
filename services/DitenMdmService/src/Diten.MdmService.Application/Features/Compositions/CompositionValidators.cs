using FluentValidation;

namespace Diten.MdmService.Application.Features.Compositions;

public sealed class CreateCompositionCommandValidator : AbstractValidator<CreateCompositionCommand>
{
    public CreateCompositionCommandValidator()
    {
        Include(new CompositionUpsertRequestValidator());
    }
}

public sealed class UpdateCompositionCommandValidator : AbstractValidator<UpdateCompositionCommand>
{
    public UpdateCompositionCommandValidator()
    {
        RuleFor(x => x.Id).NotEmpty();
        Include(new CompositionUpsertRequestValidator());
    }
}

public sealed class ChangeCompositionLifecycleCommandValidator : AbstractValidator<ChangeCompositionLifecycleCommand>
{
    public ChangeCompositionLifecycleCommandValidator()
    {
        RuleFor(x => x.Id).NotEmpty();
        RuleFor(x => x.TargetStateId).NotEmpty();
    }
}

internal sealed class CompositionUpsertRequestValidator : AbstractValidator<CompositionUpsertRequestBase>
{
    public CompositionUpsertRequestValidator()
    {
        RuleFor(x => x.FormulationCode).NotEmpty().MaximumLength(64);
        RuleFor(x => x.Name).NotEmpty().MaximumLength(200);
        RuleFor(x => x.DosageFormId).NotEmpty();
        RuleFor(x => x.StrengthValue).GreaterThan(0);
        RuleFor(x => x.StrengthUnitId).NotEmpty();
        RuleFor(x => x.LifecycleStateId).NotEmpty();
        RuleFor(x => x.Components)
            .NotEmpty()
            .Must(components => components.Select(c => c.ComponentId).Distinct().Count() == components.Count)
            .WithMessage("Duplicate ingredients are not allowed in a single composition.");
        
        RuleForEach(x => x.Components).ChildRules(component =>
        {
            component.RuleFor(c => c.Sequence).GreaterThan(0);
            component.RuleFor(c => c.ComponentId).NotEmpty();
            component.RuleFor(c => c.ComponentName).NotEmpty();
            component.RuleFor(c => c.ComponentType).IsInEnum();
            component.RuleFor(c => c.Quantity).GreaterThan(0);
            component.RuleFor(c => c.UnitId).NotEmpty();
        });
    }
}
