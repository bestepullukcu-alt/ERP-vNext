using System.Net.Http.Headers;
using System.Security.Claims;
using Diten.MdmService.Application.Contracts.Workflow;
using Microsoft.AspNetCore.Http;

namespace Diten.MdmService.Infrastructure.Workflow;

public sealed class HttpContextProductIdentityDelegatedTokenAccessor : IProductIdentityDelegatedTokenAccessor
{
    private readonly IHttpContextAccessor _contextAccessor;

    public HttpContextProductIdentityDelegatedTokenAccessor(IHttpContextAccessor contextAccessor) =>
        _contextAccessor = contextAccessor;

    public string GetRequiredToken()
    {
        var context = _contextAccessor.HttpContext;
        var authorization = context?.Request.Headers.Authorization;
        if (context?.User.Identity?.IsAuthenticated != true || authorization is null || authorization.Value.Count != 1
            || context.Request.Headers.ContainsKey("X-Delegated-Authorization")
            || context.Request.Headers.ContainsKey("X-Tenant-Id")
            || context.User.Claims.Any(x => x.Type == "actor_type" && string.Equals(x.Value, "service", StringComparison.Ordinal)))
            throw new ProductIdentityDelegatedTokenException("PRODUCT_WORKFLOW_DELEGATED_IDENTITY_INVALID");

        var subjects = context.User.Claims
            .Where(x => x.Type is "sub" or ClaimTypes.NameIdentifier)
            .Select(x => x.Value)
            .Distinct(StringComparer.Ordinal)
            .ToArray();
        var rawAuthorization = authorization.Value[0] ?? string.Empty;
        if (subjects.Length != 1 || !Guid.TryParseExact(subjects[0], "D", out var subjectId) || subjectId == Guid.Empty
            || !rawAuthorization.StartsWith("Bearer ", StringComparison.Ordinal)
            || rawAuthorization.Length <= "Bearer ".Length
            || rawAuthorization["Bearer ".Length..].Any(char.IsWhiteSpace)
            || !AuthenticationHeaderValue.TryParse(rawAuthorization, out var parsed)
            || !string.Equals(parsed.Scheme, "Bearer", StringComparison.Ordinal)
            || string.IsNullOrWhiteSpace(parsed.Parameter) || parsed.Parameter.Length > 16 * 1024
            || !string.Equals(parsed.Parameter, parsed.Parameter.Trim(), StringComparison.Ordinal))
            throw new ProductIdentityDelegatedTokenException("PRODUCT_WORKFLOW_DELEGATED_IDENTITY_INVALID");

        return parsed.Parameter;
    }
}
