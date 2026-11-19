# MOD-0183 r11 — sahip kararları ve girdiler (2026-11-15 / 2026-11-16)

Paket: `execution/domains/supply-chain-execution/module-packs/MOD-0183-shipment-tracking-pod.md`, §26 (r11). Kayıt MVP-6 CT
tarafından yazıldı. Bu kayıt onay değildir; r11 CT-APR-02 incelemesi ve CT-APR-01 satırı bekliyor.

## Kararlar

| No | Karar | Kim, ne zaman | Pakette |
|---|---|---|---|
| R11-C | Gateway arkasında korelasyon kökü: seçenek C. Servis kökü saklı sevkiyattan alır; geçiş, POD ve dispatch isteğin korelasyonunu kökle karşılaştırmaz; tekrar oynatma korelasyona bakmaz. A (`X-Client-Correlation-Id` okumak) ve B (kökü gövdede göndermek) seçilmedi. | Natig, 2026-11-15 (dev-fixes oturumu); "Evet, CT uygulasın" | §26.1 R11-1…R11-7 |
| R11-8 | C seçeneği depo alımına da uygulanır: kök, intake işini ilk hazırlayan isteğin korelasyonudur; aynı anahtarla yeniden deneme, `COMMIT_FAILED` sonrası devam ve commit edilmiş outbound'un yeniden alımı saklı kökü kullanır, çağıranın korelasyonu karşılaştırılmaz. | Natig, 2026-11-16 (MVP-6 CT oturumu, soru kutusu) | §26.1 R11-8, §13.1, CR-9 |

## Girdiler (başka sahiplerden)

| Konu | Kaynak | Pakette |
|---|---|---|
| Item adımının sürüm numaraları: sürümler donma sırasıyla numaralanıyor; WMS-BUNDLE 1.2.0 dalga üyelerine, WMS-BUNDLE 1.3.0 + WAREHOUSE-OUTBOUND 1.1.0 çıkış formunun `customerRef` ekine verildi. Item adımı WMS-BUNDLE 1.4.0 + WAREHOUSE-OUTBOUND 1.2.0 olacak. | MOD-0178 sahibi (Beste), MVP-6 CT'ye mesaj, 2026-11-16 | 25.6, §25.7 DL-7 kapısı, IR-14 (kapı içeriğe bağlı; numara beklentidir) |
| Sevk hatası için C seçeneği uygun; MVP-1 tarafında yama yapılmıyor; r11 kodu dev'e gelince MOD-0177-FU01 canlı turu tamamlanacak. | Beste, 2026-11-16 | §26.6 |

## Açık (başka modüllerin işi, XMC-001)

- MOD-0178 paketi (`MOD-0178-putaway-picking-packing.md`), MOD-0178-FU01 paketi ve
  `mod0178-fu01-outbound-form-v2-owner-decisions-01.md` Item adımını hâlâ WAREHOUSE-OUTBOUND 1.1.0 + WMS-BUNDLE 1.2.0 diye
  yazıyor; düzeltme MOD-0178 sahibinin (OD-R10-6 ile birlikte).
- Returns (MOD-0186), Claims (MOD-0187) ve Loads (MOD-0185) için ayrı revizyonlar (§26.5).
