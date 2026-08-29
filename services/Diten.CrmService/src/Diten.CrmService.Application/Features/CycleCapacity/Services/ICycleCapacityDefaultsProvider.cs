namespace Diten.CrmService.Application.Features.CycleCapacity.Services;

/// <summary>
/// MOD-0155 FU06 — the configured values a NEW capacity is born with. An interface rather than a constant because the
/// pack forbids magic numbers here: the eight-hour day and the interim FTE average are operational settings, and
/// moving either must be an ops change rather than a code change.
/// <para><b>The values are read once, at CREATE, and then STORED on the row.</b> Re-reading configuration at
/// calculation time would mean an old capacity silently produced a different figure the day someone edited a setting —
/// which is exactly the reproducibility problem the stored <c>Fte</c> exists to prevent.</para>
/// </summary>
public interface ICycleCapacityDefaultsProvider
{
    CycleCapacityDefaults Current { get; }
}

/// <summary>
/// The configured defaults.
/// </summary>
/// <param name="DailyWorkMinutes">Minutes in a field working day. 8 h × 60 = 480 unless configured otherwise.</param>
/// <param name="Fte">
/// The INTERIM average full-time-equivalent field force. There is no HR master to ask, so this is an operational
/// average — which is precisely why the resulting number is published as an ESTIMATE and why the field is disabled in
/// the UI (F-FTE-HR / F-FTE-BU).
/// </param>
public sealed record CycleCapacityDefaults(int DailyWorkMinutes, decimal Fte);
