# Workflow: Bootstrap Domain from Excel

Bu workflow, bir Excel planlama dosyasındaki verileri kullanarak Diten ERP vNext `execution/` katmanını (Domain Config + Module Packs) otomatik olarak inşa etmek için kullanılır.

## Giriş Koşulları
1.  Kök dizinde veya `execution/` altında güncel bir planlama Excel'i (`modules_pages_planning_v3.xlsx`) bulunmalıdır.
2.  `docs/guides/sop-upstream/` altındaki SOP kuralları geçerlidir.

## Akış Fazları

### Faz 1: Excel Analizi (Intake)
- **Komut:** `python3 .antigravity/scripts/excel_parser.py --domain {DomainName}`
- AI, Excel'deki ilgili domain'e ait tüm satırları tarar.
- Modül ID'leri (MOD-xxxx veya MDM-xxxx), modül isimleri, sayfa yapıları ve "Wave" bilgileri toplanır.

### Faz 2: Domain Yapılandırması
- İlgili `execution/domains/{domain-name}/` klasörü oluşturulur.
- **`domain-config.md`:** Excel'deki kapsam (in-scope) modülleri temel alınarak oluşturulur.
- `controls/`, `decisions/`, `batches/`, `snapshots/` katmanları **kurulmaz**. Engineering kuralları `.antigravity/rules/`, scope/MVP kararları `execution/portfolio/master-development-plan.md` üzerinden taşınır.

### Faz 3: Modül Paketi Üretimi (Bulk Creation)
- Excel'deki her satır için bir `{DOMAIN}-{NNN}-{slug}.md` dosyası oluşturulur.
- Her dosyanın başına zorunlu **YAML Frontmatter** eklenir.
- **Acceptance Criteria (AC):** Excel'de belirtilen sayfa yapıları (Overview, Catalog, Detail) ve fonksiyonel gereksinimler (H1-1, H2-1 vb.) AC maddelerine dönüştürülür.
- Yeni module pack'ler varsayılan olarak `status: draft` üretilir. Geliştirme için kullanıcı incelemesi sonrası `approved` veya `ready-for-dev` status gerekir.
- DataTable modüllerinde `form_field_count` ve `golden_reference: slim|compact` kararı yazılır.

### Faz 3.5: Mühendislik Standartları ve Teknik Arınma (ZORUNLU)
- **`domain-config.md` Kuralı:** Bu dosya içerisinde MongoDB, Soft Delete, JWT, API Envelope gibi teknik uygulama detaylarının yazılması **KESİNLİKLE YASAKTIR**.
- **Referans Zorunluluğu:** Tüm teknik kararlar için `.antigravity/rules/` altındaki ilgili dosyalara (Örn: `erp-architecture.md`) Markdown linki verilmelidir.
- **Modül Ayrımı:** Excel parser'dan gelen her bir modül (`MOD-xxx`), `module-packs/` altında kendi bağımsız `.md` dosyasına sahip olmalıdır. "Tek bir büyük dosya" yaklaşımı reddedilir.

- **Denetim kaydı (AUD-001):** `domain-config.md`, domain'in servisinin denetim yolunu tek satırla söyler ve kurala link verir: Platform içi (`a`), ortak iletici (`b`, K4) ya da kabul edilmiş bir eşdeğer iz (`c`). Servisin izi yoksa (bkz. `tests/architecture/audit-ledger/`) bu bir **engel** olarak yazılır: o domain'de yazma komutu içeren module pack `ready-for-dev` olamaz. Üretilen her module pack `Audited Events` bölümünü taşır (`module-pack-standard.md` Bölüm 10.1).

### Faz 4: Sokratik Doğrulama & Güncelleme Yönetimi
- AI, eksik gördüğü veya Excel'de çelişen durumları kullanıcıya raporlar.
- **Akıllı Güncelleme Politikası:** Eğer Excel dosyası projenin ortasında güncellenirse, AI mevcut dosyaların üzerine körü körüne yazmaz.
  - Mevcut Module Pack ile Excel arasındaki farkları analiz eder.
  - Kullanıcıya şu seçenekleri sunar:
    1. **Eskisini Korum (Keep):** Manuel düzenlemeler yapılmışsa tercih edilir.
    2. **Excel'e Göre Güncelle (Overwrite):** Excel planı nihai ise tercih edilir.
    3. **Akıllı Birleştirme (Smart Merge):** Yeni AC maddelerini eskisinin altına ekler.
- Kullanıcıdan "Geliştirmeye başlanabilir mi?" onayı alınır.

---

## Dış AI'lar İçin Prompt Şablonu (External Handoff)

Eğer bu işlemi dış bir AI'a (ChatGPT/Gemini/Claude) yaptıracaksanız, şu prompt'u kullanın:

> "Sana bir Excel dosyası ve Diten ERP vNext SOP dokümanlarını veriyorum. 
> 
> **GÖREVİN:** Bu Excel'deki [DOMAIN ADI] satırlarını analiz et ve bana Diten SOP standartlarına (Module Pack > Domain Config > AGENTS.md) uygun olarak şu dosyaların içeriğini Markdown formatında üret:
> 
> 1. `domain-config.md` (Domain kapsamı ve kuralları)
> 2. Excel'deki her modül için bir modül paket dosyası (Örn: `MDM-001-currency-management.md`)
> 
> **DİKKAT:** 
> - Modül paketleri mutlaka YAML frontmatter içermeli.
> - Excel'deki sayfa yapılarını 'Acceptance Criteria' bölümüne detaylıca işle.
> - Klasör yollarını `services/`, `frontend/`, `gateway/` olarak kullan."

---

## Kalite Kapısı
- [ ] Tüm modül ID'leri Excel ile tutarlı mı?
- [ ] YAML frontmatter eksiksiz mi?
- [ ] SOP hiyerarşisine uyuldu mu?
- [ ] Dosyalar `execution/domains/` altında doğru yerleştirildi mi?

---

## Çıktı ve `prepare-module-pack` İlişkisi

`bootstrap-domain` workflow'u toplu (bulk) module pack üretir; çıktı **her zaman** `status: draft`'tır. Geliştirmeye geçilmeden önce her pack için iki yoldan biri seçilir:

- **(a) Hızlı yol — yalnız scope onayı:** Pack içeriği Excel'le birebir doğru ve eksiksizse kullanıcı manuel olarak `status: approved`/`ready-for-dev` yapar; orchestrator'a doğrudan teslim edilir.
- **(b) Rafine yol — `prepare-module-pack`:** İş kuralları, AC, alan listesi veya golden reference kararı detaylandırılması gerekiyorsa pack `prepare-module-pack` workflow'una verilir; `module-pack-author` ajanı pack'i `Module Summary`, `Acceptance Criteria`, `Test Expectations`, `Runtime Constraints` gibi bölümlerle zenginleştirir ve sonra `approved`'a alır.

`bootstrap-domain` ASLA doğrudan `@orchestrator`'a teslim etmez. `@orchestrator` yalnızca `approved`/`ready-for-dev` pack ile geliştirmeyi başlatır (`add-module.md` Phase 0 kapısı).


---

## Module ID Canonicalization Gate (DCP-002)

The Blueprint (`docs/reference/blueprint/System Capability & Implementation Blueprint - master 8.7.xlsx` :: `Blueprint_Data`) is the canonical authority for every `MOD-xxxx` ID and canonical name. Master 7 is historical predecessor evidence only. Before creating or reserving any `MOD-xxxx` (new module, FU/child, or reservation):

1. **Blueprint lookup** — the ID + canonical name must exist in `Blueprint_Data`, or the ID must be an FU/child of an existing Blueprint MOD parent.
2. **Registry collision** — it must not already map to a different capability in `execution/registries/module-id-registry.md`.
3. **Canonical-name validation** — the pack `name` must match the Blueprint canonical name (or an approved alias).
4. **Parent/FU/child decision** — decide explicitly whether the work is a new module or an FU/child of an existing module before minting an ID.
5. **No repo-only identity (Master-first)** — a capability absent from the Blueprint goes to a Module Change Request in the Master; no placeholder, next-free or repo-only ID may be invented. Enterprise Architect reservations recorded in the registry before 2026-10-16 stay as recorded (`--repo-only` proves them).
6. **Preflight (fail-closed)** — run `python3 .antigravity/scripts/verify_module_id.py . --check-id MOD-XXXX --name "Canonical Name" [--parent MOD-YYYY] [--repo-only]`. A non-zero exit BLOCKS pack creation.

Authority and policy: `execution/portfolio/delivery-capability-packs/DCP-002-module-identity-canonicalization.md`. Legacy (`PSS-*`, `NEW-*`) and repo-only IDs are valid only as deprecated aliases of a canonical MOD id; a missing canonical id is a Module Change Request in the Master (Master-first rule), not a repository reservation.

### Master-first rule and the closed CAND-CAP namespace (DCP-002, Blueprint 8.7)

**Master-first rule (CEO, 2026-10-16; Blueprint 8.7 `Module_Change_Requests`):** no module may be created, coded or registered outside the Master. When the Blueprint has no capability and no existing MOD/FU fits, **stop**: raise a Module Change Request in the Master's `Module_Change_Requests` sheet (through the Blueprint owner) and continue only after the Master gives a new MOD-ID or an approved mapping. Code, routes, permissions and data objects carry a MOD-ID that exists in `Blueprint_Data`.

`CAND-CAP-####` is a closed, historical namespace: no new candidate identity is opened. The existing ones are deprecated aliases of the canonical targets Master 8.2 / 8.7 gave them (`execution/portfolio/blueprint-master-plan-reconciliation.md` § Master 8.7 resolution). The candidate gate refuses a new or a deprecated candidate and names its canonical target:

`python3 .antigravity/scripts/verify_module_id.py . --candidate CAND-CAP-#### --name "Capability Name"`

New-module identity rule: **Blueprint lookup → existing MOD or FU when available → otherwise a Module Change Request in the Master → never invent a MOD / PSS / NEW identity, and open no new CAND-CAP.**
