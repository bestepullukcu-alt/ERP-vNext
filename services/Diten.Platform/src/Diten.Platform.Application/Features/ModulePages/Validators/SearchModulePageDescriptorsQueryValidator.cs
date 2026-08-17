using Diten.Platform.Application.Features.ModulePages.Queries;
using Diten.Platform.Domain.Enums;
using FluentValidation;

namespace Diten.Platform.Application.Features.ModulePages.Validators;

public sealed class SearchModulePageDescriptorsQueryValidator : AbstractValidator<SearchModulePageDescriptorsQuery>
{
    public SearchModulePageDescriptorsQueryValidator()
    {
        RuleFor(x => x.Filter.Page)
            .GreaterThanOrEqualTo(1).WithMessage("Page must be greater than or equal to 1.");

        RuleFor(x => x.Filter.PageSize)
            .InclusiveBetween(1, 200).WithMessage("PageSize must be between 1 and 200.");

        RuleForEach(x => SplitValues(x.Filter.PageType))
            .Must(value => Enum.GetNames<ModulePageType>().Contains(value, StringComparer.Ordinal))
            .WithMessage("PageType contains an invalid value.");

        RuleForEach(x => SplitValues(x.Filter.Status))
            .Must(value => Enum.GetNames<ModulePageStatus>().Contains(value, StringComparer.Ordinal))
            .WithMessage("Status contains an invalid value.");
    }

    private static IReadOnlyList<string> SplitValues(string? value) =>
        string.IsNullOrWhiteSpace(value)
            ? []
            : value.Split(',', StringSplitOptions.TrimEntries | StringSplitOptions.RemoveEmptyEntries);
}
