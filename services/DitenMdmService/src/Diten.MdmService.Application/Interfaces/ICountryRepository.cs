using Diten.MdmService.Domain.Entities;

namespace Diten.MdmService.Application.Interfaces;

public interface ICountryRepository
{
    Task<Country> CreateAsync(Country entity, CancellationToken cancellationToken = default);
    Task<Country?> GetByIdAsync(Guid id, CancellationToken cancellationToken = default);
    Task<IEnumerable<Country>> GetAllAsync(CancellationToken cancellationToken = default);
    Task<bool> UpdateAsync(Country entity, CancellationToken cancellationToken = default);
    Task DeleteAsync(Guid id, CancellationToken cancellationToken = default);

    Task<bool> ExistsByIso2Async(string iso2, CancellationToken cancellationToken = default);

    /// <summary>
    /// Tenant bazli seed upsert. Return: insertedCount (upsert insert count).
    /// Updated count is exposed via additional result in handler (modifiedCount).
    /// </summary>
    Task<(int insertedCount, long modifiedCount)> UpsertSeedAsync(
        IEnumerable<Country> seedCountries,
        CancellationToken cancellationToken = default);
}

