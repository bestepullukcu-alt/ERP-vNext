using Xunit;

namespace Diten.Platform.Eventing.Tests;

/// <summary>
/// WP-CI-SERVICE-TESTS-03: the live-broker tests run ONE AT A TIME. Each has its own queue, but MassTransit binds every
/// receive endpoint to the exchange of the message TYPE — so while two classes run in parallel (xUnit's default), one
/// test's <c>TenantActivatedV1</c> reaches the other's queue: measured, the failure test's error queue received the
/// lifecycle test's event (another EventId), and the lifecycle test counted an extra audit (8 for 7).
/// </summary>
[CollectionDefinition(Name, DisableParallelization = true)]
public sealed class LiveRabbitMqCollection
{
    public const string Name = "Live RabbitMQ broker";
}
