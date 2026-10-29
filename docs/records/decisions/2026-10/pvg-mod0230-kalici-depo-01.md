---
status: approved
decided: 2026-10-29
module: MOD-0230
work_packages: kalıcı depo dilimi (adı paket revizyon 14'te verilir), DL-5 (WP henüz yok)
---

# PVG-MOD0230-DEPO-01 — Vaka girişi yapı/test kapısında kalıcı depoya (Mongo) geçer

> **Karar sahibi:** sahip. 2026-10-29'da PVG Control Tower sohbetinde, D-3 sorusunda "Mongo'yu aç (Önerilen)" seçeneğini seçti.
> **Kayıt:** PVG Control Tower.
> **Dayanak:** denetim dilimi karar notu `.ct-teslim/denetim-dl5/KARAR-NOTU.md` D-3 (teslim klasörü, commit edilmedi).

## Durum

Denetim dilimi (DL-5, AC-11, AC-18, AC-33) şunu istiyor: kayıt yazılamazsa işlem yapılmaz, ve kayıt iş verisiyle birlikte yazılır (AUD-001 K2, `.antigravity/rules/audit-trail-standard.md` satır 150).

Ortak denetim aktarıcısı `Diten.BuildingBlocks.CentralAudit` bu garantiyi komutun Mongo işlemiyle veriyor (`CentralAuditTransaction.EnlistAsync`; outbox servisin kendi veritabanında — `docs/guides/operations/central-audit-connect.md` satır 14–15).

Ölçüm (CT, 2026-10-29, CT dalı `0c1532821`):

- PVG'nin deposu bellekte: `InMemoryPvgIntakeDraftRepository` (paket G-10, satır 203).
- Yapı/test kapısında Mongo yasak. Yasak üç yerde yazıyor:
  - paket: satır 130, 163, 291, 621, 744–747, 1584;
  - domain-config: satır 45 ve 100;
  - DCP-004 §10.
  Üçü de Mongo'yu canlı kullanım kapısına bağlıyor.
- AC-13 "PvgService'te Mongo sürücüsü yok" diyor. Mimari test `DenyAllRegPvBasePortTests.cs:96` bunu ölçüyor.
- `PvgIntakeDraftIndexCatalog` bir koleksiyon adı ve iki index tanımını şimdiden yalnız veri olarak taşıyor.

## Karar

| # | Karar |
|---|---|
| KD-1 | MOD-0230, **yalnız yapı/test kapısında** (yerel, dev, CI) Mongo kalıcı depo kullanır: koleksiyon, kiracı indeksleri, migration. Canlı kullanım kapısı kapalı kalır ve koşulları değişmez. |
| KD-2 | Kapsam yalnız MOD-0230'dur. MOD-0231, MOD-0232 ve MOD-0234 için hiçbir şey değişmez. |
| KD-3 | Kalıcı depo, DL-5'ten **önce** ayrı bir dilimdir. DL-5 denetim kaydını iş verisiyle aynı Mongo işleminde yazar (K2). |
| KD-4 | Değişecek belgeler: **DCP-004** (§10 ve başlık notu: MOD-0230 yapı/test kapısı için Mongo istisnası), **domain-config** (satır 45 ve 100), **paket revizyon 14**. Paket revizyonu şunları kapsar: G-10; AC-13 yeni koşula çevrilir; tekil kalıcılık cümlesi; yukarıdaki yasak satırları; eskimiş engel cümlesi (satır 656, OD-PVG-9). Mimari test değişikliği iş paketinin kapsamıdır. |
| KD-5 | Seed verisi, izin seed'i ya da yetkisi, arka plan işi ve canlı kullanım yine yetkisizdir. |
| KD-6 | Değişiklik önce ön incelemeden (S-93), sonra bağımsız incelemeden (Codex) ve HIGH onaydan geçer. |

## Açık kalanlar

- **DCP-004 sahibi.** DCP-004 başlığında sahip olarak "NY" yazılı. Domain-config satır 92 ise "user approval" diyor. Değişikliğe NY'nin de onay vermesi gerekip gerekmediği sahibin kararıdır.
- **D-1, D-2, D-4, D-5, D-6** açık (karar notu). Kalıcı depo dilimi bunlara bağlı değildir; DL-5 bağlıdır.
- **CI'da Mongo.** CI'da Mongo olup olmadığı ölçülmedi. Bunu revizyon taslağı ölçer.

## Gerileme değerlendirmesi

- **DL4D ve DL7B önce gider.** Revizyon 14, `main`'deki onaylı sürümü değiştirir. Hazır olan iki prompt revizyon 13 alıntılarına ve Expected HEAD'e bağlı. Revizyon onaydan önce dağıtılırsa ikisi de yeniden yazılır ("pakette değişiklik dağıtımdan önce olmaz" kuralı). Bu yüzden revizyon 14 önce taslak olarak hazırlanır. Pakete, DL4D ve DL7B dağıtıldıktan sonra uygulanır.
- **Kabul kayıtları yeniden bağlanır.** S-86 gereği WP-0230-DL8'in kabulü yeniden yeni sürüme bağlanır.
- **AC-13 ters dönünce bir koruma düşer.** Testi kaldırmak yetmez; yerine yeni bir koruma gelir. Mongo yalnız altyapı katmanında olmalı, her koleksiyonun kiracı indeksi olmalı, ve yalnız yerel, dev ya da CI ortamında açılmalı. Ölçütler revizyonda yazılır.
- **Dev ortamı.** Mongo'dan önce başlatılan süreç her çağrıda 30 saniye takılıyor. Dilimin DOĞRULA adımları başlatma sırasını yazar.
