---
id: MOD-0181
name: Cycle Counting
domain: supply-chain-execution
service: Diten.InventoryService
shell: tenant
golden_reference: slim
data_mode: server
screens:
  - module: CycleCounts
    data_mode: server
  - module: CountVariances
    data_mode: server
  - module: CountEvidenceExports
    data_mode: server
entity_base: EntityBase
status: draft
approval_status: pending
revision: "r1 (2026-11-06) — sahip kararları işlendi (Beste, 2026-11-06): Q-1…Q-24 kapandı (§19.4); Slim/Compact yeniden sayıldı (8 alan → slim, §4.1). Önceki: r0 (2026-11-05) — ilk taslak, sahip kararı Beste 2026-11-05 \"başlatalım\" (MVP-5, merkez). Bağımsız inceleme (CT-APR-02) ve onay satırı (CT-APR-01) ayrı adımdır; bu sürümde onay satırı yoktur."
status_note: "draft — açık kararlar Q-1…Q-24 sahip kararıyla kapandı (r1, §19.4). Kalan: §19.4 'Kalan açık nokta' (PKS-001 §2 daraltma teyidi; Q-22 / Q-7 okunuşu), WMS-COUNT-BUNDLE taslağı, XMC-001 seam onayları (§5), bağımsız inceleme (CT-APR-02) ve onay satırı (CT-APR-01). Paket onaylanmadan (approved / ready-for-dev + onay satırı) geliştirme başlamaz."
owner: supply-chain-execution / merkez (Beste) — MVP-5
branch: feature/sce/mod-0173-inventory-ledger-valuation
started: 2026-11-05
target: TBD
form_field_count: 8
dependencies: [MOD-0173, MOD-0173-FU02, MOD-0173-FU03, MOD-0174, MOD-0175, MOD-0175-FU01, MOD-0176, MOD-0177, MOD-0178 (S1 — lokasyon rolleri okuması, /api/warehouse rota ailesi onayı, geçici kayıt kodu portuna CNT türü; Q-7 = B, Q-15 = A; görev modeli kullanılmaz — Q-22), MOD-0040-FU02 (RCS-001 kayıt kodu servisi; Q-7 = B geçişi), MOD-0018 (RBAC), BL-501 (merkezi denetim), MOD-0290 (PRODUCT-MASTER), INVENTORY-BUNDLE, LOCATION, TRACE, WMS-COUNT-BUNDLE (taslak yok — bu paketin sözleşmesi), DEV-0000]
---

# MOD-0181 — Cycle Counting (Sayım)

> **Taslak (r1, 2026-11-06).** Bu paket `status: draft`, `approval_status: pending`'dir. **Paket onaylanmadan geliştirme başlamaz:**
> kod yalnız `approved` / `ready-for-dev` künye + bağımsız inceleme kaydı (CT-APR-02) + onay satırı (CT-APR-01) ile yazılır
> (AGENTS.md Çekirdek madde 7). Bu dosyada onay satırı yoktur; onay ayrı adımdır.
>
> **Revizyon notu (r1, 2026-11-06).** Yalnız sahip kararları işlendi (Beste, 2026-11-06); yeni ölçüm yapılmadı, r0 atıfları değişmedi.
> Kapanan kararlar (§19.4): **Q-13** Build (Blueprint `Buy/Partner` sapması W-0181-01, §0); **Q-22** sayım görevi 0181'in kendi kaydı,
> MOD-0178 görev modeli kullanılmaz, MOD-0178 görev koduna dokunulmaz; **Q-6** lokasyon kilidi yok — anlık görüntü + sonraki hareketler
> farka eklenir, depo durmaz; **Q-21** MOD-0173'teki elle `COUNT_GAIN` / `COUNT_LOSS` kalır, izinle sınırlanır (XMC-001 eklemeli MOD-0173
> değişikliği, §2.4); **Q-23** dal `feature/sce/mod-0173-inventory-ledger-valuation`; **Q-1…Q-5, Q-7…Q-12, Q-14…Q-20, Q-24** öneri kabul.
> Etkiler: künye (revision, status_note, branch, dependencies — MOD-0023 / MOD-0022 / MOD-0048 v1 bağımlılığı değil), §0 (W-0181-01),
> §1, §2.2, §2.4 (yeni — Q-21), §3, §4.1 (Slim yeniden sayıldı: 8 → slim), §4.2 (`SerialControlled`), §4.4–§4.6, §5, §6, §7, §8.2, §8.4–§8.9,
> §8.11–§8.15 (`COUNT_LOT_UNKNOWN`, `COUNT_SERIAL_NOT_POSTABLE` öneri kodları), §12, §13, §14, §15, §16 (AC-1, AC-12, AC-13, AC-15, AC-16,
> AC-19, AC-21, AC-22, AC-27, AC-30, AC-32, AC-39, AC-43, AC-45, AC-46 düzeltildi; AC-47, AC-48 eklendi), §17, §18, §19.3, §19.4 (karar sütunu +
> kalan açık nokta), §20 (FU-0181-LOCK düştü, FU-0181-REASON eklendi), §21, §22, §23, §24.
>
> **Yetki:** bu paket > `execution/domains/supply-chain-execution/domain-config.md` > `AGENTS.md` > `.antigravity/`.
>
> **Atıf kısaltmaları.** `INV/` = `services/Diten.InventoryService/src/Diten.InventoryService.`; `INVT/` =
> `services/Diten.InventoryService/tests/Diten.InventoryService.Tests/`; `SCE/` = `execution/domains/supply-chain-execution/module-packs/`.
> Kod atıfları **`c101e22fa`** (MVP-1 kod hattı ucu, `feature/sce/mod-0173-inventory-ledger-valuation`; MOD-0178 DL-4 ve MOD-0173-FU03
> kodu yalnız orada) üzerinde ölçüldü ve `c101e22fa:INV/...:satır` biçiminde yazıldı. Paket atıfları bu worktree'nin HEAD'i
> `dbfc44b69` üzerindedir (SCE paketleri iki uçta aynı — çağıranın bildirimi; bu oturumda ayrıca karşılaştırılmadı).
> **Çıkarım** ve **öneri** olarak işaretlenmeyen her cümle bir kaynağa dayanır; kaynaksız değer yazılmadı (bilinmeyen = TBD + Q-).

## 0. Kimlik ön kontrolü ve keşif

**DCP-002 ön kontrolü (fail-closed) — çağıran oturumda koşuldu, bu oturumda yeniden koşulmadı:**

| Komut | Sonuç |
|---|---|
| `python3 .antigravity/scripts/verify_module_id.py . --check-id MOD-0181 --name "Cycle Counting"` | `OK MOD-0181: proven against Blueprint/registry.` — çıkış 0 |
| `python3 .antigravity/scripts/verify_module_id.py . --discover "Cycle Counting"` | çıkış 3 ("inceleme gerekli"); rapor sha256 `232badb9796e014374bd3ef28eea9e78c9ccadde968a3d1136cb9d1a5d0250cc`; araç sha256 `ae87a566…`; HEAD `dbfc44b69` |

**Keşif bulgularının her birine karar:**

| Bulgu | Karar |
|---|---|
| Blueprint 1 — `MOD-0181 'Cycle Counting'`, veri nesnesi count events | **Bu paketin kaynağı.** Blueprint 8.8 `Blueprint_Data` satır 182 (aşağıda) |
| Registry 1 — `MOD-0181` reserved / planned | **Bu paketin kaynağı.** `execution/registries/module-id-registry.md:315` |
| Paket 1 — `execution/domains/master-data-management/module-packs/MDM-060-cycle-counting.md` | MDM-060 — eski taslak, yanlış domain, onaysız — devralınmadı. |
| Gateway 0, görev 0 | Karar gerekmez; Gateway kararı §15 (Q-15) |

**Blueprint 8.8 satır 182 (bu oturumda xlsx'ten okundu, sütun = başlık):** A Module ID `MOD-0181` · C Suite `Supply Chain Execution Suite` ·
D Capability Group `Warehouse (WMS)` · E Aim "Optimize warehouse operations via standardized events/contracts and exception-centric
execution." · F Module Name `Cycle Counting` · G Wave `W-4` · H Dependency Gate `Inventory Ledger & Valuation` · I Outcome `Accuracy uplift` ·
J Soft Pages `Cycle Count Dashboard; Variance Trace; Evidence Pack` · K Placement `Domain App (WMS)` · L / AC / AD Min Integration Contract
`WMS-COUNT-BUNDLE (count tasks, variance schema, approvals optional, evidence export)` · M SoR `count events, variances, adjustments proposals` ·
O `DOMAIN-APP-BASE` · Q Build/Buy `Buy/Partner` · R SLO `Tier 2` · V SoR Primary Object `count events` · Y AI Tier `Assist` ·
Z AI Use Cases `Summarize; Extract; Recommend; Route` · AA AI Gate `Prompt Registry; HITL; Model Registry; Eval/Drift; Logging` · AB AI Risk `Medium`.

**Fark notu (uydurma yok):** görev metnindeki "Core / Physical count and governed adjustment" ifadesi Blueprint 8.8 satır 182'nin hiçbir
hücresinde yoktur (A…AG okundu). "Core" yalnız `docs/analysis/inventory-capability-scope-and-dependency-report.md:102`'deki
"Core/Cond." sütunundadır (tablo başlığı `:98`); "Physical count and governed adjustment" depoda 0 eşleşme (`git grep`, docs + execution).
Bu paket kapsamı Blueprint hücrelerinden (SoR: count events, variances, adjustments proposals) türetir.

**Blueprint sapması (W-0181-01) — Build/Buy:** Blueprint 8.8 satır 182 Q sütunu MOD-0181'i `Buy/Partner` işaretler. **Sahip kararı
(Beste, 2026-11-06, Q-13): Build — içeride yazılır.** Gerekçe: stok defteri (MOD-0173) bizde; dış bir ürün bu defterle konuşamaz
(0181'in tek stok kapısı süreç içi `InternalMovementPostingService`'tir, §2.3 kural 2). Bu paket Build'dir ve Blueprint'i değiştirmez;
Blueprint düzeltmesi EA'ya açık iştir (FU-0181-EA). Emsal: MOD-0178 de Blueprint'te `Buy/Partner`'dır ve sahip kararı Build'dir
(`SCE/MOD-0178-putaway-picking-packing.md:96-97`, W-1).

**Diğer kimlik kaynakları:** `execution/portfolio/delivery-capability-packs/DCP-009-supply-chain-inventory.md:16` (`canonical_modules`
MOD-0181'i içerir); `execution/domains/supply-chain-execution/domain-config.md:27` (WMS listesi); plan
`docs/roadmap/plans/integration-phase-and-mvp5-start-plan-2026-10-21.md:78` ("MOD-0181 Sayım (W-4, yalnız 0173'e bağlı)") ve `:124`
("MOD-0181 sayım (0178 ile paralel)"); `SCE/MOD-0178-putaway-picking-packing.md:136` (0181 0178 ile paralel yürüyebilir).

## 1. Module Summary

MOD-0181, depodaki **fiziksel sayımı** ve sayımın ortaya çıkardığı **farkın yönetişimli düzeltmesini** tutar: sayım planı (kapsam:
depo + lokasyon düğümleri + isteğe bağlı SKU süzgeci), kapsamın **beklenen miktar anlık görüntüsü** (defterden okunur, saklanan kopya
yalnız kanıttır), sayım girişi, fark hesabı, fark onayı ve onaylanan farkın **MOD-0173 defterine** `COUNT_GAIN` / `COUNT_LOSS` hareketi
olarak idempotent anahtarla yazılması. Blueprint SoR nesneleri: **count events** (sayım kaydı ve zaman çizelgesi), **variances** (satır
farkları), **adjustment proposals** (satırın düzeltme önerisi ve posting durumu) — Blueprint 8.8 satır 182 M / V.

**Temel sınır:** sayım **ikinci bakiye tutmaz** (MOD-0178 AC-6 deseni, `SCE/MOD-0178-putaway-picking-packing.md:2171`). Stok gerçeği
MOD-0173'tedir (`domain-config.md` "Stok gerçeği tek SoR = MOD-0173"); 0181 stoğu yalnız defterin olağan kayıt yolundan
(`InternalMovementPostingService`, `c101e22fa:INV/Application/Features/Inventory/Services/InternalMovementPostingService.cs:35,82`) değiştirir.

**Sayım sırasında depo durmaz (Q-6, sahip kararı 2026-11-06):** lokasyon kilidi yoktur; `release` anında beklenen miktarın anlık görüntüsü
alınır, anlık görüntüden sonra kapsamda olan hareketler farka eklenir (§8.4), mal kabul / sevk / transfer sürer.

Hedef kullanıcı: **sayan** (depo operatörü — sayım formunu doldurur, 375 px; Q-12), **sayım planlayıcı / depo sorumlusu** (sayım açar,
anlık görüntü alır, atar, iptal eder), **fark onaylayıcı** (modül içi onay, dört göz; onaylar, reddeder, yeniden sayım ister; Q-4 / Q-5),
**denetçi / kalite** (Variance Trace, Evidence Pack). Kiracı modülüdür (`_LayoutTenantShell`, 7 dil).

## 2. Ownership and Boundaries

### 2.1 Kapsam
- `CycleCount` sayım kaydı (plan + durum), `CycleCountLine` satırları (beklenen anlık görüntü, sayılan miktar, fark, düzeltme önerisi ve
  posting durumu), `CycleCountEvent` (yalnız ekleme yapılan zaman çizelgesi), şirket ayarları `CycleCountSettings` (eşik — Q-3),
  `CountEvidenceExport` (DL-5).
- Fark onayı, ret, yeniden sayım; onaylanan farkın defter hareketi (`COUNT_GAIN` / `COUNT_LOSS`) ve kurtarma (Pending → Posted / Refused /
  IntegrityError).
- Ekranlar: Cycle Count Dashboard, sayım formu, Variance Trace, Evidence Pack (Blueprint soft pages — §8.13).
- `WMS-COUNT-BUNDLE` sözleşmesi (taslak bu paketin işi; bugün yok — `git grep WMS-COUNT` docs + execution: 0 eşleşme).

### 2.2 Kapsam dışı
| Şey | Sahibi | 0181 nasıl dokunur |
|---|---|---|
| Stok bakiyesi, hareket, rezervasyon | MOD-0173 / MOD-0173-FU03 | bakiyeyi ve hareketleri süreç içi **okur**; hareketi yalnız kendi poster'ı → `InternalMovementPostingService` ile yazar; rezervasyon yaratmaz / bırakmaz |
| Lokasyon ağacı | MOD-0173-FU02 | okur; düğüm yaratmaz |
| Lot / seri kaydı | MOD-0174 | okur; lot / seri yaratmaz (Q-10) |
| Karantina / bloke kararı | MOD-0175 | karar vermez; tutulan kovaların farkını v1'de post etmez (Q-8, karar 2026-11-06) |
| FEFO / son kullanma, recall | MOD-0176 / MOD-0177 | kural eklemez; `COUNT_*` tür olarak muaftır (§8.10) |
| Depo görevleri (putaway / pick), lokasyon rolleri | MOD-0178 | sayım görevi 0181'in kendi kaydıdır (`wms_count_*`); MOD-0178 görevi (`WarehouseTask` / `wms_tasks`) **kullanılmaz**, `Count` türü eklenmez, MOD-0178 görev koduna dokunulmaz (Q-22, karar 2026-11-06); lokasyon rolleri yalnız okunur |
| Lokasyon kilidi | — | **yok** (Q-6, karar 2026-11-06): 0181 defter posting'ini bin bazında durdurmaz, MOD-0173'e kilit seam'i eklenmez |
| Barkod / tarama | MOD-0182 (MOD-0365'e bağlı — plan `:78`) | v1'de yok; opak `scanRef` kapısı (Q-12, karar 2026-11-06) |
| Dalga planlama | MOD-0180 | yok |
| Değerleme / maliyet etkisi | MOD-0173 (değerleme) | v1'de yok (Q-19, karar 2026-11-06) |
| AI Assist (Summarize / Extract / Recommend / Route) | Blueprint AI kapıları | v1'de yok (Q-14, karar 2026-11-06); FU-0181-AI |
| ABC sınıfı ve otomatik döngü üretimi | TBD (ABC verisinin sahibi ölçülmedi) | v1'de elle planlama (Q-2, karar 2026-11-06); FU-0181-ABC |
| Onay iş akışı / e-imza | MOD-0023 / MOD-0022 | v1'de kullanılmaz: onay modül içidir (Q-4), e-imza yok (Q-5) |
| MOD-0173 elle `COUNT_GAIN` / `COUNT_LOSS` girişi | MOD-0173 | kalır; izinle sınırlanır — MOD-0173'e eklemeli değişiklik, 0181 kodu değil (Q-21, §2.4) |

### 2.3 Sınır kuralları
1. **İkinci bakiye yok.** 0181'in hiçbir varlığı `OnHand` / `Available` / `Reserved` adlı alan taşımaz; servis genelindeki mimari test
   bu adları yalnız defter projeksiyonlarına izin verir (`c101e22fa:INVT/Architecture/SingleSystemOfRecordArchitectureTests.cs:124-142`).
   Satırdaki `ExpectedQuantity` bir **sayım kanıtıdır**: anlık görüntü anında bir kez yazılır, sonra hiçbir hareketle güncellenmez,
   başka modül tarafından okunmaz, stok kararı vermez (AC-3, AC-4).
2. **Stok yalnız defterin olağan yolundan değişir.** 0181 kodu `IInventoryLedgerRepository`'ye, `inventory_transactions` /
   `inventory_balances` adlarına referans vermez; okumalar Persistence katmanında `InventoryCollections.*` sabitleriyle **yalnız `Find`**
   yapar (emsal `c101e22fa:INV/Persistence/Warehouse/WarehouseStockReads.cs:14-44`, `c101e22fa:INV/Persistence/Quarantine/QuarantineLedgerReads.cs:126-136`);
   tek yazma kapısı `CycleCountMovementPoster` → `InternalMovementPostingService` (emsal `c101e22fa:INV/Application/Features/Warehouse/Services/WarehouseMovementPoster.cs:14-21`).
3. **Fark mutlak değil, delta olarak** post edilir: hareket miktarı = |fark|; bakiye "sayılana eşitlenmez". Böylece onay ile posting
   arasındaki sonraki hareketler düzeltmeyi bozmaz (§8.4).
4. **Aynı doğrulamalar defterindir:** negatif stok, rezervasyon kapsamı (FU03), lot durumu, seri sayısı, idempotency kuralları
   INVENTORY'nindir; 0181 onları yeniden yazmaz, ret kodunu olduğu gibi iletir (AC-28).
5. Her okuma ve yazma kiracı + şirket (`TenantId` + `LegalEntityId`, sunucuda çözülür) kapsamındadır (`domain-config.md` DEC-INV-18).
6. 0181 yalnız `COUNT_GAIN` ve `COUNT_LOSS` yazar; başka hareket türü yazmaz (AC-29).

### 2.4 MOD-0173 elle `COUNT_*` girişinin izinle sınırlanması (Q-21)
**Karar (sahip, Beste, 2026-11-06):** MOD-0173'ün elle hareket türleri arasındaki `COUNT_GAIN` / `COUNT_LOSS`
(`SCE/MOD-0173-inventory-ledger-valuation.md:714-717`, "Manual movement types (7)") 0181'den sonra **kalır**, ama **ayrı bir izinle
sınırlanır**.

- **Bugün (ölçüm `c101e22fa`):** elle hareket tek uçtan girer — `POST /api/inventory/movements`,
  `[HasPermission(InventoryPermissions.CreateMovements)]` = `inventory.movements.create`
  (`c101e22fa:INV/Api/Controllers/InventoryController.cs:27-28`; sabit `c101e22fa:INV/Infrastructure/Authorization/InventoryPermissions.cs:14`).
  Hareket türü gövdededir; `COUNT_*` için ayrı izin yoktur.
- **İzin anahtarı (öneri):** `inventory.count-adjustments.create` — namespace `inventory.*` MOD-0173'ündür (PKS-001 §4;
  `SCE/MOD-0173-inventory-ledger-valuation.md:267`); eylem Tier-1 `create` (PKS-001 §5), kaynak `count-adjustments`. 0181 bu anahtarı
  **bildirmez ve tanımlamaz**; anahtarın kesin adı ve kaydı MOD-0173 paketinin revizyonundadır (PKS-001 §4 "tek sahip").
- **Davranış:** `POST /api/inventory/movements` gövdesinde `movementType` `COUNT_GAIN` ya da `COUNT_LOSS` ise çağıran
  `inventory.movements.create` **ve** `inventory.count-adjustments.create` taşımalıdır; yoksa 403, hareket ve ret satırı yazılmaz. Diğer
  türler değişmez. Denetim **yalnız HTTP elle giriş yolundadır**: 0181'in süreç içi yolu (`CycleCountMovementPoster` →
  `InternalMovementPostingService`) bu izni istemez — izin 0181'in kendi `cycle-count.variances.approve` / `.post` kapısıyla zaten
  yönetilir (§14). MOD-0173-FU01 "New movement" ekranı izin yoksa `COUNT_*` türlerini listelemez (UAS-001).
- **Bu bir MOD-0173 değişikliğidir (XMC-001, eklemeli):** dokunulacak dosyalar `InventoryPermissions.cs` (yeni sabit),
  `InventoryController.cs` / hareket isteği yolu (tür bazlı ek denetim), `InventoryLedgerManifestProvider.cs` (anahtar bildirimi),
  MOD-0173-FU01 yeni hareket ekranı (tür listesi) ve AuthService rol şablonu (anahtarın hangi role gideceği — Ali (Auth), ayrı WP).
  **Sahibi: MOD-0173 → MVP-1 CT (Beste).** Dokunmadan önce sahibine sorulur ve kabul kaydına yazılır; 0181 dilimi bu dosyaları
  sahip onayı olmadan değiştirmez (§5 seam tablosu, AC-47).
- **PKS-001 §2 (değişmez anahtar) notu:** ek izin, `inventory.movements.create`'in tek başına verdiği erişimi `COUNT_*` için daraltır.
  Anahtar "yayımlanmış" sayılıyorsa (seed / rol satırı / JWT / gönderilmiş `[HasPermission]`) daraltma PKS-001 §2'ye göre ayrı sahip
  kararı ister; bu durum ölçülmedi (§19.4 "Kalan açık nokta").

## 3. Owned Objects

| Nesne | Dilim | Koleksiyon (öneri) | Not |
|---|---|---|---|
| `CycleCount` | DL-1 | `wms_count_counts` | Değişebilir baş kayıt, `Version` (If-Match); durum makinesi §8.3 |
| `CycleCountLine` | DL-1 (anlık görüntü), DL-2 (giriş, fark), DL-3 (düzeltme) | `wms_count_lines` | Satır başına bir anahtar: (lokasyon, SKU, seviye, lot, stok statüsü); gömülü `Adjustment` |
| `CycleCountEvent` | DL-1 | `wms_count_events` | Insert-only zaman çizelgesi; yerel GxP izi (§8.11) |
| `CycleCountSettings` | DL-3 (Q-3) | `wms_count_settings` | Şirket başına tek kayıt: tolerans, yeniden sayım sınırı, satır sınırı |
| `CountEvidenceExport` | DL-5 | `wms_count_evidence_exports` + `wms_count_evidence_files` | Insert-only; expiry / recall kanıt dışa aktarımı emsali |
| Koleksiyon sabitleri | DL-1 | `CycleCountCollections` (0181 Persistence) | `wms_` öneki MOD-0178'in `WarehouseCollections`'ıyla **paylaşılmaz**; ayrı sınıf (Q-22: sayım 0181'in kendi kaydı, `wms_tasks` kullanılmaz) |

Koleksiyon öneki `wms_count_*` önerisidir (Blueprint Placement "Domain App (WMS)"); `wms_` adları mimari testin süzgecinde zaten
tanınır (`c101e22fa:INVT/Architecture/SingleSystemOfRecordArchitectureTests.cs:104`). Kesin ad DL-1'in ilk ölçümüyle sabitlenir.

**Komutlar (yazma, 17):** `CreateCycleCountCommand`, `UpdateCycleCountCommand`, `DeleteCycleCountCommand`, `BulkDeleteCycleCountCommand`,
`ReleaseCycleCountCommand` (anlık görüntü), `CancelCycleCountCommand`, `RecordCountLineCommand`, `AddCountLineCommand` (bulunan stok),
`SubmitCycleCountCommand`, `ApproveCountVarianceCommand`, `RejectCountVarianceCommand`, `RequestCountRecountCommand`,
`RetryCountAdjustmentCommand`, `ResolveCountAdjustmentIntegrityCommand`, `ReconcilePendingCountAdjustmentCommand` (yalnız sweeper),
`UpdateCycleCountSettingsCommand` (Q-3), `CreateCountEvidenceExportCommand` (DL-5).

**Sorgular:** `GetCycleCountListQuery`, `GetCycleCountByIdQuery`, `GetCycleCountLineListQuery`, `GetCycleCountEventListQuery`,
`GetCycleCountDashboardQuery`, `GetCountVarianceListQuery`, `GetCountVarianceByIdQuery`, `GetCycleCountSettingsQuery`,
`GetCountEvidenceExportListQuery`, `GetCountEvidenceExportFileQuery`.

**API (taban `/api/warehouse` — Q-15 = A, karar 2026-11-06; yol adları sözleşme taslağında kesinleşir):**
`GET/POST /cycle-counts` · `GET/PUT/DELETE /cycle-counts/{countId}` · `POST /cycle-counts/bulk-delete` · `POST /cycle-counts/{countId}/release` ·
`POST /cycle-counts/{countId}/cancel` · `GET /cycle-counts/{countId}/lines` · `POST /cycle-counts/{countId}/lines` ·
`PUT /cycle-counts/{countId}/lines/{lineId}` · `POST /cycle-counts/{countId}/submit` · `GET /cycle-counts/{countId}/events` ·
`GET /cycle-counts/dashboard` · `GET /count-variances` · `GET /count-variances/{lineId}` · `POST /count-variances/{lineId}/approve` ·
`POST /count-variances/{lineId}/reject` · `POST /count-variances/{lineId}/recount` · `POST /count-variances/{lineId}/retry` ·
`POST /count-variances/{lineId}/resolve-integrity` · `GET/PUT /count-settings` · DL-5: `POST /cycle-counts/{countId}/evidence-exports` ·
`GET /count-evidence-exports` · `GET /count-evidence-exports/{exportId}/file`.

**Frontend rotaları:** `/Warehouse/CycleCounts` (Cycle Count Dashboard), `/Warehouse/CycleCounts/Sheet/{countId}` (sayım formu, nav değil),
`/Warehouse/CountVariances` (Variance Trace), `/Warehouse/CountEvidenceExports` (Evidence Pack, DL-5).

**İzinler:** §14.

## 4. Entity Fields

Tüm varlıklar InventoryService `EntityBase`'inden türer: `Id`, `TenantId`, `LegalEntityId`, `CreatedAt`, `UpdatedAt`, `Version` (int),
`IsDeleted`, `DeletedAt` (`c101e22fa:INV/Domain/Entities/EntityBase.cs:9-27`). `TenantId` / `LegalEntityId` sunucuda çözülür; gövde ya da
sorguda gelirse 400 (MOD-0178 §4 deseni). Kullanıcı alanı `EntityBase`'de yoktur; gereken varlık kendi alanını taşır (`CreatedBy`).
Miktarlar SKU'nun **baz biriminde** decimal string (float yok, en çok 6 ondalık). Zamanlar UTC.
**Alan adı yasağı:** hiçbir 0181 varlığında `OnHand`, `Available`, `Reserved` adlı özellik yok (§2.3 kural 1).

### 4.1 CycleCount
| Alan | Tip | Kural |
|---|---|---|
| `Number` | string | Görünen kod `{ŞİRKET}-CNT-{000001}` (RCS-001, OTOMATİK; üretici MOD-0178 geçici portu → MOD-0040-FU02, Q-7 = B); kiracı genelinde tekil `(TenantId, Number)`; değiştirilemez |
| `WarehouseId` | uuid | LOCATION `Warehouse`, `Active`, şirket kapsamında — **form alanı 1** |
| `CountType` | enum | `Cycle` (seçili kapsam) \| `Full` (deponun bütün stoklanabilir düğümleri); `Spot` türü yok — Q-1 = A — **form alanı 2** |
| `Blind` | bool | kör sayım: beklenen miktar submit'e kadar hiçbir cevapta yok (§8.2); `CountType`'tan ayrı bayrak — Q-1 = A — **form alanı 3** |
| `ScopeLocationIds` | uuid[] | `Cycle`'da 1..200 düğüm (`StorageLocation` \| `Zone` \| `Bin`), her biri `WarehouseId`'nin yolunda; `Full`'da boş — **form alanı 4** |
| `ScopeSkuIds` | uuid[] | 0..200; boş = kapsamdaki bütün SKU'lar — **form alanı 5** |
| `PlannedDate` | date | ≥ bugün (UTC) — **form alanı 6** |
| `AssignedTo` | uuid \| null | sayan kullanıcı; kullanıcı dizini okuması (MOD-0178 `IWarehouseAssigneeDirectory` emsali; 0181 kendi portunu açar) — **form alanı 7** |
| `Note` | string \| null | ≤ 500 — **form alanı 8** |
| `Status` | enum | §8.3 |
| `SnapshotAt` | datetime \| null | `Release`'te yazılır, sonra değişmez |
| `ScopeBinIds` | uuid[] | `Release`'te kapsam düğümlerinin stoklanabilir alt ağacından hesaplanır, sonra değişmez |
| `RecountOfId` | uuid \| null | yeniden sayımda kaynak sayım (§8.5) |
| `CreatedBy`, `SubmittedBy`, `SubmittedAt`, `CancelledBy`, `CancelledAt`, `CancelReason` | — | sunucu yazar |
| `IdempotencyKey`, `RequestFingerprint` | string | yaratma isteğinin anahtarı (8–100) — tekil `(TenantId, LegalEntityId, CreatedBy, IdempotencyKey)` (MOD-0178 §4.4 deseni) |

**form_field_count = 8** (yukarıdaki kalın sekiz alan) → `golden_reference: slim` (≤ 8; `module-pack-standard.md` §3).
**Slim / Compact kararı (r1, yeniden sayıldı):** Q-1 = A (`Cycle` + `Full`, `Blind` ayrı bayrak) `Blind`'ı ayrı alan olarak tutar; Q-8 = A
(yalnız `AVAILABLE`) "tutulan stoğu da say" alanı eklemez. Kalan kararların hiçbiri yaratma formuna alan eklemez (Q-3 / Q-11 / Q-24
şirket ayarıdır, form değil; Q-6 kilit alanı yok). Sonuç: **8 alan → slim, değişmedi.** Sayı sınırdadır: yaratma formuna eklenecek her yeni
alan compact'a geçiş ve paket revizyonu demektir (§19.3).

### 4.2 CycleCountLine
| Alan | Tip | Kural |
|---|---|---|
| `CountId` | uuid | baş kayıt |
| `LocationId`, `WarehouseId` | uuid | bin düzeyi |
| `ItemId`, `SkuId`, `SkuLevel` | — | PRODUCT-MASTER kimliği |
| `LotId` | string \| null | lot kontrollü SKU'da zorunlu; `Found` satırda MOD-0174'te kayıtlı lot olmalı (Q-10 = A) |
| `StockStatus` | enum | v1'de yalnız `AVAILABLE` satır olur ve post edilir (Q-8 = A) |
| `BaseUomId` | string | baz birim |
| `Origin` | enum | `Snapshot` (anlık görüntüden) \| `Found` (sayımda bulunan; beklenen 0) |
| `ExpectedQuantity` | decimal string | anlık görüntüde bir kez yazılır; `Found` satırda `0`; **güncellenmez** |
| `CountedQuantity` | decimal string \| null | ≥ 0; `Counted` ile yazılır |
| `CountedBy`, `CountedAt` | — | sunucu yazar; `CountedAt` = girişin sunucu zamanı |
| `MovementsSinceSnapshot` | decimal string \| null | `Submit`'te hesaplanır: aynı anahtarda `SnapshotAt` → `CountedAt` arası net defter hareketi (§8.4) |
| `ExpectedAtCount` | decimal string \| null | `ExpectedQuantity + MovementsSinceSnapshot` |
| `VarianceQuantity` | decimal string \| null | `CountedQuantity − ExpectedAtCount` |
| `MovementDuringCount` | bool | §8.4 bayrağı (Q-6 = B) |
| `SerialControlled` | bool | `Release` / `Add`'de PRODUCT-MASTER'dan yazılır; `true` ise fark v1'de post edilmez (Q-10 = A, §8.9) |
| `Status` | enum | §8.3 satır durumları |
| `Decision` | gömülü \| null | `{ Kind: Approved \| Rejected \| Recount, By, At, Reason }` |
| `Adjustment` | gömülü \| null | `{ MovementType: COUNT_GAIN \| COUNT_LOSS, Quantity, IdempotencyKey, Status: Pending \| Posted \| Refused \| IntegrityError, TransactionId?, TransactionNumber?, RefusalCode?, RefusalDetails?, TransactionDate, PostingDate, Attempts, LastAttemptAt }` |
| `ScanRef` | string \| null | opak; MOD-0182 doldurur, v1 doğrulamaz (Q-12 = A) |
| `Version` | int | If-Match |

### 4.3 CycleCountEvent
Insert-only (`wms_count_events`). Alanlar: `CountId`, `LineId?`, `Seq` (sayım içinde artan), `Kind` (`Created`, `Updated`, `Deleted`,
`Released`, `Cancelled`, `LineCounted`, `LineAdded`, `Submitted`, `VarianceComputed`, `Approved`, `Rejected`, `RecountRequested`,
`AdjustmentPending`, `AdjustmentPosted`, `AdjustmentRefused`, `AdjustmentIntegrityError`, `IntegrityResolved`, `Closed`), `ActorUserId`,
`At`, `RequestOp`, `IdempotencyKey?`, `RequestFingerprint?`, `Data` (önceki / sonraki değerler, ret kodu, `TransactionNumber`).
İlgili iş yazımıyla **aynı Mongo işleminde** yazılır; yazılamazsa iş yazımı geri alınır; silinmez (MOD-0178 §8.8 "Yerel GxP izi" deseni).

### 4.4 CycleCountSettings
Şirket başına tek kayıt (Q-3 = A): `ToleranceAbsolute` (decimal string, varsayılan `0`), `TolerancePercent` (0–100, varsayılan `0`),
`MaxRecounts` (varsayılan 2, Q-11 = A), `MaxLinesPerCount` (varsayılan 2000, ayarlanabilir, Q-24 = A), `Version`. Kayıt yoksa varsayılanlar
geçerlidir (her fark onay ister). Değer eşiği alanı yok (Q-3 = A, Q-19 = A).

### 4.5 CountEvidenceExport
DL-5. `CountId`, `Format` (`Pdf` \| `Csv` — Q-18 = A), `RequestedBy`, `RequestedAt`, `FileId`, `Sha256`, `Status` (`Ready` \| `Failed`). Insert-only; dosya
baytları ayrı koleksiyonda (emsal `expiry_evidence_exports` / `expiry_evidence_files`, `c101e22fa:INVT/Architecture/SingleSystemOfRecordArchitectureTests.cs:47-50`).

### 4.6 Indexes
Kiracı + şirket önce (MOD-0178 §4.6 deseni):
- `wms_count_counts`: tekil `(TenantId, Number)`; tekil `(TenantId, LegalEntityId, CreatedBy, IdempotencyKey)` (kısmi, anahtar varsa);
  `(TenantId, LegalEntityId, Status, PlannedDate)`; `(TenantId, LegalEntityId, WarehouseId, Status)`.
- `wms_count_lines`: `(TenantId, LegalEntityId, CountId)`; tekil `(TenantId, LegalEntityId, CountId, LocationId, SkuId, SkuLevel, LotId, StockStatus)`;
  tekil kısmi `(TenantId, LegalEntityId, Adjustment.IdempotencyKey)`; `(TenantId, LegalEntityId, Status, Adjustment.Status)` (Variance Trace,
  sweeper); açık sayım çakışması için `(TenantId, LegalEntityId, LocationId, CountOpen)` (Q-6 = B / AC-13 — biçim geliştiricinin ilk ölçümü).
- `wms_count_events`: `(TenantId, LegalEntityId, CountId, Seq)` tekil.
- `wms_count_settings`: tekil `(TenantId, LegalEntityId)`.

## 5. Repo Scope

| Yol | Ne | Dilim |
|---|---|---|
| `services/Diten.InventoryService/src/Diten.InventoryService.Domain/CycleCount/{Entities,Enums,Rules,Repositories}/` | varlıklar, durum / fark / tolerans kuralları (saf) | DL-1, DL-2, DL-3 |
| `services/Diten.InventoryService/src/Diten.InventoryService.Application/Features/CycleCount/` | ortak: `CycleCountContract.cs`, `CycleCountErrorCodes.cs`, `CycleCountCentralAudit.cs`, `Ports/`, `Services/` (poster, anlık görüntü, fark hesabı, posting motoru) | DL-1…DL-3 |
| `…/Application/Features/CycleCounts/` | Golden Slim: Commands, Queries, Handlers/CommandHandlers, Handlers/QueryHandlers, Validators, `CycleCountModels.cs` | DL-1, DL-2 |
| `…/Application/Features/CountVariances/` | aynı yapı (onay / ret / yeniden sayım / retry / resolve / ayarlar) | DL-3 |
| `…/Application/Features/CountEvidenceExports/` | aynı yapı | DL-5 |
| `services/Diten.InventoryService/src/Diten.InventoryService.Persistence/CycleCount/` | depolar, `CycleCountCollections`, index yapılandırması, defter okumaları (`Find`), sweeper hosted service | DL-1…DL-3 |
| `services/Diten.InventoryService/src/Diten.InventoryService.Api/Controllers/CycleCount/` | `CycleCountsController`, `CountVariancesController`, `CountEvidenceExportsController` | DL-1…DL-3, DL-5 |
| `services/Diten.InventoryService/src/Diten.InventoryService.Api/CycleCount/` | `CycleCountingManifestProvider`, `CycleCountServiceCollectionExtensions` | DL-1 |
| `services/Diten.InventoryService/src/Diten.InventoryService.Infrastructure/Authorization/CycleCountPermissions.cs` | izin sabitleri | DL-1 |
| `services/Diten.InventoryService/tests/Diten.InventoryService.Tests/CycleCount/` | birim, entegrasyon, mimari testler | her dilim |
| `frontend/Diten.Web/Views/Warehouse/{CycleCounts,CountVariances,CountEvidenceExports}/`, `frontend/Diten.Web/Controllers/`, `frontend/Diten.Web/wwwroot/assets/js/Warehouse/…`, `frontend/Diten.Web/Resources/Views/Warehouse/…` | ekranlar, proxy controller'lar, JS, resx (7 dil) | DL-4, DL-5 |
| `docs/analysis/contracts/drafts/wms-count-bundle-v1-proposal.md` | WMS-COUNT-BUNDLE taslağı (contract-first; dondurma ayrı CT adımı) | DL-1 öncesi / ilk adım |

**Başka modülün dosyalarına yalnız eklemeli seam'ler (XMC-001 — her biri dokunmadan önce sahibine sorulur):**

| Dosya (ölçüm `c101e22fa`) | Bugün | Gereken ekleme | Sahip | Dilim |
|---|---|---|---|---|
| `INVT/Architecture/SingleSystemOfRecordArchitectureTests.cs` | `:68-80` koleksiyon bildirimleri; `:84-121` bildirilmemiş koleksiyon yasağı | `CycleCountCollections.All` bildirimi ve `declared` listesine ekleme; `Only_the_ledger…` listesi (`:138-141`) **değişmez** | MOD-0173 (MVP-1 CT, Beste) | DL-1 |
| `INVT/CentralAudit/CentralAuditDeclarationTests.cs` | `:41` `Assert.Equal(87, …)`; `:48-56` manifest sağlayıcı listesi; `:116` `Assert.Equal(13, seen)` | sayı 87 + dilimin 0181 yazma komutları; listeye `CycleCountingManifestProvider`; `:116` 0181 `WithTransactionAsync` callback'leri kadar | MOD-0173 (MVP-1 CT, Beste) | DL-1, DL-2, DL-3, DL-5 |
| `INVT/Expiry/ExpiryQuarantineTypeExemptionTests.cs` | `:25-39` poster çağrı yerleri satırla sabit; `:118-134` `InternalMovementPostingService`'in tek kullanıcıları (0175 + 0178) | 0181 için `Mod0181StockDoor` ikilisi (çağrı yeri + poster dosyası) — 0175 / 0178 girdileri değişmez | MOD-0176 (MVP-1 CT, Beste) | DL-3 |
| `INV/Api/Program.cs` | `:46-59` manifest kayıtları | `CycleCountingManifestProvider` + `AddCycleCount()` (kompozisyon kökü; SHA ile sabitlenmez — standard §13) | MOD-0173 (MVP-1 CT, Beste) | DL-1 |
| `INV/Infrastructure/Middleware/InventoryScopeResolutionMiddleware.cs` | `:57-61` `ScopedBasePaths` (`/api/warehouse` dahil) | **değişmez** — Q-15 = A (karar 2026-11-06): uçlar `/api/warehouse` altında, mevcut taban kapsar | MOD-0173 (MVP-1 CT, Beste) | — |
| `INV/Application/Features/Warehouse/Ports/IWarehouseRecordCodeAllocator.cs` | `:10-17` türler PUT / PCK / OUT / PKG | `CNT` türü (Q-7 = B, karar 2026-11-06); yalnız kayıt kodu portu — MOD-0178 görev modeli / görev kodu değişmez (Q-22) | MOD-0178 (MVP-1 CT, Beste) | DL-1 |
| `INV/Infrastructure/Authorization/InventoryPermissions.cs`, `INV/Api/Controllers/InventoryController.cs` (`:27-28`, hareket isteği yolu), `INV/Api/ModuleRegistration/InventoryLedgerManifestProvider.cs`, MOD-0173-FU01 yeni hareket ekranı | `POST /api/inventory/movements` yalnız `inventory.movements.create` ister | `COUNT_GAIN` / `COUNT_LOSS` için ek izin `inventory.count-adjustments.create` (öneri ad; kayıt MOD-0173 paketinde) — §2.4, Q-21 | MOD-0173 (MVP-1 CT, Beste) | DL-3 |
| `services/Diten.AuthService/**` rol şablonu (`DefaultRolePermissionTemplate`, `EntitlementPermissionSyncService` — MOD-0178 §14.2 ölçümü) | — | sayan / sayım sorumlusu / onaylayıcı rol önerisi (Q-4 = A) + `inventory.count-adjustments.create`'in rolü (Q-21) | AuthService — Ali (Auth) (`SCE/MOD-0178-putaway-picking-packing.md:2108`, `:2915`) | ayrı WP |
| `gateway/**` | `/api/warehouse/{everything}` → 5067 (`c101e22fa:gateway/Diten.ApiGateway/ocelot.json:1153-1161`) | yalnız 0181 uç kapsama testi; `ocelot.json` değişmez (Q-15 = A) | integration-agent | DL-6 |

## 6. Protected Paths
- `.antigravity/**` (kullanıcı onayı; RCS-001 §3 tablosuna `CNT` satırı bu yüzden ayrı sahip onayıdır — Q-7 = B kararı bu satırı yazmaz, ayrı adım).
- `gateway/Diten.ApiGateway/**/ocelot.json` (yalnız integration-agent).
- `services/Diten.Building.Blocks/**` (BL-501 olduğu gibi — MOD-0178 §8.8).
- MOD-0173 defter kodu (`INV/Application/Features/Inventory/**`, `INV/Persistence/Repositories/InventoryLedgerRepository.cs`, `INV/Domain/Rules/**`) —
  0181 **çağırır**, değiştirmez; §5 tablosundaki seam'ler (test / kompozisyon, Q-7 kayıt kodu portu, Q-21 izin) dışında MOD-0173 / 0174 / 0175 /
  0176 / 0177 / 0178 dosyası yok. MOD-0178 görev kodu (`WarehouseTask*`, `wms_tasks`) değişmez (Q-22).
- Donmuş sözleşmeler (`docs/analysis/contracts/*.openapi.yaml`, `record-code-allocate.md`).
- Başka domain servisleri; `services/Diten.AuthService/**` (ayrı WP); Archive; `_Layout.cshtml`.

## 7. Dependencies

| Bağımlılık | Tür | Durum (ölçüm) | Kullanım | Değişiklik | Sahip |
|---|---|---|---|---|---|
| MOD-0173 defter + `InternalMovementPostingService` | HARD (süreç içi) | var — `c101e22fa:INV/Application/Features/Inventory/Services/InternalMovementPostingService.cs:35,82`; idempotency önce (`:96-102`) | `COUNT_GAIN` / `COUNT_LOSS` posting; kilit yok (Q-6) | yok (çağırma); elle `COUNT_*` izni §2.4 (Q-21, XMC) | MVP-1 CT (Beste) |
| MOD-0173 `IPostingKeyLookup` | HARD | var — `c101e22fa:INV/Application/Features/Inventory/Ports/IPostingKeyLookup.cs:6-58` | Pending kurtarma (NotCommitted / Committed / KeyReused) | yok | MVP-1 CT (Beste) |
| MOD-0173 bakiye ve hareket okuması | HARD (okuma) | bakiye `Find` emsali `WarehouseStockReads.cs:14-44`; hareket `Find` emsali `QuarantineLedgerReads.cs:126-136`; konum index'leri `c101e22fa:INV/Persistence/InventoryIndexConfiguration.cs:45,48,74` | anlık görüntü, `MovementsSinceSnapshot` | yok (0181 Persistence'ta yeni okuma) | MVP-1 CT (Beste) |
| MOD-0173 mimari / denetim testleri | test seam | §5 tablosu | koleksiyon bildirimi, denetim sayısı | **yalnız eklemeli, XMC-001** | MVP-1 CT (Beste) |
| MOD-0173-FU02 LOCATION | HARD | canlı (MOD-0178 §7 `:590`) | kapsamı stoklanabilir bin'lere açma | yok | MVP-1 CT (Beste) |
| MOD-0173-FU03 rezervasyon | HARD (davranış) | `COUNT_LOSS` AVAILABLE azalışıdır, ayrılmış stoğu açıkta bırakamaz → 422 `INSUFFICIENT_STOCK` (`SCE/MOD-0173-FU03-inventory-lot-location-reservation.md:385-386`; kod `c101e22fa:INV/Persistence/Repositories/InventoryLedgerRepository.cs:306-330`, `InternalMovementPostingService.cs:198-212`) | ret iletimi; uzlaştırma §8.7 | yok; FU-03-COUNT kararı Q-9 = A — 0181 rezervasyona dokunmaz (`FU03 :1262`) | MVP-1 CT (Beste) |
| MOD-0174 TRACE | HARD | lot seçici yalnız inbound (`SCE/MOD-0174-lot-serial-tracking.md:396`) | lot / seri satırları | yok (Q-10 = A) | MVP-1 CT (Beste) |
| MOD-0175 karantina | davranış | inbound → QUARANTINE / QI hareketi bir hold açar (`c101e22fa:INV/Application/Features/Quarantine/Services/QuarantineIntake.cs:45-61`; `QuarantineLedgerReads.cs:128-131` `COUNT_GAIN` dahil); REVERSAL hold'u `Voided` yapar (`SCE/MOD-0175-FU01-quarantine-hold-source-reversal.md:791`); tutulan kovadan MOD-0175 dışı çıkış hayalet kayıt (`…FU01…:1120-1124`) | Q-8 = A: tutulan kovalar sayılmaz / post edilmez | yok | MVP-1 CT (Beste) |
| MOD-0176 FEFO / expiry | muafiyet | `COUNT_GAIN` / `COUNT_LOSS` muaf (`c101e22fa:INV/Domain/Expiry/Rules/FefoRules.cs:177-183`; `SCE/MOD-0176-expiry-fefo-management.md:347-349`) | — | test seam (§5) | MVP-1 CT (Beste) |
| MOD-0177 recall | muafiyet | `COUNT_*` muaf, "count truth" (`c101e22fa:INV/Domain/Recall/Rules/RecallExitRule.cs:9-10`; `SCE/MOD-0177-recall-readiness.md:547`) | — | yok | MVP-1 CT (Beste) |
| MOD-0178 S1 | okuma / XMC | lokasyon rolleri `Receiving` / `Staging` (`c101e22fa:INV/Domain/Warehouse/Enums/WarehouseEnums.cs:7-11`); görev türleri yalnız `Putaway` / `Pick` (`…/WarehouseTaskEnums.cs:5-9`); Staging farkı için çıkış yolu "sayım (MOD-0181)" (`SCE/MOD-0178-putaway-picking-packing.md:1580-1582`); 0181 kendi namespace'ini açar (`:2062-2064`) | rota ailesi onayı (Q-15 = A, dosya değişmez), geçici sayaca `CNT` (Q-7 = B); görev modeli **kullanılmaz** (Q-22) | yalnız `IWarehouseRecordCodeAllocator`'a eklemeli `CNT` (XMC) | MVP-1 CT (Beste) |
| MOD-0040-FU02 RCS-001 | SOFT (Q-7 = B — geçiş hedefi) | paket `ready-for-dev`, `approval_status: pending`, `code_start: yok` (`execution/domains/platform-shared-services/module-packs/MOD-0040-FU02-record-code-service.md:1-25`); sözleşme `RECORD-CODE-ALLOCATE 1.0.0 FROZEN` (`docs/analysis/contracts/record-code-allocate.md:1-5`); WMS türleri onun DL-8'i (`…FU02…:1639`, `:1848`) | `CNT` kodu | `CNT` türünün DL-8'e eklenmesi (öneri) | Beste (K2) |
| MOD-0018 RBAC + AuthService rol şablonu | HARD | `[HasPermission]` + manifest; rol şablonu ölçümü MOD-0178 §14.2 | izinler, roller | rol şablonu ayrı WP (XMC) | Ali (Auth) |
| BL-501 merkezi denetim | HARD | InventoryService'te var — `c101e22fa:INV/Api/Program.cs:66` (`RequireEnlistment`); defter izi `tests/architecture/audit-ledger/Diten.InventoryService.md:12` (`inventory-merkezi-iletim`, yol b) | §21 | yok | Building.Blocks korunan yol; sahip TBD |
| MOD-0023 onay iş akışı | bağımlılık DEĞİL (v1) | emsal `WorkflowApprovalPort` (`c101e22fa:INV/Application/Features/Quarantine/Services/QuarantineWorkflow.cs:21`, port `QuarantinePorts.cs:15-24`) | kullanılmaz — Q-4 = A (karar 2026-11-06), onay modül içi | — | — |
| MOD-0022 e-imza | bağımlılık DEĞİL (v1) | emsal `IRecallSignature` (`…/Features/Recall/Ports/IRecallSignature.cs:24`) | kullanılmaz — Q-5 = A (karar 2026-11-06), e-imza yok | — | — |
| MOD-0048 gerekçe kodu | bağımlılık DEĞİL (v1) | defter `MovementReason`'ı opak tutar, doğrulamaz (`c101e22fa:INV/Domain/Entities/InventoryTransaction.cs:21-22`) | v1 sabit opak kod `CYCLE_COUNT_VARIANCE` (Q-17 = A); MOD-0048 listesi gelince bağlanır (FU-0181-REASON) | yok | TBD (MOD-0048 sahibi) |
| MDM tüzel kişilik `Code` | HARD (numara öneki) | MOD-0178 M-2 yolu (`SCE/MOD-0178-putaway-picking-packing.md:598`) | `{ŞİRKET}` | yok | MDM |
| PRODUCT-MASTER (MOD-0290) | HARD | posting doğrulaması `MovementMasterDataValidator` (`InternalMovementPostingService.cs:41`) | baz birim, lot / seri kontrolü | yok | MDM |
| WMS-COUNT-BUNDLE | HARD (contract-first) | yok (0 eşleşme) | sözleşme taslağı | bu paket | supply-chain-execution (Beste) |
| MOD-0182 / MOD-0365 barkod | bağımlılık DEĞİL (v1) | plan `:78` | Q-12 = A: yalnız opak `scanRef` | — | TBD |
| Gateway | Q-15 = A | `ocelot.json:1134-1161` (`c101e22fa`) | §15 — mevcut `/api/warehouse/{everything}` | `ocelot.json` değişmez; yalnız kapsama testi | integration-agent |

**XMC-001 özeti:** 0181 yukarıdaki başka-modül dosyalarına **yalnız eklemeli** dokunur; her biri dokunmadan önce sahibine sorulur ve kabul
kaydına yazılır. Sahip: MOD-0173 / FU02 / FU03 / 0174–0178 → MVP-1 CT (Beste) — Q-21 izin değişikliği (§2.4) ve Q-7 `CNT` portu dahil;
AuthService → Ali (Auth); RCS → Beste (K2). MOD-0023 / 0022 v1'de kullanılmaz (Q-4 / Q-5 = A); MOD-0048 v1'de bağlanmaz (Q-17 = A),
sahibi TBD (yalnız FU-0181-REASON için).

## 8. Runtime Constraints

### 8.1 Servis ve kapsam
- Servis `Diten.InventoryService`, port **5067** (`AGENTS.md:81`; `domain-config.md` OD-5 satırı). Web (5001) yalnız Gateway (5000) üzerinden
  çağırır; tarayıcı servis portuna gitmez.
- MongoDB (repo deseni); her yazım `WithTransactionAsync` içinde; olay + iş yazımı + merkezi denetim enlist'i aynı işlemde (§8.11).
- Kiracı + şirket: `TenantId` token'dan, `LegalEntityId` `X-Legal-Entity-Id`'den sunucuda çözülür (scope middleware,
  `c101e22fa:INV/Infrastructure/Middleware/InventoryScopeResolutionMiddleware.cs:57-61`); başka kiracının / şirketin kaydı **404**
  (`.antigravity/rules/multi-tenancy.md:54`).
- Silme: yalnız `Draft` sayım soft delete (`IsDeleted`, `DeletedAt`); `Release` sonrası silme yok, iptal var. Olaylar ve kanıt dışa
  aktarımları silinmez.

### 8.2 Beklenen miktar anlık görüntüsü
1. `Release` (yalnız `Draft`'tan): kapsam düğümleri LOCATION'dan stoklanabilir, `Active` bin'lere açılır → `ScopeBinIds`. Bin sayısı ya da
   satır sayısı `MaxLinesPerCount`'u (varsayılan 2000) aşarsa 422 `COUNT_SCOPE_TOO_LARGE` (Q-24 = A); hiç bin yoksa 422 `COUNT_SCOPE_EMPTY`.
2. Aynı bin başka **açık** sayımın (`Released` \| `Counting` \| `Submitted`) kapsamındaysa 409 `COUNT_SCOPE_OVERLAP` (Q-6 = B). Bu yasak
   yalnız iki sayımın aynı bin'i sayımını engeller; defter hareketini **engellemez** — lokasyon kilidi yoktur, depo durmaz (Q-6).
3. Bakiye okuması: şirketin kapsam bin'lerindeki `OnHand > 0` satırları, `ScopeSkuIds` süzgeciyle; v1'de stok statüsü `AVAILABLE`
   (Q-8 = A). Her satır için bir `CycleCountLine` (`Origin = Snapshot`, `ExpectedQuantity` = satırın `OnHand`'i). Okuma yalnız `Find`
   (emsal `WarehouseStockReads.cs:27-38`).
4. **Tutarlılık kuralı (öneri; ölçülmedi — geliştiricinin ilk ölçümü):** bakiye okuması ile `SnapshotAt` arasında commit olan bir hareketin
   iki kez sayılmaması için bakiye satırları ve kapsam bin'lerine son 5 dakikada (`CreatedAt`) dokunan hareketlerin kimlik listesi **aynı
   Mongo okuma anında** (oturum, snapshot okuma) alınır ve satırla saklanır (`SnapshotTransactionIds`); §8.4 bu listedekileri dışlar.
   Depo deseninin snapshot okumasını destekleyip desteklemediği **ölçülmedi**; desteklemiyorsa DL-1 bunu CT'ye bulgu olarak yazar.
5. **Kör sayım (`Blind = true`):** `ExpectedQuantity`, `ExpectedAtCount`, `VarianceQuantity` alanları `Submit`'ten önce **hiçbir** cevapta
   (satır listesi, sayım formu, sayım detayı, dashboard) yoktur — JSON'da alan yazılmaz; yetkiye göre değişmez. `Submit`'ten sonra yalnız
   `cycle-count.variances.read` sahibi görür (Q-1 = A).
6. Anlık görüntü sayım kanıtıdır, bakiye değildir (§2.3 kural 1). `Release`'ten sonra hiçbir yol `ExpectedQuantity`'yi yazmaz (AC-4).

### 8.3 Durum makinesi
**Sayım (`CycleCount.Status`):**

| Durum | Geçiş | Komut | Koşul |
|---|---|---|---|
| `Draft` | → `Released` | `ReleaseCycleCountCommand` | §8.2 |
| `Draft` | silinir (soft) | `Delete` / `BulkDelete` | yalnız `Draft` |
| `Released` | → `Counting` | ilk `RecordCountLineCommand` / `AddCountLineCommand` | — |
| `Released` \| `Counting` | → `Submitted` | `SubmitCycleCountCommand` | her `Snapshot` satırı sayılmış (422 `COUNT_LINES_UNCOUNTED`) |
| `Draft` \| `Released` \| `Counting` | → `Cancelled` | `CancelCycleCountCommand` | gerekçe zorunlu; hiçbir satırda posting yok |
| `Submitted` | → `Closed` | sistem (son satır son durumuna gelince) | her satır `NoVariance` \| `Rejected` \| `Recounted` \| (`Approved` + `Adjustment.Posted`) |
| `Closed` \| `Cancelled` | son durum | — | her yazma 409 `COUNT_INVALID_STATE` |

**Satır (`CycleCountLine.Status`):** `Open` → `Counted` (giriş; `Submit`'e kadar yeniden girilebilir, If-Match) → `Submit`'te:
`NoVariance` (fark 0 ve bayrak yok) \| `AwaitingApproval` (fark ≠ 0) → `Approved` (onay ya da tolerans içi — §8.5) \| `Rejected` \|
`Recounted`. `Approved` satırın `Adjustment.Status`'u: `Pending` → `Posted` \| `Refused` \| `IntegrityError`. `Refused` satır için yalnız
`Reject` ya da `Recount` (rezervasyon engelinde ayrıca `Retry` — §8.7); `IntegrityError` için yalnız `resolve-integrity`.

### 8.4 Fark hesabı
`Submit`'te her sayılmış satır için:
- `MovementsSinceSnapshot` = aynı anahtarın (bin, SKU, seviye, lot, statü) `SnapshotAt` ile `CountedAt` arasında commit olmuş defter
  hareketlerinin net etkisi (gelen +, giden −), `SnapshotTransactionIds` hariç. Zaman ölçütü hareketin **`CreatedAt`'idir**, `PostingDate`
  değil (posting tarihi geriye atılabilir; çıkarım: commit sırasını en iyi `CreatedAt` yansıtır — ölçülmedi).
- `ExpectedAtCount = ExpectedQuantity + MovementsSinceSnapshot`; `VarianceQuantity = CountedQuantity − ExpectedAtCount`.
- `MovementDuringCount = true`: anahtara `SnapshotAt`–`CountedAt` arasında en az bir hareket dokunduysa (Q-6 = B, karar 2026-11-06). Bayraklı
  satır fark 0 olsa bile `AwaitingApproval`'a gider (tolerans uygulanmaz); onaylayıcı yeniden sayım isteyebilir.
- `Found` satırda beklenen 0'dır; `MovementsSinceSnapshot` aynı kuralla hesaplanır.
- Hareket okuması 0181 Persistence'ta `Find` ile yapılır (emsal `QuarantineLedgerReads.cs:126-136`; konum index'leri
  `InventoryIndexConfiguration.cs:45,48`); okunamazsa 503 `COUNT_INVENTORY_UNAVAILABLE`, `Submit` yazılmaz.

### 8.5 Onay ve eşik
- **Eşik (Q-3 = A, karar 2026-11-06):** `|VarianceQuantity| ≤ ToleranceAbsolute` **ve** `|VarianceQuantity| / ExpectedAtCount × 100 ≤ TolerancePercent` (beklenen 0
  ise yüzde koşulu sağlanmaz) ve `MovementDuringCount = false` ve satır seri kontrollü değil (Q-10 = A, §8.9) → satır `Submit`'te `Approved` (onaylayan "Sistem (tolerans)") ve posting
  başlar. **Varsayılan tolerans 0'dır: ayar yazılmadıkça her fark onay ister.** Değer eşiği v1'de yok — InventoryService Domain'inde
  değerleme alanı yok (`git grep -i "unitcost|movingaverage|valuation" c101e22fa -- …Domain`: 0 eşleşme) (Q-3, Q-19).
- **Onay (Q-4 = A, karar 2026-11-06):** modül içi `ApproveCountVarianceCommand`, izin `cycle-count.variances.approve`, "Onaylayıcı" rolü
  (AuthService WP). MOD-0023 iş akışı v1'de kullanılmaz. Blueprint "approvals optional" der (satır 182 L).
- **Dört göz (Q-5 = A, karar 2026-11-06):** zorunlu — onaylayan, satırın `CountedBy`'ı ve sayımın `SubmittedBy`'ı olamaz → 403 `COUNT_SELF_APPROVAL_FORBIDDEN`
  (emsal `ExpiryRules.SeparationOfDuties`, `c101e22fa:INV/Domain/Expiry/Rules/ExpiryRules.cs:205`; `RecallRules.cs:211`). Ayarla kapatılamaz;
  e-imza (MOD-0022) v1'de yok.
- **Ret:** gerekçe zorunlu (≤ 500) → `Rejected`, hareket yok.
- **Yeniden sayım (Q-11 = A, karar 2026-11-06):** yalnız elle tetiklenir (otomatik yeniden sayım yok). `RequestCountRecountCommand` seçili satırlar
  için yeni bir sayım (`RecountOfId`) açar ve hemen `Release` eder (yeni anlık görüntü); eski satırlar `Recounted`. Bir satır zinciri
  `MaxRecounts`'u (varsayılan 2) aşamaz (409 `COUNT_RECOUNT_LIMIT`). Farklı sayan **önerilir, zorunlu değildir**: UI yeni sayıma önceki
  sayandan farklı bir kullanıcı atamayı önerir, sunucu reddetmez.

### 8.6 Defter kaydı ve kurtarma
- Tek kapı: `ICycleCountMovementPoster` → `InternalMovementPostingService.PostAsync` (`WarehouseMovementPoster.cs:14-21` deseni). Çağrı yeri
  tektir ve `ExpiryQuarantineTypeExemptionTests`'e eklemeli ikili olarak yazılır (§5).
- İstek: `movementType` = fark > 0 ise `COUNT_GAIN` (to* = satırın bin'i, `toStockStatus` = satır statüsü, lot), < 0 ise `COUNT_LOSS`
  (from* = bin, `fromStockStatus`, lot; seri kontrollü satır v1'de post edilmez — Q-10 = A, §8.9); `quantity` = |fark|, `uomId` = baz birim;
  `sourceModule = "MOD-0181"`, `sourceType = "CYCLE_COUNT"` (öneri ad; sözleşme taslağında sabitlenir), `sourceDocumentId` = sayım `Id`,
  `sourceLineId` = satır `Id`; `movementReason = "CYCLE_COUNT_VARIANCE"` (sabit opak kod, Q-17 = A);
  `idempotencyKey = cycle-count:{lineId:D}` (48 karakter; sınır 128 — `c101e22fa:INV/Application/Common/InventoryContract.cs:28`).
  Yön kuralları MOD-0173'ündür: inbound from* yasak, outbound kaynak satır `BaseQuantity`'yi kapsamalı (`SCE/MOD-0173-inventory-ledger-valuation.md:225-226`;
  kod `c101e22fa:INV/Domain/Rules/MovementDirectionRules.cs:23,25`; tel adları `WireNames.cs:23-24`; enum `MovementType.cs:19-20`).
- `transactionDate` = satırın `CountedAt`'i, `postingDate` = ilk denemenin UTC günü (Q-19 = A); ikisi ilk denemede yazılır ve her denemede
  bayt-bayt aynıdır (aynı parmak izi — MOD-0178 AC-5 deseni).
- **Kurtarma (MOD-0178 §8.3 deseni, `SCE/MOD-0178-putaway-picking-packing.md:712`):** işlem 1 — `Adjustment.Status = Pending` + olay; posting;
  işlem 2 — `Posted` (TransactionId / Number) ya da `Refused` (kod + details). Sonucu bilinmeyen hata (503, 500, 502, 504, iptal, istisna) →
  `Pending` kalır, cevap 503. `retry` ve sweeper önce `IPostingKeyLookup.LookupAsync` sorar: `Committed` → `Posted` (ikinci hareket yok);
  `NotCommitted` → aynı anahtarla posting; `KeyReusedDifferentPayload` ya da tutucu hareketin ters kaydı varsa → `IntegrityError`.
  Sweeper emsali `c101e22fa:INV/Persistence/Warehouse/PendingConfirmationReconcilerHostedService.cs`.

### 8.7 Rezervasyon uzlaştırması
- `COUNT_LOSS` ayrılmış stoğu açıkta bırakacaksa defter 422 `INSUFFICIENT_STOCK` + `details.level` döner ve bir ret satırı yazar
  (`InternalMovementPostingService.cs:198-212`; FU03 `:385-386`). 0181: `Adjustment.Status = Refused`, `RefusalCode = INSUFFICIENT_STOCK`,
  `RefusalDetails` aynen; hareket yok; Variance Trace'te "rezervasyon engeli" (AC-27).
- **Uzlaştırma (FU-03-COUNT, `FU03 :1262` — "sayım akışının rezervasyon uzlaştırması MOD-0181 paketinde") — Q-9 = A, karar 2026-11-06:** 0181
  rezervasyona dokunmaz; rezervasyonun sahibi (ör. MOD-0178 outbound) bıraktıktan sonra onaylayıcı "Yeniden dene" der (`retry`, aynı
  anahtar — reddedilen posting commit olmadığı için anahtar boştur). Kısmi post (yalnız serbest miktar) ve rezervasyonu 0181'in bırakması
  v1'de **yoktur**.

### 8.8 Karantina ve tutulan stok
- **Karar (Q-8 = A, karar 2026-11-06):** v1 anlık görüntüsü yalnız `AVAILABLE`; `QUARANTINE` / `QUALITY_INSPECTION` / `BLOCKED` kovaları sayım formunda salt okunur
  bilgi olarak gösterilir ve **post edilmez**. Gerekçe: MOD-0175 dışı çıkış hayalet hold bırakır (`…FU01…:1120-1124`, FU-0175-FU01-OUTBOUND).
- `COUNT_GAIN` statüsü satırın statüsüdür: v1'de her zaman `AVAILABLE` (Q-8 = A). "Bulunan stok `QUARANTINE`'e" seçeneği (Q-8 C — MOD-0175
  tam bir hold açardı, `QuarantineIntake.cs:45-61`) v1'de **seçilmedi**; tutulan kovaların sayımı FU-0181-HELD'dir.

### 8.9 Lot ve seri
- Lot kontrollü SKU'da satır lot taşır; `Found` satırda lot zorunludur. `COUNT_GAIN` inbound lot seçicisi MOD-0174 kuralıdır (`SCE/MOD-0174-lot-serial-tracking.md:396`);
  **Karar (Q-10 = A, karar 2026-11-06):** lot düzeyi zorunludur; `Found` satırda MOD-0174'te kayıtlı olmayan lot 422 `COUNT_LOT_UNKNOWN` (öneri kod)
  ile reddedilir, satır yazılmaz (kayıtsız lotu kabul etme seçeneği seçilmedi).
- **Seri kontrollü SKU (Q-10 = A):** v1'de miktar sayılır ve fark hesaplanır, ama fark **post edilmez**. Bu satırda `approve` 422
  `COUNT_SERIAL_NOT_POSTABLE` (öneri kod) döner, hareket yok; onaylayıcı yalnız `reject` (gerekçeyle) ya da `recount` yapabilir. Tolerans içi
  seri satır da otomatik `Approved` olmaz, `AwaitingApproval`'da kalır. Gerekçe: `COUNT_LOSS` seri kimliklerini ister
  (`PostMovementCommand.SerialIds`, `c101e22fa:INV/Application/Features/Inventory/Commands/PostMovementCommand.cs:36`) ve seri listesi girişi
  v1'de yok — seri düzeyi sayım FU-0181-SERIAL.
- Lot durumu retleri (`LOT_BLOCKED`, `LOT_EXPIRED`, …) olduğu gibi `Refused` olur (AC-28).

### 8.10 Muafiyetler
- Recall: `COUNT_GAIN` / `COUNT_LOSS` recall çıkış engelinden muaftır (`RecallExitRule.cs:9-10`; MOD-0177 `:547` "count truth"). 0181 kendi
  recall engeli **eklemez** (AC-34).
- FEFO / expiry kancası: muaf (`FefoRules.cs:177-183`). 0181 FEFO sapma gerekçesi istemez.
- Bu muafiyetler tür bazlıdır; `sourceModule` muafiyet vermez (`FefoRules.cs:174-175`).

### 8.11 Merkezi denetim ve yerel iz
- BL-501 InventoryService'te kurulu: `AddCentralAudit<InventoryCentralAuditScope>(…, RequireEnlistment …)` (`c101e22fa:INV/Api/Program.cs:66`);
  her yazma komutu `ICentralAuditDeclaration` taşır; kategori servisin `MasterData`'sı (`c101e22fa:INV/Application/Common/InventoryCentralAudit.cs:22`);
  `SourceModule` = manifest kodu `CYCLE-COUNTING` (Q-16 = A; MOD-0178 emsali `WarehouseCentralAudit.cs:12-18`).
- Operation adları Platform enum adlarıdır (`c101e22fa:services/Diten.Building.Blocks/src/Diten.BuildingBlocks.CentralAudit/CentralAuditPlatformValues.cs:37-52`).
- Merkezi kayıt ad ve gövde taşımaz; değerlerin tek yeri `wms_count_events`'tir (MOD-0178 §8.8 ölçümü, `:1026-1037`). Merkezi kayıt yalnız
  **başarılı ve yazan** komutta iletilir; tekrar (replay) ve 0-kayıt 0 bırakır; başarısız komut `Abandoned` (iletilmez).
- Posting'in defter enlist'i 0181 komutunun satırına düşer, ayrı merkezi kayıt doğmaz (MOD-0178 §8.8 `:1052-1056` ölçümü).
- **K2 sınıfı (Q-20 = A, karar 2026-11-06):** sayım düzeltmesi GxP stok kaydıdır; `RequireEnlistment` zaten kapalı-başarısızdır.

### 8.12 Kayıt kodu üretimi
`CycleCount.Number` = `{ŞİRKET}-CNT-{000001}` (RCS-001 biçimi, `.antigravity/rules/record-coding-standard.md:22-31`). `CNT` öneki RCS-001 §3
tablosunda yok (ölçüldü, `:45-70`) ve servis kodunda 0 eşleşme (`git grep '"CNT"\|-CNT-' c101e22fa -- services`). **Üretici — Q-7 = B (karar 2026-11-06):**
MOD-0178'in geçici portu `IWarehouseRecordCodeAllocator`'a `CNT` türü eklenir (XMC-001, eklemeli; MOD-0178 görev koduna dokunmaz —
Q-22) ve FU-0178-RCS-01 geçişiyle birlikte ortak servise (MOD-0040-FU02 DL-8'e `CNT`) taşınır (FU-0181-RCS); RCS-001 §6 "kendi sayacını
yazmaz" — 0181 ayrı sayaç **yazmaz**. RCS-001 §3 tablosuna `CNT` satırı korunan yol değişikliğidir, ayrı sahip onayıdır (§6, §22). Şirket
öneki MDM tüzel kişilik `Code`'u (`^[A-Z]{2,5}$`, aksi 422 `COUNT_COMPANY_CODE_INVALID`; MOD-0178 §22 deseni).

### 8.13 Ekranlar ve Blueprint soft pages
| Blueprint soft page | Ekran | Sayfa kodu | Veri |
|---|---|---|---|
| Cycle Count Dashboard | `/Warehouse/CycleCounts` — KPI kartları (açık sayımlar, onay bekleyen fark, reddedilen / rezervasyon engelli düzeltme, dönem doğruluğu %) + sayım listesi (DataTable) | `CYCLECOUNTS` (nav) | KPI'lar okuma anında hesaplanır, saklanmaz |
| (sayım formu) | `/Warehouse/CycleCounts/Sheet/{countId}` — bin bin satırlar, miktar girişi, "bulunan stok ekle", submit; 375 px zorunlu (Q-12 = A) | `COUNTSHEET` (nav değil) | kör sayımda beklenen yok |
| Variance Trace | `/Warehouse/CountVariances` — fark satırları; süzgeç depo / SKU / lot / tarih / durum; satırdan defter hareketine (`TransactionNumber`) bağlantı; onay / ret / yeniden sayım / yeniden dene | `COUNTVARIANCES` (nav) | — |
| Evidence Pack | `/Warehouse/CountEvidenceExports` — sayım başına dışa aktarım listesi ve indirme | `COUNTEVIDENCE` (nav) | DL-5, biçim PDF + CSV (Q-18 = A) |

### 8.14 Hata kodları
Öneri (`COUNT_` öneki; kesin liste sözleşme taslağında):

| Kod | HTTP | Ne zaman |
|---|---|---|
| `COUNT_NOT_FOUND` / `COUNT_LINE_NOT_FOUND` | 404 | yok, silinmiş, başka kiracı / şirket |
| `COUNT_CONFLICT` | 409 | If-Match uyuşmaz |
| `COUNT_INVALID_STATE` | 409 | durumda izinli olmayan komut |
| `COUNT_SCOPE_EMPTY` / `COUNT_SCOPE_TOO_LARGE` | 422 | §8.2 |
| `COUNT_SCOPE_OVERLAP` | 409 | bin başka açık sayımda (Q-6 = B) |
| `COUNT_LINES_UNCOUNTED` | 422 | submit'te sayılmamış satır |
| `COUNT_QUANTITY_INVALID` | 400 | negatif, > 6 ondalık, sayı değil |
| `COUNT_SELF_APPROVAL_FORBIDDEN` | 403 | dört göz (Q-5 = A) |
| `COUNT_LOT_UNKNOWN` | 422 | `Found` satırda MOD-0174'te kayıtlı olmayan lot (Q-10 = A) |
| `COUNT_SERIAL_NOT_POSTABLE` | 422 | seri kontrollü satırda onay — v1'de post edilmez (Q-10 = A) |
| `COUNT_REASON_REQUIRED` | 400 | ret / iptal gerekçesiz |
| `COUNT_RECOUNT_LIMIT` | 409 | zincir `MaxRecounts`'u aşar (Q-11 = A) |
| `COUNT_COMPANY_CODE_INVALID` | 422 | §8.12 |
| `COUNT_RECORD_CODE_UNAVAILABLE` | 503 | sayaç / ortak servis yazılamadı |
| `COUNT_INVENTORY_UNAVAILABLE` | 503 | defter okuması / posting sonucu bilinmiyor |
| `COUNT_ADJUSTMENT_INTEGRITY` | 409 | anahtar başka içerikle kullanılmış / tutucu hareket ters kaydedilmiş |
| `IDEMPOTENCY_KEY_REUSED` | 400 | aynı anahtar, farklı gövde |
| Defter kodları (`INSUFFICIENT_STOCK`, `LOT_BLOCKED`, `LOT_EXPIRED`, `SERIAL_COUNT_MISMATCH`, …) | 422 | aynen iletilir (`Refused`) |

### 8.15 AI Assist
Blueprint AI Tier `Assist`, kullanım Summarize / Extract / Recommend / Route, risk Medium, kapı "Prompt Registry; HITL; Model Registry;
Eval/Drift; Logging" (satır 182 Y–AB). **Karar (Q-14 = A, karar 2026-11-06):** MVP-5 v1 kapsamı **dışında**; hiçbir AI ucu ya da çağrısı yok; FU-0181-AI.

## 9. Layout & Shell Contract
- `shell: tenant` → `Views/Warehouse/{CycleCounts,CountVariances,CountEvidenceExports}/*.cshtml` dosyalarının **her birinde**
  `Layout = "_LayoutTenantShell";` **AÇIKÇA** yazılır (sayım formu `CountSheet.cshtml` dahil); `_ViewStart.cshtml` varsayılanı değişmez.
- View klasörü `Views/Warehouse/{Module}/` (MOD-0178'in `Views/Warehouse/PutawayTasks/` emsali); rota `/Warehouse/{Module}`.
- Yetkisiz kullanıcıya sayfa iskeleti çizilmez, yönlendirme yapılmaz (UAS-001, `.antigravity/rules/unauthorized-surface-standard.md` §1–§2).

## 10. Backend File Convention
Golden Reference Slim birebir (`services/Diten.DevEnablementService/src/Diten.DevEnablementService.Application/Features/GoldenReferenceSlim/`;
pack `execution/domains/developer-enablement/module-packs/DEV-0000-golden-reference-slim.md`):

```text
Features/CycleCounts/
├── Commands/        CreateCycleCountCommand.cs, UpdateCycleCountCommand.cs, DeleteCycleCountCommand.cs, BulkDeleteCycleCountCommand.cs,
│                    ReleaseCycleCountCommand.cs, CancelCycleCountCommand.cs, RecordCountLineCommand.cs, AddCountLineCommand.cs,
│                    SubmitCycleCountCommand.cs
├── Queries/         GetCycleCountListQuery.cs, GetCycleCountByIdQuery.cs, GetCycleCountLineListQuery.cs, GetCycleCountEventListQuery.cs,
│                    GetCycleCountDashboardQuery.cs
├── Handlers/
│   ├── CommandHandlers/   CreateCycleCountHandler.cs … SubmitCycleCountHandler.cs   (sealed class, suffix YOK)
│   └── QueryHandlers/     GetCycleCountListHandler.cs … GetCycleCountDashboardHandler.cs
├── Validators/      CreateCycleCountValidator.cs, UpdateCycleCountValidator.cs, RecordCountLineValidator.cs, AddCountLineValidator.cs
└── CycleCountModels.cs    (tek dosyada bütün DTO'lar)
Features/CountVariances/   aynı yapı: Approve/Reject/RequestCountRecount/RetryCountAdjustment/ResolveCountAdjustmentIntegrity/
                           ReconcilePendingCountAdjustment/UpdateCycleCountSettings komutları; GetCountVarianceList/ById, GetCycleCountSettings
                           sorguları; CountVarianceModels.cs
Features/CountEvidenceExports/  aynı yapı (DL-5)
Features/CycleCount/       ortak: CycleCountContract.cs, CycleCountErrorCodes.cs, CycleCountCentralAudit.cs, Ports/, Services/
```

Kurallar: Command / Query `sealed record`; Handler / Validator adında `Command` / `Query` soneki yok; bir dosyada tek `public` tip;
`Handlers/CommandHandlers` ve `Handlers/QueryHandlers` ayrımı zorunlu (`module-pack-standard.md` §4). Ortak `Features/CycleCount/` klasörü
MOD-0178'in `Features/Warehouse/` emsalidir (`c101e22fa` ağacı).

## 11. Frontend File Contract
Golden Slim seti (`frontend/Diten.Web/Views/DevEnablement/GoldenReferenceSlim/`):

| Klasör | Dosyalar |
|---|---|
| `Views/Warehouse/CycleCounts/` | `Index.cshtml` (KPI kartları + liste), `_Filter.cshtml`, `_DataTable.cshtml` (`data-dt-standard="v2"`, `data-dt-data-mode="server"`, skeleton), `_IndexL10n.cshtml`, `_CreateEditOffcanvas.cshtml` (8 alanlı form), `_DetailsQuickView.cshtml`, `CycleCountsIndex.cs`; ek sayfa `CountSheet.cshtml` (liste ekranı değil) |
| `Views/Warehouse/CountVariances/` | `Index.cshtml`, `_Filter.cshtml`, `_DataTable.cshtml`, `_IndexL10n.cshtml`, `_CreateEditOffcanvas.cshtml` (**yalnız karar formu**: onay / ret / yeniden sayım + gerekçe; yaratma yok), `_DetailsQuickView.cshtml`, `CountVariancesIndex.cs` |
| `Views/Warehouse/CountEvidenceExports/` | Slim seti (DL-5); `_CreateEditOffcanvas.cshtml` = dışa aktarma isteği |
| `wwwroot/assets/js/Warehouse/{CycleCounts,CountVariances,CountEvidenceExports}/` | `index.js`, `index.l10n.js`; `CycleCounts/countsheet.js` |
| `Resources/Views/Warehouse/{Module}/` | `{Module}Index.{en,tr,fr,es,zh,ar,ru}.resx` (+ `CountSheet`) |
| `frontend/Diten.Web/Controllers/` | `CycleCountsController.cs`, `CountVariancesController.cs`, `CountEvidenceExportsController.cs` — same-origin MVC proxy → Gateway |

Compact'a özgü `Create/Edit/Details.cshtml` ve `_Form.cshtml` **yok**. Liste ekranına dokunan görev `verify_datatable_page.py --area Warehouse
--module {Module} --format gaps` koşar ve sapmayı sahibine sorar (liste ekranı dokunma protokolü). Sayım formu DataTable değildir; satırlar
sunucu sayfalıdır.

## 12. Validation Rules

| Field | Required | Format/Rule | DB-level | Pre-check |
|---|---|---|---|---|
| WarehouseId | Evet | uuid; LOCATION `Warehouse`, `Active`, şirket kapsamı | — | LOCATION okuması (503 erişilemez) |
| CountType | Evet | `Cycle` \| `Full` (Q-1 = A) | — | — |
| Blind | Evet (varsayılan false) | bool | — | — |
| ScopeLocationIds | `Cycle`'da evet (1..200), `Full`'da boş | uuid; tip `StorageLocation` \| `Zone` \| `Bin`; `WarehouseId` yolunda | — | `getLocationPath` |
| ScopeSkuIds | Hayır (0..200) | uuid; tekrarsız | — | PRODUCT-MASTER (Release'te) |
| PlannedDate | Evet | ISO tarih; ≥ bugün (UTC) | — | — |
| AssignedTo | Hayır | uuid; kullanıcı dizininde, şirkette etkin | — | dizin okuması |
| Note | Hayır | trim, ≤ 500 | — | — |
| CountedQuantity | Evet (giriş) | decimal string ≥ 0, ≤ 6 ondalık; seri kontrollü SKU'da tam sayı (Q-10 = A) | — | satır `Open` \| `Counted`, sayım `Released` \| `Counting` |
| Found satır (Sku/Lot/Bin) | Evet | bin `ScopeBinIds` içinde; anahtar sayımda tekil; lot kontrollüde lot zorunlu ve MOD-0174'te kayıtlı (yoksa 422 `COUNT_LOT_UNKNOWN`, Q-10 = A) | tekil satır index'i | PRODUCT-MASTER, MOD-0174 lot okuması |
| Ret / iptal gerekçesi | Evet | trim, 1..500 | — | — |
| Tolerans (ayar) | Evet | `ToleranceAbsolute` decimal ≥ 0; `TolerancePercent` 0..100 (Q-3 = A) | tekil şirket kaydı | — |
| MaxRecounts / MaxLinesPerCount (ayar) | Hayır (varsayılan 2 / 2000) | tam sayı ≥ 0 / ≥ 1 (Q-11, Q-24 = A); üst sınır sözleşme taslağında | tekil şirket kaydı | — |
| Idempotency-Key | Evet (her yazma) | 8–100 karakter (`WarehouseContract.cs:15-16` emsali) | tekil kısmi index | anahtar araması |
| If-Match | Evet (güncelleme / giriş / karar) | `Version` | — | — |
| TenantId / LegalEntityId gövdede | Hayır | gelirse 400 | — | — |

## 13. Failure Path to Verify
- **Duplicate sayım (aynı Idempotency-Key, aynı gövde)** — Expected: 200 replay, yeni kayıt / olay / numara yok.
- **Aynı anahtar, farklı gövde** — Expected: 400 `IDEMPOTENCY_KEY_REUSED`, yazım yok.
- **Missing WarehouseId / boş kapsam** — Expected: 400 validator mesajı; `Release`'te bin yoksa 422 `COUNT_SCOPE_EMPTY`.
- **Bin başka açık sayımda** — Expected: 409 `COUNT_SCOPE_OVERLAP`; anlık görüntü yazılmaz (Q-6 = B).
- **Sayım sürerken kapsamda defter hareketi** — Expected: hareket defterde **kabul edilir** (kilit yok, Q-6); submit'te farka eklenir (§8.4).
- **Concurrency (If-Match uyuşmaz)** — Expected: 409 `COUNT_CONFLICT`; UI "veri değişti, yeniden yükle"; sessiz üzerine yazma yok.
- **Unauthorized actor (izin yok)** — Expected: 403; UI eylem gizli; sayfa izni yoksa iskelet yok (UAS-001).
- **Başka kiracı / şirket kaydı** — Expected: 404 (403 değil).
- **Sayılmamış satırla submit** — Expected: 422 `COUNT_LINES_UNCOUNTED`; durum değişmez.
- **Kendi saydığını onaylama** — Expected: 403 `COUNT_SELF_APPROVAL_FORBIDDEN` (Q-5 = A).
- **Kayıtsız lotla bulunan satır** — Expected: 422 `COUNT_LOT_UNKNOWN`; satır yazılmaz (Q-10 = A).
- **Seri kontrollü satırda onay** — Expected: 422 `COUNT_SERIAL_NOT_POSTABLE`; defterde hareket yok (Q-10 = A).
- **MOD-0173 elle `COUNT_*`, ek izin yok** — Expected: 403; hareket yok (Q-21, §2.4 — MOD-0173 seam).
- **Eşik üstü fark, onaysız posting girişimi** (ör. `retry` onaysız satırda) — Expected: 409 `COUNT_INVALID_STATE`; defterde hareket yok.
- **COUNT_LOSS rezervasyonu açıkta bırakır** — Expected: `Adjustment.Refused`, kod `INSUFFICIENT_STOCK` + `details.level` aynen; hareket yok.
- **Defter 503 / 500 / 502 / 504 / iptal** — Expected: `Adjustment.Pending` + 503; `retry` ya da sweeper tek hareketle `Posted`.
- **Anahtar başka içerikle defterde / tutucu hareket ters kaydedilmiş** — Expected: `IntegrityError` + 409 `COUNT_ADJUSTMENT_INTEGRITY`; yalnız `resolve` izniyle.
- **Kayıt kodu servisi / sayaç yazılamaz** — Expected: 503 `COUNT_RECORD_CODE_UNAVAILABLE`; sayım yaratılmaz.
- **Defter okuması (anlık görüntü / hareket farkı) erişilemez** — Expected: 503 `COUNT_INVENTORY_UNAVAILABLE`; yazım yok.
- **Kapalı / iptal sayımda yazma** — Expected: 409 `COUNT_INVALID_STATE`.

## 14. Authorization Convention

**Namespace (Q-16 = A, karar 2026-11-06):** `cycle-count.*` — MOD-0178 §14.1 kararı Q-08: 0181 kendi namespace'ini açar, önerilen ad `cycle-count.*`, kesin ad
bu pakette (`SCE/MOD-0178-putaway-picking-packing.md:2062-2064`). Depoda `cycle-count.` anahtarı yok (`git grep c101e22fa -- services frontend`:
0 eşleşme). PKS-001: küçük harf, noktalı, ≥ 3 segment, tek sahip (`.antigravity/rules/permission-key-standard.md` §1, §4). Manifest yalnız
`cycle-count.*` bildirir; `inventory.*`, `warehouse.*` bildirilmez.

| Anahtar | Tier | Dilim | Anlam |
|---|---|---|---|
| `cycle-count.counts.read` | 1 | DL-1 | dashboard, liste, detay, olaylar, satırlar (kör sayımda beklensiz) |
| `cycle-count.counts.create` | 1 | DL-1 | sayım aç |
| `cycle-count.counts.update` | 1 | DL-1 | Draft düzenle, ata, `release` (anlık görüntü) |
| `cycle-count.counts.delete` / `.bulk-delete` | 1 / 2 | DL-1 | yalnız Draft (soft) |
| `cycle-count.counts.cancel` | 3 (`cancel`) | DL-1 | iptal |
| `cycle-count.counts.record` | 3 (bu pakette kayıtlı) | DL-2 | miktar gir, bulunan satır ekle, submit |
| `cycle-count.variances.read` | 1 | DL-3 | Variance Trace, beklenen / fark görünümü |
| `cycle-count.variances.approve` / `.reject` | 2 | DL-3 | onay / ret |
| `cycle-count.variances.recount` | 3 (bu pakette kayıtlı) | DL-3 | yeniden sayım iste |
| `cycle-count.variances.post` | 3 (bu pakette kayıtlı) | DL-3 | `Pending` / rezervasyon engelli düzeltmeyi yeniden dene |
| `cycle-count.variances.resolve` | 3 (bu pakette kayıtlı) | DL-3 | `IntegrityError` uzlaştır (yalnız sorumlu) |
| `cycle-count.settings.read` / `.configure` | 1 / 3 (`configure`) | DL-3 | tolerans, yeniden sayım ve satır sınırı ayarı (Q-3, Q-11, Q-24) |
| `cycle-count.evidence.read` / `.export` | 1 / 2 | DL-5 | Evidence Pack |

**Politika ve aktör:** `[Authorize]` (tenant shell) + her eylemde `[HasPermission(CycleCountPermissions.X)]`; `AuthorizationGuardTests`
(`c101e22fa:INVT/Architecture/AuthorizationGuardTests.cs:19`) yeni controller'ları da ölçer. Aktör `tenant_user`. Rol şablonu önerisi
(yalnız yeni kiracılar; AuthService WP, Ali (Auth), Q-4 = A): Sayan = `counts.read` + `counts.record`; Sayım sorumlusu = `counts.*` +
`variances.read` + `variances.recount`; Onaylayıcı = `variances.*` + `settings.*` + `evidence.*` (rol adları öneri). Viewer'a otomatik
gidecek `read` anahtarlarında kişisel veri yok (çıkarım: sayım kaydı kişi adı değil kullanıcı kimliği taşır).

**0181 dışı anahtar (Q-21, §2.4):** MOD-0173 elle `COUNT_*` girişi için `inventory.count-adjustments.create` (öneri ad) `inventory.*`
namespace'indedir, MOD-0173'ündür; 0181 manifesti onu bildirmez. Rol şablonunda Onaylayıcı rolüne gitmesi önerilir (AuthService WP).

**Manifest (Q-16 = A, karar 2026-11-06):** ModuleCode `CYCLE-COUNTING` (MOD-0178 `PUTAWAY-PICKING-PACKING` emsali, `PutawayPickingPackingManifestProvider.cs:21`),
SortOrder 181, Pages `CYCLECOUNTS` (nav), `COUNTSHEET` (nav değil), `COUNTVARIANCES` (nav), `COUNTEVIDENCE` (nav, DL-5); nav anahtarları
`Nav.Module.CYCLECOUNTING`, `Nav.Page.CYCLECOUNTS`, `Nav.Page.COUNTVARIANCES`, `Nav.Page.COUNTEVIDENCE` (`module-pack-standard.md` §7 biçimi).

## 15. Gateway / API Routing Decision
**Karar (Q-15 = A, sahip, 2026-11-06):**
- **Seçilen — A:** uçlar `/api/warehouse/cycle-counts…`, `/api/warehouse/count-variances…`, `/api/warehouse/count-settings`,
  `/api/warehouse/count-evidence-exports…` — mevcut `/api/warehouse/{everything}` → 5067 rotasının altında
  (`c101e22fa:gateway/Diten.ApiGateway/ocelot.json:1153-1161`); **`ocelot.json` değişmez**; `ScopedBasePaths` zaten kapsar
  (`InventoryScopeResolutionMiddleware.cs:61`). Gerekli: integration-agent'ın 0181 uç kapsama testi — ayrı dosya (öneri
  `Mod0181CycleCountRouteTests`; emsal `gateway/Diten.ApiGateway.Tests/Mod0178WarehouseRouteTests.cs`, onun sabit listesine dokunulmaz) — ve
  `/api/warehouse` rota ailesinin sahibi MOD-0178'in onayı (XMC-001; aile tek-yazıcı seam'dir — `domain-config.md` K15).
- **Seçilmeyen — B:** ayrı taban `/api/cycle-counts` (yeni rota + `ScopedBasePaths` eklemesi) — v1'de yok.
- İletilen başlıklar: `Authorization`, `X-Legal-Entity-Id`, `X-Correlation-Id`, `Idempotency-Key`, `If-Match`. Dev dilimi
  `ocelot.json`'a yazmaz.

## 16. Acceptance Criteria

Her guard maddesi **üretim koduna karşı** ölçülür ve sabotaj kanıtı ister: ilgili üretim satırı geçici bozulur, test kırmızıya döner,
bozma geri alınır (kendi kopyasıyla ölçen test guard sayılmaz).

**DL-1 — çekirdek kayıt ve anlık görüntü (arka uç)**

- [ ] AC-1 (zorunlu) — `POST /cycle-counts` geçerli 8 alanla 201; `Status = Draft`; `Number` `{ŞİRKET}-CNT-` + 6 hane biçiminde ve kiracı genelinde tekil (üretici MOD-0178 geçici portu `CNT`, Q-7 = B); `wms_count_events`'te 1 `Created` olayı aynı işlemde. Sabotaj: olay yazımını işlemden çıkar → olay ya da kayıt tek başına kalır, test kırmızı.
- [ ] AC-2 (zorunlu) — `release`: kapsam düğümleri stoklanabilir `Active` bin'lere açılır; her `AVAILABLE`, `OnHand > 0` bakiye satırı için tam bir satır, `ExpectedQuantity` o anki defter bakiyesine eşit (test defterle karşılaştırır); `SnapshotAt` yazılır; bin yoksa 422 `COUNT_SCOPE_EMPTY`, sınır aşımında 422 `COUNT_SCOPE_TOO_LARGE`. Sabotaj: `ScopeSkuIds` süzgecini kaldır → fazla satır, kırmızı.
- [ ] AC-3 (zorunlu) — İkinci bakiye yok: mimari test 0181 üretim kodunda (`CycleCount*` klasörleri) `IInventoryLedgerRepository`, `inventory_transactions`, `inventory_balances` metni ve `OnHand` / `Available` / `Reserved` özelliği bulmaz; `SingleSystemOfRecordArchitectureTests` `wms_count_*` koleksiyonlarını açıkça bildirir ve `Only_the_ledger_and_its_projection_carry_quantities` listesi (`:138-141`) değişmez; 0181'in tek stok kapısı `CycleCountMovementPoster`. Sabotaj (ayrı ayrı): satıra `OnHand` özelliği ekle → kırmızı; bir handler'a `IInventoryLedgerRepository` enjekte et → kırmızı; bildirilmemiş `wms_count_x` koleksiyonu aç → kırmızı.
- [ ] AC-4 (zorunlu) — Anlık görüntü değişmez: `release`'ten sonra kapsamda `LOCATION_TRANSFER` / `COUNT_*` / satış hareketi post edilir; satırın `ExpectedQuantity`'si ve `SnapshotAt` değişmez (okuma ve submit sonrası). Sabotaj: satır okumasında beklenen miktarı defterden yeniden doldur → kırmızı.
- [ ] AC-5 (zorunlu) — Kiracı ve şirket izolasyonu: A kiracısının sayımına B kiracısından her uç (GET / PUT / DELETE / release / cancel / lines / submit / variances / approve / retry / evidence) 404; aynı kiracıda başka şirketten 404; listeler yalnız kendi şirketini döner. Sabotaj (ayrı ayrı): depodan `TenantId` yüklemini düşür → kırmızı; `LegalEntityId` yüklemini düşür → kırmızı.
- [ ] AC-6 (zorunlu) — Gövde ya da sorguda `tenantId` / `legalEntityId` → 400; kayıt sunucunun çözdüğü kapsamla yazılır.
- [ ] AC-7 (zorunlu) — Her controller eylemi `[HasPermission(CycleCountPermissions.*)]` taşır ve manifest yalnız controller'ın uyguladığı `cycle-count.*` anahtarlarını bildirir (MOD-0178 `WarehouseRegistrationTests.cs:24-62` deseni). Sabotaj: bir eylemden özniteliği kaldır → `AuthorizationGuardTests` kırmızı.
- [ ] AC-8 (zorunlu) — İdempotent yazma: her yazma komutu için aynı `Idempotency-Key` + aynı gövde (+ ilk isteğin `If-Match`'i) → 200 replay, `Version` ve olay sayısı değişmez; aynı anahtar + farklı gövde → 400 `IDEMPOTENCY_KEY_REUSED`, yazım yok. Ayrı testler: yarat, düzenle, sil, toplu sil, release, iptal, giriş, bulunan satır, submit, onay, ret, yeniden sayım, retry, resolve, ayar.
- [ ] AC-9 (zorunlu) — Eşzamanlılık: eski `If-Match` ile düzenleme / giriş / karar → 409 `COUNT_CONFLICT`, sessiz üzerine yazma yok.
- [ ] AC-10 (zorunlu) — Silme yalnız `Draft`: soft delete (`IsDeleted = true`, `DeletedAt` dolu), listeden düşer, GET 404; `Released` ve sonrası silme 409 `COUNT_INVALID_STATE`; toplu silmede `Draft` olmayan satırlar ayrı sonuçla döner, hiçbiri silinmez (satır sonucu sözleşme taslağında).
- [ ] AC-11 (zorunlu) — Merkezi denetim: her 0181 yazma komutu `ICentralAuditDeclaration` taşır; `CentralAuditDeclarationTests` sayısı = 87 + dilimin 0181 yazma komutları ve sağlayıcı listesinde `CycleCountingManifestProvider`; başarılı yazma → 1 merkezi kayıt, replay → 0, başarısız → iletilen 0; yerel olay aynı işlemde. Sabotaj: bir callback'ten `CentralAuditTransaction.EnlistAsync`'i kaldır → `CentralAuditDeclarationTests` kırmızı.
- [ ] AC-12 (zorunlu) — Kayıt kodu: `Number` API'den değiştirilemez (gövdede gelirse 400 ya da yok sayılır — sözleşme taslağında sabitlenir); eşzamanlı iki yaratma farklı numara alır; sayaç yazılamazsa 503 `COUNT_RECORD_CODE_UNAVAILABLE` ve sayım yok; 0181 kendi sayacını yazmaz (Q-7 = B).
- [ ] AC-13 (zorunlu) — Kapsam çakışması, kilit yok (Q-6 = B): bir bin aynı anda en çok bir açık sayımda; ikinci `release` 409 `COUNT_SCOPE_OVERLAP` ve anlık görüntü yazılmaz. Açık sayımın kapsamındaki bin'e `release`'ten sonra post edilen `LOCATION_TRANSFER` / satış hareketi defterde **kabul edilir** (0181 hiçbir hareketi engellemez; MOD-0173'e kilit seam'i yok). Sabotaj: çakışma denetimini kaldır → kırmızı.

**DL-2 — sayım girişi ve fark hesabı (arka uç)**

- [ ] AC-14 (zorunlu) — Giriş: `PUT …/lines/{lineId}` decimal ≥ 0, ≤ 6 ondalık → satır `Counted`, `CountedBy` / `CountedAt` sunucudan; negatif / 7 ondalık / sayı değil → 400 `COUNT_QUANTITY_INVALID`; ilk giriş sayımı `Counting`'e alır.
- [ ] AC-15 (zorunlu) — Kör sayım: `Blind = true` sayımda `Submit`'ten önce satır listesi, sayım detayı, sayım formu ve dashboard cevaplarının JSON'unda `expectedQuantity`, `expectedAtCount`, `varianceQuantity` alanı **yoktur** — en geniş yetkili kullanıcı dahil (Q-1 = A). Sabotaj: DTO eşlemesinde beklenen alanı her zaman yaz → kırmızı.
- [ ] AC-16 (zorunlu) — Bulunan stok: kapsam bin'inde anlık görüntüde olmayan SKU / lot için `POST …/lines` → `Origin = Found`, `ExpectedQuantity = 0`; kapsam dışı bin 422; aynı anahtar ikinci kez 409; lot kontrollü SKU'da lotsuz satır 400, MOD-0174'te kayıtlı olmayan lot 422 `COUNT_LOT_UNKNOWN` ve satır yazılmaz (Q-10 = A). Sabotaj: lot kayıt denetimini kaldır → kayıtsız lotla satır yazılır, kırmızı.
- [ ] AC-17 (zorunlu) — `Submit` her `Snapshot` satırı sayılmadan 422 `COUNT_LINES_UNCOUNTED`; başarılıysa `Submitted` ve her satırın farkı yazılır.
- [ ] AC-18 (zorunlu) — Fark formülü: release → kapsam bin'ine +3 `LOCATION_TRANSFER` (anlık görüntüden sonra, girişten önce) → sayılan = beklenen + 3 → `MovementsSinceSnapshot = 3`, `VarianceQuantity = 0`; aynı kurguda sayılan = beklenen + 1 → fark −2. `SnapshotTransactionIds`'teki hareket iki kez sayılmaz. Sabotaj: hareket farkını hesaptan çıkar → fark +3 / +1, kırmızı.
- [ ] AC-19 (zorunlu) — `MovementDuringCount`: anahtara `SnapshotAt`–`CountedAt` arası hareket dokunduysa bayrak `true` ve satır fark 0 olsa da `AwaitingApproval` (tolerans uygulanmaz) (Q-6 = B). Sabotaj: bayrak koşulunu kaldır → fark 0 bayraklı satır `NoVariance` olur, kırmızı.
- [ ] AC-20 (zorunlu) — `Submit` defter okuması erişilemezse 503 `COUNT_INVENTORY_UNAVAILABLE`, sayım `Counting` kalır, satırlar değişmez.

**DL-3 — onay, defter kaydı, kurtarma (arka uç)**

- [ ] AC-21 (zorunlu) — Onaysız posting yok: tolerans ayarı yokken (varsayılan 0) her fark ≠ 0 satır `AwaitingApproval`; onaylanmadan defterde `sourceModule = MOD-0181` hareketi 0; onaysız satırda `retry` 409. Eşik ayarlıyken `|fark| ≤ eşik` ve bayraksız satır `Approved` ("Sistem (tolerans)") ve post edilir, eşik üstü edilmez (mutlak + yüzde, ikisi birlikte; Q-3 = A). Sabotaj: tolerans karşılaştırmasını `true` döndür → eşik üstü fark post edilir, kırmızı.
- [ ] AC-22 (zorunlu) — Dört göz: satırı sayan ya da sayımı submit eden kullanıcının onayı 403 `COUNT_SELF_APPROVAL_FORBIDDEN`, hareket yok (Q-5 = A; ayarla kapatılamaz). Sabotaj: kullanıcı karşılaştırmasını kaldır → kırmızı.
- [ ] AC-23 (zorunlu) — Onay → posting: fark +2 → tek `COUNT_GAIN` (to = satır bin'i, `toStockStatus` = satır statüsü, lot), fark −2 → tek `COUNT_LOSS` (from = bin); miktar 2, baz birim; `sourceModule = MOD-0181`, `sourceType = CYCLE_COUNT`, `sourceDocumentId` = sayım, `sourceLineId` = satır, anahtar `cycle-count:{lineId}`; satır `Adjustment.Posted` + `TransactionNumber`; son satır bitince sayım `Closed`.
- [ ] AC-24 (zorunlu) — İdempotent posting: aynı satırın onayı iki kez, onay + retry, iki eşzamanlı retry → defterde aynı anahtarla tek hareket; ikinci cevap 200 replay. Sabotaj: anahtara deneme sayacı / rastgele değer ekle → iki hareket, kırmızı.
- [ ] AC-25 (zorunlu) — Pending kurtarma: defter 503 / 500 / 502 / 504 / iptal / beklenmeyen istisna (ayrı testler) → `Adjustment.Pending` + 503, `Refused` yazılmaz; commit öncesi hata → retry `NotCommitted` bulur, aynı anahtarla post → `Posted`, tek hareket; commit sonrası hata → sweeper ya da retry `Committed` bulur → `Posted`, ikinci hareket yok; `transactionDate` / `postingDate` her denemede bayt-bayt aynı. Sabotaj (ayrı ayrı): sonucu bilinmeyen 5xx'i `Refused`'a eşle → kırmızı; `postingDate`'i her denemede yeniden hesapla → commit sonrası kurguda parmak izi değişir, defter 400 `IDEMPOTENCY_KEY_REUSED` döner, kırmızı.
- [ ] AC-26 (zorunlu) — Bütünlük: anahtar başka içerikle defterde (`KeyReusedDifferentPayload`) ya da tutucu hareket ters kaydedilmiş → `IntegrityError` + 409 `COUNT_ADJUSTMENT_INTEGRITY`; `resolve-integrity` yalnız `cycle-count.variances.resolve` ile; hareket yazılmaz.
- [ ] AC-27 (zorunlu) — Rezervasyon: AVAILABLE 10, ayrılmış 8, sayılan 1 (fark −9) → onay → defter 422 `INSUFFICIENT_STOCK` + `details.level` → `Adjustment.Refused`, `RefusalCode` ve `details` aynen, hareket yok, Variance Trace'te "rezervasyon engeli"; rezervasyon bırakılınca `retry` (`variances.post`) aynı anahtarla `Posted` (Q-9 = A). Sabotaj: 0181'in ret eşlemesinde 422'yi `Posted` ya da kısmi miktarla yeniden deneme yap → kırmızı.
- [ ] AC-28 (zorunlu) — Defter retleri aynen: `LOT_BLOCKED`, `LOT_EXPIRED`, `SERIAL_COUNT_MISMATCH`, kaynak satır yetmez `INSUFFICIENT_STOCK` → `Refused`, kod ve `details` değişmeden; satır miktarları değişmez.
- [ ] AC-29 (zorunlu) — Tek stok kapısı: `ExpiryQuarantineTypeExemptionTests` 0181'in tek poster çağrı yerini ve tek `InternalMovementPostingService` kullanıcısını (eklemeli ikili) bilir; 0181 yalnız `COUNT_GAIN` / `COUNT_LOSS` yazar. Sabotaj (ayrı ayrı): ikinci bir `_poster.PostAsync(` çağrısı ekle → kırmızı; 0181'den `LOCATION_TRANSFER` iste → 0181 birim testi kırmızı.
- [ ] AC-30 (zorunlu) — Tutulan stok (Q-8 = A): `QUARANTINE` / `QUALITY_INSPECTION` / `BLOCKED` kovalar anlık görüntüde satır olmaz, sayım formunda salt okunur bilgi; bu kovalar için hiçbir `COUNT_*` hareketi doğmaz; her `COUNT_GAIN` `toStockStatus = AVAILABLE` taşır. Sabotaj: anlık görüntü süzgecinden statü koşulunu kaldır → `QUARANTINE` satırı doğar, kırmızı.
- [ ] AC-31 (zorunlu) — Ret gerekçe ister (400 `COUNT_REASON_REQUIRED`); `Rejected` satırda hareket yok; iptal yalnız posting yokken ve gerekçeyle.
- [ ] AC-32 (zorunlu) — Yeniden sayım: seçili satırlar için `RecountOfId`'li yeni sayım açılır ve yeni anlık görüntü alır; eski satırlar `Recounted`; zincir `MaxRecounts`'u (varsayılan 2) aşınca 409 `COUNT_RECOUNT_LIMIT`; yeniden sayım yalnız elle tetiklenir, önceki sayanın yeniden atanması reddedilmez (Q-11 = A).
- [ ] AC-33 (zorunlu) — Kapanış: her satır son durumunda iken sayım `Closed`; `Closed` sayımda her yazma 409 `COUNT_INVALID_STATE`.
- [ ] AC-34 (zorunlu) — Muafiyet: recall'daki lotta `COUNT_LOSS` / `COUNT_GAIN` 0181 tarafından engellenmez ve defter tür muafiyetiyle kabul eder; FEFO gerekçesi istenmez. Sabotaj: 0181'e recall okuması ekleyip ret ver → kırmızı.
- [ ] AC-47 (zorunlu, MOD-0173 seam — XMC-001 sahip onayıyla) — Elle `COUNT_*` izni (Q-21): `POST /api/inventory/movements` `movementType = COUNT_GAIN` ya da `COUNT_LOSS` ile, `inventory.movements.create` taşıyan ama `inventory.count-adjustments.create` (öneri ad) taşımayan kullanıcıdan 403, defterde hareket ve ret satırı yok; iki izinle 201; diğer elle türler yalnız `inventory.movements.create` ile değişmeden çalışır; 0181'in süreç içi posting'i (AC-23) bu izni istemez. Sabotaj (ayrı ayrı): tür bazlı ek denetimi kaldır → izinsiz `COUNT_GAIN` post edilir, kırmızı; denetimi `InternalMovementPostingService`'e taşı → 0181 posting'i 403 alır, AC-23 kırmızı.
- [ ] AC-48 (zorunlu) — Seri kontrollü satır (Q-10 = A): fark hesaplanır ve Variance Trace'te görünür; `approve` 422 `COUNT_SERIAL_NOT_POSTABLE`, hareket yok; tolerans içi olsa da otomatik `Approved` olmaz; `reject` / `recount` çalışır. Sabotaj: 0181'in seri denetimini kaldır → `approve` 422 `COUNT_SERIAL_NOT_POSTABLE` dönmez, kırmızı.

**DL-4 — ekranlar, 7 dil, 375 px**

- [ ] AC-35 (zorunlu) — `Views/Warehouse/CycleCounts/*.cshtml`, `Views/Warehouse/CountVariances/*.cshtml` (ve DL-5'te `CountEvidenceExports`) dosyalarının her birinde `Layout = "_LayoutTenantShell"` açıkça yazılı.
- [ ] AC-36 (zorunlu) — `python3 .antigravity/scripts/verify_datatable_page.py . --area Warehouse --module CycleCounts` ve `--module CountVariances` (DL-5: `CountEvidenceExports`) PASS; `data_mode: server` sayfa beyanıyla aynı; `quality-gate-datatable` PASS.
- [ ] AC-37 (zorunlu) — 7 dil: her resx anahtarı en, tr, fr, es, zh, ar, ru'da var (parite testi); `ar` sağdan sola düzgün; kullanıcıya görünen hardcoded metin yok; hata kodlarının UI metinleri 7 dilde.
- [ ] AC-38 (zorunlu) — UAS-001: `cycle-count.counts.read` olmayan kullanıcı `/Warehouse/CycleCounts`'ta başlık, kart, boş tablo ya da eylem düğmesi görmez; tek açıklama + ne yapılacağı; yönlendirme yok; `variances.read` olmayan için Variance Trace aynı. Sabotaj: izin denetimini kaldırıp iskeleti çiz → UAS-001 testi kırmızı.
- [ ] AC-39 (zorunlu) — Sayım formu 375 px genişlikte canlı ölçülür: yatay kaydırma yok, miktar girişi, "bulunan stok ekle" ve submit kullanılabilir (Q-12 = A).
- [ ] AC-40 (zorunlu) — Dashboard KPI'ları okuma anında hesaplanır (saklanan toplam yok): açık sayım, onay bekleyen fark, `Refused` düzeltme, dönem doğruluğu (fark 0 satır / sayılmış satır); test veriyle karşılaştırır.
- [ ] AC-41 (zorunlu) — Manifest sayfaları (`CYCLECOUNTS`, `COUNTSHEET`, `COUNTVARIANCES`) ve her gerçek düğme için bir eylem; `python3 scripts/check_pack_nav_keys.py` PASS.
- [ ] AC-42 (zorunlu) — Sayfa yardımı: `Help.{KOD}.Purpose`, `.Audience`, `.Step{n}Title` / `.Step{n}` (§25) 7 dilde; metinsiz sayfa yok.

**DL-5 — Evidence Pack**

- [ ] AC-43 (zorunlu) — Dışa aktarım: sayım başlığı, kapsam, `SnapshotAt`, satırlar (beklenen, hareket farkı, sayılan, fark), kararlar (kim / ne zaman / gerekçe), defter `TransactionNumber`'ları ve zaman çizelgesi; dosya `wms_count_evidence_files`'ta saklanır, `Sha256` kayıtta; indirilen dosyanın özeti kayıttakiyle aynı; kayıt insert-only; biçim PDF ve CSV, ikisi de aynı içerik (Q-18 = A).
- [ ] AC-44 (zorunlu) — Dışa aktarım ve indirme kiracı / şirket kapsamlı (başka kiracı 404) ve `cycle-count.evidence.export` / `.read` ister.

**DL-6 — Gateway**

- [ ] AC-45 (zorunlu) — Gateway kapsama testi (`Mod0181CycleCountRouteTests`, ayrı dosya) 0181'in her ucunu mevcut `/api/warehouse/{everything}` rotasıyla 5067'ye yönlendirildiği haliyle doğrular; `ocelot.json` değişmez (Q-15 = A) — integration-agent.

**İsteğe bağlı**

- [ ] AC-46 (isteğe bağlı) — Sayım formunda opak `scanRef` alanı kabul edilir ve saklanır, doğrulanmaz (MOD-0182 kapısı; Q-12 = A).

## 17. Test Expectations
- **Birim (Domain, saf):** durum makinesi geçişleri (§8.3 tablosu tam), fark formülü (§8.4; ondalık, sıfır beklenen, negatif), tolerans
  (mutlak + yüzde, beklenen 0), dört göz, kapsam açma, kör sayım DTO eşlemesi, idempotency anahtarı biçimi (≤ 128).
- **Entegrasyon (Mongo replica set):** InventoryService test paketi `INVENTORY_TEST_MONGO` yoksa kendi replica set'ini başlatır (çıkarım: MVP-1
  ortam notu; bu oturumda koşulmadı). AC-2, AC-4, AC-5, AC-8…AC-13, AC-17…AC-34 gerçek defter (`InternalMovementPostingService`) ile; sahte
  defter yalnız 5xx / iptal enjeksiyonunda (AC-25).
- **MOD-0173 seam (AC-47):** elle `COUNT_*` izin testi MOD-0173'ün test ağacında, sahip onayıyla (XMC-001).
- **Mimari:** AC-3 (0181'e özel tarama + `SingleSystemOfRecordArchitectureTests` eklemeli bildirimi), AC-7 (`AuthorizationGuardTests`),
  AC-11 (`CentralAuditDeclarationTests`), AC-29 (`ExpiryQuarantineTypeExemptionTests`). Servise dokunulduğunda **bütün** InventoryService
  test paketi koşar (filtre yok; MVP-1 CT kuralı).
- **Sabotaj kanıtı:** §16'daki her "Sabotaj:" maddesi kabul kaydında komut + kırmızı çıktı + geri alma ile yazılır.
- **Frontend:** `verify_datatable_page.py` (AC-36), RESX parite 7 dil (AC-37), UAS-001 (AC-38), 375 px canlı ölçüm (AC-39), tarayıcı smoke
  (E3) — sayım aç → release → giriş → submit → onay → defter hareketi.
- **Build:** `Diten.InventoryService`, `Diten.Web` PASS; Gateway test projesi (AC-45) PASS (Q-15 = A — `ocelot.json` değişmez).
- **Sözleşme:** WMS-COUNT-BUNDLE taslağına karşı sözleşme testi (`WmsDraftContract.cs` emsali, `c101e22fa:INVT/Warehouse/WmsDraftContract.cs`).

## 18. Ready-for-dev Checklist
- [ ] Golden Reference Slim (DEV-0000 + gerçek kod) referans olarak okundu — form alan sayısı Q-1 / Q-8 kararlarından sonra yeniden sayıldı (r1: 8 → slim, §4.1); DEV-0000 pack metni hâlâ açılmadı
- [ ] Frontmatter zorunlu alanları dolu (service, shell, golden_reference, data_mode, screens, entity_base)
- [ ] Layout & Shell Contract'ta `_LayoutTenantShell` açıkça yazılı
- [ ] Backend File Convention Golden Reference ile birebir
- [ ] Frontend File Contract Slim dosya listesi tam (Compact dosyası yok)
- [ ] Validation Rules her alan için yazılı
- [ ] Failure Path en az 4 senaryo (duplicate, missing, unauthorized, concurrency) — yazılı
- [ ] Authorization Convention: namespace (Q-16 = A, karar verildi), anahtarlar, politika, aktör
- [x] Gateway kararı (Q-15 = A) verildi; integration-agent işi tanımlı (AC-45)
- [ ] Acceptance criteria test edilebilir; her guard sabotajlı
- [ ] Test expectations build / verifier / RESX / smoke kapsıyor
- [ ] Audited Events tablosu her yazma komutunu tam bir kez içeriyor (AUD-001)
- [ ] Sayfa yardım metni her sayfa için yazılı (UXH-001); tur yok
- [x] Açık kararlar Q-1…Q-24 sahip kararıyla kapandı (r1, 2026-11-06, §19.4); iki uygulama notu teyit bekliyor (§19.4 "Kalan açık nokta": PKS-001 §2, Q-22 / Q-7 okunuşu)
- [ ] XMC-001 seam'leri (§5) sahiplerinden onay aldı
- [ ] WMS-COUNT-BUNDLE taslağı yazıldı
- [ ] Bağımsız inceleme (CT-APR-02) ve onay satırı (CT-APR-01)

## 19. Implementation Notes

### 19.1 Okunan kaynaklar
- Blueprint 8.8 `Blueprint_Data` satır 182 (xlsx, bu oturumda ZIP / XML ile okundu; satır 1 başlık). Kardeş satırlar: 174 MOD-0173, 179 MOD-0178,
  181 MOD-0180, 183 MOD-0182 — hepsi `Buy/Partner`.
- `execution/registries/module-id-registry.md:315`; `execution/portfolio/delivery-capability-packs/DCP-009-supply-chain-inventory.md:16`;
  `execution/domains/supply-chain-execution/domain-config.md` (WMS listesi `:27`, tek SoR, K15 tek-yazıcı seam, contract-first, DEC-INV-18, OD-5).
- `docs/analysis/inventory-capability-scope-and-dependency-report.md:98`, `:102`, `:260`, `:574`;
  `docs/roadmap/plans/integration-phase-and-mvp5-start-plan-2026-10-21.md:78`, `:124`.
- `SCE/MOD-0173-inventory-ledger-valuation.md:43`, `:225-226`, `:714-717`; `SCE/MOD-0173-FU01-inventory-ledger-ui.md:335-336`;
  `SCE/MOD-0173-FU03-inventory-lot-location-reservation.md:385-386`, `:1262`; `SCE/MOD-0174-lot-serial-tracking.md:396`;
  `SCE/MOD-0175-FU01-quarantine-hold-source-reversal.md:791`, `:1120-1124`; `SCE/MOD-0176-expiry-fefo-management.md:347-349`;
  `SCE/MOD-0177-recall-readiness.md:547`.
- `SCE/MOD-0178-putaway-picking-packing.md:96-97` (W-1), `:110-137`, `:139-181`, `:183-247`, `:364-375` (lokasyon rolü), `:550-560` (seam'ler),
  `:585-611` (bağımlılıklar), `:1023-1072` (merkezi denetim), `:1580-1582`, `:2058-2146` (izinler), `:2168-2174` (AC-6), `:2288-2294`
  (sabotaj listesi), `:3038`, `:3041-3050`, `:3105-3127`, `:3129-3140`.
- `execution/domains/platform-shared-services/module-packs/MOD-0040-FU02-record-code-service.md:1-25`, `:160-169`, `:1639`, `:1848`;
  `docs/analysis/contracts/record-code-allocate.md:1-15`.
- Kurallar: `AGENTS.md` §3 (`:81`), §6.1 (`:151-198`); `.antigravity/rules/module-pack-standard.md` (tamamı);
  `permission-key-standard.md` §1, §4, §5; `record-coding-standard.md` §1–§3, §5.5, §6; `audit-trail-standard.md` §4.3–§4.4, §5, §6;
  `multi-tenancy.md:54`; `unauthorized-surface-standard.md` (başlıklar); `cross-module-change-consent.md` (XMC-001, ad düzeyinde);
  `localization-standard.md` (ad düzeyinde — 7 dil kuralı AGENTS Çekirdek madde 4'ten).
- Golden Reference: `DEV-0001-golden-reference-compact.md:1-25`; `frontend/Diten.Web/Views/DevEnablement/GoldenReferenceSlim/` ve
  `GoldenReferenceCompact/` dosya listeleri; `services/Diten.DevEnablementService/…/Features/GoldenReferenceCompact/` ağacı.
  DEV-0000 pack metni ayrıca açılmadı (yapı module-pack-standard §4–§5 ve kod ağacından).
- `docs/guides/operations/control-tower-sop.md:1656-1662` (kanıt seviyeleri); `scripts/pack_approval.py:116-124`, `:1337-1385` (dilim tablosu
  okuyucusu); `tests/architecture/audit-ledger/Diten.InventoryService.md:8-16`.

### 19.2 Ölçülen gerçekler
Hepsi `c101e22fa` (salt okuma, `git show` / `git grep`):
- `INV/Domain/Enums/MovementType.cs:19-20` `CountGain`, `CountLoss`; `INV/Domain/Rules/MovementDirectionRules.cs:23` inbound, `:25` outbound;
  `INV/Domain/Rules/WireNames.cs:23-24` `COUNT_GAIN` / `COUNT_LOSS`.
- `INV/Domain/Expiry/Rules/FefoRules.cs:170-183` (`ExpiryHookScope.Exempt` `COUNT_*` içerir); `INV/Domain/Recall/Rules/RecallExitRule.cs:9-10`.
- `INV/Application/Features/Inventory/Services/InternalMovementPostingService.cs:35` sınıf, `:82` `PostAsync`, `:84-87` şirket kapsamı,
  `:96-102` idempotency önce, `:104-124` `reservationId` yalnız GI / LOCATION_TRANSFER, `:198-212` kapsam düzeyi `INSUFFICIENT_STOCK` + `level`,
  `:221-228` kaynak satır `INSUFFICIENT_STOCK`.
- `INV/Persistence/Repositories/InventoryLedgerRepository.cs:306-330` (net AVAILABLE azalışında rezervasyon bekçisi LOT → WAREHOUSE → SKU).
- `INV/Application/Features/Inventory/Ports/IPostingKeyLookup.cs:6-58`; `INV/Application/Common/InventoryContract.cs:28` (anahtar ≤ 128).
- `INV/Application/Features/Warehouse/Services/WarehouseMovementPoster.cs:14-21`; `INV/Application/Features/Warehouse/Ports/IWarehouseStockReader.cs:6-32`;
  `INV/Persistence/Warehouse/WarehouseStockReads.cs:14-44` (`Find`, `Limit(5000)`); `INV/Application/Features/Warehouse/Ports/IWarehouseRecordCodeAllocator.cs:10-21`;
  `INV/Application/Features/Warehouse/WarehouseContract.cs:9-22`; `INV/Application/Features/Warehouse/WarehouseCentralAudit.cs:12-18`;
  `INV/Api/Warehouse/PutawayPickingPackingManifestProvider.cs:21,32`.
- `INV/Domain/Warehouse/Enums/WarehouseEnums.cs:7-11` (`Receiving`, `Staging`); `INV/Domain/Warehouse/Enums/WarehouseTaskEnums.cs:5-9`
  (`Putaway`, `Pick`), `:45-51` (`ConfirmationStatus`); `INV/Domain/Warehouse/Entities/WarehouseTaskEntities.cs:156-165`.
- `INV/Application/Features/Quarantine/Services/QuarantineIntake.cs:45-61`; `INV/Persistence/Quarantine/QuarantineLedgerReads.cs:126-136`;
  `INV/Application/Features/Quarantine/Ports/QuarantinePorts.cs:15-24`; `INV/Application/Features/Quarantine/Services/QuarantineWorkflow.cs:21`;
  `INV/Domain/Expiry/Rules/ExpiryRules.cs:205`; `INV/Domain/Recall/Rules/RecallRules.cs:211`.
- `INV/Domain/Entities/EntityBase.cs:9-27`; `INV/Domain/Entities/InventoryTransaction.cs:14-22`;
  `INV/Application/Features/Inventory/Commands/PostMovementCommand.cs:14-55`.
- `INV/Api/Program.cs:46-59` (manifestler), `:66` (BL-501); `INV/Application/Common/InventoryCentralAudit.cs:22`;
  `services/Diten.Building.Blocks/src/Diten.BuildingBlocks.CentralAudit/CentralAuditPlatformValues.cs:37-52`.
- `INV/Infrastructure/Middleware/InventoryScopeResolutionMiddleware.cs:57-61`; `INV/Persistence/InventoryIndexConfiguration.cs:45,48,74`;
  `INV/Api/Controllers/InventoryController.cs:92-121` (bakiye ve hareket uçları; "as-of" bakiye okuması yok — `asOf` 0 eşleşme).
- Testler: `INVT/Architecture/SingleSystemOfRecordArchitectureTests.cs:68-80`, `:84-121`, `:124-142`; `INVT/CentralAudit/CentralAuditDeclarationTests.cs:41`,
  `:48-56`, `:116`; `INVT/Expiry/ExpiryQuarantineTypeExemptionTests.cs:25-39`, `:118-134`; `INVT/Warehouse/WarehouseRegistrationTests.cs:127-141`;
  `INVT/Architecture/AuthorizationGuardTests.cs:19`.
- `gateway/Diten.ApiGateway/ocelot.json:1134-1161` (`/api/warehouse` + `{everything}`); `gateway/Diten.ApiGateway.Tests/Mod0178WarehouseRouteTests.cs` (var).
- Değerleme alanı: `git grep -i "unitcost|movingaverage|valuation" c101e22fa -- INV/Domain` → 0 eşleşme.
- `CNT` öneki: `git grep '"CNT"\|-CNT-' c101e22fa -- services` → 0 eşleşme. `cycle-count.` anahtarı: 0 eşleşme (services + frontend).
- **Ölçülmedi:** Mongo snapshot okumasının depo deseninde kullanılabilirliği (§8.2 madde 4); `CreatedAt`'in commit sırasını yansıtıp yansıtmadığı
  (§8.4); seri kontrollü SKU'da `COUNT_GAIN`'in seri kaydı davranışı; kayıtlı olmayan lotla `COUNT_GAIN`'in defter cevabı; ABC sınıfı verisinin
  herhangi bir master'da var olup olmadığı; MOD-0023 / MOD-0022 / MOD-0048 sahipleri; DEV-0000 pack metni; SCE paketlerinin HEAD ile
  `c101e22fa` arasında aynılığı (çağıranın bildirimi).

### 19.3 Çıkarımlar
- Plan "yalnız 0173'e bağlı" (`:78`) Blueprint bağımlılık kapısıdır; çalışma zamanı kuralları 0173 ailesinden (FU02 / FU03 / 0174–0177) gelir
  ve bu paket onlara bağımlıdır (çıkarım: kapı ≠ çalışma zamanı bağımlılığı).
- `ExpectedQuantity` bakiyenin anlık kopyasıdır; ikinci bakiye sayılmamasının şartı güncellenmemesi ve stok kararı vermemesidir (AC-3, AC-4).
- Delta posting, onay ile posting arasındaki hareketlere dayanıklıdır; asıl risk **fiziksel sayım anı ile giriş anı** arasındaki harekettir —
  `MovementDuringCount` bayrağı bunu yakalar ama önlemez; kilit bilinçli olarak yoktur (Q-6 = B — depo durmaz).
- Staging'de ayrılmış stoğun sayımı `COUNT_LOSS`'ta sıkça rezervasyon reddi üretir (MOD-0178 S2 bağları); Q-9 kararı bu yüzden DL-3 kapısıdır.
- `form_field_count = 8` sınırdadır; Q-1 = A ve Q-8 = A alan eklemez / çıkarmaz → **slim** kalır (§4.1). Yeni bir yaratma alanı eklemek
  compact'a geçiş ve paket revizyonu demektir.
- `golden_reference` / `form_field_count` yalnız `CycleCounts` yaratma formu içindir; `CountVariances` ve `CountEvidenceExports` yaratma formu taşımaz.

### 19.4 Kararlar (r0'da açık; r1'de sahip kararıyla kapandı)
r0'da hiçbiri uydurulmadı; her biri öneri + seçenek + karar sahibi taşıyordu. **r1 (2026-11-06):** sahip (Beste) 24 kararın hepsini verdi —
Q-6, Q-13, Q-21, Q-22, Q-23 adıyla; geri kalan 19'u (Q-1…Q-5, Q-7…Q-12, Q-14…Q-20, Q-24) "öneri kabul". Öneri metni olduğu gibi korundu;
karar son sütundadır ve etkisi ilgili bölümlere işlendi (revizyon notu). "Q'ya bağlı" AC kalmadı.

| Kimlik | Soru | Öneri | Seçenekler | Karar sahibi | Karar (r1) |
|---|---|---|---|---|---|
| Q-1 | Sayım türleri ve kör sayım | `Cycle` + `Full`; `Blind` ayrı bayrak; kör sayımda beklenen submit'e kadar hiçbir cevapta yok | A öneri · B `Spot` türü ekle · C kör sayım yok | Beste | Karar (sahip, 2026-11-06): öneri kabul |
| Q-2 | Döngü sıklığı / ABC | v1 elle planlama; otomatik üretim FU-0181-ABC | A öneri · B ABC sınıfına göre otomatik sayım takvimi (ABC verisinin kaynağı ölçülmedi) | Beste; ABC verisinin sahibi TBD | Karar (sahip, 2026-11-06): öneri kabul |
| Q-3 | Onay eşiği (miktar / değer / %) | şirket ayarı mutlak + %; varsayılan 0 (her fark onay); değer eşiği yok (değerleme yok) | A öneri · B yalnız mutlak · C değer eşiği (değerleme gelince) | Beste (+ finans TBD) | Karar (sahip, 2026-11-06): öneri kabul |
| Q-4 | Onaylayan rol ve onay yolu | modül içi onay, `cycle-count.variances.approve`, "Onaylayıcı" rolü (AuthService WP) | A öneri · B MOD-0023 iş akışı (MOD-0175 emsali) | Beste; rol şablonu Ali (Auth) | Karar (sahip, 2026-11-06): öneri kabul |
| Q-5 | Dört göz | zorunlu: sayan / submit eden onaylayamaz; e-imza v1 yok | A öneri · B ayarlanabilir · C MOD-0022 e-imza | Beste | Karar (sahip, 2026-11-06): öneri kabul |
| Q-6 | Lokasyon kilidi mi, anlık görüntü + hareket farkı mı | B: kilit yok; anlık görüntü + `MovementsSinceSnapshot` + `MovementDuringCount` bayrağı + açık sayım çakışma yasağı | A defter posting'inde "sayımdaki bin" kilidi (MOD-0173 seam, XMC) · B öneri · C yalnız UI uyarısı | Beste (A seçilirse MOD-0173 sahibi MVP-1 CT) | **Karar (sahip, Beste, 2026-11-06): B** — lokasyon kilidi yok; anlık görüntü alınır, sonraki hareketler farka eklenir, depo durmaz; açık sayım çakışma yasağı (yalnız sayım–sayım, hareketi engellemez) kalır (§8.2, §8.4, AC-13, AC-19) |
| Q-7 | Görünen kod `{ŞİRKET}-CNT-…` (RCS-001) | OTOMATİK, önek `CNT`; MOD-0178 geçici portuna `CNT` türü (XMC) → MOD-0040-FU02 DL-8'e `CNT` eklenir; RCS-001 §3 satırı ayrı sahip onayı (korunan yol) | A ortak servisi bekle · B öneri | Beste (RCS K2 + 0178 sahibi) | Karar (sahip, 2026-11-06): öneri kabul |
| Q-8 | Karantina / tutulan stok sayımı; `COUNT_GAIN` statüsü | v1 yalnız `AVAILABLE` sayılır ve post edilir; tutulan kovalar salt okunur; kazanç `AVAILABLE`'a | A öneri · B tutulan kovaları say, farkı MOD-0175'e istisna olarak yönlendir · C GxP: bulunan stok `QUARANTINE`'e (MOD-0175 hold otomatik) | Beste (+ kalite TBD) | Karar (sahip, 2026-11-06): öneri kabul |
| Q-9 | Rezervasyonlu stok farkı (FU-03-COUNT) | A: reddet, `Refused` + "rezervasyon engeli", sahibi bırakınca `retry` | A öneri · B yalnız serbest miktarı post et, kalanı istisna · C 0181 rezervasyonu bırakır (FU03 seam) | Beste (FU03 sahibi MVP-1 CT) | Karar (sahip, 2026-11-06): öneri kabul |
| Q-10 | Seri / lot sayımı | lot düzeyi zorunlu; kayıtlı olmayan lot `Found`'da reddedilir; seri kontrollü SKU v1'de post edilmez | A öneri · B seri listesi girişi ve seri düzeyi düzeltme · C kayıtsız lotu kabul et | Beste | Karar (sahip, 2026-11-06): öneri kabul |
| Q-11 | Yeniden sayım kuralı | elle tetik; zincirde en çok 2; farklı sayan önerilir, zorunlu değil | A öneri · B fark eşiği üstünde otomatik yeniden sayım · C farklı sayan zorunlu | Beste | Karar (sahip, 2026-11-06): öneri kabul |
| Q-12 | Mobil 375 px ve barkod | sayım formu 375 px zorunlu (AC-39); barkod yok, opak `scanRef` (AC-46) | A öneri · B MOD-0182'yi bekle | Beste; MOD-0182 / MOD-0365 sahibi TBD | Karar (sahip, 2026-11-06): öneri kabul |
| Q-13 | Build vs Blueprint `Buy/Partner` | Build (MOD-0178 W-1 emsali); Blueprint düzeltmesi EA'ya FU | A Build · B Buy / Partner | Beste / EA | **Karar (sahip, Beste, 2026-11-06): A — Build** (içeride yazılır). Gerekçe: stok defteri (MOD-0173) bizde; dış ürün bu defterle konuşamaz. Blueprint `Buy/Partner` sapması W-0181-01 (§0); düzeltme EA'ya (FU-0181-EA) |
| Q-14 | AI Assist kapsamı | MVP-5'te dışarıda; FU-0181-AI | A öneri · B Summarize (fark özeti) HITL ile | Beste | Karar (sahip, 2026-11-06): öneri kabul |
| Q-15 | API taban yolu ve Gateway | A: `/api/warehouse/…` altında, `ocelot.json` değişmez; 0178 rota ailesi onayı | A öneri · B `/api/cycle-counts` yeni rota | Beste (0178 sahibi) + integration-agent | Karar (sahip, 2026-11-06): öneri kabul |
| Q-16 | İzin namespace'i ve manifest kodu | `cycle-count.*`; `CYCLE-COUNTING` | A öneri · B başka ad | Beste | Karar (sahip, 2026-11-06): öneri kabul |
| Q-17 | Hareket gerekçesi (MOD-0048) | sabit opak kod `CYCLE_COUNT_VARIANCE`; MOD-0048 listesi gelince bağlanır | A öneri · B MOD-0048'den seçmeli gerekçe | TBD (MOD-0048 sahibi) | Karar (sahip, 2026-11-06): öneri kabul |
| Q-18 | Evidence Pack biçimi | PDF + CSV (expiry / recall kanıt dışa aktarımı emsali) | A öneri · B yalnız CSV | Beste | Karar (sahip, 2026-11-06): öneri kabul |
| Q-19 | Değerleme etkisi raporu / posting tarihi | v1 dışı; `postingDate` = ilk deneme günü | A öneri · B dönem kapanışına göre tarih kuralı | Beste (+ finans TBD) | Karar (sahip, 2026-11-06): öneri kabul |
| Q-20 | Denetim sınıfı (BL-501 bağlı, ölçüldü) | K2 sınıfı: GxP stok kaydı; `RequireEnlistment` kapalı-başarısız | A öneri · B K1 | Beste | Karar (sahip, 2026-11-06): öneri kabul |
| Q-21 | MOD-0173 elle `COUNT_GAIN` / `COUNT_LOSS` (`MOD-0173 :714-717`) 0181'den sonra kalır mı | v1 kalır, 0181 dokunmaz | A öneri · B elle `COUNT_*`'ı yalnız 0181'e bırak (MOD-0173 değişikliği, XMC) | Beste | **Karar (sahip, Beste, 2026-11-06):** elle `COUNT_GAIN` / `COUNT_LOSS` MOD-0173'te **kalır, izinle sınırlanır** — ek anahtar `inventory.count-adjustments.create` (öneri ad; MOD-0173 paketi kaydeder); MOD-0173'e XMC-001 eklemeli değişiklik, sahibi MVP-1 CT (Beste) (§2.4, §5, AC-47) |
| Q-22 | Sayım görevi modeli | 0181'in kendi kaydı (`wms_count_*`); `wms_tasks`'a `Count` türü eklenmez | A öneri · B MOD-0178 `WarehouseTask` + `Kind = Count` (XMC) | Beste | **Karar (sahip, Beste, 2026-11-06): A** — sayım görevi MOD-0181'in kendi kaydı (`wms_count_*`); MOD-0178 görevi kullanılmaz, MOD-0178'e (görev modeli / görev kodu) dokunulmaz |
| Q-23 | Kod hattı / dal | sahip kararı: MVP-1 tek çalışma dalı mı, `feature/sce/mod-0181-cycle-counting` mı | A tek çalışma dalı · B ayrı dal | Beste | **Karar (sahip, Beste, 2026-11-06): A** — dal `feature/sce/mod-0173-inventory-ledger-valuation` (tek çalışma dalı; künye `branch`) |
| Q-24 | Bir sayımın satır sınırı | 2000 satır (`MaxLinesPerCount`, ayarlanabilir) | A öneri · B sınır yok (bakiye okuması 5000 sınırı emsali) | Beste | Karar (sahip, 2026-11-06): öneri kabul |

**Kalan açık nokta (r1 — yeni karar değil, kapanan kararların uygulama notu; sahip teyidi gerekir):**
1. **Q-21 / PKS-001 §2:** `COUNT_*` için ek izin, `inventory.movements.create`'in tek başına verdiği erişimi daraltır. Anahtar "yayımlanmış"
   sayılıyorsa (seed / rol satırı / JWT / gönderilmiş `[HasPermission]`) PKS-001 §2 daraltmayı yasaklar; yayımlanma durumu bu oturumda
   ölçülmedi. Sahip (MVP-1 CT, Beste) ya anahtarın henüz yayımlanmadığını teyit eder ya da yolu seçer (ör. yeni anahtar + takma ad, PKS-001 §3).
   Anahtar adı `inventory.count-adjustments.create` öneridir; kesin ad MOD-0173 paketinin revizyonundadır.
2. **Q-22 ile Q-7 = B'nin okunuşu:** Q-22 "MOD-0178'e dokunulmaz" bu pakette **görev modeli / görev kodu** kapsamında okundu; Q-7 = B (kabul
   edilen öneri) MOD-0178'in kayıt kodu portu `IWarehouseRecordCodeAllocator`'a eklemeli `CNT` türü ister (§5, §8.12). Sahip Q-22'yi MOD-0178'in
   bütün dosyaları için kastettiyse Q-7 = B uygulanamaz ve Q-7 yeniden açılır (A: ortak servisi bekle). Teyit gerekir.

**Plan hizalaması (2026-11-01 kuralı):** bu oturuma modülün plan satırı (`1.x.y` yetenek kimliği, plan MVP kimliği) verilmedi; plan dosyası depoda
değildir. Dilim tablosunda `plan kimliği yok` / `plan satırı yok` yazıldı. Depodaki yol haritası belgesi MVP-5'i söyler (`integration-phase-and-mvp5-start-plan-2026-10-21.md:78`).

## 20. Follow-up Items
- **FU-0181-ABC** — ABC sınıfına göre otomatik döngü (Q-2).
- **FU-0181-AI** — Blueprint AI Assist (Summarize / Extract / Recommend / Route) ve AI kapıları (Q-14).
- **FU-0181-VAL** — değerleme etkisi raporu ve değer eşiği (Q-3, Q-19); MOD-0173 değerleme motoruna bağlı.
- **FU-0181-HELD** — tutulan kovaların sayımı ve MOD-0175'e yönlendirme (Q-8 B), FU-0175-FU01-OUTBOUND ile birlikte.
- **FU-0181-SERIAL** — seri düzeyi sayım (Q-10 B).
- **FU-0181-BULK** — toplu onay / ret.
- ~~FU-0181-LOCK~~ — düştü: Q-6 = B (kilit yok, karar 2026-11-06).
- **FU-0181-REASON** — hareket gerekçesinin MOD-0048 listesine bağlanması (Q-17 = A; v1 sabit `CYCLE_COUNT_VARIANCE`).
- **FU-0181-RCS** — ortak kayıt kodu servisine geçiş (MOD-0040-FU02 DL-8 + FU-0178-RCS-01 ile; Q-7 = B).
- **FU-0181-EA** — Blueprint Build/Buy düzeltmesi (W-0181-01, Q-13 = Build) — EA'ya açık iş.
- **FU-0181-READAUDIT** — kanıt indirme okumalarının denetimi (AUD-001 §7).
- **FU-0181-BARCODE** — MOD-0182 tarama olaylarının `scanRef` kapısına bağlanması.

## 21. Audited Events

Servisin denetim altyapısı **bugün var**: `tests/architecture/audit-ledger/Diten.InventoryService.md:12` (`inventory-merkezi-iletim | b | işaret |
ICentralAuditDeclaration`); `RequireEnlistment` açık (`c101e22fa:INV/Api/Program.cs:66`). Kayıt ad ve gövde taşımaz; "olay adı" = `RequestType`
(komut sınıfı) + `Operation` (Platform enum adı, `CentralAuditPlatformValues.cs:37-52`); kategori `MasterData`; `SourceModule` = manifest kodu
(`CYCLE-COUNTING`, Q-16 = A). Önceki / sonraki ya da değişen alanlar yalnız `wms_count_events.Data`'dadır (aynı işlem, silinmez). Başarılı ve yazan komut 1 kayıt;
replay / 0 kayıt 0; başarısız iletilen 0 (§8.11). K2 sınıfı (Q-20 = A, karar 2026-11-06).

| Komut | Olay adı | Nesne (tür) | Yol | Önceki/sonraki ya da değişen alanlar | İstisna sınıfı + gerekçe |
|---|---|---|---|---|---|
| CreateCycleCountCommand | CreateCycleCountCommand + Create | CycleCount | b | Sonraki: Number, WarehouseId, CountType, Blind, kapsam, PlannedDate, AssignedTo, Note | — |
| UpdateCycleCountCommand | UpdateCycleCountCommand + Update | CycleCount | b | Önceki / sonraki: değişen form alanları | — |
| DeleteCycleCountCommand | DeleteCycleCountCommand + Delete | CycleCount | b | Önceki: Number, Status | — |
| BulkDeleteCycleCountCommand | BulkDeleteCycleCountCommand + Delete | CycleCount (toplu, EntityId boş) | b | silinen her kaydın `Deleted` olayı | — |
| ReleaseCycleCountCommand | ReleaseCycleCountCommand + LifecycleTransition | CycleCount | b | SnapshotAt, ScopeBinIds sayısı, satır sayısı | — |
| CancelCycleCountCommand | CancelCycleCountCommand + LifecycleTransition | CycleCount | b | önceki durum, gerekçe | — |
| RecordCountLineCommand | RecordCountLineCommand + Update | CycleCountLine | b | önceki / sonraki CountedQuantity, ScanRef | — |
| AddCountLineCommand | AddCountLineCommand + Create | CycleCountLine | b | Bin, SKU, lot, CountedQuantity | — |
| SubmitCycleCountCommand | SubmitCycleCountCommand + LifecycleTransition | CycleCount | b | satır başına fark özeti, bayraklar | — |
| ApproveCountVarianceCommand | ApproveCountVarianceCommand + LifecycleTransition | CycleCountLine | b | fark, karar, posting sonucu (Pending / Posted / Refused) | — |
| RejectCountVarianceCommand | RejectCountVarianceCommand + LifecycleTransition | CycleCountLine | b | gerekçe | — |
| RequestCountRecountCommand | RequestCountRecountCommand + Create | CycleCount (yeni) | b | RecountOfId, satırlar | — |
| RetryCountAdjustmentCommand | RetryCountAdjustmentCommand + Execute | CycleCountLine | b | deneme sayısı, sonuç, TransactionNumber | — |
| ResolveCountAdjustmentIntegrityCommand | ResolveCountAdjustmentIntegrityCommand + Execute | CycleCountLine | b | tutucu hareket, karar | — |
| ReconcilePendingCountAdjustmentCommand | ReconcilePendingCountAdjustmentCommand + Execute | CycleCountLine | b | lookup sonucu, Posted / IntegrityError (yalnız yazacak şey varsa gönderilir; MOD-0178 sweeper deseni) | — |
| UpdateCycleCountSettingsCommand | UpdateCycleCountSettingsCommand + Update | CycleCountSettings | b | önceki / sonraki tolerans, MaxRecounts, MaxLinesPerCount | — |
| CreateCountEvidenceExportCommand | CreateCountEvidenceExportCommand + Export | CountEvidenceExport | b | CountId, Format, Sha256 | — |

Kayda yazılmayacak alan: yok (parola / token / özel nitelikli veri taşınmaz).

## 22. Record Codes

| Kayıt türü | Sınıf | TÜR öneki (öneri) | Üretici | Gerekçe |
|---|---|---|---|---|
| Sayım (`CycleCount.Number`) | OTOMATİK | `CNT` | MOD-0178 geçici portu → MOD-0040-FU02 (Q-7 = B, karar 2026-11-06) | yüksek hacimli, kullanıcı değiştirmez |
| Sayım satırı | — | kod yok | — | kullanıcıya görünen kodu yok (bin kodu + SKU gösterilir) |
| Kanıt dışa aktarımı | — | kod yok | — | sayım numarasıyla anılır |

Biçim `{ŞİRKET}-CNT-{000001}`, sayaç (kiracı, şirket öneki, tür) başına, sıfırlanmaz, ASCII; kiracı genelinde tekil (RCS-001 §6). `CNT`'nin
RCS-001 §3 tablosuna yazılması korunan yol değişikliğidir (ayrı sahip onayı; Q-7 = B kararı bu satırı yazmaz).

## 23. Teslim dilimleri

| dilim | ad | kapsadığı bölümler | gerekli kanıt seviyesi | plan yetenek kimliği | plan MVP |
|---|---|---|---|---|---|
| DL-1 | çekirdek: sayım kaydı ve plan (yarat / düzenle / sil) + salt okunur beklenen anlık görüntü (release) + izin + kiracı + denetim + kayıt kodu (arka uç) | Owned Objects, Entity Fields, Indexes, Repo Scope, Protected Paths, Dependencies, Servis ve kapsam, Beklenen miktar anlık görüntüsü, Durum makinesi, Merkezi denetim ve yerel iz, Kayıt kodu üretimi, Backend File Convention, Validation Rules, Failure Path to Verify, Authorization Convention, Audited Events, Record Codes | E4 | plan kimliği yok | plan satırı yok |
| DL-2 | sayım girişi + bulunan stok + submit + fark hesabı + kör sayım (arka uç) | Fark hesabı, Durum makinesi, Lot ve seri, Validation Rules, Failure Path to Verify, Audited Events | E4 | plan kimliği yok | plan satırı yok |
| DL-3 | onay + eşik + dört göz + COUNT_GAIN / COUNT_LOSS posting seam + kurtarma + rezervasyon reddi + yeniden sayım + seri satır kuralı (arka uç); MOD-0173 elle `COUNT_*` izni (XMC seam, Q-21) | Onay ve eşik, Defter kaydı ve kurtarma, Rezervasyon uzlaştırması, Karantina ve tutulan stok, Muafiyetler, Hata kodları, Failure Path to Verify, Audited Events | L3 + E5 | plan kimliği yok | plan satırı yok |
| DL-4 | ekranlar (Cycle Count Dashboard, sayım formu, Variance Trace) + manifest + 7 dil + 375 px + yardım | Layout & Shell Contract, Frontend File Contract, Ekranlar ve Blueprint soft pages, Page Help (UXH-001) | E4 | plan kimliği yok | plan satırı yok |
| DL-5 | Evidence Pack dışa aktarımı (arka uç + ekran) | CountEvidenceExport, Ekranlar ve Blueprint soft pages, Audited Events | E4 | plan kimliği yok | plan satırı yok |
| DL-6 | Gateway kapsama testi ya da yeni rota (integration-agent; DL-1'den sonra) | Gateway / API Routing Decision | E3 | plan kimliği yok | plan satırı yok |

## 24. Dilim kapıları ve bağlı kararlar

Paket onaylanmadan (künye `approved` / `ready-for-dev` + CT-APR-02 inceleme kaydı + CT-APR-01 onay satırı) hiçbir dilim başlamaz.

| Dilim | Başlamadan kapanması gereken kararlar | Seam'ler (XMC-001) | AC |
|---|---|---|---|
| DL-1 | kararlar kapandı (Q-1, Q-6, Q-7, Q-13, Q-15, Q-16, Q-20, Q-22, Q-23, Q-24 — r1); kalan: WMS-COUNT-BUNDLE taslağı, Q-22 / Q-7 okunuş teyidi (§19.4) | `SingleSystemOfRecordArchitectureTests`, `CentralAuditDeclarationTests`, `Program.cs`, `IWarehouseRecordCodeAllocator` (`CNT`, Q-7 = B) | AC-1…AC-13 |
| DL-2 | kararlar kapandı (Q-1, Q-6, Q-10 — r1) | `CentralAuditDeclarationTests` (sayı) | AC-14…AC-20 |
| DL-3 | kararlar kapandı (Q-3, Q-4, Q-5, Q-8, Q-9, Q-10, Q-11, Q-17, Q-19, Q-21 — r1); kalan: Q-21 PKS-001 §2 notu (§19.4) | `ExpiryQuarantineTypeExemptionTests`, `CentralAuditDeclarationTests`; MOD-0173 izin seam'i (§2.4, Q-21) | AC-21…AC-34, AC-47, AC-48 |
| DL-4 | karar kapandı (Q-12 — r1); AuthService rol şablonu WP'si (Q-4 = A, Q-21; Ali) kabul kapısı | — | AC-35…AC-42, AC-46 |
| DL-5 | karar kapandı (Q-18 — r1) | `CentralAuditDeclarationTests`, `SingleSystemOfRecordArchitectureTests` (kanıt koleksiyonları) | AC-43, AC-44 |
| DL-6 | karar kapandı (Q-15 = A — r1) | gateway test projesi (integration-agent); `ocelot.json` değişmez | AC-45 |

Sıra: DL-1 → DL-2 → DL-3 → DL-4 (DL-2'den sonra DL-3 ile paralel yürüyebilir) → DL-5; DL-6 DL-1'den sonra her an. Bütün dilimler tek çalışma
dalında yürür: `feature/sce/mod-0173-inventory-ledger-valuation` (Q-23 = A, karar 2026-11-06). MOD-0178 ile paralel
yürür (`SCE/MOD-0178-putaway-picking-packing.md:136`); paylaşılan test dosyalarında (§5) sayı / liste çakışması birleştirmede sırayla çözülür.

## 25. Page Help (UXH-001)
- `CYCLECOUNTS` → `Help.CYCLECOUNTS.Purpose`, `.Audience`, `.Step1Title`/`.Step1` … `.Step4` (1: depo ve kapsamı seç, 2: sayımı başlat — anlık
  görüntü alınır, 3: sayım formunda miktarları gir, 4: gönder ve farkları izle); `.Immediate` ("Başlatılan sayımın kapsamı değişmez; iptal
  gerekçe ister"); ilgili `Help.CYCLECOUNTS.Related.COUNTVARIANCES`.
- `COUNTSHEET` → `Purpose`, `Audience`, `Step1..3` (bin bin say, bulunan stoğu ekle, gönder); kör sayımda beklenen gösterilmez notu.
- `COUNTVARIANCES` → `Purpose`, `Audience`, `Step1..4` (farkı incele, onayla / reddet / yeniden say, rezervasyon engelinde "Yeniden dene",
  defter hareketine git); `.Immediate` ("Onay stoğu hemen düzeltir; geri alma Stok Hareketleri'nde ters kayıttır"); `Related.CYCLECOUNTS`.
- `COUNTEVIDENCE` (DL-5) → `Purpose`, `Audience`, `Step1..2` (sayımı seç ve dışa aktar, dosyayı indir).
- Kiracı modülü → 7 dil. **Tur yok** (beş akıştan biri değil).
