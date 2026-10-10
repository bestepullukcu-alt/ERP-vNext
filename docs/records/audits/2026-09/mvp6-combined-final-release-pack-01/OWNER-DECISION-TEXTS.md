# Owner decision proposals — NOT APPROVED

Each block is a distinct authority. Approving one does not implicitly approve another. D186/D187/root business decisions are already granted; none is requested again.

## A — Consumer release disposition / consent

> SHIPMENT-BUNDLE3.0.0 / wirev1 önerisini, YAML SHA256 `5dfe7c1bba32551bd8d4b532243878684e69a6d9560e667a4183bfd516b9d21c`, publication patch SHA256 `944a228d076807d643fa1ce714a982aab2e8438e064aea3479a90b4e11b75396` ve PUBLICATION-MANIFEST.sha256 içindeki üç annex hash'i için kabul ediyorum. Consumer release disposition kapsamım: Shipment açısından additive optional-nullable root sözleşmesi; Loads açısından değişmeyen operation/schema yüzeyi; Returns/Claims açısından onaylı draft tasarımların bu exact contract ile schema/model uyumluluğudur. Bu consent çalışan producer/consumer uptake veya deployment readiness değildir. Loads çoklu-Shipment root politikası ve root runtime uptake'i HELD kalır; bağımsız root üretme veya grouping'i sessiz daraltma yetkisi yoktur. Eski script/test tüketimini3.0.0 uptake saymıyorum; eski version consent'ini taşımıyorum. Runtime/Program.cs, migration/backfill, Phase1.5, pack promotion, rollout veya DEV GO vermiyorum.

Accountable consumer-owner identity/scope must be recorded from the actual approving message; agent is not signer. Existing no-external-consumer attestation is not re-requested.

## B — Exact publication authority

> Yukarıdaki exact3.0.0/wirev1 yayın önerisini ve PUBLICATION-MANIFEST.sha256 içindeki dört hedefi, patch SHA256 `944a228d076807d643fa1ce714a982aab2e8438e064aea3479a90b4e11b75396` ile tek publication owner'ın uygulaması için onaylıyorum. Mevcut YAML baseline'ı `93c696e2fba13dbc8fbfcf2cd1ae0ae0bd93cd9d0935b3ee743229e810163571` olmalı; üç yeni annex hedefi mevcut olmamalıdır. Gerçek checkout'a uygulama, ayrı gerekli consumer/guard yetkileri kaydedildikten ve disposable publication+binding üzerinde production-mode ReadAuthority/tam DocsPathGuard doğrulandıktan sonra yapılabilir. Yeni metadata/hash/scope değişikliği bu onaya dahil değildir. Mevcut kullanıcı/concurrent değişiklikleri korunur; başarısızlıkta kapsam genişletilmez. Runtime veya deployment yetkisi vermiyorum.

## C — Guard authority, separately enumerated

> **C1 Policy/code:** R2 disposition'daki yalnız iki exact roadmap path/hash istisnası, üç boundary testi ve kural açıklaması için uygulama yetkisi veriyorum; klasör-geneli istisna vermiyorum. İncelenen birleşik policy/fixture/relocation diff SHA256 `e1b7b5eea0d3ec3788ad44283a39d5d1d773b6b2de2dcab9216855aace4481e6`. Alt parçalar guard-review-components manifestiyle bağlıdır; aynı değişiklik iki kez uygulanmaz.
>
> **C2 Fixture:** `fixture-fix.patch` SHA256 `87932c261ff46f6c7464f9f28d556b7a9e8b0783411c9200b6d86d30aa8b66bf` için gerçek checkout uygulama yetkisi veriyorum. Yalnız ReadAllText/WriteAllText→File.Copy byte-preserving düzeltmesidir; yeni seal uydurma veya mevcut seal'i yeniden meşrulaştırma yetkisi değildir. Önceden verilmiş byte-identical working-artifact relocation yetkisi korunur; tarihsel17seal ve mevcut toplam22seal değerleri değişmez.
>
> **C3 Binding/production activation:** Reader payload SHA256 `85154a3e3368bfe187447bde269d0041a5863f3ef8b57b8e4397de97635cb270` (11185byte; raw canonicalTargets + tekLF + raw sealedInputs; BOM/sonLF yok) için exact binding'i onaylıyorum. Target YAML hash'i `5dfe7c1bba32551bd8d4b532243878684e69a6d9560e667a4183bfd516b9d21c`; Carrier target değişmez. Bu gerçek onay mesajından schema-uygun DOCS_PATH_OWNER_DECISION kaydı üretilebilir; gerçek owner/APPROVED durumu yazıldıktan sonra karar DOSYASININ finalSHA256'sı authority.decision.sha256'ya bağlanmalıdır. Sadece bu metadata stamping'i yetkilidir; payload dizileri/targets/seals yeniden biçimlendirilmez. Son authority/decision dosya hash'leri raporlanır. Production activation yalnız disposable publication+approved binding üzerinde gerçek production-mode ReadAuthority ve DocsPathGuard geçerse, güncel baseline tekrar eşleşirse yapılabilir. Synthetic APPROVED kayıt kullanılamaz; test/kural gevşetilemez. Tam-suite dış kapsam hataları otomatik waived sayılmaz.

C1/C2 preparation or approval alone is not C3 activation authority. Current draft authority/decision remain UNAPPROVED. Their file hashes are review pins, not future stamped approval file hashes. Any payload change requires new exact consent.
