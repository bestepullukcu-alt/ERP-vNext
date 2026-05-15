using Diten.Platform.Application.Features.PlatformAccount.Commands;
using FluentValidation;

namespace Diten.Platform.Application.Features.PlatformAccount.Validators;

public sealed class UpdatePlatformAccountSettingsValidator : AbstractValidator<UpdatePlatformAccountSettingsCommand>
{
    public UpdatePlatformAccountSettingsValidator()
    {
        RuleFor(x => x.Request.DisplayName)
            .NotEmpty()
            .MinimumLength(2)
            .MaximumLength(200);

        RuleFor(x => x.Request.Version)
            .GreaterThanOrEqualTo(0);
    }
}
