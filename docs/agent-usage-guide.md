# ERP-vNext Agent Kullanım Rehberi

Bu rehber iki temel akışı detaylandırır: **Yol A — Sıfırdan yeni domain açma** (nadir) ve **Yol B — Mevcut domain'de yeni modül yazma** (her gün). Module pack güvenli aşamalar üzerinden yürütülür: önce sözleşme, sonra geliştirme.

> **Otorite hiyerarşisi (her iki yolda da geçerli):** Module Pack → Domain Config → AGENTS.md → `.antigravity/rules/` → `execution/portfolio/master-development-plan.md`

---

## Yol A — Sıfırdan Yeni Domain Açma

Yılda 1-2 kez. Örnek: yeni "CRM" domain'i.

### A1. Domain klasörünü kur

```
execution/domains/{domain-name}/
├── README.md             ← domain ne, kapsam, kapsam dışı, otorite hiyerarşisi, "yeni modül" kısa adımları
├── domain-config.md      ← in-scope modüller, repo scope, protected paths, ownership boundaries, runtime decisions
└── module-packs/         ← (başlangıçta boş)
```

> **Şablon olarak referans:** [execution/domains/platform-shared-services/README.md](../execution/domains/platform-shared-services/README.md) ve [domain-config.md](../execution/domains/platform-shared-services/domain-config.md) bu yapının kanonik örneğidir.

### A2. domain-config.md'de yazılması GEREKEN

- **Purpose:** Tek paragrafla domain ne sahipleniyor.
- **In-Scope Modules:** Modül kimlikleri (status için master-plan'a yönlendir, burada tekrarlama).
- **Out-of-Scope:** Hangi domain'lere yönlendirildiği netçe.
- **Domain-Level Repo Scope:** Bu domain'in dokunabileceği yollar (`services/`, `frontend/`, `gateway/`).
- **Protected Paths:** Bu domain'in **dokunamayacağı** yollar (diğer domain servisleri, archive, FROZEN dosyalar).
- **Ownership Boundaries:** Modüller arası kritik sahiplik kuralları (örn. "MOD-0023 Approvals ile MOD-0024 Tasks birleşemez").
- **Runtime Decisions:** Yalnızca **domain'e özel** olanlar; engineering kurallarına link ver, içeriği tekrarlama.

### A3. domain-config.md'de yazılMAması GEREKEN

`.antigravity/rules/`'da olan hiçbir şeyin tekrarı:
- ❌ MongoDB + Repository pattern detayı (→ `mongo-indexing.md`)
- ❌ Response envelope formatı (→ `response-envelope.md`)
- ❌ Soft delete, EntityBase alanları (→ `entity-base-template.md`)
- ❌ Layered architecture, CQRS klasör isimleri (→ `erp-architecture.md`)
- ❌ JWT/permission format detayı (→ `security-jwt.md`)

Engineering kuralları tekrarlanırsa kaçak otorite oluşur ve bayatlar.

### A4. AGENTS.md'ye domain'i tanıt

- §2 (klasör yapısı) ve §9 (branch adlandırma) → yeni domain ve kısa kodu (`crm` gibi) eklenir.
- Platform/Admin domain'i ise → [execution/portfolio/master-development-plan.md](../execution/portfolio/master-development-plan.md) Section 2 envanter tablosuna ve [execution/registries/module-id-registry.md](../execution/registries/module-id-registry.md) içine eklenir.

### A5. İlk modülü Yol B ile yaz

Domain çerçevesi hazır; modül üretimi standart akışa girer.

---

## Yol B — Mevcut Domain'de Yeni Modül

Senin günlük akışın. Örnek: PSS domain'inde "Platform Administrators Management".

### B1. `/prepare-module-pack` çağır

Vereceğin bilgiler:
- Modül adı + tek cümlelik amaç
- Domain (MDM / PSS / DEVEN / ESBP)
- Servis (`Diten.Platform`, `Diten.AuthService`, `Diten.MdmService`, `Diten.DevEnablementService`)
- Shell tipi (`platform-admin` / `tenant` / `none`)
- Create/edit formundaki kullanıcı alan sayısı + isimleri (Slim/Compact kararı için)
- DataTable kullanılacak mı
- Bilinen iş kuralları + bağımlılıklar
- Entity base kararı (`EntityBase`/`BaseEntity`/`GlobalEntity` + gerekçe)

Ne olur:
- `module-pack-author` ajanı [AGENTS.md](../AGENTS.md), [domain-config.md](../execution/domains/), [execution/portfolio/master-development-plan.md](../execution/portfolio/master-development-plan.md), [execution/registries/module-id-registry.md](../execution/registries/module-id-registry.md), [.antigravity/rules/](../.antigravity/rules/), Golden Reference Slim/Compact dosyalarını okur.
- Form alan sayısına göre `golden_reference: slim` (≤8) veya `compact` (>8) kararı verilir.
- `execution/domains/{domain}/module-packs/{ID}-{slug}.md` dosyası `status: draft` ile üretilir.

### B2. Module pack'i incele

Pack 20 zorunlu bölüm içermeli ([.antigravity/rules/module-pack-standard.md](../.antigravity/rules/module-pack-standard.md)):

| # | Bölüm | Kritik içerik |
|---|---|---|
| Frontmatter | Kimlik | id, name, domain, service, shell, golden_reference, entity_base, status, owner, branch, form_field_count |
| 1-8 | Tanım | Module Summary, Ownership, Owned Objects, Entity Fields, Repo Scope, Protected Paths, Dependencies, Runtime Constraints |
| 9 | Layout & Shell Contract | Razor `Layout = "_LayoutPlatformAdmin"` AÇIKÇA |
| 10 | Backend File Convention | Golden Reference birebir folder/naming |
| 11 | Frontend File Contract | Slim/Compact dosya seti tam |
| 12 | Validation Rules | Her field için tablo |
| 13 | Failure Path | Duplicate, missing, unauthorized, concurrency (en az 4 senaryo) |
| 14 | Authorization Convention | Permission listesi + policy + actor type |
| 15 | Gateway Routing | Gerekli mi, `integration-agent` task'ı |
| 16 | Acceptance Criteria | Test edilebilir maddeler |
| 17 | Test Expectations | Build / verifier / RESX / smoke |
| 18 | Ready-for-dev Checklist | Tüm madde işaretli olmalı |
| 19 | Implementation Notes | Domain'e özel ipuçları |
| 20 | Follow-up Items | Bilinen sonraki adımlar |

### B3. Onayla

Yanlışları düzelttirdikten sonra frontmatter:
```yaml
status: approved   # veya ready-for-dev
```

`@orchestrator` `draft` paketlerle kod yazmaz.

### B4. Branch aç

```bash
git checkout -b feature/{domain-kısa}/{module-id}-{slug}
# örnek: feature/pss/pss-001-platform-administrators
```

`domain-kısa`: `mdm` | `pss` | `deven` | `esbp`

### B5. Orchestrator çağır

```
@orchestrator execution/domains/{domain}/module-packs/{ID}-{slug}.md
```

`/add-module` workflow'u Phase 0 → 6 çalışır:
- Phase 0: pack + bağımlılık kontrolü
- Phase 1-3: backend (Domain → Persistence → Application → API)
- Phase 4: frontend (Controller → Views → JS → L10n)
- Phase 5: gateway route (gerekiyorsa, `integration-agent`)
- Phase 6: build + verifier + RESX + smoke test

### B6. Test + status güncelle

- Browser'da gerçek feature'ı test et (golden path + edge cases)
- `status: in-progress` → `review` → `done`

### B7. PR

```bash
gh pr create --title "..." --body "..."
```

Module pack silinmez, `done` olarak repository'de kalır (tarihsel kayıt).

---

## Akış Görseli

```
YOL A (yeni domain — yılda 1-2)         YOL B (yeni modül — her gün)
─────────────────────────────────       ─────────────────────────────────
A1. Domain klasörü kur                  B1. /prepare-module-pack
    (README + domain-config +               (alan sayısı + iş kuralları)
     module-packs/)                          ↓
     ↓                                  B2. Module pack incele
A2. domain-config.md yaz                    (20 bölüm + frontmatter)
    (sadece domain'e özel kararlar)         ↓
     ↓                                  B3. status: approved
A3. Engineering kurallarını            B4. Branch aç
    tekrarlama —                            (feature/{d}/{id}-{slug})
    .antigravity'ye link ver                ↓
     ↓                                  B5. @orchestrator {pack}
A4. AGENTS.md §2 + §9'a                     (/add-module Phase 0→6)
    yeni domain'i tanıt                     ↓
     ↓                                  B6. Test + status: done
A5. İlk modül → Yol B'ye geç          B7. PR
```

> **Standalone `@orchestrator` kuralı:** Module pack oluşturmaz. Pack yoksa veya `draft` ise kullanıcıyı `/prepare-module-pack`'e yönlendirir.

## Hangi Agent Ne Zaman Kullanılır?

| İş | Agent / Workflow |
|---|---|
| Çoklu-servis stratejik etki analizi (yeni domain / büyük feature) | `product-manager` (module pack hazırlığı öncesinde) |
| User Story + Gherkin Acceptance Criteria + MVP/MoSCoW kapsamı | `product-owner` (module pack içeriği için) |
| Tek modül için PRD/BRD + IFRS/KVKK iş kuralı + L10n anahtar listesi | `business-analyst` (module pack içeriği için) |
| Yeni module pack hazırlama (sözleşme dosyası) | `module-pack-author` veya `/prepare-module-pack` |
| Çok modüllü / cross-cutting yetenek planı (kod yazmadan, Delivery Capability Pack) | `/prepare-capability-pack` (CAP-001) |
| Onaylı module pack ile uçtan uca geliştirme | `@orchestrator` / `/add-module` |
| Backend endpoint/CQRS ekleme | `backend-architect` / `/add-endpoint-cqrs` |
| Frontend DataTable veya form düzenleme | `frontend-ui-ux` / `/add-page` |
| Gateway route ekleme | `integration-agent` |
| 7 dil RESX ve JS L10n bridge | `l10n-agent` |
| Test senaryoları | `testing-agent` |
| Güvenlik/tenant/RBAC denetimi | `security-agent` |
| Salt-okunur mimari/governance denetimi (kod yazma yok) | `read-only-auditor` / `/read-only-audit` |
| Hata analizi | `debugger` |
| Teknik dokümantasyon | `documentation-writer` |
| Son kullanıcı kılavuzu | `user-manual-generator` |

> **Planlama kadrosu sırası:** `product-manager` (yalnız stratejik scope), `product-owner` (AC/MVP), `business-analyst` (iş kuralı/L10n) **opsiyonel** ön adımlardır; çıktıları **her zaman** `module-pack-author`'a girdi olur. Sıradan tek modül geliştirmesi için `module-pack-author` doğrudan çağrılabilir.

> **Git güvenliği:** Staging / commit / push yalnızca [GIT-002 git-safety.md](../.antigravity/rules/git-safety.md) kapılarıyla ve açık kullanıcı onayıyla yapılır; `main`'e doğrudan push yoktur.

## Slim / Compact Seçimi

DataTable modüllerinde create/edit formundaki kullanıcı alanları sayılır.

Sayılmayanlar: `Id`, `TenantId`, `IsDeleted`, `CreatedAt`, `UpdatedAt`, audit alanları, DataTable checkbox/action kolonları.

| Form alan sayısı | Golden reference | Frontend yapı |
|---|---|---|
| `8 ve altı` | `GoldenReferenceSlim` | Index içinde `_CreateEditOffcanvas.cshtml` |
| `8'den fazla` | `GoldenReferenceCompact` | `Create.cshtml`, `Edit.cshtml`, `Details.cshtml`, `_Form.cshtml` |

Module pack içinde `form_field_count` ve `golden_reference: slim|compact` açık yazılır.

## Backend Klasör Standardı

Her feature altında CQRS ayrımı korunur:

- `Commands/`
- `Queries/`
- `Handlers/CommandHandlers/`
- `Handlers/QueryHandlers/`
- `Validators/`

Her command, query, handler ve validator ayrı dosyada olur. Controller ince kalır; MediatR'a gönderir ve `CustomBaseController` response envelope döner.

## Frontend Partial Standardı

Her DataTable modülünde ortak zorunlu yapı:

- `Index.cshtml`
- `_Filter.cshtml`
- `_DataTable.cshtml`
- `_IndexL10n.cshtml`
- `{ModuleName}Index.cs`
- `index.l10n.js`
- `index.js`

Slim ek dosyası:

- `_CreateEditOffcanvas.cshtml`

Compact ek dosyaları:

- `Create.cshtml`
- `Edit.cshtml`
- `Details.cshtml`
- `_Form.cshtml`

## Doğrulama Komutları

Slim:

```bash
python3 .antigravity/scripts/verify_datatable_page.py . --area DevEnablement --module GoldenReferenceSlim --reference slim
```

Compact:

```bash
python3 .antigravity/scripts/verify_datatable_page.py . --area DevEnablement --module GoldenReferenceCompact --reference compact
```

Build:

```bash
dotnet build services/Diten.DevEnablementService/src/Diten.DevEnablementService.Api/Diten.DevEnablementService.Api.csproj -c Debug
dotnet build frontend/Diten.Web/Diten.Web.csproj -c Debug
dotnet build gateway/Diten.ApiGateway/Diten.ApiGateway.csproj -c Debug
```

RESX:

```bash
python3 .antigravity/skills/i18n-localization/scripts/resx_sharedresource_checker.py .
```

## Legal Entity Örneği

1. `Legal Entity module pack hazırla.`
2. `module-pack-author` domain'i, alanları, form alan sayısını ve Slim/Compact kararını yazar.
3. Kullanıcı `draft` module pack'i inceler.
4. Status `approved` veya `ready-for-dev` yapılır.
5. `@orchestrator Legal Entity module pack'e göre geliştir.`
6. Orchestrator onaylı pack üzerinden geliştirmeyi yürütür.


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
