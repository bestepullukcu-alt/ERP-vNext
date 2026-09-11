# WORK PACKAGE — WP-SCMM-09-UI · KnowledgeConcepts konsolu ①② extend (frontend)

> **Control Tower kaydı (SoR).** SCMM-09'un UI yansıması — ①② uçtan uca demolanabilir olsun. Bağlı: **SCMM-09 ✅ ACCEPTED** (backend alanlar + combined-write endpoint + audit hazır, commit bed72c19). Owner: frontend lead + domain.
> **Authority:** MOD-0162-FU03 pack + mevcut hybrid konsol deseni (`Views/CRM/KnowledgeConcepts/`).

## Metadata
```text
WP ID:            WP-SCMM-09-UI
Prompt ID:        P-SCMM-09-UI · v1.0
Task Class:       Frontend (UI reflection of state-changing backend) — Profile A
Risk Class:       LOW-MEDIUM (mevcut konsol extend; yeni surface yok)
Agent Lane:       AL-SCMM-CONCEPT-UI (DEV) · Target Agent: frontend-ui-ux
Branch:           feature/structured-content-messaging · Expected HEAD: bed72c19
Area/Module:      CRM / KnowledgeConcepts · Shell: _LayoutTenantShell
golden_reference: HYBRID (ConceptType/Connections = Slim offcanvas; ConceptNode = Compact) — mevcut deseni birebir izle
```

## Ölçülmüş yapı (git ls-files)
- Konsol: `Views/CRM/KnowledgeConcepts/Index.cshtml` (hybrid; 5 tab: Types/Nodes/Connections/Templates/Graph).
- ① ConceptType Slim surface: `_TypeCreateEditOffcanvas.cshtml` · `_TypesDataTable.cshtml` · `_TypeDetailsQuickView.cshtml` · `_TypesFilter.cshtml` · `Models/CRM/KnowledgeConceptViewModels.cs` · proxy `Controllers/CRM/KnowledgeConceptsController.cs`.
- ② Connections Slim surface: `_RelationshipCreateEditOffcanvas.cshtml` · `_RelationshipsDataTable.cshtml`.
- L10n: `Resources/Views/CRM/KnowledgeConcepts/KnowledgeConceptsIndex.{ar,en,es,fr,ru,tr,zh}.resx` (7 dil) + `_IndexL10n.cshtml` bridge.

## Kapsam
**① ConceptType offcanvas/datatable/detay:**
- Offcanvas form: **Color** (color-picker/hex input), **IsGroup** + **IsList** (switch), **Parent ConceptType** (Select2, aynı Subject içi; **cycle-safe** — self + descendant seçenek dışı, backend zaten 400 reddeder ama UI yönlendirir).
- DataTable: color swatch + group/list rozet (kompakt), parent adı kolonu (opsiyonel).
- DetailsQuickView + ViewModel + proxy controller: yeni alanları taşı.

**② Connections combined-write:**
- `_RelationshipCreateEditOffcanvas`'a **"yeni node + bağla" modu** (legacy "New UCLN List" ergonomisi) → `POST /concept-nodes/with-relationship` (SCMM-09 endpoint). `NewNodeIsSource` yön seçimi. Mevcut "iki var-olan node'u bağla" modu KALIR.

**L10n:** yeni key'ler 7 dilde **gerçek metin** (key-echo YASAK, NavL10n guard); `_IndexL10n.cshtml` PascalCase bridge'e ekle. Çeviri bilinmiyorsa **l10n-agent**'a devret (placeholder gönderme).

## Kısıtlar (mevcut desen + repo tuzakları)
- Hybrid konsol: verifier her iki ref'i birden geçemez (bilinen: compact 87/8, slim 80/10) — **mevcut hybrid deseni izle**, %100 verifier kovalama; teslim öncesi `verify_datatable_page.py --area CRM --module KnowledgeConcepts` çalıştır, sapma hybrid gerekçesiyle raporlanır.
- 2. Slim filter host `class="dt-inline-filter-host"` taşımalı (chip CSS scoped).
- L10n bridge PascalCase loader (index.l10n.js camelCase→PascalCase) — key'ler window.L10n'da görünmeli.
- D8: motor/otomatik hesaplama YOK; UI yalnız CRUD + combined-write.

## Acceptance (E4 — fleet açılınca)
- Runtime: ConceptType create/edit → color/isGroup/isList/parent persist + reload; parent seçici cycle-safe; Connections combined-write → yeni node+edge tek işlem, 201; canonical authz (200/403); 7-dil L10n gerçek metin (key-echo yok). Console errors yok, raw token yok. Fleet cold ise E1 (view/VM/proxy statik) + E4 fleet turu.

## §37 CT bağımsız doğrulama (2026-09-07) → **ACCEPTED (E1/E2)**
```text
Commit: 51181f60 · Agent: PASS · Verification: PASS (CT scope+L10n+build teyit) · CT: ACCEPTED · Evidence: E1/E2 (E4 fleet-cold)
```
- ✅ Scope: 13 dosya hepsi KnowledgeConcepts konsolu (7 resx + bridge + 2 offcanvas + detay + concept-slim.js + proxy) — SCMM-dışı YOK.
- ✅ **L10n gerçek 7-dil çeviri** (tr "Grup Tipi", fr "Type de groupe", zh "分组类型") — key-echo/English-kopya YOK → NavL10n guard tamam.
- ✅ CT kendi koşumu: **Diten.Web build 0 Hata.**
- ✅ Verifier 78/9 = mevcut baseline ile birebir (stash-karşılaştırma) → **sıfır yeni fail**; 9 = archive-only hybrid sapması (bilinen).
- ⏳ **E4 (runtime persist/authz/combined-write) NOT MEASURED — fleet cold**; fleet açılınca §26 operator turu.

**Sonuç:** ①② artık **backend + UI tam** (SCMM-09 + SCMM-09-UI). İlk demolanabilir Content Studio slice (E4 fleet'e bağlı).

---

## §36.1 Agent Prompt (paste-ready)

```text
@[.antigravity/agents/frontend-ui-ux.md]
WP: WP-SCMM-09-UI · Prompt P-SCMM-09-UI v1.0  (KnowledgeConcepts konsolu ①② extend)

Repository: C:\Users\user\Desktop\ERP-vNext
Branch: feature/structured-content-messaging · Expected HEAD: bed72c19 · Worktree: ana checkout

Area: CRM · Module: KnowledgeConcepts · Shell: _LayoutTenantShell · golden_reference: HYBRID (Type/Connections=Slim, Node=Compact)

Önce oku:
1. execution/domains/commercial-suite/work-packs/WP-SCMM-09-UI-concept-console-extend.md (bu WP)
2. execution/domains/commercial-suite/work-packs/WP-SCMM-09-concept-catalog-extend.md (backend alan/endpoint sözleşmesi)
3. frontend/Diten.Web/Views/CRM/KnowledgeConcepts/Index.cshtml + _TypeCreateEditOffcanvas.cshtml + _TypesDataTable.cshtml
   + _TypeDetailsQuickView.cshtml + _RelationshipCreateEditOffcanvas.cshtml + _IndexL10n.cshtml
   + Models/CRM/KnowledgeConceptViewModels.cs + Controllers/CRM/KnowledgeConceptsController.cs

NE:
 ① ConceptType Slim surface'ine backend alanlarını yansıt: Color (hex/color-picker), IsGroup + IsList (switch),
    Parent ConceptType (Select2, aynı Subject; CYCLE-SAFE: self+descendant seçenek dışı). Offcanvas form + DataTable
    (color swatch + group/list rozet) + DetailsQuickView + ViewModel + proxy controller.
 ② Connections offcanvas'a "yeni node + bağla" modu (legacy "New UCLN List"): POST /concept-nodes/with-relationship
    (SCMM-09 endpoint), NewNodeIsSource yön seçimi. Mevcut "iki var-olan node'u bağla" modu KALIR.
 L10n: yeni key'ler 7 dilde GERÇEK metin (ar/en/es/fr/ru/tr/zh; key-echo YASAK — NavL10n guard); _IndexL10n.cshtml
    PascalCase bridge'e ekle. Çeviri bilmiyorsan l10n-agent'a devret, placeholder GÖNDERME.
NASIL: mevcut hybrid konsol desenini BİREBİR izle (Slim offcanvas pattern); 2. Slim filter host class="dt-inline-filter-host";
       Select2 chip enum alanları; teslim öncesi `python .antigravity/scripts/verify_datatable_page.py --area CRM --module KnowledgeConcepts`.
YAPMA: yeni surface/tab AÇMA; motor/otomatik hesaplama (D8) EKLEME; backend sözleşmesini DEĞİŞTİRME; başka modül view'ı;
       placeholder/İngilizce-kopya L10n; hybrid verifier %100 için deseni bozma.
DOĞRULA (E4 fleet açılınca; cold ise E1 statik + fleet turu):
       ConceptType color/isGroup/isList/parent persist+reload; parent cycle-safe; combined-write 201; authz 200/403;
       7-dil gerçek L10n; console error yok, raw token yok. verify script çıktısı (hybrid sapma gerekçeli).
§22 raporu TÜRKÇE. Senin PASS'in kapanış değildir (K13).

Durma koşulları: backend alan/endpoint belirsizse (WP-SCMM-09 oku) · L10n çevirisi yoksa (l10n-agent) · kapsam ①② dışına taşarsa. DUR + raporla.
```
