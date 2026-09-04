using Diten.CrmService.Application.Features.CycleCapacity.Services;
using Diten.CrmService.Domain.Entities;
using Microsoft.Extensions.Configuration;

namespace Diten.CrmService.Infrastructure.CycleCapacity;

/// <summary>
/// MOD-0155 FU06 — reads the capacity defaults from configuration, so the eight-hour day and the interim FTE average
/// are OPERATIONAL settings rather than constants compiled into a rule.
/// <para>Both values are copied onto a capacity when it is CREATED and are never re-read afterwards, which is what
/// keeps an old estimate reproducible after someone changes a setting.</para>
/// <para>A missing or nonsensical setting falls back to the documented default rather than throwing: a service that
/// refuses to start because an optional number is absent is worse than one that starts with 480 minutes and 1.00
/// FTE — and the FTE is visible on every screen, so a wrong value is immediately obvious rather than silent.</para>
/// </summary>
public sealed class ConfigurationCycleCapacityDefaultsProvider : ICycleCapacityDefaultsProvider
{
    /// <summary>8 h × 60. The pack's number, stated once.</summary>
    public const int FallbackDailyWorkMinutes = 480;

    /// <summary>Deliberately 1.00, not a flattering guess. Without an HR source the honest neutral multiplier is one
    /// representative; an invented "12" would make every untouched tenant's estimate look authoritative and be
    /// wrong.</summary>
    public const decimal FallbackFte = 1.00m;

    /// <summary>MOD-0155 FU06B — five minutes between two visits. The pack's number, stated once.</summary>
    public const int FallbackBetweenVisitTimeMinutes = 5;

    private readonly CycleCapacityDefaults _defaults;

    public ConfigurationCycleCapacityDefaultsProvider(IConfiguration configuration)
    {
        var dailyWorkMinutes = configuration.GetValue<int?>("CycleCapacity:DefaultDailyWorkMinutes");
        var fte = configuration.GetValue<decimal?>("CycleCapacity:DefaultFte");
        var betweenVisit = configuration.GetValue<int?>("CycleCapacity:DefaultBetweenVisitTimeMinutes");

        _defaults = new CycleCapacityDefaults(
            dailyWorkMinutes is > 0 and <= 1440 ? dailyWorkMinutes.Value : FallbackDailyWorkMinutes,
            fte is > 0m and <= 9999m ? fte.Value : FallbackFte,
            betweenVisit is >= 0 and <= CycleCapacityLimits.MaxBufferMinutes
                ? betweenVisit.Value
                : FallbackBetweenVisitTimeMinutes);
    }

    public CycleCapacityDefaults Current => _defaults;
}
