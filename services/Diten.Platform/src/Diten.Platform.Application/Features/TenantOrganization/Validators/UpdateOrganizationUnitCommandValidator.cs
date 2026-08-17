using Diten.Platform.Application.Features.TenantOrganization.Commands;
using FluentValidation;

namespace Diten.Platform.Application.Features.TenantOrganization.Validators;

public sealed class UpdateOrganizationUnitCommandValidator : AbstractValidator<UpdateOrganizationUnitCommand>
{
    public UpdateOrganizationUnitCommandValidator()
    {
        RuleFor(x => x.Id).NotEmpty();
        Include(new OrganizationUnitRequestValidator<UpdateOrganizationUnitCommand>(x => x.Request));
    }
}
