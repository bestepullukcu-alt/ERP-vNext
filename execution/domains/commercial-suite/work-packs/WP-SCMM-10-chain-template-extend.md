# WORK PACKAGE — WP-SCMM-10 · ConceptChainTemplate (③ Book Design / "Argument Blueprint") extend

> **Control Tower kaydı (SoR).** SCMM R1 model band, gate G2. Bağlı: SCMM-03 ✅ (RM2 composition-template invariants freeze) · SCMM-09 ✅. Owner: domain + frontend lead. Module: **MOD-0162** (concept foundation — H'de retained).
> **Boundary:** Bu, MOD-0162'deki **mevcut ConceptChainTemplate**'in extend'idir (③ Book Design/"Argument Blueprint"). CAND-CAP-0011'in **yeni composition template'i (④⑤⑥ Content Studio assembly, SCMM-14)** DEĞİLDİR — karıştırma.

## Metadata
```text
WP ID:            WP-SCMM-10
Prompt ID:        P-SCMM-10 · v1.0
Task Class:       Domain extend (frozen/published aggregate structure change) — state-changing
Golden-Flow Profile: B (backend); UI = SCMM-10-UI follow (Templates Slim tab)
Risk Class:       MEDIUM-HIGH (published+frozen aggregate'in çekirdek yapısı değişiyor → geri-uyumluluk kritik)
Agent Lane:       AL-SCMM-CHAIN (DEV) · Target Agent: backend-architect
Branch:           feature/structured-content-messaging · Expected HEAD: 9c13bca6
Persistence:      L3 (ChainVersion + EntityBase.Version); publish-freeze KORUNUR
```

## Ölçülmüş girdi (SCMM-02, path:line)
`ConceptChainTemplate.cs`: `OrderedConceptTypes` **flat List<Guid>** (min 2, tekrar yok, "recursion is F7"); publish `OrderedConceptTypes`'ı **dondurur** (değişiklik = yeni versiyon; aynı subject'te iki published effective-pencere çakışamaz); `ChainVersion` (iş versiyonu). **Eksik (③ %50):** paralel hat, step kardinalite, moderator, for-whom (audience) ekseni.

## Kapsam (③ extend — 4 boşluk)
1. **Paralel hat / branch:** flat `OrderedConceptTypes` → **branched yapı** (sections/parallel branches, RM2). Her branch sıralı concept-type dizisi. **Geri-uyumluluk:** mevcut flat = tek branch; **read-time migration** (mevcut published template'ler bozulmaz). `OrderedConceptTypes` korunabilir (türetilmiş/tek-branch view) veya migrate edilir — publish-freeze semantiği yeni yapıya birebir taşınır.
2. **Step cardinality:** her pozisyon için min/max seçim (RM2 min/max selections).
3. **Moderator:** "to moderator" — moderator/allowed-roles ekseni (RM2 allowed roles). Kimin bu adımı yöneteceği (rol referansı); **atama motoru DEĞİL, sadece config** (D8).
4. **For-whom (audience) ekseni:** template'e audience-dimension config (RM3 usage-context audience dims) — referans, membership DEĞİL.

## Frozen model uyumu (DEC-SCMM-03)
- **RM2** composition-template invariants: published version · sections/parallel branches · allowed roles · min/max selections · field schema · grouping/ordering — hepsi bu extend'in hedefi.
- **D8 no-engine:** template YAPIYI SAKLAR; hesaplamaz/ilerletmez/atamaz. Conformance yalnız türetilir (mevcut davranış korunur).
- **Publish-freeze:** publish yeni (zengin) yapıyı dondurur; değişiklik = yeni ChainVersion; aynı subject iki published effective-overlap yasağı korunur.
- **Rename:** ③ kullanıcı-yüzü = "Argument Blueprint / Book Design"; **aggregate adı ConceptChainTemplate KALIR** (shipped kodu rename etme = gereksiz churn; H foundation-retained).

## Acceptance
- E2: build temiz; unit — branched yapı persist/round-trip; **mevcut flat template read-time tek-branch'e migrate olur (geri-uyum)**; publish yeni yapıyı dondurur + değişiklik yeni versiyon; step cardinality min/max validasyonu; moderator/for-whom config persist; D8 (motor yok) korunur; effective-overlap yasağı korunur.
- E4 (fleet): canonical authz 200/403; branched template create/publish/version; audit emission (SCMM-09 concept audit publisher genişletilir). Cold ise E1 + fleet turu.
- Kapsam: yalnız ConceptChainTemplate + (audit publisher'a chain event'i). Başka aggregate/modül YOK; CAND-CAP-0011 composition (④⑤⑥) AÇILMAZ.

---

## §36.1 Agent Prompt (paste-ready)

```text
@[.antigravity/agents/backend-architect.md]
WP: WP-SCMM-10 · Prompt P-SCMM-10 v1.0  (ConceptChainTemplate ③ extend — MOD-0162)

Repository: C:\Users\user\Desktop\ERP-vNext
Branch: feature/structured-content-messaging · Expected HEAD: 9c13bca6 · Worktree: ana checkout

Önce oku:
1. execution/domains/commercial-suite/work-packs/WP-SCMM-10-chain-template-extend.md (bu WP)
2. docs/decisions/DEC-SCMM-03-model-policy-freeze.md (RM2 invariants, D8, publish-freeze)
3. services/Diten.CrmService/.../Domain/Entities/ConceptChainTemplate.cs +
   .../Features/Knowledge/Concept/ChainTemplate/ConceptChainTemplateCommands.cs + Handlers.cs + Queries.cs +
   .../Api/Controllers/CRM/KnowledgeConceptChainTemplatesController.cs
4. services/Diten.CrmService/.../Features/Knowledge/Concept/IKnowledgeConceptAuditPublisher.cs (SCMM-09 audit seam)

BOUNDARY: Bu MOD-0162'deki MEVCUT ConceptChainTemplate'in extend'i (③ Book Design). CAND-CAP-0011 composition
  template'i (④⑤⑥, SCMM-14) DEĞİL — onu açma.

NE (③'ün 4 boşluğu):
 1) PARALEL HAT: flat OrderedConceptTypes → branched yapı (sections/parallel branches, RM2). Her branch sıralı
    concept-type dizisi. GERİ-UYUM: mevcut flat = tek branch, read-time migration; mevcut published template'ler BOZULMAZ.
    Publish-freeze semantiği yeni yapıya birebir taşınır.
 2) STEP CARDINALITY: her pozisyon için min/max seçim (RM2).
 3) MODERATOR: "to moderator" — moderator/allowed-roles ekseni (rol referansı config). Atama MOTORU değil (D8).
 4) FOR-WHOM: audience-dimension config (referans; membership değil).
NEDEN: ③ Book Design %50 → paralel/kardinalite/moderator/for-whom eksik (SCMM-02); Content Studio Model Builder'ın gövdesi.
NASIL: L3 + ChainVersion/EntityBase.Version optimistic concurrency; publish = yeni yapıyı dondur, değişiklik = yeni versiyon,
       aynı subject iki published effective-overlap yasağı KORUNUR. new-aggregate class-map/GUID trap dikkat (embedded branch tipleri).
       Audit: chain create/edit/publish/archive → SCMM-09 IKnowledgeConceptAuditPublisher (SourceModule MOD-0162) genişlet.
YAPMA: D8 ihlali (motor/otomatik ilerletme/atama EKLEME — yalnız SAKLA). ConceptChainTemplate'i RENAME etme. Başka aggregate/
       modül (Subject/Content/Path/CAND-CAP-0011 composition) AÇMA. Global authz/serializer değiştirme. Geri-uyumu kırma.
DOĞRULA (E2 + E4 fleet):
 - build temiz; unit: branched persist/round-trip · mevcut flat → tek-branch read-time migration · publish freeze +
   değişiklik yeni versiyon · step min/max validasyon · moderator/for-whom persist · effective-overlap yasağı · D8 (motor yok).
 - E4 (fleet): authz 200/403 · branched create/publish/version · audit emission. Cold → NOT MEASURED (K10).
Ayrı commit(ler). §22 raporu TÜRKÇE. Senin PASS'in kapanış değildir (K13).

Durma koşulları: geri-uyum (mevcut published template) riske girerse · publish-freeze semantiği belirsizse · kapsam ③ dışına taşarsa. DUR + raporla.
```

## §37 CT bağımsız doğrulama (2026-09-08) → **ACCEPTED (E2)**
```text
Commits: 2308982f (③ branched+cardinality+moderator+for-whom) · 900c12da (chain audit)
Agent: PASS · Verification: PASS (CT scope+backward-compat+full-suite teyit) · CT: ACCEPTED · Evidence: E2 (E4 fleet-cold)
```
- ✅ Scope: yalnız concept/chain + audit publisher — SCMM-dışı YOK.
- ✅ **Geri-uyum (kritik):** `OrderedConceptTypes` **spine korundu** + `Branches` additive; **read-time migration** (legacy flat → tek branch "B1", DEPO DEĞİŞMEZ) — legacy published template'ler bozulmuyor.
- ✅ Embedded ConceptChainBranch/Step class-map (GUID trap kapalı); publish-freeze branch'e genişletildi (409); effective-overlap korundu; D8 no-engine.
- ✅ **CT kendi koşumu — tam suite: 1625 passed / 5 skipped / 0 failed** (1619→1625, +6) → regresyon yok (K4/K18).
- ⏳ **E4 (runtime branched create/publish/version + authz + audit) NOT MEASURED — fleet cold**; fleet açılınca §26 operator turu.

## Kalan (bu WP dışı)
- **SCMM-10-UI** (frontend): Templates Slim tab'ında branched builder + cardinality + moderator + for-whom editörü.
- **E4 runtime** turu (fleet).
- SCMM-11 (eligibility) — AudienceProfile çok-eksen blocker'ı önce.
