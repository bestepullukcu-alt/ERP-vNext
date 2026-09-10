# WORK PACKAGE — WP-DM-1c · Portable seed teslimi (CSV runtime-asset + committed config)

> **Control Tower kaydı (SoR).** DM-1 devamı. Module: **MOD-0029**. Branch: `feature/dm-document-master-register` (HEAD d61ac2c1).
> **Amaç:** DM-1'in seed mekanizması ÇALIŞIYOR (E4 kanıtlı) ama **push→arkadaş otomatik seed** için CSV + config **commit'li/taşınabilir** değil. Bu WP o boşluğu kapatır. Arkadaş `git pull` + dev fleet → 358 doküman fresh 97c5'e seed olur.

## Ölçülmüş girdi (CT, 2026-09-10)
- **E4 bulgusu:** DM-1b seed canlı doğrulandı (358 doc, 0 citable) AMA (1) `docs/…/GMG…csv` build output'a kopyalanmıyor → CsvPath taşınabilir değil; (2) `DocumentRegisterSeed` config commit'li değil (local-only, E4 sonrası geri alındı). İkisi olmadan arkadaş pull'da seed KOŞMAZ.
- **BRD deseni (birebir mirror):** seed asset = `Diten.Platform.API/Seed/business-reference-data/*.json`; `.csproj`: `<Content Update="Seed\...\x.json" CopyToOutputDirectory="PreserveNewest" />` (satır 33-34); committed config `BusinessReferenceData:CatalogLoad` `appsettings.Development.json`'da `Enabled:true` + `TenantId:"97c59330-..."` + relative `CatalogPath:"Seed/business-reference-data/..."`; runtime çözüm `Path.GetFullPath(CatalogPath)`.
- **DM-1b mevcut path kullanımı:** `DocumentRegisterSeed.EnsureSeededAsync` → gate `File.Exists` (raw) + `IngestAsync` `File.ReadAllTextAsync(raw csvPath)`. Relative path için **`Path.GetFullPath` sarması** gerek (aksi cwd'ye bağlı, deploy'da kırılır).
- **KARAR-2 uyumu:** `appsettings.Development.json`'a **97c5 (paylaşılan dev tenant)** yazmak repo'nun MEVCUT deseni (BRD.CatalogLoad zaten öyle) → hardcode-in-code DEĞİL, dev config; prod'da (`appsettings.Production.json` yok) → skip. Arkadaş farklı tenant kullanıyorsa kendi local'inde TenantId override eder.

## Kapsam
1. **CSV runtime-asset:** `docs/reference/integrations/gmg-qms/GMG_ERP_Document_Reference_List_2026-08-24.csv` → **kopyala** `services/Diten.Platform/src/Diten.Platform.API/Seed/document-management/GMG_ERP_Document_Reference_List_2026-08-24.csv`. (docs/ kopyası SoT kalır; Seed/ = runtime; BRD gibi kabul edilebilir ikilik.)
2. **.csproj:** `Diten.Platform.API.csproj` içindeki BRD Content bloğuna (satır ~33) ekle: `<Content Update="Seed\document-management\GMG_ERP_Document_Reference_List_2026-08-24.csv" CopyToOutputDirectory="PreserveNewest" />`.
3. **Committed config:** `appsettings.Development.json`'a `DocumentRegisterSeed` bölümü (BRD.CatalogLoad mirror): `{ "Enabled": true, "TenantId": "97c59330-dbc4-4665-b29c-0c26dbb5cc93", "CsvPath": "Seed/document-management/GMG_ERP_Document_Reference_List_2026-08-24.csv" }`.
4. **Path çözüm robustluğu (DM-1b'de küçük düzeltme):** `DocumentRegisterSeed.EnsureSeededAsync`/`IngestAsync` relative CsvPath'i **`Path.GetFullPath`** ile çözsün — gate'e geçen `fileExists` ve `File.ReadAllTextAsync` tam yol kullansın (BRD deseni). Null/boş guard korunur.

## YAPMA
- Prod appsettings'e config YAZMA (yalnız Development). TenantId'yi KODA hardcode etme (config'te dev tenant OK — BRD deseni). Seed mantığını/mapping'i DEĞİŞTİRME (yalnız path çözümü). PlatformCollections/şema/MOD-0262 dokunma. Yeni CSV içeriği üretme (mevcut dosyayı kopyala).

## Acceptance
- **E2:** build temiz; `.csproj` Content kopyalama → `bin/…/Seed/document-management/GMG…csv` oluşur; DM-1b unit testleri hâlâ 17/17 (path değişimi mevcut absolute/inline testleri kırmamalı — gerekirse `Path.GetFullPath` idempotent, absolute path'i değiştirmez); relative-path gate testi eklenebilir.
- **Regresyon:** tam Platform.Application.Tests — yeni fail YOK (baseline-diff; env/Mongo + 14 release-gate hariç).
- **E4 (CT yapacak, fleet):** temiz `document_management_master_register` (97c5 boş) + fleet restart → **358 doc otomatik seed** (committed config ile, elle appsettings düzenlemeden); relative CsvPath çözülür; 0 citable/36 blocked/Guid.Empty. (CT kendi doğrular — brownfield guard nedeniyle test öncesi 97c5 register temiz olmalı.)
- **Kapsam:** yalnız CSV-asset + csproj + committed config + path-çözüm. Seed mantığı/mapping değişmez.

---

## §36.1 Agent Prompt (paste-ready)

```text
@[.antigravity/agents/backend-architect.md]
WP: WP-DM-1c · Prompt P-DM-1c v1.0  (Portable seed teslimi — CSV runtime-asset + committed config — MOD-0029)

Repository: C:\Users\user\Desktop\ERP-vNext
Branch: feature/dm-document-master-register · Expected HEAD: d61ac2c1 · Worktree: ana checkout

Önce oku (BRD deseni — birebir mirror):
1. execution/domains/platform-shared-services/work-packs/WP-DM-1c-portable-seed-delivery.md (bu WP)
2. services/Diten.Platform/src/Diten.Platform.API/Diten.Platform.API.csproj (Seed\business-reference-data Content Update satırları ~33-34)
3. services/Diten.Platform/src/Diten.Platform.API/appsettings.Development.json (BusinessReferenceData:CatalogLoad bölümü — Enabled/TenantId/relative CatalogPath deseni)
4. services/Diten.Platform/.../Infrastructure/Persistence/Configurations/DocumentRegisterSeed.cs (EnsureSeededAsync/IngestAsync — File.Exists/ReadAllText path kullanımı) + DocumentRegisterSeedGate.cs

NE:
 1) CSV'yi runtime-asset olarak KOPYALA: docs/reference/integrations/gmg-qms/GMG_ERP_Document_Reference_List_2026-08-24.csv
    → services/Diten.Platform/src/Diten.Platform.API/Seed/document-management/GMG_ERP_Document_Reference_List_2026-08-24.csv
    (docs/ kopyasını SİLME — SoT kalır). İçeriği DEĞİŞTİRME.
 2) .csproj: BRD Content bloğuna ekle:
    <Content Update="Seed\document-management\GMG_ERP_Document_Reference_List_2026-08-24.csv" CopyToOutputDirectory="PreserveNewest" />
 3) appsettings.Development.json'a DocumentRegisterSeed bölümü ekle (BRD.CatalogLoad mirror):
    "DocumentRegisterSeed": { "Enabled": true, "TenantId": "97c59330-dbc4-4665-b29c-0c26dbb5cc93",
      "CsvPath": "Seed/document-management/GMG_ERP_Document_Reference_List_2026-08-24.csv" }
 4) DocumentRegisterSeed'de relative-path çözümü: EnsureSeededAsync gate'e File.Exists yerine (p => File.Exists(Path.GetFullPath(p)))
    ver VE IngestAsync File.ReadAllTextAsync(Path.GetFullPath(csvPath)) kullansın. Null/boş guard KORUNUR (Path.GetFullPath boşta atar → önce IsNullOrWhiteSpace). absolute path'te Path.GetFullPath idempotent (mevcut testleri kırmaz).
NEDEN: DM-1b seed'i çalışıyor (E4 kanıtlı) ama CSV build'e kopyalanmadığı + config commit'li olmadığı için arkadaş pull'da seed KOŞMUYOR. Bu WP push→otomatik seed'i teslim eder.
NASIL: BRD (BusinessReferenceData:CatalogLoad) desenini birebir izle — aynı Content/CopyToOutputDirectory, aynı committed dev-tenant config, aynı Path.GetFullPath çözümü.
YAPMA: prod appsettings'e yazma (yalnız Development); TenantId'yi KODA hardcode etme (config OK); seed mantığı/DM-0 mapping DEĞİŞTİRME (yalnız path çözümü + asset + config); PlatformCollections/şema/MOD-0262 dokunma; yeni CSV üretme.
DOĞRULA (E2):
 - build temiz; bin/Debug/net8.0/Seed/document-management/GMG…csv OLUŞUR (CopyToOutputDirectory çalışıyor).
 - DM-1b unit testleri 17/17 (Path.GetFullPath absolute'u bozmaz); relative-path gate unit testi ekle (Seed/ göreli yol File.Exists → true dev'de).
 - TAM Platform.Application.Tests: yeni fail YOK (env/Mongo + 14 release-gate hariç; baseline-diff).
Ayrı commit. §22 raporu TÜRKÇE. Senin PASS'in kapanış değildir (K13) — CT E4'ü fleet'te (temiz 97c5 + restart → 358 otomatik seed) bağımsız doğrular.

Durma koşulları: CSV kopyalama BRD deseniyle çakışırsa · Path.GetFullPath mevcut absolute-path testlerini kırıyorsa (raporla) · committed config KARAR-2 ile çelişir görünüyorsa (BRD precedent'i not et, DUR) · kapsam asset+config+path dışına taşarsa. DUR + raporla.
```

## Kalan (bu WP dışı)
- CT E4 (fleet, temiz 97c5): committed config ile otomatik 358 seed doğrulama.
- DM branch push + PR (arkadaşa ulaşsın).
- DM-2b (Search/Resolve canlı) · DM-3 (effectiveness:batch) · DM-4 (gerçek dosya, MOD-0262).
