using Diten.Platform.Application.Features.TenantOrganization.Commands;
using FluentValidation;

namespace Diten.Platform.Application.Features.TenantOrganization.Validators;

public sealed class SetOrganizationFieldValueValidator : AbstractValidator<SetOrganizationFieldValueCommand>
{
    public SetOrganizationFieldValueValidator()
    {
        RuleFor(x => x.OrganizationUnitId).NotEmpty();
        RuleFor(x => x.Request).NotNull();
        RuleFor(x => x.Request.DefinitionId).NotEmpty();

        // ⚠ NOT REQUIRED, unlike the definition update. A first value has no version to name, and demanding
        // one would make "create" impossible to express through the same intention as "change".
        RuleFor(x => x.Request.ExpectedVersion).GreaterThan(0).When(x => x.Request.ExpectedVersion.HasValue);
    }
}
