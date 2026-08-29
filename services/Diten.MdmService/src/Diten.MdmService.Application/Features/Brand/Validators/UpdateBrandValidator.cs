using Diten.MdmService.Application.Features.Brand.Commands;
using FluentValidation;

namespace Diten.MdmService.Application.Features.Brand.Validators;

public sealed class UpdateBrandValidator : AbstractValidator<UpdateBrandCommand>
{
    public UpdateBrandValidator()
    {
        RuleFor(x => x.BrandId).NotEmpty();
        RuleFor(x => x.Request).NotNull().SetValidator(new BrandWriteRequestValidator());
    }
}
