using Diten.Platform.Application.Features.DocumentManagementControlledDocumentRegistration.Commands;
using FluentValidation;

namespace Diten.Platform.Application.Features.DocumentManagementControlledDocumentRegistration.Validators;

public sealed class RetryControlledDocumentRegistrationValidator : AbstractValidator<RetryControlledDocumentRegistrationCommand>
{
    public RetryControlledDocumentRegistrationValidator() => RuleFor(x => x.OperationId).NotEmpty();
}
