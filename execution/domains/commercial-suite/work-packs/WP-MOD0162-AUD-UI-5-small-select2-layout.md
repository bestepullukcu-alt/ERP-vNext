# WORK PACKAGE — WP-MOD0162-AUD-UI-5 · Compose-row select'leri small select2 + layout taşma düzeltmesi

> **Control Tower kaydı (SoR).** Kullanıcı manuel-test: Dimensions compose-row select'leri **native/büyük** render oluyor + eklenen chip "Add" butonunun üstüne biniyor (layout taşma). İstek: **select'leri small select2 yap**. Module: **MOD-0162** (AudienceProfile Dimensions). Branch: `feature/scmm-content-studio` (HEAD `6cc8641f`). **Yalnız frontend** (Diten.Web); backend DOKUNMA. **SIRA:** Subject WP'sinden önce.
> **AUD-UI-4 (6cc8641f) durumu:** E2 yeşildi ama **kullanıcı görsel E4'ü reddetti** → CT ACCEPTED etmedi; bu WP üstüne düzeltir (birlikte doğrulanır).

## Ölçülmüş girdi (CT) — kök neden
- **compose select2 small değil:** `taxonomy.js initComposeSelect2` (~688) `#composeAxis`/`#composeValues` için `.select2({dropdownParent,minimumResultsForSearch,width})` çağırıyor ama **`selectionCssClass: 'form-select form-select-sm'` YOK** → varsayılan (büyük) boyutta render. Mevcut çalışan small desen: `initFilterSelect2` (~167) `selectionCssClass:'form-select form-select-sm'` kullanıyor.
- **Layout taşma:** compose-row `.diten-checkitem-text.d-flex gap-2` içinde Axis(12rem sabit)+Values(flex-grow)+Add; büyük select2 + multi-chip'ler dar satırda taşıp Add'in üstüne biniyor (kullanıcı ekranı: "Pharmacist ×" chip'i "Add axis" üstünde).
- **Korunacak (AUD-UI-3/4):** compose-then-add, ValueCode saklama, Name " / ", cascade, ProfileType-koşul, deprecated, .diten-checkitem checklist görünümü.

## Kapsam (yalnız frontend, compose-row — SADECE select2 boyut + layout)
1. **Small select2:** `initComposeSelect2`'de hem `#composeAxis` hem `#composeValues` select2 init'ine **`selectionCssClass: 'form-select form-select-sm'`** ekle (satır 167 deseni). custom-axis Values (tags) de small. Sonuç: Axis/Values Tasks filtreleriyle aynı yükseklik/boyut.
2. **Layout taşma fix:** compose-row `.diten-checkitem-text` içinde Axis + Values + Add taşmadan sığsın — Values select2 container'ı `width:'100%'` ile kendi flex hücresine sığsın (`min-width:0` flex-grow hücresi), multi-chip'ler satırda sarmalı/kırpılmalı (Add'e binmesin); Add `flex-shrink-0` sağda kalsın. Gerekirse dar ekranda Values kendi satırına sarabilir (ama Axis+Add aynı ritimde).
3. **Eklenen satır chip'leri** (display-only) compose-row'a taşmasın — ayrı `<li>` içinde kalsın (zaten öyle olmalı; taşma compose select2 boyutundan geliyorsa 1+2 çözer).

## YAPMA
- Backend/API/RBAC/ocelot DOKUNMA. AUD-UI-3/4 davranış+yapısını (compose-then-add, ValueCode, Name " / ", cascade, ProfileType-koşul, .diten-checkitem sınıfları) DEĞİŞTİRME — yalnız select2 boyut + taşma. Yeni CSS icat etme (mevcut form-select-sm / .diten-checkitem). Native select'e geri dönme (select2 kalsın, small). Subject/Topic/başka konsol. 7-dil key-echo. Başka modül.

## Acceptance
- **E2:** build temiz; compose Axis+Values **small select2** (Tasks filtre boyutu); chip'ler Add'in üstüne binmiyor (taşma yok); Add sağda `flex-shrink-0`; .diten-checkitem checklist görünümü + AUD-UI-3 davranışı korunur (ekle/sil/Name " / "/ValueCode/cascade); `Diten.Web.Tests` + Platform nav guard baseline-diff sıfır-yeni-fail.
- **E4 (kullanıcı manuel):** Dimensions compose-row temiz — small select2 Axis/Values, Add binmiyor, checklist görünümü Tasks/Create ile aynı.
- Kapsam: yalnız Diten.Web (taxonomy.js compose select2 init + gerekirse Taxonomy.cshtml compose markup ince ayar). Backend DEĞİŞMEZ.

---

## §36.1 Agent Prompt (paste-ready)

```text
@[.antigravity/agents/frontend-ui-ux.md]
WP: WP-MOD0162-AUD-UI-5 · Prompt v1.0  (Compose-row small select2 + layout taşma fix — MOD-0162, frontend)

Repository: C:\Users\user\Desktop\ERP-vNext
Branch: feature/scmm-content-studio · Expected HEAD: 6cc8641f · Worktree: ana checkout

Önce oku:
1. execution/domains/commercial-suite/work-packs/WP-MOD0162-AUD-UI-5-small-select2-layout.md (bu WP)
2. frontend/Diten.Web/wwwroot/assets/js/CRM/Knowledge/taxonomy.js — initComposeSelect2 (~688, düzeltilecek: selectionCssClass eksik) + ÇALIŞAN small desen initFilterSelect2 (~160-178, selectionCssClass:'form-select form-select-sm') + renderCompose (~671) + Views/CRM/Knowledge/Taxonomy.cshtml (#taxDimensionCompose)

NE (yalnız frontend Diten.Web, compose-row — SADECE select2 boyut + layout):
 1) initComposeSelect2: #composeAxis ve #composeValues (+ custom tags) select2 init'ine selectionCssClass: 'form-select form-select-sm' EKLE (satır 167 deseni). Sonuç: small select2, Tasks filtreleriyle aynı boyut.
 2) Layout taşma: compose-row .diten-checkitem-text içinde Axis(sabit ~12rem) + Values(flex-grow, min-width:0, select2 width:'100%') + Add(flex-shrink-0) TAŞMADAN sığsın; multi-chip'ler satırda sar/kırp, Add'e BİNMESİN. Gerekirse dar ekranda Values kendi satırına sarabilir.
 3) Eklenen display satır chip'leri kendi <li>'sinde kalsın (compose'a taşmasın).
NASIL: initFilterSelect2'nin selectionCssClass small desenini compose'a uygula. AUD-UI-3/4 mantığı+yapısı (compose-then-add/ValueCode/Name ' / '/cascade/ProfileType-koşul/.diten-checkitem) DOKUNULMADAN yalnız select2 boyut + taşma düzelir.
YAPMA: backend/API/RBAC/ocelot; AUD-UI-3/4 davranış/yapı değiştir; native select'e dön (select2 small kalsın); yeni CSS icat; Subject/Topic/başka konsol; 7-dil key-echo; başka modül.
DOĞRULA (E2):
 - build temiz; compose Axis+Values small select2 (Tasks filtre boyutu); chip'ler Add'e binmiyor; Add sağda flex-shrink-0; checklist görünümü + AUD-UI-3 davranışı korunur.
 - Diten.Web.Tests + Platform nav guard: yeni fail YOK (baseline-diff).
Ayrı commit. §22 raporu TÜRKÇE. Senin PASS'in kapanış değildir (K13) — CT E2 + kullanıcı E4 doğrular.

Durma koşulları: small select2 taşmayı çözmüyorsa (layout kök nedenini raporla) · AUD-UI-3/4 davranışı bozuluyorsa · kapsam compose-row dışına taşarsa. DUR + raporla.
```

## Kalan (bu WP dışı)
- CT E2 + kullanıcı E4 → sonra **WP-MOD0162-SUBJECT-UI** → ALMIBA retest.
