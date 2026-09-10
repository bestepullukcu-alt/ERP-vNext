using Diten.Platform.Application.Features.TenantOrganization.Commands;
using FluentValidation;

namespace Diten.Platform.Application.Features.TenantOrganization.Validators;

/*
 * ⚠ SHAPE ONLY. Everything that needs to know what a data type means, what the tenant already has, or what is
 * already stored lives in OrganizationFieldDefinitionRules and runs in the handler. Splitting a rule between
 * a validator and a handler is how one of the two paths ends up without it — this file refuses only what can
 * be refused without reading anything.
 */
public sealed class CreateOrganizationFieldDefinitionValidator
    : AbstractValidator<CreateOrganizationFieldDefinitionCommand>
{
    public CreateOrganizationFieldDefinitionValidator()
    {
        RuleFor(x => x.Request).NotNull();
        RuleFor(x => x.Request.Code).NotEmpty().MaximumLength(120);
        RuleFor(x => x.Request.Name).NotEmpty().MaximumLength(200);
        RuleFor(x => x.Request.DataType).NotEmpty();
        RuleFor(x => x.Request.DisplayOrder).GreaterThanOrEqualTo(0);
    }
}
