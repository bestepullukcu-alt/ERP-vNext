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
        Assert.True((request with { Payload = request.Payload! with { IdempotencyKey = "caller-key" } }).HasUnmappedFields);
        Assert.True((request with { Payload = request.Payload! with { PlannedDate = DateTimeOffset.UtcNow } }).HasUnmappedFields);

        var unknown = JsonSerializer.Deserialize<ProductAbbreviationWorkItemActionRequest>(
            """{"providerCode":"mdm-product-abbreviations","unexpected":true,"payload":{"expectedVersion":0}}""",
            new JsonSerializerOptions(JsonSerializerDefaults.Web))!;
        Assert.True(unknown.HasUnmappedFields);
    }
}
