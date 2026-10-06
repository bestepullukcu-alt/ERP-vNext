# WORK PACKAGE — WP-MOD0162-AUD-UI-4 · Dimensions görünümü = Tasks checklist BİREBİR (shared DitenCheckItem sınıfları)

> **Control Tower kaydı (SoR).** Kullanıcı manuel-test: Dimensions hâlâ Tasks/Create checklist gibi DEĞİL (3. deneme). Kök neden: ajan `.diten-checkitem`'i **elle taklit** etti — × için `btn-label-danger` (kocaman pembe) kullandı + compose-row'u ayrı ağır kart yaptı; paylaşılan **DitenCheckItem** bileşeninin kesin sınıflarını kullanmadı. Module: **MOD-0162** (AudienceProfile Dimensions). Branch: `feature/scmm-content-studio` (HEAD `e54305ad`). **Yalnız frontend** (Diten.Web); backend/API/RBAC DOKUNMA. **SIRA:** Subject WP'sinden önce.

## Ölçülmüş girdi (CT) — kanonik checklist yapısı
- **Paylaşılan bileşen:** `wwwroot/assets/js/shared/diten-checkitem.js` — `DitenCheckItem.row({...})` ve `DitenCheckItem.addRow({...})` factory'leri. Tasks create formu **bunu kullanıyor** (`Tasks/form-page.js` ~207 `DitenCheckItem.addRow({...})`).
- **CSS (`backbone-custom.css` ~6900-6975):** `.task-checklist` (ul: flex column, gap .375rem) · `.diten-checkitem` (flex row, gap .5rem, padding .375rem .5rem, 1px border, radius .375rem, **background var(--bs-card-bg)**) · `.diten-checkitem-grip` · `.diten-checkitem-move` · `.diten-checkitem-text` (flex 1) · `.diten-checkitem-btn` · **`.diten-checkitem-remove`** (× kontrolü) · `.diten-checkitem-withdrawn` (**visibility:hidden** — tek satırda grip/ok yerini koru).
- **Kesin remove sınıfı:** bileşende × = `class="diten-checkitem-btn diten-checkitem-remove"` + glyph `bx-x` (SESSİZ ikon). **`btn-label-danger` DEĞİL** (ajanın hatası).
- **Mevcut (08708706):** added row bordered ama × `btn-label-danger` (pembe blok); compose-row ayrı ağır kart + `+ Add axis` (purple) → checklist ritmine uymuyor.
- **Korunacak davranış (AUD-UI-3):** compose-then-add, ValueCode saklama, Name " / ", cascade, ProfileType-koşul, deprecated rozet, published-values.

## Kapsam (yalnız frontend, AudienceProfile Dimensions — SADECE GÖRÜNÜM)
1. **Added satırlar = kanonik `.diten-checkitem`:** her eklenmiş dimension `<li class="diten-checkitem">` — grip+move **yerini koruyan withdrawn** (`.diten-checkitem-withdrawn`, visibility:hidden — reorder yok ama yükseklik/hiza checklist ile aynı) + içerik (`.diten-checkitem-text` içinde: axis etiketi + değer-label chip'leri) + **× = `class="diten-checkitem-btn diten-checkitem-remove"` glyph `bx-x`** (sessiz; `btn-label-danger` KALDIR).
2. **Compose/add-row = kanonik add-row shell:** compose-row **`.diten-checkitem`** kabuğunda (aynı düz border/bg/padding — ayrı ağır kart DEĞİL): `.diten-checkitem-text` alanında Axis select2 + Values select2 (custom→ AxisCode input + tag) + sağda **Add** butonu (Tasks add-row Add konumu/stili; `+ Add axis` ağır purple yerine sade). Add → üste satır + compose temizle.
3. **`.task-checklist` ritmi:** tüm liste `<ul class="task-checklist">` içinde; boşluk/gap/hiza Tasks create ile birebir.
4. Mümkünse **`DitenCheckItem` bileşenini yeniden kullan** (row/addRow shell'ini alıp içerik slotuna Axis+Values koy); mümkün değilse yukarıdaki **kesin sınıfları** birebir kullan (elle "benzer" markup YOK).

## YAPMA
- Backend/API/RBAC/ocelot DOKUNMA. AUD-UI-3 davranışını (compose-then-add, ValueCode, Name " / ", cascade, ProfileType-koşul, deprecated) DEĞİŞTİRME — yalnız görünüm/sınıf. `btn-label-danger` × KULLANMA (diten-checkitem-remove). Compose-row'u ayrı ağır kart yapma (diten-checkitem shell). Drag-drop EKLEME (grip withdrawn). Yeni CSS icat etme (mevcut .diten-checkitem sınıflarını kullan). Subject/Topic/başka konsol. 7-dil key-echo. Başka modül.

## Acceptance
- **E2:** build temiz; Dimensions Tasks checklist ile **görsel birebir** — added satırlar `.diten-checkitem` (withdrawn grip yeri + text + `.diten-checkitem-remove` sessiz ×), compose-row `.diten-checkitem` add-row shell (Axis+Values+Add), `.task-checklist` ritmi; `btn-label-danger`/ayrı-kart YOK; AUD-UI-3 davranışı korunur (compose-then-add, ValueCode, Name " / ", cascade); `Diten.Web.Tests` + Platform nav guard baseline-diff sıfır-yeni-fail.
- **E4 (kullanıcı manuel):** görünüm Tasks/Create checklist ile aynı; ekle/sil/Name çalışır.
- Kapsam: yalnız Diten.Web (Taxonomy dimensions + taxonomy.js + gerekirse shared bileşen tüketimi). Backend DEĞİŞMEZ.

---

## §36.1 Agent Prompt (paste-ready)

```text
@[.antigravity/agents/frontend-ui-ux.md]
WP: WP-MOD0162-AUD-UI-4 · Prompt v1.0  (Dimensions = Tasks checklist BİREBİR, shared DitenCheckItem sınıfları — MOD-0162, frontend)

Repository: C:\Users\user\Desktop\ERP-vNext
Branch: feature/scmm-content-studio · Expected HEAD: e54305ad · Worktree: ana checkout

Önce oku:
1. execution/domains/commercial-suite/work-packs/WP-MOD0162-AUD-UI-4-checklist-exact-classes.md (bu WP)
2. KANONİK bileşen: frontend/Diten.Web/wwwroot/assets/js/shared/diten-checkitem.js (DitenCheckItem.row + .addRow; sınıflar diten-checkitem / -grip / -move / -text / -btn / -remove / -withdrawn) + kullanım Tasks/form-page.js ~203-208 (DitenCheckItem.addRow) + CSS backbone-custom.css ~6900-6975
3. Mevcut (düzeltilecek): frontend/Diten.Web/wwwroot/assets/js/CRM/Knowledge/taxonomy.js (renderAddedDimensions/compose-row render — × şu an btn-label-danger, compose ayrı kart) + Views/CRM/Knowledge/Taxonomy.cshtml (#taxDimensions ul + compose host)

NE (yalnız frontend Diten.Web, SADECE GÖRÜNÜM — davranış AUD-UI-3 korunur):
 1) Added satırlar kanonik .diten-checkitem: <li class="diten-checkitem"> + grip/move YERİ KORUNARAK withdrawn (.diten-checkitem-withdrawn, visibility:hidden) + içerik .diten-checkitem-text (axis etiketi + değer-label chip) + × = class="diten-checkitem-btn diten-checkitem-remove" glyph bx-x (SESSİZ). btn-label-danger KALDIR.
 2) Compose/add-row kanonik add-row shell: .diten-checkitem kabuğu (düz border/bg/padding, ayrı ağır kart DEĞİL) → .diten-checkitem-text içinde Axis select2 + Values select2 (custom→AxisCode input+tag) + sağda Add (Tasks add-row stili; purple '+ Add axis' yerine sade). Add→üste satır+compose temizle.
 3) Liste <ul class="task-checklist"> ritmi (gap/hiza Tasks ile birebir).
 4) MÜMKÜNSE DitenCheckItem.row/.addRow shell'ini yeniden kullan (içerik slotuna Axis+Values); değilse kesin sınıfları birebir kullan — elle "benzer" markup YOK.
NASIL: shared/diten-checkitem.js sınıf/yapısını + Tasks/form-page.js addRow kullanımını birebir örnek al. AUD-UI-3 mantığı (compose-then-add/ValueCode/Name ' / '/cascade/ProfileType-koşul/deprecated) DOKUNULMADAN yalnız DOM sınıfları/görünüm değişir.
YAPMA: backend/API/RBAC/ocelot; AUD-UI-3 davranışını değiştir; btn-label-danger ×; compose ayrı ağır kart; drag-drop ekle (grip withdrawn); yeni CSS icat (mevcut .diten-checkitem); Subject/Topic/başka konsol; 7-dil key-echo; başka modül.
DOĞRULA (E2):
 - build temiz; added satır .diten-checkitem (withdrawn grip yeri + text + diten-checkitem-remove sessiz ×); compose .diten-checkitem add-row (Axis+Values+Add); task-checklist ritmi; btn-label-danger/ayrı-kart yok; AUD-UI-3 davranışı korunur (ekle/sil/Name ' / '/ValueCode).
 - Diten.Web.Tests + Platform nav guard: yeni fail YOK (baseline-diff).
Ayrı commit. §22 raporu TÜRKÇE. Senin PASS'in kapanış değildir (K13) — CT E2 + kullanıcı E4 doğrular.

Durma koşulları: DitenCheckItem shell Axis+Values içeriğini alamıyorsa (kesin-sınıf fallback + belgele) · AUD-UI-3 davranışı görünüm değişince bozuluyorsa · kapsam dimensions dışına taşarsa. DUR + raporla.
```

## Kalan (bu WP dışı)
- CT E2 + kullanıcı E4 → sonra **WP-MOD0162-SUBJECT-UI** → ALMIBA retest.
