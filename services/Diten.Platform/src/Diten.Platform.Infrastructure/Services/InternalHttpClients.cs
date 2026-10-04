using Diten.Platform.Application.Features.TenantOrganization.Services;
using Diten.Platform.Application.Features.DocumentManagementApproval.Services;
using Diten.Platform.Infrastructure.Services.Auth;
using Microsoft.Extensions.DependencyInjection;

namespace Diten.Platform.Infrastructure.Services;

/// <summary>
/// BL-454 — the ONE way a Platform client that carries a credential to AuthService is built: it never follows a
/// redirect.
///
/// <para>The internal API key travels in a custom header (<c>X-Internal-Api-Key</c>). HttpClient drops the
/// Authorization header on a redirect but never a custom one, so a client that followed a 3xx would hand the key to
/// whatever host the answer named. A redirect is therefore an answer like any other non-success: the caller sees it and
/// goes no further. Same rule, same shape as AuthService's own <c>InternalHttpClients</c>.</para>
/// </summary>
public static class InternalHttpClients
{
    /// <summary>The named client every Platform → AuthService call with the internal key asks the factory for.</summary>
    public const string AuthInternal = "auth-internal";

    public static HttpMessageHandler CreatePrimaryHandler() => new SocketsHttpHandler { AllowAutoRedirect = false };

    public static IHttpClientBuilder WithoutRedirects(this IHttpClientBuilder builder) =>
        builder.ConfigurePrimaryHttpMessageHandler(CreatePrimaryHandler);

    /// <summary>
    /// Every client that calls AuthService with a credential: the named <see cref="AuthInternal"/> client (the
    /// factory-built callers) and the two typed ones (the approval role directory carries the internal key, the user
    /// reference validator the caller's own bearer).
    /// </summary>
    public static IServiceCollection AddAuthInternalHttpClients(this IServiceCollection services)
    {
        services.AddHttpClient(AuthInternal).WithoutRedirects();
        // The display-name client keeps its own named client for its short timeout (ATT-FIX2); it carries the internal
        // key too, so it is registered here without redirects as well.
        AuthUserDisplayNameClient.AddAuthDisplayNameHttpClient(services);
        services.AddHttpClient<IUserReferenceValidator, AuthServiceUserReferenceValidator>().WithoutRedirects();
        services.AddHttpClient<IApprovalRoleDirectory, AuthServiceApprovalRoleDirectory>().WithoutRedirects();
        return services;
    }
}
