# WORK PACKAGE — WP-CT-FE-6 · Yayınla onay diyaloğu + salt-okunur yayın görünümü (frontend, mockup v2)

> **CT (SoR).** MOD-0162 Chain Template. Branch `feature/crm-chain-template`. Zincir Şablonu v2 mockup **Ekran 5 (yayınla diyaloğu) + Ekran 6 (yayınlanmış görünüm)**. Mevcut iskele üstüne (templateReadOnly + tplReadOnlyBand + startNewVersion zaten var): **yayınla onay diyaloğu** (donacak omurga + engelleyen/uyaran kontroller) + **kilit bandı içeriği**. **Frontend.** Backend (BE-B publish guard'ları) DEĞİŞMEZ — diyalog **ön-uç ayna**, BE-B yine enforce eder. FE-5 üstüne.

## Kanıt
- **Mevcut iskele** (`template-form.js`): `templateReadOnly` (85) her yerde saygı görüyor (spineStatus Frozen 224, drag 369, aksiyon 453); `startNewVersion()` (668) var (draft'a döndürür, frozen temizler, chainCode editable, btnTplNewVersion gizler); `tplStatus` select (draft/published) + submit `status: val('tplStatus')` (642). `tplReadOnlyBand` div'i (`_TemplateForm.cshtml:25`, d-none, boş). `btnTplNewVersion` var.
- **BE-B publish guard'ları (backend, mevcut):** status=published'da her dal ≥2 adım + ForWhom ≥1 (yoksa 400); omurga ≥2 tip. Overlap V13. → diyalog bunları **ön-uçta gösterir**, BE-B enforce eder.
- **Mockup yayınla diyaloğu:** donacak omurga (tip etiketleri) + etki (bağlı çıktılar yeni sürüme, eski arşive) · **engelleyen** (≥2 tip · ≥2 adım/dal · ≥1 kitle → biri geçmezse buton pasif) · **engellemeyen uyarı** (çözülmemiş uyumsuz ilişkiler) · "Yayınla ve dondur".
- **Mockup yayınlanmış görünüm:** kilit bandı (salt-okunur, omurga dondu) + "Yeni sürüm oluştur".

## NE (frontend; backend DEĞİŞMEZ)
1. **Yayınla onay diyaloğu (modal):** "Yayınla ve dondur" butonu → modal:
   - **Donacak omurga** = `spineFromBranches()` → tip adları etiket.
   - **Etki metni** (bağlı çıktılar yeni sürüme geçer, önceki yayın arşivlenir).
   - **Engelleyen kontroller** (client ön-uç, pass/fail listesi): omurga ≥2 farklı tip · her dal ≥2 adım · ForWhom ≥1. **Biri fail → "Yayınla ve dondur" pasif** (hangisi eksik net). *(BE-B server-side yine enforce eder — çift kalkan.)*
   - **Engellemeyen uyarı**: çözülmemiş uyumsuz ilişki sayısı (FE-5 diagnostics `unresolved()`); yayını engellemez, uyarı gösterir.
   - Onayla → `tplStatus=published` set + mevcut submit akışı → başarıda salt-okunur/redirect (mevcut davranış).
2. **Kilit bandı** (`tplReadOnlyBand` doldur, templateReadOnly iken göster): "Bu sürüm yayında ve salt-okunur; omurga donmuş" + **"Yeni sürüm oluştur"** butonu → mevcut `startNewVersion()`.
3. **tplStatus dropdown:** yayınlama artık diyalog üzerinden birincil; dropdown kalabilir (draft↔published) ama published seçimi de aynı guard'lara tabi (diyalogla tutarlı) — tercihen publish'i diyaloğa yönlendir. Archived asla status olarak set edilmez (mevcut kural korunur).
4. **L10n (7 dil):** `PublishDialogTitle`/"Yayınla ve dondur", `PublishEffect`, `PublishBlockMinTypes`/`PublishBlockBranchSteps`/`PublishBlockAudience`, `PublishWarnUnresolved`, `PublishConfirm`, `ReadOnlyBandText`, `NewVersion`(var mı kontrol) + köprü.

## KORU / YAPMA
- **Backend (BE-B publish guardّları) DEĞİŞMEZ** — diyalog yalnız ön-uç ayna; gerçek kapı BE-B (server enforce). **Submit/veri modeli/spineFromBranches/diyagram(FE-3)/drag(FE-4)/sağ-sekmeler(FE-5) DEĞİŞMEZ** — publish = `tplStatus=published` + mevcut submit. `startNewVersion()` (mevcut) reuse — yeniden yazma. `templateReadOnly` mantığı korunur. Conformance enforce YOK (D8) — çözülmemiş uyumsuz **engellemez** (yalnız uyarı). Liste/diğer-tab/backend DOKUNMA. Tema/L10n köprüsü. Modal = Bootstrap (mevcut).
- **DUR:** publish'i diyaloğa yönlendirmek mevcut tplStatus-dropdown-save akışını bozuyorsa (ikisi çakışırsa) → dropdown'ı koru + publish butonunu ek yol yap + raporla; startNewVersion mevcut değilse (beklenmez) → DUR.

## Acceptance
- **E2:** `dotnet test frontend/Diten.Web.Tests/Diten.Web.Tests.csproj -c Release --nologo` → yeşil. git diff: template-form.js + _TemplateForm.cshtml (+ resx). **backend/liste/diğer-tab diff YOK.**
- **E4 (FLEET RESTART):** taslakta "Yayınla ve dondur" → diyalog (donacak omurga + engelleyen kontroller: eksikse buton pasif + hangisi eksik; çözülmemiş uyumsuz uyarısı engellemez) → onayla → yayınlanır (BE-B guard'ları da geçmeli); yayınlanmış görünümde kilit bandı + "Yeni sürüm oluştur" → taslak v+1 (resolutions miras). Guard eksikken buton pasif; BE-B 400'ü de yakalanır.

---

## §36.1 Agent Prompt (paste-ready) — DISPATCH: owner
```text
@[.antigravity/agents/frontend-specialist.md]
WP: WP-CT-FE-6 · Yayınla onay diyaloğu + salt-okunur yayın görünümü (MOD-0162, frontend, mockup v2)
Repository: C:\Users\user\Desktop\ERP-vNext (ANA checkout)
Branch: feature/crm-chain-template · Worktree: ana checkout

Amaç: Zincir Şablonu v2 yayın akışı. Yayınla onay diyaloğu (donacak omurga + engelleyen/uyaran kontroller) + kilit bandı içeriği. Backend (BE-B publish guard'ları) DEĞİŞMEZ — diyalog ön-uç ayna, BE-B enforce eder. Mevcut templateReadOnly + tplReadOnlyBand + startNewVersion reuse.

Önce oku: execution/domains/commercial-suite/work-packs/WP-CT-FE-6-publish-dialog-readonly.md · frontend/Diten.Web/wwwroot/assets/js/CRM/KnowledgeConcepts/template-form.js (templateReadOnly 85, spineStatus 223, startNewVersion 668, tplStatus submit 642, spineFromBranches, unresolved()[FE-5]) · frontend/Diten.Web/Views/CRM/KnowledgeConcepts/_TemplateForm.cshtml (tplReadOnlyBand 25, tplStatus 135, btnTplNewVersion) · _TemplateFormL10n + resx (7 dil) · memory l10n-bridge-pascalcase-loader.

NE (frontend; backend DEĞİŞMEZ):
 1) "Yayınla ve dondur" butonu → Bootstrap modal: donacak omurga (spineFromBranches→tip adları) + etki metni + ENGELLEYEN kontroller (client: omurga ≥2 tip / her dal ≥2 adım / ForWhom ≥1 → biri fail→onay butonu pasif, hangisi eksik net) + ENGELLEMEYEN uyarı (unresolved() sayısı). Onayla → tplStatus=published + mevcut submit → başarıda mevcut redirect/read-only.
 2) tplReadOnlyBand doldur (templateReadOnly iken göster): "yayında/salt-okunur/omurga dondu" + "Yeni sürüm oluştur" → mevcut startNewVersion().
 3) Publish birincil diyalog; tplStatus dropdown kalabilir ama published aynı guard'lara tabi; archived asla status set edilmez.
 4) L10n 7 dil: PublishDialogTitle/PublishEffect/PublishBlockMinTypes/PublishBlockBranchSteps/PublishBlockAudience/PublishWarnUnresolved/PublishConfirm/ReadOnlyBandText/NewVersion + köprü.
KORU/YAPMA: backend(BE-B guard) DEĞİŞMEZ (diyalog ön-uç ayna, server enforce); submit/veri modeli/spineFromBranches/diyagram(FE-3)/drag(FE-4)/sağ-sekme(FE-5) DEĞİŞMEZ (publish=tplStatus=published+submit); startNewVersion reuse (yeniden yazma); conformance enforce YOK (D8, çözülmemiş uyumsuz engellemez=yalnız uyarı); templateReadOnly korunur; liste/diğer-tab/backend DOKUNMA; tema/L10n köprüsü; Bootstrap modal.
DOĞRULA (E2): cd C:\Users\user\Desktop\ERP-vNext; dotnet test frontend/Diten.Web.Tests/Diten.Web.Tests.csproj -c Release --nologo → yeşil; git diff template-form.js+_TemplateForm.cshtml(+resx); backend/liste/diğer-tab diff yok. Ayrı commit ("feat(crm): WP-CT-FE-6 — chain template publish confirm dialog + read-only published view (MOD-0162)" + son satır Co-Authored-By: Claude Opus 4.8 <noreply@anthropic.com>). §22 TÜRKÇE. K13.
Durma: publish-diyaloğu mevcut tplStatus-save akışını bozuyorsa dropdown'ı koru+publish ek yol+raporla; startNewVersion yoksa DUR.
```

## §37 CT bağımsız doğrulama (2026-09-25) → **ACCEPTED (E2)**
```
Commit: abdaa1f1 · CT: ACCEPTED E2 (landed-then-verified — rapor FE-5 tekrarı geldiği için gecikmeli) · izole worktree /c/tmp/ct-fe6 @abdaa1f1 → Diten.Web.Tests 229/0
```
- ✅ **Kapsam (10 dosya, +194/−2):** 7 resx (+11 anahtar) · _TemplateForm (+32) · _TemplateFormL10n · template-form.js (+82). **backend/liste/diğer-tab/submit diff YOK.**
- ✅ **Publish dialog (kod okundu):** `publishChecks` = spine ≥2 tip · her **boş-olmayan** dal ≥2 adım (boş dallar gönderilmez) · ≥1 kitle → biri fail'de buton pasif + hangisi eksik; **unresolved non-conforming = non-blocking uyarı (D8)**. Yorum: "client-side MIRROR of WP-CT-BE-B publish guards — service still enforces (400 save-error gibi gösterilir)". Confirm = `tplStatus=published` + mevcut save; published save bu diyaloğa yönlendiriliyor.
- ✅ **Read-only band:** `renderReadOnlyBand` → `tplReadOnlyBand` doldurur (metin + "New version" butonu → **mevcut `startNewVersion()` reuse**, yeniden yazılmadı); yeni draft band'i düşürür + publish butonunu geri verir.
- ✅ **Build+test:** Diten.Web.Tests **229/0**.

**WP-CT-FE-6 KOMPLE (E2).** Chain Template v2 zinciri: BE-A/B + FE-1..6 hepsi CT-E2. Kalan: **FE-3-REFINE** (dal başlığı DAL-X + boş-dal drag-dropzone + çip sadeleştirme — kullanıcı E4 farkı) + **FE-7** (L10n süpürme + save nihai) + canlı E4 + push.

