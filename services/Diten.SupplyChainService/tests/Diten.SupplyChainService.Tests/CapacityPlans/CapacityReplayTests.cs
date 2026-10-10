using Diten.SupplyChainService.Application.Features.CapacityPlans;
using Xunit;
namespace Diten.SupplyChainService.Tests.CapacityPlans;
public sealed class CapacityReplayTests
{
    [Fact]
    public void Fingerprint_uses_decoded_business_body_and_exact_target()
    {
        var a=new Dictionary<string,object>{{"name","alpha"},{"demandPlanVersion","3"}};
        var b=new Dictionary<string,object>{{"demandPlanVersion","3"},{"name","alpha"}};
        var same=CapacityRequestFingerprint.Create("createCapacityPlan","create",a);
        Assert.Equal(same,CapacityRequestFingerprint.Create("createCapacityPlan","create",b));
        Assert.NotEqual(same,CapacityRequestFingerprint.Create("createCapacityPlan","other",b));
        Assert.NotEqual(same,CapacityRequestFingerprint.Create("createCapacityPlan","create",new {name="beta",demandPlanVersion="3"}));
    }
}
