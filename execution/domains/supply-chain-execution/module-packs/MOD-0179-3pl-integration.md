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
status: draft
approval_status: pending
revision: "r1 (2026-11-13) — ilk taslak. Girdiler: sahip kararları 01 (docs/records/decisions/2026-11/mod0179-3pl-owner-decisions-01.md — T-1…T-6, M-2, M-4, M-6, M-7, M-15, M-16; ek: M-4 kapandı, mockup tur 2 yok, T-5 için Ali'nin MDM izni 2026-11-13), mockup brief'i (BRIEF-MOD-0179-3pl-integration.md), mockup tur 1 (tek dosya HTML, depo dışında; sha256 §19.1) ve CT'nin tur 2 düzeltme listesi (kararlar 01 :26-30 — bu pakette doğrudan uygulandı, §19.5). Kimlik ön kontrolü bu oturumda koşuldu (§0). Bağımsız inceleme (CT-APR-02) ve onay satırı (CT-APR-01) yok."
status_note: "draft r1 — kod izni yok. Paket onaylanmadan (künye approved / ready-for-dev + CT-APR-02 inceleme kaydı + CT-APR-01 onay satırı, onaylayan Ali / GitHub GMG-ERP) hiçbir dilim başlamaz. Onaydan önce kapanacak: .antigravity kapısı (PKS-001 alt ad alanı satırı — Q-1; RCS-001 TPX satırı ve MDM iş ortağı öneki — Q-11; aynı adımda X-17, X-18); MDM iş ortağı kimlik kararı (Q-10, Ali + CT — Master-first). Dilim kapıları: Platform kiracı sır kasası (Q-16 — DL-4), MOD-0183 sahibinin onayı (Q-18 — DL-5), procurement sahibinin onayı (Q-19 — DL-6), servis kimliği (Q-12 — DL-5, DL-6). Ücret mutabakatı v1'de yok (T-2); Treasury'ye bilgi notu CT işi (§20)."
owner: supply-chain-execution / merkez (Beste) — MVP-5 WMS CT
approver: Ali (GitHub GMG-ERP)
risk: "HIGH — (1) internete açık, imzayla korunan bir giriş ucu ve dış sistemlere giden çağrılar (API, SFTP, EDI); (2) kiracı başına kanal sırları (Platform kasası — bugün yok, Q-16); (3) stok etkisi: 3PL onayları MOD-0142 GRN ve MOD-0183 dispatch yoluyla deftere yansır (yanlış eşleme yanlış stok demektir); (4) 3 servis, 3 domain ve 2 MVP hattında XMC-001 seam'i (MDM, MOD-0178, MOD-0183, MOD-0142, Platform); (5) Blueprint SLO Tier 1, iki EDI standardı (EDIFACT + X12)."
branch: feature/sce/mod-0173-inventory-ledger-valuation
started: 2026-11-13
target: TBD
form_field_count: 8
dependencies: [MDM iş ortağı (bu paketin DL-3'ü — MdmService; Ali izni 2026-11-13), MOD-0178 (outbound, release, dış onay kapısı — XMC), MOD-0183 (dispatch ve GOODS_ISSUE_SALES tek yazıcısı — XMC, MVP-6), MOD-0142 (GRN ve GOODS_RECEIPT_PO — XMC, procurement), MOD-0141 (PO okuması), MOD-0173 / MOD-0173-FU02 (defter okuması, LOCATION operatingMode), MOD-0174 (lot okuması), MOD-0290 (SKU okuması), MOD-0012 (sır kasası — kiracı kasası FU'su gerekir, Q-16), MOD-0033-FU02 (servis kimliği — Q-12), MOD-0040-FU02 (RCS-001 ortak servisi; ara üretici MOD-0178 geçici sayacı), MOD-0018 (RBAC), BL-501 (merkezi denetim), MOD-0037 (Blueprint bağımlılık kapısı — ertelenmiş, W-0179-01), 3PL-BUNDLE (bu paketin sözleşmesi), WMS-BUNDLE 1.1.0, DEV-0000]
---

# MOD-0179 — 3PL Integration (3PL Entegrasyonu)

> **draft (r1, 2026-11-13).** `status: draft`, `approval_status: pending`. **Paket onaylanmadan geliştirme başlamaz:** kod yalnız
> `approved` / `ready-for-dev` künye + bağımsız inceleme kaydı (CT-APR-02) + onay satırı (CT-APR-01, **onaylayan Ali / GitHub `GMG-ERP`**)
> ile yazılır (AGENTS.md Çekirdek madde 7). Bu taslak kod, commit, push, PR ya da onay izni değildir.
>
> **Kısa özet.** MOD-0179, kendi WMS'i olan dış lojistik firmasının (3PL ortağı) işlettiği depoyu bizim depo akışımıza bağlayan **entegrasyon
> köprüsüdür** (Blueprint: "Not SoR: integration bridge; References SoR: WMS/Inventory"). Sevk talimatı ve giriş ön bildirimi gönderir; 3PL'in
> onaylarını (sevk edildi, teslim alındı) ve günlük stok raporunu alır; bunları **stok defterine kendisi yazmadan**, defterin olağan yazıcıları
> üzerinden yansıtır (çıkış: MOD-0178 → MOD-0183 dispatch; giriş: MOD-0142 GRN). Üç kanal: **EDI** (EDIFACT + X12, T-1), **API** (imzalı REST),
> **dosya** (SFTP üzerinden CSV / XML). Ekranlar: entegrasyon sağlığı ve ortak ayrıntısı, istisna kutusu, mutabakat masası, eşik ayarları.
> Ücret / fatura mutabakatı v1'de **yoktur** (T-2). 3PL ortağı MDM'de iş ortağıdır (T-5); MDM'de bugün iş ortağı varlığı **yoktur** (ölçüm,
> §19.2) — MDM tarafı bu paketin DL-3'üdür ve kimliği Ali'ye açık sorudur (Q-10).

## 0. Kimlik ön kontrolü ve keşif

**DCP-002 ön kontrolü (fail-closed) — bu oturumda koşuldu (HEAD `0e8e29aab`, araç sha256 `ae87a56615f5…`):**

| Komut | Sonuç |
|---|---|
| `PYTHONUTF8=1 python .antigravity/scripts/verify_module_id.py . --check-id MOD-0179 --name "3PL Integration"` | çıkış 0 — `OK  MOD-0179: proven against Blueprint/registry.` |
| `PYTHONUTF8=1 python .antigravity/scripts/verify_module_id.py . --discover "3PL integration"` | çıkış 3 ("inceleme gerekli"); `ÖZET bulgular: blueprint 1 · sor 0 · registry 1 · paket 1 · gateway 0 · görev 1`; 611 satır, rapor sha256 `4eac4d87eabad2048d11231e14fa88cb907beecf505d68afa8d538e1fdccc28a`; 572 ref incelendi |

**Keşif bulgularının her birine karar:**

| Bulgu | Karar |
|---|---|
| Blueprint 1 — `MOD-0179 '3PL Integration'` | **Bu paketin kaynağı.** Blueprint 8.8 `Blueprint_Data` satır 180 (aşağıda) |
| Registry 1 — `MOD-0179` reserved / planned | **Bu paketin kaynağı.** `execution/registries/module-id-registry.md:317` |
| Paket 1 — `execution/domains/master-data-management/module-packs/MDM-058-3pl-integration.md` (8 eski uzak dal: `origin/feature/dev-enablement`, `origin/feature/module-catalog`, …; blob `726290f17`) | **Mükerrer değil — eski toplu iskelet, yanlış domain, onaysız; devralınmadı.** İçeriği okundu (`git show 726290f17`): `id: MDM-058`, `domain: master-data-management`, `status: draft`, `owner: ai-agent`, kaynak modülü `MOD-0179`, gövde yalnız şablon maddeleri; HEAD'de ve `origin/main`'de yok. Aynı karar MOD-0178 (`MDM-057`), MOD-0180 (`MDM-059`), MOD-0181 (`MDM-060`) ve MOD-0182 (`MDM-061`) için verildi (MOD-0182 paketi §0). Okunmaz, yazılmaz |
| Görev 1 — `execution/domains/supply-chain-execution/work-packs/mvp5-wms/CT-QUEUE.tsv:11` `WMS-MVP5-MOD0179` | **Bu paketin iş satırı**; çakışma değil |
| Gateway 0 | Karar §15 — mevcut `/api/warehouse/{everything}` ailesi; MDM için yeni rota (X-2) |

**Ek keşif (MDM iş ortağı için, çıkarım değil ölçüm):** `--discover "business partner"` ve `--discover "partner master"` → `blueprint 0 · registry 0 ·
paket 0`; Blueprint'te genel MDM satırı `MOD-0049 Master Data Management (MDM)` (satır 50) var, registry'de yok. Tedarikçi ana verisi
`MOD-0140` (procurement, `Diten.ProcurementService` — `services/Diten.ProcurementService/src/Diten.ProcurementService.Domain/Entities/Supplier.cs`)
ve müşteri hesabı CRM'dedir; MDM'de iş ortağı yoktur (§19.2). Kimlik kararı Q-10.

**Blueprint 8.8 satır 180 (bu oturumda xlsx'ten okundu):** Module ID `MOD-0179` · Suite `Supply Chain Execution Suite` · Capability Group
`Warehouse (WMS)` · Module Name `3PL Integration` · Wave `W-3` · Dependency Gate `API Gateway; Integration Monitoring & Reconciliation` · Outcome
`External warehouse alignment` · Soft Pages `3PL Integration Health; Reconciliation Workbench; Exception Inbox` · Placement `Integration Bridge
(WMS)` · Min Integration Contract `3PL-BUNDLE (partner contracts, event mapping, reconciliation exceptions, audit hooks)` · SoR `Not SoR:
integration bridge; References SoR: WMS/Inventory` · Integration Contracts `BRIDGE-BASE` · Build/Buy `Build` · SLO `Tier 1` · AI Tier `Assist` · AI
Risk `Medium`.

**Blueprint sapmaları (adıyla):**
- **W-0179-01 — bağımlılık kapısı MOD-0037.** Blueprint "Integration Monitoring & Reconciliation" ister; MOD-0037 paketi ertelenmiş durumdadır
  (`execution/domains/platform-shared-services/module-packs/MOD-0037-integration-monitoring.md:13-25` — kapsamı yalnız ertelenmiş durumun
  belgesi; registry `:135` review / planned). **Karar önerisi:** v1 kendi mesaj defterini, istisna kutusunu ve mutabakat masasını 3PL kapsamında
  tutar (Blueprint soft page'leri zaten bunlardır); MOD-0037 gelince taşınma FU-0179-MON. CT kabulü Q-15.
- **W-0179-02 — EDI Hub (MOD-0036, W-4).** Blueprint'te genel EDI Hub ayrı modüldür (satır 37; registry'de yok). v1 EDI ayrıştırma / üretimini
  yalnız 3PL mesaj kümesi için bu pakette yapar (T-1 kararı EDI'yi v1'e aldı); MOD-0036 gelince taşınma FU-0179-EDIHUB. CT kabulü Q-15.

**Diğer kimlik kaynakları:** `execution/domains/supply-chain-execution/domain-config.md:27` (WMS listesi, "conditional"); plan
`docs/roadmap/plans/integration-phase-and-mvp5-start-plan-2026-10-21.md:93` ve `:114` (W-3: planda, ertelenmiş — erteleme sahip kararıyla
2026-11-02'de kalktı, CT kuyruğu `:11`); WMS-BUNDLE taslağı §14 "3PL kapısı" (`docs/analysis/contracts/drafts/wms-bundle-v1-proposal.md:578-583`).

## 1. Module Summary

MOD-0179 **dış depo entegrasyonunun** sahibidir. Bir depo LOCATION'da `operatingMode = external-integrated` işaretlidir
(`services/Diten.InventoryService/src/Diten.InventoryService.Domain/Rules/LocationHierarchyRules.cs:25-28`); o depoyu bir 3PL ortağı işletir ve
raf / görev yönetimini kendi WMS'i yapar. MOD-0179:
- 3PL ortağını **MDM iş ortağından** seçer (ad, adres, iletişim MDM'den gelir, burada düzenlenmez — T-5) ve depolarına bağlar;
- ortak başına kanalları (EDI / API / dosya) tanımlar; sırlar **Platform sır kasasında**, ekranda yalnız "ayarlı · son değişim" (T-4);
- giden mesajları üretir (sevk talimatı, giriş ön bildirimi), gelen mesajları alır (sevk onayı, teslim onayı, stok raporu) ve **kanonik
  mesaj modeline** çevirir;
- gelen onayı defterin olağan yazıcısına iletir; işlenemeyen her mesajı **istisna kutusuna** düşürür (yeniden işleme, toplu ≤ 200 — M-6);
- günlük stok raporunu bizim defterle karşılaştırır (**mutabakat masası**; fark = bizim − 3PL, yoldaki miktar M-4 kuralıyla düşülür);
- ortak bazında gecikme eşiklerini ayar sayfasında tutar (M-2) ve sağlık / performansı gösterir.

**Temel sınırlar:**
- **Stok yazılmaz.** MOD-0179 bakiye, hareket, rezervasyon, görev, GRN ya da sevkiyat yazmaz. Çıkışta `GOODS_ISSUE_SALES`'in tek yazıcısı
  MOD-0183'tür (`execution/domains/supply-chain-execution/module-packs/MOD-0178-putaway-picking-packing.md:178`); girişte `GOODS_RECEIPT_PO`'yu
  MOD-0142 yazar (`execution/domains/procurement/module-packs/MOD-0142-receiving-grn.md:21`).
- **3PL deposunda iç görev üretilmez** (WMS-BUNDLE §14): MOD-0178 o depoda toplama / yerleştirme görevi açmaz (X-10).
- **Ücret / fatura mutabakatı v1'de yok** (T-2); tasarım genişleme noktası bırakır (§8.17).

Hedef kullanıcı: **entegrasyon sorumlusu** (ortak, kanal, eşleme, istisna), **stok / mutabakat sorumlusu** (mutabakat masası), **depo
yöneticisi** (sağlık, performans). Kiracı modülüdür (`_LayoutTenantShell`, 7 dil: en, tr, fr, es, zh, ar, ru — M-7).

## 2. Ownership and Boundaries

### 2.1 Kapsam (v1)
- 3PL ortağı bağlantısı (`ThirdPartyPartner`): MDM iş ortağına başvuru, bağlı depolar, bizdeki sorumlu, sözleşme notu, askıya alma /
  etkinleştirme (gerekçeli, silme yok — mockup K-3), ortak bazında eşikler (M-2).
- Kanallar (`ThirdPartyChannel`): EDI (EDIFACT ve X12; bir ortakta ikisi birlikte olabilir — T-1), API (giden REST + imzalı gelen uç), dosya
  (SFTP, CSV / XML); kanal açma / kapatma gerekçeli (M-16); sır referansları (T-4).
- Eşlemeler (`ThirdPartyMapping`): ürün kodu, depo kodu, stok durumu kodu.
- Mesaj defteri (`ThirdPartyMessage`, ham içerik ayrı ve maskeli görünümle), talimatlar (`ThirdPartyInstruction`).
- İstisna kutusu (`ThirdPartyException`): hata türleri, yeniden işleme (tek ve toplu ≤ 200, aynı ortak + aynı hata türü — M-6, mockup K-7), yok
  sayma (gerekçe), sorumlu atama; kapalı kanala gelen mesaj → "kanal kapalı" istisnası (M-16); lot / SKT farkı istisnası (T-3).
- Bekletilen mesajlar: askıdaki ortağa gelen mesajlar 30 gün bekletilir, elle işlenir, süresi dolan salt okunur arşivdir (mockup M-9, K-4).
- Çıkış akışı (MOD-0178 / MOD-0183 seam'i), giriş akışı (MOD-0142 seam'i).
- Stok raporu ve mutabakat masası (`ThirdPartyStockReport`, `ThirdPartyReconciliationLine`); performans sekmesi (mockup M-8).
- 3PL-BUNDLE sözleşmesi (taslak DL-1'in ilk adımı; dondurma ayrı CT adımı — §8.16).
- MDM iş ortağı, 3PL rolüyle (DL-3 — MdmService, yalnız eklemeli; Ali izni 2026-11-13).

### 2.2 Kapsam dışı
| Şey | Sahibi | 0179 nasıl dokunur |
|---|---|---|
| Ücret / fatura mutabakatı, itiraz, finans kaydı (mockup M-10…M-14, K-10, K-11, K-13'ün ücret kısmı) | v2 (T-2); fatura kaynağı ve finans bağlantısı Treasury'ye bilgi notu | yok — ekran ve izin v1'de yok; genişleme noktası §8.17 |
| Stok bakiyesi, hareket, rezervasyon | MOD-0173 / FU03 | yalnız **okur** (`Find`) |
| Outbound, release, görevler | MOD-0178 | X-10 seam'i: dış depoda görev üretmeme, release'te talimat portu, dış hazır-sevk geçişi |
| Sevkiyat, dispatch, `GOODS_ISSUE_SALES` | MOD-0183 (MVP-6) | X-12 seam'i (sahip onayı Q-18) |
| Mal kabul (GRN), `GOODS_RECEIPT_PO` | MOD-0142 (procurement) | X-13 seam'i (sahip onayı Q-19) |
| Satın alma siparişi | MOD-0141 | yalnız okur (`GET /api/purchase-orders/{poId}`) |
| İş ortağı ana verisi (ad, adres, iletişim, vergi no) | MDM (Ali) — DL-3 ile bu paket geliştirir | MOD-0179 yalnız `BusinessPartnerId` + kod saklar; ad / adres / iletişim okumadır |
| Sır kasasının kendisi | Platform (merkez / Beste — T-4); MOD-0012 ailesi | yalnız port (`IThirdPartySecretVault`); kasa yoksa DL-4 başlamaz (Q-16) |
| Genel entegrasyon izleme, genel EDI Hub | MOD-0037, MOD-0036 | yok (W-0179-01, W-0179-02) |
| 3PL deposundaki raf / görev yönetimi | 3PL'in WMS'i | yok |
| İade, depolar arası transfer, stok düzeltme bildirimi (X12 947) | MOD-0186 / MOD-0178 / FU | v1 yok (FU-0179-RETURNS, FU-0179-TRANSFER) |
| Onaylı stok düzeltmesi ("Düzeltme öner" hedefi) | — (bugün böyle ekran yok — §19.2) | v1'de düğme yok (Q-4 önerisi A) |
| AI Assist | Blueprint AI kapıları | v1 yok; FU-0179-AI |

### 2.3 Sınır kuralları
1. **İkinci bakiye yok.** 0179 varlıklarında `OnHand` / `Available` / `Reserved` alanı yoktur; mutabakat satırındaki "bizim stok" rapor anı için
   defterden okunur ve yalnız o satırın kanıtıdır (MOD-0181 `ExpectedQuantity` deseni).
2. **Başka modülün koleksiyonuna yazılmaz.** 0179 Persistence'ta başka modül koleksiyonu yalnız `Find` ile okunur. Seam'ler (X-10) MOD-0178'in
   **kendi işleminde** 0179 portunu çağırır (MOD-0182 `ScanSessionBinding` deseni).
3. **Ortak verisi kopyalanmaz.** `ThirdPartyPartner` MDM iş ortağının adını, adresini, iletişimini taşımaz; yalnız kimliğini ve değişmez kodunu
   (görüntü için) taşır. Ekran adı MDM'den okur (T-5).
4. **Sır hiçbir yerde görünmez.** Sır değeri ne Inventory koleksiyonunda, ne cevapta, ne logda / OTel'de, ne merkezi denetim kaydında, ne
   idempotency deposunda bulunur; yalnız kasa referansı ve "ayarlı · son değişim" (T-4, mockup K-6).
5. Her okuma ve yazma kiracı + şirket (`TenantId` + `LegalEntityId`, sunucuda çözülür) kapsamındadır; kiracılar / şirketler arası erişim **404**.
   Gelen API ucunda kiracı ve şirket JWT'den değil **kanal anahtarından** çözülür (§8.4); başka kiracının anahtarı 401 döner ve hiçbir şey yazmaz.
6. **Silme yok.** Ortak, kanal, eşleme, mesaj, talimat, istisna, rapor ve mutabakat satırı silinmez; DELETE ucu yoktur. Ortak askıya alınır,
   kanal kapatılır, eşleme pasifleştirilir, istisna yok sayılır.

## 3. Owned Objects

| Nesne | Dilim | Koleksiyon (öneri) | Not |
|---|---|---|---|
| `ThirdPartyPartner` | DL-1 | `wms_tpl_partners` | Değişebilir, `Version` (If-Match) |
| `ThirdPartyChannel` | DL-1 | `wms_tpl_channels` | Değişebilir, `Version`; sır değeri yok, yalnız referans (§4.2) |
| `ThirdPartyMapping` | DL-1 | `wms_tpl_mappings` | Değişebilir, `Version`; pasifleştirilir |
| `ThirdPartyMessage` | DL-1 | `wms_tpl_messages` | Zarf; durum alanı değişir (`Version`), içerik değişmez |
| `ThirdPartyMessagePayload` | DL-1 | `wms_tpl_message_payloads` | Insert-only ham içerik (≤ 5 MB) + sha256 |
| `ThirdPartyInstruction` | DL-1 (model), DL-5 / DL-6 (yazım) | `wms_tpl_instructions` | Değişebilir, `Version` |
| `ThirdPartyException` | DL-1 | `wms_tpl_exceptions` | Değişebilir, `Version`; görünen kod `TPX` (§8.13) |
| `ThirdPartyStockReport` | DL-7 | `wms_tpl_stock_reports` | Insert-only |
| `ThirdPartyReconciliationLine` | DL-7 | `wms_tpl_reconciliation_lines` | Değişebilir, `Version` |
| `ThirdPartyCommandRequest` | DL-1 | `wms_tpl_command_requests` | Komut idempotency deposu; insert-only, komutun işleminde (MOD-0182 §4.11 deseni) |
| Koleksiyon sabitleri | DL-1 | `ThirdPartyLogisticsCollections` (0179 Persistence) | `WarehouseCollections` ile paylaşılmaz (MOD-0181 / 0180 / 0182 emsali) |

Koleksiyon öneki `wms_tpl_*` önerisidir; kesin ad DL-1'in ilk ölçümüyle sabitlenir.

**Komutlar (yazma, 25 — DL-1 18, DL-4 3, DL-6 1, DL-7 3):**
- DL-1: `CreateThirdPartyPartnerCommand`, `UpdateThirdPartyPartnerCommand`, `SuspendThirdPartyPartnerCommand`, `ActivateThirdPartyPartnerCommand`,
  `UpdateThirdPartyThresholdsCommand`, `CreateThirdPartyChannelCommand`, `UpdateThirdPartyChannelCommand`, `CloseThirdPartyChannelCommand`,
  `OpenThirdPartyChannelCommand`, `CreateThirdPartyMappingCommand`, `UpdateThirdPartyMappingCommand`, `DeactivateThirdPartyMappingCommand`,
  `ReceiveThirdPartyMessageCommand` (kanal bağdaştırıcılarının ortak girişi), `ReprocessThirdPartyExceptionsCommand` (1..200),
  `IgnoreThirdPartyExceptionCommand`, `AssignThirdPartyExceptionCommand`, `ProcessHeldThirdPartyMessagesCommand`, `DiscardHeldThirdPartyMessageCommand`.
- DL-4: `SetThirdPartyChannelSecretCommand`, `TestThirdPartyChannelCommand`, `SendThirdPartyMessageCommand` (gönderici işçi).
- DL-6: `SendInboundAdviceCommand`.
- DL-7: `AddReconciliationNoteCommand`, `MarkReconciliationInvestigatingCommand`, `ResolveReconciliationLineCommand`.
- DL-5: sevk talimatının yazımı ayrı komut **değildir** — MOD-0178 release komutunun işleminde port çağrısıdır (X-10, §8.8); sevk onayının işlenmesi
  `ReceiveThirdPartyMessageCommand` / `ReprocessThirdPartyExceptionsCommand` içindedir.
- DL-3 (MDM) komutları MdmService'tedir ve bu sayıya girmez (§4.10, §21).

**Sorgular:** `GetThirdPartyDashboardQuery`, `GetThirdPartyPartnerListQuery`, `GetThirdPartyPartnerByIdQuery`, `GetThirdPartyPerformanceQuery`
(DL-7), `GetThirdPartyChannelListQuery`, `GetThirdPartyChannelByIdQuery`, `GetThirdPartyMappingListQuery`, `GetThirdPartyMessageListQuery`,
`GetThirdPartyMessageByIdQuery`, `GetThirdPartyMessageRawQuery`, `GetThirdPartyExceptionListQuery`, `GetThirdPartyExceptionByIdQuery`,
`GetThirdPartyInstructionListQuery` (DL-5), `GetThirdPartyInstructionByIdQuery` (DL-5), `GetThirdPartyStockReportListQuery` (DL-7),
`GetReconciliationLineListQuery` (DL-7), `GetReconciliationLineByIdQuery` (DL-7), `ExportReconciliationQuery` (DL-7, CSV).

**API (taban `/api/warehouse`, aile `/api/warehouse/third-party-logistics/…` — §15). PUT / DELETE yok:**
DL-1 (28): `GET dashboard` · `GET/POST partners` · `GET partners/{partnerId}` · `POST partners/{partnerId}/update` · `…/suspend` · `…/activate` ·
`…/thresholds` · `GET/POST channels` · `GET channels/{channelId}` · `POST channels/{channelId}/update` · `…/close` · `…/open` · `GET/POST mappings` ·
`POST mappings/{mappingId}/update` · `…/deactivate` · `GET messages` · `GET messages/{messageId}` · `GET messages/{messageId}/raw` ·
`POST messages/held/process` · `POST messages/{messageId}/discard` · `GET exceptions` · `GET exceptions/{exceptionId}` · `POST exceptions/reprocess` ·
`POST exceptions/{exceptionId}/ignore` · `POST exceptions/{exceptionId}/assign`.
DL-4 (3): `POST channels/{channelId}/secrets/{secretName}` (yalnız yazma; cevapta değer yok) · `POST channels/{channelId}/test` ·
`POST inbound/{keyId}/messages` (3PL'in sistemi çağırır — JWT yok, imza; §8.4).
DL-5 (2): `GET instructions` · `GET instructions/{instructionId}`.
DL-6 (1): `POST inbound-advices`.
DL-7 (8): `GET stock-reports` · `GET reconciliation` · `GET reconciliation/{lineId}` · `POST reconciliation/{lineId}/notes` · `…/investigate` ·
`…/resolve` · `GET reconciliation/export` · `GET partners/{partnerId}/performance`.
DL-9 (koşullu, 0 ya da 1): Q-2 = AS2 seçilirse `POST inbound/as2/{keyId}`.
**Toplam servis ucu: 42** (+1 koşullu). DL-2, DL-3 (MdmService), DL-8, DL-10 InventoryService ucu eklemez. Sabit kesimler (`dashboard`, `held`,
`export`) parametreli yoldan önce eşleşir; `{…Id}` parametreleri `:guid` kısıtlıdır; `{keyId}` 26 karakter (§12).

**Controller tabanı:** `ThirdPartyPartnersController`, `ThirdPartyChannelsController`, `ThirdPartyMappingsController`, `ThirdPartyMessagesController`,
`ThirdPartyExceptionsController`, `ThirdPartyInboundController`, `ThirdPartyInstructionsController`, `ThirdPartyReconciliationController` — hepsi
`[Route("api/warehouse")]`, eylem şablonu tam yol parçası; dosyalar `INV/Api/Controllers/ThirdPartyLogistics/` altında, namespace
`Diten.InventoryService.Api.Controllers.ThirdPartyLogistics`. **Neden ayrı namespace:** MOD-0178'in kayıt testi `Controllers.Warehouse`
namespace'ini 0178 manifestiyle eşit sayar (MOD-0182 §3 gerekçesi); ayrı namespace'in bedeli MOD-0173 manifest bütünlük testinde dışlamadır (X-9).

**Frontend rotaları (CT önerisi, Q-5):** `/Warehouse/ThirdPartyLogistics` (sağlık + ortak listesi; ortak ayrıntısı `Details`), `/Warehouse/ThirdPartyChannels`,
`/Warehouse/ThirdPartyMappings`, `/Warehouse/ThirdPartyMessages`, `/Warehouse/ThirdPartyExceptions`, `/Warehouse/ThirdPartyReconciliation`,
`/Warehouse/ThirdPartySettings`. Menü grubu "Entegrasyonlar · 3PL" (mockup). Brief'in `/Integrations/ThirdPartyLogistics/…` önerisi yerine
`Warehouse` alanı: MOD-0178 / 0180 / 0181 / 0182 emsali, yeni bir View alanı açmamak için.

**İzinler:** §14.

## 4. Entity Fields

Tüm 0179 varlıkları InventoryService `EntityBase`'inden türer: `Id`, `TenantId`, `LegalEntityId`, `CreatedAt`, `UpdatedAt`, `Version` (int),
`IsDeleted`, `DeletedAt` (MOD-0182 §4 ölçümü). `TenantId` / `LegalEntityId` sunucuda çözülür; gövde ya da sorguda gelirse 400. Zamanlar UTC.
**Ekranda kimlik (GUID) gösterilmez**: ortak kodu ve adı, depo kodu, SKU kodu / adı, lot numarası, belge numarası, kişi adı.

### 4.1 ThirdPartyPartner
| Alan | Tip | Kural |
|---|---|---|
| `BusinessPartnerId` | uuid | MDM iş ortağı; etkin ve rolünde `ThirdPartyLogistics` olmalı (§4.10); şirkette tekil (bir MDM ortağı şirkette bir kez bağlanır) |
| `PartnerCode` | string | MDM iş ortağının değişmez kodu; bağlantıda bir kez kopyalanır (görüntü ve istisna metni için) |
| `WarehouseIds` | uuid[] 1..20 | her biri şirketin LOCATION `Warehouse` düğümü, `OperatingMode = external-integrated`; bir depo aynı anda yalnız bir **etkin** ortağa bağlı |
| `ResponsibleUserId`, `ResponsibleUserName` | uuid / string | bizdeki sorumlu; ad atama anında kopyalanır |
| `ContractNote` | string \| null | ≤ 2000 (mockup "Sözleşme notu") |
| `Status` | enum | `Active` \| `Suspended` (§8.2) |
| `StatusReason`, `StatusChangedAt`, `StatusChangedBy` | string / UTC / uuid | askıya alma ve etkinleştirmede zorunlu gerekçe 10..500 (mockup K-3) |
| `Thresholds` | nesne | §4.1.1 — eşik ayar sayfasının alanları (M-2) |
| `CreatedBy`, `CreatedByName` | uuid / string | |

**Ad, adres, iletişim, vergi no alanı YOKTUR** (sınır kuralı 3). Görüntüde MDM'den okunur (kısa önbellek, ≤ 60 sn); MDM erişilemezse kod gösterilir ve
"MDM'ye ulaşılamadı" rozeti çizilir.

#### 4.1.1 Eşikler (`Thresholds`, M-2)
| Alan | Tip | Varsayılan (Q-6 — mockup değerleri) | Kural |
|---|---|---|---|
| `ShipmentConfirmMaxMinutes` | int | 120 | 5..2880 — sevk talimatından sevk onayına en çok süre |
| `ReceiptConfirmMaxMinutes` | int | 240 | 5..10080 — ön bildirimden teslim onayına (öneri; mockup yazmadı) |
| `StockReportMaxAgeHours` | int | 26 | 1..168 — son stok raporunun en çok yaşı |
| `StockReportExpectedLocalTime` | "HH:mm" \| null | null | bilgi; depo saat diliminde (mockup "her gün 06:00") |
| `ErrorRateTargetPercent` | decimal | 1.0 | 0..100, bir ondalık — performans hedefi |

### 4.2 ThirdPartyChannel
| Alan | Tip | Kural |
|---|---|---|
| `PartnerId` | uuid | `ThirdPartyPartner` |
| `Type` | enum | `Edi` \| `Api` \| `File` |
| `Name` | string | trim, 2..80; ortakta tekil |
| `WarehouseIds` | uuid[] 1..20 | ortağın depolarının alt kümesi (mockup "Kullanıldığı depo") |
| `Status` | enum | `Open` \| `Closed` (§8.2); kapatma ve açma gerekçe ister 10..500 (M-16) |
| `StatusReason`, `StatusChangedAt`, `StatusChangedBy` | | |
| `Edi` | nesne \| null | `Type = Edi` iken zorunlu: `Standard` (`Edifact` \| `X12`), `SyntaxVersion` (`D96A` … \| `004010` … — sabit liste), `OurId` + `OurQualifier`, `PartnerId` + `PartnerQualifier`, `OutboundMessages[]`, `InboundMessages[]` (§8.3 listesinden), `Transport` (`Sftp` \| `As2` — Q-2) |
| `Api` | nesne \| null | `Type = Api` iken zorunlu: `OutboundBaseUrl` (yalnız `https://`), `AuthType` (`ApiKey` \| `OAuthClientCredentials`), `InboundKeyId` (sunucu üretir, 26 karakter, değişmez), `TimeoutSeconds` 5..120 |
| `File` | nesne \| null | `Type = File` iken zorunlu: `Host`, `Port` 1..65535, `Username`, `HostKeyFingerprint` (SHA256, zorunlu — §8.4), `InboundFolder`, `OutboundFolder`, `ArchiveFolder`, `FilePattern` (glob), `Format` (`Csv` \| `Xml`), `PollMinutes` 5..1440 |
| `Secrets[]` | `{ Name, VaultRef, SetAt, SetBy }` | `Name` türe göre sabit: `ApiKey`, `OAuthClientSecret`, `InboundSigningSecret` (API); `SftpPassword` \| `SftpPrivateKey` (dosya / EDI-SFTP); `As2Certificate`, `As2PartnerCertificate` (EDI-AS2). `VaultRef` opaktır; **değer yoktur** (sınır kuralı 4) |
| `LastSuccessAt`, `LastErrorAt`, `LastErrorCode`, `LastTestAt`, `LastTestResult` | UTC / string | teknik sinyal alanları; `Version`'ı artırmaz (MOD-0182 nabız deseni) |

Bir ortakta aynı türden birden çok kanal olabilir (T-1: EDIFACT ve X12 birlikte — mockup örneği iki EDI kanalı, iki depo).

### 4.3 ThirdPartyMapping
`PartnerId`, `Kind` (`Product` \| `Warehouse` \| `StockStatus`), `OurRef` (SKU kimliği \| depo kimliği \| stok durumu adı), `OurDisplay`
(kod / ad kopyası), `PartnerValue` (1..64, trim, büyük-küçük harf korunur), `Status` (`Active` \| `Inactive`), `Version`. Tekillik: etkin
eşlemelerde (ortak, tür, `PartnerValue`) ve (ortak, tür, `OurRef`).

### 4.4 ThirdPartyMessage ve ThirdPartyMessagePayload
| Alan | Tip | Kural |
|---|---|---|
| `Direction` | enum | `In` \| `Out` |
| `Kind` | enum | `ShipmentInstruction` (Out), `InboundAdvice` (Out), `ShipmentConfirmation` (In), `ReceiptConfirmation` (In), `InventoryReport` (In), `Unsupported` (In — v1'de tanınan ama işlenmeyen tür, ör. fatura; §8.17) |
| `ChannelId`, `PartnerId` | uuid | |
| `Format` | enum | `EdifactInsdes`, `EdifactDesadv`, `EdifactRecadv`, `EdifactInvrpt`, `X12_940`, `X12_943`, `X12_944`, `X12_945`, `X12_846`, `ApiJson`, `FileCsv`, `FileXml` (§8.3; kesin liste Q-3) |
| `ExternalMessageId` | string ≤ 100 | EDI değişim kontrol no / API `messageId` / dosya adı |
| `DedupKey` | string | (ortak, kanal türü, `ExternalMessageId`, içerik sha256) — §8.7 |
| `BusinessDocRef` | `{ Kind, Id, Number }` \| null | `Outbound` (OUT- no), `PurchaseOrder`, `Grn`, `StockReport` |
| `Status` | enum | In: `Received` → `Processed` \| `Failed` \| `Duplicate` \| `Held` \| `Rejected` \| `Discarded`; Out: `Pending` → `Sent` \| `Failed` \| `NotSent` (§8.2) |
| `HeldUntil` | UTC \| null | `Held` iken `ReceivedAt + 30 gün`; geçince **salt okunur arşiv** (türetilmiş — ayrı yazım yok) |
| `Attempts`, `ReceivedAt`, `SentAt`, `ProcessedAt`, `LastErrorCode` | | |
| `PayloadId`, `PayloadSha256`, `PayloadBytes` | | ham içerik `ThirdPartyMessagePayload`'da; ham görüntüleme ayrı izin (mockup K-5) |
| `Canonical` | nesne | kanonik özet (satır sayısı, toplam miktar, belge no) — kişisel veri taşımaz |

`ThirdPartyMessagePayload`: `MessageId`, `ContentType`, `Bytes` (≤ 5 MB), `Sha256`. Insert-only. Ham görünüm okumada maskelenir (§8.7).

### 4.5 ThirdPartyInstruction
`Kind` (`Shipment` \| `InboundAdvice`), `PartnerId`, `ChannelId`, `WarehouseId`, `SourceDoc` (`{ Kind: Outbound \| PurchaseOrder, Id, Number }`),
`Lines[]` (`{ lineRef, skuId, lotId?, lotNumber?, expiry?, quantity, uom }`), `Status` (`Pending` \| `Sent` \| `PartiallyConfirmed` \| `Confirmed` \|
`Closed` \| `Cancelled`), `MessageIds[]`, `ConfirmedLines[]` (`{ lineRef, quantity, lotNumber, expiry, confirmedAt, messageId }`), `SentAt`,
`ConfirmedAt`. Yoldaki miktar (M-4) ve geç onay (belge kapanmış) bu kayıttan hesaplanır.

### 4.6 ThirdPartyException
| Alan | Tip | Kural |
|---|---|---|
| `Number` | string | `{ŞİRKET}-TPX-{000001}` (§8.13); bir kez yazılır |
| `PartnerId`, `ChannelId`, `MessageId` | uuid | |
| `Type` | enum | `Format`, `UnknownProductMapping`, `UnknownLot`, `LotExpiryMismatch` (T-3), `QuantityMismatch`, `LateConfirmation`, `Duplicate`, `ConnectionAuth`, `ChannelClosed` (M-16), `Unsupported` |
| `DetailCode`, `DetailArgs` | string / sözlük | 7 dilde metne çevrilen kod + parametreler (kişisel veri yok) |
| `Status` | enum | `Open` \| `Retrying` \| `Resolved` \| `Ignored` (§8.2) |
| `FirstSeenAt`, `LastAttemptAt`, `Attempts` | UTC / int | |
| `AssigneeUserId`, `AssigneeName` | uuid \| null / string | |
| `IgnoredReason` / `ResolvedBy` (`Reprocess` \| `Mapping` \| `Auto`) | string / enum | yok saymada gerekçe 10..500 zorunlu |

### 4.7 ThirdPartyStockReport ve ThirdPartyReconciliationLine
`ThirdPartyStockReport` (insert-only): `PartnerId`, `WarehouseId`, `ReportedAt` (3PL'in raporu ürettiği an — M-4), `ReceivedAt`, `MessageId`,
`Lines[]` (`{ partnerProductCode, skuId?, lotNumber, expiry?, partnerStatus, stockStatus?, quantity }`).
`ThirdPartyReconciliationLine`: `ReportId`, `PartnerId`, `WarehouseId`, `SkuId`, `LotId?`, `LotNumber`, `ExpiryOurs?`, `ExpiryTheirs?`, `OurQuantity`
(rapor anındaki defter değeri — §8.10), `TheirQuantity`, `Difference` (= bizim − 3PL, mockup M-3 / K-13), `InTransit` (işaretli, M-4), `Explained`
(notlardaki miktarların toplamı), `Unexplained` (= `Difference` − `InTransit` − `Explained`), `Status` (`Matched` \| `Difference` \| `Investigating` \|
`Resolved`), `Notes[]` (`{ text 1..1000, quantity?, by, byName, at }`), `ResolvedReason?`, `ResolvedBy?`, `ResolvedAt?`, `Version`. Yaş ("n gündür
açık") okumada hesaplanır.

### 4.8 ThirdPartyCommandRequest
`RequestOp`, `ActorUserId` (gelen API'de kanal kimliği), `IdempotencyKey`, `RequestFingerprint` (sır alanı dışlanır), `TargetId`, `ResponseStatus`,
`ResponseBody` (sırsız cevap DTO'su), `CreatedAt`. Komutun yazımıyla **aynı işlemde** eklenir (MOD-0182 §4.11 deseni). `ReceiveThirdPartyMessageCommand`
anahtarını `ThirdPartyMessage.DedupKey`'de tutar, buraya yazmaz.

### 4.9 Indexes
| Koleksiyon | Index | Amaç |
|---|---|---|
| `wms_tpl_partners` | `(TenantId, LegalEntityId, BusinessPartnerId)` tekil | bir MDM ortağı şirkette bir kez |
| `wms_tpl_partners` | `(TenantId, LegalEntityId, WarehouseIds)` çok anahtarlı, kısmi (`Status = Active`) + uygulama denetimi | bir depo bir etkin ortak |
| `wms_tpl_channels` | `(TenantId, LegalEntityId, PartnerId, Name)` tekil; `Api.InboundKeyId` **küresel** tekil kısmi | gelen ucun anahtar araması (kiracı anahtardan çözülür) |
| `wms_tpl_mappings` | `(TenantId, LegalEntityId, PartnerId, Kind, PartnerValue)` ve `(…, Kind, OurRef)` tekil kısmi (`Status = Active`) | |
| `wms_tpl_messages` | `(TenantId, LegalEntityId, DedupKey)` tekil; `(…, PartnerId, ReceivedAt desc)`; `(…, Status, HeldUntil)` kısmi | tekrar, liste, bekletilenler |
| `wms_tpl_exceptions` | `(TenantId, LegalEntityId, Status, Type, PartnerId)`; `(…, MessageId)` | kutu, toplu seçim |
| `wms_tpl_instructions` | `(TenantId, LegalEntityId, SourceDoc.Kind, SourceDoc.Id)` tekil kısmi (`Status ≠ Cancelled`); `(…, PartnerId, WarehouseId, Status, SentAt)` | çift talimat yok, yoldaki |
| `wms_tpl_reconciliation_lines` | `(TenantId, LegalEntityId, WarehouseId, ReportId, SkuId, LotNumber)` tekil; `(…, Status)` | |
| `wms_tpl_command_requests` | `(TenantId, LegalEntityId, ActorUserId, RequestOp, IdempotencyKey)` tekil | komut idempotency'si |

### 4.10 MDM iş ortağı (BusinessPartner — MDM seam)
**Ölçüm:** MdmService'te iş ortağı / partner varlığı **yok** — `services/Diten.MdmService/src/Diten.MdmService.Domain/Entities/` altında `Partner`,
`BusinessPartner`, `Supplier`, `Vendor`, `Account` adlı varlık yok; MDM varlıkları ürün ailesi, marka ve tüzel kişiliktir (§19.2). MDM `EntityBase`'i
`LegalEntityId` taşımaz (`services/Diten.MdmService/src/Diten.MdmService.Domain/Entities/EntityBase.cs:3-12`) — iş ortağı **kiracı geneli** kayıttır.
Bu yüzden "mevcut varlığa rol eklemek" mümkün değildir; gereken değişiklik **yeni dosyalar + mevcut dosyalara yalnız kayıt satırları**dır (eklemeli).
Kimliği Q-10'dadır (Ali + CT; Master-first). DL-3'ün önerilen en dar biçimi:

| Alan | Tip | Kural |
|---|---|---|
| `Code` | string | RCS-001 kiracı geneli kayıt → grup öneki + tür öneki (Q-11) + 6 hane; OTOMATİK, değişmez |
| `LegalName`, `ShortName` | string | 2..200 / 2..60; `ShortName` kiracıda tekil |
| `TaxNumber` | string \| null | ≤ 30; ekranda maskeli (mockup "6140•••••21") |
| `Country`, `City`, `District?`, `AddressLine`, `PostalCode?` | | ülke ISO-3166 alpha-2 |
| `Contacts[]` | `{ Type: Integration \| Operations \| Finance, Name, Email?, Phone? }` 0..10 | |
| `Roles[]` | enum[] 1..n | v1: yalnız `ThirdPartyLogistics`; enum genişlemeye açık (yeni rol eklemeli) |
| `Status` | `Active` \| `Inactive` | silme yok; pasifleştirme gerekçeli |
| `Version` | int | If-Match |

MOD-0179 MDM'den yalnız **okur**: `GET /api/mdm/business-partners?role=ThirdPartyLogistics&status=Active` (seçim listesi) ve
`GET /api/mdm/business-partners/{id}` (görüntü). Okuma port'u `IBusinessPartnerDirectory` (0179 Application) — DL-1 testlerde sahte uygulamayla,
DL-3 `main`'e gelince gerçek HTTP istemcisiyle çalışır.

## 5. Repo Scope

`INV` = `services/Diten.InventoryService/src/Diten.InventoryService.`, `INVT` = `services/Diten.InventoryService/tests/Diten.InventoryService.Tests/`,
`MDM` = `services/Diten.MdmService/src/Diten.MdmService.`.

| Yol | Ne | Dilim |
|---|---|---|
| `docs/analysis/contracts/drafts/3pl-bundle-v1-proposal.md` (yazılacak) | 3PL-BUNDLE taslağı (contract-first; **DL-1'in ilk adımı**; dondurma ayrı CT adımı — §8.16) | DL-1 |
| `INV/Domain/ThirdPartyLogistics/{Entities,Enums,Rules,Repositories}/` | varlıklar; durum makineleri, sağlık hesabı, yoldaki / fark kuralları, maskeleme kuralları (saf) | DL-1…DL-9 |
| `INV/Application/Features/ThirdPartyLogistics/` | ortak: `ThirdPartyContract.cs`, `ThirdPartyErrorCodes.cs`, `ThirdPartyCentralAudit.cs`, `Ports/` (`IBusinessPartnerDirectory`, `IThirdPartySecretVault`, `IExternalFulfilmentGate`, `IShipmentDispatchClient`, `IGoodsReceiptClient`, `IPurchaseOrderReader`), `Services/` (kanonik işlemci, bağdaştırıcılar: API, SFTP, EDIFACT, X12, CSV, XML), `Resources/ThirdPartyText.{en,tr,fr,es,zh,ar,ru}.resx` | DL-1…DL-9 |
| `INV/Application/Features/ThirdPartyPartners/`, `ThirdPartyChannels/`, `ThirdPartyMappings/`, `ThirdPartyMessages/`, `ThirdPartyExceptions/`, `ThirdPartyInstructions/`, `ThirdPartyReconciliation/` | Golden Slim: Commands, Queries, Handlers/CommandHandlers, Handlers/QueryHandlers, Validators, `{Module}Models.cs` | dilimine göre |
| `INV/Persistence/ThirdPartyLogistics/` | depolar, `ThirdPartyLogisticsCollections`, index yapılandırması, başka modül okumaları (**yalnız `Find`**) | DL-1…DL-9 |
| `INV/Api/Controllers/ThirdPartyLogistics/` | §3'teki controller'lar | DL-1, DL-4…DL-7, DL-9 |
| `INV/Api/ThirdPartyLogistics/` | `ThirdPartyLogisticsManifestProvider`, `ThirdPartyLogisticsServiceCollectionExtensions`, gelen uç imza süzgeci, işçiler (gönderici, SFTP okuyucu) | DL-1, DL-4, DL-8 |
| `INV/Infrastructure/Authorization/ThirdPartyLogisticsPermissions.cs` | izin sabitleri | DL-1 |
| `INVT/ThirdPartyLogistics/` | birim, entegrasyon, mimari testler (0179'un kendi test ağacı; DL-5'in 0178 davranış testleri **burada**) | her dilim |
| `frontend/Diten.Web/Views/Warehouse/{ThirdPartyLogistics,ThirdPartyChannels,ThirdPartyMappings,ThirdPartyMessages,ThirdPartyExceptions,ThirdPartyReconciliation,ThirdPartySettings}/`, `frontend/Diten.Web/Controllers/`, `frontend/Diten.Web/wwwroot/assets/js/Warehouse/…`, `frontend/Diten.Web/Resources/Views/Warehouse/…` | ekranlar, proxy controller'lar, JS, resx (7 dil) | DL-2, DL-7 |
| `frontend/Diten.Web/Resources/SharedResource.{7}.resx`, `PageHelpResource.{7}.resx`, `frontend/Diten.Web/Services/Help/PageHelpCatalog.cs` | ortak Web kaynakları — **yalnız eklemeli** (nav, yardım, `Perm.Entity.*`) | DL-2, DL-7 |
| `frontend/Diten.Web.Tests/Warehouse/ThirdPartyL10nAndHelpTests.cs` (yeni) | 0179 L10n / Layout / yardım testi | DL-2 |
| `MDM/Domain/Entities/BusinessPartner.cs`, `MDM/Domain/Enums/BusinessPartnerRole.cs`, `MDM/Application/Features/BusinessPartners/**`, `MDM/Api/Controllers/BusinessPartnersController.cs`, `MDM/Api/ModuleRegistration/BusinessPartnerManifestProvider.cs`, `MDM/Persistence/Repositories/BusinessPartnerRepository.cs` (hepsi yeni) | MDM iş ortağı (X-1) | DL-3 |
| `frontend/Diten.Web/Views/MDM/BusinessPartners/` (yeni) + resx | MDM iş ortağı ekranı (X-3) | DL-3 |
| `gateway/Diten.ApiGateway.Tests/Mod0179ThirdPartyRouteTests.cs` (yeni, integration-agent) | 0179 uç kapsama testi + gelen ucun JWT'siz geçişi | DL-10 |

### 5.1 XMC-001 — başka modülün dosyalarına dokunuşlar
Her satır dokunmadan önce sahibine **tek tek** sorulur; yalnız eklemeli. Ölçüm HEAD'i `0e8e29aab` (çalışma ağacı ölçüm sırasında `f032a41a3`'e ilerledi;
arada yalnız başka paket / inceleme kayıtları değişti — atıflar etkilenmedi). **Sahip onayı kayıtlı olan tek satır X-1'in kapsamıdır** (Ali, MDM sahibi —
kararlar 01 :32: "MDM tarafındaki değişikliği merkez geliştirir"; dosya listesi bu paketle Ali'ye gösterilir, DL-3 başında teyit). Diğer satırlar
dilim başlamadan sahibinden onay ister — paket onayını bekletmez (XMC-001 §1, §3); X-17 ve X-18 **paket onayından önce** `.antigravity` kapısında kapanır.

| # | Dosya | Bugün | Gereken değişiklik | Sahip | Dilim |
|---|---|---|---|---|---|
| X-1 | MdmService — yeni dosyalar (§5 tablosu) + `services/Diten.MdmService/src/Diten.MdmService.Api/Program.cs:453-456` (manifest kayıtları) + `services/Diten.MdmService/src/Diten.MdmService.Persistence/DependencyInjection.cs` (depo kaydı) + `tests/architecture/audit-ledger/Diten.MdmService.md` (gerekiyorsa iz satırı) | MDM'de iş ortağı yok; manifest sağlayıcıları LegalEntity / ProductItemSkuMaster / BrandProductMaster (`Program.cs:453-456`); denetim izi `mdm-merkezi-iletim` yol b (`tests/architecture/audit-ledger/Diten.MdmService.md:12`) | §4.10 varlığı, `/api/mdm/business-partners` uçları (liste, ayrıntı, oluştur, `…/update`, `…/deactivate`, `…/activate`), `mdm.business-partners.*` izinleri (`mdm.*` ad alanı MDM'nin — PKS-001 `:97`; yeni ad alanı gerekmez), manifest, yol b denetim; mevcut satırlar değişmez, MDM testleri değişmeden yeşil | MDM (Ali) — **kapsam izni verildi 2026-11-13**; dosya listesi teyidi DL-3 başında; kimlik Q-10 | DL-3 |
| X-2 | `gateway/Diten.ApiGateway/ocelot.json` — MDM rota bloğu (`/api/mdm/brands` `:3819` … emsali) | `/api/mdm/{everything}` yok; her MDM kaynağı ayrı rota (9 `UpstreamPathTemplate` `/api/mdm/…`) | `/api/mdm/business-partners` ve `/api/mdm/business-partners/{everything}` → MdmService; rota testi | **integration-agent** (T-6) | DL-3 |
| X-3 | `frontend/Diten.Web/Views/MDM/` + `Resources/` (MDM alanı) | iş ortağı ekranı yok | `Views/MDM/BusinessPartners/` (Golden Slim, 7 dil, yardım), nav anahtarları | MDM (Ali) — X-1 izniyle | DL-3 |
| X-4 | `services/Diten.InventoryService/src/Diten.InventoryService.Api/Program.cs` manifest bloğu (`:47-62`) | 0181 manifesti `:62` | `ThirdPartyLogisticsManifestProvider` + `AddThirdPartyLogistics()` (kompozisyon kökü) | MOD-0173 (merkez) | DL-1 |
| X-5 | `services/Diten.InventoryService/tests/Diten.InventoryService.Tests/Architecture/SingleSystemOfRecordArchitectureTests.cs` | 0181 koleksiyon bildirimi `:89` | `ThirdPartyLogisticsCollections.All` bildirimi; önek süzgeci değişmez | MOD-0173 (merkez) | DL-1 |
| X-6 | `services/Diten.InventoryService/tests/Diten.InventoryService.Tests/CentralAudit/CentralAuditDeclarationTests.cs` | `:53` `Assert.Equal(117, WriteCommands.Length)`; `:130` `Assert.Equal(14, seen)` | (a) komut sayısı: dilim başında ölçülen + dilimin 0179 komutları (**DL-1 +18, DL-4 +3, DL-6 +1, DL-7 +3**), yorumda adlarıyla; modül listesine `ThirdPartyLogisticsManifestProvider`; (b) işlem çağrı yeri sayısı: öneri tek ortak yardımcı `ThirdPartyTransactions.InTransactionAsync` (beklenen DL-1 +1, sonra 0) — aynı commit, yorumda dilim adı (MOD-0182 X-6 deseni). MOD-0180 / 0182 ile sıra: her dilim **dilim başında ölçülen** sayıdan devam | MOD-0173 (merkez) | DL-1, DL-4, DL-6, DL-7 |
| X-7 | `services/Diten.InventoryService/src/Diten.InventoryService.Domain/Warehouse/Rules/WarehouseRecordCodeRules.cs:14-28` `TypePrefixes` | PUT / PCK / OUT / PKG / CNT | `TPX` sabiti ve listeye ekleme (0180 `WAV`, 0182 `DVC` / `LBL` emsali) | MOD-0178 (merkez) | DL-1 |
| X-8 | `gateway/Diten.ApiGateway.Tests/Mod0178WarehouseRouteTests.cs:97` — **yalnız** `Assert.Equal(<n>, endpoints.Count)` satırı ve üstündeki uç yorumu | `Assert.Equal(73, endpoints.Count)`; test bütün `api/warehouse` controller'larını tarar | uç ekleyen her dilim (**DL-1 28, DL-4 3, DL-5 2, DL-6 1, DL-7 8**, DL-9 koşullu 1) sayıyı dilim başında ölçülen + dilimin uçları yapar; aynı commit; kabul kaydı önce → sonra değerini yazar (MOD-0182 X-12 deseni). Eylem öznitelikleri `[HttpX("…")]` biçiminde | integration-agent (test); rota ailesi MOD-0178 (Beste) | DL-1, DL-4…DL-7, DL-9 |
| X-9 | `services/Diten.InventoryService/tests/Diten.InventoryService.Tests/Api/InventoryLedgerManifestCompletenessTests.cs:213-223` dışlama listesi | Traceability, Quarantine, Expiry, Recall, Warehouse, CycleCounting dışlanır | `.Where(t => t.Namespace?.EndsWith(".Controllers.ThirdPartyLogistics", StringComparison.Ordinal) != true)` + yorum; karşılığı 0179'un kendi manifest bütünlük vakası (AC-7). MOD-0180 / 0182 dışlamaları önce gelirse satır kayar — dilim başında ölçülür | MOD-0173 (merkez) | DL-1 |
| X-10 | MOD-0178: `services/Diten.InventoryService/src/Diten.InventoryService.Application/Features/OutboundShipments/Services/OutboundAllocationService.cs:259` (toplama görevi yaratımı), `…/OutboundShipments/Handlers/CommandHandlers/OutboundFlowHandlers.cs:247` (`ReleaseOutboundShipmentHandler`) ve `:477` (sync-dispatch `ReadyToShip` ön koşulu), `…/PutawayTasks/Handlers/CommandHandlers/CreatePutawayTaskHandler.cs:28` | 0178 depo `OperatingMode`'unu okumaz (ölçüm: `OperatingMode` yalnız `Location` ve Locations özelliğinde); `Origin External` ve `ConfirmationChannel.External` enumda var (`services/Diten.InventoryService/src/Diten.InventoryService.Domain/Warehouse/Enums/WarehouseTaskEnums.cs:26`, `:84`) ama kullanılmıyor | (1) `external-integrated` depoda allocation **toplama görevi açmaz**; (2) release işlemi içinde `IExternalFulfilmentGate.InstructAsync` → `ThirdPartyInstruction` + giden mesaj (`Pending`) aynı işlemde; ortak askıda ya da kanal kapalıysa release **yine olur**, mesaj `NotSent` kalır ve istisna açılır; (3) yeni dış geçiş: 0179'un sevk onayıyla outbound `Allocated` → `ReadyToShip` (`ConfirmationChannel.External`, 3PL'in lot / miktarıyla; 0178 kuralları — lot, SKT, geri çağırma — aynen); sonrası bugünkü sync-dispatch yolu; (4) `external-integrated` depoda yerleştirme görevi açma 422 `WMS_EXTERNAL_WAREHOUSE`. İç depoda davranış **değişmez** (0178 testleri değişmeden yeşil). Tasarım notu DL-5 başında 0178 sahibine (§8.8) | MOD-0178 (merkez, Beste) | DL-5 |
| X-11 | `docs/analysis/contracts/wms-bundle.openapi.yaml` (1.1.0) | dış kapı yalnız alan olarak (`origin: External`, `channel: External`, `sourceSystem`, `externalRef` — taslak §14) | eklemeli yeni sürüm — CT adımı, **DL-5 başlamadan**: dış depoda görev üretilmemesi, `WMS_EXTERNAL_WAREHOUSE` kodu, dış hazır-sevk geçişinin sözleşmesi | MOD-0178 + CT | DL-5 |
| X-12 | MOD-0183 (`Diten.SupplyChainService`) dispatch | `GOODS_ISSUE_SALES`'in tek yazıcısı, `Dispatched`'de (`execution/domains/supply-chain-execution/module-packs/MOD-0183-shipment-tracking-pod.md:52`); `supplychain.shipments.dispatch` (`:83`); `SourceSystem` / `ExternalRef` alanları (`:97`) | 3PL deposunun sevkiyatı 3PL'in sevk onayıyla dispatch edilir: 0179 servis kimliğiyle MOD-0183 dispatch ucunu çağırır (`sourceSystem = 3PL ortak kodu`, `externalRef = mesaj kimliği`) — ya da MOD-0183 sahibinin seçtiği yol (Q-18) | MOD-0183 sahibi (MVP-6 hattı) | DL-5 |
| X-13 | MOD-0142 GRN — `services/Diten.ProcurementService/src/Diten.ProcurementService.Api/Controllers/GrnController.cs:58-59` (`POST /api/grn`, `procurement.grn.create`) | GRN `SourceSystem` / `ExternalRef` alanı var (`execution/domains/procurement/module-packs/MOD-0142-receiving-grn.md:63`); çağıran kullanıcı JWT'si | 3PL teslim onayı → 0179 servis kimliğiyle `POST /api/grn` (`sourceSystem`, `externalRef`, Idempotency-Key = mesaj kimliği); GRN kodu değişmez, yalnız servis kimliğine izin — ya da procurement sahibinin seçtiği yol (Q-19) | procurement sahibi | DL-6 |
| X-14 | Platform kiracı sır kasası (MOD-0012 ailesi) | MOD-0012 yalnız çalışma zamanı yapılandırma sırlarını okur (`services/Diten.Building.Blocks/Diten.BuildingBlocks.Security.Secrets/ISecretsProvider.cs:3-7`: yalnız `Get…`); kiracı başına, çalışma anında yazılabilen kasa **yok** (`docs/reference/modules/platform/secrets-configuration-vault/technical-reference.md:10-24`) | kiracı + şirket + kanal kapsamlı **yalnız-yaz** sır API'si (yaz / döndür, değer okumayı yalnız servis kimliği yapar), "son değişim" bilgisi; ayrı paket (MOD-0012 FU'su — numara registry sürecinden, uydurulmaz; Q-16) | Platform (merkez / Beste — T-4) | DL-4 kapısı |
| X-15 | Governance Rol İzinleri köprüsü (`frontend/Diten.Web/Views/Governance/RoleAssignments/_IndexL10n.cshtml`, `SharedResource.*.resx` `Perm.Entity.*`) | fiiller var (`read`, `create`, `update`, `activate`, `deactivate`, `configure`, `export`) | **yeni fiil yok**; yalnız varlık satır adları 7 dilde (`Perm.Entity.PARTNERS`, `.CHANNELS`, `.MAPPINGS`, `.MESSAGES`, `.RAWMESSAGES`, `.EXCEPTIONS`, `.RECONCILIATION`, `.SETTINGS` — kesin adlar DL-2 ölçümü) | Web ortak (merkez) | DL-2, DL-7 |
| X-16 | `services/Diten.AuthService/**` rol şablonu ve servis kimliği istemci izinleri | — | §14 rol önerisi (Q-13); Inventory servis kimliğine `supplychain.shipments.dispatch` (Q-18) ve `procurement.grn.create` (Q-19) | Ali (Auth) | ayrı WP; DL-5 / DL-6 kapısı |
| X-17 | `execution/domains/supply-chain-execution/module-packs/MOD-0178-putaway-picking-packing.md` §14.1 | `warehouse.waves.*` (0180) ve `warehouse.barcode.*` (0182) alt ad alanı notları | not: "`warehouse.third-party-logistics.*` adlı alt ad alanı MOD-0179'undur; MOD-0178 orada anahtar tanımlamaz" — PKS-001 kuralı gereği iki pakette de kayıt (`.antigravity/rules/permission-key-standard.md:92`) | MOD-0178 (merkez) | `.antigravity` kapısı (§6) |
| X-18 | `services/Diten.Platform/tests/Diten.Platform.Application.Tests/RecordCodes/RecordCodeTypeCatalogTests.cs` `RuleAheadOfCatalogue` | `CNT`, `WAV`, `DVC`, `LBL` satırları | RCS-001 §3'e `TPX` (ve Q-11'in MDM öneki) yazıldığı **aynı adımda** satırları | Platform (merkez) | `.antigravity` kapısı (§6) |

**0178 / 0181 / 0182 bekçi testleri değişmez (bağlayıcı):** `INVT/Warehouse/**`, `INVT/CycleCount/**` ve `INVT/Barcode/**` dosyaları **hiçbir satırı
değişmeden** yeşil kalır (X-8 Gateway testi, X-5 / X-6 / X-9 MOD-0173 testleri bu dizinlerde değildir; yalnız adlı satırları değişir). DL-5'in 0178
davranışı `INVT/ThirdPartyLogistics/` altında ölçülür. Kanıt: dilim kabulünde
`git diff --stat <dilim-başı>..<dilim-sonu> -- services/Diten.InventoryService/tests/Diten.InventoryService.Tests/Warehouse services/Diten.InventoryService/tests/Diten.InventoryService.Tests/CycleCount`
boş (AC-43).

## 6. Protected Paths
- `.antigravity/**` — kullanıcı onayı ister (Çekirdek md. 6). **Kapı — paket onayından ÖNCE, sahip onayıyla (MOD-0180 / 0182 emsali):**
  (1) PKS-001 §4'e `warehouse.third-party-logistics.*` satırı (Q-1), `:92` istisna cümlesine üçüncü alt ad alanı, `:105` `warehouse.*` satırına
  "hariç — MOD-0179" eki; (2) RCS-001 §3 `TPX` (3PL istisnası) satırı ve MDM iş ortağı satırı (Q-11); (3) aynı adımda X-17 ve X-18.
  **Hazırlayan:** CT; **onaylayan:** Beste (MDM öneki için Ali). Bu paket ve dilimleri `.antigravity/**`'a **yazmaz**. Önerilen PKS-001 satırı:
  `` | `warehouse.third-party-logistics.*` | `Diten.InventoryService` — MOD-0179 3PL Integration (3PL ortakları, kanallar, eşlemeler, mesajlar, istisna kutusu, mutabakat, eşik ayarları; owner decision Q-1 (MOD-0179 pack) — `warehouse.*` altında MOD-0179'a ayrılmış adlı alt ad alanı; base path `/api/warehouse/third-party-logistics`) | MOD-0179 (3PL Integration) | ``
- `gateway/Diten.ApiGateway/**/ocelot.json` — yalnız integration-agent (T-6). InventoryService için değişiklik **istenmez** (§15); MDM için X-2.
  `Mod0178WarehouseRouteTests.cs` yalnız X-8'in iki satırı için.
- `services/Diten.Building.Blocks/**` (BL-501 ve sır sağlayıcısı olduğu gibi).
- MOD-0173 / 0174 / 0178 / 0180 / 0181 / 0182 kodu ve testleri — yalnız §5.1'de **adıyla** sayılan dosyalar; listede olmayan bir dosyaya dokunmak
  gerekirse dilim durur ve XMC-001 ile sahibine sorulur.
- `services/Diten.MdmService/**` — yalnız X-1'in yeni dosyaları ve adlı kayıt satırları; `services/Diten.ProcurementService/**`, `services/Diten.SupplyChainService/**`
  (X-12 / X-13 yalnız sahibinin seçtiği yolla, o ekibin eliyle), `services/Diten.Platform/**` (X-18 dışında; X-14 ayrı paket),
  `services/Diten.AuthService/**` (X-16 ayrı WP); Archive; `_Layout.cshtml`.
- Donmuş sözleşmeler: `wms-bundle.openapi.yaml` (yalnız X-11, CT adımıyla), `location.openapi.yaml`, `inventory-bundle.openapi.yaml`,
  `grn-event.openapi.yaml`, PRODUCT-MASTER.

## 7. Dependencies

| Bağımlılık | Tür | Durum (ölçüm) | Kullanım | Değişiklik | Sahip |
|---|---|---|---|---|---|
| MDM iş ortağı | HARD (DL-1 canlı seçim) | **yok** (§4.10) | ortak seçimi, ad / adres / iletişim görüntüsü | X-1…X-3 (DL-3, bu paket) | MDM (Ali) |
| LOCATION `operatingMode` | HARD | var — `services/Diten.InventoryService/src/Diten.InventoryService.Domain/Entities/Location.cs:31`, değerler `internal` / `external-integrated` | 3PL deposunun tanımı | yok | merkez |
| MOD-0178 outbound | HARD; DL-5 XMC | allocation toplama görevi açar (`OutboundAllocationService.cs:259`); `Shipped` yalnız sync-dispatch'ten (`OutboundFlowHandlers.cs:525`), `ReadyToShip` ön koşuluyla (`:477`) | sevk talimatı, sevk onayı | X-10, X-11 | merkez (Beste) |
| MOD-0183 sevkiyat | HARD (DL-5) | `ready-for-dev`; dispatch + `GOODS_ISSUE_SALES` tek yazıcı | stok düşüşü | X-12 | MOD-0183 sahibi (MVP-6) |
| MOD-0142 GRN | HARD (DL-6) | `POST /api/grn` (`GrnController.cs:23`, `:58-59`); Gateway rotası var (`gateway/Diten.ApiGateway/ocelot.json:730`) | stok girişi | X-13 | procurement |
| MOD-0141 PO | HARD (DL-6, okuma) | `GET /api/purchase-orders/{poId}` (`services/Diten.ProcurementService/src/Diten.ProcurementService.Api/Controllers/PurchaseOrdersController.cs:22`, `:48`) | ön bildirim satırları | yok | procurement |
| MOD-0173 defter | HARD (okuma) | `Find`; hareket türleri `CountGain` / `CountLoss` dahil (`services/Diten.InventoryService/src/Diten.InventoryService.Domain/Enums/MovementType.cs`) | mutabakatta bizim stok | yok | merkez |
| MOD-0174 lot | HARD (okuma) | lot kaydı | lot / SKT karşılaştırması (T-3) | yok | merkez |
| MOD-0290 PRODUCT-MASTER | HARD (okuma) | SKU kodu / adı | eşleme görüntüsü | yok | MDM |
| Platform kiracı sır kasası | **HARD — DL-4 kapısı** | yok (X-14) | kanal sırları | ayrı paket (Q-16) | merkez (Beste) |
| Servis kimliği (MOD-0033-FU02) | HARD (DL-5, DL-6) | registry'de review / local integration | 0183 dispatch, 0142 GRN çağrıları | X-16 | Ali (Auth) |
| MOD-0040-FU02 RCS-001 | SOFT (hedef üretici) | ara üretici MOD-0178 geçici sayacı (`IWarehouseRecordCodeAllocator`) | `TPX` | X-7 | merkez |
| MOD-0018 RBAC | HARD | `[HasPermission]` + manifest | izinler | rol şablonu X-16 | Ali (Auth) |
| BL-501 merkezi denetim | HARD | InventoryService'te var — `tests/architecture/audit-ledger/Diten.InventoryService.md:12` (yol b) | §21 | yok | korunan yol |
| SFTP istemci kütüphanesi | HARD (DL-8) | depoda **yok** (csproj taraması: SSH / SFTP paketi 0) | dosya kanalı | yeni NuGet (öneri SSH.NET, MIT) — DL-8'in ilk adımı, güvenlik açığı taraması kabul kaydında | Beste |
| EDI ayrıştırıcı | HARD (DL-9) | depoda **yok** (EDI / X12 paketi 0) | EDIFACT + X12 | karar Q-3'le: kendi dar ayrıştırıcı (yalnız v1 mesaj kümesi) ya da kütüphane; lisans taraması | Beste |
| 3PL-BUNDLE | HARD (contract-first) | yok | sözleşme | bu paket (DL-1) | supply-chain-execution |
| MOD-0037 / MOD-0036 | Blueprint kapısı | ertelenmiş / registry'de yok | — | W-0179-01 / 02 | platform |
| Gateway | — | `/api/warehouse/{everything}` → InventoryService (`gateway/Diten.ApiGateway/ocelot.json:1153-1161`); kimlik doğrulama servistedir (rotada `AuthenticationOptions` yok) | §15 | InventoryService rotası değişmez; MDM X-2 | integration-agent |

## 8. Runtime Constraints

### 8.1 Servis ve kapsam
- Servis `Diten.InventoryService` (AGENTS.md §3 tablosu), taban `/api/warehouse/third-party-logistics`. Web (5001) servise doğrudan değil,
  same-origin MVC proxy → Gateway (5000) üzerinden gider.
- Yönetim uçları: `Authorization: Bearer`, `X-Legal-Entity-Id`, `X-Correlation-Id`; yazmalarda `Idempotency-Key`; değişebilir kayıtta `If-Match`
  (`W/"<version>"`). Replay araması sırası MOD-0182 §8.1 ile aynı: izin (403) → biçim (400) → idempotency araması → varlık (404) → `If-Match` (409)
  → durum (409) → tekillik (409).
- Gelen API ucu (`POST inbound/{keyId}/messages`): JWT yoktur; `X-TPL-Timestamp` + `X-TPL-Signature` (HMAC-SHA256) + `X-TPL-Message-Id` (§8.4).
- InventoryService PUT / DELETE bekçisi (`LedgerImmutabilityArchitectureTests`) değişmez.
- İşçiler (gönderici, SFTP okuyucu) `IHostedService` olarak 0179'un kayıt uzantısında; kiracı ve şirket kapsamını kanal kaydından alır, her döngü
  kiracı başına ayrı kapsamda çalışır.

### 8.2 Durum makineleri
**Ortak (`ThirdPartyPartner.Status`):** `Active` → `Suspended` (`suspend`, gerekçe) → `Active` (`activate`, gerekçe). Silme yok. Askıdayken giden mesaj
gönderilmez (`NotSent`), gelen mesaj `Held` olur (§8.6).

**Kanal (`ThirdPartyChannel.Status`):** `Open` → `Closed` (`close`, gerekçe — M-16) → `Open` (`open`, gerekçe). Sağlık (`Healthy` / `Delayed` / `Error`)
**durum değildir**, okumada hesaplanır (§8.11).

**Gelen mesaj:** `Received` → `Processed` | `Failed` (+ istisna) | `Duplicate` (+ kendiliğinden kapalı istisna — mockup K-8) | `Held` (ortak askıda) |
`Rejected` (kanal kapalı — + `ChannelClosed` istisnası, M-16). `Failed` → `Processed` (yeniden işleme başarılı). `Held` → `Processed` | `Discarded`
(gerekçe) | arşiv (`HeldUntil` geçti — türetilmiş, salt okunur).

**Giden mesaj:** `Pending` → `Sent` | `Failed` (deneme sınırı — `ConnectionAuth` istisnası) | `NotSent` (ortak askıda / kanal kapalı; etkinleştirme ya da
açmada sırasıyla yeniden `Pending`).

**Talimat:** `Pending` → `Sent` → `PartiallyConfirmed` → `Confirmed` → `Closed` (kaynak belge kapandı); `Cancelled` (kaynak iptal — 3PL'e iptal mesajı v1 yok,
istisna ile görünür, FU-0179-CANCEL).

**İstisna:** `Open` → `Retrying` → `Resolved` | `Open` (deneme başarısız, `Attempts` artar); `Open` → `Ignored` (gerekçe); `Duplicate` doğrudan `Ignored`
(otomatik, gerekçe metni sistem kodu). `Resolved` ve `Ignored` sondur.

**Mutabakat satırı:** `Matched` (açıklanmamış = 0) | `Difference` → `Investigating` → `Resolved` (gerekçe — mockup M-5'in elle yolu); yeni rapor aynı (depo,
SKU, lot) için yeni satır üretir, eski satır kendi raporunun kanıtı olarak kalır.

Geçersiz geçiş 409 `TPL_INVALID_STATE`.

### 8.3 Kanonik mesaj modeli ve mesaj listesi
Her kanal bağdaştırıcısı gelen içeriği **kanonik** modele çevirir; iş kuralları yalnız kanonik modeli görür (kanal bağımsız).

| Kanonik tür | Yön | EDIFACT (T-1; kesin liste Q-3) | X12 (T-1; Q-3) | API / dosya |
|---|---|---|---|---|
| `ShipmentInstruction` | Out | INSDES | 940 | JSON / CSV / XML |
| `InboundAdvice` | Out | DESADV (giden ön bildirim) | 943 (CT önerisi; mockup 856 yazdı — Q-3) | aynı |
| `ShipmentConfirmation` | In | DESADV (gelen sevk onayı) | 945 | aynı |
| `ReceiptConfirmation` | In | RECADV | 944 | aynı |
| `InventoryReport` | In | INVRPT | 846 | aynı |
| `Unsupported` | In | INVOIC vb. | 210 / 810 vb. | tanınan diğer türler — istisna `Unsupported`, işlenmez (§8.17) |

Kanonik satır: `{ partnerProductCode, quantity, uom, lotNumber?, expiry?, partnerStatus?, lineRef? }` + başlık `{ externalMessageId, businessDocNumber,
eventAt, warehouseCode }`. Eşleme (§4.3) ürün, depo ve durum kodlarını bizim kimliklerimize çevirir; eşleme yoksa `UnknownProductMapping` istisnası.
EDI'de bir ortağın aynı belge için iki standardı karıştırmasına izin verilir (kanal başına bir standart; T-1).

### 8.4 Entegrasyon kanalları
- **API (DL-4).** Giden: `OutboundBaseUrl` + 3PL-BUNDLE'daki yollar; `AuthType` `ApiKey` (başlık) ya da `OAuthClientCredentials`; zaman aşımı kanal
  ayarı; 5 deneme, üstel bekleme (1, 2, 4, 8, 16 dk), sonra `Failed` + `ConnectionAuth` istisnası. Gelen: `POST /api/warehouse/third-party-logistics/inbound/{keyId}/messages`;
  imza = HMAC-SHA256(`InboundSigningSecret`, `timestamp + "\n" + messageId + "\n" + sha256(body)`); saat kayması ± 5 dk; `keyId` küresel tekil ve
  kiracı / şirket / kanalı belirler; bilinmeyen anahtar, yanlış imza, süresi geçmiş zaman → **401, hiçbir yazım yok** (mesaj kaydı da yok); gövde ≤ 5 MB;
  kanal kapalı → 409 `TPL_CHANNEL_CLOSED` + mesaj `Rejected` + `ChannelClosed` istisnası (M-16 — mesaj kaydedilir çünkü imza geçerlidir).
- **Dosya (DL-8).** SFTP; ana makine anahtarı parmak izi **zorunlu** ve sabitlenir (uyuşmazlık `ConnectionAuth`); `PollMinutes` aralığıyla
  `InboundFolder`'da `FilePattern`; okunan dosya `ArchiveFolder`'a taşınır (3PL dosyası silinmez); dosya adı + sha256 tekrar anahtarıdır. Giden dosya
  önce `.tmp` adıyla yazılır, sonra adlandırılır (yarım dosya okunmaz). Kapalı kanalda okuma yapılmaz (gelen dosya kanal açılınca okunur — kapalı
  kanala "gelen" sayılmaz; M-16 kuralı push kanallarına uygulanır).
- **EDI (DL-9).** EDIFACT (UNB / UNH / UNT / UNZ zarf ve sayaç denetimi) ve X12 (ISA / GS / ST / SE / GE / IEA); taşıma Q-2'ye göre SFTP (öneri, DL-8
  altyapısı) ya da AS2. Fonksiyonel alındı (CONTRL / 997) v1'de **gönderilmez** (FU-0179-ACK) — Q-3 ile teyit.
- **Ortak:** her kanal yalnız `WarehouseIds`'indeki depolar için mesaj üretir / kabul eder; başka depoya ait gelen mesaj `Format` istisnası.

### 8.5 Kanal sırları ve Platform sır kasası
- Sır yalnız `SetThirdPartyChannelSecretCommand` ile, Web formundan **bir kez** gönderilir; Inventory değeri kasa portuna (`IThirdPartySecretVault.WriteAsync`)
  iletir ve yalnız `VaultRef` + `SetAt` + `SetBy` saklar. Değer hiçbir cevapta, logda, OTel'de, merkezi kayıtta ya da idempotency deposunda yoktur
  (parmak izine katılmaz — MOD-0182 §8.1 PIN kuralı).
- Okuma yalnız gönderici / doğrulayıcı işçide, servis kimliğiyle, bellekte kısa süre; ekranda yalnız "Ayarlı · son değişim {tarih}" (mockup metni).
- Kasa yoksa (X-14) DL-4 başlamaz; DL-1 kanal kaydı sırsız tutulur ve kanal `Open` olsa bile gönderici çalışmaz (DL-4 yok).
- Yardım panelinde bir cümle: "Sırlar Platform sır kasasında tutulur; burada yalnız son değişim tarihi görünür." (kararlar 01 :28-30).

### 8.6 Askıya alma ve bekletilen mesajlar
- Askıya alma / etkinleştirme / kanal kapatma / açma gerekçe ister (10..500) ve denetime yazılır (mockup K-3, M-16).
- Askıdaki ortağa giden mesaj gönderilmez (`NotSent`); gelen mesaj `Held`, `HeldUntil = ReceivedAt + 30 gün` (mockup K-4, M-9).
- Etkinleştirme bekletilenleri **kendiliğinden işlemez**; "Bekletilenler" sekmesinden seçilenler ya da tümü **geliş sırasıyla** işlenir. Seçilenler
  arasında daha eski bekleyen mesaj atlanıyorsa cevap `outOfOrder: true` döner ve istek `confirmOutOfOrder = true` olmadan yazmaz (mockup "Sıra dışı
  işleme · Yine de işle").
- Ortak askıdayken bekletilenleri işleme 422 `TPL_PARTNER_SUSPENDED` (mockup M-13 önerisi — Q-7).
- `HeldUntil` geçen mesaj salt okunurdur: işleme / atma 422 `TPL_HELD_EXPIRED`; silinmez.

### 8.7 İstisna kutusu ve yeniden işleme
- Hata türleri §4.6; her türün ipucu metni mockup'tan (7 dil). Lot / SKT farkı (T-3, mockup K-12): istisna açılır, **bizdeki lot ve SKT kaydı
  değişmez**, onay deftere yansımaz; çözülene kadar bizdeki kayıt geçerlidir.
- **Tekrar (mockup K-8):** aynı `DedupKey` → mesaj `Duplicate`, istisna doğrudan `Ignored` (görünür, işlem gerekmez).
- **Yeniden işleme:** tek kayıt ya da toplu. **Toplu: 1..200 kayıt, hepsi aynı ortak + aynı hata türü** (M-6, mockup K-7); 201 → 422 `TPL_BULK_LIMIT`;
  karışık → 422 `TPL_BULK_MIXED`; her kayıt kendi sonucunu döner (kısmi başarı normaldir). Web 200'den fazla seçimde uyarır ve düğme yalnız ilk 200
  için çalışır ("En çok 200 kayıt birlikte yeniden işlenir").
- **Eşleme ekle ve yeniden işle:** eşleme yaratılır, aynı ortağın aynı koddaki açık `UnknownProductMapping` istisnaları (≤ 200, fazlası sonraki
  tıklamada) yeniden işlenir (mockup ET.map ipucu).
- **Yok say:** gerekçe zorunlu; denetime yazılır.
- **Ham içerik görünümü:** ayrı izin (`raw-messages.read`, mockup K-5); kişisel veri maskeli (alıcı adı, telefon, e-posta, adres satırı — ilk / son iki
  karakter dışında `•`), imza ve sır alanları hiç gösterilmez ("Kişisel veriler maskelendi. Sır ve imza değerleri gösterilmez.").

### 8.8 Çıkış akışı (MOD-0178 / MOD-0183)
1. Outbound `external-integrated` depoda allocate edilir; **toplama görevi açılmaz** (X-10 (1)).
2. Release, 0178'in işleminde `IExternalFulfilmentGate.InstructAsync`'i çağırır: `ThirdPartyInstruction` (`Shipment`) + giden mesaj `Pending` (X-10 (2)).
3. Gönderici işçi mesajı kanaldan yollar (`Sent`).
4. 3PL sevk onayı (DESADV / 945 / API / dosya) → kanonik işlemci: eşleme, lot / SKT (T-3), miktar (talimattan fazla / az → `QuantityMismatch`), belge
   kapanmışsa `LateConfirmation`; geçerse 0178'in dış geçişi (X-10 (3)) → outbound `ReadyToShip` (`ConfirmationChannel.External`).
5. Dispatch: X-12'nin seçilen yolu (öneri: 0179 servis kimliğiyle MOD-0183 dispatch) → MOD-0183 `GOODS_ISSUE_SALES` yazar → 0178 bugünkü sync-dispatch
   ile `Shipped`. **0179 hiçbir aşamada stok hareketi yazmaz.**
6. Tasarım notu (DL-5 başında, 0178 + 0183 sahiplerine): kısmi sevk onayı (ikinci onay mesajı), 3PL deposunda stoğun lokasyon düzeyi (Q-9), iptal.

### 8.9 Giriş akışı (MOD-0142)
1. Kullanıcı "Giriş ön bildirimi gönder" (`POST inbound-advices`): PO numarası + depo; PO satırları MOD-0141'den okunur (Q-8: v1 elle).
2. `ThirdPartyInstruction` (`InboundAdvice`) + giden mesaj.
3. 3PL teslim onayı (RECADV / 944 / API / dosya) → eşleme; ön bildirimde lot yoksa 3PL'in lotu kabul edilir; ön bildirimdeki lotla farklıysa
   `LotExpiryMismatch` (T-3); miktar farkı `QuantityMismatch` (onay yine işlenir mi — Q-19 ile procurement'a: öneri **işlenmez**, istisna).
4. Geçerse X-13'ün seçilen yolu (öneri: servis kimliğiyle `POST /api/grn`, `sourceSystem` = ortak kodu, `externalRef` = mesaj kimliği, `Idempotency-Key`
   = mesaj kimliği) → MOD-0142 `GOODS_RECEIPT_PO` yazar. 3PL deposunda MOD-0178 yerleştirme görevi açmaz (X-10 (4)).

### 8.10 Stok raporu ve mutabakat
- Stok raporu (INVRPT / 846 / API / dosya) → `ThirdPartyStockReport` → depo × SKU × lot satırları.
- **Bizim stok** = MOD-0173 defterinde o depo için `ReportedAt` anına kadar kaydedilmiş hareketlerin toplamı (yalnız `Find`; olanak DL-7 başında
  ölçülür — defter "an itibarıyla" okuması yoksa bugünkü bakiye − `ReportedAt` sonrası hareketler).
- **Fark** = bizim − 3PL (mockup M-3, K-13).
- **Yoldaki (M-4, kararlar 01 :24):** 3PL raporunun üretildiği ana kadar gönderilmiş ama onayı gelmemiş talimatlar — sevk talimatı **+** (biz hâlâ
  tutuyoruz, 3PL göndermiş olabilir), giriş ön bildirimi **−** (3PL teslim almış olabilir, biz henüz almadık). Kalan miktar = talimat − onaylanan.
- **Açıklanmamış** = fark − yoldaki − açıklanmış (notlardaki miktar); yalnız açıklanmamış ≠ 0 satır `Difference` sayılır (mockup K-1).
- Stok düzeltmesi mutabakat masasında **yazılmaz** (mockup K-2). "Düzeltme öner" hedefi bugün yok (§19.2) → Q-4 (öneri A: v1'de düğme yok).
- Raporda olmayan ama bizde stoğu olan satır: 3PL = 0 kabul edilir. Bizde olmayan ama raporda olan: bizim = 0; eşlemesi yoksa `UnknownProductMapping`.
- CSV dışa aktarma `reconciliation.export` ister; hücre başındaki `=`, `+`, `-`, `@` kaçışlanır (formül enjeksiyonu).

### 8.11 Sağlık ve performans eşikleri
- Eşikler ortak başına (§4.1.1); ayar sayfası `settings.configure` ister (M-2).
- **Sağlık:** `Suspended` ortak → Askıda; bütün kanallar kapalı → Kapalı; son 24 saatte açık `ConnectionAuth` istisnası ya da son gönderim `Failed` →
  Hatalı; eşiği aşan onaysız talimat ya da `StockReportMaxAgeHours`'u aşan rapor → Gecikmeli; değilse Sağlıklı. Durum simge + yazıyla (yalnız renk değil).
- **KPI (dashboard):** bağlı (etkin) ortak; son 24 saatte mesaj (gelen / giden); başarısız mesaj (açık istisna); son stok raporu yaşı (saat, en eski depo);
  açık mutabakat farkı (`Difference` + `Investigating`).
- **Performans (DL-7, mockup M-8):** son 30 gün — ortalama sevk onayı süresi, zamanında stok raporu oranı, mesaj hata oranı, açıklanmamış fark oranı;
  hedefler eşiklerden.
- Gecikmiş stok raporu: sağlık ekranında bant ("Stok raporu gecikti." + ilgili istisnaya bağlantı).

### 8.12 Merkezi denetim ve yerel iz
- BL-501 InventoryService'te kurulu (yol b, `RequireEnlistment` kapalı-başarısız); her yazma komutu `ICentralAuditDeclaration` taşır; `SourceModule` =
  manifest kodu `THIRD-PARTY-LOGISTICS`.
- Merkezi kayıt yalnız **başarılı ve yazan** komutta 1; replay ve başarısız 0.
- **Hacim notu (Q-14, CT):** `ReceiveThirdPartyMessageCommand` ve `SendThirdPartyMessageCommand` her mesajda 1 merkezi kayıt doğurur (yol b). Alternatif:
  mesaj defteri yol c eşdeğeri iz (insert-only zarf + durum olayları, kullanıcıya gösterilir) — CT kabulü ister. r1 varsayılanı **b** (MOD-0182 Q-16 emsali).
- Sır değeri, imza, ham içerik merkezi kayda yazılmaz; yalnız mesaj kimliği, tür, durum, belge numarası.

### 8.13 Kayıt kodu üretimi
- `ThirdPartyException.Number` = `{ŞİRKET}-TPX-{000001}` (RCS-001 biçimi; OTOMATİK). Ara üretici MOD-0178 geçici sayacı (`IWarehouseRecordCodeAllocator`) —
  `TypePrefixes`'e ekleme X-7; ortak servise geçiş MOD-0040-FU02 ile (FU-0179-RCS).
- Ortak bağlantısı MDM iş ortağının kodunu kullanır (kendi kodu yok). Kanal, eşleme, mesaj, talimat, rapor, mutabakat satırı görünen kod **taşımaz** (§22).
- `TPX` RCS-001 §3'te yoktur (ölçüldü: `.antigravity/rules/record-coding-standard.md:45-78`) — satır korunan yol değişikliğidir (§6 kapısı).
- MDM iş ortağı: kiracı geneli kayıt → grup öneki (`.antigravity/rules/record-coding-standard.md:102-107`); tür öneki Q-11 (Ali).

### 8.14 Ekranlar ve Blueprint soft pages
| Blueprint soft page | Ekran | Sayfa kodu | Veri |
|---|---|---|---|
| 3PL Integration Health | `/Warehouse/ThirdPartyLogistics` — KPI kartları + ortak listesi (ortak, işlettiği depolar, kanal rozetleri, bağlantı durumu, son başarılı mesaj, açık hata); satır menüsü Aç, Bağlantıyı sına (DL-4), Askıya al / Etkinleştir (gerekçe); **Sil yok**; "Ortak ekle" = MDM'den 3PL rollü ortak seçimi. `Details` sayfası sekmeleri: Genel (MDM'den salt okunur bilgi, bağlı depolar + `external-integrated` notu, sözleşme notu, denetim izi), Kanallar, Eşlemeler, Mesajlar (iş belgesine tıklayınca belge zaman çizelgesi), Performans (DL-7), Bekletilenler | `TPLHEALTH` (nav) | |
| (kanallar) | `/Warehouse/ThirdPartyChannels` — Compact (Create / Edit / Details); sır alanları yalnız "Ayarla / Değiştir" düğmesi + "Ayarlı · son değişim" | `TPLCHANNELS` | |
| (eşlemeler) | `/Warehouse/ThirdPartyMappings` — eksik eşleme uyarısı | `TPLMAPPINGS` | |
| (mesajlar) | `/Warehouse/ThirdPartyMessages` — liste; ham içerik ayrı izinle | `TPLMESSAGES` | |
| Exception Inbox | `/Warehouse/ThirdPartyExceptions` — süzgeç hata türü / durum (varsayılan açık olanlar) / ortak; toplu seçim ≤ 200 + uyarı; Yeniden işle, Eşleme ekle ve yeniden işle, Yok say (gerekçe), Sorumlu ata | `TPLEXCEPTIONS` (nav) | |
| Reconciliation Workbench | `/Warehouse/ThirdPartyReconciliation` — ortak + depo + rapor tarihi (en son varsayılan), "Yalnız açık farklar", özet (satır, uyumlu, açık fark, açıklanmamış toplam), satır eylemleri Not ekle, İnceleniyor, Çözüldü (gerekçe), CSV (DL-7) | `TPLRECON` (nav) | |
| (eşik ayarları) | `/Warehouse/ThirdPartySettings` — ortak başına eşikler (M-2) | `TPLSETTINGS` (nav) | |

Durumlar her ekranda: boş ("Henüz 3PL ortağı tanımlı değil" + yetkiliyse "Ortak ekle"; "Bu depo için henüz stok raporu alınmadı"; "Açık istisna yok"),
yükleniyor, hata ("Entegrasyon servisine ulaşılamadı" + Yeniden dene), yetkisiz (UAS-001: menü ve üst çubuk kalır, içerikte tek açıklama). Masaüstü
öncelikli, 375 px'te liste ve istisna kutusu okunur, yatay sayfa kaydırması yok; Arapça sağdan sola; klavyeyle kullanılabilir. Ekran metinlerinin TR
kaynağı mockup'ın "Ekran metinleri" listesidir (ücret grubu hariç — T-2).

### 8.15 Hata kodları
| HTTP | Kod | Ne zaman |
|---|---|---|
| 400 | `VALIDATION_FAILED`, `IDEMPOTENCY_KEY_REUSED` | biçim; aynı anahtar farklı gövde; gövdede `tenantId` / `legalEntityId` |
| 401 | `TPL_SIGNATURE_INVALID` (gövdede kod yok, yalnız durum) | gelen uç: anahtar, imza, zaman |
| 403 | izin yok | `[HasPermission]` |
| 404 | — | kayıt yok / başka kiracı / şirket |
| 409 | `TPL_CONFLICT`, `TPL_INVALID_STATE`, `TPL_PARTNER_ALREADY_LINKED`, `TPL_WAREHOUSE_TAKEN`, `TPL_MAPPING_TAKEN`, `TPL_CHANNEL_CLOSED` | eski `If-Match`; geçersiz geçiş; tekillik; kapalı kanala gelen mesaj |
| 422 | `TPL_PARTNER_NOT_3PL`, `TPL_WAREHOUSE_NOT_EXTERNAL`, `TPL_BULK_LIMIT`, `TPL_BULK_MIXED`, `TPL_PARTNER_SUSPENDED`, `TPL_HELD_EXPIRED`, `TPL_OUT_OF_ORDER_UNCONFIRMED`, `TPL_SECRET_NAME_INVALID`; 0178'de `WMS_EXTERNAL_WAREHOUSE` (X-10) | iş kuralı |
| 503 | `TPL_MDM_UNAVAILABLE`, `TPL_VAULT_UNAVAILABLE`, `TPL_INVENTORY_UNAVAILABLE` | bağımlılık erişilemez |

Kullanıcı metni kodun 7 dildeki karşılığıdır; istemcide sabit metin yok. İstisna türleri ve ipuçları da kodla gelir.

### 8.16 Sözleşme (3PL-BUNDLE) ve dondurma
- Taslak `docs/analysis/contracts/drafts/3pl-bundle-v1-proposal.md` (yazılacak) **DL-1'in ilk adımıdır**: kanonik mesaj şeması, gelen API (imza, başlıklar,
  hata), giden API beklentileri, CSV / XML dosya düzenleri, EDIFACT ve X12 segment → kanonik eşleme tabloları (DL-9 için), yönetim uçları, hata kodları,
  sürüm `1.0.0-draft`.
- **Dondurma ayrı CT adımıdır** ve **DL-4 başlamadan** yapılır (ilk dış ortak onboarding'inden önce): `docs/analysis/contracts/3pl-bundle.openapi.yaml`
  `1.0.0`; sonrası yalnız eklemeli sürüm. Dondurmadan sonra ortaklara verilecek "ortak entegrasyon kılavuzu" CT işidir (§20).
- Blueprint Integration Contracts `BRIDGE-BASE`: depoda karşılığı yok (ölçülmedi — çıkarım); taslakta adıyla anılır, CT teyidi Q-15.

### 8.17 Ücret mutabakatı genişleme noktası (v2)
T-2: v1'de ekran, izin, varlık **yoktur**. Genişleme noktası: kanonik türde `Unsupported` (gelen fatura mesajları tanınır, işlenmez, istisna açılır,
ham içerik korunur), 3PL-BUNDLE'da ayrılmış `InvoiceAdvice` adı, mesaj defterinde fatura mesajları için yer. Treasury'ye bilgi notu (fatura verisinin
kaynağı, karşılaştırılacak kalemler — mockup M-10…M-14 — ve finans bağlantısı) CT işidir (§20). Mockup'ın ücret ekranı ve "Ücret mutabakatı izni" bu
pakete **girmez**; ekranlarda "v2 · planlı" notu yoktur (menüde görünmez).

### 8.18 AI Assist
Blueprint AI Tier `Assist`. v1'de **yok** (FU-0179-AI).

## 9. Layout & Shell Contract
- `shell: tenant` → `Views/Warehouse/{ThirdPartyLogistics,ThirdPartyChannels,ThirdPartyMappings,ThirdPartyMessages,ThirdPartyExceptions,ThirdPartyReconciliation,ThirdPartySettings}/`
  altındaki **her sayfa görünümünde** (partial `_*.cshtml` hariç) `Layout = "_LayoutTenantShell";` **AÇIKÇA** yazılır; `_ViewStart.cshtml` varsayılanı
  değişmez (AC-26). MDM ekranı (`Views/MDM/BusinessPartners/`, DL-3) için de aynı kural.
- View klasörü `Views/Warehouse/{Module}/`; rota `/Warehouse/{Module}` (Q-5).
- Yetkisiz kullanıcıya sayfa iskeleti çizilmez, yönlendirme yapılmaz; sol menü ve üst çubuk kalır, içerikte tek açıklama (UAS-001).

## 10. Backend File Convention
Golden Reference Slim birebir (`services/Diten.DevEnablementService/src/Diten.DevEnablementService.Application/Features/GoldenReferenceSlim/`;
pack `execution/domains/developer-enablement/module-packs/DEV-0000-golden-reference-slim.md`):

```text
Features/ThirdPartyPartners/
├── Commands/        CreateThirdPartyPartnerCommand.cs, UpdateThirdPartyPartnerCommand.cs, SuspendThirdPartyPartnerCommand.cs,
│                    ActivateThirdPartyPartnerCommand.cs, UpdateThirdPartyThresholdsCommand.cs
├── Queries/         GetThirdPartyPartnerListQuery.cs, GetThirdPartyPartnerByIdQuery.cs, GetThirdPartyDashboardQuery.cs,
│                    GetThirdPartyPerformanceQuery.cs
├── Handlers/
│   ├── CommandHandlers/   CreateThirdPartyPartnerHandler.cs … UpdateThirdPartyThresholdsHandler.cs   (sealed class, suffix YOK)
│   └── QueryHandlers/     GetThirdPartyPartnerListHandler.cs … GetThirdPartyPerformanceHandler.cs
├── Validators/      CreateThirdPartyPartnerValidator.cs, UpdateThirdPartyPartnerValidator.cs, SuspendThirdPartyPartnerValidator.cs,
│                    ActivateThirdPartyPartnerValidator.cs, UpdateThirdPartyThresholdsValidator.cs
└── ThirdPartyPartnerModels.cs    (tek dosyada bütün DTO'lar)
Features/ThirdPartyChannels/        Create / Update / Close / Open / SetThirdPartyChannelSecret / TestThirdPartyChannel komutları; List / ById; ThirdPartyChannelModels.cs
Features/ThirdPartyMappings/        Create / Update / DeactivateThirdPartyMappingCommand; List; ThirdPartyMappingModels.cs
Features/ThirdPartyMessages/        ReceiveThirdPartyMessageCommand, SendThirdPartyMessageCommand, ProcessHeldThirdPartyMessagesCommand,
                                    DiscardHeldThirdPartyMessageCommand; List / ById / Raw; ThirdPartyMessageModels.cs
Features/ThirdPartyExceptions/      Reprocess / Ignore / AssignThirdPartyException(s)Command; List / ById; ThirdPartyExceptionModels.cs
Features/ThirdPartyInstructions/    SendInboundAdviceCommand; List / ById; ThirdPartyInstructionModels.cs
Features/ThirdPartyReconciliation/  AddReconciliationNote / MarkReconciliationInvestigating / ResolveReconciliationLineCommand; StockReport List,
                                    ReconciliationLine List / ById, ExportReconciliationQuery; ThirdPartyReconciliationModels.cs
Features/ThirdPartyLogistics/       ortak: ThirdPartyContract.cs, ThirdPartyErrorCodes.cs, ThirdPartyCentralAudit.cs, Ports/, Services/, Resources/
```

**Golden sapması G-0179-01 (adıyla):** DEV-0000 `Delete` / `BulkDelete` komutu ve PUT / DELETE uçları tanımlar; 0179'da **silme yoktur** (§2.3 kural 6):
`Delete` / `BulkDelete` komutu, `delete` izni ve DataTable "Sil" eylemi yoktur; güncelleme `POST …/update` komut ucudur. `ThirdPartyMessages`,
`ThirdPartyExceptions`, `ThirdPartyReconciliation` listelerinde yaratma formu yoktur (`_CreateEditOffcanvas.cshtml` yok; eylem offcanvas'ları gerekçe /
not / sorumlu formudur). DEV-0000'ın `data_mode: client`'ı 0179'da `server`'dır (mesaj ve istisna listeleri sınırsız büyür).
**G-0179-02:** `ThirdPartyLogistics` Slim liste + ayrıca sekmeli `Details.cshtml` (ortak ayrıntısının altı sekmesi quick view'a sığmaz); `ThirdPartyChannels`
**Compact** setidir (kanal formu 10 alana kadar — §11). İkisi de CT kabulü ister (Q-17). Klasör ve adlandırma diğer her yerde Golden ile aynıdır;
`verify_datatable_page.py` sapma verirse dokunma protokolüyle sahibine sorulur.

Kurallar: Command / Query `sealed record`; Handler / Validator adında `Command` / `Query` soneki yok; bir dosyada tek `public` tip;
`Handlers/CommandHandlers` ve `Handlers/QueryHandlers` ayrımı zorunlu (`.antigravity/rules/module-pack-standard.md` §4).

## 11. Frontend File Contract

| Klasör | Golden | Dosyalar |
|---|---|---|
| `Views/Warehouse/ThirdPartyLogistics/` | Slim + `Details.cshtml` (G-0179-02) | `Index.cshtml` (KPI + liste + gecikme bandı), `_Filter.cshtml`, `_DataTable.cshtml` (`data-dt-standard="v2"`, `data-dt-data-mode="server"`, skeleton), `_IndexL10n.cshtml`, `_CreateEditOffcanvas.cshtml` (4 alan: MDM ortak seçimi, bağlı depolar, bizdeki sorumlu, sözleşme notu — ad / adres / iletişim **girdi alanı yok**), `_DetailsQuickView.cshtml`, `Details.cshtml` (6 sekme), `ThirdPartyLogisticsIndex.cs` |
| `Views/Warehouse/ThirdPartyChannels/` | Compact | `Index.cshtml`, `Create.cshtml`, `Edit.cshtml`, `Details.cshtml`, `_Form.cshtml` (ortak alanlar: ad, tür, depolar + türe göre en çok 7: EDI standart, sürüm, bizim kimlik, ortak kimliği, giden mesajlar, gelen mesajlar, taşıma; API taban URL, kimlik doğrulama türü, zaman aşımı; dosya ana makine, port, kullanıcı, parmak izi, gelen klasör, giden klasör, arşiv klasörü, desen, biçim, aralık — 10'u aşan dosya kanalı iki adımlı form: "Bağlantı" + "Dosyalar"), `_Filter.cshtml`, `_DataTable.cshtml`, `_IndexL10n.cshtml`, `ThirdPartyChannelsIndex.cs` — `_CreateEditOffcanvas` / `_DetailsQuickView` **yok** |
| `Views/Warehouse/ThirdPartyMappings/` | Slim | `Index`, `_Filter`, `_DataTable`, `_IndexL10n`, `_CreateEditOffcanvas` (3 alan: tür, bizdeki, 3PL'deki), `_DetailsQuickView`, `ThirdPartyMappingsIndex.cs` |
| `Views/Warehouse/ThirdPartyMessages/` | Slim (G-0179-01) | `Index`, `_Filter`, `_DataTable`, `_IndexL10n`, `_DetailsQuickView` (ham içerik bölümü izinle), `ThirdPartyMessagesIndex.cs` |
| `Views/Warehouse/ThirdPartyExceptions/` | Slim (G-0179-01) | `Index`, `_Filter`, `_DataTable` (toplu seçim), `_IndexL10n`, `_DetailsQuickView`, `_ActionOffcanvas.cshtml` (gerekçe / sorumlu), `ThirdPartyExceptionsIndex.cs` |
| `Views/Warehouse/ThirdPartyReconciliation/` | Slim (G-0179-01) | `Index` (özet + liste), `_Filter`, `_DataTable`, `_IndexL10n`, `_DetailsQuickView` (notlar), `_ActionOffcanvas.cshtml` (not + miktar / gerekçe), `ThirdPartyReconciliationIndex.cs` |
| `Views/Warehouse/ThirdPartySettings/` | Slim | `Index`, `_DataTable` (ortak başına eşikler), `_IndexL10n`, `_CreateEditOffcanvas` (5 eşik alanı), `ThirdPartySettingsIndex.cs` |
| `wwwroot/assets/js/Warehouse/{Module}/` | | `index.js`, `index.l10n.js`; `ThirdPartyLogistics/details.js`, `ThirdPartyExceptions/bulk.js` |
| `Resources/Views/Warehouse/{Module}/` | | `{Module}Index.{en,tr,fr,es,zh,ar,ru}.resx` |
| `frontend/Diten.Web/Controllers/` | | 7 proxy controller — same-origin MVC proxy → Gateway `/api/warehouse/third-party-logistics…` ve `/api/mdm/business-partners` (okuma); her yazma `[ValidateAntiForgeryToken]`; `Accept-Language`, `If-Match`, `Idempotency-Key` iletilir; sır gövdesi proxy'de loglanmaz |

En büyük Slim form 5 alan (eşikler) / 4 alan (ortak) → `form_field_count: 8` künyede Slim sınırı olarak yazıldı; kanal formu Compact ekranındadır (G-0179-02).
Liste ekranına dokunan görev `verify_datatable_page.py --area Warehouse --module {Module} --format gaps` koşar ve sapmayı sahibine sorar.
Nav anahtarları: `Nav.Module.THIRDPARTYLOGISTICS`, `Nav.Page.TPLHEALTH`, `Nav.Page.TPLEXCEPTIONS`, `Nav.Page.TPLRECON`, `Nav.Page.TPLSETTINGS`
(kanallar, eşlemeler, mesajlar ortak ayrıntısından açılır; menüde değil).

## 12. Validation Rules

| Field | Required | Format/Rule | DB-level | Pre-check |
|---|---|---|---|---|
| Ortak `businessPartnerId` | Evet | uuid; MDM'de etkin + rol `ThirdPartyLogistics` | tekil (şirket) | MDM okuması (`TPL_PARTNER_NOT_3PL`, 503) |
| Ortak `warehouseIds` | Evet | 1..20 uuid; şirketin `Warehouse` düğümü, `external-integrated`; başka etkin ortakta değil | uygulama denetimi + index | LOCATION okuması |
| Ortak `responsibleUserId` | Evet | kiracının kullanıcısı | — | Auth okuması |
| Ortak `contractNote` | Hayır | ≤ 2000 | — | — |
| Askıya alma / etkinleştirme / kanal kapatma / açma gerekçesi | Evet | trim, 10..500 | — | — |
| Eşikler | Evet | §4.1.1 aralıkları | — | — |
| Kanal `name` / `type` / `warehouseIds` | Evet | 2..80 / enum / ortağın depolarının alt kümesi | tekil (ortak, ad) | — |
| EDI ayarları | `Edi` ise evet | §4.2; mesaj listeleri §8.3 kümesinden, tekrarsız; kimlikler 1..35 | — | — |
| API `outboundBaseUrl` | `Api` ise evet | mutlak `https://`, özel / yerel ağ adresi (127.0.0.0/8, 10/8, 172.16/12, 192.168/16, ::1, link-local) **reddedilir** (SSRF) | — | DNS çözümü gönderimde de denetlenir |
| Dosya ayarları | `File` ise evet | §4.2; klasörler `/` ile başlar, `..` yok; parmak izi `SHA256:` + base64 | — | — |
| Sır `secretName` / değer | Evet | türe göre izinli ad (§4.2) / 1..16384 (sertifika PEM) | — | kasa yazımı (503) |
| Eşleme `kind` / `ourRef` / `partnerValue` | Evet | enum / türüne göre geçerli kimlik / 1..64 | tekil kısmi | SKU / LOCATION okuması |
| Gelen mesaj gövdesi | Evet | ≤ 5 MB; 3PL-BUNDLE şeması (API), dosya düzeni, EDI zarfı | `DedupKey` tekil | imza (401) |
| Toplu yeniden işleme `exceptionIds` | Evet | 1..200 uuid, tekrarsız; aynı ortak + aynı tür | — | `TPL_BULK_LIMIT`, `TPL_BULK_MIXED` |
| Yok sayma gerekçesi | Evet | 10..500 | — | — |
| Sorumlu ata `assigneeUserId` | Evet | kiracı kullanıcısı | — | — |
| Bekletilen işleme `messageIds` / `all` / `confirmOutOfOrder` | biri | 1..200 uuid ya da `all = true` (geliş sırası, 200'lük partiler) / bool | — | §8.6 |
| Ön bildirim `purchaseOrderId` / `warehouseId` | Evet | PO açık; depo ortağın deposu | — | PO okuması |
| Mutabakat notu / miktar | Evet / Hayır | 1..1000 / ondalık, işaretli | — | — |
| Çözüldü gerekçesi | Evet | 10..500 | — | — |
| Idempotency-Key | Evet (her yönetim yazması) — gelen uçta `X-TPL-Message-Id` | 8–100 karakter | tekil index | If-Match / durumdan önce |
| If-Match | Evet (değişebilir kayıtta yaratma dışı) — adlı muafiyet: kanal teknik sinyal alanları (§4.2) | `W/"<version>"` | — | — |
| TenantId / LegalEntityId gövdede | Hayır | gelirse 400 | — | — |

## 13. Failure Path to Verify
- **Duplicate (aynı Idempotency-Key, aynı gövde)** — Expected: ilk cevap aynen; yeni kayıt / merkezi kayıt / numara yok.
- **Aynı anahtar, farklı gövde** — Expected: 400 `IDEMPOTENCY_KEY_REUSED`, yazım yok.
- **Aynı 3PL mesajı ikinci kez (aynı `DedupKey`)** — Expected: mesaj `Duplicate`, istisna `Ignored`; ikinci GRN / dispatch / mutabakat yok.
- **Missing alan** (gerekçe, depo, kanal ayarı) — Expected: 400 validator mesajı.
- **Unauthorized actor** — Expected: 403; UI eylemi gizli; sayfa izni yoksa iskelet yok (UAS-001); ham içerik izni yoksa ham bölüm yok.
- **Concurrency (eski If-Match)** — Expected: 409 `TPL_CONFLICT`; "Kayıt değişti, yenileyin."; sessiz üzerine yazma yok.
- **Başka kiracı / şirket kaydı** — Expected: 404 (403 değil).
- **Gelen uç: yanlış imza / bilinmeyen anahtar / ± 5 dk dışı zaman / başka kiracının anahtarı** — Expected: 401; mesaj kaydı dahil hiçbir yazım yok.
- **Kapalı kanala geçerli imzalı mesaj** — Expected: 409 `TPL_CHANNEL_CLOSED`; mesaj `Rejected`, `ChannelClosed` istisnası; işlenmez.
- **Askıdaki ortağa gelen mesaj** — Expected: `Held`; işlenmez; etkinleştirmeden sonra da kendiliğinden işlenmez.
- **Askıdayken bekletilenleri işleme** — Expected: 422 `TPL_PARTNER_SUSPENDED`.
- **Sıra dışı işleme onaysız** — Expected: 422 `TPL_OUT_OF_ORDER_UNCONFIRMED`; yazım yok.
- **Süresi dolmuş bekletilen** — Expected: 422 `TPL_HELD_EXPIRED`; kayıt silinmez.
- **201 kayıt ya da karışık tür toplu yeniden işleme** — Expected: 422 `TPL_BULK_LIMIT` / `TPL_BULK_MIXED`; hiçbir kayıt işlenmez.
- **3PL lotu / SKT'si bizdekinden farklı** — Expected: `LotExpiryMismatch`; lot ve SKT kaydımız değişmez; GRN / dispatch yok.
- **Eşlemesiz ürün kodu** — Expected: `UnknownProductMapping`; eşleme eklenince "Eşleme ekle ve yeniden işle" aynı koddaki açıkları işler.
- **Talimattan fazla / az onay** — Expected: `QuantityMismatch`; deftere yansımaz (Q-19 teyidi).
- **Kapanmış belgeye onay** — Expected: `LateConfirmation`; yazım yok.
- **MDM erişilemez (ortak oluşturma)** — Expected: 503 `TPL_MDM_UNAVAILABLE`; yazım yok. Listede kod + rozet.
- **Kasa erişilemez (sır yazma)** — Expected: 503 `TPL_VAULT_UNAVAILABLE`; `SetAt` değişmez.
- **3PL depoya iç toplama / yerleştirme** — Expected: 0178 toplama görevi açmaz; yerleştirme 422 `WMS_EXTERNAL_WAREHOUSE`; iç depoda davranış aynı.
- **Ortak askıdayken release** — Expected: release yazılır; talimat mesajı `NotSent`; istisna; etkinleştirmede `Pending`.
- **Özel ağ adresine giden URL** — Expected: 400 (kayıtta) ve gönderimde ret (DNS yeniden bağlama).
- **SFTP ana makine anahtarı değişti** — Expected: `ConnectionAuth`; dosya okunmaz.
- **Fazladan PUT / DELETE ucu** — Expected: `LedgerImmutabilityArchitectureTests` kırmızı.

## 14. Authorization Convention

**Namespace (Q-1 — CT önerisi):** `warehouse.third-party-logistics.*` — PKS-001 `warehouse.*` altında MOD-0179'a ayrılmış **adlı alt ad alanı**
(`.antigravity/rules/permission-key-standard.md:92` istisna kuralı; bugünkü örnekler `warehouse.waves.*` → MOD-0180, `warehouse.barcode.*` → MOD-0182,
`:106-107`). `warehouse.3pl.*` **olamaz**: kesim dilbilgisi `^[a-z][a-z0-9-]*$` (`:40`) rakamla başlayan kesimi reddeder. Kural gereği alt ad alanı iki
pakette de kayıtlı sahip kararıyla açılır: bu paket (§14, Q-1) + MOD-0178 §14.1 notu (X-17). `.antigravity` kapısı paket onayından önce kapanır (§6).

**Fiiller:** yalnız mevcut fiiller (`read`, `create`, `update`, `activate`, `deactivate`, `configure`, `export`); **yeni fiil yok** (Rol İzinleri köprüsü — X-15).
"Askıya al" UI etiketidir, anahtar `deactivate`'tir.

| Anahtar | Tier | Dilim | Anlam |
|---|---|---|---|
| `warehouse.third-party-logistics.partners.read` | 1 | DL-1 | sağlık, KPI, ortak listesi ve ayrıntısı, performans (DL-7) |
| `….partners.create` / `.update` | 1 | DL-1 | ortak bağla, düzenle |
| `….partners.activate` / `.deactivate` | 2 | DL-1 | etkinleştir / askıya al (gerekçe) |
| `….channels.read` | 1 | DL-1 | kanal özeti (sırsız) |
| `….channels.configure` | 3 | DL-1, DL-4 | kanal oluştur / düzenle / aç / kapat, sır ayarla, bağlantıyı sına (mockup "3PL · Kanal ayarı" — K-5) |
| `….mappings.read` / `.update` | 1 | DL-1 | eşlemeler (oluştur, değiştir, pasifleştir) |
| `….messages.read` | 1 | DL-1 | mesaj listesi ve ayrıntısı (ham içerik hariç) |
| `….raw-messages.read` | 2 | DL-1 | ham içerik (maskeli) — mockup "3PL · Ham mesaj" (K-5) |
| `….messages.update` | 1 | DL-1 | bekletilenleri işle / at |
| `….exceptions.read` / `.update` | 1 | DL-1 | istisna kutusu; yeniden işle (tek / toplu), eşleme ekle ve yeniden işle (ayrıca `mappings.update`), yok say, sorumlu ata |
| `….instructions.read` / `.create` | 1 | DL-5 / DL-6 | talimatlar; giriş ön bildirimi gönder |
| `….reconciliation.read` / `.update` / `.export` | 1 / 1 / 2 | DL-7 | mutabakat masası; not, inceleniyor, çözüldü; CSV |
| `….settings.read` / `.configure` | 1 / 3 | DL-1 | eşik ayarları (M-2) |

**Gelen uç:** kullanıcı izni yoktur; `[AllowAnonymous]` + imza süzgeci (`[ThirdPartySignature]`). 0179'un kendi mimari testi `Controllers.ThirdPartyLogistics`
namespace'inde `AllowAnonymous` taşıyan eylemlerin **yalnız** adlı listede (`ThirdPartyInboundController.Receive`, Q-2 = AS2 ise `…ReceiveAs2`) olduğunu ve
hepsinin imza süzgeci taşıdığını ölçer (AC-7) — InventoryService `AuthorizationGuardTests` `AllowAnonymous`'u kendiliğinden geçirir
(`services/Diten.InventoryService/tests/Diten.InventoryService.Tests/Architecture/AuthorizationGuardTests.cs:54`), bu yüzden bekçi 0179'dadır.

**Politika ve aktör:** `[Authorize]` (tenant shell) + her yönetim eyleminde `[HasPermission(ThirdPartyLogisticsPermissions.X)]`. Aktör `tenant_user`; gelen uçta
aktör kanal kimliği (`channel:{channelId}`), işçilerde sistem kimliği. **Rol şablonu önerisi** (yalnız yeni kiracılar; X-16, Q-13 — Ali): **Entegrasyon
sorumlusu** = `partners.*` + `channels.*` + `mappings.*` + `messages.*` + `raw-messages.read` + `exceptions.*` + `settings.*`; **Stok / mutabakat sorumlusu** =
`partners.read` + `reconciliation.*` + `exceptions.read`; **Depo yöneticisi** = `partners.read` + `messages.read` + `exceptions.read` + `reconciliation.read` +
`instructions.*`. Rol adları öneri.

**Manifest:** ModuleCode `THIRD-PARTY-LOGISTICS`, SortOrder 179; Pages `TPLHEALTH` (`partners.read`; eylemler create / update / activate / deactivate),
`TPLCHANNELS` (`channels.read`; eylem `configure`), `TPLMAPPINGS`, `TPLMESSAGES` (eylem `raw-messages.read`), `TPLEXCEPTIONS`, `TPLRECON` (DL-7), `TPLSETTINGS`.
Manifest yalnız `warehouse.third-party-logistics.*` bildirir (`ManifestNamespaceOwnershipTests` deseni). MDM izinleri (`mdm.business-partners.*`) MDM'nin
kendi manifestindedir (X-1).

## 15. Gateway / API Routing Decision
- **Karar (InventoryService):** uçlar `/api/warehouse/third-party-logistics/…` — mevcut `/api/warehouse/{everything}` → InventoryService rotasının altında
  (`gateway/Diten.ApiGateway/ocelot.json:1153-1161`; taban `:1134-1142`). **InventoryService için `ocelot.json` değişmez.** Rotada `AuthenticationOptions`
  yoktur; JWT'siz gelen uç Gateway'den geçer, kimlik servisteki imza süzgecindedir (ölçüm — DL-10 testi bunu sabitler).
- **MDM:** `/api/mdm/business-partners` ve `/{everything}` için **yeni rota gerekir** (MDM rotaları kaynak başınadır, catch-all yok — `:3819` …) — X-2,
  integration-agent (T-6), DL-3.
- Gerekli: integration-agent'ın 0179 uç kapsama testi `gateway/Diten.ApiGateway.Tests/Mod0179ThirdPartyRouteTests.cs` (emsal `Mod0178WarehouseRouteTests.cs`);
  test üretim `ocelot.json`'u ve üretim controller kaynağını okur (uç listesi elle yazılmaz); gelen ucun `Authorization` başlığı olmadan iletildiğini ve
  `X-TPL-*` başlıklarının servise ulaştığını ölçer (ölçülmedi — DL-10'un ilk ölçümü).
- **0178 Gateway testinin uç sayısı (X-8):** 0179 uçları ilk dilimde (DL-1) `Mod0178WarehouseRouteTests`'in sayı satırını kırmızı yapar; uç ekleyen her dilim
  satırı uçlarıyla aynı commit'te günceller (integration-agent; sahip onayı dilim başlamadan).
- **Dış erişim:** gelen ucun internete açılması (TLS, IP izin listesi, hız sınırı) dağıtım işidir; v1 kabulünde yerel ve pilot ortamda ölçülür, üretime açma ayrı
  karar (FU-0179-EDGE).
- İletilen başlıklar: `Authorization`, `X-Legal-Entity-Id`, `X-Correlation-Id`, `Idempotency-Key`, `If-Match`, `Accept-Language`, `X-TPL-Timestamp`,
  `X-TPL-Signature`, `X-TPL-Message-Id`. Dev dilimi `ocelot.json`'a yazmaz.

## 16. Acceptance Criteria

Her guard maddesi **üretim koduna karşı** ölçülür ve sabotaj kanıtı ister: ilgili üretim satırı geçici bozulur, test kırmızıya döner, bozma geri alınır
(kendi kopyasıyla ölçen test guard sayılmaz). Sabotaj kimliği `S-n`; kabul kaydı her `S-n` için komut + kırmızı çıktı + geri almayı yazar. AC → test →
sabotaj eşlemesi §17.1'dedir. **Bekçi olmayan maddeler:** AC-27 (`verify_datatable_page.py`), AC-33 (375 px ve durumlar, tarayıcı ölçümü) — kanıtları
araç çıktısı / ekran ölçümüdür.

**DL-1 — 3PL-BUNDLE taslağı + ortak, kanal, eşleme, mesaj defteri, istisna kutusu, eşikler (arka uç)**
- [ ] AC-1 (zorunlu) — 3PL-BUNDLE taslağı (`docs/analysis/contracts/drafts/3pl-bundle-v1-proposal.md`, yazılacak) §8.16 içeriğiyle; sözleşme testi controller cevaplarını ve kanonik şemayı taslağa karşı kapalı şemayla doğrular. S-1: bir cevaptan zorunlu alanı kaldır → kırmızı.
- [ ] AC-2 (zorunlu) — Ortak bağlama MDM'de etkin + `ThirdPartyLogistics` rollü ortağı ister (yoksa 422 `TPL_PARTNER_NOT_3PL`, bilinmeyen 404, MDM yok 503); `PartnerCode` kopyalanır; `ThirdPartyPartner` tipinde ad / adres / iletişim / vergi no alanı yoktur (mimari test, yansıma). S-2a: rol denetimini kaldır → kırmızı; S-2b: varlığa `Name` alanı ekle → mimari test kırmızı.
- [ ] AC-3 (zorunlu) — Depo kuralı: `external-integrated` olmayan depo 422 `TPL_WAREHOUSE_NOT_EXTERNAL`; başka etkin ortağın deposu 409 `TPL_WAREHOUSE_TAKEN`; askıdaki ortağın deposu da alınamaz. S-3a: `OperatingMode` denetimini kaldır → kırmızı; S-3b: tekillik denetimini kaldır → eşzamanlı iki bağlama vakası (görevli kanca) kırmızı.
- [ ] AC-4 (zorunlu) — Ortak ve kanal durum makineleri §8.2 tam; gerekçesiz geçiş 400; geçersiz 409 `TPL_INVALID_STATE`; silme ucu ve komutu yok. S-4: kanal kapatmada gerekçe denetimini kaldır → kırmızı.
- [ ] AC-5 (zorunlu) — Kiracı / şirket izolasyonu: başka kiracıdan ve aynı kiracıda başka şirketten her yönetim ucu 404; listeler yalnız kendi şirketi. S-5a: `TenantId` yüklemini düşür → kırmızı; S-5b: `LegalEntityId` yüklemini düşür → kırmızı.
- [ ] AC-6 (zorunlu) — Gövde ya da sorguda `tenantId` / `legalEntityId` → 400. S-6: denetimi kaldır → kırmızı.
- [ ] AC-7 (zorunlu) — Her yönetim eylemi `[HasPermission(ThirdPartyLogisticsPermissions.*)]`; `AllowAnonymous` yalnız adlı gelen eylem(ler)de ve imza süzgeciyle; manifest yalnız uygulanan `warehouse.third-party-logistics.*` anahtarlarını bildirir; X-9 dışlamasıyla `InventoryLedgerManifestCompletenessTests` yeşil + 0179'un kendi bütünlük vakası. S-7a: bir eylemden `HasPermission`'ı kaldır → `AuthorizationGuardTests` kırmızı; S-7b: bir yönetim eylemine `AllowAnonymous` ekle → 0179 bekçisi kırmızı; S-7c: manifestten bir anahtarı kaldır → bütünlük vakası kırmızı.
- [ ] AC-8 (zorunlu) — Idempotency ve If-Match: aynı anahtar + aynı gövde → ilk cevap, yeni yazım / merkezi kayıt yok; farklı gövde 400; eski If-Match 409; anahtar araması If-Match / durum / tekillik denetimlerinden önce. S-8a: anahtar aramasını tekillikten sonraya al → replay vakası 409 verir, kırmızı; S-8b: If-Match denetimini kaldır → kırmızı.
- [ ] AC-9 (zorunlu) — Kanal kaydı türe göre doğrulanır; kanal DTO'su sır için yalnız `{ name, isSet, setAt }` taşır; sahte kasaya yazılan nöbetçi değer hiçbir cevapta, logda, merkezi kayıtta, idempotency deposunda ve Inventory koleksiyonunda yoktur (tarama). S-9a: DTO'ya değer alanı ekle → tarama kırmızı; S-9b: parmak izine sır alanını kat → depo taraması kırmızı.
- [ ] AC-10 (zorunlu) — Kapalı kanala gelen mesaj `Rejected` + `ChannelClosed` istisnası, işlenmez (M-16). S-10: kapalı kanal denetimini kaldır → mesaj işlenir, kırmızı.
- [ ] AC-11 (zorunlu) — Eşleme tekilliği iki yönde; pasif eşleme kullanılmaz; "Eşleme ekle ve yeniden işle" aynı ortak + koddaki açık `UnknownProductMapping` istisnalarını (≤ 200) işler, başka ortağınkileri işlemez. S-11: ortak süzgecini kaldır → başka ortak vakası kırmızı.
- [ ] AC-12 (zorunlu) — Kanonik işlemci: aynı `DedupKey` → `Duplicate` + kendiliğinden `Ignored` istisna; ikinci yan etki yok. S-12: tekrar denetimini kaldır → ikinci işleme vakası kırmızı.
- [ ] AC-13 (zorunlu) — Askıdaki ortak: gelen `Held` (`HeldUntil` = +30 gün, sahte saat), giden `NotSent`; etkinleştirme kendiliğinden işlemez; geliş sırası ve `outOfOrder` onayı; askıdayken işleme 422; süresi dolan 422 ve silinmez. S-13a: etkinleştirmede bekletilenleri kendiliğinden işle → kırmızı; S-13b: sıra denetimini kaldır → sıra dışı vaka kırmızı.
- [ ] AC-14 (zorunlu) — İstisna yaşam döngüsü §8.2; yok sayma gerekçesiz 400; sorumlu atama kiracı kullanıcısı ister. S-14: `Ignored` → `Open` geçişine izin ver → kırmızı.
- [ ] AC-15 (zorunlu) — Toplu yeniden işleme 1..200, aynı ortak + aynı tür; 201 → 422 `TPL_BULK_LIMIT`; karışık → 422 `TPL_BULK_MIXED`; her kayıt kendi sonucu. S-15a: sınırı 1000 yap → 201 vakası kırmızı; S-15b: karışık denetimini kaldır → kırmızı.
- [ ] AC-16 (zorunlu) — Lot / SKT farkı (T-3) `LotExpiryMismatch`; MOD-0174 koleksiyonlarında yazım yok (depo taraması), onay deftere yansımaz. S-16: farkta 3PL SKT'sini lot kaydına yazan dal ekle → tarama kırmızı.
- [ ] AC-17 (zorunlu) — `UnknownProductMapping`, `UnknownLot`, `QuantityMismatch`, `LateConfirmation`, `Format`, `Unsupported` her biri ayrı vaka ile üretilir; istisna metni 7 dilde koddan. S-17: miktar karşılaştırmasını kaldır → `QuantityMismatch` vakası kırmızı.
- [ ] AC-18 (zorunlu) — Sağlık ve KPI §8.11 kuralıyla (sahte saat, eşik sınırında iki yön). S-18: `StockReportMaxAgeHours` karşılaştırmasını ters çevir → kırmızı.
- [ ] AC-19 (zorunlu) — Eşikler: varsayılanlar §4.1.1, aralık dışı 400, `settings.configure` olmadan 403. S-19: aralık denetimini kaldır → kırmızı.
- [ ] AC-20 (zorunlu) — Merkezi denetim: başarılı ve yazan her komut 1 kayıt, replay 0, başarısız 0; X-6 sayıları aynı commit'te. S-20a: bir komuttan işareti kaldır → `CentralAuditDeclarationTests` kırmızı; S-20b: replay'de kayıt yazdır → kırmızı.
- [ ] AC-21 (zorunlu) — İstisna numarası `{ŞİRKET}-TPX-` + 6 hane; değiştirilemez; eşzamanlı iki istisnada farklı numara. S-21: `TPX`'i `TypePrefixes`'ten çıkar → kırmızı.
- [ ] AC-22 (zorunlu) — 0179 Persistence başka modül koleksiyonunda yalnız `Find` çağırır (yazma taraması); `ThirdPartyLogisticsCollections.All` SSOR testinde (X-5); varlıklarda bakiye alanı yok. S-22a: başka modül koleksiyonuna `InsertOne` ekle → tarama kırmızı; S-22b: koleksiyonu bildirimden çıkar → SSOR testi kırmızı.
- [ ] AC-23 (zorunlu) — PUT / DELETE ucu yok. S-23: bir `[HttpDelete]` ekle → `LedgerImmutabilityArchitectureTests` kırmızı.
- [ ] AC-24 (zorunlu) — Ham içerik `raw-messages.read` ister; maskeleme §8.7 (ad, telefon, e-posta, adres); imza / sır alanı yok. S-24a: izni `messages.read`'e düşür → 403 vakası kırmızı; S-24b: maskeyi kaldır → maskeleme vakası kırmızı.
- [ ] AC-25 (zorunlu) — `Mod0178WarehouseRouteTests` sayı satırı dilimin uçlarıyla aynı commit'te (X-8); Gateway test projesi dilim kabulünde yeşil. S-25: sayı satırını eski değere döndür → kırmızı.

**DL-2 — Web ekranları (sağlık, ortak ayrıntısı, kanallar, eşlemeler, mesajlar, bekletilenler, istisna kutusu, ayarlar)**
- [ ] AC-26 (zorunlu) — Her sayfa görünümünde `Layout = "_LayoutTenantShell";` açıkça. S-26: bir sayfadan satırı kaldır → L10n / Layout testi kırmızı.
- [ ] AC-27 — `verify_datatable_page.py --area Warehouse --module {Module} --format gaps` her liste ekranında PASS ya da sapmalar sahibine soruldu (bekçi değil — araç çıktısı).
- [ ] AC-28 (zorunlu) — RESX parite 7 dil (en, tr, fr, es, zh, ar, ru); Almanca yok (M-7). S-28: bir anahtarı `zh`'den sil → parite testi kırmızı.
- [ ] AC-29 (zorunlu) — UAS-001: izinsiz kullanıcıda menü ve üst çubuk kalır, içerikte tek açıklama; iskelet, yönlendirme, yalnız toast yok. S-29: yetkisiz dalda iskeleti çiz → kırmızı.
- [ ] AC-30 (zorunlu) — İzne göre çizim: `raw-messages.read` yoksa ham bölüm yok; `channels.configure` yoksa kanal düzenleme yok ve "salt okunur" notu; sır değeri hiçbir HTML'de yok. S-30: ham bölümü izinsiz çiz → vitest / view testi kırmızı.
- [ ] AC-31 (zorunlu) — Toplu seçim: 200'den fazla seçimde uyarı ve yalnız ilk 200 gönderilir; karışık seçimde düğme devre dışı + metin. S-31: sınırı JS'den kaldır → vitest kırmızı.
- [ ] AC-32 (zorunlu) — Sayfa yardımı her sayfada (§25), tur yok; sır cümlesi `TPLCHANNELS` yardımında. S-32: bir sayfanın yardım anahtarını kaldır → yardım testi kırmızı.
- [ ] AC-33 — 375 px'te liste ve istisna kutusu okunur, yatay kaydırma yok; boş / yükleniyor / hata / yetkisiz durumları; Arapça RTL (bekçi değil — tarayıcı ölçümü).
- [ ] AC-34 (zorunlu) — Ortak formunda ad / adres / iletişim girdi alanı yoktur; seçim listesi yalnız MDM'nin 3PL rollü etkin ortakları; ayrıntıda MDM bilgisi salt okunur. S-34: forma ad girdisi ekle → view testi kırmızı.

**DL-3 — MDM iş ortağı (MdmService; DL-1'den bağımsız)**
- [ ] AC-35 (zorunlu) — `BusinessPartner` varlığı, uçları ve `ThirdPartyLogistics` rolü (§4.10); MDM'de mevcut dosyalarda yalnız adlı kayıt satırları değişir; MDM test paketleri değişmeden yeşil. S-35a: rol süzgecini kaldır → rol vakası kırmızı; S-35b: `ShortName` tekilliğini kaldır → kırmızı.
- [ ] AC-36 (zorunlu) — `mdm.business-partners.*` izinleri; kiracılar arası 404; yol b merkezi denetim (MDM defteri); pasifleştirme gerekçeli, silme yok. S-36: `TenantId` yüklemini düşür → kırmızı.
- [ ] AC-37 (zorunlu) — Kod Q-11 önekiyle, grup önekli, OTOMATİK, değişmez. S-37: kodu güncellemeye izin ver → kırmızı.
- [ ] AC-38 (zorunlu) — Gateway MDM rotaları (X-2) ve rota testi; MDM ekranı Slim, 7 dil, `_LayoutTenantShell`. S-38: rotayı kaldır → rota testi kırmızı.

**DL-4 — sır kasası bağlantısı + API kanalı (kapı: Platform kiracı sır kasası)**
- [ ] AC-39 (zorunlu) — Sır yalnız kasa portundan yazılır; Inventory'de yalnız `VaultRef` + `SetAt`; gerçek kasa ile nöbetçi değer taraması (AC-9'un gerçek kasa sürümü). S-39: `VaultRef` yerine değeri sakla → tarama kırmızı.
- [ ] AC-40 (zorunlu) — Gelen uç: doğru imza 202 + mesaj; yanlış imza, bilinmeyen anahtar, ± 5 dk dışı zaman, başka kiracının anahtarı → 401 ve **sıfır yazım** (mesaj dahil); aynı `X-TPL-Message-Id` ikinci kez → `Duplicate`. S-40a: imza karşılaştırmasını kaldır → kırmızı; S-40b: zaman penceresini kaldır → kırmızı; S-40c: imza hatasında mesaj kaydı yaz → sıfır yazım vakası kırmızı.
- [ ] AC-41 (zorunlu) — Gönderici: 5 deneme, üstel bekleme (sahte saat), sonra `Failed` + `ConnectionAuth`; askıdaki ortak / kapalı kanal `NotSent`; özel ağ adresine gönderim reddi. S-41a: askı denetimini kaldır → kırmızı; S-41b: SSRF denetimini kaldır → kırmızı.
- [ ] AC-42 (zorunlu) — "Bağlantıyı sına" sonucu sır içermez; `LastTestAt` / `LastTestResult` `Version`'ı artırmaz. S-42: sonuç metnine yanıt gövdesini ekle → nöbetçi taraması kırmızı.

**DL-5 — çıkış akışı (MOD-0178 / MOD-0183 seam'i)**
- [ ] AC-43 (zorunlu) — `external-integrated` depoda allocation toplama görevi açmaz; release işleminde talimat + mesaj aynı işlemde (release başarısızsa ikisi de yok); iç depoda davranış aynı; `INVT/Warehouse/**` ve `INVT/CycleCount/**` diff'i boş. S-43a: depo modu denetimini kaldır → görev açılır, kırmızı; S-43b: talimatı işlem dışına al → geri alma vakası kırmızı.
- [ ] AC-44 (zorunlu) — Sevk onayı → outbound `ReadyToShip` (`ConfirmationChannel.External`, 3PL lotu / miktarı, 0178 lot / SKT / geri çağırma kuralları aynen) → X-12 yolu → `Shipped` bugünkü sync-dispatch ile; 0179 hiçbir hareket yazmaz (yazma taraması). S-44a: dış geçişte geri çağırma kuralını atla → geri çağrılmış lot vakası kırmızı; S-44b: 0179'dan `GOODS_ISSUE_SALES` post et → tarama kırmızı.
- [ ] AC-45 (zorunlu) — Fazla / eksik onay `QuantityMismatch`, kapanmış belgeye onay `LateConfirmation`; ikisinde de geçiş yok. S-45: miktar denetimini kaldır → kırmızı.

**DL-6 — giriş akışı (MOD-0142 seam'i)**
- [ ] AC-46 (zorunlu) — Ön bildirim PO satırlarından; kapalı PO 422; aynı PO + depo için ikinci etkin ön bildirim 409. S-46: tekillik index'ini kaldır → kırmızı.
- [ ] AC-47 (zorunlu) — Teslim onayı → X-13 yolu → GRN (`sourceSystem`, `externalRef`, Idempotency-Key = mesaj kimliği); aynı mesajın yeniden işlenmesi ikinci GRN üretmez; 0179 `GOODS_RECEIPT_PO` yazmaz; ön bildirim lotu ile fark `LotExpiryMismatch`. S-47a: Idempotency-Key'i rastgele üret → ikinci GRN vakası kırmızı; S-47b: lot karşılaştırmasını kaldır → kırmızı.

**DL-7 — stok raporu + mutabakat masası + performans (arka uç ve ekran)**
- [ ] AC-48 (zorunlu) — Fark = bizim − 3PL; yoldaki M-4 kuralıyla işaretli (sevk +, ön bildirim −, yalnız `ReportedAt` öncesi gönderilmiş ve onaysız kalan miktar); açıklanmamış formülü; raporda olmayan / bizde olmayan satırlar §8.10. S-48a: yoldakinin işaretini ters çevir → kırmızı; S-48b: `ReportedAt` süzgecini kaldır → sonradan gönderilmiş talimat vakası kırmızı.
- [ ] AC-49 (zorunlu) — Satır durum makinesi; Çözüldü gerekçe ister; mutabakat masası stok hareketi yazmaz (yazma taraması); "Düzeltme öner" Q-4 kararına göre (A: düğme yok — view testi). S-49: gerekçe denetimini kaldır → kırmızı.
- [ ] AC-50 (zorunlu) — CSV `reconciliation.export` ister; formül enjeksiyonu kaçışı. S-50: kaçışı kaldır → `=1+1` vakası kırmızı.
- [ ] AC-51 (zorunlu) — Performans metrikleri §8.11 (sahte saat, 30 gün sınırı). S-51: pencereyi 31 gün yap → sınır vakası kırmızı.
- [ ] AC-52 (zorunlu) — Gecikmiş rapor sağlıkta Gecikmeli + bant; mutabakat ekranı Layout, 7 dil, yardım, UAS-001 (AC-26, AC-28, AC-29, AC-32 bu ekran için). S-52: bant koşulunu kaldır → view testi kırmızı.

**DL-8 — dosya kanalı (SFTP, CSV / XML)**
- [ ] AC-53 (zorunlu) — SFTP okuyucu desene uyan dosyayı okur, arşive taşır, silmez; dosya adı + sha256 tekrarı `Duplicate`; CSV ve XML düzeni kanonik modele (gerçek SFTP sunucusu test kabında). S-53a: arşive taşımayı kaldır → ikinci döngü vakası kırmızı; S-53b: CSV başlık denetimini kaldır → biçim vakası kırmızı.
- [ ] AC-54 (zorunlu) — Ana makine anahtarı parmak izi sabit; uyuşmazlıkta `ConnectionAuth`, okuma yok. S-54: parmak izi denetimini kapat → kırmızı.
- [ ] AC-55 (zorunlu) — Giden dosya `.tmp` + yeniden adlandırma; yarım dosya okunmaz. S-55: doğrudan son adla yaz → yarım dosya vakası kırmızı.

**DL-9 — EDI kanalı (EDIFACT + X12)**
- [ ] AC-56 (zorunlu) — EDIFACT v1 mesaj kümesi (Q-3) ayrıştırılır / üretilir; UNB / UNZ ve UNH / UNT sayaçları denetlenir; örnek değişimler sözleşme ekinden. S-56: UNT sayaç denetimini kaldır → kırmızı.
- [ ] AC-57 (zorunlu) — X12 v1 kümesi (Q-3); ISA / IEA, GS / GE, ST / SE denetimi. S-57: SE sayaç denetimini kaldır → kırmızı.
- [ ] AC-58 (zorunlu) — Bir ortakta EDIFACT ve X12 kanalı birlikte (T-1), her biri kendi depolarında; yanlış kanala gelen depo kodu `Format`. S-58: kanal-depo denetimini kaldır → kırmızı.

**DL-10 — Gateway testi (integration-agent)**
- [ ] AC-59 (zorunlu) — `Mod0179ThirdPartyRouteTests` 0179 uçlarının tamamını üretim `ocelot.json`'unda eşler; gelen uç `Authorization`'sız iletilir, `X-TPL-*` başlıkları servise ulaşır. S-59: `ocelot.json`'da `/api/warehouse/{everything}`'den `POST`'u geçici çıkar → kırmızı (geri alınır).

## 17. Test Expectations
- **Birim (Domain, saf):** durum makineleri (§8.2); sağlık hesabı; yoldaki / fark / açıklanmamış formülü; maskeleme; imza doğrulama; SSRF adres denetimi;
  CSV kaçışı; EDIFACT / X12 zarf sayaçları.
- **Entegrasyon (Mongo replika seti):** InventoryService test paketi `INVENTORY_TEST_MONGO` yoksa kendi replika setini başlatır; AC-1…AC-25 sahte MDM ve sahte
  kasa portuyla; DL-5 vakaları gerçek MOD-0178 işleyicileriyle ve sahte MOD-0183 istemcisiyle (pilotta gerçek uç); DL-6 sahte GRN / PO istemcisiyle (pilotta
  gerçek); DL-8 test kabında SFTP sunucusu; eşzamanlılık vakaları görevli kancayla; zaman vakaları sahte saatle.
- **Mimari:** AC-7 (`AuthorizationGuardTests` değişmeden + 0179 `AllowAnonymous` bekçisi + manifest bütünlüğü, X-9), AC-2 (varlıkta MDM verisi yok), AC-9 / AC-39
  (sır taraması), AC-20 (`CentralAuditDeclarationTests`, X-6), AC-22 (`SingleSystemOfRecordArchitectureTests` + yazma taraması, X-5), AC-23
  (`LedgerImmutabilityArchitectureTests`). Servise dokunulduğunda **bütün** InventoryService paketi koşar (filtre yok); DL-3'te bütün MdmService paketleri.
- **MOD-0178 davranışı:** X-10'un ölçümü 0179 test ağacında; 0178 test dosyaları değişmez (AC-43).
- **Frontend:** `verify_datatable_page.py` (AC-27), RESX parite 7 dil (AC-28), UAS-001 (AC-29), izne göre çizim (AC-30), toplu seçim vitest (AC-31), yardım (AC-32),
  375 px ve durumlar (AC-33), MDM salt okunur (AC-34), Layout (AC-26); tarayıcı smoke (E3): ortak bağla → kanal tanımla → (DL-4) sır ayarla → imzalı mesaj gönder →
  istisna kutusunda gör → eşleme ekle ve yeniden işle → mesaj işlendi.
- **Build:** `Diten.InventoryService`, `Diten.Web` PASS; DL-3'te `Diten.MdmService`; Gateway test projesi uç ekleyen her dilimde (X-8) ve DL-10'da.
- **Sözleşme:** 3PL-BUNDLE taslağına karşı sözleşme testi (AC-1); DL-5'te WMS-BUNDLE eklemeli sürümü (X-11); DL-9'da EDI örnek değişimleri.

### 17.1 AC → test → sabotaj eşlemesi
| AC | Test türü | Sabotaj |
|---|---|---|
| AC-1 | sözleşme | S-1 |
| AC-2 | entegrasyon + mimari | S-2a, S-2b |
| AC-3 | entegrasyon (görevli kanca) | S-3a, S-3b |
| AC-4 | birim + entegrasyon | S-4 |
| AC-5 | entegrasyon | S-5a, S-5b |
| AC-6 | entegrasyon | S-6 |
| AC-7 | mimari | S-7a, S-7b, S-7c |
| AC-8 | entegrasyon | S-8a, S-8b |
| AC-9 | entegrasyon (tarama) | S-9a, S-9b |
| AC-10 | entegrasyon | S-10 |
| AC-11 | entegrasyon | S-11 |
| AC-12 | entegrasyon | S-12 |
| AC-13 | entegrasyon (sahte saat) | S-13a, S-13b |
| AC-14 | birim + entegrasyon | S-14 |
| AC-15 | entegrasyon | S-15a, S-15b |
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
| AC-27 | araç (bekçi değil; sabotaj yok — araç çıktısı) | — |
| AC-28 | Web testi | S-28 |
| AC-29 | Web testi | S-29 |
| AC-30 | view + vitest | S-30 |
| AC-31 | vitest | S-31 |
| AC-32 | Web testi | S-32 |
| AC-33 | tarayıcı ölçümü (bekçi değil; sabotaj yok) | — |
| AC-34 | view testi | S-34 |
| AC-35 | MDM entegrasyon | S-35a, S-35b |
| AC-36 | MDM entegrasyon | S-36 |
| AC-37 | MDM entegrasyon | S-37 |
| AC-38 | Gateway + Web | S-38 |
| AC-39 | entegrasyon (gerçek kasa) | S-39 |
| AC-40 | entegrasyon (sahte saat) | S-40a, S-40b, S-40c |
| AC-41 | entegrasyon (sahte saat) | S-41a, S-41b |
| AC-42 | entegrasyon | S-42 |
| AC-43 | entegrasyon (gerçek 0178) + `git diff` | S-43a, S-43b |
| AC-44 | entegrasyon (gerçek 0178) + tarama | S-44a, S-44b |
| AC-45 | entegrasyon | S-45 |
| AC-46 | entegrasyon | S-46 |
| AC-47 | entegrasyon | S-47a, S-47b |
| AC-48 | birim + entegrasyon | S-48a, S-48b |
| AC-49 | entegrasyon + view | S-49 |
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

## 18. Ready-for-dev Checklist
- [x] Golden Reference Slim referans alındı; en büyük Slim form 5 alan; sapmalar G-0179-01 / G-0179-02 adıyla §10'da (CT kabulü Q-17)
- [x] Frontmatter zorunlu alanları dolu (service, shell, golden_reference, data_mode, screens, entity_base, approver, risk)
- [x] Layout & Shell Contract'ta `_LayoutTenantShell` açıkça yazılı (§9, AC-26 + S-26)
- [x] Backend File Convention Golden Reference ile birebir (§10)
- [x] Frontend File Contract tam (§11)
- [x] Validation Rules her alan için yazılı (§12)
- [x] Failure Path en az 4 senaryo (duplicate, missing, unauthorized, concurrency) (§13)
- [x] Authorization Convention: namespace önerisi, anahtarlar, politika, aktör (§14)
- [x] Gateway kararı: InventoryService için `ocelot.json` değişmez; MDM rotası X-2; integration-agent işleri tanımlı (§15, AC-59)
- [x] Acceptance criteria test edilebilir; her bekçi sabotajlı, bekçi olmayanlar adıyla işaretli (§16, §17.1)
- [x] Audited Events tablosu her yazma komutunu tam bir kez içeriyor (§21)
- [x] Sayfa yardım metni her sayfa için yazılı (§25); tur yok
- [x] Sahip kararları T-1…T-6, M-2, M-4, M-6, M-7, M-15, M-16 işlendi (kararlar 01); mockup v1 farkları §19.5
- [ ] **`.antigravity/**` kapısı — paket onayından ÖNCE:** PKS-001 alt ad alanı (Q-1), RCS-001 `TPX` + MDM öneki (Q-11), aynı adımda X-17 ve X-18; CT hazırlar, Beste
  (MDM öneki için Ali) onaylar; bu paket `.antigravity/**`'a yazmaz
- [ ] **Q-10 — MDM iş ortağı kimliği (Ali + CT, Master-first) — paket onayından ÖNCE**
- [ ] Sahip kararları (Beste): Q-1…Q-9
- [ ] Ali kararları: Q-10, Q-11, Q-12, Q-13
- [ ] CT kararları: Q-14…Q-17
- [ ] Başka ekip: Q-18 (MOD-0183 sahibi — DL-5 kapısı), Q-19 (procurement — DL-6 kapısı)
- [ ] Platform kiracı sır kasası paketi (Q-16) — DL-4 kapısı
- [ ] XMC-001 satırları (X-2…X-16) sahiplerinden onay aldı — ilgili dilim başlamadan; X-1 kapsam izni var (2026-11-13), dosya listesi teyidi DL-3 başında
- [ ] 3PL-BUNDLE taslağı (DL-1 ilk adımı); dondurma DL-4'ten önce (§8.16)
- [ ] Bağımsız inceleme (CT-APR-02) ve onay satırı (CT-APR-01) — **onaylayan Ali (GitHub `GMG-ERP`)**; yazan ≠ onaylayan

## 19. Implementation Notes

### 19.1 Okunan kaynaklar
- Sahip kararları 01: `docs/records/decisions/2026-11/mod0179-3pl-owner-decisions-01.md` (tamamı; T-1 `:9` … M-16 `:20`, ek `:23-32`).
- Brief: `execution/domains/supply-chain-execution/work-packs/mockups/3pl-integration/BRIEF-MOD-0179-3pl-integration.md` (tamamı).
- Mockup tur 1: `3PL Entegrasyonu MOD-0179 (tek dosya).html` — **depoda değil** (Beste'nin indirmeler klasöründe, 2 194 123 bayt, sha256
  `cee456d114387fe742837610cdbc24140e1ca40c86af1031b0b38c5a7e2ce0d8`); paketlenmiş şablondan görünür metin, açık sorular (T-1…T-6, M-1…M-16), önerilen kurallar
  (K-1…K-13), ekran metinleri, yardım metinleri ve örnek veri okundu. CT incelemesinin metni depoda yok; tur 2 düzeltme listesi kararlar 01 `:26-30`'dan.
- Blueprint 8.8 `Blueprint_Data` satır 180 (MOD-0179), 37 (MOD-0036), 38 (MOD-0037), 13 (MOD-0012), 50 (MOD-0049).
- Emsal paketler: MOD-0182 (tamamına yakını), MOD-0181 §0, MOD-0180 §0; MOD-0178 (`:178`, `:218-220`, `:2663`), MOD-0183 (`:52`, `:75-97`), MOD-0142 (`:1-24`, `:45`, `:63`),
  MOD-0037 (`:1-25`), MOD-0012 (`:1-22`).
- Kurallar: `.antigravity/rules/module-pack-standard.md` (Bölüm 16 plan hizalaması `:539-556`), `permission-key-standard.md` (`:40`, `:92`, `:97`, `:105-107`),
  `record-coding-standard.md` (`:22-44`, `:45-78`, `:102-124`), `audit-trail-standard.md` (İ1…İ5 `:233-237`).
- Kod: §19.2'deki her dosya.

### 19.2 Ölçülen gerçekler
- **MDM'de iş ortağı yok:** `services/Diten.MdmService/src/Diten.MdmService.Domain/Entities/` 52 dosya — ürün ailesi, marka, tüzel kişilik, denetim; `partner` /
  `supplier` geçen yalnız 3 dosya (`PlatformAuditForwarder.cs`, `LegalEntityLookupModels.cs`, `ProductIdentityLifecycleActorContext.cs`) ve hiçbiri varlık değil.
  MDM `EntityBase` `LegalEntityId` taşımaz (`EntityBase.cs:3-12`).
- Tedarikçi ana verisi ProcurementService'te (`Supplier.cs`, MOD-0140); GRN `POST /api/grn` (`GrnController.cs:23`, `:58-59`), PO `GET /api/purchase-orders/{poId}`
  (`PurchaseOrdersController.cs:48`).
- **Platform'da kiracı sır kasası yok:** `ISecretsProvider` yalnız okur (`ISecretsProvider.cs:3-7`); MOD-0012 korunan alanları çalışma zamanı yapılandırma sırlarıdır
  (`technical-reference.md:10-24`); `services/Diten.Platform` içinde `SecretVault` / `SecretStore` adlı tip yok.
- LOCATION `OperatingMode` var (`Location.cs:31`, `LocationHierarchyRules.cs:25-28`); MOD-0178 onu okumaz (`OperatingMode` geçen Inventory dosyaları yalnız `Location` ve
  Locations özelliği); toplama görevi allocation'da açılır (`OutboundAllocationService.cs:259`); `Shipped` yalnız `SyncOutboundDispatchHandler`'da (`OutboundFlowHandlers.cs:525`),
  ön koşul `ReadyToShip` (`:477`); `WarehouseTaskOrigin.External` ve `ConfirmationChannel.External` enumda var (`WarehouseTaskEnums.cs:26`, `:84`).
- Gateway: `/api/warehouse/{everything}` → InventoryService, `AuthenticationOptions` yok (`ocelot.json:1153-1171`); MDM rotaları kaynak başına (9 `/api/mdm/…` rotası,
  catch-all yok).
- Inventory bekçi değerleri (HEAD `0e8e29aab` / `f032a41a3`): `Mod0178WarehouseRouteTests.cs:97` = 73; `CentralAuditDeclarationTests.cs:53` = 117, `:130` = 14;
  `InventoryLedgerManifestCompletenessTests.cs:213-223` altı dışlama; `WarehouseRecordCodeRules.cs:28` beş önek; `AuthorizationGuardTests.cs:54` `AllowAnonymous`'u geçirir.
- Depoda SFTP / SSH / EDI / X12 / AS2 NuGet paketi yok (bütün `*.csproj` taraması).
- Onaylı stok düzeltme ucu / ekranı yok: InventoryService'te `api/inventory` dışında düzeltme rotası yok; hareket türleri `CountGain` / `CountLoss` MOD-0181 sayım farkı
  yolundan yazılır.
- PKS-001 kesim dilbilgisi `^[a-z][a-z0-9-]*$` (`permission-key-standard.md:40`) → `3pl` geçersiz.
- RCS-001 §3'te `TPX` yok.

### 19.3 Çıkarımlar
- Defterde "an itibarıyla" bakiye okuması olup olmadığı ölçülmedi — DL-7 başında ölçülür (§8.10).
- 3PL deposunda stoğun hangi lokasyonda durduğu (depo düğümü mü tek sanal bin mi) ölçülmedi — Q-9; 0178 allocation'ı bin düzeyinde çalıştığı için önemlidir.
- `BRIDGE-BASE` sözleşmesinin depoda karşılığı bulunmadı (dosya adı taraması) — çıkarım; Q-15.
- MOD-0183 dispatch ucunun servis kimliğiyle çağrılabilirliği ölçülmedi — Q-18.

### 19.4 Açık sorular (sahip / Ali / CT)
**Sahip (Beste):**
- **Q-1** İzin alt ad alanı adı. Öneri `warehouse.third-party-logistics.*` (`3pl` dilbilgisine aykırı). `.antigravity` kapısı — onaydan önce.
- **Q-2** EDI taşıma v1: AS2 mi, SFTP üzerinden EDI mi? Öneri **SFTP** (DL-8 altyapısı; AS2 sertifika / MDN yükü FU-0179-AS2). Mockup AS2 gösterdi.
- **Q-3** EDI mesaj listesi (T-1'in açık kalanı) ve fonksiyonel alındı. Öneri: EDIFACT INSDES, DESADV (iki yön), RECADV, INVRPT; X12 940, **943** (mockup 856), 944, 945, 846;
  CONTRL / 997 v1'de yok; ayrıştırıcı: v1 kümesine dar kendi ayrıştırıcı (lisans riski yok).
- **Q-4** "Düzeltme öner" hedefi — bugün onaylı stok düzeltme ekranı yok. Öneri **A:** v1'de düğme yok, "Çözüldü" yalnız gerekçeyle (FU-0179-ADJUST). B: 0179'da öneri + ayrı onay (mockup K-2 ile çelişir).
- **Q-5** Ekran rotaları: öneri `/Warehouse/ThirdParty*` (brief `/Integrations/ThirdPartyLogistics`).
- **Q-6** Varsayılan eşikler (M-2'nin açık kalanı): öneri sevk onayı 120 dk, giriş onayı 240 dk, stok raporu 26 sa, hata hedefi %1.
- **Q-7** Askıdayken bekletilenler işlenebilir mi (mockup M-13): öneri **hayır**, önce etkinleştirilir.
- **Q-8** Giriş ön bildirimi tetiği: öneri v1 **elle** (PO seç); PO onayında kendiliğinden gönderim FU-0179-AUTOADVICE.
- **Q-9** 3PL deposunda stok lokasyonu: öneri depo başına tek stoklanabilir lokasyon (3PL raflarını kendi yönetir) — MOD-0173-FU02 ile.

**Ali:**
- **Q-10** MDM iş ortağı kimliği (Master-first). MDM'de iş ortağı yok; T-5 kararı ve izni var, ama yeni bir MDM ana nesnesi doğuyor. A) bu paketin DL-3'ü olarak, MOD-0179
  kimliğiyle, dar kapsam (yalnız `ThirdPartyLogistics` rolü; varlık genel adlı, ileride MDM modülüne devredilebilir) — **öneri**; B) Master'da MDM modülü (ör. Blueprint
  `MOD-0049` altında çocuk) — Module Change Request, numara Master'dan; DL-3 bekler. Tedarikçi (MOD-0140) ve CRM müşterisiyle mükerrer ana veri riski not edilir.
- **Q-11** MDM iş ortağı kayıt kodu öneki (kiracı geneli → grup öneki). DC-03'ün tedarikçi `SUP-` kararıyla ilişkisi (aynı ortak hem tedarikçi hem 3PL olabilir).
- **Q-12** Servis kimliği: Inventory → SupplyChain (dispatch) ve → Procurement (GRN) çağrıları için servis kimliği ve izinleri (MOD-0033-FU02).
- **Q-13** Rol şablonları (§14).

**CT:**
- **Q-14** Yüksek hacimli mesaj komutlarının merkezi denetim yolu (b mı, mesaj defteri c mi) — MOD-0182 Q-16 ile birlikte.
- **Q-15** Blueprint sapmaları W-0179-01 (MOD-0037), W-0179-02 (MOD-0036) ve `BRIDGE-BASE`'in karşılığı.
- **Q-16** Platform kiracı sır kasası: MOD-0012 FU'su (numara registry sürecinden — uydurulmaz), paketi ve sahibi (merkez / Beste, T-4). DL-4 kapısı.
- **Q-17** Golden sapmaları G-0179-01 / G-0179-02.

**Başka ekip:**
- **Q-18** MOD-0183 sahibi (MVP-6 hattı): 3PL sevk onayıyla dispatch — servis kimliğiyle çağrı mı, MOD-0183'te başka yol mu (X-12). DL-5 kapısı.
- **Q-19** Procurement sahibi: 3PL teslim onayından GRN — servis kimliğiyle `POST /api/grn` mı; miktar farkında GRN açılır mı (öneri: açılmaz, istisna) (X-13). DL-6 kapısı.

### 19.5 Mockup v1 → paket farkları
| Mockup | Paket | Kaynak |
|---|---|---|
| Ücret mutabakatı ekranı, izni, itiraz (M-10…M-14, K-10, K-11) | **yok**; genişleme noktası §8.17; Treasury notu | T-2 |
| 7 dil TR, EN, DE, FR, RU, ES, AR | en, tr, fr, es, zh, ar, ru — Almanca çıkar, Çince girer | M-7 |
| "Ortak ekle" bu modülde ortak yaratır | MDM iş ortağından seçim; ad / adres / iletişim MDM'den, düzenlenmez | T-5, kararlar 01 :28-30 |
| Eşik ortak bilgisinde salt metin | ayrı ayar sayfası (`TPLSETTINGS`), ortak bazında, yetkiyle | M-2 |
| Kanal kapatma gerekçesiz | gerekçe zorunlu; kapalı kanala gelen mesaj ret + "kanal kapalı" istisnası | M-16 |
| Toplu yeniden işleme sınırsız | ≤ 200 + uyarı | M-6 |
| Sır satırı "Ayarlı · son değişim" | aynı; yardımda kasa cümlesi | T-4 |
| Yoldaki: varsayım | kural (M-4 kapandı) | kararlar 01 :24 |
| Rota `/Integrations/ThirdPartyLogistics/…` | `/Warehouse/ThirdParty*` (öneri) | Q-5 |
| "Düzeltme öner" | v1'de yok (öneri) | Q-4 |
| "Lot / SKT uyuşmazlığı" ipucu | aynı; bizdeki kayıt geçerli | T-3 |

## 20. Follow-up Items
- **FU-0179-TREASURY-NOTE** (CT işi, paket değil) — Treasury'ye bilgi notu: 3PL fatura verisinin kaynağı, karşılaştırılacak kalemler (depolama, elleçleme, soğuk zincir farkı,
  nakliye, iade işleme, ek hizmet — mockup M-10), satır bazı (M-12), itiraz kanalı (M-14), finans bağlantısı (T-2).
- **FU-0179-FEES** — ücret / fatura mutabakatı (v2; Treasury notunun cevabıyla).
- **FU-0179-MON** — MOD-0037'ye taşınma (W-0179-01). **FU-0179-EDIHUB** — MOD-0036'ya taşınma (W-0179-02).
- **FU-0179-AS2** — AS2 taşıması (Q-2 = SFTP ise). **FU-0179-ACK** — CONTRL / 997.
- **FU-0179-ADJUST** — onaylı stok düzeltmesi bağlantısı (Q-4).
- **FU-0179-CANCEL** — 3PL'e talimat iptali mesajı.
- **FU-0179-AUTOADVICE** — PO onayında kendiliğinden ön bildirim (Q-8).
- **FU-0179-RETURNS**, **FU-0179-TRANSFER** — iade ve depolar arası transfer akışları.
- **FU-0179-RCS** — `TPX`'in MOD-0040-FU02 ortak servisine geçişi.
- **FU-0179-EDGE** — gelen ucun üretimde internete açılması (TLS, IP izin listesi, hız sınırı).
- **FU-0179-PARTNER-GUIDE** (CT işi) — 3PL-BUNDLE dondurulunca ortaklara entegrasyon kılavuzu.
- **FU-0179-AI** — Blueprint AI Assist.

## 21. Audited Events

Servisin denetim altyapısı **bugün var**: `tests/architecture/audit-ledger/Diten.InventoryService.md:12` (`inventory-merkezi-iletim | b | işaret |
ICentralAuditDeclaration`). Kayıt ad ve gövde taşımaz; "olay adı" = `RequestType` (komut sınıfı) + `Operation`; `SourceModule` = `THIRD-PARTY-LOGISTICS`.
Başarılı ve yazan komut 1 kayıt; replay 0; başarısız 0. Sır değeri, imza ve ham içerik hiçbir kayda yazılmaz.

| Komut | Olay adı | Nesne (tür) | Yol | Önceki/sonraki ya da değişen alanlar | İstisna sınıfı + gerekçe |
|---|---|---|---|---|---|
| CreateThirdPartyPartnerCommand | CreateThirdPartyPartnerCommand + Create | ThirdPartyPartner | b | Sonraki: BusinessPartnerId, PartnerCode, WarehouseIds, ResponsibleUserId | — |
| UpdateThirdPartyPartnerCommand | UpdateThirdPartyPartnerCommand + Update | ThirdPartyPartner | b | önceki / sonraki: WarehouseIds, ResponsibleUserId, ContractNote | — |
| SuspendThirdPartyPartnerCommand | SuspendThirdPartyPartnerCommand + LifecycleTransition | ThirdPartyPartner | b | önceki / sonraki Status, **gerekçe** | — |
| ActivateThirdPartyPartnerCommand | ActivateThirdPartyPartnerCommand + LifecycleTransition | ThirdPartyPartner | b | önceki / sonraki Status, **gerekçe** | — |
| UpdateThirdPartyThresholdsCommand | UpdateThirdPartyThresholdsCommand + Update | ThirdPartyPartner | b | önceki / sonraki: §4.1.1 alanları | — |
| CreateThirdPartyChannelCommand | CreateThirdPartyChannelCommand + Create | ThirdPartyChannel | b | Sonraki: Type, Name, WarehouseIds, türe göre sırsız ayarlar | — |
| UpdateThirdPartyChannelCommand | UpdateThirdPartyChannelCommand + Update | ThirdPartyChannel | b | önceki / sonraki: değişen sırsız ayarlar | — |
| CloseThirdPartyChannelCommand | CloseThirdPartyChannelCommand + LifecycleTransition | ThirdPartyChannel | b | önceki / sonraki Status, **gerekçe** | — |
| OpenThirdPartyChannelCommand | OpenThirdPartyChannelCommand + LifecycleTransition | ThirdPartyChannel | b | önceki / sonraki Status, **gerekçe** | — |
| CreateThirdPartyMappingCommand | CreateThirdPartyMappingCommand + Create | ThirdPartyMapping | b | Sonraki: Kind, OurRef, PartnerValue | — |
| UpdateThirdPartyMappingCommand | UpdateThirdPartyMappingCommand + Update | ThirdPartyMapping | b | önceki / sonraki PartnerValue | — |
| DeactivateThirdPartyMappingCommand | DeactivateThirdPartyMappingCommand + LifecycleTransition | ThirdPartyMapping | b | önceki / sonraki Status | — |
| ReceiveThirdPartyMessageCommand | ReceiveThirdPartyMessageCommand + Create | ThirdPartyMessage (+ ThirdPartyException, ThirdPartyInstruction onayı) | b (Q-14) | Kind, Format, ExternalMessageId, sonuç Status, istisna numarası, iş belgesi numarası (ham içerik **yok**) | — |
| ReprocessThirdPartyExceptionsCommand | ReprocessThirdPartyExceptionsCommand + Execute | ThirdPartyException (+ ThirdPartyMessage) | b | istisna numaraları, sonuçlar (Resolved / Open), deneme sayısı | — |
| IgnoreThirdPartyExceptionCommand | IgnoreThirdPartyExceptionCommand + LifecycleTransition | ThirdPartyException | b | önceki / sonraki Status, **gerekçe** | — |
| AssignThirdPartyExceptionCommand | AssignThirdPartyExceptionCommand + Update | ThirdPartyException | b | önceki / sonraki sorumlu | — |
| ProcessHeldThirdPartyMessagesCommand | ProcessHeldThirdPartyMessagesCommand + Execute | ThirdPartyMessage | b | mesaj kimlikleri, `confirmOutOfOrder`, sonuçlar | — |
| DiscardHeldThirdPartyMessageCommand | DiscardHeldThirdPartyMessageCommand + LifecycleTransition | ThirdPartyMessage | b | önceki / sonraki Status, **gerekçe** | — |
| SetThirdPartyChannelSecretCommand | SetThirdPartyChannelSecretCommand + Update | ThirdPartyChannel | b | sır adı, `SetAt` (değer ve `VaultRef` **yok**) | — |
| TestThirdPartyChannelCommand | — | — | — | yalnız `LastTestAt`, `LastTestResult` | İ2 — teknik sinyal: kanalın iş kaydını (ayar, durum, sır) değiştirmez, `Version`'ı artırmaz; işlem açmaz, enlist etmez (`RequireEnlistment` — MOD-0182 nabız deseni); `CentralAuditDeclarationTests` gereği işaret taşır ve sayıya dahildir |
| SendThirdPartyMessageCommand | SendThirdPartyMessageCommand + Execute | ThirdPartyMessage | b (Q-14) | mesaj kimliği, önceki / sonraki Status, deneme sayısı | — |
| SendInboundAdviceCommand | SendInboundAdviceCommand + Create | ThirdPartyInstruction (+ ThirdPartyMessage) | b | PO numarası, depo, satır sayısı | — |
| AddReconciliationNoteCommand | AddReconciliationNoteCommand + Update | ThirdPartyReconciliationLine | b | not metni, miktar | — |
| MarkReconciliationInvestigatingCommand | MarkReconciliationInvestigatingCommand + LifecycleTransition | ThirdPartyReconciliationLine | b | önceki / sonraki Status | — |
| ResolveReconciliationLineCommand | ResolveReconciliationLineCommand + LifecycleTransition | ThirdPartyReconciliationLine | b | önceki / sonraki Status, **gerekçe** | — |

**DL-5 kapısı:** sevk talimatının yazımı 0179 komutu değildir; MOD-0178 release komutunun işleminde yazılır ve o komutun merkezi kaydına enlist olur (MOD-0182 port
deseni). **MDM (DL-3):** `BusinessPartner` komutları MdmService'in yol b izinden (`mdm-merkezi-iletim`) geçer ve MDM defterine yazılır; bu tabloya girmez.
**Yazma olmayan:** CSV dışa aktarma, ham içerik görüntüleme, performans okumaları (AUD-001 §7 okuma denetimi v1 dışı).

## 22. Record Codes

| Kayıt türü | Sınıf | TÜR öneki | Üretici | Gerekçe |
|---|---|---|---|---|
| 3PL istisnası (`ThirdPartyException.Number`) | OTOMATİK | `TPX` (öneri; RCS-001 satırı `.antigravity` kapısında — §6) | hedef MOD-0040-FU02; ara MOD-0178 geçici sayacı | sorumlu atama, yazışma ve iz bu numarayla anılır |
| MDM iş ortağı (`BusinessPartner.Code`) | OTOMATİK | Q-11 (Ali) — grup önekli | MDM | kiracı geneli ana veri |
| Ortak bağlantısı | — | MDM kodu kullanılır | — | ayrı kod gerekmez |
| Kanal, eşleme, mesaj, talimat, stok raporu, mutabakat satırı | — | kod yok | — | mesaj dış kimliğiyle, talimat kaynak belge numarasıyla (OUT- / PO), rapor ortak + depo + tarihle anılır |

Biçim `{ŞİRKET}-{TÜR}-{000001}`, sayaç (kiracı, şirket öneki, tür) başına, sıfırlanmaz, ASCII; kiracı genelinde tekil (RCS-001).

## 23. Teslim dilimleri

| dilim | ad | kapsadığı bölümler | gerekli kanıt seviyesi | plan yetenek kimliği | plan MVP |
|---|---|---|---|---|---|
| DL-1 | 3PL-BUNDLE taslağı + ortak bağlantısı, kanal (sırsız), eşleme, eşikler, kanonik mesaj defteri, istisna kutusu ve yeniden işleme, bekletilen mesajlar + izin, kiracı, denetim, kayıt kodu (arka uç; çekirdek dilim) | Owned Objects, Entity Fields, ThirdPartyPartner, ThirdPartyChannel, ThirdPartyMapping, ThirdPartyMessage ve ThirdPartyMessagePayload, ThirdPartyException, ThirdPartyCommandRequest, Indexes, Repo Scope, Protected Paths, Dependencies, Servis ve kapsam, Durum makineleri, Kanonik mesaj modeli ve mesaj listesi, Askıya alma ve bekletilen mesajlar, İstisna kutusu ve yeniden işleme, Sağlık ve performans eşikleri, Merkezi denetim ve yerel iz, Kayıt kodu üretimi, Hata kodları, Sözleşme (3PL-BUNDLE) ve dondurma, Backend File Convention, Validation Rules, Failure Path to Verify, Authorization Convention, Audited Events, Record Codes | E4 | plan kimliği yok | MVP-5 |
| DL-2 | Web ekranları: sağlık + ortak ayrıntısı, kanallar, eşlemeler, mesajlar, bekletilenler, istisna kutusu, eşik ayarları; durumlar; 375 px; RTL; 7 dil; yardım | Layout & Shell Contract, Frontend File Contract, Ekranlar ve Blueprint soft pages, Page Help (UXH-001) | E4 | plan kimliği yok | MVP-5 |
| DL-3 | MDM iş ortağı, 3PL rolüyle (MdmService; DL-1'den bağımsız) + MDM Gateway rotası + MDM ekranı | MDM iş ortağı (BusinessPartner — MDM seam), XMC-001 — başka modülün dosyalarına dokunuşlar, Record Codes | E4 | plan kimliği yok | MVP-5 |
| DL-4 | Platform sır kasası bağlantısı + API kanalı (imzalı gelen uç, giden REST, bağlantıyı sına) (kapı: kiracı sır kasası — Q-16; 3PL-BUNDLE dondurulmuş) | Kanal sırları ve Platform sır kasası, Entegrasyon kanalları, Gateway / API Routing Decision, Audited Events | E4 | plan kimliği yok | MVP-5 |
| DL-5 | Çıkış akışı: 3PL deposunda görev üretmeme, release'te sevk talimatı, sevk onayı → ReadyToShip → MOD-0183 dispatch → Shipped (kapı: X-10, X-11, X-12, Q-18, Q-12) | Çıkış akışı (MOD-0178 / MOD-0183), ThirdPartyInstruction, XMC-001 — başka modülün dosyalarına dokunuşlar | L3 + E5 | plan kimliği yok | MVP-5 |
| DL-6 | Giriş akışı: ön bildirim, teslim onayı → MOD-0142 GRN (kapı: X-13, Q-19, Q-12) | Giriş akışı (MOD-0142), ThirdPartyInstruction, XMC-001 — başka modülün dosyalarına dokunuşlar | L3 + E5 | plan kimliği yok | MVP-5 |
| DL-7 | Stok raporu + mutabakat masası + performans sekmesi (arka uç ve ekran) | Stok raporu ve mutabakat, ThirdPartyStockReport ve ThirdPartyReconciliationLine, Sağlık ve performans eşikleri, Ekranlar ve Blueprint soft pages | E4 | plan kimliği yok | MVP-5 |
| DL-8 | Dosya kanalı: SFTP okuyucu / yazıcı, CSV / XML | Entegrasyon kanalları | E4 | plan kimliği yok | MVP-5 |
| DL-9 | EDI kanalı: EDIFACT + X12 ayrıştırma / üretme; taşıma Q-2 | Entegrasyon kanalları, Kanonik mesaj modeli ve mesaj listesi | E4 | plan kimliği yok | MVP-5 |
| DL-10 | yalnız Gateway kapsama testi (integration-agent; DL-4'ten sonra; `ocelot.json` değişmez) | Gateway / API Routing Decision | E3 | plan kimliği yok | MVP-5 |

Plan kaynağı: plan dosyası depoda değildir; çağıran CT'nin aktarımı (2026-11-13): "MVP-5, Conditional — only if external 3PL required"; plan yetenek kimliği (`1.x.y`)
verilmedi → `plan kimliği yok`. Plan MVP kimliği yalnız "MVP-5" olarak verildi (blok kimliği biçimi verilmedi).

## 24. Dilim kapıları ve bağlı kararlar

Paket onaylanmadan (künye `approved` / `ready-for-dev` + CT-APR-02 inceleme kaydı + CT-APR-01 onay satırı, onaylayan Ali) hiçbir dilim başlamaz.
**Onaydan önce kapanan kapılar:** `.antigravity/**` satırları (Q-1, Q-11) + aynı adımda X-17, X-18; Q-10 (MDM kimliği).

| Dilim | Başlamadan kapanması gereken kararlar | Seam'ler (XMC-001) | AC | Sabotajlar |
|---|---|---|---|---|
| DL-1 | Q-6, Q-7 (Beste), Q-14 (CT); ilk adım 3PL-BUNDLE taslağı | X-4, X-5, X-6 (a) +18 ve modül listesi, X-6 (b) (öneri +1), X-7, X-8 (+28), X-9 | AC-1…AC-25 | S-1, S-2a, S-2b, S-3a, S-3b, S-4, S-5a, S-5b, S-6, S-7a, S-7b, S-7c, S-8a, S-8b, S-9a, S-9b, S-10, S-11, S-12, S-13a, S-13b, S-14, S-15a, S-15b, S-16, S-17, S-18, S-19, S-20a, S-20b, S-21, S-22a, S-22b, S-23, S-24a, S-24b, S-25 |
| DL-2 | Q-5, Q-17; Q-13 rol şablonu (Ali) — kabul kapısı | X-15 | AC-26…AC-34 | S-26, S-28, S-29, S-30, S-31, S-32, S-34 |
| DL-3 | **Q-10 (Ali + CT)**, Q-11; X-1 dosya listesi teyidi (Ali) | X-1, X-2, X-3 | AC-35…AC-38 | S-35a, S-35b, S-36, S-37, S-38 |
| DL-4 | **Q-16 kiracı sır kasası `main`'de**; 3PL-BUNDLE dondurulmuş (§8.16); DL-1 kabulü | X-6 (a) +3, X-8 (+3), X-14 | AC-39…AC-42 | S-39, S-40a, S-40b, S-40c, S-41a, S-41b, S-42 |
| DL-5 | **Q-18 (MOD-0183 sahibi)**, Q-12 (servis kimliği), Q-9; X-10 / X-11 sahip onayı + tasarım notu; WMS-BUNDLE eklemeli sürüm DL-5 başlamadan dondurulur | X-8 (+2), X-10, X-11, X-12, X-16 | AC-43…AC-45 | S-43a, S-43b, S-44a, S-44b, S-45 |
| DL-6 | **Q-19 (procurement)**, Q-12, Q-8 | X-6 (a) +1, X-8 (+1), X-13, X-16 | AC-46, AC-47 | S-46, S-47a, S-47b |
| DL-7 | Q-4; DL-5 ya da DL-6'dan biri kabul (yoldaki talimat ister); defter "an itibarıyla" okuması ölçüldü | X-6 (a) +3, X-8 (+8), X-15 | AC-48…AC-52 | S-48a, S-48b, S-49, S-50, S-51, S-52 |
| DL-8 | SFTP NuGet ve güvenlik açığı taraması; DL-4 kabulü (sır) | — | AC-53…AC-55 | S-53a, S-53b, S-54, S-55 |
| DL-9 | Q-2, Q-3; Q-2 = SFTP ise DL-8 kabulü | X-8 (Q-2 = AS2 ise +1) | AC-56…AC-58 | S-56, S-57, S-58 |
| DL-10 | DL-4 kabulü | gateway test projesi (integration-agent); `ocelot.json` sabotajda geçici bozulur, geri alınır | AC-59 | S-59 |

Sıra: DL-1 → DL-2 ve DL-3 (paralel; DL-3 DL-1'den bağımsız) → DL-4 → DL-5 / DL-6 (paralel, kapılarına göre) → DL-7 → DL-8 → DL-9; DL-10 DL-4'ten sonra her an.
MOD-0178 / 0181 / 0182 bekçi testleri her dilimde değişmeden yeşil (AC-43'ün `git diff` kanıtı her dilim kabulünde tekrarlanır). Tek istisnalar X-5, X-6, X-8,
X-9 — `INVT/Warehouse/**` / `INVT/CycleCount/**` / `INVT/Barcode/**` dışındadır.

## 25. Page Help (UXH-001)
- `TPLHEALTH` → `Help.TPLHEALTH.Purpose` ("Bağlı 3PL ortaklarının kanal sağlığını, son mesaj zamanını ve açık hata sayısını gösterir."), `.Audience`, `.Step1Title`/`.Step1` …
  `.Step3` (1: ortağı MDM'den seç ve depolarını bağla, 2: kanallarını ve eşlemelerini tanımla, 3: durumu izle — Sağlıklı, Gecikmeli, Hatalı, Kapalı, Askıda);
  `.Immediate` ("Askıdaki ortağa mesaj gönderilmez; gelenler 30 gün bekletilir. Ortak silinemez."); `Related.TPLEXCEPTIONS`, `Related.TPLRECON`.
- `TPLCHANNELS` → `Purpose`, `Audience`, `Step1..3` (kanal türünü seç, ayarları gir, sırrı ayarla ve bağlantıyı sına); `.Immediate` ("Sırlar Platform sır kasasında
  tutulur; burada yalnız son değişim tarihi görünür. Kanal kapatma gerekçe ister; kapalı kanala gelen mesaj reddedilir ve istisna kutusuna düşer.").
- `TPLMAPPINGS` → `Purpose`, `Audience`, `Step1..2` (bizim ürünü ve 3PL kodunu eşle, eksik eşleme uyarısını izle).
- `TPLMESSAGES` → `Purpose`, `Audience`, `Step1..2` (mesajı bul, iş belgesinin zaman çizelgesini aç); `.Immediate` ("Ham içerik ayrı izinle görünür; kişisel veriler maskelenir.").
- `TPLEXCEPTIONS` → `Purpose` ("İşlenemeyen ya da kural dışı gelen 3PL mesajları; çözülene kadar stok defterine yansımaz."), `Audience`, `Step1..3` (hata türüne göre
  süz, yeniden işle ya da eşleme ekle, gerekirse gerekçeyle yok say); `.Immediate` ("Toplu yeniden işleme yalnız aynı ortağın aynı hata türündeki en çok 200 kaydında yapılır.").
- `TPLRECON` (DL-7) → `Purpose`, `Audience`, `Step1..3` (ortak, depo ve rapor tarihini seç, açık farkları incele, not ekle ya da gerekçeyle çöz); `.Immediate` ("Fark =
  bizim stok − 3PL stoğu. Yoldaki miktar farktan düşülür. Stok düzeltmesi bu ekranda yazılmaz.").
- `TPLSETTINGS` → `Purpose`, `Audience`, `Step1..2` (ortağı seç, gecikme eşiklerini belirle).
- `BUSINESSPARTNERS` (MDM, DL-3) → `Purpose`, `Audience`, `Step1..2` (iş ortağını kaydet, rolünü seç).
- Kiracı modülü → 7 dil (en, tr, fr, es, zh, ar, ru). **Tur yok.**
