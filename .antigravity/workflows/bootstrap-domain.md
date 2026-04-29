# Workflow: Bootstrap Domain from Excel

Bu workflow, bir Excel planlama dosyasındaki verileri kullanarak Diten ERP vNext `execution/` katmanını (Domain Config + Module Packs) otomatik olarak inşa etmek için kullanılır.

## Giriş Koşulları
1.  Kök dizinde veya `execution/` altında güncel bir planlama Excel'i (`modules_pages_planning_v3.xlsx`) bulunmalıdır.
2.  `docs/sop/upstream/` altındaki SOP kuralları geçerlidir.

## Akış Fazları

### Faz 1: Excel Analizi (Intake)
- **Komut:** `python3 .antigravity/scripts/excel_parser.py --domain {DomainName}`
- AI, Excel'deki ilgili domain'e ait tüm satırları tarar.
- Modül ID'leri (MOD-xxxx veya MDM-xxxx), modül isimleri, sayfa yapıları ve "Wave" bilgileri toplanır.

### Faz 2: Domain Yapılandırması
- İlgili `execution/domains/{domain-name}/` klasörü oluşturulur.
- **`domain-config.md`:** Excel'deki kapsam (in-scope) modülleri temel alınarak oluşturulur.
- **`decisions/`:** `runtime-decisions.md` ve `ownership-decisions.md` iskeletleri SOP standartlarına göre kurulur.

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
