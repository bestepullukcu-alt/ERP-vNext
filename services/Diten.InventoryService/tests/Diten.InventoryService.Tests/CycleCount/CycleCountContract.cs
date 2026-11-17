using System.Collections.Concurrent;
using System.Text.Json.Nodes;
using System.Text.RegularExpressions;
using Diten.InventoryService.Tests.TestInfrastructure;
using YamlDotNet.Serialization;

namespace Diten.InventoryService.Tests.CycleCounting;

/// <summary>
/// MOD-0181 — the count answers against the FROZEN <c>docs/analysis/contracts/wms-count-bundle.openapi.yaml</c> itself (no hand-derived copy; the
/// MOD-0178 <c>WmsContract</c> pattern). <see cref="ContractSchemas"/> turns the file into JSON Schema 2020-12 (components → <c>$defs</c>; every
/// response body an operation declares lifted as <c>{operationId}.{status}</c>), formats enforced, every object closed. The strictness of the
/// earlier draft check (<c>CycleCountDraftContract</c>, DL-1…DL-4) is unchanged: closed objects, the contract's enums, decimal strings, uuid / date /
/// date-time formats and NO JSON null anywhere (rule 4).
/// <para>The operation table is the file's <c>paths</c> (method + template; the template with the most literal segments wins, so
/// <c>/cycle-counts/dashboard</c> and <c>/cycle-counts/bulk-delete</c> are never read as <c>/cycle-counts/{countId}</c>).</para>
/// <para>1.1.0 (DL-5, Evidence Pack): a status the file declares with non-JSON content (the evidence file) is a FILE answer — its media type must be
/// one the file names and every header the file marks <c>required</c> must be present (<c>X-Content-SHA256</c> must also BE the SHA-256 of the
/// bytes). This replaced the DL-5 draft validator (<c>CountEvidenceDraftContract</c>) — the DL-5 answers are measured against the frozen file.</para>
/// </summary>
public static class CycleCountContract
{
    public const string BasePath = "/api/warehouse";

    // component names of the frozen file
    public const string Error = "CountError";
    public const string AuthError = "AuthError";
    public const string Count = "CycleCount";
    public const string Line = "CycleCountLine";
    public const string Dashboard = "CycleCountDashboard";
    public const string Variance = "CountVariance";
    public const string Settings = "CycleCountSettings";

    private static readonly Lazy<JsonObject> Document = new(() =>
    {
        var raw = new DeserializerBuilder().Build().Deserialize<object>(File.ReadAllText(ContractSchemas.WmsCountContractPath));
        return (JsonObject)JsonNode.Parse(System.Text.Json.JsonSerializer.Serialize(raw))!;
    });

    /// <summary>The frozen file as JSON (YAML scalars as strings).</summary>
    public static JsonObject Yaml => Document.Value;

    /// <summary>How many answers were validated per operationId + status.</summary>
    public static readonly ConcurrentDictionary<string, int> Validated = new(StringComparer.Ordinal);

    /// <summary>The contract's verdict on one body (empty = valid).</summary>
    public static IReadOnlyList<string> Validate(string schemaName, string body) => ContractSchemas.ValidateWmsCount(schemaName, body);

    /// <summary>A non-JSON answer the file declares: its media types and the headers it marks required.</summary>
    internal sealed record FileAnswer(IReadOnlySet<string> MediaTypes, IReadOnlyList<string> RequiredHeaders);

    internal sealed record Operation(string Method, string Template, string OperationId, Regex Path, int Literals, IReadOnlySet<int> Declared,
        IReadOnlyDictionary<int, FileAnswer> Files);

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
                var responses = operation!["responses"]!.AsObject();
                var declared = responses
                    .Where(r => r.Value!["$ref"] is not null || r.Value!["content"]?["application/json"]?["schema"] is not null)
                    .Select(r => int.Parse(r.Key, System.Globalization.CultureInfo.InvariantCulture)).ToHashSet();
                var files = responses
                    .Where(r => r.Value!["$ref"] is null && r.Value!["content"] is JsonObject c && c.Count > 0 && c["application/json"] is null)
                    .ToDictionary(r => int.Parse(r.Key, System.Globalization.CultureInfo.InvariantCulture), r => new FileAnswer(
                        r.Value!["content"]!.AsObject().Select(m => m.Key).ToHashSet(StringComparer.OrdinalIgnoreCase),
                        (r.Value!["headers"] as JsonObject ?? []).Where(h => h.Value?["required"]?.GetValue<string>() == "true").Select(h => h.Key).ToList()));
                operations.Add(new Operation(method.ToUpperInvariant(), template, operation["operationId"]!.GetValue<string>(),
                    new Regex(pattern, RegexOptions.Compiled), segments.Count(s => !s.StartsWith('{')), declared, files));
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
    /// service-wide middleware's <see cref="AuthError"/> (rule 3: code may be absent); any other undeclared status is the count error envelope.
    /// An undeclared 2xx is a violation.
    /// </summary>
    internal static string? CodeProblem(Operation operation, int status, string body)
    {
        var code = JsonNode.Parse(body)?["error"]?["code"]?.GetValue<string>();
        var catalogue = Yaml["components"]!["x-error-codes"]!.AsObject();
        if (catalogue[code ?? string.Empty] is { } entry)
        {
            if (entry["status"]!.GetValue<string>() != status.ToString(System.Globalization.CultureInfo.InvariantCulture))
                return $"the catalogue gives {code} the status {entry["status"]!.GetValue<string>()}, not {status}.";
            var listed = Yaml["paths"]![operation.Template]![operation.Method.ToLowerInvariant()]!["x-error-codes"]?.AsArray().Select(v => v!.GetValue<string>());
            if (listed is null || !listed.Contains(code)) return $"{code} is not in the operation's x-error-codes.";
        }

        return null;
    }

    internal static string? SchemaFor(Operation operation, int status)
        => operation.Declared.Contains(status) ? ContractSchemas.WmsResponse(operation.OperationId, status)
            : status is >= 200 and < 300 ? null
            : status is 401 or 403 or 500 ? AuthError
            : Error;
}

/// <summary>
/// Every <c>/api/warehouse/cycle-counts*</c>, <c>/count-variances*</c>, <c>/count-settings</c> and (1.1.0) <c>/count-evidence-exports*</c> answer a
/// cycle-count test receives is checked against the frozen WMS-COUNT-BUNDLE (<see cref="CycleCountContract"/>): the body against the schema its
/// operation declares for the status — or, for a declared FILE answer, its media type, its required headers and <c>X-Content-SHA256</c> = the
/// SHA-256 of the bytes — plus the <c>X-Correlation-Id</c> header. A violation throws (the test turns red). An empty 401 (the JWT challenge) has
/// no body to check. The bytes are handed on unchanged.
/// </summary>
public sealed class CycleCountContractHandler : DelegatingHandler
{
    protected override async Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken cancellationToken)
    {
        var response = await base.SendAsync(request, cancellationToken);
        var operation = CycleCountContract.Match(request.Method.Method, request.RequestUri!.AbsolutePath);
        if (operation is null) return response;

        var status = (int)response.StatusCode;
        var bytes = await response.Content.ReadAsByteArrayAsync(cancellationToken);
        var key = $"{operation.OperationId} {status}";
        if (operation.Files.TryGetValue(status, out var file))
        {
            var type = response.Content.Headers.ContentType?.MediaType;
            if (type is null || !file.MediaTypes.Contains(type))
                throw new ContractViolationException($"{key}: the file answer's media type is {type}, not one of [{string.Join(", ", file.MediaTypes)}].");
            foreach (var header in file.RequiredHeaders)
            {
                if (!response.Headers.NonValidated.Contains(header) && !response.Content.Headers.NonValidated.Contains(header)) // a content header (Content-Disposition) or a response header (X-Content-SHA256)
                    throw new ContractViolationException($"{key}: the file answer carries no {header} header.");
            }

            if (response.Headers.TryGetValues("X-Content-SHA256", out var declared)
                && declared.Single() != Convert.ToHexString(System.Security.Cryptography.SHA256.HashData(bytes)).ToLowerInvariant())
                throw new ContractViolationException($"{key}: X-Content-SHA256 is not the SHA-256 of the bytes.");
        }
        else
        {
            var body = System.Text.Encoding.UTF8.GetString(bytes);
            var schema = CycleCountContract.SchemaFor(operation, status)
                         ?? throw new ContractViolationException($"{key}: the frozen WMS-COUNT-BUNDLE declares no {status} answer for {operation.Method} {operation.Template}. Body: {body}");
            if (status != 401 || body.Length > 0)
            {
                var errors = CycleCountContract.Validate(schema, body);
                if (errors.Count > 0)
                    throw new ContractViolationException($"{key} body does not match the frozen WMS-COUNT-BUNDLE '{schema}':\n  {string.Join("\n  ", errors)}\nBody: {body}");
                if (status >= 400 && schema != CycleCountContract.AuthError && CycleCountContract.CodeProblem(operation, status, body) is { } problem)
                    throw new ContractViolationException($"{key}: {problem} Body: {body}");
            }
        }

        if (!response.Headers.TryGetValues("X-Correlation-Id", out var correlation) || !Guid.TryParse(correlation.Single(), out _))
            throw new ContractViolationException($"{key} carries no UUID X-Correlation-Id header.");

        CycleCountContract.Validated.AddOrUpdate(key, 1, (_, n) => n + 1);
        var content = new ByteArrayContent(bytes);
        foreach (var header in response.Content.Headers) content.Headers.TryAddWithoutValidation(header.Key, header.Value);
        response.Content = content;
        return response;
    }
}
