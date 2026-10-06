using Diten.ProcurementService.Application.Features.InvoiceMatch.Commands;
using FluentValidation;

namespace Diten.ProcurementService.Application.Features.InvoiceMatch.Validators;

/// <summary>
/// resolveMatchException validation (MOD-0143 §12): decision zorunlu ve enum(approve/reject/tolerance-override).
/// (Exception Open olma kuralı handler'da — kapalı exception resolve → 409 INVALID_STATE.)
/// </summary>
public sealed class ResolveMatchExceptionValidator : AbstractValidator<ResolveMatchExceptionCommand>
{
    public ResolveMatchExceptionValidator()
    {
        RuleFor(x => x.Decision)
            .NotEmpty().WithMessage("decision zorunlu")
            .Must(InvoiceMatchValidationRules.IsValidDecision)
            .WithMessage("decision geçersiz (approve/reject/tolerance-override)");
    }
}
