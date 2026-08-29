namespace Diten.AuthService.Application.S2S;

public sealed record DelegatedActorProofValidationResult(DelegatedActorProofProvenance? Provenance, S2SAuthenticationFailureCode Failure)
{
    public bool Succeeded => Provenance is not null && Failure == S2SAuthenticationFailureCode.None;
    public int SuggestedHttpStatusCode => Failure == S2SAuthenticationFailureCode.AuthorityUnavailable ? 503 : Succeeded ? 200 : 401;
    public static DelegatedActorProofValidationResult Success(DelegatedActorProofProvenance provenance) => new(provenance, S2SAuthenticationFailureCode.None);
    public static DelegatedActorProofValidationResult Failed(S2SAuthenticationFailureCode failure) => new(null, failure);
}
