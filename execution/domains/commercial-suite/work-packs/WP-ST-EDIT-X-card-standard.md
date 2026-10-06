# WORK PACKAGE — WP-ST-EDIT-X · 9 section card'ı Task/Create golden kart standardına getir (ikon + tipografi) (frontend)

> **CT (SoR).** MOD-0167-FU04. Branch `feature/crm-scmm-studio` (`f963cda9` üstü — WP-ST-EDIT-W §37 sonrası). **Kullanıcı roadmap adım 1:** StrategyTemplate Düzenle ekranındaki **9 section card'ı** (6 _Form + 3 _SidePanel) **Task/Create golden kart standardına** getir. **Yalnız kart chrome'u (başlık ikonu + tipografi + boşluk); işlev/JS/id/backend DEĞİŞMEZ.** Frontend (_Form.cshtml + _SidePanel.cshtml + strategy-create.css; resx yeni anahtar gerekmez).

## Kanıt (golden standart)
- **Task/Create kartı** (`Views/Tasks/_Form.cshtml:49-51`): `<section class="card mb-4"><div class="card-body p-4"><h6 class="text-uppercase text-heading fw-semibold mb-4 d-flex align-items-center gap-2"><i class="bx bx-<icon> dt-card-icon" aria-hidden="true"></i>@Localizer[...]</h6>`. `.dt-card-icon` = `flex:0 0 auto; font-size:1.125rem; color:var(--bs-primary)` (backbone-custom.css:6857). segment-create.css + visit-frequency-create.css notları: "shell same as /Tasks/Create with h6.text-uppercase.text-heading + bx dt-card-icon" → bu **uygulamanın kart standardı**.
- **6 _Form kartı (mevcut):** hepsi `card mb-4`+`card-body p-4`+`h6 text-uppercase text-heading fw-semibold` KULLANIYOR ama **ikon YOK** ve mb tutarsız: Kimlik (`d-flex justify-content-between mb-4`>h6 mb-0 + Taslak/v badge), KAPSAM (h6 mb-2 + sub-header), Segment (`d-flex justify-content-between mb-1`>h6 mb-0), Frekans (h6 mb-3), Ürün (`d-flex justify-content-between mb-1`>h6 mb-0 + Σ badge), İçerik (h6 mb-1).
- **3 _SidePanel kartı (mevcut, farklı chrome):** `card st-card mb-3`+`card-body p-3` + **`st-card-kicker`** span başlık (WhatTitle `st-card-head`; SectionsTitle `st-sections-head`+ready-pill; LifecycleActionsTitle `st-card-kicker d-block mb-2`). İkon YOK, standart h6 değil.
- **JS bağı:** form.js panel içeriğini **id** ile okur (`stRecipe*`/`stStat*`/`stReadyPill`/`stReadyBar`/`stChecklist`/`btnPrimarySave`/`btnSaveDraft`/`btnPanelNewVersion`/`btnPanelArchive`). Section id'leri (`segmentBindingSection`/`frequencySection`/`productSection`/`contentSection`) form.js/CSS'te kullanılıyor. **Bunların HİÇBİRİ değişmez.**

## NE (frontend; işlev/JS/id/backend DEĞİŞMEZ)
1. **6 _Form kartı — başlığa ikon + standart:** her section h6'sına başa `<i class="bx bx-<icon> dt-card-icon" aria-hidden="true"></i>` ekle ve h6'yı `d-flex align-items-center gap-2` yap (ikon+başlık). Başlığı bir aksiyon-satırında olan kartlarda (Kimlik badge'leri, Segment, Ürün Σ) **dış `d-flex justify-content-between` KORUNUR** — h6 kendi içinde ikon+başlık olur, sağdaki öğe (badge/Σ) yerinde kalır. Tek-başlık kartlar (KAPSAM/Frekans/İçerik) standart boşlukla (mockup/helper düzeni korunarak). İkonlar (semantik; **agent iconify-icons.css'te VAR olduğunu doğrulasın**, yoksa tanımlı alternatif — EDIT-R'deki gibi):
   - Kimlik → `bx-purchase-tag-alt` (yoksa `bx-id-card`)
   - KAPSAM (nerede) → `bx-map` (yoksa `bx-target-lock`)
   - Segment (kim) → `bx-group`
   - Frekans (ne sıklıkta) → `bx-time-five` (yoksa `bx-calendar-check`)
   - Ürün+SKU (ne) → `bx-package`
   - İçerik (hangi hikâye) → `bx-book-content` (yoksa `bx-book-open`)
2. **3 _SidePanel kartı — başlığı standarda çevir:** `st-card-kicker` span'ları → standart `<h6 class="text-uppercase text-heading fw-semibold mb-3 d-flex align-items-center gap-2"><i class="bx bx-<icon> dt-card-icon" aria-hidden="true"></i>@Localizer[...]</h6>`. BÖLÜMLER'de sağdaki `#stReadyPill` mevcut flex head'de kalır (h6 + pill yan yana). Kart gövdesi/iç yapı (recipe/stats/checklist/lifecycle butonları + **tüm id'ler**) DEĞİŞMEZ. İkonlar:
   - BU OYUN NE YAPACAK (WhatTitle) → `bx-bulb` (yoksa `bx-detail`)
   - BÖLÜMLER (SectionsTitle) → `bx-list-check` (yoksa `bx-check-square`)
   - YAŞAM DÖNGÜSÜ (LifecycleActionsTitle) → `bx-refresh` (yoksa `bx-recycle`)
3. **css (strategy-create.css):** side-panel başlık standarda geçince artık kullanılmayan `.st-card-kicker`/`.st-card-head` kuralları **kaldırılabilir veya bırakılabilir** (zararsız); h6 stili global backbone'dan gelir. `.st-sections-head` (h6+pill flex) hizası korunur. Kart gövdesi p-3 (dar panel yoğunluğu) KORUNUR — yalnız başlık chrome'u standarda gelir. 6 form kartı zaten `card mb-4`/`card-body p-4`; ikon dışında yapı değişmez.

## KORU / YAPMA
- **Yalnız kart başlık chrome'u (ikon + h6 tipografi + hizalama).** İşlev/JS/CSS-davranış DEĞİŞMEZ: tüm **id'ler** (stRecipe*/stStat*/stReadyPill/stReadyBar/stChecklist/btnPrimarySave/btnSaveDraft/btnPanelNewVersion/btnPanelArchive/section id'leri/input asp-for/activateAfterSave) + form.js/updateSidePanel/dinamik Kaydet + Σ/progress/checklist + picker'lar + save/activate akışı KORUNUR. Kimlik badge'leri (Taslak/v) + Ürün Σ badge + Segment/Ürün aksiyon satırları KORUNUR (yalnız h6'ya ikon eklenir). resx başlık metinleri DEĞİŞMEZ (yeni anahtar yok). Backend/CrmService/Controller/ViewModel/details.js/Liste/Detay DOKUNMA. Tema/L10n köprüsü.
- **DUR:** bir ikon iconify-icons.css'te yoksa ve uygun tanımlı alternatif bulunamıyorsa → DUR+raporla (tofu ikon bırakma). Başlık standardizasyonu bir id'yi/JS bağını kırıyorsa → DUR+raporla.

## Acceptance
- **E2:** Diten.Web.Tests 201/0. git diff: _Form.cshtml + _SidePanel.cshtml + strategy-create.css (+ resx YOK). **form.js/backend/Controller/ViewModel/details.js/liste/detay diff YOK.**
- **E4 (FLEET RESTART):** 9 kartın hepsinde başlık = ikon (primary renk, dt-card-icon) + text-uppercase text-heading fw-semibold, Task/Create ile aynı dilde; tüm işlevler (dinamik Kaydet, checklist, recipe, stats, picker'lar, Yeni sürüm/Arşivle) eskisi gibi çalışır; ikonlar render olur (tofu yok). **Razor+css → FLEET RESTART.**

---

## §36.1 Agent Prompt (paste-ready) — DISPATCH: owner
```text
@[.antigravity/agents/frontend-specialist.md]
WP: WP-ST-EDIT-X · 9 section card'ı Task/Create golden kart standardına getir (ikon + tipografi) (MOD-0167-FU04, frontend)
Repository: C:\Users\user\Desktop\ERP-vNext (ANA checkout)
Branch: feature/crm-scmm-studio · Expected HEAD: f963cda9 üstü · Worktree: ana checkout

Golden standart: <section class="card mb-4"><div class="card-body p-4"><h6 class="text-uppercase text-heading fw-semibold mb-4 d-flex align-items-center gap-2"><i class="bx bx-<icon> dt-card-icon" aria-hidden="true"></i>Başlık</h6> (Tasks/_Form.cshtml:49-51; .dt-card-icon backbone-custom.css:6857; segment-create/visit-frequency-create aynısını kullanır).

Önce oku: execution/domains/commercial-suite/work-packs/WP-ST-EDIT-X-card-standard.md · frontend/Diten.Web/Views/Tasks/_Form.cshtml (49-51 örnek) · frontend/Diten.Web/Views/CRM/StrategyTemplates/_Form.cshtml (6 kart h6: 45 Kimlik/85 KAPSAM/174 Segment/189 Frekans/253 Ürün/312 İçerik) · _SidePanel.cshtml (3 kart st-card-kicker: WhatTitle/SectionsTitle+stReadyPill/LifecycleActionsTitle) · wwwroot/assets/css/strategy-create.css (.st-card/.st-card-kicker/.st-card-head/.st-sections-head) · wwwroot/assets/vendor/fonts/iconify-icons.css (ikon VAR mı doğrula).

NE (yalnız kart başlık chrome'u; işlev/JS/id/backend DEĞİŞMEZ):
 1) 6 _Form h6'sına başa <i class="bx bx-<icon> dt-card-icon" aria-hidden="true"></i> + h6 d-flex align-items-center gap-2. Aksiyon-satırlı kartlarda (Kimlik badge/Segment/Ürün Σ) dış d-flex justify-content-between KORU (h6 içinde ikon+başlık; sağ öğe yerinde). İkon: Kimlik bx-purchase-tag-alt / KAPSAM bx-map / Segment bx-group / Frekans bx-time-five / Ürün bx-package / İçerik bx-book-content (her birini iconify'da doğrula; yoksa tanımlı alternatif).
 2) 3 _SidePanel st-card-kicker → standart <h6 text-uppercase text-heading fw-semibold mb-3 d-flex align-items-center gap-2><i bx bx-<icon> dt-card-icon></i>Başlık</h6>. BÖLÜMLER'de #stReadyPill flex head'de kalır. Gövde/iç yapı + TÜM id'ler DEĞİŞMEZ. İkon: WhatTitle bx-bulb / SectionsTitle bx-list-check / LifecycleActionsTitle bx-refresh (doğrula).
 3) css: kullanılmayan .st-card-kicker/.st-card-head kaldırılabilir/bırakılabilir (zararsız); .st-sections-head hizası + panel p-3 yoğunluğu KORU; 6 form kartı yapısı ikon dışında değişmez.
KORU/YAPMA: yalnız başlık chrome'u; tüm id'ler (stRecipe*/stStat*/stReadyPill/stReadyBar/stChecklist/btnPrimarySave/btnSaveDraft/btnPanelNewVersion/btnPanelArchive/section id/input asp-for/activateAfterSave) + form.js + dinamik Kaydet + Σ/progress/checklist/picker + save/activate akışı DEĞİŞMEZ; Kimlik/Ürün badge + aksiyon satırları KORU; resx başlık metinleri DEĞİŞMEZ (yeni anahtar yok); backend/CrmService/Controller/ViewModel/details.js/Liste/Detay DOKUNMA; form.js'e DOKUNMA; tema/L10n köprüsü.
DOĞRULA (E2): cd C:\Users\user\Desktop\ERP-vNext; dotnet test frontend/Diten.Web.Tests/Diten.Web.Tests.csproj -c Release --nologo → 201/0; git diff _Form+_SidePanel+strategy-create.css; form.js/backend/Controller/ViewModel/details.js/liste/detay diff yok; resx diff yok. Ayrı commit ("feat(strategy): WP-ST-EDIT-X — 9 section card Task/Create golden standardı (dt-card-icon + h6 tipografi) (MOD-0167-FU04)" + son satır Co-Authored-By: Claude Opus 4.8 <noreply@anthropic.com>). §22 TÜRKÇE. K13.
Durma: ikon iconify'da yok + alternatif yoksa (tofu bırakma); başlık standardı bir id/JS bağını kırıyorsa → DUR+raporla.
```

## §37 CT bağımsız doğrulama (2026-09-22) → **ACCEPTED (E2)**
```
Commit: 5f7d8d4d · Agent: PASS · CT: ACCEPTED E2 (izole temiz build) · /c/tmp/ct-steitx-verify @5f7d8d4d
```
- ✅ **Kapsam (3 dosya, +11/−22):** _Form.cshtml + _SidePanel.cshtml + strategy-create.css. **form.js/backend/CrmService/Controller/ViewModel/details.js/resx/liste/detay TEMİZ** ✓.
- ✅ **9 ikon (6 _Form + 3 _SidePanel):** Kimlik bx-purchase-tag-alt / KAPSAM bx-map / Segment bx-group / Frekans bx-time-five / Ürün bx-package / İçerik bx-book-content / WhatTitle bx-bulb / SectionsTitle bx-list-check / LifecycleActionsTitle bx-refresh — hepsi golden `h6 text-uppercase text-heading fw-semibold d-flex align-items-center gap-2` + `dt-card-icon`. Tümü iconify-icons.css'te doğrulandı (tofu yok).
- ✅ **KORU=0:** 11 kritik id (stReadyPill/stReadyBar/stChecklist/btnPrimarySave/btnSaveDraft/btnPanelNewVersion/btnPanelArchive + segmentBindingSection/productSection/contentSection/frequencySection) hepsi korundu; aksiyon-satırlı kartlarda (Kimlik badge/Segment/Ürün Σ) dış d-flex justify-content-between + sağ öğe yerinde; kullanılmayan `.st-card-head`/`.st-card-kicker` CSS kaldırıldı (grep: başka view kullanmıyor). form.js/işlev/save-activate/checklist/recipe DEĞİŞMEDİ.
- ✅ **Build+test (CT izole, Release):** Diten.Web.Tests **201/0**.
- ⏳ E4: 9 kart Task/Create ile aynı başlık dili (ikon+tipografi). **FLEET RESTART.**

**WP-ST-EDIT-X KOMPLE (E2). Roadmap adım 1 bitti. Sonraki: Faz 3 Detay page.**
