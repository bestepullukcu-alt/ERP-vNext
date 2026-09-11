# WORK PACKAGE — WP-SCMM-02 · Baseline doğrulama (INS lane, read-only)

> **Control Tower kaydı (SoR).** SCMM iş planı R0, gate G0. Bağımlı: SCMM-01 ✅ DECIDED (H). Amaç: SCMM'in reuse-vs-build kararlarını **ölçülmüş baseline**'a oturtmak — HTML (Figma-kaynaklı, estimate) ve stale tracker'a değil.
> **Kapsam sınırı (owner talimatı 2026-09-07):** HR&TEP / PVG sheet'leri **yalnız tüketici olarak okunur**; o bloklar/foundation'lar **değiştirilmez/downgrade edilmez** (docx §2). Yeni defect iddia etme; SCMM açısından hazırlığı ölç.

## Metadata
```text
WP ID:            WP-SCMM-02
Prompt ID:        P-SCMM-02 · v1.0
Task Class:       Inspection / baseline (Profile C — no writes)
Risk Class:       LOW (read-only)
Capability:       SCMM (Structured Content & Messaging Management) · owner CAND-CAP-0011 + MOD-0162 (foundation)
Agent Lane:       AL-SCMM-BASELINE (INS)
Target Agent:     /read-only-audit  (read-only-auditor — yazma aracı yok)
Branch:           feature/structured-content-messaging
Expected HEAD:    c965e5f6  (= origin/main tip)
Evidence hedefi:  E1 (static, path:line) + fleet ayaktaysa E2/E3 smoke; runtime yoksa "NOT MEASURED — fleet cold"
```

## Ölçülmüş ön-gerçek (CT, 2026-09-07)
Concept foundation kodu **bu branch'te ve origin/main'de MEVCUT** (1593 CrmService dosyası): `KnowledgeConceptTypes`, `KnowledgeConceptChainTemplates`, `KnowledgeContentConceptLinks`, `KnowledgeContents`, `KnowledgePaths`, `KnowledgeAudienceProfiles` controller + Application feature'ları. → reuse hedefi hazır; SCMM-02 boşlukları doğrular.

## İnceleme hedefleri (HTML ①–⑥ iddialarını adlandırılmış revizyonda doğrula/düzelt)
| # | Aggregate | Doğrulanacak boşluk (HTML iddiası) |
|---|---|---|
| ① ConceptType | `ConceptType` | color / isGroup / isList / parent alanları YOK mu? SortOrder var mı? |
| ② ConceptRelationship | `ConceptRelationship` | cycle-reject/RelationshipType/Direction/Priority/IsTemplateConforming var mı? "birleşik değer+kenar yazma" YOK mu (Nodes/Connections ayrı mı)? |
| ③ ConceptChainTemplate | `ConceptChainTemplate` | version/publish/freeze VAR mı? paralel hat / step kardinalite / moderator / for-whom ekseni YOK mu? |
| ④ Strategy Template | — | vNext'te karşılık YOK mu? **İsim çakışması:** MOD-0167-FU04 `StrategyTemplate` (ticari play) ≠ legacy Strategy Template — teyit et |
| ⑤ UCLN Book | `KnowledgePath` | KnowledgePath = içerik akışı, "çözülmüş kavram zinciri + tip-kolonlu grid" YOK mu? |
| ⑥ Book Pages | `KnowledgePath` Steps | Steps builder düz liste mi (Parent/Child + iki-panel YOK mu)? |
| İçerik | `KnowledgeContent` | video/message-script tipleri hazır mı? |
| Blocker | `AudienceProfile` | **tek-boyutlu mu** (ProfileCode/Name)? çok-eksen/Subject-bağlı DEĞİL mi? |
| Subject | `Subject` | mevcut mu? `regulated` bayrağı bugün nerede/var mı? |

## Shared-foundation consumer-readiness (read-only, SCMM açısından)
Bu branch/revizyonda kayıt/kod düzeyinde oku (değiştirme): MOD-0018, MOD-0021, MOD-0023, MOD-0028, MOD-0031, MOD-0032, MOD-0290, MOD-0026, MOD-0027. Her biri için SCMM tüketimi açısından: mevcut mu / contract var mı / runtime kanıtı var mı. Excel PVG/HR&TEP satırlarını **kaynak-kanıt** olarak alıntıla, ama o bloklara dokunma.

## Acceptance
Baseline raporu üret: her ① –⑥ + blocker için `path:line` ile EXISTS/MISSING + HTML estimate ile fark; shared-foundation consumer-readiness tablosu; "reuse-as-is / extend / new-build" per satır. **Estimate'i completion sayma; runtime yoksa açıkça NOT MEASURED yaz** (K10). Çıktı SCMM-09/10/11/12 reuse kararlarını besler.

### §37 CT bağımsız doğrulama (2026-09-07) → **ACCEPTED (E1)**
```text
Branch/HEAD: feature/structured-content-messaging @ c965e5f6
Agent Verdict: PASS · Verification: PASS (kritik bulgular CT tarafından path:line ile teyit) · CT: ACCEPTED · Evidence: E1
```
CT'nin bağımsız teyit ettiği kritik bulgular:
- ✅ **AudienceProfile** tek-eksen, **SubjectId YOK**, tenant-global (`AudienceProfile.cs:10-32`) → THE blocker (new-build: çok-eksen + Subject-scoped).
- ✅ **`regulated` bayrağı CrmService src'de 0 kez** (grep=0) → **GREENFIELD**; mevcut coupling değil.
- ✅ **ConceptType** color/isGroup/isList/parent yok, SortOrder var (`ConceptType.cs`).
- ✅ **StrategyTemplate = MOD-0167-FU04** "binds not produces" (`StrategyTemplate.cs:4-11`) → isim çakışması teyitli.
- Reuse/extend/new-build haritası (rapor §2) SCMM-09+ girdisi olarak kabul.

**Açık kalan (dürüst gap'ler, E1 sınırı):**
- MOD-0031 evidence-linking ayrı servis olarak E1'de doğrulanamadı → SCMM-07 dependency riski (PVG: spec-only %0 ile tutarlı).
- Excel doğrudan okunamadı (repo-dışı) + fleet cold → **shared-foundation runtime readiness (E3) ölçülmedi**; HR&TEP↔PVG uzlaştırması **kapsam-dışı** (owner: o sheet'lere dokunma) — SCMM tüketici okuması yeterli, canlı readiness gerekirse ayrı E3 turu.
- Registry record-changes (DEC-SCMM-01) working-tree'de, **commit'siz** — baseline'ın parçası.

---

## §36.1 Agent Prompt (paste-ready)

```text
/read-only-audit
WP: WP-SCMM-02 · Prompt P-SCMM-02 v1.0  (INS — baseline, YAZMA YOK)

Repository: C:\Users\user\Desktop\ERP-vNext
Branch: feature/structured-content-messaging · Expected HEAD: c965e5f6 · Worktree: ana checkout

Denetim modu: worktree-read-only (strict). Her bulgu path:line kanıtı taşır. DÜZELTME/YAZMA YOK.

Önce oku:
1. execution/domains/commercial-suite/work-packs/SCMM-content-studio-work-plan.md (§2 reuse-vs-build, §3 readiness)
2. docs/decisions/DEC-SCMM-01-bl316-ownership-decision-brief.md (H kararı; owner = MOD-0162 foundation + CAND-CAP-0011)

NE:  SCMM için ÖLÇÜLMÜŞ baseline üret. HTML (Figma estimate) ①–⑥ iddialarını bu revizyonda doğrula/düzelt.
     Concept foundation kodu MEVCUT (services/Diten.CrmService — KnowledgeConceptTypes/ChainTemplates/
     ContentConceptLinks/Contents/Paths/AudienceProfiles). Her aggregate için EXISTS/MISSING + alan-düzeyi boşluk:
     - ConceptType: color/isGroup/isList/parent yok mu? SortOrder var mı?
     - ConceptRelationship: cycle-reject/RelationshipType/Direction/Priority/IsTemplateConforming? birleşik yazma yok mu?
     - ConceptChainTemplate: version/publish/freeze var mı? paralel hat/kardinalite/moderator/for-whom yok mu?
     - Strategy Template (④): karşılık yok mu? MOD-0167-FU04 StrategyTemplate ISIM ÇAKIŞMASI teyit.
     - KnowledgePath (⑤⑥): "çözülmüş kavram zinciri" değil (içerik akışı) mı? Steps builder düz liste (Parent/Child yok) mu?
     - KnowledgeContent: video/message-script tipleri var mı?
     - AudienceProfile: tek-boyutlu mu (çok-eksen/Subject-bağlı DEĞİL)? — bu THE blocker, netleştir.
     - Subject + `regulated` bayrağı: mevcut mu, bugün nerede?
     + Shared-foundation consumer-readiness (READ-ONLY): MOD-0018/0021/0023/0028/0031/0032/0290/0026/0027 —
       SCMM tüketimi açısından mevcut/contract/runtime kanıtı. Excel PVG/HR&TEP'i kaynak-kanıt olarak alıntıla.
NEDEN: SCMM-09+ reuse-vs-build kararları ölçülmüş baseline ister; HTML estimate + stale tracker (2026-06-23) yeterli değil.
NASIL: static kod okuması (E1) + fleet AYAKTAYSA ilgili sayfaların runtime smoke'u (E3); değilse "NOT MEASURED — fleet cold".
YAPMA: HİÇBİR dosyayı değiştirme. HR&TEP/PVG bloklarını/foundation'ları downgrade etme, yeni defect iddia etme (docx §2).
       Estimate'i completion olarak raporlama. Kapsamı SCMM dışına taşıma.
DOĞRULA/ÇIKTI: ①–⑥ + blocker tablosu (EXISTS/MISSING + path:line + reuse/extend/new-build) + shared-foundation
       consumer-readiness tablosu + "HTML estimate ile fark" notları. §22 formatı, RAPORU TÜRKÇE.

Senin PASS'in kapanış değildir (K13). Kod değişmesi gerekiyorsa o ayrı bir WP'dir, bu lane'in işi değil.
```
