using Diten.AuthService.Application.S2S;
using Diten.AuthService.Domain.S2S;

namespace Diten.AuthService.Application.Tests.S2S;

public sealed class DelegatedActorProofV1ContractTests
{
    private readonly DelegatedActorProofV1ContractValidator _validator = new();

    [Fact]
    public void Exact_contract_is_accepted()
    {
        var proof = _validator.Validate(ValidClaims());
        Assert.Equal(DelegatedActorProofV1.ExactType, proof.Type);
        Assert.Equal("diten-fpa-service", proof.Audience);
        Assert.Equal(["fpa.budget.read"], proof.Permissions);
    }

    [Theory]
    [InlineData("typ", "DITEN-DELEGATED-ACTOR-PROOF+JWT")]
    [InlineData("aud", "diten-erp")]
    [InlineData("client_id", "DITEN-FPA-PRODUCER")]
    [InlineData("scope", "diten.s2s.*")]
    [InlineData("permission", "fpa.budget.read ")]
    public void Alias_case_wildcard_and_normalization_are_rejected(string type, string value)
    {
        Assert.Throws<S2SContractException>(() => _validator.Validate(Replace(type, value)));
    }

    [Fact]
    public void Missing_duplicate_unknown_and_malformed_claims_are_rejected()
    {
        Assert.Throws<S2SContractException>(() => _validator.Validate(ValidClaims().Where(x => x.Type != "jti")));
        Assert.Throws<S2SContractException>(() => _validator.Validate(ValidClaims().Append(new S2SClaim("jti", Guid.NewGuid().ToString("D")))));
        Assert.Throws<S2SContractException>(() => _validator.Validate(ValidClaims().Append(new S2SClaim("email", "x@example.test"))));
        Assert.Throws<S2SContractException>(() => _validator.Validate(Replace("sub", "not-a-guid")));
        Assert.Throws<S2SContractException>(() => _validator.Validate(Replace("exp", "100")));
    }

    private static IEnumerable<S2SClaim> Replace(string type, string value) =>
        ValidClaims().Select(x => x.Type == type ? new S2SClaim(type, value) : x);

    private static S2SClaim[] ValidClaims()
    {
        const long iat = 2_000_000_000;
        return
        [
            new("typ", DelegatedActorProofV1.ExactType), new("iss", DelegatedActorProofV1.ExactIssuer),
            new("aud", "diten-fpa-service"), new("sub", Guid.NewGuid().ToString("D")),
            new("client_id", "diten-fpa-producer"), new("azp", "diten-fpa-producer"), new("actor_type", "service"),
            new("tenant_id", Guid.NewGuid().ToString("D")), new("delegated_actor_id", Guid.NewGuid().ToString("D")),
            new("delegated_actor_type", "tenant_user"), new("delegation_id", Guid.NewGuid().ToString("D")),
            new("operation_id", "budget.read"), new("permission", "fpa.budget.read"),
            new("scope", DelegatedActorProofV1.ExactScope),
            new("request_hash", "AAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAA"),
            new("nonce", Guid.NewGuid().ToString("D")), new("jti", Guid.NewGuid().ToString("D")),
            new("iat", iat.ToString()), new("nbf", iat.ToString()), new("exp", (iat + 300).ToString()),
            new("tenant_grant_version", "1"), new("service_principal_version", "1"), new("credential_generation", "1")
        ];
    }
}
