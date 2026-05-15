using Diten.Platform.Application.Features.Audit.Queries;
using Diten.Platform.Domain.Enums;
using FluentValidation;

namespace Diten.Platform.Application.Features.Audit.Validators;

public sealed class GetAuditEventListQueryValidator : AbstractValidator<GetAuditEventListQuery>
{
    public GetAuditEventListQueryValidator()
    {
        RuleFor(x => x.Filter.Page)
            .GreaterThanOrEqualTo(1).WithMessage("Page must be greater than or equal to 1.");

        RuleFor(x => x.Filter.PageSize)
            .InclusiveBetween(1, 500).WithMessage("PageSize must be between 1 and 500.");

        RuleFor(x => x.Filter.EntityType)
            .MaximumLength(160).When(x => !string.IsNullOrWhiteSpace(x.Filter.EntityType));

        RuleFor(x => x.Filter.SourceModule)
            .MaximumLength(120).When(x => !string.IsNullOrWhiteSpace(x.Filter.SourceModule));

        RuleFor(x => x.Filter)
            .Must(x => !x.FromUtc.HasValue || !x.ToUtc.HasValue || x.FromUtc.Value <= x.ToUtc.Value)
            .WithMessage("FromUtc must be before or equal to ToUtc.");

        RuleFor(x => x.Filter.Category)
            .Must(IsValidEnum<AuditCategory>).WithMessage("Category contains an invalid value.");

        RuleFor(x => x.Filter.Operation)
            .Must(IsValidEnum<AuditOperation>).WithMessage("Operation contains an invalid value.");

        RuleFor(x => x.Filter.Outcome)
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
