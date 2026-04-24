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
│       ├── master-data-management/      (MDM — henüz servis yok, domain planlaması mevcut)
│       ├── developer-enablement/        (DEVEN — Diten.DevEnablementService)
│       ├── platform-shared-services/    (PSS — Diten.Platform + Diten.AuthService)
│       └── enterprise-strategy-business-performance/  (ESBP — Diten.EnterpriseStrategyService)
├── services/                            .NET 8 mikroservisler
│   ├── Diten.AuthService/
│   ├── Diten.DevEnablementService/
│   ├── Diten.Platform/
│   ├── Diten.Platform.Common/
│   └── Diten.EnterpriseStrategyService/
├── frontend/                            Razor MVC + Sneat PRO + DataTables v2
│   └── Diten.Web/
├── gateway/                             Ocelot API Gateway
│   └── Diten.ApiGateway/
└── docs/                                Dokümantasyon ve audit raporları
    └── audits/
```

**ÖNEMLİ:** Bu proje `src/Backend/` veya `src/Frontend/` yapısı **kullanmaz**. Yukarıdaki gerçek yapıya uyulmalıdır.

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
| `frontend/Diten.Web/Views/Shared/_Layout.cshtml` | Archive için FROZEN layout. Yeni modüller `_LayoutBackbone.cshtml` kullanır |
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
python3 .antigravity/scripts/verify_datatable_page.py . --area {AreaName} --module {ModuleName}
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
| UI Layout | `_LayoutBackbone.cshtml` (yeni modüller); `_Layout.cshtml` FROZEN | [.antigravity/rules/views-organization.md](.antigravity/rules/views-organization.md) |
| DataTable | v2 kontratı zorunlu (`data-dt-standard="v2"`) | [.antigravity/rules/frontend-datatable-template.md](.antigravity/rules/frontend-datatable-template.md) |

---

## 7. Ajan Seçimi ve Varsayılan Entry Point

Varsayılan entry point: **`@orchestrator`**

Orchestrator çağrıldığında Aşama 0'da şu dosyaları okur:
1. `AGENTS.md` (bu dosya)
2. İlgili `execution/domains/{domain}/domain-config.md`
3. Varsa `execution/domains/{domain}/module-packs/{ID}.md`
4. Görev tipine uygun `.antigravity/workflows/*.md`

Ajan listesi ve sorumlulukları: [.antigravity/agents/](.antigravity/agents/)
Prompt yazma rehberi: [.antigravity/PROMPT-GUIDE.md](.antigravity/PROMPT-GUIDE.md)

---

## 8. Çalışma Akışı (New Module / New Feature)

Yeni bir modül veya büyük feature geldiğinde:

1. **Domain seç** — Hangi domain'e ait? (MDM / PSS / ESBP)
2. **Module pack oluştur** — `execution/domains/{domain}/module-packs/MOD-XXXX-slug.md`
   - YAML frontmatter doldur (id, name, domain, status=draft, owner, branch, dates)
   - Acceptance criteria, repo scope, protected paths yaz
3. **Branch aç** — `feature/{domain-kısa}/{module-id}-{slug}`
   - Örnek: `feature/mdm/mdm-001-product-management`
4. **Orchestrator çağır** — Module pack adını ver
5. **`/add-module` workflow** çalışır — Phase 1 → 6
6. **Status güncelle** — `draft` → `in-progress` → `review` → `done`
7. **PR aç, merge et** — Module pack silinmez, `done` olarak kalır

Fix/refactor işleri için: [.antigravity/rules/GEMINI.md](.antigravity/rules/GEMINI.md) working-agreement akışı uygulanır (önce kod düzeltilir, onay alındıktan sonra `.antigravity` etkisi kontrol edilir).

---

## 9. Branch Adlandırma Kuralı

```
feature/{domain-kısa}/{module-id}-{slug}
```

- `domain-kısa`: `mdm` | `pss` | `esbp`
- `module-id`: `mdm-001`, `pss-002`, vb. (küçük harf)
- `slug`: 2-4 kelimelik kısa isim

Örnekler:
- `feature/mdm/mdm-001-product-management`
- `feature/pss/pss-001-identity-access`
- `feature/esbp/esbp-001-strategy-core`

Yedekleme branch'leri için ayrı kural: [.antigravity/rules/git-backup-policy.md](.antigravity/rules/git-backup-policy.md)

---

## 10. Module Pack Zorunluluğu

**Yeni bir modül veya büyük bir feature'ın kodu module pack olmadan yazılamaz.**

Module pack minimum içermeli:
- YAML frontmatter (id, status, owner, branch)
- Owned objects
- Repo scope (dokunulacak klasörler)
- Protected paths (dokunulmayacak klasörler)
- Acceptance criteria (test edilebilir)
- Test expectations

Şablon: [.antigravity/rules/module-pack-standard.md](.antigravity/rules/module-pack-standard.md)

---

## 11. Terim Sözlüğü

| Terim | Anlam |
|-------|-------|
| **Global Engineering System** | `.antigravity/` — tüm projeler arası yeniden kullanılabilir katman |
| **Domain** | `execution/domains/{name}/` — bir iş alanı (MDM, PSS, ESBP) |
| **Module Pack** | `execution/domains/{d}/module-packs/{ID}.md` — tek bir modülün kimlik + AC dosyası |
| **Domain Config** | `execution/domains/{d}/domain-config.md` — domain sınırları ve kararlar |
| **Controls** | `execution/domains/{d}/controls/` — domain'e özel ek kontroller (opsiyonel) |
| **Workflow** | `.antigravity/workflows/*.md` — yeniden kullanılabilir tarif (ör. `/add-module`) |

---

## 12. Adaptasyon Notu

Bu repo, [Layered Agent + Domain Package Model SOP v2.1](docs/sop/upstream/) temelinde çalışır; fakat projemize özgü farklar vardır:

- `batches/` katmanı **kullanılmaz** (`.antigravity/workflows/add-module.md` zaten phase orchestration sağlar)
- `snapshots/` katmanı **kullanılmaz** (git history + `docs/audits/` bu işi yapar)
- Module ID formatı `MOD-xxxx-slug` (örn: `MOD-0018-rbac-abac-authorization`) — hem teknik standartlar hem de tüm modül kimlikleri için birincil formattır
- Klasör yapısı `services/` + `frontend/` + `gateway/` (SOP'taki `src/Backend/` + `src/Frontend/` yerine)

SOP'tan sapmaların tam listesi, yukarıdaki hiyerarşi ve proje bazlı SOP dosyaları (`docs/sop/upstream/`) üzerinden takip edilir.
