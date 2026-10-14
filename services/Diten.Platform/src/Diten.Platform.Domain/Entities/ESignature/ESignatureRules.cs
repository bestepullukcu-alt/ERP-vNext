namespace Diten.Platform.Domain.Entities.ESignature;

/// <summary>
/// MOD-0022 — the pure server rules of signing (pack §8 E2 steps 3–6, V3 / V6): effective policy, visual mode × appearance,
/// re-authentication kind × policy. No I/O; the handlers call these in the pack's order.
/// </summary>
public static class ESignatureRules
{
    /// <summary>KS-5: the LegalEntity override when one exists for the subject's company, else the tenant default.</summary>
    public static ESignaturePolicy Effective(ESignaturePolicy tenantDefault, ESignaturePolicy? legalEntityOverride) =>
        legalEntityOverride ?? tenantDefault;

    /// <summary>The row for <paramref name="actionCode"/>; null when the action is not in the policy (no action = free signing).</summary>
    public static ESignaturePolicyRow? Row(ESignaturePolicy policy, string? actionCode) =>
        string.IsNullOrWhiteSpace(actionCode)
            ? null
            : policy.Rows.FirstOrDefault(r => string.Equals(r.ActionCode, actionCode, StringComparison.Ordinal));

    /// <summary>
    /// KS-8 (V6): None ⇒ no image · Optional ⇒ any · SavedAccepted ⇒ Sample or Drawn · DrawRequired ⇒ Drawn only.
    /// Without a policy row the visual signature is optional.
    /// </summary>
    public static bool AppearanceAllowed(ESignatureVisualMode mode, ESignatureAppearanceType appearance) => mode switch
    {
        ESignatureVisualMode.None => appearance == ESignatureAppearanceType.None,
        ESignatureVisualMode.Optional => true,
        ESignatureVisualMode.SavedAccepted => appearance is ESignatureAppearanceType.Sample or ESignatureAppearanceType.Drawn,
        ESignatureVisualMode.DrawRequired => appearance == ESignatureAppearanceType.Drawn,
        _ => false
    };

    /// <summary>
    /// KS-3 / KS-10 (V3): <c>ReauthEvery</c> ⇒ the password was typed for this ticket; otherwise a window ticket is good while
    /// its password proof is younger than the policy window; a saved-sample appearance and an invalidation always need a
    /// password ticket.
    /// </summary>
    public static bool ReauthSatisfies(
        ESignatureReauthKind kind,
        DateTime passwordVerifiedAtUtc,
        DateTime nowUtc,
        bool reauthEvery,
        int reauthWindowMinutes,
        bool passwordAlwaysRequired)
    {
        if (kind == ESignatureReauthKind.Password)
        {
            return true;
        }

        if (reauthEvery || passwordAlwaysRequired)
        {
            return false;
        }

        var window = Math.Clamp(reauthWindowMinutes, ESignaturePolicy.MinWindowMinutes, ESignaturePolicy.MaxWindowMinutes);
        var age = nowUtc - passwordVerifiedAtUtc;
        return age >= TimeSpan.Zero && age <= TimeSpan.FromMinutes(window);
    }

    /// <summary>
    /// Verification precedence (ESIGN-BUNDLE §2.6): invalidated &gt; tampered &gt; subjectUnresolvable &gt; changed &gt; valid.
    /// </summary>
    public static ESignatureVerificationStatus Verdict(bool invalidated, bool imageIntact, bool digestIntact, bool subjectResolved, bool subjectUnchanged)
    {
        if (invalidated)
        {
            return ESignatureVerificationStatus.Invalidated;
        }

        if (!imageIntact || !digestIntact)
        {
            return ESignatureVerificationStatus.Tampered;
        }

        if (!subjectResolved)
        {
            return ESignatureVerificationStatus.SubjectUnresolvable;
        }

        return subjectUnchanged ? ESignatureVerificationStatus.Valid : ESignatureVerificationStatus.Changed;
    }

    /// <summary>V13: trimmed length 10–1000.</summary>
    public static bool ReasonOk(string? reason)
    {
        var length = reason?.Trim().Length ?? 0;
        return length is >= 10 and <= 1000;
    }
}
