---
record_id: TREASURY-BANK-DISPATCH-GAP-LIST-08
kind: sahip inceleme bulguları ve kapanışları (MOD-0269 BANK-DISPATCH 0.1.0 rev 2) — onay değildir
received_at_local: 2026-10-25
received_from: depo sahibi (K1), sohbette yapıştırılan inceleme ("CONDITIONAL PASS — dondurmadan önce üç düzeltme")
reviewed_input: .ct-teslim/treasury-intake/treasury-MOD0269-contract-ba44787d0.zip (sha256 67437e498053ba53f827c857b4bfa57f21467b6541b1633f9cf78073c3cf8077)
fixed_in: docs/analysis/contracts/drafts/bank-dispatch-v0.1-proposal.md (rev 3), MOD-0126 pack, ZIP üretim betiği (bu kaydın commit'i)
previous: GAP-LIST-07-BANK-DISPATCH.md
---

# BANK-DISPATCH 0.1.0 — gap listesi 8 ve kapanışları

| # | Öncelik | Gap | Kapanış |
|---|---|---|---|
| P1 | P1 | Manifest yolları ZIP ile uyuşmuyor (`docs/` ve `execution/` önekleri ilk yol parçası kadar kesilmiş: `analysis/…`, `tion/…`) | Neden: ZIP betiği `git archive --prefix` varsayıp ilk parçayı her yoldan siliyordu. Düzeltme: manifest ZIP köküne yazılır, yollar ZIP içindeki yolların aynısıdır; betik ZIP'i boş bir klasöre açıp `shasum -a 256 -c MANIFEST.sha256`'yı koşar, geçmezse ZIP verilmez. Önceki ZIP'lerin (`…-ba44787d0`, `…-975356da0`, `treasury-packs-14e650974`) manifestleri aynı hatayı taşır; içerik hash'leri doğru, yollar yanlış. |
| P2-a | P2 | Geri alma sonrası "aynı sıra / aynı `dispatchKey`" garantisi yanlış (arada başka Release commit ederse yeniden deneme yeni sıra alır) | Sözleşme §4.2: commit edilmiş makbuz yanıtını (ve anahtarını) korur; geri alınan deneme sıra ya da anahtar rezerve etmez; dağıtım kapalı sürümde yeterli, gerçek bağdaştırıcıda anahtar uzak çağrıdan önce kalıcı niyete yazılır (§6/3). MOD-0126 Runtime Constraints, Failure Path ve AC-37(c) buna göre düzeltildi. GAP-LIST-07 P1-b kapanışındaki "aynı istemci anahtarı aynı `dispatchKey`" cümlesi bu kayıtla düzeltilmiştir. |
| P2-b | P2 | `releaseSequence` sınırları çelişkili (`int ≥ 1` ama desen dokuz hane) | Aralık 1..999 999 999 (desenle aynı, `int` içinde); aralık dışı → `BANK_DISPATCH_REQUEST_INVALID`; MOD-0126 üst sınırdaki partide artırma ve port çağrısından önce 409 `PAYMENT_RELEASE_SEQUENCE_EXHAUSTED` (kayıt yok, taşma yok). AC-37(c2) ve §5.1 sınır vakaları. |
| iyileştirme | — | Durum sorgusunda yanlış `contractVersion` için §3.4 ve §5 farklı kod | Tek eşleme: gönderimde ve durum sorgusunda `BANK_DISPATCH_CONTRACT_VERSION_UNSUPPORTED`; diğer ihlaller `REQUEST_INVALID`. §5.1'e vaka eklendi. |

Belge incelemesidir; build / test / runtime yok.

## Kapanış incelemesi (2026-10-25, sahip)

İnceleme girdisi `treasury-packs-126-119-120-124-269-473e3b3ee.zip` (sha256 `f67ff17a08eb7cc9e45a447a012b71b57c687a0ba649a97a84f2f4b26550c237`;
manifest 9/9 geçti). Sonuç: tasarım/belge incelemesi **PASS**; geliştirmeye hazırlık **CONDITIONAL PASS**.

| Modül | Sonuç |
|---|---|
| MOD-0126 Payments | PASS |
| MOD-0119 AP | PASS |
| MOD-0120 AR | PASS |
| MOD-0124 Cash Positioning | PASS |
| MOD-0269 Bank Dispatch | PASS — sözleşme taslağı (ayrı uygulama pack'i yok) |

Tek küçük düzeltme: MOD-0126 "Sözleşme bağı" satırı rev 2 diyordu → rev 3 yapıldı (bu bölümün commit'i).

Geliştirme öncesi açık koşullar (sahibin listesi): MOD-0023 saklama politikası, Reconcile eşleme alanları ve aynı anahtarlı
yarış kanıtı; MOD-0143 `totalAmount` net/brüt onayı; SEC-002 tüketim yolu, para birimi hassasiyeti, RCS ve ilgili
sözleşmeler; merkezi denetim `Denied` boşluğu (G-AUD-1) ve sürüme bağlı onaylar (CT-APR-01/02); tanımlı AC'lerin uygulanıp
çalıştırılması (belgedeki testler çalışma kanıtı değildir). Dört pack'te toplam 157 AC tanımı, modül içinde mükerrer yok.
