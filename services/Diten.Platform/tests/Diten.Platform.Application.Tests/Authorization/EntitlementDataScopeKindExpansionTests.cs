using Diten.Platform.Common.Authorization;
using Xunit;

namespace Diten.Platform.Application.Tests.Authorization;

public sealed class EntitlementDataScopeKindExpansionTests
{
    [Fact]
    public void Enum_contains_exactly_thirteen_values()
    {
        var values = Enum.GetValues<EntitlementDataScopeKind>();

        Assert.Equal(13, values.Length);
    }

    [Fact]
    public void Original_five_values_preserve_numeric_identity()
    {
        Assert.Equal(0, (int)EntitlementDataScopeKind.Company);
        Assert.Equal(1, (int)EntitlementDataScopeKind.Country);
        Assert.Equal(2, (int)EntitlementDataScopeKind.Own);
        Assert.Equal(3, (int)EntitlementDataScopeKind.Assigned);
        Assert.Equal(4, (int)EntitlementDataScopeKind.ProcessRelatedRecord);
    }

    [Fact]
    public void New_eight_values_occupy_ids_five_through_twelve()
    {
        Assert.Equal(5, (int)EntitlementDataScopeKind.OrgUnit);
        Assert.Equal(6, (int)EntitlementDataScopeKind.Department);
        Assert.Equal(7, (int)EntitlementDataScopeKind.Team);
        Assert.Equal(8, (int)EntitlementDataScopeKind.Position);
        Assert.Equal(9, (int)EntitlementDataScopeKind.LegalEntity);
        Assert.Equal(10, (int)EntitlementDataScopeKind.Region);
        Assert.Equal(11, (int)EntitlementDataScopeKind.ManagerChain);
        Assert.Equal(12, (int)EntitlementDataScopeKind.RecordOwner);
    }

    [Theory]
    [InlineData(EntitlementDataScopeKind.OrgUnit)]
    [InlineData(EntitlementDataScopeKind.Department)]
    [InlineData(EntitlementDataScopeKind.Team)]
    [InlineData(EntitlementDataScopeKind.Position)]
    [InlineData(EntitlementDataScopeKind.LegalEntity)]
    [InlineData(EntitlementDataScopeKind.Region)]
    [InlineData(EntitlementDataScopeKind.ManagerChain)]
    [InlineData(EntitlementDataScopeKind.RecordOwner)]
    public void New_values_are_defined_enum_members(EntitlementDataScopeKind value)
    {
        Assert.True(Enum.IsDefined(value));
    }
}
