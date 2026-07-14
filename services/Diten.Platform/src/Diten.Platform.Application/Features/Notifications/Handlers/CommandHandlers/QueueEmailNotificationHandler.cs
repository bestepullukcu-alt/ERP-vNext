using System.Net;
using System.Text.Json;
using Diten.BuildingBlocks.Eventing;
using Diten.Platform.Application.Common;
using Diten.Platform.Application.Features.Notifications.Commands;
using Diten.Platform.Application.Features.Notifications.Services;
using Diten.Platform.Contracts.Events.Notifications;
using Diten.Platform.Domain.Entities.Notifications;
using Diten.Platform.Domain.Enums;
using Diten.Platform.Domain.Repositories;
using MediatR;
using Microsoft.Extensions.Logging;

namespace Diten.Platform.Application.Features.Notifications.Handlers.CommandHandlers;

public sealed class QueueEmailNotificationHandler
    : IRequestHandler<QueueEmailNotificationCommand, Response<NotificationDispatchDto>>
{
    private readonly ITenantMessagingSettingsResolver _settingsResolver;
    private readonly INotificationTemplateRepository _templateRepository;
    private readonly IEmailTemplateRenderer _renderer;
    private readonly INotificationDispatchRepository _dispatchRepository;
    private readonly IMessagingProviderResolver _providerResolver;
    private readonly IEventBus _eventBus;
    private readonly ILogger<QueueEmailNotificationHandler> _logger;

    public QueueEmailNotificationHandler(
        ITenantMessagingSettingsResolver settingsResolver,
        INotificationTemplateRepository templateRepository,
        IEmailTemplateRenderer renderer,
        INotificationDispatchRepository dispatchRepository,
        IMessagingProviderResolver providerResolver,
        IEventBus eventBus,
        ILogger<QueueEmailNotificationHandler> logger)
    {
        _settingsResolver = settingsResolver;
        _templateRepository = templateRepository;
        _renderer = renderer;
        _dispatchRepository = dispatchRepository;
        _providerResolver = providerResolver;
        _eventBus = eventBus;
        _logger = logger;
    }

    public async Task<Response<NotificationDispatchDto>> Handle(QueueEmailNotificationCommand request, CancellationToken ct)
    {
        var settingsResponse = await _settingsResolver.ResolveAsync(request.TenantId, ct);
        if (!settingsResponse.IsSuccessful || settingsResponse.Data is null)
        {
            return Response<NotificationDispatchDto>.Fail(settingsResponse.Errors, settingsResponse.StatusCode);
        }

        if (!Enum.TryParse<MessagingProviderCode>(settingsResponse.Data.ProviderCode, ignoreCase: true, out var providerCode))
        {
            return Response<NotificationDispatchDto>.Fail("Resolved provider code is invalid.", 400);
        }

        var providerResponse = _providerResolver.Resolve(providerCode);
        if (!providerResponse.IsSuccessful || providerResponse.Data is null)
        {
            return Response<NotificationDispatchDto>.Fail(providerResponse.Errors, providerResponse.StatusCode);
        }

        var template = await _templateRepository.GetBestActiveByKeyAsync(
            request.TenantId,
            NotificationParsing.NormalizeTemplateKey(request.Request.TemplateKey),
            NotificationParsing.NormalizeLocale(request.Request.Locale),
            NotificationChannelCode.Email,
            ct);
        if (template is null)
        {
            return Response<NotificationDispatchDto>.Fail("Notification template not found.", 404);
        }

        var renderResponse = _renderer.Render(template, request.Request.Variables);
        if (!renderResponse.IsSuccessful || renderResponse.Data is null)
        {
            return Response<NotificationDispatchDto>.Fail(renderResponse.Errors, renderResponse.StatusCode);
        }

        var correlationId = string.IsNullOrWhiteSpace(request.CorrelationId)
            ? Guid.NewGuid().ToString("N")
            : request.CorrelationId;
        var dispatch = new NotificationDispatch
        {
            TenantId = request.TenantId,
            TemplateKey = template.TemplateKey,
            TemplateId = template.Id,
            Locale = template.Locale,
            Channel = template.Channel,
            ProviderCode = providerCode,
            Status = NotificationDispatchStatus.Queued,
            To = MapRecipients(request.Request.To),
            Cc = MapRecipients(request.Request.Cc ?? []),
            Bcc = MapRecipients(request.Request.Bcc ?? []),
            Subject = renderResponse.Data.Subject,
            // The full rendered body (renderResponse.Data.BodyHtml) is sent to the provider below; the
            // truncated preview is the only body form PERSISTED on the dispatch, so mask any sensitive
            // variable values (e.g. temporary passwords) out of it to avoid leaking them into the record
            // and the read-only dispatch monitoring UI.
            BodyHtmlPreview = MaskSensitiveValues(renderResponse.Data.BodyHtmlPreview, request.Request.Variables),
            BodyTextPreview = MaskSensitiveValues(renderResponse.Data.BodyTextPreview, request.Request.Variables),
            VariablesJson = JsonSerializer.Serialize(SanitizeVariables(request.Request.Variables)),
            QueuedAt = DateTimeOffset.UtcNow,
            RetryCount = 0,
            CorrelationId = correlationId,
            CausationId = request.Request.CausationId
        };

        await _dispatchRepository.CreateAsync(dispatch, ct);

        await _eventBus.PublishAsync(
            new NotificationEmailQueuedV1(
                dispatch.Id,
                dispatch.TenantId,
                dispatch.TemplateKey,
                dispatch.TemplateId,
                dispatch.Locale,
                dispatch.ProviderCode.ToString(),
                dispatch.To.Count + dispatch.Cc.Count + dispatch.Bcc.Count,
                dispatch.QueuedAt,
                dispatch.CorrelationId),
            new EventPublishOptions { TenantId = dispatch.TenantId, CausationId = dispatch.CausationId },
            ct);

        var providerResult = await providerResponse.Data.SendEmailAsync(
            new MessagingProviderEmailRequest(
                dispatch.Id,
                dispatch.TenantId,
                correlationId,
                dispatch.Subject,
                dispatch.To.Select(ToProviderRecipient).ToArray(),
                dispatch.Cc.Select(ToProviderRecipient).ToArray(),
                dispatch.Bcc.Select(ToProviderRecipient).ToArray(),
                dispatch.BodyHtmlPreview,
                dispatch.BodyTextPreview,
                renderResponse.Data.BodyHtml,
                renderResponse.Data.BodyText),
            ct);

        if (providerResult.Accepted)
        {
            dispatch.TryMarkSent(providerResult.ProviderMessageId, DateTimeOffset.UtcNow);
            await _dispatchRepository.UpdateAsync(dispatch, ct);
            await _eventBus.PublishAsync(
                new NotificationDispatchSentV1(
                    dispatch.Id,
                    dispatch.TenantId,
                    dispatch.TemplateKey,
                    dispatch.Locale,
                    dispatch.ProviderCode.ToString(),
                    dispatch.ProviderMessageId,
                    dispatch.RetryCount,
                    dispatch.SentAt ?? DateTimeOffset.UtcNow,
                    dispatch.CorrelationId),
                new EventPublishOptions { TenantId = dispatch.TenantId, CausationId = dispatch.CausationId },
                ct);
            _logger.LogInformation("Notification dispatch accepted. DispatchId={DispatchId} TenantId={TenantId} Status={Status} CorrelationId={CorrelationId}", dispatch.Id, dispatch.TenantId, dispatch.Status, dispatch.CorrelationId);
            return Response<NotificationDispatchDto>.Success(dispatch.ToDto(), 201);
        }

        dispatch.TryMarkFailed(
            Redact(providerResult.ErrorCode) ?? "ProviderRejected",
            Redact(providerResult.ErrorMessage) ?? "Provider rejected the message.",
            DateTimeOffset.UtcNow);
        await _dispatchRepository.UpdateAsync(dispatch, ct);
        await _eventBus.PublishAsync(
            new NotificationDispatchFailedV1(
                dispatch.Id,
                dispatch.TenantId,
                dispatch.TemplateKey,
                dispatch.Locale,
                dispatch.ProviderCode.ToString(),
                dispatch.ErrorCode ?? "ProviderRejected",
                dispatch.RetryCount,
                dispatch.NextRetryAt,
                dispatch.FailedAt ?? DateTimeOffset.UtcNow,
                dispatch.CorrelationId),
            new EventPublishOptions { TenantId = dispatch.TenantId, CausationId = dispatch.CausationId },
            ct);
        _logger.LogWarning("Notification dispatch failed. DispatchId={DispatchId} TenantId={TenantId} Status={Status} CorrelationId={CorrelationId} ErrorCode={ErrorCode}", dispatch.Id, dispatch.TenantId, dispatch.Status, dispatch.CorrelationId, dispatch.ErrorCode);
        return Response<NotificationDispatchDto>.Fail("Messaging provider rejected the message.", 400);
    }

    private static List<EmailRecipient> MapRecipients(IReadOnlyList<EmailRecipientDto> recipients) =>
        recipients.Select(x => new EmailRecipient
        {
            Email = NotificationParsing.NormalizeEmail(x.Email),
            DisplayName = string.IsNullOrWhiteSpace(x.DisplayName) ? null : x.DisplayName.Trim()
        }).ToList();

    private static EmailRecipientDto ToProviderRecipient(EmailRecipient recipient) =>
        new(recipient.Email, recipient.DisplayName);

    private static IReadOnlyDictionary<string, object?> SanitizeVariables(IReadOnlyDictionary<string, object?> variables) =>
        variables.ToDictionary(
            pair => pair.Key,
            pair => IsSensitiveKey(pair.Key) || NotificationParsing.LooksLikeRawSecret(Convert.ToString(pair.Value)) ? "[REDACTED]" : pair.Value,
            StringComparer.OrdinalIgnoreCase);

    private const string RedactedToken = "[REDACTED]";

    // Replaces the concrete values of sensitive variables inside the persisted body preview, so a rendered
    // secret (temporary password, token, API key) never lands in the dispatch record. The full body sent to
    // the provider is untouched. Masking keys off sensitive variable NAMES only (not the raw-secret value
    // heuristic, which flags ordinary spaced/`=` text like tenant names and login URLs). Both the raw and
    // HTML-encoded forms are masked because the renderer may HTML-encode substituted values.
    private static string? MaskSensitiveValues(string? preview, IReadOnlyDictionary<string, object?> variables)
    {
        if (string.IsNullOrEmpty(preview))
        {
            return preview;
        }

        var masked = preview;
        foreach (var pair in variables)
        {
            if (!IsSensitiveKey(pair.Key))
            {
                continue;
            }

            var value = Convert.ToString(pair.Value);
            if (string.IsNullOrEmpty(value))
            {
                continue;
            }

            masked = masked.Replace(value, RedactedToken, StringComparison.Ordinal);
            var encoded = WebUtility.HtmlEncode(value);
            if (!string.Equals(encoded, value, StringComparison.Ordinal))
            {
                masked = masked.Replace(encoded, RedactedToken, StringComparison.Ordinal);
            }
        }

        return masked;
    }

    private static string? Redact(string? value) =>
        NotificationParsing.LooksLikeRawSecret(value) ? "[REDACTED]" : value;

    private static bool IsSensitiveKey(string key) =>
        key.Contains("secret", StringComparison.OrdinalIgnoreCase)
        || key.Contains("token", StringComparison.OrdinalIgnoreCase)
        || key.Contains("password", StringComparison.OrdinalIgnoreCase)
        || key.Contains("apiKey", StringComparison.OrdinalIgnoreCase)
        || key.Contains("api_key", StringComparison.OrdinalIgnoreCase);
}
