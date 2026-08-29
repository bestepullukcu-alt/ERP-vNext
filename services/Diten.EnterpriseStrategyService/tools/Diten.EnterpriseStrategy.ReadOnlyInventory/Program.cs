using System.Text;
using System.Text.Json;
using Diten.Application.Common.Tenancy;
using Diten.Persistence.Evidence;
using static Diten.Application.Common.Tenancy.EnterpriseStrategyCollectionInventoryContracts;

InventoryOptions options;
try
{
    options = EnterpriseStrategyCollectionInventoryContracts.ReadOptions(Environment.GetEnvironmentVariable);
}
catch
{
    Console.Error.WriteLine("Read-only inventory environment gate failed; no MongoDB client was constructed.");
    return 1;
}
InventoryReport report;
try
{
    var reader = new ReadOnlyEnterpriseStrategyCollectionInventory(options);
    report = await reader.ReadAsync(TimeProvider.System, CancellationToken.None);
}
catch (OperationCanceledException)
{
    Console.Error.WriteLine("Read-only inventory was cancelled; no evidence was written.");
    return 2;
}
catch
{
    Console.Error.WriteLine("Read-only inventory failed; connection details were suppressed and no evidence was written.");
    return 3;
}
var repositoryRoot = FindRepositoryRoot(AppContext.BaseDirectory);
var evidenceDirectory = Path.Combine(
    repositoryRoot,
    "docs",
    "audits",
    "enterprise-strategy-security-tenancy-migration");
Directory.CreateDirectory(evidenceDirectory);

var jsonPath = Path.Combine(evidenceDirectory, "collection-inventory-v1.json");
var markdownPath = Path.Combine(evidenceDirectory, "collection-inventory-v1.md");
await File.WriteAllTextAsync(jsonPath, Serialize(report), new UTF8Encoding(false));
await File.WriteAllTextAsync(markdownPath, RenderMarkdown(report), new UTF8Encoding(false));

Console.WriteLine($"Read-only inventory completed for target-copy '{SanitizeMarkdown(report.TargetCopyId)}'.");
Console.WriteLine($"Run '{SanitizeMarkdown(report.RunId)}'; collections: {report.Collections.Count}.");
Console.WriteLine("Evidence: docs/audits/enterprise-strategy-security-tenancy-migration/collection-inventory-v1.json");
Console.WriteLine("Evidence: docs/audits/enterprise-strategy-security-tenancy-migration/collection-inventory-v1.md");
return 0;

static string Serialize(InventoryReport report) => JsonSerializer.Serialize(
    report,
    new JsonSerializerOptions { PropertyNamingPolicy = JsonNamingPolicy.CamelCase, WriteIndented = true });

static string RenderMarkdown(InventoryReport report)
{
    var output = new StringBuilder()
        .AppendLine("# Enterprise Strategy collection inventory v1")
        .AppendLine()
        .AppendLine($"- Target copy: `{SanitizeMarkdown(report.TargetCopyId)}`")
        .AppendLine($"- Run: `{SanitizeMarkdown(report.RunId)}`")
        .AppendLine($"- Measured UTC: `{report.MeasuredAtUtc:O}`")
        .AppendLine($"- Metadata SHA-256: `{report.MetadataSha256}`")
        .AppendLine()
        .AppendLine("All collections are `unreviewed`; this artifact does not authorize migration or mutation.")
        .AppendLine();

    foreach (var collection in report.Collections)
        AppendCollection(output, collection);
    return output.ToString();
}

static void AppendCollection(StringBuilder output, CollectionEvidence collection)
{
    output.AppendLine($"## {SanitizeMarkdown(collection.Name)}")
        .AppendLine()
        .AppendLine($"- Documents: {collection.DocumentCount}")
        .AppendLine($"- Disposition: `{SanitizeMarkdown(collection.Disposition)}`")
        .AppendLine("- Owner: `unassigned`")
        .AppendLine()
        .AppendLine("| Index | Keys | Unique | Sparse | TTL seconds |")
        .AppendLine("|---|---|---:|---:|---:|");
    foreach (var index in collection.Indexes)
        output.Append("| ").Append(SanitizeMarkdown(index.Name)).Append(" | ")
            .Append(SanitizeMarkdown(string.Join(", ", index.Keys.Select(key => $"{key.Field}:{key.Direction}"))))
            .Append(" | ").Append(index.IsUnique).Append(" | ").Append(index.IsSparse).Append(" | ")
            .Append(index.ExpireAfterSeconds?.ToString() ?? "-").AppendLine(" |");
    output.AppendLine().AppendLine("| Field | BSON type | Count |").AppendLine("|---|---|---:|");
    foreach (var field in collection.FieldTypes)
        output.Append("| ").Append(SanitizeMarkdown(field.Field)).Append(" | ")
            .Append(SanitizeMarkdown(field.BsonType)).Append(" | ").Append(field.Count).AppendLine(" |");
    output.AppendLine();
}

static string FindRepositoryRoot(string start)
{
    for (var current = new DirectoryInfo(start); current is not null; current = current.Parent)
        if (File.Exists(Path.Combine(current.FullName, "AGENTS.md")))
            return current.FullName;
    throw new InvalidOperationException("Repository root was not found.");
}
