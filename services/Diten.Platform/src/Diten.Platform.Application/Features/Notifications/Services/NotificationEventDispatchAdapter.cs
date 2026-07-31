using Diten.Platform.Application.Common;
using Diten.Platform.Application.Features.Notifications.Commands;
using Diten.Platform.Domain.Entities.Notifications;
using Diten.Platform.Domain.Enums;
using Diten.Platform.Domain.Repositories;
using MediatR;
using Microsoft.Extensions.Logging;

namespace Diten.Platform.Application.Features.Notifications.Services;

// MOD-0027-FU04B — EventCode Dispatch Adapter. Lets a producer start a notification by canonical eventCode instead of a
// raw templateKey. This is a SOURCE-AGNOSTIC resolver ONLY: it resolves the Active NotificationEventDefinition, derives
// its DefaultTemplateKey, validates the event's RequiredVariables, then DELEGATES to the existing
// QueueEmailNotificationCommand (via IMediator) and returns that command's Response<NotificationDispatchDto> unchanged.
// It does NOT change QueueEmailNotificationCommand/handler, does NOT create a new dispatch pipeline/provider/tracking
// model, and does NOT touch any producer. Template EXISTENCE is left to the existing handler
// (NotificationTemplateRepository.GetBestActiveByKeyAsync: tenant → platform-default → neutral-locale fallback).

/// <summary>Producer-supplied dispatch request keyed by eventCode (recipients + variables come from the producer).</summary>
public sealed record NotificationEventDispatchRequest(
    Guid TenantId,
    string EventCode,
    IReadOnlyList<EmailRecipientDto> To,
    IReadOnlyDictionary<string, object?> Variables,
    /// <summary>
    /// The recipient's language, if the producer knows it. <c>null</c> means "I do not know" and is a legitimate
    /// answer — <see cref="INotificationLocaleResolver"/> then supplies the tenant's own configured language.
    ///
    /// <para><b>This used to be a lie.</b> The field read as optional and the adapter forwarded
    /// <c>request.Locale ?? string.Empty</c> into a command whose validator says
    /// <c>RuleFor(x => x.Request.Locale).NotEmpty()</c>. Every producer that trusted the default got a
    /// ValidationException instead of a notification — which is exactly how MOD-0024 shipped five task events that
    /// sent nothing. Optional now means optional: nobody downstream ever receives a blank locale.</para>
    /// </summary>
    string? Locale = null,
    IReadOnlyList<EmailRecipientDto>? Cc = null,
    IReadOnlyList<EmailRecipientDto>? Bcc = null,
    string? CorrelationId = null,
    Guid? CausationId = null);

public interface INotificationEventDispatchAdapter
{
    Task<Response<NotificationDispatchDto>> DispatchByEventCodeAsync(
        NotificationEventDispatchRequest request, CancellationToken ct = default);
}

public sealed class NotificationEventDispatchAdapter : INotificationEventDispatchAdapter
{
    // Failure reason codes (surfaced on Response.ReasonCode; controlled — never throws).
    public const string ReasonInvalidEventCode = "INVALID_EVENT_CODE";
    public const string ReasonEventNotFound = "EVENT_NOT_FOUND";
    public const string ReasonEventNotActive = "EVENT_NOT_ACTIVE";
    public const string ReasonTemplateKeyMissing = "TEMPLATE_KEY_MISSING_OR_INVALID";
    public const string ReasonRequiredVariableMissing = "REQUIRED_VARIABLE_MISSING";
    public const string ReasonRecipientMissing = "RECIPIENT_MISSING";

    private readonly INotificationEventDefinitionRepository _eventRepository;
    private readonly IMediator _mediator;
    private readonly INotificationLocaleResolver _localeResolver;
    private readonly ILogger<NotificationEventDispatchAdapter> _logger;

    public NotificationEventDispatchAdapter(
        INotificationEventDefinitionRepository eventRepository,
        IMediator mediator,
        INotificationLocaleResolver localeResolver,
        ILogger<NotificationEventDispatchAdapter> logger)
    {
        _eventRepository = eventRepository;
        _mediator = mediator;
        _localeResolver = localeResolver;
        _logger = logger;
    }

    public async Task<Response<NotificationDispatchDto>> DispatchByEventCodeAsync(
        NotificationEventDispatchRequest request, CancellationToken ct = default)
    {
        var eventCode = (request.EventCode ?? string.Empty).Trim().ToLowerInvariant();

        // 1) EventCode format.
        if (!NotificationParsing.IsValidTemplateKey(eventCode))
        {
            return Response<NotificationDispatchDto>.Fail("Invalid event code.", 400, ReasonInvalidEventCode);
        }

        // 2) Event lookup.
        var definition = await _eventRepository.GetByEventCodeAsync(eventCode, ct);
        if (definition is null)
        {
            return Response<NotificationDispatchDto>.Fail("Notification event not found.", 404, ReasonEventNotFound);
        }

        // 3) Status must be Active (Draft/Deprecated/Archived never dispatch).
        if (definition.Status != NotificationEventStatus.Active)
        {
            return Response<NotificationDispatchDto>.Fail(
                $"Event is not active ({definition.Status}).", 409, ReasonEventNotActive);
        }

        // 4) DefaultTemplateKey present + valid format (existence is the handler's authority — not double-checked here).
        var templateKey = NotificationParsing.NormalizeTemplateKey(definition.DefaultTemplateKey);
        if (!NotificationParsing.IsValidTemplateKey(templateKey))
        {
            return Response<NotificationDispatchDto>.Fail(
                "Event has no valid default template key.", 422, ReasonTemplateKeyMissing);
        }

        // 5) RequiredVariables: every required variable must be present with a non-empty value.
        var missing = FindMissingRequiredVariables(definition.RequiredVariables, request.Variables);
        if (missing.Count > 0)
        {
            return Response<NotificationDispatchDto>.Fail(
                $"Missing required variables: {string.Join(", ", missing)}.", 422, ReasonRequiredVariableMissing);
        }

        // 6) At least one recipient.
        if (request.To is null || request.To.Count == 0)
        {
            return Response<NotificationDispatchDto>.Fail(
                "At least one recipient is required.", 400, ReasonRecipientMissing);
        }

        /*
         * 7) Locale. QueueEmailNotificationRequest.Locale is a non-nullable string and its validator says NotEmpty,
         *    so this adapter is the last place that can honour its own optional-looking Locale. It resolves rather
         *    than defaults: caller's value → tenant's configured language → "en". The previous line here was
         *    `request.Locale ?? string.Empty`, which satisfied the compiler and failed the validator.
         */
        var locale = await _localeResolver.ResolveAsync(request.TenantId, request.Locale, ct);

        // Delegate to the EXISTING pipeline unchanged. OptionalVariables pass through as-is (no adapter mutation).
        var queueRequest = new QueueEmailNotificationRequest(
            TemplateKey: templateKey,
            Locale: locale,
            Variables: request.Variables,
            To: request.To,
            Cc: request.Cc,
            Bcc: request.Bcc,
            CausationId: request.CausationId);

        var command = new QueueEmailNotificationCommand(request.TenantId, queueRequest, request.CorrelationId);
        var response = await _mediator.Send(command, ct);

        if (!response.IsSuccessful)
        {
            /*
             * The authoritative diagnosis line, emitted where the facts actually live.
             *
             * This adapter is the only place that knows the RESOLVED template key and the RESOLVED locale together
             * with the downstream reason code — the caller sees a Response with no room for either. Without this
             * line the operator's question ("which language was looked for, under which key?") had no answer
             * anywhere in the logs, and WC-4 spent a round guessing at a locale/template problem that did not
             * exist while the real refusal was missing messaging settings.
             */
            _logger.LogWarning(
                "notification.dispatch_failed TenantId={TenantId} EventCode={EventCode} TemplateKey={TemplateKey} "
                + "Locale={Locale} ReasonCode={ReasonCode} Status={Status} Reason={Reason}",
                request.TenantId,
                eventCode,
                templateKey,
                locale,
                response.ReasonCode ?? "<none>",
                response.StatusCode,
                string.Join(" | ", response.Errors));
        }

        return response;
    }

    // Missing = key absent, null value, or empty/whitespace string value. Non-string non-null values are accepted.
    private static List<string> FindMissingRequiredVariables(
        IReadOnlyList<TemplateVariableDefinition> required,
        IReadOnlyDictionary<string, object?> supplied)
    {
        var missing = new List<string>();
        foreach (var variable in required)
        {
            var name = variable.Name;
            if (string.IsNullOrWhiteSpace(name))
            {
                continue;
            }

            if (!supplied.TryGetValue(name, out var value) || value is null)
            {
                missing.Add(name);
                continue;
            }

            if (value is string s && string.IsNullOrWhiteSpace(s))
            {
                missing.Add(name);
            }
        }
        return missing;
    }
}
