using Diten.Platform.Application.Features.Tenants.Commands;
using FluentValidation;

namespace Diten.Platform.Application.Features.Tenants.Validators;

public sealed class UpdateTenantSettingsCommandValidator : AbstractValidator<UpdateTenantSettingsCommand>
{
    public UpdateTenantSettingsCommandValidator()
    {
        RuleFor(x => x.TenantId).NotEmpty();
        RuleFor(x => x.Request.Language).NotEmpty().MaximumLength(10);
        RuleFor(x => x.Request.Timezone).NotEmpty().MaximumLength(64);
        RuleFor(x => x.Request.Currency).NotEmpty().Length(3);
        RuleFor(x => x.Request.Environment).NotEmpty().MaximumLength(32);

        // WP-TASK-CALENDAR-ENGINE-01 — the default working window: both or neither, and a start before the end.
        RuleFor(x => x.Request.DefaultWorkdayEnd)
            .NotNull()
            .When(x => x.Request.DefaultWorkdayStart is not null)
            .WithMessage("DefaultWorkdayEnd is required when DefaultWorkdayStart is given.");
        RuleFor(x => x.Request.DefaultWorkdayStart)
            .NotNull()
            .When(x => x.Request.DefaultWorkdayEnd is not null)
            .WithMessage("DefaultWorkdayStart is required when DefaultWorkdayEnd is given.");
        RuleFor(x => x.Request.DefaultWorkdayStart)
            .Must((command, start) => start < command.Request.DefaultWorkdayEnd)
            .When(x => x.Request.DefaultWorkdayStart is not null && x.Request.DefaultWorkdayEnd is not null)
            .WithMessage("DefaultWorkdayStart must be before DefaultWorkdayEnd.");
    }
}
