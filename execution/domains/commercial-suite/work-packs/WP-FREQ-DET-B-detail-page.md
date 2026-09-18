# WORK PACKAGE — WP-FREQ-DET-B · Frekans Politikası Detay AYRI SAYFA (mockup) — DET-A API'sini tüketir (frontend)

> **CT (SoR).** MOD-0165-FU03. Branch `feature/scmm-content-studio` (`3c5bfb80` üstü). Detay fazlaması FAZ 2: quick-view offcanvas → **ayrı Detay sayfası** (mockup tam tasarım), app-card kabuklar. DET-A `/analysis` (ETKİ + çakışanlar) + `/{id}` get tüketilir. **Frontend only** (controller route + Details.cshtml + details.js + css + L10n + index.js row-nav + Index.cshtml offcanvas emekli). Backend DEĞİŞMEZ (DET-A hazır).

## Mockup layout (detay sayfası)
**Header (app kabuk):** PolicyName (h5 bold) + status badge (Yayında/Taslak/…) · altında PolicyCode (mono muted) · Description. Sağda actions: **[Düzenle]** (→ /Edit/{id}) + **[Archive]** (status archived değilse; archive endpoint + onay).
**Stat kartları (4, app-card `card`+`card-body`, satır):**
- FREKANS: "N / dönem" büyük + "Her ay N ziyaret" alt (cadence).
- HEDEF: hedef adı + "Segment · {impact.targetCount} hedef" alt (computable=false → tip · "—").
- GEÇERLİLİK: "dd.MM.yyyy → dd.MM.yyyy|süresiz" + not (varsa cycle).
- AĞIRLIK: band etiketi + "Kaynak: {source label}" alt.
**BAĞLAM:** kapsam çipleri (label:value; boşsa "tüm hedefler").
**NOTES:** notlar (varsa; app card).
**Sağ panel:**
- **DURUM AKIŞI (timeline):** mevcut zaman damgalarından — Taslak oluşturuldu (CreatedAt·CreatedBy) / Yayına alındı (status active ise UpdatedAt vb.) / Arşivlendi (ArchivedAt·ArchivedBy). Renkli nokta + tarih + aktör. **Ağırlık-değişim geçmişi + "sonraki değerlendirme" DET-C (audit) — bu fazda YOK/atla** (yalnız eldeki damgalar; kısmi olduğu KABUL).
- **ETKİ (DET-A analysis.impact):** "{targetCount} hedef bu politikayı alıyor" + "{plannedVisitsPerQuarter} planlanan ziyaret / çeyrek". computable=false → "—" + ProjectionNote (tooltip/alt not).
**BU HEDEFTE ÇAKIŞAN POLİTİKALAR (DET-A analysis.conflicts):** "N aday" rozeti + liste: her aday → **seçilen/aday** badge + name + targetType + "N×/period" + **reason insan-dili** (en dar kapsam kazandı=specificity / daha geniş kapsam / yedek olarak duruyor=last-resort/priority — reasonLabels + specificity'den türet). Seçilen satır vurgulu (yeşil sol-şerit).

## Kapsam
- **Controller:** `[HttpGet("Details/{policyId:guid}")]` → `Details.cshtml` (RBAC read gate — canResolve/read deseni; UAS-001).
- **Details.cshtml** (yeni sayfa, `_LayoutTenantShell`, app-card kabuklar, `.vfp-detail-scope` tema-duyarlı; Segment Details + editör app-card deseni referans, DEĞİŞTİRME).
- **details.js** (yeni): `GET /CRM/VisitFrequencyPolicies/api/visit-frequency-policies/{id}` + `/{id}/analysis` → render (header/stat/bağlam/notes/timeline/etki/çakışanlar). İsim çözümü + verdict/reason/band etiketleri **resolve.js deseninden** (nameOf/labels) reuse/kopya (resolve.js'i BOZMA).
- **index.js:** row "Detay" (`.js-quick-view`) → `window.location='/CRM/VisitFrequencyPolicies/Details/'+id`; `openDetails` offcanvas açma KALDIR.
- **Index.cshtml:** `_DetailsQuickView` partial'ı KALDIR (offcanvas emekli). **_Resolve (Çözümleme tab) KALIR** (ayrı liste-konsol tab'ı).
- **L10n** (7 dil): yeni sayfa metinleri (ETKİ/DURUM AKIŞI/çakışan başlıkları, reason insan-dili "en dar kapsam kazandı" vb., stat başlıkları). verdict/reason/band L10n mevcut.

## KORU / YAPMA
- Backend DEĞİŞMEZ (DET-A + get + resolve hazır). Liste (index.js kolon/Save View/AĞIRLIK/FREKANS) DEĞİŞMEZ (yalnız row Detay yönlendirme). Editör/Çözümleme tab (_Resolve/resolve.js)/Segment/başka modül DOKUNMA. resolve.js davranışı bozma (desen reuse ok). Uydurma değer YOK (computable=false→"—"; isim çözülemezse kısa id). Golden Compact/tema/L10n köprüsü korunur. app-card kabuk (kabuk app içerik mockup). Yeni css visit-frequency-details.css (FREQ-C'den) genişletilebilir; Segment css DEĞİŞTİRME.

## Acceptance
- **E2:** Diten.Web.Tests 137/0. git diff: controller (Details route) + Details.cshtml + details.js + css + L10n(resx) + index.js (row-nav) + Index.cshtml (offcanvas kaldır). Backend/liste-kolon/editör/_Resolve/resolve.js/Segment diff YOK.
- **E4:** row Detay → /Details/{id} ayrı sayfa (mockup): header+actions, 4 stat kartı (HEDEF'te impact sayısı), BAĞLAM, NOTES, sağ DURUM AKIŞI (kısmi) + ETKİ (impact), çakışan politikalar (resolve adayları + seçilen + reason). Düzenle→/Edit, Archive çalışır; tema-duyarlı.

---

## §36.1 Agent Prompt (paste-ready) — DISPATCH: owner
```text
@[.antigravity/agents/frontend-specialist.md]
WP: WP-FREQ-DET-B · Frekans Politikası Detay ayrı sayfa (mockup) — DET-A API tüketir (MOD-0165-FU03, frontend)
Repository: C:\Users\user\Desktop\ERP-vNext (ANA checkout)
Branch: feature/scmm-content-studio · Expected HEAD: 3c5bfb80 üstü · Worktree: ana checkout

Önce oku: execution/domains/commercial-suite/work-packs/WP-FREQ-DET-B-detail-page.md · frontend/Diten.Web/Views/CRM/VisitFrequencyPolicies/{Index,_DetailsQuickView,Edit}.cshtml + wwwroot/assets/js/CRM/VisitFrequencyPolicies/{index.js (openDetails/js-quick-view), resolve.js (nameOf/verdict/reason/band labels — REUSE deseni), details? } + wwwroot/assets/css/visit-frequency-details.css · frontend/Diten.Web/Controllers/CRM/VisitFrequencyPoliciesController.cs (Edit route + analysis proxy + get) · services/.../VisitFrequencyPolicyDtos.cs (VisitFrequencyPolicyAnalysisDto: Impact{TargetCount,TargetCountComputable,PlannedVisitsPerQuarter,ProjectionNote}, Conflicts{SelectedPolicyId,Verdict,Candidates[]}) · (referans app-card kabuk) Segment Details/editör.

NE (frontend; backend DEĞİŞMEZ):
 Controller [HttpGet("Details/{policyId:guid}")] → Details.cshtml (RBAC read gate). Details.cshtml ayrı sayfa (_LayoutTenantShell, app-card kabuklar, .vfp-detail-scope tema-duyarlı): header (name+status badge+code+description + [Düzenle→/Edit/{id}][Archive]); 4 stat kartı (FREKANS cadence / HEDEF adı+"tip · {impact.targetCount} hedef" / GEÇERLİLİK tarih+not / AĞIRLIK band+"Kaynak:source"); BAĞLAM çipleri; NOTES; sağ DURUM AKIŞI (mevcut CreatedAt/UpdatedAt/ArchivedAt timeline — ağırlık-değişim/sonraki-değerlendirme DET-C, bu fazda atla) + ETKİ ({targetCount} hedef + {plannedVisitsPerQuarter} ziyaret/çeyrek; computable=false→"—"+not); BU HEDEFTE ÇAKIŞAN POLİTİKALAR (analysis.conflicts adayları: seçilen/aday badge + name + tip + N×/period + reason insan-dili en-dar-kapsam-kazandı/daha-geniş/yedek; seçilen vurgulu; "N aday" rozeti). details.js: GET /{id} + /{id}/analysis → render; isim/etiket çözümü resolve.js deseninden reuse (resolve.js BOZMA). index.js: row Detay → window.location /Details/{id} (openDetails offcanvas KALDIR). Index.cshtml: _DetailsQuickView KALDIR (offcanvas emekli); _Resolve (Çözümleme tab) KALIR. L10n 7 dil (yeni metinler + reason insan-dili).
KORU/YAPMA: backend DEĞİŞMEZ; liste kolon/Save View + editör + _Resolve/resolve.js + Segment/başka modül DOKUNMA (yalnız row Detay yönlendirme + offcanvas kaldır); uydurma değer yok (computable=false→"—", isim yoksa kısa id); app-card kabuk; tema/L10n köprüsü korunur; yeni css visit-frequency-details.css (Segment css değiştirme).
DOĞRULA (E2): Diten.Web.Tests 137/0; git diff controller+Details.cshtml+details.js+css+resx+index.js(row-nav)+Index.cshtml(offcanvas kaldır); backend/liste-kolon/editör/_Resolve/resolve.js/Segment diff yok. Ayrı commit. §22 TÜRKÇE. K13.
Durma: analysis DTO şekli beklenenden farklıysa; resolve.js reuse edilemiyorsa; liste/editör/_Resolve davranışı değişmek zorundaysa; kapsam VisitFrequencyPolicies dışına taşarsa → DUR+raporla.
```
## §37 CT bağımsız doğrulama (2026-09-17) → **ACCEPTED (E2)**
```
Commit: 2cf8efa8 · Agent: PASS · CT: ACCEPTED E2 (izole temiz build) · /c/tmp/ct-detb-verify @2cf8efa8
```
- ✅ **Kapsam:** controller (Details route) + Details.cshtml(A) + details.js(A) + Index.cshtml(M) + index.js(row-nav) + visit-frequency-details.css + 7 resx; _DetailsQuickView(D). backend/editör/_DataTable/Segment = 0.
- ✅ **KRİTİK: _Resolve.cshtml + resolve.js BYTE-IDENTİK (diff 0 satır).** Ajan `_DetailsQuickView`'daki ortak Çözümleme köprüsünü (vfp-dr-l10n + resolve.js yükleme + _Resolve) Index.cshtml'e taşıdı (canResolve koşullu, 12 eşleşme) → offcanvas emekli ama **Çözümleme tab aynen çalışır**.
- ✅ **DET-A tüketimi:** details.js paralel GET /{id} + /{id}/analysis → impact (HEDEF alt "tip · N hedef" + ETKİ bloğu; computable=false→"—"+ProjectionNote, uydurma yok) + conflicts ("N aday" + seçilen/aday + reason insan-dili). İsim/etiket resolve.js deseninden KOPYA (resolve.js bozulmadı).
- ✅ row Detay → `/Details/{id}` (openDetails offcanvas kaldırıldı). app-card kabuk, `.vfp-det-*`, tema-duyarlı. Düzenle/Archive manage-only.
- ✅ **Build+test (CT izole, Release):** Diten.Web.Tests **137/0**.
- ℹ️ Conflict candidate DTO'da targetType/specificity yok → yalnız DTO alanları render (uydurma yok). Timeline = mevcut damgalar (kısmi); tam audit DET-C.
- ⏳ E4: row Detay → /Details sayfası (impact + çakışanlar + timeline).

**DET-B KOMPLE. Opsiyonel: DET-C (tam DURUM AKIŞI audit timeline).**
```
