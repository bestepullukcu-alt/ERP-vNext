# SupplyChain l10n - MOD-0190 SandopPlans / MOD-0192 CapacityPlans (G6 skeleton) - translator notes

Scope: `SandopPlans/SandopPlansIndex.{en,tr,fr,es,zh,ar,ru}.resx` and `CapacityPlans/CapacityPlansIndex.{...}.resx`.
`en` is the wire source. Every non-en value below was written to the {Claims, Returns} register but the domain terms in
section 1 were NOT confirmed by a native translator. Nothing was left as an English copy; the entries are best-effort and
listed so a human can confirm or replace them. (Same routing as Part B of the SupplyChain l10n owner decision.)

## 1. Deferred domain terms (best-effort translation, needs native review)

| term (en) | key(s) | tr | fr | es | zh | ar | ru | doubt |
|---|---|---|---|---|---|---|---|---|
| S&OP | SandopPlansTitle, PageDescription, ErrSandop* | S&OP (acronym kept) | S&OP | S&OP | S&OP | S&OP | S&OP | Kept as the acronym in all 7. Whether to expand it ("Satış ve Operasyon Planlama" etc.) is a product call. |
| sign-off / sign-offs | SignOffsHeader, RecordSignOff, SignOffRecorded, ErrSandopSignOffStateConflict, ErrSignOffAlreadyRecorded, SignOffFinalNote | Onay / Onaylar | validation(s) | validación(es) | 签核 | اعتماد / الاعتمادات | согласование(я) | No single standard word. Each choice avoids colliding with the Approved status/decision, but tr "Onay" overlaps "Onaylandı" (Approved). Strongest doubt. |
| snapshot | CurrentSnapshot, SnapshotsHeader, CaptureSnapshot, CapturedAt, SignOffSnapshot, ErrInvalidSnapshotReference | anlık görüntü | instantané | instantánea | 快照 | لقطة | снимок | Common IT terms; business-user register unconfirmed. "Capture snapshot": ru "Зафиксировать снимок", zh "捕获快照" (could be "创建快照"). |
| horizon | HorizonStart, HorizonEnd, ValidationHorizonOrder | Ufuk | horizon | horizonte | 计划期 | الأفق | горизонт | Planning horizon. zh uses 计划期 (planning period), ar/tr/ru/fr/es use the literal "horizon" noun. |
| provenance | Provenance | Veri kökeni | Provenance | Procedencia | 数据溯源 | منشأ البيانات | Происхождение данных | Data-lineage sense; no short settled term. |
| scenario | CapacityPlans scenario keys | senaryo | scénario | escenario | 方案 | سيناريو | сценарий | zh 方案 vs 情景 is the doubt. |
| bottleneck | Bottlenecks | Darboğaz | goulot d'étranglement | cuello de botella | 瓶颈 | الاختناق | узкое место | Standard in all 6; low doubt. |
| shortfall | BottleneckShortfall | Eksik miktar | Déficit | Déficit | 缺口 | العجز | Дефицит | Medium. |
| Finite / Infinite (evaluation mode) | ModeFinite, ModeInfinite | Sonlu / Sonsuz | Finie / Infinie | Finita / Infinita | 有限 / 无限 | محدودة / غير محدودة | Конечная / Бесконечная | APS "finite/infinite capacity" mode. Adjectives agree with "capacité/capacidad/мощность"; the noun is not in the value. Strong doubt for ru (could be "С ограничением мощности" / "Без ограничений"). |
| role enum (DemandPlanning, SupplyPlanning, Finance, Operations, Executive) | Role* | Talep planlama, Tedarik planlama, Finans, Operasyonlar, Üst yönetim | Planification de la demande / de l'approvisionnement, Finance, Opérations, Direction | Planificación de la demanda / del suministro, Finanzas, Operaciones, Dirección ejecutiva | 需求计划, 供应计划, 财务, 运营, 管理层 | تخطيط الطلب, تخطيط التوريد, المالية, العمليات, الإدارة التنفيذية | Планирование спроса / поставок, Финансы, Операции, Руководство | "Supply" is rendered differently per language (tedarik / approvisionnement / suministro / 供应 / التوريد / поставки). "Executive" = a role, not a verb. |
| idempotency (user text) | ErrIdempotencyKeyReused | - | - | - | - | - | - | No term needed: the {Claims, Returns} canon sentence ("already received with different values ... open a new form") is reused verbatim in all 7. |
| supply input reference | SupplyInputRefs, SupplyInputRefsCount, Add/RemoveSupplyInputRef | Tedarik girdi referansı | Références des entrées d'approvisionnement | Referencias de entradas de suministro | 供应输入引用 | مراجع مدخلات التوريد | Ссылки на входные данные по поставкам | Technical phrase from the contract (`supplyInputRefs`). |
| Source (system) vs Resource | RefSource, ConstraintSource vs RefResource*, AdjResourceRef, Bottleneck* | Source = "Kaynak sistem", Resource = "Kaynak" | Source / Ressource | Origen / Recurso | 来源 / 资源 | المصدر / المورد | Источник / Ресурс | In tr both English words are "kaynak"; "Kaynak sistem" was chosen for the contract's `source` to keep them apart. |
| Decided by | DecidedBy | Karar veren | Décidé par | Decidido por | 决定人 | صاحب القرار | Принял решение | Value is a user UUID in this slice (no lookup), the label is still "by". |

## 2. Non-en values equal to en (legitimate cognates, not placeholders)

Only fr, and only genuine French words that are spelled the same as English:

- `Provenance` (both modules), `RefSource` / `ConstraintSource` ("Source"), `ConstraintVersion` ("Version"), `RoleFinance` ("Finance").
- Nothing else is equal to en in any language (checked by script: tr/es/zh/ar/ru have zero en-equal values).

## 3. Convention notes / deviations the integrator should know

1. **Title key name.** The task listed the key as `Title`. The l10n rule requires `[ModuleName]Title`, so the keys are
   `SandopPlansTitle` and `CapacityPlansTitle` (this also matches UI-SCOPE-0190/0192 §7). If the views are written against a
   bare `Title`, rename in the resx + bridge + loader or change the view; the rename is mechanical.
2. **No `AddNew[ModuleName]` / `ActionsHeader` keys.** Both modules are workspaces with no list and no actions column; the
   "add new" concept is `CreatePlan` (and `CreateScenario`). Add `ActionsHeader` later only if a table gains an actions column.
3. **Sentence case.** The `en` values follow the task text (sentence case, e.g. "Create plan"). The Claims/Returns `en` values are
   Title Case ("Create Claim"). Non-en values follow the target language's sentence case. Owner can ask for Title Case as one pass.
4. **`SharedLocalizer` has no `Create`, `Actions` or `SupportReference` key**, so they were not routed to Shared. `SupportReference`
   is a module key (as in Claims/Returns/Loads). The bridge pulls these from SharedResource (all verified present in 7 languages):
   LegalEntity, LegalEntityUnavailable, LegalEntityNone, LegalEntityRequired, Status, Save, Cancel, Close, Copy, Copied, Loading,
   Back, SelectPlaceholder, ErrorOccurred, RecordSaved, AreYouSure. `Search`, `Active`, `Passive` were not emitted (no such control
   on these surfaces) - add them to the bridge AND `requiredKeys` together if a view needs them.
5. **Keys added beyond the supplied list** (needed by the approved surface, same conventions):
   both: `PlanIdHelp`, `ValidationHorizonOrder`, `PlanCreated`, `RequestPending`, `Retry`, `NotProvided`, `StatusApproved`,
   `StatusArchived`. SandopPlans: `CurrentMarker`, `AddSupplyInputRef`, `RemoveSupplyInputRef`, `SignOffNeedsSnapshot`,
   `SignOffFinalNote`, `StatusRejected`, `SnapshotCaptured`, `SignOffRecorded`. CapacityPlans: `ScenarioIdHelp`, `DecimalHelp`,
   `AddConstraintRef`, `RemoveConstraintRef`, `AddAdjustment`, `RemoveAdjustment`, `AddResourceRef`, `RemoveResourceRef`,
   `EvaluationId`, `EvaluationPending`, `Bottlenecks`, `NoBottlenecks`, `StatusEvaluating`, `StatusReady`, `StatusEvaluated`,
   `ScenarioCreated`, `EvaluationSubmitted`. The extra `Status*` keys cover every enum value the contract can return
   (SandopPlan: Draft/InReview/Approved/Rejected/Archived; CapacityPlan: Draft/Evaluating/Ready/Approved/Archived; Scenario:
   Draft/Evaluating/Evaluated/Archived; Evaluation: Accepted/Running/Completed/Failed) so no server status renders as a raw code.
   Create endpoints return 201 only (no replay status in the contract), so no `*Replayed` keys.
6. **Status keys are shared across entities** (e.g. CapacityPlans `StatusDraft` serves plan and scenario; `StatusApproved` is the
   plan status, distinct from SandopPlans `DecisionApproved`). Grammatical gender in fr/es/ru/ar follows the dominant noun
   (plan/scenario masculine, evaluation feminine for Accepted/Completed/Failed); a genuinely gender-neutral form was not available.
7. **Error wording.** `ErrForbidden` = "You do not have permission for this action." (owner ruling A2) and `ErrInternalError` =
   "An unexpected error occurred." as instructed. `ErrInvalidRequest`, `ErrUnauthenticated`, `ErrIdempotencyKeyReused` reuse the
   {Claims, Returns} translations. The ErrInvalidCorrelationId / ErrCommitResultUnresolved / ErrDependencyUnavailable sentences
   are new (no earlier module has the first two) - same retry-the-same-request register as the Returns/Loads wording.
8. **Bridge/loader.** `requiredKeys` lists every key the bridge emits (shared + module), in the same order - including the four
   `LegalEntity*` keys that Claims' loader omits - verified by script (91 / 109 keys). RTL is a view/CSS concern: nothing in
   the resx is direction-specific; ar values contain no embedded Latin tokens except `S&OP` and `UUID`.
9. **Not built.** `dotnet build` was not run (skeleton only; the bridge uses the exact Claims Razor shape).
