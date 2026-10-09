using System.Text;
using Diten.InventoryService.Application.Common;
using Diten.InventoryService.Application.Features.Inventory.Services;
using Diten.InventoryService.Application.Features.StockPresence;
using Diten.InventoryService.Domain.Entities;
using Diten.InventoryService.Domain.Repositories;
using Diten.InventoryService.Infrastructure.InternalAccess;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.Filters;

namespace Diten.InventoryService.Api.InternalAccess;

/// <summary>
/// Opens an internal operation to a service key only (WP-0173-PM2, INVENTORY-BUNDLE 1.1.0 security conditions). Runs as
/// the action's authorization filter, before any parameter is read:
/// <list type="bullet">
/// <item>Outside Development the call must be https (else refused + a warning log).</item>
/// <item>A request that carries <c>Authorization</c> (a user token) is refused: this operation is never reachable with a
/// user token, alone or next to a key.</item>
/// <item><c>X-Internal-Api-Key</c> (exactly one value) must authenticate AND list this operation
/// (<see cref="InternalServiceKeyAuthenticator"/>).</item>
/// </list>
/// Every refusal is the SAME 401 with the SAME body, byte for byte (<see cref="RefusalBody"/> — no code, no correlation id;
/// the id is in the <c>X-Correlation-Id</c> response header and the audit row). The reason is logged, the secret never.
/// Refusals are audited too (key identifier only when it is a configured one).
/// </summary>
public sealed class InternalServiceKeyAttribute : TypeFilterAttribute
{
    public InternalServiceKeyAttribute(string operationId) : base(typeof(InternalServiceKeyFilter))
        => Arguments = [operationId];
}

public sealed class InternalServiceKeyFilter : IAsyncAuthorizationFilter
{
    /// <summary>The configured identifier of the key that opened the call, for the handler's audit row.</summary>
    public const string KeyIdentifierItem = "Inventory.InternalServiceKey";

    public static readonly string RefusalBody = $"{{\"error\":{{\"message\":\"Unauthorized.\"}},\"contractVersion\":\"{InventoryContract.Version}\"}}";

    private readonly string _operationId;
    private readonly InternalServiceKeyAuthenticator _authenticator;
    private readonly IInternalAccessAuditWriter _audit;
    private readonly ICorrelationContext _correlation;
    private readonly IHostEnvironment _environment;
    private readonly ILogger<InternalServiceKeyFilter> _logger;

    public InternalServiceKeyFilter(
        string operationId,
        InternalServiceKeyAuthenticator authenticator,
        IInternalAccessAuditWriter audit,
        ICorrelationContext correlation,
        IHostEnvironment environment,
        ILogger<InternalServiceKeyFilter> logger)
    {
        _operationId = operationId;
        _authenticator = authenticator;
        _audit = audit;
        _correlation = correlation;
        _environment = environment;
        _logger = logger;
    }

    public async Task OnAuthorizationAsync(AuthorizationFilterContext context)
    {
        var request = context.HttpContext.Request;
        var keyValues = request.Headers[StockPresenceContract.KeyHeader];
        var check = _authenticator.Check(keyValues.Count == 1 ? keyValues[0] : null, _operationId);

        var reason =
            !_environment.IsDevelopment() && !request.IsHttps ? "not https"
            : request.Headers.ContainsKey("Authorization") ? "a user token was sent"
            : keyValues.Count == 0 ? "no key"
            : check.KeyIdentifier is null ? "unknown key"
            : !check.Allowed ? "the key does not open this operation"
            : null;

        if (reason is null)
        {
            context.HttpContext.Items[KeyIdentifierItem] = check.KeyIdentifier;
            return;
        }

        if (reason == "not https")
        {
            _logger.LogWarning("Internal operation {Operation} refused: the call is not https. Correlation={CorrelationId}", _operationId, _correlation.CorrelationId);
        }
        else
        {
            _logger.LogWarning("Internal operation {Operation} refused: {Reason}. Key={KeyIdentifier} Correlation={CorrelationId}",
                _operationId, reason, check.KeyIdentifier, _correlation.CorrelationId);
        }

        await AuditRefusalAsync(context.HttpContext, check.KeyIdentifier);
        context.Result = new ContentResult
        {
            StatusCode = StatusCodes.Status401Unauthorized,
            Content = RefusalBody,
            ContentType = "application/json"
        };
    }

    private async Task AuditRefusalAsync(HttpContext http, string? keyIdentifier)
    {
        var tenant = http.Request.Headers[InventoryContract.TenantHeader];
        var sku = http.Request.Query["skuId"];
        var level = http.Request.Query["skuLevel"];
        try
        {
            await _audit.WriteAsync(new InternalServiceAccessAuditRecord
            {
                Action = InternalServiceAccessAuditRecord.StockPresenceAction,
                KeyIdentifier = keyIdentifier,
                TenantId = tenant.Count == 1 && WireParsing.TryParseUuid(tenant[0], out var t) ? t : null,
                SkuId = sku.Count == 1 && WireParsing.TryParseUuid(sku[0], out var s) ? s : null,
                SkuLevel = level.Count == 1 && StockPresenceContract.TryParseLevel(level[0], out _) ? level[0] : null,
                StatusCode = StatusCodes.Status401Unauthorized,
                Outcome = "unauthorized",
                CorrelationId = _correlation.CorrelationId,
                OccurredAt = DateTimeOffset.UtcNow
            }, http.RequestAborted);
        }
        catch (Exception ex) when (ex is not OperationCanceledException)
        {
            _logger.LogError(ex, "Internal operation refusal could not be audited. Correlation={CorrelationId}", _correlation.CorrelationId);
        }
    }

    /// <summary>The refusal body as bytes (tests compare refusals byte for byte).</summary>
    public static byte[] RefusalBytes => Encoding.UTF8.GetBytes(RefusalBody);
}
