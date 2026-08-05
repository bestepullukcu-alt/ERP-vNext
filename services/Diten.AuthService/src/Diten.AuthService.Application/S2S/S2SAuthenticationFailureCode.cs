namespace Diten.AuthService.Application.S2S;

public enum S2SAuthenticationFailureCode
{
    None = 0,
    MalformedToken,
    InvalidTokenType,
    InvalidAlgorithm,
    InvalidKeyIdentifier,
    UnknownKey,
    InvalidSignature,
    InvalidIssuer,
    InvalidAudience,
    InvalidLifetime,
    InvalidClaims,
    InactivePrincipal,
    InvalidCredential,
    InvalidRequestBinding,
    Replay,
    AuthorityUnavailable
}
