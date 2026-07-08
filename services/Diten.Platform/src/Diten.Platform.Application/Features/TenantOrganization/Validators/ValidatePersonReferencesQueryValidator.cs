using Diten.Platform.Application.Features.TenantOrganization.Queries;
using FluentValidation;

namespace Diten.Platform.Application.Features.TenantOrganization.Validators;

public sealed class ValidatePersonReferencesQueryValidator : AbstractValidator<ValidatePersonReferencesQuery>
{
    public ValidatePersonReferencesQueryValidator()
    {
        RuleFor(x => x.Request).NotNull();
        When(x => x.Request is not null, () =>
        {
            RuleFor(x => x.Request.PersonIds)
                .NotEmpty()
                .Must(ids => ids.Count <= 100)
                .WithMessage("At most 100 person references can be validated at once.");
            RuleForEach(x => x.Request.PersonIds).NotEmpty();
        });
    }
}
