using Diten.Platform.Application.Features.InterfaceRegistry.Commands;
using FluentValidation;

namespace Diten.Platform.Application.Features.InterfaceRegistry.Validators;

public sealed class ConfirmInterfaceDiscoveryBatchRequestValidator : AbstractValidator<ConfirmInterfaceDiscoveryBatchRequest>
{
    public ConfirmInterfaceDiscoveryBatchRequestValidator()
    {
        RuleFor(x => x.BatchId).NotEmpty();
    }
}
