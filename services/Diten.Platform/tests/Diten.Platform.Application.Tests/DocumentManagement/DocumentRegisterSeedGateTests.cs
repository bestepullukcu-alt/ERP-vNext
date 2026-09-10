using Diten.Platform.Infrastructure.Persistence.Configurations;
using Diten.Platform.Infrastructure.Settings;
using Xunit;

namespace Diten.Platform.Application.Tests.DocumentManagement;

/// <summary>
/// WP-DM-1b — leakage guard for the tenant-agnostic auto-seed. Proves the seed runs ONLY with a full dev config
/// (Development + Enabled + a real target tenant + an existing CSV) and is SKIPPED for production, disabled config, an
/// absent/blank/invalid tenant, or a missing CSV — with no hardcoded tenant fallback. Pure (no MongoDB), like
/// BootstrapSeedPolicy.
/// </summary>
public sealed class DocumentRegisterSeedGateTests
{
    private const string ValidTenant = "11111111-2222-3333-4444-555555555555";
    private static readonly Func<string, bool> FileExists = _ => true;
    private static readonly Func<string, bool> FileMissing = _ => false;

    private static DocumentRegisterSeedOptions Options(bool enabled = true, string? tenant = ValidTenant, string? csv = "reg.csv") =>
        new() { Enabled = enabled, TenantId = tenant, CsvPath = csv };

    [Fact]
    public void Runs_only_with_full_dev_config()
    {
        Assert.True(DocumentRegisterSeedGate.ShouldRun(Options(), isDevelopment: true, FileExists));
    }

    [Fact]
    public void Skips_in_production_even_with_full_config()
    {
        // The leakage guard: a complete config must NOT seed outside Development.
        Assert.False(DocumentRegisterSeedGate.ShouldRun(Options(), isDevelopment: false, FileExists));
    }

    [Fact]
    public void Skips_when_disabled()
    {
        Assert.False(DocumentRegisterSeedGate.ShouldRun(Options(enabled: false), isDevelopment: true, FileExists));
    }

    [Theory]
    [InlineData(null)]
    [InlineData("")]
    [InlineData("   ")]
    [InlineData("not-a-guid")]
    [InlineData("00000000-0000-0000-0000-000000000000")] // Guid.Empty is not a real target
    public void Skips_when_tenant_is_absent_blank_or_invalid(string? tenant)
    {
        Assert.False(DocumentRegisterSeedGate.ShouldRun(Options(tenant: tenant), isDevelopment: true, FileExists));
    }

    [Theory]
    [InlineData(null)]
    [InlineData("")]
    public void Skips_when_csv_path_is_absent(string? csv)
    {
        Assert.False(DocumentRegisterSeedGate.ShouldRun(Options(csv: csv), isDevelopment: true, FileExists));
    }

    [Fact]
    public void Skips_when_csv_file_does_not_exist()
    {
        Assert.False(DocumentRegisterSeedGate.ShouldRun(Options(), isDevelopment: true, FileMissing));
    }
}
