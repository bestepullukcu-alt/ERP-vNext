using Diten.Platform.Application.Features.PlatformAdministrators.Commands;
using FluentValidation;

namespace Diten.Platform.Application.Features.PlatformAdministrators.Validators;

public sealed class ResendPlatformAdministratorInviteValidator : AbstractValidator<ResendPlatformAdministratorInviteCommand>
{
    public ResendPlatformAdministratorInviteValidator()
    {
        RuleFor(x => x.Request.Version)
            .GreaterThan(0);
    }
}
