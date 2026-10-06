# WORK PACKAGE — WP-FREQ-DET-C · DURUM AKIŞI audit timeline (ağırlık/status değişim geçmişi) (backend+frontend)

> **CT (SoR).** MOD-0165-FU03. Branch `feature/scmm-content-studio` (`a79682a5` üstü). Detay fazlaması FAZ 3: Detay sağ **DURUM AKIŞI**'nı tam audit timeline'a çıkar (mockup: Taslak oluşturuldu / Yayına alındı / Ağırlık "Temel"→"Standart" / Sonraki değerlendirme). Politika şu an event geçmişi tutmuyor (yalnız CreatedAt/UpdatedAt/ArchivedAt). **Backend (embedded event trail + timeline) + frontend (Details timeline).**

## Depolama kararı (embedded, additive)
- Aggregate'e `List<VisitFrequencyPolicyEvent> Events` (init boş). `VisitFrequencyPolicyEvent` record: `Type` (created/published/deactivated/reactivated/weight-changed/archived), `At` (DateTimeOffset), `By` (string?), `FromValue`/`ToValue` (string?, weight/status için band kodu). **GUID FK YOK** (aynı doküman içi). AutoMap class-map yeni alanı otomatik alır; **eski dokümanlar (Events yok) → boş liste** (strict class-map yalnız BİLİNMEYEN eleman reddeder, EKSİK değil — güvenli). Yeni koleksiyon YOK.

## Kapsam
### 1) Domain
- `VisitFrequencyPolicyEvent` record + `VisitFrequencyPolicy.Events` (init `new List<>()`). (Gerekirse RegisterClassMaps'te teyit — AutoMap yeterli olmalı; GUID subtype sorunu yok çünkü FK yok.)

### 2) Command handlers (additive event append; write semantiği DEĞİŞMEZ)
- **Create:** `Events.Add(created, At=CreatedAt, By=CreatedBy)`; eğer create status=active → ayrıca `published`.
- **Update:** ESKİ Status/Priority'yi atamadan ÖNCE yakala; diff:
  - Status draft→active → `published`; active→inactive → `deactivated`; inactive/draft→active → `reactivated`.
  - Priority(band) değişti → `weight-changed` (FromValue=eski band code, ToValue=yeni band code — value→code contract'tan).
  - (Diğer alan değişiklikleri event üretmez — yalnız status+weight; mockup böyle.)
- **Archive:** `archived`.
- Event `By` = `_actor.ActorName`, `At` = now.

### 3) Timeline (analysis DTO'ya ekle)
- `VisitFrequencyPolicyAnalysisDto`'ya `Timeline` (IReadOnlyList<TimelineEntry>): gerçek `Events` (kronolojik) → {type, at, by, label/detail}. **Backfill:** Events boşsa (eski kayıt) zaman damgalarından türet — created (CreatedAt/By) + (archived: ArchivedAt/By). weight-changed sadece gerçek event'ten (eski kayıtta yok).
- **Sonraki değerlendirme (next-eval):** türetilmiş — CyclePeriodId varsa o dönemin bitişi; yoksa EffectiveTo; ikisi de yoksa **atla** (uydurma yok). Timeline'ın sonuna "gelecek" girişi (nokta soluk).

### 4) Frontend (Details DURUM AKIŞI)
- `details.js`: DURUM AKIŞI'nı analysis.Timeline'dan render et (mevcut kısmi-damga render'ı değiştir). Her giriş: renkli nokta (type'a göre) + başlık (type→L10n etiket, weight-changed → "Ağırlık {from}→{to}" band etiketleriyle) + tarih + aktör. Next-eval girişi soluk.
- L10n: timeline tip etiketleri (Taslak oluşturuldu/Yayına alındı/Geçici kapatıldı/Yeniden yayında/Ağırlık değişti/Arşivlendi/Sonraki değerlendirme) 7 dil.

### 5) Tests
CrmService.Application.Tests: create→created event; update status draft→active→published; priority değişimi→weight-changed(from/to); archive→archived; timeline backfill (Events boş→created/archived); next-eval türetme. Baseline sıfır-yeni-fail.

## KORU / YAPMA
- Write semantiği (create/update/archive doğrulama, immutability, resolve, soft-delete) DAVRANIŞ olarak DEĞİŞMEZ — yalnız **additive event append**. Resolve/liste/editör/detay stat+impact+conflicts (DET-A/B) DEĞİŞMEZ (yalnız timeline eklenir). Segment/başka modül DOKUNMA. Yeni koleksiyon YOK (embedded). class-map: eski docs okunabilir kalmalı (Events eksik→boş). Tenant izolasyonu. Uydurma event/next-eval YOK (backfill yalnız gerçek damgalar).

## Acceptance
- **E2:** CrmService.Application.Tests baseline-diff sıfır-yeni-fail + Diten.Web.Tests 137/0. Eski politika okunur (Events eksik→boş, backfill timeline). Yeni create/update(status/weight)/archive → event'ler birikir; analysis.Timeline döner. git diff: Domain (Event+Events) + command handlers (append) + analysis DTO/handler (Timeline) + details.js/Details.cshtml (render) + resx + test. resolve/CRUD write davranışı değişmedi.
- **E4:** politikayı düzenle (ağırlık değiştir / yayına al) → Detay DURUM AKIŞI'nda yeni event görünür (Ağırlık X→Y, tarih, aktör); eski kayıtta created/archived + next-eval.

---

## §36.1 Agent Prompt (paste-ready) — DISPATCH: owner
```text
@[.antigravity/agents/backend-architect.md]
WP: WP-FREQ-DET-C · DURUM AKIŞI audit timeline (ağırlık/status değişim geçmişi) (MOD-0165-FU03, backend+frontend)
Repository: C:\Users\user\Desktop\ERP-vNext (ANA checkout)
Branch: feature/scmm-content-studio · Expected HEAD: a79682a5 üstü · Worktree: ana checkout

Önce oku: execution/domains/commercial-suite/work-packs/WP-FREQ-DET-C-audit-timeline.md · services/.../Domain/Entities/VisitFrequencyPolicy.cs (Status/Priority/CreatedAt/UpdatedAt/ArchivedAt; FrequencyPriorityBands value↔code) · services/.../Handlers/VisitFrequencyPolicyCommandHandlers.cs (Create 52 / Update 142 [Priority=195,Status=197 atamadan ÖNCE eski değeri yakala] / Archive 221) · services/.../Handlers/GetVisitFrequencyPolicyAnalysisHandler.cs + VisitFrequencyPolicyDtos.cs (AnalysisDto — Timeline ekle) · services/.../Persistence/Repositories/VisitFrequencyPolicyRepository.cs + RegisterClassMaps (AutoMap; yeni alan) · frontend/.../details.js + Details.cshtml (DURUM AKIŞI render) · resx.

NE (backend additive event trail + timeline + frontend render):
 Domain: VisitFrequencyPolicyEvent record {Type,At,By,FromValue?,ToValue?} + VisitFrequencyPolicy.Events (init boş, GUID FK YOK). class-map AutoMap teyit (eski docs Events eksik→boş, güvenli).
 Handlers (additive, write semantiği DEĞİŞMEZ): Create→created(+active ise published); Update→eski Status/Priority atamadan önce yakala, diff: status draft→active=published/active→inactive=deactivated/→active=reactivated, priority band değişti=weight-changed(from/to band code); Archive→archived. By=_actor.ActorName, At=now.
 Analysis: AnalysisDto'ya Timeline (gerçek Events kronolojik; Events boşsa CreatedAt/ArchivedAt'ten backfill created/archived; next-eval türet: CyclePeriodId dönem bitişi→yoksa EffectiveTo→yoksa atla). Handler timeline kurar.
 Frontend: details.js DURUM AKIŞI'nı analysis.Timeline'dan render (renkli nokta+başlık[weight-changed→"Ağırlık from→to" band etiketi]+tarih+aktör; next-eval soluk). L10n 7 dil timeline tip etiketleri.
 Tests: CrmService.Application.Tests (created/published/weight-changed/archived event + backfill + next-eval).
KORU/YAPMA: write semantiği (create/update/archive doğrulama/immutability/resolve/soft-delete) DAVRANIŞ DEĞİŞMEZ (yalnız additive event); DET-A/B stat/impact/conflicts + liste/editör/_Resolve/resolve.js/Segment DOKUNMA (yalnız timeline ekle); yeni koleksiyon YOK (embedded); class-map eski docs okunur kalmalı; TenantId; uydurma event/next-eval YOK.
DOĞRULA (E2): CrmService.Application.Tests baseline-diff sıfır-yeni-fail + Diten.Web.Tests 137/0 (Release); eski politika okunur (backfill); yeni event'ler birikir; git diff Domain+handlers+analysis+details.js/Details.cshtml+resx+test; resolve/CRUD write davranışı değişmedi. Ayrı commit. §22 TÜRKÇE. K13.
Durma: class-map eski doc okumayı bozuyorsa; event append write semantiğini değiştiriyorsa; embedded yerine yeni koleksiyon gerekiyorsa; kapsam VisitFrequencyPolicy dışına taşarsa → DUR+raporla.
```
## §37 CT bağımsız doğrulama (2026-09-17) → **ACCEPTED (E2, sıfır-yeni-fail)**
```
Commit: 0dca7354 · Agent: PASS · CT: ACCEPTED E2 (izole temiz build) · /c/tmp/ct-detc-verify @0dca7354
```
- ✅ **Kapsam:** Domain (VisitFrequencyPolicyEvent + Events + CodeForValue) + command handlers (event append) + analysis handler (Timeline+next-eval) + AnalysisDto (TimelineEntry) + Persistence DI (Map<VisitFrequencyPolicyEvent>) + details.js + Details.cshtml + css + 7 resx + 2 test. Embedded (yeni koleksiyon YOK).
- ✅ **KORU=0:** resolve engine / DET-A impact counter+conflicts / _Resolve/resolve.js / Segment / _DataTable / _Editor/form.js / index.js DAVRANIŞ değişmedi (grep 0). Update `Priority`/`Status` atamaları korundu + additive event; Create created(+published); Archive archived. Write semantiği değişmedi.
- ✅ **class-map eski-doc güvenli:** `Map<VisitFrequencyPolicy>` AutoMap + `Map<VisitFrequencyPolicyEvent>(_=>{})` açık kayıt; strict class-map yalnız BİLİNMEYEN eleman reddeder → Events eksik eski docs boş listeye okunur.
- ✅ **Build+test (CT izole, Release):** Diten.Web.Tests **137/0**. CrmService.Application.Tests **1783/1/5** — tek fail `ContactLocationPiiHardeningTests.PiiMasking_...` **PRE-EXISTING ORDER-FLAKE** (izole 18/0 hem baseline hem DET-C'de geçiyor; **baseline a79682a5 full run'da da AYNI test fail** → DET-C getirmedi, PII maskeleme frekansla alakasız). **Baseline-diff = sıfır-yeni-fail.**
- ✅ **Timeline:** Create/Update-diff/Archive event append; Events boş→CreatedAt/ArchivedAt backfill; next-eval CyclePeriod EndDate→EffectiveTo→atla.
- ⏳ **E4:** düzenle (ağırlık/yayın) → DURUM AKIŞI'nda event.

**DET-C KOMPLE. Sıra: DET-D (layout mockup + BAĞLAM isim + segment taslak-sayım fix).**
