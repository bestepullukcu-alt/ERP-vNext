using System.Collections.Concurrent;
using System.Reflection;
using System.Runtime.Loader;
using System.Security.Claims;
using System.Security.Cryptography;
using System.IdentityModel.Tokens.Jwt;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.IdentityModel.Tokens;
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
    [InlineData("GlobalProductCorrection")]
    [InlineData("GlobalProductRetirement")]
    public async Task Exact_global_product_lifecycle_profiles_are_allowed_for_start_and_machine_reads(
        string objectType)
    {
        var objectId = Guid.NewGuid().ToString("D");
        var request = StartRequest() with { ObjectType = objectType, ObjectId = objectId };
        var handler = new CaptureHandler(message =>
            message.RequestUri!.AbsolutePath.EndsWith("start", StringComparison.Ordinal)
                ? Success(StartResult())
                : message.RequestUri.AbsolutePath.EndsWith("start-result", StringComparison.Ordinal)
                    ? Failure(HttpStatusCode.Conflict, "WORKFLOW_START_NOT_COMPLETED")
                    : Failure(HttpStatusCode.Conflict, "WORKFLOW_DECISION_NOT_TERMINAL"));
        var client = Client(handler, new IdentityProvider());

        var start = await client.StartAsync(Guid.NewGuid(), request, "human.jwt");
        var lookup = await client.GetStartResultAsync(Guid.NewGuid(), new(
            objectType, objectId, Guid.NewGuid(), request.IdempotencyKey));
        var evidence = await client.GetTerminalEvidenceAsync(Guid.NewGuid(), new(
            Guid.NewGuid(), objectType, objectId));

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

    [Fact]
    public async Task Retirement_cancel_terminal_evidence_is_rejected_by_the_shared_transport_contract()
    {
        var request = new ProductIdentityWorkflowTerminalEvidenceRequest(
            Guid.NewGuid(), "GlobalProductRetirement", Guid.NewGuid().ToString("D"));
        var evidence = new ProductIdentityWorkflowTerminalEvidence(
            request.WorkflowInstanceId, Guid.NewGuid(), Guid.NewGuid(), Guid.NewGuid(),
            request.ExpectedObjectType, request.ExpectedObjectId, "GP-1", "Cancel",
            Guid.NewGuid().ToString("D"), "CANCELLED", DateTimeOffset.UtcNow, 2,
            "Cancelled", "Cancelled", "corr");
        var client = Client(new CaptureHandler(_ => EvidenceSuccess(evidence)), new IdentityProvider());

        var result = await client.GetTerminalEvidenceAsync(Guid.NewGuid(), request);

        Assert.Equal(ProductIdentityWorkflowTransportOutcome.Invalid, result.Outcome);
    }

    [Fact]
    public async Task Cancellation_uses_delegated_header_and_strict_versioned_evidence()
    {
        var instanceId = Guid.NewGuid();
        var taskId = Guid.NewGuid();
        var objectId = Guid.NewGuid().ToString("D");
        var maker = Guid.NewGuid();
        var request = new ProductIdentityWorkflowCancellationRequest(instanceId, taskId, "GlobalProduct",
            objectId, maker, 3, 5, "REQUESTER_WITHDRAWAL", new string('x', 2000), "cancel-key");
        var evidence = new ProductIdentityWorkflowCancellationEvidence(instanceId, taskId, Guid.NewGuid(),
            Guid.NewGuid(), "GlobalProduct", objectId, "GP-1", "Cancel", maker,
            request.ReasonCode, request.Comment, DateTimeOffset.UtcNow, 3, Guid.NewGuid(),
            "Cancelled", "Cancelled", 4, 6, false, "corr");
        var handler = new CaptureHandler(_ => EnvelopeSuccess(evidence));

        var result = await Client(handler, new IdentityProvider()).CancelAsync(
            Guid.NewGuid(), request, "human.jwt");

        Assert.Equal(ProductIdentityWorkflowTransportOutcome.Success, result.Outcome);
        var sent = Assert.Single(handler.Requests);
        Assert.Equal("/api/internal/v1/workflow/trusted-consumer/cancel", sent.Path);
        Assert.Equal("Bearer human.jwt", sent.Delegated);
        Assert.Equal("cancel-key", sent.IdempotencyKey);
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


    [Theory]
    [InlineData("GskuCorrection")]
    [InlineData("GskuRetirementRequest")]
    public async Task Gsku_profiles_cross_real_Platform_named_validation_parser_and_exact_authorization(string profile)
    {
        using var owner = new WorkflowOwnerTransport();
        var client = Client(owner, owner);
        var request = StartRequest() with { ObjectType = profile, TemplateCode = WorkflowOwnerTransport.Template(profile) };
        var start = await client.StartAsync(owner.TenantId, request, owner.DelegatedToken());
        // The boundary deliberately returns a missing test-owned workflow, never a fabricated durable success.
        Assert.Equal(ProductIdentityWorkflowTransportOutcome.NotFound, start.Outcome);
        Assert.Equal("OWNER_CONTRACT_VALIDATED_NO_WORKFLOW", start.ErrorCode);
        Assert.Equal(profile, owner.LastProfile);
        Assert.Equal(request.ObjectId, owner.LastObjectId);
        Assert.Equal(request.IdempotencyKey, owner.LastKey);
        Assert.Equal(ProductIdentityWorkflowTransportOutcome.NotFound,
            (await client.GetStartResultAsync(owner.TenantId,
                new(profile, request.ObjectId, owner.SubjectId, request.IdempotencyKey))).Outcome);
        Assert.Equal(ProductIdentityWorkflowTransportOutcome.NotFound,
            (await client.GetTerminalEvidenceAsync(owner.TenantId, new(Guid.NewGuid(), profile, request.ObjectId))).Outcome);
        Assert.Equal(3, owner.ValidatedCalls);
    }

    [Theory]
    [InlineData("GskuCorrection", "tenant")]
    [InlineData("GskuRetirementRequest", "tenant")]
    [InlineData("GskuCorrection", "audience")]
    [InlineData("GskuRetirementRequest", "audience")]
    [InlineData("GskuCorrection", "mixed-template")]
    [InlineData("GskuRetirementRequest", "mixed-template")]
    [InlineData("GskuCorrection", "wrong-client")]
    [InlineData("GskuRetirementRequest", "missing-grant")]
    [InlineData("gskucorrection", "unknown")]
    [InlineData("GskuRetirement", "unknown")]
    [InlineData("GskuCorrection.extra", "unknown")]
    [InlineData("GskuRetirementRequest*", "unknown")]
    public async Task Gsku_transport_rejects_invalid_authority_or_cross_profile_payload(string profile, string drift)
    {
        using var owner = new WorkflowOwnerTransport(drift);
        var request = StartRequest() with { ObjectType = profile,
            TemplateCode = WorkflowOwnerTransport.Template(drift == "mixed-template"
                ? profile == "GskuCorrection" ? "GskuRetirementRequest" : "GskuCorrection" : profile) };
        var result = await Client(owner, owner).StartAsync(owner.TenantId, request, owner.DelegatedToken());
        Assert.Equal(drift == "unknown" ? ProductIdentityWorkflowTransportOutcome.Invalid
            : drift == "audience" ? ProductIdentityWorkflowTransportOutcome.AuthenticationRejected
            : ProductIdentityWorkflowTransportOutcome.Forbidden, result.Outcome);
        Assert.Null(result.Value);
        Assert.Equal(0, owner.ValidatedCalls);
        if (drift == "unknown") Assert.Equal(0, owner.HttpCalls);
    }

    // Actual owner binaries are loaded from the same worktree Release build, not copied or mocked.
    // Test-only keys/config stay in memory. The final dispatch is an explicit no-workflow boundary,
    // so these are transport/security contracts, not live start or durable-acceptance claims.
    private sealed class WorkflowOwnerTransport : HttpMessageHandler, IProductIdentityWorkflowServiceIdentityProvider
    {
        private readonly Assembly api;
        private readonly object executor;
        private readonly object policy;
        private readonly ServiceProvider services;
        private readonly RSA rsa = RSA.Create(2048);
        private readonly string humanSecret = Convert.ToBase64String(RandomNumberGenerator.GetBytes(48));
        private readonly string drift;
        private readonly Func<AssemblyLoadContext, AssemblyName, Assembly?> resolver;
        private readonly Guid clientId = Guid.NewGuid();
        public Guid TenantId { get; } = Guid.NewGuid();
        public Guid SubjectId { get; } = Guid.NewGuid();
        public int ValidatedCalls { get; private set; }
        public int HttpCalls { get; private set; }
        public string? LastProfile { get; private set; }
        public string? LastObjectId { get; private set; }
        public string? LastKey { get; private set; }
        public static string Template(string profile) => profile == "GskuCorrection"
            ? "TEST-GSKU-CORRECTION" : "TEST-GSKU-RETIREMENT";

        public WorkflowOwnerTransport(string drift = "")
        {
            this.drift = drift;
            var root = new DirectoryInfo(AppContext.BaseDirectory);
            while (root is not null && !File.Exists(Path.Combine(root.FullName, "AGENTS.md"))) root = root.Parent;
            Assert.NotNull(root);
            var bin = Path.Combine(root!.FullName, "services", "Diten.Platform", "src", "Diten.Platform.API",
                "bin", "Release", "net8.0");
            resolver = (context, name) => File.Exists(Path.Combine(bin, name.Name + ".dll"))
                ? context.LoadFromAssemblyPath(Path.Combine(bin, name.Name + ".dll")) : null;
            AssemblyLoadContext.Default.Resolving += resolver;
            api = AssemblyLoadContext.Default.LoadFromAssemblyPath(Path.Combine(bin, "Diten.Platform.API.dll"));
            var collection = new ServiceCollection();
            collection.AddLogging();
            collection.AddAuthentication();
            var config = new ConfigurationBuilder().AddInMemoryCollection(new Dictionary<string, string?>
            {
                ["TrustedServiceTokenValidation:Issuer"] = "test-auth",
                ["TrustedServiceTokenValidation:CurrentKeyId"] = "test-only-key",
                ["TrustedServiceTokenValidation:CurrentPublicKeyPem"] = rsa.ExportSubjectPublicKeyInfoPem(),
                ["JwtSettings:Issuer"] = "test-human",
                ["JwtSettings:Audience"] = "test-human",
                ["JwtSettings:Secret"] = humanSecret
            }).Build();
            api.GetType("Diten.Platform.API.Security.TrustedServiceTokenValidationExtensions", true)!
                .GetMethod("AddTrustedServiceTokenValidation")!.Invoke(null, [collection, config, TimeProvider.System]);
            services = collection.BuildServiceProvider();
            var type = api.GetType("Diten.Platform.API.Security.TrustedWorkflowConsumerRequestExecutor", true)!;
            var tenantInterface = type.GetConstructors().Single().GetParameters()[1].ParameterType;
            var tenantType = tenantInterface.Assembly.GetType("Diten.Platform.Common.Tenancy.TenantContext", true)!;
            executor = Activator.CreateInstance(type,
                Activator.CreateInstance(api.GetType("Diten.Platform.API.Models.Workflow.TrustedWorkflowConsumerRequestParser", true)!)!,
                Activator.CreateInstance(tenantType)!)!;
            var optionsType = api.GetType("Diten.Platform.API.Configuration.TrustedWorkflowStartAuthorizationOptions", true)!;
            var options = Activator.CreateInstance(optionsType)!;
            var entries = (System.Collections.IList)optionsType.GetProperty("Entries")!.GetValue(options)!;
            foreach (var profile in new[] { "GskuCorrection", "GskuRetirementRequest" })
            {
                if (drift == "missing-grant") continue;
                var entry = Activator.CreateInstance(entries.GetType().GetGenericArguments()[0])!;
                Set(entry, "ClientId", drift == "wrong-client" ? Guid.NewGuid() : clientId);
                Set(entry, "ServiceName", "Diten.MDM"); Set(entry, "Audience", "TRUSTED_WORKFLOW_CONSUMER");
                Set(entry, "ObjectType", profile); Set(entry, "TemplateCode", Template(profile));
                entries.Add(entry);
            }
            policy = Activator.CreateInstance(api.GetType("Diten.Platform.API.Security.ConfiguredTrustedWorkflowStartAuthorizationPolicy", true)!,
                Activator.CreateInstance(typeof(OptionsWrapper<>).MakeGenericType(optionsType), options)!)!;
        }

        public Task<ProductIdentityWorkflowServiceIdentity> GetAsync(Guid tenantId, bool forceRefresh,
            CancellationToken cancellationToken = default)
        {
            Assert.Equal(TenantId, tenantId);
            var now = DateTimeOffset.UtcNow.ToUnixTimeSeconds();
            var token = new JwtSecurityToken("test-auth",
                drift == "audience" ? "TRUSTED_AUDIT_SOURCE_INGEST" : "TRUSTED_WORKFLOW_CONSUMER",
                [new("sub", clientId.ToString("D")), new("tenant_id", TenantId.ToString("D")),
                 new("actor_type", "service"), new("service_name", "Diten.MDM"),
                 new("jti", Guid.NewGuid().ToString("D")), new("iat", now.ToString(System.Globalization.CultureInfo.InvariantCulture), ClaimValueTypes.Integer64)],
                DateTimeOffset.FromUnixTimeSeconds(now).UtcDateTime,
                DateTimeOffset.FromUnixTimeSeconds(now + 300).UtcDateTime,
                new SigningCredentials(new RsaSecurityKey(rsa) { KeyId = "test-only-key" }, SecurityAlgorithms.RsaSha256));
            return Task.FromResult(new ProductIdentityWorkflowServiceIdentity(
                new JwtSecurityTokenHandler().WriteToken(token), DateTimeOffset.FromUnixTimeSeconds(now + 300)));
        }

        public string DelegatedToken() => new JwtSecurityTokenHandler().WriteToken(new JwtSecurityToken(
            "test-human", "test-human",
            [new("sub", SubjectId.ToString("D")),
             new("tenant_id", (drift == "tenant" ? Guid.NewGuid() : TenantId).ToString("D")),
             new("actor_type", "tenant_user"), new("permission", "platform.workflow.instances.start")],
            DateTime.UtcNow.AddSeconds(-1), DateTime.UtcNow.AddMinutes(2),
            new SigningCredentials(new SymmetricSecurityKey(Encoding.UTF8.GetBytes(humanSecret)), SecurityAlgorithms.HmacSha256)));

        private Task<IActionResult> Start<TRequest, TService, TUser>(TRequest request, string key,
            TService service, TUser user, CancellationToken ct)
        {
            var method = policy.GetType().GetMethod("IsAuthorized")!;
            var contract = method.GetParameters()[0].ParameterType;
            var authorized = (bool)method.Invoke(policy, [Activator.CreateInstance(contract,
                Get(service!, "ClientId"), Get(service!, "ServiceName"), Get(service!, "Audience"),
                Get(request!, "ObjectType"), Get(request!, "TemplateId"), Get(request!, "TemplateCode"))!])!;
            Assert.Equal(TenantId, Get(service!, "TenantId"));
            Assert.Equal(SubjectId, Get(user!, "UserId"));
            if (!authorized) return Task.FromResult(Fail(403, "WORKFLOW_TRUSTED_CONSUMER_FORBIDDEN"));
            LastKey = key;
            return Validated(request!, "ObjectType", "ObjectId");
        }
        private Task<IActionResult> Result<TRequest, TService>(TRequest request, string key, TService service, CancellationToken ct)
        {
            Assert.Equal(TenantId, Get(service!, "TenantId"));
            Assert.Equal(SubjectId, Get(request!, "ExpectedMakerSubjectId"));
            LastKey = key;
            return Validated(request!, "ExpectedObjectType", "ExpectedObjectId");
        }
        private Task<IActionResult> Evidence<TRequest, TService>(TRequest request, TService service, CancellationToken ct)
        {
            Assert.Equal(TenantId, Get(service!, "TenantId"));
            return Validated(request!, "ExpectedObjectType", "ExpectedObjectId");
        }
        private Task<IActionResult> Validated(object request, string profile, string id)
        {
            ValidatedCalls++;
            LastProfile = (string)Get(request, profile)!; LastObjectId = (string)Get(request, id)!;
            return Task.FromResult(Fail(404, "OWNER_CONTRACT_VALIDATED_NO_WORKFLOW"));
        }
        private static IActionResult Fail(int status, string code) => new ObjectResult(new
        { data = (object?)null, statusCode = status, isSuccessful = false, errors = new[] { code },
          reason_code = code, correlation_id = "owner-contract-test" }) { StatusCode = status };

        protected override async Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken ct)
        {
            HttpCalls++;
            using var scope = services.CreateScope();
            var context = new DefaultHttpContext { RequestServices = scope.ServiceProvider };
            context.Request.Method = request.Method.Method;
            foreach (var header in request.Headers) context.Request.Headers[header.Key] = header.Value.ToArray();
            Assert.False(context.Request.Headers.ContainsKey("X-Tenant-Id"));
            var bytes = await request.Content!.ReadAsByteArrayAsync(ct);
            context.Request.ContentType = "application/json"; context.Request.ContentLength = bytes.Length;
            context.Request.Body = new MemoryStream(bytes);
            var path = request.RequestUri!.AbsolutePath;
            var (methodName, callbackName, arity) = path.EndsWith("/start", StringComparison.Ordinal)
                ? ("ExecuteStartAsync", nameof(Start), 3)
                : path.EndsWith("/start-result", StringComparison.Ordinal)
                    ? ("ExecuteStartResultAsync", nameof(Result), 2)
                    : ("ExecuteEvidenceAsync", nameof(Evidence), 2);
            var method = executor.GetType().GetMethod(methodName)!;
            var delegateType = method.GetParameters()[2].ParameterType;
            var types = delegateType.GenericTypeArguments.Where(t => t.Assembly.GetName().Name!.StartsWith("Diten.Platform", StringComparison.Ordinal))
                .Take(arity).ToArray();
            var callback = GetType().GetMethod(callbackName, BindingFlags.Instance | BindingFlags.NonPublic)!
                .MakeGenericMethod(types).CreateDelegate(delegateType, this);
            var response = Assert.IsType<ObjectResult>(await (Task<IActionResult>)method.Invoke(executor,
                [context, ct, callback, (Func<int, string, IActionResult>)Fail])!);
            return Json((HttpStatusCode)response.StatusCode!, JsonSerializer.Serialize(response.Value));
        }
        private static object? Get(object instance, string name) => instance.GetType().GetProperty(name)!.GetValue(instance);
        private static void Set(object instance, string name, object value) => instance.GetType().GetProperty(name)!.SetValue(instance, value);
        protected override void Dispose(bool disposing)
        {
            if (disposing) { services.Dispose(); rsa.Dispose(); AssemblyLoadContext.Default.Resolving -= resolver; }
            base.Dispose(disposing);
        }
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
    private static HttpResponseMessage EnvelopeSuccess<T>(T result)
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
