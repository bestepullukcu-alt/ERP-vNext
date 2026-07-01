using Diten.Platform.Application.Features.DocumentManagementAccessMatrix.Commands;
using FluentValidation;

namespace Diten.Platform.Application.Features.DocumentManagementAccessMatrix.Validators;

public sealed class UpdateDocumentAccessPolicyValidator : AbstractValidator<UpdateDocumentAccessPolicyCommand>
{
    public UpdateDocumentAccessPolicyValidator()
    {
        RuleFor(x => x.Id).NotEmpty();
        RuleFor(x => x.Input).NotNull();
        RuleFor(x => x.Input.TargetType).NotEmpty().When(x => x.Input is not null);
        RuleFor(x => x.Input.TargetId).NotEmpty().When(x => x.Input is not null);
        RuleFor(x => x.Input.PrincipalType).NotEmpty().When(x => x.Input is not null);
        RuleFor(x => x.Input.PrincipalId).NotEmpty().MaximumLength(160).When(x => x.Input is not null);
        RuleFor(x => x.Input.Actions).NotEmpty().When(x => x.Input is not null);
        RuleFor(x => x.Input.Effect).NotEmpty().When(x => x.Input is not null);
        RuleFor(x => x.Input.Reason).MaximumLength(1000).When(x => x.Input?.Reason is not null);
    }
}
