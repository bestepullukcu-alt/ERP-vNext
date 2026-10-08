using System.Net;
using System.Text;
using System.Text.Json;
using System.Text.Json.Nodes;
using Diten.InventoryService.Application.Features.Quarantine.Ports;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.TestHost;
using Microsoft.Extensions.DependencyInjection;

namespace Diten.InventoryService.Tests.TestInfrastructure;

/// <summary>
/// The production host with the Workflow approval port (test port OFF) — only the network edge to the Gateway is replaced:
/// the REAL <c>GatewayWorkflowClient</c> and <c>ForwardCallerHeadersHandler</c> run, and their HTTP requests land in
/// <see cref="FakeWorkflowGateway"/> instead of a socket.
/// </summary>
public sealed class WorkflowQuarantineApiFactory : QuarantineApiFactory
{
    public FakeWorkflowGateway Workflow { get; } = new();

    protected override bool TestApprovalPort => false;

    protected override void ConfigureWebHost(IWebHostBuilder builder)
    {
        base.ConfigureWebHost(builder);
        builder.ConfigureTestServices(services => services.AddHttpClient(nameof(IQuarantineWorkflowClient))
            .ConfigurePrimaryHttpMessageHandler(() => new FakeWorkflowHandler(Workflow)));
    }
}

/// <summary>The primary handler of the workflow HttpClient (a fresh one per handler rotation; the state lives in the gateway).</summary>
public sealed class FakeWorkflowHandler(FakeWorkflowGateway gateway) : HttpMessageHandler
{
    protected override Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken cancellationToken)
        => gateway.HandleAsync(request, cancellationToken);
}

/// <summary>
/// An in-memory MOD-0023 behind the Gateway, with the behaviours slice Q2 relies on, modelled on the Platform code
/// (<c>WorkflowDefinitionsController</c>, WP-0023-FX): every call needs the caller's bearer token (no token → 401, the
/// actor is the token's <c>sub</c>) and the <c>X-Tenant-Id</c> of the token's tenant; definitions per tenant (duplicate code →
/// 409 <c>DUPLICATE_TEMPLATE_CODE</c>); a start needs a published template (else 404) and candidates (else 400) and is
/// idempotent per tenant + key (the SAME instance comes back); the starter cannot approve (409 <c>SOD_VIOLATION</c>);
/// cancel binds the actor to the caller. Envelope <c>{ data, errors, reason_code }</c>. Tests drive the approver side
/// (approve / reject / timeout) directly — that is WorkCenter's job, not 0175's.
/// </summary>
public sealed class FakeWorkflowGateway
{
    private readonly object _gate = new();
    private readonly Dictionary<(Guid Tenant, string Code), Definition> _definitions = new();
    private readonly Dictionary<(Guid Tenant, string Key), Instance> _byKey = new();
    private readonly Dictionary<Guid, Instance> _instances = new();

    /// <summary>Every request: method, path, Authorization header, X-Tenant-Id header.</summary>
    public List<(string Method, string Path, string? Authorization, string? Tenant)> Requests { get; } = [];

    /// <summary>Raw bodies of POST /instances (one per call, replays included).</summary>
    public List<JsonObject> StartBodies { get; } = [];

    public int CreateCalls { get; private set; }
    public int PublishCalls { get; private set; }

    /// <summary>The service is down: every call answers 503.</summary>
    public bool Down { get; set; }

    /// <summary>Callers (sub) refused on the design keys (403).</summary>
    public HashSet<Guid> DesignForbidden { get; } = [];

    public sealed class Definition
    {
        public Guid Id { get; init; }
        public string Code { get; init; } = "";
        public Guid? ActiveVersionId { get; set; }
        public int Version { get; set; }
        public string? PublishedJson { get; set; }
        public string? SchemaVersion { get; set; }
    }

    public sealed class Instance
    {
        public Guid Id { get; init; }
        public Guid TaskId { get; init; }
        public Guid Tenant { get; init; }
        public string ObjectType { get; init; } = "";
        public string ObjectId { get; init; } = "";
        public Guid StartedBy { get; init; }
        public List<string> Candidates { get; init; } = [];
        public string Status { get; set; } = "Running";
        public string TaskStatus { get; set; } = "WaitingApproval";
        public string? Outcome { get; set; }
        public DateTimeOffset? CompletedAt { get; set; }
        public List<(string Action, string Actor)> History { get; } = [];
    }

    public Definition? DefinitionOf(Guid tenant, string code)
    {
        lock (_gate) return _definitions.GetValueOrDefault((tenant, code));
    }

    public IReadOnlyList<Instance> InstancesOf(string objectId)
    {
        lock (_gate) return _instances.Values.Where(i => i.ObjectId == objectId).ToList();
    }

    // ── the approver side (WorkCenter) ───────────────────────────────────────────────────────────────────

    /// <summary>Approve as <paramref name="actor"/>: 409 SOD_VIOLATION for the starter, 403 for a non-candidate, else 200.</summary>
    public (int Status, string? Reason) Approve(string instanceId, Guid actor) => Decide(instanceId, actor, "approve", "approved");

    public (int Status, string? Reason) Reject(string instanceId, Guid actor) => Decide(instanceId, actor, "reject", "rejected");

    public void TimeOut(string instanceId)
    {
        lock (_gate)
        {
            var instance = _instances[Guid.Parse(instanceId)];
            Complete(instance, "timeout", "system", "timed-out", "TimedOut");
        }
    }

    private (int, string?) Decide(string instanceId, Guid actor, string action, string outcome)
    {
        lock (_gate)
        {
            var instance = _instances[Guid.Parse(instanceId)];
            if (instance.Status != "Running") return (409, "WORKFLOW_TASK_NOT_ACTIONABLE");
            if (actor == instance.StartedBy) return (409, "SOD_VIOLATION");
            if (!instance.Candidates.Contains("user:" + actor.ToString("D"))) return (403, "WORKFLOW_NOT_A_CANDIDATE");
            Complete(instance, action, actor.ToString("D"), outcome, outcome == "approved" ? "Approved" : "Rejected");
            return (200, null);
        }
    }

    private static void Complete(Instance instance, string action, string actor, string outcome, string taskStatus)
    {
        instance.History.Add((action, actor));
        instance.Status = "Completed";
        instance.Outcome = outcome;
        instance.TaskStatus = taskStatus;
        instance.CompletedAt = DateTimeOffset.UtcNow;
    }

    // ── HTTP ─────────────────────────────────────────────────────────────────────────────────────────────

    public async Task<HttpResponseMessage> HandleAsync(HttpRequestMessage request, CancellationToken ct)
    {
        var path = request.RequestUri!.AbsolutePath;
        var query = System.Web.HttpUtility.ParseQueryString(request.RequestUri.Query);
        var authorization = request.Headers.Authorization?.ToString();
        var tenantHeader = request.Headers.TryGetValues("X-Tenant-Id", out var values) ? values.FirstOrDefault() : null;
        var body = request.Content is null ? null : JsonNode.Parse(await request.Content.ReadAsStringAsync(ct)) as JsonObject;
        lock (_gate)
        {
            Requests.Add((request.Method.Method, path, authorization, tenantHeader));
            if (Down) return Reply(503, null, "SERVICE_UNAVAILABLE");
            if (Caller(authorization) is not { } caller) return Reply(401, null, "UNAUTHORIZED");
            if (!Guid.TryParse(tenantHeader, out var tenant) || tenant != caller.Tenant) return Reply(403, null, "TENANT_MISMATCH");

            var segments = path.Split('/', StringSplitOptions.RemoveEmptyEntries).Skip(3).ToArray(); // api/v1/workflow/...
            return (request.Method.Method, segments) switch
            {
                ("POST", ["definitions"]) => CreateDefinition(tenant, caller.User, body!),
                ("GET", ["definitions"]) => Reply(200, new JsonArray(_definitions.Where(d => d.Key.Tenant == tenant).Select(d => (JsonNode)DefinitionJson(d.Value)).ToArray())),
                ("GET", ["definitions", var id]) => _definitions.Values.FirstOrDefault(d => d.Id.ToString("D") == id) is { } d ? Reply(200, DefinitionJson(d)) : Reply(404, null, "NOT_FOUND_NON_LEAKAGE"),
                ("POST", ["definitions", var id, "publish"]) => Publish(tenant, caller.User, id, body!),
                ("GET", ["definitions", var id, "versions", var v]) => _definitions.Values.FirstOrDefault(d => d.Id.ToString("D") == id && d.ActiveVersionId?.ToString("D") == v) is { } d
                    ? Reply(200, new JsonObject { ["id"] = v, ["versionNumber"] = d.Version })
                    : Reply(404, null, "NOT_FOUND_NON_LEAKAGE"),
                ("POST", ["instances"]) => Start(tenant, caller.User, body!),
                ("GET", ["instances", "by-objects"]) => ByObjects(tenant, query["objectType"], query["objectIds"]),
                ("GET", ["instances", var id, "history"]) => _instances.GetValueOrDefault(Guid.Parse(id)) is { } i && i.Tenant == tenant
                    ? Reply(200, new JsonArray(i.History.Select((h, n) => (JsonNode)new JsonObject
                        { ["sequenceNo"] = n + 1, ["action"] = h.Action, ["actorId"] = h.Actor, ["occurredAt"] = DateTimeOffset.UtcNow.ToString("O") }).ToArray()))
                    : Reply(404, null, "NOT_FOUND_NON_LEAKAGE"),
                ("GET", ["tasks"]) => Reply(200, new JsonArray(_instances.Values.Where(i => i.Tenant == tenant).Select(i => (JsonNode)new JsonObject
                    { ["id"] = i.TaskId.ToString("D"), ["workflowInstanceId"] = i.Id.ToString("D"), ["status"] = i.TaskStatus }).ToArray())),
                ("POST", ["tasks", var id, "cancel"]) => Cancel(tenant, caller.User, id, body!),
                _ => Reply(404, null, "ROUTE_NOT_FOUND")
            };
        }
    }

    private HttpResponseMessage CreateDefinition(Guid tenant, Guid user, JsonObject body)
    {
        CreateCalls++;
        if (DesignForbidden.Contains(user)) return Reply(403, null, "FORBIDDEN");
        var code = body["templateCode"]!.GetValue<string>();
        if (_definitions.ContainsKey((tenant, code))) return Reply(409, null, "DUPLICATE_TEMPLATE_CODE");
        var definition = new Definition { Id = Guid.NewGuid(), Code = code };
        _definitions[(tenant, code)] = definition;
        return Reply(201, DefinitionJson(definition));
    }

    private HttpResponseMessage Publish(Guid tenant, Guid user, string id, JsonObject body)
    {
        PublishCalls++;
        if (DesignForbidden.Contains(user)) return Reply(403, null, "FORBIDDEN");
        var definition = _definitions.Where(d => d.Key.Tenant == tenant).Select(d => d.Value).FirstOrDefault(d => d.Id.ToString("D") == id);
        if (definition is null) return Reply(404, null, "NOT_FOUND_NON_LEAKAGE");
        var json = body["definitionJson"]!.GetValue<string>();
        JsonDocument.Parse(json).Dispose(); // Platform's only check (PublishWorkflowDefinitionHandler): parseable JSON
        definition.Version++;
        definition.ActiveVersionId = Guid.NewGuid();
        definition.PublishedJson = json;
        definition.SchemaVersion = body["schemaVersion"]!.GetValue<string>();
        return Reply(200, new JsonObject { ["templateVersionId"] = definition.ActiveVersionId?.ToString("D"), ["versionNumber"] = definition.Version });
    }

    private HttpResponseMessage Start(Guid tenant, Guid user, JsonObject body)
    {
        StartBodies.Add((JsonObject)body.DeepClone());
        var key = body["idempotencyKey"]!.GetValue<string>();
        if (_byKey.TryGetValue((tenant, key), out var existing))
        {
            return Reply(200, StartJson(existing));
        }

        var code = body["templateCode"]!.GetValue<string>();
        if (_definitions.GetValueOrDefault((tenant, code)) is not { ActiveVersionId: not null })
        {
            return Reply(404, null, "WORKFLOW_TEMPLATE_NOT_FOUND");
        }

        var candidates = body["candidatePrincipalIds"]?.AsArray().Select(c => c!.GetValue<string>()).ToList() ?? [];
        if (candidates.Count == 0) return Reply(400, null, "WORKFLOW_ASSIGNMENT_CANDIDATES_REQUIRED");

        var instance = new Instance
        {
            Id = Guid.NewGuid(), TaskId = Guid.NewGuid(), Tenant = tenant, StartedBy = user, Candidates = candidates,
            ObjectType = body["objectType"]!.GetValue<string>(), ObjectId = body["objectId"]!.GetValue<string>()
        };
        instance.History.Add(("start", user.ToString("D")));
        _instances[instance.Id] = instance;
        _byKey[(tenant, key)] = instance;
        return Reply(201, StartJson(instance));
    }

    private HttpResponseMessage ByObjects(Guid tenant, string? objectType, string? objectIds)
    {
        var ids = (objectIds ?? "").Split(',', StringSplitOptions.RemoveEmptyEntries);
        if (ids.Length is 0 or > 100) return Reply(400, null, "WORKFLOW_BY_OBJECTS_INVALID");
        return Reply(200, new JsonArray(ids.Select(id => (JsonNode)new JsonObject
        {
            ["objectId"] = id,
            ["instances"] = new JsonArray(_instances.Values.Where(i => i.Tenant == tenant && i.ObjectType == objectType && i.ObjectId == id)
                .Select(i => (JsonNode)new JsonObject
                {
                    ["workflowInstanceId"] = i.Id.ToString("D"), ["status"] = i.Status, ["outcome"] = i.Outcome,
                    ["completedAt"] = i.CompletedAt?.ToString("O")
                }).ToArray())
        }).ToArray()));
    }

    private HttpResponseMessage Cancel(Guid tenant, Guid user, string taskId, JsonObject body)
    {
        if (body.ContainsKey("actorId") && body["actorId"] is not null) return Reply(403, null, "WORKFLOW_ACTOR_MISMATCH");
        var instance = _instances.Values.FirstOrDefault(i => i.Tenant == tenant && i.TaskId.ToString("D") == taskId);
        if (instance is null) return Reply(404, null, "NOT_FOUND_NON_LEAKAGE");
        if (instance.Status != "Running") return Reply(409, null, "WORKFLOW_TASK_NOT_ACTIONABLE");
        Complete(instance, "cancel", user.ToString("D"), "cancelled", "Cancelled");
        return Reply(200, new JsonObject { ["taskId"] = taskId, ["status"] = "Cancelled" });
    }

    private static JsonObject DefinitionJson(Definition d) => new()
    {
        ["id"] = d.Id.ToString("D"), ["templateCode"] = d.Code, ["status"] = d.ActiveVersionId is null ? "Draft" : "Published",
        ["activePublishedVersionId"] = d.ActiveVersionId?.ToString("D")
    };

    private static JsonObject StartJson(Instance i) => new()
    {
        ["workflowInstanceId"] = i.Id.ToString("D"), ["approvalTaskId"] = i.TaskId.ToString("D")
    };

    private static HttpResponseMessage Reply(int status, JsonNode? data, string? reason = null)
    {
        var envelope = new JsonObject
        {
            ["data"] = data,
            ["errors"] = reason is null ? new JsonArray() : new JsonArray(reason),
            ["reason_code"] = reason
        };
        return new HttpResponseMessage((HttpStatusCode)status) { Content = new StringContent(envelope.ToJsonString(), Encoding.UTF8, "application/json") };
    }

    /// <summary>The bearer token's <c>sub</c> + <c>tenant_id</c> (the Gateway / Platform validate the signature; the fake reads the claims).</summary>
    private static (Guid User, Guid Tenant)? Caller(string? authorization)
    {
        if (authorization is null || !authorization.StartsWith("Bearer ", StringComparison.Ordinal)) return null;
        var parts = authorization["Bearer ".Length..].Split('.');
        if (parts.Length != 3) return null;
        var payload = parts[1].Replace('-', '+').Replace('_', '/');
        payload = payload.PadRight(payload.Length + (4 - payload.Length % 4) % 4, '=');
        var claims = JsonNode.Parse(Convert.FromBase64String(payload))!.AsObject();
        return Guid.TryParse(claims["sub"]?.GetValue<string>(), out var user) && Guid.TryParse(claims["tenant_id"]?.GetValue<string>(), out var tenant)
            ? (user, tenant)
            : null;
    }
}
