using Diten.MdmService.Application.Common;
using Diten.MdmService.Domain.Entities;
using Diten.MdmService.Domain.Enums;
using Diten.MdmService.Domain.Repositories;
using MongoDB.Driver;

namespace Diten.MdmService.Persistence.Repositories;

public sealed class LegalEntityRepository : RepositoryBase<LegalEntity>, ILegalEntityRepository
{
    private const int MaximumReferenceableBatchSize = 200;

    public LegalEntityRepository(IMongoDatabase database, ITenantContext tenantContext)
        : base(database, tenantContext, "mdm_legal_entities")
    {
        EnsureIndexes();
    }

    public async Task<bool> ExistsByCodeAsync(string code, Guid? excludeId = null, CancellationToken cancellationToken = default)
    {
        var filter = Builders<LegalEntity>.Filter.And(
            TenantFilter,
            Builders<LegalEntity>.Filter.Eq(x => x.Code, code));

        if (excludeId.HasValue)
        {
            filter &= Builders<LegalEntity>.Filter.Ne(x => x.Id, excludeId.Value);
        }

        return await Collection.Find(filter).AnyAsync(cancellationToken);
    }

    public async Task<bool> UpdateEditableFieldsAsync(
        LegalEntity proposed,
        int expectedVersion,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(proposed);

        var filter = Builders<LegalEntity>.Filter.And(
            Builders<LegalEntity>.Filter.Eq(entity => entity.TenantId, TenantId),
            Builders<LegalEntity>.Filter.Eq(entity => entity.Id, proposed.Id),
            Builders<LegalEntity>.Filter.Eq(entity => entity.IsDeleted, false),
            Builders<LegalEntity>.Filter.Eq(entity => entity.Version, expectedVersion));

        var update = Builders<LegalEntity>.Update
            .Set(entity => entity.Code, proposed.Code)
            .Set(entity => entity.LegalName, proposed.LegalName)
            .Set(entity => entity.DisplayName, proposed.DisplayName)
            .Set(entity => entity.LegalFormCode, proposed.LegalFormCode)
            .Set(entity => entity.OrganizationRoleCode, proposed.OrganizationRoleCode)
            .Set(entity => entity.RegistrationNumber, proposed.RegistrationNumber)
            .Set(entity => entity.TaxId, proposed.TaxId)
            .Set(entity => entity.VatNumber, proposed.VatNumber)
            .Set(entity => entity.PlaceOfIncorporation, proposed.PlaceOfIncorporation)
            .Set(entity => entity.IncorporationDate, proposed.IncorporationDate)
            .Set(entity => entity.DissolutionDate, proposed.DissolutionDate)
            .Set(entity => entity.CountryCode, proposed.CountryCode)
            .Set(entity => entity.StatutoryStatus, proposed.StatutoryStatus)
            .Set(entity => entity.ParentLegalEntityId, proposed.ParentLegalEntityId)
            .Set(entity => entity.OwnershipPercent, proposed.OwnershipPercent)
            .Set(entity => entity.ControlTypeCode, proposed.ControlTypeCode)
            .Set(entity => entity.FiscalYearVariant, proposed.FiscalYearVariant)
            .Set(entity => entity.AccountingStandardCode, proposed.AccountingStandardCode)
            .Set(entity => entity.TaxRegimeCode, proposed.TaxRegimeCode)
            .Set(entity => entity.BaseCurrencyCode, proposed.BaseCurrencyCode)
            .Set(entity => entity.RegisteredAddressJson, proposed.RegisteredAddressJson)
            .Set(entity => entity.CorrespondenceAddressJson, proposed.CorrespondenceAddressJson)
            .Set(entity => entity.OfficialEmail, proposed.OfficialEmail)
            .Set(entity => entity.OfficialPhone, proposed.OfficialPhone)
            .Set(entity => entity.Website, proposed.Website)
            .Set(entity => entity.CompletenessScore, proposed.CompletenessScore)
            .Set(entity => entity.UpdatedAt, DateTimeOffset.UtcNow)
            .Inc(entity => entity.Version, 1);

        try
        {
            var result = await Collection.UpdateOneAsync(filter, update, cancellationToken: cancellationToken);
            return result.ModifiedCount == 1;
        }
        catch (MongoWriteException exception) when (
            exception.WriteError?.Category == ServerErrorCategory.DuplicateKey)
        {
            // The application performs a tenant-scoped visible re-read and maps this edit conflict to 409.
            return false;
        }
    }

    public async Task<IReadOnlyList<LegalEntity>> GetReferenceableAsync(CancellationToken cancellationToken = default)
    {
        var filter = Builders<LegalEntity>.Filter.And(
            TenantFilter,
            Builders<LegalEntity>.Filter.Eq(x => x.OperationalStatus, LegalEntityOperationalStatus.Active));

        return await Collection.Find(filter).SortBy(x => x.LegalName).ToListAsync(cancellationToken);
    }

    public async Task<IReadOnlyList<LegalEntity>> GetReferenceableByIdsAsync(
        IReadOnlyCollection<Guid> ids,
        CancellationToken cancellationToken = default)
    {
        cancellationToken.ThrowIfCancellationRequested();
        ArgumentNullException.ThrowIfNull(ids);
        if (ids.Count > MaximumReferenceableBatchSize)
        {
            throw new ArgumentOutOfRangeException(
                nameof(ids),
                "LEGAL_ENTITY_SCOPE_ID_LIMIT_EXCEEDED");
        }
        if (ids.Any(id => id == Guid.Empty) || ids.Distinct().Count() != ids.Count)
        {
            throw new ArgumentException(
                "LEGAL_ENTITY_SCOPE_IDS_MUST_BE_NONEMPTY_UNIQUE",
                nameof(ids));
        }
        if (ids.Count == 0)
        {
            return [];
        }

        var filter = Builders<LegalEntity>.Filter.And(
            TenantFilter,
            Builders<LegalEntity>.Filter.Eq(
                entity => entity.OperationalStatus,
                LegalEntityOperationalStatus.Active),
            Builders<LegalEntity>.Filter.In(entity => entity.Id, ids));

        var entities = await Collection.Find(filter).ToListAsync(cancellationToken);
        return entities
            .OrderBy(entity => entity.Id.ToString("D"), StringComparer.Ordinal)
            .ToArray();
    }

    public async Task<IReadOnlyList<LegalEntity>> GetAllAsync(CancellationToken cancellationToken = default)
        => await Collection.Find(TenantFilter)
            .SortBy(x => x.LegalName)
            .ToListAsync(cancellationToken);

    private void EnsureIndexes()
    {
        var keys = Builders<LegalEntity>.IndexKeys
            .Ascending(x => x.TenantId)
            .Ascending(x => x.Code);

        var options = new CreateIndexOptions
        {
            Unique = true,
            Name = "ux_mdm_legal_entities_tenant_code"
        };

        Collection.Indexes.CreateOne(new CreateIndexModel<LegalEntity>(keys, options));
    }
}
