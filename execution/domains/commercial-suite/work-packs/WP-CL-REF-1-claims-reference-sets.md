# WORK PACKAGE — WP-CL-REF-1 · İddialar v2 referans setleri (ülke içerik dilleri · kapatma nedeni · uyarlama tipi · kanıt tipi) (Platform seed, veri)

> **CT (SoR).** İddialar v2 Faz 1. Kaynak: `SCMM-claims-v2-mockup-analysis-plan.md` (CL-REF-1, K4, D3).
>
> **Amaç:** İddialar v2'nin ihtiyaç duyduğu 4 referans setini **deklaratif katalog yükleyicisiyle** eklemek. Mongo'ya elle yazılmaz.
>
> **Ülke ekseni:** Mevcut Global set **`COUNTRY_CODES`** kullanılır. Bugün 6 değer var (TR, BY, UZ, TM, GE, AZ). **Kullanıcı kararı: 6 ülkeyle başla.** Bu WP `COUNTRY_CODES`'a DOKUNMAZ.
>
> **Çalışma yeri:** worktree `C:\tmp\cl-ref-1`, dal `wp/cl-ref-1`. Commit bu dala, push YOK.

## Kanıt (CT)
- **Katalog yükleyici:** `Platform.API/Services/BusinessReferenceData/BusinessReferenceDataCatalogLoadWorker.cs`.
  - `CatalogPath`'in **klasöründeki tüm `.json` katalogları** yükleniyor.
  - Başlangıçta seti oluşturuyor ve v1'i yayınlıyor. İdempotent: var olan set atlanıyor.
- **Katalog klasörü:** `services/Diten.Platform/src/Diten.Platform.API/Seed/business-reference-data/`.
  - Örnekler: `document-management-qms.json` (tenant ve global setler), `mod-0290-gsku-reference.json` (**`attribute_definitions` + değer `attributes`** örneği: `uom`).
- **Ayar:** `Platform.API/appsettings.Development.json` → `BusinessReferenceData.CatalogLoad` (TenantId = 97c5, `RequiredSetCodes`).
- **Scope tuzağı (memory `brd-catalog-loader-seed-path`):**
  - **Global** set `scope_key` ile okunursa 400 döner.
  - **Tenant** set `scope_key` olmadan okunursa 400 döner.
- **`COUNTRY_CODES`** (Global, yayında): TR, BY, UZ, TM, GE, AZ (büyük harf).

## NE
Yeni katalog dosyası `Seed/business-reference-data/crm-claims-reference.json` (`catalog_version` / `module` / `note` / `sets` yapısı mevcutlarla aynı):

| set_code | scope | değerler (value_code → display_name) | not |
|---|---|---|---|
| `country-content-languages` | **global** | `TR`→Turkey · `BY`→Belarus · `UZ`→Uzbekistan · `TM`→Turkmenistan · `GE`→Georgia · `AZ`→Azerbaijan | `attribute_definitions`: `Languages` (string, zorunlu), virgüllü ISO-639-1 listesi. **TR `tr` · BY `ru,be` · UZ `uz,ru` · TM `tk,ru` · GE `ka` · AZ `az`**. Değer kodları `COUNTRY_CODES` ile birebir aynı. |
| `claim-country-closure-reason` | **tenant** | `no-license`→No marketing authorisation · `regulation-disallows`→Local regulation does not allow · `business-decision`→Business decision | İddia × ülke hücresini "açılmayacak" işaretlerken tek seçim. 7 dil etiketi UI resx'inden gelir (value_code anahtar). |
| `claim-adaptation-type` | **tenant** | `verbatim`→Verbatim translation · `narrowed`→Narrowed · `softened`→Softened | Ülke sürümünün çekirdeğe göre uyarlaması. `verbatim` dışındakilerde gerekçe zorunlu (kural CL-BE-1'de). |
| `evidence-type` | **global** | `smpc-pil`→SmPC / PIL · `clinical-study`→Clinical study · `literature`→Literature · `internal-data`→Internal data · `regulatory-letter`→Regulatory letter | MOD-0031 (CL-BE-2) doğrular. Platform geneli, bu yüzden global. |

- Her değerde şunlar dolu olsun: `is_active: true`, `sort_order` (10, 20…), kısa İngilizce `description`.
- `appsettings.Development.json` → `RequiredSetCodes`'a 4 kod eklenir.
- **Doğrulama (canlı, yazmasız):**
  - Platform'u **fleet restart** ile yeniden başlat.
  - `diten_personalization_dev.business_reference_data_sets`: 4 setin `PublishedVersionId`'si Published (Status 1) sürüme işaret ediyor.
  - Published-values ucu: global setler `scope_key`'siz 200, tenant setler `scope_key=97c5…` ile 200.
  - `country-content-languages` değerlerinde `Languages` attribute'u dolu.

## KORU / YAPMA
- **`COUNTRY_CODES` ve mevcut katalog dosyaları ile setleri DEĞİŞMEZ.** Yalnız yeni dosya eklenir + `RequiredSetCodes` güncellenir.
- Mongo'ya doğrudan yazma YOK. Set eklemek yalnız yükleyiciyle yapılır.
- CRM, Auth, Web ve kod DOKUNMA. Yükleyici davranışı bozulmuyorsa yeni kod yazma.
- **DUR:**
  - Yükleyici `attribute_definitions`'ı global sette desteklemiyorsa → attribute'u kaldırma. Dur ve raporla (alternatif: dil listesini ayrı `Mappings` alanında tutmak, CT kararı).
  - Aynı set kodu başka katalogda varsa → dur ve raporla.
  - `FailOnBlockedConflicts` Platform açılışını durdurursa → dur, logu raporla.

## Acceptance
- **E2:**
  - `dotnet test services/Diten.Platform/tests/Diten.Platform.Application.Tests -c Release --nologo` → **0 kırmızı**. Katalog JSON'unu doğrulayan mevcut test varsa yeni dosyayı da kapsar; yoksa en az bir **katalog ayrıştırma testi** eklenir.
  - Platform build 0 hata.
- **E4 (fleet restart, yazmasız okuma):** 4 set yayında, değer sayıları 6 / 3 / 3 / 5, scope doğru, `Languages` dolu.
- **Diff:** yalnız yeni katalog JSON + `appsettings.Development.json` (`RequiredSetCodes`) + (varsa) test.

---

## §36.1 Agent Prompt (paste-ready) — DISPATCH: owner
```text
@[.antigravity/agents/backend-specialist.md]
WP: WP-CL-REF-1 · İddialar v2 referans setleri (ülke içerik dilleri · kapatma nedeni · uyarlama tipi · kanıt tipi) (Platform seed, veri)
Repository: C:\tmp\cl-ref-1 (worktree) · Branch: wp/cl-ref-1 (taban feature/crm-claims-v2) · commit bu dala, push YOK

Amaç: İddialar v2 için 4 BRD setini deklaratif katalog yükleyicisiyle ekle (Mongo'ya elle yazma YOK). Ülke ekseni mevcut Global COUNTRY_CODES (TR,BY,UZ,TM,GE,AZ) — DOKUNMA; kullanıcı kararı 6 ülkeyle başla.

Önce oku: execution/domains/commercial-suite/work-packs/WP-CL-REF-1-claims-reference-sets.md (4 set tablosu) · services/Diten.Platform/src/Diten.Platform.API/Services/BusinessReferenceData/BusinessReferenceDataCatalogLoadWorker.cs · services/Diten.Platform/src/Diten.Platform.API/Seed/business-reference-data/{document-management-qms.json, mod-0290-gsku-reference.json} (attribute_definitions örneği: uom) · services/Diten.Platform/src/Diten.Platform.API/appsettings.Development.json (BusinessReferenceData.CatalogLoad) · memory brd-catalog-loader-seed-path (global=scope_key YOK, tenant=scope_key ZORUNLU).

NE:
 1) Yeni Seed/business-reference-data/crm-claims-reference.json: country-content-languages (global; TR,BY,UZ,TM,GE,AZ; attribute Languages string zorunlu: TR "tr", BY "ru,be", UZ "uz,ru", TM "tk,ru", GE "ka", AZ "az") · claim-country-closure-reason (tenant; no-license, regulation-disallows, business-decision) · claim-adaptation-type (tenant; verbatim, narrowed, softened) · evidence-type (global; smpc-pil, clinical-study, literature, internal-data, regulatory-letter). Display name/description WP tablosundaki gibi, is_active true, sort_order 10,20,…
 2) appsettings.Development.json RequiredSetCodes'a 4 kodu ekle.
 3) Katalog ayrıştırma testi (mevcut katalog testi varsa yeni dosyayı kapsat; yoksa ekle).
 4) Canlı doğrulamayı SEN YAPMA (fleet ana checkout'tan çalışır, worktree'den fleet çalıştırma YOK) — CT birleştirip fleet restart sonrası yapar: Mongo salt-okuma (4 set Published, değer sayıları 6/3/3/5, Languages dolu) + published-values ucu (global scope_key'siz 200, tenant scope_key=97c59330-dbc4-4665-b29c-0c26dbb5cc93 ile 200). Sen raporda bu kontrol listesini hazır bırak.
KORU/YAPMA: COUNTRY_CODES ve mevcut katalog/setler DEĞİŞMEZ; Mongo'ya doğrudan yazma YOK; CRM/Auth/Web/yükleyici kodu DOKUNMA.
DOĞRULA (E2): cd C:\tmp\cl-ref-1; dotnet test services/Diten.Platform/tests/Diten.Platform.Application.Tests -c Release --nologo → 0 kırmızı; Platform build 0 hata; git diff yalnız yeni JSON + appsettings RequiredSetCodes + (varsa) test. Commit ("feat(platform): WP-CL-REF-1 — claims v2 reference sets (country-content-languages, closure reason, adaptation type, evidence type)" + son satır Co-Authored-By: Claude Opus 5.5 <noreply@anthropic.com>). §22 TÜRKÇE. K13.
Durma: global sette attribute_definitions desteklenmiyorsa DUR+raporla (attribute'u sessizce kaldırma); set kodu başka katalogda varsa DUR; FailOnBlockedConflicts açılışı durdurursa DUR+log.
Not: canlı doğrulama için fleet ana checkout'tan çalışır; worktree'deki JSON'u canlıda denemek için CT'ye raporla — CT birleştirip restart sonrası doğrular (worktree'den fleet çalıştırma YOK).
```
