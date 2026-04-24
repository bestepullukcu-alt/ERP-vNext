using FluentValidation;
using Diten.AuthService.Application.Features.Roles.Commands;

namespace Diten.AuthService.Application.Features.Roles.Validators;

public sealed class CreateRoleCommandValidator : AbstractValidator<CreateRoleCommand>
{
    public CreateRoleCommandValidator()
    {
        RuleFor(x => x.Name)
            .NotEmpty().WithMessage("Rol adı boş bırakılamaz.")
            .MaximumLength(50).WithMessage("Rol adı en fazla 50 karakter olabilir.");

        RuleFor(x => x.DisplayName)
            .NotEmpty().WithMessage("Görünen ad boş bırakılamaz.")
            .MaximumLength(100).WithMessage("Görünen ad en fazla 100 karakter olabilir.");
    }
}
