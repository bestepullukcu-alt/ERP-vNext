using System.Diagnostics;
using System.IdentityModel.Tokens.Jwt;
using System.Net;
using System.Net.Http.Headers;
using System.Net.Sockets;
using System.Security.Claims;
using System.Security.Cryptography;
using System.Text;
using Microsoft.IdentityModel.Tokens;
using Xunit;

namespace Diten.PpmService.IntegrationTests.GateI.DecisionTrace;

[Collection(GateIDisposableMongoReplicaSet.CollectionName)]
public sealed class GateILocalApiSmokeTests(GateIDisposableMongoReplicaSet mongo)
{
    private const int PpmPort = 5061;

    [Fact]
    public async Task Compiled_api_health_authenticated_gate_i_and_default_off_503_are_local_only()
    {
        Assert.True(CanBind(PpmPort), "Canonical PPM port 5061 is occupied; the smoke fails closed.");

        var temporaryRoot = Path.Combine(Path.GetTempPath(), $"diten-mod0117-api-smoke-{Guid.NewGuid():N}");
        Directory.CreateDirectory(temporaryRoot);
        var standardOutput = new StringBuilder();
        var standardError = new StringBuilder();
        Process? process = null;
        try
        {
            var assemblyPath = typeof(global::Program).Assembly.Location;
            Assert.True(File.Exists(assemblyPath));
            var secret = Convert.ToBase64String(RandomNumberGenerator.GetBytes(48));
            var startInfo = new ProcessStartInfo("dotnet")
            {
                UseShellExecute = false,
                CreateNoWindow = true,
                RedirectStandardOutput = true,
                RedirectStandardError = true,
                WorkingDirectory = temporaryRoot
            };
            startInfo.ArgumentList.Add(assemblyPath);
            SetEnvironment(startInfo, "ASPNETCORE_ENVIRONMENT", "Production");
            SetEnvironment(startInfo, "ASPNETCORE_URLS", $"http://127.0.0.1:{PpmPort}");
            SetEnvironment(startInfo, "Kestrel__Endpoints__Http__Url", $"http://127.0.0.1:{PpmPort}");
            SetEnvironment(startInfo, "JwtSettings__Issuer", "diten-mod0117-local-evidence");
            SetEnvironment(startInfo, "JwtSettings__Audience", "diten-ppm-service-local-evidence");
            SetEnvironment(startInfo, "JwtSettings__Secret", secret);
            SetEnvironment(startInfo, "Mongo__ConnectionString", mongo.ConnectionString);
            SetEnvironment(startInfo, "Mongo__DatabaseName", $"ppm_gate_i_api_{Guid.NewGuid():N}");
            SetEnvironment(startInfo, "GateI__Composition__Enabled", "false");
            SetEnvironment(startInfo, "GateI__DecisionTrace__Enabled", "false");
            SetEnvironment(startInfo, "GateI__FundingScenario__Enabled", "false");
            SetEnvironment(startInfo, "GateI__BenefitRealization__Enabled", "false");
            SetEnvironment(startInfo, "PpmEntitlementDecision__Enabled", "false");
            SetEnvironment(startInfo, "PpmAuditProducer__Enabled", "false");
            SetEnvironment(startInfo, "PpmAuditProducer__WorkerEnabled", "false");

            process = Process.Start(startInfo)
                ?? throw new InvalidOperationException("The compiled PPM API child process did not start.");
            process.OutputDataReceived += (_, args) => { if (args.Data is not null) standardOutput.AppendLine(args.Data); };
            process.ErrorDataReceived += (_, args) => { if (args.Data is not null) standardError.AppendLine(args.Data); };
            process.BeginOutputReadLine();
            process.BeginErrorReadLine();

            using var client = new HttpClient { BaseAddress = new Uri($"http://127.0.0.1:{PpmPort}") };
            await WaitForHealthyApiAsync(client, process, standardOutput, standardError);

            using var unauthenticated = await client.PutAsync(
                $"/api/v1/ppm/investment-cases/{Guid.NewGuid():D}/gate-i/governing-decision?expectedVersion=1",
                new StringContent("{}", Encoding.UTF8, "application/json"));
            Assert.Equal(HttpStatusCode.Unauthorized, unauthenticated.StatusCode);

            var tenantId = Guid.NewGuid();
            var actorId = Guid.NewGuid();
            client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue(
                "Bearer",
                CreateToken(secret, tenantId, actorId));
            client.DefaultRequestHeaders.Add("Idempotency-Key", $"local-smoke-{Guid.NewGuid():N}");
            using var defaultOff = await client.PutAsync(
                $"/api/v1/ppm/investment-cases/{Guid.NewGuid():D}/gate-i/governing-decision?expectedVersion=1",
                new StringContent("{}", Encoding.UTF8, "application/json"));
            Assert.Equal(HttpStatusCode.ServiceUnavailable, defaultOff.StatusCode);
            var responseBody = await defaultOff.Content.ReadAsStringAsync();
            Assert.Contains("Authoritative entitlement decision is unavailable", responseBody, StringComparison.Ordinal);
        }
        finally
        {
            if (process is { HasExited: false })
            {
                process.Kill(entireProcessTree: true);
                await process.WaitForExitAsync().WaitAsync(TimeSpan.FromSeconds(10));
            }
            process?.Dispose();
            if (Directory.Exists(temporaryRoot)) Directory.Delete(temporaryRoot, recursive: true);
        }

        Assert.True(CanBind(PpmPort), "The local PPM API smoke left a listener on port 5061.");
        Assert.False(Directory.Exists(temporaryRoot), "The local PPM API smoke left temporary residue.");
    }

    private static async Task WaitForHealthyApiAsync(
        HttpClient client,
        Process process,
        StringBuilder standardOutput,
        StringBuilder standardError)
    {
        var deadline = DateTime.UtcNow.AddSeconds(30);
        while (DateTime.UtcNow < deadline)
        {
            if (process.HasExited)
                throw new InvalidOperationException(
                    $"PPM API exited before readiness. stdout={standardOutput} stderr={standardError}");
            try
            {
                using var response = await client.GetAsync("/health");
                if (response.StatusCode == HttpStatusCode.OK) return;
            }
            catch (HttpRequestException)
            {
            }
            await Task.Delay(150);
        }
        throw new TimeoutException($"PPM API health did not become ready. stdout={standardOutput} stderr={standardError}");
    }

    private static string CreateToken(string secret, Guid tenantId, Guid actorId)
    {
        var key = new SymmetricSecurityKey(Encoding.UTF8.GetBytes(secret));
        var token = new JwtSecurityToken(
            issuer: "diten-mod0117-local-evidence",
            audience: "diten-ppm-service-local-evidence",
            claims:
            [
                new Claim("tenant_id", tenantId.ToString("D")),
                new Claim(JwtRegisteredClaimNames.Sub, actorId.ToString("D")),
                new Claim("permissions", "ppm.investment-cases.update")
            ],
            notBefore: DateTime.UtcNow.AddSeconds(-5),
            expires: DateTime.UtcNow.AddMinutes(2),
            signingCredentials: new SigningCredentials(key, SecurityAlgorithms.HmacSha256));
        return new JwtSecurityTokenHandler().WriteToken(token);
    }

    private static void SetEnvironment(ProcessStartInfo startInfo, string key, string value) =>
        startInfo.Environment[key] = value;

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
