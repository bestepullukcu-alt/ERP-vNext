using Diten.Platform.Application.Features.Tenants.Queries;
using FluentValidation;

namespace Diten.Platform.Application.Features.Tenants.Validators;

public sealed class GetTenantsQueryValidator : AbstractValidator<GetTenantsQuery>
{
    public GetTenantsQueryValidator()
    {
        RuleFor(x => x.Page).GreaterThanOrEqualTo(1);
        RuleFor(x => x.PageSize).InclusiveBetween(1, 200);
        RuleFor(x => x.Sort).NotEmpty().MaximumLength(64);
    }
}
