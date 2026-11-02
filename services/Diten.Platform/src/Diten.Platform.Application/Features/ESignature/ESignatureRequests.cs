using Diten.Platform.Application.Common;
using Diten.Platform.Application.Contracts.Audit;
using Diten.Platform.Domain.Enums;
using MediatR;

namespace Diten.Platform.Application.Features.ESignature;

// MOD-0022 E2 — commands and queries (ESIGN-BUNDLE §4.4–4.16, 4.20–4.26).
//
// AUDIT. Every state-changing command owns its audit: the handler writes the MOD-0021 intent into the outbox INSIDE the
// business transaction (ITransactionOwnedAuditCommand; authorized in AuditBehavior through ESignatureTransactionAudit), so
// a signature, the request it fulfils and its audit event commit together. Two commands are audited by the central
// AuditBehavior instead (IAuditableCommand + IAuditMetadataProvider, AUD-001 §5 path a), so a refusal is recorded too
// (AUD-001 §3): verification, which changes no record, and the platform-global signable-action catalog registration,
// which has no tenant transaction to write an intent into. Queries are not audited.
//
// The five commands that share their name with a MOD-0029-FU23 document-signature command carry "ESignature" in theirs
// (VerifyESignatureCommand, …): the audit ledger tells commands apart by type name.
//
// ⚠ ToString of every record that carries a ticket or image bytes is overridden: MediatR logging behaviors and exception
// paths may print a request, and a ticket or a PNG must never reach a log line (ESIGN-BUNDLE rule 8).

/// <summary>The commands whose audit is written by their own handler in their own transaction (AuditBehavior allowlist).</summary>
public static class ESignatureTransactionAudit
{
    private static readonly HashSet<Type> Owners =
    [
        typeof(SignSubjectCommand), typeof(InvalidateESignatureCommand),
        typeof(CreateESignatureRequestCommand), typeof(RejectESignatureRequestCommand), typeof(CancelESignatureRequestCommand),
        typeof(ExpireSignatureRequestsCommand),
        typeof(SavePolicyCommand), typeof(RevertLegalEntityPolicyCommand),
        typeof(CreateMeaningCommand), typeof(UpdateMeaningCommand), typeof(SetMeaningActiveCommand),
        typeof(CreateExportCommand),
        // E3 — samples
        typeof(CreateSampleCommand), typeof(DecideSampleCommand), typeof(DeactivateSampleCommand), typeof(WithdrawSampleCommand)
    ];

    public static bool Owns(Type requestType) => Owners.Contains(requestType);

    public static IReadOnlyCollection<Type> All => Owners;
}

// ── signatures ───────────────────────────────────────────────────────────────────────────────────────────────────────

public sealed record SignAppearanceInput(string? Type, Guid? SampleId, string? PngBase64, string? StrokesJson);

public sealed record SignSubjectCommand(
    string? Ticket,
    string? Module,
    string? SubjectType,
    string? SubjectId,
    string? ActionCode,
    string? MeaningCode,
    string? Note,
    SignAppearanceInput? Appearance,
    Guid? RequestId,
    IReadOnlyDictionary<string, string?>? Context,
    string? IdempotencyKey) : IRequest<Response<SignatureRecordDto>>, ITransactionOwnedAuditCommand
{
    public override string ToString() =>
        $"SignSubjectCommand {{ Subject = {Module}/{SubjectType}/{SubjectId}, MeaningCode = {MeaningCode}, ActionCode = {ActionCode}, RequestId = {RequestId}, Appearance = {Appearance?.Type} }}";
}

public sealed record GetSignatureQuery(Guid Id) : IRequest<Response<SignatureRecordDto>>;

public sealed record ListSignaturesBySubjectQuery(string? Module, string? SubjectType, string? SubjectId) : IRequest<Response<IReadOnlyList<SignatureRecordDto>>>;

/// <summary>§4.7 — audited by AuditBehavior on every run, a refused one (404) included; the record is never written.</summary>
public sealed record VerifyESignatureCommand(Guid Id) : IRequest<Response<VerificationResultDto>>, IAuditableCommand, IAuditMetadataProvider
{
    public AuditRequestMetadata GetAuditMetadata() =>
        new(AuditCategory.ESignature, AuditOperation.Execute, "ESignatureRecord", EntityId: Id, SourceModule: ESignAuditEvents.SourceModule,
            Metadata: new Dictionary<string, object?> { ["event"] = ESignAuditEvents.Verified });
}

public sealed record InvalidateESignatureCommand(Guid Id, string? Ticket, string? Reason, string? IdempotencyKey)
    : IRequest<Response<SignatureRecordDto>>, ITransactionOwnedAuditCommand
{
    public override string ToString() => $"InvalidateESignatureCommand {{ Id = {Id} }}";
}

public sealed record SearchSignaturesQuery(
    DateTime? FromUtc,
    DateTime? ToUtc,
    string? Module,
    string? SignerUserId,
    string? MeaningCode,
    Guid? LegalEntityId,
    int Page,
    int PageSize) : IRequest<Response<PageDto<SignatureSearchRowDto>>>;

// ── requests ─────────────────────────────────────────────────────────────────────────────────────────────────────────

public sealed record CreateESignatureRequestCommand(
    string? Module,
    string? SubjectType,
    string? SubjectId,
    string? ActionCode,
    string? MeaningCode,
    Guid? SignerUserId,
    DateTime? DueAtUtc,
    string? Note,
    string? IdempotencyKey) : IRequest<Response<SignatureRequestDto>>, ITransactionOwnedAuditCommand;

public sealed record ListSignatureRequestsQuery(string? Scope, string? Module, string? Status, DateTime? FromUtc, DateTime? ToUtc, int Page, int PageSize)
    : IRequest<Response<SignatureRequestPageDto>>;

public sealed record GetSignatureRequestQuery(Guid Id) : IRequest<Response<SignatureRequestDto>>;

public sealed record RejectESignatureRequestCommand(Guid Id, string? Reason, string? IdempotencyKey)
    : IRequest<Response<SignatureRequestDto>>, ITransactionOwnedAuditCommand;

public sealed record CancelESignatureRequestCommand(Guid Id, string? Reason) : IRequest<Response<SignatureRequestDto>>, ITransactionOwnedAuditCommand;

/// <summary>System-only (Hangfire sweeper): Waiting requests past their due date → Expired, in the current tenant scope.</summary>
public sealed record ExpireSignatureRequestsCommand(int MaxItems) : IRequest<Response<int>>, ITransactionOwnedAuditCommand;

// ── policy ───────────────────────────────────────────────────────────────────────────────────────────────────────────

public sealed record GetPolicyQuery(Guid? LegalEntityId) : IRequest<Response<PolicyViewDto>>;

public sealed record GetEffectivePolicyQuery(string? ActionCode, string? Module, string? SubjectType, string? SubjectId) : IRequest<Response<EffectivePolicyDto>>;

public sealed record PolicyRowInput(string? ActionCode, bool Required, string? MeaningCode, string? VisualMode, string? SecondFactor);

/// <summary><see cref="LegalEntityId"/> null = the tenant default (PUT policy/tenant); otherwise the company override.</summary>
public sealed record SavePolicyCommand(
    Guid? LegalEntityId,
    IReadOnlyList<PolicyRowInput>? Rows,
    bool ReauthEvery,
    int ReauthWindowMinutes,
    bool? SampleApprovalRequired,
    string? Reason,
    int? ExpectedVersion) : IRequest<Response<PolicyDto>>, ITransactionOwnedAuditCommand;

public sealed record RevertLegalEntityPolicyCommand(Guid LegalEntityId, string? Reason) : IRequest<Response<NoContent>>, ITransactionOwnedAuditCommand;

public sealed record GetPolicyHistoryQuery(Guid? LegalEntityId) : IRequest<Response<IReadOnlyList<PolicyHistoryEntryDto>>>;

// ── meanings and actions ─────────────────────────────────────────────────────────────────────────────────────────────

public sealed record ListMeaningsQuery(bool ActiveOnly) : IRequest<Response<IReadOnlyList<MeaningDto>>>;

public sealed record CreateMeaningCommand(string? Code, IReadOnlyDictionary<string, string>? Names, string? Description)
    : IRequest<Response<MeaningDto>>, ITransactionOwnedAuditCommand;

public sealed record UpdateMeaningCommand(string Code, IReadOnlyDictionary<string, string>? Names, string? Description)
    : IRequest<Response<MeaningDto>>, ITransactionOwnedAuditCommand;

public sealed record SetMeaningActiveCommand(string Code, bool Active) : IRequest<Response<MeaningDto>>, ITransactionOwnedAuditCommand;

public sealed record ListSignableActionsQuery : IRequest<Response<IReadOnlyList<SignableActionDto>>>;

public sealed record SignableActionInput(
    string? ActionCode,
    IReadOnlyDictionary<string, string>? Names,
    bool DefaultRequired,
    string? DefaultMeaningCode,
    string? DefaultVisualMode);

/// <summary>
/// S2S (CT-D12): a consumer module registers its signable actions in the global catalog. The catalog has no tenant, so
/// the audit event is platform-global; AuditBehavior writes it, a refused registration included.
/// </summary>
public sealed record RegisterSignableActionsCommand(string? Module, IReadOnlyList<SignableActionInput>? Actions)
    : IRequest<Response<RegisterActionsResultDto>>, IAuditableCommand, IAuditMetadataProvider
{
    public AuditRequestMetadata GetAuditMetadata() =>
        new(AuditCategory.ESignature, AuditOperation.Update, "ESignableAction", SourceModule: ESignAuditEvents.SourceModule, IsPlatformGlobal: true,
            Metadata: new Dictionary<string, object?>
            {
                ["event"] = ESignAuditEvents.ActionsRegistered,
                ["module"] = Module,
                ["actionCodes"] = Actions is null ? null : string.Join(",", Actions.Select(a => a.ActionCode))
            });
}

// ── export ───────────────────────────────────────────────────────────────────────────────────────────────────────────

public sealed record CreateExportCommand(
    string? Format,
    DateTime? FromUtc,
    DateTime? ToUtc,
    string? Module,
    string? SignerUserId,
    string? MeaningCode,
    Guid? LegalEntityId,
    string? IdempotencyKey) : IRequest<Response<ESignatureExportFile>>, ITransactionOwnedAuditCommand;

public sealed record ListExportsQuery(int Page, int PageSize) : IRequest<Response<PageDto<ExportLogEntryDto>>>;

// ── E3 — signature samples (§4.17–4.19) ──────────────────────────────────────────────────────────────────────────────

/// <summary>§4.17 — save a sample with a PASSWORD ticket (intent sample). ToString never prints the ticket or the PNG.</summary>
public sealed record CreateSampleCommand(string? Ticket, string? Kind, string? PngBase64, string? StrokesJson, string? IdempotencyKey)
    : IRequest<Response<SampleDto>>, ITransactionOwnedAuditCommand
{
    public override string ToString() => $"CreateSampleCommand {{ Kind = {Kind}, PngBase64Length = {PngBase64?.Length ?? 0} }}";
}

public sealed record ListMySamplesQuery : IRequest<Response<IReadOnlyList<SampleDto>>>;

/// <summary>§4.17 image — the caller's own sample, an approver, or a reader of a signature that shows it; else 404.</summary>
public sealed record GetSampleImageQuery(Guid Id, bool CanApprove, bool CanReadSignatures) : IRequest<Response<SampleImageFile>>;

public sealed record DeactivateSampleCommand(Guid Id) : IRequest<Response<SampleDto>>, ITransactionOwnedAuditCommand;

public sealed record WithdrawSampleCommand(Guid Id) : IRequest<Response<SampleDto>>, ITransactionOwnedAuditCommand;

public sealed record ListPendingSamplesQuery : IRequest<Response<IReadOnlyList<SampleDto>>>;

/// <summary>§4.19 — approve (<see cref="Approve"/> = true) or reject (reason ≥ 10). Never by the owner (CT-D5).</summary>
public sealed record DecideSampleCommand(Guid Id, bool Approve, string? Reason) : IRequest<Response<SampleDto>>, ITransactionOwnedAuditCommand;

public sealed record ListSampleDecisionsQuery : IRequest<Response<IReadOnlyList<SampleDecisionLogDto>>>;
