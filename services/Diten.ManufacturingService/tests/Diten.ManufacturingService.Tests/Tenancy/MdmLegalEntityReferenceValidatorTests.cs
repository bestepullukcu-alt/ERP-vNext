using System.Net;
using System.Text;
using Diten.ManufacturingService.Application.Common;
using Diten.ManufacturingService.Infrastructure.LegalEntities;
using Microsoft.AspNetCore.Http;
using Microsoft.Extensions.Configuration;
using Xunit;

namespace Diten.ManufacturingService.Tests.Tenancy;

/// <summary>
/// The production MDM validator against a scripted HTTP answer: what MDM says maps to Valid / NotReferenceable /
/// Unavailable exactly as the CRM / Platform copies do, and the caller's token and tenant travel with the call.
/// </summary>
public sealed class MdmLegalEntityReferenceValidatorTests
{
    private static readonly Guid Le = Guid.NewGuid();
    private static readonly Guid Tenant = Guid.NewGuid();

    private static string Body(Guid id, string state = "ACTIVE", bool referenceable = true) =>
        $$"""{"data":{"legalEntityId":"{{id}}","code":"LE1","legalName":"Acme","displayName":"Acme","lifecycleState":"{{state}}","referenceable":{{(referenceable ? "true" : "false")}}},"statusCode":200,"isSuccessful":true,"errors":[]}""";

    [Theory]
    [InlineData(200, "ACTIVE", true, LegalEntityValidation.Valid)]
    [InlineData(200, "SUSPENDED", true, LegalEntityValidation.NotReferenceable)]
    [InlineData(200, "ACTIVE", false, LegalEntityValidation.NotReferenceable)]
    [InlineData(404, "", false, LegalEntityValidation.NotReferenceable)]
    [InlineData(403, "", false, LegalEntityValidation.Unavailable)]
    [InlineData(400, "", false, LegalEntityValidation.Unavailable)]
    [InlineData(500, "", false, LegalEntityValidation.Unavailable)]
    public async Task Mdm_answer_maps_fail_closed(int status, string state, bool referenceable, LegalEntityValidation expected)
    {
        var handler = new Scripted(_ => new HttpResponseMessage((HttpStatusCode)status)
        {
            Content = new StringContent(status == 200 ? Body(Le, state, referenceable) : "{}", Encoding.UTF8, "application/json")
        });
        Assert.Equal(expected, await Validator(handler).ValidateAsync(Le, CancellationToken.None));
    }

    [Fact]
    public async Task A_different_id_echoed_back_is_not_proof()
    {
        var handler = new Scripted(_ => Json(Body(Guid.NewGuid())));
        Assert.Equal(LegalEntityValidation.NotReferenceable, await Validator(handler).ValidateAsync(Le, CancellationToken.None));
    }

    [Fact]
    public async Task One_transient_failure_is_retried_two_are_unavailable()
    {
        var once = new Scripted(n => n == 1 ? new HttpResponseMessage(HttpStatusCode.ServiceUnavailable) : Json(Body(Le)));
        var twice = new Scripted(_ => new HttpResponseMessage(HttpStatusCode.BadGateway));

        Assert.Equal(LegalEntityValidation.Valid, await Validator(once).ValidateAsync(Le, CancellationToken.None));
        Assert.Equal(LegalEntityValidation.Unavailable, await Validator(twice).ValidateAsync(Le, CancellationToken.None));
        Assert.Equal(2, twice.Requests.Count);
    }

    [Fact]
    public async Task Unreachable_is_unavailable_never_valid()
    {
        var handler = new Scripted(_ => throw new HttpRequestException("connection refused"));
        Assert.Equal(LegalEntityValidation.Unavailable, await Validator(handler).ValidateAsync(Le, CancellationToken.None));
    }

    [Fact]
    public async Task The_callers_token_tenant_and_correlation_travel_through_the_gateway_path()
    {
        var handler = new Scripted(_ => Json(Body(Le)));
        await Validator(handler).ValidateAsync(Le, CancellationToken.None);

        var request = Assert.Single(handler.Requests);
        Assert.Equal($"http://gateway.test/api/legal-entities/{Le}/lookup-validation", request.RequestUri!.ToString());
        Assert.Equal("Bearer caller-token", request.Headers.Authorization!.ToString());
        Assert.Equal(Tenant.ToString(), request.Headers.GetValues("X-Tenant-Id").Single());
        Assert.Equal("corr-1", request.Headers.GetValues("X-Correlation-Id").Single());
    }

    private static HttpResponseMessage Json(string body) => new(HttpStatusCode.OK) { Content = new StringContent(body, Encoding.UTF8, "application/json") };

    private static MdmLegalEntityReferenceValidator Validator(Scripted handler)
    {
        var http = new DefaultHttpContext();
        http.Request.Headers.Authorization = "Bearer caller-token";
        var tenant = new TenantContext();
        tenant.SetTenant(Tenant, Le);
        var correlation = new CorrelationContext();
        correlation.Set("corr-1");
        var configuration = new ConfigurationBuilder().AddInMemoryCollection(new Dictionary<string, string?> { ["Gateway:BaseUrl"] = "http://gateway.test" }).Build();
        return new MdmLegalEntityReferenceValidator(new HttpClient(handler), configuration, new HttpContextAccessor { HttpContext = http }, tenant, correlation);
    }

    private sealed class Scripted(Func<int, HttpResponseMessage> answer) : HttpMessageHandler
    {
        public List<HttpRequestMessage> Requests { get; } = [];

        protected override Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken cancellationToken)
        {
            Requests.Add(request);
            return Task.FromResult(answer(Requests.Count));
        }
    }
}
