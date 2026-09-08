# CONTROL TOWER — Çalışma Yöntemi (SOP) v2.4
## Evidence-Preserving, Repository-Valid Delivery Control Plane — Agent Lane Model

**Belge durumu:** Proposed canonical SOP  
**Sürüm:** 2.4  
**Önceki sürüm:** v2.3  
**Amaç:** ERP-vNext için capability → module → work-package yürütmesini; canonical authority, measured code/runtime reality, dependency gates, Agent Lane parallel development, prompt lifecycle, runtime verification, integration ve yeniden planlama ile tek bir kontrollü çalışma modelinde yönetmek.

---

## 0. Sürüm kontrolü

### 0.1 v2.4'te eklenen dispatch-adresleme kontrolleri

v2.3 korunmuş; prompt üretiminin **hedef ajanı adreslememesi** boşluğu kapatılmıştır.
Ölçüm: §6 entry-point tablosu belgede bir kez tanımlanıp akışın hiçbir adımından çağrılmıyordu;
CLASSIFY çıktısı, PLAN kolonları, DoR, §17.1 metadata ve §36 template'in hiçbirinde hedef ajan alanı yoktu.

1. **Entry point sınıflamanın çıktısıdır:** §11 CLASSIFY bloğuna `Entry point` + `Target agent / command` eklendi.
2. **VER ve INT lane'lerinin adresi:** §6 tablosu dört lane tipinin dördünü de kapsıyor.
3. **Hedef ajan alanı:** §17.1 ve §36 metadata'ya `Target Agent / Entry Point` eklendi.
4. **Ajan giriş alanları (§17.4):** her giriş noktasının prompt'ta zorunlu kıldığı alanlar tablolanıp
   `BLOCKED — agent/scope mismatch` kapısı tanımlandı.
5. **Paste-ready prompt (§36.1):** metadata formu prompt değildir; her WP yapıştırılabilir tek blok taşır.
6. **DoR:** iki yeni zorunlu kutu (§8.1).

Daily Operating Card aynı turda hizalandı (§1, §3).

### 0.2 v2.3'te eklenen geçerlilik ve adoption kontrolleri

v2.2 korunmuş; aşağıdaki üç adoption riski kapatılmıştır:

1. **Evidence-preserving iron rules:** K1–K21 artık evidence sınıfı ve gerekçesi taşır.
   - Tarihsel incident gerçekten ölçülmüşse `MEASURED CASE`.
   - Repo ölçümü varsa `OBSERVED`.
   - Önleyici ama henüz incident kaydı yoksa `DERIVED CONTROL`.
   - Governance gereği ise `POLICY`.
   - Ölçülmemiş vaka **uydurulmaz**.
2. **Repository-valid references:** repoda bulunmayan iki upstream artifact artık normatif repository dependency değildir.
   Bunlar `SOURCE / PENDING ADOPTION` olarak ayrılmış; gerekli minimum çalışma kuralları bu SOP içinde self-contained gate olarak tanımlanmıştır.
3. **Daily operating card:** uzun SOP'un günlük uygulanabilirliği için tek sayfalık companion card tanımlanmıştır.
4. **K4 genişletildi:** yalnız seam/contract değil; UI semantics, payload, state, timing ve diğer çok taraflı düzeltmeleri kapsar.
5. **Agent usage guide adoption gap:** parallel Agent Lane / worktree konusu için guide'a eklenecek minimum cross-reference tanımlanmıştır.
6. **Repository adoption gate:** SOP'un canonical ilanından önce referans-resolvability ve companion-link kontrolü eklendi.

### 0.3 v2.2'de eklenen ana kontroller

v2.1 korunmuş ve Agent Lane terminolojisi canonical hale getirilmiştir:

1. **Agent Lane**, CONTROL TOWER tarafından yönetilen kalıcı AI execution workspace/chat için canonical terim olarak tanımlandı.
2. **Build Lane** ile **Agent Lane** ayrıştırıldı:
   - Build Lane = dependency/sequence planındaki mantıksal iş akışı,
   - Agent Lane = Work Package'ların yürütüldüğü kontrollü chat/workspace.
3. Agent Lane türleri tanımlandı: `DEV`, `INS`, `VER`, `INT`.
4. Agent Lane naming convention ve Agent Lane ID standardı eklendi.
5. Work Package metadata'ya **Agent Lane ID / Type** eklendi.
6. Parallel development kuralları **writer Agent Lane** ve **single-writer shared seam** mantığıyla netleştirildi.
7. Agent Lane reuse / new-lane trigger / stale-context kuralları eklendi.
8. CONTROL TOWER dashboard ve canonical templates Agent Lane alanlarıyla güncellendi.
9. Ana workflow `CONTROL TOWER → Agent Lanes → Verification/Integration → CONTROL TOWER` olarak standardize edildi.

### 0.4 v2.1'de eklenen ana kontroller

v2.0 korunmuş ve aşağıdaki boşluklar kapatılmıştır:

1. **Authority order tek liste olmaktan çıkarıldı; karar türüne göre authority matrix eklendi.**
2. **Definition of Ready (DoR)** ve **Definition of Done (DoD)** tanımlandı.
3. Work package için **tam yaşam döngüsü / state machine** eklendi.
4. **Prompt versioning, supersede, cancel ve rework** kuralları eklendi.
5. **Git / branch / base HEAD / dirty-worktree preflight gate** netleştirildi.
6. Parallel development için **single-writer shared-contract** ve WIP kuralları eklendi.
7. **Risk / blast-radius sınıflaması** ve review derinliği eklendi.
8. **Evidence sufficiency modeli** eklendi; agent raporu ile runtime kanıtı ayrıştırıldı.
9. Cross-module işler için bağımsız **Integration Gate** eklendi.
10. **Work Package Done ≠ Module Done ≠ Capability Done ≠ Released** ayrımı eklendi.
11. Migration, rollback, deprecation, data classification, observability, accessibility, finance precision gibi **conditional gates** eklendi.
12. Waiver/exception için **owner + reason + compensating control + expiry/review** zorunluluğu eklendi.
13. **Reconciliation cadence**, drift kontrolü ve `/reconcile-records` kullanımı netleştirildi.
14. Agent Lane handoff ve stale-context riskine karşı **authoritative handoff packet** eklendi.
15. Repeated rework için **architecture reassessment trigger** eklendi.

---

# 1. Amaç ve temel işletim ilkesi

CONTROL TOWER, ERP-vNext geliştirme sisteminin **delivery control plane** rolüdür.

Amaç:

- canonical mimariyi korumak,
- code/runtime reality ile kayıtları uzlaştırmak,
- hangi işin ne zaman ve hangi sırayla yapılacağını belirlemek,
- güvenli paralelliği açmak,
- yürütülebilir prompt/work package üretmek,
- ajan çıktısını bağımsız doğrulamak,
- entegrasyon ve kapanış kapılarını yönetmek,
- kayıtları ve sonraki planı aynı tur içinde güncellemek.

Ana ilke:

```text
Canonical authority
    + measured code/runtime reality
    + dependency-aware planning
    + bounded execution
    + independent verification
    + integration evidence
    = accepted delivery
```

---

# 2. Referans geçerliliği ve normatif kaynaklar

Bu SOP başka bir canonical engineering standardını kopyalayıp ikinci authority haline getirmez.
Ancak **yalnız repository içinde çözülebilen kaynaklar normatif runtime authority olarak çağrılabilir**.

## 2.1 Repository-resolvable normatif kaynaklar

CONTROL TOWER iş türüne göre aşağıdaki repository kaynaklarını okur ve uygular:

```text
AGENTS.md
.antigravity/rules/**
.antigravity/workflows/**
.antigravity/PROMPT-GUIDE.md
docs/agent-usage-guide.md
execution/portfolio/master-development-plan.md
execution/delivery/platform-delivery-board.md
execution/portfolio/delivery-capability-packs/**
execution/domains/{domain}/domain-config.md
execution/domains/{domain}/module-packs/**
execution/registries/module-id-registry.md
```

Bir normatif path repository'de çözülemiyorsa:

```text
REFERENCE_NOT_RESOLVABLE
→ o kaynağa dayanarak gate çalıştırma
→ source'u ya repo'ya canonical olarak adopt et
→ ya SOP içindeki self-contained gate'i kullan
```

## 2.2 Upstream / project-source artifacts — repository authority DEĞİL

Aşağıdaki iki artifact CONTROL TOWER tasarımının kaynaklarıdır; ancak repository içinde bulunmadıkları
sürece ajanlara “gidip bunu oku” diye verilemez:

```text
RUNTIME VERTICAL-SLICE DEVELOPMENT RULES — MANDATORY
Golden-Flow Prompt Guideline — v2.2
```

Durumları:

```text
SOURCE / PENDING REPOSITORY ADOPTION
NOT a repository-resolvable authority
```

Bu nedenle v2.3:

- Profile A/B/C seçim mantığını §6.1 içinde tanımlar,
- minimum runtime vertical-slice gate'ini §18.0 içinde tanımlar,
- UI pattern gate'ini §17.3 içinde self-contained tutar,
- external artifact bulunmadığı için implementation'ı BLOCKED yapmaz.

İleride bu iki artifact repository'ye canonical olarak adopt edilirse:
1. approved target path belirlenir,
2. `AGENTS.md` / ilgili guide'da authority link'i eklenir,
3. SOP'taki temporary self-contained özetler link-only hale getirilir,
4. duplicate rule drift yaratılmaz.

> **Not:** `.antigravity/**` protected governance alanıdır; yeni canonical rule dosyası eklemek ilgili repository approval sürecine tabidir.

## 2.3 Concern-specific conflict

Çelişki halinde §4'teki concern-specific authority matrix uygulanır.
Code reality ve agent report authority değil, **evidence**'dır.

## 2.4 Daily operating artifact

Bu SOP'un günlük yürütme companion'ı:

```text
docs/control-tower-operating-card.md
```

Operating Card hızlı gate/checklist içindir; yeni authority veya yeni rule yaratmaz.
Çelişki halinde **bu SOP kazanır**.


---

# 3. CONTROL TOWER nedir, ne değildir

CONTROL TOWER bir kişi değil, bir **yürütme, karar, doğrulama ve kayıt rolüdür**.

| CONTROL TOWER yapar | CONTROL TOWER yapmaz |
|---|---|
| Code/runtime state'i ölçer | Agent raporunu kanıt saymaz |
| Authority kaynaklarını uzlaştırır | Agent Lane chat geçmişini canonical source saymaz |
| Capability/build plan yönetir | Module Pack yerine kendi scope'unu icat etmez |
| Dependency ve parallel-safe gate hesaplar | Shared contract üzerinde iki kontrolsüz writer açmaz |
| Work package üretir ve dispatch eder | “Geliştir” gibi sınırsız prompt göndermez |
| Agent verdict'ini bağımsız doğrular | Agent PASS'i otomatik ACCEPTED yapmaz |
| Cross-module integration gate çalıştırır | İzole test PASS'ini capability completion saymaz |
| Decision/waiver/blocker kaydeder | Sessiz scope/contract override yapmaz |
| Replan + next prompts üretir | Kapanıştan sonra planı eski bırakmaz |

## 3.1 Break-glass mikro düzeltme

CONTROL TOWER normal koşulda implementasyon kodu yazmaz.

Yalnızca aşağıdakilerin tamamı sağlanırsa **CT break-glass micro-fix** yapılabilir:

- mekanik ve dar değişiklik,
- architecture/ownership/contract kararı yok,
- başka module sınırına geçmiyor,
- yeni runtime capability tasarlamıyor,
- kusurun kırmızı kanıtı var,
- düzeltme testiyle birlikte,
- git scope'u izole,
- kayıt ve verification aynı turda.

Aksi halde ayrı work package üretilir.

## 3.2 Agent Lane — canonical execution workspace

**Agent Lane**, CONTROL TOWER tarafından yönetilen ve bir veya daha fazla versioned Work Package'ı
aynı bounded execution context içinde yürüten kalıcı AI development/inspection/verification/integration
chat veya workspace'idir.

Agent Lane bir Work Package değildir.

```text
Build Lane
    = plan içindeki mantıksal dependency / sequencing stream

Agent Lane
    = AI chat/workspace execution stream

Work Package
    = Agent Lane'e dispatch edilen tekil executable contract
```

### 3.2.1 Agent Lane türleri

| Type | Prefix | Amaç |
|---|---|---|
| Development Agent Lane | `DEV` | Bounded implementation work |
| Inspection Agent Lane | `INS` | Read-only readiness / code-reality inspection |
| Verification Agent Lane | `VER` | Independent verification |
| Integration Agent Lane | `INT` | Cross-module integrated runtime verification |

### 3.2.2 Naming convention

Canonical chat/workspace adı:

```text
{TYPE} — {Capability/Domain} — {Module or Scope} — {Lane Purpose}
```

Örnek:

```text
DEV — PV — MOD-0028 — Evidence
DEV — PV — MOD-0040 — Correlation
INS — PV — Capability Readiness
VER — PV — Runtime Verification
INT — PV — Capability Integration
```

Agent Lane ID, display name'den ayrı ve stable olmalıdır:

```text
AL-PV-MOD0028-EVIDENCE
AL-PV-MOD0040-CORRELATION
AL-PV-VERIFY
AL-PV-INTEGRATION
```

### 3.2.3 Agent Lane reuse kuralı

Aynı Agent Lane şu koşullarda ardışık Work Package'ları yürütebilir:

- aynı owner module veya aynı bounded capability scope,
- aynı branch/worktree strategy,
- aynı authority set,
- contract context materially değişmemiş,
- chat context stale/contaminated değil.

Aşağıdaki durumlarda **yeni Agent Lane** açılır:

- owner module değişirse,
- branch/worktree değişirse,
- protected-path/authority boundary materially değişirse,
- aynı Agent Lane'in context'i stale veya unrelated work ile contaminated olursa,
- independent verification için implementer'dan ayrışma gerekiyorsa,
- cross-module integration ayrı runtime context gerektiriyorsa.

### 3.2.4 Agent Lane concurrency kuralı

Bir Agent Lane aynı anda birden fazla **overlapping writer Work Package** yürütemez.

Parallel work gerekiyorsa:

```text
CONTROL TOWER
   ├─ Agent Lane A → WP-A
   ├─ Agent Lane B → WP-B
   └─ Agent Lane C → WP-C
```

ve her Agent Lane için branch/worktree, scope ve shared-seam ownership açık olmalıdır.

---

# 4. Authority modeli — karar türüne göre

v2.0'daki tek yönlü authority listesi yeterli değildir. ERP-vNext'te authority **karar türüne göre** değişir.

## 4.1 Authority matrix

| Concern | Birincil authority | İkincil / execution context | CONTROL TOWER kuralı |
|---|---|---|---|
| Canonical `MOD-xxxx` kimliği / adı | Blueprint + Module ID canonicalization policy / registry gate | Module Pack alias/reference | Yeni ID icat edilmez |
| Module-local scope / owned objects / AC / protected paths | **Approved Module Pack** | Domain Config | CT scope'u genişletemez |
| Domain boundary / repo scope | **Domain Config** | AGENTS.md | Module Pack domain dışına çıkamaz |
| Repo-wide execution / protected paths / ports / git | **AGENTS.md** | `.antigravity` rules | CT bunları bypass edemez |
| Global engineering pattern | `.antigravity/rules/**` | workflow / prompt guide | Daha spesifik rule yoksa uygulanır |
| Capability sequencing / cross-module dependency | Approved DCP / capability build plan | Module Packs | DCP module ownership'u override edemez |
| Product priority / business decision | Product owner / approved portfolio plan | CT recommendation | CT risk ile itiraz eder, kararı kaydeder |
| Code reality | Repository + runtime evidence | Reports | **Evidence'dir, authority değildir** |
| Agent report | Agent output | Test/runtime evidence | **Claim'dir, authority değildir** |

## 4.2 Conflict protocol

Bir conflict tespit edilirse:

```text
MEASURE
→ classify concern
→ identify authority
→ document conflict
→ options + trade-offs
→ decision / approval
→ update canonical record
→ only then generate implementation work
```

**Fail-closed:** ownership, canonical identity, protected path veya contract conflict açıkken implementasyon başlamaz.

---

# 5. Sistem-of-record ve artifact sorumluluğu

Agent Lane içindeki chat/workspace bir **execution session**'dır; system-of-record değildir.

## 5.1 Artifact matrix

| Bilgi | System of Record |
|---|---|
| Canonical module ID/name | Blueprint / module-id registry policy |
| Domain boundary | Domain Config |
| Module ownership/scope/AC | Module Pack |
| Cross-module capability plan | DCP / capability build plan |
| Current active delivery status | Platform Delivery Board |
| Portfolio sequencing | Master Development Plan |
| Cross-module seam | Seam register / contract artifact |
| Architectural decision | ADR / decision log |
| Prompt execution evidence | Audit/report artifact |
| Git history | Repository |
| Runtime truth | Measured runtime evidence |

## 5.2 Drift rule

Bir kayıt code/runtime reality'den geri kaldıysa:

- code otomatik authority olmaz,
- kayıt otomatik doğru kabul edilmez,
- drift kaydı açılır,
- owner/authority üzerinden reconciliation yapılır.

---

# 6. İş sınıfları ve doğru entry point

CONTROL TOWER raw prompt üretmeden önce **iş türünü** belirler.

| İhtiyaç | Entry point / workflow |
|---|---|
| Yeni module pack | `module-pack-author` / `/prepare-module-pack` |
| Çok modüllü cross-cutting capability | `/prepare-capability-pack` |
| Approved pack ile uçtan uca implementation | `@orchestrator` / `/add-module` |
| Backend-only contract | `backend-architect` / ilgili workflow |
| Frontend-only bounded work | `frontend-ui-ux` |
| Gateway route | `integration-agent` |
| L10n | `l10n-agent` |
| Security/RBAC review | `security-agent` |
| Test/quality gate | `testing-agent` |
| Read-only audit | `/read-only-audit` / `read-only-auditor` |
| Ambiguous bug/root-cause | `debugger` veya Profile C inspection |
| Independent verification (VER lane) | `read-only-auditor` / `/read-only-audit` — implementer lane'inden ayrı; verdict veren lane yazma yapmaz. Vacuity kanıtı (§24.2) gerekiyorsa test yazımı ayrı `testing-agent` WP'sidir |
| Cross-module integration (INT lane) | CONTROL TOWER runtime turu; root-cause gerekirse `debugger`. Ayrı runtime context, §25 freshness gate zorunlu |

`read-only-auditor` yalnız `/read-only-audit` altında çalışır ve yazma araçları yoktur; bu, VER lane'in
"verdict veren yazmaz" gereğini araç düzeyinde karşılar. Doğrulama sırasında kod değişmesi gerekiyorsa
o bir REWORK WP'sidir, VER lane'in işi değildir.

## 6.1 Work Package execution profili

Her Work Package aşağıdaki üç profilden **tam birini** taşır. Bu seçim repository dışındaki bir
dokümana bağımlı değildir.

| Profil | Kullanım | Minimum flow |
|---|---|---|
| **Profile A — UI / state-changing feature** | create/edit/save/import/approve/publish/delete/submit gibi kullanıcı veya state değiştiren işler | actor → interaction → server validation/auth → persist/side effect → reload/result → audit/evidence |
| **Profile B — Backend / contract** | API, event, adapter, shared service, master/reference-data contract | caller → schema/auth validation → handler/side effect → persistence/event → response/error behavior |
| **Profile C — Inspection / read-only** | readiness audit, route/contract inventory, code-reality ölçümü, no-change review | inspect → evidence → gaps/blockers → recommended next work; **no writes** |

Profile seçimi hatalıysa prompt dispatch edilmez.

**Profile A/B için:** no-shell, contract-blocker, persistence, RBAC/tenant, consistency ve validation gate'leri uygulanır.  
**Profile C için:** read-only constraints ve mutation-free evidence uygulanır.

---

# 7. Work Package yaşam döngüsü

Her work package tekil kimliğe ve explicit state'e sahiptir.

```text
DISCOVERED
   ↓
INSPECTION_REQUIRED
   ↓
INSPECTED
   ↓
PLANNED
   ↓
READY
   ↓
DISPATCHED
   ↓
IN_PROGRESS
   ↓
REPORTED
   ↓
VERIFYING
   ↓
VERIFIED
   ↓
INTEGRATION_READY   (gerekiyorsa)
   ↓
INTEGRATED          (gerekiyorsa)
   ↓
ACCEPTED
```

Exception states:

```text
BLOCKED
REWORK
DEFERRED
SUPERSEDED
CANCELLED
FAILED
```

## 7.1 State transition kuralı

State yalnızca **kanıtlanmış gate** ile ilerler.

Örnek:

- `READY` → DoR PASS
- `REPORTED` → structured agent report alındı
- `VERIFIED` → independent verification PASS
- `INTEGRATED` → cross-module integration gate PASS
- `ACCEPTED` → CT acceptance + required records updated

---

# 8. Definition of Ready — work package dispatch gate

Bir implementation work package ancak aşağıdakiler PASS ise `READY` olabilir.

## 8.1 Mandatory DoR

- [ ] Canonical module identity geçerli.
- [ ] Owner module / owned objects açık.
- [ ] Module Pack gerekli ise mevcut ve `approved` / `ready-for-dev`.
- [ ] Cross-module iş ise DCP/capability boundary açık.
- [ ] Required contract'lar mevcut ve ambiguous değil.
- [ ] Dependency gate'leri `SATISFIED` veya kayıtlı `WAIVED`.
- [ ] Allowed paths / protected paths açık.
- [ ] Target branch/worktree/base HEAD açık.
- [ ] Dirty-worktree inventory biliniyor.
- [ ] Prompt profile seçildi.
- [ ] Golden/contract/inspection flow açık.
- [ ] Acceptance criteria ölçülebilir.
- [ ] Required persistence level açık.
- [ ] Required security/audit/evidence gate açık.
- [ ] Parallel-safety değerlendirmesi tamam.
- [ ] Risk / blast-radius sınıfı atanmış.
- [ ] Gerekli conditional gates doldurulmuş.
- [ ] Entry point / target agent §6'ya göre seçildi; o ajanın §17.4 zorunlu giriş alanları prompt'ta dolu.
- [ ] Paste-ready agent prompt bloğu (§36.1) yazıldı; `Repository/Branch/HEAD` ölçülmüş değer taşıyor.

Herhangi biri kritik şekilde eksikse:

```text
NOT READY → implementation dispatch yok
```

---

# 9. Risk ve blast-radius sınıflaması

Her work package risk sınıfı taşır.

| Risk | Tipik örnek | Ek kontrol |
|---|---|---|
| LOW | Dar UI text, isolated test, non-shared local behavior | Standard review |
| MEDIUM | Module CRUD slice, gateway route, permission extension | Runtime + integration smoke |
| HIGH | Shared contract, schema/migration, auth, audit, evidence, financial/compliance, cross-module state change | Independent reviewer + rollback/compatibility + integration gate |

## 9.1 Otomatik HIGH sinyalleri

Aşağıdakilerden biri varsa default `HIGH`:

- shared contract/event schema,
- database migration/backfill,
- authorization/authentication,
- tenant isolation,
- audit/evidence foundation,
- financial amount/calculation,
- compliance approval,
- shared gateway/global route behavior,
- cross-module write,
- data deletion/retention,
- deprecation/removal.

---

# 10. Ana CONTROL TOWER workflow

```text
1. INTAKE / CLASSIFY
2. INSPECT
3. RECONCILE
4. DECIDE
5. PLAN
6. DEPENDENCY + PARALLELIZATION GATE
7. PACKAGE / PROMPT
8. DISPATCH + IMPLEMENT
9. DEVELOPER VALIDATION + REPORT
10. INDEPENDENT VERIFICATION
11. INTEGRATION GATE (gerekiyorsa)
12. ACCEPT / REWORK / BLOCK
13. RECORD + REPLAN + NEXT PROMPTS
```

---

# 11. Adım 1 — INTAKE / CLASSIFY

CONTROL TOWER önce talebi sınıflar:

- capability / module / feature / fix / audit / contract / migration,
- single-module vs cross-module,
- state-changing vs read-only,
- runtime vs documentation/governance,
- risk class,
- required authority artifacts.

Çıktı:

```text
Task class:
Owner candidate:
Required pack:
Prompt profile:
Risk:
Inspection required: yes/no
Entry point (§6):
Target agent / command:
```

Entry point ve target agent **sınıflamanın çıktısıdır**, dispatch anının kararı değildir.
Bu iki satır boşken plan ve prompt üretimine geçilmez.

Belirsizlik varsa implementation yerine inspection başlar.

---

# 12. Adım 2 — INSPECT

Amaç: rapora değil repository/runtime reality'ye dayanmak.

Minimum inspection scope:

- module identity,
- owner/scope,
- pack/status,
- routes/controllers,
- DTO/contracts/events,
- entities/collections/migrations,
- handlers/services,
- frontend,
- gateway,
- RBAC,
- audit/evidence,
- tenant isolation,
- tests,
- relevant reports,
- current branch/HEAD/worktree,
- runtime availability.

## 12.1 Inspection budget

Profile C işlerde soft budget yazılabilir:

```text
Max files:
Max time:
Early-stop blocker:
No-write constraint:
```

Blocker kanıtlandıysa gereksiz geniş inspection yapılmaz.

## 12.2 Inspection output

| Concern | Expected | Code Reality | Runtime Reality | Evidence | Gap |
|---|---|---|---|---|---|
| Identity | | | | | |
| Ownership | | | | | |
| Contract | | | | | |
| Persistence | | | | | |
| UI | | | | | |
| RBAC/Tenant | | | | | |
| Audit/Evidence | | | | | |
| Tests | | | | | |

---

# 13. Adım 3 — RECONCILE

Reconciliation zinciri:

```text
Identity authority
→ DCP / capability plan
→ Domain Config
→ Module Pack
→ Build Plan / Backlog
→ Code
→ Runtime
```

## 13.1 Reconciliation gate

- ID/name valid mi?
- owner doğru mu?
- duplicate ownership var mı?
- pack scope code ile uyuşuyor mu?
- dependency declarations gerçek mi?
- contract version tüketicilerle uyumlu mu?
- shipped code kayıtlarda görünür mü?
- records shipped olmayan şeyi `done` gösteriyor mu?
- runtime stale binary/shell riskli mi?

Sonuç:

```text
ALIGNED
DRIFT_FOUND
BLOCKED
DECISION_REQUIRED
```

---

# 14. Adım 4 — DECIDE

Decision Brief minimum:

```text
Decision ID:
Context:
Measured evidence:
Authority concern:
Options:
Trade-offs:
Recommendation:
Selected option:
Rejected alternatives:
Owner/approver:
Affected boundaries:
Affected contracts:
Required record changes:
Effective version/date:
```

**Kural:** önemli bir karar yalnızca chat'te kalamaz.

---

# 15. Adım 5 — PLAN

Capability/build plan minimum kolonları:

| Seq | WP ID | Module | Build Lane | Agent Lane | Depends On | Gate | Parallel-Safe | Risk | Readiness | Status |
|---:|---|---|---|---|---|---|---|---|---|---|

Readiness yüzdesi varsa sadece management telemetry'dir; gate yerine geçmez.

---

# 16. Adım 6 — DEPENDENCY + PARALLELIZATION GATE

## 16.1 Dependency gate

```text
Dependency:
Owner:
Gate condition:
State: OPEN | READY | BLOCKED | SATISFIED | WAIVED
Evidence:
```

## 16.2 Waiver standardı

Her waiver:

```text
Waiver ID:
Scope:
Reason:
Owner/approver:
Compensating control:
Risk accepted:
Review/expiry:
Exit condition:
```

Expiry/review'suz waiver kalıcı normal davranışa dönüşemez.

## 16.3 Parallel-safe analizi

Karşılaştır:

- module/SoR ownership,
- touched files,
- shared service registration,
- API/event contracts,
- DB migrations/indexes,
- gateway route family,
- permission constants/policies,
- reference/seed data,
- frontend shared shell,
- branch/worktree/base,
- expected integration order.

Çıktı:

```text
Parallel-safe with:
Not parallel-safe with:
Shared seam:
Merge risk:
Required integration order:
```

## 16.4 Single-writer kuralı

Aynı anda:

- aynı shared contract,
- aynı migration chain,
- aynı permission definition,
- aynı global route family,
- aynı shared shell/core registration

üzerinde **birden fazla writer Agent Lane** açılmaz.

Consumer Agent Lane'ler yalnızca contract version freeze edildikten sonra paralel çalışabilir.

## 16.5 WIP kuralı

Default:

- bir worktree/module üzerinde aynı shared surface için **1 active writer Agent Lane**,
- paralel işler ayrı Agent Lane + ayrı worktree + disjoint scope ile,
- WIP artırımı CT tarafından risk gerekçesiyle yapılır.

Paralellik varsa en az iki executable prompt üretmek hedeflenir; güvenli değilse sırf paralellik için iş bölünmez.

---

# 17. Adım 7 — WORK PACKAGE / PROMPT üret

## 17.1 Zorunlu metadata

```text
Work Package ID:
Prompt ID:
Prompt Version:

Capability Block:
Module:
Build Sequence:
Build Lane:
Agent Lane ID:
Agent Lane Type: DEV | INS | VER | INT
Target Agent / Entry Point:   # §6 tablosundan; ör. `@orchestrator` + `/add-module`
Risk Class:

Target Branch:
Expected Base HEAD:
Worktree:
Dirty-worktree baseline:

Depends On:
Parallel-Safe With:
Integration Order:

Authority Sources:
- Identity authority:
- DCP:
- Domain Config:
- Module Pack:
- Related inspection/ADR:

Allowed Paths:
Protected Paths:

Golden-Flow Profile:
A | B | C
```

## 17.2 Prompt çekirdeği

```text
NE
NEDEN
NASIL
YAPMA
DOĞRULA
```

Ek olarak mutlaka:

```text
Objective / runtime outcome
Ownership & boundaries
Preconditions
Persistence level
Consistency expectation
Acceptance criteria
Validation plan
Output contract
Failure protocol
```

## 17.3 UI pattern gate — self-contained

Data-writing UI varsa pattern görev şekline göre seçilir:

| Pattern | Kullanım |
|---|---|
| **Wizard** | Infrequent, sequentially-dependent setup/submission; terminal commit |
| **Single detail / section** | Repeated view/edit; non-linear, revisable record |
| **Form + summary** | Kısa, tek ekran structured input; tek commit |
| **Table / bulk** | Expert operator, high-volume scan/create/process |
| **Mixed pattern** | Boundaries açıkça ayrılmış birden fazla pattern |

Prompt içinde:

```text
Pattern:
Justification:
Pattern boundary:   # mixed ise
```

zorunludur.

Pattern gerekçesi task shape ile uyuşmuyorsa:

```text
BLOCKED — pattern mismatch
```

Wizard default değildir; pattern kullanım sıklığı ve interaction shape'e göre seçilir.

## 17.4 Hedef ajan giriş alanları — prompt alıcısına göre şekillenir

Work Package metadata'sı CONTROL TOWER'ın kaydıdır; **prompt ise hedef ajanın giriş kapısına göre yazılır.**
Her ajanın kendi zorunlu bağlam okuması ve sıfır-inisiyatif kuralı vardır: eksik alanla giden prompt iş
üretmez, geri soru üretir. Aşağıdaki alanlar §36 metadata'sına ek değil, **paste-ready prompt bloğunun içinde**
açıkça yazılır.

| Target agent / entry point | Prompt'ta zorunlu alanlar | Neden (ajan sözleşmesi) |
|---|---|---|
| `@orchestrator` + `/add-module` | Module pack **yolu** + status (`approved` / `ready-for-dev`) · domain · servis · `shell` · `golden_reference` (slim/compact) + `form_field_count` · branch · beklenen alt-ajan zinciri | Aşama 0 bağlam kapısı bunları okumadan alt ajan tetiklemez; eksikse Sokratik soruya döner |
| `module-pack-author` / `/prepare-module-pack` | Modül adı + tek cümle amaç · domain · servis · shell · form alan sayısı + isimleri · DataTable var mı · entity base + gerekçe · bilinen iş kuralları/bağımlılıklar | Pack `draft` üretir; alan sayısı Slim/Compact kararının girdisidir |
| `backend-architect` | Hedef servis + proje yolu · aggregate/entity + alanlar · command/query listesi · validator kuralları · permission key'leri · `Response<T>` + `CustomBaseController` beklentisi | Validator yazılmadan handler yazmaz; DTO/alan uydurması yasak |
| `frontend-ui-ux` | Area + module adı · `golden_reference` slim/compact · shell/layout adı · kolon + filtre listesi (enum alanlar Select2 chip mi) · L10n key seti · teslim öncesi `verify_datatable_page.py --area ... --module ... --reference slim/compact` | Şablonu birebir kopyalar; referans/layout belirsizse üretim durur |
| `l10n-agent` | Modül türü (Platform 2 dil / Tenant 7 dil) · resx dosya yolları · key listesi + **her dil için gerçek metin** · SharedResource'a mı modül resx'ine mi gideceği | Placeholder/İngilizce kopya yasak; çeviri bilinmiyorsa senden ister |
| `integration-agent` | Controller/route family · upstream + downstream path · hedef servis portu (AGENTS.md §3) · header geçişi (`Authorization`, `X-Tenant-Id`) · hangi ocelot dosyası | Port/rota uydurması yasak; kayıtlı port şeması dışına çıkamaz |
| `security-agent` | Permission key'leri + policy · actor tipi (platform-admin / tenant_user) · tenant izolasyon yüzeyi · denetlenecek endpoint listesi | Mevcut `[HasPermission]` + JWT modeline sadık kalır, yeni model kurmaz |
| `testing-agent` | Test edilecek handler/akış · beklenen davranış (PRD/AC) · soft-delete + TenantId izolasyon senaryoları · test projesi yolu | İş kuralı uydurmaz; AC yoksa test yazamaz |
| `read-only-auditor` / `/read-only-audit` | Denetim modu (worktree-read-only / strict) · kapsam (path/modül) · her bulgu için `path:line` kanıt zorunluluğu · **düzeltme yok** ifadesi | Yazma aracı yoktur; düzeltme talebi bu ajana verilemez |
| `debugger` | Semptom + tekrar üretme adımları · katman (frontend/gateway/auth/service) · log/korelasyon kanıtı · dokunulmayacak alan | Katmanlı izolasyonla kök neden arar, semptom yamamaz |

**Kural:** hedef ajanın zorunlu alanlarından biri prompt'ta yoksa WP `READY` olamaz (§8.1).
Alan eksikliğini ajanın sana soru sorarak kapatması, dispatch sonrası scope müzakeresidir ve K16'ya aykırıdır.

Ajanların araç ve yazma sınırı biliniyor (`.antigravity/agents/**`). Seçilen ajanın yazma alanı ile WP'nin
`Allowed Paths` alanı çelişiyorsa — ör. resx yolu `backend-architect`'e verilmişse — prompt dispatch edilmez:

```text
BLOCKED — agent/scope mismatch
```

`@orchestrator` tek bir DEV lane içinde birden çok uzman ajana dağıtım yapar. Bu dağıtım §16.4 single-writer
kuralının **içinde** kalır: lane tektir, shared seam sahibi tektir. Aynı shared seam'e iki farklı **lane**
açmak yasaktır; bir lane'in kendi içinde sıralı uzman ajan çalıştırması yasak değildir.

---

# 18. Conditional gates

## 18.0 Minimum runtime vertical-slice gate — repository-valid local minimum

State-changing bir Work Package aşağıdaki minimumları sağlamadan `READY` olamaz:

- **Golden flow:** actor, trigger, interaction sequence, expected response, success result.
- **No-shell:** operational-looking control gerçek save/load/update davranışı olmadan yapılamaz.
- **Contract blocker:** eksik API/DTO/event/service/permission/evidence contract uydurulmaz.
- **Persistence:** `L1 | L2 | L3` açıkça seçilir ve test edilir; compliance/financial/audit/evidence/final state default `L3`.
- **Concurrency:** editable record için version/etag; conflict davranışı açık.
- **Idempotency:** create/submit/commit replay duplicate side effect yaratmaz.
- **Validation:** client + authoritative server validation.
- **RBAC/Tenant:** server-side permission + tenant isolation.
- **Data classification:** confidential/PII/secret değerler log/trace/audit'te açık yazılmaz.
- **Audit/Evidence:** kritik mutation audit edilir; regulated flow shared evidence service kullanır.
- **Consistency:** `atomic | compensating | partial-with-marker`.
- **UX states:** loading, empty, validation error, denied, save-failed, conflict — applicable olduğunda.
- **Observability:** correlation ID + required logs/metrics/traces + redaction.
- **Do-not-change:** protected/unrelated scope açık.

Bu §18.0, repository dışında bulunan upstream runtime-rule artifact'ının yerine geçen **minimum CT gate**'idir;
full engineering detail için repository-resolvable `AGENTS.md` ve `.antigravity/rules/**` uygulanmaya devam eder.


Her prompt'a her bölümü zorla ekleme. İş türüne göre gate aç.

## 18.1 Security / privacy

State-changing veya sensitive flow:

- data classification,
- PII/confidential/secret fields,
- masking/redaction,
- server-side auth,
- tenant isolation,
- secret/token handling.

## 18.2 Observability

Backend/state-changing flow:

- correlation ID,
- logs,
- metrics,
- traces,
- audit event names,
- error event names,
- redaction.

## 18.3 Data / migration

Persistent schema/reference-data değişiyorsa:

- schema change,
- migration,
- backfill,
- seed,
- rollback,
- test cleanup,
- retention impact.

## 18.4 Deprecation / sunset

Contract/behavior değişiyorsa:

- old behavior,
- consumers,
- compatibility,
- dual-run window,
- removal version/date,
- data migration path.

## 18.5 NFR / performance

Dashboard, list, import, batch, calculation, high-volume flow:

- p95 target,
- result cap,
- batch limit,
- page-load target,
- pagination/virtualization.

## 18.6 Accessibility

UI flow:

- keyboard,
- focus,
- labels/ARIA,
- loading/empty/error/permission/conflict states.

## 18.7 Export/reporting

Export/snapshot varsa:

- format,
- permission,
- immutability,
- filename/metadata,
- sensitive-data rules.

## 18.8 Finance-domain precision

CF/AP/AR/payment/bank/tax/payroll/reconciliation:

- decimal, never float,
- currency source,
- precision,
- rounding,
- timezone,
- week boundary,
- inflow/outflow model,
- FX scope/rate/as-of,
- immutable published snapshots where applicable.

---

# 19. Prompt lifecycle ve versioning

Prompt metni dispatch sonrası sessizce değiştirilmez.

## 19.1 Version rule

```text
v1.0  first executable contract
v1.1  clarification; intended outcome unchanged
v2.0  material scope/contract/acceptance change
```

Material change çoğu durumda yeni Work Package ID gerektirir.

## 19.2 Rework identity

Rework original work package'a bağlı ama ayrı execution olur:

```text
WP-...-R1
WP-...-R2
```

Original report overwrite edilmez.

## 19.3 Supersede

Yeni prompt eski prompt'u geçersiz kılıyorsa:

```text
Old state: SUPERSEDED
Superseded by: <new WP/Prompt ID>
Reason:
```

SUPERSEDED prompt çalıştırılmaz.

## 19.4 Mid-flight scope change

`IN_PROGRESS` işin scope'u materially değişirse:

```text
STOP
→ classify change
→ update authority/plan
→ supersede or new WP
→ redispatch
```

Ajan kendi scope'unu genişletmez.

---

# 20. Dispatch preflight — git ve environment gate

Dispatch öncesi veya ajan başlangıcında:

```text
Repository path:
Branch:
HEAD:
Expected base HEAD:
Worktree:
Staged files:
Unstaged relevant files:
Untracked relevant files:
Unrelated dirty inventory:
```

## 20.1 Stop conditions

Aşağıdakilerde fail-closed:

- yanlış repository/worktree,
- yanlış branch,
- base HEAD mismatch ve task buna tolerans tanımlamıyor,
- overlapping dirty files,
- protected path değişikliği gerekiyor ama approval yok,
- dependency unsatisfied,
- pack draft,
- missing/ambiguous contract,
- ownership conflict,
- unplanned migration,
- required pattern justification yok.

Git operasyonları repo `GIT-002` kurallarına tabidir.

**Commit ≠ acceptance.**

---

# 21. Adım 8 — AGENT LANE DEVELOPMENT EXECUTION

Development Agent Lane / kod ajanı:

```text
read authority packet
→ preflight
→ implement bounded scope
→ self-validate
→ structured report
```

Ajan:

- module identity icat etmez,
- ownership değiştirmez,
- missing contract uydurmaz,
- protected path'a izinsiz dokunmaz,
- unrelated cleanup yapmaz,
- kendi PASS verdict'ini CT acceptance saymaz.

## 21.1 Handoff packet

Agent Lane'e yalnız prompt değil, authoritative handoff verilir:

```text
WP/Prompt ID
Agent Lane ID / Type
Authority source paths
Expected branch/HEAD/worktree
Scope/protected paths
Dependencies
Frozen contract versions
Acceptance
Required evidence
```

Agent Lane chat geçmişi handoff'ın yerine geçmez.

---

# 22. Adım 9 — DEVELOPER VALIDATION + structured report

Minimum report:

```text
Agent Verdict:
Branch / HEAD:
Worktree status:

Changed files:
Golden/Contract flow:
Sub-flows:
Failure paths:
Tests:
Persistence evidence:
Security/RBAC/Tenant evidence:
Audit/Evidence:
Observability:
Migration/Rollback:
Decisions:
Blockers:
Known gaps:
Out-of-scope changes: none / list
```

Report evidence pointer içermeli; “works” tek başına kanıt değildir.

---

# 23. Evidence sufficiency modeli

| Level | Kanıt | Kullanım |
|---|---|---|
| E0 | Agent narrative/claim | Acceptance için yetersiz |
| E1 | Static code/config/file inspection | Readiness/inspection |
| E2 | Build/unit/contract test | Implementation confidence |
| E3 | Runtime API/UI smoke | Runtime behavior |
| E4 | Persistence + RBAC/tenant + audit/evidence + failure paths | State-changing acceptance |
| E5 | Cross-module integrated runtime + restart/cold-start/compatibility where required | Capability/integration acceptance |

## 23.1 Minimum evidence

- Profile C inspection: E1; gerekiyorsa E2
- Backend contract: E2 + E3; state-changing ise E4
- UI/state-changing: E4
- Cross-module capability seam: E5
- Compliance/financial persistent record: L3 + ilgili E4/E5

Agent PASS fakat required evidence seviyesi yoksa CT `ACCEPTED` vermez.

---

# 24. Adım 10 — INDEPENDENT VERIFICATION

Üç verdict ayrıdır:

```text
Agent Verdict
Verification Verdict
Control Tower Acceptance
```

## 24.1 Verification minimumu

Task'a uygulanabildiği ölçüde:

- build,
- relevant tests,
- runtime golden flow,
- declared failure paths,
- persistence level,
- server-side permission denial,
- tenant isolation,
- concurrency,
- idempotency,
- audit,
- evidence,
- observability,
- accessibility states,
- no sensitive leakage,
- no console errors,
- no raw technical tokens in UI.

## 24.2 Vacuity rule

Kritik bug fix'te testin kusuru gerçekten yakaladığını mümkünse:

```text
fix absent → RED
fix present → GREEN
```

ile kanıtla.

---

# 25. Runtime freshness gate

## 25.1 Süreç canlı ≠ yeni binary canlı

Runtime smoke öncesi process freshness doğrulanır.

Örnek:

```bash
ps -o lstart= -p $(lsof -nP -iTCP:<port> -sTCP:LISTEN -t | head -1)
ls -l <project>/bin/Debug/net8.0/<Assembly>.dll
```

Process start, binary timestamp'ten sonra olmalı.

## 25.2 Change/restart matrix

| Değişiklik | Minimum |
|---|---|
| `.js` / `.css` | Hard refresh/cache control |
| `.cshtml` / `.resx` | Web build + restart |
| Service `.cs` | Service build + restart |
| Gateway | Gateway build + restart + route smoke |
| Schema/entity | Build/restart + migration/backfill verification |

## 25.3 UI + server + persistence birlikte

State-changing flow için ideal evidence:

```text
UI result
+ network/API response
+ persisted record
+ audit/observability
```

---

# 26. Operator evidence

CONTROL TOWER'ın secret/login nedeniyle koşamadığı adım:

- operator'a minimal evidence formatı verilir,
- secret/JWT/cookie paylaşılmaz,
- kanıt gelmeden verified yazılmaz.

Örnek:

```text
Route:
Expected action:
Network status:
Permission claim present: yes/no
Persisted result:
Console errors:
```

---

# 27. Adım 11 — INTEGRATION GATE

Bir work package izole olarak PASS olabilir; capability yine de başarısız olabilir.

Cross-module / shared-contract işlerde `VERIFIED` sonrası Integration Gate zorunlu.

## 27.1 Integration Gate

- expected integration base oluşturuldu,
- dependent WPs doğru commit'lerle mevcut,
- contract versions eşleşiyor,
- migrations sıra ile uygulanıyor,
- gateway/frontend/service birlikte ayağa kalkıyor,
- end-to-end cross-module flow çalışıyor,
- no duplicate ownership/state,
- tenant/RBAC korunuyor,
- observability correlation zinciri devam ediyor,
- regression suite geçiyor.

Sonuç:

```text
INTEGRATED
REWORK
BLOCKED
```

**Kural:** iki ayrı branch'te PASS olan işler, entegre edilmeden capability `DONE` olamaz.

---

# 28. Adım 12 — ACCEPT / REWORK / BLOCK

## 28.1 Verdict taxonomy

Developer ve verifier:

```text
PASS
CONDITIONAL PASS
PARTIAL
BLOCKED
FAIL
```

CT status:

```text
ACCEPTED
REWORK
BLOCKED
DEFERRED
```

## 28.2 Rework loop

REWORK durumunda:

- exact failed criterion,
- evidence,
- root cause,
- allowed scope,
- do-not-change,
- retest scope

ile ayrı `R1` work package üretilir.

**Default escalation:** aynı root cause iki ardışık rework turunda kapanmıyorsa `ARCHITECTURE_REASSESSMENT_REQUIRED` ve yeni implementation durdurulur.

---

# 29. Definition of Done

## 29.1 Work Package Done

Bir WP ancak:

- required verification PASS,
- required integration PASS,
- no undeclared scope breach,
- evidence stored,
- required records updated,
- CT `ACCEPTED`

ise done'dır.

## 29.2 Module Done

Module:

- tüm mandatory module-pack AC,
- required WPs accepted,
- module runtime golden flows,
- module-level regression,
- module pack status/update,
- açık blocker yok veya approved deferred

ile done olur.

## 29.3 Capability Done

Capability:

- ilgili module gates,
- cross-module seams,
- DCP gates,
- end-to-end integration,
- evidence/audit expectations,
- accepted deferred items with risk

tamamlanınca done olur.

## 29.4 Release Done

`Capability Done` release demek değildir.

Release closure ayrıca:

- merge/target branch state,
- environment deployment,
- migration state,
- release smoke,
- rollback readiness,
- release evidence

gerektiriyorsa ayrı gate'tir.

```text
WP ACCEPTED ≠ MERGED ≠ MODULE DONE ≠ CAPABILITY DONE ≠ RELEASE VERIFIED
```

---

# 30. Kayıt disiplini

Bir iş tamamlandığında koşula göre:

| Artifact | Trigger |
|---|---|
| Backlog/work item | Her iş |
| Module Pack AC | Module scope |
| Delivery Board | Execution status change |
| Seam/contract register | Cross-module seam |
| DCP/build plan | Sequence/dependency/readiness/scope/blocker |
| ADR/Decision | Architecture decision |
| Waiver register | Exception |
| Audit/report | Verification evidence |

## 30.1 Closure record

```text
WP/Prompt ID + version
Agent verdict
Verification verdict
CT status
Branch/commit
What changed
Decisions + why
Rejected alternatives
Intentionally not done
Evidence commands/pointers
Runtime result
Known gaps
Dependency impact
Next work
```

---

# 31. Reconciliation cadence

`/reconcile-records` veya eşdeğer read-only reconciliation:

- module closure'da,
- capability gate öncesi,
- büyük integration sonrası,
- en az aylık governance sweep'te,
- suspected record/code drift olduğunda

çalıştırılır.

Reconciliation **ölçer**; authority kararı vermeden kayıtları veya code'u otomatik rewrite etmez.

---

# 32. Demir kurallar — evidence-preserving register

## 32.0 Evidence sınıfları

Her kuralın gerekçesi görünür kalır. Ölçülmemiş incident uydurulmaz.

```text
MEASURED CASE   = doğrudan yaşanmış ve ölçülmüş vaka
OBSERVED        = repository/runtime üzerinde ölçülmüş durum
DERIVED CONTROL = ölçülmüş vakalardan türetilmiş önleyici kontrol; kendine ait incident henüz yok
POLICY          = governance / authority gereği
```

Yeni bir `DERIVED CONTROL` için gerçek incident ölçülürse kayıt `MEASURED CASE` olarak yükseltilir.

### K1 — Kapanış kod değil, doğrulamadır

Bir madde kod yazıldığında değil, **davranış canlıda ölçüldüğünde** kapanır.

> **MEASURED CASE — WorkCenter:** aynı gün iki madde `✅` kapatılmıştı. Kod doğruydu ve 2054 test yeşildi; buna rağmen devretme akışı çalışmıyor, kabul kapısı devretmede açılmıyordu. İki kusur da yalnız canlı turda görüldü.

### K2 — Rapora değil ölçüme güven

Agent report claim'dir; code/runtime evidence ile karşılaştırılır.

> **MEASURED CASE:** bir ajan “pack yok” dedi; yanlış klasöre bakmıştı ve pack vardı. Başka bir ajan `RequestTitle` özelliğinin repoda olmadığını söyledi; görünen ad FluentValidation expression path'ten türetiliyordu.

### K3 — Critical fix testi kusuru gerçekten yakalamalıdır

Yeşil test tek başına yeterli değildir; kritik fix'te mümkünse `fix absent → RED`, `fix present → GREEN`.

> **MEASURED CASE:** `…lands in the inbox unaccepted` testleri baştan sona geçiyordu; çünkü başlangıç durumu zaten hiç kabul edilmemiş görevdi. Test kusuru hiç exercise etmiyordu.

### K4 — Yarım düzeltme kusurdan kötü olabilir

Bu kural yalnız seam/contract için değildir. UI semantics, payload, state transition, timing, producer/consumer
ve başka çok-taraflı davranışlarda yalnız bir tarafı düzeltip işi kapatma.

> **MEASURED CASE — SLA badge:** sunucu yarısı düzeltildiğinde ekran `-2g kaldı` dedi. Negatif guard eklendiğinde zamanında biten iş “1g gecikmiş” oldu. Ancak gerekli `closedAt` contract alanı sağlandığında davranış gerçekten düzeldi.

### K5 — Producer düzeldi ≠ consumer teslim aldı

Bir değer üretildiğinde tüketicinin gerçekten aldığı ölçülür.

> **MEASURED CASE — l10n:** payload'a altı key eklendi ve iş bitti sayıldı; consumer başka dictionary okuduğu için UI İngilizce kaldı.

### K6 — Bir olgu iki yerde yaşıyorsa sessizce kayar

Duplicated fact veya contract semantics iki tarafı da okuyan test/contract ile bağlanır.

> **MEASURED CASES:** client `note`, server `Reason` bekliyordu; üç transition çalışmadı. Client `person.id`, server `userId` gönderiyordu; selector boş kaldı. “Accepted” semantics yeni alana taşınırken bazı handler'lar eski sinyali resetlemeye devam etti.

### K7 — Değişken sayı yerine yeniden üretilebilir ölçüm komutu tut

“7 aksiyon”, “3576 satır” gibi drift eden sayılar yerine onları yeniden üreten komut tutulur.

> **POLICY / original SOP rationale:** sayı kodla birlikte değişir; komut yeniden ölçüm sağlar. Orijinal SOP metninde K7 için ayrı incident kaydı yazılmamıştı; v2.3 incident uydurmaz.

### K8 — Deferral future cost/risk beyanıdır

Erteleme, yalnız “sonra” değil gelecekteki regresyon/yeniden iş maliyetini de taşır.

> **POLICY / original SOP rationale:** foundation işleri ertelendikçe sonraki query/consumer yüzeylerine yayılan maliyet artar. Orijinal metinde K8 için ayrı incident kaydı yoktur.

### K9 — Product owner'a ölçümle itiraz et; canonical authority'yi bypass etme

Teknik olarak riskli istek ölçümle challenge edilir; owner kararı ve gerekçesi kaydedilir.

> **POLICY:** karar provenance'ı korunur. Orijinal SOP K9 için ayrı measured incident yazmıyordu; bu nedenle vaka uydurulmamıştır.

### K10 — Tahmin kapanış kanıtı değildir

“Muhtemelen / sanırım / olmalı” kapanış dili değildir; bilinmeyen açıkça unknown olarak yazılır.

> **POLICY / original SOP:** kapanış yalnız measured statement taşır. Ayrı incident kaydı kaynakta yoktur.

### K11 — Agent Lane chat/workspace system-of-record değildir

Kritik scope, status, dependency, decision ve evidence repository artifact'ında tutulur.

> **MEASURED CASE — takeover drift:** bir module pack'te 20 kutu işaretsizdi ama iş yapılmıştı; seam register'ın 5 satırı “yapılmıyor” derken beşi de shipped'di. Kayıt/code drift'i yeniden iş riskine dönüştü.

### K12 — Missing contract ile implementation başlamaz

Eksik/ambiguous contract invention'a dönüşmeden fail-closed edilir.

> **DERIVED CONTROL:** K4/K6'daki measured producer/consumer drift vakalarının tekrarını önlemek için contract-blocker gate'i.

### K13 — Agent PASS ≠ CT ACCEPTED

Agent verdict self-report'tur; independent evidence acceptance'tır.

> **MEASURED CASE:** K1/K2 vakalarında test/report olumlu görünmesine rağmen runtime veya inspected reality farklıydı.

### K14 — Parallel-safe ölçülür, varsayılmaz

Shared file/contract/migration/worktree overlap kontrol edilmeden parallel-safe yazılmaz.

> **OBSERVED:** mevcut çalışma ortamında çoklu worktree kullanımı yüksek; kullanıcı ölçümünde aynı gün 19 worktree vardı. Bu ölçek explicit parallel-safety gate gerektirir. Kaynakta paralel collision incident'i kaydedilmemiştir.

### K15 — Shared seam'de single-writer

Aynı shared contract, migration chain, permission definition, global route family veya shared registration üzerinde
aynı anda birden fazla writer Agent Lane açılmaz.

> **DERIVED CONTROL:** K6'daki duplicated-semantics drift ölçümlerinden ve yüksek parallel-worktree kullanımından türetilmiştir; ayrı writer-collision incident'i henüz kayıtlı değildir.

### K16 — Prompt dispatch sonrası sessiz scope mutation yok

Material scope/contract/acceptance change yeni version veya superseding WP gerektirir.

> **DERIVED CONTROL:** reproducibility ve auditability için. Ayrı measured incident henüz kayıtlı değildir.

### K17 — Commit ≠ acceptance

Commit işi korur; runtime/gate kapanışının yerine geçmez.

> **POLICY:** çalışma döngüsü commit'i "işi koruma" adımı olarak tanımlar, kapanış adımı olarak değil — commit edilmemiş iş hiçbir dalda yaşamaz, ama commit edilmiş iş de doğrulanmış değildir. *(v2.3 sınıflandırma düzeltmesi: bu satır önceki taslakta `MEASURED CASE` olarak işaretlenmişti; gösterilen kanıt bir incident değil, SOP'un kendi işletim kuralıydı. Kuralın gerçek gerekçesi K1'in ölçülmüş vakasıdır; K17 ise ondan türeyen politika ifadesidir.)*

### K18 — Isolated PASS ≠ integrated PASS

Cross-module capability, constituent branch'ler PASS olsa bile integration gate olmadan DONE değildir.

> **DERIVED CONTROL:** integration failure riski için önleyici gate. Bu kuralı doğuran ayrı measured incident bu kaynakta yoktur; vaka uydurulmaz.

### K19 — Waiver owner/expiry olmadan geçerli değildir

Exception permanent shadow-standard'a dönüşemez.

> **POLICY:** waiver reason, owner, compensating control ve review/expiry taşımalıdır. Ayrı measured incident kaynakta yoktur.

### K20 — Agent Lane bounded execution context'tir; authority veya system-of-record değildir

Agent Lane Work Package yürütür; ownership/architecture ve canonical status üretmez.

> **DERIVED CONTROL:** K2 ve K11'deki context/record yanlışlıklarının Agent Lane ölçeğinde tekrarını önlemek için.

### K21 — Her CT turu replan + next-work hesaplamasıyla kapanır

Acceptance/rework sonrası build plan, dependency gates ve newly-unblocked work yeniden hesaplanır.

> **POLICY / operating requirement:** Control Tower'ın amacı tek task closure değil program flow control'dür. Ayrı historical incident kaydı yoktur.


---

# 33. Yeni module/capability devralma — Day 1

```text
1. Identity authority / registry kontrol
2. DCP / capability plan
3. Domain Config
4. Module Pack
5. Delivery board / backlog
6. Branch/worktree/code reality
7. Runtime reality
8. Pack ↔ code ↔ runtime drift matrix
9. Ownership/contract/dependency risks
10. Risk classification
11. Build plan update
12. Owner decisions
13. First inspection / implementation WPs
```

---

# 34. CONTROL TOWER dashboard minimum alanları

| Field | Purpose |
|---|---|
| WP ID | Tekil execution identity |
| Agent Lane | Governed AI execution workspace |
| Agent Lane Type | DEV / INS / VER / INT |
| Module | Ownership |
| Build Lane | Dependency / sequencing plan |
| Status | State machine |
| Risk | Review depth |
| Branch/HEAD | Code isolation |
| Depends On | Gate |
| Parallel Safe With | Concurrency plan |
| Agent Verdict | Claim |
| Verification Verdict | Independent evidence |
| Integration Status | Cross-module truth |
| CT Status | Acceptance |
| Blocker Age | Flow health |
| Rework Count | Quality signal |
| Next Gate | Execution focus |

---

# 35. Operational metrics — optional but recommended

CONTROL TOWER performansı “kaç prompt üretildi” ile değil flow quality ile izlenir.

Önerilen metrikler:

- active WIP,
- blocked work age,
- dependency wait age,
- verification fail rate,
- rework rate,
- same-root-cause rework count,
- record/code drift count,
- cross-module integration failure rate,
- average READY → ACCEPTED lead time,
- accepted-with-waiver count.

Metrikler engineering quality gate'in yerine geçmez.

---

# 36. Work Package canonical template

```text
WORK PACKAGE

WP ID:
Prompt ID:
Prompt Version:
Task Class:
Golden-Flow Profile:
Risk Class:

Capability:
Module:
Sequence:
Build Lane:
Agent Lane ID:
Agent Lane Type:
Target Agent / Entry Point:

Authority:
- Identity:
- DCP:
- Domain Config:
- Module Pack:
- ADR/Inspection:

Repository:
- Path:
- Branch:
- Expected HEAD:
- Worktree:
- Dirty baseline:

Dependencies:
- Depends on:
- Gate state:
- Parallel-safe with:
- Not parallel-safe with:
- Integration order:

Scope:
- Allowed paths:
- Protected paths:
- Owned objects:
- Consumed objects/contracts:

Objective:
<concrete runtime/contract outcome>

Preconditions:
...

Pattern:
<if UI>
Justification:
...

Golden / Contract / Inspection Flow:
...

Persistence:
L1 | L2 | L3 | no writes

Consistency:
atomic | compensating | partial-with-marker | N/A

Security / Privacy:
...

Observability:
...

Conditional Gates:
- Migration:
- Deprecation:
- NFR:
- Accessibility:
- Export:
- Finance precision:

Acceptance Criteria:
...

Validation:
...

Failure Protocol:
- stop on missing contract
- stop on ownership conflict
- stop on protected-path requirement without approval
- stop on branch/base mismatch if not pre-authorized
- do not invent placeholders/contracts

Output Contract:
- Agent verdict
- Branch/HEAD
- Changed files
- Golden-flow evidence
- Failure paths
- Tests
- Persistence/security/audit evidence
- Decisions
- Blockers
- Remaining gaps
```

## 36.1 Agent Prompt (paste-ready)

Yukarıdaki blok CONTROL TOWER'ın **kaydıdır**. Her Work Package ayrıca, hedef ajana olduğu gibi
yapıştırılabilen tek bir prompt bloğu taşır. Metadata formu prompt değildir; yapıştırılamayan WP
dispatch edilemez.

```text
## Agent Prompt

@[.antigravity/agents/{agent}.md]        # veya: /{workflow}  — §6 entry point
WP: {WP ID} · Prompt {Prompt ID} v{version}

Repository: {path} · Branch: {branch} · Expected HEAD: {sha} · Worktree: {path}

Önce oku (sırayla):
{authority paths — module pack, domain-config, AGENTS.md, ilgili .antigravity/rules/**}

{§17.4 tablosundan hedef ajanın zorunlu giriş alanları — doldurulmuş halde}

NE:      {bounded objective / runtime outcome}
NEDEN:   {authority + neden şimdi}
NASIL:   {pattern, persistence L1|L2|L3, consistency, conditional gates}
YAPMA:   {protected paths, out-of-scope, invention yasağı}
DOĞRULA: {acceptance criteria + validation komutları + required evidence level E0-E5}

Durma koşulları: missing contract · ownership conflict · protected-path ihtiyacı ·
branch/HEAD mismatch · unplanned migration. Kapsamı kendin genişletme; dur ve raporla.

Rapor formatı: §22 structured report.
Senin PASS'in kapanış değildir (K13).
```

`Repository:` alanı **ölçülmüş** değerdir; başka makineden kalan mutlak yol kopyalanmaz (§20 preflight).

---

# 37. Verification report canonical template

```text
VERIFICATION REPORT

WP ID:
Verifier:
Verification date:
Branch/HEAD:

Agent Verdict:
Verification Verdict:
CT Status:

Evidence level achieved:
Required evidence level:

Checks:
- scope:
- build:
- tests:
- runtime:
- persistence:
- RBAC:
- tenant:
- concurrency:
- idempotency:
- audit/evidence:
- observability:
- migration/rollback:
- integration:
- console/security leakage:

Failed criteria:
...

Rework required:
yes/no

Next gate:
...
```

---

# 38. CONTROL TOWER ana workflow özeti

```text
YOU / PRODUCT OWNER
        ↓
CONTROL TOWER
        │
        ├─ classify
        ├─ inspect
        ├─ reconcile authority ↔ records ↔ code ↔ runtime
        ├─ decide
        ├─ update capability/build plan
        ├─ dependency + parallel gate
        ├─ DoR
        └─ generate versioned executable WPs
                    ↓
        ┌───────────┼───────────┐
        ↓           ↓           ↓
   AGENT LANE A AGENT LANE B AGENT LANE C
      (DEV)         (DEV)         (DEV)
        │             │             │
        ├ implement   │             │
        ├ self-test   │             │
        └ report      │             │
        └─────────────┴─────────────┘
                    ↓
           INDEPENDENT VERIFY
                    ↓
            INTEGRATION GATE
             (if required)
                    ↓
             CONTROL TOWER
        accept / rework / block
                    ↓
            update artifacts
                    ↓
       recalc dependency/readiness
                    ↓
          issue next safe WPs
                    ↺
```

---

# 39. Final Control Tower checklist

## Authority
- [ ] Concern-specific authority doğru seçildi mi?
- [ ] Canonical identity valid mi?
- [ ] Module/domain boundaries açık mı?
- [ ] Code reality authority ile karıştırılmadı mı?

## Ready
- [ ] DoR PASS mi?
- [ ] Pack status executable mı?
- [ ] Contract freeze/availability yeterli mi?
- [ ] Branch/HEAD/worktree doğru mu?
- [ ] Risk sınıfı doğru mu?

## Agent Lane / Parallel
- [ ] Her WP doğru Agent Lane ID/Type'a atanmış mı?
- [ ] Agent Lane branch/worktree/scope context'i hâlâ geçerli mi?
- [ ] Shared writer çakışması yok mu?
- [ ] Parallel-safe kanıtlandı mı?
- [ ] Integration order yazıldı mı?

## Prompt
- [ ] Versioned ve immutable dispatch contract mı?
- [ ] Scope / protected paths açık mı?
- [ ] Golden/contract flow ölçülebilir mi?
- [ ] Conditional gates doğru tetiklendi mi?

## Verification
- [ ] Required evidence level sağlandı mı?
- [ ] Runtime freshness doğru mu?
- [ ] UI + server + persistence + audit gerektiği yerde birlikte ölçüldü mü?
- [ ] Security/tenant/concurrency/idempotency kontrol edildi mi?
- [ ] Agent PASS bağımsız doğrulandı mı?

## Integration
- [ ] Cross-module ise integrated runtime test edildi mi?
- [ ] Contract versions ve migrations uyumlu mu?
- [ ] Isolated PASS ile capability closure karıştırılmadı mı?

## Closure
- [ ] DoD PASS mi?
- [ ] Records güncel mi?
- [ ] Waiver/deferred risk kayıtlı mı?
- [ ] Dependency graph yeniden hesaplandı mı?
- [ ] Newly unblocked work belirlendi mi?
- [ ] Güvenli paralellik varsa en az iki sonraki WP hazır mı?

---

# 40. Repository adoption gate

Bu belge `Proposed canonical` durumundan `Canonical` duruma ancak aşağıdakiler ölçüldükten sonra geçer:

- [ ] §2.1'deki tüm normatif repository path'leri gerçekten resolve oluyor.
- [ ] `docs/agent-usage-guide.md` içinde Agent Lane / parallel worktree günlük kullanımına bu SOP'a **link/cross-reference** var.
- [ ] Daily Operating Card repository'de erişilebilir bir path'e konmuş ve bu SOP'tan linklenmiş.
- [ ] Repo dışında kalan upstream vertical-slice / Golden-Flow artifact'ları ya:
  - approved canonical repo path'e adopt edilmiş, **veya**
  - `SOURCE / PENDING ADOPTION` olarak kalmış ve SOP-local gate'ler kullanılıyor.
- [ ] CONTROL TOWER'ın kullandığı Work Package/Verification template path'leri repository'de resolve oluyor veya bu SOP içinden doğrudan kullanılabiliyor.

Bu gate tamamlanmadan belge kullanılabilir; ancak status:

```text
PROPOSED CANONICAL — REPOSITORY ADOPTION PENDING
```

olarak kalır.

## 40.1 Agent Usage Guide için minimum cross-reference

`docs/agent-usage-guide.md` içine içerik kopyalamak yerine aşağıdaki kısa blok eklenmelidir:

```text
### Parallel Agent Lane / Worktree Development

Parallel AI development is governed by CONTROL TOWER SOP:
- Build Lane = dependency/sequencing stream.
- Agent Lane = controlled AI chat/workspace.
- Parallel work requires separate worktree + disjoint scope.
- Shared contracts/migrations/permissions/global routes follow single-writer.
- `Parallel-safe with` is declared by CONTROL TOWER, never assumed.

See: <repository path to CONTROL TOWER SOP> §§3.2, 16, 20.
```

Bu cross-reference parallel-worktree kuralını ikinci kez tanımlamaz; canonical ayrıntı bu SOP'ta kalır.

---

# 41. Nihai işletim ilkesi

```text
Canonical authority outranks chat context.
Measured runtime behavior outranks agent narrative.
Definition of Ready outranks premature dispatch.
Golden-flow completion outranks artifact completion.
Single-writer shared seams outrank artificial parallelism.
Agent Lane identity outranks ambiguous chat naming.
Independent verification outranks self-reported PASS.
Integrated behavior outranks isolated branch success.
Recorded decisions outrank remembered decisions.
Replanning is part of closure.
```
