---
record_id: TREASURY-GAP-LIST-19
kind: sahip inceleme bulguları ve kapanışları (tam Treasury paketi) — onay değildir
received_at_local: 2026-10-25
received_from: depo sahibi (K1), sohbette yapıştırılan inceleme ("CONDITIONAL PASS — tüm pakette üç doküman açığı")
reviewed_input: .ct-teslim/treasury-intake/treasury-ALL-b9df4e281.zip (sha256 789d9ea3d1cd59a1b4fe654c1b77d9ed9b5416e8b05f4ed0f8bb824e1b6c81b3)
fixed_in: DCP, MOD-0126 pack, bank-dispatch.md (belge düzeltmesi) — bu kaydın commit'i
previous: GAP-LIST-17-D26.md
---

# Gap listesi 19 ve kapanışları

| # | Öncelik | Gap | Kapanış |
|---|---|---|---|
| 1 | P2 | DCP güncel kararlarla hizalı değil (pack yok, MDM onayı bekleniyor, bankacılık sahibi yok, MOD-0220 ilişki modeli eksik) | DCP baştan hizalandı: özet / sorun, üye tablosu (kayıt · pack · kod, AC sayıları, kod alma izni), dış modül tablosu (MOD-0220 §22, MOD-0023 §17, MOD-0143, MOD-0140, MOD-0149, MOD-0048), sahiplik (D20, D27/8, D10/3, D29/2), bağımlılık grafiği (LE↔LE modeli kaldırıldı — TRM-07), sıra, kapı ölçütleri, gözden geçirme soruları cevaplandı, açık kararlar. |
| 2 | P2 | PaymentMethod kararı (O-13, D28) uygulanabilir kurala dönüşmemiş | MOD-0126: alan ve doğrulama tabloları MOD-0048 `payment-method` consumable set'ine bağlandı (uç ölçüldü: `ConsumableReferenceDataController.cs:25, 43-44`, öğe `Code / Label / IsActive`); bilinmeyen / pasif kod 422 `PAYMENT_METHOD_NOT_ALLOWED`; küme okunamıyor 503 `PAYMENT_METHOD_SOURCE_UNAVAILABLE`; kayıtlı kod sonradan pasifleşince Approve / Release etkilenmez. **Ölçüm: küme bugün yok** (`appsettings.json:104` izin listesinde `payment-method` yok) → açık üretici bağımlılığı (bağımlılık satırı + kontrol listesi). AC-41 (sabotajlı, DL-1). |
| 3 | P2 | BANK-DISPATCH §5.1 test beklentisi çelişkili | §5.1 katmanlara ayrıldı: A doğrudan port → `BankDispatchContractException`; B1 tüketici doğrulaması → MOD-0126 400 / 409 / 422, port çağrısı yok; B2 porta ulaşan ihlal → 500; C sıra tükenmesi → 409, port çağrısı yok; D beklenmeyen sonuç → 502. Arayüz değişmedi (0.1.0 FROZEN, belge düzeltmesi değişiklik geçmişinde). MOD-0126 AC-37 aynı katmanlarla yazıldı. |

Ek düzeltme: MOD-0126 bağımlılık özetindeki "MOD-0269 dağıtım arayüzü (taslak, TBD)" → FROZEN 0.1.0.
Kontroller: MOD-0126 24 bölüm, 42 AC satırı (AC-1…41 + 6a), tekil. Belge incelemesidir; build / test / runtime yok.

## Belge kapanışı (2026-10-25, sahip)

İnceleme girdisi `treasury-ALL-54eb86dcf.zip` (sha256 `54f03426f91cad9ee73ba03da9500ac37c7b43dc3eef934af79df94a60edcc88`;
manifest 17/17 geçti). Sonuç: **PASS** — gap 19 kapandı; yeni engelleyici belge açığı yok. MOD-0126'da 42 tekil AC
(AC-1…41 + AC-6a). Paket `ready-for-dev` değildir: kayıtlı onaylar, üretici bağımlılıkları ve MOD-0289 güvenlik onayı açık.
