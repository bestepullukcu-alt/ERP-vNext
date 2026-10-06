# WORK PACKAGE — WP-CT-FE-8 · Zincir Şablonu editörü 3 sütun → 2 sütun (Task Create düzeni) (frontend, markup-only)

> **CT (SoR).** MOD-0162 Chain Template v2. Branch `feature/crm-chain-template` (L10N-LEGACY `22182eed` / §37 `a3e30ef1` üstü). **Kullanıcı isteği (2026-09-27):** `/CRM/KnowledgeConcepts/Templates/Create` kartları **3 sütun (3/6/3)** yerine **Task Create gibi 2 sütun (8/4)**. **Yalnız `_TemplateForm.cshtml` grid/kart yerleşimi**; JS/id/L10n/backend DEĞİŞMEZ. Create + Edit aynı partial'ı kullandığı için ikisi birlikte değişir.

## Kanıt
- **Mevcut** `Views/CRM/KnowledgeConcepts/_TemplateForm.cshtml:27-160`: `row g-4` →
  - `col-lg-3`: Kimlik kartı (Konu, Zincir Kodu, Zincir Adı, Moderatör, Kimin İçin, Açıklama — hepsi `col-12`) + Tip Paleti kartı
  - `col-lg-6`: Dallar kartı (`tplBranches` diyagram/lane, FE-3/4/REFINE)
  - `col-lg-3`: Sağ panel sekmeleri (`tplSidePanel`: Bağlantılar/Uyumsuz/Sürümler, FE-5) + Referans kartı (Sürüm, Durum, Geçerlilik başlangıç/bitiş, `btnTplPublish`)
- **Hedef referans** `Views/Tasks/_Form.cshtml`: `row g-4` → `col-12 col-lg-8` (ana kartlar, içte `col-md-6` ikili alan ızgarası) + `col-12 col-lg-4` (yan kartlar, satır 457).
- `template-form.js` sütun sınıflarına bağlı DEĞİL (yalnız id'ler: `tplTypePalette`, `tplBranches`, `tplSidePanel`, … ; drag `document` üstünde delegasyonla) → kart taşımak davranışı bozmaz.

## NE (yalnız markup)
1. **Sol `col-12 col-lg-8`:**
   - **Kimlik kartı** — iç ızgara ikili: `Konu` | `Zincir Kodu` (`col-md-6`), `Zincir Adı` | `Moderatör` (`col-md-6`), `Kimin İçin` (`col-12`), `Açıklama` (`col-12`). form-text ipuçları alanlarının altında kalır.
   - **Dallar kartı** (diyagram) — aynen, artık daha geniş (8/12).
2. **Sağ `col-12 col-lg-4`** (sıra):
   - **Tip Paleti kartı** (en üstte — diyagramın hemen yanında, sürükle-bırak mesafesi kısa)
   - **Sağ panel sekmeleri** (`tplSidePanel`)
   - **Referans kartı** (Sürüm | Durum ikili `col-md-6`, Geçerlilik başlangıç | bitiş ikili `col-md-6`; `btnTplPublish` en altta `w-100`)
3. `tplReadOnlyBand`, alert'ler, form attribute'ları, yayın modalı (`tplPublishModal`) yerinde kalır. FE-2 yorum bloğunu "2 sütun (Task Create düzeni)" olarak güncelle.
4. **Duyarlılık:** `lg` altında tek sütuna yığılır (sol kartlar üstte, sonra palet/sekme/referans) — mevcut davranışla aynı.

## KORU / YAPMA
- **Hiçbir id/class/`name`/`data-*` değişmez** (template-form.js'in kullandığı tüm `tpl*`, `btnTpl*`, `js-*`). Alanlar yalnız **taşınır**, yeniden yazılmaz; label/Localizer/required işaretleri aynen.
- **template-form.js, concept-slim.js, CSS, resx, köprü (`_TemplateFormL10n`), controller, backend DEĞİŞMEZ.** Yeni L10n anahtarı YOK.
- Diyagram/lane iç yerleşimi (FE-3 grid, FE-3-REFINE DAL-X/dropzone), drag (FE-4), sekmeler (FE-5), yayın diyaloğu/salt-okunur (FE-6) davranışı aynı kalır.
- Liste sekmesi, quick-view (`_TemplateDetailsQuickView`), diğer sayfalar DOKUNMA.
- **DUR:** diyagram 8/12 genişlikte lane grid'i bozuluyorsa (taşma/kırılma) → CSS'e dokunmadan raporla; bir alan taşınınca select2 genişliği bozuluyorsa (`tpl-select2` init) → raporla.

## Acceptance
- **E2:** `dotnet test frontend/Diten.Web.Tests/Diten.Web.Tests.csproj -c Release --nologo` → **229/0**. git diff: **yalnız `_TemplateForm.cshtml`**. id seti öncesi/sonrası birebir aynı (grep ile say: `id="` sayısı ve listesi eşit).
- **E4 (tarayıcı, fleet restart gerekmez — cshtml):** Create + Edit sayfası: sol geniş (Kimlik ikili alanlar + Dallar), sağ dar (Palet → Sekmeler → Referans+Yayınla). Paletten diyagrama sürükle-bırak çalışır; "+tip ekle" klavye yolu çalışır; kaydet (create + edit) çalışır; yayınlanmış şablonda salt-okunur bant + "Yeni sürüm" çalışır; `lg` altı tek sütun.

---

## §36.1 Agent Prompt (paste-ready) — DISPATCH: owner
```text
@[.antigravity/agents/frontend-specialist.md]
WP: WP-CT-FE-8 · Zincir Şablonu editörü 3 sütun → 2 sütun (Task Create düzeni) (MOD-0162, frontend, markup-only)
Repository: C:\Users\user\Desktop\ERP-vNext (ANA checkout)
Branch: feature/crm-chain-template · Worktree: ana checkout

Kullanıcı isteği: /CRM/KnowledgeConcepts/Templates/Create kartları 3 sütun (3/6/3) yerine Task Create gibi 2 sütun (8/4). Yalnız _TemplateForm.cshtml grid/kart yerleşimi; JS/id/L10n/backend DEĞİŞMEZ. Create+Edit aynı partial → ikisi birlikte.

Önce oku: execution/domains/commercial-suite/work-packs/WP-CT-FE-8-two-column-layout.md · frontend/Diten.Web/Views/CRM/KnowledgeConcepts/_TemplateForm.cshtml (row g-4, 27-160) · frontend/Diten.Web/Views/Tasks/_Form.cshtml (referans: col-lg-8 + col-lg-4 satır 47/457, içte col-md-6 ikili alanlar).

NE (yalnız markup):
 1) Sol col-12 col-lg-8: Kimlik kartı iç ızgara ikili (Konu|Zincir Kodu col-md-6, Zincir Adı|Moderatör col-md-6, Kimin İçin col-12, Açıklama col-12) + Dallar kartı (diyagram, aynen).
 2) Sağ col-12 col-lg-4 sırayla: Tip Paleti kartı (en üst) → tplSidePanel sekmeleri → Referans kartı (Sürüm|Durum col-md-6, Geçerlilik başlangıç|bitiş col-md-6, btnTplPublish en altta w-100).
 3) tplReadOnlyBand/alert'ler/form attribute'ları/tplPublishModal yerinde; FE-2 yorum bloğunu "2 sütun (Task Create düzeni)" diye güncelle. lg altı tek sütun.
KORU/YAPMA: hiçbir id/class/name/data-* değişmez (tpl*, btnTpl*, js-*); alanlar yalnız TAŞINIR (label/Localizer/required aynen); template-form.js/concept-slim.js/CSS/resx/köprü/controller/backend DEĞİŞMEZ; yeni L10n anahtarı YOK; diyagram iç düzeni (FE-3/REFINE), drag (FE-4), sekmeler (FE-5), yayın/salt-okunur (FE-6) aynı; liste/quick-view/diğer sayfalar DOKUNMA.
DOĞRULA (E2): cd C:\Users\user\Desktop\ERP-vNext; dotnet test frontend/Diten.Web.Tests/Diten.Web.Tests.csproj -c Release --nologo → 229/0; git diff yalnız _TemplateForm.cshtml; id="..." listesi öncesi/sonrası birebir aynı (say + karşılaştır). Tarayıcı: Create+Edit 8/4 düzen, paletten diyagrama sürükle-bırak, "+tip ekle" klavye yolu, kaydet (create+edit), yayınlanmışta salt-okunur bant+Yeni sürüm, lg altı tek sütun. Ayrı commit ("feat(crm): WP-CT-FE-8 — chain template editor two-column layout (Task Create pattern) (MOD-0162)" + son satır Co-Authored-By: Claude Opus 5.5 <noreply@anthropic.com>). §22 TÜRKÇE. K13.
Durma: diyagram 8/12'de lane grid'i bozuluyorsa CSS'e dokunmadan raporla; select2 genişliği taşımadan bozuluyorsa raporla.
```

## §37 CT bağımsız doğrulama (2026-09-27) → **ACCEPTED (E2)**
```
Commit: f6a9243c · Agent: PASS (229/0, canlı Create sayfası görsel+etkileşim) · CT: ACCEPTED E2 · izole worktree /c/tmp/ct-fe8 @f6a9243c → Diten.Web.Tests 229/0
```
- ✅ **Kapsam:** yalnız `_TemplateForm.cshtml` (+23/−25). JS/CSS/resx/köprü/controller/backend diff YOK.
- ✅ **id seti:** öncesi (a3e30ef1) / sonrası 40 = 40, `diff` boş. Localizer/required/data-* imza hash'i öncesi=sonrası (birebir).
- ✅ **Düzen (kod okundu):** `col-12 col-lg-8` (Kimlik ikili col-md-6 ×4 + Dallar) · `col-12 col-lg-4` (Tip Paleti → `tplSidePanel` → Referans ikili col-md-6 ×4 + Yayınla).
- ✅ **Agent canlı E4 (TUTUKON, kayıtsız):** 1440px 8/4; paletten sürükle → "Dal A · 1 adım"; "+ Tip ekle" odak picker'a; select2/tarih taşmıyor; 820px tek sütun, yatay kaydırma yok.
- ⚠ **Canlı doğrulanmadı:** kaydet + Edit + yayınlanmış görünüm (tenant'ta şablon yok; agent veri yazmadı). JS değişmediği ve id'ler aynı olduğu için risk düşük → kullanıcı canlı E4'ünde kapanacak.
- ⚠ **Süreç notu:** agent'ın tarayıcı test iskeleti, kullanıcının oturum açık `/WorkCenterNext` sekmesini kısa süre lokal mock'la değiştirdi (sunucuya istek yok, geri yüklendi). Sonraki WP'lerde: **oturum açık sekmede mock/harness enjekte etme; ayrı sekme kullan.**

**WP-CT-FE-8 KOMPLE (E2).** Kalan: (ops.) quick-view hizalama · canlı E4 (kaydet/Edit/yayınla/yeni sürüm) · push + PR.
