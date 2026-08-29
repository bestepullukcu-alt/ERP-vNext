using Diten.MdmService.Application.Features.ProductItemSkuMaster.Lifecycle.Commands;
using FluentValidation;

namespace Diten.MdmService.Application.Features.ProductItemSkuMaster.Lifecycle.Validators;

public sealed class SubmitGlobalProductIdentityValidator : AbstractValidator<SubmitGlobalProductIdentityCommand>
{
    public SubmitGlobalProductIdentityValidator()
    {
        RuleFor(x => x.Request).NotNull();
        When(x => x.Request is not null, () =>
        {
            RuleFor(x => x.Request.GlobalProductId).NotEmpty();
            RuleFor(x => x.Request.ExpectedVersion).GreaterThanOrEqualTo(0);
            RuleFor(x => x.Request.WorkflowBinding).NotNull();
            When(x => x.Request.WorkflowBinding is not null, () =>
            {
                RuleFor(x => x.Request.WorkflowBinding.WorkflowInstanceId).NotEmpty();
                RuleFor(x => x.Request.WorkflowBinding.WorkflowTemplateId).NotEmpty();
                RuleFor(x => x.Request.WorkflowBinding.WorkflowTemplateVersionId).NotEmpty();
                RuleFor(x => x.Request.WorkflowBinding.ApprovalTaskId).NotEmpty();
                RuleFor(x => x.Request.WorkflowBinding.AssignmentSnapshotId).NotEmpty();
                RuleFor(x => x.Request.WorkflowBinding.StartTransitionLogId).NotEmpty();
                RuleFor(x => x.Request.WorkflowBinding.ObjectType)
                    .Equal("global-product", StringComparer.Ordinal);
                RuleFor(x => x.Request.WorkflowBinding.ObjectId).NotEmpty();
                RuleFor(x => x.Request.WorkflowBinding.ObjectRef)
                    .NotEmpty().MaximumLength(512).Must(IsExact);
                RuleFor(x => x.Request.WorkflowBinding.SubmitterSubjectId).NotEmpty();
                RuleFor(x => x.Request.WorkflowBinding.StartIdempotencyKey)
                    .NotEmpty().MaximumLength(128).Must(IsExact);
                RuleFor(x => x.Request.WorkflowBinding.StartRequestFingerprint)
                    .Must(IsSha256);
                RuleFor(x => x.Request.WorkflowBinding.SubmittedAtUtc).Must(IsUtc);
                RuleFor(x => x.Request.WorkflowBinding.DueAtUtc)
                    .Must(value => value is null || IsUtc(value.Value));
                RuleFor(x => x.Request.WorkflowBinding.TerminalDecision).Null();
            });
        });
    }

    private static bool IsExact(string value) =>
        string.Equals(value, value.Trim(), StringComparison.Ordinal)
        && !value.Any(char.IsControl);

    private static bool IsSha256(string value) =>
        value.Length == 64 && value.All(Uri.IsHexDigit);

    private static bool IsUtc(DateTimeOffset value) => value.Offset == TimeSpan.Zero;
}
