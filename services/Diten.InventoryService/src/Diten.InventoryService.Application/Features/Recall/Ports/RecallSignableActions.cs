namespace Diten.InventoryService.Application.Features.Recall.Ports;

/// <summary>
/// MOD-0177 pack §8.8 signable actions (RECALL <c>x-signable-actions</c>), registered by InventoryService at start-up under module <c>recall</c>
/// (Platform enforces the <c>recall.</c> prefix) — WP-0177-R4. CT-R16: <c>recall.settings.update</c> is not required by default (the reason is
/// always mandatory); the tenant changes required / meaning / visual mode in the MOD-0022 policy. Names in the seven tenant languages
/// (Platform V19: every action carries all seven).
/// </summary>
public static class RecallSignableActions
{
    public const string Module = RecallContract.ESignModule;

    public const string CaseSubjectType = RecallContract.CaseSubjectType;

    /// <summary>The drill run (R5: served by the provider, dr-v1).</summary>
    public const string DrillRunSubjectType = "recall.drill-run";

    public const string SettingsChangeSubjectType = RecallContract.SettingsSubjectType;

    /// <summary>The field-list versions (pack §8.8, RECALL <c>x-signable-actions.subjects</c>). A changed field list needs a new version.</summary>
    public const string CaseFieldListVersion = "rc-v1";

    public const string DrillRunFieldListVersion = "dr-v1";

    public const string SettingsChangeFieldListVersion = "rs-v1";

    public const string ScopeApprove = "recall.scope.approve";
    public const string EffectivenessComplete = "recall.effectiveness.complete";
    public const string CaseClose = "recall.case.close";
    public const string DrillResult = "recall.drill.result";
    public const string SettingsUpdate = RecallContract.SettingsUpdateAction;

    public sealed record Action(string Code, string DefaultMeaning, string DefaultVisualMode, bool DefaultRequired, string SubjectType,
        IReadOnlyDictionary<string, string> Names);

    private static Dictionary<string, string> Names(string en, string tr, string fr, string es, string zh, string ar, string ru) => new()
    {
        ["en"] = en, ["tr"] = tr, ["fr"] = fr, ["es"] = es, ["zh"] = zh, ["ar"] = ar, ["ru"] = ru
    };

    public static readonly IReadOnlyList<Action> All =
    [
        new(ScopeApprove, RecallContract.DefaultMeaningCode, RecallContract.DefaultVisualMode, true, CaseSubjectType,
            Names("Approve the recall scope", "Geri çağırma kapsamını onayla", "Approuver le périmètre du rappel", "Aprobar el alcance de la retirada",
                "批准召回范围", "اعتماد نطاق الاستدعاء", "Утвердить объём отзыва")),
        new(EffectivenessComplete, RecallContract.DefaultMeaningCode, RecallContract.DefaultVisualMode, true, CaseSubjectType,
            Names("Complete the effectiveness check", "Etkinlik kontrolünü tamamla", "Terminer le contrôle d'efficacité",
                "Completar la verificación de eficacia", "完成有效性核查", "إكمال فحص الفعالية", "Завершить проверку эффективности")),
        new(CaseClose, RecallContract.DefaultMeaningCode, RecallContract.DefaultVisualMode, true, CaseSubjectType,
            Names("Close the recall case", "Geri çağırma vakasını kapat", "Clôturer le dossier de rappel", "Cerrar el caso de retirada",
                "关闭召回案例", "إغلاق حالة الاستدعاء", "Закрыть дело об отзыве")),
        new(DrillResult, RecallContract.DefaultMeaningCode, RecallContract.DefaultVisualMode, true, DrillRunSubjectType,
            Names("Sign the drill result", "Tatbikat sonucunu imzala", "Signer le résultat de l'exercice", "Firmar el resultado del simulacro",
                "签署演练结果", "توقيع نتيجة التمرين", "Подписать результат учения")),
        new(SettingsUpdate, RecallContract.DefaultMeaningCode, RecallContract.DefaultVisualMode, false, SettingsChangeSubjectType,
            Names("Change the recall settings", "Geri çağırma ayarlarını değiştir", "Modifier les paramètres de rappel",
                "Cambiar la configuración de retirada", "更改召回设置", "تغيير إعدادات الاستدعاء", "Изменить настройки отзыва"))
    ];

    /// <summary>The subject type of an action code (the policy and the binding read it; an unknown code has none).</summary>
    public static string? SubjectTypeOf(string actionCode) => All.FirstOrDefault(a => a.Code == actionCode)?.SubjectType;
}

/// <summary>The MOD-0022 effective policy of one signable action for one subject (ESIGN-BUNDLE §4.15 <c>EffectivePolicy</c>).</summary>
public sealed record RecallESignPolicy(bool Required, string? MeaningCode, string? VisualMode);

/// <summary>
/// The parts of a MOD-0022 <c>SignatureRecord</c> (ESIGN-BUNDLE §4.5, <c>esign.openapi.yaml:654-684</c>) 0177 checks (CT decision F3-A, the
/// 0176 F3 pattern). 0177 keeps none of it — only the id.
/// </summary>
public sealed record RecallESignSignature(
    string Id,
    string? SignerUserId,
    string? Module,
    string? SubjectType,
    string? SubjectId,
    string? ActionCode,
    string? MeaningCode,
    string? SubjectHash,
    DateTimeOffset? SignedAtUtc,
    bool Invalidated);

public enum RecallESignOutcome
{
    Ok,
    Forbidden,
    NotFound,
    Rejected,
    Unavailable
}

public sealed record RecallESignCall<T>(RecallESignOutcome Outcome, T? Value)
{
    public bool IsOk => Outcome == RecallESignOutcome.Ok && Value is not null;
}

/// <summary>
/// MOD-0022 over the Gateway (<c>/api/v1/esign/*</c>) with the CALLER's bearer token (the MOD-0175 rule: no service token reads a user's
/// signature). Only <c>getEffectivePolicy</c> and <c>getSignature</c> — never <c>verifySignature</c> (CT decision F3-A).
/// </summary>
public interface IRecallESignClient
{
    Task<RecallESignCall<RecallESignPolicy>> EffectivePolicyAsync(string actionCode, string subjectType, string subjectId, CancellationToken cancellationToken = default);

    Task<RecallESignCall<RecallESignSignature>> SignatureAsync(string signatureId, CancellationToken cancellationToken = default);
}

/// <summary>
/// The subject provider's first read (before the company scope exists): the company and, for a prepared settings change, the user who
/// prepared it — bound to the caller's TENANT (another tenant's subject does not exist: 404). R5: <c>recall.drill-run</c> (company only).
/// </summary>
public interface IRecallSubjectDirectory
{
    Task<(Guid LegalEntityId, Guid? Owner)?> SubjectAsync(Guid tenantId, string subjectType, Guid subjectId, CancellationToken cancellationToken = default);
}
