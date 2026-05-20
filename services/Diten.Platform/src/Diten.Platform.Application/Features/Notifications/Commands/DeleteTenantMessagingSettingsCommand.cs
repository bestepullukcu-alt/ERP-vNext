using Diten.Platform.Application.Common;
using Diten.Platform.Application.Contracts.Audit;
using Diten.Platform.Domain.Enums;
using MediatR;

namespace Diten.Platform.Application.Features.Notifications.Commands;

public sealed record DeleteTenantMessagingSettingsCommand(Guid TenantId)
    : IRequest<Response<NoContent>>, IAuditableCommand, IAuditMetadataProvider
{
    public AuditRequestMetadata GetAuditMetadata() =>
        new(
            AuditCategory.PlatformConfiguration,
            AuditOperation.Delete,
            "TenantMessagingSettings",
            SourceModule: "MOD-0027",
            TargetTenantId: TenantId,
            Metadata: new Dictionary<string, object?>
            {
                ["EventName"] = "notifications.tenant_messaging_settings.deleted"
            });
}
