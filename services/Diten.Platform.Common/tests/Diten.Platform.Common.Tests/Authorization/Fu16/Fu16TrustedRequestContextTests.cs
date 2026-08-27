using Diten.Platform.Common.Authorization.Fu16;
using Xunit;

namespace Diten.Platform.Common.Tests.Authorization.Fu16;

public sealed class Fu16TrustedRequestContextTests
{
    [Fact]
    public void Exact_context_is_valid_and_permissions_are_ordinal()
    {
        var context = Create();
        Assert.Same(context, context.Validate());
        Assert.Equal(["ppm.projects.read", "ppm.projects.update"], context.Permissions);
    }

    [Fact]
    public void Missing_identity_duplicate_permission_and_long_lifetime_fail_closed()
    {
        Assert.Throws<ArgumentException>(() => (Create() with { TenantId = Guid.Empty }).Validate());
        Assert.Throws<ArgumentException>(() => (Create() with { Permissions = ["x", "x"] }).Validate());
        Assert.Throws<ArgumentException>(() => (Create() with
        {
            ExpiresAtUtc = Create().IssuedAtUtc.AddMinutes(5).AddTicks(1)
        }).Validate());
    }

    internal static Fu16TrustedRequestContext Create()
    {
        var issued = DateTimeOffset.FromUnixTimeSeconds(1_800_000_000);
        return new(
            Guid.Parse("00112233-4455-6677-8899-aabbccddeeff"),
            Guid.Parse("10112233-4455-6677-8899-aabbccddeeff"),
            Guid.Parse("20112233-4455-6677-8899-aabbccddeeff"),
            Guid.Parse("30112233-4455-6677-8899-aabbccddeeff"),
            Guid.Parse("40112233-4455-6677-8899-aabbccddeeff"),
            Guid.Parse("50112233-4455-6677-8899-aabbccddeeff"),
            "diten.ppm", "https://auth.local", "diten-ppm-service",
            "diten-delegated-actor-proof+jwt", "diten.s2s.delegated.invoke",
            "ppm.projects.read", ["ppm.projects.read", "ppm.projects.update"],
            new string('a', 64), 4, 5, 6, issued, issued, issued.AddMinutes(5));
    }
}
