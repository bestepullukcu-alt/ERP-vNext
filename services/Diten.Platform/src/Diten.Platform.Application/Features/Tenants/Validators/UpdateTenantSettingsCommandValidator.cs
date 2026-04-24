using Diten.Platform.Application.Features.Tenants.Commands;
using FluentValidation;

namespace Diten.Platform.Application.Features.Tenants.Validators;

public sealed class UpdateTenantSettingsCommandValidator : AbstractValidator<UpdateTenantSettingsCommand>
{
    public UpdateTenantSettingsCommandValidator()
    {
        RuleFor(x => x.TenantId).NotEmpty();
        RuleFor(x => x.Request.Language).NotEmpty().MaximumLength(10);
        RuleFor(x => x.Request.Timezone).NotEmpty().MaximumLength(64);
        RuleFor(x => x.Request.Currency).NotEmpty().Length(3);
        RuleFor(x => x.Request.Environment).NotEmpty().MaximumLength(32);
    }
}
