using FluentValidation;
using Diten.AuthService.Application.Features.Roles.Commands;

namespace Diten.AuthService.Application.Features.Roles.Validators;

public sealed class CreateRoleCommandValidator : AbstractValidator<CreateRoleCommand>
{
    public CreateRoleCommandValidator()
    {
        RuleFor(x => x.Name)
            .NotEmpty().WithMessage("Rol adı boş bırakılamaz.").WithErrorCode(RoleErrorCodes.NameRequired)
            .MaximumLength(RoleFieldLimits.NameMaxLength).WithMessage("Rol adı en fazla 50 karakter olabilir.").WithErrorCode(RoleErrorCodes.NameTooLong);

        RuleFor(x => x.DisplayName)
            .NotEmpty().WithMessage("Görünen ad boş bırakılamaz.").WithErrorCode(RoleErrorCodes.DisplayNameRequired)
            .MaximumLength(RoleFieldLimits.DisplayNameMaxLength).WithMessage("Görünen ad en fazla 100 karakter olabilir.").WithErrorCode(RoleErrorCodes.DisplayNameTooLong);
    }
}
