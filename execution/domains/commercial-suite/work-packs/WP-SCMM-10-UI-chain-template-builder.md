# WORK PACKAGE — WP-SCMM-10-UI · ChainTemplate branched builder (Templates Slim tab)

> **Control Tower kaydı (SoR).** SCMM-10'un UI yansıması — ③ uçtan uca demolanabilir olsun. Bağlı: **SCMM-10 ✅ ACCEPTED** (backend Branches/cardinality/moderator/for-whom + spine + read-time migration, commit 2308982f+900c12da). Owner: frontend lead + domain.
> **Boundary:** MOD-0162 mevcut Templates Slim yüzeyi extend'i. CAND-CAP-0011 composition (④⑤⑥) DEĞİL.

## Metadata
```text
WP ID:            WP-SCMM-10-UI
Prompt ID:        P-SCMM-10-UI · v1.0
Task Class:       Frontend (branched builder — state-changing backend reflection) — Profile A
Risk Class:       MEDIUM (branched builder non-trivial; publish-freeze read-only + backward-compat UX)
Agent Lane:       AL-SCMM-CHAIN-UI (DEV) · Target Agent: frontend-ui-ux
Branch:           feature/structured-content-messaging · Expected HEAD: 66965f4e
Area/Module:      CRM / KnowledgeConcepts (Templates tab) · Shell: _LayoutTenantShell · golden_reference: HYBRID (Slim)
```

## Ölçülmüş yapı
- Templates Slim yüzeyi: `_TemplateCreateEditOffcanvas.cshtml` (bugün **flat ordered ConceptType sequence**: hidden `tplOrderedConceptTypes` + sequence editor, min 2, publish-freeze) · `_TemplatesDataTable.cshtml` · `_TemplateDetailsQuickView.cshtml` · `_TemplatesFilter.cshtml` · `concept-slim.js` Tab 4 (`openTemplateForm`/`refreshTemplateTypePicker`/submit) · proxy `KnowledgeConceptChainTemplates`.
- **Backend sözleşmesi (SCMM-10, WP-SCMM-10):** `OrderedConceptTypes` **spine** (zorunlu, min 2, conformance) + additive `Branches: [{ code, steps:[{ conceptTypeId, minSelection, maxSelection, allowedRoleRefs[], audienceDimensionRefs[] }] }]`. Update = **full replace** (branches yeniden gönderilir). Legacy (branches boş) = read-time tek-branch. Publish yeni yapıyı dondurur; değişiklik = yeni versiyon.

## Kapsam
1. **Branched builder** (offcanvas): mevcut flat sequence editor → branch'ler (ekle/sil), her branch sıralı **step** listesi; her step: ConceptType picker + **MinSelection/MaxSelection** + **Moderator** (allowed-roles Select2, ref) + **For-whom** (audience-dimension Select2, ref). **Spine korunur:** ya primary branch'ten türet ya mevcut flat editörü spine olarak tut — backend'e **hem OrderedConceptTypes hem Branches** gönder (sözleşme).
2. **Publish-freeze UX:** published template → yapı read-only + "yeni versiyon" akışı (mevcut freeze davranışını izle).
3. DataTable/DetailsQuickView: branch sayısı / yapı özeti göster (yeni kolon eklemeden mümkünse).
4. **L10n:** yeni key'ler 7 dilde gerçek metin (key-echo YASAK); `_IndexL10n.cshtml` bridge.

## Kısıtlar
- Hybrid Slim deseni birebir; `dt-inline-filter-host`; Select2 chip; teslim öncesi `verify_datatable_page.py --area CRM --module KnowledgeConcepts` (hybrid sapma gerekçeli, sıfır-yeni-fail hedefi).
- **D8:** builder yalnız yapı kurar; motor/ilerletme/atama YOK.
- Backward-compat: legacy flat template açıldığında tek-branch olarak görünür + kaydedilebilir (spine bozulmaz).

## Acceptance (E4 fleet açılınca)
- Runtime: branched template create → branches+steps (min/max/moderator/for-whom) persist+reload; legacy flat template tek-branch görünür ve bozulmadan kaydedilir; publish → read-only + yeni versiyon; canonical `concept-template.manage` 200/403; 7-dil gerçek L10n; console error/raw token yok. Cold ise E1 statik + fleet turu.

---

## §36.1 Agent Prompt (paste-ready)

```text
@[.antigravity/agents/frontend-ui-ux.md]
WP: WP-SCMM-10-UI · Prompt P-SCMM-10-UI v1.0  (ChainTemplate branched builder — Templates Slim tab)

Repository: C:\Users\user\Desktop\ERP-vNext
Branch: feature/structured-content-messaging · Expected HEAD: 66965f4e · Worktree: ana checkout

Area: CRM · Module: KnowledgeConcepts (Templates tab) · Shell: _LayoutTenantShell · golden_reference: HYBRID (Slim)

Önce oku:
1. execution/domains/commercial-suite/work-packs/WP-SCMM-10-UI-chain-template-builder.md (bu WP)
2. execution/domains/commercial-suite/work-packs/WP-SCMM-10-chain-template-extend.md (backend Branches/step sözleşmesi)
3. frontend/Diten.Web/Views/CRM/KnowledgeConcepts/_TemplateCreateEditOffcanvas.cshtml + _TemplatesDataTable.cshtml
   + _TemplateDetailsQuickView.cshtml + _IndexL10n.cshtml + wwwroot/assets/js/CRM/KnowledgeConcepts/concept-slim.js (Tab 4)

BOUNDARY: MOD-0162 Templates Slim extend. CAND-CAP-0011 composition (④⑤⑥) AÇMA.

NE:
 1) Branched builder: mevcut flat sequence editor → branch'ler (ekle/sil), her branch sıralı step listesi; her step:
    ConceptType picker + MinSelection/MaxSelection + Moderator (allowed-roles Select2, ref) + For-whom (audience-dim Select2, ref).
    SPINE KORU: backend'e HEM OrderedConceptTypes (min 2, conformance) HEM Branches gönder (SCMM-10 sözleşmesi; update=full-replace).
 2) Publish-freeze UX: published template → read-only + yeni-versiyon akışı (mevcut freeze davranışı).
 3) DataTable/DetailsQuickView: branch sayısı/yapı özeti (mümkünse yeni kolon açmadan — verifier sabit kalsın).
 4) L10n: yeni key'ler 7 dilde GERÇEK metin (ar/en/es/fr/ru/tr/zh; key-echo YASAK — NavL10n guard); _IndexL10n.cshtml bridge.
       Çeviri bilmiyorsan l10n-agent'a devret, placeholder GÖNDERME.
NASIL: hybrid Slim desenini BİREBİR izle; dt-inline-filter-host; Select2 chip; teslim öncesi
       python .antigravity/scripts/verify_datatable_page.py --area CRM --module KnowledgeConcepts (sıfır-yeni-fail hedefi).
YAPMA: motor/ilerletme/atama (D8) EKLEME; backend sözleşmesini DEĞİŞTİRME; başka tab/modül; backward-compat kır
       (legacy flat template tek-branch açılıp bozulmadan kaydedilmeli); placeholder L10n; hybrid deseni bozma.
DOĞRULA (E4 fleet açılınca; cold ise E1 statik + fleet turu):
       branched create persist+reload (min/max/moderator/for-whom); legacy flat → tek-branch bozulmaz; publish read-only+
       yeni versiyon; authz 200/403; 7-dil gerçek L10n; console error yok. verify script çıktısı (hybrid gerekçeli).
§22 raporu TÜRKÇE. Senin PASS'in kapanış değildir (K13).

Durma koşulları: backend Branches/step sözleşmesi belirsizse (WP-SCMM-10 oku) · L10n çevirisi yoksa (l10n-agent) · backward-compat riske girerse · kapsam Templates dışına taşarsa. DUR + raporla.
```

---

## §37 CT bağımsız doğrulama (2026-09-08) → **ACCEPTED (E1/E2)**
```text
Commit: 23034ac0 · Agent: PASS · Verification: PASS (CT scope+L10n+build teyit) · CT: ACCEPTED · Evidence: E1/E2 (E4 fleet-cold)
```
- ✅ Scope: 11 dosya, hepsi KnowledgeConcepts Templates + L10n — SCMM-dışı YOK.
- ✅ **L10n gerçek 7-dil** (tr "Dallar", zh "分支", fr "Ajouter une branche") — key-echo yok → NavL10n guard tamam.
- ✅ CT kendi koşumu: **Diten.Web build 0 Hata.** Verifier 78/9 = baseline birebir (sıfır-yeni-fail) + node --check temiz (agent).
- ✅ **Backward-compat:** legacy flat → tek-branch açılır/bozulmaz; published legacy read-only + yeni-versiyon akışı.
- Pragmatik karar (kabul): moderator/for-whom refs = virgüllü text input (dinamik per-step Select2 kırılganlığından kaçınma; opak config ref, D8-uyumlu).
- ⏳ **E4 (runtime branched persist/publish/version + authz) NOT MEASURED — fleet cold.**

**Sonuç:** ③ artık **backend+UI tam** — ①②③ Content Studio Model Builder çekirdeği demolanabilir (E4 fleet'e bağlı).
