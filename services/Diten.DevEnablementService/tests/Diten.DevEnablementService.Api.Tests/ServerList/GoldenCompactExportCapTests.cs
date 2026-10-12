using Diten.DevEnablementService.Application.Common;
using Diten.DevEnablementService.Domain.Entities;
using Diten.DevEnablementService.Domain.Repositories;
using Diten.DevEnablementService.Persistence.Repositories;
using Xunit;

namespace Diten.DevEnablementService.Api.Tests.ServerList;

/// <summary>
/// BL-452 — the export cap is a "no" BEFORE the read (CT acceptance, 2026-09-25). When the count already exceeds
/// <see cref="GoldenReferenceCompactListCriteria.RefuseAbove"/> the repository hands back the count and no row: the
/// controller refuses the file (413) without 50 000 documents having been read for it. Same shape as AuthService's reader.
/// </summary>
[Collection(ServerListCollection.Name)]
public sealed class GoldenCompactExportCapTests(DevEnablementTestHost host)
{
    [Fact]
    public async Task When_the_count_exceeds_the_cap_the_repository_hands_back_the_count_and_no_row()
    {
        var tenantId = Guid.NewGuid();
        await host.Collection.InsertManyAsync(Enumerable.Range(1, 12).Select(i => new GoldenReferenceCompact
        {
            TenantId = tenantId, Code = $"CAP-{i:00}", Name = $"Cap row {i}", ReferenceType = "Standard", Category = "Alpha",
            Owner = "Ops", Version = "1", Priority = 10, IsActive = true
        }));
        var repository = new GoldenReferenceCompactRepository(host.Collection.Database, new FixedTenant(tenantId));

        var refused = await repository.QueryAsync(new GoldenReferenceCompactListCriteria(Length: 11, RefuseAbove: 10));
        Assert.Equal(12, refused.FilteredTotal);
        Assert.Empty(refused.Items);

        var allowed = await repository.QueryAsync(new GoldenReferenceCompactListCriteria(Length: 13, RefuseAbove: 12));
        Assert.Equal(12, allowed.FilteredTotal);
        Assert.Equal(12, allowed.Items.Count);
    }

    private sealed class FixedTenant(Guid tenantId) : ITenantContext
    {
        public Guid TenantId => tenantId;
        public bool IsResolved => true;
        public bool IsPlatformContext => false;
        public Guid? TargetTenantId => null;
        public void SetTenant(Guid id) => throw new NotSupportedException();
        public void SetPlatformContext(Guid targetTenantId) => throw new NotSupportedException();
    }
}
