using Xunit;

namespace Diten.SupplyChainService.Tests.Common;

/// <summary>
/// One host, shared, never in parallel — the pattern CompositionRootGuardCollection already uses in this suite.
/// Measured 2026-10-11: two classes each taking their own IClassFixture built two real hosts, which share one
/// database and register BSON serializers process-globally, and the pair went red in ways neither showed alone.
/// </summary>
[CollectionDefinition(Name, DisableParallelization = true)]
public sealed class SandopCapacityHttpCollection : ICollectionFixture<SandopCapacityHttpHost>
{
    public const string Name = "Sandop/Capacity bounded-slice HTTP host";
}
