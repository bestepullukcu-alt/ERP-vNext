using Diten.CrmService.Application.Features.AccountContact.Commands;
using FluentValidation;

namespace Diten.CrmService.Application.Features.AccountContact.Validators;

public sealed class LinkContactToAccountValidator : AbstractValidator<LinkContactToAccountCommand>
{
    public LinkContactToAccountValidator()
    {
        RuleFor(x => x.AccountId).NotEmpty();
        RuleFor(x => x.ContactId).NotEmpty();
        RuleFor(x => x.RoleCode).NotEmpty();
        RuleFor(x => x.Notes!).MaximumLength(2000).When(x => x.Notes is not null);
        RuleFor(x => x)
            .Must(x => x.ValidFrom is null || x.ValidTo is null || x.ValidFrom <= x.ValidTo)
            .WithMessage("ValidFrom must be on or before ValidTo.")
            .WithName("Validity");
    }
}

public sealed class UpdateAccountContactLinkValidator : AbstractValidator<UpdateAccountContactLinkCommand>
{
    public UpdateAccountContactLinkValidator()
    {
        RuleFor(x => x.AccountId).NotEmpty();
        RuleFor(x => x.LinkId).NotEmpty();
        RuleFor(x => x.RoleCode).NotEmpty();
        RuleFor(x => x.Notes!).MaximumLength(2000).When(x => x.Notes is not null);
        RuleFor(x => x)
            .Must(x => x.ValidFrom is null || x.ValidTo is null || x.ValidFrom <= x.ValidTo)
            .WithMessage("ValidFrom must be on or before ValidTo.")
            .WithName("Validity");
    }
}
