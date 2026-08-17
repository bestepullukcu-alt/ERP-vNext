using Diten.Platform.Application.Features.PlatformAdministrators.Commands;
using Diten.Platform.Domain.Enums;
using FluentValidation;

namespace Diten.Platform.Application.Features.PlatformAdministrators.Validators;

public sealed class UpdatePlatformAdministratorValidator : AbstractValidator<UpdatePlatformAdministratorCommand>
{
    public UpdatePlatformAdministratorValidator()
    {
        RuleFor(x => x.Request.Email)
            .NotEmpty()
            .EmailAddress()
            .MaximumLength(256);

        RuleFor(x => x.Request.UserName)
            .NotEmpty()
            .MinimumLength(3)
            .MaximumLength(64)
            .Matches("^[A-Za-z0-9._-]+$")
            .WithMessage("UserName may contain only letters, numbers, dots, underscores, and hyphens.");

        RuleFor(x => x.Request.DisplayName)
            .NotEmpty()
            .MaximumLength(200);

        RuleFor(x => x.Request.ActorType)
            .NotEmpty()
            .Must(value => Enum.TryParse<ActorType>(value, ignoreCase: false, out _))
            .WithMessage("ActorType must be a valid administrator actor type.");

        RuleFor(x => x.Request.Status)
            .NotEmpty()
            .Must(value => Enum.TryParse<AdministratorStatus>(value, ignoreCase: false, out _))
            .WithMessage("Status must be a valid administrator status.");

        RuleFor(x => x.Request.Roles)
            .NotEmpty()
            .Must(ContainOnlyValidRoles)
            .WithMessage("Roles must contain at least one valid administrator role.");

        RuleFor(x => x.Request.Version)
            .GreaterThan(0);

        RuleFor(x => x.Request.PartnerId)
            .NotNull()
            .NotEqual(Guid.Empty)
            .When(x => IsPartnerAdmin(x.Request.ActorType))
            .WithMessage("PartnerId is required for PartnerAdmin.");

        RuleFor(x => x.Request.AllowedTenantIds)
            .Must(values => values is { Count: > 0 } && values.All(id => id != Guid.Empty))
            .When(x => IsPartnerAdmin(x.Request.ActorType))
            .WithMessage("AllowedTenantIds must contain at least one tenant id for PartnerAdmin.");
    }

    private static bool IsPartnerAdmin(string actorType) =>
        Enum.TryParse<ActorType>(actorType, ignoreCase: false, out var parsed)
        && parsed == ActorType.PartnerAdmin;

    private static bool ContainOnlyValidRoles(IReadOnlyList<string>? roles) =>
        roles is { Count: > 0 }
        && roles.All(role => Enum.TryParse<AdministratorRole>(role, ignoreCase: false, out _));
}
