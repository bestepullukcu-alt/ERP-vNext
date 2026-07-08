using Diten.Platform.Application.Features.TenantOrganization.Queries;
using FluentValidation;

namespace Diten.Platform.Application.Features.TenantOrganization.Validators;

public sealed class GetPersonReferenceByIdQueryValidator : AbstractValidator<GetPersonReferenceByIdQuery>
{
    public GetPersonReferenceByIdQueryValidator()
    {
        RuleFor(x => x.PersonId).NotEmpty();
    }
}
