using Diten.ProcurementService.Application.Features.Supplier.Commands;
using FluentValidation;

namespace Diten.ProcurementService.Application.Features.Supplier.Validators;

/// <summary>createSupplier validation (MOD-0140 §12): Name zorunlu/trim/max 200; contact.type enum.</summary>
public sealed class CreateSupplierValidator : AbstractValidator<CreateSupplierCommand>
{
    public CreateSupplierValidator()
    {
        RuleFor(x => x.Name)
            .NotEmpty().WithMessage("name zorunlu")
            .MaximumLength(200);

        RuleFor(x => x.TaxId).MaximumLength(64);
        RuleFor(x => x.Country).MaximumLength(8);

        RuleForEach(x => x.Contacts).ChildRules(c =>
        {
            c.RuleFor(ct => ct.Type)
                .NotEmpty()
                .Must(SupplierValidationRules.IsValidContactType)
                .WithMessage("contact.type geçersiz (primary|billing|quality|logistics)");
        });
    }
}
