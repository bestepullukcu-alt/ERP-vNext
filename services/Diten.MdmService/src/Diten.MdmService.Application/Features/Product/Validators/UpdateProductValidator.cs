using Diten.MdmService.Application.Features.Product.Commands;
using FluentValidation;

namespace Diten.MdmService.Application.Features.Product.Validators;

public sealed class UpdateProductValidator : AbstractValidator<UpdateProductCommand>
{
    public UpdateProductValidator()
    {
        RuleFor(x => x.ProductId).NotEmpty();
        RuleFor(x => x.Request).NotNull().SetValidator(new ProductWriteRequestValidator());
    }
}
