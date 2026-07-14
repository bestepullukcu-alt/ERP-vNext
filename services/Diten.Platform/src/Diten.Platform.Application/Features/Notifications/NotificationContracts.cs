using Diten.Platform.Domain.Enums;

namespace Diten.Platform.Application.Features.Notifications;

public sealed record TenantMessagingSettingsDto(
    Guid Id,
    Guid? TenantId,
    bool IsPlatformDefault,
    string ProviderCode,
    string SenderEmail,
    string? SenderName,
    string? ReplyToEmail,
    string? Host,
    int? Port,
    bool UseSsl,
    string? ApiBaseUrl,
    string? CredentialSecretRef,
    bool IsEnabled,
    string FallbackPolicy,
    DateTimeOffset? LastValidatedAt,
    string? ValidationStatus,
    string? ValidationError,
    DateTimeOffset CreatedAt,
    DateTimeOffset? UpdatedAt);

public sealed record TenantMessagingSettingsUpsertRequest(
    string ProviderCode,
    string SenderEmail,
    string? SenderName,
    string? ReplyToEmail,
    string? Host,
    int? Port,
    bool UseSsl,
    string? ApiBaseUrl,
    string? CredentialSecretRef,
    bool IsEnabled,
    string FallbackPolicy);

public sealed record ResolvedMessagingSettingsDto(
    Guid Id,
    Guid RequestedTenantId,
    Guid? EffectiveTenantId,
    bool IsPlatformDefault,
    string ProviderCode,
    string SenderEmail,
    string? SenderName,
    string? ReplyToEmail,
    bool IsEnabled,
    string FallbackPolicy);

public sealed record TemplateVariableDefinitionDto(
    string Name,
    string Type,
    bool IsRequired);

public sealed record NotificationTemplateDto(
    Guid Id,
    Guid? TenantId,
    bool IsPlatformDefault,
    string TemplateKey,
    string Channel,
    string Locale,
    string SubjectTemplate,
    string BodyHtmlTemplate,
    string? BodyTextTemplate,
    IReadOnlyList<TemplateVariableDefinitionDto> Variables,
    string Status,
    string? SemanticVersion,
    DateTimeOffset CreatedAt,
    DateTimeOffset? UpdatedAt);

public sealed record NotificationTemplateUpsertRequest(
    bool IsPlatformDefault,
    string TemplateKey,
    string Channel,
    string Locale,
    string SubjectTemplate,
    string BodyHtmlTemplate,
    string? BodyTextTemplate,
    IReadOnlyList<TemplateVariableDefinitionDto> Variables,
    string Status,
    string? SemanticVersion,
    byte[]? RowVersion = null);

public sealed record RenderTemplateRequest(
    string TemplateKey,
    string Locale,
    NotificationChannelCode Channel,
    IReadOnlyDictionary<string, object?> Variables);

public sealed record RenderedEmailTemplateDto(
    string Subject,
    string? BodyHtmlPreview,
    string? BodyTextPreview,
    string? BodyHtml = null,
    string? BodyText = null);

public sealed record EmailRecipientDto(
    string Email,
    string? DisplayName);

public sealed record EmailMessageDto(
    IReadOnlyList<EmailRecipientDto> To,
    IReadOnlyList<EmailRecipientDto>? Cc,
    IReadOnlyList<EmailRecipientDto>? Bcc);

public sealed record QueueEmailNotificationRequest(
    string TemplateKey,
    string Locale,
    IReadOnlyDictionary<string, object?> Variables,
    IReadOnlyList<EmailRecipientDto> To,
    IReadOnlyList<EmailRecipientDto>? Cc = null,
    IReadOnlyList<EmailRecipientDto>? Bcc = null,
    Guid? CausationId = null);

public sealed record NotificationDispatchDto(
    Guid Id,
    Guid TenantId,
    string TemplateKey,
    Guid? TemplateId,
    string Locale,
    string Channel,
    string ProviderCode,
    string? ProviderMessageId,
    string Status,
    IReadOnlyList<EmailRecipientDto> To,
    int CcCount,
    int BccCount,
    string Subject,
    string? BodyHtmlPreview,
    string? BodyTextPreview,
    string VariablesJson,
    DateTimeOffset QueuedAt,
    DateTimeOffset? SentAt,
    DateTimeOffset? FailedAt,
    int RetryCount,
    string? ErrorCode,
    string? ErrorMessage,
    string? CorrelationId);

public sealed record RenderTemplatePreviewRequest(
    string SubjectTemplate,
    string? BodyHtmlTemplate,
    string? BodyTextTemplate,
    IReadOnlyList<TemplateVariableDefinitionDto> Variables,
    IReadOnlyDictionary<string, object?> SampleVariables);

public sealed record NotificationDispatchListItemDto(
    Guid Id,
    Guid TenantId,
    string TemplateKey,
    string Locale,
    string Channel,
    string ProviderCode,
    string Status,
    string Subject,
    DateTimeOffset QueuedAt,
    DateTimeOffset? SentAt,
    DateTimeOffset? FailedAt,
    int RecipientCount,
    string? CorrelationId);
