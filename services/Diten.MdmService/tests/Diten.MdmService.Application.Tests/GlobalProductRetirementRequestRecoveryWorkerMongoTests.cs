using Diten.MdmService.Api.Services.ProductItemSkuMaster;
using Microsoft.Extensions.Hosting;
using Xunit;

namespace Diten.MdmService.Application.Tests;

public sealed class GlobalProductRetirementRequestRecoveryWorkerMongoTests
{
    [Fact]
    public void Recovery_worker_is_hosted_orchestration_not_a_second_product_writer()
    {
        Assert.True(typeof(BackgroundService).IsAssignableFrom(
            typeof(GlobalProductRetirementRequestRecoveryWorker)));
        Assert.DoesNotContain(typeof(GlobalProductRetirementRequestRecoveryWorker).GetFields(
                System.Reflection.BindingFlags.Instance | System.Reflection.BindingFlags.NonPublic),
            field => field.FieldType.Name.Contains("GlobalProductRepository", StringComparison.Ordinal));
    }
}
