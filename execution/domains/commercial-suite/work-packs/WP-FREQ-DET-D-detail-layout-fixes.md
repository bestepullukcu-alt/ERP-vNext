# WORK PACKAGE — WP-FREQ-DET-D · Detay sayfası layout mockup + BAĞLAM isim + ETKİ segment sayımı (frontend + backend inceleme)

> **CT (SoR).** MOD-0165-FU03. Branch `feature/scmm-content-studio` (**DET-C landing sonrası HEAD üstü** — Details.cshtml/details.js SIRALI). Kullanıcı DET-B/C sonrası 3 düzeltme: (1) kart konumları mockup gibi değil, (2) BAĞLAM'da saha alanı GUID, (3) ETKİ "Mevcut Değil" (sayı gelmedi). **Frontend (layout+isim) + backend (segment sayımı incele).**

## İstekler (mockup resim-2 vs mevcut resim-1)
1. **Layout → mockup:** SIKLIK/HEDEF/GEÇERLİLİK/AĞIRLIK + BAĞLAM + NOTES **ayrı kartlar DEĞİL** — hepsi **ana politika kartının İÇİNDE** (Kardiyoloji A-Segment Aylık kartı). Mockup:
   - **Ana kart (sol):** header (name+status+code+description + Düzenle/Archive) → altında **4'lü stat şeridi** (FREKANS/HEDEF/GEÇERLİLİK/AĞIRLIK — kart-içi bordered hücreler, ayrı kart değil) → **BAĞLAM** (kart içi bölüm) → **NOTES** (kart içi bölüm).
   - **Ana kart altı:** BU HEDEFTE ÇAKIŞAN POLİTİKALAR (ayrı kart).
   - **Sağ kolon:** DURUM AKIŞI kartı + ETKİ kartı.
   Ana kart(lar) task-create app-card (`card`+`card-body p-4`); stat şeridi mockup'taki gibi 4 bordered hücre (Segment stat-strip deseni referans).
2. **BAĞLAM saha alanı GUID → isim:** details.js BAĞLAM çiplerinde territory-node id'sini **isme** çöz (F19'daki `nodes/by-ids` proxy reuse); diğer context id'leri (segment/campaign/brand/product/cycle/BU) de isimle (mevcut nameOf deseni). Çözülemezse kısa id (uydurma yok).
3. **ETKİ segment sayımı "Mevcut Değil" (GERÇEK KÖK NEDEN):** `VisitFrequencyTargetImpactCounter.CountSegmentAsync` kalıcı segmenti doğrudan `ResolveAsync`'e veriyor; segment **TASLAK** ise `ReasonSegmentNotInEffect` `!IsActive()` → **EmptyResult** → sayı yok → "Mevcut Değil". Oysa segment sayfası (38 üye) `PreviewSegmentReachHandler` ile taslağı **`DraftSegment(subjectType, matchMode, criteria)`** (yapı gereği aktif) sarmalayıcısı kurup `ResolveAsync(limit:0)` ile çözüyor.
   **FIX (PreviewSegmentReachHandler desenini reuse):**
   - Segment **active + effective** → mevcut yol (`ResolveAsync(persisted segment, limit:0)`) → gerçek `TotalMemberCount`.
   - Segment **taslak/aktif değil** → kriterlerinden `DraftSegment(SubjectType, MatchMode, Criteria)` kurup `ResolveAsync(limit:0)` → **önizleme sayısı** (segment sayfasıyla aynı 38) + `Computable=true` + Note "taslak — bugünkü veriyi yansıtır". (Static segment → manuel liste sayısı; kriter yoksa 0/uygun not.)
   - `CandidateCapExceeded` (gerçek 10.000+ aday) → `Computable=false` + Note **"10.000+ aday — sayım kapsam dışı"** (senin (a)+(b) kararın: hesaplanabilende gerçek sayı, cap'te dürüst mesaj).
   **Segment resolver'ın core davranışını KIRMA** (DraftSegment sarmalayıcı + limit:0 okuma reuse; PreviewSegmentReachHandler ile aynı desen). Büyük resolver değişikliği gerekmiyor.
   - Frontend: `Computable=false` + Note → çipte/ETKİ'de "—" yerine **Note metni** ("10.000+ aday — sayım kapsam dışı"); taslak önizleme sayısında sayı + küçük "taslak" ibaresi.

## Kapsam
- `Details.cshtml` + `visit-frequency-details.css`: layout mockup (ana kart içi stat şeridi + BAĞLAM + NOTES; sağ DURUM AKIŞI/ETKİ; çakışanlar altta). app-card kabuk, tema-duyarlı.
- `details.js`: BAĞLAM context id → isim çözümü (territory-node by-ids dahil).
- (backend, gerekirse) `VisitFrequencyTargetImpactCounter` / segment count-only yolu — yalnız sayım okuması, resolver write/davranış değişmez.
- L10n gerekiyorsa (yeni etiket yok muhtemelen).

## KORU / YAPMA
- Segment membership resolver write/resolve davranışı DEĞİŞMEZ (yalnız count-only okuma). DET-C timeline + DET-A conflicts/impact contract'ı bozma (layout değişir, DTO tüketimi aynı). resolve/CRUD/liste/editör/_Resolve/resolve.js/Segment/başka modül DOKUNMA. Uydurma değer/isim YOK (computable=false→"—", isim yoksa kısa id). app-card kabuk; tema/L10n köprüsü. Yalnız Details.cshtml + details.js + css (+ gerekirse impact counter backend + test).

## Acceptance
- **E2:** Diten.Web.Tests 137/0 (+ backend'e dokunulduysa CrmService.Application.Tests baseline-diff sıfır-yeni-fail). git diff: Details.cshtml + details.js + css (+ impact counter/test). Liste/editör/_Resolve/resolve.js/DET-A conflicts/DET-C timeline handler diff YOK (impact count-only hariç).
- **E4:** Detay mockup layout (ana kart içi stat+BAĞLAM+NOTES, sağ timeline+ETKİ, altta çakışanlar); BAĞLAM saha alanı **isimle**; ETKİ segment sayısı gelir (hesaplanabilen segmentte) veya dürüst "—" (gerçekten hesaplanamıyorsa).

---

## §36.1 Agent Prompt (paste-ready) — DISPATCH: owner (DET-C landing SONRASI)
```text
@[.antigravity/agents/backend-architect.md]
WP: WP-FREQ-DET-D · Detay layout mockup + BAĞLAM isim + ETKİ segment sayımı (MOD-0165-FU03, frontend+backend)
Repository: C:\Users\user\Desktop\ERP-vNext (ANA checkout)
Branch: feature/scmm-content-studio · Expected HEAD: <DET-C commit> üstü · Worktree: ana checkout

Önce oku: execution/domains/commercial-suite/work-packs/WP-FREQ-DET-D-detail-layout-fixes.md · frontend/Diten.Web/Views/CRM/VisitFrequencyPolicies/Details.cshtml + wwwroot/assets/js/CRM/VisitFrequencyPolicies/details.js + wwwroot/assets/css/visit-frequency-details.css · frontend/.../VisitFrequencyPoliciesController.cs (nodes/by-ids proxy — F19) · (referans stat-strip) Segment Details · services/.../VisitFrequencyPolicy/Analysis/VisitFrequencyTargetImpactCounter.cs (CountSegmentAsync) + Segmentation/Resolution/SegmentMembershipResolver.cs (count-only limit:0 + CandidateCapExceeded).

NE:
 1) Layout (Details.cshtml+css): SIKLIK/HEDEF/GEÇERLİLİK/AĞIRLIK + BAĞLAM + NOTES → ANA politika kartının İÇİNE (ayrı kart değil): header+Düzenle/Archive → 4'lü stat şeridi (kart-içi bordered hücre) → BAĞLAM bölümü → NOTES bölümü; altında ÇAKIŞAN POLİTİKALAR ayrı kart; sağ kolon DURUM AKIŞI + ETKİ kartları. app-card kabuk, tema-duyarlı.
 2) BAĞLAM isim (details.js): territory-node id → isim (nodes/by-ids proxy reuse) + segment/campaign/brand/product/cycle/BU nameOf; çözülemezse kısa id.
 3) ETKİ segment sayımı: GERÇEK KÖK NEDEN = CountSegmentAsync kalıcı segmenti doğrudan ResolveAsync'e veriyor; TASLAK segmentte ReasonSegmentNotInEffect(!IsActive)→EmptyResult→"Mevcut Değil" (segment sayfası 38 gösteriyor). FIX (PreviewSegmentReachHandler desenini reuse): active+effective→ResolveAsync(persisted,limit:0) gerçek TotalMemberCount; TASLAK/aktif değil→DraftSegment(SubjectType,MatchMode,Criteria) sarmalayıcı kurup ResolveAsync(limit:0)→önizleme sayısı (segment sayfasıyla aynı)+Computable=true+Note "taslak — bugünkü veri"; CandidateCapExceeded→Computable=false+Note "10.000+ aday — sayım kapsam dışı"; static→manuel liste. Segment resolver core davranışını KIRMA (DraftSegment+limit:0 reuse, PreviewSegmentReachHandler deseni). Frontend: Computable=false→Note metnini göster ("—" yerine), taslakta sayı+"taslak" ibaresi.
KORU/YAPMA: segment resolver write/resolve DEĞİŞMEZ (count-only okuma); resolve/CRUD/liste/editör/_Resolve/resolve.js/Segment/başka modül DOKUNMA; DET-A conflicts + DET-C timeline DTO tüketimi aynı; uydurma değer/isim yok; app-card kabuk; yalnız Details.cshtml+details.js+css (+gerekirse impact counter+test).
DOĞRULA (E2): Diten.Web.Tests 137/0 (+backend'e dokunulduysa CrmService.Application.Tests baseline sıfır-yeni-fail, Release); git diff Details+details.js+css(+impact counter/test); liste/editör/_Resolve/resolve.js/conflicts/timeline handler diff yok. Ayrı commit. §22 TÜRKÇE. K13.
Durma: segment sayımı büyük resolver değişikliği gerektiriyorsa; layout DET-C timeline/impact tüketimini bozuyorsa; kapsam dışına taşarsa → DUR+raporla.
```
## §37 CT bağımsız doğrulama (2026-09-17) → **ACCEPTED (E2)**
```
Commit: c7617529 · Agent: PASS · CT: ACCEPTED E2 (izole temiz build) · /c/tmp/ct-detd-verify @c7617529
```
- ✅ **Kapsam (6 dosya):** Details.cshtml + visit-frequency-details.css + details.js + ICounter (CountableWithNote) + Counter (segment taslak fix) + test. **resolve/CRUD/handler(DET-C timeline, DET-A conflicts)/_Resolve/resolve.js/Segment/liste/editör = 0** (grep 0).
- ✅ **Segment taslak-sayım fix:** `inEffect = IsActive() && IsEffectiveAt(at)`; `toResolve = inEffect ? segment : PreviewClone(segment)` (id/type/criteria korunur, Active+açık-uçlu zorlanır) → ResolveAsync(limit:0) → taslakta bugünkü erişim sayısı (segment sayfasıyla aynı) + Note "taslak"; cap → "10.000+ aday — sayım kapsam dışı". **Resolver core write/resolve davranışı değişmedi** (clone okuma). Active segment mevcut yol (test 7 korundu).
- ✅ **BAĞLAM isim:** TerritoryNodeId → nodes/by-ids (F19 reuse) isim; BusinessUnit → MOD-0048 etiket; çözülemezse kısa id/ham kod (uydurma yok).
- ✅ **Layout:** tek ana kart = header + 4'lü kart-içi stat şeridi + BAĞLAM + NOTES; altta ÇAKIŞAN POLİTİKALAR; sağ DURUM AKIŞI+ETKİ. app-card, tema-duyarlı, id'ler korundu (DET-A/C tüketim aynı).
- ✅ **Build+test (CT izole, Release):** CrmService.Application.Tests **1785/0/5** (bu koşuda PII order-flake tetiklenmedi; pre-existing/order-bağımlı, baseline'da teyitli) + Diten.Web.Tests **137/0**.
- ⏳ E4: Detay layout + BAĞLAM isim + ETKİ segment sayısı (taslakta bile).

**DET-D KOMPLE. Sıra: DET-E (timeline aktör-isim/tasarım + ETKİ rakam stili).**
```
