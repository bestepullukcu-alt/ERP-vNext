using Diten.MdmService.Application.Features.Brand.Commands;
using FluentValidation;

namespace Diten.MdmService.Application.Features.Brand.Validators;

public sealed class CreateBrandValidator : AbstractValidator<CreateBrandCommand>
{
    public CreateBrandValidator()
    {
        RuleFor(x => x.Request).NotNull().SetValidator(new BrandWriteRequestValidator());
    }
}
