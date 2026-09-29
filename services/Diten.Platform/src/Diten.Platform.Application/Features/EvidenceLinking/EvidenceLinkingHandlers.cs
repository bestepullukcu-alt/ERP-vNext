using System.Security.Cryptography;
using System.Text;
using Diten.BuildingBlocks.Eventing;
using Diten.Platform.Application.Common;
using Diten.Platform.Application.Contracts;
using Diten.Platform.Application.Contracts.Eventing;
using Diten.Platform.Application.Features.BusinessReferenceData.Services;
using Diten.Platform.Application.Features.EvidenceLinking.Events;
using Diten.Platform.Application.Features.EvidenceLinking.Services;
using Diten.Platform.Common.Tenancy;
using Diten.Platform.Domain.Entities.DocumentManagement;
using Diten.Platform.Domain.Entities.EvidenceLinking;
using Diten.Platform.Domain.Repositories;
using MediatR;

namespace Diten.Platform.Application.Features.EvidenceLinking;

// MOD-0031 slice 1 — evidence-link handlers. Document access is ALWAYS delegated to IEvidenceDocumentAccessGate (the
// MOD-0029 evaluator); DocMgmt entities are read, never written. Writes run in one Platform transaction with their
// outbox event, so a link and its event commit together or not at all.

internal static class EvidenceLinkRules
{
    public static string? Clean(string? value) => string.IsNullOrWhiteSpace(value) ? null : value.Trim();

    public static string Key(string? value) => (value ?? string.Empty).Trim().ToLowerInvariant();

    /// <summary>Object + document + version + page + quote → the active-uniqueness key (SHA-256, hex).</summary>
    public static string DedupKey(EvidenceObjectRef o, Guid documentId, Guid? versionId, string? page, string quote)
    {
        var raw = string.Join('\u001f', o.Module, o.ObjectType, o.ObjectId, o.ObjectVersion ?? string.Empty,
            documentId.ToString("D"), versionId?.ToString("D") ?? string.Empty,
            (page ?? string.Empty).Trim().ToLowerInvariant(), quote.Trim());
        return Convert.ToHexString(SHA256.HashData(Encoding.UTF8.GetBytes(raw)));
    }

    public static Response<T> Invalid<T>(string message, string? correlationId,
        string reasonCode = EvidenceLinkReasonCodes.ValidationFailed)
        => Response<T>.Fail(message, 400, reasonCode, correlationId);

    public static Response<T> NoTenant<T>(string? correlationId)
        => Response<T>.Fail("Tenant context is required.", 400, EvidenceLinkReasonCodes.ValidationFailed, correlationId);

    public static string? TooLong(string? value, int max, string field)
        => value is not null && value.Length > max ? $"{field} cannot exceed {max} characters." : null;
}

public sealed class CreateEvidenceLinkCommandHandler
    : IRequestHandler<CreateEvidenceLinkCommand, Response<EvidenceLinkDto>>
{
    private readonly ITenantContext _tenant;
    private readonly ICurrentUserContext _user;
    private readonly IEvidenceLinkRepository _links;
    private readonly IControlledDocumentRepository _documents;
    private readonly IControlledDocumentVersionRepository _versions;
    private readonly IExternalDocumentRegisterRepository _externalDocuments;
    private readonly IEvidenceDocumentAccessGate _access;
    private readonly IBusinessReferenceDataActiveMembershipService _referenceData;
    private readonly IPlatformTransactionExecutor _transactions;
    private readonly ITransactionalIntegrationEventWriter _events;

    public CreateEvidenceLinkCommandHandler(
        ITenantContext tenant,
        ICurrentUserContext user,
        IEvidenceLinkRepository links,
        IControlledDocumentRepository documents,
        IControlledDocumentVersionRepository versions,
        IExternalDocumentRegisterRepository externalDocuments,
        IEvidenceDocumentAccessGate access,
        IBusinessReferenceDataActiveMembershipService referenceData,
        IPlatformTransactionExecutor transactions,
        ITransactionalIntegrationEventWriter events)
    {
        _tenant = tenant;
        _user = user;
        _links = links;
        _documents = documents;
        _versions = versions;
        _externalDocuments = externalDocuments;
        _access = access;
        _referenceData = referenceData;
        _transactions = transactions;
        _events = events;
    }

    public async Task<Response<EvidenceLinkDto>> Handle(CreateEvidenceLinkCommand request, CancellationToken ct)
    {
        var cid = request.CorrelationId;
        if (!_tenant.IsResolved || _tenant.TenantId == Guid.Empty)
        {
            return EvidenceLinkRules.NoTenant<EvidenceLinkDto>(cid);
        }

        // ── shape ────────────────────────────────────────────────────────────────────────────────────────────
        var o = request.ObjectRef;
        var module = EvidenceLinkRules.Key(o?.Module);
        var objectType = EvidenceLinkRules.Key(o?.ObjectType);
        var objectId = (o?.ObjectId ?? string.Empty).Trim();
        var objectVersion = EvidenceLinkRules.Clean(o?.ObjectVersion);
        if (module.Length == 0 || objectType.Length == 0 || objectId.Length == 0)
        {
            return EvidenceLinkRules.Invalid<EvidenceLinkDto>("ObjectRef.Module, ObjectType and ObjectId are required.", cid);
        }

        var quote = EvidenceLinkRules.Clean(request.Locator?.Quote);
        if (quote is null)
        {
            return EvidenceLinkRules.Invalid<EvidenceLinkDto>("Locator.Quote is required.", cid,
                EvidenceLinkReasonCodes.QuoteRequired);
        }

        var section = EvidenceLinkRules.Clean(request.Locator?.Section);
        var page = EvidenceLinkRules.Clean(request.Locator?.Page);
        var table = EvidenceLinkRules.Clean(request.Locator?.Table);
        var lengthError = EvidenceLinkRules.TooLong(module, EvidenceLinkLimits.Module, "ObjectRef.Module")
            ?? EvidenceLinkRules.TooLong(objectType, EvidenceLinkLimits.ObjectType, "ObjectRef.ObjectType")
            ?? EvidenceLinkRules.TooLong(objectId, EvidenceLinkLimits.ObjectId, "ObjectRef.ObjectId")
            ?? EvidenceLinkRules.TooLong(objectVersion, EvidenceLinkLimits.ObjectVersion, "ObjectRef.ObjectVersion")
            ?? EvidenceLinkRules.TooLong(section, EvidenceLinkLimits.Section, "Locator.Section")
            ?? EvidenceLinkRules.TooLong(page, EvidenceLinkLimits.Page, "Locator.Page")
            ?? EvidenceLinkRules.TooLong(table, EvidenceLinkLimits.Table, "Locator.Table")
            ?? EvidenceLinkRules.TooLong(quote, EvidenceLinkLimits.Quote, "Locator.Quote");
        if (lengthError is not null)
        {
            return EvidenceLinkRules.Invalid<EvidenceLinkDto>(lengthError, cid);
        }

        var spanInputs = request.SupportedSpans ?? [];
        if (spanInputs.Count > EvidenceLinkLimits.MaxSpans)
        {
            return EvidenceLinkRules.Invalid<EvidenceLinkDto>(
                $"At most {EvidenceLinkLimits.MaxSpans} supported spans are allowed.", cid);
        }

        var spans = new List<EvidenceSupportedSpan>();
        foreach (var s in spanInputs)
        {
            var language = EvidenceLinkRules.Key(s?.LanguageCode);
            var text = EvidenceLinkRules.Clean(s?.Text);
            if (language.Length == 0 || text is null)
            {
                return EvidenceLinkRules.Invalid<EvidenceLinkDto>("Each supported span needs LanguageCode and Text.", cid);
            }

            var spanError = EvidenceLinkRules.TooLong(language, EvidenceLinkLimits.SpanLanguage, "SupportedSpans.LanguageCode")
                ?? EvidenceLinkRules.TooLong(text, EvidenceLinkLimits.SpanText, "SupportedSpans.Text");
            if (spanError is not null)
            {
                return EvidenceLinkRules.Invalid<EvidenceLinkDto>(spanError, cid);
            }

            if (s!.Start is < 0 || s.End is < 0 || (s.Start is { } start && s.End is { } end && end < start))
            {
                return EvidenceLinkRules.Invalid<EvidenceLinkDto>("SupportedSpans Start/End must be ≥ 0 and End ≥ Start.", cid);
            }

            spans.Add(new EvidenceSupportedSpan { LanguageCode = language, Text = text, Start = s.Start, End = s.End });
        }

        var kind = EvidenceLinkRules.Key(request.DocumentKind);
        if (!EvidenceDocumentKinds.IsValid(kind))
        {
            return EvidenceLinkRules.Invalid<EvidenceLinkDto>("DocumentKind must be 'controlled' or 'external'.", cid);
        }

        if (request.DocumentId == Guid.Empty)
        {
            return EvidenceLinkRules.Invalid<EvidenceLinkDto>("DocumentId is required.", cid);
        }

        // ── document (tenant-scoped repositories; read-only) + caller's read access ─────────────────────────────
        string title;
        string? versionLabel;
        Guid? versionId = null;
        if (kind == EvidenceDocumentKinds.Controlled)
        {
            if (request.DocumentVersionId is not { } requestedVersion || requestedVersion == Guid.Empty)
            {
                return EvidenceLinkRules.Invalid<EvidenceLinkDto>(
                    "DocumentVersionId is required for a controlled document (evidence pins a version).", cid,
                    EvidenceLinkReasonCodes.VersionRequired);
            }

            var document = await _documents.GetByIdAsync(request.DocumentId, ct);
            if (document is null || document.DeletedAt is not null)
            {
                return Response<EvidenceLinkDto>.Fail("Document not found.", 404, EvidenceLinkReasonCodes.DocumentNotFound, cid);
            }

            if (!await _access.CanReadControlledAsync(document, ct))
            {
                return Response<EvidenceLinkDto>.Fail("You cannot read this document.", 403,
                    EvidenceLinkReasonCodes.DocumentNotReadable, cid);
            }

            var version = await _versions.GetByIdAsync(requestedVersion, ct);
            if (version is null)
            {
                return EvidenceLinkRules.Invalid<EvidenceLinkDto>("Document version not found.", cid,
                    EvidenceLinkReasonCodes.VersionNotFound);
            }

            if (version.DocumentId != document.Id)
            {
                return EvidenceLinkRules.Invalid<EvidenceLinkDto>("The version does not belong to this document.", cid,
                    EvidenceLinkReasonCodes.VersionDocumentMismatch);
            }

            if (version.DeletedAt is not null || version.IsDeleted)
            {
                return EvidenceLinkRules.Invalid<EvidenceLinkDto>("The document version is deleted.", cid,
                    EvidenceLinkReasonCodes.VersionDeleted);
            }

            title = document.Title;
            versionLabel = $"v{version.VersionNumber}";
            versionId = version.Id;
        }
        else
        {
            if (request.DocumentVersionId is { } v && v != Guid.Empty)
            {
                return EvidenceLinkRules.Invalid<EvidenceLinkDto>(
                    "An external document has no version id; its SourceVersion is snapshotted.", cid,
                    EvidenceLinkReasonCodes.VersionNotAllowed);
            }

            var entry = await _externalDocuments.GetByIdAsync(request.DocumentId, ct);
            if (entry is null || entry.DeletedAt is not null)
            {
                return Response<EvidenceLinkDto>.Fail("Document not found.", 404, EvidenceLinkReasonCodes.DocumentNotFound, cid);
            }

            if (!await _access.CanReadExternalAsync(entry, ct))
            {
                return Response<EvidenceLinkDto>.Fail("You cannot read this document.", 403,
                    EvidenceLinkReasonCodes.DocumentNotReadable, cid);
            }

            title = entry.ExternalDocumentTitle;
            versionLabel = entry.SourceVersion;
        }

        // ── evidence type (Global BRD set; a missing set is fail-closed, never a silent accept) ─────────────────
        var evidenceType = EvidenceLinkRules.Key(request.EvidenceTypeCode);
        if (evidenceType.Length == 0)
        {
            return EvidenceLinkRules.Invalid<EvidenceLinkDto>("EvidenceTypeCode is required.", cid,
                EvidenceLinkReasonCodes.InvalidEvidenceType);
        }

        var membership = await _referenceData.ValidateActiveValueAsync(EvidenceReferenceSets.EvidenceType, evidenceType, ct);
        if (!membership.IsActive)
        {
            return membership.ReasonCode is "reference_value_not_active" or "reference_value_required"
                ? EvidenceLinkRules.Invalid<EvidenceLinkDto>(
                    $"'{evidenceType}' is not an active value of '{EvidenceReferenceSets.EvidenceType}'.", cid,
                    EvidenceLinkReasonCodes.InvalidEvidenceType)
                : EvidenceLinkRules.Invalid<EvidenceLinkDto>(
                    $"Reference set '{EvidenceReferenceSets.EvidenceType}' is not available ({membership.ReasonCode}).", cid,
                    EvidenceLinkReasonCodes.ReferenceSetMissing);
        }

        // ── duplicate (friendly pre-check; the partial unique index is the guarantee) ────────────────────────────
        var objectRef = new EvidenceObjectRef
        {
            Module = module, ObjectType = objectType, ObjectId = objectId, ObjectVersion = objectVersion
        };
        var dedupKey = EvidenceLinkRules.DedupKey(objectRef, request.DocumentId, versionId, page, quote);
        if (await _links.FindActiveByDedupKeyAsync(dedupKey, ct) is not null)
        {
            return Response<EvidenceLinkDto>.Fail("An identical active evidence link already exists.", 409,
                EvidenceLinkReasonCodes.DuplicateLink, cid);
        }

        var now = DateTimeOffset.UtcNow;
        var actorId = _user.UserId == Guid.Empty ? (Guid?)null : _user.UserId;
        var link = new EvidenceLink
        {
            TenantId = _tenant.TenantId,
            CreatedBy = _user.ActorName,
            ObjectRef = objectRef,
            DocumentKind = kind,
            DocumentId = request.DocumentId,
            DocumentVersionId = versionId,
            DocumentVersionLabel = versionLabel,
            DocumentTitle = title,
            EvidenceTypeCode = evidenceType,
            Locator = new EvidenceLocator { Section = section, Page = page, Table = table, Quote = quote },
            SupportedSpans = spans,
            Status = EvidenceLinkStatuses.Active,
            ActiveDedupKey = dedupKey,
            LinkedAt = now,
            LinkedBy = _user.ActorName,
            LinkedByUserId = actorId
        };

        try
        {
            await _transactions.ExecuteAsync(async (session, tct) =>
            {
                await _links.InsertAsync(session, link, tct);
                await EvidenceLinkEventPublisher.EnqueueCreatedAsync(_events, session, link, actorId, cid, tct);
                return true;
            }, ct);
        }
        catch (EvidenceLinkDuplicateException)
        {
            return Response<EvidenceLinkDto>.Fail("An identical active evidence link already exists.", 409,
                EvidenceLinkReasonCodes.DuplicateLink, cid);
        }
        catch (PlatformTransactionUnavailableException)
        {
            return Response<EvidenceLinkDto>.Fail("The link could not be saved. Nothing was written.", 503,
                EvidenceLinkReasonCodes.TransactionUnavailable, cid);
        }

        return Response<EvidenceLinkDto>.Success(EvidenceLinkMapper.ToDto(link), 201, cid);
    }
}

public sealed class RemoveEvidenceLinkCommandHandler
    : IRequestHandler<RemoveEvidenceLinkCommand, Response<EvidenceLinkDto>>
{
    private readonly ITenantContext _tenant;
    private readonly ICurrentUserContext _user;
    private readonly IEvidenceLinkRepository _links;
    private readonly IPlatformTransactionExecutor _transactions;
    private readonly ITransactionalIntegrationEventWriter _events;

    public RemoveEvidenceLinkCommandHandler(
        ITenantContext tenant, ICurrentUserContext user, IEvidenceLinkRepository links,
        IPlatformTransactionExecutor transactions, ITransactionalIntegrationEventWriter events)
    {
        _tenant = tenant;
        _user = user;
        _links = links;
        _transactions = transactions;
        _events = events;
    }

    public async Task<Response<EvidenceLinkDto>> Handle(RemoveEvidenceLinkCommand request, CancellationToken ct)
    {
        var cid = request.CorrelationId;
        if (!_tenant.IsResolved || _tenant.TenantId == Guid.Empty)
        {
            return EvidenceLinkRules.NoTenant<EvidenceLinkDto>(cid);
        }

        var reason = EvidenceLinkRules.Clean(request.Reason);
        if (reason is null)
        {
            return EvidenceLinkRules.Invalid<EvidenceLinkDto>("A removal reason is required.", cid,
                EvidenceLinkReasonCodes.RemovalReasonRequired);
        }

        if (reason.Length > EvidenceLinkLimits.RemovalReason)
        {
            return EvidenceLinkRules.Invalid<EvidenceLinkDto>(
                $"Reason cannot exceed {EvidenceLinkLimits.RemovalReason} characters.", cid);
        }

        var link = await _links.GetByIdAsync(request.LinkId, ct);
        if (link is null)
        {
            return Response<EvidenceLinkDto>.Fail("Evidence link not found.", 404, EvidenceLinkReasonCodes.LinkNotFound, cid);
        }

        if (!link.IsActive)
        {
            return Response<EvidenceLinkDto>.Fail("The evidence link is already removed.", 409,
                EvidenceLinkReasonCodes.AlreadyRemoved, cid);
        }

        var now = DateTimeOffset.UtcNow;
        var actorId = _user.UserId == Guid.Empty ? (Guid?)null : _user.UserId;
        link.Status = EvidenceLinkStatuses.Removed;
        link.RemovedAt = now;
        link.RemovedBy = _user.ActorName;
        link.RemovedByUserId = actorId;
        link.RemovalReason = reason;
        link.UpdatedAt = now;
        link.UpdatedBy = _user.ActorName;

        try
        {
            var removed = await _transactions.ExecuteAsync(async (session, tct) =>
            {
                if (!await _links.MarkRemovedAsync(session, link, tct))
                {
                    return false;
                }

                await EvidenceLinkEventPublisher.EnqueueRemovedAsync(_events, session, link, actorId, cid, tct);
                return true;
            }, ct);
            if (!removed)
            {
                return Response<EvidenceLinkDto>.Fail("The evidence link is already removed.", 409,
                    EvidenceLinkReasonCodes.AlreadyRemoved, cid);
            }
        }
        catch (PlatformTransactionUnavailableException)
        {
            return Response<EvidenceLinkDto>.Fail("The link could not be removed. Nothing was written.", 503,
                EvidenceLinkReasonCodes.TransactionUnavailable, cid);
        }

        return Response<EvidenceLinkDto>.Success(EvidenceLinkMapper.ToDto(link), 200, cid);
    }
}

internal static class EvidenceLinkEventPublisher
{
    private const string Producer = "Diten.Platform";

    public static Task EnqueueCreatedAsync(ITransactionalIntegrationEventWriter events, IPlatformTransactionSession session,
        EvidenceLink link, Guid? actorId, string? correlationId, CancellationToken ct)
    {
        var (eventId, correlation, at) = Ids(correlationId);
        return events.EnqueueAsync(session,
            new EvidenceLinkCreatedV1(eventId, at, link.TenantId, correlation, actorId, link.Id,
                link.ObjectRef.Module, link.ObjectRef.ObjectType, link.ObjectRef.ObjectId, link.ObjectRef.ObjectVersion,
                link.DocumentKind, link.DocumentId, link.DocumentVersionId, link.EvidenceTypeCode),
            Options(eventId, correlation, link.TenantId, at), ct);
    }

    public static Task EnqueueRemovedAsync(ITransactionalIntegrationEventWriter events, IPlatformTransactionSession session,
        EvidenceLink link, Guid? actorId, string? correlationId, CancellationToken ct)
    {
        var (eventId, correlation, at) = Ids(correlationId);
        return events.EnqueueAsync(session,
            new EvidenceLinkRemovedV1(eventId, at, link.TenantId, correlation, actorId, link.Id,
                link.ObjectRef.Module, link.ObjectRef.ObjectType, link.ObjectRef.ObjectId, link.ObjectRef.ObjectVersion,
                link.DocumentKind, link.DocumentId, link.DocumentVersionId, link.EvidenceTypeCode),
            Options(eventId, correlation, link.TenantId, at), ct);
    }

    private static (Guid EventId, Guid Correlation, DateTimeOffset At) Ids(string? correlationId)
        => (Guid.NewGuid(), Guid.TryParse(correlationId, out var c) ? c : Guid.NewGuid(), DateTimeOffset.UtcNow);

    private static EventPublishOptions Options(Guid eventId, Guid correlation, Guid tenantId, DateTimeOffset at) => new()
    {
        EventId = eventId,
        CorrelationId = correlation,
        TenantId = tenantId,
        Producer = Producer,
        OccurredAtUtc = at
    };
}

public sealed class GetEvidenceLinksByObjectQueryHandler
    : IRequestHandler<GetEvidenceLinksByObjectQuery, Response<IReadOnlyList<EvidenceLinkDto>>>
{
    private readonly ITenantContext _tenant;
    private readonly IEvidenceLinkRepository _links;

    private readonly IEvidenceDocumentStateResolver? _state;

    public GetEvidenceLinksByObjectQueryHandler(ITenantContext tenant, IEvidenceLinkRepository links,
        IEvidenceDocumentStateResolver? state = null)
    {
        _tenant = tenant;
        _links = links;
        _state = state;
    }

    public async Task<Response<IReadOnlyList<EvidenceLinkDto>>> Handle(GetEvidenceLinksByObjectQuery request, CancellationToken ct)
    {
        var cid = request.CorrelationId;
        if (!_tenant.IsResolved || _tenant.TenantId == Guid.Empty)
        {
            return EvidenceLinkRules.NoTenant<IReadOnlyList<EvidenceLinkDto>>(cid);
        }

        var module = EvidenceLinkRules.Key(request.Module);
        var objectType = EvidenceLinkRules.Key(request.ObjectType);
        var objectId = (request.ObjectId ?? string.Empty).Trim();
        if (module.Length == 0 || objectType.Length == 0 || objectId.Length == 0)
        {
            return EvidenceLinkRules.Invalid<IReadOnlyList<EvidenceLinkDto>>("module, objectType and objectId are required.", cid);
        }

        var rows = await _links.ListByObjectAsync(module, objectType, objectId,
            EvidenceLinkRules.Clean(request.ObjectVersion), request.IncludeRemoved, ct);
        return Response<IReadOnlyList<EvidenceLinkDto>>.Success(await EvidenceLinkMapper.ToDtosAsync(rows, _state, ct), 200, cid);
    }
}

/// <summary>WP-CL-BE-5 — the links of many objects in one read (tenant-scoped repository, one query), each with the
/// computed document state. Every requested object gets a row, empty when it has no links.</summary>
public sealed class QueryEvidenceLinksByObjectsQueryHandler
    : IRequestHandler<QueryEvidenceLinksByObjectsQuery, Response<IReadOnlyList<EvidenceObjectLinksDto>>>
{
    private readonly ITenantContext _tenant;
    private readonly IEvidenceLinkRepository _links;
    private readonly IEvidenceDocumentStateResolver? _state;

    public QueryEvidenceLinksByObjectsQueryHandler(ITenantContext tenant, IEvidenceLinkRepository links,
        IEvidenceDocumentStateResolver? state = null)
    {
        _tenant = tenant;
        _links = links;
        _state = state;
    }

    public async Task<Response<IReadOnlyList<EvidenceObjectLinksDto>>> Handle(
        QueryEvidenceLinksByObjectsQuery request, CancellationToken ct)
    {
        var cid = request.CorrelationId;
        if (!_tenant.IsResolved || _tenant.TenantId == Guid.Empty)
        {
            return EvidenceLinkRules.NoTenant<IReadOnlyList<EvidenceObjectLinksDto>>(cid);
        }

        var inputs = request.Objects ?? [];
        if (inputs.Count == 0)
        {
            return EvidenceLinkRules.Invalid<IReadOnlyList<EvidenceObjectLinksDto>>("objects must not be empty.", cid);
        }

        if (inputs.Count > EvidenceLinkLimits.MaxQueryObjects)
        {
            return EvidenceLinkRules.Invalid<IReadOnlyList<EvidenceObjectLinksDto>>(
                $"At most {EvidenceLinkLimits.MaxQueryObjects} objects per query.", cid);
        }

        var objects = new List<EvidenceObjectRef>();
        var seen = new HashSet<string>(StringComparer.Ordinal);
        foreach (var o in inputs)
        {
            var module = EvidenceLinkRules.Key(o?.Module);
            var objectType = EvidenceLinkRules.Key(o?.ObjectType);
            var objectId = (o?.ObjectId ?? string.Empty).Trim();
            var objectVersion = EvidenceLinkRules.Clean(o?.ObjectVersion);
            if (module.Length == 0 || objectType.Length == 0 || objectId.Length == 0)
            {
                return EvidenceLinkRules.Invalid<IReadOnlyList<EvidenceObjectLinksDto>>(
                    "Each object needs module, objectType and objectId.", cid);
            }

            if (seen.Add(string.Join('\u001f', module, objectType, objectId, objectVersion ?? string.Empty)))
            {
                objects.Add(new EvidenceObjectRef
                {
                    Module = module, ObjectType = objectType, ObjectId = objectId, ObjectVersion = objectVersion
                });
            }
        }

        var rows = await _links.ListByObjectsAsync(objects, request.IncludeRemoved, ct);
        var dtos = await EvidenceLinkMapper.ToDtosAsync(rows, _state, ct);
        var result = objects.Select(o => new EvidenceObjectLinksDto(
                new EvidenceObjectRefDto(o.Module, o.ObjectType, o.ObjectId, o.ObjectVersion),
                dtos.Where(d => d.ObjectRef.Module == o.Module && d.ObjectRef.ObjectType == o.ObjectType
                        && d.ObjectRef.ObjectId == o.ObjectId
                        && (o.ObjectVersion is null || d.ObjectRef.ObjectVersion == o.ObjectVersion))
                    .ToList()))
            .ToList();
        return Response<IReadOnlyList<EvidenceObjectLinksDto>>.Success(result, 200, cid);
    }
}

public sealed class GetEvidenceLinkByIdQueryHandler :IRequestHandler<GetEvidenceLinkByIdQuery, Response<EvidenceLinkDto>>
{
    private readonly ITenantContext _tenant;
    private readonly IEvidenceLinkRepository _links;

    private readonly IEvidenceDocumentStateResolver? _state;

    public GetEvidenceLinkByIdQueryHandler(ITenantContext tenant, IEvidenceLinkRepository links,
        IEvidenceDocumentStateResolver? state = null)
    {
        _tenant = tenant;
        _links = links;
        _state = state;
    }

    public async Task<Response<EvidenceLinkDto>> Handle(GetEvidenceLinkByIdQuery request, CancellationToken ct)
    {
        if (!_tenant.IsResolved || _tenant.TenantId == Guid.Empty)
        {
            return EvidenceLinkRules.NoTenant<EvidenceLinkDto>(request.CorrelationId);
        }

        var link = await _links.GetByIdAsync(request.LinkId, ct);
        return link is null
            ? Response<EvidenceLinkDto>.Fail("Evidence link not found.", 404, EvidenceLinkReasonCodes.LinkNotFound,
                request.CorrelationId)
            : Response<EvidenceLinkDto>.Success((await EvidenceLinkMapper.ToDtosAsync([link], _state, ct))[0], 200,
                request.CorrelationId);
    }
}

/// <summary>Reverse lookup. When the document still resolves in this tenant the caller must be able to read it
/// (403 otherwise); links to a document that no longer resolves are still listed (history outlives the document).</summary>
public sealed class GetEvidenceLinksByDocumentQueryHandler
    : IRequestHandler<GetEvidenceLinksByDocumentQuery, Response<IReadOnlyList<EvidenceLinkDto>>>
{
    private readonly ITenantContext _tenant;
    private readonly IEvidenceLinkRepository _links;
    private readonly IControlledDocumentRepository _documents;
    private readonly IExternalDocumentRegisterRepository _externalDocuments;
    private readonly IEvidenceDocumentAccessGate _access;
    private readonly IEvidenceDocumentStateResolver? _state;

    public GetEvidenceLinksByDocumentQueryHandler(
        ITenantContext tenant, IEvidenceLinkRepository links, IControlledDocumentRepository documents,
        IExternalDocumentRegisterRepository externalDocuments, IEvidenceDocumentAccessGate access,
        IEvidenceDocumentStateResolver? state = null)
    {
        _tenant = tenant;
        _links = links;
        _documents = documents;
        _externalDocuments = externalDocuments;
        _access = access;
        _state = state;
    }

    public async Task<Response<IReadOnlyList<EvidenceLinkDto>>> Handle(GetEvidenceLinksByDocumentQuery request, CancellationToken ct)
    {
        var cid = request.CorrelationId;
        if (!_tenant.IsResolved || _tenant.TenantId == Guid.Empty)
        {
            return EvidenceLinkRules.NoTenant<IReadOnlyList<EvidenceLinkDto>>(cid);
        }

        var readable = true;
        if (await _documents.GetByIdAsync(request.DocumentId, ct) is { } document)
        {
            readable = await _access.CanReadControlledAsync(document, ct);
        }
        else if (await _externalDocuments.GetByIdAsync(request.DocumentId, ct) is { } entry)
        {
            readable = await _access.CanReadExternalAsync(entry, ct);
        }

        if (!readable)
        {
            return Response<IReadOnlyList<EvidenceLinkDto>>.Fail("You cannot read this document.", 403,
                EvidenceLinkReasonCodes.DocumentNotReadable, cid);
        }

        var rows = await _links.ListActiveByDocumentAsync(request.DocumentId,
            request.VersionId is { } v && v != Guid.Empty ? v : null, ct);
        return Response<IReadOnlyList<EvidenceLinkDto>>.Success(await EvidenceLinkMapper.ToDtosAsync(rows, _state, ct), 200, cid);
    }
}

/// <summary>Picker source: controlled documents the caller can READ (MOD-0029 gate per row) and external register
/// entries when the caller may read the register. Deleted rows never appear.</summary>
public sealed class GetEvidenceDocumentOptionsQueryHandler
    : IRequestHandler<GetEvidenceDocumentOptionsQuery, Response<IReadOnlyList<EvidenceDocumentOptionDto>>>
{
    private readonly ITenantContext _tenant;
    private readonly IControlledDocumentRepository _documents;
    private readonly IExternalDocumentRegisterRepository _externalDocuments;
    private readonly IEvidenceDocumentAccessGate _access;
    private readonly IDocumentMasterRegisterRepository? _register;

    public GetEvidenceDocumentOptionsQueryHandler(
        ITenantContext tenant, IControlledDocumentRepository documents,
        IExternalDocumentRegisterRepository externalDocuments, IEvidenceDocumentAccessGate access,
        IDocumentMasterRegisterRepository? register = null)
    {
        _tenant = tenant;
        _documents = documents;
        _externalDocuments = externalDocuments;
        _access = access;
        _register = register;
    }

    public async Task<Response<IReadOnlyList<EvidenceDocumentOptionDto>>> Handle(
        GetEvidenceDocumentOptionsQuery request, CancellationToken ct)
    {
        var cid = request.CorrelationId;
        if (!_tenant.IsResolved || _tenant.TenantId == Guid.Empty)
        {
            return EvidenceLinkRules.NoTenant<IReadOnlyList<EvidenceDocumentOptionDto>>(cid);
        }

        var kind = EvidenceLinkRules.Clean(request.Kind)?.ToLowerInvariant();
        if (kind is not null && !EvidenceDocumentKinds.IsValid(kind))
        {
            return EvidenceLinkRules.Invalid<IReadOnlyList<EvidenceDocumentOptionDto>>(
                "kind must be 'controlled' or 'external'.", cid);
        }

        var take = Math.Clamp(request.Take ?? EvidenceLinkLimits.DefaultOptionTake, 1, EvidenceLinkLimits.MaxOptionTake);
        var term = EvidenceLinkRules.Clean(request.Search);
        var result = new List<EvidenceDocumentOptionDto>();

        if (kind is null or EvidenceDocumentKinds.Controlled)
        {
            // WP-CL-FIX-1 — the register is read ONCE for the page (no per-document call). A document linked by exactly
            // one live register row takes that row's DocumentCode and lifecycle; zero or several rows (the link is not
            // 1:1-enforced) are not guessed: the code falls back to CanonicalId / DocumentKey and the state stays unknown.
            var registerByDocument = _register is null
                ? new Dictionary<Guid, List<DocumentMasterRegisterEntry>>()
                : (await _register.GetAllForTenantAsync(ct))
                    .Where(r => r.ControlledDocumentId is not null && r.DeletedAt is null && !r.IsDeleted)
                    .GroupBy(r => r.ControlledDocumentId!.Value)
                    .ToDictionary(g => g.Key, g => g.ToList());
            DocumentMasterRegisterEntry? RegisterRow(Guid documentId) =>
                registerByDocument.TryGetValue(documentId, out var rows) && rows.Count == 1 ? rows[0] : null;

            var candidates = (await _documents.GetAllForTenantAsync(ct))
                .Where(d => d.DeletedAt is null && !d.IsDeleted)
                .Where(d => term is null
                    || (RegisterRow(d.Id)?.DocumentCode?.Contains(term, StringComparison.OrdinalIgnoreCase) ?? false)
                    || d.Title.Contains(term, StringComparison.OrdinalIgnoreCase)
                    || (d.CanonicalId?.Contains(term, StringComparison.OrdinalIgnoreCase) ?? false)
                    || d.DocumentKey.Contains(term, StringComparison.OrdinalIgnoreCase))
                .OrderBy(d => d.Title, StringComparer.OrdinalIgnoreCase);
            foreach (var d in candidates)
            {
                if (result.Count >= take)
                {
                    break;
                }

                if (!await _access.CanReadControlledAsync(d, ct))
                {
                    continue;
                }

                var row = RegisterRow(d.Id);
                result.Add(new EvidenceDocumentOptionDto(
                    EvidenceDocumentKinds.Controlled, d.Id, d.Title, EvidenceDocumentStateResolver.ControlledDocumentCode(d, row),
                    d.DocumentType.ToString(), d.CurrentVersionId,
                    d.CurrentVersionId is null ? null : $"v{d.CurrentVersionNumber}",
                    d.Status.ToString(), d.EffectiveDate, null, null, null,
                    row is null
                        ? EvidenceDocumentStates.Unknown
                        : EvidenceDocumentStateResolver.ControlledLifecycleState(row.LifecycleStatus)));
            }
        }

        if ((kind is null or EvidenceDocumentKinds.External) && result.Count < take)
        {
            var entries = (await _externalDocuments.GetAllForTenantAsync(ct))
                .Where(e => e.DeletedAt is null && !e.IsDeleted)
                .Where(e => term is null
                    || e.ExternalDocumentTitle.Contains(term, StringComparison.OrdinalIgnoreCase)
                    || (e.ExternalDocumentCode?.Contains(term, StringComparison.OrdinalIgnoreCase) ?? false))
                .OrderBy(e => e.ExternalDocumentTitle, StringComparer.OrdinalIgnoreCase);
            foreach (var e in entries)
            {
                if (result.Count >= take)
                {
                    break;
                }

                if (!await _access.CanReadExternalAsync(e, ct))
                {
                    continue;
                }

                result.Add(new EvidenceDocumentOptionDto(
                    EvidenceDocumentKinds.External, e.Id, e.ExternalDocumentTitle, e.ExternalDocumentCode,
                    e.ExternalDocumentType.ToString(), null, null, e.ExternalDocumentStatus.ToString(),
                    e.SourceEffectiveDate, e.CountryCode, e.SourceVersion, e.SourceStatus.ToString(),
                    EvidenceDocumentStateResolver.External(e, DateTimeOffset.UtcNow).DocumentState));
            }
        }

        return Response<IReadOnlyList<EvidenceDocumentOptionDto>>.Success(result, 200, cid);
    }
}
