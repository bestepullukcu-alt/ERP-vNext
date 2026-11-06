using System.Diagnostics;
using System.Globalization;
using System.Net;
using System.Net.Sockets;
using MongoDB.Bson;
using MongoDB.Driver;

namespace Diten.CrmService.Application.Tests.CentralAudit;

/// <summary>
/// CRM-AUD-KN-01 — a test-owned mongod (fresh port, temporary dbpath), the Platform audit-seed tests' pattern: the real
/// CRM host creates its indexes at registration (<c>AddPersistence → TryEnsureIndexes</c>) and cannot start without a
/// reachable server. Never the developer's 27017–27021. The executable is <c>DITEN_TEST_MONGOD</c>, else the newest
/// Windows install under <c>Program Files\MongoDB\Server</c>, else <c>mongod</c> on PATH; none → the test FAILS by name
/// (no silent skip).
/// </summary>
public sealed class DisposableCrmMongod : IDisposable
{
    private static readonly HashSet<int> ProtectedPorts = [27017, 27018, 27019, 27020, 27021];
    private readonly Process _process;
    private readonly string _root;
    private int _disposed;

    public DisposableCrmMongod()
    {
        Port = FreePort();
        _root = Path.Combine(Path.GetTempPath(), $"diten-crm-central-audit-{Guid.NewGuid():N}");
        var data = Path.Combine(_root, "data");
        Directory.CreateDirectory(data);

        var start = new ProcessStartInfo(ResolveExecutable())
        {
            UseShellExecute = false,
            CreateNoWindow = true,
            RedirectStandardOutput = true,
            RedirectStandardError = true
        };
        foreach (var argument in new[]
                 {
                     "--bind_ip", "127.0.0.1", "--port", Port.ToString(CultureInfo.InvariantCulture), "--dbpath", data,
                     "--logpath", Path.Combine(_root, "mongod.log")
                 })
        {
            start.ArgumentList.Add(argument);
        }

        _process = Process.Start(start) ?? throw new InvalidOperationException("The test-owned mongod did not start.");
        _ = _process.StandardOutput.ReadToEndAsync();
        _ = _process.StandardError.ReadToEndAsync();

        ConnectionString = $"mongodb://127.0.0.1:{Port}/?directConnection=true&serverSelectionTimeoutMS=2000";
        WaitForPing();
    }

    public int Port { get; }

    public string ConnectionString { get; }

    public void Dispose()
    {
        if (Interlocked.Exchange(ref _disposed, 1) == 1)
        {
            return;
        }

        if (!_process.HasExited)
        {
            _process.Kill(entireProcessTree: true);
            _process.WaitForExit(10_000);
        }

        _process.Dispose();
        try
        {
            Directory.Delete(_root, recursive: true);
        }
        catch (IOException)
        {
            // A file the dying process still held; the temp folder is the OS's to clean.
        }
    }

    private void WaitForPing()
    {
        var client = new MongoClient(ConnectionString);
        var deadline = DateTime.UtcNow.AddSeconds(30);
        while (true)
        {
            try
            {
                client.GetDatabase("admin").RunCommand<BsonDocument>(new BsonDocument("ping", 1));
                return;
            }
            catch (Exception) when (DateTime.UtcNow < deadline && !_process.HasExited)
            {
                Thread.Sleep(200);
            }
        }
    }

    private static string ResolveExecutable()
    {
        var given = Environment.GetEnvironmentVariable("DITEN_TEST_MONGOD");
        if (!string.IsNullOrWhiteSpace(given))
        {
            return File.Exists(given)
                ? given
                : throw new InvalidOperationException($"DITEN_TEST_MONGOD='{given}' is not an installed mongod.");
        }

        var windows = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.ProgramFiles), "MongoDB", "Server");
        if (Directory.Exists(windows))
        {
            var installed = Directory.GetDirectories(windows)
                .Select(dir => Path.Combine(dir, "bin", "mongod.exe"))
                .Where(File.Exists)
                .OrderByDescending(path => path, StringComparer.Ordinal)
                .FirstOrDefault();
            if (installed is not null)
            {
                return installed;
            }
        }

        var onPath = (Environment.GetEnvironmentVariable("PATH") ?? string.Empty)
            .Split(Path.PathSeparator, StringSplitOptions.RemoveEmptyEntries)
            .Select(dir => Path.Combine(dir, OperatingSystem.IsWindows() ? "mongod.exe" : "mongod"))
            .FirstOrDefault(File.Exists);

        return onPath ?? throw new InvalidOperationException(
            "No mongod for the CRM central-audit host tests: set DITEN_TEST_MONGOD to an installed mongod.");
    }

    private static int FreePort()
    {
        while (true)
        {
            var listener = new TcpListener(IPAddress.Loopback, 0);
            listener.Start();
            var port = ((IPEndPoint)listener.LocalEndpoint).Port;
            listener.Stop();
            if (!ProtectedPorts.Contains(port))
            {
                return port;
            }
        }
    }
}
