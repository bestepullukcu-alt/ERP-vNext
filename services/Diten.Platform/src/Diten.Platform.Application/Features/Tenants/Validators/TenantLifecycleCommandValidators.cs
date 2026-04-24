using Diten.Platform.Application.Features.Tenants.Commands;
using FluentValidation;

namespace Diten.Platform.Application.Features.Tenants.Validators;

public sealed class SuspendTenantCommandValidator : AbstractValidator<SuspendTenantCommand>
{
    public SuspendTenantCommandValidator()
    {
        RuleFor(x => x.TenantId).NotEmpty();
        RuleFor(x => x.Reason).MaximumLength(300);
    }
}

public sealed class ReactivateTenantCommandValidator : AbstractValidator<ReactivateTenantCommand>
{
    public ReactivateTenantCommandValidator()
    {
        RuleFor(x => x.TenantId).NotEmpty();
        RuleFor(x => x.Reason).MaximumLength(300);
    }
}
