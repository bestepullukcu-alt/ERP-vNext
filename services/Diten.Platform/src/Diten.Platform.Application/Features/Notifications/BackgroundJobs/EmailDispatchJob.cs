using Diten.BuildingBlocks.BackgroundJobs;
using Diten.Platform.Application.Features.Notifications.Commands;
using Diten.Platform.Application.Features.Notifications.Services;
using Diten.Platform.Domain.Entities.Notifications;
using Diten.Platform.Domain.Enums;
using Diten.Platform.Domain.Repositories;
using MediatR;
using Microsoft.Extensions.Logging;

namespace Diten.Platform.Application.Features.Notifications.BackgroundJobs;

public sealed class EmailDispatchJob : IBackgroundJobHandler<EmailDispatchJobArgs>
{
    private static readonly TimeSpan BaseRetryDelay = TimeSpan.FromMinutes(1);
    private const int MaxRetryDelayMinutes = 60;

    private readonly INotificationDispatchRepository _dispatchRepository;
    private readonly ITenantMessagingSettingsResolver _settingsResolver;
    private readonly IMessagingProviderResolver _providerResolver;
    private readonly IMediator _mediator;
    private readonly ILogger<EmailDispatchJob> _logger;

    public EmailDispatchJob(
        INotificationDispatchRepository dispatchRepository,
        ITenantMessagingSettingsResolver settingsResolver,
        IMessagingProviderResolver providerResolver,
        IMediator mediator,
        ILogger<EmailDispatchJob> logger)
    {
        _dispatchRepository = dispatchRepository;
        _settingsResolver = settingsResolver;
        _providerResolver = providerResolver;
        _mediator = mediator;
        _logger = logger;
    }

    public async Task HandleAsync(EmailDispatchJobArgs args, BackgroundJobContext context, CancellationToken cancellationToken = default)
    {
        cancellationToken.ThrowIfCancellationRequested();
        ArgumentNullException.ThrowIfNull(args);
        ArgumentNullException.ThrowIfNull(context);

        var dispatch = await _dispatchRepository.GetByIdForTenantAsync(args.TenantId, args.DispatchId, cancellationToken);
        if (dispatch is null)
        {
            _logger.LogWarning(
                "email.dispatch.job.not_found DispatchId={DispatchId} TenantId={TenantId} CorrelationId={CorrelationId}",
                args.DispatchId,
                args.TenantId,
                context.EffectiveCorrelationId);
            return;
        }

        if (dispatch.Status is NotificationDispatchStatus.Sent or NotificationDispatchStatus.Cancelled)
        {
            _logger.LogInformation(
                "email.dispatch.job.skipped DispatchId={DispatchId} TenantId={TenantId} Status={Status} CorrelationId={CorrelationId}",
                dispatch.Id,
                dispatch.TenantId,
                dispatch.Status,
                context.EffectiveCorrelationId);
            return;
        }

        MessagingProviderResult result;
        try
        {
            result = await AttemptSendAsync(dispatch, context, cancellationToken);
        }
        catch (OperationCanceledException)
        {
            throw;
        }
        catch (Exception ex)
        {
            result = MessagingProviderResult.Fail("ProviderException", Redact(ex.GetType().Name) ?? "ProviderException");
            _logger.LogWarning(
                "email.dispatch.job.exception DispatchId={DispatchId} TenantId={TenantId} ExceptionType={ExceptionType} CorrelationId={CorrelationId}",
                dispatch.Id,
                dispatch.TenantId,
                ex.GetType().Name,
                context.EffectiveCorrelationId);
        }

        if (result.Accepted)
        {
            await _mediator.Send(
                new MarkNotificationDispatchSentCommand(dispatch.TenantId, dispatch.Id, result.ProviderMessageId),
                cancellationToken);
            return;
        }

        var nextRetryAt = ComputeNextRetryAt(dispatch.RetryCount + 1);
        await _mediator.Send(
            new MarkNotificationDispatchFailedCommand(
                dispatch.TenantId,
                dispatch.Id,
                Redact(result.ErrorCode) ?? "ProviderRejected",
                Redact(result.ErrorMessage) ?? "Provider rejected the message.",
                RetryCount: dispatch.RetryCount + 1,
                NextRetryAt: nextRetryAt),
            cancellationToken);
    }

    private async Task<MessagingProviderResult> AttemptSendAsync(NotificationDispatch dispatch, BackgroundJobContext context, CancellationToken cancellationToken)
    {
        var settings = await _settingsResolver.ResolveAsync(dispatch.TenantId, cancellationToken);
        if (!settings.IsSuccessful || settings.Data is null)
        {
            return MessagingProviderResult.Fail("SettingsUnresolved", "Tenant messaging settings could not be resolved.");
        }

        if (!Enum.TryParse<MessagingProviderCode>(settings.Data.ProviderCode, ignoreCase: true, out var providerCode))
        {
            return MessagingProviderResult.Fail("ProviderInvalid", "Resolved provider code is invalid.");
        }

        var providerResponse = _providerResolver.Resolve(providerCode);
        if (!providerResponse.IsSuccessful || providerResponse.Data is null)
        {
            return MessagingProviderResult.Fail("ProviderUnavailable", "Messaging provider is unavailable.");
        }

        var correlationId = string.IsNullOrWhiteSpace(dispatch.CorrelationId)
            ? context.EffectiveCorrelationId.ToString("N")
            : dispatch.CorrelationId;

        return await providerResponse.Data.SendEmailAsync(
            new MessagingProviderEmailRequest(
                dispatch.Id,
                dispatch.TenantId,
                correlationId,
                dispatch.Subject,
                dispatch.To.Select(ToProviderRecipient).ToArray(),
                dispatch.Cc.Select(ToProviderRecipient).ToArray(),
                dispatch.Bcc.Select(ToProviderRecipient).ToArray(),
                dispatch.BodyHtmlPreview,
                dispatch.BodyTextPreview),
            cancellationToken);
    }

    private static EmailRecipientDto ToProviderRecipient(EmailRecipient recipient) =>
        new(recipient.Email, recipient.DisplayName);

    private static DateTimeOffset ComputeNextRetryAt(int retryCount)
    {
        var clampedAttempt = Math.Max(1, retryCount);
        var exponentialMinutes = Math.Min(MaxRetryDelayMinutes, (int)BaseRetryDelay.TotalMinutes * (1 << Math.Min(clampedAttempt - 1, 6)));
        return DateTimeOffset.UtcNow.AddMinutes(exponentialMinutes);
    }

    private static string? Redact(string? value)
    {
        if (string.IsNullOrEmpty(value))
        {
            return value;
        }

        return Diten.Platform.Application.Features.Notifications.NotificationParsing.LooksLikeRawSecret(value)
            ? "[REDACTED]"
            : value;
    }
}
