using Diten.Platform.Application.Common;
using Diten.Platform.Application.Contracts;
using Diten.Platform.Application.Contracts.DocumentRepository;
using Diten.Platform.Common.Tenancy;
using Diten.Platform.Domain.Entities;
using Diten.Platform.Domain.Entities.Notifications;
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

/// <summary>WP-VW-W6 PLT-W6 (MOD-0168 AÇIK-4) — a file the mail carries (the depot / pharmacy copy of a pre-order as PDF):
/// exactly one of <paramref name="ContentBase64"/> (the bytes) or <paramref name="DocumentRef"/> (a MOD-0262 content id of
/// the SAME tenant). Bounded by the platform's persisted-attachment budget so a retry carries it too.</summary>
public sealed record InternalEventAttachment(
    string? FileName,
    string? ContentType,
    string? ContentBase64 = null,
    Guid? DocumentRef = null);

public sealed record InternalEventRecipients(
    IReadOnlyList<InternalEventRecipient>? To = null,
    IReadOnlyList<InternalEventRecipient>? Cc = null);

/// <summary>
/// WP-VW-W5 PLT-NOTIFY (K-N1) — what a service or a scheduler sends to <c>POST api/internal/notifications/events/dispatch</c>.
/// There is no request context behind it: the tenant comes from <paramref name="TenantId"/>, the asking module from
/// <paramref name="SourceModule"/>. Variables are display text — the producer formats dates and names.
/// <para>W5-FIX2 (F-1) — <paramref name="IdempotencyKey"/> (optional, at most
/// <see cref="InternalEventDispatchService.MaxIdempotencyKeyLength"/> characters, scoped by the tenant and
/// <paramref name="SourceModule"/>): a second call with the same key opens no dispatch and answers what the first one
/// answered. CRM sends its notification row id, so a retry after a time-out never mails the same report twice.</para>
/// </summary>
public sealed record InternalEventDispatchRequest(
    Guid TenantId,
    string? EventCode,
    string? SourceModule,
    InternalEventRecipients? Recipients,
    IReadOnlyDictionary<string, string?>? Variables,
    string? CorrelationId,
    Guid? CausationId = null,
    InternalEventReplyTo? ReplyTo = null,
    // WP-VW-W6 PLT-W6 (AÇIK-4) — optional files; never echoed in the answer
    IReadOnlyList<InternalEventAttachment>? Attachments = null,
    string? IdempotencyKey = null);

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
/// <para>W5-FIX2 (F-1) — the send is NOT part of the call: every mail is written <c>Queued</c> and the platform's retry
/// sweep sends it (<see cref="QueueEmailNotificationRequest.QueueOnly"/>), so the answer is <c>queued</c> and a slow mail
/// server can no longer push the caller past its time budget. With an idempotency key the call is claimed before anything
/// is written (after the tenant and owner checks, so a module cannot probe another module's keys): a repeat answers the
/// stored result, a concurrent repeat waits briefly for it and otherwise answers 409 (try again later), and a call that had
/// nothing accepted releases its key.</para>
/// </summary>
public sealed class InternalEventDispatchService : IInternalEventDispatchService
{
    public const string ReasonBadRequest = "INTERNAL_DISPATCH_BAD_REQUEST";
    public const string ReasonEventForbidden = "EVENT_NOT_OWNED_OR_NOT_ACTIVE";
    public const string ReasonAttachmentInvalid = "INTERNAL_DISPATCH_ATTACHMENT_INVALID";

    /// <summary>WP-VW-W6 PLT-W6 — the most files one call may carry.</summary>
    public const int MaxAttachments = 5;

    /// <summary>All files of one call together: the platform's persisted-attachment budget, so the stored dispatch keeps
    /// them and its retry sends the same files (BL-374). Above it: 400, nothing sent.</summary>
    public const int MaxAttachmentBytes = Handlers.CommandHandlers.QueueEmailNotificationHandler.MaxPersistedAttachmentBytes;

    public const int MaxAttachmentFileNameLength = 200;

    /// <summary>The file types a service may attach (a document copy or its picture) — nothing executable, nothing
    /// archive-shaped.</summary>
    public static readonly IReadOnlySet<string> AllowedAttachmentTypes = new HashSet<string>(StringComparer.OrdinalIgnoreCase)
    {
        "application/pdf", "image/png", "image/jpeg"
    };

    public const string ReasonInProgress = "INTERNAL_DISPATCH_IN_PROGRESS";

    /// <summary>W5-FIX2 — the longest idempotency key a caller may send.</summary>
    public const int MaxIdempotencyKeyLength = 128;

    /// <summary>W5-FIX2 — a claim left pending this long (its call died) is taken over by the next call with the key.</summary>
    public static readonly TimeSpan StaleClaimAfter = TimeSpan.FromMinutes(2);

    /// <summary>W5-FIX2 — how long a concurrent repeat waits for the first call's answer (under CRM's 3 s budget).</summary>
    public static readonly TimeSpan InFlightWait = TimeSpan.FromSeconds(2);

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
    private readonly IInternalDispatchKeyRepository _keys;
    private readonly ILogger<InternalEventDispatchService> _logger;
    private readonly IRepositoryObjectRepository? _documents;
    private readonly IContentStorageGateway? _storage;

    public InternalEventDispatchService(
        ITenantRegistryRepository tenants,
        INotificationEventDefinitionRepository events,
        ITenantContext tenantContext,
        ILineManagerResolver lineManagers,
        ITaskNotificationRecipientResolver recipients,
        INotificationLocaleResolver locales,
        INotificationEventDispatchAdapter adapter,
        IInternalDispatchKeyRepository keys,
        ILogger<InternalEventDispatchService> logger,
        // WP-VW-W6 PLT-W6 — a documentRef attachment is read from the MOD-0262 repository (tenant-filtered)
        IRepositoryObjectRepository? documents = null,
        IContentStorageGateway? storage = null)
    {
        _keys = keys;
        _documents = documents;
        _storage = storage;
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

        var idempotencyKey = request.IdempotencyKey?.Trim();
        if (string.IsNullOrEmpty(idempotencyKey))
        {
            return await ResolveAndDispatchAsync(request, eventCode, sourceModule, ct);
        }

        // W5-FIX2 (F-1) — one dispatch per key: claimed before anything is written.
        var module = sourceModule.ToUpperInvariant();
        var claim = await _keys.ClaimAsync(module, idempotencyKey, DateTimeOffset.UtcNow, StaleClaimAfter, ct);
        if (claim.Outcome != InternalDispatchKeyOutcome.Claimed)
        {
            return await ReplayAsync(claim, module, idempotencyKey, eventCode, ct);
        }

        Response<InternalEventDispatchResultDto> response;
        try
        {
            response = await ResolveAndDispatchAsync(request, eventCode, sourceModule, ct);
        }
        catch
        {
            await ReleaseQuietlyAsync(claim.Record, eventCode);
            throw;
        }

        if (response.IsSuccessful && response.Data is not null)
        {
            await _keys.CompleteAsync(
                claim.Record.Id, claim.Record.ClaimToken,
                System.Text.Json.JsonSerializer.Serialize(response.Data, ResultJson), DateTimeOffset.UtcNow, CancellationToken.None);
        }
        else
        {
            // Nothing was accepted: the key goes back, so the caller's retry is a real second try.
            await ReleaseQuietlyAsync(claim.Record, eventCode);
        }

        return response;
    }

    private static readonly System.Text.Json.JsonSerializerOptions ResultJson = new(System.Text.Json.JsonSerializerDefaults.Web);

    /// <summary>The key is held: the stored answer, or — while the first call runs — a short wait for it, then 409.</summary>
    private async Task<Response<InternalEventDispatchResultDto>> ReplayAsync(
        InternalDispatchKeyClaim claim, string module, string key, string eventCode, CancellationToken ct)
    {
        var record = claim.Record;
        var waitUntil = DateTimeOffset.UtcNow + InFlightWait;
        while (record.State != InternalDispatchKeyStates.Done && DateTimeOffset.UtcNow < waitUntil)
        {
            await Task.Delay(100, ct);
            record = await _keys.GetAsync(module, key, ct) ?? record;
        }

        if (record.State == InternalDispatchKeyStates.Done && record.ResultJson is { Length: > 0 } json
            && System.Text.Json.JsonSerializer.Deserialize<InternalEventDispatchResultDto>(json, ResultJson) is { } stored)
        {
            _logger.LogInformation(
                "notification.internal_dispatch_replayed EventCode={EventCode} SourceModule={SourceModule} Status={Status}",
                eventCode, module, stored.Status);
            return Response<InternalEventDispatchResultDto>.Success(stored, 200);
        }

        _logger.LogInformation(
            "notification.internal_dispatch_in_progress EventCode={EventCode} SourceModule={SourceModule}", eventCode, module);
        return Response<InternalEventDispatchResultDto>.Fail(
            "The same request is still being handled; try again later.", 409, ReasonInProgress);
    }

    private async Task ReleaseQuietlyAsync(InternalDispatchKey record, string eventCode)
    {
        try
        {
            await _keys.ReleaseAsync(record.Id, record.ClaimToken, DateTimeOffset.UtcNow, CancellationToken.None);
        }
        catch (Exception ex)
        {
            // A claim that could not be released is taken over once it is stale.
            _logger.LogWarning(ex, "notification.internal_dispatch_key_release_failed EventCode={EventCode}", eventCode);
        }
    }

    private async Task<Response<InternalEventDispatchResultDto>> ResolveAndDispatchAsync(
        InternalEventDispatchRequest request, string eventCode, string sourceModule, CancellationToken ct)
    {
        // 3b) WP-VW-W6 PLT-W6 — the files: read (a documentRef only in this tenant), the whole set within the budget; a
        //     refusal sends nothing.
        var (attachments, attachmentError) = await AttachmentsAsync(request.Attachments, ct);
        if (attachmentError is not null)
        {
            return Response<InternalEventDispatchResultDto>.Fail(attachmentError, 400, ReasonAttachmentInvalid);
        }

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
                        ReplyToEmail: replyToEmail,
                        Attachments: attachments,
                        // W5-FIX2 (F-1) — written and answered; the background sweep sends.
                        QueueOnly: true),
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

        if (request.IdempotencyKey is { } key
            && (string.IsNullOrWhiteSpace(key) || key.Trim().Length > MaxIdempotencyKeyLength || key.Any(char.IsControl)))
        {
            return $"idempotencyKey is 1 to {MaxIdempotencyKeyLength} printable characters.";
        }

        return AttachmentShapeError(request.Attachments);
    }

    /// <summary>WP-VW-W6 PLT-W6 — each file: a safe name, an allowed type, exactly one source, decodable bytes; the inline
    /// bytes already within the budget (a documentRef's size is checked when it is read).</summary>
    private static string? AttachmentShapeError(IReadOnlyList<InternalEventAttachment>? attachments)
    {
        if (attachments is null || attachments.Count == 0)
        {
            return null;
        }

        if (attachments.Count > MaxAttachments)
        {
            return $"At most {MaxAttachments} attachments may be sent.";
        }

        long inline = 0;
        foreach (var attachment in attachments)
        {
            if (attachment is null)
            {
                return "An attachment is empty.";
            }

            var name = attachment.FileName?.Trim();
            if (string.IsNullOrEmpty(name) || name.Length > MaxAttachmentFileNameLength
                || name.IndexOfAny(['/', '\\', ':', '*', '?', '"', '<', '>', '|']) >= 0 || name.Any(char.IsControl))
            {
                return $"An attachment needs a plain file name (≤ {MaxAttachmentFileNameLength}, no path).";
            }

            if (string.IsNullOrWhiteSpace(attachment.ContentType) || !AllowedAttachmentTypes.Contains(attachment.ContentType.Trim()))
            {
                return $"An attachment's type must be one of {string.Join(", ", AllowedAttachmentTypes)}.";
            }

            var sources = (string.IsNullOrWhiteSpace(attachment.ContentBase64) ? 0 : 1) + (attachment.DocumentRef is null ? 0 : 1);
            if (sources != 1 || attachment.DocumentRef == Guid.Empty)
            {
                return "Each attachment carries exactly one of contentBase64 or documentRef.";
            }

            if (attachment.ContentBase64 is { } base64 && !string.IsNullOrWhiteSpace(base64))
            {
                var bytes = DecodeOrNull(base64);
                if (bytes is null || bytes.Length == 0)
                {
                    return "An attachment's contentBase64 is not valid base64.";
                }

                inline += bytes.Length;
            }
        }

        return inline > MaxAttachmentBytes
            ? $"The attachments may total at most {MaxAttachmentBytes} bytes."
            : null;
    }

    /// <summary>The files as the mail carries them; a documentRef read in the pinned tenant only (another tenant's id is
    /// "not found"). Over the budget in total → refused.</summary>
    private async Task<(IReadOnlyList<MessagingProviderAttachment>? Files, string? Error)> AttachmentsAsync(
        IReadOnlyList<InternalEventAttachment>? attachments, CancellationToken ct)
    {
        if (attachments is null || attachments.Count == 0)
        {
            return (null, null);
        }

        var files = new List<MessagingProviderAttachment>(attachments.Count);
        long total = 0;
        foreach (var attachment in attachments)
        {
            byte[]? bytes;
            if (attachment.DocumentRef is { } contentId)
            {
                var (document, tooBig) = await ReadDocumentAsync(contentId, ct);
                if (tooBig)
                {
                    return (null, $"The attachments may total at most {MaxAttachmentBytes} bytes.");
                }

                if (document is null)
                {
                    return (null, "An attachment's documentRef was not found.");
                }

                bytes = document;
            }
            else
            {
                bytes = DecodeOrNull(attachment.ContentBase64!)!;
            }

            total += bytes.Length;
            if (total > MaxAttachmentBytes)
            {
                return (null, $"The attachments may total at most {MaxAttachmentBytes} bytes.");
            }

            files.Add(new MessagingProviderAttachment(attachment.FileName!.Trim(), attachment.ContentType!.Trim().ToLowerInvariant(), bytes));
        }

        return (files, null);
    }

    /// <summary>The document's bytes (null = not found in this tenant); <c>TooBig</c> = over the budget, never read whole.</summary>
    private async Task<(byte[]? Bytes, bool TooBig)> ReadDocumentAsync(Guid contentId, CancellationToken ct)
    {
        if (_documents is null || _storage is null)
        {
            return (null, false);
        }

        var row = await _documents.GetByContentIdAsync(contentId, ct);
        if (row is null || row.TenantId != _tenantContext.TenantId || row.IsDeleted)
        {
            return (null, false);
        }

        if (row.ByteSize > MaxAttachmentBytes)
        {
            return (null, true);
        }

        var opened = await _storage.OpenReadAsync(row.StorageProvider, row.ObjectKey, ct);
        if (!opened.IsSuccessful || opened.Data is null)
        {
            return (null, false);
        }

        await using var content = opened.Data.Content;
        using var buffer = new MemoryStream();
        var chunk = new byte[81920];
        int read;
        while ((read = await content.ReadAsync(chunk, ct)) > 0)
        {
            buffer.Write(chunk, 0, read);
            if (buffer.Length > MaxAttachmentBytes)
            {
                return (null, true); // the stored size lied: never read a bigger file whole
            }
        }

        return (buffer.ToArray(), false);
    }

    private static byte[]? DecodeOrNull(string base64)
    {
        try
        {
            return Convert.FromBase64String(base64.Trim());
        }
        catch (FormatException)
        {
            return null;
        }
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
