using Diten.Platform.Application.Features.AccessGovernance.LegalEntityScopeResolution.Queries;
using FluentValidation;

namespace Diten.Platform.Application.Features.AccessGovernance.LegalEntityScopeResolution.Validators;

public sealed class ResolveTrustedLegalEntityScopeValidator : AbstractValidator<ResolveTrustedLegalEntityScopeQuery>
{
    public ResolveTrustedLegalEntityScopeValidator()
    {
        RuleFor(x => x.TenantId).NotEmpty();
        RuleFor(x => x.SubjectId).NotEmpty();
        RuleFor(x => x.ModuleCode)
            .Must(TrustedLegalEntityScopeResolutionLimits.IsValidModuleCode)
            .WithMessage("ModuleCode must be lowercase kebab-case.");
        RuleFor(x => x.PermissionKey)
            .Must(TrustedLegalEntityScopeResolutionLimits.IsValidPermissionKey)
            .WithMessage("PermissionKey must be lowercase dotted-kebab with at least three segments.");
    }
}
