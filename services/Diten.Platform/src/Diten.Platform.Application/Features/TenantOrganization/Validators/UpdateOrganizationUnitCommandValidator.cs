using Diten.Platform.Application.Features.TenantOrganization.Commands;
using FluentValidation;

namespace Diten.Platform.Application.Features.TenantOrganization.Validators;

public sealed class UpdateOrganizationUnitCommandValidator : AbstractValidator<UpdateOrganizationUnitCommand>
{
    public UpdateOrganizationUnitCommandValidator()
    {
        RuleFor(x => x.Id).NotEmpty();
        Include(new OrganizationUnitRequestValidator<UpdateOrganizationUnitCommand>(x => x.Request));

        // MOD-0288-FU02 — see the create validator for why this is the ONLY second-parent rule here.
        RuleFor(x => x.Request.AdministrativeParentOrganizationUnitId)
            .NotEqual(Guid.Empty)
            .When(x => x.Request.AdministrativeParentOrganizationUnitId.HasValue);

        // On update the id IS known, so self-reference is a shape error and need not reach the repository.
        RuleFor(x => x.Request.AdministrativeParentOrganizationUnitId)
            .Must((command, parent) => parent != command.Id)
            .WithMessage("Organization Unit cannot be its own administrative parent.")
            .When(x => x.Request.AdministrativeParentOrganizationUnitId.HasValue);
    }
}
