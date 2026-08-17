using System.Net;
using System.Text;
using Diten.Platform.Infrastructure.Services.Mdm;
using Diten.Platform.Infrastructure.Settings;
using Microsoft.AspNetCore.Http;
using Microsoft.Extensions.Options;
using Xunit;

namespace Diten.Platform.Application.Tests.TenantOrganization;

public sealed class MdmLegalEntityReferenceValidatorTests
{
    private static readonly Guid LegalEntityId = Guid.Parse("10000000-0000-0000-0000-000000000001");

    [Fact]
    public async Task Validate_returns_success_for_matching_active_referenceable_response()
    {
        var validator = CreateValidator(JsonResponse($$"""
            {"data":{"legalEntityId":"{{LegalEntityId}}","legalName":"Legal","displayName":"Legal","lifecycleState":"ACTIVE","referenceable":true},"statusCode":200,"isSuccessful":true,"errors":[]}
            """));

        var response = await validator.ValidateAsync(LegalEntityId);

        Assert.True(response.IsSuccessful);
        Assert.Equal(LegalEntityId, response.Data!.LegalEntityId);
    }

    [Fact]
    public async Task Validate_fails_closed_on_network_exception()
    {
        var validator = CreateValidator(_ => throw new HttpRequestException("network down"));

        var response = await validator.ValidateAsync(LegalEntityId);

        Assert.False(response.IsSuccessful);
        Assert.Equal(404, response.StatusCode);
    }

    [Fact]
    public async Task Validate_fails_closed_on_malformed_json()
    {
        var validator = CreateValidator(new HttpResponseMessage(HttpStatusCode.OK)
        {
            Content = new StringContent("{not-json", Encoding.UTF8, "application/json")
        });

        var response = await validator.ValidateAsync(LegalEntityId);

        Assert.False(response.IsSuccessful);
        Assert.Equal(404, response.StatusCode);
    }

    [Fact]
    public async Task Validate_fails_closed_on_null_payload()
    {
        var validator = CreateValidator(JsonResponse("""{"data":null,"statusCode":200,"isSuccessful":true,"errors":[]}"""));

        var response = await validator.ValidateAsync(LegalEntityId);

        Assert.False(response.IsSuccessful);
        Assert.Equal(404, response.StatusCode);
    }

    [Fact]
    public async Task Validate_fails_closed_on_legal_entity_id_mismatch()
    {
        var otherId = Guid.NewGuid();
        var validator = CreateValidator(JsonResponse($$"""
            {"data":{"legalEntityId":"{{otherId}}","legalName":"Legal","displayName":"Legal","lifecycleState":"ACTIVE","referenceable":true},"statusCode":200,"isSuccessful":true,"errors":[]}
            """));

        var response = await validator.ValidateAsync(LegalEntityId);

        Assert.False(response.IsSuccessful);
        Assert.Equal(404, response.StatusCode);
    }

    [Theory]
    [InlineData("DRAFT", true)]
    [InlineData("ARCHIVED", true)]
    [InlineData("ACTIVE", false)]
    public async Task Validate_fails_closed_on_non_active_or_non_referenceable_payload(string lifecycleState, bool referenceable)
    {
        var validator = CreateValidator(JsonResponse($$"""
            {"data":{"legalEntityId":"{{LegalEntityId}}","legalName":"Legal","displayName":"Legal","lifecycleState":"{{lifecycleState}}","referenceable":{{referenceable.ToString().ToLowerInvariant()}}},"statusCode":200,"isSuccessful":true,"errors":[]}
            """));

        var response = await validator.ValidateAsync(LegalEntityId);

        Assert.False(response.IsSuccessful);
        Assert.Equal(404, response.StatusCode);
    }

    [Fact]
    public async Task Validate_preserves_caller_cancellation()
    {
        using var cts = new CancellationTokenSource();
        await cts.CancelAsync();
        var validator = CreateValidator(async (_, ct) =>
        {
            await Task.Delay(TimeSpan.FromSeconds(10), ct);
            return JsonResponse("{}");
        });

        await Assert.ThrowsAsync<TaskCanceledException>(() => validator.ValidateAsync(LegalEntityId, cts.Token));
    }

    private static MdmLegalEntityReferenceValidator CreateValidator(HttpResponseMessage response) =>
        CreateValidator(_ => response);

    private static MdmLegalEntityReferenceValidator CreateValidator(Func<HttpRequestMessage, HttpResponseMessage> responseFactory) =>
        CreateValidator((request, _) => Task.FromResult(responseFactory(request)));

    private static MdmLegalEntityReferenceValidator CreateValidator(Func<HttpRequestMessage, CancellationToken, Task<HttpResponseMessage>> responseFactory)
    {
        var httpClient = new HttpClient(new StubHttpMessageHandler(responseFactory));
        var options = Options.Create(new MdmServiceOptions { BaseUrl = "http://mdm.local" });
        var accessor = new HttpContextAccessor
        {
            HttpContext = new DefaultHttpContext()
        };
        accessor.HttpContext.Request.Headers.Authorization = "Bearer token";

        return new MdmLegalEntityReferenceValidator(httpClient, options, accessor);
    }

    private static HttpResponseMessage JsonResponse(string json) =>
        new(HttpStatusCode.OK)
        {
            Content = new StringContent(json, Encoding.UTF8, "application/json")
        };

    private sealed class StubHttpMessageHandler : HttpMessageHandler
    {
        private readonly Func<HttpRequestMessage, CancellationToken, Task<HttpResponseMessage>> _responseFactory;

        public StubHttpMessageHandler(Func<HttpRequestMessage, CancellationToken, Task<HttpResponseMessage>> responseFactory)
        {
            _responseFactory = responseFactory;
        }

        protected override Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken cancellationToken) =>
            _responseFactory(request, cancellationToken);
    }
}
