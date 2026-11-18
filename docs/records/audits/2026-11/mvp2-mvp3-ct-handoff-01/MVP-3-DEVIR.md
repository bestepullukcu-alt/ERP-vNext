# MVP-3 (Manufacturing) — devir notu

> **Tarih:** 2026-11-17 · **Ölçüm:** origin/main `76466f413` · **Devreden:** Cihan
> **Kayıt kuralı:** bu bir tarihli kayıttır (`docs-organization.md` §1 soru 2); ana dalda düzeltilmez.
> **Not:** bu dosyada gizli bilgi, parola ve bağlantı adresi yoktur.

## 1. Özet

- Devredenin oturumunda Ekim–Kasım döneminde MVP-3 için **kod işi yapılmadı**; yapılan iş durum izleme ve raporlamaydı.
- Bu MVP için açık dal, açık PR ya da push edilmemiş iş yok.
- Ana modül MOD-0193 koşullu olarak tamamlandı (G2B). Kalan işin büyük kısmı sahip ve CT kararına bağlı.

## 2. Modüller

Puan ve onay sütunlarının anlamı MVP-2 notundaki gibi (Ö1…Ö7; `pack_approval.py check`, main `76466f413`).

| Modül | Rol | Paket durumu | Onay (main) | Ö1–Ö7 | Kalan iş (özet) |
|---|---|---|---|---|---|
| MOD-0193 BOM ve Rotalar | Ana | done | eski onay geçerli | 1·1·1·1·0,5·0,5·0,5 | Koşullu kabul (G2B). Ürün doğrulaması izin verici modda (W-G2B-0290). Merkezi denetim bağlı değil. Değişiklik kontrolü yalnız biçim denetimi. Olay taşıması ortamda kapalı. Kayıt defterinde port (5067) ve durum (`review`) eski. |
| MOD-0290 Ürün / Kalem / SKU (MVP-2 + MVP-3) | Destek | in-progress | eski onay geçerli; MOD-0290-FU06 ve FU07 onaylı (2026-11-10 / 11-11, ready-for-dev, kod yok) | 0,5·1·1·1·0,5·0,5·0 | Sözleşme hâlâ `1.1.0-draft.16`. Servisler arası doğrulama ucu yok. Bütün tüketiciler izin verici modda. Kabul kaydı yok. |
| MOD-0209 Değişiklik Kontrolü | Destek | paket yok | — | 0·0·0·0·0·0·0 | Kayıt satırı, paket ve kod yok. Sahip alanı atanmadı. |
| MOD-0040 Kanonik Kimlik / Correlation | Destek | paket yok (yalnız teamül) | — | 0·0,5·0·0·0,5·0·0 | Kimlik numarası kullanımdan kalkmış bir takma ada (MOD-0288) ait. Blueprint satırı ve paket yok. |
| MOD-0003 Veri Sözleşmesi Kayıt Defteri | Destek | paket yok (planned) | — | 0·0·0·0·0·0·0 | Paket ve çalışma zamanı yok. W3-E dalgasına planlı. |

## 3. Dilimler ve iş paketleri

| İş | Ne | Bağımlılık |
|---|---|---|
| INT3-01 | MOD-0193 → MOD-0290 gerçek doğrulama (Mode=Http, 200'lük parçalar, 400 eşlemesi, `lifecycleStatus` kararı, `getSkuUom`, testler) | MOD-0290 v1.1 dondurma + servis kimliği kararı |
| INT3-02 | Manufacturing merkezi denetimi (4 komut; `82ebedeb4` işaretlerinin geri gelmesi + `EnlistAsync`) | MOD-0021 (BL-501) |
| INT3-03 | Correlation | MOD-0040 kararı |
| Olay taşıması | Ortamda açılması | ortam sahibi |

Hiçbiri başlamadı. Sürmekte olan bir MVP-3 iş paketi ya da oturumu yok.

## 4. CT kuyruğu (MVP-3)

| Bekleyen | Kimde |
|---|---|
| MOD-0290 sözleşme v1.1'in dondurulması | CT (MDM sahibiyle) |
| MOD-0290 servisler arası kimlik / iç doğrulama ucu kararı | CT / Auth |
| MOD-0209 sahip alan ataması, kayıt satırı, paket | CT |
| MOD-0040 kararı: yeni kimlik mi, "yalnız standart" kaydı mı; E-1 ortak parçası | CT / kayıt sahibi |
| MOD-0003 platform paketinin ready-for-dev olması (W-0193-02'yi kapatır) | platform sahibi |
| MOD-0193 kayıt defteri satırının düzeltilmesi (port, durum) | kayıt sahibi |

## 5. Açık sahip kararları ve feragatler

- **W-G2B-0290:** MOD-0193 ürün doğrulamasının izin verici kalması. MOD-0290 canlı olunca kapanır.
- **W-0193-01:** değişiklik kontrolü MOD-0209 olmadan, yalnız biçim denetimiyle.
- **W-0193-02:** sözleşme kaydı MOD-0003 olmadan.
- **BL-501:** Manufacturing bağlantısı MVP'lerden sonraya ertelenmişti (`730a7fa1f`). INT3-02 bunu geri getirir.
- MOD-0209 için e-imza ve 21 CFR Part 11 gereksinimi; sahip atanınca pakette ele alınmalı.

## 6. Bekleyen incelemeler ve başka ekiplere verilmiş sözler

- **Bekleyen inceleme:** yok.
- **MVP-3 adına verilmiş söz:** yok.
- **İlgili kayıtlar:**
  - `docs/records/audits/2026-10/mvp3-mod0193-bom-routings-01/REPORT.md`;
  - `docs/records/decisions/2026-10/mvp3-g2b-bom-structure-gate-owner-decision-01.md`;
  - MVP-4'e verilen BOM sürüm olayı devri: `docs/records/audits/2026-10/mvp3-mod0193-bom-routings-01/mvp4-handoff-bom-version-event.md`.
