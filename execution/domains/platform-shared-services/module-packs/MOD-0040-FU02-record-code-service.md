---
id: MOD-0040-FU02
name: Record Code Service
domain: platform-shared-services
service: Diten.Platform
shell: none
golden_reference: none
entity_base: BaseEntity
status: draft
approval_status: pending
owner: Beste (platform-shared-services, K2)
branch: feature/pss/mod-0040-fu02-record-code-service
started: 2026-10-30
target: 2026-11-06
form_field_count: 0
parent: MOD-0040
parent_meaning: Blueprint 8.8 MOD-0040 Canonical ID & Correlation Standard — repodaki MOD-0040 / MOD-0040-FU01 takma adları değil
backlog: BL-565
rule: RCS-001
consumer_module: MOD-0126, MOD-0119, MOD-0120, MOD-0124, MOD-0289, MOD-0217, MOD-0178
contract_draft: docs/analysis/contracts/drafts/record-code-allocate-v1.0-proposal.md
revision: r12 — DL-2 paket revizyonu (ORG / POS süreç içi bağlanma); girdi sahip kararı A-8 = LegalEntity (Beste 2026-11-10, docs/records/decisions/2026-11/mod0040-fu02-owner-decisions-02.md @ 6609910c8) → K-15; DL-1 CT doğrulaması docs/records/audits/2026-11/mod-0040-fu02-dl1-ct-acceptance-2026-11-04.md; DL-1 metni (AC-1…AC-47, S-1…S-73) değişmedi, yalnız AC-9'a DL-2 notu; yeni AC-48…AC-61, S-74…S-87, açık sorular Q-1…Q-7; sözleşme RECORD-CODE-ALLOCATE 1.0.0 (FROZEN) değişmedi; künye draft — kod izni yok, yeni inceleme ve onay gerekir; r11 — r10 incelemeleri KOŞULLU (inceleme 03 tur 8, bulgu dosyası 2026-11-02-42b32c0a.md, özet 96476fa0; son bakış 01 tur 5, 2026-11-02-42b32c0a-son-bakis.md, özet fb4cf83e), aynı orta bulgu (AC-17 yarış ölçümü yürütücüyle kurulamaz); sahip kararı Beste 2026-11-02 "B — r11: yarış testi çıkarılır, ölçüm sıralı yapılır"; r11 = sıralı ölçüm + ilk kilit sınırı (Bölüm 8.6) + son bakış tur 5 F-2, F-3; yeni inceleme ve yeni onay gerekir; kapsam genişlemedi; r10 — r9 onaylı (approvals.tsv satırı 466aacfc9, r9 sha256 5fed433c); r10 = bağımsız son bakış 01 tur 4 düşük bulguları (bulgu dosyası 2026-11-02-5fed433c-son-bakis.md, özet 0e25482b, F-1…F-7), yeni onay gerekir; kapsam genişlemedi, yeni karar açılmadı; r9 — r8 sahip onayı (Beste, 2026-11-02, «onayla») + iki inceleme UYGUN (inceleme 03 tur 6, özet f9c26991; son bakış 01 tur 3, özet ec0346bb); r9 = ready-for-dev künyesi (pack_approval S-49: draft künyeli sürümün onay satırı kodu yetkilendirmez) + iki incelemenin düşük bulgularının kapanışı (tur 6 F-1, son bakış tur 3 F-1…F-7); kapsam genişlemedi, yeni karar açılmadı; r8 — sahip kararı Beste 2026-11-02 "düzelt, bir tur daha": bağımsız son bakış 01 tur 2 KOŞULLU (1 ORTA, 8 DÜŞÜK; hedef r7 pack sha256 fb151036, bulgu dosyası 2026-11-02-fb151036-son-bakis.md, özet 8a63c4e5) F-1…F-9 ve inceleme 03 tur 5 UYGUN (2 DÜŞÜK; 2026-11-02-fb151036.md, özet e6dd4cd8) F-1…F-2 işlendi; r7 — bağımsız son bakış 01 KOŞULLU (0 ENGEL, 3 ORTA, 9 DÜŞÜK; hedef r6 pack sha256 4b4c3db1, bulgu dosyası 2026-11-02-4b4c3db1-son-bakis.md, özet a52313ba) bulguları F-1…F-12 ve sahip kararı K-13 (F-3) ile inceleme 03 tur 4 UYGUN (2 DÜŞÜK; bulgu dosyası 2026-11-02-4b4c3db1.md, özet f4049860) bulguları F-1…F-2 işlendi; r6 — bağımsız inceleme 03 tur 3 KOŞULLU (0 ENGEL, 1 ORTA, 0 DÜŞÜK; hedef r5 pack sha256 bcd62d6c, bulgu dosyası 2026-11-02-bcd62d6c.md, özet ace6292b) bulgusu F-1 sahip kararı (a) ile işlendi (hizalama retleri denetime yazılır); r5 — bağımsız inceleme 03 tur 2 KOŞULLU (0 ENGEL, 1 ORTA, 4 DÜŞÜK; hedef r4 pack sha256 127ec6f2, bulgu dosyası 2026-11-02-127ec6f2.md, özet e2dbb0d8) bulguları F-1…F-5 ve CT kararı (F-1 yolu) işlendi; r4 — bağımsız inceleme 03 tur 1 KOŞULLU (0 ENGEL, 2 ORTA, 8 DÜŞÜK; hedef r3 pack sha256 010fe1c2, bulgu dosyası 2026-11-02-010fe1c2.md, özet 47c28da0) bulguları F-1…F-10 işlendi; r3 — bağımsız inceleme 02 KOŞULLU (0 ENGEL, 2 ORTA, 9 DÜŞÜK; hedef e49a67359, pack blob b18184f0, bulgu dosyası özeti 46aeb4a5) bulguları F-1…F-11 işlendi; r2 — inceleme 01 UYGUN DEĞİL (bulgu dosyası özeti 277fd253) bulguları ve sahip kararları K-8r, K-9…K-12 işlendi
code_start: yok — CT-APR-02 bağımsız inceleme kaydı ve CT-APR-01 onay satırı olmadan kod izni yoktur; DL-1 kodu r11 onayıyla yazıldı (CT kabulü 2026-11-04); DL-2 kodu r12'nin (ya da ready-for-dev künyeli ardılının) inceleme + onayını bekler (Q-6)
---

# MOD-0040-FU02 — Record Code Service (ortak kayıt kodu servisi, RCS-001)

> **Künye `draft` (r12) — bu sürüm onaysızdır ve hiçbir kod izni vermez.** `status: draft`, `approval_status: pending`. DL-1
> kodu r11'in onayıyla yazıldı ve CT tarafından doğrulandı (r11 sha256 `62f352de…cc2d`, `execution/approvals/approvals.tsv:61`);
> r12 DL-1 için yeni izin gerektirmez. DL-2 kodu için r12'nin (ya da künyesi `ready-for-dev` olan ardılının) inceleme kaydı ve
> onay satırı gerekir — `pack_approval` S-49: künyesi `draft` olan sürüme yazılan onay satırı kodu yetkilendirmez
> (`scripts/pack_approval.py:1473-1474`; Q-6).
> **Onaylanan sürüm**, `execution/approvals/approvals.tsv`'de bu paket yolu için yazılmış **son satırın** sha256'sına eşit olan
> sürümdür; bu dosyanın sha256'sı o satırla tutmuyorsa bu sürüm onaysızdır (r11, son bakış 01 tur 5 F-3). Bu paket, aynı sürümünün
> **CT-APR-02 bağımsız incelemesi** (`execution/approvals/reviews.tsv` + bulgu dosyası) ve **CT-APR-01 onay satırı**
> (`execution/approvals/approvals.tsv`, paketin bu sürümünün SHA-256'sına bağlı) main'e girmeden ve künye
> `ready-for-dev` olmadan hiçbir kod, yapılandırma, gizli bilgi ya da veri değişikliğini yetkilendirmez. Bir WP paketin
> tamamını alamaz; dilim tablosundaki (Bölüm 23) bir ya da birkaç dilimi alır. DL-2 bu sürümde **yazıldı ama kod izni
> taşımaz** (künye `draft`, açık sorular Q-1…Q-7); DL-3 ve sonrası yazılmadı — her biri kendi paket revizyonu ve onayıyla açılır.
>
> **Revizyon r12 (2026-11-10) — DL-2 paket revizyonu (ORG / POS bağlanması); künye `draft`, kod izni yok.** Girdi: sahip
> kararı **A-8 = LegalEntity** (Beste 2026-11-10; `docs/records/decisions/2026-11/mod0040-fu02-owner-decisions-02.md:8`) → **K-15**.
> Karar dosyası bu dalda ve `origin/main`'de **yok** (ölçüldü); yalnız commit `6609910c8`'de (dal `chore/ct/mod0178-dl1-accept-01`)
> vardır ve oradan okundu. DL-1 CT tarafından doğrulandı (`docs/records/audits/2026-11/mod-0040-fu02-dl1-ct-acceptance-2026-11-04.md:9`,
> VERIFIED; kayıt kodu testleri 263 / 263 `:17`). r12 DL-1 metnini (AC-1…AC-47, S-1…S-73) değiştirmez; yalnız AC-9'a DL-2 notu
> eklendi. r12 = DL-2'nin kod izni verilebilir hâle getirilmesi: süreç içi port (Bölüm 8.14), mevcut kodlar ve kod değişmezliği
> (Bölüm 8.15), ekran / API davranışı (Bölüm 11, 12), kabul ölçütleri **AC-48…AC-61**, sabotaj **S-74…S-87**, dokunulacak
> dosyalar ve XMC-001 satırları (Bölüm 5 "DL-2"), açık sorular **Q-1…Q-7** (Bölüm 24.1). Sözleşme RECORD-CODE-ALLOCATE 1.0.0
> (FROZEN, `docs/analysis/contracts/record-code-allocate.md`) **değişmez**: ORG / POS HTTP'de 403 kalır (sözleşme §6), çağrı süreç
> içidir. **Değişen bölümler:** künye; üst uyarı; 0 (ön kontrol r12 için yeniden koşuldu); 0.1 (K-15); 2.2; 2.4 (yeni); 3; 4.4;
> 5 ("DL-2" alt bölümü); 6; 7 ve 7.1; 8.14 ve 8.15 (yeni); 9; 11; 12; 13; 14; 15; 16 (AC-9 notu, AC-48…AC-61); 17; 18 ve 18.1; 19.4
> (yeni); 20; 21.4 (yeni); 22; 23 (DL-2 satırı); 24 (A-8 kapandı) ve 24.1 (yeni). Ölçüm commit'i `a9fd192ba` (bu dal); atıf
> yapılan Platform, Platform.Common, Web, sözleşme ve kural dosyaları `origin/main @ 084971b1e` ile aynıdır (`git diff --stat`,
> 2026-11-10). Çıkarımlar "çıkarım" diye işaretlidir; ölçülmeyenler "ölçülmedi" der.
>
> **Revizyon r11 (2026-11-02).** r10'un iki incelemesi **KOŞULLU** ve aynı orta bulguyu bağımsız verdi: inceleme 03 **tur 8**
> (`docs/records/reviews/MOD-0040-FU02/2026-11-02-42b32c0a.md`, özet `96476fa0…`; 1 ORTA) ve son bakış 01 **tur 5**
> (`…/2026-11-02-42b32c0a-son-bakis.md`, özet `fb4cf83e…`; 1 ORTA, 2 DÜŞÜK). Bulgu: AC-17'ye r10'da eklenen bariyerli yarış
> ölçümü `PlatformTransactionExecutor` ile kurulamaz. Sahip kararı (Beste, 2026-11-02): **"B — r11: yarış testi çıkarılır,
> ölçüm sıralı yapılır"**. r11 = AC-17 sıralı ölçüm (S-73 sıralı testte kırmızı) + ilk kilit yarışı Bölüm 8.6'da **kabul
> edilen sınır** + son bakış tur 5 F-2 (AC-44 iki evre) ve F-3 (onaylanan sürüm sürüm numarasından bağımsız). r10 onaylanmadı;
> **yeni inceleme ve yeni onay gerekir**. Kapsam genişlemedi; yeni karar yalnız sahip kararı "B". Kapanışlar Bölüm 19.3'te
> (`F-1@96476fa0`, `F-n@fb4cf83e`). Sözleşme taslağı rev 9 (değişmedi). Ölçümler `4288ae40e`'de.
>
> **Revizyon r10 (2026-11-02).** **r9 onaylı** (onay satırı `466aacfc9`, r9 sha256 `5fed433c…3e8b`; r9 commit `92beb1228`). r10 =
> bağımsız son bakış 01 **tur 4** (`docs/records/reviews/MOD-0040-FU02/2026-11-02-5fed433c-son-bakis.md`, özet `0e25482b…`; UYGUN,
> 7 DÜŞÜK) bulgularının kapanışı; **yeni inceleme ve yeni onay gerekir** (r9 onayı r10'u kapsamaz). Kapsam genişlemedi, yeni karar
> açılmadı. Kapanışlar Bölüm 19.3'te (`F-n@0e25482b`). Sözleşme taslağı rev 9. Ölçümler `466aacfc9`'da.
>
> **Revizyon r9 (2026-11-02).** r8 sahip tarafından onaylandı («onayla», Beste 2026-11-02) ve iki incelemeden UYGUN aldı
> (inceleme 03 tur 6, `docs/records/reviews/MOD-0040-FU02/2026-11-02-02db18ea.md`, özet `f9c26991…`, 1 DÜŞÜK; son bakış 01
> tur 3, `…/2026-11-02-02db18ea-son-bakis.md`, özet `ec0346bb…`, 7 DÜŞÜK). `pack_approval` S-49 uyarısı gereği onay satırı
> künyesi `ready-for-dev` olan sürüme yazılır: r9 = künye `ready-for-dev` + bu sekiz düşük bulgunun kapanışı (CT kararı: S-101,
> DL-1 başlamadan r9'da). Kapsam genişlemedi, yeni karar açılmadı. `approval_status: pending` — onay satırı r9'a yazılır.
> Kapanışlar Bölüm 19.3'te (`F-1@f9c26991`, `F-n@ec0346bb`). Sözleşme taslağı rev 8. Ölçümler `2265cce90`'da. CT kararı
> (MVP-1 CT, 2026-11-02): A-1, A-11, A-12, A-16, A-17 ve sözleşme dondurma ready-for-dev önkoşulu değil, DL-1 kabul / merge
> kapısıdır (Bölüm 18.1).
>
> **Revizyon r8 (2026-11-02).** Sahip kararı (Beste): "düzelt, bir tur daha". Hedef r7 pack sha256 `fb151036…f3f7`. İşlenenler:
> (1) **son bakış 01 tur 2** (`docs/records/reviews/MOD-0040-FU02/2026-11-02-fb151036-son-bakis.md`, özet `8a63c4e5…`;
> KOŞULLU, 1 ORTA, 8 DÜŞÜK) F-1…F-9; (2) **inceleme 03 tur 5** (`…/2026-11-02-fb151036.md`, özet `e6dd4cd8…`; UYGUN, 2 DÜŞÜK)
> F-1…F-2. Kapanışlar Bölüm 19.3'te (`F-n@8a63c4e5`, `F-n@e6dd4cd8`). Sözleşme taslağı rev 7. Ölçümler `cf0aea997`'de.
>
> **Revizyon r7 (2026-11-02).** İki girdi tek geçişte işlendi (hedef r6 pack sha256 `4b4c3db1…b541`): (1) **bağımsız son
> bakış 01** (`docs/records/reviews/MOD-0040-FU02/2026-11-02-4b4c3db1-son-bakis.md`, özet `a52313ba…`; KOŞULLU, 0 ENGEL,
> 3 ORTA, 9 DÜŞÜK) F-1…F-12 ve F-3 için sahip kararı **K-13** (Beste 2026-11-02, seçenek A — Bölüm 0.1); (2) **inceleme 03
> tur 4** (`…/2026-11-02-4b4c3db1.md`, özet `f4049860…`; UYGUN, 2 DÜŞÜK) F-1…F-2. Kapanışlar Bölüm 19.3'te (`F-n@a52313ba`,
> `F-n@f4049860`). Sözleşme taslağı rev 6. r7 ölçümleri `703cbaec7`'de yapıldı; atıf yapılan Platform, Common, MDM, Auth,
> Gateway, kural ve denetim defteri dosyaları `81864bd98` ile `703cbaec7` arasında değişmedi (diff --stat ile ölçüldü).
>
> **Revizyon r6 (2026-11-02).** Bağımsız inceleme 03, **tur 3** (hedef r5 pack sha256 `bcd62d6c…c071`, kaynak `98833e2b1`;
> bulgu dosyası `docs/records/reviews/MOD-0040-FU02/2026-11-02-bcd62d6c.md`, özet `ace6292b…`) sonucu **KOŞULLU** (0 ENGEL,
> 1 ORTA). F-1, sahip kararı (Beste, 2026-11-02) **seçenek (a)** ile işlendi: hizalama retleri AUD-001 §3'e göre ("Reddedilen
> komut da kaydedilir") paketin ret yazıcısıyla denetime yazılır (Bölüm 4, 21.3 Tablo D; AC-43). Kapanış Bölüm 19.3'te
> (`F-1@ace6292b`).
>
> **Revizyon r5 (2026-11-02).** Bağımsız inceleme 03, **tur 2** (hedef r4 pack sha256 `127ec6f2…95bd`, kaynak `5899d8e30`;
> bulgu dosyası `docs/records/reviews/MOD-0040-FU02/2026-11-02-127ec6f2.md`, özet `e2dbb0d8…`) sonucu **KOŞULLU** (0 ENGEL,
> 1 ORTA, 4 DÜŞÜK). Bulgular F-1…F-5 ve CT kararı (F-1: hizalama komutu işlem içi denetim kapısından, AUD-001 §4.4) tek
> yazar geçişinde işlendi; kapanış yerleri Bölüm 19.3'te (`F-n@e2dbb0d8`).
>
> **Revizyon r4 (2026-11-02).** Bağımsız inceleme 03, **tur 1** (hedef r3 pack sha256 `010fe1c2…8124`, kaynak `81864bd98`;
> bulgu dosyası `docs/records/reviews/MOD-0040-FU02/2026-11-02-010fe1c2.md`, özet `47c28da0…`) sonucu **KOŞULLU** (0 ENGEL,
> 2 ORTA, 8 DÜŞÜK). Bulgular F-1…F-10 (CT kabulü 2026-11-02) tek yazar geçişinde işlendi; kapanış yerleri Bölüm 19.3'te
> (`F-n@47c28da0`).
>
> **Revizyon r3 (2026-10-30).** Bağımsız inceleme 02 (hedef `e49a67359`, pack blob `b18184f0…`; bulgu dosyası
> `docs/records/reviews/MOD-0040-FU02/2026-10-30-b18184f0.md` @ `bbd30dd87`, özet `46aeb4a5…`) sonucu **KOŞULLU**
> (0 ENGEL, 2 ORTA, 9 DÜŞÜK). Bulgular F-1…F-11 (CT onaylı inceleyen önerileri) bu sürüme işlendi; kapanış yerleri
> Bölüm 19.2'de (`F-n@46aeb4a5`). İnceleme 02'nin notu: iki orta bulgu kapanmadan CT-APR-01 önerilmez; r3 doğrulama
> incelemesine (03) gider.
>
> **Revizyon r2 (2026-10-30).** Bağımsız inceleme 01 (hedef `74c62a076`, pack blob `219f30c9…`; bulgu dosyası özeti
> `277fd253…`) sonucu **UYGUN DEĞİL** (1 ENGEL, 4 ORTA, 7 DÜŞÜK). Bulgular F-1…F-12 ve sahip kararları K-8r, K-9…K-12 işlendi;
> kapanış yerleri Bölüm 19.1'de (`F-n@277fd253` — POLICY-REVIEW-START.md'ye göre `@` öneki önceki **bulgu dosyasının**
> özetidir; r2'de yanlışlıkla pack blob'u `219f30c9` yazılmıştı, r3'te düzeltildi). r1'deki "denetim aktörü Service"
> kararı geri alındı (K-8r): aktör isteği yapan kullanıcıdır.
>
> **Kimlik (sahip kararı Beste 2026-10-30).** `parent = Blueprint MOD-0040 (Canonical ID & Correlation Standard)`,
> **repodaki MOD-0040 / MOD-0040-FU01 takma adları değil.** `execution/registries/module-id-registry.md` tablosundaki
> `MOD-0040` satırı eski bir takma addır (Tenant Organization Foundation → MOD-0288) ve `MOD-0040-FU01` satırı da eski
> takma addır (→ MOD-0288-FU01); dosyanın "`MOD-0040` disambiguation (added 2026-08-09)" notu bu ayrımı yazar,
> "`MOD-0040` is reserved for Tenant Organization Foundation…" notu ise eski anlamı hâlâ "reserved" diye anar (A-12).
> Bu paket kayıt dosyasına **yazmaz**; önerilen satır Bölüm 0'dadır, satırı CT yazar.
>
> **Kaynak.** RCS-001 (`.antigravity/rules/record-coding-standard.md`, tamamı okundu), sahip kararları (Bölüm 0.1),
> Treasury v0 çağrı sözleşmesi (KARAR D59/1, Y-8 — `docs/records/decisions/2026-10/treasury-intake-owner-decisions-05.md:605-610`,
> `docs/records/audits/2026-10/treasury-intake-handover-review-01/SOURCE-ANSWERS-2026-10-27.md:80-85`). **Satır ölçüm
> commit'i `81864bd98`** (r4, inceleme 03 tur 1 F-10): satır numaraları ilk kez `origin/main @ 6d49f5823` üzerinde ölçüldü
> (2026-10-30); 2026-11-02'de `git diff --stat 6d49f5823 81864bd98` atıf yapılan servis, Gateway ve kural dosyalarında
> değişiklik göstermedi (yalnız kayıt dosyası değişti — onun notlarına artık metinle atıf yapılır), bu yüzden numaralar
> `81864bd98`'de geçerlidir. r4'te eklenen atıflar dosya yolu + sembol adıyla yazılır (S-99). Çıkarım olan yerler
> "çıkarım" ya da "CT önerisi" diye işaretlidir.

## 0. Kimlik ve ön kontrol (DCP-002)

| Adım | Sonuç (2026-10-30, `origin/main @ 6d49f5823`) |
|---|---|
| Blueprint araması | `Blueprint_Data` satırı **MOD-0040**: Module Name `Canonical ID & Correlation Standard`; Domain `1) Platform & Shared Services`; Suite `Integration & Interoperability`; Capability Group `Interface Governance`; Wave `W-2`; SoR `Canonical ID definitions, correlation rules (standard)`; Placement `Control-Plane Standard`; SoR Notes: "Historical project alias: MOD-0040 was once used for Tenant Organization Foundation. That legacy meaning is retired; canonical MOD-0040 remains Canonical ID & Correlation Standard." (Blueprint 8.8, sha256 `edbcb725…5939`) |
| Ön kontrol | `python3 -P -E -X utf8 .antigravity/scripts/verify_module_id.py . --check-id MOD-0040-FU02 --name "Record Code Service" --parent MOD-0040` → `OK  MOD-0040-FU02: proven against Blueprint/registry.` · **çıkış 0** (2026-10-30; **r10 için yeniden koşuldu** 2026-11-02, `466aacfc9`, Python 3.12.10 → aynı çıktı, çıkış 0 — son bakış 01 tur 4 F-7; **r11 için yeniden koşuldu** 2026-11-02, `4288ae40e`, Python 3.12.10 → aynı çıktı, çıkış 0; **r12 için yeniden koşuldu** 2026-11-10, `a9fd192ba`, Python 3.12.10 → aynı çıktı, çıkış 0 — DL-2 yeni kimlik açmaz) |
| Keşif | `… --discover "record code"` → `KEŞİF RAPORU v2`; ÖZET: blueprint 0 · sor 0 · registry 0 · paket 0 · gateway 0 · görev 3 · BİLGİ 0; **çıkış 3** ("inceleme gerekli"); rapor-sha256 `21e60a99332f19ae59b1bcb00b355b15f036302809ff4b3bb38dcf8cef617c49`; 447 ref incelendi |
| Keşif bulguları (3, görev) | `docs/roadmap/plans/mvp6-process-pilot-01/CT-QUEUE.tsv` Q234 (MOD-0183 form alanı), Q406 (kanıtsız paket uyarısı), Q503 (MOD-0186 DL-1) — üçü de "record" / "code" kelimelerinden eşleşti; kayıt kodu servisiyle ilgisi yok (karar: **mükerrer değil**, gerekçe: konu başka modül) |
| Blueprint ek araması | `Blueprint_Data` içinde `number range / numbering / document sequence / code generat / record code / business reference / identifier` deseni: yalnız MOD-0290 (`identifier` kelimesi, ürün kimliği ailesi — RCS-001 §4 KALSIN). Numaralandırma için ayrı Blueprint modülü yok |
| FU02 repoda geçiyor mu | `git grep -n "MOD-0040-FU02" origin/main` → **0 satır**; `git log --all --oneline -S "MOD-0040-FU02"` → **0 commit** (r1 öncesi). Araç farklı adla yeniden kullanımı yakalamaz; FU02 başka hiçbir dalda kullanılmamış |
| Ebeveyn kararı | Yeni modül değil; Blueprint MOD-0040'ın (Canonical ID definitions SoR) FU'su. Sahip kararı (Beste 2026-10-30) |

**Önerilen kayıt satırı (CT yazar; bu paket kayıt dosyasına dokunmaz):**

`| MOD-0040-FU02 | Record Code Service | record-code-service | Follow-up | ready-for-dev |  | MOD-0040 | platform-shared-services | Child of **Blueprint** MOD-0040 (Canonical ID & Correlation Standard, W-2) — NOT the repo alias row MOD-0040 (Tenant Organization Foundation → MOD-0288) nor MOD-0040-FU01 (→ MOD-0288-FU01). RCS-001 shared record-code service (BL-565). Pack: MOD-0040-FU02-record-code-service.md (ready-for-dev; approved revision = the pack sha256 in the last execution/approvals/approvals.tsv row for this path — the row is written with that revision at DL-1 merge, Bölüm 18.1 A-12). Owner decision Beste 2026-10-30. |`

> ⚠ **Kayıt çözümleme riski (ölçüldü):** kayıt dosyasında `MOD-0040` satırı `Deprecated Alias → MOD-0288`'dir ve
> "`MOD-0040` is reserved for Tenant Organization Foundation…" notu hâlâ eski anlamı anar. Ebeveyni kayıttan çözen bir
> araç ya da okuyucu FU02'yi yanlışlıkla MOD-0288'e bağlayabilir. CT satırı yazarken "`MOD-0040` disambiguation" notunu
> satıra da yazar ve "reserved" notunu düzeltir (A-12).

## 0.1 Sahip kararları (bağlayıcı)

| # | Karar (Beste 2026-10-30) | Pakette nerede |
|---|---|---|
| K-1 | **Yapı:** Diten.Platform içinde **ayrı** servis/özellik (`Features/RecordCodes`). Doküman kimlik üreticisi (`DocumentIdentifierAllocationService`) **genişletilmez**; doküman kodları KALSIN. Yalnız deseni örnek alınır | Bölüm 2, 5, 6, 19 |
| K-2 | **v1 kapsamı** (hedef: servis v1 **2026-11-06** main'de; kesin sözleşme **2026-11-03**): ayırma (allocate) iç ucu + atomik sayaç (`findOneAndUpdate` + `$inc`, RCS-001 §5.8) + önek kapsamları + RCS-001 §3'teki bütün **OTOMATİK** türlerin önek kaydı + şirket kısa kodu okuması + idempotency + denetim + kiracı izolasyonu. **v1 DIŞI** (sonraki dilimler, kod izni yok): mevcut modüllerin (Organizasyon Birimi, Pozisyon, Görev Türü, PPM, Abonelik Planı) bağlanması; İKİSİ DE türlerinde öneri + gerekçeli değişiklik + AUD-001 kaydı; Tüzel Kişilik kısa kodu önerisi; ekranlar. KALSIN türler ve sınıflandırılmamış mevcut üreticiler (Inventory, e-İmza, Procurement, SupplyChain, DMS) v1'de **değişmez** | Bölüm 2, 23 |
| K-3 | **Uç** (v0 Treasury'ye verildi; gövde ve hata kodlarında değişiklik **yalnız ekleme**): `POST /api/internal/record-codes/allocate`, servisten servise; kiracı gövdeden **değil**, güvenilir bağlamdan. Gövde `{ "recordType", "prefixScope", "legalEntityId", "idempotencyKey" }`. 200 `{ code, sequence }`; aynı anahtar + aynı gövde → aynı kod; farklı gövde → 400 `IDEMPOTENCY_KEY_REUSED`. Hatalar: 422 `RECORD_TYPE_UNKNOWN`; 422 `RECORD_CODE_SCOPE_INVALID`; 409 `LEGAL_ENTITY_SHORT_CODE_MISSING`; 409 `GROUP_PREFIX_MISSING`; başka kiracının şirketi 404; erişilemez 503 (fail-closed). Yanıt zarfı için bkz. A-17 (F-5) | Bölüm 3, 8, 12, 13, sözleşme taslağı |
| K-4 | **prefixScope**: `LegalEntity` (varsayılan; `legalEntityId` zorunlu; önek şirket kısa kodu) · `Group` (`legalEntityId` boş; önek kiracının grup öneki, RCS-001 §5.3) · `Platform` (`legalEntityId` boş; öneksiz). Tür başına izinli kapsamı **servis** belirler: BNK yalnız Group; BAC LegalEntity (şirket hesabı) + Group (tedarikçi hesabı); PAY, APD, ARI, CPS, CTP, AGR yalnız LegalEntity; PLN Platform. Sayaç (kiracı, önek, tür) başına (§5.1) | Bölüm 4.4, 8.4, 22 |
| K-5 | **Şirket kısa kodu (KARAR D91):** MOD-0220 `LegalEntity.Code` (`services/Diten.MdmService/src/Diten.MdmService.Domain/Entities/LegalEntity.cs:11`), `^[A-Z]{2,5}$`; MDM lookup-validation üzerinden (kiracı + aktif). Geçersiz → 409 `LEGAL_ENTITY_SHORT_CODE_MISSING`; MDM erişilemez → 503. Önek koda bir kez yazılır (§5.2). **Platform'da ayrı kısa kod tablosu YOK** | Bölüm 8.3 |
| K-6 | **Şirket öneki biçimi:** RCS-001 §1 bağlayıcı. K1 (`gmg-10`) "tek grup öneki" / "öneksiz"i değerlendiriyor; **2026-11-02**'ye kadar karar gelmezse sözleşme mevcut kurala göre dondurulur | Bölüm 24 (A-1) |
| K-7 | **MOD-0217 öneki `CTR` → `AGR`.** Procurement MOD-0144 bugün `CTR-` + 8 hex üretiyor (`services/Diten.ProcurementService/src/Diten.ProcurementService.Application/Features/Contract/Handlers/CommandHandlers/CreateContractHandler.cs:130-131`); aynı önek iki türde olamaz (§5.5). RCS-001 §3 satır değişikliği ayrı adımdır (`.antigravity`); bu paket yalnız `AGR`'yi kullanır. MOD-0144 / MOD-0217 kapsam çakışması açık soru | Bölüm 7, 22, 24 |
| ~~K-8~~ | ~~r1: aktör `Service`~~ — **K-8r ile değişti** | — |
| **K-8r** | **Aktör:** denetim kaydında aktör = isteği yapan **kullanıcı** (`TenantUser`, iletilen kullanıcı token'ının `sub`'ı) — `Service` değil. `CanonicalTransactionalAuditOutboxWriter` bunu bugün principal'dan zaten çıkarıyor (`services/Diten.Platform/src/Diten.Platform.Application/Features/Audit/CanonicalTransactionalAuditOutboxWriter.cs:156-185`). İç uç kimliği için mevcut Platform deseni ölçülür ve ona uyulur; yeni gizli bilgi/yapılandırma pakette yazılır, **değer uydurulmaz** | Bölüm 8.2, 14, 21 |
| **K-9** | **Kaynak servis:** denetim kaydının `SourceService` alanı = çağıran servisin adı (ör. `Diten.FinanceTreasuryService`). Yazıcıdaki sabit `SourceService = "Diten.Platform"` (`CanonicalTransactionalAuditOutboxWriter.cs:39`, kullanım `:138`) **eklemeli** genişletilir: `TransactionOwnedAuditIntent`'e isteğe bağlı kaynak servis alanı; boşsa bugünkü gibi `"Diten.Platform"` (diğer bütün kullanıcılar değişmez). **Güvenlik:** değer istek gövdesinden/başlığından **asla** alınmaz; yalnız doğrulanmış tüketici kimlik bilgisinden (kimlik bilgisi → servis adı eşlemesi, kapalı liste) gelir | Bölüm 8.2, 8.11, 21; AC-28 |
| **K-10** | **Ret kaydı:** reddedilen istekler denetime tek `Outcome` kuralıyla (Bölüm 21.2 başındaki cümle: yetkisizlik ya da tek gövdeli gizleme → `Denied`, diğer her ret → `Failed`) yazılır. Ret iş işlemi olmadığından kayıt **ayrı kısa bir işlemle** (ayrı oturum) yazılır; yazıcıya **yalnız ret için** eklemeli bir yol. Ret hiçbir zaman kod üretmez. Hangi retlerin yazıldığı tabloyla belirlenir (Bölüm 21.2). **Tek `Outcome` kuralı:** Bölüm 21.2 başındaki cümle (r7 F-4, r8 tek biçim). **AUD-001 §3'ten tek adlı sapma (r7, K-13; r8 kapsamı):** hız sınırlayıcının durdurduğu retler denetime yazılmaz — 429 `RECORD_CODE_RATE_LIMITED` ve sınırı aşan platform aktörü retleri (cevapları 403 kalır; inceleme 03 tur 5 F-2) — gerekçe: hız sınırı reddi saldırı ya da tüketici hatası anında denetim günlüğünü şişirir ve kiracının gerçek izini boğar; yerine **uyarı logu** (kiracı, kaynak servis, korelasyon) ve **sayaç / metrik** (`record_codes_rate_limited_total`, tek etiket kümesi `consumer_service`, `tenant`, `reason`; `reason ∈ {rate_limited, platform_actor_refusal}` (429 için `rate_limited`); `tenant` etiketi token kiracısıdır — platform aktörü reddinde `tenant_id` yoksa, Guid değilse ya da `Guid.Empty` ise değer **`platform`** (bölüm anahtarıyla aynı kural; r10, son bakış 01 tur 4 F-5); prometheus-net bir sayaç için tek etiket adı kümesi ister — r9, son bakış 01 tur 3 F-5; prometheus-net `Metrics.CreateCounter`, emsal `services/Diten.Platform/src/Diten.Platform.API/Observability/EventConsumeMetricsSink.cs`) tutulur. Sahip kararı Beste 2026-11-02 | Bölüm 8.2, 8.11, 21.2; AC-19, AC-29, AC-33 |
| **K-13** | **Ayırma tarafında retlerin yazımı (Beste 2026-11-02, son bakış 01 F-3, seçenek A):** 400 `RECORD_CODE_REQUEST_INVALID` denetime `Failed` yazılır; iç uca `platform_admin` / `partner_admin` token'ıyla gelen istek (AC-44) `Denied` yazılır (yetki reddi; kayıt platform denetim kiracısında; r8: yazım hız sınırlayıcıdan geçer, sınırı aşan retler 429 gibi yazılmaz — inceleme 03 tur 5 F-2); 429 yazılmaz — K-10'daki adlı sapma. Sınıflama koddan ölçüldü (Bölüm 21.2 başı) | Bölüm 8.5, 21.2; AC-19, AC-44 |
| **K-14** | **Hizalama retlerinin yazımı (Beste 2026-11-02, inceleme 03 tur 3 F-1, seçenek (a)):** 404 / 400 / 409 / 409 `Failed` yazılır (Bölüm 21.3 Tablo D) | Bölüm 4, 21.3; AC-43 |
| **K-15** | **A-8 kapandı — `ORG` / `POS` izinli kapsamı yalnız LegalEntity (Beste 2026-11-10; `docs/records/decisions/2026-11/mod0040-fu02-owner-decisions-02.md:8`, commit `6609910c8`).** ORG ve POS kodları şirket öneki alır: `{ŞİRKET}-ORG-000001`, `{ŞİRKET}-POS-000001`. Karar gerekçesi: `OrganizationUnit.LegalEntityId` zorunlu (`services/Diten.Platform/src/Diten.Platform.Domain/Entities/Organization/OrganizationUnit.cs:9`). POS'un şirket kaynağı (birim üzerinden) karar metninde adlandırılmaz — paket veri modelinden türetti (çıkarım; Bölüm 8.14, Q-5 teyit) | Bölüm 2.4, 4.4, 8.14, 22, 24 |
| **K-11** | **Platform kapsamı** (öneksiz kod, `prefixScope = Platform`): sözleşmede kalır, **DL-1'de uygulanmaz** — denetim boş `TenantId`'yi reddeder (`services/Diten.Platform/src/Diten.Platform.Application/Contracts/Audit/AuditOutboxWriteRequest.cs:28-30`) ve bugün tüketici yok. DL-1'de `prefixScope = Platform` → 422 `RECORD_CODE_SCOPE_INVALID` **(tür izni adımı geçtiyse;** değerlendirme sırasında tür izni kapsamdan önce geldiği için `PLN` + Platform 403 `RECORD_CODE_FORBIDDEN` döner — r3, inceleme 02 F-9), sözleşmede "DL-4'te açılır" notuyla. Uygulama DL-4'te, PLN bağlanmasıyla | Bölüm 8.5, 8.7; AC-8, AC-25 (DL-4) |
| **K-12** | **Önceki öneriler kabul:** (a) `AGR` kalır — doküman tür kodundaki `AGR` ile biçimler farklı, çakışma yok (Bölüm 22); (b) tür kaydı **kodda** (`RecordCodeTypeCatalog`); (c) grup öneki **kiracı ayarı**, bugün `GMG`, belge `OrgPrefix` ile aynı değer — r3'te (inceleme 02 F-7) değerin **tek kaynağı belge `OrgPrefix`'idir**; kiracı ayarı onun kilitlenen bir kopyasıdır, ayrı düzenlenebilir değer yoktur (Bölüm 4.3 / 8.12); (d) `PLN` katalogda, bağlanması DL-4; (e) kimlik deseni **P2** = kimlik bilgisi + iletilen kullanıcı token'ı, F-4 azaltıcılarıyla (Bölüm 8.2) | Bölüm 4, 8, 22 |
| A-3 sahibi | `.antigravity` kural dosyaları **Ali**'nin sorumluluğunda (Beste notu): RCS-001 §3 `AGR` satırı ve izinli kapsam sütunu için sahip "Ali (`.antigravity`) + Treasury PR'ı" | Bölüm 24 (A-3) |
| RCS | 6 hane alt sınır, `999999`'dan sonra 7 hane (§5.7); boşluk kabul, numara geri verilmez, arşivli kod tekrar kullanılmaz (§5.8); yalnız ASCII `A–Z`, `0–9`, `-` (§5.10); içe aktarmada sayaç en büyük numaranın üstünden (§5.11) — **v1'de içe aktarma ucu yok**, kural DL-6 | Bölüm 8, 16, 23 |

## 1. Module Summary

**Amaç.** RCS-001 §5.12'nin "tek ortak kod üretme servisi": kullanıcıya görünen iş kodunu (`{ŞİRKET}-{TÜR}-{SAYAÇ}`,
ör. `MYG-PAY-000042`) tek yerden, atomik, kiracı izole, idempotent ve denetimli olarak verir. Bugün ortak üretici yok;
dört ayrı sayaç var (CRM hesap, CRM kampanya, MDM ürün, Doküman — RCS-001 §5.12) ve Treasury (6 paket) ile WMS
(MOD-0178) beşinci/altıncı bir kopya yazmamak için bu servisi bekliyor (D59/1; MOD-0178 Q-07 B, FU-0178-RCS-01).

**Kullanıcı.** Servisler, bir kullanıcı adına: v1'de Diten.FinanceTreasuryService (PAY, APD, ARI, CPS, CTP) ve
Diten.MdmService (BNK, BAC, AGR). Ayırma her zaman bir kullanıcının başlattığı Create'in içinde olur; çağıran servis
kullanıcının token'ını iletir; denetimde aktör o kullanıcıdır, kaynak servis çağıran servistir (K-8r, K-9).
Diten.InventoryService (WMS) DL-8 ile bağlanır.

**v1 kapasite özeti (DL-1).**

1. `POST /api/internal/record-codes/allocate` — tek iç uç, Gateway'e açılmaz.
2. Atomik sayaç `(kiracı, önek, tür)` başına; hiç sıfırlanmaz; 6 hane alt sınır, sonra 7+ hane.
3. İki etkin önek kapsamı: `LegalEntity` (MDM `Code`), `Group` (kiracının grup öneki ayarı). `Platform` sözleşmede var,
   DL-1'de tür izni geçtiyse 422, izinli çağıranı olmayan türde (`PLN`) 403 (K-11).
4. Tür önek kaydı: kod kataloğu (RCS-001 §3 OTOMATİK türler + `AGR` + `PLN`), tür başına izinli kapsam, izinli
   çağıran ve gerekli kullanıcı izni; §3'e testle sabitlenir.
5. İdempotency: `(kiracı, çağıran servis, idempotencyKey)` başına tek kod; gövde alanlarının ayrı ayrı eşitliğiyle (Bölüm 4.2).
6. Denetim: her yeni ayırma, ayırma defteriyle **aynı işlemde** merkezi denetim kutusuna yazılır (aktör kullanıcı, kaynak
   servis çağıran); kiracısı ya da kişisi bilinen her ret ayrı kısa bir işlemle `Denied` / `Failed` yazılır — **tek adlı istisna**
   hız sınırlayıcının durdurduğu retler — 429 `RECORD_CODE_RATE_LIMITED` ve sınırı aşan platform aktörü retleri (K-10 / K-13; uyarı logu + sayaç) (r8, son bakış 01 tur 2 F-2).
7. Kiracı izolasyonu: kiracı doğrulanmış kullanıcı token'ının `tenant_id`'si; bütün okuma/yazmalar **tam eşitlik**
   `TenantId = token kiracısı`; askıdaki kiracıya kod verilmez.
8. Kimlik bilgisi sızmasına karşı: tüketici başına izinli tür kümesi, kullanıcıda modülün oluşturma izni, hız sınırı,
   sır döndürme / iptal.

## 2. Ownership and Boundaries

### 2.1 In scope (DL-1 = v1)

- Ayırma iç ucu, istek yürütücüsü (kimlik + kiracı + izin değerlendirmesi), istek ayrıştırıcısı, komut, işleyici — FluentValidation
  doğrulayıcısı **yok** (r7 F-1, Bölüm 8.5 (d); r8 kalıntı düzeltmesi).
- Üç kiracı kapsamlı koleksiyon (`TenantScopedEntity`): sayaç `record_code_counters`, ayırma defteri
  `record_code_allocations`, grup öneki ayarı `record_code_group_prefixes` (Bölüm 4).
- Tür önek kaydı (kod kataloğu `RecordCodeTypeCatalog`) ve onu RCS-001 §3'e sabitleyen test.
- MDM'den şirket kısa kodu okuyan yeni okuyucu (`MdmLegalEntityShortCodeReader`); mevcut
  `MdmLegalEntityReferenceValidator` **değişmez** (MOD-0288'in).
- Denetim yazıcısının **eklemeli** genişletilmesi (K-9 kaynak servis, K-10 ret yolu) — Platform domain'i içinde,
  r7 (son bakış 01 F-5) ile düzeltildi: bu bir **paylaşılan bileşen** değişikliğidir ve **XMC-001 onayı olarak** kayda
  geçer — "XMC-001 gerekmez" yazımı yanlıştı. Değişen paylaşılan bileşenler ve onayları: `CanonicalTransactionalAuditOutboxWriter`
  + `TransactionOwnedAuditIntent` + yeni `ITransactionalAuditRefusalWriter` (MOD-0021 denetim altyapısı; onay sahip kararları
  **K-9, K-10**, Beste — Platform domain sahibi ve bu bileşenin sahibi); `TransactionOwnedAuditCommands.Authorized`'a tek ad
  (onay **CT kararı 2026-11-02**, AUD-001 §4.4); `LoginOnlyEndpointGuardTests.AnonymousInternalControllers`'a tek ad (Platform
  güvenlik koruması; onay **CT, dosyaya dokunulmadan önce**: CT-APR-01 onay satırı bu tek satırı paketin bu sürümüyle birlikte adıyla kapsar ve DL-1'in ilk commit grubu ancak ondan sonra yazılır — r8, son bakış 01 tur 2 F-8; listenin kendi koşulu, yani her ret dalının adlı testi, ilk grupta kanıtlanır, Bölüm 16 AC-41). Etkilenen tüketiciler (değişmeden
  yeşil kalmaları AC-27 / AC-28'in ölçüsü): yazıcının bugünkü bütün çağıranları — modül hakkı (`PhysicalEntitlementAuditIntent`),
  kiracı aboneliği (`TenantSubscriptionTransactionWriter`, `TenantSubscriptionCommandSupport`), abonelik planı ve modül
  kataloğu / modül kaydı (`IGlobalApplicabilityTransactionCoordinator`), e-imza (`ESignatureAuditIntent`) — ve
  `TransactionOwnedAuditCommandGuardTests`, `LoginOnlyEndpointGuardTests` (Bölüm 5, 21).
- Grup öneki ayarının yazma yolu: Platform yöneticisi için UI'sız, gövdesiz bir **hizalama** ucu — kiracı ayarına etkin
  belge `OrgPrefix`'ini kopyalar; kullanılana kadar yeniden hizalanabilir, kullanıldıktan sonra kilitli (r3, F-7).
- Hız sınırı: yürütücü içinde, (kimlik bilgisi, kiracı) bölümlü; değeri A-16 (r3, F-5).
- Sözleşme taslağı `docs/analysis/contracts/drafts/record-code-allocate-v1.0-proposal.md`; dondurma ayrı CT adımı
  (2026-11-03).

### 2.2 Out of scope (v1 dışı — kod izni yok)

- **Platform kapsamı** (öneksiz kod, `PLN`) — DL-4 (K-11).
- **Mevcut modüllerin bağlanması** (ORG, POS → DL-2; TTY, PPM `PFL/PRG/PRJ/INI/INV`, PLN → DL-4). Bu modüller v1'de
  bugünkü elle kod davranışını korur. **r12:** DL-2'nin kapsamı Bölüm 2.4'te yazıldı; kod izni r12'nin (ya da ardılının)
  inceleme + onayı ve künyenin `ready-for-dev` olmasıyla açılır.
- **İKİSİ DE akışı** (öneri + gerekçeli değişiklik + AUD-001 eski/yeni kod + gerekçe) → DL-3.
- **Tüzel Kişilik kısa kodu önerisi** ve RCS-001 §5.4 kullanım kilidi (MDM tarafı) → DL-5.
- **İçe aktarma / sayaç yüksek su işareti** (§5.11), WMS geçici sayacından devam → DL-6.
- **Ekranlar** → DL-10. v1'de hiçbir `.cshtml`, rota, menü yok.
- **Kodun kayda bağlanması / tüketilmesi** (MDM `CodeReservation`'ın "tüket" adımı, §5.8): v1 yalnız **ayırır**;
  kullanılmayan kod bir boşluktur (§5.8 kabul eder) — FU-RCS-04.
- **KALSIN türler** (CRM `ACC`/`CMP`, MDM ürün ailesi `GP/GS/LS/FG/IT`, doküman `UID` ve `GMG-QMS-…`): kod verilmez
  (422 `RECORD_TYPE_UNKNOWN`).
- **Sınıflandırılmamış mevcut üreticiler** (Inventory, e-İmza, Procurement `CTR-` + 8 hex, SupplyChain, DMS): değişmez (A-4).
- Arka plan işlerinden (kullanıcı token'ı olmayan) çağrı → DL-9.

### 2.3 Ownership decision

- Sahip: **Beste** (platform-shared-services, K2). Platform kodu bu domain'in
  (`execution/domains/platform-shared-services/domain-config.md` "Domain-Level Repo Scope": `services/Diten.Platform/**`).
- SoR: Blueprint MOD-0040 "Canonical ID definitions". Kayıt türünün kendisi (ödeme partisi, banka…) tüketicinin SoR'udur;
  bu servis kodu verir, kaydı tutmaz.
- Denetim yazıcısı (MOD-0021 altyapısı) Platform domain'indedir; değişiklik yalnız eklemelidir, sahibi Beste.
- MDM `LegalEntity.Code` MOD-0220'nindir; bu servis yalnız okur.
- Domain-config In-Scope listesinde MOD-0040-FU02 yok (ölçüldü); ekleme CT adımı (A-12).

### 2.4 In scope (DL-2 — r12; kod izni yok, Bölüm 18.1 DL-2 kapıları)

- **Tür kataloğu:** `ORG` / `POS` satırlarında izinli kapsam `LegalEntity` (K-15), izinli çağıran `Diten.Platform` (yalnız
  süreç içi port), gerekli izin `platform.organization-units.create` / `platform.positions.create` (Bölüm 4.4). HTTP kimlik
  bilgisi eşlemesi (`TrustedRecordCodeCredentialAuthenticator.Consumers`, `services/Diten.Platform/src/Diten.Platform.API/Security/TrustedRecordCodeCredentialAuthenticator.cs:20-21`)
  **değişmez** — `ORG` / `POS` hiçbir bağlamada yoktur, iç uçta 403 kalır (sözleşme 1.0.0 §6; AC-49).
- **Süreç içi port** `IInProcessRecordCodeAllocator` (Application) ve uygulaması `InProcessRecordCodeAllocator` — Bölüm 8.14.
  DL-1 ayırma işleyicisi (`AllocateRecordCodeHandler`) **değişmez**; port onu çağırır.
- **MOD-0288 bağlanması (XMC-001; Bölüm 5 "DL-2", Q-7):** Organizasyon Birimi ve Pozisyon oluşturma işleyicileri kodu porttan
  alır; oluşturma isteğinde kod kabul edilmez; güncelleme kodu değiştirmez; formlarda kod salt okunur (Bölüm 8.15, 11, 12).
- **Mevcut (elle girilmiş) kodlar:** RCS-001 §2 / §7 — korunur (Q-1 önerisi A); sayaç çakışma savunması (Bölüm 8.15).
- **DL-2 dışında:** içe aktarma / sayaç yüksek su işareti (DL-6); İKİSİ DE akışı (DL-3); toplu kod göçü (Q-1 B seçilirse ayrı
  revizyon); geliştirme tohumu `PositionSeed` ve kullanılmayan `_CreateEditOffcanvas.cshtml` parçaları (FU-RCS-18); MDM v1.1
  onay denetimi (A-19, Q-4); merkezi oluşturma denetiminin zenginleştirilmesi (FU-RCS-17).

## 3. Owned Objects

| Tür | Ad | Not |
|---|---|---|
| Entity | `RecordCodeCounter` | `TenantScopedEntity`; Bölüm 4.1 |
| Entity | `RecordCodeAllocation` | `TenantScopedEntity`; ekleme-yalnız defter; Bölüm 4.2 |
| Entity | `RecordCodeGroupPrefix` | `TenantScopedEntity`; kiracı başına tek satır; belge `OrgPrefix`'inin kilitlenen kopyası; Bölüm 4.3 |
| Kod kataloğu | `RecordCodeTypeCatalog` | Domain, değişmez; Bölüm 4.4 |
| Değer | `RecordCodeFormat` | biçimleme + doğrulama |
| Enum | `RecordCodePrefixScope` | `LegalEntity = 1`, `Group = 2`, `Platform = 3` (JSON'da ad) |
| Repository | `IRecordCodeCounterRepository` | `NextAsync(tenantId, prefix, recordType)` |
| Repository | `IRecordCodeAllocationRepository` | `InsertAsync(session, tenantId, …)` (idempotency index'inde E11000 → `RecordCodeIdempotencyConflictException`, Bölüm 8.6), `FindByIdempotencyKeyAsync(tenantId, caller, key)` (oturumlu ve oturumsuz), `FindLatestPrefixForLegalEntityAsync(tenantId, legalEntityId)`, `ExistsPrefixForOtherLegalEntityAsync(tenantId, prefix, legalEntityId)` — güncelleme / silme / değiştirme yok (r4: `FindByIdAsync` kaldırıldı — inceleme 03 tur 1 F-3) |
| İstisnalar (Domain, `RecordCodeExceptions.cs`) | `RecordCodeIdempotencyConflictException`, `RecordCodeGroupPrefixChangedException`, `RecordCodeAuditOutboxInvariantException`, `RecordCodeTenantPredicateViolationException` | hepsi `Exception`'dan türer, `MongoException` ya da `InvalidOperationException` **değildir**: yürütücü iptal edip olduğu gibi yeniden atar; ilk üçünü işleyici yakalar (Bölüm 8.6); dördüncüsü programlama hatasıdır → 500, iç cümle gönderilmez (r4 F-3; r7, son bakış 01 F-9 ve ölçülen `GlobalExceptionHandler` eşlemesi) |
| Repository | `IRecordCodeGroupPrefixRepository` | `GetAsync(tenantId)` ve `GetAsync(session, tenantId)` (oturumlu: hizalamanın önceki değeri, r5 F-1), `AlignIfUnlockedAsync(session, tenantId, orgPrefix, …)`, `LockAsync(session, tenantId, prefix)` — `UpdateOne` filtresi `TenantId == tenantId && GroupPrefix == prefix`, güncelleme `LockedAtUtc`'yi yalnız boşsa yazar (boru hattı güncellemesi `$ifNull`); **"eşleşmez" = `MatchedCount == 0`**: ayar adım 11'den sonra başka bir önekle yeniden hizalanmıştır (ayar silinmez, yokluk olamaz); zaten kilitli ve aynı önekli satır eşleşir (değişiklik yok) — r9, son bakış 01 tur 3 F-1. **Dönüş (r10, son bakış 01 tur 4 F-4):** `LockResult(bool Matched, bool LockedNow)` — `Matched = MatchedCount == 1`, `LockedNow = ModifiedCount == 1` (kilidi bu işlem yazdı; zaten kilitli satırda `$ifNull` aynı değeri bırakır, MongoDB değişmeyen belgeyi `ModifiedCount`'a saymaz → `false` — **çıkarım** (sunucu davranışı, bu repoda ölçülmedi) **— DL-1 testi ölçer:** AC-17 sıralı ölçümünde ikinci ayırmanın `LockedNow`'ı `false`; r11, son bakış 01 tur 5 F-1); `Matched == false` → `RecordCodeGroupPrefixChangedException` — silme yok |
| Port (Application) | `ILegalEntityShortCodeReader` | sonuç: `Found(code, approvalStatus?)` · `NotFound` · `Unavailable(reason)` |
| Komut | `AllocateRecordCodeCommand` | yazma komutu; `IAuditableCommand` değil (denetim işleyicide, Bölüm 21) |
| Komut | `AlignRecordCodeGroupPrefixCommand(Guid TenantId, bool BodyPresent)` | gövdesiz (`BodyPresent` controller'ın ölçtüğü "istekte gövde var" bayrağıdır, gövde içeriği komuta girmez; ret işleyicide verilir ki denetime yazılsın — r6 F-1); kiracı ayarını etkin belge `OrgPrefix`'ine hizalar; **`ITransactionOwnedAuditCommand`** (r5, inceleme 03 tur 2 F-1, CT kararı 2026-11-02) — `IAuditableCommand` / `IAuditMetadataProvider` **değil**; `TransactionOwnedAuditCommands.Authorized`'a adıyla eklenir; denetim işleyicide, aynı işlemde, `RecordCodeAuditIntent.EnqueueGroupPrefixAlignedAsync` ile (Bölüm 4, 21.1, 21.3) |
| Sorgu | `GetRecordCodeGroupPrefixQuery` | yönetici ucu `GET` |
| DTO | `RecordCodeAllocationResult` (`Code`, `Sequence`) | `RecordCodesModels.cs` |
| API (iç) | `POST /api/internal/record-codes/allocate` | `InternalRecordCodesController`; Gateway'e açılmaz |
| API (yönetici) | `PUT` (gövdesiz hizalama) / `GET /api/admin/tenants/{tenantId:guid}/record-code-group-prefix` | `TenantRecordCodeGroupPrefixController`; `[Authorize(Policy = "PlatformAdminOnly")]` (r4 F-4); mevcut `/api/admin/tenants/{everything}` rotası |
| Yapılandırma | `TrustedRecordCodeCredential` | tüketici başına kimlik bilgisi bağlaması; değer depoda yok |
| Hız sınırlayıcı | `RecordCodeAllocateRateLimiter` | yürütücü içi `PartitionedRateLimiter`, bölüm (tüketici servis, kiracı); ASP.NET `AddRateLimiter` / `OnRejected` kullanılmaz (r3, F-5); değer A-16 |
| Denetim yazıcısı (eklemeli) | `TransactionOwnedAuditIntent.SourceService` (isteğe bağlı), `TransactionOwnedAuditSourceServices` (izinli ad kümesi), `ITransactionalAuditRefusalWriter` (yeni arayüz, `CanonicalTransactionalAuditOutboxWriter` uygular) | K-9, K-10; r3 F-3, F-4 |
| Denetim çağrısı | `RecordCodeAuditIntent.EnqueueAsync` / `.EnqueueRefusalAsync` / `.EnqueueGroupPrefixAlignedAsync` (r5 F-1) / `.EnqueueGroupPrefixAlignRefusalAsync` (r6 F-1, ret yazıcısıyla) | yeni belirteçler; CT defter kararı (A-10) |
| İzin | `platform.record-code-group-prefixes.read`, `platform.record-code-group-prefixes.update` | PKS-001 Tier 1; yalnız `platform_admin` (`partner_admin` 403, r4 F-4) |
| Frontend rota | — | yok (`shell: none`) |
| Port (Application, **DL-2**) | `IInProcessRecordCodeAllocator` | `Task<InProcessRecordCodeResult> AllocateAsync(InProcessRecordCodeRequest request, CancellationToken ct)`; Bölüm 8.14 |
| Değer (**DL-2**) | `InProcessRecordCodeRequest(string RecordType, Guid LegalEntityId, Guid RecordId)` · `InProcessRecordCodeResult(bool IsAllocated, string? Code, long? Sequence, int StatusCode, string? ReasonCode)` | aynı dosyada (`Contracts/RecordCodes/IInProcessRecordCodeAllocator.cs`) |
| Uygulama (**DL-2**) | `InProcessRecordCodeAllocator` | `AllocateRecordCodeCommand`'ı `ISender` ile gönderir; DL-1 işleyicisinin adım 4–13'ü ve ret kaydı aynen koşar; kendi kural kopyası yok |
| Neden kodları (**DL-2**) | `RECORD_CODE_SYSTEM_ASSIGNED` (400), `RECORD_CODE_IMMUTABLE` (409), `RECORD_CODE_COLLISION` (409) | ORG / POS uçlarının cevap kodları (`RecordCodeReasonCodes.cs`); RECORD-CODE-ALLOCATE sözleşmesine **girmez** — iç uç bunları üretmez |
| Depo yöntemi (**DL-2**, MOD-0288 deposu, XMC) | `IOrganizationUnitRepository.ExistsByCodeIncludingDeletedAsync`, `IPositionRepository.ExistsByCodeIncludingDeletedAsync` | silinmiş satırlar dahil kod araması (RCS-001 §5.8; Bölüm 8.15) |

## 4. Entity Fields

Ortak taban `BaseEntity` (`services/Diten.Platform.Common/src/Diten.Platform.Common/Persistence/BaseEntity.cs:3-14`) ve
**`TenantScopedEntity`** (`EntityVariations.cs:8-11`, `required Guid TenantId`). r1'deki `HybridEntity` kaldırıldı
(inceleme 01 F-2): Platform kapsamı DL-1'de yok (K-11), kiracısız satır da yoktur; `HybridRepository`'nin "`TenantId` null
**veya** mevcut kiracı" filtresi (`services/Diten.Platform.Common/src/Diten.Platform.Common/Persistence/Repositories.cs:96-109`)
bu özellikte kullanılmaz. DL-4 kiracısız satırın tabanını ayrıca kararlaştırır.

**Kiracı yüklemi kuralı (inceleme 01 F-2/F-3; r3 inceleme 02 F-1 ile düzeltildi).** Üç deponun **her** okuma ve yazma
yöntemi `Guid tenantId`'yi açık parametre olarak alır ve filtre **tam eşitliktir** `TenantId == tenantId`. Depo şu üç
durumda `RecordCodeTenantPredicateViolationException` atar (r7: `InvalidOperationException` değil — ölçülen eşlemede 400 ve iç cümle olurdu, Bölüm 8.6) ve hiçbir şey okumaz/yazmaz: (a) `tenantId == Guid.Empty`; (b)
`ITenantContext.IsPlatformContext == true` (`services/Diten.Platform.Common/src/Diten.Platform.Common/Tenancy/ITenantContext.cs:10`,
`SetPlatformContext` ile kurulan bağlam); (c) `tenantId ≠ ITenantContext.TenantId`. **Platform sistem kiracısı
`00000000-0000-0000-0000-000000000001` reddedilmez.** Bu kimlik bir iş kiracısı **değildir** (r4, inceleme 03 tur 1 F-10 ile
düzeltildi): `SystemTenantRules.PlatformSystemTenantId` (`services/Diten.Platform/src/Diten.Platform.Domain/Entities/SystemTenantRules.cs`)
tohumlanmış **Internal** platform sistem kiracısıdır (Code `PLATFORM`, ad "Platform Admin Tenant", `Status = Active`,
`TenantType = Internal` — `…/Infrastructure/Persistence/Configurations/TenantSeed.cs` `EnsureSeededAsync`), platform
yöneticilerinin ev kiracısıdır; AuthService'te aynı kimlik `PlatformSystemTenant.Id`
(`services/Diten.AuthService/src/Diten.AuthService.Domain/Authorization/PlatformSystemTenant.cs`) ve geliştirme ortamının
`DevBypassTenantId`'sidir (`…/Diten.AuthService.Api/appsettings.Development.json`); Platform'un yalnız açıkça açılan
geliştirme tohumu `PositionSeed.DefaultTenantId` olarak kullanır. Kiracı kaydı Active olduğundan ve `tenant_user` token'ı
taşıyabildiğinden bu servis onu reddetmez; o kiracının `tenant_user` token'ıyla gelen istek kiracı kapsamlı her istek
gibi kod alır (AC-38; inceleme 02 F-1 kararı değişmedi, yalnız gerekçe düzeltildi). Platform yöneticisi token'ı iç uçta
403 alır (`actor_type ≠ tenant_user`, Bölüm 8.2; AC-44). Kiracı değeri işleyiciye yürütücüden (token) gelir; komut
DTO'sunda kiracı alanı yoktur.

**Yönetici ucu — rota kiracısında okuma/yazma (r4, inceleme 03 tur 1 F-1).** `/api/admin/*` isteğinde
`TenantResolutionMiddleware` (`services/Diten.Platform.Common/src/Diten.Platform.Common/Tenancy/TenantResolutionMiddleware.cs`,
`IsAdminPath` dalı) `tenantContext.SetPlatformContext(Guid.Empty)` çağırır; bu bağlamda yukarıdaki (b) kuralı depoyu
reddeder. Bu yüzden hizalama (`AlignRecordCodeGroupPrefixHandler`) ve okuma (`GetRecordCodeGroupPrefixHandler`) işleyicileri
şu sırayı izler:

1. Platform bağlamında rota kiracısını doğrular: `ITenantRegistryRepository.GetByIdAsync(Guid id, …)`
   (`services/Diten.Platform/src/Diten.Platform.Domain/Repositories/ITenantRegistryRepository.cs`); kayıt yok ya da
   `IsDeleted` → 404, depo çağrılmaz. `Guid.Empty` rota değeri `{tenantId:guid}` kısıtından geçse de 404'tür.
2. Doğrulanan kiracı için `using (TenantScope.Begin(_tenantContext, routeTenantId))`
   (`services/Diten.Platform/src/Diten.Platform.Application/Contracts/TenantScope.cs`, `Begin`) açar — bağlam
   `SetTenant(routeTenantId)` ile kiracı bağlamına geçer (`IsPlatformContext = false`, `TenantId = routeTenantId`);
   `IRecordCodeGroupPrefixRepository` çağrılarının hepsi bu bloğun içindedir ve `tenantId = routeTenantId` verir. Blok
   kapanınca `TenantScope` anlık görüntüsü platform bağlamını (`SetPlatformContext(Guid.Empty)`) geri yükler
   (`TenantScope.TenantSnapshot.RestoreTo`). `TenantScope.BeginPlatform` **kullanılmaz** (bağlamı platform bırakır).
3. **Denetim işlem içinde (r5, inceleme 03 tur 2 F-1; CT kararı 2026-11-02).** r4'teki merkezi yol (`AuditBehavior`)
   kaldırıldı: `AuditBehavior.BuildAuditPlan` komutun `GetAuditMetadata()`'sını işleyiciden **önce** okur
   (`…/Application/Contracts/Behaviors/AuditBehavior.cs`), komut gövdesizdir ve önceki değer yalnız işleyicide, `TenantScope`
   içinde okunabilir — merkezi yol önceki / sonraki değeri üretemez. Bu yüzden `AlignRecordCodeGroupPrefixCommand`
   `ITransactionOwnedAuditCommand`'dır (`IAuditableCommand` değil); `AuditBehavior` onu yalnız
   `TransactionOwnedAuditCommands.Authorized` listesinde adı varsa geçirir (yoksa işleyici koşmadan "Transaction-owned audit
   is not authorized" istisnası). İşleyici 2. adımdaki blok içinde tek bir `IPlatformTransactionExecutor.ExecuteAsync`
   gövdesinde: (a) `GetAsync(session, routeTenantId)` ile **önceki** `GroupPrefix` ve `LockedAtUtc`'yi okur; (b) etkin
   `OrgPrefix`'i doğrular, kilit kuralını uygular, `AlignIfUnlockedAsync(session, …)` ile yazar (değişiklik yoksa yazmaz);
   (c) aynı oturumla `RecordCodeAuditIntent.EnqueueGroupPrefixAlignedAsync` → `ITransactionalAuditOutboxWriter.TryEnqueueAsync`
   çağırır: `AuditOutboxWriteRequest.TenantId` = `Intent.TargetTenantId` = `routeTenantId` (yazıcı boş hedefi reddeder),
   aktör principal'dan (`platform_admin` → `PlatformAdministrator`), yük Bölüm 21.3 Tablo C. Ayar yazımı ve denetim satırı
   birlikte commit olur ya da hiçbiri olmaz. Emsal: modül hakkı komutları (`EnableTenantModuleEntitlementCommand` +
   `PhysicalEntitlementAuditIntent.EnqueueAsync`, önceki / sonraki durum `StateOf`) ve kiracı aboneliği komutları; koruma
   `TransactionOwnedAuditCommandGuardTests` (liste = işaretli komutlar; işleyicinin kurucusunda işlem içi yazıcı kapısı).
   Okuma (`GetRecordCodeGroupPrefixQuery`) denetlenmez (Bölüm 21.2 "Okuma denetimi").
4. **Hizalama retleri denetime yazılır** (r6, inceleme 03 tur 3 F-1; sahip kararı Beste 2026-11-02, seçenek (a); AUD-001 §3
   "Reddedilen komut da kaydedilir", `.antigravity/rules/audit-trail-standard.md`). Dört ret de **işleyicide** verilir —
   `ValidationBehavior` `AuditBehavior`'dan ve işleyiciden önce koştuğu için (`…/Application/DependencyInjection.cs`,
   `AddOpenBehavior` sırası) doğrulayıcıda verilen ret denetime ulaşamazdı; bu yüzden hizalama komutunun doğrulayıcısı
   **yoktur** (Bölüm 5). Değerlendirme sırası: (i) rota kiracısı yok / silinmiş → 404; (ii) `BodyPresent = true` → 400
   `RECORD_CODE_REQUEST_INVALID`; (iii) etkin `OrgPrefix` kalıba uymuyor → 409 `GROUP_PREFIX_SOURCE_INVALID`; (iv) kilitli ve
   farklı → 409 `GROUP_PREFIX_LOCKED`. (iii)–(iv) iş işleminin gövdesinde saptanır; gövde **hiçbir şey yazmadan** ret sonucunu
   döner, iş işlemi boş commit olur. Ret kaydı ardından, paketin K-10 ret yazıcısıyla **ayrı kısa bir işlemde** yazılır:
   `RecordCodeAuditIntent.EnqueueGroupPrefixAlignRefusalAsync` → `ITransactionalAuditRefusalWriter.TryEnqueueRefusalAsync(session,
   request, AuditOutcome.Failed, …)` (Bölüm 5, 8.11 — aynı arayüz, aynı doğrulamalar ve aktör çözümü; yeni yazıcı yok). Aktör
   `platform_admin` principal'ı (`PlatformAdministrator`), `Intent.TargetTenantId` = rota kimliği. Yük ve kiracı bağı Bölüm
   21.3 Tablo D. **Sınıflama — dördü de `Failed`** (tek `Outcome` kuralı, Bölüm 21.2 başındaki cümle; r8 tur 2 F-3): hiçbiri
   kol (a)'ya girmez (aktör `PlatformAdminOnly` ve yönetici kaydı denetimini geçmiştir), hiçbiri kol (b)'ye girmez (platform
   yöneticisi bütün kiracıları görür; 404 gerçekten olmayan kiracıdır, gizleme değildir). Ret kaydı yazılamazsa uyarı loglanır, cevap değişmez (K-10 kuralı).

Emsal notu: platform bağlamındaki bir yönetici isteğinden `TenantScope.Begin` ile kiracı bağlamına geçen mevcut bir
işleyici yoktur (ölçüldü, `services/Diten.Platform/src` içinde `TenantScope.Begin(` kullanımları iş akışı, arka plan işi ve
güvenilir yürütücülerdir); bazı mevcut yönetici uçları depoya açık `tenantId` verip platform bağlamında yazar (ör.
`ITenantModuleEntitlementRepository.GetByIdAsync(Guid tenantId, …)`) — bu özellik (b) kuralı gereği bunu yapmaz. Bu yolu
AC-43 sabotajla ölçer.

**Silme yok:** soft delete dahil hiçbir silme yolu yoktur — kod asla yeniden kullanılmaz (§5.8); `IsDeleted` daima
`false`, `DeletedAt` uygulanmaz (gerekçe: silinebilir kayıt "arşivli kodu tekrar kullanma" kuralını zayıflatır).

### 4.1 RecordCodeCounter — `record_code_counters`

| Alan | Tip | Zorunlu | Kural |
|---|---|---|---|
| `TenantId` | Guid | evet | token kiracısı; `Guid.Empty` yasak |
| `Prefix` | string | evet | LegalEntity: MDM kısa kodu (`^[A-Z]{2,5}$`) · Group: grup öneki |
| `RecordType` | string | evet | `^[A-Z]{3}$`, katalogda |
| `LastNumber` | long | evet | son verilen numara; `$inc` 1; asla azalmaz, sıfırlanmaz |

Index: `ux_record_code_counters_tenant_prefix_type` — `(TenantId, Prefix, RecordType)` unique. Anahtarda `LegalEntityId`
yok (§5.1). MOD-0178 §22'nin "önek devri" notundaki durum (serbest kalan kısa kodun başka şirkete geçip aynı sayaçtan
devam etmesi) bu serviste r3'ten itibaren 409 ile durdurulur (Bölüm 8.13).

### 4.2 RecordCodeAllocation — `record_code_allocations` (ekleme-yalnız defter)

| Alan | Tip | Zorunlu | Kural |
|---|---|---|---|
| `Id` | Guid | evet | **ayırma kimliği (`allocationId`)**: her yeni ayırma denemesinde yeni Guid; denetim kutusu anahtarı bundan türer (Bölüm 8.6) |
| `TenantId` | Guid | evet | token kiracısı |
| `CallerService` | string | evet | kimlik bilgisinin kapalı listedeki servis adı (Bölüm 8.2, ör. `Diten.FinanceTreasuryService`) |
| `IdempotencyKey` | string | evet | Bölüm 12 (istemcinin anahtarı; yalnız bu defterde, kiracı + çağıran kapsamında tekil) |
| `RecordType`, `PrefixScope`, `LegalEntityId?`, `Prefix`, `Sequence` (long), `Code` | — | evet | `Prefix` koda bir kez yazılır (§5.2); `PrefixScope` **varsayılan uygulanmış** değerdir (gövdede yoksa `LegalEntity`) |
| `OnBehalfOfUserId` | Guid | evet | token `sub` |
| `CorrelationId` | string | evet | isteğin korelasyonu |
| `AllocatedAtUtc` | DateTimeOffset | evet | UTC |

**İstek eşitliği (r4, inceleme 03 tur 1 F-9).** r3'teki birleştirilmiş `RequestFingerprint` (SHA-256 metni) kaldırıldı;
"aynı gövde" kuralı saklanan üç alanın **ayrı ayrı** karşılaştırılmasıdır (ortak bulgu §3 "birleştirilmiş metin"):
`RecordType` sıralı (ordinal) eşit, `PrefixScope` enum değeri eşit — karşılaştırma **varsayılan uygulandıktan sonra**
yapılır, yani `prefixScope` gönderilmeyen v0 gövdesi ile `"prefixScope": "LegalEntity"` gönderen gövde aynıdır —,
`LegalEntityId` `Guid` olarak eşit (ikisi de boş ya da aynı değer; metin biçimi — büyük/küçük harf, `D` / `N` — karşılaştırmaya
girmez, ayrıştırılan `Guid` karşılaştırılır). Koleksiyon yenidir; eski biçimde satır yoktur.

Index'ler: `ux_record_code_allocations_tenant_caller_key` `(TenantId, CallerService, IdempotencyKey)` unique;
`ux_record_code_allocations_tenant_code` `(TenantId, Code)` unique; `ix_record_code_allocations_tenant_type_prefix_seq`
`(TenantId, RecordType, Prefix, Sequence desc)` (DL-6 için); **r3 (inceleme 02 F-11):**
`ix_record_code_allocations_tenant_le_time` `(TenantId, LegalEntityId, AllocatedAtUtc desc)` ve
`ix_record_code_allocations_tenant_prefix_le` `(TenantId, Prefix, LegalEntityId)` — kısa kod kullanım kilidinin ara dönem
denetimi için (Bölüm 8.13). Ayırma kimliğiyle arama yolu yoktur (r4 F-3: kutu `false` kurtarması kaldırıldı, Bölüm 8.6).

### 4.3 RecordCodeGroupPrefix — `record_code_group_prefixes` (kiracı ayarı)

| Alan | Tip | Zorunlu | Kural |
|---|---|---|---|
| `TenantId` | Guid | evet | yönetici ucunun rota kiracısı (var olan, silinmemiş kiracı; yoksa 404); `Guid.Empty` ve platform bağlamı yasak — depo çağrısı `TenantScope.Begin(_tenantContext, routeTenantId)` içinde (Bölüm 4 "Yönetici ucu", r4 F-1); ilk `Group` ayırmasında kilit yazımı token kiracısının bağlamında (yürütücü) |
| `GroupPrefix` | string | evet | **etkin belge `OrgPrefix`'inin kopyası** — elle girilmez; `^[A-Z]{2,5}$`, ASCII, normalleştirme yok |
| `AlignedAtUtc` / `AlignedBy` | — | evet | son hizalamanın zamanı / Platform aktörü |
| `LockedAtUtc` | DateTimeOffset? | hayır | ilk `Group` ayırması **aynı işlemde** yazar; doluysa değer kilitlidir |

Index: `ux_record_code_group_prefixes_tenant` `(TenantId)` unique.

**Tek kaynak (r3, inceleme 02 F-7).** Grup önekinin değerinin tek kaynağı belge kodlarının `OrgPrefix`'idir
(`DocumentCodingOptions.OrgPrefix`, `…/DocumentManagementIdentifiersModels.cs:45`, bölüm `DocumentManagement:Coding`
`:43`). Ayrı düzenlenebilir bir değer **yoktur**: yönetici ucu gövde almaz, yalnız etkin `OrgPrefix`'i kiracı ayarına
kopyalar (hizalama). Kiracı ayarı iki şey için vardır: (1) Group kapsamının bu kiracıda açıkça devreye alındığını
kaydetmek (kayıt yoksa 409 `GROUP_PREFIX_MISSING` — servis `OrgPrefix`'ten kendiliğinden doldurmaz); (2) önek bir kez
kullanıldıktan sonra **kilitlemek** (RCS-001 §5.4 dili: "bir kez herhangi bir kaydın önekinde kullanıldıktan sonra
değiştirilemez").

- **Kilitsizken düzeltme = `OrgPrefix`'i değiştirmek:** operatör `DocumentManagement:Coding:OrgPrefix` yapılandırmasını
  değiştirir, **bütün Platform örneklerini yeniden başlatır** (erişimci `IOptions`, Bölüm 8.12 — r7 F-8), sonra hizalama ucunu yeniden çağırır; kiracı ayarı yeni değere güncellenir (önceki/sonraki değer yol a ile,
  işlem içi kapıdan aynı işlemde denetlenir — Bölüm 21.3 Tablo C, r5 F-1).
- **Kilitten sonra:** hizalama ucu, etkin `OrgPrefix` kilitli değerden farklıysa 409 `GROUP_PREFIX_LOCKED` döner ve
  değişmez; aynıysa 200 (değişiklik yok).
- **Kilitten sonra `OrgPrefix` değişirse:** her `Group` ayırması 409 `GROUP_PREFIX_MISMATCH` alır (kod verilmez).
  **Kurtarma yolu:** (a) önerilen ve tek yetkili yol — `OrgPrefix` yapılandırması kilitli değere geri alınır (belge
  kodları ile kayıt kodları aynı grup önekini taşımalıdır, RCS-001 §5.3); (b) grup önekinin bilinçli olarak değiştirilmesi
  (yeni önekle yeni sayaç) bu paketin kapsamında değildir — sahip kararı ve ayrı iş (FU-RCS-16). Bu durum Platform
  günlüğüne **hata** düzeyinde yazılır.
- Silme yok. Kodda varsayılan yok (`OrgPrefix`'in kod varsayılanı `"GMG"`, belge kodlarının bugün kullandığı etkin değer
  olarak okunur; servis bunu kendiliğinden kiracıya yazmaz — yalnız Platform yöneticisinin hizalamasıyla).

### 4.4 Tür önek kaydı — `RecordCodeTypeCatalog` (kodda, K-12 b)

Gerekçe: §3 tablosu `.antigravity` altında onaylı bir kuraldır; kataloğu §3'e sabitleyen test kural ile çalışma zamanı
arasındaki her sapmayı kırmızı yapar.

| TÜR | Kayıt türü (RCS-001 §3) | Sınıf | Sahip modül | İzinli kapsam | İzinli çağıran (v1) | Kullanıcıda gerekli izin (F-4) |
|---|---|---|---|---|---|---|
| `ORG` | Organizasyon Birimi | OTOMATİK | MOD-0288 | DL-1'de boş küme · **DL-2 (r12, K-15): yalnız LegalEntity** — birimin `LegalEntityId`'si | DL-1: yok · **DL-2: `Diten.Platform` — yalnız süreç içi port**, HTTP bağlaması yok | DL-2: `platform.organization-units.create` |
| `POS` | Pozisyon (kadro) | OTOMATİK | MOD-0288 | DL-1'de boş küme · **DL-2 (r12, K-15): yalnız LegalEntity** — pozisyonun oluşturma anındaki biriminin `LegalEntityId`'si (Bölüm 8.14) | DL-1: yok · **DL-2: `Diten.Platform` — yalnız süreç içi port** | DL-2: `platform.positions.create` |
| `PAY` | Ödeme Partisi | OTOMATİK | MOD-0126 | LegalEntity | FinanceTreasury | `finance-treasury.payment-batches.create` |
| `APD` | AP Belgesi | OTOMATİK | MOD-0119 | LegalEntity | FinanceTreasury | `finance-treasury.ap-invoices.create` |
| `ARI` | AR Alacak Faturası | OTOMATİK | MOD-0120 | LegalEntity | FinanceTreasury | `finance-treasury.receivable-invoices.create` |
| `CPS` | Nakit Pozisyonu Anlık Görüntüsü | OTOMATİK | MOD-0124 | LegalEntity | FinanceTreasury | `finance-treasury.cash-positions.create` |
| `CTP` | Nakit Eşik Politikası | OTOMATİK | MOD-0124 | LegalEntity | FinanceTreasury | `finance-treasury.cash-position-threshold-policies.create` |
| `BNK` | Banka | OTOMATİK | MOD-0289 | **yalnız Group** | Mdm | `mdm.banks.create` |
| `BAC` | Banka Hesabı | OTOMATİK | MOD-0289 | LegalEntity (şirket hesabı) **ve** Group (tedarikçi hesabı) | Mdm | `mdm.bank-accounts.create` |
| `AGR` | Sözleşme | OTOMATİK | MOD-0217 | LegalEntity (yöneten şirket) | Mdm | `mdm.contracts.create` |
| `PLN` | Abonelik Planı | İKİSİ DE (§5.3) | Platform | Platform (DL-4) | yok (DL-4) | DL-4'te |

İzin adları MOD-0018-FU27 taslağındaki anahtarlardır (`execution/domains/platform-shared-services/module-packs/MOD-0018-FU27-treasury-permission-onboarding.md`,
ölçüldü); FU27 onayıyla kesinleşir, değişirse bu tablo aynı gün güncellenir (A-11). `APD` alacak dekontu da
`ap-invoices.create` ile açılır (çıkarım; Treasury teyidi A-11).

Katalogda olmayan her değer — KALSIN (`ACC`, `CMP`, `GP`, `GS`, `LS`, `FG`, `IT`, `UID`), İKİSİ DE (`TTY`, `PFL`,
`PRG`, `PRJ`, `INI`, `INV`), eski `CTR`, WMS'in henüz §3'te olmayan `PUT` / `PCK` / `OUT` / `PKG`'si — 422
`RECORD_TYPE_UNKNOWN`. `ORG`, `POS`, `PLN` katalogdadır ama izinli çağıranı yoktur: HTTP'den 403 `RECORD_CODE_FORBIDDEN`.
`ORG` / `POS` için izinli kapsam DL-1 kataloğunda **boş kümedir** (r4, inceleme 03 tur 1 F-7): açık karar A-8 DL-1'e sabitlenmez;
izinli çağıranı olmadığı için kapsam adımına (Bölüm 8.5 adım 10) hiç gelinmez. Kapsam DL-2 paket revizyonunda A-8 kararıyla
yazılır.

**r12 (DL-2, K-15).** `ORG` / `POS` kapsamı `LegalEntity`, izinli çağıranı `Diten.Platform` olur (süreç içi port, Bölüm 8.14);
bugünkü katalog satırları `services/Diten.Platform/src/Diten.Platform.Domain/Features/RecordCodes/RecordCodeTypeCatalog.cs:21-22`
(boş kapsam, `null` çağıran, `null` izin). HTTP yolunda adım 7 kimlik bilgisinin izinli tür kümesine bakar ve bu küme koddaki
kapalı eşlemeden gelir (`TrustedRecordCodeCredentialAuthenticator.cs:20-21`: FinanceTreasury `PAY/APD/ARI/CPS/CTP`, Mdm
`BNK/BAC/AGR`) — bu yüzden iç uçtan `ORG` / `POS` DL-2'den sonra da 403 `RECORD_CODE_FORBIDDEN`'dır (sözleşme 1.0.0 §6 metniyle
aynı; AC-49). Katalogdaki `AllowedCaller = "Diten.Platform"` HTTP eşlemesine girmez; DL-1'in "katalog çağıranları = HTTP eşlemesi"
testi (`services/Diten.Platform/tests/Diten.Platform.Application.Tests/RecordCodes/RecordCodeTypeCatalogTests.cs:70-88`)
`Diten.Platform` dışındaki çağıranlar için kurulur (AC-48). `Diten.Platform` denetim kapısının kapalı kümesinde zaten vardır
(`…/Application/Features/Audit/TransactionOwnedAuditSourceServices.cs:10-14`); küme büyümez.

## 5. Repo Scope

**DL-1 (v1) — tam dosya yolları.** N = yeni, M = yalnız belirtilen eklemeli değişiklik. Kompozisyon dosyalarının
(`Program.cs`, `DependencyInjection.cs`) SHA-256'sı sabitlenmez (module-pack-standard §13).

API (`services/Diten.Platform/src/Diten.Platform.API/`):

- N `Controllers/Internal/InternalRecordCodesController.cs`
- N `Controllers/Admin/TenantRecordCodeGroupPrefixController.cs`
- N `Security/ITrustedRecordCodeRequestExecutor.cs`
- N `Security/TrustedRecordCodeRequestExecutor.cs`
- N `Security/ITrustedRecordCodeCredentialAuthenticator.cs`
- N `Security/TrustedRecordCodeCredentialAuthenticator.cs`
- N `Security/ITrustedRecordCodeJwtContext.cs`
- N `Security/TrustedRecordCodeJwtContext.cs`
- N `Security/TrustedRecordCodeAllocateRequestParser.cs`
- N `Security/RecordCodeAllocateRateLimiter.cs` — yürütücü içi `System.Threading.RateLimiting.PartitionedRateLimiter`
  (singleton), bölüm anahtarı (tüketici servis adı, token kiracısı); **`PeopleSearchRateLimit.cs` ve ASP.NET
  `AddRateLimiter` / `EnableRateLimiting` kullanılmaz** — oradaki `OnRejected` tek kancadır ve politika dışı reddi
  `RATE_LIMITED` diye yazar (`…/Security/PeopleSearchRateLimit.cs:23`, `:38-46`); bu özellik kendi 429
  `RECORD_CODE_RATE_LIMITED` cevabını yürütücünün ret yolundan yazar (r3, inceleme 02 F-5); `record_codes_rate_limited_total` sayacı da bu dosyadadır (r7, K-13)
- N `Configuration/TrustedRecordCodeCredentialOptions.cs`
- N `Models/RecordCodes/AllocateRecordCodeTransportRequest.cs`
- M `Program.cs` — yalnız DI kaydı ve seçenek bağlama satırları

Application (`services/Diten.Platform/src/Diten.Platform.Application/`):

- N `Features/RecordCodes/Commands/AllocateRecordCodeCommand.cs`
- N `Features/RecordCodes/Commands/AlignRecordCodeGroupPrefixCommand.cs`
- N `Features/RecordCodes/Queries/GetRecordCodeGroupPrefixQuery.cs`
- N `Features/RecordCodes/Handlers/CommandHandlers/AllocateRecordCodeHandler.cs`
- N `Features/RecordCodes/Handlers/CommandHandlers/AlignRecordCodeGroupPrefixHandler.cs`
- N `Features/RecordCodes/Handlers/QueryHandlers/GetRecordCodeGroupPrefixHandler.cs`
- ~~N `Features/RecordCodes/Validators/AllocateRecordCodeValidator.cs`~~ — **r7'de kaldırıldı** (son bakış 01 F-1): `ValidationBehavior` işleyiciden ve `AuditBehavior`'dan önce koşar, ret `ProblemDetails` olur, zarf ve ret kaydı yoktur; biçim kuralları yalnız API ayrıştırıcısında (`TrustedRecordCodeAllocateRequestParser`), ret işleyicide (Bölüm 8.5; AC-45)
- ~~N `Features/RecordCodes/Validators/AlignRecordCodeGroupPrefixValidator.cs`~~ — **r6'da kaldırıldı** (inceleme 03 tur 3
  F-1): `ValidationBehavior` işleyiciden önce koşar, orada verilen ret denetime yazılamaz; dört hizalama reddi işleyicide
  verilir (Bölüm 4 "Yönetici ucu" adım 4). Komutun doğrulanacak gövdesi yoktur.
- N `Features/RecordCodes/RecordCodesModels.cs`
- N `Features/RecordCodes/RecordCodeReasonCodes.cs`
- N `Features/RecordCodes/RecordCodeAuditIntent.cs`
- N `Contracts/RecordCodes/ILegalEntityShortCodeReader.cs`
- M `DependencyInjection.cs` — yalnız kayıt satırları; **ret yazıcısı burada ayrı** `services.AddScoped<ITransactionalAuditRefusalWriter, CanonicalTransactionalAuditOutboxWriter>();`
  satırıyla, mevcut `ITransactionalAuditOutboxWriter` kaydının (`:329-330`) yanında kaydedilir (r3, inceleme 02 F-3).
  `DependencyInjectionSmokeTests.cs:281-282` (`ITransactionalAuditOutboxWriter` için tek kayıt, uygulama
  `CanonicalTransactionalAuditOutboxWriter`) değişmeden yeşil kalır; Infrastructure DI'da yazıcı kaydı **yoktur**.

**Denetim yazıcısı — yalnız eklemeli; varsayılanlar aynı; mevcut bütün denetim testleri değişmeden yeşil (K-9, K-10):**

- M `services/Diten.Platform/src/Diten.Platform.Application/Contracts/Audit/TransactionOwnedAuditIntent.cs` — yalnız
  isteğe bağlı `public string? SourceService { get; init; }` ve sınıf yorumunun (`:6-9`) güncellenmesi: "kaynak servis
  kapıda kurulur; intent yalnız `TransactionOwnedAuditSourceServices` kümesindeki bir adı önerebilir, boşsa
  `Diten.Platform`" (r3, inceleme 02 F-4). Dosya `Contracts/Audit` altındadır — **Contracts/Audit'e dokunulur**.
- N `services/Diten.Platform/src/Diten.Platform.Application/Contracts/Audit/ITransactionalAuditRefusalWriter.cs` —
  `Task<bool> TryEnqueueRefusalAsync(IPlatformTransactionSession session, AuditOutboxWriteRequest request,
  AuditOutcome outcome, CancellationToken ct)`; `outcome ∈ {Denied, Failed}`, `Succeeded` → `TransactionOwnedAuditIntentInvalidException`.
- N `services/Diten.Platform/src/Diten.Platform.Application/Features/Audit/TransactionOwnedAuditSourceServices.cs` —
  **kapalı izinli kaynak servis adı kümesi** (r3, F-4): `Diten.Platform`, `Diten.FinanceTreasuryService`,
  `Diten.MdmService` (bugün `Diten.Platform` + bu servisin tüketici kimlik bilgisi eşlemesindeki adlar; DL-8'de
  `Diten.InventoryService` eklenir). Küme yalnız bu pakete bağlı revizyonla büyür.
- M `services/Diten.Platform/src/Diten.Platform.Application/Features/Audit/CanonicalTransactionalAuditOutboxWriter.cs` —
  (a) `:138`: `SourceService: string.IsNullOrWhiteSpace(intent.SourceService) ? SourceService : intent.SourceService`;
  `:39` sabiti aynı; doluysa değer `TransactionOwnedAuditSourceServices` kümesinde **birebir** (sıralı karşılaştırma,
  kırpma yok) olmalıdır, değilse `TransactionOwnedAuditIntentInvalidException` (hiçbir şey yazılmaz); (b) sınıf
  `ITransactionalAuditRefusalWriter`'ı da uygular: aynı doğrulamalar (`:81-92`), aynı aktör çözümü (`:156-185`), aynı kaynak
  servis kuralı, aynı yük kurucu (`:117-140`), tek fark `Outcome` = verilen ret sonucu (bugün `:131` sabit `Succeeded`).
  Mevcut `TryEnqueueAsync` imzası ve davranışı değişmez.
- M `services/Diten.Platform/src/Diten.Platform.Application/Contracts/Audit/TransactionOwnedAuditCommands.cs` — **yalnız**
  `Authorized` kümesine tek satır `typeof(AlignRecordCodeGroupPrefixCommand)` (yorum: "record codes — MOD-0040-FU02, CT
  kararı 2026-11-02, AUD-001 §4.4") ve onun `using Diten.Platform.Application.Features.RecordCodes.Commands;` satırı (r5,
  inceleme 03 tur 2 F-1). Başka ad eklenmez, çıkarılmaz; sınıf yorumu değişmez. `TransactionOwnedAuditCommandGuardTests`
  **değişmez** ve yeşil kalır (liste = işaretli komutlar; işleyici `ITransactionalAuditOutboxWriter` taşır).
- `ITransactionalAuditOutboxWriter.cs`, `ITransactionalAuditOutboxStore.cs`, `AuditOutboxWriteRequest.cs` **değişmez**.
- `TransactionOwnedAuditPayloadExemptions.cs` listesine **ekleme yapılmaz** (liste yalnız küçülür, 2026-11-13'te biter;
  `:9-13`); elle yük yolu kullanılmaz — `RecordCodeAuditIntent` her zaman `Intent` taşır.

Domain (`services/Diten.Platform/src/Diten.Platform.Domain/`):

- N `Entities/RecordCodes/RecordCodeCounter.cs`
- N `Entities/RecordCodes/RecordCodeAllocation.cs`
- N `Entities/RecordCodes/RecordCodeGroupPrefix.cs`
- N `Enums/RecordCodes/RecordCodePrefixScope.cs`
- N `Features/RecordCodes/RecordCodeTypeCatalog.cs`
- N `Features/RecordCodes/RecordCodeFormat.cs`
- N `Features/RecordCodes/RecordCodeExceptions.cs` — dört istisna (r4 F-3; r7 F-9)
- N `Repositories/IRecordCodeRepositories.cs`

Infrastructure (`services/Diten.Platform/src/Diten.Platform.Infrastructure/`):

- N `Persistence/Repositories/RecordCodeRepositories.cs`
- N `Persistence/Schema/PlatformSchemaManifest.RecordCodes.cs`
- M `Persistence/Schema/PlatformSchemaManifest.cs` — yalnız `.Concat(RecordCodesCollections)`
- M `Persistence/Schema/SchemaProfile.cs` — yalnız `RecordCodes = 14`
- M `Persistence/Schema/PlatformCollections.cs` — yalnız üç koleksiyon adı sabiti
- N `Services/Mdm/MdmLegalEntityShortCodeReader.cs`
- M `DependencyInjection.cs` — yalnız depo ve MDM `HttpClient` kayıt satırları (denetim yazıcısı kaydı **yok**)

Yapılandırma: M `services/Diten.Platform/src/Diten.Platform.API/appsettings.json` — yalnız `TrustedRecordCodeCredential`
bölümünün **boş** iskeleti ve hız sınırı değeri (A-16 kapanınca); `appsettings.Development.json`'a gizli değer yazılmaz.
`DocumentManagement:Coding` bölümüne dokunulmaz.

Testler (`services/Diten.Platform/tests/Diten.Platform.Application.Tests/`):

- N `RecordCodes/RecordCodeFormatTests.cs`
- N `RecordCodes/RecordCodeTypeCatalogTests.cs`
- N `RecordCodes/AllocateRecordCodeHandlerTests.cs`
- N `RecordCodes/RecordCodeAllocationMongoTests.cs`
- N `RecordCodes/RecordCodeTenantIsolationMongoTests.cs`
- N `RecordCodes/RecordCodeAuditDeliveryMongoTests.cs`
- N `RecordCodes/RecordCodeActorProbeHttpTests.cs` — DL-1'in **ilk commit grubu** (AC-41; asgari yol Bölüm 16 AC-41)
- N `RecordCodes/InternalRecordCodesHttpTests.cs`
- N `RecordCodes/TrustedRecordCodeRequestExecutorRefusalTests.cs`
- N `RecordCodes/RecordCodeAllocateRateLimiterTests.cs`
- N `RecordCodes/MdmLegalEntityShortCodeReaderTests.cs`
- N `RecordCodes/RecordCodeGatewayExposureTests.cs`
- N `RecordCodes/TenantRecordCodeGroupPrefixHttpTests.cs`
- N `Audit/TransactionOwnedAuditSourceServiceTests.cs`
- N `Audit/TransactionalAuditRefusalWriterTests.cs`
- M `Authorization/LoginOnlyEndpointGuardTests.cs` — yalnız `AnonymousInternalControllers`'a `InternalRecordCodesController`
  (gerekçe yorumuyla; `:77-128` deseni); **DL-1'in ilk commit grubunda** (AC-41, r5 tur 2 F-4)
- M `Schema/PlatformSchemaManifestTests.cs` — yalnız yeni profilin beklenen sayımı (gerekirse)
- Mevcut denetim testleri ve `DependencyInjectionSmokeTests.cs` **değiştirilmez**; `"Diten.Platform"` varsayılanını
  `TransactionOwnedAuditDoorTests.cs:55` ve `TransactionOwnedAuditDeliveryMongoTests.cs:73` kanıtlar.

**DL-1'in dışında, CT / sahip adımı (geliştirici yazmaz):** `tests/architecture/audit-ledger/Diten.Platform.md` `## İzler`
(`platform-islem-ici` satırının belirtecine `RecordCodeAuditIntent.EnqueueAsync/RecordCodeAuditIntent.EnqueueRefusalAsync/RecordCodeAuditIntent.EnqueueGroupPrefixAlignedAsync/RecordCodeAuditIntent.EnqueueGroupPrefixAlignRefusalAsync`;
r5: hizalama komutu `ITransactionOwnedAuditCommand` olduğu için `platform-merkezi` izinden çıkar ve bu belirteç olmadan
"denetimsiz" sayılır)
+ `tests/architecture/audit-ledger/CT-DECISIONS.md` + `tests/architecture/TenantArchitecture.ArchitectureTests/AuditTrailStandardTests.cs`
sabitleri (A-10); `.antigravity/rules/record-coding-standard.md` §3 (A-3, Ali + Treasury PR'ı);
`execution/registries/module-id-registry.md` ve domain-config (A-12); sözleşmenin dondurulması (2026-11-03).

### DL-2 (r12) — tam dosya yolları

N = yeni, M = yalnız belirtilen değişiklik. **XMC** = başka modülün dosyası: MOD-0288 Organization, Person & Position Directory
(`execution/domains/platform-shared-services/module-packs/MOD-0288-organization-person-position-directory.md`, künye
`owner: platform-team`; FU02–FU04 künyeleri `organization-governance-owner`). Aynı domain'dir ama başka modüldür; XMC-001 gereği
**dokunmadan önce sahip onayı** gerekir — onaylayan kişi **TBD** (Q-7). Satır numaraları `a9fd192ba`'da ölçüldü.

Bu paketin dosyaları (MOD-0040-FU02):

- M `services/Diten.Platform/src/Diten.Platform.Domain/Features/RecordCodes/RecordCodeTypeCatalog.cs` — yalnız `ORG` / `POS`
  satırları (`:21-22`: kapsam `[LegalEntity]`, çağıran `"Diten.Platform"`, izin) ve sınıf yorumu (`:10-12`).
- N `services/Diten.Platform/src/Diten.Platform.Application/Contracts/RecordCodes/IInProcessRecordCodeAllocator.cs` — port ve
  iki kayıt (Bölüm 3).
- N `services/Diten.Platform/src/Diten.Platform.Application/Features/RecordCodes/InProcessRecordCodeAllocator.cs`.
- M `services/Diten.Platform/src/Diten.Platform.Application/Features/RecordCodes/RecordCodeReasonCodes.cs` — yalnız üç yeni sabit.
- M `services/Diten.Platform/src/Diten.Platform.Application/DependencyInjection.cs` — yalnız
  `services.AddScoped<IInProcessRecordCodeAllocator, InProcessRecordCodeAllocator>();`.
- M `services/Diten.Platform/tests/Diten.Platform.Application.Tests/RecordCodes/RecordCodeTypeCatalogTests.cs` —
  `AC09_each_type_allows_exactly_its_scopes_and_ORG_POS_have_no_caller_and_no_scope` (`:47-67`) ve
  `AC14_the_consumer_map_is_the_catalogue_s_callers_and_no_binding_can_ask_for_another_s_types` (`:70-88`) yalnız ORG / POS
  ve `Diten.Platform` çağıranı için (`PLN` beklentisi aynen kalır); AC-48 testleri.
- M `services/Diten.Platform/tests/Diten.Platform.Application.Tests/RecordCodes/InternalRecordCodesHttpTests.cs` — yalnız AC-49 testi.
- N `services/Diten.Platform/tests/Diten.Platform.Application.Tests/RecordCodes/InProcessRecordCodeAllocatorTests.cs`.
- N `services/Diten.Platform/tests/Diten.Platform.Application.Tests/RecordCodes/OrganizationRecordCodeHttpTests.cs` (gerçek HTTP,
  üretim `TenantResolutionMiddleware`, gerçek Mongo, sahte MDM — `RecordCodeHttpHost` deseni).
- N `frontend/Diten.Web/tests/organization-record-code-form.test.js` (Web node testi, AC-60).
- **Değişmez (ölçülür):** `TrustedRecordCodeCredentialAuthenticator.cs` (`Consumers`), `TrustedRecordCodeRequestExecutor.cs`,
  `InternalRecordCodesController.cs`, `AllocateRecordCodeHandler.cs`, `RecordCodeAuditIntent.cs`,
  `TransactionOwnedAuditSourceServices.cs`, `PlatformSchemaManifest.RecordCodes.cs`, `docs/analysis/contracts/record-code-allocate.md`.

MOD-0288 dosyaları (**XMC-001**, sahip onayı TBD — Q-7; her satır ayrı onay kalemidir):

- M (XMC) `services/Diten.Platform/src/Diten.Platform.Application/Features/TenantOrganization/Handlers/CommandHandlers/CreateOrganizationUnitCommandHandler.cs`
  — kurucuya `IInProcessRecordCodeAllocator`; `:34-43` elle kod dalı yerine Bölüm 8.14 akışı; `:76-84` varlık kurulumunda
  `Id` = önceden üretilen kayıt kimliği, `Code` = ayrılan kod.
- M (XMC) `…/TenantOrganization/Handlers/CommandHandlers/CreatePositionCommandHandler.cs` — aynı; şirket = birimin `LegalEntityId`'si
  (`:27-54`).
- M (XMC) `…/TenantOrganization/Handlers/CommandHandlers/UpdateOrganizationUnitCommandHandler.cs` — `:49-58` ve `:145`: kod
  değişikliği reddi, saklı kodun korunması (Bölüm 8.15). `UpdateOrganizationUnitReportingLinesCommandHandler` (`:188-232`)
  **değişmez** (saklı kodu geri yükler, `:215-216`).
- M (XMC) `…/TenantOrganization/Handlers/CommandHandlers/UpdatePositionCommandHandler.cs` — `:32-41`, `:49`.
- M (XMC) `…/TenantOrganization/TenantOrganizationContracts.cs` — `OrganizationUnitRequest.Code` (`:9`) ve `PositionRequest.Code`
  (`:52`) `string?` olur; yer ve ad değişmez (konumsal kurulum ve JSON bağlaması korunur).
- M (XMC) `…/TenantOrganization/Validators/TenantOrganizationRequestValidators.cs` — `:9`, `:19`: `NotEmpty()` kalkar,
  `MaximumLength(80)` kalır. Oluşturmada dolu kod reddi doğrulayıcıda **değil** işleyicide (Bölüm 8.15).
- M (XMC) `services/Diten.Platform/src/Diten.Platform.Domain/Repositories/IOrganizationUnitRepository.cs`, `IPositionRepository.cs`
  — yalnız `Task<bool> ExistsByCodeIncludingDeletedAsync(string code, CancellationToken ct = default)`.
- M (XMC) `services/Diten.Platform/src/Diten.Platform.Infrastructure/Persistence/Repositories/OrganizationUnitRepository.cs`,
  `PositionRepository.cs` — yalnız o yöntem: filtre `TenantId == bağlam kiracısı && Code == code`, `IsDeleted` filtresi **yok**
  (bugünkü `ExistsByCodeAsync` `ExecutionFilter` ile silinmişleri dışlar — `OrganizationUnitRepository.cs:96-110`,
  `PositionRepository.cs:17-31`, `services/Diten.Platform.Common/src/Diten.Platform.Common/Persistence/Repositories.cs:75-79`).
- M (XMC) `services/Diten.Platform/tests/Diten.Platform.Application.Tests/TenantOrganization/TenantOrganizationRulesTests.cs` —
  oluşturma işleyicisi kurucuları (`:25`, `:38`, `:50`, `:78`, `:90`, `:134`, `:149`, `:182`, `:197`, `:425`) ve kod semantiği
  değişen testler: `Organization_unit_create_rejects_duplicate_code_in_tenant` (`:34`), `…create_rejects_normalized_empty_code`
  (`:47`), `Organization_unit_update_rejects_normalized_empty_code` (`:59`), `Position_create_rejects_normalized_empty_code`
  (`:143`), `Position_update_rejects_normalized_empty_code` (`:158`), `Position_create_rejects_duplicate_code_in_tenant` (`:175`) —
  yeni davranışa (AC-52, AC-53) çevrilir, silinmez; değişiklik kabul kaydında satır satır gerekçelenir.
- M (XMC) `…/TenantOrganization/OrganizationEnterpriseFieldsTests.cs` (`:28`, `:60` kurucu) ve
  `…/TenantOrganization/OrganizationMatrixReportingTests.cs` (`:467` kurucu).
- M (XMC) `frontend/Diten.Web/Views/Organization/OrganizationUnits/Form.cshtml` (`:53-56` kod girişi),
  `frontend/Diten.Web/Views/Organization/Positions/Form.cshtml` (`:51-54`).
- M (XMC) `frontend/Diten.Web/wwwroot/assets/js/Organization/OrganizationUnits/form.js` (`:31` zorunlu liste, `:249` yük,
  `:274-275` hata gösterimi, `:399` kod doldurma) ve `…/Organization/Positions/form.js` (`:15`, `:152`, `:184-186`, `:206`).
- M (XMC) `frontend/Diten.Web/Views/Organization/OrganizationUnits/_FormL10n.cshtml`, `…/Positions/_FormL10n.cshtml` — yalnız yeni
  anahtarların JS'e aktarımı.
- M (XMC) `frontend/Diten.Web/Resources/Views/Organization/OrganizationUnits/OrganizationUnitsIndex.{en,tr,fr,es,zh,ar,ru}.resx`
  ve `frontend/Diten.Web/Resources/Views/Organization/Positions/PositionsIndex.{en,tr,fr,es,zh,ar,ru}.resx` — yalnız Bölüm 11'deki
  anahtarlar (7 dil — Q-3).
- M (XMC, ortak ekran testi) `frontend/Diten.Web/tests/xmc-date-guard-screens.test.js` — `:122` ve `:127` zorunlu alan
  listelerinden `ouCode` / `pCode`.
- **Değişmez:** API controller'ları `services/Diten.Platform/src/Diten.Platform.API/Controllers/Platform/OrganizationUnitsController.cs`
  ve `PositionsController.cs` (`[HasPermission]` anahtarları aynı); Web vekilleri `frontend/Diten.Web/Controllers/OrganizationUnitsController.cs`,
  `PositionsController.cs`; liste ekranları (`Index.cshtml`, `_DataTable.cshtml`, `_Filter.cshtml`, `index.js` — liste ekranı
  dokunma protokolü tetiklenmez); `Details.cshtml`; `_CreateEditOffcanvas.cshtml` parçaları; `OrganizationCodeNormalizer.cs`
  (oluşturma yolunda artık çağrılmaz; Q-2 B seçilirse güncellemede kullanılır); `…/Infrastructure/Persistence/Configurations/PositionSeed.cs`;
  `PlatformSchemaManifest.Organization.cs` (index değişmez); `tests/architecture/audit-ledger/**`; `gateway/**/ocelot.json`.

**Koordinasyon (ölçülmedi):** MOD-0288-FU03 (`MOD-0288-FU03-organization-unit-screen-matrix-and-values.md`, `status: ready-for-dev`,
`shell: tenant`) ve FU04 aynı Organizasyon Birimi ekranlarına dokunabilir; bugün açık bir dalda bu dosyalarda iş olup olmadığı bu
revizyonda ölçülmedi — sıra CT'nin (Q-7).

## 6. Protected Paths

- `.antigravity/**` — RCS-001 §3 değişikliği Ali'nin (A-3).
- Doküman kimlik üreticisi: `services/Diten.Platform/src/Diten.Platform.Application/Features/DocumentManagementIdentifiers/**`,
  `…/Infrastructure/Persistence/Repositories/DocumentManagementIdentifierRepositories.cs`,
  `…/Domain/Entities/DocumentManagement/DocumentIdentifierSequenceCounter.cs` — genişletilmez (K-1). `DocumentCodingOptions`
  **okunur**, değiştirilmez (Bölüm 8.12).
- `…/Infrastructure/Services/Mdm/MdmLegalEntityReferenceValidator.cs` — değişmez.
- `…/API/Security/TrustedLegalEntityScope*.cs`, `TrustedServiceTokenValidationExtensions.cs`, `TrustedSourceAuditIntent*.cs` — emsal, değişmez.
- Denetim altyapısında Bölüm 5'te adı geçen eklemeler **dışındaki her şey**: özellikle `Contracts/Audit/AuditActorTypeResolver.cs`,
  `Contracts/Audit/TransactionOwnedAuditCommands.cs` (**tek istisna**: `Authorized`'a `typeof(AlignRecordCodeGroupPrefixCommand)`
  satırı ve onun `using`'i — CT kararı 2026-11-02, AUD-001 §4.4; r5 F-1; başka her değişiklik korunan yol),
  `Contracts/Behaviors/AuditBehavior.cs`, `Contracts/Audit/ITransactionOwnedAuditCommand.cs`,
  `tests/…/Audit/TransactionOwnedAuditCommandGuardTests.cs`, `Contracts/Audit/ITransactionalAuditOutboxWriter.cs`,
  `Contracts/Audit/ITransactionalAuditOutboxStore.cs`, `Contracts/Audit/AuditOutboxWriteRequest.cs`,
  `Features/Audit/TransactionOwnedAuditPayloadExemptions.cs` (ekleme yasak), `Domain/Enums/AuditEnums.cs` (yeni kategori yok).
- `services/Diten.MdmService/**`, `services/Diten.AuthService/**`, `services/Diten.ProcurementService/**`,
  `services/Diten.InventoryService/**`, `services/Diten.Building.Blocks/**` ve diğer domain servisleri.
- `gateway/Diten.ApiGateway/**/ocelot.json` — integration-agent'ın; bu paket rota eklemez.
- `frontend/Diten.Web/**` — v1'de ekran yok.
- `execution/registries/**`, `tests/architecture/audit-ledger/**`, `docs/roadmap/backlog/**` — CT'nin.
- **DL-2 (r12):** `docs/analysis/contracts/record-code-allocate.md` (FROZEN 1.0.0 — değişmez; ORG / POS HTTP'de 403 kalır);
  HTTP kimlik bilgisi eşlemesi `TrustedRecordCodeCredentialAuthenticator.Consumers` (ORG / POS eklenmez); `AllocateRecordCodeHandler.cs`
  (port onu çağırır, değiştirmez); `TransactionOwnedAuditSourceServices.cs` (küme büyümez). MOD-0288 dosyalarından yalnız Bölüm 5
  "DL-2" listesindekiler ve yalnız XMC-001 onayından sonra; listede olmayan MOD-0288 dosyası korunan yoldur.

## 7. Dependencies

| Bağımlılık | Bugünkü durum (ölçüldü) | DL-1 için |
|---|---|---|
| MDM lookup-validation `GET /api/legal-entities/{id}/lookup-validation` (izin `mdm.legal-entities.read`) | var — `services/Diten.MdmService/src/Diten.MdmService.Api/Controllers/LegalEntitiesController.cs:48-54`; yok / silinmiş / başka kiracı / aktif değil → 404; yanıt `LegalEntityLookupDto` 1.0.0 (`…/Features/LegalEntity/LegalEntityModels.cs:100-106`) — **`approvalStatus` yok** | gerekli; değişiklik istenmez |
| **MDM lookup-validation v1.1 (`approvalStatus`)** | taslak (`docs/analysis/contracts/drafts/legal-entity-lookup-validation-v1.1-proposal.md`); v1.1'de alan yoksa yanıt geçersizdir → 503 (`:69`) | **açık bağımlılık, sahibi MOD-0220 sahibi** (r3, inceleme 02 F-8): v1.1 yayınlanana kadar servis yalnız aktiflik denetler ve TC-3 zorunludur; v1.1 yayınlandığı sürümde bu servisin okuyucusu da "alan yok → 503" kuralına geçirilir (kod değişikliği, FU-RCS-15) |
| Çağıranın kullanıcı token'ında `mdm.legal-entities.read` | MDM, iletilen kullanıcı token'ını yetkilendirir (Platform emsali `MdmLegalEntityReferenceValidator.cs:142-152`); finans rolleri bağımlı izin olarak alır (MOD-0018-FU27 K-4 / D72) | LegalEntity kapsamı için gerekli; yoksa MDM 403 → **403 `LEGAL_ENTITY_READ_PERMISSION_MISSING`** (r7, F-10; kalıcı) |
| Kullanıcı token'ında `permission` iddiaları | FU21 bunları okuyor (`TrustedLegalEntityScopeJwtContext.cs:40`) | gerekli (Bölüm 4.4 son sütunu) |
| Treasury ve MDM izin anahtarları (Bölüm 4.4) | MOD-0018-FU27 taslağında | adlar FU27 onayıyla kesinleşir (A-11) |
| Platform Mongo işlemi (replica set) | var — `PlatformTransactionExecutor.cs`, `IPlatformTransactionSession` | gerekli |
| Denetim kutusu kapısı `CanonicalTransactionalAuditOutboxWriter` | var (`:37-186`); aktör principal'dan (`:156-185`); `SourceService` sabit (`:39`, `:138`); `Outcome` sabit `Succeeded` (`:131`); kayıt Application DI `:329-330` | **eklemeli genişletme** (Bölüm 5) |
| `audit_outbox` tekillik index'i | `ux_audit_outbox_idempotency_key` **küresel tekil** (`…/Persistence/Schema/PlatformSchemaManifest.AccessGovernance.cs:118-120`); işlem içi yolda çakışmada depo `false` döner (`…/Persistence/Repositories/AuditOutboxRepository.cs`, `TryInsertAsync(IPlatformTransactionSession session, AuditOutboxWriteRequest request, …)` — r4, inceleme 03 tur 1 F-3 ile düzeltildi; r3'ün `:159-161` atfı oturumsuz `private TryInsertAsync(AuditOutboxMessage message, …)` yolundaydı) | anahtar istemci anahtarından türetilmez (Bölüm 8.6, r3 F-2); `false` = değişmez ihlali → 503 (Bölüm 8.6, r4 F-3) |
| Platform işlem yürütücüsü `PlatformTransactionExecutor.ExecuteAsync` (`services/Diten.Platform/src/Diten.Platform.Infrastructure/Persistence/PlatformTransactionExecutor.cs`) | gövde yalnız geçici hatada (`TransientTransactionError` etiketi ya da `MongoCommandException` kod 112) yeni oturumla yeniden koşar (`MaximumBodyAttempts = 3`); commit yalnız `UnknownTransactionCommitResult`'ta **aynı oturumda** yenilenir, gövde tekrar koşmaz (`MaximumCommitAttempts = 3`); geçici olmayan her `MongoException` (E11000 dahil) `PlatformTransactionUnavailableException`'a çevrilir; `MongoException` olmayan istisna iptalden sonra olduğu gibi yeniden atılır; test sondası `IPlatformTransactionFaultProbe` (`BeforeCommitAsync` / `AfterCommitAsync`) | değişmez; Bölüm 8.6'daki kurtarma yolları bu davranışa göre yazıldı (r4 F-3) |
| Yeni belirteç kabulü (`platform-islem-ici` iz satırına `RecordCodeAuditIntent.*`) | defterde yok (`tests/architecture/audit-ledger/Diten.Platform.md:8-17`) | **DL-1 main'e girmeden önce CT kararı** (A-10) |
| Kiracı durumu | `GetTenantStatusQuery` — yalnız `TenantStatus.Active` canlı (`…/Features/Tenants/Handlers/GetTenantStatusQueryHandler.cs:18-29`) | gerekli (askıdaki kiracı) |
| Hız sınırlayıcı | `System.Threading.RateLimiting` (çerçeve); `PeopleSearchRateLimit` emsali yalnız okunur, kullanılmaz | gerekli; değer A-16 |
| Belge `OrgPrefix` | `DocumentCodingOptions.OrgPrefix` (`…/DocumentManagementIdentifiersModels.cs:45`, kod varsayılanı `"GMG"`; `appsettings*.json`'da bölüm yok — ölçüldü) | grup önekinin **tek kaynağı** (Bölüm 4.3) |
| MDM kısa kodunun değiştirilebilirliği | bugün değiştirilebilir — güncelleme işleyicisi yeni `Code`'u kırpıp yalnız tekilliğini denetler (`…/LegalEntity/Handlers/CommandHandlers/UpdateLegalEntityHandler.cs:36-37`); §5.4 kilidi yok | ara dönem azaltıcısı bu serviste (Bölüm 8.13); MDM kilidi **A-18** (MDM sahibi Ali + Beste, 2026-11-06) |
| RCS-001 §3 `AGR` satırı + izinli kapsam sütunu | §3 bugün `CTR` (`record-coding-standard.md:70`) | DL-1'i bekletmez: katalog testinde adlı tek istisna; DL-7'de kalkar (A-3) |
| Tüketici kimlik bilgileri (`FinanceTreasury`, `Mdm`) | yok | uçtan uca kabul için gerekli; değerler ortam gizli bilgisi, depoda yok |
| BL-501 ortak iletici | MOD-0178 §8.8: olduğu gibi | **kullanılmaz** — bu komut Platform içindedir |
| **DL-5 bağımlılığı** — RCS-001 §5.4 kısa kod kalıcı kullanım kilidi ve kısa kod önerisi (MDM kodu) | MDM `UpdateLegalEntityHandler` kısa kodu bugün değiştirebiliyor (Bölüm 8.13) | **bu paketin kodu değil** (r4, inceleme 03 tur 1 F-8). Bağlı paket: **MOD-0220** (`execution/domains/master-data-management/module-packs/MOD-0220-legal-entity-foundation.md`, künye `owner: mdm-domain-team`); işin MOD-0220'nin yeni revizyonu mu yoksa bir FU'su mu olacağı **TBD** — kimliği MOD-0220 sahibi DCP-002 ön kontrolüyle verir. Sahip rolü: MDM sahibi (paket kararı: Ali) + Beste (Platform tarafı); karar A-18. DL-1'i bekletmez |
| **DL-7 bağımlılığı** — RCS-001 §3 `CTR` → `AGR` satırı ve tür başına izinli kapsam sütunu | §3 bugün `CTR` | **bu paketin kodu değil** (r4 F-8). Bağlı paket: **yok** — değişiklik `.antigravity/rules/record-coding-standard.md` kural dosyasıdır (korunan yol, AGENTS.md §4, kullanıcı onayı); ayrı paket gerekip gerekmediği **TBD** (CT). Sahip rolü: `.antigravity` sahibi (Ali) + Treasury PR'ı; karar A-3. DL-1'i bekletmez |
| **DL-2 bağımlılığı — MOD-0288 dosyaları (XMC-001)** | ORG / POS oluşturma ve güncelleme MOD-0288'in kodudur (`…/Features/TenantOrganization/**`, `Views/Organization/**`); MOD-0288 künyesi `owner: platform-team` (`MOD-0288-organization-person-position-directory.md`), `status: done` | sahip onayı Bölüm 5 "DL-2" listesine; onaylayan **TBD** (Q-7); DL-2 kodunu bekletir |
| **DL-2 bağımlılığı — tüzel kişilik onay ön kontrolü (TC-3)** | ORG oluşturma bugün yalnız "referenceable" ister (`CreateOrganizationUnitCommandHandler.cs:45-49`; MDM'de `IsReferenceable => OperationalStatus == Active && !IsDeleted`, `services/Diten.MdmService/src/Diten.MdmService.Domain/Entities/LegalEntity.cs:60`); onay alanı ayrıdır (`:50`) ve Platform onu lookup-validation 1.0.0'da göremez (yukarıdaki satır) | ORG / POS süreç içi tüketici TC-3'ü bugün karşılayamaz → **Q-4** (öneri: v1.1'e kadar adlı istisna, FU-RCS-15 v1.1'de herkese uygular) |
| **DL-2 bağımlılığı — kimlik ve izin bağlamı** | ORG / POS yollarında ara katman kiracıyı JWT'den kurar ve `platform_admin`'i reddeder (`services/Diten.Platform.Common/src/Diten.Platform.Common/Tenancy/TenantResolutionMiddleware.cs:314-318`); `IActorPermissionContext` (`…/Application/Contracts/IActorPermissionContext.cs`, kayıt `…/API/Program.cs:141`), `ITenantAuthorizationContext` (`services/Diten.Platform.Common/src/Diten.Platform.Common/Authorization/ITenantAuthorizationContext.cs:3-31`), `Diten.Platform.Common.Observability.ICorrelationContext` var | değişmez; port bunları okur (Bölüm 8.14) |
| **DL-9 bağımlılığı** — arka plan çağıranları için servis kimliği token'ı (P3) | Auth servis kimliği token'ı bugün yalnız `Diten.MDM` hedef kitlesine verilir (`ServiceIdentityTokenAudiencePolicy`) | **bu paketin kodu değil** (r4 F-8). Bağlı paket: **MOD-0033-FU02** Service Identity Token Issuance Foundation (`execution/domains/platform-shared-services/module-packs/MOD-0033-FU02-service-identity-token-issuance-foundation.md`, `service: Diten.AuthService`, `status: review`); Platform kayıt kodu hedef kitlesinin onun revizyonu mu yeni FU'su mu olacağı **TBD** (kimlik uydurulmaz). Sahip rolü: künyedeki `auth-security-owner` / `api-consumer-credential-owner` + Ali (Auth) + Beste (Platform yürütücüsünün P3'ü kabulü). DL-1'i bekletmez |

### 7.1 Tüketici yükümlülükleri (tüketici paketlerine yazılır; bu paketin AC'si değildir)

| # | Yükümlülük | Kim | Neden |
|---|---|---|---|
| TC-1 | Çağrıyı başlatan kullanıcının bearer token'ını iletmek; `X-Tenant-Id` göndermemek | bütün tüketiciler | Bölüm 8.2 |
| TC-2 | 200 dışı her cevap, zaman aşımı ya da port bağlı değilse Create'i yapmamak; kendi sayacını / yedek kodu üretmemek; zaman aşımı, 429 ve 503 sonrası **aynı** `idempotencyKey` ile yeniden denemek — **sınırlı ve geri çekilerek** (r7, F-10: öneri en çok 3 deneme, üstel bekleme; sonra Create başarısız ve kullanıcıya "kayıt kodu şu an alınamıyor"); sonsuz yeniden deneme yok; 403 `LEGAL_ENTITY_READ_PERMISSION_MISSING` ve diğer 4xx yeniden denenmez | bütün tüketiciler | D59/1, RCS-001 §6 |
| TC-3 | **Tüzel kişilik onay ön kontrolü:** MDM lookup-validation v1.1 (`approvalStatus`) yayınlanana kadar şirketin **onaylı** olduğunu kendi Create'inde denetlemek — bu servis o güne kadar yalnız "aktif"i garanti eder (r3, inceleme 02 F-8) | MOD-0126 / 0119 / 0120 / 0124 (D10/1 tablosu zaten yazıyor), MOD-0289, MOD-0217 | Bölüm 7, 8.3 |
| TC-4 | **`BAC` kapsam seçimi çağıranda:** MDM bağdaştırıcısı şirket hesabında `prefixScope = LegalEntity` + `legalEntityId` = hesabın sahibi şirket, tedarikçi hesabında `prefixScope = Group` + `legalEntityId = null` gönderir; tüketici AC'si: iki hesap türü için doğru kapsam ve sahte allocator'la sabotaj (eşleme ters çevrilince kırmızı) | MOD-0289 (MDM bağdaştırıcısı, A-11) | servis hesabın türünü bilmez |
| TC-5 | `BNK` için `prefixScope = Group`; port imzasına `prefixScope` eklemek (v0 portunda yok) | MOD-0289, Treasury `IRecordCodeAllocator` (A-11) | K-4 |
| TC-6 | `reason_code`'u kendi kaynak dosyasıyla 7 dilde çevirmek; `errors[]` metnini göstermemek | bütün tüketiciler | Bölüm 8.9 |
| TC-7 | Kendi Create kaydına `idempotencyKey` ve verilen `code`'u yazmak (iki denetim kaydının eşleşmesi) | bütün tüketiciler | Bölüm 21 |
| TC-8 | 409 `LEGAL_ENTITY_SHORT_CODE_CHANGED`'i kullanıcıya "şirket kısa kodu değişmiş, MDM yöneticisine başvurun" olarak göstermek | bütün LegalEntity tüketicileri | Bölüm 8.13 |

**DL-2 süreç içi tüketici (ORG / POS, r12).** Tüketici bu paketin kendisi olduğundan yükümlülükler bu paketin DL-2 AC'leridir:
TC-1 → port token'ı iletmez, aynı süreçteki doğrulanmış principal'ı okur (Bölüm 8.14); TC-2 → ayırma 200 değilse kayıt yazılmaz,
yedek kod yok (AC-54); TC-3 → **Q-4**; TC-4 / TC-5 → uygulanmaz (yalnız LegalEntity); TC-6 / TC-8 → formlar `reason_code`'u 7 dilde
gösterir (Bölüm 11, AC-60); TC-7 → kayıt kimliği anahtardadır, kod kayıttadır (Bölüm 8.14, AC-59).

**Lookup & Reference Data kararı (module-pack-standard §13.1):** Platform lookup kullanılmaz; yeni Platform lookup
anahtarı yok. Şirket kısa kodu MDM/reference sınırındadır, MDM'den okunur, Platform'da kopyası tutulmaz (ayırma
defterindeki `Prefix` alanı verilmiş kodun parçasıdır, kısa kod tablosu değildir). Hardcoded yedek liste yok.

## 8. Runtime Constraints

### 8.1 Port, rota, servis

- Diten.Platform, port **5057** (AGENTS.md §3). Uç yalnız `/api/internal/*`; Platform `TenantResolutionMiddleware` bu yolu
  atlar (`InternalAuditController.cs:13`) — kiracıyı yürütücü kurar (token), kiracı durumunu işleyici denetler (r7, Bölüm 8.5 adım → katman tablosu).
- Gövde ≤ **4 KB** → aşarsa 413 `RECORD_CODE_REQUEST_TOO_LARGE`.
- Zaman bütçesi: MDM okuması **3 s** — emsal `WorkingCalendarLegalEntityValidator.TotalTimeout = TimeSpan.FromSeconds(3)`
  (`services/Diten.Platform/src/Diten.Platform.Infrastructure/Services/Mdm/WorkingCalendarLegalEntityValidator.cs`).
  r3 bunu yanlış sınıfa bağlıyordu (r4, inceleme 03 tur 1 F-10): `MdmLegalEntityReferenceValidator.TotalTimeout` **5 s**'dir ve bu
  özellikte örnek alınmaz (yalnız sınıf yorumu WorkingCalendar emsalini anar). İsteğin tamamı **5 s**; dolarsa 503. Sözleşme
  503 der; emsallerin 504'ü kullanılmaz.

### 8.2 İç uç kimliği — P2 (K-8r, K-12 e) ve kimlik bilgisi sızmasına karşı azaltıcılar (F-4)

Ölçülen üç desen: P1 ortak sır `X-Internal-Api-Key` (`InternalAuditController.cs:24, 92-111`) kiracıyı **gövdeden** alır
(`:56-70`) → uymaz. P3 RS256 servis kimliği token'ı (`TrustedServiceTokenValidationExtensions.cs:16-23`,
`TrustedSourceAuditIntentServiceIdentity.cs:32-55`) Auth'ta yalnız `Diten.MDM` için verilir
(`services/Diten.AuthService/src/Diten.AuthService.Application/Features/ServiceIdentityTokens/ServiceIdentityTokenAudiencePolicy.cs:5-17`)
ve kullanıcı token'ı taşımaz → DL-9. **Seçim P2** (MOD-0018-FU21 deseni; `TrustedLegalEntityScopeRequestExecutor.cs:12-92`,
`TrustedLegalEntityScopeJwtContext.cs:11-41`, `TrustedLegalEntityScopeCredentialAuthenticator.cs:58-95`).

**Başlıklar (her biri tam bir kez):** `X-Record-Code-Credential-Id`, `X-Record-Code-Credential`, `X-Record-Code-Audience`
(`TRUSTED_RECORD_CODE_ALLOCATE`), `Authorization: Bearer <kullanıcı JWT'si>`.

**401 / 403 eşlemesi — FU21 ile birebir:**

| Durum | FU21 kaynağı | Cevap |
|---|---|---|
| Üç kimlik bilgisi başlığından biri yok ya da birden çok | `TrustedLegalEntityScopeRequestExecutor.cs:36-41` | 401 `RECORD_CODE_UNAUTHENTICATED` |
| Kimlik bilgisi yapılandırması geçersiz (boş, tekrarlı, katalog dışı tür, kaynak servis kümesi dışı ad) | `…CredentialAuthenticator.cs:63-66` (`Denied`) | 403 `RECORD_CODE_FORBIDDEN` |
| Kimlik bilgisi **iptal** (`IsRevoked`), tanımlayıcı boş/bilinmiyor, sır boş ya da yanlış (döndürme penceresi dışı) | `:68-73`, `:83-94` (`Unauthenticated`) | **401** `RECORD_CODE_UNAUTHENTICATED` |
| Bağlamanın `ConsumerService` / `AllowedAudience`'ı ya da hedef kitle başlığı uyuşmuyor | `:76-80` (`Denied`) | 403 `RECORD_CODE_FORBIDDEN` |
| `X-Tenant-Id` başlığı var | `TrustedLegalEntityScopeJwtContext.cs:11-16` (`Forbidden` = kimliği doğrulanmış sayılır, `ITrustedLegalEntityScopeJwtContext.cs:20-21`) | 403 `RECORD_CODE_FORBIDDEN` |
| Kullanıcı token'ı yok / geçersiz / süresi dolmuş | `:18-22` | 401 `RECORD_CODE_UNAUTHENTICATED` |
| Token'da tam bir `tenant_id` / `sub` yok, Guid değil, boş, ya da `actor_type ≠ tenant_user` (`platform_admin` ve `partner_admin` token'ı dahil — AC-44, r4 F-4; platform aktöründe ret `Denied` yazılır — r7, K-13) | `:25-38` | 403 `RECORD_CODE_FORBIDDEN` |
| Hız sınırı aşıldı | yeni (r3 F-5) | 429 `RECORD_CODE_RATE_LIMITED` |
| Kiracı aktif değil (askıda, sağlanıyor, devre dışı, yok) | yeni (inceleme 01 F-12) | 403 `RECORD_CODE_TENANT_NOT_ACTIVE` |
| Tür bu kimlik bilgisinin izinli kümesinde değil | FU21 `AllowsPair` emsali `:68-72` | 403 `RECORD_CODE_FORBIDDEN` |
| Kullanıcıda türün gerekli izni yok | FU21 `HasExactPermission` emsali `:68-72` | 403 `RECORD_CODE_PERMISSION_MISSING` |

**Kiracı, kullanıcı ve denetim aktörü (K-8r; r3 inceleme 02 F-6).** Kiracı = doğrulanmış token'ın `tenant_id`'si;
`TenantScope.Begin(_tenantContext, jwtTenant)` (`TrustedLegalEntityScopeRequestExecutor.cs:74` deseni). Gövdede
`tenantId` üyesi 400; sorgu dizesi okunmaz. Yürütücü kullanıcı token'ını FU21'deki gibi **açık**
`HttpContext.AuthenticateAsync(JwtBearerDefaults.AuthenticationScheme)` ile doğrular (`TrustedLegalEntityScopeJwtContext.cs:18`)
ve `HttpContext.User`'ı doğrulanmış principal'a ayarlar; bu, MediatR işleyicisi ve denetim kapısının
`ITenantAuthorizationContext`'i çözülmeden **önce** olur (o bağlam principal'ı kurucusunda okur —
`…/Infrastructure/Authorization/JwtTenantAuthorizationContext.cs:24`; emsal: iş akışı yürütücüsü principal'ı dispatch'ten
önce ayarlıyor, `TrustedWorkflowConsumerRequestExecutor.cs:126-138`). Aktörün gerçekten `TenantUser` + `sub` çıktığı
DL-1'in **ilk commit grubunda** gerçek HTTP ana makinesinde üretim yazıcısıyla ölçülür (AC-41); ölçüm başarısızsa DL-1 durur ve
paket revizyonuna döner.

**Kaynak servis kapalı listesi (K-9; r3 inceleme 02 F-4).** Kimlik bilgisi bağlaması → servis adı:

| Bağlama | `ConsumerService` | Denetimdeki `SourceService` / defterdeki `CallerService` | İzinli türler |
|---|---|---|---|
| `FinanceTreasury` | `DITENFINANCETREASURYSERVICE` | `Diten.FinanceTreasuryService` | `PAY`, `APD`, `ARI`, `CPS`, `CTP` |
| `Mdm` | `DITENMDMSERVICE` | `Diten.MdmService` | `BNK`, `BAC`, `AGR` |

Eşleme **kodda** sabittir; yapılandırma yalnız sırları ve izinli türleri taşır (tablonun alt kümesi olmak zorunda, değilse
yapılandırma geçersiz → bütün istekler 403). Eşlemedeki her ad, denetim kapısının izinli kaynak servis kümesinde
(`TransactionOwnedAuditSourceServices`, Bölüm 5) olmak zorundadır — bunu bir test sabitler. Kapı küme dışı bir adı
istisnayla reddeder; böylece başka bir Platform çağıranı da başka bir servis adına kayıt yazamaz. Gövdede ya da başlıkta
gelen bir servis adı okunmaz; gövdede `sourceService` üyesi 400.

**Sızma senaryosu (inceleme 01 F-4).** Bir kimlik bilgisi sırrı sızarsa saldırganın ayrıca o kiracıda, türün oluşturma
iznini taşıyan geçerli bir kullanıcı token'ına ihtiyacı vardır. Yapabileceği: o kiracıda, o bağlamanın izinli türleri için
numara harcamak. Yapamayacağı: veri okumak, başka kiracıya ya da başka türe kod üretmek, var olan kodu değiştirmek.
Azaltıcılar:

1. Tüketici başına izinli tür kümesi — tür dışı → 403.
2. Kullanıcıda türün oluşturma izni (Bölüm 4.4 son sütun) — yoksa 403 `RECORD_CODE_PERMISSION_MISSING`.
3. **Hız sınırı (r3 F-5):** yürütücü içinde, kimlik bilgisi ve token doğrulandıktan **sonra**, (tüketici servis adı,
   token kiracısı) bölümlü sabit pencere (`System.Threading.RateLimiting.PartitionedRateLimiter`, kuyruk 0). Bir kiracıdaki
   taşkın başka kiracının kovasını boşaltmaz. Aşımda yürütücü 429 `RECORD_CODE_RATE_LIMITED`'i kendi ret yolundan yazar
   (`Response<T>` zarfı); ASP.NET `OnRejected` kancası kullanılmaz. Ret denetime yazılmaz — AUD-001 §3'ten adlı sapma, K-10 / K-13 (Bölüm 21.2); yerine uyarı logu ve `record_codes_rate_limited_total{reason="rate_limited"}` sayacı (etiket kümesi K-10). **Değer açık karar
   A-16** (CT önerisi 600 istek/dakika/bölüm).
4. Her ayırma denetimde kullanıcı + kaynak servisle görünür (tespit).
5. Döndürme ve iptal (FU-RCS-13): yeni `ActiveSecret`, eskisi `PreviousSecret` + `PreviousValidUntilUtc` (en çok 24 saat
   önerisi, A-16); acil durumda `IsRevoked = true` → anında 401.

**Sınır (kabul):** arka plan işi (kullanıcı token'ı yok) çağıramaz → 401. v1 tüketicileri kullanıcı başlatmalı:
MOD-0124 "Arka plan işi yok" (`MOD-0124-cash-positioning.md:1488`), MOD-0178 S1 görev kaynağı elle
(`MOD-0178-putaway-picking-packing.md:506-514`). İleride → DL-9.

**Yeni yapılandırma (değer yok):** bölüm `TrustedRecordCodeCredential`, bağlama başına `Identifier`, `ConsumerService`,
`AllowedAudience`, `ActiveSecret`, `PreviousSecret`, `PreviousValidUntilUtc`, `IsRevoked`, `AllowedRecordTypes`; hız
sınırı `RecordCodes:RateLimit:PermitsPerMinute`. Sırlar ortam değişkeni / gizli bilgi deposundan (MOD-0012).

### 8.3 Şirket kısa kodu ve uygunluk (K-5, F-9)

- `MdmLegalEntityShortCodeReader`: `GET {MdmService:BaseUrl}/api/legal-entities/{id:D}/lookup-validation`; `X-Tenant-Id`
  = token kiracısı; `Authorization` = iletilen kullanıcı token'ı (`MdmLegalEntityReferenceValidator.cs:104-109, 142-152`);
  tenancy istemcide (`:19-27` uyarısı).
- **Uygunluk (r3, inceleme 02 F-8 ile düzeltildi).** Hedef kural: kod yalnız **aktif VE onaylı** tüzel kişiliğe verilir.
  Bugün MDM main'deki lookup-validation **1.0.0**'dır ve `approvalStatus` alanı yoktur (`LegalEntityModels.cs:100-106`);
  v1.1 taslaktır. Bu yüzden:
  - **v1.1 yayınlanana kadar (DL-1):** servis yalnız **aktiflik** denetler (MDM 404 → 404); onay denetimi **tüketicinin
    zorunlu ön kontrolüdür** (TC-3). Yanıtta `approvalStatus` görünürse yine tabloya göre değerlendirilir (aşağıda).
  - **v1.1 yayınlandığında:** v1.1 kuralı geçerlidir — alan **yoksa** ya da tanımsız değerdeyse yanıt geçersizdir → 503
    (`legal-entity-lookup-validation-v1.1-proposal.md:69`). Bu, bu serviste bir **kod değişikliğidir** (okuyucunun "alan yok"
    dalı 503'e çevrilir); kendiliğinden olmaz. Açık bağımlılık: **MOD-0220 sahibi** v1.1'i yayınlar; Beste aynı sürümde
    FU-RCS-15'i teslim eder; TC-3 o zaman kalkar.

| MDM yanıtı | DL-1 (v1.1 öncesi) cevap |
|---|---|
| 200 tutarlı (`legalEntityId` eşit, `referenceable = true`, `lifecycleState = ACTIVE`), `code` `^[A-Z]{2,5}$`, `approvalStatus` yok | ayırma sürer (onay: TC-3) |
| aynı, `approvalStatus = APPROVED` | ayırma sürer |
| aynı, `approvalStatus ∈ {DRAFT, SUBMITTED, REJECTED}` | 422 `LEGAL_ENTITY_NOT_ELIGIBLE` |
| aynı, `approvalStatus` boş / `null` / dört değer dışı | 503 `LEGAL_ENTITY_SOURCE_UNAVAILABLE` |
| aynı, `code` kalıba birebir uymuyor (normalleştirme yok) | 409 `LEGAL_ENTITY_SHORT_CODE_MISSING` |
| 404 | 404 `LEGAL_ENTITY_NOT_FOUND` (tek gövde) |
| 200 ama `data` yok / ayrıştırılamaz / `legalEntityId` farklı / `referenceable = false` / `lifecycleState ≠ ACTIVE` | 503 `LEGAL_ENTITY_SOURCE_UNAVAILABLE` |
| 403 (iletilen kullanıcı token'ında `mdm.legal-entities.read` yok) | **403 `LEGAL_ENTITY_READ_PERMISSION_MISSING`** — kalıcı, tüketici yeniden denemez; `Denied` ret kaydı (r7, son bakış 01 F-10; sözleşmeye ekleme) |
| 400, 401, 5xx, 3 s aşımı, bağlantı yok | 503 `LEGAL_ENTITY_SOURCE_UNAVAILABLE` (400: Platform istemcisinin hatası; 401: iletilen token MDM'de geçmedi — ikisi de geçici değil olabilir, bu yüzden TC-2 yeniden denemeyi sınırlar) |

- Her yeni ayırmada okunur; önbellek yok. İdempotent tekrar MDM'i çağırmaz.

### 8.4 Biçim ve sayaç

- Kod `{ÖNEK}-{TÜR}-{SAYAÇ}`; `SAYAÇ` en az 6 hane sıfır dolgulu, `999999`'dan sonra `1000000`, kesilmez (§5.7). `long`.
- Sayaç: `findOneAndUpdate` + `$inc LastNumber 1`, `upsert`, `ReturnDocument.After`, filtre `(TenantId == token kiracısı,
  Prefix, RecordType)` — **işlem dışında** (MDM `CodeReservationRepository.cs:549-600` deseni). İlk yaratma yarışında
  E11000 → upsert'süz yeniden dener (MDM `:579`, en çok 3 deneme `:16`). ⚠ Doküman sayacı
  (`DocumentManagementIdentifierRepositories.cs:58-90`) bu kurtarmayı yapmıyor — örnek alınmaz (FU-RCS-12).
- Boşluk kabul (§5.8); ardışıklık garanti değildir, tekillik garantidir; `(TenantId, Code)` unique ikinci savunma.

### 8.5 Değerlendirme sırası

1. Boyut → 413. 2. Kimlik bilgisi + kullanıcı token'ı + `X-Tenant-Id` → 401 / 403 (Bölüm 8.2 tablosu). 3. Hız sınırı
(tüketici servis, kiracı) → 429 `RECORD_CODE_RATE_LIMITED`. 4. Kiracı aktif mi → 403 `RECORD_CODE_TENANT_NOT_ACTIVE`.
5. Gövde → 400 `RECORD_CODE_REQUEST_INVALID`. 6. Tür katalogda mı → 422 `RECORD_TYPE_UNKNOWN`. 7. Tür bu kimlik bilgisine
izinli mi → 403 `RECORD_CODE_FORBIDDEN`. 8. Kullanıcıda türün izni var mı → 403 `RECORD_CODE_PERMISSION_MISSING`.
9. **İdempotency** `(TenantId, CallerService, IdempotencyKey)`: kayıt varsa `RecordType`, varsayılan uygulanmış
`PrefixScope` ve `LegalEntityId` alan alan eşit (Bölüm 4.2) → **200 aynı kod + sequence**
(sayaç, MDM, yeni denetim yok); değil → 400 `IDEMPOTENCY_KEY_REUSED`. 10. Kapsam kuralları → 422 `RECORD_CODE_SCOPE_INVALID`
(DL-1'de `Platform` bu adımda reddedilir). 11. Önek: Group → kiracı ayarı (yoksa 409 `GROUP_PREFIX_MISSING`; etkin belge
`OrgPrefix`'ten farklıysa 409 `GROUP_PREFIX_MISMATCH`); LegalEntity → MDM (Bölüm 8.3), ardından kısa kod kullanım
denetimi (Bölüm 8.13; 409 `LEGAL_ENTITY_SHORT_CODE_CHANGED`). 12. Sayaç `$inc` (depo hatası → 503 `RECORD_CODE_UNAVAILABLE`).
13. **Tek işlem** (Bölüm 8.6; gövdenin ilk işi anahtarın oturumlu yeniden okunması) → 200; kilit filtresi eşleşmezse 409
`GROUP_PREFIX_MISMATCH`, işlem / bütçe tükenirse 503 `RECORD_CODE_UNAVAILABLE` (r9, son bakış 01 tur 3 F-1).

**Adım → katman (r7, son bakış 01 F-2).** Her adım tek bir katmanda karar verir; ret kaydını o katman yazar.

| Adım | Karar veren | Komuta / işleyiciye geçen | Ret kaydı (Bölüm 21.2) — kim yazar |
|---|---|---|---|
| 1 boyut (413) | API: `TrustedRecordCodeRequestExecutor` | — | yazılmaz |
| 2 kimlik bilgisi, kullanıcı token'ı, `X-Tenant-Id` (401 / 403) | API: yürütücü + `TrustedRecordCodeCredentialAuthenticator` + `TrustedRecordCodeJwtContext` | `CallerService` (kapalı listedeki ad), izinli tür kümesi, token kiracısı, `sub` | yazılmaz — **tek istisna** `actor_type ∈ {platform_admin, partner_admin}` olan geçerli token: yürütücü `HttpContext.User`'ı ayarladıktan **sonra**, **satırı yazmadan önce hız sınırlayıcıdan izin ister** (bölüm anahtarı **ayrı önekli**: `platform-actor:{tüketici servis}:{tenant_id | platform}` — kiracının `tenant_user` ayırma kovasıyla (`{tüketici servis}:{kiracı}`) asla paylaşılmaz, retler kiracının Create'lerini 429'a düşüremez; r8 tur 5 F-2, r9 inceleme 03 tur 6 F-1 / son bakış 01 tur 3 F-4. Token'daki `tenant_id` yoksa, Guid değilse ya da `Guid.Empty` ise anahtar `platform` ve kayıtta `TargetTenantId = null`): izin varsa ret yazıcısını `HttpContext.RequestServices`'ten çözer ve `Denied` yazar (K-13, AC-44); izin yoksa satır **yazılmaz**, uyarı logu + `record_codes_rate_limited_total{reason="platform_actor_refusal"}` — 429 ile aynı adlı sapma (K-10 / K-13 taşkın gerekçesi). Cevap her iki durumda **403 `RECORD_CODE_FORBIDDEN`** kalır (adım sırası değişmez) |
| 3 hız sınırı (429) | API: yürütücü içi `RecordCodeAllocateRateLimiter` | — | **yazılmaz — adlı sapma (K-10 / K-13)**; uyarı logu + `record_codes_rate_limited_total{reason="rate_limited"}` |
| 4 kiracı aktif mi (403) | **işleyici** (`AllocateRecordCodeHandler`): `ITenantRegistryRepository.GetByIdAsync` + `Status == Active` (`GetTenantStatusQueryHandler` ile aynı kural; işleyici içinden MediatR sorgusu gönderilmez) | — | işleyici, `Denied` |
| 5 gövde (400) | **ayrıştırma** API'de (`TrustedRecordCodeAllocateRequestParser`; ham gövde okunur, model bağlama ve doğrulayıcı yok); **ret işleyicide** | ayrıştırma sonucu: ya `ParsedRequest` ya `RequestInvalid(errors)` | işleyici, `Failed` (K-13) |
| 6 katalog (422) | işleyici (`RecordCodeTypeCatalog`, Domain) | — | işleyici, `Failed` |
| 7 tür kimlik bilgisine izinli mi (403) | işleyici (yürütücünün geçirdiği izinli tür kümesiyle) | — | işleyici, `Denied` |
| 8 kullanıcı izni (403) | **değerlendirme** API'de: yürütücü, ayrıştırılan `recordType`'ın Bölüm 4.4 iznini **`HasExactPermission`** ile ölçer — FU21 emsali (`…/API/Security/ITrustedLegalEntityScopeJwtContext.cs`, `PermissionKeys.Contains`; `permission` iddiaları `StringComparer.Ordinal` küme, `TrustedLegalEntityScopeJwtContext`): yalnız `permission` iddia türü, birebir ve büyük/küçük harfe duyarlı, iddia değeri bölünmez, takma ad yok. `PermissionClaimEvaluator.Evaluate` **kullanılmaz** (r8, son bakış 01 tur 2 F-4: o, büyük/küçük harf duyarsız eşler, iddia değerini boşluk / virgül / noktalı virgülle böler, `permissions` iddiasını da okur ve `PermissionAliasResolver` takma adlarını kabul eder; servisler arası güvenilir yolda kabul edilen biçim tek ve birebir olmalıdır — Bölüm 8.2 tablosundaki FU21 eşlemesiyle aynı); **ret** işleyicide | `HasRequiredPermission` (bool; tür katalog dışıysa `false`, adım 6 önce reddeder) | işleyici, `Denied` |
| 9–13 | işleyici | — | işleyici, Bölüm 21.2 |

Sonuçları: (a) **Ret yazıcısını çağıranlar** iki yerdir — `AllocateRecordCodeHandler` (adım 4–13) ve yürütücü (yalnız
platform aktörü reddi); ikisi de `RecordCodeAuditIntent.EnqueueRefusalAsync`'i çağırır, bu yüzden `RecordCodeAuditIntent`
`public static`'tir (API katmanından erişilir) ve A-10 belirteci aynıdır. (b) **DI kapsamı:** `ITransactionalAuditRefusalWriter`
Application DI'da `AddScoped` (Bölüm 5); işleyici onu kurucusunda alır, yürütücü ise `HttpContext.User` ayarlandıktan sonra
istek kapsamından çözer — `JwtTenantAuthorizationContext` principal'ı kurucusunda okuduğu için daha önce çözülen bir örnek
kimliksiz kalırdı (AC-41 bunu ölçer). (c) **Ret testlerinin sınıfı:** adım 1–3 ve platform aktörü reddi
`TrustedRecordCodeRequestExecutorRefusalTests`'te (işleyici çağrılmaz; platform aktörü satırı gerçek Mongo ile
`RecordCodeAuditDeliveryMongoTests`'te); adım 4–13 kararları `AllocateRecordCodeHandlerTests`'te, yazılan satırlar
`RecordCodeAuditDeliveryMongoTests`'te. (d) Bu özellikte **FluentValidation doğrulayıcısı yoktur** (Bölüm 5, 12; AC-45):
`ValidationBehavior` (`…/Application/Contracts/Behaviors/ValidationBehavior.cs`) işleyiciden ve `AuditBehavior`'dan önce
koşar ve yalnız `ValidationException` atar; `GlobalExceptionHandler` (`…/API/Middleware/GlobalExceptionHandler.cs`) onu
`ProblemDetails`'e çevirir — `Response<T>` zarfı yoktur ve ret denetime yazılmaz.

Yetki (7–8) idempotency'den (9) önce gelir: izni olmayan kullanıcı başkasının anahtarını tekrarlayarak kod okuyamaz.
**Tür izni (7) kapsamdan (10) önce gelir:** izinli çağıranı olmayan bir tür (`ORG`, `POS`, `PLN`) hangi kapsamla
istenirse istensin 403 alır; ör. `PLN` + `Platform` → 403 `RECORD_CODE_FORBIDDEN`, `PAY` + `Platform` → 422
`RECORD_CODE_SCOPE_INVALID` (r3, inceleme 02 F-9).

### 8.6 İşlem ve eşzamanlılık (F-6)

- Tek işlem: defter satırı ekle (yeni `allocationId`) + (Group ise) grup öneki ayarını kilitle (`LockedAtUtc` boşsa yaz;
  filtre `GroupPrefix == kullanılan önek`) + denetim kutusu satırı.
- **Kilit filtresi eşleşmezse — tek sonuç 409 `GROUP_PREFIX_MISMATCH` (r8, son bakış 01 tur 2 F-1; r7'nin yeniden
  değerlendirme dalı kaldırıldı).** Ölçüm: etkin `OrgPrefix` bu süreçte sabittir (`IOptions<DocumentCodingOptions>.Value`,
  Bölüm 8.12) ve adım 11 her `Group` ayırmasında kiracı ayarını bu değerle karşılaştırır (eşit değilse 409). Hizalama ucu
  ayarı yalnız **çağrıldığı sürecin** etkin `OrgPrefix`'ine yazar. Aynı süreçteki eşzamanlı bir hizalama aynı değeri yazar →
  filtre eşleşir. Filtre ancak ayar adım 11'den sonra **başka bir süreçte, farklı bir `OrgPrefix` ile** yeniden hizalandıysa
  eşleşmez (çok örnekli dağıtımda yeniden başlatma sürerken). O anda ayarın yeni değeri bu sürecin etkin `OrgPrefix`'inden
  farklıdır; bu, adım 11'in 409 `GROUP_PREFIX_MISMATCH` dediği durumun **aynısıdır**. r7'deki "adım 11'den yeniden
  değerlendir, eşitse yeni önekin sayacından numara al" dalı üretimde oluşamaz (yeniden okuma her zaman farklı değeri görür)
  ve kaldırıldı. Kural: gövde `RecordCodeGroupPrefixChangedException` (MongoException değil) atar → yürütücü iptal edip
  yeniden atar ("eşleşmez" tanımı: `LockAsync` `MatchedCount == 0`, Bölüm 3) → işleyici yakalar → **409 `GROUP_PREFIX_MISMATCH`** (`Response<T>` zarfı), `Failed` ret kaydı (`Prefix` =
  bu sürecin kullandığı önek), **hata** logu. 503 seçilmedi: durum geçici olsa da (yeniden başlatma bitince düzelir)
  yapılandırma tutarsızlığıdır, aynı durum adım 11'de zaten 409'dur; aynı duruma iki kod vermek tek kural ilkesini bozar ve
  tüketiciyi yeniden deneme döngüsüne sokar (TC-2 409'u yeniden denemez). **Harcanan sayaç numarası:** adım 12'de alınan tek
  numara boşluk kalır (§5.8). AC-46.
- **Denetim kutusu anahtarı (r3, inceleme 02 F-2).** `audit_outbox.IdempotencyKey` küresel tekildir
  (`PlatformSchemaManifest.AccessGovernance.cs:118-120`); istemcinin `idempotencyKey`'i **kullanılmaz** (iki kiracı aynı
  istemci anahtarını kullanabilir; önce ret kaydı yazılmış bir anahtarla gelen başarılı ayırma çakışırdı). Emsal
  `PhysicalEntitlementAuditIntent.cs:29` (`physical-entitlement:{requestType}:{intentId:N}`):
  - başarılı ayırma: `record-code:allocated:{allocationId:N}` — `allocationId` defter satırının kimliğidir, her ayırma
    denemesinde yeni Guid;
  - ret kaydı: `record-code:refused:{Guid.NewGuid():N}` — her ret için yeni Guid.
- **Yürütücü gerçeği (r4, inceleme 03 tur 1 F-3; Bölüm 7 satırı).** `PlatformTransactionExecutor.ExecuteAsync` gövdeyi yalnız
  geçici hatada yeni oturumla yeniden koşar, commit'i yalnız `UnknownTransactionCommitResult`'ta aynı oturumda yeniler
  (gövde tekrar koşmaz) ve geçici olmayan her `MongoException`'ı — E11000 dahil — `PlatformTransactionUnavailableException`'a
  çevirir. Bütün kurtarma yolları buna göre yazılır:
- **Cevaba eşleme (r7, ölçüldü; r8 son bakış 01 tur 2 F-5 / inceleme 03 tur 5 F-1 ile tamamlandı).** İstisna yolu iki
  katmandan geçer. (1) MediatR `ExceptionBehavior` (`…/Application/Contracts/Behaviors/ExceptionBehavior.cs`):
  `TransactionOwnedAuditRefusedException`'ı ve `PlatformAdministratorRecordUnavailableException`'ı **loglamadan**, türüyle
  olduğu gibi yeniden atar — `TransactionOwnedAuditIntentInvalidException` `TransactionOwnedAuditRefusedException`'dan
  türediği için (`CanonicalTransactionalAuditOutboxWriter.cs`) aynı `catch` onu da kapsar; diğer her istisnayı **hata
  düzeyinde loglar** (r9, son bakış 01 tur 3 F-6 ile düzeltildi) ve onlar için `Response<T>.Fail(string, int)` arar — gerçek imza dört parametreli
  olduğundan (`…/Application/Common/Response.cs`) arama boş döner ve istisna yine yeniden atılır (bugünkü davranış; aramanın bir
  gün tutması `InvalidOperationException`'ı 400 zarfına çevirirdi). (2) `GlobalExceptionHandler`
  (`services/Diten.Platform/src/Diten.Platform.API/Middleware/GlobalExceptionHandler.cs`): `ValidationException` → 400
  `ProblemDetails`; `TransactionOwnedAuditIntentInvalidException` → **500** `AUDIT_INTENT_INVALID`;
  `TransactionOwnedAuditRefusedException` → **503** `AUDIT_RECORD_UNAVAILABLE` (ikisi de `InvalidOperationException` alt
  sınıfıdır, `CanonicalTransactionalAuditOutboxWriter.cs`; kollar `InvalidOperationException` kolundan önce gelir);
  `PlatformAdministratorRecordUnavailableException` → 503; diğer `InvalidOperationException` → **400** "Application Error" ve
  iç cümle; `PlatformTransactionUnavailableException` (`Exception`'dan türer) ve tanınmayan her istisna → **500** "Server Error".
  **Seçim — bu özelliğin kendi istisnaları zarfa çevrilir, denetim kapısının iki istisnası çevrilmez:**
  - İşleyiciler (`AllocateRecordCodeHandler`, `AlignRecordCodeGroupPrefixHandler`) yürütücüyü bir `try` içinde çağırır:
    `PlatformTransactionUnavailableException` → zarfta **503 `RECORD_CODE_UNAVAILABLE`** (+ `Failed` ret kaydı en iyi çaba);
    `RecordCodeIdempotencyConflictException`, `RecordCodeGroupPrefixChangedException`, `RecordCodeAuditOutboxInvariantException`
    → Bölüm 8.6'daki zarflı cevaplar. Bu özelliğin kendi kodu `InvalidOperationException` **atmaz**.
  - **Başarı yolunda** kapının `TransactionOwnedAuditRefusedException` (aktör adlandırılamadı) ve
    `TransactionOwnedAuditIntentInvalidException` (geçersiz niyet — programlama hatası) istisnaları **yakalanmaz**; işlem iptal
    olur (iş verisi ve kod yok) ve Platform'un ortak kuralı uygulanır: 503 `AUDIT_RECORD_UNAVAILABLE` / 500
    `AUDIT_INTENT_INVALID`, ikisi de **`ProblemDetails`**. Gerekçe: `ExceptionBehavior` yorumu bunu Platform genelinde bilinçli
    kılar ("a refused audit record is NEVER turned into a response here: it must reach the API's exception handler as itself");
    bu özellik o kuralı yerel bir zarfla değiştirmez. Sözleşme §4 bu iki satırı `ProblemDetails` istisnası olarak adlandırır;
    tüketici 503'ü aynı anahtarla, sınırlı olarak yeniden dener (TC-2), 500'ü denemez.
  - **Ret yolunda** (K-10, Tablo B / D) ret yazıcısının bu iki istisnası ve `PlatformTransactionUnavailableException`
    **yakalanır**, uyarı düzeyinde loglanır ve cevap değişmez: ret kaydı en iyi çabadır, reddin kendi cevabını bozamaz.
  Depo kiracı yüklemi ihlali (Bölüm 4) `InvalidOperationException` değil **`RecordCodeTenantPredicateViolationException :
  Exception`** atar → yakalanmaz, programlama hatası, 500 "Server Error", iç cümle gönderilmez (AC-30). Bunları AC-47 ölçer.
- **`allocationId` her gövde denemesinde yeni Guid'dir** ve kutu anahtarı ondan türer. İptal edilen bir deneme hiçbir şey
  bırakmaz; commit yenilemesi aynı satırları yeniden yazmaz. Bu yüzden işlem içi `TryInsertAsync` `false`
  (`AuditOutboxRepository.TryInsertAsync(IPlatformTransactionSession …)`, E11000 → `false`) üretim yürütücüsüyle
  **erişilemez bir değişmez ihlalidir** (yalnız başka bir yazarın aynı anahtarı kullanmasında olur). r3'ün "kutu `false` →
  defteri `allocationId` ile oku → 200" kurtarması **kaldırıldı**: `false` → gövde `RecordCodeAuditOutboxInvariantException`
  atar (emsal `PhysicalEntitlementAuditIntent.EnqueueAsync`'in `!inserted` dalı `InvalidOperationException` atar — o, ölçülen
  eşlemede 400 olurdu, bu yüzden örnek alınmaz), işlem iptal, işleyici onu yakalar → 503 `RECORD_CODE_UNAVAILABLE` + **hata**
  logu, defter satırı yok, kod yok. Ret yolunda `false` → uyarı logu, cevap değişmez.
- **Commit sonucu bilinmiyor:** yürütücü commit'i en çok 3 kez yeniler; biri başarılıysa 200. Yenilemeler tükenirse 503
  `RECORD_CODE_UNAVAILABLE` — işlem commit olmuş **ya da** olmamış olabilir (ortak bulgu §5). Tüketici **aynı**
  `idempotencyKey` ile yeniden dener (TC-2): commit olmuşsa adım 9 defter satırını bulur → 200 aynı kod; olmamışsa bu
  istek bir tekrar değil **ilk yazımdır** → yeni ayırma, yeni kod (önceki denemenin aldığı sayaç numarası boşluk kalır).
- **Aynı istemci anahtarı yarışı — dört ayrı durum** (ortak bulgu §3 "her durum ayrı yazılır"; 4. durum r5):
  1. *İlk okumada bulunan tekrar:* adım 9 (`FindByIdempotencyKeyAsync`, oturumsuz) kazananın commit olmuş satırını bulur →
     alan alan eşitse 200 kazananın kodu, değilse 400; sayaç, MDM, defter, denetim yazımı yok.
  2. *İşlem içi örtüşme (kazanan henüz commit olmamış):* iki işlem aynı unique anahtara yazınca MongoDB geç gelene
     `WriteConflict` döner (çıkarım: sürücü/sunucu davranışı, bu repoda ölçülmedi — test bunu ölçer) → yürütücü gövdeyi yeni
     oturumla yeniden koşar. **Gövdenin ilk işi** `FindByIdempotencyKeyAsync(session, …)` ile anahtarı yeniden okumaktır;
     kazanan o arada commit olduysa satır bulunur → gövde defter/denetim yazmadan kazananın kodunu döner (eşitse 200, değilse
     400). Yürütücü denemeler arasında beklemez; kazananın commit'i üç denemeden uzun sürerse gövde denemeleri tükenir →
     503 `RECORD_CODE_UNAVAILABLE`, tüketici aynı anahtarla yeniden dener ve durum 1'e düşer (200 aynı kod). Bu, kabul
     edilen bir sınırdır; yürütücüye bekleme eklenmez (paylaşılan altyapı, bu paketin kapsamı dışı).
  3. *Geç gelen (kazanan, bu isteğin adım 9 okumasından sonra ama işlem gövdesi başlamadan önce commit oldu):* r5'te
     düzeltildi (inceleme 03 tur 2 F-2): gövdenin ilk işi olan oturumlu yeniden okuma kazananı **bulur** → gövde defter ve
     denetim yazmadan kazananın kodunu döner (eşitse 200, değilse 400). Bu durumda defter eklemesi hiç koşmaz, **E11000
     alınmaz**; r4'ün "geç gelen E11000 alır" yazımı yanlıştı.
  4. *Savunma — yeniden okuma kazananı göremezse E11000 (r5 F-2):* üretim akışında gövdenin yeniden okuması kazananı
     bulduğu için bu dal beklenmez (durum 2 ve 3 onu karşılar); bir kodlama hatası ya da gelecekteki bir değişiklik yeniden
     okumayı atlatırsa defter eklemesi E11000 alır. Depo bunu `MongoException` olarak sızdırmaz:
     `ux_record_code_allocations_tenant_caller_key` E11000'ini `RecordCodeIdempotencyConflictException`'a çevirir (iç
     istisna olarak `MongoWriteException` taşır; MDM `CodeReservationRepository` "E11000'de kazananı oku" deseni); yürütücü bu
     istisnayı 503'e çevirmeden iptal edip yeniden atar (`catch` → `AbortIfActiveAsync` → `throw`). İşleyici onu yürütücünün
     dışında yakalar, kazananı oturumsuz okur → eşitse 200 kazananın kodu, değilse 400. Başka bir index'teki E11000 (ör.
     `(TenantId, Code)`) bu istisnaya çevrilmez → 503 + hata logu. Çeviri **savunma olarak kalır** (CT kararı, tur 2 F-2);
     ölçümü AC-5 durum 4'te yeniden okumayı atlatan test sarmalayıcısıyla zorlanır.
  Her dört durumda defterde tek satır ve tek başarı denetim satırı kalır. **503 yalnız gerçek erişilemezlikte** (bağlantı yok,
  bütçe doldu, yeniden denemeler tükendi — durum 2'nin sınırı ve aşağıdaki ilk kilit sınırı dahil).
- **İlk kilit yarışı — kabul edilen sınır (r11; inceleme 03 tur 8 F-1, son bakış 01 tur 5 F-1; sahip kararı Beste
  2026-11-02 "B").** Aynı kiracıda kilitsiz ayara **iki eşzamanlı ilk `Group` ayırması** (farklı anahtarlar) gelirse ikisi de
  gövdede aynı ayar belgesine `LockAsync` yazar; ikinci işlem bu satırda **`WriteConflict`** alır (çıkarım: sunucu davranışı,
  bu repoda ölçülmedi — durum 2'deki mekanizmayla aynı; kazananın yazımı commit olmamışken de, ikinci işlemin anlık görüntüsü
  alındıktan sonra commit olmuşken de çakışma beklenir). `PlatformTransactionExecutor`
  (`services/Diten.Platform/src/Diten.Platform.Infrastructure/Persistence/PlatformTransactionExecutor.cs`; ölçüldü
  `4288ae40e`: `MaximumBodyAttempts = 3`, `IsTransientBodyFailure` = `TransientTransactionError` etiketi ya da
  `MongoCommandException` kod 112) gövdeyi yeni oturumla, denemeler arasında **beklemeden**, toplam **en çok 3 deneme** koşar
  (ilk deneme + en çok 2 yeniden koşu). Kazanan o arada commit olduysa yeniden denemede `LockAsync` eşleşir, `LockedNow = false`
  → 200, yeni kod, `GroupPrefixLockedNow = false`. Üçüncü denemede de çakışma alınırsa yürütücü onu
  `PlatformTransactionUnavailableException`'a çevirir → işleyici **503 `RECORD_CODE_UNAVAILABLE`** (+ `Failed` ret kaydı en
  iyi çaba); defter satırı ve kod yok; adım 12'de işlem dışında alınan sayaç numarası boşluk kalır (§5.8). Tüketici **aynı**
  `idempotencyKey` ile yeniden dener (sözleşme §8 kuralı: 503'ten sonra aynı anahtarla, sınırlı ve geri çekilerek): önceki
  denemeler commit olmadığından bu istek tekrar değil **ilk yazımdır** → yeni ayırma, 200, ayar artık kilitli
  (`GroupPrefixLockedNow = false`). Sınır kiracı başına yalnız **ilk** `Group` ayırmasında oluşur (kilit bir kez yazılır);
  sonraki ayırmalarda `LockAsync` belgeyi değiştirmez (Bölüm 3'teki `ModifiedCount` çıkarımı; AC-17 sıralı ölçümü ölçer).
  Yürütücüye bekleme eklenmez (paylaşılan altyapı, bu paketin kapsamı dışı — durum 2'deki sınırla aynı karar). Bu yarış DL-1'de
  testle **zorlanmaz**: kazananın commit'ini `IPlatformTransactionFaultProbe.BeforeCommitAsync`'te tutan bir bariyer ikinci
  işlemin bariyere ulaşmasına izin vermez (ikinci işlem gövdede çakışır ve denemelerini tüketir); r10'un bariyerli yarış
  ölçümü bu yüzden kaldırıldı, `GroupPrefixLockedNow` AC-17'de **sıralı** ölçülür.
- **Boşluk üst sınırı:** aynı anahtarla eşzamanlı **N** istek en çok **N−1** sayaç numarası harcar (sayaç adım 12'de, işlem
  dışında alınır); 200 alan her istek aynı kodu alır (durum 2 sınırında 503 alan istek aynı anahtarla tekrarlanır).
- Farklı anahtarlı eşzamanlı istekler aynı sayaçta farklı numara alır; sayaç işlem dışında olduğu için sıcak sayaçta işlem
  çakışması doğmaz. Tek istisna kiracının ilk `Group` ayırmasındaki kilit yazımıdır (yukarıdaki ilk kilit sınırı, r11).

### 8.7 Platform kapsamı (K-11)

DL-1'de `prefixScope = Platform`, tür izni adımını geçmiş her istekte 422 `RECORD_CODE_SCOPE_INVALID` (sözleşme: "DL-4'te
açılır"); izinli çağıranı olmayan tür (`PLN`) bu adıma gelmeden 403 alır (Bölüm 8.5). Gerekçe:
merkezi denetim kutusu boş `TenantId`'yi reddeder (`AuditOutboxWriteRequest.cs:28-30`), kayıt yoksa kod yok kuralı
(Bölüm 21) kiracısız satırı bugün yazamaz; HTTP'de Platform kapsamlı tüketici de yok. DL-4: PLN bağlanmasıyla birlikte
kiracısız sayaç/defter tabanı, platform-global denetim yolu ve süreç içi port kararlaştırılır (AC-25).

### 8.8 Veri kapsamı (SEC-002) — kapsam cümlesi

v1'de kiracı çapında satır döndüren sorgu yoktur (yönetici `GET`'i tek satırlık kiracı ayarıdır, Platform aktörüne;
data-scope-enforcement "Platform yöneticisi yüzeyleri" istisnası). Şirket kapsamı (kullanıcı bu şirket için kayıt
açabilir mi) çağıranın Create komutunda kendi kapsam portuyla denetlenir; Platform bu uçta kapsam sormaz — yetkinin
sınırı kimlik bilgisi + kullanıcının modül izni + kiracı eşitliğidir ve kod şirket verisi sızdırmaz. Tenant-wide izni yok.
DL-10 ekranları kapsam cümlesini o revizyonda yazar.

### 8.9 Yerelleştirme

Platform v1'de kullanıcıya görünen metin üretmez. `errors[]` İngilizce tanılama metnidir ve gösterilmez (TC-6). `.resx` yok.

### 8.10 Günlük

Her ret `reason_code`, kaynak servis, kiracı, korelasyonla **uyarı** düzeyinde loglanır. Kimlik bilgisi sırrı,
`Authorization`, JWT, MDM gövdesi loglanmaz.

### 8.11 Ret kaydı (K-10) — yazım yolu

Ret iş işlemi değildir: kayıt **ayrı kısa bir işlemle** (yeni `IPlatformTransactionSession`) yalnız denetim kutusuna
yazılır — `ITransactionalAuditRefusalWriter.TryEnqueueRefusalAsync(session, request, outcome)`. Defter, sayaç ve grup öneki
ayarına bu işlemde **dokunulmaz**; ret yolu hiçbir koşulda kod üretmez ya da döndürmez. Ret kaydı yazılamazsa uyarı
loglanır, cevap değişmez (en iyi çaba, sessiz değil). Hangi ayırma retlerinin yazıldığı Bölüm 21.2'dedir; grup öneki
hizalama retleri de aynı yazıcıyla yazılır (r6, Bölüm 21.3 Tablo D) — yazıcının kendisi değişmez, ikinci bir çağıranı olur.

### 8.12 Grup öneki tutarlılığı (K-12 c; r3 inceleme 02 F-7)

- Grup öneki değerinin **tek kaynağı** belge kodlarının `OrgPrefix`'idir (RCS-001 §5.3: "Belge kodlarındaki `OrgPrefix` ile
  aynı değer"). Kiracı ayarı (`record_code_group_prefixes`) onun Platform yöneticisi hizalamasıyla alınmış ve ilk
  kullanımda kilitlenen kopyasıdır; ayrı düzenlenebilir değer yoktur (Bölüm 4.3).
- Her `Group` ayırmasında kiracı ayarı etkin `OrgPrefix` ile karşılaştırılır (bellekte); farklıysa 409
  `GROUP_PREFIX_MISMATCH`, kod verilmez, sayaç ilerlemez.
- Kilitsizken düzeltme = `OrgPrefix`'i değiştirip yeniden hizalamak; kilitten sonra hizalama farklı değerde 409
  `GROUP_PREFIX_LOCKED`. Kurtarma: `OrgPrefix`'i kilitli değere geri almak; bilinçli önek değişikliği FU-RCS-16 (sahip kararı).
- Servis grup önekini hiçbir zaman kendiliğinden doldurmaz; kayıt yoksa 409 `GROUP_PREFIX_MISSING`.
- **Erişimci (r7, son bakış 01 F-8; ölçüldü).** Etkin `OrgPrefix` = `IOptions<DocumentCodingOptions>.Value.OrgPrefix` —
  belge üreticisinin kullandığı erişimcinin aynısı (`DocumentIdentifierAllocationService` kurucusu `IOptions<DocumentCodingOptions>`
  alır; bağlama `services/Diten.Platform/src/Diten.Platform.Infrastructure/DependencyInjection.cs`, `services.Configure<DocumentCodingOptions>(…GetSection(SectionName))`).
  `IOptions<T>.Value` süreç ömrü boyunca sabittir: yapılandırma değişikliği **süreç yeniden başlatılmadan görünmez**. Bu
  bilinçli tercihtir — `IOptionsMonitor` kullanılsaydı kayıt kodu belge kodundan önce yeni değeri görür ve iki biçim aynı
  süreçte ayrışırdı (RCS-001 §5.3 "aynı değer"). Sonuç: **düzeltme yolu** = yapılandırmayı değiştir → **bütün Platform
  örneklerini yeniden başlat** → hizalama ucunu çağır. Çok örnekli dağıtımda yeniden başlatma bitene kadar eski değeri
  taşıyan örnekte `Group` ayırması geçici 409 `GROUP_PREFIX_MISMATCH` alabilir ve hizalama eski değeri yazabilir; bu yüzden
  hizalama yeniden başlatma tamamlandıktan sonra çağrılır (dev-runbook adımı, FU-RCS-13'le birlikte). Test yapılandırması
  AC-35'te.
- Sınır: `OrgPrefix` bugün kiracı başına değil, tek yapılandırma değeridir; ikinci bir iş kiracısı farklı grup önekiyle
  gelirse belge tarafının kiracı başına olması gerekir (FU-RCS-14).

### 8.13 Kısa kod kullanım kilidi — ara dönem azaltıcısı (RCS-001 §5.4; r3 inceleme 02 F-11)

- **Risk (ölçüldü):** MDM tüzel kişilik kısa kodu bugün değiştirilebilir (`UpdateLegalEntityHandler.cs:36-37` yalnız
  tekillik denetler); §5.4 "bir kez önekte kullanıldıktan sonra değiştirilemez" kuralının MDM tarafı (DL-5) yok. Serbest
  kalan bir kod başka bir şirkete verilirse sayaç anahtarı `(kiracı, önek, tür)` olduğu için aynı sayaçtan ona da kod
  üretilir ve önek iki şirketi gösterir.
- **Azaltıcı (DL-1, bu serviste):** LegalEntity kapsamında MDM'den okunan kısa kod `K`, ayırma defterine karşı denetlenir:
  (a) bu şirketin (`LegalEntityId = X`) en son ayırmasının `Prefix`'i `K`'den farklıysa — kısa kod kullanıldıktan sonra
  değişmiş — 409 `LEGAL_ENTITY_SHORT_CODE_CHANGED`; (b) `K` bu kiracıda daha önce **başka** bir şirketin
  (`LegalEntityId ≠ X`; yalnız LegalEntity kapsamlı satırlar — Group satırlarında `LegalEntityId` boştur ve sayılmaz)
  ayırmasında önek olarak kullanılmışsa — kod başka şirkete geçmiş — 409
  `LEGAL_ENTITY_SHORT_CODE_CHANGED`. İkisinde de kod verilmez, sayaç ilerlemez, `Failed` ret kaydı ve **hata** logu yazılır.
  Index'ler Bölüm 4.2. Bu, Platform'da ayrı bir kısa kod tablosu değildir (K-5); verilmiş kodların defteridir.
- Kurtarma: MDM kısa kodu eski değerine geri alınır (yetkili yol), ya da sahip kararıyla bilinçli önek değişikliği
  (DL-5 ile birlikte).
- **Sahip ve tarih:** MDM tarafındaki kalıcı kilit (DL-5) için karar **MDM sahibi (Ali) + Beste, 2026-11-06** (A-18).

### 8.14 Süreç içi port — ORG / POS (DL-2, r12; K-3, K-12, K-15; sözleşme 1.0.0 değişmez)

**Neden HTTP değil.** ORG ve POS Diten.Platform'un içindedir (MOD-0288; `services/Diten.Platform/src/Diten.Platform.Domain/Entities/Organization/OrganizationUnit.cs`,
`Position.cs`). İç uç servisten servise tasarlandı (P2: kimlik bilgisi + iletilen kullanıcı token'ı, Bölüm 8.2); Platform'un kendine
HTTP çağrısı, kimlik bilgisi sırrını Platform'un kendi yapılandırmasına koymayı ve sözleşme §6'nın "`ORG`, `POS` … → 403" satırını
değiştirmeyi gerektirirdi. Sözleşme değişmez; HTTP'de ORG / POS 403 kalır (AC-49). K-3'ün "servisten servise" ucu ve K-12'nin
kod kataloğu aynen geçerlidir; süreç içi yol aynı işleyiciyi çağırır.

**Arayüz (Application)** — `services/Diten.Platform/src/Diten.Platform.Application/Contracts/RecordCodes/IInProcessRecordCodeAllocator.cs`:

```csharp
public interface IInProcessRecordCodeAllocator
{
    Task<InProcessRecordCodeResult> AllocateAsync(InProcessRecordCodeRequest request, CancellationToken ct);
}

public sealed record InProcessRecordCodeRequest(string RecordType, Guid LegalEntityId, Guid RecordId);

public sealed record InProcessRecordCodeResult(
    bool IsAllocated, string? Code, long? Sequence, int StatusCode, string? ReasonCode);
```

Ad: Treasury'nin kendi portu `IRecordCodeAllocator` adını taşır (Bölüm 7.1 TC-5) — karışmasın diye `InProcess` öneki. Kapsam
parametresi yoktur: süreç içi türlerin (ORG, POS) tek izinli kapsamı `LegalEntity`'dir (K-15); yeni bir süreç içi tür başka bir
kapsam isterse imza o dilimin revizyonunda **eklemeli** genişler (DL-4: TTY Group, PLN Platform).

**Uygulama** — `Features/RecordCodes/InProcessRecordCodeAllocator.cs`. Kendi kural kopyası **yoktur**: DL-1 işleyicisini
(`AllocateRecordCodeHandler`, `…/Features/RecordCodes/Handlers/CommandHandlers/AllocateRecordCodeHandler.cs:69-300`)
`ISender.Send(new AllocateRecordCodeCommand(…))` ile çağırır; adım 4–13 (kiracı aktif, katalog, tür izni, kullanıcı izni,
idempotency, kapsam, önek ve kısa kod kilidi, sayaç, tek işlem + denetim) ve ret kaydı aynen koşar. İşleyici içinden MediatR
gönderiminin Platform'daki emsali: `Features/Meetings/Handlers/CommandHandlers/GenerateDueMeetingSeriesHandler.cs:33`,
`Features/DocumentManagementMasterRegister/Handlers/CommandHandlers/DocumentRegisterImportHandlers.cs:43`.
`AllocateRecordCodeCommand` `IAuditableCommand` değildir (`…/Commands/AllocateRecordCodeCommand.cs:13-22`), `AuditBehavior`
onu kayıtsız geçirir; doğrulayıcısı yoktur (AC-45). Komut alanları:

| Alan | Süreç içi değer | Kaynak / savunma |
|---|---|---|
| `TenantId` | `TenantGuard.RequireTenant(ITenantContext)` — ORG / POS yollarında ara katman kiracıyı JWT'den kurar (`TenantResolutionMiddleware.cs:314-318`) | `ITenantAuthorizationContext.TenantId` ile eşit değilse port ayırma **göndermeden** 403 `RECORD_CODE_FORBIDDEN` döner (savunma; AC-57) |
| `OnBehalfOfUserId` | `ITenantAuthorizationContext.UserId` (token `sub`) | `Guid.Empty` ya da `ActorType ≠ tenant_user` → göndermeden 403 `RECORD_CODE_FORBIDDEN` (savunma; bu yollarda ara katman platform aktörünü zaten reddeder, `:314`) — AC-58 |
| `CallerService` | `"Diten.Platform"` (sabit) | denetim kapısının kapalı kümesinde (`TransactionOwnedAuditSourceServices.cs:10-14`, `CanonicalTransactionalAuditOutboxWriter.SourceService`); küme büyümez |
| `AllowedRecordTypes` | `{ RecordType }` yalnız katalogda `AllowedCaller == "Diten.Platform"` ise; değilse boş küme | boş kümede adım 7 → 403 (`AllocateRecordCodeHandler.cs:98-103`) |
| `Request` | `new AllocateRecordCodeRequest(RecordType, RecordCodePrefixScope.LegalEntity, LegalEntityId, IdempotencyKey)` (`RecordCodesModels.cs:15-19`) | adım 5 (gövde) bu yolda hiç ret vermez |
| `IdempotencyKey` | `organization-unit:{RecordId:N}` (ORG, 50 karakter) · `position:{RecordId:N}` (POS, 41 karakter); çakışma atlamasında `…:{deneme}` (Bölüm 8.15) | sözleşme §2 biçimi (1–200 yazdırılabilir ASCII); defter tekilliği `(TenantId, CallerService, IdempotencyKey)` |
| `RequestErrors` | boş | — |
| `HasRequiredPermission` | `IActorPermissionContext.Has(definition.RequiredPermission)` | controller'ın `[HasPermission]` filtresiyle **aynı** değerlendirici (`PermissionClaimEvaluator`; `IActorPermissionContext.cs` sınıf yorumu) — iki kapı aynı kullanıcıya farklı cevap veremez. HTTP yolundaki birebir `HasExactPermission` (Bölüm 8.5 adım 8) servisler arası güvenilir yol içindir; burada principal Platform'un kendi doğruladığı token'dır (paket kararı; inceleme konusu) |
| `CorrelationId` | `Diten.Platform.Common.Observability.ICorrelationContext.CorrelationId`; boşsa yeni Guid (`N`) | — |

**Sonuç eşlemesi.** `Response<RecordCodeAllocationResult>` başarılıysa `IsAllocated = true`, `Code`, `Sequence`; değilse
`IsAllocated = false`, `StatusCode` ve `ReasonCode` cevaptakiyle aynı (`Response<T>.ReasonCode`, `…/Application/Common/Response.cs:12-13`).
Denetim kapısının başarı yolu istisnaları (`TransactionOwnedAuditRefusedException`, `TransactionOwnedAuditIntentInvalidException`,
Bölüm 8.6) port tarafından **yakalanmaz**.

**Kayıt kimliği önce üretilir.** İşleyici yeni kaydın `Id`'sini porttan **önce** üretir ve hem anahtara hem varlığa yazar
(`BaseEntity.Id` `init`, `services/Diten.Platform.Common/src/Diten.Platform.Common/Persistence/BaseEntity.cs:5`). Defter satırı
ve başarı denetim satırı böylece kayda bağlıdır (TC-7'nin süreç içi karşılığı).

**İşlem ilişkisi — iki ayrı yazım, sabit sıra (ayırma başarısızsa kayıt oluşmaz).** ORG / POS yazımı bugün işlem dışıdır
(`CreateOrganizationUnitCommandHandler.cs:87`, `CreatePositionCommandHandler.cs:54` → `TenantRepository.CreateAsync`, tek
`InsertOneAsync`, `…/Persistence/Repositories.cs:81-91`) ve merkezi denetimi `AuditBehavior` işleyiciden sonra yazar. Ayırma kendi
işlemindedir (Bölüm 8.6: defter + denetim kutusu tek işlem, sayaç işlem dışında). Sıra:

1. Koddan bağımsız doğrulamalar bugünkü sırayla: kiracı, oluşturmada dolu kod reddi (Bölüm 8.15), ORG'de şirket referansı
   (`CreateOrganizationUnitCommandHandler.cs:45-49`) ve üst birim / döngü (`:51-74`); POS'ta birim, üst pozisyon ve döngü
   (`PositionReferenceGuard.ValidateAsync`, `…/PositionReferenceGuard.cs:16-38`). Başarısız doğrulama numara harcamaz.
2. Port → ayırma (kendi işlemi; başarı ya da ret kaydı yazılır).
3. Çakışma savunması (Bölüm 8.15).
4. Kayıt yazımı (`Code` = ayrılan kod).

- **Ayırma başarısızsa kayıt oluşmaz.** İşleyici portun `StatusCode` ve `ReasonCode`'unu `Response<Guid>.Fail(…)` ile olduğu gibi
  döner (ör. 409 `LEGAL_ENTITY_SHORT_CODE_CHANGED`, 403 `RECORD_CODE_PERMISSION_MISSING`, 403 `RECORD_CODE_TENANT_NOT_ACTIVE`,
  503 `RECORD_CODE_UNAVAILABLE`); kendi sayacına, normalleştiriciye ya da yedek koda **düşmez** (TC-2'nin süreç içi karşılığı; AC-54).
- **Ayırma başarılı, kayıt yazımı başarısızsa** (Mongo hatası, eşzamanlı unique çakışma): kayıt yoktur; defter satırı ve başarı
  denetim satırı kalır; kod bir **boşluktur** — RCS-001 §5.8 "Başarısız kayıtta harcanan numara geri verilmez; boşluk doğal ve
  kabul" (`.antigravity/rules/record-coding-standard.md:122`); uyarı logu (kod, kayıt kimliği, kiracı). Kullanıcının yeniden
  denemesi yeni kayıt kimliği → yeni anahtar → yeni koddur. Defter "verilmiş ama kullanılmamış" kodu böylece taşır (FU-RCS-04
  raporunun konusu).
- **Aynı işlem neden seçilmedi:** ORG / POS depolarının oturumlu yazım yolu yoktur (MOD-0288 değişikliği büyürdü), ayırma
  işleyicisinin işlemi kendi içindedir ve sayaç zaten işlem dışındadır (Bölüm 8.4) — tek işlem boşluğu ortadan kaldırmaz, yalnız
  defter satırını geri alırdı. §5.8 boşluğu kabul eder.
- **İstisna yolu:** başarı yolundaki denetim kapısı istisnaları ORG / POS işleyicisinden de geçer; `AuditBehavior` çalışma zamanı
  istisna kaydını yazar (`…/Contracts/Behaviors/AuditBehavior.cs:63-66`), kayıt yazılmaz, cevap Platform'un ortak `ProblemDetails`'idir
  (503 `AUDIT_RECORD_UNAVAILABLE` / 500 `AUDIT_INTENT_INVALID`; AC-54 (d)).

**Kısa kod kilidi (Bölüm 8.13) ORG / POS'ta nasıl görünür.** Adım 11 aynıdır: LegalEntity kapsamında şirketin **herhangi bir
türdeki** en son ayırmasının öneki MDM'in bugünkü kısa koduyla karşılaştırılır (`FindLatestPrefixForLegalEntityAsync(tenantId,
legalEntityId)` türden bağımsızdır — `AllocateRecordCodeHandler.cs:177-187`). Sonuç: Treasury'nin `MYG` ile `PAY` aldığı şirketin
MDM kodu `MYX`'e çevrilirse o şirkette **Organizasyon Birimi ve Pozisyon oluşturma da** 409 `LEGAL_ENTITY_SHORT_CODE_CHANGED` alır;
kayıt oluşmaz, sayaç ilerlemez, ayırma reddi `Failed` yazılır (`platform.record-code.allocation-refused`, `EvaluationStep = 11`,
`Prefix` = MDM'in bugünkü kodu), hata logu, merkezi `Failed` oluşturma satırı. Form `reason_code`'u yerelleştirilmiş metinle
gösterir ("Şirketin kısa kodu kullanıldıktan sonra değişmiş; MDM yöneticisine başvurun" — anahtar `RecordCodeShortCodeChanged`,
TC-8'in süreç içi karşılığı). Aynı şekilde 409 `LEGAL_ENTITY_SHORT_CODE_MISSING` (MDM kısa kodu `^[A-Z]{2,5}$`'e uymuyor)
oluşturmayı durdurur — bugün kısa kodu olmayan şirkete birim açılabiliyor; bu bir **davranış değişikliğidir** (AC-54, AC-56).
Kalıcı kilit DL-5 / A-18.

**Ek MDM çağrısı.** ORG oluşturma şirketi bugün `MdmLegalEntityReferenceValidator` ile doğrular (`CreateOrganizationUnitCommandHandler.cs:45-49`);
port ayrıca `MdmLegalEntityShortCodeReader` ile kısa kodu okur (iletilen token `IHttpContextAccessor`'dan,
`…/Infrastructure/Services/Mdm/MdmLegalEntityShortCodeReader.cs:54`) — oluşturma başına iki MDM çağrısı, bütçeler ayrı (5 s / 3 s).
Birleştirme kapsam dışı. POS oluşturma bugün MDM'i hiç çağırmaz; DL-2'den sonra bir kez çağırır ve birimin şirketi aktif değilse
404 `LEGAL_ENTITY_NOT_FOUND` alır (davranış değişikliği; AC-51). Kullanıcı token'ında `mdm.legal-entities.read` yoksa 403
`LEGAL_ENTITY_READ_PERMISSION_MISSING` (Bölüm 8.3).

**POS'un şirket kaynağı (K-15; çıkarım — Q-5 teyit).** `Position`'da şirket alanı **yoktur** (`…/Entities/Organization/Position.cs:7-9`:
`Code`, `Name`, `OrganizationUnitId`); şirket **birim üzerinden** gelir: `LegalEntityId` = pozisyonun **oluşturma anındaki**
`OrganizationUnit`'inin `LegalEntityId`'si (`OrganizationUnit.cs:9`, zorunlu). Birim bugün `PositionReferenceGuard` içinde okunur
(`…/PositionReferenceGuard.cs:16-20`; yok ya da arşivli → 404); işleyici şirketi aynı okumadan alır. Pozisyon sonra başka şirketin
birimine taşınırsa (`UpdatePositionCommandHandler.cs:51`) ya da birimin şirketi değişirse (`UpdateOrganizationUnitCommandHandler.cs:147`)
**kod değişmez** — RCS-001 §5.2 (önek kaydı ilk açan şirkettir, kod kalıcıdır; `record-coding-standard.md:89-92`).

**Hız sınırı yok (adlı karar).** `RecordCodeAllocateRateLimiter` HTTP yürütücüsünün içindedir (Bölüm 8.2 azaltıcı 3); süreç içi
port ondan geçmez. Gerekçe: sınır kimlik bilgisi sızmasına karşıdır; süreç içi çağrıda kimlik bilgisi yoktur ve her çağrı
`[HasPermission]` kapılı, kullanıcı başlatmalı bir oluşturmadır. 429 bu yolda oluşmaz.

**Denetim.** Başarı satırı Tablo A'dır; farklar yalnız değerlerdedir: `SourceService = Diten.Platform`, `Metadata.IdempotencyKey`
= `organization-unit:{id:N}` / `position:{id:N}`, `GroupPrefixLockedNow = false`. Retler Bölüm 21.2 ve Tablo B ile aynı (adım 4–13).
Portun savunma retleri (kiracı / aktör) ayırmaya ulaşmaz → yalnız merkezi `Failed` oluşturma satırı (Bölüm 21.4).

### 8.15 Mevcut kodlar ve kod değişmezliği (RCS-001 §2, §6, §7; DL-2, r12)

**Bugünkü durum (ölçüldü, `a9fd192ba`).**

- Kod elle girilir: `OrganizationUnit.Code` / `Position.Code` `required string` (`OrganizationUnit.cs:7`, `Position.cs:7`);
  istek alanı `OrganizationUnitRequest.Code` (`…/TenantOrganization/TenantOrganizationContracts.cs:9`), `PositionRequest.Code` (`:52`);
  doğrulayıcı `NotEmpty().MaximumLength(80)` (`…/Validators/TenantOrganizationRequestValidators.cs:9`, `:19`). İşleyici
  `OrganizationCodeNormalizer.Normalize` ile normalleştirir (`OrganizationCodeNormalizer.cs:7-30`: kırp, büyük harf, harf / rakam
  dışını `-` — `char.IsLetterOrDigit` Türkçe harfi korur, RCS-001 §5.10'a aykırı), tekilliği denetler (409) ve yazar
  (`CreateOrganizationUnitCommandHandler.cs:34-43`, `:79`; `CreatePositionCommandHandler.cs:27-36`, `:47`).
- Güncelleme kodu serbestçe değiştirir: `UpdateOrganizationUnitCommandHandler.cs:49-58`, `:145`; `UpdatePositionCommandHandler.cs:32-41`,
  `:49`. Raporlama hattı ucu saklı kodu geri yükler (`UpdateOrganizationUnitCommandHandler.cs:215-216`); liste ekranındaki
  sürükle-bırak yeniden bağlama tüm kaydı okuyup geri gönderir (`frontend/Diten.Web/wwwroot/assets/js/Organization/OrganizationUnits/index.js:617-627`).
- API: `POST` / `PUT /api/platform/organization-units` (`…/API/Controllers/Platform/OrganizationUnitsController.cs:30-38`),
  `PUT …/{id}/reporting-lines` (`:49-56`); `POST` / `PUT /api/platform/positions` (`PositionsController.cs:35-43`).
- Tekillik: `ux_organization_units_tenant_code_active` / `ux_positions_tenant_code_active` `(TenantId, Code)` unique, **yalnız
  `IsDeleted == false`** (`…/Persistence/Schema/PlatformSchemaManifest.Organization.cs:32-41`, `:128-137`); `ExistsByCodeAsync`
  da silinmişleri görmez (`TenantRepository.ExecutionFilter`, `Repositories.cs:75-79`) → silinen kaydın elle kodu bugün yeniden
  kullanılabilir (RCS-001 §5.8'e aykırı; DL-2 yalnız otomatik kodlar için kapatır — eski elle kodlar FU-RCS-19).
- Ekran: oluşturma / düzenleme formunda zorunlu kod girişi (`frontend/Diten.Web/Views/Organization/OrganizationUnits/Form.cshtml:53-56`,
  `…/Positions/Form.cshtml:51-54`; `form.js` zorunlu listesi `OrganizationUnits/form.js:31`, `Positions/form.js:15`).
- Geliştirme tohumu elle kod yazar (`…/Infrastructure/Persistence/Configurations/PositionSeed.cs:54`, `:65-69`: `HEADQUARTERS`,
  `CEO`, `CTO`, `HR_MGR`, `DEV_LEAD`, `DEV_ENG`).
- Bugünkü kodlardan kaçının otomatik desene (`^[A-Z]{2,5}-(ORG|POS)-[0-9]{6,}$`) uyduğu **ölçülmedi** (canlı veri bu revizyonda
  okunmadı) — DL-2 kabulünde kiracı başına ölçülür (AC-55, Bölüm 18.1).

**Geri uyum — Q-1 (sahip Beste; DL-2 kodunu bekletir).**

| Seçenek | Ne olur | Sayaç başlangıcı / çakışma | Kural |
|---|---|---|---|
| **A — koru (öneri)** | Bugünkü kodlar olduğu gibi kalır; DL-2'den sonra açılan her kayıt otomatik kod alır | Sayaç (kiracı, şirket kısa kodu, tür) başına `1`'den başlar (DL-1 sayaçlarında ORG / POS belgesi yok). Eski bir kod ancak desene uyuyor **ve** aynı şirket öneki + türü taşıyorsa çakışabilir. **Savunma:** işleyici ayrılan kodu kayıt yazmadan önce kendi koleksiyonunda **silinmişler dahil** arar (`ExistsByCodeIncludingDeletedAsync`); varsa kodu atlar ve yeni anahtarla (`…:{deneme}`) yeniden ayırır, en çok **3** deneme; atlanan numara defterde ayrılmış ama kullanılmamış kalır (§5.8; §5.9 "sayaç üretirken mevcut kodu atlar", `record-coding-standard.md:126-130`), uyarı logu; üçü de çakışırsa 409 `RECORD_CODE_COLLISION`, kayıt yok. DL-2 kabulünde desene uyan eski kod sayımı kiracı başına kayda yazılır; bir (kiracı, önek, tür) için sayım 3'ü aşarsa sayacı en büyük numaranın üstüne taşımak DL-6'nın (yüksek su işareti, §5.11) işidir ve o kiracıda DL-2 devreye alınmadan önce CT'ye bildirilir | RCS-001 §2 "Mevcut kodlar değiştirilmez … Geçmiş kayıtlar eski kodlarıyla kalır" (`record-coding-standard.md:42-43`) ve §7 (`:161-165`) ile birebir |
| **B — göç** (eski kod yeni `LegacyCode` alanına, kayda otomatik kod) | Her mevcut kayda otomatik kod verilir; eski kod `LegacyCode`'da kalır (§5.11 dili). "İkisi birden" budur — eski kod korunur, görünen kod değişir | Göç sırasında sayaç sıfırdan dolar; önce desene uyan eski kodlar sayılmalı | **§2'ye aykırı** ("Mevcut kodlar değiştirilmez … yazışma ve rapor referansları kırılmasın") — yönetim kararı ve `.antigravity` kural değişikliği (Ali) gerekir; varlıkta yeni alan, göç betiği, kodu gösteren ekran ve raporlar → ayrı paket revizyonu. Öneri değil |

**Kod değişmezliği — Q-2 (sahip Beste; DL-2 kodunu bekletir).** RCS-001 §6: "Kod oluşturulduktan sonra OTOMATİK türde
değiştirilemez; API de reddeder (yalnız ekran değil)" (`record-coding-standard.md:156`).

- **A (öneri) — bütün kodlar değişmez** (eski elle kodlar dahil): güncelleme isteğinde `Code` boş / yok ya da saklı değere
  **birebir** (sıralı karşılaştırma, normalleştirme yok) eşitse kabul ve kod korunur — formlar ve sürükle-bırak tüm kaydı geri
  gönderdiği için bu gereklidir; farklıysa 409 `RECORD_CODE_IMMUTABLE`, kayıt değişmez. Gerekçe: eski kodu düzenlenebilir bırakmak
  kullanıcının desene uyan bir kod yazmasına (§5.9 riski) ve aynı türde iki ayrı kurala yol açar; §2 eski kodların "eski kodlarıyla
  kalması"nı söyler.
- **B — eski kodlar elle değişebilir, yeni kodlar değişmez:** kaydın "otomatik mi" bilgisini taşıması gerekir (defterde
  `organization-unit:{id:N}` anahtarı ya da varlıkta yeni alan); eski kod değişikliğinde normalleştirici + silinmişler dahil
  tekillik + §5.9 desen yasağı. Daha çok kod ve test; öneri değil.

**Oluşturma isteğinde kod.** `Code` gönderilmez; dolu gelirse 400 `RECORD_CODE_SYSTEM_ASSIGNED`, ayırma yapılmaz (sayaç
ilerlemez). Ret **işleyicide** verilir: `Response<T>` zarfında `reason_code` dolu olsun ve merkezi denetimde `Failed` görünsün diye
(`ValidationBehavior` `AuditBehavior`'dan önce koşar ve `ProblemDetails` döner — Bölüm 8.5 (d) ile aynı gerekçe). Sessiz yok sayma
seçilmedi: kullanıcının yazdığı değerin kaybolması görünmez bir veri kaybıdır.

## 9. Layout & Shell Contract

`shell: none`. Bu FU hiçbir Razor görünümü, ön yüz rotası, DataTable ya da gezinme öğesi sahiplenmez; hiçbir `.cshtml`
oluşturulmaz, bu yüzden `Layout = "_LayoutPlatformAdmin"` ya da `Layout = "_LayoutTenantShell"` yazılacak dosya yoktur.
DL-10 ekranları Platform yönetici kabuğunda olacaktır (`Layout = "_LayoutPlatformAdmin"`, `Views/Platform/RecordCodes/`).

**DL-2 (r12).** Bu paket yeni görünüm açmaz; MOD-0288'in var olan iki formunu değiştirir (XMC-001, Bölüm 5 "DL-2"):
`frontend/Diten.Web/Views/Organization/OrganizationUnits/Form.cshtml` ve `frontend/Diten.Web/Views/Organization/Positions/Form.cshtml`.
İkisi de **`Layout = "_LayoutTenantShell"`** taşır (`OrganizationUnits/Form.cshtml:10`, `Positions/Form.cshtml:9`; ölçüldü) ve DL-2'de
layout **değişmez** — kiracı kabuğu ekranlarıdır (Bölüm 11 dil kararı buna dayanır). AC-60 iki formun bu satırı değişmeden
taşıdığını ölçer.

## 10. Backend File Convention

Golden Reference klasör/ad kuralı (module-pack-standard §4) birebir; `DocumentManagementIdentifiers`'ın `Services/` alt
klasörü örnek alınmaz:

```text
services/Diten.Platform/src/Diten.Platform.Application/Features/RecordCodes/
├── Commands/
│   ├── AllocateRecordCodeCommand.cs
│   └── AlignRecordCodeGroupPrefixCommand.cs     (ITransactionOwnedAuditCommand; gövdesiz — r5)
├── Queries/
│   └── GetRecordCodeGroupPrefixQuery.cs
├── Handlers/
│   ├── CommandHandlers/
│   │   ├── AllocateRecordCodeHandler.cs
│   │   └── AlignRecordCodeGroupPrefixHandler.cs
│   └── QueryHandlers/
│       └── GetRecordCodeGroupPrefixHandler.cs
│   (Validators/ klasörü yok — r6 / r7: iki komutun da doğrulayıcısı yok, retler işleyicide; Bölüm 8.5, AC-45)
├── RecordCodeReasonCodes.cs
├── RecordCodeAuditIntent.cs
└── RecordCodesModels.cs
```

- Handler adlarında `Command` / `Query` eki yok.
- **Golden Reference'tan adlı sapma (r7, son bakış 01 F-1):** `Validators/` klasörü yoktur. Gerekçe: bu özelliğin bütün
  retleri denetime yazılır (AUD-001 §3, K-13, K-14), `ValidationBehavior` retleri işleyiciden ve denetimden önce
  `ProblemDetails` olarak verir (Bölüm 8.5 (d)). Biçim kuralları API ayrıştırıcısında, retler işleyicide; AC-45 bunu
  sabitler. `golden_reference: none` (DataTable / form yok).
- İşleyici sırayı yönetir; biçim `RecordCodeFormat`'ta, kapsam kuralı `RecordCodeTypeCatalog`'da (Domain), MDM portta,
  Mongo depoda; kiracı işleyiciye yürütücüden parametre olarak gelir.
- Controller `CustomBaseController`, `Response<T>` (`…/Application/Common/Response.cs:7-16`).
- `AllocateRecordCodeCommand` `IAuditableCommand` değildir: `AuditBehavior` yazmaz, denetim işleyicinin içindedir.
- `AlignRecordCodeGroupPrefixCommand` `ITransactionOwnedAuditCommand`'dır (r5 F-1): `AuditBehavior` yalnız liste denetimi
  yapar; işleyici kurucusunda `ITransactionalAuditOutboxWriter` ve `IPlatformTransactionExecutor` alır (koruma testinin
  "kapı" şartı).

## 11. Frontend File Contract

Yok. `golden_reference: none`, `form_field_count: 0`. Tarayıcı bu uçları çağırmaz. Sayfa yardımı (UXH-001) ve tur gerekmez.

**DL-2 (r12) — MOD-0288 formlarının kod alanı (XMC-001).** Bu paketin künyesindeki `golden_reference: none` /
`form_field_count: 0` değişmez: formlar MOD-0288'indir ve dosya setleri değişmez (yeni dosya yok). Değişiklik:

- **Oluşturma:** kod girişi **salt okunur ve boş** kalır; altında yerelleştirilmiş not ("Kod, kayıt kaydedilince sistem
  tarafından verilir" — `RecordCodeAssignedOnSave`); `form.js` yükte `code` göndermez; zorunlu listeden (`REQUIRED`) `ouCode` /
  `pCode` çıkar; `required` ve `data-required` öznitelikleri kod girişinden kalkar (`OrganizationUnits/Form.cshtml:56`,
  `Positions/Form.cshtml:54`), zorunluluk işareti (`*`, `:53` / `:51`) kalkar.
- **Düzenleme:** kod salt okunur, sunucudaki değeri gösterir; düzenleme yükü saklı kodu olduğu gibi geri gönderir (AC-53 ile uyumlu).
  Ayrıntı ekranı (`Details.cshtml`) zaten salt okurdur, değişmez.
- **Hata gösterimi:** cevapta `reason_code` Bölüm 3 / 8.14'teki kodlardan biriyse yerelleştirilmiş metin gösterilir; değilse bugünkü
  davranış (`errors[]`) sürer. Kayıt kodu retlerinde İngilizce `errors[]` metni gösterilmez (TC-6'nın süreç içi karşılığı).
- **Dil: 7 dil** (`en`, `tr`, `fr`, `es`, `zh`, `ar`, `ru`). Formlar kiracı kabuğundadır (Bölüm 9) ve kaynak dosyaları bugün 7 dildedir
  (`frontend/Diten.Web/Resources/Views/Organization/OrganizationUnits/OrganizationUnitsIndex.{en,tr,fr,es,zh,ar,ru}.resx`,
  `…/Positions/PositionsIndex.{…}.resx`; ölçüldü). `.antigravity/rules/localization-standard.md:8-9`: 2 dil yalnız "Platform
  Modülleri (Admin)" için, kiracı modüllerinde 7 dil. Görev metnindeki "Platform modülü 2 dil (en, tr)" bu ekranlar için ölçümle
  çelişir → **Q-3** (öneri 7 dil; sahip 2 dil derse var olan 5 dil dosyasında anahtar eksik kalır ve kural ihlali olur).
- **Anahtarlar** (iki kaynak dosyası, her dilde): `RecordCodeAssignedOnSave`, `RecordCodeImmutable`, `RecordCodeSystemAssigned`,
  `RecordCodeCollision`, `RecordCodeShortCodeChanged`, `RecordCodeShortCodeMissing`, `RecordCodeLegalEntityNotFound`,
  `RecordCodeLegalEntityNotEligible`, `RecordCodeLegalEntityReadPermissionMissing`, `RecordCodePermissionMissing`,
  `RecordCodeTenantNotActive`, `RecordCodeUnavailable`. `_FormL10n.cshtml` bunları JS'e aktarır. Var olan `CodePlaceholder`,
  `InvalidCode`, `FinalCodePreview` anahtarları kullanılmaz hâle gelir ama **silinmez** (FU-RCS-18).
- **Değişmeyenler:** liste ekranları (`Index.cshtml`, `_DataTable.cshtml`, `_Filter.cshtml`, `index.js`) — liste ekranı dokunma
  protokolü (BL-440) tetiklenmez; kullanılmayan `_CreateEditOffcanvas.cshtml` parçaları (hiçbir görünüm çağırmıyor — ölçüldü;
  yalnız `frontend/Diten.Web/tests/diten-field-icons.test.js:610-612` adlarını anıyor) — FU-RCS-18.
- **Sayfa yardımı (UXH-001):** iki formda sayfa yardımı işareti bulunmadı (`PageHelp` / `page-help` arandı, ölçüldü); yardım metni
  değişikliği gerekmez.

## 12. Validation Rules

| Alan | Zorunlu | Biçim / kural | DB düzeyi | Ön kontrol |
|---|---|---|---|---|
| gövde | evet | JSON nesnesi; `Content-Type` `application/json` değilse → 400 `RECORD_CODE_REQUEST_INVALID` (415 kullanılmaz: ham gövde ayrıştırıcıda okunur, `[Consumes]` / model bağlama yok — r7, son bakış 01 F-7); bilinmeyen üye (ör. `tenantId`, `sourceService`, `code`) → 400; tekrarlı üye → 400; ≤ 4 KB | — | API ayrıştırıcısı `TrustedRecordCodeAllocateRequestParser`; ret işleyicide, `Failed` kaydı (K-13); FluentValidation yok (AC-45) |
| `recordType` | evet | `^[A-Z]{3}$`, duyarlı; normalleştirme yok | — | katalog (422), kimlik bilgisi (403), kullanıcı izni (403) |
| `prefixScope` | hayır | `LegalEntity` \| `Group` \| `Platform`; yoksa `LegalEntity`; başka değer 400 | — | izinli kapsam; DL-1'de `Platform` tür izni geçtiyse 422 |
| `legalEntityId` | kapsama bağlı | `LegalEntity`: zorunlu, Guid, boş değil · `Group`: yok ya da `null` | — | uyumsuzluk 422; Guid değil 400 |
| `idempotencyKey` | evet | 1–200 karakter, yazdırılabilir ASCII `!`…`~` (A-11) | unique `(TenantId, CallerService, IdempotencyKey)` (yalnız defterde; denetim kutusu anahtarı bundan türemez) | defter araması |
| Grup öneki hizalaması (yönetici ucu) | gövde yok | etkin `OrgPrefix` `^[A-Z]{2,5}$` olmalı, değilse 409 `GROUP_PREFIX_SOURCE_INVALID` | unique `(TenantId)` | kilitli ve farklıysa 409 `GROUP_PREFIX_LOCKED`; kiracı yoksa 404 `TENANT_NOT_FOUND` (cevap ve denetim aynı kod — r7, inceleme 03 tur 4 F-2); gövde gönderilirse 400 `RECORD_CODE_REQUEST_INVALID` — dördü de **işleyicide** (doğrulayıcı yok) ve dördü de `Failed` ret kaydı yazar (r6, Bölüm 4 adım 4, 21.3 Tablo D) |
| `Intent.SourceService` (kapı) | hayır | boş ya da `TransactionOwnedAuditSourceServices` kümesinde birebir | — | küme dışı → istisna, hiçbir şey yazılmaz |
| Üretilen kod | — | `^[A-Z]{2,5}-[A-Z]{3}-[0-9]{6,}$` (DL-1) | unique `(TenantId, Code)` | `RecordCodeFormat` |
| ORG / POS oluşturma `code` (**DL-2**) | **gönderilmez** | dolu → 400 `RECORD_CODE_SYSTEM_ASSIGNED`, ayırma yok (işleyicide; doğrulayıcıda `NotEmpty` kalkar, `MaximumLength(80)` kalır) | `ux_organization_units_tenant_code_active` / `ux_positions_tenant_code_active` (değişmez) | ayırma Bölüm 8.14; çakışma savunması Bölüm 8.15 |
| ORG / POS güncelleme `code` (**DL-2**, Q-2 A) | hayır | boş / yok ya da saklı değere birebir eşit → kabul, kod korunur; farklı → 409 `RECORD_CODE_IMMUTABLE`, kayıt değişmez | — | raporlama hattı ucu saklı kodu gönderir (değişmez) |
| ORG / POS üretilen kod (**DL-2**) | — | `^[A-Z]{2,5}-(ORG\|POS)-[0-9]{6,}$`; önek = şirketin MDM kısa kodu (POS'ta birimin şirketi) | defter `(TenantId, Code)` + kayıt koleksiyonunun `(TenantId, Code)` unique'i | silinmişler dahil çakışma araması (Bölüm 8.15); en çok 3 atlama, sonra 409 `RECORD_CODE_COLLISION` |

## 13. Failure Path to Verify

- **Aynı anahtar, aynı gövde (tekrar)** — 200, ilk kod ve sequence; sayaç ilerlemez; MDM çağrılmaz; yeni denetim satırı yok.
- **Aynı anahtar, farklı gövde** — 400 `IDEMPOTENCY_KEY_REUSED`; sayaç ilerlemez; `Failed` ret kaydı.
- **Eşzamanlı aynı anahtar (N istek)** — 200 alan her istek aynı kodu alır (farklı gövdeliler 400); tek defter satırı;
  geçici yazım çakışması yeniden denenir ve kazanan okunur; yeniden denemeler kazananın commit'inden önce tükenirse 503
  (Bölüm 8.6 durum 2 sınırı), aynı anahtarla tekrar 200 kazananın kodu (r5, sözleşme §8); en çok **N−1** boşluk.
- **İki kiracı aynı istemci anahtarıyla** — ikisi de kendi kodunu alır; denetim kutusu anahtarları çakışmaz (ayırma kimliğinden).
- **Önce ret, sonra aynı anahtarla başarı** (ör. önce 409, MDM düzeltilince tekrar) — başarılı ayırma 200; ret ve başarı
  kayıtları ayrı satırlardır (ret anahtarı yeni Guid).
- **Commit sonucu bilinmiyor** (r4 F-3) — yürütücü commit'i yeniler, başarılıysa 200; tükenirse 503; aynı anahtarla tekrar:
  commit olmuşsa 200 aynı kod, olmamışsa yeni ayırma (ilk yazım).
- **İşlem içi kutu `false`** (değişmez ihlali, yalnız sahte depoyla zorlanabilir) — 503 `RECORD_CODE_UNAVAILABLE` + hata logu;
  defter satırı yok.
- **v0 gövdesi, sonra aynı anahtarla açık `"prefixScope": "LegalEntity"`** (r4 F-9) — 200 aynı kod; 400 değil.
- **Eşzamanlı farklı anahtarlar** — hepsi farklı kod; ilk sayaç yaratma yarışı 500 üretmez.
- **Aynı kiracıda eşzamanlı iki ilk `Group` ayırması** (kilitsiz ayar; r11) — kabul edilen sınır (Bölüm 8.6): ikinci işlem
  ayar satırında `WriteConflict` alır, yürütücü gövdeyi beklemeden en çok 3 deneme koşar; kazanan o arada commit olduysa 200 ve
  `GroupPrefixLockedNow = false`, denemeler tükenirse 503 `RECORD_CODE_UNAVAILABLE` + bir sayaç boşluğu; aynı anahtarla tekrar
  ilk yazımdır → 200 yeni kod. DL-1'de testle zorlanmaz (AC-17 sıralı ölçer).
- **Tanımsız / KALSIN tür** — 422 `RECORD_TYPE_UNKNOWN`; sayaç yaratılmaz.
- **Tür kimlik bilgisine izinsiz** (Mdm ile `PAY`; herkes için `ORG`/`POS`/`PLN`, hangi kapsamla olursa olsun) — 403 `RECORD_CODE_FORBIDDEN`; `Denied`.
- **Kullanıcıda izin yok** — 403 `RECORD_CODE_PERMISSION_MISSING`; `Denied`; tekrar da reddedilir.
- **Uyumsuz kapsam** (`BNK` + LegalEntity; `PAY` + Group; Group + dolu `legalEntityId`; LegalEntity + boş; tür izni geçmiş `Platform`) — 422 `RECORD_CODE_SCOPE_INVALID`; MDM çağrılmaz; `Failed` (r7, F-4).
- **Geçersiz kısa kod** — 409 `LEGAL_ENTITY_SHORT_CODE_MISSING`; sayaç yaratılmaz.
- **Kısa kod kullanıldıktan sonra değişmiş / başka şirkete geçmiş** — 409 `LEGAL_ENTITY_SHORT_CODE_CHANGED`; kod yok.
- **Onaysız şirket** (`approvalStatus` görünür ve `APPROVED` değil) — 422 `LEGAL_ENTITY_NOT_ELIGIBLE`.
- **Grup öneki yok / belge `OrgPrefix`'ten farklı** — 409 `GROUP_PREFIX_MISSING` / `GROUP_PREFIX_MISMATCH`; varsayılana düşülmez.
- **Başka kiracının / olmayan / aktif olmayan şirket** — 404 `LEGAL_ENTITY_NOT_FOUND`; tek gövde; `Denied`.
- **Askıdaki kiracı** — 403 `RECORD_CODE_TENANT_NOT_ACTIVE`; tekrar da reddedilir; kod yok.
- **Varsayılan / tohum kiracı (`…0001`) kullanıcısı** — normal iş kiracısı gibi kod alır.
- **Platform bağlamı ya da `Guid.Empty` kiracı ile depo çağrısı** — istisna; satır yok.
- **MDM erişilemez** — 503 `LEGAL_ENTITY_SOURCE_UNAVAILABLE`; kod yok; sayaç ilerlemez.
- **MDM 403 (kullanıcıda okuma izni yok)** — 403 `LEGAL_ENTITY_READ_PERMISSION_MISSING`, kalıcı; `Denied` (r7, F-10).
- **Mongo erişilemez / işlem tükenir** — 503 `RECORD_CODE_UNAVAILABLE`; kod yok; alınmış sayaç numarası boşluk kalır.
- **Denetim kutusu satırı yazılamaz (bağlantı)** — işlem iptal, 503; defter satırı yok.
- **Kapıya küme dışı kaynak servis adı** — istisna, hiçbir şey yazılmaz.
- **Yetkisiz aktör** — Bölüm 8.2 tablosu; işleyici çağrılmaz. İç uçta `platform_admin` / `partner_admin` token'ı 403
  `RECORD_CODE_FORBIDDEN`; yönetici ucunda `partner_admin` 403 (`PlatformAdminOnly`) — AC-44 (r4 F-4).
- **Yönetici ucu platform bağlamında depoya gider** (`TenantScope.Begin` unutulmuş) — depo istisnası; ayar yazılmaz; AC-43
  bunu kırmızı yapar (r4 F-1).
- **Hizalama reddi** (404 kiracı yok, 400 gövde var, 409 `GROUP_PREFIX_SOURCE_INVALID`, 409 `GROUP_PREFIX_LOCKED`) — ayar
  değişmez; ayrı kısa işlemle tek `Failed` ret kaydı; 404'te kayıt platform denetim kiracısına (`…2121`), diğerlerinde rota
  kiracısına bağlanır (r6, inceleme 03 tur 3 F-1; Bölüm 21.3 Tablo D; AC-43).
- **Hizalamanın denetim kutusu satırı yazılamaz** — işlem iptal, ayar değişmez, 5xx (durum kodu Platform'un mevcut istisna eşlemesinden gelir; bu pakette ölçülmedi, AC-43 yalnız "ayar yok"u ölçer); hizalama komutu izin listesinde
  değilse `AuditBehavior` işleyiciden önce reddeder (r5, inceleme 03 tur 2 F-1; AC-43).
- **Gövdede kiracı ya da kaynak servis** — 400; kiracı ve kaynak servis değişmez; `Failed` ret kaydı (r7, K-13).
- **Biçimi bozuk gövde** — `Response<T>` zarfında 400 `RECORD_CODE_REQUEST_INVALID` (`ProblemDetails` değil), `Failed` (AC-45).
- **Hız sınırı** — 429 `RECORD_CODE_RATE_LIMITED`; başka kiracının kovası etkilenmez; denetime yazılmaz (adlı sapma, K-10), uyarı logu + sayaç.
- **Kilit filtresi eşleşmez** (adım 11'den sonra ayar başka bir süreçte farklı `OrgPrefix` ile yeniden hizalanır) — yeniden değerlendirme yok; 409 `GROUP_PREFIX_MISMATCH`, `Failed` ret kaydı, hata logu, bir sayaç boşluğu (r8, son bakış 01 tur 2 F-1; AC-46).
- **Sayaç sınırı** — `…-999999`, sonra `…-1000000`.
- **Grup öneki hizalama + eşzamanlı ilk ayırma** — biri kazanır: hizalama önce (ayırma yeni öneki kullanır ve kilitler) ya
  ayırma önce (farklı değerle hizalama 409 `GROUP_PREFIX_LOCKED`); sessiz üzerine yazma YOK.
- **Kilitten sonra `OrgPrefix` değişti** — her Group ayırması 409 `GROUP_PREFIX_MISMATCH` + hata logu; kurtarma Bölüm 4.3.

**DL-2 (r12) — ORG / POS:**

- **Oluşturmada kod gönderildi** — 400 `RECORD_CODE_SYSTEM_ASSIGNED`; ayırma yok, sayaç ilerlemez; merkezi `Failed` (AC-52).
- **Ayırma reddi** (403 / 404 / 409 / 422 / 503) — kayıt yok; aynı durum kodu ve `reason_code`; ayırma ret kaydı (Bölüm 21.2) +
  merkezi `Failed`; yedek kod yok (AC-54 (a)).
- **Kısa kod değişmiş / eksik** — 409 `LEGAL_ENTITY_SHORT_CODE_CHANGED` / `LEGAL_ENTITY_SHORT_CODE_MISSING`; kayıt yok (AC-56).
- **Ayırma başarılı, kayıt yazımı düştü** — kayıt yok; defter satırı ve başarı denetimi kalır; kod boşluktur; uyarı logu (AC-54 (b)).
- **Doğrulama hatası** (olmayan üst birim, döngü, başka şirketin üst birimi, olmayan birim) — ayırmadan önce reddedilir, numara
  harcanmaz (AC-54 (c)).
- **Eski kodla çakışma** (silinmiş eski kayıt dahil) — atla + yeniden ayır (≤ 3); tükenirse 409 `RECORD_CODE_COLLISION`, kayıt yok
  (AC-55; Q-1 A).
- **Güncellemede kod değişikliği** (eski ya da yeni kod) — 409 `RECORD_CODE_IMMUTABLE`; kayıt değişmez (AC-53; Q-2 A).
- **Pozisyon başka şirketin birimine taşınır / birimin şirketi değişir** — kod değişmez (§5.2; AC-51, AC-53).
- **Pozisyonun biriminin şirketi aktif değil** — 404 `LEGAL_ENTITY_NOT_FOUND`; pozisyon oluşmaz (bugün oluşurdu — davranış
  değişikliği; AC-51).
- **Başka kiracının şirketi / birimi** — bugünkü doğrulamayla 404; hiçbir kiracıda sayaç ya da defter satırı yok (AC-57).
- **İç uçtan `ORG` / `POS`** — 403 `RECORD_CODE_FORBIDDEN`; sözleşme §6 değişmez (AC-49).
- **Port savunması** — `ITenantContext` kiracısı token kiracısıyla uyuşmuyor, `ActorType ≠ tenant_user` ya da `sub` boş → 403
  `RECORD_CODE_FORBIDDEN`, ayırma gönderilmez (AC-57, AC-58).
- **Başarı yolunda denetim kapısı istisnası** — kayıt yok; 503 / 500 `ProblemDetails` (Bölüm 8.6, 8.14; AC-54 (d)).

## 14. Authorization Convention

- **İç uç:** `[ApiController]`, `[AllowAnonymous]` (çerçeve düzeyinde), `[Route("api/internal/record-codes")]`; kapı
  yürütücüdedir (Bölüm 8.2). `LoginOnlyEndpointGuardTests.AnonymousInternalControllers` listesine yalnız her reddi adı
  konmuş bir testi kırmızıya döndüren controller girer (`:99-113` yorumu). `[HasPermission]` yoktur (çağıran servis);
  yürütücü kullanıcının **türe özgü oluşturma iznini** ister (Bölüm 4.4).
- **Aktör:** denetimde `TenantUser` (iletilen token'ın `sub`'ı), kaynak servis kapalı listeden (K-8r, K-9).
- **İzinli çağıran × tür:** Bölüm 8.2 tablosu.
- **Yönetici ucu (gövdesiz hizalama):** `[Authorize(Policy = "PlatformAdminOnly")]` (r4, inceleme 03 tur 1 F-4) —
  `PlatformActor` **kullanılmaz**: o politika `partner_admin`'i de kabul eder (`AddPlatformActorPolicies`,
  `services/Diten.Platform/src/Diten.Platform.Infrastructure/DependencyInjection.cs`: uç `IPartnerAdminAllowed` taşıyorsa
  ortak yöneticiyi, taşımıyorsa yalnız kaydı doğrulanmış platform yöneticisini alır). `PlatformAdminOnly` yalnız kaydı canlı
  ve ortak kaydı olmayan `platform_admin`'i kabul eder (`IsConfirmedPlatformAdministratorAsync`); emsal
  `TenantsController.RegisterTenant` / `DeleteTenant`. Controller `[PartnerAdminScope]` taşımaz. Ayrıca
  `[HasPermission("platform.record-code-group-prefixes.update")]` (PUT) ve `...read` (GET). `partner_admin` 403, kiracı
  kullanıcısı 403 (ara katman `IsAdminPath` dalı). Değer yönetici tarafından girilmez; etkin belge `OrgPrefix`'inden
  kopyalanır (Bölüm 4.3). Rota kiracısında okuma/yazma `TenantScope.Begin` içinde, denetim `TargetTenantId` ile (Bölüm 4
  "Yönetici ucu", r4 F-1).
- **`HasPermission` anahtarları yönetici ucunda koruma değildir (r7, son bakış 01 F-11; ölçüldü).** `HasPermissionAttribute`
  (`services/Diten.Platform/src/Diten.Platform.API/Security/HasPermissionAttribute.cs`, `OnAuthorizationAsync`) platform
  aktöründe yönetici **kaydını** denetler (yok / silinmiş / etkin değil / ortak yönetici → ret) ve ardından
  `isPlatformActor || PermissionClaimEvaluator.Evaluate(…)` koşuluyla anahtara bakmadan geçirir. Bu uçta koruma
  `PlatformAdminOnly` + o kayıt denetimidir; `platform.record-code-group-prefixes.read` / `.update` yalnız izin kataloğu
  (A1 otomatik kaydı) ve PKS-001 adlandırması için durur, bir platform yöneticisini ayırt etmez. Sabotaj kanıtı bu yüzden
  anahtar üzerinden değil politika üzerinden kurulur (AC-44, S-51).
- **Yönetici ucu cevap kodları (r7, inceleme 03 tur 4 F-2):** 200; 404 `TENANT_NOT_FOUND`; 400 `RECORD_CODE_REQUEST_INVALID`;
  409 `GROUP_PREFIX_SOURCE_INVALID`; 409 `GROUP_PREFIX_LOCKED`; 403 (politika / ara katman). Hepsi `Response<T>` zarfında
  (403 hariç — çerçeve cevabı); cevaptaki `reason_code` denetim satırındaki `Metadata.ReasonCode` ile aynıdır (Tablo D).
- **DL-2 (r12) — süreç içi port.** Yetki iki katmandır: (1) controller `[HasPermission("platform.organization-units.create")]` /
  `[HasPermission("platform.positions.create")]` (`services/Diten.Platform/src/Diten.Platform.API/Controllers/Platform/OrganizationUnitsController.cs:30-33`,
  `PositionsController.cs:35-38`), güncellemede `…organization-units.update` / `…positions.update` (`:35-38`, `:40-43`) — değişmez;
  (2) ayırmanın adım 8'i aynı anahtarı katalogdan (`RequiredPermission`) aynı değerlendiriciyle (`IActorPermissionContext.Has`)
  sorar. Katalog anahtarının controller anahtarıyla aynı olduğunu yansıma testi sabitler (AC-48). Platform aktörü bu yollarda ara
  katmanda reddedilir (`TenantResolutionMiddleware.cs:314-318`); port ayrıca `ActorType = tenant_user` ve dolu `sub` ister (savunma,
  AC-58). Denetim aktörü `TenantUser` + `sub`, kaynak servis `Diten.Platform`. **Yeni izin anahtarı yok.** Veri kapsamı (SEC-002)
  MOD-0288'in bugünkü kuralıdır; DL-2 değiştirmez.

## 15. Gateway / API Routing Decision

**Karar: Gateway değişikliği gereksiz.**

- İç uç Gateway'e açılmaz: `gateway/Diten.ApiGateway/ocelot.json`'da `/internal` geçen rota **0**, genel yakalayıcı yok
  (ölçüldü). Tüketiciler Platform'a servis adresiyle (5057) gider.
- Yönetici ucu mevcut `/api/admin/tenants/{everything}` → 5057 rotasıyla ulaşılır (`ocelot.json` upstream `:1733`).
- `ocelot.json` korunan yol; `RecordCodeGatewayExposureTests` `record-codes` geçen rota olmadığını ölçer.
- **DL-2 (r12):** Gateway değişikliği yok. ORG / POS uçları bugünkü yollarıyla (`/api/platform/organization-units`,
  `/api/platform/positions`) ve bugünkü Web vekiliyle (`frontend/Diten.Web/Controllers/OrganizationUnitsController.cs:66` →
  `{gateway}/api/platform/organization-units/{id}`; `PositionsController.cs` — ikisi de değişmez) ulaşılır; ayırma süreç içidir,
  iç uç Gateway'e açılmaz (AC-21 değişmez).

## 16. Acceptance Criteria

**DL-1 — v1 servis (K-2).** Her maddenin sabotajı maddenin içindedir. Madde 25 DL-4'e aittir (K-11). Madde 41 DL-1'in ilk commit grubudur (r4 F-6).

- [ ] AC-1 (zorunlu) — Uç yalnız `POST /api/internal/record-codes/allocate`'tır; 401 / 403 eşlemesi Bölüm 8.2 tablosuyla birebirdir (başlık eksik/çift, iptal edilmiş kimlik bilgisi, yanlış sır, token yok/geçersiz → 401 `RECORD_CODE_UNAUTHENTICATED`; geçersiz yapılandırma, yanlış hedef kitle ya da tüketici, `X-Tenant-Id`, eksik token iddiaları → 403 `RECORD_CODE_FORBIDDEN`) ve her retle işleyici hiç çağrılmaz; `InternalRecordCodesController` `LoginOnlyEndpointGuardTests.AnonymousInternalControllers` listesindedir.
  Sabotaj: yürütücüdeki her ret dalı tek tek kaldırılınca `TrustedRecordCodeRequestExecutorRefusalTests`'ten adı konmuş bir test kırmızı; iptal edilmiş kimlik bilgisi 403'e çevrilince eşleme testi kırmızı.
- [ ] AC-2 (zorunlu) — Kiracı yalnız doğrulanmış kullanıcı token'ının `tenant_id`'sidir: gövdede `tenantId` üyesi 400, sorgu dizesindeki kiracı yok sayılır, MDM'e giden `X-Tenant-Id` token kiracısıdır.
  Sabotaj: kiracıyı sorgu dizesinden okuyan bir değişiklik konunca `InternalRecordCodesHttpTests` kiracı testi kırmızı.
- [ ] AC-3 (zorunlu) — `LegalEntity` kapsamında MDM `code = MYG` olan şirket için `PAY` ilk ayırması 200 ve `data = { "code": "MYG-PAY-000001", "sequence": 1 }`, ikincisi `MYG-PAY-000002` / `2`; `prefixScope` gönderilmeyen v0 gövdesi aynı sonucu verir.
  Sabotaj: dolgu 5'e indirilince ya da varsayılan kapsam kaldırılınca biçim / HTTP testi kırmızı.
- [ ] AC-4 (zorunlu) — Gerçek Mongo'da aynı `(kiracı, MYG, PAY)` için farklı anahtarlı 50 eşzamanlı ayırma 50 farklı kod ve 50 farklı sequence üretir; sayaç belgesi yokken başlayan ilk yarış 500 üretmez.
  Sabotaj: `$inc` yerine oku-artır-yaz konunca çift kod; upsert yarışı kurtarması kaldırılınca ilk-yarış testi kırmızı.
- [ ] AC-5 (zorunlu) — Aynı anahtar + aynı gövde aynı kodu ve sequence'i döner, sayacı ilerletmez, MDM'i çağırmaz, ikinci defter ve denetim satırı yazmaz; `prefixScope` gönderilmeyen v0 gövdesinden sonra aynı anahtarla açık `"prefixScope": "LegalEntity"` gövdesi 200 ve aynı kodu döner (varsayılan uygulanmış eşitlik, Bölüm 4.2; r4 F-9), `legalEntityId` büyük harfle yazılmış aynı Guid de aynıdır; aynı anahtar + farklı gövde (her alan ayrı: `recordType`, `prefixScope`, `legalEntityId`) 400 `IDEMPOTENCY_KEY_REUSED`. Aynı anahtar yarışı Bölüm 8.6'nın dört durumuyla **ayrı ayrı** gerçek Mongo'da ölçülür; her birinde defterde tek satır, tek başarı denetim satırı kalır ve ikinci istek 200 alır (r4 F-3; durum 3–4 r5, inceleme 03 tur 2 F-2). Ölçüm, üretim deposunu saran bir test sarmalayıcısıyla (`IRecordCodeAllocationRepository` dekoratörü; her çağrıyı, sonucunu ve atılan istisnayı kaydeder) yapılır: (1) ilk okumada bulunan tekrar — kazanan commit olduktan sonra gelen istek 200; sarmalayıcı adım 9 okumasının satırı döndürdüğünü ve `InsertAsync`'in hiç çağrılmadığını kaydeder; (2) işlem içi örtüşme — kazananın commit'i `IPlatformTransactionFaultProbe.BeforeCommitAsync` içinde bir bariyerle tutulur, ikinci istek gövdesine girer; sarmalayıcı ikinci isteğin ilk `WriteConflict`'inde bariyeri bırakır ve kazananın `AfterCommitAsync`'ini bekledikten sonra hatayı yeniden atar; ikinci istek gövde yeniden denemesinde oturumlu yeniden okumayla kazananı bulur ve 200 alır; sarmalayıcı ikinci denemede `InsertAsync` çağrılmadığını kaydeder (`WriteConflict` alınmazsa test kırmızıdır — çıkarım ölçülür); (3) geç gelen — ikinci istek adım 9'u geçtikten sonra sahte MDM okumasında (adım 11) bekletilir, kazanan commit olunca bırakılır; sarmalayıcı gövdedeki oturumlu yeniden okumanın kazananı döndürdüğünü, `InsertAsync`'in çağrılmadığını ve **hiçbir E11000 alınmadığını** kaydeder; (4) savunma E11000 — durum 3'ün sıralamasıyla, ama sarmalayıcı gövdedeki oturumlu yeniden okumayı **bir kez atlatır** (satır yokmuş gibi `null` döner); defter eklemesi E11000 alır ve test, sarmalayıcının kaydettiği istisnanın `RecordCodeIdempotencyConflictException` olduğunu ve iç istisnasının `MongoWriteException` + `WriteError.Category == ServerErrorCategory.DuplicateKey` olduğunu **doğrular** (E11000 yerine `WriteConflict` ya da başka hata gelirse test açık iletiyle kırmızıdır); cevap 200 ve kazananın kodudur. Ayrıca aynı anahtarla 10 eşzamanlı istek tek defter satırı ve tek kod üretir, sayaç en çok 9 (N−1) numara fazla ilerler; bu testte 503 alan bir istek olursa (yürütücü gövdeyi beklemesiz 3 kez dener — Bölüm 8.6 durum 2) aynı anahtarla tekrar 200 ve aynı kodu alır (destekleyici; durum kanıtı 1–4'tür).
  Sabotaj: alan karşılaştırmasından `PrefixScope` çıkarılınca farklı-kapsam testi; varsayılan karşılaştırmadan önce uygulanmayınca v0→açık-`LegalEntity` testi; gövde içi yeniden okuma kaldırılınca durum (2) ve (3) testleri ("`InsertAsync` çağrılmadı" kaydı bozulur) kırmızı; depodaki E11000 → `RecordCodeIdempotencyConflictException` çevirisi kaldırılınca durum (4) testi 503 alır ve kırmızı; idempotency unique index'i kaldırılınca durum (4) testi E11000 alamaz ve kırmızı, 10-istek testi çift satırla kırmızı.
- [ ] AC-6 (zorunlu) — Sayaç `(kiracı, önek, tür)` başınadır ve sıfırlanmaz: `MYG-PAY`, `GPL-PAY`, `MYG-APD` ve B kiracısının `MYG-PAY`'i bağımsız `000001`'den başlar; zorla düşürülen bir ayırmadan sonra bir sonraki kod bir üst numarayı alır (boşluk kalır, hiçbir kod iki kez verilmez); sayaç deposunda azaltan/sıfırlayan üye yok.
  Sabotaj: sayaç anahtarından önek çıkarılınca bağımsızlık testi; başarısızlıkta sayacı geri alan değişiklik konunca boşluk testi kırmızı.
- [ ] AC-7 (zorunlu) — `LastNumber = 999998` ile tohumlanmış sayaçta iki ayırma `MYG-PAY-999999` ve `MYG-PAY-1000000` üretir; sequence `long`.
  Sabotaj: biçim 6 haneye kesilince biçim ve Mongo testi kırmızı.
- [ ] AC-8 (zorunlu) — Uyumsuz kapsam 422 `RECORD_CODE_SCOPE_INVALID` döner, MDM çağrılmaz, sayaç yaratılmaz: `Group` + dolu `legalEntityId`, `LegalEntity` + boş `legalEntityId` ve DL-1'de **tür izni adımını geçmiş** her `prefixScope = Platform` isteği (ör. `PAY` + `Platform` → 422); izinli çağıranı olmayan tür kapsamdan önce reddedilir (ör. `PLN` + `Platform` → 403 `RECORD_CODE_FORBIDDEN`, sözleşme örneğiyle aynı); tanımsız `prefixScope` 400.
  Sabotaj: `Platform` reddi kaldırılınca `PAY` + `Platform` testi; tür izni adımı kapsam adımının arkasına taşınınca `PLN` + `Platform` testi kırmızı.
- [ ] AC-9 (zorunlu) — Tür başına izinli kapsam: `BNK` yalnız `Group`; `BAC` `LegalEntity` (önek sahip şirketin kısa kodu) ve `Group` (grup öneki); `PAY`, `APD`, `ARI`, `CPS`, `CTP`, `AGR` yalnız `LegalEntity`; `PLN` yalnız `Platform`; `ORG` ve `POS` izinli çağıranı olmayan türlerdir ve DL-1'de izinli kapsam kümeleri **boştur** — bu madde onların kapsamını ölçmez (açık karar A-8, DL-2 revizyonunda yeni AC ile; r4 F-7), yalnız izinli çağıran kümelerinin boş olduğunu ölçer.
  Sabotaj: `PAY`'e `Group` izni eklenince katalog tür×kapsam testi; `ORG`'a izinli çağıran eklenince boş-çağıran testi kırmızı.
  **r12 notu (DL-2):** DL-2 kodu girince bu maddenin `ORG` / `POS` cümlesinin yerini AC-48 alır (kapsam `LegalEntity`, çağıran
  yalnız süreç içi `Diten.Platform`); madde silinmez, `PLN` ve diğer türler için aynen geçerlidir. HTTP'den `ORG` / `POS` 403'ü
  (AC-14) DL-2'den sonra da geçerlidir (AC-49).
- [ ] AC-10 (zorunlu) — Grup öneki ayarı hizalanmış (`GMG`, etkin `OrgPrefix` = `GMG`) kiracıda `BNK` ilk ayırması `GMG-BNK-000001`, tedarikçi `BAC` ilk ayırması `GMG-BAC-000001`; ayarı olmayan kiracıda 409 `GROUP_PREFIX_MISSING`, sayaç yaratılmaz; servis grup önekini kendiliğinden (kod sabiti, `OrgPrefix` ya da başka varsayılan) doldurmaz.
  Sabotaj: kayıt yokken `OrgPrefix`'e düşen yedek eklenince 409 testi kırmızı.
- [ ] AC-11 (zorunlu) — MDM `code` `^[A-Z]{2,5}$`'e birebir uymuyorsa (`GMG-CH`, `my`, `M`, `ABCDEF`, `MYĞ`, boş, boşluklu) 409 `LEGAL_ENTITY_SHORT_CODE_MISSING`, sayaç yaratılmaz; normalleştirme yok; `MYG`, `GPL`, `GMT` kabul.
  Sabotaj: okuyucuya `Trim().ToUpperInvariant()` eklenince okuyucu testi kırmızı.
- [ ] AC-12 (zorunlu) — A kiracısının token'ıyla B kiracısının şirketi istenince (MDM 404) cevap 404 `LEGAL_ENTITY_NOT_FOUND`; olmayan ve aktif olmayan şirket için gövde aynı; A ve B kiracılarının sayaç belgeleri ve defter satırları doğrudan okunarak hiçbir satırın oluşmadığı gösterilir; MDM'e giden istekte `X-Tenant-Id` = A ve `Authorization` = iletilen token.
  Sabotaj: MDM isteğine şirketin kiracısını gönderen değişiklik konunca kiracılar arası test kırmızı.
- [ ] AC-13 (zorunlu) — MDM kapalı, 5xx, 3 s aşımı, 400, 401, çelişkili gövde → 503 `LEGAL_ENTITY_SOURCE_UNAVAILABLE`; MDM **403** (kullanıcıda `mdm.legal-entities.read` yok) → **403 `LEGAL_ENTITY_READ_PERMISSION_MISSING`** (kalıcı; tüketici yeniden denemez; `Denied` ret kaydı — r7, son bakış 01 F-10); Mongo gerçekten erişilemez → 503 `RECORD_CODE_UNAVAILABLE`; hiçbir durumda kod dönmez, MDM hatasında sayaç ilerlemez.
  Sabotaj: 5xx'i 404'e çeviren değişiklik konunca okuyucu testi; MDM 403'ü yeniden 503'e çeviren değişiklik konunca kalıcı-izin testi kırmızı.
- [ ] AC-14 (zorunlu) — Katalog dışı tür (`XYZ`, `CTR`, `ACC`, `CMP`, `GP`, `UID`, `IT`, `TTY`, `PUT`) 422 `RECORD_TYPE_UNKNOWN`; biçimsiz tür (`pay`, `PA`, `PAYX`) 400; katalogda olan ama kimlik bilgisinin izinli kümesinde olmayan tür (Mdm ile `PAY`, FinanceTreasury ile `BNK`, herkes için `ORG`, `POS`, `PLN`) 403 `RECORD_CODE_FORBIDDEN`.
  Sabotaj: kimlik bilgisi × tür denetimi kaldırılınca 403 testi kırmızı.
- [ ] AC-15 (zorunlu) — DL-1'de izinli her (tür, kapsam) için üretilen her kod yalnız ASCII `A–Z`, `0–9`, `-` içerir ve `^[A-Z]{2,5}-[A-Z]{3}-[0-9]{6,}$` kalıbına uyar.
  Sabotaj: önek doğrulaması gevşetilip `Ğ` kabul edilince özellik testi kırmızı.
- [ ] AC-16 (zorunlu) — `RecordCodeTypeCatalog` RCS-001 §3'ün bütün OTOMATİK satırlarını (`ORG`, `POS`, `PAY`, `APD`, `ARI`, `CPS`, `CTP`, `BNK`, `BAC`; `CTR` yerine `AGR`) ve §5.3'teki `PLN`'yi birebir içerir; test kuralı `.antigravity/rules/record-coding-standard.md`'den okur; `CTR` → `AGR` adı konmuş tek istisnadır ve §3 güncellenince testten çıkarılmazsa test kırmızıdır; KALSIN türü katalogda yok.
  Sabotaj: kataloğa §3'te olmayan tür eklenince ya da bir OTOMATİK tür çıkarılınca katalog testi kırmızı.
- [ ] AC-17 (zorunlu) — Her yeni ayırma, defter satırıyla **aynı Mongo işleminde** tek denetim kutusu satırı yazar; bu satırın `IdempotencyKey`'i `record-code:allocated:{allocationId:N}`'dir (istemci anahtarı değildir); işleyiciden sonra `audit_events`'te tek satır ve bu satır **Bölüm 21.3 Tablo A**'yla alan alan birebirdir (r4, inceleme 03 tur 1 F-2): `ActorType = TenantUser`, `ActorId` = iletilen token'ın `sub`'ı, `SourceService` = kimlik bilgisinin kapalı listedeki adı, `TenantId` = `TargetTenantId` = token kiracısı, `Category = PlatformConfiguration`, `EntityType = RecordCodeAllocation`, `EntityId` = defter satırının `Id`'si, `Operation = Create`, `Outcome = Succeeded`, `RequestType = AllocateRecordCodeCommand`, `SourceModule = MOD-0040-FU02`, `BeforeState` yok, `AfterState` tam olarak `Code`, `Sequence`, `RecordType`, `PrefixScope`, `Prefix`, `LegalEntityId`, `Metadata` tam olarak `EventName`, `IdempotencyKey`, `GroupPrefixLockedNow` (+ kapının eklediği `ClientCorrelation`, başlık varsa); tekrar yeni satır yazmaz; kutu yazımı bağlantı hatasıyla düşerse işlem iptal, 503, defter satırı yok; işlem içi kutu `false` (sahte depoyla zorlanır) → 503 `RECORD_CODE_UNAVAILABLE` + hata logu, defter satırı yok (r4 F-3, Bölüm 8.6). Aktör ölçümü AC-41'e dayanır. **`GroupPrefixLockedNow` sıralı ölçülür (r11; inceleme 03 tur 8 F-1, son bakış 01 tur 5 F-1; sahip kararı Beste 2026-11-02 "B" — r10'un bariyerli yarış ölçümü kaldırıldı, yürütücüyle kurulamaz; eşzamanlı ilk kilit Bölüm 8.6'da kabul edilen sınırdır ve testle zorlanmaz):** kilitsiz `GMG` ayarlı kiracıda farklı anahtarlı iki `BNK` (`Group`) ayırması **sırayla** gönderilir (ikincisi birincinin 200'ü alındıktan sonra). (1) İlk ayırma 200 alır, başarı satırı `GroupPrefixLockedNow = true` taşır ve ayara `LockedAtUtc` yazılır (ayırmadan önce boş, sonra dolu). (2) Sıradaki ayırma 200 alır, başarı satırı `GroupPrefixLockedNow = false` taşır ve ayarın `LockedAtUtc`'si değişmez — ikinci ayırmadan önce ve sonra okunan değer birincinin yazdığı **tek değerdir**. (2) Bölüm 3'teki `ModifiedCount` çıkarımını da ölçer.
  Sabotaj: kutu yazımı commit sonrasına taşınınca hata yolu testi; aktör sabit `Service`/`System` yapılınca aktör testi; `AfterState`'ten bir alan çıkarılınca ya da bir alan `Metadata`'ya taşınınca Tablo A eşitlik testi; `false`'ta kod döndüren değişiklik konunca `false` testi kırmızı. `GroupPrefixLockedNow` `MatchedCount == 1`'den türetilince (S-73) sıralı testte sıradaki ayırmanın satırı da `true` taşır (zaten kilitli satır eşleşir) ve "sıradaki ayırma `false`" adımı kırmızı (r11); Group ayırmasında sabit `true` yazılınca aynı adım kırmızı.
- [ ] AC-18 (zorunlu) — Merkezi denetim BL-501 ile değil Platform içi işlem içi kapıyla yapılır: işleyici `RecordCodeAuditIntent.EnqueueAsync`'i çağırır ve o her zaman `Intent` taşır (elle yük yok; `TransactionOwnedAuditPayloadExemptions` listesi değişmez); CT'nin defter kararıyla `AuditTrailStandardTests` komutu denetlenen sayar; Platform'a iletici kopyası eklenmez; tüketicinin Create kaydıyla eşleşme `idempotencyKey` + `code` üzerinden.
  Sabotaj: işleyiciden yazıcı çağrısı kaldırılınca mimari test komutu "yeni ve denetimsiz" sayar; muafiyet listesine satır eklenince mevcut muafiyet testi kırmızı.
- [ ] AC-19 (zorunlu) — Ret kaydı Bölüm 21.2 tablosuna birebir uyar: "yazılır" denen her ret ayrı kısa bir işlemle tek denetim satırı yazar (`Outcome` tablodaki `Denied` ya da `Failed`, aktör kullanıcı, kaynak servis kapalı listeden, kutu anahtarı her ret için yeni Guid) ve `Metadata`'sı **Bölüm 21.3 Tablo B**'yle birebirdir: `EventName`, `ReasonCode`, `EvaluationStep` her retde; `IdempotencyKey`, `RecordType`, `PrefixScope`, `LegalEntityId` yalnız adım 6–13 retlerinde — adım 4 (`RECORD_CODE_TENANT_NOT_ACTIVE`) ve adım 5 (400, r7) retlerinde bu anahtarlar **yoktur**; `Prefix` yalnız Tablo B'deki retlerde; `Code` / `Sequence` hiçbir retde (r4 F-2); "yazılmaz" denenler (401, platform aktörü dışında kiracı çözülmeden verilen 403, 413, 429) yalnız loglanır — 400 `RECORD_CODE_REQUEST_INVALID` r7'den itibaren `Failed` yazılır, platform aktörü reddi `Denied` (K-13; AC-44), 429 adlı sapmadır (K-10) ve testte yerine `record_codes_rate_limited_total` sayacının arttığı ölçülür; hiçbir ret defter, sayaç ya da grup öneki satırı yazmaz ve kod döndürmez.
  Sabotaj: 404 dalından ret yazımı kaldırılınca ret testi; 401'de ret yazdıran değişiklik konunca "yazılmaz" testi; adım 4 reddine boş `RecordType` anahtarı yazdıran değişiklik konunca Tablo B adım testi; (r7) 400 `RECORD_CODE_REQUEST_INVALID` dalından ret yazımı kaldırılınca 400-ret testi; 429'da ret yazdıran ya da sayacı artırmayan değişiklik konunca 429 testi kırmızı.
- [ ] AC-20 (zorunlu) — Defter deposunda güncelleme, değiştirme ya da silme üyesi yok; sayaç deposunda yalnız `NextAsync`; grup öneki deposunda yalnız kilitsizken koşullu hizalama ve kilitleme (silme yok); v1'de kodu değiştiren, iptal eden ya da yeniden veren uç yok.
  Sabotaj: defter arayüzüne `DeleteAsync` eklenince yansıma testi kırmızı.
- [ ] AC-21 (zorunlu) — `ocelot.json`'da `record-codes` ya da `/internal` içeren rota yok; `frontend/Diten.Web` altında `record-codes/allocate` çağrısı yok.
  Sabotaj: test kopyasına rota eklenince Gateway testi kırmızı.
- [ ] AC-22 (zorunlu) — Yalnız Platform aktörü gövdesiz `PUT /api/admin/tenants/{tenantId}/record-code-group-prefix` ile kiracının grup öneki ayarını **etkin belge `OrgPrefix`'ine hizalar**; gövde gönderilirse 400; kilitsizken `OrgPrefix` değiştirilip yeniden hizalanınca ayar yeni değere geçer ve aynı işlemde tek denetim satırı yazılır: `BeforeState` = `{ GroupPrefix: <önceki değer>, LockedAtUtc: null }`, `AfterState` = `{ GroupPrefix: <yeni değer>, LockedAtUtc: null }` (Bölüm 21.3 Tablo C, örnek: `GMG` → `GMX`); ilk hizalamada `BeforeState` yoktur ve `Operation = Create`; aynı değerle hizalamada denetim satırı yazılmaz (r5, inceleme 03 tur 2 F-1); ilk `Group` ayırması aynı işlemde `LockedAtUtc`'yi yazar; kilitten sonra farklı `OrgPrefix`'le hizalama 409 `GROUP_PREFIX_LOCKED` ve ayar değişmez, aynı değerle 200 ve değişiklik yok; olmayan ya da silinmiş kiracı 404 (depo çağrılmaz); kiracı kullanıcısı ve `partner_admin` 403 (AC-44); eşzamanlı hizalama + ilk ayırma sessiz üzerine yazma üretmez. Ayar satırı rota kiracısının bağlamında yazılır ve okunur (AC-43; Bölüm 4 "Yönetici ucu").
  Sabotaj: kilit denetimi kaldırılınca kilitli-hizalama testi; kilidin ayırma işleminin dışına taşınması eşzamanlılık testinde kırmızı; (r5 F-1) işleyici `BeforeState`'i boş bırakınca (`null` ya da `GroupPrefix` anahtarı yok) `GMG` → `GMX` testi kırmızı; `BeforeState` `AlignIfUnlockedAsync`'ten **sonra** okunursa (önceki yerine yeni değer) aynı test kırmızı.
- [ ] AC-23 (zorunlu) — İstek ve cevap biçimi `docs/analysis/contracts/drafts/record-code-allocate-v1.0-proposal.md`'deki örneklerle ve hata tablosuyla birebir test edilir: örnek gövdeler kabul, bilinmeyen üye 400, her hata durumunda `statusCode` ve `reason_code` tablodakiyle aynı (401/403 satırları Bölüm 8.2 eşlemesiyle), cevap `data`'sında yalnız `code` ve `sequence`.
  Sabotaj: cevapta `code` → `recordCode` yapılınca sözleşme testi kırmızı.
- [ ] AC-24 (zorunlu) — Üç koleksiyon `SchemaProfile.RecordCodes` ile kayıtlı, `TenantScopedEntity`, Bölüm 4'teki adlı index'ler (kiracı önce, unique olanlar unique) uygulanır; `PlatformSchemaManifestTests` yeşil; mevcut profiller değişmez.
  Sabotaj: `ux_record_code_allocations_tenant_code` unique bayrağı kaldırılınca şema testi ve eşzamanlı ayırmanın kod tekilliği testi kırmızı.
- [ ] AC-25 (zorunlu) — **(DL-4, K-11)** `PLN` bağlanmasıyla Platform kapsamı açıldığında: yalnız izinli kapsamı `Platform` olan tür için, kiracısız sayaç/defter ve platform-global denetim kaydıyla öneksiz `PLN-000001` biçiminde kod verilir; kiracı kapsamlı okumalar kiracısız satırı görmez. DL-1'de bu madde uygulanmaz ve DL-1 kabulünde ölçülmez.
  Sabotaj: (DL-4) kiracı kapsamlı sorguya kiracısız satırı katan filtre konunca kiracı testi kırmızı.
- [ ] AC-26 (zorunlu) — MDM 3 s'de cevap vermezse 503 `LEGAL_ENTITY_SOURCE_UNAVAILABLE` bütçe içinde döner; istek 5 s'yi aşmaz; günlüklerde sır, `Authorization`, JWT yok.
  Sabotaj: MDM bütçesi kaldırılınca geciktirilmiş sahte MDM'le zaman testi kırmızı.
- [ ] AC-27 (zorunlu) — `dotnet build` Diten.Platform PASS; Platform'un **tam** test takımı (filtre yok), mevcut bütün denetim testleri ve `DependencyInjectionSmokeTests` **değiştirilmeden** ve `tests/architecture/TenantArchitecture.ArchitectureTests` yeşil; atlanan test yok; main'deki bilinen kırmızılar aynı gün main'de ölçülüp ayrıca yazılır.
  Sabotaj: yeni bir testin `Skip` ile işaretlenmesi kabul kaydında kırmızı.
- [ ] AC-28 (zorunlu) — Yazıcı genişletmesi (K-9): `TransactionOwnedAuditIntent.SourceService` boşsa kayıttaki `SourceService` `"Diten.Platform"`'dur (mevcut `TransactionOwnedAuditDoorTests` ve `TransactionOwnedAuditDeliveryMongoTests` değişmeden yeşil), doluysa o değerdir; ayırma kaydında değer yalnız kimlik bilgisinin kapalı listedeki adıdır — gövdede `sourceService` üyesi 400, `X-Source-Service` gibi bir başlık okunmaz, kayıt yine kapalı listedeki adı taşır.
  Sabotaj: kaynak servisi gövdeden ya da başlıktan alan değişiklik konunca `TransactionOwnedAuditSourceServiceTests` / HTTP testi kırmızı; varsayılan `"Diten.Platform"` kaldırılınca mevcut kapı testleri kırmızı.
- [ ] AC-29 (zorunlu) — Ret yolu (K-10): `ITransactionalAuditRefusalWriter.TryEnqueueRefusalAsync` yalnız `Denied` ve `Failed` kabul eder (`Succeeded` → geçersiz niyet istisnası), ayırma işleminden **ayrı** bir oturumla yazar, aktörü, kaynak servis kuralı ve doğrulamaları başarı yoluyla aynıdır; yazıcı Application DI'da ayrı `AddScoped` kaydıyla çözülür; ret işleminde defter, sayaç ve grup öneki koleksiyonlarına yazım sıfırdır ve cevapta `data = null`'dır.
  Sabotaj: ret yolu `Succeeded` kabul edince yazıcı testi; ret işleminde defter satırı yazdıran değişiklik konunca Mongo ret testi; ret yazıcısı kaydı kaldırılınca DI testi kırmızı.
- [ ] AC-30 (zorunlu) — Kiracı yüklemi: üç depoda bütün okuma ve yazmalar açık `tenantId` parametresiyle **tam eşitlik** filtresi kullanır; `HybridRepository` ve "`TenantId` null veya kiracı" filtresi kullanılmaz; depo yalnız şu durumlarda `RecordCodeTenantPredicateViolationException` atar (r7; HTTP'de 500, iç cümle gönderilmez — Bölüm 8.6) ve hiçbir satır yazmaz: `tenantId = Guid.Empty`, `ITenantContext.IsPlatformContext = true` (`SetPlatformContext` ile), `tenantId ≠ ITenantContext.TenantId`; platform sistem kiracısı `00000000-0000-0000-0000-000000000001` **reddedilmez**. Kural istisnasızdır: depoyu platform bağlamından çağıran tek yüzey olan yönetici ucu bu yüzden depoyu rota kiracısı doğrulandıktan sonra `TenantScope.Begin(_tenantContext, routeTenantId)` içinde çağırır (AC-43, r4 F-1) — depo platform bağlamını hiçbir uç için kabul etmez.
  Sabotaj: herhangi bir depoya `HybridRepository` tabanı, `Guid.Empty` kabulü ya da platform bağlamı kabulü konunca `RecordCodeTenantIsolationMongoTests` kırmızı; sentinel kimliğini reddeden bir kural eklenince sentinel kiracı testi kırmızı.
- [ ] AC-31 (zorunlu) — Kiracı yüklemleri **ayrı ayrı** sabotajlanır; iki kiracının ikisi de `GMG` grup önekiyle aynı kod metnini üretebileceği için testler kodu değil **B kiracısının defter satırını ve sayaç belgesini** doğrular: (a) sayaç filtresinden `TenantId` çıkarılınca A ve B'nin `(GMG, BNK)` sayacı tek belgeye düşer ve B'nin ayrı sayaç belgesi testi kırmızı; (b) idempotency aramasından `TenantId` çıkarılınca B'nin aynı anahtarlı isteği A'nın satırını döndürür ve "B'de defter satırı var, `TenantId = B`" testi kırmızı; (c) grup öneki okumasından kiracı parametresi çıkarılınca öneki olmayan B kiracısı A'nın önekini alır ve B'nin 409 testi kırmızı; (d) defter yazımında kiracı token yerine bağlamdan alınıp bağlam değiştirildiğinde satır `TenantId`'si testi kırmızı; (e) `X-Tenant-Id` reddi tek başına kaldırılınca 403 testi kırmızı.
  Sabotaj: beş sabotaj tek tek uygulanır, her biri kendi adlı testini kırmızıya döndürür.
- [ ] AC-32 (zorunlu) — Kullanıcı izni: her tür için Bölüm 4.4'teki izin token'da yoksa 403 `RECORD_CODE_PERMISSION_MISSING` döner, kod verilmez, tekrar da reddedilir; izin varsa sürer; kimlik bilgisinin izinli tür kümesi dışındaki tür 403 `RECORD_CODE_FORBIDDEN`. **Ölçüt `HasExactPermission`** (r8, son bakış 01 tur 2 F-4): şu token'lar 403 `RECORD_CODE_PERMISSION_MISSING` alır — izin yalnız büyük/küçük harf farklı (`FINANCE-TREASURY.PAYMENT-BATCHES.CREATE`), izin tek `permission` iddiasında başka bir anahtarla birlikte boşlukla birleşik (`"finance-treasury.payment-batches.read finance-treasury.payment-batches.create"`), izin yalnız `permissions` iddia türünde.
  Sabotaj: izin denetimi kaldırılınca izinsiz-kullanıcı testi; ölçüt `PermissionClaimEvaluator.Evaluate`'e çevrilince büyük/küçük harf, birleşik değer ve `permissions` testleri 200 alır ve kırmızı.
- [ ] AC-33 (zorunlu) — Hız sınırı: sınırlayıcı yürütücü içinde, kimlik bilgisi ve token doğrulandıktan sonra (tüketici servis, kiracı) bölümüyle uygulanır; testte yapılandırılan düşük sınır A kiracısında aşılınca 429 `RECORD_CODE_RATE_LIMITED` (`Response<T>` zarfı) döner, işleyici çağrılmaz, sayaç ilerlemez, denetime yazılmaz (AUD-001 §3'ten adlı sapma, K-10 / K-13), uyarı logu yazılır ve `record_codes_rate_limited_total{consumer_service, tenant, reason="rate_limited"}` bir artar (etiket kümesi K-10); aynı anda B kiracısının istekleri sürer; `PeopleSearchRateLimit.cs` değişmez ve oradaki `RATE_LIMITED` kodu bu uçta görünmez; üretim değeri A-16 ile belirlenir.
  Sabotaj: sınırlayıcı kaldırılınca 429 testi; bölüm anahtarından kiracı çıkarılınca B kiracısı testi kırmızı.
- [ ] AC-34 (zorunlu) — Döndürme ve iptal: `PreviousSecret` `PreviousValidUntilUtc`'ye kadar kabul edilir, sonra 401; `IsRevoked = true` anında 401; sır karşılaştırması sabit zamanlıdır (FU21 `:117` deseni).
  Sabotaj: süre denetimi kaldırılınca süresi geçmiş-eski-sır testi kırmızı.
- [ ] AC-35 (zorunlu) — Grup öneki tek kaynağı: `Group` ayırmasında kiracı ayarı etkin belge `OrgPrefix`'inden farklıysa (ör. kilitten sonra yapılandırma değişmiş) 409 `GROUP_PREFIX_MISMATCH`, kod verilmez, sayaç ilerlemez, hata logu yazılır; `OrgPrefix` kilitli değere geri alınınca ayırma yeniden çalışır; hizalama ucunda etkin `OrgPrefix` kalıba uymuyorsa 409 `GROUP_PREFIX_SOURCE_INVALID`. **Test yapılandırması (r7, son bakış 01 F-8):** etkin değer `IOptions<DocumentCodingOptions>.Value`'dan okunur (süreç başında bir kez bağlanır, değişiklik süreç yeniden başlatılmadan görünmez — Bölüm 8.12); bu yüzden "yapılandırma değişti" senaryosu **aynı veritabanı üzerinde iki ayrı test ana makinesiyle** kurulur: ilk ana makine `DocumentManagement:Coding:OrgPrefix = GMG` ile hizalar ve ilk `Group` ayırmasıyla kilitler; ikinci ana makine bellek içi yapılandırmayla `OrgPrefix = GMX` başlar ve 409 alır; üçüncü ana makine yeniden `GMG` ile başlar ve ayırma çalışır. Aynı ana makinede `IOptions` değeri değiştirilmez.
  Sabotaj: ayırmadaki eşitlik denetimi kaldırılınca uyuşmazlık testi; erişimci `IOptions<DocumentCodingOptions>` yerine kod sabiti `"GMG"` yapılınca ikinci ana makinenin 409 testi kırmızı.
- [ ] AC-36 (zorunlu) — Tüzel kişilik uygunluğu (v1.1 öncesi): sahte MDM yanıtında `approvalStatus` yoksa ya da `APPROVED` ise ayırma sürer; `DRAFT`, `SUBMITTED`, `REJECTED` → 422 `LEGAL_ENTITY_NOT_ELIGIBLE`; boş, `null` ya da tanımsız değer → 503; aktif olmayan şirket her durumda 404. "Alan yok → 503" geçişi MDM v1.1 yayınıyla FU-RCS-15'te yapılır ve bu maddeyi o revizyon günceller.
  Sabotaj: `approvalStatus` denetimi kaldırılınca `REJECTED` testi kırmızı.
- [ ] AC-37 (zorunlu) — Askıdaki kiracı: işleyici (adım 4, Bölüm 8.5) kiracı kaydını `GetTenantStatusQueryHandler` ile aynı kuralla (`ITenantRegistryRepository`, yalnız `Active` canlı) `Active` görmüyorsa (Suspended, Provisioning, Deactivated, yok) cevap 403 `RECORD_CODE_TENANT_NOT_ACTIVE`'dır, idempotent tekrar da reddedilir, sayaç ilerlemez, `Denied` ret kaydı yazılır; kaydın kiracısı askıdaki / sağlanan / devre dışı kiracıda **token kiracısı**, kiracı kaydı **yok ya da silinmiş** iken **platform denetim kiracısı** (`AuditTenantIds.PlatformSystemTenantId`) ve `TargetTenantId` = token kiracısıdır (r9, son bakış 01 tur 3 F-3; Bölüm 21.2); aktif kiracı sürer.
  Sabotaj: kiracı durumu denetimi kaldırılınca askıdaki-kiracı testi kırmızı; olmayan kiracının ret kaydı token kiracısına bağlanınca "olmayan kiracı → platform kaydı" testi (`RecordCodeAuditDeliveryMongoTests`) kırmızı (S-71; r10, son bakış 01 tur 4 F-6).
- [ ] AC-38 (zorunlu) — Varsayılan / tohum kiracı: `00000000-0000-0000-0000-000000000001` kiracısının `tenant_user` token'ıyla (kiracı Active) `PAY` ayırması 200 döner; sayaç ve defter satırı `TenantId = …0001` taşır ve denetim kaydı yazılır.
  Sabotaj: depoya ya da yürütücüye sentinel kimliğini reddeden kural eklenince bu test kırmızı.
- [ ] AC-39 (zorunlu) — Denetim kutusu anahtarı istemci anahtarından türemez: (a) A ve B kiracıları aynı istemci `idempotencyKey`'iyle ayırma yapınca ikisi de 200 alır ve iki ayrı kutu satırı yazılır; (b) aynı istemci anahtarıyla önce 409 alan (ret kaydı yazılmış) istek, sorun giderilince aynı anahtarla tekrarlandığında 200 alır ve başarı kutu satırı yazılır; (c) **commit sonucu bilinmiyor** (r4, inceleme 03 tur 1 F-3): `IPlatformTransactionFaultProbe.AfterCommitAsync` commit olmuş bir denemede `UnknownTransactionCommitResult` etiketli hata atınca yürütücü commit'i aynı oturumda yeniler ve cevap 200 aynı koddur, defterde ve kutuda tek satır vardır; `BeforeCommitAsync` üç denemede de aynı hatayı atınca cevap 503 `RECORD_CODE_UNAVAILABLE`'dır, ardından aynı anahtarla tekrar commit olmamış ilk denemeden sonra **ilk yazım** olarak yeni bir kod alır (emsal `PlatformTransactionUnknownCommitMongoTests`); (d) işlem içi kutu `false` sahte depoyla zorlanınca cevap 503, defter satırı yok, kod yok.
  Sabotaj: kutu anahtarı istemci anahtarından türetilince (a) ve (b) 503 alır ve testler kırmızı; commit yenilemesi kaldırılınca (c)'nin ilk senaryosu 503 alır ve kırmızı; kutu `false`'ta defterden kod döndüren r3 yolu geri konunca (d) kırmızı.
- [ ] AC-40 (zorunlu) — Kaynak servis değer denetimi: kapı `Intent.SourceService` dolu ve `TransactionOwnedAuditSourceServices` kümesinde değilse (`Diten.Unknown`, `diten.mdmservice`, baş/son boşluklu ad) `TransactionOwnedAuditIntentInvalidException` atar ve kutuya hiçbir şey yazmaz; küme `Diten.Platform` ve kimlik bilgisi eşlemesindeki adları içerir, eşlemedeki her ad kümededir (test); `TransactionOwnedAuditIntent` sınıf yorumu yeni kuralı söyler.
  Sabotaj: küme denetimi kaldırılınca küme-dışı-ad testi; eşlemeye kümede olmayan bir ad eklenince eşleme testi kırmızı.
- [ ] AC-41 (zorunlu) — **DL-1'in ilk commit grubu = aktör ölçümünün asgari yolu** (r4, inceleme 03 tur 1 F-6: "ilk adım" sıralaması commit geçmişiyle ölçülür). DL-1 dalının ilk commit grubu **yalnız** şu dosyaları ekler ya da değiştirir: `Controllers/Internal/InternalRecordCodesController.cs` (yalnız `allocate` eylemi), `Security/ITrustedRecordCodeRequestExecutor.cs` + `TrustedRecordCodeRequestExecutor.cs` (yalnız adım 1–2: kimlik bilgisi, açık `HttpContext.AuthenticateAsync(JwtBearerDefaults.AuthenticationScheme)`, `HttpContext.User` ataması, `TenantScope.Begin(_tenantContext, jwtTenant)`), `Security/ITrustedRecordCodeCredentialAuthenticator.cs` + `TrustedRecordCodeCredentialAuthenticator.cs`, `Security/ITrustedRecordCodeJwtContext.cs` + `TrustedRecordCodeJwtContext.cs`, `Configuration/TrustedRecordCodeCredentialOptions.cs`, `Features/RecordCodes/Commands/AllocateRecordCodeCommand.cs`, `Features/RecordCodes/Handlers/CommandHandlers/AllocateRecordCodeHandler.cs` (bu aşamada yalnız `IPlatformTransactionExecutor.ExecuteAsync` içinde `RecordCodeAuditIntent.EnqueueAsync` çağrısı; sayaç, MDM ve defter yok, cevap sabit sınama kodu), `Features/RecordCodes/RecordCodeAuditIntent.cs` (`public static`, bu aşamada yalnız `EnqueueAsync`), `Features/RecordCodes/RecordCodesModels.cs` (yalnız `RecordCodeAllocationResult` — `Code`, `Sequence`), `Features/RecordCodes/RecordCodeReasonCodes.cs` (yalnız adım 1–2'nin kodları: `RECORD_CODE_REQUEST_TOO_LARGE`, `RECORD_CODE_UNAUTHENTICATED`, `RECORD_CODE_FORBIDDEN`), `Models/RecordCodes/AllocateRecordCodeTransportRequest.cs`, kayıt satırları **yalnız** `services/Diten.Platform/src/Diten.Platform.API/Program.cs` (yürütücü, kimlik bilgisi doğrulayıcısı, JWT bağlamı, seçenek bağlama) ile `services/Diten.Platform/src/Diten.Platform.Application/DependencyInjection.cs`'de (işleyici MediatR taramasıyla gelir; bu aşamada ek satır gerekirse yalnız o) — `Diten.Platform.Infrastructure/DependencyInjection.cs` bu grupta **değişmez** (depo ve MDM istemcisi sonraki commit'lerde) (r7, son bakış 01 F-6), `RecordCodes/RecordCodeActorProbeHttpTests.cs` ve — **r5, inceleme 03 tur 2 F-4** — `Authorization/LoginOnlyEndpointGuardTests.cs` (yalnız `AnonymousInternalControllers`'a `"InternalRecordCodesController"` satırı ve gerekçe yorumu) ile `RecordCodes/TrustedRecordCodeRequestExecutorRefusalTests.cs`'in adım 1–2 ret dalları (başlık eksik / çift, kimlik bilgisi doğrulanmadı, kimlik bilgisi reddedildi, kullanıcı token'ı yok / geçersiz, `X-Tenant-Id`, `tenant_id` / `sub` yok, `actor_type ≠ tenant_user`; her biri adlı test, işleyici çağrılmaz). **Seçim: liste girişi ilk commit grubunda; ara kırmızı kabul edilmez.** Gerekçe: (a) controller `[AllowAnonymous]` taşır ve `LoginOnlyEndpointGuardTests.Every_anonymous_action_is_an_allow_listed_internal_api_key_route` listede olmayan anonim controller'ı kırmızı yapar; CT Platform'un tam takımını filtresiz koşar, ilk commit grubunun kanıtı da tam takım yeşilliğidir; (b) listenin kendi kuralı ("A controller enters this list only on those terms": her ret kaldırılınca adlı bir test kırmızı) ancak ret testleri aynı grupta gelirse sağlanır — bu yüzden adım 1–2'nin ret testleri gruba girer, sonraki adımların (3–13) ret testleri o adımların commit'leriyle gelir. Test gerçek HTTP ana makinesinde (`SignedTenantTokenHttpHost` deseni), üretimdeki `CanonicalTransactionalAuditOutboxWriter` ve gerçek Mongo ile kutu satırında `ActorType = TenantUser` ve `ActorId` = iletilen token'ın `sub`'ı olduğunu ölçer (bu aşamada `SourceService` `"Diten.Platform"` olabilir; K-9 genişletmesi sonraki commit'lerdedir). **Kanıt:** kabul kaydı bu commit grubunun kimliğini, grubun dosya listesini (`git show --stat`), testin ve Platform tam test takımının (filtresiz, `LoginOnlyEndpointGuardTests` dahil) o commit'teki yeşil koşusunu yazar; dosya listesi bu maddedekinden genişse madde karşılanmamıştır. **Başarısızlıkta:** sonraki hiçbir DL-1 commit'i yazılmaz; ilk commit grubu dalda `git revert` ile geri alınır (başka kod yoktur), DL-1 durur ve paket yeni revizyona döner.
  Sabotaj: `HttpContext.User` ataması kaldırılınca kapı aktörü çözemez (`TransactionOwnedAuditRefusedException` ya da yanlış aktör) ve ölçüm testi kırmızı.
- [ ] AC-42 (zorunlu) — Kısa kod kullanım kilidi (ara dönem): X şirketi `MYG` önekiyle ayırma yaptıktan sonra MDM kodu `MYX`'e değişirse X için yeni ayırma 409 `LEGAL_ENTITY_SHORT_CODE_CHANGED`; `MYG` daha sonra Y şirketine verilirse Y için ayırma 409 `LEGAL_ENTITY_SHORT_CODE_CHANGED`; ikisinde kod verilmez, sayaç ilerlemez, `Failed` ret kaydı ve hata logu yazılır; X'in kodu `MYG`'ye geri alınınca ayırma yeniden çalışır; Group kapsamlı satırlar bu denetime girmez.
  Sabotaj: defter denetimi kaldırılınca iki senaryo testi de kırmızı.
- [ ] AC-43 (zorunlu) — **Yönetici ucunun kiracı bağlamı, işlem içi denetimi ve ret kayıtları** (r4, inceleme 03 tur 1 F-1; r5, tur 2 F-1; r6, tur 3 F-1): üretimdeki `TenantResolutionMiddleware` ile kurulan gerçek HTTP ana makinesinde `platform_admin` token'ıyla gövdesiz `PUT /api/admin/tenants/{T}/record-code-group-prefix` 200 döner; `record_code_group_prefixes`'te tek satır `TenantId = T` ile yazılır; `GET` aynı satırı döner; denetim **işlem içi kapıdan** yazılır (r5, inceleme 03 tur 2 F-1): `audit_outbox`'ta ayar satırıyla aynı işlemde tek satır (`IdempotencyKey` `record-code:group-prefix-aligned:` önekli) ve teslimden sonra `audit_events`'te tek satır `TenantId = T`, `TargetTenantId = T`, `ActorType = PlatformAdministrator`, `RequestType = AlignRecordCodeGroupPrefixCommand`, `EntityType = RecordCodeGroupPrefix`, yük Bölüm 21.3 Tablo C'yle alan alan birebir; ikinci hizalamada (`GMG` → `GMX`) `BeforeState.GroupPrefix = GMG`; merkezi `AuditBehavior` yolu bu komut için hiçbir kayıt yazmaz; işleyici dönünce bağlam yeniden `IsPlatformContext = true` ve `TenantId = Guid.Empty`'dir (scope geri yüklemesi); başka bir kiracının (`U`) ayar satırı yoktur ve değişmez; kutu yazımı sahte depoyla düşürülünce ayar satırı da yazılmaz (aynı işlem). **404 cevabı (r7, inceleme 03 tur 4 F-2):** `Response<T>` zarfında 404, `reason_code = TENANT_NOT_FOUND` — denetim satırındaki `Metadata.ReasonCode` ile **aynı** kod; 404 testi ikisini birlikte ölçer. **Retler (r6, inceleme 03 tur 3 F-1; sahip kararı (a)):** dört ret de 4xx döner, ayar satırını değiştirmez ve teslimden sonra `audit_events`'te tam bir satır bırakır — `Outcome = Failed`, `ActorType = PlatformAdministrator`, `RequestType = AlignRecordCodeGroupPrefixCommand`, `Metadata.EventName = platform.record-code.group-prefix.align-refused`, `Metadata.ReasonCode` Bölüm 21.3 Tablo D'deki değer; 400 / 409 / 409 için `TenantId = TargetTenantId = T`; olmayan kiracı `X` için 404'te `TenantId` = `AuditTenantIds.PlatformSystemTenantId`, `TargetTenantId = X` ve `X` adına hiçbir kiracı satırı yoktur; `Guid.Empty` rota değerinde 404, `TargetTenantId = null`, `Metadata.RouteTenantId` boş Guid; her retin kutu satırı iş işleminden ayrı bir işlemde yazılır (ret yazıcısını saran test kaydedicisi, ona verilen oturumun iş işleminin oturumundan farklı olduğunu ve iş işleminin hiçbir koleksiyona yazmadığını kaydeder); ret yazımı düşerse cevap aynı 4xx'tir ve uyarı loglanır. `TransactionOwnedAuditCommandGuardTests` değişmeden yeşildir.
  Sabotaj: işleyiciden `TenantScope.Begin` kaldırılınca depo platform bağlamını reddeder ve "PUT 200 + satır `TenantId = T`" testi kırmızı; `TenantScope.Begin` yerine `TenantScope.BeginPlatform` konunca aynı test kırmızı; `typeof(AlignRecordCodeGroupPrefixCommand)` `TransactionOwnedAuditCommands.Authorized`'dan silinince `AuditBehavior` işleyiciden önce reddeder — PUT 200 testi ve `TransactionOwnedAuditCommandGuardTests.The_pipelines_allow_list_is_exactly_the_marked_commands` kırmızı; işleyici `BeforeState`'i boş bırakınca "ikinci hizalama `BeforeState.GroupPrefix = GMG`" testi kırmızı; kutu yazımı işlem dışına (commit sonrasına) taşınınca "kutu düşerse ayar yok" testi kırmızı; scope `using` dışına taşınınca geri yükleme testi kırmızı; (r6) ret yazımı (`EnqueueGroupPrefixAlignRefusalAsync` çağrısı) dört ret dalının herhangi birinden kaldırılınca o retin adlı "ret satırı var" testi kırmızı; bir ret `Failed` yerine `Denied` ya da `Succeeded` yazılınca aynı test kırmızı; 404 kaydı rota kimliğine (`TenantId = X`) bağlanınca "404 kaydı platform denetim kiracısında" testi kırmızı; bir ret doğrulayıcıya (`ValidationBehavior`) geri taşınınca o retin "ret satırı var" testi kırmızı. Testler `TenantRecordCodeGroupPrefixHttpTests` içinde adlandırılır.
- [ ] AC-44 (zorunlu) — **Platform aktörleri** (r4, inceleme 03 tur 1 F-4): yönetici ucunda `partner_admin` token'ı (kaydı ortak yönetici olan) `PUT` ve `GET` için 403 alır, ayar satırı yazılmaz; `platform_admin` 200. İç uçta `platform_admin` ve `partner_admin` token'ıyla (geçerli kimlik bilgisiyle) gelen ayırma 403 `RECORD_CODE_FORBIDDEN` alır, işleyici çağrılmaz ve **ret denetime `Denied` yazılır** (r7, K-13; son bakış 01 F-3): yürütücü `HttpContext.User`'ı ayarladıktan sonra ret yazıcısını çözer; kayıt platform denetim kiracısında (`AuditTenantIds.PlatformSystemTenantId`), `TargetTenantId` = token'daki tam bir `tenant_id` varsa o, yoksa, Guid değilse ya da `Guid.Empty` ise `null` (r9, son bakış 01 tur 3 F-4); `ActorType` = `PlatformAdministrator` / `PartnerAdministrator`; `Metadata.ReasonCode` = `RECORD_CODE_FORBIDDEN`, `EvaluationStep` = 2. Testte düşük sınırla aynı platform token'ından sınırı aşan istekler yine 403 alır ama yeni satır yazmaz ve sayaç (`reason = platform_actor_refusal`) artar (r8, tur 5 F-2). **Ayrı kova (r9, inceleme 03 tur 6 F-1):** `tenant_id = B` taşıyan bir platform aktörü token'ıyla sınırı aşacak kadar ret üretildiği anda B kiracısının `tenant_user` ayırmaları 200 almaya devam eder (kova `platform-actor:` önekli, B'nin kovası boşalmaz). **Test sınıfı ataması (r9, son bakış 01 tur 3 F-7):** yönetici ucu 403 → `TenantRecordCodeGroupPrefixHttpTests`; iç uç 403 ve "işleyici çağrılmaz" → `TrustedRecordCodeRequestExecutorRefusalTests`; `Denied` satırı, kayıt kiracısı ve `TargetTenantId` (dolu / `null`) → `RecordCodeAuditDeliveryMongoTests`; sınır aşımında satır yazılmaması, sayaç ve ayrı kova (B kiracısı etkilenmez) → `RecordCodeAllocateRateLimiterTests` (HTTP + sahte ret yazıcısıyla çağrı sayımı). **Eksik / `Guid.Empty` `tenant_id` — iki evre (r10, son bakış 01 tur 4 F-5; r11, son bakış 01 tur 5 F-2 ile ayrıldı):** (i) *sınır altı* — ret denetime `Denied` satırı olarak yazılır ve satırda `TargetTenantId = null`'dur (`tenant_id` yok, Guid değil ya da `Guid.Empty`); bu evrede sayaç artmaz (K-10: sayaç yalnız sınırlayıcının durdurduğu retler içindir) → `RecordCodeAuditDeliveryMongoTests`. (ii) *sınır üstü* — denetim satırı **yazılmaz** (`TargetTenantId` diye bir değer oluşmaz); `record_codes_rate_limited_total{reason="platform_actor_refusal"}` artar ve `tenant` etiketi token'daki tam `tenant_id`'dir, `tenant_id` yok, Guid değil ya da `Guid.Empty` iken değer `platform`'dur → `RecordCodeAllocateRateLimiterTests`. İki evre de `Guid.Empty` taşıyan platform token'ıyla ölçülür: sınır altında satırın `TargetTenantId`'si, sınır üstünde sayacın `tenant` etiketi.
  Sabotaj: controller'da `PlatformAdminOnly` yerine `PlatformActor` + `[PartnerAdminScope]` konunca `partner_admin` 403 testi; yürütücüdeki `actor_type = tenant_user` denetimi kaldırılınca iç uç `platform_admin` testi; platform aktörü reddinde ret yazımı kaldırılınca ya da `Failed` yazılınca "platform aktörü reddi `Denied` satırı" testi (`RecordCodeAuditDeliveryMongoTests`) kırmızı; platform aktörü reddinin yazımı sınırlayıcıdan önceye alınınca sınır-aşımı testi (satır yazılmamalı) kırmızı; bölüm anahtarından `platform-actor:` öneki kaldırılınca "B kiracısı etkilenmez" testi kırmızı. Testlerin sınıfları yukarıdaki atamadadır. `Guid.Empty` `tenant_id` kova anahtarına / etikete / `TargetTenantId`'ye olduğu gibi yazılınca "`Guid.Empty` → `platform` / `null`" testi (`RecordCodeAuditDeliveryMongoTests` + `RecordCodeAllocateRateLimiterTests`) kırmızı (S-72).
- [ ] AC-45 (zorunlu) — **Doğrulayıcı yok, ret zarfı ve kaydı işleyicide** (r7, son bakış 01 F-1): `Diten.Platform.Application` derlemesinde `AllocateRecordCodeCommand` ya da `AlignRecordCodeGroupPrefixCommand` için hiçbir `IValidator<>` uygulaması yoktur (yansıma testi); biçimi bozuk gövde (bilinmeyen üye, `pay` türü, Guid olmayan `legalEntityId`, `Content-Type` `application/json` değil) gerçek HTTP'de **`Response<T>` zarfında** 400 `RECORD_CODE_REQUEST_INVALID` döner (`data = null`, `reason_code` dolu — `ProblemDetails` değil) ve `audit_events`'te tek `Failed` ret satırı bırakır (`EvaluationStep = 5`); yönetici ucunda gövde gönderilmesi aynı biçimde 400 + `Failed` satır (Tablo D).
  Sabotaj: biçim kurallarından biri bir `AllocateRecordCodeValidator`'a taşınınca `ValidationBehavior` işleyiciden önce `ValidationException` atar, `GlobalExceptionHandler` `ProblemDetails` döner ve ret satırı yazılmaz — zarf testi ve ret satırı testi kırmızı; yansıma testi de kırmızı.
- [ ] AC-46 (zorunlu) — **Kilit filtresi eşleşmezliği** (r7, son bakış 01 F-9; r8, tur 2 F-1 ile yeniden yazıldı): üretimdeki durumu iki gerçek test ana makinesi aynı veritabanında kurar — A (`OrgPrefix = GMG`) ve B (`OrgPrefix = GMX`); kiracının kilitsiz ayarı `GMG`. A'nın `BNK` ayırması adım 11'i geçtikten sonra (A'daki üretim grup öneki deposunu saran test kaydedicisi `GetAsync` döndükten sonra bekletir) B gövdesiz `PUT` ile ayarı `GMX`'e hizalar; A sürünce gerçek `LockAsync` filtresi (`GroupPrefix == GMG`) eşleşmez ve cevap `Response<T>` zarfında **409 `GROUP_PREFIX_MISMATCH`**'tir: kod yok, defter satırı yok, ayar `GMX` ve kilitsiz kalır, `GMG` sayacında tek numara boşluk, `audit_events`'te tek `Failed` ret satırı (`ReasonCode = GROUP_PREFIX_MISMATCH`, `Prefix = GMG`, `EvaluationStep = 13`). A'nın bir sonraki `Group` ayırması adım 11'de aynı 409'u alır (tek kural); B'nin ayırması `GMX-BNK-000001` ile sürer.
  Sabotaj: `LockAsync` filtresinden `GroupPrefix == kullanılan önek` koşulu kaldırılınca A 200 ve `GMG-BNK-…` alır, ayar kilitlenir ve test kırmızı; `RecordCodeGroupPrefixChangedException` yakalanmayınca (ya da `InvalidOperationException` yapılınca) cevap `ProblemDetails` 500 / 400 olur ve zarf testi kırmızı; r7'nin yeniden değerlendirme dalı geri konunca A'nın cevabı yine 409 olmalıdır — dal ölçülebilir bir davranış üretmediği için ayrıca sabotajı yoktur.
- [ ] AC-47 (zorunlu) — **İstisna → cevap eşlemesi** (r8, son bakış 01 tur 2 F-5; inceleme 03 tur 5 F-1): gerçek HTTP'de, iki uç için de ayrı ayrı — (a) yürütücü `PlatformTransactionUnavailableException` atınca (sahte yürütücü) cevap `Response<T>` zarfında **503 `RECORD_CODE_UNAVAILABLE`** (`ProblemDetails` değil), kod yok, `Failed` ret satırı denenir; (b) başarı yolunda kapı `TransactionOwnedAuditRefusedException` atınca (aktörsüz principal) cevap **503 `ProblemDetails`, `reason_code = AUDIT_RECORD_UNAVAILABLE`**, defter ve ayar satırı yok; (c) kapı `TransactionOwnedAuditIntentInvalidException` atınca (küme dışı `SourceService`, AC-40) cevap **500 `ProblemDetails`, `AUDIT_INTENT_INVALID`**, iç cümle yok; (d) ret yolunda ret yazıcısı bu iki istisnadan birini ya da `PlatformTransactionUnavailableException`'ı atınca cevap reddin kendi cevabıdır (ör. 409 `GROUP_PREFIX_MISSING`, hizalamada 409 `GROUP_PREFIX_LOCKED`) ve uyarı logu yazılır; (e) depo kiracı yüklemi ihlali 500 "Server Error", iç cümle yok. Sözleşme §4'teki iki `ProblemDetails` satırıyla birebir.
  Sabotaj: işleyiciden `PlatformTransactionUnavailableException` yakalaması kaldırılınca (a) 500 `ProblemDetails` alır ve kırmızı; işleyici `TransactionOwnedAuditRefusedException`'ı yakalayıp zarfa çevirince (b) kırmızı; ret yolunda yakalama kaldırılınca (d) 503 alır ve kırmızı; `RecordCodeTenantPredicateViolationException` `InvalidOperationException` yapılınca (e) 400 ve iç cümle alır, kırmızı.

**DL-2 — ORG / POS süreç içi bağlanma (r12, K-15).** Numaralar AC-47'den devam eder. Testler Bölüm 17'deki sınıflardadır; her
maddenin sabotajı maddenin içinde ve Bölüm 17 sabotaj listesinde (S-74…S-87). "Q-n A" ile işaretli maddeler o sorunun önerilen
cevabına göre yazıldı; sahip başka seçeneği seçerse madde DL-2 kodundan önce yeni revizyonla değişir.

- [ ] AC-48 (zorunlu, DL-2) — **Katalog:** `ORG` ve `POS`'un izinli kapsamı yalnız `LegalEntity`, `AllowedCaller = "Diten.Platform"`, `RequiredPermission` sırasıyla `platform.organization-units.create` / `platform.positions.create`; bu iki anahtar `OrganizationUnitsController` ve `PositionsController`'ın `[HttpPost]` eylemlerindeki `[HasPermission]` değeriyle birebirdir (yansıma); HTTP eşlemesi `TrustedRecordCodeCredentialAuthenticator.Consumers` `ORG` / `POS` içermez ve `Diten.Platform` dışındaki her katalog çağıranı eşlemeyle birebirdir; `TransactionOwnedAuditSourceServices.Names` üç ad olarak kalır. Testler `RecordCodeTypeCatalogTests.AC48_org_and_pos_allow_only_legal_entity_and_only_the_in_process_caller`, `RecordCodeTypeCatalogTests.AC48_the_catalogue_permission_is_the_controller_s_has_permission_key`; DL-1'in `AC09_…` ve `AC14_the_consumer_map…` testleri yalnız ORG / POS ve `Diten.Platform` için güncellenir.
  Sabotaj: `ORG` kapsamına `Group` eklenince kapsam testi; `POS` izni `platform.positions.update` yapılınca yansıma testi; `ORG` `Mdm` bağlamasına eklenince eşleme testi kırmızı (S-74, S-75).
- [ ] AC-49 (zorunlu, DL-2) — **HTTP yüzeyi değişmez:** iç uca her iki kimlik bilgisiyle ve `platform.organization-units.create` / `platform.positions.create` taşıyan kullanıcı token'ıyla `ORG` ya da `POS` gövdesi 403 `RECORD_CODE_FORBIDDEN` alır (sözleşme 1.0.0 §6), sayaç ve defter satırı oluşmaz, `Denied` ret satırı yazılır (`EvaluationStep = 7`); kabul kaydı `git diff` ile sözleşme dosyasının değişmediğini gösterir. Test `InternalRecordCodesHttpTests.AC49_org_and_pos_stay_forbidden_over_http_after_dl2`.
  Sabotaj: `ORG` `FinanceTreasury` bağlamasına eklenince test 200 alır ve kırmızı (S-75).
- [ ] AC-50 (zorunlu, DL-2) — **ORG oluşturma:** MDM kısa kodu `MYG` olan şirket için kiracı kullanıcısı kodsuz `POST /api/platform/organization-units` gönderir → 201; kayıt `Code = MYG-ORG-000001`, ikincisi `MYG-ORG-000002`; `GET` aynı kodu döner; defterde her biri için tek satır: `CallerService = Diten.Platform`, `IdempotencyKey = organization-unit:{kayıtId:N}`, `RecordType = ORG`, `PrefixScope = LegalEntity`, `LegalEntityId` = birimin şirketi, `TenantId` = token kiracısı; oluşturma yolunda `OrganizationCodeNormalizer` çağrılmaz. Test `OrganizationRecordCodeHttpTests.AC50_org_unit_create_takes_MYG_ORG_000001_then_000002` (gerçek HTTP, üretim `TenantResolutionMiddleware`, gerçek Mongo, sahte MDM).
  Sabotaj: işleyici kodu istekten / normalleştiriciden almaya döndürülünce kod adımı; anahtar kayıt kimliği yerine yeni Guid'den türetilince "defter anahtarı = kayıt kimliği" adımı kırmızı (S-76, S-77).
- [ ] AC-51 (zorunlu, DL-2) — **POS oluşturma ve şirket kaynağı:** pozisyonun şirketi oluşturma anındaki biriminin `LegalEntityId`'sidir: `MYG` şirketinin birimine açılan ilk pozisyon `MYG-POS-000001`, `GPL` şirketinin birimine açılan `GPL-POS-000001`; ORG ve POS sayaçları ayrıdır (pozisyon `MYG-ORG-…` sayacını ilerletmez); pozisyon `GPL` birimine taşınınca (`PUT`, saklı kodla) kod `MYG-POS-000001` kalır; birimin şirketi MDM'de aktif değilse 404 `LEGAL_ENTITY_NOT_FOUND`, pozisyon oluşmaz; defter anahtarı `position:{kayıtId:N}`. Test `OrganizationRecordCodeHttpTests.AC51_position_takes_its_unit_s_company_prefix_and_keeps_it_after_a_move`.
  Sabotaj: şirket kaynağı birim dışına (istek, kullanıcının `LegalEntityId` iddiası) çevrilince ya da taşımada kod yeniden ayrılınca test kırmızı (S-78).
- [ ] AC-52 (zorunlu, DL-2) — **Oluşturmada kod kabul edilmez:** ORG ya da POS gövdesinde dolu `code` → `Response<T>` zarfında 400 `RECORD_CODE_SYSTEM_ASSIGNED` (`reason_code` dolu, `ProblemDetails` değil); sayaç belgesi ve defter satırı oluşmaz, kayıt yok; merkezi denetimde `CreateOrganizationUnitCommand` / `CreatePositionCommand` satırı `Outcome = Failed`; `code` yok ya da boş → AC-50 / AC-51 akışı. Test `OrganizationRecordCodeHttpTests.AC52_a_create_that_carries_a_code_is_refused_and_allocates_nothing`.
  Sabotaj: dolu kodu sessizce yok sayan değişiklik konunca 201 alınır; ret doğrulayıcıya taşınınca `ProblemDetails` döner — iki adım da kırmızı (S-79).
- [ ] AC-53 (zorunlu, DL-2; Q-2 A) — **Kod değişmez:** ORG ve POS `PUT`'unda saklı koddan farklı `code` → 409 `RECORD_CODE_IMMUTABLE`, kayıt değişmez; saklı koda birebir eşit ya da boş `code` → 204 ve kod aynı; `PUT …/reporting-lines` ve tüm kaydı geri gönderen güncelleme kodu değiştirmez; ORG'nin şirketi değiştirilince kod aynı kalır (§5.2); kural eski (elle) kodlu kayıtlarda da geçerlidir (tohumlanmış `HEADQUARTERS`, `CEO`). Test `OrganizationRecordCodeHttpTests.AC53_update_cannot_change_an_old_or_a_new_code`.
  Sabotaj: güncelleme işleyicisinde `entity.Code = canonicalCode` (`UpdateOrganizationUnitCommandHandler.cs:145` / `UpdatePositionCommandHandler.cs:49`) geri konunca test kırmızı (S-80).
- [ ] AC-54 (zorunlu, DL-2) — **Sıra ve başarısızlık:** (a) ayırma reddi — sahte MDM `code` kalıba uymuyor → 409 `LEGAL_ENTITY_SHORT_CODE_MISSING`; kiracı askıda → 403 `RECORD_CODE_TENANT_NOT_ACTIVE`; ayırma işlemi düşer → 503 `RECORD_CODE_UNAVAILABLE` — ORG / POS cevabı aynı durum ve `reason_code`, koleksiyonda kayıt yok, `audit_events`'te tek `platform.record-code.allocation-refused` satırı (`SourceService = Diten.Platform`) ve merkezi `Failed` oluşturma satırı; (b) ayırma başarılı, kayıt yazımı düşer (kayıt deposunu saran test sarmalayıcısı `CreateAsync`'te istisna atar) → kayıt yok, defter satırı ve başarı denetim satırı kalır, sonraki oluşturma bir üst numarayı alır (boşluk), uyarı logu; (c) doğrulama hatası (olmayan üst birim, döngü, olmayan birim) ayırmadan önce reddedilir, sayaç ilerlemez; (d) başarı yolunda denetim kapısı `TransactionOwnedAuditRefusedException` → 503 `ProblemDetails` `AUDIT_RECORD_UNAVAILABLE`, kayıt yok. Testler `OrganizationRecordCodeHttpTests.AC54a_…`, `AC54b_…`, `AC54c_…`, `AC54d_…`.
  Sabotaj: kayıt yazımı ayırmadan önceye alınınca (a)'nın "kayıt yok" adımı; ayırma reddinde normalleştirilmiş elle koda düşen yedek konunca (a); doğrulama ayırmadan sonraya taşınınca (c)'nin "sayaç ilerlemez" adımı kırmızı (S-81, S-82).
- [ ] AC-55 (zorunlu, DL-2; Q-1 A) — **Eski kodlar korunur, çakışma atlanır:** tohumlanmış eski kodlar (`HEADQUARTERS`, `CEO`) DL-2'den sonra aynen okunur; kiracıda `MYG-ORG-000001` kodlu **silinmiş** eski bir birim varken ilk ORG oluşturma `MYG-ORG-000002` alır, `000001` defterde ayrılmış ama kullanılmamış kalır, uyarı logu yazılır; `000001`…`000003` eski kayıtlarca tutuluyorsa 409 `RECORD_CODE_COLLISION`, kayıt yok. DL-2 kabul kaydı her kiracı için desene (`^[A-Z]{2,5}-(ORG|POS)-[0-9]{6,}$`) uyan eski kod sayısını yazar. Test `OrganizationRecordCodeHttpTests.AC55_a_legacy_code_on_the_counter_s_path_is_skipped_even_when_soft_deleted`.
  Sabotaj: çakışma araması yalnız canlı satırlara (`ExistsByCodeAsync`) çevrilince silinmiş eski kodun numarası yeniden kullanılır ve "`000002`" adımı kırmızı; atlama kaldırılınca canlı eski kodlu senaryoda birim oluşmaz ve test kırmızı (S-83).
- [ ] AC-56 (zorunlu, DL-2) — **Kısa kod kilidi ORG / POS'ta:** X şirketi `MYG` ile `PAY` (HTTP, FinanceTreasury kimlik bilgisi) ve `ORG` almışken MDM kodu `MYX`'e çevrilince X için ORG ve POS oluşturma 409 `LEGAL_ENTITY_SHORT_CODE_CHANGED`; kayıt yok, sayaç ilerlemez, `Failed` ret satırı (`EvaluationStep = 11`, `Prefix = MYX`), hata logu; kod `MYG`'ye geri alınınca ORG oluşturma `MYG-ORG-000002` ile sürer. Test `OrganizationRecordCodeHttpTests.AC56_the_interim_short_code_lock_stops_org_and_pos_creates`.
  Sabotaj: ORG işleyicisi 409'da normalleştirilmiş elle koda düşünce birim oluşur ve test kırmızı (S-81).
- [ ] AC-57 (zorunlu, DL-2) — **Kiracı izolasyonu:** A kiracısının kullanıcısı B kiracısının şirketiyle ORG ya da B kiracısının birimiyle POS isterse 404 (bugünkü doğrulama), ayırma gönderilmez; A ve B'nin `record_code_counters` ve `record_code_allocations` koleksiyonları doğrudan okunarak hiçbir satırın oluşmadığı gösterilir; başarılı ayırmanın sayaç ve defter satırı `TenantId` = token kiracısıdır; port `ITenantContext.TenantId ≠ ITenantAuthorizationContext.TenantId` iken ayırma göndermeden 403 `RECORD_CODE_FORBIDDEN` döner. Testler `OrganizationRecordCodeHttpTests.AC57_cross_tenant_company_or_unit_is_404_and_allocates_nothing`, `InProcessRecordCodeAllocatorTests.AC57_a_contradicting_tenant_context_never_reaches_the_allocator`.
  Sabotaj: port kiracıyı şirketin / birimin satırından alınca birim testi; ORG şirket doğrulaması ayırmadan sonraya taşınınca "ayırma gönderilmez" adımı kırmızı (S-82, S-84).
- [ ] AC-58 (zorunlu, DL-2) — **Yetki:** `platform.organization-units.create` / `platform.positions.create` taşımayan kiracı kullanıcısı 403 alır (çerçeve, `[HasPermission]`), ayırma yok; port adım 8'i `IActorPermissionContext.Has` ile sorar — izin yoksa (birim testi, sahte bağlam) 403 `RECORD_CODE_PERMISSION_MISSING` ve `Denied` ret satırı; `ActorType ≠ tenant_user` ya da `UserId = Guid.Empty` → ayırma göndermeden 403 `RECORD_CODE_FORBIDDEN`; `platform_admin` token'ı ORG / POS uçlarında ara katmanda reddedilir (bugünkü davranış, değişmez). Testler `InProcessRecordCodeAllocatorTests.AC58_*`, `OrganizationRecordCodeHttpTests.AC58_a_user_without_the_create_key_allocates_nothing`.
  Sabotaj: port `HasRequiredPermission = true` sabitlenince izinsiz-bağlam birim testi; controller'dan `[HasPermission]` kaldırılınca HTTP 403 testi kırmızı (S-85).
- [ ] AC-59 (zorunlu, DL-2) — **Denetim (AUD-001):** başarılı ORG oluşturma `audit_events`'te iki satır bırakır: (1) merkezi `CreateOrganizationUnitCommand` (`Outcome = Succeeded`, `platform-merkezi`; bugünkü gibi) ve (2) Bölüm 21.3 Tablo A satırı: `RequestType = AllocateRecordCodeCommand`, `SourceService = Diten.Platform`, `ActorType = TenantUser`, `ActorId` = token `sub`, `AfterState.Code` = kaydın kodu, `AfterState.LegalEntityId` = birimin şirketi, `Metadata.IdempotencyKey = organization-unit:{kayıtId:N}`, `Metadata.GroupPrefixLockedNow = false`; POS için `position:{kayıtId:N}`; retler Bölüm 21.2'ye göre; `tests/architecture/audit-ledger/Diten.Platform.md` değişmez ve `AuditTrailStandardTests` değişmeden yeşildir (yeni komut, yeni belirteç yok). Test `OrganizationRecordCodeHttpTests.AC59_an_org_create_leaves_the_central_row_and_the_allocation_row`.
  Sabotaj: port `CallerService`'i kapalı küme dışı bir ad yapınca kapı `TransactionOwnedAuditIntentInvalidException` atar, kayıt oluşmaz ve test kırmızı; anahtar kayıt kimliği taşımayınca `IdempotencyKey` adımı kırmızı (S-77, S-86).
- [ ] AC-60 (zorunlu, DL-2; Q-3 A) — **Ekran:** ORG ve POS oluşturma formunda kod girişi salt okunur ve boştur, yükte `code` yoktur, zorunlu listede `ouCode` / `pCode` yoktur; düzenleme formunda kod salt okunur ve saklı değeri gösterir; `reason_code`'u Bölüm 11 listesinde olan cevapta yerelleştirilmiş metin gösterilir (`errors[]` değil); Bölüm 11'deki anahtarlar iki kaynak dosyasında **7 dilin her birinde** vardır; iki form `Layout = "_LayoutTenantShell"` satırını değişmeden taşır. Testler `frontend/Diten.Web/tests/organization-record-code-form.test.js` (yeni) ve `frontend/Diten.Web/tests/xmc-date-guard-screens.test.js` (zorunlu listeden `ouCode` / `pCode` çıkar).
  Sabotaj: kod girişi düzenlenebilir yapılınca; bir anahtar `ar` dosyasından silinince; `form.js` `code` göndermeye döndürülünce — yeni testin adlı adımı kırmızı (S-87).
- [ ] AC-61 (zorunlu, DL-2) — **Takım:** `dotnet build` Diten.Platform PASS; Platform'un tam test takımı filtresiz; DL-1 kayıt kodu testleri — `RecordCodeTypeCatalogTests`'teki iki ORG / POS testi dışında — ve AC-23 (donmuş sözleşme) değişmeden yeşil; MOD-0288 testlerindeki değişiklik yalnız Bölüm 5 "DL-2" listesindeki dosya ve satırlarda (kurucu + kod semantiği) ve kabul kaydında satır satır gerekçeli; Web node testleri yeşil; atlanan test yok; main'deki bilinen kırmızılar aynı gün main'de ölçülüp ayrıca yazılır.
  Sabotaj: yeni bir testin `Skip` ile işaretlenmesi kabul kaydında kırmızı.

**DL-3…DL-10.** Kabul ölçütleri her dilimin kendi paket revizyonunda yeni numarayla eklenir (numaralar silinmez,
yeniden numaralanmaz).

## 17. Test Expectations

| Test sınıfı | Tür | Kapsadığı kabul ölçütleri |
|---|---|---|
| `RecordCodeActorProbeHttpTests` | gerçek HTTP + üretim yazıcısı + gerçek Mongo — **DL-1'in ilk commit grubu** | AC-41 |
| `RecordCodeFormatTests` | birim | AC-3, AC-7, AC-15 |
| `RecordCodeTypeCatalogTests` | birim (RCS-001 §3'ü `RepoPaths` ile okur) | AC-9, AC-14, AC-16 |
| `AllocateRecordCodeHandlerTests` | birim (sahte depo/okuyucu) | AC-5 (alan alan eşitlik), AC-8, AC-10, AC-13, AC-19, AC-35, AC-42 |
| `RecordCodeAllocationMongoTests` | gerçek Mongo replica set + `IPlatformTransactionFaultProbe` bariyeri + üretim deposunu saran kaydedici / atlatıcı test sarmalayıcısı | AC-4, AC-5 (dört yarış durumu, v0→açık kapsam), AC-46 (kilit filtresi, iki ana makine), AC-17 (`GroupPrefixLockedNow` sıralı ölçümü — ilk `Group` ayırması `true` + `LockedAtUtc` yazılır, sıradaki `false` + `LockedAtUtc` değişmez; r11, bariyer kullanmaz), AC-6, AC-7, AC-20, AC-22 (kilit), AC-24, AC-39 (c), AC-42 |
| `RecordCodeTenantIsolationMongoTests` | gerçek Mongo, iki kiracı + sentinel kiracı | AC-12, AC-30, AC-31, AC-38 |
| `RecordCodeAuditDeliveryMongoTests` | gerçek Mongo + üretimdeki kutu işleyicisi | AC-17 (Tablo A eşitliği), AC-18, AC-19 (Tablo B, adım 4 dahil), AC-29, AC-37 (olmayan kiracı → platform kaydı), AC-39 (a, b, d), AC-44 (platform aktörü `Denied` satırı, kayıt kiracısı, `TargetTenantId` dolu / `null`; sınır altı evre: `Guid.Empty` → `null` — r11) |
| `InternalRecordCodesHttpTests` | gerçek HTTP (`SignedTenantTokenHttpHost` deseni) + sahte MDM | AC-1, AC-2, AC-3, AC-8, AC-12, AC-13, AC-23, AC-26, AC-28, AC-32, AC-37, AC-38, AC-45 (zarf + yansıma: doğrulayıcı yok), AC-47 (istisna eşlemesi, iç uç) |
| `TrustedRecordCodeRequestExecutorRefusalTests` | birim, her ret dalı | AC-1, AC-33 (429 + sayaç), AC-34, AC-44 (iç uç 403, işleyici çağrılmaz) |
| `RecordCodeAllocateRateLimiterTests` | birim + HTTP (+ sahte ret yazıcısı, çağrı sayımı) | AC-33, AC-44 (platform aktörü retlerinin sınırlayıcıdan geçmesi, ayrı `platform-actor:` kovası, B kiracısı etkilenmez, sayaç `reason`; sınır üstü evre: `Guid.Empty` `tenant_id` taşıyan platform token'ıyla satır yazılmaz ve sayaç `tenant="platform"` etiketiyle artar — r11, S-72) |
| `MdmLegalEntityShortCodeReaderTests` | birim, sahte HTTP | AC-11, AC-13, AC-26, AC-36 |
| `RecordCodeGatewayExposureTests` | dosya ölçümü | AC-21 |
| `TenantRecordCodeGroupPrefixHttpTests` | gerçek HTTP + üretim `TenantResolutionMiddleware` + üretim `AddPlatformActorPolicies` + üretim `AuditBehavior` ve `CanonicalTransactionalAuditOutboxWriter` + gerçek Mongo | AC-22 (`BeforeState`), AC-35 (üç ana makine), AC-43 (başarı, dört ret satırı, 404'ün platform kaydı ve `TENANT_NOT_FOUND`), AC-44 (yönetici ucu), AC-45 (yönetici 400), AC-47 (istisna eşlemesi, hizalama) |
| `TransactionOwnedAuditSourceServiceTests` | birim + Mongo | AC-28, AC-40 |
| `TransactionalAuditRefusalWriterTests` | birim + Mongo + DI | AC-29 |
| `LoginOnlyEndpointGuardTests` (liste), `DependencyInjectionSmokeTests` (değişmeden) | mimari / DI | AC-1, AC-27, AC-29, AC-41 (liste girişi ilk commit grubunda) |
| `TransactionOwnedAuditCommandGuardTests` (değişmeden) | mimari | AC-43 (liste = işaretli komutlar; işleyicide yazıcı kapısı) |
| `AuditTrailStandardTests` (CT sabiti) | mimari | AC-18 |
| Tam takım + mimari testler | CI | AC-27 |
| `RecordCodeTypeCatalogTests` (M, **DL-2**) | birim + yansıma (controller `[HasPermission]` değeri) | AC-48 (DL-1'in `AC09_…`, `AC14_…` testleri yalnız ORG / POS için) |
| `InternalRecordCodesHttpTests` (M, **DL-2**) | gerçek HTTP | AC-49 |
| `InProcessRecordCodeAllocatorTests` (N, **DL-2**) | birim (sahte `ISender`, sahte `ITenantContext` / `ITenantAuthorizationContext` / `IActorPermissionContext`) | AC-57 (kiracı savunması), AC-58 (izin, aktör) |
| `OrganizationRecordCodeHttpTests` (N, **DL-2**) | gerçek HTTP + üretim `TenantResolutionMiddleware`, `AuditBehavior`, `CanonicalTransactionalAuditOutboxWriter` + gerçek Mongo + sahte MDM + kayıt deposunu saran test sarmalayıcısı (AC-54 (b)) | AC-50…AC-59 |
| `TenantOrganizationRulesTests`, `OrganizationEnterpriseFieldsTests`, `OrganizationMatrixReportingTests` (M, **DL-2**, XMC) | birim | AC-61 (kurucu; kod semantiği AC-52 / AC-53'e çevrilir) |
| `organization-record-code-form.test.js` (N, **DL-2**), `xmc-date-guard-screens.test.js` (M, **DL-2**, XMC) | Web node | AC-60 |
| Tam takım (Platform + Web node) (**DL-2**) | CI | AC-61 |

**Sabotaj listesi (kabul kaydına koşu kanıtıyla; her satır tek tek uygulanıp geri alınır):**

| # | Sabotaj | Kırmızıya dönmesi gereken |
|---|---|---|
| S-1 | Kimlik bilgisi reddi kaldırılır | ret testleri (adlı) |
| S-2 | Kullanıcı token'ı reddi kaldırılır | aynı |
| S-3 | İptal edilmiş kimlik bilgisi 403'e çevrilir | 401/403 eşleme testi |
| S-4 | `$inc` yerine oku-artır-yaz | eşzamanlı ayırma testi |
| S-5 | Upsert yarışı kurtarması kaldırılır | ilk-yarış testi |
| S-6 | Alan karşılaştırmasından bir alan (`PrefixScope`) çıkarılır (r4: birleştirilmiş parmak izi kaldırıldı) | farklı-kapsam testi |
| S-7 | İdempotency unique index'i kaldırılır | eşzamanlı-anahtar testi |
| S-8 | Geçici çakışma yeniden denemesi kaldırılır | geçici-çakışma testi |
| S-9 | Sayaç anahtarından önek çıkarılır | bağımsızlık testi |
| S-10 | Biçim 6 haneye kesilir | 999999 testi |
| S-11 | `PAY`'e `Group` izni | katalog testi |
| S-12 | Grup öneki yokken `OrgPrefix`'e düşme | 409 testi |
| S-13 | Kısa koda `Trim().ToUpperInvariant()` | okuyucu testi |
| S-14 | MDM 5xx → 404 | okuyucu 503 testi |
| S-15 | Kimlik bilgisi × tür denetimi kaldırılır | 403 testi |
| S-16 | Kataloğa §3 dışı tür | katalog sabitleme testi |
| S-17 | Kutu yazımı commit sonrasına | denetim hata yolu testi |
| S-18 | Aktör sabit `Service` yapılır | aktör testi |
| S-19 | İşleyiciden `RecordCodeAuditIntent.EnqueueAsync` kaldırılır | `AuditTrailStandardTests` |
| S-20 | 404'te ret yazımı kaldırılır / 401'de ret yazılır | ret tablosu testleri |
| S-21 | Defter arayüzüne `DeleteAsync` | yansıma testi |
| S-22 | Gateway rotası (test kopyası) | Gateway testi |
| S-23 | Grup öneki kilit denetimi kaldırılır | kilitli-hizalama testi |
| S-24 | Cevap alanı yeniden adlandırılır | sözleşme testi |
| S-25 | `(TenantId, Code)` unique kaldırılır | şema testi |
| S-26 | MDM bütçesi kaldırılır | zaman testi |
| S-27 | Kaynak servis gövdeden / başlıktan alınır | kaynak servis testi |
| S-28 | Yazıcıdaki `"Diten.Platform"` varsayılanı kaldırılır | `TransactionOwnedAuditDoorTests.cs:55`, `TransactionOwnedAuditDeliveryMongoTests.cs:73` |
| S-29 | Ret yolu `Succeeded` kabul eder / defter yazar | ret yazıcı testleri |
| S-30 | Depoya `HybridRepository`, `Guid.Empty` kabulü ya da platform bağlamı kabulü; ya da sentinel kimliğinin reddi | kiracı izolasyon testi / sentinel kiracı testi |
| S-31 | Sayaç filtresinden `TenantId` çıkarılır | AC-31 (a) |
| S-32 | İdempotency aramasından `TenantId` çıkarılır | AC-31 (b) |
| S-33 | Grup öneki okumasından kiracı parametresi çıkarılır | AC-31 (c) |
| S-34 | Defter yazımında kiracı bağlamdan alınır | AC-31 (d) |
| S-35 | `X-Tenant-Id` reddi tek başına kaldırılır | AC-31 (e) |
| S-36 | Kullanıcı izni denetimi kaldırılır | izinsiz-kullanıcı testi |
| S-37 | Hız sınırlayıcı kaldırılır / bölümden kiracı çıkarılır | 429 testi / B kiracısı testi |
| S-38 | Eski sır süre denetimi kaldırılır | döndürme testi |
| S-39 | Ayırmada `OrgPrefix` eşitliği kaldırılır | uyuşmazlık testi |
| S-40 | `approvalStatus` denetimi kaldırılır | onaysız şirket testi |
| S-41 | Kiracı durumu denetimi kaldırılır | askıdaki kiracı testi |
| S-42 | Kutu anahtarı istemci anahtarından türetilir | AC-39 (a), (b) |
| S-43 | (r4 F-3 ile değişti) Kutu `false`'ta defterden kod döndüren r3 yolu geri konur | AC-39 (d) |
| S-44 | Kaynak servis küme denetimi kaldırılır | küme-dışı-ad testi |
| S-45 | Ret yazıcısının Application DI kaydı kaldırılır | DI testi |
| S-46 | `HttpContext.User` ayarı kaldırılır | aktör ölçüm testi (AC-41) |
| S-47 | Kısa kod kullanım denetimi kaldırılır | AC-42 iki senaryo |
| S-48 | Tür izni adımı kapsam adımının arkasına taşınır | `PLN` + `Platform` 403 testi |
| S-49 | Yönetici işleyicisinden `TenantScope.Begin` kaldırılır / `BeginPlatform` konur (r4 F-1) | AC-43 "PUT 200 + satır `TenantId = T`" |
| S-50 | (r5 tur 2 F-1 ile değişti; r4'ün `GetAuditMetadata()` yolu kalktı) `typeof(AlignRecordCodeGroupPrefixCommand)` `TransactionOwnedAuditCommands.Authorized`'dan silinir | AC-43 PUT 200 testi + `TransactionOwnedAuditCommandGuardTests.The_pipelines_allow_list_is_exactly_the_marked_commands` |
| S-51 | Yönetici controller'ında `PlatformAdminOnly` yerine `PlatformActor` + `[PartnerAdminScope]` (r4 F-4) | AC-44 `partner_admin` 403 |
| S-52 | Yürütücüde `actor_type = tenant_user` denetimi kaldırılır (r4 F-4) | AC-44 iç uç `platform_admin` 403 |
| S-53 | Gövde içi idempotency yeniden okuması kaldırılır (r4 F-3) | AC-5 durum (2) ve (3) — sarmalayıcının "`InsertAsync` çağrılmadı" kaydı |
| S-54 | Depodaki E11000 → `RecordCodeIdempotencyConflictException` çevirisi kaldırılır (r4 F-3; r5 tur 2 F-2: durum 4'te sarmalayıcı yeniden okumayı atlatır, test E11000'in gerçekten alındığını iç istisnadan doğrular) | AC-5 durum (4) 503 alır |
| S-55 | Varsayılan kapsam karşılaştırmadan sonra uygulanır (r4 F-9) | AC-5 v0 → açık `LegalEntity` |
| S-56 | `AfterState`'ten bir alan çıkarılır ya da `Metadata`'ya taşınır; adım 4 reddine gövde anahtarı yazılır (r4 F-2) | AC-17 Tablo A / AC-19 Tablo B |
| S-57 | `PlatformTransactionExecutor`'ın commit yenilemesi geçici olarak kaldırılır (yalnız sabotaj koşusu, geri alınır) (r4 F-3) | AC-39 (c) ilk senaryo 503 alır |
| S-58 | Hizalama işleyicisi `BeforeState`'i boş bırakır ya da önceki değeri yazımdan sonra okur (r5 tur 2 F-1) | AC-22 `GMG` → `GMX` testi, AC-43 ikinci hizalama testi |
| S-59 | Hizalamanın kutu yazımı işlem dışına (commit sonrasına) taşınır (r5 tur 2 F-1) | AC-43 "kutu düşerse ayar yok" testi |
| S-60 | (r5 tur 2 F-4) `InternalRecordCodesController` `AnonymousInternalControllers`'tan çıkarılır, ya da ilk commit grubundaki adım 1–2 ret dallarından biri kaldırılır | `LoginOnlyEndpointGuardTests.Every_anonymous_action_is_an_allow_listed_internal_api_key_route` / adlı ret testi |
| S-61 | (r6 tur 3 F-1) Hizalama ret dallarından birinde ret yazımı kaldırılır / `Outcome` `Denied` ya da `Succeeded` yapılır / 404 kaydı rota kimliğine bağlanır / ret doğrulayıcıya taşınır | AC-43 o retin "ret satırı var" testi / "404 kaydı platform denetim kiracısında" testi |
| S-62 | (r7 F-1) Bir biçim kuralı `AllocateRecordCodeValidator`'a taşınır | AC-45 zarf testi + ret satırı testi + yansıma testi |
| S-63 | (r7 F-3) 400 dalında ret yazımı kaldırılır / platform aktörü reddinde ret yazımı kaldırılır ya da `Failed` yazılır | AC-19 400-ret testi / AC-44 "platform aktörü reddi `Denied` satırı" |
| S-64 | (r7 F-3) 429'da ret yazılır ya da sayaç artırılmaz | AC-19 / AC-33 429 testi |
| S-65 | (r8, son bakış 01 tur 2 F-1 ile değişti) `LockAsync` filtresinden önek koşulu kaldırılır / `RecordCodeGroupPrefixChangedException` yakalanmaz | AC-46 iki ana makine testi |
| S-66 | (r7 F-10) MDM 403 yeniden 503'e çevrilir | AC-13 kalıcı-izin testi |
| S-67 | (r7 F-8) Erişimci kod sabitine çevrilir | AC-35 ikinci ana makine testi |
| S-68 | (r8 F-4@8a63c4e5) Kullanıcı izni ölçütü `PermissionClaimEvaluator.Evaluate`'e çevrilir | AC-32 büyük/küçük harf, birleşik değer, `permissions` testleri |
| S-69 | (r8 F-5@8a63c4e5) `PlatformTransactionUnavailableException` yakalaması kaldırılır / kapı istisnası zarfa çevrilir / ret yolunda yakalama kaldırılır / yüklem istisnası `InvalidOperationException` yapılır | AC-47 (a)–(e) |
| S-70 | (r8 F-2@e6dd4cd8; r9) Platform aktörü reddinin yazımı sınırlayıcıdan önceye alınır / bölüm anahtarından `platform-actor:` öneki kaldırılır | `RecordCodeAllocateRateLimiterTests`: AC-44 sınır-aşımı testi / "B kiracısı etkilenmez" testi |
| S-71 | (r10, son bakış 01 tur 4 F-6) Adım 4 reddinde olmayan / silinmiş kiracının kaydı token kiracısına bağlanır | `RecordCodeAuditDeliveryMongoTests`: AC-37 "olmayan kiracı → platform kaydı" |
| S-72 | (r10, son bakış 01 tur 4 F-5 / F-6) Platform aktörü token'ındaki `Guid.Empty` `tenant_id` kova anahtarına, sayaç etiketine ya da `TargetTenantId`'ye olduğu gibi yazılır | `RecordCodeAuditDeliveryMongoTests` + `RecordCodeAllocateRateLimiterTests`: AC-44 "`Guid.Empty` → `platform` / `null`" |
| S-73 | (r10, son bakış 01 tur 4 F-4; r11 sıralı test) `GroupPrefixLockedNow` `MatchedCount`'tan türetilir | `RecordCodeAllocationMongoTests`: AC-17 sıralı test — "sıradaki ayırma `false`" adımı (satır `true` taşır) |
| S-74 | (r12, DL-2) `ORG` kapsamına `Group` eklenir / `POS` izni `platform.positions.update` yapılır | `RecordCodeTypeCatalogTests`: AC-48 kapsam testi / yansıma testi |
| S-75 | (r12, DL-2) `ORG` bir HTTP bağlamasına (`Mdm` ya da `FinanceTreasury`) eklenir | `RecordCodeTypeCatalogTests`: AC-48 eşleme testi; `InternalRecordCodesHttpTests`: AC-49 |
| S-76 | (r12, DL-2) ORG / POS oluşturma kodu istekten ya da `OrganizationCodeNormalizer`'dan alır | `OrganizationRecordCodeHttpTests`: AC-50 kod adımı |
| S-77 | (r12, DL-2) Port anahtarı kayıt kimliği yerine yeni Guid'den türetir | `OrganizationRecordCodeHttpTests`: AC-50 "defter anahtarı = kayıt kimliği", AC-59 `IdempotencyKey` adımı |
| S-78 | (r12, DL-2) POS şirketi birim dışından alınır / taşımada kod yeniden ayrılır | `OrganizationRecordCodeHttpTests`: AC-51 |
| S-79 | (r12, DL-2) Oluşturmada dolu kod sessizce yok sayılır / ret doğrulayıcıya taşınır | `OrganizationRecordCodeHttpTests`: AC-52 (201 / `ProblemDetails`) |
| S-80 | (r12, DL-2) Güncelleme işleyicisinde `entity.Code = canonicalCode` geri konur | `OrganizationRecordCodeHttpTests`: AC-53 |
| S-81 | (r12, DL-2) Kayıt yazımı ayırmadan önceye alınır / ayırma reddinde normalleştirilmiş elle koda düşülür | `OrganizationRecordCodeHttpTests`: AC-54 (a) "kayıt yok", AC-56 |
| S-82 | (r12, DL-2) Koddan bağımsız doğrulama ayırmadan sonraya taşınır | `OrganizationRecordCodeHttpTests`: AC-54 (c) "sayaç ilerlemez", AC-57 "ayırma gönderilmez" |
| S-83 | (r12, DL-2) Çakışma araması yalnız canlı satırlara bakar / atlama kaldırılır | `OrganizationRecordCodeHttpTests`: AC-55 "`000002`" adımı / canlı eski kod senaryosu |
| S-84 | (r12, DL-2) Port kiracıyı şirketin / birimin satırından alır ya da bağlam çelişkisini yok sayar | `InProcessRecordCodeAllocatorTests`: AC-57 |
| S-85 | (r12, DL-2) Port `HasRequiredPermission = true` sabitler / controller'dan `[HasPermission]` kaldırılır | `InProcessRecordCodeAllocatorTests`: AC-58 izinsiz bağlam; `OrganizationRecordCodeHttpTests`: AC-58 HTTP 403 |
| S-86 | (r12, DL-2) Port `CallerService`'i kapalı küme dışı bir ad yapar | `OrganizationRecordCodeHttpTests`: AC-59 (kapı istisnası, kayıt yok) |
| S-87 | (r12, DL-2) Form kod girişi düzenlenebilir yapılır / bir anahtar `ar` dosyasından silinir / `form.js` `code` gönderir | `organization-record-code-form.test.js`: AC-60 adlı adımlar |

Diğer: kiracı izolasyonu gerçek HTTP + gerçek Mongo ile; soft delete yok; tarayıcı smoke yok; RESX yok; DataTable
doğrulayıcısı uygulanmaz; Mongo testleri Platform test sabitleriyle kendi replica set'ini kullanır.

## 18. Ready-for-dev Checklist

- [x] Golden Reference `none`; arka uç düzeni Golden Reference'a birebir (Bölüm 10).
- [x] Künye alanları dolu; `approval_status: pending`; `code_start` yok; `revision: r12`; künye `status: draft` (r12 — DL-2
  revizyonu; r11 `ready-for-dev` künyesiyle onaylıydı, `approvals.tsv:61`). S-49: draft künyeli sürüme yazılan onay satırı kodu
  yetkilendirmez → DL-2 kod izni için künye `ready-for-dev` gerekir (Q-6).
- [x] Layout & Shell: `shell: none` (Bölüm 9). Validation (Bölüm 12), Failure Path (Bölüm 13), Authorization (Bölüm 14),
  Gateway (Bölüm 15), Lookup kararı (Bölüm 7.1), Audited Events (Bölüm 21) yazılı.
- [x] Kimlik ön kontrolü `OK`, çıkış 0 (Bölüm 0).
- [x] `pack_approval.slices()` DL listesi döndürür — `PYTHONUTF8=1 python3 -P -c "import sys; sys.path.insert(0,'scripts'); import pack_approval as p; t=open('execution/domains/platform-shared-services/module-packs/MOD-0040-FU02-record-code-service.md',encoding='utf-8').read(); print(p.slices(t)); print(p.kunye(t)['status'])"` → `['DL-1', … 'DL-10']` ve `ready-for-dev` (r11, 2026-11-02 bu dosya üzerinde); **r12** (2026-11-10, bu dosya üzerinde) →
  `['DL-1', … 'DL-10']` ve `draft` (`approval_status` `pending`).
- [x] İnceleme 01 bulguları (Bölüm 19.1), inceleme 02 bulguları F-1…F-11 (Bölüm 19.2), inceleme 03 tur 1 bulguları
  F-1…F-10, tur 2 bulguları F-1…F-5, tur 3 bulgusu F-1, tur 4 bulguları F-1…F-2, tur 5 bulguları F-1…F-2, tur 6 bulgusu F-1, bağımsız son bakış 01 tur 1 bulguları F-1…F-12, tur 2 bulguları F-1…F-9 tur 3 bulguları F-1…F-7, tur 4 bulguları F-1…F-7 ve tur 5 bulguları F-1…F-3, inceleme 03 tur 8 bulgusu F-1 (Bölüm 19.3) işlendi.
- [x] Açık kararlar A-1, A-11, A-12, A-16, A-17 ve sözleşme dondurma **ready-for-dev önkoşulu değildir** — CT kararı (MVP-1 CT,
  2026-11-02) ile Bölüm 18.1'deki DL-1 kabul / merge kapılarına taşındı (her biri sahip ve tarihle). (r4, inceleme 03 tur 1 F-5:
  A-18 de buradan çıkarılmıştı — DL-5'in kararıdır, Bölüm 18.1.)
- [ ] CT-APR-02 doğrulama incelemesi — **her revizyonun kendi incelemesi zorunludur** (SOP §20.4/1; r10, son bakış 01 tur 4 F-2):
  r8 → inceleme 03 tur 6 + son bakış 01 tur 3 (UYGUN); r9 → inceleme 03 tur 7 (inceleme satırı `3ceb760e8`) + son bakış 01 tur 4
  (UYGUN, `132b9944d`) ve onay `466aacfc9`; r10 → inceleme 03 tur 8 (KOŞULLU, 1 ORTA; satır `4288ae40e`) + son bakış 01 tur 5
  (KOŞULLU, 1 ORTA, 2 DÜŞÜK; satır `28c4449cf`), onay yok; **r11 → kendi incelemesi henüz yok** (inceleme 03 tur 9 + son bakış 01
  tur 6 ya da CT'nin belirlediği eşdeğeri); açık ENGEL ve ORTA yok koşuluyla. Tur numarası bulgu dosyası adındaki hedef özetle eşleşir.
- [ ] CT-APR-01 onay satırı (yüksek risk: iç uç, kiracı, şema, kimlik bilgisi, denetim altyapısı; paketi yazmamış `high: evet` onaylayıcı) — r9 için yazıldı (`466aacfc9`, sha256 `5fed433c`); r10 onaylanmadı; **r11 için yeni onay satırı gerekir** (onay sürümün sha256'sına bağlıdır); `approval_status` onay satırıyla güncellenir.
- [x] **DL-2 (r12):** A-8 kapandı (K-15); süreç içi port (Bölüm 8.14); mevcut kodlar ve değişmezlik (Bölüm 8.15); ekran / API
  davranışı (Bölüm 11, 12); AC-48…AC-61; S-74…S-87; dosya listesi ve XMC-001 satırları (Bölüm 5 "DL-2"); denetim (Bölüm 21.4);
  sözleşme 1.0.0 değişmedi; kimlik ön kontrolü r12 için çıkış 0.
- [ ] **DL-2 açık soruları** (Bölüm 24.1): Q-1, Q-2, Q-3, Q-4 sahip kararı ve Q-7 XMC-001 onayı — DL-2 kodunu bekletir; Q-5
  teyit; Q-6 CT kararı. Seçilen cevap önerilenden farklıysa ilgili AC'ler (AC-53, AC-55, AC-60) DL-2 kodundan önce yeni revizyonla
  değişir.
- [ ] **r12 CT-APR-02 bağımsız incelemesi** (her revizyonun kendi incelemesi, SOP §20.4/1) ve **CT-APR-01 onay satırı** — r12 için
  henüz yok.
- **r9 notu — CT kararı (MVP-1 CT, 2026-11-02):** A-1, A-11, A-12, A-16, A-17 ve sözleşme dondurma ready-for-dev önkoşulu değildir;
  DL-1 kabul / merge kapısıdır (Bölüm 18.1). Künye `ready-for-dev` / `pending` kalır. **Kararın izi (r10, son bakış 01 tur 4 F-3):** r9 commit'i `92beb1228` (taşımayı yazan sürüm), r9 inceleme satırı `3ceb760e8` (inceleme 03 tur 7) ve r9 onay satırı `466aacfc9` (`execution/approvals/approvals.tsv`; gerekçesi "A-1 / A-11 / A-12 / A-16 / A-17 / sözleşme dondurmanın DL-1 kapılarına taşınması (CT kararı, 92beb1228)" der).

### 18.1 Kabul ve merge kapıları (hazırlık şartı değil — r4, inceleme 03 tur 1 F-5)

Aşağıdakiler `ready-for-dev`'i bekletmez; dilim tablosundaki (Bölüm 23) kapılarıdır ve orada da aynı yazılır:

| Kapı | Koşul | Ne zaman | Sahip |
|---|---|---|---|
| DL-1 **merge kapısı** | A-10 CT defter kararı: `platform-islem-ici` iz satırına `RecordCodeAuditIntent.*` belirteci, CT-DECISIONS satırı, `AuditTrailStandardTests` sabiti | DL-1 kodu main'e girmeden önce (kod yazımını bekletmez) | CT |
| DL-1 **kabul kapısı** | AC-41 ilk commit grubu kanıtı; FU-RCS-13 döndürme prosedürü | DL-1 kabul kaydı | Beste |
| DL-1 **kabul kapısı** (r9, CT kararı 2026-11-02) | A-1 şirket öneki biçimi kapandı ve işlendi (gelmezse RCS-001 §1 ile) | DL-1 kabul kaydından önce; karar hedefi 2026-11-02 | K1 (`gmg-10`) |
| DL-1 **kabul kapısı** (r9) | A-11 dondurma ayrıntıları (`idempotencyKey` biçimi, `prefixScope` portu, Bölüm 4.4 izin adlarının FU27 teyidi) kapandı ve işlendi | DL-1 kabul kaydından önce; karar hedefi 2026-11-03 | Beste + Treasury CT + MDM sahibi |
| DL-1 **kabul kapısı** (r9) | A-16 hız sınırı değeri ve eski sır penceresi kapandı ve `appsettings.json`'a işlendi | DL-1 kabul kaydından önce; karar hedefi 2026-11-03 | Beste |
| DL-1 **kabul kapısı** (r9) | A-17 200 yanıt zarfı: Treasury CT teyidi ya da Bölüm 24 varsayılanı (zarf) uygulandı | sözleşme dondurmasıyla, 2026-11-03 | Treasury CT (teyit); CT (varsayılanın uygulanması) |
| DL-1 **kabul kapısı** (r9; r10 tutarlılık) | Sözleşme 1.0.0 CT adımıyla donduruldu (AC-23 dondurulmuş sözleşmeye karşı koşar) — yalnız sözleşme §12'de **"Bekletir: evet"** olan noktalar (N-2, N-3, N-4, N-7, N-9, N-10) kapanmış olmalı; N-8 çakışma kolu, N-11, N-12 dondurmayı bekletmez (sözleşme rev 9, son bakış 01 tur 4 F-1) | 2026-11-03 | CT |
| DL-1 **merge kapısı** (r9) | A-12 kayıt satırı, "reserved" notunun düzeltilmesi ve domain-config In-Scope satırı yazıldı | DL-1 kodu main'e girmeden önce | CT |
| DL-5 **başlama kapısı** | A-18 kararı (MDM kalıcı kısa kod kilidi) ve bağlı paket kimliği (Bölüm 7, DL-5 bağımlılığı) | DL-5 paket revizyonundan önce; hedef 2026-11-06 — DL-1'i bekletmez, o güne kadar Bölüm 8.13 ara dönem azaltıcısı geçerli | MDM sahibi (Ali) + Beste |
| DL-2 **başlama kapısı** (r12) | A-8 kararı | **kapandı** — K-15, 2026-11-10 | Beste |
| DL-2 **başlama kapısı** (r12) | Q-1, Q-2, Q-3, Q-4 sahip kararı işlendi (seçim öneriden farklıysa yeni revizyon) | DL-2 kodundan önce | Beste |
| DL-2 **başlama kapısı** (r12) | Q-7 XMC-001: MOD-0288 sahibinin Bölüm 5 "DL-2" XMC satırlarına onayı; MOD-0288-FU03 / FU04 ile sıra | DL-2 kodundan önce | MOD-0288 sahibi (**TBD**) + CT |
| DL-2 **başlama kapısı** (r12) | r12'nin (ya da ardılının) CT-APR-02 incelemesi ve CT-APR-01 onay satırı; onaylanan sürümün künyesi `ready-for-dev` (S-49, Q-6) | DL-2 kodundan önce | CT + Beste |
| DL-2 **kabul kapısı** (r12) | Kiracı başına desene uyan eski ORG / POS kodu sayımı (AC-55) kabul kaydında; bir (kiracı, önek, tür) için sayım 3'ü aşarsa o kiracıda DL-2 devreye alınmadan CT'ye bildirim (DL-6) | DL-2 kabul kaydı | CT |

## 19. Implementation Notes

**Örnek alınan çalışan kod (yalnız okundu; satırlar `6d49f5823`'te ölçüldü, `81864bd98`'de değişmedi — r4 F-10).**

- Doküman kimlik üreticisi (K-1): `services/Diten.Platform/src/Diten.Platform.Application/Features/DocumentManagementIdentifiers/Services/DocumentIdentifierAllocationService.cs`
  — sınıf `:19`; yoklama döngüsü `:317-335` (100 deneme `:28`); biçim `:337-343`; elle rezervasyon `:102-153`; iptal = durum
  değişikliği `:157-213`. Sayaç deposu `…/DocumentManagementIdentifierRepositories.cs:52-90` (**E11000 kurtarması yok**);
  silinmişleri de gören yoklama `:22-30`; varlık `DocumentIdentifierSequenceCounter.cs:12-21`; unique index
  `PlatformSchemaManifest.DocumentManagementFollowUps.cs:282-295`; `DocumentCodingOptions` `DocumentManagementIdentifiersModels.cs:41-50`;
  tür kodu çözücüsü `:65` (`QualityTechnicalAgreementSdea → "AGR"`).
- MDM `CodeReservation` deseni: `services/Diten.MdmService/src/Diten.MdmService.Persistence/Repositories/CodeReservationRepository.cs:37-110`
  (önce idempotency, sayaç, ekle, E11000'de kazananı oku), `:549-600` (upsert yarışı kurtarması); varlık `CodeReservation.cs:5-27`.
- Denetim kapısı: `CanonicalTransactionalAuditOutboxWriter.cs` — sabit kaynak `:39`, intent zorunlu `:81-83`, kategori `:84-87`,
  boş hedef kiracı `:89-92`, aktör `:156-185` (principal → `AuditActorTypeResolver`; kullanıcı kimliği yoksa ret), sonuç sabit
  `Succeeded` `:131`, kaynak `:138`; `TransactionOwnedAuditIntent.cs:11-33` (`SystemActor` `:32`); `AuditOutboxWriteRequest.Validate`
  boş `TenantId` reddi `:28-30`; muafiyet listesi `TransactionOwnedAuditPayloadExemptions.cs:9-40` (yalnız küçülür, 2026-11-13).
  Principal kaynağı `JwtTenantAuthorizationContext.cs:24` (`HttpContext.User`, kurucuda).
- `HybridRepository` filtresi `Repositories.cs:96-109` (kullanılmaz); `TenantRepository` `:70-92`.
- FU21 kimlik ve 401/403: Bölüm 8.2 tablosu; sonuç sabitleri `ITrustedLegalEntityScopeCredentialAuthenticator.cs:10-11`,
  `ITrustedLegalEntityScopeJwtContext.cs:17-21`.
- Kiracı durumu: `GetTenantStatusQueryHandler.cs:18-29`. Hız sınırı emsali `PeopleSearchRateLimit.cs:17-57` (`OnRejected` tek kanca, politika dışı ret `RATE_LIMITED` `:23`, `:38-46`) — **kullanılmaz**, yürütücü içi sınırlayıcı (r3 F-5). Denetim kutusu anahtarı emsali `…/Entitlements/Handlers/CommandHandlers/PhysicalEntitlementAuditIntent.cs:29`; kutu tekilliği `PlatformSchemaManifest.AccessGovernance.cs:118-120`, işlem içi ekleme `AuditOutboxRepository.TryInsertAsync(IPlatformTransactionSession …)` (r4 F-3; oturumsuz `TryInsertAsync(AuditOutboxMessage …)` değil). Platform sistem kiracısı `SystemTenantRules.PlatformSystemTenantId` (Internal, Code `PLATFORM`; r4 F-10). İşlem yürütücüsü `PlatformTransactionExecutor.ExecuteAsync`, test sondası `IPlatformTransactionFaultProbe`, emsal test `PlatformTransactionUnknownCommitMongoTests`. Yönetici politikaları `AddPlatformActorPolicies` (`Diten.Platform.Infrastructure/DependencyInjection.cs`). Kiracı kapsamı `TenantScope.Begin` / `BeginPlatform`. İşlem içi denetim izin listesi `TransactionOwnedAuditCommands.Authorized` ve koruması `TransactionOwnedAuditCommandGuardTests`; emsal `EnableTenantModuleEntitlementCommandHandler` (r5 F-1). (r4'ün dayandığı `AuditService.ResolveTenantId` yolu bu özellikte kullanılmaz.) Ret yazıcısı DI: Application `DependencyInjection.cs:329-330`, `DependencyInjectionSmokeTests.cs:281-282`.
- MOD-0220 yanıtı `LegalEntityModels.cs:100-106` (`approvalStatus` yok).
- MOD-0178 tüketici beklentisi: §8.7 (`:815-822`), §22 (`:1956-1974`) — WMS kendi 422 `WMS_COMPANY_CODE_INVALID`'ini döner
  ve bu servisin 409'unu kendi koduna eşler (FU-0178-RCS-01); geçici sayaçtan geçişte merkezi sayaç WMS'in en büyük
  numarasının üstünden başlamalı (§5.11) → DL-6.
- **Önek çakışması (bilgi):** bir şirketin kısa kodu grup önekiyle aynıysa `(kiracı, GMG, BAC)` sayacı ortaktır; kod
  çakışmaz, sahip bilgisi kaydın `LegalEntityId`'sindedir (§5.2).
- **AUD-001 §3 ile K-8r:** kuralın 3. satırı servis çağrısında `ActorType = System` + `SourceService` der
  (`audit-trail-standard.md:103`); bu uçta işlemi bir kişi başlattığı için satır 1 (kim = kullanıcı) ve satır 3'ün
  `SourceService`'i birlikte uygulanır — çelişki yok (r1'in A-15'i kapandı).

### 19.1 Bağımsız inceleme 01 (UYGUN DEĞİL) — kapanış tablosu

Bulgu dosyası `docs/records/reviews/MOD-0040-FU02/2026-10-30-219f30c9.md`, özet `277fd253…` (`@` öneki önceki bulgu
dosyasının özetidir — POLICY-REVIEW-START.md; r2'de yanlışlıkla pack blob'u `219f30c9` yazılmıştı, r3'te düzeltildi).
İnceleme 02'nin değerlendirmesi: 1, 2, 3, 4, 6, 7, 10, 12 kapandı; 5, 8, 9 kısmen (kalanları inceleme 02'nin 10, 7, 8'i);
11 pack tarafında kapandı, kayıt satırı CT adımı (A-12).

| Bulgu | Seviye | Kapanış | Nerede |
|---|---|---|---|
| F-1@277fd253 | ENGEL | Aktör kullanıcı (K-8r), kaynak servis kapalı listeden (K-9), ret kaydı ayrı işlemle (K-10), Platform kapsamı DL-4 (K-11); yazıcı yalnız eklemeli genişler | Bölüm 0.1, 5, 8.2, 8.7, 8.11, 21; AC-17, AC-18, AC-19, AC-25 (DL-4), AC-28, AC-29; sözleşme §11 |
| F-2@277fd253 | ORTA | `HybridEntity` / `HybridRepository` kaldırıldı; `TenantScopedEntity`; açık `tenantId` + tam eşitlik (r3'te sentinel reddi kaldırıldı — inceleme 02 F-1) | Bölüm 4, 8.7; AC-30 |
| F-3@277fd253 | ORTA | Beş kiracı yüklemi ayrı sabotaj; B'nin defter satırı ve sayaç belgesi doğrulanır | Bölüm 3, 4; AC-12, AC-31 |
| F-4@277fd253 | ORTA | Sızma senaryosu; tüketici başına izinli türler; kullanıcı izni; hız sınırı; döndürme/iptal | Bölüm 4.4, 8.2, 20; AC-14, AC-32, AC-33, AC-34 |
| F-5@277fd253 | ORTA | `Response<T>` zarfı; v0 biçimi **öneri olarak** bağlayıcı değil (r3 — inceleme 02 F-10) | Bölüm 24 (A-17); sözleşme başlığı, §3, §9 |
| F-6@277fd253 | DÜŞÜK | Geçici çakışmada yeniden deneme / kazananı okuma; N−1 boşluk | Bölüm 8.6, 13; AC-5 |
| F-7@277fd253 | DÜŞÜK | 401/403 FU21 ile birebir | Bölüm 8.2; AC-1, AC-23 |
| F-8@277fd253 | DÜŞÜK | Grup öneki kilidi (r3: tek kaynak `OrgPrefix` — inceleme 02 F-7) | Bölüm 4.3, 8.12; AC-22, AC-35 |
| F-9@277fd253 | DÜŞÜK | Aktif + onaylı (r3: v1.1 bağımlılığı — inceleme 02 F-8) | Bölüm 7, 7.1, 8.3; AC-36 |
| F-10@277fd253 | DÜŞÜK | `BAC` kapsam seçimi tüketicide | Bölüm 7.1 (TC-4, TC-5) |
| F-11@277fd253 | DÜŞÜK | `module-id-registry.md` "reserved" notu A-12'de (r4: satır numarası yerine metinle) | Bölüm 0, 24 (A-12) |
| F-12@277fd253 | DÜŞÜK | Askıdaki kiracı DL-1'de | Bölüm 8.2, 8.5; AC-37 |

### 19.2 Bağımsız inceleme 02 (KOŞULLU) — kapanış tablosu

Bulgu dosyası `docs/records/reviews/MOD-0040-FU02/2026-10-30-b18184f0.md` @ `bbd30dd87`, özet `46aeb4a5…`
(`git show bbd30dd87:<yol> | sha256sum`, 2026-10-30). Hedef r2 pack blob `b18184f0…`. Kapanışlar inceleyenin CT onaylı
önerileridir; sahip kararı gerektirmedi.

| Bulgu | Seviye | Kapanış | Nerede |
|---|---|---|---|
| F-1@46aeb4a5 | ORTA | Sentinel kimliği `…0001` reddedilmez (r4 F-10: iş kiracısı değil, Internal platform sistem kiracısı — Bölüm 4); depo yalnız `Guid.Empty` ve platform bağlamını (`IsPlatformContext`) reddeder; sentinel kiracıda normal ayırma | Bölüm 4; AC-30, AC-38; S-30 |
| F-2@46aeb4a5 | ORTA | Kutu anahtarı istemci anahtarı değil: başarıda `record-code:allocated:{allocationId:N}`, retde yeni Guid (emsal `PhysicalEntitlementAuditIntent.cs:29`); `TryInsertAsync` `false` → işlem iptal, defter `allocationId` ile okunur, 200 aynı kod (503 değil) — **r4 F-3 ile geri alındı**: `false` değişmez ihlali → 503 (Bölüm 8.6) | Bölüm 4.2, 8.6, 13; AC-17, AC-19, AC-39; S-42, S-43; sözleşme §11 |
| F-3@46aeb4a5 | DÜŞÜK | Ret yazıcısı Application DI'da ayrı `AddScoped`; Infrastructure DI ifadesi kaldırıldı; `DependencyInjectionSmokeTests.cs:281-282` değişmeden yeşil | Bölüm 5; AC-27, AC-29; S-45 |
| F-4@46aeb4a5 | DÜŞÜK | `TransactionOwnedAuditSourceServices` kapalı küme; küme dışı → istisna; `TransactionOwnedAuditIntent.cs:6-9` yorumu güncellenir | Bölüm 5, 8.2, 12; AC-40; S-44 |
| F-5@46aeb4a5 | DÜŞÜK | Yürütücü içi `PartitionedRateLimiter`, bölüm (tüketici servis, kiracı); 429 `RECORD_CODE_RATE_LIMITED` yürütücünün ret yolundan; `PeopleSearchRateLimit.cs` ve `OnRejected` kullanılmaz; değer A-16 | Bölüm 3, 5, 8.2, 8.5; AC-33; S-37 |
| F-6@46aeb4a5 | DÜŞÜK | Açık `AuthenticateAsync` + `HttpContext.User` işleyiciden önce; aktör DL-1'in ilk commit grubunda (r4 F-6) gerçek HTTP + üretim yazıcısıyla ölçülür, başarısızsa DL-1 durur | Bölüm 8.2; AC-17, AC-41; S-46 |
| F-7@46aeb4a5 | DÜŞÜK | Grup önekinin tek kaynağı belge `OrgPrefix`'i; ayar gövdesiz hizalama + kilit; kilitsizken düzeltme = `OrgPrefix` değişikliği; kilitten sonra uyuşmazlık 409 + kurtarma yolu (geri alma; bilinçli değişiklik FU-RCS-16) | Bölüm 3, 4.3, 8.12, 14; AC-22, AC-35 |
| F-8@46aeb4a5 | DÜŞÜK | v1.1 yayınlanana kadar yalnız aktiflik + TC-3 zorunlu; v1.1'de alan yoksa 503 (`v1.1-proposal:69`) — kod değişikliği (FU-RCS-15), MOD-0220 sahibine açık bağımlılık; "kod değişikliği gerekmez" kaldırıldı | Bölüm 7, 7.1, 8.3, 20; AC-36 |
| F-9@46aeb4a5 | DÜŞÜK | "Platform DL-1'de 422 (tür izni geçtiyse)"; `PLN` + `Platform` 403; sözleşme örneğiyle hizalı | Bölüm 0.1 (K-11), 8.5; AC-8; S-48; sözleşme §2, §4, §10 |
| F-10@46aeb4a5 | DÜŞÜK | "v0 yanıt biçimi bağlayıcı değildi" **öneri** olarak; Treasury CT teyidi (A-17, 2026-11-02) | sözleşme başlığı, §3, §9, §12 |
| F-11@46aeb4a5 | DÜŞÜK | Ara dönem azaltıcısı: kullanılmış kısa kod değişirse ya da başka şirkete geçerse 409 `LEGAL_ENTITY_SHORT_CODE_CHANGED`; MDM kalıcı kilidi A-18, MDM sahibi (Ali) + Beste, 2026-11-06 | Bölüm 4.2, 7, 8.13, 24; AC-42; S-47 |

### 19.3 Bağımsız inceleme 03 (tur 1–8) ve bağımsız son bakış 01 (tur 1–5) — karar tablosu

**Tur 1** — bulgu dosyası `docs/records/reviews/MOD-0040-FU02/2026-11-02-010fe1c2.md` (dosya özeti `47c28da0…`; hedef r3
pack sha256 `010fe1c2…8124`, kaynak `81864bd98`); CT kabulü 2026-11-02 (S-116, tek yazar geçişi). r4 bu tabloda
`review-03-raw-2026-11-02` adını önek yapıyordu; r5'te önek POLICY-REVIEW-START kuralına göre bulgu dosyası
özetinin ilk 8 hanesidir (tur 2 F-5). Sütunlar: bulgu | düzeltme | bölümler.

| Bulgu | Düzeltme | Bölümler |
|---|---|---|
| F-1@47c28da0 | Yönetici ucu rota kiracısını platform bağlamında doğrular, depoyu `TenantScope.Begin` içinde çağırır; denetim `TargetTenantId` = rota kiracısı (denetim yolu r5'te işlem içi kapıya geçti — F-1@e2dbb0d8); AC-30 istisnasız, yönetici yolu AC-43 | Bölüm 3, 4, 4.3, 13, 14, 21.1; AC-22, AC-30, AC-43; S-49, S-50 |
| F-2@47c28da0 | Başarı ve ret yükünün tek tanımı (yer, ad, adım); sözleşme §11 aynı tabloya bağlandı | Bölüm 21.1, 21.3; AC-17, AC-19; S-56; sözleşme §11 |
| F-3@47c28da0 | Kutu `false` kurtarması kaldırıldı (değişmez ihlali → 503); commit belirsizliği ve aynı anahtar yarışının durumları yürütücü davranışına göre (r5'te dört durum — F-2@e2dbb0d8); atıf `TryInsertAsync(IPlatformTransactionSession …)` | Bölüm 3, 4.2, 5, 7, 8.5, 8.6, 13, 19; AC-5, AC-17, AC-39; S-43, S-53, S-54, S-57; sözleşme §11 |
| F-4@47c28da0 | Yönetici ucu `PlatformAdminOnly`; iç uçta platform aktörleri 403, adlı testlerle | Bölüm 3, 8.2, 13, 14; AC-44; S-51, S-52 |
| F-5@47c28da0 | A-18 ve A-10 hazırlık listesinden kabul / merge kapılarına | Bölüm 18, 18.1, 23 |
| F-6@47c28da0 | AC-41 ilk commit grubu: asgari dosya listesi, kanıt, başarısızlıkta geri alma | Bölüm 5, 16, 17; AC-41 |
| F-7@47c28da0 | `ORG` / `POS` kapsamı DL-1'de boş küme; A-8 DL-2'de | Bölüm 4.4; AC-9 |
| F-8@47c28da0 | DL-5 / DL-7 / DL-9 bağımlılık olarak: bağlı paket + sahip rolü, bilinmeyen kimlik TBD | Bölüm 7, 20, 23 |
| F-9@47c28da0 | Birleştirilmiş parmak izi yerine alan alan, varsayılan uygulanmış karşılaştırma; v0 → açık `LegalEntity` senaryosu | Bölüm 4.2, 8.5, 13; AC-5; S-6, S-55; sözleşme §8 |
| F-10@47c28da0 | Satır ölçüm commit'i `81864bd98`; platform sistem kiracısı tanımı ve MDM 3 s emsali düzeltildi; kayıt notlarına metinle atıf | başlık, Bölüm 0, 4, 8.1, 19, 19.1 |

**Tur 2** — bulgu dosyası `docs/records/reviews/MOD-0040-FU02/2026-11-02-127ec6f2.md` (dosya özeti `e2dbb0d8…`; hedef r4
pack sha256 `127ec6f2…95bd`, kaynak `5899d8e30`); CT kararı 2026-11-02 (beşi kabul; F-1 yolu: işlem içi denetim kapısı).

| Bulgu | Düzeltme | Bölümler |
|---|---|---|
| F-1@e2dbb0d8 | Hizalama komutu `ITransactionOwnedAuditCommand`, `TransactionOwnedAuditCommands.Authorized`'a adıyla (CT kararı, AUD-001 §4.4); önceki değer işleyicide `TenantScope` içinde aynı işlemde okunur, denetim işlem içi yazıcıyla; korunan yol tek ad için daraltıldı; `BeforeState` sabotajla | Bölüm 3, 4, 4.3, 5, 6, 10, 17, 21, 21.1, 21.3 (Tablo C); AC-22, AC-43; S-50, S-58, S-59 |
| F-2@e2dbb0d8 | Yarış durumları dörde ayrıldı: geç gelen gövde içi okumayla karşılanır (E11000 yok), E11000 çevirisi savunma olarak kalır ve atlatıcı test sarmalayıcısıyla ölçülür; test E11000'i iç istisnadan doğrular | Bölüm 8.6; AC-5; S-53, S-54 |
| F-3@e2dbb0d8 | Sözleşme §8'e aynı anahtar örtüşmesindeki 503 sınırı ve aynı anahtarla yeniden deneme; `x-measured-at` 81864bd98 | sözleşme başlığı, §8, §14 |
| F-4@e2dbb0d8 | `LoginOnlyEndpointGuardTests` liste girişi ve adım 1–2 ret testleri ilk commit grubunda; ara kırmızı kabul edilmez | Bölüm 5, 17; AC-41; S-60 |
| F-5@e2dbb0d8 | Önekler bulgu dosyası özetinden; inceleme numarası "inceleme 03, tur n" | künye, başlık, Bölüm 18, 19.3 |

**Tur 3** — bulgu dosyası `docs/records/reviews/MOD-0040-FU02/2026-11-02-bcd62d6c.md` (dosya özeti `ace6292b…`,
`git show ba17abf46:<yol> | sha256sum`; hedef r5 pack sha256 `bcd62d6c…c071`, kaynak `98833e2b1`); sahip kararı Beste
2026-11-02.

| Bulgu | Düzeltme | Bölümler |
|---|---|---|
| F-1@ace6292b | Sahip kararı (a): hizalama retleri (404, 400, 409 ×2) işleyicide verilir ve K-10 ret yazıcısıyla ayrı kısa işlemde `Failed` yazılır; 404 kaydı platform denetim kiracısına, `TargetTenantId` rota kimliği; hizalama doğrulayıcısı kaldırıldı; r5'in "retler yazılmaz" kuralı geri alındı | Bölüm 3, 4 (adım 4), 5, 10, 12, 13, 17, 21, 21.1, 21.3 (Tablo D); AC-43; S-61 |

**Tur 4** — bulgu dosyası `docs/records/reviews/MOD-0040-FU02/2026-11-02-4b4c3db1.md` (dosya özeti `f4049860…`; hedef r6
pack sha256 `4b4c3db1…b541`, kaynak `450bd479f`); sonuç UYGUN, 2 DÜŞÜK; CT kararı: bir sonraki revizyonda kapanır (S-101).

| Bulgu | Düzeltme | Bölümler |
|---|---|---|
| F-1@f4049860 | İç uç 400 `Failed` yazılır, 429 adlı sapma — sahip kararı K-13 (son bakış 01 F-3 ile birlikte) | Bölüm 0.1 (K-10, K-13), 8.2, 21.2; AC-19, AC-33 |
| F-2@f4049860 | Yönetici 404 cevabı `reason_code = TENANT_NOT_FOUND`, denetimle aynı kod; AC-43 ikisini birlikte ölçer | Bölüm 12, 14, 21.3 Tablo D; AC-43 |

**Bağımsız son bakış 01** — bulgu dosyası `docs/records/reviews/MOD-0040-FU02/2026-11-02-4b4c3db1-son-bakis.md` (dosya özeti
`a52313ba…`; hedef r6 pack sha256 `4b4c3db1…b541`, kaynak `f93215b32`); KOŞULLU, 3 ORTA, 9 DÜŞÜK; F-3 için sahip kararı K-13.

| Bulgu | Düzeltme | Bölümler |
|---|---|---|
| F-1@a52313ba | İki komutun da doğrulayıcısı yok; biçim kuralları API ayrıştırıcısında, ret işleyicide zarfla ve `Failed` kayıtla; Golden Reference `Validators/` sapması adlı | Bölüm 5, 8.5 (d), 10, 12, 13; AC-45; S-62 |
| F-2@a52313ba | Adım → katman tablosu; ret yazıcısını çağıranlar (işleyici + yürütücü), DI kapsamı, A-10 belirteci, ret testlerinin sınıfı | Bölüm 8.1, 8.5; AC-37 |
| F-3@a52313ba | Sahip kararı K-13 (seçenek A): 400 `Failed`, platform aktörü `Denied` (platform denetim kiracısı), 429 adlı sapma + uyarı logu + sayaç | Bölüm 0.1, 8.2, 8.5, 21.2, 21.3 Tablo B; AC-19, AC-33, AC-44; S-63, S-64 |
| F-4@a52313ba | Tek `Outcome` kuralı (yetki / erişim alanı `Denied`, diğerleri `Failed`); 422 `RECORD_CODE_SCOPE_INVALID` `Failed` | Bölüm 0.1 (K-10), 13, 21.2; sözleşme §11 |
| F-5@a52313ba | XMC-001 onayı olarak yazıldı: paylaşılan bileşenler, onaylayan kararlar (K-9, K-10, CT), etkilenen tüketiciler | Bölüm 2.1 |
| F-6@a52313ba | AC-41 ilk grup listesine modeller, neden kodları, taşıma modeli; DI dosyaları adlı | AC-41 |
| F-7@a52313ba | Sözleşme–paket beş uyuşmazlık giderildi (alan alan eşitlik, ORG / POS boş küme, `GROUP_PREFIX_MISMATCH` kapsamı, `Content-Type` 400, künye) | Bölüm 12; sözleşme künye, §1, §4, §5, §6 |
| F-8@a52313ba | Erişimci `IOptions<DocumentCodingOptions>` (belge üreticisiyle aynı), yeniden başlatma + çok örnek kuralı; AC-35 üç ana makineyle | Bölüm 4.3, 8.12; AC-35; S-67 |
| F-9@a52313ba | Kilit filtresi: bir yeniden değerlendirme, ikincide 503, en çok 2 sayaç boşluğu (**r8'de geri alındı** — tek sonuç 409, F-1@8a63c4e5); ölçülen istisna eşlemesine göre işleyici 503'ü kendisi döner, adlı istisnalar | Bölüm 3, 4, 5, 8.6, 13, 21.2; AC-30, AC-46; S-65 |
| F-10@a52313ba | MDM 403 → kalıcı 403 `LEGAL_ENTITY_READ_PERMISSION_MISSING` (`Denied`); TC-2 sınırlı yeniden deneme | Bölüm 7, 7.1, 8.3, 13, 21.2; AC-13; S-66; sözleşme §4, §7, §9 |
| F-11@a52313ba | `HasPermission` anahtarları platform aktörü için değerlendirilmez (ölçüldü); koruma `PlatformAdminOnly` + kayıt denetimi | Bölüm 14 |
| F-12@a52313ba | A-17 varsayılanı: teyit gelmezse zarfla donar | Bölüm 24 (A-17); sözleşme §12 N-2 |

**Son bakış 01, tur 2** — bulgu dosyası `docs/records/reviews/MOD-0040-FU02/2026-11-02-fb151036-son-bakis.md` (dosya özeti
`8a63c4e5…`, `git show cf0aea997:<yol> | sha256sum`; hedef r7 pack sha256 `fb151036…f3f7`, kaynak `49628016e`); KOŞULLU,
1 ORTA, 8 DÜŞÜK; sahip kararı Beste 2026-11-02 "düzelt, bir tur daha".

| Bulgu | Düzeltme | Bölümler |
|---|---|---|
| F-1@8a63c4e5 | Kilit filtresi eşleşmezliği tek sonuç 409 `GROUP_PREFIX_MISMATCH` (IOptions sabitliği ve adım 11 ölçüldü; aynı duruma aynı kod); yeniden değerlendirme dalı kaldırıldı; AC-46 iki gerçek ana makineyle | Bölüm 8.6, 13, 17, 21.2; AC-46; S-65 |
| F-2@8a63c4e5 | Kalıntılar: Bölüm 1 madde 6 (429 istisnası), Bölüm 2.1 (doğrulayıcı yok) | Bölüm 1, 2.1 |
| F-3@8a63c4e5 | Tek `Outcome` kuralı tek cümle; kol (a) koddan, kol (b) "tek gövdeli gizleme" paket kararı | Bölüm 0.1 (K-10), 4 (adım 4), 21.2 |
| F-4@8a63c4e5 | Kullanıcı izni ölçütü `HasExactPermission` (FU21), `PermissionClaimEvaluator` kullanılmaz; AC-32 biçim / takma ad sabotajı | Bölüm 8.5; AC-32; S-68 |
| F-5@8a63c4e5 | `ExceptionBehavior` + `GlobalExceptionHandler` eşlemesi yazıldı; kendi istisnalar zarfa, kapının iki istisnası `ProblemDetails` (Platform kuralı), ret yolunda yakalanıp loglanır; iki işleyici için | Bölüm 8.6; AC-47; S-69; sözleşme §4 |
| F-6@8a63c4e5 | Sözleşme §11 ret yükü Tablo B ile birebir | sözleşme §11 |
| F-7@8a63c4e5 | A-17 metinleri tek varsayılan; Bölüm 18 tarihi dondurma | Bölüm 18; sözleşme başlık, §12 |
| F-8@8a63c4e5 | `LoginOnlyEndpointGuardTests` XMC-001 onayı dokunmadan önce (CT-APR-01) | Bölüm 2.1 |
| F-9@8a63c4e5 | Bölüm 4.2 alan tablosu birleşti | Bölüm 4.2 |

**İnceleme 03, tur 5** — bulgu dosyası `docs/records/reviews/MOD-0040-FU02/2026-11-02-fb151036.md` (dosya özeti `e6dd4cd8…`;
hedef r7 pack sha256 `fb151036…f3f7`, kaynak `49628016e`); UYGUN, 2 DÜŞÜK.

| Bulgu | Düzeltme | Bölümler |
|---|---|---|
| F-1@e6dd4cd8 | Son bakış F-5 ile birlikte: kapının iki istisnası `ProblemDetails` olarak kabul (gerekçe `ExceptionBehavior` Platform kuralı), işleyicinin kendi istisnaları zarfa; hizalama işleyicisi dahil | Bölüm 8.6; AC-47; sözleşme §4 |
| F-2@e6dd4cd8 | Platform aktörü reddinin denetim yazımı hız sınırlayıcıdan geçer; cevap 403 kalır; sınırı aşan retler 429 gibi adlı sapma (K-13 taşkın gerekçesi) | Bölüm 0.1 (K-13), 8.5, 21.2; AC-44; S-70 |

**İnceleme 03, tur 6** — bulgu dosyası `docs/records/reviews/MOD-0040-FU02/2026-11-02-02db18ea.md` (dosya özeti `f9c26991…`,
`git show 2265cce90:<yol> | sha256sum`; hedef r8 pack sha256 `02db18ea…dc43`, kaynak `925ea4e34`); UYGUN, 1 DÜŞÜK; CT: S-101, r9'da kapanır.

| Bulgu | Düzeltme | Bölümler |
|---|---|---|
| F-1@f9c26991 | Platform aktörü retleri ayrı `platform-actor:` önekli kovada; kiracının ayırma kovası etkilenmez; AC-44'e "B kiracısı etkilenmez" | Bölüm 8.5; AC-44; Bölüm 17; S-70 |

**Son bakış 01, tur 3** — bulgu dosyası `docs/records/reviews/MOD-0040-FU02/2026-11-02-02db18ea-son-bakis.md` (dosya özeti
`ec0346bb…`; hedef r8 pack sha256 `02db18ea…dc43`, kaynak `925ea4e34`); UYGUN, 7 DÜŞÜK; CT: S-101, r9'da kapanır.

| Bulgu | Düzeltme | Bölümler |
|---|---|---|
| F-1@ec0346bb | Kilit filtresi kalıntıları: Tablo B `Prefix` = sürecin etkin `OrgPrefix`'i (iki kaynakta aynı); adım 13'e 409; `LockAsync` "eşleşmez" = `MatchedCount == 0` | Bölüm 3, 8.5, 8.6, 21.3 Tablo B; sözleşme §5 |
| F-2@ec0346bb | Sözleşme §11 tek `Outcome` cümlesi paketteki ile aynı; yinelenen yük cümlesi kaldırıldı | sözleşme §11 |
| F-3@ec0346bb | Adım 4 reddinde olmayan / silinmiş kiracının kaydı platform denetim kiracısında, hedef token kiracısı; AC-37 ölçer | Bölüm 21.2, 21.3 Tablo B; AC-37; sözleşme §11 |
| F-4@ec0346bb | Ayrı `platform-actor:` kovası (tur 6 F-1 ile aynı); `tenant_id` yok / Guid değil / `Guid.Empty` → `TargetTenantId = null` | Bölüm 8.5, 21.2, 21.3; AC-44; sözleşme §11 |
| F-5@ec0346bb | `record_codes_rate_limited_total` tek etiket kümesi `consumer_service`, `tenant`, `reason`; 429 için `reason = rate_limited` | Bölüm 0.1 (K-10), 8.2, 8.5; AC-33 |
| F-6@ec0346bb | `ExceptionBehavior`: iki türü loglamadan yeniden atar, diğerlerini loglar; `IntentInvalid` ⊂ `Refused`, tek `catch` | Bölüm 8.6 |
| F-7@ec0346bb | AC-44 test sınıfı ataması tek; Bölüm 17 satırları; S-70 sınıfı `RecordCodeAllocateRateLimiterTests` | AC-44; Bölüm 17; S-70 |

**Son bakış 01, tur 4** — bulgu dosyası `docs/records/reviews/MOD-0040-FU02/2026-11-02-5fed433c-son-bakis.md` (dosya özeti
`0e25482b…`, `git show 466aacfc9:<yol> | sha256sum`; hedef r9 pack sha256 `5fed433c…3e8b`, kaynak `92beb1228`); UYGUN, 7 DÜŞÜK;
CT: S-101, r9'u değiştirmez, DL-1 başlamadan r10'da kapanır. r9 onaylı (`466aacfc9`).

| Bulgu | Düzeltme | Bölümler |
|---|---|---|
| F-1@0e25482b | Sözleşme §12'ye "Bekletir" sütunu: N-2, N-3, N-4, N-7, N-9, N-10 bekletir; N-8, N-11, N-12 bekletmez; başlık cümlesi ve Bölüm 18.1 dondurma kapısı buna göre | Bölüm 18.1; sözleşme başlık, §12 |
| F-2@0e25482b | Her revizyonun kendi incelemesi zorunlu (SOP §20.4/1); r10 incelemesi ve onayı açık | Bölüm 18 |
| F-3@0e25482b | DL-1 kapılarına taşıma kararının izi: `92beb1228`, `3ceb760e8`, onay `466aacfc9` | Bölüm 18 (r9 notu) |
| F-4@0e25482b | `LockAsync` dönüşü `LockResult(Matched, LockedNow)`; `GroupPrefixLockedNow = ModifiedCount == 1`; AC-17 yarışla (r11'de yarış ölçümü kaldırıldı, AC-17 sıralı — F-1@96476fa0, F-1@fb4cf83e) | Bölüm 3, 17, 21.3 Tablo A; AC-17; S-73 |
| F-5@0e25482b | Sayacın `tenant` etiketi: `tenant_id` yok / Guid değil / `Guid.Empty` → `platform` | Bölüm 0.1 (K-10); AC-44 |
| F-6@0e25482b | Bölüm 17'ye S-71 (AC-37 olmayan kiracı), S-72 (AC-44 `Guid.Empty`); AC-37'nin iki sabotajı tek satır | Bölüm 17; AC-37, AC-44 |
| F-7@0e25482b | Bölüm 0 önerilen kayıt satırı `ready-for-dev`; kimlik ön kontrolü r10 için yeniden koşuldu (çıkış 0) | Bölüm 0 |

**İnceleme 03, tur 8** — bulgu dosyası `docs/records/reviews/MOD-0040-FU02/2026-11-02-42b32c0a.md` (dosya özeti `96476fa0…`,
`git show 4288ae40e:<yol> | sha256sum`; hedef r10 pack sha256 `42b32c0a…4e2a`, kaynak `024e76fcd`); KOŞULLU, 1 ORTA. Sahip
kararı (Beste, 2026-11-02): **"B — r11: yarış testi çıkarılır, ölçüm sıralı yapılır"**; r11'de kapanır.

| Bulgu | Düzeltme | Bölümler |
|---|---|---|
| F-1@96476fa0 | AC-17'nin bariyerli yarış ölçümü kaldırıldı; yerine sıralı ölçüm: ilk `Group` ayırması `GroupPrefixLockedNow = true` ve `LockedAtUtc` yazar, sıradaki `false` ve `LockedAtUtc` değişmez (tek değer); S-73 sıralı testin "sıradaki `false`" adımında kırmızı; eşzamanlı ilk kilit Bölüm 8.6'da kabul edilen sınır | Bölüm 8.6, 13, 17; AC-17; S-73 |

**Son bakış 01, tur 5** — bulgu dosyası `docs/records/reviews/MOD-0040-FU02/2026-11-02-42b32c0a-son-bakis.md` (dosya özeti
`fb4cf83e…`, `git show 4288ae40e:<yol> | sha256sum`; hedef r10 pack sha256 `42b32c0a…4e2a`, kaynak `024e76fcd`); KOŞULLU, 1 ORTA,
2 DÜŞÜK. Sahip kararı aynı ("B"); r11'de kapanır.

| Bulgu | Düzeltme | Bölümler |
|---|---|---|
| F-1@fb4cf83e | Yarış ölçümü kaldırıldı, AC-17 sıralı (F-1@96476fa0 ile aynı); ilk kilit yarışı Bölüm 8.6'da kabul edilen sınır: ikinci işlem ayar satırında `WriteConflict`, yürütücü beklemeden en çok 3 gövde denemesi (`MaximumBodyAttempts = 3`, ölçüldü `4288ae40e`), tükenirse 503 `RECORD_CODE_UNAVAILABLE`, tüketici aynı anahtarla yeniden dener (sözleşme §8); `ModifiedCount` cümlesi "çıkarım — DL-1 testi ölçer" diye işaretli | Bölüm 3, 8.6, 13, 21.3 Tablo A; AC-17 |
| F-2@fb4cf83e | AC-44 sayaç etiketi cümlesi iki evre: sınır altı `Denied` satırı + `TargetTenantId = null`; sınır üstü satır yok + sayaç `tenant="platform"`; `RecordCodeAllocateRateLimiterTests` satırına `Guid.Empty` etiket ölçümü | AC-44; Bölüm 17 |
| F-3@fb4cf83e | Üst uyarı ve Bölüm 0 önerilen kayıt satırı sürüm numarasından bağımsız: onaylanan sürüm, `approvals.tsv`'de bu yol için son satırın sha256'sı | üst uyarı; Bölüm 0 |

### 19.4 r12 (DL-2) ölçüm kaynakları

Ölçüm commit'i `a9fd192ba` (dal `feature/sce/mod-0173-inventory-ledger-valuation`); aşağıdaki dosyalar `origin/main @ 084971b1e` ile
aynıdır (`git diff --stat origin/main HEAD -- <yollar>` boş, 2026-11-10). Okunan ve satırla atıf yapılan kaynaklar:

- Girdi kararları: `docs/records/decisions/2026-11/mod0040-fu02-owner-decisions-02.md:1-11` (`git show 6609910c8:<yol>`; bu dalda yok);
  `execution/approvals/approvals.tsv:61` (r11 onayı); `docs/records/audits/2026-11/mod-0040-fu02-dl1-ct-acceptance-2026-11-04.md:1-43`;
  `.antigravity/rules/record-coding-standard.md` (tamamı; §1 `:22-32`, §2 `:34-43`, §3 `:45-70`, §5.2 `:89-92`, §5.8 `:120-124`,
  §5.9 `:126-130`, §5.11 `:137-140`, §6 `:148-159`, §7 `:161-165`); `docs/analysis/contracts/record-code-allocate.md` (başlık,
  §6 ORG / POS satırı, §12, §13 — değişmedi).
- MOD-0288 kodu: `OrganizationUnit.cs:5-43`, `Position.cs:5-27`; `Features/TenantOrganization/Commands/CreateOrganizationUnitCommand.cs:8-14`,
  `UpdateOrganizationUnitCommand.cs`, `CreatePositionCommand.cs:8-14`, `UpdatePositionCommand.cs`; işleyiciler
  `CreateOrganizationUnitCommandHandler.cs:31-116`, `UpdateOrganizationUnitCommandHandler.cs:25-232`, `CreatePositionCommandHandler.cs:24-56`,
  `UpdatePositionCommandHandler.cs:19-56`, `PositionReferenceGuard.cs:8-71`; `TenantOrganizationContracts.cs:8-87`;
  `OrganizationCodeNormalizer.cs:5-31`; doğrulayıcılar `CreateOrganizationUnitCommandValidator.cs`, `CreatePositionCommandValidator.cs`,
  `UpdateOrganizationUnitCommandValidator.cs`, `UpdatePositionCommandValidator.cs`, `TenantOrganizationRequestValidators.cs:5-19`;
  depolar `OrganizationUnitRepository.cs:11-21`, `:96-110`, `PositionRepository.cs:10-40`; şema `PlatformSchemaManifest.Organization.cs:20-41`,
  `:126-137`; controller'lar `OrganizationUnitsController.cs:12-125`, `PositionsController.cs:12-53`; tohum `PositionSeed.cs:20-69`.
- DL-1 kodu: `RecordCodeTypeCatalog.cs:1-66`, `AllocateRecordCodeCommand.cs:1-22`, `AllocateRecordCodeHandler.cs:32-300`,
  `RecordCodesModels.cs:1-27`, `TrustedRecordCodeCredentialAuthenticator.cs:20-21`, `TrustedRecordCodeRequestExecutor.cs:125-183`,
  `InternalRecordCodesController.cs:25-50`, `MdmLegalEntityShortCodeReader.cs:14-57`, `ILegalEntityShortCodeReader.cs:7-9`,
  `TransactionOwnedAuditSourceServices.cs:10-17`; test `RecordCodeTypeCatalogTests.cs:20-97`.
- Platform altyapısı: `BaseEntity.cs:3-14`, `Repositories.cs:70-92`, `TenantResolutionMiddleware.cs:136-150`, `:222-232`, `:304-325`,
  `ITenantAuthorizationContext.cs:3-31`, `IActorPermissionContext.cs:1-35`, `Program.cs:141`, `ICorrelationContext.cs:3-15`,
  `AuditBehavior.cs:55-88`, `:160-185`, `Response.cs:5-80`, `Application/DependencyInjection.cs:45-52`.
- MDM: `services/Diten.MdmService/src/Diten.MdmService.Domain/Entities/LegalEntity.cs:28-60`.
- Web: `Views/Organization/OrganizationUnits/Form.cshtml:3-56`, `Views/Organization/Positions/Form.cshtml:9-54`,
  `wwwroot/assets/js/Organization/OrganizationUnits/form.js:31`, `:249`, `:274-275`, `:399`, `…/index.js:612-632`,
  `…/Positions/form.js:15`, `:152`, `:184-186`, `:206`; `Controllers/OrganizationUnitsController.cs:9-123`; 14 kaynak dosyasının
  dil listesi (`Resources/Views/Organization/{OrganizationUnits,Positions}/*.resx`); `tests/xmc-date-guard-screens.test.js:122`, `:127`;
  `tests/diten-field-icons.test.js:610-612`.
- Kurallar: `.antigravity/rules/localization-standard.md:8-9`, `.antigravity/rules/handler-design.md:16-38`,
  `.antigravity/rules/module-pack-standard.md:539-556` (var olan dilim tablosu geriye dönük değişmez — DL-2 satırı yalnız içerik),
  `scripts/pack_approval.py:1473-1474` (S-49), `tests/architecture/audit-ledger/Diten.Platform.md:12`, `:16`.

**Çıkarımlar (ölçüm değil):** POS'un şirket kaynağı birimdir (K-15'in POS'a uygulanışı — Q-5); `ISender` ile iç içe gönderimin
`AuditBehavior`'dan kayıtsız geçmesi (`AllocateRecordCodeCommand` işaretsiz olduğu için; DL-2 testi ölçer — AC-59 satır sayısı);
eski kodlardan desene uyan olup olmadığı (canlı veri okunmadı — AC-55 sayımı).

## 20. Follow-up Items

| # | İş | Sahip | Dilim |
|---|---|---|---|
| FU-RCS-01 | ORG / POS bağlanması (süreç içi port) — **r12'de yazıldı** (Bölüm 2.4, 8.14, 8.15; AC-48…AC-61); kod izni r12 (ya da ardılı) onayı + `ready-for-dev` künyesiyle | Beste | DL-2 |
| FU-RCS-17 | MOD-0288 merkezi oluşturma denetimi `EntityId` ve kod taşımaz (`CreateOrganizationUnitCommand.cs:11-13`, `CreatePositionCommand.cs:11-13` — `GetAuditMetadata` işleyiciden önce okunur); kod bugün yalnız ayırma satırında (Tablo A) görünür. Merkezi satırın zenginleştirilmesi | MOD-0288 sahibi (TBD) | — |
| FU-RCS-18 | Geliştirme tohumu `PositionSeed` elle kod yazar (`PositionSeed.cs:54`, `:65-69`); kullanılmayan `_CreateEditOffcanvas.cshtml` parçaları kod girişi taşır; `CodePlaceholder` / `InvalidCode` / `FinalCodePreview` anahtarları DL-2'den sonra kullanılmaz — temizlik | MOD-0288 sahibi (TBD) | — |
| FU-RCS-19 | Eski elle kodlarda silinmiş kaydın kodunun yeniden kullanımı (`ux_*_tenant_code_active` kısmi index'i ve `ExistsByCodeAsync`'in silinmişleri dışlaması — RCS-001 §5.8); DL-2 yalnız otomatik kodlarda kapatır | MOD-0288 sahibi (TBD) + Beste | — |
| FU-RCS-02 | İKİSİ DE motoru: öneri + gerekçeli değişiklik + AUD-001; §5.9 | Beste | DL-3 |
| FU-RCS-03 | TTY, PPM (PPM sahibi Codex), PLN bağlanması + Platform kapsamı | Beste + PPM sahibi | DL-4 |
| FU-RCS-04 | Kodu kayda bağlama / tüketme, kullanılmayan kod raporu | Beste | DL-3 / DL-4 |
| FU-RCS-05 | Tüzel Kişilik kısa kodu önerisi + §5.4 kalıcı kilidi (MDM) — karar A-18; **bağımlılık**, bağlı paket MOD-0220 (revizyon mu FU mu: TBD) | MDM sahibi (MOD-0220 `mdm-domain-team`; paket kararı Ali) + Beste | DL-5 (karar 2026-11-06) |
| FU-RCS-06 | İçe aktarma / yüksek su işareti; WMS geçici sayacından devam | Beste + MOD-0178 sahibi | DL-6 |
| FU-RCS-07 | RCS-001 §3 `CTR` → `AGR` + izinli kapsam sütunu — **bağımlılık**, bağlı paket yok (kural dosyası; ayrı paket gerekip gerekmediği TBD, CT) | Ali (`.antigravity`) + Treasury PR'ı | DL-7 |
| FU-RCS-08 | WMS türleri `PUT` / `PCK` / `OUT` / `PKG` + Inventory kimlik bilgisi (+ `Diten.InventoryService` kaynak servis kümesine) | Beste + MOD-0178 sahibi | DL-8 |
| FU-RCS-09 | Arka plan çağıranları için servis kimliği token'ı — **bağımlılık**, bağlı paket MOD-0033-FU02 (revizyon mu yeni FU mu: TBD); Platform tarafı (yürütücünün P3'ü kabulü) bu paketin DL-9 revizyonu | Auth: MOD-0033-FU02 sahip rolleri (`auth-security-owner` / `api-consumer-credential-owner`) + Ali; Platform: Beste | DL-9 |
| FU-RCS-10 | Platform yönetici ekranları | Beste | DL-10 |
| FU-RCS-12 | Doküman sayacındaki E11000 yarışı — MOD-0029 sahibine bildirilir | MOD-0029 sahibi | — |
| FU-RCS-13 | Kimlik bilgisi döndürme / iptal prosedürü (dev-runbook): yeni `ActiveSecret` → eski `PreviousSecret` + `PreviousValidUntilUtc` → tüketici geçişi → eskiyi sil; acil durumda `IsRevoked`; değer depoya yazılmaz | Beste + tüketici sahipleri | DL-1 kabulü |
| FU-RCS-14 | İkinci iş kiracısı farklı grup önekiyle gelirse belge `OrgPrefix`'in kiracı başına olması | Beste + MOD-0029 sahibi | — |
| FU-RCS-15 | MDM lookup-validation v1.1 (`approvalStatus`) yayınlandığı sürümde okuyucunun "alan yok → 503" kuralına geçirilmesi (kod değişikliği) ve TC-3'ün kaldırılması; AC-36 o revizyonda güncellenir | MOD-0220 sahibi (yayın) + Beste (kod) | v1.1 yayını |
| FU-RCS-16 | Grup önekinin kilitten sonra bilinçli değiştirilmesi (yeni önek, yeni sayaç, belge kodlarıyla birlikte) — sahip kararı | Beste + MOD-0029 sahibi | — |

(FU-RCS-11 kapandı: askıdaki kiracı DL-1'e alındı — inceleme 01 F-12, AC-37.)

## 21. Audited Events

Servisin denetim altyapısı (`tests/architecture/audit-ledger/Diten.Platform.md:8-17`): `platform-merkezi` (a, işaret),
`platform-islem-ici` (a, yazıcı; "üçü de tek girişten (`CanonicalTransactionalAuditOutboxWriter`) geçer"),
`esign-islem-ici-denetim` (a, yazıcı). Bu paketin çağrısı da aynı tek girişten geçer; belirteç
`RecordCodeAuditIntent.EnqueueAsync/RecordCodeAuditIntent.EnqueueRefusalAsync/RecordCodeAuditIntent.EnqueueGroupPrefixAlignedAsync/RecordCodeAuditIntent.EnqueueGroupPrefixAlignRefusalAsync` (üçüncüsü r5 F-1, dördüncüsü r6 F-1) `platform-islem-ici` satırına CT kararıyla
eklenir (A-10). Karar yoksa DL-1 main'e giremez.

### 21.1 Komutlar

| Komut | Olay adı | Nesne (tür) | Yol | Önceki/sonraki ya da değişen alanlar | İstisna sınıfı + gerekçe |
|---|---|---|---|---|---|
| `AllocateRecordCodeCommand` (başarı) | `platform.record-code.allocated` | `RecordCodeAllocation` | a — işlem içi kapı (`RecordCodeAuditIntent.EnqueueAsync` → `ITransactionalAuditOutboxWriter.TryEnqueueAsync`, `Intent.SourceService` = kapalı listedeki ad; kutu anahtarı `record-code:allocated:{allocationId:N}`) | **yük Bölüm 21.3 Tablo A** (tek tanım, r4 F-2) | — |
| `AllocateRecordCodeCommand` (ret) | `platform.record-code.allocation-refused` | `RecordCodeAllocation` (kimliksiz) | a — ayrı kısa işlem (`RecordCodeAuditIntent.EnqueueRefusalAsync` → `ITransactionalAuditRefusalWriter`; kutu anahtarı `record-code:refused:{yeni Guid:N}`) | **yük Bölüm 21.3 Tablo B** (alanların hangi adımda elde olduğuyla); sonuç Bölüm 21.2 | — |
| `AlignRecordCodeGroupPrefixCommand` | `platform.record-code.group-prefix.aligned` | `RecordCodeGroupPrefix` | a — **işlem içi kapı** (r5, inceleme 03 tur 2 F-1; CT kararı 2026-11-02): komut `ITransactionOwnedAuditCommand`, `TransactionOwnedAuditCommands.Authorized`'da adıyla; `RecordCodeAuditIntent.EnqueueGroupPrefixAlignedAsync` → `ITransactionalAuditOutboxWriter.TryEnqueueAsync`, ayar yazımıyla aynı işlemde; aktör `PlatformAdministrator`; `TenantId` = `TargetTenantId` = rota kiracısı | **yük Bölüm 21.3 Tablo C** | — |
| `AlignRecordCodeGroupPrefixCommand` (ret) | `platform.record-code.group-prefix.align-refused` | `RecordCodeGroupPrefix` | a — ayrı kısa işlem (`RecordCodeAuditIntent.EnqueueGroupPrefixAlignRefusalAsync` → `ITransactionalAuditRefusalWriter`; r6, inceleme 03 tur 3 F-1, sahip kararı (a)); aktör `PlatformAdministrator`; `Outcome = Failed` | **yük ve kiracı bağı Bölüm 21.3 Tablo D** (404'te platform denetim kiracısı) | — |

### 21.2 Hangi ret yazılır (K-10, K-13)

**Tek `Outcome` kuralı — tek biçim (r7, son bakış 01 F-4; r8, tur 2 F-3).** Kural paketin her yerinde bu cümleyle
uygulanır (Bölüm 0.1 K-10, Bölüm 4 adım 4, bu tablo, sözleşme §11):

> **`Denied`** = (a) istekçi (kimlik bilgisi, kullanıcı ya da kiracı) bu işi yapmaya **yetkili değil**, ya da (b) istenen kayıt
> istekçiye **tek gövdeli bir "yok" cevabıyla gizlenir** (yok / başka kiracının / aktif değil ayırt edilmez). **`Failed`** =
> diğer her ret (biçim, iş kuralı, yapılandırma / ana veri, kaynak erişilemezliği).

Kolların kaynağı ayrıdır: (a) **koddan ölçüldü** — `AuditBehavior` başarısız iş cevabını `Failed` yazar (`IsFailureResponse`),
`Denied` kodda yalnız yetki retlerinde geçer (`PartnerAdminScopeAttribute`, `PlatformEntitlementAuditSink`,
`SelfAccessExplainService`). (b) **paket kararıdır** (r8; kodda karşılığı yoktur, sahip onayı CT-APR-01 ile): kiracılar arası
yoklamayı yetki reddi gibi iz bırakacak biçimde işaretlemek için; bugün yalnız 404 `LEGAL_ENTITY_NOT_FOUND` bu kola girer.
r6'da `Denied` yazılan 422 `RECORD_CODE_SCOPE_INVALID` iki kola da girmez → r7'den beri **`Failed`**.

| Cevap | Yazılır mı | Outcome | Gerekçe |
|---|---|---|---|
| 413 boyut | hayır (log) | — | gövde okunmadan, kimlik doğrulanmadan; kiracı ve kişi adlandırılamaz |
| 401 (her türü) | hayır (log) | — | kimliksiz istek: ne kiracı ne kişi adlandırılabilir (`AuditOutboxWriteRequest.cs:28-30` kiracı ister, yazıcı `:156-185` kişi ister); herkesin bir kiracının günlüğünü doldurmasını önler |
| 403 kiracı çözülmeden (yapılandırma geçersiz, hedef kitle / tüketici yanlış, `X-Tenant-Id` — kullanıcı token'ından önce denetlenir —, eksik `tenant_id` / `sub`) | hayır (log) | — | aynı gerekçe: doğrulanmış kiracı + kişi yok |
| 403 `RECORD_CODE_FORBIDDEN` — **platform aktörü token'ı** (`actor_type ∈ {platform_admin, partner_admin}`, token geçerli) | **evet** (r7, K-13) | **Denied** | yetki reddi, kişi adlandırılabilir; yürütücü yazar; kayıt platform denetim kiracısında (`AuditTenantIds.PlatformSystemTenantId`), `TargetTenantId` token'daki `tenant_id` (yoksa, Guid değilse ya da `Guid.Empty` ise `null`) — emsal `PartnerAdminScopeAttribute` (AC-44). **Yazım hız sınırlayıcıdan geçer** (r8, tur 5 F-2): sınırı aşan istekçinin retleri yazılmaz (429 ile aynı adlı sapma), cevap yine 403 |
| 429 hız sınırı | **hayır — AUD-001 §3'ten adlı sapma** (K-10, K-13; sahip kararı Beste 2026-11-02) | — | taşkın anında denetimi şişirir, kiracının gerçek izini boğar; yerine uyarı logu + `record_codes_rate_limited_total` sayacı (AC-33) |
| 403 `RECORD_CODE_TENANT_NOT_ACTIVE` | evet | Denied | kiracı bu işi yapmaya yetkili değil (durumu). **Kayıt kiracısı (r9, son bakış 01 tur 3 F-3):** kiracı kaydı varsa (askıda, sağlanıyor, devre dışı) token kiracısı; kiracı kaydı **yoksa ya da silinmişse** platform denetim kiracısı `AuditTenantIds.PlatformSystemTenantId`, `TargetTenantId` = token kiracısı — Tablo D'nin 404 ilkesiyle aynı (olmayan kiracıya kayıt bağlanmaz) |
| 403 `RECORD_CODE_FORBIDDEN` (tür kimlik bilgisine izinsiz) | evet | Denied | yetki; sızma tespiti |
| 403 `RECORD_CODE_PERMISSION_MISSING` | evet | Denied | yetki |
| 403 `LEGAL_ENTITY_READ_PERMISSION_MISSING` (MDM 403; r7, F-10) | evet | Denied | yetki (kullanıcıda MDM okuma izni yok) |
| 400 `RECORD_CODE_REQUEST_INVALID` | **evet** (r7, K-13) | **Failed** | biçim reddi; kimlik ve kiracı doğrulanmış (adım 2 geçti), iz bırakır; `EvaluationStep = 5`, gövde anahtarları yok (Tablo B) |
| 400 `IDEMPOTENCY_KEY_REUSED` | evet | Failed | yanlış kullanım, iz bırakmalı |
| 404 `LEGAL_ENTITY_NOT_FOUND` | evet | Denied | kol (b), paket kararı: tek gövdeli 404 yok / başka kiracı / aktif değil ayırt etmez; kiracılar arası yoklama izi |
| 409 `LEGAL_ENTITY_SHORT_CODE_MISSING`, `LEGAL_ENTITY_SHORT_CODE_CHANGED`, `GROUP_PREFIX_MISSING`, `GROUP_PREFIX_MISMATCH` | evet | Failed | yapılandırma / ana veri tutarsızlığı |
| 422 `RECORD_CODE_SCOPE_INVALID` | evet | **Failed** (r7, F-4; r6'da Denied) | istek / iş kuralı: tür bu kapsama izin vermiyor |
| 422 `RECORD_TYPE_UNKNOWN`, `LEGAL_ENTITY_NOT_ELIGIBLE` | evet | Failed | iş kuralı |
| 503 `LEGAL_ENTITY_SOURCE_UNAVAILABLE` | evet (en iyi çaba) | Failed | kaynak erişilemez; Platform deposu çalışıyorsa yazılır |
| 503 `RECORD_CODE_UNAVAILABLE` | dener (en iyi çaba) | Failed | depo erişilemiyorsa ret kaydı da yazılamaz → uyarı logu (r8: kilit filtresi eşleşmezliği artık 409 `GROUP_PREFIX_MISMATCH`, Bölüm 8.6) |

- Her ret kaydının kutu anahtarı yeni bir Guid'dir; aynı istemci anahtarıyla gelen sonraki başarılı ayırma onunla çakışmaz.
- **K2 sınıfı değil** (kimlik, rol/izin, kiracı durumu, GxP, KVKK özel veri değişmez). Başarı kaydı yine işlem içidir
  (kayıt yoksa kod yok).
- **Kayda yazılmayan:** sır, `Authorization`, JWT, MDM gövdesi; kişi verisi yok (kimlikler; e-posta/ad kapının maskesiyle).
- **Okuma denetimi:** yok (AUD-001 §7'nin üç sınıfına girmez).
- **BL-501:** kullanılmaz (AUD-001 §5 yol a). Tüketicinin Create kaydı tüketicinin yoluyla; eşleşme `idempotencyKey` + `code` (TC-7).

### 21.3 Denetim yükü — tek tanım (r4, inceleme 03 tur 1 F-2; Tablo C r5, tur 2 F-1; Tablo D r6, tur 3 F-1)

Bu bölüm başarılı ayırma, ret ve grup öneki hizalaması kayıtlarının **tek** tanımıdır; Bölüm 21.1, AC-17, AC-19, AC-22,
AC-43 ve (ayırma için) sözleşme §11 aynı alanları
aynı yerde ve aynı yazımla anar, kendi listelerini tutmaz. **Ölçülen kapı davranışı** (`CanonicalTransactionalAuditOutboxWriter.TryEnqueueAsync`,
`services/Diten.Platform/src/Diten.Platform.Application/Features/Audit/CanonicalTransactionalAuditOutboxWriter.cs`):
`intent.BeforeState`, `intent.AfterState` ve `intent.Metadata` **olduğu gibi** `AuditCanonicalRecord`'a kopyalanır; kapı
`Metadata`'ya kendiliğinden yalnız `SystemActor` (principal yokken; bu uçta yok) ve `ClientCorrelation`
(`AuditCorrelation.ClientCorrelationMetadataKey`, istemcinin korelasyon başlığı varsa) ekler; `Outcome` bugün sabit
`Succeeded`, `SourceService` bugün sabit `"Diten.Platform"` (K-9 / K-10 ile eklemeli genişler, Bölüm 5); aktör principal'dan
(`ResolveActor`). Anahtar yazımı PascalCase'dir (emsal `PhysicalEntitlementAuditIntent`: `ModuleCode`, `IsEnabled`);
sensitive-field redactor'ı (`SensitiveFieldRedactionRegistry`) bu adların hiçbirini maskelemez (ölçüldü: `IdempotencyKey`
"apiKey" / "token" / "secret" içermez). Olay adı `RequestType`'ta değil `Metadata.EventName`'dedir (emsal
`CancelNotificationDispatchCommand`: `["EventName"] = "notifications.dispatch.cancelled"`); `RequestType` komut adıdır.

**Tablo A — başarılı ayırma** (`RecordCodeAuditIntent.EnqueueAsync`, ayırmayla aynı işlem):

| Yer | Alan | Değer |
|---|---|---|
| `AuditOutboxWriteRequest` | `TenantId` | token kiracısı |
| | `IdempotencyKey` | `record-code:allocated:{allocationId:N}` |
| | `RequestType` | `AllocateRecordCodeCommand` |
| | `Operation` / `EntityType` / `EntityId` | `Create` / `RecordCodeAllocation` / `allocationId` (defter satırının `Id`'si) |
| `Intent` | `Category` | `PlatformConfiguration` |
| | `TargetTenantId` | token kiracısı |
| | `SourceService` | kimlik bilgisinin kapalı listedeki adı (ör. `Diten.FinanceTreasuryService`) |
| | `SourceModule` | `MOD-0040-FU02` |
| | `BeforeState` | yok (`null`) |
| | `AfterState` | `Code`, `Sequence`, `RecordType`, `PrefixScope` (varsayılan uygulanmış, enum adı), `Prefix`, `LegalEntityId` (Group'ta `null`) |
| | `Metadata` | `EventName` = `platform.record-code.allocated`, `IdempotencyKey` (istemcinin anahtarı), `GroupPrefixLockedNow` (bool; kaynağı `LockAsync` dönüşünün `LockedNow`'ı = `ModifiedCount == 1` — r10, son bakış 01 tur 4 F-4; zaten kilitli satırda `ModifiedCount == 0` olduğu çıkarımdır, AC-17 sıralı ölçümü ölçer — r11; LegalEntity'de daima `false`) |
| Kapının eklediği | aktör, `Outcome`, `SourceService`, `ClientCorrelation` | `TenantUser` + `sub`; `Succeeded`; `Intent.SourceService`; başlık varsa |

**Tablo B — ret** (`RecordCodeAuditIntent.EnqueueRefusalAsync`, ayrı kısa işlem). `RequestType`, `Operation`, `EntityType`,
`Category`, `TargetTenantId`, `SourceService`, `SourceModule` Tablo A ile aynıdır; `EntityId` boş (`null`), kutu anahtarı
`record-code:refused:{Guid.NewGuid():N}`, `BeforeState` / `AfterState` yok, `Outcome` Bölüm 21.2'deki değer. `Metadata`
anahtarları ve **elde oldukları adım** (Bölüm 8.5 numaraları; adımda elde olmayan anahtar **yazılmaz** — `null` değil):

| Anahtar | Değer | Elde olduğu adım | Yazılan retler |
|---|---|---|---|
| `EventName` | `platform.record-code.allocation-refused` | sabit | her yazılan ret |
| `ReasonCode` | cevaptaki `reason_code` | ret anı | her yazılan ret |
| `EvaluationStep` | retin adım numarası (2 — yalnız platform aktörü —, 4…13) | ret anı | her yazılan ret |
| `IdempotencyKey`, `RecordType`, `PrefixScope` (varsayılan uygulanmış), `LegalEntityId` | gövdedeki değerler (`LegalEntityId` Group'ta yok) | **adım 5'ten sonra** (gövde ayrıştırıldı) | adım 6–13 retleri; **adım 4 reddinde (`RECORD_CODE_TENANT_NOT_ACTIVE`) ve adım 5 reddinde (400) yoktur** — gövde ayrıştırılmamıştır |
| `Prefix` | çözülen önek | adım 11'de önek çözüldükten sonra | `LEGAL_ENTITY_SHORT_CODE_CHANGED` (MDM kısa kodu `K`), `GROUP_PREFIX_MISMATCH` — **bu sürecin etkin `OrgPrefix`'i** (adım 11'de ayarla karşılaştırılan değer = adım 13'te kilitlenmeye çalışılan önek; iki kaynakta aynı tanım — r9, son bakış 01 tur 3 F-1), adım 12–13'teki 503 |
| `Code`, `Sequence` | — | — | **hiçbir ret yazmaz** (ret kod üretmez; adım 12'de alınmış numara boşluk kalır ve loglanır) |

Aktör (`TenantUser` + `sub`) ve kiracı adım 2'de doğrulanmıştır; adım 2 öncesi ve adım 2–3 retleri yazılmaz (Bölüm 21.2) —
**iki istisna (r7, K-13):** (1) adım 2'deki platform aktörü reddi yazılır: `AuditOutboxWriteRequest.TenantId` =
`AuditTenantIds.PlatformSystemTenantId`, `Intent.TargetTenantId` = token'daki tam `tenant_id` (yoksa, Guid değilse ya da `Guid.Empty` ise `null`), aktör
`PlatformAdministrator` / `PartnerAdministrator`, `Outcome = Denied`, `Metadata` = `EventName`, `ReasonCode`
(`RECORD_CODE_FORBIDDEN`), `EvaluationStep = 2` — gövde anahtarları yok; (2) adım 5 reddi (400) Tablo B'deki gibi yazılır
(`EvaluationStep = 5`, gövde anahtarları yok). 429 (adım 3) adlı sapmadır, yazılmaz. Adım 4 reddinde kiracı kaydı yok ya da silinmişse kayıt platform denetim kiracısındadır, `TargetTenantId` = token kiracısı (r9, son bakış 01 tur 3 F-3; Bölüm 21.2).

**Tablo C — grup öneki hizalaması** (`RecordCodeAuditIntent.EnqueueGroupPrefixAlignedAsync`, ayar yazımıyla aynı işlem;
r5, inceleme 03 tur 2 F-1). Önceki değer işleyicide, aynı işlemde, `TenantScope.Begin(_tenantContext, routeTenantId)` içinde
`GetAsync(session, routeTenantId)` ile okunur (Bölüm 4 "Yönetici ucu" adım 3):

| Yer | Alan | Değer |
|---|---|---|
| `AuditOutboxWriteRequest` | `TenantId` | rota kiracısı |
| | `IdempotencyKey` | `record-code:group-prefix-aligned:{alignmentId:N}` — `alignmentId` her gövde denemesinde yeni Guid |
| | `RequestType` | `AlignRecordCodeGroupPrefixCommand` |
| | `Operation` / `EntityType` / `EntityId` | ilk hizalamada `Create`, sonrakilerde `Update` / `RecordCodeGroupPrefix` / ayar satırının `Id`'si |
| `Intent` | `Category` | `PlatformConfiguration` |
| | `TargetTenantId` | rota kiracısı |
| | `SourceService` | boş (`Diten.Platform`) |
| | `SourceModule` | `MOD-0040-FU02` |
| | `BeforeState` | `GroupPrefix` (önceki değer), `LockedAtUtc` (önceki; ISO-8601 metin ya da `null`) — **ilk hizalamada `BeforeState` yoktur** (`null`), sonraki her hizalamada iki anahtar da **zorunludur** |
| | `AfterState` | `GroupPrefix` (= etkin `OrgPrefix`), `LockedAtUtc` (yazımdan sonraki; kilitsiz hizalamada `null`) |
| | `Metadata` | `EventName` = `platform.record-code.group-prefix.aligned` |
| Kapının eklediği | aktör, `Outcome`, `SourceService`, `ClientCorrelation` | `PlatformAdministrator` + yöneticinin kimliği; `Succeeded`; `Diten.Platform`; başlık varsa |

Değişiklik yoksa (aynı değer, kilitli ya da kilitsiz → 200) ayar yazılmaz ve denetim satırı **yazılmaz** (ret değil, başarılı
"değişiklik yok" cevabıdır). r5'teki "hizalama retleri denetime yazılmaz" kuralı **r6'da kaldırıldı** (inceleme 03 tur 3
F-1; AUD-001 §3'e aykırıydı, sahip kararı (a) ile retler yazılır — Tablo D).

**Tablo D — hizalama retleri** (`RecordCodeAuditIntent.EnqueueGroupPrefixAlignRefusalAsync` →
`ITransactionalAuditRefusalWriter.TryEnqueueRefusalAsync`, iş işleminden **ayrı kısa işlem**, K-10 / Bölüm 8.11 ile aynı
yazıcı; r6, sahip kararı Beste 2026-11-02 seçenek (a)). Ortak alanlar: `RequestType` `AlignRecordCodeGroupPrefixCommand`,
`EntityType` `RecordCodeGroupPrefix`, `Category` `PlatformConfiguration`, `SourceService` boş (`Diten.Platform`),
`SourceModule` `MOD-0040-FU02`, `BeforeState` / `AfterState` yok, kutu anahtarı
`record-code:group-prefix-align-refused:{Guid.NewGuid():N}` (her ret için yeni), aktör principal'dan (`platform_admin` →
`PlatformAdministrator` + yöneticinin kimliği), `Intent.TargetTenantId` = **rota kimliği** (her satırda; tek istisna `Guid.Empty` rota değeri — bkz. son paragraf),
`Outcome` = `Failed`
(gerekçe Bölüm 4 adım 4), `Metadata.EventName` = `platform.record-code.group-prefix.align-refused`.

| Ret | Adım | Kaydın kiracısı (`AuditOutboxWriteRequest.TenantId`) | `Operation` / `EntityId` | `Metadata` (EventName dışında) |
|---|---|---|---|---|
| 404 kiracı yok / silinmiş | (i) | **platform denetim kiracısı** `AuditTenantIds.PlatformSystemTenantId` (`…0000-000000002121`, `services/Diten.Platform/src/Diten.Platform.Domain/Entities/Audit/AuditTenantIds.cs`) | `Update` / yok | `ReasonCode` = `TENANT_NOT_FOUND`, `HttpStatus` = 404 |
| 400 gövde var | (ii) | rota kiracısı | ayar satırı varsa `Update` + satırın `Id`'si, yoksa `Create` / yok | `ReasonCode` = `RECORD_CODE_REQUEST_INVALID`, `HttpStatus` = 400 |
| 409 `GROUP_PREFIX_SOURCE_INVALID` | (iii) | rota kiracısı | aynı | `ReasonCode`, `HttpStatus` = 409, `EffectiveOrgPrefix` (yapılandırmadaki değer; gizli değil) |
| 409 `GROUP_PREFIX_LOCKED` | (iv) | rota kiracısı | `Update` + satırın `Id`'si | `ReasonCode`, `HttpStatus` = 409, `LockedGroupPrefix`, `EffectiveOrgPrefix` |
| hizalama işlemi erişilemez (5xx) | gövde | rota kiracısı | aynı | `ReasonCode` = `RECORD_CODE_UNAVAILABLE` — **dener (en iyi çaba)**, depo erişilemiyorsa ret kaydı da yazılamaz → uyarı logu (Bölüm 21.2'deki 503 satırıyla aynı kural) |

**404'ün kiracı bağı (ölçüldü).** Var olmayan bir kiracıya kayıt bağlanamaz (kiracı günlüğü olmayan bir kimliğe yazmak sahte
bir kiracı izi açardı). Kod iki şeyi gösterir: (1) merkezi yolda platform-global olay `AuditService.ResolveTenantId`
(`…/Application/Features/Audit/AuditService.cs`) ile `AuditTenantIds.PlatformSystemTenantId`'ye bağlanır ve `audit_outbox`'a
`TenantId = …2121` ile yazılır (`AppendAsync` → `AuditOutboxWriteRequest.TenantId = tenantResolution.TenantId`); (2) emsal
`PartnerAdminScopeAttribute` kiracıya yönelik bir yönetici reddini tam bu biçimde yazar (`IsPlatformGlobal = true`,
`TargetTenantId` = hedef kimlik, "A platform security event (filed under the platform), naming the tenant it was aimed
at"). İşlem içi ret yazıcısının `IsPlatformGlobal` bayrağı yoktur; aynı sonuç `AuditOutboxWriteRequest.TenantId =
AuditTenantIds.PlatformSystemTenantId` verilerek elde edilir (yazıcı `TenantId`'yi olduğu gibi kayda koyar —
`CanonicalTransactionalAuditOutboxWriter.TryEnqueueAsync`, `AuditCanonicalRecord(TenantId: request.TenantId, …)`).
Bu değerle kutu satırının işlem içi eklemedeki teslim eşleyicisinden (`AuditOutboxRepository.TryInsertAsync` içindeki
`PayloadGate.Map`) geçtiği koddan çıkarılamaz — **AC-43 ölçer**; geçmezse 404 reddi yazılamaz, bu bir **AUD-001 §3
sapmasıdır** ve paket revizyonuna döner (sessizce "yazılmaz" denmez).
`Guid.Empty` rota değeri (`{tenantId:guid}` kısıtından geçer): yazıcı `Intent.TargetTenantId == Guid.Empty`'yi reddeder ama
`null`'u (platform-global) kabul eder (`CanonicalTransactionalAuditOutboxWriter.TryEnqueueAsync`, "names an empty target
tenant" denetimi yalnız `Guid.Empty` eşitliğidir; `TransactionOwnedAuditIntent.TargetTenantId` `Guid?`, belgesi "null when
platform-global"). Bu yüzden bu 404'te `TargetTenantId = null` ve `Metadata.RouteTenantId` = `00000000-0000-0000-0000-000000000000`
yazılır; kayıt yine platform denetim kiracısındadır. Sapma yoktur; `null` hedefin teslimden geçtiğini AC-43 ölçer.

### 21.4 DL-2 (r12) — ORG / POS

Servisin denetim altyapısı bugün vardır: `platform-merkezi` ve `platform-islem-ici` izleri kabul edilmiş (`a`)
(`tests/architecture/audit-ledger/Diten.Platform.md:12`, `:16`; `RecordCodeAuditIntent.*` belirteçleri `:16`'da, A-10 `658945363`).
DL-2 **yeni komut ve yeni belirteç açmaz**; defter değişmez.

| Komut | Olay adı | Nesne (tür) | Yol | Önceki / sonraki ya da değişen alanlar | Not |
|---|---|---|---|---|---|
| `CreateOrganizationUnitCommand` / `CreatePositionCommand` (MOD-0288; arayüz değişmez) | merkezi satır (`RequestType` = komut adı) | `OrganizationUnit` / `Position` | a — `platform-merkezi` (`IAuditableCommand` + `IAuditMetadataProvider`, `CreateOrganizationUnitCommand.cs:8-14`, `CreatePositionCommand.cs:8-14`) | bugünkü gibi — `EntityId` / `AfterState` yok (FU-RCS-17) | başarısız cevap `Outcome = Failed` (`AuditBehavior.cs:69`): ayırma reddi, `RECORD_CODE_SYSTEM_ASSIGNED`, `RECORD_CODE_COLLISION`, port savunma retleri |
| `AllocateRecordCodeCommand` (süreç içi, başarı) | `platform.record-code.allocated` | `RecordCodeAllocation` | a — `platform-islem-ici` (`RecordCodeAuditIntent.EnqueueAsync`, ayırmayla aynı işlem) | **Tablo A**; `SourceService = Diten.Platform`, `Metadata.IdempotencyKey = organization-unit:{id:N}` / `position:{id:N}`, `AfterState.Code` = kaydın kodu | kayıt yazımı sonradan düşerse satır kalır (boşluk, Bölüm 8.14) |
| `AllocateRecordCodeCommand` (süreç içi, ret) | `platform.record-code.allocation-refused` | `RecordCodeAllocation` (kimliksiz) | a — ayrı kısa işlem (`RecordCodeAuditIntent.EnqueueRefusalAsync`) | **Tablo B**; adım 4–13; `Outcome` Bölüm 21.2 | — |
| `UpdateOrganizationUnitCommand` / `UpdatePositionCommand` (kod değişikliği reddi) | merkezi satır | `OrganizationUnit` / `Position` | a — `platform-merkezi` | 409 `RECORD_CODE_IMMUTABLE` → `Failed` | yeni komut yok |

- Portun savunma retleri (kiracı çelişkisi, `ActorType ≠ tenant_user`, boş `sub`) ayırmaya ulaşmaz; ayırma ret satırı yazılmaz —
  Bölüm 21.2'deki "403 kiracı çözülmeden" satırıyla aynı gerekçe (doğrulanmış kiracı + kişi ikilisi yok); merkezi `Failed` oluşturma
  satırı iz bırakır.
- K2 sınıfı değil; okuma denetimi yok; BL-501 kullanılmaz (Bölüm 21.2 ile aynı).

## 22. Record Codes

Bu paket kendi kullanıcıya görünen kayıt türünü **açmaz**. Servisin verdiği türler ve kuralları Bölüm 4.4'tedir (RCS-001
§6'nın servis tarafı):

- Sınıf: §3'teki sınıfla (OTOMATİK; `PLN` İKİSİ DE, bağlanması DL-4).
- Önek (§5.5): 3 büyük harf, §3'te merkezi; `AGR` için §3 güncellemesi DL-7 (Ali + Treasury PR'ı). **`AGR` kalır (K-12 a):**
  `AGR` harfleri doküman kodunda tür parçası olarak da geçer (`GMG-QMS-AGR-0001`, `DocumentManagementIdentifiersModels.cs:65`),
  ama iki biçim farklıdır — doküman kodu `{ORG}-{ALAN}-{TÜR}-{4 hane}` (dört parça, ikinci parça alan kodu `QMS`), kayıt kodu
  `{ŞİRKET}-{TÜR}-{6+ hane}` (üç parça); aynı dizgi iki biçimde üretilemez, RCS-001 §3/§5.5 tablosunda doküman tür kodları
  yer almaz (doküman KALSIN) — çakışma yok.
- Ortak servis (§5.12): bu paket o servistir; tüketicinin kendi sayacı yasak (§6).
- Değiştirilemezlik: OTOMATİK kod değiştirilemez; servis değiştirme ucu sunmaz.
- Benzersizlik: `(TenantId, Code)` unique.
- Testler: eşzamanlı ayırma (AC-4), yeniden kullanılmaz (AC-6, AC-20), 999999 → 7 hane (AC-7), ASCII (AC-15).
- **DL-2 (r12) — `ORG` / `POS` (RCS-001 §6 kontrol listesi):** sınıf OTOMATİK (§3); önek `ORG` / `POS` (§3, çakışma yok);
  ortak servis süreç içi port üzerinden (§5.12; MOD-0288 kendi sayacını yazmaz, normalleştirici oluşturma yolunda kullanılmaz);
  kapsam `LegalEntity` (K-15) — `{ŞİRKET}-ORG-000001`, `{ŞİRKET}-POS-000001`; oluşturulduktan sonra değiştirilemez, API reddeder
  (AC-53); benzersizlik kiracı genelinde, silinmiş kayıtların kodu da dolu sayılır (Bölüm 8.15, AC-55); mevcut elle kodlar
  korunur (§2, §7; Q-1 A). Eşzamanlı iki oluşturmanın farklı kod alması DL-1'in AC-4'ü ile aynı sayaçtır; 999999 ve ASCII DL-1
  AC-7 / AC-15 ile ölçülü.

## 23. Teslim dilimleri

| dilim | ad | kapsadığı bölümler | kabul ölçütleri ve kapsam notu |
|---|---|---|---|
| DL-1 | v1 servis — ilk commit grubu aktör ölçümü; allocate iç ucu, atomik sayaç, LegalEntity ve Group kapsamları, OTOMATİK tür önek kaydı, şirket kısa kodu ve uygunluk okuması, kısa kod kullanım kilidi (ara dönem), idempotency, işlem içi denetim (aktör kullanıcı, kaynak servis, ayırma kimliğinden kutu anahtarı), ret kaydı, denetim yazıcısının eklemeli genişletmesi, kiracı izolasyonu ve askıdaki kiracı, kimlik bilgisi azaltıcıları ve hız sınırı, grup öneki hizalaması | Owned Objects, Entity Fields, Repo Scope, Dependencies, Runtime Constraints, Backend File Convention, Validation Rules, Failure Path to Verify, Authorization Convention, Gateway / API Routing Decision, Acceptance Criteria, Test Expectations, Audited Events, Record Codes | AC-1…AC-24 ve AC-26…AC-47; AC-41 ilk commit grubu (başarısızsa geri alınır, dilim durur); Platform kapsamı tür izni geçtiyse 422 (K-11); hedef main 2026-11-06; sözleşme 2026-11-03'te donar; **merge kapısı** A-10 CT defter kararı ve A-12; **kabul kapısı** A-1, A-11, A-16, A-17, sözleşme dondurma (Bölüm 18.1; r9 CT kararı 2026-11-02) — kod yazımını bekletmez |
| DL-2 | OTOMATİK mevcut modüllerin bağlanması — Organizasyon Birimi (ORG) ve Pozisyon (POS), süreç içi port | Ownership and Boundaries, Owned Objects, Entity Fields, Repo Scope, Protected Paths, Dependencies, Runtime Constraints, Layout & Shell Contract, Frontend File Contract, Validation Rules, Failure Path to Verify, Authorization Convention, Gateway / API Routing Decision, Acceptance Criteria, Test Expectations, Audited Events, Record Codes, Follow-up Items | **r12:** AC-48…AC-61; A-8 kapandı (K-15, 2026-11-10); süreç içi port `IInProcessRecordCodeAllocator` (Bölüm 8.14), sözleşme 1.0.0 değişmez (HTTP'de ORG / POS 403); MOD-0288 dosyaları XMC-001 (Bölüm 5 "DL-2"); **başlama kapısı** Q-1, Q-2, Q-3, Q-4 sahip kararı, Q-7 XMC-001 onayı, r12 (ya da ardılı) inceleme + onay ve künye `ready-for-dev` (S-49, Q-6); **kabul kapısı** eski kod sayımı (AC-55); kod izni yok (r12 `draft`) |
| DL-3 | İKİSİ DE motoru — öneri, gerekçeli değişiklik, AUD-001 eski/yeni kod + gerekçe, RCS-001 §5.9 | Ownership and Boundaries, Audited Events, Follow-up Items | kabul ölçütleri bu dilimin paket revizyonunda; kod izni yok |
| DL-4 | Platform kapsamı ve İKİSİ DE modüllerinin bağlanması — Abonelik Planı (PLN, öneksiz kod), Görev Türü (TTY), PPM (PFL/PRG/PRJ/INI/INV) | Ownership and Boundaries, Runtime Constraints, Acceptance Criteria, Record Codes, Follow-up Items | AC-25 (K-11'den taşındı) ve bu dilimin revizyonunda eklenecekler; kiracısız satır tabanı ve platform-global denetim yolu burada kararlaştırılır; kod izni yok |
| DL-5 | Tüzel Kişilik kısa kodu önerisi ve RCS-001 §5.4 kalıcı kullanım kilidi (MDM tarafı) — **bağımlılık**: MDM kodu bağlı paket MOD-0220'nin (revizyon mu FU mu: TBD), bu paketin payı yalnız Platform azaltıcısının (Bölüm 8.13) kilit gelince gözden geçirilmesi | Dependencies, Follow-up Items | **başlama kapısı** A-18 (MDM sahibi Ali + Beste, 2026-11-06; Bölüm 18.1); kabul ölçütleri bu dilimin paket revizyonunda; o güne kadar ara dönem azaltıcısı DL-1'de; kod izni yok |
| DL-6 | İçe aktarma ve sayaç yüksek su işareti (RCS-001 §5.11), WMS geçici sayacından devam | Runtime Constraints, Follow-up Items | kabul ölçütleri bu dilimin paket revizyonunda; FU-0178-RCS-01 önkoşulu; kod izni yok |
| DL-7 | RCS-001 §3 tür başına izinli kapsam sütunu ve CTR → AGR satırı — **bağımlılık**: kural dosyası değişikliği, bağlı paket yok (TBD, CT); bu paketin payı yalnız katalog testindeki adlı istisnanın kaldırılması | Record Codes, Açık kararlar | sahip Ali (.antigravity) + Treasury PR'ı (A-3); DL-1'i bekletmez |
| DL-8 | WMS türleri (PUT, PCK, OUT, PKG) kataloğa ve Inventory kimlik bilgisi | Entity Fields, Authorization Convention, Follow-up Items | kabul ölçütleri bu dilimin paket revizyonunda; FU-0178-RCS-02 önkoşul; kaynak servis kümesine Diten.InventoryService; kod izni yok |
| DL-9 | Arka plan çağıranları için servis kimliği token'ı (P3) — **bağımlılık**: Auth kodu bağlı paket MOD-0033-FU02'nin (revizyon mu yeni FU mu: TBD); bu paketin payı Platform yürütücüsünün P3 token'ını kabul etmesi | Runtime Constraints, Follow-up Items | kabul ölçütleri bu dilimin paket revizyonunda; Auth sahip rolleri MOD-0033-FU02 künyesindeki + Ali; kod izni yok |
| DL-10 | Platform yönetici ekranları (tür kataloğu, sayaç, defter, grup öneki) | Layout & Shell Contract, Frontend File Contract, Follow-up Items | kabul ölçütleri bu dilimin paket revizyonunda; 2 dil, `_LayoutPlatformAdmin`, SEC-002 kapsam cümlesi; kod izni yok |

Dilim sırası: DL-1 tek başına teslim edilebilir ve ilk commit grubu aktör ölçümüdür (AC-41). DL-5, DL-7 ve DL-9'un ana
işi başka sahiplerin kodudur ve bu paketin bağımlılığıdır (Bölüm 7, r4 F-8). DL-2 ve DL-4 DL-1'i, DL-4 ayrıca DL-3'ü
bekler. DL-6, MOD-0178'in FU-0178-RCS-01 geçişinden önce main'de olmalıdır. DL-7 DL-1'i bekletmez. DL-8, DL-6 ve
FU-0178-RCS-02'yi bekler. Tüketici kapıları: Treasury paketlerinin DL-1 kabulü bu paketin DL-1 kodunun main'de olmasını
bekler (D59/1); MOD-0289 ve MOD-0217 için ayrıca MDM bağdaştırıcısı, TC-4 ve TC-5 (A-11).

## 24. Açık kararlar

| # | Konu | Seçenekler | CT önerisi / durum | Sahip | Son tarih |
|---|---|---|---|---|---|
| A-1 | Şirket öneki biçimi (K-6) | RCS-001 §1 · tek grup öneki · öneksiz | açık; gelmezse §1'e göre donar | K1 (`gmg-10`) | 2026-11-02 |
| A-2 | MOD-0144 / MOD-0217 kapsam çakışması | — | AÇIK KARAR; DL-1'in `AGR` kullanımını bekletmez | Cihan + Treasury | TBD |
| A-3 | RCS-001 §3 `CTR` → `AGR` satırı ve izinli kapsam sütunu | — | açık; DL-7; o güne kadar katalog testinde adlı istisna | Ali (`.antigravity`) + Treasury PR'ı | 2026-11-03 (öneri) |
| A-3c | `AGR` doküman tür koduyla çakışıyor mu | — | **KAPANDI — K-12 (a)**: biçimler farklı, çakışma yok (Bölüm 22) | Beste | 2026-10-30 |
| A-4 | Sınıflandırılmamış üreticilerin envanteri | — | açık | Treasury (gönderecek) | TBD |
| A-5 | Grup önekinin kaynağı | — | **KAPANDI — K-12 (c)**; r3 (inceleme 02 F-7): tek kaynak belge `OrgPrefix`'i, kiracı ayarı gövdesiz hizalama + kilit (Bölüm 4.3, 8.12) | Beste | 2026-10-30 |
| A-6 | Platform kapsamı | — | **KAPANDI — K-11**: sözleşmede kalır, DL-1'de tür izni geçtiyse 422, uygulama DL-4 | Beste | 2026-10-30 |
| A-7 | `PLN` v1 kataloğunda mı | — | **KAPANDI — K-12 (d)**: katalogda, bağlanması DL-4 | Beste | 2026-10-30 |
| A-8 | `ORG` / `POS` izinli kapsamı | LegalEntity · Group · ikisi | **KAPANDI — K-15: LegalEntity** (Beste 2026-11-10, `docs/records/decisions/2026-11/mod0040-fu02-owner-decisions-02.md:8`, commit `6609910c8`); r12'de işlendi (Bölüm 4.4, 8.14) | Beste | 2026-11-10 |
| A-9 | Tür kaydı kod mu koleksiyon mu | — | **KAPANDI — K-12 (b)**: kodda | Beste | 2026-10-30 |
| A-10 | `platform-islem-ici` iz satırına `RecordCodeAuditIntent.*` belirteci; CT-DECISIONS; `AuditTrailStandardTests` sabiti | — | açık; kabul önerisi | CT | DL-1 main'e girmeden önce |
| A-11 | Dondurma ayrıntıları: `idempotencyKey` biçimi; Treasury ve MDM portuna `prefixScope`; Bölüm 4.4 izin adlarının FU27 ile teyidi (`APD` alacak dekontu dahil) | — | açık; taslaktaki gibi | Beste + Treasury CT + MDM sahibi | 2026-11-03 |
| A-12 | Kayıt satırı (Bölüm 0), `module-id-registry.md` "`MOD-0040` is reserved…" notunun düzeltilmesi, domain-config In-Scope satırı | — | açık | CT | DL-1 merge'ünden önce (CT kararı 2026-11-02: ready-for-dev önkoşulu değil, Bölüm 18.1) |
| A-13 | İç uç kimlik deseni | — | **KAPANDI — K-12 (e)**: P2 + azaltıcılar (Bölüm 8.2) | Beste | 2026-10-30 |
| A-14 | Ret denetimi | — | **KAPANDI — K-10** (Bölüm 21.2) | Beste | 2026-10-30 |
| A-15 | AUD-001 §3 satır 3 ile aktör | — | **KAPANDI — K-8r**: aktör kullanıcı, `SourceService` çağıran; çelişki yok | Beste | 2026-10-30 |
| A-16 | Hız sınırı değeri (bölüm: tüketici servis + kiracı) ve eski sır penceresi | değer · gerekçe | açık; CT önerisi 600 istek/dakika/bölüm, kuyruk 0; `PreviousValidUntilUtc` en çok 24 saat | Beste | 2026-11-03 |
| A-17 | 200 yanıt zarfı: v0 `{code, sequence}`'i en üst düzeyde yazıyordu; D59/1 ve Y-8 "yanıt dahil yalnız ekleme" der; öneri 1.0.0'ı `Response<T>` zarfının `data`'sında dondurmak | zarf · en üst düzey | açık; öneri zarf — "v0 yanıt biçimi bağlayıcı değildi" bir **öneridir**, teyit gelmeden karar sayılmaz. **Varsayılan (r7, son bakış 01 F-12):** teyit sözleşme dondurma adımına (2026-11-03) kadar gelmezse 1.0.0 **`Response<T>` zarfıyla** donar (`data = { code, sequence }`) — gerekçe: Platform'un bütün uçları bu zarfı döner, v0 yazılı sözleşme değildi ve `703cbaec7`'de `services/` altında `IRecordCodeAllocator` ya da `record-codes/allocate` geçen tüketici kodu yok (ölçüldü; başka dallar ölçülmedi — Treasury dalında bağdaştırıcı varsa onu bu varsayılan değiştirir, Treasury CT'ye bildirilir); Treasury sonradan en üst düzey biçimi isterse bu bir **büyük sürüm** değişikliğidir (sözleşme §13), dondurma adımı varsayılanın uygulandığını kayda yazar | **Treasury CT** (teyit); varsayılanın uygulanması CT (dondurma) | 2026-11-03 (dondurma) |
| A-18 | RCS-001 §5.4 kısa kod kalıcı kullanım kilidi (MDM tarafı, DL-5) — MDM'de kısa kod bugün değiştirilebiliyor (`UpdateLegalEntityHandler.cs:36-37`) | MDM'de kilit · MDM'de değişiklikte uyarı · yalnız Platform azaltıcısı | açık; ara dönemde Platform azaltıcısı (Bölüm 8.13, 409 `LEGAL_ENTITY_SHORT_CODE_CHANGED`) | MDM sahibi (Ali) + Beste | 2026-11-06 |
| A-19 | MDM lookup-validation v1.1 (`approvalStatus`) yayını ve bu serviste "alan yok → 503" geçişi | — | açık bağımlılık; o güne kadar TC-3 zorunlu (FU-RCS-15) | MOD-0220 sahibi + Beste | TBD |

### 24.1 DL-2 açık soruları (r12) — karar sahibinin

"Bekletir" = cevap gelmeden DL-2 kodu yazılmaz. Önerilen cevap pakete işlendi ("Q-n A" işaretli AC'ler); başka seçenek seçilirse
ilgili bölümler DL-2 kodundan önce yeni revizyonla değişir. Paket hiçbir soruyu kendiliğinden kapatmaz.

| # | Soru | Seçenekler | Öneri ve gerekçe | Sahip | Bekletir |
|---|---|---|---|---|---|
| Q-1 | Bugün elle girilmiş ORG / POS kodları ne olacak (RCS-001 §7) | **A** koru + çakışma savunması · **B** göç (eski kod `LegacyCode`'a, kayda otomatik kod — "ikisi birden" budur) | **A** — RCS-001 §2 "Mevcut kodlar değiştirilmez" bağlayıcıdır (`record-coding-standard.md:42-43`); B §2'ye aykırıdır, yönetim kararı + `.antigravity` değişikliği (Ali) + ayrı revizyon ister. Sayaç çakışması: silinmişler dahil arama + en çok 3 atlama + kabulde kiracı başına sayım; 3'ü aşan durumda DL-6 (Bölüm 8.15) | Beste (B seçilirse + yönetim, Ali) | evet |
| Q-2 | Güncellemede kod değiştirilebilir mi | **A** hiçbir kod değişmez (eski elle kodlar dahil) · **B** eski kodlar elle değişebilir, otomatik kodlar değişmez | **A** — RCS-001 §6 "API de reddeder" (`:156`); eski kodu düzenlenebilir bırakmak §5.9 çakışma riskini açık tutar ve aynı türe iki kural getirir. Bugün güncelleme kodu serbestçe değiştiriyor (`UpdateOrganizationUnitCommandHandler.cs:145`, `UpdatePositionCommandHandler.cs:49`) — A kullanıcı için bir davranış değişikliğidir | Beste | evet |
| Q-3 | Ekran metinleri kaç dilde | **A** 7 dil · **B** 2 dil (görev metni) | **A** — formlar kiracı kabuğunda (`_LayoutTenantShell`, Bölüm 9), kaynak dosyaları bugün 7 dilde (ölçüldü); `localization-standard.md:8-9` 2 dili yalnız Platform yönetici modüllerine verir. B, var olan 5 dil dosyasında anahtar eksik bırakır | Beste | evet |
| Q-4 | Tüzel kişilik onay ön kontrolü (TC-3) ORG / POS için | **A** MDM v1.1'e kadar yalnız aktiflik — adlı istisna · **B** DL-2'yi MDM v1.1 yayınına (A-19) bağla | **A** — Platform `approvalStatus`'u bugün göremez (lookup-validation 1.0.0, Bölüm 7); ORG bugün de yalnız "referenceable" (aktif) ister (`CreateOrganizationUnitCommandHandler.cs:45-49`, MDM `LegalEntity.cs:60`); v1.1'de FU-RCS-15 onay denetimini servis tarafında bütün çağıranlara uygular. B DL-2'yi tarihi belirsiz bir yayına bağlar | Beste (+ MOD-0220 sahibi bilgi) | evet |
| Q-5 | POS'un şirket kaynağı | birimin `LegalEntityId`'si (oluşturma anı) | **teyit** — A-8 karar metni POS'un şirketini adlandırmaz; `Position`'da şirket alanı yok (`Position.cs:7-9`), başka kaynak yok; taşımada kod değişmez (§5.2) | Beste | hayır (teyit) |
| Q-6 | Künye `draft` ve onay sırası (S-49) | **A** r12 incelenir; onay satırından önce künye `ready-for-dev`'e çevrilir (yeni sha256'ya onay) · **B** r12 incelenir ve onaylanır, ardından r13 = yalnız künye `ready-for-dev` + yeni onay (r8 → r9 emsali) | CT kararı — A bir tur kısa, B emsale uygun. r12 `draft`'ta onaylanırsa onay satırı DL-2 kodunu yetkilendirmez (`scripts/pack_approval.py:1473-1474`) | CT + Beste | evet |
| Q-7 | XMC-001: MOD-0288 dosyalarına dokunma onayı (Bölüm 5 "DL-2" XMC satırları) ve MOD-0288-FU03 / FU04 ile sıra | onay · ret · kısmi | MOD-0288 künyesi `owner: platform-team`, FU02–FU04 `organization-governance-owner` — kişi adı yok; onaylayan **TBD** (CT belirler). FU03 / FU04 aynı ekranlara dokunabilir; açık dallarda iş olup olmadığı ölçülmedi | TBD (MOD-0288 sahibi) + CT | evet |
