# MOD-0175-FU01 — sahip kararları 01 (2026-11-03)

Paket: `execution/domains/supply-chain-execution/module-packs/MOD-0175-FU01-quarantine-hold-source-reversal.md`. Sahip: Beste (K2).

| # | Konu | Karar | Sahip | Tarih |
|---|---|---|---|---|
| Q-01…Q-09 | Paketin açık soruları | "Hepsi öneri gibi" — paket r2'de işlendi (`e89bce84c`) | Beste | 2026-11-03 |
| XMC-001 DL-3 Web | MOD-0173-FU01 ekran dosyalarına dokunma izni | **Verildi.** Kapsam yalnız `REVERSAL_QUALITY_RECORD_LOCKED` hata kodunun ekran metni: `frontend/Diten.Web/Models/Inventory/InventoryErrorMessages.cs` (kod → metin eşlemesi), Stok Defteri (`StockLedgerIndex`) ve Stok Hareketleri (`StockMovementsIndex`) resx aileleri (7 dil), `InventoryResxParityTests.cs` (kod listesi). Başka değişiklik yok | Beste | 2026-11-03 |

Paketin bir sonraki revizyonu izni "verildi" olarak işler (§5, §18, §19.5, §22).

| # | Konu | Karar | Sahip | Tarih |
|---|---|---|---|---|
| XMC-001 MOD-0178 | MOD-0178'in iki açık-yüklem yeri `!= HoldStatus.Decided` ile Voided hold'u açık sayıyor | **Verildi.** MOD-0175-FU01 DL-2 kapsamında yalnız şu iki yer `QuarantineRecordStates.IsOpen` / `IsTerminal` kullanacak biçimde düzeltilir: `services/Diten.InventoryService/.../PutawayTasks/.../GetPutawayCandidateListHandler.cs:185`, `.../Warehouse/Services/WarehouseSerialReader.cs:36`; davranış değişikliği yalnız Voided hold'un açık sayılmaması; kaynak taraması bu iki dosyayı da kapsar. Başka değişiklik yok | Beste | 2026-11-04 |
