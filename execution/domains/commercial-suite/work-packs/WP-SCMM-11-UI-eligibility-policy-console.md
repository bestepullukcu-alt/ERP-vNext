# WORK PACKAGE — WP-SCMM-11-UI · Eligibility policy authoring konsolu + evaluate/preview (frontend)

> **Control Tower kaydı (SoR).** SCMM R1, eligibility (permission ekseni) frontend. Module: **CAND-CAP-0011**. Branch: `feature/scmm-content-studio` (HEAD `cb876c77`). **Ön koşul HAZIR:** SCMM-11-follow-API (CT-ACCEPTED E2, commit cb876c77) — policy CRUD + evaluate uçları gateway'den tüketilebilir, RBAC seed+97c5 grant edildi. **Yalnız frontend** (Diten.Web) + nav (Platform CrmManifestProvider); backend/API/ocelot/RBAC'a **DOKUNMA** (hazır). **Bizim SCMM tarafının SON parçası.**
> **Kural:** UI hep aranabilir dropdown/select2 — **ham entity-Id girişi YOK** (D14-e). (Condition/context *değerleri* referans-string'dir, Id değil — tag/multi giriş serbest.)

## Ölçülmüş girdi (CT, 2026-09-14)

### Tüketilecek API (gateway, HAZIR — SCMM-11-follow-API)
- **Policy CRUD:** `GET` + `POST` `api/crm/content-composition/eligibility-policies` · `GET /{id}` · `PUT /{id}` · `POST /{id}/archive`. Delete YOK (kapatma = archive).
- **Evaluate:** `POST api/crm/content-composition/eligibility:evaluate`.
- **Perm:** `crm.eligibility.read` (list/get) · `crm.eligibility.manage` (create/update/archive) · `crm.eligibility.evaluate` (evaluate). **Seed + 97c5 grant edildi → dev-fallback GEREKMEZ.** Gateway route `content-composition/{everything}` zaten var.

### DTO / şekiller (ölçüldü — render + form + preview için)
- **`EligibilityPolicyDto`:** `EligibilityPolicyId` · `PolicyCode` · `PolicyName` · `Description?` · `PolicyVersion` · `Status` · `Conditions[]` · `EffectiveFrom` · `EffectiveTo?` · audit (`CreatedAt/By`, `UpdatedAt/By`, `ArchivedAt/By`) · `IsArchived`. Liste = `EligibilityPolicyListDto{Items,Total}`.
- **`EligibilityConditionDto`:** `Dimension` (string) · `Match` (`includes`/`excludes` — `EligibilityMatchKinds.All`) · `Values[]` (string) · `Required` (bool).
- **Status vocab:** `draft/review/approved/published/inactive/archived` (`EligibilityPolicyStatuses.All`). Bu WP **create/edit/archive** yapar — publish/freeze YOK (o ayrı, R1 sonrası).
- **Evaluate request** (`EvaluateEligibilityRequest`): `{ PolicyId, Context:[{Dimension, Values[]}], PinnedSelections[]?, At? }`.
- **Evaluate result** (`EligibilityResult`): `{ State (Eligible/Blocked/Unresolved), BlockingLevel? (context/policy), Reason?, PolicyId, PolicyVersion, Conditions:[{Dimension, Match, State, Reason?}], EvaluatedAtUtc }`. **Disjoint, fail-closed** — non-Eligible asla sessiz değil (rozet + reason göster).
- **Context boyutları (docx §):** audience / product / market / channel / language / period (referans-string, sektör-nötr; **LE yok**).

### Mirror yüzeyler (birebir örnek al — DEĞİŞTİRME)
- **Controller:** `frontend/Diten.Web/Controllers/CRM/ContentSetsController.cs` + `ClaimsController.cs` (proxy / RequirePage / SendGatewayAsync / LoadOptionsAsync / ViewRoot).
- **View/JS:** `Views/CRM/ContentSets/*` (`Index/Create/Edit/_DataTable/_Filter/_IndexL10n/_WorkspaceL10n.cshtml` + `ContentSetsIndex.cs`) + `wwwroot/assets/js/CRM/ContentSets/*` (`index.js/create.js/workspace.js/*.l10n.js`) + `Resources/Views/CRM/ContentSets/*.{en,tr,fr,es,zh,ar,ru}.resx`.
- **Nav (Web dışı TEK dosya):** `services/Diten.Platform/src/Diten.Platform.Application/Features/Crm/SelfRegistration/CrmManifestProvider.cs` — `ModuleManifestPage(code,label,route,readPerm,…,order,[actions])` deseni (CONTENT_SETS order 160 son) + `Resources/SharedResource.*.resx` `Nav.Page.*` (NavManifestL10nGuardTests key varlığını zorlar).

## Kapsam (yalnız frontend + nav)
1. **CRM/EligibilityPolicies konsolu** (ContentScopes/Claims aynası):
   - **List** (PolicyCode / PolicyName / PolicyVersion / Status / EffectiveFrom / updated + filtre). Archived rozet.
   - **Create/Edit** → `eligibility-policies` POST/PUT. Form: `PolicyCode` · `PolicyName` · `Description?` · `PolicyVersion` · `EffectiveFrom` · `EffectiveTo?` + **Condition builder** (ekle/çıkar satır: **Dimension** select2 [audience/product/market/channel/language/period] · **Match** select2 [includes/excludes] · **Values** multi-tag giriş · **Required** checkbox). Perm `crm.eligibility.read/manage`.
   - **Archive** butonu (`/{id}/archive`). Delete YOK.
2. **Evaluate / Preview paneli** (aynı konsolda ayrı sekme/panel, perm `crm.eligibility.evaluate`):
   - **Policy picker** (single select2, `eligibility-policies`) + **Context builder** (Dimension select2 + Values multi-tag satırları) + **PinnedSelections?** (opsiyonel multi-tag) + **At?** (opsiyonel tarih).
   - **Evaluate** → `eligibility:evaluate` POST → sonucu göster: genel **State rozeti** (Eligible=yeşil / Blocked=kırmızı / Unresolved=amber) + `BlockingLevel` + `Reason` + **per-condition tablo** (Dimension / Match / State / Reason). Non-Eligible asla sessiz değil.
   - Read-only (kaydetmez).
3. **7-dil L10n** (`Resources/Views/CRM/EligibilityPolicies/*.{en,tr,fr,es,zh,ar,ru}.resx`) — gerçek çeviri, key-echo YOK (NavL10n guard).
4. **Nav:** CrmManifestProvider'a `ELIGIBILITY_POLICIES` sayfası (route `/CRM/EligibilityPolicies`, readPerm `crm.eligibility.read`, order 170; actions: MANAGE=`crm.eligibility.manage`, EVALUATE=`crm.eligibility.evaluate`) + `Nav.Page.ELIGIBILITYPOLICIES` 7-dil SharedResource.

## YAPMA
- Backend/API/ocelot/RBAC DOKUNMA (hazır). ContentSets/ContentScopes/Claims/KnowledgeConcepts konsolu DEĞİŞTİRME (yalnız desen). Yeni API uydurma. **Ham entity-Id girişi YOK** (policy picker=select2). Publish/freeze/review UI (R1 sonrası). dev-fallback perm (granted). 7-dilde key-echo. Yetkisiz→iskele çizme (UAS-001). Evaluate mantığını UI'da tekrarlama (resolver yapar — UI yalnız context yollar + sonucu gösterir). Silent fallback (non-Eligible mutlaka rozet+reason). Başka modül.

## Acceptance
- **E2:** build temiz; konsol + evaluate paneli render; proxy doğru uçlara (`eligibility-policies` + `eligibility:evaluate`); RequirePage `crm.eligibility.read/manage/evaluate`; **tüm seçimler select2** (Dimension/Match/policy picker — ham Id yok); condition builder ekle/çıkar; evaluate → State rozetleri (Eligible/Blocked/Unresolved) + per-condition tablo + reason; 7-dil key-echo yok; `Diten.Web.Tests` + Platform NavManifestL10nGuard yeşil (baseline-diff sıfır-yeni-fail).
- **E4 (CT/kullanıcı manuel, son toplu test):** login → Eligibility Policies → policy oluştur (condition'larla) → list → evaluate (context gir → disjoint sonuç rozetleri) → archive; yetkisiz→403. Fleet restart + seed (grant zaten var) gerekir. Alttaki API E2-proven, E4 son manuel pass'e katlanır.
- Kapsam: yalnız Diten.Web + CrmManifestProvider(nav) + SharedResource. Backend DEĞİŞMEZ.

---

## §36.1 Agent Prompt (paste-ready)

```text
@[.antigravity/agents/frontend-ui-ux.md]
WP: WP-SCMM-11-UI · Prompt P-SCMM-11-UI v1.0  (Eligibility policy authoring konsolu + evaluate/preview — CAND-CAP-0011, frontend)

Repository: C:\Users\user\Desktop\ERP-vNext
Branch: feature/scmm-content-studio · Expected HEAD: cb876c77 · Worktree: ana checkout

Önce oku (mirror):
1. execution/domains/commercial-suite/work-packs/WP-SCMM-11-UI-eligibility-policy-console.md (bu WP)
2. frontend/Diten.Web/Controllers/CRM/ContentSetsController.cs + ClaimsController.cs (proxy/RequirePage/SendGatewayAsync/LoadOptionsAsync/ViewRoot) + Views/CRM/ContentSets/* + wwwroot/assets/js/CRM/ContentSets/* + Resources/Views/CRM/ContentSets/*.resx (7-dil)
3. Nav (Web DIŞI tek dosya): services/Diten.Platform/src/Diten.Platform.Application/Features/Crm/SelfRegistration/CrmManifestProvider.cs (ModuleManifestPage CONTENT_SETS deseni + const *Read anahtarları) + services/Diten.Platform/.../Resources/SharedResource.*.resx (Nav.Page.*)
4. Tüketilecek DTO/uç şekli: services/Diten.CrmService/.../Features/ContentComposition/Eligibility/EligibilityPolicyDtos.cs + EligibilityModels.cs (EligibilityResult/ConditionOutcome) + Api/Models/CRM/EligibilityRequests.cs (Create/Update/Evaluate request şekli) + EligibilityPolicy.cs (EligibilityMatchKinds includes/excludes · EligibilityPolicyStatuses)

NE (yalnız frontend Diten.Web + nav):
 1) CRM/EligibilityPolicies konsolu (ContentSets/Claims aynası): list + create/edit/archive → api/crm/content-composition/eligibility-policies. RequirePage crm.eligibility.read/manage.
    Form: PolicyCode/PolicyName/Description?/PolicyVersion/EffectiveFrom/EffectiveTo? + Condition builder satırları: Dimension (select2: audience/product/market/channel/language/period) + Match (select2: includes/excludes) + Values (multi-tag) + Required (checkbox). Delete YOK (archive).
 2) Evaluate/Preview paneli (perm crm.eligibility.evaluate): Policy picker (single select2, eligibility-policies) + Context builder (Dimension select2 + Values multi-tag) + PinnedSelections? (multi-tag) + At? → POST eligibility:evaluate → sonucu göster: genel State rozeti (Eligible=yeşil/Blocked=kırmızı/Unresolved=amber) + BlockingLevel + Reason + per-condition tablo (Dimension/Match/State/Reason). Read-only. Non-Eligible ASLA sessiz (rozet+reason).
 3) 7-dil resx (EligibilityPolicies) — GERÇEK çeviri, key-echo YOK.
 4) Nav: CrmManifestProvider'a ELIGIBILITY_POLICIES sayfası (route /CRM/EligibilityPolicies, readPerm crm.eligibility.read, order 170; actions MANAGE=crm.eligibility.manage + EVALUATE=crm.eligibility.evaluate) + Nav.Page.ELIGIBILITYPOLICIES 7-dil SharedResource.
NASIL: ContentSets/Claims konsolunu birebir örnek al (proxy/RequirePage/select2/LoadOptionsAsync/7-dil/nav). crm.eligibility.* gerçek+granted → dev-fallback YOK. Condition/context DEĞERLERİ referans-string (Id değil) → multi-tag serbest; Dimension/Match/policy picker select2.
YAPMA: backend/API/ocelot/RBAC DEĞİŞTİR; mirror console (ContentSets/Scopes/Claims/KnowledgeConcepts) DEĞİŞTİR; yeni API; ham entity-Id girişi (policy picker=select2); publish/freeze/review UI; dev-fallback; 7-dil key-echo; yetkisiz iskele (UAS-001); evaluate mantığını UI'da tekrarla (resolver yapar); silent fallback (non-Eligible rozetsiz); başka modül.
DOĞRULA (E2):
 - build temiz; konsol + evaluate paneli render; proxy doğru uçlar (eligibility-policies + eligibility:evaluate); RequirePage crm.eligibility.read/manage/evaluate; select2 (ham Id yok); condition builder ekle/çıkar; evaluate→State rozetleri + per-condition tablo + reason; 7-dil key-echo yok.
 - Diten.Web.Tests + Platform NavManifestL10nGuard: yeni fail YOK (baseline-diff).
Ayrı commit(ler). §22 raporu TÜRKÇE. Senin PASS'in kapanış değildir (K13) — CT/kullanıcı E4'ü fleet'te authenticated doğrular.

Durma koşulları: eligibility DTO/uç sözleşmesi beklenenden farklıysa · EligibilityResult şekli rozet/tabloya map edilemiyorsa · nav ModuleManifestPage deseni uygulanamıyorsa · kapsam frontend+nav dışına taşarsa. DUR + raporla.
```

## §37 CT bağımsız doğrulama (2026-09-14) → **ACCEPTED (E2)** · E4 = son manuel test (fleet authenticated)
```text
Commits: 295eee1c (konsol+evaluate, 22 dosya) + 0e282c88 (nav, 8 dosya) · Agent: PASS · CT: ACCEPTED E2 · izole worktree @0e282c88
```
- ✅ **Scope (name-set):** 295eee1c = 22 dosya, **tamamı `frontend/Diten.Web`** (EligibilityPoliciesController + Models/CRM/EligibilityViewModels + Views/CRM/EligibilityPolicies/* + Resources 7-dil + wwwroot js). 0e282c88 = SharedResource 7-dil + `CrmManifestProvider.cs` (Platform, Web-dışı tek nav dosyası). Backend/API/ocelot/RBAC + mirror konsollar (ContentSets/Scopes/Claims/KnowledgeConcepts) **dokunulmadı**.
- ✅ **CT kendi koşumu (izole worktree, Release):** Diten.Web build **0/0**; Platform.Application build **0/0**; **Diten.Web.Tests 137/0**; Platform nav/manifest guard **112/0** (NavManifestL10nGuard ELIGIBILITYPOLICIES 7-dil dahil, key-echo yok).
- ✅ **Mantık (CT commit blob'undan okudu):** (1) controller `[Authorize]`, **delete verb YOK** (kapatma=archive HttpPost); RequirePage = list→Read, Create/Edit/archive→Manage, evaluate→**Evaluate** (ayrı SoD anahtarı); proxy uçları `eligibility-policies` + `eligibility:evaluate` doğru; (2) evaluate action context'i resolver'a **forward eder, UI'da karar tekrarlamaz**; (3) evaluate.js **disjoint rozet** (eligible→success/yeşil · blocked→danger/kırmızı · unresolved→warning/amber) + BlockingLevel + Reason + per-condition tablo → non-Eligible **asla sessiz değil**; (4) select2 form.js 8× / evaluate.js 9× → **ham entity-Id yok** (policy picker/Dimension/Match select2); (5) nav `ELIGIBILITY_POLICIES` route `/CRM/EligibilityPolicies` readPerm `crm.eligibility.read` order 170 + MANAGE/EVALUATE aksiyonları.
- ✅ **Verifier:** Claims aynasıyla birebir aynı 9 "uygulanamaz" FAIL → sıfır yeni (agent raporu ile tutarlı).
- ⏳ **E4 = son toplu manuel test** (fleet authenticated: login→policy oluştur[condition'larla]→list→evaluate[disjoint rozet]→archive · yetkisiz→403). RBAC seed+97c5 grant hazır; fleet restart gerekir. SCMM-11-follow E4 ile birlikte.

## Kalan (bu WP dışı)
- CT/kullanıcı **E4** (fleet authenticated; SCMM-11-follow E4 ile birlikte son toplu manuel testte).
- → **BİZİM SCMM TARAFI TAMAM** (SCMM 09/10 + 11-core/AUD/follow-API + 12/13/14 backend+API + 12-UI/14-UI/11-UI) → manuel test → main sync + push + PR.
- (en sonda) foundation/blocked program: SCMM-05 kalan · 06/07/08 · 15/16/17/… · R2/R3 (video=SCMM-27).
