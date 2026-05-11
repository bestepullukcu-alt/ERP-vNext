using System.Text.RegularExpressions;
using Diten.Platform.Domain.Enums;
using FluentValidation;

namespace Diten.Platform.Application.Features.ModuleCatalog.Validators;

public abstract class ModuleCatalogItemRequestValidator<T> : AbstractValidator<T>
{
    protected ModuleCatalogItemRequestValidator(
        Func<T, string?> moduleCode,
        Func<T, string?> moduleName,
        Func<T, string?> displayName,
        Func<T, string?> domain,
        Func<T, string?> service,
        Func<T, string?> status,
        Func<T, string?> version,
        Func<T, int?> sortOrder)
    {
        RuleFor(x => moduleCode(x))
            .Cascade(CascadeMode.Stop)
            .NotEmpty().WithMessage("ModuleCode is required.")
            .Must(code =>
            {
                var normalized = ModuleCatalogCodeNormalizer.Normalize(code);
                return normalized.Length is >= 2 and <= 80;
            }).WithMessage("ModuleCode must be between 2 and 80 characters after canonical normalization.")
            .Must(code =>
            {
                var normalized = ModuleCatalogCodeNormalizer.Normalize(code);
                return Regex.IsMatch(normalized, @"^[A-Z0-9]+(-[A-Z0-9]+)*$");
            }).WithMessage("ModuleCode must be uppercase, dash-separated, and use only A-Z, 0-9, and single dashes.");

        RuleFor(x => moduleName(x))
            .NotEmpty().WithMessage("ModuleName is required.")
            .MaximumLength(200).WithMessage("ModuleName cannot exceed 200 characters.");

        RuleFor(x => displayName(x))
            .NotEmpty().WithMessage("DisplayName is required.")
            .MaximumLength(200).WithMessage("DisplayName cannot exceed 200 characters.");

        RuleFor(x => domain(x))
            .NotEmpty().WithMessage("Domain is required.")
            .MaximumLength(120).WithMessage("Domain cannot exceed 120 characters.");

        RuleFor(x => service(x))
            .NotEmpty().WithMessage("Service is required.")
            .MaximumLength(120).WithMessage("Service cannot exceed 120 characters.");

        RuleFor(x => status(x))
            .Cascade(CascadeMode.Stop)
            .NotEmpty().WithMessage("Status is required.")
            .Must(value => Enum.GetNames<ModuleCatalogStatus>().Contains(value, StringComparer.Ordinal))
            .WithMessage("Status must be one of: Draft, Active, Inactive, Deprecated.");

        RuleFor(x => version(x))
            .Cascade(CascadeMode.Stop)
            .NotEmpty().WithMessage("Module version is required.")
            .Matches(@"^\d+\.\d+\.\d+$").WithMessage("Module version must use semantic major.minor.patch format.");

        RuleFor(x => sortOrder(x))
            .GreaterThanOrEqualTo(0).When(x => sortOrder(x).HasValue)
            .WithMessage("SortOrder must be greater than or equal to 0.");
    }
}
