---
id: DCP-003
slug: ppm-work-management
name: PPM Work Management
type: Delivery Capability Pack
standard: CAP-001
status: draft
owner_domain: portfolio-delivery
owner: TBD
parent_module: MOD-0117
branch: feature/ppm-integration
created: 2026-07-07
---

# DCP-003 — PPM Work Management (Delivery Capability Pack)

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
| Owner | 🔴 TBD |
| Standard | CAP-001 — `.antigravity/rules/capability-pack-standard.md` |
| Authority note | Bu pack AGENTS.md §1 yetki hiyerarşisini (`Module Pack > Domain Config > AGENTS.md > .antigravity/`) değiştirmez; yalnızca üst seviye orkestrasyon yaşam döngüsüdür. |

## 2. Business outcome

Eski `DitenPPM` + `PharmacovigilanceWeb` PPM yeteneğinin (iş/proje kayıtları, görevler, workstream,
takvim planlama, efor kaydı, toplantı/durum raporları) ERP-vNext üzerinde **tenant-güvenli, RBAC'lı,
7 dilli ve audit-uyumlu** olarak yeniden kurulması; eski sistemin auth'suz/tenant'sız teknik borcunun
**devralınmaması**. Hedef, Blueprint `R1 - PPM MVP` kapsamının eski-sistem-paritesi alt kümesidir.

## 3. Problem statement

- Eski PPM backend'inde kimlik doğrulama, TenantId ve soft delete yoktur; kullanıcı kimliği client'tan gelir (feasibility audit §3).
- ~39.000 satır sayfa-JS'i vNext DataTable v2 / l10n / SweetAlert2 / tenant-shell standartlarıyla uyumsuzdur; 3 UI çağrısının backend karşılığı yoktur.
- ERP-vNext'te PPM için domain, registry kaydı ve module pack yoktur; Blueprint'te MOD-0117 mevcuttur ama repo governance'ına işlenmemiştir.
- Alanların bir kısmı başka modüllerin SoR'una değer (MOD-0023/0024/0028/0048/0280/0288) — sınırlar sözleşmeyle sabitlenmezse SoR sürünmesi ve isim çakışması (özellikle "Workflow") kaçınılmazdır.

## 4. Capability boundary

**İçeride:** Work Record yaşam döngüsü; PPM Task instance'ları (subtask/dependency/checklist/complete);
Workstream hiyerarşisi; Calendar Scheduling (schedule slot + yerleşmemiş iş kuyruğu); Project Effort Log
(ASSUMPTION-1 rejimi); Meeting / Status Report aggregate'i; PPM'e özel geçici referans veriler; bunların
tenant-shell UI'ları; `/services/ppm/*` gateway kontratı; `ppm.*` permission ailesi.

**Dışarıda (sahibi başka modül):** onay/SLA/eskalasyon motoru (MOD-0023); görev/checklist şablonları
(MOD-0024); doküman/binary depolama (MOD-0028/0262); kurumsal lookup SSOT (MOD-0048); organizasyon
dizini (MOD-0288); Time Entry/Attendance/Leave (MOD-0280); bildirim gönderimi (MOD-0027); audit trail
depolama (MOD-0021); Google/SignalR/AI entegrasyonları (§12 exclusions).

**Scope guard:** Blueprint MOD-0117 50+ soft page tanımlar (demand intake, benefits, capacity...);
bu DCP'nin kapsamı **eski sistem paritesinin güvenli alt kümesidir** — geniş Blueprint kapsamı ileride
ayrı FU dalgalarıdır.

## 5. Member modules and follow-ups

Kesin FU numaraları **rezerve edilmedi** — her üye pack authoring'de
`verify_module_id.py --check-id MOD-0117-FUxx --name "..." --parent MOD-0117` preflight'ından geçer.

| # | Candidate member | Kimlik | Faz | UI yüzeyi |
|---|---|---|---|---|
| C1 | PPM Work Records Core | **MOD-0117** (parent MVP dilimi) | **MVP** | DataTable v2 + Compact Create/Edit |
| C2 | PPM Task Core / My Tasks | MOD-0117-FU adayı | P1 | DataTable v2 + Detail/Form + Wizard (complete) |
| C3 | PPM Workstream & Hierarchy | MOD-0117-FU adayı | P1 | DataTable + Detail/Form (hiyerarşi tasarım kararı pack'te) |
| C5 | PPM Project Effort Log | MOD-0117-FU adayı (ASSUMPTION-1) | P1/P2 (B2'ye bağlı) | DataTable v2 + Slim Offcanvas; QuickTimer detayı Read-only |
| C4 | PPM Calendar Scheduling | MOD-0117-FU adayı | P2 | Calendar screen (FullCalendar vendor onayı şart) |
| C6 | PPM Meeting / Status Reports | MOD-0117-FU adayı | P2 | Detail/Form (zengin editör vendor kararı pack'te; CDN yasak) |
| C7 | PPM Files / Attachments | — | Ertelendi | Evidence/document **link** deseni (§12) |
| C8 | PPM Reference Data / Lookups | C1'e gömülü | MVP içi | — (MOD-0048 devri follow-up) |
| C9 | Google Calendar / Meet Integration | — | **Faz-dışı** | — |
| C10 | SignalR Real-time Meeting Hub | — | **Faz-dışı** | — |
| C11 | AI Action Extraction | — | **Faz-dışı / Bloklu** | — |
| — | TimerPopup | — | **Faz-dışı / Bloklu** (backend kontratı yok) | — |
| — | Excel task import | — | **İlk faz dışı** (P2+ karar) | — |

## 6. Ownership map (System-of-Record)

| Nesne | SoR | Statü |
|---|---|---|
| Work Record / Project | **MOD-0117** | KESİN (Blueprint SoR: "programs/projects, portfolio items, demand items") |
| PPM Task instance | **MOD-0117** | KESİN (MOD-0024 yalnız şablon SoR'u) |
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
AuthService (JWT/RBAC/kullanıcı) ─┐
Diten.Platform (tenant/nav/manifest) ─┤→ C1 Work Records Core ─→ C2 Task Core ─→ C3 Workstream
Gateway (/services/ppm/*) ─┘                                      │
                                                                  ├─→ C5 Effort Log (B2 kararına bağlı)
MOD-0021 AuditEvent v1 (kontrat) ────────────────────────────────┤
MOD-0023 Workflow Designer (hazır DEĞİL → yalın statü) ──────────┤─→ C4 Calendar ─→ C6 Meeting/Status Reports
MOD-0024 şablon kontratı (ileride) ──────────────────────────────┘
MOD-0027/0028/0048/0288: hedef kontratlar (follow-up)
MOD-0280 (repoda yok): Effort Log sınır kontratı — EA-TBD
MOD-0007 (pack yok): Decision link kontratı — follow-up
```

## 8. Ordered delivery sequence

`C1 → C2 → C3 → (C5, B2 kararına göre) → C4 → C6`. C7–C11 bu DCP'de başlamaz.
Her adımda: üye module pack `draft → approved/ready-for-dev` kapısı + kendi acceptance criteria'sı.

## 9. Prerequisites

1. Bu DCP `approved` / `ready-for-execution` (kullanıcı onayı).
2. B-serisi bloklayıcılar (§18): özellikle B6 (MVP rol matrisi) C1'den önce.
3. C1 module pack (`MOD-0117 PPM Work Records Core`) `approved`/`ready-for-dev`.
4. Gateway route + port rezervasyonu (`ports.md`) — C1 pack'in Gateway/Routing bölümünde, integration-agent ile.
5. Veri migration ayrı fazdır; B4 (kullanıcı eşleme) ve B5 (hedef tenant) kapanmadan **migration-ready denemez**.

## 10. Architecture decisions

- **Yeni mikroservis:** `services/Diten.PpmService/` — 5 katman + CQRS + 4 pipeline behavior + `Response<T>`; C1 pack onayına kadar **oluşturulmaz**. Port: `ports.md` bandından C1 aşamasında rezerve edilir (ASSUMPTION-4: 5060 önerisi).
- **Kimlik/Tenant:** `userId`/`tenantId` yalnız JWT claim + tenant middleware'den; client-supplied kimlik deseni (eski sistem) **yasak**. DTO'da TenantId yasak; cross-tenant 404.
- **Veri:** GUID (subtype-4), `IsDeleted`/`DeletedAt`, tenant-first compound index (ESR). Eski ObjectId/`Status` bool desenleri devralınmaz.
- **Statü yaşam döngüsü:** MOD-0023 hazır olana dek yalın `statusId` alanı; approval-engine yazımı yasak.
- **Concurrency:** MeetingReport/MeetingInvite `Version` optimistic concurrency davranışı korunur.
- **UI:** `_LayoutTenantShell.cshtml`; DataTable v2; SweetAlert2 (MOD-0013 standardı); CDN plugin yasağı; 7 dil l10n köprüsü. Eski JS/View **kopyalanmaz** — yalnızca iş kuralı referansı.
- **Takvimden kayıt kontratı:** Eski 6 örtüşen endpoint (`CreateTaskOrMeeting`, `UpsertTask`, `upsert-meeting`, `SaveTask`, `SaveMeeting`, `UpdateTaskOrMeeting`) tek konsolide `calendar/entries` kontratına indirgenir (C4 pack'inde).
- **Sırlar:** Eski appsettings sırları (SMTP şifresi, Google service-account key) repoya taşınmaz; mevcutları rotate edilmelidir.

## 11. Scope

Bkz. §4 (boundary) + §5 (üyeler). İlk uygulanabilir dilim (**minimum güvenli ilk faz = C1**):
Work Record CRUD (soft delete + audit alanları), tenant-scoped kod üretimi, PPM-yerel referans-veri
**okuma** uçları, JWT+`[HasPermission]`, gateway kontratı, Work Records DataTable v2 liste + Compact
Create/Edit (backend kontratı aynı pack'te hazır olduğu için), yalın statü yaşam döngüsü, 7 dil l10n.

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
4. MVP rol matrisi (B6) kim tarafından, ne zaman verilecek? → **EVET (2026-07-07, kullanıcı)** — §18 "B6 kararı" tablosu.
5. C5 Effort Log P1 mi P2 mi? → B2 EA kararına bağlı, 🔴 açık.
6. FullCalendar ve zengin editör vendor onayları? → C4/C6 pack'lerinden önce, 🔴 açık.

## 15. Gate criteria

- **DCP onay kapısı:** `draft → under-review → approved` yalnızca kullanıcı onayıyla.
- **Üye pack kapısı:** her üye `module-pack-standard.md` 20 bölümüyle ayrıca geçer; C1 için ek koşul: B6 rol matrisi.
- **Kimlik kapısı:** her FU için preflight exit 0 + registry satırı.
- **Naming kapısı:** §13/1 grep kuralı.
- **Kontrat kapısı:** backend kontratı olmayan hiçbir ekran operational UI kapsamına alınmaz (TimerPopup emsali).
- **APP-PPM-BUNDLE hizası:** OIDC/SSO, RBAC hooks, AuditEvent v1, Data Contracts, Correlation-ID maddeleri C1'den itibaren; Workflow APIs maddesi MOD-0023 hazır olduğunda.
- **Migration kapısı:** B4 + B5 kapanmadan migration scripti yazılmaz; Mongo GUID subtype-4 kuralı zorunlu.

## 16. Acceptance criteria

1. Bu DCP `approved` olduğunda: domain scaffold + registry rezervasyonu mevcut (bu materialization ile sağlandı), üye sırası ve sınırlar bağlayıcı.
2. C1 tamamlandığında: tenant-güvenli Work Records CRUD + liste UI'ı, `ppm.work-records.*` permission seed'i, gateway route'u ve 7 dil RESX'i canlı; hiçbir `Workflow*` adı üretilmemiş; approval-engine yok.
3. Her üye pack `done` olduğunda kendi acceptance criteria'sı + bu DCP'nin gate kriterleri birlikte sağlanmış.
4. Runtime davranışı bu DCP'nin kendisi tarafından hiçbir aşamada değiştirilmemiş (yalnızca üye pack'ler değiştirir).

## 17. Downstream business-module impacts

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
| B3 | MOD-0117 registry rezervasyonu + FU numaraları | Rezervasyonsuz pack açılamaz | Registry satırı + preflight | ✅ Parent rezervasyonu bu materialization'da; FU'lar 🔴 açık (pack authoring'de) |
| B4 | Eski kullanıcı Id ↔ AuthService eşlemesi | Migration-ready denemez | Eşleme stratejisi (ör. e-posta) | 🔴 AÇIK |
| B5 | PvPPM verisinin hedef tenant ataması | Migration scripti yazılamaz | Hedef tenant kararı | 🔴 AÇIK |
| B6 | RBAC rol × kaynak × aksiyon matrisi | Permission seed anlamlandırılamaz; implementation-ready denemez | Business rol matrisi (en az MVP: Admin/Member/Viewer) | ✅ **KAPANDI 2026-07-07** — MVP matrisi kullanıcı tarafından verildi (aşağıda "B6 kararı") |
| B7 | Karşılıksız UI kontratları (TimeTracker/Stop, UpdateTaskOrder, AI ExtractActions) | Kontratsız akışlar kapsama alınamaz | Kapsam kararı | ✅ **KAPANDI 2026-07-07** — ilk DCP'de faz-dışı |
| B8 | MOD-0023 hazır değil (review/planned) | PPM onay akışı yazamaz | Yalın statü + motor yasağı kuralı | ✅ **KAPANDI 2026-07-07** — kural kabul edildi (§10/§13) |

**Onaylanan kararlar (2026-07-07, kullanıcı):** domain `portfolio-delivery`; kısa kod `ppm`; "Workflow"
adlandırma yasağı; ilk faz `PPM Work Records Core`; Google/SignalR/AI/TimerPopup/Excel-import ilk faz dışı;
Project Effort Log ASSUMPTION-1 geçici kabul (MOD-0280 SoR kararı TBD); eski UI kopyalanmayacak;
approval-engine yazılmayacak (MOD-0023'e kadar yalın statü).

**B6 kararı — C1 MVP RBAC matrisi (2026-07-07, kullanıcı):**

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

> C1 module pack'te netleştirilecek iki tasarım noktası (karar değil, tasarım detayı):
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
- **ASSUMPTION-4 (Port):** `Diten.PpmService` için 5060 önerilir; kesin rezervasyon C1 aşamasında `ports.md`'ye
  işlenir (fiili fleet'te 5059 MDM kullanımı doğrulanarak). Risk: bant çakışması.

**🔴 TBD:** pack owner; FU numaraları; FullCalendar vendor onayı; zengin editör vendor kararı;
C5'in P1/P2 yerleşimi; geçici attachment ihtiyacı; MOD-0117'nin wave ataması (master plan/delivery board).

## 19. Future follow-ups

- MOD-0048'e lookup devri; MOD-0288 besleme kontratı; MOD-0007 Decision link kontratı; MOD-0028/0262 evidence-link entegrasyonu.
- MOD-0023 hazır olduğunda yalın statüden approval kontratına geçiş pack'i.
- MOD-0280 geldiğinde Effort Log ↔ Time Entry SoR kontratı / devri.
- Veri migration fazı (Id yeniden anahtarlama GUID subtype-4, tenant ataması, kullanıcı eşleme) — B4/B5 kapandığında ayrı plan.
- Google/SignalR/AI/TimerPopup/Excel-import için kapsam kararları (ayrı tur).
- Blueprint geniş kapsamı (demand intake, benefits, capacity) için ayrı FU dalgaları.

## 20. Audit and reconciliation notes

- 2026-07-07: Migration feasibility audit (read-only) — eski backend/UI haritası, endpoint/view mapping, bloklayıcılar.
- 2026-07-07: Governance audit (read-only) — MOD-0117 preflight exit 0; registry/master plan'da PPM yokluğu; MOD-0023/0280 sınır analizi.
- 2026-07-07: Kullanıcı karar turu — B1/B7/B8 kapandı; ASSUMPTION-1 geçici kabul; DCP taslağı onaylanarak materialize edildi (`draft`).
- Reconciliation: implementasyon fazları sonrası bu bölüme fiili/plan sapmaları işlenecek (CAP-001 §5 `reconciled`).
