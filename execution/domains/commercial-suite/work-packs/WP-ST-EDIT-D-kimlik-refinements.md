# WORK PACKAGE — WP-ST-EDIT-D · Düzenle Kimlik kartı rötuşları (Notlar kaldır, Açıklama placeholder, Taslak+vN badge) (frontend)

> **CT (SoR).** MOD-0167-FU04. Branch `feature/crm-scmm-studio` (`c3433cc2` üstü). **E4 rötuşu (canlı Kimlik kartı):** (1) Notlar alanı kaldırılsın; (2) Açıklama placeholder'ı eklensin; (3) Kimlik kartı başlığının sağına **Taslak** (durum) + **vN** (sürüm) badge'leri. **Frontend** (`_Form.cshtml` Kimlik section + resx). Backend/js DEĞİŞMEZ.

## Kanıt
- `_Form.cshtml` Kimlik section (~27-52): TemplateCode + TemplateName + Description(textarea) + **Notes(textarea, ~47-48)**. Header `<h6>IdentitySection</h6>` — badge yok.
- ViewModel: `TemplateStatus` (string) + `TemplateVersion` (int) mevcut → badge kaynağı. `Notes` (string?) opsiyonel.

## NE (frontend; backend DEĞİŞMEZ)
1. **Notlar kaldır:** görünür Notes textarea'sını Kimlik section'dan çıkar. **Veri kaybını önle:** `asp-for="Notes"` **hidden input** olarak kalsın (edit'te mevcut Notes değeri POST'ta korunur, silinmez — TemplateStatus deseniyle aynı).
2. **Açıklama placeholder:** Description textarea'ya `placeholder` = "Bu oyun kimi, hangi hikâye ile, hangi ürün ağırlığıyla hedefliyor?" (L10n anahtarı, ör. `DescriptionPlaceholder`; 7 dil).
3. **Kimlik başlığı badge'leri:** `<h6>` satırını flex satıra çevir (başlık solda, badge'ler sağda): **durum badge** (Model.TemplateStatus → localized: draft→"Taslak"/active→"Aktif"/inactive/archived; renk mockup gibi nötr/secondary) + **sürüm badge** ("v" + Model.TemplateVersion). Display-only (Model'den; form alanı değil). Mockup: sağ üstte "Taslak" + "v1".

## KORU / YAPMA
- Backend/js/ViewModel/Controller DEĞİŞMEZ (Notes hidden ile korunur; badge'ler Model okur). Kimlik dışındaki bölümler (KAPSAM/Segment/Frekans/Ürün/İçerik) + sağ panel + submit DOKUNMA. Liste/Detay/details.js/CrmService DOKUNMA. Notes VM property'si kalır. Tema/L10n köprüsü. Durum/sürüm etiketleri mevcut L10n (varsa reuse: TemplateStatus_* / ScopeType deseni).

## Acceptance
- **E2:** Diten.Web.Tests 201/0. git diff: _Form.cshtml + resx. Backend/js/Controller/Liste/Detay diff YOK.
- **E4:** Kimlik kartında Notlar yok; Açıklama placeholder görünür; başlık sağında Taslak+vN badge; edit'te mevcut Notes kaybolmaz. **Razor+resx → FLEET RESTART.**

---

## §36.1 Agent Prompt (paste-ready) — DISPATCH: owner
```text
@[.antigravity/agents/frontend-specialist.md]
WP: WP-ST-EDIT-D · Düzenle Kimlik kartı rötuşları (MOD-0167-FU04, frontend)
Repository: C:\Users\user\Desktop\ERP-vNext (ANA checkout)
Branch: feature/crm-scmm-studio · Expected HEAD: c3433cc2 üstü · Worktree: ana checkout

Önce oku: execution/domains/commercial-suite/work-packs/WP-ST-EDIT-D-kimlik-refinements.md · frontend/Diten.Web/Views/CRM/StrategyTemplates/_Form.cshtml (Kimlik section ~27-52) · Models/CRM/StrategyTemplateViewModels.cs (TemplateStatus/TemplateVersion/Notes) · resx (StrategyTemplates* — form L10n).

NE (frontend; backend/js DEĞİŞMEZ):
 1) Notes görünür textarea'sını Kimlik'ten çıkar; asp-for="Notes" HIDDEN input olarak bırak (edit'te değer korunur).
 2) Description textarea'ya placeholder = "Bu oyun kimi, hangi hikâye ile, hangi ürün ağırlığıyla hedefliyor?" (yeni L10n DescriptionPlaceholder, 7 dil).
 3) Kimlik <h6> başlığını flex satır yap: başlık solda + sağda durum badge (Model.TemplateStatus localized: draft→Taslak/active→Aktif/...) + sürüm badge ("v"+Model.TemplateVersion). Display-only, Model'den.
KORU/YAPMA: backend/js/ViewModel/Controller DEĞİŞMEZ; Notes VM property kalır (hidden korur); KAPSAM/Segment/Frekans/Ürün/İçerik/sağ panel/submit DOKUNMA; Liste/Detay/details.js/CrmService DOKUNMA; durum etiketi mevcut L10n reuse (varsa); tema/L10n köprüsü.
DOĞRULA (E2): cd C:\Users\user\Desktop\ERP-vNext; dotnet test frontend/Diten.Web.Tests/Diten.Web.Tests.csproj -c Release --nologo → 201/0; git diff _Form.cshtml+resx; backend/js/Controller/Liste/Detay diff yok. Ayrı commit ("feat(strategy): WP-ST-EDIT-D — Düzenle Kimlik kartı (Notlar kaldır+hidden, Açıklama placeholder, Taslak+vN badge) (MOD-0167-FU04)" + son satır Co-Authored-By: Claude Opus 4.8 <noreply@anthropic.com>). §22 TÜRKÇE. K13.
Durma: Notes hidden edit değerini bozuyorsa; badge Model alanı yoksa → DUR+raporla.
```
## §37 CT bağımsız doğrulama (2026-09-21) → **ACCEPTED (E2)**
```
Commit: 28ac5949 · Agent: PASS · CT: ACCEPTED E2 (izole temiz build) · /c/tmp/ct-steditd-verify @28ac5949
```
- ✅ **Kapsam (8 dosya, +29/−6):** _Form.cshtml + 7 resx. **backend/js/ViewModel/Controller/liste/detay/panel TEMİZ** ✓.
- ✅ Notlar görünür textarea kaldırıldı → `<input type="hidden" asp-for="Notes">` (edit'te değer korunur; Notes VM property dokunulmadı). Açıklama placeholder (`DescriptionPlaceholder` 7 dil). Kimlik `<h6>` flex satır: sol başlık + sağ durum badge (Model.TemplateStatus localized, mevcut L10n reuse) + sürüm badge (v{TemplateVersion}); bg-label-secondary, display-only.
- ✅ **Build+test (CT izole, Release):** Diten.Web.Tests **201/0**.
- ⏳ E4: Kimlik'te Notlar yok + placeholder + Taslak/vN badge. **Razor+resx → FLEET RESTART.**

**WP-ST-EDIT-D KOMPLE.**
```
