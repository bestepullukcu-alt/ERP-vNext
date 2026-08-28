using System.Net;
using System.Text;
using Diten.CrmService.Application.Common;
using Diten.CrmService.Application.Common.ReferenceValidation;
using Diten.CrmService.Infrastructure.ReferenceValidation;
using Microsoft.AspNetCore.Http;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Logging.Abstractions;
using Xunit;

namespace Diten.CrmService.Application.Tests;

/// <summary>
/// MOD-0149 — proves the MOD-0048/PSS-012 consumer seam sends the tenant `scope_key` (tenant-scoped sets
/// return `scope_key_required` without it) and degrades in a controlled way. No hardcoded/local fallback.
/// </summary>
public sealed class GatewayReferenceDataValidatorTests
{
    private static readonly Guid Tenant = Guid.Parse("97c59330-dbc4-4665-b29c-0c26dbb5cc93");

    private sealed class CapturingHandler : HttpMessageHandler
    {
        public Uri? LastUri { get; private set; }
        private readonly HttpStatusCode _status;
        private readonly string _body;
        public CapturingHandler(HttpStatusCode status, string body) { _status = status; _body = body; }
        protected override Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken cancellationToken)
        {
            LastUri = request.RequestUri;
            return Task.FromResult(new HttpResponseMessage(_status)
            {
                Content = new StringContent(_body, Encoding.UTF8, "application/json")
            });
        }
    }

    private static (GatewayReferenceDataValidator v, CapturingHandler h) Build(
        Guid? tenantId, HttpStatusCode status = HttpStatusCode.OK, string body = "[]")
    {
        var handler = new CapturingHandler(status, body);
        var client = new HttpClient(handler);
        var config = new ConfigurationBuilder().AddInMemoryCollection(new Dictionary<string, string?>
        {
            ["Gateway:BaseUrl"] = "http://localhost:5000"
        }).Build();

        var tenantContext = new TenantContext();
        if (tenantId is { } t) tenantContext.SetTenant(t);

        var validator = new GatewayReferenceDataValidator(
            client, config, new HttpContextAccessor(), tenantContext,
            NullLogger<GatewayReferenceDataValidator>.Instance);
        return (validator, handler);
    }

    [Fact]
    public async Task Sends_ScopeKey_With_Tenant()
    {
        var (v, h) = Build(Tenant, HttpStatusCode.OK, """[{"valueCode":"organization"}]""");

        var result = await v.ValidateAsync("account-type", "organization", default);

        Assert.NotNull(h.LastUri);
        Assert.Contains("/api/v1/reference-data/sets/account-type/published-values", h.LastUri!.ToString());
        Assert.Contains($"scope_key={Tenant}", h.LastUri!.ToString());
        Assert.Equal(ReferenceValidationStatus.Valid, result.Status);
    }

    [Fact]
    public async Task Without_Tenant_Returns_SetMissing_And_Does_Not_Call()
    {
        var (v, h) = Build(tenantId: null);

        var result = await v.ValidateAsync("account-type", "organization", default);

        Assert.Equal(ReferenceValidationStatus.SetMissing, result.Status);
        Assert.Null(h.LastUri); // no call attempted without a scope_key
    }

    [Fact]
    public async Task Unknown_Value_Returns_InvalidValue()
    {
        var (v, _) = Build(Tenant, HttpStatusCode.OK, """[{"valueCode":"hospital"}]""");

        var result = await v.ValidateAsync("account-type", "not-a-real-type", default);

        Assert.Equal(ReferenceValidationStatus.InvalidValue, result.Status);
    }

    [Fact]
    public async Task Parses_Canonical_Data_Items_Envelope()
    {
        // MOD-0048/PSS-012 canonical shape: Response<BusinessReferenceDataPublishedValuesModel>
        const string body = """
        {"data":{"setCode":"account-type","versionNumber":1,"publishedAt":"2026-07-16T00:00:00Z",
          "items":[{"valueCode":"organization","displayName":"Organization","isActive":true,"sortOrder":10},
                   {"valueCode":"hospital","displayName":"Hospital","isActive":true,"sortOrder":20}]},
         "isSuccessful":true,"statusCode":200}
        """;
        var (v, _) = Build(Tenant, HttpStatusCode.OK, body);

        Assert.Equal(ReferenceValidationStatus.Valid, (await v.ValidateAsync("account-type", "organization", default)).Status);
        Assert.Equal(ReferenceValidationStatus.Valid, (await v.ValidateAsync("account-type", "hospital", default)).Status);
        Assert.Equal(ReferenceValidationStatus.InvalidValue, (await v.ValidateAsync("account-type", "clinic", default)).Status);
    }

    [Fact]
    public async Task Deprecated_Value_Is_Not_Selectable()
    {
        const string body = """
        {"data":{"setCode":"account-status","items":[
            {"valueCode":"active","isActive":true},
            {"valueCode":"archived","isActive":false},
            {"valueCode":"legacy","isDeprecated":true}]}}
        """;
        var (v, _) = Build(Tenant, HttpStatusCode.OK, body);

        Assert.Equal(ReferenceValidationStatus.Valid, (await v.ValidateAsync("account-status", "active", default)).Status);
        Assert.Equal(ReferenceValidationStatus.InvalidValue, (await v.ValidateAsync("account-status", "archived", default)).Status);
        Assert.Equal(ReferenceValidationStatus.InvalidValue, (await v.ValidateAsync("account-status", "legacy", default)).Status);
    }

    [Fact]
    public async Task Unpublished_Set_Returns_SetMissing()
    {
        var (v, _) = Build(Tenant, HttpStatusCode.NotFound, """{"detail":"no_published_version"}""");

        var result = await v.ValidateAsync("account-status", "active", default);

        Assert.Equal(ReferenceValidationStatus.SetMissing, result.Status);
    }
}
