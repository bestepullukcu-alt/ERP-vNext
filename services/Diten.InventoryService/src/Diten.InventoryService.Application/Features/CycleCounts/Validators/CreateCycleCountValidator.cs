using System.Globalization;
using Diten.InventoryService.Application.Common;
using Diten.InventoryService.Application.Features.CycleCounts.Commands;
using Diten.InventoryService.Domain.CycleCounting.Rules;
using FluentValidation;

namespace Diten.InventoryService.Application.Features.CycleCounts.Validators;

/// <summary>Pack §12 — the shape of the Draft form on create (400 <c>INVALID_REQUEST</c>; business checks are the handler's).</summary>
public sealed class CreateCycleCountValidator : AbstractValidator<CreateCycleCountCommand>
{
    public CreateCycleCountValidator() => RuleFor(x => x.Count).Custom((form, context) => CycleCountFormShape.Check(form, context));
}

/// <summary>The form's shape rules, shared by create and update.</summary>
internal static class CycleCountFormShape
{
    public static void Check<T>(CycleCountFormInput form, ValidationContext<T> context)
    {
        void Fail(string message) => context.AddFailure(new FluentValidation.Results.ValidationFailure(nameof(form), message)
            { ErrorCode = InventoryErrorCodes.InvalidRequest });

        if (form.WarehouseId == Guid.Empty) Fail("warehouseId is required.");
        if (form.CountType is not ("Cycle" or "Full")) Fail("countType must be Cycle or Full.");
        if (form.CountType == "Cycle" && form.ScopeLocationIds.Count is < 1 or > CycleCountRules.MaxScopeLocations)
            Fail($"A Cycle count names 1-{CycleCountRules.MaxScopeLocations} scopeLocationIds.");
        if (form.CountType == "Full" && form.ScopeLocationIds.Count > 0) Fail("A Full count names no scopeLocationIds (it covers the whole warehouse).");
        if (form.ScopeLocationIds.Distinct().Count() != form.ScopeLocationIds.Count) Fail("scopeLocationIds must not repeat.");
        if (form.ScopeSkuIds.Count > CycleCountRules.MaxScopeSkus) Fail($"scopeSkuIds holds at most {CycleCountRules.MaxScopeSkus} SKUs.");
        if (form.ScopeSkuIds.Distinct().Count() != form.ScopeSkuIds.Count) Fail("scopeSkuIds must not repeat.");
        if (form.ScopeLocationIds.Contains(Guid.Empty) || form.ScopeSkuIds.Contains(Guid.Empty)) Fail("Scope ids must be non-empty UUIDs.");
        if (!DateOnly.TryParseExact(form.PlannedDate, "yyyy-MM-dd", CultureInfo.InvariantCulture, DateTimeStyles.None, out _))
            Fail("plannedDate must be a date (yyyy-MM-dd).");
        if (form.AssignedTo == Guid.Empty) Fail("assignedTo must be a non-empty UUID.");
        if (form.Note?.Trim() is { Length: > CycleCountRules.MaxNoteLength }) Fail($"note must be at most {CycleCountRules.MaxNoteLength} characters.");
    }
}
