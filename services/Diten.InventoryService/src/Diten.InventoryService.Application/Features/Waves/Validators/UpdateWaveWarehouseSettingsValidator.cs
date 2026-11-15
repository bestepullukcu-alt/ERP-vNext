using Diten.InventoryService.Application.Features.Waves.Commands;
using Diten.InventoryService.Domain.WavePlanning.Rules;
using FluentValidation;

namespace Diten.InventoryService.Application.Features.Waves.Validators;

/// <summary>
/// Pack §12 — the warehouse setting's shape (400 <c>INVALID_REQUEST</c>, nothing written): the three thresholds in their ranges, the time zone an
/// IANA id the runtime resolves, and a time zone whenever the warehouse is wave managed. The reason is the handler's (400
/// <c>WAVE_REASON_REQUIRED</c>).
/// </summary>
public sealed class UpdateWaveWarehouseSettingsValidator : AbstractValidator<UpdateWaveWarehouseSettingsCommand>
{
    public UpdateWaveWarehouseSettingsValidator() => RuleFor(x => x.Settings).Custom((settings, context) =>
    {
        if (!WaveRules.RiskMinutesInRange(settings.RiskMinutesBeforeCutoff))
            WaveFormShape.Fail(context, $"riskMinutesBeforeCutoff must be 1-{WaveRules.MaxRiskMinutes}.");
        if (!WaveRules.RiskPercentInRange(settings.RiskProgressPercent))
            WaveFormShape.Fail(context, $"riskProgressPercent must be 1-{WaveRules.MaxRiskPercent}.");
        if (!WaveRules.PendingMinutesInRange(settings.PendingTaskMinutes))
            WaveFormShape.Fail(context, $"pendingTaskMinutes must be 1-{WaveRules.MaxPendingMinutes}.");
        var zone = string.IsNullOrWhiteSpace(settings.TimeZoneId) ? null : settings.TimeZoneId;
        if (zone is not null && !WaveCutoffRules.IsKnownIanaZone(zone)) WaveFormShape.Fail(context, "timeZoneId must be an IANA time zone id (e.g. Europe/Istanbul).");
        if (settings.WaveManaged && zone is null) WaveFormShape.Fail(context, "timeZoneId is required when waveManaged is true.");
    });
}
