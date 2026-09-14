# WORK PACKAGE — WP-SCMM-14-UI · Content Studio workspace (ContentScope + ContentSet authoring, frontend)

> **Control Tower kaydı (SoR).** SCMM R1, Content Studio ④⑤⑥ frontend. Module: **CAND-CAP-0011**. Branch: `feature/scmm-content-studio` (HEAD 0f918e74). **Ön koşul HAZIR:** SCMM-14 backend+API (CT-ACCEPTED E2+E4-lite) — content-scopes + content-sets uçları gateway'den tüketilebilir. **Yalnız frontend** (Diten.Web); backend'e DOKUNMA.
> **D14-e:** UI hep **aranabilir dropdown/select2** — **ham Id girişi YOK**.

## Ölçülmüş girdi
- **Mirror console:** `Controllers/CRM/ClaimsController.cs` + `KnowledgeConceptsController.cs` (proxy/RequirePage/SendGatewayAsync/LoadOptionsAsync/ViewRoot) + `Views/CRM/Claims|KnowledgeConcepts/*` + 7-dil resx + JS + `CrmManifestProvider` nav.
- **Tüketilecek API (gateway, HAZIR):** ContentScopes `GET/POST/PUT + /archive` `api/crm/content-composition/content-scopes` · ContentSets `GET/POST/PUT + /archive + /clone + /components + /claims + /arrange + /apply-eligibility` `api/crm/content-composition/content-sets`. Perm: `crm.content-scope.read/manage` · `crm.content-set.read/manage` (SCMM-14'te seed+97c5 grant edildi → dev-fallback GEREKMEZ).
- **Picker kaynakları (hepsi GET, HAZIR):** template `api/crm/knowledge/concept-chain-templates` · scope `.../content-scopes` · component `api/crm/knowledge/contents` · claim `api/crm/content-composition/claims`.
- **DTO'lar:** ContentScopeDto · ContentSetDto (TemplateRef/ScopeRef pinned + SelectedComponents/Claims{SelectionId,version,Arrangement{TemplateStepId,BranchId?,Position}} + EligibilitySnapshot).

## Kapsam (yalnız frontend)
1. **CRM/ContentScopes console** (Claims aynası, basit): list + create/edit/archive (ScopeCode/Name + ProductRefs/MarketRefs/AudienceRefs multi-select2 + Channel/Language/Period). Perm crm.content-scope.*.
2. **CRM/ContentSets — Content Studio workspace** (asıl): 
   - List (SetCode/Name/Template/Status/updated + filtre).
   - Create/Edit: **Template picker** (single select2, `concept-chain-templates`) + **Scope picker** (single select2, `content-scopes`, opsiyonel) → seçilince arrangement iskeleti template adımlarından çıkar.
   - **Arrangement:** template adımlarına (step/branch) **component** (multi select2, `knowledge/contents`) + **claim** (multi select2, `claims`) ekle/çıkar/sırala (position). Ham Id yok — hep adla ara-seç.
   - Butonlar: **clone-to-draft** · **apply-eligibility** (sonucu = EligibilitySnapshot'ı per-claim Eligible/Blocked/Unresolved rozetleriyle göster) · archive.
   - Approved/frozen YOK (bu SCMM-14 draft; freeze SCMM-15).
3. **7-dil L10n** (`Resources/Views/CRM/ContentScopes|ContentSets/*.{en,tr,fr,es,zh,ar,ru}.resx`) — gerçek çeviri, key-echo YOK (NavL10n guard).
4. **Nav:** `CrmManifestProvider`'a `CONTENT_SCOPES` + `CONTENT_SETS` sayfaları + `Nav.Page.*` 7-dil (SCMM-12-UI CLAIMS deseni).

## YAPMA
- Backend/API/ocelot/RBAC DOKUNMA (hazır). Claims/KnowledgeConcepts console DEĞİŞTİRME (yalnız desen). Yeni API uydurma. **Ham Id girişi YOK** (hep select2). Freeze/render/release UI (SCMM-15/16/17). dev-fallback perm. 7-dilde key-echo. Yetkisiz→iskele çizme (UAS-001). Backend arrangement/pin mantığını UI'da tekrar etme (API yapıyor).

## Acceptance
- **E2:** build temiz; iki console render; proxy doğru uçlara; RequirePage crm.content-scope/set.*; **tüm seçimler select2 (ham Id yok)**; template seç→arrangement iskeleti; component/claim picker `knowledge/contents`+`claims`'ten; apply-eligibility snapshot rozetleri; 7-dil key-echo yok; Diten.Web.Tests + Platform nav guard yeşil (baseline-diff sıfır-yeni-fail).
- **E4 (kullanıcı manuel):** login→Content Studio→scope oluştur→set oluştur (template+scope seç)→component/claim ekle+arrange→apply-eligibility→clone. (Fleet restart + seed verisi gerekir — template/content/claim.) Alttaki API E4-proven.
- Kapsam: yalnız Diten.Web (+CrmManifestProvider nav). Backend DEĞİŞMEZ.

---

## §36.1 Agent Prompt (paste-ready)

```text
@[.antigravity/agents/frontend-ui-ux.md]
WP: WP-SCMM-14-UI · Prompt P-SCMM-14-UI v1.0  (Content Studio workspace — ContentScope + ContentSet authoring, frontend)

Repository: C:\Users\user\Desktop\ERP-vNext
Branch: feature/scmm-content-studio · Expected HEAD: 0f918e74 · Worktree: ana checkout

Önce oku (mirror):
1. execution/domains/commercial-suite/work-packs/WP-SCMM-14-UI-content-studio-workspace.md (bu WP) + DESIGN-SCMM-14 (D14-e: select2, no raw Id)
2. frontend/Diten.Web/Controllers/CRM/ClaimsController.cs + KnowledgeConceptsController.cs (proxy/RequirePage/SendGatewayAsync/LoadOptionsAsync/ViewRoot) + Views/CRM/Claims|KnowledgeConcepts/* + Resources/Views/CRM/Claims/*.resx (7-dil) + wwwroot/assets/js/CRM/Claims|KnowledgeConcepts/*
3. frontend/Diten.Web'de nav: Features/Crm/SelfRegistration/CrmManifestProvider.cs (CLAIMS sayfası deseni) + Resources/SharedResource.*.resx (Nav.Page.*)
4. Tüketilecek DTO/uç: services/Diten.CrmService/.../ContentComposition/ContentScopes/ContentScopeDtos.cs + ContentSets/ContentSetDtos.cs (alan/şekil)

NE (yalnız frontend, Diten.Web):
 1) CRM/ContentScopes console (Claims aynası): list + create/edit/archive → api/crm/content-composition/content-scopes.
    Form: ScopeCode/Name + ProductRefs/MarketRefs/AudienceRefs (multi select2) + Channel?/Language?/Period?. RequirePage crm.content-scope.read/manage.
 2) CRM/ContentSets Content Studio workspace: list + create/edit → api/crm/content-composition/content-sets. RequirePage crm.content-set.read/manage.
    - Template picker (single select2, api/crm/knowledge/concept-chain-templates) + Scope picker (single select2, content-scopes, ops.).
    - Arrangement: template adımlarına component (multi select2, api/crm/knowledge/contents) + claim (multi select2, api/crm/content-composition/claims) ekle/çıkar/sırala (position).
    - Butonlar: clone-to-draft (.../{id}/clone) · apply-eligibility (.../{id}/apply-eligibility → EligibilitySnapshot'ı per-claim Eligible/Blocked/Unresolved rozetiyle göster) · archive.
    - HAM Id YOK — hep adla ara-seç select2 (D14-e).
 3) 7-dil resx (ContentScopes + ContentSets) — GERÇEK çeviri, key-echo YOK.
 4) Nav: CrmManifestProvider'a CONTENT_SCOPES + CONTENT_SETS + Nav.Page.* 7-dil (CLAIMS deseni).
NASIL: Claims/KnowledgeConcepts console'unu birebir örnek al (proxy/RequirePage/select2/LoadOptionsAsync/7-dil/nav). crm.content-scope/set.* gerçek+granted → dev-fallback YOK.
YAPMA: backend/API/ocelot/RBAC DEĞİŞTİR; mirror console DEĞİŞTİR; yeni API; ham Id girişi (hep select2); freeze/render/release UI; dev-fallback; 7-dil key-echo; yetkisiz iskele (UAS-001); arrangement/pin mantığını UI'da tekrarlama (API yapar).
DOĞRULA (E2):
 - build temiz; iki console render; proxy doğru uçlar; RequirePage; select2 (ham Id yok); template seç→arrangement iskeleti; picker'lar knowledge/contents+claims'ten; apply-eligibility snapshot rozetleri; 7-dil key-echo yok.
 - Diten.Web.Tests + Platform nav/manifest guard: yeni fail YOK (baseline-diff).
Ayrı commit(ler). §22 raporu TÜRKÇE. Senin PASS'in kapanış değildir (K13) — CT/kullanıcı E4'ü fleet'te authenticated doğrular.

Durma koşulları: ContentSet API sözleşmesi beklenenden farklıysa · arrangement template adımlarına map edilemiyorsa · picker uçları beslenemezse · nav mekanizması uygulanamıyorsa · kapsam frontend dışına taşarsa. DUR + raporla.
```

## §37 CT bağımsız doğrulama (2026-09-14) → **ACCEPTED (E2)** · E4 = kullanıcı manuel
```text
Commits: dab4ad5d (2 konsol frontend) + 00f5848a (nav) · Agent: PASS · CT: ACCEPTED E2
```
- ✅ **Scope:** 49 dosya, yalnız Diten.Web + CrmManifestProvider(nav) + SharedResource. Backend/API/ocelot/RBAC + mirror console **dokunulmadı**.
- ✅ **CT kendi koşumu:** Diten.Web build 0 hata; **Diten.Web.Tests 137/0**; Platform nav/manifest guard **60/0** (NavManifestL10nGuard CONTENTSCOPES/CONTENTSETS 7-dil dahil).
- ✅ **Spot-check:** proxy uçları doğru (content-sets/scopes/claims + picker'lar knowledge/concept-chain-templates/contents/concept-types); perm crm.content-set/scope.* **dev-fallback YOK**; resx gerçek çeviri (Template=Şablon, ApplyEligibility=Uygunluğu değerlendir, 14 dosya); verifier Claims aynasıyla parity (0 yeni).
- ⏳ **E4 = kullanıcı manuel** (login→Content Studio→scope→set→component/claim ekle+arrange→apply-eligibility→clone) — fleet restart + seed verisi (template/content/claim) gerekir; alttaki API E4-lite proven.

## Kalan (bu WP dışı)
- CT/kullanıcı E4 (fleet authenticated — seed verisiyle tam akış) · SCMM-11-follow (eligibility HTTP/UI) · sonra bizim SCMM tarafı TAMAM → sync/push/PR.
