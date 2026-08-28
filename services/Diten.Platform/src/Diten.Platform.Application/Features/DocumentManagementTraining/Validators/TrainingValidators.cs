using Diten.Platform.Application.Features.DocumentManagementTraining.Commands;
using FluentValidation;

namespace Diten.Platform.Application.Features.DocumentManagementTraining.Validators;

// MOD-0029-FU11 — input-shape validators. Matrix/readiness/eligibility rules stay in the service.

public sealed class ResolveTrainingMatrixValidator : AbstractValidator<ResolveTrainingMatrixCommand>
{
    public ResolveTrainingMatrixValidator() => RuleFor(x => x.RegisterEntryId).NotEmpty();
}

public sealed class AddManualTrainingRequirementValidator : AbstractValidator<AddManualTrainingRequirementCommand>
{
    public AddManualTrainingRequirementValidator()
    {
        RuleFor(x => x.RegisterEntryId).NotEmpty();
        RuleFor(x => x.Input).NotNull();
        RuleFor(x => x.Input.AudienceType).NotEmpty().When(x => x.Input is not null);
        RuleFor(x => x.Input.TrainingType).NotEmpty().When(x => x.Input is not null);
    }
}

public sealed class AssignTrainingValidator : AbstractValidator<AssignTrainingCommand>
{
    public AssignTrainingValidator()
    {
        RuleFor(x => x.RegisterEntryId).NotEmpty();
        RuleFor(x => x.Input).NotNull();
        RuleFor(x => x.Input.RequirementId).NotEmpty().When(x => x.Input is not null);
    }
}

public sealed class CompleteTrainingValidator : AbstractValidator<CompleteTrainingCommand>
{
    public CompleteTrainingValidator()
    {
        RuleFor(x => x.AssignmentId).NotEmpty();
        RuleFor(x => x.Input).NotNull();
        RuleFor(x => x.Input.CompletionEvidenceReference).NotEmpty().When(x => x.Input is not null);
    }
}

public sealed class RestrictTrainingValidator : AbstractValidator<RestrictTrainingCommand>
{
    public RestrictTrainingValidator()
    {
        RuleFor(x => x.AssignmentId).NotEmpty();
        RuleFor(x => x.Input).NotNull();
        RuleFor(x => x.Input.Reason).NotEmpty().When(x => x.Input is not null);
    }
}
