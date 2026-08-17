using System.Net;
using System.Text;
using Diten.Platform.Infrastructure.Services.Auth;
using Diten.Platform.Infrastructure.Settings;
using Microsoft.AspNetCore.Http;
using Microsoft.Extensions.Options;
using Xunit;

namespace Diten.Platform.Application.Tests.TenantOrganization;

// MOD-0288-FU01 focused tests. Mirrors MdmLegalEntityReferenceValidatorTests.
// Note: X-Tenant-Id propagation is handled by the shared TenantPropagationHandler (registered in DI)
// and is covered by its own behavior, not by this validator (which uses a bare HttpClient in unit tests).
public sealed class AuthServiceUserReferenceValidatorTests
{
    private static readonly Guid UserId = Guid.Parse("550e8400-e29b-41d4-a716-446655440000");

    [Fact]
    public async Task Validate_returns_success_for_matching_referenceable_user()
    {
        var validator = CreateValidator(JsonResponse($$"""
            {"data":{"userId":"{{UserId}}","referenceable":true},"statusCode":200,"isSuccessful":true,"errors":[]}
            """));

        var response = await validator.ValidateAsync(UserId);

        Assert.True(response.IsSuccessful);
        Assert.Equal(UserId, response.Data!.UserId);
        Assert.True(response.Data.Referenceable);
    }

    [Fact]
    public async Task Validate_fails_closed_on_http_non_success()
    {
        var validator = CreateValidator(new HttpResponseMessage(HttpStatusCode.NotFound)
        {
            Content = new StringContent("""{"data":null,"statusCode":404,"isSuccessful":false,"errors":["Not found"]}""", Encoding.UTF8, "application/json")
        });

        var response = await validator.ValidateAsync(UserId);

        Assert.False(response.IsSuccessful);
        Assert.Equal(404, response.StatusCode);
    }

    [Fact]
    public async Task Validate_fails_closed_on_network_exception()
    {
        var validator = CreateValidator(_ => throw new HttpRequestException("network down"));

        var response = await validator.ValidateAsync(UserId);

        Assert.False(response.IsSuccessful);
        Assert.Equal(404, response.StatusCode);
    }

    [Fact]
    public async Task Validate_fails_closed_on_timeout()
    {
        var validator = CreateValidator(_ => throw new TaskCanceledException("timeout"));

        var response = await validator.ValidateAsync(UserId);

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

        var response = await validator.ValidateAsync(UserId);

        Assert.False(response.IsSuccessful);
        Assert.Equal(404, response.StatusCode);
    }

    [Fact]
    public async Task Validate_fails_closed_on_null_payload()
    {
        var validator = CreateValidator(JsonResponse("""{"data":null,"statusCode":200,"isSuccessful":true,"errors":[]}"""));

        var response = await validator.ValidateAsync(UserId);

        Assert.False(response.IsSuccessful);
        Assert.Equal(404, response.StatusCode);
    }

    [Fact]
    public async Task Validate_fails_closed_on_user_id_mismatch()
    {
        var otherId = Guid.NewGuid();
        var validator = CreateValidator(JsonResponse($$"""
            {"data":{"userId":"{{otherId}}","referenceable":true},"statusCode":200,"isSuccessful":true,"errors":[]}
            """));

        var response = await validator.ValidateAsync(UserId);

        Assert.False(response.IsSuccessful);
        Assert.Equal(404, response.StatusCode);
    }

    [Fact]
    public async Task Validate_fails_closed_on_referenceable_false()
    {
        var validator = CreateValidator(JsonResponse($$"""
            {"data":{"userId":"{{UserId}}","referenceable":false},"statusCode":200,"isSuccessful":true,"errors":[]}
            """));

        var response = await validator.ValidateAsync(UserId);

        Assert.False(response.IsSuccessful);
        Assert.Equal(404, response.StatusCode);
    }

    [Fact]
    public async Task Validate_fails_closed_when_envelope_not_successful()
    {
        var validator = CreateValidator(JsonResponse($$"""
            {"data":{"userId":"{{UserId}}","referenceable":true},"statusCode":404,"isSuccessful":false,"errors":["Not found"]}
            """));

        var response = await validator.ValidateAsync(UserId);

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

        await Assert.ThrowsAsync<TaskCanceledException>(() => validator.ValidateAsync(UserId, cts.Token));
    }

    [Fact]
    public async Task Validate_forwards_bearer_token()
    {
        HttpRequestMessage? captured = null;
        var validator = CreateValidator((request, _) =>
        {
            captured = request;
            return Task.FromResult(JsonResponse($$"""
                {"data":{"userId":"{{UserId}}","referenceable":true},"statusCode":200,"isSuccessful":true,"errors":[]}
                """));
        });

        await validator.ValidateAsync(UserId);

        Assert.NotNull(captured);
        Assert.NotNull(captured!.Headers.Authorization);
        Assert.Equal("Bearer", captured.Headers.Authorization!.Scheme);
    }

    private static AuthServiceUserReferenceValidator CreateValidator(HttpResponseMessage response) =>
        CreateValidator(_ => response);

    private static AuthServiceUserReferenceValidator CreateValidator(Func<HttpRequestMessage, HttpResponseMessage> responseFactory) =>
        CreateValidator((request, _) => Task.FromResult(responseFactory(request)));

    private static AuthServiceUserReferenceValidator CreateValidator(Func<HttpRequestMessage, CancellationToken, Task<HttpResponseMessage>> responseFactory)
    {
        var httpClient = new HttpClient(new StubHttpMessageHandler(responseFactory));
        var options = Options.Create(new AuthServiceOptions { BaseUrl = "http://auth.local" });
        var accessor = new HttpContextAccessor
        {
            HttpContext = new DefaultHttpContext()
        };
        accessor.HttpContext.Request.Headers.Authorization = "Bearer token";

        return new AuthServiceUserReferenceValidator(httpClient, options, accessor);
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
