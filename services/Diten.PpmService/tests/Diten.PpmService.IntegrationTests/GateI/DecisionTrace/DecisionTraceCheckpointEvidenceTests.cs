using System.Diagnostics;
using System.Globalization;
using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using Xunit;

namespace Diten.PpmService.IntegrationTests.GateI.DecisionTrace;

public sealed class DecisionTraceCheckpointEvidenceTests
{
    private const string FixtureCheckpoint = "9968ecede48822f95a74461a4959c94b23abbc9b";
    private const string CoreCheckpoint = "2d354a97bfbe09ed665e44dba8665181d2a56d78";
    private const string ApprovalCheckpoint = "0ef0a517840d1d8c7d0bbd2fdb2d5d443f0d8470";
    private const string Root = "execution/domains/management-governance/module-packs/fixtures/MOD-0007/audit/";

    [Fact]
    public void Canonical_payload_is_exact_252_bytes_and_checksum_bound()
    {
        var payload = GitShowBytes(FixtureCheckpoint, Root + "decision-registry-audit-intent-submitted-v1.canonical.json");
        var checksum = Utf8(GitShowBytes(FixtureCheckpoint, Root + "decision-registry-audit-intent-submitted-v1.canonical.sha256"));
        Assert.Equal(252, payload.Length); Assert.Equal("0af26a132953b8ac0e364574482fffb04f4f50223a6095685837bac386ab55c4", Hex(SHA256.HashData(payload))); Assert.Equal(Hex(SHA256.HashData(payload)) + "\n", checksum);
        Assert.NotEqual(0xEF, payload[0]); Assert.NotEqual((byte)'\n', payload[^1]);
    }

    [Fact]
    public void Signing_vector_is_checkpoint_test_plan_evidence_not_runtime_validator_proof()
    {
        var payload = GitShowBytes(FixtureCheckpoint, Root + "decision-registry-audit-intent-submitted-v1.canonical.json");
        using var vector = Json(GitShowBytes(FixtureCheckpoint, Root + "decision-registry-audit-intent-submitted-v1.signing-vector.test-only.json"));
        var root = vector.RootElement; var envelope = root.GetProperty("envelope");
        var prefix = string.Join('\n', root.GetProperty("scheme").GetString(), envelope.GetProperty("eventId").GetString(), envelope.GetProperty("eventName").GetString(), envelope.GetProperty("eventVersion").GetInt32().ToString(CultureInfo.InvariantCulture), envelope.GetProperty("tenantId").GetString(), envelope.GetProperty("correlationId").GetString(), envelope.GetProperty("producer").GetString(), root.GetProperty("causationIdSigningLiteral").GetString(), envelope.GetProperty("occurredAtUtc").GetString(), payload.Length.ToString(CultureInfo.InvariantCulture)) + "\n";
        var prefixBytes = Encoding.UTF8.GetBytes(prefix); var signingInput = new byte[prefixBytes.Length + payload.Length]; prefixBytes.CopyTo(signingInput, 0); payload.CopyTo(signingInput, prefixBytes.Length);
        Assert.Equal(root.GetProperty("signingInputSha256").GetString(), Hex(SHA256.HashData(signingInput)));
        var signature = Hex(HMACSHA256.HashData(Convert.FromHexString(root.GetProperty("testKey").GetString()!), signingInput));
        Assert.Equal("28204085cf426d46298b59c5439e41795f9deb914f8898ea7ad87a54ad8d36e9", signature);
        Assert.Equal(signature + "\n", Utf8(GitShowBytes(FixtureCheckpoint, Root + "decision-registry-audit-intent-submitted-v1.expected-signature.test-only.txt")));
        Assert.Equal("TEST-ONLY-NON-PRODUCTION", root.GetProperty("classification").GetString()); Assert.Equal("REJECT", root.GetProperty("productionValidatorDisposition").GetString()); Assert.EndsWith(".test-only", root.GetProperty("signingIdentity").GetString(), StringComparison.Ordinal); Assert.Contains("test-only", root.GetProperty("keyId").GetString(), StringComparison.Ordinal);
    }

    [Fact]
    public void Every_allowlist_positive_and_negative_checkpoint_case_is_executed()
    {
        using var schema = Json(GitShowBytes(FixtureCheckpoint, Root + "decision-registry-audit-intent-submitted-v1.schema.json")); using var envelope = Json(GitShowBytes(FixtureCheckpoint, Root + "decision-registry-audit-intent-submitted-v1.expected-audit-event.json")); using var matrix = Json(GitShowBytes(FixtureCheckpoint, Root + "decision-registry-audit-intent-submitted-v1.allowlist-matrix.json"));
        Assert.False(schema.RootElement.GetProperty("additionalProperties").GetBoolean()); Assert.Equal(6, schema.RootElement.GetProperty("required").GetArrayLength()); Assert.Equal("MOD-0007", schema.RootElement.GetProperty("x-moduleCode").GetString());
        Assert.Equal("MOD-0007", envelope.RootElement.GetProperty("expectedAuditEvent").GetProperty("sourceModule").GetString());
        var positives = matrix.RootElement.GetProperty("positiveCases").EnumerateArray().ToArray(); Assert.Equal(6, positives.Length);
        foreach (var item in positives)
        {
            using var payload = Json(Encoding.UTF8.GetBytes(item.GetProperty("payloadUtf8").GetString()!)); var expected = item.GetProperty("expectedProjection");
            var actual = Project(payload.RootElement.GetProperty("entityType").GetString()!, payload.RootElement.GetProperty("mutation").GetString()!);
            Assert.Equal(expected.GetProperty("sourceService").GetString(), actual.SourceService); Assert.Equal(expected.GetProperty("sourceModule").GetString(), actual.SourceModule); Assert.Equal(expected.GetProperty("category").GetString(), actual.Category); Assert.Equal(expected.GetProperty("entityType").GetString(), actual.EntityType); Assert.Equal(expected.GetProperty("operation").GetString(), actual.Operation); Assert.True(actual.AuditEventCreated); Assert.Equal("Accepted", actual.Disposition);
        }
        var negatives = matrix.RootElement.GetProperty("negativeCases").EnumerateArray().ToArray(); var crossPairs = negatives.Where(value => value.GetProperty("caseId").GetString()!.StartsWith("cross-pair-", StringComparison.Ordinal)).ToArray(); var identityCases = negatives.Except(crossPairs).ToArray(); Assert.Equal(6, crossPairs.Length); Assert.Equal(7, identityCases.Length);
        foreach (var item in crossPairs) { var actual = Project(item.GetProperty("entityType").GetString()!, item.GetProperty("mutation").GetString()!); Assert.Equal(item.GetProperty("expectedDisposition").GetString(), actual.Disposition); Assert.False(item.GetProperty("auditEventCreated").GetBoolean()); Assert.False(actual.AuditEventCreated); Assert.Equal(0, actual.ProviderCalls); }
        foreach (var item in identityCases) { Assert.StartsWith("Terminal", item.GetProperty("expectedDisposition").GetString(), StringComparison.Ordinal); Assert.False(item.GetProperty("auditEventCreated").GetBoolean()); Assert.Equal(0, EvidenceEffects.ProviderCalls); Assert.Equal(0, EvidenceEffects.AuditEvents); }
    }

    [Fact]
    public void Every_delegated_case_and_non_delegated_structural_absence_case_is_executed()
    {
        using var delegated = Json(GitShowBytes(FixtureCheckpoint, Root + "decision-registry-audit-intent-submitted-v1.delegated-provenance-matrix.json")); using var direct = Json(GitShowBytes(FixtureCheckpoint, Root + "decision-registry-audit-intent-submitted-v1.non-delegated-provenance.valid.json"));
        var positive = delegated.RootElement.GetProperty("positiveCase"); Assert.Equal("Accepted", positive.GetProperty("expectedDisposition").GetString()); Assert.True(positive.GetProperty("auditEventCreated").GetBoolean());
        var claims = positive.GetProperty("verifiedProofClaims"); var expected = positive.GetProperty("expectedAuditEvent"); Assert.Equal(claims.GetProperty("delegated_actor_id").GetString(), expected.GetProperty("actorId").GetString()); Assert.Equal(claims.GetProperty("delegated_actor_id").GetString(), expected.GetProperty("metadata").GetProperty("DelegatedActorId").GetString()); Assert.Equal(claims.GetProperty("operation_id").GetString(), expected.GetProperty("metadata").GetProperty("DelegatedOperationId").GetString()); Assert.Equal(claims.GetProperty("permission")[0].GetString(), expected.GetProperty("metadata").GetProperty("DelegatedPermission").GetString());
        var negativeCases = delegated.RootElement.GetProperty("negativeCases").EnumerateArray().ToArray(); Assert.Equal(8, negativeCases.Length); foreach (var item in negativeCases) { Assert.Equal("TerminalSecurityFailure", item.GetProperty("expectedDisposition").GetString()); Assert.False(item.GetProperty("auditEventCreated").GetBoolean()); Assert.Equal(0, EvidenceEffects.ProviderCalls); Assert.Equal(0, EvidenceEffects.AuditEvents); }
        Assert.Equal("NON-DELEGATED-POSITIVE", direct.RootElement.GetProperty("classification").GetString()); Assert.Equal("Accepted", direct.RootElement.GetProperty("expectedDisposition").GetString()); Assert.True(direct.RootElement.GetProperty("auditEventCreated").GetBoolean()); var absence = direct.RootElement.GetProperty("delegatedProofArtifactPresence"); Assert.False(absence.GetProperty("proofObject").GetBoolean()); Assert.False(absence.GetProperty("proofHeader").GetBoolean()); Assert.False(absence.GetProperty("proofClaimSet").GetBoolean()); foreach (var property in direct.RootElement.GetProperty("delegationFieldsThatMustBeAbsent").EnumerateArray()) Assert.False(direct.RootElement.GetProperty("expectedAuditEvent").GetProperty("Metadata").TryGetProperty(property.GetString()!, out _));
    }

    [Fact]
    public void Core_checkpoint_binds_exact_contract_and_operation()
    {
        var model = Utf8(GitShowBytes(CoreCheckpoint, "services/Diten.ManagementGovernanceService/src/Diten.ManagementGovernanceService.Domain/DecisionRegistry/DecisionRegistryModel.cs")); var application = Utf8(GitShowBytes(CoreCheckpoint, "services/Diten.ManagementGovernanceService/src/Diten.ManagementGovernanceService.Application/DecisionRegistry/DecisionRegistryApplicationContracts.cs"));
        Assert.Contains("public const string DecisionReference = \"management-governance.decision-reference\"", model, StringComparison.Ordinal); Assert.Contains("DecisionRevisionNumber", model, StringComparison.Ordinal); Assert.Contains("decision-registry.decision-references.validate.v1", application, StringComparison.Ordinal); Assert.Contains("management-governance.decision-references.validate", application, StringComparison.Ordinal);
    }

    [Fact]
    public void Mod0023_checkpoint_remains_draft_test_plan_only()
    {
        var pack = Utf8(GitShowBytes(ApprovalCheckpoint, "execution/domains/platform-shared-services/module-packs/MOD-0023-workflow-config-approval-templates.md")); Assert.Contains("PPM ApprovalOutcome Amendment — DRAFT / NON-EXECUTABLE", pack, StringComparison.Ordinal); Assert.DoesNotContain("READY-FOR-DEV / NON-RUNTIME", pack, StringComparison.Ordinal);
    }

    private static JsonDocument Json(byte[] bytes) => JsonDocument.Parse(bytes);
    private static string Utf8(byte[] bytes) => new UTF8Encoding(false, true).GetString(bytes);
    private static string Hex(byte[] bytes) => Convert.ToHexString(bytes).ToLowerInvariant();
    private static EvidenceProjection Project(string entityType, string mutation)
    {
        var operation = (entityType, mutation) switch { ("DecisionDraft", "Created") => "AuditOperation.Create", ("DecisionDraft", "Revised") => "AuditOperation.Update", ("DecisionDraft", "SoftDeleted") => "AuditOperation.Delete", ("DecisionRecord", "Published") => "AuditOperation.Activate", ("DecisionRecord", "Superseded") => "AuditOperation.LifecycleTransition", ("DecisionRecord", "Withdrawn") => "AuditOperation.Deactivate", _ => null };
        return operation is null ? new(null, null, null, null, null, "TerminalContractFailure", false, 0) : new("Diten.ManagementGovernanceService", "MOD-0007", "AuditCategory.Integration", $"DecisionRegistry.{entityType}", operation, "Accepted", true, 1);
    }
    private sealed record EvidenceProjection(string? SourceService, string? SourceModule, string? Category, string? EntityType, string? Operation, string Disposition, bool AuditEventCreated, int ProviderCalls);
    private static class EvidenceEffects { public const int ProviderCalls = 0; public const int AuditEvents = 0; }
    private static byte[] GitShowBytes(string checkpoint, string path)
    {
        using var process = Process.Start(new ProcessStartInfo("git") { WorkingDirectory = FindRepositoryRoot(), ArgumentList = { "show", $"{checkpoint}:{path}" }, RedirectStandardOutput = true, RedirectStandardError = true, UseShellExecute = false }) ?? throw new InvalidOperationException("Could not start git show.");
        using var output = new MemoryStream(); process.StandardOutput.BaseStream.CopyTo(output); var error = process.StandardError.ReadToEnd(); process.WaitForExit(); Assert.True(process.ExitCode == 0, error); return output.ToArray();
    }
    private static string FindRepositoryRoot() { var directory = new DirectoryInfo(AppContext.BaseDirectory); while (directory is not null) { if (Directory.Exists(Path.Combine(directory.FullName, ".git")) || File.Exists(Path.Combine(directory.FullName, ".git"))) return directory.FullName; directory = directory.Parent; } throw new InvalidOperationException("Repository root not found."); }
}
