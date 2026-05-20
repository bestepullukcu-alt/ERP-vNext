using Diten.Platform.Domain.Entities.Notifications;
using Diten.Platform.Domain.Enums;
using MongoDB.Driver;

namespace Diten.Platform.Infrastructure.Persistence.Configurations;

public static class NotificationTemplateSeed
{
    public static async Task EnsureSeededAsync(IMongoDatabase database, CancellationToken ct = default)
    {
        var collection = database.GetCollection<NotificationTemplate>("notification_templates");

        foreach (var template in CreatePlatformDefaults())
        {
            var exists = await collection.Find(x =>
                    x.IsDeleted == false &&
                    x.TenantId == null &&
                    x.IsPlatformDefault &&
                    x.Status == NotificationTemplateStatus.Active &&
                    x.Channel == template.Channel &&
                    x.Locale == template.Locale &&
                    x.TemplateKey == template.TemplateKey)
                .AnyAsync(ct);

            if (exists)
            {
                continue;
            }

            await collection.InsertOneAsync(template, cancellationToken: ct);
        }
    }

    private static IReadOnlyList<NotificationTemplate> CreatePlatformDefaults()
    {
        return
        [
            TenantInvite("en"),
            TenantInvite("tr"),
            TenantSuspended("en"),
            TenantSuspended("tr"),
            TenantReactivated("en"),
            TenantReactivated("tr")
        ];
    }

    private static NotificationTemplate TenantInvite(string locale)
    {
        var isTurkish = locale == "tr";
        return Create(
            "tenant.invite.email",
            locale,
            isTurkish ? "Diten tenant davetiniz" : "Your Diten tenant invitation",
            isTurkish
                ? "<p>Merhaba,</p><p>{{TenantDisplayName}} tenant ortamina davet edildiniz.</p><p>Tenant Id: {{TenantId}}</p>"
                : "<p>Hello,</p><p>You have been invited to the {{TenantDisplayName}} tenant.</p><p>Tenant Id: {{TenantId}}</p>",
            isTurkish
                ? "Merhaba, {{TenantDisplayName}} tenant ortamina davet edildiniz. Tenant Id: {{TenantId}}"
                : "Hello, you have been invited to the {{TenantDisplayName}} tenant. Tenant Id: {{TenantId}}",
            ["TenantId", "TenantDisplayName"]);
    }

    private static NotificationTemplate TenantSuspended(string locale)
    {
        var isTurkish = locale == "tr";
        return Create(
            "tenant.suspended.email",
            locale,
            isTurkish ? "Diten tenant erisimi askiya alindi" : "Diten tenant access suspended",
            isTurkish
                ? "<p>Merhaba,</p><p>Tenant erisiminiz askiya alindi.</p><p>Neden: {{Reason}}</p><p>Tarih: {{SuspendedAtUtc}}</p>"
                : "<p>Hello,</p><p>Your tenant access has been suspended.</p><p>Reason: {{Reason}}</p><p>Date: {{SuspendedAtUtc}}</p>",
            isTurkish
                ? "Tenant erisiminiz askiya alindi. Neden: {{Reason}} Tarih: {{SuspendedAtUtc}}"
                : "Your tenant access has been suspended. Reason: {{Reason}} Date: {{SuspendedAtUtc}}",
            ["Reason", "SuspendedAtUtc"]);
    }

    private static NotificationTemplate TenantReactivated(string locale)
    {
        var isTurkish = locale == "tr";
        return Create(
            "tenant.reactivated.email",
            locale,
            isTurkish ? "Diten tenant erisimi yeniden acildi" : "Diten tenant access reactivated",
            isTurkish
                ? "<p>Merhaba,</p><p>Tenant erisiminiz yeniden acildi.</p><p>Tarih: {{ReactivatedAtUtc}}</p>"
                : "<p>Hello,</p><p>Your tenant access has been reactivated.</p><p>Date: {{ReactivatedAtUtc}}</p>",
            isTurkish
                ? "Tenant erisiminiz yeniden acildi. Tarih: {{ReactivatedAtUtc}}"
                : "Your tenant access has been reactivated. Date: {{ReactivatedAtUtc}}",
            ["ReactivatedAtUtc"]);
    }

    private static NotificationTemplate Create(
        string templateKey,
        string locale,
        string subject,
        string bodyHtml,
        string bodyText,
        IReadOnlyList<string> requiredVariables)
    {
        return new NotificationTemplate
        {
            TenantId = null,
            IsPlatformDefault = true,
            TemplateKey = templateKey,
            Channel = NotificationChannelCode.Email,
            Locale = locale,
            SubjectTemplate = subject,
            BodyHtmlTemplate = bodyHtml,
            BodyTextTemplate = bodyText,
            Variables = requiredVariables
                .Select(name => new TemplateVariableDefinition
                {
                    Name = name,
                    Type = TemplateVariableType.String,
                    IsRequired = true
                })
                .ToList(),
            Status = NotificationTemplateStatus.Active,
            SemanticVersion = "1.0.0",
            CreatedBy = "system",
            Version = 1,
            IsDeleted = false
        };
    }
}
