using System.Text.Json;

namespace Diten.Platform.API.Models.ESignature;

// MOD-0022 E2 — wire request bodies (ESIGN-BUNDLE §4). ToString never prints a ticket or image bytes (rule 8): model
// binding errors and request logging may stringify a body.

public sealed record ESignSubjectRefRequest(string? Module, string? SubjectType, string? SubjectId);

public sealed record ESignDrawingRequest(string? PngBase64, JsonElement? Strokes)
{
    public override string ToString() => $"ESignDrawingRequest {{ PngBase64Length = {PngBase64?.Length ?? 0} }}";
}

public sealed record ESignAppearanceRequest(string? Type, Guid? SampleId, ESignDrawingRequest? Drawing);

public sealed record SignSubjectRequest(
    string? Ticket,
    ESignSubjectRefRequest? Subject,
    string? ActionCode,
    string? MeaningCode,
    string? Note,
    ESignAppearanceRequest? Appearance,
    Guid? RequestId,
    Dictionary<string, string?>? Context,
    string? IdempotencyKey)
{
    public override string ToString() =>
        $"SignSubjectRequest {{ Subject = {Subject?.Module}/{Subject?.SubjectType}/{Subject?.SubjectId}, MeaningCode = {MeaningCode}, Appearance = {Appearance?.Type} }}";
}

public sealed record InvalidateSignatureRequest(string? Ticket, string? Reason, string? IdempotencyKey)
{
    public override string ToString() => "InvalidateSignatureRequest { }";
}

public sealed record CreateSignatureRequestRequest(
    ESignSubjectRefRequest? Subject,
    string? ActionCode,
    string? MeaningCode,
    Guid? SignerUserId,
    DateTime? DueAtUtc,
    string? Note,
    string? IdempotencyKey);

public sealed record ESignReasonRequest(string? Reason, string? IdempotencyKey);

public sealed record PolicyRowRequest(string? ActionCode, bool Required, string? MeaningCode, string? VisualMode, string? SecondFactor);

public sealed record SavePolicyRequest(
    List<PolicyRowRequest>? Rows,
    bool ReauthEvery,
    int ReauthWindowMinutes,
    bool? SampleApprovalRequired,
    string? Reason,
    int? ExpectedVersion);

public sealed record CreateMeaningRequest(string? Code, Dictionary<string, string>? Names, string? Description);

public sealed record UpdateMeaningRequest(Dictionary<string, string>? Names, string? Description);

public sealed record ExportFilterRequest(DateTime? FromUtc, DateTime? ToUtc, string? Module, string? SignerUserId, string? MeaningCode, Guid? LegalEntityId);

public sealed record CreateExportRequest(string? Format, ExportFilterRequest? Filter, string? IdempotencyKey);

// E3 — §4.17 createSample body. ToString never prints the ticket or the PNG.
public sealed record CreateSampleRequest(string? Ticket, string? Kind, string? PngBase64, JsonElement? Strokes, string? IdempotencyKey)
{
    public override string ToString() => $"CreateSampleRequest {{ Kind = {Kind}, PngBase64Length = {PngBase64?.Length ?? 0} }}";
}

public sealed record SignableActionRequest(
    string? ActionCode,
    Dictionary<string, string>? Names,
    bool DefaultRequired,
    string? DefaultMeaningCode,
    string? DefaultVisualMode);

public sealed record RegisterSignableActionsRequest(string? Module, List<SignableActionRequest>? Actions);
