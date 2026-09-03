using System.Security.Claims;
using System.Text.Json;
using Diten.MdmService.Application.Common;
using Diten.MdmService.Application.Features.ProductItemSkuMaster.Lifecycle.OrphanedOperationRecovery;
using Diten.MdmService.Application.Features.ProductItemSkuMaster.Lifecycle.OrphanedOperationRecovery.Commands;
using Diten.MdmService.Infrastructure.Authorization;
using Diten.Shared.Core;
using MediatR;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace Diten.MdmService.Api.Controllers;

[Authorize]
[ApiController]
[Route("api/product-identity-workflow-operations")]
public sealed class ProductIdentityWorkflowOperationsController : CustomBaseController
{
    private const string TenantUserActorType = "tenant_user";
    private static readonly string[] SubmitPermissions =
    [
        "mdm.global-products.submit",
        "mdm.gskus.submit",
        "mdm.lskus.submit",
        "mdm.finished-goods.submit"
    ];
    private readonly IMediator _mediator;
    private readonly ITenantContext _tenantContext;

    public ProductIdentityWorkflowOperationsController(
        IMediator mediator,
        ITenantContext tenantContext)
    {
        _mediator = mediator ?? throw new ArgumentNullException(nameof(mediator));
        _tenantContext = tenantContext ?? throw new ArgumentNullException(nameof(tenantContext));
    }

    [HttpPost("{operationId:guid}/recover-before-start")]
    [HasPermission(ProductIdentityWorkflowOperationRecoveryPermissions.Recover)]
    public async Task<IActionResult> RecoverBeforeStart(
        Guid operationId,
        [FromBody] JsonElement request,
        CancellationToken cancellationToken)
    {
        if (!TryValidateExactCaller(out _)
            || !TryValidateRouteOperationId(operationId)
            || !TryParseBody(request, out var parsedRequest))
        {
            return InvalidRequest();
        }

        if (!TryGetCommandId(out var commandId))
        {
            return InvalidIdempotencyKey();
        }

        var command = new RecoverOrphanedProductIdentityWorkflowOperationCommand(
            operationId,
            commandId,
            parsedRequest!);

        return CreateActionResultInstance(await _mediator.Send(command, cancellationToken));
    }

    private bool TryGetCommandId(out Guid commandId)
    {
        commandId = Guid.Empty;
        var values = Request.Headers["Idempotency-Key"];
        return values.Count == 1
            && TryCanonicalGuid(values[0], out commandId);
    }

    private bool TryValidateExactCaller(out Guid tenantId)
    {
        tenantId = Guid.Empty;
        var principal = User;
        if (principal.Identity?.IsAuthenticated != true)
        {
            return false;
        }

        var actorTypes = principal.Claims
            .Where(claim => string.Equals(claim.Type, "actor_type", StringComparison.OrdinalIgnoreCase))
            .ToArray();
        if (actorTypes.Length != 1
            || !string.Equals(actorTypes[0].Type, "actor_type", StringComparison.Ordinal)
            || !string.Equals(actorTypes[0].Value, TenantUserActorType, StringComparison.Ordinal)
            || Request.Headers.ContainsKey("X-Delegated-Authorization")
            || !TrySingleCanonicalGuidClaim(principal, "tenant_id", out tenantId)
            || !TrySingleCanonicalGuidClaim(principal, "sub", out var subjectId)
            || !HasNoConflictingNameIdentifier(principal, subjectId)
            || !HasExactPermissionClaims(principal)
            || !_tenantContext.IsResolved
            || _tenantContext.TenantId != tenantId)
        {
            return false;
        }

        var tenantHeaders = Request.Headers["X-Tenant-Id"];
        if (tenantHeaders.Count == 0)
        {
            return true;
        }

        return tenantHeaders.Count == 1
            && TryCanonicalGuid(tenantHeaders[0], out var headerTenantId)
            && headerTenantId == tenantId;
    }

    private bool TryValidateRouteOperationId(Guid operationId)
    {
        if (operationId == Guid.Empty)
        {
            return false;
        }

        // ASP.NET supplies the raw route token. Direct unit invocation has no route value;
        // when the token exists it must be the same canonical D-GUID that model binding produced.
        if (!Request.RouteValues.TryGetValue("operationId", out var rawValue))
        {
            return true;
        }

        if (rawValue is not string rawOperationId
            || !TryCanonicalGuid(rawOperationId, out var routeOperationId))
        {
            return false;
        }

        return routeOperationId == operationId;
    }

    private static bool HasNoConflictingNameIdentifier(ClaimsPrincipal principal, Guid subjectId)
    {
        var claims = principal.Claims
            .Where(claim => string.Equals(
                claim.Type,
                ClaimTypes.NameIdentifier,
                StringComparison.OrdinalIgnoreCase))
            .ToArray();
        return claims.Length == 0
            || claims.Length == 1
            && string.Equals(claims[0].Type, ClaimTypes.NameIdentifier, StringComparison.Ordinal)
            && TryCanonicalGuid(claims[0].Value, out var nameIdentifier)
            && nameIdentifier == subjectId;
    }

    private static bool TrySingleCanonicalGuidClaim(
        ClaimsPrincipal principal,
        string claimType,
        out Guid value)
    {
        value = Guid.Empty;
        var claims = principal.Claims
            .Where(claim => string.Equals(claim.Type, claimType, StringComparison.OrdinalIgnoreCase))
            .ToArray();
        return claims.Length == 1
            && string.Equals(claims[0].Type, claimType, StringComparison.Ordinal)
            && TryCanonicalGuid(claims[0].Value, out value);
    }

    private static bool TryCanonicalGuid(string? value, out Guid result)
    {
        result = Guid.Empty;
        return Guid.TryParseExact(value, "D", out result)
            && result != Guid.Empty
            && string.Equals(value, result.ToString("D"), StringComparison.Ordinal);
    }

    private static bool HasExactPermissionClaims(ClaimsPrincipal principal)
    {
        var guarded = SubmitPermissions
            .Append(ProductIdentityWorkflowOperationRecoveryPermissions.Recover)
            .ToArray();
        var guardedClaims = principal.Claims
            .Where(claim => string.Equals(claim.Type, "permission", StringComparison.OrdinalIgnoreCase)
                || string.Equals(claim.Type, "permissions", StringComparison.OrdinalIgnoreCase))
            .SelectMany(claim => claim.Value.Split(
                    [',', ' ', ';'],
                    StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries)
                .Select(value => (claim.Type, Value: value)))
            .Where(candidate => guarded.Any(permission =>
                string.Equals(candidate.Value, permission, StringComparison.OrdinalIgnoreCase)))
            .ToArray();
        if (guardedClaims.Any(candidate =>
                !string.Equals(candidate.Type, "permission", StringComparison.Ordinal)
                || !guarded.Contains(candidate.Value, StringComparer.Ordinal)))
        {
            return false;
        }

        return guardedClaims.Count(claim => string.Equals(
            claim.Value,
            ProductIdentityWorkflowOperationRecoveryPermissions.Recover,
            StringComparison.Ordinal)) == 1;
    }

    private static bool TryParseBody(
        JsonElement body,
        out ProductIdentityWorkflowOperationRecoveryRequest? request)
    {
        request = null;
        if (body.ValueKind != JsonValueKind.Object)
        {
            return false;
        }

        JsonElement action = default;
        JsonElement operationVersion = default;
        JsonElement targetVersion = default;
        JsonElement reasonCode = default;
        JsonElement comment = default;
        var hasAction = false;
        var hasOperationVersion = false;
        var hasTargetVersion = false;
        var hasReasonCode = false;
        var hasComment = false;
        var names = new HashSet<string>(StringComparer.Ordinal);

        foreach (var property in body.EnumerateObject())
        {
            if (!names.Add(property.Name))
            {
                return false;
            }

            switch (property.Name)
            {
                case "action": action = property.Value; hasAction = true; break;
                case "expectedOperationVersion": operationVersion = property.Value; hasOperationVersion = true; break;
                case "expectedTargetVersion": targetVersion = property.Value; hasTargetVersion = true; break;
                case "reasonCode": reasonCode = property.Value; hasReasonCode = true; break;
                case "comment": comment = property.Value; hasComment = true; break;
                default: return false;
            }
        }

        if (!hasAction || !hasOperationVersion || !hasTargetVersion || !hasReasonCode
            || action.ValueKind != JsonValueKind.Number
            || !action.TryGetInt32(out var actionValue)
            || actionValue is not ((int)ProductIdentityWorkflowOperationRecoveryAction.Abandon)
                and not ((int)ProductIdentityWorkflowOperationRecoveryAction.Supersede)
            || operationVersion.ValueKind != JsonValueKind.Number
            || !operationVersion.TryGetInt32(out var expectedOperationVersion)
            || expectedOperationVersion is < 0 or int.MaxValue
            || reasonCode.ValueKind != JsonValueKind.String
            || !TryParseTargetVersions(targetVersion, out var versions)
            || hasComment && comment.ValueKind is not (JsonValueKind.Null or JsonValueKind.String))
        {
            return false;
        }

        var parsedReasonCode = reasonCode.GetString();
        var parsedComment = hasComment && comment.ValueKind == JsonValueKind.String
            ? comment.GetString()
            : null;
        if (!IsExactBoundedText(parsedReasonCode, 128, required: true)
            || !IsExactBoundedText(parsedComment, 512, required: false))
        {
            return false;
        }

        request = new ProductIdentityWorkflowOperationRecoveryRequest(
            (ProductIdentityWorkflowOperationRecoveryAction)actionValue,
            expectedOperationVersion,
            versions!,
            parsedReasonCode!,
            parsedComment);
        return true;
    }

    private static bool TryParseTargetVersions(
        JsonElement body,
        out ProductIdentityWorkflowTargetVersions? versions)
    {
        versions = null;
        if (body.ValueKind != JsonValueKind.Object)
        {
            return false;
        }

        var names = new HashSet<string>(StringComparer.Ordinal);
        var hasPrimary = false;
        var primary = 0;
        int? revision = null;
        foreach (var property in body.EnumerateObject())
        {
            if (!names.Add(property.Name))
            {
                return false;
            }

            switch (property.Name)
            {
                case "primaryEntityVersion" when property.Value.ValueKind == JsonValueKind.Number
                    && property.Value.TryGetInt32(out primary):
                    hasPrimary = true;
                    break;
                case "productDefinitionRevisionVersion" when property.Value.ValueKind == JsonValueKind.Null:
                    revision = null;
                    break;
                case "productDefinitionRevisionVersion" when property.Value.ValueKind == JsonValueKind.Number
                    && property.Value.TryGetInt32(out var parsedRevision):
                    revision = parsedRevision;
                    break;
                default:
                    return false;
            }
        }

        if (!hasPrimary
            || primary is < 0 or int.MaxValue
            || revision is < 0 or int.MaxValue)
        {
            return false;
        }

        versions = new ProductIdentityWorkflowTargetVersions(primary, revision);
        return true;
    }

    private static bool IsExactBoundedText(string? value, int maximumLength, bool required)
    {
        if (value is null)
        {
            return !required;
        }

        return (!required || value.Length > 0)
            && value.Length <= maximumLength
            && string.Equals(value, value.Trim(), StringComparison.Ordinal)
            && !value.Any(char.IsControl);
    }

    private IActionResult InvalidRequest() =>
        CreateActionResultInstance(
            Response<NoContent>.Fail("PRODUCT_IDENTITY_WORKFLOW_RECOVERY_REQUEST_INVALID", 400));

    private IActionResult InvalidIdempotencyKey() =>
        CreateActionResultInstance(Response<NoContent>.Fail("IDEMPOTENCY_KEY_INVALID", 400));

}
