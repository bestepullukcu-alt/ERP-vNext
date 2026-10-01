namespace Diten.MdmService.Application.Features.ProductItemSkuMaster.Lifecycle;

public static class GskuRetirementRequestPermissions
{
    public const string Request = "mdm.gskus.request-retirement";
}

public sealed record GskuRetirementRequestExecutionConfiguration(TimeSpan LeaseDuration, TimeSpan RetryDelay);
