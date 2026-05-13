using Diten.Platform.Application.Contracts;
using Diten.Platform.Domain.Entities;
using Diten.Platform.Domain.Enums;

namespace Diten.Platform.Application.Features.PlatformAdministrators.Handlers.CommandHandlers;

internal static class PlatformAdministratorMutationSupport
{
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
