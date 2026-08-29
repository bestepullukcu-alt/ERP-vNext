namespace Diten.AuthService.Application.S2S;
public enum S2SProofAcceptanceKind { Accepted, StaleAuthority, Replay, AuthorityUnavailable }
public sealed record S2SProofAcceptanceResult(S2SProofAcceptanceKind Kind)
{
    public static S2SProofAcceptanceResult Accepted() => new(S2SProofAcceptanceKind.Accepted);
    public static S2SProofAcceptanceResult StaleAuthority() => new(S2SProofAcceptanceKind.StaleAuthority);
    public static S2SProofAcceptanceResult Replay() => new(S2SProofAcceptanceKind.Replay);
    public static S2SProofAcceptanceResult AuthorityUnavailable() => new(S2SProofAcceptanceKind.AuthorityUnavailable);
}
