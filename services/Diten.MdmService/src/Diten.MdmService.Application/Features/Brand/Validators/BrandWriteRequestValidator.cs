using Diten.MdmService.Application.Features.BrandProductContract;
using FluentValidation;

namespace Diten.MdmService.Application.Features.Brand.Validators;

// Field-shape rules only. Vocabulary, uniqueness, immutability, archive state and the effective window are
// business rules and stay in the handlers, where they can return their own reason code and status.
//
// WP-MDM-BRAND-ACCEPT-01 FIX2 (M5) — every rule answers "field_invalid: {Field} — {detail}" (BrandProductFieldRefusal), so
// the brand form can name the field in the reader's language instead of showing a status sentence or MDM's English.
public sealed class BrandWriteRequestValidator : AbstractValidator<BrandWriteRequest>
{
    public BrandWriteRequestValidator()
    {
        RuleFor(x => x.BrandCode)
            .NotEmpty().WithMessage(BrandProductFieldRefusal.Required("BrandCode"))
            .MaximumLength(64).WithMessage(BrandProductFieldRefusal.TooLong("BrandCode", 64))
            .Matches("^[A-Za-z0-9._-]+$")
            .WithMessage(BrandProductFieldRefusal.Message("BrandCode", "may contain letters, digits, dot, underscore and hyphen only."));

        RuleFor(x => x.BrandName)
            .NotEmpty().WithMessage(BrandProductFieldRefusal.Required("BrandName"))
            .MaximumLength(200).WithMessage(BrandProductFieldRefusal.TooLong("BrandName", 200));
        RuleFor(x => x.BrandStatus)
            .NotEmpty().WithMessage(BrandProductFieldRefusal.Required("BrandStatus"))
            .MaximumLength(32).WithMessage(BrandProductFieldRefusal.TooLong("BrandStatus", 32));
        RuleFor(x => x.Description)
            .MaximumLength(2000).WithMessage(BrandProductFieldRefusal.TooLong("Description", 2000));
        RuleFor(x => x.EffectiveFrom)
            .NotEmpty().WithMessage(BrandProductFieldRefusal.Required("EffectiveFrom"));

        RuleForEach(x => x.ExternalReferences).ChildRules(reference =>
        {
            reference.RuleFor(x => x.SourceSystem)
                .NotEmpty().WithMessage(BrandProductFieldRefusal.Required("SourceSystem"))
                .MaximumLength(100).WithMessage(BrandProductFieldRefusal.TooLong("SourceSystem", 100));
            reference.RuleFor(x => x.ExternalId)
                .NotEmpty().WithMessage(BrandProductFieldRefusal.Required("ExternalId"))
                .MaximumLength(200).WithMessage(BrandProductFieldRefusal.TooLong("ExternalId", 200));
            reference.RuleFor(x => x.ExternalCode)
                .MaximumLength(100).WithMessage(BrandProductFieldRefusal.TooLong("ExternalCode", 100));
            reference.RuleFor(x => x.ExternalName)
                .MaximumLength(200).WithMessage(BrandProductFieldRefusal.TooLong("ExternalName", 200));
        });
    }
}
