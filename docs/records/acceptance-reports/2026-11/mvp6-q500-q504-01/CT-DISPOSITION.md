# MVP-6 CT kabul kararı — Q500, Q501, Q503, Q504 · 2026-11-03

Durum: **karar verildi.** Sahip Natig (K1) 2026-11-03'te, MVP-6 CT oturumunda soru kutusuyla onayladı: Q500, Q501, Q504
kabul; Q503 koşullu kabul (F-5). Süreç: `docs/guides/operations/mvp6-development-process-v1.0.md` §3 adım 6–7 (bağımsız
VER + tek CT kararı). `scripts/acceptance_record.py` (CT-OPS-03) bu kuyrukta kullanılamadı: `sce-mvp6` kuyruğunda `module`
sütunu yok ve MOD-0183 / MOD-0186 paketlerinde `AC-<n>` kimliği yok (aracın "eski biçim" durumu, çıkış 2).

## Dondurulmuş kaynak

- `origin/main` @ `35e973bd22af68168686862f5b80f417c174a563` (#355 dahil), temiz ağaç `ERP-vNext-recovery-accept`.
- Paketler: MOD-0183 r6 `07152235…` (iş bu sürüme göre verildi) ve main'deki r7 `22fe550d…` (onaylı, `approvals.tsv`);
  MOD-0186 r8 `e96a9add…` (onaylı, #269).

## Kanıt

| Kanıt | Sonuç | Yer |
|---|---|---|
| Bağımsız VER (salt okunur, read-only-auditor; CT notlarını görmedi) | Q501, Q504 KABUL EDİLEBİLİR; Q500, Q503 KOŞULLU | `VER-REPORT.md` (bu klasör) |
| SupplyChain tam takım, süzgeçsiz, lane 6154 replica set, mongod 8.0.18, 09:46–09:58 | 1015 geçti, 0 kaldı, 1 atlandı (9 dk 43 sn) | TRX depoda değil (7,2 MB), sha256 `df86497d5cd714702458ba9d732a8bd4e7a73a38612b588b583ede796b42fd78` |
| Web tam takım | 4136 geçti, 2 kaldı — ikisi de bilinen `NavManifestL10nGuardTests`, eksik anahtar `Nav.Module.MRPREPLENISHMENT` / `SAFETYSTOCKOPTIMIZATION` (MOD-0189 / 0191; bu dört işe ait değil) | TRX depoda değil (9,3 MB), sha256 `2d265e62679b9b6b73ed2cb9a6aec92d56816d3339ea3f69f5970e5963b07fa5` |
| Yerel canlı koşu 2026-11-01 (gerçek Auth → Gateway → UI → servis → DB) | T1, T2 ekrandan geçti; F-1 bulundu ve #334'te kapandı | CT yerel kayıtları (`tour-20261101*`) |

Q500'ün VER koşulu "testler yeşil" idi: SupplyChain takımı yeşil → koşul kapandı.

## Karar

| Satır | İş | Karar | Gerekçe |
|---|---|---|---|
| Q500 | MOD-0183 DL-1 | **Kabul** | Kapsamdaki her satırda kod + bozulunca kırmızı olacak test (VER); takım yeşil; canlı koşu. Açık DÜŞÜK bulgular aşağıda, bir sonraki paket revizyonuna. |
| Q501 | MOD-0183 DL-2 | **Kabul** | 33 anahtar × 7 dil = 231 eşleşme, hiçbiri İngilizceyle aynı değil; ekran `validationError`'a düşmüyor. |
| Q503 | MOD-0186 DL-1 | **Koşullu kabul (F-5)** | Kapsamdaki her satırda kod + test, R34-8 ortak senaryo dahil; takım yeşil. Koşul: R34-16/17'nin gerçek INVENTORY'de (QUARANTINE'den CUSTOMER_RETURN REVERSAL) ölçülmesi — MVP-1 CT R-1/R-2 cevabı ya da canlı tur. Sahip kararı 2026-10-27 R-1/R-2'siz ilerlemeyi onaylamıştı. |
| Q504 | MOD-0186 DL-2 | **Kabul** | 31 anahtar × 7 dil = 217 eşleşme; son geri düşüş `ErrInternalError`. |

## Açık bulgular (kabulü engellemez; sahibi ve yeri)

- **F-1 (DÜŞÜK):** `fromLocationId` olmayan satırda yeni sevk denemesi 422 `SHIPMENT_LINE_STOCK_INCOMPLETE`
  (`ShipmentDispatchCoordinator.cs`, #352 `fc03f9f81`). Kural doğru (INVENTORY konumsuz çıkışı reddeder) ama pakette
  yazılı değil → MOD-0183 sonraki revizyonu (Dilim B r8 sonrası) bu kuralı yazar.
- **F-2 (DÜŞÜK):** paket durum notları eski (MOD-0183 `status_note`, MOD-0186 :917) → sonraki revizyon.
- **F-3 (DÜŞÜK):** `GSKU` / `LSKU` görünen etiketleri sabit yazılı (`Shipments/_Form.cshtml`, `_IndexL10n.cshtml`; #336,
  #355 — CT'nin kendi işi). Kısaltma kodu ama kural gereği 7 dile taşınmalı → küçük ekran düzeltmesi.
- **F-4 (DÜŞÜK):** S23-12 koruma testi yalnız kurucu parametrelerine bakıyor; `IServiceProvider` yolunu yakalamaz → test güçlendirme.
- **F-5 (ORTA):** Q503 koşulu, yukarıda.
- **F-6 (DÜŞÜK):** r7'nin iki cevap sınıfı (zarfsız 503, 401) yalnız istemci birim testinde → uçtan uca test.

## Ölçülmeyenler

Bozma (sabotaj) koşusu bu kabulde yeniden yapılmadı (iş PR'larında yapılmıştı); gerçek INVENTORY'de REVERSAL; MVP-1
canlı turlarının kalan satırları; AUD-001 sayımı; `scripts/tests` Python takımı (bu kabulde koşulmadı).

## Kuyruk

`docs/roadmap/plans/mvp6-process-pilot-01/CT-QUEUE.tsv`: dört satır `READY` → `DONE` (`sce-mvp6` sözlüğünde tanımlı
kapalı durum; eski `DONE (CT ACCEPTED)` biçimi `ct_queue.py validate`'te tanımsız uyarısı verir). Kabul ve Q503'ün F-5
koşulu bu dosyadadır; satırların `since` sütunu 2026-11-03, `record` sütunu bu dosya. `validate`: 0 hata. Q502 / Q505
(MOD-0147, taklide karşı) bu kararın dışında.
