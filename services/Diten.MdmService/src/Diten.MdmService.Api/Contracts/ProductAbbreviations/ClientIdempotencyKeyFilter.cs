using Diten.Shared.Core;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.Filters;

namespace Diten.MdmService.Api.Contracts.ProductAbbreviations;

/// <summary>
/// WP-MDM-ABB-ACCEPT-01 FIX2 A5 — a client-chosen <c>Idempotency-Key</c> may not contain ':'. The server derives keys
/// from a caller's key with ':'-separated suffixes (":former", ":replacement", the Task Center's "abb-wc:…"); a caller
/// able to send such a key could name a key the server derives from another command. Refused before any handler runs,
/// with a stable code (400 ABBREVIATION_IDEMPOTENCY_KEY_INVALID).
/// </summary>
[AttributeUsage(AttributeTargets.Class | AttributeTargets.Method)]
public sealed class ClientIdempotencyKeyFilterAttribute : ActionFilterAttribute
{
    public const string HeaderName = "Idempotency-Key";
    public const string InvalidCode = "ABBREVIATION_IDEMPOTENCY_KEY_INVALID";

    public override void OnActionExecuting(ActionExecutingContext context)
    {
        if (context.HttpContext.Request.Headers.TryGetValue(HeaderName, out var values)
            && values.Any(value => value is not null && value.Contains(':', StringComparison.Ordinal)))
        {
            context.Result = new ObjectResult(Response<object>.Fail(InvalidCode, 400)) { StatusCode = 400 };
        }
    }
}
