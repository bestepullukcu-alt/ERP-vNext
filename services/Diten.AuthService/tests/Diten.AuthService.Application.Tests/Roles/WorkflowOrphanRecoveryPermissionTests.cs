using Diten.AuthService.Domain.Authorization;
using Diten.AuthService.Domain.Entities;
using Xunit;

namespace Diten.AuthService.Application.Tests.Roles;

// BL-540 (1) (WP-WF-ENGINE-RECOVERY-01): the approval engine's orphan recovery closes live approvals in bulk — a system
// action an operator runs through a service client, never a person's role.
public sealed class WorkflowOrphanRecoveryPermissionTests
{
    [Theory]
    [InlineData("workflow.orphans", "recover", false)]
    [InlineData("workflow.tasks", "cancel", true)]
    public void The_orphan_recovery_key_is_not_grantable_to_a_human_role(string resource, string action, bool grantable)
    {
        var permission = new Permission("platform", resource, action, "", null);

        Assert.Equal($"platform.{resource}.{action}", permission.Key); // non-vacuity: the key the Platform endpoint asks for
        Assert.Equal(grantable, DefaultRolePermissionTemplate.CanBeGrantedToHumanRole(permission));
    }
}
