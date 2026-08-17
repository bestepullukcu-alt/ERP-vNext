using Diten.Platform.Application.Common;
using Diten.Platform.Application.Contracts.Audit;
using Diten.Platform.Domain.Enums;
using MediatR;

namespace Diten.Platform.Application.Features.Notifications.Commands;

public sealed record UpsertTenantMessagingSettingsCommand(Guid TenantId, TenantMessagingSettingsUpsertRequest Request)
    : IRequest<Response<TenantMessagingSettingsDto>>, IAuditableCommand, IAuditMetadataProvider
{
    public AuditRequestMetadata GetAuditMetadata() =>
        new(
            AuditCategory.PlatformConfiguration,
            AuditOperation.Update,
            "TenantMessagingSettings",
            SourceModule: "MOD-0027",
            TargetTenantId: TenantId,
            Metadata: new Dictionary<string, object?>
            {
                ["EventName"] = "notifications.tenant_messaging_settings.upserted",
                ["ProviderCode"] = Request.ProviderCode,
                ["IsEnabled"] = Request.IsEnabled
            });
}
