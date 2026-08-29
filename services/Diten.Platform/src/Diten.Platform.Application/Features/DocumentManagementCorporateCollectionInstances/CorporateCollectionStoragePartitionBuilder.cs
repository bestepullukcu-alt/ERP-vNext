using Diten.Platform.Common.Tenancy;

namespace Diten.Platform.Application.Features.DocumentManagementCorporateCollectionInstances;

public sealed class CorporateCollectionStoragePartitionBuilder
{
    private readonly ITenantContext _tenantContext;

    public CorporateCollectionStoragePartitionBuilder(ITenantContext tenantContext) => _tenantContext = tenantContext;

    public string ForCompany(Guid companyId, Guid folderId)
    {
        Ensure(companyId, nameof(companyId));
        Ensure(folderId, nameof(folderId));
        return $"tenant/{_tenantContext.TenantId:D}/company/{companyId:D}/folder/{folderId:D}";
    }

    public string ForCorporate(Guid corporateOwnerId, Guid folderId)
    {
        Ensure(corporateOwnerId, nameof(corporateOwnerId));
        Ensure(folderId, nameof(folderId));
        return $"tenant/{_tenantContext.TenantId:D}/corporate/{corporateOwnerId:D}/folder/{folderId:D}";
    }

    private static void Ensure(Guid value, string name)
    {
        if (value == Guid.Empty) throw new ArgumentException("Scope and folder identifiers must be non-empty.", name);
    }
}
