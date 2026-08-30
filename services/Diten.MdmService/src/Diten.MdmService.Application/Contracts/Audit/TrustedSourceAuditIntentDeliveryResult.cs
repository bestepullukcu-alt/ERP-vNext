namespace Diten.MdmService.Application.Contracts.Audit;

public sealed record TrustedSourceAuditIntentDeliveryResult(
    TrustedSourceAuditIntentDeliveryOutcome Outcome,
    TrustedSourceAuditIntentAcceptanceReceipt? Receipt,
    string ErrorCode)
{
    public static TrustedSourceAuditIntentDeliveryResult Accepted(TrustedSourceAuditIntentAcceptanceReceipt receipt) =>
        new(TrustedSourceAuditIntentDeliveryOutcome.Accepted, receipt, string.Empty);

    public static TrustedSourceAuditIntentDeliveryResult AuthenticationRejected(string code) =>
        new(TrustedSourceAuditIntentDeliveryOutcome.AuthenticationRejected, null, code);

    public static TrustedSourceAuditIntentDeliveryResult Retryable(string code) =>
        new(TrustedSourceAuditIntentDeliveryOutcome.Retryable, null, code);

    public static TrustedSourceAuditIntentDeliveryResult Terminal(string code) =>
        new(TrustedSourceAuditIntentDeliveryOutcome.Terminal, null, code);
}

public enum TrustedSourceAuditIntentDeliveryOutcome
{
    Accepted = 1,
    AuthenticationRejected = 2,
    Retryable = 3,
    Terminal = 4
}
