using Diten.InventoryService.Application;
using Diten.InventoryService.Application.Behaviors;
using MediatR;
using Microsoft.Extensions.DependencyInjection;
using Xunit;

namespace Diten.InventoryService.Tests.Architecture;

/// <summary>AC1 — the four mandatory pipeline behaviors (pipeline-behaviors.md) are registered, in order.</summary>
public sealed class PipelineArchitectureTests
{
    [Fact]
    public void The_four_pipeline_behaviors_are_registered_in_order()
    {
        var services = new ServiceCollection().AddApplication();

        var behaviors = services
            .Where(d => d.ServiceType == typeof(IPipelineBehavior<,>))
            .Select(d => d.ImplementationType!.GetGenericTypeDefinition())
            .ToList();

        Assert.Equal(
            [
                typeof(ValidationBehavior<,>), typeof(LoggingBehavior<,>),
                typeof(ExceptionHandlingBehavior<,>), typeof(PerformanceBehavior<,>)
            ],
            behaviors);
    }
}
