using System.Diagnostics;
using Diten.AuthService.Api.Operational;
using Diten.AuthService.Application.Common.Entitlements;
using Diten.AuthService.Application.Tests.ServiceIdentityTokens;

namespace Diten.AuthService.Application.Tests.Operational;

/// <summary>
/// WP-AUTH-BL570-01 FIX1 R3 — the reconciliation command's quiescence observation on Unix: the same proof as the Windows
/// one (no other process with Diten.AuthService in its command line, nothing listening on 5056), fail-closed when the
/// listing cannot be trusted. The decision is tested on synthetic listings; the real observation (the runner's own
/// entry point) is run against a live child process that carries the marker.
/// </summary>
public sealed class UnixProcessObservationTests
{
    private const int Self = 4242;
    private const int Observer = 4343;

    private static (int, string)[] Listing(params (int, string)[] extra) =>
        [(1, "/sbin/launchd"), (Self, "/usr/local/share/dotnet/dotnet Diten.AuthService.Api.dll --run-product-identity-entitlement-reconciliation"),
            (Observer, "/bin/ps -A -ww -o pid=,uid=,args="), .. extra];

    [Fact]
    public void A_listing_without_another_auth_process_or_listener_is_quiescent()
    {
        Assert.Equal(EntitlementReconciliationPlan.Hash(UnixProcessObservation.Quiescent),
            UnixProcessObservation.Classify(Listing((77, "/usr/bin/sleep 5")), [443, 5001], Self, Observer));
    }

    [Theory]
    [InlineData("/usr/local/share/dotnet/dotnet /srv/auth/Diten.AuthService.Api.dll")]
    [InlineData("/srv/auth/Diten.AuthService.Api --urls http://0.0.0.0:5056")]
    public void Another_auth_process_is_not_quiescent(string commandLine)
    {
        Assert.Equal(UnixProcessObservation.NotQuiescent, Assert.Throws<InvalidOperationException>(
            () => UnixProcessObservation.Classify(Listing((77, commandLine)), [], Self, Observer)).Message);
    }

    [Fact]
    public void A_listener_on_the_auth_port_is_not_quiescent()
    {
        Assert.Equal(UnixProcessObservation.NotQuiescent, Assert.Throws<InvalidOperationException>(
            () => UnixProcessObservation.Classify(Listing(), [UnixProcessObservation.AuthPort], Self, Observer)).Message);
    }

    [Theory]
    [InlineData(1)]
    [InlineData(Self)]
    public void A_listing_that_misses_pid_one_or_this_process_is_not_trusted(int missing)
    {
        var rows = Listing().Where(row => row.Item1 != missing).ToArray();
        Assert.Equal(UnixProcessObservation.ObservationFailed, Assert.Throws<InvalidOperationException>(
            () => UnixProcessObservation.Classify(rows, [], Self, Observer)).Message);
    }

    // The real observation, through the runner's own default observer: on Unix it no longer refuses with
    // WINDOWS_PROCESS_OBSERVATION_REQUIRED — it lists every process, sees a live child that carries the marker, and says
    // so. (In a test run other Diten.AuthService processes exist too — the test runner — so "quiescent" is not expected.)
    [UnixFact]
    public async Task The_real_unix_observation_sees_a_running_auth_process()
    {
        using var child = Process.Start(new ProcessStartInfo("/bin/sh")
        {
            ArgumentList = { "-c", "sleep 3", "Diten.AuthService.ObservationProbe" },
            UseShellExecute = false
        })!;
        try
        {
            var refused = await Assert.ThrowsAsync<InvalidOperationException>(
                () => EntitlementReconciliationOperationalRunner.ObserveProcessesAsync(CancellationToken.None));
            Assert.Equal(UnixProcessObservation.NotQuiescent, refused.Message);
        }
        finally
        {
            await child.WaitForExitAsync(); // ends by itself; never killed
        }
    }
}
