using System.Net.Http.Headers;
using System.Security.Claims;
using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using Diten.PvgService.Api;
using Diten.PvgService.Application.RegPvBase;
using Diten.PvgService.Domain.RegPvBase;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Hosting.Server.Features;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;
using Xunit;

namespace Diten.PvgService.Api.Tests;

// WP-0230-DL4E (MOD-0230 DL-4, AC-39, G-24): the triage form sends the outcome as text. Every request here goes to the
// real host (PvgServiceApiHost.CreatePvgServiceWebHostBuilder, Development, Kestrel on loopback with a random port)
// through its whole middleware chain with a signed test JWT and the tenant header; the endpoint method is never called
// directly. The permission gate is the host's own DenyAllPermissionGate. The request body is hand-written JSON text in
// the shape the Web proxy sends (TriageFormInput.ToPayload keys), never a serialized PvgCaseIntakeTriageRequest: a
// serialized body would write the enum as a number and hide a missing text binding. Keys are generated at run time and
// every identity value is fake.
public sealed class PvgCaseIntakeTriageBindingTests
{
    private const string Issuer = "pvg-test-issuer";
    private const string Audience = "pvg-test-audience";
    private static readonly string Secret = Convert.ToBase64String(RandomNumberGenerator.GetBytes(48));

    // ---- AC-39 (a) ----------------------------------------------------------------------------------------------

    [Theory]
    [InlineData("Triaged")]
    [InlineData("Rejected")]
    [InlineData("Duplicate")]
    public async Task Triage_with_the_outcome_as_the_form_sends_it_reaches_the_permission_gate(string outcome)
    {
        using var harness = Harness.Create();

        var response = await harness.TriageAsync(FormBody("\"" + outcome + "\""));

        // Today the permission refusal is a 409 (G-8); AC-6 makes it a 403. Either is a 4xx that is not the binding 400.
        Assert.InRange(response.StatusCode, 400, 499);
        Assert.NotEqual(400, response.StatusCode);
        Assert.False(string.IsNullOrWhiteSpace(response.Raw));
        Assert.Contains(PvgPermissionReasonCodes.PermissionDenied, response.Raw, StringComparison.Ordinal);
    }

    // ---- AC-39 (b) ----------------------------------------------------------------------------------------------

    [Theory]
    [InlineData("Unknown")]
    [InlineData("triaged")]
    [InlineData("TRIAGED")]
    [InlineData("1")]
    [InlineData("")]
    [InlineData(" Triaged")]
    [InlineData("Triaged, Rejected")]
    public async Task Triage_with_an_unknown_outcome_text_is_refused_with_a_4xx(string outcomeText)
    {
        using var harness = Harness.Create();

        var response = await harness.TriageAsync(FormBody("\"" + outcomeText + "\""));

        Assert.Equal(400, response.StatusCode);
        Assert.DoesNotContain(PvgPermissionReasonCodes.PermissionDenied, response.Raw, StringComparison.Ordinal);
        Assert.Equal(0, harness.Store.WriteCalls);
        Assert.Equal(0, harness.Store.ReadCalls);
    }

    // ---- CT reading, beyond the pack: a null and a number read as they did with the plain enum -----------------------

    [Theory]
    [InlineData("null")]
    [InlineData("1")]
    public async Task Triage_with_a_null_or_numeric_outcome_still_reaches_the_permission_gate(string outcomeJson)
    {
        using var harness = Harness.Create();

        var response = await harness.TriageAsync(FormBody(outcomeJson));

        Assert.InRange(response.StatusCode, 400, 499);
        Assert.NotEqual(400, response.StatusCode);
        Assert.False(string.IsNullOrWhiteSpace(response.Raw));
        Assert.Contains(PvgPermissionReasonCodes.PermissionDenied, response.Raw, StringComparison.Ordinal);
    }

    // The converter writes a defined value by name and any other value as the number, so its own Read accepts both.
    [Theory]
    [InlineData(PvgTriageOutcome.Triaged, "\"Triaged\"")]
    [InlineData(PvgTriageOutcome.Rejected, "\"Rejected\"")]
    [InlineData(PvgTriageOutcome.Duplicate, "\"Duplicate\"")]
    [InlineData((PvgTriageOutcome)4, "4")]
    public void The_converter_writes_a_defined_outcome_by_name_and_an_undefined_one_as_the_number(
        PvgTriageOutcome outcome,
        string expectedOutcomeJson)
    {
        var json = JsonSerializer.Serialize(
            new PvgCaseIntakeTriageRequest(outcome, null, null, 1),
            new JsonSerializerOptions(JsonSerializerDefaults.Web));

        Assert.Contains("\"triageOutcome\":" + expectedOutcomeJson, json, StringComparison.Ordinal);
        var back = JsonSerializer.Deserialize<PvgCaseIntakeTriageRequest>(
            json,
            new JsonSerializerOptions(JsonSerializerDefaults.Web));
        Assert.Equal(outcome, back!.TriageOutcome);
    }

    [Theory]
    [InlineData("1.5")]
    [InlineData("3000000000")]
    [InlineData("true")]
    [InlineData("[]")]
    [InlineData("{}")]
    public void The_converter_refuses_a_non_integer_number_and_every_other_token_with_a_json_exception(string outcomeJson)
    {
        Assert.Throws<JsonException>(() => JsonSerializer.Deserialize<PvgCaseIntakeTriageRequest>(
            FormBody(outcomeJson),
            new JsonSerializerOptions(JsonSerializerDefaults.Web)));
    }

    // ---- helpers ------------------------------------------------------------------------------------------------

    // The Web proxy's payload keys (TriageFormInput.ToPayload), written by hand.
    private static string FormBody(string outcomeJson) =>
        "{\"TriageOutcome\":" + outcomeJson + ",\"TriageReasonCode\":null,\"TriageReason\":null,\"ExpectedVersion\":1}";

    private sealed record Response(int StatusCode, string Raw);

    private sealed class CountingIntakeDraftStore : IPvgIntakeDraftStore
    {
        private int _writeCalls;
        private int _readCalls;

        public int WriteCalls => Volatile.Read(ref _writeCalls);

        public int ReadCalls => Volatile.Read(ref _readCalls);

        public ValueTask<string> AddAsync(
            PvgPersistenceTenantScope tenantScope,
            string actorId,
            SafetyCaseIntake intake,
            CancellationToken cancellationToken = default)
        {
            Interlocked.Increment(ref _writeCalls);
            return ValueTask.FromResult("pvg-draft-" + Guid.NewGuid().ToString("N"));
        }

        public ValueTask<PvgIntakeDraftReplaceResult> ReplaceAsync(
            PvgPersistenceTenantScope tenantScope,
            string intakeDraftId,
            long? expectedVersion,
            string actorId,
            SafetyCaseIntake intake,
            CancellationToken cancellationToken = default)
        {
            Interlocked.Increment(ref _writeCalls);
            return ValueTask.FromResult(PvgIntakeDraftReplaceResult.NotFound());
        }

        public ValueTask<PvgPersistedIntakeDraft?> FindByIdAsync(
            PvgPersistenceTenantScope tenantScope,
            string intakeDraftId,
            CancellationToken cancellationToken = default)
        {
            Interlocked.Increment(ref _readCalls);
            return ValueTask.FromResult<PvgPersistedIntakeDraft?>(null);
        }

        public ValueTask<IReadOnlyList<PvgPersistedIntakeDraft>> ListAsync(
            PvgPersistenceListScope listScope,
            CancellationToken cancellationToken = default)
        {
            Interlocked.Increment(ref _readCalls);
            return ValueTask.FromResult<IReadOnlyList<PvgPersistedIntakeDraft>>([]);
        }

        public ValueTask<int> CountAsync(
            PvgPersistenceTenantScope tenantScope,
            PvgIntakeStatus? status,
            CancellationToken cancellationToken = default)
        {
            Interlocked.Increment(ref _readCalls);
            return ValueTask.FromResult(0);
        }
    }

    // The real host on 127.0.0.1 with a random port; only the draft store is replaced (to count its calls). The
    // permission gate, field security, workflow gate and evidence port are the host's own non-production adapters.
    private sealed class Harness : IDisposable
    {
        private readonly IWebHost _host;
        private readonly HttpClient _client;

        private Harness(IWebHost host, CountingIntakeDraftStore store)
        {
            _host = host;
            Store = store;
            var address = host.ServerFeatures.Get<IServerAddressesFeature>()!.Addresses.First();
            _client = new HttpClient { BaseAddress = new Uri(address) };
        }

        public CountingIntakeDraftStore Store { get; }

        public static Harness Create()
        {
            var configuration = new ConfigurationBuilder()
                .AddInMemoryCollection(new Dictionary<string, string?>
                {
                    ["JwtSettings:Secret"] = Secret,
                    ["JwtSettings:Issuer"] = Issuer,
                    ["JwtSettings:Audience"] = Audience
                })
                .Build();
            var store = new CountingIntakeDraftStore();
            var host = PvgServiceApiHost
                .CreatePvgServiceWebHostBuilder(
                    "Development",
                    configuration,
                    "http://127.0.0.1:0",
                    services => services.Replace(ServiceDescriptor.Singleton<IPvgIntakeDraftStore>(store)))
                .Build();
            host.Start();
            return new Harness(host, store);
        }

        public async Task<Response> TriageAsync(string jsonBody)
        {
            var token = PvgTestIdentity.CreateToken(
                Secret,
                Issuer,
                Audience,
                [
                    new Claim("sub", "actor-a"),
                    new Claim("tenant_id", PvgTestIdentity.TenantA),
                    new Claim("actor_type", "tenant_user")
                ],
                DateTime.UtcNow.AddMinutes(30));

            using var request = new HttpRequestMessage(
                HttpMethod.Post,
                $"{PvgCaseIntakeTriageEndpoints.RoutePrefix}/pvg-draft-{Guid.NewGuid():N}/triage")
            {
                Content = new StringContent(jsonBody, Encoding.UTF8, "application/json")
            };
            request.Headers.Authorization = new AuthenticationHeaderValue("Bearer", token);
            request.Headers.Add(PvgCaseIntakeRequestContext.TenantHeader, PvgTestIdentity.TenantA);
            request.Headers.Add(PvgCaseIntakeRequestContext.CorrelationIdHeader, "corr-fake-1");

            using var response = await _client.SendAsync(request);
            return new Response((int)response.StatusCode, await response.Content.ReadAsStringAsync());
        }

        public void Dispose()
        {
            _client.Dispose();
            _host.Dispose();
        }
    }
}
