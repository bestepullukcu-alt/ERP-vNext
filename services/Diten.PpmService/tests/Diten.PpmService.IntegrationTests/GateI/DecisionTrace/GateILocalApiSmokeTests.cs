using System.Diagnostics;
using System.Net;
using System.Net.Http.Headers;
using System.Net.Sockets;
using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using MongoDB.Driver;
using Xunit;

namespace Diten.PpmService.IntegrationTests.GateI.DecisionTrace;

[Collection(GateILocalEvidenceCollection.CollectionName)]
public sealed class GateILocalApiSmokeTests(GateIDisposableMongoReplicaSet mongo)
{
    private const int ApiPort = 5062;
    private const string Issuer = "diten-ppm-gate-i-local-test";
    private const string Audience = "diten-ppm-gate-i-local-test-client";

    [Fact]
    public async Task Occupied_canonical_port_fails_closed_without_starting_or_reusing_a_process()
    {
        using var occupied = new TcpListener(IPAddress.Loopback, ApiPort);
        occupied.Start();
        var host = new LocalApiProcess(mongo);

        var failure = await Assert.ThrowsAsync<InvalidOperationException>(() => host.StartAsync());

        Assert.Equal("The canonical PPM local-test port 5062 is occupied.", failure.Message);
        Assert.Equal(0, host.ProcessStarts);
        Assert.True(occupied.Server.IsBound);
    }

    [Fact]
    public async Task Health_and_all_fourteen_authenticated_Gate_I_routes_prove_default_off_503_and_zero_residue()
    {
        await using var host = new LocalApiProcess(mongo);
        await host.StartAsync();
        using var client = new HttpClient { BaseAddress = new Uri($"http://127.0.0.1:{ApiPort}") };

        using var health = await client.GetAsync("/health");
        Assert.Equal(HttpStatusCode.OK, health.StatusCode);

        var before = await SnapshotAsync(mongo);
        var investmentCaseId = Guid.NewGuid();
        var benefitCommitmentId = Guid.NewGuid();
        var referenceId = Guid.NewGuid();
        var routes = new (HttpMethod Method, string Path, bool HasBody)[]
        {
            (HttpMethod.Put, $"/api/v1/ppm/investment-cases/{investmentCaseId:D}/gate-i/governing-decision?expectedVersion=1", true),
            (HttpMethod.Delete, $"/api/v1/ppm/investment-cases/{investmentCaseId:D}/gate-i/governing-decision?expectedVersion=1", false),
            (HttpMethod.Post, $"/api/v1/ppm/investment-cases/{investmentCaseId:D}/gate-i/supporting-decisions?expectedVersion=1", true),
            (HttpMethod.Delete, $"/api/v1/ppm/investment-cases/{investmentCaseId:D}/gate-i/supporting-decisions/{referenceId:D}?expectedVersion=1", false),
            (HttpMethod.Put, $"/api/v1/ppm/investment-cases/{investmentCaseId:D}/gate-i/selected-budget-version?expectedVersion=1", true),
            (HttpMethod.Delete, $"/api/v1/ppm/investment-cases/{investmentCaseId:D}/gate-i/selected-budget-version?expectedVersion=1", false),
            (HttpMethod.Post, $"/api/v1/ppm/investment-cases/{investmentCaseId:D}/gate-i/scenario-versions?expectedVersion=1", true),
            (HttpMethod.Delete, $"/api/v1/ppm/investment-cases/{investmentCaseId:D}/gate-i/scenario-versions/{referenceId:D}?expectedVersion=1", false),
            (HttpMethod.Post, $"/api/v1/ppm/investment-cases/{investmentCaseId:D}/gate-i/comparator-outputs?expectedVersion=1", true),
            (HttpMethod.Delete, $"/api/v1/ppm/investment-cases/{investmentCaseId:D}/gate-i/comparator-outputs/{referenceId:D}?expectedVersion=1", false),
            (HttpMethod.Put, $"/api/v1/ppm/investment-cases/{investmentCaseId:D}/gate-i/selected-scenario?expectedVersion=1", true),
            (HttpMethod.Delete, $"/api/v1/ppm/investment-cases/{investmentCaseId:D}/gate-i/selected-scenario?expectedVersion=1", false),
            (HttpMethod.Post, $"/api/v1/ppm/benefit-commitments/{benefitCommitmentId:D}/gate-i/outcomes?expectedVersion=1", true),
            (HttpMethod.Delete, $"/api/v1/ppm/benefit-commitments/{benefitCommitmentId:D}/gate-i/outcomes/{referenceId:D}?expectedVersion=1", false)
        };

        foreach (var (method, path, hasBody) in routes)
        {
            using var request = new HttpRequestMessage(method, path);
            request.Headers.Authorization = new AuthenticationHeaderValue("Bearer", CreateToken(host.JwtSecret));
            request.Headers.TryAddWithoutValidation("Idempotency-Key", $"gate-i-local-smoke-{Guid.NewGuid():N}");
            if (hasBody)
                request.Content = new StringContent("{}", Encoding.UTF8, "application/json");
            using var response = await client.SendAsync(request);

            Assert.True(
                response.StatusCode == HttpStatusCode.ServiceUnavailable,
                $"Expected 503 for {method} {path} but received {(int)response.StatusCode}; WWW-Authenticate: {string.Join(" | ", response.Headers.WwwAuthenticate)}; Logs: {host.Logs}");
        }

        Assert.Equal(before, await SnapshotAsync(mongo));
        Assert.Equal(1, host.ProcessStarts);
    }

    private static async Task<string> SnapshotAsync(GateIDisposableMongoReplicaSet mongo)
    {
        var database = new MongoClient(mongo.ConnectionString).GetDatabase(mongo.DatabaseName);
        var names = await (await database.ListCollectionNamesAsync()).ToListAsync();
        var rows = new List<string>();
        foreach (var name in names.OrderBy(value => value, StringComparer.Ordinal))
            rows.Add($"{name}:{await database.GetCollection<MongoDB.Bson.BsonDocument>(name).CountDocumentsAsync(FilterDefinition<MongoDB.Bson.BsonDocument>.Empty)}");
        return string.Join("|", rows);
    }

    private static string CreateToken(string secret)
    {
        var now = DateTimeOffset.UtcNow;
        var header = Base64Url(JsonSerializer.SerializeToUtf8Bytes(new { alg = "HS256", typ = "JWT" }));
        var payload = Base64Url(JsonSerializer.SerializeToUtf8Bytes(new
        {
            iss = Issuer,
            aud = Audience,
            sub = Guid.NewGuid().ToString("D"),
            tenant_id = Guid.NewGuid().ToString("D"),
            nbf = now.AddMinutes(-1).ToUnixTimeSeconds(),
            exp = now.AddMinutes(5).ToUnixTimeSeconds()
        }));
        var signingInput = $"{header}.{payload}";
        var signature = HMACSHA256.HashData(
            Encoding.UTF8.GetBytes(secret),
            Encoding.ASCII.GetBytes(signingInput));
        return $"{signingInput}.{Base64Url(signature)}";
    }

    private static string Base64Url(ReadOnlySpan<byte> bytes) =>
        Convert.ToBase64String(bytes).TrimEnd('=').Replace('+', '-').Replace('/', '_');

    private sealed class LocalApiProcess(GateIDisposableMongoReplicaSet mongo) : IAsyncDisposable
    {
        private Process? _process;
        private readonly StringBuilder _logs = new();
        public int ProcessStarts { get; private set; }
        public string JwtSecret { get; } = Convert.ToBase64String(RandomNumberGenerator.GetBytes(48));
        public string Logs
        {
            get { lock (_logs) return _logs.ToString(); }
        }

        public async Task StartAsync()
        {
            if (!CanBind(ApiPort))
                throw new InvalidOperationException("The canonical PPM local-test port 5062 is occupied.");

            var apiAssembly = typeof(global::Program).Assembly.Location;
            var start = new ProcessStartInfo("dotnet")
            {
                WorkingDirectory = Path.GetDirectoryName(apiAssembly)!,
                UseShellExecute = false,
                CreateNoWindow = true,
                RedirectStandardOutput = true,
                RedirectStandardError = true
            };
            start.ArgumentList.Add(apiAssembly);
            start.ArgumentList.Add("--urls");
            start.ArgumentList.Add($"http://127.0.0.1:{ApiPort}");
            SetEnvironment(start);

            _process = Process.Start(start)
                ?? throw new InvalidOperationException("The PPM local-test API process did not start.");
            ProcessStarts++;
            _process.OutputDataReceived += Capture;
            _process.ErrorDataReceived += Capture;
            _process.BeginOutputReadLine();
            _process.BeginErrorReadLine();

            try
            {
                await WaitForHealthAsync(_process);
            }
            catch
            {
                await DisposeAsync();
                throw;
            }
        }

        public async ValueTask DisposeAsync()
        {
            if (_process is not null)
            {
                if (!_process.HasExited)
                {
                    _process.Kill(entireProcessTree: true);
                    await _process.WaitForExitAsync().WaitAsync(TimeSpan.FromSeconds(10));
                }
                _process.Dispose();
                _process = null;
            }

            if (!CanBind(ApiPort))
                throw new InvalidOperationException("The PPM local-test API listener was not cleaned up.");
        }

        private void SetEnvironment(ProcessStartInfo start)
        {
            start.Environment["ASPNETCORE_ENVIRONMENT"] = "Production";
            start.Environment["DOTNET_ENVIRONMENT"] = "Production";
            start.Environment["JwtSettings__Issuer"] = Issuer;
            start.Environment["JwtSettings__Audience"] = Audience;
            start.Environment["JwtSettings__Secret"] = JwtSecret;
            start.Environment["Mongo__ConnectionString"] = mongo.ConnectionString;
            start.Environment["Mongo__DatabaseName"] = mongo.DatabaseName;
            start.Environment["PpmEntitlementDecision__Enabled"] = "false";
            start.Environment["PpmAuditProducer__Enabled"] = "false";
            start.Environment["PpmAuditProducer__WorkerEnabled"] = "false";
            start.Environment["GateI__Composition__Enabled"] = "false";
            start.Environment["GateI__DecisionTrace__Enabled"] = "false";
            start.Environment["GateI__FundingScenario__Enabled"] = "false";
            start.Environment["GateI__BenefitRealization__Enabled"] = "false";
        }

        private void Capture(object sender, DataReceivedEventArgs args)
        {
            if (args.Data is null) return;
            lock (_logs) _logs.AppendLine(args.Data);
        }

        private static async Task WaitForHealthAsync(Process process)
        {
            using var client = new HttpClient { BaseAddress = new Uri($"http://127.0.0.1:{ApiPort}") };
            var deadline = DateTime.UtcNow.AddSeconds(20);
            Exception? last = null;
            while (DateTime.UtcNow < deadline)
            {
                if (process.HasExited)
                    throw new InvalidOperationException(
                        $"The PPM local-test API exited with code {process.ExitCode}.");
                try
                {
                    using var response = await client.GetAsync("/health");
                    if (response.IsSuccessStatusCode) return;
                }
                catch (HttpRequestException exception)
                {
                    last = exception;
                }
                await Task.Delay(100);
            }
            throw new InvalidOperationException("The PPM local-test API did not become healthy.", last);
        }

        private static bool CanBind(int port)
        {
            try
            {
                using var listener = new TcpListener(IPAddress.Loopback, port);
                listener.Start();
                listener.Stop();
                return true;
            }
            catch (SocketException)
            {
                return false;
            }
        }
    }
}
