# WORK PACKAGE — WP-FREQ-C · Frekans Politikası Detay + Çözümleme (frontend)

> **CT (SoR).** MOD-0165-FU03. Branch `feature/scmm-content-studio` (`1a034858` üstü). FREQ-A (konsol+liste) + FREQ-B (editör) DONE. Bu WP = **son**: `_DetailsQuickView` (row-action detay, tam tasarım) + **Çözümleme tab** (`_Resolve` — "bu hedefe ne sıklıkta?"). **Backend resolve HAZIR** — yalnız frontend.

## Referans
- **Çözümleme paneli deseni:** `Views/CRM/EligibilityPolicies/_Evaluate.cshtml` (input section → `btnEvaluate` → result section) + evaluate.js. Aynı desen: input (targetType + hedef + bağlam + effectiveAt) → resolve → sonuç.
- **Detay dili:** `Views/CRM/Segments/Details.cshtml` (segment-details.css tema-duyarlı, app-card kabuk, read-only alan gösterimi, human label). Mockup detay/çözümleme görseli `C:\tmp\mockup-freq.html` (verdict/resolve).
- **Backend resolve:** `GET /api/crm/visit-frequency-policies/resolve` (FREQ-A controller'da proxy) → `VisitFrequencyResolveResult` (seçilen policy + `FrequencyCandidatePolicy[]` adaylar + verdict `resolved/unknown/conflict/not_applicable` + neden `policy_selected_by_specificity/priority/latest_effective_from`). Entity-picker FREQ-B'den reuse.

## Kapsam (frontend: _DetailsQuickView + _Resolve + resolve.js + L10n)
**1) `_DetailsQuickView`** (FREQ-A placeholder'ı doldur): row-action → policy detayı, **tam tasarım** read-only. Bölümler: Kimlik (code/name/description) · Hedef (targetType + isim) · Bağlam (BU/territory/segment/campaign/brand/product/cycle/period — isimlerle) · Frekans (type + count/period "Ayda 4") · Priority (band etiketi) · Source · Effective from/to · Status (badge) · İz (created/updated/by). Mockup dili (tema-duyarlı, app-card; Segment Details deseni). Entity/GUID → isim.
**2) Çözümleme tab** (`_Resolve`, FREQ-A placeholder tab'ı doldur): EligibilityPolicies _Evaluate deseni. Input: targetType + hedef picker + bağlam + effectiveAt → **Çözümle** → GET /resolve → sonuç: **verdict** badge (resolved/unknown/conflict/not_applicable) + **seçilen policy** (frekans + neden) + **aday policy'ler** (FrequencyCandidatePolicy liste, hangisi neden seçildi/elendi) + "salt-okunur, hiçbir şey yazmaz" notu.
**3) resolve.js** + L10n (7 dil).

## KORU / YAPMA
- Backend resolve/contract/CRUD/soft-delete DEĞİŞMEZ (frontend-only; resolve zaten GET read-only). FREQ-A (konsol/DataTable/nav) + FREQ-B (editör/form.js/contract band/picker) DEĞİŞMEZ. Segment dosyaları (segment-details.css/details.js) DÜZENLEME (reuse/desen ok). Vocabulary/verdict hardcode YOK (contract + resolve response). Uydurma sonuç YOK (resolve gerçek). Yeni scoped css gerekirse visit-frequency-details.css (Segment css DEĞİŞTİRME). Başka modül.

## Acceptance
- **E2:** Diten.Web.Tests baseline-diff yeşil (137/0). Row → _DetailsQuickView tam detay (isimlerle, tema-duyarlı); Çözümleme tab → input→resolve→verdict+seçilen+adaylar+neden (gerçek /resolve); contract-driven; entity-picker isim. FREQ-A/B + Segment değişmedi. git diff: _DetailsQuickView + _Resolve + resolve.js + L10n (+ css?).
- **E4:** row detay + çözümleme uçtan uca (ALMIBA nefrolog → resolve verdict).

---

## §36.1 Agent Prompt (paste-ready) — DISPATCH: owner
```text
@[.antigravity/agents/frontend-specialist.md]
WP: WP-FREQ-C · Frekans Politikası Detay + Çözümleme (MOD-0165-FU03, frontend)
Repository: C:\Users\user\Desktop\ERP-vNext (ANA checkout)
Branch: feature/scmm-content-studio · Expected HEAD: <dispatch anındaki HEAD (1a034858 üstü)> · Worktree: ana checkout

Önce oku: execution/domains/commercial-suite/work-packs/WP-FREQ-C-details-resolve.md · frontend/Diten.Web/Views/CRM/EligibilityPolicies/_Evaluate.cshtml + evaluate.js (çözümleme deseni) · frontend/Diten.Web/Views/CRM/Segments/Details.cshtml + segment-details.css (detay dili — DEĞİŞTİRME, reuse) · frontend/Diten.Web/Views/CRM/VisitFrequencyPolicies/ (_DetailsQuickView + Index çözümleme tab placeholder, _CreateEditOffcanvas+form.js entity-picker reuse) · services/.../VisitFrequencyPolicy/Resolve/VisitFrequencyResolveContracts.cs (VisitFrequencyResolveResult + FrequencyCandidatePolicy + verdict/neden) · C:\tmp\mockup-freq.html (detay/çözümleme görseli).

NE (frontend: _DetailsQuickView + _Resolve/çözümleme tab + resolve.js + L10n + gerekirse css):
 - _DetailsQuickView: row→policy tam detay read-only (Kimlik/Hedef/Bağlam[isimlerle]/Frekans[type+count/period]/Priority[band etiketi]/Source/Effective/Status/İz). Mockup dili, tema-duyarlı, app-card kabuk (Segment Details deseni). GUID→isim.
 - Çözümleme tab (EligibilityPolicies _Evaluate deseni): input targetType+hedef picker+bağlam+effectiveAt → Çözümle → GET /resolve → verdict badge(resolved/unknown/conflict/not_applicable)+seçilen policy(frekans+neden)+aday policy'ler(FrequencyCandidatePolicy)+"salt-okunur" notu.
 - resolve.js + L10n 7 dil.
KORU/YAPMA: backend resolve/contract/CRUD/soft-delete DEĞİŞMEZ (frontend-only, resolve GET read-only); FREQ-A/B + Segment(segment-details.css/details.js/segment-create.css/form.js) DÜZENLEME (reuse ok); vocabulary/verdict hardcode YOK (contract+resolve response); uydurma sonuç YOK (gerçek /resolve); entity-picker FREQ-B reuse; başka modül.
DOĞRULA (E2): Diten.Web.Tests baseline-diff yeşil (137/0); row→_DetailsQuickView tam detay (isimlerle); çözümleme input→resolve→verdict+seçilen+adaylar+neden (gerçek); contract-driven; FREQ-A/B+Segment diff yok. Ayrı commit. §22 TÜRKÇE. K13.
Durma: resolve response şekli beklenenden farklıysa; entity-picker reuse edilemiyorsa; Segment/FREQ-A/B düzenlemek gerekiyorsa; kapsam VisitFrequencyPolicies dışına taşarsa → DUR+raporla.
```
## §37 CT bağımsız doğrulama (2026-09-17) → **ACCEPTED (E2)**
```
Commit: 67fee10e · Agent: PASS · CT: ACCEPTED E2 (gerçek build) · izole /c/tmp/ct-freqc-verify @67fee10e
```
- ✅ **Kapsam (5 dosya + resx):** `_DetailsQuickView.cshtml` (M) + `_Resolve.cshtml` (A) + `Index.cshtml` (M, sadece 9 satır: çözümleme tab placeholder→`<partial _Resolve>`, `id="tab-resolve"` marker korundu) + `resolve.js` (A) + `visit-frequency-details.css` (A) + 7 resx. **FREQ-A/B (index.js/DataTable/nav/form.js/editör/contract band/picker) + Segment (segment-details.css/details.js/segment-create.css) + backend = 0 değişiklik** (grep sayacı 0). Frontend-only.
- ✅ **Gerçek /resolve (uydurma yok):** resolve.js input (targetType+hedef picker+bağlam+effectiveAt) → `GET /CRM/VisitFrequencyPolicies/api/resolve` → backend'in döndürdüğünü birebir render (verdict `frequencyStatus` + `selectedPolicyName`+frekans + `candidateRow` adaylar `selected`/`reason` + neden). Çözülemeyen ref kısa id'ye düşer — **asla uydurma isim**.
- ✅ **Contract-driven / hardcode yok:** verdict tonu (resolved→success/unknown→warning/conflict→danger/not_applicable→secondary) sadece UI rengi; verdict/reason **etiketleri** kapalı L10n map (`verdictLabels`/`reasonLabels` = FrequencyStatus/FrequencyReasonCodes) + humanize fallback — authoring vocabulary hardcode edilmemiş.
- ✅ **_DetailsQuickView:** `show.bs.offcanvas`→GET `/visit-frequency-policies/{id}` read-model → read-only detay; her bağlam GUID'i picker proxy (FREQ-B reuse) ile **isme** çözülür; `.vfp-detail-scope` tema-duyarlı app-card kabuk.
- ✅ **"Yeni Politika" butonu (kullanıcı endişesi):** `index.js:239` `window.DtDefaults.exportButtons(L.NewPolicy, …)` ile DataTable toolbar'a enjekte — bizzat doğrulandı (FREQ-C değil FREQ-A kaynaklı; buton yerinde, statik Index'te değil DataTable init'te). Kullanıcının gördüğü "yok" durumu = restart/hard-refresh yapılmamış olması.
- ✅ **Build+test (CT izole, Release, GERÇEK):** Diten.Web.Tests **137/0/0** (6 s). Baseline-diff sıfır-yeni-fail.
- ⏳ **E4:** row detay (isimlerle) + çözümleme uçtan uca (ALMIBA nefrolog → resolve verdict) — fleet restart sonrası kullanıcı manuel testi.

**FREQ-C KOMPLE → MOD-0165-FU03 Frekans Politikası UI (konsol+editör+detay+çözümleme) TAM. Kalan: fleet restart + E4 manuel test.**
