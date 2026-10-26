---
record_id: TREASURY-MOD0126-GAP-LIST-01
kind: sahip inceleme bulguları ve kapanışları (pack taslağı düzeltmesi) — onay değildir
received_at_local: 2026-10-24
received_from: depo sahibi (K1), sohbette yapıştırılan inceleme sonucu ("CONDITIONAL PASS — planlama paketi kapsamlı, ancak geliştirmeye hazır değil")
reviewed_input: .ct-teslim/treasury-intake/treasury-packs-review-afc8ad486.zip (sha256 5157f6f0ddcd8da92d948add40b12d26d4d10ebcd69847eebb0b23fecdfff32d), pack @ afc8ad486
fixed_in: execution/domains/finance-treasury/module-packs/MOD-0126-payments-approvals.md (bu kaydın commit'i)
decisions: D12/1, D12/2 (docs/records/decisions/2026-10/treasury-intake-owner-decisions-02.md)
---

# MOD-0126 pack — gap listesi 1 ve kapanışları

İnceleme sonucu: **CONDITIONAL PASS.** Aşağıdaki her madde pack'te düzeltildi; pack hâlâ `draft`, `ready-for-dev`
engelleri (CT-APR-02, CT-APR-01, DCP, port, TRM-06, TRM-14, RCS-001, kod alma izni) açık.

| # | Öncelik | Gap (özet) | Kapanış (pack bölümü) |
|---|---|---|---|
| G1 | P1 | SoD ayarı yokken davranış çelişkili ("her onay tier-2" ile "servis başlamaz") | Runtime Constraints SoD maddesi: iki durum ayrıldı — ayar yok → hata değil, `DefaultAllTier2`; ayar deposu erişilemiyor → 503 `PAYMENT_SOD_POLICY_UNAVAILABLE`, servis açılışı ayara bağlı değil. Failure Path ve AC-7 hizalandı (iki ayrı test). |
| G2 | P1 | Muafiyet kuralları tutarsız (R9 R1/R3/R4, O-17 R3/R4, aday dışlama, Release R1–R4) | Sahip kararı D12/1: muafiyet yalnız R4. R9, R11, AC-9, Failure Path, Tasarım notları, O-17 güncellendi; yeni kod `PAYMENT_SOD_WAIVER_NOT_ALLOWED` (403). |
| G3 | P1 | Idempotency isteğe bağlı / sunucu üretiyor; Submit tekrarında çelişki | Sahip kararı D12/2: her yazma komutunda zorunlu (400 `PAYMENT_IDEMPOTENCY_KEY_REQUIRED`); makbuz kapsamı (Tenant, LE, Actor, Operation, Target, Key); kontrol sırası makbuz → açık niyet → CAS → kurallar → tx1; aynı anahtarla tekrar makbuzdaki ilk yanıtı (202 dahil) döndürür, farklı anahtar açık niyette 409. Entity Fields, Validation Rules, AC-13, AC-16 güncellendi. |
| G4 | P1 | Belirsiz karar varken ikinci kararın uzak sisteme gitmesi engellenmemiş; AC-31 isteğe bağlı | Parti başına tek açık karar niyeti: tekil kısmi indeks `(TenantId, BatchId)` `State ∈ {Requested, Unknown}` + `Version` CAS ile atomik tx1; ikinci karar uzak çağrısız 409 `PAYMENT_DECISION_PENDING`. Başlatma niyeti için aynı kural. AC-31 zorunlu, üç yarış senaryosu + indeks sabotajı. |
| G5 | P1 | Denetim garantisi iki aşamalı işlemle uzlaşmamış (AC-17 "komut başına tek olay") | Runtime 4a: tx1 deneme olayı + tx2 sonuç olayı; uzak başarı sonrası tx2 hatası / çökme → niyet açık, Reconcile uzak çağrıyı tekrarlamadan sonucu yazar; K2'de deneme olayı yazılamazsa uzak çağrı yok. Audited Events'e deneme olayı adları eklendi; AC-17 yeniden yazıldı (kurtarma testi dahil). |
| G6 | P2 | Bağımlılık sözleşmeleri hazır değil (PAYMENT-BATCH, MOD-0269, MOD-0220 eki, şablon, küsurat) | Dependencies altına "kapanış tablosu": sözleşme dosyası/sürüm, hash, sahip, hata davranışı, kapanış kanıtı (sözleşme testi ile canlı kanıt ayrı), dilim. |
| G7 | not | Opak banka/alacaklı referansları yalnız Guid | Runtime "Bankaya dağıtım kapalı" maddesine zorunlu kapı: hesap sahipliği, Tenant/LE uygunluğu, alacaklı–hesap eşleşmesi kapanmadan gerçek bağdaştırıcı bağlanamaz. |

Ölçülmeyen: pack'teki değişikliklerin bağımsız incelemesi (CT-APR-02) yapılmadı; kaynak derleme/test yeniden koşulmadı.
