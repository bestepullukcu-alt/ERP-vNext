using Diten.Platform.Application.Common;
using Diten.Platform.Application.Contracts;
using Diten.Platform.Application.Security;
using Diten.Platform.Domain.Entities;
using Diten.Platform.Domain.Enums;
using Diten.Platform.Domain.Repositories;

namespace Diten.Platform.Application.Features.PlatformAdministrators.Handlers.CommandHandlers;

internal static class PlatformAdministratorMutationSupport
{
    /// <summary>The address of the system seed administrator (PlatformAdministratorSeed), the one Suspend and Delete
    /// always recognised it by.</summary>
    internal const string SystemSeedEmail = "admin@diten.com";

    /// <summary>BL-521 FIX3 item 2(c) — the system seed administrator, recognised the way Suspend and Delete do.</summary>
    public static bool IsSystemSeed(PlatformAdministrator administrator) =>
        string.Equals(administrator.Email?.Trim(), SystemSeedEmail, StringComparison.OrdinalIgnoreCase);

    /// <summary>A live, active PLATFORM administrator holding SuperAdmin — one the last-super-admin rule counts; taking it
    /// out of the set is written under the guard (FIX3 item 3).</summary>
    public static bool IsActivePlatformSuperAdmin(PlatformAdministrator administrator) =>
        !administrator.IsDeleted
        && administrator.Status == AdministratorStatus.Active
        && administrator.ActorType == ActorType.PlatformAdmin
        && administrator.Roles.Contains(AdministratorRole.SuperAdmin);

    /// <summary>BL-521 FIX3 item 3 — the answer to a write that did not happen.</summary>
    public static Response<NoContent> RefusedWrite(SuperAdminGuardedWriteResult outcome, string versionConflictMessage) => outcome switch
    {
        SuperAdminGuardedWriteResult.LastActiveSuperAdmin =>
            Response<NoContent>.Fail(AdminSafetyMessages.LastActiveSuperAdmin, 409, AdminSafetyReasonCodes.LastActiveSuperAdmin),
        SuperAdminGuardedWriteResult.ConcurrentChange =>
            Response<NoContent>.Fail(AdminSafetyMessages.ChangedByAnotherUser, 409, AdminSafetyReasonCodes.ConcurrentSuperAdminChange),
        SuperAdminGuardedWriteResult.Unavailable =>
            Response<NoContent>.Fail(AdminSafetyMessages.SafetyCheckUnavailable, 503, AdminSafetyReasonCodes.SafetyCheckUnavailable),
        _ => Response<NoContent>.Fail(versionConflictMessage, 409)
    };

    public static void ApplyScope(PlatformAdministrator administrator, ActorType actorType, Guid? partnerId, IReadOnlyList<Guid>? allowedTenantIds)
    {
        administrator.ActorType = actorType;
        administrator.PartnerId = actorType == ActorType.PartnerAdmin ? partnerId : null;
        administrator.AllowedTenantIds = actorType == ActorType.PartnerAdmin
            ? PlatformAdministratorParsing.NormalizeTenantScope(allowedTenantIds).ToList()
            : [];
    }

    public static void MarkUpdated(PlatformAdministrator administrator, ICurrentUserContext currentUser)
    {
        administrator.UpdatedBy = currentUser.ActorName;
    }

    public static string NewInviteToken() =>
        Convert.ToBase64String(Guid.NewGuid().ToByteArray())
            .TrimEnd('=')
            .Replace('+', '-')
            .Replace('/', '_');
}
