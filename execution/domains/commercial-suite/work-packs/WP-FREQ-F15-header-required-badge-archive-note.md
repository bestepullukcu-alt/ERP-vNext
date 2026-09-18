# WORK PACKAGE — WP-FREQ-F15 · Header "Zorunlu: N/M" rozeti + "Silme yok/Archive" amber callout (frontend, mini)

> **CT (SoR).** MOD-0165-FU03. Branch `feature/scmm-content-studio` (**F14 landing sonrası HEAD üstü** — aynı dosya, SIRALI). Kullanıcı: (1) zorunlu-alan tamamlanma rozeti ("Zorunlu: N/M") üst header'da **"Listeye Dön" butonunun SOLUNDA** olsun; (2) Yaşam Döngüsü'ndeki "Silme yok. Kapatma Archive ile…" notu mockup'taki **amber callout kutusu** olsun. **Frontend only** (_Editor.cshtml + form.js + css + L10n). buildPayload/id/validation KORUNUR.

> **Bağlam:** Kullanıcının gördüğü "Zorunlu: 6/7" rozeti F13 öncesiydi; F13 checklist rozetini (#vfpCheckBadge) "N uyarı"/"hazır" yaptı. Bu WP header'a AYRI bir canlı zorunlu-sayaç rozeti ekler; checklist kartı rozeti (F13) OLDUĞU GİBİ kalır.

## Kapsam
0. **Header düzeni → Task Create deseni (F17):** editör header'ı Task form header'ı gibi olsun (`frontend/Diten.Web/Views/Tasks/ChecklistTemplates/_Form.cshtml` header deseni): `<div class="d-flex align-items-center justify-content-between mb-3"><div><h5 class="mb-0">@title</h5><nav><ol class="breadcrumb mb-0">…</ol></nav></div><div class="d-flex align-items-center gap-2"> … actions … </div></div>`. Yani **h5 başlık ÜSTTE, breadcrumb ALTINDA (mb-0)**; mevcut sıra ters (breadcrumb üstte) → düzelt. Sağ actions grubu `d-flex align-items-center gap-2` içinde: [Zorunlu rozeti][Listeye Dön].
1. **Header zorunlu rozeti:** yukarıdaki sağ actions grubunda `@Localizer["BackToList"]` `<a>`'nın SOLUNA bir `<span id="vfpRequiredBadge">` ekle. form.js canlı doldurur: **"{Zorunlu}: {satisfied}/{total}"** — total = formdaki zorunlu (`*`) alan sayısı; satisfied = geçerli olanlar (validate mantığından türetilir, ama validate()'i ÇAĞIRMADAN/DEĞİŞTİRMEDEN salt-okunur sayım; hata gösterme yok). Amber pill (bg `--bs-warning-bg-subtle`, color `--bs-warning-text-emphasis`, border `--bs-warning-border-subtle`); tümü tamamsa yeşil (`--bs-success-*`). Her form değişiminde güncellenir (mevcut updateChecklist/updatePanel akışına bağla).
2. **"Silme yok/Archive" amber callout:** `.vfp-life-note` (satır ~469) → amber callout kutusu: bg `var(--bs-warning-bg-subtle)`, border `1px solid var(--bs-warning-border-subtle)`, color `var(--bs-warning-text-emphasis)`, radius 6, padding 10/12, `text-wrap:pretty`; "Archive" kelimesi `<strong>`. (Mockup oklch amber → warning token karşılığı; tema-duyarlı light+dark.)
3. **(F18) `.vfp-btn-armed` box-shadow kaldır:** "Taslak olarak kaydet" (#vfpSaveDraft) butonundaki `.vfp-btn-armed { box-shadow: 0 0 0 3px var(--vfp-accent-ring); }` istenmeyen halkayı **kaldır** (visit-frequency-create.css). Buton işlevi/durumu değişmez; yalnız box-shadow gider.

## KORU / YAPMA
- validate()/buildPayload/id/kaydetme DEĞİŞMEZ (rozet salt-okunur sayım; validate'i tetiklemez/hata basmaz). checklist kartı rozeti (#vfpCheckBadge, F13) DEĞİŞMEZ. Backend/liste/Detay/Çözümleme/resolve.js/index.js/Segment/diğer bölümler DOKUNMA. Tema-duyarlı. Yalnız _Editor.cshtml (header + life-note) + form.js (required badge sayımı) + css + resx (RequiredBadge kalıbı gerekiyorsa; "Required"="Zorunlu" mevcut).

## Acceptance
- **E2:** Diten.Web.Tests 137/0. git diff: _Editor.cshtml + form.js + css + (resx?). Backend/liste/detay/diğer bölüm diff YOK.
- **E4:** header'da Listeye Dön solunda canlı "Zorunlu: N/M" rozeti (form doldukça artar, tamamlanınca yeşil); Yaşam Döngüsü notu amber callout kutusu ("Archive" bold); validate/kaydetme aynı; tema-duyarlı.

---

## §36.1 Agent Prompt (paste-ready) — DISPATCH: owner (F14 landing SONRASI)
```text
@[.antigravity/agents/frontend-specialist.md]
WP: WP-FREQ-F15 · Header "Zorunlu: N/M" rozeti + "Silme yok/Archive" amber callout (MOD-0165-FU03, frontend)
Repository: C:\Users\user\Desktop\ERP-vNext (ANA checkout)
Branch: feature/scmm-content-studio · Expected HEAD: <F14 commit> üstü · Worktree: ana checkout

Önce oku: execution/domains/commercial-suite/work-packs/WP-FREQ-F15-header-required-badge-archive-note.md · frontend/Diten.Web/Views/CRM/VisitFrequencyPolicies/_Editor.cshtml (header BackToList satır ~143 + .vfp-life-note satır ~469) · wwwroot/assets/js/CRM/VisitFrequencyPolicies/form.js (updateChecklist/updatePanel/validate — sayım için mantık; validate'i DEĞİŞTİRME) · visit-frequency-create.css.

NE (frontend; validate/buildPayload/id KORUNUR):
 1) Header: BackToList <a>'nın SOLUNA <span id="vfpRequiredBadge"> ekle; form.js canlı "{Required}: {satisfied}/{total}" doldurur (total=zorunlu * alan sayısı; satisfied=geçerli olanlar, validate mantığından SALT-OKUNUR türet, validate()'i çağırma/hata basma). Amber pill (--bs-warning-*), tamamsa yeşil (--bs-success-*). Form değişiminde güncelle (updateChecklist akışına bağla).
 2) .vfp-life-note → amber callout: bg var(--bs-warning-bg-subtle), border 1px var(--bs-warning-border-subtle), color var(--bs-warning-text-emphasis), radius6 padding10/12 text-wrap:pretty; "Archive" <strong>.
KORU/YAPMA: validate/buildPayload/id/kaydetme DEĞİŞMEZ (rozet salt-okunur); #vfpCheckBadge (F13) DEĞİŞMEZ; backend/liste/Detay/Çözümleme/resolve.js/index.js/Segment/diğer bölümler DOKUNMA; tema-duyarlı; yalnız _Editor+form.js+css+resx.
DOĞRULA (E2): Diten.Web.Tests 137/0; git diff yalnız _Editor+form.js+css(+resx); backend/liste/detay diff yok. Ayrı commit. §22 TÜRKÇE. K13.
Durma: rozet sayımı validate'i tetikliyor/bozuyorsa; kapsam header+life-note dışına taşarsa → DUR+raporla.
```
## §37 CT bağımsız doğrulama (2026-09-17) → **ACCEPTED (E2)**
```
Commit: c85b4e0a · Agent: PASS · CT: ACCEPTED E2 (izole temiz build) · /c/tmp/ct-f15-verify @c85b4e0a
```
- ✅ Kapsam: _Editor.cshtml + form.js + visit-frequency-create.css (3 dosya). backend/resx/liste/detay = 0.
- ✅ Header → Task deseni (h5 üstte, breadcrumb mb-0 altta, sağda d-flex gap-2 actions); `#vfpRequiredBadge` BackToList solunda canlı "Zorunlu: N/10" (`requiredStatus()` salt-okunur, validate çağırmadan; amber→tamamsa yeşil). `.vfp-life-note` amber callout (--bs-warning-* subtle). `.vfp-btn-armed` box-shadow kaldırıldı.
- ✅ validate/buildPayload/vfpCheckBadge(F13) silinmemiş (grep 0). Diten.Web.Tests 137/0.
- ℹ️ `.vfp-editor-head` CSS yetim kaldı (zararsız, bilerek bırakıldı).
```
