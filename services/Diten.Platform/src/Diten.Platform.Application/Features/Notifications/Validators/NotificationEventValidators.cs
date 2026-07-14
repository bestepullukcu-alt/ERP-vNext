using Diten.Platform.Application.Features.Notifications.Commands;
using FluentValidation;

namespace Diten.Platform.Application.Features.Notifications.Validators;

// MOD-0027-FU03 — SOFT-field update validation (HARD fields are manifest-reconciled, not user-editable).
public sealed class UpdateNotificationEventValidator : AbstractValidator<UpdateNotificationEventCommand>
{
    public UpdateNotificationEventValidator()
    {
        RuleFor(x => x.Id).NotEmpty();
        RuleFor(x => x.Request).NotNull();

        When(x => x.Request is not null, () =>
        {
            RuleFor(x => x.Request.FallbackDisplayName)
                .MaximumLength(200)
                .When(x => !string.IsNullOrWhiteSpace(x.Request.FallbackDisplayName));

            RuleFor(x => x.Request.DisplayNameKey)
                .MaximumLength(200)
                .When(x => !string.IsNullOrWhiteSpace(x.Request.DisplayNameKey));

            RuleFor(x => x.Request.Description)
                .MaximumLength(1000)
                .When(x => !string.IsNullOrWhiteSpace(x.Request.Description));
        });
    }
}
