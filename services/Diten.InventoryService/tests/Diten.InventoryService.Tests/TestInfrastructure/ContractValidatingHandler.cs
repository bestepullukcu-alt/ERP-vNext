using System.Collections.Concurrent;
using System.Net;

namespace Diten.InventoryService.Tests.TestInfrastructure;

/// <summary>
/// Sits in EVERY API test's HttpClient (AC15: "every response body validated automatically"). Each response is
/// checked for (1) a status the operation may return and (2) a body that validates against the frozen contract
/// schema for that operation + status. A violation throws, so the test that provoked it fails with the reason.
///
/// Allowed statuses = the contract's declared responses ∪ the refusals the module pack mandates on every endpoint
/// (401 no token and 403 permission / cross-scope — pack §13.4-5; 400 malformed filters, pack §12). Anything else —
/// a 500 above all — fails.
/// </summary>
public sealed class ContractValidatingHandler : DelegatingHandler
{
    private const string ReleasePath = "/api/inventory/reservations/{id}/release";

    private static readonly System.Text.RegularExpressions.Regex ReleaseRoute = new(@"^/api/inventory/reservations/[^/]+/release$");

    private static readonly Dictionary<(string Method, string Path), HashSet<int>> AllowedStatuses = new()
    {
        [("POST", "/api/inventory/movements")] = [200, 201, 400, 401, 403, 422, 503],
        // 503: pack ASSUMPTION-0173-18 (FU-S1-01) — no ledger row and PRODUCT-MASTER unreachable (extra status, as S1-05).
        [("GET", "/api/inventory/availability")] = [200, 400, 401, 403, 404, 503],
        [("GET", "/api/inventory/balance")] = [200, 400, 401, 403],
        [("GET", "/api/inventory/transactions")] = [200, 400, 401, 403],
        // S2. Contract declares 201 + 422 (create) and 200 + 409 (release); 200 replay, 400 (malformed / missing If-Match),
        // 401 / 403 and 404 (unknown / other-scope id) come from pack §8 / ASSUMPTION-0173-17 on every endpoint.
        [("POST", "/api/inventory/reservations")] = [200, 201, 400, 401, 403, 422],
        [("POST", ReleasePath)] = [200, 400, 401, 403, 404, 409]
    };

    private static readonly Dictionary<(string Method, string Path), string> SuccessSchema = new()
    {
        [("POST", "/api/inventory/movements")] = ContractSchemas.MovementResponse,
        [("GET", "/api/inventory/availability")] = ContractSchemas.AvailabilityView,
        [("GET", "/api/inventory/balance")] = ContractSchemas.BalancePage,
        [("GET", "/api/inventory/transactions")] = ContractSchemas.TransactionPage,
        [("POST", "/api/inventory/reservations")] = ContractSchemas.ReservationResponse,
        [("POST", ReleasePath)] = ContractSchemas.ReservationResponse
    };

    /// <summary>How many responses were validated per operation + status (reported by the conformance test).</summary>
    public static readonly ConcurrentDictionary<string, int> Validated = new(StringComparer.Ordinal);

    protected override async Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken cancellationToken)
    {
        var response = await base.SendAsync(request, cancellationToken);
        var path = ReleaseRoute.IsMatch(request.RequestUri!.AbsolutePath) ? ReleasePath : request.RequestUri!.AbsolutePath;
        var key = (request.Method.Method, AbsolutePath: path);
        if (!AllowedStatuses.TryGetValue(key, out var allowed))
        {
            return response; // e.g. /health, or a deliberately wrong route in a routing test
        }

        var status = (int)response.StatusCode;
        var body = await response.Content.ReadAsStringAsync(cancellationToken);

        if (!allowed.Contains(status))
        {
            throw new ContractViolationException($"{key.Method} {key.AbsolutePath} answered {status}, which the contract/pack does not allow. Body: {body}");
        }

        var schemaName = status is 200 or 201 ? SuccessSchema[key] : ContractSchemas.Error;
        var errors = ContractSchemas.Validate(schemaName, body);
        if (errors.Count > 0)
        {
            throw new ContractViolationException(
                $"{key.Method} {key.AbsolutePath} {status} body does not match '{schemaName}':\n  {string.Join("\n  ", errors)}\nBody: {body}");
        }

        if (!response.Headers.TryGetValues("X-Correlation-Id", out var correlation) || !Guid.TryParse(correlation.Single(), out _))
        {
            throw new ContractViolationException($"{key.Method} {key.AbsolutePath} {status} carries no UUID X-Correlation-Id header.");
        }

        Validated.AddOrUpdate($"{key.Method} {key.AbsolutePath} {status} → {schemaName}", 1, (_, n) => n + 1);

        // Hand the (already buffered) body back to the test.
        var content = new StringContent(body, System.Text.Encoding.UTF8, "application/json");
        foreach (var header in response.Content.Headers)
        {
            content.Headers.Remove(header.Key);
            content.Headers.TryAddWithoutValidation(header.Key, header.Value);
        }

        response.Content = content;
        return response;
    }
}
