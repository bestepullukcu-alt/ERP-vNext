using System.Diagnostics;
using System.Reflection;
using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using Diten.PpmService.Application.Features.InvestmentCases.GateI.FundingScenario;
using Diten.PpmService.Application.GateI;
using Diten.PpmService.Domain.GateI.FundingScenario;
using Xunit;

namespace Diten.PpmService.Tests.GateI.FundingScenario;


public sealed class FundingScenarioArchitectureAndFixtureTests
{
    [Fact]
    public void Authorized_lane_is_split_by_contract_and_keeps_forbidden_runtime_dependencies_out()
    {
        var root=ServiceRoot();var files=Directory.GetFiles(root,"*",SearchOption.AllDirectories).Where(x=>x.Contains($"{Path.DirectorySeparatorChar}GateI{Path.DirectorySeparatorChar}FundingScenario{Path.DirectorySeparatorChar}",StringComparison.Ordinal)&&!x.Contains($"{Path.DirectorySeparatorChar}bin{Path.DirectorySeparatorChar}")&&!x.Contains($"{Path.DirectorySeparatorChar}obj{Path.DirectorySeparatorChar}")).ToArray();
        var source=files.Where(x=>x.Contains($"{Path.DirectorySeparatorChar}src{Path.DirectorySeparatorChar}")).ToArray();
        var expectedFiles=new[]{"BudgetReferenceValidationRequest.cs","BudgetVersionReferenceV1.cs","ComparatorOutputReferenceV1.cs","FundingScenarioAtomicLane.cs","FundingScenarioContractEvaluator.cs","FundingScenarioContractResult.cs","FundingScenarioContractValidator.cs","FundingScenarioFailureCodes.cs","FundingScenarioProducerProfile.cs","FundingScenarioReferenceKind.cs","FundingScenarioRequestBinding.cs","FundingScenarioValidationMode.cs","IBudgetVersionReferenceValidationPort.cs","IScenarioPlanningReferenceValidationPort.cs","InvestmentCaseComparatorOutputReferenceV1.cs","InvestmentCaseContextV1.cs","InvestmentCaseScenarioVersionReferenceV1.cs","ProducerReferenceState.cs","ProducerReferenceValidationResult.cs","S2SAuthenticationState.cs","S2SAuthorizationState.cs","S2SFreshnessState.cs","S2SFundingScenarioContextV1.cs","S2SVersionFenceV1.cs","ScenarioReferenceValidationRequest.cs","ScenarioVersionReferenceV1.cs","SelectedBudgetVersionReferenceV1.cs","SelectedScenarioReferenceV1.cs"};
        Assert.Equal(expectedFiles.Order(StringComparer.Ordinal),source.Select(Path.GetFileName).Order(StringComparer.Ordinal));
        Assert.All(source,path=>Assert.True(path.Contains("Diten.PpmService.Domain/GateI/FundingScenario/",StringComparison.Ordinal)||path.Contains("Diten.PpmService.Application/Features/InvestmentCases/GateI/FundingScenario/",StringComparison.Ordinal),path));
        var text=string.Join('\n',source.Select(File.ReadAllText));foreach(var forbidden in new[]{"MongoDB","IMongo","DbContext","HttpClient","IServiceCollection","Controller","Endpoint","IEventBus","PublishAsync","RabbitMQ","MassTransit","BackgroundService","appsettings"})Assert.DoesNotContain(forbidden,text,StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public void Atomic_lane_keeps_distinct_owner_operation_permission_and_signing_fixture_profiles()
    {
        Assert.Equal(2,FundingScenarioAtomicLane.RequiredProfiles.Count);
        var b=FundingScenarioAtomicLane.Budgeting;var s=FundingScenarioAtomicLane.ScenarioPlanning;
        Assert.Equal("MOD-0136",b.OwnerModule);Assert.Equal("MOD-0138",s.OwnerModule);
        Assert.Equal("budgeting.budget-version-references.validate",b.OperationId);Assert.Equal(b.OperationId,b.Permission);
        Assert.Equal("fpa.scenario-planning.references.validate",s.OperationId);Assert.Equal(s.OperationId,s.Permission);
        Assert.Equal(b.Audience,s.Audience);Assert.Equal(b.ClientId,s.ClientId);
        Assert.NotEqual(b.SigningIdentity,s.SigningIdentity);Assert.NotEqual(b.FixtureKeyId,s.FixtureKeyId);Assert.NotEqual(b.FixtureClosure,s.FixtureClosure);Assert.NotEqual(b.CoreCheckpoint,s.CoreCheckpoint);
    }

    [Fact]
    public void Immutable_producer_signing_vectors_are_read_at_exact_checkpoint_and_never_copied()
    {
        var b=ReadGitJson(FundingScenarioAtomicLane.Budgeting.FixtureClosure,FundingScenarioAtomicLane.Budgeting.SigningVectorPath);
        var s=ReadGitJson(FundingScenarioAtomicLane.ScenarioPlanning.FixtureClosure,FundingScenarioAtomicLane.ScenarioPlanning.SigningVectorPath);
        Assert.Equal("MOD-0136",b.RootElement.GetProperty("ModuleCode").GetString());Assert.Equal("diten.fpa.budgeting.audit",b.RootElement.GetProperty("signingIdentity").GetString());Assert.Equal("budgeting-mod-0136-fixture-current.test-only",b.RootElement.GetProperty("keyId").GetString());Assert.Equal("de9534d5b6ce6f7ef7237e6bcf593dfaa460a79b63e6293495d0de25b5225fe8",b.RootElement.GetProperty("canonicalPayloadSha256").GetString());
        Assert.Equal("MOD-0138",s.RootElement.GetProperty("moduleCode").GetString());Assert.Equal("diten.fpa.mod-0138.scenario-planning",s.RootElement.GetProperty("signingIdentity").GetString());Assert.Equal("mod-0138-scenario-planning-fixture-current",s.RootElement.GetProperty("keyId").GetString());Assert.Equal("db9385def75d885a581554e10ce38877408ed445ed3c4f62e17283b30830462b",s.RootElement.GetProperty("canonicalPayloadSha256").GetString());
        Assert.Equal("REJECT",b.RootElement.GetProperty("productionValidatorDisposition").GetString());Assert.Equal(4,b.RootElement.GetProperty("productionRejectionAssertions").GetArrayLength());Assert.True(b.RootElement.GetProperty("testOnly").GetBoolean());Assert.True(s.RootElement.GetProperty("testOnly").GetBoolean());
        Assert.NotEqual(b.RootElement.GetProperty("fixtureSecretBase64").GetString(),s.RootElement.GetProperty("fixtureSecretBase64").GetString());
        Assert.DoesNotContain(Directory.GetFiles(ServiceRoot(),"*signing-vector*",SearchOption.AllDirectories),x=>x.Contains("GateI/FundingScenario",StringComparison.Ordinal));
        VerifySigningVector(FundingScenarioAtomicLane.Budgeting.FixtureClosure,"execution/domains/enterprise-strategy-business-performance/module-packs/fixtures/MOD-0136/audit/","budgeting-audit-intent-submitted-v1",b,true);
        VerifySigningVector(FundingScenarioAtomicLane.ScenarioPlanning.FixtureClosure,"execution/domains/enterprise-strategy-business-performance/module-packs/fixtures/MOD-0138/audit/","scenario-planning-audit-intent-submitted-v1",s,false);
    }

    [Fact]
    public void Immutable_schema_projection_allowlist_provenance_and_production_rejection_matrices_execute()
    {
        const string br="execution/domains/enterprise-strategy-business-performance/module-packs/fixtures/MOD-0136/audit/",sr="execution/domains/enterprise-strategy-business-performance/module-packs/fixtures/MOD-0138/audit/";
        using var bs=ReadGitJson(FundingScenarioAtomicLane.Budgeting.FixtureClosure,br+"budgeting-audit-intent-submitted-v1.schema.json");Assert.Equal("MOD-0136",bs.RootElement.GetProperty("x-moduleCode").GetString());Assert.False(bs.RootElement.GetProperty("additionalProperties").GetBoolean());Assert.Equal(14,bs.RootElement.GetProperty("oneOf").GetArrayLength());
        using var be=ReadGitJson(FundingScenarioAtomicLane.Budgeting.FixtureClosure,br+"budgeting-audit-intent-submitted-v1.expected-audit-event.json");Assert.Equal("MOD-0136",be.RootElement.GetProperty("InputEnvelope").GetProperty("ModuleCode").GetString());Assert.Equal("MOD-0136",be.RootElement.GetProperty("ExpectedAuditEvent").GetProperty("SourceModule").GetString());Assert.Equal("Diten.FpaService",be.RootElement.GetProperty("ExpectedAuditEvent").GetProperty("SourceService").GetString());
        using var ba=ReadGitJson(FundingScenarioAtomicLane.Budgeting.FixtureClosure,br+"budgeting-audit-intent-submitted-v1.allowlist-matrix.json");Assert.Equal(14,ba.RootElement.GetProperty("positiveCases").GetArrayLength());Assert.Equal(31,ba.RootElement.GetProperty("invalidCrossPairCases").GetArrayLength());
        using var bc=ReadGitJson(FundingScenarioAtomicLane.Budgeting.FixtureClosure,br+"budgeting-audit-intent-submitted-v1.contract-negative-matrix.json");Assert.Equal(34,bc.RootElement.GetProperty("cases").GetArrayLength());Assert.Equal("TERMINAL_CONTRACT_REJECT",bc.RootElement.GetProperty("defaultExpectedDisposition").GetString());
        using var bd=ReadGitJson(FundingScenarioAtomicLane.Budgeting.FixtureClosure,br+"budgeting-audit-intent-submitted-v1.delegated-provenance.negative-matrix.json");Assert.Equal(58,bd.RootElement.GetProperty("cases").GetArrayLength());Assert.Equal("TERMINAL_SECURITY_REJECT",bd.RootElement.GetProperty("defaultExpectedDisposition").GetString());
        using var bdp=ReadGitJson(FundingScenarioAtomicLane.Budgeting.FixtureClosure,br+"budgeting-audit-intent-submitted-v1.delegated-provenance.valid.json");Assert.Equal("ACCEPT",bdp.RootElement.GetProperty("expectedDisposition").GetString());
        using var bnp=ReadGitJson(FundingScenarioAtomicLane.Budgeting.FixtureClosure,br+"budgeting-audit-intent-submitted-v1.non-delegated-provenance.valid.json");Assert.Equal("ACCEPT",bnp.RootElement.GetProperty("expectedDisposition").GetString());

        using var sm=ReadGitJson(FundingScenarioAtomicLane.ScenarioPlanning.FixtureClosure,sr+"scenario-planning-audit-intent-submitted-v1.fixture-manifest.json");Assert.Equal("MOD-0138",sm.RootElement.GetProperty("schemaIdentity").GetProperty("moduleCode").GetString());Assert.Equal("MOD-0138",sm.RootElement.GetProperty("projectionPair").GetProperty("sourceModule").GetString());Assert.True(sm.RootElement.GetProperty("productionValidatorEvidence").GetProperty("correctTestHmacStillRejected").GetBoolean());
        using var sa=ReadGitJson(FundingScenarioAtomicLane.ScenarioPlanning.FixtureClosure,sr+"scenario-planning-audit-intent-submitted-v1.allowlist-matrix.json");Assert.Equal(14,sa.RootElement.GetProperty("acceptedPairCount").GetInt32());Assert.Equal(46,sa.RootElement.GetProperty("rejectedCrossPairCount").GetInt32());Assert.Equal(64,sa.RootElement.GetProperty("cases").GetArrayLength());
        using var sc=ReadGitJson(FundingScenarioAtomicLane.ScenarioPlanning.FixtureClosure,sr+"scenario-planning-audit-intent-submitted-v1.contract-negative-matrix.json");Assert.Equal(25,sc.RootElement.GetProperty("cases").GetArrayLength());
        using var sdp=ReadGitJson(FundingScenarioAtomicLane.ScenarioPlanning.FixtureClosure,sr+"scenario-planning-audit-intent-submitted-v1.delegated-provenance.positive.json");Assert.Equal(2,sdp.RootElement.GetProperty("cases").GetArrayLength());
        using var sdn=ReadGitJson(FundingScenarioAtomicLane.ScenarioPlanning.FixtureClosure,sr+"scenario-planning-audit-intent-submitted-v1.delegated-provenance.negative.json");Assert.Equal(19,sdn.RootElement.GetProperty("cases").GetArrayLength());
        using var sn=ReadGitJson(FundingScenarioAtomicLane.ScenarioPlanning.FixtureClosure,sr+"scenario-planning-audit-intent-submitted-v1.non-delegated-provenance.matrix.json");Assert.Equal(2,sn.RootElement.GetProperty("caseCount").GetInt32());
        using var spr=ReadGitJson(FundingScenarioAtomicLane.ScenarioPlanning.FixtureClosure,sr+"scenario-planning-audit-intent-submitted-v1.production-validator.negative.json");Assert.Equal(5,spr.RootElement.GetProperty("cases").GetArrayLength());
    }

    [Fact]
    public void Immutable_core_checkpoints_expose_exact_five_field_producer_contracts()
    {
        var budget=GitShow(FundingScenarioAtomicLane.Budgeting.CoreCheckpoint,"services/Diten.FpaService/src/Diten.FpaService.Domain/Modules/Budgeting/Selections/BudgetingSelectionContracts.cs");
        var scenario=GitShow(FundingScenarioAtomicLane.ScenarioPlanning.CoreCheckpoint,"services/Diten.FpaService/src/Diten.FpaService.Domain/Modules/ScenarioPlanning/ScenarioPlanningCore.cs");
        Assert.Contains("BudgetVersionReferenceV1",budget,StringComparison.Ordinal);Assert.Contains("fpa.budget-version-reference",budget,StringComparison.Ordinal);
        Assert.Contains("ScenarioVersionReferenceV1",scenario,StringComparison.Ordinal);Assert.Contains("ComparatorOutputReferenceV1",scenario,StringComparison.Ordinal);
        Assert.Equal(5,typeof(BudgetVersionReferenceV1).GetProperties().Length);Assert.Equal(5,typeof(ScenarioVersionReferenceV1).GetProperties().Length);Assert.Equal(5,typeof(ComparatorOutputReferenceV1).GetProperties().Length);
    }

    [Fact]
    public void Mutation_evidence_is_bound_to_restored_source_hashes_and_real_target_tests()
    {
        var path=Path.Combine(ServiceRoot(),"tests/Diten.PpmService.Tests/GateI/FundingScenario/gate-i-b-mutation-evidence-v1.json");
        using var manifest=JsonDocument.Parse(File.ReadAllText(path));
        Assert.Equal("ppm.mod-0117.gate-i-b.mutation-evidence.v1",manifest.RootElement.GetProperty("schema").GetString());
        Assert.Equal("c954984d00ad43bc3127bbd0ada09da2b2721768",manifest.RootElement.GetProperty("baseCommit").GetString());
        var sourcePath=manifest.RootElement.GetProperty("sourcePath").GetString()!;var sha=Convert.ToHexString(SHA256.HashData(GitShowBytes(manifest.RootElement.GetProperty("sourceCheckpoint").GetString()!,sourcePath))).ToLowerInvariant();Assert.Equal(manifest.RootElement.GetProperty("restoredSha256").GetString(),sha);
        Assert.False(File.Exists(Path.Combine(RepoRoot(),sourcePath)),"Historical combined source must remain replaced by the authoritative split-file architecture.");
        var entries=manifest.RootElement.GetProperty("entries").EnumerateArray().ToArray();Assert.Equal(6,entries.Length);Assert.Equal(entries.Length,entries.Select(x=>x.GetProperty("mutation").GetString()).Distinct(StringComparer.Ordinal).Count());Assert.Equal(entries.Length,entries.Select(x=>x.GetProperty("runId").GetString()).Distinct(StringComparer.Ordinal).Count());
        foreach(var entry in entries)
        {
            Assert.Equal(0,entry.GetProperty("compileExit").GetInt32());Assert.NotEqual(0,entry.GetProperty("targetedExit").GetInt32());Assert.False(string.IsNullOrWhiteSpace(entry.GetProperty("failureIdentity").GetString()));var digest=entry.GetProperty("rawOutputSha256").GetString()!;Assert.Equal(64,digest.Length);Assert.All(digest,c=>Assert.True(c is >= '0' and <= '9' or >= 'a' and <= 'f'));
            var identity=entry.GetProperty("testIdentity").GetString()!;var separator=identity.LastIndexOf('.');var type=typeof(FundingScenarioArchitectureAndFixtureTests).Assembly.GetType(identity[..separator],true)!;var method=type.GetMethod(identity[(separator+1)..],BindingFlags.Instance|BindingFlags.Public);Assert.NotNull(method);Assert.NotNull(method!.GetCustomAttribute<FactAttribute>());Assert.Contains(method.Name,entry.GetProperty("commandFilter").GetString(),StringComparison.Ordinal);
        }
    }

    private static void VerifySigningVector(string commit,string root,string stem,JsonDocument vectorDoc,bool budget)
    {
        var v=vectorDoc.RootElement;var payload=GitShowBytes(commit,root+stem+".canonical.json");var expectedLength=budget?v.GetProperty("canonicalPayloadByteLength").GetInt32():v.GetProperty("payloadByteLength").GetInt32();Assert.Equal(expectedLength,payload.Length);var payloadHash=Convert.ToHexString(SHA256.HashData(payload)).ToLowerInvariant();Assert.Equal(v.GetProperty("canonicalPayloadSha256").GetString(),payloadHash);Assert.Equal(payloadHash+"\n",Encoding.ASCII.GetString(GitShowBytes(commit,root+stem+".canonical.sha256")));
        string S(string lower,string upper)=>v.TryGetProperty(lower,out var p)?p.GetString()!:v.GetProperty(upper).GetString()!;var prefix=string.Join('\n',v.GetProperty("signatureScheme").GetString(),v.GetProperty("eventId").GetString(),v.GetProperty("eventName").GetString(),v.GetProperty("eventVersion").GetInt32().ToString(),v.GetProperty("tenantId").GetString(),v.GetProperty("correlationId").GetString(),v.GetProperty("producer").GetString(),v.GetProperty("causationId").GetString(),v.GetProperty("occurredAtUtc").GetString(),payload.Length.ToString())+"\n";var prefixBytes=Encoding.UTF8.GetBytes(prefix);var input=new byte[prefixBytes.Length+payload.Length];prefixBytes.CopyTo(input,0);payload.CopyTo(input,prefixBytes.Length);Assert.Equal(v.GetProperty("signingInputSha256").GetString(),Convert.ToHexString(SHA256.HashData(input)).ToLowerInvariant());var signature=Convert.ToHexString(HMACSHA256.HashData(Convert.FromBase64String(v.GetProperty("fixtureSecretBase64").GetString()!),input)).ToLowerInvariant();var expected=budget?Encoding.ASCII.GetString(GitShowBytes(commit,root+stem+".expected-signature.txt")).Trim():JsonDocument.Parse(GitShowBytes(commit,root+stem+".expected-signature.json")).RootElement.GetProperty("signatureLowerHex").GetString();Assert.Equal(expected,signature);Assert.False(string.IsNullOrWhiteSpace(S("signingIdentity","signingIdentity")));
    }
    private static JsonDocument ReadGitJson(string commit,string path)=>JsonDocument.Parse(GitShowBytes(commit,path));
    private static byte[] GitShowBytes(string commit,string path)
    {
        using var p=Process.Start(new ProcessStartInfo("git") {WorkingDirectory=RepoRoot(),RedirectStandardOutput=true,RedirectStandardError=true,UseShellExecute=false,ArgumentList={"show",$"{commit}:{path}"}})??throw new InvalidOperationException("git_start_failed");using var stream=new MemoryStream();p.StandardOutput.BaseStream.CopyTo(stream);var error=p.StandardError.ReadToEnd();p.WaitForExit();Assert.True(p.ExitCode==0,error);return stream.ToArray();
    }
    private static string GitShow(string commit,string path)
    {
        using var p=Process.Start(new ProcessStartInfo("git",$"-C \"{RepoRoot()}\" show {commit}:{path}") {RedirectStandardOutput=true,RedirectStandardError=true,UseShellExecute=false})??throw new InvalidOperationException("git_start_failed");var output=p.StandardOutput.ReadToEnd();var error=p.StandardError.ReadToEnd();p.WaitForExit();Assert.True(p.ExitCode==0,error);return output;
    }
    private static string ServiceRoot(){var d=new DirectoryInfo(AppContext.BaseDirectory);while(d is not null&&d.Name!="Diten.PpmService")d=d.Parent;return d?.FullName??throw new InvalidOperationException("service_root_missing");}
    private static string RepoRoot()=>Directory.GetParent(ServiceRoot())?.Parent?.FullName??throw new InvalidOperationException("repo_root_missing");
}
