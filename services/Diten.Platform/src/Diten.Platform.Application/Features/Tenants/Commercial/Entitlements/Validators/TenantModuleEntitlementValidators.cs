using Diten.Platform.Application.Features.Tenants.Commercial.Entitlements.Commands;
using Diten.Platform.Application.Features.Tenants.Commercial.Entitlements.Queries;
using Diten.Platform.Domain.Enums;
using FluentValidation;

namespace Diten.Platform.Application.Features.Tenants.Commercial.Entitlements.Validators;

public sealed class AddTenantModuleEntitlementCommandValidator : AbstractValidator<AddTenantModuleEntitlementCommand>
{
    public AddTenantModuleEntitlementCommandValidator()
    {
        RuleFor(x => x.TenantId).NotEmpty();
        RuleFor(x => x.Request.ModuleCode).NotEmpty().MaximumLength(64);
        RuleFor(x => x.Request.Source).IsInEnum();
        RuleFor(x => x.Request.Reason)
            .NotEmpty()
            .MaximumLength(500)
            .When(x => x.Request.Source == EntitlementSource.ManualOverride || x.Request.IsEnabled == false);
        RuleFor(x => x.Request.Reason).MaximumLength(500);
    }
}

public sealed class DisableTenantModuleEntitlementCommandValidator : AbstractValidator<DisableTenantModuleEntitlementCommand>
{
    public DisableTenantModuleEntitlementCommandValidator()
    {
        RuleFor(x => x.TenantId).NotEmpty();
        RuleFor(x => x.Request.ModuleCode).NotEmpty().MaximumLength(64);
        RuleFor(x => x.Request.Reason).NotEmpty().MaximumLength(500);
    }
}

public sealed class EnableTenantModuleEntitlementCommandValidator : AbstractValidator<EnableTenantModuleEntitlementCommand>
{
    public EnableTenantModuleEntitlementCommandValidator()
    {
        RuleFor(x => x.TenantId).NotEmpty();
        RuleFor(x => x.EntitlementId).NotEmpty();
    }
}

public sealed class UpdateTenantModuleEntitlementExpiryCommandValidator : AbstractValidator<UpdateTenantModuleEntitlementExpiryCommand>
{
    public UpdateTenantModuleEntitlementExpiryCommandValidator()
    {
        RuleFor(x => x.TenantId).NotEmpty();
        RuleFor(x => x.EntitlementId).NotEmpty();
        RuleFor(x => x.Request.Reason).MaximumLength(500);
    }
}

public sealed class RemoveTenantManualModuleOverrideCommandValidator : AbstractValidator<RemoveTenantManualModuleOverrideCommand>
{
    public RemoveTenantManualModuleOverrideCommandValidator()
    {
        RuleFor(x => x.TenantId).NotEmpty();
        RuleFor(x => x.EntitlementId).NotEmpty();
    }
}

public sealed class GetTenantModuleEffectiveAccessQueryValidator : AbstractValidator<GetTenantModuleEffectiveAccessQuery>
{
    public GetTenantModuleEffectiveAccessQueryValidator()
    {
        RuleFor(x => x.TenantId).NotEmpty();
        RuleFor(x => x.ModuleCode).NotEmpty().MaximumLength(64);
    }
}
