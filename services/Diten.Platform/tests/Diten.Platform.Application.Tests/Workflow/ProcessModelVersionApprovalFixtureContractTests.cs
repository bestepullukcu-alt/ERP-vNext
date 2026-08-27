using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using System.Text.Json.Nodes;
using System.Text.RegularExpressions;
using Xunit;

namespace Diten.Platform.Application.Tests.Workflow;

public sealed class ProcessModelVersionApprovalFixtureContractTests
{
    private static readonly string FixtureRoot = FindFixtureRoot();
    private static readonly DateTimeOffset Now = DateTimeOffset.Parse("2026-08-26T10:00:15Z");

    [Fact]
    public void Manifest_and_all_canonical_payload_sidecars_are_exact()
    {
        using var manifest = Load("fixture-manifest.json");
        var root = manifest.RootElement;
        Assert.Equal("management-governance.process-model-version-context/1.0", root.GetProperty("profile").GetString());
        Assert.Equal("management-governance.process-model-version-source-attestation/1.0", root.GetProperty("attestation").GetString());
        Assert.Equal("NON-RUNTIME_CONTRACT_TEST_ONLY", root.GetProperty("disposition").GetString());
        Assert.Equal(16, root.GetProperty("negativeCaseCount").GetInt32());

        var files = root.GetProperty("sourceFiles").EnumerateArray().Select(item => item.GetString()!).ToArray();
        Assert.Equal(7, files.Length);
        foreach (var file in files)
        {
            var canonicalBytes = ReadCanonicalBytes(file);
            var actual = Convert.ToHexString(SHA256.HashData(canonicalBytes)).ToLowerInvariant();
            Assert.Equal(actual + "\n", File.ReadAllText(Path.Combine(FixtureRoot, file + ".sha256")));
        }

        var request = ReadCanonicalBytes("canonical-submission-request.json");
        Assert.Equal(root.GetProperty("canonicalRequestBytes").GetInt32(), request.Length);
        Assert.Equal(root.GetProperty("canonicalRequestSha256").GetString(),
            Convert.ToHexString(SHA256.HashData(request)).ToLowerInvariant());
    }

    [Fact]
    public void Positive_fixture_binds_source_attestation_mapping_outcome_and_reference()
    {
        var evidence = Evidence.Load();
        EvidenceValidator.Validate(evidence, Now);

        Assert.Equal(evidence.Source["ProcessModelId"]!.GetValue<string>(), evidence.Outcome["ProcessModelId"]!.GetValue<string>());
        Assert.Equal(evidence.Source["ProcessModelVersionId"]!.GetValue<string>(), evidence.Outcome["ProcessModelVersionId"]!.GetValue<string>());
        Assert.Equal(evidence.Source["ContentHash"]!.GetValue<string>(), evidence.Outcome["ContentHash"]!.GetValue<string>());
        Assert.Equal(evidence.Mapping["WorkflowTemplateVersionId"]!.GetValue<string>(), evidence.Outcome["WorkflowTemplateVersionId"]!.GetValue<string>());
        Assert.Equal(evidence.Outcome["ApprovalOutcomeId"]!.GetValue<string>(), evidence.Reference["ApprovalOutcomeId"]!.GetValue<string>());
    }

    [Fact]
    public void Source_and_outcome_reference_are_closed_and_copy_no_foreign_aggregate_fields()
    {
        var evidence = Evidence.Load();
        AssertExactFields(evidence.Source,
            "ContractName", "ContractVersion", "ProcessModelId", "ProcessModelVersionId", "ContentHash");
        AssertExactFields(evidence.Reference, "ContractName", "ContractVersion", "ApprovalOutcomeId");

        var forbidden = new[]
        {
            "TenantId", "ActorId", "AuthorId", "ApproverId", "WorkflowTemplateVersionId", "TaskId",
            "Route", "Comment", "Activities", "ControlPoints", "RoleBindings", "KpiBindings", "ModelContent",
            "FinalState", "DecisionActorId", "DecidedAtUtc"
        };
        Assert.All(forbidden, field => Assert.False(evidence.Source.ContainsKey(field), field));
        Assert.All(forbidden, field => Assert.False(evidence.Reference.ContainsKey(field), field));
    }

    [Fact]
    public void Attestation_header_claims_and_hard_thirty_second_window_are_exact()
    {
        var evidence = Evidence.Load();
        AssertExactFields(evidence.Header, "alg", "kid", "typ");
        Assert.Equal("RS256", evidence.Header["alg"]!.GetValue<string>());
        Assert.Equal("test-only-mod0355-pmv-v1", evidence.Header["kid"]!.GetValue<string>());
        Assert.Equal("diten-process-model-version-source-attestation+jwt", evidence.Header["typ"]!.GetValue<string>());

        AssertExactFields(evidence.Claims,
            "ContractName", "ContractVersion", "TenantId", "SubmittingServiceId", "EffectiveActorId",
            "DelegatedActorChain", "ProcessModelId", "ProcessModelVersionId", "ContentHash",
            "WorkflowTemplateVersionId", "SubmissionRequestDigest", "IssuedAtUtc", "ExpiresAtUtc", "Nonce");
        var issued = DateTimeOffset.Parse(evidence.Claims["IssuedAtUtc"]!.GetValue<string>());
        var expires = DateTimeOffset.Parse(evidence.Claims["ExpiresAtUtc"]!.GetValue<string>());
        Assert.Equal(TimeSpan.FromSeconds(30), expires - issued);
    }

    [Fact]
    public void Canonical_request_is_fixed_order_utf8_and_request_bound()
    {
        const string expected = "{\"ContractName\":\"management-governance.process-model-version-context\",\"ContractVersion\":\"1.0\",\"ContentHash\":\"sha256:0123456789abcdef0123456789abcdef0123456789abcdef0123456789abcdef\",\"ProcessModelId\":\"11111111-1111-1111-1111-111111111111\",\"ProcessModelVersionId\":\"22222222-2222-2222-2222-222222222222\",\"WorkflowTemplateVersionId\":\"33333333-3333-3333-3333-333333333333\"}";
        var bytes = ReadCanonicalBytes("canonical-submission-request.json");
        Assert.Equal(expected, Encoding.UTF8.GetString(bytes));
        var digest = Convert.ToHexString(SHA256.HashData(bytes)).ToLowerInvariant();
        Assert.Equal(Evidence.Load().Claims["SubmissionRequestDigest"]!.GetValue<string>(), digest);
    }

    [Fact]
    public void All_declared_negative_cases_fail_with_the_exact_terminal_status()
    {
        using var cases = Load("negative-cases.json");
        var executed = 0;
        foreach (var item in cases.RootElement.GetProperty("cases").EnumerateArray())
        {
            var name = item.GetProperty("name").GetString()!;
            var expected = item.GetProperty("status").GetInt32();
            var evidence = Evidence.Load();
            ApplyMutation(evidence, name);
            var failure = Assert.Throws<ContractFailure>(() => EvidenceValidator.Validate(evidence, Now));
            Assert.True(expected == failure.Status,
                $"Negative case {name} expected HTTP {expected} but produced {failure.Status}.");
            executed++;
        }
        Assert.Equal(16, executed);
    }

    private static void ApplyMutation(Evidence evidence, string name)
    {
        const string other = "77777777-7777-7777-7777-777777777777";
        switch (name)
        {
            case "source_unknown_field": evidence.Source["TenantId"] = other; break;
            case "source_wrong_contract_version": evidence.Source["ContractVersion"] = "2.0"; break;
            case "source_model_mismatch": evidence.Source["ProcessModelId"] = other; break;
            case "source_version_mismatch": evidence.Source["ProcessModelVersionId"] = other; break;
            case "attestation_tenant_mismatch": evidence.Claims["TenantId"] = other; break;
            case "attestation_hash_mismatch": evidence.Claims["ContentHash"] = "sha256:aaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaa"; break;
            case "attestation_template_mismatch": evidence.Claims["WorkflowTemplateVersionId"] = other; break;
            case "attestation_expired": evidence.Claims["ExpiresAtUtc"] = "2026-08-26T10:00:10Z"; break;
            case "attestation_future_issued": evidence.Claims["IssuedAtUtc"] = "2026-08-26T10:00:16Z"; break;
            case "mapping_changed": evidence.Mapping["WorkflowTemplateVersionId"] = other; break;
            case "mapping_not_published": evidence.Mapping["TemplateVersionStatus"] = "Draft"; break;
            case "mapping_mutable": evidence.Mapping["IsImmutable"] = false; break;
            case "mapping_wrong_tenant": evidence.Mapping["TenantId"] = other; break;
            case "outcome_rejected": evidence.Outcome["FinalState"] = "Rejected"; break;
            case "outcome_source_mismatch": evidence.Outcome["ProcessModelVersionId"] = other; break;
            case "outcome_reference_fourth_field": evidence.Reference["TenantId"] = other; break;
            default: throw new InvalidOperationException(name);
        }
    }

    private static JsonDocument Load(string name) => JsonDocument.Parse(ReadCanonicalBytes(name));

    private static byte[] ReadCanonicalBytes(string name)
    {
        var bytes = File.ReadAllBytes(Path.Combine(FixtureRoot, name));
        Assert.NotEmpty(bytes);
        Assert.Equal((byte)'\n', bytes[^1]);
        Assert.DoesNotContain((byte)'\r', bytes);
        Assert.False(bytes.AsSpan().StartsWith(Encoding.UTF8.GetPreamble()));
        return bytes[..^1];
    }

    private static void AssertExactFields(JsonObject value, params string[] expected) =>
        Assert.Equal(expected, value.Select(property => property.Key));

    private static string FindFixtureRoot()
    {
        foreach (var start in new[] { Directory.GetCurrentDirectory(), AppContext.BaseDirectory })
        {
            var directory = new DirectoryInfo(start);
            while (directory is not null)
            {
                var candidate = Path.Combine(directory.FullName, "execution", "domains", "platform-shared-services",
                    "module-packs", "fixtures", "MOD-0023", "process-model-version-approval", "v1");
                if (Directory.Exists(candidate)) return candidate;
                directory = directory.Parent;
            }
        }
        throw new DirectoryNotFoundException("MOD-0023 B-05b fixture root was not found.");
    }

    private sealed record Evidence(JsonObject Source, JsonObject Request, JsonObject Header, JsonObject Claims,
        JsonObject Mapping, JsonObject Outcome, JsonObject Reference)
    {
        public static Evidence Load() => new(
            Node("source-reference.json"), Node("canonical-submission-request.json"),
            Node("source-attestation-header.json"), Node("source-attestation-claims.json"),
            Node("template-version-mapping.json"), Node("terminal-outcome-binding.json"),
            Node("approval-outcome-reference-v1.json"));

        private static JsonObject Node(string name) => JsonNode.Parse(ReadCanonicalBytes(name))!.AsObject();
    }

    private sealed class ContractFailure(int status) : Exception
    {
        public int Status { get; } = status;
    }

    private static class EvidenceValidator
    {
        private static readonly Regex Hash = new("^sha256:[0-9a-f]{64}$", RegexOptions.CultureInvariant);

        public static void Validate(Evidence e, DateTimeOffset now)
        {
            Exact(e.Source, 400, "ContractName", "ContractVersion", "ProcessModelId", "ProcessModelVersionId", "ContentHash");
            if (Text(e.Source, "ContractName") != "management-governance.process-model-version-context" || Text(e.Source, "ContractVersion") != "1.0") Fail(400);
            CanonicalUuid(Text(e.Source, "ProcessModelId"), 400);
            CanonicalUuid(Text(e.Source, "ProcessModelVersionId"), 400);
            if (!Hash.IsMatch(Text(e.Source, "ContentHash"))) Fail(400);

            Exact(e.Request, 400, "ContractName", "ContractVersion", "ContentHash", "ProcessModelId", "ProcessModelVersionId", "WorkflowTemplateVersionId");
            Equal(e.Source, e.Request, 400, "ContractName", "ContractVersion", "ContentHash", "ProcessModelId", "ProcessModelVersionId");
            CanonicalUuid(Text(e.Request, "WorkflowTemplateVersionId"), 400);

            Exact(e.Header, 401, "alg", "kid", "typ");
            if (Text(e.Header, "alg") != "RS256" || Text(e.Header, "typ") != "diten-process-model-version-source-attestation+jwt" || Text(e.Header, "kid") != "test-only-mod0355-pmv-v1") Fail(401);
            Exact(e.Claims, 400, "ContractName", "ContractVersion", "TenantId", "SubmittingServiceId", "EffectiveActorId", "DelegatedActorChain", "ProcessModelId", "ProcessModelVersionId", "ContentHash", "WorkflowTemplateVersionId", "SubmissionRequestDigest", "IssuedAtUtc", "ExpiresAtUtc", "Nonce");
            if (Text(e.Claims, "ContractName") != "management-governance.process-model-version-source-attestation" || Text(e.Claims, "ContractVersion") != "1.0" || Text(e.Claims, "SubmittingServiceId") != "diten.management-governance") Fail(400);
            if (Text(e.Claims, "TenantId") != "44444444-4444-4444-4444-444444444444") Fail(400);
            Equal(e.Source, e.Claims, 400, "ProcessModelId", "ProcessModelVersionId", "ContentHash");
            if (Text(e.Request, "WorkflowTemplateVersionId") != Text(e.Claims, "WorkflowTemplateVersionId")) Fail(400);
            var requestDigest = Convert.ToHexString(SHA256.HashData(ReadCanonicalBytes("canonical-submission-request.json"))).ToLowerInvariant();
            if (Text(e.Claims, "SubmissionRequestDigest") != requestDigest) Fail(400);
            var issued = DateTimeOffset.Parse(Text(e.Claims, "IssuedAtUtc"));
            var expires = DateTimeOffset.Parse(Text(e.Claims, "ExpiresAtUtc"));
            if (issued > now || expires < now || expires <= issued || expires - issued > TimeSpan.FromSeconds(30)) Fail(400);

            Exact(e.Mapping, 409, "ContractName", "ContractVersion", "TenantId", "WorkflowTemplateVersionId", "TemplateVersionStatus", "IsImmutable", "IsProfileEligible");
            if (Text(e.Mapping, "TenantId") != Text(e.Claims, "TenantId")) Fail(404);
            if (Text(e.Mapping, "WorkflowTemplateVersionId") != Text(e.Request, "WorkflowTemplateVersionId")) Fail(409);
            if (Text(e.Mapping, "TemplateVersionStatus") != "Published" || !Bool(e.Mapping, "IsImmutable") || !Bool(e.Mapping, "IsProfileEligible")) Fail(409);

            Exact(e.Outcome, 409, "ApprovalOutcomeId", "TenantId", "ProcessModelId", "ProcessModelVersionId", "ContentHash", "WorkflowTemplateVersionId", "FinalState", "DecisionActorId", "DecidedAtUtc");
            if (Text(e.Outcome, "TenantId") != Text(e.Claims, "TenantId")) Fail(404);
            Equal(e.Source, e.Outcome, 409, "ProcessModelId", "ProcessModelVersionId", "ContentHash");
            if (Text(e.Outcome, "WorkflowTemplateVersionId") != Text(e.Request, "WorkflowTemplateVersionId")) Fail(409);
            if (Text(e.Outcome, "FinalState") != "Approved") Fail(403);

            Exact(e.Reference, 400, "ContractName", "ContractVersion", "ApprovalOutcomeId");
            if (Text(e.Reference, "ContractName") != "platform.approval-outcome-reference" || Text(e.Reference, "ContractVersion") != "1.0") Fail(400);
            if (Text(e.Reference, "ApprovalOutcomeId") != Text(e.Outcome, "ApprovalOutcomeId")) Fail(409);
        }

        private static void Exact(JsonObject value, int status, params string[] expected)
        {
            if (!value.Select(property => property.Key).SequenceEqual(expected, StringComparer.Ordinal)) Fail(status);
        }

        private static void Equal(JsonObject left, JsonObject right, int status, params string[] fields)
        {
            if (fields.Any(field => Text(left, field) != Text(right, field))) Fail(status);
        }

        private static string Text(JsonObject value, string field) => value[field]?.GetValue<string>() ?? throw new ContractFailure(400);
        private static bool Bool(JsonObject value, string field) => value[field]?.GetValue<bool>() ?? throw new ContractFailure(400);
        private static void CanonicalUuid(string value, int status)
        {
            if (!Guid.TryParseExact(value, "D", out var parsed) || parsed == Guid.Empty || parsed.ToString("D") != value) Fail(status);
        }
        private static void Fail(int status) => throw new ContractFailure(status);
    }
}
