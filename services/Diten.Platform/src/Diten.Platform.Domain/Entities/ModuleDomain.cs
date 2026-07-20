using Diten.Platform.Common.Persistence;
using Diten.Platform.Domain.Catalog;

namespace Diten.Platform.Domain.Entities;

/// <summary>
/// Operator-managed module domain. Replaces the static <c>ModuleCatalogDomain</c> enum as the source of the
/// Module Catalog "Domain" dropdown. The enum is retained only as the seed source (see ModuleDomainSeed).
/// </summary>
public sealed class ModuleDomain : GlobalEntity
{
    private string _code = string.Empty;

    /// <summary>
    /// Canonical domain code (UPPERCASE, no separators — see <see cref="CodeKey"/>). Assigning it keeps
    /// <see cref="CodeKey"/> in sync automatically, so no creation path can forget to set the uniqueness key.
    /// </summary>
    public string Code
    {
        get => _code;
        set
        {
            _code = value ?? string.Empty;
            CodeKey = ModuleTaxonomyCanonicalizer.NormalizeKey(_code);
        }
    }

    /// <summary>
    /// FIX-DOMAIN-DEDUP — normalized uniqueness key derived from <see cref="Code"/> (drop every non-alphanumeric
    /// char + uppercase). Persisted so the unique partial index <c>ux_platform_module_domains_code_key</c> can
    /// forbid two live rows whose Codes differ only by separators/case (e.g. "MASTER-DATA-MANAGEMENT" vs
    /// "MASTERDATAMANAGEMENT"), which historically produced duplicate domain rows. Maintained by the Code setter;
    /// the private setter exists only so MongoDB can rehydrate the stored value on read.
    /// </summary>
    public string CodeKey { get; private set; } = string.Empty;

    public string DisplayName { get; set; } = string.Empty;
    public string? Description { get; set; }
    public int SortOrder { get; set; }
    public bool IsActive { get; set; } = true;
}
