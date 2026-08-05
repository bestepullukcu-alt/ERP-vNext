namespace Diten.AuthService.Application.S2S;

public sealed record DelegatedActorProofIssuanceResult(string? Token, S2SAuthenticationFailureCode Failure)
{
    public bool Succeeded => Token is not null && Failure == S2SAuthenticationFailureCode.None;
    public int SuggestedHttpStatusCode => Failure == S2SAuthenticationFailureCode.AuthorityUnavailable ? 503 : Succeeded ? 200 : 401;
    public static DelegatedActorProofIssuanceResult Success(string token) => new(token, S2SAuthenticationFailureCode.None);
    public static DelegatedActorProofIssuanceResult Failed(S2SAuthenticationFailureCode failure) => new(null, failure);
}
