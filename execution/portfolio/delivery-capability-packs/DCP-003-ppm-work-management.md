---
id: DCP-003
slug: ppm-work-management
name: PPM Work Management
type: Delivery Capability Pack
standard: CAP-001
status: draft
owner_domain: portfolio-delivery
owner: enterprise-architect-interim-governance-only / permanent-business-owner-TBD
parent_module: MOD-0117
branch: feature/ppm-integration
created: 2026-07-07
---

# DCP-003 — PPM Work Management (Delivery Capability Pack)

> **DEFERRED SCOPE-PARTITION PLANNING SOURCE — NON-EXECUTABLE:** Enterprise Architect scope-partition
> decision dated 2026-07-28 removed active MOD-0117 core and C1 Work Records/Project implementation from
> this draft. DCP-006 is the sole active orchestration contract for Management & Governance 1.3. DCP-003 is
> retained only for deferred legacy safe-parity planning and migration reference; it authorizes no module
> pack, service scaffold, production implementation or delivery sequence.

> **Artifact type:** This is a **Delivery Capability Pack** (CAP-001 governance / orchestration contract).
> It is **NOT** a runtime entity, **NOT** a module pack, **NOT** a MOD-0014 runtime Capability Group,
> and **NOT** a business-capability-matrix row. It references member modules **by ID only** and never
> replaces a module pack. See [`.antigravity/rules/capability-pack-standard.md`](../../../.antigravity/rules/capability-pack-standard.md).

> **Premature-coding guard (CAP-001 §7):** Production code for any member starts only when **both** hold:
> (a) this Delivery Capability Pack is `approved` / `ready-for-execution`, **and** (b) the next member's
> module pack is `approved` / `ready-for-dev`. This draft authorizes **no** implementation.

> **Kaynak analizler (2026-07-07):** (1) DitenPPM / PharmacovigilanceWeb migration feasibility audit,
> (2) PPM governance / MOD-ID / boundary read-only audit. Her ikisi de read-only yapılmıştır; kanıt
> yolları bu pack'in ilgili bölümlerinde özetlenmiştir.

---

## 1. Identity and status

| Field | Value |
|-------|-------|
| ID | DCP-003 |
| Slug | ppm-work-management |
| Name | PPM Work Management |
| Type | Delivery Capability Pack (CAP-001) |
| Status | `draft` |
| Parent module identity | **MOD-0117 — Project & Portfolio Management (PPM)** (Blueprint-kanonik) |
| Identity proof | `verify_module_id.py . --check-id MOD-0117 --name "Project & Portfolio Management (PPM)"` → `OK … proven` (exit 0, 2026-07-07) |
| Owner domain | portfolio-delivery (kısa kod `ppm`) — kullanıcı onayı 2026-07-07 |
| Interim governance owner | Enterprise Architect — OD-07 reconciliation only; no business acceptance or production authority |
| Permanent business owner | 🔴 TBD |
| Standard | CAP-001 — `.antigravity/rules/capability-pack-standard.md` |
| Authority note | Bu pack AGENTS.md §1 yetki hiyerarşisini (`Module Pack > Domain Config > AGENTS.md > .antigravity/`) değiştirmez; yalnızca üst seviye orkestrasyon yaşam döngüsüdür. |

## 2. Business outcome

Eski `DitenPPM` + `PharmacovigilanceWeb` içindeki PPM Task/My Tasks, Workstream, Calendar Scheduling,
Project Effort Log, Meeting/Status Reports ve migration bilgisini ileride yeniden değerlendirilebilecek,
non-executable safe-parity planlama kaynağı olarak korumak. Aktif portfolio/investment/initiative/program/
project/benefit-value teslimatı DCP-006'ya aittir.

## 3. Problem statement

- Eski PPM backend'inde kimlik doğrulama, TenantId ve soft delete yoktur; kullanıcı kimliği client'tan gelir (feasibility audit §3).
- ~39.000 satır sayfa-JS'i vNext DataTable v2 / l10n / SweetAlert2 / tenant-shell standartlarıyla uyumsuzdur; 3 UI çağrısının backend karşılığı yoktur.
- **2026-07-07 historical AS-IS finding:** ERP-vNext'te PPM domain ve registry kaydı yoktu. Bugün
  `portfolio-delivery` domain scaffold ve MOD-0117 registry kaydı mevcuttur; aktif bir PPM module pack
  hâlâ yoktur.
- Alanların bir kısmı başka modüllerin SoR'una değer (MOD-0023/0024/0028/0048/0280/0288) — sınırlar sözleşmeyle sabitlenmezse SoR sürünmesi ve isim çakışması (özellikle "Workflow") kaçınılmazdır.

## 4. Capability boundary

**İçeride — deferred planning only:** PPM Task/My Tasks, Workstream, Calendar Scheduling, Project Effort
Log, Meeting / Status Reports ve legacy migration referansları. Bunlar implementation authority taşımaz.

**Dışarıda (sahibi başka modül):** onay/SLA/eskalasyon motoru (MOD-0023); görev/checklist şablonları
(MOD-0024); doküman/binary depolama (MOD-0028/0262); kurumsal lookup SSOT (MOD-0048); organizasyon
dizini (MOD-0288); Time Entry/Attendance/Leave (MOD-0280); bildirim gönderimi (MOD-0027); audit trail
depolama (MOD-0021); Google/SignalR/AI entegrasyonları (§12 exclusions).

**Scope guard:** DCP-006, aktif Management & Governance 1.3/MOD-0117 orchestration sahibidir. Demand
implementation ve Capacity kapsam dışıdır; Demand yalnız typed MOD-0117 transition/reference olabilir.

## 5. Member modules and follow-ups

Kesin FU numaraları **rezerve edilmedi**. Bu backlog ileride yeni explicit EA/user kararıyla yeniden
etkinleştirilirse, o tarihteki her yeni kimlik parent-aware DCP-002 preflight'ından geçmek zorundadır. Bu
ifade mevcut module-pack authoring talimatı değildir.

| # | Candidate member | Kimlik | Faz | UI yüzeyi |
|---|---|---|---|---|
| C1 | PPM Work Records Core | **MOD-0117** | **Historical — retired from active sequence** | Active implementation moved to DCP-006 scope partition |
| C2 | PPM Task Core / My Tasks | Identity unresolved | **Deferred / non-executable** | Must reconcile MOD-0024, DWS, MOD-0023 and DCP-004 first |
| C3 | PPM Workstream & Hierarchy | Identity unresolved | **Deferred / non-executable** | Must reconcile MOD-0024, DWS, MOD-0023 and DCP-004 first |
| C5 | PPM Project Effort Log | Identity unresolved | **Deferred / non-executable** | ASSUMPTION-1 remains unresolved |
| C4 | PPM Calendar Scheduling | Identity unresolved | **Deferred / non-executable** | Future planning only |
| C6 | PPM Meeting / Status Reports | Identity unresolved | **Deferred / non-executable** | Future planning only |
| C7 | PPM Files / Attachments | — | Ertelendi | Evidence/document **link** deseni (§12) |
| C8 | PPM Reference Data / Lookups | Historical C1 note | **Deferred / non-executable** | MOD-0048 boundary reference |
| C9 | Google Calendar / Meet Integration | — | **Faz-dışı** | — |
| C10 | SignalR Real-time Meeting Hub | — | **Faz-dışı** | — |
| C11 | AI Action Extraction | — | **Faz-dışı / Bloklu** | — |
| — | TimerPopup | — | **Faz-dışı / Bloklu** (backend kontratı yok) | — |
| — | Excel task import | — | **İlk faz dışı** (P2+ karar) | — |

## 6. Ownership map (System-of-Record)

| Nesne | SoR | Statü |
|---|---|---|
| Work Record / Project | **MOD-0117** | KESİN (Blueprint SoR: "programs/projects, portfolio items, demand items") |
| PPM Task instance | **Unresolved after scope partition** | MOD-0024, MOD-0354 (historical alias CAND-CAP-0008), MOD-0023 and DCP-004 reconciliation required |
| Task/Checklist **şablonları** | MOD-0024 | KESİN — PPM sahiplenemez |
| Schedule Slot | MOD-0117-FU adayı | ÖNERİ (Blueprint'te başka sahip yok) |
| Project Effort Log / Time Entry | **ÇÖZÜMSÜZ** | ASSUMPTION-1 (§18 Open decisions) |
| Meeting / Status Report | MOD-0117-FU adayı | ÖNERİ; Decision kayıtlarının kalıcı SoR'u MOD-0007 |
| Dosya binary | MOD-0028 / MOD-0262 | KESİN — PPM kalıcı binary owner olmaz |
| Kurumsal lookup | MOD-0048 | KESİN hedef; PPM-yerel = geçici |
| Organizasyon (domain/subdomain) | MOD-0288 hedef | PPM-yerel snapshot = geçici (ASSUMPTION-2) |
| Onay/SLA/eskalasyon tanımları | MOD-0023 | KESİN — PPM motor yazamaz |

## 7. Dependency graph

```text
 DCP-006 — active 1.3/MOD-0117 orchestration
                         │
                         └── DCP-003 deferred planning archive
                                  ├── C2 Task/My Tasks
                                  ├── C3 Workstream
                                  ├── C4 Calendar
                                  ├── C5 Effort Log
                                  └── C6 Meeting/Status
MOD-0021 AuditEvent v1 (kontrat) ────────────────────────────────┤
Future reconciliation dependencies only:
  C2/C3 ── MOD-0024 + MOD-0354 + MOD-0023 + DCP-004
  C4/C6 ── MOD-0023 + MOD-0027/0028/0048/0288 + MOD-0007
  C5    ── MOD-0280 SoR decision
```

## 8. Ordered delivery sequence

There is no production delivery sequence in DCP-003 after the 2026-07-28 scope partition. C1 is retired
from this active sequence; C2–C6 are unordered, deferred and non-executable planning records. No module pack
may be authored from this sequence. Task and Workstream planning must first be reconciled with MOD-0024,
MOD-0354, MOD-0023 and DCP-004.

## 9. Prerequisites

1. DCP-003 remains `draft` and non-executable.
2. No C1 or C2–C6 module pack may be authored from this document.
3. Any future reconsideration requires a new explicit EA/user governance decision and current SoR
   reconciliation; PPM Task and Workstream additionally require MOD-0024, MOD-0354, MOD-0023 and
   DCP-004 reconciliation.
4. Legacy migration remains blocked by B4/B5 and requires separate approved governance.

## 10. Architecture decisions

The following entries are retained as historical planning constraints only; they are not approved runtime
decisions or implementation authority.

- **Service authority:** DCP-003 does not authorize `Diten.PpmService` scaffolding. Active scaffold gates are
  defined by DCP-006 and the reconciled portfolio-delivery domain config.
- **Kimlik/Tenant:** `userId`/`tenantId` yalnız JWT claim + tenant middleware'den; client-supplied kimlik deseni (eski sistem) **yasak**. DTO'da TenantId yasak; cross-tenant 404.
- **Veri:** GUID (subtype-4), `IsDeleted`/`DeletedAt`, tenant-first compound index (ESR). Eski ObjectId/`Status` bool desenleri devralınmaz.
- **Statü yaşam döngüsü:** MOD-0023 hazır olana dek yalın `statusId` alanı; approval-engine yazımı yasak.
- **Concurrency:** MeetingReport/MeetingInvite `Version` optimistic concurrency davranışı korunur.
- **UI:** `_LayoutTenantShell.cshtml`; DataTable v2; SweetAlert2 (MOD-0013 standardı); CDN plugin yasağı; 7 dil l10n köprüsü. Eski JS/View **kopyalanmaz** — yalnızca iş kuralı referansı.
- **Historical calendar proposal:** Eski 6 örtüşen endpoint'in tek kontrata indirilmesi yalnız future
  reconciliation girdisidir; aktif C4 pack veya endpoint kararı değildir.
- **Sırlar:** Eski appsettings sırları (SMTP şifresi, Google service-account key) repoya taşınmaz; mevcutları rotate edilmelidir.

## 11. Scope

Scope is limited to preserving deferred legacy safe-parity planning and migration evidence in §4/§5.
There is no implementable first slice. Active Work Records/Project scope is governed only by DCP-006.

## 12. Explicit exclusions

| Alan | Statü | Gerekçe |
|---|---|---|
| Google Calendar / Meet | **Faz-dışı** (onaylı, 2026-07-07) | Dış sağlayıcı + sır yönetimi; External Systems Register kalıbı gerekir |
| SignalR real-time hub | **Faz-dışı** (onaylı) | Ocelot WS route kararı; MVP'de gereksiz |
| AI Action Extraction | **Faz-dışı / Bloklu** (onaylı) | Backend hiçbir projede yok (feasibility BLOKER-3) |
| TimerPopup | **Faz-dışı / Bloklu** (onaylı) | `TimeTracker/Stop` backend kontratı yok (feasibility BLOKER-1); kontratsız operational UI yasak |
| Excel task import | **İlk faz dışı** (onaylı) | EPPlus lisans + dosya güvenliği ayrı karar |
| SMTP/e-posta outbox | Faz-dışı | MOD-0027 sahası |
| Demand intake / Benefits / Capacity (Blueprint geniş kapsam) | Bu DCP dışı | §4 scope guard |
| Eski UI/backend dosyalarının kopyalanması | **Kalıcı yasak** (onaylı) | Kimlik modeli + l10n/DataTable uyumsuzluğu |
| `Workflow*` adlandırması | **Kalıcı yasak** (onaylı) | §13 drift riski / MOD-0023 ayrışması |

## 13. Governance drift risks

1. **"Workflow" naming sızıntısı (KRİTİK):** PPM tarafında `Workflow`, `WorkflowTask`, `WorkflowCategory`
   ve türevleri hiçbir yeni route, permission key, UI metni, menü, class/namespace, JS, Mongo koleksiyonu,
   pack adı veya branch slug'ında kullanılamaz (MOD-0023 ayrışması). Onaylı sözlük: Work Record, Project
   Record, Work Item, PPM Task, Project Effort Log, Meeting / Status Report, Workstream, Schedule Slot.
   Tek istisna: migration'da eski kaynak koleksiyonlarının salt-okunması. **Gate kriteri:** her üye pack
   PR'ında `grep -ri "workflow"` yalnızca MOD-0023 sınır/yasak bağlamında sonuç verir.
2. **SoR sürünmesi:** Geçici sahiplikler (lookup, organizasyon, attachment, effort) "geçici + hedef modül"
   etiketi olmadan kalıcılaşırsa MOD-0028/0048/0280/0288 ile çatışma borcu birikir.
3. **Scope patlaması:** Blueprint tam kapsamına kayma — §4 scope guard her pack'te tekrarlanır.
4. **FU kimlik disiplini:** Preflight'sız FU numarası basılması DCP-002 ihlalidir.
5. **Master plan / delivery board senkronu:** Wave/kapasite ataması yapılmadan yürütme, plan disiplinini bozar.

## 14. Review questions

1. `portfolio-delivery` + `ppm` domain kararı onaylı mı? → **EVET (2026-07-07, kullanıcı).**
2. Effort Log geçici sahipliği (ASSUMPTION-1) kabul mü? → **EVET — MOD-0280 SoR kararı TBD kalarak.**
3. Faz-dışı liste (Google/SignalR/AI/TimerPopup/Excel) onaylı mı? → **EVET.**
4. Historical C1 role matrix (B6) recorded? → **YES (2026-07-07)**; retained as reference, not active authority.
5. C5 Effort Log P1 mi P2 mi? → B2 EA kararına bağlı, 🔴 açık.
6. FullCalendar ve zengin editör kararları? → Yalnız backlog yeniden etkinleştirilirse future governance
   reconciliation girdisi, 🔴 açık.

## 15. Gate criteria

- **DCP onay kapısı:** `draft → under-review → approved` yalnızca kullanıcı onayıyla.
- **Non-execution gate:** no member module pack may be initiated from DCP-003.
- **Kimlik kapısı:** her FU için preflight exit 0 + registry satırı.
- **Naming kapısı:** §13/1 grep kuralı.
- **Kontrat kapısı:** backend kontratı olmayan hiçbir ekran operational UI kapsamına alınmaz (TimerPopup emsali).
- **Scope-partition gate:** active 1.3/MOD-0117 work routes through DCP-006; deferred task/workstream work
  requires fresh cross-owner reconciliation before module-pack authoring.
- **Migration kapısı:** B4 + B5 kapanmadan migration scripti yazılmaz; Mongo GUID subtype-4 kuralı zorunlu.

## 16. Acceptance criteria

1. DCP-003 remains `draft`, retained and non-executable.
2. C1 is recorded as historical/retired from the active sequence.
3. C2–C6 remain deferred planning records and create no module-pack authority.
4. DCP-006 is the sole active 1.3/MOD-0117 orchestration contract.
5. No runtime behavior is changed by this reconciliation.

## 17. Downstream business-module impacts

These are deferred planning implications only:

- **MOD-0023:** PPM, Workflow Designer'ın ilk büyük tüketici adayıdır; MOD-0023 önceliklendirmesine talep sinyali üretir.
- **MOD-0024:** PPM task instance hacmi, şablon motoru gereksinimlerini şekillendirir.
- **MOD-0280 (gelecek HCM):** Effort Log verisi Time Entry SoR kontratının ilk gerçek kaynağı olur; ASSUMPTION-1 devri planlanmalıdır.
- **MOD-0048 / MOD-0288:** PPM-yerel lookup/organizasyon snapshot'ları devir backlog'u üretir.
- **MOD-0007:** Meeting Decisions link kontratı Decision & Rationale Log'un tenant-side ilk kullanımı olabilir.
- **MOD-0021 / MOD-0027:** AuditEvent v1 ve bildirim kontratlarına yeni tüketici ekler.

## 18. Open decisions

**BLOKER'lar (merkezi liste):**

| # | BLOKER | Etkisi | Gereken karar | Durum |
|---|---|---|---|---|
| B1 | Domain adı/kısa kodu | DCP/pack konacak yer yok | Kullanıcı/EA Yol A onayı | ✅ **KAPANDI 2026-07-07** — `portfolio-delivery` / `ppm` |
| B2 | Timesheet SoR sınırı (MOD-0280 vs MOD-0117-FU) | C5 veri modeli/permission tasarlanamaz | EA kararı + registry notu | 🔴 **AÇIK — TBD** (ASSUMPTION-1 geçici kabul, 2026-07-07) |
| B3 | MOD-0117 registry rezervasyonu + FU numaraları | Deferred backlog kimliksizdir | Yeni EA/user reactivation kararı sonrası registry + parent-aware preflight | ✅ Parent mevcut; FU authoring yetkisi yok |
| B4 | Eski kullanıcı Id ↔ AuthService eşlemesi | Migration-ready denemez | Eşleme stratejisi (ör. e-posta) | 🔴 AÇIK |
| B5 | PvPPM verisinin hedef tenant ataması | Migration scripti yazılamaz | Hedef tenant kararı | 🔴 AÇIK |
| B6 | RBAC rol × kaynak × aksiyon matrisi | Permission seed anlamlandırılamaz; implementation-ready denemez | Business rol matrisi (en az MVP: Admin/Member/Viewer) | ✅ **KAPANDI 2026-07-07** — MVP matrisi kullanıcı tarafından verildi (aşağıda "B6 kararı") |
| B7 | Karşılıksız UI kontratları (TimeTracker/Stop, UpdateTaskOrder, AI ExtractActions) | Kontratsız akışlar kapsama alınamaz | Kapsam kararı | ✅ **KAPANDI 2026-07-07** — ilk DCP'de faz-dışı |
| B8 | MOD-0023 hazır değil (review/planned) | PPM onay akışı yazamaz | Yalın statü + motor yasağı kuralı | ✅ **KAPANDI 2026-07-07** — kural kabul edildi (§10/§13) |

**Tarihsel karar kaydı (2026-07-07; aktif sıra 2026-07-28 scope partition ile kaldırıldı):** domain
`portfolio-delivery`; kısa kod `ppm`; "Workflow" adlandırma yasağı; eski ilk faz `PPM Work Records Core`;
Google/SignalR/AI/TimerPopup/Excel-import ilk faz dışı;
Project Effort Log ASSUMPTION-1 geçici kabul (MOD-0280 SoR kararı TBD); eski UI kopyalanmayacak;
approval-engine yazılmayacak (MOD-0023'e kadar yalın statü).

**B6 tarihsel kaydı — retired C1 MVP RBAC matrisi (2026-07-07, kullanıcı; non-executable):**

Roller: `PPM Admin`, `PPM Member`, `PPM Viewer`. C1 permission seti:
`ppm.work-records.read`, `ppm.work-records.create`, `ppm.work-records.update`,
`ppm.work-records.delete`, `ppm.reference-data.read`.

| Yetki | PPM Admin | PPM Member | PPM Viewer |
|---|---|---|---|
| work-records.read | ✅ (tenant'taki TÜM kayıtlar) | ✅ (kendi oluşturduğu VEYA owner/assignee/team-member olduğu kayıtlar) | ✅ (kendisine görünür kılınan kayıtlar, salt-okunur) |
| work-records.create | ✅ | ✅ | ❌ |
| work-records.update | ✅ (tüm kayıtlar) | ✅ (yalnız kendi görünürlük kapsamındaki kayıtlar) | ❌ |
| work-records.delete | ✅ (**soft delete**) | ❌ | ❌ |
| reference-data.read | ✅ | ✅ | ✅ |

Tenant kuralları (bağlayıcı): tüm roller yalnızca kendi `TenantId` kapsamını görür; cross-tenant erişim
**404** (veya kontrollü forbidden) döner; `TenantId` asla client payload'dan alınmaz — server-side
context/JWT'den çözülür. İlk fazda **CompanyId kullanılmaz** (ASSUMPTION-3 ilk faz için kullanıcı onaylı).
İlk fazda approval/SLA/escalation yok — yalnız yalın status lifecycle. Delete = soft delete (`IsDeleted`/`DeletedAt`).

> **Retired C1 historical design questions — non-executable:**
> (1) **Viewer görünürlük mekanizması** — "kendisine görünür kılınan kayıt" hangi mekanizmayla belirlenir
> (varsayılan tenant-geneli read mi, açık paylaşım/atama mı)?
> (2) **Member'ın team-member kapsamı** — C1'de team/assignee kavramı hangi minimum veri modeliyle temsil
> edilir (Work Record `OwnerId` + basit üye listesi önerisi), yoksa team kapsamı C2/C3'e mi kayar?

**ASSUMPTION'lar:**

- **ASSUMPTION-1 (Effort Log SoR):** PPM, "Project Effort Log" nesnesini MOD-0117 altında **geçici olarak**
  sahiplenir; MOD-0280 hayata geçtiğinde Time Entry SoR'u ile resmi kontrat kurulur ve gerekiyorsa devir yapılır.
  Kesin karar değildir. Risk: çifte SoR / migration tekrarı. Kalıcı karar: EA + registry notu. *(Geçici kabul: 2026-07-07)*
- **ASSUMPTION-2 (Organizasyon referansı):** FunctionalDomain/SubDomain ilk fazda PPM-yerel snapshot.
  Risk: MOD-0288 drift'i. Kalıcı karar: MOD-0288 besleme kontratı.
- **ASSUMPTION-3 (CompanyId):** PPM tenant-level izolasyonla başlar; company-level filtre P2+ follow-up.
  Risk: sonradan boyut ekleme migration'ı. Kalıcı karar: EA/product.
- **ASSUMPTION-4 (Port) — retired historical proposal:** `5060` eski öneriydi ve aktif rezervasyon değildir.
  Aktif port kararı yalnız DCP-006 Slice 2 için OD-03/OD-04 kapandıktan ve onaylı module pack mevcut
  olduktan sonra verilebilir.

**🔴 TBD:** pack owner; FU numaraları; FullCalendar vendor onayı; zengin editör vendor kararı;
C5'in P1/P2 yerleşimi; geçici attachment ihtiyacı; MOD-0117'nin wave ataması (master plan/delivery board).

## 19. Future follow-ups

- MOD-0048'e lookup devri; MOD-0288 besleme kontratı; MOD-0007 Decision link kontratı; MOD-0028/0262 evidence-link entegrasyonu.
- MOD-0023 hazır olduğunda yalın statüden approval kontratına geçiş pack'i.
- MOD-0280 geldiğinde Effort Log ↔ Time Entry SoR kontratı / devri.
- Veri migration fazı (Id yeniden anahtarlama GUID subtype-4, tenant ataması, kullanıcı eşleme) — B4/B5 kapandığında ayrı plan.
- Google/SignalR/AI/TimerPopup/Excel-import için kapsam kararları (ayrı tur).
- Demand implementation ve Capacity bu DCP dışında kalır; Demand yalnız typed MOD-0117
  transition/reference olabilir. Benefit/value aktif orchestration DCP-006'dadır.

## 20. Audit and reconciliation notes

### Change log

| Date | Change | Authority |
|---|---|---|
| 2026-07-28 | OD-07 scope partition: C1 retired from the active sequence; C2–C6 retained as deferred/non-executable legacy safe-parity planning; DCP-006 established as sole active 1.3/MOD-0117 orchestration. | Enterprise Architect — interim governance owner only |

- 2026-07-28: **Scope partition** — DCP-006 became the sole active 1.3/MOD-0117 orchestration contract.
  DCP-003 was retained as a deferred, non-executable legacy safe-parity planning source; C1 retired from its
  active sequence and C2–C6 deferred. Enterprise Architect is interim governance owner only; permanent PPM
  business owner remains TBD.
- 2026-07-07: Migration feasibility audit (read-only) — eski backend/UI haritası, endpoint/view mapping, bloklayıcılar.
- 2026-07-07: Governance audit (read-only) — MOD-0117 preflight exit 0; registry/master plan'da PPM yokluğu; MOD-0023/0280 sınır analizi.
- 2026-07-07: Kullanıcı karar turu — B1/B7/B8 kapandı; ASSUMPTION-1 geçici kabul; DCP taslağı onaylanarak materialize edildi (`draft`).
- Reconciliation: implementasyon fazları sonrası bu bölüme fiili/plan sapmaları işlenecek (CAP-001 §5 `reconciled`).
