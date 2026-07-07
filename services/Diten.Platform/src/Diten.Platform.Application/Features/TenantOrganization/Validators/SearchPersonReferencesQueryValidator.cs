using Diten.Platform.Application.Features.TenantOrganization.Handlers.QueryHandlers;
using Diten.Platform.Application.Features.TenantOrganization.Queries;
using FluentValidation;

namespace Diten.Platform.Application.Features.TenantOrganization.Validators;

public sealed class SearchPersonReferencesQueryValidator : AbstractValidator<SearchPersonReferencesQuery>
{
    public SearchPersonReferencesQueryValidator()
    {
        RuleFor(x => x.Query).MaximumLength(160);
        RuleFor(x => x.Status).MaximumLength(32);
        RuleFor(x => x.Page).GreaterThanOrEqualTo(0);
        RuleFor(x => x.PageSize).InclusiveBetween(0, SearchPersonReferencesQueryHandler.MaxPageSize);
    }
}
