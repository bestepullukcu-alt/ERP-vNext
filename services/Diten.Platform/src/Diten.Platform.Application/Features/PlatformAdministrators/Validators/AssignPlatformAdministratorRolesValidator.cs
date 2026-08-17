using Diten.Platform.Application.Features.PlatformAdministrators.Commands;
using Diten.Platform.Domain.Enums;
using FluentValidation;

namespace Diten.Platform.Application.Features.PlatformAdministrators.Validators;

public sealed class AssignPlatformAdministratorRolesValidator : AbstractValidator<AssignPlatformAdministratorRolesCommand>
{
    public AssignPlatformAdministratorRolesValidator()
    {
        RuleFor(x => x.Request.Roles)
            .NotEmpty()
            .Must(roles => roles is { Count: > 0 } && roles.All(role => Enum.TryParse<AdministratorRole>(role, ignoreCase: false, out _)))
            .WithMessage("Roles must contain at least one valid administrator role.");

        RuleFor(x => x.Request.Version)
            .GreaterThan(0);
    }
}
