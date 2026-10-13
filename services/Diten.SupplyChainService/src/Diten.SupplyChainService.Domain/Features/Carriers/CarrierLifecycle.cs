namespace Diten.SupplyChainService.Domain.Features.Carriers;
public static class CarrierLifecycle
{
    public static bool Allows(CarrierStatus from, CarrierStatus to) => from switch
    {
        CarrierStatus.Active => to is CarrierStatus.Suspended or CarrierStatus.Retired,
        CarrierStatus.Suspended => to is CarrierStatus.Active or CarrierStatus.Retired,
        _ => false
    };
}
