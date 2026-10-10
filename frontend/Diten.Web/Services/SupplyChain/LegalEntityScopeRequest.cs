using Microsoft.AspNetCore.Http;

namespace Diten.Web.Services.SupplyChain;

/// <summary>
/// R-2 (PR #134, SHIPMENT-BUNDLE 3.2.0). LegalEntityId no longer comes from the token: the user picks it on the
/// page and the browser sends it as a query parameter, which the five SupplyChain adapters forward downstream as
/// the <c>X-Legal-Entity-Id</c> header the contract declares on all 17 operations.
///
/// One implementation rather than five copies. The adapters each carry their own private
/// <c>TryResolveScopeClaim</c>, and duplicating a security-relevant parse five times is how the two sides of a
/// rule drift apart — tenant isolation is the thing being parsed here, and CLAUDE.md records that a breach of it
/// is silent.
/// </summary>
public static class LegalEntityScopeRequest
{
    public const string QueryKey = "legalEntityId";

    /// <summary>
    /// True only when the query carries exactly one <c>legalEntityId</c> and it parses as a non-empty UUID.
    /// Repeated or malformed values are a request fault, not an authorization answer, so callers answer 400 —
    /// the same status the service's own middleware gives a missing or malformed <c>X-Legal-Entity-Id</c>.
    /// </summary>
    public static bool TryResolve(HttpRequest request, out Guid legalEntityId)
    {
        legalEntityId = Guid.Empty;
        if (!request.Query.TryGetValue(QueryKey, out var values) || values.Count != 1)
        {
            return false;
        }

        return Guid.TryParse(values[0], out legalEntityId) && legalEntityId != Guid.Empty;
    }
}
