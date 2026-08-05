namespace Diten.AuthService.Application.S2S;

public sealed record ReplayReceiptAcceptance(ReplayReceiptAcceptanceKind Kind)
{
    public int SuggestedHttpStatusCode => Kind switch
    {
        ReplayReceiptAcceptanceKind.Accepted => 204,
        ReplayReceiptAcceptanceKind.Replay => 401,
        ReplayReceiptAcceptanceKind.AuthorityUnavailable => 503,
        _ => 503
    };

    public static ReplayReceiptAcceptance Accepted() => new(ReplayReceiptAcceptanceKind.Accepted);
    public static ReplayReceiptAcceptance Replay() => new(ReplayReceiptAcceptanceKind.Replay);
    public static ReplayReceiptAcceptance AuthorityUnavailable() => new(ReplayReceiptAcceptanceKind.AuthorityUnavailable);
}
