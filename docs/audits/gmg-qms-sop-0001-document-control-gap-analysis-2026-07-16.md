# GMG-QMS-SOP-0001 Document Control — Gap Analizi

**Kaynak SOP:** GMG-QMS-SOP-0001 Document Control, v0.20 DRAFT (2026-07-11), Permanent UID `UID-0000001`
**Criticality:** Critical · **Process owner:** Global Quality Director
**Analiz kapsamı:** MOD-0028 (Documentation Structure / Baseline) + MOD-0029 (Controlled Documents)
**Tür:** ANALİZ-ONLY — kod/DB/migration/commit yapılmadı.
**Tarih:** 2026-07-16

---

## 1. Executive Summary

**Genel verdict: `MAJOR_GAPS`** (regulated Document Control SOP uyumu açısından)

Kısa değerlendirme:

- **Altyapı (folder / register / repository / baseline) tarafı GÜÇLÜ.** MOD-0028 register-backed import, `Draft → Approved → Effective → Superseded` baseline lifecycle, provisioning evidence + read-back reconciliation + deviation log ve UI'ı gerçekten regülasyon diliyle tasarlanmış. Bu, SOP'un "approved repository / effective release / reconciliation" beklentilerinin **klasör/baseline seviyesindeki** karşılığıdır ve sağlam bir temeldir.
- **Controlled Document lifecycle tarafı ZAYIF.** `ControlledDocument` bugün teknik bir dosya/versiyon kabıdır (CRUD + upload + share + access). SOP'un asıl regüle ettiği katman — request → UID/code allocation → criticality → impact assessment → review → comment → **approval (segregation)** → approved-pending-effective → **implementation readiness gate** → issue effective → withdraw obsolete → periodic review → suspend/retire — **neredeyse tamamen yok**. `ControlledItemStatus` yalnızca `Active/Archived`; SOP'un 9 statülü modeli, criticality, permanent UID, approval route yok.
- **Approval / segregation / training / non-waivable release gate tarafı EKSİK.** Approval workflow yok (matrix'te `Approve/Review` action'ları var ama **INERT placeholder**). Author≠approver kuralı yok. Training matrix / training readiness yok. SOP §19'daki 6 **non-waivable** release gate motoru yok. Bu üçü, Critical bir Document Control SOP'unun uyum çekirdeğidir.
- **Register ailesi (LOG-0001..0006, MTX, PLN) modül olarak yok.** Document Master Register (regulated decision system), Scope Entity Register, External Document Register, Retention Schedule + Litigation Hold, Coding/Legacy Register, Controlled Copy Log, Downtime Log — hiçbiri ayrı domain olarak mevcut değil.

**Tek cümlelik özet:** *Sistem bir "controlled repository + baseline lifecycle" olarak olgun; bir "controlled document lifecycle + release governance" sistemi olarak henüz değil.* En büyük risk, mevcut olgun altyapının **DMS gibi görünüp** aslında SOP §11 anlamında **approved interim repository** olması ve document-level release gate'lerinin enforce edilmemesidir.

---

## 2. SOP Requirement Matrix

Durum kodları: **✅ Covered** · **🟡 Partial** · **❌ Missing** · **⚪ Out of scope (öneri)**

| SOP § | Requirement | Mevcut implementasyon | Durum | Risk | Önerilen task |
|---|---|---|---|---|---|
| 1 Metadata | Document code, title, version, status, type, criticality, owner, effective date, review cycle | `ControlledDocument`: DocumentKey, Title, DocumentType, EffectiveDate, ReviewDate, ExpiryDate, Status | 🟡 | Orta | FU06/FU08 |
| 1 | **Permanent UID** (immutable, never reused) | Yok (yalnız teknik `Guid Id`) | ❌ | **Yüksek** | FU07 |
| 1 | Criticality (Critical/Major/Minor/Urgent) | Yok | ❌ | **Yüksek** | FU08 |
| 1 | Process owner / governing language / approved repository alanları | Yok (owner yalnız `OwnerCompanyId`) | ❌ | Orta | FU06 |
| 1 | Code/UID merkezi allocation, reuse engelleme, legacy mapping | Yok | ❌ | **Yüksek** | FU07 |
| 1 | UID migration'da korunur | N/A (UID yok) | ❌ | Yüksek | FU07 |
| 2 Record boundary | Blank form/template = controlled document | `TemplateMaster`/`TemplateVariant`/`TemplateDocument` blank template'i ele alır | 🟡 | Orta | FU06 |
| 2 | Completed record ≠ controlled document; record revize edilmez | Ayrım yok; Record Control (SOP-0002) modülü yok | ❌ | Orta | Record Control (ayrı MOD, ⚪) |
| 3 Scope Entity | Scope Entity Register (LOG-0005), applicability | Yok (scope yalnız TemplateVariant/CollectionInstance company/plant) | 🟡→❌ | Orta | FU (Scope Register, ⚪ düşük) |
| 3/13.3/14ext External docs | External Document Register (LOG-0003), monitoring, impact | Yok | ❌ | Orta | FU14 |
| 4 Regulatory refs | Effective version governs; draft = intelligence input | Yok | ❌ | Düşük | FU14 |
| 5 Roles | GQD/GRA/QPPV/QP/QA Doc/Owner/Local QA/IT-CSV/Training Coord | Access matrix `Role` principal var; named QMS rolleri yok | 🟡 | Yüksek | FU09 |
| 5.1 Segregation | Author ≠ sole approver; requester ≠ exception approver; admin audit koruması | Yok (approval workflow yok) | ❌ | **Yüksek** | FU09 |
| 6.1 Classes | 7 document class | DocumentType 6 değer (Sop/WI/Policy/Form/Template/Other); QualityAgreement/Urgent yok | 🟡 | Orta | FU08 |
| 6.2 Status | Draft/InReview/ApprovedPendingEffective/Effective/UnderRevision/Suspended/Superseded/Retired/ObsoleteCopy | `ControlledItemStatus`=Active/Archived; version `Draft/Active/Superseded/Archived` | ❌ | **Yüksek** | FU08 |
| 6.3 Versioning | Draft 0.x → 1.0 → minor 1.x → major 2.0 | `VersionNumber` int; major/minor/draft semantiği yok | 🟡 | Orta | FU08 |
| 6.1 Urgent/temp | 30 gün max, expiry → 4 zorunlu transition | Yok | ❌ | Orta | FU13 |
| 7.1 Criticality overlay | Critical/Major/Minor/Urgent farklı control | Yok | ❌ | Yüksek | FU08+FU09 |
| 7.2 Approval overlay | RA→GRA, PV→QPPV, release→QP, agreement→GQD+Legal, DMS→IT/CSV, group→CEO | Yok (INERT approval placeholders) | ❌ | **Yüksek** | FU09 |
| 7.3 Implementation readiness | 7 kriter YES/NO, no partial | Baseline seviyesinde `GetQualificationReadinessQuery` (FU09/10) var; document seviyesinde yok | 🟡 | **Yüksek** | FU10+FU11 |
| 8/9 Lifecycle 9.1–9.18 | request→UID→classify→draft→impact→review→comment→approve→pending→train→effective→withdraw→archive→periodic→revise/retire→in-flight→orphan | Yalnız CRUD/version/share; baseline tarafında approve/effective var | ❌ (document) / 🟡 (baseline) | **Yüksek** | FU08+FU09+FU12+FU13 |
| 10 GDocP/ALCOA+ | Audit trail, correction single line-through, backdating yasak | Genel BaseEntity audit alanları; GDocP correction trail yok; backdate koruması yok | 🟡 | Yüksek | FU21 |
| 11 DMS vs interim | 3 ortam ayrımı; repository assessment; effective-copy control; e-signature | Mevcut sistem = interim repository; ayrım modelde tutulmuyor; assessment/e-sig yok | ❌ | **Yüksek** | FU16 |
| 12.1 Suspension/urgent withdrawal | Suspended status + urgent withdrawal workflow | Yok | ❌ | Orta | FU13 |
| 12.2 Downtime | Downtime log + 3 WD reconciliation | Yok | ❌ | Düşük | FU20 |
| 12.3 Legacy migration | Inventory, gap-assess, code retention, UID korunur | Yok | ❌ | Düşük | FU07 (kod), ⚪ |
| 13.2 Variants | Own version/status/effective; auto Under-Revision; superseded master → suspend; language gate | TemplateVariant: own status + drift (computed); auto-underrevision/suspend/language YOK | 🟡 | Orta | FU18 |
| 14 External docs | Register + impact ≤10 WD, no editing | Yok | ❌ | Orta | FU14 |
| 15 Retention | Retention Schedule (LOG-0004), ≥10y, litigation hold | Yok (deletion soft-delete var, retention policy yok) | ❌ | **Yüksek** | FU15 |
| 16 KPI | 9 KPI, deviation trending | Deviation datası var (reconciliation); KPI dashboard yok | 🟡 | Orta | FU19 |
| 17 Deviations/escalation | Quality event/CAPA, SLA, planned exception workflow | FU09 deviation yalnız repository reconciliation; CAPA/SLA yok | 🟡 | Yüksek | FU13/FU19 + CAPA entegrasyonu |
| 18 Training | Training matrix (MTX-0003), competency/read-understand, effective gate | Yok | ❌ | **Yüksek** | FU11 |
| 19 Associated registers | LOG-0001..0006, MTX, PLN, FRM, TPL, WIN | Çoğu yok; template tarafı kısmen | ❌ | Yüksek | FU06/14/15 |
| 20 Master Register governance | Regulated decision system: access, segregation, protected gates, correction trail, own UID | Yok (ControlledDocument list ≠ register) | ❌ | **Yüksek** | FU06 |
| 21/19 Non-waivable gates | 6 gate, evidence+verifier+date, exception=permanently NO | Yok (baseline qualification ≠ document release gate) | ❌ | **Yüksek** | FU10 |
| 22 Revision history / drafts | Pre-effective drafts development file'da; revision history | Version `Draft` status var; development file / revision history kavramı yok | 🟡 | Düşük | FU08 |

---

## 3. Ne Zaten Yapılmış (Covered / güçlü temel)

- **Register-backed folder import (MOD-0028-FU06/FU07):** register kaynaklı klasör ağacı import + canonical key. SOP §12.3 legacy import ve §18 register mantığının klasör-seviyesi temeli.
- **Baseline lifecycle (FU08):** `BaselineRelease` üzerinde `Draft → Approved → Effective → Superseded`, `ApproveQmsBaselineCommand` / `MarkEffectiveQmsBaselineCommand`, `SourcePackageStatus` gate, approval reference/comment, supersedes/superseded-by linkage. **Bu, SOP'un effective-release + supersession mantığının en olgun kısmı** — ama *baseline* (klasör paketi) seviyesinde, *document* seviyesinde değil.
- **Access Profile Policy Templates (MOD-0029-FU05):** `AccessProfileTemplateCatalog` + planner; generated vs manual policy provenance (`DocumentAccessPolicySource`). Role-based access matrix'in tohumu.
- **Read-back reconciliation + provisioning evidence + deviation (FU09):** `DocumentCollectionProvisioningEvidence` (IT `PermissionsApplied` + QA `QaVerified` çift imza), `DocumentCollectionDeviation` (idempotent, non-destructive trail), `GetQualificationReadinessQuery`. SOP §11.1 reconciliation ve §18.1 register-reconciliation beklentisinin klasör-seviyesi karşılığı.
- **Reconciliation / Evidence / Deviation UI (FU10):** deviation resolve/accept, evidence sign-off UI.
- **Template Master (FU02):** `TemplateMaster` (MasterCode, Classification, VariantPolicy, status, EffectiveDate).
- **Template Variant + drift (FU03):** `TemplateVariant` own status + read-time `TemplateVariantDriftStatus` (InSync/RebaseRequired/Drifted/Blocked), master version linkage, rebase lineage, linked TemplateDocument.
- **Controlled Documents explorer + folder attachment (FU01):** `ControlledDocument` + `ControlledDocumentVersion` (immutable versions, checksum), folder-attach, favorite, move, copy-on-adopt lineage.
- **Access Matrix (FU04):** generalized sidecar `DocumentAccessPolicyEntry` (Allow/Deny, principal User/Role/Group/Company, 14 action), two-layer fail-closed authz.

---

## 4. Ne Kısmen Yapılmış (Partial)

- **Metadata:** temel alanlar var; **permanent UID, criticality, process-owner-role, governing language, approved-repository** yok.
- **Versioning:** integer version + immutable version var; **major/minor/draft (0.x/1.0/1.x/2.0)** semantiği yok.
- **Status model:** version `Draft` var ama approval transition'ı yok; document status yalnız `Active/Archived`.
- **Roller:** access matrix `Role` principal destekler; **named QMS rolleri ve karar hakları** yok.
- **Repository assessment / effective-copy:** baseline'da effective snapshot/manifest + hash var; **interim-repository assessment entity, backup/restore evidence, effective read-only copy control** yok.
- **Reconciliation:** klasör/provisioning reconciliation var; **document-register reconciliation** (her effective document register'da mı) yok.
- **Access control:** kaynak-seviyesi allow/deny var; **create/review/approve/release/archive/admin ayrımı ve access-review frequency** yok.
- **Variants:** own status + drift + rebase var; **auto Under-Revision on master effective, superseded-master→suspend, local-language gate, bilingual reviewer/local approver** yok.
- **Audit / GDocP:** genel audit alanları var; **GDocP correction trail (single line-through), backdating önleme, approval audit trail** yok.
- **Implementation readiness:** baseline qualification readiness var; **document-level 7-kriter readiness + training kriteri** yok.

---

## 5. Ne Eksik (Missing)

1. **Document Master Register** (LOG-0001) — regulated decision system, release gate enforcement, protected gate logic, correction trail, kendi UID/versiyonu.
2. **Permanent UID & Code allocation** — merkezi, immutable, never-reused, legacy mapping (LOG-0006 / Coding Register).
3. **Controlled Document lifecycle status engine** — 9 statü + geçiş kuralları + urgent/temp expiry.
4. **Approval workflow + segregation** — role-based mandatory approver resolution, author≠approver, RA/PV/QP/GQD overlay.
5. **Non-waivable release gate engine** (SOP §19, §21) — 6 gate, evidence+verifier+date, exception permanently NO.
6. **Training matrix + training readiness** (MTX-0003) — effective release gate ile bağlı.
7. **Periodic review** — 60 gün önce initiate, due-date, ONE extension max, overdue escalation.
8. **Suspension / retirement / urgent withdrawal** — Suspended status + urgent withdrawal workflow.
9. **External Document Register** (LOG-0003) — owner, monitoring source, impact ≤10 WD.
10. **Retention Schedule + Litigation Hold** (LOG-0004) — ≥10y, deletion prevention, legal hold.
11. **Repository Assessment & DMS boundary** — interim vs validated DMS ayrımı, assessment entity, e-signature sınırı.
12. **Controlled Copy Log + obsolete copy withdrawal** (LOG-0002).
13. **Downtime log** (WIN-0002) — 3 WD reconciliation.
14. **Quality event / CAPA escalation** entegrasyonu (MOD-0023 workflow / QMS event ile).
15. **KPI dashboard** (SOP §15).
16. **Scope Entity Register** (LOG-0005).
17. **Record Control / GDocP** (ayrı SOP-0002 modülü — completed record boundary).

---

## 6. Önerilen FU / Task Roadmap (MOD-0029)

| FU | İsim | Ne kapatır (SOP §) | Bağımlılık |
|---|---|---|---|
| **FU06** | Document Master Register Foundation | §18, §20, §1 metadata, §2 boundary | — |
| **FU07** | Controlled Document UID & Code Allocation | §1, §6.3, §12.3 (legacy code retention) | FU06 |
| **FU08** | Controlled Document Lifecycle Status Engine | §6.1/6.2/6.3, §1 criticality, §22 | FU06, FU07 |
| **FU09** | Approval Route Matrix & Segregation Rules | §5, §5.1, §7.2, §9.9 | FU08, (MOD-0023) |
| **FU10** | Non-Waivable Release Gate Engine | §7.3, §19, §21, §20 | FU06, FU08, FU09 |
| **FU11** | Training Matrix & Effective Release Readiness | §7.3(1-2), §17, §19 gate 5 | FU10, (HCM/LMS) |
| **FU12** | Periodic Review / Extension / Overdue Control | §8, §9.15, §15 | FU08 |
| **FU13** | Suspension / Urgent Withdrawal / Retirement | §6.2, §12.1, §16 | FU08, FU09 |
| **FU14** | External Document Register | §3, §13.3, §14 | FU06 |
| **FU15** | Retention Schedule & Litigation Hold | §14 | FU06 |
| **FU16** | Repository Assessment & DMS Boundary | §11, §11.1, §11.2 | FU06 |
| **FU17** | Controlled Copy / Obsolete Copy Reconciliation | §9.13, §18(LOG-0002), gate 6 | FU10 |
| **FU18** | Variants & Translations Lifecycle Hardening | §13.2 | FU08 (mevcut FU03 üstüne) |
| **FU19** | Document Control KPI Dashboard | §15, §16 | FU08–FU13 |
| **FU20** | Downtime / Temporary Controlled Issue | §12.2 | FU16 |
| **FU21** | Audit Trail / GDocP Correction Trail Hardening | §10, §16 | FU06 |

---

## 7. Önerilen Sonraki 3 Task

Mevcut durum: **MOD-0028-FU11 (Qualification Hard Gate for Effective Baselines)** bekliyor.

**Öneri: FU11'i şimdilik ertele; sıralama şu olsun:**

1. **MOD-0029-FU06 Document Master Register Foundation** — her şeyin çıpası. UID, status, release gate, reconciliation bunun üstüne oturur. Bu olmadan diğer FU'lar duracağı bir zemin bulamaz.
2. **MOD-0029-FU07 UID & Code Allocation** — permanent UID + code-never-reuse. SOP'un en yüksek-risk, en ucuz-erken kazancı (retrofit maliyeti sonradan çok yüksek).
3. **MOD-0028-FU11 Qualification Hard Gate** — **ardından** yapılsın ve **FU10 Non-Waivable Release Gate Engine ile hizalanacak biçimde** tasarlansın (bkz. §8 karar 1). FU11 baseline-effective gate'i, document-release gate ile aynı gate modelini paylaşmalı.

**FU11 hakkında kritik uyarı:** FU11 "Qualification Hard Gate for Effective **Baselines**" — yani *baseline* (klasör paketi) qualification'ı. SOP §19'daki 6 gate ise *document* release gate'i. **Bunlar farklı iki gate.** FU11'i document release gate ile karıştırmak, iki farklı "effective" kararını tek koda bağlayıp regülasyon açısından yanlış bir eşdeğerlik yaratır (bkz. Risk 2). FU11 yapılırsa, gate motoru **paylaşılan** ama **subject'i (baseline vs document) ayrı** olacak şekilde kurulmalı.

---

## 8. Kritik Tasarım Kararları

1. **Baseline Effective gate ≠ Document Effective gate — AYRILMALI.** Baseline = klasör/register paketi lifecycle'ı (MOD-0028). Document = tekil controlled document lifecycle'ı (MOD-0029). Ortak bir **gate evaluation engine** paylaşabilirler ama iki ayrı subject ve iki ayrı readiness kriter seti olmalı. Öneri: gate motoru MOD-0029-FU10'da, `subjectType = Baseline | Document` ile generic.
2. **Document Master Register MOD-0029 içinde olmalı.** ControlledDocument aggregate'in "governance projection"ı olarak; ayrı MOD açmak yerine FU06. Register = regulated decision system (protected gate result, segregation, correction trail).
3. **Approval workflow MOD-0023 ile entegre olmalı.** Sıfırdan approval engine yazmak yerine MOD-0023 workflow'u approval route resolution için kullan; MOD-0029 yalnız mandatory-approver matrix + segregation kuralını sağlar. (Not: MOD-0023 runtime bugün ocelot route + permission seed'e bağlı — hafıza: `mod-0023-workflow-frontend`.)
4. **E-signature scope DIŞI kalmalı (şimdilik).** Mevcut sistem **approved interim repository**. SOP §11 validated-DMS/e-signature'ı açıkça interim'den ayırıyor. Öneri: FU16'da sistemi resmî olarak "interim repository" işaretle, e-signature'ı explicit **out-of-scope** yaz; "separate approval mechanism" (wet-sign / reconciled) alanı tut.
5. **Training readiness HCM/LMS ile entegre olmalı.** FU11 training kriteri, kendi mini training-record tablosu yerine HCM/LMS training assignment'ına referansla çözülmeli (loose coupling).
6. **Record Control AYRI modül olmalı (SOP-0002).** SOP §2 boundary net: completed record ≠ controlled document. Mevcut Document Management yanlışlıkla completed record lifecycle'ı ele **almıyor** (iyi) — ama ele almaya da çalışmamalı. Ayrı MOD (⚪ scope dışı, sonraki dalga).
7. **External documents AYRI register olmalı** (FU14) — read-only, no-edit, monitoring metadata; ControlledDocument aggregate'ine karıştırılmamalı.
8. **Retention/Litigation Hold ayrı lifecycle service** (FU15) — deletion'ı intercept eden cross-cutting policy; her aggregate'in soft-delete'i buna danışmalı.

---

## 9. Riskler

1. **Interim repository DMS gibi görünüyor.** Sistem olgun ve "kontrollü" hissettiriyor; SOP §11 anlamında validated DMS **değil**. Repository assessment ve boundary işaretlenmezse denetimde yanlış beyan riski. *(Yüksek)*
2. **Release gate'siz effective document.** Bugün bir ControlledDocument approval/gate olmadan "effective date" alabiliyor. SOP §19: gate'siz effective = kontrolsüz. *(Yüksek)*
3. **UID/code kontrolü yok.** Permanent UID retrofit'i, veri büyüdükçe katlanarak pahalılaşır. *(Yüksek — erken yap)*
4. **Self-approval riski.** Author≠approver enforce edilmiyor; segregation yok. *(Yüksek)*
5. **Variant/master mismatch.** Master effective olduğunda variant otomatik Under-Revision'a geçmiyor; superseded master'ın variant'ı suspend olmuyor → yürürlükte yanlış versiyon. *(Orta)*
6. **Training tamamlanmadan effective use.** Training readiness gate yok. *(Yüksek — Critical process için)*
7. **Superseded kopya point-of-use'da kalıyor.** Controlled Copy Log / obsolete withdrawal yok (baseline reconciliation var, document yok). *(Orta)*
8. **Audit trail manipülasyonu.** GDocP correction trail + backdate koruması yok; approval audit trail yok. *(Yüksek)*
9. **Retention / litigation hold yok.** Yasal saklama ve legal-hold deletion prevention yok. *(Yüksek)*
10. **External document drift.** İzlenmeyen dış referanslar; impact assessment yok. *(Orta)*

---

## 10. Final Recommendation

- **Önce ne yapılmalı:** MOD-0029-FU06 (Document Master Register Foundation) → FU07 (UID & Code Allocation). Bu ikisi tüm SOP uyum zincirinin çıpasıdır ve retrofit maliyeti en yüksek olan iki gereksinimdir.
- **FU11 ertelenmeli mi?** **Evet, kısmen.** FU06+FU07'den sonraya alınmalı ve FU10 gate motoruyla **aynı gate modelini paylaşacak** şekilde tasarlanmalı. FU11'i şimdi izole yapmak, ileride document release gate ile çakışacak ikinci bir gate implementasyonu doğurur. FU11 = *baseline* gate; SOP §19 = *document* gate — motor ortak, subject ayrı.
- **Minimum Viable Compliance Path (Critical SOP için asgari):**
  1. FU06 Master Register (UID + status + gate result alanları, protected, segregation).
  2. FU07 Permanent UID + code-never-reuse.
  3. FU08 Lifecycle status engine (9 statü + criticality).
  4. FU09 Approval route + segregation (author≠approver).
  5. FU10 Non-waivable 6-gate engine (evidence+verifier+date).
  6. FU11 Training readiness + FU16 repository-assessment/DMS-boundary işareti.
  Bu 6 adım, SOP'un **non-waivable** çekirdeğini karşılar. Periodic review (FU12), retention/hold (FU15), external register (FU14), KPI (FU19) ikinci dalgadır.

---

### Final Verdict: `MAJOR_GAPS`

- Altyapı (baseline/repository/reconciliation): **PARTIAL-to-strong**
- Controlled document lifecycle + release governance: **MAJOR_GAPS**

### Suggested Next Prompt

**Başlık:** `MOD-0029-FU06 Document Master Register Foundation — implementation plan`

**Öneri:** Önce şu prompt çalıştırılmalı — *"MOD-0029-FU06 Document Master Register Foundation için module pack + entity/model tasarımı hazırla: ControlledDocument'e permanent UID, criticality, process-owner-role, governing-language, approved-repository, lifecycle-status alanları ekle; Document Master Register'ı regulated decision system olarak (protected gate result, segregation, correction/audit trail, kendi UID/versiyonu) tasarla; FU07 UID allocation ve FU10 release-gate engine için extension noktalarını bırak. Baseline-effective gate ile document-release gate'i ayrı subject, ortak motor olacak şekilde konumla."*
