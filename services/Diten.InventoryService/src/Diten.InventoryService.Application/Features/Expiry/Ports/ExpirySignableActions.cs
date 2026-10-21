namespace Diten.InventoryService.Application.Features.Expiry.Ports;

/// <summary>
/// MOD-0176 pack §8.6 signable actions (MOD-0022 CT-D12), registered by InventoryService at start-up under module <c>expiry</c>
/// (Platform enforces the <c>expiry.</c> prefix). CT-F8: the default meaning of all four is <c>APPROVE</c> (the pack table's
/// <c>REVIEW</c> for <c>expiry.exception.fix</c> is superseded); the tenant changes required / meaning / visual mode in the policy.
/// </summary>
public static class ExpirySignableActions
{
    public const string Module = ExpiryContract.ESignModule;

    public const string ExceptionSubjectType = ExpiryContract.ExceptionSubjectType;

    public const string DateOverrideSubjectType = "expiry.date-override";

    /// <summary>The field-list version of <c>expiry.exception</c> subjects (pack §8.6). A changed field list needs a new version.</summary>
    public const string ExceptionFieldListVersion = "fx-v1";

    /// <summary>The field-list version of <c>expiry.date-override</c> subjects (pack §8.6).</summary>
    public const string DateOverrideFieldListVersion = "do-v1";

    public const string LotDateOverride = "expiry.lot-date.override";
    public const string ExceptionAccept = "expiry.exception.accept";
    public const string ExceptionFix = "expiry.exception.fix";
    public const string ExceptionCancel = "expiry.exception.cancel";

    public sealed record Action(string Code, string DefaultMeaning, string DefaultVisualMode, bool DefaultRequired, IReadOnlyDictionary<string, string> Names);

    private static Dictionary<string, string> Names(string en, string tr, string fr, string es, string zh, string ar, string ru) => new()
    {
        ["en"] = en, ["tr"] = tr, ["fr"] = fr, ["es"] = es, ["zh"] = zh, ["ar"] = ar, ["ru"] = ru
    };

    public static readonly IReadOnlyList<Action> All =
    [
        new(LotDateOverride, ExpiryContract.DefaultMeaningCode, ExpiryContract.DefaultVisualMode, true,
            Names("Lot date override", "Lot tarih sapması", "Dérogation de date du lot", "Desviación de fecha del lote", "批次日期偏差",
                "انحراف تاريخ التشغيلة", "Отклонение даты партии")),
        new(ExceptionAccept, ExpiryContract.DefaultMeaningCode, ExpiryContract.DefaultVisualMode, true,
            Names("Accept the exception with reason", "İstisnayı gerekçeyle kabul et", "Accepter l'exception avec justification",
                "Aceptar la excepción con justificación", "附理由接受例外", "قبول الاستثناء مع المبرر", "Принять исключение с обоснованием")),
        new(ExceptionFix, ExpiryContract.DefaultMeaningCode, ExpiryContract.DefaultVisualMode, true,
            Names("Close the exception as fixed", "İstisnayı düzeltildi olarak kapat", "Clore l'exception comme corrigée",
                "Cerrar la excepción como corregida", "将例外关闭为已纠正", "إغلاق الاستثناء كمصحَّح", "Закрыть исключение как исправленное")),
        new(ExceptionCancel, ExpiryContract.DefaultMeaningCode, ExpiryContract.DefaultVisualMode, true,
            Names("Cancel the exception", "İstisnayı iptal et", "Annuler l'exception", "Cancelar la excepción", "取消例外",
                "إلغاء الاستثناء", "Отменить исключение"))
    ];
}

/// <summary>The MOD-0022 effective policy of one signable action for one subject (ESIGN-BUNDLE §4.15 <c>EffectivePolicy</c>).</summary>
public sealed record ExpiryESignPolicy(bool Required, string? MeaningCode, string? VisualMode);

/// <summary>
/// The parts of a MOD-0022 <c>SignatureRecord</c> (ESIGN-BUNDLE §4.5, <c>esign.openapi.yaml:654-684</c>) 0176 checks (CT decision F3-A).
/// 0176 keeps none of it — only the id (KF-E).
/// WP-MVP1-FIX-LIVE2 (additive, display only): <see cref="SignerNameAtSign"/> — <c>signer.name</c>, the name MOD-0022 recorded at signing
/// (<c>signerNameAtSign</c> of the digest); <see cref="MeaningText"/> — <c>meaningText</c> as signed. Absent = null, never invented.
/// </summary>
public sealed record ExpiryESignSignature(
    string Id,
    string? SignerUserId,
    string? Module,
    string? SubjectType,
    string? SubjectId,
    string? ActionCode,
    string? MeaningCode,
    string? SubjectHash,
    DateTimeOffset? SignedAtUtc,
    bool Invalidated,
    string? SignerNameAtSign = null,
    string? MeaningText = null);

public enum ExpiryESignOutcome
{
    Ok,
    Forbidden,
    NotFound,
    Rejected,
    Unavailable
}

public sealed record ExpiryESignCall<T>(ExpiryESignOutcome Outcome, T? Value)
{
    public bool IsOk => Outcome == ExpiryESignOutcome.Ok && Value is not null;
}

/// <summary>
/// MOD-0022 over the Gateway (<c>/api/v1/esign/*</c>) with the CALLER's bearer token (the MOD-0175 rule: no service token reads a
/// user's signature). Only <c>getEffectivePolicy</c> and <c>getSignature</c> — never <c>verifySignature</c> (CT decision F3-A).
/// </summary>
public interface IExpiryESignClient
{
    Task<ExpiryESignCall<ExpiryESignPolicy>> EffectivePolicyAsync(string actionCode, string subjectType, string subjectId, CancellationToken cancellationToken = default);

    Task<ExpiryESignCall<ExpiryESignSignature>> SignatureAsync(string signatureId, CancellationToken cancellationToken = default);
}
