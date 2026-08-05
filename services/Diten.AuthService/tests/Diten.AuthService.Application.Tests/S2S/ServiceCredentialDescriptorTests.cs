using Diten.AuthService.Domain.S2S;

namespace Diten.AuthService.Application.Tests.S2S;

public sealed class ServiceCredentialDescriptorTests
{
    [Theory]
    [InlineData("HS256", 3072)]
    [InlineData("rs256", 3072)]
    [InlineData("RS512", 4096)]
    [InlineData("RS256", 2048)]
    public void Only_exact_rs256_with_minimum_rsa_size_is_accepted(string algorithm, int bits)
    {
        Assert.Throws<S2SContractException>(() => Create(algorithm, bits));
    }

    [Theory]
    [InlineData("")]
    [InlineData(" kid")]
    [InlineData("kid ")]
    [InlineData("*")]
    public void Kid_is_exact_and_non_partial(string kid)
    {
        Assert.Throws<S2SContractException>(() => Create(kid: kid));
    }

    [Fact]
    public void Rotation_metadata_supports_active_previous_overlap_without_secret_material()
    {
        var descriptor = Create();
        descriptor.TransitionTo(ServiceCredentialStatus.Active, "operator", DateTimeOffset.UtcNow);
        descriptor.TransitionTo(ServiceCredentialStatus.Previous, "operator", DateTimeOffset.UtcNow);

        Assert.Equal(ServiceCredentialStatus.Previous, descriptor.Status);
        Assert.NotNull(descriptor.OverlapValidUntilUtc);
        var names = typeof(ServiceCredentialDescriptor).GetProperties().Select(x => x.Name).ToArray();
        Assert.DoesNotContain(names, x => x.Contains("Private", StringComparison.OrdinalIgnoreCase));
        Assert.DoesNotContain(names, x => x.Contains("Secret", StringComparison.OrdinalIgnoreCase));
        Assert.Throws<S2SContractException>(() => descriptor.TransitionTo(ServiceCredentialStatus.Active, "operator", DateTimeOffset.UtcNow));
    }

    private static ServiceCredentialDescriptor Create(string algorithm = "RS256", int bits = 3072, string kid = "gate-i-key-01")
    {
        var now = DateTimeOffset.UtcNow;
        return new ServiceCredentialDescriptor(Guid.NewGuid(), Guid.NewGuid(), kid, algorithm, bits,
            "vault-public-key-reference", "sha256-thumbprint", now.AddMinutes(-1), now.AddDays(1), 1,
            now.AddHours(1), "test");
    }
}
