# WORK PACKAGE — WP-SEG-DETAILS3 · Draft'ta üye önizleme (/preview) + stat/thead rötuş (frontend)

> **CT (SoR).** MOD-0167-FU02 Segments. Branch `feature/scmm-content-studio` (`febe3e88` üstü). **Yalnız frontend (Diten.Web).** Owner geri bildirimi + CT teşhisi.

## Kök bulgu (CT)
- **Madde 4 (asıl):** `/resolve` **draft segmenti reddediyor** → ekranda "**segment_not_active**", 0 Members (territory ile ilgisi YOK; segment draft). `/preview` (SEG-C, `POST api/crm/segments/preview`) **draft-rule kabul eder** (segmentId yok, aktif gerekmez): body `{subjectType, matchMode, criteria[]}` → totalCount + conditionCounts + sampleMembers. Details'te `segment.Criteria` + `segment.SubjectType` + `segment.MatchMode` **zaten var** (criteriaJson satır 17). → **draft → /preview, active → /resolve.**
- **Madde 3:** resolve tablosu `segd-thead` background yok → `var(--bs-body-bg)`.
- **Madde 1:** `segd-stat` CSS'te `background: var(--bs-card-bg)` + border **zaten var** (satır 82-84); light'ta card-bg beyaz sayfa açık-griden zor ayrılıyor → mockup gibi **belirginleştir** (gerekirse `--bs-tertiary-bg` yüzey veya hafif gölge; tema-duyarlı kalsın).
- **Madde 2 (stored bg-label-primary):** DETAILS2'de (`1900568b`) ZATEN yapıldı → owner eski görüntüyü görüyor (Razor recompile/fleet). Bu WP kapsamı DIŞI (tekrar yapma).

## Kapsam (frontend: details.js + Details.cshtml + segment-details.css)
1. **Draft üye önizleme (/preview):**
   - `resolveSection`'a segment **status** ver (data-attr, ör. `data-segment-status="@segment.SegmentStatus"`).
   - `details.js`: segment **active** → mevcut `/resolve` (verdict/excluded/gerçek üyelik). segment **draft/archived-değil** → `/preview` (body `{subjectType, matchMode, criteria}` — Details'ten; criteriaJson/segment alanlarından). `/preview` yanıtı: totalCount → Members stat + "included" chip; conditionCounts/sampleMembers → satırlar (displayName + subjectSecondaryLabel). "Bu draft; sonuç bugünkü veriyle, aktive edilene dek audience olarak kullanılamaz" notu (mockup resolveIdleNote deseni).
   - Buton etiketi + boş/hata durumları korunur (0→NoMembers, 4xx/5xx→hata satırı).
   - **Not:** /preview excluded/verdict taşımaz (yalnız count+conditionCounts+sample); draft modda "Excluded" bölümü gizlenir veya conditionCounts funnel gösterilir. Active /resolve tam verdict/excluded verir.
2. **segd-thead** background `var(--bs-body-bg)` (tema-duyarlı).
3. **segd-stat** görünürlüğü mockup'a göre belirginleştir (bg/border/gölge; tema-duyarlı; sabit renk YOK).

## KORU / YAPMA
- Active `/resolve` yolu DEĞİŞMEZ (verdict/excluded/reason/source). 3-durum toggle (static→manuel, dynamic/hybrid→önizleme), lifecycle eylemleri, secondary label, criteria ağaç, JSON toggle, scope izolasyon, tema-duyarlılık, app-card, font ayrımı KORU. Backend/DTO/controller DOKUNMA (/preview + /resolve mevcut). segment-create.css DOKUNMA. Stored-note (DETAILS2 bg-label-primary) tekrar DOKUNMA. Uydurma veri YOK. Başka modül.

## Acceptance
- **E2:** Diten.Web.Tests baseline-diff yeşil (137/0). Draft segment Details'te önizleme /preview çağırır (segment_not_active YOK), totalCount+sample gösterir; active segment /resolve (verdict/excluded) korunur; segd-thead bg-body; segd-stat belirgin kart. Scope izole + tema + davranış korundu. git diff yalnız Details.cshtml/details.js/segment-details.css(/resx).
- **E4:** draft dynamic segment (specialty=Nephrology) → "Run preview" gerçek sayı (segment_not_active değil); active segment → resolve verdict/excluded.

---

## §36.1 Agent Prompt (paste-ready) — DISPATCH: owner
```text
@[.antigravity/agents/frontend-specialist.md]
WP: WP-SEG-DETAILS3 · Draft üye önizleme (/preview) + stat/thead rötuş (MOD-0167-FU02, frontend)
Repository: C:\Users\user\Desktop\ERP-vNext (ANA checkout)
Branch: feature/scmm-content-studio · Expected HEAD: <dispatch anındaki HEAD (febe3e88 üstü)> · Worktree: ana checkout

Önce oku: execution/domains/commercial-suite/work-packs/WP-SEG-DETAILS3-draft-preview.md · frontend/Diten.Web/wwwroot/assets/js/CRM/Segments/details.js (runResolve /resolve ~104-175) · frontend/Diten.Web/Views/CRM/Segments/Details.cshtml (resolveSection ~237, criteriaJson satır 17, segment.SubjectType/MatchMode/Criteria) · frontend/Diten.Web/wwwroot/assets/js/CRM/Segments/form.js (runPreview /preview çağrısı = SEG-C referansı: POST /segments/preview, body {subjectType, matchMode, criteria}) · segment-details.css (segd-stat 81, segd-thead).

AMAÇ: Draft segment /resolve reddediyor ("segment_not_active", 0 Members). Draft'ta SEG-C /preview kullan (aktif gerekmez); active'de /resolve kalsın.

NE (details.js + Details.cshtml + segment-details.css):
 1) resolveSection'a data-segment-status="@segment.SegmentStatus". details.js: active→mevcut /resolve; draft→POST /segments/preview body {subjectType, matchMode, criteria} (Details'ten: criteriaJson/segment alanları). /preview yanıtı totalCount→Members stat + included chip; sampleMembers→satır (displayName + subjectSecondaryLabel); "draft, bugünkü veri, aktive edilene dek audience değil" notu. 0→NoMembers, 4xx/5xx→hata satırı. Draft modda Excluded gizli (preview verdict/excluded taşımaz); active tam verdict/excluded.
 2) segd-thead background var(--bs-body-bg).
 3) segd-stat kart görünürlüğü belirginleştir (bg/border/gölge, tema-duyarlı, sabit renk yok).
KORU/YAPMA: active /resolve yolu değişmez; 3-durum toggle/lifecycle/secondary label/criteria ağaç/JSON toggle/scope/tema/app-card/font ayrımı KORU; backend/DTO/controller DOKUNMA (/preview+/resolve mevcut); segment-create.css DOKUNMA; stored-note (DETAILS2) tekrar dokunma; uydurma veri yok; başka modül.
DOĞRULA (E2): Diten.Web.Tests baseline-diff yeşil (137/0); draft→/preview (segment_not_active yok, totalCount+sample), active→/resolve (verdict/excluded) korundu; segd-thead bg-body; segd-stat belirgin; scope+tema+davranış korundu; git diff yalnız Details/details.js/segment-details.css(/resx). Ayrı commit. §22 TÜRKÇE. K13.
Durma: /preview body Details criteria'sıyla kurulamıyorsa; active/draft ayrımı yapılamıyorsa; backend gerekiyorsa (DUR); kapsam Details dışına taşarsa → DUR+raporla.
```
## §37 CT bağımsız doğrulama (2026-09-17) → **ACCEPTED (E2)**
```text
Commit: 83efda40 · Agent: PASS · CT: ACCEPTED E2 (gerçek build) · izole worktree /c/tmp/ct-det3-verify @83efda40
```
- ✅ **Scope:** Details.cshtml + details.js + segment-details.css + _IndexL10n + 7 resx. Backend/DTO/controller/segment-create.css sızıntı=0.
- ✅ **Madde 4 (asıl):** active→`runResolve`/`/resolve` **UNCHANGED** (verdict/excluded); draft→yeni `runDraftPreview`/`/preview` (segmentId yok, aktif gerekmez → "segment_not_active" duvarı aşıldı); `#btnResolve` = `segmentStatus==='active' ? runResolve : runDraftPreview`. `/preview` body camelCase criteria (`#segmentPreviewCriteria` script, `segment.Criteria` tek kaynak — PreviewSegmentReachRequest ile şekil-uyumlu); totalCount→Members stat + included chip; sampleMembers→satır (displayName+subjectSecondaryLabel); draft notu + 0→NoMembers + 4xx/5xx→hata; Excluded draft'ta gizli (preview verdict taşımaz).
- ✅ **Madde 3:** segd-thead bg `var(--bs-body-bg)`. **Madde 1:** segd-stat tema-duyarlı gölge ile belirginleştirildi (rgba(--bs-body-color-rgb), sabit renk yok).
- ✅ **Korundu:** 3-durum toggle, lifecycle, secondary label, criteria ağaç, JSON toggle (PascalCase criteriaJson ayrı), scope izolasyon, tema, app-card, font ayrımı; DETAILS2 stored bg-label-primary dokunulmadı.
- ✅ **Build+test (CT izole, Release, GERÇEK build):** Diten.Web.Tests **137/0**.
- ⏳ **E4:** draft dynamic (specialty=Nephrology) → "Run preview" gerçek sayı (segment_not_active YOK); active → resolve verdict/excluded.
- ℹ️ **Madde 2 (stored bg-label-primary) DETAILS2'de zaten yapıldıydı** — owner eski görüntü gördü (Razor recompile/fleet); bu WP kapsamı dışı, dokunulmadı.
