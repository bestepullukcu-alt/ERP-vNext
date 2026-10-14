# AGENTS.md — Başvuru Bölümleri

Bu belge `AGENTS.md`'nin devamıdır ve **aynı yetki seviyesindedir** (`AGENTS.md` §1, 3. sıra). Bölüm numaraları `AGENTS.md` ile aynıdır.
Metin `AGENTS.md`'den kelimesi kelimesine taşındı (İ5-B, 2026-10-15). Yalnız şu düzeltmeler yapıldı: §2'ye klasörlerden ölçülen
güncel servis ve domain listesi eklendi (eski ağaç tarihsel olarak duruyor); §8'deki `docs/agent-usage-guide.md` bağlantısı
dosyanın taşındığı `docs/guides/operations/agent-usage-guide.md`'ye, §12'deki silinmiş `docs/sop/upstream/` bağlantısı taşındığı `docs/guides/sop-upstream/`'a yönlendirildi.

---

## 2. Proje Klasör Yapısı

**Güncel (2026-10-15, klasörlerden ölçüldü):**

- `services/` — 16 servis: `Diten.AuthService`, `Diten.CrmService`, `Diten.DevEnablementService`, `Diten.EnterpriseStrategyService`, `Diten.HcmService`, `Diten.HumanCapitalService`, `Diten.InventoryService`, `Diten.ManagementGovernanceService`, `Diten.ManufacturingService`, `Diten.MdmService`, `Diten.Platform`, `Diten.PpmService`, `Diten.ProcurementService`, `Diten.PvgService`, `Diten.SupplyChainService`, `Diten.TalentEcosystemService`; 3 ortak kütüphane: `Diten.Building.Blocks`, `Diten.Platform.Common`, `Diten.Platform.Contracts`
- `execution/domains/` — 11 domain: `commercial-suite`, `developer-enablement`, `enterprise-strategy-business-performance`, `human-capital-management`, `management-governance`, `master-data-management`, `pharmacovigilance`, `platform-shared-services`, `portfolio-delivery`, `procurement`, `supply-chain-execution`

### Taşınmadan önceki metin (tarihsel)

> Aşağıdaki ağaçtaki servis ve domain notları yazıldıkları tarihteki durumu anlatır. Bugün MDM, MG ve PVG servisleri dahil
> yukarıdaki güncel liste geçerlidir; ağaçtaki onay kuralları (module pack ve kullanıcı onayı) yeni servis açarken geçerliliğini korur.

```
ERP-vNext/
├── AGENTS.md                            (bu dosya)
├── .antigravity/                        Global Engineering System (ajanlar, kurallar, workflow'lar)
├── execution/                           Domain ve module execution katmanı
│   └── domains/
│       ├── developer-enablement/        (DEVEN — mevcut; Diten.DevEnablementService golden references)
│       ├── enterprise-strategy-business-performance/ (ESBP — governance scaffold mevcut; Diten.EnterpriseStrategyService)
│       ├── management-governance/       (MG — governance scaffold; DWS + BPM; production service yok)
│       ├── pharmacovigilance/           (PVG — governance scaffold mevcut; DCP-004; production service yok)
│       ├── master-data-management/       (MDM — governance scaffold mevcut; production service yok)
│       ├── platform-shared-services/    (PSS — mevcut; Diten.Platform + Diten.AuthService)
│       └── portfolio-delivery/          (PPM — governance scaffold mevcut; MOD-0117; production service yok)
├── services/                            .NET 8 mikroservisler
│   ├── Diten.AuthService/
│   ├── Diten.DevEnablementService/
│   ├── Diten.Platform/
│   ├── Diten.Platform.Common/
│   └── Diten.EnterpriseStrategyService/
│   # Diten.ManagementGovernanceService/  MG service henüz scaffold edilmedi; approved module pack + açık kullanıcı onayı gerekir
│   # Diten.MdmService/                   MDM service scaffold henüz oluşturulmadı
│   # Diten.PpmService/                   DCP-006 + OD-03/04 + approved Slice 2 pack + açık kullanıcı onayı gerekir
│   # Diten.PvgService/                   PVG service scaffold henüz oluşturulmadı; DCP-004 approved/ready-for-execution + üye module pack approved/ready-for-dev onayı olmadan oluşturulmaz
├── frontend/                            Razor MVC + Sneat PRO + DataTables v2
│   └── Diten.Web/
├── gateway/                             Ocelot API Gateway
│   └── Diten.ApiGateway/
└── docs/                                Dokümantasyon ve audit raporları
    └── audits/
```

**ÖNEMLİ:** Bu proje `src/Backend/` veya `src/Frontend/` yapısı **kullanmaz**. Yukarıdaki gerçek yapıya uyulmalıdır.

**MDM notu:** `execution/domains/master-data-management/` governance scaffold olarak mevcuttur. Bu milestone
`services/Diten.MdmService/` production service scaffold'ı oluşturmaz. MDM service implementation yalnızca ilgili
module pack `approved` / `ready-for-dev` olduktan ve açık kullanıcı onayı verildikten sonra ele alınabilir.

---

## 3. Port Şeması — tarihli atama notları

Tablo ve kural `AGENTS.md` §3'tedir; aşağıdaki notlar oradan kelimesi kelimesine taşındı.

> **Not (2026-09-11):** Mikroservis bandı 5011–5060'tan **5061'e uzatıldı** — `SupplyChain Service` (DCP-009, supply-chain-execution domain, kullanıcı onaylı). Sonraki mikroservisler 5062+ kullanır.
> **Not (2026-09-16, revize 2026-09-17):** `Procurement Service` (DCP-010, procurement domain, kullanıcı onaylı) **port 5065**. İlk atama 5062'ydi; 2026-09-17 ölçümünde 5062 çakışması bulundu (OD-5): 5062'yi `Diten.DataKnowledgeService` (çalışıyor) + `Diten.PpmService` (config) kullanıyor; 5063 `HumanCapitalService`, 5064 `TalentEcosystemService`. En yeni servis (Procurement, henüz fleet'te çalışmıyor) en düşük boş porta (**5065**) taşındı — çalışan/başka-domain servisleri bozulmadı. **Sonraki mikroservisler 5066+ kullanır.**
> **Not (2026-09-30):** `Inventory Service` (DCP-009 OD-5, supply-chain-execution domain, kullanıcı onaylı) **port 5066** — MOD-0173…0177 (stok defteri + izlenebilirlik) `Diten.SupplyChainService`'ten ayrılıp kendi servisine alındı. 5066 ölçümü: HEAD'de ve tüm remote dallarda config/launchSettings/script kullanımı yok. **Sonraki mikroservisler 5067+ kullanır.**
> ⚠ **Drift (2026-09-30 ölçümü):** `services/Diten.CrmService/.../launchSettings.json` **5061** kullanıyor — tabloda `SupplyChain Service`'e ayrılı port. SupplyChainService henüz main'de değil; MVP-6 servisi main'e inmeden önce çözülmeli (EA / MVP-6 kararı).
> **Not (2026-10-12, sahip kararı OD-P1 — yukarıdaki drift'i kapatır):** MVP-6 main'e indiğinde `SupplyChain Service` fiilen **5066**'da çalışıyordu (`appsettings.json` `Urls`, gateway `/api/shipment-bundle` → 5066) ve `Inventory Service` ile çakıştı. Karar: SupplyChain **5066**'da kalır, `Inventory Service` **5067**'ye taşındı (WP-MVP1-SYNC1, gateway'de bizim 8 rotamız 5067'de). 5061 = **CRM Service** (fiili kullanım). Kanıt: `docs/roadmap/plans/mvp1-mvp6-integration-2026-10-12.md`. **Sonraki mikroservisler 5068+ kullanır.**
> **Not (2026-10-06, revize 2026-10-12):** `Manufacturing Service` (MOD-0193 BOM & Routings, MVP-3, DCP-009, ürün sahibi onaylı ayrı servis) **port 5070**. İlk atama 5067'ydi; OD-P1 (2026-10-12) ile **5067 Inventory'nin**, Manufacturing **5070**'e taşındı (5070 ölçümü: tüm remote dallarda config/launchSettings/script kullanımı yok). 5068 ve 5069 boş kalır.
> ⚠ **Drift (EA reconciliation gerek):** yukarıdaki tablo `PpmService (5062)`, `DataKnowledgeService (5062, çalışıyor)`, `HumanCapitalService (5063)`, `TalentEcosystemService (5064)` satırlarını içermiyor; bu servisler §3 port authority'sine kayıtlı değil ve 5062 config-seviyesinde Ppm+Procurement tarafından çift-talep edildi. Bu satırların canonical port tahsisi + §3'e eklenmesi ilgili domain owner'lar/EA kararıdır (procurement sınırı dışı).

---

## 5. Build ve Test Komutları

### Tüm servisleri build et
```bash
./run_all.sh          # Tüm servisleri derler ve çalıştırır (log prefix'li)
./run-diten.sh        # Mac Terminal tab'larıyla servisleri başlatır
```

### Bireysel servis build
```bash
dotnet build services/Diten.AuthService/src/Diten.AuthService.Api/Diten.AuthService.Api.csproj -c Debug
dotnet build services/Diten.DevEnablementService/src/Diten.DevEnablementService.Api/Diten.DevEnablementService.Api.csproj -c Debug
dotnet build services/Diten.Platform/src/Diten.Platform.API/Diten.Platform.API.csproj -c Debug
dotnet build gateway/Diten.ApiGateway/Diten.ApiGateway.csproj -c Debug
dotnet build frontend/Diten.Web/Diten.Web.csproj -c Debug
```

### Test komutları
```bash
dotnet test services/Diten.AuthService
dotnet test services/Diten.EnterpriseStrategyService
dotnet test services/Diten.Platform
dotnet test tests/architecture/TenantArchitecture.ArchitectureTests
# Her servisin kendi test projeleri varsa onların yolu kullanılır
```

> ⚠ `tests/architecture` **`services/*` altında değildir** ve bu yüzden yıllarca bu listede yer almadı.
> İçindeki muhafızlar repo genelini tarar (Mongo test veritabanı deseni — [DB-010](rules/mongo-indexing.md),
> `MongoClient` kurulum yeri). CI onu her push'ta koşuyor (`scripts/run_phase1_gates.sh`); yerelde de koşulmalı,
> yoksa ihlal ancak PR açıldığında görünür.

### DataTable Kontrat Doğrulama (Frontend)
```bash
python3 .antigravity/scripts/verify_datatable_page.py . --area {AreaName} --module {ModuleName} --reference slim|compact [--data-mode server|client] [--format gaps]
```

> Eski bir liste ekranına dokunan görev **dokunma protokolüne** tabidir (`frontend-datatable-template.md` → Dokunma protokolü):
> sapmalar `--format gaps` ile listelenir, sahibe sorulur, sessizce ne düzeltilir ne atlanır. Claude Code'da `.claude/settings.json`
> PostToolUse kancası (`list_screen_touch_hook.py`) bunu düzenlemeden hemen sonra otomatik koşturur.

---

## 8. Çalışma Akışı (Domain / Module Oluşturma)

İki ana yol var. Detaylı walkthrough: [docs/guides/operations/agent-usage-guide.md](../docs/guides/operations/agent-usage-guide.md).

### Yol A — Sıfırdan yeni domain (nadir)

1. `execution/domains/{name}/` klasörü kur: `README.md` + `domain-config.md` + `module-packs/`
2. `domain-config.md`'de **sadece domain'e özel kararlar** yaz (in-scope modüller, repo scope, protected paths, ownership boundaries, runtime decisions). Engineering kuralları için `.antigravity/rules/`'a link ver — **içeriği tekrarlama**.
3. AGENTS.md §2 (klasör yapısı) + §9 (branch kodu) güncelle. Platform/Admin domain'i ise [execution/portfolio/master-development-plan.md](../execution/portfolio/master-development-plan.md) Section 2 (Module Inventory) ve [execution/registries/module-id-registry.md](../execution/registries/module-id-registry.md) içine ekle.
4. İlk modülü Yol B ile yaz.

> Şablon: [execution/domains/platform-shared-services/](../execution/domains/platform-shared-services/) (README + domain-config kanonik örnek).

### Yol B — Mevcut domain'de yeni modül (her gün)

**Aşama 1 — Module Pack Hazırlık:**
1. `/prepare-module-pack` çağır (modül adı + domain + servis + shell + form alan sayısı + iş kuralları)
2. `module-pack-author` ajanı `execution/domains/{domain}/module-packs/{ID}-{slug}.md` dosyasını `status: draft` ile üretir
3. Kullanıcı incelemesi → `status: approved` veya `ready-for-dev`

**Aşama 2 — Geliştirme:**
4. Branch aç: `feature/{domain-kısa}/{module-id}-{slug}` (örn: `feature/pss/pss-001-platform-administrators`)
5. `@orchestrator {pack-yolu}` → `/add-module` workflow'u Phase 0 → 6 çalışır
6. Test + `status: in-progress → review → done`
7. PR aç, merge et. Module pack silinmez, `done` olarak kalır.

> `@orchestrator` module pack oluşturmaz. Pack yoksa veya `draft` ise kullanıcıyı `/prepare-module-pack`'e yönlendirir.

Fix/refactor işleri için: [.antigravity/rules/GEMINI.md](rules/GEMINI.md) working-agreement akışı uygulanır (önce kod düzeltilir, onay alındıktan sonra `.antigravity` etkisi kontrol edilir).

---

## 11. Terim Sözlüğü

| Terim | Anlam |
|-------|-------|
| **Global Engineering System** | `.antigravity/` — tüm projeler arası yeniden kullanılabilir katman |
| **Domain** | `execution/domains/{name}/` — bir iş alanı (DEVEN, PSS; MDM/ESBP planlı) |
| **Module Pack** | `execution/domains/{d}/module-packs/{ID}-{slug}.md` — tek bir modülün kimlik + AC dosyası |
| **Domain Config** | `execution/domains/{d}/domain-config.md` — domain sınırları ve kararlar |
| **Master Development Plan** | `execution/portfolio/master-development-plan.md` — High-level wave planı ve modül envanteri (eski monolith `docs/platform/master-plan.md` yerine) |
| **Platform Delivery Board** | `execution/delivery/platform-delivery-board.md` — Aktif iş/hardening takibi ve status panosu |
| **Module ID Registry** | `execution/registries/module-id-registry.md` — Tüm modüllerin canonical ID listesi ve eşleşmeleri |
| **Workflow** | `.antigravity/workflows/*.md` — yeniden kullanılabilir tarif (ör. `/add-module`) |
| **Delivery Capability Pack** | `execution/portfolio/delivery-capability-packs/DCP-{NNN}-{slug}.md` — çok modüllü / cross-cutting teslimat için sınır + sıra + sahiplik orkestrasyon sözleşmesi ([CAP-001](rules/capability-pack-standard.md)). Bir runtime entity, module pack veya MOD-0014 runtime Capability Group **değildir**; yalın `Capability` adıyla anılmaz |

---

## 12. Adaptasyon Notu

Bu repo, [Layered Agent + Domain Package Model SOP v2.1](../docs/guides/sop-upstream/) temelinde çalışır; fakat projemize özgü farklar vardır:

- `batches/` katmanı **kullanılmaz** (`.antigravity/workflows/add-module.md` zaten phase orchestration sağlar)
- `snapshots/` katmanı **kullanılmaz** (git history + `docs/audits/` bu işi yapar)
- `controls/` ve `decisions/` katmanları **kullanılmaz** (engineering standartları `.antigravity/rules/`'de, scope/MVP kararları `execution/portfolio/master-development-plan.md`'de — tarihsel klasörler `archive/domains/` altına taşındı)
- Module ID registry canonical kaynaktır. Yeni ERP product module formatı `MOD-NNNN-slug`; follow-up formatı `MOD-NNNN-FUxx-slug`; Delivery Capability Pack formatı `DCP-NNN-slug`; Developer Enablement golden reference formatı `DEV-NNNN-slug`. Tarihsel/legacy formatlar migration boyunca geçerli kalır ve registry cleanup backlog'u üzerinden izlenir; toplu rename yapılmaz.
- Klasör yapısı `services/` + `frontend/` + `gateway/` (SOP'taki `src/Backend/` + `src/Frontend/` yerine)

SOP'tan sapmaların tam listesi, yukarıdaki hiyerarşi ve proje bazlı SOP dosyaları (`docs/guides/sop-upstream/`) üzerinden takip edilir.


---

## Module ID Canonicalization Gate (DCP-002)

The Blueprint (`docs/System Capability & Implementation Blueprint - master 8.1.xlsx` :: `Blueprint_Data`) is the canonical authority for every `MOD-xxxx` ID and canonical name. Master 7 is its historical predecessor only. Before creating or reserving any `MOD-xxxx` (new module, FU/child, or reservation):

1. **Blueprint lookup** — the ID + canonical name must exist in `Blueprint_Data`, or the ID must be an FU/child of an existing Blueprint MOD parent.
2. **Registry collision** — it must not already map to a different capability in `execution/registries/module-id-registry.md`.
3. **Canonical-name validation** — the pack `name` must match the Blueprint canonical name (or an approved alias).
4. **Parent/FU/child decision** — decide explicitly whether the work is a new module or an FU/child of an existing module before minting an ID.
5. **Repo-only reservation** — a capability absent from the Blueprint requires an explicit Enterprise Architect reservation recorded in the registry; no placeholder or next-free ID may be invented.
6. **Preflight (fail-closed)** — run `python3 .antigravity/scripts/verify_module_id.py . --check-id MOD-XXXX --name "Canonical Name" [--parent MOD-YYYY] [--repo-only]`. A non-zero exit BLOCKS pack creation.

Authority and policy: `execution/portfolio/delivery-capability-packs/DCP-002-module-identity-canonicalization.md`. Legacy (`PSS-*`, `NEW-*`) and repo-only IDs are valid only as deprecated aliases pending Enterprise Architect reservation.

### CAND-CAP candidate namespace (DCP-002)

When the Blueprint has no capability and no existing MOD/FU fits, use a temporary candidate identity `CAND-CAP-####` — a governance/documentation identity ONLY, never written into runtime literals. Validate with the fail-closed candidate gate:

`python3 .antigravity/scripts/verify_module_id.py . --candidate CAND-CAP-#### --name "Capability Name"`

Lifecycle: `legacy ID → deprecated alias to CAND-CAP-#### → later deprecated alias to the EA-assigned canonical MOD-xxxx`. New-module identity rule: **Blueprint lookup → existing MOD or FU when available → otherwise CAND-CAP only → never invent a MOD / PSS / NEW identity.**

---

## Yükleme mimarisi (tarihçe)

bu dosyayı otomatik yüklemez** — yalnız `CLAUDE.md`'yi yükler. 2026-09-08'de
canlı oturumda ölçüldü: bağlamdaki tek proje dosyası `MEMORY.md`'ydi; bu dosya da,
`.antigravity/` altındaki 41 kural / 20 ajan / 18 akış da yoktu.

Bu yüzden repo kökünde **kısa bir `CLAUDE.md`** durur ve tek işi okuyucuyu buraya
göndermektir. Sembolik bağ denendi ve **geri alındı**: ekipte Windows kullanan var, ve
`core.symlinks=false` altında bağ "AGENTS.md" yazan düz bir metin dosyasına dönüşerek
sessizce işlevsiz kalıyor — dosya var görünür, içerik yoktur.

`CLAUDE.md` bir kopya değildir ve olmamalıdır; çakışmada **bu dosya kazanır**.

### CLAUDE.md neden var

`AGENTS.md` yıllardır "Claude Code tarafından otomatik yüklenir" diyordu ve bu doğru
değildi. Ekip Antigravity'den Claude Code'a geçtiğinde `.antigravity/` altındaki her şey
sessizce görünmez oldu — dosyalar duruyordu, kimse okumuyordu.

Bu dosya o boşluğu kapatır. Tek işi seni `AGENTS.md`'ye götürmek; büyümeye başlarsa
amacını kaybeder.
