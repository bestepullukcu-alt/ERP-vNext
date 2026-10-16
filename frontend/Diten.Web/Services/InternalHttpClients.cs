using Diten.Web.Services.Branding;
using Diten.Web.Services.TenantResolution;
using Diten.Web.Services.TenantStatus;

namespace Diten.Web.Services;

/// <summary>
/// BL-454 — the ONE way a Web client that carries the shared internal API key to Platform is built: it never follows
/// a redirect.
///
/// <para>The key travels in a custom header (<c>X-Internal-Api-Key</c>). HttpClient drops the Authorization header on
/// a redirect but never a custom one, so a client that followed a 3xx would hand the key to whatever host the answer
/// named. A redirect is an answer like any other non-success: each gateway already treats it as "no answer" and falls
/// back. Same rule, same shape as AuthService's and Platform's own <c>InternalHttpClients</c>.</para>
/// </summary>
public static class InternalHttpClients
{
    public static HttpMessageHandler CreatePrimaryHandler() => new SocketsHttpHandler { AllowAutoRedirect = false };

    public static IHttpClientBuilder WithoutRedirects(this IHttpClientBuilder builder) =>
        builder.ConfigurePrimaryHttpMessageHandler(CreatePrimaryHandler);

    /// <summary>The three pre-auth lookups that go to Platform directly with the internal key.</summary>
    public static IServiceCollection AddPlatformInternalClients(this IServiceCollection services, string platformServiceUrl)
    {
        // Pre-auth tenant branding lookup for the login screen. Targets the Platform service DIRECTLY
        // (the internal branding endpoint is not exposed through the gateway), authenticated with the
        // shared internal API key. Best-effort: failures fall back to platform default branding.
        services.AddHttpClient<IBrandingGateway, BrandingGateway>(client =>
        {
            client.BaseAddress = new Uri(platformServiceUrl);
        }).WithoutRedirects();
        // FIX-4: per-request tenant liveness lookup for the shell session guard (deleted/suspended tenant → sign-out).
        // Same Platform target + shared internal API key; best-effort/fail-open and short-cached (~30s) in the gateway.
        services.AddHttpClient<ITenantStatusGateway, TenantStatusGateway>(client =>
        {
            client.BaseAddress = new Uri(platformServiceUrl);
            client.Timeout = TimeSpan.FromSeconds(5);
        }).WithoutRedirects();
        // Vanity slug → tenant login redirect (e.g. http://<host>/gmg → /account/login?tenantId=...).
        // Targets the Platform service DIRECTLY with the shared internal API key (same pattern as branding).
        services.AddHttpClient<ITenantSlugResolver, TenantSlugResolver>(client =>
        {
            client.BaseAddress = new Uri(platformServiceUrl);
            client.Timeout = TimeSpan.FromSeconds(5);
        }).WithoutRedirects();
        return services;
    }
}
