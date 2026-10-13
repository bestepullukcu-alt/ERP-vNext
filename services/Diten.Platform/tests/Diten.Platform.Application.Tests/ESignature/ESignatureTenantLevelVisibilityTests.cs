using System.Net;
using System.Net.Http.Json;
using System.Text;
using System.Text.Json;
using Diten.Platform.Application.Features.ESignature;
using Diten.Platform.Application.Features.Workflow;
using Diten.Platform.Application.Tests.Persistence;
using Diten.Platform.Infrastructure.Persistence.Schema;
using MongoDB.Bson;
using MongoDB.Driver;
using Xunit;

namespace Diten.Platform.Application.Tests.ESignature;

/// <summary>
/// MOD-0022 VIS1 (FU-0022-VIS-01, Part 11 §11.10(b)/(e)) — a signature whose <c>LegalEntityId</c> is null (a pure
/// <c>workflow / workflow-task</c> subject) is a TENANT-LEVEL record: search / get / verify show it to the tenant's holders of
/// <c>esign.signatures.read</c>, the audit export includes it for the holders of <c>esign.signatures.export</c> (company cell
/// "Kiracı düzeyi"), and its signer sees it as before. A record WITH a company keeps the E2 rule (own, or a company in the
/// caller's SEC-002 data scope); another tenant's record is never visible. Written first as the leak list (security-agent):
/// the cases a widening could break are here next to the ones it must open. Real HTTP host, real Mongo.
/// </summary>
[Collection(DisposableMongoReplicaSetCollection.Name)]
public sealed class ESignatureTenantLevelVisibilityTests : IClassFixture<WorkflowESignatureTestHost>
{
    private const string Workflow = "api/v1/workflow";
    private const string Signatures = "api/v1/esign/signatures";

    private static readonly string[] WorkflowKeys =
    [
        WorkflowPermissions.DefinitionsView, WorkflowPermissions.DefinitionsManage, WorkflowPermissions.DefinitionsPublish,
        WorkflowPermissions.InstancesStart, WorkflowPermissions.InstancesView, WorkflowPermissions.TasksApprove
    ];

    private static readonly string[] AdminKeys = ESignPermissions.All.Concat(WorkflowKeys).ToArray();

    private readonly WorkflowESignatureTestHost _host;

    public ESignatureTenantLevelVisibilityTests(WorkflowESignatureTestHost host) => _host = host;

    private sealed record World(Guid Tenant, Guid Admin, Guid A, Guid B, Guid C);

    private sealed record Signed(Guid SignatureId, string SignatureNumber, Guid TaskId);

    private static World NewWorld() => new(Guid.NewGuid(), Guid.NewGuid(), Guid.NewGuid(), Guid.NewGuid(), Guid.NewGuid());

    private static string Key() => "k-" + Guid.NewGuid().ToString("N");

    private static string Token(Guid tenant, Guid user, params string[] keys) => ESignatureTestHost.Token(tenant, user, keys, name: "User " + user.ToString("N")[..6]);

    // ── arrange: B signs a signature-required workflow task (LegalEntityId null) ──────────────────────────────────

    private async Task<Signed> SignedTaskAsync(World w)
    {
        using var admin = _host.Client(Token(w.Tenant, w.Admin, AdminKeys));
        var code = "VIS1-" + Guid.NewGuid().ToString("N")[..8].ToUpperInvariant();
        var created = await admin.PostAsJsonAsync($"{Workflow}/definitions", new { templateCode = code, name = "VIS1", description = "MOD-0022 VIS1" });
        var createdBody = await created.Content.ReadAsStringAsync();
        Assert.True(created.IsSuccessStatusCode, createdBody);
        var definition = "{ \"stages\": [ { \"code\": \"QA\", \"steps\": [ { \"code\": \"QA_APPROVAL\", \"name\": \"QA\", \"type\": \"approval\", "
            + "\"assignment\": { \"candidatePrincipalIds\": [] }, \"requirements\": { \"signatureRequired\": true, \"signatureMeaning\": \"APPROVE\" } } ] } ] }";
        var published = await admin.PostAsJsonAsync($"{Workflow}/definitions/{Data(createdBody).GetProperty("id").GetString()}/publish",
            new { definitionJson = definition, schemaVersion = "workflow_schema_v1", expressionVersion = "workflow_expr_v1", publishReason = "VIS1" });
        Assert.True(published.IsSuccessStatusCode, await published.Content.ReadAsStringAsync());

        using var starter = _host.Client(Token(w.Tenant, w.A, AdminKeys));
        var objectId = "VIS1-" + Guid.NewGuid().ToString("N")[..8];
        var started = await starter.PostAsJsonAsync($"{Workflow}/instances", new
        {
            templateCode = code, objectType = "vis1.test-object", objectId, objectRef = objectId,
            candidatePrincipalIds = new[] { "user:" + w.B.ToString("D") }, reasonCode = "SUBMITTED", idempotencyKey = Key(),
            commentRequired = false, evidenceRequired = false
        });
        var startedBody = await started.Content.ReadAsStringAsync();
        Assert.True(started.IsSuccessStatusCode, startedBody);
        var instanceId = Data(startedBody).GetProperty("workflowInstanceId").GetString()!;
        var taskId = Guid.Parse(Data(startedBody).GetProperty("approvalTaskId").GetString()!);

        using var signer = _host.Client(Token(w.Tenant, w.B, AdminKeys));
        var sign = await signer.PostAsJsonAsync(Signatures, new
        {
            ticket = _host.Tickets.Issue(w.Tenant, w.B),
            subject = new { module = "workflow", subjectType = "workflow-task", subjectId = taskId.ToString("D") },
            meaningCode = "APPROVE",
            appearance = new { type = "none" },
            context = new Dictionary<string, string> { ["workflowInstanceId"] = instanceId, ["workflowTaskId"] = taskId.ToString("D"), ["stepCode"] = "QA_APPROVAL", ["decision"] = "approve" },
            idempotencyKey = Key()
        });
        var signBody = await sign.Content.ReadAsStringAsync();
        Assert.True(sign.StatusCode == HttpStatusCode.Created, signBody);
        var record = Data(signBody);
        Assert.Equal(JsonValueKind.Null, record.GetProperty("legalEntityId").ValueKind); // the case this WP is about
        return new Signed(record.GetProperty("id").GetGuid(), record.GetProperty("signatureNumber").GetString()!, taskId);
    }

    /// <summary>A copy of a stored record with another tenant / company / signer (test data for the boundaries).</summary>
    private async Task<(Guid Id, string Number)> CopyAsync(Guid sourceId, Guid tenant, Guid? legalEntity, Guid signer)
    {
        var records = _host.Database.GetCollection<BsonDocument>(PlatformCollections.ESignatureRecords);
        var doc = await records.Find(Builders<BsonDocument>.Filter.Eq("_id", new BsonBinaryData(sourceId, GuidRepresentation.Standard))).FirstAsync();
        var id = Guid.NewGuid();
        var number = "ESIG-VIS1-" + id.ToString("N")[..8].ToUpperInvariant();
        doc["_id"] = new BsonBinaryData(id, GuidRepresentation.Standard);
        doc["TenantId"] = new BsonBinaryData(tenant, GuidRepresentation.Standard);
        doc["LegalEntityId"] = legalEntity is { } le ? new BsonBinaryData(le, GuidRepresentation.Standard) : BsonNull.Value;
        doc["SignerUserId"] = signer.ToString("D");
        doc["SignatureNumber"] = number;
        foreach (var name in doc.Names.Where(n => n.Contains("Idempotency", StringComparison.Ordinal)).ToList()) doc[name] = "copy-" + id.ToString("N");
        await records.InsertOneAsync(doc);
        return (id, number);
    }

    // ── act ─────────────────────────────────────────────────────────────────────────────────────────────────────

    private async Task<List<string>> SearchNumbersAsync(string token)
    {
        using var client = _host.Client(token);
        var response = await client.GetAsync($"{Signatures}?pageSize=100");
        var body = await response.Content.ReadAsStringAsync();
        Assert.True(response.IsSuccessStatusCode, body);
        return Data(body).GetProperty("items").EnumerateArray().Select(r => r.GetProperty("signature").GetProperty("signatureNumber").GetString()!).ToList();
    }

    private async Task<int> GetStatusAsync(string token, Guid id)
    {
        using var client = _host.Client(token);
        return (int)(await client.GetAsync($"{Signatures}/{id:D}")).StatusCode;
    }

    private async Task<int> VerifyStatusAsync(string token, Guid id)
    {
        using var client = _host.Client(token);
        return (int)(await client.PostAsync($"{Signatures}/{id:D}/verify", null)).StatusCode;
    }

    private async Task<string> ExportCsvAsync(string token)
    {
        using var client = _host.Client(token);
        var response = await client.PostAsJsonAsync("api/v1/esign/exports", new
        {
            format = "csv",
            filter = new { fromUtc = DateTime.UtcNow.AddDays(-1), toUtc = DateTime.UtcNow.AddDays(1) },
            idempotencyKey = Key()
        });
        var bytes = await response.Content.ReadAsByteArrayAsync();
        Assert.True(response.IsSuccessStatusCode, Encoding.UTF8.GetString(bytes));
        return Encoding.UTF8.GetString(bytes);
    }

    // ── what the WP opens (red before VIS1) ───────────────────────────────────────────────────────────────────────

    [Fact]
    public async Task A_read_holder_without_a_company_scope_finds_gets_and_verifies_a_tenant_level_signature()
    {
        var w = NewWorld();
        var s = await SignedTaskAsync(w);
        var admin = Token(w.Tenant, w.Admin, AdminKeys); // no data scope, not the signer

        Assert.Contains(s.SignatureNumber, await SearchNumbersAsync(admin));
        Assert.Equal(200, await GetStatusAsync(admin, s.SignatureId));
        Assert.Equal(200, await VerifyStatusAsync(admin, s.SignatureId));
    }

    [Fact]
    public async Task An_export_holder_gets_the_tenant_level_signature_in_the_file_labelled_tenant_level()
    {
        var w = NewWorld();
        var s = await SignedTaskAsync(w);
        var csv = await ExportCsvAsync(Token(w.Tenant, w.Admin, ESignPermissions.SignaturesExport));

        var line = csv.Split("\r\n").SingleOrDefault(l => l.Contains(s.SignatureNumber, StringComparison.Ordinal));
        Assert.NotNull(line);
        Assert.Contains("\"Kiracı düzeyi\"", line, StringComparison.Ordinal); // company cell: no company, tenant level
    }

    // ── what must stay closed ─────────────────────────────────────────────────────────────────────────────────────

    /// <summary>K3 sabotage (c): drop the read check → red.</summary>
    [Fact]
    public async Task Without_esign_signatures_read_a_user_does_not_see_another_users_tenant_level_signature()
    {
        var w = NewWorld();
        var s = await SignedTaskAsync(w);
        // C may call search (export key) and verify (verify key) but holds no esign.signatures.read
        var c = Token(w.Tenant, w.C, ESignPermissions.SignaturesExport, ESignPermissions.SignaturesVerify);

        Assert.DoesNotContain(s.SignatureNumber, await SearchNumbersAsync(c));
        Assert.Equal(404, await VerifyStatusAsync(c, s.SignatureId));
        Assert.Equal(403, await GetStatusAsync(c, s.SignatureId)); // the endpoint key itself
    }

    /// <summary>K3 sabotage (b): apply the tenant-level rule to records WITH a company → red.</summary>
    [Fact]
    public async Task A_record_of_a_company_outside_the_callers_scope_stays_invisible_to_a_read_holder()
    {
        var w = NewWorld();
        var s = await SignedTaskAsync(w);
        var inScope = Guid.NewGuid();
        var outside = Guid.NewGuid();
        var (_, outsideNumber) = await CopyAsync(s.SignatureId, w.Tenant, outside, w.A);
        var (_, inScopeNumber) = await CopyAsync(s.SignatureId, w.Tenant, inScope, w.A);
        _host.Scopes.LegalEntitiesByUser[w.Admin] = [inScope];

        var numbers = await SearchNumbersAsync(Token(w.Tenant, w.Admin, AdminKeys));
        Assert.DoesNotContain(outsideNumber, numbers);           // E2 company rule, unchanged
        Assert.Contains(inScopeNumber, numbers);                 // ... in both directions
        Assert.Contains(s.SignatureNumber, numbers);             // and the tenant-level record next to them

        var csv = await ExportCsvAsync(Token(w.Tenant, w.Admin, ESignPermissions.SignaturesExport));
        Assert.DoesNotContain(outsideNumber, csv, StringComparison.Ordinal);
        Assert.Contains(inScopeNumber, csv, StringComparison.Ordinal);
    }

    /// <summary>K3 sabotage (a): drop the tenant filter → red.</summary>
    [Fact]
    public async Task Another_tenants_tenant_level_signature_is_never_visible()
    {
        var w = NewWorld();
        var s = await SignedTaskAsync(w);
        var other = NewWorld();
        var (otherId, otherNumber) = await CopyAsync(s.SignatureId, other.Tenant, null, other.B);
        var admin = Token(w.Tenant, w.Admin, AdminKeys);

        Assert.DoesNotContain(otherNumber, await SearchNumbersAsync(admin));
        Assert.Equal(404, await GetStatusAsync(admin, otherId));
        Assert.Equal(404, await VerifyStatusAsync(admin, otherId));
        Assert.DoesNotContain(otherNumber, await ExportCsvAsync(Token(w.Tenant, w.Admin, ESignPermissions.SignaturesExport)), StringComparison.Ordinal);
    }

    [Fact]
    public async Task The_signer_still_sees_their_own_signature_without_the_read_key()
    {
        var w = NewWorld();
        var s = await SignedTaskAsync(w);
        var signer = Token(w.Tenant, w.B, ESignPermissions.SignaturesExport, ESignPermissions.SignaturesVerify);

        Assert.Contains(s.SignatureNumber, await SearchNumbersAsync(signer));
        Assert.Equal(200, await VerifyStatusAsync(signer, s.SignatureId));
    }

    // ── FUB1 (FU-0022-VIS-02): verify applies the company rule too ───────────────────────────────────────────────

    /// <summary>K3 sabotage (a) of FUB1: drop the new scope check in verify → red.</summary>
    [Fact]
    public async Task Verify_of_a_company_record_outside_the_callers_scope_is_not_found()
    {
        var w = NewWorld();
        var s = await SignedTaskAsync(w);
        var inScope = Guid.NewGuid();
        var (outsideId, _) = await CopyAsync(s.SignatureId, w.Tenant, Guid.NewGuid(), w.A);
        var (inScopeId, _) = await CopyAsync(s.SignatureId, w.Tenant, inScope, w.A);
        _host.Scopes.LegalEntitiesByUser[w.Admin] = [inScope];
        var admin = Token(w.Tenant, w.Admin, AdminKeys); // read + verify, company scope = inScope only

        Assert.Equal(404, await VerifyStatusAsync(admin, outsideId)); // as if the record did not exist
        Assert.Equal(200, await VerifyStatusAsync(admin, inScopeId));
        Assert.Equal(200, await VerifyStatusAsync(admin, s.SignatureId)); // tenant-level: VIS1 unchanged
    }

    [Fact]
    public async Task The_signer_verifies_their_own_company_record_even_outside_their_scope()
    {
        var w = NewWorld();
        var s = await SignedTaskAsync(w);
        var (ownId, _) = await CopyAsync(s.SignatureId, w.Tenant, Guid.NewGuid(), w.C); // C signed it; C has no company scope
        Assert.Equal(200, await VerifyStatusAsync(Token(w.Tenant, w.C, ESignPermissions.SignaturesVerify), ownId));
    }

    private static JsonElement Data(string body)
    {
        using var document = JsonDocument.Parse(body);
        return document.RootElement.GetProperty("data").Clone();
    }
}
