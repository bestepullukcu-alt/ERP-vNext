using System.Security.Claims;
using Diten.MdmService.Application.Common;
using Diten.MdmService.Application.Contracts;
using Microsoft.AspNetCore.Http;

namespace Diten.MdmService.Infrastructure.Security;

public sealed class ProductAbbreviationActorContext : IProductAbbreviationActorContext
{
    private readonly IHttpContextAccessor _httpContextAccessor;
    private readonly ITenantContext _tenantContext;

    public ProductAbbreviationActorContext(
        IHttpContextAccessor httpContextAccessor,
        ITenantContext tenantContext)
    {
        _httpContextAccessor = httpContextAccessor;
        _tenantContext = tenantContext;
    }

    public Guid TenantId => _tenantContext.TenantId;
    public bool TenantIsResolved => _tenantContext.IsResolved;
    public bool IsAuthenticated => Principal?.Identity?.IsAuthenticated == true;

    public string ActorType
        => Principal?.FindFirstValue("actor_type")
           ?? Principal?.FindFirstValue("actorType")
           ?? string.Empty;

    public string CanonicalHumanSubjectId
    {
        get
        {
            var nameIdentifier = Principal?.FindFirstValue(ClaimTypes.NameIdentifier)?.Trim();
            var subject = Principal?.FindFirstValue("sub")?.Trim();

            if (string.IsNullOrEmpty(nameIdentifier) && string.IsNullOrEmpty(subject))
            {
                return string.Empty;
            }

            Guid? nameIdentifierGuid = null;
            if (!string.IsNullOrEmpty(nameIdentifier))
            {
                if (!Guid.TryParse(nameIdentifier, out var parsedNameIdentifier))
                {
                    return string.Empty;
                }

                nameIdentifierGuid = parsedNameIdentifier;
            }

            Guid? subjectGuid = null;
            if (!string.IsNullOrEmpty(subject))
            {
                if (!Guid.TryParse(subject, out var parsedSubject))
                {
                    return string.Empty;
                }

                subjectGuid = parsedSubject;
            }

            if (nameIdentifierGuid.HasValue
                && subjectGuid.HasValue
                && nameIdentifierGuid.Value != subjectGuid.Value)
            {
                return string.Empty;
            }

            return (nameIdentifierGuid ?? subjectGuid)!.Value.ToString("D");
        }
    }

    public IReadOnlySet<string> GrantedPermissions
        => Principal?.Claims
               .Where(claim => claim.Type is "permission" or "permissions")
               .SelectMany(claim => claim.Value.Split(
                   [',', ' ', ';'],
                   StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries))
               .ToHashSet(StringComparer.Ordinal)
           ?? new HashSet<string>(StringComparer.Ordinal);

    public string CorrelationId
        => Principal?.FindFirstValue("correlation_id")
           ?? _httpContextAccessor.HttpContext?.TraceIdentifier
           ?? string.Empty;

    private ClaimsPrincipal? Principal => _httpContextAccessor.HttpContext?.User;
}
