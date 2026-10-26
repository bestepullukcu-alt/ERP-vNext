---
record_id: TREASURY-MOD0126-GAP-LIST-02
kind: sahip inceleme bulguları ve kapanışları (pack taslağı düzeltmesi) — onay değildir
received_at_local: 2026-10-24
received_from: depo sahibi (K1), sohbette yapıştırılan inceleme sonucu ("CONDITIONAL PASS — önemli ilerleme var; birkaç tasarım çelişkisi sürüyor")
reviewed_input: .ct-teslim/treasury-intake/treasury-packs-review-3bcfd1d03.zip (sha256 fcca4dcf7795eb74d957cfcccf2be1cf085b0e38d2c8b781a82564a9595c0ec2), pack @ 3bcfd1d03
fixed_in: execution/domains/finance-treasury/module-packs/MOD-0126-payments-approvals.md (bu kaydın commit'i)
decisions: D13 (docs/records/decisions/2026-10/treasury-intake-owner-decisions-02.md)
previous: GAP-LIST-01-MOD-0126.md
---

# MOD-0126 pack — gap listesi 2 ve kapanışları

| # | Öncelik | Gap (özet) | Kapanış (pack bölümü) |
|---|---|---|---|
| G1 | P1 | Replay ve recovery çelişiyor (aynı anahtar ilk yanıtı döndürüyor mu, Reconcile'a mı giriyor) | Runtime Constraints Idempotency: replay yalnız kayıtlı yanıtı döndürür ya da makbuz `Pending` ise 202 `PAYMENT_REQUEST_IN_PROGRESS`; başka adım yok. **Kurtarma yalnız `ReconcilePaymentBatchWorkflowCommand` ile** (kendi anahtarı ve makbuzu). 4a çökme metni, Failure Path, AC-13, AC-16 hizalandı. |
| G2 | P1 | Makbuzun zamanı belirsiz (tx1'de sonuç yokken yanıt yazılıyor) | Yeni `PaymentCommandReceipt` varlığı: `Pending` / `Completed` yaşam döngüsü; tek aşamalı komutta tx1'de `Completed`; iki aşamalı komutta tx1'de `Pending` (yanıtsız, `LinkedIntentId`), yanıt tx2'de ya da Reconcile'da yazılır; çökme davranışı tanımlı. |
| G3 | P1 | İndeks tablosu eski `TenantId + IdempotencyKey` tekilliğini kullanıyor | Dört eski idempotency indeksi kaldırıldı; yerine tek `ux_payment_command_receipt_scope` (altı alan). İş varlıklarındaki anahtar alanı istemci anahtarını taşımaz; `PaymentWorkflowStartIntent.IdempotencyKey` uzak (MOD-0023) anahtardır, kendi tekil indeksi var. `ux_payment_decision_open` ve `ix_payment_command_receipt_pending` eklendi. |
| G4 | P1 | Muafiyet adayı seçilemiyor (R4 yalnız tier-2 aday seçiyor, R9 tier-2'siz kişiye muafiyet veriyor) | Sahip kararı D13: tier-2 aday yoksa `waive-sod` izni olanlar `IsWaiverCandidate = true` ile aday olur, yalnız muafiyetli onay verebilir; tier-2 aday varken eklenmez. R4 güncellendi, `PaymentApprovalDecision.IsWaiverCandidate`, AC-6a. |
| G5 | P2 | Karar durum sözlüğü tutarsız (`RefusedByWorkflow` / `Refused` / `Cancelled`) | Owned Objects'e tek geçiş matrisi: başlatma ve karar niyeti aynı sonuç sözlüğü (`Started`/`Recorded`, `Refused`, `Cancelled`, `Unknown`); her sonuç bir denetim olayına bağlı. `RefusedByWorkflow` kullanılmaz. |
| G6 | not | `submitted` sonuç olayı tabloda `Status → Submitted` yazıyordu | Audited Events: Submit satırı sonuç olayı = `Submitted → PendingApproval` (tx2/Reconcile); tx1 deneme olayı ayrı. Reconcile kendi olayını değil kapattığı niyetin sonuç olayını yazar. Olumsuz sonuç olayları (`…refused`, `…cancelled`) eklendi. |
| — | açık | Merkezi `Denied` kaydı (G-AUD-1) | Kapanmadı: CentralAudit başarısız komutu kaydetmiyor; follow-up BL-501 sahibinde. Pack bunu bilinen boşluk olarak tutuyor. |

İnceleyicinin notu doğru: bağımlılık sözleşmeleri ve onaylar açık, pack `draft` kalır. Bağımsız inceleme (CT-APR-02) yapılmadı; build/test yok.
