using System.Text.RegularExpressions;
using Diten.Platform.Application.Features.TimeEntry.Commands;
using FluentValidation;

namespace Diten.Platform.Application.Features.TimeEntry.Validators;

/// <summary>MOD-0280-FU01 §4.6 / §12 — a tenant category's shape.</summary>
public sealed class CreateWorkCategoryValidator : AbstractValidator<CreateWorkCategoryCommand>
{
    public CreateWorkCategoryValidator()
    {
        RuleFor(x => x.Request.Code)
            .Must(code => code is not null && WorkCategoryRules.CodePattern.IsMatch(code.Trim()))
            .WithErrorCode(TimeEntryReasonCodes.CategoryCodeInvalid)
            .WithMessage("The code is 2–32 characters: an upper-case letter, then upper-case letters, digits or '_'.");

        // D10 — leave and absence are a separate module in every comparable system; they cannot hide in here.
        RuleFor(x => x.Request.Code)
            .Must(code => !WorkCategoryRules.IsReserved(code))
            .WithErrorCode(TimeEntryReasonCodes.CategoryCodeReserved)
            .WithMessage("Leave and absence are not work categories.")
            .When(x => x.Request.Code is not null && WorkCategoryRules.CodePattern.IsMatch(x.Request.Code.Trim()));

        WorkCategoryRules.AddCommonRules(this, x => x.Request.LabelText, x => x.Request.Description, x => x.Request.SortOrder);
    }
}

/// <summary>The rules create and update share.</summary>
public static class WorkCategoryRules
{
    public static readonly Regex CodePattern = new("^[A-Z][A-Z0-9_]{1,31}$", RegexOptions.Compiled | RegexOptions.CultureInvariant);

    /// <summary>Codes that would turn the catalogue into a leave/absence register (D10).</summary>
    private static readonly HashSet<string> Reserved = new(StringComparer.Ordinal)
    {
        "LEAVE", "ABSENCE", "VACATION", "HOLIDAY", "SICK", "SICK_LEAVE", "ANNUAL_LEAVE", "PUBLIC_HOLIDAY", "OTHER"
    };

    public static bool IsReserved(string? code) => code is not null && Reserved.Contains(code.Trim());

    public static void AddCommonRules<T>(
        AbstractValidator<T> validator,
        System.Linq.Expressions.Expression<Func<T, string?>> label,
        System.Linq.Expressions.Expression<Func<T, string?>> description,
        System.Linq.Expressions.Expression<Func<T, int>> sortOrder)
    {
        validator.RuleFor(label)
            .Must(text => !string.IsNullOrWhiteSpace(text) && text.Trim().Length <= TimeEntryLimits.CategoryLabelMaxLength)
            .WithErrorCode(TimeEntryReasonCodes.CategoryLabelRequired)
            .WithMessage("A category needs a label (at most 200 characters).");

        validator.RuleFor(description)
            .Must(text => text is null || text.Trim().Length <= TimeEntryLimits.CategoryDescriptionMaxLength)
            .WithErrorCode(TimeEntryReasonCodes.CategoryDescriptionTooLong);

        validator.RuleFor(sortOrder)
            .InclusiveBetween(0, 999)
            .WithErrorCode(TimeEntryReasonCodes.CategorySortOrderInvalid);
    }
}
