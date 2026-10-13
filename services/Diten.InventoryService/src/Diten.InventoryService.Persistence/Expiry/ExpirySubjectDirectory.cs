using Diten.InventoryService.Application.Features.Expiry.Ports;
using Diten.InventoryService.Domain.Expiry.Entities;
using MongoDB.Driver;

namespace Diten.InventoryService.Persistence.Expiry;

/// <summary>F3 — <see cref="IExpirySubjectDirectory"/>: tenant-filtered, company-free reads of a case / an override (company + owner only).</summary>
public sealed class ExpirySubjectDirectory : IExpirySubjectDirectory
{
    private readonly IMongoCollection<FefoExceptionCase> _cases;
    private readonly IMongoCollection<LotDateOverride> _overrides;

    public ExpirySubjectDirectory(IMongoDatabase database)
    {
        _cases = database.GetCollection<FefoExceptionCase>(ExpiryCollections.Exceptions);
        _overrides = database.GetCollection<LotDateOverride>(ExpiryCollections.DateOverrides);
    }

    public async Task<(Guid LegalEntityId, Guid? Owner)?> SubjectAsync(Guid tenantId, string subjectType, Guid subjectId, CancellationToken cancellationToken = default)
    {
        if (subjectType == ExpirySignableActions.ExceptionSubjectType)
        {
            var c = await _cases.Find(Builders<FefoExceptionCase>.Filter.Eq(x => x.TenantId, tenantId) & Builders<FefoExceptionCase>.Filter.Eq(x => x.IsDeleted, false)
                                      & Builders<FefoExceptionCase>.Filter.Eq(x => x.Id, subjectId)).FirstOrDefaultAsync(cancellationToken);
            return c?.LegalEntityId is { } company ? (company, null) : null;
        }

        if (subjectType == ExpirySignableActions.DateOverrideSubjectType)
        {
            var o = await _overrides.Find(Builders<LotDateOverride>.Filter.Eq(x => x.TenantId, tenantId) & Builders<LotDateOverride>.Filter.Eq(x => x.IsDeleted, false)
                                          & Builders<LotDateOverride>.Filter.Eq(x => x.Id, subjectId)).FirstOrDefaultAsync(cancellationToken);
            return o?.LegalEntityId is { } company ? (company, o.CreatedBy) : null;
        }

        return null;
    }
}
