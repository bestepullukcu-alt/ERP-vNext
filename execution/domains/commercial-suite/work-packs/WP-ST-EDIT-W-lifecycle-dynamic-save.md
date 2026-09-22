# WORK PACKAGE — WP-ST-EDIT-W · Yaşam döngüsü: dinamik Kaydet (aktif/engellendi/ve aktifleştir) + tek-tık kaydet+aktifleştir (mockup) (frontend + Web controller)

> **CT (SoR).** MOD-0167-FU04. Branch `feature/crm-scmm-studio` (`1f738a09` üstü — WP-ST-EDIT-V §37 sonrası). **Kullanıcı kararı (2 soru):** (1) "Tam mockup: dinamik Kaydet + engellendi"; (2) "Tek tık kaydet+aktifleştir (mockup birebir)" — **görevler-ayrılığı (SoD) bilinçli gevşetiliyor** (aktifleştirme artık authoring Kaydet'inden de yapılabilir, yine ActivatePermission ile korunur). **Frontend (form.js + _SidePanel.cshtml + _Form.cshtml + _IndexL10n + 7 resx) + Web controller/ViewModel (Diten.Web; CrmService/domain/contract DEĞİŞMEZ).**

## Kanıt
- **Mockup save mantığı:** `saveLabel = canSave ? (status==='active' ? "Kaydet (aktif)" : "Kaydet ve aktifleştir") : "Kaydet (engellendi)"`; `saveDisabled = !canSave`; `canSave = scopeOk && segOk && totalOk && okContent && mdmOk`. Yeni sürüm al alt-metni "Mevcut oyunu dondurur, {nextVersion} kopyası açılır". Taslak/Aktif toggle = mevcut durumu gösterir (target seçici değil).
- **form.js `updateSidePanel` (1104-1183):** readiness zaten hesaplanıyor — `navKeys` (identity/scope/segments/frequency/products/content) 'warn' değilse `done`; `done===total`. `canSave` = **hiçbir `.st-check` `is-warn` değil** (mdm dahil). `state` + `norm`/`weightSum`/`skuLinesConsistent` mevcut.
- **_SidePanel.cshtml YAŞAM DÖNGÜSÜ:** iki submit butonu — `Save` (btn-primary) + `SaveDraft` (btn-label-secondary), ikisinin de **name/value/formaction'ı YOK** (aynı POST). `isEdit` → divider + `#btnPanelNewVersion` ("Yeni sürüm al" title + `NewVersionDetailTpl` alt-metin) + `#btnPanelArchive` ("Arşivle") ZATEN var. Taslak/Aktif tabs display. EffectiveFrom/To dates + (Zorunlu badge) da burada — **DOKUNMA** (bu WP kapsamı yalnız Kaydet/aktifleştir).
- **Controller (Diten.Web):** `Create` POST (83) başarı→`envelope.Data` id + `RedirectToAction(Edit,{id})`; `Edit` POST (129) başarı→`RedirectToAction(Details,{id})`. `Activate` proxy (196-199) `POST /api/crm/strategy-templates/{id}/activate`, `ActivatePermission="crm.strategy-template.activate"` (28). `SendGatewayAsync` helper mevcut. `TemplateStatus` hidden **display-only** (ViewModel).
- **cfg bootstrap (_Form 322-):** `subjectType`/`areBindingsFrozen` var; **`templateStatus` YOK** — eklenecek (dinamik etiket için mevcut durum gerek).

## NE
### A) Frontend — dinamik Kaydet (form.js + _SidePanel + _Form + resx + L köprüsü)
1. **Primary Kaydet butonu id + dinamik:** `_SidePanel` primary submit'e `id="btnPrimarySave"`. `updateSidePanel` sonunda:
   - `canSave = !document.querySelector('.st-check.is-warn')` (tüm bölümler hazır; mdm dahil).
   - `status = cfg.templateStatus || 'draft'`.
   - `!canSave` → label `SaveBlocked`="Kaydet (engellendi)", `disabled=true`, `dataset.mode='blocked'`.
   - `canSave && status==='active'` → label `SaveActive`="Kaydet (aktif)", enabled, `mode='save'`.
   - `canSave && status!=='active'` → label `SaveAndActivate`="Kaydet ve aktifleştir", enabled, `mode='activate'`.
2. **Tek-tık kaydet+aktifleştir flag:** `_Form` içine hidden `<input asp-for="ActivateAfterSave" form="strategyTemplateForm" id="activateAfterSave" value="false" />`. `#btnPrimarySave` click → `#activateAfterSave`.value = (`dataset.mode==='activate'`) ? 'true' : 'false' (submit'ten önce; disabled ise zaten submit olmaz). `SaveDraft` click → 'false'. (Böylece yalnız "Kaydet ve aktifleştir" aktifleştirir; "Kaydet (aktif)" zaten aktif oyunu kaydeder, flag'e gerek yok.)
3. **cfg.templateStatus:** `_Form` bootstrap'a `templateStatus = Model.TemplateStatus` ekle; form.js `cfg.templateStatus` okur.
4. **Yeni sürüm al/Arşivle:** mevcut (isEdit) KORUNUR — mockup alt-metni `NewVersionDetailTpl` zaten "Mevcut oyunu dondurur…"; değişiklik gerekmez (yalnız doğrula).
5. **resx (7 dil) + L köprüsü:** yeni `SaveActive`/`SaveAndActivate`/`SaveBlocked` (7 dil); form.js'in okuduğu bu 3 anahtarı `_IndexL10n` whitelist'e ekle. PascalCase köprü.

### B) Web controller/ViewModel — kaydet+aktifleştir orkestrasyonu (Diten.Web; CrmService DEĞİŞMEZ)
6. **ViewModel:** `StrategyTemplateEditViewModel`'e `public bool ActivateAfterSave { get; set; }`.
7. **Create POST (83):** başarılı create + `id` alındıktan sonra: **`model.ActivateAfterSave && HasAnyPermission(ActivatePermission)`** ise `SendGatewayAsync(POST, "/api/crm/strategy-templates/{id}/activate", null, ct)` çağır; başarılıysa `TempData["SuccessMessage"]=RecordActivated`, `RedirectToAction(Details,{id})`; activate başarısızsa (403/409/…) oyun yine KAYDEDİLDİ → `TempData["WarningMessage"]` (kaydedildi, aktifleştirilemedi) + `RedirectToAction(Edit,{id})`. Flag yoksa MEVCUT davranış (RedirectToAction Edit).
8. **Edit POST (129):** başarılı update sonrası aynı desen: flag+permission ise activate → Details; activate başarısız → uyarı + Details (zaten Details'e gidiyor). Flag yoksa mevcut (Details).
9. **Permission:** activate yalnız `HasAnyPermission(ActivatePermission)` ile denenir (SoD gevşedi ama izinle korunur). Idempotency: activate zaten aktifse gateway 409/200 döndürebilir — hata değil, uyarı ile geç.

## KORU / YAPMA
- **CrmService/domain/contract/aggregate DEĞİŞMEZ** — yalnız Diten.Web (controller orkestrasyonu = mevcut activate endpoint'ini çağırır, yeni endpoint/komut YOK). `TemplateStatus` hidden display-only KALIR (flag ayrı). Save/SaveDraft submit akışı (ToCreatePayload/ToUpdatePayload) DEĞİŞMEZ; yalnız başarıdan SONRA opsiyonel activate eklenir. EffectiveFrom/To + Taslak/Aktif tabs + Zorunlu + BÖLÜMLER/recipe/stat + Yeni sürüm al/Arşivle mevcut davranışı KORUNUR. Segment/Frekans/KAPSAM/Ürün+SKU/İçerik DOKUNMA. Liste/Detay/details.js DOKUNMA. Tema/L10n köprüsü.
- **DUR:** activate çağrısı kaydı geri alıyorsa/oyunu bozuyorsa (activate ayrı işlem, save'i etkilememeli) → DUR+raporla. `canSave` hesabı mevcut readiness'ı bozuyorsa → DUR+raporla.

## Acceptance
- **E2:** Diten.Web.Tests 201/0. git diff: form.js + _SidePanel.cshtml + _Form.cshtml + StrategyTemplateViewModels.cs + StrategyTemplatesController.cs + _IndexL10n.cshtml + 7 resx. **CrmService/MdmService/domain/contract/liste/detay(details.js) diff YOK.**
- **E4 (FLEET RESTART):** hazır değilken Kaydet="Kaydet (engellendi)" pasif; taslak+hazır→"Kaydet ve aktifleştir" → tıkla→kaydeder+aktifleştirir→Detay (aktif); aktif+hazır→"Kaydet (aktif)" → kaydeder; Taslak kaydet→taslak kalır; düzenlemede Yeni sürüm al (+alt metin) + Arşivle. Activate izni yoksa→kaydedildi+uyarı (aktifleştirilemedi). **Razor+cs+resx → FLEET RESTART.**

---

## §36.1 Agent Prompt (paste-ready) — DISPATCH: owner
```text
@[.antigravity/agents/frontend-specialist.md]
WP: WP-ST-EDIT-W · Yaşam döngüsü dinamik Kaydet (aktif/engellendi/ve aktifleştir) + tek-tık kaydet+aktifleştir (MOD-0167-FU04; frontend + Diten.Web controller)
Repository: C:\Users\user\Desktop\ERP-vNext (ANA checkout)
Branch: feature/crm-scmm-studio · Expected HEAD: 1f738a09 üstü · Worktree: ana checkout

Kullanıcı: mockup birebir — dinamik Kaydet + engellendi + tek-tık kaydet+aktifleştir (SoD bilinçli gevşetildi; ActivatePermission ile korunur). CrmService/domain/contract DEĞİŞMEZ; aktifleştirme MEVCUT activate endpoint'i Web controller'dan çağrılır.

Önce oku: execution/domains/commercial-suite/work-packs/WP-ST-EDIT-W-lifecycle-dynamic-save.md · frontend/Diten.Web/wwwroot/assets/js/CRM/StrategyTemplates/form.js (updateSidePanel 1104-1183, canSave=hiçbir .st-check.is-warn) · frontend/Diten.Web/Views/CRM/StrategyTemplates/_SidePanel.cshtml (YAŞAM DÖNGÜSÜ: Save/SaveDraft + isEdit btnPanelNewVersion/btnPanelArchive) · _Form.cshtml (bootstrap 322 + hidden alanlar) · _IndexL10n.cshtml (L köprüsü) · frontend/Diten.Web/Models/CRM/StrategyTemplateViewModels.cs (StrategyTemplateEditViewModel) · frontend/Diten.Web/Controllers/CRM/StrategyTemplatesController.cs (Create POST 83, Edit POST 129, Activate 196, ActivatePermission 28, SendGatewayAsync) · 7 resx.

NE:
A) Frontend:
 1) _SidePanel primary submit'e id=btnPrimarySave. updateSidePanel sonunda: canSave=!document.querySelector('.st-check.is-warn'); status=cfg.templateStatus||'draft'. !canSave→label SaveBlocked "Kaydet (engellendi)" disabled dataset.mode=blocked; canSave&&active→SaveActive "Kaydet (aktif)" mode=save; canSave&&!active→SaveAndActivate "Kaydet ve aktifleştir" mode=activate.
 2) _Form içine hidden <input asp-for="ActivateAfterSave" form="strategyTemplateForm" id="activateAfterSave" value="false"/>. btnPrimarySave click→#activateAfterSave.value=(dataset.mode==='activate')?'true':'false'; SaveDraft click→'false'.
 3) _Form bootstrap'a templateStatus=Model.TemplateStatus; form.js cfg.templateStatus okur.
 4) Yeni sürüm al/Arşivle (isEdit) KORU (yalnız doğrula, NewVersionDetailTpl alt-metni zaten var).
 5) resx 7 dil SaveActive/SaveAndActivate/SaveBlocked + _IndexL10n whitelist'e ekle; PascalCase köprü.
B) Web controller/ViewModel (Diten.Web; CrmService DEĞİŞMEZ):
 6) StrategyTemplateEditViewModel + public bool ActivateAfterSave.
 7) Create POST: create başarı+id sonrası model.ActivateAfterSave && HasAnyPermission(ActivatePermission) ise SendGatewayAsync(POST,"/api/crm/strategy-templates/{id}/activate",null,ct); başarı→TempData Success=RecordActivated + RedirectToAction(Details,{id}); activate başarısız→TempData Warning + RedirectToAction(Edit,{id}). Flag yoksa mevcut (Edit).
 8) Edit POST: update başarı sonrası aynı desen (flag+izin→activate→Details; başarısız→uyarı+Details). Flag yoksa mevcut (Details).
 9) activate yalnız izinle denenir; zaten aktif→gateway 409/200 hata değil, uyarıyla geç. Save'i asla geri alma.
KORU/YAPMA: CrmService/domain/contract/aggregate DEĞİŞMEZ (yeni endpoint/komut YOK, mevcut activate çağrılır); TemplateStatus hidden display-only KALIR; ToCreatePayload/ToUpdatePayload + save akışı DEĞİŞMEZ (yalnız başarıdan sonra opsiyonel activate); EffectiveFrom/To+tabs+Zorunlu+BÖLÜMLER+recipe+stat+Yeni sürüm al/Arşivle KORU; Segment/Frekans/KAPSAM/Ürün+SKU/İçerik DOKUNMA; Liste/Detay/details.js DOKUNMA; tema/L10n köprüsü.
DOĞRULA (E2): cd C:\Users\user\Desktop\ERP-vNext; dotnet test frontend/Diten.Web.Tests/Diten.Web.Tests.csproj -c Release --nologo → 201/0; git diff form.js+_SidePanel+_Form+ViewModels+Controller+_IndexL10n+7 resx; CrmService/MdmService/domain/contract/details.js diff yok. Ayrı commit ("feat(strategy): WP-ST-EDIT-W — Yaşam döngüsü dinamik Kaydet (aktif/engellendi/ve aktifleştir) + tek-tık kaydet+aktifleştir (MOD-0167-FU04)" + son satır Co-Authored-By: Claude Opus 4.8 <noreply@anthropic.com>). §22 TÜRKÇE. K13.
Durma: activate save'i geri alıyorsa/bozuyorsa; canSave mevcut readiness'ı bozuyorsa → DUR+raporla.
```

## §37 CT bağımsız doğrulama (2026-09-22) → **ACCEPTED (E2)**
```
Commit: f963cda9 · Agent: PASS · CT: ACCEPTED E2 (izole temiz build) · /c/tmp/ct-steitw-verify @f963cda9
```
- ✅ **Kapsam (20 dosya, +153/−6):** form.js + _SidePanel + _Form + _IndexL10n + ViewModels + Controller + 7 StrategyTemplatesIndex resx + **7 SharedResource resx**. **CrmService/MdmService/domain/contract/StrategyTemplateModels/details.js/liste/detay TEMİZ** ✓.
- ✅ **Scope sapması HAKLI (14 resx):** controller TempData `_sharedLocalizer` (SharedResource) kullanıyor; `RecordActivated`/`SavedNotActivated` SharedResource'ta yoktu → 7 SharedResource resx zorunlu. WP "7 resx" diyordu; +7 SharedResource gerekli ve doğru (aksi halde toast ham anahtar gösterirdi). Kabul.
- ✅ **Dinamik Kaydet:** `updateSidePanel` sonu (1184+) `#btnPrimarySave`: `canSave = !document.querySelector('.st-check.is-warn')` (mdm dahil, pill/bar readiness'ıyla aynı); status=cfg.templateStatus. !canSave→SaveBlocked disabled mode=blocked; active→SaveActive mode=save; draft→SaveAndActivate mode=activate. Click→#activateAfterSave (mode==='activate'→'true'); SaveDraft→'false'. Yeni sürüm al/Arşivle korundu.
- ✅ **KORU=0:** `ActivateAfterSave` **ToCreate/ToUpdatePayload'a GİRMEZ** (yalnız orkestrasyon sinyali). `ActivateAfterSaveAsync` (337) save başarısından SONRA ayrı activate POST'u; başarı→RecordActivated+Details; başarısız→SavedNotActivated uyarı + failureAction redirect (**save'i asla geri almaz**; 409/403 hata değil). Mevcut activate endpoint'i çağrılır — **yeni endpoint/komut YOK, CrmService DEĞİŞMEZ**.
- ✅ **Build+test (CT izole, Release):** Diten.Web.Tests **201/0** (Diten.Web derlendi → controller/ViewModel geçerli).
- ⏳ E4: dinamik Kaydet + tek-tık kaydet+aktifleştir. **FLEET RESTART.**
- ⚠ **Minör E4 notu (F-ST-WARNING-TOAST):** _Layout yalnız SuccessMessage/ErrorMessage toast'lar, **WarningMessage'ı DEĞİL** → activate başarısızsa "SavedNotActivated" uyarısı toast olmaz (oyun yine kaydedilir, SuccessMessage görünür, Edit'te kalır). 97c5 Admin ActivatePermission'a sahip → activate başarılı (RecordActivated) olur; uyarı yolu nadir. Düşük etkili; layout WarningMessage desteği ayrı housekeeping.

**WP-ST-EDIT-W KOMPLE (E2). Yaşam döngüsü mockup-tam. Düzenle sayfası bütünüyle bitti (Kimlik/KAPSAM/Segment/Frekans/Ürün+SKU/İçerik/Yaşam döngüsü).**
