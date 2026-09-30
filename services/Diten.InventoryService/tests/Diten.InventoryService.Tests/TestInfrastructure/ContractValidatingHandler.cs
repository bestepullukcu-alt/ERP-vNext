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
    private static readonly Dictionary<(string Method, string Path), HashSet<int>> AllowedStatuses = new()
    {
        [("POST", "/api/inventory/movements")] = [200, 201, 400, 401, 403, 422, 503],
        [("GET", "/api/inventory/availability")] = [200, 400, 401, 403, 404],
        [("GET", "/api/inventory/balance")] = [200, 400, 401, 403],
        [("GET", "/api/inventory/transactions")] = [200, 400, 401, 403]
    };

    private static readonly Dictionary<(string Method, string Path), string> SuccessSchema = new()
    {
        [("POST", "/api/inventory/movements")] = ContractSchemas.MovementResponse,
        [("GET", "/api/inventory/availability")] = ContractSchemas.AvailabilityView,
        [("GET", "/api/inventory/balance")] = ContractSchemas.BalancePage,
        [("GET", "/api/inventory/transactions")] = ContractSchemas.TransactionPage
    };

    /// <summary>How many responses were validated per operation + status (reported by the conformance test).</summary>
    public static readonly ConcurrentDictionary<string, int> Validated = new(StringComparer.Ordinal);

    protected override async Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken cancellationToken)
    {
        var response = await base.SendAsync(request, cancellationToken);
        var key = (request.Method.Method, request.RequestUri!.AbsolutePath);
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
