namespace Diten.MdmService.Api.Configuration;

/// <summary>
/// WP-MDM-SETTLE-CAP-01 (FIX1, CT decision M5) — the one rule for every approval worker's <c>StartSettleSeconds</c>: how
/// long after a start MDM does not trust the engine's "nothing arrived". Platform's sweep closes a half-started workflow
/// that has no task after <c>WorkflowStartRecovery:AbandonAfterMinutes</c>; its FLOOR is 15 min
/// (<c>WorkflowStartRecoveryOptions.MinimumAbandonAfterMinutes</c>, engine lane <c>0478d4e63</c>), its default 30. A
/// longer MDM window would keep MDM waiting on a start Platform has already closed.
///
/// <para><b>Operator rule:</b> Platform <c>AbandonAfterMinutes</c> × 60 ≥ MDM <c>StartSettleSeconds</c> + 300. The cap
/// holds it for ANY Platform setting: 600 s = the 15-min floor − 5 min of margin. When the engine lane is merged, the
/// cross-service guard <c>MaxSeconds + 300 ≤ MinimumAbandonAfterMinutes × 60</c> is Control Tower's (named here).</para>
///
/// Every <c>*WorkflowWorkerOptions</c> that carries the setting checks it here (StartSettleWindowGuardTests holds them to it).
/// </summary>
public static class StartSettleWindow
{
    public const int MaxSeconds = 600;

    public static bool IsValid(int seconds) => seconds is >= 1 and <= MaxSeconds;
}
