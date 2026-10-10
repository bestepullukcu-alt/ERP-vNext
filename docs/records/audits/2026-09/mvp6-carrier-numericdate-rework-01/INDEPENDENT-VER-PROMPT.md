# MVP6-CARRIER-NUMERICDATE-REWORK-VER-01

Role: source writer'dan farklı bağımsız verifier; strict source no-write.

## NE

`MVP6-CARRIER-NUMERICDATE-REWORK-01` exact candidate'ını bağımsız doğrula. F-01 CLOSED sonucunu koru ve yalnız F-02 JSON türü kapanışını değerlendir.

## GİRDİLER

- Bu dizindeki `numericdate-rework.patch`, `DELTA-MANIFEST.tsv`, `FINAL-22-SOURCE-MANIFEST.tsv`, `BUILD-SOURCE-MANIFEST.tsv`, `candidate-source.tar.gz`, `evidence.tar.gz` ve `OWNER-DECISION-TEXT.md`.
- Predecessor: `mvp6-carrier-auth-token-rework-dev-01` ve `mvp6-carrier-auth-token-rework-ver-02`.
- Gerçek owner'ın candidate hash'lerine bağlı uygulama/VER kararı.

## NASIL

1. Owner kararını exact patch ve manifest hash'lerine bağla. Karar yoksa uygulama yapmadan authority gap'i raporla.
2. Candidate archive ve 3,333-entry build-source manifestini doğrula; iki-file delta dışında source drift kabul etme.
3. Native `/Users/natig/.dotnet/dotnet` SDK 8.0.417/runtime 8.0.23 ile ayrı disposable ortamda fresh build yap; major roll-forward kullanma.
4. Raw signed payload üzerinden `iat`/`nbf`/`exp` JSON Number türünü bağımsız ölç. Digit-only JSON string, fractional ve Int64 dışı number retlerini doğrula.
5. Aynı imzalı payloadın exact duplicate/cardinality denetimini koru. Normalize edilmiş `Claim.Value` görünümünü raw JSON türü veya duplicate kanıtı sayma.
6. Geçerli numeric token, configured skew, `iat == nbf`, expiry ve maximum lifetime sınırlarını tekrar doğrula.
7. Lane'e özel DB-010 Mongo ve port kullan. Her ret için HTTP 401 ve sıfır scoped `mdm_legal_entities` repository erişimini bağımsız ölç.
8. Signature validation gerçekleşmeden raw payloaddan identity/authority üretilmediğini kaynak ve negatif signed-token vakalarıyla doğrula.
9. Başarılı sonuçta Auth login/refresh re-resolution ve Carrier E2E için exact handoff ver; CT/full-module acceptance ilan etme.

## YAPMA

Kaynak düzeltme, Carrier UI/gateway/permission, JWT gevşetmesi, operational 27017, secret/bearer arşivleme, commit/push/stash veya rollout yok.

## ÇIKTI

SOP §22 bağımsız CLOSED/OPEN disposition; source→build→binary→process→HTTP/Mongo zinciri; full 49-case matrix; no-change ve cleanup kanıtı.
