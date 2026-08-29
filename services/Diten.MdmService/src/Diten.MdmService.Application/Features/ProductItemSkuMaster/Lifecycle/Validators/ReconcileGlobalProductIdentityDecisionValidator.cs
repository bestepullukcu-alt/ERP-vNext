using Diten.MdmService.Application.Features.ProductItemSkuMaster.Lifecycle.Commands;
using Diten.MdmService.Domain.Enums;
using FluentValidation;

namespace Diten.MdmService.Application.Features.ProductItemSkuMaster.Lifecycle.Validators;

public sealed class ReconcileGlobalProductIdentityDecisionValidator
    : AbstractValidator<ReconcileGlobalProductIdentityDecisionCommand>
{
    public ReconcileGlobalProductIdentityDecisionValidator()
    {
        RuleFor(x => x.Request).NotNull();
        When(x => x.Request is not null, () =>
        {
            RuleFor(x => x.Request.GlobalProductId).NotEmpty();
            RuleFor(x => x.Request.ExpectedVersion).GreaterThanOrEqualTo(0);
            RuleFor(x => x.Request.ExpectedWorkflowInstanceId).NotEmpty();
            RuleFor(x => x.Request.DecisionEvidence).NotNull();
            When(x => x.Request.DecisionEvidence is not null, () =>
            {
                RuleFor(x => x.Request.DecisionEvidence.Decision)
                    .IsInEnum()
                    .Must(value => value is ProductIdentityDecisionKind.Approved
                        or ProductIdentityDecisionKind.Rejected);
                RuleFor(x => x.Request.DecisionEvidence.WorkflowInstanceId).NotEmpty();
                RuleFor(x => x.Request.DecisionEvidence.ApprovalTaskId).NotEmpty();
                RuleFor(x => x.Request.DecisionEvidence.WorkflowTemplateId).NotEmpty();
                RuleFor(x => x.Request.DecisionEvidence.WorkflowTemplateVersionId).NotEmpty();
                RuleFor(x => x.Request.DecisionEvidence.ObjectType)
                    .Equal("global-product", StringComparer.Ordinal);
                RuleFor(x => x.Request.DecisionEvidence.ObjectId).NotEmpty();
                RuleFor(x => x.Request.DecisionEvidence.ObjectRef)
                    .NotEmpty().MaximumLength(512).Must(IsExact);
                RuleFor(x => x.Request.DecisionEvidence.DecisionActorSubjectId).NotEmpty();
                RuleFor(x => x.Request.DecisionEvidence.ReasonCode)
                    .NotEmpty().MaximumLength(128).Must(value => value is not null && IsExact(value));
                RuleFor(x => x.Request.DecisionEvidence.DecisionAtUtc).Must(IsUtc);
                RuleFor(x => x.Request.DecisionEvidence.TransitionSequence).GreaterThan(0);
                RuleFor(x => x.Request.DecisionEvidence.TaskStatus)
                    .NotEmpty().MaximumLength(32).Must(IsExact);
                RuleFor(x => x.Request.DecisionEvidence.InstanceStatus)
                    .NotEmpty().MaximumLength(32).Must(IsExact);
            });
        });
    }

    private static bool IsExact(string value) =>
        string.Equals(value, value.Trim(), StringComparison.Ordinal)
        && !value.Any(char.IsControl);

    private static bool IsUtc(DateTimeOffset value) => value.Offset == TimeSpan.Zero;
}
