using System.Net;
using System.Security.Claims;
using System.Text;
using System.Text.Json;
using System.Text.Json.Nodes;
using Diten.MdmService.Application.Contracts.ReferenceData;
using Diten.MdmService.Application.Features.ItemMaster;
using Diten.MdmService.Infrastructure;
using Diten.MdmService.Infrastructure.ReferenceData;
using Microsoft.AspNetCore.Http;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Options;
using Xunit;

namespace Diten.MdmService.Application.Tests.ReferenceData;

/// <summary>
/// WP-PLT-ITEM-LISTS-01: the MDM client of Platform's ITEM-UNIVERSAL-V1 resolver — the contract answer (the file
/// Platform's handler test produces) is trusted; anything that does not exactly cover the request is UNTRUSTED_ANSWER;
/// every failure is fail-closed; the production wiring and its named start refusal.
/// </summary>
public sealed class PlatformItemReferenceResolverClientTests
{
    private static readonly ItemReferenceRequest[] ContractRequest =
    [
        new("material-type", "RAW_MATERIAL"), new("item-uom", "KGM"), new("item-uom", "GRM"),
        new("storage-condition", "AMBIENT_15_25"), new("storage-condition", "PROTECT_FROM_LIGHT")
    ];

    // ------------------------------------------------------------------------------------------- contract

    [Fact]
    public async Task The_contract_answer_is_trusted_and_mapped()
    {
        var handler = new Recording(_ => Json(HttpStatusCode.OK, ContractResponse()));

        var result = await Client(handler).ResolveLatestAsync(ContractRequest);

        Assert.True(result.IsSuccessful, result.FailureCode);
        Assert.Equal(5, result.Selections.Count);
        Assert.Equal("TEMPERATURE", result.Selections.Single(x => x.ValueCode == "AMBIENT_15_25").Attributes["ConditionKind"]);
        Assert.Equal("MASS", result.Selections.Single(x => x.ValueCode == "KGM").Attributes["DimensionCode"]);
        var request = Assert.Single(handler.Requests);
        Assert.Equal("https://platform.test/" + PlatformItemReferenceResolverClient.RelativePath, request.Uri);
        // FIX1 M5: the secret header's VALUE, not only its presence.
        Assert.Equal("resolver-secret", request.Headers[PlatformItemReferenceResolverClient.CredentialSecretHeader]);
        // FIX1 R2: the body the real client sends is the contract's request, exactly.
        Assert.True(JsonNode.DeepEquals(ContractFixture()["request"], JsonNode.Parse(request.Body)), request.Body);
        Assert.Equal("Bearer user-token", request.Authorization);
        Assert.Equal("resolver-id", request.Headers[PlatformItemReferenceResolverClient.CredentialIdHeader]);
        Assert.Equal("VERIFIED_GSKU_RESOLVE", request.Headers[PlatformItemReferenceResolverClient.AudienceHeader]);
        Assert.Equal(5, JsonNode.Parse(request.Body)!["selections"]!.AsArray().Count);
    }

    // Each way an answer can fail to cover the request exactly.
    [Theory]
    [InlineData("pair-missing")]
    [InlineData("pair-extra")]
    [InlineData("pair-repeated")]
    [InlineData("storage-without-kind")]
    [InlineData("storage-unknown-kind")]
    [InlineData("unit-without-dimension")]
    [InlineData("unit-bad-precision")]
    [InlineData("attributes-null")]
    [InlineData("version-disagrees-in-set")]
    [InlineData("catalog-version-empty")]
    [InlineData("catalog-version-id-empty")]
    [InlineData("catalog-version-number-zero")]
    [InlineData("mode-pinned")]
    [InlineData("selection-null")]
    public async Task An_answer_that_does_not_exactly_cover_the_request_is_untrusted(string fault)
    {
        var answer = ContractResponse();
        var selections = answer["data"]!["selections"]!.AsArray();
        JsonNode Find(string value) => selections.First(x => (string)x!["value_code"]! == value)!;
        switch (fault)
        {
            case "pair-missing": selections.RemoveAt(4); break;
            case "pair-extra": selections.Add(Find("KGM").DeepClone().AsObject().Also(x => x["value_code"] = "LTR")); break;
            case "pair-repeated": selections[4] = Find("KGM").DeepClone(); break;
            case "storage-without-kind": Find("AMBIENT_15_25")["attributes"]!.AsObject().Remove("ConditionKind"); break;
            case "storage-unknown-kind": Find("AMBIENT_15_25")["attributes"]!["ConditionKind"] = "HUMIDITY"; break;
            case "unit-without-dimension": Find("GRM")["attributes"]!.AsObject().Remove("DimensionCode"); break;
            case "unit-bad-precision": Find("GRM")["attributes"]!["MaximumDecimalPrecision"] = "three"; break;
            case "attributes-null": Find("RAW_MATERIAL")["attributes"] = null; break;
            case "version-disagrees-in-set": Find("GRM")["catalog_version_id"] = Guid.NewGuid().ToString(); break;
            // FIX1 (CT L3): on the single-member set, so no other rule (one version per set) can catch it first.
            case "catalog-version-empty": Find("RAW_MATERIAL")["catalog_version"] = ""; break;
            case "catalog-version-id-empty": Find("RAW_MATERIAL")["catalog_version_id"] = Guid.Empty.ToString(); break;
            case "catalog-version-number-zero": Find("RAW_MATERIAL")["catalog_version_number"] = 0; break;
            case "mode-pinned": Find("KGM")["resolution_mode"] = "PINNED"; break;
            case "selection-null": selections[0] = null; break;
        }

        var result = await Client(new Recording(_ => Json(HttpStatusCode.OK, answer))).ResolveLatestAsync(ContractRequest);

        Assert.False(result.IsSuccessful);
        Assert.Equal(503, result.StatusCode);
        Assert.Equal(ItemReferenceCheck.UntrustedAnswer, result.FailureCode);
    }

    // ------------------------------------------------------------------------------------------- named refusals

    [Fact]
    public async Task A_value_platform_does_not_hold_is_named_missing()
    {
        var result = await Client(new Recording(_ => Json(HttpStatusCode.NotFound, NotFound(("item-uom", "GRM")))))
            .ResolveLatestAsync(ContractRequest);

        Assert.Equal(ItemReferenceResolveResult.ValueNotFound, result.FailureCode);
        Assert.Equal(new[] { new ItemReferenceRequest("item-uom", "GRM") }, result.NotFound);
    }

    // FIX1 R2: the contract's 404 (Platform's handler produces it) is read as a named "missing".
    [Fact]
    public async Task The_contract_not_found_answer_is_named_missing()
    {
        var contract = ContractFixture()["not_found"]!;
        var asked = contract["request"]!["selections"]!.AsArray()
            .Select(x => new ItemReferenceRequest((string)x!["set_code"]!, (string)x["value_code"]!)).ToArray();
        var handler = new Recording(_ => Json(HttpStatusCode.NotFound, contract["response"]!.DeepClone()));

        var result = await Client(handler).ResolveLatestAsync(asked);

        Assert.Equal(ItemReferenceResolveResult.ValueNotFound, result.FailureCode);
        Assert.Equal(new[] { new ItemReferenceRequest("item-uom", "LBR") }, result.NotFound);
        Assert.True(JsonNode.DeepEquals(contract["request"], JsonNode.Parse(Assert.Single(handler.Requests).Body)));
    }

    // FIX1 R2: the contract's 400 is an outage for the item, with Platform's reason.
    [Fact]
    public async Task The_contract_bad_request_answer_is_an_outage_with_its_reason()
    {
        var contract = ContractFixture()["bad_request"]!;
        var result = await Client(new Recording(_ => Json(HttpStatusCode.BadRequest, contract["response"]!.DeepClone())))
            .ResolveLatestAsync([new ItemReferenceRequest("uom", "KGM")]);
        Assert.Equal(503, result.StatusCode);
        Assert.Equal("REFERENCE_SET_NOT_SUPPORTED", result.FailureCode);
    }

    // FIX1 M6 / M5: the 200 limit and the material types are the contract's (Platform binds the same file).
    [Fact]
    public void The_limit_and_the_material_types_are_the_contracts()
    {
        var fixture = ContractFixture();
        Assert.Equal((int)fixture["maximum_selections"]!, PlatformItemReferenceResolverClient.MaximumSelections);
        Assert.Equal(fixture["material_types"]!.AsArray().Select(x => (string)x!), ItemRules.MaterialTypeValueCodes);
    }

    // FIX1 M2: an HTML error page is a named outage, not a crash.
    [Fact]
    public async Task An_html_error_page_is_a_named_outage()
    {
        var result = await Client(new Recording(_ => new HttpResponseMessage(HttpStatusCode.BadGateway)
        {
            Content = new StringContent("<html><body>Bad gateway</body></html>", Encoding.UTF8, "text/html")
        })).ResolveLatestAsync(ContractRequest);
        Assert.Equal(503, result.StatusCode);
        Assert.Equal("REFERENCE_PROVIDER_UNAVAILABLE", result.FailureCode);
    }

    // FIX1 M2: a failure that is neither HTTP nor JSON (here the transport throws something unexpected) is still a named
    // outage. The HTML page above is caught earlier (a JsonException); this one reaches the catch-all.
    [Fact]
    public async Task An_unexpected_transport_failure_is_a_named_outage()
    {
        var result = await Client(new Recording((HttpRequestMessage _) => throw new InvalidOperationException("unexpected")))
            .ResolveLatestAsync(ContractRequest);
        Assert.Equal(503, result.StatusCode);
        Assert.Equal("REFERENCE_PROVIDER_UNAVAILABLE", result.FailureCode);
    }

    // FIX1 M4: outside Development an http base is refused (the secret never goes as plain text); Development may use it.
    [Theory]
    [InlineData(null, false)]
    [InlineData("Production", false)]
    [InlineData("Development", true)]
    public async Task An_http_base_is_used_only_in_development(string? environment, bool sends)
    {
        var handler = new Recording(_ => Json(HttpStatusCode.OK, ContractResponse()));
        var options = Valid();
        options.PlatformBaseAddress = new Uri("http://platform.test/");
        var result = await Client(handler, options: options,
            environment: environment is null ? null : new ItemListsTestEnvironment(environment)).ResolveLatestAsync(ContractRequest);
        Assert.Equal(sends, handler.Requests.Count == 1);
        if (!sends) Assert.Equal(PlatformItemReferenceResolverClient.ConfigurationInvalid, result.FailureCode);
    }

    // A "missing" pair we never asked for is not a named answer.
    [Fact]
    public async Task A_missing_pair_that_was_not_asked_is_untrusted()
    {
        var result = await Client(new Recording(_ => Json(HttpStatusCode.NotFound, NotFound(("item-uom", "LBR")))))
            .ResolveLatestAsync(ContractRequest);
        Assert.Equal(ItemReferenceCheck.UntrustedAnswer, result.FailureCode);
    }

    [Theory]
    [InlineData(HttpStatusCode.BadRequest, "REFERENCE_SET_NOT_SUPPORTED", "REFERENCE_SET_NOT_SUPPORTED")]
    [InlineData(HttpStatusCode.Unauthorized, null, "REFERENCE_UNAUTHENTICATED")]
    [InlineData(HttpStatusCode.Forbidden, "REFERENCE_FORBIDDEN", "REFERENCE_FORBIDDEN")]
    [InlineData(HttpStatusCode.InternalServerError, "something", "REFERENCE_PROVIDER_UNAVAILABLE")]
    [InlineData(HttpStatusCode.GatewayTimeout, null, "REFERENCE_PROVIDER_TIMEOUT")]
    public async Task Every_provider_failure_is_an_outage_for_the_item(HttpStatusCode status, string? reason, string code)
    {
        var result = await Client(new Recording(_ => Json(status,
            new JsonObject { ["isSuccessful"] = false, ["reason_code"] = reason }))).ResolveLatestAsync(ContractRequest);
        Assert.Equal(503, result.StatusCode);
        Assert.Equal(code, result.FailureCode);
    }

    [Fact]
    public async Task An_answer_slower_than_the_budget_is_a_timeout()
    {
        var clock = System.Diagnostics.Stopwatch.StartNew();
        var result = await Client(new Recording(async token =>
        {
            await Task.Delay(TimeSpan.FromSeconds(10), token);
            return Json(HttpStatusCode.OK, ContractResponse());
        }), TimeSpan.FromMilliseconds(200)).ResolveLatestAsync(ContractRequest);

        Assert.Equal("REFERENCE_PROVIDER_TIMEOUT", result.FailureCode);
        Assert.True(clock.Elapsed < TimeSpan.FromSeconds(3), $"no budget: {clock.Elapsed}");
    }

    [Fact]
    public async Task Without_the_callers_token_nothing_is_sent()
    {
        var handler = new Recording(_ => Json(HttpStatusCode.OK, ContractResponse()));
        var result = await Client(handler, authenticated: false).ResolveLatestAsync(ContractRequest);
        Assert.Equal("REFERENCE_UNAUTHENTICATED", result.FailureCode);
        Assert.Empty(handler.Requests);
    }

    [Theory]
    [InlineData("no-address")]
    [InlineData("budget-over-2s")]
    [InlineData("no-secret")]
    public async Task A_bad_configuration_sends_nothing(string fault)
    {
        var handler = new Recording(_ => Json(HttpStatusCode.OK, ContractResponse()));
        var options = Valid();
        if (fault == "no-address") options.PlatformBaseAddress = null;
        if (fault == "budget-over-2s") options.Timeout = TimeSpan.FromSeconds(3);
        if (fault == "no-secret") options.CredentialSecret = null;
        var result = await Client(handler, options: options).ResolveLatestAsync(ContractRequest);
        Assert.Equal("REFERENCE_PROVIDER_CONFIGURATION_INVALID", result.FailureCode);
        Assert.Empty(handler.Requests);
    }

    [Fact]
    public async Task More_than_200_codes_are_never_sent()
    {
        var handler = new Recording(_ => Json(HttpStatusCode.OK, ContractResponse()));
        var many = Enumerable.Range(0, 201).Select(i => new ItemReferenceRequest("item-uom", $"U{i}")).ToList();
        var result = await Client(handler).ResolveLatestAsync(many);
        Assert.Equal("REFERENCE_BATCH_LIMIT_EXCEEDED", result.FailureCode);
        Assert.Empty(handler.Requests);
    }

    // ------------------------------------------------------------------------------------------- production wiring

    // CT decision A: always the Platform client (like GSKU / market); a missing configuration never refuses the start.
    [Fact]
    public void Production_always_wires_the_platform_client_and_never_refuses_the_start()
    {
        using var unconfigured = Provider(new());
        Assert.IsType<PlatformItemReferenceResolverClient>(unconfigured.GetRequiredService<IItemReferenceResolver>());
    }

    // CT decision A: without configuration the call fails closed — named, logged — never an empty "pass".
    [Fact]
    public async Task Without_configuration_the_call_fails_closed_and_says_so()
    {
        var handler = new Recording(_ => Json(HttpStatusCode.OK, ContractResponse()));
        var log = new CapturingLogger<PlatformItemReferenceResolverClient>();
        var result = await Client(handler, options: new VerifiedGskuResolverOptions(), logger: log)
            .ResolveLatestAsync(ContractRequest);

        Assert.False(result.IsSuccessful);
        Assert.Equal(503, result.StatusCode);
        Assert.Equal(PlatformItemReferenceResolverClient.ConfigurationInvalid, result.FailureCode);
        Assert.Empty(result.Selections);
        Assert.Single(log.Warnings);
        Assert.Empty(handler.Requests);
    }

    // CT decision A: outside Development, one start-up warning when unconfigured; Development and a configured start
    // say nothing; the start is never stopped.
    [Theory]
    [InlineData("Production", false, 1)]
    [InlineData("Staging", false, 1)]
    [InlineData("Development", false, 0)]
    [InlineData("Production", true, 0)]
    [InlineData("Production", null, 1)]
    public async Task The_start_warns_once_outside_development_when_unconfigured(string environment, bool? configured, int warnings)
    {
        var log = new CapturingLogger<Diten.MdmService.Api.ReferenceData.ItemReferenceProviderStartupWarning>();
        var warning = new Diten.MdmService.Api.ReferenceData.ItemReferenceProviderStartupWarning(
            new ItemListsTestEnvironment(environment), Options.Create(configured switch
            {
                true => Valid(),
                // FIX1 M4: an http base outside Development is "not configured".
                null => Valid().Also(o => o.PlatformBaseAddress = new Uri("http://platform.test/")),
                false => new VerifiedGskuResolverOptions()
            }), log);

        await warning.StartAsync(CancellationToken.None);

        Assert.Equal(warnings, log.Warnings.Count);
        if (warnings > 0) Assert.Equal(ItemReferenceProvider.NotConfiguredWarning, log.Warnings[0]);
    }

    // ------------------------------------------------------------------------------------------- helpers

    private static ServiceProvider Provider(Dictionary<string, string?> settings)
    {
        var services = new ServiceCollection();
        services.AddLogging();
        services.AddInfrastructure(new ConfigurationBuilder().AddInMemoryCollection(settings).Build());
        return services.BuildServiceProvider();
    }

    internal static string ContractFixturePath()
    {
        var directory = new DirectoryInfo(AppContext.BaseDirectory);
        while (directory is not null && !Directory.Exists(Path.Combine(directory.FullName, "services")))
            directory = directory.Parent;
        return Path.Combine(directory!.FullName, "services", "Diten.Platform", "tests", "Diten.Platform.Application.Tests",
            "BusinessReferenceData", "Fixtures", "item-universal-resolve.contract.json");
    }

    private static JsonNode ContractFixture() => JsonNode.Parse(File.ReadAllText(ContractFixturePath()))!;

    private static JsonObject ContractResponse() => ContractFixture()["response"]!.DeepClone().AsObject();

    private static JsonObject NotFound(params (string Set, string Value)[] pairs) => new()
    {
        ["isSuccessful"] = false,
        ["reason_code"] = "REFERENCE_VALUE_NOT_FOUND",
        ["data"] = new JsonObject
        {
            ["selections"] = new JsonArray(),
            ["not_found"] = new JsonArray(pairs.Select(p => (JsonNode)new JsonObject
                { ["set_code"] = p.Set, ["value_code"] = p.Value }).ToArray())
        }
    };

    private static HttpResponseMessage Json(HttpStatusCode status, JsonNode body) =>
        new(status) { Content = new StringContent(body.ToJsonString(), Encoding.UTF8, "application/json") };

    private static VerifiedGskuResolverOptions Valid() => new()
    {
        PlatformBaseAddress = new Uri("https://platform.test/"),
        Timeout = TimeSpan.FromSeconds(2),
        CredentialIdentifier = "resolver-id",
        CredentialSecret = "resolver-secret"
    };

    private static PlatformItemReferenceResolverClient Client(Recording handler, TimeSpan? timeout = null,
        bool authenticated = true, VerifiedGskuResolverOptions? options = null,
        Microsoft.Extensions.Logging.ILogger<PlatformItemReferenceResolverClient>? logger = null,
        Microsoft.Extensions.Hosting.IHostEnvironment? environment = null)
    {
        var http = new DefaultHttpContext();
        if (authenticated)
        {
            http.User = new ClaimsPrincipal(new ClaimsIdentity([new Claim("sub", "user")], "Bearer"));
            http.Request.Headers.Authorization = "Bearer user-token";
        }
        var resolved = options ?? Valid();
        if (timeout is { } budget) resolved.Timeout = budget;
        return new PlatformItemReferenceResolverClient(new HttpClient(handler),
            new HttpContextAccessor { HttpContext = http }, Options.Create(resolved),
            logger ?? Microsoft.Extensions.Logging.Abstractions.NullLogger<PlatformItemReferenceResolverClient>.Instance,
            environment);
    }

    private sealed record Captured(string Uri, string? Authorization, Dictionary<string, string> Headers, string Body);

    private sealed class Recording(Func<CancellationToken, Task<HttpResponseMessage>> answer) : HttpMessageHandler
    {
        public Recording(Func<HttpRequestMessage, HttpResponseMessage> answer) : this(_ => Task.FromResult(answer(null!))) { }

        public List<Captured> Requests { get; } = [];

        protected override async Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken token)
        {
            Requests.Add(new Captured(request.RequestUri!.ToString(), request.Headers.Authorization?.ToString(),
                request.Headers.ToDictionary(h => h.Key, h => string.Join(",", h.Value)),
                await request.Content!.ReadAsStringAsync(token)));
            return await answer(token);
        }
    }
}

internal sealed class CapturingLogger<T> : Microsoft.Extensions.Logging.ILogger<T>
{
    public List<string> Warnings { get; } = [];
    public IDisposable? BeginScope<TState>(TState state) where TState : notnull => null;
    public bool IsEnabled(Microsoft.Extensions.Logging.LogLevel logLevel) => true;
    public void Log<TState>(Microsoft.Extensions.Logging.LogLevel logLevel, Microsoft.Extensions.Logging.EventId eventId,
        TState state, Exception? exception, Func<TState, Exception?, string> formatter)
    {
        if (logLevel == Microsoft.Extensions.Logging.LogLevel.Warning) Warnings.Add(formatter(state, exception));
    }
}

internal sealed class ItemListsTestEnvironment(string name) : Microsoft.Extensions.Hosting.IHostEnvironment
{
    public string EnvironmentName { get; set; } = name;
    public string ApplicationName { get; set; } = "Diten.MdmService.Api";
    public string ContentRootPath { get; set; } = AppContext.BaseDirectory;
    public Microsoft.Extensions.FileProviders.IFileProvider ContentRootFileProvider { get; set; } =
        new Microsoft.Extensions.FileProviders.NullFileProvider();
}

internal static class JsonNodeTestExtensions
{
    public static T Also<T>(this T value, Action<T> change) where T : class
    {
        change(value);
        return value;
    }

}
