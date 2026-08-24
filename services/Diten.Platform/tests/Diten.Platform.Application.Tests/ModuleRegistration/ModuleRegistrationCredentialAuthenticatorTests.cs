using Diten.Platform.API.Configuration;
using Diten.Platform.API.Security;
using Microsoft.Extensions.Options;
using Xunit;

namespace Diten.Platform.Application.Tests.ModuleRegistration;

public sealed class ModuleRegistrationCredentialAuthenticatorTests
{
    private static readonly DateTimeOffset Now = new(2026, 8, 5, 12, 0, 0, TimeSpan.Zero);

    [Fact]
    public void Active_credential_maps_to_mdm_owner()
    {
        var (authenticator, identifier, active, _) = Build();

        var result = authenticator.Authenticate(identifier, active);

        Assert.True(result.IsAuthenticated);
        Assert.Equal("DITENMDMSERVICE", result.ProducerOwnerCode);
    }

    [Theory]
    [InlineData(null, null)]
    [InlineData("unknown", "unknown")]
    public void Missing_or_unknown_credential_is_rejected(string? identifier, string? secret)
    {
        var (authenticator, _, _, _) = Build();

        Assert.False(authenticator.Authenticate(identifier, secret).IsAuthenticated);
    }

    [Fact]
    public void Correct_identifier_with_wrong_secret_is_rejected()
    {
        var (authenticator, identifier, _, _) = Build();
        var wrongSecret = Convert.ToBase64String(Guid.NewGuid().ToByteArray());

        Assert.False(authenticator.Authenticate(identifier, wrongSecret).IsAuthenticated);
    }

    [Fact]
    public void Previous_credential_is_accepted_only_strictly_inside_overlap()
    {
        var (inside, identifier, _, previous) = Build(previousValidUntilUtc: Now.AddSeconds(1));
        var (atBoundary, _, _, _) = Build(previousValidUntilUtc: Now);

        Assert.True(inside.Authenticate(identifier, previous).IsAuthenticated);
        Assert.False(atBoundary.Authenticate(identifier, previous).IsAuthenticated);
    }

    [Fact]
    public void Revocation_rejects_active_and_previous_credentials()
    {
        var (authenticator, identifier, active, previous) = Build(isRevoked: true);

        Assert.False(authenticator.Authenticate(identifier, active).IsAuthenticated);
        Assert.False(authenticator.Authenticate(identifier, previous).IsAuthenticated);
    }

    private static (ModuleRegistrationCredentialAuthenticator Authenticator, string Identifier, string Active, string Previous) Build(
        DateTimeOffset? previousValidUntilUtc = null,
        bool isRevoked = false)
    {
        var identifier = $"test-{Guid.NewGuid():N}";
        var active = Convert.ToBase64String(Guid.NewGuid().ToByteArray());
        var previous = Convert.ToBase64String(Guid.NewGuid().ToByteArray());
        var options = Options.Create(new ModuleRegistrationCredentialOptions
        {
            Mdm = new ModuleRegistrationServiceCredentialOptions
            {
                Identifier = identifier,
                ActiveSecret = active,
                PreviousSecret = previous,
                PreviousValidUntilUtc = previousValidUntilUtc ?? Now.AddMinutes(5),
                IsRevoked = isRevoked
            }
        });
        return (new(options, new FixedTimeProvider(Now)), identifier, active, previous);
    }

    private sealed class FixedTimeProvider(DateTimeOffset utcNow) : TimeProvider
    {
        public override DateTimeOffset GetUtcNow() => utcNow;
    }
}
