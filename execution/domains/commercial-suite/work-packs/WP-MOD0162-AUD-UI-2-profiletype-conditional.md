# WORK PACKAGE — WP-MOD0162-AUD-UI-2 · AudienceProfile formu: ProfileType-koşullu Dimensions + Name türetme + Tasks-checklist görünüm

> **Control Tower kaydı (SoR).** Kullanıcı manuel-test 2. tur, AudienceProfile formu follow-up (72ad7f24 üstüne). Module: **MOD-0162**. Branch: `feature/scmm-content-studio` (HEAD `86f427dc`). **Yalnız frontend** (Diten.Web); backend/API/RBAC'a **DOKUNMA** (hazır). **SIRA:** Subject WP'sinden ÖNCE (kullanıcı isteği) — aynı `Taxonomy.cshtml`/`taxonomy.js`, sıralı.

## Ölçülmüş girdi (CT)
- **Mevcut (72ad7f24):** AudienceProfile formu (`Taxonomy.cshtml` `tax-only-profile` + `taxonomy.js`) — ProfileType select2 + dimension builder (axis/values, ValueCode saklar) zaten var. Ama: ProfileType sırası, koşullu görünürlük, Name türetme YOK; dimension builder görünümü serbest (Tasks-checklist değil).
- **ProfileType vocab (`AudienceProfileTypes.All`):** `healthcare-professional` · `pharmacist` · `patient` · `learner` · `employee` · `sales-representative` · `manager` · `administrator` · `other`.
- **Tasks checklist deseni (mirror):** `Views/Tasks/_Form.cshtml` (~303-336: `card` + `<ul class="task-checklist" id="taskChecklistItems">` + `#taskChecklistAddRow`) + `wwwroot/assets/js/Tasks/form.js` (satır render: grip/up-down/optional/x + add-row). **Drag-drop var — biz ALMAYACAĞIZ.**
- **Name alanı:** `taxName` (input). taxonomy.js submit `el.value` okur (readonly değeri de okunur → payload'a girer).

## Kapsam (yalnız frontend, AudienceProfile formu)
1. **ProfileType ilk sırada:** `tax-only-profile` bloğunda ProfileType alanı **en üste** taşınır (formu sürükleyen alan).
2. **Koşullu Dimensions:** Dimensions bölümü **yalnız ProfileType ∈ {healthcare-professional, pharmacist}** iken görünür/kullanılabilir; diğer tüm ProfileType'larda **gizli**. ProfileType dimension-dışı bir değere çevrilince: Dimensions **gizlenir + temizlenir** (payload'a boş gider, orphan dimension kalmaz).
3. **Name türetme + disable:**
   - **ProfileType ∈ {healthcare-professional, pharmacist}:** `Name` seçili **Dimensions değerlerinden otomatik** türetilir (çözülen display label'ları okunur birleşim, ör. "Doctor · Nephrology"); dimensions değişince canlı güncellenir; alan **read-only/disabled** (ama değeri payload'a girer — taxonomy.js `.value` okur, readonly tercih et).
   - **ProfileType ∉ o ikisi:** `Name` **ProfileType display adından** otomatik prefill (ör. "Patient"); alan **düzenlenebilir** (kullanıcı değiştirebilir; elle değiştirdiyse ProfileType tekrar değişene kadar ezme — dirty-flag).
   - Boş/seçimsiz durumda: HCP/pharmacist + hiç dimension yok → Name boş/placeholder (dimension seçilince dolar).
4. **Dimensions görünümü = Tasks/Create checklist:** dimension satırları Tasks `.task-checklist` kart/satır desenine benzer (bordered satır kartları + "eksen ekle" add-row + satır sil ×), **DRAG-DROP YOK** (grip/sürükleme kaldırılır; sıralama gerekmez — dimensions bir küme). Axis select2 + values multi-select2 içerik aynı kalır, yalnız kapsayıcı/row stili Tasks-checklist'e yaklaşır.

## YAPMA
- Backend/API/RBAC/ocelot DOKUNMA. ValueCode saklama kararını bozma (adla-seç, kod-sakla — 72ad7f24). Subject/Topic bölümünü veya başka konsolu değiştirme (yalnız AudienceProfile formu). Dimensions'a **drag-drop** ekleme. Name'i disabled yaparken payload'dan düşürme (readonly + değeri topla). ProfileType dimension-dışıyken dimension'ı payload'a gönderme. 7-dil key-echo. Başka modül.

## Acceptance
- **E2:** build temiz; ProfileType formda ilk; Dimensions yalnız healthcare-professional/pharmacist'te görünür (diğerlerinde gizli+temiz); Name → HCP/pharmacist'te dimensions'tan türeyip read-only (payload'a girer), diğerlerinde ProfileType'tan prefill + düzenlenebilir; dimension satırları Tasks-checklist görünümü, drag-drop yok; kaydet→doğru ProfileType/Name/dimensions payload; edit round-trip; 7-dil key-echo yok; `Diten.Web.Tests` + Platform nav guard baseline-diff sıfır-yeni-fail.
- **E4 (kullanıcı manuel):** ProfileType=healthcare-professional seç → Dimensions açılır; contact-type:doctor+medical-specialty:nephrology seç → Name otomatik+read-only; ProfileType=patient seç → Dimensions gizlenir, Name="Patient" düzenlenebilir.
- Kapsam: yalnız Diten.Web (Taxonomy AudienceProfile bölümü + taxonomy.js + gerekirse resx). Backend DEĞİŞMEZ.

---

## §36.1 Agent Prompt (paste-ready)

```text
@[.antigravity/agents/frontend-ui-ux.md]
WP: WP-MOD0162-AUD-UI-2 · Prompt v1.0  (AudienceProfile ProfileType-koşullu Dimensions + Name türetme + Tasks-checklist görünüm — MOD-0162, frontend)

Repository: C:\Users\user\Desktop\ERP-vNext
Branch: feature/scmm-content-studio · Expected HEAD: 86f427dc · Worktree: ana checkout

Önce oku:
1. execution/domains/commercial-suite/work-packs/WP-MOD0162-AUD-UI-2-profiletype-conditional.md (bu WP)
2. frontend/Diten.Web/Views/CRM/Knowledge/Taxonomy.cshtml (tax-only-profile bloğu — ProfileType/dimension builder, 72ad7f24) + wwwroot/assets/js/CRM/Knowledge/taxonomy.js (dimension builder, openForm/collectDimensions/writePayload, ProfileType alanı)
3. Tasks checklist MIRROR (görünüm): frontend/Diten.Web/Views/Tasks/_Form.cshtml (~303-336 task-checklist card/ul + add-row) + wwwroot/assets/js/Tasks/form.js (satır render — grip/optional/x/add-row; DRAG-DROP ALMA)
4. ProfileType vocab: services/Diten.CrmService/.../Domain/Entities/AudienceProfile.cs (AudienceProfileTypes.All — healthcare-professional/pharmacist/patient/…)

NE (yalnız frontend Diten.Web, AudienceProfile formu):
 1) ProfileType alanını tax-only-profile'da EN ÜSTE al (formu sürükleyen alan).
 2) Koşullu Dimensions: yalnız ProfileType ∈ {healthcare-professional, pharmacist} iken görünür; diğerlerinde gizli. Dimension-dışı değere geçince Dimensions gizle + TEMİZLE (payload boş).
 3) Name türetme:
    - ProfileType ∈ {healthcare-professional, pharmacist}: Name = seçili dimension değerlerinin display-label'larından otomatik (ör. "Doctor · Nephrology"), dimensions değişince canlı; alan READ-ONLY (değer payload'a GİRSİN — taxonomy.js .value okur, readonly tercih et).
    - ProfileType ∉ o ikisi: Name = ProfileType display adından prefill (ör. "Patient"); DÜZENLENEBİLİR (dirty-flag: kullanıcı değiştirdiyse ProfileType değişene kadar ezme).
 4) Dimensions görünümü Tasks/Create checklist'e benze (bordered satır kartları + eksen-ekle add-row + satır sil ×); DRAG-DROP YOK; sıralama yok (küme). Axis select2 + values multi-select2 içeriği aynı.
NASIL: Tasks/_Form.cshtml + Tasks/form.js checklist satır/add-row stilini örnek al (grip/drag ALMA). Mevcut dimension builder mantığı (ValueCode saklama, cascade, published-values) KORUNUR — sadece görünüm + ProfileType-koşul + Name.
YAPMA: backend/API/RBAC/ocelot DEĞİŞTİR; ValueCode saklama kararını boz; Subject/Topic/başka konsol; drag-drop ekle; Name disabled iken payload'dan düşür; dimension-dışı ProfileType'ta dimension gönder; 7-dil key-echo; başka modül.
DOĞRULA (E2):
 - build temiz; ProfileType ilk; Dimensions yalnız HCP/pharmacist'te (diğerinde gizli+temiz); Name HCP/pharmacist'te dimensions-türevli read-only (payload'a girer) + diğerinde ProfileType-türevli düzenlenebilir; dimension satırları Tasks-checklist görünüm, drag-drop yok; kaydet+edit round-trip doğru; 7-dil key-echo yok.
 - Diten.Web.Tests + Platform nav guard: yeni fail YOK (baseline-diff).
Ayrı commit(ler). §22 raporu TÜRKÇE. Senin PASS'in kapanış değildir (K13) — CT E2 + kullanıcı E4 doğrular.

Durma koşulları: ProfileType alanı/vocab beklenenden farklıysa · Name'i readonly iken payload'a katamıyorsan · dimension builder (72ad7f24) mantığı bozulmadan görünüm değiştirilemiyorsa · kapsam AudienceProfile formu dışına taşarsa. DUR + raporla.
```

## Kalan (bu WP dışı)
- CT E2 + kullanıcı E4 → sonra **WP-MOD0162-SUBJECT-UI** (Subject↔Global Product, sıralı) → sonra ALMIBA retest.
