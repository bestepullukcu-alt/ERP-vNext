using System.IdentityModel.Tokens.Jwt;
using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using Diten.AuthService.Application.Common.Interfaces;
using Diten.AuthService.Application.S2S;
using Diten.AuthService.Domain.S2S;
using Microsoft.IdentityModel.Tokens;

namespace Diten.AuthService.Infrastructure.S2S;

public sealed class DelegatedActorProofValidator : IDelegatedActorProofValidator
{
    private const int MaximumTokenSizeBytes = 16 * 1024;
    private static readonly string[] ValidAudiences =
    [
        "diten-management-governance-service",
        "diten-fpa-service",
        "diten-decision-intelligence-service"
    ];

    private readonly IS2STrustedValidationKeyProvider _keyProvider;
    private readonly IServicePrincipalRepository _principalRepository;
    private readonly IServiceCredentialDescriptorRepository _credentialRepository;
    private readonly IS2SProofAcceptanceCoordinator _acceptanceCoordinator;
    private readonly DelegatedActorProofV1ContractValidator _contractValidator;
    private readonly TimeProvider _timeProvider;

    public DelegatedActorProofValidator(
        IS2STrustedValidationKeyProvider keyProvider,
        IServicePrincipalRepository principalRepository,
        IServiceCredentialDescriptorRepository credentialRepository,
        IS2SProofAcceptanceCoordinator acceptanceCoordinator,
        DelegatedActorProofV1ContractValidator contractValidator,
        TimeProvider timeProvider)
    {
        _keyProvider = keyProvider;
        _principalRepository = principalRepository;
        _credentialRepository = credentialRepository;
        _acceptanceCoordinator = acceptanceCoordinator;
        _contractValidator = contractValidator;
        _timeProvider = timeProvider;
    }

    public async Task<DelegatedActorProofValidationResult> ValidateAsync(DelegatedActorProofValidationRequest request, CancellationToken cancellationToken)
    {
        cancellationToken.ThrowIfCancellationRequested();
        if (string.IsNullOrEmpty(request.Token) || Encoding.UTF8.GetByteCount(request.Token) > MaximumTokenSizeBytes)
            return Failed(S2SAuthenticationFailureCode.MalformedToken);

        var header = ReadExactHeader(request.Token);
        if (header.Failure != S2SAuthenticationFailureCode.None) return Failed(header.Failure);

        var resolution = await _keyProvider.ResolveAsync(DelegatedActorProofV1.ExactIssuer, header.Kid!, cancellationToken);
        if (resolution.Kind == S2SKeyResolutionKind.AuthorityUnavailable) return Failed(S2SAuthenticationFailureCode.AuthorityUnavailable);
        if (resolution.Kind != S2SKeyResolutionKind.Resolved || resolution.Key is null) return Failed(S2SAuthenticationFailureCode.UnknownKey);
        var key = resolution.Key;
        var now = _timeProvider.GetUtcNow();
        if (!TrustedKeyIsAccepted(key, header.Kid!, now)) return Failed(S2SAuthenticationFailureCode.UnknownKey);

        JwtSecurityToken jwt;
        try
        {
            var handler = new JwtSecurityTokenHandler { MapInboundClaims = false, MaximumTokenSizeInBytes = MaximumTokenSizeBytes };
            var parameters = BuildValidationParameters(key.Key, now);
            handler.ValidateToken(request.Token, parameters, out var validatedToken);
            jwt = validatedToken as JwtSecurityToken ?? throw new SecurityTokenException("Invalid S2S token structure.");
        }
        catch (SecurityTokenInvalidIssuerException) { return Failed(S2SAuthenticationFailureCode.InvalidIssuer); }
        catch (SecurityTokenInvalidAudienceException) { return Failed(S2SAuthenticationFailureCode.InvalidAudience); }
        catch (SecurityTokenExpiredException) { return Failed(S2SAuthenticationFailureCode.InvalidLifetime); }
        catch (SecurityTokenNotYetValidException) { return Failed(S2SAuthenticationFailureCode.InvalidLifetime); }
        catch (SecurityTokenInvalidLifetimeException) { return Failed(S2SAuthenticationFailureCode.InvalidLifetime); }
        catch (SecurityTokenException) { return Failed(S2SAuthenticationFailureCode.InvalidSignature); }
        catch (ArgumentException) { return Failed(S2SAuthenticationFailureCode.MalformedToken); }

        DelegatedActorProofV1 proof;
        try
        {
            var claims = jwt.Claims.Select(x => new S2SClaim(x.Type, x.Value)).ToArray();
            proof = _contractValidator.Validate(claims);
        }
        catch (S2SContractException) { return Failed(S2SAuthenticationFailureCode.InvalidClaims); }

        if (jwt.Audiences.Count() != 1 || !string.Equals(jwt.Audiences.Single(), proof.Audience, StringComparison.Ordinal))
            return Failed(S2SAuthenticationFailureCode.InvalidAudience);
        if (!LifetimeIsExact(proof, now)) return Failed(S2SAuthenticationFailureCode.InvalidLifetime);

        var principal = await _principalRepository.GetByClientIdAsync(proof.ClientId, cancellationToken);
        if (principal is null || principal.Status != ServicePrincipalStatus.Active || now < principal.NotBeforeUtc ||
            (principal.ExpiresAtUtc is not null && now > principal.ExpiresAtUtc))
            return Failed(S2SAuthenticationFailureCode.InactivePrincipal);
        if (principal.ServicePrincipalId != proof.ServicePrincipalId ||
            !string.Equals(principal.ClientId, proof.AuthorizedParty, StringComparison.Ordinal) ||
            principal.PrincipalVersion != proof.ServicePrincipalVersion || principal.CredentialGeneration != proof.CredentialGeneration ||
            !principal.AllowsAudience(proof.Audience) || !principal.AllowsProtocolScope(proof.Scope))
            return Failed(S2SAuthenticationFailureCode.InactivePrincipal);

        var acceptedCredentials = await _credentialRepository.GetAcceptedAsync(principal.ServicePrincipalId, now, cancellationToken);
        var matchingCredentials = acceptedCredentials.Where(x => string.Equals(x.Kid, header.Kid, StringComparison.Ordinal)).ToArray();
        if (matchingCredentials.Length != 1) return Failed(S2SAuthenticationFailureCode.InvalidCredential);
        var credential = matchingCredentials[0];
        if (!CredentialMatches(credential, key, principal, proof, now)) return Failed(S2SAuthenticationFailureCode.InvalidCredential);

        if (request.ExpectedTenantId != proof.TenantId || !string.Equals(request.ExpectedOperationId, proof.OperationId, StringComparison.Ordinal))
            return Failed(S2SAuthenticationFailureCode.InvalidRequestBinding);
        string expectedHash;
        try { expectedHash = CanonicalRequestBinding.Compute(request.Method, request.Path, request.ExpectedTenantId, request.ExpectedOperationId, request.Body.Span); }
        catch (S2SContractException) { return Failed(S2SAuthenticationFailureCode.InvalidRequestBinding); }
        if (!FixedEquals(expectedHash, proof.RequestHash)) return Failed(S2SAuthenticationFailureCode.InvalidRequestBinding);

        var acceptance = await _acceptanceCoordinator.TryAcceptAsync(new S2SProofAcceptanceRequest(
            principal.ServicePrincipalId, principal.ClientId, principal.PrincipalVersion, principal.NotBeforeUtc, principal.ExpiresAtUtc,
            credential.CredentialId, credential.Generation, credential.Kid, credential.NotBeforeUtc, credential.ExpiresAtUtc, now,
            new S2SReplayReceipt(proof.Issuer, proof.Jti, proof.Nonce, proof.RequestHash, DateTimeOffset.FromUnixTimeSeconds(proof.ExpiresAt), now)), cancellationToken);
        if (acceptance.Kind == S2SProofAcceptanceKind.Replay) return Failed(S2SAuthenticationFailureCode.Replay);
        if (acceptance.Kind == S2SProofAcceptanceKind.StaleAuthority) return Failed(S2SAuthenticationFailureCode.InvalidCredential);
        if (acceptance.Kind == S2SProofAcceptanceKind.AuthorityUnavailable) return Failed(S2SAuthenticationFailureCode.AuthorityUnavailable);

        return DelegatedActorProofValidationResult.Success(new DelegatedActorProofProvenance(
            principal.ServicePrincipalId, credential.CredentialId, principal.PrincipalVersion, credential.Generation,
            proof.TenantId, proof.DelegatedActorId, proof.DelegationId, proof.ClientId, proof.Audience,
            proof.OperationId, proof.Permissions));
    }

    private TokenValidationParameters BuildValidationParameters(SecurityKey key, DateTimeOffset now) => new()
    {
        RequireSignedTokens = true,
        ValidateIssuerSigningKey = true,
        IssuerSigningKey = key,
        ValidAlgorithms = [SecurityAlgorithms.RsaSha256],
        AlgorithmValidator = (algorithm, _, _, _) => string.Equals(algorithm, SecurityAlgorithms.RsaSha256, StringComparison.Ordinal),
        ValidateIssuer = true,
        ValidIssuer = DelegatedActorProofV1.ExactIssuer,
        ValidateAudience = true,
        ValidAudiences = ValidAudiences,
        AudienceValidator = (audiences, _, _) => audiences is not null && audiences.Count() == 1 && ValidAudiences.Contains(audiences.Single(), StringComparer.Ordinal),
        ValidateLifetime = true,
        RequireExpirationTime = true,
        ClockSkew = TimeSpan.FromSeconds(DelegatedActorProofV1.MaximumClockSkewSeconds),
        LifetimeValidator = (notBefore, expires, _, _) => notBefore is not null && expires is not null &&
            notBefore.Value <= now.UtcDateTime.AddSeconds(DelegatedActorProofV1.MaximumClockSkewSeconds) &&
            expires.Value >= now.UtcDateTime.AddSeconds(-DelegatedActorProofV1.MaximumClockSkewSeconds)
    };

    private static (string? Kid, S2SAuthenticationFailureCode Failure) ReadExactHeader(string token)
    {
        var parts = token.Split('.');
        if (parts.Length != 3 || parts.Any(string.IsNullOrEmpty)) return (null, S2SAuthenticationFailureCode.MalformedToken);
        try
        {
            using var document = JsonDocument.Parse(Base64UrlEncoder.DecodeBytes(parts[0]));
            if (document.RootElement.ValueKind != JsonValueKind.Object) return (null, S2SAuthenticationFailureCode.MalformedToken);
            var properties = document.RootElement.EnumerateObject().ToArray();
            if (properties.Any(x => x.Name is not ("alg" or "typ" or "kid"))) return (null, S2SAuthenticationFailureCode.MalformedToken);
            string? One(string name) => properties.Count(x => x.NameEquals(name)) == 1
                ? properties.Single(x => x.NameEquals(name)).Value.GetString()
                : null;
            var typ = One("typ");
            var algorithm = One("alg");
            var kid = One("kid");
            if (!string.Equals(typ, DelegatedActorProofV1.ExactType, StringComparison.Ordinal)) return (null, S2SAuthenticationFailureCode.InvalidTokenType);
            if (!string.Equals(algorithm, SecurityAlgorithms.RsaSha256, StringComparison.Ordinal)) return (null, S2SAuthenticationFailureCode.InvalidAlgorithm);
            try { S2SExactValue.Required(kid!, "kid"); }
            catch (S2SContractException) { return (null, S2SAuthenticationFailureCode.InvalidKeyIdentifier); }
            return (kid, S2SAuthenticationFailureCode.None);
        }
        catch (Exception ex) when (ex is FormatException or JsonException or ArgumentException)
        {
            return (null, S2SAuthenticationFailureCode.MalformedToken);
        }
    }

    private static bool TrustedKeyIsAccepted(S2STrustedValidationKeyDescriptor key, string kid, DateTimeOffset now) =>
        string.Equals(key.Issuer, DelegatedActorProofV1.ExactIssuer, StringComparison.Ordinal) &&
        string.Equals(key.Kid, kid, StringComparison.Ordinal) && !key.Revoked &&
        key.Status is ServiceCredentialStatus.Active or ServiceCredentialStatus.Previous &&
        now >= key.NotBeforeUtc && now <= key.ExpiresAtUtc &&
        (key.Status != ServiceCredentialStatus.Previous || (key.OverlapValidUntilUtc is not null && now <= key.OverlapValidUntilUtc)) &&
        key.Key is RsaSecurityKey rsa && rsa.KeySize >= ServiceCredentialDescriptor.MinimumRsaKeySizeBits;

    private static bool LifetimeIsExact(DelegatedActorProofV1 proof, DateTimeOffset now)
    {
        var iat = DateTimeOffset.FromUnixTimeSeconds(proof.IssuedAt);
        var nbf = DateTimeOffset.FromUnixTimeSeconds(proof.NotBefore);
        var exp = DateTimeOffset.FromUnixTimeSeconds(proof.ExpiresAt);
        var skew = TimeSpan.FromSeconds(DelegatedActorProofV1.MaximumClockSkewSeconds);
        return iat <= nbf && nbf < exp && exp - iat <= TimeSpan.FromSeconds(DelegatedActorProofV1.MaximumLifetimeSeconds) &&
               iat <= now + skew && nbf <= now + skew && exp >= now - skew;
    }

    private static bool CredentialMatches(ServiceCredentialDescriptor credential, S2STrustedValidationKeyDescriptor key,
        ServicePrincipal principal, DelegatedActorProofV1 proof, DateTimeOffset now) =>
        credential.ServicePrincipalId == principal.ServicePrincipalId && credential.CredentialId == key.CredentialId &&
        credential.ServicePrincipalId == key.ServicePrincipalId && credential.Generation == proof.CredentialGeneration &&
        credential.Generation == key.Generation && string.Equals(credential.Kid, key.Kid, StringComparison.Ordinal) &&
        string.Equals(credential.Algorithm, ServiceCredentialDescriptor.RequiredAlgorithm, StringComparison.Ordinal) &&
        credential.PublicKeySizeBits >= ServiceCredentialDescriptor.MinimumRsaKeySizeBits && now >= credential.NotBeforeUtc && now <= credential.ExpiresAtUtc;

    private static bool FixedEquals(string left, string right)
    {
        var leftBytes = Encoding.UTF8.GetBytes(left);
        var rightBytes = Encoding.UTF8.GetBytes(right);
        return leftBytes.Length == rightBytes.Length && CryptographicOperations.FixedTimeEquals(leftBytes, rightBytes);
    }

    private static DelegatedActorProofValidationResult Failed(S2SAuthenticationFailureCode code) => DelegatedActorProofValidationResult.Failed(code);
}
