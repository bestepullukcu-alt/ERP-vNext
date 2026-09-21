# WORK PACKAGE — WP-FREQ-DET-G · Detay stat-strip beyaz/bitişik + çakışan-politika satır tasarımı (frontend)

> **CT (SoR).** MOD-0165-FU03. Branch `feature/scmm-content-studio` (`b6c0ad08` üstü). Layout tek-kart oturdu; iki tasarım rötuşu. **DÜZELTME (agent DUR ile yakaladı):** detay analysis DTO'su `VisitFrequencyPolicyConflictCandidateDto` yalnız PolicyId/Code/Name/FrequencySummary("2×/month")/Priority/Selected/Reason taşıyor; `TargetType/RequiredVisitCount/PeriodType/Specificity` YOK (mapper eziyor). Bu alanlar iç `FrequencyCandidatePolicy`'de var ama frontend'e gelmiyor. → **Backend: DTO'ya 4 alan ekle (additive, FrequencySummary korunur) + `GetVisitFrequencyPolicyAnalysisHandler` mapper** + Frontend (stat-strip css + conflict rows). (backend+frontend)

## Kapsam
### 1) Stat şeridi (Sıklık/Hedef/Geçerlilik/Ağırlık) — beyaz + bitişik
Mevcut: `.vfp-det-stat-cell { background: var(--vfp-soft-bg); border-radius:10px }` + strip `gap:12px` → gri chip görünümü. Mockup: **beyaz hücreler, bitişik, 1px ince ayraç** (gri chip değil). Fix (css):
- `.vfp-det-stat-strip`: `gap:1px; background: var(--vfp-card-border)/divider; border:1px solid var(--bs-border-color); border-radius:6px; overflow:hidden` (1px gap → ayraç çizgisi).
- `.vfp-det-stat-cell`: `background: var(--bs-card-bg)` (beyaz/kart-bg, **gri YOK**); `border:none; border-radius:0`. Responsive kolon sayısı korunur.

### 2) "Bu hedefte çakışan politikalar" satır tasarımı (mockup)
`conflictRow(c)` (details.js) + css — mockup grid: `grid-template-columns: auto minmax(140px,1fr) auto auto; align-items:center; gap:6px 14px; padding:12px 14px; border-radius:6px; border:1px solid`.
- **badge:** seçilen → yeşil (`bg-label-success`/oklch yeşil); aday → gri (`bg-label-secondary`). "seçilen"/"aday" (mevcut L10n).
- **ad + tip alt-satır:** ad (13px 600) + alt (11px mono muted) = **TargetType etiketi** (humanize/label: segment→"Segment", campaign-target→"Kampanya hedefi", contact→"Kişiye özel", account→"Kurum" vb.) + specificity ipucu (en spesifik→"· en dar kapsam", en geniş→ isteğe bağlı). c.TargetType kullan.
- **frekans:** `{c.RequiredVisitCount} / {periodLabel(c.PeriodType)}` (liste FREKANS gibi "2 / ay", "6 / dönem") — "2×/month" DEĞİL. periodLabel yerel (Period_ L10n; details.js'e periodLabels yoksa detail l10n köprüsüne ekle).
- **reason:** sağda (`text-align:right`), muted mono → `reasonLabels[c.reason]` (mevcut; specificity→"en dar kapsam kazandı", loser→"daha geniş kapsam"/"yedek olarak duruyor", priority→"Önceliğe göre seçildi" — koddan gelen gerçek reason).
- **seçilen satır:** yeşil tint bg + sol yeşil vurgu; aday: beyaz/kart-bg. "N aday" rozeti korunur.

## KORU / YAPMA
- Backend DEĞİŞMEZ (DTO alanları mevcut). resolve/CRUD/liste/editör/_Resolve/resolve.js/Segment/DET-A/B/C/D/E mantığı DOKUNMA (yalnız details.js conflictRow render + css stat-strip/conflict + gerekirse detail L10n periodLabels). Uydurma reason/değer YOK (gerçek DTO + reason kodu). app-card/tema/L10n köprüsü. id'ler korunur.

## Acceptance
- **E2:** Diten.Web.Tests 137/0. git diff: details.js + visit-frequency-details.css (+ Details.cshtml/resx yalnız periodLabels bridge gerekirse). Backend/liste/editör/_Resolve/resolve.js diff YOK.
- **E4:** stat şeridi beyaz+bitişik+ince ayraç (gri chip değil); çakışan satırlar mockup grid (badge yeşil/gri + ad+tip + "N / ay" + sağda reason); seçilen yeşil.

---

## §36.1 Agent Prompt (paste-ready) — DISPATCH: owner
```text
@[.antigravity/agents/frontend-specialist.md]
WP: WP-FREQ-DET-G · Detay stat-strip beyaz/bitişik + çakışan-politika satır tasarımı (MOD-0165-FU03, frontend)
Repository: C:\Users\user\Desktop\ERP-vNext (ANA checkout)
Branch: feature/scmm-content-studio · Expected HEAD: b6c0ad08 üstü · Worktree: ana checkout

Önce oku: execution/domains/commercial-suite/work-packs/WP-FREQ-DET-G-statstrip-conflict-rows.md · frontend/Diten.Web/wwwroot/assets/js/CRM/VisitFrequencyPolicies/details.js (conflictRow ~306 + periodLabel/nameOf + vfp-detail-l10n) · wwwroot/assets/css/visit-frequency-details.css (.vfp-det-stat-strip/-cell ~277, conflict/vfp-det-cand) · services/.../Resolve/VisitFrequencyResolveContracts.cs (FrequencyCandidatePolicy: TargetType/RequiredVisitCount/PeriodType/Specificity/Reason MEVCUT) · (referans) index.js periodLabels/frequencyCell.

NE (frontend; backend DEĞİŞMEZ):
 1) Stat şeridi css: .vfp-det-stat-cell background var(--vfp-soft-bg)→var(--bs-card-bg) (gri YOK), border/radius kaldır; .vfp-det-stat-strip gap 12px→1px + background divider(--bs-border-color) + border+radius+overflow:hidden → beyaz bitişik hücre + 1px ayraç. Responsive kolonlar korunur.
 2) conflictRow (details.js) + css: mockup grid auto minmax(140px,1fr) auto auto — badge (seçilen=yeşil bg-label-success / aday=gri bg-label-secondary) | ad(13/600)+tip alt-satır(11 mono muted = TargetType etiketi: segment→Segment, campaign-target→Kampanya hedefi, contact→Kişiye özel, account→Kurum, +specificity ipucu) | frekans "{RequiredVisitCount} / {periodLabel(PeriodType)}" (2 / ay, "2×/month" DEĞİL; periodLabel yerel, yoksa detail l10n köprüsüne periodLabels ekle) | reason sağda muted (reasonLabels[c.reason]). seçilen satır yeşil tint+sol vurgu, aday beyaz. "N aday" rozeti korunur.
KORU/YAPMA: backend DEĞİŞMEZ; resolve/CRUD/liste/editör/_Resolve/resolve.js/Segment/DET-A-E mantığı DOKUNMA (yalnız details.js conflictRow + css + gerekirse detail l10n periodLabels); uydurma reason/değer yok; tema/L10n köprüsü/id korunur.
DOĞRULA (E2): Diten.Web.Tests 137/0; git diff details.js+css(+Details.cshtml/resx periodLabels gerekirse); backend/liste/editör/_Resolve diff yok. Ayrı commit. §22 TÜRKÇE. K13.
Durma: DTO alanı eksikse (beklenmiyor, hepsi var); kapsam details.js/css dışına taşarsa → DUR+raporla.
```
## §37 CT bağımsız doğrulama (2026-09-18) → **ACCEPTED (E2)**
```
Commit: 1a8f4251 · Agent: PASS · CT: ACCEPTED E2 (izole temiz build) · /c/tmp/ct-detg-verify @1a8f4251
```
- ✅ **Kapsam:** DTO + GetVisitFrequencyPolicyAnalysisHandler(mapper) + analysis test + details.js + css + Details.cshtml + 7 resx. **KORU=0** (resolve engine/CRUD/liste/editör/_Resolve/resolve.js/Segment/DET-C timeline/DET-D counter dokunulmadı).
- ✅ **Backend additive (ilk yanlış varsayımım düzeltildi):** `VisitFrequencyPolicyConflictCandidateDto`'ya TargetType/RequiredVisitCount/PeriodType/Specificity eklendi (FrequencySummary korundu, geriye-uyumlu); mapper iç FrequencyCandidatePolicy'den map ediyor. Hazır string parse edilmedi (uydurma yok).
- ✅ **Stat strip:** `.vfp-det-stat-cell` beyaz (--vfp-card-bg, gri kalktı) + border/radius yok; `.vfp-det-stat-strip` gap 1px + divider bg → beyaz bitişik + 1px ayraç (responsive 4/2/1).
- ✅ **Çakışan satır:** mockup grid (badge seçilen=yeşil/aday=gri | ad+TargetType alt-satır | "N / yerel-dönem" | reason sağda); seçilen yeşil tint+sol vurgu; "N aday" rozeti.
- ✅ **Build+test (CT izole, Release):** CrmService.Application.Tests **1786/0/5** + Diten.Web.Tests **137/0**.
- ⏳ E4: stat-strip beyaz/ayraç + çakışan satır mockup.

**DET-G KOMPLE. Sıra: DET-H (Çözümleme tab mockup).**
```
