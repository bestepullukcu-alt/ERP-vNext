using Diten.Platform.Infrastructure.Settings;

namespace Diten.Platform.Infrastructure.Persistence.Configurations;

/// <summary>
/// WP-DM-1b — the pure "should the Document Master Register auto-seed run?" decision, split out so it can be unit-tested
/// without MongoDB (mirrors <see cref="BootstrapSeedPolicy"/>). This is the leakage guard: the seed runs ONLY in
/// Development, when explicitly Enabled, with a real target tenant GUID configured, and a CSV file that exists. Any gate
/// unmet ⇒ false ⇒ skip. There is no hardcoded tenant fallback — an absent/blank tenant is a skip, never a guess.
/// </summary>
public static class DocumentRegisterSeedGate
{
    public static bool ShouldRun(DocumentRegisterSeedOptions options, bool isDevelopment, Func<string, bool> fileExists)
    {
        ArgumentNullException.ThrowIfNull(options);
        ArgumentNullException.ThrowIfNull(fileExists);

        if (!isDevelopment || !options.Enabled || !options.TryGetTenantId(out _))
        {
            return false;
        }

        return !string.IsNullOrWhiteSpace(options.CsvPath) && fileExists(options.CsvPath);
    }
}
