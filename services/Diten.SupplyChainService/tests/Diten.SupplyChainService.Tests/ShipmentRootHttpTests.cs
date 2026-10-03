using System.Reflection;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Diten.SupplyChainService.Api.Controllers;
using Diten.SupplyChainService.Application.Features.Shipments;
using Diten.SupplyChainService.Infrastructure.Authorization;
using Xunit;

namespace Diten.SupplyChainService.Tests;

// Q247 (2026-10-03): this file used to hold one [Fact] whose entire body was
//   Assert.Equal("DB-010", "DB-010"); Assert.True(true);
// It could not fail, so it was not a test (SOP §24.2 vacuity rule) while still counting as one of the
// module's green results. Q245 found it. The name claimed two things — that the detail endpoint is
// read-only, and that a fixture cannot silently widen its scope — and both are now actually asserted
// against the production controller. Permissions are read through CustomAttributeData because
// HasPermissionAttribute keeps its arguments in a private field.
public sealed class ShipmentRootHttpTests
{
    private static MethodInfo Action(string name) =>
        typeof(ShipmentsController).GetMethod(name, BindingFlags.Public | BindingFlags.Instance)
        ?? throw new InvalidOperationException($"ShipmentsController.{name} no longer exists.");

    private static string[] Permissions(MethodInfo action) =>
        action.GetCustomAttributesData()
            .Where(a => a.AttributeType == typeof(HasPermissionAttribute))
            .SelectMany(a => a.ConstructorArguments
                .SelectMany(arg => arg.Value is IReadOnlyCollection<CustomAttributeTypedArgument> many
                    ? many.Select(x => (string)x.Value!)
                    : [(string)arg.Value!]))
            .ToArray();

    [Fact]
    public void AcceptanceContractRequiresScopedReadOnlyDetail()
    {
        var detail = Action("Detail");

        // Read-only: the detail endpoint is a GET and carries no mutating verb.
        Assert.NotNull(detail.GetCustomAttribute<HttpGetAttribute>());
        Assert.Null(detail.GetCustomAttribute<HttpPostAttribute>());
        Assert.Null(detail.GetCustomAttribute<HttpPutAttribute>());
        Assert.Null(detail.GetCustomAttribute<HttpPatchAttribute>());
        Assert.Null(detail.GetCustomAttribute<HttpDeleteAttribute>());

        // Scoped: exactly the read permission, so adding Create, Dispatch, Cancel or CapturePod here fails.
        Assert.Equal([ShipmentPermissions.Read], Permissions(detail));

        // The route stays under the contract family rather than at the service root.
        Assert.Equal("api/shipment-bundle/shipments",
            typeof(ShipmentsController).GetCustomAttribute<RouteAttribute>()?.Template);
        Assert.Equal("{shipmentId:guid}", detail.GetCustomAttribute<HttpGetAttribute>()!.Template);

        // The whole controller is behind authentication.
        Assert.NotNull(typeof(ShipmentsController).GetCustomAttribute<AuthorizeAttribute>());
    }

    [Fact]
    public void MutatingShipmentActionsNeverCarryTheReadPermissionAlone()
    {
        foreach (var name in new[] { "Create", "Transition", "Pod" })
        {
            var permissions = Permissions(Action(name));
            Assert.NotEmpty(permissions);
            Assert.DoesNotContain(ShipmentPermissions.Read, permissions);
        }
    }
}
