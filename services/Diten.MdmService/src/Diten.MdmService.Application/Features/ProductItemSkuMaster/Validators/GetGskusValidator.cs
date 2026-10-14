using Diten.MdmService.Application.Features.ProductItemSkuMaster.Queries;
using FluentValidation;

namespace Diten.MdmService.Application.Features.ProductItemSkuMaster.Validators;

public sealed class GetGskusValidator : AbstractValidator<GetGskusQuery>
{
    public GetGskusValidator()
    {
        // FIX4 item 9: bounded — an unbounded page number overflowed the skip into a negative one (a 500).
        RuleFor(x => x.PageNumber).GreaterThan(0).LessThanOrEqualTo(GetGskusQuery.MaxPageNumber);
        RuleFor(x => x.PageSize).InclusiveBetween(1, 100);
        RuleFor(x => x.Search).MaximumLength(100);
        RuleFor(x => x.LifecycleStatus).IsInEnum().When(x => x.LifecycleStatus.HasValue);
        RuleFor(x => x.OrderBy)
            .Must(orderBy => GetGskusQuery.SortableFields.ContainsKey(orderBy!))
            .When(x => !string.IsNullOrEmpty(x.OrderBy))
            .WithMessage("GSKU_LIST_ORDER_BY_INVALID");
        RuleFor(x => x.OrderDir)
            .Must(GetGskusQuery.IsKnownOrderDir)
            .WithMessage("GSKU_LIST_ORDER_DIR_INVALID");
    }
}
