using System.Text.Json;
using System.Text.Json.Nodes;

namespace Diten.Platform.Application.Features.Workflow.Operations;

/// <summary>
/// WP-PLT-WORKFLOW-TEMPLATES-01 — the versioned approval-definition catalog in the repository
/// (<c>Seed/workflow-templates/product-identity-templates.json</c>, copied next to the API host): the templates the
/// <c>--ensure-workflow-templates</c> command creates and publishes when a tenant does not have them. Read only; nothing here
/// writes.
/// </summary>
public sealed record WorkflowTemplateCatalog(int CatalogVersion, IReadOnlyList<WorkflowTemplateCatalogEntry> Templates)
{
    /// <summary>The catalog's path relative to the API host's directory.</summary>
    public const string RelativePath = "Seed/workflow-templates/product-identity-templates.json";

    /// <summary>The publish reason every version this command writes carries — the measurable trace of the command (Create /
    /// Publish are not audited commands).</summary>
    public string PublishReason => $"{PublishReasonPrefix}{CatalogVersion}";

    /// <summary>What every publish reason of this command starts with (any catalog version): how the command recognises its
    /// own versions.</summary>
    public const string PublishReasonPrefix = "WP-PLT-WORKFLOW-TEMPLATES-01 catalog v";

    public static WorkflowTemplateCatalog Load(string path)
    {
        using var stream = File.OpenRead(path);
        var root = JsonNode.Parse(stream) as JsonObject
                   ?? throw new InvalidDataException("The workflow template catalog is not a JSON object.");
        var version = root["catalogVersion"]?.GetValue<int>() ?? 0;
        if (version < 1)
        {
            throw new InvalidDataException("The workflow template catalog has no catalogVersion (an integer, 1 or more).");
        }

        var entries = new List<WorkflowTemplateCatalogEntry>();
        foreach (var item in root["templates"] as JsonArray ?? [])
        {
            if (item is not JsonObject template)
            {
                throw new InvalidDataException("A workflow template catalog entry is not a JSON object.");
            }

            var definition = template["definition"] as JsonObject
                             ?? throw new InvalidDataException("A workflow template catalog entry has no definition object.");
            entries.Add(new WorkflowTemplateCatalogEntry(
                Required(template, "templateCode"),
                Required(template, "name"),
                template["description"]?.GetValue<string>(),
                Required(template, "schemaVersion"),
                Required(template, "expressionVersion"),
                Canonical(definition)));
        }

        if (entries.Count == 0 || entries.Select(e => e.TemplateCode).Distinct(StringComparer.Ordinal).Count() != entries.Count)
        {
            throw new InvalidDataException("The workflow template catalog must list at least one template, each code once.");
        }

        return new WorkflowTemplateCatalog(version, entries);
    }

    /// <summary>
    /// The definition's canonical JSON: object keys in ordinal order at every depth, no insignificant whitespace. Two
    /// definitions are the same when their canonical forms are equal (key order and spacing do not matter). Null when the
    /// text is not JSON.
    /// </summary>
    public static string? CanonicalOrNull(string? json)
    {
        if (string.IsNullOrWhiteSpace(json))
        {
            return null;
        }

        try
        {
            return Canonical(JsonNode.Parse(json));
        }
        catch (JsonException)
        {
            return null;
        }
    }

    private static string Canonical(JsonNode? node) => Sorted(node)?.ToJsonString() ?? "null";

    private static JsonNode? Sorted(JsonNode? node) => node switch
    {
        JsonObject obj => new JsonObject(obj
            .OrderBy(pair => pair.Key, StringComparer.Ordinal)
            .Select(pair => KeyValuePair.Create(pair.Key, Sorted(pair.Value)))),
        JsonArray array => new JsonArray(array.Select(Sorted).ToArray()),
        null => null,
        _ => JsonNode.Parse(node.ToJsonString())
    };

    private static string Required(JsonObject template, string property)
    {
        var value = template[property]?.GetValue<string>();
        if (string.IsNullOrWhiteSpace(value))
        {
            throw new InvalidDataException($"A workflow template catalog entry has no '{property}'.");
        }

        return value.Trim();
    }
}

/// <param name="DefinitionJson">The definition in its canonical JSON form (what is published and what is compared).</param>
public sealed record WorkflowTemplateCatalogEntry(
    string TemplateCode,
    string Name,
    string? Description,
    string SchemaVersion,
    string ExpressionVersion,
    string DefinitionJson)
{
    /// <summary>The same definition: canonical JSON equal, schema and expression versions equal (name / description are not
    /// compared).</summary>
    public bool Matches(string? definitionJson, string? schemaVersion, string? expressionVersion) =>
        string.Equals(WorkflowTemplateCatalog.CanonicalOrNull(definitionJson), DefinitionJson, StringComparison.Ordinal)
        && string.Equals(schemaVersion?.Trim(), SchemaVersion, StringComparison.Ordinal)
        && string.Equals(expressionVersion?.Trim(), ExpressionVersion, StringComparison.Ordinal);
}
