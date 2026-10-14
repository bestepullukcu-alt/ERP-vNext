using System.Diagnostics;
using System.Net;
using System.Net.Sockets;
using System.Text.Json;
using Diten.InventoryService.Tests.TestInfrastructure;
using Xunit;

namespace Diten.InventoryService.Tests.Contract;

/// <summary>
/// Starts Prism 4.10.5 mocks of the consumed contracts — PRODUCT-MASTER-BUNDLE v1.1 draft (WP-0173-PM1), LOCATION, and the
/// frozen PRODUCT-MASTER v1.0 copy (backward compatibility only) — on free local
/// ports and stops them afterwards. Requires Node (WP E5). If Prism cannot start the fixture throws — the smoke tests
/// then FAIL; they are never skipped.
///
/// ⚠ Prism runs as ONE <c>node</c> process on the cached CLI entry point, not through <c>cmd → npx → node</c>.
/// Measured on this machine: with the npx chain, killing the tree left the grandchild Prism alive; it held the test
/// host's inherited stdout pipe open and the test run never returned although every test had finished.
/// </summary>
public sealed class PrismMockFixture : IAsyncLifetime
{
    public const string PrismPackage = "@stoplight/prism-cli@4.10.5";
    public const string PrismVersion = "4.10.5";

    private readonly List<Process> _processes = [];

    public Uri ProductMasterBaseUrl { get; private set; } = null!;

    public Uri LocationMasterBaseUrl { get; private set; } = null!;

    /// <summary>WP-0173-PM1: the frozen PRODUCT-MASTER v1.0 copy — for the backward-compatibility smoke only.</summary>
    public Uri ProductMasterV10BaseUrl { get; private set; } = null!;

    public async Task InitializeAsync()
    {
        var entryPoint = await PrismEntryPointAsync();
        ProductMasterBaseUrl = await StartAsync(entryPoint, RepoPaths.Contract("product-master-bundle.openapi.yaml"));
        LocationMasterBaseUrl = await StartAsync(entryPoint, RepoPaths.Contract("location.openapi.yaml"));
        ProductMasterV10BaseUrl = await StartAsync(entryPoint, RepoPaths.Contract("product-master-bundle.v1.0.openapi.yaml"));
    }

    public Task DisposeAsync()
    {
        foreach (var process in _processes)
        {
            try
            {
                if (!process.HasExited)
                {
                    process.Kill(entireProcessTree: true);
                    process.WaitForExit(10_000);
                }
            }
            catch (Exception ex) when (ex is InvalidOperationException or System.ComponentModel.Win32Exception)
            {
                // already gone
            }

            process.Dispose();
        }

        return Task.CompletedTask;
    }

    /// <summary>The cached <c>prism-cli/dist/index.js</c> of exactly 4.10.5; installs it into the npx cache once if absent.</summary>
    private static async Task<string> PrismEntryPointAsync()
    {
        var found = FindCachedEntryPoint();
        if (found is not null)
        {
            return found;
        }

        // One-shot install: `npx --yes <pkg> --version` downloads into the npx cache and EXITS (no server process).
        using var install = Process.Start(new ProcessStartInfo
        {
            FileName = OperatingSystem.IsWindows() ? "cmd.exe" : "npx",
            Arguments = (OperatingSystem.IsWindows() ? "/c npx " : string.Empty) + $"--yes {PrismPackage} --version",
            UseShellExecute = false,
            RedirectStandardOutput = true,
            RedirectStandardError = true,
            RedirectStandardInput = true,
            CreateNoWindow = true
        }) ?? throw new InvalidOperationException("npx could not be started.");
        await install.WaitForExitAsync(new CancellationTokenSource(TimeSpan.FromMinutes(4)).Token);

        return FindCachedEntryPoint()
               ?? throw new InvalidOperationException($"{PrismPackage} is not available in the npx cache after install.");
    }

    private static string? FindCachedEntryPoint()
    {
        var cache = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData), "npm-cache", "_npx");
        if (!Directory.Exists(cache))
        {
            return null;
        }

        foreach (var dir in Directory.EnumerateDirectories(cache))
        {
            var package = Path.Combine(dir, "node_modules", "@stoplight", "prism-cli", "package.json");
            var entry = Path.Combine(dir, "node_modules", "@stoplight", "prism-cli", "dist", "index.js");
            if (File.Exists(package) && File.Exists(entry)
                && JsonDocument.Parse(File.ReadAllText(package)).RootElement.GetProperty("version").GetString() == PrismVersion)
            {
                return entry;
            }
        }

        return null;
    }

    private async Task<Uri> StartAsync(string entryPoint, string spec)
    {
        var port = FreePort();
        var info = new ProcessStartInfo
        {
            FileName = "node",
            UseShellExecute = false,
            RedirectStandardOutput = true,
            RedirectStandardError = true,
            RedirectStandardInput = true,
            CreateNoWindow = true
        };
        foreach (var argument in new[] { entryPoint, "mock", spec, "--port", port.ToString(System.Globalization.CultureInfo.InvariantCulture), "--host", "127.0.0.1" })
        {
            info.ArgumentList.Add(argument);
        }

        var process = Process.Start(info) ?? throw new InvalidOperationException("Prism did not start.");
        process.OutputDataReceived += (_, _) => { };
        process.ErrorDataReceived += (_, _) => { };
        process.BeginOutputReadLine();
        process.BeginErrorReadLine();
        _processes.Add(process);

        var baseUrl = new Uri($"http://127.0.0.1:{port}/");
        using var probe = new HttpClient { Timeout = TimeSpan.FromSeconds(2) };
        var deadline = DateTime.UtcNow.AddSeconds(90);
        while (DateTime.UtcNow < deadline)
        {
            if (process.HasExited)
            {
                throw new InvalidOperationException($"Prism exited early (code {process.ExitCode}) for {spec}.");
            }

            try
            {
                using var response = await probe.GetAsync(baseUrl);
                return baseUrl; // any HTTP answer means Prism is listening
            }
            catch (HttpRequestException)
            {
            }
            catch (TaskCanceledException)
            {
            }

            await Task.Delay(500);
        }

        throw new TimeoutException($"Prism did not listen on {baseUrl} within 90 s for {spec}.");
    }

    private static int FreePort()
    {
        var listener = new TcpListener(IPAddress.Loopback, 0);
        listener.Start();
        var port = ((IPEndPoint)listener.LocalEndpoint).Port;
        listener.Stop();
        return port;
    }
}
