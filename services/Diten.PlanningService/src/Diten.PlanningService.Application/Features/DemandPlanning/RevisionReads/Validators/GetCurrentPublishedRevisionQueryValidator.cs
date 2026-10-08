using System.Globalization;
using FluentValidation;

namespace Diten.PlanningService.Application.Features.DemandPlanning;

public sealed class GetCurrentPublishedRevisionQueryValidator
    : AbstractValidator<GetCurrentPublishedRevisionQuery>
{
    public GetCurrentPublishedRevisionQueryValidator()
    {
        RuleFor(x => x.TenantId).NotEmpty();
        RuleFor(x => x.LegalEntityId).NotEmpty();
        RuleFor(x => x.ActorId).NotEmpty();
        RuleFor(x => x.PlanningPeriodKey).Must(value =>
            !string.IsNullOrWhiteSpace(value) && value.Length == 10 &&
            DateOnly.TryParseExact(value, "yyyy-MM-dd",
                CultureInfo.InvariantCulture, DateTimeStyles.None, out var date) &&
            date.ToString("yyyy-MM-dd", CultureInfo.InvariantCulture) == value);
    }
}
