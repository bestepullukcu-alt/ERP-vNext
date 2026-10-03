using Diten.Platform.Common.Tenancy;
using Diten.Platform.Domain.Entities;
using Diten.Platform.Domain.Enums;
using Diten.Platform.Infrastructure.Persistence;
using Diten.Platform.Infrastructure.Persistence.Repositories;
using Xunit;

namespace Diten.Platform.Application.Tests.Persistence;

/// <summary>
/// BL-500 FIX2 — the Modules list reads every catalogue record in ONE query (<c>GetByCodesAsync</c>); the HTTP host
/// fakes the repository, so the real one is measured here: it answers what <c>GetByCodeAsync</c> would, and a deleted
/// record is not a catalogue module.
/// </summary>
[Collection(DisposableMongoReplicaSetCollection.Name)]
public sealed class ModuleCatalogGetByCodesMongoTests
{
    [Fact]
    public async Task Many_codes_are_read_at_once_and_a_deleted_record_is_not_returned()
    {
        await using var mongo = await DisposableMongoReplicaSet.StartAsync();
        var database = mongo.CreateDatabase();
        var repository = new ModuleCatalogRepository(new PlatformDbContext(mongo.Client, database), new TenantContext());
        var collection = database.GetCollection<ModuleCatalogItem>("platform_module_catalog");
        await collection.InsertManyAsync(
        [
            new ModuleCatalogItem { ModuleCode = "CRM", ModuleName = "Crm", DisplayName = "CRM", Status = ModuleCatalogStatus.Active, IsCoreModule = true },
            new ModuleCatalogItem { ModuleCode = "HR", ModuleName = "Hr", DisplayName = "HR", Status = ModuleCatalogStatus.Active, IsDeleted = true }
        ]);

        var found = await repository.GetByCodesAsync(["CRM", "HR", "NOPE"]);

        Assert.Equal(["CRM"], found.Keys.ToArray());
        Assert.True(found["crm"].IsCoreModule); // looked up as the list does, whatever the letter case
        Assert.Null(await repository.GetByCodeAsync("HR"));
    }
}
