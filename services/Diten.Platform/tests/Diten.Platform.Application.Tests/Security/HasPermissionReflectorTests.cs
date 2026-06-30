using Diten.Platform.API.Security;
using Diten.Platform.Application.Features.Workflow;
using Xunit;

namespace Diten.Platform.Application.Tests.Security;

// A1 — the startup auto-registration source. Reflection must surface a non-empty, deduped key set that
// includes the landed workflow permissions (the keys that were missing from auth and caused the 403s).
public sealed class HasPermissionReflectorTests
{
    private static readonly System.Reflection.Assembly ApiAssembly = typeof(HasPermissionAttribute).Assembly;

    [Fact]
    public void Collects_non_empty_key_set()
    {
        var keys = HasPermissionReflector.CollectPermissionKeys(ApiAssembly);

        Assert.NotEmpty(keys);
        Assert.All(keys, k => Assert.False(string.IsNullOrWhiteSpace(k)));
    }

    [Fact]
    public void Includes_landed_workflow_permission_keys()
    {
        var keys = HasPermissionReflector.CollectPermissionKeys(ApiAssembly);

        Assert.Contains(WorkflowPermissions.DefinitionsView, keys);
        Assert.Contains(WorkflowPermissions.DefinitionsManage, keys);
    }

    [Fact]
    public void Result_is_deduplicated()
    {
        var keys = HasPermissionReflector.CollectPermissionKeys(ApiAssembly);

        Assert.Equal(keys.Count, keys.Distinct(System.StringComparer.OrdinalIgnoreCase).Count());
    }
}
