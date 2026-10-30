---
status: proposal
kind: owner-decision-request
date: 2026-10-29
author: Treasury Intake Control Tower (K1 / gmg-10)
decision_owner: Beste (K2, bestepullukcu-alt) — RCS-001 / BL-565 sahibi (D44)
related: MOD-0289 AC-60, O-14; KARAR D59/1, D65/2
---

> **Öneri — karar değil.** Bu dosya karar sahibinin onayını ister; merge edilmesi kararı vermez. Cevap bu PR'da yorum ya da
> "Approve" ile verilir; Treasury CT cevabı kaynak cevap (Y-n) ve karar (D-n) olarak kaydeder, sonra pakete işler.
> Seçim: kabul / değişiklikle kabul / ret; ayrıca MOD-0289 CT-APR-01 onaylayıcılığı (D89) evet / hayır

# Treasury CT → Beste: O-14 önerisi (onayına)

Treasury CT → Beste: O-14 için bir öneri hazırladık, onaylarsan RCS-001 v0 sözleşmesine (3 Kasım) eklemeli olarak girebilir.

Sorun: grup önekli kayıtlarda (banka `BNK`, tedarikçi hesabı `BAC` — RCS-001 §5.3) `allocate` çağrısının `legalEntityId` alanına ne
gönderileceği v0'da yazılı değil (MOD-0289 AC-60, DL-1 kabul kapısı).

Önerimiz:
1. Gövdeye açık bir alan eklenir: `prefixScope` = `"LegalEntity"` (varsayılan) ya da `"Group"`.
2. `prefixScope = "Group"` iken `legalEntityId` **boş (null) olmalı**; servis öneki kiracının grup önekinden alır (bugün `GMG`, belge
   kodlarındaki `OrgPrefix` ile aynı değer); sayaç (kiracı, grup öneki, kayıt türü) başınadır.
3. `prefixScope = "LegalEntity"` iken `legalEntityId` zorunlu; servis o şirketin kısa kodunu kullanır (bugünkü davranış).
4. Uyumsuz istek (Group + legalEntityId dolu, ya da LegalEntity + legalEntityId boş) → 422 `RECORD_CODE_SCOPE_INVALID`; grup öneki
   kiracıda tanımlı değilse → 409 `GROUP_PREFIX_MISSING`. Böylece bir hata yüzünden boş `legalEntityId` sessizce grup koduna dönüşmez.
5. Şirket banka hesabında (`BAC`, şirket öneki) `legalEntityId` = hesabın sahibi şirket (D65/2 önerisi) — bunu da teyit eder misin?

Neden alan, neden "boş = grup" değil: boş değer bir hatadan da gelebilir; açık alan grup kodunu bilinçli bir seçim yapar ve ekleme
türünde bir değişikliktir (v0 kuralı: değişiklik yalnız ekleme).

Ayrıca: MOD-0289 ready-for-dev olduğunda CT-APR-01 onaylayıcısı olarak seni seçtik (K2) — kabul ediyor musun?
