using System.Reflection;
using System.Text.Json;
using Diten.MdmService.Api.Controllers;
using Diten.MdmService.Application.Features.ProductLegalEntityScopes;
using MediatR;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.Routing;
using Xunit;

namespace Diten.MdmService.Application.Tests;

public sealed class ProductLegalEntityScopeApiContractTests
{
    [Fact]
    public void Controller_exposes_exactly_seven_nested_routes_and_no_completeness_or_lifecycle_surface()
    {
        var type = typeof(ProductLegalEntityScopesController);
        Assert.Equal("api/global-products/{globalProductId:guid}/legal-entity-scope-policy", type.GetCustomAttribute<RouteAttribute>()!.Template);
        var routes = type.GetMethods(BindingFlags.Public | BindingFlags.Instance | BindingFlags.DeclaredOnly)
            .SelectMany(method => method.GetCustomAttributes<HttpMethodAttribute>().Select(attribute => (method.Name, Attribute: attribute)))
            .ToArray();
        Assert.Equal(7, routes.Length);
        Assert.Equal(new[] { "", "history", "effective", "create-options", "", "replace", "end" }.Order(),
            routes.Select(x => x.Attribute.Template ?? string.Empty).Order());
        Assert.DoesNotContain(routes, route => route.Attribute.HttpMethods.Any(x => x is "DELETE" or "PATCH" or "PUT"));
        Assert.DoesNotContain(routes, route => (route.Attribute.Template ?? "").Contains("complete", StringComparison.OrdinalIgnoreCase));
        Assert.DoesNotContain(routes, route => (route.Attribute.Template ?? "").Contains("activ", StringComparison.OrdinalIgnoreCase));
    }

    [Theory]
    [InlineData("tenantId")]
    [InlineData("actorId")]
    [InlineData("effectiveFromUtc")]
    [InlineData("auditEvidence")]
    [InlineData("unknown")]
    public async Task Unknown_or_technical_create_body_fields_fail_before_mediator(string field)
    {
        var json = $$"""{"mode":1,"legalEntityIds":[],"{{field}}":"x"}""";
        var request = JsonSerializer.Deserialize<ProductLegalEntityScopeModels.CreatePolicyRequest>(json,
            new JsonSerializerOptions { PropertyNameCaseInsensitive = true })!;
        var mediator = DispatchProxy.Create<IMediator, ScopeThrowingMediator>();
        var controller = Controller(mediator, Guid.NewGuid().ToString("D"));

        var result = await controller.CreatePolicy(Guid.NewGuid(), request, default);

        Assert.IsType<BadRequestObjectResult>(result);
        Assert.False(((ScopeThrowingMediator)(object)mediator).Called);
    }

    [Theory]
    [InlineData("")]
    [InlineData("00000000-0000-0000-0000-000000000000")]
    [InlineData("not-a-guid")]
    [InlineData("{5CB88EA0-EE09-4A32-8262-C77AD4F94E06}")]
    public async Task Missing_empty_or_noncanonical_idempotency_header_fails_before_mediator(string value)
    {
        var mediator = DispatchProxy.Create<IMediator, ScopeThrowingMediator>();
        var controller = Controller(mediator, value);
        var result = await controller.CreatePolicy(Guid.NewGuid(), new()
        {
            Mode = Domain.Enums.ProductLegalEntityScopeMode.GroupWide
        }, default);
        Assert.IsType<BadRequestObjectResult>(result);
        Assert.False(((ScopeThrowingMediator)(object)mediator).Called);
    }

    [Fact]
    public async Task Replace_and_end_unknown_fields_and_multiple_idempotency_values_fail_before_mediator()
    {
        var mediator = DispatchProxy.Create<IMediator, ScopeThrowingMediator>();
        var controller = Controller(mediator, Guid.NewGuid().ToString("D"));
        var unknown = new Dictionary<string, JsonElement> { ["tenantId"] = JsonSerializer.SerializeToElement(Guid.NewGuid()) };

        Assert.IsType<BadRequestObjectResult>(await controller.ReplacePolicy(Guid.NewGuid(), new()
        {
            ExpectedVersion = 0, Mode = Domain.Enums.ProductLegalEntityScopeMode.GroupWide,
            UnmappedFields = unknown
        }, default));
        Assert.IsType<BadRequestObjectResult>(await controller.EndPolicy(Guid.NewGuid(), new()
        {
            ExpectedVersion = 0, UnmappedFields = unknown
        }, default));

        controller.ControllerContext.HttpContext.Request.Headers["Idempotency-Key"] =
            new Microsoft.Extensions.Primitives.StringValues([Guid.NewGuid().ToString("D"), Guid.NewGuid().ToString("D")]);
        Assert.IsType<BadRequestObjectResult>(await controller.CreatePolicy(Guid.NewGuid(), new()
        {
            Mode = Domain.Enums.ProductLegalEntityScopeMode.GroupWide
        }, default));
        Assert.False(((ScopeThrowingMediator)(object)mediator).Called);
    }

    [Fact]
    public void Public_write_models_have_only_business_fields_plus_extension_guard()
    {
        Assert.Equal(["LegalEntityIds", "Mode", "UnmappedFields"], Properties<ProductLegalEntityScopeModels.CreatePolicyRequest>());
        Assert.Equal(["ExpectedVersion", "LegalEntityIds", "Mode", "UnmappedFields"], Properties<ProductLegalEntityScopeModels.ReplacePolicyRequest>());
        Assert.Equal(["ExpectedVersion", "UnmappedFields"], Properties<ProductLegalEntityScopeModels.EndPolicyRequest>());
    }

    private static string[] Properties<T>() => typeof(T).GetProperties().Select(x => x.Name).Order(StringComparer.Ordinal).ToArray();

    private static ProductLegalEntityScopesController Controller(IMediator mediator, string header)
    {
        var context = new DefaultHttpContext();
        if (header.Length > 0) context.Request.Headers["Idempotency-Key"] = header;
        return new ProductLegalEntityScopesController(mediator)
        {
            ControllerContext = new ControllerContext { HttpContext = context }
        };
    }

    private class ScopeThrowingMediator : DispatchProxy
    {
        public bool Called { get; private set; }
        protected override object? Invoke(MethodInfo? targetMethod, object?[]? args)
        {
            Called = true;
            throw new InvalidOperationException("Mediator must not be called.");
        }
    }
}
