using System.Diagnostics;
using System.Net;
using System.Net.Http.Headers;
using System.Net.Http.Json;
using System.Net.Sockets;
using System.Runtime.InteropServices;
using System.Text;
using System.Text.Json;

namespace Diten.PpmService.IntegrationTests.Portfolios;

// macOS-only consumer of Auth Acceptance Host Control Protocol v1.2 revision 2.
// No Auth assembly reference, runtime transport, developer configuration, or inherited credentials.
internal sealed class PortfolioAuthProviderProcessHost : IAsyncDisposable
{
    private const string Dotnet = "/usr/local/share/dotnet/dotnet";
    private const int MaxFrame = 65536;
    private readonly string _runId = Guid.NewGuid().ToString("N");
    private readonly Dictionary<int, ProcessIdentity> _owned = [];
    private readonly List<string> _secrets = [];
    private readonly SemaphoreSlim _cleanup = new(1);
    private readonly CancellationTokenSource _monitorStop = new();
    private Socket? _listener;
    private NetworkStream? _control;
    private ProcessIdentity? _host;
    private ProcessIdentity? _api;
    private FileIdentity? _rootIdentity;
    private FileIdentity? _apiSocketIdentity;
    private Task? _monitor;
    private Task<byte[]>? _stdout;
    private Task<byte[]>? _stderr;
    private bool _ready;
    private bool _disposed;
    private bool _reaped;
    private int? _exitCode;
    public string Root { get; private set; } = "";
    public Guid TenantId { get; private set; }
    public Guid ForeignTenantId { get; private set; }
    public Dictionary<string, Guid> Subjects { get; } = new(StringComparer.Ordinal);
    private Dictionary<string, Actor> Actors { get; } = new(StringComparer.Ordinal);
    public Guid ActorId(string name) => Actors[name].Id;
    public bool CleanupVerified { get; private set; }
    public bool OutputContainsNoSecrets { get; private set; }
    internal int? ExitCode => _exitCode;
    internal int HostPid => _host?.Pid ?? 0;

    public async Task StartAsync(CancellationToken ct = default, string? failureMode = null)
    {
        if (!OperatingSystem.IsMacOS()) throw new PlatformNotSupportedException("C3 acceptance supports macOS only.");
        if (_host is not null || _disposed) throw new InvalidOperationException("C3 host cannot be restarted.");
        try
        {
            var template = Encoding.UTF8.GetBytes("/private/tmp/ppm-c3-XXXXXX\0");
            if (mkdtemp(template) == IntPtr.Zero) throw Failure("root-create");
            Root = Encoding.UTF8.GetString(template).TrimEnd('\0');
            _rootIdentity = CheckPath(Root, 0x4000, privateDirectory: true);
            foreach (var name in new[] { "mongo", "home", "tmp", "content" })
            {
                Directory.CreateDirectory(Path.Combine(Root, name), UnixFileMode.UserRead | UnixFileMode.UserWrite | UnixFileMode.UserExecute);
                CheckPath(Path.Combine(Root, name), 0x4000, privateDirectory: true);
            }
            _listener = new Socket(AddressFamily.Unix, SocketType.Stream, ProtocolType.Unspecified);
            _listener.Bind(new UnixDomainSocketEndPoint(Path.Combine(Root, "control.sock")));
            if (chmod(Path.Combine(Root, "control.sock"), 0x180) != 0) throw Failure("socket-mode");
            var controlIdentity = CheckPath(Path.Combine(Root, "control.sock"), 0xc000);
            _listener.Listen(1);
            var env = CreateEnvironment(Root, _runId, Dotnet);
            if (failureMode == "api-start") env["DITEN_ACCEPTANCE_DOTNET_PATH"] = "/nonexistent/ppm-c3-dotnet";
            else if (failureMode == "seed") env["DITEN_ACCEPTANCE_TESTONLY_CORRUPT_PERMISSION_KEY"] = "auth.users.lookup";
            else if (failureMode is not (null or "cancel-after-hello" or "timeout-after-hello")) throw Failure("test-mode");
            var pid = Spawn(ResolveHostDll(), env);
            _host = ReadProcess(pid) ?? throw Failure("host-identity");
            if (_host.Parent != Environment.ProcessId || _host.Group != pid || _host.Uid != getuid()) throw Failure("host-ownership");
            lock (_owned) _owned.Add(pid, _host);
            _monitor = MonitorAsync();
            using var acceptDeadline = CancellationTokenSource.CreateLinkedTokenSource(ct);
            acceptDeadline.CancelAfter(TimeSpan.FromSeconds(10));
            var peer = await _listener.AcceptAsync(acceptDeadline.Token);
            try
            {
                if (CheckPath(Path.Combine(Root, "control.sock"), 0xc000) != controlIdentity) throw Failure("control-replaced");
                VerifyPeer(peer, _host);
                _control = new NetworkStream(peer, ownsSocket: true);
            }
            catch { peer.Dispose(); throw; }
            using var hello = await ReceiveAsync("hello", TimeSpan.FromSeconds(10), ct);
            if (hello.RootElement.GetProperty("hostPid").GetInt32() != pid) throw Failure("hello-peer");
            if (failureMode == "cancel-after-hello") throw new OperationCanceledException("C3 test cancellation.");
            if (failureMode == "timeout-after-hello")
            {
                using var ignored = await ReceiveAsync("ready", TimeSpan.FromMilliseconds(100), ct);
                throw Failure("missing-timeout");
            }
            await SendAsync("hello-ack", ct);
            using var ready = await ReceiveAsync("ready", TimeSpan.FromSeconds(60), ct);
            var r = ready.RootElement;
            _api = VerifyChild(r, "apiPid", "apiStartTime");
            _ = VerifyChild(r, "mongodPid", "mongodStartTime");
            if (!Path.GetFileName(ProcessPath(_api.Pid)).Equals("dotnet", StringComparison.Ordinal)) throw Failure("api-executable");
            _apiSocketIdentity = CheckPath(Path.Combine(Root, "api.sock"), 0xc000);
            using var seed = await ReceiveAsync("seed-ready", TimeSpan.FromSeconds(10), ct);
            ReadSeed(seed.RootElement);
            _ready = true;
        }
        catch
        {
            await DisposeAsync();
            throw;
        }
    }

    // Build this block from literals, before posix_spawn: .NET startup hooks and the developer's HOME never enter it.
    internal static Dictionary<string, string> CreateEnvironment(string root, string runId, string dotnet) => new(StringComparer.Ordinal)
    {
        ["DITEN_ACCEPTANCE_ROOT"] = root, ["DITEN_ACCEPTANCE_RUN_ID"] = runId,
        ["DITEN_ACCEPTANCE_DOTNET_PATH"] = dotnet, ["PATH"] = "/opt/homebrew/bin:/usr/bin:/bin",
        ["HOME"] = Path.Combine(root, "home"), ["TMPDIR"] = Path.Combine(root, "tmp")
    };

    private static string ResolveHostDll()
    {
        var repo = new DirectoryInfo(AppContext.BaseDirectory);
        while (repo is not null && !File.Exists(Path.Combine(repo.FullName, "AGENTS.md"))) repo = repo.Parent;
        var configuration = new DirectoryInfo(AppContext.BaseDirectory).Parent!.Name;
        var dll = Path.Combine(repo?.FullName ?? throw Failure("repo-path"), "services/Diten.AuthService/tests/Diten.AuthService.AccountKindAcceptanceHost/bin", configuration, "net8.0/Diten.AuthService.AccountKindAcceptanceHost.dll");
        if (!File.Exists(dll)) throw Failure("host-build-missing");
        return dll;
    }

    public async Task<HttpClient> ClientAsync(string actor = "pmo", CancellationToken ct = default)
    {
        if (!_ready || _disposed) throw Failure("host-not-ready");
        var client = CreateClient();
        try
        {
            var a = Actors[actor];
            client.DefaultRequestHeaders.Add("X-Tenant-Id", TenantId.ToString("D"));
            using var response = await client.PostAsJsonAsync("api/tenant-auth/login", new { email = a.Email, password = a.Password, rememberMe = false }, ct);
            if (response.StatusCode != HttpStatusCode.OK) throw Failure("login-status");
            using var json = JsonDocument.Parse(await response.Content.ReadAsByteArrayAsync(ct));
            var envelope = json.RootElement;
            if (!envelope.TryGetProperty("isSuccessful", out var ok) || ok.ValueKind != JsonValueKind.True ||
                !envelope.TryGetProperty("data", out var data) || !data.TryGetProperty("accessToken", out var raw) ||
                raw.ValueKind != JsonValueKind.String || string.IsNullOrWhiteSpace(raw.GetString())) throw Failure("login-envelope");
            var token = raw.GetString()!;
            _secrets.Add(token);
            client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", token);
            return client;
        }
        catch { client.Dispose(); throw Failure("login-failed"); }
    }

    private HttpClient CreateClient()
    {
        var handler = new SocketsHttpHandler
        {
            AllowAutoRedirect = false, UseProxy = false, UseCookies = false,
            ConnectCallback = async (context, ct) =>
            {
                if (context.DnsEndPoint.Host != "localhost" || context.DnsEndPoint.Port != 80 || _disposed) throw Failure("http-target");
                VerifyRoot();
                if (CheckPath(Path.Combine(Root, "api.sock"), 0xc000) != _apiSocketIdentity) throw Failure("api-socket-replaced");
                var socket = new Socket(AddressFamily.Unix, SocketType.Stream, ProtocolType.Unspecified);
                try
                {
                    await socket.ConnectAsync(new UnixDomainSocketEndPoint(Path.Combine(Root, "api.sock")), ct);
                    VerifyPeer(socket, _api ?? throw Failure("api-identity"));
                    if (CheckPath(Path.Combine(Root, "api.sock"), 0xc000) != _apiSocketIdentity) throw Failure("api-socket-replaced");
                    return new NetworkStream(socket, ownsSocket: true);
                }
                catch { socket.Dispose(); throw; }
            }
        };
        return new HttpClient(new TargetGuard(handler)) { BaseAddress = new Uri("http://localhost/"), Timeout = TimeSpan.FromSeconds(10) };
    }

    private sealed class TargetGuard(HttpMessageHandler inner) : DelegatingHandler(inner)
    {
        protected override Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken ct)
        {
            if (request.RequestUri is not { Scheme: "http", Host: "localhost", Port: 80 } || !string.IsNullOrEmpty(request.RequestUri.UserInfo))
                throw Failure("http-target");
            return base.SendAsync(request, ct);
        }
    }

    private ProcessIdentity VerifyChild(JsonElement ready, string pidField, string startField)
    {
        var p = ReadProcess(ready.GetProperty(pidField).GetInt32()) ?? throw Failure("child-identity");
        if (p.Parent != _host!.Pid || p.Group != _host.Pid || p.Uid != getuid() || p.StartMs != ready.GetProperty(startField).GetInt64())
            throw Failure("child-binding");
        lock (_owned) _owned[p.Pid] = p;
        return p;
    }

    private void ReadSeed(JsonElement seed)
    {
        TenantId = seed.GetProperty("tenantId").GetGuid();
        ForeignTenantId = seed.GetProperty("foreignTenantId").GetGuid();
        if (TenantId == ForeignTenantId || TenantId == Guid.Parse("00000000-0000-0000-0000-000000000001")) throw Failure("tenant-scope");
        foreach (var actor in seed.GetProperty("actors").EnumerateObject())
        {
            var v = actor.Value;
            Actors.Add(actor.Name, new(v.GetProperty("userId").GetGuid(), v.GetProperty("email").GetString()!, v.GetProperty("password").GetString()!));
            _secrets.Add(v.GetProperty("password").GetString()!);
        }
        foreach (var subject in seed.GetProperty("subjects").EnumerateObject()) Subjects.Add(subject.Name, subject.Value.GetProperty("userId").GetGuid());
    }

    private Task SendAsync(string type, CancellationToken ct)
    {
        var data = new Dictionary<string, string> { ["type"] = type, ["runId"] = _runId };
        if (type == "hello-ack") data.Add("protocolVersion", "1.2");
        return _control!.WriteAsync(Encoding.UTF8.GetBytes(JsonSerializer.Serialize(data) + "\n"), ct).AsTask();
    }

    private async Task<JsonDocument> ReceiveAsync(string expected, TimeSpan timeout, CancellationToken ct)
    {
        using var deadline = CancellationTokenSource.CreateLinkedTokenSource(ct);
        deadline.CancelAfter(timeout);
        return await ReadFrameAsync(_control!, _runId, expected, deadline.Token);
    }

    internal static async Task<JsonDocument> ReadFrameAsync(Stream stream, string runId, string expected, CancellationToken ct)
    {
        using var line = CancellationTokenSource.CreateLinkedTokenSource(ct);
        var buffer = new byte[MaxFrame + 1]; var count = 0;
        while (true)
        {
            if (await stream.ReadAsync(buffer.AsMemory(count, 1), line.Token) == 0) throw Failure("control-eof");
            if (count == 0) line.CancelAfter(TimeSpan.FromSeconds(5));
            if (buffer[count] == 10) break;
            if (++count > MaxFrame) throw Failure("frame-size");
        }
        return ParseFrame(buffer.AsSpan(0, count).ToArray(), runId, expected);
    }

    internal static JsonDocument ParseFrame(byte[] bytes, string runId, string expected)
    {
        try
        {
            if (bytes.Length == 0 || bytes.Length > MaxFrame || bytes.Contains((byte)13) || bytes.AsSpan().StartsWith(new byte[] { 239, 187, 191 })) throw Failure("frame");
            _ = new UTF8Encoding(false, true).GetString(bytes);
            var reader = new Utf8JsonReader(bytes); var names = new Stack<HashSet<string>>();
            while (reader.Read())
            {
                if (reader.TokenType == JsonTokenType.StartObject) names.Push(new(StringComparer.Ordinal));
                if (reader.TokenType == JsonTokenType.EndObject) names.Pop();
                if (reader.TokenType == JsonTokenType.PropertyName && !names.Peek().Add(reader.GetString()!)) throw Failure("duplicate-key");
            }
            var doc = JsonDocument.Parse(bytes);
            try
            {
                var r = doc.RootElement;
                if (Text(r, "runId") != runId) throw Failure("run-binding");
                var type = Text(r, "type");
                if (type == "error")
                {
                    Shape(r, "type", "runId", "code", "message");
                    var errors = new Dictionary<string, string>
                    {
                        ["unsupported-version"] = "Protocol version not supported.", ["peer-verification-failed"] = "Peer verification failed.",
                        ["mongo-start-failed"] = "Disposable MongoDB could not be started.", ["api-start-failed"] = "Auth API process could not be started.",
                        ["api-bind-failed"] = "Auth API did not bind its socket.", ["seed-failed"] = "Seeding the disposable tenant failed.",
                        ["timeout"] = "A protocol step timed out.", ["protocol-violation"] = "Protocol violation."
                    };
                    if (!errors.TryGetValue(Text(r, "code"), out var message) || Text(r, "message") != message) throw Failure("error-schema");
                    throw Failure("host-error"); // Never disclose input or fixture secrets in an exception.
                }
                if (type != expected) throw Failure("message-order");
                switch (type)
                {
                    case "hello":
                        Shape(r, "type", "runId", "protocolVersion", "hostPid");
                        if (Text(r, "protocolVersion") != "1.2" || r.GetProperty("hostPid").GetInt32() <= 1) throw Failure("hello-schema");
                        break;
                    case "ready":
                        Shape(r, "type", "runId", "apiPid", "apiStartTime", "mongodPid", "mongodStartTime", "endpoint", "loginSettings", "authLoginProof");
                        foreach (var n in new[] { "apiPid", "mongodPid" }) if (r.GetProperty(n).GetInt32() <= 1) throw Failure("pid");
                        foreach (var n in new[] { "apiStartTime", "mongodStartTime" }) if (r.GetProperty(n).GetInt64() <= 0) throw Failure("start-time");
                        var e = r.GetProperty("endpoint"); Shape(e, "kind", "socket");
                        if (Text(e, "kind") != "unix" || Text(e, "socket") != "api.sock") throw Failure("endpoint");
                        var l = r.GetProperty("loginSettings"); Shape(l, "source", "boundaryEndpoint", "provesPlatformIntegration");
                        if (Text(l, "source") != "stub" || Text(l, "boundaryEndpoint") != "/api/internal/tenants/{tenantId}/login-settings" || l.GetProperty("provesPlatformIntegration").ValueKind != JsonValueKind.False || Text(r, "authLoginProof") != "real") throw Failure("trust-labels");
                        break;
                    case "seed-ready":
                        Shape(r, "type", "runId", "tenantId", "foreignTenantId", "actors", "subjects");
                        GuidValue(r, "tenantId"); GuidValue(r, "foreignTenantId");
                        var actors = r.GetProperty("actors"); Shape(actors, "kindAdmin", "pmo", "creator", "noPermission");
                        foreach (var a in actors.EnumerateObject()) { Shape(a.Value, "userId", "email", "password"); GuidValue(a.Value, "userId"); Text(a.Value, "email"); Text(a.Value, "password"); }
                        var subjects = r.GetProperty("subjects"); Shape(subjects, "human", "unknown", "service", "passive", "foreign", "mutable", "unnamed", "whitespace", "emailUserName", "longName");
                        foreach (var s in subjects.EnumerateObject()) { Shape(s.Value, "userId"); GuidValue(s.Value, "userId"); }
                        break;
                    case "bye": Shape(r, "type", "runId"); break;
                    default: throw Failure("message-type");
                }
                return doc;
            }
            catch { doc.Dispose(); throw; }
        }
        catch (Exception e) when (e is JsonException or DecoderFallbackException or InvalidOperationException or FormatException or KeyNotFoundException or OverflowException)
        { throw Failure("protocol-schema"); }
    }

    private static void Shape(JsonElement e, params string[] keys)
    {
        if (e.ValueKind != JsonValueKind.Object || !e.EnumerateObject().Select(p => p.Name).ToHashSet(StringComparer.Ordinal).SetEquals(keys)) throw Failure("schema");
    }
    private static string Text(JsonElement e, string key) => e.GetProperty(key).ValueKind == JsonValueKind.String && !string.IsNullOrEmpty(e.GetProperty(key).GetString()) ? e.GetProperty(key).GetString()! : throw Failure("string");
    private static void GuidValue(JsonElement e, string key) { if (!e.GetProperty(key).TryGetGuid(out var g) || g == Guid.Empty) throw Failure("guid"); }
    private sealed record Actor(Guid Id, string Email, string Password) { public override string ToString() => "C3 disposable actor (redacted)"; }
    private static IOException Failure(string code) => new("C3 " + code + ".");

    private async Task MonitorAsync()
    {
        try
        {
            while (!_monitorStop.IsCancellationRequested)
            {
                DiscoverChildren();
                await Task.Delay(25, _monitorStop.Token);
            }
        }
        catch (OperationCanceledException) when (_monitorStop.IsCancellationRequested) { }
    }

    private void DiscoverChildren()
    {
        if (_host is null) return;
        var pids = new int[4096];
        var bytes = proc_listpids(2, (uint)_host.Pid, pids, pids.Length * sizeof(int));
        if (bytes < 0 || bytes >= pids.Length * sizeof(int)) throw Failure("group-enumeration");
        foreach (var pid in pids.Take(bytes / sizeof(int)).Where(x => x > 1))
        {
            var p = ReadProcess(pid);
            if (p is null || p.Group != _host.Pid || p.Uid != getuid() || p.StartMs < _host.StartMs) continue;
            lock (_owned)
            {
                if (_owned.TryGetValue(pid, out var old) && old.StartMs != p.StartMs) throw Failure("pid-reused");
                // Only direct descendants or children of a previously proven descendant enter cleanup ownership.
                if (pid != _host.Pid && !_owned.ContainsKey(p.Parent)) continue;
                _owned[pid] = p;
            }
        }
    }

    internal void DisconnectControlForTest() { _control?.Dispose(); _control = null; _ready = false; }

    public async ValueTask DisposeAsync()
    {
        await _cleanup.WaitAsync();
        try
        {
            if (_disposed) return;
            _disposed = true;
            var orderly = false;
            var shutdownProtocolFailed = false;
            if (_ready && _control is not null)
            {
                try
                {
                    using var deadline = new CancellationTokenSource(TimeSpan.FromSeconds(15));
                    await SendAsync("shutdown", deadline.Token);
                    using var bye = await ReceiveAsync("bye", TimeSpan.FromSeconds(15), deadline.Token);
                    orderly = true;
                }
                catch (Exception e) when (e is IOException or OperationCanceledException or SocketException) { shutdownProtocolFailed = true; }
            }
            _control?.Dispose(); _listener?.Dispose();
            if (_host is not null)
            {
                // EOF is the first cleanup request on startup failure/cancellation. Keep monitoring while the
                // seed operation finishes so descendants created before the host observes EOF remain accounted for.
                await WaitHostAsync(TimeSpan.FromSeconds(15));
                if (!_reaped)
                {
                    DiscoverChildren();
                    SignalOwned(15);
                    await WaitHostAsync(TimeSpan.FromSeconds(6));
                }
                DiscoverChildren();
                SignalOwned(15);
                await WaitChildrenAsync(TimeSpan.FromSeconds(5));
                SignalOwned(9);
                await WaitHostAsync(TimeSpan.FromSeconds(5));
                await WaitChildrenAsync(TimeSpan.FromSeconds(5));
            }
            _monitorStop.Cancel();
            if (_monitor is not null) await _monitor;
            ProcessIdentity[] owned;
            lock (_owned) owned = _owned.Values.ToArray();
            if (owned.Any(IsSameLiveProcess) || (_host is not null && !_reaped)) throw Failure("process-residue");
            if (_host is not null)
            {
                var remaining = new int[4096];
                var count = proc_listpids(2, (uint)_host.Pid, remaining, remaining.Length * sizeof(int));
                if (count < 0 || count >= remaining.Length * sizeof(int) || remaining.Take(count / sizeof(int)).Where(pid => pid > 1).Any(pid => ReadProcess(pid) is { Status: not 5 }))
                    throw Failure("unowned-group-residue"); // Never signal an unproven PID or delete data beneath it.
            }
            if (orderly && _exitCode != 0) throw Failure("shutdown-exit");
            var stdout = _stdout is null ? [] : await _stdout.WaitAsync(TimeSpan.FromSeconds(3));
            var stderr = _stderr is null ? [] : await _stderr.WaitAsync(TimeSpan.FromSeconds(3));
            var output = Encoding.UTF8.GetString(stdout) + Encoding.UTF8.GetString(stderr);
            OutputContainsNoSecrets = stdout.Length == 0 && !_secrets.Any(s => output.Contains(s, StringComparison.Ordinal)) &&
                !output.Contains("eyJ", StringComparison.Ordinal) && !output.Contains("mongodb://", StringComparison.Ordinal) &&
                Encoding.UTF8.GetString(stderr).Split('\n', StringSplitOptions.RemoveEmptyEntries).All(s => s.StartsWith("[host] DIAG ", StringComparison.Ordinal));
            if (Root.Length > 0)
            {
                VerifyRoot();
                DeleteOwnedTree(Root); // Never a host-supplied path; reject symlink traversal at every level.
                if (Directory.Exists(Root)) throw Failure("directory-residue");
            }
            CleanupVerified = true;
            Actors.Clear(); _secrets.Clear();
            if (!OutputContainsNoSecrets) throw Failure("secret-output");
            if (shutdownProtocolFailed) throw Failure("shutdown-protocol");
        }
        finally { _cleanup.Release(); }
    }

    private async Task WaitHostAsync(TimeSpan timeout)
    {
        if (_host is null || _reaped) return;
        var timer = Stopwatch.StartNew();
        while (timer.Elapsed < timeout)
        {
            var pid = waitpid(_host.Pid, out var status, 1);
            if (pid == _host.Pid)
            {
                _reaped = true; _exitCode = (status & 127) == 0 ? (status >> 8) & 255 : -(status & 127); return;
            }
            if (pid < 0) throw Failure("waitpid");
            await Task.Delay(50);
        }
    }
    private async Task WaitChildrenAsync(TimeSpan timeout)
    {
        var timer = Stopwatch.StartNew();
        while (timer.Elapsed < timeout)
        {
            ProcessIdentity[] members; lock (_owned) members = _owned.Values.ToArray();
            if (!members.Any(IsSameLiveProcess)) return;
            await Task.Delay(50);
        }
    }
    private void SignalOwned(int signal)
    {
        ProcessIdentity[] members; lock (_owned) members = _owned.Values.ToArray();
        foreach (var p in members.OrderByDescending(x => x.Pid == _host?.Pid))
            if (p.Group != getpgid(0) && IsSameLiveProcess(p)) kill(p.Pid, signal);
    }
    private static bool IsSameLiveProcess(ProcessIdentity p)
    {
        var current = ReadProcess(p.Pid);
        return current is not null && current.Status != 5 && current.StartMs == p.StartMs && current.Uid == p.Uid && current.Group == p.Group;
    }
    private void VerifyRoot()
    {
        if (_rootIdentity is null || CheckPath(Root, 0x4000, privateDirectory: true) != _rootIdentity) throw Failure("root-replaced");
    }
    private static void DeleteOwnedTree(string path)
    {
        var identity = CheckPath(path, 0x4000);
        foreach (var entry in Directory.EnumerateFileSystemEntries(path))
        {
            var state = Stat(entry);
            if (state.Uid != getuid()) throw Failure("cleanup-owner");
            if (state.Kind == 0x4000) DeleteOwnedTree(entry);
            else File.Delete(entry); // unlink a symlink/socket/file, never recurse through one.
        }
        if (CheckPath(path, 0x4000) != identity) throw Failure("cleanup-replaced");
        Directory.Delete(path);
    }

    private int Spawn(string dll, Dictionary<string, string> env)
    {
        var attributes = IntPtr.Zero; var actions = IntPtr.Zero;
        int[] outPipe = [-1, -1], errPipe = [-1, -1];
        var args = new NativeStrings([Dotnet, dll]);
        var environment = new NativeStrings(env.Select(x => x.Key + "=" + x.Value).ToArray());
        try
        {
            if (pipe(outPipe) != 0 || pipe(errPipe) != 0 || posix_spawnattr_init(ref attributes) != 0 || posix_spawn_file_actions_init(ref actions) != 0) throw Failure("spawn-init");
            // New process group + close non-listed descriptors: control sockets never leak to descendants.
            if (posix_spawnattr_setflags(ref attributes, 0x4002) != 0 || posix_spawnattr_setpgroup(ref attributes, 0) != 0 ||
                posix_spawn_file_actions_addopen(ref actions, 0, "/dev/null", 0, 0) != 0 ||
                posix_spawn_file_actions_adddup2(ref actions, outPipe[1], 1) != 0 || posix_spawn_file_actions_adddup2(ref actions, errPipe[1], 2) != 0) throw Failure("spawn-actions");
            foreach (var fd in outPipe.Concat(errPipe)) if (posix_spawn_file_actions_addclose(ref actions, fd) != 0) throw Failure("spawn-close");
            if (posix_spawn(out var pid, Dotnet, ref actions, ref attributes, args.Pointer, environment.Pointer) != 0) throw Failure("spawn");
            close(outPipe[1]); outPipe[1] = -1; close(errPipe[1]); errPipe[1] = -1;
            _stdout = CaptureAsync(outPipe[0]); outPipe[0] = -1;
            _stderr = CaptureAsync(errPipe[0]); errPipe[0] = -1;
            return pid;
        }
        finally
        {
            foreach (var fd in outPipe.Concat(errPipe).Where(x => x >= 0)) close(fd);
            if (attributes != IntPtr.Zero) posix_spawnattr_destroy(ref attributes);
            if (actions != IntPtr.Zero) posix_spawn_file_actions_destroy(ref actions);
            args.Dispose(); environment.Dispose();
        }
    }
    private static async Task<byte[]> CaptureAsync(int fd)
    {
        using var handle = new Microsoft.Win32.SafeHandles.SafeFileHandle((IntPtr)fd, ownsHandle: true);
        using var stream = new FileStream(handle, FileAccess.Read, 4096, isAsync: false);
        using var capture = new MemoryStream(); var buffer = new byte[4096];
        int n;
        while ((n = await stream.ReadAsync(buffer)) != 0)
        {
            if (capture.Length + n > 1024 * 1024) throw Failure("output-limit");
            capture.Write(buffer, 0, n);
        }
        return capture.ToArray();
    }
    private sealed class NativeStrings : IDisposable
    {
        private readonly IntPtr[] _strings;
        public IntPtr Pointer { get; }
        public NativeStrings(string[] strings)
        {
            _strings = strings.Select(Marshal.StringToCoTaskMemUTF8).Append(IntPtr.Zero).ToArray();
            Pointer = Marshal.AllocHGlobal(_strings.Length * IntPtr.Size); Marshal.Copy(_strings, 0, Pointer, _strings.Length);
        }
        public void Dispose() { foreach (var p in _strings) if (p != IntPtr.Zero) Marshal.FreeCoTaskMem(p); Marshal.FreeHGlobal(Pointer); }
    }

    private sealed record FileIdentity(int Device, long Inode, uint Uid, int Kind, int Mode);
    private static FileIdentity Stat(string path)
    {
        var bytes = new byte[144];
        if (lstat(path, bytes) != 0) throw Failure("lstat");
        var mode = BitConverter.ToUInt16(bytes, 4);
        return new(BitConverter.ToInt32(bytes, 0), BitConverter.ToInt64(bytes, 8), BitConverter.ToUInt32(bytes, 16), mode & 0xf000, mode & 0x1ff);
    }
    private static FileIdentity CheckPath(string path, int kind, bool privateDirectory = false)
    {
        var f = Stat(path);
        if (f.Uid != getuid() || f.Kind != kind || (privateDirectory && f.Mode != 0x1c0)) throw Failure("path-ownership");
        return f;
    }
    // Darwin proc_bsdinfo (sys/proc_info.h): UID@20, PGID@100, start timeval@120/128.
    private sealed record ProcessIdentity(int Pid, int Parent, int Group, uint Uid, long StartMs, int Status);
    private static ProcessIdentity? ReadProcess(int pid)
    {
        var bytes = new byte[136];
        var n = proc_pidinfo(pid, 3, 0, bytes, bytes.Length);
        if (n == 0) return null;
        if (n != bytes.Length || BitConverter.ToInt32(bytes, 12) != pid) throw Failure("process-layout");
        return new(pid, BitConverter.ToInt32(bytes, 16), BitConverter.ToInt32(bytes, 100), BitConverter.ToUInt32(bytes, 20),
            checked(BitConverter.ToInt64(bytes, 120) * 1000 + BitConverter.ToInt64(bytes, 128) / 1000), BitConverter.ToInt32(bytes, 4));
    }
    private static string ProcessPath(int pid)
    {
        var bytes = new byte[4096];
        if (proc_pidpath(pid, bytes, (uint)bytes.Length) <= 0) throw Failure("process-path");
        return Encoding.UTF8.GetString(bytes).TrimEnd('\0');
    }
    internal static void VerifyWrongPeerForTest(Socket socket, string mismatch)
    {
        var own = ReadProcess(Environment.ProcessId) ?? throw Failure("test-process");
        var expected = mismatch switch
        {
            "pid" => own with { Pid = own.Pid + 1 },
            "uid" => own with { Uid = own.Uid + 1 },
            "start" => own with { StartMs = own.StartMs - 1 },
            _ => throw Failure("test-mismatch")
        };
        VerifyPeer(socket, expected);
    }

    private static void VerifyPeer(Socket socket, ProcessIdentity expected)
    {
        var fd = (int)socket.SafeHandle.DangerousGetHandle();
        var bytes = new byte[4]; uint length = 4;
        if (getsockopt(fd, 0, 2, bytes, ref length) != 0 || length != 4 || BitConverter.ToInt32(bytes) != expected.Pid) throw Failure("peer-pid");
        var creds = new byte[128]; length = (uint)creds.Length;
        if (getsockopt(fd, 0, 1, creds, ref length) != 0 || length < 8 || BitConverter.ToUInt32(creds) != 0 || BitConverter.ToUInt32(creds, 4) != getuid() || !IsSameLiveProcess(expected)) throw Failure("peer-identity");
    }

    [DllImport("libc")] private static extern IntPtr mkdtemp(byte[] template);
    [DllImport("libc")] private static extern int lstat(string path, byte[] buffer);
    [DllImport("libc")] private static extern int chmod(string path, int mode);
    [DllImport("libc")] private static extern uint getuid();
    [DllImport("libc")] private static extern int getpgid(int pid);
    [DllImport("libc")] private static extern int getsockopt(int fd, int level, int name, byte[] value, ref uint length);
    [DllImport("libproc")] private static extern int proc_pidinfo(int pid, int flavor, ulong arg, byte[] buffer, int size);
    [DllImport("libproc")] private static extern int proc_pidpath(int pid, byte[] buffer, uint size);
    [DllImport("libproc")] private static extern int proc_listpids(uint type, uint info, int[] buffer, int size);
    [DllImport("libc")] private static extern int kill(int pid, int signal);
    [DllImport("libc")] private static extern int waitpid(int pid, out int status, int options);
    [DllImport("libc")] private static extern int pipe(int[] fds);
    [DllImport("libc")] private static extern int close(int fd);
    [DllImport("libc")] private static extern int posix_spawnattr_init(ref IntPtr attributes);
    [DllImport("libc")] private static extern int posix_spawnattr_destroy(ref IntPtr attributes);
    [DllImport("libc")] private static extern int posix_spawnattr_setflags(ref IntPtr attributes, short flags);
    [DllImport("libc")] private static extern int posix_spawnattr_setpgroup(ref IntPtr attributes, int group);
    [DllImport("libc")] private static extern int posix_spawn_file_actions_init(ref IntPtr actions);
    [DllImport("libc")] private static extern int posix_spawn_file_actions_destroy(ref IntPtr actions);
    [DllImport("libc")] private static extern int posix_spawn_file_actions_addopen(ref IntPtr actions, int fd, string path, int flags, int mode);
    [DllImport("libc")] private static extern int posix_spawn_file_actions_adddup2(ref IntPtr actions, int fd, int target);
    [DllImport("libc")] private static extern int posix_spawn_file_actions_addclose(ref IntPtr actions, int fd);
    [DllImport("libc")] private static extern int posix_spawn(out int pid, string path, ref IntPtr actions, ref IntPtr attributes, IntPtr args, IntPtr env);
}
