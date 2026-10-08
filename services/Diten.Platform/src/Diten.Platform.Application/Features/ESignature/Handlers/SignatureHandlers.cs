using System.Text;
using System.Text.Json;
using System.Text.RegularExpressions;
using Diten.Platform.Application.Common;
using Diten.Platform.Application.Contracts.Audit;
using Diten.Platform.Application.Contracts.DocumentRepository;
using Diten.Platform.Application.Features.ESignature.Services;
using Diten.Platform.Common.Authorization;
using Diten.Platform.Domain.Entities.ESignature;
using Diten.Platform.Domain.Enums;
using Diten.Platform.Domain.Repositories;
using MediatR;
using Microsoft.Extensions.Logging;

namespace Diten.Platform.Application.Features.ESignature.Handlers;

/// <summary>Shared failure shapes and the transaction-abort carrier of the e-signature handlers.</summary>
internal static class ESignResponses
{
    public static Response<T> Fail<T>(string code, int status, string message) => Response<T>.Fail(message, status, code);

    public static Response<T> Actor<T>() =>
        Fail<T>("PERMISSION_DENIED", 403, "Only a tenant user can perform this e-signature operation with their own token.");

    public static Response<T> Validation<T>(string message) => Fail<T>(ESignCodes.ValidationFailed, 400, message);

    public static bool IdempotencyKeyOk(string? key) => key is { Length: >= 8 and <= 100 } && !string.IsNullOrWhiteSpace(key);
}

/// <summary>Thrown inside a transaction body to abort it and answer with <see cref="Code"/> / <see cref="Status"/>.</summary>
internal sealed class ESignRejectedException(string code, int status, string message) : Exception(message)
{
    public string Code { get; } = code;
    public int Status { get; } = status;
}

/// <summary>
/// MOD-0022 E2 — <c>POST api/v1/esign/signatures</c> (ESIGN-BUNDLE §4.4). The server order is pack §8 "E2 — Signing"
/// steps 1–8, exactly: idempotent replay BEFORE the ticket; subject (visibility + tenant guard); effective policy; visual
/// mode; request; THEN the ticket is consumed; image + digests + record + request + meaning + audit in one transaction.
/// </summary>
public sealed class SignSubjectCommandHandler : IRequestHandler<SignSubjectCommand, Response<SignatureRecordDto>>
{
    private const int MaxNoteLength = 500;
    private const int MaxStrokesBytes = 262_144;
    private const int MaxContextKeys = 20;
    private static readonly Regex ContextKey = new("^[a-zA-Z][a-zA-Z0-9]{0,63}$", RegexOptions.Compiled);

    private readonly IESignActorAccessor _actors;
    private readonly ISignableSubjectRegistry _subjects;
    private readonly IESignatureCatalog _catalog;
    private readonly IESignMeaningRepository _meanings;
    private readonly IESignRequestRepository _requests;
    private readonly IESignatureRecordRepository _records;
    private readonly IESignTicketConsumer _tickets;
    private readonly IContentStorageGateway _storage;
    private readonly IESignNumberGenerator _numbers;
    private readonly IPlatformTransactionExecutor _transactions;
    private readonly ITransactionalAuditOutboxWriter _audit;
    private readonly ILogger<SignSubjectCommandHandler> _logger;

    public SignSubjectCommandHandler(
        IESignActorAccessor actors,
        ISignableSubjectRegistry subjects,
        IESignatureCatalog catalog,
        IESignMeaningRepository meanings,
        IESignRequestRepository requests,
        IESignatureRecordRepository records,
        IESignTicketConsumer tickets,
        IContentStorageGateway storage,
        IESignNumberGenerator numbers,
        IPlatformTransactionExecutor transactions,
        ITransactionalAuditOutboxWriter audit,
        ILogger<SignSubjectCommandHandler> logger)
    {
        _actors = actors;
        _subjects = subjects;
        _catalog = catalog;
        _meanings = meanings;
        _requests = requests;
        _records = records;
        _tickets = tickets;
        _storage = storage;
        _numbers = numbers;
        _transactions = transactions;
        _audit = audit;
        _logger = logger;
    }

    public async Task<Response<SignatureRecordDto>> Handle(SignSubjectCommand request, CancellationToken ct)
    {
        // ── 1. who (JWT sub, tenant_user only) + idempotent replay BEFORE the ticket ─────────────────────────────────
        var actor = _actors.Current();
        if (actor is null)
        {
            return ESignResponses.Actor<SignatureRecordDto>();
        }

        if (!ESignResponses.IdempotencyKeyOk(request.IdempotencyKey))
        {
            return ESignResponses.Validation<SignatureRecordDto>("idempotencyKey is required (8–100 characters).");
        }

        var replay = await _records.GetByIdempotencyKeyAsync(actor.TenantId, actor.UserIdText, request.IdempotencyKey!, ct);
        if (replay is not null)
        {
            return Response<SignatureRecordDto>.Success(ESignatureMapping.ToDto(replay, null), 200);
        }

        if (string.IsNullOrWhiteSpace(request.Ticket))
        {
            return ESignResponses.Fail<SignatureRecordDto>(ESignCodes.TicketRequired, 400, "A re-authentication ticket is required.");
        }

        if (string.IsNullOrWhiteSpace(request.MeaningCode))
        {
            return ESignResponses.Validation<SignatureRecordDto>("meaningCode is required.");
        }

        if (request.Note is { Length: > MaxNoteLength })
        {
            return ESignResponses.Validation<SignatureRecordDto>($"note must be at most {MaxNoteLength} characters.");
        }

        var context = request.Context ?? new Dictionary<string, string?>();
        if (context.Count > MaxContextKeys || context.Any(p => !ContextKey.IsMatch(p.Key) || p.Value is { Length: > 200 }))
        {
            return ESignResponses.Validation<SignatureRecordDto>("context: at most 20 alphanumeric keys, values at most 200 characters.");
        }

        if (!ESignatureWire.TryParse<ESignatureAppearanceType>(request.Appearance?.Type ?? "none", out var appearance))
        {
            return ESignResponses.Validation<SignatureRecordDto>("appearance.type must be none, drawn or sample.");
        }

        // ── 2. subject: visibility + tenant guard (KS-2) — the company comes from the provider, never the client ─────
        var resolution = await _subjects.ResolveAsync(request.Module ?? "", request.SubjectType ?? "", request.SubjectId ?? "", ct);
        if (resolution.Outcome != SignableSubjectOutcome.Found)
        {
            return ESignResponses.Fail<SignatureRecordDto>(ESignCodes.SubjectNotFound, 404, "The subject was not found.");
        }

        var subject = resolution.Subject!;

        // ── 3. effective policy (KS-5) + meaning ─────────────────────────────────────────────────────────────────────
        await _catalog.EnsureMeaningsAsync(actor.TenantId, ct);
        var policy = await _catalog.GetEffectivePolicyAsync(actor.TenantId, subject.LegalEntityId, ct);
        var row = await _catalog.GetRowAsync(actor.TenantId, policy, request.ActionCode, ct);
        if (!string.IsNullOrWhiteSpace(request.ActionCode) && row is null)
        {
            return ESignResponses.Validation<SignatureRecordDto>("actionCode is not a registered signable action.");
        }

        var meaning = await _meanings.GetAsync(actor.TenantId, request.MeaningCode, ct);
        if (meaning is null || !meaning.IsActive
            || (row?.MeaningCode is { } fixedMeaning && !string.Equals(fixedMeaning, request.MeaningCode, StringComparison.Ordinal)))
        {
            return ESignResponses.Fail<SignatureRecordDto>(ESignCodes.MeaningNotAllowed, 409, "The meaning is not active or not allowed for this action.");
        }

        // ── 4. appearance against the visual mode (KS-8) ─────────────────────────────────────────────────────────────
        var visualMode = row?.VisualMode ?? ESignatureVisualMode.Optional;
        if (!ESignatureRules.AppearanceAllowed(visualMode, appearance))
        {
            return ESignResponses.Fail<SignatureRecordDto>(ESignCodes.AppearanceNotAllowed, 409, "The visual signature does not match the policy.");
        }

        if (appearance == ESignatureAppearanceType.Sample)
        {
            // E3 builds signature samples; until then no sample can be the caller's active one (pack E2 port).
            return ESignResponses.Fail<SignatureRecordDto>(ESignCodes.SampleNotActive, 409, "The signature sample is not your active sample.");
        }

        byte[]? png = null;
        string? strokesJson = null;
        if (appearance == ESignatureAppearanceType.Drawn)
        {
            var drawing = ReadDrawing(request.Appearance!);
            if (drawing.Failure is { } failure)
            {
                return failure;
            }

            png = drawing.Png;
            strokesJson = drawing.StrokesJson;
        }

        // ── 5. request (optional): waiting, nominated, same subject and meaning ──────────────────────────────────────
        ESignatureRequest? signatureRequest = null;
        if (request.RequestId is { } requestId)
        {
            signatureRequest = await _requests.GetAsync(actor.TenantId, requestId, ct);
            if (signatureRequest is null || !signatureRequest.IsOpen
                || signatureRequest.Module != request.Module || signatureRequest.SubjectType != request.SubjectType
                || signatureRequest.SubjectId != request.SubjectId)
            {
                return ESignResponses.Fail<SignatureRecordDto>(ESignCodes.RequestNotOpen, 409, "The signature request is not open for this subject.");
            }

            if (!string.Equals(signatureRequest.SignerUserId, actor.UserIdText, StringComparison.Ordinal))
            {
                return ESignResponses.Fail<SignatureRecordDto>(ESignCodes.SignerNotNominated, 403, "You are not the signer of this request.");
            }

            if (!string.Equals(signatureRequest.MeaningCode, request.MeaningCode, StringComparison.Ordinal))
            {
                return ESignResponses.Fail<SignatureRecordDto>(ESignCodes.MeaningNotAllowed, 409, "The meaning differs from the request.");
            }
        }

        // ── 6. consume the ticket (intent sign), then check its kind against the policy (KS-3 / KS-10) ───────────────
        var consumed = await _tickets.ConsumeAsync(request.Ticket, actor.TenantId, actor.UserId, "sign", ct);
        if (consumed.Outcome == TicketConsumeOutcome.Unavailable)
        {
            return ESignResponses.Fail<SignatureRecordDto>(ESignCodes.ReauthUnavailable, 503, "Re-authentication could not be checked.");
        }

        if (consumed.Outcome == TicketConsumeOutcome.Invalid)
        {
            return ESignResponses.Fail<SignatureRecordDto>(ESignCodes.TicketInvalid, 403, "The re-authentication ticket is not valid.");
        }

        // Times enter the digest, and Mongo keeps DateTime to the millisecond: both are truncated BEFORE hashing, or every
        // record would verify as tampered after its first round trip (measured 2026-10-08).
        var now = ESignatureDigest.ToStoredPrecision(DateTime.UtcNow);
        var passwordVerifiedAt = ESignatureDigest.ToStoredPrecision(consumed.PasswordVerifiedAtUtc);
        if (!ESignatureRules.ReauthSatisfies(consumed.Kind, passwordVerifiedAt, now, policy.ReauthEvery, policy.ReauthWindowMinutes,
                passwordAlwaysRequired: appearance == ESignatureAppearanceType.Sample))
        {
            return ESignResponses.Fail<SignatureRecordDto>(ESignCodes.ReauthRequired, 403, "The policy requires your password for this signature.");
        }

        // ── 7. image, digests, record + request + meaning + audit in ONE transaction ─────────────────────────────────
        var recordId = Guid.NewGuid();
        ContentStoreResult? stored = null;
        if (png is not null)
        {
            var store = await _storage.StoreAsync(new ContentStoreRequest(
                actor.TenantId, subject.LegalEntityId ?? Guid.Empty, ContentStorageScope.ESignatureImages, recordId, recordId,
                "signature.png", "image/png", new MemoryStream(png), actor.UserIdText,
                $"tenant/{actor.TenantId:D}/esign-signatures"), ct);
            if (!store.IsSuccessful || store.Data is null)
            {
                _logger.LogWarning("E-signature image could not be stored. Status={Status}", store.StatusCode);
                return ESignResponses.Fail<SignatureRecordDto>(ESignCodes.ReauthUnavailable, 503, "The signature image could not be stored.");
            }

            stored = store.Data;
        }

        var number = await _numbers.NextAsync(actor.TenantId, "ESIG", now.Year, ct);
        var (meaningText, meaningLanguage) = ESignatureLanguages.Pick(meaning.Names, actor.Language);
        var subjectHash = ESignatureDigest.SubjectHash(request.Module!, request.SubjectType!, request.SubjectId!, subject.Fields);
        var record = new ESignatureRecord
        {
            Id = recordId,
            TenantId = actor.TenantId,
            CreatedBy = actor.UserIdText,
            SignatureNumber = $"ESIG-{now.Year}-{number:D6}",
            Module = request.Module!,
            SubjectType = request.SubjectType!,
            SubjectId = request.SubjectId!,
            SubjectDisplay = subject.Display,
            SubjectSummary = subject.Summary.Select(l => new ESignatureSummaryLine(l.Label, l.Value)).ToList(),
            LegalEntityId = subject.LegalEntityId,
            ActionCode = string.IsNullOrWhiteSpace(request.ActionCode) ? null : request.ActionCode,
            Context = context.ToDictionary(p => p.Key, p => p.Value),
            MeaningCode = meaning.Code,
            MeaningTextAtSign = meaningText,
            MeaningLanguage = meaningLanguage,
            SignerUserId = actor.UserIdText,
            SignerLogin = actor.Login,
            SignerNameAtSign = actor.Name,
            SignedAtUtc = now,
            Note = string.IsNullOrWhiteSpace(request.Note) ? null : request.Note.Trim(),
            SubjectHash = subjectHash,
            SignatureDigest = string.Empty,
            ReauthKind = consumed.Kind,
            PasswordVerifiedAtUtc = passwordVerifiedAt,
            TicketRef = ESignTicketRef.Of(request.Ticket),
            AppearanceType = appearance,
            AppearanceContentId = stored?.ContentId.ToString("D"),
            AppearanceStorageProvider = stored?.StorageProvider,
            AppearanceStoragePath = stored?.ObjectKey,
            AppearanceVectorJson = strokesJson,
            AppearanceImageHash = png is null ? null : ESignatureDigest.ImageHash(png),
            RequestId = signatureRequest?.Id,
            PolicySnapshot = new ESignaturePolicySnapshot(policy.Scope, policy.LegalEntityId, policy.Version, visualMode, policy.ReauthEvery,
                policy.ReauthWindowMinutes),
            IdempotencyKey = request.IdempotencyKey!
        };
        record = WithDigest(record);

        try
        {
            await _transactions.ExecuteAsync(async (session, txCt) =>
            {
                await _records.InsertAsync(session, record, txCt);

                if (signatureRequest is not null)
                {
                    var expected = signatureRequest.Version;
                    signatureRequest.MarkSigned(record.Id, now);
                    signatureRequest.UpdatedAt = DateTimeOffset.UtcNow;
                    signatureRequest.UpdatedBy = actor.UserIdText;
                    if (!await _requests.SaveDecisionAsync(session, signatureRequest, expected, txCt))
                    {
                        throw new ESignRejectedException(ESignCodes.RequestNotOpen, 409, "The signature request was decided meanwhile.");
                    }
                }

                await _meanings.MarkUsedAsync(session, actor.TenantId, meaning.Code, now, txCt);
                await ESignatureAuditIntent.EnqueueAsync(_audit, session, actor.TenantId, ESignAuditEvents.Signed, AuditOperation.Create,
                    "ESignatureRecord", record.Id, AuditActorType.TenantUser, actor.UserId,
                    new Dictionary<string, object?>
                    {
                        ["signatureNumber"] = record.SignatureNumber,
                        ["module"] = record.Module,
                        ["subjectType"] = record.SubjectType,
                        ["subjectId"] = record.SubjectId,
                        ["meaningCode"] = record.MeaningCode,
                        ["actionCode"] = record.ActionCode,
                        ["reauthKind"] = ESignatureWire.Name(record.ReauthKind),
                        ["appearanceType"] = ESignatureWire.Name(record.AppearanceType),
                        ["subjectHash"] = record.SubjectHash,
                        ["signatureDigest"] = record.SignatureDigest,
                        ["requestId"] = record.RequestId
                    }, txCt);
                return true;
            }, ct);
        }
        catch (ESignatureDuplicateException)
        {
            // A parallel call with the same idempotency key won: answer with its record (the transaction was aborted).
            var winner = await _records.GetByIdempotencyKeyAsync(actor.TenantId, actor.UserIdText, request.IdempotencyKey!, ct);
            return winner is null
                ? ESignResponses.Fail<SignatureRecordDto>(ESignCodes.ValidationFailed, 409, "The signature could not be recorded.")
                : Response<SignatureRecordDto>.Success(ESignatureMapping.ToDto(winner, null), 200);
        }
        catch (ESignRejectedException rejected)
        {
            return ESignResponses.Fail<SignatureRecordDto>(rejected.Code, rejected.Status, rejected.Message);
        }
        catch (ESignatureTaskBindingException)
        {
            return ESignResponses.Fail<SignatureRecordDto>(ESignCodes.SignatureMismatch, 409, "A signature is already bound to this workflow task.");
        }
        catch (PlatformTransactionUnavailableException)
        {
            return ESignResponses.Fail<SignatureRecordDto>(ESignCodes.ReauthUnavailable, 503, "The signature could not be recorded now.");
        }

        // ── 8. the audit intent is in the transaction above; 201 ─────────────────────────────────────────────────────
        return Response<SignatureRecordDto>.Success(ESignatureMapping.ToDto(record, null), 201);
    }

    internal static ESignatureRecord WithDigest(ESignatureRecord record)
    {
        var digest = ESignatureDigest.SignatureDigest(record.DigestInput());
        return new ESignatureRecord
        {
            Id = record.Id, TenantId = record.TenantId, CreatedAt = record.CreatedAt, CreatedBy = record.CreatedBy,
            SignatureNumber = record.SignatureNumber, Module = record.Module, SubjectType = record.SubjectType, SubjectId = record.SubjectId,
            SubjectDisplay = record.SubjectDisplay, SubjectSummary = record.SubjectSummary, LegalEntityId = record.LegalEntityId,
            ActionCode = record.ActionCode, Context = record.Context, MeaningCode = record.MeaningCode,
            MeaningTextAtSign = record.MeaningTextAtSign, MeaningLanguage = record.MeaningLanguage, SignerUserId = record.SignerUserId,
            SignerLogin = record.SignerLogin, SignerNameAtSign = record.SignerNameAtSign, SignedAtUtc = record.SignedAtUtc,
            Note = record.Note, SubjectHashAlgorithm = record.SubjectHashAlgorithm, SubjectHash = record.SubjectHash,
            SignatureDigest = digest, ReauthKind = record.ReauthKind, PasswordVerifiedAtUtc = record.PasswordVerifiedAtUtc,
            TicketRef = record.TicketRef, SecondFactor = record.SecondFactor, AppearanceType = record.AppearanceType,
            AppearanceSampleId = record.AppearanceSampleId, AppearanceContentId = record.AppearanceContentId,
            AppearanceStorageProvider = record.AppearanceStorageProvider, AppearanceStoragePath = record.AppearanceStoragePath,
            AppearanceVectorJson = record.AppearanceVectorJson, AppearanceImageHash = record.AppearanceImageHash,
            RequestId = record.RequestId, PolicySnapshot = record.PolicySnapshot, IdempotencyKey = record.IdempotencyKey,
            CorrelationId = record.CorrelationId
        };
    }

    private static (byte[]? Png, string? StrokesJson, Response<SignatureRecordDto>? Failure) ReadDrawing(SignAppearanceInput input)
    {
        if (string.IsNullOrWhiteSpace(input.PngBase64))
        {
            return (null, null, ESignResponses.Fail<SignatureRecordDto>(ESignCodes.DrawingEmpty, 400, "A drawn signature needs a PNG."));
        }

        // Size first, before decoding: base64 of > 1 MiB cannot decode to ≤ 1 MiB (V9).
        if (input.PngBase64.Length > (ESignaturePngValidator.MaxBytes + 2) / 3 * 4 + 4)
        {
            return (null, null, ESignResponses.Fail<SignatureRecordDto>(ESignCodes.ImageTooLarge, 413, "The signature image is larger than 1 MB."));
        }

        byte[] png;
        try
        {
            png = Convert.FromBase64String(input.PngBase64);
        }
        catch (FormatException)
        {
            return (null, null, ESignResponses.Fail<SignatureRecordDto>(ESignCodes.ImageNotPng, 415, "The signature image is not a PNG."));
        }

        switch (ESignaturePngValidator.Check(png))
        {
            case PngCheck.Empty:
                return (null, null, ESignResponses.Fail<SignatureRecordDto>(ESignCodes.DrawingEmpty, 400, "A drawn signature needs a PNG."));
            case PngCheck.TooLarge:
                return (null, null, ESignResponses.Fail<SignatureRecordDto>(ESignCodes.ImageTooLarge, 413, "The signature image is larger than 1 MB."));
            case PngCheck.NotPng:
                return (null, null, ESignResponses.Fail<SignatureRecordDto>(ESignCodes.ImageNotPng, 415, "The signature image is not a PNG."));
        }

        if (input.StrokesJson is null)
        {
            return (png, null, null);
        }

        if (Encoding.UTF8.GetByteCount(input.StrokesJson) > MaxStrokesBytes || !StrokesAreNormalized(input.StrokesJson))
        {
            return (null, null, ESignResponses.Validation<SignatureRecordDto>("strokes must be at most 256 KB of points between 0 and 1."));
        }

        return (png, input.StrokesJson, null);
    }

    private static bool StrokesAreNormalized(string json)
    {
        try
        {
            using var document = JsonDocument.Parse(json);
            if (document.RootElement.ValueKind != JsonValueKind.Array)
            {
                return false;
            }

            foreach (var stroke in document.RootElement.EnumerateArray())
            {
                if (stroke.ValueKind != JsonValueKind.Array) return false;
                foreach (var point in stroke.EnumerateArray())
                {
                    if (point.ValueKind != JsonValueKind.Array || point.GetArrayLength() < 2) return false;
                    foreach (var coordinate in point.EnumerateArray())
                    {
                        if (coordinate.ValueKind != JsonValueKind.Number) return false;
                        var value = coordinate.GetDouble();
                        if (value is < 0 or > 1) return false;
                    }
                }
            }

            return true;
        }
        catch (JsonException)
        {
            return false;
        }
    }
}

public sealed class GetSignatureQueryHandler : IRequestHandler<GetSignatureQuery, Response<SignatureRecordDto>>
{
    private readonly IESignActorAccessor _actors;
    private readonly IESignatureRecordRepository _records;
    private readonly IESignatureInvalidationRepository _invalidations;
    private readonly ISignableSubjectRegistry _subjects;

    public GetSignatureQueryHandler(IESignActorAccessor actors, IESignatureRecordRepository records,
        IESignatureInvalidationRepository invalidations, ISignableSubjectRegistry subjects)
    {
        _actors = actors;
        _records = records;
        _invalidations = invalidations;
        _subjects = subjects;
    }

    public async Task<Response<SignatureRecordDto>> Handle(GetSignatureQuery request, CancellationToken ct)
    {
        var actor = _actors.Current();
        if (actor is null)
        {
            return ESignResponses.Actor<SignatureRecordDto>();
        }

        var record = await _records.GetAsync(actor.TenantId, request.Id, ct);
        // 404 when missing, in another tenant, or the subject is not visible to the caller (§4.5) — one answer.
        if (record is null
            || (await _subjects.ResolveAsync(record.Module, record.SubjectType, record.SubjectId, ct)).Outcome != SignableSubjectOutcome.Found)
        {
            return ESignResponses.Fail<SignatureRecordDto>(ESignCodes.NotFound, 404, "Signature not found.");
        }

        var invalidation = await _invalidations.GetBySignatureAsync(actor.TenantId, record.Id, ct);
        return Response<SignatureRecordDto>.Success(ESignatureMapping.ToDto(record, invalidation));
    }
}

public sealed class ListSignaturesBySubjectQueryHandler : IRequestHandler<ListSignaturesBySubjectQuery, Response<IReadOnlyList<SignatureRecordDto>>>
{
    private readonly IESignActorAccessor _actors;
    private readonly IESignatureRecordRepository _records;
    private readonly IESignatureInvalidationRepository _invalidations;
    private readonly ISignableSubjectRegistry _subjects;

    public ListSignaturesBySubjectQueryHandler(IESignActorAccessor actors, IESignatureRecordRepository records,
        IESignatureInvalidationRepository invalidations, ISignableSubjectRegistry subjects)
    {
        _actors = actors;
        _records = records;
        _invalidations = invalidations;
        _subjects = subjects;
    }

    public async Task<Response<IReadOnlyList<SignatureRecordDto>>> Handle(ListSignaturesBySubjectQuery request, CancellationToken ct)
    {
        var actor = _actors.Current();
        if (actor is null)
        {
            return ESignResponses.Actor<IReadOnlyList<SignatureRecordDto>>();
        }

        var resolution = await _subjects.ResolveAsync(request.Module ?? "", request.SubjectType ?? "", request.SubjectId ?? "", ct);
        if (resolution.Outcome != SignableSubjectOutcome.Found)
        {
            return ESignResponses.Fail<IReadOnlyList<SignatureRecordDto>>(ESignCodes.SubjectNotFound, 404, "The subject was not found.");
        }

        var records = await _records.ListBySubjectAsync(actor.TenantId, request.Module!, request.SubjectType!, request.SubjectId!, ct);
        var invalidations = await _invalidations.GetBySignaturesAsync(actor.TenantId, records.Select(r => r.Id).ToList(), ct);
        IReadOnlyList<SignatureRecordDto> items = records
            .Select(r => ESignatureMapping.ToDto(r, invalidations.GetValueOrDefault(r.Id)))
            .ToList();
        return Response<IReadOnlyList<SignatureRecordDto>>.Success(items);
    }
}

/// <summary>§4.7 — verification changes nothing; it writes one audit event carrying the computed status.</summary>
public sealed class VerifySignatureCommandHandler : IRequestHandler<VerifySignatureCommand, Response<VerificationResultDto>>
{
    private readonly IESignActorAccessor _actors;
    private readonly IESignatureRecordRepository _records;
    private readonly IESignatureInvalidationRepository _invalidations;
    private readonly IESignatureVerifier _verifier;
    private readonly IAuditService _audit;
    private readonly ITenantAuthorizationContext _principal;

    public VerifySignatureCommandHandler(IESignActorAccessor actors, IESignatureRecordRepository records,
        IESignatureInvalidationRepository invalidations, IESignatureVerifier verifier, IAuditService audit, ITenantAuthorizationContext principal)
    {
        _actors = actors;
        _records = records;
        _invalidations = invalidations;
        _verifier = verifier;
        _audit = audit;
        _principal = principal;
    }

    public async Task<Response<VerificationResultDto>> Handle(VerifySignatureCommand request, CancellationToken ct)
    {
        var actor = _actors.Current();
        if (actor is null)
        {
            return ESignResponses.Actor<VerificationResultDto>();
        }

        var record = await _records.GetAsync(actor.TenantId, request.Id, ct);
        if (record is null)
        {
            return ESignResponses.Fail<VerificationResultDto>(ESignCodes.NotFound, 404, "Signature not found.");
        }

        var invalidation = await _invalidations.GetBySignatureAsync(actor.TenantId, record.Id, ct);
        var result = await _verifier.VerifyAsync(record, invalidation, ct);

        await _audit.AppendAsync(new AuditAppendRequest
        {
            CorrelationId = Guid.NewGuid(),
            RequestType = ESignAuditEvents.Verified,
            ActorType = AuditActorTypeResolver.ForCommand(_principal),
            ActorId = actor.UserId,
            Category = AuditCategory.ESignature,
            EntityType = "ESignatureRecord",
            EntityId = record.Id,
            Operation = AuditOperation.Execute,
            Metadata = new Dictionary<string, object?>
            {
                ["signatureNumber"] = record.SignatureNumber,
                ["status"] = result.Status,
                ["imageIntact"] = result.ImageIntact,
                ["digestIntact"] = result.DigestIntact
            },
            SourceModule = ESignAuditEvents.SourceModule
        }, ct);

        return Response<VerificationResultDto>.Success(result);
    }
}

/// <summary>§4.8 — inserts one invalidation (reason ≥ 10, a PASSWORD ticket with intent invalidate); the record is untouched.</summary>
public sealed class InvalidateSignatureCommandHandler : IRequestHandler<InvalidateSignatureCommand, Response<SignatureRecordDto>>
{
    private readonly IESignActorAccessor _actors;
    private readonly IESignatureRecordRepository _records;
    private readonly IESignatureInvalidationRepository _invalidations;
    private readonly IESignTicketConsumer _tickets;
    private readonly IPlatformTransactionExecutor _transactions;
    private readonly ITransactionalAuditOutboxWriter _audit;

    public InvalidateSignatureCommandHandler(IESignActorAccessor actors, IESignatureRecordRepository records,
        IESignatureInvalidationRepository invalidations, IESignTicketConsumer tickets, IPlatformTransactionExecutor transactions,
        ITransactionalAuditOutboxWriter audit)
    {
        _actors = actors;
        _records = records;
        _invalidations = invalidations;
        _tickets = tickets;
        _transactions = transactions;
        _audit = audit;
    }

    public async Task<Response<SignatureRecordDto>> Handle(InvalidateSignatureCommand request, CancellationToken ct)
    {
        var actor = _actors.Current();
        if (actor is null)
        {
            return ESignResponses.Actor<SignatureRecordDto>();
        }

        if (!ESignResponses.IdempotencyKeyOk(request.IdempotencyKey))
        {
            return ESignResponses.Validation<SignatureRecordDto>("idempotencyKey is required (8–100 characters).");
        }

        var record = await _records.GetAsync(actor.TenantId, request.Id, ct);
        if (record is null)
        {
            return ESignResponses.Fail<SignatureRecordDto>(ESignCodes.NotFound, 404, "Signature not found.");
        }

        var existing = await _invalidations.GetBySignatureAsync(actor.TenantId, record.Id, ct);
        if (existing is not null)
        {
            // Same key = the first result again (replay); any other call = a second invalidation (V14).
            return existing.IdempotencyKey == request.IdempotencyKey && existing.InvalidatedBy == actor.UserIdText
                ? Response<SignatureRecordDto>.Success(ESignatureMapping.ToDto(record, existing), 200)
                : ESignResponses.Fail<SignatureRecordDto>(ESignCodes.AlreadyInvalidated, 409, "The signature is already invalidated.");
        }

        if (!ESignatureRules.ReasonOk(request.Reason))
        {
            return ESignResponses.Fail<SignatureRecordDto>(ESignCodes.ReasonTooShort, 400, "The reason must be 10–1000 characters.");
        }

        if (string.IsNullOrWhiteSpace(request.Ticket))
        {
            return ESignResponses.Fail<SignatureRecordDto>(ESignCodes.TicketRequired, 400, "A re-authentication ticket is required.");
        }

        var consumed = await _tickets.ConsumeAsync(request.Ticket, actor.TenantId, actor.UserId, "invalidate", ct);
        if (consumed.Outcome == TicketConsumeOutcome.Unavailable)
        {
            return ESignResponses.Fail<SignatureRecordDto>(ESignCodes.ReauthUnavailable, 503, "Re-authentication could not be checked.");
        }

        if (consumed.Outcome == TicketConsumeOutcome.Invalid)
        {
            return ESignResponses.Fail<SignatureRecordDto>(ESignCodes.TicketInvalid, 403, "The re-authentication ticket is not valid.");
        }

        if (consumed.Kind != ESignatureReauthKind.Password)
        {
            return ESignResponses.Fail<SignatureRecordDto>(ESignCodes.ReauthRequired, 403, "Invalidation always requires your password.");
        }

        var invalidation = new ESignatureInvalidation
        {
            TenantId = actor.TenantId,
            CreatedBy = actor.UserIdText,
            SignatureId = record.Id,
            Reason = request.Reason!.Trim(),
            InvalidatedBy = actor.UserIdText,
            InvalidatedByName = actor.Name,
            InvalidatedAtUtc = DateTime.UtcNow,
            TicketRef = ESignTicketRef.Of(request.Ticket),
            IdempotencyKey = request.IdempotencyKey!
        };

        try
        {
            await _transactions.ExecuteAsync(async (session, txCt) =>
            {
                await _invalidations.InsertAsync(session, invalidation, txCt);

                await ESignatureAuditIntent.EnqueueAsync(_audit, session, actor.TenantId, ESignAuditEvents.Invalidated, AuditOperation.Revoke,
                    "ESignatureRecord", record.Id, AuditActorType.TenantUser, actor.UserId,
                    new Dictionary<string, object?>
                    {
                        ["signatureNumber"] = record.SignatureNumber,
                        ["invalidationId"] = invalidation.Id,
                        ["reasonLength"] = invalidation.Reason.Length
                    }, txCt);
                return true;
            }, ct);
        }
        catch (ESignatureDuplicateException)
        {
            return ESignResponses.Fail<SignatureRecordDto>(ESignCodes.AlreadyInvalidated, 409, "The signature is already invalidated.");
        }
        catch (PlatformTransactionUnavailableException)
        {
            return ESignResponses.Fail<SignatureRecordDto>(ESignCodes.ReauthUnavailable, 503, "The invalidation could not be recorded now.");
        }

        return Response<SignatureRecordDto>.Success(ESignatureMapping.ToDto(record, invalidation), 201);
    }
}

/// <summary>§4.24 — the export screen's table: visibility-scoped search plus a computed verification status per row.</summary>
public sealed class SearchSignaturesQueryHandler : IRequestHandler<SearchSignaturesQuery, Response<PageDto<SignatureSearchRowDto>>>
{
    private readonly IESignActorAccessor _actors;
    private readonly IESignatureVisibility _visibility;
    private readonly IESignatureRecordRepository _records;
    private readonly IESignatureInvalidationRepository _invalidations;
    private readonly IESignatureVerifier _verifier;

    public SearchSignaturesQueryHandler(IESignActorAccessor actors, IESignatureVisibility visibility, IESignatureRecordRepository records,
        IESignatureInvalidationRepository invalidations, IESignatureVerifier verifier)
    {
        _actors = actors;
        _visibility = visibility;
        _records = records;
        _invalidations = invalidations;
        _verifier = verifier;
    }

    public async Task<Response<PageDto<SignatureSearchRowDto>>> Handle(SearchSignaturesQuery request, CancellationToken ct)
    {
        var actor = _actors.Current();
        if (actor is null)
        {
            return ESignResponses.Actor<PageDto<SignatureSearchRowDto>>();
        }

        if (request.FromUtc is { } from && request.ToUtc is { } to && from > to)
        {
            return ESignResponses.Validation<PageDto<SignatureSearchRowDto>>("fromUtc must not be after toUtc.");
        }

        var page = Math.Max(1, request.Page);
        var pageSize = Math.Clamp(request.PageSize <= 0 ? 25 : request.PageSize, 1, 100);
        var search = new ESignatureSearch(request.FromUtc, request.ToUtc, request.Module, request.SignerUserId, request.MeaningCode,
            request.LegalEntityId, await _visibility.VisibleLegalEntitiesAsync(actor, ct), actor.UserIdText);
        var (items, total) = await _records.SearchAsync(actor.TenantId, search, (page - 1) * pageSize, pageSize, ct);
        var invalidations = await _invalidations.GetBySignaturesAsync(actor.TenantId, items.Select(r => r.Id).ToList(), ct);

        var rows = new List<SignatureSearchRowDto>(items.Count);
        foreach (var record in items)
        {
            var invalidation = invalidations.GetValueOrDefault(record.Id);
            var verification = await _verifier.VerifyAsync(record, invalidation, ct);
            rows.Add(new SignatureSearchRowDto(ESignatureMapping.ToDto(record, invalidation), verification.Status));
        }

        return Response<PageDto<SignatureSearchRowDto>>.Success(new PageDto<SignatureSearchRowDto>(rows, total, page, pageSize));
    }
}
