# WORK PACKAGE — WP-FREQ-F8 · "Nerede geçerli olsun?" alt-bölümü mockup birebir (frontend)

> **CT (SoR).** MOD-0165-FU03. Branch `feature/scmm-content-studio` (`38522ac4` üstü). F7 bu alt-bölüme açıklama + kod-badge + form-select ekledi; kullanıcı kalan mockup farklarını istiyor. **Frontend only** (_Editor.cshtml scope alt-bölümü + form.js scope render + visit-frequency-create.css + L10n). buildPayload/id/cascade KORUNUR (vfpBusinessUnit/vfpTerritoryModel/vfpTerritoryNode/vfpSegment/vfpCampaign/vfpBrand/vfpProduct/vfpCyclePeriod).

## Somut istekler (mockup resim-1 vs mevcut)
1. **Alt-grup başlığı:** mockup = **title-case bold başlık + INLINE muted açıklama** aynı satırda (ör. "Organizasyon   Hangi ekip ve saha alanında geçerli") + altında **divider çizgisi**. Mevcut = uppercase başlık + block açıklama, divider yok. → title-case bold + inline desc + alt divider (her grup: Organizasyon/Kapsam/Ürün/Dönem).
2. **Font + field:** alt-bölüm task-create fontu (app family/size) + label `form-label fw-medium` (mevcut `vfp-label` → app). Select'ler `form-select` (F7 mevcut) app görünümde.
3. **Saha alanı yan yana:** şu an TerritoryModel + TerritoryNode **alt alta** (stacked). İstenen: **model + node yan yana** (Saha alanı hücresinde nested 2-kolon: model | node). İş birimi | Saha alanı ana 2-kolon düzeni korunur; Saha hücresi içinde model|node yan yana.
4. **"kısıtlama yok" ipuçları:** kısıtsız select'lerin altında (İş birimi/Saha/Segment/Kampanya/Marka/Cycle) "kısıtlama yok" (L10n `NoConstraint`); Ürün="Önce marka seçin" (mevcut), Cycle dönemi="Önce cycle seçin". (Statik yardımcı; mockup gibi.)
5. **KAPSAM kutusu:** en alt özet mockup gibi bordered kutu (bg subtle, uppercase "KAPSAM" label + `vfpScopeChips` çipleri/"tüm hedefler" + sağda "Kapsamı temizle").

## Kapsam
- `_Editor.cshtml` (yalnız "Nerede geçerli olsun?" `#vfpScopeBody`): alt-grup başlıklarını title-case bold + inline desc + divider yapan markup; `vfp-label`→`form-label fw-medium`; Saha alanı hücresi model|node nested 2-kolon; her kısıtsız select altına "kısıtlama yok" hint; KAPSAM özet bordered kutu.
- `visit-frequency-create.css`: `.vfp-scope-group-title` title-case bold (uppercase KALDIR) + inline flex (title + desc yan yana) + `border-bottom` divider; scope alt-bölüm app font (vfp-label yerine form-label kullanıldığı için app gelir); Saha nested 2-kolon; `.vfp-scope-summary` bordered kutu (bg `--bs-tertiary-bg`, border). Tema-duyarlı.
- `form.js`: yalnız gerekiyorsa scope render (Saha model|node yan yana cascade korunur; kısıtlama-yok hint görünürlüğü; KAPSAM chips) — **cascade + select id'leri + buildPayload DEĞİŞMEZ**.
- L10n: `NoConstraint` ("kısıtlama yok") 7 dil (yoksa ekle). Grup açıklamaları F7'de mevcut (ScopeGroup*Desc).

## KORU / YAPMA
- form.js cascade (model→node, brand→product)/validation/buildPayload/id DEĞİŞMEZ. Backend/liste/Detay/Çözümleme/resolve.js/index.js/Segment/**diğer bölümler (Kimlik/Frekans/Geçerlilik/Çakışma) ve HEDEF üst-kısmı (chip/Hangi kayıt) DOKUNMA** (yalnız #vfpScopeBody + ilgili css/L10n). Hardcode vocabulary yok. Tema-duyarlı (--bs-* token). Yalnız _Editor(scope)+form.js(scope render)+css+resx.

## Acceptance
- **E2:** Diten.Web.Tests 137/0. git diff: _Editor.cshtml + visit-frequency-create.css + form.js(scope) + resx. Backend/liste/detay/diğer bölüm diff YOK. cascade/buildPayload korunmuş.
- **E4:** "Nerede geçerli olsun?" mockup gibi: title-case başlık+inline açıklama+divider; app font/label; Saha alanı model|node yan yana; "kısıtlama yok" ipuçları; KAPSAM bordered kutu; cascade+kaydetme işlevi aynı.

---

## §36.1 Agent Prompt (paste-ready) — DISPATCH: owner
```text
@[.antigravity/agents/frontend-specialist.md]
WP: WP-FREQ-F8 · "Nerede geçerli olsun?" alt-bölümü mockup birebir (MOD-0165-FU03, frontend)
Repository: C:\Users\user\Desktop\ERP-vNext (ANA checkout)
Branch: feature/scmm-content-studio · Expected HEAD: 38522ac4 üstü · Worktree: ana checkout

Önce oku: execution/domains/commercial-suite/work-packs/WP-FREQ-F8-scope-subsection-mockup.md · frontend/Diten.Web/Views/CRM/VisitFrequencyPolicies/_Editor.cshtml (#vfpScopeBody alt-bölümü) · wwwroot/assets/js/CRM/VisitFrequencyPolicies/form.js (cascadeNodes/scope render/renderScopeSummary/brand-product) · wwwroot/assets/css/visit-frequency-create.css (.vfp-scope*/.vfp-code-hint) · frontend/Diten.Web/Views/Tasks/Create.cshtml (app field/font).

NE (frontend; cascade/buildPayload/id KORUNUR; yalnız #vfpScopeBody):
 1) Alt-grup başlığı: title-case bold başlık + INLINE muted açıklama (aynı satır) + altında border-bottom divider (Organizasyon/Kapsam/Ürün/Dönem). uppercase KALDIR.
 2) Font/field app: vfp-label → form-label fw-medium; scope app font; select'ler form-select.
 3) Saha alanı: TerritoryModel + TerritoryNode alt alta DEĞİL → yan yana (Saha hücresinde nested 2-kolon model|node). İş birimi|Saha ana 2-kolon korunur. cascade (model→node) KORUNUR.
 4) Kısıtsız select altına "kısıtlama yok" (L10n NoConstraint, 7 dil yoksa ekle); Ürün "Önce marka seçin", Cycle dönemi "Önce cycle seçin".
 5) KAPSAM özet: bordered kutu (bg --bs-tertiary-bg, border), uppercase "KAPSAM" + vfpScopeChips + sağda "Kapsamı temizle".
KORU/YAPMA: form.js cascade/validation/buildPayload/id DEĞİŞMEZ; backend/liste/Detay/Çözümleme/resolve.js/index.js/Segment/diğer bölümler + HEDEF üst-kısmı (chip/Hangi kayıt) DOKUNMA; hardcode vocabulary yok; tema-duyarlı; yalnız _Editor(#vfpScopeBody)+form.js(scope)+css+resx.
DOĞRULA (E2): Diten.Web.Tests 137/0; git diff yalnız _Editor+form.js+css+resx; backend/liste/detay/diğer bölüm diff yok; cascade/buildPayload korunmuş. Ayrı commit. §22 TÜRKÇE. K13.
Durma: cascade/buildPayload/id korunamıyorsa; kapsam #vfpScopeBody dışına taşarsa → DUR+raporla.
```
## §37 CT bağımsız doğrulama (2026-09-17) → **ACCEPTED (E2)**
```
Commit: 7c9d6bda · Agent: PASS · CT: ACCEPTED E2 (izole temiz build) · /c/tmp/ct-freqf8-verify @7c9d6bda
```
- ✅ **Kapsam:** _Editor.cshtml(#vfpScopeBody) + visit-frequency-create.css + 7 resx. **form.js/backend/liste/detay/çözümleme/resolve.js/index.js/Segment = 0** → cascade/buildPayload trivially korundu (form.js dokunulmadı).
- ✅ **Markup:** `vfp-scope-group-head` (title-case + inline desc + divider), `vfp-territory-pair` (Saha model|node yan yana), `vfp-scope-summary` (KAPSAM bordered kutu), `NoConstraint` ("kısıtlama yok"). Tüm context select id'leri (vfpBusinessUnit…vfpCyclePeriod + vfpScopeChips/Clear) korundu.
- ✅ **Build+test (CT izole, Release, temiz):** Diten.Web.Tests **137/0**.
- ℹ️ Cycle belirsizliği: Cycle dönemi="Önce cycle seçin", diğer kısıtsızlar="kısıtlama yok" (E4'te gözden geçirilebilir).
- ⏳ **E4:** alt-bölüm mockup görünümü.

**FREQ-F8 KOMPLE. Sıra: F9 (select'ler select2-arama'lı + Hangi kayıt bg-body) — bu commit üstüne.**
