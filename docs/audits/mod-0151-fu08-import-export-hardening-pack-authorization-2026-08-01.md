# MOD-0151 FU08 — Import/Export Hardening — Pack Authorization

- **Tarih:** 2026-08-01
- **Modül:** MOD-0151 — Territory Management (`Diten.CrmService`)
- **Task tipi:** Module pack authorization / governance hizalama (**kod değil, runtime değil**)
- **Target file:** `execution/domains/commercial-suite/module-packs/MOD-0151-territory-management.md`
- **Owner:** module-pack-author
- **Verdict:** **PASS**

---

## 1. Preflight

- Bu task MOD-0151 module pack içinde **FU08 Import/Export Hardening** kapsamını yetkilendirme ve governance hizalama
  task'ıdır. **Kod yazma / runtime implementation task'ı değildir**; hiçbir servis, gateway, frontend veya test dosyası
  değiştirilmemiştir.
- Bu task **workflow değildir**, **approval değildir**, **ChangeRequest değildir**, **visit/route planning
  implementation değildir**. FU06 / workflow / approval / ChangeRequest işleri bilinçli olarak en sona bırakılmıştır.
- Otorite sırası korundu: Blueprint Excel > Module Pack > Domain Config > `AGENTS.md` > `.antigravity/rules/`.
- Referans desen: **MOD-0150 Contact Import/Export** (template → export → upload → **dry-run** → safe apply),
  `docs/audits/mod-0150-contact-import-export-task2-xlsx-upload-dryrun-apply.md`. FU08 bu deseni MOD-0151 nesnelerine
  uygular; **yeni bir import framework önerilmez**.

## 2. Dependency Confirmation

| Ön koşul | Durum | Kanıt |
|---|---|---|
| FU01 Backend Core | **PASS** | `mod-0151-fu01-contract-territory-model-node-backend-2026-07-23.md` |
| FU02 Territory UI / Model Viewer | **PASS** | pack §22 / FU02 closeout |
| FU02A Country + Business Unit Scope | **PASS** (2 follow-up ile) | `mod-0151-fu02a-country-business-unit-scope-selector-hardening-2026-07-25.md` |
| FU02B Lifecycle | **PASS** | `mod-0151-fu02b-live-smoke-closeout-retry-2-after-status-publish-2026-07-28.md` |
| FU03 Assignment Rules + Preview | **PASS** | pack §22.2 / FU03 closeout |
| FU04 / FU04A Resource Assignments | **operational** | pack §22.3 |
| FU04B Plan vs Current | **canlı zincir PASS** | `mod-0151-fu04b-resource-assignment-plan-vs-current-live-smoke-closeout-2026-07-31.md` |
| FU05 Account Assignment Apply + History | **PASS (90/90)** | `mod-0151-fu05-account-assignment-apply-history-live-smoke-closeout-2026-07-31.md` |
| FU05A CoverageSummary Model Lifecycle Guard | **PASS** | `mod-0151-fu05a-coverage-summary-model-lifecycle-guard-implementation-2026-07-31.md` |
| FU06 / workflow / approval / ChangeRequest | **bilinçli olarak ertelendi** | pack §22.1 FU06 boundary |

**Bağımlılık uyuşmazlığı bulundu ve giderildi (governance hizalama):** pack §22 FU tablosunda FU08'in `Depends On`
alanı **`FU05, FU07`** yazıyordu. FU08 bu task ile FU06/FU07'den **önce** yetkilendirildiği için bu satır çelişki
yaratıyordu. Analiz sonucu: import/export'un gerçek hard prerequisite'i **FU05 + FU05A**'dır (export edilecek/import
edilecek veri şekli ve guard'lar oradan gelir); **FU07 evidence pack import/export'un ön koşulu değildir** — ilişki
tersidir, FU07 geldiğinde FU08'in export yüzeyi evidence pack'e **girdi** olabilir. `Depends On` alanı
`FU05, FU05A` olarak düzeltildi ve gerekçe §22.1 authorization update bloğuna yazıldı.

## 3. Business Need Summary

Gerçek bir territory planı yüzlerce `TerritoryNode` ve binlerce `AccountTerritoryAssignment` içerir. Bugün MOD-0151'in
tek veri giriş yolu ekrandır; bu, ilk plan kurulumunu, yıllık yeniden planlamayı ve saha genişlemesini operasyonel
olarak sürdürülemez kılar. FU08'in amacı **büyük territory modellerini ve atamaları elle tek tek girmeden yönetilebilir
hale getirmektir**: kontrollü XLSX export → doldurulabilir şablon → dry-run doğrulama → güvenli apply → izlenebilir run
history.

Aynı zamanda FU08 en yüksek "sessiz veri bozma" riskini taşıyan FU'dur; bu yüzden yetkilendirme, kapsamı açmaktan çok
**sınırları çivilemeye** odaklanmıştır (bkz. §8/§9/§11).

## 4. Pack Changes

| Yer | Değişiklik |
|---|---|
| Frontmatter `runtime_code_scope` | **+`FU08-import-export-hardening`** (additive). FU01, FU02, FU02A, FU02B, FU03, FU04, FU04A, FU04B, FU05, FU05A **korundu** |
| Header banner | Başlık "FU08 scope update 2026-08-01"; yetkili FU listesine FU05A + FU08 eklendi; FU08 açıklama paragrafı eklendi; "yetkilendirilmemiş" listesinden import/export çıkarıldı, FU06/FU07/FU09 korundu; resource mutasyon sınırı "FU08 v1 resource **apply** içermez" notuyla netleştirildi |
| **§7.5b (yeni)** | `TerritoryImportRun` aggregate'i — **append-only**, update/delete komutu yok, ham dosya saklanmaz (yalnız hash) |
| §17 | **FU08 permission kararı** paragrafı (canonical `crm.territory.export` / `crm.territory.import`; katalog hazır değilse FU08-RBAC follow-up + `model.read`/`model.manage` fallback; fallback yetki genişletmez) |
| §18 | Sayfa #10 Import / Export satırı FU08 akışıyla genişletildi (dry-run hiçbir şey yazmaz; CoverageSummary/Plan vs Current yalnız export; resource apply yok) |
| §19 | API tablosuna FU08 endpoint seti: export · import-template · `import-file?dryRun=true` (varsayılan true) · **ayrı** `import-file/apply` · `import-runs` |
| §22.1 | **FU08 authorization update (2026-08-01)** bloğu + FU07 dependency reconciliation notu |
| **§22.5 (yeni)** | **FU08 — Import/Export Hardening**: allowed scope · import/export object scope tablosu · dry-run validation policy · safe apply policy · import run history policy · account/resource import policy · permission decision · contract flags · test expectations · boundary · explicit exclusions |
| §22 FU tablosu | FU08 satırı yeniden yazıldı (scope detayı, `Depends On: FU05, FU05A`, genişletilmiş out-of-scope) |
| §23 | **F20** follow-up'ı eklendi (FU08 + alt follow-up'lar FU08-RBAC ve FU08A); **R7** (import ile guard bypass'ı) ve **R8** (sessiz toplu overwrite) riskleri eklendi |
| §24 | FU08 acceptance criteria maddesi eklendi |
| §25 | FU08 implementation prompt'u (#3), FU08-RBAC follow-up'ı (#4) ve **FU08A** authorization önerisi (#5) eklendi |

**Silinen / daraltılan hiçbir mevcut scope yoktur.**

## 5. FU08 Authorized Scope

1. **Export (read-only, XLSX).** Territory Model metadata · Territory Node'lar · hiyerarşi · Business Unit scope'ları ·
   Assignment Rule'lar · Account Assignment current + history · CoverageSummary · Resource Assignment current + history ·
   Plan vs Current. Satır bazlı açık kolonlar; tenant **claim'den**; çıktıda/payload'da `TenantId` yok; Gateway
   üzerinden, direct 5061 yok.
2. **Import template generation.** Çok-sheet XLSX: `Model` · `Nodes` · `AssignmentRules` · `AccountAssignments` ·
   `ResourceAssignments` · `ReferenceValues` · `ValidationNotes`. Required kolonlar, kabul edilen değerler,
   reference-data ipuçları, örnek satırlar ve validation kuralı açıklamaları.
3. **Dry-run validation (zorunlu ilk adım).** Import hiçbir koşulda doğrudan yazmaz; dry-run **hiçbir şey persist
   etmez**.
4. **Safe apply.** Yalnız dry-run sonucu üzerinden, sheet-level policy ile (§9).
5. **Import run history.** Read-only, append-only `TerritoryImportRun` (§10).
6. **UI.** §18 #10 Import / Export sayfası (export · template · upload · dry-run sonuç tablosu · apply onayı · run
   history), 7 dil RESX paritesi.
7. Backend/frontend testleri, contract flag/limitation hizalaması, Gateway-only authenticated smoke, FU08 evidence
   report.

**Temel mimari kural (pack'e yazıldı):** *import bir **taşıma yolu**dur, ikinci bir iş kuralı motoru değildir.* Her
satır, UI'dan girilmiş gibi mevcut FU03/FU04A/FU05/FU05A guard'larından geçer; import kendi paralel validasyonunu
koyamaz, guard gevşetemez. **Yeni import framework yazılmaz** — `Diten.CrmService` içinde MOD-0149/MOD-0150 için zaten
çalışan XLSX parse / dry-run / apply altyapısı yeniden kullanılır.

## 6. FU08 Exclusions

Workflow approval · controlled activation · ChangeRequest / Change Approval Trace · MOD-0023 integration · visit/route
planning implementation · campaign / frequency / call-cycle implementation · digital detailing · survey · GPS
check-in/out · Brand Scope · Product/Brand master · Account master mutasyonu · Contact mutasyonu ·
`ContactTerritoryAssignment` · **CoverageSummary import** · **Plan vs Current import** · **resource assignment apply
(FU08A)** · yeni import framework · hard delete · Mongo hand-edit · RBAC seed/grant (ayrıca yetkilendirilmedikçe) ·
MOD-0048 publish (ayrıca yetkilendirilmedikçe) · `crm.territory.delete` · `crm.micro-zone.manage` · request
payload'ında `TenantId` · direct port 5061 business API çağrısı.

## 7. Import / Export Object Scope

| Nesne | Export | Template | Dry-run | Apply | Zorunlu guard |
|---|---|---|---|---|---|
| Territory Model metadata | ✅ | ✅ | ✅ | ✅ | Yalnız `draft` model editable; active/archived model import ile değiştirilemez |
| Territory Nodes | ✅ | ✅ | ✅ | ✅ | **Hiyerarşi validasyonu zorunlu** (duplicate code, geçersiz parent, cycle, level, tarih containment) |
| Territory hierarchy | ✅ | ✅ | ✅ | ✅ | Node ile aynı guard; ayrı hiyerarşi yazma yolu yok |
| Business Unit scopes | ✅ | ✅ | ✅ | ✅ | FU02A normalized `BusinessScopes`; yalnız `business-unit`; MOD-0048 published |
| Assignment Rules | ✅ | ✅ | ✅ | ✅ | FU03 validasyonu; **preview yan etkisizliği korunur** — import rule'u çalıştırmaz, tanımını yazar |
| Account Assignments | ✅ (current + history) | ✅ | ✅ | ✅ **FU05 guard'larıyla** | §11 |
| Resource Assignments | ✅ (current + history) | ✅ | ✅ | ❌ **→ FU08A** | §11 |
| CoverageSummary | ✅ | ❌ | ❌ | ❌ | Read model — import edilemez |
| Plan vs Current | ✅ | ❌ | ❌ | ❌ | Snapshot/diff read model — import edilemez |

## 8. Dry-Run Validation Policy

Dry-run **hiçbir kayıt yazmaz** (run history satırı dahil). Zorunlu kontrol kümesi:

| Kategori | Kontrol |
|---|---|
| Yapısal | required kolonlar · bilinmeyen/duplicate kolon · veri tipi · boş satır · dosya-seviyesi hata (bozuk/parolalı dosya, eksik zorunlu sheet) |
| Node/hiyerarşi | duplicate node code · geçersiz parent · **cycle riski** · geçersiz `TerritoryLevel` · level sırası ihlali |
| Scope | geçersiz business-unit scope · model scope'unu aşan satır scope'u · geçersiz country scope |
| Rule | geçersiz `RuleType` · geçersiz `ConflictPolicy` · geçersiz hedef node |
| Account | geçersiz `AccountId` · çözülemeyen account external reference · cross-tenant account |
| Resource | geçersiz/policy'siz position code · geçersiz resource ref · snapshot alanı tutarsızlığı *(yalnız dry-run)* |
| Tarih | effective window containment (assignment ⊆ node ⊆ model) · `EffectiveTo < EffectiveFrom` |
| Lifecycle | **active model overlap riski** (single-active-model guard) · active/archived model'e yazma denemesi |
| Reference data | ilgili MOD-0048 setleri published mı → değilse **fail-closed** |
| İzolasyon | tenant isolation; her satır çağıran tenant claim'ine bağlanır |

**Sonuç satırı sözleşmesi:** `Sheet` · `RowNumber` (gerçek Excel satırı) · `Severity` · `ErrorCode` (stabil,
makine-okunur) · `Message` (lokalize) · `SuggestedFix` · `Blocking` (bool) · `Operation` · `EntityType` ·
`ResolvedKey` · `ChangedFields`. Özet sayaçları: creates · updates · ends · skips · errors · conflicts · warnings.
**Blocking / non-blocking ayrımı zorunludur**: blocking satır hiçbir koşulda apply edilmez; non-blocking warning
apply'ı tek başına bloklamaz ama raporlanır ve run history'de sayılır.

## 9. Safe Apply Policy

| Karar | Sonuç |
|---|---|
| Import doğrudan apply yapabilir mi? | **Hayır** — dry-run zorunlu ilk adım; apply **ayrı rota** (yıkıcı çağrı önizleme isteğiyle kazara tetiklenemez) |
| Genel motor | "validate-all, then apply" — onaylanan plan ile çalışan plan aynı doğrulamadan geçer |
| `Model` / `Nodes` / `AssignmentRules` | **Sheet-level all-or-nothing** — kısmi hiyerarşi yetim/kopuk ağaç üretir, bu yüzden yasak |
| `AccountAssignments` | **Batch-level all-or-nothing** — FU05 §22.2 policy #2 ile birebir aynı |
| `ResourceAssignments` | Apply **yok** (FU08A) |
| Strict mode | Operatör isterse **dosya-seviyesi all-or-nothing** |
| Partial apply | Sheet bazında **açıkça raporlanır** ve run history'ye yazılır; **sessiz partial apply yasak** |
| Sheet sırası | `Model` → `Nodes` → `AssignmentRules` → `AccountAssignments`; önceki sheet blocking hata alırsa bağımlı satırlar `skipped_dependency` |
| Hard delete | **Yok** — `delete` → controlled `unsupported_operation`; kapatma yalnız `end` semantiğiyle |
| Update overwrite | Boş hücre = **değiştirme**; açık `<CLEAR>` = temizle (zorunlu alan temizlenemez); id/immutable alan değişikliği controlled hata |
| Idempotency | Aynı dosyanın ikinci apply'ı **duplicate üretmez** (`no_change` skip veya controlled conflict); eşleştirme **doğal anahtar** ile (model+code / model+account+scope+window), serbest metin ile değil |
| Provenance | Yazılan/kapatılan her kayıtta `ImportRunId` + `CorrelationId`; source file **hash**'i run kaydında |
| Reference set eksik | **Fail-closed** — apply bloklanır |
| Uygulanacak satır yok | Apply bloklanır; yanıltıcı "başarılı" gösterilmez |
| Hata oranı eşiği | Yüksek blocking oranı (yanlış dosya/şablon sinyali) apply'ı bloklar |
| `TenantId` | Excel'de **yer almaz**; claim'den gelir; dosyada kolon varsa yok sayılır + uyarı |
| Atomiklik | Mongo multi-document transaction zorunlu kılınmaz; standalone dev'de FU05 compensation deseni; partial durum açıkça raporlanır |

## 10. Import Run History Policy

Read-only, **append-only** `TerritoryImportRun` (pack §7.5b): `ImportRunId` · `TenantId` (server-resolved) ·
`FileName` · `FileHash` · `UploadedBy` · `UploadedAt` · `Status` (`applied` / `partially-applied` / `failed` /
`blocked`) · `DryRunResult` · `AppliedAt` · `AppliedBy` · `CorrelationId` · sheet bazında row counts
(total/created/updated/ended/skipped) · error counts · warning counts.

Kurallar: yalnız **apply** run kaydı yazar (salt dry-run kalıcı iz bırakmaz); kayıt **güncellenmez ve silinmez**;
**ham dosya saklanmaz**, yalnız hash tutulur (PII/dosya saklama yüzeyi açılmaz); run kaydı bir approval/evidence
artefaktı **değildir** — FU06 approval trace ve FU07 evidence pack sahiplikleri değişmez.

## 11. Account / Resource Assignment Import Policy

**Account Assignments — import EDİLEBİLİR, ama FU05'i bypass ederek DEĞİL.** Import satırı FU05
`ApplyAccountTerritoryAssignments` ile **aynı** guard setinden geçer: yalnız stored status'u `active` olan model;
batch all-or-nothing; kesişen scope + örtüşen window'da controlled **409**; override yalnız non-empty reason ile; eski
kayıt **silinmez**, `ended` + `EffectiveTo`/`EndedAt` ile kapatılır; assignment window ⊆ node window ⊆ model window.
Import, preview/rule sürecini "atlayan" ayrı bir yazma yolu değildir: rule kaynaklı satırlar
`AppliedRuleId`/`AppliedRuleCode` provenance'ını taşır, manuel satırlar `AssignmentSource=import` olarak işaretlenir.
FU05A current-coverage guard'ı okuma tarafındadır ve import'tan etkilenmez.

**Resource Assignments — v1'de yalnız export + template + dry-run; apply FU08A'ya ERTELENDİ.** Gerekçe: FU04A
`proposed` (planning) ↔ `active` (operational) ayrımını, activation transition'ını, atomik replacement/transfer'ı,
reason/provenance zorunluluğunu ve position exclusivity guard'ını taşır. "Bu satır replacement mı, transfer mı, yeni
atama mı?" sorusunun bir Excel satırında güvenle ifade edilmesi ayrı bir tasarım kararıdır ve aceleye getirilirse
FU04A'nın tüm lifecycle sözleşmesi bir dosyayla bypass edilebilir. FU08A açılırsa **proposed/active ayrımı ve
reason/provenance korunmak zorundadır**; import ile doğrudan `active` operational responsibility yaratmak veya
replacement/transfer'ı bypass etmek yetkilendirilmemiştir.

**CoverageSummary ve Plan vs Current — import EDİLEMEZ.** İkisi de türetilmiş read model'dir; import edilmeleri
kaynağı ile projeksiyonu çelişkiye düşürürdü. FU05A ile yeni kapatılan lifecycle guard'ının anlamını da bozardı.

## 12. Contract Flags

FU08 sonrası önerilen additive flag'ler (pack §22.5):

```json
{
  "supportsTerritoryExport": true,
  "supportsTerritoryImportExport": true,
  "supportsTerritoryImportDryRun": true,
  "supportsTerritoryImportApply": true,
  "supportsResourceAssignmentImportApply": false,
  "supportsWorkflowActivation": false
}
```

**Korunan mevcut flag'ler:** `supportsAssignmentRules` · `supportsAssignmentPreview` · `supportsResourceAssignments` ·
`supportsResourceAssignmentPlanVsCurrent` · `supportsAccountAssignmentApply` · `supportsAssignmentHistory` ·
`supportsCoverageSummary` · `supportsCoverageSummaryModelLifecycleGuard`.

`supportsWorkflowActivation = false` **kalır**; workflow/approval readiness flag'i eklenmez.
`supportsResourceAssignmentImportApply=false` bilinçli olarak eklendi: FU08A sınırını contract yüzeyinde de görünür
kılar, böylece tüketici "resource import var mı?" sorusunu tahmin etmez.

## 13. Guard Checks

| Kontrol | Sonuç |
|---|---|
| Runtime code changed? | **No** |
| Backend/frontend changed? | **No** |
| Gateway changed? | **No** |
| MOD-0023 code changed? | **No** |
| Workflow scope opened? | **No** |
| Visit/route implementation opened? | **No** |
| Campaign/frequency implementation opened? | **No** |
| Account master mutation opened? | **No** |
| Contact mutation opened? | **No** |
| ContactTerritoryAssignment opened? | **No** |
| CoverageSummary import opened? | **No** |
| Plan vs Current import opened? | **No** |
| Resource assignment import **apply** opened? | **No** (bilinçli olarak FU08A'ya ertelendi) |
| Hard delete allowed? | **No** |
| Mongo hand-edit allowed? | **No** |
| RBAC seed/grant changed? | **No** (yalnız FU08-RBAC follow-up'ı açıldı) |
| MOD-0048 publish changed? | **No** |
| New import framework authorized? | **No** (mevcut MOD-0149/0150 altyapısı yeniden kullanılır) |
| FU08 scope added? | **Yes** |
| Existing FU scopes preserved? | **Yes** (FU01, FU02, FU02A, FU02B, FU03, FU04, FU04A, FU04B, FU05, FU05A) |
| FU05 / FU04A / FU05A guard'ları korundu mu? | **Yes** (import bypass yolu yok) |
| supportsWorkflowActivation remains false? | **Yes** |
| `crm.territory.delete` / `crm.micro-zone.manage` opened? | **No** |
| Request payload'ında `TenantId` opened? | **No** (claim'den) |
| Direct 5061 business call opened? | **No** |

## 14. Created / Updated Files

- **Updated:** `execution/domains/commercial-suite/module-packs/MOD-0151-territory-management.md`
  - frontmatter `runtime_code_scope` (**+FU08 additive**)
  - header banner (FU08 paragrafı + yetkili FU listesi + resource-apply sınırı)
  - **yeni §7.5b** `TerritoryImportRun` (append-only aggregate)
  - §17 FU08 permission kararı
  - §18 sayfa #10 Import / Export akışı
  - §19 API tablosu FU08 endpoint seti (export · template · dry-run · ayrı apply · run history)
  - §22.1 **FU08 authorization update (2026-08-01)** + FU07 dependency reconciliation
  - **yeni §22.5** FU08 — Import/Export Hardening (scope · object scope · dry-run · safe apply · run history ·
    account/resource policy · permission · contract flags · test expectations · boundary · exclusions)
  - §22 FU tablosu FU08 satırı (scope + `Depends On: FU05, FU05A` + out-of-scope)
  - §23 **F20** follow-up + **R7** / **R8** riskleri
  - §24 FU08 acceptance criteria maddesi
  - §25 FU08 implementation prompt + FU08-RBAC + FU08A önerileri
- **Created:** `docs/audits/mod-0151-fu08-import-export-hardening-pack-authorization-2026-08-01.md` (bu rapor)

**Kod, test, gateway, frontend, seed veya reference-data dosyası değiştirilmemiştir.**

## 15. Final Verdict

**PASS**

- FU08 scope'u **additive** olarak eklendi; mevcut 10 FU scope'unun hiçbiri silinmedi veya daraltılmadı.
- Import/export **object scope**'u nesne bazında netleşti (neyin export/template/dry-run/apply edileceği tablo hâlinde).
- **Dry-run-first** policy netleşti: import hiçbir koşulda doğrudan yazmaz, dry-run hiçbir şey persist etmez, satır
  raporu sözleşmesi ve blocking/non-blocking ayrımı yazıldı.
- **Safe apply** policy netleşti: sheet-level all-or-nothing (yapısal nesneler), batch-level all-or-nothing (account
  assignments), strict mode, sessiz partial apply yasağı, idempotency, provenance, hard delete yasağı.
- **Account/resource assignment import bypass riskleri kapatıldı:** account import FU05 guard'larının aynısını kullanır;
  resource apply v1'de **kapalıdır** (FU08A) — FU04A lifecycle sözleşmesi bir Excel satırıyla bypass edilemez.
- **CoverageSummary ve Plan vs Current import dışı** bırakıldı (read model bütünlüğü + FU05A guard'ının anlamı korunur).
- Workflow / approval / ChangeRequest / MOD-0023 / visit-route / campaign-frequency kapsamı **açılmadı**;
  `supportsWorkflowActivation=false` korundu.
- Ek governance kazanımları: (a) FU08'in `Depends On` alanındaki **FU07 çelişkisi** giderildi; (b) `TerritoryImportRun`
  append-only aggregate'i §7'ye eklendi; (c) **FU08-RBAC** ve **FU08A** follow-up'ları açıkça açıldı; (d) import'a özgü
  iki risk (**R7** guard bypass, **R8** sessiz toplu overwrite) risk tablosuna yazıldı.
- Implementation prompt'u hazırlanabilir.

## 16. Next Recommended Prompt

```
@orchestrator MOD-0151 FU08 — Import/Export Hardening
```
