using Diten.HcmService.Application.Features.CoreHrEmployeeMaster.Queries;
using FluentValidation;

namespace Diten.HcmService.Application.Features.CoreHrEmployeeMaster.Validators;

public sealed class SearchEmployeesValidator : AbstractValidator<SearchEmployeesQuery>
{
    private static readonly HashSet<string> AllowedSortDirections = new(StringComparer.OrdinalIgnoreCase)
    {
        "asc",
        "desc"
    };

    public SearchEmployeesValidator()
    {
        RuleFor(query => query.Search)
            .MaximumLength(100)
            .When(query => !string.IsNullOrWhiteSpace(query.Search));

        RuleFor(query => query.EmployeeStatus)
            .MaximumLength(50)
            .When(query => !string.IsNullOrWhiteSpace(query.EmployeeStatus));

        RuleFor(query => query.WorkerType)
            .MaximumLength(50)
            .When(query => !string.IsNullOrWhiteSpace(query.WorkerType));

        RuleFor(query => query.EmploymentType)
            .MaximumLength(50)
            .When(query => !string.IsNullOrWhiteSpace(query.EmploymentType));

        RuleFor(query => query.SortDirection)
            .Must(value => string.IsNullOrWhiteSpace(value) || AllowedSortDirections.Contains(value))
            .WithMessage("Sort direction must be asc or desc.");
    }
}
