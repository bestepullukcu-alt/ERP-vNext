using Diten.InventoryService.Application.Common;
using Diten.InventoryService.Application.Features.Quarantine.Commands;
using FluentValidation;

namespace Diten.InventoryService.Application.Features.Quarantine.Validators;

/// <summary>MOD-0175-FU01 V1: the REVERSAL id is a non-empty UUID (an internal caller's programming error otherwise → 400).</summary>
public sealed class ApplySourceReversalToHoldValidator : AbstractValidator<ApplySourceReversalToHoldCommand>
{
    public ApplySourceReversalToHoldValidator()
    {
        RuleFor(x => x.ReversalTransactionId).Must(SourceReversalCommandIds.IsReversalId)
            .WithMessage("reversalTransactionId must be a non-empty UUID.")
            .WithErrorCode(InventoryErrorCodes.InvalidRequest);
    }
}

/// <summary>V1, shared by both validators.</summary>
public static class SourceReversalCommandIds
{
    public static bool IsReversalId(string? id) => Guid.TryParse(id, out var parsed) && parsed != Guid.Empty;
}
