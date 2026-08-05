namespace Diten.AuthService.Domain.S2S;

public static class S2STokenFamilyProfile
{
    public const string Algorithm = ServiceCredentialDescriptor.RequiredAlgorithm;
    public const string TokenType = DelegatedActorProofV1.ExactType;

    public static bool Accepts(string algorithm, string tokenType) =>
        string.Equals(algorithm, Algorithm, StringComparison.Ordinal) &&
        string.Equals(tokenType, TokenType, StringComparison.Ordinal);
}
