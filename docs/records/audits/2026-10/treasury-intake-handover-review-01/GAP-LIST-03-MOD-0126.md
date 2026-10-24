---
record_id: TREASURY-MOD0126-GAP-LIST-03
kind: sahip inceleme bulguları ve kapanışları (pack taslağı düzeltmesi) — onay değildir
received_at_local: 2026-10-24
received_from: depo sahibi (K1), sohbette yapıştırılan inceleme sonucu ("CONDITIONAL PASS — önceki beş bulgu büyük ölçüde giderilmiş; üç tutarsızlık kalmış")
reviewed_input: .ct-teslim/treasury-intake/treasury-packs-review-e882cca97.zip (sha256 d526f0a1359d1fdfd58198f1b1d38cbd055fc854da85173901a6d9cc181fe563), pack @ e882cca97
fixed_in: execution/domains/finance-treasury/module-packs/MOD-0126-payments-approvals.md (bu kaydın commit'i)
decisions: yok (üçü de tasarım tutarlılığı; D5 ve D12/2 içinde)
previous: GAP-LIST-02-MOD-0126.md
---

# MOD-0126 pack — gap listesi 3 ve kapanışları

| # | Gap (özet) | Kapanış (pack bölümü) |
|---|---|---|
| G1 | İstemci anahtarının saklandığı yer çelişkili (`PaymentBatch.IdempotencyKey` vs "yalnız makbuzda") | `PaymentBatch.IdempotencyKey` kaldırıldı; `PaymentDispatchAttempt` anahtar yerine `ReceiptId` taşır; `PaymentApprovalDecision` alanı `RemoteIdempotencyKey` (MOD-0023'e giden deterministik uzak anahtar) oldu. Entity Fields'te kural: istemci anahtarı yalnız `PaymentCommandReceipt`'te; uzak anahtarlar ayrı ve deterministik. |
| G2 | `Completed` makbuzun değişmezliği belirsiz (202 belirsiz sonrası Reconcile ne yapar) | `Completed` makbuz değişmez; 202 gövdesi `intentId` + durum bağlantısı taşır ve hep aynı döner; gerçek sonuç niyette izlenir, `GET …/{id}` ve `…/trace` ile okunur. Reconcile yalnız `Pending` makbuzu (tx1 sonrası çökme) tamamlar. |
| G3 | Release için kurtarma modeli yok (`LinkedIntentId` var, release niyeti yok) | Bu dilimde Release **tek işlem**: bağdaştırıcı süreç içi ve ağsız, R11 + bağdaştırıcı + `PaymentDispatchAttempt` + makbuz + tek `…release-attempted` olayı aynı işlemde. İki aşamalı listelerden ve 4a'dan Release çıkarıldı; AC-17 hizalandı. Gerçek dağıtım açılınca `PaymentReleaseIntent` yaşam döngüsü o pakette (Follow-up). |
| — | Merkezi `Denied` kaydı (G-AUD-1), bağımlılık ve onay engelleri | Açık; pack `draft` kalır. |

Bağımsız inceleme (CT-APR-02) yapılmadı; build/test yok.

## Kapanış incelemesi (2026-10-24, sahip)

Sonuç: gap listesi 3 **PASS**; geliştirmeye hazırlık **CONDITIONAL PASS**. Kalan iki belge düzeltmesi yapıldı:
(1) Runtime 4a iki aşamalı sonuç olayları listesinden `…release-attempted` çıkarıldı; (2) Reconcile'ın makbuzu tamamladığı
her yerde "yalnız `Pending` ise" koşulu ve değişmezlik kuralı aynı anlatımla yazıldı (Idempotency maddesi, Çökme, 4a,
Audited Events Reconcile satırı). `ready-for-dev` için bağımlılık sözleşmeleri, onaylar ve G-AUD-1 kapanmalı.
