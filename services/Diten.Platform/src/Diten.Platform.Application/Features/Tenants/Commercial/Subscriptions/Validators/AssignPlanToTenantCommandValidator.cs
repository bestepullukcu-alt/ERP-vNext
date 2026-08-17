using Diten.Platform.Application.Features.Tenants.Commercial.Subscriptions.Commands;
using FluentValidation;

namespace Diten.Platform.Application.Features.Tenants.Commercial.Subscriptions.Validators;

public sealed class AssignPlanToTenantCommandValidator : AbstractValidator<AssignPlanToTenantCommand>
{
    public AssignPlanToTenantCommandValidator()
    {
        RuleFor(x => x.TenantId).NotEmpty();
        RuleFor(x => x.Request.PlanId).NotEmpty();
        RuleFor(x => x.Request.CurrentPeriodEndUtc)
            .NotEmpty()
            .When(x => !x.Request.IsTrial);
        RuleFor(x => x.Request.TrialEndDateUtc)
            .GreaterThan(DateTimeOffset.UtcNow)
            .When(x => x.Request.IsTrial && x.Request.TrialEndDateUtc.HasValue);
        RuleFor(x => x.Request.CurrentPeriodEndUtc)
            .GreaterThan(x => x.Request.CurrentPeriodStartUtc)
            .When(x => !x.Request.IsTrial && x.Request.CurrentPeriodStartUtc.HasValue && x.Request.CurrentPeriodEndUtc.HasValue);
    }
}
