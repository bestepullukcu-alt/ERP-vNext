using Diten.ProcurementService.Application.Features.Supplier.Commands;
using FluentValidation;

namespace Diten.ProcurementService.Application.Features.Supplier.Validators;

/// <summary>MOD-0140 DL-3: the same body rules as <see cref="UpdateSupplierValidator"/> (§12) for the master.update path.</summary>
public sealed class UpdateSupplierMasterValidator : AbstractValidator<UpdateSupplierMasterCommand>
{
    public UpdateSupplierMasterValidator()
    {
        RuleFor(x => x.SupplierId).NotEmpty();

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

/// <summary>1.4.0 relationship writes: a known status value; a payment-terms code of reasonable length (no bank data, K-8).</summary>
public sealed class ChangeRelationshipStatusValidator : AbstractValidator<ChangeRelationshipStatusCommand>
{
    public ChangeRelationshipStatusValidator()
    {
        RuleFor(x => x.SupplierId).NotEmpty();
        RuleFor(x => x.Status).IsInEnum();
    }
}

public sealed class SetRelationshipPaymentTermsValidator : AbstractValidator<SetRelationshipPaymentTermsCommand>
{
    public SetRelationshipPaymentTermsValidator()
    {
        RuleFor(x => x.SupplierId).NotEmpty();
        RuleFor(x => x.PaymentTermsCode).MaximumLength(64);
    }
}
