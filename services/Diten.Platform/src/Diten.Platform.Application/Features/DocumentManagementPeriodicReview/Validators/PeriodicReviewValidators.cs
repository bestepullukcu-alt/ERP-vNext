using Diten.Platform.Application.Features.DocumentManagementPeriodicReview.Commands;
using FluentValidation;

namespace Diten.Platform.Application.Features.DocumentManagementPeriodicReview.Validators;

// MOD-0029-FU12 — input-shape validators. Cycle/extension/overdue rules stay in the service.

public sealed class InitiatePeriodicReviewValidator : AbstractValidator<InitiatePeriodicReviewCommand>
{
    public InitiatePeriodicReviewValidator() => RuleFor(x => x.RegisterEntryId).NotEmpty();
}

public sealed class CompletePeriodicReviewValidator : AbstractValidator<CompletePeriodicReviewCommand>
{
    public CompletePeriodicReviewValidator()
    {
        RuleFor(x => x.RegisterEntryId).NotEmpty();
        RuleFor(x => x.ReviewId).NotEmpty();
        RuleFor(x => x.Input).NotNull();
        RuleFor(x => x.Input.Decision).NotEmpty().When(x => x.Input is not null);
        RuleFor(x => x.Input.ReviewEvidenceReference).NotEmpty().When(x => x.Input is not null);
    }
}

public sealed class RequestPeriodicReviewExtensionValidator : AbstractValidator<RequestPeriodicReviewExtensionCommand>
{
    public RequestPeriodicReviewExtensionValidator()
    {
        RuleFor(x => x.ReviewId).NotEmpty();
        RuleFor(x => x.Input).NotNull();
        RuleFor(x => x.Input.RiskAssessmentReference).NotEmpty().When(x => x.Input is not null);
        RuleFor(x => x.Input.ExtensionDays).GreaterThan(0).When(x => x.Input is not null);
    }
}

public sealed class ApprovePeriodicReviewExtensionValidator : AbstractValidator<ApprovePeriodicReviewExtensionCommand>
{
    public ApprovePeriodicReviewExtensionValidator()
    {
        RuleFor(x => x.ExtensionId).NotEmpty();
        RuleFor(x => x.Input).NotNull();
        RuleFor(x => x.Input.ApproverRole).NotEmpty().When(x => x.Input is not null);
    }
}

public sealed class RejectPeriodicReviewExtensionValidator : AbstractValidator<RejectPeriodicReviewExtensionCommand>
{
    public RejectPeriodicReviewExtensionValidator()
    {
        RuleFor(x => x.ExtensionId).NotEmpty();
        RuleFor(x => x.Input).NotNull();
        RuleFor(x => x.Input.Reason).NotEmpty().When(x => x.Input is not null);
    }
}
