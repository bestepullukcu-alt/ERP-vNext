using System.Collections.Concurrent;
using System.Net;
using System.Text;
using System.Text.Json;
using Diten.MdmService.Application.Contracts.Workflow;
using Diten.MdmService.Infrastructure.Workflow;
using Microsoft.Extensions.Options;
using Xunit;

namespace Diten.MdmService.Application.Tests.Workflow;

public sealed class PlatformProductIdentityWorkflowClientTests
{
    [Fact]
    public async Task Start_uses_exact_headers_and_header_only_idempotency()
    {
        var handler = new CaptureHandler(_ => Success(StartResult()));
        var identities = new IdentityProvider();
        var client = Client(handler, identities);
        var request = StartRequest();

        var result = await client.StartAsync(Guid.NewGuid(), request, "human.jwt", CancellationToken.None);

        Assert.Equal(ProductIdentityWorkflowTransportOutcome.Success, result.Outcome);
        var sent = Assert.Single(handler.Requests);
        Assert.Equal("/api/internal/v1/workflow/trusted-consumer/start", sent.Path);
        Assert.Equal("Bearer service-token-1", sent.Authorization);
        Assert.Equal("Bearer human.jwt", sent.Delegated);
        Assert.Equal(request.IdempotencyKey, sent.IdempotencyKey);
        Assert.DoesNotContain("idempotency", sent.Body, StringComparison.OrdinalIgnoreCase);
        Assert.DoesNotContain("tenant", sent.Body, StringComparison.OrdinalIgnoreCase);
    }

    [Theory]
    [InlineData("gsku", "GSKU-IDENTITY", "GS-000000000001|REV-001")]
    [InlineData("lsku", "LSKU-IDENTITY", "LS-000000000001")]
    [InlineData("finished-good", "FINISHED-GOOD-IDENTITY", "FG-000000000001")]
    public async Task Exact_lowercase_product_identity_profiles_are_allowed_for_start_and_machine_reads(
        string objectType,
        string templateCode,
        string objectRef)
    {
        var gskuId = Guid.NewGuid().ToString("D");
        var startRequest = StartRequest() with
        {
            TemplateCode = templateCode,
            ObjectType = objectType,
            ObjectId = gskuId,
            ObjectRef = objectRef
        };
        var handler = new CaptureHandler(request =>
            request.RequestUri!.AbsolutePath.EndsWith("start", StringComparison.Ordinal)
                ? Success(StartResult() with { ObjectRef = startRequest.ObjectRef! })
                : request.RequestUri.AbsolutePath.EndsWith("start-result", StringComparison.Ordinal)
                    ? Failure(HttpStatusCode.Conflict, "WORKFLOW_START_NOT_COMPLETED")
                    : Failure(HttpStatusCode.Conflict, "WORKFLOW_DECISION_NOT_TERMINAL"));
        var client = Client(handler, new IdentityProvider());

        var start = await client.StartAsync(Guid.NewGuid(), startRequest, "human.jwt");
        var lookup = await client.GetStartResultAsync(Guid.NewGuid(), new(
            objectType, gskuId, Guid.NewGuid(), startRequest.IdempotencyKey));
        var evidence = await client.GetTerminalEvidenceAsync(Guid.NewGuid(), new(
            Guid.NewGuid(), objectType, gskuId));

        Assert.Equal(ProductIdentityWorkflowTransportOutcome.Success, start.Outcome);
        Assert.Equal(ProductIdentityWorkflowTransportOutcome.Incomplete, lookup.Outcome);
        Assert.Equal(ProductIdentityWorkflowTransportOutcome.NonTerminal, evidence.Outcome);
    }

    [Theory]
    [InlineData("Gsku")]
    [InlineData("GSKU")]
    [InlineData("global-product")]
    [InlineData("gsku ")]
    public async Task Unapproved_object_type_profiles_fail_before_transport(string objectType)
    {
        var handler = new CaptureHandler(_ => throw new InvalidOperationException("Transport must not run."));
        var client = Client(handler, new IdentityProvider());
        var request = StartRequest() with { ObjectType = objectType };

        var start = await client.StartAsync(Guid.NewGuid(), request, "human.jwt");
        var lookup = await client.GetStartResultAsync(Guid.NewGuid(), new(
            objectType, request.ObjectId, Guid.NewGuid(), request.IdempotencyKey));
        var evidence = await client.GetTerminalEvidenceAsync(Guid.NewGuid(), new(
            Guid.NewGuid(), objectType, request.ObjectId));

        Assert.Equal(ProductIdentityWorkflowTransportOutcome.Invalid, start.Outcome);
        Assert.Equal(ProductIdentityWorkflowTransportOutcome.Invalid, lookup.Outcome);
        Assert.Equal(ProductIdentityWorkflowTransportOutcome.Invalid, evidence.Outcome);
        Assert.Empty(handler.Requests);
    }

    [Fact]
    public async Task Unauthorized_forces_exactly_one_refresh_and_one_replay()
    {
        var count = 0;
        var handler = new CaptureHandler(_ => Interlocked.Increment(ref count) == 1
            ? Failure(HttpStatusCode.Unauthorized, "UNAUTHENTICATED")
            : Success(StartResult()));
        var identities = new IdentityProvider();

        var result = await Client(handler, identities).StartAsync(Guid.NewGuid(), StartRequest(), "human.jwt");

        Assert.Equal(ProductIdentityWorkflowTransportOutcome.Success, result.Outcome);
        Assert.Equal([false, true], identities.Refreshes);
        Assert.Equal(2, handler.Requests.Count);
    }

    [Fact]
    public async Task Machine_reads_do_not_forward_delegated_authority_and_map_recovery_states()
    {
        var handler = new CaptureHandler(request => request.RequestUri!.AbsolutePath.EndsWith("start-result", StringComparison.Ordinal)
            ? Failure(HttpStatusCode.Conflict, "WORKFLOW_START_NOT_COMPLETED")
            : Failure(HttpStatusCode.Conflict, "WORKFLOW_DECISION_NOT_TERMINAL"));
        var client = Client(handler, new IdentityProvider());

        var start = await client.GetStartResultAsync(Guid.NewGuid(), new("GlobalProduct", Guid.NewGuid().ToString("D"), Guid.NewGuid(), "stable-key"));
        var evidence = await client.GetTerminalEvidenceAsync(Guid.NewGuid(), new(Guid.NewGuid(), "GlobalProduct", Guid.NewGuid().ToString("D")));

        Assert.Equal(ProductIdentityWorkflowTransportOutcome.Incomplete, start.Outcome);
        Assert.Equal(ProductIdentityWorkflowTransportOutcome.NonTerminal, evidence.Outcome);
        var requests = handler.Requests.ToArray();
        Assert.All(requests, x => Assert.Null(x.Delegated));
        Assert.NotNull(requests[0].IdempotencyKey);
        Assert.Null(requests[1].IdempotencyKey);
    }

    [Fact]
    public async Task Unknown_success_fields_and_nonleaking_not_found_are_not_conflated()
    {
        var count = 0;
        var handler = new CaptureHandler(_ => Interlocked.Increment(ref count) == 1
            ? Success(StartResult(), extraData: true)
            : Failure(HttpStatusCode.NotFound, "NOT_FOUND_NON_LEAKAGE"));
        var client = Client(handler, new IdentityProvider());

        var invalid = await client.StartAsync(Guid.NewGuid(), StartRequest(), "human.jwt");
        var missing = await client.GetStartResultAsync(Guid.NewGuid(), new("GlobalProduct", Guid.NewGuid().ToString("D"), Guid.NewGuid(), "key"));

        Assert.Equal(ProductIdentityWorkflowTransportOutcome.Invalid, invalid.Outcome);
        Assert.Equal(ProductIdentityWorkflowTransportOutcome.NotFound, missing.Outcome);
    }

    [Fact]
    public async Task Start_success_without_immutable_started_at_is_rejected()
    {
        var client = Client(
            new CaptureHandler(_ => Success(StartResult() with { StartedAt = null })),
            new IdentityProvider());

        var result = await client.StartAsync(Guid.NewGuid(), StartRequest(), "human.jwt");

        Assert.Equal(ProductIdentityWorkflowTransportOutcome.Invalid, result.Outcome);
    }

    [Fact]
    public async Task Contradictory_terminal_statuses_are_rejected_at_transport_boundary()
    {
        var request = new ProductIdentityWorkflowTerminalEvidenceRequest(
            Guid.NewGuid(), "GlobalProduct", Guid.NewGuid().ToString("D"));
        var evidence = new ProductIdentityWorkflowTerminalEvidence(
            request.WorkflowInstanceId, Guid.NewGuid(), Guid.NewGuid(), Guid.NewGuid(),
            request.ExpectedObjectType, request.ExpectedObjectId, "GP-1", "Approve",
            Guid.NewGuid().ToString("D"), "APPROVED", DateTimeOffset.UtcNow, 2,
            "Pending", "Active", "corr");
        var client = Client(new CaptureHandler(_ => EvidenceSuccess(evidence)), new IdentityProvider());

        var result = await client.GetTerminalEvidenceAsync(Guid.NewGuid(), request);

        Assert.Equal(ProductIdentityWorkflowTransportOutcome.Invalid, result.Outcome);
    }

    [Theory]
    [InlineData(HttpStatusCode.BadRequest, "INVALID", ProductIdentityWorkflowTransportOutcome.Invalid)]
    [InlineData(HttpStatusCode.Forbidden, "FORBIDDEN", ProductIdentityWorkflowTransportOutcome.Forbidden)]
    [InlineData(HttpStatusCode.Conflict, "WORKFLOW_TERMINAL_EVIDENCE_INCONSISTENT", ProductIdentityWorkflowTransportOutcome.Conflict)]
    [InlineData(HttpStatusCode.ServiceUnavailable, "UNAVAILABLE", ProductIdentityWorkflowTransportOutcome.Retryable)]
    [InlineData(HttpStatusCode.GatewayTimeout, "TIMEOUT", ProductIdentityWorkflowTransportOutcome.Timeout)]
    public async Task Failure_statuses_map_without_false_success(
        HttpStatusCode status,
        string code,
        ProductIdentityWorkflowTransportOutcome expected)
    {
        var client = Client(new CaptureHandler(_ => Failure(status, code)), new IdentityProvider());

        var result = await client.GetStartResultAsync(Guid.NewGuid(), new(
            "GlobalProduct", Guid.NewGuid().ToString("D"), Guid.NewGuid(), "key"));

        Assert.Equal(expected, result.Outcome);
        Assert.Null(result.Value);
    }

    private static PlatformProductIdentityWorkflowClient Client(HttpMessageHandler handler, IProductIdentityWorkflowServiceIdentityProvider identities) =>
        new(new Factory(handler), identities, Options.Create(new ProductIdentityWorkflowClientOptions { PlatformBaseUrl = "http://platform.test" }));
    private static ProductIdentityWorkflowStartRequest StartRequest() => new(
        null, "GLOBAL-PRODUCT-IDENTITY", "GlobalProduct", Guid.NewGuid().ToString("D"), "GP-1",
        [Guid.NewGuid().ToString("D")], "IDENTITY_APPROVAL", "stable-key", true, true, null);
    private static ProductIdentityWorkflowStartResult StartResult() => new(
        Guid.NewGuid(), Guid.NewGuid(), Guid.NewGuid(), Guid.NewGuid(), Guid.NewGuid(), Guid.NewGuid(), "GP-1",
        "Active", "APPROVAL", "REVIEW", DateTimeOffset.UtcNow, null, false, "corr");

    private static HttpResponseMessage Success(ProductIdentityWorkflowStartResult result, bool extraData = false)
    {
        var data = JsonSerializer.SerializeToNode(result, new JsonSerializerOptions(JsonSerializerDefaults.Web))!.AsObject();
        if (extraData) data["secret"] = "must-reject";
        var root = new System.Text.Json.Nodes.JsonObject
        {
            ["data"] = data, ["statusCode"] = 201, ["isSuccessful"] = true,
            ["errors"] = new System.Text.Json.Nodes.JsonArray(), ["reason_code"] = null, ["correlation_id"] = "corr"
        };
        return Json(HttpStatusCode.Created, root.ToJsonString());
    }
    private static HttpResponseMessage EvidenceSuccess(ProductIdentityWorkflowTerminalEvidence result)
    {
        var root = new System.Text.Json.Nodes.JsonObject
        {
            ["data"] = JsonSerializer.SerializeToNode(result, new JsonSerializerOptions(JsonSerializerDefaults.Web)),
            ["statusCode"] = 200,
            ["isSuccessful"] = true,
            ["errors"] = new System.Text.Json.Nodes.JsonArray(),
            ["reason_code"] = null,
            ["correlation_id"] = "corr"
        };
        return Json(HttpStatusCode.OK, root.ToJsonString());
    }
    private static HttpResponseMessage Failure(HttpStatusCode status, string code) => Json(status, JsonSerializer.Serialize(new Dictionary<string, object?>
    {
        ["data"] = null, ["statusCode"] = (int)status, ["isSuccessful"] = false,
        ["errors"] = new[] { code }, ["reason_code"] = code, ["correlation_id"] = "corr"
    }));
    private static HttpResponseMessage Json(HttpStatusCode status, string json) => new(status)
    {
        Content = new StringContent(json, Encoding.UTF8, "application/json")
    };

    private sealed class IdentityProvider : IProductIdentityWorkflowServiceIdentityProvider
    {
        private int _count;
        public List<bool> Refreshes { get; } = [];
        public Task<ProductIdentityWorkflowServiceIdentity> GetAsync(Guid tenantId, bool forceRefresh, CancellationToken cancellationToken = default)
        {
            Refreshes.Add(forceRefresh);
            var count = Interlocked.Increment(ref _count);
            return Task.FromResult(new ProductIdentityWorkflowServiceIdentity($"service-token-{count}", DateTimeOffset.UtcNow.AddMinutes(5)));
        }
    }
    private sealed class CaptureHandler(Func<HttpRequestMessage, HttpResponseMessage> responder) : HttpMessageHandler
    {
        public ConcurrentQueue<Captured> Requests { get; } = new();
        protected override async Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken cancellationToken)
        {
            Requests.Enqueue(new(
                request.RequestUri!.AbsolutePath,
                request.Headers.Authorization?.ToString(),
                request.Headers.TryGetValues(PlatformProductIdentityWorkflowClient.DelegatedAuthorizationHeader, out var delegated) ? delegated.Single() : null,
                request.Headers.TryGetValues(PlatformProductIdentityWorkflowClient.IdempotencyKeyHeader, out var key) ? key.Single() : null,
                await request.Content!.ReadAsStringAsync(cancellationToken)));
            return responder(request);
        }
    }
    private sealed record Captured(string Path, string? Authorization, string? Delegated, string? IdempotencyKey, string Body);
    private sealed class Factory(HttpMessageHandler handler) : IHttpClientFactory
    {
        public HttpClient CreateClient(string name) => new(handler, false);
    }
}
