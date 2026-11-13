# WP-MOD0178-ANTIGRAVITY-ROWS — MOD-0173 / MOD-0178: `.antigravity` kural satırları (AS-1 PKS-001 §4, AS-2 RCS-001 §3)

| Alan | Değer |
|---|---|
| Modüller | MOD-0173 Inventory Ledger & Valuation (`inventory.*`), MOD-0178 Putaway / Picking / Packing (`warehouse.*`, kayıt önekleri) |
| Kaynak | WP-MOD0181-ANTIGRAVITY-ROWS §4 (AS-1, AS-2; sahip kararı 2026-11-10 "ayrı paket olarak yaparız"); MOD-0178 pack FU-0178-PKS-01 (F-14), FU-0178-RCS-02, Q-07 = B, Q-08, Q-EA-4; MOD-0173 pack §14 (namespace kararı 2026-10-01); MOD-0181 inceleme F-17 notu |
| Tür | Kural belgesi (korunan yol) + Platform testinde bir sözlüğe dört satır. **Üretim kodu yok.** |
| Onaylayan | Beste (AGENTS.md Çekirdek madde 6: `.antigravity/**` kullanıcı onayı; madde 9 XMC-001: Platform test dosyası) |
| Yazan | MVP-5 CT, sahip onayından sonra |
| Yer | MVP-1 hattı `feature/sce/mod-0173-inventory-ledger-valuation` (Platform test dosyası ve `RuleAheadOfCatalogue` yalnız hatta var) |
| Kapanış adımı | Hat PR'ı açılmadan önce (dilimleri ve geliştirmeyi engellemez) |
| Durum | **Onaylandı** (Beste «İş paketini onaylıyorum», 2026-11-10) — CT yazdı, §2 metni birebir; AC-1…AC-4 ölçüldü (Platform `RecordCodeTypeCatalogTests` 13/13; sabotaj `PKG` satırı silindi → AC16 kırmızı, bayt-eşit geri) |

## 1. Değişecek dosyalar (yalnız bunlar)

1. `.antigravity/rules/permission-key-standard.md` — §4 "Module ownership" tablosuna **iki satır** (AS-1).
2. `.antigravity/rules/record-coding-standard.md` — §3 "Kayıt türleri tablosu"na **dört satır** (AS-2).
3. `services/Diten.Platform/tests/Diten.Platform.Application.Tests/RecordCodes/RecordCodeTypeCatalogTests.cs` — `RuleAheadOfCatalogue`
   sözlüğüne **dört satır** (AS-2'nin zorunlu eşi, §3).

Başka `.antigravity/**` dosyası, üretim kodu, ekran, `ocelot.json` değişmez. `sync_adapters.py` gerekmez (Çekirdek blok değişmiyor).

## 2. Eklenecek satırlar (önerilen metin)

### 2.1 AS-1 — PKS-001 §4 (tabloya, `cycle-count.*` satırından sonra)

```
| `inventory.*` | `Diten.InventoryService` — MOD-0173 Inventory Ledger & Valuation (stok okuması, hareketler, lokasyonlar, rezervasyonlar; owner decision 2026-10-01 — `supplychain.inventory.*` → `inventory.*`, `supplychain.*` stays with `Diten.SupplyChainService`; MOD-0173 pack §14) | MOD-0173 (Inventory Ledger & Valuation) |
| `warehouse.*` | `Diten.InventoryService` — MOD-0178 Putaway / Picking / Packing (putaway / pick görevleri, lokasyon rolleri, outbound, koli, izleme; owner decision Q-08 2026-10-21 — one namespace per WMS module, MOD-0178 pack §14.1; base path `/api/warehouse`) | MOD-0178 (Putaway / Picking / Packing) |
```

Bugün kodda olan anahtarlar (bilgi; tabloya yazılmaz): `inventory.stock.read`, `inventory.movements.create`, `inventory.locations.{read,manage}`,
`inventory.reservations.manage` · `warehouse.putaway-tasks.*`, `warehouse.pick-tasks.*`, `warehouse.location-roles.*`,
`warehouse.outbound-shipments.*`, `warehouse.packages.*`, `warehouse.exceptions.*`, `warehouse.activity.*`,
`warehouse.integration-monitor.*` (tam liste `WarehousePermissions`, MOD-0178 pack §14.1 ve manifest `PUTAWAY-PICKING-PACKING`). Anahtar adı değişmez (PKS-001 §2); bu WP yalnız sahipliği yazar.

### 2.2 AS-2 — RCS-001 §3 (tabloya, `CNT` satırından sonra)

```
| Yerleştirme Görevi (MOD-0178) | OTOMATİK | `PUT` | kod var — Inventory servisinde geçici sayaç (`IWarehouseRecordCodeAllocator`, Q-07 = B, sahip kararı Beste 2026-10-21); ortak servise (MOD-0040-FU02) geçişte DL-8'e `PUT` eklenir (FU-0178-RCS-01) |
| Toplama Görevi (MOD-0178) | OTOMATİK | `PCK` | kod var — Inventory servisinde geçici sayaç (`IWarehouseRecordCodeAllocator`, Q-07 = B, sahip kararı Beste 2026-10-21); ortak servise (MOD-0040-FU02) geçişte DL-8'e `PCK` eklenir (FU-0178-RCS-01) |
| Sevkiyat Çıkışı — outbound (MOD-0178) | OTOMATİK | `OUT` | kod var — Inventory servisinde geçici sayaç (`IWarehouseRecordCodeAllocator`, Q-07 = B, sahip kararı Beste 2026-10-21); ortak servise (MOD-0040-FU02) geçişte DL-8'e `OUT` eklenir (FU-0178-RCS-01) |
| Koli / Paket (MOD-0178) | OTOMATİK | `PKG` | kod var — Inventory servisinde geçici sayaç (`IWarehouseRecordCodeAllocator`, Q-07 = B, sahip kararı Beste 2026-10-21); ortak servise (MOD-0040-FU02) geçişte DL-8'e `PKG` eklenir (FU-0178-RCS-01) |
```

Önekler kodda sabit: `WarehouseRecordCodeRules` (`PutawayTask = "PUT"`, `PickTask = "PCK"`, `OutboundShipment = "OUT"`, `Package = "PKG"`).

### 2.3 Platform testinin eşi (`RuleAheadOfCatalogue`)

`RecordCodeTypeCatalogTests.AC16`, §3'teki her OTOMATİK satırın Platform kataloğunda olmasını ister. Katalog bu dört türü ancak
MOD-0040-FU02 DL-8'de alır; §3'e satır girip test değişmezse AC16 kırmızı yanar (`CNT` ile aynı durum; sahip 2026-11-10 (a)
yolunu seçti). Bu yüzden aynı commit'te sözlüğe:

```csharp
["PUT"] = "MOD-0040-FU02 DL-8",
["PCK"] = "MOD-0040-FU02 DL-8",
["OUT"] = "MOD-0040-FU02 DL-8",
["PKG"] = "MOD-0040-FU02 DL-8",
```

Test her satır için iki şeyi zaten denetliyor: tür §3'te OTOMATİK mi, katalogda **yok** mu. DL-8 türü kataloğa eklediğinde test
kırmızı yanar ve satırın silinmesini ister (kendini temizler).

## 3. Kabul ölçütleri

- [ ] AC-1 `git diff --stat` yalnız §1'deki üç dosyayı gösterir: PKS +2 / −0, RCS +4 / −0, Platform testi +4 / −0.
- [ ] AC-2 Eklenen satırlar §2'deki metinle birebir aynıdır (sahibin onayladığı metin).
- [ ] AC-3 `inventory.*` ve `warehouse.*` başka bir namespace satırıyla çakışmaz; `PUT` / `PCK` / `OUT` / `PKG` §3'te başka satırla
      çakışmaz (yazmadan önce ölçüldü: dördü de §3'te yok; iki namespace de §4'te yok).
- [ ] AC-4 Platform `RecordCodeTypeCatalogTests` yeşil; **sabotaj:** sözlükten `PKG` satırı silinince AC16 kırmızı, geri alınınca yeşil.
- [ ] AC-5 Commit yalnız açık dosya yoluyla stage edilir; `main`'e commit yok; push / PR sahibin sözüyle.
- [ ] AC-6 MOD-0178 FU-0178-PKS-01 ve FU-0178-RCS-02 bu commit'in numarasıyla CT kaydına işlenir (pack metni değişmez).

## 4. Açık / bilgi

- **Q-EA-4 (MOD-0178 pack):** paket "öneklerin seçimi EA ile" diyor. Önekler kodda S1'den beri yazılıyor ve RCS-001 §2 gereği
  verilmiş kod değişmez. **Öneri:** sahip onayı yeterli, EA'ya bilgi notu.
- MOD-0040-FU02 DL-8: dört tür + `CNT` kataloğa girerken `RuleAheadOfCatalogue` satırları silinir (test zorlar).
