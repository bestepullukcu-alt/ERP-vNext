using Diten.Platform.Application.Features.DocumentManagementReleaseGates.Commands;
using FluentValidation;

namespace Diten.Platform.Application.Features.DocumentManagementReleaseGates.Validators;

// MOD-0029-FU10 — input-shape validators. Gate computation, evidence sufficiency and non-waivability stay in the engine.

public sealed class EvaluateReleaseGatesValidator : AbstractValidator<EvaluateReleaseGatesCommand>
{
    public EvaluateReleaseGatesValidator() => RuleFor(x => x.RegisterEntryId).NotEmpty();
}

public sealed class RecordReleaseGateEvidenceValidator : AbstractValidator<RecordReleaseGateEvidenceCommand>
{
    public RecordReleaseGateEvidenceValidator()
    {
        RuleFor(x => x.RegisterEntryId).NotEmpty();
        RuleFor(x => x.Input).NotNull();
        RuleFor(x => x.Input.GateKey).NotEmpty().When(x => x.Input is not null);
        RuleFor(x => x.Input.EvidenceReference).NotEmpty().When(x => x.Input is not null);
    }
}
