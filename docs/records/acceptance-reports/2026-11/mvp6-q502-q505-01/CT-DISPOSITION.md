# MVP-6 CT kabul kararı — Q502, Q505 (MOD-0147 DL-1 / DL-2) · 2026-11-05

Durum: **karar verildi.** Sahip Natig (K1), 2026-11-05'te MVP-6 CT oturumunda soru kutusuyla "mock üzerinde kabul" seçeneğini
onayladı (plan v10.2 K3). İki satır da **mock üzerinde kabul edildi; canlı bağlantı açık.** Kod, donmuş SUPPLIER 1.3.0
sözleşmesinin taklidine karşı çalışıyor. Gerçek MOD-0140 uygunluk bağlantısı (plan v10.2 M7) ayrı bir iş olarak açık kalıyor.
Süreç: `docs/guides/operations/mvp6-development-process-v1.0.md` §3 adım 6–7 (bağımsız VER ve tek CT kararı).
`scripts/acceptance_record.py` (CT-OPS-03) bu kuyrukta kullanılamadı. Sebep Q500–Q504 ile aynı: `sce-mvp6` kuyruğunda
`module` sütunu yok.

## Dondurulmuş kaynak

- `origin/main` @ `a38bab855c9ad7f7c259c5e80d7a03b8e0140a0c` (#370 ve #371 dahil), temiz ağaç `ERP-vNext-recovery-accept-0147`.
- Paket: MOD-0147 `f1c951bc4d68bd617326bed9f7c2e835393765424832fe46ee709cc12216aa03`, `ready-for-dev`. Onayı
  `execution/approvals/approvals.tsv:25` satırında; notu "only DL-1, DL-2; start-on-mock §0.0 (1)-(5)".
- Birleştirmeler: #320 `cc63c620c` (DL-1), #327 `0504c96ea` (DL-2) ve sonrasında giren düzeltme `43aa8ea16`.

## Kanıt

| Kanıt | Sonuç | Yer |
|---|---|---|
| Bağımsız VER (salt okunur, read-only-auditor; CT notlarını görmedi) | Q502 ve Q505 KABUL EDİLEBİLİR; 8 DÜŞÜK bulgu, ENGEL ya da ORTA bulgu yok | `VER-REPORT.md` (bu klasör), sha256 `414758a330c3d7d52e7148d28750699996d700032cf584ccbd574f1af0a8867b` |
| SupplyChain tam takımı: süzgeçsiz, lane 6154 replica set, mongod 8.0.18 | 1015 geçti, 0 kaldı, 1 atlandı (4 dk 27 sn), 2026-11-05 11:41 | TRX depoda değil |
| Taklit ↔ sözleşme | `supplier.openapi.yaml` 1.3.0 FROZEN; durumlar ve hata kodları birebir (VER) | `VER-REPORT.md` |

## Karar

| Satır | İş | Karar | Gerekçe |
|---|---|---|---|
| Q502 | MOD-0147 DL-1 | **Kabul (mock üzerinde, canlı bağlantı açık)** | Kapsamdaki her kalemde üretim kodu ve bozulunca kırmızıya dönecek test var (VER). Kiracı ve LE sınırı, kiracılar arası 404, yetki özniteliği, yalnız soft delete. Kapsam dışı iş yok. Takım yeşil. |
| Q505 | MOD-0147 DL-2 | **Kabul** | Submit, outbox, değişmezlik, hata yolu ve denetimli olaylar kod ve testle kapsanıyor (VER). Takım yeşil. |

## Açık işler (kabulü engellemez; sahibi ve yeri)

- **Canlı bağlantı (M7):** taklitten gerçek MOD-0140 uygunluk ucuna geçiş. Ön koşul: MOD-0140 DL-3 kodu main'de olmalı
  (Cihan). Sahibi MVP-6 CT.
- VER bulguları F-1…F-7, hepsi DÜŞÜK. Ayrıntısı, sahibi ve son tarihi `VER-REPORT.md` §3'te:
  - F-1: merkezi denetim alanları, Option A.
  - F-2: taklidi sözleşme dosyasına bağlayan test.
  - F-3: teslim taşıyıcısı, G5.
  - F-4: sınırsız liste, 1.1.0.
  - F-5: `SUP-` deseninin teyidi.
  - F-6: bayat `Publishing` sayımı.
  - F-7: paket durum notları.
- F-8 (satırlar `READY` duruyordu) bu kararla kapandı.

## Ölçülmeyenler

- Sabotaj koşusu tekrarlanmadı.
- CI'nin mongod 7.0.28 sürümüyle sonuç alınmadı.
- Canlı Gateway / MDM / MOD-0140 yolu (E4) ölçülmedi.
- `scripts/tests` Python takımı koşulmadı.
- AUD-001 sayısı test tablosundan alınmadı. VER'in kaynak okumasına göre 2 yazma komutu var, ikisi de denetleniyor.

## Kuyruk

`docs/roadmap/plans/mvp6-process-pilot-01/CT-QUEUE.tsv` dosyasında Q502 ve Q505 `READY`'den `DONE`'a geçti. `since`
2026-11-05, `record` bu dosya.
