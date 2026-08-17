using Diten.Platform.Application.Features.InterfaceRegistry.Commands;
using FluentValidation;

namespace Diten.Platform.Application.Features.InterfaceRegistry.Validators;

public sealed class RejectInterfaceDiffItemRequestValidator : AbstractValidator<RejectInterfaceDiffItemRequest>
{
    public RejectInterfaceDiffItemRequestValidator()
    {
        RuleFor(x => x.DiffItemId).NotEmpty();
        RuleFor(x => x.ReviewReason)
            .NotEmpty()
            .Must(reason => !string.IsNullOrWhiteSpace(reason))
            .WithMessage("ReviewReason is required when rejecting a diff item.");
    }
}
