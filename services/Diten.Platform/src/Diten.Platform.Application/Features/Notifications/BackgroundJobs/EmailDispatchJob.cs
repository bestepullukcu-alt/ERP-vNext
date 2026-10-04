using System.Text.Json;
using Diten.BuildingBlocks.BackgroundJobs;
using Diten.BuildingBlocks.Email;
using Diten.Platform.Application.Features.Notifications.Commands;
using Diten.Platform.Application.Features.Notifications.Handlers.CommandHandlers;
using Diten.Platform.Application.Features.Notifications.Services;
using Diten.Platform.Domain.Entities.Notifications;
using Diten.Platform.Domain.Enums;
using Diten.Platform.Domain.Repositories;
using MediatR;
using Microsoft.Extensions.Logging;

namespace Diten.Platform.Application.Features.Notifications.BackgroundJobs;

public sealed class EmailDispatchJob : IBackgroundJobHandler<EmailDispatchJobArgs>
{

    private readonly INotificationDispatchRepository _dispatchRepository;
    private readonly ITenantMessagingSettingsResolver _settingsResolver;
    private readonly IMessagingProviderResolver _providerResolver;
    private readonly IMediator _mediator;
    private readonly ILogger<EmailDispatchJob> _logger;
    // BL-374 — trailing and OPTIONAL: both are already registered in DI (QueueEmailNotificationHandler uses
    // the same two), so production always gets them injected. A test double built against the old 5-arg
    // shape (NotificationsBatch2Tests' own BuildJob) still compiles and runs unchanged — it simply never gets
    // a full-fidelity retry, which is exactly this job's own PRE-BL-374 behaviour.
    private readonly INotificationTemplateRepository? _templateRepository;
    private readonly IEmailTemplateRenderer? _renderer;
    // BL-454 — same shape, same reason: registered in DI, absent from older test doubles.
    private readonly IEmailShellComposer? _shellComposer;

    public EmailDispatchJob(
        INotificationDispatchRepository dispatchRepository,
        ITenantMessagingSettingsResolver settingsResolver,
        IMessagingProviderResolver providerResolver,
        IMediator mediator,
        ILogger<EmailDispatchJob> logger,
        INotificationTemplateRepository? templateRepository = null,
        IEmailTemplateRenderer? renderer = null,
        IEmailShellComposer? shellComposer = null)
    {
        _shellComposer = shellComposer;
        _dispatchRepository = dispatchRepository;
        _settingsResolver = settingsResolver;
        _providerResolver = providerResolver;
        _mediator = mediator;
        _logger = logger;
        _templateRepository = templateRepository;
        _renderer = renderer;
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

        // BL-454 — a permanent failure (the last retry, or the sweep's retry window) is never tried again: its variables
        // are already released, and a retry rendered from nothing would send blanks.
        if (dispatch.Status is NotificationDispatchStatus.Sent or NotificationDispatchStatus.Cancelled
            || dispatch.PermanentlyFailedNotifiedAt is not null)
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
        string? degradedReason = null;
        try
        {
            (result, degradedReason) = await AttemptSendAsync(dispatch, context, cancellationToken);
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
            // BL-454 — a retry that could only send the stored preview is marked as such on the dispatch itself, so the
            // monitoring screen says it, not only a log line.
            await _mediator.Send(
                new MarkNotificationDispatchSentCommand(dispatch.TenantId, dispatch.Id, result.ProviderMessageId, degradedReason),
                cancellationToken);
            return;
        }

        var newRetryCount = dispatch.RetryCount + 1;
        var nextRetryAt = ComputeNextRetryAt(newRetryCount);
        // BL-406 — this is the ONLY place that knows both the retry count this failure is about to persist AND
        // the maxRetryCount the sweep used to select the dispatch for this attempt in the first place. Once
        // newRetryCount reaches args.MaxRetryCount, FindDueRetriesAsync's own `RetryCount < maxRetryCount` filter
        // will never surface this dispatch again — so this is the one and only transition where "no further
        // retry is coming" becomes true, never re-entered on a later sweep pass over the same terminal row.
        var isPermanentFailure = newRetryCount >= args.MaxRetryCount;
        await _mediator.Send(
            new MarkNotificationDispatchFailedCommand(
                dispatch.TenantId,
                dispatch.Id,
                Redact(result.ErrorCode) ?? "ProviderRejected",
                Redact(result.ErrorMessage) ?? "Provider rejected the message.",
                RetryCount: newRetryCount,
                NextRetryAt: nextRetryAt,
                IsPermanentFailure: isPermanentFailure),
            cancellationToken);
    }

    private async Task<(MessagingProviderResult Result, string? DegradedReason)> AttemptSendAsync(NotificationDispatch dispatch, BackgroundJobContext context, CancellationToken cancellationToken)
    {
        var settings = await _settingsResolver.ResolveAsync(dispatch.TenantId, cancellationToken);
        if (!settings.IsSuccessful || settings.Data is null)
        {
            return (MessagingProviderResult.Fail("SettingsUnresolved", "Tenant messaging settings could not be resolved."), null);
        }

        if (!Enum.TryParse<MessagingProviderCode>(settings.Data.ProviderCode, ignoreCase: true, out var providerCode))
        {
            return (MessagingProviderResult.Fail("ProviderInvalid", "Resolved provider code is invalid."), null);
        }

        var providerResponse = _providerResolver.Resolve(providerCode);
        if (!providerResponse.IsSuccessful || providerResponse.Data is null)
        {
            return (MessagingProviderResult.Fail("ProviderUnavailable", "Messaging provider is unavailable."), null);
        }

        var correlationId = string.IsNullOrWhiteSpace(dispatch.CorrelationId)
            ? context.EffectiveCorrelationId.ToString("N")
            : dispatch.CorrelationId;

        var (bodyHtml, bodyText, template, variables, degradedReason) = await ResolveRetryBodyAsync(dispatch, context, cancellationToken);
        var subject = EmailHeaderText.CleanSubject(dispatch.Subject);
        string? senderName = null;
        if (_shellComposer is not null)
        {
            /*
             * BL-454 — a retry is framed like the first send. When the full body could be reproduced it is framed
             * with its template's own heading, table and action; when all that is left is the stored preview
             * (variables masked, template moved on), THAT fragment is framed — a degraded body, never a bare one.
             */
            var composed = await _shellComposer.ComposeAsync(
                dispatch.TenantId,
                template,
                dispatch.Locale,
                subject,
                bodyHtml ?? dispatch.BodyHtmlPreview,
                bodyText ?? dispatch.BodyTextPreview,
                variables,
                cancellationToken);
            bodyHtml = composed.BodyHtml;
            bodyText = composed.BodyText;
            senderName = composed.SenderName;
        }

        var attachments = dispatch.Attachments.Count == 0
            ? null
            : dispatch.Attachments.Select(ToProviderAttachment).ToArray();

        var sent = await providerResponse.Data.SendEmailAsync(
            new MessagingProviderEmailRequest(
                dispatch.Id,
                dispatch.TenantId,
                correlationId,
                subject,
                dispatch.To.Select(ToProviderRecipient).ToArray(),
                dispatch.Cc.Select(ToProviderRecipient).ToArray(),
                dispatch.Bcc.Select(ToProviderRecipient).ToArray(),
                dispatch.BodyHtmlPreview,
                dispatch.BodyTextPreview,
                bodyHtml,
                bodyText,
                attachments,
                senderName),
            cancellationToken);
        return (sent, degradedReason);
    }

    /// <summary>
    /// BL-374 — a retry's whole point: try to reproduce the ORIGINAL full body (not the masked preview) from
    /// TemplateId + the persisted VariablesJson, but only when doing so cannot resurface a value the queue-time
    /// masking deliberately removed, and only when the template itself has not moved since. Every other
    /// outcome falls back to today's pre-BL-374 behaviour (the preview alone) and says why — never silently.
    /// </summary>
    private async Task<(string? BodyHtml, string? BodyText, NotificationTemplate? Template, IReadOnlyDictionary<string, object?>? Variables, string? DegradedReason)> ResolveRetryBodyAsync(
        NotificationDispatch dispatch, BackgroundJobContext context, CancellationToken ct)
    {
        if (_templateRepository is null || _renderer is null)
        {
            // No renderer/template repository wired in (older test doubles) — not a BL-374 refusal, just the
            // feature not being present at all. Behaves exactly as it did before this WP.
            return (null, null, null, null, null);
        }

        if (dispatch.VariablesJson.Contains(QueueEmailNotificationHandler.RedactedToken, StringComparison.Ordinal))
        {
            LogRetryDegraded(dispatch, context, "VariablesRedacted");
            return (null, null, null, null, "VariablesRedacted");
        }

        if (dispatch.TemplateId is not { } templateId)
        {
            LogRetryDegraded(dispatch, context, "TemplateIdMissing");
            return (null, null, null, null, "TemplateIdMissing");
        }

        var template = await _templateRepository.GetByIdAsync(templateId, ct);
        if (template is null)
        {
            LogRetryDegraded(dispatch, context, "TemplateNotFound");
            return (null, null, null, null, "TemplateNotFound");
        }

        if (!string.Equals(template.SemanticVersion, dispatch.TemplateSemanticVersion, StringComparison.Ordinal))
        {
            LogRetryDegraded(dispatch, context, "TemplateVersionChanged");
            return (null, null, null, null, "TemplateVersionChanged");
        }

        // BL-454 — the same strings the first send rendered (see NotificationVariables), looked up without regard to case.
        var variables = NotificationVariables.FromJson(dispatch.VariablesJson);
        var rendered = _renderer.Render(template, variables);
        if (!rendered.IsSuccessful || rendered.Data is null)
        {
            LogRetryDegraded(dispatch, context, "RenderFailed");
            return (null, null, null, null, "RenderFailed");
        }

        return (rendered.Data.BodyHtml, rendered.Data.BodyText, template, variables, null);
    }

    // BL-454 — a Warning: a degraded retry sends the stored, masked preview (a subject or a link may read [REDACTED]).
    private void LogRetryDegraded(NotificationDispatch dispatch, BackgroundJobContext context, string reasonCode) =>
        _logger.LogWarning(
            "email.dispatch.retry_degraded DispatchId={DispatchId} TenantId={TenantId} ReasonCode={ReasonCode} CorrelationId={CorrelationId}",
            dispatch.Id,
            dispatch.TenantId,
            reasonCode,
            context.EffectiveCorrelationId);

    private static EmailRecipientDto ToProviderRecipient(EmailRecipient recipient) =>
        new(recipient.Email, recipient.DisplayName);

    private static MessagingProviderAttachment ToProviderAttachment(NotificationDispatchAttachment attachment) =>
        new(attachment.FileName, attachment.ContentType, attachment.Content);

    private static DateTimeOffset ComputeNextRetryAt(int retryCount) =>
        EmailDispatchRetryPolicy.NextRetryAt(retryCount, DateTimeOffset.UtcNow);

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
