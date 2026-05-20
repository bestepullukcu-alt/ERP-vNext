using System.Diagnostics;
using Diten.Platform.Application.Features.Notifications;
using Diten.Platform.Application.Features.Notifications.Services;
using Diten.Platform.Domain.Entities.Notifications;
using Diten.Platform.Domain.Enums;
using Diten.Platform.Domain.Repositories;
using Diten.Platform.Infrastructure.Settings;
using MailKit.Security;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using MimeKit;
using MimeKit.Utils;

namespace Diten.Platform.Infrastructure.Services.Notifications;

internal sealed class SmtpMessagingProvider : IMessagingProvider
{
    private readonly IOptionsMonitor<SmtpProviderOptions> _options;
    private readonly ITenantMessagingSettingsRepository _settingsRepository;
    private readonly ISmtpClientFactory _clientFactory;
    private readonly SecretReferenceResolver _secretResolver;
    private readonly IHostEnvironment _environment;
    private readonly ILogger<SmtpMessagingProvider> _logger;

    public SmtpMessagingProvider(
        IOptionsMonitor<SmtpProviderOptions> options,
        ITenantMessagingSettingsRepository settingsRepository,
        ISmtpClientFactory clientFactory,
        SecretReferenceResolver secretResolver,
        IHostEnvironment environment,
        ILogger<SmtpMessagingProvider> logger)
    {
        _options = options;
        _settingsRepository = settingsRepository;
        _clientFactory = clientFactory;
        _secretResolver = secretResolver;
        _environment = environment;
        _logger = logger;
    }

    public MessagingProviderCode ProviderCode => MessagingProviderCode.Smtp;

    public async Task<MessagingProviderResult> SendEmailAsync(MessagingProviderEmailRequest request, CancellationToken ct = default)
    {
        ArgumentNullException.ThrowIfNull(request);

        var stopwatch = Stopwatch.StartNew();
        var options = _options.CurrentValue;

        var settings = await ResolveSettingsAsync(request.TenantId, ct);
        if (settings is null)
        {
            return LogAndReturnFailure(request, MessagingProviderErrorCodes.ProviderConfigInvalid, "Tenant SMTP settings could not be resolved.", stopwatch);
        }

        var validation = ValidateRequest(request, settings, options);
        if (validation is { } validationFailure)
        {
            return LogAndReturnFailure(request, validationFailure.ErrorCode, validationFailure.ErrorMessage, stopwatch);
        }

        var secretResult = await _secretResolver.ResolveAsync(settings.CredentialSecretRef, ct);
        if (!secretResult.IsSuccessful || secretResult.Value is null)
        {
            return LogAndReturnFailure(request, secretResult.ErrorCode!, secretResult.ErrorMessage!, stopwatch);
        }

        var secureSocketOptions = ResolveSocketOptions(settings, options);
        var timeoutMs = options.SendTimeoutSeconds * 1000;

        using var timeoutCts = CancellationTokenSource.CreateLinkedTokenSource(ct);
        timeoutCts.CancelAfter(TimeSpan.FromSeconds(options.SendTimeoutSeconds));

        using var transport = _clientFactory.Create();
        transport.Timeout = timeoutMs;

        try
        {
            var message = BuildMessage(request, settings);

            await transport.ConnectAsync(settings.Host!, settings.Port!.Value, secureSocketOptions, timeoutCts.Token);
            // Batch 1.1 limitation: TenantMessagingSettings carries no dedicated SMTP-AUTH username field;
            // we use SenderEmail as the AUTH username for MVP. Providers that require a username distinct
            // from the sender mailbox need a future MOD-0027 contract amendment to add SmtpUsername.
            await transport.AuthenticateAsync(settings.SenderEmail, secretResult.Value, timeoutCts.Token);
            var providerMessageId = await transport.SendAsync(message, timeoutCts.Token);
            await transport.DisconnectAsync(true, timeoutCts.Token);

            if (string.IsNullOrWhiteSpace(providerMessageId))
            {
                providerMessageId = string.IsNullOrWhiteSpace(message.MessageId)
                    ? $"smtp-{request.DispatchId:N}"
                    : message.MessageId;
            }

            LogSuccess(request, options, stopwatch, providerMessageId);
            return MessagingProviderResult.Success(providerMessageId);
        }
        catch (OperationCanceledException) when (!ct.IsCancellationRequested)
        {
            return LogAndReturnFailure(
                request,
                MessagingProviderErrorCodes.ProviderTimeout,
                "Operation timed out.",
                stopwatch);
        }
        catch (OperationCanceledException)
        {
            return LogAndReturnFailure(
                request,
                MessagingProviderErrorCodes.ProviderTimeout,
                "Operation cancelled.",
                stopwatch);
        }
        catch (Exception exception)
        {
            var (errorCode, errorMessage) = MessagingProviderErrorMapper.Map(exception);
            return LogAndReturnFailure(request, errorCode, errorMessage, stopwatch, exception.GetType().Name);
        }
    }

    private async Task<TenantMessagingSettings?> ResolveSettingsAsync(Guid tenantId, CancellationToken ct)
    {
        var tenantSettings = await _settingsRepository.GetByTenantIdAsync(tenantId, ct);
        if (tenantSettings is { IsDeleted: false, IsEnabled: true })
        {
            return tenantSettings;
        }

        var platformDefault = await _settingsRepository.GetPlatformDefaultAsync(ct);
        return platformDefault is { IsDeleted: false, IsEnabled: true } ? platformDefault : null;
    }

    private static (string ErrorCode, string ErrorMessage)? ValidateRequest(
        MessagingProviderEmailRequest request,
        TenantMessagingSettings settings,
        SmtpProviderOptions options)
    {
        if (string.IsNullOrWhiteSpace(settings.Host))
        {
            return (MessagingProviderErrorCodes.ProviderConfigInvalid, "SMTP host is missing.");
        }

        if (settings.Port is null or <= 0 or > 65535)
        {
            return (MessagingProviderErrorCodes.ProviderConfigInvalid, "SMTP port is out of range.");
        }

        if (string.IsNullOrWhiteSpace(settings.SenderEmail))
        {
            return (MessagingProviderErrorCodes.ProviderConfigInvalid, "Sender email is missing.");
        }

        if (string.IsNullOrWhiteSpace(settings.CredentialSecretRef))
        {
            return (MessagingProviderErrorCodes.ProviderConfigInvalid, "Credential reference is missing.");
        }

        var recipientCount = (request.To?.Count ?? 0) + (request.Cc?.Count ?? 0) + (request.Bcc?.Count ?? 0);
        if (recipientCount <= 0)
        {
            return (MessagingProviderErrorCodes.ProviderConfigInvalid, "At least one recipient is required.");
        }

        if (recipientCount > options.MaxRecipientsPerMessage)
        {
            return (MessagingProviderErrorCodes.ProviderRejectedRecipientLimit, "Recipient limit exceeded.");
        }

        return null;
    }

    private SecureSocketOptions ResolveSocketOptions(TenantMessagingSettings settings, SmtpProviderOptions options)
    {
        if (settings.UseSsl)
        {
            return SecureSocketOptions.StartTlsWhenAvailable;
        }

        if (options.AllowInsecureTlsInDevelopment && !_environment.IsProduction())
        {
            return SecureSocketOptions.None;
        }

        return SecureSocketOptions.StartTlsWhenAvailable;
    }

    private static MimeMessage BuildMessage(MessagingProviderEmailRequest request, TenantMessagingSettings settings)
    {
        var message = new MimeMessage();
        message.From.Add(new MailboxAddress(settings.SenderName ?? string.Empty, settings.SenderEmail));

        if (!string.IsNullOrWhiteSpace(settings.ReplyToEmail))
        {
            message.ReplyTo.Add(new MailboxAddress(string.Empty, settings.ReplyToEmail));
        }

        AddRecipients(message.To, request.To);
        AddRecipients(message.Cc, request.Cc);
        AddRecipients(message.Bcc, request.Bcc);

        message.Subject = request.Subject ?? string.Empty;

        // Prefer the full rendered body (Batch 1.1). Preview is the truncated audit/log form
        // and is only used as a fallback for retries that re-issue from a persisted dispatch.
        var builder = new BodyBuilder
        {
            HtmlBody = request.BodyHtml ?? request.BodyHtmlPreview,
            TextBody = request.BodyText ?? request.BodyTextPreview
        };

        message.Body = builder.ToMessageBody();
        if (string.IsNullOrWhiteSpace(message.MessageId))
        {
            message.MessageId = MimeUtils.GenerateMessageId();
        }

        return message;
    }

    private static void AddRecipients(InternetAddressList list, IReadOnlyList<EmailRecipientDto>? recipients)
    {
        if (recipients is null)
        {
            return;
        }

        foreach (var recipient in recipients)
        {
            if (string.IsNullOrWhiteSpace(recipient.Email))
            {
                continue;
            }

            list.Add(new MailboxAddress(recipient.DisplayName ?? string.Empty, recipient.Email));
        }
    }

    private void LogSuccess(MessagingProviderEmailRequest request, SmtpProviderOptions options, Stopwatch stopwatch, string providerMessageId)
    {
        stopwatch.Stop();
        _logger.LogInformation(
            "smtp.provider.send.success ProviderCode={ProviderCode} DispatchId={DispatchId} TenantId={TenantId} CorrelationId={CorrelationId} Status={Status} DurationMs={DurationMs} ProviderMessageId={ProviderMessageId}",
            ProviderCode,
            request.DispatchId,
            request.TenantId,
            request.CorrelationId,
            "Accepted",
            stopwatch.ElapsedMilliseconds,
            providerMessageId);
    }

    private MessagingProviderResult LogAndReturnFailure(
        MessagingProviderEmailRequest request,
        string errorCode,
        string errorMessage,
        Stopwatch stopwatch,
        string? exceptionType = null)
    {
        stopwatch.Stop();
        _logger.LogWarning(
            "smtp.provider.send.failure ProviderCode={ProviderCode} DispatchId={DispatchId} TenantId={TenantId} CorrelationId={CorrelationId} Status={Status} ErrorCode={ErrorCode} DurationMs={DurationMs} ExceptionType={ExceptionType}",
            ProviderCode,
            request.DispatchId,
            request.TenantId,
            request.CorrelationId,
            "Failed",
            errorCode,
            stopwatch.ElapsedMilliseconds,
            exceptionType ?? string.Empty);

        return MessagingProviderResult.Fail(errorCode, errorMessage);
    }
}
