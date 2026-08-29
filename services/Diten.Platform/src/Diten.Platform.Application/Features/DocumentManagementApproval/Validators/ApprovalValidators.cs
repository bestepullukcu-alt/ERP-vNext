using Diten.Platform.Application.Features.DocumentManagementApproval.Commands;
using FluentValidation;

namespace Diten.Platform.Application.Features.DocumentManagementApproval.Validators;

// MOD-0029-FU09 — input-shape validators. Route/segregation/role rules stay in the service.

public sealed class ResolveApprovalRouteValidator : AbstractValidator<ResolveApprovalRouteCommand>
{
    public ResolveApprovalRouteValidator() => RuleFor(x => x.RegisterEntryId).NotEmpty();
}

public sealed class RecordApprovalEvidenceValidator : AbstractValidator<RecordApprovalEvidenceCommand>
{
    public RecordApprovalEvidenceValidator()
    {
        RuleFor(x => x.RegisterEntryId).NotEmpty();
        RuleFor(x => x.Input).NotNull();
        RuleFor(x => x.Input.RequirementId).NotEmpty().When(x => x.Input is not null);
        RuleFor(x => x.Input.Action).NotEmpty().When(x => x.Input is not null);
        RuleFor(x => x.Input.PerformedByRole).NotEmpty().When(x => x.Input is not null);
    }
}

public sealed class RejectApprovalValidator : AbstractValidator<RejectApprovalCommand>
{
    public RejectApprovalValidator()
    {
        RuleFor(x => x.RegisterEntryId).NotEmpty();
        RuleFor(x => x.Input).NotNull();
        RuleFor(x => x.Input.RequirementId).NotEmpty().When(x => x.Input is not null);
        RuleFor(x => x.Input.PerformedByRole).NotEmpty().When(x => x.Input is not null);
        RuleFor(x => x.Input.Reason).NotEmpty().When(x => x.Input is not null);
    }
}
