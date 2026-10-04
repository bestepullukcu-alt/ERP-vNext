using System.Reflection;
using System.Text.Json;
using Diten.MdmService.Api.Contracts.ProductAbbreviationWorkItems;
using Diten.MdmService.Api.Controllers;
using Diten.MdmService.Application.Features.ProductAbbreviationRegister.Services;
using Diten.MdmService.Application.Features.ProductAbbreviationRegister.WorkItems;
using Diten.MdmService.Infrastructure.Authorization;
using MediatR;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.Routing;
using Xunit;

namespace Diten.MdmService.Application.Tests;

public sealed class ProductAbbreviationWorkItemContractTests
{

    [Theory]
    [InlineData("productAbbreviationAllocationRequest", false)]
    [InlineData("productAbbreviationCorrectionRequest", false)]
    [InlineData("productAbbreviationRetirementRequest", false)]
    [InlineData("productAbbreviationAllocationRequest", true)]
    public async Task Serialized_MDM_projection_crosses_real_Platform_transport_and_provider_validation(string objectType, bool foreignProvider)
    {
        // Cross-service contract without adding a production dependency: run the Platform Release build first.
        var directory = new DirectoryInfo(AppContext.BaseDirectory);
        while (directory is not null && !File.Exists(Path.Combine(directory.FullName, "AGENTS.md")))
            directory = directory.Parent;
        Assert.NotNull(directory);
        var platformBin = Path.Combine(directory.FullName, "services", "Diten.Platform", "src",
            "Diten.Platform.API", "bin", "Release", "net8.0");
        var infrastructure = Assembly.LoadFrom(Path.Combine(platformBin, "Diten.Platform.Infrastructure.dll"));
        var application = Assembly.LoadFrom(Path.Combine(platformBin, "Diten.Platform.Application.dll"));
        var optionsType = infrastructure.GetType("Diten.Platform.Infrastructure.Settings.RemoteWorkItemProviderOptions", true)!;
        var gatewayType = infrastructure.GetType("Diten.Platform.Infrastructure.Services.WorkAggregation.RemoteWorkItemGateway", true)!;
        var providerType = infrastructure.GetType("Diten.Platform.Infrastructure.Services.WorkAggregation.HttpWorkItemProvider", true)!;
        var options = Activator.CreateInstance(optionsType)!;
        optionsType.GetProperty("ProviderCode")!.SetValue(options, "mdm-product-abbreviations");
        optionsType.GetProperty("BaseUrl")!.SetValue(options, "https://abb-fixture.invalid");
        optionsType.GetProperty("ProjectionPath")!.SetValue(options, "api/v1/work-items/product-abbreviations/projection");
        var id = Guid.NewGuid().ToString("D");
        var item = new ProductAbbreviationWorkItemProjection(
            "workItem", id, "approval", "approval", "notApplicable", "notApplicable", "Pending",
            "notApplicable", "notApplicable", "notApplicable", "fresh", "inline",
            ProductAbbreviationWorkItemLabel.Display("TST"),
            new("REQUESTED", ProductAbbreviationWorkItemLabel.Resource("WorkAggregation_NativeStatus_WaitingApproval")),
            new(foreignProvider ? "foreign-provider" : "mdm-product-abbreviations", "1.0", objectType, id,
                "/MDM/ProductAbbreviationRegister"), "mdm-product-abbreviations", [], [], new("version", "3"));
        var wire = JsonSerializer.Serialize(
            ProductAbbreviationWorkItemEnvelope<ProductAbbreviationWorkItemProjectionResponse>.Success(new("1.0", [item])),
            new JsonSerializerOptions(JsonSerializerDefaults.Web));
        var tenantId = Guid.NewGuid();
        using var handler = new ProjectionWireHandler(wire, tenantId);
        using var client = new HttpClient(handler);
        var factory = new ProjectionClientFactory(client);
        var http = new Microsoft.AspNetCore.Http.HttpContextAccessor { HttpContext = new Microsoft.AspNetCore.Http.DefaultHttpContext() };
        // Deliberately synthetic header fixture, not an issued token; this proves forwarding, not JWT authentication.
        http.HttpContext.Request.Headers.Authorization = "Bearer synthetic-human-fixture";
        var tenantInterface = gatewayType.GetConstructors().Single().GetParameters()[2].ParameterType;
        var tenant = DispatchProxy.Create(tenantInterface, typeof(ProjectionTenantProxy));
        ((ProjectionTenantProxy)tenant).Tenant = tenantId;
        var gateway = Activator.CreateInstance(gatewayType, factory, http, tenant)!;
        var logger = Activator.CreateInstance(
            typeof(Microsoft.Extensions.Logging.Abstractions.NullLogger<>).MakeGenericType(providerType))!;
        var provider = Activator.CreateInstance(providerType, options, gateway, logger)!;
        var actorType = application.GetType("Diten.Platform.Application.Features.WorkAggregation.WorkItemActor", true)!;
        var actor = Activator.CreateInstance(actorType, Guid.NewGuid(), false, new HashSet<string>(StringComparer.Ordinal))!;
        var task = (Task)providerType.GetMethod("GetWorkItemsAsync")!.Invoke(provider, [actor, CancellationToken.None])!;
        await task;
        var result = ((System.Collections.IEnumerable)task.GetType().GetProperty("Result")!.GetValue(task)!).Cast<object>().ToArray();
        Assert.Equal(foreignProvider ? 0 : 1, result.Length);
        Assert.Equal(1, handler.Calls);
        if (!foreignProvider)
        {
            var source = result[0].GetType().GetProperty("Source")!.GetValue(result[0])!;
            Assert.Equal(objectType, source.GetType().GetProperty("ObjectType")!.GetValue(source));
        }
    }

    public class ProjectionTenantProxy : DispatchProxy
    {
        public Guid Tenant { get; set; }
        protected override object? Invoke(MethodInfo? method, object?[]? args) => method!.Name switch
        {
            "get_TenantId" => Tenant, "get_IsResolved" => true, "get_IsPlatformContext" => false,
            "get_TargetTenantId" => null, _ => throw new InvalidOperationException(method.Name)
        };
    }
    private sealed class ProjectionClientFactory(HttpClient client) : IHttpClientFactory
    {
        public HttpClient CreateClient(string name) => client;
    }
    private sealed class ProjectionWireHandler(string wire, Guid tenant) : HttpMessageHandler
    {
        public int Calls { get; private set; }
        protected override Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken ct)
        {
            ct.ThrowIfCancellationRequested();
            Calls++;
            Assert.Equal(tenant.ToString(), Assert.Single(request.Headers.GetValues("X-Tenant-Id")));
            Assert.Equal("synthetic-human-fixture", request.Headers.Authorization?.Parameter);
            Assert.StartsWith("/api/v1/work-items/product-abbreviations/projection", request.RequestUri!.AbsolutePath);
            return Task.FromResult(new HttpResponseMessage(System.Net.HttpStatusCode.OK)
            { Content = new StringContent(wire, System.Text.Encoding.UTF8, "application/json") });
        }
    }

    [Fact]
    public void Controller_exposes_only_the_exact_remote_provider_pair()
    {
        var type = typeof(ProductAbbreviationWorkItemsController);
        Assert.NotNull(type.GetCustomAttribute<AuthorizeAttribute>());
        Assert.Equal("api/v1/work-items/product-abbreviations", type.GetCustomAttribute<RouteAttribute>()!.Template);
        Assert.Equal(typeof(IMediator), Assert.Single(type.GetConstructors()).GetParameters().Single().ParameterType);

        var projection = type.GetMethod(nameof(ProductAbbreviationWorkItemsController.GetProjection))!;
        var projectionHttp = Assert.Single(projection.GetCustomAttributes<HttpMethodAttribute>());
        Assert.Equal(["GET"], projectionHttp.HttpMethods);
        Assert.Equal("projection", projectionHttp.Template);
        Assert.Equal(
            $"Permission:{ProductAbbreviationPermissions.Read}",
            projection.GetCustomAttribute<HasPermissionAttribute>()!.Policy);

        var action = type.GetMethod(nameof(ProductAbbreviationWorkItemsController.DispatchAction))!;
        var actionHttp = Assert.Single(action.GetCustomAttributes<HttpMethodAttribute>());
        Assert.Equal(["POST"], actionHttp.HttpMethods);
        Assert.Equal("{itemId:guid}/actions/{actionCode}", actionHttp.Template);
    }

    [Fact]
    public void Wire_identity_actions_and_projection_field_count_are_frozen()
    {
        Assert.Equal("mdm-product-abbreviations", ProductAbbreviationWorkItemContract.ProviderCode);
        Assert.Equal("1.0", ProductAbbreviationWorkItemContract.ContractVersion);
        Assert.Equal(
            ["approve", "cancel", "reject"],
            ProductAbbreviationWorkItemContract.ActionCodes.Order().ToArray());
        Assert.Equal(31, typeof(ProductAbbreviationWorkItemProjection).GetProperties().Length);
        Assert.Equal(
            ["Payload", "ProviderCode", "UnmappedFields"],
            typeof(ProductAbbreviationWorkItemActionRequest).GetProperties()
                .Where(x => x.Name != nameof(ProductAbbreviationWorkItemActionRequest.HasUnmappedFields))
                .Select(x => x.Name).Order().ToArray());
    }

    [Fact]
    public void Dedicated_envelope_serializes_reason_code_and_exact_five_members()
    {
        var json = JsonSerializer.Serialize(
            ProductAbbreviationWorkItemEnvelope<object>.Fail(503, "ABBREVIATION_WORK_ITEM_BOUND_EXCEEDED"),
            new JsonSerializerOptions(JsonSerializerDefaults.Web));
        using var document = JsonDocument.Parse(json);
        var root = document.RootElement;

        Assert.Equal(5, root.EnumerateObject().Count());
        Assert.Equal("ABBREVIATION_WORK_ITEM_BOUND_EXCEEDED", root.GetProperty("reason_code").GetString());
        Assert.False(root.GetProperty("isSuccessful").GetBoolean());
    }

    [Fact]
    public void Full_shared_action_union_accepts_null_unused_members_but_rejects_caller_identity_override()
    {
        const string wire = """
            {
              "providerCode":"mdm-product-abbreviations",
              "payload":{
                "expectedVersion":0,
                "reason":null,
                "reasonCode":null,
                "note":null,
                "plannedDate":null,
                "assigneeUserId":null,
                "waitingOnUserId":null,
                "comment":null,
                "evidenceRef":null,
                "targetPrincipalId":null,
                "idempotencyKey":null
              }
            }
            """;
        var request = JsonSerializer.Deserialize<ProductAbbreviationWorkItemActionRequest>(
            wire,
            new JsonSerializerOptions(JsonSerializerDefaults.Web))!;

        Assert.False(request.HasUnmappedFields);
        // FIX1 C1: the Task Center's own key is a mapped field (it was refused as "unknown" before, so no ABB decision
        // could cross from the Task Center at all).
        Assert.False((request with { Payload = request.Payload! with { IdempotencyKey = "caller-key" } }).HasUnmappedFields);
        Assert.True((request with { Payload = request.Payload! with { PlannedDate = DateTimeOffset.UtcNow } }).HasUnmappedFields);

        var unknown = JsonSerializer.Deserialize<ProductAbbreviationWorkItemActionRequest>(
            """{"providerCode":"mdm-product-abbreviations","unexpected":true,"payload":{"expectedVersion":0}}""",
            new JsonSerializerOptions(JsonSerializerDefaults.Web))!;
        Assert.True(unknown.HasUnmappedFields);
    }

    // ── FIX1 C1 / C2 ────────────────────────────────────────────────────────────────────────────────────────

    [Theory]
    [InlineData("body-key", null, 200, "body-key")]
    [InlineData(null, "header-key", 200, "header-key")]
    [InlineData("same-key", "same-key", 200, "same-key")]
    [InlineData("body-key", "other-key", 400, null)]
    public async Task The_controller_binds_one_idempotency_key_and_refuses_two_that_disagree(
        string? bodyKey, string? headerKey, int expectedStatus, string? boundKey)
    {
        var mediator = new CapturingMediator();
        var controller = new ProductAbbreviationWorkItemsController(mediator);
        var request = new ProductAbbreviationWorkItemActionRequest(
            ProductAbbreviationWorkItemContract.ProviderCode,
            new ProductAbbreviationWorkItemActionPayload(ExpectedVersion: 0, IdempotencyKey: bodyKey));

        var result = (ObjectResult)await controller.DispatchAction(Guid.NewGuid(), "approve", request, headerKey);

        Assert.Equal(expectedStatus, result.StatusCode);
        if (expectedStatus == 400)
        {
            Assert.Null(mediator.Sent);
            Assert.Contains("WORK_ITEM_IDEMPOTENCY_KEY_CONFLICT", JsonSerializer.Serialize(result.Value));
        }
        else
        {
            Assert.Equal(boundKey, mediator.Sent!.IdempotencyKey);
        }
    }

    [Fact]
    public void Platforms_development_provider_row_reaches_the_routes_and_actions_MDM_serves()
    {
        // FIX1 C3 — without this row no ABB item reaches the Task Center. Its two paths are not the contract's
        // defaults, so they are compared with the routes on MDM's controller, and its actions with the contract's.
        var root = new DirectoryInfo(AppContext.BaseDirectory);
        while (root is not null && !File.Exists(Path.Combine(root.FullName, "AGENTS.md")))
            root = root.Parent;
        Assert.NotNull(root);
        using var settings = JsonDocument.Parse(File.ReadAllText(Path.Combine(root.FullName,
            "services", "Diten.Platform", "src", "Diten.Platform.API", "appsettings.Development.json")),
            new JsonDocumentOptions { CommentHandling = JsonCommentHandling.Skip });
        var row = Assert.Single(settings.RootElement.GetProperty("WorkAggregation").GetProperty("RemoteProviders")
                .EnumerateArray(),
            item => item.GetProperty("ProviderCode").GetString() == ProductAbbreviationWorkItemContract.ProviderCode);
        var controller = typeof(ProductAbbreviationWorkItemsController);
        var prefix = controller.GetCustomAttribute<RouteAttribute>()!.Template;
        var projection = controller.GetMethod(nameof(ProductAbbreviationWorkItemsController.GetProjection))!
            .GetCustomAttribute<HttpGetAttribute>()!.Template;
        var action = controller.GetMethod(nameof(ProductAbbreviationWorkItemsController.DispatchAction))!
            .GetCustomAttribute<HttpPostAttribute>()!.Template!.Replace("{itemId:guid}", "{itemId}", StringComparison.Ordinal);

        Assert.Equal(ProductAbbreviationWorkItemContract.ContractVersion, row.GetProperty("ContractVersion").GetString());
        Assert.Equal($"{prefix}/{projection}", row.GetProperty("ProjectionPath").GetString());
        Assert.Equal($"{prefix}/{action}", row.GetProperty("ActionPathTemplate").GetString());
        Assert.True(Uri.TryCreate(row.GetProperty("BaseUrl").GetString(), UriKind.Absolute, out _));
        var actions = row.GetProperty("Actions").EnumerateObject().ToDictionary(x => x.Name, x => x.Value.GetString());
        Assert.Equal(ProductAbbreviationWorkItemContract.ActionCodes.Order(), actions.Keys.Order());
        Assert.Equal(ProductAbbreviationPermissions.Approve, actions["approve"]);
        Assert.Equal(ProductAbbreviationPermissions.Reject, actions["reject"]);
        Assert.Equal(ProductAbbreviationPermissions.Cancel, actions["cancel"]);
    }

    [Fact]
    public async Task The_Task_Centers_real_body_crosses_Platform_and_binds_in_MDM()
    {
        // FIX1 C2 — the browser half (tests/abb-work-item-action-contract.test.js) pins this fixture to what
        // app.js + work-items-api.js actually post. Here it goes through Platform's REAL HttpWorkItemActionDispatcher
        // (Release build, as the projection test above) and the bytes Platform forwards are bound by MDM's own model.
        var root = new DirectoryInfo(AppContext.BaseDirectory);
        while (root is not null && !File.Exists(Path.Combine(root.FullName, "AGENTS.md")))
            root = root.Parent;
        Assert.NotNull(root);
        using var fixture = JsonDocument.Parse(File.ReadAllText(Path.Combine(root.FullName,
            "frontend", "Diten.Web", "tests", "fixtures", "abb-work-item-action-approve.wcn-body.json")));
        var browserPayload = fixture.RootElement.GetProperty("payload");
        var fixtureKey = browserPayload.GetProperty("idempotencyKey").GetString();
        var web = new JsonSerializerOptions(JsonSerializerDefaults.Web);

        var platformBin = Path.Combine(root.FullName, "services", "Diten.Platform", "src",
            "Diten.Platform.API", "bin", "Release", "net8.0");
        var infrastructure = Assembly.LoadFrom(Path.Combine(platformBin, "Diten.Platform.Infrastructure.dll"));
        var application = Assembly.LoadFrom(Path.Combine(platformBin, "Diten.Platform.Application.dll"));
        var optionsType = infrastructure.GetType("Diten.Platform.Infrastructure.Settings.RemoteWorkItemProviderOptions", true)!;
        var gatewayType = infrastructure.GetType("Diten.Platform.Infrastructure.Services.WorkAggregation.RemoteWorkItemGateway", true)!;
        var dispatcherType = infrastructure.GetType("Diten.Platform.Infrastructure.Services.WorkAggregation.HttpWorkItemActionDispatcher", true)!;
        var payloadType = application.GetType("Diten.Platform.Application.Features.WorkAggregation.Dispatch.WorkItemActionPayloadDto", true)!;
        var requestType = application.GetType("Diten.Platform.Application.Features.WorkAggregation.Dispatch.WorkItemActionDispatchRequest", true)!;
        var actorType = application.GetType("Diten.Platform.Application.Features.WorkAggregation.WorkItemActor", true)!;
        var resilienceType = application.GetType("Diten.Platform.Application.Features.WorkAggregation.Services.WorkAggregationResilienceOptions", true)!;

        var row = Activator.CreateInstance(optionsType)!;
        optionsType.GetProperty("ProviderCode")!.SetValue(row, ProductAbbreviationWorkItemContract.ProviderCode);
        optionsType.GetProperty("BaseUrl")!.SetValue(row, "https://abb-fixture.invalid");
        optionsType.GetProperty("ProjectionPath")!.SetValue(row, "api/v1/work-items/product-abbreviations/projection");
        optionsType.GetProperty("ActionPathTemplate")!.SetValue(row, "api/v1/work-items/product-abbreviations/{itemId}/actions/{actionCode}");
        optionsType.GetProperty("Actions")!.SetValue(row, new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase)
        {
            ["approve"] = ProductAbbreviationPermissions.Approve
        });
        var itemId = Guid.Parse("6b0f5d43-2c1e-4d8a-9a57-0c2f6f1e7a10");
        var answer = JsonSerializer.Serialize(
            ProductAbbreviationWorkItemEnvelope<ProductAbbreviationWorkItemActionResponse>.Success(
                new(itemId.ToString("D"), ProductAbbreviationWorkItemContract.ProviderCode, "approve")),
            web);
        var tenantId = Guid.NewGuid();
        using var handler = new CapturingHandler(answer);
        using var client = new HttpClient(handler);
        var http = new Microsoft.AspNetCore.Http.HttpContextAccessor { HttpContext = new Microsoft.AspNetCore.Http.DefaultHttpContext() };
        http.HttpContext.Request.Headers.Authorization = "Bearer synthetic-human-fixture";
        var tenantInterface = gatewayType.GetConstructors().Single().GetParameters()[2].ParameterType;
        var tenant = DispatchProxy.Create(tenantInterface, typeof(ProjectionTenantProxy));
        ((ProjectionTenantProxy)tenant).Tenant = tenantId;
        var gateway = Activator.CreateInstance(gatewayType, new ProjectionClientFactory(client), http, tenant)!;
        var resilience = typeof(Microsoft.Extensions.Options.Options).GetMethod("Create")!
            .MakeGenericMethod(resilienceType).Invoke(null, [Activator.CreateInstance(resilienceType)])!;
        var logger = Activator.CreateInstance(
            typeof(Microsoft.Extensions.Logging.Abstractions.NullLogger<>).MakeGenericType(dispatcherType))!;
        var dispatcher = Activator.CreateInstance(dispatcherType, row, gateway, resilience, logger)!;
        var payload = JsonSerializer.Deserialize(browserPayload.GetRawText(), payloadType, web)!;
        var actor = Activator.CreateInstance(actorType, Guid.NewGuid(), false, new HashSet<string>(StringComparer.Ordinal))!;
        var dispatch = Activator.CreateInstance(requestType, itemId, "approve", payload, actor, "contract-correlation")!;

        var task = (Task)dispatcherType.GetMethod("DispatchAsync")!.Invoke(dispatcher, [dispatch, CancellationToken.None])!;
        await task;

        Assert.Equal($"/api/v1/work-items/product-abbreviations/{itemId:D}/actions/approve", handler.Path);
        var forwarded = JsonSerializer.Deserialize<ProductAbbreviationWorkItemActionRequest>(handler.Body!, web)!;
        Assert.False(forwarded.HasUnmappedFields);
        Assert.Equal(0, forwarded.Payload!.ExpectedVersion);
        Assert.Equal(fixtureKey, forwarded.Payload.IdempotencyKey);

        var mediator = new CapturingMediator();
        var bound = (ObjectResult)await new ProductAbbreviationWorkItemsController(mediator)
            .DispatchAction(itemId, "approve", forwarded);
        Assert.Equal(200, bound.StatusCode);
        Assert.Equal(fixtureKey, mediator.Sent!.IdempotencyKey);
        Assert.Equal(0, mediator.Sent.ExpectedVersion);
    }

    [Theory]
    [InlineData("client:key", 400)]
    [InlineData("X:former", 400)]
    [InlineData("plain-client-key", 0)]
    public void A_client_idempotency_key_with_a_colon_is_refused_before_any_handler(string key, int expectedStatus)
    {
        // FIX2 A5 — the server derives keys with ':'-separated suffixes; a client may not name one.
        var http = new Microsoft.AspNetCore.Http.DefaultHttpContext();
        http.Request.Headers["Idempotency-Key"] = key;
        var context = new Microsoft.AspNetCore.Mvc.Filters.ActionExecutingContext(
            new Microsoft.AspNetCore.Mvc.ActionContext(http, new Microsoft.AspNetCore.Routing.RouteData(),
                new Microsoft.AspNetCore.Mvc.Abstractions.ActionDescriptor()),
            [], new Dictionary<string, object?>(), controller: new object());

        new Diten.MdmService.Api.Contracts.ProductAbbreviations.ClientIdempotencyKeyFilterAttribute().OnActionExecuting(context);

        if (expectedStatus == 0)
        {
            Assert.Null(context.Result);
            return;
        }

        var refused = Assert.IsType<ObjectResult>(context.Result);
        Assert.Equal(expectedStatus, refused.StatusCode);
        Assert.Contains("ABBREVIATION_IDEMPOTENCY_KEY_INVALID", JsonSerializer.Serialize(refused.Value));
    }

    private sealed class CapturingHandler(string answer) : HttpMessageHandler
    {
        public string? Body { get; private set; }
        public string? Path { get; private set; }

        protected override async Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken ct)
        {
            Path = request.RequestUri!.AbsolutePath;
            Body = await request.Content!.ReadAsStringAsync(ct);
            return new HttpResponseMessage(System.Net.HttpStatusCode.OK)
            { Content = new StringContent(answer, System.Text.Encoding.UTF8, "application/json") };
        }
    }

    private sealed class CapturingMediator : IMediator
    {
        public Diten.MdmService.Application.Features.ProductAbbreviationRegister.WorkItems.Commands
            .DispatchProductAbbreviationWorkItemActionCommand? Sent { get; private set; }

        public Task<TResponse> Send<TResponse>(IRequest<TResponse> request, CancellationToken cancellationToken = default)
        {
            Sent = (Diten.MdmService.Application.Features.ProductAbbreviationRegister.WorkItems.Commands
                .DispatchProductAbbreviationWorkItemActionCommand)(object)request;
            object result = ProductAbbreviationWorkItemOperationResult<ProductAbbreviationWorkItemActionResponse>.Success(
                new(Sent.ItemId.ToString("D"), ProductAbbreviationWorkItemContract.ProviderCode, Sent.ActionCode));
            return Task.FromResult((TResponse)result);
        }

        public Task Send<TRequest>(TRequest request, CancellationToken cancellationToken = default) where TRequest : IRequest
            => throw new NotSupportedException();
        public Task<object?> Send(object request, CancellationToken cancellationToken = default) => throw new NotSupportedException();
        public IAsyncEnumerable<TResponse> CreateStream<TResponse>(IStreamRequest<TResponse> request, CancellationToken cancellationToken = default)
            => throw new NotSupportedException();
        public IAsyncEnumerable<object?> CreateStream(object request, CancellationToken cancellationToken = default)
            => throw new NotSupportedException();
        public Task Publish(object notification, CancellationToken cancellationToken = default) => throw new NotSupportedException();
        public Task Publish<TNotification>(TNotification notification, CancellationToken cancellationToken = default)
            where TNotification : INotification => throw new NotSupportedException();
    }
}
