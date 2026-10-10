using Diten.SupplyChainService.Domain.Features.Claims;
using Xunit;
namespace Diten.SupplyChainService.Tests.Claims;
public sealed class ClaimLifecycleTests
{
    [Fact]
    public void CanTransition_All49Pairs_ExactlySevenFrozenArrows()
    {
        var allowed=new HashSet<(ClaimStatus,ClaimStatus)> {
            (ClaimStatus.Open,ClaimStatus.Investigating),(ClaimStatus.Open,ClaimStatus.Withdrawn),
            (ClaimStatus.Investigating,ClaimStatus.Approved),(ClaimStatus.Investigating,ClaimStatus.Rejected),
            (ClaimStatus.Approved,ClaimStatus.Settled),(ClaimStatus.Rejected,ClaimStatus.Closed),(ClaimStatus.Settled,ClaimStatus.Closed)};
        var states=Enum.GetValues<ClaimStatus>(); Assert.Equal(7,states.Length);
        foreach(var from in states) foreach(var to in states)
            Assert.Equal(allowed.Contains((from,to)),ClaimLifecycle.CanTransition(from,to));
    }
}
