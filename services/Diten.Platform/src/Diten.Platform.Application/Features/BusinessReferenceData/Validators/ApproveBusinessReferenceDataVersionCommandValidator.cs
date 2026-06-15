using Diten.Platform.Application.Features.BusinessReferenceData.Commands;
using Diten.Platform.Application.Features.BusinessReferenceData.Queries;
using Diten.Platform.Application.Features.BusinessReferenceData.Services;
using FluentValidation;

namespace Diten.Platform.Application.Features.BusinessReferenceData.Validators;

public sealed class ApproveBusinessReferenceDataVersionCommandValidator : AbstractValidator<ApproveBusinessReferenceDataVersionCommand>
{
    public ApproveBusinessReferenceDataVersionCommandValidator()
    {
        RuleFor(x => x.VersionId).NotEmpty();
        RuleFor(x => x.ActorId).NotEmpty().MaximumLength(128);
        RuleFor(x => x.CorrelationId).NotEmpty().MaximumLength(64);
        RuleFor(x => x.RejectionReason)
            .NotEmpty()
            .When(x => x.Action == BusinessReferenceDataWorkflowTransitionAction.Reject)
            .WithMessage("rejectionReason is required when decision is reject.");
        RuleFor(x => x.RequestInfoComment)
            .NotEmpty()
            .When(x => x.Action == BusinessReferenceDataWorkflowTransitionAction.RequestInfo)
            .WithMessage("comment is required when decision is request_info.");
        RuleFor(x => x.RequestInfoTargetStep)
            .NotEmpty()
            .When(x => x.Action == BusinessReferenceDataWorkflowTransitionAction.RequestInfo)
            .WithMessage("targetStep is required when decision is request_info.");
        RuleFor(x => x.OverrideReason).NotEmpty().When(x => x.OverrideAction);
    }
}
