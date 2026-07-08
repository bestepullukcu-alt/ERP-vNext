using Diten.HcmService.Application.Features.CoreHrEmployeeMaster.Commands;
using FluentValidation;

namespace Diten.HcmService.Application.Features.CoreHrEmployeeMaster.Validators;

public sealed class PatchEmployeeDraftValidator : AbstractValidator<PatchEmployeeDraftCommand>
{
    public PatchEmployeeDraftValidator()
    {
        RuleFor(x => x.DraftSessionId)
            .NotEmpty();

        RuleFor(x => x.IfMatch)
            .NotEmpty()
            .WithMessage("If-Match header is required.");

        RuleFor(x => x.Request.IdempotencyKey)
            .NotEmpty()
            .MaximumLength(200);

        RuleFor(x => x.Request.StepCode)
            .NotEmpty()
            .MaximumLength(80);

        RuleFor(x => x.Request.PayloadSchemaVersion)
            .NotEmpty()
            .MaximumLength(80);

        RuleFor(x => x.Request.StepPayload)
            .Must(payload => !EmployeeDraftPayloadGuard.ContainsGovernmentIdentifier(payload))
            .WithMessage("Government identifier capture is disabled for this slice.");
    }
}
