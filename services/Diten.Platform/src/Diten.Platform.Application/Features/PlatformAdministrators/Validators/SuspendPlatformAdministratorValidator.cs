using Diten.Platform.Application.Features.PlatformAdministrators.Commands;
using FluentValidation;

namespace Diten.Platform.Application.Features.PlatformAdministrators.Validators;

public sealed class SuspendPlatformAdministratorValidator : AbstractValidator<SuspendPlatformAdministratorCommand>
{
    public SuspendPlatformAdministratorValidator()
    {
        RuleFor(x => x.Request.Reason)
            .NotEmpty()
            .MaximumLength(500);

        RuleFor(x => x.Request.Version)
            .GreaterThan(0);
    }
}
