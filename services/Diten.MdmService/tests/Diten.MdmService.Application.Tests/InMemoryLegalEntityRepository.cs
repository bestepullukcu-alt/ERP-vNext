using Diten.MdmService.Domain.Entities;
using Diten.MdmService.Domain.Repositories;

namespace Diten.MdmService.Application.Tests;

internal sealed class InMemoryLegalEntityRepository : ILegalEntityRepository
{
    private readonly Guid _tenantId;
    private readonly List<LegalEntity> _entities;

    public InMemoryLegalEntityRepository(Guid tenantId, IEnumerable<LegalEntity> entities)
    {
        _tenantId = tenantId;
        _entities = entities.ToList();
    }

    public IReadOnlyList<LegalEntity> Entities => _entities;

    public Task<LegalEntity?> GetByIdAsync(Guid id, CancellationToken cancellationToken = default)
    {
        var entity = _entities.FirstOrDefault(x => x.Id == id && x.TenantId == _tenantId && !x.IsDeleted);
        return Task.FromResult(entity);
    }

    public Task<LegalEntity> CreateAsync(LegalEntity entity, CancellationToken cancellationToken = default)
    {
        entity.TenantId = _tenantId;
        entity.CreatedAt = DateTimeOffset.UtcNow;
        entity.UpdatedAt = DateTimeOffset.UtcNow;
        entity.IsDeleted = false;
        _entities.Add(entity);
        return Task.FromResult(entity);
    }

    public Task<bool> UpdateAsync(LegalEntity entity, CancellationToken cancellationToken = default)
    {
        var current = _entities.FirstOrDefault(x => x.Id == entity.Id && x.TenantId == _tenantId && !x.IsDeleted);
        if (current is null)
        {
            return Task.FromResult(false);
        }

        current.Code = entity.Code;
        current.LegalName = entity.LegalName;
        current.DisplayName = entity.DisplayName;
        current.LegalFormCode = entity.LegalFormCode;
        current.OrganizationRoleCode = entity.OrganizationRoleCode;
        current.RegistrationNumber = entity.RegistrationNumber;
        current.TaxId = entity.TaxId;
        current.CountryCode = entity.CountryCode;
        current.StatutoryStatus = entity.StatutoryStatus;
        current.ParentLegalEntityId = entity.ParentLegalEntityId;
        current.OwnershipPercent = entity.OwnershipPercent;
        current.ControlTypeCode = entity.ControlTypeCode;
        current.FiscalYearVariant = entity.FiscalYearVariant;
        current.AccountingStandardCode = entity.AccountingStandardCode;
        current.TaxRegimeCode = entity.TaxRegimeCode;
        current.BaseCurrencyCode = entity.BaseCurrencyCode;
        current.RegisteredAddressJson = entity.RegisteredAddressJson;
        current.CorrespondenceAddressJson = entity.CorrespondenceAddressJson;
        current.OfficialEmail = entity.OfficialEmail;
        current.OfficialPhone = entity.OfficialPhone;
        current.Website = entity.Website;
        current.OperationalStatus = entity.OperationalStatus;
        current.ApprovalStatus = entity.ApprovalStatus;
        current.ReviewDueUtc = entity.ReviewDueUtc;
        current.SourceSystem = entity.SourceSystem;
        current.LegacyCode = entity.LegacyCode;
        current.EvidenceStatus = entity.EvidenceStatus;
        current.CompletenessScore = entity.CompletenessScore;
        current.UpdatedAt = DateTimeOffset.UtcNow;
        current.Version++;
        return Task.FromResult(true);
    }

    public Task<bool> DeleteAsync(Guid id, CancellationToken cancellationToken = default)
    {
        var entity = _entities.FirstOrDefault(x => x.Id == id && x.TenantId == _tenantId && !x.IsDeleted);
        if (entity is null)
        {
            return Task.FromResult(false);
        }

        entity.IsDeleted = true;
        entity.DeletedAt = DateTimeOffset.UtcNow;
        return Task.FromResult(true);
    }

    public Task<IReadOnlyList<LegalEntity>> GetAllAsync(CancellationToken cancellationToken = default)
    {
        IReadOnlyList<LegalEntity> items = _entities
            .Where(x => x.TenantId == _tenantId && !x.IsDeleted)
            .OrderBy(x => x.LegalName)
            .ToList();
        return Task.FromResult(items);
    }

    public Task<bool> ExistsByCodeAsync(string code, Guid? excludeId = null, CancellationToken cancellationToken = default)
    {
        var exists = _entities.Any(x =>
            x.TenantId == _tenantId
            && !x.IsDeleted
            && string.Equals(x.Code, code, StringComparison.OrdinalIgnoreCase)
            && (!excludeId.HasValue || x.Id != excludeId.Value));

        return Task.FromResult(exists);
    }
}
