using Diten.Platform.Application.Features.Audit.Commands;
using Diten.Platform.Domain.Enums;
using FluentValidation;

namespace Diten.Platform.Application.Features.Audit.Validators;

public sealed class UpdateAuditRetentionValidator : AbstractValidator<UpdateAuditRetentionCommand>
{
    public UpdateAuditRetentionValidator()
    {
        RuleFor(x => x.Request.PolicyId)
            .NotEmpty().WithMessage("Retention policy id is required.");

        RuleFor(x => x.Request.Category)
            .NotEmpty().WithMessage("Retention category is required.")
            .Must(IsValidCategory).WithMessage("Retention category is invalid.");

        RuleFor(x => x.Request.PlanTierCode)
            .NotEmpty().WithMessage("Plan tier code is required.")
            .MaximumLength(80);

        RuleFor(x => x.Request.MinimumRetentionDays)
            .GreaterThan(0).WithMessage("MinimumRetentionDays must be greater than zero.");

        RuleFor(x => x.Request.MaximumRetentionDays)
            .GreaterThan(0).WithMessage("MaximumRetentionDays must be greater than zero.")
            .GreaterThanOrEqualTo(x => x.Request.MinimumRetentionDays)
            .WithMessage("MaximumRetentionDays must be greater than or equal to MinimumRetentionDays.");

        RuleFor(x => x.Request.DefaultRetentionDays)
            .GreaterThan(0).WithMessage("DefaultRetentionDays must be greater than zero.")
            .Must((command, value) =>
                value >= command.Request.MinimumRetentionDays
                && value <= command.Request.MaximumRetentionDays)
            .WithMessage("DefaultRetentionDays must be within the floor and ceiling.");

        RuleFor(x => x.Request.HotStorageDays)
            .GreaterThan(0).WithMessage("HotStorageDays must be greater than zero.")
            .LessThanOrEqualTo(x => x.Request.DefaultRetentionDays)
            .WithMessage("HotStorageDays cannot exceed DefaultRetentionDays.");
    }

    private static bool IsValidCategory(string value)
    {
        return Enum.TryParse<AuditCategory>(value.Trim(), ignoreCase: true, out var parsed)
               && Enum.IsDefined(parsed)
               && parsed != AuditCategory.Unknown;
    }
}
