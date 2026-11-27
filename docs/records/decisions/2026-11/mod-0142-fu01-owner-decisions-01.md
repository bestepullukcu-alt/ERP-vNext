# MOD-0142-FU01 — sahip kararları 01 (S-1, S-2, S-5)

- **Tarih:** 2026-11-25
- **Karar veren:** Beste (MVP-2 sahibi, MOD-0142-FU01 onaylayıcısı), sohbette.
- **Paket:** `execution/domains/procurement/module-packs/MOD-0142-FU01-grn-inventory-posting-lot-creation.md` taslak (`9eb3c649a`,
  LF sha `d6a16816…`). Pakette S-1…S-12 açık soruları var; bu kayıt üçünü kapatır.

## Kararlar

| Soru | Karar | Sahibin sözü |
|---|---|---|
| **S-1** — Mal kabulü yapan kullanıcının rolü `inventory.movements.create`, `trace.lots.create` ve `trace.lots.read` izinlerine sahip olsun mu (kullanıcı token'ı iletimi; servis token'ı yok)? | **Evet.** Procurement → Inventory / TRACE çağrıları kullanıcı token'ıyla, Gateway üzerinden yapılır (SupplyChain `InventoryMovementClient` emsali). Rol şablonu / atama notu pakette CT adımı C-5 olarak kalır. | "evet. Sevkiyatta da aynı yöntem kullanılıyor." |
| **S-2** — Inventory kesin ret verdiğinde (ör. 10 satırın 6'sı yazıldı, 7. satır reddedildi) yazılmış satırlar ne olsun? | **Öneri A: hemen geri alınır.** Yazılmış satırlar aynı işlem akışında ters kayıtla telafi edilir; GRN `Posted` olmaz, yarım GRN stokta görünmez. | "hemen geri alınsın. Böylece yarım kalmış bir mal kabul stokta görünmez." |
| **S-5** — Mal kabulde konum (`locationId`) zorunlu mu? | **Zorunlu ve listeden seçilir.** Serbest metin depo / konum yerine şirketin LOCATION kayıtlarından seçim; INVENTORY giriş hareketinin `toLocationId` zorunluluğuyla tutarlı. | "zorunlu olsun, listeden seçilsin." |

## Sonuç

- S-3, S-4, S-6…S-12 açık kalır (paketteki önerilerle); paket bu üç kararla güncellenir, ardından bağımsız inceleme ve onay satırı.
- Bu kayıt kod, commit ya da dağıtım izni değildir.
