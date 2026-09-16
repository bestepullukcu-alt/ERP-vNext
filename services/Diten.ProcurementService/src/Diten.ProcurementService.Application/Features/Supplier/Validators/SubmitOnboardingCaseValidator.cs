using Diten.ProcurementService.Application.Features.Supplier.Commands;
using FluentValidation;

namespace Diten.ProcurementService.Application.Features.Supplier.Validators;

/// <summary>
/// submitOnboardingCase validation (MOD-0140 §12): onaya gönderimde KYC legalName zorunlu + en az 1 document.
/// Her document type + evidenceRef dolu olmalı (binary yok, yalnız referans).
/// </summary>
public sealed class SubmitOnboardingCaseValidator : AbstractValidator<SubmitOnboardingCaseCommand>
{
    public SubmitOnboardingCaseValidator()
    {
        RuleFor(x => x.SupplierId).NotEmpty();

        When(x => x.SubmitForApproval == true, () =>
        {
            RuleFor(x => x.Kyc)
                .NotNull().WithMessage("onaya gönderim için KYC zorunlu");

            RuleFor(x => x.Kyc!.LegalName)
                .NotEmpty().WithMessage("KYC legalName zorunlu")
                .When(x => x.Kyc is not null);

            RuleFor(x => x.Documents)
                .NotNull().WithMessage("onaya gönderim için en az 1 document zorunlu")
                .Must(d => d is not null && d.Count >= 1).WithMessage("onaya gönderim için en az 1 document zorunlu");
        });

        RuleForEach(x => x.Documents).ChildRules(d =>
        {
            d.RuleFor(doc => doc.Type).NotEmpty().WithMessage("document.type zorunlu");
            d.RuleFor(doc => doc.EvidenceRef).NotEmpty().WithMessage("document.evidenceRef zorunlu");
        });
    }
}
