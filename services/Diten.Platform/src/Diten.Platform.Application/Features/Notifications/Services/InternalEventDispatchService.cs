using Diten.Platform.Application.Common;
using Diten.Platform.Application.Contracts;
using Diten.Platform.Common.Tenancy;
using Diten.Platform.Domain.Entities;
using Diten.Platform.Domain.Enums;
using Diten.Platform.Domain.Repositories;
using Microsoft.Extensions.Logging;

namespace Diten.Platform.Application.Features.Notifications.Services;

/// <summary>One reader of an internal event: exactly one of <paramref name="UserId"/> (that person),
/// <paramref name="LineManagerOf"/> (that person's line manager, K-N2) or <paramref name="Email"/> (an address the
/// caller holds — a mailbox, not a user).</summary>
/// <param name="DisplayName">Only with <paramref name="Email"/>.</param>
/// <param name="Locale">Only with <paramref name="Email"/>; a user's language comes from their own preference.</param>
public sealed record InternalEventRecipient(
    Guid? UserId = null,
    Guid? LineManagerOf = null,
    string? Email = null,
    string? DisplayName = null,
    string? Locale = null);

public sealed record InternalEventRecipients(
    IReadOnlyList<InternalEventRecipient>? To = null,
    IReadOnlyList<InternalEventRecipient>? Cc = null);

/// <summary>
/// WP-VW-W5 PLT-NOTIFY (K-N1) — what a service or a scheduler sends to <c>POST api/internal/notifications/events/dispatch</c>.
/// There is no request context behind it: the tenant comes from <paramref name="TenantId"/>, the asking module from
/// <paramref name="SourceModule"/>. Variables are display text — the producer formats dates and names.
/// </summary>
public sealed record InternalEventDispatchRequest(
    Guid TenantId,
    string? EventCode,
    string? SourceModule,
    InternalEventRecipients? Recipients,
    IReadOnlyDictionary<string, string?>? Variables,
    string? CorrelationId,
    Guid? CausationId = null,
    InternalEventReplyTo? ReplyTo = null);

/// <summary>WP-VW-W5 (contract annex) — whose address a reply goes to: a user of the same tenant (the representative,
/// K-N4). The address is resolved here and never returned; unresolved, the tenant's own reply address applies.</summary>
public sealed record InternalEventReplyTo(Guid? UserId);

/// <summary>One language group's mail. <paramref name="DispatchId"/> is null when that group was refused before a row was
/// written.</summary>
public sealed record InternalEventDispatchItemDto(Guid? DispatchId, string Locale, string Status, string? ReasonCode = null);

/// <summary>Who a mail went to — a user id and how they were found; NEVER an address.</summary>
/// <param name="Via"><c>direct</c>, <c>line-manager</c>, <c>manager-chain</c> or <c>email</c>.</param>
public sealed record InternalResolvedRecipientDto(Guid? UserId, string Via);

public sealed record InternalResolvedRecipientsDto(
    IReadOnlyList<InternalResolvedRecipientDto> To,
    IReadOnlyList<InternalResolvedRecipientDto> Cc);

/// <param name="Status"><c>sent</c> (every group handed over), <c>queued</c> (accepted, the platform's retry still owes a
/// send), <c>partial</c> (some groups refused), <c>failed</c> (none accepted) or <c>no-recipient</c> (nobody resolved,
/// nothing sent).</param>
public sealed record InternalEventDispatchResultDto(
    IReadOnlyList<InternalEventDispatchItemDto> Dispatches,
    InternalResolvedRecipientsDto Resolved,
    string Status);

public interface IInternalEventDispatchService
{
    Task<Response<InternalEventDispatchResultDto>> DispatchAsync(InternalEventDispatchRequest request, CancellationToken ct = default);
}

/// <summary>
/// WP-VW-W5 PLT-NOTIFY — the internal event dispatch: a service (CRM) or its scheduler starts a catalogued notification
/// with no user behind the call. The key check is the controller's; everything after it is here, in this order:
/// <list type="number">
/// <item>The tenant exists and is Active — otherwise 404, naming nothing.</item>
/// <item>The event is catalogued, Active, and owned by the asking module — otherwise 403: a module sends its own events
/// only, never another module's.</item>
/// <item>The tenant context is pinned from the body for every read below, so a user id or a "line manager of" names a
/// person IN THAT TENANT only; another tenant's id simply resolves to nobody.</item>
/// <item>Readers are resolved to addresses here (AuthService, K-N2); the caller never holds them and the answer never
/// carries them.</item>
/// <item>One mail per language (each reader in their own language); the copies ride with the first group that was
/// accepted. Nobody resolved → nothing is sent and the answer says <c>no-recipient</c>.</item>
/// </list>
/// </summary>
public sealed class InternalEventDispatchService : IInternalEventDispatchService
{
    public const string ReasonBadRequest = "INTERNAL_DISPATCH_BAD_REQUEST";
    public const string ReasonEventForbidden = "EVENT_NOT_OWNED_OR_NOT_ACTIVE";

    public const string StatusSent = "sent";
    public const string StatusQueued = "queued";
    public const string StatusPartial = "partial";
    public const string StatusFailed = "failed";
    public const string StatusNoRecipient = "no-recipient";

    public const string ViaDirect = "direct";
    public const string ViaLineManager = "line-manager";
    public const string ViaManagerChain = "manager-chain";
    public const string ViaEmail = "email";

    /// <summary>The most readers one call may name in To and Cc together.</summary>
    public const int MaxRecipientEntries = 50;

    /// <summary>
    /// A service names itself by its Blueprint module id; the catalog records the code of the manifest that declared the
    /// event (CRM's events live in <c>CrmManifestProvider</c>, module code <c>CRM</c>). This is the explicit list of which
    /// asking module may speak for which owner — anything else must name the owner itself.
    /// </summary>
    public static readonly IReadOnlyDictionary<string, string> ProducerOwners =
        new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase)
        {
            // MOD-0155 Visit / Route Planning — the visit report notifications (WP-VW-W5 K-N4).
            ["MOD-0155"] = "CRM"
        };

    private readonly ITenantRegistryRepository _tenants;
    private readonly INotificationEventDefinitionRepository _events;
    private readonly ITenantContext _tenantContext;
    private readonly ILineManagerResolver _lineManagers;
    private readonly ITaskNotificationRecipientResolver _recipients;
    private readonly INotificationLocaleResolver _locales;
    private readonly INotificationEventDispatchAdapter _adapter;
    private readonly ILogger<InternalEventDispatchService> _logger;

    public InternalEventDispatchService(
        ITenantRegistryRepository tenants,
        INotificationEventDefinitionRepository events,
        ITenantContext tenantContext,
        ILineManagerResolver lineManagers,
        ITaskNotificationRecipientResolver recipients,
        INotificationLocaleResolver locales,
        INotificationEventDispatchAdapter adapter,
        ILogger<InternalEventDispatchService> logger)
    {
        _tenants = tenants;
        _events = events;
        _tenantContext = tenantContext;
        _lineManagers = lineManagers;
        _recipients = recipients;
        _locales = locales;
        _adapter = adapter;
        _logger = logger;
    }

    public async Task<Response<InternalEventDispatchResultDto>> DispatchAsync(
        InternalEventDispatchRequest request, CancellationToken ct = default)
    {
        var shapeError = ShapeError(request);
        if (shapeError is not null)
        {
            return Response<InternalEventDispatchResultDto>.Fail(shapeError, 400, ReasonBadRequest);
        }

        var eventCode = request.EventCode!.Trim().ToLowerInvariant();
        var sourceModule = request.SourceModule!.Trim();

        // 1) The tenant: exists (soft-deleted reads as absent) and is live.
        var tenant = await _tenants.GetByIdAsync(request.TenantId, ct);
        if (tenant is null || tenant.Status != TenantStatus.Active)
        {
            return Response<InternalEventDispatchResultDto>.Fail("Not found.", 404);
        }

        // 2) The event: catalogued, Active, and the asking module's own.
        var definition = await _events.GetByEventCodeAsync(eventCode, ct);
        if (definition is null || definition.Status != NotificationEventStatus.Active || !IsOwnedBy(definition.OwnerModuleId, sourceModule))
        {
            _logger.LogWarning(
                "notification.internal_dispatch_refused EventCode={EventCode} SourceModule={SourceModule} Known={Known}",
                eventCode, sourceModule, definition is not null);
            return Response<InternalEventDispatchResultDto>.Fail(
                "The event is not available to this module.", 403, ReasonEventForbidden);
        }

        // 3) Every read below happens in the body's tenant — and only there.
        using var scope = TenantScope.Begin(_tenantContext, request.TenantId);

        var to = await ResolveAsync(request.TenantId, request.Recipients?.To, ct);
        var cc = await ResolveAsync(request.TenantId, request.Recipients?.Cc, ct);

        // One address, one mail: a reader already in To is not copied as well.
        var toAddresses = new HashSet<string>(to.Select(r => NotificationParsing.NormalizeEmail(r.Email)), StringComparer.Ordinal);
        cc = cc.Where(r => !toAddresses.Contains(NotificationParsing.NormalizeEmail(r.Email))).ToList();

        if (to.Count == 0 && cc.Count > 0)
        {
            // Nobody to write TO, but somebody to inform: they are written to rather than left out.
            (to, cc) = (cc, []);
        }

        var resolved = new InternalResolvedRecipientsDto(
            to.Select(r => new InternalResolvedRecipientDto(r.UserId, r.Via)).ToList(),
            cc.Select(r => new InternalResolvedRecipientDto(r.UserId, r.Via)).ToList());

        // The reply address (contract annex): a user of THIS tenant, resolved like a reader; never in the answer.
        var replyToEmail = await ReplyToAsync(request.ReplyTo, ct);

        if (to.Count == 0)
        {
            _logger.LogInformation(
                "notification.internal_dispatch_no_recipient TenantId={TenantId} EventCode={EventCode} SourceModule={SourceModule} CorrelationId={CorrelationId}",
                request.TenantId, eventCode, sourceModule, request.CorrelationId);
            return Response<InternalEventDispatchResultDto>.Success(
                new InternalEventDispatchResultDto([], resolved, StatusNoRecipient), 200);
        }

        // 4) One mail per language: the reader's own, else the tenant's.
        var groups = new List<(string Locale, List<EmailRecipientDto> To)>();
        foreach (var reader in to)
        {
            var locale = await _locales.ResolveAsync(request.TenantId, SupportedLocaleOrNull(reader.Locale), ct);
            var group = groups.FirstOrDefault(g => string.Equals(g.Locale, locale, StringComparison.Ordinal));
            if (group.To is null)
            {
                group = (locale, []);
                groups.Add(group);
            }

            group.To.Add(new EmailRecipientDto(reader.Email, reader.DisplayName, locale));
        }

        var variables = (request.Variables ?? new Dictionary<string, string?>())
            .ToDictionary(pair => pair.Key, pair => (object?)pair.Value, StringComparer.Ordinal);
        var copies = cc.Select(r => new EmailRecipientDto(r.Email, r.DisplayName)).ToList();
        var copiesPending = copies.Count > 0;

        var items = new List<InternalEventDispatchItemDto>(groups.Count);
        Response<NotificationDispatchDto>? firstRefusal = null;
        foreach (var (locale, readers) in groups)
        {
            Response<NotificationDispatchDto> response;
            try
            {
                response = await _adapter.DispatchByEventCodeAsync(
                    new NotificationEventDispatchRequest(
                        request.TenantId,
                        eventCode,
                        readers,
                        variables,
                        Locale: locale,
                        Cc: copiesPending ? copies : null,
                        CorrelationId: request.CorrelationId,
                        CausationId: request.CausationId,
                        SourceModule: sourceModule,
                        ReplyToEmail: replyToEmail),
                    ct);
            }
            catch (Exception ex) when (ex is not OperationCanceledException || !ct.IsCancellationRequested)
            {
                // A group that threw is that group's failure; the next language is still written to.
                _logger.LogWarning(
                    ex,
                    "notification.internal_dispatch_group_failed TenantId={TenantId} EventCode={EventCode} Locale={Locale}",
                    request.TenantId, eventCode, locale);
                response = Response<NotificationDispatchDto>.Fail(
                    "The dispatch of one language group failed.", IsValidationRefusal(ex) ? 400 : 500,
                    NotificationEventDispatchAdapter.ReasonGroupDispatchFailed);
            }

            var retryPending = !response.IsSuccessful && string.Equals(
                response.ReasonCode,
                Handlers.CommandHandlers.QueueEmailNotificationHandler.ReasonProviderRejectedRetryPending,
                StringComparison.Ordinal);

            if (response.IsSuccessful || retryPending)
            {
                // Accepted: its row exists and the copies went with it (a retry-pending row carries them on its retry).
                copiesPending = false;
                items.Add(new InternalEventDispatchItemDto(
                    response.Data?.Id,
                    locale,
                    retryPending ? StatusQueued : (response.Data?.Status ?? StatusQueued).ToLowerInvariant(),
                    retryPending ? response.ReasonCode : null));
            }
            else
            {
                firstRefusal ??= response;
                items.Add(new InternalEventDispatchItemDto(null, locale, StatusFailed, response.ReasonCode));
            }
        }

        var accepted = items.Count(i => i.Status != StatusFailed);
        if (accepted == 0)
        {
            // Nothing was accepted: the refusal is the answer, with its own status — a caller retries a 5xx, not a 4xx.
            return Response<InternalEventDispatchResultDto>.Fail(
                firstRefusal!.Errors.Count > 0 ? firstRefusal.Errors.ToList() : ["The notification was not accepted."],
                firstRefusal.StatusCode >= 400 ? firstRefusal.StatusCode : 500,
                firstRefusal.ReasonCode);
        }

        var status = accepted < items.Count
            ? StatusPartial
            : items.All(i => i.Status == StatusSent) ? StatusSent : StatusQueued;

        return Response<InternalEventDispatchResultDto>.Success(new InternalEventDispatchResultDto(items, resolved, status), 200);
    }

    /// <summary>The asking module owns the event: it is the owner, or the module listed as speaking for it.</summary>
    public static bool IsOwnedBy(string? ownerModuleId, string sourceModule)
    {
        if (string.IsNullOrWhiteSpace(ownerModuleId) || string.IsNullOrWhiteSpace(sourceModule))
        {
            return false;
        }

        var owner = ownerModuleId.Trim();
        return string.Equals(owner, sourceModule, StringComparison.OrdinalIgnoreCase)
               || (ProducerOwners.TryGetValue(sourceModule, out var spokenFor)
                   && string.Equals(owner, spokenFor, StringComparison.OrdinalIgnoreCase));
    }

    private async Task<string?> ReplyToAsync(InternalEventReplyTo? replyTo, CancellationToken ct)
    {
        if (replyTo?.UserId is not { } userId)
        {
            return null;
        }

        var contact = (await _recipients.ResolveAsync([userId], ct)).FirstOrDefault(c => c.UserId == userId);
        if (contact is null || !Diten.BuildingBlocks.Email.EmailAddressText.IsSingleAddress(contact.Email?.Trim()))
        {
            _logger.LogInformation(
                "notification.internal_dispatch_reply_to_unresolved UserId={UserId}; the tenant's reply address applies.", userId);
            return null;
        }

        return contact.Email.Trim();
    }

    private sealed record Reader(Guid? UserId, string Via, string Email, string? DisplayName, string? Locale);

    private async Task<List<Reader>> ResolveAsync(
        Guid tenantId, IReadOnlyList<InternalEventRecipient>? entries, CancellationToken ct)
    {
        var readers = new List<Reader>();
        if (entries is null || entries.Count == 0)
        {
            return readers;
        }

        // Users first, each with how they were found; the first way a user was named wins.
        var wanted = new List<(Guid UserId, string Via)>();
        foreach (var entry in entries)
        {
            if (entry.UserId is { } userId)
            {
                wanted.Add((userId, ViaDirect));
            }
            else if (entry.LineManagerOf is { } of)
            {
                var managers = await _lineManagers.ResolveAsync(tenantId, of, ct);
                var via = managers.Resolution == LineManagerResolution.ManagerChain ? ViaManagerChain : ViaLineManager;
                wanted.AddRange(managers.UserIds.Select(id => (id, via)));
            }
        }

        var contacts = wanted.Count == 0
            ? []
            : (await _recipients.ResolveAsync(wanted.Select(w => w.UserId).Distinct().ToList(), ct))
                .GroupBy(c => c.UserId)
                .ToDictionary(g => g.Key, g => g.First());

        var seen = new HashSet<string>(StringComparer.Ordinal);
        foreach (var (userId, via) in wanted)
        {
            if (contacts.TryGetValue(userId, out var contact) && seen.Add(NotificationParsing.NormalizeEmail(contact.Email)))
            {
                readers.Add(new Reader(userId, via, contact.Email, contact.DisplayName, contact.PreferredLanguage));
            }
        }

        foreach (var entry in entries.Where(e => !string.IsNullOrWhiteSpace(e.Email)))
        {
            var email = entry.Email!.Trim();
            if (seen.Add(NotificationParsing.NormalizeEmail(email)))
            {
                readers.Add(new Reader(null, ViaEmail, email, string.IsNullOrWhiteSpace(entry.DisplayName) ? null : entry.DisplayName.Trim(), entry.Locale));
            }
        }

        return readers;
    }

    private static string? ShapeError(InternalEventDispatchRequest? request)
    {
        if (request is null)
        {
            return "A body is required.";
        }

        if (request.TenantId == Guid.Empty)
        {
            return "tenantId is required.";
        }

        if (string.IsNullOrWhiteSpace(request.EventCode) || !NotificationParsing.IsValidTemplateKey(request.EventCode.Trim().ToLowerInvariant()))
        {
            return "eventCode is required and must be a catalogue code.";
        }

        if (string.IsNullOrWhiteSpace(request.SourceModule))
        {
            return "sourceModule is required.";
        }

        var entries = (request.Recipients?.To ?? []).Concat(request.Recipients?.Cc ?? []).ToList();
        if (entries.Count == 0)
        {
            return "At least one recipient is required.";
        }

        if (entries.Count > MaxRecipientEntries)
        {
            return $"At most {MaxRecipientEntries} recipients may be named.";
        }

        foreach (var entry in entries)
        {
            if (entry is null)
            {
                return "A recipient is empty.";
            }

            var named = (entry.UserId is not null ? 1 : 0) + (entry.LineManagerOf is not null ? 1 : 0)
                        + (string.IsNullOrWhiteSpace(entry.Email) ? 0 : 1);
            if (named != 1)
            {
                return "Each recipient names exactly one of userId, lineManagerOf or email.";
            }

            if (entry.UserId == Guid.Empty || entry.LineManagerOf == Guid.Empty)
            {
                return "A recipient's user id is empty.";
            }
        }

        if (request.ReplyTo is { } replyTo && (replyTo.UserId is null || replyTo.UserId == Guid.Empty))
        {
            return "replyTo names a user id.";
        }

        return null;
    }

    private static string? SupportedLocaleOrNull(string? locale)
    {
        if (string.IsNullOrWhiteSpace(locale))
        {
            return null;
        }

        var normalized = NotificationParsing.NormalizeLocale(locale);
        var separator = normalized.IndexOfAny(['-', '_']);
        var primary = separator > 0 ? normalized[..separator] : normalized;
        return NotificationEventDispatchAdapter.RecipientLocales.Contains(primary) ? primary : null;
    }

    private static bool IsValidationRefusal(Exception ex) => ex is FluentValidation.ValidationException;
}
