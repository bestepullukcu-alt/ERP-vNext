---
status: approved
decided: 2026-10-29
module: MOD-0230
supplements: docs/records/decisions/2026-10/pvg-mod0230-kalici-depo-01.md
---

# PVG-MOD0230-DEPO-03 — Kalıcı depo taslağının 12 açık sorusu

> **Karar sahibi:** sahip. 2026-10-29'da PVG Control Tower sohbetinde, "12 açık soruda CT önerilerini topluca kabul ediyor musunuz?" sorusuna "Hepsini kabul (Önerilen)" yanıtını verdi.
> **Kayıt:** PVG Control Tower.
> **Dayanak:** revizyon 14 değişiklik taslağı `.ct-teslim/kalici-depo/REVIZYON-14-TASLAK.md` §5 (teslim klasörü; commit edilmedi; CT dalı `b50492b94` üzerinde yazıldı).

## Karar

| # | Konu | Karar |
|---|---|---|
| S-1 | DCP-004 dilim tablosu | `## 8.1 Delivery slices` tablosu bu revizyonda, DCP-004 değişikliğiyle aynı commit'te eklenir. Uygulamadan önce taslak kopyada `pack_approval.py check` koşulur. |
| S-2 | Proje yeri | Mongo kodu ayrı `Diten.PvgService.Persistence` projesinde durur. `DenyAllRegPvBasePortTests.cs:96` değişmez. |
| S-3 | `TenantId` türü | Token'daki gibi metin kalır, normalleştirilmez. Repo kuralından (GUID) bu sapma pakette yazılır. |
| S-4 | `CreatedBy` / `UpdatedBy` | DL-10'da eklenir. Aktör doğrulanmış özneden gelir. WP-0230-DL4B kabul edilmemişse bu kısım onu bekler. |
| S-5 | İşlem | DL-10'da yazmalar Mongo işlemi içinde yapılır. Açılışta işlem yoklaması yapılır. |
| S-6 | Liste index'i | Kataloğa üçüncü index eklenir: (TenantId, ReceivedAtUtc azalan, IntakeDraftId). Diğer `DB-level` planları eklenmez. |
| S-7 | Yapılandırma | Anahtarlar `Pvg:Persistence:Store`, `Mongo:ConnectionString` ve `Mongo:DatabaseName` (Inventory deseni). Veritabanı adı WP prompt'unda yazılır, kodda sabit olmaz. |
| S-8 | Varsayılan depo | Bellek içi kalır. Mongo açıkça seçilir. |
| S-9 | DL-5 ölçütleri | Revizyon 14 yalnız olgu satırlarını düzeltir (satır 656 ve OD-PVG-9). AC-11, AC-18 ve AC-33 metni D-1 ve D-2 kararlarından sonra, DL-5 revizyonunda düzeltilir. |
| S-10 | Dağıtıcı arka plan servisi | Denetim karar notuna D-7 olarak eklenir ve DL-5'ten önce karara bağlanır. DL-10'u engellemez. |
| S-11 | Dev mongod replica set mi | DL-10 DoR'unda salt okunur olarak ölçülür. |
| S-12 | `WP-0230-03` dosyası | Dosya değişmez. DL-10 dilim tablosundan atanır. |

## Sınırlar

- Taslak yine ön incelemeden (S-93), bağımsız incelemeden ve HIGH onaydan geçer (DEPO-01 KD-6).
- Pakete uygulama, WP-0230-DL4D ve WP-0230-DL7B dağıtıldıktan sonra yapılır (DEPO-01 "Gerileme değerlendirmesi").
