using Diten.Platform.Domain.Enums;
using FluentValidation;

namespace Diten.Platform.Application.Features.ModulePages.Validators;

public abstract class ModulePageActionDescriptorRequestValidator<T> : AbstractValidator<T>
{
    protected ModulePageActionDescriptorRequestValidator(
        Func<T, string?> actionCode,
        Func<T, string?> displayName,
        Func<T, string?> permissionKey,
        Func<T, string?> actionType,
        Func<T, string?> status,
        Func<T, int?> sortOrder)
    {
        RuleFor(x => actionCode(x))
            .Cascade(CascadeMode.Stop)
            .NotEmpty().WithMessage("ActionCode is required.")
            .Must(value =>
            {
                var normalized = ModulePageDescriptorNormalizer.NormalizePageCode(value);
                return normalized.Length is >= 2 and <= 80
                    && normalized.All(ch => char.IsAsciiLetterOrDigit(ch) || ch == '_');
            }).WithMessage("ActionCode must use uppercase letters, numbers and underscores.");

        RuleFor(x => displayName(x))
            .NotEmpty().WithMessage("DisplayName is required.")
            .MaximumLength(200).WithMessage("DisplayName cannot exceed 200 characters.");

        RuleFor(x => permissionKey(x))
            .Cascade(CascadeMode.Stop)
            .NotEmpty().WithMessage("PermissionKey is required.")
            .Must(value => ModulePageDescriptorNormalizer.NormalizePermission(value).Length <= 200)
            .WithMessage("PermissionKey cannot exceed 200 characters.")
            .Must(IsCanonicalPermission)
            .WithMessage("PermissionKey must use module.resource.action format.");

        RuleFor(x => actionType(x))
            .Cascade(CascadeMode.Stop)
            .NotEmpty().WithMessage("ActionType is required.")
            .Must(value => Enum.GetNames<ModulePageActionType>().Contains(value, StringComparer.Ordinal))
            .WithMessage("ActionType must be View, Toolbar, RowAction, FormAction, BulkAction, System or Custom.");

        RuleFor(x => status(x))
            .Cascade(CascadeMode.Stop)
            .NotEmpty().WithMessage("Status is required.")
            .Must(value => Enum.GetNames<ModulePageActionStatus>().Contains(value, StringComparer.Ordinal))
            .WithMessage("Status must be Draft, Active, Inactive or Deprecated.");

        RuleFor(x => sortOrder(x))
            .GreaterThanOrEqualTo(0).When(x => sortOrder(x).HasValue)
            .WithMessage("SortOrder must be greater than or equal to 0.");
    }

    private static bool IsCanonicalPermission(string? value)
    {
        var permission = ModulePageDescriptorNormalizer.NormalizePermission(value);
        var parts = permission.Split('.', StringSplitOptions.RemoveEmptyEntries);
        // PKS-001 §1: module.resource.action is the minimum; deeper resource paths (>= 3 segments) are valid.
        // AG-STEP-004B Slice 2 (D-6): relaxed from `== 3` to `>= 3`. All other grammar rules are unchanged.
        return parts.Length >= 3
            && parts.All(part => part.Length > 0
                && char.IsAsciiLetterLower(part[0])
                && part.All(ch => char.IsAsciiLetterOrDigit(ch) || ch == '-'));
    }
}
