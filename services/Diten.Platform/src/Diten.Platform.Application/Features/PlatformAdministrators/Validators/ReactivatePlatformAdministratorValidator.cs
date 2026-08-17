using Diten.Platform.Application.Features.PlatformAdministrators.Commands;
using FluentValidation;

namespace Diten.Platform.Application.Features.PlatformAdministrators.Validators;

public sealed class ReactivatePlatformAdministratorValidator : AbstractValidator<ReactivatePlatformAdministratorCommand>
{
    public ReactivatePlatformAdministratorValidator()
    {
        RuleFor(x => x.Request.Reason)
            .MaximumLength(500);

        RuleFor(x => x.Request.Version)
            .GreaterThan(0);
    }
}
