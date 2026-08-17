using Diten.Platform.Application.Features.Audit.Queries;
using Diten.Platform.Domain.Enums;
using FluentValidation;

namespace Diten.Platform.Application.Features.Audit.Validators;

public sealed class ExportAuditEventsValidator : AbstractValidator<ExportAuditEventsQuery>
{
    public ExportAuditEventsValidator()
    {
        RuleFor(x => x.Request.Limit)
            .InclusiveBetween(1, AuditExportLimits.MaxRows)
            .WithMessage($"Export limit must be between 1 and {AuditExportLimits.MaxRows}.");

        RuleFor(x => x.Request.Format)
            .Must(value => AuditFilterParser.TryParseExportFormat(value, out _, out _))
            .WithMessage("Export format must be csv or json.");

        RuleFor(x => x.Request.FromUtc)
            .NotNull().WithMessage("Export requires FromUtc.");

        RuleFor(x => x.Request.ToUtc)
            .NotNull().WithMessage("Export requires ToUtc.");

        RuleFor(x => x.Request)
            .Must(x => !x.FromUtc.HasValue || !x.ToUtc.HasValue || x.FromUtc.Value <= x.ToUtc.Value)
            .WithMessage("FromUtc must be before or equal to ToUtc.");

        RuleFor(x => x.Request)
            .Must(x => !x.FromUtc.HasValue || !x.ToUtc.HasValue || (x.ToUtc.Value - x.FromUtc.Value).TotalDays <= AuditExportLimits.MaxDays)
            .WithMessage($"Export date range cannot exceed {AuditExportLimits.MaxDays} days.");

        RuleFor(x => x.Request.EntityType)
            .MaximumLength(160).When(x => !string.IsNullOrWhiteSpace(x.Request.EntityType));

        RuleFor(x => x.Request.SourceModule)
            .MaximumLength(120).When(x => !string.IsNullOrWhiteSpace(x.Request.SourceModule));

        RuleFor(x => x.Request.Category)
            .Must(IsValidEnum<AuditCategory>).WithMessage("Category contains an invalid value.");

        RuleFor(x => x.Request.Operation)
            .Must(IsValidEnum<AuditOperation>).WithMessage("Operation contains an invalid value.");

        RuleFor(x => x.Request.Outcome)
            .Must(IsValidEnum<AuditOutcome>).WithMessage("Outcome contains an invalid value.");
    }

    private static bool IsValidEnum<TEnum>(string? value)
        where TEnum : struct, Enum
    {
        return string.IsNullOrWhiteSpace(value)
               || (Enum.TryParse<TEnum>(value.Trim(), ignoreCase: true, out var parsed)
                   && Enum.IsDefined(parsed)
                   && !EqualityComparer<TEnum>.Default.Equals(parsed, default));
    }
}
