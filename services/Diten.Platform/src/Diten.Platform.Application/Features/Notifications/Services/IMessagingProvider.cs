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
    IReadOnlyList<MessagingProviderAttachment>? Attachments = null,
    /// <summary>
    /// BL-454 — ADDITIVE ONLY. The name this message is sent under, composed by the one sender-name rule
    /// (<c>Diten.BuildingBlocks.Email.EmailSender</c>). Null — every caller that predates the rule — leaves the
    /// provider on its settings row's own name, exactly as before. The ADDRESS is never carried here: it stays
    /// the settings row's.
    /// </summary>
    string? SenderName = null,
    /// <summary>
    /// WP-VW-W5 PLT-NOTIFY — ADDITIVE ONLY. This message's own reply address (already checked to be ONE plain address
    /// by the queue handler), used over the settings row's. Null — every caller that predates it — leaves the provider
    /// on the settings row's reply address, exactly as before.
    /// </summary>
    string? ReplyToEmail = null);

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
