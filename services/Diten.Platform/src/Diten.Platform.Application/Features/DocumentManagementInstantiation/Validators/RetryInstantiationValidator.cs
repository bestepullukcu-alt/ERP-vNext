using Diten.Platform.Application.Features.DocumentManagementInstantiation.Commands;
using FluentValidation;

namespace Diten.Platform.Application.Features.DocumentManagementInstantiation.Validators;

public sealed class RetryInstantiationValidator : AbstractValidator<RetryInstantiationCommand>
{
    public RetryInstantiationValidator()
    {
        RuleFor(x => x.OperationId).NotEmpty();
        RuleForEach(x => x.NodeKeys).NotEmpty().MaximumLength(512);
    }
}
