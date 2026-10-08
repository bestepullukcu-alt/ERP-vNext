using Diten.Platform.Application.Common;
using Diten.Platform.Application.Contracts.Audit;
using MediatR;

namespace Diten.Platform.Application.Features.ESignature;

// MOD-0022 E2 — commands and queries (ESIGN-BUNDLE §4.4–4.16, 4.20–4.26).
//
// AUDIT. Every state-changing command owns its audit: the handler writes the MOD-0021 intent into the outbox INSIDE the
// business transaction (ITransactionOwnedAuditCommand; authorized in AuditBehavior through ESignatureTransactionAudit), so
// a signature, the request it fulfils and its audit event commit together. Verification changes nothing and writes its
// own event with the computed status (IAuditExcludedRequest). Queries are not audited.
//
// ⚠ ToString of every record that carries a ticket or image bytes is overridden: MediatR logging behaviors and exception
// paths may print a request, and a ticket or a PNG must never reach a log line (ESIGN-BUNDLE rule 8).

/// <summary>The commands whose audit is written by their own handler in their own transaction (AuditBehavior allowlist).</summary>
public static class ESignatureTransactionAudit
{
    private static readonly HashSet<Type> Owners =
    [
        typeof(SignSubjectCommand), typeof(InvalidateSignatureCommand),
        typeof(CreateSignatureRequestCommand), typeof(RejectSignatureRequestCommand), typeof(CancelSignatureRequestCommand),
        typeof(ExpireSignatureRequestsCommand),
        typeof(SavePolicyCommand), typeof(RevertLegalEntityPolicyCommand),
        typeof(CreateMeaningCommand), typeof(UpdateMeaningCommand), typeof(SetMeaningActiveCommand),
        typeof(RegisterSignableActionsCommand),
        typeof(CreateExportCommand)
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

public sealed record VerifySignatureCommand(Guid Id) : IRequest<Response<VerificationResultDto>>, IAuditExcludedRequest;

public sealed record InvalidateSignatureCommand(Guid Id, string? Ticket, string? Reason, string? IdempotencyKey)
    : IRequest<Response<SignatureRecordDto>>, ITransactionOwnedAuditCommand
{
    public override string ToString() => $"InvalidateSignatureCommand {{ Id = {Id} }}";
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

public sealed record CreateSignatureRequestCommand(
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

public sealed record RejectSignatureRequestCommand(Guid Id, string? Reason, string? IdempotencyKey)
    : IRequest<Response<SignatureRequestDto>>, ITransactionOwnedAuditCommand;

public sealed record CancelSignatureRequestCommand(Guid Id, string? Reason) : IRequest<Response<SignatureRequestDto>>, ITransactionOwnedAuditCommand;

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

/// <summary>S2S (CT-D12): a consumer module registers its signable actions in the global catalog.</summary>
public sealed record RegisterSignableActionsCommand(string? Module, IReadOnlyList<SignableActionInput>? Actions)
    : IRequest<Response<RegisterActionsResultDto>>, ITransactionOwnedAuditCommand;

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
