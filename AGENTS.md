# AGENTS.md — Diten ERP vNext Execution Contract

Bu dosya, Claude Code, Codex ve diğer AI ajanlarının repo genelinde uyması gereken yürütme kontratıdır.

**Yükleme:** Codex ve Antigravity bu dosyayı kendileri yükler (bu denetimde canlı ölçülmedi). Claude Code, `CLAUDE.md` varken bu
dosyayı yüklemez; bu yüzden aşağıdaki **Çekirdek** bloğu `.claude/rules/agents-core.md` olarak üretilir
(`python3 .antigravity/scripts/sync_adapters.py`) ve Claude Code'un başlangıç bağlamına girmesi için oraya konur. Canlı ölçüm
(2026-10-17, başsız CLI 2.1.71 / Haiku 4.5, araçlar ve MCP kapalı): çekirdek `9a8c009001a5` (`c02b28c16`) ve `c9fc70b86047`
(`c82d7db2c`) birer dar pozitif-negatif ölçümden geçti; başlangıç belleği izole değildi, cevabın yalnız bu dosyadan geldiği
kanıtlanmadı; etkileşimli oturum, alt ajanlar ve başka modeller ölçülmedi. Sembolik bağ kullanılmaz (Windows). Ayrıntı ve
tarihçe: `.antigravity/AGENTS-REFERENCE.md`.

`CLAUDE.md` bir kopya değildir ve olmamalıdır; çakışmada **bu dosya kazanır**.

> **Otorite:** Bu dosya, `.antigravity/` içindeki global standartlardan üstündür. Domain veya module seviyesinde yazılmış bir kural bu dosyadan üstündür.
> `.antigravity/AGENTS-REFERENCE.md` bu dosyanın devamıdır ve aynı yetki seviyesindedir.

---

<!-- core:start -->
## Çekirdek — her işte geçerli davranış kuralları

> Kısa ama **bağlayıcıdır** — hangi araçta kendiliğinden yüklendiğinden bağımsız olarak her işte geçerlidir. Ayrıntı gerektiğinde belirtilen bölümü (§N) ya da kural dosyasını **aç** — özetle yetinme.

1. **Yetki sırası (§1):** Module Pack > Domain Config > AGENTS.md > `.antigravity/` > Archive. Çakışmada üstteki kazanır; bu sırayı bozma.
2. **Kiracı izolasyonu (§6, `multi-tenancy`):** Kiracı verisine dokunan her sorgu ve endpoint `TenantId` ile sınırlanır; kiracılar arası erişim 404 döner. Üretim kodunda sabit ya da varsayılan kiracı kullanma. Silme yalnız soft delete (`IsDeleted`, `DeletedAt`).
3. **Yetki (§6, `security-jwt`, `permission-key-standard`):** JWT + RBAC; her eylem `[HasPermission]` ister. Yetkisiz kullanıcıya sayfa iskeleti çizme, yönlendirme yapma (UAS-001).
4. **Dil (§6, `localization-standard`):** Kullanıcıya görünen metin hardcoded yazılmaz. Platform modülü 2 dil (en, tr), kiracı modülü 7 dil (en, tr, fr, es, zh, ar, ru); bir dil eksikse iş bitmemiştir.
5. **Portlar (§3):** Portların tek kaynağı §3 tablosudur; port uydurma. Web (5001) servislere doğrudan değil, Gateway (5000) üzerinden gider.
6. **Korunan yollar (§4):** `.antigravity/**` değişikliği kullanıcı onayı ister. `ocelot.json`'u yalnız `integration-agent` değiştirir. Archive ve `_Layout.cshtml` donuktur. Başka domain'in servisine dokunma.
7. **Modül ve kimlik (§7, §10, DCP-002):** Kod yalnız `approved` / `ready-for-dev` module pack ile yazılır; `@orchestrator` pack oluşturmaz. MOD kimliği uydurulmaz; pack açmadan önce `verify_module_id.py` ön kontrolü (fail-closed) koşulur. **Modül talebi:** Adı verilen yeni modül ya da kapsam istenince önce araştır: Blueprint, `python3 .antigravity/scripts/verify_module_id.py . --discover "<ad>"`, pack ve domain-config. Son yanıt dört parça: okunan kaynaklar (çıkarım ayrı); koşmayan ya da eksik keşif ve nedeni ("kısmi inceleme"); adaylar/mükerrerler, gerekçeli ilk kapsam (tek öneri); tüm açık onay/sahiplik kararları (TBD dahil). Belirsizde sor, salt-okunur adım öner; küçük düzeltmede tarama gerekmez. Kod, commit ya da dağıtım izni değildir.
8. **Git (§9, GIT-002 `git-safety`):** `main`'e commit/push yok. Yalnız açık dosya yoluyla stage (`git add -A` / `git add .` yok). Commit, push, PR ve merge yalnız kullanıcının açık onayıyla; yıkıcı git komutları onaysız çalışmaz.
9. **Başka modül (XMC-001):** İşin kapsamı dışındaki bir modülde düzeltme gerekirse dokunmadan önce sor. **Kayıt kodu (RCS-001):** yeni kayıt türünün görünen kodu `MG-ORG-000042` biçiminde ortak servisten gelir.
10. **Test ve belge:** Kural, kendi kopyasıyla değil üretim koduyla ölçülür; sabotaj kanıtı olmayan guard, guard değildir. Belge yeri `docs-organization` beş sorusuyla belirlenir; `docs/` köküne dosya konmaz.
11. **Kural haritası (§6.1):** İşe başlamadan §6.1'de işin satırını bul ve o kural dosyalarını aç. Bu blokta olmayan her ayrıntı için ilgili bölüm (§N) ya da `.antigravity/AGENTS-REFERENCE.md` açılır.
<!-- core:end -->

---

## 1. Yetki Hiyerarşisi (Authority Order)

Bir talep birden fazla kaynakta yer alıyorsa, en spesifik katman kazanır:

1. **Module Pack** — `execution/domains/{domain}/module-packs/{ID}-{slug}.md`
2. **Domain Config** — `execution/domains/{domain}/domain-config.md`
3. **AGENTS.md** — bu dosya (repo root)
4. **Global Engineering System** — `.antigravity/` (ajanlar, kurallar, workflow'lar, skill'ler)
5. **Archive / dış referans** — otorite değil, sadece referans

Çakışma varsa yukarıdaki sırayla karar verilir. Ajanlar bu sırayı bozamaz.

---

## 2. Proje Klasör Yapısı

Bu proje `src/Backend/` veya `src/Frontend/` yapısı **kullanmaz**. Kök: `services/` · `frontend/Diten.Web` · `gateway/Diten.ApiGateway` · `execution/domains/` · `.antigravity/` · `tests/` · `docs/`.

- **Servisler (17):** `Diten.AuthService`, `Diten.CrmService`, `Diten.DevEnablementService`, `Diten.EnterpriseStrategyService`, `Diten.HcmService`, `Diten.HumanCapitalService`, `Diten.InventoryService`, `Diten.ManagementGovernanceService`, `Diten.ManufacturingService`, `Diten.MdmService`, `Diten.PlanningService`, `Diten.Platform`, `Diten.PpmService`, `Diten.ProcurementService`, `Diten.PvgService`, `Diten.SupplyChainService`, `Diten.TalentEcosystemService`
- **Ortak kütüphaneler (3):** `Diten.Building.Blocks`, `Diten.Platform.Common`, `Diten.Platform.Contracts`
- **Domain'ler (11):** `commercial-suite`, `developer-enablement`, `enterprise-strategy-business-performance`, `human-capital-management`, `management-governance`, `master-data-management`, `pharmacovigilance`, `platform-shared-services`, `portfolio-delivery`, `procurement`, `supply-chain-execution`

Klasör ağacı ve tarihsel notlar: `.antigravity/AGENTS-REFERENCE.md` §2.

---

## 3. Port Şeması

| Servis | Port | Proje Yolu |
|--------|------|------------|
| Gateway (Ocelot) | 5000 | `gateway/Diten.ApiGateway` |
| Frontend (Diten.Web) | 5001 | `frontend/Diten.Web` |
| Auth Service | 5056 | `services/Diten.AuthService/src/Diten.AuthService.Api` |
| Platform Service | 5057 | `services/Diten.Platform/src/Diten.Platform.API` |
| DevEnablement Service | 5058 | `services/Diten.DevEnablementService/src/Diten.DevEnablementService.Api` |
| MDM Service | 5059 | `services/Diten.MdmService/src/Diten.MdmService.Api` |
| HCM Service | 5060 | `services/Diten.HcmService/src/Diten.HcmService.Api` |
| CRM Service | 5061 | `services/Diten.CrmService/src/Diten.CrmService.Api` |
| Procurement Service | 5065 | `services/Diten.ProcurementService/src/Diten.ProcurementService.Api` |
| SupplyChain Service | 5066 | `services/Diten.SupplyChainService/src/Diten.SupplyChainService.Api` |
| Inventory Service | 5067 | `services/Diten.InventoryService/src/Diten.InventoryService.Api` |
| Planning Service | 5068 | `services/Diten.PlanningService/src/Diten.PlanningService.Api` |
| Manufacturing Service | 5070 | `services/Diten.ManufacturingService/src/Diten.ManufacturingService.Api` |
| MongoDB | 27017 | yerel çalışmalı |

> **Güncel durum (tarihli atama notları ve gerekçeleri: `.antigravity/AGENTS-REFERENCE.md` §3):** O notlar tarihseldir; içlerindeki "sıradaki" ve "boş" port ifadeleri yazıldıkları günün durumudur. Yeni servise port vermeden önce bu tabloyu kontrol et; tahsis EA kararıyla yapılır, burada sıradaki port önerilmez. Tabloda olmayan ama kullanılan portlar: 5062 (`PpmService` ve `DataKnowledgeService` — çift talep), 5063 (`HumanCapitalService`), 5064 (`TalentEcosystemService`); canonical tahsis EA kararını bekliyor. Port atamadan önce o notları oku.

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

Tek servis: `dotnet build <proje>.csproj -c Debug` · test: `dotnet test <servis-klasörü>`. Mimari testler `services/*` altında **değildir**, repo genelini tarar ve yerelde de koşulur (CI her push'ta koşar):

```bash
dotnet test tests/architecture/TenantArchitecture.ArchitectureTests
```

Tüm komutlar, çalıştırma betikleri ve DataTable kontrat doğrulaması: `.antigravity/AGENTS-REFERENCE.md` §5.

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
| Pipeline Behaviors | 4 zorunlu (Validation, Logging, Exception, Performance) + denetim kaydı davranışı (Platform: `AuditBehavior`; diğer servisler: ortak iletici, AUD-001 K4) | [.antigravity/rules/pipeline-behaviors.md](.antigravity/rules/pipeline-behaviors.md) |
| Denetim kaydı | Kiracı ya da platform verisini değiştiren **her komut denetlenir** (kim · neyi · ne zaman). Denetlenmemek istisnadır ve gerekçesiyle bildirilir; yeni denetimsiz komutu ve deftere eklenen borç satırını mimari testi + CI reddeder; kimlik / yetki / GxP sınıfında kayıt yazılamazsa işlem durmalıdır (karar K2; bugün hiçbir yol bunu teslim etmiyor, eksikler defterde `## K2 borcu` olarak sayılı — AUD-001 §4.4) | [.antigravity/rules/audit-trail-standard.md](.antigravity/rules/audit-trail-standard.md) |
| Yerelleştirme | **Platform modülleri 2 dil** (en, tr) · **Tenant modülleri 7 dil** (en, tr, fr, es, zh, ar, ru) — `.resx` + `window.L10n` bridge. Ölçüldü 2026-09-07: `Views/Platform/*` 2 dosya, `Views/Organization/*` 7 dosya taşır. | [.antigravity/rules/localization-standard.md](.antigravity/rules/localization-standard.md) |
| UI Layout | Admin modülleri `_LayoutPlatformAdmin.cshtml`; tenant modülleri `_LayoutTenantShell.cshtml`; `_Layout.cshtml` FROZEN | [.antigravity/rules/views-organization.md](.antigravity/rules/views-organization.md) |
| DataTable | v2 kontratı zorunlu (`data-dt-standard="v2"`) + Golden Slim/Compact seçimi | [.antigravity/rules/frontend-datatable-template.md](.antigravity/rules/frontend-datatable-template.md) |
| Modaller & Uyarılar | Premium SweetAlert2 Standardı (MOD-0013) zorunlu | [.antigravity/rules/premium-modal-standard.md](.antigravity/rules/premium-modal-standard.md) |
| Yetkisiz Ekran | İzni olmayan kullanıcıya sayfa iskeleti çizilmez — başlık, kart, boş tablo, eylem butonu yok; tek açıklama + ne yapılacağı. Yönlendirme YASAK (UAS-001) | [.antigravity/rules/unauthorized-surface-standard.md](.antigravity/rules/unauthorized-surface-standard.md) |

### Golden Reference DataTable Kararı

DataTable tabanlı yeni modüllerde resmi referans iki canlı DevEnablement modülüdür:

| Form alan sayısı | Referans | UI surface |
|---|---|---|
| `8 ve altı` | `GoldenReferenceSlim` | Index içinde create/edit offcanvas |
| `8'den fazla` | `GoldenReferenceCompact` | Ayrı `Create.cshtml`, `Edit.cshtml`, `Details.cshtml`, `_Form.cshtml` |

Alan sayımı yalnızca create/edit formunda kullanıcının doldurduğu modül alanlarıdır. `Id`, `TenantId`, `IsDeleted`, `CreatedAt`, `UpdatedAt`, audit alanları ve DataTable checkbox/action kolonları sayılmaz.

---

## 6.1 Kural Haritası — hangi işte hangi kural

`.antigravity/rules/` altında 46 kural dosyası var (2026-10-17, `git ls-tree` ile sayıldı). Kural dosyaları kendiliğinden
yüklenmez; `.antigravity/adapters.json`'da listelenenlerin üretilmiş kopyaları `.claude/rules/` (Claude Code) ve
`.agents/rules/` (Antigravity) altına konur — bu denetimde yalnız `agents-core.md`'nin yüklenmesi ölçüldü. Codex ve
Antigravity bu haritayı bu dosyayla birlikte yükler (bu denetimde canlı ölçülmedi); Claude Code'da Çekirdek bloğu (madde 11)
işe başlamadan bu haritanın açılmasını şart koşar.

Kural, ona atıf verildiği kadar görünür. **İşe başlamadan önce satırını bul ve o
kuralların dosyasını aç** — harita kuralın yerini söyler, içeriğini değil.

### Daima — istisnasız
`multi-tenancy` kiracı izolasyonu · `security-jwt` token ve yetki ·
`git-safety` dal ve commit güvenliği · `code-style` kod stili ·
`docs-organization` belge nereye yazılır ·
`cross-module-change-consent` kapsam dışı başka bir modülde düzeltme gerekirse durup sorar: "bunu da düzeltelim mi?" (XMC-001)

### Backend / handler yazıyorsan
`handler-design` · `repository-standard` · `response-envelope` ·
`pipeline-behaviors` · `entity-base-template` · `entity-versioning` ·
`mongo-indexing` · `api-conventions` · `routes` · `ports` ·
`audit-trail-standard` yazma komutu denetlenir; yol a/b/c ya da gerekçeli istisna (AUD-001)

### Ekran / sayfa yazıyorsan
`frontend-standards` genel · `views-organization` dosya yerleşimi ·
`unauthorized-surface-standard` yetkisiz kullanıcı ne görür (UAS-001) ·
`frontend-js-standard` · `premium-modal-standard` uyarı ve modal ·
liste → `frontend-datatable-template` · detay → `frontend-details-template` ·
create/edit → `frontend-form-template` ·
`page-help-and-keyboard` "?" yardım metni (yoksa borç defteri), yan panelde Enter, kısayol katmanı, tur (UXH-001)

### Kullanıcıya görünen metin varsa
`localization-standard` (Platform 2 dil · Tenant 7 dil) ·
`dynamic-localization-standard` çalışma zamanı çeviri

### Yetki, izin, lookup dokunuyorsan
`permission-key-standard` izin anahtarı · `business-module-enforcement-standard`
modül yetki zorlaması · `data-scope-enforcement` hangi satırları görür (SEC-002) ·
`platform-lookups-reference-data` referans veri ·
`platform-global-search-registry` Ctrl+K kaydı

### Yeni modül / pack açıyorsan
`module-pack-standard` · `capability-pack-standard` ·
`module-self-registration-standard` manifest ·
`audit-trail-standard` pakette "Denetlenen Olaylar" bölümü (AUD-001) ·
`record-coding-standard` kullanıcıya görünen kayıt kodu: `MG-ORG-000042` biçimi, OTOMATİK / İKİSİ DE / KALSIN (RCS-001)

### Ortam, çalıştırma, kayıt
`dev-runbook` yerel ortam · `configuration-safety` ayar ve bağımlılık ·
`logging-observability` log · `git-backup-policy` yedek ve isimlendirme ·
`status-reporting-and-evidence` çok modüllü durum raporu ve kanıt (REP-001)

### Mimari kararlar
`erp-architecture` · `diten_standards`

### Görev Merkezi'ne bağlanıyorsan
`workcenter-bridge-standard` — köprü yasakları (WC-D1)

⚠ Bu harita eksikse kural görünmez olur. Yeni bir kural dosyası eklendiğinde
**aynı commit'te** buraya satırı yazılır; yazılmayan kural yetimdir.

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
3. Varsa `execution/domains/{domain}/module-packs/{ID}-{slug}.md`
4. Görev tipine uygun `.antigravity/workflows/*.md`

**Kural:** `@orchestrator` module pack oluşturmaz. Yeni modül geliştirmesi yalnızca mevcut ve kullanıcı tarafından onaylanmış (`approved` veya `ready-for-dev`) module pack üzerinden başlar. Module pack yoksa veya `draft` durumundaysa kod yazılmaz; kullanıcı önce `/prepare-module-pack` veya `module-pack-author` ile module pack hazırlamaya yönlendirilir.

Ajan listesi ve sorumlulukları: [.antigravity/agents/](.antigravity/agents/)
Prompt yazma rehberi: [.antigravity/PROMPT-GUIDE.md](.antigravity/PROMPT-GUIDE.md)

---

## 8. Çalışma Akışı (Domain / Module Oluşturma)

Yeni modül: `/prepare-module-pack` → pack `approved` / `ready-for-dev` → dal `feature/{domain-kısa}/{module-id}-{slug}` → `@orchestrator {pack-yolu}` → `/add-module` (Phase 0 → 6). Yeni domain nadirdir. Adım adım iki yol (A: yeni domain, B: mevcut domain'de modül): `.antigravity/AGENTS-REFERENCE.md` §8.

---

## 9. Branch Adlandırma Kuralı

```
feature/{domain-kısa}/{module-id}-{slug}
```

- `domain-kısa`: `mdm` | `pss` | `deven` | `esbp` | `mg` | `ppm` | `pvg` | `crm`
- `module-id`: `mdm-001`, `pss-002`, vb. (küçük harf)
- `slug`: 2-4 kelimelik kısa isim

> `crm` = Commercial Suite (CRM + O2C) domain'i (`execution/domains/commercial-suite/`). CRM core + Sales + Marketing +
> Field Sales module pack branch'leri bu kısa kodu kullanır. CPQ / O2C / Service / Business Development adjacent modüllerin
> ileride `crm` altında mı yoksa ayrı bir kısa kod altında mı ilerleyeceği EA-TBD'dir; MOD-0149 ve CRM core için `crm` kullanılır.

Örnekler:
- `feature/mdm/mdm-001-product-management`
- `feature/pss/pss-001-identity-access`
- `feature/esbp/esbp-001-strategy-core`
- `feature/ppm/mod-0117-work-records-core`
- `feature/pvg/mod-0230-case-intake-triage`
- `feature/crm/mod-0149-customer-360-account-hierarchy`


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

Terimler (Global Engineering System, Domain, Module Pack, Delivery Capability Pack …): `.antigravity/AGENTS-REFERENCE.md` §11.

---

## 12. Adaptasyon Notu

Katman SOP'undan (v2.1) sapmalar ve kimlik formatları: `.antigravity/AGENTS-REFERENCE.md` §12.


---

## Module ID Canonicalization Gate (DCP-002)

The Blueprint (`Blueprint_Data`) is the canonical authority for every `MOD-xxxx` ID and canonical name. **Never invent a MOD / PSS / NEW identity:** Blueprint lookup → existing MOD or FU when available → otherwise `CAND-CAP-####` only (governance identity, never a runtime literal). Before creating or reserving any ID run the fail-closed preflight — a non-zero exit BLOCKS pack creation:

`python3 .antigravity/scripts/verify_module_id.py . --check-id MOD-XXXX --name "Canonical Name" [--parent MOD-YYYY] [--repo-only]`
`python3 .antigravity/scripts/verify_module_id.py . --candidate CAND-CAP-#### --name "Capability Name"`

The six gate steps, repo-only reservation and the CAND-CAP lifecycle: `.antigravity/AGENTS-REFERENCE.md` (DCP-002). Authority and policy: `execution/portfolio/delivery-capability-packs/DCP-002-module-identity-canonicalization.md`.
