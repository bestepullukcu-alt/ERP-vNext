---
id: MOD-0179
name: 3PL Integration
domain: supply-chain-execution
service: Diten.InventoryService
shell: tenant
golden_reference: slim
data_mode: server
screens:
  - module: ThirdPartyLogistics
    data_mode: server
  - module: ThirdPartyChannels
    data_mode: server
  - module: ThirdPartyMappings
    data_mode: server
  - module: ThirdPartyMessages
    data_mode: server
  - module: ThirdPartyExceptions
    data_mode: server
  - module: ThirdPartyReconciliation
    data_mode: server
  - module: ThirdPartySettings
    data_mode: server
entity_base: EntityBase
status: ready-for-dev
approval_status: pending
revision: "r3 (2026-11-13, dar tur) — bağımsız inceleme r2 (docs/records/reviews/MOD-0179/2026-11-13-073e8ffb.md, KOŞULLU, F-13 orta, F-14…F-17 düşük) işlendi; yalnız bu maddelerin bölümleri değişti (kapanış tablosu §19.7): F-13 sır referansı sunucuda ThirdPartyLogistics:{TenantId}:{ChannelId}:{SecretName} olarak üretilir, istemci referans vermez, çözücü referansı kiracı + kanaldan yeniden kurar (AC-39 + S-39c); F-14 gelen yol IsNonTenantPath'e alınır (bypass ve başlık yazımı yok; X-19, AC-63 bypass açık / kapalı); F-15 dış release ön denetimleri tahsislerden (X-10 (2), AC-43 + S-43d); F-16 AC-34 seçim kısmı DL-4 kabulüne, DL-3 Web testinde sahte proxy (yeni uç yok); F-17 atıf kalıntıları (registry :70 notu, :160-161, AC-20 / AC-25 dilim sütunları). Künye ready-for-dev / pending değişmedi. r2 (2026-11-13) — bağımsız inceleme r1 (docs/records/reviews/MOD-0179/2026-11-13-aca86685.md, KOŞULLU, F-1…F-5 orta, F-6…F-12 düşük) ve sahip kararları 01'in son üç maddesi (Q-1…Q-9 öneri kabul; r1 inceleme kararları F-11 / F-8 / F-5 / F-10; Q-10 = MOD-0049-FU01) işlendi; F-1…F-12 kapandı (kapanış tablosu §19.6). Başlıca değişiklikler: v1.0 yalnız API kanalı, SFTP ve EDI v1.1 (F-11); DL-1 ikiye bölündü ve dilimler yeniden numaralandı (r1 → r2 eşlemesi §23); 3PL dış depo çıkışı tek model — dış release Allocated → ReadyToShip + outbox + talimat, sevk onayı → MOD-0183 dispatch → Shipped (F-1); Gateway kiracı ara katmanı seam'i X-19 ve bypass kapalı ölçüm (F-2); bekçi değerleri r2 HEAD'inde yeniden ölçüldü (F-3); Q-1…Q-9 karar olarak yazıldı, AS2 çıktı (F-4); ham içerikte kişisel veri 90 gün sonra kaldırılır (F-5); tek stoklanabilir lokasyon kuralı (F-6); dilim kapıları (F-7); ekranda 'v2 · planlı' ücret notu (F-8); sır akışı ve sabit süreli imza karşılaştırması (F-9); sır referansı ara çözümü — kasa FU'su sonraki adım (F-10); sınıf düzeyi HasPermission yasağı ve ara katman sabotajı (F-12); MDM iş ortağı MOD-0049-FU01'de, MOD-0179 yalnız okur (Q-10). Künye status ready-for-dev (çağıran CT talimatı, S-49); approval_status pending. r1 (2026-11-13) — ilk taslak (commit 241659850, LF sha256 aca86685…)."
status_note: "ready-for-dev r3 — approval_status pending; kod izni yok. Paket onayı için: r3 fark incelemesi (CT-APR-02) + onay satırı (CT-APR-01, onaylayan Ali / GitHub GMG-ERP; yazan ≠ onaylayan). Onaydan önce AÇIK kalan kapı: .antigravity satırları — PKS-001 warehouse.third-party-logistics.* (Q-1) ve RCS-001 TPX; aynı adımda X-17 (MOD-0178 §14.1 notu) ve X-18 (RecordCodeTypeCatalogTests). Bu paket .antigravity/**'a yazmaz. Dilim kapıları (paket onayını bekletmez): MOD-0049-FU01 API'si main'de (DL-4), MOD-0183 sahibi Q-18 (DL-6), procurement Q-19 (DL-7), servis kimliği Q-12 (DL-6, DL-7). v1.1 (DL-10 SFTP, DL-11 EDI) v1.0 kabulünden sonra. Ücret mutabakatı v2 (T-2); Treasury notu CT işi."
owner: supply-chain-execution / merkez (Beste) — MVP-5 WMS CT
approver: Ali (GitHub GMG-ERP)
risk: "HIGH — (1) internete açık, imzayla korunan bir giriş ucu (Gateway kiracı ara katmanında adlı muafiyet — X-19) ve dış sisteme giden çağrılar; (2) kanal sırları (v1.0 referans ara çözümü, kasa FU'su sonraki adım); (3) stok etkisi: 3PL onayları MOD-0183 dispatch ve MOD-0142 GRN yoluyla deftere yansır; (4) 4 servis, 4 domain ve 2 MVP hattında seam (MDM okuması, MOD-0178, MOD-0183, MOD-0142, Gateway); (5) Blueprint SLO Tier 1; (6) ham mesajlarda kişisel veri (90 gün kuralı)."
branch: feature/sce/mod-0173-inventory-ledger-valuation
started: 2026-11-13
target: "sahip tarihi verilmedi"
form_field_count: 8
dependencies: [MOD-0049-FU01 Business Partner Master Foundation (MDM iş ortağı; MOD-0179 yalnız okur — DL-4 kapısı), MOD-0178 (outbound, dış release, sync-dispatch — XMC X-10), MOD-0183 (dispatch ve GOODS_ISSUE_SALES tek yazıcısı — XMC X-12, MVP-6), MOD-0142 (GRN ve GOODS_RECEIPT_PO — XMC X-13, procurement), MOD-0141 (PO okuması), MOD-0173 / MOD-0173-FU02 (defter okuması, LOCATION operatingMode ve stoklanabilir lokasyon), MOD-0174 (lot okuması), MOD-0290 (SKU okuması), MOD-0012 (sır sağlayıcısı — v1.0 referans ara çözümü), MOD-0012-FU01 Tenant Runtime Secret Vault (kiracı kasası — sonraki adım, v1.0 kapısı değil; F-10), MOD-0033-FU02 (servis kimliği — Q-12), MOD-0032 (Gateway kiracı ara katmanı — X-19), MOD-0040-FU02 (RCS-001 ortak servisi; ara üretici MOD-0178 geçici sayacı), MOD-0018 (RBAC), BL-501 (merkezi denetim), MOD-0037 (Blueprint bağımlılık kapısı — ertelenmiş, W-0179-01), 3PL-BUNDLE (bu paketin sözleşmesi), WMS-BUNDLE 1.1.0, DEV-0000]
---

# MOD-0179 — 3PL Integration (3PL Entegrasyonu)

> **ready-for-dev (r3, 2026-11-13).** `status: ready-for-dev`, `approval_status: pending`. **Paket onaylanmadan geliştirme başlamaz:** kod yalnız
> `approved` / `ready-for-dev` künye + bağımsız inceleme kaydı (CT-APR-02) + onay satırı (CT-APR-01, **onaylayan Ali / GitHub `GMG-ERP`**) ile
> yazılır (AGENTS.md Çekirdek madde 7). **Onaydan önce açık kapı:** `.antigravity` satırları (PKS-001 `warehouse.third-party-logistics.*`, RCS-001 `TPX`,
> aynı adımda X-17, X-18) — §6. Bu sürüm kod, commit, push, PR ya da onay izni değildir.
>
> **r3 revizyon notu (2026-11-13, dar tur).** Girdi: bağımsız inceleme r2 `docs/records/reviews/MOD-0179/2026-11-13-073e8ffb.md` (KOŞULLU; 1 orta,
> 4 düşük; hedef r2 LF sha256 `073e8ffb…`). Yalnız F-13…F-17'nin bölümleri değişti; kapanış §19.7. **F-13:** sır referansı sunucuda
> `ThirdPartyLogistics:{TenantId}:{ChannelId}:{SecretName}` olarak üretilir; istemci referans göndermez; çözücü referansı her okumada kiracı + kanaldan
> yeniden kurar (AC-39, S-39c). **F-14:** gelen yol `IsNonTenantPath`'e alınır — Development bypass'ı ve `X-Tenant-Id` yazımı o yolda çalışmaz (X-19,
> AC-63 bypass açık ve kapalı). **F-15:** dış release'in ön denetimleri (geri çağırma, lot durumu, SKT / yeniden test, FEFO) tahsislerden (X-10 (2), AC-43,
> S-43d). **F-16:** AC-34'ün seçim kısmı DL-4 kabulünde; DL-3 Web testinde sahte proxy; yeni uç yok. **F-17:** atıf kalıntıları düzeltildi. Komut 26,
> uç 42, AC-1…AC-63 değişmedi; yeni sabotajlar S-34b, S-39c, S-43d, S-63b.
>
> **r2 revizyon notu (2026-11-13).** Girdiler: bağımsız inceleme r1 `docs/records/reviews/MOD-0179/2026-11-13-aca86685.md` (KOŞULLU; 5 orta, 7 düşük;
> hedef r1 LF sha256 `aca86685…`) ve sahip kararları 01'in son üç maddesi (`docs/records/decisions/2026-11/mod0179-3pl-owner-decisions-01.md:33-55`,
> commit `70cb6ca01`). Her bulgunun kapanışı §19.6'dadır. **Sahip kararları:** Q-1…Q-9 öneri kabul (karar olarak yazıldı — F-4); **F-11:** v1.0 yalnız
> API kanalı, SFTP (DL-10) ve EDI (DL-11) v1.1, r1'in DL-1'i ikiye bölündü; **F-8:** ekranlarda ücret için "v2 · planlı" notu; **F-5:** ham içerikte kişisel
> veri 90 gün sonra kaldırılır; **F-10:** sır kasası FU'su gelene kadar kanal yalnız sır **referansı** tutar (DL-5 kasayı beklemez); **Q-10:** MDM iş
> ortağı ana verisi **MOD-0049-FU01 "Business Partner Master Foundation"** — ayrı paket; MOD-0179 iş ortağını **yalnız okur**, r1'in MDM yazma
> satırları (X-1…X-3) o pakete taşındı. **CT kapanışları:** 3PL depo çıkışı **tek model** (F-1, AC-43 / AC-44 yeniden yazıldı); Gateway
> `TenantResolutionMiddleware` için X-19 + gelen uçta `X-Tenant-Id` kuralı + bypass kapalı AC-63 (F-2); bekçi değerleri r2 HEAD'inde yeniden ölçüldü —
> Gateway 87, merkezi denetim 123 / 15, `TypePrefixes` 6, dışlama listesi 7 satır, manifest bloğu `Program.cs:48-65` (F-3); tek stoklanabilir lokasyon
> AC-61 (F-6); dilim kapıları (F-7); sabit süreli imza karşılaştırması S-40d (F-9); sınıf düzeyi `HasPermission` yasağı S-7d ve Gateway ara katman
> sabotajı S-63 (F-12). **Yeni AC:** AC-60 (90 gün), AC-61 (tek lokasyon), AC-62 (v2 notu), AC-63 (Gateway muafiyeti). **Dilim numaraları değişti**
> (r1 → r2 eşlemesi §23 altında). Yazma komutu 26, v1.0 servis ucu 42.
>
> **Kısa özet.** MOD-0179, kendi WMS'i olan dış lojistik firmasının (3PL ortağı) işlettiği depoyu bizim depo akışımıza bağlayan **entegrasyon
> köprüsüdür** (Blueprint: "Not SoR: integration bridge; References SoR: WMS/Inventory"). Sevk talimatı ve giriş ön bildirimi gönderir; 3PL'in onaylarını
> (sevk edildi, teslim alındı) ve günlük stok raporunu alır; bunları **stok defterine kendisi yazmadan**, defterin olağan yazıcıları üzerinden yansıtır
> (çıkış: MOD-0178 dış release → MOD-0183 dispatch; giriş: MOD-0142 GRN). **v1.0 kanalı: API** (imzalı REST); **v1.1:** dosya (SFTP, CSV / XML) ve
> EDI (EDIFACT + X12, SFTP taşıması). Ekranlar: entegrasyon sağlığı ve ortak ayrıntısı, istisna kutusu, mutabakat masası, eşik ayarları. Ücret /
> fatura mutabakatı v2'dir (T-2). 3PL ortağı MDM iş ortağıdır (T-5) ve MOD-0049-FU01'de tanımlanır.

## 0. Kimlik ön kontrolü ve keşif

**DCP-002 ön kontrolü (fail-closed):**

| Komut | Sonuç |
|---|---|
| r2: `PYTHONUTF8=1 python .antigravity/scripts/verify_module_id.py . --check-id MOD-0179 --name "3PL Integration"` | kimlik ve ad değişmedi; r1 sonucu geçerli (çıkış 0, `OK  MOD-0179: proven against Blueprint/registry.`); r1 incelemesi aynı komutu çıkış 0 ile yeniden koştu |
| r1: `PYTHONUTF8=1 python .antigravity/scripts/verify_module_id.py . --discover "3PL integration"` | çıkış 3 ("inceleme gerekli"); `ÖZET bulgular: blueprint 1 · sor 0 · registry 1 · paket 1 · gateway 0 · görev 1`; 611 satır, rapor sha256 `4eac4d87eabad2048d11231e14fa88cb907beecf505d68afa8d538e1fdccc28a`; 572 ref |
| Q-10 (sahip kararı, kararlar 01 `:48-55`): `verify_module_id.py --check-id MOD-0049-FU01 --name "Business Partner Master Foundation" --parent MOD-0049` | OK (karar kaydındaki sonuç); registry satırları `execution/registries/module-id-registry.md:160` (MOD-0049) ve `:161` (MOD-0049-FU01) (r3, F-17 (b)) |

**Keşif bulgularının her birine karar:**

| Bulgu | Karar |
|---|---|
| Blueprint 1 — `MOD-0179 '3PL Integration'` | **Bu paketin kaynağı.** Blueprint 8.8 `Blueprint_Data` satır 180 |
| Registry 1 — `MOD-0179` reserved / planned | **Bu paketin kaynağı.** `execution/registries/module-id-registry.md` (MOD-0179 satırı) |
| Paket 1 — `execution/domains/master-data-management/module-packs/MDM-058-3pl-integration.md` (8 eski uzak dal; blob `726290f17`) | **Mükerrer değil — eski toplu iskelet, yanlış domain, onaysız; devralınmadı.** `id: MDM-058`, `status: draft`, `owner: ai-agent`, gövde yalnız şablon; HEAD'de ve `origin/main`'de yok. Aynı karar MOD-0178 (`MDM-057`), MOD-0180 (`MDM-059`), MOD-0181 (`MDM-060`), MOD-0182 (`MDM-061`) için verildi |
| Görev 1 — `execution/domains/supply-chain-execution/work-packs/mvp5-wms/CT-QUEUE.tsv:11` `WMS-MVP5-MOD0179` | **Bu paketin iş satırı**; çakışma değil |
| Gateway 0 | Karar §15 — mevcut `/api/warehouse/{everything}` ailesi + kiracı ara katmanında adlı muafiyet (X-19) |

**MDM iş ortağı (Q-10 — karar):** MdmService'te iş ortağı varlığı yoktur (r1 ölçümü, §19.2). Sahip kararıyla ana veri **MOD-0049-FU01**'dir (ebeveyn
Blueprint MOD-0049 Master Data Management; domain master-data-management; servis `Diten.MdmService`). MOD-0179 köprüdür ve iş ortağını **yalnız okur**;
r1'in MDM yazma satırları (X-1 kod, X-2 Gateway rotası, X-3 ekran) ve Ali'nin T-5 izni o pakete taşındı. Aynı firmanın tedarikçi (`SUP-`), 3PL ve CRM
müşterisi kayıtlarının bağlanması backlog BL-665.

**Blueprint 8.8 satır 180:** Module ID `MOD-0179` · Capability Group `Warehouse (WMS)` · Wave `W-3` · Dependency Gate `API Gateway; Integration
Monitoring & Reconciliation` · Soft Pages `3PL Integration Health; Reconciliation Workbench; Exception Inbox` · Placement `Integration Bridge (WMS)` ·
Min Integration Contract `3PL-BUNDLE (partner contracts, event mapping, reconciliation exceptions, audit hooks)` · SoR `Not SoR: integration bridge;
References SoR: WMS/Inventory` · Integration Contracts `BRIDGE-BASE` · Build/Buy `Build` · SLO `Tier 1` · AI Tier `Assist`.

**Blueprint sapmaları (adıyla):**
- **W-0179-01 — bağımlılık kapısı MOD-0037.** MOD-0037 ertelenmiş (`execution/domains/platform-shared-services/module-packs/MOD-0037-integration-monitoring.md:13-25`).
  v1 kendi mesaj defterini, istisna kutusunu ve mutabakat masasını 3PL kapsamında tutar; MOD-0037 gelince taşınma FU-0179-MON. CT kabulü Q-15.
- **W-0179-02 — EDI Hub (MOD-0036, W-4).** EDI v1.1'de (F-11) yalnız 3PL mesaj kümesi için bu pakette (DL-11); taşınma FU-0179-EDIHUB. CT kabulü Q-15.

**Diğer kimlik kaynakları:** `execution/domains/supply-chain-execution/domain-config.md:27`; plan
`docs/roadmap/plans/integration-phase-and-mvp5-start-plan-2026-10-21.md:93`, `:114`; WMS-BUNDLE taslağı §14
(`docs/analysis/contracts/drafts/wms-bundle-v1-proposal.md:578-583`).

## 1. Module Summary

MOD-0179 **dış depo entegrasyonunun** sahibidir. Bir depo LOCATION'da `operatingMode = external-integrated` işaretlidir
(`services/Diten.InventoryService/src/Diten.InventoryService.Domain/Rules/LocationHierarchyRules.cs:25-28`) ve **tek bir stoklanabilir lokasyonu** vardır
(Q-9, §8.10.1); o depoyu bir 3PL ortağı işletir, raf / görev yönetimini kendi WMS'i yapar. MOD-0179:
- 3PL ortağını **MOD-0049-FU01 iş ortağından** seçer (ad, adres, iletişim oradan okunur, burada düzenlenmez — T-5) ve depolarına bağlar;
- ortak başına kanalları tanımlar (v1.0: API; v1.1: dosya, EDI); sırlar için yalnız **referans** tutar, ekranda yalnız "ayarlı" (T-4, F-10);
- giden mesajları üretir (sevk talimatı, giriş ön bildirimi), gelen mesajları alır (sevk onayı, teslim onayı, stok raporu), **kanonik** modele çevirir;
- gelen onayı defterin olağan yazıcısına iletir; işlenemeyen her mesajı **istisna kutusuna** düşürür (yeniden işleme, toplu ≤ 200 — M-6);
- günlük stok raporunu defterle karşılaştırır (**mutabakat masası**; fark = bizim − 3PL, yoldaki miktar M-4 kuralıyla düşülür);
- ortak bazında gecikme eşiklerini ayar sayfasında tutar (M-2) ve sağlık / performansı gösterir.

**Temel sınırlar:**
- **Stok yazılmaz.** Çıkışta `GOODS_ISSUE_SALES`'in tek yazıcısı MOD-0183'tür (`execution/domains/supply-chain-execution/module-packs/MOD-0178-putaway-picking-packing.md:178`);
  girişte `GOODS_RECEIPT_PO`'yu MOD-0142 yazar (`execution/domains/procurement/module-packs/MOD-0142-receiving-grn.md:21`).
- **3PL deposunda iç görev üretilmez** (WMS-BUNDLE §14): MOD-0178 o depoda toplama / yerleştirme görevi açmaz (X-10).
- **İş ortağı yazılmaz** (Q-10): MDM tarafı MOD-0049-FU01'dir.
- **Ücret / fatura mutabakatı v2** (T-2); ekranlarda "v2 · planlı" notu (F-8, §8.17).

Hedef kullanıcı: **entegrasyon sorumlusu**, **stok / mutabakat sorumlusu**, **depo yöneticisi**. Kiracı modülüdür (`_LayoutTenantShell`, 7 dil: en, tr,
fr, es, zh, ar, ru — M-7).

## 2. Ownership and Boundaries

### 2.1 Kapsam (v1.0 ve v1.1)
**v1.0 (DL-1…DL-9):**
- 3PL ortağı bağlantısı (`ThirdPartyPartner`): MOD-0049-FU01 iş ortağına başvuru, bağlı depolar (her biri `external-integrated` + tek stoklanabilir
  lokasyon), bizdeki sorumlu, sözleşme notu, askıya alma / etkinleştirme (gerekçeli, silme yok), ortak bazında eşikler (M-2).
- Kanallar (`ThirdPartyChannel`): **API** (giden REST + imzalı gelen uç); kanal açma / kapatma gerekçeli (M-16); sır referansları (F-10). Dosya ve EDI
  kanal türleri modelde vardır ama v1.0'da oluşturulamaz (422 `TPL_CHANNEL_TYPE_NOT_ENABLED`).
- Eşlemeler (`ThirdPartyMapping`), mesaj defteri (`ThirdPartyMessage` + maskeli ham içerik, 90 gün kişisel veri kuralı — F-5), talimatlar.
- İstisna kutusu: hata türleri, yeniden işleme (tek, toplu ≤ 200, aynı ortak + aynı tür), yok sayma (gerekçe), sorumlu atama; kapalı kanala gelen mesaj
  → "kanal kapalı" istisnası (M-16); lot / SKT farkı istisnası (T-3).
- Bekletilen mesajlar (askıdaki ortak; 30 gün; elle işlenir; Q-7: askıdayken işlenmez).
- Çıkış akışı (MOD-0178 dış release / MOD-0183 dispatch seam'i), giriş akışı (MOD-0142 seam'i).
- Stok raporu, mutabakat masası, performans sekmesi.
- 3PL-BUNDLE sözleşmesi (taslak DL-1'in ilk adımı; dondurma DL-5'ten önce ayrı CT adımı).

**v1.1 (DL-10, DL-11 — F-11):** dosya kanalı (SFTP, CSV / XML) ve EDI kanalı (EDIFACT + X12, SFTP taşıması — Q-2). T-1'in EDI kapsamı v1.1'de karşılanır.

### 2.2 Kapsam dışı
| Şey | Sahibi | 0179 nasıl dokunur |
|---|---|---|
| Ücret / fatura mutabakatı, itiraz, finans kaydı (mockup M-10…M-14, K-10, K-11) | v2 (T-2); Treasury'ye bilgi notu | yok — yalnız "v2 · planlı" notu (F-8) ve genişleme noktası §8.17 |
| İş ortağı ana verisi (ad, adres, iletişim, vergi no, rol) | **MOD-0049-FU01** (MDM; Q-10) | yalnız okur (`IBusinessPartnerDirectory`); yazmaz |
| Stok bakiyesi, hareket, rezervasyon; lokasyon yaratma | MOD-0173 / FU02 / FU03 | yalnız **okur** (`Find`); tek stoklanabilir lokasyonu kullanıcı LOCATION ekranında yaratır |
| Outbound, release, görevler, sync-dispatch | MOD-0178 | X-10 seam'i (§8.8) |
| Sevkiyat, dispatch, `GOODS_ISSUE_SALES` | MOD-0183 (MVP-6) | X-12 seam'i (Q-18) |
| Mal kabul (GRN), `GOODS_RECEIPT_PO` | MOD-0142 (procurement) | X-13 seam'i (Q-19) |
| Satın alma siparişi | MOD-0141 | yalnız okur |
| Kiracı sır kasası (çalışma anında yazılabilen) | Platform (merkez / Beste — T-4); MOD-0012 FU'su — sonraki adım (F-10) | v1.0'da yok; referans ara çözümü §8.5 |
| Genel entegrasyon izleme, genel EDI Hub | MOD-0037, MOD-0036 | yok (W-0179-01 / 02) |
| AS2 taşıması, CONTRL / 997 | — | yok (Q-2, Q-3; FU-0179-AS2, FU-0179-ACK) |
| "Düzeltme öner" / onaylı stok düzeltmesi | — | v1'de düğme yok (Q-4; FU-0179-ADJUST) |
| İade, depolar arası transfer, X12 947 | MOD-0186 / MOD-0178 / FU | yok |
| AI Assist | Blueprint AI kapıları | yok; FU-0179-AI |

### 2.3 Sınır kuralları
1. **İkinci bakiye yok.** 0179 varlıklarında `OnHand` / `Available` / `Reserved` alanı yoktur; mutabakattaki "bizim stok" rapor anı için defterden okunur.
2. **Başka modülün koleksiyonuna yazılmaz.** 0179 Persistence'ta başka modül koleksiyonu yalnız `Find` ile okunur. Seam'ler (X-10) MOD-0178'in **kendi
   işleminde** 0179 portunu çağırır (MOD-0182 `ScanSessionBinding` deseni).
3. **Ortak verisi kopyalanmaz.** `ThirdPartyPartner` iş ortağının adını, adresini, iletişimini taşımaz; yalnız kimliğini ve değişmez kodunu taşır.
4. **Sır değeri hiçbir yerde yoktur.** v1.0'da sır değeri MOD-0179'a hiç gelmez (yalnız referans); değer yalnız gönderici işçide ve gelen uç imza süzgecinde,
   çözüldüğü anda bellekte kullanılır; cevapta, logda, OTel'de, merkezi kayıtta, idempotency deposunda ve koleksiyonda bulunmaz (F-9 (c)).
5. Her okuma ve yazma kiracı + şirket kapsamındadır; kiracılar / şirketler arası erişim **404**. Gelen API ucunda kiracı ve şirket **yalnız kanal
   anahtarından** çözülür; istekteki `X-Tenant-Id` kiracıyı belirlemez — gelirse ve anahtarın kiracısından farklıysa 401, hiçbir yazım yok (F-2, §8.4).
6. **Silme yok.** Ortak askıya alınır, kanal kapatılır, eşleme pasifleştirilir, istisna yok sayılır. Ham içeriğin 90 gün sonra kişisel verisiz yeniden
   yazılması (F-5) silme değildir: sha256 ve kanonik özet kalır.

## 3. Owned Objects

| Nesne | Dilim | Koleksiyon (öneri) | Not |
|---|---|---|---|
| `ThirdPartyPartner` | DL-1 | `wms_tpl_partners` | Değişebilir, `Version` (If-Match) |
| `ThirdPartyChannel` | DL-1 | `wms_tpl_channels` | Değişebilir, `Version`; sır değeri yok, yalnız referans (§4.2) |
| `ThirdPartyMapping` | DL-1 | `wms_tpl_mappings` | Değişebilir, `Version`; pasifleştirilir |
| `ThirdPartyMessage` | DL-2 | `wms_tpl_messages` | Zarf; durum alanı değişir (`Version`) |
| `ThirdPartyMessagePayload` | DL-2 | `wms_tpl_message_payloads` | Ham içerik (≤ 5 MB); yalnız bir kez, 90. günde kişisel verisiz yeniden yazılır (F-5) |
| `ThirdPartyInstruction` | DL-2 (model), DL-6 / DL-7 (yazım) | `wms_tpl_instructions` | Değişebilir, `Version` |
| `ThirdPartyException` | DL-2 | `wms_tpl_exceptions` | Değişebilir, `Version`; görünen kod `TPX` (§8.13) |
| `ThirdPartyStockReport` | DL-8 | `wms_tpl_stock_reports` | Insert-only |
| `ThirdPartyReconciliationLine` | DL-8 | `wms_tpl_reconciliation_lines` | Değişebilir, `Version` |
| `ThirdPartyCommandRequest` | DL-1 | `wms_tpl_command_requests` | Komut idempotency deposu; insert-only, komutun işleminde |
| Koleksiyon sabitleri | DL-1 | `ThirdPartyLogisticsCollections` (0179 Persistence) | `WarehouseCollections` ile paylaşılmaz |

**Komutlar (yazma, 26 — DL-1 12, DL-2 7, DL-5 3, DL-7 1, DL-8 3):**
- DL-1: `CreateThirdPartyPartnerCommand`, `UpdateThirdPartyPartnerCommand`, `SuspendThirdPartyPartnerCommand`, `ActivateThirdPartyPartnerCommand`,
  `UpdateThirdPartyThresholdsCommand`, `CreateThirdPartyChannelCommand`, `UpdateThirdPartyChannelCommand`, `CloseThirdPartyChannelCommand`,
  `OpenThirdPartyChannelCommand`, `CreateThirdPartyMappingCommand`, `UpdateThirdPartyMappingCommand`, `DeactivateThirdPartyMappingCommand`.
- DL-2: `ReceiveThirdPartyMessageCommand` (kanal bağdaştırıcılarının ortak girişi), `ReprocessThirdPartyExceptionsCommand` (1..200; "eşleme ekle ve yeniden
  işle" dahil), `IgnoreThirdPartyExceptionCommand`, `AssignThirdPartyExceptionCommand`, `ProcessHeldThirdPartyMessagesCommand`,
  `DiscardHeldThirdPartyMessageCommand`, `PurgeThirdPartyMessagePiiCommand` (F-5; zamanlanmış, sistem aktörü).
- DL-5: `SetThirdPartyChannelSecretRefCommand` (F-10: değer değil referans), `TestThirdPartyChannelCommand`, `SendThirdPartyMessageCommand` (gönderici işçi).
- DL-7: `SendInboundAdviceCommand`.
- DL-8: `AddReconciliationNoteCommand`, `MarkReconciliationInvestigatingCommand`, `ResolveReconciliationLineCommand`.
- DL-6: sevk talimatının yazımı ayrı komut **değildir** — MOD-0178'in dış release işleminde port çağrısıdır (X-10, §8.8); sevk onayının işlenmesi
  `ReceiveThirdPartyMessageCommand` / `ReprocessThirdPartyExceptionsCommand` içindedir.
- DL-10 / DL-11 (v1.1) yeni komut eklemez (bağdaştırıcılar `ReceiveThirdPartyMessageCommand` / `SendThirdPartyMessageCommand`'ı kullanır).

**Sorgular:** `GetThirdPartyDashboardQuery` (DL-2), `GetThirdPartyPartnerListQuery`, `GetThirdPartyPartnerByIdQuery`, `GetThirdPartyPerformanceQuery` (DL-8),
`GetThirdPartyChannelListQuery`, `GetThirdPartyChannelByIdQuery`, `GetThirdPartyMappingListQuery`, `GetThirdPartyMessageListQuery`,
`GetThirdPartyMessageByIdQuery`, `GetThirdPartyMessageRawQuery`, `GetThirdPartyExceptionListQuery`, `GetThirdPartyExceptionByIdQuery`,
`GetThirdPartyInstructionListQuery` (DL-6), `GetThirdPartyInstructionByIdQuery` (DL-6), `GetThirdPartyStockReportListQuery` (DL-8),
`GetReconciliationLineListQuery` (DL-8), `GetReconciliationLineByIdQuery` (DL-8), `ExportReconciliationQuery` (DL-8, CSV).

**API (taban `/api/warehouse`, aile `/api/warehouse/third-party-logistics/…` — §15). PUT / DELETE yok:**
DL-1 (17): `GET/POST partners` · `GET partners/{partnerId}` · `POST partners/{partnerId}/update` · `…/suspend` · `…/activate` · `…/thresholds` ·
`GET/POST channels` · `GET channels/{channelId}` · `POST channels/{channelId}/update` · `…/close` · `…/open` · `GET/POST mappings` ·
`POST mappings/{mappingId}/update` · `…/deactivate`.
DL-2 (11): `GET dashboard` · `GET messages` · `GET messages/{messageId}` · `GET messages/{messageId}/raw` · `POST messages/held/process` ·
`POST messages/{messageId}/discard` · `GET exceptions` · `GET exceptions/{exceptionId}` · `POST exceptions/reprocess` · `POST exceptions/{exceptionId}/ignore` ·
`POST exceptions/{exceptionId}/assign`.
DL-5 (3): `POST channels/{channelId}/secret-refs/{secretName}` · `POST channels/{channelId}/test` · `POST inbound/{keyId}/messages` (3PL'in sistemi —
JWT yok, imza; §8.4).
DL-6 (2): `GET instructions` · `GET instructions/{instructionId}`.
DL-7 (1): `POST inbound-advices`.
DL-8 (8): `GET stock-reports` · `GET reconciliation` · `GET reconciliation/{lineId}` · `POST reconciliation/{lineId}/notes` · `…/investigate` · `…/resolve` ·
`GET reconciliation/export` · `GET partners/{partnerId}/performance`.
**Toplam v1.0 servis ucu: 42.** DL-3, DL-4, DL-9, DL-10, DL-11 InventoryService ucu eklemez (Q-2 = SFTP: AS2 ucu yok — F-4). Sabit kesimler (`dashboard`,
`held`, `export`) parametreli yoldan önce eşleşir; `{…Id}` `:guid` kısıtlı; `{keyId}` 26 karakter.

**Controller tabanı:** `ThirdPartyPartnersController`, `ThirdPartyChannelsController`, `ThirdPartyMappingsController`, `ThirdPartyMessagesController`,
`ThirdPartyExceptionsController`, `ThirdPartyInboundController`, `ThirdPartyInstructionsController`, `ThirdPartyReconciliationController` — hepsi
`[Route("api/warehouse")]`, eylem şablonu tam yol parçası; `INV/Api/Controllers/ThirdPartyLogistics/`, namespace
`Diten.InventoryService.Api.Controllers.ThirdPartyLogistics`. **`[HasPermission]` yalnız eylem düzeyinde** — sınıf düzeyinde yasak (F-12, AC-7).
Ayrı namespace'in bedeli MOD-0173 manifest bütünlük testinde dışlamadır (X-9).

**Frontend rotaları (Q-5 — karar):** `/Warehouse/ThirdPartyLogistics` (sağlık + ortak listesi; ortak ayrıntısı `Details`), `/Warehouse/ThirdPartyChannels`,
`/Warehouse/ThirdPartyMappings`, `/Warehouse/ThirdPartyMessages`, `/Warehouse/ThirdPartyExceptions`, `/Warehouse/ThirdPartyReconciliation`,
`/Warehouse/ThirdPartySettings`. Menü grubu "Entegrasyonlar · 3PL".

**İzinler:** §14.

## 4. Entity Fields

Tüm 0179 varlıkları InventoryService `EntityBase`'inden türer: `Id`, `TenantId`, `LegalEntityId`, `CreatedAt`, `UpdatedAt`, `Version` (int), `IsDeleted`,
`DeletedAt`. `TenantId` / `LegalEntityId` sunucuda çözülür; gövde ya da sorguda gelirse 400. Zamanlar UTC. **Ekranda kimlik (GUID) gösterilmez.**

### 4.1 ThirdPartyPartner
| Alan | Tip | Kural |
|---|---|---|
| `BusinessPartnerId` | uuid | MOD-0049-FU01 iş ortağı; etkin ve rolünde `ThirdPartyLogistics` (FU01 sözleşmesi); şirkette tekil |
| `PartnerCode` | string | iş ortağının değişmez kodu; bağlantıda bir kez kopyalanır |
| `WarehouseIds` | uuid[] 1..20 | her biri şirketin LOCATION `Warehouse` düğümü, `OperatingMode = external-integrated`, **tam bir etkin stoklanabilir alt lokasyonu** var (Q-9, §8.10.1); bir depo aynı anda yalnız bir **etkin** ortağa bağlı |
| `ResponsibleUserId`, `ResponsibleUserName` | uuid / string | bizdeki sorumlu |
| `ContractNote` | string \| null | ≤ 2000 |
| `Status` | enum | `Active` \| `Suspended` (§8.2) |
| `StatusReason`, `StatusChangedAt`, `StatusChangedBy` | string / UTC / uuid | gerekçe 10..500 (mockup K-3) |
| `Thresholds` | nesne | §4.1.1 (M-2) |
| `CreatedBy`, `CreatedByName` | uuid / string | |

**Ad, adres, iletişim, vergi no alanı YOKTUR.** Görüntüde FU01'den okunur (≤ 60 sn önbellek); erişilemezse kod + "Ana veriye ulaşılamadı" rozeti.

#### 4.1.1 Eşikler (`Thresholds`, M-2; varsayılanlar Q-6 — karar)
| Alan | Tip | Varsayılan | Kural |
|---|---|---|---|
| `ShipmentConfirmMaxMinutes` | int | 120 | 5..2880 |
| `ReceiptConfirmMaxMinutes` | int | 240 | 5..10080 |
| `StockReportMaxAgeHours` | int | 26 | 1..168 |
| `StockReportExpectedLocalTime` | "HH:mm" \| null | null | bilgi; depo saat diliminde |
| `ErrorRateTargetPercent` | decimal | 1.0 | 0..100, bir ondalık |

### 4.2 ThirdPartyChannel
| Alan | Tip | Kural |
|---|---|---|
| `PartnerId` | uuid | `ThirdPartyPartner` |
| `Type` | enum | `Api` (v1.0) \| `File` (v1.1, DL-10) \| `Edi` (v1.1, DL-11); v1.0'da `File` / `Edi` yaratma 422 `TPL_CHANNEL_TYPE_NOT_ENABLED` |
| `Name` | string | trim, 2..80; ortakta tekil |
| `WarehouseIds` | uuid[] 1..20 | ortağın depolarının alt kümesi |
| `Status` | enum | `Open` \| `Closed`; açma / kapatma gerekçe 10..500 (M-16) |
| `StatusReason`, `StatusChangedAt`, `StatusChangedBy` | | |
| `Api` | nesne \| null | `Type = Api` iken zorunlu: `OutboundBaseUrl` (yalnız `https://`), `AuthType` (`ApiKey` \| `OAuthClientCredentials`), `InboundKeyId` (sunucu üretir, 26 karakter, değişmez), `TimeoutSeconds` 5..120 |
| `File` | nesne \| null | v1.1: `Host`, `Port`, `Username`, `HostKeyFingerprint` (zorunlu), `InboundFolder`, `OutboundFolder`, `ArchiveFolder`, `FilePattern`, `Format` (`Csv` \| `Xml`), `PollMinutes` |
| `Edi` | nesne \| null | v1.1: `Standard` (`Edifact` \| `X12`), `SyntaxVersion` (sabit liste), `OurId` + `OurQualifier`, `PartnerId` + `PartnerQualifier`, `OutboundMessages[]`, `InboundMessages[]` (§8.3); **taşıma yalnız SFTP** (Q-2) — `File` alanlarıyla aynı bağlantı ayarları |
| `SecretRefs[]` | `{ Name, Ref, RefSetAt, RefSetBy }` | `Name` türe göre: `ApiKey`, `OAuthClientSecret`, `InboundSigningSecret` (API); `SftpPassword` \| `SftpPrivateKey` (v1.1). `Ref` **sunucunun ürettiği** yapılandırma anahtarı adıdır: `ThirdPartyLogistics:{TenantId}:{ChannelId}:{Name}` (r3, F-13; §8.5); istemci vermez; **değer yoktur** |
| `LastSuccessAt`, `LastErrorAt`, `LastErrorCode`, `LastTestAt`, `LastTestResult` | UTC / string | teknik sinyal alanları; `Version`'ı artırmaz |

### 4.3 ThirdPartyMapping
`PartnerId`, `Kind` (`Product` \| `Warehouse` \| `StockStatus`), `OurRef`, `OurDisplay`, `PartnerValue` (1..64), `Status` (`Active` \| `Inactive`), `Version`.
Tekillik: etkin eşlemelerde (ortak, tür, `PartnerValue`) ve (ortak, tür, `OurRef`).

### 4.4 ThirdPartyMessage ve ThirdPartyMessagePayload
| Alan | Tip | Kural |
|---|---|---|
| `Direction` | enum | `In` \| `Out` |
| `Kind` | enum | `ShipmentInstruction` (Out), `InboundAdvice` (Out), `ShipmentConfirmation` (In), `ReceiptConfirmation` (In), `InventoryReport` (In), `Unsupported` (In — tanınan ama işlenmeyen, ör. fatura; §8.17) |
| `ChannelId`, `PartnerId` | uuid | |
| `Format` | enum | v1.0 `ApiJson`; v1.1 `FileCsv`, `FileXml`, `EdifactInsdes`, `EdifactDesadv`, `EdifactRecadv`, `EdifactInvrpt`, `X12_940`, `X12_943`, `X12_944`, `X12_945`, `X12_846` (Q-3 — karar) |
| `ExternalMessageId` | string ≤ 100 | API `X-TPL-Message-Id` / dosya adı / EDI kontrol no |
| `DedupKey` | string | (ortak, kanal türü, `ExternalMessageId`, içerik sha256) |
| `BusinessDocRef` | `{ Kind, Id, Number }` \| null | `Outbound`, `PurchaseOrder`, `Grn`, `StockReport` |
| `Status` | enum | In: `Received` → `Processed` \| `Failed` \| `Duplicate` \| `Held` \| `Rejected` \| `Discarded`; Out: `Pending` → `Sent` \| `Failed` \| `NotSent` |
| `HeldUntil` | UTC \| null | `Held` iken `ReceivedAt + 30 gün`; geçince salt okunur arşiv (türetilmiş) |
| `Attempts`, `ReceivedAt`, `SentAt`, `ProcessedAt`, `LastErrorCode` | | |
| `PayloadId`, `PayloadSha256`, `PayloadBytes` | | ham içerik ayrı; ham görüntüleme ayrı izin |
| `Canonical` | nesne | kanonik özet (satır sayısı, toplam miktar, belge no) — kişisel veri taşımaz |

`ThirdPartyMessagePayload`: `MessageId`, `ContentType`, `Bytes` (≤ 5 MB), `OriginalSha256` (alındığı andaki), `PiiPurgedAt` (UTC \| null), `CurrentSha256`.
**90 gün kuralı (F-5 — sahip kararı):** `ReceivedAt`'ten 90 gün sonra zamanlanmış `PurgeThirdPartyMessagePiiCommand` içeriği **kişisel verisiz** yeniden yazar
(§8.7 maskeleme alanları kalıcı olarak `•` ile değiştirilir; alan yapısı ve iş alanları — ürün, lot, miktar, belge no — kalır), `PiiPurgedAt` ve `CurrentSha256`
yazılır; `OriginalSha256`, mesaj zarfı ve kanonik özet değişmez. Yeniden yazım bir kez yapılır, geri alınmaz; 90 günden eski bir mesajın yeniden işlenmesi
temizlenmiş içerikle yapılır (iş alanları kaldığı için mümkündür). Kişisel veri notu: ham içerikte alıcı adı, telefon, e-posta, adres bulunabilir;
sahibi Beste (karar), saklama sorumlusu MOD-0179. MDM iş ortağı `Contacts` için aynı kural MOD-0049-FU01'de ayrıca değerlendirilir (kararlar 01).

### 4.5 ThirdPartyInstruction
`Kind` (`Shipment` \| `InboundAdvice`), `PartnerId`, `ChannelId`, `WarehouseId`, `SourceDoc` (`{ Kind: Outbound \| PurchaseOrder, Id, Number }`),
`Lines[]` (`{ lineRef, skuId, lotId?, lotNumber?, expiry?, quantity, uom }`), `Status` (`Pending` \| `Sent` \| `PartiallyConfirmed` \| `Confirmed` \| `Closed`
\| `Cancelled`), `MessageIds[]`, `ConfirmedLines[]`, `SentAt`, `ConfirmedAt`.

### 4.6 ThirdPartyException
| Alan | Tip | Kural |
|---|---|---|
| `Number` | string | `{ŞİRKET}-TPX-{000001}` (§8.13); bir kez yazılır |
| `PartnerId`, `ChannelId`, `MessageId` | uuid | |
| `Type` | enum | `Format`, `UnknownProductMapping`, `UnknownLot`, `LotExpiryMismatch` (T-3), `QuantityMismatch`, `LateConfirmation`, `Duplicate`, `ConnectionAuth`, `ChannelClosed` (M-16), `Unsupported` |
| `DetailCode`, `DetailArgs` | string / sözlük | 7 dilde metne çevrilen kod + parametreler (kişisel veri yok) |
| `Status` | enum | `Open` \| `Retrying` \| `Resolved` \| `Ignored` |
| `FirstSeenAt`, `LastAttemptAt`, `Attempts` | | |
| `AssigneeUserId`, `AssigneeName` | uuid \| null / string | |
| `IgnoredReason` / `ResolvedBy` (`Reprocess` \| `Mapping` \| `Auto`) | | yok saymada gerekçe 10..500 |

### 4.7 ThirdPartyStockReport ve ThirdPartyReconciliationLine
`ThirdPartyStockReport` (insert-only): `PartnerId`, `WarehouseId`, `ReportedAt`, `ReceivedAt`, `MessageId`, `Lines[]`.
`ThirdPartyReconciliationLine`: `ReportId`, `PartnerId`, `WarehouseId`, `SkuId`, `LotId?`, `LotNumber`, `ExpiryOurs?`, `ExpiryTheirs?`, `OurQuantity`, `TheirQuantity`,
`Difference` (= bizim − 3PL), `InTransit` (işaretli, M-4), `Explained`, `Unexplained`, `Status` (`Matched` \| `Difference` \| `Investigating` \| `Resolved`),
`Notes[]`, `ResolvedReason?`, `ResolvedBy?`, `ResolvedAt?`, `Version`.

### 4.8 ThirdPartyCommandRequest
`RequestOp`, `ActorUserId` (gelen API'de kanal kimliği), `IdempotencyKey`, `RequestFingerprint`, `TargetId`, `ResponseStatus`, `ResponseBody`, `CreatedAt`.
Komutun yazımıyla aynı işlemde. `ReceiveThirdPartyMessageCommand` anahtarını `ThirdPartyMessage.DedupKey`'de tutar. v1.0'da sır değeri istekte hiç
bulunmadığı için parmak izi bütün gövdeyi (sır referansı dahil) kapsar — r1'in "farklı sırla aynı anahtar sessizce ilk cevabı döner" açığı (F-9 (a)) bu
yüzden kapanır; kasa FU'su değer taşıyan yazmayı getirirse değerin anahtarlı özeti (HMAC, sunucu anahtarı) parmak izine katılır.

### 4.9 Indexes
| Koleksiyon | Index | Amaç |
|---|---|---|
| `wms_tpl_partners` | `(TenantId, LegalEntityId, BusinessPartnerId)` tekil | |
| `wms_tpl_partners` | `(TenantId, LegalEntityId, WarehouseIds)` çok anahtarlı, kısmi (`Status = Active`) + uygulama denetimi | bir depo bir etkin ortak |
| `wms_tpl_channels` | `(TenantId, LegalEntityId, PartnerId, Name)` tekil; `Api.InboundKeyId` **küresel** tekil kısmi | gelen ucun anahtar araması |
| `wms_tpl_mappings` | `(TenantId, LegalEntityId, PartnerId, Kind, PartnerValue)` ve `(…, Kind, OurRef)` tekil kısmi | |
| `wms_tpl_messages` | `(TenantId, LegalEntityId, DedupKey)` tekil; `(…, PartnerId, ReceivedAt desc)`; `(…, Status, HeldUntil)` kısmi | |
| `wms_tpl_message_payloads` | `(TenantId, LegalEntityId, PiiPurgedAt, CreatedAt)` kısmi (`PiiPurgedAt` null) | 90 gün süpürücüsü |
| `wms_tpl_exceptions` | `(TenantId, LegalEntityId, Status, Type, PartnerId)`; `(…, MessageId)` | |
| `wms_tpl_instructions` | `(TenantId, LegalEntityId, SourceDoc.Kind, SourceDoc.Id)` tekil kısmi (`Status ≠ Cancelled`); `(…, PartnerId, WarehouseId, Status, SentAt)` | |
| `wms_tpl_reconciliation_lines` | `(TenantId, LegalEntityId, WarehouseId, ReportId, SkuId, LotNumber)` tekil; `(…, Status)` | |
| `wms_tpl_command_requests` | `(TenantId, LegalEntityId, ActorUserId, RequestOp, IdempotencyKey)` tekil | |

### 4.10 İş ortağı okuması (MOD-0049-FU01 — Q-10)
MOD-0179 iş ortağını **yalnız okur**; varlığın sahibi ve alanları MOD-0049-FU01 paketindedir (ayrı paket — kararlar 01 `:48-55`: taraf kaydı ve roller,
v1 rolü `ThirdPartyLogistics`; eşleştirme / birleştirme, tedarikçi ve müşteri SoR'u kapsam dışı). Okuma portu `IBusinessPartnerDirectory` (0179
Application): `ListAsync(role: ThirdPartyLogistics, status: Active)` ve `GetAsync(id)` → `{ id, code, displayName, status, roles, contactSummary }`. Kesin
uç ve alan adları FU01 paketinin sözleşmesinden gelir (DL-4 başında); DL-1…DL-3 testleri sahte uygulamayla çalışır. MDM'ye hiçbir yazım yoktur.
**İş ortağı pasifleşir ya da rolü kalkarsa:** mevcut bağlantı korunur, sağlıkta "Ana veride pasif" rozeti, giden mesaj `NotSent` + istisna (`ConnectionAuth`
değil `Unsupported` değil — yeni tür gerekmez: `Format` dışı ayrı kod `TPL_PARTNER_MASTER_INACTIVE`, §8.15); yeni bağlantı 422 `TPL_PARTNER_NOT_3PL`.

## 5. Repo Scope

`INV` = `services/Diten.InventoryService/src/Diten.InventoryService.`, `INVT` = `services/Diten.InventoryService/tests/Diten.InventoryService.Tests/`.

| Yol | Ne | Dilim |
|---|---|---|
| `docs/analysis/contracts/drafts/3pl-bundle-v1-proposal.md` (yazılacak) | 3PL-BUNDLE taslağı (**DL-1'in ilk adımı**; dondurma DL-5'ten önce ayrı CT adımı) | DL-1 |
| `INV/Domain/ThirdPartyLogistics/{Entities,Enums,Rules,Repositories}/` | varlıklar; durum makineleri, sağlık, yoldaki / fark kuralları, maskeleme ve 90 gün temizleme kuralları (saf) | DL-1…DL-8, DL-10, DL-11 |
| `INV/Application/Features/ThirdPartyLogistics/` | ortak: `ThirdPartyContract.cs`, `ThirdPartyErrorCodes.cs`, `ThirdPartyCentralAudit.cs`, `Ports/` (`IBusinessPartnerDirectory`, `IThirdPartySecretResolver`, `IExternalFulfilmentGate`, `IShipmentDispatchClient`, `IGoodsReceiptClient`, `IPurchaseOrderReader`), `Services/` (kanonik işlemci, API bağdaştırıcısı; v1.1: SFTP, CSV, XML, EDIFACT, X12), `Resources/ThirdPartyText.{en,tr,fr,es,zh,ar,ru}.resx` | DL-1…DL-8, DL-10, DL-11 |
| `INV/Application/Features/ThirdPartyPartners/`, `ThirdPartyChannels/`, `ThirdPartyMappings/`, `ThirdPartyMessages/`, `ThirdPartyExceptions/`, `ThirdPartyInstructions/`, `ThirdPartyReconciliation/` | Golden Slim yapısı | dilimine göre |
| `INV/Persistence/ThirdPartyLogistics/` | depolar, `ThirdPartyLogisticsCollections`, index'ler, başka modül okumaları (**yalnız `Find`**) | DL-1…DL-8 |
| `INV/Api/Controllers/ThirdPartyLogistics/` | §3'teki controller'lar | DL-1, DL-2, DL-5…DL-8 |
| `INV/Api/ThirdPartyLogistics/` | `ThirdPartyLogisticsManifestProvider`, `ThirdPartyLogisticsServiceCollectionExtensions`, imza süzgeci, işçiler (gönderici, 90 gün süpürücüsü; v1.1 SFTP okuyucu) | DL-1, DL-2, DL-5, DL-10 |
| `INV/Infrastructure/Authorization/ThirdPartyLogisticsPermissions.cs` | izin sabitleri | DL-1 |
| `INV/Infrastructure/ThirdPartyLogistics/` | `IBusinessPartnerDirectory` HTTP istemcisi (FU01), `IShipmentDispatchClient` (MOD-0183), `IGoodsReceiptClient` (MOD-0142), `IPurchaseOrderReader` (MOD-0141) | DL-4, DL-6, DL-7 |
| `INVT/ThirdPartyLogistics/` | 0179'un test ağacı (DL-6'nın 0178 davranış testleri **burada**) | her dilim |
| `frontend/Diten.Web/Views/Warehouse/{ThirdPartyLogistics,ThirdPartyChannels,ThirdPartyMappings,ThirdPartyMessages,ThirdPartyExceptions,ThirdPartyReconciliation,ThirdPartySettings}/`, `frontend/Diten.Web/Controllers/`, `frontend/Diten.Web/wwwroot/assets/js/Warehouse/…`, `frontend/Diten.Web/Resources/Views/Warehouse/…` | ekranlar, proxy controller'lar, JS, resx (7 dil) | DL-3, DL-8 |
| `frontend/Diten.Web/Resources/SharedResource.{7}.resx`, `PageHelpResource.{7}.resx`, `frontend/Diten.Web/Services/Help/PageHelpCatalog.cs` | ortak Web kaynakları — **yalnız eklemeli** | DL-3, DL-8 |
| `frontend/Diten.Web.Tests/Warehouse/ThirdPartyL10nAndHelpTests.cs` (yeni) | 0179 L10n / Layout / yardım testi | DL-3 |
| `gateway/Diten.ApiGateway.Tests/Mod0179ThirdPartyRouteTests.cs` (yeni, integration-agent) | uç kapsama + kiracı ara katmanı muafiyeti (bypass kapalı) | DL-5 (AC-63), DL-9 |

### 5.1 XMC-001 — başka modülün dosyalarına dokunuşlar
Her satır dokunmadan önce sahibine **tek tek** sorulur; yalnız eklemeli. **Ölçüm HEAD'i `70cb6ca01` (r2, F-3).** r1'in "atıflar etkilenmedi" cümlesi
yanlıştı: `0e8e29aab..241659850` aralığında MOD-0180 DL-1 kodu girdi ve bekçi değerleri değişti (aşağıdaki "Bugün" sütunu r2 ölçümüdür). Sayılar
**sabit taban değildir**: uç / komut / çağrı yeri ekleyen her dilim satırı **dilim başında ölçülen** değerden günceller (MOD-0180 / 0182 aynı satırları
artırır; paralel dallarda sonra birleşen dal yeniden ölçer). X-17 ve X-18 **paket onayından önce** `.antigravity` kapısında kapanır; diğer satırlar dilim
başlamadan sahibinden onay ister (paket onayını bekletmez).

| # | Dosya | Bugün (r2 ölçümü) | Gereken değişiklik | Sahip | Dilim |
|---|---|---|---|---|---|
| X-1…X-3 | (r1: MdmService kodu, MDM Gateway rotası, MDM ekranı) | — | **Taşındı → MOD-0049-FU01 paketi** (Q-10); T-5 izni o pakete geçer. MOD-0179 MDM'ye dokunmaz | MDM (Ali) — FU01 | — |
| X-4 | `services/Diten.InventoryService/src/Diten.InventoryService.Api/Program.cs` manifest bloğu (`:48-65`) | son satır MOD-0180 `:65` | `ThirdPartyLogisticsManifestProvider` + `AddThirdPartyLogistics()` | MOD-0173 (merkez) | DL-1 |
| X-5 | `services/Diten.InventoryService/tests/Diten.InventoryService.Tests/Architecture/SingleSystemOfRecordArchitectureTests.cs` | son bildirim MOD-0180 `WaveCollections.All` `:120` | `ThirdPartyLogisticsCollections.All` bildirimi | MOD-0173 (merkez) | DL-1 |
| X-6 | `services/Diten.InventoryService/tests/Diten.InventoryService.Tests/CentralAudit/CentralAuditDeclarationTests.cs` | `:55` `Assert.Equal(123, WriteCommands.Length)`; `:134` `Assert.Equal(15, seen)` | (a) komut sayısı: dilim başı + **DL-1 +12, DL-2 +7, DL-5 +3, DL-7 +1, DL-8 +3** (toplam 26), yorumda adlarıyla; modül listesine `ThirdPartyLogisticsManifestProvider`; (b) işlem çağrı yeri: öneri tek yardımcı `ThirdPartyTransactions.InTransactionAsync` (beklenen DL-1 +1, sonra 0) — aynı commit | MOD-0173 (merkez) | DL-1, DL-2, DL-5, DL-7, DL-8 |
| X-7 | `services/Diten.InventoryService/src/Diten.InventoryService.Domain/Warehouse/Rules/WarehouseRecordCodeRules.cs:31` `TypePrefixes` | altı önek (PUT, PCK, OUT, PKG, CNT, WAV — `Wave` `:29`) | `TPX` sabiti ve listeye ekleme | MOD-0178 (merkez) | DL-2 |
| X-8 | `gateway/Diten.ApiGateway.Tests/Mod0178WarehouseRouteTests.cs:103` — yalnız `Assert.Equal(<n>, endpoints.Count)` ve üstündeki yorum | `Assert.Equal(87, endpoints.Count)` | uç ekleyen her dilim: dilim başı + **DL-1 17, DL-2 11, DL-5 3, DL-6 2, DL-7 1, DL-8 8**; aynı commit; kabul kaydı önce → sonra | integration-agent | DL-1, DL-2, DL-5…DL-8 |
| X-9 | `services/Diten.InventoryService/tests/Diten.InventoryService.Tests/Api/InventoryLedgerManifestCompletenessTests.cs:213-225` dışlama listesi | yedi satır (Traceability, Quarantine, Expiry, Recall, Warehouse, CycleCounting, WavePlanning) | `.Where(t => t.Namespace?.EndsWith(".Controllers.ThirdPartyLogistics", StringComparison.Ordinal) != true)` + yorum; karşılığı 0179'un kendi bütünlük vakası (AC-7); MOD-0182 `Controllers.Barcode` önce gelirse satır kayar — dilim başında ölçülür | MOD-0173 (merkez) | DL-1 |
| X-10 | MOD-0178: `services/Diten.InventoryService/src/Diten.InventoryService.Application/Features/OutboundShipments/Services/OutboundAllocationService.cs:259` (toplama görevi yaratımı), `…/OutboundShipments/Handlers/CommandHandlers/OutboundFlowHandlers.cs:247` (`ReleaseOutboundShipmentHandler`; `:281` yalnız `Packed` kabul; `:378-382` tek işlemde `ReadyToShip`; `:394-411` `OutboundShipmentReady` outbox satırı), `:447` (`SyncOutboundDispatchHandler`; `:477` `ReadyToShip` ön koşulu; `:509-511` kısa sevk planı toplama görevlerinden), `…/PutawayTasks/Handlers/CommandHandlers/CreatePutawayTaskHandler.cs:28` | 0178 depo `OperatingMode`'unu okumaz; `ConfirmationChannel.External` var ama kullanılmaz (`services/Diten.InventoryService/src/Diten.InventoryService.Domain/Warehouse/Enums/WarehouseTaskEnums.cs:84`) | **Tek model (F-1, §8.8):** (1) `external-integrated` depoda allocation toplama görevi **açmaz**; (2) **dış release:** aynı `POST …/{outboundId}/release` ucu, depo `external-integrated` ise `Allocated` outbound'u kabul eder (iç depoda `Packed` kuralı aynen); **ön denetimler tahsislerden (r3, F-15):** release bugün geri çağırma, lot durumu (Blocked / Expired), SKT / yeniden test ve FEFO denetimlerini toplama görevlerinden hesaplanan `picked` kümesi üzerinden yapar (`OutboundFlowHandlers.cs:294-295` küme, `:298-342` denetimler); dış depoda görev olmadığından küme boştur ve denetimler sessizce atlanırdı — dış release'te kümenin yerine outbound'un **tahsisleri** konur, denetimler ve hata kodları (ör. 422 `LOT_UNDER_RECALL`) aynen uygulanır; FEFO gerekçe denetimi aynen; **tek işlemde** `ReadyToShip` + `OutboundShipmentReady` outbox satırı + `IExternalFulfilmentGate.InstructAsync` (talimat + giden mesaj `Pending`) — biri başarısızsa hiçbiri yazılmaz; ortak askıda / kanal kapalıysa release yine olur, mesaj `NotSent` + istisna; (3) sync-dispatch dış outbound'da kısa sevk planını toplama görevleri yerine **tahsisler** üzerinden kurar (toplama görevi yoktur); `Shipped` geçişi ve kuralları aynen; (4) `external-integrated` depoda yerleştirme görevi açma 422 `WMS_EXTERNAL_WAREHOUSE`. İç depoda davranış **değişmez** (0178 testleri değişmeden yeşil) | MOD-0178 (merkez, Beste) | DL-6 |
| X-11 | `docs/analysis/contracts/wms-bundle.openapi.yaml` (1.1.0) | dış kapı yalnız alan olarak (taslak §14) | eklemeli yeni sürüm — CT adımı, **DL-6 başlamadan**: dış depoda görev üretilmemesi, `Allocated` → dış release, `WMS_EXTERNAL_WAREHOUSE` | MOD-0178 + CT | DL-6 |
| X-12 | MOD-0183 (`Diten.SupplyChainService`) dispatch | `GOODS_ISSUE_SALES` tek yazıcı, `Dispatched`'de (`execution/domains/supply-chain-execution/module-packs/MOD-0183-shipment-tracking-pod.md:52`); `supplychain.shipments.dispatch` (`:83`); `SourceSystem` / `ExternalRef` (`:97`) | 3PL deposunun sevkiyatı 3PL'in sevk onayıyla dispatch edilir: 0179 servis kimliğiyle MOD-0183 dispatch ucunu çağırır (`sourceSystem` = ortak kodu, `externalRef` = mesaj kimliği, satırlar = 3PL'in onayladığı lot / miktar) — ya da MOD-0183 sahibinin seçtiği yol (Q-18) | MOD-0183 sahibi (MVP-6) | DL-6 |
| X-13 | MOD-0142 GRN — `services/Diten.ProcurementService/src/Diten.ProcurementService.Api/Controllers/GrnController.cs:58-59` | GRN `SourceSystem` / `ExternalRef` alanı var (`execution/domains/procurement/module-packs/MOD-0142-receiving-grn.md:63`); başlık `warehouseId` + `locationId` ister (`:68`) | 3PL teslim onayı → servis kimliğiyle `POST /api/grn` (`locationId` = deponun tek stoklanabilir lokasyonu — Q-9), `sourceSystem`, `externalRef`, Idempotency-Key = mesaj kimliği — ya da procurement sahibinin seçtiği yol (Q-19) | procurement sahibi | DL-7 |
| X-14 | Platform kiracı sır kasası — **MOD-0012-FU01 Tenant Runtime Secret Vault** (registry `execution/registries/module-id-registry.md:70`, paket taslak) | `ISecretsProvider` yalnız okur (`services/Diten.Building.Blocks/Diten.BuildingBlocks.Security.Secrets/ISecretsProvider.cs:3-7`) | **Sonraki adım (F-10)** — v1.0'ı bekletmez; FU gelince `SetThirdPartyChannelSecretRefCommand` değer kabul eden yazmaya eklemeli genişler (§8.5). Registry notu (`:70`) FU01'i "not a gate (F-10 interim secret reference)" olarak yazar — paketle tutarlı (r3, F-17 (a)) | Platform (merkez / Beste) | DL-5 sonrası FU |
| X-15 | Governance Rol İzinleri köprüsü (`frontend/Diten.Web/Views/Governance/RoleAssignments/_IndexL10n.cshtml`, `SharedResource.*.resx` `Perm.Entity.*`) | fiiller var | **yeni fiil yok**; yalnız varlık satır adları 7 dilde | Web ortak (merkez) | DL-3, DL-8 |
| X-16 | `services/Diten.AuthService/**` rol şablonu ve servis kimliği izinleri | — | §14 rol önerisi (Q-13); Inventory servis kimliğine `supplychain.shipments.dispatch` (Q-18) ve `procurement.grn.create` (Q-19) | Ali (Auth) | ayrı WP; DL-6 / DL-7 kapısı |
| X-17 | `execution/domains/supply-chain-execution/module-packs/MOD-0178-putaway-picking-packing.md` §14.1 | `warehouse.waves.*`, `warehouse.barcode.*` notları | not: "`warehouse.third-party-logistics.*` MOD-0179'undur; MOD-0178 orada anahtar tanımlamaz" (`.antigravity/rules/permission-key-standard.md:92`) | MOD-0178 (merkez) | `.antigravity` kapısı (§6) |
| X-18 | `services/Diten.Platform/tests/Diten.Platform.Application.Tests/RecordCodes/RecordCodeTypeCatalogTests.cs` `RuleAheadOfCatalogue` | `CNT`, `WAV`, `DVC`, `LBL` | RCS-001 §3'e `TPX` yazıldığı aynı adımda `["TPX"]` satırı | Platform (merkez) | `.antigravity` kapısı (§6) |
| X-19 | `gateway/Diten.ApiGateway/Middleware/TenantResolutionMiddleware.cs` — `IsNonTenantPath` (`:430-436`; çağrı yeri `:35`) | Program.cs `:186` her isteğe uygular; kiracı sinyali yoksa ve yol public değilse 400 "Missing Tenant" (`:213-226`); gelen yol public listede yok; Development'ta `TryGetDevelopmentBypassTenant` (`:595`) eksik kiracıyı gizler; çözülen kiracı `X-Tenant-Id` olarak servise yazılır (`:238`) | **Adlı muafiyet (F-2; r3, F-14):** `IsNonTenantPath`'e yalnız `/api/warehouse/third-party-logistics/inbound` öneki (yorumla: "MOD-0179 gelen uç — kiracı kanal anahtarından servis tarafında çözülür"). `:35`'teki erken dönüş kiracı çözümünden, Development bypass'ından (`:203-211`) ve `X-Tenant-Id` yazımından (`:238`) **önce** çalışır; bu yolda Gateway başlık yazmaz ve 3PL'in gönderdiği başlığa dokunmaz. `IsPublicEndpoint` kullanılmaz (r2'nin seçimi bypass açıkken sabit kiracıyı başlığa yazıyordu). Başka hiçbir 0179 yolu muaf olmaz. Servis kuralı (§8.4): gelen uçta `X-Tenant-Id` kiracıyı belirlemez; gelirse ve anahtarın kiracısından farklıysa 401, yazım yok. Ölçüm bypass **kapalı ve açık** (AC-63, S-63, S-63b) | integration-agent (Gateway, MOD-0032) | DL-5 |

**0178 / 0181 / 0182 bekçi testleri değişmez (bağlayıcı):** `INVT/Warehouse/**`, `INVT/CycleCount/**` ve `INVT/Barcode/**` dosyaları **hiçbir satırı değişmeden**
yeşil kalır (X-5 / X-6 / X-9 ve Gateway X-8 bu dizinlerde değildir; yalnız adlı satırları değişir). DL-6'nın 0178 davranışı `INVT/ThirdPartyLogistics/`
altında ölçülür. Kanıt: dilim kabulünde
`git diff --stat <dilim-başı>..<dilim-sonu> -- services/Diten.InventoryService/tests/Diten.InventoryService.Tests/Warehouse services/Diten.InventoryService/tests/Diten.InventoryService.Tests/CycleCount`
boş (AC-43).

## 6. Protected Paths
- `.antigravity/**` — kullanıcı onayı ister. **Kapı — paket onayından ÖNCE, AÇIK:** (1) PKS-001 §4'e `warehouse.third-party-logistics.*` satırı (Q-1 — karar),
  `:92` istisna cümlesine üçüncü alt ad alanı, `:105` `warehouse.*` satırına "hariç — MOD-0179" eki; (2) RCS-001 §3 `TPX` (3PL istisnası) satırı; (3) aynı
  adımda X-17 ve X-18. **Hazırlayan:** CT; **onaylayan:** Beste. Bu paket ve dilimleri `.antigravity/**`'a **yazmaz**. Kapı kapanmadan paket onay satırı
  (CT-APR-01) yazılmaz. Önerilen PKS-001 satırı:
  `` | `warehouse.third-party-logistics.*` | `Diten.InventoryService` — MOD-0179 3PL Integration (3PL ortakları, kanallar, eşlemeler, mesajlar, istisna kutusu, mutabakat, eşik ayarları; owner decision Q-1 (MOD-0179 owner decisions 01, 2026-11-13) — `warehouse.*` altında MOD-0179'a ayrılmış adlı alt ad alanı; base path `/api/warehouse/third-party-logistics`) | MOD-0179 (3PL Integration) | ``
  MDM iş ortağının RCS öneki (r1 Q-11) bu kapıda **değildir** — MOD-0049-FU01'e taşındı.
- `gateway/Diten.ApiGateway/**/ocelot.json` — yalnız integration-agent; bu paket değişiklik **istemez**. Gateway ara katmanı (`TenantResolutionMiddleware.cs`) yalnız
  X-19'un tek satırı için, integration-agent eliyle; `Mod0178WarehouseRouteTests.cs` yalnız X-8'in iki satırı için.
- `services/Diten.Building.Blocks/**` (BL-501 ve sır sağlayıcısı olduğu gibi).
- MOD-0173 / 0174 / 0178 / 0180 / 0181 / 0182 kodu ve testleri — yalnız §5.1'de adıyla sayılan dosyalar.
- `services/Diten.MdmService/**` (MOD-0049-FU01'in), `services/Diten.ProcurementService/**`, `services/Diten.SupplyChainService/**` (X-12 / X-13 yalnız
  sahibinin seçtiği yolla, o ekibin eliyle), `services/Diten.Platform/**` (X-18 dışında), `services/Diten.AuthService/**` (X-16 ayrı WP); Archive; `_Layout.cshtml`.
- Donmuş sözleşmeler: `wms-bundle.openapi.yaml` (yalnız X-11, CT adımıyla), `location.openapi.yaml`, `inventory-bundle.openapi.yaml`, `grn-event.openapi.yaml`.

## 7. Dependencies

| Bağımlılık | Tür | Durum (ölçüm) | Kullanım | Değişiklik | Sahip |
|---|---|---|---|---|---|
| MOD-0049-FU01 iş ortağı | **HARD — DL-4 kapısı** (DL-1…DL-3 sahte portla) | registry `:160` planned; paket yok | ortak seçimi, görüntü | yok (yalnız okuma) | MDM (Ali) — merkez geliştirir |
| LOCATION `operatingMode` + stoklanabilir lokasyon | HARD | `services/Diten.InventoryService/src/Diten.InventoryService.Domain/Entities/Location.cs:31`; stoklanabilir yalnız `Bin` / `StorageLocation` (`LocationHierarchyRules.cs:48-49`) | 3PL deposunun tanımı, GRN hedefi | yok | merkez |
| MOD-0178 outbound | HARD; DL-6 XMC | release yalnız `Packed` (`OutboundFlowHandlers.cs:281`); `Shipped` yalnız sync-dispatch'ten (`:525`) | dış release, sevk onayı | X-10, X-11 | merkez (Beste) |
| MOD-0183 sevkiyat | HARD (DL-6) | `ready-for-dev`; dispatch + `GOODS_ISSUE_SALES` tek yazıcı | stok düşüşü | X-12 | MOD-0183 sahibi (MVP-6) |
| MOD-0142 GRN | HARD (DL-7) | `POST /api/grn` (`GrnController.cs:23`, `:58-59`) | stok girişi | X-13 | procurement |
| MOD-0141 PO | HARD (DL-7, okuma) | `GET /api/purchase-orders/{poId}` (`services/Diten.ProcurementService/src/Diten.ProcurementService.Api/Controllers/PurchaseOrdersController.cs:48`) | ön bildirim satırları | yok | procurement |
| MOD-0173 defter, MOD-0174 lot, MOD-0290 SKU | HARD (okuma) | `Find` | mutabakat, lot / SKT karşılaştırması, görüntü | yok | merkez / MDM |
| Sır sağlayıcısı (MOD-0012) | HARD (DL-5) | Inventory `Diten.BuildingBlocks.Security.Secrets`'a başvuruyor (`services/Diten.InventoryService/src/Diten.InventoryService.Api/Program.cs:3`); emsal Platform `SecretReferenceResolver` (`services/Diten.Platform/src/Diten.Platform.Infrastructure/Services/Notifications/SecretReferenceResolver.cs:22-55`) | referans çözümü (F-10) | yok | — |
| Kiracı sır kasası (MOD-0012 FU'su) | SOFT — sonraki adım | yok | değer yazımı ekranı | X-14 | merkez (Beste) |
| Gateway kiracı ara katmanı | HARD (DL-5) | gelen yol kiracısız yol listesinde değil (`TenantResolutionMiddleware.cs:430-436`; r3, F-14) | tokensız gelen uç | X-19 | integration-agent |
| Servis kimliği (MOD-0033-FU02) | HARD (DL-6, DL-7) | registry'de review / local integration | MOD-0183 dispatch, MOD-0142 GRN çağrıları | X-16 | Ali (Auth) |
| MOD-0040-FU02 RCS-001 | SOFT | ara üretici MOD-0178 geçici sayacı | `TPX` | X-7 | merkez |
| MOD-0018 RBAC, BL-501 | HARD | `[HasPermission]` + manifest; InventoryService yol b (`tests/architecture/audit-ledger/Diten.InventoryService.md:12`) | izin, denetim | X-16 | Ali / korunan yol |
| SFTP kütüphanesi | HARD (DL-10, v1.1) | depoda yok | dosya ve EDI taşıması | yeni NuGet (öneri SSH.NET, MIT) — DL-10 ilk adımı, güvenlik taraması | Beste |
| EDI ayrıştırıcı | HARD (DL-11, v1.1) | depoda yok | EDIFACT + X12 | **karar Q-3: v1.1 mesaj kümesine dar kendi ayrıştırıcı** (kütüphane yok) | Beste |
| 3PL-BUNDLE | HARD (contract-first) | yok | sözleşme | bu paket (DL-1) | supply-chain-execution |
| Gateway rotası | — | `/api/warehouse/{everything}` → InventoryService (`gateway/Diten.ApiGateway/ocelot.json:1153-1171`), `AuthenticationOptions` yok | §15 | `ocelot.json` değişmez | integration-agent |

## 8. Runtime Constraints

### 8.1 Servis ve kapsam
- Servis `Diten.InventoryService` (AGENTS.md §3 tablosu), taban `/api/warehouse/third-party-logistics`. Web (5001) servise doğrudan değil, same-origin
  MVC proxy → Gateway (5000) üzerinden gider.
- Yönetim uçları: `Authorization: Bearer`, `X-Legal-Entity-Id`, `X-Correlation-Id`; yazmalarda `Idempotency-Key`; değişebilir kayıtta `If-Match`. Replay
  araması sırası: izin (403) → biçim (400) → idempotency araması → varlık (404) → `If-Match` (409) → durum (409) → tekillik (409).
- Gelen API ucu: JWT yok; `X-TPL-Timestamp` + `X-TPL-Signature` + `X-TPL-Message-Id` (§8.4). InventoryService kapsam ara katmanı kimliği doğrulanmamış
  isteği dokunmadan geçirir (`services/Diten.InventoryService/src/Diten.InventoryService.Infrastructure/Middleware/InventoryScopeResolutionMiddleware.cs:76-81`);
  kiracı / şirket kapsamını 0179'un imza süzgeci kanal kaydından kurar.
- InventoryService PUT / DELETE bekçisi (`LedgerImmutabilityArchitectureTests`) değişmez.
- İşçiler (gönderici, 90 gün süpürücüsü; v1.1 SFTP okuyucu) `IHostedService`; kiracı başına ayrı kapsamda çalışır.

### 8.2 Durum makineleri
**Ortak:** `Active` ⇄ `Suspended` (gerekçe). Silme yok. **Kanal:** `Open` ⇄ `Closed` (gerekçe — M-16); sağlık (`Healthy` / `Delayed` / `Error`) okumada
hesaplanır. **Gelen mesaj:** `Received` → `Processed` | `Failed` (+ istisna) | `Duplicate` (+ kendiliğinden `Ignored` istisna — mockup K-8) | `Held` (ortak
askıda) | `Rejected` (kanal kapalı — + `ChannelClosed`); `Failed` → `Processed`; `Held` → `Processed` | `Discarded` (gerekçe) | arşiv (türetilmiş). **Giden
mesaj:** `Pending` → `Sent` | `Failed` | `NotSent` (askı / kapalı kanal / iş ortağı ana veride pasif; düzelince `Pending`). **Talimat:** `Pending` → `Sent` →
`PartiallyConfirmed` → `Confirmed` → `Closed`; `Cancelled` (FU-0179-CANCEL). **İstisna:** `Open` → `Retrying` → `Resolved` | `Open`; `Open` → `Ignored`
(gerekçe); `Resolved` / `Ignored` sondur. **Mutabakat satırı:** `Matched` | `Difference` → `Investigating` → `Resolved` (gerekçe). Geçersiz geçiş 409
`TPL_INVALID_STATE`.

### 8.3 Kanonik mesaj modeli ve mesaj listesi
Her bağdaştırıcı içeriği **kanonik** modele çevirir; iş kuralları yalnız kanonik modeli görür.

| Kanonik tür | Yön | API (v1.0) | EDIFACT (v1.1; Q-3 — karar) | X12 (v1.1; Q-3 — karar) |
|---|---|---|---|---|
| `ShipmentInstruction` | Out | JSON | INSDES | 940 |
| `InboundAdvice` | Out | JSON | DESADV (giden) | 943 |
| `ShipmentConfirmation` | In | JSON | DESADV (gelen) | 945 |
| `ReceiptConfirmation` | In | JSON | RECADV | 944 |
| `InventoryReport` | In | JSON | INVRPT | 846 |
| `Unsupported` | In | tanınan diğer türler | INVOIC vb. | 210 / 810 vb. |

X12 856 kullanılmaz (Q-3). CONTRL / 997 fonksiyonel alındı v1.1'de de gönderilmez (FU-0179-ACK). Dosya (v1.1) CSV / XML düzenleri API JSON'unun
düzleştirilmiş biçimidir (3PL-BUNDLE). Eşleme yoksa `UnknownProductMapping`.

### 8.4 Entegrasyon kanalları
- **API (v1.0, DL-5).** Giden: `OutboundBaseUrl` + 3PL-BUNDLE yolları; `ApiKey` ya da `OAuthClientCredentials`; 5 deneme, üstel bekleme (1, 2, 4, 8, 16 dk),
  sonra `Failed` + `ConnectionAuth`. **Gelen:** `POST /api/warehouse/third-party-logistics/inbound/{keyId}/messages`; imza = HMAC-SHA256(`InboundSigningSecret`,
  `timestamp + "\n" + messageId + "\n" + sha256(body)`), karşılaştırma **sabit süreli** (`CryptographicOperations.FixedTimeEquals` — F-9 (b)); saat kayması
  ± 5 dk; `keyId` küresel tekil ve kiracı / şirket / kanalı belirler. Bilinmeyen anahtar, yanlış imza (başka kiracının sırrıyla imzalanmış dahil), süresi
  geçmiş zaman → **401, sıfır yazım** (mesaj kaydı da yok). **Kiracı kuralı (F-2):** Gateway `X-19` muafiyetiyle tokensız isteği geçirir; istekte `X-Tenant-Id`
  varsa Gateway onu servise iletir — servis bu başlıkla kiracı **seçmez**; değer anahtarın kiracısından farklıysa 401, sıfır yazım; aynıysa yok sayılır. Gövde
  ≤ 5 MB. Kanal kapalı → 409 `TPL_CHANNEL_CLOSED` + mesaj `Rejected` + `ChannelClosed` istisnası (imza geçerli olduğu için kayıt yazılır).
- **Dosya (v1.1, DL-10).** SFTP; ana makine anahtarı parmak izi zorunlu; desene uyan dosya okunur, arşiv klasörüne taşınır (silinmez); dosya adı + sha256
  tekrar anahtarı; giden dosya `.tmp` + yeniden adlandırma; kapalı kanalda okuma yok.
- **EDI (v1.1, DL-11).** EDIFACT (UNB / UNH / UNT / UNZ) ve X12 (ISA / GS / ST / SE / GE / IEA) zarf ve sayaç denetimi; **taşıma SFTP** (Q-2; AS2 FU-0179-AS2);
  bir ortakta iki standart birlikte (kanal başına bir standart; T-1).
- **Ortak:** kanal yalnız `WarehouseIds`'indeki depolar için mesaj üretir / kabul eder; başka depo `Format` istisnası.

### 8.5 Kanal sırları — referans ara çözümü (F-10) ve kasa FU'su
- **v1.0 (sahip kararı F-10):** kanal kaydı yalnız **sır referansı** tutar (`SecretRefs[].Ref` — yapılandırma anahtarı adı,
  `ThirdPartyLogistics:{TenantId}:{ChannelId}:{SecretName}`); **değeri işletim yerleştirir** (InventoryService ortam / yapılandırma sağlayıcısı). Emsal
  Platform `TenantMessagingSettings.CredentialSecretRef` + `SecretReferenceResolver` (yalnız referans saklanır, değer salt okunur `ISecretsProvider`'dan).
  **Referans sunucuda üretilir (r3, F-13):** `SetThirdPartyChannelSecretRefCommand` yalnız `secretName` alır (türe göre izinli ad, §4.2); referansı sunucu
  isteğin kiracısı (`TenantId`, JWT'den) ve kanalın kimliğinden `ThirdPartyLogistics:{TenantId:D}:{ChannelId:D}:{SecretName}` olarak kurar ve `RefSetAt` /
  `RefSetBy` yazar. Gövdede `ref` (ya da herhangi bir referans / değer alanı) gelirse 400 — istemci referans seçemez, başka kiracının anahtarını gösteremez.
  **Çözücü saklanan dizgeye güvenmez:** `IThirdPartySecretResolver` her okumada referansı kanal kaydının `TenantId` + `Id` + `Name`'inden yeniden kurar;
  saklanan `Ref` bundan farklıysa çözmez (`TPL_SECRET_UNRESOLVED`, "Ayarlı değil", değer hiçbir yere gönderilmez) — kiracılar arası sır okuması ve "ayarlı"
  bilgisiyle varlık sızdırma kapanır.
- **Ekran:** yalnız "Ayarlı" (referans çözülüyor) ya da "Ayarlı değil" + referansın son değişim tarihi; değer ve referansın çözdüğü içerik hiçbir ekranda
  yok. Yardımda bir cümle: "Sırlar ekranda görünmez; kanal yalnız sır referansını tutar, değeri işletim ekibi yerleştirir."
- **Sırrı okuyan yerler (F-9 (c)):** (1) gönderici işçi (giden çağrı kimlik bilgisi), (2) gelen uç imza süzgeci (`InboundSigningSecret`), (3) bağlantıyı sına.
  Üçü de `IThirdPartySecretResolver` üzerinden, bellekte, istek / döngü süresince; değer loga, OTel'e, istisna metnine, cevaba yazılmaz.
- **Sonraki adım (X-14):** Platform kiracı kasası FU'su gelince değeri Web'den bir kez yazan genişleme (değer kasaya, Inventory'de yalnız referans); o zaman
  parmak izine değerin anahtarlı özeti katılır (§4.8).

### 8.6 Askıya alma ve bekletilen mesajlar
- Askıya alma / etkinleştirme / kanal kapatma / açma gerekçe ister ve denetime yazılır.
- Askıdaki ortağa giden mesaj gönderilmez (`NotSent`); gelen mesaj `Held`, `HeldUntil = ReceivedAt + 30 gün`.
- Etkinleştirme bekletilenleri kendiliğinden işlemez; "Bekletilenler" sekmesinden seçilenler ya da tümü geliş sırasıyla işlenir; sıra dışı seçimde
  `confirmOutOfOrder = true` gerekir.
- **Q-7 (karar):** askıdayken bekletilenleri işleme 422 `TPL_PARTNER_SUSPENDED` — önce etkinleştirilir.
- `HeldUntil` geçen mesaj salt okunur: işleme / atma 422 `TPL_HELD_EXPIRED`; silinmez.

### 8.7 İstisna kutusu yeniden işleme ve kişisel veri
- Lot / SKT farkı (T-3): istisna açılır, **bizdeki lot ve SKT değişmez**, onay deftere yansımaz.
- Tekrar: aynı `DedupKey` → `Duplicate`, istisna doğrudan `Ignored`.
- **Yeniden işleme:** tek ya da toplu **1..200, aynı ortak + aynı tür** (M-6); 201 → 422 `TPL_BULK_LIMIT`; karışık → 422 `TPL_BULK_MIXED`; her kayıt kendi sonucu.
  Web 200'den fazla seçimde uyarır, yalnız ilk 200'ü gönderir.
- **Eşleme ekle ve yeniden işle:** eşleme yaratılır, aynı ortağın aynı koddaki açık `UnknownProductMapping` istisnaları (≤ 200) yeniden işlenir.
- **Yok say:** gerekçe zorunlu.
- **Ham içerik:** ayrı izin (`raw-messages.read`); okumada maskeli (alıcı adı, telefon, e-posta, adres satırı — ilk / son iki karakter dışında `•`); imza ve sır
  alanları hiç gösterilmez. **90 gün (F-5):** süpürücü günde bir, kiracı başına ≤ 1.000 yük / döngü, `PurgeThirdPartyMessagePiiCommand` ile kişisel veriyi
  kalıcı kaldırır (§4.4); temizlenmiş içerikte maskeleme alanları zaten `•`'dir, ekran "Kişisel veriler {tarih} tarihinde kaldırıldı" gösterir.

### 8.8 Çıkış akışı — tek model (F-1; MOD-0178 / MOD-0183)
1. Outbound `external-integrated` depoda **allocate** edilir (tahsis deponun tek stoklanabilir lokasyonundan — Q-9); **toplama görevi açılmaz** (X-10 (1)).
   Outbound `Allocated` kalır (`Picked` / `Packed` adımları yoktur).
2. **Dış release** (aynı release ucu, X-10 (2)): `Allocated` + dış depo → **tek işlemde** `ReadyToShip` + `OutboundShipmentReady` outbox satırı (MOD-0183
   sevkiyatı bugünkü gibi bu olaydan doğar) + `ThirdPartyInstruction` (`Shipment`) + giden mesaj `Pending`. İç depoda release bugünkü gibi yalnız `Packed`.
3. Gönderici işçi talimatı API kanalından yollar (`Sent`).
4. 3PL sevk onayı → kanonik işlemci: eşleme; lot / SKT (T-3 — tahsis lotundan farklıysa `LotExpiryMismatch`); miktar (talimattan fazla → `QuantityMismatch`,
   eksik → kısa sevk olarak kabul, MOD-0178 kısa sevk kuralı); outbound `ReadyToShip` değilse ya da talimat kapanmışsa `LateConfirmation`.
5. Geçerse X-12 yolu: 0179 servis kimliğiyle MOD-0183 dispatch (satırlar = 3PL'in onayladığı lot / miktar) → MOD-0183 `GOODS_ISSUE_SALES` yazar → 0178
   bugünkü sync-dispatch ile `Shipped` (kısa sevk planı tahsislerden — X-10 (3)). **0179 hiçbir aşamada stok hareketi yazmaz.**
6. Talimat `Confirmed`, outbound `Shipped` olunca `Closed`.

### 8.9 Giriş akışı (MOD-0142)
1. **Q-8 (karar):** kullanıcı elle "Giriş ön bildirimi gönder" (`POST inbound-advices`: PO + depo); PO satırları MOD-0141'den okunur.
2. `ThirdPartyInstruction` (`InboundAdvice`) + giden mesaj.
3. 3PL teslim onayı → eşleme; ön bildirimde lot yoksa 3PL'in lotu kabul edilir; farklıysa `LotExpiryMismatch`; miktar farkı `QuantityMismatch` (Q-19 teyidi —
   öneri GRN açılmaz).
4. Geçerse X-13 yolu: servis kimliğiyle `POST /api/grn` (`warehouseId`, `locationId` = deponun tek stoklanabilir lokasyonu, `sourceSystem`, `externalRef`,
   Idempotency-Key = mesaj kimliği) → MOD-0142 `GOODS_RECEIPT_PO`. 3PL deposunda yerleştirme görevi açılmaz (X-10 (4)).

### 8.10 Stok raporu ve mutabakat
- Stok raporu → `ThirdPartyStockReport` → depo × SKU × lot satırları.
- **Bizim stok** = defterde o depo için `ReportedAt` anına kadarki toplam (yalnız `Find`; "an itibarıyla" okuma DL-8 başında ölçülür; yoksa bugünkü bakiye −
  `ReportedAt` sonrası hareketler).
- **Fark** = bizim − 3PL. **Yoldaki (M-4):** `ReportedAt`'e kadar gönderilmiş, onaysız kalan talimat miktarı — sevk talimatı **+**, ön bildirim **−**.
  **Açıklanmamış** = fark − yoldaki − açıklanmış; yalnız açıklanmamış ≠ 0 satır `Difference`.
- **Q-4 (karar):** "Düzeltme öner" düğmesi **yok**; "Çözüldü" yalnız gerekçeyle (FU-0179-ADJUST). Mutabakat masası stok yazmaz.
- Raporda olmayan bizdeki satır: 3PL = 0; bizde olmayan rapor satırı: bizim = 0 (eşlemesizse `UnknownProductMapping`).
- CSV `reconciliation.export` ister; `=`, `+`, `-`, `@` ile başlayan hücre kaçışlanır.

#### 8.10.1 Tek stoklanabilir lokasyon
- **Kim yaratır:** kullanıcı, MOD-0173-FU02 LOCATION ekranında, 3PL deposunun altında **tek** bir `StorageLocation` (`isStockable = true`) — 0179 lokasyon yaratmaz.
  Stoklanabilir yalnız `Bin` / `StorageLocation` olabilir (`LocationHierarchyRules.cs:48-49`).
- **Bağlamada doğrulama:** depo `external-integrated` **ve** altında tam bir etkin stoklanabilir lokasyon; sıfır ya da birden çok → 422
  `TPL_WAREHOUSE_STOCK_LOCATION` (ortak oluşturma ve depo ekleme). Bağlamadan sonra ikinci stoklanabilir lokasyon açılırsa sağlıkta "Hatalı · lokasyon
  kuralı" ve giden / gelen işlem `Format` dışı istisna `TPL_WAREHOUSE_STOCK_LOCATION` ile durur (yeniden işleme düzeltmeden sonra).
- **GRN hedefi:** o lokasyon (X-13). **0178 allocation:** depoda tek stoklanabilir lokasyon olduğu için tahsis oradandır — DL-6 başında ölçülür; değilse X-10'a
  "dış depoda tahsis tek lokasyondan" kuralı eklenir.

### 8.11 Sağlık ve performans eşikleri
- Eşikler ortak başına (§4.1.1); ayar sayfası `settings.configure` ister (M-2).
- **Sağlık:** `Suspended` → Askıda; bütün kanallar kapalı → Kapalı; son 24 saatte açık `ConnectionAuth` ya da son gönderim `Failed` ya da lokasyon kuralı bozuk →
  Hatalı; eşiği aşan onaysız talimat ya da `StockReportMaxAgeHours`'u aşan rapor → Gecikmeli; değilse Sağlıklı. Simge + yazı.
- **KPI:** bağlı ortak; son 24 saatte mesaj (gelen / giden); başarısız mesaj; son stok raporu yaşı; açık mutabakat farkı.
- **Performans (DL-8):** son 30 gün — ortalama sevk onayı süresi, zamanında rapor oranı, mesaj hata oranı, açıklanmamış fark oranı.
- Gecikmiş rapor bandı ("Stok raporu gecikti." + istisnaya bağlantı).

### 8.12 Merkezi denetim ve yerel iz
- BL-501 InventoryService'te kurulu (yol b, `RequireEnlistment` kapalı-başarısız); her yazma komutu `ICentralAuditDeclaration` taşır; `SourceModule` =
  `THIRD-PARTY-LOGISTICS`. Başarılı ve yazan komut 1 kayıt; replay ve başarısız 0.
- **Hacim (Q-14, CT):** `ReceiveThirdPartyMessageCommand` ve `SendThirdPartyMessageCommand` her mesajda 1 kayıt (yol b) — varsayılan; alternatif mesaj defteri
  yol c eşdeğeri, CT kabulü ister.
- Sır değeri, imza, ham içerik ve kişisel veri merkezi kayda yazılmaz.

### 8.13 Kayıt kodu üretimi
- `ThirdPartyException.Number` = `{ŞİRKET}-TPX-{000001}` (OTOMATİK). Ara üretici MOD-0178 geçici sayacı — X-7; ortak servise geçiş FU-0179-RCS.
- Ortak bağlantısı iş ortağının kodunu kullanır; diğer nesneler görünen kod taşımaz (§22).
- `TPX` RCS-001 §3'te yoktur (`.antigravity/rules/record-coding-standard.md:45-78`) — `.antigravity` kapısında, **onaydan önce açık** (§6).

### 8.14 Ekranlar ve Blueprint soft pages
| Blueprint soft page | Ekran | Sayfa kodu |
|---|---|---|
| 3PL Integration Health | `/Warehouse/ThirdPartyLogistics` — KPI + ortak listesi; satır menüsü Aç, Bağlantıyı sına, Askıya al / Etkinleştir (gerekçe); **Sil yok**; "Ortak ekle" = FU01'den 3PL rollü ortak seçimi. `Details` sekmeleri: Genel (iş ortağı bilgisi salt okunur, depolar + lokasyon kuralı, sözleşme notu, denetim izi, **"Ücret mutabakatı — v2 · planlı" notu** — F-8), Kanallar, Eşlemeler, Mesajlar, Performans (DL-8), Bekletilenler | `TPLHEALTH` (nav) |
| (kanallar) | `/Warehouse/ThirdPartyChannels` — Compact; v1.0'da tür seçiminde yalnız API etkin, dosya / EDI "v1.1" etiketiyle devre dışı; sır satırı yalnız referans + "Ayarlı" | `TPLCHANNELS` |
| (eşlemeler) | `/Warehouse/ThirdPartyMappings` | `TPLMAPPINGS` |
| (mesajlar) | `/Warehouse/ThirdPartyMessages` — ham içerik ayrı izinle; 90 gün sonrası "kişisel veri kaldırıldı" | `TPLMESSAGES` |
| Exception Inbox | `/Warehouse/ThirdPartyExceptions` — süzgeç; toplu ≤ 200 + uyarı | `TPLEXCEPTIONS` (nav) |
| Reconciliation Workbench | `/Warehouse/ThirdPartyReconciliation` (DL-8) | `TPLRECON` (nav) |
| (eşik ayarları) | `/Warehouse/ThirdPartySettings` | `TPLSETTINGS` (nav) |

**"v2 · planlı" notu (F-8 — sahip kararı):** ortak ayrıntısının Genel sekmesinde ve sağlık ekranının alt bilgi şeridinde salt metin "Ücret mutabakatı — v2 ·
planlı"; bağlantı, düğme, izin ve menü öğesi **yoktur**. Durumlar her ekranda: boş, yükleniyor, hata, yetkisiz (UAS-001). Masaüstü öncelikli, 375 px, RTL, klavye.

### 8.15 Hata kodları
| HTTP | Kod | Ne zaman |
|---|---|---|
| 400 | `VALIDATION_FAILED`, `IDEMPOTENCY_KEY_REUSED` | biçim; aynı anahtar farklı gövde; gövdede kiracı / şirket; ham sıra benzeyen referans |
| 401 | (gövdede kod yok) | gelen uç: anahtar, imza, zaman, `X-Tenant-Id` uyuşmazlığı |
| 403 | izin yok | `[HasPermission]` |
| 404 | — | kayıt yok / başka kiracı / şirket |
| 409 | `TPL_CONFLICT`, `TPL_INVALID_STATE`, `TPL_PARTNER_ALREADY_LINKED`, `TPL_WAREHOUSE_TAKEN`, `TPL_MAPPING_TAKEN`, `TPL_CHANNEL_CLOSED` | |
| 422 | `TPL_PARTNER_NOT_3PL`, `TPL_PARTNER_MASTER_INACTIVE`, `TPL_WAREHOUSE_NOT_EXTERNAL`, `TPL_WAREHOUSE_STOCK_LOCATION`, `TPL_CHANNEL_TYPE_NOT_ENABLED`, `TPL_BULK_LIMIT`, `TPL_BULK_MIXED`, `TPL_PARTNER_SUSPENDED`, `TPL_HELD_EXPIRED`, `TPL_OUT_OF_ORDER_UNCONFIRMED`, `TPL_SECRET_NAME_INVALID`; 0178'de `WMS_EXTERNAL_WAREHOUSE` | iş kuralı |
| 503 | `TPL_MDM_UNAVAILABLE`, `TPL_SECRET_UNRESOLVED`, `TPL_INVENTORY_UNAVAILABLE` | bağımlılık |

### 8.16 Sözleşme (3PL-BUNDLE) ve dondurma
- Taslak `docs/analysis/contracts/drafts/3pl-bundle-v1-proposal.md` (yazılacak) **DL-1'in ilk adımı**: kanonik şema, gelen API (imza, başlıklar, `X-Tenant-Id`
  kuralı, hata), giden API, yönetim uçları, hata kodları; v1.1 ekleri (CSV / XML düzeni, EDIFACT / X12 eşleme tabloları) ayrı bölümde.
- **Dondurma DL-5 başlamadan** (ilk dış ortaktan önce): `docs/analysis/contracts/3pl-bundle.openapi.yaml` `1.0.0`; v1.1 eklemeli `1.1.0`.
- `BRIDGE-BASE`'in depoda karşılığı bulunmadı (dosya adı taraması) — Q-15.

### 8.17 Ücret mutabakatı genişleme noktası (v2)
T-2: v1'de ücret ekranı, izni, varlığı **yoktur**; **ekranda yalnız "v2 · planlı" notu** (F-8, §8.14). Genişleme noktası: kanonik `Unsupported` (gelen fatura
mesajları tanınır, işlenmez, istisna), 3PL-BUNDLE'da ayrılmış `InvoiceAdvice` adı. Treasury'ye bilgi notu CT işi (§20).

### 8.18 AI Assist
Blueprint AI Tier `Assist`. v1'de **yok** (FU-0179-AI).

## 9. Layout & Shell Contract
- `shell: tenant` → `Views/Warehouse/{ThirdPartyLogistics,ThirdPartyChannels,ThirdPartyMappings,ThirdPartyMessages,ThirdPartyExceptions,ThirdPartyReconciliation,ThirdPartySettings}/`
  altındaki **her sayfa görünümünde** (partial hariç) `Layout = "_LayoutTenantShell";` **AÇIKÇA**; `_ViewStart.cshtml` değişmez (AC-26).
- View klasörü `Views/Warehouse/{Module}/`; rota `/Warehouse/{Module}` (Q-5).
- Yetkisiz kullanıcıya iskelet ve yönlendirme yok; menü ve üst çubuk kalır, içerikte tek açıklama (UAS-001).

## 10. Backend File Convention
Golden Reference Slim birebir (`services/Diten.DevEnablementService/src/Diten.DevEnablementService.Application/Features/GoldenReferenceSlim/`;
pack `execution/domains/developer-enablement/module-packs/DEV-0000-golden-reference-slim.md`):

```text
Features/ThirdPartyPartners/
├── Commands/        CreateThirdPartyPartnerCommand.cs, UpdateThirdPartyPartnerCommand.cs, SuspendThirdPartyPartnerCommand.cs,
│                    ActivateThirdPartyPartnerCommand.cs, UpdateThirdPartyThresholdsCommand.cs
├── Queries/         GetThirdPartyPartnerListQuery.cs, GetThirdPartyPartnerByIdQuery.cs, GetThirdPartyDashboardQuery.cs, GetThirdPartyPerformanceQuery.cs
├── Handlers/
│   ├── CommandHandlers/   CreateThirdPartyPartnerHandler.cs … UpdateThirdPartyThresholdsHandler.cs   (sealed class, suffix YOK)
│   └── QueryHandlers/     GetThirdPartyPartnerListHandler.cs … GetThirdPartyPerformanceHandler.cs
├── Validators/      CreateThirdPartyPartnerValidator.cs … UpdateThirdPartyThresholdsValidator.cs
└── ThirdPartyPartnerModels.cs
Features/ThirdPartyChannels/        Create / Update / Close / Open, SetThirdPartyChannelSecretRef, TestThirdPartyChannel; List / ById; ThirdPartyChannelModels.cs
Features/ThirdPartyMappings/        Create / Update / DeactivateThirdPartyMappingCommand; List; ThirdPartyMappingModels.cs
Features/ThirdPartyMessages/        Receive / Send / ProcessHeld / DiscardHeld / PurgeThirdPartyMessagePii; List / ById / Raw; ThirdPartyMessageModels.cs
Features/ThirdPartyExceptions/      ReprocessThirdPartyExceptions / IgnoreThirdPartyException / AssignThirdPartyException; List / ById; ThirdPartyExceptionModels.cs
Features/ThirdPartyInstructions/    SendInboundAdviceCommand; List / ById; ThirdPartyInstructionModels.cs
Features/ThirdPartyReconciliation/  AddReconciliationNote / MarkReconciliationInvestigating / ResolveReconciliationLine; StockReport List, Line List / ById, Export; Models
Features/ThirdPartyLogistics/       ortak: ThirdPartyContract.cs, ThirdPartyErrorCodes.cs, ThirdPartyCentralAudit.cs, Ports/, Services/, Resources/
```

**G-0179-01:** silme yok — `Delete` / `BulkDelete` komutu, `delete` izni, "Sil" eylemi yok; güncelleme `POST …/update`; mesaj / istisna / mutabakat listelerinde
yaratma formu yok; `data_mode: server`. **G-0179-02:** `ThirdPartyLogistics` Slim + sekmeli `Details.cshtml`; `ThirdPartyChannels` **Compact** (kanal formu > 8 alan).
İkisi CT kabulü ister (Q-17). Kurallar: Command / Query `sealed record`; Handler / Validator adında sonek yok; bir dosyada tek `public` tip;
`Handlers/CommandHandlers` / `Handlers/QueryHandlers` ayrımı zorunlu.

## 11. Frontend File Contract

| Klasör | Golden | Dosyalar |
|---|---|---|
| `Views/Warehouse/ThirdPartyLogistics/` | Slim + `Details.cshtml` (G-0179-02) | `Index.cshtml` (KPI + liste + gecikme bandı + "v2 · planlı" şeridi), `_Filter`, `_DataTable` (`data-dt-standard="v2"`, `data-dt-data-mode="server"`), `_IndexL10n`, `_CreateEditOffcanvas` (4 alan: iş ortağı seçimi, depolar, sorumlu, sözleşme notu — ad / adres / iletişim girdisi yok), `_DetailsQuickView`, `Details.cshtml` (6 sekme), `ThirdPartyLogisticsIndex.cs` |
| `Views/Warehouse/ThirdPartyChannels/` | Compact | `Index`, `Create`, `Edit`, `Details`, `_Form` (ortak: ad, tür, depolar; API: taban URL, kimlik doğrulama türü, zaman aşımı + sır referansları; v1.1 dosya / EDI alanları aynı formda, iki adımlı), `_Filter`, `_DataTable`, `_IndexL10n`, `ThirdPartyChannelsIndex.cs` — offcanvas / quick view **yok** |
| `Views/Warehouse/ThirdPartyMappings/` | Slim | `Index`, `_Filter`, `_DataTable`, `_IndexL10n`, `_CreateEditOffcanvas` (3 alan), `_DetailsQuickView`, `ThirdPartyMappingsIndex.cs` |
| `Views/Warehouse/ThirdPartyMessages/` | Slim (G-0179-01) | `Index`, `_Filter`, `_DataTable`, `_IndexL10n`, `_DetailsQuickView` (ham içerik izinle), `ThirdPartyMessagesIndex.cs` |
| `Views/Warehouse/ThirdPartyExceptions/` | Slim (G-0179-01) | `Index`, `_Filter`, `_DataTable` (toplu seçim), `_IndexL10n`, `_DetailsQuickView`, `_ActionOffcanvas.cshtml`, `ThirdPartyExceptionsIndex.cs` |
| `Views/Warehouse/ThirdPartyReconciliation/` | Slim (G-0179-01) | `Index`, `_Filter`, `_DataTable`, `_IndexL10n`, `_DetailsQuickView`, `_ActionOffcanvas.cshtml`, `ThirdPartyReconciliationIndex.cs` |
| `Views/Warehouse/ThirdPartySettings/` | Slim | `Index`, `_DataTable`, `_IndexL10n`, `_CreateEditOffcanvas` (5 eşik), `ThirdPartySettingsIndex.cs` |
| `wwwroot/assets/js/Warehouse/{Module}/` | | `index.js`, `index.l10n.js`; `ThirdPartyLogistics/details.js`, `ThirdPartyExceptions/bulk.js` |
| `Resources/Views/Warehouse/{Module}/` | | `{Module}Index.{en,tr,fr,es,zh,ar,ru}.resx` |
| `frontend/Diten.Web/Controllers/` | | 7 proxy controller → Gateway `/api/warehouse/third-party-logistics…` ve FU01 okuma ucu; her yazma `[ValidateAntiForgeryToken]`; `Accept-Language`, `If-Match`, `Idempotency-Key` iletilir |

Liste ekranına dokunan görev `verify_datatable_page.py --area Warehouse --module {Module} --format gaps` koşar ve sapmayı sahibine sorar. Nav anahtarları:
`Nav.Module.THIRDPARTYLOGISTICS`, `Nav.Page.TPLHEALTH`, `Nav.Page.TPLEXCEPTIONS`, `Nav.Page.TPLRECON`, `Nav.Page.TPLSETTINGS`.

## 12. Validation Rules

| Field | Required | Format/Rule | DB-level | Pre-check |
|---|---|---|---|---|
| Ortak `businessPartnerId` | Evet | uuid; FU01'de etkin + rol `ThirdPartyLogistics` | tekil (şirket) | FU01 okuması (`TPL_PARTNER_NOT_3PL`, 503) |
| Ortak `warehouseIds` | Evet | 1..20; şirketin `Warehouse`'u, `external-integrated`, tam bir etkin stoklanabilir alt lokasyon; başka etkin ortakta değil | uygulama denetimi + index | LOCATION okuması (`TPL_WAREHOUSE_NOT_EXTERNAL`, `TPL_WAREHOUSE_STOCK_LOCATION`, `TPL_WAREHOUSE_TAKEN`) |
| Ortak `responsibleUserId` | Evet | kiracı kullanıcısı | — | Auth okuması |
| Ortak `contractNote` | Hayır | ≤ 2000 | — | — |
| Askı / etkinleştirme / kanal açma-kapama gerekçesi | Evet | 10..500 | — | — |
| Eşikler | Evet | §4.1.1 | — | — |
| Kanal `name` / `type` / `warehouseIds` | Evet | 2..80 / v1.0 yalnız `Api` / ortağın depoları | tekil (ortak, ad) | — |
| API `outboundBaseUrl` | `Api` ise evet | `https://`; özel / yerel ağ adresi reddedilir (SSRF) | — | gönderimde DNS denetimi |
| Sır referansı `secretName` | Evet | türe göre izinli ad; `ref` / değer alanı gönderilirse 400 — referans sunucuda `ThirdPartyLogistics:{TenantId}:{ChannelId}:{SecretName}` (r3, F-13) | — | çözüm denetimi (yalnız "ayarlı" bilgisi; çözücü referansı yeniden kurar) |
| Eşleme `kind` / `ourRef` / `partnerValue` | Evet | enum / geçerli kimlik / 1..64 | tekil kısmi | SKU / LOCATION okuması |
| Gelen mesaj | Evet | ≤ 5 MB; 3PL-BUNDLE şeması; imza; `X-Tenant-Id` kuralı | `DedupKey` tekil | imza (401) |
| Toplu yeniden işleme `exceptionIds` | Evet | 1..200; aynı ortak + aynı tür | — | `TPL_BULK_LIMIT`, `TPL_BULK_MIXED` |
| Yok sayma / çözüldü gerekçesi | Evet | 10..500 | — | — |
| Sorumlu ata | Evet | kiracı kullanıcısı | — | — |
| Bekletilen işleme | biri | 1..200 uuid ya da `all = true` / `confirmOutOfOrder` | — | §8.6 |
| Ön bildirim `purchaseOrderId` / `warehouseId` | Evet | PO açık; ortağın deposu | tekil kısmi (talimat) | PO okuması |
| Mutabakat notu / miktar | Evet / Hayır | 1..1000 / işaretli ondalık | — | — |
| Idempotency-Key | Evet (yönetim yazması) — gelen uçta `X-TPL-Message-Id` | 8–100 | tekil index | If-Match / durumdan önce |
| If-Match | Evet (değişebilir kayıtta yaratma dışı) — adlı muafiyet: kanal teknik sinyal alanları | `W/"<version>"` | — | — |
| TenantId / LegalEntityId gövdede | Hayır | 400 | — | — |

## 13. Failure Path to Verify
- **Duplicate (aynı Idempotency-Key, aynı gövde)** — Expected: ilk cevap; yeni kayıt / merkezi kayıt / numara yok.
- **Aynı anahtar, farklı gövde (farklı sır referansı dahil)** — Expected: 400 `IDEMPOTENCY_KEY_REUSED`.
- **Aynı 3PL mesajı ikinci kez** — Expected: `Duplicate`, `Ignored` istisna; ikinci GRN / dispatch yok.
- **Missing alan** — Expected: 400. **Unauthorized** — Expected: 403; UAS-001; ham içerik izni yoksa ham bölüm yok.
- **Concurrency (eski If-Match)** — Expected: 409 `TPL_CONFLICT`. **Başka kiracı / şirket** — Expected: 404.
- **Gelen uç: yanlış imza / bilinmeyen anahtar / ± 5 dk dışı / A kiracısının `keyId`'si + B kiracısının sırrıyla imza / `X-Tenant-Id` başka kiracı** — Expected:
  401; mesaj kaydı dahil sıfır yazım.
- **Gelen uç, Gateway bypass kapalı, tokensız ve `X-Tenant-Id`'siz** — Expected: Gateway 400 değil; servise ulaşır; geçerli imzada 202.
- **Kapalı kanala geçerli imzalı mesaj** — Expected: 409; `Rejected`, `ChannelClosed`.
- **Askıdaki ortak** — Expected: `Held`; askıdayken işleme 422 `TPL_PARTNER_SUSPENDED`; süresi dolan 422 `TPL_HELD_EXPIRED`.
- **201 kayıt / karışık tür toplu** — Expected: 422; hiçbiri işlenmez.
- **Lot / SKT farkı** — Expected: `LotExpiryMismatch`; lot ve SKT değişmez; GRN / dispatch yok.
- **Talimattan fazla onay / kapanmış belgeye onay** — Expected: `QuantityMismatch` / `LateConfirmation`; yazım yok.
- **3PL deposunda sıfır ya da iki stoklanabilir lokasyon** — Expected: bağlama 422 `TPL_WAREHOUSE_STOCK_LOCATION`.
- **3PL deposunda iç toplama / yerleştirme; `Packed` olmayan iç depo release** — Expected: toplama görevi yok; yerleştirme 422; iç depoda release kuralı aynen.
- **Dış release'te talimat yazımı başarısız** — Expected: release, `ReadyToShip`, outbox ve talimatın hiçbiri yazılmaz.
- **Ortak askıdayken dış release** — Expected: release yazılır; talimat mesajı `NotSent`; istisna.
- **90 günü geçmiş yük** — Expected: kişisel veri kalıcı kaldırılır; `OriginalSha256`, kanonik özet ve zarf aynı.
- **v1.0'da dosya / EDI kanalı yaratma** — Expected: 422 `TPL_CHANNEL_TYPE_NOT_ENABLED`.
- **Gövdede `ref` ya da değer alanı; saklanan referans başka kiracıyı gösteriyor (r3, F-13)** — Expected: 400, kayıt yok / çözülmez, "Ayarlı değil", değer gönderilmez.
- **Özel ağ adresine giden URL** — Expected: 400 ve gönderimde ret.
- **Fazladan PUT / DELETE** — Expected: `LedgerImmutabilityArchitectureTests` kırmızı.

## 14. Authorization Convention

**Namespace (Q-1 — sahip kararı, kararlar 01 `:33-39`):** `warehouse.third-party-logistics.*` — PKS-001 `warehouse.*` altında MOD-0179'a ayrılmış **adlı alt ad alanı**
(`.antigravity/rules/permission-key-standard.md:92`; bugünkü örnekler `warehouse.waves.*`, `warehouse.barcode.*`, `:106-107`). `warehouse.3pl.*` olamaz (`:40` kesim
dilbilgisi). İki pakette kayıt: bu paket + MOD-0178 §14.1 notu (X-17). **PKS-001 satırı onaydan önce `.antigravity` kapısında, AÇIK** (§6).

**Fiiller:** yalnız mevcut fiiller; **yeni fiil yok** (X-15). "Askıya al" UI etiketi, anahtar `deactivate`.

| Anahtar | Tier | Dilim | Anlam |
|---|---|---|---|
| `warehouse.third-party-logistics.partners.read` | 1 | DL-1 | ortak listesi / ayrıntısı; DL-2'de sağlık / KPI; DL-8'de performans |
| `….partners.create` / `.update` | 1 | DL-1 | ortak bağla, düzenle |
| `….partners.activate` / `.deactivate` | 2 | DL-1 | etkinleştir / askıya al |
| `….channels.read` | 1 | DL-1 | kanal özeti (sırsız) |
| `….channels.configure` | 3 | DL-1, DL-5 | kanal oluştur / düzenle / aç / kapat, sır referansı, bağlantıyı sına (mockup K-5) |
| `….mappings.read` / `.update` | 1 | DL-1 | eşlemeler |
| `….messages.read` / `.update` | 1 | DL-2 | mesajlar; bekletilenleri işle / at |
| `….raw-messages.read` | 2 | DL-2 | ham içerik (maskeli; mockup K-5) |
| `….exceptions.read` / `.update` | 1 | DL-2 | istisna kutusu; yeniden işle, yok say, sorumlu ata |
| `….instructions.read` / `.create` | 1 | DL-6 / DL-7 | talimatlar; giriş ön bildirimi |
| `….reconciliation.read` / `.update` / `.export` | 1 / 1 / 2 | DL-8 | mutabakat; CSV |
| `….settings.read` / `.configure` | 1 / 3 | DL-1 | eşik ayarları (M-2) |

**Gelen uç:** `[AllowAnonymous]` + imza süzgeci `[ThirdPartySignature]`; Gateway muafiyeti X-19. **0179 mimari bekçisi (AC-7):** (a) `Controllers.ThirdPartyLogistics`
namespace'inde `AllowAnonymous` yalnız `ThirdPartyInboundController.Receive`'de ve imza süzgeciyle; (b) bu namespace'te **sınıf düzeyinde `[HasPermission]` /
`[HasAnyPermission]` yasak** — InventoryService `AuthorizationGuardTests` sınıf düzeyi anahtarı da kabul eder
(`services/Diten.InventoryService/tests/Diten.InventoryService.Tests/Architecture/AuthorizationGuardTests.cs:53-54`), bu yüzden S-7a yalnız eylem düzeyi kuralıyla
anlamlıdır (F-12).

**Politika ve aktör:** `[Authorize]` + her yönetim eyleminde `[HasPermission(ThirdPartyLogisticsPermissions.X)]`. Aktör `tenant_user`; gelen uçta
`channel:{channelId}`; işçilerde sistem. **Rol şablonu önerisi** (X-16, Q-13 — Ali): Entegrasyon sorumlusu = `partners.*` + `channels.*` + `mappings.*` +
`messages.*` + `raw-messages.read` + `exceptions.*` + `settings.*`; Stok / mutabakat sorumlusu = `partners.read` + `reconciliation.*` + `exceptions.read`; Depo
yöneticisi = `partners.read` + `messages.read` + `exceptions.read` + `reconciliation.read` + `instructions.*`.

**Manifest:** ModuleCode `THIRD-PARTY-LOGISTICS`, SortOrder 179; Pages `TPLHEALTH`, `TPLCHANNELS`, `TPLMAPPINGS`, `TPLMESSAGES`, `TPLEXCEPTIONS`, `TPLRECON`
(DL-8), `TPLSETTINGS`. Manifest yalnız `warehouse.third-party-logistics.*` bildirir.

## 15. Gateway / API Routing Decision
- **Rota:** uçlar `/api/warehouse/third-party-logistics/…` — mevcut `/api/warehouse/{everything}` rotası (`gateway/Diten.ApiGateway/ocelot.json:1153-1171`;
  taban `:1134-1142`). **`ocelot.json` değişmez.**
- **Kiracı ara katmanı (F-2 — r1'in "JWT'siz istek Gateway'den geçer" iddiası yalnız `ocelot.json`'a dayanıyordu ve yanlıştı):** `TenantResolutionMiddleware`
  (Program.cs `:186`) kiracı sinyali olmayan, public olmayan isteğe 400 "Missing Tenant" döner (`TenantResolutionMiddleware.cs:213-226`); gelen yol public
  listede değildir (`:438-449`); yerelde Development bypass'ı (`:595`) bunu gizler. **Karar:** X-19 — yalnız `/api/warehouse/third-party-logistics/inbound`
  öneki `IsNonTenantPath`'e eklenir (`:430-436`, erken dönüş `:35` — bypass ve başlık yazımından önce; r3, F-14; integration-agent). Kiracı servis tarafında
  kanal anahtarından çözülür; `X-Tenant-Id` kuralı §8.4. Ölçüm **bypass kapalı ve açıkken** (AC-63).
- **0178 Gateway testinin uç sayısı (X-8):** bugün 87 (`Mod0178WarehouseRouteTests.cs:103`); uç ekleyen her dilim aynı commit'te günceller.
- **0179 uç kapsama testi** `gateway/Diten.ApiGateway.Tests/Mod0179ThirdPartyRouteTests.cs` (DL-9; muafiyet vakası DL-5'te): üretim `ocelot.json`'u ve controller
  kaynağını okur; gelen ucun `Authorization` olmadan iletildiğini, `X-TPL-*` başlıklarının ulaştığını, başka 0179 yolunun public olmadığını ölçer.
- **Dış erişim** (TLS, IP izin listesi, hız sınırı) dağıtım işi — FU-0179-EDGE.
- İletilen başlıklar: `Authorization`, `X-Legal-Entity-Id`, `X-Correlation-Id`, `Idempotency-Key`, `If-Match`, `Accept-Language`, `X-TPL-Timestamp`, `X-TPL-Signature`,
  `X-TPL-Message-Id`.

## 16. Acceptance Criteria

Her guard maddesi **üretim koduna karşı** ölçülür ve sabotaj kanıtı ister (bozma → kırmızı → geri alma; kabul kaydı komut + çıktı + geri almayı yazar).
AC → test → sabotaj eşlemesi §17.1. **Bekçi olmayan:** AC-27 (araç çıktısı), AC-33 (tarayıcı ölçümü).

**DL-1 — 3PL-BUNDLE taslağı + ortak, kanal, eşleme, eşik (arka uç; r1 DL-1a)**
- [ ] AC-1 (zorunlu) — 3PL-BUNDLE taslağı §8.16 içeriğiyle; sözleşme testi cevapları ve kanonik şemayı kapalı şemayla doğrular. S-1: zorunlu alanı kaldır → kırmızı.
- [ ] AC-2 (zorunlu) — Ortak bağlama FU01'de etkin + `ThirdPartyLogistics` rollü ortak ister (yoksa 422, bilinmeyen 404, erişilemez 503; DL-1…DL-3'te sahte port); `PartnerCode` kopyalanır; varlıkta ad / adres / iletişim / vergi no yok (yansıma). S-2a: rol denetimini kaldır → kırmızı; S-2b: varlığa `Name` ekle → mimari test kırmızı.
- [ ] AC-3 (zorunlu) — Depo kuralı: `external-integrated` değil 422; başka etkin ortağın deposu 409; eşzamanlı iki bağlamada tam biri başarılı. S-3a: `OperatingMode` denetimini kaldır → kırmızı; S-3b: tekillik denetimini kaldır → görevli kanca vakası kırmızı.
- [ ] AC-4 (zorunlu) — Ortak ve kanal durum makineleri; gerekçesiz 400; geçersiz 409; silme yok; v1.0'da `File` / `Edi` kanal yaratma 422 `TPL_CHANNEL_TYPE_NOT_ENABLED`. S-4a: kanal kapatma gerekçe denetimini kaldır → kırmızı; S-4b: tür kapısını kaldır → `Edi` yaratma vakası kırmızı.
- [ ] AC-5 (zorunlu) — Kiracı / şirket izolasyonu: başka kiracı ve aynı kiracıda başka şirket 404. S-5a: `TenantId` yüklemini düşür → kırmızı; S-5b: `LegalEntityId` yüklemini düşür → kırmızı.
- [ ] AC-6 (zorunlu) — Gövde / sorguda `tenantId` / `legalEntityId` → 400. S-6: denetimi kaldır → kırmızı.
- [ ] AC-7 (zorunlu) — Her yönetim eylemi eylem düzeyinde `[HasPermission]`; namespace'te sınıf düzeyi izin özniteliği yok; `AllowAnonymous` yalnız adlı gelen eylemde ve imza süzgeciyle; manifest yalnız `warehouse.third-party-logistics.*`; X-9 dışlamasıyla manifest bütünlük testi yeşil + 0179'un kendi bütünlük vakası. S-7a: bir eylemden `HasPermission`'ı kaldır → `AuthorizationGuardTests` kırmızı; S-7b: bir yönetim eylemine `AllowAnonymous` ekle → 0179 bekçisi kırmızı; S-7c: manifestten anahtar kaldır → bütünlük vakası kırmızı; S-7d: bir 0179 controller'ına sınıf düzeyi `[HasPermission]` ekle → 0179 bekçisi kırmızı (F-12).
- [ ] AC-8 (zorunlu) — Idempotency ve If-Match; anahtar araması If-Match / durum / tekillikten önce. S-8a: anahtar aramasını tekillikten sonraya al → kırmızı; S-8b: If-Match denetimini kaldır → kırmızı.
- [ ] AC-9 (zorunlu) — Kanal DTO'su sır için yalnız `{ name, isSet, refSetAt }`; sahte sır sağlayıcısının nöbetçi değeri hiçbir cevapta, logda, merkezi kayıtta, idempotency deposunda, koleksiyonda yok (tarama). S-9a: DTO'ya çözülmüş değeri ekle → tarama kırmızı; S-9b: istisna metnine değeri yaz → tarama kırmızı.
- [ ] AC-11 (zorunlu) — Eşleme tekilliği iki yönde; pasif eşleme kullanılmaz. S-11: (ortak, tür, `OurRef`) tekilliğini kaldır → kırmızı.
- [ ] AC-19 (zorunlu) — Eşikler: varsayılanlar (Q-6), aralık dışı 400, `settings.configure` yoksa 403. S-19: aralık denetimini kaldır → kırmızı.
- [ ] AC-22 (zorunlu) — 0179 Persistence başka modül koleksiyonunda yalnız `Find`; `ThirdPartyLogisticsCollections.All` SSOR testinde (X-5); bakiye alanı yok. S-22a: başka modül koleksiyonuna `InsertOne` ekle → kırmızı; S-22b: koleksiyonu bildirimden çıkar → kırmızı.
- [ ] AC-23 (zorunlu) — PUT / DELETE yok. S-23: `[HttpDelete]` ekle → `LedgerImmutabilityArchitectureTests` kırmızı.
- [ ] AC-61 (zorunlu, F-6) — Bağlama, deponun tam bir etkin stoklanabilir alt lokasyonu olmasını ister; sıfır ya da iki → 422 `TPL_WAREHOUSE_STOCK_LOCATION`; bağlamadan sonra ikinci lokasyon açılırsa sağlık Hatalı. S-61: sayım denetimini "en az bir" yap → iki lokasyon vakası kırmızı.

**DL-2 — mesaj defteri, istisna kutusu, bekletilenler, 90 gün kuralı (arka uç; r1 DL-1b)**
- [ ] AC-10 (zorunlu) — Kapalı kanala gelen mesaj `Rejected` + `ChannelClosed`, işlenmez (M-16). S-10: kapalı kanal denetimini kaldır → kırmızı.
- [ ] AC-12 (zorunlu) — Aynı `DedupKey` → `Duplicate` + kendiliğinden `Ignored`; ikinci yan etki yok. S-12: tekrar denetimini kaldır → kırmızı.
- [ ] AC-13 (zorunlu) — Askıdaki ortak: gelen `Held` (+30 gün, sahte saat), giden `NotSent`; etkinleştirme kendiliğinden işlemez; geliş sırası ve `outOfOrder` onayı; askıdayken işleme 422 (Q-7); süresi dolan 422 ve silinmez. S-13a: etkinleştirmede kendiliğinden işle → kırmızı; S-13b: sıra denetimini kaldır → kırmızı.
- [ ] AC-14 (zorunlu) — İstisna yaşam döngüsü; yok sayma gerekçesiz 400. S-14: `Ignored` → `Open` geçişine izin ver → kırmızı.
- [ ] AC-15 (zorunlu) — Toplu yeniden işleme 1..200, aynı ortak + aynı tür; "eşleme ekle ve yeniden işle" yalnız aynı ortak + koddaki açıkları işler. S-15a: sınırı 1000 yap → 201 vakası kırmızı; S-15b: karışık denetimini kaldır → kırmızı; S-15c: ortak süzgecini kaldır → başka ortak vakası kırmızı.
- [ ] AC-16 (zorunlu) — Lot / SKT farkı `LotExpiryMismatch`; MOD-0174 koleksiyonlarında yazım yok; deftere yansımaz. S-16: 3PL SKT'sini lot kaydına yazan dal ekle → kırmızı.
- [ ] AC-17 (zorunlu) — `UnknownProductMapping`, `UnknownLot`, `QuantityMismatch`, `LateConfirmation`, `Format`, `Unsupported` ayrı vakalarla; metin 7 dilde koddan. S-17: miktar karşılaştırmasını kaldır → kırmızı.
- [ ] AC-18 (zorunlu) — Sağlık ve KPI §8.11 (sahte saat, eşik sınırında iki yön). S-18: rapor yaşı karşılaştırmasını ters çevir → kırmızı.
- [ ] AC-20 (zorunlu; komut ekleyen her dilimde — DL-1, DL-2, DL-5, DL-7, DL-8; r3, F-17 (c)) — Merkezi denetim: başarılı ve yazan komut 1 kayıt, replay 0, başarısız 0; X-6 sayıları aynı commit'te. S-20a: komuttan işareti kaldır → `CentralAuditDeclarationTests` kırmızı; S-20b: replay'de kayıt yazdır → kırmızı.
- [ ] AC-21 (zorunlu) — İstisna numarası `{ŞİRKET}-TPX-` + 6 hane; değişmez; eşzamanlı ikisi farklı. S-21: `TPX`'i `TypePrefixes`'ten çıkar → kırmızı.
- [ ] AC-24 (zorunlu) — Ham içerik `raw-messages.read` ister; okuma maskesi; imza / sır alanı yok. S-24a: izni `messages.read`'e düşür → kırmızı; S-24b: maskeyi kaldır → kırmızı.
- [ ] AC-25 (zorunlu; uç ekleyen her dilimde — DL-1, DL-2, DL-5…DL-8; r3, F-17 (c)) — `Mod0178WarehouseRouteTests` sayı satırı dilimin uçlarıyla aynı commit'te (X-8). S-25: sayıyı eski değere döndür → kırmızı.
- [ ] AC-60 (zorunlu, F-5) — 90 gün (sahte saat): 89. günde yük değişmez; 90. günde süpürücü kişisel veri alanlarını kalıcı kaldırır, `PiiPurgedAt` + `CurrentSha256` yazılır, `OriginalSha256` / kanonik özet / zarf aynı kalır; ikinci koşu yazmaz; temizlenmiş mesaj yeniden işlenebilir; merkezi kayıt kişisel veri taşımaz. S-60a: süreyi 900 gün yap → 90. gün vakası kırmızı; S-60b: temizlemede kanonik özeti de sil → kırmızı; S-60c: temizlemeyi yalnız okuma maskesine bırak (depoya yazma) → depo taraması kırmızı.

**DL-3 — Web ekranları (sağlık, ortak ayrıntısı, kanallar, eşlemeler, mesajlar, bekletilenler, istisna kutusu, ayarlar; r1 DL-2)**
- [ ] AC-26 (zorunlu) — Her sayfa görünümünde `Layout = "_LayoutTenantShell";`. S-26: satırı kaldır → kırmızı.
- [ ] AC-27 — `verify_datatable_page.py` her liste ekranında PASS ya da sapmalar sahibine soruldu (bekçi değil).
- [ ] AC-28 (zorunlu) — RESX parite 7 dil; Almanca yok. S-28: `zh`'den anahtar sil → kırmızı.
- [ ] AC-29 (zorunlu) — UAS-001. S-29: yetkisiz dalda iskelet çiz → kırmızı.
- [ ] AC-30 (zorunlu) — İzne göre çizim; sır değeri hiçbir HTML'de yok; v1.0'da dosya / EDI türü devre dışı. S-30: ham bölümü izinsiz çiz → kırmızı.
- [ ] AC-31 (zorunlu) — Toplu seçim > 200 uyarı + ilk 200; karışık seçimde düğme devre dışı. S-31: JS sınırını kaldır → vitest kırmızı.
- [ ] AC-32 (zorunlu) — Sayfa yardımı her sayfada; tur yok; sır cümlesi `TPLCHANNELS`'da. S-32: yardım anahtarını kaldır → kırmızı.
- [ ] AC-33 — 375 px, durumlar, RTL (bekçi değil).
- [ ] AC-34 (zorunlu) — (a) **DL-3:** ortak formunda ad / adres / iletişim girdisi yok; ayrıntıdaki iş ortağı bilgisi salt okunur — Web testi FU01 okuma ucunu **sahte proxy** ile taklit eder (proxy controller'ın Gateway istemcisi test kurulumunda sahte HTTP işleyiciyle değiştirilir; yeni uç yok); (b) **DL-4 kabulünde (r3, F-16):** seçim listesi yalnız FU01'in 3PL rollü etkin ortaklarını gösterir — gerçek FU01 ucuyla. S-34: forma ad girdisi ekle → (a) kırmızı; S-34b: proxy'de rol / durum süzgecini kaldır → (b) kırmızı.
- [ ] AC-62 (zorunlu, F-8) — "Ücret mutabakatı — v2 · planlı" notu ortak ayrıntısı Genel sekmesinde ve sağlık şeridinde 7 dilde; ücret için bağlantı, düğme, menü öğesi ve izin yok. S-62a: notu kaldır → view testi kırmızı; S-62b: nota bağlantı ekle → view testi kırmızı.

**DL-4 — iş ortağı okuma bağlantısı (MOD-0049-FU01 istemcisi; kapı: FU01 API'si `main`'de)**
- [ ] AC-35 (zorunlu) — `IBusinessPartnerDirectory` gerçek istemcisi FU01 sözleşmesini okur (liste: rol + durum süzgeci; ayrıntı); sözleşme testi FU01 şemasına karşı kapalı. S-35a: rol süzgecini istemciden kaldır → kırmızı; S-35b: şemadan alan kaldır → sözleşme testi kırmızı.
- [ ] AC-36 (zorunlu) — Başka kiracının iş ortağı 404 (FU01 kiracı yüklemi okuma tarafında doğrulanır); FU01 erişilemez → oluşturma 503, liste kod + rozet. S-36: istemcide kiracı başlığını iletme → kırmızı.
- [ ] AC-37 (zorunlu) — İş ortağı pasifleşir / rolü kalkarsa: bağlantı korunur, giden mesaj `NotSent` + `TPL_PARTNER_MASTER_INACTIVE`, yeni bağlama 422. S-37: pasif denetimini kaldır → kırmızı.
- [ ] AC-38 (zorunlu) — MOD-0179 MdmService'e hiçbir yazma isteği göndermez (istemci tipinde yalnız GET; mimari tarama). S-38: istemciye POST metodu ekle → tarama kırmızı.

**DL-5 — API kanalı + sır referansı (kasa FU'sunu beklemez — F-10) + Gateway muafiyeti**
- [ ] AC-39 (zorunlu) — `SetThirdPartyChannelSecretRefCommand` yalnız `secretName` alır, referansı sunucu `ThirdPartyLogistics:{TenantId}:{ChannelId}:{SecretName}` olarak üretir; gövdede `ref` / değer alanı 400; "ayarlı" bilgisi çözümden; nöbetçi değer taraması (gerçek `ISecretsProvider`). **r3 (F-13) — başka kiracı referansı vakası:** B kiracısının kanalına ait anahtar yapılandırmada dolu iken (a) A kiracısının isteğinde `ref = ThirdPartyLogistics:{B}:{kanalB}:ApiKey` gönderilir → 400, kayıt yok; (b) A kiracısının kanal kaydına bu referans depoya doğrudan yazılır (test kurulumu) → çözücü çözmez, ekran "Ayarlı değil", gönderici A'nın `OutboundBaseUrl`'ine hiçbir kimlik bilgisi göndermez (sahte HTTP işleyicide B'nin nöbetçi değeri yok). S-39a: gövdede `ref` alanı kabul eden dalı ekle → (a) vakası kırmızı; S-39b: çözülen değeri kanala yaz → tarama kırmızı; S-39c: çözücüyü saklanan `Ref` dizgesini doğrudan kullanacak hale getir (yeniden kurmayı kaldır) → (b) vakası kırmızı.
- [ ] AC-40 (zorunlu) — Gelen uç: doğru imza 202 + mesaj; yanlış imza, bilinmeyen anahtar, ± 5 dk dışı, **A kiracısının `keyId`'si + B kiracısının sırrıyla imza**, anahtarın kiracısından farklı `X-Tenant-Id` → 401 ve sıfır yazım; aynı `X-TPL-Message-Id` → `Duplicate`. S-40a: imza karşılaştırmasını kaldır → kırmızı; S-40b: zaman penceresini kaldır → kırmızı; S-40c: imza hatasında mesaj kaydı yaz → kırmızı; S-40d: `FixedTimeEquals`'ı `SequenceEqual` ile değiştir → kaynak bekçisi kırmızı (F-9 (b)); S-40e: kiracıyı `X-Tenant-Id`'den seç → uyuşmazlık vakası kırmızı.
- [ ] AC-41 (zorunlu) — Gönderici: 5 deneme, üstel bekleme, sonra `Failed` + `ConnectionAuth`; askı / kapalı kanal `NotSent`; özel ağ adresine ret. S-41a: askı denetimini kaldır → kırmızı; S-41b: SSRF denetimini kaldır → kırmızı.
- [ ] AC-42 (zorunlu) — "Bağlantıyı sına" sonucu sır içermez; teknik alanlar `Version`'ı artırmaz. S-42: sonuca yanıt gövdesini ekle → tarama kırmızı.
- [ ] AC-63 (zorunlu, F-2; r3, F-14) — Gateway: (a) **Development bypass kapalıyken** (`TenantResolution:DevBypassEnabled = false`) tokensız, `X-Tenant-Id`'siz geçerli imzalı istek servise ulaşır ve 202 alır; aynı istek başka bir 0179 yoluna (ör. `…/partners`) 400 "Missing Tenant" alır; (b) **bypass açıkken** (`DevBypassEnabled = true`, `DevBypassTenantId` dolu) gelen yola giden istekte servise ulaşan `X-Tenant-Id` yoktur (Gateway yazmaz) ve istek 202 alır; 3PL'in gönderdiği `X-Tenant-Id` değiştirilmeden iletilir. S-63: X-19 önekini `IsNonTenantPath`'ten çıkar → (a) 400, kırmızı; S-63b: öneki `IsNonTenantPath` yerine `IsPublicEndpoint`'e koy → (b) vakasında başlığa bypass kiracısı yazılır, servis 401, kırmızı (ikisi de geri alınır).

**DL-6 — çıkış akışı, tek model (MOD-0178 / MOD-0183; F-1)**
- [ ] AC-43 (zorunlu) — Dış depoda allocation toplama görevi açmaz, outbound `Allocated` kalır; **dış release** `Allocated` outbound'u kabul eder ve tek işlemde `ReadyToShip` + `OutboundShipmentReady` outbox + talimat + giden mesaj yazar (talimat yazımı başarısızsa hiçbiri yok); iç depoda release yalnız `Packed` (bugünkü davranış); **r3 (F-15):** tahsisli lotu geri çağrılmış dış outbound'un release'i 422 `LOT_UNDER_RECALL` (ayrıntıda `allocSeq`, `lotNumber`), `ReadyToShip` / outbox / talimat / mesaj yazılmaz; aynı vaka Blocked ve Expired lotla; yerleştirme 422 `WMS_EXTERNAL_WAREHOUSE`; `INVT/Warehouse/**` ve `INVT/CycleCount/**` diff'i boş. S-43a: depo modu denetimini kaldır → toplama görevi açılır, kırmızı; S-43b: talimatı işlem dışına al → geri alma vakası kırmızı; S-43c: iç depoda da `Allocated` release'e izin ver → iç depo vakası kırmızı; S-43d: dış release'te ön denetimleri `picked` kümesinde bırak (tahsislere geçmeyi kaldır) → geri çağrılmış lot vakası 200 döner, kırmızı.
- [ ] AC-44 (zorunlu) — Sevk onayı (outbound `ReadyToShip`) → X-12 yolu (MOD-0183 dispatch, satırlar = 3PL lot / miktarı; sahte istemci + pilotta gerçek) → MOD-0183 `GOODS_ISSUE_SALES` → 0178 sync-dispatch `Shipped`; dış outbound'da kısa sevk planı tahsislerden; tahsis lotundan farklı lot `LotExpiryMismatch`, dispatch yok; 0179 hiçbir hareket yazmaz (tarama). S-44a: lot karşılaştırmasını kaldır → kırmızı; S-44b: 0179'dan `GOODS_ISSUE_SALES` post et → tarama kırmızı; S-44c: sync-dispatch'te dış outbound için de toplama görevi tabanını kullan → kısa sevk vakası kırmızı.
- [ ] AC-45 (zorunlu) — Fazla onay `QuantityMismatch`; outbound `ReadyToShip` değilken ya da talimat kapanmışken onay `LateConfirmation`; ikisinde dispatch yok. S-45: durum denetimini kaldır → kırmızı.

**DL-7 — giriş akışı (MOD-0142)**
- [ ] AC-46 (zorunlu) — Ön bildirim PO satırlarından; kapalı PO 422; aynı PO + depo için ikinci etkin ön bildirim 409. S-46: tekillik index'ini kaldır → kırmızı.
- [ ] AC-47 (zorunlu) — Teslim onayı → X-13 yolu → GRN (`locationId` = tek stoklanabilir lokasyon, `sourceSystem`, `externalRef`, Idempotency-Key = mesaj kimliği); yeniden işleme ikinci GRN üretmez; 0179 `GOODS_RECEIPT_PO` yazmaz; lot farkı `LotExpiryMismatch`. S-47a: Idempotency-Key'i rastgele üret → kırmızı; S-47b: lot karşılaştırmasını kaldır → kırmızı.

**DL-8 — stok raporu + mutabakat masası + performans (arka uç ve ekran)**
- [ ] AC-48 (zorunlu) — Fark, yoldaki (M-4, işaretli, `ReportedAt` öncesi), açıklanmamış formülü. S-48a: yoldakinin işaretini ters çevir → kırmızı; S-48b: `ReportedAt` süzgecini kaldır → kırmızı.
- [ ] AC-49 (zorunlu) — Satır durum makinesi; Çözüldü gerekçe ister; stok hareketi yazılmaz; **"Düzeltme öner" düğmesi yok** (Q-4 — view testi). S-49a: gerekçe denetimini kaldır → kırmızı; S-49b: düğmeyi ekle → view testi kırmızı.
- [ ] AC-50 (zorunlu) — CSV `reconciliation.export` ister; formül enjeksiyonu kaçışı. S-50: kaçışı kaldır → kırmızı.
- [ ] AC-51 (zorunlu) — Performans metrikleri (30 gün sınırı). S-51: pencereyi 31 gün yap → kırmızı.
- [ ] AC-52 (zorunlu) — Gecikmiş rapor bandı; mutabakat ekranı Layout, 7 dil, yardım, UAS-001. S-52: bant koşulunu kaldır → kırmızı.

**DL-9 — Gateway kapsama testi (integration-agent; v1.0'ın son dilimi)**
- [ ] AC-59 (zorunlu) — `Mod0179ThirdPartyRouteTests` 0179 uçlarının tamamını üretim `ocelot.json`'unda eşler; gelen uç `Authorization`'sız iletilir, `X-TPL-*` başlıkları ulaşır; yalnız gelen yol public. S-59: `ocelot.json`'da `/api/warehouse/{everything}`'den `POST`'u geçici çıkar → kırmızı (geri alınır). Ara katman muafiyeti S-63 ile ayrıca ölçülür (F-12).

**DL-10 — dosya kanalı, SFTP + CSV / XML (v1.1)**
- [ ] AC-53 (zorunlu) — SFTP okuyucu desene uyan dosyayı okur, arşive taşır, silmez; dosya adı + sha256 tekrarı `Duplicate`; CSV / XML kanonik modele (test kabında SFTP). S-53a: arşive taşımayı kaldır → kırmızı; S-53b: CSV başlık denetimini kaldır → kırmızı.
- [ ] AC-54 (zorunlu) — Ana makine anahtarı parmak izi; uyuşmazlıkta `ConnectionAuth`, okuma yok. S-54: denetimi kapat → kırmızı.
- [ ] AC-55 (zorunlu) — Giden dosya `.tmp` + yeniden adlandırma. S-55: doğrudan son adla yaz → kırmızı.

**DL-11 — EDI kanalı, EDIFACT + X12, SFTP taşıması (v1.1)**
- [ ] AC-56 (zorunlu) — EDIFACT kümesi (Q-3) ayrıştırılır / üretilir; UNB / UNZ, UNH / UNT sayaçları. S-56: UNT denetimini kaldır → kırmızı.
- [ ] AC-57 (zorunlu) — X12 kümesi 940, 943, 944, 945, 846; ISA / IEA, GS / GE, ST / SE. S-57: SE denetimini kaldır → kırmızı.
- [ ] AC-58 (zorunlu) — Bir ortakta EDIFACT ve X12 kanalı birlikte (T-1); yanlış kanala gelen depo kodu `Format`. S-58: kanal-depo denetimini kaldır → kırmızı.

## 17. Test Expectations
- **Birim (saf):** durum makineleri; sağlık; yoldaki / fark formülü; maskeleme ve 90 gün temizleme; imza doğrulama; sır referansı deseni; SSRF; CSV kaçışı; v1.1 EDI sayaçları.
- **Entegrasyon (Mongo replika seti):** InventoryService test paketi `INVENTORY_TEST_MONGO` yoksa kendi replika setini başlatır; DL-1…DL-3 sahte iş ortağı portuyla;
  DL-4 FU01 sözleşmesine karşı; DL-5 gerçek `ISecretsProvider` (yapılandırma sağlayıcısı) ile; DL-6 gerçek MOD-0178 işleyicileri + sahte MOD-0183 istemcisi (pilotta
  gerçek); DL-7 sahte GRN / PO (pilotta gerçek); DL-10 test kabında SFTP; eşzamanlılık görevli kancayla; zaman sahte saatle.
- **Mimari:** AC-7 (`AuthorizationGuardTests` değişmeden + 0179 bekçisi: `AllowAnonymous` listesi, sınıf düzeyi izin yasağı, manifest bütünlüğü), AC-2, AC-9 / AC-39
  (sır taraması), AC-20 (X-6), AC-22 (X-5), AC-23, AC-38 (MDM'ye yazma yok), AC-40 (sabit süreli karşılaştırma kaynak bekçisi). Servise dokunulduğunda **bütün**
  InventoryService paketi koşar.
- **Gateway:** AC-63 (bypass kapalı muafiyet), AC-59, AC-25 (X-8).
- **MOD-0178 davranışı:** X-10'un ölçümü 0179 test ağacında; 0178 test dosyaları değişmez (AC-43).
- **Frontend:** AC-26…AC-34 (AC-34 (b) DL-4'te — r3, F-16), AC-62; Web testlerinde FU01 okuma ucu sahte proxy ile; tarayıcı smoke (E3) **iki aşamada (F-7):** DL-3 kabulünde sahte iş ortağı + sahte mesajla (ortak bağla → kanal tanımla →
  istisna kutusu → eşleme ekle ve yeniden işle); DL-5 kabulünde gerçek imzalı gelen mesajla tekrar.
- **Build:** `Diten.InventoryService`, `Diten.Web`; Gateway test projesi uç ekleyen her dilimde, DL-5 ve DL-9'da.
- **Sözleşme:** 3PL-BUNDLE (AC-1), FU01 okuma sözleşmesi (AC-35), WMS-BUNDLE eklemeli sürümü (X-11), v1.1 EDI örnek değişimleri.

### 17.1 AC → test → sabotaj eşlemesi
| AC | Test türü | Sabotaj |
|---|---|---|
| AC-1 | sözleşme | S-1 |
| AC-2 | entegrasyon + mimari | S-2a, S-2b |
| AC-3 | entegrasyon (görevli kanca) | S-3a, S-3b |
| AC-4 | birim + entegrasyon | S-4a, S-4b |
| AC-5 | entegrasyon | S-5a, S-5b |
| AC-6 | entegrasyon | S-6 |
| AC-7 | mimari | S-7a, S-7b, S-7c, S-7d |
| AC-8 | entegrasyon | S-8a, S-8b |
| AC-9 | entegrasyon (tarama) | S-9a, S-9b |
| AC-10 | entegrasyon | S-10 |
| AC-11 | entegrasyon | S-11 |
| AC-12 | entegrasyon | S-12 |
| AC-13 | entegrasyon (sahte saat) | S-13a, S-13b |
| AC-14 | birim + entegrasyon | S-14 |
| AC-15 | entegrasyon | S-15a, S-15b, S-15c |
| AC-16 | entegrasyon (depo taraması) | S-16 |
| AC-17 | entegrasyon | S-17 |
| AC-18 | birim (sahte saat) | S-18 |
| AC-19 | entegrasyon | S-19 |
| AC-20 | mimari + entegrasyon | S-20a, S-20b |
| AC-21 | entegrasyon (görevli kanca) | S-21 |
| AC-22 | mimari | S-22a, S-22b |
| AC-23 | mimari | S-23 |
| AC-24 | entegrasyon | S-24a, S-24b |
| AC-25 | Gateway testi | S-25 |
| AC-26 | Web testi | S-26 |
| AC-27 | araç (bekçi değil; sabotaj yok) | — |
| AC-28 | Web testi | S-28 |
| AC-29 | Web testi | S-29 |
| AC-30 | view + vitest | S-30 |
| AC-31 | vitest | S-31 |
| AC-32 | Web testi | S-32 |
| AC-33 | tarayıcı (bekçi değil; sabotaj yok) | — |
| AC-34 | view testi (a, sahte proxy) + entegrasyon (b, DL-4) | S-34, S-34b |
| AC-35 | sözleşme + entegrasyon | S-35a, S-35b |
| AC-36 | entegrasyon | S-36 |
| AC-37 | entegrasyon | S-37 |
| AC-38 | mimari | S-38 |
| AC-39 | entegrasyon (gerçek sağlayıcı, iki kiracı) | S-39a, S-39b, S-39c |
| AC-40 | entegrasyon (sahte saat) + kaynak bekçisi | S-40a, S-40b, S-40c, S-40d, S-40e |
| AC-41 | entegrasyon (sahte saat) | S-41a, S-41b |
| AC-42 | entegrasyon | S-42 |
| AC-43 | entegrasyon (gerçek 0178) + `git diff` | S-43a, S-43b, S-43c, S-43d |
| AC-44 | entegrasyon (gerçek 0178) + tarama | S-44a, S-44b, S-44c |
| AC-45 | entegrasyon | S-45 |
| AC-46 | entegrasyon | S-46 |
| AC-47 | entegrasyon | S-47a, S-47b |
| AC-48 | birim + entegrasyon | S-48a, S-48b |
| AC-49 | entegrasyon + view | S-49a, S-49b |
| AC-50 | birim | S-50 |
| AC-51 | birim (sahte saat) | S-51 |
| AC-52 | view testi | S-52 |
| AC-53 | entegrasyon (SFTP kabı) | S-53a, S-53b |
| AC-54 | entegrasyon (SFTP kabı) | S-54 |
| AC-55 | entegrasyon (SFTP kabı) | S-55 |
| AC-56 | birim + sözleşme | S-56 |
| AC-57 | birim + sözleşme | S-57 |
| AC-58 | entegrasyon | S-58 |
| AC-59 | Gateway testi | S-59 |
| AC-60 | entegrasyon (sahte saat, depo taraması) | S-60a, S-60b, S-60c |
| AC-61 | entegrasyon | S-61 |
| AC-62 | view testi | S-62a, S-62b |
| AC-63 | Gateway testi (bypass kapalı ve açık) | S-63, S-63b |

## 18. Ready-for-dev Checklist
- [x] Golden Reference Slim; sapmalar G-0179-01 / G-0179-02 (CT kabulü Q-17)
- [x] Frontmatter zorunlu alanları dolu
- [x] `_LayoutTenantShell` açıkça (§9, AC-26 + S-26)
- [x] Backend / Frontend File Convention (§10, §11)
- [x] Validation Rules her alan için (§12); Failure Path ≥ 4 senaryo (§13)
- [x] Authorization Convention (§14); Gateway kararı ve ara katman muafiyeti (§15, X-19, AC-63)
- [x] AC test edilebilir; her bekçi sabotajlı (§16, §17.1)
- [x] Audited Events her yazma komutunu tam bir kez içeriyor (§21)
- [x] Sayfa yardımı her sayfa için (§25); tur yok
- [x] Sahip kararları: T-1…T-6, M-2, M-4, M-6, M-7, M-15, M-16; **Q-1…Q-9 (karar)**; r1 inceleme kararları F-5, F-8, F-10, F-11; **Q-10 = MOD-0049-FU01** —
  kararlar 01 `:33-55`
- [x] r1 bağımsız inceleme (CT-APR-02) — `docs/records/reviews/MOD-0179/2026-11-13-aca86685.md`, KOŞULLU; F-1…F-12 r2'de kapandı (§19.6)
- [ ] **`.antigravity/**` kapısı — paket onayından ÖNCE, AÇIK:** PKS-001 `warehouse.third-party-logistics.*` (Q-1), RCS-001 `TPX`; aynı adımda X-17, X-18. CT hazırlar,
  Beste onaylar; bu paket `.antigravity/**`'a yazmaz
- [ ] r2 bağımsız yeniden inceleme (CT-APR-02) ve onay satırı (CT-APR-01) — **onaylayan Ali (GitHub `GMG-ERP`)**; yazan ≠ onaylayan
- [ ] Ali kararları: Q-12 (servis kimliği — DL-6 / DL-7 kapısı), Q-13 (rol şablonu — DL-3 kabul kapısı); Q-10'un Ali'ye bilgilendirilmesi (kararlar 01 `:48`)
- [ ] CT kararları: Q-14 (DL-2 kapısı), Q-15, Q-17 (DL-3 kapısı)
- [ ] Başka ekip: Q-18 (MOD-0183 sahibi — DL-6 kapısı), Q-19 (procurement — DL-7 kapısı)
- [ ] MOD-0049-FU01 paketi onaylı ve okuma API'si `main`'de — DL-4 kapısı
- [ ] XMC-001 satırları (X-4…X-16, X-19) sahiplerinden onay aldı — ilgili dilim başlamadan
- [ ] 3PL-BUNDLE taslağı (DL-1 ilk adımı); dondurma DL-5'ten önce

## 19. Implementation Notes

### 19.1 Okunan kaynaklar
- Sahip kararları 01: `docs/records/decisions/2026-11/mod0179-3pl-owner-decisions-01.md` (tamamı; r2 girdisi `:33-55`).
- İnceleme r1: `docs/records/reviews/MOD-0179/2026-11-13-aca86685.md` (tamamı).
- Brief: `execution/domains/supply-chain-execution/work-packs/mockups/3pl-integration/BRIEF-MOD-0179-3pl-integration.md`.
- Mockup tur 1: depoda değil (Beste'nin indirmeler klasöründe; 2 194 123 bayt; sha256 `cee456d114387fe742837610cdbc24140e1ca40c86af1031b0b38c5a7e2ce0d8`).
- Registry: `execution/registries/module-id-registry.md:160-161` (MOD-0049, MOD-0049-FU01; r3, F-17 (b)) ve `:70` (MOD-0012-FU01).
- Kod (r2 ölçümü, HEAD `70cb6ca01`): `OutboundFlowHandlers.cs` (`:247`, `:281`, `:378-411`, `:447`, `:477`, `:509-511`, `:525`), `OutboundAllocationService.cs:259`,
  `TenantResolutionMiddleware.cs` (`:213-226`, `:238`, `:438-449`, `:595`), gateway `Program.cs:186`, `InventoryScopeResolutionMiddleware.cs:76-81`,
  `SecretReferenceResolver.cs:22-55`, `Mod0178WarehouseRouteTests.cs:103`, `CentralAuditDeclarationTests.cs:55`, `:134`, `WarehouseRecordCodeRules.cs:29-31`,
  `InventoryLedgerManifestCompletenessTests.cs:213-225`, Inventory `Program.cs:48-65`, `SingleSystemOfRecordArchitectureTests.cs:120`, `AuthorizationGuardTests.cs:53-54`,
  `LocationHierarchyRules.cs:48-49`.
- Emsal paketler: MOD-0182, MOD-0181, MOD-0180, MOD-0178 (DL-1a / 1b bölmesi ve yeniden numaralandırma emsali), MOD-0183, MOD-0142.

### 19.2 Ölçülen gerçekler
- MDM'de iş ortağı varlığı yok (r1); ana veri MOD-0049-FU01 (Q-10).
- **MOD-0178 release yalnız `Packed` kabul eder** (`OutboundFlowHandlers.cs:281`) ve aynı işlemde `ReadyToShip` + `OutboundShipmentReady` outbox yazar (`:378-411`);
  toplama görevi yoksa outbound `Packed`'e ulaşmaz — r1'in "sevk onayıyla `Allocated` → `ReadyToShip`" modeli bu yüzden çalışmıyordu (F-1).
- **Gateway kiracı ara katmanı** tokensız, kiracı sinyalsiz, public olmayan isteğe 400 döner (`TenantResolutionMiddleware.cs:213-226`); Development bypass'ı
  (`:595`) yerelde gizler (F-2). InventoryService kapsam ara katmanı kimliği doğrulanmamış isteği geçirir (`InventoryScopeResolutionMiddleware.cs:76-81`).
- **Bekçi değerleri r2 (F-3):** Gateway 87 (`:103`), merkezi denetim 123 (`:55`) / 15 (`:134`), `TypePrefixes` altı önek (`:31`, `WAV` `:29`), dışlama yedi satır
  (`:213-225`), manifest bloğu `Program.cs:48-65`, SSOR son bildirim `:120`. r1 değerleri (73, 117 / 14, beş önek, altı satır) `0e8e29aab`'daydı; arada MOD-0180 DL-1 girdi.
- Platform `TenantMessagingSettings.CredentialSecretRef` yalnız referans tutar; `SecretReferenceResolver` değeri salt okunur `ISecretsProvider`'dan çözer (F-10).
  InventoryService zaten `Diten.BuildingBlocks.Security.Secrets`'a başvurur (`Program.cs:3`).
- Stoklanabilir yalnız `Bin` / `StorageLocation` (`LocationHierarchyRules.cs:48-49`); GRN başlığı `locationId` ister (MOD-0142 paketi `:68`).
- `AuthorizationGuardTests` sınıf düzeyi izni kabul eder (`:53`) ve `AllowAnonymous`'u geçirir (`:54`) (F-12).

### 19.3 Çıkarımlar
- Defterde "an itibarıyla" okuma ölçülmedi — DL-8 başında.
- Dış depoda 0178 allocation'ın tek stoklanabilir lokasyonu seçtiği ölçülmedi — DL-6 başında (§8.10.1).
- MOD-0183 dispatch'in servis kimliğiyle çağrılabilirliği ölçülmedi — Q-18.
- `BRIDGE-BASE` karşılığı bulunmadı — Q-15.

### 19.4 Kararlar (r2) ve açık sorular
**Kapanan (sahip kararı, kararlar 01 `:33-55`):** Q-1 `warehouse.third-party-logistics.*`; Q-2 EDI taşıması SFTP (AS2 FU-0179-AS2); Q-3 EDIFACT INSDES / DESADV
(iki yön) / RECADV / INVRPT, X12 940 / 943 / 944 / 945 / 846, CONTRL / 997 yok, dar kendi ayrıştırıcı; Q-4 "Düzeltme öner" yok; Q-5 `/Warehouse/ThirdParty*`;
Q-6 eşikler 120 dk / 240 dk / 26 sa / %1; Q-7 askıdayken işlenmez; Q-8 ön bildirim elle; Q-9 tek stoklanabilir lokasyon; Q-10 MOD-0049-FU01; F-5 90 gün; F-8
"v2 · planlı"; F-10 sır referansı ara çözümü; F-11 v1.0 yalnız API. **Taşınan:** Q-11 (MDM iş ortağı öneki) → MOD-0049-FU01 paketi.

**Açık — Ali:** Q-12 servis kimliği (Inventory → SupplyChain dispatch, → Procurement GRN); Q-13 rol şablonları.
**Açık — CT:** Q-14 mesaj komutlarının denetim yolu (b / c); Q-15 Blueprint sapmaları ve `BRIDGE-BASE`; Q-16 kiracı sır kasası FU'su — kimliği artık
MOD-0012-FU01 (registry `:70`, paket taslak); sonraki adım, v1.0 kapısı değil (F-10; registry notu da öyle — r3, F-17 (a)); Q-17 Golden sapmaları.
**Açık — başka ekip:** Q-18 MOD-0183 sahibi (dispatch yolu); Q-19 procurement (GRN yolu, miktar farkında GRN açılmaması).

### 19.5 Mockup v1 → paket farkları
| Mockup | Paket | Kaynak |
|---|---|---|
| Ücret ekranı, izni, itiraz | yok; yalnız "v2 · planlı" notu (§8.14) | T-2, F-8 |
| 7 dil (DE dahil) | en, tr, fr, es, zh, ar, ru | M-7 |
| "Ortak ekle" ortak yaratır | MOD-0049-FU01'den seçim, salt okunur | T-5, Q-10 |
| Eşik salt metin | ayrı ayar sayfası | M-2 |
| Kanal kapatma gerekçesiz | gerekçe; kapalı kanala gelen ret + istisna | M-16 |
| Toplu yeniden işleme sınırsız | ≤ 200 + uyarı | M-6 |
| Sır "ayarlı · son değişim" | referans + "ayarlı"; değer işletimde | T-4, F-10 |
| EDI AS2 kanalı | v1.0'da yok; v1.1 EDI SFTP üzerinden | F-11, Q-2 |
| Rota `/Integrations/…` | `/Warehouse/ThirdParty*` | Q-5 |
| "Düzeltme öner" | yok | Q-4 |
| Ham içerik süresiz | 90 gün sonra kişisel veri kaldırılır | F-5 |

### 19.6 r1 → r2 kapanış tablosu
| Bulgu | Kapanış | Bölüm / AC |
|---|---|---|
| F-1 (orta) | Dış depo çıkışı tek model: allocation görevsiz → dış release (`Allocated`, aynı uç) tek işlemde `ReadyToShip` + outbox + talimat → sevk onayı → MOD-0183 dispatch → sync-dispatch `Shipped` (kısa sevk tahsislerden); X-10 dört madde | §5.1 X-10, §8.8, AC-43 (S-43c yeni), AC-44 (S-44c yeni), AC-45 |
| F-2 (orta) | X-19 (`IsPublicEndpoint`'e yalnız gelen önek); `X-Tenant-Id` kiracı seçmez, uyuşmazlıkta 401; bypass kapalı AC-63 + S-63; AC-40'a "A `keyId` + B sırrı" ve S-40e | §2.3 kural 5, §8.4, §15, AC-40, AC-63 |
| F-3 (orta) | r2 HEAD `70cb6ca01`'de yeniden ölçüldü (87; 123 / 15; altı önek; yedi satır; `Program.cs:48-65`; SSOR `:120`); "atıflar etkilenmedi" cümlesi düzeltildi; dilim başı ölçüm kuralı korundu | §5.1, §19.2 |
| F-4 (orta) | Q-1…Q-9 karar olarak yazıldı; AS2 taşıma değeri, AS2 sır adları, koşullu uç ve `ReceiveAs2` çıktı; Q-3 kesin liste (943, 856 yok), CONTRL / 997 yok, dar ayrıştırıcı; AC-49 düğme yok (S-49b); dilim kapılarından Q-1…Q-9 düştü | §4.2, §8.3, §8.4, §8.10, §19.4, §24 |
| F-5 (orta) | 90 gün kuralı (sahip kararı): `PurgeThirdPartyMessagePiiCommand`, `OriginalSha256` / `CurrentSha256` / `PiiPurgedAt`, sahibi, kişisel veri notu; MDM Contacts FU01'e | §4.4, §8.7, §21, AC-60 |
| F-6 (düşük) | Tek stoklanabilir lokasyon: kim yaratır, bağlamada doğrulama, GRN hedefi, allocation | §8.10.1, §12, X-13, AC-61 |
| F-7 (düşük) | DL-3 Web kabulü sahte iş ortağı + sahte mesajla, smoke DL-5'te gerçek imzayla tekrar; DL-6 / DL-7 kapısına DL-5 kabulü | §17, §24 |
| F-8 (düşük) | "v2 · planlı" notu ekranlarda (sahip kararı) | §8.14, §8.17, AC-62 |
| F-9 (düşük) | (a) v1.0'da sır değeri istekte yok, parmak izi gövdenin tamamı; kasa FU'sunda anahtarlı özet; (b) `FixedTimeEquals` + S-40d; (c) sırrı okuyan üç yer adıyla | §4.8, §8.4, §8.5, AC-40 |
| F-10 (düşük) | Sır referansı ara çözümü (sahip kararı); DL-5 kasayı beklemez; kasa FU'su X-14 sonraki adım | §8.5, X-14, AC-39 |
| F-11 (düşük) | v1.0 yalnız API (DL-1…DL-9); SFTP DL-10, EDI DL-11 v1.1; r1 DL-1 → DL-1 (1a) + DL-2 (1b) | §2.1, §23, §24 |
| F-12 (düşük) | Sınıf düzeyi izin yasağı + S-7d; ara katman muafiyeti S-63 (S-59 yalnız `ocelot.json`) | §3, §14, AC-7, AC-59, AC-63 |
| Q-10 (sahip) | MDM iş ortağı MOD-0049-FU01; X-1…X-3 taşındı; DL-4 okuma istemcisi, AC-35…AC-38 yeniden yazıldı | §0, §4.10, §5.1, §7, DL-4 |

### 19.7 r2 → r3 kapanış tablosu
| Bulgu | Kapanış | Bölüm / AC |
|---|---|---|
| F-13 (orta) | Referans sunucuda `ThirdPartyLogistics:{TenantId}:{ChannelId}:{SecretName}`; istemci referans vermez (gövdede `ref` 400); çözücü her okumada referansı kiracı + kanaldan yeniden kurar, saklanan dizge farklıysa çözmez | §4.2, §8.5, §12, §13, AC-39 (S-39a yeniden tanımlandı, S-39c yeni) |
| F-14 (düşük) | X-19 `IsPublicEndpoint` yerine `IsNonTenantPath` (`TenantResolutionMiddleware.cs:430-436`, erken dönüş `:35`): bypass ve `X-Tenant-Id` yazımı bu yolda çalışmaz; bypass açık vaka | §5.1 X-19, §7, §15, AC-63 (S-63b yeni) |
| F-15 (düşük) | Dış release ön denetimleri `picked` kümesi yerine tahsislerden (`OutboundFlowHandlers.cs:294-295`, `:298-342`); geri çağrılmış / Blocked / Expired lotlu dış release 422 | §5.1 X-10 (2), AC-43 (S-43d yeni) |
| F-16 (düşük) | AC-34 (a) DL-3'te sahte proxy ile, (b) seçim kısmı DL-4 kabulünde; yeni uç yok | AC-34 (S-34b yeni), §17, §17.1, §24 |
| F-17 (düşük) | (a) X-14 ve Q-16: registry `:70` notu "not a gate" — tutarlı; (b) MOD-0049 / FU01 registry `:160-161`; (c) AC-20 / AC-25 dilim sütunlarına DL-5, DL-6 (yalnız AC-25), DL-7, DL-8 eklendi | §0, §5.1 X-14, §16, §19.1, §19.4, §24 |

## 20. Follow-up Items
- **FU-0179-TREASURY-NOTE** (CT işi) — Treasury'ye bilgi notu (fatura kaynağı, kalemler — mockup M-10, satır bazı M-12, itiraz kanalı M-14, finans bağlantısı).
- **FU-0179-FEES** — ücret mutabakatı (v2). **FU-0179-VAULT** — kiracı sır kasasıyla değer yazımı (X-14, Q-16).
- **FU-0179-MON** / **FU-0179-EDIHUB** — MOD-0037 / MOD-0036'ya taşınma. **FU-0179-AS2**, **FU-0179-ACK** (CONTRL / 997).
- **FU-0179-ADJUST** (Q-4), **FU-0179-CANCEL**, **FU-0179-AUTOADVICE** (Q-8), **FU-0179-RETURNS**, **FU-0179-TRANSFER**, **FU-0179-RCS**, **FU-0179-EDGE**,
  **FU-0179-PARTNER-GUIDE** (CT), **FU-0179-AI**.
- **BL-665** — aynı firmanın tedarikçi / 3PL / CRM müşterisi kayıtlarının bağlanması (kararlar 01).

## 21. Audited Events

Servisin denetim altyapısı **bugün var**: `tests/architecture/audit-ledger/Diten.InventoryService.md:12` (yol b). "Olay adı" = `RequestType` + `Operation`;
`SourceModule` = `THIRD-PARTY-LOGISTICS`. Başarılı ve yazan komut 1; replay 0; başarısız 0. Sır değeri, imza, ham içerik ve kişisel veri hiçbir kayda yazılmaz.

| Komut | Olay adı | Nesne (tür) | Yol | Önceki/sonraki ya da değişen alanlar | İstisna sınıfı + gerekçe |
|---|---|---|---|---|---|
| CreateThirdPartyPartnerCommand | CreateThirdPartyPartnerCommand + Create | ThirdPartyPartner | b | Sonraki: BusinessPartnerId, PartnerCode, WarehouseIds, ResponsibleUserId | — |
| UpdateThirdPartyPartnerCommand | UpdateThirdPartyPartnerCommand + Update | ThirdPartyPartner | b | önceki / sonraki: WarehouseIds, ResponsibleUserId, ContractNote | — |
| SuspendThirdPartyPartnerCommand | SuspendThirdPartyPartnerCommand + LifecycleTransition | ThirdPartyPartner | b | Status, **gerekçe** | — |
| ActivateThirdPartyPartnerCommand | ActivateThirdPartyPartnerCommand + LifecycleTransition | ThirdPartyPartner | b | Status, **gerekçe** | — |
| UpdateThirdPartyThresholdsCommand | UpdateThirdPartyThresholdsCommand + Update | ThirdPartyPartner | b | §4.1.1 alanları | — |
| CreateThirdPartyChannelCommand | CreateThirdPartyChannelCommand + Create | ThirdPartyChannel | b | Type, Name, WarehouseIds, sırsız ayarlar | — |
| UpdateThirdPartyChannelCommand | UpdateThirdPartyChannelCommand + Update | ThirdPartyChannel | b | değişen sırsız ayarlar | — |
| CloseThirdPartyChannelCommand | CloseThirdPartyChannelCommand + LifecycleTransition | ThirdPartyChannel | b | Status, **gerekçe** | — |
| OpenThirdPartyChannelCommand | OpenThirdPartyChannelCommand + LifecycleTransition | ThirdPartyChannel | b | Status, **gerekçe** | — |
| CreateThirdPartyMappingCommand | CreateThirdPartyMappingCommand + Create | ThirdPartyMapping | b | Kind, OurRef, PartnerValue | — |
| UpdateThirdPartyMappingCommand | UpdateThirdPartyMappingCommand + Update | ThirdPartyMapping | b | PartnerValue | — |
| DeactivateThirdPartyMappingCommand | DeactivateThirdPartyMappingCommand + LifecycleTransition | ThirdPartyMapping | b | Status | — |
| ReceiveThirdPartyMessageCommand | ReceiveThirdPartyMessageCommand + Create | ThirdPartyMessage (+ istisna, talimat onayı) | b (Q-14) | Kind, Format, ExternalMessageId, sonuç Status, istisna no, belge no | — |
| ReprocessThirdPartyExceptionsCommand | ReprocessThirdPartyExceptionsCommand + Execute | ThirdPartyException (+ mesaj) | b | istisna numaraları, sonuçlar | — |
| IgnoreThirdPartyExceptionCommand | IgnoreThirdPartyExceptionCommand + LifecycleTransition | ThirdPartyException | b | Status, **gerekçe** | — |
| AssignThirdPartyExceptionCommand | AssignThirdPartyExceptionCommand + Update | ThirdPartyException | b | sorumlu | — |
| ProcessHeldThirdPartyMessagesCommand | ProcessHeldThirdPartyMessagesCommand + Execute | ThirdPartyMessage | b | mesaj kimlikleri, `confirmOutOfOrder`, sonuçlar | — |
| DiscardHeldThirdPartyMessageCommand | DiscardHeldThirdPartyMessageCommand + LifecycleTransition | ThirdPartyMessage | b | Status, **gerekçe** | — |
| PurgeThirdPartyMessagePiiCommand | PurgeThirdPartyMessagePiiCommand + Execute | ThirdPartyMessagePayload | b | temizlenen yük sayısı, mesaj kimlikleri, `PiiPurgedAt` (içerik ve kişisel veri **yok**); sistem aktörü | — |
| SetThirdPartyChannelSecretRefCommand | SetThirdPartyChannelSecretRefCommand + Update | ThirdPartyChannel | b | sır adı, referans adı, `RefSetAt` (değer **yok**) | — |
| TestThirdPartyChannelCommand | — | — | — | yalnız `LastTestAt`, `LastTestResult` | İ2 — teknik sinyal: kanalın iş kaydını değiştirmez, `Version`'ı artırmaz; işlem açmaz, enlist etmez (`RequireEnlistment`); işaret taşır ve sayıya dahildir |
| SendThirdPartyMessageCommand | SendThirdPartyMessageCommand + Execute | ThirdPartyMessage | b (Q-14) | mesaj kimliği, Status, deneme | — |
| SendInboundAdviceCommand | SendInboundAdviceCommand + Create | ThirdPartyInstruction (+ mesaj) | b | PO numarası, depo, satır sayısı | — |
| AddReconciliationNoteCommand | AddReconciliationNoteCommand + Update | ThirdPartyReconciliationLine | b | not, miktar | — |
| MarkReconciliationInvestigatingCommand | MarkReconciliationInvestigatingCommand + LifecycleTransition | ThirdPartyReconciliationLine | b | Status | — |
| ResolveReconciliationLineCommand | ResolveReconciliationLineCommand + LifecycleTransition | ThirdPartyReconciliationLine | b | Status, **gerekçe** | — |

**DL-6:** sevk talimatı MOD-0178 dış release işleminde yazılır ve o komutun merkezi kaydına enlist olur (port deseni). **Yazma olmayan:** CSV, ham içerik
görüntüleme, performans okumaları.

## 22. Record Codes

| Kayıt türü | Sınıf | TÜR öneki | Üretici | Gerekçe |
|---|---|---|---|---|
| 3PL istisnası (`ThirdPartyException.Number`) | OTOMATİK | `TPX` (RCS-001 satırı `.antigravity` kapısında — onaydan önce, açık) | hedef MOD-0040-FU02; ara MOD-0178 geçici sayacı | sorumlu atama ve yazışma bu numarayla |
| Ortak bağlantısı | — | iş ortağının kodu (MOD-0049-FU01) | — | ayrı kod gerekmez |
| Kanal, eşleme, mesaj, talimat, rapor, mutabakat satırı | — | kod yok | — | dış kimlik / kaynak belge no / ortak + depo + tarih |

Biçim `{ŞİRKET}-{TÜR}-{000001}` (RCS-001).

## 23. Teslim dilimleri

| dilim | ad | kapsadığı bölümler | gerekli kanıt seviyesi | plan yetenek kimliği | plan MVP |
|---|---|---|---|---|---|
| DL-1 | (r1 DL-1a) 3PL-BUNDLE taslağı + ortak bağlantısı, kanal (sırsız), eşleme, eşikler, tek lokasyon kuralı + izin, kiracı, denetim (arka uç; çekirdek dilim) | Owned Objects, Entity Fields, ThirdPartyPartner, ThirdPartyChannel, ThirdPartyMapping, ThirdPartyCommandRequest, Indexes, Repo Scope, Protected Paths, Dependencies, Servis ve kapsam, Durum makineleri, Tek stoklanabilir lokasyon, Sözleşme (3PL-BUNDLE) ve dondurma, Backend File Convention, Validation Rules, Failure Path to Verify, Authorization Convention, Audited Events | E4 | plan kimliği yok | MVP-5 |
| DL-2 | (r1 DL-1b) Mesaj defteri ve kanonik model, istisna kutusu ve yeniden işleme, bekletilenler, 90 gün kişisel veri kuralı, sağlık / KPI, kayıt kodu | ThirdPartyMessage ve ThirdPartyMessagePayload, ThirdPartyInstruction, ThirdPartyException, Kanonik mesaj modeli ve mesaj listesi, Askıya alma ve bekletilen mesajlar, İstisna kutusu yeniden işleme ve kişisel veri, Sağlık ve performans eşikleri, Merkezi denetim ve yerel iz, Kayıt kodu üretimi, Hata kodları, Record Codes | E4 | plan kimliği yok | MVP-5 |
| DL-3 | (r1 DL-2) Web ekranları: sağlık + ortak ayrıntısı, kanallar, eşlemeler, mesajlar, bekletilenler, istisna kutusu, ayarlar; "v2 · planlı" notu; 7 dil; yardım | Layout & Shell Contract, Frontend File Contract, Ekranlar ve Blueprint soft pages, Ücret mutabakatı genişleme noktası (v2), Page Help (UXH-001) | E4 | plan kimliği yok | MVP-5 |
| DL-4 | (r1 DL-3 yerine) İş ortağı okuma bağlantısı — MOD-0049-FU01 istemcisi (kapı: FU01 API'si `main`'de; MDM'ye yazım yok) | İş ortağı okuması (MOD-0049-FU01 — Q-10), Dependencies | E4 | plan kimliği yok | MVP-5 |
| DL-5 | (r1 DL-4) API kanalı: imzalı gelen uç, giden REST, bağlantıyı sına, sır referansı ara çözümü, Gateway kiracı ara katmanı muafiyeti (3PL-BUNDLE dondurulmuş) | Kanal sırları — referans ara çözümü (F-10) ve kasa FU'su, Entegrasyon kanalları, Gateway / API Routing Decision, XMC-001 — başka modülün dosyalarına dokunuşlar | E4 | plan kimliği yok | MVP-5 |
| DL-6 | (r1 DL-5) Çıkış akışı, tek model: görevsiz tahsis, dış release, sevk onayı → MOD-0183 dispatch → Shipped | Çıkış akışı — tek model (F-1; MOD-0178 / MOD-0183), ThirdPartyInstruction, XMC-001 — başka modülün dosyalarına dokunuşlar | L3 + E5 | plan kimliği yok | MVP-5 |
| DL-7 | (r1 DL-6) Giriş akışı: ön bildirim, teslim onayı → MOD-0142 GRN | Giriş akışı (MOD-0142), ThirdPartyInstruction, XMC-001 — başka modülün dosyalarına dokunuşlar | L3 + E5 | plan kimliği yok | MVP-5 |
| DL-8 | (r1 DL-7) Stok raporu + mutabakat masası + performans (arka uç ve ekran) | Stok raporu ve mutabakat, ThirdPartyStockReport ve ThirdPartyReconciliationLine, Sağlık ve performans eşikleri, Ekranlar ve Blueprint soft pages | E4 | plan kimliği yok | MVP-5 |
| DL-9 | (r1 DL-10) Gateway kapsama testi (integration-agent; DL-5'ten sonra; v1.0'ın son dilimi) | Gateway / API Routing Decision | E3 | plan kimliği yok | MVP-5 |
| DL-10 | (r1 DL-8) **v1.1** — dosya kanalı: SFTP okuyucu / yazıcı, CSV / XML | Entegrasyon kanalları | E4 | plan kimliği yok | MVP-5 |
| DL-11 | (r1 DL-9) **v1.1** — EDI kanalı: EDIFACT + X12, SFTP taşıması | Entegrasyon kanalları, Kanonik mesaj modeli ve mesaj listesi | E4 | plan kimliği yok | MVP-5 |

**r1 → r2 dilim eşlemesi (F-11; MOD-0178 emsali — kapı aracı yalnız `DL-n` okur, `DL-1a` / `DL-1b` yazılamaz):** r1 DL-1 → r2 DL-1 (1a) + DL-2 (1b);
r1 DL-2 → DL-3; r1 DL-3 (MDM yazımı) → MOD-0049-FU01'e taşındı, yerine DL-4 (okuma); r1 DL-4 → DL-5; r1 DL-5 → DL-6; r1 DL-6 → DL-7; r1 DL-7 → DL-8;
r1 DL-10 → DL-9; r1 DL-8 → DL-10 (v1.1); r1 DL-9 → DL-11 (v1.1). **v1.0 = DL-1…DL-9; v1.1 = DL-10, DL-11.** Plan kaynağı: plan dosyası depoda değil;
çağıran CT aktarımı "MVP-5, Conditional — only if external 3PL required"; plan yetenek kimliği verilmedi.

## 24. Dilim kapıları ve bağlı kararlar

Paket onaylanmadan (künye `approved` / `ready-for-dev` + CT-APR-02 + CT-APR-01, onaylayan Ali) hiçbir dilim başlamaz. **Onaydan önce kapanan, AÇIK kapı:**
`.antigravity/**` satırları (PKS-001 `warehouse.third-party-logistics.*`, RCS-001 `TPX`) + aynı adımda X-17, X-18.

| Dilim | Başlamadan kapanması gereken | Seam'ler (XMC-001) | AC | Sabotajlar |
|---|---|---|---|---|
| DL-1 | ilk adım 3PL-BUNDLE taslağı | X-4, X-5, X-6 (a) +12 ve modül listesi, X-6 (b) (öneri +1), X-8 (+17), X-9 | AC-1…AC-9, AC-11, AC-19, AC-20, AC-22, AC-23, AC-25, AC-61 | S-1, S-2a, S-2b, S-3a, S-3b, S-4a, S-4b, S-5a, S-5b, S-6, S-7a, S-7b, S-7c, S-7d, S-8a, S-8b, S-9a, S-9b, S-11, S-19, S-20a, S-20b, S-22a, S-22b, S-23, S-25, S-61 |
| DL-2 | DL-1 kabulü; Q-14 (CT) | X-6 (a) +7, X-7, X-8 (+11) | AC-10, AC-12…AC-18, AC-20, AC-21, AC-24, AC-25, AC-60 | S-10, S-12, S-13a, S-13b, S-14, S-15a, S-15b, S-15c, S-16, S-17, S-18, S-20a, S-20b, S-21, S-24a, S-24b, S-25, S-60a, S-60b, S-60c |
| DL-3 | DL-2 kabulü; Q-17 (CT); Q-13 rol şablonu (Ali) — kabul kapısı; kabul **sahte iş ortağı portuyla**, Web testlerinde FU01 okuma ucu **sahte proxy** ile (F-7; r3, F-16) | X-15 | AC-26…AC-33, AC-34 (a), AC-62 | S-26, S-28, S-29, S-30, S-31, S-32, S-34, S-62a, S-62b |
| DL-4 | **MOD-0049-FU01 paketi onaylı ve okuma API'si `main`'de**; DL-1 kabulü | — (MDM'ye yazım yok) | AC-35…AC-38, AC-34 (b) (r3, F-16) | S-35a, S-35b, S-36, S-37, S-38, S-34b |
| DL-5 | DL-2 kabulü; 3PL-BUNDLE `1.0.0` dondurulmuş; X-19 sahip onayı (integration-agent) | X-6 (a) +3, X-8 (+3), X-19 | AC-39…AC-42, AC-63, AC-20, AC-25 (r3, F-17 (c)) | S-39a, S-39b, S-39c, S-40a, S-40b, S-40c, S-40d, S-40e, S-41a, S-41b, S-42, S-63, S-63b, S-20a, S-20b, S-25 |
| DL-6 | **DL-5 kabulü** (F-7); **Q-18 (MOD-0183 sahibi)**; Q-12 (servis kimliği); X-10 / X-11 sahip onayı; WMS-BUNDLE eklemeli sürüm dondurulmuş | X-8 (+2), X-10, X-11, X-12, X-16 | AC-43…AC-45, AC-25 (r3, F-17 (c)) | S-43a, S-43b, S-43c, S-43d, S-44a, S-44b, S-44c, S-45, S-25 |
| DL-7 | **DL-5 kabulü** (F-7); **Q-19 (procurement)**; Q-12 | X-6 (a) +1, X-8 (+1), X-13, X-16 | AC-46, AC-47, AC-20, AC-25 (r3, F-17 (c)) | S-46, S-47a, S-47b, S-20a, S-20b, S-25 |
| DL-8 | DL-6 ya da DL-7 kabulü; defter "an itibarıyla" okuması ölçüldü | X-6 (a) +3, X-8 (+8), X-15 | AC-48…AC-52, AC-20, AC-25 (r3, F-17 (c)) | S-48a, S-48b, S-49a, S-49b, S-50, S-51, S-52, S-20a, S-20b, S-25 |
| DL-9 | DL-5 kabulü | gateway test projesi; `ocelot.json` sabotajda geçici bozulur, geri alınır | AC-59 | S-59 |
| DL-10 | **v1.0 kabulü** (DL-1…DL-9); SFTP NuGet + güvenlik taraması | — | AC-53…AC-55 | S-53a, S-53b, S-54, S-55 |
| DL-11 | DL-10 kabulü | — | AC-56…AC-58 | S-56, S-57, S-58 |

Sıra: DL-1 → DL-2 → DL-3 ve DL-4 (paralel; DL-4 FU01 kapısına bağlı) → DL-5 → DL-6 / DL-7 (paralel) → DL-8 → DL-9 = **v1.0**; sonra DL-10 → DL-11 = **v1.1**.
MOD-0178 / 0181 / 0182 bekçi testleri her dilimde değişmeden yeşil. Tek istisnalar X-5, X-6, X-8, X-9 — `INVT/Warehouse/**` / `INVT/CycleCount/**` /
`INVT/Barcode/**` dışındadır.

## 25. Page Help (UXH-001)
- `TPLHEALTH` → `Help.TPLHEALTH.Purpose` ("Bağlı 3PL ortaklarının kanal sağlığını, son mesaj zamanını ve açık hata sayısını gösterir."), `.Audience`,
  `.Step1Title`/`.Step1` … `.Step3` (1: ortağı ana veriden seç ve depolarını bağla — her depoda tek stok lokasyonu, 2: kanalını ve eşlemelerini tanımla, 3: durumu
  izle); `.Immediate` ("Askıdaki ortağa mesaj gönderilmez; gelenler 30 gün bekletilir. Ortak silinemez. Ücret mutabakatı v2'de gelecek."); `Related.TPLEXCEPTIONS`,
  `Related.TPLRECON`.
- `TPLCHANNELS` → `Purpose`, `Audience`, `Step1..3` (API kanalını seç, ayarları gir, sır referansını gir ve bağlantıyı sına); `.Immediate` ("Sırlar ekranda görünmez;
  kanal yalnız sır referansını tutar, değeri işletim ekibi yerleştirir. Kanal kapatma gerekçe ister; kapalı kanala gelen mesaj reddedilir ve istisna kutusuna
  düşer. Dosya ve EDI kanalları v1.1'de.").
- `TPLMAPPINGS` → `Purpose`, `Audience`, `Step1..2`.
- `TPLMESSAGES` → `Purpose`, `Audience`, `Step1..2`; `.Immediate` ("Ham içerik ayrı izinle görünür; kişisel veriler maskelenir ve 90 gün sonra kalıcı kaldırılır.").
- `TPLEXCEPTIONS` → `Purpose`, `Audience`, `Step1..3`; `.Immediate` ("Toplu yeniden işleme yalnız aynı ortağın aynı hata türündeki en çok 200 kaydında yapılır.").
- `TPLRECON` (DL-8) → `Purpose`, `Audience`, `Step1..3`; `.Immediate` ("Fark = bizim stok − 3PL stoğu. Yoldaki miktar farktan düşülür. Stok düzeltmesi bu ekranda
  yazılmaz.").
- `TPLSETTINGS` → `Purpose`, `Audience`, `Step1..2`.
- Kiracı modülü → 7 dil (en, tr, fr, es, zh, ar, ru). **Tur yok.**
