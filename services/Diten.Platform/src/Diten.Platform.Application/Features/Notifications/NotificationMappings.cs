using Diten.Platform.Domain.Entities.Notifications;

namespace Diten.Platform.Application.Features.Notifications;

public static class NotificationMappings
{
    public static TenantMessagingSettingsDto ToDto(this TenantMessagingSettings settings) =>
        new(
            settings.Id,
            settings.TenantId,
            settings.IsPlatformDefault,
            settings.ProviderCode.ToString(),
            settings.SenderEmail,
            settings.SenderName,
            settings.ReplyToEmail,
            settings.Host,
            settings.Port,
            settings.UseSsl,
            settings.ApiBaseUrl,
            settings.CredentialSecretRef,
            settings.IsEnabled,
            settings.FallbackPolicy.ToString(),
            settings.LastValidatedAt,
            settings.ValidationStatus,
            settings.ValidationError,
            settings.CreatedAt,
            settings.UpdatedAt);

    public static ResolvedMessagingSettingsDto ToResolvedDto(this TenantMessagingSettings settings, Guid requestedTenantId) =>
        new(
            settings.Id,
            requestedTenantId,
            settings.TenantId,
            settings.IsPlatformDefault,
            settings.ProviderCode.ToString(),
            settings.SenderEmail,
            settings.SenderName,
            settings.ReplyToEmail,
            settings.IsEnabled,
            settings.FallbackPolicy.ToString());

    public static NotificationTemplateDto ToDto(this NotificationTemplate template) =>
        new(
            template.Id,
            template.TenantId,
            template.IsPlatformDefault,
            template.TemplateKey,
            template.Channel.ToString(),
            template.Locale,
            template.SubjectTemplate,
            template.BodyHtmlTemplate,
            template.BodyTextTemplate,
            template.Variables.Select(x => new TemplateVariableDefinitionDto(x.Name, x.Type.ToString(), x.IsRequired)).ToArray(),
            template.Status.ToString(),
            template.SemanticVersion,
            template.CreatedAt,
            template.UpdatedAt);

    public static NotificationDispatchDto ToDto(this NotificationDispatch dispatch) =>
        new(
            dispatch.Id,
            dispatch.TenantId,
            dispatch.TemplateKey,
            dispatch.TemplateId,
            dispatch.Locale,
            dispatch.Channel.ToString(),
            dispatch.ProviderCode.ToString(),
            dispatch.ProviderMessageId,
            dispatch.Status.ToString(),
            dispatch.To.Select(ToDto).ToArray(),
            dispatch.Cc.Count,
            dispatch.Bcc.Count,
            dispatch.Subject,
            dispatch.BodyHtmlPreview,
            dispatch.BodyTextPreview,
            dispatch.VariablesJson,
            dispatch.QueuedAt,
            dispatch.SentAt,
            dispatch.FailedAt,
            dispatch.RetryCount,
            dispatch.ErrorCode,
            dispatch.ErrorMessage,
            dispatch.CorrelationId);

    public static NotificationDispatchListItemDto ToListItemDto(this NotificationDispatch dispatch) =>
        new(
            dispatch.Id,
            dispatch.TenantId,
            dispatch.TemplateKey,
            dispatch.Locale,
            dispatch.Channel.ToString(),
            dispatch.ProviderCode.ToString(),
            dispatch.Status.ToString(),
            dispatch.Subject,
            dispatch.QueuedAt,
            dispatch.SentAt,
            dispatch.FailedAt,
            dispatch.To.Count + dispatch.Cc.Count + dispatch.Bcc.Count,
            dispatch.CorrelationId);

    public static EmailRecipientDto ToDto(this EmailRecipient recipient) =>
        new(MaskEmail(recipient.Email), recipient.DisplayName);

    private static string MaskEmail(string email)
    {
        var atIndex = email.IndexOf('@');
        if (atIndex <= 1)
        {
            return "***";
        }

        return $"{email[0]}***{email[atIndex..]}";
    }
}
