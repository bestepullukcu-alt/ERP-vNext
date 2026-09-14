# WORK PACKAGE — WP-MOD0162-AUD-UI-3 · Dimensions compose-then-add (gerçek checklist UX) + Name " / " türetme düzeltmesi

> **Control Tower kaydı (SoR).** Kullanıcı manuel-test geri bildirimi (AUD-UI-2 üstüne 2 düzeltme). Module: **MOD-0162** (AudienceProfile formu). Branch: `feature/scmm-content-studio` (HEAD `c6abd2e4`). **Yalnız frontend** (Diten.Web); backend/API/RBAC'a **DOKUNMA**. **SIRA:** Subject WP'sinden ÖNCE (aynı dosyalar).

## Ölçülmüş girdi (CT) — neden bozuk
- **Mevcut (046d6115):** dimension satırları **inline-editable** (her satır axis select + values multiselect + × inline) + "eksen ekle" boş-satır butonu. Kullanıcı beklentisi: **Tasks/Create checklist compose-then-add** — sabit bir alt "oluştur" satırı, Add'e basınca öğe üstte satır olarak eklenir (checklist "Type an item… + Add" deseni).
- **Name bozuk:** `dimensionNameLabel` (taxonomy.js ~585-588) `parts.join(' · ')` — kullanıcı **" / "** istiyor ("Doctor / Nephrology"). Ayrıca `refValueLabel` (~571) async `refValues` cache'ine bağlı → label geç/çözülmeyince Name boş kalıyor. Label **add-anında** yakalanmalı.
- **Tasks compose/add mekaniği (mirror):** `Views/Tasks/_Form.cshtml` + `wwwroot/assets/js/Tasks/form.js` — add-row (input + Add) → Add öğeyi `<li>` olarak üste ekler + input temizlenir.
- **Korunacak (72ad7f24 + AUD-UI-2):** ValueCode saklama (adla-seç/kod-sakla), published-values proxy, cascade (contact-type doctor→medical-specialty), ProfileType-koşullu görünürlük, Name readonly/editable + dirty, deprecated rozet.

## Kapsam (yalnız frontend, AudienceProfile Dimensions)
1. **Compose-then-add (checklist UX):** Dimensions bölümü =
   - **Eklenen satırlar (üstte, display-only):** her eklenmiş dimension bir checklist satırı — axis etiketi + seçili **değer label'ları** (chip/metin) + **× sil**. Inline düzenleme YOK (değiştirmek için sil + yeniden ekle).
   - **Sabit compose-row (altta):** **Axis** select2 + **Values** multi-select2 (+ custom axis'te serbest AxisCode input & tag values) + **Add** butonu. Add'e basınca: axis + ≥1 değer doğrula → dimension'ı üste satır olarak ekle → compose-row temizlenir (sıradaki için hazır). (Tasks checklist "Type an item + Add" birebir.)
   - Drag-drop YOK; sıra anlamsız.
2. **Add-anında label yakala:** eklenen dimension `{axisCode, values:[code], valueLabels:[label]}` tutar — **kod SAKLANIR** (payload/backend `{axisCode, values:[code]}`), **label** display satırı + Name için add-anındaki compose-row select2 seçili-option text'inden alınır (async lookup boşluğu YOK). Custom axis: value=label=serbest metin.
3. **Name " / " türetme:** HCP/pharmacist'te Name = tüm eklenmiş dimension'ların **değer label'ları**, **" / " ile** birleştirilir → "Doctor / Nephrology". Add/sil sonrası canlı güncellenir; alan read-only (değer payload'a girer). (AUD-UI-2 Name readonly/editable + dirty davranışı KORUNUR — yalnız separator " / " + label-yakalama düzelir.)
4. **Cascade uyumu:** contact-type doctor Add'lenince medical-specialty için compose-row axis'i otomatik medical-specialty'ye set edilip odaklanır/önerilir (veya belgeli fallback) — mevcut cascade niyeti korunur, yeni compose akışına uyarlanır.

## YAPMA
- Backend/API/RBAC/ocelot DOKUNMA. ValueCode-saklama kararını bozma (kod sakla; label yalnız display/Name). Published-values proxy/cascade/ProfileType-koşul/deprecated rozet mantığını bozma. Subject/Topic/başka konsol. Drag-drop ekle. Eklenen satırı inline-editable yapma (compose-then-add). Name'i disabled iken payload'dan düşür (readonly). 7-dil key-echo. Başka modül.

## Acceptance
- **E2:** build temiz; Dimensions = üstte display satırları (axis + değer label chip + ×) + altta sabit compose-row (Axis+Values+Add); Add→üste satır + compose temizlenir; kod saklanır, label add-anında yakalanır; Name HCP/pharmacist'te değer-label'ları **" / "** ile ("Doctor / Nephrology") read-only, canlı; custom axis serbest; cascade korunur; `Diten.Web.Tests` + Platform nav guard baseline-diff sıfır-yeni-fail.
- **E4 (kullanıcı manuel):** healthcare-professional → contact-type Doctor seç+Add → satır görünür + Name "Doctor"; medical-specialty Nephrology seç+Add → Name "Doctor / Nephrology"; × ile sil → Name güncellenir; kaydet→payload values=ValueCode.
- Kapsam: yalnız Diten.Web (Taxonomy AudienceProfile dimensions + taxonomy.js + gerekirse resx). Backend DEĞİŞMEZ.

---

## §36.1 Agent Prompt (paste-ready)

```text
@[.antigravity/agents/frontend-ui-ux.md]
WP: WP-MOD0162-AUD-UI-3 · Prompt v1.0  (Dimensions compose-then-add checklist UX + Name " / " fix — MOD-0162, frontend)

Repository: C:\Users\user\Desktop\ERP-vNext
Branch: feature/scmm-content-studio · Expected HEAD: c6abd2e4 · Worktree: ana checkout

Önce oku:
1. execution/domains/commercial-suite/work-packs/WP-MOD0162-AUD-UI-3-checklist-compose-name.md (bu WP)
2. frontend/Diten.Web/wwwroot/assets/js/CRM/Knowledge/taxonomy.js (dimension builder 046d6115: renderDimensions ~618-660, dimensionNameLabel ~585-588 ' · '→' / ', refValueLabel ~571, loadRefValues ~554, updateProfileName, collectDimensions, cascade) + Views/CRM/Knowledge/Taxonomy.cshtml (#taxDimensionsSection)
3. Tasks compose/add MIRROR: frontend/Diten.Web/Views/Tasks/_Form.cshtml + wwwroot/assets/js/Tasks/form.js (add-row input+Add → üste <li> ekle + temizle)

NE (yalnız frontend Diten.Web, AudienceProfile Dimensions):
 1) Compose-then-add: üstte eklenmiş dimension'lar display-only satır (axis etiketi + değer label chip + × sil, inline düzenleme YOK); altta SABİT compose-row = Axis select2 + Values multi-select2 (+ custom: serbest AxisCode + tag) + Add. Add→ axis+≥1 değer doğrula→ üste satır ekle→ compose-row temizle (Tasks checklist deseni). Drag-drop YOK.
 2) Add-anında label yakala: eklenen {axisCode, values:[code], valueLabels:[label]} — KOD saklanır (payload {axisCode, values:[code]}), label add-anındaki compose select2 seçili-option text'inden (async lookup boşluğu yok). custom: value=label=serbest.
 3) Name " / ": HCP/pharmacist → Name = tüm eklenmiş değer label'ları " / " ile ("Doctor / Nephrology"); dimensionNameLabel separator ' · '→' / ' + label add-anından; add/sil sonrası canlı; readonly (payload'a girer). AUD-UI-2 readonly/editable+dirty davranışı KORUNUR.
 4) Cascade: contact-type doctor Add'lenince compose-row axis otomatik medical-specialty'ye set/öner (belgeli fallback). Mevcut cascade niyeti korunur.
NASIL: Tasks/form.js add-row (input+Add→li) mekaniğini örnek al, input yerine Axis+Values compose. ValueCode saklama + published-values + ProfileType-koşul + deprecated (046d6115) KORUNUR — yalnız compose UX + Name separator/label-yakalama.
YAPMA: backend/API/RBAC/ocelot DEĞİŞTİR; ValueCode-saklama boz (kod sakla, label display); published-values/cascade/ProfileType-koşul/deprecated boz; eklenen satırı inline-editable yap; drag-drop; Name disabled iken payload'dan düşür; Subject/Topic/başka konsol; 7-dil key-echo; başka modül.
DOĞRULA (E2):
 - build temiz; üstte display satır + altta compose-row (Axis+Values+Add); Add→üste satır+compose temizlenir; kod saklanır/label yakalanır; Name " / " ile canlı ("Doctor / Nephrology"); custom serbest; cascade korunur; kaydet payload values=ValueCode; 7-dil key-echo yok.
 - Diten.Web.Tests + Platform nav guard: yeni fail YOK (baseline-diff).
Ayrı commit. §22 raporu TÜRKÇE. Senin PASS'in kapanış değildir (K13) — CT E2 + kullanıcı E4 doğrular.

Durma koşulları: compose-row select2 seçili-option text'i alınamıyorsa · Name label yakalanamıyorsa · cascade yeni akışa uyarlanamıyorsa · kapsam AudienceProfile dimensions dışına taşarsa. DUR + raporla.
```

## Kalan (bu WP dışı)
- CT E2 + kullanıcı E4 → sonra **WP-MOD0162-SUBJECT-UI** → ALMIBA retest.
