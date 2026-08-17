using Diten.Platform.Application.Features.InterfaceRegistry.Commands;
using FluentValidation;

namespace Diten.Platform.Application.Features.InterfaceRegistry.Validators;

public sealed class DeprecateInterfaceRequestValidator : AbstractValidator<DeprecateInterfaceRequest>
{
    public DeprecateInterfaceRequestValidator()
    {
        RuleFor(x => x.InterfaceCode)
            .NotEmpty()
            .Must(InterfaceCodeNormalizer.IsValid)
            .WithMessage("InterfaceCode must use {MODULE}.{RESOURCE}.{ACTION} format.");
        RuleFor(x => x.Version).NotEmpty().Matches("^v[0-9]+$").WithMessage("Version must use vN format.");
        RuleFor(x => x.Reason)
            .NotEmpty()
            .Must(reason => !string.IsNullOrWhiteSpace(reason))
            .WithMessage("Deprecation reason is required.");
    }
}
