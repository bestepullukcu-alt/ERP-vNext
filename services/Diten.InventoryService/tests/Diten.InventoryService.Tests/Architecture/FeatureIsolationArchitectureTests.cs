using System.Text.RegularExpressions;
using Xunit;

namespace Diten.InventoryService.Tests.Architecture;

/// <summary>
/// WP-MVP1-FU-CLEAN1 (FU-EVIDENCE-AUDIT-NAMES) — the evidence packs' person-name read is ONE shared reader in a neutral place
/// (<c>Application/Common/IAuditActorNames</c> + <c>Persistence/CentralAuditActorNames</c>), and the Expiry (MOD-0176) and Recall (MOD-0177)
/// features do not depend on each other for it: every source reference from one feature to the other is one of the two links the CT accepted
/// by name (CT-R12: the FEFO candidate read skips lots under recall; R7b: the recall PDF uses the expiry pack's embedded font resolver). A new
/// cross-link — a copied reader, a shared helper moved into one feature — is red.
/// </summary>
public sealed class FeatureIsolationArchitectureTests
{
    /// <summary>The accepted cross-feature links: (the referencing file, the referenced type).</summary>
    private static readonly HashSet<(string File, string Type)> Accepted =
    [
        ("Diten.InventoryService.Persistence/Expiry/ExpiryReadModel.cs", "IRecallLotStatusReader"),           // CT-R12 (WP-0177-R3)
        ("Diten.InventoryService.Api/Recall/RecallEvidencePdfRenderer.cs", "ExpiryEvidencePdfFontResolver")   // R7b: one font set per service
    ];

    private static bool In(string path, string feature)
        => path.Contains($"/Features/{feature}/", StringComparison.Ordinal) || Regex.IsMatch(path, $@"^Diten\.InventoryService\.\w+/{feature}/");

    /// <summary>Type names declared by a feature's production files (class / record / interface / enum / struct).</summary>
    private static HashSet<string> TypesOf(IEnumerable<SourceScan.SourceFile> files)
        => files.SelectMany(f => Regex.Matches(f.Body,
                    @"^\s*(?:(?:public|internal|private|protected|sealed|static|partial|abstract|readonly|file)\s+)+(?:record\s+(?:struct\s+|class\s+)?|class\s+|interface\s+|enum\s+|struct\s+)([A-Z]\w*)",
                    RegexOptions.Multiline).Select(m => m.Groups[1].Value))
            .ToHashSet(StringComparer.Ordinal);

    [Fact]
    public void Expiry_and_Recall_reference_each_other_only_through_the_accepted_links()
    {
        var files = SourceScan.Production();
        var expiry = files.Where(f => In(f.RelativePath, "Expiry")).ToList();
        var recall = files.Where(f => In(f.RelativePath, "Recall")).ToList();
        Assert.True(expiry.Count > 20 && recall.Count > 20, $"vacuous scan: {expiry.Count} / {recall.Count}");
        // a feature's OWN types carry its prefix (ExpiryEvidenceBuilder, FefoCandidateReader, IRecallLotStatusReader …); short nested names
        // (Row, Names) are not evidence of a link — a namespace reference is
        var expiryTypes = TypesOf(expiry).Where(t => Regex.IsMatch(t, @"^I?(Expiry|Fefo)[A-Z]")).ToHashSet(StringComparer.Ordinal);
        var recallTypes = TypesOf(recall).Where(t => Regex.IsMatch(t, @"^I?Recall[A-Z]")).ToHashSet(StringComparer.Ordinal);
        Assert.True(expiryTypes.Count > 20 && recallTypes.Count > 20, $"vacuous type list: {expiryTypes.Count} / {recallTypes.Count}");

        var links = new List<(string File, string Type)>();
        void Scan(IEnumerable<SourceScan.SourceFile> from, IReadOnlySet<string> otherTypes, string otherFeature)
        {
            foreach (var f in from)
            {
                foreach (var type in otherTypes.Where(t => Regex.IsMatch(f.Body, $@"\b{Regex.Escape(t)}\b"))) links.Add((f.RelativePath, type));
                if (Regex.IsMatch(f.Body, $@"\b(Features|Persistence|Domain|Api|Infrastructure)\.{otherFeature}\b")
                    && !links.Any(l => l.File == f.RelativePath))
                    links.Add((f.RelativePath, $"namespace .{otherFeature}"));
            }
        }

        Scan(expiry, recallTypes, "Recall");
        Scan(recall, expiryTypes, "Expiry");
        Assert.All(links, l => Assert.True(Accepted.Contains(l), $"new cross-feature link: {l.File} → {l.Type}"));
        Assert.All(Accepted, a => Assert.Contains(a, links)); // the list stays honest: an accepted link that disappeared is removed from it
    }

    [Fact]
    public void The_audit_name_read_is_one_shared_reader_in_the_neutral_layers()
    {
        var files = SourceScan.Production();
        var port = Assert.Single(files, f => Regex.IsMatch(f.Body, @"\binterface\s+IAuditActorNames\b"));
        Assert.Equal("Diten.InventoryService.Application/Common/IAuditActorNames.cs", port.RelativePath);
        var reader = Assert.Single(files, f => Regex.IsMatch(f.Body, @"class\s+\w+\s*:\s*IAuditActorNames\b"));
        Assert.Equal("Diten.InventoryService.Persistence/CentralAuditActorNames.cs", reader.RelativePath);

        // no feature declares its own copy any more (the two FIX-LIVE2 readers are gone)
        Assert.DoesNotContain(files, f => (In(f.RelativePath, "Expiry") || In(f.RelativePath, "Recall"))
                                          && Regex.IsMatch(f.Body, @"\b(?:interface|class)\s+\w*AuditNames\b"));
        // and both packs read names through it
        Assert.Contains("IAuditActorNames", Assert.Single(files, f => f.RelativePath.EndsWith("Features/Expiry/Handlers/EvidenceHandlers.cs", StringComparison.Ordinal)).Body);
        Assert.Contains("IAuditActorNames", Assert.Single(files, f => f.RelativePath.EndsWith("Features/Recall/Evidence/RecallEvidence.cs", StringComparison.Ordinal)).Body);
    }
}
