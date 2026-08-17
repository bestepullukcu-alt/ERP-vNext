using Diten.Platform.Application.Features.ESignature.Commands;
using FluentValidation;

namespace Diten.Platform.Application.Features.ESignature.Validators;

public sealed class RecordInternalAttestationValidator : AbstractValidator<RecordInternalAttestationCommand>
{
    public RecordInternalAttestationValidator()
    {
        RuleFor(x => x.TargetTenantId).NotEmpty();
        RuleFor(x => x.EnvelopeId).NotEmpty();
        RuleFor(x => x.Request.ParticipantId).NotEmpty();
        RuleFor(x => x.Request.AttestationText).NotEmpty().MinimumLength(10).MaximumLength(4000);
        RuleFor(x => x.Request.ExpectedEnvelopeVersion).GreaterThan(0);
        RuleFor(x => x.Request.ExpectedParticipantVersion).GreaterThan(0);
        RuleFor(x => x.Request.CorrelationId).NotEmpty();
    }
}
