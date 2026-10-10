# Acceptance örnekleri — öneri, çalıştırılmış runtime testleri değil

Fixture sembolleri T1/T2, L1/L2, S1/S2, issuer I ve actor U/V gerçek production kimliği değildir. Her başarısız mutation beklentisi: sıfır business write, receipt ve event. Denial audit kaydı bu sayımdan hariçtir. Yeni wire davranışları SS-04 concurrence/contract kapısına tabidir.

| ID / karar | Given / When | Exact expected |
|---|---|---|
| AC-01 SS-01 | SCE ownership önerisi seçilir | SCE/Diten.SupplyChainService spec hedefi; domain/DCP/registry ve draft pack byte'ları değişmez; ready-for-dev ilan edilmez |
| AC-02 SS-02 | SUPPLIER-BASE bağı çözülür | Mevcut SUPPLIER1.0.0 hash'i referans alınır; ikinci master veya yeni approved API oluşmaz |
| AC-03 SS-03 | Eligible T1/L1/S1; her dört base status ile yeni evaluation ve risk create | Active evaluation201; diğerleri422; risk dört durumda201. İki komut da getSupplier+LE authority ister |
| AC-04 SS-03 | S1 Active fakat LE authority cevabı yok; yahut base timeout/malformed ID | Success yok; live seam GAP, fixture'da dependency503; status Active LE uygunluğu sayılmaz |
| AC-05 SS-03 | Valid binding, base OnHold/Blocked/Inactive | status200 SUSPENDED/count0/lastSubmissionAt absent; list/get/create/submit403, receipt dönmez |
| AC-06 SS-03 | Gelecek bulk fixture: requested[S1,S2], response[S2,S1]; sonra S2 missing/duplicate/extraS3/unknown enum | İlk case ID ile doğru eşleşir; diğerleri dependency-invalid503. known=false explicit unknown; boş results success değildir |
| AC-07 SS-04 | Frozen YAML'ları before/after hashle | SUPPLIER ve SUPPLIER-PERFORMANCE byte eşit; status/null/503/LE değişiklikleri bu pakette uygulanmamış |
| AC-08 SS-05 | Aynı I/U/T1/L1 için zero / two active bindings; tek binding S1 | İlk ikisi403 unresolved; tek binding trusted S1. I/U/T1/L2 ayrı yetkili binding olabilir; implicit LE seçimi yok |
| AC-09 SS-05 | Expired JWT+malformed body; valid JWT grant yok+foreign target | Sırasıyla401 ve403; mapping/target lookup yapılmaz |
| AC-10 SS-05 | T1/L1/S1 valid mapping; target T2/L1/S1 veya T1/L2/S1 veya T1/L1/S2 veya deleted | Her biri404 UNKNOWN_SUBMISSION; id varlığı ayrımı yok; doğru own target200 |
| AC-11 SS-05 | Revocation commit sonrası eski geçerli JWT ile cached receipt isteği; veya mapping authority timeout | Revoked403 replay yok; timeout503 PORTAL_IDENTITY_UNAVAILABLE. Revoke-before-mutation-commit fence kaybeden write/event0 |
| AC-12 SS-05 | Auth/scoped create body/query supplierId/tenantId/legalEntityId override |400 INVALID_REQUEST; mapping değişmez. Arbitrary payload alanı authorization girdisi olmaz |
| AC-13 SS-06 | Create S1 keyK→201; aynı actor/LE/body keyK tekrar |200 aynı submissionId/version, toplam1 write/receipt; create event varsa en çok1. JSON object alan sırası farkı aynı fingerprint; array/string farkı farklıdır |
| AC-14 SS-06 | Başarılı submit keyK If-Match1; aynı request tekrar artık version2/SUBMITTED; yeni keyJ If-Match1; keyJ If-Match2 | Sırasıyla200 replay tek submitted event;409 VERSION_CONFLICT;422 INVALID_STATUS_TRANSITION |
| AC-15 SS-06 | Aynı tuple/key fakat body veya If-Match farklı |409 IDEMPOTENCY_KEY_REUSED, eski receipt değişmez. key baş/son whitespace400; trim alias yok |
| AC-16 SS-06 | S1→S2 remap; keyK yeniden create; S1 target; ayrıca aynı S1 actorV veya LE L2 | S2 namespace ayrı yeni create; S1 target404. V ve L2 için bağımsız receipt; U receipt'i dönmez. Aynı U/S1 yeni active binding revision current gates sonrası eski receipt'i okuyabilir |
| AC-17 SS-06 | Aynı tuple eşzamanlı identical request / farklı fingerprint | Identical: tek commit+outbox, diğer200 replay; farklı: bir winner, diğer409. Receipt business record ömründe key yeniden yeni işlem üretmez |
| AC-18 SS-07 | Registry-authorized ON_TIME_DELIVERY94 weight60; QUALITY_ACCEPTANCE98 weight40 | Exact sum95.6 → overallScore95.60, LOW; sonuç client overallScore'dan alınmaz |
| AC-19 SS-07 | Tek metric weight100, values89.995 /90 /74.995 /49.995 | overall90.00/MEDIUM;90.00/LOW;75.00/HIGH;50.00/CRITICAL. Band raw score üzerinden; display rounding bandı değiştirmez |
| AC-20 SS-07 | Values1.225 ve1.235 weight100 ayrı örnekler | Half-even outputs1.22 ve1.24. 1.23456 input422; weight toplam99.99 veya duplicate code veya weight0 →422 |
| AC-21 SS-07 | Unsupported unit/direction/code; registry missing revision/timeout/stale validity | Bilinen unsupported422; authority doğrulanamayan503, write yok; eksik metric drop/weight renormalization yok |
| AC-22 SS-07 | Create revisionR1, sonra registryR2; submit | R1 snapshot ile aynı score; R2 retroactive etkilemez. R1 withdrawn422; authenticity doğrulanamıyor503; mevcut published scorecard immutable |
| AC-23 SS-08 | OPEN v1 risk; current If-Match ile ACKNOWLEDGED→MITIGATED(note)→CLOSED(note) | Version2→3→4, her adım1 event; MITIGATED server resolvedAt, CLOSED aynı timestamp |
| AC-24 SS-08 | OPEN→CLOSED; CLOSED→OPEN; same-state fresh key; MITIGATED boş note | Hepsi422; stale version varsa önce409. Başarılı aynı-key receipt tekrarında200, event/version artmaz |
| AC-25 SS-08 | Source SCORECARD same-scope published / foreign; PORTAL_SUBMISSION DRAFT/SUBMITTED; MANUAL/EXTERNAL_SIGNAL | Published ve scoped SUBMITTED kabul; foreign/DRAFT/son iki source type422 generic, disclosure yok. Resolver outage503 önerisi ayrı wire review ister |
| AC-26 SS-08 | Register taxonomy authority unavailable; existing risk mitigation sırasında aynı outage | Register503; existing valid transition local snapshot ile200. Otomatik SupplierRisk create veya scorecard-band kaynaklı level mutation yok |
| AC-27 SS-09 | Gelecekte fixture seti tüm case'leri geçirir | SIMULATED evidence only; producer canlılık/G5/consent/ready-for-dev/runtime activation sonucu çıkmaz |

## Açık fixture kapsamı

Önerilen set: base dört status+unknown+malformed+timeout; binding zero/one/many/revoked/remapped+revision fence; trusted scope T1/L1 ve cross-scope; deterministic replay race; iki yüzde metric revisionR1/R2/withdrawn; taxonomy revision ve source fixtures. Clock sabit UTC, kimlikler sabit test değerleri; gerçek token/secret/kişisel veri yok. Gerçek transport, issuer, claim schema, deployment ve producer endpoint'i fixture tarafından tanımlanmaz.
