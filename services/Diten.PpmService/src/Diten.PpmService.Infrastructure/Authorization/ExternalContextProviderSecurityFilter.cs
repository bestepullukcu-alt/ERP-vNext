using System.Security.Claims;
using System.Security.Cryptography;
using System.Text;
using Diten.PpmService.Application.Features.ExternalContextReferences;
using Microsoft.AspNetCore.Authentication;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.Filters;
using Microsoft.Extensions.Options;

namespace Diten.PpmService.Infrastructure.Authorization;


public sealed class ExternalContextProviderSecurityFilter(IOptions<ExternalContextProviderOptions> options)
    : IAsyncResourceFilter
{
    public async Task OnResourceExecutionAsync(
        ResourceExecutingContext context,
        ResourceExecutionDelegate next)
    {
        var configured = options.Value;
        if (!configured.Enabled || string.IsNullOrEmpty(configured.ServiceCredential))
        {
            context.Result = new StatusCodeResult(StatusCodes.Status503ServiceUnavailable);
            return;
        }

        var request = context.HttpContext.Request;
        if (!HasExactHeader(request, ExternalContextProviderOptions.ConsumerHeader,
                ExternalContextProviderOptions.AllowedConsumer) ||
            !HasFixedTimeCredential(request, configured.ServiceCredential))
        {
            context.Result = new UnauthorizedResult();
            return;
        }

        var authentication = await context.HttpContext.AuthenticateAsync(
            "Bearer");
        if (!authentication.Succeeded || authentication.Principal is null ||
            !TryResolveStrictContext(authentication.Principal, out _, out _))
        {
            context.Result = new UnauthorizedResult();
            return;
        }

        context.HttpContext.User = authentication.Principal;
        await next();
    }

    public static bool TryResolveStrictContext(
        ClaimsPrincipal principal,
        out Guid tenantId,
        out Guid actorId)
    {
        tenantId = Guid.Empty;
        actorId = Guid.Empty;
        if (principal.Identity?.IsAuthenticated != true ||
            !TryNonEmptyGuid(principal.FindFirst("tenant_id")?.Value, out tenantId))
        {
            return false;
        }

        var subject = principal.FindFirst("sub");
        if (subject is not null)
        {
            return TryNonEmptyGuid(subject.Value, out actorId);
        }

        return TryNonEmptyGuid(principal.FindFirst(ClaimTypes.NameIdentifier)?.Value, out actorId);
    }

    private static bool HasExactHeader(HttpRequest request, string name, string expected) =>
        request.Headers.TryGetValue(name, out var values) && values.Count == 1 &&
        string.Equals(values[0], expected, StringComparison.Ordinal);

    private static bool HasFixedTimeCredential(HttpRequest request, string expected)
    {
        if (!request.Headers.TryGetValue(ExternalContextProviderOptions.CredentialHeader, out var values) ||
            values.Count != 1)
        {
            return false;
        }

        var actualBytes = Encoding.UTF8.GetBytes(values[0]!);
        var expectedBytes = Encoding.UTF8.GetBytes(expected);
        return actualBytes.Length == expectedBytes.Length &&
               CryptographicOperations.FixedTimeEquals(actualBytes, expectedBytes);
    }

    private static bool TryNonEmptyGuid(string? value, out Guid parsed) =>
        Guid.TryParse(value, out parsed) && parsed != Guid.Empty;
}
