namespace Diten.InventoryService.Application.Features.Quarantine.Ports;

/// <summary>The effective MOD-0022 policy of one signable action for one decision (ESIGN-BUNDLE §4.15 <c>EffectivePolicy</c>).</summary>
public sealed record ESignEffectivePolicy(bool Required, string? MeaningCode);

/// <summary>
/// The parts of a MOD-0022 <c>SignatureRecord</c> (ESIGN-BUNDLE §4.5) 0175 checks. 0175 keeps none of it — only the id
/// (KQ-G); this record lives for the duration of one submit.
/// </summary>
public sealed record ESignSignatureInfo(
    string Id,
    string? SignerUserId,
    string? Module,
    string? SubjectType,
    string? SubjectId,
    string? MeaningCode,
    string? SubjectHash,
    bool Invalidated);

public sealed record ESignCallResult<T>(WorkflowCallOutcome Outcome, T? Value, string? ReasonCode)
{
    public bool IsOk => Outcome == WorkflowCallOutcome.Ok;
}

/// <summary>
/// MOD-0022 over the Gateway (<c>/api/v1/esign/*</c>) with the CALLER's bearer token — the same rule as the Workflow client
/// (no service token reads a user's signature).
/// </summary>
public interface IQuarantineESignClient
{
    /// <summary><c>GET /api/v1/esign/policy/effective</c> for the decision subject (key <c>esign.signatures.sign</c>).</summary>
    Task<ESignCallResult<ESignEffectivePolicy>> EffectivePolicyAsync(string actionCode, Guid decisionId, CancellationToken cancellationToken = default);

    /// <summary><c>GET /api/v1/esign/signatures/{id}</c> (key <c>esign.signatures.read</c>).</summary>
    Task<ESignCallResult<ESignSignatureInfo>> SignatureAsync(string signatureId, CancellationToken cancellationToken = default);
}

/// <summary>Pack §8 signable-action rows (MOD-0022 CT-D12): registered by 0175 at start-up, changed by the tenant in the policy.</summary>
public static class QuarantineSignableActions
{
    public const string Module = "quarantine";
    public const string SubjectType = "quarantine.decision";
    public const string FieldListVersion = "qd-v1";

    public sealed record Action(string Code, string DefaultMeaning, string DefaultVisualMode, bool DefaultRequired, IReadOnlyDictionary<string, string> Names);

    private static Dictionary<string, string> Names(string en, string tr, string fr, string es, string zh, string ar, string ru) => new()
    {
        ["en"] = en, ["tr"] = tr, ["fr"] = fr, ["es"] = es, ["zh"] = zh, ["ar"] = ar, ["ru"] = ru
    };

    public static readonly IReadOnlyList<Action> All =
    [
        new("quarantine.hold.release", QuarantineContract.MeaningRelease, "savedAccepted", true,
            Names("Quarantine release", "Karantinadan serbest bırakma", "Libération de quarantaine", "Liberación de cuarentena", "隔离放行", "الإفراج من الحجر", "Выпуск из карантина")),
        new("quarantine.hold.reject", QuarantineContract.MeaningApprove, "drawRequired", true,
            Names("Quarantine rejection", "Karantina reddi", "Rejet de quarantaine", "Rechazo de cuarentena", "隔离拒收", "رفض الحجر", "Отклонение карантина")),
        new("quarantine.hold.block", QuarantineContract.MeaningApprove, "optional", true,
            Names("Stock block", "Stok blokajı", "Blocage du stock", "Bloqueo de stock", "库存冻结", "حظر المخزون", "Блокировка запаса")),
        new("quarantine.blocked.dispose", QuarantineContract.MeaningApprove, "drawRequired", true,
            Names("Blocked stock disposition", "Bloke stok disposition kararı", "Décision sur stock bloqué", "Disposición de stock bloqueado", "冻结库存处置", "التصرف في المخزون المحظور", "Решение по заблокированному запасу")),
        new("quarantine.blocked.unblock", QuarantineContract.MeaningRelease, "none", true,
            Names("Unblock stock", "Blokeyi kaldırma", "Déblocage du stock", "Desbloqueo de stock", "解除冻结", "رفع الحظر عن المخزون", "Снятие блокировки")),
        new("quarantine.hold.create", QuarantineContract.MeaningApprove, "none", false,
            Names("Put into quarantine", "Karantinaya alma", "Mise en quarantaine", "Puesta en cuarentena", "放入隔离", "الوضع في الحجر", "Помещение в карантин")),
        new("quarantine.lot.release", QuarantineContract.MeaningRelease, "savedAccepted", true,
            Names("Lot release", "Lot serbest bırakma", "Libération du lot", "Liberación del lote", "批次放行", "الإفراج عن التشغيلة", "Выпуск партии"))
    ];
}
