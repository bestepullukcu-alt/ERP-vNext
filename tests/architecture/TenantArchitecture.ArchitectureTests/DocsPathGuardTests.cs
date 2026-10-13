using System.Text.RegularExpressions;
using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using System.Text.Json.Serialization;

namespace TenantArchitecture.ArchitectureTests;

/*
 * THE GUARD — NO CODE FILE POINTS INTO docs/ OUTSIDE THE FIVE FOLDERS (2026-09-10).
 *
 * WHY THIS FILE EXISTS. The 2026-09-07 docs reorganisation (9d8551e1) broke CODE four times, not documents:
 * a Python gate (.py), a CSS comment (.css), DocumentReferenceListTests (.cs — ten tests red on main) and two
 * PowerShell scripts (.ps1). The fixes are in 9c5119c1. `.antigravity/rules/docs-organization.md` §4 step 4
 * already said "scan the code extensions too" — but it was a step somebody had to REMEMBER, and its list
 * did not even name .ps1. A rule that is written is not a rule that runs; this is the running version.
 *
 * THE RULE. After the reorganisation `docs/` holds exactly five folders — vendor · records · roadmap · guides
 * · reference. Any code-side path naming another folder directly under it points at something that was moved
 * or never existed, and it fails the day somebody runs it rather than the day the docs moved.
 * A separately approved exact authority record can distinguish a verified legacy canonical
 * dependency or sealed historical evidence. No prefix/directory exclusion is granted; see ReadAuthority.
 *
 * WHAT IS MATCHED — the two shapes the four breakages actually had:
 *   • a slashed path whose first segment under docs is followed by a separator: docs/‹x›/… or docs\‹x›\…
 *     (a FOLDER; a file sitting at the docs root is a different question and not this guard's);
 *   • Path.Combine/Path.Join arguments: …, "docs", "‹x›", … — again only when another argument follows,
 *     and only when ‹x› looks like a folder name (no spaces): `"docs", "Supporting documents attached",` is
 *     a UI label pair in DemandIdeaCapturePageMapper, not a path, and it was the first false positive.
 * Comments are NOT stripped: the CSS breakage WAS a comment, and a stale path in a comment sends the next
 * reader to a folder that does not exist.
 *
 * ⚠ THIS FILE'S OWN EXAMPLES use ‹x›, which is not a name character — prose about the rule cannot
 * trip it, and the scan still reads this file like any other.
 *
 * WHAT IS NOT MATCHED, deliberately:
 *   • a SERVICE'S OWN docs folder (services/Diten.AuthService/docs/…) — discovered from disk, not listed, so
 *     a new one is recognised without editing this file;
 *   • generated or third-party trees: obj/ bin/ node_modules/ wwwroot/assets/vendor/ frontend/_Reference/,
 *     and tool state (.git, .git-backups, .claude, .logs, .vscode). `.antigravity` IS scanned — the Python
 *     gate that broke lives there.
 *
 * ⚠ IT READS TEXT, NOT A SYNTAX TREE. A path assembled across statements (var d = "docs"; … d + "/x/")
 * evades it. None of the four breakages had that shape; if one ever does, that is the upgrade.
 */
public class DocsPathGuardTests
{
    private static readonly HashSet<string> Five = new(StringComparer.Ordinal)
    {
        "vendor", "records", "roadmap", "guides", "reference"
    };

    /// <summary>The extensions the 2026-09-07 breakages lived in, and every other one code is written in here.</summary>
    private static readonly HashSet<string> CodeExtensions = new(StringComparer.OrdinalIgnoreCase)
    {
        ".cs", ".cshtml", ".js", ".py", ".sh", ".ps1", ".json", ".csproj", ".css", ".html",
        ".yaml", ".yml", ".xml", ".resx"
    };

    private static readonly HashSet<string> PrunedDirectoryNames = new(StringComparer.Ordinal)
    {
        "obj", "bin", "node_modules", ".git", ".git-backups", ".claude", ".logs", ".vscode"
    };

    private static readonly string[] PrunedPaths =
    [
        "frontend/Diten.Web/wwwroot/assets/vendor",
        "frontend/_Reference"
    ];

    private const string NameChars = "A-Za-z0-9_.-";

    /* docs/‹x›/  or  docs\‹x›\  — not preceded by a name character, so "mydocs/x/" is not a docs path. */
    private static readonly Regex Slashed = new(
        $@"(?<![{NameChars}])docs[/\\]+(?<x>[{NameChars}]+)[/\\]",
        RegexOptions.Compiled);

    /* …, "docs", "‹x›", … — the trailing comma is what makes x a folder rather than the last (file) segment. */
    private static readonly Regex Combined = new(
        $@"""docs""\s*,\s*@?""(?<x>[{NameChars}]+)""\s*,",
        RegexOptions.Compiled);

    /* What sits immediately before a match — the parent folder, if the path names one. */
    private static readonly Regex SlashedParent = new($@"(?<p>[{NameChars}]+)[/\\]+$", RegexOptions.Compiled);
    private static readonly Regex CombinedParent = new(@"""(?<p>[^""]+)""\s*,\s*$", RegexOptions.Compiled);

    [Fact]
    public void NoCodeFilePointsIntoDocsOutsideTheFiveFolders()
    {
        VerifyRoot(RepoRoot());
    }

    private static void VerifyRoot(string root, bool syntheticFixture = false)
    {
        var authority = ReadAuthority(root, syntheticFixture);
        var consumed = new HashSet<string>(StringComparer.Ordinal);
        var files = CodeFiles(root).ToList();
        var nestedDocsParents = NestedDocsParents(root);

        var offenders = new List<string>();
        var honoured = 0;

        foreach (var file in files)
        {
            var relative = Path.GetRelativePath(root, file).Replace('\\', '/');
            var lines = File.ReadAllLines(file);

            for (var i = 0; i < lines.Length; i++)
            {
                foreach (var hit in Hits(lines[i], nestedDocsParents))
                {
                    if (Five.Contains(hit))
                    {
                        honoured++;
                        continue;
                    }

                    // Only validated structured authority fields or an exact sealed input can qualify.
                    if (authority.Contains(relative))
                    {
                        consumed.Add(relative);
                        continue;
                    }
                    offenders.Add($"{relative}:{i + 1} → docs/{hit} — bkz. docs-organization.md §4");
                }
            }
        }

        /*
         * ⚠ PRESENCE FIRST. "No violations" from a scan that read nothing is the vacuous green this guard
         * exists to replace. So: it must have read the extension that was missing from the written rule, the
         * language that broke ten tests, and at least one LEGITIMATE docs path — proof the patterns fire.
         */
        Assert.Contains(files, f => f.EndsWith(".ps1", StringComparison.OrdinalIgnoreCase));
        Assert.Contains(files, f => f.EndsWith(".cs", StringComparison.OrdinalIgnoreCase));
        Assert.True(honoured > 0, "the scan matched no docs path at all — the patterns are not firing");

        Assert.True(authority.SetEquals(consumed), "stale or unconsumed authority disposition");
        Assert.True(offenders.Count == 0,
            "code points into docs/ outside vendor · records · roadmap · guides · reference. "
            + "Each of these fails the day it runs, not the day the docs moved:\n"
            + string.Join("\n", offenders));
    }

    private const string AuthorityPath = "docs/reference/architecture/docs-path-authority.json";
    private static readonly JsonSerializerOptions AuthorityJson = new()
    {
        PropertyNamingPolicy = JsonNamingPolicy.CamelCase,
        UnmappedMemberHandling = JsonUnmappedMemberHandling.Disallow
    };
    private sealed record Target(string Path, string Sha256);
    private sealed record DecisionRef(string Path, string Sha256);
    private sealed record Seal(string Path, string Sha256, string Kind, string ProvenancePath,
        string ProvenanceSha256, int ProvenanceLine, string[] Targets);
    private sealed record Authority(int SchemaVersion, string Status, DecisionRef? Decision,
        Target[] CanonicalTargets, Seal[] SealedInputs);
    private sealed record OwnerDecision(string Kind, string Status, string DecisionId,
        string ApprovedBy, string PayloadSha256);

    private static string Hash(byte[] bytes) => Convert.ToHexString(SHA256.HashData(bytes)).ToLowerInvariant();
    private static byte[] ReadExact(string root, string relative, string? digest = null)
    {
        Assert.False(string.IsNullOrWhiteSpace(relative), "missing path");
        Assert.False(Path.IsPathRooted(relative) || relative.Contains('\\') || relative.Contains('*') ||
            relative.Contains('?') || relative.Split('/').Any(x => x is "" or "." or ".."), "unsafe path");
        var path = root;
        foreach (var segment in relative.Split('/'))
        {
            path = Path.Combine(path, segment);
            Assert.True(File.Exists(path) || Directory.Exists(path), "missing input: " + relative);
            Assert.False(File.GetAttributes(path).HasFlag(FileAttributes.ReparsePoint), "symlink input");
        }
        var bytes = File.ReadAllBytes(path);
        if (digest != null)
        {
            Assert.Matches("^[a-f0-9]{64}$", digest);
            Assert.Equal(digest, Hash(bytes));
        }
        return bytes;
    }
    private static byte[] ReadPinned(string root, string relative, string digest)
    {
        Assert.False(string.IsNullOrWhiteSpace(digest), "missing required SHA-256");
        Assert.Matches("^[a-f0-9]{64}$", digest);
        return ReadExact(root, relative, digest);
    }
    private static void NoDuplicateKeys(JsonElement node)
    {
        if (node.ValueKind == JsonValueKind.Object)
        {
            var names = new HashSet<string>(StringComparer.Ordinal);
            foreach (var property in node.EnumerateObject())
            {
                Assert.True(names.Add(property.Name), "duplicate JSON property");
                NoDuplicateKeys(property.Value);
            }
        }
        else if (node.ValueKind == JsonValueKind.Array)
            foreach (var item in node.EnumerateArray()) NoDuplicateKeys(item);
    }
    private static HashSet<string> ReadAuthority(string root, bool syntheticFixture)
    {
        if (!File.Exists(Path.Combine(root, AuthorityPath))) return new(StringComparer.Ordinal);
        var bytes = ReadExact(root, AuthorityPath);
        using var doc = JsonDocument.Parse(bytes);
        NoDuplicateKeys(doc.RootElement);
        var record = JsonSerializer.Deserialize<Authority>(bytes, AuthorityJson)!;
        Assert.Equal(1, record.SchemaVersion);
        Assert.Equal("APPROVED", record.Status);
        Assert.NotNull(record.Decision);
        Assert.StartsWith("docs/records/", record.Decision.Path);
        var decisionBytes = ReadPinned(root, record.Decision.Path, record.Decision.Sha256);
        using var decisionDoc = JsonDocument.Parse(decisionBytes);
        NoDuplicateKeys(decisionDoc.RootElement);
        var decision = JsonSerializer.Deserialize<OwnerDecision>(decisionBytes, AuthorityJson)!;
        Assert.Equal(syntheticFixture ? "SYNTHETIC_TEST_ONLY" : "DOCS_PATH_OWNER_DECISION", decision.Kind);
        Assert.Equal("APPROVED", decision.Status);
        Assert.False(string.IsNullOrWhiteSpace(decision.DecisionId), "missing decision identity");
        Assert.False(string.IsNullOrWhiteSpace(decision.ApprovedBy), "missing accountable owner");
        var payload = doc.RootElement.GetProperty("canonicalTargets").GetRawText() + "\n" +
            doc.RootElement.GetProperty("sealedInputs").GetRawText();
        Assert.Equal(Hash(Encoding.UTF8.GetBytes(payload)), decision.PayloadSha256);
        Assert.NotEmpty(record.CanonicalTargets);
        Assert.NotEmpty(record.SealedInputs);
        var targets = new HashSet<string>(StringComparer.Ordinal);
        foreach (var target in record.CanonicalTargets)
        {
            Assert.True(targets.Add(target.Path), "duplicate target");
            ReadPinned(root, target.Path, target.Sha256);
        }
        var paths = new HashSet<string>(StringComparer.Ordinal) { AuthorityPath };
        var usedTargets = new HashSet<string>(StringComparer.Ordinal);
        foreach (var seal in record.SealedInputs)
        {
            Assert.True(paths.Add(seal.Path), "duplicate sealed input");
            // Two immutable recovery snapshots only; all other paths retain the records requirement.
            var exactRecoverySnapshot = seal.Kind == "historical-data" &&
                ((seal.Path == "docs/roadmap/plans/mod-0183-root-uptake-recovery-01/input-manifest.json" &&
                  seal.Sha256 == "a70f79d7892d83ddbe772653a06cb009ebfe7e2b72549d11dd956f72ab9f83fe") ||
                 (seal.Path == "docs/roadmap/plans/mod-0183-root-uptake-recovery-01/baseline.json" &&
                  seal.Sha256 == "74e31359aaeb662645d224d9602cb801f8f81a433cc75cf09840d852fd2e878e"));
            Assert.True(seal.Path.StartsWith("docs/records/", StringComparison.Ordinal) ||
                exactRecoverySnapshot, "sealed input must be in records or an exact approved recovery snapshot");
            ReadPinned(root, seal.Path, seal.Sha256);
            Assert.StartsWith("docs/records/", seal.ProvenancePath);
            var proof = Encoding.UTF8.GetString(ReadPinned(root, seal.ProvenancePath, seal.ProvenanceSha256))
                .Replace("\r\n", "\n").Split('\n');
            Assert.InRange(seal.ProvenanceLine, 1, proof.Length);
            Assert.Matches("`" + Regex.Escape(seal.Path) + @":\d+`; baseline SHA256 `" + seal.Sha256 + "`",
                proof[seal.ProvenanceLine - 1]);
            Assert.Contains(seal.Kind, new[] { "active-tool", "historical-data", "historical-tool" });
            Assert.NotNull(seal.Targets);
            if (seal.Kind is "historical-data" or "historical-tool")
            {
                Assert.EndsWith(seal.Kind == "historical-data" ? ".json" : ".py", seal.Path);
                Assert.Empty(seal.Targets);
            }
            else
            {
                Assert.EndsWith(".py", seal.Path);
                Assert.NotEmpty(seal.Targets);
                foreach (var target in seal.Targets)
                {
                    Assert.Contains(target, targets);
                    usedTargets.Add(target);
                }
            }
        }
        Assert.True(targets.SetEquals(usedTargets), "unused canonical target");
        return paths;
    }

    // Synthetic approval exists only in isolated fixture directories. Repository entrypoint never opts in.
    private sealed class AuthorityFixture : IDisposable
    {
        public string Root { get; } = Path.Combine(Path.GetTempPath(), "docs-authority-fixture-" + Guid.NewGuid());
        public System.Text.Json.Nodes.JsonObject Record { get; }
        public string Tool => Record["sealedInputs"]!.AsArray().Select(x => x!.AsObject())
            .First(x => x["kind"]!.GetValue<string>() == "active-tool")["path"]!.GetValue<string>();
        public string History => Record["sealedInputs"]!.AsArray().Select(x => x!.AsObject())
            .First(x => x["kind"]!.GetValue<string>() == "historical-data")["path"]!.GetValue<string>();
        public string Target => Record["canonicalTargets"]![0]!["path"]!.GetValue<string>();
        public string Proof => Record["sealedInputs"]![0]!["provenancePath"]!.GetValue<string>();
        public string DecisionPath => "docs/records/decisions/synthetic-docs-path.json";
        public AuthorityFixture()
        {
            Directory.CreateDirectory(Root);
            var source = RepoRoot();
            Record = System.Text.Json.Nodes.JsonNode.Parse(File.ReadAllText(Path.Combine(source, AuthorityPath)))!.AsObject();
            var paths = Record["canonicalTargets"]!.AsArray().Select(x => x!["path"]!.GetValue<string>())
                .Concat(Record["sealedInputs"]!.AsArray().Select(x => x!["path"]!.GetValue<string>()))
                .Concat(Record["sealedInputs"]!.AsArray().Select(x => x!["provenancePath"]!.GetValue<string>())).Distinct();
            foreach (var path in paths)
            {
                var destination = Path.Combine(Root, path);
                Directory.CreateDirectory(Path.GetDirectoryName(destination)!);
                File.Copy(Path.Combine(source, path), destination);
            }
            Write("witness.cs", "// docs/reference/architecture/\n");
            Write("witness.ps1", "# docs/guides/operations/\n");
            Approve();
        }
        public void Write(string path, string content)
        {
            var target = Path.Combine(Root, path);
            Directory.CreateDirectory(Path.GetDirectoryName(target)!);
            File.WriteAllText(target, content);
        }
        public void Save() => Write(AuthorityPath, Record.ToJsonString());
        public void Approve()
        {
            Record["status"] = "APPROVED";
            using var d = JsonDocument.Parse(Record.ToJsonString());
            var digest = Hash(Encoding.UTF8.GetBytes(d.RootElement.GetProperty("canonicalTargets").GetRawText() + "\n" +
                d.RootElement.GetProperty("sealedInputs").GetRawText()));
            var decision = JsonSerializer.Serialize(new OwnerDecision("SYNTHETIC_TEST_ONLY", "APPROVED",
                "FIXTURE-NOT-OWNER-APPROVAL", "synthetic-fixture", digest), AuthorityJson);
            Write(DecisionPath, decision);
            Record["decision"] = new System.Text.Json.Nodes.JsonObject
            { ["path"] = DecisionPath, ["sha256"] = Hash(Encoding.UTF8.GetBytes(decision)) };
            Save();
        }
        public void Dispose() => Directory.Delete(Root, true);
    }

    [Fact]
    public void AuthorityFixture_ReadsExactFilesAndProvenance()
    {
        using var f = new AuthorityFixture();
        VerifyRoot(f.Root, syntheticFixture: true);
    }

    [Theory]
    [InlineData("canonical-sha-null")]
    [InlineData("canonical-sha-missing")]
    [InlineData("source-sha-null")]
    [InlineData("source-sha-missing")]
    [InlineData("provenance-sha-null")]
    [InlineData("provenance-sha-missing")]
    [InlineData("decision-sha-null")]
    [InlineData("decision-sha-missing")]
    [InlineData("unapproved")]
    [InlineData("missing-decision")]
    [InlineData("invalid-decision")]
    [InlineData("invalid-decision-kind")]
    [InlineData("missing-owner")]
    [InlineData("wrong-source-hash")]
    [InlineData("synthetic-production")]
    [InlineData("unknown-path")]
    [InlineData("wrong-canonical-hash")]
    [InlineData("missing-canonical")]
    [InlineData("changed-history")]
    [InlineData("unregistered-tool")]
    [InlineData("missing-provenance")]
    [InlineData("wrong-provenance-hash")]
    [InlineData("wrong-provenance-line")]
    [InlineData("payload-tampering")]
    [InlineData("unknown-manifest-field")]
    [InlineData("duplicate-json-key")]
    [InlineData("duplicate-source")]
    [InlineData("unknown-kind")]
    [InlineData("traversal")]
    [InlineData("wildcard")]
    [InlineData("symlink")]
    [InlineData("roadmap-neighbor")]
    [InlineData("roadmap-wrong-hash")]
    [InlineData("roadmap-active-tool")]
    [InlineData("empty-inputs")]
    [InlineData("missing-cs-presence")]
    [InlineData("missing-ps1-presence")]
    public void AuthorityFixture_RejectsInvalidEvidence(string scenario)
    {
        using var f = new AuthorityFixture();
        var first = f.Record["sealedInputs"]![0]!;
        if (scenario.Contains("-sha-"))
        {
            var parts = scenario.Split('-');
            var node = parts[0] switch
            {
                "canonical" => f.Record["canonicalTargets"]![0]!.AsObject(),
                "decision" => f.Record["decision"]!.AsObject(),
                _ => first.AsObject()
            };
            var key = parts[0] == "provenance" ? "provenanceSha256" : "sha256";
            if (parts[2] == "missing") node.Remove(key); else node[key] = null;
            if (parts[0] == "decision") f.Save(); else f.Approve();
        }
        else switch (scenario)
        {
            case "unapproved": f.Record["status"] = "UNAPPROVED"; f.Save(); break;
            case "missing-decision": File.Delete(Path.Combine(f.Root, f.DecisionPath)); break;
            case "invalid-decision": f.Write(f.DecisionPath, "{}"); break;
            case "invalid-decision-kind":
            case "missing-owner":
                var decision = System.Text.Json.Nodes.JsonNode.Parse(File.ReadAllText(Path.Combine(f.Root, f.DecisionPath)))!;
                decision[scenario == "missing-owner" ? "approvedBy" : "kind"] = scenario == "missing-owner" ? "" : "INVALID";
                f.Write(f.DecisionPath, decision.ToJsonString());
                f.Record["decision"]!["sha256"] = Hash(File.ReadAllBytes(Path.Combine(f.Root, f.DecisionPath)));
                f.Save(); break;
            case "wrong-source-hash": first["sha256"] = new string('0', 64); f.Approve(); break;
            case "synthetic-production": break;
            case "unknown-path": f.Write("new.cs", "// " + f.Target + ".unknown"); break;
            case "wrong-canonical-hash":
                f.Record["canonicalTargets"]![0]!["sha256"] = new string('0', 64); f.Approve(); break;
            case "missing-canonical": File.Delete(Path.Combine(f.Root, f.Target)); break;
            case "changed-history": File.AppendAllText(Path.Combine(f.Root, f.History), "\n "); break;
            case "unregistered-tool": f.Write("unregistered.py", File.ReadAllText(Path.Combine(f.Root, f.Tool))); break;
            case "missing-provenance": File.Delete(Path.Combine(f.Root, f.Proof)); break;
            case "wrong-provenance-hash": first["provenanceSha256"] = new string('0', 64); f.Approve(); break;
            case "wrong-provenance-line": first["provenanceLine"] = 1; f.Approve(); break;
            case "payload-tampering": first["kind"] = "tampered"; f.Save(); break;
            case "unknown-manifest-field": f.Record["unreviewed"] = f.Target; f.Save(); break;
            case "duplicate-json-key":
                f.Write(AuthorityPath, f.Record.ToJsonString().Insert(1, "\"status\":\"APPROVED\",")); break;
            case "duplicate-source": f.Record["sealedInputs"]!.AsArray().Add(first.DeepClone()); f.Approve(); break;
            case "unknown-kind": first["kind"] = "unknown"; f.Approve(); break;
            case "traversal": first["path"] = "../outside.json"; f.Approve(); break;
            case "wildcard": first["path"] = "docs/records/*.json"; f.Approve(); break;
            case "symlink":
                var target = Path.Combine(f.Root, f.Target);
                var contents = File.ReadAllText(target); File.Delete(target);
                f.Write("linked-target.txt", contents); File.CreateSymbolicLink(target, Path.Combine(f.Root, "linked-target.txt")); break;
            case "roadmap-neighbor":
            case "roadmap-wrong-hash":
            case "roadmap-active-tool":
                var recovery = f.Record["sealedInputs"]!.AsArray().First(x =>
                    x!["path"]!.GetValue<string>().StartsWith("docs/roadmap/", StringComparison.Ordinal))!;
                if (scenario == "roadmap-neighbor") recovery["path"] = "docs/roadmap/plans/unapproved-neighbor.json";
                if (scenario == "roadmap-wrong-hash") recovery["sha256"] = new string('0', 64);
                if (scenario == "roadmap-active-tool") recovery["kind"] = "active-tool";
                f.Approve();
                var pathError = Assert.ThrowsAny<Exception>(() => ReadAuthority(f.Root, syntheticFixture: true));
                Assert.Contains("sealed input must be in records or an exact approved recovery snapshot", pathError.Message);
                return;
            case "empty-inputs":
                Directory.Delete(f.Root, true); Directory.CreateDirectory(f.Root); break;
            case "missing-cs-presence": File.Delete(Path.Combine(f.Root, "witness.cs")); break;
            case "missing-ps1-presence": File.Delete(Path.Combine(f.Root, "witness.ps1")); break;
            default: throw new InvalidOperationException(scenario);
        }
        Assert.ThrowsAny<Exception>(() => VerifyRoot(f.Root, syntheticFixture: scenario != "synthetic-production"));
    }

    /// <summary>Every docs folder a line names — minus the ones that belong to a service's own docs tree.</summary>
    private static IEnumerable<string> Hits(string line, IReadOnlySet<string> nestedDocsParents)
    {
        foreach (Match m in Slashed.Matches(line))
        {
            var parent = SlashedParent.Match(line[..m.Index]);
            if (parent.Success && nestedDocsParents.Contains(parent.Groups["p"].Value)) continue;
            yield return m.Groups["x"].Value;
        }

        foreach (Match m in Combined.Matches(line))
        {
            var parent = CombinedParent.Match(line[..m.Index]);
            if (parent.Success && nestedDocsParents.Contains(parent.Groups["p"].Value)) continue;
            yield return m.Groups["x"].Value;
        }
    }

    /// <summary>
    /// Folders that carry a docs/ of their own below the root (today: Diten.AuthService, Diten.Platform).
    /// Found on disk so a new one needs no edit here — and so the exclusion can never outlive the folder.
    /// </summary>
    private static IReadOnlySet<string> NestedDocsParents(string root) =>
        Directories(root)
            .Where(d => Path.GetFileName(d) == "docs")
            .Select(d => Path.GetDirectoryName(d)!)
            .Where(parent => !string.Equals(parent, root, StringComparison.Ordinal))
            .Select(parent => Path.GetFileName(parent)!)
            .ToHashSet(StringComparer.Ordinal);

    private static IEnumerable<string> CodeFiles(string root) =>
        Directories(root)
            .Prepend(root)
            .SelectMany(dir => Directory.EnumerateFiles(dir))
            .Where(file => CodeExtensions.Contains(Path.GetExtension(file)));

    /// <summary>Every directory below the root, pruned as it walks — node_modules is never entered, not filtered after.</summary>
    private static IEnumerable<string> Directories(string root)
    {
        var pending = new Stack<string>();
        pending.Push(root);

        while (pending.Count > 0)
        {
            foreach (var child in Directory.EnumerateDirectories(pending.Pop()))
            {
                if (PrunedDirectoryNames.Contains(Path.GetFileName(child))) continue;

                var relative = Path.GetRelativePath(root, child).Replace('\\', '/');
                if (PrunedPaths.Any(p => relative == p || relative.StartsWith(p + "/", StringComparison.Ordinal))) continue;

                yield return child;
                pending.Push(child);
            }
        }
    }

    /// <summary>The repository root — the same walk-up-to-AGENTS.md as PlatformSchemaManifestTests.RepoRoot().</summary>
    private static string RepoRoot()
    {
        var current = new DirectoryInfo(AppContext.BaseDirectory);
        while (current is not null)
        {
            if (File.Exists(Path.Combine(current.FullName, "AGENTS.md"))) return current.FullName;
            current = current.Parent;
        }

        throw new InvalidOperationException("Repo root not found.");
    }
}
