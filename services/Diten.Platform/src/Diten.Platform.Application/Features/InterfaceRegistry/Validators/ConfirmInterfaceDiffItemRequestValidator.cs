using Diten.Platform.Application.Features.InterfaceRegistry.Commands;
using FluentValidation;

namespace Diten.Platform.Application.Features.InterfaceRegistry.Validators;

public sealed class ConfirmInterfaceDiffItemRequestValidator : AbstractValidator<ConfirmInterfaceDiffItemRequest>
{
    public ConfirmInterfaceDiffItemRequestValidator()
    {
        RuleFor(x => x.DiffItemId).NotEmpty();
    }
}
