using Diten.AuthService.Domain.Entities;
using Diten.AuthService.Infrastructure.Services;
using Xunit;

namespace Diten.AuthService.Application.Tests.ServiceIdentityTokens;

public sealed class ServiceClientCredentialRotationTests
{
    [Fact]
    public void Active_and_previous_credentials_obey_strict_overlap_and_revocation()
    {
        var verifier = new ServiceClientCredentialVerifier();
        var boundary = DateTimeOffset.UtcNow.AddMinutes(1);
        var identity = new ServiceClientIdentity
        {
            ClientCode = "mdm",
            ServiceName = "Diten.MDM",
            ActiveCredentialHash = verifier.Hash("active"),
            ActiveCredentialVersion = "v2",
            PreviousCredentialHash = verifier.Hash("previous"),
            PreviousCredentialVersion = "v1",
            PreviousValidUntilUtc = boundary
        };

        Assert.True(verifier.Verify(identity, "active", boundary));
        Assert.True(verifier.Verify(identity, "previous", boundary.AddTicks(-1)));
        Assert.False(verifier.Verify(identity, "previous", boundary));
        Assert.False(verifier.Verify(identity, "wrong", boundary.AddTicks(-1)));

        identity.IsRevoked = true;
        Assert.False(verifier.Verify(identity, "active", boundary));
        Assert.False(verifier.Verify(identity, "previous", boundary.AddTicks(-1)));
    }

    [Fact]
    public void Hash_is_versioned_salted_pbkdf2_and_malformed_hash_fails_closed()
    {
        var verifier = new ServiceClientCredentialVerifier();
        var first = verifier.Hash("same");
        var second = verifier.Hash("same");
        Assert.StartsWith("pbkdf2-sha256$100000$", first, StringComparison.Ordinal);
        Assert.NotEqual(first, second);

        var identity = new ServiceClientIdentity { ActiveCredentialHash = "not-a-supported-hash" };
        Assert.False(verifier.Verify(identity, "same", DateTimeOffset.UtcNow));
        verifier.PerformUnknownClientWork("same");
    }

    [Fact]
    public void Dummy_literal_cannot_authenticate_malformed_active_or_missing_previous_hash()
    {
        var verifier = new ServiceClientCredentialVerifier();
        var identity = new ServiceClientIdentity
        {
            ActiveCredentialHash = "malformed",
            ActiveCredentialVersion = "v1",
            PreviousValidUntilUtc = DateTimeOffset.UtcNow.AddMinutes(5),
            PreviousCredentialVersion = "v0"
        };

        Assert.False(verifier.Verify(identity, "service-identity-dummy", DateTimeOffset.UtcNow));

        identity.ActiveCredentialHash = verifier.Hash("active");
        identity.PreviousCredentialHash = verifier.Hash("previous");
        identity.PreviousCredentialVersion = string.Empty;
        Assert.False(verifier.Verify(identity, "previous", DateTimeOffset.UtcNow));
    }

    [Fact]
    public void Credential_state_requires_complete_active_and_all_or_none_previous_tuple()
    {
        var verifier = new ServiceClientCredentialVerifier();
        var identity = new ServiceClientIdentity
        {
            ActiveCredentialHash = verifier.Hash("active"),
            ActiveCredentialVersion = "v2"
        };
        Assert.True(verifier.IsStateCoherent(identity));

        identity.PreviousCredentialHash = verifier.Hash("previous");
        Assert.False(verifier.IsStateCoherent(identity));
        identity.PreviousCredentialVersion = "v1";
        Assert.False(verifier.IsStateCoherent(identity));
        identity.PreviousValidUntilUtc = DateTimeOffset.UtcNow;
        Assert.True(verifier.IsStateCoherent(identity));

        identity.ActiveCredentialHash = "malformed";
        Assert.False(verifier.IsStateCoherent(identity));
    }
}
