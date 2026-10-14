using FluentValidation;
using Diten.MdmService.Application.Features.ProductAbbreviationRegister.WorkItems.Queries;

namespace Diten.MdmService.Application.Features.ProductAbbreviationRegister.WorkItems.Validators;

public sealed class GetProductAbbreviationWorkItemsValidator
    : AbstractValidator<GetProductAbbreviationWorkItemsQuery>
{
    public GetProductAbbreviationWorkItemsValidator()
    {
        RuleFor(x => x.Scope).Must(x => x is "self" or "team");
        RuleFor(x => x.Limit).InclusiveBetween(1, ProductAbbreviationWorkItemContract.MaximumItems);
    }
}
