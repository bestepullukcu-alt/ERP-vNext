// WP-INFRA-AUTH-ACCEPTANCE-HOST-01 — T4 (Aşama D): black-box network evidence. Samples the REAL API child
// process's TCP connections (via `lsof -p <apiPid>`, an external, unprivileged, black-box observer — nothing in
// the assertions below is derived from Program.cs's own knowledge of what it connected to) and proves the ONLY
// two TCP targets that ever appear are the disposable mongod's own port and the in-process login-settings stub's
// own port — both discovered independently, from lsof against mongodPid and hostPid respectively, never assumed.
// The named forbidden ports (27017 real Mongo, 5672 RabbitMQ, 1025 Mailpit, 4317/4318 OTEL, 5057 real Platform)
// are checked explicitly as defense in depth, but the PRIMARY assertion is a strict allow-list (only these two
// discovered ports may appear at all) — strictly stronger than a finite forbidden-list, since it also catches any
// OTHER unexpected target, not just the six named here.

using System.Diagnostics;
using System.Net.Sockets;
using System.Text;
using System.Text.Json;
using System.Text.RegularExpressions;
using Xunit;
using Xunit.Abstractions;

namespace Diten.AuthService.AccountKindAcceptanceHost.Tests;

[Collection("SevenStateSupervisor")]
public sealed class NetworkIsolationTests
{
    private static readonly int[] ForbiddenPorts = { 27017, 5672, 1025, 4317, 4318, 5057 };

    private readonly ITestOutputHelper _output;

    public NetworkIsolationTests(ITestOutputHelper output)
    {
        _output = output;
    }

    [Fact]
    public async Task ApiChildConnections_OnlyMongodAndLoginSettingsStub_NeverAForbiddenTarget()
    {
        var lsofCheck = PoisonedEnvironmentTests.RunLsof(Environment.ProcessId);
        Assert.True(lsofCheck.ToolAvailable, "lsof is required for T4 evidence and is not available — RED, not skipped");

        var h = new SupervisorTestHarness(_output);
        var (root, dev, ino) = h.CreateRunRoot();
        h.PrepareFixedSubdirs(root);
        using var listener = h.Listen(root, out var runId);
        var hostPid = h.SpawnHost(root, runId);

        try
        {
            var acceptTask = listener.AcceptAsync();
            Assert.Same(acceptTask, await Task.WhenAny(acceptTask, Task.Delay(TimeSpan.FromSeconds(10))));
            using var sock = await acceptTask;
            using var stream = new NetworkStream(sock, ownsSocket: false);
            using var reader = new StreamReader(stream, Encoding.UTF8, leaveOpen: true);
            await reader.ReadLineAsync(); // hello
            await h.SendAsync(stream, runId, "hello-ack", includeProtocolVersion: true);

            var ready = await h.ReceiveRaw(reader, TimeSpan.FromSeconds(60));
            Assert.NotNull(ready);
            Assert.Equal("ready", ready!.Value.GetProperty("type").GetString());
            var apiPid = ready.Value.GetProperty("apiPid").GetInt32();
            var mongodPid = ready.Value.GetProperty("mongodPid").GetInt32();

            var seedReady = await h.ReceiveRaw(reader, TimeSpan.FromSeconds(60));
            Assert.NotNull(seedReady);

            // Discover the two legitimate TCP targets INDEPENDENTLY — never from Program.cs's own config values.
            var mongodListen = LsofTcpEntries(mongodPid).Where(e => e.State == "LISTEN").ToList();
            Assert.True(mongodListen.Count >= 1, "could not discover mongod's own TCP listen port via lsof");
            var mongodPorts = mongodListen.Select(e => e.LocalPort).Distinct().ToList();

            var hostListen = LsofTcpEntries(hostPid).Where(e => e.State == "LISTEN").ToList();
            Assert.True(hostListen.Count == 1, $"expected exactly one TCP LISTEN socket on the host process (the login-settings stub) — found {hostListen.Count}: " + string.Join(", ", hostListen.Select(e => e.LocalPort)));
            var stubPort = hostListen[0].LocalPort;

            _output.WriteLine($"discovered allowed targets: mongod={string.Join(",", mongodPorts)} stub={stubPort}");

            // Sample the API child's connections twice — once right at ready (P7's pre-ready login already
            // happened, so its Mongo/stub connections should already be visible), and again just before shutdown,
            // to catch anything opened late.
            var sample1 = LsofTcpEntries(apiPid);
            await Task.Delay(500);
            var sample2 = LsofTcpEntries(apiPid);
            var allObserved = sample1.Concat(sample2).ToList();
            _output.WriteLine($"api ({apiPid}) TCP entries observed ({allObserved.Count} total across 2 samples):");
            foreach (var e in allObserved.DistinctBy(e => (e.LocalPort, e.RemotePort, e.State)))
            {
                _output.WriteLine($"  local={e.LocalPort} remote={e.RemotePort?.ToString() ?? "-"} state={e.State}");
            }

            var allowedPorts = mongodPorts.Append(stubPort).ToHashSet();
            var violations = allObserved
                .Where(e => e.RemotePort.HasValue)
                .Where(e => !allowedPorts.Contains(e.RemotePort!.Value))
                .ToList();
            Assert.True(violations.Count == 0,
                "api process connected to an UNAUTHORIZED target (not mongod, not the login-settings stub): " +
                string.Join(", ", violations.Select(e => $"remote={e.RemotePort}")));

            foreach (var forbidden in ForbiddenPorts)
            {
                Assert.DoesNotContain(allObserved, e => e.LocalPort == forbidden || e.RemotePort == forbidden);
            }

            await h.SendAsync(stream, runId, "shutdown");
            var bye = await h.ReceiveRaw(reader, TimeSpan.FromSeconds(15));
            Assert.NotNull(bye);
        }
        finally
        {
            h.CleanRoot(root, dev, ino);
        }
    }

    internal sealed record TcpEntry(int LocalPort, int? RemotePort, string State);

    internal static List<TcpEntry> LsofTcpEntries(int pid)
    {
        var lsof = PoisonedEnvironmentTests.RunLsof2(pid, "-iTCP -a -P -n");
        var result = new List<TcpEntry>();
        // lsof's own columns are whitespace-separated; the NAME field (second-to-last token) is either
        // "host:lport" (LISTEN) or "host:lport->host:rport" (ESTABLISHED etc.), and the LAST token is always
        // "(STATE)". Splitting on whitespace and taking the last two tokens is robust regardless of column
        // widths or which of COMMAND/PID/USER/FD/TYPE/DEVICE/SIZE contain variable-width values.
        foreach (var line in lsof.Lines)
        {
            var tokens = line.Split(' ', StringSplitOptions.RemoveEmptyEntries);
            if (tokens.Length < 2) continue;
            var stateToken = tokens[^1];
            if (stateToken.Length < 3 || stateToken[0] != '(' || stateToken[^1] != ')') continue;
            var state = stateToken[1..^1];
            var nameToken = tokens[^2];

            string? localPart, remotePart;
            var arrow = nameToken.IndexOf("->", StringComparison.Ordinal);
            if (arrow >= 0) { localPart = nameToken[..arrow]; remotePart = nameToken[(arrow + 2)..]; }
            else { localPart = nameToken; remotePart = null; }

            if (!TryLastPort(localPart, out var lport)) continue;
            int? rport = null;
            if (remotePart is not null && TryLastPort(remotePart, out var rp)) rport = rp;

            result.Add(new TcpEntry(lport, rport, state));
        }
        return result;

        static bool TryLastPort(string hostPort, out int port)
        {
            var idx = hostPort.LastIndexOf(':');
            port = 0;
            return idx >= 0 && idx < hostPort.Length - 1 && int.TryParse(hostPort[(idx + 1)..], out port);
        }
    }
}
