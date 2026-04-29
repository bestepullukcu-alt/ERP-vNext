using Diten.Platform.Application.Features.ModuleCatalog.Queries;
using Diten.Platform.Domain.Enums;
using FluentValidation;

namespace Diten.Platform.Application.Features.ModuleCatalog.Validators;

public sealed class GetModuleCatalogItemsQueryValidator : AbstractValidator<GetModuleCatalogItemsQuery>
{
    public GetModuleCatalogItemsQueryValidator()
    {
        RuleFor(x => x.Filter.Page).GreaterThan(0);
        RuleFor(x => x.Filter.PageSize).InclusiveBetween(1, 200);
        RuleFor(x => x.Filter.Status)
            .Must(value => string.IsNullOrWhiteSpace(value) || Enum.GetNames<ModuleCatalogStatus>().Contains(value, StringComparer.Ordinal))
            .WithMessage("Status must be one of: Draft, Active, Inactive, Deprecated.");
    }
}
