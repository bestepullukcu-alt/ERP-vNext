using Diten.Platform.Application.Features.PlatformAdministrators.Commands;
using FluentValidation;

namespace Diten.Platform.Application.Features.PlatformAdministrators.Validators;

public sealed class DeletePlatformAdministratorValidator : AbstractValidator<DeletePlatformAdministratorCommand>
{
    public DeletePlatformAdministratorValidator()
    {
        RuleFor(x => x.Request.Version)
            .GreaterThan(0);
    }
}
