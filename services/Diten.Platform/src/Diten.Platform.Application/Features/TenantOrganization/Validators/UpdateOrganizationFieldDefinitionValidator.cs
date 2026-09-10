using Diten.Platform.Application.Features.TenantOrganization.Commands;
using FluentValidation;

namespace Diten.Platform.Application.Features.TenantOrganization.Validators;

public sealed class UpdateOrganizationFieldDefinitionValidator
    : AbstractValidator<UpdateOrganizationFieldDefinitionCommand>
{
    public UpdateOrganizationFieldDefinitionValidator()
    {
        RuleFor(x => x.Id).NotEmpty();
        RuleFor(x => x.Request).NotNull();
        RuleFor(x => x.Request.Name).NotEmpty().MaximumLength(200);
        RuleFor(x => x.Request.DataType).NotEmpty();
        RuleFor(x => x.Request.DisplayOrder).GreaterThanOrEqualTo(0);

        // CAS is not optional on an update: without an expected version a stale write cannot be detected.
        RuleFor(x => x.Request.ExpectedVersion).GreaterThan(0);
    }
}
