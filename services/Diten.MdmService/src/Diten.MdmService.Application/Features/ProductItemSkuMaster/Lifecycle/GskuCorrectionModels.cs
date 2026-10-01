namespace Diten.MdmService.Application.Features.ProductItemSkuMaster.Lifecycle;

public static class GskuCorrectionPermissions
{
    public const string Request = "mdm.gskus.request-correction";
}

public sealed record GskuCorrectionExecutionConfiguration(TimeSpan LeaseDuration, TimeSpan RetryDelay);
