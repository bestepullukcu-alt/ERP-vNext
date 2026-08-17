using System.Text.RegularExpressions;
using Diten.Platform.Infrastructure.Eventing;
using Xunit;

namespace Diten.Platform.Application.Tests.Authorization;

public sealed class PlatformInstanceIdentityTests
{
    [Fact]
    public void InstanceId_is_non_empty()
    {
        Assert.False(string.IsNullOrWhiteSpace(PlatformInstanceIdentity.InstanceId));
    }

    [Fact]
    public void InstanceId_is_stable_within_the_process()
    {
        // Process-lifetime identity: every read returns the same value (the fan-out endpoint name must not change
        // between bus configuration and reconnects within a single process).
        Assert.Equal(PlatformInstanceIdentity.InstanceId, PlatformInstanceIdentity.InstanceId);
    }

    [Fact]
    public void InstanceId_is_queue_safe_32_lowercase_hex()
    {
        // Queue/endpoint-name safe: 32 lowercase hex characters (Guid "N" format), no separators or special chars.
        Assert.Matches(new Regex("^[0-9a-f]{32}$"), PlatformInstanceIdentity.InstanceId);
    }
}
