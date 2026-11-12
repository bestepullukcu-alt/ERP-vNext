---
id: MOD-0180
name: Wave Planning
domain: supply-chain-execution
service: Diten.InventoryService
shell: tenant
golden_reference: slim
data_mode: server
screens:
  - module: Waves
    data_mode: server
  - module: WaveExceptions
    data_mode: server
entity_base: EntityBase
status: draft
approval_status: pending
revision: "r2 (2026-11-12) — bağımsız inceleme r1 (`docs/records/reviews/MOD-0180/2026-11-12-c7c774c6.md`, KOŞULLU, F-1…F-16) ve sahip kararları 03 (Y-1…Y-4, Q-6 = B, diğer Q'lar öneri gibi, Q-17 sahipten, Q-20 Ali) işlendi; F-1…F-16 kapandı (kapanış tablosu §19.5). Başlıca değişiklikler: depoda ertelenmiş sipariş varken dalga yönetimini kapatma 409 (Y-1, AC-15); Excluded / OrderCancelled → PartiallyCompleted (D-9 karar 03 Y-2 ile genişledi); ayrı ayar izni warehouse.waves.settings.configure (Tier-3, Q-6 = B); WMS-BUNDLE eklemeli sürümü (OutboundEventKind dahil) DL-2 başlamadan önce CT adımı (X-8); Governance Rol İzinleri köprüsü X-15; .antigravity satırları paket onayından önce (Y-4); atıflar dc010b911'e sabitlendi. r1: ilk taslak (PA-MOD0180-01; girdiler BRIEF-MOD-0180, kararlar 01 / 02, mockup v2). Onaylayan Ali (GMG-ERP) — D-17, karar 02; yazan ≠ onaylayan. Bu sürümde onay satırı yoktur."
status_note: "draft r2 — kod izni yok. Q-1…Q-16, Q-18, Q-19, Q-21, Q-22 karar 03 ile kapandı (Q-15 r1'de kapanmıştı: kararlar 02 dosyası depoda); açık: Q-17 hedef tarih (sahip), Q-20 rol şablonu (Ali). r2 yeni bağımsız inceleme (CT-APR-02) ve Ali (D-17) onayı bekler. Onaydan önce kapanacak kapı: .antigravity/** satırları (PKS-001 §4 warehouse.waves.*, RCS-001 §3 WAV + X-12) — sahip onayıyla, paket onayından ÖNCE (Y-4, §6). DL-2 öncesi CT adımı: WMS-BUNDLE eklemeli sürümü (X-8). WMS-WAVE-BUNDLE taslağı DL-1'in ilk adımı, dondurma ayrı CT adımı."
owner: supply-chain-execution / merkez (Beste) — MVP-5
branch: feature/sce/mod-0173-inventory-ledger-valuation
started: 2026-11-12
target: "TBD (sahip) — Q-17; plan satırı tarih vermez"
form_field_count: 8
dependencies: [MOD-0178 (S2 outbound + tahsis + pick görevi; XMC-001 seam'leri §5), MOD-0173 (defter yalnız dolaylı — 0180 stok okumaz / yazmaz), MOD-0173-FU02 (LOCATION yolu ve bin kodu), MOD-0040-FU02 (RCS-001 kayıt kodu servisi; DL-8 gelene kadar ara üretici Q-4), MOD-0018 (RBAC; Governance Rol İzinleri köprüsü X-15), BL-501 (merkezi denetim), WMS-BUNDLE (MOD-0178 sözleşmesi; eklemeli üye + OutboundEventKind değerleri — X-8, DL-2 öncesi CT adımı), WAREHOUSE-OUTBOUND 1.0.0 (okuma), LOCATION v1.1 (okuma), WMS-WAVE-BUNDLE (taslak yok — bu paketin sözleşmesi), DEV-0000]
---

# MOD-0180 — Wave Planning (Dalga Planlama)

> **draft (r2, 2026-11-12).** `status: draft`, `approval_status: pending`. **Paket onaylanmadan geliştirme başlamaz:** kod yalnız
> `approved` / `ready-for-dev` künye + bağımsız inceleme kaydı (CT-APR-02) + onay satırı (CT-APR-01, **onaylayan Ali / GitHub `GMG-ERP`**,
> D-17) ile yazılır (AGENTS.md Çekirdek madde 7). Bu sürüm kod, commit, push, PR ya da onay izni değildir.
>
> **r2 revizyon notu (2026-11-12).** Girdiler: bağımsız inceleme r1 `docs/records/reviews/MOD-0180/2026-11-12-c7c774c6.md` (KOŞULLU; 6 orta,
> 10 düşük bulgu) ve sahip kararları 03 `docs/records/decisions/2026-11/mod0180-wave-planning-owner-decisions-03.md` (Y-1…Y-4; Q-6 = B;
> Q-1…Q-5, Q-7…Q-16, Q-18, Q-19, Q-21, Q-22 öneri gibi; Q-17 sahipten; Q-20 Ali). F-1…F-16'nın hepsi bu sürümde kapandı; her bulgu ve
> kararın değişikliği ve bölümü §19.5 "r1 → r2 kapanış tablosu"ndadır. Davranış değişiklikleri: Y-1 (dalga yönetimini kapatma 409 —
> §4.5, §8.6, AC-15), Y-2 (D-9 genişlemesi — §8.3, §8.9, AC-30), Q-6 = B (ayar izni — §14, AC-15, AC-48), X-8 sırası (DL-2 öncesi), X-15
> (rol köprüsü — AC-56). Form alanı sayısı (8) ve `golden_reference: slim` **değişmedi** (Q-1 = A, karar 03).
>
> **Yetki:** bu paket > `execution/domains/supply-chain-execution/domain-config.md` > `AGENTS.md` > `.antigravity/`.
>
> **Atıf kısaltmaları.** `INV/` = `services/Diten.InventoryService/src/Diten.InventoryService.`; `INVT/` =
> `services/Diten.InventoryService/tests/Diten.InventoryService.Tests/`. Kod atıfları r2'de **`dc010b911`** üzerinde yeniden ölçüldü ve
> `dc010b911:INV/...:satır` biçiminde sabitlendi (`dc010b911`, r1'in ölçtüğü MVP-1 kod hattı ucu `a3f329ece`'nin torunudur; aradaki fark yalnız
> MOD-0181 DL-5 kodu — 0180'in atıf yaptığı 0178 dosyaları değişmedi, `SingleSystemOfRecordArchitectureTests` ve `CentralAuditDeclarationTests`
> satırları kaydı, yeni satırlar yazıldı). **Öneksiz depo belgesi atıfları** (MOD-0178 **r28**, MOD-0181 **r8**, sözleşmeler, kurallar) de
> `dc010b911` ağacındaki konumlardır. **Çıkarım** ve **öneri** olarak işaretlenmeyen her cümle bir kaynağa dayanır; kaynaksız değer yazılmadı
> (bilinmeyen = Q-n ya da TBD).
>
> **Mockup bağı.** Ekran düzeni (pano, ayrıntı sekmeleri, planlayıcı, istisna kuyruğu, performans, durum örnekleri) **mockup v2'ye göre**dir
> (`C:\Users\Ali\Downloads\Dalga Planlama (standalone) v2.html`, depo dışı). Paket mockup'ı kopyalamaz; kuralı paket koyar, mockup ile paket
> çelişirse paket geçerlidir. Mockup v2'den okunan farklar §19.2'dedir.

## 0. Kimlik ön kontrolü ve keşif

**DCP-002 ön kontrolü (fail-closed) — r1'de koşuldu (HEAD `a3f329ece`, araç sha256 `ae87a566…`); r2'de `dc010b911`'de yeniden koşuldu
(araç aynı, `ae87a56615f5…`):**

| Komut | Sonuç |
|---|---|
| r2: `PYTHONUTF8=1 python .antigravity/scripts/verify_module_id.py . --check-id MOD-0180 --name "Wave Planning"` | çıkış 0 — `OK  MOD-0180: proven against Blueprint/registry.` |
| r2: `PYTHONUTF8=1 python .antigravity/scripts/verify_module_id.py . --discover "Wave Planning"` | çıkış 3; `ÖZET bulgular: blueprint 1 · sor 0 · registry 1 · paket 2 · gateway 0 · görev 1`; rapor sha256 `ba84a0e299438c92c9c7af18d849970346ccc22e1138282d6ec016b93e45e208`, 605 satır. Paket 2 = r1'deki `MDM-059` + bu paketin kendisi (`MOD-0180-wave-planning.md`, r1 commit'i) — yeni aday yok |
| r1: `PYTHONUTF8=1 python .antigravity/scripts/verify_module_id.py . --discover "Wave Planning"` | çıkış 3 ("inceleme gerekli"); `ÖZET bulgular: blueprint 1 · sor 0 · registry 1 · paket 1 · gateway 0 · görev 1`; rapor sha256 `467aa3da2ec477e5f83b0637e761f434b9364a039c8d0c12f0fd67638bf79d14`; 562 ref incelendi |
| r1: `PYTHONUTF8=1 python .antigravity/scripts/verify_module_id.py . --discover "Dalga Planlama"` | çıkış 3; `ÖZET bulgular: blueprint 0 · sor 0 · registry 0 · paket 0 · gateway 0 · görev 1` (yalnız aynı CT kuyruğu satırı) |

**Keşif bulgularının her birine karar:**

| Bulgu | Karar |
|---|---|
| Blueprint 1 — `MOD-0180 'Wave Planning'`, veri nesnesi `waves` | **Bu paketin kaynağı.** Blueprint 8.8 `Blueprint_Data` satır 181 (aşağıda) |
| Registry 1 — `MOD-0180` reserved / planned | **Bu paketin kaynağı.** `execution/registries/module-id-registry.md:318` |
| Paket 1 — `execution/domains/master-data-management/module-packs/MDM-059-wave-planning.md` (8 eski dal: `origin/feature/module-catalog`, `origin/feature/dev-enablement`, …) | **Mükerrer değil — eski toplu taslak, yanlış domain, onaysız; devralınmadı.** İçeriği okundu (`git show refs/remotes/origin/feature/module-catalog:…`): `id: MDM-059`, `domain: master-data-management`, `status: draft`, `owner: ai-agent`, tek commit `8c3498bef` ("Initial commit", 2026-04-24), gövde yalnız şablon maddeleri. Aynı durum MOD-0178'de (`MDM-057`, `execution/domains/supply-chain-execution/module-packs/MOD-0178-putaway-picking-packing.md:119`) ve MOD-0181'de (`MDM-060`, `execution/domains/supply-chain-execution/module-packs/MOD-0181-cycle-counting.md:219`) aynı kararla kapandı. Okunmaz, yazılmaz |
| Görev 1 — `execution/domains/supply-chain-execution/work-packs/mvp5-wms/CT-QUEUE.tsv` `WMS-MVP5-MOD0180` [INSPECTION_REQUIRED] "pack yok" | **Bu paketin iş satırı** (CT kuyruğu); çakışma değil |
| Gateway 0 | Karar §15 — mevcut `/api/warehouse/{everything}` ailesi |

Çakışma ya da mükerrer **yoktur**; CT'ye durdurma yazısı gerekmedi.

**Blueprint 8.8 satır 181 (bu oturumda xlsx'ten okundu):** Module ID `MOD-0180` · Suite `Supply Chain Execution Suite` · Capability Group
`Warehouse (WMS)` · Aim "Optimize warehouse operations via standardized events/contracts and exception-centric execution." · Module Name
`Wave Planning` · Wave `W-4` · Dependency Gate `Putaway / Picking / Packing` · Outcome `Ops optimization` · Soft Pages `Wave Planner View;
Performance KPIs; Exception Queue` · Placement `Domain App (WMS)` · Min Integration Contract `WMS-WAVE-BUNDLE (wave objects, optimization
params, KPI bindings)` · SoR `waves, wave assignments, wave outcomes` · SoR Primary Object `waves` · Build/Buy `Buy/Partner` · SLO `Tier 2` ·
AI Tier `Assist` · AI Use Cases `Summarize; Extract; Recommend; Route` · AI Gate `Prompt Registry; HITL; Model Registry; Eval/Drift; Logging` ·
AI Risk `Medium`.

**Blueprint sapması (W-0180-01) — Build/Buy:** Blueprint `Buy/Partner` der. **Karar: Build** (Q-13 = A, karar 03). Gerekçe: dalga, MOD-0178'in tahsis planına
ve görev üretimine süreç içi bağlıdır (§8.6); dış bir ürün bu seam'le konuşamaz. Emsal: MOD-0178 ve MOD-0181 Blueprint'te `Buy/Partner`'dır,
sahip kararı Build'dir (`execution/domains/supply-chain-execution/module-packs/MOD-0181-cycle-counting.md:235-239`).

**Diğer kimlik kaynakları:** `execution/domains/supply-chain-execution/domain-config.md:27` (WMS listesi `MOD-0180 Wave Planning`); plan
`docs/roadmap/plans/integration-phase-and-mvp5-start-plan-2026-10-21.md:78` ("MOD-0180 Dalga Planlama (W-4)") ve `:124` ("… MOD-0181 sayım (0178 ile
paralel) → MOD-0180 dalga → MOD-0182 barkod"); MOD-0178 paketi (r28) `:171-172` ("MOD-0180 Dalga ve MOD-0182 Barkod sonra") ve `:202` ("Dalga planlama,
sayım | MOD-0180 / MOD-0181 | yok").

## 1. Module Summary

MOD-0180, **tek bir depoda** sevke hazırlanacak outbound siparişleri (MOD-0178 `OutboundShipment`) **toplama dalgalarına** gruplar ve
toplama görevlerini dalga dalga **serbest bırakır**. Dalga **tahsis yapmaz**: yalnız tahsisi bitmiş (`Allocated`) outbound'ları gruplar;
toplama görevini **MOD-0178 üretir** (dalga serbest bırakıldığında, 0178'in kendi kalıcı pick planından — §8.6). Blueprint SoR nesneleri:
**waves** (dalga kaydı), **wave assignments** (dalga–sipariş üyeliği ve görev sırası), **wave outcomes** (ilerleme, istisna, tamamlanma) —
Blueprint 8.8 satır 181.

**Kapsam kararları (bağlayıcı):** toplama dalgası, tek depo (sahip, BRIEF `:6`); kurma **elle** + **"Siparişleri öner"** düğmesi, periyodik
otomatik kurma yok (D-1); rota / sıra optimizasyonu **v1'de var** (D-2, §8.7); kesim saati deponun yerel saat diliminde (D-3, §8.4); toplama +
paketleme + sevk ilerlemesi görünür, MOD-0178'den **okunur** (D-5, §8.8).

**Temel sınır:** 0180 stok bakiyesi, rezervasyon, görev ya da outbound **yazmaz**. MOD-0178'in koleksiyonlarına (`wms_tasks`,
`wms_outbound_shipments`, …) tek yazıcı MOD-0178 kodudur; 0180 yalnız 0178'in **sahip olduğu** süreç içi port üzerinden "bu dalganın görevlerini
aç / kalanını geri çek" ister (§8.6, XMC-001 §5).

Hedef kullanıcı: **depo planlayıcısı** (dalgayı kurar, sipariş ekler / çıkarır, serbest bırakır), **vardiya sorumlusu** (izler, istisnaları
çözer, kalanı geri çeker), **toplayıcı** (görevleri MOD-0178 Toplama Görevleri ekranında yapar; bu modülde ekranı yok — BRIEF `:12-13`). Kiracı
modülüdür (`_LayoutTenantShell`, 7 dil).

## 2. Ownership and Boundaries

### 2.1 Kapsam
- `Wave` (dalga baş kaydı), `WaveOrder` (dalga–outbound üyeliği), `WaveEvent` (insert-only zaman çizelgesi), `WaveException` (istisna kuyruğu
  kaydı), `WaveWarehouseSettings` (depo başına dalga ayarı: dalga yönetimi anahtarı, saat dilimi, eşikler).
- Dalga kurma (elle + öneri), sınır uyarıları, serbest bırakma (rota sırası + 0178 görev üretimi isteği), geri çekme, iptal (yalnız Taslak),
  ilerleme okuması, durum uzlaştırması, istisna tespiti ve çözümü, performans KPI'ları + CSV.
- Ekranlar: Dalga panosu (+ ayrıntı), Planlayıcı, İstisna kuyruğu, Performans (Blueprint soft pages — §8.15).
- `WMS-WAVE-BUNDLE` sözleşmesi (taslak bu paketin DL-1 işi; bugün yok — `docs/analysis/contracts/drafts/` altında yalnız
  `bank-account-lookup-validation-v1.0-proposal.md` var, ölçüldü).

### 2.2 Kapsam dışı
| Şey | Sahibi | 0180 nasıl dokunur |
|---|---|---|
| Stok bakiyesi, hareket, rezervasyon, tahsis | MOD-0173 / MOD-0173-FU03 / MOD-0178 | **dokunmaz**; tahsisi 0178 outbound'la birlikte yapar (`execution/domains/supply-chain-execution/module-packs/MOD-0178-putaway-picking-packing.md:1235-1236`) |
| Outbound kaydı, pick görevi, onay, koli, sevke hazır | MOD-0178 | outbound / görev / olay koleksiyonlarını süreç içi **yalnız `Find`** ile okur; görev üretimi ve geri çekme **0178'in portuyla** (§8.6) |
| Lokasyon ağacı, bin kodu | MOD-0173-FU02 | okur (yol + kod); düğüm yaratmaz |
| Sevkiyat, taşıyıcı, POD | MOD-0183 / MOD-0184 (MVP-6) | yok; taşıyıcı süzgeci v1'de yok (Q-1) |
| Satış siparişi / müşteri kimliği | O2C (SoR yok — MOD-0178 `OrderRef` opak) | yalnız outbound'un `ShipTo` ad / şehir / posta kodu / ülkesi ile süzer (Q-21) |
| Depolar arası dalga, yerine koyma (replenishment) dalgası | — | yok (tek depo, toplama dalgası) |
| Periyodik otomatik dalga kurma | — | yok (D-1); FU-0180-AUTO |
| Toplayıcı ataması | MOD-0178 (`warehouse.pick-tasks.update`) | yapmaz; yük sekmesinde toplayıcı başına görev sayısını **gösterir** |
| AI Assist | Blueprint AI kapıları | v1'de yok (Q-14) |

### 2.3 Sınır kuralları
1. **İkinci bakiye yok, ikinci görev kaydı yok.** 0180'in hiçbir varlığı `OnHand` / `Available` / `Reserved` alanı ya da görev miktarı
   toplamı tutmaz; ilerleme okuma anında 0178 verisinden hesaplanır, saklanmaz (D-5).
2. **0178 verisine yazılmaz.** 0180 üretim kodu `IOutboundShipmentRepository`, `IWarehouseTaskRepository` ya da `WarehouseCollections.*` üzerinde
   `Insert*` / `Update*` / `Replace*` / `Delete*` / `BulkWrite` çağırmaz; okumalar 0180 Persistence'ta `WarehouseCollections.*` sabitleriyle
   **yalnız `Find`**'dır (0181 emsali: `execution/domains/supply-chain-execution/module-packs/MOD-0181-cycle-counting.md:299-302`). Görev üretimi
   ve geri çekme 0178'in **kendi kodundadır** (port `IWavePickIssuance`, §8.6); 0180 onu çağırır (AC-16).
3. **Sipariş bölünmez (D-8).** Bir outbound aynı anda en çok **bir** etkin dalgadadır ve bütün satırları / tahsisleri / görevleriyle birlikte
   dalgadadır.
4. **0178 davranışı varsayılanda değişmez.** Dalga yönetimi kapalı (varsayılan) depoda 0178 bugünkü gibi çalışır; 0178 test dosyaları değişmez
   ve yeşil kalır (AC-18).
5. Her okuma ve yazma kiracı + şirket (`TenantId` + `LegalEntityId`, sunucuda çözülür) + depo (`WarehouseId`) kapsamındadır; kiracılar /
   şirketler arası erişim **404** (`.antigravity/rules/multi-tenancy.md`).
6. **Silme yok.** Dalga, üyelik, olay, istisna ve ayar kaydı silinmez; DELETE ucu yoktur; Taslak dalga yalnız **iptal** edilir (durum).

## 3. Owned Objects

| Nesne | Dilim | Koleksiyon (öneri) | Not |
|---|---|---|---|
| `Wave` | DL-1 | `wms_wave_waves` | Değişebilir baş kayıt, `Version` (If-Match, D-14); durum makinesi §8.3; serbest bırakmada kalıcı `PickPlan[]` (DL-2) |
| `WaveOrder` | DL-1 | `wms_wave_orders` | Dalga–outbound üyeliği; tekil kısmi index ile "sipariş tek etkin dalgada" (D-8) |
| `WaveEvent` | DL-1 | `wms_wave_events` | Insert-only zaman çizelgesi (olay geçmişi sekmesi); yerel GxP izi |
| `WaveException` | DL-3 | `wms_wave_exceptions` | İstisna kuyruğu kaydı (Açık / Çözüldü / Yok sayıldı); tespit anahtarıyla tekil |
| `WaveWarehouseSettings` | DL-1 | `wms_wave_settings` | Depo başına tek kayıt: `WaveManaged`, `TimeZoneId`, D-6 / D-11 eşikleri |
| Koleksiyon sabitleri | DL-1 | `WaveCollections` (0180 Persistence, `Persistence/WavePlanning/`) | MOD-0178'in `WarehouseCollections`'ıyla paylaşılmaz (0181 `CycleCountCollections` emsali, `dc010b911:INVT/Architecture/SingleSystemOfRecordArchitectureTests.cs:88-89`) |

Koleksiyon öneki `wms_wave_*` önerisidir (Blueprint Placement "Domain App (WMS)"); kesin ad DL-1'in ilk ölçümüyle sabitlenir.

**Komutlar (yazma, 11):** `CreateWaveCommand`, `UpdateWaveCommand`, `AddWaveOrdersCommand`, `RemoveWaveOrdersCommand`, `CancelWaveCommand`,
`ReleaseWaveCommand`, `CompleteWaveReleaseCommand` (yalnız bakım servisi), `RecallWaveRemainingCommand`, `ResolveWaveExceptionCommand`,
`ReconcileWaveCommand` (yalnız uzlaştırma servisi), `UpdateWaveWarehouseSettingsCommand`.

**Sorgular:** `GetWaveListQuery`, `GetWaveByIdQuery`, `GetWaveDashboardQuery`, `GetWaveOrderListQuery`, `GetWaveTaskListQuery`,
`GetWaveLoadQuery`, `GetWaveEventListQuery`, `GetWaveCandidateListQuery` (planlayıcı sol listesi), `GetWaveSuggestionListQuery` (D-1 "Öner"),
`GetWaveExceptionListQuery`, `GetWaveExceptionByIdQuery`, `GetWaveKpiSeriesQuery`, `ExportWaveKpiCsvQuery`, `GetWaveWarehouseSettingsQuery`.

**API (taban `/api/warehouse`, aile `/api/warehouse/waves…` — §15; yol adları sözleşme taslağında kesinleşir). PUT / DELETE yok, komut uçları:**
`GET/POST /waves` · `GET /waves/{waveId}` · `POST /waves/{waveId}/update` · `GET /waves/{waveId}/orders` · `POST /waves/{waveId}/orders` (ekle) ·
`POST /waves/{waveId}/orders/remove` · `GET /waves/{waveId}/suggestions` · `POST /waves/{waveId}/release` · `POST /waves/{waveId}/cancel` ·
`POST /waves/{waveId}/recall` · `GET /waves/{waveId}/tasks` · `GET /waves/{waveId}/load` · `GET /waves/{waveId}/events` · `GET /waves/dashboard` ·
`GET /waves/candidates` · `GET /waves/exceptions` · `GET /waves/exceptions/{exceptionId}` · `POST /waves/exceptions/{exceptionId}/resolve` ·
`GET /waves/kpis` · `GET /waves/kpis/export` (`text/csv`) · `GET /waves/settings/{warehouseId}` · `POST /waves/settings/{warehouseId}/update`.
Sabit kesimler (`dashboard`, `candidates`, `exceptions`, `kpis`, `settings`) parametreli yoldan önce eşleşir; `{waveId}` ve `{exceptionId}` `:guid`
kısıtlıdır (yeni, donmuş olmayan yollar — MOD-0178 §8.18'deki donmuş yol notu bu yollara uygulanmaz).

**Controller tabanı:** `WavesController`, `WaveExceptionsController`, `WaveKpisController` — üçü de `[Route("api/warehouse")]` taşır, eylem
şablonları tam yol parçasıdır (`[HttpPost("waves/{waveId:guid}/release")]` …); dosyalar **`INV/Api/Controllers/WavePlanning/`** altında ve
namespace `Diten.InventoryService.Api.Controllers.WavePlanning`'dir. **Neden ayrı namespace:** 0178'in kayıt testi
`Controllers.Warehouse` namespace'indeki bütün controller'ların zorladığı anahtarları 0178 manifestiyle eşit sayar
(`dc010b911:INVT/Warehouse/WarehouseRegistrationTests.cs:49-56`); 0180 controller'ı oraya konursa o test kırmızı olur (AC-5 sabotajı).

**Frontend rotaları:** `/Warehouse/Waves` (pano), `/Warehouse/Waves/Details/{waveId}` (ayrıntı, nav değil), `/Warehouse/WavePlanner`
(planlayıcı), `/Warehouse/WaveExceptions` (istisna kuyruğu), `/Warehouse/WaveKpis` (performans) — BRIEF `:22`, `:53`, `:59`, `:66`.

**İzinler:** §14.

## 4. Entity Fields

Tüm varlıklar InventoryService `EntityBase`'inden türer: `Id`, `TenantId`, `LegalEntityId`, `CreatedAt`, `UpdatedAt`, `Version` (int),
`IsDeleted`, `DeletedAt` (MOD-0178 §4 ölçümü, `execution/domains/supply-chain-execution/module-packs/MOD-0178-putaway-picking-packing.md:286-289`).
`IsDeleted` 0180'de hiç `true` olmaz (silme yok, §2.3 kural 6). `TenantId` / `LegalEntityId` sunucuda çözülür; gövde ya da sorguda gelirse 400.
Kullanıcı alanı `EntityBase`'de yoktur; gereken varlık kendi alanını taşır (`CreatedBy`). Zamanlar UTC saklanır; kesim saati ayrıca yerel
değer + saat dilimi ile saklanır (§8.4).

### 4.1 Wave
| Alan | Tip | Kural |
|---|---|---|
| `Number` | string | Görünen kod `{ŞİRKET}-WAV-{000001}` (RCS-001, OTOMATİK; §8.14); kiracı genelinde tekil `(TenantId, Number)`; değiştirilemez |
| `CompanyCodePrefix` | string `^[A-Z]{2,5}$` | numaraya yazılan şirket öneki (MOD-0178 M-2 deseni) |
| `WarehouseId`, `WarehouseCode` | uuid, string | LOCATION `Warehouse`, `Active`, şirket kapsamında; yaratmadan sonra **değişmez** (BRIEF `:36`) — **form alanı 1** |
| `WaveType` | enum | `Cargo` (Kargo çıkışı) \| `Customer` (Müşteri) \| `Region` (Bölge) \| `Manual` (Elle) — BRIEF `:28`; yalnız sınıflandırma ve süzgeç varsayılanı, iş kuralı değiştirmez — **form alanı 2** |
| `CutoffLocal` | yerel tarih-saat (saniyesiz) | deponun saat diliminde kesim saati (D-3) — **form alanı 3**; `CutoffAtUtc` ve `TimeZoneId` sunucuda yazılır (§8.4) |
| `CutoffAtUtc` | date-time | `CutoffLocal` + `TimeZoneId`'den; sıralama, risk ve KPI bununla |
| `TimeZoneId` | string (IANA) | yaratma anında depo ayarından kopyalanır (`WaveWarehouseSettings.TimeZoneId`); sonra değişmez |
| `ShipToFilter` | string ≤ 100 \| null | "Müşteri ya da teslim bölgesi" (mockup v2): outbound `ShipTo.name` / `city` / `postalCode` / `country` üzerinde büyük-küçük harf duyarsız içerir eşleşmesi (Q-21) — **form alanı 4** |
| `MaxOrders` | int 1..1000 \| null | dalga sipariş sınırı (D-7: aşım engellenmez, uyarı) — **form alanı 5** |
| `MaxLines` | int 1..20000 \| null | dalga satır sınırı (D-7) — **form alanı 6** |
| `ResponsibleUserId` | uuid \| null | sorumlu; kullanıcı dizini okuması (0178 `IWarehouseAssigneeDirectory` emsali; 0180 kendi portunu açar) — **form alanı 7** |
| `Note` | string ≤ 500 \| null | — **form alanı 8** |
| `Status` | enum | `Draft` \| `Releasing` (iç) \| `Released` \| `Picking` \| `Completed` \| `PartiallyCompleted` \| `Cancelled` (§8.3) |
| `PickPlan[]` | gömülü | serbest bırakmada bir kez yazılır, sonra değişmez: `{ Sequence, OutboundId, LineSeq, AllocSeq, LocationId, BinCode, ZoneKey, Aisle?, Rack?, Level?, Position?, RouteFallback }` (§8.7) |
| `RouteSummary` | gömülü \| null | `{ TaskCount, FallbackCount, ProxyDistance, ComputedAtUtc, Algorithm = "S-SHAPE-v1" }` |
| `ReleaseLeaseUntilUtc` | date-time \| null | yalnız `Releasing`'de; bakım kurtarma kilidi (§8.6) |
| `ReleaseRequest` | gömülü \| null | `{ IdempotencyKey, RequestFingerprint, ActorUserId, ConfirmLimitOverride }` — bakımın aynı isteği yeniden kurması için |
| `CreatedBy`, `ReleasedBy`, `ReleasedAt`, `CancelledBy`, `CancelledAt`, `CancelReason`, `CompletedAt` | — | sunucu yazar; `CompletedAt` §8.8 kuralıyla 0178 olay zamanından |
| `Recalls[]` | gömülü | her geri çekme: `{ By, At, Reason, WithdrawnOutboundIds[], StartedOutboundIds[] }` (D-9) |
| `IdempotencyKey`, `RequestFingerprint` | string | yaratma anahtarı (8–100); tekil `(TenantId, LegalEntityId, CreatedBy, IdempotencyKey)` (MOD-0178 §4.1 deseni) |
| `Version` | int | If-Match `W/"<version>"` (D-14) |

**form_field_count = 8** (yukarıdaki kalın sekiz alan) → `golden_reference: slim` (≤ 8; `.antigravity/rules/module-pack-standard.md` §2).
**Slim / Compact kararı (Q-1 = A, karar 03 — kesin; r2'de değişmedi):** mockup v2 yaratma panelinde **dokuz** alan gösterir: yukarıdaki sekizine ek **"Taşıyıcı / sevk
yöntemi"**. Bu alanın v1'de **veri kaynağı yoktur**: WAREHOUSE-OUTBOUND outbound'u taşıyıcı alanı taşımaz (`Address`
`docs/analysis/contracts/warehouse-outbound.openapi.yaml:76-85`; outbound alanları MOD-0178 §4.5 `:422-443` — taşıyıcı yok); taşıyıcı MVP-6'da
sevkiyata `ReadyToShip`'ten sonra bağlanır. Karar (Q-1 = A): alan v1 formunda yoktur → 8 alan → **slim** (mockup'ın yan paneli korunur);
taşıyıcı süzgeci FU-0180-CARRIER. (Reddedilen B: 9 alan → compact.)

### 4.2 WaveOrder
| Alan | Tip | Kural |
|---|---|---|
| `WaveId`, `WarehouseId` | uuid | baş kayıt; depo dalganınkiyle aynı olmalı |
| `OutboundId`, `OutboundNumber` | uuid, string | MOD-0178 outbound; numara gösterim kopyası |
| `LineCount`, `AllocationCount`, `PlannedTaskCount` | int | eklemede 0178 outbound'undan okunur (satır, tahsis, `PlannedBins` sayısı); sınır uyarıları için (§8.5); stok değil |
| `State` | enum | `Planned` (Taslakta) \| `Issued` (görevleri açıldı) \| `Withdrawn` (geri çekildi) \| `Excluded` (serbest bırakmada uygun çıkmadı) \| `Removed` (Taslaktan çıkarıldı) \| `OrderCancelled` (outbound 0178'de iptal) |
| `Active` | bool | `Planned` \| `Issued` iken `true`; diğerlerinde `false` — tekil kısmi index'in filtresi (D-8) |
| `IssuedTaskIds[]`, `IssuedTaskNumbers[]`, `IssuedAtUtc` | — | 0178 port sonucundan (§8.6); sonra değişmez |
| `AddedBy`, `AddedAt`, `AddedVia` | — | `AddedVia` = `Manual` \| `Suggestion` (D-1; olay geçmişinde "Siparişleri öner") |
| `ClosedBy`, `ClosedAt`, `CloseReason` | — | `Withdrawn` / `Removed` / `Excluded` / `OrderCancelled` geçişinde |
| `MovedToWaveId` | uuid \| null | "Sonraki dalgaya taşı"da hedef dalga (D-13) |
| `Version` | int | If-Match |

### 4.3 WaveEvent
Insert-only (`wms_wave_events`). Alanlar: `WaveId`, `Seq` (dalga içinde artan), `Kind` (`Created`, `Updated`, `OrdersAdded`, `OrdersRemoved`,
`Cancelled`, `ReleaseStarted`, `Released`, `ReleaseAborted`, `OrderExcluded`, `PickingStarted`, `RemainingRecalled`, `OrderWithdrawn`,
`OrderMoved`, `OrderCancelledUpstream`, `Completed`, `PartiallyCompleted`, `ExceptionRaised`, `ExceptionResolved`, `ExceptionIgnored`,
`ExceptionCleared`), `ActorUserId` \| `system`, `At`, `RequestOp`, `IdempotencyKey?`, `RequestFingerprint?`, `Data` (önceki / sonraki
değerler, gerekçe, sipariş ve görev numaraları). İlgili iş yazımıyla **aynı Mongo işleminde** yazılır; yazılamazsa iş yazımı geri alınır;
silinmez (MOD-0178 §8.8 "Yerel GxP izi" deseni). Olay geçmişi sekmesi bu koleksiyondan okunur (BRIEF `:51`).

### 4.4 WaveException
| Alan | Tip | Kural |
|---|---|---|
| `WaveId`, `WarehouseId` | uuid | — |
| `Kind` | enum | `ShortPick` \| `StockMissing` \| `LotUnderRecall` \| `CutoffMissed` \| `TaskWaitingLong` \| `OrderCancelledInWave` \| `OrderNotIssuable` (BRIEF `:61-62` + serbest bırakma kısmi hatası §8.6) |
| `SubjectKey` | string | tespit anahtarı: `{Kind}:{waveId}:{outboundId}[:{taskId}[:{seq}]]`; tekil `(TenantId, LegalEntityId, SubjectKey)` — aynı istisna iki kez açılmaz |
| `OutboundId`, `OutboundNumber`, `TaskId?`, `TaskNumber?`, `LineSeq?` | — | konu |
| `DetectedAt` | date-time | tespit eden uzlaştırma turunun zamanı |
| `Status` | enum | `Open` \| `Resolved` \| `Ignored` |
| `Resolution` | gömülü \| null | `{ Action: RemoveOrder \| MoveToNextWave \| Ignore \| SystemCleared, TargetWaveId?, By?, At, Reason? }` |
| `Version` | int | If-Match |

### 4.5 WaveWarehouseSettings
Depo başına tek kayıt (`wms_wave_settings`, tekil `(TenantId, LegalEntityId, WarehouseId)`): `WaveManaged` (bool, varsayılan `false` — §8.6,
Q-2), `TimeZoneId` (IANA, ör. `Europe/Istanbul`; `WaveManaged = true` için zorunlu — D-3, Q-3), `RiskMinutesBeforeCutoff` (varsayılan 90 —
D-6), `RiskProgressPercent` (varsayılan 70 — D-6), `PendingTaskMinutes` (varsayılan 30 — D-11), `Version`. Kayıt yoksa: `WaveManaged = false`,
eşikler varsayılan, saat dilimi yok (dalga yaratılamaz — 422 `WAVE_SETTINGS_REQUIRED`).

**Dalga yönetimini kapatma (Y-1, karar 03):** `WaveManaged` `true` → `false` değişikliği, depoda (aynı kiracı + şirket + `WarehouseId`)
`PickIssuance.State = Deferred` olan **ya da** `Allocating` durumundaki (tahsisi bitince `Deferred` olacak) bir MOD-0178 outbound'u varsa
**409 `WAVE_SETTINGS_DEFERRED_ORDERS`** ile reddedilir (`details.deferredCount`, `details.allocatingCount`, ilk 50 outbound numarası); yazım yok.
Kullanıcı önce bekleyen siparişleri bir dalgaya alıp serbest bırakır ya da 0178'de iptal eder (rezervasyonlar 0178'in iptal yoluyla geri bırakılır —
karar 03 "geri bırakılır"ın okunuşu, çıkarım). Sayım 0178 koleksiyonlarında **yalnız `Find`**'dır (§2.3 kural 2). Diğer ayar alanlarının
değişikliği ve `false` → `true` her zaman serbesttir. **Kalan yarış (çıkarım):** 0178 politika okumasını (X-3) ayar yazımından önce yapıp
outbound'u ayar yazımından sonra `Deferred` yazan bir tahsis, kapalı depoda bir ertelenmiş outbound bırakabilir; bu durumda `GET /waves/settings/
{warehouseId}` cevabı `deferredOrderCount > 0` gösterir ve çıkış yolu ayarı yeniden `true` yapıp siparişi dalgaya almaktır (açma her zaman serbest).
Pano KPI'ı "dalgaya girmemiş sipariş" aynı sayıyı gösterir.

### 4.6 Indexes
Kiracı + şirket önce (MOD-0178 §4.6 deseni):
- `wms_wave_waves`: tekil `(TenantId, Number)`; tekil kısmi `(TenantId, LegalEntityId, CreatedBy, IdempotencyKey)`;
  `(TenantId, LegalEntityId, WarehouseId, Status, CutoffAtUtc)` (pano, D-13 hedef arama); `(TenantId, Status, ReleaseLeaseUntilUtc)` (bakım).
- `wms_wave_orders`: **tekil kısmi** `(TenantId, LegalEntityId, OutboundId)` `PartialFilterExpression { Active: true }` (D-8);
  `(TenantId, LegalEntityId, WaveId, State)`.
- `wms_wave_events`: tekil `(TenantId, WaveId, Seq)`; istek anahtarı tekil kısmi `(TenantId, WaveId, ActorUserId, RequestOp, IdempotencyKey)`
  (MOD-0178 `wms_task_events` deseni, `:491-496`).
- `wms_wave_exceptions`: tekil `(TenantId, LegalEntityId, SubjectKey)`; `(TenantId, LegalEntityId, WarehouseId, Status, DetectedAt)`.
- `wms_wave_settings`: tekil `(TenantId, LegalEntityId, WarehouseId)`.
- MOD-0178 koleksiyonlarına 0180 **index eklemez** (okumalar mevcut index'lerle — §8.8; gerekirse XMC-001 ile sorulur).

## 5. Repo Scope

| Yol | Ne | Dilim |
|---|---|---|
| `docs/analysis/contracts/drafts/wms-wave-bundle-v1-proposal.md` (yazılacak) | WMS-WAVE-BUNDLE taslağı (contract-first; **DL-1'in ilk adımı**; dondurma ayrı CT adımı) | DL-1 |
| `INV/Domain/WavePlanning/{Entities,Enums,Rules,Repositories}/` | varlıklar, durum / sınır / risk / rota kuralları (saf) | DL-1…DL-3 |
| `INV/Application/Features/WavePlanning/` | ortak: `WaveContract.cs`, `WaveErrorCodes.cs`, `WaveCentralAudit.cs`, `Ports/`, `Services/` (rota, serbest bırakma motoru, uzlaştırma, istisna tespiti, KPI) | DL-1…DL-5 |
| `INV/Application/Features/Waves/` | Golden Slim: Commands, Queries, Handlers/CommandHandlers, Handlers/QueryHandlers, Validators, `WaveModels.cs` | DL-1…DL-3 |
| `INV/Application/Features/WaveExceptions/` | aynı yapı (çözüm komutu, liste / ayrıntı sorguları) | DL-3 |
| `INV/Application/Features/WaveKpis/` | aynı yapı (yalnız sorgular + CSV) | DL-5 |
| `INV/Persistence/WavePlanning/` | depolar, `WaveCollections`, index yapılandırması, 0178 okumaları (**yalnız `Find`**, `WarehouseCollections.*` sabitleriyle), `WaveMaintenanceHostedService` (yalnız `CompleteWaveReleaseCommand` / `ReconcileWaveCommand`'ı `IMediator` ile gönderir; kendi işlemi yok — 0181 emsali `PendingConfirmationReconcilerHostedService`) | DL-1…DL-3 |
| `INV/Api/Controllers/WavePlanning/` | `WavesController`, `WaveExceptionsController`, `WaveKpisController` — `[Route("api/warehouse")]` (§3) | DL-1…DL-5 |
| `INV/Api/WavePlanning/` | `WavePlanningManifestProvider`, `WavePlanningServiceCollectionExtensions` | DL-1 |
| `INV/Infrastructure/Authorization/WavePermissions.cs` | izin sabitleri | DL-1 |
| `INVT/WavePlanning/` | birim, entegrasyon, mimari testler (0180'in kendi test ağacı; 0178 davranış testleri **burada**) | her dilim |
| `frontend/Diten.Web/Views/Warehouse/{Waves,WavePlanner,WaveExceptions,WaveKpis}/`, `frontend/Diten.Web/Controllers/`, `frontend/Diten.Web/wwwroot/assets/js/Warehouse/…`, `frontend/Diten.Web/Resources/Views/Warehouse/…` | ekranlar, proxy controller'lar, JS, resx (7 dil) | DL-4, DL-5 |
| `frontend/Diten.Web/Resources/SharedResource.{en,tr,fr,es,zh,ar,ru}.resx`, `frontend/Diten.Web/Resources/PageHelpResource.{en,tr,fr,es,zh,ar,ru}.resx` | ortak Web kaynakları — **yalnız eklemeli anahtar**: nav (`Nav.Module.*`, `Nav.Page.*`), sayfa yardımı (`Help.*`, §25) ve Rol İzinleri satır adları (`Perm.Entity.*` — `SharedResource`'ta; 0181 `Perm.Entity.COUNTS` / `.VARIANCES` emsali, `SharedResource.en.resx:1317`, `:1320`; ekranın fiil köprüsü ayrı Governance dosyalarıdır — X-15); mevcut anahtar değişmez | `Perm.Entity.*` DL-1, DL-3, DL-5 (manifestin anahtar bildirdiği dilim); nav + yardım DL-4, DL-5 |
| `frontend/Diten.Web.Tests/Warehouse/WavePlanningL10nAndHelpTests.cs` (yeni) | 0180'in kendi L10n / Layout / yardım testi (0181 `CycleCountL10nAndHelpTests.cs:235` emsali — Layout satırı); 0178 testi değişmez | DL-4 |
| `gateway/Diten.ApiGateway.Tests/Mod0180WaveRouteTests.cs` (yeni, integration-agent) | uç kapsama testi — üretim `ocelot.json`'u (test projesine bağlı kopya) ve üretim controller kaynağını okur (§15); `ocelot.json` değişmez | DL-6 |

### 5.1 XMC-001 — başka modülün dosyalarına dokunuşlar (her biri dokunmadan önce sahibine sorulur; tek tek)

| # | Dosya (ölçüm `dc010b911`) | Bugün | Gereken değişiklik (yalnız eklemeli) | Sahip | Dilim |
|---|---|---|---|---|---|
| X-1 | `INV/Domain/Warehouse/Entities/OutboundShipmentEntities.cs` | outbound'da görev üretim kipi yok | nullable `PickIssuance { State: Deferred \| Issued, WaveId?, WaveNumber?, IssuedAtUtc? }`; `null` = bugünkü davranış (görev tahsiste açılır) | MOD-0178 (MVP-1 CT, Beste) | DL-2 |
| X-2 | `INV/Domain/Warehouse/Entities/` altındaki `WarehouseTask` varlığı | dalga bağı yok | nullable `WaveRef { WaveId, WaveNumber, Sequence }` (yalnız `Kind = Pick`) | MOD-0178 | DL-2 |
| X-3 | `dc010b911:INV/Application/Features/OutboundShipments/Services/OutboundAllocationService.cs:172-185`, `:200-213`, `:227-233`, `:248`, `:288` | işlem B pick görevi numaralarını işlem dışında alır (`:172-185`), görevleri kurar (`:200-213`), outbound + olay + görevleri tek `InTransactionAsync`'te yazar (`:227-233`); görev kurucusu `PickTask` `private static` (`:248`), `Created` olay kurucusu `TaskCreated` `private` örnek yöntemi (`:288`) | (a) **kurucuların taşınması (F-16):** `PickTask` ve `TaskCreated` **davranış değişmeden** 0178 içinde ortak bir yardımcıya taşınır (öneri `INV/Application/Features/OutboundShipments/Services/OutboundPickTaskFactory.cs`, X-4 dosyaları arasında); `OutboundAllocationService` ve `WavePickIssuanceService` aynı yardımcıyı çağırır (AC-19'un "0178 `PickTask` kuruluşuyla aynı" şartı bu yüzden tek kaynaktan karşılanır); taşımanın davranış değiştirmediğinin kanıtı 0178'in mevcut S2 testlerinin değişmeden yeşil kalmasıdır (AC-18). (b) **erteleme dalı:** `IPickIssuancePolicy.ForWarehouse(...)` `Deferred` derse numara alınmaz, görev kurulmaz, outbound `Allocated` + `PickIssuance.State = Deferred` yazılır, `Allocated` olayının `pickTasks` verisi boş + `pickIssuance = Deferred`; `Immediate` (varsayılan) dalında kod yolu **değişmez** | MOD-0178 | DL-2 |
| X-4 | yeni 0178 dosyaları: `INV/Application/Features/Warehouse/Ports/IPickIssuancePolicy.cs` (+ varsayılan `ImmediatePickIssuancePolicy`), `INV/Application/Features/Warehouse/Ports/IWavePickIssuance.cs`, `INV/Application/Features/OutboundShipments/Services/WavePickIssuanceService.cs`, `INV/Application/Features/OutboundShipments/Services/OutboundPickTaskFactory.cs` (X-3 a) | yok | port + 0178 içi uygulama (§8.6): görev açma ve geri çekme **0178 kodunda**; **adlar (F-12, tek ad):** port `IPickIssuancePolicy` (0178), 0180'in uygulaması `WavePickIssuancePolicy` (0180 `Features/WavePlanning/Ports/` altında, depo ayarını okur); 0180 yalnız `WavePickIssuancePolicy`'yi `IPickIssuancePolicy` olarak kaydeder (DI, X-11) ve `IWavePickIssuance`'ı çağırır | MOD-0178 | DL-2 |
| X-5 | `dc010b911:INV/Domain/Warehouse/Enums/OutboundShipmentEnums.cs:22-39` | `OutboundEventKind` 15 değer | eklemeli `PickTasksIssued`, `PickTasksWithdrawn` | MOD-0178 | DL-2 |
| X-6 | `dc010b911:INV/Domain/Warehouse/Rules/WarehouseRecordCodeRules.cs:14-33` | `TypePrefixes` = PUT / PCK / OUT / PKG / CNT (`:28`); bilinmeyen önek reddedilir | **Q-4 = A (karar 03):** `Wave = "WAV"` sabiti + listeye `WAV` (MOD-0181 `CNT` emsali, `:26`) | MOD-0178 | DL-1 |
| X-7 | MOD-0178 Toplama Görevleri listesi: `GetPickTaskListQuery` (`INV/Application/Features/PickTasks/Queries/PickTaskQueries.cs`), `INV/Api/Controllers/Warehouse/PickTasksController.cs`, Web `Views/Warehouse/PickTasks/` + `index.js` + resx (7 dil) | dalga yok | **Q-12 = A (karar 03):** isteğe bağlı `waveId` süzgeci, `sort=waveSequence`, liste sütunu "Dalga" (`WaveNumber` + sıra); varsayılan sıralama değişmez | MOD-0178 | DL-4 |
| X-8 | `docs/analysis/contracts/wms-bundle.openapi.yaml` (MOD-0178'in sözleşmesi, `version: 1.1.0` `:64`) | dalga üyesi yok; `OutboundEventKind` kapalı enum, 15 değer (`:1812-1814`); servis sözleşme testleri şemayı **kapalı** okur — `additionalProperties` yazmayan her nesne `false` sayılır (`dc010b911:INVT/TestInfrastructure/ContractSchemas.cs:19-21`, `:404-407`) | eklemeli: (1) `OutboundEventKind` enum'una `PickTasksIssued`, `PickTasksWithdrawn` (X-5'in tel karşılığı); (2) pick görevi (`WarehouseTask` `:1352`, `OutboundPickTask` `:1694`) `wave` üyesi; (3) outbound görünümü (`OutboundShipmentView` `:1757`) `pickIssuance` üyesi; (4) X-7 süzgeç / sıralama (`waveId`, `sort=waveSequence`). **Sıra (F-3):** şemalar kapalı olduğu için DTO'ya `pickIssuance` / `wave` eklendiği an değişmeyen 0178 sözleşme testleri kırmızı olur → eklemeli sürüm **DL-2 başlamadan önce** yazılır ve dondurulur (CT adımı; 0180 dev dilimi yazmaz). **Sürüm numarası sabitlenmez:** MOD-0178 r28 WMS-BUNDLE 1.2.0'ı Item adımına ayırdı (MOD-0178 paketi `:25`); 0180 üyeleri "0178 CT ile eşgüdümlenen bir sonraki eklemeli sürüm (1.2.0 ya da 1.3.0)"e girer — **hangisi olacağı CT kararıdır** (§19.4 CT-1). (4) aynı sürüme girer (Q-12 = A); CT ayrı sürüm seçerse (4) DL-4 başlamadan önce | MOD-0178 + CT | DL-2 başlamadan önce CT adımı ((4) en geç DL-4 öncesi) |
| X-9 | `dc010b911:INVT/Architecture/SingleSystemOfRecordArchitectureTests.cs:88-89`, `:92-93`, `:100-115`, `:120` | 0181 bildirimleri `CycleCountCollections.All` (`:88-89`) ve `.Evidence` (`:92-93`); `declared` listesi `:100-115`; ad öneki süzgecinde `wms_` zaten var (`:120`, MOD-0178 DL-1) | `WaveCollections.All` için `Assert.Equal` (0181 satırlarının yanında) + `declared`'a `.Concat(WaveCollections.All)`; önek süzgeci **değişmez** (`wms_wave_*` `wms_` ile yakalanır) | MOD-0173 (MVP-1 CT, Beste) | DL-1 (DL-3'te `wms_wave_exceptions` aynı listeye eklenir) |
| X-10 | `dc010b911:INVT/CentralAudit/CentralAuditDeclarationTests.cs:53`, `:60-69`, `:130` | `Assert.Equal(117, WriteCommands.Length)` (`:53`; 116 → 117 MOD-0181 DL-5, `c76c02944`); sağlayıcı listesi (`:60-69`); `Assert.Equal(14, seen)` (`:130`) | sayı = **dilim başındaki ölçülen sayı + dilimin 0180 yazma komutları** (0181 emsali, `:44-52` yorumları; 0180 toplamı 11) — paket sabit bir taban sayı yazmaz, dilim kaydı tabanı o dilimin başlangıç commit'inde ölçer; listeye `WavePlanningManifestProvider`; `seen` 0180 depolarının `WithTransactionAsync` geri çağrıları kadar artar (yalnız yeni işlem açan depo) | MOD-0173 (MVP-1 CT, Beste) | DL-1…DL-3 |
| X-11 | `INV/Api/Program.cs` | manifest kayıtları | `WavePlanningManifestProvider` + `AddWavePlanning()` (politika uygulamasının DI kaydı dahil; kompozisyon kökü) | MOD-0173 (MVP-1 CT, Beste) | DL-1, DL-2 |
| X-12 | `services/Diten.Platform/tests/Diten.Platform.Application.Tests/RecordCodes/RecordCodeTypeCatalogTests.cs` (commit `2f7b6145e` `RuleAheadOfCatalogue`) | `CNT` → "MOD-0040-FU02 DL-8" satırı; RCS-001 §3'teki her OTOMATİK satır ya katalogda ya bu sözlükte olmalı | RCS-001 §3'e `WAV` satırı yazıldığı **aynı adımda** sözlüğe `["WAV"] = "MOD-0040-FU02 DL-8"` (yoksa test kırmızı) | MOD-0040-FU02 (Beste, K2) | `.antigravity` kapısı adımı (§6) |
| X-13 | `services/Diten.AuthService/**` rol şablonu | — | planlayıcı / vardiya sorumlusu / performans izleyici rol önerisi (§14) | AuthService — Ali (Auth) | ayrı WP |
| X-14 | `gateway/Diten.ApiGateway.Tests/Mod0178WarehouseRouteTests.cs` | 0178 uç kapsama testi; üretim `ocelot.json`'u okur (`:23`; test projesine bağlı kopya `gateway/Diten.ApiGateway.Tests/Diten.ApiGateway.Tests.csproj:26`) ve **bütün** InventoryService controller'larını özyineli tarar — sınıf `[Route]`'u `api/warehouse` ile başlayan her controller (`:219-236`) | **değişmez.** Ölçülen sonuç: 0180 controller'ları (`[Route("api/warehouse")]`, `Controllers/WavePlanning/`) bu testin taramasına **kendiliğinden girer**; her 0180 ucu `/api/warehouse/{everything}` ile tam bir rotaya eşleşir, PATCH yoktur (§3) → test değişmeden yeşil kalmalıdır (AC-55). 0180 ayrı dosya açar (X-yok; bilgi) | integration-agent | — |
| X-15 | Governance Rol İzinleri köprüsü: `frontend/Diten.Web/Views/Governance/RoleAssignments/_IndexL10n.cshtml` (`ActionVerbs` köprüsü; `configure` `:239`, `release` `:269`, `resolve` `:276`, `read` / `create` / `update` / `export` / `cancel` `:165-196` var; **`recall` yok**), `frontend/Diten.Web/Resources/Views/Governance/RoleAssignments/RoleAssignmentsIndex.{en,tr,fr,es,zh,ar,ru}.resx` (`ActionVerb_*`), `frontend/Diten.Web/wwwroot/assets/js/Governance/RoleAssignments/perm-label.js` (`ACTION_FAMILIES` `:52-54`; `configure` `:52`, `release` / `resolve` `:54` var; **`recall` fiil olarak yok**) | ölçen bekçi `frontend/Diten.Web/tests/role-assignments-label-ledger.test.js` üretim manifestlerini okur; defter `docs/roadmap/backlog/role-permission-label-ledger.md` "Tavan: fiil 0 · satır 96" — fiil defteri **boş kalmak zorunda**, yeni fiil deftere yazılamaz | eklemeli: (1) yeni fiil **`recall`** (`warehouse.waves.recall`, DL-3): köprüye `["recall"] = Localizer["ActionVerb_Recall"].Value`, 7 resx'e `ActionVerb_Recall` (en dışı değerler en'den farklı), `perm-label.js` `ACTION_FAMILIES`'te bir aileye `recall` (öneri: `danger` / geri alma ailesi — kesin aile sahibin); (2) **`settings.configure`** (Q-6 = B, DL-1): fiil `configure` köprüde **var** (`:239`, aile `:52`) — fiil eklemesi gerekmez, yalnız satır adı; (3) satır adları `SharedResource.{7}.resx` `Perm.Entity.*` (§5 tablosu; beklenen kodlar `WAVES`, `WAVESSETTINGS`, `WAVESEXCEPTIONS`, `WAVESKPIS` — **çıkarım**, kesin kodu üretim `PermLabel.split` verir, DL-1'de vitest ile ölçülür). **Sahip (depoda ölçüldü):** bu dosyaları sahiplenen bir module pack ya da domain-config satırı **yok** (`git grep` `execution/`: 0); ekranı Ali kurdu (`af9602775`, `d593edf4a`, `8bcd75314`; BL-508 / WP-WEB-ROLE-PERMISSION-LABELS-01), son eklemeli değişiklik MOD-0181 DL-3 (`65b968b64`, Beste "Köprüye eklemeli yaz"), 0181 r8 bu dokunuşun sahip teyidini hâlâ açık yazar (MOD-0181 paketi `:1395`). **XMC-001 durumu:** açık — dokunmadan önce sorulur; soru Ali'ye (RBAC / MOD-0018, ekranın kurucusu) ve Beste'ye (merkez) | Governance Rol İzinleri — **sahip TBD** (öneri Ali, MOD-0018) | DL-1 (satır adları), DL-3 (`recall` + satır), DL-5 (satır) |
| X-16 | `execution/domains/supply-chain-execution/module-packs/MOD-0178-putaway-picking-packing.md` §14.1 (`:2117-2120`) | "`warehouse.*` MOD-0178'in; MOD-0180 / 0181 / 0182 kendi namespace'lerini açar" (Q-08) | Q-7 = A (karar 03): §14.1'e not — "`warehouse.waves.*` alt ad alanı MOD-0180'indir (D-4, 2026-11-12; PKS-001 §4 satırı)"; 0178 paket revizyonu (yalnız not, davranış yok) | MOD-0178 (MVP-1 CT, Beste) | `.antigravity` kapısı adımıyla (§6) |

**MOD-0178 bekçi testleri değişmez (bağlayıcı):** `INVT/Warehouse/**`, `INVT/PickTasks/**` (varsa) ve 0178 adını taşıyan mimari testler
**hiçbir satırı değişmeden** yeşil kalır; 0178'e eklenen davranış (X-1…X-5) 0180'in test ağacında (`INVT/WavePlanning/`) ölçülür. Paylaşılan servis
geneli bekçiler (X-9, X-10) MOD-0173'ündür ve yalnız eklemeli değişir (MOD-0181 emsali). Kanıt: kabul kaydında
`git diff --stat <dilim-başı>..<dilim-sonu> -- services/Diten.InventoryService/tests/Diten.InventoryService.Tests/Warehouse` boş (AC-18).

## 6. Protected Paths
- `.antigravity/**` — kullanıcı onayı ister (Çekirdek md. 6). **Kapı — paket onayından ÖNCE, sahip onayıyla (Y-4, karar 03; D-4 "paket
  hazırlanırken yazılır"):** iki satır + aynı adımda X-12 ve X-16. **Hazırlayan:** CT (satırları hazırlar); **onaylayan:** Beste (sahip —
  `.antigravity/**` değişikliği kullanıcı onayıyla); bu paket ve geliştirme dilimleri `.antigravity/**`'a **yazmaz**. **Kapanış:** paketin
  onay satırı (CT-APR-01) yazılmadan önce; kapı kapanmadan paket `approved` / `ready-for-dev` olmaz (§18, §24). r1'in "modül PR'ı açılmadan
  önce; paket onayını engellemez" zamanlaması (MOD-0181 emsali, MOD-0181 paketi `:573-576`) karar 03 ile **değişti**. Önerilen satır metinleri
  (son metin kapı adımında sahip onayıyla; paket bu metni yalnız önerir):
  (1) **PKS-001 §4** (`.antigravity/rules/permission-key-standard.md:105`; bugün `warehouse.*` satırı bütünüyle MOD-0178'in, Q-08 "one
  namespace per WMS module"), Q-7 = A — `warehouse.*` satırının sonuna "(`warehouse.waves.*` hariç — MOD-0180)" ve hemen altına yeni satır:
  `` | `warehouse.waves.*` | `Diten.InventoryService` — MOD-0180 Wave Planning (dalgalar, dalga istisnaları, dalga KPI'ları, depo dalga
  ayarı; owner decision D-4 2026-11-12 — `warehouse.*` altında MOD-0180'e ayrılmış alt ad alanı, Q-08 "one namespace per WMS module"
  kuralına adlı istisna; MOD-0180 pack §14; base path `/api/warehouse/waves`) | MOD-0180 (Wave Planning) | ``
  (2) **RCS-001 §3** (`.antigravity/rules/record-coding-standard.md:45-75` tablosunda `WAV` yok, ölçüldü; satır `:71` `CNT` emsali):
  `` | Dalga (MOD-0180) | OTOMATİK | `WAV` | yok — **yeni** (sahip kararı D-4, 2026-11-12; kod yok); Inventory servisinde MOD-0178'in geçici
  sayacından üretilecek (`IWarehouseRecordCodeAllocator`, MOD-0180 Q-4 = A); ortak servise (MOD-0040-FU02) geçişte DL-8'e `WAV` eklenir
  (FU-0180-RCS) | ``
  (3) aynı adımda **X-12** (Platform `RecordCodeTypeCatalogTests` `RuleAheadOfCatalogue` sözlüğüne `["WAV"] = "MOD-0040-FU02 DL-8"` — yoksa
  RCS-001 satırı bu testi kırmızı yapar) ve **X-16** (MOD-0178 §14.1 notu). İzleme: §18'deki işaretsiz madde.
- `gateway/Diten.ApiGateway/**/ocelot.json` — yalnız integration-agent; bu paket değişiklik **istemez** (§15). AC-55'in S-55a sabotajı dosyayı
  integration-agent eliyle geçici bozar ve geri alır; kalıcı fark bırakmaz.
- `services/Diten.Building.Blocks/**` (BL-501 olduğu gibi).
- MOD-0178 kodu ve testleri — yalnız §5.1 satırları; başka 0178 dosyası yok. MOD-0173 / 0174 / 0175 / 0176 / 0177 / 0181 dosyası yok.
- Donmuş sözleşmeler: `docs/analysis/contracts/warehouse-outbound.openapi.yaml` (1.0.0 — 0180 değiştirmez), `wms-bundle.openapi.yaml` (yalnız X-8,
  0178'in CT adımıyla), `location.openapi.yaml`, `record-code-allocate.md`.
- Başka domain servisleri; `services/Diten.AuthService/**` (X-13, ayrı WP); Archive; `_Layout.cshtml`.

## 7. Dependencies

| Bağımlılık | Tür | Durum (ölçüm) | Kullanım | Değişiklik | Sahip |
|---|---|---|---|---|---|
| MOD-0178 S2 outbound + tahsis | HARD | kod var — `dc010b911:INV/Application/Features/OutboundShipments/Services/OutboundAllocationService.cs`; kalıcı pick planı `PlannedBins[]` (MOD-0178 §4.5 `:452`) | uygun sipariş listesi, satır / tahsis / plan okuması | X-1, X-3 | MVP-1 CT (Beste) |
| MOD-0178 pick görevi + onay | HARD | görev outbound'la birlikte açılır (`OutboundAllocationService.cs:200-213`); tek başına iptal edilmez, outbound iptali kapatır (`MOD-0178…md:1397-1398`; kod `dc010b911:INV/Application/Features/OutboundShipments/Handlers/CommandHandlers/OutboundFlowHandlers.cs:161`); bütün görevler terminal → outbound `Picked` (`dc010b911:INV/Application/Features/PickTasks/Services/PickTaskCompletion.cs:39`) | görev üretimi (port), ilerleme, istisna | X-2, X-4, X-5 | MVP-1 CT (Beste) |
| MOD-0178 paketleme / sevk durumu | okuma | outbound durumları `Packed` / `ReadyToShip` / `Shipped` (MOD-0178 §8.18 `:1335-1345`) | D-5 ilerleme | yok | MVP-1 CT (Beste) |
| MOD-0173-FU02 LOCATION | HARD (okuma) | yol `Site→Warehouse→StorageLocation→Zone→Bin` (`docs/analysis/contracts/location.openapi.yaml:6`); v1.1 toplu bin kodu `{AISLE}-{RACK:00}-{LEVEL:00}-{POS:00}` (`:148`, `:280-287`); **koridor / raf alanı yok** — yalnız kod (ölçüldü `:205-252`); **saat dilimi alanı yok** (`git grep -i timezone` LOCATION sözleşmesi + InventoryService: 0) | rota girdisi (§8.7), depo doğrulaması | yok (Q-3 = A, karar 03) | MVP-1 CT (Beste) |
| MOD-0040-FU02 RCS-001 | SOFT (hedef üretici) | `status: ready-for-dev`, `approval_status: pending`; Inventory bağlantısı ve WMS türleri **DL-8**'de (`execution/domains/platform-shared-services/module-packs/MOD-0040-FU02-record-code-service.md:2604`); DL-8 bugün yok | `WAV` kodu | ara üretici Q-4 | Beste (K2) |
| MOD-0018 RBAC + AuthService rol şablonu | HARD | `[HasPermission]` + manifest | izinler, roller | rol şablonu ayrı WP (X-13) | Ali (Auth) |
| BL-501 merkezi denetim | HARD | InventoryService'te var — `tests/architecture/audit-ledger/Diten.InventoryService.md:12` (`inventory-merkezi-iletim`, yol b) | §21 | yok | Building.Blocks korunan yol |
| Kullanıcı dizini | SOFT | 0178 `IWarehouseAssigneeDirectory` (`INV/Application/Features/Warehouse/Ports/IWarehouseAssigneeDirectory.cs`) | sorumlu seçimi, toplayıcı adları | yok (0180 kendi portunu açar) | MVP-1 CT |
| MDM tüzel kişilik `Code` | HARD | MOD-0178 M-2 yolu | `{ŞİRKET}` | yok | MDM |
| WMS-WAVE-BUNDLE | HARD (contract-first) | yok (taslak dizininde yok, ölçüldü) | sözleşme taslağı | bu paket (DL-1) | supply-chain-execution (Beste) |
| WMS-BUNDLE (0178) | HARD (DL-2 kapısı) | 1.1.0 (`docs/analysis/contracts/wms-bundle.openapi.yaml:64`); kapalı şema okuması (`dc010b911:INVT/TestInfrastructure/ContractSchemas.cs:19-21`) | X-8 eklemeli üyeler + `OutboundEventKind` değerleri | 0178 CT adımı, **DL-2 başlamadan önce**; sürüm numarası CT kararı (CT-1) | MOD-0178 + CT |
| Governance Rol İzinleri köprüsü | HARD (DL-1 / DL-3 / DL-5 kabulü) | `role-assignments-label-ledger.test.js` üretim manifestlerini okur; `recall` fiili köprüde yok (X-15) | yeni fiil + satır adları 7 dilde | X-15 (eklemeli) | sahip TBD (öneri Ali) |
| Gateway | — | `/api/warehouse/{everything}` → 5067 (`dc010b911:gateway/Diten.ApiGateway/ocelot.json:1153-1161`) | §15 | `ocelot.json` değişmez | integration-agent |
| MVP-6 taşıyıcı / sevkiyat | bağımlılık DEĞİL (v1) | taşıyıcı outbound'da yok | Q-1 | — | MVP-6 |

## 8. Runtime Constraints

### 8.1 Servis ve kapsam
- Servis `Diten.InventoryService`, port **5067** (`AGENTS.md` §3; `domain-config.md:72`). Web (5001) yalnız Gateway (5000) üzerinden çağırır.
- MongoDB (repo deseni); her 0180 yazımı `WithTransactionAsync` içinde; olay + iş yazımı + merkezi denetim enlist'i aynı işlemde (§8.13).
- Kiracı + şirket: `TenantId` token'dan, `LegalEntityId` `X-Legal-Entity-Id`'den sunucuda çözülür (scope middleware; `/api/warehouse` zaten
  kapsanır — MOD-0181 §15 ölçümü). Başka kiracının / şirketin kaydı **404**.
- Depo kapsamı: her sorgu `warehouseId` ister (pano ve kuyrukta zorunlu süzgeç — BRIEF `:31`); dalga başka depodaki outbound'u içeremez.
- Silme yok; DELETE / PUT ucu yok (§3). `LedgerImmutabilityArchitectureTests` PUT / DELETE listesi (`dc010b911:INVT/Architecture/LedgerImmutabilityArchitectureTests.cs:131-160`) **değişmez** (AC-8).

### 8.2 Dalga kurma ve uygunluk
Kararlar: D-1, D-8.
1. **Yaratma** (`CreateWaveCommand`): depo ayarında `WaveManaged = true` ve `TimeZoneId` dolu olmalı (aksi 422 `WAVE_SETTINGS_REQUIRED`);
   dalga `Draft` doğar, siparişsiz olabilir.
2. **Uygun sipariş** (planlayıcı sol listesi, öneri ve ekleme için tek tanım): aynı kiracı + şirket + depo; MOD-0178 outbound `Status =
   Allocated` **ve** `PickIssuance.State = Deferred` (görevleri henüz açılmamış — §8.6); etkin dalga üyeliği yok (`wms_wave_orders` `Active =
   true` kaydı yok). Planlayıcıda `Allocating` (iç) outbound gri ve "rezervasyon bekliyor" nedeniyle gösterilir, eklenemez (BRIEF `:57`);
   `AllocationFailed`, `Picked` ve sonrası, `Cancelled` ve görevleri açılmış (`PickIssuance` `null` ya da `Issued`) outbound listede yoktur.
3. **Ekleme** (`AddWaveOrdersCommand`, yalnız `Draft`): `outboundIds` 1..200; her biri uygun olmalı (aksi 422 `WAVE_ORDER_NOT_ELIGIBLE`,
   satır sonucu); sipariş **bütünüyle** eklenir (D-8). Başka etkin dalgadaysa 409 `WAVE_ORDER_IN_OTHER_WAVE`; eşzamanlı iki eklemede tekil
   kısmi index ikinciyi reddeder (aynı işlem, kaybedende kayıt yok). Sınır aşımı engellemez (§8.5).
4. **Çıkarma** (`RemoveWaveOrdersCommand`, yalnız `Draft`): üyelik `Removed`, `Active = false`; gerekçe isteğe bağlı.
5. **Öner** (`GetWaveSuggestionListQuery`, D-1): uygun siparişlerden dalganın `ShipToFilter`'ına uyanlar, outbound `CreatedAt` artan (en eski
   önce), dalganın kalan `MaxOrders` / `MaxLines` kapasitesi kadar (sınır yoksa en çok 200). **Yazmaz**; kullanıcı seçip "Ekle" der
   (`AddedVia = Suggestion`). Periyodik otomatik kurma yok.
6. **Sipariş başına kesim saati yok:** outbound kesim / sevk tarihi taşımaz (MOD-0178 §4.5 `:422-443`); planlayıcı sütunu "—" gösterir,
   dalganın kesimi dalganındır (Q-22).

### 8.3 Durum makinesi
| Durum | Geçiş | Komut | Koşul |
|---|---|---|---|
| `Draft` | → `Releasing` → `Released` | `ReleaseWaveCommand` (+ gerekirse `CompleteWaveReleaseCommand`) | §8.6; ≥ 1 sipariş |
| `Draft` | → `Cancelled` | `CancelWaveCommand` | gerekçe 10–500 zorunlu; üyelikler `Removed`; **silinmez** |
| `Releasing` (iç) | → `Released` · → `Draft` (`ReleaseAborted`, hiç görev açılmadıysa) | bakım / istek | §8.6 |
| `Released` | → `Picking` | `ReconcileWaveCommand` | dalga görevlerinden biri `InProgress` / `Completed` ya da onay aldı |
| `Released` \| `Picking` | → `Completed` | `ReconcileWaveCommand` | `Issued` her siparişin dalga görevleri terminal **ve** hiç `Withdrawn` / `Excluded` / `OrderCancelled` üyelik yok |
| `Released` \| `Picking` | → `PartiallyCompleted` | `ReconcileWaveCommand` | `Issued` siparişlerin görevleri terminal **ve** en az bir üyelik `Withdrawn` (D-9) ya da `Excluded` ya da `OrderCancelled` (karar 03 Y-2 — D-9'un genişlemesi) |
| `Released` \| `Picking` | (durum değişmez) | `RecallWaveRemainingCommand` | §8.9; sonraki uzlaştırma `PartiallyCompleted`'a götürebilir |

- İptal **yalnız `Draft`**'tadır; serbest bırakılmış dalga için tek yol "kalan görevleri geri çek"tir (D-9; BRIEF `:19`). `Released` ve sonrası
  `cancel` → 409 `WAVE_INVALID_STATE`.
- Kesim saati geçince **otomatik kapanış yoktur**; `CutoffMissed` istisnası açılır (D-9, §8.10).
- Kısa toplama (0178 kısa kapatma) görevi terminal yapar; dalga `Completed` olabilir, ayrıca `ShortPick` istisnası açılır (Q-10).
- `Completed`, `PartiallyCompleted`, `Cancelled` terminaldir; terminal dalgada her yazma 409 `WAVE_INVALID_STATE` (istisna çözümü hariç —
  istisna kaydının kendisi yazılır, dalga değişmez).
- Serbest bırakılmış dalgada sipariş listesi salt okunurdur; yalnız geri çekme ve istisna eylemleri (BRIEF `:75`).

### 8.4 Kesim saati ve saat dilimi
Karar: D-3.
- Kesim saati **deponun yerel saat diliminde** girilir ve gösterilir (D-3). Saat dilimi kaynağı: `WaveWarehouseSettings.TimeZoneId` (IANA) —
  LOCATION'da alan yoktur (ölçüldü, §7); kalıcı yer kararı Q-3 = A (karar 03; FU-0180-TZ-LOCATION).
- `CutoffAtUtc = ToUtc(CutoffLocal, TimeZoneId)`; **yaz saati boşluğundaki** yerel saat (var olmayan an) → 400 `WAVE_CUTOFF_INVALID`; **çakışan**
  (iki kez yaşanan) yerel saat → erken ofset seçilir ve cevapta `cutoffAmbiguousResolved = true`. Yaratma / düzenlemede `CutoffAtUtc` şimdiden en
  çok 1 saat geride olabilir (geç girişe pay), daha eskiyse 400 `WAVE_CUTOFF_INVALID`.
- Ekranda kesim saati depo saatiyle yazılır; kullanıcının saat dilimi (tarayıcı) depodan farklıysa ikisi de gösterilir ("14:30 depo · 13:30
  sizin") (D-3). Geri sayım `CutoffAtUtc`'den.
- Gün sınırları (KPI günlük kovaları, "bugün serbest bırakılan") deponun yerel gününe göredir.

### 8.5 Sınırlar ve uyarılar
Kararlar: D-7, D-12.
- `MaxOrders` / `MaxLines` **iş sınırıdır, engellemez** (D-7): ekleme sınırı aşsa da yazılır; planlayıcı kartı doluluk çubuğu + "Sipariş sınırı
  aşıldı (n / m)" / "Satır sınırı aşıldı (n / m)" uyarısı gösterir (mockup v2). Serbest bırakmada sınır aşılmışsa istek
  `confirmLimitOverride = true` taşımalıdır, aksi 409 `WAVE_LIMIT_CONFIRMATION_REQUIRED` (cevapta aşımlar); ekran bunu onay modalıyla sorar.
- **Ağırlık / hacim (D-12):** outbound satırı ağırlık / hacim taşımaz (WAREHOUSE-OUTBOUND `OutboundLine` `:86-97`); değer yoksa "—" gösterilir,
  v1'de sınır değildir; toplam hesaplanmaz.
- **Teknik üst sınır (iş sınırından ayrı, engelleyici — Q-8 = A, karar 03):** dalga başına en çok **500 sipariş** ve **2.000 planlı toplama görevi**
  (`Σ PlannedTaskCount`); aşımda ekleme 422 `WAVE_TOO_LARGE`. Gerekçe: rota bütçesi (§8.7) ve tek dalga belgesinin boyutu.
- Uyarı türleri (kart ve ayrıntı): sınır aşımı, kesime < `RiskMinutesBeforeCutoff` kaldı (yalnız Taslak'ta "zaman dar"), farklı saklama koşullu
  Staging yok (bilgi değil — kapsam dışı). Taşıyıcı uyarısı ("n sipariş farklı taşıyıcıda") v1'de **yok** (veri yok, Q-1).

### 8.6 Serbest bırakma ve MOD-0178 seam'i
**Ön koşul — görevlerin ertelenmesi (X-1, X-3, X-4; Q-2):** dalga yönetimli depoda (`WaveManaged = true`) MOD-0178 tahsis bittiğinde pick
görevi **açmaz**: outbound `Allocated` + `PickIssuance.State = Deferred` olur; kalıcı plan (`PlannedBins[]`) zaten işlem A'da yazılıdır
(MOD-0178 §8.17 adım 5 `:1290-1298`). Karar 0178'in portundadır: port **`IPickIssuancePolicy`** (0178; varsayılan uygulama
`ImmediatePickIssuancePolicy` — bugünkü davranış); 0180 kendi uygulaması **`WavePickIssuancePolicy`**'yi `IPickIssuancePolicy` olarak kaydeder
(depo ayarını okur; X-4, X-11 — tek ad, F-12). `WaveManaged = false` ya da ayar yok → `Immediate` → 0178 kod yolu ve çıktısı bugünküyle aynı
(AC-18). **Ayar `true` → `false` (Y-1, karar 03):** depoda `Deferred` ya da `Allocating` outbound varken **reddedilir** (409
`WAVE_SETTINGS_DEFERRED_ORDERS`, §4.5); böylece ertelenmiş sipariş dalga yaratılamayan (422 `WAVE_SETTINGS_REQUIRED`) bir depoda takılı
kalmaz. Kalan yarış ve çıkış yolu §4.5'tedir.

**Port (0178'in, X-4) — `IWavePickIssuance`:**
- `IssueAsync(WaveIssueRequest)`: `{ WaveId, WaveNumber, ActorUserId, OutboundId, Items[] { AllocSeq, LocationId, Sequence } }` — **sipariş
  başına bir çağrı**. 0178 kendi işleminde: outbound `Allocated` ve `PickIssuance.State = Deferred` (ya da aynı `WaveId` ile `Issued` → **replay**,
  yazım yok, mevcut görev kimlikleri döner); `Items` kümesi outbound'un `(AllocSeq, LocationId)` plan kümesine **eşit** olmalı (aksi
  `PlanMismatch`); görev numaraları sayaçtan işlem dışında (0178 deseni `OutboundAllocationService.cs:172-185`); görevler 0178'in `PickTask`
  (`:248`) ve `TaskCreated` (`:288`) kuruluşuyla — X-3 (a) ile ortak yardımcıya taşınmış **aynı** kurucular — + `WaveRef { WaveId, WaveNumber,
  Sequence }`; outbound `PickIssuance = Issued(WaveId)`, `Version` koşullu; olaylar
  `PickTasksIssued` (outbound) + görev başına `Created` — **tek 0178 işleminde, hepsi ya da hiçbiri (sipariş düzeyinde)**. Sonuç:
  `Issued { TaskIds, TaskNumbers }` \| `Ineligible { Code: OutboundCancelled | NotDeferred | IssuedToOtherWave | PlanMismatch | NotAllocated }`.
  Teknik hata / zaman aşımı istisna olarak yükselir (sonuç bilinmiyor).
- `WithdrawAsync(WaveWithdrawRequest)`: `{ WaveId, OutboundId, ActorUserId, Reason }` — 0178 kendi işleminde: outbound `PickIssuance =
  Issued(WaveId)` olmalı; outbound'un bu dalgaya ait **bütün** görevleri `Open`, onaysız (`Posted` / `Pending` onay yok, uçuş kilidi yok)
  ise görevler `Cancelled` (`CancelReason` = gerekçe, olay `Data.waveRecalled = true`), outbound `PickIssuance.State = Deferred`, `WaveId = null`,
  olay `PickTasksWithdrawn` → `Withdrawn`; biri başlamışsa yazım yok → `Started`; zaten bu dalgadan çekilmişse → `Withdrawn` (replay).
  `Refused` onay "başlamış" sayılmaz (stok hareketi yok).
- Merkezi denetim: port 0178 deposunun işleminde çalışır ve çağıran **0180 komutunun** merkezi kaydına enlist olur (MOD-0181 §8.11 posting
  emsali — ayrı merkezi kayıt doğmaz).

**0180 serbest bırakma algoritması (`ReleaseWaveCommand`, izin `warehouse.waves.release`, If-Match):**
1. **Ön kontroller (yazımsız):** istek anahtarı; `Draft`; ≥ 1 `Planned` üyelik (aksi 422 `WAVE_EMPTY`); depo ayarı (§8.2 kural 1); her
   üyeliğin outbound'u hâlâ uygun (§8.2 kural 2) — değilse **422 `WAVE_ORDERS_INELIGIBLE`** (`details.orders[] { outboundId, code }`), yazım yok,
   kullanıcı çıkarır; teknik üst sınır (§8.5); sınır onayı (§8.5).
2. **Rota** (§8.7, yazımsız): her üyeliğin `PlannedBins`'inden plan öğeleri → `Sequence` 1..n.
3. **İşlem R1 (0180):** dalga `Releasing`, `ReleaseLeaseUntilUtc = şimdi + Warehouse:Wave:ReleaseLeaseSeconds` (öneri 120), `PickPlan[]`,
   `RouteSummary`, `ReleaseRequest`, olay `ReleaseStarted`, merkezi enlist.
4. **Görev üretimi:** siparişler en küçük `Sequence`'larına göre sırayla `IssueAsync`. Her `Issued` sonucu üyeliğe **aynı turda** yazılır
   (işlem R2-i: `Issued`, görev kimlikleri; olay yok — toplu olay R3'te).
   **Kısmi hata (iş):** `Ineligible` (ör. outbound ön kontrol ile istek arasında 0178'de iptal edildi) → üyelik `Excluded` (`Active = false`)
   + istisna `OrderNotIssuable` (iptal ise `OrderCancelledInWave`); diğer siparişler sürer. **Teknik hata:** döngü durur, dalga `Releasing`
   kalır, cevap 503 `WAVE_RELEASE_OUTCOME_UNKNOWN`; tamamlama bakım servisinin `CompleteWaveReleaseCommand`'ıdır (kilit dolunca, aynı
   `PickPlan`'dan, aynı isteklerle — port idempotent olduğu için açılmış siparişler replay olur, ikinci görev doğmaz).
5. **İşlem R3:** en az bir `Issued` varsa dalga `Released`, `ReleasedBy` / `ReleasedAt`, olay `Released` (açılan görev ve hariç kalan sipariş
   sayıları), `ReleaseLeaseUntilUtc = null`; **hiç** `Issued` yoksa dalga `Draft`'a döner, `Excluded` üyelikler kapalı kalır, olay
   `ReleaseAborted`, cevap 422 `WAVE_NOTHING_ISSUED`.
6. **Tekrar:** aynı `Idempotency-Key` + aynı gövde: `Releasing` → 409 `WAVE_RELEASE_IN_PROGRESS`; `Released` ve sonrası → 200 replay; farklı
   gövde → 400 `IDEMPOTENCY_KEY_REUSED`. `PickPlan` yeniden **hesaplanmaz** (kurtarmada aynı sıra).
7. Geri alma: serbest bırakmanın tersi yoktur; açık görevler §8.9 geri çekme ile kapatılır.

### 8.7 Rota optimizasyonu
Karar: D-2.
**Yöntem — S-biçimli (serpantin) koridor geçişi, belirlenimci, v1 (`Algorithm = "S-SHAPE-v1"`):** WMS'lerde yaygın sezgisel yöntem (çıkarım:
en kısa yolun kesin çözümü — gezgin satıcı — dalga boyunda pratik değildir; S-biçimi her koridoru bir kez gezer). Bu, "raf yerleşimine göre
en kısa yol" kararının (D-2) v1 yaklaşımıdır; kesin optimum iddia edilmez. **Sahip teyidi: karar 03 Y-3** — v1'de S-biçimli belirlenimci
yaklaşık yöntem kabul; kabul ölçüsü AC-25'in 39 / 66 karşılaştırmasıdır.

**Girdiler (bölge → koridor → raf):**
- Her plan öğesinin kaynak bin'i (`LocationId`) için LOCATION yolu (`getLocationPath`): **bölge anahtarı** `ZoneKey` = yoldaki `Zone` kodu; `Zone`
  yoksa (Bin → StorageLocation, K-01) `StorageLocation` kodu.
- Bin kodu LOCATION v1.1 toplu kod biçiminde ayrıştırılır: `^(?<aisle>[A-Za-z]|[0-9]{1,3})-(?<rack>[0-9]{2})-(?<level>[0-9]{2})-(?<pos>[0-9]{2})$`
  (`docs/analysis/contracts/location.openapi.yaml:148`, `:284-287`). Koridor: harf → harf sırası, sayı → tam sayı; bir bölgede harfli ve sayılı
  koridor birlikteyse harfliler önce.
- LOCATION okumaları istek başına bin başına bir kez (önbellek istek ömründe); LOCATION erişilemezse 503 `WAVE_INVENTORY_UNAVAILABLE`, yazım yok.

**Sıralama:**
1. Bölgeler `ZoneKey` sıralı (ordinal, artan). (Bölge sırası ayarı FU-0180-ZONEORDER, Q-9.)
2. Bölge içinde koridorlar artan; bölgede gezilen **k.** koridor (0'dan) çiftse raflar **artan**, tekse **azalan** (serpantin).
3. Raf içinde kat artan, sonra pozisyon artan.
4. **Eşitlik bozma (toplam sıra):** bin kodu (ordinal), sonra `OutboundId` (ordinal `D`), sonra `AllocSeq`.
5. **Geri düşüş:** kodu biçime uymayan bin'ler aynı bölgenin ayrıştırılmış bin'lerinden **sonra**, bin kodu sırasıyla; `RouteFallback = true`,
   `RouteSummary.FallbackCount` artar; ekran "n görev için raf sırası bilinmiyor — kod sırası kullanıldı" uyarısı gösterir.

**Çıktı:** `Sequence` 1..n dalga genelinde tek rota (toplayıcıya bölme v1'de yok — FU-0180-PICKER-SPLIT); `WaveRef.Sequence` 0178 görevine
yazılır (X-2); ayrıntı ekranı "Önerilen yol" sırasını ve haritasını gösterir (mockup v2).

**Ölçü (AC için):** `ProxyDistance` = ardışık iki öğe arasında Manhattan uzaklığı toplamı, koordinat `(bölge sırası × 1000 + koridor sırası,
raf)`; aynı koridorda |Δraf|, koridor değişiminde |Δkoridor| + |Δraf|. Geri düşüş öğeleri ölçüye girmez.

**Sınırlar:** dalga başına ≤ 2.000 plan öğesi (§8.5); kat / pozisyon yönü, tek yönlü koridor, asansör, sıcaklık bölgesi geçiş maliyeti v1'de
yok (FU-0180-ROUTE-2).

**Belirlenimcilik:** aynı plan öğeleri ve aynı LOCATION verisi → girdi sırasından bağımsız **aynı** `Sequence` (AC-24).

**Performans bütçesi:** (a) sıralama (LOCATION okumaları hariç) 2.000 öğede **≤ 200 ms p95** (in-process, 20 koşu); (b) serbest bırakma
uçtan uca 200 sipariş / 2.000 görev **≤ 15 s** (entegrasyon ortamı, gerçek Mongo replika seti); (c) öneri sorgusu 5.000 uygun outbound'lu
depoda **≤ 1 s**. CI gürültüsüne karşı test eşiği bütçenin 3 katıdır; bütçe aşımı kabul kaydına yazılır (AC-27).

### 8.8 İlerleme ve durum uzlaştırması
Karar: D-5.
- **Okuma, yazma yok:** ilerleme okuma anında `wms_tasks` (dalga görevleri, `WaveRef.WaveId`), `wms_task_events` ve `wms_outbound_shipments`
  üzerinde **yalnız `Find`** ile hesaplanır; 0180 bu koleksiyonlara yazmaz (§2.3 kural 2).
- **Tanımlar (sayılar, miktar toplanmaz — farklı SKU / birim toplanmaz, MOD-0178 §4 `:291`):**
  - Toplama % = dalganın terminal (`Completed` \| `Cancelled`) görevleri / dalganın açtığı görevler (geri çekilen siparişlerin görevleri hariç);
    tamamlanan / toplam görev sayısı listede ayrıca (BRIEF `:29`).
  - Paketleme % = outbound'u `Packed` \| `ReadyToShip` \| `Shipped` olan `Issued` siparişler / `Issued` siparişler.
  - Sevk % = outbound'u `Shipped` olan `Issued` siparişler / `Issued` siparişler; `ReadyToShip` ayrıca "sevke hazır" sayısı.
- **Durum uzlaştırması:** `WaveMaintenanceHostedService` her **60 sn** (Q-11 = A, karar 03; `Warehouse:Wave:ReconcileSeconds`) açık dalgalar
  (`Released` \| `Picking`) için `ReconcileWaveCommand` gönderir (kayıt başına `SetScope`). Komut yalnız **yazacak şey varsa** yazar: durum
  geçişi (§8.3), `OrderCancelled` üyelik (outbound 0178'de `Cancelled`), istisna aç / kapat (§8.10). `CompletedAt` = dalga görevlerinin terminal
  olaylarının (`Completed` / `ClosedShort` / `Cancelled`, `wms_task_events.AtUtc`) en büyüğü — uzlaştırma zamanı değil.
- Gecikme: ekran ilerlemesi anlıktır (okuma), dalga **durumu** en çok bir uzlaştırma turu (≤ 60 sn + işlem) gecikir.

### 8.9 Geri çekme ve iptal
Karar: D-9.
- **İptal** (`CancelWaveCommand`, `warehouse.waves.cancel`): yalnız `Draft`; gerekçe 10–500 zorunlu; üyelikler `Removed`; olay `Cancelled`.
- **Kalan görevleri geri çek** (`RecallWaveRemainingCommand`, `warehouse.waves.recall`, If-Match): `Released` \| `Picking`; gerekçe 10–500
  zorunlu. Her `Issued` sipariş için `WithdrawAsync`: `Withdrawn` → üyelik `Withdrawn`, `Active = false` (sipariş yeniden uygun olur — başka
  Taslak dalgaya eklenebilir, D-13); `Started` → sipariş dalgada kalır (sipariş bölünmez, D-8). Sonuç dalganın `Recalls[]`'ına ve olay
  `RemainingRecalled`'a yazılır. **Kısmi hata:** teknik hata alınan sipariş `Issued` kalır, cevap 207 benzeri satır sonuçlu 200 + `details.failed[]`
  (sözleşme taslağında kesinleşir); komut aynı anahtarla tekrar edilebilir (port idempotent).
- Geri çekmeden sonra kalan `Issued` siparişlerin görevleri terminal olunca uzlaştırma dalgayı `PartiallyCompleted` yapar (D-9, karar 02:
  "Kısmen tamamlandı" yalnız kalan görevler geri çekilince). **Genişleme — karar 03 Y-2:** serbest bırakmada `Excluded` olan ya da 0178'de
  iptal edilip `OrderCancelled` olan üyelik de dalgayı `PartiallyCompleted` yapar (§8.3). Kısa toplama bu kurala girmez (Q-10 = A).
- Hiç sipariş kalmazsa (hepsi çekildi) dalga bir sonraki turda `PartiallyCompleted` olur (tamamlanan 0 sipariş).

### 8.10 İstisnalar
Kararlar: D-9, D-10, D-11, D-13.
**Tespit:** `ReconcileWaveCommand` her turda açık dalgalar için (terminal dalgalarda tespit durur) koşulları ölçer; istisna `SubjectKey` ile
tekildir — aynı koşul ikinci kez kayıt açmaz.

| Tür | Koşul (0178 verisinden) | Kendiliğinden kapanır mı | İzinli eylemler |
|---|---|---|---|
| `ShortPick` | dalga görevi `ClosedShort` olayıyla terminal ya da `Completed` iken `ConfirmedQuantity < PlannedQuantity` | hayır | Yok say (gerekçe); "Görevi aç (MOD-0178)" bağlantısı |
| `StockMissing` | dalga görevinde `Refused` onay, kod `INSUFFICIENT_STOCK` | hayır | Siparişi çıkar · Sonraki dalgaya taşı · Yok say |
| `LotUnderRecall` | dalga görevinde `Refused` onay, kod `LOT_UNDER_RECALL` — **otomatik çıkarma yok**, vardiya sorumlusu karar verir (D-10) | hayır | Siparişi çıkar · Sonraki dalgaya taşı · Yok say |
| `CutoffMissed` | şimdi > `CutoffAtUtc` ve dalga terminal değil — **otomatik kapanış yok** (D-9) | hayır | Yok say (gerekçe) |
| `TaskWaitingLong` | dalga görevi `Open`, onaysız ve `IssuedAtUtc`'den beri ≥ `PendingTaskMinutes` (varsayılan 30, D-11) | **evet** — görev başlayınca ya da terminal olunca `Resolved` + `Action = SystemCleared` (olay `ExceptionCleared`) | Siparişi çıkar · Sonraki dalgaya taşı · Yok say |
| `OrderCancelledInWave` | `Issued` siparişin outbound'u 0178'de `Cancelled` (görevleri 0178 kapattı) ya da serbest bırakmada `OutboundCancelled` | hayır | Yok say (bilgi; üyelik zaten `OrderCancelled`) |
| `OrderNotIssuable` | serbest bırakmada `Ineligible` (iptal dışı kod) | hayır | Sonraki dalgaya taşı · Yok say |

**Eylemler** (`ResolveWaveExceptionCommand`, `warehouse.waves.exceptions.resolve`, If-Match; gerekçe 10–500 her eylemde zorunlu):
- **Siparişi dalgadan çıkar:** `Released` \| `Picking` dalgada `WithdrawAsync` (§8.6) → `Withdrawn`; başlamışsa 409 `WAVE_ORDER_STARTED`.
- **Sonraki dalgaya taşı (D-13):** önce çıkar (aynı kural), sonra hedef dalgaya `AddWaveOrders` kuralıyla ekle — **tek komut**; hedef varsayılanı
  aynı depoda `Draft` ve `CutoffAtUtc` en yakın gelecekteki dalga; kullanıcı başka `Draft` dalga seçebilir; hedef yoksa 422 `WAVE_TARGET_INVALID`
  (kullanıcı önce dalga açar). Çıkarma başarılı, ekleme başarısız olursa sipariş serbest kalır (uygun listede) ve cevap bunu söyler; istisna
  `Resolved` olmaz.
- **Yok say:** istisna `Ignored`; dalga ve sipariş değişmez.
- Stok düzeltme bu ekrandan **yapılmaz**; satırdan ilgili ekrana bağlantı (BRIEF `:63-64`).

### 8.11 Riskli dalga
Karar: D-6.
Dalga **riskli** ⇔ durum `Released` \| `Picking` **ve** `CutoffAtUtc − şimdi < RiskMinutesBeforeCutoff` (varsayılan 90 dk) **ve** toplama % <
`RiskProgressPercent` (varsayılan %70); eşikler depo ayarı (D-6). Kesim geçmiş ve terminal olmayan dalga da risklidir (ayrıca `CutoffMissed`).
Risk saklanmaz, okuma anında hesaplanır; pano KPI kartı ve "Riskli olanlar" süzgeci (BRIEF `:24-25`, `:31`). Risk ve durum yalnız renge dayanmaz
(ikon + metin).

### 8.12 İyimser kilit
Karar: D-14.
Her değişebilir kayıt (`Wave`, `WaveOrder`, `WaveException`, `WaveWarehouseSettings`) `Version` taşır; yazma uçları `If-Match: W/"<version>"`
ister; uyuşmazlıkta 409 `WAVE_CONFLICT`, sessiz üzerine yazma yok; ekran "Kayıt değişti, yenileyin." çakışma durumunu gösterir (mockup v2).
Uzlaştırma (sistem) yazımı da `Version` koşulludur; kullanıcı yazımıyla yarışta kaybeden yeniden okur (CAS deseni).

### 8.13 Merkezi denetim ve yerel iz
- BL-501 InventoryService'te kurulu (`tests/architecture/audit-ledger/Diten.InventoryService.md:12`, `RequireEnlistment` açık); her 0180 yazma
  komutu `ICentralAuditDeclaration` taşır; `SourceModule` = manifest kodu `WAVE-PLANNING` (0178 `PUTAWAY-PICKING-PACKING` emsali,
  `dc010b911:INV/Application/Features/Warehouse/WarehouseCentralAudit.cs:15`).
- Merkezi kayıt ad ve gövde taşımaz; değerlerin ve **gerekçelerin** tek yeri `wms_wave_events.Data`'dır (aynı işlem, silinmez). Başarılı ve
  yazan komut 1 kayıt; replay ve 0-kayıt 0; başarısız komut iletilmez.
- 0178 portunun yazımları çağıran 0180 komutunun kaydına enlist olur (§8.6).
- **Gerekçe zorunlu yazmalar (AUD-001 + D-9):** iptal, geri çekme, istisna eylemleri (çıkar / taşı / yok say), depo ayarı değişikliği; gerekçe
  olaya yazılır. Taslak düzenleme / ekleme / çıkarmada gerekçe isteğe bağlıdır.

### 8.14 Kayıt kodu üretimi
`Wave.Number` = `{ŞİRKET}-WAV-{000001}` (RCS-001 biçimi; D-4). `WAV` öneki RCS-001 §3 tablosunda yok (ölçüldü) ve servis kodunda yok
(`WarehouseRecordCodeRules.TypePrefixes` `dc010b911:…:28`). **Hedef üretici:** MOD-0040-FU02 ortak servisi (D-4). **Bugün:** FU02'nin Inventory
bağlantısı ve WMS türleri DL-8'dedir ve DL-8 yoktur (§7). **Ara üretici (Q-4 = A, karar 03):** MOD-0178 geçici sayacı (`IWarehouseRecordCodeAllocator`)
— `TypePrefixes`'e `WAV` (X-6), MOD-0181 `CNT` emsali (RCS-001 §3 `:71`); FU02 DL-8'de `WAV` kataloğa girer ve geçiş FU-0180-RCS ile yapılır.
0180 kendi sayacını **yazmaz** (RCS-001 §6). Şirket öneki MDM tüzel kişilik `Code`'u (`^[A-Z]{2,5}$`, aksi 422 `WAVE_COMPANY_CODE_INVALID`);
sayaç yazılamazsa 503 `WAVE_RECORD_CODE_UNAVAILABLE`, dalga yaratılmaz.

### 8.15 Ekranlar ve Blueprint soft pages
| Blueprint soft page | Ekran | Sayfa kodu | Veri |
|---|---|---|---|
| (pano) | `/Warehouse/Waves` — KPI kartları (bugün serbest bırakılan, açık, riskli, ort. tamamlanma süresi, açık istisna — BRIEF `:24-25`) + liste (DataTable, server) + oluştur / düzenle yan paneli (8 alan) + hızlı görünüm | `WAVES` (nav) | KPI'lar okuma anında hesaplanır |
| (ayrıntı) | `/Warehouse/Waves/Details/{waveId}` — üst: kod, durum çizgisi, kesim geri sayımı (depo + kullanıcı saati), üç ilerleme; sekmeler **Siparişler** (toplama / paketleme / sevk durumu), **Görevler** (0178 bağlantısı, toplayıcı, raf, sıra), **Yük** (bölge / koridor yoğunluk tablosu, toplayıcı başına görev), **Olay geçmişi**; "Önerilen yol" (§8.7) | `WAVEDETAIL` (nav değil) | 0178'den okuma |
| Wave Planner View | `/Warehouse/WavePlanner` — sol: uygun siparişler (süzgeçli; ağırlık / hacim "—"; Allocating gri); sağ: Taslak dalga kartları (doluluk, kesim, uyarılar); sürükle-bırak **ve** "Ekle" düğmesi / klavye (§11) | `WAVEPLANNER` (nav) | DataTable değil (pano; Q-19) |
| Exception Queue | `/Warehouse/WaveExceptions` — DataTable (server): dalga kodu, sipariş, satır, tür, oluşma, durum, çözen; eylem yan paneli (§8.10) | `WAVEEXCEPTIONS` (nav) | — |
| Performance KPIs | `/Warehouse/WaveKpis` — menüde **ayrı sayfa** (D-16); zaman serileri + CSV (§8.16) | `WAVEKPIS` (nav) | — |
| (depo dalga ayarı) | ekran **yok** (v1) — yalnız API (`GET /waves/settings/{warehouseId}` `read`; `POST …/update` `warehouse.waves.settings.configure`); manifest `ApiPermissions` | — | Q-6 = B (karar 03); Y-1 kapatma kuralı §4.5 |

Her ekranda durumlar: **yükleniyor, hata ("Stok servisine ulaşılamadı."), boş, çakışma ("Kayıt değişti, yenileyin."), yetkisiz (UAS-001)**
(BRIEF `:70-76`; mockup v2 durum örnekleri). Masaüstü öncelikli; **375 px**'te pano ve istisna kuyruğu yatay sayfa kaydırmasız okunur (BRIEF `:80`);
planlayıcı 375 px'te salt okunur liste görünümüne düşer (sürükle-bırak yok, "Ekle" düğmesi var). RTL (`ar`) her ekranda.

### 8.16 Performans sayfası ve CSV
Karar: D-16.
- Süzgeç: depo (zorunlu), tarih aralığı **en çok 92 gün** (aksi 400 `WAVE_KPI_RANGE_TOO_LARGE`), dalga türü (BRIEF `:68`).
- Seriler (deponun yerel günü):
  - **Günlük dalga sayısı** = o gün `ReleasedAt`'i olan dalgalar.
  - **Ortalama tamamlanma süresi** = `CompletedAt − ReleasedAt` ortalaması (`Completed` + `PartiallyCompleted`, tamamlandığı güne).
  - **Kesime yetişme oranı** = `CompletedAt ≤ CutoffAtUtc` olan / o gün kesimi olan tamamlanmış dalgalar.
  - **Toplayıcı başına satır / saat** = dalga görevlerinin `Posted` onay sayısı / toplayıcıların etkin saatleri (toplayıcı başına gün içinde ilk →
    son onay, en az 0,25 saat); toplayıcı = onayın `ConfirmedBy`'ı.
- **CSV** (`GET /waves/kpis/export`, `warehouse.waves.kpis.export`): aynı seriler aynı süzgeçle, satır başına gün; başlık satırı isteğin
  `Accept-Language`'ında (7 dil), sayılar değişmez kültürde (`.` ondalık); UTF-8. Dosya saklanmaz; okuma işlemidir (denetim §21 notu).

### 8.17 Hata kodları
Öneri (`WAVE_` öneki; kesin liste sözleşme taslağında):

| Kod | HTTP | Ne zaman |
|---|---|---|
| `WAVE_NOT_FOUND` / `WAVE_EXCEPTION_NOT_FOUND` | 404 | yok, başka kiracı / şirket |
| `WAVE_CONFLICT` | 409 | If-Match uyuşmaz (D-14) |
| `WAVE_INVALID_STATE` | 409 | durumda izinli olmayan komut |
| `WAVE_SETTINGS_REQUIRED` | 422 | depo dalga yönetimli değil ya da saat dilimi yok |
| `WAVE_SETTINGS_DEFERRED_ORDERS` | 409 | depoda `Deferred` / `Allocating` outbound varken `WaveManaged` kapatma (Y-1, §4.5) |
| `WAVE_CUTOFF_INVALID` | 400 | yaz saati boşluğu, geçmiş kesim |
| `WAVE_WAREHOUSE_IMMUTABLE` | 400 | düzenlemede depo değişimi |
| `WAVE_ORDER_NOT_ELIGIBLE` | 422 | eklenen outbound uygun değil (§8.2) |
| `WAVE_ORDER_IN_OTHER_WAVE` | 409 | outbound başka etkin dalgada (D-8) |
| `WAVE_TOO_LARGE` | 422 | teknik üst sınır (§8.5) |
| `WAVE_EMPTY` | 422 | siparişsiz serbest bırakma |
| `WAVE_ORDERS_INELIGIBLE` | 422 | serbest bırakma ön kontrolünde uygun olmayan sipariş |
| `WAVE_LIMIT_CONFIRMATION_REQUIRED` | 409 | sınır aşımı onaysız (D-7) |
| `WAVE_RELEASE_IN_PROGRESS` | 409 | `Releasing` dalgada aynı anahtar ya da yeni istek |
| `WAVE_RELEASE_OUTCOME_UNKNOWN` | 503 | 0178 portu sonuç vermedi; bakım tamamlar |
| `WAVE_NOTHING_ISSUED` | 422 | serbest bırakmada hiç görev açılamadı |
| `WAVE_ORDER_STARTED` | 409 | başlamış siparişi çıkarma / taşıma |
| `WAVE_TARGET_INVALID` | 422 | taşıma hedefi yok / Taslak değil / başka depo |
| `WAVE_REASON_REQUIRED` | 400 | gerekçesiz iptal / geri çekme / istisna eylemi / ayar değişikliği |
| `WAVE_KPI_RANGE_TOO_LARGE` | 400 | > 92 gün |
| `WAVE_COMPANY_CODE_INVALID` | 422 | §8.14 |
| `WAVE_RECORD_CODE_UNAVAILABLE` | 503 | sayaç / ortak servis yazılamadı |
| `WAVE_INVENTORY_UNAVAILABLE` | 503 | 0178 / LOCATION okuması erişilemez |
| `IDEMPOTENCY_KEY_REUSED` | 400 | aynı anahtar, farklı gövde |

### 8.18 AI Assist
Blueprint AI Tier `Assist` (Summarize / Extract / Recommend / Route), risk Medium. **Karar (Q-14 = A, karar 03):** v1 kapsamı dışında; hiçbir AI ucu ya da
çağrısı yok; FU-0180-AI. "Route" kullanım durumu §8.7'nin belirlenimci algoritmasıyla karşılanır, AI değildir.

## 9. Layout & Shell Contract
- `shell: tenant` → `Views/Warehouse/{Waves,WavePlanner,WaveExceptions,WaveKpis}/*.cshtml` dosyalarının **her birinde**
  `Layout = "_LayoutTenantShell";` **AÇIKÇA** yazılır (ayrıntı sayfası `Details.cshtml` dahil); `_ViewStart.cshtml` varsayılanı değişmez.
- View klasörü `Views/Warehouse/{Module}/` (MOD-0178 / MOD-0181 emsali); rota `/Warehouse/{Module}`.
- Yetkisiz kullanıcıya sayfa iskeleti çizilmez, yönlendirme yapılmaz; sol menü ve üst çubuk kalır, içerikte tek açıklama ("Yöneticinizden yetki
  isteyin") (UAS-001; BRIEF `:72-73`).

## 10. Backend File Convention
Golden Reference Slim birebir (`services/Diten.DevEnablementService/src/Diten.DevEnablementService.Application/Features/GoldenReferenceSlim/`;
pack `execution/domains/developer-enablement/module-packs/DEV-0000-golden-reference-slim.md`):

```text
Features/Waves/
├── Commands/        CreateWaveCommand.cs, UpdateWaveCommand.cs, AddWaveOrdersCommand.cs, RemoveWaveOrdersCommand.cs,
│                    CancelWaveCommand.cs, ReleaseWaveCommand.cs, CompleteWaveReleaseCommand.cs, RecallWaveRemainingCommand.cs,
│                    ReconcileWaveCommand.cs, UpdateWaveWarehouseSettingsCommand.cs
├── Queries/         GetWaveListQuery.cs, GetWaveByIdQuery.cs, GetWaveDashboardQuery.cs, GetWaveOrderListQuery.cs, GetWaveTaskListQuery.cs,
│                    GetWaveLoadQuery.cs, GetWaveEventListQuery.cs, GetWaveCandidateListQuery.cs, GetWaveSuggestionListQuery.cs,
│                    GetWaveWarehouseSettingsQuery.cs
├── Handlers/
│   ├── CommandHandlers/   CreateWaveHandler.cs … UpdateWaveWarehouseSettingsHandler.cs   (sealed class, suffix YOK)
│   └── QueryHandlers/     GetWaveListHandler.cs … GetWaveWarehouseSettingsHandler.cs
├── Validators/      CreateWaveValidator.cs, UpdateWaveValidator.cs, AddWaveOrdersValidator.cs, ReleaseWaveValidator.cs,
│                    CancelWaveValidator.cs, RecallWaveRemainingValidator.cs, UpdateWaveWarehouseSettingsValidator.cs
└── WaveModels.cs    (tek dosyada bütün DTO'lar)
Features/WaveExceptions/   aynı yapı: ResolveWaveExceptionCommand; GetWaveExceptionList/ById sorguları; WaveExceptionModels.cs
Features/WaveKpis/         aynı yapı: GetWaveKpiSeriesQuery, ExportWaveKpiCsvQuery; WaveKpiModels.cs
Features/WavePlanning/     ortak: WaveContract.cs, WaveErrorCodes.cs, WaveCentralAudit.cs, Ports/, Services/
                           (WaveRoutePlanner, WaveReleaseEngine, WaveReconciler, WaveExceptionDetector, WaveKpiCalculator)
```

**Golden Slim sapması G-0180-01 (adıyla):** DEV-0000 `Delete{Module}Command` / `BulkDelete{Module}Command` ve PUT / DELETE uçları tanımlar
(`execution/domains/developer-enablement/module-packs/DEV-0000-golden-reference-slim.md`). 0180'de **silme yoktur** (§2.3 kural 6): `Delete` /
`BulkDelete` komutu, `delete` / `bulk-delete` izni ve DataTable "Sil" / toplu sil eylemi yoktur; Taslak dalganın tek "kaldırma" yolu
`CancelWaveCommand`'dır (durum, silme değil). Güncelleme `POST …/update` komut ucudur (InventoryService PUT bekçisi — §8.1). DEV-0000'ın
`data_mode: client`'ı 0180'de `server`'dır (dalga listesi sınırsız büyür). Klasör, adlandırma ve partial seti Golden Slim ile aynı kalır.
DataTable satır eylemi "Sil" yerine "İptal"dir (yalnız Taslak); `verify_datatable_page.py` sapma verirse dokunma protokolüyle sahibine sorulur.

Kurallar: Command / Query `sealed record`; Handler / Validator adında `Command` / `Query` soneki yok; bir dosyada tek `public` tip;
`Handlers/CommandHandlers` ve `Handlers/QueryHandlers` ayrımı zorunlu (`.antigravity/rules/module-pack-standard.md` §4).

## 11. Frontend File Contract
Golden Slim seti (`frontend/Diten.Web/Views/DevEnablement/GoldenReferenceSlim/`):

| Klasör | Dosyalar |
|---|---|
| `Views/Warehouse/Waves/` | `Index.cshtml` (KPI kartları + liste), `_Filter.cshtml` (depo zorunlu, durum, tarih, kesim aralığı, tür, "riskli olanlar"), `_DataTable.cshtml` (`data-dt-standard="v2"`, `data-dt-data-mode="server"`, skeleton), `_IndexL10n.cshtml`, `_CreateEditOffcanvas.cshtml` (8 alanlı form + "Siparişleri öner"), `_DetailsQuickView.cshtml`, `WavesIndex.cs`; ek sayfa `Details.cshtml` (liste ekranı değil) |
| `Views/Warehouse/WavePlanner/` | `Index.cshtml` (pano; DataTable değil), `_IndexL10n.cshtml`, `WavePlannerIndex.cs` |
| `Views/Warehouse/WaveExceptions/` | `Index.cshtml`, `_Filter.cshtml`, `_DataTable.cshtml`, `_IndexL10n.cshtml`, `_CreateEditOffcanvas.cshtml` (**yalnız eylem formu**: çıkar / taşı + hedef / yok say + gerekçe; yaratma yok), `_DetailsQuickView.cshtml`, `WaveExceptionsIndex.cs` |
| `Views/Warehouse/WaveKpis/` | `Index.cshtml` (grafikler + tablo görünümü + CSV düğmesi), `_Filter.cshtml`, `_IndexL10n.cshtml`, `WaveKpisIndex.cs` |
| `wwwroot/assets/js/Warehouse/{Waves,WavePlanner,WaveExceptions,WaveKpis}/` | `index.js`, `index.l10n.js`; `Waves/details.js`; `WavePlanner/planner.js` (sürükle-bırak + klavye) |
| `Resources/Views/Warehouse/{Module}/` | `{Module}Index.{en,tr,fr,es,zh,ar,ru}.resx` (+ `WavesDetails`) |
| `frontend/Diten.Web/Controllers/` | `WavesController.cs` (`[Route("Warehouse/Waves")]`, `Details/{waveId}` eylemi), `WavePlannerController.cs`, `WaveExceptionsController.cs`, `WaveKpisController.cs` — same-origin MVC proxy → Gateway `/api/warehouse/waves…`; her yazma `[ValidateAntiForgeryToken]`; her proxy isteğine sayfanın `Accept-Language`'ı (CSV başlığı ve hata metinleri); `If-Match` ve `Idempotency-Key` iletilir |
| `frontend/Diten.Web/Resources/` (ortak) | `SharedResource.{7}.resx` nav anahtarları, `PageHelpResource.{7}.resx` sayfa yardımı (§25) — yalnız eklemeli |

**Planlayıcı erişilebilirliği (BRIEF `:55`):** sol listede her satırın "Ekle" düğmesi ve hedef dalga seçimi vardır; klavyeyle: satıra Tab, `Enter`
= seçili dalgaya ekle, `Shift+Enter` = hedef seçici; dalga kartında siparişe Tab + `Delete` tuşu = çıkar (Taslak); sürükle-bırak yalnız fare / dokunma
kolaylığıdır, her işlem düğmeyle de yapılır; işlem sonucu `aria-live` bölgesinde okunur. Kısayol katmanı UXH-001'e uyar (yan panelde Enter).

Compact'a özgü `Create/Edit/Details.cshtml` (liste ekranı formu olarak) ve `_Form.cshtml` **yok** (`Waves/Details.cshtml` bir ayrıntı sayfasıdır,
form değildir — MOD-0181 `CountSheet.cshtml` emsali). Liste ekranına dokunan görev `verify_datatable_page.py --area Warehouse --module {Module}
--format gaps` koşar ve sapmayı sahibine sorar.

## 12. Validation Rules

| Field | Required | Format/Rule | DB-level | Pre-check |
|---|---|---|---|---|
| WarehouseId | Evet | uuid; LOCATION `Warehouse`, `Active`, şirket kapsamı; ayar `WaveManaged = true` + `TimeZoneId` | — | LOCATION + ayar okuması |
| WaveType | Evet | `Cargo` \| `Customer` \| `Region` \| `Manual` | — | — |
| CutoffLocal | Evet | `yyyy-MM-ddTHH:mm`; depo saat diliminde var olan an; ≥ şimdi − 1 saat (§8.4) | — | saat dilimi dönüşümü |
| ShipToFilter | Hayır | trim, 1..100 | — | — |
| MaxOrders | Hayır | tam sayı 1..1000 | — | — |
| MaxLines | Hayır | tam sayı 1..20000 | — | — |
| ResponsibleUserId | Hayır | uuid; kullanıcı dizininde, şirkette etkin | — | dizin okuması |
| Note | Hayır | trim, ≤ 500 | — | — |
| outboundIds (ekle / çıkar) | Evet | 1..200, uuid, tekrarsız; her biri §8.2 uygun | tekil kısmi `(TenantId, LegalEntityId, OutboundId)` `Active` | 0178 outbound `Find` |
| confirmLimitOverride | Hayır (varsayılan false) | bool; sınır aşımında `true` zorunlu | — | §8.5 |
| Gerekçe (iptal / geri çekme / istisna eylemi / ayar) | Evet | trim, 10..500 | — | — |
| Taşıma hedefi | Hayır | uuid; aynı depo, `Draft`; yoksa varsayılan (D-13) | — | dalga okuması |
| TimeZoneId (ayar) | `WaveManaged = true` ise evet | IANA kimliği, çalışma zamanının saat dilimi veritabanında çözülür | — | — |
| RiskMinutesBeforeCutoff / RiskProgressPercent / PendingTaskMinutes (ayar) | Hayır | 1..1440 / 1..100 / 1..1440 | tekil depo kaydı | — |
| WaveManaged `true` → `false` (ayar) | — | depoda `Deferred` / `Allocating` outbound yok (Y-1); aksi 409 `WAVE_SETTINGS_DEFERRED_ORDERS` | — | 0178 outbound `Find` sayımı (§4.5) |
| KPI aralığı | Evet | `from ≤ to`, ≤ 92 gün, depo zorunlu | — | — |
| Idempotency-Key | Evet (her yazma) | 8–100 karakter (MOD-0178 emsali) | tekil kısmi index | anahtar araması |
| If-Match | Evet (yaratma dışı her yazma) | `W/"<version>"` | — | — |
| TenantId / LegalEntityId gövdede | Hayır | gelirse 400 | — | — |

## 13. Failure Path to Verify
- **Duplicate (aynı Idempotency-Key, aynı gövde)** — Expected: 200 replay, yeni kayıt / olay / numara / görev yok.
- **Aynı anahtar, farklı gövde** — Expected: 400 `IDEMPOTENCY_KEY_REUSED`, yazım yok.
- **Missing alan (depo, tür, kesim)** — Expected: 400 validator mesajı; saat dilimi yoksa 422 `WAVE_SETTINGS_REQUIRED`.
- **Unauthorized actor** — Expected: 403; UI eylem gizli; sayfa izni yoksa iskelet yok (UAS-001).
- **Concurrency (eski If-Match)** — Expected: 409 `WAVE_CONFLICT`; "Kayıt değişti, yenileyin."; sessiz üzerine yazma yok (D-14).
- **Başka kiracı / şirket kaydı** — Expected: 404 (403 değil).
- **Sipariş başka dalgada / eşzamanlı iki ekleme** — Expected: 409 `WAVE_ORDER_IN_OTHER_WAVE`; tam biri başarılı.
- **Sınır aşımı serbest bırakma onaysız** — Expected: 409 `WAVE_LIMIT_CONFIRMATION_REQUIRED`; onayla 200 (D-7).
- **Ön kontrolden sonra outbound 0178'de iptal** — Expected: dalga diğer siparişlerle `Released`; iptal edilen `Excluded` + `OrderCancelledInWave`.
- **0178 portu zaman aşımı** — Expected: 503 `WAVE_RELEASE_OUTCOME_UNKNOWN`, dalga `Releasing`; bakım tamamlar; görev sayısı plan sayısına eşit (ikinci görev yok).
- **Süreç R1'den sonra ölür** — Expected: kilit dolunca bakım `CompleteWaveReleaseCommand` aynı `PickPlan`'la tamamlar.
- **Başlamış siparişi geri çekme / çıkarma** — Expected: sipariş dalgada kalır (`Started`); istisna eyleminde 409 `WAVE_ORDER_STARTED`.
- **Serbest bırakılmış dalgayı iptal** — Expected: 409 `WAVE_INVALID_STATE` (D-9).
- **Kesim geçti** — Expected: dalga kapanmaz; `CutoffMissed` istisnası tek kez (D-9).
- **Lot geri çağırmada** — Expected: `LotUnderRecall` istisnası; sipariş dalgadan **otomatik çıkmaz** (D-10).
- **0178 / LOCATION erişilemez** — Expected: 503 `WAVE_INVENTORY_UNAVAILABLE`; yazım yok.
- **Dalga yönetimi kapalı depo** — Expected: 0178 outbound'u bugünkü gibi görevleriyle doğar; planlayıcıda görünmez.
- **Ertelenmiş sipariş varken dalga yönetimini kapatma (Y-1)** — Expected: 409 `WAVE_SETTINGS_DEFERRED_ORDERS`, ayar değişmez, merkezi kayıt 0;
  siparişler dalgaya alınıp serbest bırakıldıktan ya da 0178'de iptal edildikten sonra aynı istek 200.
- **Ayar izni olmayan planlayıcı** — Expected: `warehouse.waves.update` taşıyan ama `warehouse.waves.settings.configure` taşımayan kullanıcıya ayar
  yazımı 403 (Q-6 = B).
- **Fazladan PUT / DELETE ucu** — Expected: `LedgerImmutabilityArchitectureTests` kırmızı.

## 14. Authorization Convention

**Namespace (D-4, sahip kararı Beste 2026-11-12):** `warehouse.waves.*` — PKS-001 `warehouse.*` ad alanı altında; mockup'taki `wave.*` adları
buna çevrilir. **Not:** PKS-001 §4 bugün `warehouse.*`'ı bütünüyle MOD-0178'e yazar ve "one namespace per WMS module" (Q-08) der
(`.antigravity/rules/permission-key-standard.md:105`); D-4 bunun MOD-0180 için istisnasıdır → §4'e alt ad alanı satırı (Q-7 = A, karar 03) **paket onayından önce** yazılır (§6, Y-4).
Manifest yalnız `warehouse.waves.*` bildirir; 0178'in `warehouse.*` anahtarları bildirilmez. Depoda `warehouse.waves.` anahtarı yok
(`git grep`, services + frontend: 0 eşleşme — çıkarım: yeni).

**Son ek düzeltmesi (Q-5):** iş paketinin listesindeki `edit` PKS-001 §5'te eski yazımdır, kanonik karşılığı `update`'tir
(`.antigravity/rules/permission-key-standard.md:115-127`) → paket **`warehouse.waves.update`** yazar. `release`, `recall`, `resolve` Tier-3 (bu
pakette kayıtlı), `cancel` Tier-3 (sözlükte örnek), `export` Tier-2, `settings.configure` Tier-3 (Q-6 = B, karar 03 — fiil `configure`
PKS-001 §5'te ve Rol İzinleri köprüsünde var, X-15).

**Ayar izni ayrı (Q-6 = B, karar 03; inceleme F-15):** `WaveManaged` anahtarı **bütün depo** için MOD-0178'in görev açılışını değiştirir
(§8.6); bu yüzden Tier-1 `update`'e bağlanmaz, ayrı Tier-3 anahtar `warehouse.waves.settings.configure` ister. Planlayıcının `update`'i
depo davranışını değiştiremez (AC-15). Ayar okuması `read`'dedir.

| Anahtar | Tier | Dilim | Anlam |
|---|---|---|---|
| `warehouse.waves.read` | 1 | DL-1 | pano, liste, ayrıntı, siparişler, görevler, yük, olaylar, planlayıcı listesi, depo ayarı okuması |
| `warehouse.waves.create` | 1 | DL-1 | dalga oluştur |
| `warehouse.waves.update` | 1 | DL-1 | Taslak düzenle, sipariş ekle / çıkar, öneri (depo dalga ayarı **değil** — Q-6 = B) |
| `warehouse.waves.settings.configure` | 3 (bu pakette kayıtlı) | DL-1 | depo dalga ayarı yazma: `WaveManaged`, `TimeZoneId`, eşikler; gerekçe zorunlu; Y-1 kapatma kuralı (§4.5) |
| `warehouse.waves.cancel` | 3 | DL-1 | Taslak iptal (gerekçe) |
| `warehouse.waves.release` | 3 (bu pakette kayıtlı) | DL-2 | serbest bırak (rota + görev üretimi) |
| `warehouse.waves.recall` | 3 (bu pakette kayıtlı) | DL-3 | kalan görevleri geri çek (gerekçe) |
| `warehouse.waves.exceptions.read` | 1 | DL-3 | istisna kuyruğu |
| `warehouse.waves.exceptions.resolve` | 3 (bu pakette kayıtlı) | DL-3 | çıkar / taşı / yok say |
| `warehouse.waves.kpis.read` | 1 | DL-5 | performans sayfası |
| `warehouse.waves.kpis.export` | 2 | DL-5 | CSV |

**Politika ve aktör:** `[Authorize]` (tenant shell) + her eylemde `[HasPermission(WavePermissions.X)]`; `AuthorizationGuardTests` yeni
controller'ları da ölçer. Aktör `tenant_user`; sistem komutları (`CompleteWaveRelease`, `Reconcile`) bakım servisinin kapsamıyla (`SetScope`)
çalışır, HTTP ucu yoktur. Rol şablonu önerisi (yalnız yeni kiracılar; X-13, Ali (Auth)): **Dalga planlayıcı** = `read` + `create` + `update` +
`cancel` + `release`; **Vardiya sorumlusu** = `read` + `recall` + `exceptions.*`; **Performans izleyici** = `read` + `kpis.*` (rol adları öneri).
`settings.configure` **hiçbir şablon role girmez** — depo yöneticisine elle verilir (öneri; bütün depoyu etkiler, F-15). Rol şablonu kararı
Ali'nindir (Q-20, karar 03).
`read` anahtarlarında kişisel veri: kullanıcı kimliği ve adı (toplayıcı) — KVKK açısından görev ataması 0178'de zaten görünürdür (çıkarım).

**Manifest:** ModuleCode `WAVE-PLANNING`, SortOrder 180 (0178 `PUTAWAY-PICKING-PACKING` / 178 emsali,
`dc010b911:INV/Api/Warehouse/PutawayPickingPackingManifestProvider.cs:29`, `:40`), Pages `WAVES` (nav, `read`), `WAVEDETAIL` (nav değil, `read`),
`WAVEPLANNER` (nav, `read`; eylemler `update`), `WAVEEXCEPTIONS` (nav, `exceptions.read`), `WAVEKPIS` (nav, `kpis.read`; DL-5); `ApiPermissions` =
`warehouse.waves.settings.configure` (DL-1; ekranı olmayan ayar ucu — Q-6 = B; FU-0180-SETTINGS-UI gelince sayfa eylemine taşınır); nav anahtarları `Nav.Module.WAVEPLANNING`, `Nav.Page.WAVES`,
`Nav.Page.WAVEPLANNER`, `Nav.Page.WAVEEXCEPTIONS`, `Nav.Page.WAVEKPIS`.

## 15. Gateway / API Routing Decision
- **Karar:** uçlar `/api/warehouse/waves…` — mevcut `/api/warehouse/{everything}` → 5067 rotasının altında
  (`dc010b911:gateway/Diten.ApiGateway/ocelot.json:1153-1161`; taban `/api/warehouse` `:1134-1142`). **`ocelot.json` değişmez (doğrulandı:
  rota ailesi zaten kapsar).** Scope middleware `/api/warehouse` tabanını kapsar (MOD-0181 §15 ölçümü) — değişmez.
- Gerekli: integration-agent'ın 0180 uç kapsama testi — ayrı dosya `gateway/Diten.ApiGateway.Tests/Mod0180WaveRouteTests.cs` (emsal
  `Mod0178WarehouseRouteTests.cs`, ona dokunulmaz) ve `/api/warehouse` rota ailesinin sahibi MOD-0178'in onayı (XMC-001; aile tek-yazıcı
  seam'dir — `domain-config.md:56`).
- **Testin ölçtüğü şey üretimdir (ölçüldü, F-5):** Gateway testleri üretim `gateway/Diten.ApiGateway/ocelot.json`'u okur — test projesi dosyayı
  bağlı kopya olarak çıktı dizinine alır (`gateway/Diten.ApiGateway.Tests/Diten.ApiGateway.Tests.csproj:26`) ve 0178 testi onu
  `AppContext.BaseDirectory`'den okur (`Mod0178WarehouseRouteTests.cs:23`); uç listesini de üretim controller kaynağından tarar (`:219-236`).
  0180 testi aynı yolu izler: üretim `ocelot.json` + `INV/Api/Controllers/WavePlanning/` kaynağı + WMS-WAVE-BUNDLE taslağının yolları; uç listesi
  testte elle yazılmaz. 0178 testi bütün `api/warehouse` controller'larını özyineli taradığı için 0180 uçları onun ölçümüne de girer ve o
  test **değişmeden** yeşil kalır (X-14, AC-55).
- İletilen başlıklar: `Authorization`, `X-Legal-Entity-Id`, `X-Correlation-Id`, `Idempotency-Key`, `If-Match`, `Accept-Language`. Dev dilimi
  `ocelot.json`'a yazmaz.

## 16. Acceptance Criteria

Her guard maddesi **üretim koduna karşı** ölçülür ve sabotaj kanıtı ister: ilgili üretim satırı geçici bozulur, test kırmızıya döner, bozma geri
alınır (kendi kopyasıyla ölçen test guard sayılmaz). Sabotaj kimliği `S-n` biçimindedir; kabul kaydı her `S-n` için komut + kırmızı çıktı + geri
almayı yazar.

**Bekçi olmayan maddeler (F-13):** AC-40 (`verify_datatable_page.py`), AC-43 (durumlar), AC-44 (375 px), AC-46 (kesim gösterimi) ve
AC-47 (ağırlık / hacim, ikon + metin) **araç ya da tarayıcı kontrolüdür, bekçi değildir** — sabotaj istenmez; kanıtları araç çıktısı / ekran
ölçümüdür. AC-27 bir performans ölçümüdür, S-27 eşiğin çalıştığını gösterir. Diğer her maddenin adlı sabotajı vardır.

**DL-1 — sözleşme taslağı + dalga çekirdeği (arka uç)**

- [ ] AC-1 (zorunlu) — `docs/analysis/contracts/drafts/wms-wave-bundle-v1-proposal.md` yazıldı: §3'teki her uç (operationId, istek / cevap şeması,
  hata kodları §8.17, `If-Match` / `Idempotency-Key`), §8.6 port sözleşmesinin 0178'e bakan yüzü (bilgi), sürüm `1.0.0-draft`; sözleşme testi
  (`WmsDraftContract.cs` emsali) controller cevaplarını taslağa karşı doğrular. S-1: bir cevaptan zorunlu alanı kaldır → sözleşme testi kırmızı.
- [ ] AC-2 (zorunlu) — `POST /waves` geçerli 8 alanla 201; `Status = Draft`; `Number` `{ŞİRKET}-WAV-` + 6 hane, kiracı genelinde tekil;
  `CutoffAtUtc` ve `TimeZoneId` sunucudan; `wms_wave_events`'te 1 `Created` olayı aynı işlemde. S-2: olay yazımını işlemden çıkar → kırmızı.
- [ ] AC-3 (zorunlu) — Kiracı / şirket / depo izolasyonu: başka kiracıdan her uç (GET, update, orders, release, cancel, recall, exceptions,
  resolve, kpis, settings) 404; aynı kiracıda başka şirketten 404; listeler yalnız kendi şirketi + istenen depo. S-3a: depodan `TenantId`
  yüklemini düşür → kırmızı; S-3b: `LegalEntityId` yüklemini düşür → kırmızı.
- [ ] AC-4 (zorunlu) — Gövde ya da sorguda `tenantId` / `legalEntityId` → 400. S-4: denetimi kaldır → kırmızı.
- [ ] AC-5 (zorunlu) — Her eylem `[HasPermission(WavePermissions.*)]`; manifest yalnız uygulanan `warehouse.waves.*` anahtarlarını bildirir;
  controller'lar `Controllers.WavePlanning` namespace'inde; `WarehouseRegistrationTests` (`dc010b911:INVT/Warehouse/WarehouseRegistrationTests.cs:49-56`)
  **değişmeden** yeşil. S-5a: bir eylemden özniteliği kaldır → `AuthorizationGuardTests` kırmızı; S-5b: `WavesController`'ı
  `Controllers.Warehouse` namespace'ine taşı → `WarehouseRegistrationTests` kırmızı (0178 manifestinde olmayan anahtar).
- [ ] AC-6 (zorunlu) — İdempotent yazma: her yazma komutu için aynı anahtar + aynı gövde → 200 replay (olay / `Version` değişmez); farklı gövde → 400
  `IDEMPOTENCY_KEY_REUSED`. Ayrı testler: yarat, düzenle, ekle, çıkar, iptal, serbest bırak, geri çek, istisna eylemi, ayar. S-6a: parmak izi
  karşılaştırmasını kaldır → kırmızı; S-6b: anahtar aramasını kaldır → ikinci olay, kırmızı.
- [ ] AC-7 (zorunlu) — İyimser kilit (D-14): eski `If-Match` ile her yazma → 409 `WAVE_CONFLICT`; eşzamanlı iki düzenlemeden tam biri. S-7: depoda
  `Version` koşullu filtreyi kaldır → kırmızı.
- [ ] AC-8 (zorunlu) — Silme yok: DELETE / PUT ucu yok, `LedgerImmutabilityArchitectureTests` **değişmeden** yeşil; `cancel` yalnız `Draft`'ta,
  gerekçe zorunlu (400 `WAVE_REASON_REQUIRED`), durum `Cancelled`, `IsDeleted = false`, GET 200; `Released`'da 409 `WAVE_INVALID_STATE`. S-8a:
  `DELETE /waves/{waveId}` ekle → bekçi kırmızı; S-8b: `cancel`'da durum denetimini kaldır → `Released` iptal olur, kırmızı.
- [ ] AC-9 (zorunlu) — Merkezi denetim: her 0180 yazma komutu `ICentralAuditDeclaration` taşır; `CentralAuditDeclarationTests` sayısı =
  **dilim başındaki ölçülen sayı + dilimin 0180 yazma komutları** (taban dilimin başlangıç commit'inde ölçülür ve kabul kaydına yazılır; r2
  ölçümü `dc010b911:INVT/CentralAudit/CentralAuditDeclarationTests.cs:53` = 117, bilgi — paket sabit taban yazmaz) ve sağlayıcı listesinde
  `WavePlanningManifestProvider` (`:60-69`, X-10); başarılı yazma → 1 kayıt, replay → 0, başarısız → 0. S-9: bir geri çağrıdan
  `CentralAuditTransaction.EnlistAsync`'i kaldır → kırmızı.
- [ ] AC-10 (zorunlu) — Kayıt kodu: `Number` değiştirilemez (gövdede gelirse 400); eşzamanlı iki yaratma farklı numara; sayaç yazılamazsa 503
  `WAVE_RECORD_CODE_UNAVAILABLE` ve dalga yok; 0180 kendi sayacını yazmaz (Q-4 = A: 0178 sayacı `WAV`). S-10: `TypePrefixes`'ten `WAV`'ı çıkar →
  yaratma 503, AC-2 kırmızı.
- [ ] AC-11 (zorunlu) — Uygunluk ve D-8: yalnız `Allocated` + `PickIssuance = Deferred` outbound eklenir (diğerleri 422 `WAVE_ORDER_NOT_ELIGIBLE`
  satır sonuçlu); bir outbound iki etkin dalgada olamaz (409 `WAVE_ORDER_IN_OTHER_WAVE`); **eşzamanlı** iki eklemede tam biri; sipariş bütünüyle
  eklenir (üyelik satır / tahsis düzeyinde değil). S-11: tekil kısmi index'i kaldır → eşzamanlı testte ikisi de başarılı, kırmızı.
- [ ] AC-12 (zorunlu) — Öner (D-1): `GET /waves/{waveId}/suggestions` yalnız uygun ve `ShipToFilter`'a uyan siparişleri, `CreatedAt` artan, kalan
  kapasite kadar döner; **yazmaz** (olay ve üyelik sayısı değişmez); periyodik kurma yok (hosted service dalga **yaratmaz** — mimari test:
  `WaveMaintenanceHostedService` yalnız `CompleteWaveReleaseCommand` / `ReconcileWaveCommand` gönderir). S-12: uygunluk süzgecinden `Active`
  üyelik koşulunu kaldır → başka dalgadaki sipariş önerilir, kırmızı; S-12b: `WaveMaintenanceHostedService`'e `CreateWaveCommand` gönderimi
  ekle → mimari test kırmızı.
- [ ] AC-13 (zorunlu) — Sınırlar (D-7): `MaxOrders` / `MaxLines` aşan ekleme **yazılır**, cevap `limitWarnings[]` taşır; teknik üst sınır (500 /
  2.000) aşan ekleme 422 `WAVE_TOO_LARGE`. S-13: sınır aşımında ekleme reddi koy → kırmızı (D-7 engellemez).
- [ ] AC-14 (zorunlu) — Saat dilimi (D-3): `Europe/Istanbul` deposunda `CutoffLocal = 2026-11-20T14:30` → `CutoffAtUtc = 11:30Z`; ABD deposunda yaz
  saati boşluğundaki yerel saat 400 `WAVE_CUTOFF_INVALID`; çakışan saat erken ofset + `cutoffAmbiguousResolved = true`; ayar yoksa 422
  `WAVE_SETTINGS_REQUIRED`. S-14: dönüşümde sunucu saat dilimini kullan → kırmızı.
- [ ] AC-15 (zorunlu) — Depo dalga ayarı: okuma `read`; yazma **`warehouse.waves.settings.configure`** (Q-6 = B, karar 03) + gerekçe;
  yalnız `warehouse.waves.update` taşıyan kullanıcıya yazma 403; aralık dışı eşik 400, kayıt değişmez; kayıt yokken varsayılanlar
  (false / — / 90 / 70 / 30) döner; başka şirketin ayarı 404. **Kapatma (Y-1, karar 03):** (a) depoda `Deferred` outbound varken
  `WaveManaged` `true` → `false` → 409 `WAVE_SETTINGS_DEFERRED_ORDERS`, ayar ve `Version` değişmez, merkezi kayıt 0; (b) `Allocating` outbound
  varken aynı; (c) sipariş dalgayla serbest bırakıldıktan (ya da 0178'de iptal edildikten) sonra aynı istek 200; (d) başka depodaki
  `Deferred` outbound kapatmayı engellemez; (e) `false` → `true` ve eşik değişikliği `Deferred` varken de 200. S-15: yüzde aralık denetimini
  kaldır → 150 kabul, kırmızı; S-15b: kapatma ön kontrolünden `Deferred` sayımını kaldır → (a) 200 döner, kırmızı; S-15c: ayar ucunun
  `[HasPermission]`'ını `WavePermissions.Update`'e çevir → 403 vakası kırmızı.
- [ ] AC-16 (zorunlu) — 0178 verisine yazılmaz (§2.3 kural 2): mimari test 0180 üretim kodunda (`WavePlanning` / `Waves` / `WaveExceptions` /
  `WaveKpis` klasörleri) `IOutboundShipmentRepository`, `IWarehouseTaskRepository` referansı ve `WarehouseCollections.*` üzerinde `Find` dışı
  koleksiyon çağrısı bulmaz; 0180 hiçbir `OnHand` / `Available` / `Reserved` özelliği taşımaz. S-16a: bir 0180 deposuna `wms_tasks` üzerinde
  `UpdateOneAsync` ekle → kırmızı; S-16b: handler'a `IWarehouseTaskRepository` enjekte et → kırmızı.
- [ ] AC-17 (zorunlu) — `SingleSystemOfRecordArchitectureTests` `wms_wave_*` koleksiyonlarını açıkça bildirir (X-9). S-17: bildirilmemiş
  `wms_wave_x` koleksiyonu aç → kırmızı.
- [ ] AC-56 (zorunlu; DL-1'de açılır, DL-3 ve DL-5 kabulünde tekrarlanır — X-15) — Rol İzinleri: manifestin bildirdiği her
  `warehouse.waves.*` anahtarının fiili ve satır adı 7 dilde söylenir; `frontend/Diten.Web/tests/role-assignments-label-ledger.test.js` yeşil,
  defter tavanı (`docs/roadmap/backlog/role-permission-label-ledger.md` "fiil 0 · satır 96") **değişmez** (0180 deftere satır eklemez); DL-3'te
  `recall` köprüde, 7 resx'te ve bir `ACTION_FAMILIES` ailesinde. S-56: `RoleAssignmentsIndex.ar.resx`'ten `ActionVerb_Recall`'ı sil (DL-3) ya da
  `SharedResource.ru.resx`'ten `Nav.Page.*` karşılığı olmayan bir 0180 `Perm.Entity.*` satır adını (beklenen `WAVESSETTINGS`, çıkarım) sil
  (DL-1) → vitest kırmızı.

**DL-2 — serbest bırakma + rota + MOD-0178 seam'i**

- [ ] AC-18 (zorunlu) — 0178 varsayılan davranışı değişmez: `WaveManaged = false` (ya da ayar yok) depoda outbound yaratma çıktısı (outbound
  belgesi, pick görevleri, olaylar) X-1…X-5 öncesiyle **alan alan aynı**. **Karşılaştırma referansı (F-14):** X-1…X-5 öncesi commit'te
  (DL-2 dilim başı) aynı fikstür betiğiyle üretilen çıktı — kimlik / zaman alanları normalize edilmiş JSON + sha256 — **kabul kaydına
  yazılır**; dilim sonu çıktısı aynı betikle üretilir ve sha256 eşitliği kayıtta gösterilir. Bu karşılaştırma **kanıttır, bekçi değildir**:
  0180 test ağacında saklanan bir kopya referans sayılmaz (kendi kopyasıyla ölçme). Bekçi, 0178'in mevcut testleridir: 0178 test dosyaları
  değişmez (`git diff --stat … -- INVT/Warehouse` boş) ve tam paket yeşil. `WaveManaged = true` depoda outbound `Allocated` + `PickIssuance.State = Deferred`,
  `wms_tasks`'ta o outbound'un görevi **yok**. S-18: politika kontrolünü ters çevir (her depoda ertele) → 0178'in mevcut S2 testleri kırmızı.
- [ ] AC-19 (zorunlu) — Görev üretimi: serbest bırakma sonrası her `Issued` siparişin `(AllocSeq, LocationId)` plan öğesi için tam **bir** pick
  görevi; `PlannedQuantity` = `PlannedBins.quantity`, `FromLocationId`, `ToLocationId` (Staging), `ReservationId` 0178 `PickTask` kuruluşuyla aynı (X-3 (a): `OutboundAllocationService` ile `WavePickIssuanceService` **aynı** ortak
  kurucuyu çağırır — mimari test: `WavePickIssuanceService` kendi `WarehouseTask` nesne başlatıcısını içermez);
  `WaveRef { WaveId, WaveNumber, Sequence }` dolu; görevler 0178 Toplama Görevleri listesinde görünür ve 0178 onay makinesiyle onaylanır (bir
  onay `Posted`). S-19: portta `Items` küme eşitliği denetimini kaldır, eksik öğeyle çağır → eksik görev, kırmızı; S-19b: `WavePickIssuanceService`'e
  kendi `new WarehouseTask { … }` kuruluşunu yaz → mimari test kırmızı.
- [ ] AC-20 (zorunlu) — Sipariş düzeyinde atomiklik: 0178 işleminde outbound `PickIssuance` güncellemesi, görevler ve olaylar birlikte yazılır;
  görev yazımı başarısızsa outbound `Deferred` kalır. S-20: görev eklemeyi işlem dışına al → enjekte hatada outbound `Issued`, görev yok, kırmızı.
- [ ] AC-21 (zorunlu) — İdempotent serbest bırakma ve kurtarma: (a) aynı istek iki kez → tek görev seti; (b) port 2. siparişte zaman aşımı → 503
  `WAVE_RELEASE_OUTCOME_UNKNOWN`, dalga `Releasing`; bakım kilit dolunca tamamlar → görev sayısı = plan öğesi sayısı, ikinci görev yok;
  (c) R1'den sonra süreç ölümü benzetimi → bakım aynı `PickPlan`'la tamamlar, `Sequence` değişmez. S-21a: port replay dalını kaldır → (b)'de çift
  görev, kırmızı; S-21b: kurtarmada rotayı yeniden hesapla (LOCATION verisi değiştirilmiş kurguda) → `Sequence` farkı, kırmızı.
- [ ] AC-22 (zorunlu) — Ön kontrol ve kısmi hata: (a) ön kontrolde uygun olmayan sipariş → 422 `WAVE_ORDERS_INELIGIBLE`, yazım yok; (b) ön kontrol ile
  port arasında 0178'de iptal edilen sipariş → `Excluded` + `OrderCancelledInWave` istisnası, diğerleri `Issued`, dalga `Released`; (c) hiçbiri
  açılamazsa dalga `Draft`, 422 `WAVE_NOTHING_ISSUED`. S-22: `Ineligible` sonucunu teknik hata gibi ele al → (b)'de dalga `Releasing` kalır, kırmızı.
- [ ] AC-23 (zorunlu) — Sınır onayı (D-7): sınır aşımlı dalgada `confirmLimitOverride` yok → 409 `WAVE_LIMIT_CONFIRMATION_REQUIRED`, yazım yok;
  `true` → 200. S-23: onay denetimini kaldır → kırmızı.
- [ ] AC-24 (zorunlu) — Rota belirlenimciliği: aynı plan öğeleri 100 rastgele karışık sırada verildiğinde `Sequence` her seferinde aynı; eşit bin
  kodlu öğelerde eşitlik bozma `OutboundId` → `AllocSeq`. S-24: sıralamadan eşitlik bozmayı kaldır → kırmızı.
- [ ] AC-25 (zorunlu) — Rota yöntemi (D-2): fikstür F1 — tek bölge, koridor A…D, her koridorda raf 01…10, her rafta bir görev (40 öğe) →
  (a) her `(bölge, koridor)` çifti tek ardışık blok; (b) blok sırası A, B, C, D; (c) raf yönü A artan, B azalan, C artan, D azalan;
  (d) `ProxyDistance = 39` (koridor içi 4 × 9 + koridor geçişi 3 × 1); aynı fikstürde kod sırası (koridor artan, raf artan) **66**'dır
  (4 × 9 + 3 × (1 + 9)) — rota ondan kısa. Fikstür F2 — iki bölge (`A`, `B` kodlu Zone) → önce `A`'nın bütün öğeleri. S-25: yön değiştirmeyi kaldır →
  (c) ve (d) kırmızı (66).
- [ ] AC-26 (zorunlu) — Geri düşüş: biçime uymayan kodlu bin'ler (`DOCK-1`, `COLD/7`) bölgenin ayrıştırılmış öğelerinden sonra kod sırasıyla;
  `RouteFallback = true`, `FallbackCount` doğru; Zone'suz yol `StorageLocation` koduyla gruplanır. S-26: geri düşüş öğelerini başa koy → kırmızı.
- [ ] AC-27 (zorunlu) — Performans bütçesi (§8.7): (a) 2.000 öğe sıralaması p95 ≤ 200 ms (test eşiği 600 ms); (b) 200 sipariş / 2.000 görev serbest
  bırakma ≤ 15 s (eşik 45 s); (c) 5.000 uygun outbound'da öneri ≤ 1 s (eşik 3 s); ölçümler kabul kaydına sayıyla yazılır. S-27: sıralayıcıya
  karşılaştırma başına LOCATION okuması ekle → (a) eşiği aşar, kırmızı.
- [ ] AC-28 (zorunlu) — Kesin saklama: `PickPlan` ve `RouteSummary` serbest bırakmada bir kez yazılır, sonra hiçbir yol değiştirmez; ayrıntı ekranı
  "Önerilen yol" bunu gösterir. S-28: ayrıntı okumasında rotayı yeniden hesapla → LOCATION değiştirilmiş kurguda fark, kırmızı.

**DL-3 — ilerleme + istisnalar + geri çekme**

- [ ] AC-29 (zorunlu) — İlerleme (D-5): fikstür — 3 sipariş, 6 görev; 4 görev terminal, 1 outbound `Packed`, 1 `Shipped` → toplama %67 (4/6),
  paketleme %67 (2/3), sevk %33 (1/3); 0180 bu okumada hiçbir koleksiyona yazmaz. S-29: paketleme sayımından `Shipped`'i çıkar → %33, kırmızı.
- [ ] AC-30 (zorunlu) — Durum uzlaştırması: ilk onayla `Picking`; bütün görevler terminal + geri çekme yok → `Completed`, `CompletedAt` = son
  görevin terminal olay zamanı (uzlaştırma zamanı değil); yazacak şey yoksa komut yazmaz (merkezi kayıt 0). **Y-2 (karar 03):** bir üyelik
  `Excluded` (serbest bırakmada) ya da `OrderCancelled` (0178 iptali) iken kalan `Issued` siparişlerin görevleri terminal → `PartiallyCompleted`
  (`Completed` değil); yalnız kısa toplama varken `Completed` (Q-10 = A). S-30: `CompletedAt`'i şimdiye bağla → kırmızı; S-30b: kısmi koşuldan
  `Excluded` / `OrderCancelled`'ı çıkar → dalga `Completed` olur, Y-2 vakası kırmızı.
- [ ] AC-31 (zorunlu) — Geri çekme (D-9): 3 `Issued` sipariş, biri başlamış → geri çekme 200; iki sipariş `Withdrawn`, görevleri 0178'de
  `Cancelled` (`Data.waveRecalled = true`), outbound'ları `Deferred` ve planlayıcıda yeniden uygun; başlamış sipariş dalgada; gerekçe olayda; gerekçesiz
  400. Başlamışın görevleri bitince dalga `PartiallyCompleted`. S-31a: `Started` denetimini kaldır → başlamış siparişin açık görevi iptal olur,
  kırmızı; S-31b: `Refused` onayı "başlamış" say → `StockMissing`'li sipariş çekilemez, kırmızı.
- [ ] AC-32 (zorunlu) — Kesim (D-9): kesim geçen ve bitmemiş dalga `Picking` kalır (otomatik kapanış yok), tek `CutoffMissed`; ikinci tur ikinci
  istisna açmaz. S-32: `SubjectKey` tekilliğini kaldır → iki istisna, kırmızı.
- [ ] AC-33 (zorunlu) — Riskli dalga (D-6): kesime 60 dk, toplama %50 → riskli; kesime 60 dk, %80 → değil; kesime 120 dk, %50 → değil; depo ayarı 150 dk
  → 120 dk / %50 riskli. S-33: eşiği sabite bağla → ayar vakası kırmızı.
- [ ] AC-34 (zorunlu) — Bekleyen görev (D-11): `IssuedAtUtc + 31 dk`'da `Open` görev → `TaskWaitingLong`; görev başlayınca bir sonraki turda
  `Resolved` + `SystemCleared` (olay `ExceptionCleared`); ayar 45 dk iken 31 dk'da istisna yok. S-34: kendiliğinden kapanmayı kaldır → kırmızı.
- [ ] AC-35 (zorunlu) — Geri çağırma (D-10): `LOT_UNDER_RECALL` ile `Refused` onay → `LotUnderRecall` istisnası; sipariş dalgada **kalır**, görev
  değişmez; vardiya sorumlusu "Siparişi çıkar" ile çıkarır. S-35: tespitte otomatik geri çekme çağır → kırmızı.
- [ ] AC-36 (zorunlu) — İstisna eylemleri: çıkar (başlamamış → `Withdrawn`; başlamış → 409 `WAVE_ORDER_STARTED`); taşı (D-13) — hedef verilmezse aynı
  depoda kesimi en yakın gelecek `Draft` dalga, verilirse o; hedef yok → 422 `WAVE_TARGET_INVALID`; başka depo hedefi 422; yok say (`Ignored`,
  gerekçe); her eylem `exceptions.resolve` ister. S-36: varsayılan hedef seçiminde kesim sıralamasını ters çevir → kırmızı.
- [ ] AC-37 (zorunlu) — 0178'de outbound iptali: `Issued` siparişin outbound'u 0178'de iptal edilir → sonraki turda üyelik `OrderCancelled`,
  istisna `OrderCancelledInWave`, toplama ilerlemesi iptal edilen görevleri terminal sayar. S-37: uzlaştırmada outbound durum okumasını kaldır →
  kırmızı.
- [ ] AC-38 (zorunlu) — Kısa toplama: 0178 kısa kapatması → `ShortPick` istisnası; dalga diğer koşullar tamamsa `Completed` (Q-10). S-38: kısa
  kapatmayı terminal saymayan koşul → dalga `Picking` kalır, kırmızı.

**DL-4 — ekranlar (7 dil)**

- [ ] AC-39 (zorunlu) — `Views/Warehouse/{Waves,WavePlanner,WaveExceptions,WaveKpis}/*.cshtml` (ve `Waves/Details.cshtml`) dosyalarının her birinde
  `Layout = "_LayoutTenantShell"` açık (`WavePlanningL10nAndHelpTests`; emsal `CycleCountL10nAndHelpTests.cs:235`). S-39: `WaveKpis/Index.cshtml`'den
  `Layout` satırını kaldır → kırmızı.
- [ ] AC-40 (zorunlu; araç kontrolü — bekçi değil) — `verify_datatable_page.py . --area Warehouse --module Waves` ve `--module WaveExceptions` PASS; `data_mode: server` beyanla
  aynı; satır eylemi "İptal" (Taslak), "Sil" yok (G-0180-01).
- [ ] AC-41 (zorunlu) — 7 dil (D-15): her resx anahtarı en, tr, fr, es, zh, ar, ru'da (parite testi); `ar` sağdan sola; kullanıcıya görünen
  hardcoded metin yok; hata kodu ve istisna türü metinleri 7 dilde. S-41: bir anahtarı `ru`'dan sil → parite kırmızı.
- [ ] AC-42 (zorunlu) — UAS-001: izni olmayan kullanıcı pano, ayrıntı, planlayıcı, istisna kuyruğu ve performans sayfalarında başlık / kart / boş
  tablo / düğme görmez; tek açıklama; yönlendirme yok. S-42: izin denetimini kaldırıp iskeleti çiz → kırmızı.
- [ ] AC-43 (zorunlu; tarayıcı kontrolü — bekçi değil) — Durumlar: her sayfada yükleniyor, hata ("Stok servisine ulaşılamadı."), boş ("Bu depoda henüz dalga yok", "Dalgaya girmemiş
  sipariş yok", "Açık istisna yok"), çakışma ("Kayıt değişti, yenileyin.") gerçek uç cevaplarıyla tetiklenir (503 / boş liste / 409) ve doğru
  metni gösterir (tarayıcı testi).
- [ ] AC-44 (zorunlu; canlı ölçüm — bekçi değil) — 375 px: pano ve istisna kuyruğu yatay sayfa kaydırmasız (canlı ölçüm `document.scrollingElement.scrollWidth ≤ 375`);
  planlayıcı salt okunur liste + "Ekle" düğmesiyle çalışır.
- [ ] AC-45 (zorunlu) — Planlayıcı klavye ve sürükle-bırak: yalnız klavyeyle (Tab / Enter / Shift+Enter / Delete) bir sipariş eklenir ve çıkarılır;
  sürükle-bırak aynı uca gider; sonuç `aria-live`'da okunur; Allocating satırı gri, eklenemez, nedeni yazılı. S-45: "Ekle" düğmesini kaldır →
  klavye testi kırmızı.
- [ ] AC-46 (zorunlu; tarayıcı kontrolü — bekçi değil) — Kesim gösterimi (D-3): tarayıcı saat dilimi depodan farklıyken iki saat birden; aynıyken tek saat.
- [ ] AC-47 (zorunlu; tarayıcı kontrolü — bekçi değil) — Ağırlık / hacim (D-12) değer yokken "—"; risk / durum / istisna türü ikon + metinle (yalnız renk değil).
- [ ] AC-48 (zorunlu) — Manifest sayfaları (`WAVES`, `WAVEDETAIL`, `WAVEPLANNER`, `WAVEEXCEPTIONS`) ve her gerçek düğme için bir eylem;
  `ApiPermissions` = `warehouse.waves.settings.configure` (Q-6 = B; DL-1'den beri);
  `python3 scripts/check_pack_nav_keys.py` PASS; `delete` / `bulk-delete` eylemi yok. S-48: manifestte `warehouse.waves.delete` bildir → AC-5
  uyum testi kırmızı.
- [ ] AC-49 (zorunlu) — Sayfa yardımı (UXH-001): `Help.{KOD}.Purpose`, `.Audience`, `.Step{n}Title` / `.Step{n}` (§25) 7 dilde; tur yok
  (`WavePlanningL10nAndHelpTests`, `PageHelpResource.{7}.resx`'i okur). S-49: `PageHelpResource.zh.resx`'ten `Help.WAVES.Step2`'yi sil → kırmızı.
- [ ] AC-50 (zorunlu — Q-12 = A, karar 03; XMC X-7) — 0178 Toplama Görevleri listesinde "Dalga" sütunu ve `waveId` süzgeci; `sort=waveSequence` dalga
  sırasını verir; varsayılan sıralama ve 0178 testleri değişmez. S-50: sıralamayı `Number`'a bağla → dalga sırası testi kırmızı.

**DL-5 — performans + CSV**

- [ ] AC-51 (zorunlu) — KPI formülleri (§8.16) fikstürle: 3 günde 5 dalga (bilinen `ReleasedAt` / `CompletedAt` / `CutoffAtUtc`, iki toplayıcı) →
  günlük sayı, ortalama süre, kesime yetişme oranı ve satır / saat beklenen değerlerle aynı; günler deponun yerel gününe göre (gece yarısı sınırı
  vakası). S-51: günü UTC'ye göre kovala → sınır vakası kırmızı.
- [ ] AC-52 (zorunlu) — Aralık: 92 gün 200, 93 gün 400 `WAVE_KPI_RANGE_TOO_LARGE`; depo zorunlu. S-52: üst sınırı kaldır → kırmızı.
- [ ] AC-53 (zorunlu) — CSV: `kpis.export` ister (yoksa 403); içerik JSON serisiyle satır satır aynı; `Accept-Language: tr` başlığı Türkçe, `ar`
  Arapça; sayılar değişmez kültürde; başka kiracının verisi yok. S-53a: CSV yolundan `[HasPermission]` kaldır → `AuthorizationGuardTests` kırmızı;
  S-53b: sayıları istek kültürüyle yaz (`tr` → virgül) → kırmızı.
- [ ] AC-54 (zorunlu) — Performans menüde **ayrı sayfa** (`WAVEKPIS`, nav, D-16), `Layout` açık, UAS-001, `check_pack_nav_keys.py` PASS.
  S-54: Web `WaveKpisController`'ın sayfa eyleminden izin denetimini kaldır → izinsiz kullanıcı iskeleti görür, UAS-001 testi kırmızı
  (AC-42 testinin `WAVEKPIS` vakası).

**DL-6 — Gateway kapsama testi**

- [ ] AC-55 (zorunlu) — `gateway/Diten.ApiGateway.Tests/Mod0180WaveRouteTests.cs` 0180'in her ucunu (uç listesi üretim
  `INV/Api/Controllers/WavePlanning/` kaynağından ve WMS-WAVE-BUNDLE taslağından okunur, testte elle yazılmaz) **üretim** `ocelot.json`'un
  (`Diten.ApiGateway.Tests.csproj:26` bağlı kopyası) `/api/warehouse/{everything}` rotasıyla 5067'ye tam bir rotada yönlendirildiği haliyle
  doğrular; `ocelot.json` ve `Mod0178WarehouseRouteTests.cs` değişmez, 0178 testi değişmeden yeşil (X-14). Sabotajlar **üretimi** bozar
  (integration-agent, geri alınır; F-5): S-55a: `gateway/Diten.ApiGateway/ocelot.json`'da `/api/warehouse/{everything}` bloğunun
  `Port`'unu (`:1158`) geçici 5068 yap → kırmızı; S-55b: bir 0180 controller'ının sınıf `[Route]`'unu geçici `api/waves` yap → o uçlar
  hiçbir rotaya eşleşmez, kırmızı.

**AC sayısı: 56** (hepsi zorunlu; r1'deki koşullu AC-50 Q-12 = A ile zorunlu oldu; AC-56 r2'de eklendi — X-15).

## 17. Test Expectations
- **Birim (Domain, saf):** durum makinesi (§8.3 tablosu tam), rota (S-biçimi, ayrıştırma, geri düşüş, eşitlik bozma, ölçü), saat dilimi dönüşümü
  (boşluk / çakışma), risk, sınır uyarıları, istisna tespit koşulları, KPI formülleri.
- **Entegrasyon (Mongo replika seti):** InventoryService test paketi `INVENTORY_TEST_MONGO` yoksa kendi replika setini başlatır (MVP-1 ortam notu);
  AC-2…AC-38 gerçek MOD-0178 koduyla (outbound yaratma → tahsis → serbest bırakma → 0178 onayı); sahte port yalnız zaman aşımı / hata
  enjeksiyonunda (AC-21, AC-22).
- **MOD-0178 davranışı:** X-1…X-5'in ölçümü 0180 test ağacında; 0178 test dosyaları değişmez (AC-18). Servise dokunulduğunda **bütün**
  InventoryService paketi koşar (filtre yok — MVP-1 CT kuralı).
- **Mimari:** AC-5 (`AuthorizationGuardTests`, `WarehouseRegistrationTests` değişmeden), AC-8 (`LedgerImmutabilityArchitectureTests` değişmeden),
  AC-9 (`CentralAuditDeclarationTests`), AC-16 (0180 yazma yasağı taraması), AC-17 (`SingleSystemOfRecordArchitectureTests`).
- **Sabotaj kanıtı:** §16'daki her `S-n` kabul kaydında komut + kırmızı çıktı + geri alma ile.
- **Frontend:** `verify_datatable_page.py` (AC-40), RESX parite 7 dil (AC-41), UAS-001 (AC-42), durumlar (AC-43), 375 px canlı (AC-44), klavye
  (AC-45), tarayıcı smoke (E3): dalga oluştur → öner → ekle → serbest bırak → 0178'de toplama onayı → ilerleme → geri çek → istisna çöz → KPI + CSV.
- **Rol İzinleri (X-15, AC-56):** `npx vitest run tests/role-assignments-label-ledger.test.js` (Web, `frontend/Diten.Web`) manifest anahtarı
  bildiren her dilimde (DL-1, DL-3, DL-5) koşar; Web `vitest` paketinin tamamı DL-4 / DL-5'te.
- **Build:** `Diten.InventoryService`, `Diten.Web` PASS; Gateway test projesi (AC-55) PASS — üretim `ocelot.json`'u okur (§15).
- **Sözleşme:** WMS-WAVE-BUNDLE taslağına karşı sözleşme testi (AC-1).

## 18. Ready-for-dev Checklist
- [x] Golden Reference Slim referans alındı; form alanı 8 → slim (§4.1; Q-1 = A, karar 03); sapma G-0180-01 adıyla §10'da
- [x] Frontmatter zorunlu alanları dolu (service, shell, golden_reference, data_mode, screens, entity_base)
- [x] Layout & Shell Contract'ta `_LayoutTenantShell` açıkça yazılı (§9, AC-39 + S-39)
- [x] Backend File Convention Golden Reference ile birebir (§10)
- [x] Frontend File Contract Slim dosya listesi tam; Compact dosyası yok (§11)
- [x] Validation Rules her alan için yazılı (§12)
- [x] Failure Path en az 4 senaryo (duplicate, missing, unauthorized, concurrency) (§13)
- [x] Authorization Convention: namespace (D-4), anahtarlar (Q-6 = B ayar izni dahil), politika, aktör (§14)
- [x] Gateway kararı: `ocelot.json` değişmez; integration-agent işi tanımlı; sabotaj üretimi bozar (AC-55)
- [x] Acceptance criteria test edilebilir; her bekçi sabotajlı, bekçi olmayanlar adıyla işaretli (§16)
- [x] Audited Events tablosu her yazma komutunu tam bir kez içeriyor (§21)
- [x] Sayfa yardım metni her sayfa için yazılı (§25); tur yok
- [x] Sahip kararları: Q-1…Q-16, Q-18, Q-19, Q-21, Q-22 ve Y-1…Y-4 karar 03 ile kapandı (§19.4); D-1…D-17 (kararlar 01, 02)
- [ ] Q-17 hedef tarih — **TBD (sahip)**; künye `target` (dilim başlangıcını engellemez, plan izlemesi için)
- [ ] Q-20 rol şablonu — karar Ali (Auth); X-13 ayrı WP, DL-4 kabul kapısı (§24)
- [ ] CT kararı CT-1 — WMS-BUNDLE eklemeli sürüm numarası (1.2.0 ya da 1.3.0, MOD-0178 CT ile eşgüdüm) (§19.4)
- [ ] XMC-001 satırları X-1…X-16 sahiplerinden onay aldı (§5.1); X-15'in sahibi TBD (öneri Ali)
- [ ] **`.antigravity/**` kapısı — paket onayından ÖNCE (Y-4, karar 03):** PKS-001 §4 `warehouse.waves.*` satırı ve RCS-001 §3 `WAV` satırı
  (+ aynı adımda X-12, X-16); CT hazırlar, Beste onaylar; önerilen metin §6. Bu kapı kapanmadan onay satırı yazılmaz (§6, §24)
- [x] Kararlar 02 dosyası depoya alındı (Q-15) — MVP-1 hattında (`ct/line-ff-2026-11-12i` ve sonrası)
- [ ] WMS-WAVE-BUNDLE taslağı yazıldı (DL-1 ilk adımı, AC-1); dondurma ayrı CT adımı
- [ ] WMS-BUNDLE eklemeli sürümü (X-8: `OutboundEventKind` değerleri + `wave` / `pickIssuance` + X-7 üyeleri) MOD-0178 CT adımıyla yazıldı ve
  donduruldu — **DL-2 başlamadan önce** (F-3)
- [ ] r2 bağımsız inceleme (CT-APR-02) ve onay satırı (CT-APR-01) — **onaylayan Ali (GitHub `GMG-ERP`), D-17 (karar 02)**; yazan ≠ onaylayan

## 19. Implementation Notes

### 19.1 Okunan kaynaklar
- Brief `execution/domains/supply-chain-execution/work-packs/mockups/wave-planning/BRIEF-MOD-0180-wave-planning.md` (tamamı, `:1-93`).
- Kararlar 01 `docs/records/decisions/2026-11/mod0180-wave-planning-owner-decisions-01.md` (tamamı).
- Kararlar 02 — `docs/records/decisions/2026-11/mod0180-wave-planning-owner-decisions-02.md` (MVP-1 hattında; r1 yazımında dalsız commit
  `58e3e73b4`'ten `git show` ile okunmuştu; içerik aynı) (Q-15).
- Mockup v2 (`C:\Users\Ali\Downloads\Dalga Planlama (standalone) v2.html`, 2.223.236 bayt, paketlenmiş JS sayfası) — yalnız metin dizgeleri çıkarıldı
  (form etiketleri, durumlar, KPI, istisna eylemleri, durum örnekleri); çalıştırılmadı, görsel olarak incelenmedi (kısmi inceleme).
- `execution/domains/supply-chain-execution/domain-config.md` (tamamı); MOD-0181 paketi — r1 yazımında **r7** okundu (yapı, §2–§8, §16, §18,
  §20–§25; §19 kısmen); geçerli onaylı sürüm **r8** (`execution/approvals/approvals.tsv:83`, LF sha256 `f943ab48…` = `dc010b911` dosyası) —
  r2'de atıflar r8 konumlarına taşındı (`:219`, `:235-239`, `:299-302`, `:573-576`, `:1395`); MOD-0178 paketi — r1'de r27 okundu (§1–§4.6,
  §8.17–§8.21 başı), r2'de atıflar **r28** konumlarına taşındı (r28 farkı yalnız künye, §19.1.27 ve Item adımı satırları; atıf yapılan bölümler
  +8 / +9 kaydı, içerik aynı) ve §14.1 (`:2117-2120`) okundu; diğer bölümler okunmadı — kısmi inceleme.
- r2 girdileri: bağımsız inceleme r1 `docs/records/reviews/MOD-0180/2026-11-12-c7c774c6.md` (tamamı); sahip kararları 03
  `docs/records/decisions/2026-11/mod0180-wave-planning-owner-decisions-03.md` (tamamı); kararlar 02 yeniden (D-9 metni, D-17).
- r2 ölçümleri (`dc010b911`, salt okuma): `OutboundAllocationService.cs:165-300`; `CentralAuditDeclarationTests.cs:40-132`;
  `SingleSystemOfRecordArchitectureTests.cs:86-120`; `ContractSchemas.cs:15-24`, `:404-407`; `wms-bundle.openapi.yaml:64`, `:1352`, `:1694`,
  `:1757`, `:1805-1835`; `gateway/Diten.ApiGateway.Tests/Mod0178WarehouseRouteTests.cs:1-60`, `:215-250` ve `.csproj:24-26`;
  `ocelot.json:1130-1162`; Governance `_IndexL10n.cshtml` fiil satırları, `perm-label.js:52-54`, `:165-295`,
  `role-assignments-label-ledger.test.js:1-40`, `docs/roadmap/backlog/role-permission-label-ledger.md:1-30`; bu dosyaların `git log`'u;
  `.antigravity/rules/permission-key-standard.md:98-110`, `record-coding-standard.md:45-75`; `RecordCodeTypeCatalogTests.cs:24-29`;
  `git diff a3f329ece dc010b911` (servis, sözleşme, kural ve paket atıflarının kayması).
- **Koşulmayanlar (r2):** test, build, vitest, sabotaj koşulmadı; mockup v2 ve Blueprint xlsx r2'de yeniden açılmadı; X-15'in beklenen
  satır kodları (`WAVES`, `WAVESSETTINGS`, …) üretim `PermLabel.split` ile ölçülmedi — çıkarımdır, DL-1'de vitest ile ölçülür.
- Blueprint 8.8 satır 181 (xlsx); registry `:318`; MVP-5 planı `:78`, `:124` (W-4 satırı; plan yetenek kimliği vermez).
- `.claude/agents/module-pack-author.md`, `.antigravity/rules/module-pack-standard.md` (`:1-110`), PKS-001 §4 tablosu ve §5, RCS-001 §3 başlıkları
  ve `CNT` / `PCK` satırları, AGENTS.md §6.1.
- MOD-0040-FU02 paket künyesi ve DL-8 satırları.
- DEV-0000 metni r1'de **açılmadı**; Golden Slim yapısı module-pack-author ajan tanımındaki kopyadan ve MOD-0181 §10 ölçümünden alındı.
  r1 bağımsız incelemesi DEV-0000 Golden Slim kod ağacını (Application `Features/GoldenReferenceSlim`, Web
  `Views/DevEnablement/GoldenReferenceSlim`) §10 / §11 ile karşılaştırdı ve uyumlu buldu (inceleme kaydı "Dayanaklar").

### 19.2 Ölçülen gerçekler (r1 `a3f329ece`; r2'de `dc010b911`'de yeniden ölçüldü, salt okuma)
- 0178 pick görevleri tahsisin işlem B'sinde, outbound'la aynı işlemde açılır (`OutboundAllocationService.cs:172-185` numaralar işlem dışı,
  `:200-213` kurulum, `:227-233` tek `InTransactionAsync`); kurucular `PickTask` `private static` `:248`, `TaskCreated` `private` `:288`;
  dalga için erteleme zorunlu bir 0178 değişikliğidir (X-3).
- 0178 kayıt testi `Controllers.Warehouse` namespace'indeki bütün controller anahtarlarını 0178 manifestiyle eşitler (`WarehouseRegistrationTests.cs:49-56`).
- `CentralAuditDeclarationTests`: `dc010b911`'de **117** yazma komutu (`:53`; r1'in ölçtüğü 116 `a3f329ece`'deydi, 117'ye MOD-0181 DL-5
  `c76c02944` çıkardı), 14 işlem geri çağrısı (`:130`), sağlayıcı listesi `:60-69`.
- `SingleSystemOfRecordArchitectureTests`: 0181 bildirimleri `:88-89`, `:92-93`; `declared` `:100-115`; `wms_` önek süzgeci `:120`.
- WMS-BUNDLE 1.1.0 (`:64`); `OutboundEventKind` kapalı enum 15 değer (`:1812-1814`); servis sözleşme testleri `additionalProperties`'siz
  her nesneyi kapalı okur (`ContractSchemas.cs:19-21`, `:404-407`).
- PUT / DELETE bekçisi `LedgerImmutabilityArchitectureTests.cs:131-160`.
- Gateway `/api/warehouse` + `/api/warehouse/{everything}` → 5067 (`ocelot.json:1134-1161`; `{everything}` portu `:1158`); Gateway testleri
  üretim `ocelot.json`'u bağlı kopya olarak okur (`Diten.ApiGateway.Tests.csproj:26`); 0178 testi `api/warehouse` controller'larını özyineli
  tarar (`Mod0178WarehouseRouteTests.cs:219-236`).
- Governance Rol İzinleri: `recall` fiili köprüde ve ailelerde yok; `configure`, `release`, `resolve`, `read`, `create`, `update`, `export`,
  `cancel` var (`_IndexL10n.cshtml:165-276`, `perm-label.js:52-54`); defter "fiil 0 · satır 96".
- LOCATION: koridor / raf / saat dilimi alanı yok; yalnız v1.1 toplu bin kodu biçimi (`location.openapi.yaml:148`, `:205-252`, `:280-287`).
- Outbound: taşıyıcı, müşteri kimliği, kesim saati, ağırlık / hacim alanı yok (`warehouse-outbound.openapi.yaml:76-97`; MOD-0178 §4.5).
- RCS-001 §3'te `WAV` yok; Platform `RecordCodeTypeCatalogTests` RCS satırı ile kataloğu eşitler (`2f7b6145e`; sözlük `:29` `CNT`).
- PKS-001 §4: `warehouse.*` → MOD-0178 (`:105`); §5: `edit` → `update`.
- `a3f329ece` → `dc010b911` arası: sözleşmeler, kurallar, domain-config, registry, plan, brief ve 0180'in atıf yaptığı 0178 kod dosyaları
  **değişmedi** (`git diff --stat` boş); MOD-0178 paketi r27 → r28, MOD-0181 paketi r7 → r8 (atıf kaymaları §19.1).
- **Mockup v2 ile brief / karar farkları:** (1) yaratma panelinde 9 alan (taşıyıcı ayrı, "En çok sipariş" ve "En çok satır" ayrı) — paket 8
  (Q-1); (2) mockup "önerilen ad alanı `wave.`" diyor — karar `warehouse.waves.*` (D-4); (3) mockup risk eşiğini "kesime < 90 dk, ilerleme < %70"
  gösterir — D-6 ile aynı; (4) mockup sipariş satırında "taşıyıcı" ve "Taşıyıcı süzgeci yok" uyarısı gösterir — v1'de veri yok (Q-1).

### 19.3 Çıkarımlar
- S-biçimli rota "en kısa yol"un yaklaşığıdır; kesin optimum değildir (§8.7).
- Uzlaştırma servisinin 60 sn turu durum gecikmesi yaratır; olay yolu (bus) gelince FU-0180-EVENTS.
- `TaskWaitingLong` görev atanmamış ama başlamamış görevi de sayar; atanmış / atanmamış ayrımı v1'de yok.

### 19.4 Kararlar (Q-n, Y-n) — karar 03 sonrası durum

Kaynak: sahip kararları 03 (`docs/records/decisions/2026-11/mod0180-wave-planning-owner-decisions-03.md`, Beste, 2026-11-12): "Y-1…Y-4 için
önerdiğin gibi, Q'lar için Q-6 ayar izni, diğerleri öneri gibi". Açık kalan: **Q-17** (TBD, sahip), **Q-20** (Ali), **CT-1** (CT).

| Q | Konu | Seçenekler | Karar (r2) | Karar sahibi |
|---|---|---|---|---|
| Q-1 | Taşıyıcı / sevk yöntemi süzgeci (mockup v2 alanı) — outbound'da veri yok | A: v1'de alan yok, 8 alan → slim · B: alan kalır, kaynak kararı + 9 alan → compact | **A** (karar 03); FU-0180-CARRIER — form 8 alan, slim değişmedi | Beste |
| Q-2 | Görev ertelemesinin anahtarı | A: depo ayarı `WaveManaged` + 0178 `IPickIssuancePolicy` portu · B: outbound başına istek bayrağı | **A** (karar 03) | Beste (0180 + MOD-0178 sahibi) |
| Q-3 | Depo saat dilimi kaynağı (D-3) — LOCATION'da yok | A: 0180 depo ayarı `TimeZoneId` · B: LOCATION `Warehouse` alanı | **A** (karar 03); FU-0180-TZ-LOCATION | Beste |
| Q-4 | `WAV` ara üreticisi — FU02 DL-8 yok | A: 0178 geçici sayacı + `WAV` (X-6) · B: FU02 DL-8'i bekle | **A** (karar 03); FU-0180-RCS | Beste (K2) |
| Q-5 | İzin son eki `edit` ↔ PKS-001 §5 `update` | A: `warehouse.waves.update` · B: `edit` | **A** (karar 03) | Beste |
| Q-6 | Depo dalga ayarının izni | A: `warehouse.waves.update` · B: yeni `warehouse.waves.settings.configure` (Tier-3) | **B** (karar 03; inceleme F-15) — §14, AC-15, AC-48 | Beste |
| Q-7 | PKS-001 §4 satır metni ve MOD-0178 Q-08 ile fark kaydı | A: §4'e alt ad alanı satırı + MOD-0178 §14.1'e not (X-16) · B: yalnız §4 satırı | **A** (karar 03); metin önerisi §6 | Beste |
| Q-8 | Teknik üst sınır (500 sipariş / 2.000 görev) | A: kabul · B: başka değer | **A** (karar 03) | Beste / CT |
| Q-9 | Bölge sırası | A: bölge kodu artan · B: depo ayarında liste | **A** (karar 03); FU-0180-ZONEORDER | Beste |
| Q-10 | Kısa toplama ve "Tamamlandı" | A: kısa kapatılan görev terminal → `Completed` + `ShortPick` · B: kısa toplama `PartiallyCompleted` | **A** (karar 03). Gerekçe (r2, F-2): "Kısmen tamamlandı" geri çekme (D-9) ve `Excluded` / `OrderCancelled` üyeliğe (karar 03 Y-2) bağlıdır; kısa toplama bu kümeye girmez | Beste |
| Q-11 | Uzlaştırma turu | A: 60 sn · B: başka | **A** (karar 03) | CT |
| Q-12 | Toplayıcı 0178 listesinde dalga sırası (X-7) | A: 0178 listesine dalga sütunu + sıralama · B: yalnız 0180 ayrıntısı | **A** (karar 03) — AC-50 zorunlu | Beste (MOD-0178 sahibi) |
| Q-13 | Build / Buy (W-0180-01) | A: Build · B: Buy/Partner | **A** (karar 03); FU-0180-EA | Beste |
| Q-14 | AI Assist | A: v1 dışı · B: v1'de | **A** (karar 03); FU-0180-AI | Beste |
| Q-15 | Kararlar 02 dosyası | A: CT dosyayı çalışma dalına alır | **KAPANDI — A** (r1; dosya depoda) | CT |
| Q-16 | Dal | A: tek çalışma dalı `feature/sce/mod-0173-inventory-ledger-valuation` · B: ayrı dal | **A** (karar 03) | Beste |
| Q-17 | Hedef tarih (künye `target`) | plan satırı tarih vermez | **TBD (sahip)** — karar 03 "sahipten bekleniyor" | Beste |
| Q-18 | KPI CSV okuma denetimi (AUD-001 §7) | A: v1'de yok · B: okuma kaydı | **A** (karar 03); FU-0180-READAUDIT | Beste |
| Q-19 | Planlayıcı DataTable değil | A: kabul · B: sol listeyi DataTable yap | **A** (karar 03) | Beste / CT |
| Q-20 | Rol şablonu (X-13) | A: §14 önerisi · B: başka | **Açık — karar Ali (Auth)**; öneri §14 (`settings.configure` şablonsuz) | Ali (Auth) |
| Q-21 | "Müşteri ya da teslim bölgesi" süzgeci | A: `ShipTo` metin eşleşmesi · B: v1'de süzgeç yok | **A** (karar 03); FU-0180-CUSTOMER | Beste |
| Q-22 | Sipariş başına kesim saati yok | A: planlayıcı sütunu "—" · B: outbound'a kesim alanı | **A** (karar 03) | Beste |
| Y-1 (F-1) | Ertelenmiş sipariş varken dalga yönetimini kapatma | A: 409 · B: kapatınca 0178'e hepsini aç isteği | **A — 409 `WAVE_SETTINGS_DEFERRED_ORDERS`** (karar 03) — §4.5, §8.6, AC-15, S-15b | Beste |
| Y-2 (F-2) | Dalgadan çıkarılan / 0178'de iptal edilen sipariş | A: `PartiallyCompleted` · B: `Completed` | **A** (karar 03) — D-9 genişler; §8.3, §8.9, AC-30, S-30b | Beste |
| Y-3 (F-11) | "En kısa yol" (D-2) | A: S-biçimli v1 yaklaşığı | **A** (karar 03) — §8.7, AC-25 | Beste |
| Y-4 (F-10) | `.antigravity` satırlarının zamanı | A: paket onayından önce · B: modül PR'ından önce | **A** (karar 03) — §6, §18, §24 | Beste |
| CT-1 (F-3) | WMS-BUNDLE eklemeli sürüm numarası (X-8) | 1.2.0 (0178 Item adımıyla birleşik) · 1.3.0 (Item 1.2.0'dan sonra) | **Açık — CT kararı**; adım DL-2 başlamadan önce | CT (MOD-0178 CT ile) |

### 19.5 r1 → r2 kapanış tablosu

| # | Bulgu / karar | r2 değişikliği | Bölüm |
|---|---|---|---|
| F-1 | Dalga yönetimi kapatılınca ertelenmiş siparişler takılır (ORTA) | Y-1: `Deferred` / `Allocating` outbound varken kapatma 409 `WAVE_SETTINGS_DEFERRED_ORDERS`; kalan yarış ve çıkış yolu yazıldı; AC-15 (a)…(e) + S-15b | §4.5, §8.6, §8.17, §12, §13, §16 AC-15 |
| F-2 | D-9 genişletilmiş ve yanlış atfedilmiş (ORTA) | Y-2: `Excluded` / `OrderCancelled` → `PartiallyCompleted` "karar 03 Y-2" diye atfedildi; D-9'un karar 02 metni ayrı yazıldı; Q-10 gerekçesi düzeltildi; AC-30 + S-30b | §8.3, §8.9, §16 AC-30, §19.4 Q-10 |
| F-3 | X-8 eksik ve sırası geç (ORTA) | X-8'e `OutboundEventKind` `PickTasksIssued` / `PickTasksWithdrawn`; kapalı şema ölçümü; eklemeli sürüm **DL-2 başlamadan önce** CT adımı; numara sabitlenmedi — "0178 CT ile eşgüdümlenen bir sonraki eklemeli sürüm (1.2.0 ya da 1.3.0)", CT-1 | §5.1 X-8, §7, §18, §19.4 CT-1, §24 |
| F-4 | XMC listesinde rol köprüsü yok (ORTA) | X-15 (Governance `_IndexL10n.cshtml`, `RoleAssignmentsIndex` 7 resx, `perm-label.js`; `recall` yeni fiil, `configure` var); sahip depoda ölçüldü (sahiplenen pack yok → TBD, öneri Ali), XMC-001 açık; §5 ortak kaynak tanımı `Perm.Entity.*` ile genişledi; AC-56 + S-56 | §5, §5.1 X-15, §7, §16 AC-56, §17 |
| F-5 | S-55 sabotajı testi bozuyor (ORTA) | Gateway testinin üretim `ocelot.json`'u okuduğu ölçüldü (`.csproj:26`, `Mod0178WarehouseRouteTests.cs:23`, `:219-236`); S-55a üretim `ocelot.json` portu, S-55b üretim controller `[Route]`'u (integration-agent, geri alınır) | §5.1 X-14, §15, §16 AC-55 |
| F-6 | AC-9 ve X-10 bayat sayıya bağlı (ORTA) | AC-9 "dilim başındaki ölçülen sayı + dilimin 0180 komutları"; X-10 `:53` (117), `:60-69`, `:130`; X-9 `:88-89`, `:92-93`, `:100-115`, `:120` | §5.1 X-9 / X-10, §16 AC-9, §19.2 |
| F-7 | Belge atıfları eski konumda; 0181 r7 (DÜŞÜK) | Kod atıfları `dc010b911:` ile sabitlendi; MOD-0178 atıfları r28 (+8 / +9), MOD-0181 atıfları r8 (+38); 0181 onaylı sürüm r8 yazıldı | giriş notu, §0, §2, §3, §4, §6, §7, §8, §19.1 |
| F-8 | D-17 pakette yok (DÜŞÜK) | Onaylayan Ali (GitHub `GMG-ERP`), D-17 — künye `revision`, giriş notu, §18 | künye, giriş, §18 |
| F-9 | `status_note` Q-15'i açık sayıyor (DÜŞÜK) | `status_note` güncellendi (Q-15 kapalı; r2 inceleme + Ali onayı bekler) | künye |
| F-10 | `.antigravity` satırlarının zamanı (DÜŞÜK) | Y-4: paket onayından ÖNCE, sahip onayıyla; CT hazırlar; satır metinleri önerildi | §6, §18, §24 |
| F-11 | D-2 yorumu sahip teyitsiz (DÜŞÜK) | Y-3: S-biçimli v1 yaklaşığı sahipçe kabul | §8.7, §19.4 |
| F-12 | Ad ve atıf tutarsızlığı (DÜŞÜK) | Tek ad: port `IPickIssuancePolicy`, 0180 uygulaması `WavePickIssuancePolicy`; X-3 aralığı `:227-233` | §5.1 X-3 / X-4, §8.6 |
| F-13 | Adlı sabotajı olmayan bekçiler (DÜŞÜK) | S-12b, S-39, S-49, S-54 eklendi; AC-40, AC-43, AC-44, AC-46, AC-47 "bekçi değil" diye işaretlendi | §16, §24 |
| F-14 | AC-18 karşılaştırma referansı tanımsız (DÜŞÜK) | Referans: X öncesi commit'te aynı fikstür betiğiyle üretilip kabul kaydına yazılan normalize JSON + sha256; kanıttır, bekçi değil; bekçi 0178 testleri | §16 AC-18 |
| F-15 | Q-6 = A riski (DÜŞÜK) | Q-6 = B (karar 03): `warehouse.waves.settings.configure` Tier-3; izin tablosu, manifest `ApiPermissions`, rol önerisi, AC-15 / AC-48, S-15c | §8.15, §13, §14, §16 |
| F-16 | Kurucuların paylaşımı (DÜŞÜK) | X-3 (a): `PickTask` / `TaskCreated` davranış değişmeden 0178 içinde ortak yardımcıya (`OutboundPickTaskFactory`); X-4 dosya listesine eklendi; AC-19 + S-19b | §5.1 X-3 / X-4, §8.6, §16 AC-19 |
| Y-1…Y-4 | Sahip kararları 03 | F-1, F-2, F-11, F-10 satırları | §19.4 |
| Q-6 | Ayar izni B | F-15 satırı | §14, §16 |
| Q-1…Q-5, Q-7…Q-16, Q-18, Q-19, Q-21, Q-22 | Öneri gibi (A) | §19.4 "Karar (r2)" sütunu; koşullu ifadeler ("Q-4 = A ise", "Q-12 = A ise") kesinleştirildi; AC-50 zorunlu | §5.1 X-6 / X-7, §16, §19.4 |
| Q-17 | Hedef tarih | künye `target` "TBD (sahip)" | künye, §18 |
| Q-20 | Rol şablonu | karar Ali (Auth); öneri §14 | §14, §18 |
| D-17 | Onaylayan Ali (karar 02) | F-8 satırı | künye, §18 |

## 20. Follow-up Items
- **FU-0180-RCS** — `WAV`'ın MOD-0040-FU02 ortak servisine geçişi (DL-8 + FU-0178-RCS-01 ile; Q-4).
- **FU-0180-CARRIER** — taşıyıcı süzgeci ve "farklı taşıyıcı" uyarısı (MVP-6 taşıyıcı verisi; Q-1).
- **FU-0180-CUSTOMER** — müşteri kimliğiyle süzgeç (O2C; Q-21).
- **FU-0180-TZ-LOCATION** — saat diliminin LOCATION `Warehouse`'a taşınması (Q-3).
- **FU-0180-AUTO** — kurala göre periyodik otomatik dalga (D-1 v1 dışı).
- **FU-0180-ZONEORDER** — bölge sırası ayarı (Q-9). **FU-0180-ROUTE-2** — kat / tek yön / sıcaklık bölgesi maliyetleri.
- **FU-0180-PICKER-SPLIT** — toplayıcıya rota bölme ve yük dengeleme.
- **FU-0180-WEIGHT** — ağırlık / hacim (MOD-0290 P9 ambalaj verisi; D-12).
- **FU-0180-EVENTS** — uzlaştırma turu yerine olay yolu.
- **FU-0180-AI** — Blueprint AI Assist (Q-14). **FU-0180-EA** — Blueprint Build/Buy düzeltmesi (Q-13).
- **FU-0180-READAUDIT** — KPI / CSV okuma denetimi (Q-18).
- **FU-0180-SETTINGS-UI** — depo dalga ayarı ekranı (v1 yalnız API; izin `warehouse.waves.settings.configure`, Q-6 = B).

## 21. Audited Events

Servisin denetim altyapısı **bugün var**: `tests/architecture/audit-ledger/Diten.InventoryService.md:12` (`inventory-merkezi-iletim | b | işaret |
ICentralAuditDeclaration`). Kayıt ad ve gövde taşımaz; "olay adı" = `RequestType` (komut sınıfı) + `Operation` (Platform enum adı); kategori
`MasterData` (servisin); `SourceModule` = `WAVE-PLANNING`. Önceki / sonraki değerler ve gerekçeler yalnız `wms_wave_events.Data`'dadır (aynı işlem,
silinmez). Başarılı ve yazan komut 1 kayıt; replay / 0 kayıt 0; başarısız 0 (§8.13). 0178 portunun yazımları çağıran 0180 komutunun kaydına enlist
olur.

| Komut | Olay adı | Nesne (tür) | Yol | Önceki/sonraki ya da değişen alanlar | İstisna sınıfı + gerekçe |
|---|---|---|---|---|---|
| CreateWaveCommand | CreateWaveCommand + Create | Wave | b | Sonraki: Number, WarehouseId, WaveType, CutoffLocal / CutoffAtUtc, TimeZoneId, ShipToFilter, MaxOrders, MaxLines, ResponsibleUserId, Note | — |
| UpdateWaveCommand | UpdateWaveCommand + Update | Wave | b | Önceki / sonraki: değişen form alanları | — |
| AddWaveOrdersCommand | AddWaveOrdersCommand + Update | Wave (+ WaveOrder) | b | eklenen OutboundId / numara, `AddedVia`, sınır uyarıları | — |
| RemoveWaveOrdersCommand | RemoveWaveOrdersCommand + Update | Wave (+ WaveOrder) | b | çıkarılan siparişler, gerekçe (varsa) | — |
| CancelWaveCommand | CancelWaveCommand + LifecycleTransition | Wave | b | önceki durum, **gerekçe**, kapanan üyelikler | — |
| ReleaseWaveCommand | ReleaseWaveCommand + LifecycleTransition | Wave | b | sipariş / görev sayısı, `RouteSummary`, sınır onayı, hariç kalanlar, açılan görev numaraları (0178 yazımları bu kayda enlist) | — |
| CompleteWaveReleaseCommand | CompleteWaveReleaseCommand + Execute | Wave | b | kurtarma sonucu (açılan / hariç / replay sayıları); yalnız yazacak şey varsa | — |
| RecallWaveRemainingCommand | RecallWaveRemainingCommand + LifecycleTransition | Wave | b | **gerekçe**, çekilen ve başlamış siparişler, iptal edilen görev numaraları | — |
| ResolveWaveExceptionCommand | ResolveWaveExceptionCommand + Execute | WaveException | b | eylem (çıkar / taşı / yok say), hedef dalga, **gerekçe** | — |
| ReconcileWaveCommand | ReconcileWaveCommand + Execute | Wave | b | durum geçişi, `CompletedAt`, açılan / kapanan istisnalar, `OrderCancelled` üyelikler; yalnız yazacak şey varsa gönderilir (0178 sweeper deseni) | — |
| UpdateWaveWarehouseSettingsCommand | UpdateWaveWarehouseSettingsCommand + Update | WaveWarehouseSettings | b | önceki / sonraki: WaveManaged, TimeZoneId, eşikler, **gerekçe**; izin `warehouse.waves.settings.configure` (Q-6 = B); Y-1 reddi (409) başarısızdır, kayıt doğurmaz | — |

**Yazma olmayan:** KPI okuması ve CSV (`ExportWaveKpiCsvQuery`) okumadır, merkezi kayıt doğurmaz (AUD-001 §7 okuma denetimi v1 dışı, Q-18).
Kayda yazılmayacak alan: yok (parola / token / özel nitelikli veri taşınmaz; `ShipToFilter` serbest metindir — KVKK için olay `Data`'sına yazılır,
log / OTel'e yazılmaz).

## 22. Record Codes

| Kayıt türü | Sınıf | TÜR öneki | Üretici | Gerekçe |
|---|---|---|---|---|
| Dalga (`Wave.Number`) | OTOMATİK | `WAV` (D-4) | hedef MOD-0040-FU02; ara MOD-0178 geçici sayacı (Q-4) | yüksek hacimli, kullanıcı değiştirmez |
| Dalga üyeliği | — | kod yok | — | sipariş numarası (`OUT`) gösterilir |
| İstisna | — | kod yok | — | dalga kodu + sipariş + tür ile anılır |

Biçim `{ŞİRKET}-WAV-{000001}`, sayaç (kiracı, şirket öneki, tür) başına, sıfırlanmaz, ASCII; kiracı genelinde tekil (RCS-001 §6). `WAV`'ın RCS-001
§3 tablosuna yazılması korunan yol değişikliğidir (izlenen kapı §6, + X-12 aynı adımda).

## 23. Teslim dilimleri

| dilim | ad | kapsadığı bölümler | gerekli kanıt seviyesi | plan yetenek kimliği | plan MVP |
|---|---|---|---|---|---|
| DL-1 | WMS-WAVE-BUNDLE sözleşme taslağı + dalga çekirdeği: yarat / düzenle / iptal, sipariş ekle / çıkar, öner, sınır uyarıları, kesim saat dilimi, depo ayarı, izin, kiracı, denetim, kayıt kodu (arka uç) | Owned Objects, Entity Fields, Indexes, Repo Scope, Protected Paths, Dependencies, Servis ve kapsam, Dalga kurma ve uygunluk, Durum makinesi, Kesim saati ve saat dilimi, Sınırlar ve uyarılar, İyimser kilit, Merkezi denetim ve yerel iz, Kayıt kodu üretimi, Backend File Convention, Validation Rules, Failure Path to Verify, Authorization Convention, Audited Events, Record Codes | E4 | yok (plan `:78` yetenek kimliği vermez) | MVP-5 |
| DL-2 | serbest bırakma + rota optimizasyonu + MOD-0178 seam'i (görev erteleme, port, kurtarma) | Serbest bırakma ve MOD-0178 seam'i, Rota optimizasyonu, Durum makinesi, Hata kodları, Failure Path to Verify, Audited Events | L3 + E5 | yok (plan `:78` yetenek kimliği vermez) | MVP-5 |
| DL-3 | ilerleme + durum uzlaştırması + istisnalar + riskli dalga + geri çekme | İlerleme ve durum uzlaştırması, Geri çekme ve iptal, İstisnalar, Riskli dalga, Hata kodları, Audited Events | E4 | yok (plan `:78` yetenek kimliği vermez) | MVP-5 |
| DL-4 | ekranlar: pano + ayrıntı, planlayıcı (sürükle-bırak + klavye), istisna kuyruğu; durumlar; 375 px; RTL; 7 dil; yardım | Layout & Shell Contract, Frontend File Contract, Ekranlar ve Blueprint soft pages, Page Help (UXH-001) | E4 | yok (plan `:78` yetenek kimliği vermez) | MVP-5 |
| DL-5 | performans sayfası + CSV | Performans sayfası ve CSV, Frontend File Contract, Audited Events | E4 | yok (plan `:78` yetenek kimliği vermez) | MVP-5 |
| DL-6 | yalnız Gateway kapsama testi (integration-agent; DL-1'den sonra; `ocelot.json` değişmez) | Gateway / API Routing Decision | E3 | yok (plan `:78` yetenek kimliği vermez) | MVP-5 |

## 24. Dilim kapıları ve bağlı kararlar

Paket onaylanmadan (künye `approved` / `ready-for-dev` + CT-APR-02 inceleme kaydı + CT-APR-01 onay satırı, onaylayan Ali — D-17) hiçbir dilim
başlamaz. **Onaydan önce kapanan kapı (Y-4):** `.antigravity/**` satırları (PKS-001 §4, RCS-001 §3) + aynı adımda X-12 ve X-16 (§6).
Sahip kararları (karar 03) bütün dilimler için kapalıdır; aşağıdaki "kararlar" sütunu yalnız açık kalanları ve CT adımlarını sayar.

| Dilim | Başlamadan kapanması gereken kararlar | Seam'ler (XMC-001) | AC | Sabotajlar |
|---|---|---|---|---|
| DL-1 | `.antigravity` kapısı (Y-4, onaydan önce); ilk adım: WMS-WAVE-BUNDLE taslağı; X-15 sahip teyidi (satır adları) | X-6, X-9, X-10, X-11, X-15 | AC-1…AC-17, AC-56 | S-1…S-17 (S-12b, S-15b, S-15c dahil), S-56 |
| DL-2 | **CT adımı: WMS-BUNDLE eklemeli sürümü (X-8) DL-2 başlamadan önce yazılır ve dondurulur; sürüm numarası CT-1** | X-1, X-2, X-3, X-4, X-5, X-8, X-10, X-11 | AC-18…AC-28 | S-18…S-28 (S-19b dahil) |
| DL-3 | X-15 sahip teyidi (`recall` fiili) | X-9, X-10, X-15 | AC-29…AC-38; AC-56 tekrar | S-29…S-38 (S-30b dahil), S-56 |
| DL-4 | AuthService rol şablonu WP'si (Q-20 Ali, X-13) kabul kapısı; X-7 üyeleri X-8 sürümünde değilse DL-4 öncesi CT adımı | X-7, X-8 | AC-39…AC-50 | S-39, S-41, S-42, S-45, S-48, S-49, S-50 |
| DL-5 | — | X-10 (KPI komutu yok — sayı değişmez), X-15 (satır adı) | AC-51…AC-54; AC-56 tekrar | S-51, S-52, S-53, S-54, S-56 |
| DL-6 | — | gateway test projesi (integration-agent); `ocelot.json` değişmez, sabotajda geçici bozulur ve geri alınır | AC-55 | S-55a, S-55b |

Sıra: DL-1 → DL-2 → DL-3 → DL-4 → DL-5; DL-4 geliştirmesi DL-2'den sonra DL-3 ile paralel yürüyebilir ama **DL-4 kabulü DL-3 kabulünden
sonradır** (istisna kuyruğu ve ilerleme ekranları DL-3 uçlarına dayanır); DL-6 DL-1'den sonra her an. MOD-0178 bekçi testleri her dilimde
değişmeden yeşil (AC-18'in `git diff` kanıtı her dilim kabulünde tekrarlanır).

## 25. Page Help (UXH-001)
- `WAVES` → `Help.WAVES.Purpose`, `.Audience`, `.Step1Title`/`.Step1` … `.Step4` (1: depoyu seç, 2: dalga oluştur — kesim saati depo saatiyle,
  3: siparişleri ekle ya da öner, 4: serbest bırak ve ilerlemeyi izle); `.Immediate` ("Serbest bırakılan dalga iptal edilmez; kalan görevler
  gerekçeyle geri çekilir"); `Related.WAVEPLANNER`, `Related.WAVEEXCEPTIONS`.
- `WAVEDETAIL` → `Purpose`, `Audience`, `Step1..3` (ilerlemeyi oku, görevleri ve önerilen yolu incele, olay geçmişine bak); "İlerleme Toplama
  Görevleri'nden okunur" notu.
- `WAVEPLANNER` → `Purpose`, `Audience`, `Step1..3` (soldan siparişi seç, "Ekle" ya da sürükle-bırak — klavyeyle Enter, sınır uyarılarını kontrol
  et); "Gri siparişlerin rezervasyonu bitmedi" notu.
- `WAVEEXCEPTIONS` → `Purpose`, `Audience`, `Step1..3` (istisnayı incele, çıkar / sonraki dalgaya taşı / yok say — gerekçe zorunlu, stok düzeltmesi
  için bağlantıyı kullan); `.Immediate` ("Siparişi çıkarmak açık görevlerini iptal eder").
- `WAVEKPIS` (DL-5) → `Purpose`, `Audience`, `Step1..2` (depo ve tarih aralığını seç — en çok 92 gün, CSV indir).
- Kiracı modülü → 7 dil (D-15). **Tur yok.**
