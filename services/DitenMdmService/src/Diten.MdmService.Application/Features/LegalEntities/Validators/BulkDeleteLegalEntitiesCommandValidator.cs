using FluentValidation;
using Diten.MdmService.Application.Features.LegalEntities.Commands;

namespace Diten.MdmService.Application.Features.LegalEntities.Validators;

public sealed class BulkDeleteLegalEntitiesCommandValidator : AbstractValidator<BulkDeleteLegalEntitiesCommand>
{
    public BulkDeleteLegalEntitiesCommandValidator()
    {
        RuleFor(x => x.Ids)
            .NotNull().WithMessage("LegalEntity.Validation.IdRequired")
            .NotEmpty().WithMessage("LegalEntity.Validation.IdRequired");
    }
}
