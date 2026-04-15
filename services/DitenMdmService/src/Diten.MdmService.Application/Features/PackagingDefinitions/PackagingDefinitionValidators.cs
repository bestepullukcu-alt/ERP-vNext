using FluentValidation;
using Diten.MdmService.Application.Interfaces;

namespace Diten.MdmService.Application.Features.PackagingDefinitions;

public sealed class CreatePackagingDefinitionValidator : AbstractValidator<CreatePackagingDefinitionRequest>
{
    public CreatePackagingDefinitionValidator(IPackagingDefinitionRepository repository)
    {
        RuleFor(x => x.Code)
            .NotEmpty().WithMessage("Code is required.")
            .MaximumLength(50).WithMessage("Code cannot exceed 50 characters.")
            .MustAsync(async (code, ct) => !await repository.ExistsByCodeAsync(code, ct: ct))
            .WithMessage("Packaging code must be unique.");

        RuleFor(x => x.Name)
            .NotEmpty().WithMessage("Name is required.")
            .MaximumLength(150).WithMessage("Name cannot exceed 150 characters.");

        RuleFor(x => x.UnitsPerPack)
            .GreaterThan(0).WithMessage("Units per pack must be greater than zero.");
    }
}

public sealed class UpdatePackagingDefinitionValidator : AbstractValidator<UpdatePackagingDefinitionRequest>
{
    public UpdatePackagingDefinitionValidator(IPackagingDefinitionRepository repository)
    {
        RuleFor(x => x.Id).NotEmpty().WithMessage("Id is required.");

        RuleFor(x => x.Code)
            .NotEmpty().WithMessage("Code is required.")
            .MaximumLength(50).WithMessage("Code cannot exceed 50 characters.")
            .MustAsync(async (req, code, ct) => !await repository.ExistsByCodeAsync(code, req.Id, ct))
            .WithMessage("Packaging code must be unique.");

        RuleFor(x => x.Name)
            .NotEmpty().WithMessage("Name is required.")
            .MaximumLength(150).WithMessage("Name cannot exceed 150 characters.");

        RuleFor(x => x.UnitsPerPack)
            .GreaterThan(0).WithMessage("Units per pack must be greater than zero.");
    }
}
