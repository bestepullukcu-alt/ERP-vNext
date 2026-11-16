# MOD-0018-FU32 — sahip kararları 01

- **Kaynak:** CT · Auth (Yetki kulesi).
- **Söz:** Sahibin bu sohbette doğrudan yazdığı sözdür.
- **Kişi adı:** Yazılmaz; sahip = MOD-0018 sahibi.
- **Kimlik ön kontrolü:**
  - `verify_module_id.py --check-id MOD-0018-FU32 --parent MOD-0018` sonucu OK.
  - Keşif (`--discover "warehouse role template crm account barcode permission grant"`) 5 gateway ve 2 görev bulgusu verdi. Bulguların hiçbiri mükerrer iş değil: gateway bulguları üretici–tüketici ilişkisi, görev bulguları Q354 ve Q477 kapanmış eski kayıtlar. Raporun sha256 değeri `8ef90c8e…`.

| # | Karar | Söz | Nerede | Zaman |
|---|---|---|---|---|
| 1 | Aşağıdaki kapsam önerisi kabul edildi. Kimlik MOD-0018-FU32'dir. FU31 donmuş kalır; bu iş FU31'e eklenmez. | "Öneriyi kabul et" | CT · Auth sohbeti, doğrudan | 2026-11-16 11:15 |

## Kabul edilen kapsam (tek paket, iki dilim)

1. **DL-1, CRM anahtarları** (MOD-0178-FU01 Q-5).
   - Anahtarlar: `crm.account.lookup-validation` ve `crm.account-ship-to.read`.
   - Hangi rollere: `ShippingPlanner` ve `WarehouseSupervisor`, var olan kiracılar dahil.
   - Koşul: yalnız CRM modül hakkı da olan kiracılarda.
   - `crm.account-ship-to.read`, CRM manifestine girdikten sonra devreye girer (MOD-0149 §32 DL-3b).
2. **DL-2, barkod anahtarları** (MOD-0182 Q-14).
   - Yeni rol açılmaz. `warehouse.barcode.*` anahtarları FU31'in üç rolüne dağıtılır:
     - `WarehouseOperator`: okutma ve cihaz kaydı.
     - `WarehouseSupervisor`: cihaz, ayar, etiket, aşım onayı ve iz.
   - Koşul: yalnız BARCODE-RFID modül hakkı olan kiracılarda.

## Açık kalanlar

- **MOD-0182 sahibinin teyidi:** MOD-0182 §14 yalnız yeni kiracılar için beş yeni rol öneriyor. Bu karar ise mevcut üç rolün kullanılmasıdır; farkı MOD-0182 sahibi teyit eder.
- **Sıra:** Önce FU31 onaylanır («onayla»), sonra FU31'in kodu yazılır, en son FU32 paketi yazılır.
- **Henüz izin verilmeyenler:** Paket yazımı, commit ve push sahibin sözüyle yapılır. Bu kayıt paket yazma izni değildir.

Bu kayıt commit'ten sonra değiştirilmez.
