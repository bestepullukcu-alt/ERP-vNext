# MOD-0191 — sahip kararları 04 (O-R5-1, O-R5-2)

- **Tarih:** 2026-11-25
- **Karar veren:** Beste (MOD-0191 sahibi ve onaylayıcısı), sohbette.
- **Bağlam:** MOD-0191 r5 (`e04f923f9`, LF sha `254a8322…`) bağımsız inceleme r4 (`docs/records/reviews/MOD-0191/2026-11-25-86cf97fe.md`)
  bulgularını kapattı ve iki açık soru bıraktı: O-R5-1 ve O-R5-2. Önceki kayıtlar: `mod-0191-owner-decisions-01.md` (K1, K2),
  `-02.md` (S-1…S-14), `-03.md` (S-8).

## Kararlar

| Soru | Karar | Sahibin sözü |
|---|---|---|
| **O-R5-1** — Politikaya yeni revision açılmışken (taslak, incelemede ya da reddedilmiş) daha önce onaylanmış bir miktarın etkinleştirilmesi, reddedilmesi ve gerekçeli değişikliği (override) kilitlenmesin mi; kayıt kendi onaylı politika sürümüne göre mi işlensin? | **Evet.** Kayıt üzerindeki komutlar kaydın kendi `PolicyRevisionId`'sine bağlanır; serinin güncel revision'ı kilit yaratmaz. Yeni normal kayıt oluşturma güncel ve onaylı revision'a bağlı kalır (r5 §23.7). | "evet, düzeltilebilsin. Her kayıt kendi onaylandığı politika sürümüne göre işlenir." |
| **O-R5-2** — SafetyStock servisindeki `[Authorize(Policy)]` yazımı (G-0191-03) standart `[HasPermission]`'a mı çekilsin, sapma olarak mı kabul edilsin? | **Standarda çekilir.** r2+ dilimlerinde yazılan yeni uçlar baştan `[HasPermission]` ile yazılır; r1'den kalan uçlar ayrı bir düzeltme işiyle çevrilir (takip etiketi FU-0191-PERM; MOD kimliği değil, gerekirse DCP-002 ön kontrolüyle alınır). G-0191-03 sapma olarak kabul edilmez; geçiş dönemi notu olarak kalır. | "standarda çekelim. Yeni uçlar baştan standart yazılır, eskiler ayrı bir düzeltmeyle çevrilir." |

## Sonuç

- MOD-0191'de açık sahip sorusu kalmaz. Paket bu kararlarla yeni revizyona (r6) geçer; ardından bağımsız inceleme (CT-APR-02) ve onay satırı
  (CT-APR-01, onaylayıcı Beste).
- MOD-0147'de aynı sorun 2026-11-25'te düzeltildi (yerel commit `5b855c67`, CT doğrulaması bekliyor); FU-0191-PERM aynı deseni izler.
- Bu kayıt kod, commit ya da dağıtım izni değildir.
