---
record_id: TREASURY-BANK-DISPATCH-GAP-LIST-07
kind: sahip inceleme bulguları ve kapanışları (MOD-0269 BANK-DISPATCH 0.1.0 taslak sözleşmesi) — onay değildir
received_at_local: 2026-10-25
received_from: depo sahibi (K1), sohbette yapıştırılan inceleme ("CONDITIONAL PASS — dağıtım kapalı taslak için uygun; dondurulmadan önce şu boşluklar kapanmalı")
reviewed_input: .ct-teslim/treasury-intake/treasury-MOD0269-contract-975356da0.zip (sha256 bdcb8bcda870cf3fa96e6ef1a2b1790acec8e11494e9e2c11894b98bd18a314f)
fixed_in: docs/analysis/contracts/drafts/bank-dispatch-v0.1-proposal.md (rev 2), MOD-0126 pack (bu kaydın commit'i)
decisions: D17 (kaynak hesap talimat düzeyinde; beklenmeyen sonuçta kaydet + kilitle)
previous: GAP-LIST-06-AP-AR-CP.md
---

# BANK-DISPATCH 0.1.0 — gap listesi 7 ve kapanışları

| # | Öncelik | Gap | Kapanış |
|---|---|---|---|
| P1-a | P1 | Kaynak hesap kayboluyor: MOD-0126'da talimat başına `SourceBankAccountRef`, sözleşmede yalnız parti düzeyi | D17/1: `sourceBankAccountRef` talimata taşındı, parti alanı kaldırıldı; "birleştirme, gruplama, tek hesaba indirgeme yok" kuralı (sözleşme §3.2); MOD-0126 `DefaultSourceBankAccountRef`'i talimat değerinin yerine koymaz. Test: AC-37 (a) iki kaynak hesaplı parti. |
| P1-b | P1 | `dispatchKey` üretimi eksik (BD-2 açık) | Sözleşme §4.2 + MOD-0126 `PaymentBatch.ReleaseSequence`: yeni Release denemesi aynı işlemde +1 (CAS), makbuz tekrarı yeni sıra almaz ve portu çağırmaz, geri alınan işlemin artışı da geri alınır → aynı istemci anahtarı aynı `dispatchKey`. İstek `releaseSequence` taşır; port anahtarı yeniden üretip karşılaştırır. BD-2 kapandı. Test: AC-37 (b)(c)(d). |
| P1-c | P1 | "Aynı anahtar → aynı sonuç" ile durumsuz bağdaştırıcı çelişiyor | §4.1: bağdaştırıcı garantisi sabit `NotConfigured` alanlarına daraltıldı, `decidedAt` garantinin parçası değil; tam yanıt tekrarı MOD-0126 makbuzunun; anahtar/yük çatışması 0.1'de MOD-0126'nın (yapısal: anahtar parti + sıra, `ux_payment_dispatch_attempt_key` tekil indeksi, `Approved` partinin talimatları değişmez), gerçek bağdaştırıcıda bağdaştırıcının (`BANK_DISPATCH_KEY_PAYLOAD_CONFLICT`, §6/4). |
| P2-a | P2 | Hata dönüş biçimi | §2 + §5: iş sonucu her zaman tipli sonuç; sözleşme ihlali tek istisna tipi `BankDispatchContractException { Code }`; iptal `OperationCanceledException`. Eşleme: `NotConfigured` → 200; ihlal / sözleşme dışı istisna → işlem geri alma + 500 `PAYMENT_BANK_DISPATCH_CONTRACT_VIOLATION` (makbuzda saklanmaz); iptal → geri alma, kayıt yok. MOD-0126 hata sözlüğü, Runtime Constraints ve Failure Path güncellendi. |
| P2-b | P2 | Doğrulama tamlığı | §3: tüm Guid alanlarında `Guid.Empty` yasak; `correlationId` ölçüldü — UUID **değil**, sunucunun opak korelasyon değeri (imzalı korelasyon UUID değildir, `CorrelationIdMiddleware.cs:74@975356da`; ≤ 128, `ObservabilityOptions.cs:66@975356da`); `instructionId` tekil; hassasiyet için `currencyMinorUnits` alanı (MOD-0126, MOD-0048'den doldurur), Σ `amount` = `totalAmount` tam eşitlik; durum sorgusu doğrulaması. Negatif test listesi §5.1 → MOD-0126 AC-37 (sabotajlı). |
| iyileştirme 1 | — | Süreç içi portta "kablo üzerinde string" | §3: 0.1'de serileştirme yok, .NET `decimal` / `Guid` / `DateOnly` / `DateTimeOffset`; ileride serileştirme kuralı ayrı yazıldı (string, değişmez kültür, üs yok; Mongo `Decimal128`). |
| iyileştirme 2 | — | 0.1 tüketicisi `Accepted` / `Rejected` / `Unknown` alırsa | D17/2: başarı sayılmaz — deneme alınan sonuçla kaydedilir, parti `Approved` + `ReleaseBlockedForReview`, 502 `PAYMENT_BANK_DISPATCH_OUTCOME_UNSUPPORTED`; sonraki Release 409 `PAYMENT_RELEASE_BLOCKED_FOR_REVIEW`. Kilit açma komutu Follow-up. Test: AC-38 (sabotajlı). |

Kontroller: MOD-0126 pack 24 bölüm, AC'ler tekil (38 kimlik + AC-6a = 39 satır; yeni AC-37, AC-38 DL-3'te kapanır),
dilim tablosu okunuyor; `ct_queue.py validate --queue treasury-intake` 0 hata. Belge incelemesidir; build / test / runtime yok.

Açık kalanlar (dondurma öncesi): BD-1 (`rail`), BD-3 (bankacılık sahibi), BD-4 (dondurma: CT incelemesi + sahip onayı).
