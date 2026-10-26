namespace Diten.BuildingBlocks.CentralAudit;

/// <summary>
/// Where the events go and how often. The address and the key are the SAME two settings every service already uses to
/// reach Platform's internal endpoints (MDM's audit forwarder, every service's module self-registration):
/// <c>PlatformRegistration:BaseUrl</c> and <c>PlatformRegistration:InternalApiKey</c> — the key Platform checks as
/// <c>AuthService:InternalApiKey</c>. Tuning lives under <c>CentralAudit:*</c>.
///
/// <para>Unset address or key does NOT stop commands: events keep accumulating in the outbox, the dispatcher logs a
/// warning every <see cref="UnconfiguredWarningInterval"/> and the health check reports Degraded (AUD-001 §4.3: "not
/// silent"; closes K8 for every service on this package).</para>
/// </summary>
public sealed class CentralAuditOptions
{
    public const string PlatformSection = "PlatformRegistration";
    public const string TuningSection = "CentralAudit";

    public string? PlatformBaseUrl { get; set; }

    /// <summary>⚠ A secret. Sent as <c>X-Internal-Api-Key</c>; never logged.</summary>
    public string? InternalApiKey { get; set; }

    public TimeSpan PollInterval { get; set; } = TimeSpan.FromSeconds(5);

    public int BatchSize { get; set; } = 50;

    public TimeSpan BaseBackoff { get; set; } = TimeSpan.FromSeconds(5);

    public TimeSpan MaxBackoff { get; set; } = TimeSpan.FromMinutes(10);

    public TimeSpan UnconfiguredWarningInterval { get; set; } = TimeSpan.FromMinutes(1);

    /// <summary>A waiting row older than this makes the health check Degraded (delivery is stuck).</summary>
    public TimeSpan StaleAfter { get; set; } = TimeSpan.FromMinutes(10);

    /// <summary>A row written in a command's transaction that the command never confirmed is released after this.</summary>
    public TimeSpan OrphanAfter { get; set; } = TimeSpan.FromMinutes(10);

    /// <summary>
    /// Set by a service whose every write runs in a Mongo transaction that enlists the event (Inventory). Then a marked
    /// command that SUCCEEDED without enlisting wrote nothing — an idempotent replay, a no-op — and leaves no record
    /// (otherwise every replay would file a second "Create"). A command that owns no transaction carries
    /// <see cref="ICentralAuditOutsideTransaction"/> and is recorded right after it succeeds. The service pairs this with a
    /// guard that every transaction of its persistence enlists (a forgotten one would otherwise drop records silently).
    /// </summary>
    public bool RequireEnlistment { get; set; }

    /// <summary>The background dispatcher can be switched off (tests that drive <see cref="CentralAuditDispatcher.RunOnceAsync"/> themselves).</summary>
    public bool DispatcherEnabled { get; set; } = true;

    public bool IsConfigured => !string.IsNullOrWhiteSpace(PlatformBaseUrl) && !string.IsNullOrWhiteSpace(InternalApiKey);

    /// <summary>Exponential, capped: attempt 1 → base, 2 → 2×base, … ≤ <see cref="MaxBackoff"/>.</summary>
    public TimeSpan BackoffFor(int attempts)
    {
        var factor = Math.Pow(2, Math.Clamp(attempts - 1, 0, 20));
        var delay = TimeSpan.FromTicks((long)Math.Min(BaseBackoff.Ticks * factor, MaxBackoff.Ticks));
        return delay < BaseBackoff ? BaseBackoff : delay;
    }
}
