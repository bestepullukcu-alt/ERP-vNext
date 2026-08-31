using System.Diagnostics;
using System.Net;
using System.Net.Sockets;
using Xunit;

namespace Diten.Platform.Application.Tests.Persistence;

[Collection(DisposableMongoReplicaSetCollection.Name)]
public sealed class DisposableMongoPortPolicyTests
{
    [Theory]
    [InlineData(27017)]
    [InlineData(27018)]
    public async Task ProtectedPort_IsRejectedBeforeProcessOrWorkspaceCreation(int protectedPort)
    {
        var allocator = new SequenceAllocator(Enumerable.Repeat(protectedPort, 50).ToArray());
        var process = new ProcessSpy();
        var workspace = new WorkspaceSpy();

        await Assert.ThrowsAsync<InvalidOperationException>(() => DisposableMongoReplicaSet.StartAsync(allocator, process, workspace));

        Assert.Equal(0, process.InvocationCount);
        Assert.Equal(0, workspace.InvocationCount);
    }

    [Theory]
    [InlineData(27017, false)]
    [InlineData(27018, false)]
    [InlineData(27021, false)]
    [InlineData(27022, true)]
    [InlineData(28000, true)]
    public void PortPolicyMatrix_IsExplicit(int port, bool expected) =>
        Assert.Equal(expected, ReplicaSetPortPolicy.IsAllowed(port));

    [Fact]
    public void InUseCandidate_IsRejectedAndSafeCandidateIsSelected()
    {
        var allocator = new SequenceAllocator([28000, 28001], inUse: [28000]);
        Assert.Equal(28001, ReplicaSetPortPolicy.Select(allocator));
    }

    [Fact]
    public async Task ConcurrentSelections_ReturnDistinctSafePorts()
    {
        var allocator = new SequenceAllocator([28002, 28003]);
        var selected = await Task.WhenAll(
            Task.Run(() => ReplicaSetPortPolicy.Select(allocator)),
            Task.Run(() => ReplicaSetPortPolicy.Select(allocator)));

        Assert.Equal(2, selected.Distinct().Count());
        Assert.All(selected, port => Assert.True(port >= 27022));
    }

    [Fact]
    public void ExhaustionHasNoProtectedPortFallback()
    {
        var allocator = new SequenceAllocator(Enumerable.Repeat(27017, 25).Concat(Enumerable.Repeat(27018, 25)).ToArray());
        Assert.Throws<InvalidOperationException>(() => ReplicaSetPortPolicy.Select(allocator));
    }

    [Fact]
    public async Task RealReplicaSet_UsesDynamicSafePort_AndCleansProcessPortAndWorkspace()
    {
        string root;
        int selectedPort;
        await using (var replicaSet = await DisposableMongoReplicaSet.StartAsync())
        {
            selectedPort = replicaSet.Port;
            root = replicaSet.RootDirectory;
            Assert.True(selectedPort >= 27022, $"Selected disposable replica-set port: {selectedPort}");
            Assert.NotEqual(27017, selectedPort);
            Assert.NotEqual(27018, selectedPort);
            Assert.True(Directory.Exists(root));
            Assert.True(replicaSet.ProcessId > 0);
        }

        Assert.False(Directory.Exists(root));
        using var listener = new TcpListener(IPAddress.Loopback, selectedPort);
        listener.Start();
        listener.Stop();
    }

    private sealed class SequenceAllocator(int[] candidates, int[]? inUse = null) : IReplicaSetPortAllocator
    {
        private int _index = -1;
        private readonly HashSet<int> _inUse = new(inUse ?? []);
        public int NextCandidate()
        {
            var index = Interlocked.Increment(ref _index);
            return candidates[Math.Min(index, candidates.Length - 1)];
        }
        public bool IsInUse(int port) => _inUse.Contains(port);
    }

    private sealed class ProcessSpy : IMongodProcessStarter
    {
        public int InvocationCount { get; private set; }
        public Process? Start(ProcessStartInfo startInfo) { InvocationCount++; return null; }
    }

    private sealed class WorkspaceSpy : IReplicaSetWorkspaceFactory
    {
        public int InvocationCount { get; private set; }
        public string CreateRoot() { InvocationCount++; return Path.Combine(Path.GetTempPath(), "must-not-exist"); }
    }
}
