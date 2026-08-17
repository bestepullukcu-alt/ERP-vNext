using Diten.AuthService.Domain.Entities;

namespace Diten.AuthService.Application.Tests.Roles;

// S1 / AG-INFRA-COMPLETION Part 0 — grant source-tracking for selective revoke.
public sealed class RolePermissionGrantSourceTests
{
    private static readonly Guid RoleId = Guid.Parse("cccccccc-cccc-cccc-cccc-cccccccccccc");
    private static readonly Guid PermissionId = Guid.Parse("eeeeeeee-eeee-eeee-eeee-eeeeeeeeeeee");
    private static readonly Guid TenantId = Guid.Parse("aaaaaaaa-aaaa-aaaa-aaaa-aaaaaaaaaaaa");

    [Fact]
    public void System_is_the_zero_default_so_legacy_documents_deserialize_as_system()
    {
        // MongoDB has no class map; a document written before GrantSource existed has no field and
        // deserializes to default(GrantSource). That must be the baseline-safe value.
        Assert.Equal(0, (int)GrantSource.System);
        Assert.Equal(GrantSource.System, default);
    }

    [Fact]
    public void SystemGrant_tags_system_with_no_module_code()
    {
        var rp = RolePermission.SystemGrant(RoleId, PermissionId, TenantId, "system");

        Assert.Equal(GrantSource.System, rp.GrantSource);
        Assert.Null(rp.SourceModuleCode);
        Assert.Equal(TenantId, rp.TenantId);
        Assert.Equal("system", rp.AssignedBy);
    }

    [Fact]
    public void ModuleGrant_tags_module_and_records_the_source_module_code()
    {
        var rp = RolePermission.ModuleGrant(RoleId, PermissionId, TenantId, "system", "MDM");

        Assert.Equal(GrantSource.Module, rp.GrantSource);
        Assert.Equal("MDM", rp.SourceModuleCode);
    }

    [Fact]
    public void ManualGrant_tags_manual()
    {
        var rp = RolePermission.ManualGrant(RoleId, PermissionId, TenantId, "operator");

        Assert.Equal(GrantSource.Manual, rp.GrantSource);
        Assert.Null(rp.SourceModuleCode);
    }

    [Fact]
    public void Legacy_four_arg_constructor_is_manual_for_the_operator_api_path()
    {
        var rp = new RolePermission(RoleId, PermissionId, TenantId, "operator");

        Assert.Equal(GrantSource.Manual, rp.GrantSource);
        Assert.Null(rp.SourceModuleCode);
    }
}
