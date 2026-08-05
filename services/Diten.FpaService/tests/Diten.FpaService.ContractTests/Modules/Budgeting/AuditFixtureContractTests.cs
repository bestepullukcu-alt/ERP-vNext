using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using System.Text.Json.Nodes;
using Diten.FpaService.Application.Modules.Budgeting.Contracts;

namespace Diten.FpaService.ContractTests.Modules.Budgeting;

public sealed class AuditFixtureContractTests
{
    private static readonly string FixtureRoot=Path.Combine(AppContext.BaseDirectory,"Fixtures","Audit");
    private const string Prefix="budgeting-audit-intent-submitted-v1";

    [Fact] public void Canonical_payload_checksum_schema_and_order_are_exact()
    {
        var payload=File.ReadAllBytes(Path.Combine(FixtureRoot,$"{Prefix}.canonical.json"));
        Assert.Equal(254,payload.Length);
        Assert.False(payload.AsSpan().StartsWith(new byte[]{0xef,0xbb,0xbf}));
        Assert.False(payload.AsSpan().EndsWith("\n"u8));
        var digest=Convert.ToHexString(SHA256.HashData(payload)).ToLowerInvariant();
        Assert.Equal("de9534d5b6ce6f7ef7237e6bcf593dfaa460a79b63e6293495d0de25b5225fe8",digest);
        Assert.Equal(digest+"\n",File.ReadAllText(Path.Combine(FixtureRoot,$"{Prefix}.canonical.sha256")));
        using var doc=JsonDocument.Parse(payload);
        Assert.Equal(new[]{"auditIntentId","actorId","entityType","entityId","mutation","occurredAtUtc"},doc.RootElement.EnumerateObject().Select(x=>x.Name));
        using var schema=JsonDocument.Parse(File.ReadAllBytes(Path.Combine(FixtureRoot,$"{Prefix}.schema.json")));
        Assert.Equal("urn:diten:fpa:budgeting-audit-intent-submitted:v1",schema.RootElement.GetProperty("$id").GetString());
        Assert.Equal(digest,schema.RootElement.GetProperty("x-canonicalSha256").GetString());
        Assert.Equal(1,schema.RootElement.GetProperty("x-eventVersion").GetInt32());
    }

    [Fact] public void Envelope_projection_allowlist_and_provenance_are_closed()
    {
        using var envelope=Read($"{Prefix}.expected-audit-event.json");
        var input=envelope.RootElement.GetProperty("InputEnvelope");
        var projection=envelope.RootElement.GetProperty("ExpectedAuditEvent");
        Assert.Equal("MOD-0136",input.GetProperty("ModuleCode").GetString());
        Assert.Equal("Diten.FpaService",projection.GetProperty("SourceService").GetString());
        Assert.Equal("MOD-0136",projection.GetProperty("SourceModule").GetString());
        using var matrix=Read($"{Prefix}.allowlist-matrix.json");
        Assert.Equal(14,matrix.RootElement.GetProperty("positiveCases").GetArrayLength());
        Assert.Equal(31,matrix.RootElement.GetProperty("invalidCrossPairCases").GetArrayLength());
        using var delegated=Read($"{Prefix}.delegated-provenance.valid.json");
        Assert.Equal("ACCEPT",delegated.RootElement.GetProperty("expectedDisposition").GetString());
        using var nonDelegated=Read($"{Prefix}.non-delegated-provenance.valid.json");
        Assert.Equal(6,nonDelegated.RootElement.GetProperty("delegationFieldsThatMustBeAbsent").GetArrayLength());
        using var negatives=Read($"{Prefix}.delegated-provenance.negative-matrix.json");
        Assert.All(negatives.RootElement.GetProperty("cases").EnumerateArray(),x=>Assert.False(string.IsNullOrWhiteSpace(x.GetProperty("id").GetString())));
    }

    [Fact] public void Budgeting_only_test_hmac_is_exact_and_marked_for_production_rejection()
    {
        using var vector=Read($"{Prefix}.signing-vector.json");var root=vector.RootElement;
        var payload=File.ReadAllBytes(Path.Combine(FixtureRoot,$"{Prefix}.canonical.json"));
        var fields=new[]{root.GetProperty("signatureScheme").GetString(),root.GetProperty("eventId").GetString(),root.GetProperty("eventName").GetString(),root.GetProperty("eventVersion").GetInt32().ToString(),root.GetProperty("tenantId").GetString(),root.GetProperty("correlationId").GetString(),root.GetProperty("producer").GetString(),root.GetProperty("causationId").GetString(),root.GetProperty("occurredAtUtc").GetString(),payload.Length.ToString()};
        var header=Encoding.UTF8.GetBytes(string.Join('\n',fields)+"\n");var input=new byte[header.Length+payload.Length];header.CopyTo(input,0);payload.CopyTo(input,header.Length);
        Assert.Equal(529,input.Length);
        Assert.Equal(root.GetProperty("signingInputSha256").GetString(),Convert.ToHexString(SHA256.HashData(input)).ToLowerInvariant());
        var key=Convert.FromBase64String(root.GetProperty("fixtureSecretBase64").GetString()!);
        var signature=Convert.ToHexString(HMACSHA256.HashData(key,input)).ToLowerInvariant();
        Assert.Equal(File.ReadAllText(Path.Combine(FixtureRoot,$"{Prefix}.expected-signature.txt")).TrimEnd('\n'),signature);
        Assert.True(root.GetProperty("testOnly").GetBoolean());Assert.Equal("REJECT",root.GetProperty("productionValidatorDisposition").GetString());
        Assert.Equal(ProductionSigningIdentityPolicy.FixtureIdentity,root.GetProperty("fixtureIdentity").GetString());
        Assert.Equal(ProductionSigningIdentityPolicy.FixtureKeyId,root.GetProperty("keyId").GetString());
        Assert.Equal(new[]{"testOnly-marker","fixtureIdentity","keyId","fixtureSecretDerivedSignature"},root.GetProperty("productionRejectionAssertions").EnumerateArray().Select(x=>x.GetString()));
        Assert.True(ProductionSigningIdentityPolicy.Reject(true,"production","production",false));
        Assert.True(ProductionSigningIdentityPolicy.Reject(false,ProductionSigningIdentityPolicy.FixtureIdentity,"production",false));
        Assert.True(ProductionSigningIdentityPolicy.Reject(false,"production",ProductionSigningIdentityPolicy.FixtureKeyId,false));
        Assert.True(ProductionSigningIdentityPolicy.Reject(false,"production","production",true));
        Assert.False(ProductionSigningIdentityPolicy.Reject(false,"production","production",false));
    }

    private static JsonDocument Read(string name)=>JsonDocument.Parse(File.ReadAllBytes(Path.Combine(FixtureRoot,name)));

    [Fact] public void All_raw_byte_and_rfc6902_contract_negatives_execute_and_reject()
    {
        using var matrix=Read($"{Prefix}.contract-negative-matrix.json");var executed=0;Assert.Equal("TERMINAL_CONTRACT_REJECT",matrix.RootElement.GetProperty("defaultExpectedDisposition").GetString());Assert.Equal(0,matrix.RootElement.GetProperty("defaultExpectedAuditEventCount").GetInt32());
        foreach(var item in matrix.RootElement.GetProperty("cases").EnumerateArray())
        {
            var target=item.GetProperty("target").GetString()!;var accepted=false;
            if(item.TryGetProperty("candidateBytesBase64",out var encoded))
            {var candidate=Convert.FromBase64String(encoded.GetString()!);accepted=ValidateCanonical(candidate);}
            else if(item.TryGetProperty("candidate",out var candidate))
            {accepted=ValidateChecksum(candidate.GetString()!);}
            else
            {var baseName=item.GetProperty("baseFile").GetString()!;var original=JsonNode.Parse(File.ReadAllText(Path.Combine(FixtureRoot,baseName)))!;var mutated=original.DeepClone();Apply(mutated,item.GetProperty("patch"));accepted=ValidateStage(target,mutated);}
            Assert.Contains(target,new[]{"canonical","checksum","schema","pair","envelope","projection"});Assert.False(accepted);executed++;
        }
        Assert.Equal(34,executed);
    }

    [Fact] public void Every_delegated_provenance_patch_executes_to_zero_projection()
    {
        using var matrix=Read($"{Prefix}.delegated-provenance.negative-matrix.json");var baseNode=JsonNode.Parse(File.ReadAllText(Path.Combine(FixtureRoot,matrix.RootElement.GetProperty("baseFile").GetString()!)))!;var executed=0;
        foreach(var item in matrix.RootElement.GetProperty("cases").EnumerateArray()){var mutated=baseNode.DeepClone();Apply(mutated,item.GetProperty("patch"));var actual=EvaluateProvenance(mutated,baseNode);var expectedDisposition=item.TryGetProperty("expectedDisposition",out var disposition)?disposition.GetString():matrix.RootElement.GetProperty("defaultExpectedDisposition").GetString();var expectedStatus=item.TryGetProperty("expectedHttpStatus",out var status)?status.GetInt32():403;Assert.Equal((expectedDisposition,expectedStatus,0),actual);executed++;}
        Assert.Equal(58,executed);Assert.Equal(0,matrix.RootElement.GetProperty("defaultExpectedAuditEventCount").GetInt32());
    }

    [Fact] public void Full_allowlist_cartesian_matrix_is_pair_sensitive()
    {
        using var matrix=Read($"{Prefix}.allowlist-matrix.json");var positives=matrix.RootElement.GetProperty("positiveCases").EnumerateArray().Select(x=>$"{x.GetProperty("entityType").GetString()}|{x.GetProperty("mutation").GetString()}").ToHashSet(StringComparer.Ordinal);
        Assert.Equal(14,positives.Count);Assert.All(matrix.RootElement.GetProperty("positiveCases").EnumerateArray(),x=>Assert.Equal("ACCEPT",x.GetProperty("expectedDisposition").GetString()));
        var negatives=matrix.RootElement.GetProperty("invalidCrossPairCases").EnumerateArray().Select(x=>$"{x.GetProperty("entityType").GetString()}|{x.GetProperty("mutation").GetString()}").ToArray();Assert.Equal(31,negatives.Length);Assert.All(negatives,x=>Assert.DoesNotContain(x,positives));Assert.Equal(45,positives.Count+negatives.Length);
    }

    private static void Apply(JsonNode root,JsonElement patches)
    {
        foreach(var patch in patches.EnumerateArray()){var parts=patch.GetProperty("path").GetString()!.Split('/',StringSplitOptions.RemoveEmptyEntries).Select(x=>x.Replace("~1","/").Replace("~0","~")).ToArray();JsonNode parent=root;for(var i=0;i<parts.Length-1;i++)parent=parent[parts[i]]!;var obj=(JsonObject)parent;var op=patch.GetProperty("op").GetString();if(op=="remove")obj.Remove(parts[^1]);else obj[parts[^1]]=JsonNode.Parse(patch.GetProperty("value").GetRawText());}
    }
    private static bool ValidateCanonical(byte[] candidate)=>candidate.AsSpan().SequenceEqual(File.ReadAllBytes(Path.Combine(FixtureRoot,$"{Prefix}.canonical.json")));
    private static bool ValidateChecksum(string candidate)=>System.Text.RegularExpressions.Regex.IsMatch(candidate,"^[0-9a-f]{64}\\n$")&&candidate==File.ReadAllText(Path.Combine(FixtureRoot,$"{Prefix}.canonical.sha256"));
    private static bool ValidateStage(string target,JsonNode candidate)
    {
        if(target=="canonical")return JsonNode.DeepEquals(candidate,JsonNode.Parse(File.ReadAllText(Path.Combine(FixtureRoot,$"{Prefix}.canonical.json"))));
        if(target is "schema" or "pair")return JsonNode.DeepEquals(candidate,JsonNode.Parse(File.ReadAllText(Path.Combine(FixtureRoot,$"{Prefix}.schema.json"))));
        return JsonNode.DeepEquals(candidate,JsonNode.Parse(File.ReadAllText(Path.Combine(FixtureRoot,$"{Prefix}.expected-audit-event.json"))));
    }
    private static (string? Disposition,int HttpStatus,int AuditCount) EvaluateProvenance(JsonNode candidate,JsonNode authoritative){if(JsonNode.DeepEquals(candidate,authoritative))return("ACCEPT",200,1);var authority=candidate["authorityState"]?["verification"]?.GetValue<string>();return authority is "indeterminate" or "unavailable"?("FAIL_CLOSED_AUTHORITY_UNAVAILABLE",503,0):("TERMINAL_SECURITY_REJECT",403,0);}
}
