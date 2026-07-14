using Diten.Platform.Application.Common;
using Diten.Platform.Application.Features.Notifications.Commands;
using Diten.Platform.Domain.Entities.Notifications;
using Diten.Platform.Domain.Enums;
using Diten.Platform.Domain.Repositories;
using MediatR;

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

    public NotificationEventDispatchAdapter(
        INotificationEventDefinitionRepository eventRepository,
        IMediator mediator)
    {
        _eventRepository = eventRepository;
        _mediator = mediator;
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

        // Delegate to the EXISTING pipeline unchanged. OptionalVariables pass through as-is (no adapter mutation).
        var queueRequest = new QueueEmailNotificationRequest(
            TemplateKey: templateKey,
            Locale: request.Locale ?? string.Empty,
            Variables: request.Variables,
            To: request.To,
            Cc: request.Cc,
            Bcc: request.Bcc,
            CausationId: request.CausationId);

        var command = new QueueEmailNotificationCommand(request.TenantId, queueRequest, request.CorrelationId);
        return await _mediator.Send(command, ct);
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
