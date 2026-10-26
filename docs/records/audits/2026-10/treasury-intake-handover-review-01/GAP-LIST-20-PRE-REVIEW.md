---
record_id: TREASURY-GAP-LIST-20
kind: sahip ön incelemesi (CT-APR-02 öncesi; resmî inceleme değildir) — bulgular ve kapanışlar
received_at_local: 2026-10-25
received_from: depo sahibi (K1), sohbette yapıştırılan inceleme ("CONDITIONAL PASS — hedef sürümler doğru; dört düzeltme")
reviewed_input: HEAD e66746606; CT-APR-02 prompt'undaki 9/9 hedef SHA-256 eşleşti
fixed_in: MOD-0048-FU01 §21, DCP, CT-APR-02 prompt'u (bu kaydın commit'i; hedef hash'leri değişti, prompt yenilendi)
previous: GAP-LIST-19-ALL.md
---

# Gap listesi 20 ve kapanışları

| # | Öncelik | Gap | Kapanış |
|---|---|---|---|
| 1 | P1 | MOD-0048-FU01 AC-R3 yükleyicinin "fail-closed" olduğunu varsayıyor; kod öyle değil | Ölçüldü: eksik zorunlu küme `published_set_not_found` olarak raporlanır (`BusinessReferenceDataCatalogLoaderService.cs:506-513`), worker günlüğe yazıp devam eder (`BusinessReferenceDataCatalogLoadWorker.cs:86-92`; yalnız engelleyen çakışma fırlatılır ve yakalanır, `:105, 112-114`). AC-R3 yeniden yazıldı: durdurma noktası platform açılışı değil, **tüketici yazmasıdır** (503, yazma yok). Kapsam yalnız seed + config kaldı; hazırlık sağlık kontrolü (kod) follow-up. |
| 2 | P2 | Hassasiyet aşımı FU01'de 422, dört Treasury pack'inde 400 | FU01 §21.4 400 (`…_VALIDATION_FAILED`, yuvarlama yok) olarak hizalandı; dört pack zaten 400. |
| 3 | P2 | İnceleme prompt'unda commit yetkisi çelişkili | Prompt yenilendi: Codex yalnız okur ve bulgu dosyasını yazar (commit / push yok); CT bulgu dosyalarını commit eder, `review-row` satırlarını üretir ve commit eder, sonra push + PR (D31/3). |
| 4 | P2 | DCP §18 AR O-8 muafiyetini açık gösteriyor | "Açık" listesinden çıkarıldı; "Kapandı" listesine D31/1 olarak eklendi. |

Bu ön inceleme resmî CT-APR-02 kaydı değildir. Belge incelemesidir; build / test / runtime yok.

## Kapanış (2026-10-25, sahip)

Sonuç: **PASS** — dört bulgu kapandı; prompt'taki 9/9 hedef SHA-256 `@e26014bbe` eşleşiyor; prompt resmî CT-APR-02
incelemesini başlatmaya uygun. Bu, dokuz hedefin resmî incelemesinin tamamlandığı anlamına gelmez; bulgu dosyası ve
inceleme satırı henüz yok.
