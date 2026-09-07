# WORK PACKAGE — WP-SCMM-09 · Concept catalog & relationship extend (①②) + audit-wiring

> **Control Tower kaydı (SoR).** SCMM R1 model band (gate G2). **İlk gerçek SCMM domain build'i.** Bağlı: SCMM-03 ✅ (model freeze) · SCMM-04 ✅ (contracts) · SCMM-05/S1 ✅ (canonical authz). Owner: CRM/domain lead. Module: **MOD-0162** (concept foundation; SCMM-03/H: foundation retained here).
> **Authority:** MOD-0162-FU03 pack (concept-graph-runtime-ui) + SCMM-content-studio-work-plan §2 + DEC-SCMM-03 (frozen model: RM1 cycle policy).

## Metadata
```text
WP ID:            WP-SCMM-09
Prompt ID:        P-SCMM-09 · v1.0
Task Class:       Domain extend (entity + command + authz + audit) — state-changing
Golden-Flow Profile: B (backend); UI reflection = SCMM-09-UI follow (frontend-ui-ux)
Risk Class:       MEDIUM (MOD-0162 CRUD slice; audit shared-seam dokunuşu)
Agent Lane:       AL-SCMM-CONCEPT (DEV) · Target Agent: backend-architect
Branch:           feature/structured-content-messaging · Expected HEAD: 570827fb
Persistence:      L3 (authoritative catalog; EntityBase.Version optimistic concurrency)
```

## Ölçülmüş girdi (SCMM-02, path:line)
- **① ConceptType** eksik alanlar: `color`, `isGroup`, `isList`, `parent` (bugün Code/Name/Description/SortOrder/Status/audit var) — `ConceptType.cs:11-35`.
- **② ConceptRelationship** birleşik değer+kenar yazma YOK: `CreateConceptRelationshipCommand` yalnız var-olan `FromConceptNodeId`+`ToConceptNodeId` alır (`ConceptRelationshipCommands.cs:9-20`); node önce ayrı yaratılıyor. Legacy "New UCLN List" ergonomisi = node+edge tek işlem.
- **Authz:** canonical `crm.knowledge.concept.*` S1'de seed+switch edildi; SCMM-09 canonical `ConceptPermissions.Read/Manage` kullanır (fallback DEĞİL).
- **Audit:** SCMM concept yüzeyi bugün **0 audit** (SCMM-05); `HttpCrmAuditPublisher` deseni var ama `SourceModule` hardcoded `"MOD-0150"` (`HttpCrmAuditPublisher.cs:71`) — concept için MOD-0162 olmalı.

## Kapsam (①② + audit bundle)
1. **ConceptType extend:** additive alanlar `Color` (string?, hex), `IsGroup` (bool), `IsList` (bool), `ParentConceptTypeId` (Guid?). CRUD + validasyon. ⚠️ `ParentConceptTypeId` **hiyerarşi** → RM1 (DEC-SCMM-03): cycle-guard (self/döngü red). Subject-scoped korunur.
2. **ConceptRelationship combined-write:** yeni `CreateConceptNodeWithRelationshipCommand` (veya eşdeğer) — node + edge **tek işlemde**; mevcut ayrı command'lar KALIR. Mevcut cycle-reject/RelationshipType/Direction/Priority/IsTemplateConforming validasyonu birebir uygulanır. Consistency: **atomic** (CRM standalone Mongo → `SupportsTransactionsAsync` guard + compensation; aksi 500).
3. **Audit-wiring (bundle):** `IKnowledgeConceptAuditPublisher` (mirror `IAccountAuditPublisher`) + `HttpCrmAuditPublisher`'da implement; concept create/edit/archive + combined-write → audit event (actor+tenant+UTC+object/version+correlation). **SourceModule = MOD-0162** (concept için doğru etiket). Fail-soft mevcut davranış (release yolu değil; R1 owning-team).
4. **Authz:** yeni/değişen endpoint'ler canonical `ConceptPermissions.Manage/Read`.

## Frozen model uyumu (DEC-SCMM-03)
- RM1: cycle politikası yalnız hiyerarşik ilişki (`ParentConceptTypeId`) + mevcut constrained edge'lere; her semantic edge'e değil.
- D8 no-engine korunur (ConceptChainTemplate motoru açılmaz; bu WP yalnız catalog+relationship).
- İsim: ①② rename edilmez (Message Scope/Argument Set renames ③④⑤⑥ içindir).

## Acceptance
- E2: build temiz; unit testler — yeni alanlar persist/round-trip; ParentConceptTypeId cycle-red; combined-write atomic (tx yoksa compensation); combined-write mevcut relationship validasyonlarını uygular; audit event alanları (SourceModule=MOD-0162, object/version) doğru.
- E4 (state-changing): persistence L3 + canonical authz denial (403) + tenant isolation + audit emission — **E3/E4 fleet açılınca**; cold ise NOT MEASURED (K10).
- Kapsam: yalnız ConceptType/ConceptRelationship + concept audit publisher; başka aggregate/modül YOK.

---

## §36.1 Agent Prompt (paste-ready)

```text
@[.antigravity/agents/backend-architect.md]
WP: WP-SCMM-09 · Prompt P-SCMM-09 v1.0  (MOD-0162 concept extend ①② + audit bundle)

Repository: C:\Users\user\Desktop\ERP-vNext
Branch: feature/structured-content-messaging · Expected HEAD: 570827fb · Worktree: ana checkout

Önce oku:
1. execution/domains/commercial-suite/work-packs/WP-SCMM-09-concept-catalog-extend.md (bu WP)
2. execution/domains/commercial-suite/module-packs/MOD-0162-FU03-concept-graph-runtime-ui.md (authority)
3. docs/decisions/DEC-SCMM-03-model-policy-freeze.md (RM1 cycle policy, frozen model)
4. services/Diten.CrmService/.../Domain/Entities/ConceptType.cs + ConceptRelationship.cs +
   .../Features/Knowledge/Concept/ConceptRelationshipCommands.cs + ConceptPermissions.cs
5. services/Diten.CrmService/.../Infrastructure/Audit/HttpCrmAuditPublisher.cs + Application/.../Account/IAccountAuditPublisher.cs

NE:
 (1) ConceptType additive alanlar: Color (string?, hex validasyonlu), IsGroup (bool), IsList (bool),
     ParentConceptTypeId (Guid?). CRUD + validasyon. ParentConceptTypeId HİYERARŞİ → cycle-guard (self+döngü red, RM1).
     Subject-scoped korunur; SortOrder mevcut kalır.
 (2) ConceptRelationship COMBINED-WRITE: yeni command (node + edge tek işlemde, legacy "New UCLN List" ergonomisi).
     Mevcut ayrı command'lar KALIR. Mevcut cycle-reject + RelationshipType/Direction/Priority/IsTemplateConforming
     validasyonu birebir uygulanır. Consistency ATOMIC: SupportsTransactionsAsync guard + compensation (standalone Mongo).
 (3) AUDIT bundle: IKnowledgeConceptAuditPublisher (IAccountAuditPublisher desenini birebir mirror) + HttpCrmAuditPublisher'da
     implement; concept create/edit/archive + combined-write → audit event, alanlar actor+tenant+UTC+object/version+correlation,
     SourceModule="MOD-0162" (hardcoded "MOD-0150" DEĞİL). Mevcut fail-soft davranışı koru (release-durability R1 owning-team işi).
 (4) AUTHZ: yeni/değişen endpoint'ler canonical ConceptPermissions.Manage/Read (fallback DEĞİL; S1 seed etti).
NEDEN: SCMM ①② extend — Content Studio Model Builder'ın ilk yapı taşı; SCMM-02 alan boşlukları + SCMM-04 audit contract.
NASIL: L3 persistence + EntityBase.Version optimistic concurrency; mevcut ConceptType/Relationship desenine sadık;
       new aggregate class-map trap'ine dikkat (RegisterClassMaps); DateTimeOffset array pitfall.
YAPMA: başka aggregate/modül (Subject/Content/Path/Campaign/Consent) DEĞİŞTİRME. ConceptChainTemplate motoru AÇMA (D8).
       ①②'yi rename ETME. Global authz/serializer değiştirme. HR&TEP/PVG/MOD-0018/0021 kodu DOKUNMA. Fallback'e geri dönme.
DOĞRULA (E2 + E4 fleet açılınca):
 - build temiz; unit: yeni alan round-trip · ParentConceptTypeId cycle-red · combined-write atomic (tx yoksa compensation) +
   mevcut relationship validasyonlarını uygular · audit event SourceModule=MOD-0162 + object/version.
 - E4 (fleet): 200 canonical-key'li rol / 403 key'siz · tenant isolation · audit emission. Cold → NOT MEASURED (K10).
Ayrı commit(ler). §22 raporu TÜRKÇE. Senin PASS'in kapanış değildir (K13).

Durma koşulları: MOD-0162-FU03 pack/contract belirsizse · combined-write mevcut validasyonla çelişirse · kapsam ①② dışına taşarsa. DUR + raporla.
```

## §37 CT bağımsız doğrulama (2026-09-07) → **ACCEPTED (E2)**
```text
Commits: c25a641e (①extend) · 0bcbcd5a (②combined-write) · 6a600830 (audit bundle)
Agent: PASS · Verification: PASS (CT git-scope + full-suite teyit) · CT: ACCEPTED · Evidence: E2 (E4 fleet-cold)
```
- ✅ Scope: 3 commit hepsi CrmService Knowledge/Concept + audit publisher — **SCMM-dışı domain dosyası YOK** (git diff --name-only teyit).
- ✅ **ParentConceptTypeId class-map** `NullableSerializer<Guid>(stringGuid)` (`DependencyInjection.cs:547`) — GUID trap kapalı.
- ✅ **Audit:** `sourceModule` default **"MOD-0150" korundu** (Account/Contact davranışı değişmedi), concept "MOD-0162" + objectVersion.
- ✅ **CT kendi koşumu — tam suite: 1619 passed / 5 skipped / 0 failed** → shared audit değişikliğinden **regresyon yok** (K4/K18).
- ⏳ **E4 (runtime 200/403 + tenant + audit emission) NOT MEASURED — fleet cold**; fleet açılınca §26 operator turu.

**Sonuç:** ①② extend + audit bundle canlıda değil ama kod+test tam; Content Studio Model Builder ilk yapı taşı doğdu.

## Kalan (bu WP dışı)
- **SCMM-09-UI** (frontend-ui-ux): ConceptTypes tab'ına color/isGroup/isList/parent + Connections combined-write ekranı.
- ③ (ConceptChainTemplate paralel-hat/kardinalite/moderator/for-whom) = **SCMM-10**.
- **E4 runtime** turu (fleet).
