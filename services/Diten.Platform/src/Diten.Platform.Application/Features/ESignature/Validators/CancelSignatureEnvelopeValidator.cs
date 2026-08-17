using Diten.Platform.Application.Features.ESignature.Commands;
using FluentValidation;

namespace Diten.Platform.Application.Features.ESignature.Validators;

public sealed class CancelSignatureEnvelopeValidator : AbstractValidator<CancelSignatureEnvelopeCommand>
{
    public CancelSignatureEnvelopeValidator()
    {
        RuleFor(x => x.TargetTenantId).NotEmpty();
        RuleFor(x => x.EnvelopeId).NotEmpty();
        RuleFor(x => x.Request.Reason).NotEmpty().MaximumLength(1000);
        RuleFor(x => x.Request.ExpectedVersion).GreaterThan(0);
        RuleFor(x => x.Request.CorrelationId).NotEmpty();
    }
}
