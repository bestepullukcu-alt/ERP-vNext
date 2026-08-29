using System.IdentityModel.Tokens.Jwt;
using System.Security.Claims;
using Diten.AuthService.Application.Common.Interfaces;
using Diten.AuthService.Application.S2S;
using Diten.AuthService.Domain.S2S;
using Microsoft.IdentityModel.Tokens;

namespace Diten.AuthService.Infrastructure.S2S;

public sealed class DelegatedActorProofIssuer : IDelegatedActorProofIssuer
{
    private readonly IS2SPrivateSigningKeyProvider _keyProvider;
    private readonly DelegatedActorProofV1ContractValidator _contractValidator;
    private readonly TimeProvider _timeProvider;

    public DelegatedActorProofIssuer(
        IS2SPrivateSigningKeyProvider keyProvider,
        DelegatedActorProofV1ContractValidator contractValidator,
        TimeProvider timeProvider)
    {
        _keyProvider = keyProvider;
        _contractValidator = contractValidator;
        _timeProvider = timeProvider;
    }

    public async Task<DelegatedActorProofIssuanceResult> IssueAsync(DelegatedActorProofIssuanceRequest request, CancellationToken cancellationToken)
    {
        cancellationToken.ThrowIfCancellationRequested();
        var now = _timeProvider.GetUtcNow();
        var proof = request.Proof;
        var principal = request.Principal;
        var credential = request.Credential;

        try { _contractValidator.Validate(S2SClaimProjection.FromProof(proof)); }
        catch (S2SContractException) { return DelegatedActorProofIssuanceResult.Failed(S2SAuthenticationFailureCode.InvalidClaims); }

        if (principal.Status != ServicePrincipalStatus.Active || now < principal.NotBeforeUtc || (principal.ExpiresAtUtc is not null && now > principal.ExpiresAtUtc))
            return DelegatedActorProofIssuanceResult.Failed(S2SAuthenticationFailureCode.InactivePrincipal);
        if (proof.ServicePrincipalId != principal.ServicePrincipalId ||
            !string.Equals(proof.ClientId, principal.ClientId, StringComparison.Ordinal) ||
            !string.Equals(proof.AuthorizedParty, principal.ClientId, StringComparison.Ordinal) ||
            proof.ServicePrincipalVersion != principal.PrincipalVersion ||
            proof.CredentialGeneration != principal.CredentialGeneration ||
            !principal.AllowsAudience(proof.Audience) || !principal.AllowsProtocolScope(proof.Scope))
            return DelegatedActorProofIssuanceResult.Failed(S2SAuthenticationFailureCode.InvalidClaims);

        if (credential.Status != ServiceCredentialStatus.Active || credential.ServicePrincipalId != principal.ServicePrincipalId ||
            credential.Generation != proof.CredentialGeneration || !string.Equals(credential.Kid, request.Kid, StringComparison.Ordinal) ||
            !string.Equals(credential.Algorithm, ServiceCredentialDescriptor.RequiredAlgorithm, StringComparison.Ordinal) ||
            credential.PublicKeySizeBits < ServiceCredentialDescriptor.MinimumRsaKeySizeBits || now < credential.NotBeforeUtc || now > credential.ExpiresAtUtc)
            return DelegatedActorProofIssuanceResult.Failed(S2SAuthenticationFailureCode.InvalidCredential);

        var issuedAt = DateTimeOffset.FromUnixTimeSeconds(proof.IssuedAt);
        var notBefore = DateTimeOffset.FromUnixTimeSeconds(proof.NotBefore);
        var expiresAt = DateTimeOffset.FromUnixTimeSeconds(proof.ExpiresAt);
        var skew = TimeSpan.FromSeconds(DelegatedActorProofV1.MaximumClockSkewSeconds);
        if (issuedAt > now + skew || notBefore > now + skew || expiresAt <= now || expiresAt - issuedAt > TimeSpan.FromSeconds(DelegatedActorProofV1.MaximumLifetimeSeconds))
            return DelegatedActorProofIssuanceResult.Failed(S2SAuthenticationFailureCode.InvalidLifetime);

        var resolution = await _keyProvider.ResolveAsync(DelegatedActorProofV1.ExactIssuer, request.Kid, cancellationToken);
        if (resolution.Kind == S2SKeyResolutionKind.AuthorityUnavailable)
            return DelegatedActorProofIssuanceResult.Failed(S2SAuthenticationFailureCode.AuthorityUnavailable);
        if (resolution.Kind != S2SKeyResolutionKind.Resolved || resolution.Key is null)
            return DelegatedActorProofIssuanceResult.Failed(S2SAuthenticationFailureCode.UnknownKey);

        var key = resolution.Key;
        if (!DescriptorMatches(key, principal, credential, now) || key.Key is not RsaSecurityKey rsaKey || rsaKey.KeySize < ServiceCredentialDescriptor.MinimumRsaKeySizeBits)
            return DelegatedActorProofIssuanceResult.Failed(S2SAuthenticationFailureCode.InvalidCredential);

        var credentials = new SigningCredentials(rsaKey, SecurityAlgorithms.RsaSha256);
        var header = new JwtHeader(credentials) { ["typ"] = DelegatedActorProofV1.ExactType, ["kid"] = request.Kid };
        var payload = new JwtPayload();
        foreach (var claim in S2SClaimProjection.FromProof(proof)) payload.AddClaim(new Claim(claim.Type, claim.Value));
        var token = new JwtSecurityToken(header, payload);
        return DelegatedActorProofIssuanceResult.Success(new JwtSecurityTokenHandler().WriteToken(token));
    }

    private static bool DescriptorMatches(S2SPrivateSigningKeyDescriptor key, ServicePrincipal principal, ServiceCredentialDescriptor credential, DateTimeOffset now) =>
        string.Equals(key.Issuer, DelegatedActorProofV1.ExactIssuer, StringComparison.Ordinal) &&
        string.Equals(key.Kid, credential.Kid, StringComparison.Ordinal) && key.CredentialId == credential.CredentialId &&
        key.ServicePrincipalId == principal.ServicePrincipalId && key.Generation == credential.Generation &&
        now >= key.NotBeforeUtc && now <= key.ExpiresAtUtc;
}
