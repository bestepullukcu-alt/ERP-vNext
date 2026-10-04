using Microsoft.Extensions.DependencyInjection;

namespace Diten.AuthService.Infrastructure.Services;

/// <summary>
/// BL-454 — the ONE way an AuthService client that carries the internal API key is built: it never follows a redirect.
///
/// <para>The key travels in a custom header (<c>X-Internal-Api-Key</c>). HttpClient drops the Authorization header on a
/// redirect but never a custom one, so a client that followed a 3xx would hand the key to whatever host the answer
/// named. A redirect is therefore an answer like any other non-success: the caller sees it and goes no further.</para>
/// </summary>
public static class InternalHttpClients
{
    public static HttpMessageHandler CreatePrimaryHandler() => new SocketsHttpHandler { AllowAutoRedirect = false };

    public static IHttpClientBuilder WithoutRedirects(this IHttpClientBuilder builder) =>
        builder.ConfigurePrimaryHttpMessageHandler(CreatePrimaryHandler);
}
