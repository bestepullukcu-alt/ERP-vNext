using Diten.Platform.Application.Features.DocumentManagementControlledCopy.Commands;
using FluentValidation;

namespace Diten.Platform.Application.Features.DocumentManagementControlledCopy.Validators;

// MOD-0029-FU17 — input-shape validators. Eligibility, evidence and reconciliation rules stay in the service.

public sealed class RegisterControlledCopyValidator : AbstractValidator<RegisterControlledCopyCommand>
{
    public RegisterControlledCopyValidator()
    {
        RuleFor(x => x.RegisterEntryId).NotEmpty();
        RuleFor(x => x.Input).NotNull();
        RuleFor(x => x.Input.CopyType).NotEmpty().When(x => x.Input is not null);
    }
}

public sealed class WithdrawControlledCopyValidator : AbstractValidator<WithdrawControlledCopyCommand>
{
    public WithdrawControlledCopyValidator()
    {
        RuleFor(x => x.CopyId).NotEmpty();
        RuleFor(x => x.Input).NotNull();
        RuleFor(x => x.Input.WithdrawalEvidenceReference).NotEmpty().When(x => x.Input is not null);
    }
}

public sealed class ReconcileControlledCopyValidator : AbstractValidator<ReconcileControlledCopyCommand>
{
    public ReconcileControlledCopyValidator()
    {
        RuleFor(x => x.CopyId).NotEmpty();
        RuleFor(x => x.Input).NotNull();
        RuleFor(x => x.Input.ReconciliationEvidenceReference).NotEmpty().When(x => x.Input is not null);
    }
}

public sealed class MarkControlledCopyObsoleteValidator : AbstractValidator<MarkControlledCopyObsoleteCommand>
{
    public MarkControlledCopyObsoleteValidator()
    {
        RuleFor(x => x.CopyId).NotEmpty();
        RuleFor(x => x.Input).NotNull();
        RuleFor(x => x.Input.ObsoleteReason).NotEmpty().When(x => x.Input is not null);
    }
}

public sealed class ResolveObsoleteCopyFindingValidator : AbstractValidator<ResolveObsoleteCopyFindingCommand>
{
    public ResolveObsoleteCopyFindingValidator()
    {
        RuleFor(x => x.FindingId).NotEmpty();
        RuleFor(x => x.Input).NotNull();
        RuleFor(x => x.Input.ResolutionEvidenceReference).NotEmpty().When(x => x.Input is not null);
    }
}
