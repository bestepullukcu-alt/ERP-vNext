namespace Diten.AuthService.Domain.Entities;

/// <summary>
/// Identifies the origin of a role→permission grant so that selective revoke can target the
/// right rows without disturbing the others — e.g. the entitlement consumer removing only the
/// grants a disabled module produced, while leaving baseline and operator-assigned grants intact.
/// </summary>
public enum GrantSource
{
    /// <summary>
    /// Baseline grant written by provisioning / seed. Default value (0) so legacy documents that
    /// predate this field deserialize as <see cref="System"/> — the baseline-safe interpretation.
    /// </summary>
    System = 0,

    /// <summary>
    /// Grant derived from a tenant module entitlement; <see cref="RolePermission.SourceModuleCode"/>
    /// names the module that produced it (consumed by the entitlement→permission bridge).
    /// </summary>
    Module = 1,

    /// <summary>Grant assigned manually through the roles API by an operator.</summary>
    Manual = 2
}
