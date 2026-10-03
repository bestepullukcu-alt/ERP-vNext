# MVP6-MOD0183-ROOT-PREP-RECOVERY-01 — v1.0 DRAFT

Reviewable spec paketi; runtime yetkisi YOK. Mevcut pack ve tarihsel kayıtlar değiştirilmedi.

- [Scope + Phase1.5](scope-and-phase-1.5.md) / [exact prospective paths](prospective-paths.txt)
- [Tek owner provenance karar önerisi](owner-provenance-decision.md)
- [Test edilebilir acceptance](acceptance.md)
- [HELD DEV](DEV-v1.0-HELD.md) / [HELD bağımsız VER](VER-v1.0-HELD.md)
- [SOP22 recovery raporu](SOP-22-REPORT.md), input-manifest.json, validation.json, output-manifest.sha256

## Üç ayrı kapı
| Kapı | Gereken | Vermediği yetki |
|---|---|---|
| A canonical publication |Exact approved version/hash artifact, publication/guard yetkisi ve tamamlanmış yayın kanıtı|Runtime/rollout otomatik açılmaz|
| B isolated DEV/VER |Açık bounded runtime scope ve Phase1.5 onayı; erişilebilir exact approved contract target; isolated test ortamı|Production dataset/rollout consent değildir|
| C deployment/rollout |Yayımlanmış exact contract, independently verified runtime, hedef cohort provenance disposition ve deployment yetkisi|Fixture PASS legacy veriyi kanıtlamaz|

A ve B aynı kapı değildir: unpublished ama exact approved hedefle isolated implementation/test ayrıca açıkça yetkilendirilebilir.
Bugün B HELD; yeni scope/Phase1.5/runtime onayı yok. C veri incelemesi spec veya isolated test için başlangıç engeli değildir.
Canonical mevcut info.version2.0.0; contractVersion v1. Root2.1.0 ve önceki hashler historical conversation referansı;
erişilebilir artifact olmadan yeni pin doğrulaması yapılmış sayılmaz. Bu paket yeni contract candidate üretmez.
