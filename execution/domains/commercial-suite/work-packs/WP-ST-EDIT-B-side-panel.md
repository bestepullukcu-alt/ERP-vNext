# WORK PACKAGE — WP-ST-EDIT-B · StrategyTemplate Düzenle: sağ panel (özet + BÖLÜMLER checklist + yaşam döngüsü) + frekans info-box + görsel (frontend)

> **CT (SoR).** MOD-0167-FU04. Branch `feature/crm-scmm-studio` (`c2b72820` üstü). **Faz 2b** (Düzenle authoring görsel tamamlama): mockup sağ panel **"BU OYUN NE YAPACAK?"** canlı özet + **BÖLÜMLER** checklist + **YAŞAM DÖNGÜSÜ** aksiyonları; **Frekans** info-box ("Ziyaret Sıklığı Politikaları'nda yönetilir — işaretçi"); iki-kolon layout. **Frontend** (Create/Edit.cshtml layout + _Form/side-panel + form.js canlı güncelleme + css + resx). Backend DEĞİŞMEZ.

## İlke: VFP editörü sağ-panel desenini AYNALA
VisitFrequencyPolicy editörü (`_Editor.cshtml` + form.js + `visit-frequency-create.css` `.vfp-create-scope`) tam bu deseni taşıyor: sol bölümler + sağ sticky panel (canlı "ne yapacak" özeti + KAYDETMEDEN ÖNCE canlı checklist ✓/!/– + yaşam döngüsü status+aksiyon). StrategyTemplate'in kendi veri modeline (scope + segment/ürün/içerik binding'leri + frekans) uyarlanır.

## Kanıt (mevcut)
- StrategyTemplate `Create.cshtml`/`Edit.cshtml`: TEK KOLON `_Form` partial; submit header'da (`form="strategyTemplateForm"`). Sağ panel YOK. `_Form`: Kimlik/**KAPSAM**(2a)/Segment/Frekans/Ürün/İçerik/Classification/Lifecycle. `form.js`: binding-builder'lar + 2a scope cascade.
- Ayna: `Views/CRM/VisitFrequencyPolicies/_Editor.cshtml` (iki-kolon + sağ panel markup) + `wwwroot/assets/js/CRM/VisitFrequencyPolicies/form.js` (canlı özet + checklist update) + `wwwroot/assets/css/visit-frequency-create.css`.
- Mockup Düzenle sağ panel: **BU OYUN NE YAPACAK?** (NEREDE=scope breadcrumb / KİM=N segment·tip / NE SIKLIKTA=frekans politikası / NE=N ürün·Σ% / HİKAYE=N içerik + cümle + 4 stat tile) · **BÖLÜMLER 4/5 hazır** (Kimlik/Kapsam/Segmentler/Frekans/Ürün+SKU%/İçerik/**MDM doğrulaması** — ✓ text-success/! text-warning/– nötr + dinamik alt-metin) · **YAŞAM DÖNGÜSÜ** (Taslak/Aktif + Kaydet/Taslak kaydet/Yeni sürüm al/Arşivle).

## NE (frontend; backend DEĞİŞMEZ)
1. **Layout** (Create.cshtml + Edit.cshtml): iki-kolon (sol = mevcut `_Form` sol bölümleri, sağ = sticky panel). VFP editör layout aynası. Header submit → yaşam-döngüsü paneline taşınabilir (mockup: aksiyonlar sağ panelde) veya header korunur + panel özet/checklist gösterir (VFP deseni: aksiyonlar panelde). Mockup'a uy: Kaydet/Taslak kaydet/Yeni sürüm/Arşivle sağ panelde.
2. **Sağ panel — BU OYUN NE YAPACAK** (canlı, form.js): NEREDE (2a ÇÖZÜMLENEN KAPSAM'dan) / KİM (segment sayısı+tip) / NE SIKLIKTA (frekans mod/politika) / NE (ürün sayısı+Σ ağırlık%) / HİKAYE (içerik sayısı) + özet cümle + 4 stat tile (segment / ürün satırı / SKU/ağırlık toplamı / sabitlenmiş içerik). Form durumundan canlı türetilir (uydurma yok).
3. **Sağ panel — BÖLÜMLER checklist** (canlı): Kimlik / Kapsam / Segmentler / Frekans / Ürün+SKU% / İçerik / MDM doğrulaması — her biri ✓(tamam)/!(eksik/uyarı)/–(opsiyonel-boş) + dinamik alt-metin (ör. "N segment", "Σ %"). "N/M hazır" sayacı. Kurallar gerçek form durumundan (SKU%=100 mi, en az 1 segment vb.).
4. **Frekans info-box:** Frekans section'a mockup uyarısı — "Frekans, Ziyaret Sıklığı Politikaları'nda yönetilir; bu alan yalnızca referans (işaretçi)." + "Politikaları aç ↗" linki (mevcut MOD-0165 sayfası). Frekans alan mantığı DEĞİŞMEZ (yalnız açıklama + link).
5. **css + resx (7 dil):** panel/özet/checklist/stat/yaşam-döngüsü stilleri (VFP scoped-css aynası ya da Golden Compact reuse; mockup görsel fidelity) + L10n anahtarları (BU OYUN NE YAPACAK, NEREDE/KİM/NE SIKLIKTA/NE/HİKAYE, BÖLÜMLER, section adları, checklist alt-metinleri, YAŞAM DÖNGÜSÜ, frekans info). PascalCase köprü.

## KORU / YAPMA
- Backend DEĞİŞMEZ. 2a KAPSAM cascade + Segment/Frekans/Ürün/İçerik binding-builder + SKU%/homojen/pinned + submit/create-update akışı DOKUNMA (yalnız layout iki-kolon + sağ panel EKLE + frekans info-box + canlı özet/checklist form.js'e EKLE). Liste/Detay/details.js/index.js DOKUNMA. Canlı özet/checklist yalnız gerçek form durumundan (uydurma değer yok). Yeni sürüm/Arşiv aksiyonları mevcut endpoint/mantıkla (yeni backend yok). Tema/L10n köprüsü. Aktif/dondurulmuş sürümde binding salt-okunur davranışı (varsa) korunur.

## Acceptance
- **E2:** Diten.Web.Tests 201/0. git diff: Create/Edit.cshtml + _Form (veya yeni _SidePanel partial) + form.js + css + resx. CrmService/Liste/Detay diff YOK.
- **E4:** Düzenle iki-kolon; sağ panel canlı özet (form doldukça güncellenir) + BÖLÜMLER checklist (✓/!/–, N/M hazır) + yaşam döngüsü aksiyonları; Frekans info-box + link; mockup görsel. **Razor+resx+css → FLEET RESTART.**

---

## §36.1 Agent Prompt (paste-ready) — DISPATCH: owner
```text
@[.antigravity/agents/frontend-specialist.md]
WP: WP-ST-EDIT-B · StrategyTemplate Düzenle sağ panel + frekans info-box + görsel (MOD-0167-FU04, frontend)
Repository: C:\Users\user\Desktop\ERP-vNext (ANA checkout)
Branch: feature/crm-scmm-studio · Expected HEAD: c2b72820 üstü · Worktree: ana checkout

Önce oku: execution/domains/commercial-suite/work-packs/WP-ST-EDIT-B-side-panel.md · AYNA VFP editörü: Views/CRM/VisitFrequencyPolicies/_Editor.cshtml (iki-kolon + sağ panel özet/checklist/lifecycle) + wwwroot/assets/js/CRM/VisitFrequencyPolicies/form.js (canlı özet+checklist update) + wwwroot/assets/css/visit-frequency-create.css (.vfp-create-scope) · HEDEF StrategyTemplate: Views/CRM/StrategyTemplates/{Create.cshtml,Edit.cshtml,_Form.cshtml} + wwwroot/assets/js/CRM/StrategyTemplates/form.js + Models/CRM/StrategyTemplateViewModels.cs (yeni alan gerekmez) + resx. Mockup: /c/tmp/mockup-strategy.html "Düzenle" sağ panel (gerekirse py -m http.server ile aç).

NE (frontend; backend DEĞİŞMEZ):
 1) Create.cshtml+Edit.cshtml iki-kolon layout (sol _Form bölümleri, sağ sticky panel — VFP aynası). Kaydet/Taslak kaydet/Yeni sürüm al/Arşivle sağ panelde (mockup).
 2) Sağ panel BU OYUN NE YAPACAK (canlı, form.js): NEREDE(2a scope)/KİM(N segment·tip)/NE SIKLIKTA(frekans)/NE(N ürün·Σ ağırlık%)/HİKAYE(N içerik) + cümle + 4 stat tile. Form durumundan türet.
 3) Sağ panel BÖLÜMLER checklist (canlı): Kimlik/Kapsam/Segmentler/Frekans/Ürün+SKU%/İçerik/MDM doğrulaması → ✓/!/– + dinamik alt-metin + "N/M hazır". Gerçek form durumundan (SKU%=100, ≥1 segment vb.).
 4) Frekans info-box: "Ziyaret Sıklığı Politikaları'nda yönetilir — işaretçi" + "Politikaları aç ↗" link. Frekans alan mantığı DEĞİŞMEZ.
 5) css + resx 7 dil: panel/checklist/stat/lifecycle stil (VFP scoped-css aynası ya da Golden Compact) + L10n. PascalCase köprü.
KORU/YAPMA: backend DEĞİŞMEZ; 2a KAPSAM cascade + binding-builder(segment/ürün/içerik) + SKU%/homojen/pinned + submit/create-update akışı DOKUNMA (yalnız layout+panel+info-box+canlı özet/checklist EKLE); Liste/Detay/details.js/index.js DOKUNMA; canlı değerler gerçek form durumundan (uydurma yok); yeni sürüm/arşiv mevcut endpoint; tema/L10n köprüsü; aktif/dondurulmuş salt-okunur davranışı korunur.
DOĞRULA (E2): cd C:\Users\user\Desktop\ERP-vNext; dotnet test frontend/Diten.Web.Tests/Diten.Web.Tests.csproj -c Release --nologo → 201/0; git diff Create/Edit/_Form(+_SidePanel)+form.js+css+resx; CrmService/Liste/Detay diff yok. Ayrı commit ("feat(strategy): WP-ST-EDIT-B — Düzenle sağ panel (özet+BÖLÜMLER checklist+yaşam döngüsü) + frekans info-box (MOD-0167-FU04)" + son satır Co-Authored-By: Claude Opus 4.8 <noreply@anthropic.com>). §22 TÜRKÇE. K13.
Durma: canlı özet/checklist binding-builder state'ini bozuyorsa; layout submit akışını kırıyorsa; kapsam dışına (backend/liste/detay) taşarsa → DUR+raporla.
```
## §37 CT bağımsız doğrulama (2026-09-21) → **ACCEPTED (E2)**
```
Commit: 58944fba · Agent: PASS · CT: ACCEPTED E2 (izole temiz build) · /c/tmp/ct-steditb-verify @58944fba
```
- ✅ **Kapsam (15 dosya, +1043/−36):** Create/Edit.cshtml(iki-kolon) + _SidePanel.cshtml(YENİ) + _Form.cshtml(+9 sadece frekans info-box) + form.js(**+183/−0 saf additive**) + strategy-create.css(YENİ) + _IndexL10n + 7 resx. **CrmService/Liste/Detay/details.js/index.js/Controller/ViewModel TEMİZ** ✓.
- ✅ **VFP editör aynası:** iki-kolon `.st-editor-layout` + sağ sticky `_SidePanel`; "BU OYUN NE YAPACAK" canlı özet (NEREDE/KİM/NE SIKLIKTA/NE/HİKAYE + cümle + 4 stat tile) form durumundan türetilir; BÖLÜMLER checklist (Kimlik/Kapsam/Segmentler/Frekans/Ürün+SKU%/İçerik/MDM doğrulaması → ✓/!/– + "N/M hazır" + ilerleme çubuğu, gerçek kurallar: SKU%=100&satır-ağırlığı=100, ≥1 segment vb.); YAŞAM DÖNGÜSÜ (Kaydet/Taslak kaydet aynı `form="strategyTemplateForm"` MVC POST; Yeni sürüm/Arşivle mevcut endpoint'ler — yeni backend yok).
- ✅ **Frekans info-box:** "Ziyaret Sıklığı Politikaları'nda yönetilir — işaretçi" + "Politikaları aç ↗" → /CRM/VisitFrequencyPolicies; frekans alan mantığı değişmedi.
- ✅ **KORU=0:** 2a KAPSAM cascade + segment/frekans/ürün/içerik binding-builder + SKU%/homojen/pinned + submit/create-update akışı korundu (form.js additive-only); dondurulmuş (`AreBindingsFrozen`) salt-okunur korundu.
- ✅ **Build+test (CT izole, Release):** Diten.Web.Tests **201/0**.
- ⏳ E4: Düzenle iki-kolon + canlı özet/checklist + info-box. **Razor+resx+css → FLEET RESTART.**

**WP-ST-EDIT-B KOMPLE (Faz 2b). Düzenle sayfası TAM. Kalan: Faz 3 (Detay — KAPSAM salt-okunur + sürüm geçmişi + Kampanyada kullan).**
```
