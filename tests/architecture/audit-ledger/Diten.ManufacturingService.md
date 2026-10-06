# Denetim defteri — Diten.ManufacturingService

Kural: `.antigravity/rules/audit-trail-standard.md` (AUD-001) · Ölçen test: `AuditTrailStandardTests`

Bu dosyada yalnız **bildirim** durur; bir komutun denetlenip denetlenmediği buradan değil, `services/Diten.ManufacturingService/src`
altındaki üretim kodundan okunur. Biçim ve bölümlerin anlamı: [README.md](README.md).

## İzler

| iz | yol | tür | belirteç |
|---|---|---|---|
| bom-surum-gecmisi | c | yazıcı | IBomHistoryJournal.CommitAsync |

Ölçüm notları (2026-10-06):

> **bom-surum-gecmisi** — `mfg_bom_history`: aktör kimliği + o andaki adı, kiracı + tüzel kişi, BOM sürümü + item, işlem, önceki/sonraki durum, değişen alan ADLARI, değişiklik kontrolü referansı, korelasyon, UTC zaman, sonuç. `IBomHistoryJournal` arayüzünde güncelleme / silme üyesi yok; `CommitAsync` BOM sürümünü ve geçmişi TEK Mongo işleminde yazar — geçmiş yazılamazsa BOM da yazılmaz (GxP, K2 fail-closed; `BomApiTests.When_the_history_cannot_be_written_the_bom_is_not_written_either`). Kaydın kendi ekranında okunur: `GET /api/bom/version/{id}/history` (izin `manufacturing.bom.read`) → `/Manufacturing/Boms` Details → Geçmiş. CT kabulü: 2026-10-06 (MVP-3 lane CT, MOD-0193 pack §21). Merkezi günlüğe iletim yok — K4 ortak iletici gelince eklenir (F-0193-05).

## İstisnalar

| komut | sınıf | gerekçe |
|---|---|---|

## Dolaylı

| komut | iz | üzerinden |
|---|---|---|

## Bilinen borç

Bu liste **yalnız küçülür**. Satır eklemek yasaktır; denetlenen ya da silinen komutun satırı çıkarılır (test bunu zorlar).

## K2 borcu

Bu liste **yalnız küçülür**. Servisin dört komutu da K2 sınıfındadır (GxP — reçete) ve hepsi kapalı-başarısızdır: borç yok.

## Yazan sorgular

Bu liste **yalnız küçülür**.
