using Diten.MdmService.Domain.Entities;

namespace Diten.MdmService.Application.Interfaces;

/// <summary>
/// Repository for Country operations.
/// All queries automatically filter by TenantId and IsDeleted=false.
/// </summary>
public interface ICountryRepository
{
    Task<Country?> GetByIdAsync(Guid id, CancellationToken cancellationToken = default);
    Task<bool> ExistsAsync(Guid id, CancellationToken cancellationToken = default);
    Task<IEnumerable<Country>> GetAllAsync(CancellationToken cancellationToken = default);
    Task<Country?> GetByIso2CodeAsync(string iso2Code, CancellationToken cancellationToken = default);
    Task<Country?> GetByIso3CodeAsync(string iso3Code, CancellationToken cancellationToken = default);
    Task<Country> CreateAsync(Country entity, CancellationToken cancellationToken = default);
    Task<bool> UpdateAsync(Country entity, CancellationToken cancellationToken = default);
    Task DeleteAsync(Guid id, CancellationToken cancellationToken = default);
}