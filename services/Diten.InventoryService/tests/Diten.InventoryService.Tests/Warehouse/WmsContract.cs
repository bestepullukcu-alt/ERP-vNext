using System.Collections.Concurrent;
using System.Text.Json.Nodes;
using System.Text.RegularExpressions;
using Diten.InventoryService.Tests.TestInfrastructure;
using YamlDotNet.Serialization;

namespace Diten.InventoryService.Tests.Warehouse;

/// <summary>
/// MOD-0178 AC-60 (pack §19.7, the CT freeze step) — the warehouse answers against the FROZEN <c>docs/analysis/contracts/wms-bundle.openapi.yaml</c>
/// itself (no hand-derived copy): <see cref="ContractSchemas"/> turns the file into JSON Schema 2020-12 (components → <c>$defs</c>; every response body
/// an operation declares lifted as <c>{operationId}.{status}</c>), formats enforced, every object closed. Since AC-19 (DL-2) measured the same
/// answers against the draft, the strictness is unchanged: closed objects, the contract's enums, decimal strings, uuid / date-time formats and
/// NO JSON null — the only nulls the file admits are the two named §8.23 members of a <c>ShortDispatchUnresolved</c> exception's details
/// (<c>stagingDifference</c>, <c>differenceDeferredBy</c>; rule 4 exception, see the YAML).
/// <para>The operation table is the file's <c>paths</c> (method + template; the template with the most literal segments wins, so
/// <c>/outbound-shipments/search</c> is never read as the frozen <c>/outbound-shipments/{outboundId}</c>).</para>
/// </summary>
public static class WmsContract
{
    public const string BasePath = "/api/warehouse";

    // component names of the frozen file
    public const string Error = "WmsError";
    public const string AuthError = "AuthError";
    public const string Task = "WarehouseTask";
    public const string ConfirmResult = "ConfirmPutawayTaskResult";
    public const string ExceptionPage = "WarehouseExceptionPage";

    private static readonly Lazy<JsonObject> Document = new(() =>
    {
        var raw = new DeserializerBuilder().Build().Deserialize<object>(File.ReadAllText(ContractSchemas.WmsContractPath));
        return (JsonObject)JsonNode.Parse(System.Text.Json.JsonSerializer.Serialize(raw))!;
    });

    /// <summary>The frozen file as JSON (YAML scalars as strings).</summary>
    public static JsonObject Yaml => Document.Value;

    /// <summary>How many answers were validated per operationId + status (AC-60 coverage).</summary>
    public static readonly ConcurrentDictionary<string, int> Validated = new(StringComparer.Ordinal);

    /// <summary>The contract's verdict on one body (empty = valid).</summary>
    public static IReadOnlyList<string> Validate(string schemaName, string body) => ContractSchemas.ValidateWms(schemaName, body);

    internal sealed record Operation(string Method, string Template, string OperationId, Regex Path, int Literals, IReadOnlySet<int> Declared);

    private static readonly Lazy<IReadOnlyList<Operation>> Table = new(() =>
    {
        var operations = new List<Operation>();
        foreach (var (template, item) in Yaml["paths"]!.AsObject())
        {
            foreach (var (method, operation) in item!.AsObject())
            {
                if (method is not ("get" or "post" or "put" or "delete" or "patch")) continue;
                var segments = template.Split('/', StringSplitOptions.RemoveEmptyEntries);
                var pattern = "^" + Regex.Escape(BasePath) + string.Concat(segments.Select(s => "/" + (s.StartsWith('{') ? "[^/]+" : Regex.Escape(s)))) + "/?$";
                var declared = operation!["responses"]!.AsObject()
                    .Where(r => r.Value!["$ref"] is not null || r.Value!["content"]?["application/json"]?["schema"] is not null)
                    .Select(r => int.Parse(r.Key, System.Globalization.CultureInfo.InvariantCulture)).ToHashSet();
                operations.Add(new Operation(method.ToUpperInvariant(), template, operation["operationId"]!.GetValue<string>(),
                    new Regex(pattern, RegexOptions.Compiled), segments.Count(s => !s.StartsWith('{')), declared));
            }
        }

        return operations;
    });

    /// <summary>Every operation the frozen file declares.</summary>
    internal static IReadOnlyList<Operation> Operations => Table.Value;

    /// <summary>The operation of a request, or null when the path is not one of the contract's.</summary>
    internal static Operation? Match(string method, string path)
        => Operations.Where(o => o.Method == method && o.Path.IsMatch(path)).OrderByDescending(o => o.Literals).FirstOrDefault();

    /// <summary>
    /// The schema an answer is measured against: the body the operation declares for the status; an undeclared 401 / 403 / 500 is the
    /// service-wide middleware's <see cref="AuthError"/> (rule 3: code may be absent); any other undeclared status is the WMS error envelope.
    /// An undeclared 2xx is a violation.
    /// </summary>
    internal static string? SchemaFor(Operation operation, int status)
        => operation.Declared.Contains(status) ? ContractSchemas.WmsResponse(operation.OperationId, status)
            : status is >= 200 and < 300 ? null
            : status is 401 or 403 or 500 ? AuthError
            : Error;
}

/// <summary>
/// AC-60 — every <c>/api/warehouse</c> answer a warehouse test receives is checked against the frozen WMS-BUNDLE (<see cref="WmsContract"/>): the
/// body against the schema its operation declares for the status, plus the <c>X-Correlation-Id</c> header. A violation throws (the test turns red).
/// </summary>
public sealed class WmsContractHandler : DelegatingHandler
{
    protected override async Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken cancellationToken)
    {
        var response = await base.SendAsync(request, cancellationToken);
        var operation = WmsContract.Match(request.Method.Method, request.RequestUri!.AbsolutePath);
        if (operation is null) return response;

        var status = (int)response.StatusCode;
        var body = await response.Content.ReadAsStringAsync(cancellationToken);
        var key = $"{operation.OperationId} {status}";
        var schema = WmsContract.SchemaFor(operation, status)
                     ?? throw new ContractViolationException($"{key}: the frozen WMS-BUNDLE declares no {status} answer for {operation.Method} {operation.Template}. Body: {body}");
        var errors = WmsContract.Validate(schema, body);
        if (errors.Count > 0)
        {
            throw new ContractViolationException($"{key} body does not match the frozen WMS-BUNDLE '{schema}':\n  {string.Join("\n  ", errors)}\nBody: {body}");
        }

        if (!response.Headers.TryGetValues("X-Correlation-Id", out var correlation) || !Guid.TryParse(correlation.Single(), out _))
        {
            throw new ContractViolationException($"{key} carries no UUID X-Correlation-Id header.");
        }

        WmsContract.Validated.AddOrUpdate(key, 1, (_, n) => n + 1);        var content = new StringContent(body, System.Text.Encoding.UTF8, "application/json");
        foreach (var header in response.Content.Headers)
        {
            content.Headers.Remove(header.Key);
            content.Headers.TryAddWithoutValidation(header.Key, header.Value);
        }

        response.Content = content;
        return response;
    }
}
