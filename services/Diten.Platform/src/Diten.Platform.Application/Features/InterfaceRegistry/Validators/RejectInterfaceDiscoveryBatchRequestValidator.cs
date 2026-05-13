using Diten.Platform.Application.Features.InterfaceRegistry.Commands;
using FluentValidation;

namespace Diten.Platform.Application.Features.InterfaceRegistry.Validators;

public sealed class RejectInterfaceDiscoveryBatchRequestValidator : AbstractValidator<RejectInterfaceDiscoveryBatchRequest>
{
    public RejectInterfaceDiscoveryBatchRequestValidator()
    {
        RuleFor(x => x.BatchId).NotEmpty();
        RuleFor(x => x.ReviewReason)
            .NotEmpty()
            .Must(reason => !string.IsNullOrWhiteSpace(reason))
            .WithMessage("ReviewReason is required when rejecting a batch.");
    }
}
