using Diten.Platform.Application.Features.DocumentManagementSuspension.Commands;
using FluentValidation;

namespace Diten.Platform.Application.Features.DocumentManagementSuspension.Validators;

// MOD-0029-FU13 — input-shape validators. Eligibility, approver-role, evidence and expiry rules stay in the services.

public sealed class OpenSuspensionCaseValidator : AbstractValidator<OpenSuspensionCaseCommand>
{
    public OpenSuspensionCaseValidator()
    {
        RuleFor(x => x.RegisterEntryId).NotEmpty();
        RuleFor(x => x.Input).NotNull();
        RuleFor(x => x.Input.TriggerType).NotEmpty().When(x => x.Input is not null);
        RuleFor(x => x.Input.TriggerDescription).NotEmpty().When(x => x.Input is not null);
    }
}

public sealed class ApproveSuspensionValidator : AbstractValidator<ApproveSuspensionCommand>
{
    public ApproveSuspensionValidator()
    {
        RuleFor(x => x.CaseId).NotEmpty();
        RuleFor(x => x.Input).NotNull();
        RuleFor(x => x.Input.Decision).NotEmpty().When(x => x.Input is not null);
        RuleFor(x => x.Input.DecisionReason).NotEmpty().When(x => x.Input is not null);
        RuleFor(x => x.Input.ApprovedByRole).NotEmpty().When(x => x.Input is not null);
        RuleFor(x => x.Input.CommunicationPlanReference).NotEmpty().When(x => x.Input is not null);
    }
}

public sealed class ExecuteSuspensionValidator : AbstractValidator<ExecuteSuspensionCommand>
{
    public ExecuteSuspensionValidator()
    {
        RuleFor(x => x.CaseId).NotEmpty();
        RuleFor(x => x.Input).NotNull();
        RuleFor(x => x.Input.SuspensionNoticeReference).NotEmpty().When(x => x.Input is not null);
        RuleFor(x => x.Input.AccessRemovalEvidenceReference).NotEmpty().When(x => x.Input is not null);
        RuleFor(x => x.Input.AffectedRecordsBatchesActivitiesReference).NotEmpty().When(x => x.Input is not null);
    }
}

public sealed class RequestRetirementValidator : AbstractValidator<RequestRetirementCommand>
{
    public RequestRetirementValidator()
    {
        RuleFor(x => x.RegisterEntryId).NotEmpty();
        RuleFor(x => x.Input).NotNull();
        RuleFor(x => x.Input.RetirementReason).NotEmpty().When(x => x.Input is not null);
        RuleFor(x => x.Input.JustificationReference).NotEmpty().When(x => x.Input is not null);
        RuleFor(x => x.Input.TransitionAssessmentReference).NotEmpty().When(x => x.Input is not null);
    }
}

public sealed class ExecuteRetirementValidator : AbstractValidator<ExecuteRetirementCommand>
{
    public ExecuteRetirementValidator()
    {
        RuleFor(x => x.CaseId).NotEmpty();
        RuleFor(x => x.Input).NotNull();
        RuleFor(x => x.Input.CommunicationEvidenceReference).NotEmpty().When(x => x.Input is not null);
        RuleFor(x => x.Input.ArchivalEvidenceReference).NotEmpty().When(x => x.Input is not null);
    }
}

public sealed class CloseTemporaryInstructionValidator : AbstractValidator<CloseTemporaryInstructionCommand>
{
    public CloseTemporaryInstructionValidator()
    {
        RuleFor(x => x.RegisterEntryId).NotEmpty();
        RuleFor(x => x.Input).NotNull();
        RuleFor(x => x.Input.ExpiryAction).NotEmpty().When(x => x.Input is not null);
    }
}
