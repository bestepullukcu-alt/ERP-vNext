// WP-INFRA-AUTH-ACCEPTANCE-HOST-01 — F9 (Aşama E): `seed-ready`'s shape, asserted against a REAL run's real wire
// payload — not a hand-crafted JSON string. Checks exactly what the prompt asked for, no more: tenantId and
// foreignTenantId present and distinct; actors is EXACTLY {kindAdmin, pmo, creator, noPermission}, each carrying
// userId+email+password; subjects is EXACTLY the ten named keys, each carrying ONLY userId (never password); and
// no identity anywhere in the message is the DefaultTenant GUID. The password VALUE is never asserted or logged
// — only whether the "password" field itself is present or absent on a given object.

using System.Net.Sockets;
using System.Text;
using System.Text.Json;
using Xunit;
using Xunit.Abstractions;

namespace Diten.AuthService.AccountKindAcceptanceHost.Tests;

[Collection("SevenStateSupervisor")]
public sealed class SeedReadyShapeTests
{
    private const string DefaultTenantId = "00000000-0000-0000-0000-000000000001";
    private static readonly string[] ExpectedActorKeys = { "kindAdmin", "pmo", "creator", "noPermission" };
    private static readonly string[] ExpectedSubjectKeys =
    {
        "human", "unknown", "service", "passive", "foreign", "mutable", "unnamed", "whitespace", "emailUserName", "longName"
    };

    private readonly ITestOutputHelper _output;

    public SeedReadyShapeTests(ITestOutputHelper output)
    {
        _output = output;
    }

    [Fact]
    public async Task SeedReady_HasExactActorAndSubjectShape_NoSubjectPasswords_NoDefaultTenantIdentities()
    {
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

            var seedReady = await h.ReceiveRaw(reader, TimeSpan.FromSeconds(60));
            Assert.NotNull(seedReady);
            Assert.Equal("seed-ready", seedReady!.Value.GetProperty("type").GetString());
            var msg = seedReady.Value;

            // ── tenantId / foreignTenantId present and distinct ─────────────────────────────────────────
            Assert.True(msg.TryGetProperty("tenantId", out var tenantIdProp), "seed-ready is missing tenantId");
            Assert.True(msg.TryGetProperty("foreignTenantId", out var foreignTenantIdProp), "seed-ready is missing foreignTenantId");
            var tenantId = tenantIdProp.GetGuid();
            var foreignTenantId = foreignTenantIdProp.GetGuid();
            Assert.NotEqual(tenantId, foreignTenantId);

            // ── actors: exactly the 4 expected keys, each with userId+email+password ───────────────────
            Assert.True(msg.TryGetProperty("actors", out var actors), "seed-ready is missing actors");
            var actorKeys = actors.EnumerateObject().Select(p => p.Name).ToHashSet();
            Assert.Equal(ExpectedActorKeys.ToHashSet(), actorKeys);
            var actorUserIds = new List<Guid>();
            foreach (var key in ExpectedActorKeys)
            {
                var actor = actors.GetProperty(key);
                Assert.True(actor.TryGetProperty("userId", out var userIdProp), $"actor '{key}' is missing userId");
                Assert.True(actor.TryGetProperty("email", out _), $"actor '{key}' is missing email");
                Assert.True(actor.TryGetProperty("password", out var pw), $"actor '{key}' is missing password");
                Assert.False(string.IsNullOrEmpty(pw.GetString()), $"actor '{key}' has an empty password");
                actorUserIds.Add(userIdProp.GetGuid());
            }

            // ── subjects: exactly the 10 expected keys, each with ONLY userId (never password) ─────────
            Assert.True(msg.TryGetProperty("subjects", out var subjects), "seed-ready is missing subjects");
            var subjectKeys = subjects.EnumerateObject().Select(p => p.Name).ToHashSet();
            Assert.Equal(ExpectedSubjectKeys.ToHashSet(), subjectKeys);
            var subjectUserIds = new List<Guid>();
            foreach (var key in ExpectedSubjectKeys)
            {
                var subject = subjects.GetProperty(key);
                Assert.True(subject.TryGetProperty("userId", out var userIdProp), $"subject '{key}' is missing userId");
                Assert.False(subject.TryGetProperty("password", out _), $"subject '{key}' has a password field — subjects must never carry one");
                subjectUserIds.Add(userIdProp.GetGuid());
            }

            // ── no identity anywhere in this message is the DefaultTenant GUID ─────────────────────────
            var defaultTenantGuid = Guid.Parse(DefaultTenantId);
            Assert.NotEqual(defaultTenantGuid, tenantId);
            Assert.NotEqual(defaultTenantGuid, foreignTenantId);
            foreach (var id in actorUserIds.Concat(subjectUserIds))
            {
                Assert.NotEqual(defaultTenantGuid, id);
            }

            await h.SendAsync(stream, runId, "shutdown");
            await h.ReceiveRaw(reader, TimeSpan.FromSeconds(15)); // bye
        }
        finally
        {
            await Task.Delay(1000);
            h.CleanRoot(root, dev, ino);
        }
    }
}
