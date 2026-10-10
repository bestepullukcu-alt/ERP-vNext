using System.Text.Json;
using Diten.SupplyChainService.Api.Features.CapacityPlans;
using Diten.SupplyChainService.Application.Features.CapacityPlans;
using Xunit;
namespace Diten.SupplyChainService.Tests.CapacityPlans;
public sealed class CapacityContractTests
{
    [Fact]
    public void Scenario_name_conflict_uses_the_exact_successor_message()
    {
        using var json=JsonDocument.Parse(JsonSerializer.Serialize(
            CapacityContractError.Create("CAPACITY_SCENARIO_NAME_CONFLICT",409,Guid.Parse("19200000-0000-4000-8000-000000000192"))));
        Assert.Equal("Capacity scenario name already exists in this plan",
            json.RootElement.GetProperty("error").GetProperty("message").GetString());
    }
    [Fact]
    public void Request_does_not_accept_client_scope_or_unknown_fields()
    {
        const string request="""{"name":"FY2027","horizonStart":"2027-01-01","horizonEnd":"2027-12-31","demandPlanId":"dp-2027","demandPlanVersion":"3","sourceCapturedAt":"2026-09-15T09:45:00Z","sourceChecksum":"sha256:ee56d4f9a3c8","tenantId":"19200000-0000-4000-8000-000000000001"}""";
        Assert.Throws<JsonException>(()=>JsonSerializer.Deserialize<CreateCapacityPlanRequest>(request,new JsonSerializerOptions(JsonSerializerDefaults.Web)));
    }
    [Fact]
    public void Plan_projection_has_published_wire_keys_and_provenance()
    {
        var entity=new Diten.SupplyChainService.Domain.Features.CapacityPlans.CapacityPlan {
            Id=Guid.NewGuid(),Name="FY2027",HorizonStart=new DateOnly(2027,1,1),HorizonEnd=new DateOnly(2027,12,31),
            DemandPlanId="dp-2027",DemandPlanVersion="3",SourceCapturedAt=DateTimeOffset.Parse("2026-09-15T09:45:00Z"),
            SourceChecksum="sha256:ee56d4f9a3c8",CreatedAt=DateTimeOffset.UtcNow };
        using var json=JsonDocument.Parse(JsonSerializer.Serialize(CapacityProjection.Plan(entity),new JsonSerializerOptions(JsonSerializerDefaults.Web)));
        Assert.Equal(entity.Id.ToString(),json.RootElement.GetProperty("capacityPlanId").GetString());
        Assert.Equal("DEMAND",json.RootElement.GetProperty("provenance").GetProperty("sourceContract").GetString());
        Assert.Equal("v1",json.RootElement.GetProperty("provenance").GetProperty("sourceContractVersion").GetString());
        Assert.Equal("Draft",json.RootElement.GetProperty("status").GetString());
        Assert.Equal("v1",json.RootElement.GetProperty("contractVersion").GetString());
    }
}
