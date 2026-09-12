using Diten.Platform.Application.Common;
using Diten.Platform.Domain.Enums;

namespace Diten.Platform.Application.Features.Notifications.Services;

public interface IMessagingProvider
{
    MessagingProviderCode ProviderCode { get; }
    Task<MessagingProviderResult> SendEmailAsync(MessagingProviderEmailRequest request, CancellationToken ct = default);
}

public sealed record MessagingProviderEmailRequest(
    Guid DispatchId,
    Guid TenantId,
    string CorrelationId,
    string Subject,
    IReadOnlyList<EmailRecipientDto> To,
    IReadOnlyList<EmailRecipientDto> Cc,
    IReadOnlyList<EmailRecipientDto> Bcc,
    string? BodyHtmlPreview,
    string? BodyTextPreview,
    string? BodyHtml = null,
    string? BodyText = null,
    /// <summary>
    /// MOD-0357 S5b — ADDITIVE ONLY: null/empty behaves EXACTLY as before this field existed (every existing
    /// caller and test omits it). Never persisted on <c>NotificationDispatch</c> — a retry
    /// (<c>EmailDispatchJob</c>) rebuilds this request from the persisted dispatch, which has no attachment
    /// column, so a retried send already carries no attachment, the same way it already carries no full body
    /// (only <see cref="BodyHtmlPreview"/>/<see cref="BodyTextPreview"/>) — an existing, accepted limitation
    /// this field does not change.
    /// </summary>
    IReadOnlyList<MessagingProviderAttachment>? Attachments = null);

/// <summary>One file attached to an outgoing e-mail — MOD-0357 S5b's own reason to exist is the meeting
/// <c>.ics</c> invite, but the shape carries nothing meeting-specific.</summary>
public sealed record MessagingProviderAttachment(
    string FileName, string ContentType, byte[] Content, string? ContentId = null);

public sealed record MessagingProviderResult(
    bool Accepted,
    string? ProviderMessageId,
    string? ErrorCode,
    string? ErrorMessage)
{
    public static MessagingProviderResult Success(string providerMessageId) => new(true, providerMessageId, null, null);
    public static MessagingProviderResult Fail(string errorCode, string errorMessage) => new(false, null, errorCode, errorMessage);
}

public interface IMessagingProviderResolver
{
    Response<IMessagingProvider> Resolve(MessagingProviderCode providerCode);
}
