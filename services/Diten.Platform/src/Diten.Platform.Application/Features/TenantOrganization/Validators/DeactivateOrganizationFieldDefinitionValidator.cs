using Diten.Platform.Application.Features.TenantOrganization.Commands;
using FluentValidation;

namespace Diten.Platform.Application.Features.TenantOrganization.Validators;

public sealed class DeactivateOrganizationFieldDefinitionValidator
    : AbstractValidator<DeactivateOrganizationFieldDefinitionCommand>
{
    public DeactivateOrganizationFieldDefinitionValidator()
    {
        RuleFor(x => x.Id).NotEmpty();
        RuleFor(x => x.ExpectedVersion).GreaterThan(0);
    }
}
