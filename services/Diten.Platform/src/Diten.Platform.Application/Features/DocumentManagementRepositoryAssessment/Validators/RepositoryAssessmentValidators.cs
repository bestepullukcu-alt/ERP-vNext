using Diten.Platform.Application.Features.DocumentManagementRepositoryAssessment.Commands;
using FluentValidation;

namespace Diten.Platform.Application.Features.DocumentManagementRepositoryAssessment.Validators;

// MOD-0029-FU16 — input-shape validators. Content/boundary/approval rules stay in the service + evaluator.

public sealed class CreateRepositoryAssessmentValidator : AbstractValidator<CreateRepositoryAssessmentCommand>
{
    public CreateRepositoryAssessmentValidator()
    {
        RuleFor(x => x.Input).NotNull();
        RuleFor(x => x.Input.RepositoryName).NotEmpty().When(x => x.Input is not null);
        RuleFor(x => x.Input.RepositoryType).NotEmpty().When(x => x.Input is not null);
    }
}

public sealed class UpdateRepositoryAssessmentValidator : AbstractValidator<UpdateRepositoryAssessmentCommand>
{
    public UpdateRepositoryAssessmentValidator()
    {
        RuleFor(x => x.Id).NotEmpty();
        RuleFor(x => x.Input).NotNull();
        RuleFor(x => x.Input.RepositoryName).NotEmpty().When(x => x.Input is not null);
        RuleFor(x => x.Input.RepositoryType).NotEmpty().When(x => x.Input is not null);
    }
}

public sealed class ApproveRepositoryAssessmentValidator : AbstractValidator<ApproveRepositoryAssessmentCommand>
{
    public ApproveRepositoryAssessmentValidator()
    {
        RuleFor(x => x.Id).NotEmpty();
        RuleFor(x => x.Input).NotNull();
        RuleFor(x => x.Input.ApprovedByRole).NotEmpty().When(x => x.Input is not null);
    }
}

public sealed class LinkRepositoryAssessmentValidator : AbstractValidator<LinkRepositoryAssessmentToRegisterEntryCommand>
{
    public LinkRepositoryAssessmentValidator()
    {
        RuleFor(x => x.RegisterEntryId).NotEmpty();
        RuleFor(x => x.Input).NotNull();
        RuleFor(x => x.Input.RepositoryAssessmentId).NotEmpty().When(x => x.Input is not null);
    }
}
