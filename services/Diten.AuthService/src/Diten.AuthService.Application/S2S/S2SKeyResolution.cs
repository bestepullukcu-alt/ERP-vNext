namespace Diten.AuthService.Application.S2S;

public sealed record S2SKeyResolution<T>(S2SKeyResolutionKind Kind, T? Key) where T : class
{
    public static S2SKeyResolution<T> Resolved(T key) => new(S2SKeyResolutionKind.Resolved, key);
    public static S2SKeyResolution<T> Unknown() => new(S2SKeyResolutionKind.Unknown, null);
    public static S2SKeyResolution<T> AuthorityUnavailable() => new(S2SKeyResolutionKind.AuthorityUnavailable, null);
}
