using Diten.MdmService.Application.Features.ProductItemSkuMaster.Lifecycle.OrphanedOperationRecovery.Commands;
using FluentValidation;

namespace Diten.MdmService.Application.Features.ProductItemSkuMaster.Lifecycle.OrphanedOperationRecovery.Validators;

public sealed class RecoverOrphanedProductIdentityWorkflowOperationValidator
    : AbstractValidator<RecoverOrphanedProductIdentityWorkflowOperationCommand>
{
    public RecoverOrphanedProductIdentityWorkflowOperationValidator()
    {
        RuleFor(command => command.OperationId).NotEmpty();
        RuleFor(command => command.CommandId).NotEmpty();
        RuleFor(command => command.Request).NotNull();
        When(command => command.Request is not null, () =>
        {
            RuleFor(command => command.Request.Action).IsInEnum()
                .Must(action => action is ProductIdentityWorkflowOperationRecoveryAction.Abandon
                    or ProductIdentityWorkflowOperationRecoveryAction.Supersede);
            RuleFor(command => command.Request.ExpectedOperationVersion)
                .InclusiveBetween(0, int.MaxValue - 1);
            RuleFor(command => command.Request.ExpectedTargetVersion).NotNull();
            When(command => command.Request.ExpectedTargetVersion is not null, () =>
            {
                RuleFor(command => command.Request.ExpectedTargetVersion.PrimaryEntityVersion)
                    .InclusiveBetween(0, int.MaxValue - 1);
                RuleFor(command => command.Request.ExpectedTargetVersion.ProductDefinitionRevisionVersion)
                    .InclusiveBetween(0, int.MaxValue - 1)
                    .When(command => command.Request.ExpectedTargetVersion.ProductDefinitionRevisionVersion.HasValue);
            });
            RuleFor(command => command.Request.ReasonCode)
                .NotEmpty().MaximumLength(128).Must(Exact);
            RuleFor(command => command.Request.Comment)
                .MaximumLength(512).Must(value => value is null || Exact(value));
        });
    }

    private static bool Exact(string value) =>
        string.Equals(value, value.Trim(), StringComparison.Ordinal)
        && !value.Any(char.IsControl);
}
