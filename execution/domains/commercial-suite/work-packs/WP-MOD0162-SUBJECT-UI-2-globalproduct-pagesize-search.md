# WORK PACKAGE — WP-MOD0162-SUBJECT-UI-2 · Global Product picker pageSize=100 + ajax server-side search

> **Control Tower kaydı (SoR).** Kullanıcı E4: Subject formunda Global Product picker "unavailable". **CT kök-neden (kanıtlı):** Web proxy MDM selector'a `pageSize=200` istiyor ama **MDM max pageSize=100** → **400** ("'Page Size', 1 ve 100 arasında olmalı") → `!IsSuccessStatusCode` → `GlobalProductPickerUnavailable`. İzin/route/DB **hepsi doğru** (grant var+aktif, 200 alınıyor pageSize≤100 ile). Ayrıca MDM'de **177 ürün** var → statik `pageSize=100` sadece ilk 100'ü getirir (77 kaçar) → **ajax server-side search** gerek (MDM `search=` destekliyor, kanıtlı). Module: **MOD-0162**. Branch: `feature/scmm-content-studio` (HEAD `e3740ffb`). **Yalnız frontend** (Diten.Web); backend/MDM DOKUNMA.

## Ölçülmüş girdi (CT — kanıtlı)
- **Bug:** `KnowledgeController` satır **256** (Subject `GlobalProductOptions`) + **582** (`LoadGlobalProductOptionsAsync`, content formu) hardcoded `"/api/global-products/selector?pageSize=200"`. MDM 400 (max 100).
- **MDM selector:** `GetGlobalProductSelectorQuery` **`search`** param destekliyor (`?search=ALMIBA` → 1 sonuç, kanıtlı); `pageSize` max **100** (probe: 100→200 OK, 200→400). 177 ürün.
- **Mirror (client-driven ajax):** `KnowledgeConceptsController` satır **304** `/api/global-products/selector{Request.QueryString}` — client pageSize+search'ü geçiriyor + disabled/reason marker. Segments(241)/StrategyTemplates(237) de query-string geçiriyor (sorunsuz). Subject + content **tek** hardcoded-200 kullananlar.
- **Korunacak (e3740ffb):** externalReferences {sourceSystem:global-product, externalId, externalName} saklama, graceful-degradation reason'ları, custom toggle, EnsureGlobalProductSelected (edit id→ad).

## Kapsam (yalnız frontend Diten.Web)
1. **KnowledgeController Subject picker (256):** hardcoded `pageSize=200` yerine **client-driven** yap — `{Request.QueryString}` geçir (KnowledgeConcepts 304 deseni), default/cap **pageSize=100** (200'ü asla gönderme), `search` param'ını MDM'e ilet. disabled/reason marker korunur.
2. **Subject picker frontend (taxonomy.js):** Global Product select2'yi **ajax server-side search** yap — kullanıcı yazdıkça `?search=<term>&pageSize=100` → MDM; select2 sonuçları gösterir. 177 ürünün hepsi aranabilir (ilk-100 limiti kalkar). Seçilen → externalReferences (değişmez). Edit'te kayıtlı id → `EnsureGlobalProductSelected`/by-id resolve (mevcut) korunur.
3. **Content formu (582) — aynı 400 bug'ı:** `LoadGlobalProductOptionsAsync` `pageSize=200`→**`100`** (en azından 400'ü durdur; content picker'ı ajax'a çevirmek bu WP dışı — sadece pageSize düzelt, pre-existing bug).

## YAPMA
- Backend/MDM/API/RBAC/ocelot DOKUNMA (bug frontend'te, pageSize). MDM'e `pageSize>100` gönderme. externalReferences saklama/graceful-degradation/custom-toggle/AUD formunu bozma. KnowledgeConcepts/Segments/StrategyTemplates picker'larına dokunma (zaten client-driven, sorunsuz). 7-dil key-echo. Başka modül.

## Acceptance
- **E2:** build temiz; Subject Global Product picker **ajax search** (yaz→sonuç, `pageSize=100`, `search=` MDM'e); MDM 400 yok; disabled/reason korunur; edit'te kayıtlı ürün id→ad; content formu `pageSize=100` (400 yok); `Diten.Web.Tests` + Platform nav guard sıfır-yeni-fail.
- **E4 (kullanıcı manuel):** Subject formunda "From a Global Product" → ara "ALMIBA" → seçilir → Name gelir + ref saklanır (177'nin hepsi aranabilir).
- Kapsam: yalnız Diten.Web (KnowledgeController 256+582 + taxonomy.js Subject picker + gerekirse resx). Backend DEĞİŞMEZ.

---

## §36.1 Agent Prompt (paste-ready)

```text
@[.antigravity/agents/frontend-ui-ux.md]
WP: WP-MOD0162-SUBJECT-UI-2 · Prompt v1.0  (Global Product picker pageSize=100 + ajax server-side search — MOD-0162, frontend)

Repository: C:\Users\user\Desktop\ERP-vNext
Branch: feature/scmm-content-studio · Expected HEAD: e3740ffb · Worktree: ana checkout

CT kök-neden (kanıtlı): Web proxy MDM /api/global-products/selector'a pageSize=200 istiyor; MDM max=100 → 400 → picker "unavailable". İzin/route/DB doğru. MDM 177 ürün + `search=` destekliyor → ajax search gerek.

Önce oku:
1. execution/domains/commercial-suite/work-packs/WP-MOD0162-SUBJECT-UI-2-globalproduct-pagesize-search.md (bu WP)
2. frontend/Diten.Web/Controllers/CRM/KnowledgeController.cs (256 Subject GlobalProductOptions pageSize=200 · 582 LoadGlobalProductOptionsAsync pageSize=200)
3. MIRROR client-driven ajax: KnowledgeConceptsController.cs 304 (`/api/global-products/selector{Request.QueryString}` + disabled/reason) + concept node-form global-product select2 ajax (wwwroot/assets/js/CRM/KnowledgeConcepts/*)
4. frontend/Diten.Web/wwwroot/assets/js/CRM/Knowledge/taxonomy.js (Subject GP picker — loadSubjectGlobalProduct/select2, e3740ffb)

NE (yalnız frontend Diten.Web):
 1) KnowledgeController Subject picker (256): hardcoded pageSize=200 → client-driven {Request.QueryString} geçir (KnowledgeConcepts 304 deseni), default/cap pageSize=100 (200 GÖNDERME), search param'ı MDM'e ilet; disabled/reason korunur.
 2) taxonomy.js Subject GP select2 → ajax server-side search: kullanıcı yazınca ?search=<term>&pageSize=100 → MDM; 177 ürün aranabilir. Seçim→externalReferences (değişmez). Edit'te kayıtlı id→ad resolve (EnsureGlobalProductSelected/by-id) korunur.
 3) KnowledgeController content (582): LoadGlobalProductOptionsAsync pageSize=200→100 (400'ü durdur; ajax'a çevirme, sadece pageSize).
NASIL: KnowledgeConcepts 304 client-driven passthrough + node-form global-product ajax select2 desenini örnek al. e3740ffb externalReferences/graceful-degradation/custom-toggle KORUNUR. MDM'e pageSize>100 ASLA.
YAPMA: backend/MDM/API/RBAC/ocelot; pageSize>100 gönder; externalReferences/graceful-degradation/custom/AUD formu boz; KnowledgeConcepts/Segments/StrategyTemplates picker'a dokun; 7-dil key-echo; başka modül.
DOĞRULA (E2):
 - build temiz; Subject GP ajax search (yaz→sonuç, pageSize=100, search= MDM'e); MDM 400 yok; disabled/reason korunur; edit kayıtlı id→ad; content pageSize=100; AUD formu bozulmadı.
 - Diten.Web.Tests + Platform nav guard: yeni fail YOK (baseline-diff).
Ayrı commit. §22 raporu TÜRKÇE. Senin PASS'in kapanış değildir (K13) — CT E2 + kullanıcı E4 doğrular.

Durma koşulları: MDM selector search param adı farklıysa (search= kanıtlı) · ajax select2 externalReferences saklamayı bozuyorsa · kapsam frontend dışına taşarsa. DUR + raporla.
```

## Kalan (bu WP dışı)
- CT E2 + kullanıcı E4 → **Subject formu TAMAM** → ALMIBA retest (Subject=Global Product ALMIBA + AudienceProfile Nefrolog → A2..A8).
