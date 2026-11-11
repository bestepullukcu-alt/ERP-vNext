using System.Reflection;
using Diten.InventoryService.Api.Controllers;
using Diten.InventoryService.Application.Features.Inventory.Ports;
using Microsoft.AspNetCore.Mvc;
using Xunit;

namespace Diten.InventoryService.Tests.Architecture;

/// <summary>
/// MOD-0173-FU03 AC-16 (§8.7) — the in-process reservation port is MOD-0178's door, never an HTTP one: no controller takes it (constructor or
/// action parameter) or names it in its source; it checks no HTTP permission, so a controller using it would bypass [HasPermission].
/// </summary>
public sealed class ReservationPortArchitectureTests
{
    [Fact]
    public void No_controller_uses_the_reservation_port()
    {
        var controllers = typeof(InventoryController).Assembly.GetTypes()
            .Where(t => typeof(ControllerBase).IsAssignableFrom(t) && !t.IsAbstract).ToList();
        Assert.True(controllers.Count > 5, "vacuous scan: " + controllers.Count);

        foreach (var controller in controllers)
        {
            var parameters = controller.GetConstructors().SelectMany(c => c.GetParameters())
                .Concat(controller.GetMethods(BindingFlags.Public | BindingFlags.Instance | BindingFlags.DeclaredOnly).SelectMany(m => m.GetParameters()));
            Assert.DoesNotContain(parameters, p => p.ParameterType == typeof(IInventoryReservationPort));
            Assert.DoesNotContain(controller.GetFields(BindingFlags.NonPublic | BindingFlags.Instance), f => f.FieldType == typeof(IInventoryReservationPort));
        }

        var api = SourceScan.Production("Diten.InventoryService.Api");
        Assert.NotEmpty(api);
        Assert.All(api, f => Assert.DoesNotContain(nameof(IInventoryReservationPort), f.Body, StringComparison.Ordinal));
    }
}
