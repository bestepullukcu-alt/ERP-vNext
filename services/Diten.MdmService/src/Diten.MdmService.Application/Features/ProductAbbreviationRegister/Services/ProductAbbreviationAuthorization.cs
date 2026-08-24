using Diten.MdmService.Application.Contracts;

namespace Diten.MdmService.Application.Features.ProductAbbreviationRegister.Services;

public static class ProductAbbreviationPermissions
{
    public const string Read = "mdm.product-abbreviations.read";
    public const string Request = "mdm.product-abbreviations.request";
    public const string Cancel = "mdm.product-abbreviations.cancel";
    public const string Approve = "mdm.product-abbreviations.approve";
    public const string Reject = "mdm.product-abbreviations.reject";
    public const string Correct = "mdm.product-abbreviations.correct";
    public const string Retire = "mdm.product-abbreviations.retire";
    public const string Audit = "mdm.product-abbreviations.audit";

    public static readonly IReadOnlySet<string> All = new HashSet<string>(StringComparer.Ordinal)
    {
        Read, Request, Cancel, Approve, Reject, Correct, Retire, Audit
    };
}

public sealed record ProductAbbreviationAuthorizationResult(bool Succeeded, string? ErrorCode, int StatusCode)
{
    public static ProductAbbreviationAuthorizationResult Success() => new(true, null, 200);
}

public sealed class ProductAbbreviationAuthorization
{
    private readonly IProductAbbreviationActorContext _context;

    public ProductAbbreviationAuthorization(IProductAbbreviationActorContext context)
    {
        _context = context;
    }

    public ProductAbbreviationAuthorizationResult Demand(string permission)
    {
        if (!_context.IsAuthenticated)
        {
            return new(false, "ABBREVIATION_ACTOR_UNAUTHENTICATED", 401);
        }

        if (!_context.TenantIsResolved || _context.TenantId == Guid.Empty)
        {
            return new(false, "ABBREVIATION_TENANT_CONTEXT_UNTRUSTED", 403);
        }

        if (!string.Equals(_context.ActorType, "tenant_user", StringComparison.Ordinal)
            || string.IsNullOrWhiteSpace(_context.CanonicalHumanSubjectId))
        {
            return new(false, "ABBREVIATION_ACTOR_NOT_DIRECT_TENANT_HUMAN", 403);
        }

        return ProductAbbreviationPermissions.All.Contains(permission)
               && _context.GrantedPermissions.Contains(permission)
            ? ProductAbbreviationAuthorizationResult.Success()
            : new(false, "ABBREVIATION_PERMISSION_DENIED", 403);
    }
}
