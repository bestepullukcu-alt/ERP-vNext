namespace Diten.MdmService.Domain.Entities;

/// <summary>
/// WP-MDM-SCOPE-ACCEPT-01 FIX1 B3 — evidence of an abandoned activation / suspension fence. A fence is only ever
/// abandoned by its own command (the transition that took it, failing its readiness or inventory check, or an operator
/// re-running the same command with the abort action); the record says which, so a reopened tenant is never silent.
/// </summary>
public sealed class ProductLegalEntityScopeFenceAbort
{
    public string Token { get; set; } = string.Empty;
    public string Action { get; set; } = string.Empty;
    public Guid CommandId { get; set; }
    public Guid ActorId { get; set; }
    public string ReasonCode { get; set; } = string.Empty;
    public string Cause { get; set; } = string.Empty;
    public DateTimeOffset AbortedAtUtc { get; set; }

    public void EnsureValid()
    {
        if (Token.Length != 64 || !Token.All(Uri.IsHexDigit) || CommandId == Guid.Empty || ActorId == Guid.Empty
            || Action is not ("ActivateEnforced" or "SuspendFailClosed")
            || !ProductLegalEntityScopeActivationFence.Reason(ReasonCode)
            || !ProductLegalEntityScopeActivationFence.Reason(Cause)
            || AbortedAtUtc.Offset != TimeSpan.Zero)
        {
            throw new InvalidOperationException("PRODUCT_SCOPE_FENCE_ABORT_INVALID");
        }
    }
}
