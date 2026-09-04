using Diten.ManagementGovernanceService.Application.Features.Dws;
using Diten.ManagementGovernanceService.Application.Modules.Dws;
using Diten.ManagementGovernanceService.Domain.Modules.Dws;
using Diten.ManagementGovernanceService.Infrastructure.Modules.Dws;
using Xunit;

namespace Diten.ManagementGovernanceService.IntegrationTests.Modules.Dws;

[Collection(DwsMongoCollection.Name)]
public sealed class DwsFunctionalProviderMongoTests(DisposableDwsMongo mongo)
{
    [Fact]
    public async Task MOD0117_and_FU16_failure_matrices_leave_zero_Mongo_residue()
    {
        await using var scope = await DwsFunctionalMongoScope.CreateAsync(mongo);
        var tenant = Guid.NewGuid();
        var context = scope.CommandActor(tenant, "provider");
        var reference = DwsFunctionalMongoScope.Reference();

        foreach (var disposition in Enum.GetValues<DwsLocalContextDisposition>().Where(value => value != DwsLocalContextDisposition.Accepted))
        {
            var fixture = new DwsLocalMod0117Fixture();
            fixture.Configure(new(context, reference, 1, disposition));
            await Assert.ThrowsAnyAsync<Exception>(() =>
                new LocalTestMod0117FunctionalContextValidator(fixture).ValidateAsync(context, reference, default));
            Assert.Equal(0, await scope.CountTenantAsync(tenant));
        }

        foreach (var disposition in Enum.GetValues<DwsLocalAuthorizationDisposition>().Where(value => value != DwsLocalAuthorizationDisposition.Accepted))
        {
            var fixture = new DwsLocalFu16Fixture();
            fixture.Configure(new(
                DwsFunctionalAuthorizationBinding.ModuleCode,
                DwsFunctionalAuthorizationBinding.ModuleEntitlementCode,
                context,
                "CreateStructureCommand",
                DwsAuthorizationManifest.RequireExact("CreateStructureCommand"),
                true, 1, 1, 1, 1, disposition));
            await Assert.ThrowsAsync<DwsValidationException>(() =>
                new LocalTestFu16FunctionalAuthorization(fixture).AuthorizeAsync(
                    context,
                    DwsFunctionalAuthorizationBinding.ModuleCode,
                    DwsFunctionalAuthorizationBinding.ModuleEntitlementCode,
                    "CreateStructureCommand",
                    DwsAuthorizationManifest.RequireExact("CreateStructureCommand"),
                    default));
            Assert.Equal(0, await scope.CountTenantAsync(tenant));
        }
    }
}
