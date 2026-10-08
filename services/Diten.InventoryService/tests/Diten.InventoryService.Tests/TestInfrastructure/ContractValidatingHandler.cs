using System.Collections.Concurrent;
using System.Text.RegularExpressions;

namespace Diten.InventoryService.Tests.TestInfrastructure;

/// <summary>
/// Sits in EVERY API test's HttpClient (AC15: "every response body validated automatically"; MOD-0173-FU02 AC-15 for
/// LOCATION). Each response is checked for (1) a status the operation may return and (2) a body that validates against
/// the contract schema for that operation + status. A violation throws, so the test that provoked it fails with the
/// reason.
///
/// Allowed statuses = the contract's declared responses ∪ the refusals the module pack mandates on every endpoint
/// (401 no token and 403 permission / cross-scope — pack §13.4-5; 400 malformed filters, pack §12). Anything else —
/// a 500 above all — fails. LOCATION single-location answers must also carry <c>ETag: W/"n"</c> (FU02 §8).
/// </summary>
public sealed class ContractValidatingHandler : DelegatingHandler
{
    private const string Inventory = "inventory";
    private const string Location = "location";
    private const string Trace = "trace";
    private const string Quarantine = "quarantine";

    private sealed record Operation(
        string Method,
        string Template,
        Regex Path,
        string Contract,
        HashSet<int> Allowed,
        Func<int, string> SuccessSchema,
        bool ETagOnSuccess = false);

    private static readonly Operation[] Operations =
    [
        Inv("POST", "/api/inventory/movements", [200, 201, 400, 401, 403, 422, 503], ContractSchemas.MovementResponse),
        // 503: pack ASSUMPTION-0173-18 (FU-S1-01) — no ledger row and PRODUCT-MASTER unreachable (extra status, as S1-05).
        Inv("GET", "/api/inventory/availability", [200, 400, 401, 403, 404, 503], ContractSchemas.AvailabilityView),
        Inv("GET", "/api/inventory/balance", [200, 400, 401, 403], ContractSchemas.BalancePage),
        Inv("GET", "/api/inventory/transactions", [200, 400, 401, 403], ContractSchemas.TransactionPage),
        // S2. Contract declares 201 + 422 (create) and 200 + 409 (release); 200 replay, 400 (malformed / missing If-Match),
        // 401 / 403 and 404 (unknown / other-scope id) come from pack §8 / ASSUMPTION-0173-17 on every endpoint.
        Inv("POST", "/api/inventory/reservations", [200, 201, 400, 401, 403, 422], ContractSchemas.ReservationResponse),
        Inv("POST", "/api/inventory/reservations/{id}/release", [200, 400, 401, 403, 404, 409], ContractSchemas.ReservationResponse),

        // LOCATION v1 (frozen reads). 400 (scope member in the query, malformed filter) / 401 / 403 / 503 (company not
        // confirmable) are the MOD-0173 scope refusals every endpoint may give.
        Loc("GET", "/api/locations", [200, 400, 401, 403, 503], _ => ContractSchemas.LocationList),
        // Literal segments first: /search must not be taken for /{id}.
        Loc("GET", "/api/locations/search", [200, 400, 401, 403, 503], _ => ContractSchemas.LocationSearchPage),
        Loc("GET", "/api/locations/{id}", [200, 400, 401, 403, 404, 503], _ => ContractSchemas.LocationRef, etag: true),
        Loc("GET", "/api/locations/{id}/path", [200, 400, 401, 403, 404, 503], _ => ContractSchemas.LocationPath),
        Loc("POST", "/api/locations/validate", [200, 400, 401, 403, 503], _ => ContractSchemas.LocationValidation),
        // LOCATION v1.1 (owner operations, MOD-0173-FU02 §15).
        Loc("POST", "/api/locations", [200, 201, 400, 401, 403, 404, 409, 422, 503], _ => ContractSchemas.LocationDetail, etag: true),
        Loc("PATCH", "/api/locations/{id}", [200, 400, 401, 403, 404, 409, 422, 503], _ => ContractSchemas.LocationDetail, etag: true),
        Loc("POST", "/api/locations/{id}/status", [200, 400, 401, 403, 404, 409, 422, 503], _ => ContractSchemas.LocationDetail, etag: true),
        Loc("POST", "/api/locations/{id}/bins/bulk", [200, 201, 400, 401, 403, 404, 409, 422, 503],
            status => status == 201 ? ContractSchemas.BulkBinsResult : ContractSchemas.BulkBins200),
        Loc("GET", "/api/locations/{id}/history", [200, 400, 401, 403, 404, 503], _ => ContractSchemas.LocationHistoryPage),
        Loc("GET", "/api/locations/{id}/stock-presence", [200, 400, 401, 403, 404, 503], _ => ContractSchemas.LocationStockPresence),

        // TRACE-BUNDLE (MOD-0174, WP-0174-P1): v1 frozen + v1.1. 400 / 401 / 403 / 503 are the MOD-0173 scope refusals every
        // endpoint may give (the scope middleware covers /api/trace). Literal segments first: /serials/gs1-check before /serials/{id}.
        Trc("GET", "/api/trace/lots", [200, 400, 401, 403, 503], _ => ContractSchemas.LotList),
        Trc("POST", "/api/trace/lots", [200, 201, 400, 401, 403, 409, 422, 503], _ => ContractSchemas.LotRef, etag: true),
        Trc("POST", "/api/trace/validate", [200, 400, 401, 403, 503], _ => ContractSchemas.ValidateResponse),
        Trc("POST", "/api/trace/serials/gs1-check", [200, 400, 401, 403, 404, 422, 503], _ => ContractSchemas.Gs1CheckResult),
        Trc("GET", "/api/trace/serials", [200, 400, 401, 403, 503], _ => ContractSchemas.SerialPage),
        Trc("GET", "/api/trace/serials/{id}", [200, 400, 401, 403, 404, 503], _ => ContractSchemas.SerialRef),
        Trc("GET", "/api/trace/lots/{id}", [200, 400, 401, 403, 404, 503], _ => ContractSchemas.LotRef, etag: true),
        Trc("PATCH", "/api/trace/lots/{id}", [200, 400, 401, 403, 404, 409, 422, 503], _ => ContractSchemas.LotRef, etag: true),
        Trc("POST", "/api/trace/lots/{id}/status", [200, 400, 401, 403, 404, 409, 422, 503], _ => ContractSchemas.LotRef, etag: true),
        Trc("GET", "/api/trace/lots/{id}/history", [200, 400, 401, 403, 404, 503], _ => ContractSchemas.LotHistoryPage),
        Trc("GET", "/api/trace/lots/{id}/detail", [200, 400, 401, 403, 404, 503], _ => ContractSchemas.LotDetail, etag: true),
        Trc("GET", "/api/trace/lots/{id}/genealogy", [200, 400, 401, 403, 404, 503], _ => ContractSchemas.Genealogy),
        Trc("POST", "/api/trace/lots/{id}/genealogy", [200, 201, 400, 401, 403, 404, 422, 503], _ => ContractSchemas.GenealogyLink),

        // QUARANTINE-BUNDLE (MOD-0175, 1.0.0) — the operations slice Q1 implements. 400 / 401 / 403 / 503 are the scope
        // refusals every endpoint may give (the scope middleware covers /api/quarantine). Literal segments before {id}.
        Qrn("GET", "/api/quarantine/holds", [200, 400, 401, 403, 503], _ => ContractSchemas.HoldList),
        Qrn("POST", "/api/quarantine/holds", [200, 201, 400, 401, 403, 409, 422, 503], _ => ContractSchemas.HoldRefArray),
        Qrn("GET", "/api/quarantine/holds/{id}", [200, 400, 401, 403, 404, 503], _ => ContractSchemas.HoldDetail, etag: true),
        Qrn("POST", "/api/quarantine/holds/{id}/review", [200, 400, 401, 403, 404, 409, 503], _ => ContractSchemas.HoldRef, etag: true),
        Qrn("POST", "/api/quarantine/decisions", [200, 201, 400, 401, 403, 404, 409, 503], _ => ContractSchemas.Decision),
        Qrn("GET", "/api/quarantine/decisions/{id}", [200, 400, 401, 403, 404, 503], _ => ContractSchemas.Decision),
        Qrn("POST", "/api/quarantine/decisions/{id}/submit", [202, 400, 401, 403, 404, 409, 503], _ => ContractSchemas.Decision),
        Qrn("POST", "/api/quarantine/decisions/{id}/apply", [200, 400, 401, 403, 404, 409, 422, 503], _ => ContractSchemas.Decision),
        Qrn("GET", "/api/quarantine/blocked", [200, 400, 401, 403, 503], _ => ContractSchemas.BlockedList),
        Qrn("POST", "/api/quarantine/blocked", [200, 201, 400, 401, 403, 409, 422, 503], _ => ContractSchemas.BlockedRefArray),
        Qrn("GET", "/api/quarantine/blocked/{id}", [200, 400, 401, 403, 404, 503], _ => ContractSchemas.BlockedStockDetail, etag: true),
        Qrn("POST", "/api/quarantine/blocked/{id}/review-complete", [200, 400, 401, 403, 404, 409, 503], _ => ContractSchemas.BlockedStockRef, etag: true),
        Qrn("GET", "/api/quarantine/settings", [200, 400, 401, 403, 503], _ => ContractSchemas.Settings, etag: true),
        Qrn("PUT", "/api/quarantine/settings", [200, 400, 401, 403, 409, 503], _ => ContractSchemas.Settings, etag: true),
        Qrn("GET", "/api/quarantine/block-reasons", [200, 400, 401, 403, 503], _ => ContractSchemas.BlockReasonArray),
        Qrn("POST", "/api/quarantine/block-reasons", [201, 400, 401, 403, 409, 503], _ => ContractSchemas.BlockReason),
        Qrn("PUT", "/api/quarantine/block-reasons/{id}", [200, 400, 401, 403, 409, 503], _ => ContractSchemas.BlockReason)
    ];

    private static readonly Regex WeakETag = new("^W/\"\\d+\"$", RegexOptions.Compiled);

    /// <summary>How many responses were validated per operation + status (reported by the conformance test).</summary>
    public static readonly ConcurrentDictionary<string, int> Validated = new(StringComparer.Ordinal);

    protected override async Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken cancellationToken)
    {
        var response = await base.SendAsync(request, cancellationToken);
        var path = request.RequestUri!.AbsolutePath;
        var operation = Operations.FirstOrDefault(o => o.Method == request.Method.Method && o.Path.IsMatch(path));
        if (operation is null)
        {
            return response; // e.g. /health, or a deliberately wrong route in a routing test
        }

        var key = $"{operation.Method} {operation.Template}";
        var status = (int)response.StatusCode;
        var body = await response.Content.ReadAsStringAsync(cancellationToken);

        if (!operation.Allowed.Contains(status))
        {
            throw new ContractViolationException($"{key} answered {status}, which the contract/pack does not allow. Body: {body}");
        }

        var schemaName = status is 200 or 201 or 202 ? operation.SuccessSchema(status) : ContractSchemas.Error;
        var errors = operation.Contract switch
        {
            Location => ContractSchemas.ValidateLocation(schemaName, body),
            Trace => ContractSchemas.ValidateTrace(schemaName, body),
            Quarantine => ContractSchemas.ValidateQuarantine(schemaName, body),
            _ => ContractSchemas.Validate(schemaName, body)
        };
        if (errors.Count > 0)
        {
            throw new ContractViolationException(
                $"{key} {status} body does not match {operation.Contract} '{schemaName}':\n  {string.Join("\n  ", errors)}\nBody: {body}");
        }

        if (!response.Headers.TryGetValues("X-Correlation-Id", out var correlation) || !Guid.TryParse(correlation.Single(), out _))
        {
            throw new ContractViolationException($"{key} {status} carries no UUID X-Correlation-Id header.");
        }

        if (operation.ETagOnSuccess && status is 200 or 201 or 202
            && (response.Headers.ETag is null || !WeakETag.IsMatch(response.Headers.ETag.ToString())))
        {
            throw new ContractViolationException($"{key} {status} carries no ETag W/\"<version>\" header.");
        }

        Validated.AddOrUpdate($"{key} {status} → {operation.Contract}:{schemaName}", 1, (_, n) => n + 1);

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

    private static Operation Inv(string method, string template, int[] allowed, string schema)
        => new(method, template, Route(template), Inventory, [.. allowed], _ => schema);

    private static Operation Loc(string method, string template, int[] allowed, Func<int, string> schema, bool etag = false)
        => new(method, template, Route(template), Location, [.. allowed], schema, etag);

    /// <summary><c>{id}</c> = one path segment; literal segments (<c>search</c>, <c>validate</c>) are listed before it wins.</summary>
    private static Operation Trc(string method, string template, int[] allowed, Func<int, string> schema, bool etag = false)
        => new(method, template, Route(template), Trace, [.. allowed], schema, etag);

    private static Operation Qrn(string method, string template, int[] allowed, Func<int, string> schema, bool etag = false)
        => new(method, template, Route(template), Quarantine, [.. allowed], schema, etag);

    private static Regex Route(string template)
        => new("^" + Regex.Escape(template).Replace("\\{id}", "[^/]+") + "/?$", RegexOptions.Compiled);
}
