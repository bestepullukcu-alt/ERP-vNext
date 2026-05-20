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
    string? BodyText = null);

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
