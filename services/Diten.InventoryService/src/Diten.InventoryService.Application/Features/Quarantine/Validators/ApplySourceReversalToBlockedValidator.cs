using Diten.InventoryService.Application.Common;
using Diten.InventoryService.Application.Features.Quarantine.Commands;
using FluentValidation;

namespace Diten.InventoryService.Application.Features.Quarantine.Validators;

/// <summary>MOD-0175-FU01 V1 for the blocked-stock command.</summary>
public sealed class ApplySourceReversalToBlockedValidator : AbstractValidator<ApplySourceReversalToBlockedCommand>
{
    public ApplySourceReversalToBlockedValidator()
    {
        RuleFor(x => x.ReversalTransactionId).Must(SourceReversalCommandIds.IsReversalId)
            .WithMessage("reversalTransactionId must be a non-empty UUID.")
            .WithErrorCode(InventoryErrorCodes.InvalidRequest);
    }
}
