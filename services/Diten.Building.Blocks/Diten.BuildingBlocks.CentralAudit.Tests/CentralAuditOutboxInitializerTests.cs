using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using Xunit;

namespace Diten.BuildingBlocks.CentralAudit.Tests;

/// <summary>
/// FU-BL501-OUTBOX-RACE — AddCentralAudit creates the outbox at startup, before requests, even with the background dispatcher
/// off. A failure stops the host only when every write enlists in a transaction (RequireEnlistment). The race itself, on a
/// real fresh database, is measured in the Inventory suite (CentralAuditOutboxStartupTests).
/// </summary>
public sealed class CentralAuditOutboxInitializerTests
{
    [Fact]
    public async Task The_startup_step_is_registered_with_the_dispatcher_off_and_ensures_the_outbox_once()
    {
        using var harness = new Harness();

        var initializer = Assert.Single(harness.Get<IEnumerable<IHostedService>>().OfType<CentralAuditOutboxInitializerHostedService>());
        await initializer.StartAsync(CancellationToken.None);

        Assert.Equal(1, harness.Store.EnsureCreatedCalls);
    }

    [Fact]
    public async Task With_require_enlistment_a_startup_failure_refuses_to_start()
    {
        using var harness = new Harness(requireEnlistment: true);
        harness.Store.FailEnsureCreated = true;

        var initializer = harness.Get<IEnumerable<IHostedService>>().OfType<CentralAuditOutboxInitializerHostedService>().Single();

        await Assert.ThrowsAsync<InvalidOperationException>(() => initializer.StartAsync(CancellationToken.None));
        Assert.Contains(harness.Logs.Entries, e => e.Level == LogLevel.Critical && e.Message.Contains("refusing to start", StringComparison.Ordinal));
    }

    [Fact]
    public async Task Without_require_enlistment_a_startup_failure_is_logged_and_the_host_starts()
    {
        using var harness = new Harness();
        harness.Store.FailEnsureCreated = true;

        var initializer = harness.Get<IEnumerable<IHostedService>>().OfType<CentralAuditOutboxInitializerHostedService>().Single();
        await initializer.StartAsync(CancellationToken.None);

        Assert.Contains(harness.Logs.Entries, e => e.Level == LogLevel.Error && e.Message.Contains("outbox collection could not be created", StringComparison.Ordinal));
    }
}
