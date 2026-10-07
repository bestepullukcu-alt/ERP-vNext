namespace Diten.MdmService.Api.Configuration;

/// <summary>
/// WP-MDM-SETTLE-CAP-01 (from BL-552 flow 3 FIX3 EK, XMC-001): the one rule for every approval worker's
/// <c>StartSettleSeconds</c> — how long after a start MDM does not trust the engine's "nothing arrived".
/// Platform's sweep closes a half-started workflow that has no task after
/// <c>WorkflowStartRecovery:AbandonAfterMinutes</c> (at least 15 min, default 30). A longer window would keep MDM waiting
/// on a start Platform has already closed; 25 min keeps the sweep's shortest age at least 5 min longer.
/// Every <c>*WorkflowWorkerOptions</c> that carries the setting checks it here (a reflection guard test holds them to it).
/// </summary>
public static class StartSettleWindow
{
    public const int MaxSeconds = 1_500;

    public static bool IsValid(int seconds) => seconds is >= 1 and <= MaxSeconds;
}
