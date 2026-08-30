using System.Diagnostics;
using System.Globalization;
using System.Net;
using System.Net.Sockets;
using MongoDB.Bson;
using MongoDB.Driver;
using Xunit;

namespace Diten.PpmService.IntegrationTests.GateI.DecisionTrace;


[CollectionDefinition(CollectionName, DisableParallelization = true)]
public sealed class GateILocalEvidenceCollection : ICollectionFixture<GateIDisposableMongoReplicaSet>
{
    public const string CollectionName = "MOD-0117-Gate-I-local-evidence";
}
