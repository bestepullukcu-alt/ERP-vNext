# AGENTS.md — Diten ERP vNext Execution Contract

Bu dosya, Claude Code, Codex ve diğer AI ajanlarının repo genelinde uyması gereken yürütme kontratıdır. Repo root'tan çalışıldığında otomatik yüklenir.

> **Otorite:** Bu dosya, `.antigravity/` içindeki global standartlardan üstündür. Domain veya module seviyesinde yazılmış bir kural bu dosyadan üstündür.

---

## 1. Yetki Hiyerarşisi (Authority Order)

Bir talep birden fazla kaynakta yer alıyorsa, en spesifik katman kazanır:

1. **Module Pack** — `execution/domains/{domain}/module-packs/{ID}.md`
2. **Domain Config** — `execution/domains/{domain}/domain-config.md`
3. **AGENTS.md** — bu dosya (repo root)
4. **Global Engineering System** — `.antigravity/` (ajanlar, kurallar, workflow'lar, skill'ler)
5. **Archive / dış referans** — otorite değil, sadece referans

Çakışma varsa yukarıdaki sırayla karar verilir. Ajanlar bu sırayı bozamaz.

---

## 2. Proje Klasör Yapısı

```
ERP-vNext/
├── AGENTS.md                            (bu dosya)
├── .antigravity/                        Global Engineering System (ajanlar, kurallar, workflow'lar)
├── execution/                           Domain ve module execution katmanı
│   └── domains/
│       ├── developer-enablement/        (DEVEN — mevcut; Diten.DevEnablementService golden references)
│       ├── master-data-management/       (MDM — governance scaffold mevcut; production service yok)
│       ├── platform-shared-services/    (PSS — mevcut; Diten.Platform + Diten.AuthService)
│       └── portfolio-delivery/          (PPM — governance scaffold mevcut; MOD-0117; production service yok)
│       # planned, not scaffolded yet:
│       # enterprise-strategy-business-performance (ESBP)
├── services/                            .NET 8 mikroservisler
│   ├── Diten.AuthService/
│   ├── Diten.DevEnablementService/
│   ├── Diten.Platform/
│   ├── Diten.Platform.Common/
│   └── Diten.EnterpriseStrategyService/
│   # Diten.MdmService/                   MDM service scaffold henüz oluşturulmadı
│   # Diten.PpmService/                   PPM service henüz scaffold edilmedi; C1 "PPM Work Records Core" module pack onayı olmadan oluşturulmaz (DCP-003)
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

## 3. Port Şeması

| Servis | Port | Proje Yolu |
|--------|------|------------|
| Gateway (Ocelot) | 5000 | `gateway/Diten.ApiGateway` |
| Frontend (Diten.Web) | 5001 | `frontend/Diten.Web` |
| Auth Service | 5056 | `services/Diten.AuthService/src/Diten.AuthService.Api` |
| Platform Service | 5057 | `services/Diten.Platform/src/Diten.Platform.API` |
| DevEnablement Service | 5058 | `services/Diten.DevEnablementService/src/Diten.DevEnablementService.Api` |
| MongoDB | 27017 | yerel çalışmalı |

**Kural:** Frontend (5001) asla doğrudan servis portlarına (5056/5057/5058) istek atmaz. Her istek Gateway (5000) üzerinden geçer.

Detay: [.antigravity/rules/ports.md](.antigravity/rules/ports.md)

---

## 4. Protected Paths (Dokunulmaz Alanlar)

Ajanlar aşağıdaki yollara **asla** dokunmaz (kullanıcı açıkça talep etmedikçe):

| Path | Neden |
|------|-------|
| `.antigravity/**` | Global engineering system — değişiklik önce kullanıcı onayı ister ([working-agreement](.antigravity/rules/GEMINI.md)) |
| `frontend/Diten.Web/Controllers/Archive/**` | Legacy kontrolcüler (FROZEN) |
| `frontend/Diten.Web/Views/Archive/**` | Legacy sayfalar (FROZEN) |
| `frontend/Diten.Web/Views/Shared/_Layout.cshtml` | Archive için FROZEN layout. Yeni modüller shell tipine göre `_LayoutPlatformAdmin.cshtml` veya `_LayoutTenantShell.cshtml` kullanır |
| `gateway/Diten.ApiGateway/.../ocelot.json` | Sadece `integration-agent` modifiye eder (rota ekleme kuralı: [.antigravity/rules/routes.md](.antigravity/rules/routes.md)) |
| Diğer domain'lerin `services/` klasörleri | Bir module pack yalnızca kendi domain'inin servisine dokunabilir |

Module pack'ler kendi `Protected Paths` bölümünde ek kısıtlama getirebilir.

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
# Her servisin kendi test projeleri varsa onların yolu kullanılır
```

### DataTable Kontrat Doğrulama (Frontend)
```bash
python3 .antigravity/scripts/verify_datatable_page.py . --area {AreaName} --module {ModuleName} --reference slim|compact
```

---

## 6. Runtime Kararları (Tüm Modüller İçin Geçerli)

Bu kararlar repo genelinde **zorunludur**. Bir modül bunlardan muaf olmak isterse domain-config veya module pack içinde **açıkça** belirtmeli ve gerekçelendirmelidir.

| Karar | Değer | Kaynak |
|-------|-------|--------|
| Veri saklama | MongoDB (tek DB, multi-tenant) | [.antigravity/rules/multi-tenancy.md](.antigravity/rules/multi-tenancy.md) |
| Tenant İzolasyonu | `TenantId` **zorunlu** — cross-tenant 404 döner | [.antigravity/rules/multi-tenancy.md](.antigravity/rules/multi-tenancy.md) |
| Soft Delete | `IsDeleted`, `DeletedAt` **zorunlu** | [.antigravity/rules/entity-base-template.md](.antigravity/rules/entity-base-template.md) |
| Auth | JWT + RBAC (`[HasPermission]`) | [.antigravity/rules/security-jwt.md](.antigravity/rules/security-jwt.md) |
| Mimari | 5 katman (Api/Application/Domain/Persistence/Infrastructure) + CQRS (MediatR) | [.antigravity/rules/erp-architecture.md](.antigravity/rules/erp-architecture.md) |
| API Yanıt | `Response<T>` envelope + `CustomBaseController` | [.antigravity/rules/response-envelope.md](.antigravity/rules/response-envelope.md) |
| Pipeline Behaviors | 4 zorunlu (Validation, Logging, Exception, Performance) | [.antigravity/rules/pipeline-behaviors.md](.antigravity/rules/pipeline-behaviors.md) |
| Yerelleştirme | 7 dil (en, fr, es, zh, ar, ru, tr) — `.resx` + `window.L10n` bridge | [.antigravity/rules/localization-standard.md](.antigravity/rules/localization-standard.md) |
| UI Layout | Admin modülleri `_LayoutPlatformAdmin.cshtml`; tenant modülleri `_LayoutTenantShell.cshtml`; `_Layout.cshtml` FROZEN | [.antigravity/rules/views-organization.md](.antigravity/rules/views-organization.md) |
| DataTable | v2 kontratı zorunlu (`data-dt-standard="v2"`) + Golden Slim/Compact seçimi | [.antigravity/rules/frontend-datatable-template.md](.antigravity/rules/frontend-datatable-template.md) |
| Modaller & Uyarılar | Premium SweetAlert2 Standardı (MOD-0013) zorunlu | [.antigravity/rules/premium-modal-standard.md](.antigravity/rules/premium-modal-standard.md) |

### Golden Reference DataTable Kararı

DataTable tabanlı yeni modüllerde resmi referans iki canlı DevEnablement modülüdür:

| Form alan sayısı | Referans | UI surface |
|---|---|---|
| `8 ve altı` | `GoldenReferenceSlim` | Index içinde create/edit offcanvas |
| `8'den fazla` | `GoldenReferenceCompact` | Ayrı `Create.cshtml`, `Edit.cshtml`, `Details.cshtml`, `_Form.cshtml` |

Alan sayımı yalnızca create/edit formunda kullanıcının doldurduğu modül alanlarıdır. `Id`, `TenantId`, `IsDeleted`, `CreatedAt`, `UpdatedAt`, audit alanları ve DataTable checkbox/action kolonları sayılmaz.

---

## 7. Ajan Seçimi ve Varsayılan Entry Point

Varsayılan geliştirme entry point'i: **`@orchestrator`**

Module pack hazırlama entry point'i: **`module-pack-author`** veya **`/prepare-module-pack`**

Çok modüllü / cross-cutting iş entry point'i: **`/prepare-capability-pack`** — production'dan önce bir **Delivery Capability Pack** hazırlar ([CAP-001](.antigravity/rules/capability-pack-standard.md)). Tek modül yeterliyse `/prepare-module-pack`'e döner.

Salt-okunur denetim entry point'i: **`/read-only-audit`** ([read-only-audit.md](.antigravity/workflows/read-only-audit.md)) — repoyu değiştirmeden mimari/governance denetimi.

Tüm git operasyonları (branch, staging, commit, push) [GIT-002 git-safety.md](.antigravity/rules/git-safety.md) kapılarına tabidir.

Orchestrator çağrıldığında Aşama 0'da şu dosyaları okur:
1. `AGENTS.md` (bu dosya)
2. İlgili `execution/domains/{domain}/domain-config.md`
3. Varsa `execution/domains/{domain}/module-packs/{ID}.md`
4. Görev tipine uygun `.antigravity/workflows/*.md`

**Kural:** `@orchestrator` module pack oluşturmaz. Yeni modül geliştirmesi yalnızca mevcut ve kullanıcı tarafından onaylanmış (`approved` veya `ready-for-dev`) module pack üzerinden başlar. Module pack yoksa veya `draft` durumundaysa kod yazılmaz; kullanıcı önce `/prepare-module-pack` veya `module-pack-author` ile module pack hazırlamaya yönlendirilir.

Ajan listesi ve sorumlulukları: [.antigravity/agents/](.antigravity/agents/)
Prompt yazma rehberi: [.antigravity/PROMPT-GUIDE.md](.antigravity/PROMPT-GUIDE.md)

---

## 8. Çalışma Akışı (Domain / Module Oluşturma)

İki ana yol var. Detaylı walkthrough: [docs/agent-usage-guide.md](docs/agent-usage-guide.md).

### Yol A — Sıfırdan yeni domain (nadir)

1. `execution/domains/{name}/` klasörü kur: `README.md` + `domain-config.md` + `module-packs/`
2. `domain-config.md`'de **sadece domain'e özel kararlar** yaz (in-scope modüller, repo scope, protected paths, ownership boundaries, runtime decisions). Engineering kuralları için `.antigravity/rules/`'a link ver — **içeriği tekrarlama**.
3. AGENTS.md §2 (klasör yapısı) + §9 (branch kodu) güncelle. Platform/Admin domain'i ise [execution/portfolio/master-development-plan.md](execution/portfolio/master-development-plan.md) Section 2 (Module Inventory) ve [execution/registries/module-id-registry.md](execution/registries/module-id-registry.md) içine ekle.
4. İlk modülü Yol B ile yaz.

> Şablon: [execution/domains/platform-shared-services/](execution/domains/platform-shared-services/) (README + domain-config kanonik örnek).

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

Fix/refactor işleri için: [.antigravity/rules/GEMINI.md](.antigravity/rules/GEMINI.md) working-agreement akışı uygulanır (önce kod düzeltilir, onay alındıktan sonra `.antigravity` etkisi kontrol edilir).

---

## 9. Branch Adlandırma Kuralı

```
feature/{domain-kısa}/{module-id}-{slug}
```

- `domain-kısa`: `mdm` | `pss` | `deven` | `esbp` | `ppm`
- `module-id`: `mdm-001`, `pss-002`, vb. (küçük harf)
- `slug`: 2-4 kelimelik kısa isim

Örnekler:
- `feature/mdm/mdm-001-product-management`
- `feature/pss/pss-001-identity-access`
- `feature/esbp/esbp-001-strategy-core`
- `feature/ppm/mod-0117-work-records-core`

Yedekleme branch'leri için ayrı kural: [.antigravity/rules/git-backup-policy.md](.antigravity/rules/git-backup-policy.md)

---

## 10. Module Pack Zorunluluğu

**Yeni bir modül veya büyük bir feature'ın kodu module pack olmadan yazılamaz.**

**Onay kapısı:** `draft` module pack yalnızca planlama dokümanıdır. Kod üretimi için status `approved` veya `ready-for-dev` olmalıdır.

Module pack minimum içermeli:
- YAML frontmatter (id, status, owner, branch)
- Owned objects
- Repo scope (dokunulacak klasörler)
- Protected paths (dokunulmayacak klasörler)
- Acceptance criteria (test edilebilir)
- Test expectations
- DataTable modülleri için form alan sayısı ve `golden_reference: slim|compact`

Şablon: [.antigravity/rules/module-pack-standard.md](.antigravity/rules/module-pack-standard.md)

---

## 11. Terim Sözlüğü

| Terim | Anlam |
|-------|-------|
| **Global Engineering System** | `.antigravity/` — tüm projeler arası yeniden kullanılabilir katman |
| **Domain** | `execution/domains/{name}/` — bir iş alanı (DEVEN, PSS; MDM/ESBP planlı) |
| **Module Pack** | `execution/domains/{d}/module-packs/{ID}.md` — tek bir modülün kimlik + AC dosyası |
| **Domain Config** | `execution/domains/{d}/domain-config.md` — domain sınırları ve kararlar |
| **Master Development Plan** | `execution/portfolio/master-development-plan.md` — High-level wave planı ve modül envanteri (eski monolith `docs/platform/master-plan.md` yerine) |
| **Platform Delivery Board** | `execution/delivery/platform-delivery-board.md` — Aktif iş/hardening takibi ve status panosu |
| **Module ID Registry** | `execution/registries/module-id-registry.md` — Tüm modüllerin canonical ID listesi ve eşleşmeleri |
| **Workflow** | `.antigravity/workflows/*.md` — yeniden kullanılabilir tarif (ör. `/add-module`) |
| **Delivery Capability Pack** | `execution/portfolio/delivery-capability-packs/DCP-{NNN}-{slug}.md` — çok modüllü / cross-cutting teslimat için sınır + sıra + sahiplik orkestrasyon sözleşmesi ([CAP-001](.antigravity/rules/capability-pack-standard.md)). Bir runtime entity, module pack veya MOD-0014 runtime Capability Group **değildir**; yalın `Capability` adıyla anılmaz |

---

## 12. Adaptasyon Notu

Bu repo, [Layered Agent + Domain Package Model SOP v2.1](docs/sop/upstream/) temelinde çalışır; fakat projemize özgü farklar vardır:

- `batches/` katmanı **kullanılmaz** (`.antigravity/workflows/add-module.md` zaten phase orchestration sağlar)
- `snapshots/` katmanı **kullanılmaz** (git history + `docs/audits/` bu işi yapar)
- `controls/` ve `decisions/` katmanları **kullanılmaz** (engineering standartları `.antigravity/rules/`'de, scope/MVP kararları `execution/portfolio/master-development-plan.md`'de — tarihsel klasörler `archive/domains/` altına taşındı)
- Module ID registry canonical kaynaktır. Yeni ERP product module formatı `MOD-NNNN-slug`; follow-up formatı `MOD-NNNN-FUxx-slug`; Delivery Capability Pack formatı `DCP-NNN-slug`; Developer Enablement golden reference formatı `DEV-NNNN-slug`. Tarihsel/legacy formatlar migration boyunca geçerli kalır ve registry cleanup backlog'u üzerinden izlenir; toplu rename yapılmaz.
- Klasör yapısı `services/` + `frontend/` + `gateway/` (SOP'taki `src/Backend/` + `src/Frontend/` yerine)

SOP'tan sapmaların tam listesi, yukarıdaki hiyerarşi ve proje bazlı SOP dosyaları (`docs/sop/upstream/`) üzerinden takip edilir.


---

## Module ID Canonicalization Gate (DCP-002)

The Blueprint (`docs/System Capability & Implementation Blueprint - master 7.xlsx` :: `Blueprint_Data`) is the canonical authority for every `MOD-xxxx` ID and canonical name. Before creating or reserving any `MOD-xxxx` (new module, FU/child, or reservation):

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
