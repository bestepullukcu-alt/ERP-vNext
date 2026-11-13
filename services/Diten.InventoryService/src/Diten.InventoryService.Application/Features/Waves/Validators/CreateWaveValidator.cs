using Diten.InventoryService.Application.Common;
using Diten.InventoryService.Application.Features.Waves.Commands;
using Diten.InventoryService.Domain.WavePlanning.Enums;
using Diten.InventoryService.Domain.WavePlanning.Rules;
using FluentValidation;

namespace Diten.InventoryService.Application.Features.Waves.Validators;

/// <summary>Pack §12 — the shape of the Draft form on create (400 <c>INVALID_REQUEST</c>; business checks are the handler's).</summary>
public sealed class CreateWaveValidator : AbstractValidator<CreateWaveCommand>
{
    public CreateWaveValidator() => RuleFor(x => x.Wave).Custom((form, context) => WaveFormShape.Check(form, context));
}

/// <summary>The shape rules shared by the wave validators.</summary>
internal static class WaveFormShape
{
    public static void Fail<T>(ValidationContext<T> context, string message)
        => context.AddFailure(new FluentValidation.Results.ValidationFailure("request", message) { ErrorCode = InventoryErrorCodes.InvalidRequest });

    public static void Check<T>(WaveFormInput form, ValidationContext<T> context)
    {
        if (form.WarehouseId == Guid.Empty) Fail(context, "warehouseId is required.");
        if (!Enum.TryParse<WaveType>(form.WaveType, false, out var type) || !Enum.IsDefined(type) || form.WaveType != type.ToString())
            Fail(context, "waveType must be one of " + string.Join(", ", Enum.GetNames<WaveType>()) + ".");
        if (!WaveCutoffRules.TryParseLocal(form.CutoffLocal, out _)) Fail(context, "cutoffLocal must be a local date-time (yyyy-MM-ddTHH:mm).");
        if (form.ShipToFilter?.Trim() is { Length: > WaveRules.MaxShipToFilterLength })
            Fail(context, $"shipToFilter must be at most {WaveRules.MaxShipToFilterLength} characters.");
        if (form.MaxOrders is < WaveRules.MinMaxOrders or > WaveRules.MaxMaxOrders)
            Fail(context, $"maxOrders must be {WaveRules.MinMaxOrders}-{WaveRules.MaxMaxOrders}.");
        if (form.MaxLines is < WaveRules.MinMaxLines or > WaveRules.MaxMaxLines)
            Fail(context, $"maxLines must be {WaveRules.MinMaxLines}-{WaveRules.MaxMaxLines}.");
        if (form.ResponsibleUserId == Guid.Empty) Fail(context, "responsibleUserId must be a non-empty UUID.");
        if (form.Note?.Trim() is { Length: > WaveRules.MaxNoteLength }) Fail(context, $"note must be at most {WaveRules.MaxNoteLength} characters.");
    }

    /// <summary>An optional free-text reason (edit / add / remove): at most 500 characters after trim.</summary>
    public static void OptionalReason<T>(string? reason, ValidationContext<T> context)
    {
        if (reason?.Trim() is { Length: > WaveRules.MaxReasonLength }) Fail(context, $"reason must be at most {WaveRules.MaxReasonLength} characters.");
    }

    /// <summary>Pack §12: 1..200 outbound ids, non-empty, not repeated.</summary>
    public static void OutboundIds<T>(IReadOnlyList<Guid> ids, ValidationContext<T> context)
    {
        if (ids.Count is < 1 or > WaveRules.MaxOutboundsPerRequest) Fail(context, $"outboundIds names 1-{WaveRules.MaxOutboundsPerRequest} outbounds.");
        if (ids.Contains(Guid.Empty)) Fail(context, "outboundIds must be non-empty UUIDs.");
        if (ids.Distinct().Count() != ids.Count) Fail(context, "outboundIds must not repeat.");
    }
}
