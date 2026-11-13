---
id: MOD-0040-FU02
name: Record Code Service
domain: platform-shared-services
service: Diten.Platform
shell: none
golden_reference: none
entity_base: BaseEntity
status: ready-for-dev
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
revision: r14 — DL-3 paket revizyonu (İKİSİ DE motoru — öneri, elle kod sahiplenmesi, gerekçeli değişiklik, kullanım kilidi, değişiklik geçmişi; RCS-001 §2, §5.8, §5.9, §5.10, §6); r13 onaylı (sha256 325e0624, approvals.tsv satır 73 sahip + 74 Ali, 2026-11-11) ve DL-2 VERIFIED (docs/records/audits/2026-11/mod-0040-fu02-dl2-fix01-ct-acceptance-2026-11-12.md); yeni AC-62…AC-75, sabotaj S-93…S-113, açık sorular Q-8…Q-16 (Bölüm 24.2), açık karar A-20 (DL-3 defter belirteci); ölçüm türü TTY (Görev Türü, MOD-0024) — Q-8 A önerisiyle yazıldı; DL-1 ve DL-2 metni (AC-1…AC-61, S-1…S-92) ve sözleşme RECORD-CODE-ALLOCATE 1.0.0 değişmedi; künye ready-for-dev, approval_status pending — r14 onay satırı yok, DL-3 kodu r14'ün CT-APR-02 incelemesi ve r14 sha256'sına yazılan CT-APR-01 onay satırıyla açılır; r13 — bağımsız inceleme 04 tur 1 KOŞULLU (0 ENGEL, 4 ORTA, 9 DÜŞÜK; hedef r12 sha256 d29f9c5e, bulgu dosyası docs/records/reviews/MOD-0040-FU02/2026-11-10-d29f9c5e.md, özet 25888259) F-1…F-13 kapatıldı; sahip kararları 03 (Q-1 A, Q-2 A, Q-3 A, Q-4 A, Q-5 teyit, Q-6 A, Q-7 onay — docs/records/decisions/2026-11/mod0040-fu02-owner-decisions-03.md) ve 04 (F-1 dar ayrı arayüz + XMC ek onayı InMemoryTenantOrganizationRepositories.cs, F-2 (b) ikinci okuma, F-4 A-18 = DL-2 canlıya alma kapısı — …-04.md) işlendi → K-16, K-17; Q-6 A gereği künye ready-for-dev, approval_status pending (onay satırı yok, kod izni yok); değişen bölümler künye, üst uyarı, 0 (ön kontrol r13), 0.1 (K-15 notu, K-16, K-17), 2.4, 3, 4.4, 5 (DL-2), 6, 7, 7.1, 8.13, 8.14, 8.15, 8.16 (yeni, sürüm notu), 11, 12, 13, 14, 16 (DL-2 giriş notu, AC-51…AC-61), 17 (test tablosu iki satır; S-78, S-80, S-82, S-84, S-85, S-87 metni; yeni S-88…S-92), 18, 18.1, 19.5 (yeni), 20 (FU-RCS-20), 21.4, 23 (DL-2 satırı), 24 (A-8, A-18), 24.1; DL-1 metni (AC-1…AC-47, S-1…S-73) ve sözleşme RECORD-CODE-ALLOCATE 1.0.0 değişmedi, yeni AC yok; r12 — DL-2 paket revizyonu (ORG / POS süreç içi bağlanma); girdi sahip kararı A-8 = LegalEntity (Beste 2026-11-10, docs/records/decisions/2026-11/mod0040-fu02-owner-decisions-02.md @ 6609910c8) → K-15; DL-1 CT doğrulaması docs/records/audits/2026-11/mod-0040-fu02-dl1-ct-acceptance-2026-11-04.md; DL-1 metni (AC-1…AC-47, S-1…S-73) değişmedi, yalnız AC-9'a DL-2 notu; yeni AC-48…AC-61, S-74…S-87, açık sorular Q-1…Q-7; sözleşme RECORD-CODE-ALLOCATE 1.0.0 (FROZEN) değişmedi; künye draft — kod izni yok, yeni inceleme ve onay gerekir; r11 — r10 incelemeleri KOŞULLU (inceleme 03 tur 8, bulgu dosyası 2026-11-02-42b32c0a.md, özet 96476fa0; son bakış 01 tur 5, 2026-11-02-42b32c0a-son-bakis.md, özet fb4cf83e), aynı orta bulgu (AC-17 yarış ölçümü yürütücüyle kurulamaz); sahip kararı Beste 2026-11-02 "B — r11: yarış testi çıkarılır, ölçüm sıralı yapılır"; r11 = sıralı ölçüm + ilk kilit sınırı (Bölüm 8.6) + son bakış tur 5 F-2, F-3; yeni inceleme ve yeni onay gerekir; kapsam genişlemedi; r10 — r9 onaylı (approvals.tsv satırı 466aacfc9, r9 sha256 5fed433c); r10 = bağımsız son bakış 01 tur 4 düşük bulguları (bulgu dosyası 2026-11-02-5fed433c-son-bakis.md, özet 0e25482b, F-1…F-7), yeni onay gerekir; kapsam genişlemedi, yeni karar açılmadı; r9 — r8 sahip onayı (Beste, 2026-11-02, «onayla») + iki inceleme UYGUN (inceleme 03 tur 6, özet f9c26991; son bakış 01 tur 3, özet ec0346bb); r9 = ready-for-dev künyesi (pack_approval S-49: draft künyeli sürümün onay satırı kodu yetkilendirmez) + iki incelemenin düşük bulgularının kapanışı (tur 6 F-1, son bakış tur 3 F-1…F-7); kapsam genişlemedi, yeni karar açılmadı; r8 — sahip kararı Beste 2026-11-02 "düzelt, bir tur daha": bağımsız son bakış 01 tur 2 KOŞULLU (1 ORTA, 8 DÜŞÜK; hedef r7 pack sha256 fb151036, bulgu dosyası 2026-11-02-fb151036-son-bakis.md, özet 8a63c4e5) F-1…F-9 ve inceleme 03 tur 5 UYGUN (2 DÜŞÜK; 2026-11-02-fb151036.md, özet e6dd4cd8) F-1…F-2 işlendi; r7 — bağımsız son bakış 01 KOŞULLU (0 ENGEL, 3 ORTA, 9 DÜŞÜK; hedef r6 pack sha256 4b4c3db1, bulgu dosyası 2026-11-02-4b4c3db1-son-bakis.md, özet a52313ba) bulguları F-1…F-12 ve sahip kararı K-13 (F-3) ile inceleme 03 tur 4 UYGUN (2 DÜŞÜK; bulgu dosyası 2026-11-02-4b4c3db1.md, özet f4049860) bulguları F-1…F-2 işlendi; r6 — bağımsız inceleme 03 tur 3 KOŞULLU (0 ENGEL, 1 ORTA, 0 DÜŞÜK; hedef r5 pack sha256 bcd62d6c, bulgu dosyası 2026-11-02-bcd62d6c.md, özet ace6292b) bulgusu F-1 sahip kararı (a) ile işlendi (hizalama retleri denetime yazılır); r5 — bağımsız inceleme 03 tur 2 KOŞULLU (0 ENGEL, 1 ORTA, 4 DÜŞÜK; hedef r4 pack sha256 127ec6f2, bulgu dosyası 2026-11-02-127ec6f2.md, özet e2dbb0d8) bulguları F-1…F-5 ve CT kararı (F-1 yolu) işlendi; r4 — bağımsız inceleme 03 tur 1 KOŞULLU (0 ENGEL, 2 ORTA, 8 DÜŞÜK; hedef r3 pack sha256 010fe1c2, bulgu dosyası 2026-11-02-010fe1c2.md, özet 47c28da0) bulguları F-1…F-10 işlendi; r3 — bağımsız inceleme 02 KOŞULLU (0 ENGEL, 2 ORTA, 9 DÜŞÜK; hedef e49a67359, pack blob b18184f0, bulgu dosyası özeti 46aeb4a5) bulguları F-1…F-11 işlendi; r2 — inceleme 01 UYGUN DEĞİL (bulgu dosyası özeti 277fd253) bulguları ve sahip kararları K-8r, K-9…K-12 işlendi
code_start: yok — CT-APR-02 bağımsız inceleme kaydı ve CT-APR-01 onay satırı olmadan kod izni yoktur; DL-1 kodu r11 onayıyla yazıldı (CT kabulü 2026-11-04); DL-2 kodu r13 onayıyla yazıldı (approvals.tsv satır 73 / 74, sha256 325e0624; CT kabulü 2026-11-12, VERIFIED); DL-3 kodu r14'ün kendi CT-APR-02 incelemesini ve r14 sha256'sına yazılan CT-APR-01 onay satırını bekler (r14 onayıyla kod izni; Bölüm 18.1 DL-3 kapıları); DL-2'nin canlıya alınması ayrıca A-18'i bekler (Bölüm 18.1, K-17)
---

# MOD-0040-FU02 — Record Code Service (ortak kayıt kodu servisi, RCS-001)

> **Künye `ready-for-dev` (r14) ama bu sürüm henüz onaysızdır ve DL-3 için hiçbir kod izni vermez.** `status: ready-for-dev`,
> `approval_status: pending`. **r13 onaylıdır** (sha256 `325e0624…4176`; `execution/approvals/approvals.tsv` satır 73 sahip
> «onayla» 2026-11-10, satır 74 Ali «onayla» 2026-11-11) ve DL-2 kodu o onayla yazılıp CT tarafından doğrulandı
> (`docs/records/audits/2026-11/mod-0040-fu02-dl2-ct-acceptance-2026-11-12.md` KOŞULLU →
> `docs/records/audits/2026-11/mod-0040-fu02-dl2-fix01-ct-acceptance-2026-11-12.md` VERIFIED). r14 DL-1 ve DL-2 için yeni izin
> gerektirmez; **DL-3 kodu** r14'ün kendi CT-APR-02 incelemesini ve **r14'ün** sha256'sına yazılan CT-APR-01 onay satırını bekler
> (S-49; Bölüm 18.1 DL-3 kapıları). DL-1 kodu r11'in onayıyla yazıldı ve CT tarafından doğrulandı (r11 sha256 `62f352de…cc2d`).
> **DL-2 canlıya alma kapısı:** A-18 kararı (Bölüm 18.1, K-17).
> **Onaylanan sürüm**, `execution/approvals/approvals.tsv`'de bu paket yolu için yazılmış **son satırın** sha256'sına eşit olan
> sürümdür; bu dosyanın sha256'sı o satırla tutmuyorsa bu sürüm onaysızdır (r11, son bakış 01 tur 5 F-3). Bu paket, aynı sürümünün
> **CT-APR-02 bağımsız incelemesi** (`execution/approvals/reviews.tsv` + bulgu dosyası) ve **CT-APR-01 onay satırı**
> (`execution/approvals/approvals.tsv`, paketin bu sürümünün SHA-256'sına bağlı) main'e girmeden ve künye
> `ready-for-dev` olmadan hiçbir kod, yapılandırma, gizli bilgi ya da veri değişikliğini yetkilendirmez. Bir WP paketin
> tamamını alamaz; dilim tablosundaki (Bölüm 23) bir ya da birkaç dilimi alır. DL-2 yazılı ve doğrulanmıştır (Q-1…Q-7 karar,
> Bölüm 24.1). **DL-3 r14'te yazıldı** (Bölüm 2.5, 4.5, 8.17, 16 "DL-3", 21.5); açık soruları Q-8…Q-16 (Bölüm 24.2) — maddeler
> önerilen cevaplarla yazıldı ve "Q-n A önerisi" diye işaretlidir; sahip farklı seçerse yeni revizyon gerekir. DL-4 ve sonrası
> yazılmadı — her biri kendi paket revizyonu ve onayıyla açılır.
>
> **Revizyon r14 (2026-11-13) — DL-3 paket revizyonu (İKİSİ DE motoru); künye `ready-for-dev`, onay yok.** Girdi: paketin
> Bölüm 23 DL-3 satırı ("İKİSİ DE motoru — öneri, gerekçeli değişiklik, AUD-001 eski/yeni kod + gerekçe, RCS-001 §5.9"; r13'te
> "kabul ölçütleri bu dilimin paket revizyonunda; kod izni yok") ve CT talimatı (2026-11-13). r14 = DL-3'ün kod izni
> verilebilir hâle getirilmesi: kapsam (Bölüm 2.5), sahiplik defteri `record_code_claims` (Bölüm 4.5), motorun davranışı ve
> değerlendirme sırası (Bölüm 8.17), dosya listesi (Bölüm 5 "DL-3"), kabul ölçütleri **AC-62…AC-75**, sabotaj **S-93…S-113**,
> denetim yükü Tablo E–H (Bölüm 21.5), açık sorular **Q-8…Q-16** (Bölüm 24.2), açık karar **A-20** (yeni defter belirteçleri,
> DL-3 merge kapısı). **Ölçüm türü:** RCS-001 §3'ün İKİSİ DE türlerinden yalnız **TTY** (Görev Türü, MOD-0024, Diten.Platform)
> kataloğa girer (Q-8 A önerisi) — MOD-0024 dosyalarına dokunulmaz, TTY ekranının bağlanması DL-4'tür. DL-1 ve DL-2 metni
> (AC-1…AC-61, S-1…S-92) **değişmedi**; DL-1'in iki katalog testinin TTY satırları AC-62 ile değişir (AC-9 / AC-48 emsali).
> Sözleşme RECORD-CODE-ALLOCATE 1.0.0 (FROZEN) **değişmez** (§13: "Yeni tür eklemek sözleşme değişikliği değildir"); iç uçtan
> TTY 403'tür. Ölçümler bu çalışma ağacında (`8931275d2`, detached; DL-1 ve DL-2 kodu var) yapıldı; DL-1 kodunun main'de, DL-2
> kodunun main'de olmadığı `origin/main` üzerinde ölçüldü. **Değişen bölümler:** künye; üst uyarı; 0 (ön kontrol r14); 2.5 (yeni);
> 3; 4.4 (TTY satırı ve r14 notu); 4.5 (yeni); 5 ("DL-3"); 6; 7; 8.17 (yeni); 9; 10; 11; 12; 13; 14; 15; 16 (DL-3, AC-62…AC-75);
> 17; 18; 18.1; 19.6 (yeni); 20 (FU-RCS-02, FU-RCS-04, FU-RCS-20 eki, FU-RCS-21, FU-RCS-22); 21.5 (yeni); 22; 23 (DL-3 satırı, dilim sırası);
> 24 (A-20); 24.2 (yeni).
>
> **Revizyon r13 (2026-11-10) — inceleme 04 tur 1 bulgularının ve sahip kararları 03 / 04'ün işlenmesi; künye `ready-for-dev`,
> onay yok.** Girdiler: (1) **bağımsız inceleme 04 tur 1** (`docs/records/reviews/MOD-0040-FU02/2026-11-10-d29f9c5e.md`, özet
> `25888259…`; hedef r12 sha256 `d29f9c5e…ba6d`; **KOŞULLU**, 0 ENGEL, 4 ORTA, 9 DÜŞÜK) F-1…F-13; (2) **sahip kararları 03**
> (`docs/records/decisions/2026-11/mod0040-fu02-owner-decisions-03.md`) Q-1 A, Q-2 A, Q-3 A (7 dil), Q-4 A, Q-5 teyit, Q-6 A,
> Q-7 onay → **K-16**; (3) **sahip kararları 04** (`…/mod0040-fu02-owner-decisions-04.md`) F-1 dar ayrı arayüz + XMC-001 ek onayı
> (yalnız `InMemoryTenantOrganizationRepositories.cs`), F-2 (b) ikinci `GetByIdAsync` okuması (`PositionReferenceGuard.cs`
> değişmez), F-4 A-18 = DL-2 **canlıya alma kapısı** → **K-17**. Kapanışlar Bölüm 19.5'te (`F-n@25888259`). Kod iddiaları
> `f73964c6e`'de (dal `feature/sce/mod-0173-inventory-ledger-valuation`) yeniden ölçüldü; Platform, Platform.Common, Web ve
> sözleşme dosyaları `36465315c` (r12 ölçümü, inceleme kaynağı) ile aynıdır (`git diff --stat 36465315c f73964c6e -- <yollar>` boş,
> 2026-11-10). Bu paketin yazıldığı dalda (`chore/ct/mod0178-dl1-accept-01`, `91d72310e`) DL-1 kodu **yoktur** (ölçüldü); satır
> numaraları kod dalınındır. Bir ölçüm inceleme bulgusunu düzeltti: çerçevenin 403'ü zarfta **`reason_code = PERM_DENIED`**
> taşır (F-3; Bölüm 19.5). DL-1 metni (AC-1…AC-47, S-1…S-73) ve sözleşme 1.0.0 değişmedi; yeni AC yok, yeni sabotaj S-88…S-92.
> **Değişen bölümler:** künye; üst uyarı; 0 (ön kontrol r13 için yeniden koşuldu, çıkış 0); 0.1 (K-15 notu, K-16, K-17); 2.4; 3; 4.4; 5 ("DL-2"); 6; 7; 7.1; 8.13; 8.14; 8.15;
> 8.16 (yeni, sürüm notu); 11; 12; 13; 14; 16 (DL-2 giriş notu, AC-51…AC-61); 17; 18; 18.1; 19.5 (yeni); 20 (FU-RCS-20); 21.4; 23 (DL-2 satırı);
> 24 (A-8, A-18); 24.1.
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
| Ön kontrol | `python3 -P -E -X utf8 .antigravity/scripts/verify_module_id.py . --check-id MOD-0040-FU02 --name "Record Code Service" --parent MOD-0040` → `OK  MOD-0040-FU02: proven against Blueprint/registry.` · **çıkış 0** (2026-10-30; **r10 için yeniden koşuldu** 2026-11-02, `466aacfc9`, Python 3.12.10 → aynı çıktı, çıkış 0 — son bakış 01 tur 4 F-7; **r11 için yeniden koşuldu** 2026-11-02, `4288ae40e`, Python 3.12.10 → aynı çıktı, çıkış 0; **r12 için yeniden koşuldu** 2026-11-10, `a9fd192ba`, Python 3.12.10 → aynı çıktı, çıkış 0 — DL-2 yeni kimlik açmaz; **r13 için yeniden koşuldu** 2026-11-10, `91d72310e`, Python 3.12.10 → aynı çıktı, çıkış 0; **r14 için yeniden koşuldu** 2026-11-13, `8931275d2` çalışma kopyası, Python 3.12.10 → aynı çıktı, çıkış 0 — DL-3 yeni kimlik açmaz) |
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
| **K-15** | **A-8 kapandı — `ORG` / `POS` izinli kapsamı yalnız LegalEntity (Beste 2026-11-10; `docs/records/decisions/2026-11/mod0040-fu02-owner-decisions-02.md:8`, commit `6609910c8`).** ORG ve POS kodları şirket öneki alır: `{ŞİRKET}-ORG-000001`, `{ŞİRKET}-POS-000001`. Karar gerekçesi: `OrganizationUnit.LegalEntityId` zorunlu (`services/Diten.Platform/src/Diten.Platform.Domain/Entities/Organization/OrganizationUnit.cs:9`). POS'un şirket kaynağı (birim üzerinden) karar metninde adlandırılmaz — paket veri modelinden türetti; **r13: Q-5 ile teyit edildi** (Beste 2026-11-10, `…/mod0040-fu02-owner-decisions-03.md`: "birimin oluşturma anındaki `LegalEntityId`'si") | Bölüm 2.4, 4.4, 8.14, 22, 24 |
| **K-16** | **DL-2 soruları Q-1…Q-7 kapandı (Beste 2026-11-10; `docs/records/decisions/2026-11/mod0040-fu02-owner-decisions-03.md`).** Q-1 **A** — elle girilmiş kodlar korunur, çakışma savunması paketteki gibi (silinmişler dahil arama, en çok 3 deneme, sonra 409); Q-2 **A** — güncellemede hiçbir kod değişmez, eski elle kodlar dahil (davranış değişikliği, sürüm notuna yazılır — Bölüm 8.16); Q-3 **A** — 7 dil (CT talimatındaki "2 dil" CT'nin hatasıydı); Q-4 **A** — MDM lookup-validation v1.1'e kadar yalnız aktiflik, adlı istisna; Q-5 **teyit** — POS'un şirketi birimin oluşturma anındaki `LegalEntityId`'si; Q-6 **A** — bağımsız inceleme sonrası künye `ready-for-dev`, onay o sürümün sha256'sına (CT kararı, sahip onayladı); Q-7 **onay** — XMC-001 yalnız r12 Bölüm 5 "DL-2" XMC satırlarındaki MOD-0288 dosyaları, onaylayan merkez sahibi Beste | Bölüm 5, 7, 8.15, 8.16, 11, 16, 18.1, 24.1 |
| **K-17** | **İnceleme 04 tur 1 kararları (Beste 2026-11-10; `docs/records/decisions/2026-11/mod0040-fu02-owner-decisions-04.md`).** F-1: silinmişler dahil kod araması ayrı **dar** arayüzlere taşınır (`IOrganizationReportingGraphRepository` emsali); yalnız iki Mongo deposu ve `InMemoryTenantOrganizationRepositories.cs` uygular; **XMC-001 ek onayı** yalnız `services/Diten.Platform/tests/Diten.Platform.Application.Tests/TenantOrganization/InMemoryTenantOrganizationRepositories.cs` (MOD-0288), yalnız bu arayüzlerin uygulanması. F-2: **(b)** — `CreatePositionCommandHandler` birimi ikinci kez `GetByIdAsync` ile okur; `PositionReferenceGuard.cs` değişmez; iki okuma arasında şirket değişimi §5.2 gereği kabul. F-4: DL-2 kodu yazılabilir; **A-18 (sözleşme N-12) DL-2'nin canlıya alma kapısıdır**; iki yön ve "başka şirkette aynı önek" durumu pakete yazılır; Treasury CT ve MDM sahibine bildirim + kurtarma yolu | Bölüm 3, 5, 6, 8.14, 18.1, 24 |
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
- **MOD-0288 bağlanması (XMC-001; Bölüm 5 "DL-2"; Q-7 onayı K-16, ek onay K-17 F-1):** Organizasyon Birimi ve Pozisyon
  oluşturma işleyicileri kodu porttan alır; oluşturma isteğinde kod kabul edilmez; güncelleme kodu değiştirmez; formlarda kod
  salt okunur (Bölüm 8.15, 11, 12). Silinmişler dahil kod araması **ayrı dar arayüzlerdedir** (`IOrganizationUnitCodeHistoryRepository`,
  `IPositionCodeHistoryRepository`; r13, K-17 F-1) — mevcut `IOrganizationUnitRepository` / `IPositionRepository` değişmez.
- **Mevcut (elle girilmiş) kodlar:** RCS-001 §2 / §7 — korunur (**Q-1 kararı A**, K-16); sayaç çakışma savunması (Bölüm 8.15).
- **DL-2 dışında:** içe aktarma / sayaç yüksek su işareti (DL-6); İKİSİ DE akışı (DL-3); toplu kod göçü (Q-1 B seçilirse ayrı
  revizyon — Q-1 A seçildi, açılmaz); geliştirme tohumu `PositionSeed` ve kullanılmayan `_CreateEditOffcanvas.cshtml` parçaları
  (FU-RCS-18); MDM v1.1 onay denetimi (A-19; Q-4 kararı A); merkezi oluşturma denetiminin zenginleştirilmesi (FU-RCS-17); MDM kısa
  kod kalıcı kilidi (A-18 — DL-2'nin **canlıya alma** kapısı, kod kapısı değil; Bölüm 18.1).

### 2.5 In scope (DL-3 — r14; kod izni r14 onayıyla, Bölüm 18.1 DL-3 kapıları)

RCS-001'in İKİSİ DE sınıfı: "Sistem önerir; … Evet [kullanıcı değiştirebilir], ama gerekçe ZORUNLU ve gerekçe denetim kaydına
yazılır (AUD-001: eski kod, yeni kod, gerekçe, kişi, zaman)" (`.antigravity/rules/record-coding-standard.md:39`). DL-3 bu sınıfın
**ortak motorudur** (§5.12 "önek ayarı, sayaç, öneri, gerekçe kaydı", `record-coding-standard.md:151-153@8931275d2`); bir tüketici modülün
ekranını ya da komutunu bağlamaz.

- **Öneri (önizleme):** türün bir sonraki sistem kodunu **ayırmadan** gösterir (`IsReserved = false`); sayaç ilerlemez, sayaç
  belgesi yaratılmaz (Bölüm 8.17).
- **Sahiplenme (kayıt oluşturulurken):** kullanıcı kod yazmadıysa sistem kodu DL-1 ayırmasıyla verilir (`AllocateRecordCodeCommand`,
  kural kopyası yok); yazdıysa elle kod RCS-001 §5.9 / §5.10 kurallarıyla ve **gerekçeyle** kabul edilir (Q-9 A önerisi). Her iki
  durumda kod, kaydın kimliğine (`RecordId`) bağlı bir **sahiplik satırı** olarak `record_code_claims`'e yazılır (Bölüm 4.5).
- **Gerekçeli değişiklik:** aktif kod yalnız gerekçeyle değişir; eski kod emekliye ayrılır ve **hiçbir kayda bir daha verilmez**
  (§5.8 "asla yeniden kullanılmaz", `record-coding-standard.md:132@8931275d2`); eski kod, yeni kod, gerekçe, kişi, zaman denetim kaydına
  iş yazımıyla **aynı işlemde** yazılır (DL-1 deseni, Bölüm 21.5 Tablo F).
- **Kullanım kilidi:** tüketicinin "kod artık kullanılıyor" bildirimiyle sahiplik kilitlenir; kilitli kod değişmez (409). Kilit
  geri alınmaz. Gerekçe: MOD-0024 Görev Türü kodu bugün tam da bu nedenle değişmez ("Changing a code after tasks have been opened
  with it rewrites the identity of those tasks", `services/Diten.Platform/src/Diten.Platform.Domain/Entities/Tasks/TaskSupportingEntities.cs:583-589`);
  RCS-001 §5.4 aynı kilidi tüzel kişilik kısa kodu için söyler (`record-coding-standard.md:110-113@8931275d2`). Tüzel kişilik kısa kodunun
  kilidi bu motorda değil MDM'dedir (sahip kararları 04 ek, 2026-11-13, `docs/records/decisions/2026-11/mod0040-fu02-owner-decisions-04.md:17`);
  DL-5 o kilidin motoru kullanıp kullanmayacağını yazar.
- **Değişiklik geçmişi:** bir kaydın bütün sahiplik satırları (aktif, emekli, kilit zamanı, gerekçe, kişi) sorgulanır.
- **Benzersizlik:** (kiracı, tür, kod) başına — emekli kodlar dahil (Q-13 A önerisi; RCS-001 §6 "arşivli kayıtların kodu da dolu
  sayılır", `record-coding-standard.md:166@8931275d2`).
- **Yüzey:** yalnız süreç içi port `IInProcessRecordCodeAssignments` (Bölüm 8.17). **HTTP ucu, Gateway rotası, ekran yok** (Q-15 A
  önerisi). Komutlar DL-1 komutunun biçimini taşır (`TenantId`, `OnBehalfOfUserId`, `CallerService`, `AllowedRecordTypes`,
  `HasRequiredPermission`) — bir HTTP yürütücüsü ileride aynı komutları gönderebilir.
- **Ölçüm türü (Q-8 A önerisi):** `TTY` (Görev Türü, MOD-0024) kataloğa İKİSİ DE sınıfıyla, yalnız `Group` kapsamıyla (RCS-001 §5.3:
  "Kiracı genelinde ortak kayıt … ör. Görev Türü: öneki grubun öneki olur", `record-coding-standard.md:105-106@8931275d2`), izinli çağıran
  `Diten.Platform` (yalnız süreç içi) ve gerekli izin `platform.tasks.task-types.manage` (MOD-0024'ün bugünkü anahtarı,
  `services/Diten.Platform/src/Diten.Platform.Application/Features/Tasks/TaskModels.cs:32`) ile girer. Böylece motorun her kabul
  ölçütü üretim kataloğundaki bir türle ölçülür; MOD-0024'ün hiçbir dosyası değişmez.
- **Başlama koşulu DL-1** (main'de; ölçüldü — `RecordCodeAuditIntent.cs` `origin/main`'de var). DL-3 kodu DL-2'nin hiçbir üyesine
  **bağlanmaz** (DL-2 kodu `origin/main`'de yok — `InProcessRecordCodeAllocator.cs` ölçüldü); süreç içi savunma ve izin yardımcısı
  DL-3'te ayrı bir dosyadadır (Bölüm 8.17), DL-2 portunun onu kullanacak biçimde sadeleşmesi FU-RCS-21.

**DL-3 dışında:** tüketici bağlanması — TTY ekranı ve komutları, PPM (`PFL` / `PRG` / `PRJ` / `INI` / `INV`, Diten.PpmService,
MOD-0117), PLN (MOD-0361, Platform kapsamı) → DL-4; Tüzel Kişilik kısa kodu önerisi ve §5.4 kilidi (MOD-0220, MDM) → DL-5; tüketicinin
**mevcut (eski) kodlarının** sahiplik defterine alınması ve sistem kodunun eski kodla çakışma atlaması (DL-2 Bölüm 8.15 emsali) →
bağlayan dilim (DL-4) / içe aktarma (DL-6) — FU-RCS-22; motorun HTTP yüzeyi (süreç dışı tüketiciler PPM ve MDM için) ve sözleşmede
yeri → onları bağlayan dilim; `LegalEntity` kapsamlı İKİSİ DE türü DL-3 kataloğunda olmadığı için önizlemenin LegalEntity kolu
(MDM okuması) → o türü açan dilim; Platform kapsamı → DL-4 (K-11); ekranlar ve yerelleştirilmiş metin → tüketici dilimi (TTY kiracı
kabuğundadır → 7 dil, `.antigravity/rules/localization-standard.md:8-9`); yönetici ekranları (2 dil) → DL-10.

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
| Dar depo arayüzü (**DL-2**, MOD-0288 deposu, XMC; r13 K-17 F-1) | `IOrganizationUnitCodeHistoryRepository.ExistsByCodeIncludingDeletedAsync(string code, CancellationToken ct = default)`, `IPositionCodeHistoryRepository.ExistsByCodeIncludingDeletedAsync(…)` — her biri tek yöntemli **ayrı** arayüz, sırasıyla `IOrganizationUnitRepository.cs` ve `IPositionRepository.cs` dosyasında (`IOrganizationReportingGraphRepository` emsali, `IOrganizationUnitRepository.cs:15-24`); `IOrganizationUnitRepository` / `IPositionRepository` **değişmez** | silinmiş satırlar dahil kod araması (RCS-001 §5.8; Bölüm 8.15). Gerekçe: mevcut arayüzlere gövdesiz yöntem eklemek Bölüm 5'te olmayan sekiz test sahtesini derlemeden düşürürdü (inceleme 04 F-1; ölçüldü Bölüm 5) |
| Entity (**DL-3**) | `RecordCodeClaim` | `TenantScopedEntity`; İKİSİ DE kod sahiplik defteri `record_code_claims`; Bölüm 4.5 |
| Enum (**DL-3**) | `RecordCodeClaimOrigin` (`System = 1`, `Manual = 2`) · `RecordCodeClaimStatus` (`Active = 1`, `Retired = 2`) | JSON'da ad |
| Kural (**DL-3**, Domain) | `RecordCodeManualCode` | elle kodun normalleştirilmesi ve biçimi (§5.10), otomatik desen yasağı (§5.9), gerekçe sınırları; Bölüm 8.17 |
| Değer (**DL-3**, eklemeli) | `RecordCodeFormat.IsValidPlatformCode` | `^[A-Z]{3}-[0-9]{6,}\z` (Platform kapsamı, RCS-001 §5.3 `PLN-000001`); desen yasağının ikinci kalıbı; DL-4 PLN biçimlemesi aynı üyeyi kullanır |
| Repository (**DL-3**) | `IRecordCodeClaimRepository` | `InsertAsync(session, tenantId, claim)` (E11000 → `RecordCodeClaimConflictException`), `RetireIfActiveAsync(session, …)`, `LockIfUnlockedAsync(session, …)`, `FindActiveAsync(tenantId, recordType, recordId)` (oturumlu ve oturumsuz), `CodeExistsAsync(tenantId, recordType, code)` (emekliler dahil), `ListHistoryAsync(tenantId, recordType, recordId)` — silme / değiştirme / kilit açma yok |
| Dar okuma arayüzü (**DL-3**) | `IRecordCodeCounterReader.PeekLastNumberAsync(tenantId, prefix, recordType)` | önizleme için; ayrı arayüz — `IRecordCodeCounterRepository` (yalnız `NextAsync`) değişmez, DL-1'in AC-20 testi değişmeden yeşil (K-17 F-1 emsali) |
| İstisna (**DL-3**, Domain) | `RecordCodeClaimConflictException` (`IndexName`) | `RecordCodeExceptions.cs`'e eklenir; `Exception`'dan türer — `MongoException` değildir: işlem yürütücüsü geçici olmayan `MongoException`'ı `PlatformTransactionUnavailableException`'a çevirir (Bölüm 7), bu istisnayı olduğu gibi yeniden atar; işleyici yakalar (409) |
| Komut (**DL-3**) | `ClaimRecordCodeCommand`, `ChangeRecordCodeCommand`, `LockRecordCodeCommand` | yazma komutları; `IAuditableCommand` / `ITransactionOwnedAuditCommand` **değil** — denetim işleyicide, iş işlemiyle aynı işlemde (DL-1 `AllocateRecordCodeCommand` deseni; Bölüm 21.5) |
| Sorgu (**DL-3**) | `PreviewRecordCodeQuery`, `GetRecordCodeHistoryQuery` | okuma; denetlenmez (AUD-001 §7) |
| DTO (**DL-3**) | `RecordCodePreviewResult(SuggestedCode, IsReserved)`, `RecordCodeClaimResult(Code, Origin, Replayed)`, `RecordCodeChangeResult(PreviousCode, Code)`, `RecordCodeLockResult(Code, LockedNow, LockedAtUtc)`, `RecordCodeHistoryEntry(Code, Origin, Status, Reason, ClaimedAtUtc, ClaimedBy, RetiredAtUtc, RetiredBy, ReplacedByCode, LockedAtUtc, LockedBy)` | `RecordCodesModels.cs` |
| Port (Application, **DL-3**) | `IInProcessRecordCodeAssignments` | `PreviewAsync`, `ClaimAsync`, `ChangeAsync`, `LockAsync`, `GetHistoryAsync`; uygulama `InProcessRecordCodeAssignments`; Bölüm 8.17 |
| Yardımcı (**DL-3**) | `RecordCodeInProcessGuard` | süreç içi kiracı / aktör savunması ve izin hükmü (boş anahtar kapalı) — DL-2 portunun kuralıyla aynı; DL-2 dosyasına bağlanmaz (FU-RCS-21) |
| Neden kodları (**DL-3**) | `RECORD_CODE_MANUAL_FORMAT_INVALID` (400), `RECORD_CODE_PATTERN_RESERVED` (400), `RECORD_CODE_REASON_REQUIRED` (400), `RECORD_CODE_UNCHANGED` (400), `RECORD_CODE_NOT_ASSIGNED` (404), `RECORD_CODE_TAKEN` (409), `RECORD_CODE_ALREADY_ASSIGNED` (409), `RECORD_CODE_CONFLICT` (409), `RECORD_CODE_LOCKED` (409); DL-2'nin `RECORD_CODE_IMMUTABLE` (409) OTOMATİK türlerde yeniden kullanılır | `RecordCodeReasonCodes.cs`; RECORD-CODE-ALLOCATE sözleşmesine **girmez** (iç uç bunları üretmez) |
| Denetim çağrısı (**DL-3**) | `RecordCodeAuditIntent.EnqueueClaimedAsync` / `.EnqueueChangedAsync` / `.EnqueueLockedAsync` (işlem içi) / `.EnqueueAssignmentRefusalAsync` (ret yazıcısıyla, ayrı kısa işlem) | yeni belirteçler; CT defter kararı **A-20** (Bölüm 21.5) |

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
| `TTY` (**DL-3**, r14; Q-8 A önerisi) | Görev Türü | İKİSİ DE (§3) | MOD-0024 | **yalnız Group** (RCS-001 §5.3) | **`Diten.Platform` — yalnız süreç içi port** (DL-3 motoru), HTTP bağlaması yok | `platform.tasks.task-types.manage` (`TaskPermissions.TaskTypesManage`) |

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

**Sözleşme §6 sütunu ile katalog (r13, inceleme 04 F-13).** Sözleşme 1.0.0 §6 `ORG`, `POS` satırı izinli kapsamı "1.0.0'da boş
küme" diye yazar (`docs/analysis/contracts/record-code-allocate.md:170`, ölçüldü). DL-2'den sonra katalogdaki kapsam `LegalEntity`'dir.
Sözleşme **değiştirilmez** (FROZEN; HTTP davranışı aynı: iç uçta ORG / POS 403); §6'nın kapsam ve çağıran sütunları bu tür için
yalnız **HTTP görünümünü** anlatır — süreç içi yol sözleşmenin konusu değildir (Bölüm 8.14). Sözleşmenin bir sonraki eklemeli
sürümünde (yeni küçük sürüm; her değişiklik sözleşme §13'e göre) bu satır "HTTP: izinli çağıran yok → 403; süreç içi (Platform):
LegalEntity" diye güncellenir — FU-RCS-20 (Bölüm 20). Kendi başına sürüm açtırmaz.

**r14 (DL-3, Q-8 A önerisi) — `TTY`.** Yukarıdaki "Katalogda olmayan her değer" listesindeki `TTY` DL-3 kodu girince katalogdadır;
listenin geri kalanı (`PFL`, `PRG`, `PRJ`, `INI`, `INV` dahil) aynen geçerlidir. Katalog satırı `RecordCodeTypeCatalog.All`'a
eklenir (bugün `PLN` son satır, `services/Diten.Platform/src/Diten.Platform.Domain/Features/RecordCodes/RecordCodeTypeCatalog.cs:41`).
HTTP kimlik bilgisi eşlemesi (`TrustedRecordCodeCredentialAuthenticator.Consumers`) **değişmez**: iç uçtan `TTY` adım 7'de 403
`RECORD_CODE_FORBIDDEN`'dır (DL-1'de 422 `RECORD_TYPE_UNKNOWN` idi). Sözleşme 1.0.0 değişmez — §13 "Yeni tür eklemek sözleşme
değişikliği değildir; katalog RCS-001 §3'e bağlıdır" (`docs/analysis/contracts/record-code-allocate.md:382`); §4'teki
"`RECORD_TYPE_UNKNOWN` — tür katalogda yok (KALSIN, İKİSİ DE, …)" tanımı (`record-code-allocate.md:123`) bir sonraki eklemeli
sürümde "bağlanmamış İKİSİ DE" diye daraltılır (FU-RCS-20'ye eklendi). Katalog `Class` alanı her satırda RCS-001 §3'teki sınıfla
aynıdır (`TTY` → `İKİSİ DE`); DL-1'in "PLN dışındaki her satır OTOMATİK" testi bu yüzden AC-62 ile değişir.

### 4.5 RecordCodeClaim — `record_code_claims` (İKİSİ DE sahiplik defteri; DL-3, r14)

Bir İKİSİ DE kaydının kodu bu defterde, kaydın kimliğine bağlı **sahiplik satırı** olarak durur. Tüketici kendi kaydında kodun
**kopyasını** tutar (DL-4); kodun geçmişinin ve benzersizliğinin kaynağı bu defterdir. Kiracı yüklemi kuralı Bölüm 4 başındakiyle
**aynıdır** (açık `tenantId`, tam eşitlik, `Guid.Empty` / platform bağlamı / bağlam dışı kiracı → `RecordCodeTenantPredicateViolationException`;
aynı `RecordCodeTenantPredicate.Ensure` — `services/Diten.Platform/src/Diten.Platform.Infrastructure/Persistence/Repositories/RecordCodeRepositories.cs:20`).

| Alan | Tip | Zorunlu | Kural |
|---|---|---|---|
| `Id` | Guid | evet | sahiplik kimliği; her satırda yeni Guid; denetim kutusu anahtarı bundan türer |
| `TenantId` | Guid | evet | token kiracısı |
| `RecordType` | string | evet | katalogda, sınıfı `İKİSİ DE` (DL-3'te yalnız `TTY`) |
| `RecordId` | Guid | evet | tüketici kaydının kimliği; `Guid.Empty` yasak (400 `RECORD_CODE_REQUEST_INVALID`) |
| `Code` | string | evet | `System`: DL-1 kalıbı `^[A-Z]{2,5}-[A-Z]{3}-[0-9]{6,}$`; `Manual`: `RecordCodeManualCode` kuralları (Bölüm 8.17) — ikisi hiçbir zaman örtüşmez (§5.9 yasağı) |
| `Origin` | `RecordCodeClaimOrigin` | evet | `System` (sistem kodu, DL-1 ayırması) · `Manual` (kullanıcının yazdığı) |
| `Status` | `RecordCodeClaimStatus` | evet | `Active` → `Retired` tek yön; geri dönüş yok |
| `Reason` | string? | `Manual`'da evet | kırpılmış gerekçe; `System`'de `null` |
| `ClaimedAtUtc` / `ClaimedBy` | DateTimeOffset / Guid | evet | UTC; token `sub` |
| `RetiredAtUtc` / `RetiredBy` / `ReplacedByCode` | — | emeklilikte evet | bir kez yazılır (yalnız `Active` → `Retired` geçişinde) |
| `LockedAtUtc` / `LockedBy` | — | hayır | kullanım kilidi; bir kez yazılır (`$ifNull`), silinmez |
| `CorrelationId` | string | evet | isteğin korelasyonu |

Index'ler (kiracı önce): `ux_record_code_claims_tenant_type_code` `(TenantId, RecordType, Code)` **unique, bütün durumlar** —
emekli kod da doludur (§5.8, §6); `ux_record_code_claims_tenant_type_record_active` `(TenantId, RecordType, RecordId)` unique,
**kısmi** `Status == Active` — bir kaydın tek aktif kodu; `ix_record_code_claims_tenant_type_record_time`
`(TenantId, RecordType, RecordId, ClaimedAtUtc)` — geçmiş sorgusu. Profil `SchemaProfile.RecordCodes` (DL-1'in profili; yeni profil
yok). **Silme yok** (soft delete dahil; `IsDeleted` daima `false`): emekli satır geçmişin kendisidir. Güncelleme yalnız iki
koşullu yazımdır — `RetireIfActiveAsync` (filtre `TenantId`, `RecordType`, `RecordId`, `Code == beklenen`, `Status == Active`,
`LockedAtUtc == null`) ve `LockIfUnlockedAsync` (filtre `TenantId`, `RecordType`, `RecordId`, `Status == Active`; `LockedAtUtc`
yalnız boşsa yazılır; dönüş `Matched`, `LockedNow` — DL-1 `LockResult` deseni, Bölüm 3).

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
**dokunmadan önce sahip onayı** gerekir. **Onay verildi (r13):** Q-7 — yalnız r12 Bölüm 5 "DL-2" XMC satırlarındaki MOD-0288
dosyaları, onaylayan merkez sahibi Beste (2026-11-10, `docs/records/decisions/2026-11/mod0040-fu02-owner-decisions-03.md`, K-16);
**ek onay** — yalnız `InMemoryTenantOrganizationRepositories.cs`, yalnız dar arayüzlerin uygulanması (2026-11-10,
`…/mod0040-fu02-owner-decisions-04.md` F-1, K-17). Listede olmayan MOD-0288 dosyası onaysızdır. r13'te bir XMC satırının
**içeriği** değişti (dosya aynı, onay dosya düzeyinde): `TenantOrganizationRequestValidators.cs` (kod kuralı tümüyle kalkar —
inceleme 04 F-6), `IOrganizationUnitRepository.cs` / `IPositionRepository.cs` (mevcut arayüze yöntem değil, ayrı dar arayüz —
F-1), depolar (dar arayüzü uygular — F-1), `CreatePositionCommandHandler.cs` (ikinci okuma — F-2). Satır numaraları
`a9fd192ba`'da ölçüldü, r13'te `f73964c6e`'de yeniden doğrulandı (aynı).

Bu paketin dosyaları (MOD-0040-FU02):

- M `services/Diten.Platform/src/Diten.Platform.Domain/Features/RecordCodes/RecordCodeTypeCatalog.cs` — yalnız `ORG` / `POS`
  satırları (`:21-22`: kapsam `[LegalEntity]`, çağıran `"Diten.Platform"`, izin) ve sınıf yorumu (`:10-12`).
- N `services/Diten.Platform/src/Diten.Platform.Application/Contracts/RecordCodes/IInProcessRecordCodeAllocator.cs` — port ve
  iki kayıt (Bölüm 3).
- N `services/Diten.Platform/src/Diten.Platform.Application/Features/RecordCodes/InProcessRecordCodeAllocator.cs`.
- M `services/Diten.Platform/src/Diten.Platform.Application/Features/RecordCodes/RecordCodeReasonCodes.cs` — yalnız üç yeni sabit.
- M `services/Diten.Platform/src/Diten.Platform.Application/DependencyInjection.cs` — yalnız
  `services.AddScoped<IInProcessRecordCodeAllocator, InProcessRecordCodeAllocator>();`.
- M `services/Diten.Platform/src/Diten.Platform.Infrastructure/DependencyInjection.cs` (kompozisyon dosyası; DL-1'deki gibi
  yalnız kayıt satırları, r13 K-17 F-1) — yalnız iki satır: `services.AddScoped<IOrganizationUnitCodeHistoryRepository>(sp =>
  sp.GetRequiredService<OrganizationUnitRepository>());` (mevcut `:337-339` deseni — aynı istek içinde tek depo örneği) ve
  `services.AddScoped<IPositionCodeHistoryRepository, PositionRepository>();` (mevcut `:343` satırı **değişmez**; ayrı örnek
  zararsızdır — yöntem durumsuz bir okumadır, kiracıyı istek kapsamlı `ITenantContext`'ten alır). Elle kurulan test DI ana
  makineleri (ör. `TimeEntry/TimeEntryTestHost.cs:487-492`) ORG / POS oluşturma komutu göndermez (ölçüldü: `CreateOrganizationUnitCommand`
  / `CreatePositionCommand` gönderen ya da işleyiciyi kuran testler yalnız `TenantOrganization/**` altındaki üç dosya ve
  `Audit/Faz1AuditCoverageTests.cs:29` — o, yalnız denetim üst verisini okur); bu yüzden onlara kayıt eklenmez.
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

MOD-0288 dosyaları (**XMC-001 — onaylı**: Q-7, K-16; `InMemoryTenantOrganizationRepositories.cs` için ek onay K-17 F-1; her
satır ayrı onay kalemidir, listede olmayan dosyaya dokunulmaz):

- M (XMC) `services/Diten.Platform/src/Diten.Platform.Application/Features/TenantOrganization/Handlers/CommandHandlers/CreateOrganizationUnitCommandHandler.cs`
  — kurucuya `IInProcessRecordCodeAllocator`, `IOrganizationUnitCodeHistoryRepository` ve `ILogger<CreateOrganizationUnitCommandHandler>` (bugün logger yok, `:19-29`; çakışma atlaması ve yazım hatası uyarısı için); `:34-43` elle kod dalı yerine Bölüm 8.14
  akışı; `:76-84` varlık kurulumunda `Id` = önceden üretilen kayıt kimliği, `Code` = ayrılan kod; kayıt yazımı (`:87`) Bölüm 8.14'teki
  uyarı logu + yeniden fırlatma kalıbıyla sarılır (inceleme 04 F-10).
- M (XMC) `…/TenantOrganization/Handlers/CommandHandlers/CreatePositionCommandHandler.cs` — aynı (kurucuya `IPositionCodeHistoryRepository` ve `ILogger<CreatePositionCommandHandler>`, bugün `:17-22`);
  şirket = birimin `LegalEntityId`'si, `PositionReferenceGuard.ValidateAsync` başarılı olduktan sonra birimin **ikinci**
  `_organizationUnits.GetByIdAsync` okumasından (K-17 F-2 (b); `:27-54`; Bölüm 8.14).
- M (XMC) `…/TenantOrganization/Handlers/CommandHandlers/UpdateOrganizationUnitCommandHandler.cs` — `:49-58` ve `:145`: kod
  değişikliği reddi, saklı kodun korunması (Bölüm 8.15). `UpdateOrganizationUnitReportingLinesCommandHandler` (`:188-232`)
  **değişmez** (saklı kodu geri yükler, `:215-216`).
- M (XMC) `…/TenantOrganization/Handlers/CommandHandlers/UpdatePositionCommandHandler.cs` — `:32-41`, `:49`.
- M (XMC) `…/TenantOrganization/TenantOrganizationContracts.cs` — `OrganizationUnitRequest.Code` (`:9`) ve `PositionRequest.Code`
  (`:52`) `string?` olur; yer ve ad değişmez (konumsal kurulum ve JSON bağlaması korunur).
- M (XMC) `…/TenantOrganization/Validators/TenantOrganizationRequestValidators.cs` — `:9` ve `:19`'daki `RuleFor(x => selector(x).Code)`
  satırı **tümüyle kalkar** (r13, inceleme 04 F-6; r12'de `MaximumLength(80)` kalıyordu). Bu iki paylaşılan doğrulayıcı hem
  oluşturma hem güncelleme doğrulayıcısına `Include` edilir (`CreateOrganizationUnitCommandValidator.cs:10`,
  `UpdateOrganizationUnitCommandValidator.cs:11`, `CreatePositionCommandValidator.cs:8`, `UpdatePositionCommandValidator.cs:11`);
  `ValidationBehavior` en dıştadır (`…/Application/DependencyInjection.cs:45-48`) ve kalan bir uzunluk kuralı 80 karakterden uzun
  kodu `RECORD_CODE_SYSTEM_ASSIGNED` / `RECORD_CODE_IMMUTABLE`'a ulaşmadan `ProblemDetails` yapardı. Kodun bütün kuralı
  işleyicidedir (Bölüm 8.15).
- M (XMC) `services/Diten.Platform/src/Diten.Platform.Domain/Repositories/IOrganizationUnitRepository.cs`, `IPositionRepository.cs`
  — yalnız **yeni dar arayüz**, aynı dosyada (r13, K-17 F-1; `IOrganizationReportingGraphRepository` emsali, `IOrganizationUnitRepository.cs:15-24`
  "Widening IOrganizationUnitRepository would have forced every existing test double to grow…"): `public interface
  IOrganizationUnitCodeHistoryRepository { Task<bool> ExistsByCodeIncludingDeletedAsync(string code, CancellationToken ct = default); }`
  ve aynı biçimde `IPositionCodeHistoryRepository`. **`IOrganizationUnitRepository` (`:5-13`) ve `IPositionRepository` (`:5-17`) değişmez.**
  Gerekçe (ölçüldü, `f73964c6e`): mevcut arayüzlere eklenen gövdesiz yöntem şu sekiz test sahtesini derlemeden düşürürdü —
  `TenantOrganization/InMemoryTenantOrganizationRepositories.cs:7`, `:304`; `Tasks/TaskTestDoubles.cs:651`, `:674`;
  `Tasks/TaskOrganizationUnitScopeTests.cs:270`; `WorkAggregation/ApprovalStepContextTests.cs:650`, `:728`;
  `Workflow/WorkflowCrossServiceTests.cs:1151` — ve yalnız ilki onaylıdır.
- M (XMC) `services/Diten.Platform/src/Diten.Platform.Infrastructure/Persistence/Repositories/OrganizationUnitRepository.cs`,
  `PositionRepository.cs` — yalnız dar arayüzün sınıf bildirimine eklenmesi (`OrganizationUnitRepository.cs:12`, `PositionRepository.cs:10`)
  ve o yöntem: filtre `TenantId == bağlam kiracısı && Code == code`, `IsDeleted` filtresi **yok** (bugünkü `ExistsByCodeAsync`
  `ExecutionFilter` ile silinmişleri dışlar — `OrganizationUnitRepository.cs:96-110`, `PositionRepository.cs:17-31`,
  `services/Diten.Platform.Common/src/Diten.Platform.Common/Persistence/Repositories.cs:75-79`).
- M (XMC, **ek onay K-17 F-1**) `services/Diten.Platform/tests/Diten.Platform.Application.Tests/TenantOrganization/InMemoryTenantOrganizationRepositories.cs`
  — **yalnız** `InMemoryOrganizationUnitRepository` (`:6-7`) ve `InMemoryPositionRepository` (`:304`) sınıflarının dar arayüzü
  uygulaması: `_items.Any(x => x.TenantId == _tenantId && x.Code == code)` — `!x.IsDeleted` koşulu yok (silme bayrakla yapılır,
  `:42-48`, `:339-345`; satır listede kalır). Başka değişiklik yok.
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
  anahtarlar (7 dil — Q-3 kararı A, K-16).
- M (XMC, ortak ekran testi) `frontend/Diten.Web/tests/xmc-date-guard-screens.test.js` — `:122` ve `:127` zorunlu alan
  listelerinden `ouCode` / `pCode`.
- **Değişmez:** API controller'ları `services/Diten.Platform/src/Diten.Platform.API/Controllers/Platform/OrganizationUnitsController.cs`
  ve `PositionsController.cs` (`[HasPermission]` anahtarları aynı); Web vekilleri `frontend/Diten.Web/Controllers/OrganizationUnitsController.cs`,
  `PositionsController.cs`; liste ekranları (`Index.cshtml`, `_DataTable.cshtml`, `_Filter.cshtml`, `index.js` — liste ekranı
  dokunma protokolü tetiklenmez); `Details.cshtml`; `_CreateEditOffcanvas.cshtml` parçaları; `OrganizationCodeNormalizer.cs`
  (oluşturma ve güncelleme yolunda artık çağrılmaz — Q-2 kararı A); `…/Infrastructure/Persistence/Configurations/PositionSeed.cs`;
  `PlatformSchemaManifest.Organization.cs` (index değişmez); `tests/architecture/audit-ledger/**`; `gateway/**/ocelot.json`;
  **r13:** `…/TenantOrganization/Handlers/CommandHandlers/PositionReferenceGuard.cs` (K-17 F-2 (b) — birimi döndürmesi için
  değiştirilmez, XMC listesine girmez); depo arayüzlerini uygulayan diğer altı test sahtesi (sekizden ikisi `InMemoryTenantOrganizationRepositories.cs`'tedir) (`Tasks/TaskTestDoubles.cs:651`, `:674`;
  `Tasks/TaskOrganizationUnitScopeTests.cs:270`; `WorkAggregation/ApprovalStepContextTests.cs:650`, `:728`;
  `Workflow/WorkflowCrossServiceTests.cs:1151`) — dar arayüz sayesinde dokunulmaz (K-17 F-1).

**Koordinasyon (ölçülmedi):** MOD-0288-FU03 (`MOD-0288-FU03-organization-unit-screen-matrix-and-values.md`, `status: ready-for-dev`,
`shell: tenant`) ve FU04 aynı Organizasyon Birimi ekranlarına dokunabilir; bugün açık bir dalda bu dosyalarda iş olup olmadığı bu
revizyonda ölçülmedi. Q-7 onayı dosya kapsamını verdi; sıra kararı CT'nindir (DL-2 WP'si yazılırken ölçülür — Bölüm 18.1).

### DL-3 (r14) — tam dosya yolları

N = yeni, M = yalnız belirtilen değişiklik. **Bütün dosyalar bu paketindir (MOD-0040-FU02); XMC-001 satırı yoktur** — MOD-0024
dosyaları yalnız **okunur** (test yansımayla `TasksController`'ın `[HasPermission]` değerini okur; Bölüm 16 AC-62). Kompozisyon
dosyalarının SHA-256'sı sabitlenmez (module-pack-standard §13).

Domain (`services/Diten.Platform/src/Diten.Platform.Domain/`):

- M `Features/RecordCodes/RecordCodeTypeCatalog.cs` — yalnız `TTY` satırı (Bölüm 4.4) ve sınıf yorumu ("İKİSİ DE türleri … unknown"
  cümlesi `TTY` için güncellenir).
- M `Features/RecordCodes/RecordCodeFormat.cs` — yalnız eklemeli `IsValidPlatformCode` ve onun kalıbı.
- M `Features/RecordCodes/RecordCodeExceptions.cs` — yalnız `RecordCodeClaimConflictException`.
- N `Features/RecordCodes/RecordCodeManualCode.cs`
- N `Entities/RecordCodes/RecordCodeClaim.cs`
- N `Enums/RecordCodes/RecordCodeClaimOrigin.cs`, `Enums/RecordCodes/RecordCodeClaimStatus.cs`
- M `Repositories/IRecordCodeRepositories.cs` — yalnız iki yeni arayüz (`IRecordCodeClaimRepository`, `IRecordCodeCounterReader`);
  `IRecordCodeCounterRepository`, `IRecordCodeAllocationRepository`, `IRecordCodeGroupPrefixRepository` **değişmez**.

Application (`services/Diten.Platform/src/Diten.Platform.Application/`):

- N `Features/RecordCodes/Commands/ClaimRecordCodeCommand.cs`, `ChangeRecordCodeCommand.cs`, `LockRecordCodeCommand.cs`
- N `Features/RecordCodes/Queries/PreviewRecordCodeQuery.cs`, `GetRecordCodeHistoryQuery.cs`
- N `Features/RecordCodes/Handlers/CommandHandlers/ClaimRecordCodeHandler.cs`, `ChangeRecordCodeHandler.cs`, `LockRecordCodeHandler.cs`
- N `Features/RecordCodes/Handlers/QueryHandlers/PreviewRecordCodeHandler.cs`, `GetRecordCodeHistoryHandler.cs`
- N `Features/RecordCodes/RecordCodeInProcessGuard.cs`
- N `Features/RecordCodes/InProcessRecordCodeAssignments.cs`
- N `Contracts/RecordCodes/IInProcessRecordCodeAssignments.cs` — port ve iki istek kaydı (Bölüm 8.17).
- M `Features/RecordCodes/RecordCodesModels.cs` — yalnız Bölüm 3'teki DL-3 DTO'ları.
- M `Features/RecordCodes/RecordCodeReasonCodes.cs` — yalnız Bölüm 3'teki dokuz yeni sabit.
- M `Features/RecordCodes/RecordCodeAuditIntent.cs` — yalnız dört yeni yöntem ve onların sabitleri (olay adları, `RequestType`'lar,
  `EntityType = RecordCodeClaim`); mevcut dört yöntem değişmez.
- M `DependencyInjection.cs` — yalnız `services.AddScoped<IInProcessRecordCodeAssignments, InProcessRecordCodeAssignments>();`.
- Doğrulayıcı **yok** (Bölüm 8.5 (d) ve AC-45 gerekçesi; AC-75 ölçer).

Infrastructure (`services/Diten.Platform/src/Diten.Platform.Infrastructure/`):

- M `Persistence/Repositories/RecordCodeRepositories.cs` — yalnız yeni `RecordCodeClaimRepository` sınıfı ve `RecordCodeCounterRepository`'nin
  `IRecordCodeCounterReader`'ı da uygulaması (tek okuma yöntemi; `NextAsync` değişmez).
- M `Persistence/Schema/PlatformSchemaManifest.RecordCodes.cs` — yalnız `record_code_claims` koleksiyonu ve üç index'i (Bölüm 4.5).
- M `Persistence/Schema/PlatformCollections.cs` — yalnız `RecordCodeClaims = "record_code_claims"`.
- M `DependencyInjection.cs` — yalnız iki kayıt satırı: `services.AddScoped<IRecordCodeClaimRepository, RecordCodeClaimRepository>();`
  ve `services.AddScoped<IRecordCodeCounterReader, RecordCodeCounterRepository>();` (bugünkü kayıtlar `:495-497` değişmez).

Testler (`services/Diten.Platform/tests/Diten.Platform.Application.Tests/`):

- N `RecordCodes/RecordCodeManualCodeTests.cs`
- N `RecordCodes/RecordCodeAssignmentMongoTests.cs`
- N `RecordCodes/InProcessRecordCodeAssignmentsTests.cs`
- M `RecordCodes/RecordCodeTypeCatalogTests.cs` — `AC16_the_catalogue_holds_every_automatic_row_of_RCS001_section_3_and_PLN_and_nothing_else`
  (beklenen listeye `TTY`, sınıf denetimi "§3'teki sınıf") ve `AC14_kalsin_ikisi_de_old_and_unknown_types_are_not_in_the_catalogue`
  (`[InlineData("TTY")]` yerine `[InlineData("PFL")]`) yalnız `TTY` için; yeni AC-62 testleri. Başka DL-1 / DL-2 testi değişmez.
- M `RecordCodes/InternalRecordCodesHttpTests.cs` — yalnız AC-62 HTTP testi (`TTY` → 403).

**Değişmez (ölçülür, AC-75):** `AllocateRecordCodeHandler.cs`, `AlignRecordCodeGroupPrefixHandler.cs`, `TrustedRecordCodeCredentialAuthenticator.cs`
(`Consumers`), `TrustedRecordCodeRequestExecutor.cs`, `InternalRecordCodesController.cs`, `TransactionOwnedAuditSourceServices.cs`,
`TransactionOwnedAuditCommands.cs`, `CanonicalTransactionalAuditOutboxWriter.cs`, `docs/analysis/contracts/record-code-allocate.md`;
DL-2 dosyaları (`InProcessRecordCodeAllocator.cs`, `IInProcessRecordCodeAllocator.cs`, MOD-0288 dosyaları); MOD-0024 dosyalarının
tamamı (`…/Features/Tasks/**`, `…/Entities/Tasks/**`, `…/API/Controllers/TasksController.cs`, `frontend/Diten.Web/**`).

**DL-3'ün dışında, CT / sahip adımı (geliştirici yazmaz):** `tests/architecture/audit-ledger/Diten.Platform.md` `## İzler`
`platform-islem-ici` satırının belirtecine `RecordCodeAuditIntent.EnqueueClaimedAsync/RecordCodeAuditIntent.EnqueueChangedAsync/RecordCodeAuditIntent.EnqueueLockedAsync/RecordCodeAuditIntent.EnqueueAssignmentRefusalAsync`
eklenmesi, `tests/architecture/audit-ledger/CT-DECISIONS.md` satırı ve gerekirse `AuditTrailStandardTests` sabiti — **A-20**
(A-10 emsali; bugünkü satır `tests/architecture/audit-ledger/Diten.Platform.md:16`).

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
  "DL-2" listesindekiler (XMC-001 onaylı — K-16, K-17); listede olmayan MOD-0288 dosyası korunan yoldur — **r13 adıyla:**
  `PositionReferenceGuard.cs`, mevcut `IOrganizationUnitRepository` / `IPositionRepository` arayüz gövdeleri ve Bölüm 5'te adı geçen
  altı test sahtesi (inceleme 04 F-1, F-2).
- **DL-3 (r14):** MOD-0024'ün bütün dosyaları (`services/Diten.Platform/src/Diten.Platform.Application/Features/Tasks/**`,
  `…/Domain/Entities/Tasks/**`, `…/API/Controllers/TasksController.cs`, görev ekranları) — DL-3 yalnız okur; bağlanma DL-4'tür ve
  orada XMC-001 onayı ister. DL-2'nin dosyaları (`InProcessRecordCodeAllocator.cs`, `IInProcessRecordCodeAllocator.cs`, MOD-0288
  dosyaları) — DL-3 onlara bağlanmaz (FU-RCS-21). Denetim altyapısı: `TransactionOwnedAuditCommands.cs` (DL-3 komutları
  `ITransactionOwnedAuditCommand` değildir, listeye ad eklenmez), `CanonicalTransactionalAuditOutboxWriter.cs`,
  `ITransactionalAuditRefusalWriter.cs`, `TransactionOwnedAuditSourceServices.cs` (küme büyümez — `Diten.Platform` zaten içinde).
  `IRecordCodeCounterRepository` arayüz gövdesi (okuma ayrı dar arayüzdedir). Sözleşme `docs/analysis/contracts/record-code-allocate.md`.

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
| **DL-2 bağımlılığı — MOD-0288 dosyaları (XMC-001)** | ORG / POS oluşturma ve güncelleme MOD-0288'in kodudur (`…/Features/TenantOrganization/**`, `Views/Organization/**`); MOD-0288 künyesi `owner: platform-team` (`MOD-0288-organization-person-position-directory.md`), `status: done` | **onaylandı (r13):** Q-7 — Bölüm 5 "DL-2" XMC satırları, onaylayan merkez sahibi Beste (K-16); ek onay `InMemoryTenantOrganizationRepositories.cs` (K-17 F-1). DL-2 kodunu artık bekletmez |
| **DL-2 bağımlılığı — tüzel kişilik onay ön kontrolü (TC-3)** | ORG oluşturma bugün yalnız "referenceable" ister (`CreateOrganizationUnitCommandHandler.cs:45-49`; MDM'de `IsReferenceable => OperationalStatus == Active && !IsDeleted`, `services/Diten.MdmService/src/Diten.MdmService.Domain/Entities/LegalEntity.cs:60`); onay alanı ayrıdır (`:50`) ve Platform onu lookup-validation 1.0.0'da göremez (yukarıdaki satır) | ORG / POS süreç içi tüketici TC-3'ü bugün karşılayamaz → **Q-4 kararı A** (K-16): v1.1'e kadar yalnız aktiflik, adlı istisna; FU-RCS-15 v1.1'de onay denetimini herkese uygular |
| **DL-2 canlıya alma bağımlılığı — MDM kısa kod kalıcı kilidi (A-18, sözleşme N-12; r13, K-17 F-4)** | MDM kısa kodu bugün değiştirilebilir (`UpdateLegalEntityHandler.cs:36-37`); DL-2'den sonra organizasyon şeması kurulan her şirket ayırma defterinde LegalEntity kapsamlı satır taşır ve Platform ara dönem kilidi (Bölüm 8.13) türden bağımsız olarak **bütün** LegalEntity tüketicilerini durdurur (Bölüm 8.14 "iki yön") | DL-2 kodunu bekletmez; DL-2'nin **canlıya alınmasını** bekletir (Bölüm 18.1). Sahip MDM sahibi (Ali) + Beste |
| **DL-2 bağımlılığı — kimlik ve izin bağlamı** | ORG / POS yollarında ara katman kiracıyı JWT'den kurar ve `platform_admin`'i reddeder (`services/Diten.Platform.Common/src/Diten.Platform.Common/Tenancy/TenantResolutionMiddleware.cs:314-318`); `IActorPermissionContext` (`…/Application/Contracts/IActorPermissionContext.cs`, kayıt `…/API/Program.cs:141`), `ITenantAuthorizationContext` (`services/Diten.Platform.Common/src/Diten.Platform.Common/Authorization/ITenantAuthorizationContext.cs:3-31`), `Diten.Platform.Common.Observability.ICorrelationContext` var | değişmez; port bunları okur (Bölüm 8.14) |
| **DL-3 bağımlılığı — DL-1 kodu** (ayırma işleyicisi, katalog, depolar, işlem içi kapı ve ret yazıcısı) | main'de (ölçüldü: `origin/main`'de `RecordCodeAuditIntent.cs` var; CT kabulü `docs/records/audits/2026-11/mod-0040-fu02-dl1-ct-acceptance-2026-11-04.md`) | **başlama koşulu — sağlandı.** DL-2 kodu `origin/main`'de yok (ölçüldü); DL-3 ona bağlanmaz (Bölüm 2.5) |
| **DL-3 bağımlılığı — grup öneki ayarı** (TTY `Group` kapsamı) | DL-1 hizalama ucu ve kilidi var (Bölüm 4.3); bir kiracıda hizalama yapılmamışsa TTY sistem kodu 409 `GROUP_PREFIX_MISSING` alır | DL-3 testleri hizalanmış kiracıyla kurulur; canlıda TTY'yi açan DL-4'ün önkoşuludur, DL-3'ün değil |
| **DL-3 bağımlılığı — yeni defter belirteçleri** (`RecordCodeAuditIntent.EnqueueClaimedAsync/…/EnqueueAssignmentRefusalAsync` → `platform-islem-ici`) | defterde yok (`tests/architecture/audit-ledger/Diten.Platform.md:16`) | **DL-3 main'e girmeden önce CT kararı — A-20** (A-10 emsali); kod yazımını bekletmez |
| **DL-4 bağımlılığı (bilgi)** — İKİSİ DE tüketicileri | TTY: MOD-0024 (`TaskType.Code` bugün elle girilir, değişmez, en çok 40 karakter — `services/Diten.Platform/src/Diten.Platform.Application/Features/Tasks/Services/TaskTypeRules.cs:17`); PPM `PFL` / `PRG` / `PRJ` / `INI` / `INV`: MOD-0117, Diten.PpmService (süreç dışı); PLN: MOD-0361 (`SubscriptionPlan`, `GlobalEntity`, Platform kapsamı); Tüzel Kişilik: MOD-0220 (MDM, DL-5) | DL-3'ü bekletmez; DL-4 DL-3'ü bekler (Bölüm 23) |
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
yedek kod yok (AC-54); TC-3 → **Q-4 kararı A** (v1.1'e kadar yalnız aktiflik; adlı istisna, K-16); TC-4 / TC-5 → uygulanmaz (yalnız
LegalEntity); TC-6 / TC-8 → formlar `reason_code`'u 7 dilde gösterir (Bölüm 11, AC-60); TC-7 → kayıt kimliği anahtardadır, kod
kayıttadır (Bölüm 8.14, AC-59). **Diğer tüketicilere yükümlülük (r13, K-17 F-4):** TC-8'in metni DL-2'den sonra Treasury ve MDM
kullanıcıları için de geçerlidir — kısa kodu değişen şirkette organizasyon birimi açılmış olması kendi türlerini 409
`LEGAL_ENTITY_SHORT_CODE_CHANGED`'e düşürür (Bölüm 8.14); bildirim Bölüm 18.1'deki canlıya alma kapısındadır.

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
- **DL-2'den sonra kapsam genişler (r13, inceleme 04 F-4; K-17).** Kilit türden bağımsızdır: (a) yalnız LegalEntity kapsamlı defter
  satırına bakar, (b) türe bakmaz (`RecordCodeRepositories.cs:164-193`: `FindLatestPrefixForLegalEntityAsync` filtresi `TenantId`,
  `LegalEntityId`, `PrefixScope == LegalEntity`; `ExistsPrefixForOtherLegalEntityAsync` filtresi `TenantId`, `Prefix`,
  `PrefixScope == LegalEntity`, `LegalEntityId ≠ X`). DL-1'de yalnız Treasury / MDM ayırması yapılmış şirketler kilit altındaydı;
  DL-2'den sonra **organizasyon birimi açılmış her şirket** kilit altına girer. İki yön ve sonuçları Bölüm 8.14'te; A-18 DL-2'nin
  canlıya alma kapısıdır (Bölüm 18.1).

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
| `OnBehalfOfUserId` | `ITenantAuthorizationContext.UserId` (token `sub`) | `Guid.Empty` ya da `ActorType ≠ tenant_user` → göndermeden 403 `RECORD_CODE_FORBIDDEN` (savunma; bu yollarda ara katman platform aktörünü zaten reddeder, `:314`) — AC-58. **Karşılaştırma kuralı (r13, inceleme 04 F-9):** `string.Equals(ActorType, "tenant_user", StringComparison.OrdinalIgnoreCase)` — ara katmanın kuralıyla birebir (`TenantResolutionMiddleware.cs:199-212`, `:201`; aynı bağlamın `IsPlatformAdmin`'i de `OrdinalIgnoreCase`, `JwtTenantAuthorizationContext.cs:42`): ara katmanın kabul ettiği bir token'ı port reddetmez, reddettiğini kabul etmez |
| `CallerService` | `"Diten.Platform"` (sabit) | denetim kapısının kapalı kümesinde (`TransactionOwnedAuditSourceServices.cs:10-14`, `CanonicalTransactionalAuditOutboxWriter.SourceService`); küme büyümez |
| `AllowedRecordTypes` | `{ RecordType }` yalnız katalogda `AllowedCaller == "Diten.Platform"` ise; değilse boş küme | boş kümede adım 7 → 403 (`AllocateRecordCodeHandler.cs:98-103`) |
| `Request` | `new AllocateRecordCodeRequest(RecordType, RecordCodePrefixScope.LegalEntity, LegalEntityId, IdempotencyKey)` (`RecordCodesModels.cs:15-19`) | adım 5 (gövde) bu yolda hiç ret vermez |
| `IdempotencyKey` | `organization-unit:{RecordId:N}` (ORG, 50 karakter) · `position:{RecordId:N}` (POS, 41 karakter). **Çakışma atlaması (r13, inceleme 04 F-8):** deneme numarası `n` 1'den başlar; **1. denemede ek yoktur**, 2. ve 3. denemede anahtar `{taban}:{n}` olur — `organization-unit:{RecordId:N}:2`, `…:3` (ORG 52, POS 43 karakter); ek ondalık, sıfır dolgusuz; en çok 3 deneme (Bölüm 8.15). Her deneme ayrı anahtar olduğu için adım 9 önceki denemenin kodunu geri **döndürmez** | sözleşme §2 biçimi (1–200 yazdırılabilir ASCII); defter tekilliği `(TenantId, CallerService, IdempotencyKey)` |
| `RequestErrors` | boş | — |
| `HasRequiredPermission` | `!string.IsNullOrWhiteSpace(definition.RequiredPermission) && IActorPermissionContext.Has(definition.RequiredPermission)` — tek bir iç yardımcıda (`InProcessRecordCodeAllocator.HoldsRequiredPermission(string? key, IActorPermissionContext permissions)`) | controller'ın `[HasPermission]` filtresiyle **aynı** değerlendirici (`PermissionClaimEvaluator`; `IActorPermissionContext.cs` sınıf yorumu) — iki kapı aynı kullanıcıya farklı cevap veremez. HTTP yolundaki birebir `HasExactPermission` (Bölüm 8.5 adım 8) servisler arası güvenilir yol içindir; burada principal Platform'un kendi doğruladığı token'dır (paket kararı). **Boş anahtar kapalıdır (r13, inceleme 04 F-9):** `IActorPermissionContext.Has` null / boş anahtara **true** döner (`IActorPermissionContext.cs:31-34` — alan düzeyi yetki için bilinçli); katalogda izni boş bir süreç içi tür ileride açık kapı olmasın diye boş anahtar `false` sayılır → adım 8 403 `RECORD_CODE_PERMISSION_MISSING` + `Denied` ret satırı (ayırma gönderilir ki ret iz bıraksın). Bugün iki süreç içi türün de anahtarı doludur (Bölüm 4.4); kural AC-58 ve S-88 ile sabitlenir |
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
   (`PositionReferenceGuard.ValidateAsync`, `…/PositionReferenceGuard.cs:16-38`), ardından POS'ta birimin ikinci okuması ve şirketin
   alınması ("POS'un şirket kaynağı" aşağıda; K-17 F-2). Başarısız doğrulama numara harcamaz.
2. Port → ayırma (kendi işlemi; başarı ya da ret kaydı yazılır).
3. Çakışma savunması (Bölüm 8.15).
4. Kayıt yazımı (`Code` = ayrılan kod).

- **Ayırma başarısızsa kayıt oluşmaz.** İşleyici portun `StatusCode` ve `ReasonCode`'unu `Response<Guid>.Fail(…)` ile olduğu gibi
  döner (ör. 409 `LEGAL_ENTITY_SHORT_CODE_CHANGED`, 403 `RECORD_CODE_PERMISSION_MISSING`, 403 `RECORD_CODE_TENANT_NOT_ACTIVE`,
  503 `RECORD_CODE_UNAVAILABLE`); kendi sayacına, normalleştiriciye ya da yedek koda **düşmez** (TC-2'nin süreç içi karşılığı; AC-54).
- **Ayırma başarılı, kayıt yazımı başarısızsa** (Mongo hatası, eşzamanlı unique çakışma): kayıt yoktur; defter satırı ve başarı
  denetim satırı kalır; kod bir **boşluktur** — RCS-001 §5.8 "Başarısız kayıtta harcanan numara geri verilmez; boşluk doğal ve
  kabul" (`.antigravity/rules/record-coding-standard.md:123`); uyarı logu (kod, kayıt kimliği, kiracı). Kullanıcının yeniden
  denemesi yeni kayıt kimliği → yeni anahtar → yeni koddur. Defter "verilmiş ama kullanılmamış" kodu böylece taşır (FU-RCS-04
  raporunun konusu).
  **Cevap ve denetim (r13, inceleme 04 F-10; ölçüldü `f73964c6e`).** Kayıt yazımının istisnası zarfa çevrilemez: MediatR sırası
  `ValidationBehavior` → `LoggingBehavior` → `ExceptionBehavior` → `AuditBehavior` → … → işleyici (`…/Application/DependencyInjection.cs:45-52`);
  `AuditBehavior` istisnayı yakalar, merkezi satırı `Outcome = Failed` ve `Metadata` `AuditBehaviorOutcome = RuntimeException`,
  `ExceptionType` ile yazar ve yeniden fırlatır (`…/Contracts/Behaviors/AuditBehavior.cs:63-66`, `:192-206`); `ExceptionBehavior`
  hata loglar, `Response<T>.Fail(string, int)` araması boş döndüğü için yeniden fırlatır (`ExceptionBehavior.cs:42-44`, `:66`, `:78-82`);
  `GlobalExceptionHandler` `MongoException` (ve `InvalidOperationException` olmayan her istisna) için **500 `ProblemDetails`
  "Server Error"**, iç cümlesiz döner (`…/API/Middleware/GlobalExceptionHandler.cs:61-64`). Beklenen cevap budur; `Response<T>` zarfı
  ve `reason_code` yoktur. **Log kalıbı:** işleyici yalnız kayıt yazım çağrısını sarar —
  `try { await _repository.CreateAsync(entity, ct); } catch (Exception ex) when (ex is not OperationCanceledException) {
  _logger.LogWarning(ex, "Record code {Code} was allocated for {RecordId} in tenant {TenantId} but the record was not written; the
  number stays a gap.", …); throw; }` — istisna yutulmaz, türü değişmez (yutmak 201'i ya da sahte bir zarfı mümkün kılardı; S-91).
  `InvalidOperationException` bu yolda 400 olurdu (`GlobalExceptionHandler.cs:57-60`) — Mongo sürücüsü onu atmaz; AC-54 (b)'nin test
  sarmalayıcısı bu yüzden `MongoException` türevi atar.
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

**Kilidin iki yönü ve aynı önek (r13, inceleme 04 F-4; sahip kararı K-17).** Kilit tek bir ortak defter üzerinde çalıştığı için
DL-2 onu iki yönlü yapar (ölçüldü: `AllocateRecordCodeHandler.cs:177-187`, `RecordCodeRepositories.cs:164-193`):

| Durum | Ne olur | Etkilenen |
|---|---|---|
| **Yön 1 — Treasury / MDM önce** | X şirketi `MYG` ile `PAY` (ya da `APD`, `ARI`, `CPS`, `CTP`, LegalEntity `BAC`, `AGR`) almış; MDM kodu `MYX` yapılır | X'te ORG ve POS oluşturma 409 `LEGAL_ENTITY_SHORT_CODE_CHANGED` (AC-56 a) |
| **Yön 2 — ORG / POS önce (yeni)** | X'te organizasyon birimi açılmış (defterde `MYG` LegalEntity satırı); Treasury henüz hiç ayırma yapmamış olsa da MDM kodu `MYX` yapılır | X için **bütün** LegalEntity tüketicileri 409 alır: `PAY`, `APD`, `ARI`, `CPS`, `CTP`, `BAC` (şirket hesabı), `AGR` — ve ORG / POS (AC-56 b). Organizasyon şeması Treasury'den önce kurulduğu için DL-2'den sonra kilit altındaki şirket sayısı pratikte "organizasyonu olan her şirket"tir |
| **Başka şirkette aynı önek** | X `MYG`'yi kullanmış ve kodu `MYX` yapılmış; serbest kalan `MYG` MDM'de Y şirketine verilir (MDM aynı anda iki şirkette aynı kodu kabul etmez, eskisini serbest bırakınca kabul eder — `UpdateLegalEntityHandler.cs:36-37` yalnız tekillik denetler) | Y için ORG / POS **ve** bütün LegalEntity tüketicileri 409 (`ExistsPrefixForOtherLegalEntityAsync`, `AllocateRecordCodeHandler.cs:180`); X de yön 1 / 2 gereği durur (AC-56 c) |

Her üç durumda kod verilmez, sayaç ilerlemez, `Failed` ret satırı (`EvaluationStep = 11`, `Prefix` = MDM'in bugünkü kodu) ve hata
logu yazılır; cevap tüketicinin kendi yoluyla 409'dur (Treasury / MDM: TC-8; ORG / POS: form metni `RecordCodeShortCodeChanged`).

**Kurtarma yolu (tek yetkili yol bugün):** MDM kısa kodu, şirketin defterdeki son öneki olan eski değere geri alınır (yön 1 / 2);
"aynı önek" durumunda önce Y'nin kodu başka bir değere, sonra X'in kodu eski değerine alınır. Bilinçli önek değişikliği (yeni
önekle yeni sayaç) bu paketin kapsamında değildir — DL-5 / A-18 kararıyla açılır. Platform'da elle "kilidi çöz" ucu yoktur ve
açılmaz (defter ekleme-yalnızdır, Bölüm 4.2).

**Canlıya alma kapısı ve bildirim (K-17).** DL-2 kodu yazılabilir (K-17); DL-2 **canlıya alınmadan** önce A-18 (MDM kalıcı
kısa kod kilidi, sözleşme N-12) karara bağlanmış olmalıdır (Bölüm 18.1). Canlıya almadan önce CT, Treasury CT'ye ve MDM sahibine
(Ali) yazılı bildirim yapar: (1) yön 2 ve "aynı önek" durumu, (2) TC-8 metni, (3) yukarıdaki kurtarma yolu, (4) A-18 kararının
içeriği. Bildirimin kaydı DL-2 kabul kaydına bağlanır.

**Ek MDM çağrısı.** ORG oluşturma şirketi bugün `MdmLegalEntityReferenceValidator` ile doğrular (`CreateOrganizationUnitCommandHandler.cs:45-49`);
port ayrıca `MdmLegalEntityShortCodeReader` ile kısa kodu okur (iletilen token `IHttpContextAccessor`'dan,
`…/Infrastructure/Services/Mdm/MdmLegalEntityShortCodeReader.cs:54`) — oluşturma başına iki MDM çağrısı, bütçeler ayrı (5 s / 3 s).
Birleştirme kapsam dışı. POS oluşturma bugün MDM'i hiç çağırmaz; DL-2'den sonra bir kez çağırır ve birimin şirketi aktif değilse
404 `LEGAL_ENTITY_NOT_FOUND` alır (davranış değişikliği; AC-51). Kullanıcı token'ında `mdm.legal-entities.read` yoksa 403
`LEGAL_ENTITY_READ_PERMISSION_MISSING` (Bölüm 8.3).

**POS'un şirket kaynağı (K-15; Q-5 teyit edildi — K-16; okuma yolu K-17 F-2 (b)).** `Position`'da şirket alanı **yoktur**
(`…/Entities/Organization/Position.cs:7-9`: `Code`, `Name`, `OrganizationUnitId`); istekte de yoktur (`PositionRequest`,
`…/TenantOrganization/TenantOrganizationContracts.cs:51-64`). Şirket **birim üzerinden** gelir: `LegalEntityId` = pozisyonun
**oluşturma anındaki** `OrganizationUnit`'inin `LegalEntityId`'si (`OrganizationUnit.cs:9`, `required Guid`). Birim bugün
`PositionReferenceGuard` içinde okunur (`…/PositionReferenceGuard.cs:16-20`; yok ya da arşivli → 404), ama guard `internal static`'tir
ve yalnız `Response<NoContent>` döner (`:6-14`) — birimi vermez. **r13 (K-17 F-2 (b)):** guard **değişmez**; guard başarılı olduktan
sonra `CreatePositionCommandHandler` birimi **ikinci kez** `_organizationUnits.GetByIdAsync(request.Request.OrganizationUnitId, ct)`
ile okur; ikinci okuma `null` ya da `IsArchived` dönerse (iki okuma arasında silindi / arşivlendi) guard'la aynı cevap — 404
"Organization Unit not found." —, ayırma gönderilmez. Şirket bu ikinci okumanın `LegalEntityId`'sidir. **İki okuma arasında birimin
şirketi değişirse** pozisyon ikinci okumadaki şirketin önekini alır; bu kabul edilen sınırdır — RCS-001 §5.2: önek kaydı ilk açan
şirkettir ve kod sonra değişmez; ikinci okuma kaydın açıldığı andaki şirkettir. Başka kaynak kullanılmaz: özellikle
`ITenantAuthorizationContext.LegalEntityId` (kullanıcının bağlam şirketi; yalnız `InitializeAsync`'ten sonra dolar,
`…/Infrastructure/Authorization/JwtTenantAuthorizationContext.cs:71`, `:77-89`) pozisyonun şirketi **değildir** (AC-51, S-78). Pozisyon sonra başka şirketin
birimine taşınırsa (`UpdatePositionCommandHandler.cs:51`) ya da birimin şirketi değişirse (`UpdateOrganizationUnitCommandHandler.cs:147`)
**kod değişmez** — RCS-001 §5.2 (önek kaydı ilk açan şirkettir, kod kalıcıdır; `record-coding-standard.md:90-93`).

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

**Geri uyum — Q-1: karar A (Beste 2026-11-10, K-16).**

| Seçenek | Ne olur | Sayaç başlangıcı / çakışma | Kural |
|---|---|---|---|
| **A — koru (KARAR)** | Bugünkü kodlar olduğu gibi kalır; DL-2'den sonra açılan her kayıt otomatik kod alır | Sayaç (kiracı, şirket kısa kodu, tür) başına `1`'den başlar (DL-1 sayaçlarında ORG / POS belgesi yok). Eski bir kod ancak desene uyuyor **ve** aynı şirket öneki + türü taşıyorsa çakışabilir. **Savunma:** işleyici ayrılan kodu kayıt yazmadan önce kendi koleksiyonunda **silinmişler dahil** arar (dar arayüz `IOrganizationUnitCodeHistoryRepository` / `IPositionCodeHistoryRepository`.`ExistsByCodeIncludingDeletedAsync`, K-17 F-1); varsa kodu atlar ve yeni anahtarla (`{taban}:{n}`, n = 2, 3 — biçim Bölüm 8.14 `IdempotencyKey` satırı) yeniden ayırır, en çok **3** deneme; atlanan numara defterde ayrılmış ama kullanılmamış kalır (§5.8; §5.9 "sayaç üretirken mevcut kodu atlar", `record-coding-standard.md:127-131`), uyarı logu; üçü de çakışırsa 409 `RECORD_CODE_COLLISION`, kayıt yok. DL-2 kabulünde desene uyan eski kod sayımı kiracı başına kayda yazılır; bir (kiracı, önek, tür) için sayım 3'ü aşarsa sayacı en büyük numaranın üstüne taşımak DL-6'nın (yüksek su işareti, §5.11) işidir ve o kiracıda DL-2 devreye alınmadan önce CT'ye bildirilir | RCS-001 §2 "Mevcut kodlar değiştirilmez … Geçmiş kayıtlar eski kodlarıyla kalır" (`record-coding-standard.md:42-43`) ve §7 (`:162-166`) ile birebir |
| **B — göç** (eski kod yeni `LegacyCode` alanına, kayda otomatik kod) | Her mevcut kayda otomatik kod verilir; eski kod `LegacyCode`'da kalır (§5.11 dili). "İkisi birden" budur — eski kod korunur, görünen kod değişir | Göç sırasında sayaç sıfırdan dolar; önce desene uyan eski kodlar sayılmalı | **§2'ye aykırı** ("Mevcut kodlar değiştirilmez … yazışma ve rapor referansları kırılmasın") — yönetim kararı ve `.antigravity` kural değişikliği (Ali) gerekir; varlıkta yeni alan, göç betiği, kodu gösteren ekran ve raporlar → ayrı paket revizyonu. **Seçilmedi** (K-16) |

**Çakışma atlamasının bedeli ve izi (r13, inceleme 04 F-8).** Her deneme ayrı bir ayırmadır (ayrı anahtar, adım 4–13 yeniden koşar,
MDM yeniden okunur): `k` atlamayla biten başarılı oluşturma `k + 1` numara harcar ve defterde / denetimde `k + 1` başarı satırı
bırakır — `k` tanesi kullanılmayan kodları (anahtar `{taban}`, `{taban}:2`, …), sonuncusu kaydın kodunu taşır. Üç denemenin üçü de
çakışırsa 409 `RECORD_CODE_COLLISION` **3 numara yakar** (üç defter satırı, üç Tablo A satırı, kayıt yok, merkezi `Failed`
oluşturma satırı); kullanıcının yeniden denemesi yeni kayıt kimliği → yeni anahtarlar → sayaçta sonraki numaralardır, yani eski
kodların sardığı aralık her denemede 3 ilerler. Bu §5.8 boşluk kuralının içindedir; desene uyan eski kod sayımı (AC-55, Bölüm 18.1)
3'ü aşan bir (kiracı, önek, tür) için DL-6'yı tetikler. Denetim satırları AC-55 ve AC-59'da.

**Kod değişmezliği — Q-2: karar A (Beste 2026-11-10, K-16; kullanıcı için davranış değişikliği — sürüm notu Bölüm 8.16).** RCS-001 §6: "Kod oluşturulduktan sonra OTOMATİK türde
değiştirilemez; API de reddeder (yalnız ekran değil)" (`record-coding-standard.md:157`).

- **A (KARAR) — bütün kodlar değişmez** (eski elle kodlar dahil): güncelleme isteğinde `Code` boş / yok ya da saklı değere
  **birebir** (sıralı karşılaştırma, normalleştirme yok) eşitse kabul ve kod korunur — formlar ve sürükle-bırak tüm kaydı geri
  gönderdiği için bu gereklidir; farklıysa 409 `RECORD_CODE_IMMUTABLE`, kayıt değişmez. Gerekçe: eski kodu düzenlenebilir bırakmak
  kullanıcının desene uyan bir kod yazmasına (§5.9 riski) ve aynı türde iki ayrı kurala yol açar; §2 eski kodların "eski kodlarıyla
  kalması"nı söyler.
- **B — eski kodlar elle değişebilir, yeni kodlar değişmez:** kaydın "otomatik mi" bilgisini taşıması gerekir (defterde
  `organization-unit:{id:N}` anahtarı ya da varlıkta yeni alan); eski kod değişikliğinde normalleştirici + silinmişler dahil
  tekillik + §5.9 desen yasağı. Daha çok kod ve test; **seçilmedi** (K-16).

**Oluşturma isteğinde kod.** `Code` gönderilmez; dolu gelirse 400 `RECORD_CODE_SYSTEM_ASSIGNED`, ayırma yapılmaz (sayaç
ilerlemez). Ret **işleyicide** verilir: `Response<T>` zarfında `reason_code` dolu olsun ve merkezi denetimde `Failed` görünsün diye
(`ValidationBehavior` `AuditBehavior`'dan önce koşar ve `ProblemDetails` döner — Bölüm 8.5 (d) ile aynı gerekçe). Sessiz yok sayma
seçilmedi: kullanıcının yazdığı değerin kaybolması görünmez bir veri kaybıdır.

**"Dolu" ve "boş"un tanımı ve uzunluk (r13, inceleme 04 F-6).** Tek kural, oluşturmada ve güncellemede aynı:
`string.IsNullOrWhiteSpace(Code)` → **boş** (alan yok, `null`, `""` ya da yalnız boşluk); değilse **dolu**. Oluşturma: boş → otomatik
kod (AC-50 / AC-51); dolu → 400 `RECORD_CODE_SYSTEM_ASSIGNED` — **uzunluğu ne olursa olsun** (81+ karakter dahil). Güncelleme: boş →
saklı kod korunur; dolu ve saklı değere sıralı (`StringComparison.Ordinal`) eşit → kabul; dolu ve farklı → 409 `RECORD_CODE_IMMUTABLE`
— kırpma, büyük harfe çevirme, normalleştirme yok (`"  ceo"` saklı `CEO`'ya eşit **değildir** → 409). Bu yüzden kodun doğrulayıcı
kuralı tümüyle kalkar (Bölüm 5, `TenantOrganizationRequestValidators.cs:9`, `:19`): kalan bir `MaximumLength(80)` 81+ karakterli
kodu `ValidationBehavior`'da (`…/Application/DependencyInjection.cs:45`, en dışta) `ProblemDetails`'e çevirir ve merkezi `Failed`
satırı yazılmazdı.

### 8.16 Sürüm notu (DL-2; Q-2 kararı A, inceleme 04 F-11)

DL-2 canlıya alınırken kullanıcıya ve tüketici ekiplere giden notun içeriği (metni CT yayımlar; DL-2 kabul kaydı notun bağlantısını
taşır). Her madde bir **davranış değişikliğidir**:

| # | Değişiklik | Kimi etkiler | Pakette |
|---|---|---|---|
| SN-1 | Organizasyon Birimi ve Pozisyon kodu artık sistem tarafından verilir (`{ŞİRKET}-ORG-000001`, `{ŞİRKET}-POS-000001`); oluşturma formunda kod girilmez, API'de oluşturma isteğindeki dolu kod 400 `RECORD_CODE_SYSTEM_ASSIGNED` alır | ekran kullanıcısı, API çağıranı | 8.15, 11; AC-50…AC-52 |
| SN-2 | Hiçbir ORG / POS kodu düzenlenemez — **eski (elle girilmiş) kodlar dahil**; güncellemede farklı kod 409 `RECORD_CODE_IMMUTABLE` (Q-2 A) | ekran kullanıcısı, API çağıranı, toplu düzeltme betikleri | 8.15; AC-53 |
| SN-3 | Eski kodlar olduğu gibi kalır; yeni kayıtlar yeni biçimi alır (Q-1 A) | rapor / yazışma kullanıcısı | 8.15; AC-55 |
| SN-4 | Şirketin MDM kısa kodu eksik ya da kalıba uymuyorsa (`^[A-Z]{2,5}$`) o şirkette birim / pozisyon açılmaz (409 `LEGAL_ENTITY_SHORT_CODE_MISSING`); kısa kod kullanıldıktan sonra değişmişse ya da başka şirkete geçmişse açılmaz (409 `LEGAL_ENTITY_SHORT_CODE_CHANGED`) | MDM yöneticisi, organizasyon yöneticisi | 8.14; AC-54, AC-56 |
| SN-5 | DL-2'den sonra organizasyon birimi açılmış bir şirketin MDM kısa kodu değiştirilirse Treasury (`PAY`, `APD`, `ARI`, `CPS`, `CTP`) ve MDM (`BAC` şirket hesabı, `AGR`) kod ayırmaları da 409 `LEGAL_ENTITY_SHORT_CODE_CHANGED` alır; kurtarma: kodu eski değerine geri almak | Treasury CT, MDM sahibi | 8.14 "iki yön"; AC-56; Bölüm 18.1 |
| SN-6 | Pozisyon oluşturma artık MDM'i bir kez çağırır: kullanıcı token'ında `mdm.legal-entities.read` yoksa 403 `LEGAL_ENTITY_READ_PERMISSION_MISSING`; birimin şirketi MDM'de aktif değilse 404 `LEGAL_ENTITY_NOT_FOUND` — bugün ikisi de pozisyonu durdurmuyordu | pozisyon oluşturan roller, rol yöneticisi (MOD-0018) | 8.14 "Ek MDM çağrısı"; AC-51 |
| SN-7 | Kod ayırması MDM kısa kod okumasına ya da kayıt kodu deposuna erişemezse birim / pozisyon açılmaz (503 `LEGAL_ENTITY_SOURCE_UNAVAILABLE` / `RECORD_CODE_UNAVAILABLE`); pozisyon için yenidir (bugün MDM'i çağırmaz) | herkes | 8.14; AC-54 |

### 8.17 İKİSİ DE motoru — öneri, sahiplenme, gerekçeli değişiklik, kullanım kilidi (DL-3, r14; RCS-001 §2, §5.8–§5.10, §6)

**Port (Application)** — `services/Diten.Platform/src/Diten.Platform.Application/Contracts/RecordCodes/IInProcessRecordCodeAssignments.cs`:

```csharp
public interface IInProcessRecordCodeAssignments
{
    Task<Response<RecordCodePreviewResult>> PreviewAsync(string recordType, CancellationToken ct);
    Task<Response<RecordCodeClaimResult>> ClaimAsync(InProcessRecordCodeClaimRequest request, CancellationToken ct);
    Task<Response<RecordCodeChangeResult>> ChangeAsync(InProcessRecordCodeChangeRequest request, CancellationToken ct);
    Task<Response<RecordCodeLockResult>> LockAsync(string recordType, Guid recordId, CancellationToken ct);
    Task<Response<IReadOnlyList<RecordCodeHistoryEntry>>> GetHistoryAsync(string recordType, Guid recordId, CancellationToken ct);
}

public sealed record InProcessRecordCodeClaimRequest(string RecordType, Guid RecordId, string? ManualCode, string? Reason);
public sealed record InProcessRecordCodeChangeRequest(
    string RecordType, Guid RecordId, string ExpectedCurrentCode, string NewCode, string Reason);
```

`InProcessRecordCodeAssignments` kendi kuralı taşımaz: doğrulanmış principal'dan komutu / sorguyu kurar ve `ISender` ile gönderir
(DL-2 portunun deseni). Komut alanları DL-1 komutuyla aynı adları taşır: `TenantId` = `TenantGuard.RequireTenant(ITenantContext)`;
`OnBehalfOfUserId` = `ITenantAuthorizationContext.UserId`; `CallerService = "Diten.Platform"`; `AllowedRecordTypes` = `{ recordType }`
yalnız katalogda `AllowedCaller == "Diten.Platform"` ise, değilse boş küme; `HasRequiredPermission` = `RecordCodeInProcessGuard.HoldsRequiredPermission(definition.RequiredPermission, IActorPermissionContext)`
(boş anahtar `false` — DL-2 Bölüm 8.14 F-9 kuralı); `CorrelationId` = `ICorrelationContext.CorrelationId`, boşsa yeni Guid (`N`).
**Savunma** (`RecordCodeInProcessGuard.Defend`): `ITenantContext` kiracısı `ITenantAuthorizationContext.TenantId`'ye eşit değil,
`UserId == Guid.Empty` ya da `ActorType` `tenant_user` değil (`StringComparison.OrdinalIgnoreCase`) → hiçbir şey göndermeden 403
`RECORD_CODE_FORBIDDEN`, ret satırı yok, uyarı logu (DL-2 Bölüm 21.4 ile aynı gerekçe: doğrulanmış kiracı + kişi ikilisi yok). Kural
DL-2 portundakiyle (`InProcessRecordCodeAllocator.HoldsRequiredPermission`, `services/Diten.Platform/src/Diten.Platform.Application/Features/RecordCodes/InProcessRecordCodeAllocator.cs:103`)
aynıdır; DL-3 o dosyaya bağlanmaz (başlama koşulu DL-1), iki kopyanın teke inmesi FU-RCS-21.

**Değerlendirme sırası.** Her adım işleyicidedir (doğrulayıcı yok — retler denetime yazılsın diye, Bölüm 8.5 (d)); `Outcome` Bölüm
21.2'deki tek kurala göre. Adım numarası ret satırında `Metadata.EvaluationStep`'tir. "Ret" sütunundaki `Denied` / `Failed` yalnız
**yazma** komutlarının (sahiplenme, değişiklik, kilit) ret satırı içindir; önizleme ve geçmiş okumadır, retleri aynı durum kodu ve
`reason_code` ile döner ama denetime yazılmaz (AUD-001 §7).

| # | Adım | Önizleme | Sahiplenme | Değişiklik | Kilit | Geçmiş | Ret |
|---|---|---|---|---|---|---|---|
| 1 | Kiracı aktif mi (`ITenantRegistryRepository.GetByIdAsync` + `Status == Active` — DL-1 adım 4 kuralı) | ✓ | ✓ | ✓ | ✓ | ✓ | 403 `RECORD_CODE_TENANT_NOT_ACTIVE`, `Denied` |
| 2 | Tür katalogda mı | ✓ | ✓ | ✓ | ✓ | ✓ | 422 `RECORD_TYPE_UNKNOWN`, `Failed` |
| 3 | Tür çağırana izinli mi (`AllowedRecordTypes`) | ✓ | ✓ | ✓ | ✓ | ✓ | 403 `RECORD_CODE_FORBIDDEN`, `Denied` |
| 4 | Kullanıcı izni (`HasRequiredPermission`) | ✓ | ✓ | ✓ | — (aşağıda) | ✓ | 403 `RECORD_CODE_PERMISSION_MISSING`, `Denied` |
| 5 | Türün sınıfı `İKİSİ DE` mi | ✓ | ✓ | ✓ | ✓ | ✓ | 409 `RECORD_CODE_IMMUTABLE`, `Failed` — OTOMATİK türün kodu elle verilmez, değişmez (RCS-001 §6, `record-coding-standard.md:165@8931275d2`) |
| 6 | İstek biçimi (`RecordId ≠ Guid.Empty`; elle kod yokken gerekçe dolu değil) | — | ✓ | ✓ | ✓ | ✓ | 400 `RECORD_CODE_REQUEST_INVALID`, `Failed` |
| 7 | Aktif sahiplik | — | var ise: aynı kod (elle: normalleştirilmiş kod eşit; sistem: köken `System`) → **200 tekrar** (`Replayed = true`, yeni satır yok); farklı → 409 `RECORD_CODE_ALREADY_ASSIGNED` | yoksa 404 `RECORD_CODE_NOT_ASSIGNED` (başka kiracının kaydı da — tek gövde) | yoksa 404 | hiç satır yoksa 404 | 404 → `Denied` (21.2 kol b); 409 → `Failed` |
| 8 | Beklenen kod = aktif kod mu | — | — | ✓ | — | — | 409 `RECORD_CODE_CONFLICT`, `Failed` |
| 9 | Kilitli mi | — | — | ✓ | zaten kilitli → **200**, `LockedNow = false`, yeni satır yok | — | 409 `RECORD_CODE_LOCKED`, `Failed` |
| 10 | Gerekçe (elle kod ve değişiklik) | — | elle kodda ✓ | ✓ | — | — | 400 `RECORD_CODE_REASON_REQUIRED`, `Failed` |
| 11 | Elle kodun biçimi; otomatik desen yasağı | — | elle kodda ✓ | ✓ | — | — | 400 `RECORD_CODE_MANUAL_FORMAT_INVALID` / `RECORD_CODE_PATTERN_RESERVED`, `Failed` |
| 12 | Yeni kod = aktif kod mu | — | — | ✓ | — | — | 400 `RECORD_CODE_UNCHANGED`, `Failed` |
| 13 | Kod dolu mu (`CodeExistsAsync`: aynı kiracı + tür, **emekliler dahil**, her kayıt — kaydın kendi emekli kodu dahil, Q-14 A önerisi) | — | elle kodda ✓ | ✓ | — | — | 409 `RECORD_CODE_TAKEN`, `Failed` |
| 14 | Önek (Group: kiracı ayarı yoksa 409 `GROUP_PREFIX_MISSING`; etkin `OrgPrefix`'ten farklıysa 409 `GROUP_PREFIX_MISMATCH` — DL-1 adım 11 kuralı, `services/Diten.Platform/src/Diten.Platform.Application/Features/RecordCodes/Handlers/CommandHandlers/AllocateRecordCodeHandler.cs:130-148`) ve sayaç okuması | ✓ → 200 `{ SuggestedCode, IsReserved = false }` | sistem kodunda: DL-1 ayırması (aşağıda) | — | — | — | önizlemede ret satırı yok (okuma); sahiplenmede DL-1'in kendi ret satırı |
| 15 | Tek işlem (`IPlatformTransactionExecutor.ExecuteAsync`) | — | sahiplik ekle + Tablo E | koşullu emeklilik + yeni sahiplik + Tablo F | koşullu kilit + Tablo G (yalnız `LockedNow`) | — | aşağıda |

- **Adım 4 kilitte yoktur (paket kararı, gerekçe):** kilit, tüketicinin kendi `[HasPermission]` kapılı komutunun sonucudur (ör. DL-4'te
  bir görev bu türle açılınca); o kullanıcı türün yönetim iznini taşımaz. Kilidi isteyen yine doğrulanmış kiracı kullanıcısıdır
  (savunma, adım 1–3 ve 5); denetim aktörü odur.
- **Önizleme** = `{önek}-{tür}-{PeekLastNumberAsync + 1, 6+ hane}`; sayaç belgesi yoksa `…-000001`. Sayaç **ilerlemez**, belge
  yaratılmaz, kilit yazılmaz, denetim satırı yok. Önizleme bir **sözdür, ayırma değildir**: arada başka bir ayırma olursa sahiplenme
  sonraki numarayı alır. Önek kuralı DL-1 adım 11'in kopyasıdır (iki satırlık eşitlik); aynı durumda iki yolun aynı cevabı verdiğini
  parite testi sabitler (AC-65).
- **Sahiplenme, sistem kodu** (elle kod yok): işleyici DL-1 `AllocateRecordCodeCommand`'ı `ISender` ile gönderir — `PrefixScope`
  = türün tek izinli kapsamı (TTY → `Group`, `LegalEntityId = null`), `IdempotencyKey = {tür küçük harf}:{RecordId:N}` (TTY → `tty:…`,
  36 karakter), `AllowedRecordTypes` / `HasRequiredPermission` / `CallerService` komuttan aynen. DL-1'in adım 4–13'ü ve ret kaydı aynen
  koşar; ret, durum kodu ve `reason_code` değişmeden döner, sahiplik satırı yazılmaz. Başarıda ikinci işlemde sahiplik satırı
  (`Origin = System`) + Tablo E yazılır. **İki işlem, sabit sıra:** ikinci işlem düşerse 503 `RECORD_CODE_UNAVAILABLE`; ayırma
  defterde kalır; aynı kayıtla tekrar → DL-1 adım 9 aynı kodu döner, sahiplik satırı yazılır (tek satır). Kod bir boşluk olarak
  kalabilir (§5.8, `record-coding-standard.md:131@8931275d2`) ama iki kayda gidemez.
- **Sahiplenme, elle kod:** sayaç ve DL-1 defteri **hiç** kullanılmaz; tek işlemde sahiplik satırı (`Origin = Manual`, `Reason`) +
  Tablo E.
- **Değişiklik:** tek işlemde (a) `RetireIfActiveAsync(…, expectedCode)` — eşleşmezse işlem iptal, satır yeniden okunur: kilitliyse 409
  `RECORD_CODE_LOCKED`, kod farklıysa 409 `RECORD_CODE_CONFLICT` (ön denetimle yarışan kilit / değişiklik); (b) yeni sahiplik
  (`Origin = Manual`, `Reason`); (c) Tablo F. Hepsi birlikte commit olur ya da hiçbiri olmaz. **Sistem kodu değişikliğin hedefi
  olamaz** (yeni kod her zaman elle koddur; §5.9 yasağı sistem desenini reddeder).
- **E11000** (sahiplik ekleme yarışı): depo `RecordCodeClaimConflictException(IndexName)` atar; işleyici `…_tenant_type_code` için 409
  `RECORD_CODE_TAKEN`, `…_tenant_type_record_active` için satırı yeniden okur → aynı kodsa 200 tekrar, değilse 409
  `RECORD_CODE_ALREADY_ASSIGNED`. 500 olmaz.
- **İşlem içi kapı `false`** (değişmez ihlali) ya da işlem / bütçe tükenir → 503 `RECORD_CODE_UNAVAILABLE`, hiçbir satır kalmaz (DL-1
  Bölüm 8.6 kuralı). Ret kaydı ayrı kısa işlemle; yazılamazsa uyarı logu, cevap değişmez (K-10).
- **Hız sınırı yok** (DL-2 Bölüm 8.14 adlı kararıyla aynı gerekçe: kimlik bilgisi yok, her çağrı kullanıcı başlatmalı).

**Elle kod — `RecordCodeManualCode` (RCS-001 §5.9, §5.10; Q-11 A, Q-12 A önerisi).**

1. Normalleştirme (tek yön, sabit): baştaki / sondaki boşluk kırpılır; Türkçe harfler ASCII karşılığına çevrilir — `Ç→C`, `Ğ→G`,
   `İ→I`, `I→I`, `Ö→O`, `Ş→S`, `Ü→U` ve küçükleri `ç→C`, `ğ→G`, `ı→I`, `i→I`, `ö→O`, `ş→S`, `ü→U` (§5.10: "elle girişte Türkçe harf
   ASCII karşılığına çevrilir", `record-coding-standard.md:143-144@8931275d2`); ardından `ToUpperInvariant`. Başka hiçbir dönüşüm yok (boşluk,
   alt çizgi, nokta, `-` dışı işaret **çevrilmez** → reddedilir).
2. Biçim: `^[A-Z0-9]+(?:-[A-Z0-9]+)*\z`, uzunluk **2–40** (MOD-0024 bugünkü üst sınırı 40 — `TaskTypeRules.cs:17`) → değilse 400
   `RECORD_CODE_MANUAL_FORMAT_INVALID`.
3. Otomatik desen yasağı (§5.9 varsayılan seçeneği, `record-coding-standard.md:135-139@8931275d2`): normalleştirilmiş kod
   `RecordCodeFormat.IsValidCode` (DL-1 kalıbı `^[A-Z]{2,5}-[A-Z]{3}-[0-9]{6,}\z`, `services/Diten.Platform/src/Diten.Platform.Domain/Features/RecordCodes/RecordCodeFormat.cs:18`)
   ya da `RecordCodeFormat.IsValidPlatformCode` (`^[A-Z]{3}-[0-9]{6,}\z`, §5.3 `PLN-000001`) ise 400 `RECORD_CODE_PATTERN_RESERVED`.
   Kalıp **kopyalanmaz** — tek kaynak `RecordCodeFormat`.
4. Kayda giden ve denetime yazılan değer normalleştirilmiş koddur; cevap onu döner (kullanıcı neyin kaydedildiğini görür).

**Gerekçe (RCS-001 §2; Q-9 A, Q-10 A önerisi).** Kırpılmış uzunluk **10–500** karakter; `null`, boş, yalnız boşluk, 9 karakter → 400
`RECORD_CODE_REASON_REQUIRED`; 501+ → aynı kod. Metin kırpılır, başka dönüşüm yok. Gerekçe elle kodla oluşturmada **ve** her
değişiklikte zorunludur; sistem kodunda gerekçe gönderilirse 400 `RECORD_CODE_REQUEST_INVALID` (sessiz yok sayma yok — DL-2
`RECORD_CODE_SYSTEM_ASSIGNED` gerekçesi). Gerekçe yalnız başarı satırına (Tablo E / F) yazılır; ret satırına yazılmaz.

**Tüketici kaydı ile tutarlılık (DL-4'ün işi; Q-16 A önerisi).** Motor kodun geçmişinin kaynağıdır; tüketici kendi kaydında kopyasını
tutar ve motordan **sonra** yazar. Motor başarılı, tüketici yazımı düştüyse tüketici `GetHistoryAsync` / aktif satırla kendi kaydını
onarır; tüketici yazımını motorun işlemine katmak (oturumlu port) DL-4'te tüketiciyle birlikte karara bağlanır. DL-3 bu ilişkiyi
ölçmez (tüketici yok).

## 9. Layout & Shell Contract

`shell: none`. Bu FU hiçbir Razor görünümü, ön yüz rotası, DataTable ya da gezinme öğesi sahiplenmez; hiçbir `.cshtml`
oluşturulmaz, bu yüzden `Layout = "_LayoutPlatformAdmin"` ya da `Layout = "_LayoutTenantShell"` yazılacak dosya yoktur.
DL-10 ekranları Platform yönetici kabuğunda olacaktır (`Layout = "_LayoutPlatformAdmin"`, `Views/Platform/RecordCodes/`).

**DL-2 (r12).** Bu paket yeni görünüm açmaz; MOD-0288'in var olan iki formunu değiştirir (XMC-001, Bölüm 5 "DL-2"):
`frontend/Diten.Web/Views/Organization/OrganizationUnits/Form.cshtml` ve `frontend/Diten.Web/Views/Organization/Positions/Form.cshtml`.
İkisi de **`Layout = "_LayoutTenantShell"`** taşır (`OrganizationUnits/Form.cshtml:10`, `Positions/Form.cshtml:9`; ölçüldü) ve DL-2'de
layout **değişmez** — kiracı kabuğu ekranlarıdır (Bölüm 11 dil kararı buna dayanır). AC-60 iki formun bu satırı değişmeden
taşıdığını ölçer.

**DL-3 (r14).** Görünüm yok, layout yok (Q-15 A önerisi). Öneri ve gerekçeli değişiklik ekranı tüketicinin ekranıdır: TTY için
MOD-0024'ün görev türü ekranı (kiracı kabuğu) DL-4'te bağlanır ve layout'unu o dilim yazar.

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

**DL-3 (r14)** — aynı klasöre eklenir (Golden Reference adları; işleyicide `Command` / `Query` eki yok; `Validators/` yok):

```text
Features/RecordCodes/
├── Commands/        ClaimRecordCodeCommand.cs · ChangeRecordCodeCommand.cs · LockRecordCodeCommand.cs
├── Queries/         PreviewRecordCodeQuery.cs · GetRecordCodeHistoryQuery.cs
├── Handlers/
│   ├── CommandHandlers/  ClaimRecordCodeHandler.cs · ChangeRecordCodeHandler.cs · LockRecordCodeHandler.cs
│   └── QueryHandlers/    PreviewRecordCodeHandler.cs · GetRecordCodeHistoryHandler.cs
├── InProcessRecordCodeAssignments.cs   (port uygulaması)
└── RecordCodeInProcessGuard.cs         (süreç içi savunma + izin hükmü)
```

- Üç komut `AllocateRecordCodeCommand` gibi `IAuditableCommand` ve `ITransactionOwnedAuditCommand` **değildir**: `AuditBehavior` onları
  kayıtsız geçirir, denetim işleyicinin işlemindedir; `TransactionOwnedAuditCommands.Authorized` değişmez.

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
  Modülleri (Admin)" için, kiracı modüllerinde 7 dil. **Q-3 kararı A — 7 dil** (Beste 2026-11-10, K-16; CT talimatındaki "2 dil"
  CT'nin hatasıydı).
- **Anahtarlar** (iki kaynak dosyası, her dilde) ve eşledikleri `reason_code`: `RecordCodeAssignedOnSave` (not), `RecordCodeImmutable`
  (`RECORD_CODE_IMMUTABLE`), `RecordCodeSystemAssigned` (`RECORD_CODE_SYSTEM_ASSIGNED`), `RecordCodeCollision` (`RECORD_CODE_COLLISION`),
  `RecordCodeShortCodeChanged` (`LEGAL_ENTITY_SHORT_CODE_CHANGED`), `RecordCodeShortCodeMissing` (`LEGAL_ENTITY_SHORT_CODE_MISSING`),
  `RecordCodeLegalEntityNotFound` (`LEGAL_ENTITY_NOT_FOUND`), `RecordCodeLegalEntityNotEligible` (`LEGAL_ENTITY_NOT_ELIGIBLE`),
  `RecordCodeLegalEntityReadPermissionMissing` (`LEGAL_ENTITY_READ_PERMISSION_MISSING`), `RecordCodePermissionMissing`
  (`RECORD_CODE_PERMISSION_MISSING`), `RecordCodeTenantNotActive` (`RECORD_CODE_TENANT_NOT_ACTIVE`), `RecordCodeUnavailable`
  (`RECORD_CODE_UNAVAILABLE`), **r13 (inceleme 04 F-5):** `RecordCodeLegalEntitySourceUnavailable` (`LEGAL_ENTITY_SOURCE_UNAVAILABLE`
  — 503; MDM 400 / 401 / 5xx / bütçe aşımı / bağlantı yok ya da tutarsız 200, `MdmLegalEntityShortCodeReader.cs:70`, `:82`, `:121`;
  sabit `RecordCodeReasonCodes.cs:23`), `RecordCodeForbidden` (`RECORD_CODE_FORBIDDEN` — 403; portun kiracı / aktör savunması ve
  adım 7, Bölüm 8.14). Süreç içi yolda oluşamayan kodlar (`RECORD_TYPE_UNKNOWN`, `RECORD_CODE_SCOPE_INVALID`, `RECORD_CODE_REQUEST_INVALID`,
  `IDEMPOTENCY_KEY_REUSED`, `GROUP_PREFIX_*`, `RECORD_CODE_RATE_LIMITED`) anahtar almaz; gelirse bugünkü davranış sürer. 6 dilin
  (`tr`, `fr`, `es`, `zh`, `ar`, `ru`) hiçbirinde değer `en` değeriyle aynı bırakılmaz (`.antigravity/rules/localization-standard.md:14`
  "Placeholder YASAK"). `_FormL10n.cshtml` bunları JS'e aktarır. Var olan `CodePlaceholder`,
  `InvalidCode`, `FinalCodePreview` anahtarları kullanılmaz hâle gelir ama **silinmez** (FU-RCS-18).
- **Değişmeyenler:** liste ekranları (`Index.cshtml`, `_DataTable.cshtml`, `_Filter.cshtml`, `index.js`) — liste ekranı dokunma
  protokolü (BL-440) tetiklenmez; kullanılmayan `_CreateEditOffcanvas.cshtml` parçaları (hiçbir görünüm çağırmıyor — ölçüldü;
  yalnız `frontend/Diten.Web/tests/diten-field-icons.test.js:610-612` adlarını anıyor) — FU-RCS-18.
- **Sayfa yardımı (UXH-001):** iki formda sayfa yardımı işareti bulunmadı (`PageHelp` / `page-help` arandı, ölçüldü); yardım metni
  değişikliği gerekmez.

**DL-3 (r14).** Yok — dosya, `.resx`, JS yok. Motor kullanıcıya görünen metin üretmez; yalnız `reason_code` döner (Bölüm 3). Metinler
tüketici ekranının kaynak dosyasındadır ve **dil sayısını o ekranın kabuğu belirler** (`.antigravity/rules/localization-standard.md:8-9`):
TTY ekranı kiracı kabuğunda → 7 dil (DL-4); Platform yönetici ekranları → 2 dil (DL-10). "2 dil" DL-3 için geçerli değildir —
DL-2 Q-3'teki hatanın tekrarı olmasın diye adıyla yazıldı.

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
| ORG / POS oluşturma `code` (**DL-2**) | **gönderilmez** | `IsNullOrWhiteSpace` → boş, otomatik kod; dolu (uzunluk ne olursa olsun) → 400 `RECORD_CODE_SYSTEM_ASSIGNED`, ayırma yok — işleyicide; **doğrulayıcıda kod kuralı yok** (r13, inceleme 04 F-6: `TenantOrganizationRequestValidators.cs:9`, `:19` satırı kalkar) | `ux_organization_units_tenant_code_active` / `ux_positions_tenant_code_active` (değişmez) | ayırma Bölüm 8.14; çakışma savunması Bölüm 8.15 |
| ORG / POS güncelleme `code` (**DL-2**, Q-2 kararı A) | hayır | `IsNullOrWhiteSpace` → boş, kabul, kod korunur; saklı değere `Ordinal` eşit → kabul; farklı (81+ karakter ve baştaki / sondaki boşluk ya da harf büyüklüğü farkı dahil) → 409 `RECORD_CODE_IMMUTABLE`, kayıt değişmez; normalleştirme yok | — | raporlama hattı ucu saklı kodu gönderir (değişmez) |
| ORG / POS üretilen kod (**DL-2**) | — | `^[A-Z]{2,5}-(ORG\|POS)-[0-9]{6,}$`; önek = şirketin MDM kısa kodu (POS'ta birimin şirketi) | defter `(TenantId, Code)` + kayıt koleksiyonunun `(TenantId, Code)` unique'i | silinmişler dahil çakışma araması (Bölüm 8.15); en çok 3 atlama, sonra 409 `RECORD_CODE_COLLISION` |
| İKİSİ DE elle kod (**DL-3**; sahiplenme `ManualCode`, değişiklik `NewCode`) | sahiplenmede hayır (yoksa sistem kodu); değişiklikte evet | kırp → Türkçe harf ASCII → `ToUpperInvariant`; sonra `^[A-Z0-9]+(?:-[A-Z0-9]+)*\z`, 2–40 karakter (değilse 400 `RECORD_CODE_MANUAL_FORMAT_INVALID`); `RecordCodeFormat.IsValidCode` ya da `IsValidPlatformCode` → 400 `RECORD_CODE_PATTERN_RESERVED` (Q-11 A, Q-12 A önerisi) | `ux_record_code_claims_tenant_type_code` (emekliler dahil) | `CodeExistsAsync` → 409 `RECORD_CODE_TAKEN`; işleyicide, doğrulayıcı yok |
| Gerekçe (**DL-3**) | elle kodda ve değişiklikte evet; sistem kodunda **gönderilmez** | kırpılmış 10–500 karakter (Q-10 A önerisi); değilse 400 `RECORD_CODE_REASON_REQUIRED`; sistem kodunda dolu → 400 `RECORD_CODE_REQUEST_INVALID` | — | yalnız başarı satırına yazılır (Tablo E / F) |
| `RecordId` (**DL-3**) | evet | Guid, `Guid.Empty` değil → değilse 400 `RECORD_CODE_REQUEST_INVALID` | `ux_record_code_claims_tenant_type_record_active` (aktif satırda tek) | aktif sahiplik araması (Bölüm 8.17 adım 7) |
| `ExpectedCurrentCode` (**DL-3**, değişiklik) | evet | aktif koda **sıralı** (`Ordinal`) eşit; normalleştirme yok | `RetireIfActiveAsync` filtresi | farklı → 409 `RECORD_CODE_CONFLICT` |
| Sistem kodu (**DL-3**, TTY) | — | `^[A-Z]{2,5}-TTY-[0-9]{6,}$`; önek = grup öneki (RCS-001 §5.3) | DL-1 defteri + `ux_record_code_claims_tenant_type_code` | DL-1 ayırması (Bölüm 8.17) |

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
- **Ayırma başarılı, kayıt yazımı düştü** — kayıt yok; defter satırı ve başarı denetimi kalır; kod boşluktur; uyarı logu; cevap
  **500 `ProblemDetails` "Server Error"** (zarf ve `reason_code` yok), merkezi satır `Failed` + `AuditBehaviorOutcome = RuntimeException`
  (Bölüm 8.14; AC-54 (b); r13 F-10).
- **Oluşturmada 81+ karakterli kod / yalnız boşluk kod** — 400 `RECORD_CODE_SYSTEM_ASSIGNED` zarfta (`ProblemDetails` değil) / boş
  sayılır, otomatik kod (AC-52; r13 F-6).
- **Pozisyon oluşturan kullanıcıda `mdm.legal-entities.read` yok** — 403 `LEGAL_ENTITY_READ_PERMISSION_MISSING`, pozisyon yok, `Denied`
  ayırma ret satırı (bugün oluşurdu — sürüm notu SN-6; AC-51; r13 F-11).
- **Kilidin ters yönü** — ORG açılmış şirketin MDM kodu değişince `PAY` (HTTP) 409 `LEGAL_ENTITY_SHORT_CODE_CHANGED`; serbest kalan
  kodu alan başka şirkette ORG / POS ve HTTP türleri 409 (Bölüm 8.14; AC-56 (b), (c); r13 F-4).
- **Üç çakışma** — 409 `RECORD_CODE_COLLISION`, 3 numara yanar, 3 başarı satırı defterde ve denetimde kalır (Bölüm 8.15; AC-55; r13 F-8).
- **Çerçeve yetki reddi** (`[HasPermission]`) — 403 zarfta `reason_code = PERM_DENIED`; ayırma ve ayırma ret satırı yok (AC-58; r13 F-3).
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
- **Port savunması** — `ITenantContext` kiracısı token kiracısıyla uyuşmuyor, `ActorType ≠ tenant_user` (`OrdinalIgnoreCase`) ya da
  `sub` boş → 403 `RECORD_CODE_FORBIDDEN`, ayırma gönderilmez (AC-57, AC-58). Katalogda izni boş süreç içi tür → 403
  `RECORD_CODE_PERMISSION_MISSING` + `Denied` (r13 F-9; AC-58).
- **Başarı yolunda denetim kapısı istisnası** — kayıt yok; 503 / 500 `ProblemDetails` (Bölüm 8.6, 8.14; AC-54 (d)).

**DL-3 (r14) — İKİSİ DE motoru (TTY ile ölçülür):**

- **Önizleme iki kez, sahiplenme yok** — iki cevap da `GMG-TTY-000001`, `IsReserved = false`; sayaç belgesi yok, denetim satırı yok (AC-65).
- **Önizlemeden sonra araya başka ayırma girer** — sahiplenme `GMG-TTY-000002` alır; önizleme ayrılmamıştır (AC-65).
- **Grup öneki ayarı yok / `OrgPrefix`'ten farklı** — önizleme ve sistem sahiplenmesi aynı 409 `GROUP_PREFIX_MISSING` / `GROUP_PREFIX_MISMATCH`; sahiplik satırı yok (AC-65, AC-66).
- **Sistem sahiplenmesinin ikinci işlemi düşer** — 503 `RECORD_CODE_UNAVAILABLE`; ayırma defterde kalır; aynı kayıtla tekrar → 200 aynı kod, tek sahiplik satırı (AC-66).
- **Aynı kayıt için ikinci sahiplenme** — aynı kod → 200 `Replayed = true`, yeni satır yok; farklı kod → 409 `RECORD_CODE_ALREADY_ASSIGNED` (AC-66, AC-67).
- **Elle kod başka bir kaydın aktif ya da emekli kodu** — 409 `RECORD_CODE_TAKEN`; sayaç ve defter dokunulmaz (AC-67).
- **Aynı elle kodla iki kayıt, ön denetim atlatılır** — ikinci ekleme E11000 → 409 `RECORD_CODE_TAKEN`; 500 / 503 değil (AC-67).
- **Elle kod otomatik desende** (`GMG-TTY-000050`, `gmg-tty-1000000`, `PLN-000001`) — 400 `RECORD_CODE_PATTERN_RESERVED` (AC-63).
- **Elle kod biçimsiz** (boşluk, alt çizgi, `--`, baştaki `-`, 1 karakter, 41 karakter, ASCII dışı) — 400 `RECORD_CODE_MANUAL_FORMAT_INVALID` (AC-63).
- **Gerekçe yok / kısa / uzun** — 400 `RECORD_CODE_REASON_REQUIRED`; sistem kodunda gerekçe → 400 `RECORD_CODE_REQUEST_INVALID` (AC-64).
- **Beklenen kod eskimiş** (form açıkken başkası değiştirdi) — 409 `RECORD_CODE_CONFLICT`; değişmez (AC-68).
- **Yeni kod = aktif kod** — 400 `RECORD_CODE_UNCHANGED` (AC-68).
- **Kaydın kendi emekli koduna dönüş** — 409 `RECORD_CODE_TAKEN` (Q-14 A önerisi; AC-68).
- **Değişiklik işleminde kapı `false` / yeni sahiplik eklemesi düşer** — 503; eski kod aktif kalır, emeklilik geri alınır, denetim satırı yok (AC-68).
- **OTOMATİK türde sahiplenme / değişiklik / kilit** (`ORG`, `POS` — süreç içi çağıranı `Diten.Platform` olan OTOMATİK türler; başka
  çağıranın türü, ör. `PAY`, adım 3'te 403 alır) — 409 `RECORD_CODE_IMMUTABLE`; hiçbir sahiplik satırı (AC-69).
- **Kilitli kod değiştirilir** — 409 `RECORD_CODE_LOCKED`; ön denetimden sonra gelen kilit de emeklilik filtresinde yakalanır (AC-70).
- **İkinci kilit** — 200 `LockedNow = false`, `LockedAtUtc` değişmez, yeni denetim satırı yok (AC-70).
- **Başka kiracının kaydı** (değişiklik / kilit / geçmiş) — 404 `RECORD_CODE_NOT_ASSIGNED` (tek gövde), `Denied` ret satırı istekçinin kiracısında; B'nin satırı değişmez (AC-72).
- **Askıdaki kiracı** — beş işlemde 403 `RECORD_CODE_TENANT_NOT_ACTIVE` (AC-72).
- **İzin yok** — 403 `RECORD_CODE_PERMISSION_MISSING` + `Denied` ret satırı (yazma); okumada satır yok (AC-73).
- **Port savunması** (kiracı çelişkisi, `ActorType ≠ tenant_user`, boş `sub`) — 403 `RECORD_CODE_FORBIDDEN`, hiçbir şey gönderilmez (AC-73).
- **İzinli çağıranı olmayan İKİSİ DE tür** (`PLN`) — 403 `RECORD_CODE_FORBIDDEN` (adım 3), `Denied` (AC-73).
- **İç uçtan `TTY`** — 403 `RECORD_CODE_FORBIDDEN` (AC-62).

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
  katmanda reddedilir (`TenantResolutionMiddleware.cs:314-318`); port ayrıca `ActorType = tenant_user` (`OrdinalIgnoreCase`, ara
  katmanla aynı — `:199-212`) ve dolu `sub` ister (savunma, AC-58). **r13 (inceleme 04 F-9):** katalog izni boş / `null` ise adım 8
  reddeder — `IActorPermissionContext.Has`'in "boş anahtar = true" kuralı (`IActorPermissionContext.cs:31-34`) bu yolda uygulanmaz
  (Bölüm 8.14). **İki 403 ayrıdır (r13, F-3; ölçüldü):** çerçevenin reddi `HasPermissionAttribute.PermissionDenied` zarfıdır —
  `reason_code = PERM_DENIED` (`…/API/Security/HasPermissionAttribute.cs:140-155`, sabit `DocumentManagementReasonCodes.PermissionDenied`,
  `…/Features/DocumentManagementContract/DocumentManagementContractModels.cs:31`), işleyici koşmaz, ayırma ret satırı yoktur; adım 8'in
  reddi `RECORD_CODE_PERMISSION_MISSING` + `platform.record-code.allocation-refused` satırıdır. AC-58 ikisini bu iki işaretle ayırır. Denetim aktörü `TenantUser` + `sub`, kaynak servis `Diten.Platform`. **Yeni izin anahtarı yok.** Veri kapsamı (SEC-002)
  MOD-0288'in bugünkü kuralıdır; DL-2 değiştirmez.
- **DL-3 (r14) — süreç içi port, yeni uç yok (Q-15 A önerisi).** DL-3 controller açmaz; bu yüzden DL-3'te yeni bir `[HasPermission]`
  özniteliği ve yeni izin anahtarı **yoktur** (PKS-001 kataloğu, rol köprüsü ve dil dosyaları değişmez). Yetki iki katmanlıdır ve
  ikisi de mevcut anahtardır: (1) tüketicinin kendi ucu — TTY için `TasksController.CreateTaskType` / `UpdateTaskType` üzerindeki
  `[HasPermission(TaskPermissions.TaskTypesManage)]` (`services/Diten.Platform/src/Diten.Platform.API/Controllers/TasksController.cs:855-857`,
  `:865-867`; değer `platform.tasks.task-types.manage`, `TaskModels.cs:32`) — DL-4'te motoru çağıracak uçlar bunlardır; (2) motorun
  adım 4'ü aynı anahtarı katalogdan (`RequiredPermission`) `[HasPermission]`'ın kullandığı değerlendiriciyle (`IActorPermissionContext.Has`)
  sorar; boş anahtar kapalıdır. Katalog anahtarının controller anahtarıyla aynı olduğunu yansıma testi sabitler (AC-62, AC-48 emsali).
  Kilit (adım 4 yok) Bölüm 8.17'deki gerekçeyle. Aktör: doğrulanmış `tenant_user` (`OrdinalIgnoreCase`) ve dolu `sub`; platform aktörü
  ve `partner_admin` port savunmasında 403. Denetim aktörü `TenantUser` + `sub`, kaynak servis `Diten.Platform`. Veri kapsamı (SEC-002):
  motor kiracı çapında satır döndürmez — geçmiş sorgusu tek kaydın satırlarıdır; tüketici kaydının kapsamını tüketici uç denetler (DL-4).

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
- **DL-3 (r14):** Gateway değişikliği yok, yeni rota yok (Q-15 A önerisi); `ocelot.json`'a dokunulmaz. Motor yalnız süreç içidir;
  AC-21 (`record-codes` ya da `/internal` geçen rota yok) değişmeden yeşil kalır.

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
maddenin sabotajı maddenin içinde ve Bölüm 17 sabotaj listesinde (S-74…S-87; r13: S-88…S-92). "Q-n A" ile işaretli maddeler o
sorunun cevabına göre yazıldı; **r13: sahip A'yı seçti** (K-16), maddeler karardır. r13 bu maddelere yeni AC numarası açmadan
inceleme 04 bulgularının adımlarını ekledi (F-n işaretiyle).

- [ ] AC-48 (zorunlu, DL-2) — **Katalog:** `ORG` ve `POS`'un izinli kapsamı yalnız `LegalEntity`, `AllowedCaller = "Diten.Platform"`, `RequiredPermission` sırasıyla `platform.organization-units.create` / `platform.positions.create`; bu iki anahtar `OrganizationUnitsController` ve `PositionsController`'ın `[HttpPost]` eylemlerindeki `[HasPermission]` değeriyle birebirdir (yansıma); HTTP eşlemesi `TrustedRecordCodeCredentialAuthenticator.Consumers` `ORG` / `POS` içermez ve `Diten.Platform` dışındaki her katalog çağıranı eşlemeyle birebirdir; `TransactionOwnedAuditSourceServices.Names` üç ad olarak kalır. Testler `RecordCodeTypeCatalogTests.AC48_org_and_pos_allow_only_legal_entity_and_only_the_in_process_caller`, `RecordCodeTypeCatalogTests.AC48_the_catalogue_permission_is_the_controller_s_has_permission_key`; DL-1'in `AC09_…` ve `AC14_the_consumer_map…` testleri yalnız ORG / POS ve `Diten.Platform` için güncellenir.
  Sabotaj: `ORG` kapsamına `Group` eklenince kapsam testi; `POS` izni `platform.positions.update` yapılınca yansıma testi; `ORG` `Mdm` bağlamasına eklenince eşleme testi kırmızı (S-74, S-75).
- [ ] AC-49 (zorunlu, DL-2) — **HTTP yüzeyi değişmez:** iç uca her iki kimlik bilgisiyle ve `platform.organization-units.create` / `platform.positions.create` taşıyan kullanıcı token'ıyla `ORG` ya da `POS` gövdesi 403 `RECORD_CODE_FORBIDDEN` alır (sözleşme 1.0.0 §6), sayaç ve defter satırı oluşmaz, `Denied` ret satırı yazılır (`EvaluationStep = 7`); kabul kaydı `git diff` ile sözleşme dosyasının değişmediğini gösterir. Test `InternalRecordCodesHttpTests.AC49_org_and_pos_stay_forbidden_over_http_after_dl2`.
  Sabotaj: `ORG` `FinanceTreasury` bağlamasına eklenince test 200 alır ve kırmızı (S-75).
- [ ] AC-50 (zorunlu, DL-2) — **ORG oluşturma:** MDM kısa kodu `MYG` olan şirket için kiracı kullanıcısı kodsuz `POST /api/platform/organization-units` gönderir → 201; kayıt `Code = MYG-ORG-000001`, ikincisi `MYG-ORG-000002`; `GET` aynı kodu döner; defterde her biri için tek satır: `CallerService = Diten.Platform`, `IdempotencyKey = organization-unit:{kayıtId:N}`, `RecordType = ORG`, `PrefixScope = LegalEntity`, `LegalEntityId` = birimin şirketi, `TenantId` = token kiracısı; oluşturma yolunda `OrganizationCodeNormalizer` çağrılmaz. Test `OrganizationRecordCodeHttpTests.AC50_org_unit_create_takes_MYG_ORG_000001_then_000002` (gerçek HTTP, üretim `TenantResolutionMiddleware`, gerçek Mongo, sahte MDM).
  Sabotaj: işleyici kodu istekten / normalleştiriciden almaya döndürülünce kod adımı; anahtar kayıt kimliği yerine yeni Guid'den türetilince "defter anahtarı = kayıt kimliği" adımı kırmızı (S-76, S-77).
- [ ] AC-51 (zorunlu, DL-2) — **POS oluşturma ve şirket kaynağı:** pozisyonun şirketi oluşturma anındaki biriminin `LegalEntityId`'sidir: `MYG` şirketinin birimine açılan ilk pozisyon `MYG-POS-000001`, `GPL` şirketinin birimine açılan `GPL-POS-000001`; ORG ve POS sayaçları ayrıdır (pozisyon `MYG-ORG-…` sayacını ilerletmez); pozisyon `GPL` birimine taşınınca (`PUT`, saklı kodla) kod `MYG-POS-000001` kalır; birimin şirketi MDM'de aktif değilse 404 `LEGAL_ENTITY_NOT_FOUND`, pozisyon oluşmaz; defter anahtarı `position:{kayıtId:N}`. **r13 (inceleme 04 F-7):** testte kullanıcının bağlam şirketi (`ITenantAuthorizationContext.LegalEntityId`, `InitializeAsync`'ten sonra dolar) **birimin şirketinden farklıdır** (`GPL` kullanıcısı `MYG` birimine pozisyon açar → `MYG-POS-…`), böylece şirketi birim dışından alan uygulama (S-78) kırmızı verir; `PositionRequest`'te şirket alanı yoktur (`TenantOrganizationContracts.cs:51-64`). **r13 (F-11):** `platform.positions.create` taşıyan ama `mdm.legal-entities.read` taşımayan kullanıcı → 403 `LEGAL_ENTITY_READ_PERMISSION_MISSING` (zarfta), pozisyon yok, `platform.record-code.allocation-refused` satırı `Outcome = Denied`, `EvaluationStep = 11`. **r13 (F-2):** guard'dan sonra birim silinirse (ikinci okumayı saran test sarmalayıcısı `null` döner) → 404, ayırma gönderilmez, sayaç ilerlemez. Test `OrganizationRecordCodeHttpTests.AC51_position_takes_its_unit_s_company_prefix_and_keeps_it_after_a_move`, `…AC51_a_position_without_mdm_read_is_refused_and_allocates_nothing`.
  Sabotaj: şirket kaynağı birim dışına (istek, kullanıcının `LegalEntityId` iddiası) çevrilince ya da taşımada kod yeniden ayrılınca test kırmızı (S-78).
- [ ] AC-52 (zorunlu, DL-2) — **Oluşturmada kod kabul edilmez:** ORG ya da POS gövdesinde dolu `code` → `Response<T>` zarfında 400 `RECORD_CODE_SYSTEM_ASSIGNED` (`reason_code` dolu, `ProblemDetails` değil); sayaç belgesi ve defter satırı oluşmaz, kayıt yok; merkezi denetimde `CreateOrganizationUnitCommand` / `CreatePositionCommand` satırı `Outcome = Failed`; `code` yok ya da boş → AC-50 / AC-51 akışı. **r13 (inceleme 04 F-6):** 81 karakterlik (ve 500 karakterlik) `code` da zarfta 400 `RECORD_CODE_SYSTEM_ASSIGNED` + merkezi `Failed` alır (`ProblemDetails` değil); yalnız boşluk `code` (`"   "`) boş sayılır → 201, otomatik kod; doğrulayıcı yansıması: `CreateOrganizationUnitCommandValidator` / `CreatePositionCommandValidator` `Code` için kural taşımaz. Test `OrganizationRecordCodeHttpTests.AC52_a_create_that_carries_a_code_is_refused_and_allocates_nothing`.
  Sabotaj: dolu kodu sessizce yok sayan değişiklik konunca 201 alınır; ret doğrulayıcıya taşınınca `ProblemDetails` döner — iki adım da kırmızı (S-79).
- [ ] AC-53 (zorunlu, DL-2; Q-2 A) — **Kod değişmez:** ORG ve POS `PUT`'unda saklı koddan farklı `code` → 409 `RECORD_CODE_IMMUTABLE`, kayıt değişmez; saklı koda birebir eşit ya da boş `code` → 204 ve kod aynı; `PUT …/reporting-lines` ve tüm kaydı geri gönderen güncelleme kodu değiştirmez; ORG'nin şirketi değiştirilince kod aynı kalır (§5.2); kural eski (elle) kodlu kayıtlarda da geçerlidir (tohumlanmış `HEADQUARTERS`, `CEO`). **r13 (inceleme 04 F-6):** yalnız boşluk `code` → 204, kod aynı; saklı `CEO`'ya karşı `" CEO"` ya da `"ceo"` → 409 (normalleştirme yok); 81 karakterlik farklı `code` → zarfta 409 `RECORD_CODE_IMMUTABLE` (`ProblemDetails` değil). Test `OrganizationRecordCodeHttpTests.AC53_update_cannot_change_an_old_or_a_new_code`.
  Sabotaj: güncelleme işleyicisinde `entity.Code = canonicalCode` (`UpdateOrganizationUnitCommandHandler.cs:145` / `UpdatePositionCommandHandler.cs:49`) geri konunca test kırmızı (S-80).
- [ ] AC-54 (zorunlu, DL-2) — **Sıra ve başarısızlık:** (a) ayırma reddi — sahte MDM `code` kalıba uymuyor → 409 `LEGAL_ENTITY_SHORT_CODE_MISSING`; kiracı askıda → 403 `RECORD_CODE_TENANT_NOT_ACTIVE`; ayırma işlemi düşer → 503 `RECORD_CODE_UNAVAILABLE` — ORG / POS cevabı aynı durum ve `reason_code`, koleksiyonda kayıt yok, `audit_events`'te tek `platform.record-code.allocation-refused` satırı (`SourceService = Diten.Platform`) ve merkezi `Failed` oluşturma satırı; (b) ayırma başarılı, kayıt yazımı düşer (kayıt deposunu saran test sarmalayıcısı `CreateAsync`'te bir `MongoException` türevi atar — `InvalidOperationException` değil) → kayıt yok, defter satırı ve başarı denetim satırı kalır, sonraki oluşturma bir üst numarayı alır (boşluk); **r13 (inceleme 04 F-10):** HTTP cevabı **500 `ProblemDetails`**, başlık "Server Error", gövdede iç cümle ve `reason_code` yok (`GlobalExceptionHandler.cs:61-64`); `audit_events`'te merkezi `CreateOrganizationUnitCommand` / `CreatePositionCommand` satırı `Outcome = Failed`, `Metadata.AuditBehaviorOutcome = RuntimeException`, `Metadata.ExceptionType` = atılan tür (`AuditBehavior.cs:63-66`, `:192-206`) ve Tablo A satırı (kod boşluğu); işleyicinin uyarı logu kod, kayıt kimliği ve kiracıyı taşır ve istisnadan **önce** yazılır (Bölüm 8.14 log kalıbı); (c) doğrulama hatası (olmayan üst birim, döngü, olmayan birim) ayırmadan önce reddedilir, sayaç ilerlemez; (d) başarı yolunda denetim kapısı `TransactionOwnedAuditRefusedException` → 503 `ProblemDetails` `AUDIT_RECORD_UNAVAILABLE`, kayıt yok. Testler `OrganizationRecordCodeHttpTests.AC54a_…`, `AC54b_…`, `AC54c_…`, `AC54d_…`.
  Sabotaj: kayıt yazımı ayırmadan önceye alınınca (a)'nın "kayıt yok" adımı; ayırma reddinde normalleştirilmiş elle koda düşen yedek konunca (a); doğrulama ayırmadan sonraya taşınınca (c)'nin "sayaç ilerlemez" adımı kırmızı (S-81, S-82).
- [ ] AC-55 (zorunlu, DL-2; Q-1 A) — **Eski kodlar korunur, çakışma atlanır:** tohumlanmış eski kodlar (`HEADQUARTERS`, `CEO`) DL-2'den sonra aynen okunur; kiracıda `MYG-ORG-000001` kodlu **silinmiş** eski bir birim varken ilk ORG oluşturma `MYG-ORG-000002` alır, `000001` defterde ayrılmış ama kullanılmamış kalır, uyarı logu yazılır; `000001`…`000003` eski kayıtlarca tutuluyorsa 409 `RECORD_CODE_COLLISION`, kayıt yok. **r13 (inceleme 04 F-8):** tek atlamalı senaryoda defterde iki satır vardır — `000001` anahtar `organization-unit:{kayıtId:N}`, `000002` anahtar `organization-unit:{kayıtId:N}:2` — ve `audit_events`'te iki Tablo A satırı (aynı anahtar / kod çiftleriyle); kaydın kodu ikincidedir. Üç çakışmada defterde üç satır (anahtarlar `{taban}`, `{taban}:2`, `{taban}:3`), üç Tablo A satırı, sayaç 3 ilerler, kayıt yok, merkezi `Failed` satırı. DL-2 kabul kaydı her kiracı için desene (`^[A-Z]{2,5}-(ORG|POS)-[0-9]{6,}$`) uyan eski kod sayısını yazar. Test `OrganizationRecordCodeHttpTests.AC55_a_legacy_code_on_the_counter_s_path_is_skipped_even_when_soft_deleted`.
  Sabotaj: çakışma araması yalnız canlı satırlara (`ExistsByCodeAsync`) çevrilince silinmiş eski kodun numarası yeniden kullanılır ve "`000002`" adımı kırmızı; atlama kaldırılınca canlı eski kodlu senaryoda birim oluşmaz ve test kırmızı (S-83).
- [ ] AC-56 (zorunlu, DL-2) — **Kısa kod kilidi ORG / POS'ta:** (a) X şirketi `MYG` ile `PAY` (HTTP, FinanceTreasury kimlik bilgisi) ve `ORG` almışken MDM kodu `MYX`'e çevrilince X için ORG ve POS oluşturma 409 `LEGAL_ENTITY_SHORT_CODE_CHANGED`; kayıt yok, sayaç ilerlemez, `Failed` ret satırı (`EvaluationStep = 11`, `Prefix = MYX`), hata logu; kod `MYG`'ye geri alınınca ORG oluşturma `MYG-ORG-000002` ile sürer. **r13 (inceleme 04 F-4) — (b) ters yön:** Z şirketinde **hiç HTTP ayırması yokken** önce ORG (`MYZ-ORG-000001`), sonra MDM kodu `MZX`'e çevrilir, sonra iç uca `PAY` (FinanceTreasury) → 409 `LEGAL_ENTITY_SHORT_CODE_CHANGED` (`Response<T>` zarfı), kod yok, `Failed` ret satırı `SourceService = Diten.FinanceTreasuryService`, `Prefix = MZX`; aynı adım Mdm kimlik bilgisiyle `AGR` için de 409; kod `MYZ`'ye geri alınınca `PAY` `MYZ-PAY-000001` alır (kurtarma yolu, Bölüm 8.14). **(c) başka şirkette aynı önek:** Z'nin kodu yeniden `MZX` yapılıp serbest kalan `MYZ` W şirketine verilince W için ORG oluşturma ve `PAY` ikisi de 409 (`ExistsPrefixForOtherLegalEntityAsync`). Test `OrganizationRecordCodeHttpTests.AC56_the_interim_short_code_lock_stops_org_and_pos_creates`, `…AC56b_an_org_unit_first_then_an_mdm_change_stops_treasury`, `…AC56c_a_freed_prefix_given_to_another_company_stops_both`.
  Sabotaj: ORG işleyicisi 409'da normalleştirilmiş elle koda düşünce birim oluşur ve test kırmızı (S-81).
- [ ] AC-57 (zorunlu, DL-2) — **Kiracı izolasyonu:** A kiracısının kullanıcısı B kiracısının şirketiyle ORG ya da B kiracısının birimiyle POS isterse 404 (bugünkü doğrulama), ayırma gönderilmez; A ve B'nin `record_code_counters` ve `record_code_allocations` koleksiyonları doğrudan okunarak hiçbir satırın oluşmadığı gösterilir; **r13 (inceleme 04 F-3):** `audit_events`'te (iki kiracıda ve platform denetim kiracısında) `platform.record-code.allocation-refused` satırı **yoktur** — reddi ayırmadan önceki doğrulama verir; doğrulama ayırmanın arkasına kayarsa (S-82) ayırma adım 11'de MDM 404'üyle reddeder ve bir `Denied` ret satırı doğar (`AllocateRecordCodeHandler.cs:158-160`), test kırmızı olur; başarılı ayırmanın sayaç ve defter satırı `TenantId` = token kiracısıdır; port `ITenantContext.TenantId ≠ ITenantAuthorizationContext.TenantId` iken ayırma göndermeden 403 `RECORD_CODE_FORBIDDEN` döner. Testler `OrganizationRecordCodeHttpTests.AC57_cross_tenant_company_or_unit_is_404_and_allocates_nothing`, `InProcessRecordCodeAllocatorTests.AC57_a_contradicting_tenant_context_never_reaches_the_allocator`.
  Sabotaj: port kiracıyı şirketin / birimin satırından alınca birim testi; ORG şirket doğrulaması ayırmadan sonraya taşınınca "ayırma gönderilmez" adımı kırmızı (S-82, S-84).
- [ ] AC-58 (zorunlu, DL-2) — **Yetki:** `platform.organization-units.create` / `platform.positions.create` taşımayan kiracı kullanıcısı 403 alır (çerçeve, `[HasPermission]`), ayırma yok; **r13 (inceleme 04 F-3; ölçüldü):** HTTP testi çerçevenin reddini ayırt eder — zarfta `reason_code = PERM_DENIED` (`HasPermissionAttribute.cs:140-155`, `DocumentManagementContractModels.cs:31`), **`RECORD_CODE_PERMISSION_MISSING` değil**; `audit_events`'te `platform.record-code.allocation-refused` satırı **yok**; sayaç ve defter satırı yok. (İnceleme "zarfta `reason_code` yok" demişti; ölçüm `PERM_DENIED` gösterdi — ayrım bu değerle kurulur.) Port adım 8'i `IActorPermissionContext.Has` ile sorar — izin yoksa (birim testi, sahte bağlam) 403 `RECORD_CODE_PERMISSION_MISSING` ve `Denied` ret satırı; **r13 (F-9):** `HoldsRequiredPermission(null | "" | "  ", ctx)` her şeye `true` diyen sahte bağlamla bile `false`; `ActorType ≠ tenant_user` ya da `UserId = Guid.Empty` → ayırma göndermeden 403 `RECORD_CODE_FORBIDDEN`; `ActorType = "Tenant_User"` (yalnız harf büyüklüğü farkı) port'u geçer (ara katmanla aynı `OrdinalIgnoreCase`); `platform_admin` token'ı ORG / POS uçlarında ara katmanda reddedilir (bugünkü davranış, değişmez). Testler `InProcessRecordCodeAllocatorTests.AC58_*` (yeni: `AC58_a_blank_catalogue_permission_is_never_held`, `AC58_actor_type_is_compared_like_the_middleware`), `OrganizationRecordCodeHttpTests.AC58_a_user_without_the_create_key_allocates_nothing`.
  Sabotaj: port `HasRequiredPermission = true` sabitlenince izinsiz-bağlam birim testi; controller'dan `[HasPermission]` kaldırılınca HTTP 403 testi kırmızı (S-85).
- [ ] AC-59 (zorunlu, DL-2) — **Denetim (AUD-001):** başarılı ORG oluşturma `audit_events`'te iki satır bırakır: (1) merkezi `CreateOrganizationUnitCommand` (`Outcome = Succeeded`, `platform-merkezi`; bugünkü gibi) ve (2) Bölüm 21.3 Tablo A satırı: `RequestType = AllocateRecordCodeCommand`, `SourceService = Diten.Platform`, `ActorType = TenantUser`, `ActorId` = token `sub`, `AfterState.Code` = kaydın kodu, `AfterState.LegalEntityId` = birimin şirketi, `Metadata.IdempotencyKey = organization-unit:{kayıtId:N}`, `Metadata.GroupPrefixLockedNow = false`; POS için `position:{kayıtId:N}`; **r13 (inceleme 04 F-8):** bu iki iddia **atlamasız** oluşturma içindir — çakışma atlamasında başarı satırı sayısı deneme sayısıdır ve `AfterState.Code` = kaydın kodu yalnız **son** satırda, onun anahtarı `{taban}:{n}`'dir (AC-55); retler Bölüm 21.2'ye göre; `tests/architecture/audit-ledger/Diten.Platform.md` değişmez ve `AuditTrailStandardTests` değişmeden yeşildir (yeni komut, yeni belirteç yok). Test `OrganizationRecordCodeHttpTests.AC59_an_org_create_leaves_the_central_row_and_the_allocation_row`.
  Sabotaj: port `CallerService`'i kapalı küme dışı bir ad yapınca kapı `TransactionOwnedAuditIntentInvalidException` atar, kayıt oluşmaz ve test kırmızı; anahtar kayıt kimliği taşımayınca `IdempotencyKey` adımı kırmızı (S-77, S-86).
- [ ] AC-60 (zorunlu, DL-2; Q-3 A) — **Ekran:** ORG ve POS oluşturma formunda kod girişi salt okunur ve boştur, yükte `code` yoktur, zorunlu listede `ouCode` / `pCode` yoktur; düzenleme formunda kod salt okunur ve saklı değeri gösterir; `reason_code`'u Bölüm 11 listesinde olan cevapta yerelleştirilmiş metin gösterilir (`errors[]` değil); Bölüm 11'deki anahtarlar iki kaynak dosyasında **7 dilin her birinde** vardır — **r13 (inceleme 04 F-5):** `RecordCodeLegalEntitySourceUnavailable` ve `RecordCodeForbidden` dahil; 503 `LEGAL_ENTITY_SOURCE_UNAVAILABLE` ve 403 `RECORD_CODE_FORBIDDEN` cevabında da yerelleştirilmiş metin gösterilir, `errors[]` gösterilmez; **resx-check eşdeğeri:** her anahtarın `tr`, `fr`, `es`, `zh`, `ar`, `ru` değeri boş değildir ve `en` değerinden **farklıdır** (`localization-standard.md:14`); iki form `Layout = "_LayoutTenantShell"` satırını değişmeden taşır. Testler `frontend/Diten.Web/tests/organization-record-code-form.test.js` (yeni) ve `frontend/Diten.Web/tests/xmc-date-guard-screens.test.js` (zorunlu listeden `ouCode` / `pCode` çıkar).
  Sabotaj: kod girişi düzenlenebilir yapılınca; bir anahtar `ar` dosyasından silinince; `form.js` `code` göndermeye döndürülünce — yeni testin adlı adımı kırmızı (S-87).
- [ ] AC-61 (zorunlu, DL-2) — **Takım:** `dotnet build` Diten.Platform PASS; Platform'un tam test takımı filtresiz; DL-1 kayıt kodu testleri — `RecordCodeTypeCatalogTests`'teki iki ORG / POS testi dışında — ve AC-23 (donmuş sözleşme) değişmeden yeşil; MOD-0288 testlerindeki değişiklik yalnız Bölüm 5 "DL-2" listesindeki dosya ve satırlarda (kurucu + kod semantiği) ve kabul kaydında satır satır gerekçeli; Web node testleri yeşil; atlanan test yok; main'deki bilinen kırmızılar aynı gün main'de ölçülüp ayrıca yazılır. **r13 (inceleme 04 F-1):** kabul kaydı `git diff` ile gösterir: `IOrganizationUnitRepository` ve `IPositionRepository` arayüz gövdeleri değişmedi (yalnız aynı dosyalara dar arayüz eklendi); `Tasks/TaskTestDoubles.cs`, `Tasks/TaskOrganizationUnitScopeTests.cs`, `WorkAggregation/ApprovalStepContextTests.cs`, `Workflow/WorkflowCrossServiceTests.cs` ve `PositionReferenceGuard.cs` değişmedi; `InMemoryTenantOrganizationRepositories.cs`'teki değişiklik yalnız dar arayüzlerin uygulanmasıdır (K-17).
  Sabotaj: yeni bir testin `Skip` ile işaretlenmesi kabul kaydında kırmızı.

**DL-3 — İKİSİ DE motoru (r14).** Numaralar AC-61'den devam eder. Her madde **üretim koduyla** ölçülür: üretim kataloğu (`TTY`,
Q-8 A önerisi), üretim işleyicileri MediatR üzerinden, üretim depoları gerçek Mongo replica set'te, üretim `CanonicalTransactionalAuditOutboxWriter`
ve kutu işleyicisi; test sahtesi yalnız principal / izin bağlamında ve depoyu **saran** kaydedici / atlatıcı sarmalayıcıda (DL-1 AC-5
deseni — yarış bariyeri yok, sıralı ölçüm; r11 dersi). Testler Bölüm 17'deki sınıflardadır; her maddenin sabotajı maddenin içinde ve
Bölüm 17 sabotaj listesinde (S-93…S-113). "Q-n A önerisi" ile işaretli maddeler Bölüm 24.2'deki sorunun önerilen cevabına göre
yazıldı; sahip farklı seçerse madde yeni revizyonda değişir.

- [ ] AC-62 (zorunlu, DL-3; sabotaj S-93; Q-8 A önerisi) — **Katalog — `TTY`:** `RecordCodeTypeCatalog`'da `TTY` satırı: `Class = "İKİSİ DE"`, `OwnerModule = "MOD-0024"`, `AllowedScopes = [Group]`, `AllowedCaller = "Diten.Platform"`, `RequiredPermission = "platform.tasks.task-types.manage"`; bu anahtar `TasksController.CreateTaskType` ve `UpdateTaskType` eylemlerindeki `[HasPermission]` değeriyle birebirdir (yansıma, `HasPermissionAttribute`'ün taşıdığı değer okunur — sabit metin değil); katalogdaki her satırın `Class`'ı RCS-001 §3 tablosundaki sınıfla aynıdır (kural dosyası `RepoPaths` ile okunur; `PLN` §5.3); HTTP eşlemesi `TrustedRecordCodeCredentialAuthenticator.Consumers` `TTY` içermez ve iç uca FinanceTreasury ya da Mdm kimlik bilgisiyle ve `platform.tasks.task-types.manage` taşıyan token'la `TTY` istenince **403 `RECORD_CODE_FORBIDDEN`** döner, sayaç ve defter satırı yoktur. DL-1'in `AC16_the_catalogue_holds_every_automatic_row_of_RCS001_section_3_and_PLN_and_nothing_else` testi beklenen listeye `TTY`'yi ve "§3'teki sınıf" denetimini alır; `AC14_kalsin_ikisi_de_old_and_unknown_types_are_not_in_the_catalogue`'deki `TTY` satırı `PFL` olur — bu madde DL-1 AC-14'ün `TTY` örneğinin yerini alır (AC-14 metni değişmez; AC-9 / AC-48 emsali). Testler `RecordCodeTypeCatalogTests.AC62_tty_is_a_both_type_with_group_scope_and_only_the_in_process_caller`, `RecordCodeTypeCatalogTests.AC62_the_tty_permission_is_the_task_type_controller_key`, `RecordCodeTypeCatalogTests.AC62_every_catalogue_class_is_the_rule_s_class`, `InternalRecordCodesHttpTests.AC62_tty_over_http_is_forbidden`.
  Sabotaj: `TTY` sınıfı `OTOMATİK` yapılınca sınıf testi; `TTY` kapsamına `LegalEntity` eklenince kapsam testi; izin `platform.tasks.read` yapılınca yansıma testi; `TTY` `Mdm` bağlamasına eklenince HTTP testi (200 / 409 alır) kırmızı (S-93).
- [ ] AC-63 (zorunlu, DL-3; sabotaj S-94…S-96; RCS-001 §5.9, §5.10; Q-11 A, Q-12 A önerisi) — **Elle kodun biçimi ve otomatik desen yasağı:** `RecordCodeManualCode.Normalize` / `Validate` — `"  denetim-01 "` → `DENETIM-01`; `"ıslak"` → `ISLAK`; `"İzmir-Şube"` → `IZMIR-SUBE`; `"çğöşü"` → `CGOSU`; ret 400 `RECORD_CODE_MANUAL_FORMAT_INVALID`: `"Şube Açılışı"` (boşluk), `"A_B"`, `"AB--C"`, `"-AB"`, `"AB-"`, `"A"`, 41 karakter, `"AB€"`, tam genişlikli `"ＡＢ"`; 2 ve 40 karakter kabul; ret 400 `RECORD_CODE_PATTERN_RESERVED`: `"GMG-TTY-000050"`, `"gmg-tty-000050"` (normalleştirmeden sonra), `"GMG-TTY-1000000"` (7 hane), `"MYG-PAY-000001"`, `"PLN-000001"`, `"PLN-1234567"`; kabul (desen dışı): `"MYG-TTY-12345"` (5 hane), `"GMG-TT-000001"`, `"DENETIM"`. Desen yasağı `RecordCodeFormat.IsValidCode` ve `IsValidPlatformCode`'u çağırır — kalıp kopyası yoktur (7 haneli adım bunu davranışla ölçer, S-95). Aynı kurallar sahiplenmede (`ManualCode`) ve değişiklikte (`NewCode`) gerçek işleyiciden aynı neden koduyla döner ve `Failed` ret satırı yazar (adım 11). Testler `RecordCodeManualCodeTests.AC63_*`, `RecordCodeAssignmentMongoTests.AC63_a_reserved_or_malformed_code_is_refused_by_claim_and_change`.
  Sabotaj: desen yasağı kaldırılınca `RESERVED` adımları; yasak `RecordCodeFormat` yerine `[0-9]{6}` sabit hane sayılı bir kopyayla yazılınca `GMG-TTY-1000000` adımı; Türkçe eşlemesi kaldırılıp yalnız `ToUpperInvariant` bırakılınca `İzmir-Şube` adımı (`İ` ASCII olmaz) kırmızı (S-94, S-95, S-96).
- [ ] AC-64 (zorunlu, DL-3; sabotaj S-97, S-98; RCS-001 §2; Q-9 A, Q-10 A önerisi) — **Gerekçe zorunlu:** elle kodlu sahiplenmede ve her değişiklikte gerekçe kırpılmış 10–500 karakterdir: `null`, `""`, `"   "`, 9 karakter, 501 karakter → 400 `RECORD_CODE_REASON_REQUIRED`, sahiplik satırı yazılmaz, kod değişmez, `Failed` ret satırı (gerekçe metni ret satırında **yok**); 10 ve 500 karakter kabul; başarı satırında (Tablo E / F) `Metadata.Reason` = kırpılmış metin, başka dönüşüm yok (`"  Ekip kısaltmasına geçildi  "` → `"Ekip kısaltmasına geçildi"`, Türkçe harfler korunur); **sistem kodlu** sahiplenmede dolu gerekçe → 400 `RECORD_CODE_REQUEST_INVALID` (sessiz yok sayma yok). Testler `RecordCodeManualCodeTests.AC64_reason_bounds`, `RecordCodeAssignmentMongoTests.AC64_a_manual_code_or_a_change_without_a_reason_is_refused_and_written`.
  Sabotaj: gerekçe denetimi yalnız `IsNullOrWhiteSpace`'e indirilince 9 karakter adımı; sistem kodunda dolu gerekçe sessizce yok sayılınca 400 adımı; ret satırına gerekçe yazılınca "ret satırında gerekçe yok" adımı kırmızı (S-97, S-98).
- [ ] AC-65 (zorunlu, DL-3; sabotaj S-99, S-100) — **Öneri (önizleme) ayırmaz:** grup öneki `GMG`'ye hizalanmış kiracıda (etkin `OrgPrefix = GMG`) `PreviewAsync("TTY")` iki kez → ikisi de 200 `{ "suggestedCode": "GMG-TTY-000001", "isReserved": false }`; `record_code_counters`, `record_code_allocations`, `record_code_claims` ve `audit_outbox` doğrudan okunur — **hiç satır yok**, grup öneki ayarının `LockedAtUtc`'si boş kalır; ardından elle kodsuz sahiplenme `GMG-TTY-000001` alır; yeni önizleme `GMG-TTY-000002` der; önizlemeden sonra başka bir kayıt için sahiplenme araya girerse ilk kaydın sahiplenmesi `GMG-TTY-000003` alır (önizleme söz değil). **Parite:** grup öneki ayarı yokken önizleme ve sistem sahiplenmesi aynı 409 `GROUP_PREFIX_MISSING`; ayar `GMG`, `OrgPrefix` `GMX` iken aynı 409 `GROUP_PREFIX_MISMATCH` (iki ana makine, DL-1 AC-35 deseni). `IRecordCodeCounterRepository`'nin üyeleri değişmez — DL-1'in `AC20_no_store_can_change_remove_or_reissue_a_code` testi değişmeden yeşil; okuma `IRecordCodeCounterReader.PeekLastNumberAsync` üzerindendir ve kiracı yüklemine uyar. Testler `RecordCodeAssignmentMongoTests.AC65_a_preview_reserves_nothing`, `AC65_preview_and_claim_refuse_the_same_group_prefix_states`.
  Sabotaj: önizleme `NextAsync` çağırınca "sayaç belgesi yok" adımı; ayar yokken `OrgPrefix`'e düşen yedek eklenince parite adımı kırmızı (S-99, S-100).
- [ ] AC-66 (zorunlu, DL-3; sabotaj S-101) — **Sistem kodlu sahiplenme DL-1 ayırmasıdır:** `ClaimAsync(new("TTY", recordId, null, null))` → 200 `{ code: "GMG-TTY-000001", origin: "System", replayed: false }`; sarmalayıcı `ISender`'a giden tek `AllocateRecordCodeCommand`'ı kaydeder: `PrefixScope = Group`, `LegalEntityId = null`, `IdempotencyKey = "tty:{recordId:N}"`, `CallerService = "Diten.Platform"`; defterde bir satır (Tablo A, `SourceService = Diten.Platform`), `record_code_claims`'te bir `Active` / `System` satır (`Reason = null`, `RecordId` = kayıt), `audit_events`'te iki satır (Tablo A ve Tablo E). Aynı kayıt için tekrar → 200 aynı kod, `replayed: true`; defter, sahiplik ve denetim satır sayıları değişmez, sayaç ilerlemez. Retler sahiplik satırı yazmaz: grup öneki ayarı yok → DL-1 ayırması 409 `GROUP_PREFIX_MISSING` döner, motor aynı durum kodu ve `reason_code`'u değiştirmeden döner, ret satırı DL-1'in kendi `platform.record-code.allocation-refused` satırıdır (motor ikinci bir ret satırı yazmaz); kiracı askıda → motorun adım 1'i DL-1'e hiç gitmeden 403 `RECORD_CODE_TENANT_NOT_ACTIVE` ve motorun `Denied` ret satırı (sarmalayıcı `ISender` çağrısı 0). **İkinci işlem düşer:** sarmalayıcı `InsertAsync`'te bir kez `MongoException` türevi atar → 503 `RECORD_CODE_UNAVAILABLE`, sahiplik satırı yok, defter satırı var (boşluk); aynı kayıtla tekrar → 200 **aynı kod** (DL-1 adım 9), tek sahiplik satırı, defterde tek satır. Testler `RecordCodeAssignmentMongoTests.AC66_*`.
  Sabotaj: anahtar `RecordId` yerine yeni Guid'den türetilince "ikinci işlem düşer" adımı iki ayrı kod ve iki defter satırı görür; sahiplik satırı ayırmadan önce yazılınca ayırma reddi adımı bir sahiplik satırı görür — kırmızı (S-101).
- [ ] AC-67 (zorunlu, DL-3; sabotaj S-102, S-103; RCS-001 §5.8, §6; Q-13 A önerisi) — **Elle kodlu sahiplenme ve benzersizlik:** `ClaimAsync(new("TTY", r1, "denetim", "Ekip kısaltması kullanılıyor"))` → 200 `{ code: "DENETIM", origin: "Manual" }`; sayaç belgesi ve defter satırı **yok**; `record_code_claims`'te `Active` / `Manual` satır, `Reason` kırpılmış metin; `audit_events`'te tek Tablo E satırı. Aynı kayıt aynı kodla tekrar → 200 `replayed: true`, yeni satır yok; aynı kayıt farklı kodla → 409 `RECORD_CODE_ALREADY_ASSIGNED`. Başka kayıt (`r2`) `DENETIM` ister → 409 `RECORD_CODE_TAKEN`; `r1`'in kodu değişip `DENETIM` emekliye ayrıldıktan sonra `r2` yine `DENETIM` ister → 409 `RECORD_CODE_TAKEN` (emekli kod dolu). **Savunma E11000:** sarmalayıcı `r2`'nin `CodeExistsAsync` ön denetimini **bir kez** `false` döndürür; ekleme `ux_record_code_claims_tenant_type_code`'da E11000 alır; test, sarmalayıcının kaydettiği istisnanın `RecordCodeClaimConflictException` (`IndexName = ux_record_code_claims_tenant_type_code`) olduğunu ve iç istisnanın `MongoWriteException` + `ServerErrorCategory.DuplicateKey` olduğunu doğrular; cevap 409 `RECORD_CODE_TAKEN` (500 ya da 503 değil). Aynı yöntemle `…_tenant_type_record_active` E11000'i (aynı kayıt, farklı kod, ön denetim atlatılır) → 409 `RECORD_CODE_ALREADY_ASSIGNED`. Testler `RecordCodeAssignmentMongoTests.AC67_*`.
  Sabotaj: `CodeExistsAsync` filtresine `Status == Active` eklenince emekli kod adımı; unique index kısmi (`Status == Active`) yapılınca E11000 adımı E11000 alamaz; depodaki E11000 → `RecordCodeClaimConflictException` çevirisi kaldırılınca yürütücü `PlatformTransactionUnavailableException` üretir ve E11000 adımı 503 alır — kırmızı (S-102, S-103).
- [ ] AC-68 (zorunlu, DL-3; sabotaj S-104, S-105; RCS-001 §2, AUD-001 §3) — **Gerekçeli değişiklik ve değişiklik geçmişi:** sistem koduyla sahiplenmiş kayıtta `ChangeAsync(new("TTY", r1, "GMG-TTY-000001", "denetim", "Ekip kısaltmasına geçildi"))` → 200 `{ previousCode: "GMG-TTY-000001", code: "DENETIM" }`; eski satır `Retired`, `RetiredAtUtc` dolu, `RetiredBy` = token `sub`, `ReplacedByCode = DENETIM`; yeni satır `Active` / `Manual`, `Reason` dolu; `audit_events`'te tek Tablo F satırı: `RequestType = ChangeRecordCodeCommand`, `Operation = Update`, `EntityType = RecordCodeClaim`, `EntityId = r1`, `BeforeState.Code = GMG-TTY-000001`, `AfterState.Code = DENETIM`, `Metadata.Reason`, `Metadata.RecordType = TTY`, `ActorType = TenantUser`, `ActorId = sub`, `OccurredAtUtc` dolu, `SourceService = Diten.Platform`, `Outcome = Succeeded` — RCS-001 §2'nin beş öğesi (eski kod, yeni kod, gerekçe, kişi, zaman) tek satırda. İkinci değişiklik `DENETIM` → `DENETIM-2` aynı biçimde; `GetHistoryAsync("TTY", r1)` üç satırı `ClaimedAtUtc` sırasıyla döner (kod, köken, durum, gerekçe, kişi, zamanlar). Ret: `ExpectedCurrentCode` eski → 409 `RECORD_CODE_CONFLICT`; yeni = aktif → 400 `RECORD_CODE_UNCHANGED`; kaydın kendi emekli kodu `GMG-TTY-000001` → 409 `RECORD_CODE_TAKEN` (Q-14 A önerisi); sistem desenli yeni kod → 400 `RECORD_CODE_PATTERN_RESERVED`; hepsinde satırlar değişmez ve `Failed` ret satırı. **Atomiklik:** (a) sarmalayıcı yeni sahiplik eklemesinde bir kez `MongoException` türevi atar → 503 `RECORD_CODE_UNAVAILABLE`; eski satır hâlâ `Active` (emeklilik geri alındı), yeni satır yok, Tablo F yok; (b) sahte işlem içi yazıcı `false` döner → 503, aynı sonuç. Testler `RecordCodeAssignmentMongoTests.AC68_*`.
  Sabotaj: emeklilik ve yeni sahiplik ayrı işlemlere bölününce atomiklik adımı eski satırı `Retired` görür; Tablo F'ye `BeforeState` ya da `Metadata.Reason` yazılmayınca satır eşitliği adımı kırmızı (S-104, S-105).
- [ ] AC-69 (zorunlu, DL-3; sabotaj S-106; RCS-001 §6) — **OTOMATİK türün kodu bu motorla verilmez ve değişmez:** `ORG` ve `POS` için (çağıranı `Diten.Platform`, kullanıcıda türün izni var) sahiplenme (elle ve sistem), değişiklik ve kilit → 409 `RECORD_CODE_IMMUTABLE`; `record_code_claims`, `record_code_counters`, `record_code_allocations` satırı yok; `Failed` ret satırı (`EvaluationStep = 5`). Test `RecordCodeAssignmentMongoTests.AC69_an_automatic_type_cannot_be_claimed_changed_or_locked`.
  Sabotaj: adım 5 (sınıf denetimi) kaldırılınca değişiklik 404 `RECORD_CODE_NOT_ASSIGNED`, sistem sahiplenmesi 200 alır — kırmızı (S-106).
- [ ] AC-70 (zorunlu, DL-3; sabotaj S-107, S-108; RCS-001 §5.4 deseni) — **Kullanım kilidi:** `LockAsync("TTY", r1)` → 200 `{ lockedNow: true }`, `LockedAtUtc` / `LockedBy` dolu, tek Tablo G satırı (`BeforeState.LockedAtUtc = null`); ikinci kilit → 200 `{ lockedNow: false }`, `LockedAtUtc` **aynı**, yeni denetim satırı yok; kilitli koda değişiklik → 409 `RECORD_CODE_LOCKED`, satırlar değişmez, `Failed` ret satırı; **ön denetimden sonra gelen kilit:** sarmalayıcı değişikliğin `FindActiveAsync` ön okumasından hemen sonra aynı kayıtta kilidi yazar (sıralı, bariyersiz) → emeklilik filtresi (`LockedAtUtc == null`) eşleşmez, cevap 409 `RECORD_CODE_LOCKED`, eski satır `Active`; kilit kullanıcıda türün iznini **istemez** (izinsiz bağlamla 200), ama port savunmasına ve adım 1–3, 5'e tabidir; kaydı olmayan kilit → 404. `IRecordCodeClaimRepository`'nin yöntem adları tam olarak `CodeExistsAsync`, `FindActiveAsync`, `InsertAsync`, `ListHistoryAsync`, `LockIfUnlockedAsync`, `RetireIfActiveAsync` (yansıma; `Unlock`, `Delete`, `Remove`, `Replace`, `Update`, `Set` ile başlayan üye yok). Testler `RecordCodeAssignmentMongoTests.AC70_*`, `RecordCodeAssignmentMongoTests.AC70_the_claim_store_can_only_add_retire_and_lock`.
  Sabotaj: değişiklik işleyicisindeki kilit ön denetimi **ve** emeklilik filtresindeki `LockedAtUtc == null` kaldırılınca kilitli değişiklik adımı 200 alır; yalnız filtre kaldırılınca "ön denetimden sonra gelen kilit" adımı 200 alır; kilit güncellemesi `$ifNull` yerine düz `$set` olunca "`LockedAtUtc` aynı" adımı kırmızı (S-107, S-108).
- [ ] AC-71 (zorunlu, DL-3; sabotaj S-102) — **Şema:** `record_code_claims` `SchemaProfile.RecordCodes` ile kayıtlı, `TenantScopedEntity`; üç adlı index Bölüm 4.5'teki anahtarlarla (kiracı önce): `ux_record_code_claims_tenant_type_code` unique ve **kısmi değil**, `ux_record_code_claims_tenant_type_record_active` unique ve kısmi (`Status == Active`), `ix_record_code_claims_tenant_type_record_time`; DL-1'in `AC24_the_three_collections_carry_their_named_tenant_first_indexes` testi değişmeden yeşil. Test `RecordCodeAssignmentMongoTests.AC71_the_claim_collection_carries_its_named_tenant_first_indexes`.
  Sabotaj: kod index'i kısmi yapılınca ya da anahtardan `RecordType` çıkarılınca index adımı kırmızı (S-102 ile birlikte koşar).
- [ ] AC-72 (zorunlu, DL-3; sabotaj S-109) — **Kiracı izolasyonu:** A ve B kiracıları aynı elle kod `DENETIM`'i kendi kayıtlarına sahiplenir → ikisi de 200; A kullanıcısı B'nin `RecordId`'si için değişiklik, kilit ve geçmiş ister → 404 `RECORD_CODE_NOT_ASSIGNED` (gövde, olmayan kayıtla birebir aynı), `Denied` ret satırı **A'nın** kiracısında; B'nin satırları doğrudan okunarak değişmediği gösterilir. Depo kiracı yüklemi her yöntemde: `Guid.Empty`, platform bağlamı ve bağlam dışı kiracı → `RecordCodeTenantPredicateViolationException`, hiçbir şey okunmaz / yazılmaz. **Yüklemler ayrı ayrı sabotajlanır:** `FindActiveAsync`, `CodeExistsAsync`, `RetireIfActiveAsync`, `ListHistoryAsync` ve `PeekLastNumberAsync` filtresinden `TenantId` tek tek çıkarılınca ilgili adlı adım kırmızıdır (testler kodu değil **B'nin satırlarını** ve A'nın cevabını ölçer — DL-1 AC-31 deseni). Askıdaki kiracı: önizleme, sahiplenme (elle ve sistem), değişiklik, kilit ve geçmiş → 403 `RECORD_CODE_TENANT_NOT_ACTIVE`; yazma işlemlerinde `Denied` ret satırı; kural DL-1 adım 4 ile aynı cevabı verir (aynı askıdaki kiracıda `AllocateRecordCodeCommand` da 403 — parite). Testler `RecordCodeAssignmentMongoTests.AC72_*`.
  Sabotaj: yukarıdaki beş yüklem kaldırma (S-109); kiracı aktiflik denetimi kaldırılınca askıdaki kiracı adımı elle sahiplenmede 200 alır — kırmızı.
- [ ] AC-73 (zorunlu, DL-3; sabotaj S-110) — **Yetki ve port savunması:** port adım 4'ü `RecordCodeInProcessGuard.HoldsRequiredPermission(definition.RequiredPermission, IActorPermissionContext)` ile sorar: `platform.tasks.task-types.manage` yoksa önizleme ve geçmiş 403 `RECORD_CODE_PERMISSION_MISSING` (ret satırı yok — okuma), sahiplenme ve değişiklik 403 + `Denied` ret satırı; `HoldsRequiredPermission(null | "" | "  ", her şeye true diyen bağlam)` → `false`; `ActorType ≠ tenant_user`, `UserId = Guid.Empty` ya da `ITenantContext` kiracısı ≠ `ITenantAuthorizationContext.TenantId` → hiçbir şey göndermeden 403 `RECORD_CODE_FORBIDDEN` (sahte `ISender` çağrı sayısı 0); `ActorType = "Tenant_User"` geçer (`OrdinalIgnoreCase`); izinli çağıranı olmayan İKİSİ DE tür `PLN` → 403 `RECORD_CODE_FORBIDDEN` (adım 3), `Denied`. **Yeni izin anahtarı yok:** DL-3 kodunda `[HasPermission]` özniteliği eklenmez; PKS-001 izin kataloğu ve rol köprüsü değişmez (AC-75 `git diff` ölçümü); motorun sorduğu anahtar AC-62 yansıma testiyle `TasksController`'ın anahtarıdır. Testler `InProcessRecordCodeAssignmentsTests.AC73_*`, `RecordCodeAssignmentMongoTests.AC73_a_user_without_the_type_key_is_refused_and_written`.
  Sabotaj: `HoldsRequiredPermission` `permissions.Has(key)`'e indirgenince boş anahtar adımı; port `HasRequiredPermission = true` sabitleyince izinsiz adım; savunmadan kiracı çelişkisi kolu kaldırılınca `ISender` çağrı sayısı adımı kırmızı (S-110).
- [ ] AC-74 (zorunlu, DL-3; sabotaj S-111, S-112; AUD-001 §3, §4; DL-1 deseni) — **İşlem içi denetim:** Tablo E, F, G satırları iş yazımıyla **aynı Mongo işleminde** `RecordCodeAuditIntent.EnqueueClaimedAsync` / `EnqueueChangedAsync` / `EnqueueLockedAsync` → `ITransactionalAuditOutboxWriter.TryEnqueueAsync` ile yazılır; her biri `Intent` taşır (elle yük yok), kutu anahtarları `record-code:claimed:{claimId:N}`, `record-code:changed:{newClaimId:N}`, `record-code:locked:{claimId:N}` — istemci verisinden türemez; üretim kutu işleyicisiyle `audit_events`'e düşen satırlar Bölüm 21.5 tablolarıyla alan alan eşittir. Kapı `false` → 503, işlem iptal, iş satırı yok (AC-68 (b) ile aynı ölçüm, sahiplenme ve kilit için de). Retler `RecordCodeAuditIntent.EnqueueAssignmentRefusalAsync` → `ITransactionalAuditRefusalWriter` ile **ayrı kısa işlemde**, Tablo H'ye göre; `Outcome` Bölüm 21.2'nin tek kuralıyla — 403 ve 404 `Denied`, diğerleri `Failed`; ret yazıcısı `false` dönerse uyarı logu, cevap değişmez. Üç komut `IAuditableCommand` ve `ITransactionOwnedAuditCommand` değildir (yansıma); `AuditBehavior` onlar için merkezi satır yazmaz (başarılı bir değişiklikten sonra `audit_events`'te `RequestType = ChangeRecordCodeCommand` satırı **bir** tanedir); `TransactionOwnedAuditCommands.Authorized` değişmez. **Merge kapısı A-20:** dört yeni belirteç defterdedir ve `AuditTrailStandardTests` değişmeden yeşildir. Testler `RecordCodeAssignmentMongoTests.AC74_*`.
  Sabotaj: başarı satırı işlemden sonra (commit'ten sonra ayrı çağrıyla) yazılınca "kapı `false` → iş satırı yok" adımı sahiplik satırını görür; 404 ret satırı `Failed` yazılınca Tablo H adımı; `EntityId` kayıt kimliği yerine sahiplik kimliği yazılınca Tablo F adımı kırmızı (S-111, S-112).
- [ ] AC-75 (zorunlu, DL-3; sabotaj S-113) — **Takım ve değişmeyenler:** `dotnet build` Diten.Platform PASS; Platform'un tam test takımı filtresiz; DL-1 ve DL-2 kayıt kodu testleri — `RecordCodeTypeCatalogTests`'teki iki `TTY` satırı dışında — ve AC-23 (donmuş sözleşme) değişmeden yeşil; `Diten.Platform.Application` derlemesinde `ClaimRecordCodeCommand`, `ChangeRecordCodeCommand`, `LockRecordCodeCommand`, `PreviewRecordCodeQuery`, `GetRecordCodeHistoryQuery` için `IValidator<>` uygulaması yoktur (yansıma — AC-45 deseni; test `InProcessRecordCodeAssignmentsTests.AC75_the_assignment_requests_have_no_validator`) ve her ret `Response<T>` zarfında `reason_code` ile döner; atlanan test yok; main'deki bilinen kırmızılar aynı gün main'de ölçülüp ayrıca yazılır. Kabul kaydı `git diff` ile gösterir: Bölüm 5 "DL-3" **Değişmez** listesindeki dosyalar (MOD-0024 dosyaları, DL-2 dosyaları, sözleşme, denetim kapısı ve `TransactionOwnedAuditCommands.cs`, HTTP eşlemesi) değişmedi; DL-3 kodu `InProcessRecordCodeAllocator` / `IInProcessRecordCodeAllocator` adlarını anmaz (DL-1 tabanına uygulanabilir).
  Sabotaj: yeni bir testin `Skip` ile işaretlenmesi kabul kaydında kırmızı; DL-3 komutlarından birine FluentValidation doğrulayıcısı eklenince yansıma adımı kırmızı (S-113).

**DL-4…DL-10.** Kabul ölçütleri her dilimin kendi paket revizyonunda yeni numarayla eklenir (numaralar silinmez,
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
| `InProcessRecordCodeAllocatorTests` (N, **DL-2**) | birim (sahte `ISender`, sahte `ITenantContext` / `ITenantAuthorizationContext` / `IActorPermissionContext`) | AC-57 (kiracı savunması), AC-58 (izin, aktör; r13: boş katalog izni, `ActorType` karşılaştırması) |
| `OrganizationRecordCodeHttpTests` (N, **DL-2**) | gerçek HTTP + üretim `TenantResolutionMiddleware`, `AuditBehavior`, `CanonicalTransactionalAuditOutboxWriter`, `GlobalExceptionHandler` + gerçek Mongo + sahte MDM (kısa kodu test içinde değiştirilebilir — AC-56) + kayıt deposunu saran test sarmalayıcısı (AC-54 (b), `MongoException` türevi) + birim deposunu saran ikinci-okuma sarmalayıcısı (AC-51, r13 F-2) + FinanceTreasury / Mdm kimlik bilgili iç uç çağrısı (AC-56 (b), (c)) | AC-50…AC-59 |
| `TenantOrganizationRulesTests`, `OrganizationEnterpriseFieldsTests`, `OrganizationMatrixReportingTests` (M, **DL-2**, XMC) | birim | AC-61 (kurucu; kod semantiği AC-52 / AC-53'e çevrilir) |
| `organization-record-code-form.test.js` (N, **DL-2**), `xmc-date-guard-screens.test.js` (M, **DL-2**, XMC) | Web node | AC-60 |
| Tam takım (Platform + Web node) (**DL-2**) | CI | AC-61 |
| `RecordCodeTypeCatalogTests` (M, **DL-3**) | birim + yansıma (`TasksController` `[HasPermission]` değeri) + RCS-001 §3 okuma (`RepoPaths`) | AC-62 (DL-1'in `AC16_…` ve `AC14_kalsin_…` testleri yalnız `TTY` için) |
| `InternalRecordCodesHttpTests` (M, **DL-3**) | gerçek HTTP | AC-62 (`TTY` → 403) |
| `RecordCodeManualCodeTests` (N, **DL-3**) | birim (Domain kuralı) | AC-63, AC-64 |
| `RecordCodeAssignmentMongoTests` (N, **DL-3**) | gerçek Mongo replica set + üretim işleyicileri MediatR üzerinden + üretim `RecordCodeClaimRepository` / `RecordCodeCounterRepository` + üretim `CanonicalTransactionalAuditOutboxWriter` ve kutu işleyicisi + `ISender` kaydedicisi + sahiplik deposunu **saran** kaydedici / atlatıcı / hata enjekte eden sarmalayıcı (bariyer yok, sıralı) + iki ana makine (`OrgPrefix` `GMG` / `GMX`, AC-65 paritesi) | AC-63 (işleyici retleri), AC-64, AC-65…AC-72, AC-73 (yazma retleri), AC-74 |
| `InProcessRecordCodeAssignmentsTests` (N, **DL-3**) | birim (sahte `ISender` çağrı sayımı, sahte `ITenantContext` / `ITenantAuthorizationContext` / `IActorPermissionContext`) + yansıma (doğrulayıcı yok) | AC-73, AC-75 (doğrulayıcı yok) |
| Tam takım (Platform) + `AuditTrailStandardTests` (A-20 sonrası) (**DL-3**) | CI | AC-74 (defter), AC-75 |

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
| S-78 | (r12, DL-2; r13 F-7) POS şirketi birim dışından alınır — `PositionRequest`'te şirket alanı olmadığından tek aday kullanıcının bağlam şirketidir (`ITenantAuthorizationContext.LegalEntityId`) / taşımada kod yeniden ayrılır | `OrganizationRecordCodeHttpTests`: AC-51 (bağlam şirketi `GPL`, birim `MYG` → `MYG-POS-…` beklenir) |
| S-79 | (r12, DL-2) Oluşturmada dolu kod sessizce yok sayılır / ret doğrulayıcıya taşınır | `OrganizationRecordCodeHttpTests`: AC-52 (201 / `ProblemDetails`) |
| S-80 | (r12, DL-2; r13 inceleme 04 F-7 ile genişledi) Güncelleme işleyicisinde `entity.Code = canonicalCode` geri konur **ya da** 409 `RECORD_CODE_IMMUTABLE` denetimi kaldırılır (farklı kod sessizce yok sayılır, 204) | `OrganizationRecordCodeHttpTests`: AC-53 "kod aynı" adımı / "farklı kod → 409" adımı |
| S-81 | (r12, DL-2) Kayıt yazımı ayırmadan önceye alınır / ayırma reddinde normalleştirilmiş elle koda düşülür | `OrganizationRecordCodeHttpTests`: AC-54 (a) "kayıt yok", AC-56 |
| S-82 | (r12, DL-2) Koddan bağımsız doğrulama ayırmadan sonraya taşınır | `OrganizationRecordCodeHttpTests`: AC-54 (c) "sayaç ilerlemez", AC-57 "ayırma ret satırı yok" adımı (r13 F-3: yalnız "sayaç / defter satırı yok" bu sabotajda yeşil kalırdı — adım 11 reddi sayaçtan önce gelir) |
| S-83 | (r12, DL-2) Çakışma araması yalnız canlı satırlara bakar / atlama kaldırılır | `OrganizationRecordCodeHttpTests`: AC-55 "`000002`" adımı / canlı eski kod senaryosu |
| S-84 | (r12, DL-2; r13 inceleme 04 F-7 ile daraltıldı) Port `ITenantContext` ile `ITenantAuthorizationContext` kiracı çelişkisini yok sayar (port imzası `InProcessRecordCodeRequest(RecordType, LegalEntityId, RecordId)` kiracı taşımadığı için "kiracıyı satırdan alır" kolu uygulanamazdı ve kaldırıldı) | `InProcessRecordCodeAllocatorTests`: AC-57 |
| S-85 | (r12, DL-2) Port `HasRequiredPermission = true` sabitler / controller'dan `[HasPermission]` kaldırılır | `InProcessRecordCodeAllocatorTests`: AC-58 izinsiz bağlam; `OrganizationRecordCodeHttpTests`: AC-58 HTTP — r13 F-3: `[HasPermission]` kaldırılınca cevap yine 403 olur ama `reason_code` `PERM_DENIED` yerine `RECORD_CODE_PERMISSION_MISSING` olur ve bir `allocation-refused` satırı doğar; test bu iki adımda kırmızı |
| S-86 | (r12, DL-2) Port `CallerService`'i kapalı küme dışı bir ad yapar | `OrganizationRecordCodeHttpTests`: AC-59 (kapı istisnası, kayıt yok) |
| S-87 | (r12, DL-2; r13 F-5) Form kod girişi düzenlenebilir yapılır / bir anahtar `ar` dosyasından silinir / `form.js` `code` gönderir / `RecordCodeLegalEntitySourceUnavailable`'ın `fr` değeri `en` değeriyle aynı yapılır / `RecordCodeForbidden` eşlemesi `form.js`'ten çıkarılır | `organization-record-code-form.test.js`: AC-60 adlı adımlar |
| S-88 | (r13, inceleme 04 F-9) `HoldsRequiredPermission` `permissions.Has(key)`'e indirgenir (boş anahtar `true` olur) | `InProcessRecordCodeAllocatorTests`: `AC58_a_blank_catalogue_permission_is_never_held` |
| S-89 | (r13, inceleme 04 F-4) Kısa kod kilidi sorgusuna `RecordType` filtresi eklenir (kilit türe bağlı yapılır) | `OrganizationRecordCodeHttpTests`: AC-56 (b) "ORG önce, sonra `PAY` 409" adımı (`PAY` 200 alır) |
| S-90 | (r13, inceleme 04 F-6) Paylaşılan doğrulayıcıya `RuleFor(…Code).MaximumLength(80)` geri konur | `OrganizationRecordCodeHttpTests`: AC-52 81 karakter adımı (`ProblemDetails`, merkezi satır yok), AC-53 81 karakter adımı |
| S-91 | (r13, inceleme 04 F-10) Kayıt yazımı istisnası işleyicide yutulup `Response<Guid>.Fail(…)` zarfına çevrilir / uyarı logu kaldırılır | `OrganizationRecordCodeHttpTests`: AC-54 (b) "500 `ProblemDetails`" / "`RuntimeException` merkezi satırı" / log adımı |
| S-92 | (r13, inceleme 04 F-8) Çakışma atlaması aynı anahtarı (`{taban}`) yeniden kullanır | `OrganizationRecordCodeHttpTests`: AC-55 "`000002`" adımı (adım 9 aynı kodu geri verir, üç denemede 409) ve "anahtar `{taban}:2`" adımı |
| S-93 | (r14, DL-3) `TTY` sınıfı `OTOMATİK` yapılır / kapsamına `LegalEntity` eklenir / izni `platform.tasks.read` yapılır / `TTY` `Mdm` bağlamasına eklenir | `RecordCodeTypeCatalogTests`: AC-62 sınıf, kapsam, yansıma adımları; `InternalRecordCodesHttpTests`: `AC62_tty_over_http_is_forbidden` |
| S-94 | (r14, DL-3) Otomatik desen yasağı kaldırılır | `RecordCodeManualCodeTests`: AC-63 `RESERVED` adımları; `RecordCodeAssignmentMongoTests`: `AC63_a_reserved_or_malformed_code_is_refused_by_claim_and_change` |
| S-95 | (r14, DL-3) Desen yasağı `RecordCodeFormat` yerine sabit 6 haneli kopya kalıpla yazılır | `RecordCodeManualCodeTests`: AC-63 `GMG-TTY-1000000` adımı |
| S-96 | (r14, DL-3) Türkçe harf eşlemesi kaldırılır (yalnız `ToUpperInvariant`) | `RecordCodeManualCodeTests`: AC-63 `İzmir-Şube` adımı |
| S-97 | (r14, DL-3) Gerekçe denetimi `IsNullOrWhiteSpace`'e indirgenir | `RecordCodeManualCodeTests`: `AC64_reason_bounds` 9 karakter adımı |
| S-98 | (r14, DL-3) Sistem kodunda dolu gerekçe sessizce yok sayılır / ret satırına gerekçe metni yazılır | `RecordCodeAssignmentMongoTests`: AC-64 "400 `RECORD_CODE_REQUEST_INVALID`" / "ret satırında gerekçe yok" adımı |
| S-99 | (r14, DL-3) Önizleme `NextAsync` çağırır | `RecordCodeAssignmentMongoTests`: `AC65_a_preview_reserves_nothing` "sayaç belgesi yok" adımı |
| S-100 | (r14, DL-3) Önizleme ayar yokken etkin `OrgPrefix`'e düşer | `RecordCodeAssignmentMongoTests`: `AC65_preview_and_claim_refuse_the_same_group_prefix_states` |
| S-101 | (r14, DL-3) Sistem sahiplenmesinin ayırma anahtarı `RecordId` yerine yeni Guid'den türer / sahiplik satırı ayırmadan önce yazılır | `RecordCodeAssignmentMongoTests`: AC-66 "ikinci işlem düşer" adımı (iki kod) / ayırma reddi adımı (sahiplik satırı var) |
| S-102 | (r14, DL-3) `CodeExistsAsync` yalnız `Active` satırlara bakar / kod index'i `Status == Active`'e kısmi yapılır | `RecordCodeAssignmentMongoTests`: AC-67 emekli kod adımı ve E11000 adımı; AC-71 index adımı |
| S-103 | (r14, DL-3) Depodaki E11000 → `RecordCodeClaimConflictException` çevirisi kaldırılır | `RecordCodeAssignmentMongoTests`: AC-67 savunma E11000 adımı (503 alır) |
| S-104 | (r14, DL-3) Emeklilik ve yeni sahiplik ayrı işlemlerde yazılır | `RecordCodeAssignmentMongoTests`: AC-68 atomiklik adımı (a) |
| S-105 | (r14, DL-3) Tablo F'ye `BeforeState.Code` ya da `Metadata.Reason` yazılmaz | `RecordCodeAssignmentMongoTests`: AC-68 Tablo F eşitliği adımı; AC-74 |
| S-106 | (r14, DL-3) Sınıf denetimi (adım 5) kaldırılır | `RecordCodeAssignmentMongoTests`: `AC69_an_automatic_type_cannot_be_claimed_changed_or_locked` |
| S-107 | (r14, DL-3) Kilit ön denetimi ve emeklilik filtresindeki `LockedAtUtc == null` kaldırılır / yalnız filtre kaldırılır | `RecordCodeAssignmentMongoTests`: AC-70 kilitli değişiklik adımı / "ön denetimden sonra gelen kilit" adımı |
| S-108 | (r14, DL-3) Kilit güncellemesi `$ifNull` yerine düz `$set` | `RecordCodeAssignmentMongoTests`: AC-70 "`LockedAtUtc` aynı" adımı |
| S-109 | (r14, DL-3) `FindActiveAsync`, `CodeExistsAsync`, `RetireIfActiveAsync`, `ListHistoryAsync`, `PeekLastNumberAsync` filtresinden `TenantId` tek tek çıkarılır (beş ayrı koşu) | `RecordCodeAssignmentMongoTests`: AC-72 adlı yüklem adımları |
| S-110 | (r14, DL-3) `HoldsRequiredPermission` `permissions.Has(key)`'e indirgenir / port `HasRequiredPermission = true` sabitler / savunmadan kiracı çelişkisi kolu kaldırılır | `InProcessRecordCodeAssignmentsTests`: AC-73 boş anahtar, izinsiz bağlam, `ISender` çağrı sayısı adımları |
| S-111 | (r14, DL-3) Başarı denetim satırı commit'ten sonra ayrı çağrıyla yazılır | `RecordCodeAssignmentMongoTests`: AC-74 "kapı `false` → iş satırı yok" adımı |
| S-112 | (r14, DL-3) 404 `RECORD_CODE_NOT_ASSIGNED` reddi `Failed` yazılır / Tablo F `EntityId` = sahiplik kimliği | `RecordCodeAssignmentMongoTests`: AC-74 Tablo H adımı / Tablo F adımı |
| S-113 | (r14, DL-3) Yeni bir test `Skip` ile işaretlenir / DL-3 komutuna FluentValidation doğrulayıcısı eklenir | kabul kaydı (AC-75); `InProcessRecordCodeAssignmentsTests.AC75_the_assignment_requests_have_no_validator` |

Diğer: kiracı izolasyonu gerçek HTTP + gerçek Mongo ile; soft delete yok; tarayıcı smoke yok; RESX yok; DataTable
doğrulayıcısı uygulanmaz; Mongo testleri Platform test sabitleriyle kendi replica set'ini kullanır.

## 18. Ready-for-dev Checklist

- [x] Golden Reference `none`; arka uç düzeni Golden Reference'a birebir (Bölüm 10).
- [x] Künye alanları dolu; `approval_status: pending`; `code_start` yok; `revision: r13`; künye `status: ready-for-dev` (r13 — Q-6
  kararı A, K-16: bağımsız inceleme 04 sonrası künye `ready-for-dev`, onay satırı bu sürümün sha256'sına yazılır; r12 `draft`'tı;
  r11 `ready-for-dev` künyesiyle onaylıydı). S-49: draft künyeli sürüme yazılan onay satırı kodu yetkilendirmez — r13 bu koşulu
  karşılar; **onay satırı yazılmadı**. **r14:** `revision: r14`, künye `ready-for-dev`, `approval_status: pending`, `code_start`
  DL-3 için r14 onayını anar; r13'ün onay satırları yazıldı (`execution/approvals/approvals.tsv` satır 73 / 74, sha256 `325e0624`);
  **r14 onay satırı yazılmadı**.
- [x] Layout & Shell: `shell: none` (Bölüm 9). Validation (Bölüm 12), Failure Path (Bölüm 13), Authorization (Bölüm 14),
  Gateway (Bölüm 15), Lookup kararı (Bölüm 7.1), Audited Events (Bölüm 21) yazılı.
- [x] Kimlik ön kontrolü `OK`, çıkış 0 (Bölüm 0).
- [x] `pack_approval.slices()` DL listesi döndürür — `PYTHONUTF8=1 python3 -P -c "import sys; sys.path.insert(0,'scripts'); import pack_approval as p; t=open('execution/domains/platform-shared-services/module-packs/MOD-0040-FU02-record-code-service.md',encoding='utf-8').read(); print(p.slices(t)); print(p.kunye(t)['status'])"` → `['DL-1', … 'DL-10']` ve `ready-for-dev` (r11, 2026-11-02 bu dosya üzerinde); **r12** (2026-11-10, bu dosya üzerinde) →
  `['DL-1', … 'DL-10']` ve `draft` (`approval_status` `pending`); **r13** (2026-11-10, bu dosya üzerinde, `91d72310e` çalışma kopyası) →
  `['DL-1', … 'DL-10']` ve `ready-for-dev` (Bölüm 19.5); **r14** (2026-11-13, bu dosya üzerinde, `8931275d2` çalışma kopyası) →
  `['DL-1', 'DL-2', 'DL-3', 'DL-4', 'DL-5', 'DL-6', 'DL-7', 'DL-8', 'DL-9', 'DL-10']` ve `ready-for-dev`.
- [x] İnceleme 01 bulguları (Bölüm 19.1), inceleme 02 bulguları F-1…F-11 (Bölüm 19.2), inceleme 03 tur 1 bulguları
  F-1…F-10, tur 2 bulguları F-1…F-5, tur 3 bulgusu F-1, tur 4 bulguları F-1…F-2, tur 5 bulguları F-1…F-2, tur 6 bulgusu F-1, bağımsız son bakış 01 tur 1 bulguları F-1…F-12, tur 2 bulguları F-1…F-9 tur 3 bulguları F-1…F-7, tur 4 bulguları F-1…F-7 ve tur 5 bulguları F-1…F-3, inceleme 03 tur 8 bulgusu F-1 (Bölüm 19.3) işlendi; **r13:** inceleme 04 tur 1 bulguları F-1…F-13 (Bölüm 19.5) işlendi.
- [x] Açık kararlar A-1, A-11, A-12, A-16, A-17 ve sözleşme dondurma **ready-for-dev önkoşulu değildir** — CT kararı (MVP-1 CT,
  2026-11-02) ile Bölüm 18.1'deki DL-1 kabul / merge kapılarına taşındı (her biri sahip ve tarihle). (r4, inceleme 03 tur 1 F-5:
  A-18 de buradan çıkarılmıştı — DL-5'in kararıdır, Bölüm 18.1.)
- [ ] CT-APR-02 doğrulama incelemesi — **her revizyonun kendi incelemesi zorunludur** (SOP §20.4/1; r10, son bakış 01 tur 4 F-2):
  r8 → inceleme 03 tur 6 + son bakış 01 tur 3 (UYGUN); r9 → inceleme 03 tur 7 (inceleme satırı `3ceb760e8`) + son bakış 01 tur 4
  (UYGUN, `132b9944d`) ve onay `466aacfc9`; r10 → inceleme 03 tur 8 (KOŞULLU, 1 ORTA; satır `4288ae40e`) + son bakış 01 tur 5
  (KOŞULLU, 1 ORTA, 2 DÜŞÜK; satır `28c4449cf`), onay yok; **r11 → inceleme 03 tur 9 + son bakış 01 tur 6, ikisi de UYGUN**
  (`docs/records/reviews/MOD-0040-FU02/2026-11-02-62f352de.md`, `…-62f352de-son-bakis.md`; `execution/approvals/reviews.tsv`'te r11
  sha256 `62f352de`'li iki satır — kod dalında `f73964c6e` `:286-287`, bu dalda `91d72310e` `:203-204`) ve **onaylı**; **r12 →**
  inceleme 04 tur 1 **KOŞULLU** (0 ENGEL, 4 ORTA, 9 DÜŞÜK; `2026-11-10-d29f9c5e.md`; `reviews.tsv` `f73964c6e` `:323` / `91d72310e`
  `:244`), onay yok; **r13 → kendi incelemesi henüz yok** (inceleme 04 tur 2 ya da CT'nin belirlediği eşdeğeri); açık ENGEL ve
  ORTA yok koşuluyla. Tur numarası bulgu dosyası adındaki hedef özetle eşleşir. (r13, inceleme 04 F-12: r12'deki "r11 → kendi
  incelemesi henüz yok" satırı eskimişti.)
- [ ] CT-APR-01 onay satırı (yüksek risk: iç uç, kiracı, şema, kimlik bilgisi, denetim altyapısı; paketi yazmamış `high: evet` onaylayıcı) — r9 için yazıldı (`466aacfc9`, sha256 `5fed433c`); r10 onaylanmadı; **r11 onaylı** (`approvals.tsv`'te sha256 `62f352de`'li satır — `f73964c6e` `:61`, `91d72310e` `:44`); r12 onaylanmadı (`draft`, KOŞULLU); **r13 için onay satırı gerekir** (onay sürümün sha256'sına bağlıdır; r13'ün incelemesinden sonra); `approval_status` onay satırıyla güncellenir. (r13, inceleme 04 F-12: r12'deki "r11 için yeni onay satırı gerekir" eskimişti.)
- [x] **DL-2 (r12):** A-8 kapandı (K-15); süreç içi port (Bölüm 8.14); mevcut kodlar ve değişmezlik (Bölüm 8.15); ekran / API
  davranışı (Bölüm 11, 12); AC-48…AC-61; S-74…S-87; dosya listesi ve XMC-001 satırları (Bölüm 5 "DL-2"); denetim (Bölüm 21.4);
  sözleşme 1.0.0 değişmedi; kimlik ön kontrolü r12 için çıkış 0; **r13 için yeniden koşuldu** (2026-11-10, `91d72310e` çalışma kopyası,
  Python 3.12.10) → `OK  MOD-0040-FU02: proven against Blueprint/registry.`, çıkış 0.
- [x] **DL-2 soruları** (Bölüm 24.1) **kapandı (r13, K-16):** Q-1 A, Q-2 A, Q-3 A, Q-4 A, Q-5 teyit, Q-6 A, Q-7 onay
  (`docs/records/decisions/2026-11/mod0040-fu02-owner-decisions-03.md`); seçilen cevaplar önerilenle aynı olduğundan AC-53, AC-55,
  AC-60 öz olarak değişmedi. İnceleme 04 kararları (K-17, `…-04.md`) işlendi.
- [ ] **r13 CT-APR-02 bağımsız incelemesi** (her revizyonun kendi incelemesi, SOP §20.4/1) ve **CT-APR-01 onay satırı** — r13 için
  henüz yok; DL-2 kodu ikisini bekler. (r12'nin incelemesi yapıldı — inceleme 04 tur 1, KOŞULLU; r12 onaylanmadı.) **r14 notu:** bu
  madde r13 yazılırken doğruydu; r13 sonradan inceleme 04 tur 2 UYGUN aldı ve onaylandı (`execution/approvals/approvals.tsv` satır 73
  / 74 — satırların gerekçe metni), DL-2 kodu yazılıp doğrulandı (Bölüm 23).
- [x] **DL-3 (r14):** kapsam (Bölüm 2.5), sahiplik defteri (Bölüm 4.5), motor ve değerlendirme sırası (Bölüm 8.17), dosya listesi
  (Bölüm 5 "DL-3"; XMC-001 satırı yok), AC-62…AC-75, S-93…S-113, denetim Tablo E–H (Bölüm 21.5), açık sorular Q-8…Q-16 (Bölüm 24.2),
  açık karar A-20; sözleşme 1.0.0 değişmedi; kimlik ön kontrolü r14 için çıkış 0 (Bölüm 0).
- [ ] **r14 CT-APR-02 bağımsız incelemesi** (açık ENGEL ve ORTA yok) ve **r14 sha256'sına yazılmış CT-APR-01 onay satırı** — yok;
  DL-3 kodu ikisini bekler. Q-8…Q-16'nın sahip cevabı da DL-3'ün başlama kapısıdır (Bölüm 18.1); cevap öneriden farklıysa yeni revizyon.
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
| DL-5 **başlama kapısı** | A-18 kararı (MDM kalıcı kısa kod kilidi) ve bağlı paket kimliği (Bölüm 7, DL-5 bağımlılığı) | DL-5 paket revizyonundan önce; hedef 2026-11-06 — DL-1'i bekletmez, o güne kadar Bölüm 8.13 ara dönem azaltıcısı geçerli. **r13:** aynı karar DL-2'nin canlıya alma kapısıdır (aşağıda) | MDM sahibi (Ali) + Beste |
| DL-2 **başlama kapısı** (r12) | A-8 kararı | **kapandı** — K-15, 2026-11-10 | Beste |
| DL-2 **başlama kapısı** (r12) | Q-1, Q-2, Q-3, Q-4 sahip kararı işlendi (seçim öneriden farklıysa yeni revizyon) | **kapandı** — K-16 (Q-1 A, Q-2 A, Q-3 A, Q-4 A; 2026-11-10), r13'te işlendi | Beste |
| DL-2 **başlama kapısı** (r12) | Q-7 XMC-001: MOD-0288 sahibinin Bölüm 5 "DL-2" XMC satırlarına onayı; MOD-0288-FU03 / FU04 ile sıra | **onay kapandı** — K-16 (Q-7, merkez sahibi Beste) + K-17 ek onayı (`InMemoryTenantOrganizationRepositories.cs`); **açık kalan:** FU03 / FU04 ile sıra — DL-2 WP'si yazılmadan CT ölçer | Beste (onay, kapandı); CT (sıra) |
| DL-2 **başlama kapısı** (r12; r13) | r13'ün CT-APR-02 incelemesi (açık ENGEL / ORTA yok) ve r13 sha256'sına yazılmış CT-APR-01 onay satırı; künye `ready-for-dev` (r13'te sağlandı — Q-6 A, S-49) | DL-2 kodundan önce | CT + Beste |
| DL-2 **kabul kapısı** (r12) | Kiracı başına desene uyan eski ORG / POS kodu sayımı (AC-55) kabul kaydında; bir (kiracı, önek, tür) için sayım 3'ü aşarsa o kiracıda DL-2 devreye alınmadan CT'ye bildirim (DL-6) | DL-2 kabul kaydı | CT |
| DL-2 **canlıya alma kapısı** (r13; inceleme 04 F-4, sahip kararı K-17) | **A-18 kararı** (MDM kalıcı kısa kod kullanım kilidi, sözleşme N-12) verilmiş ve kayda geçmiş olmalı. Gerekçe: DL-2'den sonra organizasyon birimi açılmış her şirket ara dönem kilidine girer; MDM'de kısa kod değişikliği o şirketin **bütün** LegalEntity kayıt kodlarını (Treasury `PAY` / `APD` / `ARI` / `CPS` / `CTP`, MDM `BAC` / `AGR`, ORG / POS) 409 `LEGAL_ENTITY_SHORT_CODE_CHANGED`'e düşürür; serbest kalan kodu alan başka şirket de durur (Bölüm 8.14 "iki yön"). **Bildirim:** canlıya almadan önce CT, Treasury CT'ye ve MDM sahibine (Ali) iki yönü, "aynı önek" durumunu, TC-8 metnini, kurtarma yolunu (MDM kodunu defterdeki son öneke geri almak) ve A-18 kararını yazılı bildirir; bildirim kaydı DL-2 kabul kaydına bağlanır. Kod yazımını bekletmez (K-17); merge'ün ayrıca bekleyip beklemediği sahip kararında yazılı değildir — **CT kararı, açık** (main'e giren kodun canlıya çıkış biçimine bağlı) | DL-2 canlıya alınmadan önce; merge için CT kararı açık | A-18: MDM sahibi (Ali) + Beste; bildirim: CT |
| DL-3 **başlama kapısı** (r14) | DL-1 kodu main'de | **sağlandı** — `origin/main`'de DL-1 kodu var (ölçüldü 2026-11-13; CT kabulü 2026-11-04). DL-3 DL-2'yi beklemez (Bölüm 2.5) | CT |
| DL-3 **başlama kapısı** (r14) | r14'ün CT-APR-02 incelemesi (açık ENGEL / ORTA yok) ve r14 sha256'sına yazılmış CT-APR-01 onay satırı; künye `ready-for-dev` (r14'te sağlandı) | DL-3 kodundan önce | CT + Beste |
| DL-3 **başlama kapısı** (r14) | Q-8…Q-16 sahip cevabı (Bölüm 24.2); cevap öneriden farklıysa yeni paket revizyonu ve yeni onay | DL-3 kodundan önce (r14 onayıyla birlikte verilebilir) | Beste |
| DL-3 **merge kapısı** (r14) | **A-20** CT defter kararı: `platform-islem-ici` iz satırına `RecordCodeAuditIntent.EnqueueClaimedAsync/EnqueueChangedAsync/EnqueueLockedAsync/EnqueueAssignmentRefusalAsync` belirteci, CT-DECISIONS satırı, `AuditTrailStandardTests` yeşil (A-10 emsali) | DL-3 kodu main'e girmeden önce (kod yazımını bekletmez) | CT |
| DL-4 **başlama kapısı** (r14 bilgi) | DL-3 kodu CT tarafından doğrulanmış ve DL-4'ün yazılacağı tabanda (Bölüm 23 dilim sırası: DL-4 DL-3'ü bekler); DL-4'ün kendi kapıları kendi revizyonunda | DL-4 kodundan önce | CT |

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

### 19.5 Bağımsız inceleme 04 tur 1 (KOŞULLU) — kapanış tablosu (r13)

Bulgu dosyası `docs/records/reviews/MOD-0040-FU02/2026-11-10-d29f9c5e.md`, özet `25888259…` (`execution/approvals/reviews.tsv`
`f73964c6e` `:323` / `91d72310e` `:244`); hedef r12 sha256 `d29f9c5e…ba6d`. Sahip kararları `…/mod0040-fu02-owner-decisions-03.md`
(K-16) ve `-04.md` (K-17).

| Bulgu | Ciddiyet | Kapanış | Nerede |
|---|---|---|---|
| F-1@25888259 | ORTA | Ölçüldü: mevcut arayüzlere eklenen gövdesiz yöntem sekiz test sahtesini düşürürdü (`InMemoryTenantOrganizationRepositories.cs:7`, `:304`; `TaskTestDoubles.cs:651`, `:674`; `TaskOrganizationUnitScopeTests.cs:270`; `ApprovalStepContextTests.cs:650`, `:728`; `WorkflowCrossServiceTests.cs:1151`). Sahip kararı (K-17 F-1): yöntem iki **dar** arayüze taşındı (`IOrganizationUnitCodeHistoryRepository`, `IPositionCodeHistoryRepository`; emsal `IOrganizationReportingGraphRepository`, `IOrganizationUnitRepository.cs:15-24`); uygulayanlar iki Mongo deposu ve `InMemoryTenantOrganizationRepositories.cs` (XMC ek onayı); DI iki satır (Infrastructure kompozisyon dosyası); diğer altı sahte korunan yol; AC-61'e `git diff` adımı | Bölüm 0.1 K-17; 2.4; 3; 5 "DL-2" (bu paketin dosyaları, XMC listesi, Değişmez); 6; 8.15; 16 AC-61 |
| F-2@25888259 | ORTA | Ölçüldü: `PositionReferenceGuard` `internal static`, yalnız `Response<NoContent>` döner (`PositionReferenceGuard.cs:6-20`). Sahip kararı **(b)**: `CreatePositionCommandHandler` guard'dan sonra birimi ikinci kez `GetByIdAsync` ile okur; ikinci okumada yok / arşivli → 404, ayırma yok; iki okuma arasında şirket değişimi §5.2 gereği kabul; guard değişmez ve XMC listesine girmez | 0.1 K-17; 5 (`CreatePositionCommandHandler.cs` satırı, Değişmez); 6; 8.14 "Sıra" adım 1 ve "POS'un şirket kaynağı"; 16 AC-51; 17 S-78 |
| F-3@25888259 | ORTA | AC-58 HTTP testi çerçevenin 403'ünü `reason_code` ile ayırır — **ölçüm incelemeyi düzeltti:** zarfta `reason_code` vardır ve `PERM_DENIED`'dir (`HasPermissionAttribute.cs:140-155`, `DocumentManagementContractModels.cs:31`); ayrıca `allocation-refused` satırı yok adımı. AC-57'ye "ayırma ret satırı yok" adımı (`AllocateRecordCodeHandler.cs:158-160`). S-82 ve S-85 hedefleri buna göre | 13; 14 (DL-2); 16 AC-57, AC-58; 17 S-82, S-85 |
| F-4@25888259 | ORTA | İki yön + "başka şirkette aynı önek" tablosu, kurtarma yolu, bildirim (Treasury CT + MDM sahibi); ölçüm `AllocateRecordCodeHandler.cs:177-187`, `:180`, `RecordCodeRepositories.cs:164-193`. Sahip kararı (K-17 F-4): **A-18 DL-2'nin canlıya alma kapısıdır** (kod yazımı bekletilmez; merge için CT kararı açık). AC-56'ya (b) "önce ORG, sonra MDM değişikliği, sonra `PAY` 409" ve (c) adımları; yeni S-89 | 0.1 K-17; 2.4; 7 (yeni satır), 7.1; 8.13; 8.14 "Kilidin iki yönü"; 8.16 SN-5; 13; 16 AC-56; 17 S-89; 18.1 (yeni satır); 23; 24 A-18 |
| F-5@25888259 | DÜŞÜK | `RecordCodeLegalEntitySourceUnavailable` (`LEGAL_ENTITY_SOURCE_UNAVAILABLE`, `RecordCodeReasonCodes.cs:23`; üretildiği yerler `MdmLegalEntityShortCodeReader.cs:70`, `:82`, `:121`) ve `RecordCodeForbidden` 7 dilde eklendi; anahtar → `reason_code` eşlemesi yazıldı; AC-60'a resx-check eşdeğeri (6 dilde değer ≠ `en`, `localization-standard.md:14`); S-87 genişledi | 11; 16 AC-60; 17 S-87 |
| F-6@25888259 | DÜŞÜK | Kod kuralı paylaşılan doğrulayıcıdan **tümüyle** kalkar (`TenantOrganizationRequestValidators.cs:9`, `:19`; `ValidationBehavior` en dışta, `Application/DependencyInjection.cs:45-48`); "boş" = `IsNullOrWhiteSpace`, oluşturma ve güncellemede aynı; AC-52 / AC-53'e 81 karakter ve yalnız boşluk adımları; yeni S-90 | 5 (doğrulayıcı satırı); 8.15 "Dolu ve boşun tanımı"; 12; 13; 16 AC-52, AC-53; 17 S-90 |
| F-7@25888259 | DÜŞÜK | S-80'e "409 denetimi kaldırılır" kolu; S-84 "kiracı çelişkisi yok sayılır" diye daraltıldı (port imzası kiracı taşımaz); AC-51'de bağlam şirketi birimin şirketinden farklı (`TenantOrganizationContracts.cs:51-64`, `JwtTenantAuthorizationContext.cs:71`, `:77-89`); S-78 netleşti | 8.14 "POS'un şirket kaynağı"; 16 AC-51; 17 S-78, S-80, S-84 |
| F-8@25888259 | DÜŞÜK | Anahtar eki tanımlandı: 1. denemede ek yok, sonra `{taban}:2`, `{taban}:3`; her deneme ayrı ayırma; `k` atlama `k + 1` numara harcar, 409 `RECORD_CODE_COLLISION` 3 numara yakar; AC-55 / AC-59'a atlama senaryosunun defter ve denetim satırları; yeni S-92 | 8.14 `IdempotencyKey` satırı; 8.15 "Çakışma atlamasının bedeli ve izi"; 13; 16 AC-55, AC-59; 17 S-92; 21.4 |
| F-9@25888259 | DÜŞÜK | Boş / `null` katalog izni `false` (`IActorPermissionContext.cs:31-34`'teki "boş = true" bu yolda uygulanmaz) — `HoldsRequiredPermission` yardımcısı, adım 8 reddi + `Denied`; `ActorType` `OrdinalIgnoreCase` (`TenantResolutionMiddleware.cs:199-212`); AC-58 iki test; yeni S-88 | 8.14 (`HasRequiredPermission`, `OnBehalfOfUserId` satırları); 13; 14; 16 AC-58; 17 S-88 |
| F-10@25888259 | DÜŞÜK | AC-54 (b): 500 `ProblemDetails` "Server Error" (`GlobalExceptionHandler.cs:61-64`), merkezi `Failed` + `RuntimeException` satırı (`AuditBehavior.cs:63-66`, `:192-206`), `ExceptionBehavior` yeniden fırlatır (`:42-44`, `:66`, `:78-82`); log kalıbı try / catch + uyarı + `throw;`; sarmalayıcı `MongoException` türevi atar; işleyiciye `ILogger`; yeni S-91 | 5 (oluşturma işleyicisi satırları); 8.14 "Cevap ve denetim"; 13; 16 AC-54 (b); 17 S-91; 21.4 |
| F-11@25888259 | DÜŞÜK | "Sürüm notu" alt bölümü (SN-1…SN-7; Q-2 kararı); AC-51'e `mdm.legal-entities.read` taşımayan kullanıcı adımı (403 `LEGAL_ENTITY_READ_PERMISSION_MISSING`) | 8.16 (yeni); 13; 16 AC-51 |
| F-12@25888259 | DÜŞÜK | Bölüm 18'deki iki eskimiş satır güncellendi: r11 incelendi (inceleme 03 tur 9 + son bakış 01 tur 6, UYGUN) ve onaylı; r12 incelendi (inceleme 04 tur 1, KOŞULLU), onaysız; r13 incelemesi ve onayı bekliyor. Satır numaraları dala bağlıdır, ikisi de yazıldı | 18; üst uyarı |
| F-13@25888259 | DÜŞÜK | Sözleşme §6 sütununun yalnız HTTP görünümünü anlattığı ve sonraki eklemeli sürümde güncelleneceği notu (`record-code-allocate.md:170`); FU-RCS-20 | 4.4; 20 |

**r13 ölçüm kaynakları.** Kod: `f73964c6e` (dal `feature/sce/mod-0173-inventory-ledger-valuation`), `git show <commit>:<yol>` ile
okundu; Platform, Platform.Common, Web ve sözleşme dosyaları `36465315c` ve `a9fd192ba` ile aynıdır (`git diff --stat` boş,
2026-11-10). Okunan dosyalar: `IOrganizationUnitRepository.cs:1-67`, `IPositionRepository.cs:1-17`, `Infrastructure/DependencyInjection.cs:334-345`,
`OrganizationUnitRepository.cs:12`, `PositionRepository.cs:10`, sekiz test sahtesinin sınıf bildirimleri (`git grep`),
`InMemoryTenantOrganizationRepositories.cs:1-60`, `:300-345`, `TimeEntry/TimeEntryTestHost.cs:455-500`, `PositionReferenceGuard.cs:1-40`,
`CreatePositionCommandHandler.cs:1-57`, `CreateOrganizationUnitCommandHandler.cs:1-92`, `AllocateRecordCodeHandler.cs:60-215`,
`RecordCodeRepositories.cs:164-193`, `RecordCodeReasonCodes.cs:1-30`, `MdmLegalEntityShortCodeReader.cs:60-125`,
`TenantOrganizationRequestValidators.cs:1-25`, `Application/DependencyInjection.cs:40-55`, `TenantOrganizationContracts.cs:49-66`,
`OrganizationUnit.cs:5-12`, `JwtTenantAuthorizationContext.cs:15-95`, `IActorPermissionContext.cs:1-35`,
`TenantResolutionMiddleware.cs:195-215`, `:310-320`, `ExceptionBehavior.cs:30-90`, `AuditBehavior.cs:50-90`, `:192-217`,
`GlobalExceptionHandler.cs:40-66`, `HasPermissionAttribute.cs:95-156`, `DocumentManagementContractModels.cs:29-31`. Kural ve kayıt:
`.antigravity/rules/record-coding-standard.md` (bu dalda ve `f73964c6e`'de aynı; `a9fd192ba`'dan sonra §3'e `:71` satırı eklendiği için
§5 ve sonrası **bir satır kaydı** — paketteki atıflar r13'te güncellendi: §5.2 `:90-93`, §5.8 `:121-125` / `:123`, §5.9 `:127-131`,
§6 `:157`, §7 `:162-166`; Bölüm 19.4'teki r12 numaraları `a9fd192ba`'nındır), `.antigravity/rules/localization-standard.md:14`,
`docs/analysis/contracts/record-code-allocate.md:160-171`, `execution/approvals/reviews.tsv` ve `approvals.tsv` (iki dalda). Bu paketin
yazıldığı dalda (`91d72310e`) `Features/RecordCodes/**` yoktur (`git diff --stat 36465315c HEAD` ölçtü) — DL-1 kodu kod dalındadır.

### 19.6 r14 (DL-3) ölçüm kaynakları

Ölçüm ağacı: `8931275d2` (detached; DL-1 ve DL-2 kodu var), 2026-11-13. `origin/main` ayrıca ölçüldü. Yazım sırasında ağaç
`50a2aaf89`'a ilerledi: aradaki `63f046c92` (MOD-0049-FU01 `.antigravity` kapısı) RCS-001 §3'e `BPT` satırı ve
`RecordCodeTypeCatalogTests`'e bir satır (`BPT` "kataloğun önünde") ekler — `git diff --stat 8931275d2 50a2aaf89 -- services/Diten.Platform`
yalnız o test dosyasını gösterir (ölçüldü). Bu yüzden RCS-001 satır atıfları `@8931275d2` ile sabitlendi; `AC16_…` testine DL-3'ün
`TTY` eklemesi ile MOD-0049-FU01'in `BPT` satırı aynı testte buluşur (birleştirmede ikisi de kalır).

| İddia | Kaynak (ölçüldü) |
|---|---|
| İKİSİ DE türleri ve sahipleri | RCS-001 §3 (`.antigravity/rules/record-coding-standard.md`): Tüzel Kişilik, Görev Türü `TTY`, `PFL` / `PRG` / `PRJ`, `INI` / `INV`, Abonelik Planı. Sahipler: `TaskType` `TenantScopedEntity` (`services/Diten.Platform/src/Diten.Platform.Domain/Entities/Tasks/TaskSupportingEntities.cs:583`) → MOD-0024 (kayıt satırı `execution/registries/module-id-registry.md`, `service: Diten.Platform`); PPM `Portfolio` / `Program` / `Project` / `Initiative` / `InvestmentCase` `Code` alanları `services/Diten.PpmService/src/Diten.PpmService.Domain/Entities/` → MOD-0117 (Diten.PpmService); `SubscriptionPlan : GlobalEntity` (`services/Diten.Platform/src/Diten.Platform.Domain/Entities/SubscriptionPlan.cs:5`) → MOD-0361 (CAND-CAP-0002'nin kanonik hedefi); Tüzel Kişilik → MOD-0220 (MDM) |
| TTY kodu bugün | `TaskType.Code` "Tenant-unique and IMMUTABLE" (`TaskSupportingEntities.cs:586`); normalleştirme kırp + büyük harf (`services/Diten.Platform/src/Diten.Platform.Application/Features/Tasks/Services/TaskTypeRules.cs:39`), en çok 40 karakter (`TaskTypeRules.cs:17`) |
| TTY izni | `TaskPermissions.TaskTypesManage = "platform.tasks.task-types.manage"` (`services/Diten.Platform/src/Diten.Platform.Application/Features/Tasks/TaskModels.cs:32`); `CreateTaskType` / `UpdateTaskType` üzerinde (`services/Diten.Platform/src/Diten.Platform.API/Controllers/TasksController.cs:856`, `:866`) |
| Katalog bugün | `TTY` yok, `PLN` son satır ve İKİSİ DE (`services/Diten.Platform/src/Diten.Platform.Domain/Features/RecordCodes/RecordCodeTypeCatalog.cs:41`); DL-1 testleri `TTY`'yi "katalogda yok" diye ölçer (`RecordCodeTypeCatalogTests.AC14_kalsin_ikisi_de_old_and_unknown_types_are_not_in_the_catalogue`) ve PLN dışındaki her satırı OTOMATİK sayar (`AC16_…`) |
| Sayaç deposu yalnız `NextAsync` | `services/Diten.Platform/src/Diten.Platform.Domain/Repositories/IRecordCodeRepositories.cs:10-13`; DL-1 testi `AC20_no_store_can_change_remove_or_reissue_a_code` üye adlarını birebir sabitler → önizleme okuması ayrı arayüz |
| E11000 işlem içinde | yürütücü geçici olmayan `MongoException`'ı `PlatformTransactionUnavailableException`'a çevirir (Bölüm 7 tablosu); DL-1 bu yüzden E11000'i depoda `MongoException` olmayan istisnaya çevirir (`RecordCodeIdempotencyConflictException`) — DL-3 aynı deseni izler |
| Grup öneki kuralı | `AllocateRecordCodeHandler.cs:130-148` (ayar yok → 409 `GROUP_PREFIX_MISSING`; `OrgPrefix`'ten farklı → 409 `GROUP_PREFIX_MISMATCH`) |
| Denetim defteri | `platform-islem-ici` satırı `RecordCodeAuditIntent.*` dört belirteci taşır (`tests/architecture/audit-ledger/Diten.Platform.md:16`); A-10 CT satırı `tests/architecture/audit-ledger/CT-DECISIONS.md:23` |
| Redaktör | `SensitiveFieldRedactionRegistry` `Reason`, `Code` adlarını maskelemez (`services/Diten.Platform/src/Diten.Platform.Application/Features/Audit/SensitiveFieldRedactionRegistry.cs:9-26`) |
| Sözleşme | §13 "Yeni tür eklemek sözleşme değişikliği değildir" (`docs/analysis/contracts/record-code-allocate.md:382`); §4 `RECORD_TYPE_UNKNOWN` tanımı İKİSİ DE türlerini anar (`:123`) |
| DL-1 / DL-2 main durumu | `git cat-file -e origin/main:…/RecordCodeAuditIntent.cs` → var; `…/InProcessRecordCodeAllocator.cs` → yok |
| Kimlik ön kontrolü | `verify_module_id.py … --check-id MOD-0040-FU02 --name "Record Code Service" --parent MOD-0040` → OK, çıkış 0 |

**Çıkarım (ölçülmedi):** PPM türlerinin izinli kapsamı (LegalEntity mi Group mu) ve PPM kodunun bugünkü kuralları bu revizyonda
okunmadı — DL-4'ün işi. TTY'nin canlı verisinde desene uyan kod olup olmadığı okunmadı — DL-4 kabulünde (FU-RCS-22).

## 20. Follow-up Items

| # | İş | Sahip | Dilim |
|---|---|---|---|
| FU-RCS-01 | ORG / POS bağlanması (süreç içi port) — **r12'de yazıldı** (Bölüm 2.4, 8.14, 8.15; AC-48…AC-61); kod izni r12 (ya da ardılı) onayı + `ready-for-dev` künyesiyle | Beste | DL-2 |
| FU-RCS-17 | MOD-0288 merkezi oluşturma denetimi `EntityId` ve kod taşımaz (`CreateOrganizationUnitCommand.cs:11-13`, `CreatePositionCommand.cs:11-13` — `GetAuditMetadata` işleyiciden önce okunur); kod bugün yalnız ayırma satırında (Tablo A) görünür. Merkezi satırın zenginleştirilmesi | MOD-0288 sahibi (TBD) | — |
| FU-RCS-18 | Geliştirme tohumu `PositionSeed` elle kod yazar (`PositionSeed.cs:54`, `:65-69`); kullanılmayan `_CreateEditOffcanvas.cshtml` parçaları kod girişi taşır; `CodePlaceholder` / `InvalidCode` / `FinalCodePreview` anahtarları DL-2'den sonra kullanılmaz — temizlik | MOD-0288 sahibi (TBD) | — |
| FU-RCS-20 | Sözleşme RECORD-CODE-ALLOCATE §6 `ORG`, `POS` satırının bir sonraki eklemeli sürümde "HTTP: izinli çağıran yok → 403; süreç içi (Platform): LegalEntity" diye güncellenmesi (`record-code-allocate.md:170`; inceleme 04 F-13, Bölüm 4.4). Kendi başına sürüm açtırmaz; HTTP davranışı değişmez. **r14 (DL-3) eki:** aynı sürümde §4 `RECORD_TYPE_UNKNOWN` tanımı ("KALSIN, İKİSİ DE, …", `record-code-allocate.md:123`) "bağlanmamış İKİSİ DE" diye daraltılır ve §6'ya `TTY` satırı ("HTTP: izinli çağıran yok → 403; süreç içi (Platform): Group") eklenir | CT (sözleşme) + Beste | sonraki sözleşme sürümü |
| FU-RCS-19 | Eski elle kodlarda silinmiş kaydın kodunun yeniden kullanımı (`ux_*_tenant_code_active` kısmi index'i ve `ExistsByCodeAsync`'in silinmişleri dışlaması — RCS-001 §5.8); DL-2 yalnız otomatik kodlarda kapatır | MOD-0288 sahibi (TBD) + Beste | — |
| FU-RCS-02 | İKİSİ DE motoru: öneri + gerekçeli değişiklik + AUD-001; §5.9 — **r14'te yazıldı** (Bölüm 2.5, 4.5, 8.17; AC-62…AC-75); kod izni r14 onayıyla | Beste | DL-3 |
| FU-RCS-03 | TTY, PPM (PPM sahibi Codex), PLN bağlanması + Platform kapsamı | Beste + PPM sahibi | DL-4 |
| FU-RCS-04 | Kodu kayda bağlama / tüketme, kullanılmayan kod raporu — **r14:** İKİSİ DE türlerinde kod–kayıt bağı DL-3 sahiplik defterindedir (`RecordId`, Bölüm 4.5); OTOMATİK türler ve kullanılmayan kod raporu açık | Beste | DL-4 |
| FU-RCS-21 | Süreç içi savunma ve izin hükmünün iki kopyası (DL-2 `InProcessRecordCodeAllocator.HoldsRequiredPermission` + kiracı / aktör savunması; DL-3 `RecordCodeInProcessGuard`) teke indirilir: DL-2 portu DL-3 yardımcısını çağırır, davranış aynı kalır (DL-2 testleri değişmeden yeşil). DL-2 ve DL-3 aynı tabanda olunca | Beste | DL-3 ile DL-2 aynı tabanda |
| FU-RCS-22 | İKİSİ DE tüketicisinin **mevcut (eski) kodlarının** sahiplik defterine alınması (köken "eski", gerekçesiz, emekli değil) ve sistem kodunun desene uyan eski kodla çakışma savunması (DL-2 Bölüm 8.15 emsali) — tüketici bağlanırken; TTY canlı verisinde desene uyan kod sayımı | Beste + tüketici sahibi | DL-4 / DL-6 |
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
| `CreateOrganizationUnitCommand` / `CreatePositionCommand` (MOD-0288; arayüz değişmez) | merkezi satır (`RequestType` = komut adı) | `OrganizationUnit` / `Position` | a — `platform-merkezi` (`IAuditableCommand` + `IAuditMetadataProvider`, `CreateOrganizationUnitCommand.cs:8-14`, `CreatePositionCommand.cs:8-14`) | bugünkü gibi — `EntityId` / `AfterState` yok (FU-RCS-17) | başarısız cevap `Outcome = Failed` (`AuditBehavior.cs:69`): ayırma reddi, `RECORD_CODE_SYSTEM_ASSIGNED`, `RECORD_CODE_COLLISION`, port savunma retleri; **r13 (F-10):** kayıt yazımı istisnasında `Failed` + `Metadata.AuditBehaviorOutcome = RuntimeException`, `ExceptionType` (`AuditBehavior.cs:63-66`, `:192-206`), cevap 500 `ProblemDetails` |
| `AllocateRecordCodeCommand` (süreç içi, başarı) | `platform.record-code.allocated` | `RecordCodeAllocation` | a — `platform-islem-ici` (`RecordCodeAuditIntent.EnqueueAsync`, ayırmayla aynı işlem) | **Tablo A**; `SourceService = Diten.Platform`, `Metadata.IdempotencyKey = organization-unit:{id:N}` / `position:{id:N}`, `AfterState.Code` = kaydın kodu | kayıt yazımı sonradan düşerse satır kalır (boşluk, Bölüm 8.14); **r13 (F-8):** çakışma atlamasında her deneme ayrı satırdır (anahtar `{taban}`, `{taban}:2`, `{taban}:3`), kaydın kodu yalnız son satırdadır; 409 `RECORD_CODE_COLLISION` üç satır bırakır (Bölüm 8.15) |
| `AllocateRecordCodeCommand` (süreç içi, ret) | `platform.record-code.allocation-refused` | `RecordCodeAllocation` (kimliksiz) | a — ayrı kısa işlem (`RecordCodeAuditIntent.EnqueueRefusalAsync`) | **Tablo B**; adım 4–13; `Outcome` Bölüm 21.2 | — |
| `UpdateOrganizationUnitCommand` / `UpdatePositionCommand` (kod değişikliği reddi) | merkezi satır | `OrganizationUnit` / `Position` | a — `platform-merkezi` | 409 `RECORD_CODE_IMMUTABLE` → `Failed` | yeni komut yok |

- Portun savunma retleri (kiracı çelişkisi, `ActorType ≠ tenant_user`, boş `sub`) ayırmaya ulaşmaz; ayırma ret satırı yazılmaz —
  Bölüm 21.2'deki "403 kiracı çözülmeden" satırıyla aynı gerekçe (doğrulanmış kiracı + kişi ikilisi yok); merkezi `Failed` oluşturma
  satırı iz bırakır.
- K2 sınıfı değil; okuma denetimi yok; BL-501 kullanılmaz (Bölüm 21.2 ile aynı).

### 21.5 DL-3 (r14) — İKİSİ DE motoru

Servisin denetim altyapısı bugün vardır (`platform-islem-ici`, `a`, `tests/architecture/audit-ledger/Diten.Platform.md:16`). DL-3 **üç
yeni yazma komutu** ve **dört yeni belirteç** açar; belirteçler `platform-islem-ici` satırına CT kararıyla eklenir — **A-20** (A-10
emsali). Karar yoksa DL-3 main'e giremez (Bölüm 18.1). Üç komut da yol `a`'dır (işlem içi kapı); istisna sınıfı yok.

| Komut | Olay adı | Nesne (tür) | Yol | Önceki / sonraki ya da değişen alanlar | İstisna sınıfı + gerekçe |
|---|---|---|---|---|---|
| `ClaimRecordCodeCommand` (başarı) | `platform.record-code.claimed` | `RecordCodeClaim` (`EntityId` = `RecordId`) | a — işlem içi kapı (`RecordCodeAuditIntent.EnqueueClaimedAsync` → `ITransactionalAuditOutboxWriter.TryEnqueueAsync`), sahiplik satırıyla aynı işlem; kutu anahtarı `record-code:claimed:{claimId:N}` | **Tablo E** | — |
| `ChangeRecordCodeCommand` (başarı) | `platform.record-code.changed` | `RecordCodeClaim` (`EntityId` = `RecordId`) | a — işlem içi kapı (`EnqueueChangedAsync`), emeklilik + yeni sahiplikle aynı işlem; anahtar `record-code:changed:{newClaimId:N}` | **Tablo F** — eski kod, yeni kod, gerekçe, kişi, zaman (RCS-001 §2) | — |
| `LockRecordCodeCommand` (başarı, `LockedNow`) | `platform.record-code.locked` | `RecordCodeClaim` (`EntityId` = `RecordId`) | a — işlem içi kapı (`EnqueueLockedAsync`); anahtar `record-code:locked:{claimId:N}`; zaten kilitliyse satır yok (değişiklik yok) | **Tablo G** | — |
| Üçünün reddi | `platform.record-code.claim-refused` / `.change-refused` / `.lock-refused` | `RecordCodeClaim` | a — ayrı kısa işlem (`RecordCodeAuditIntent.EnqueueAssignmentRefusalAsync` → `ITransactionalAuditRefusalWriter`); anahtar `record-code:assignment-refused:{yeni Guid:N}` | **Tablo H**; `Outcome` Bölüm 21.2 kuralıyla | — |
| `PreviewRecordCodeQuery`, `GetRecordCodeHistoryQuery` | — | — | okuma — denetlenmez (AUD-001 §7'nin üç sınıfına girmez) | — | — |

Ortak alanlar (dört tablo): `TenantId` = `TargetTenantId` = token kiracısı; `SourceService = Diten.Platform`; `SourceModule =
MOD-0040-FU02`; `Category = PlatformConfiguration` (DL-1 ile aynı; `AuditEnums.cs`'e yeni kategori yok); aktör principal'dan
(`TenantUser`, `ActorId` = `sub`; kapı çözer — K-8r); `OccurredAtUtc` kapıdan; `CorrelationId` komuttan; `Metadata.EventName` olay
adı; `Metadata.RecordType`.

**Tablo E — sahiplenme** (`RequestType = ClaimRecordCodeCommand`, `Operation = Create`): `AfterState` = `{ Code, Origin, Status =
Active, RecordType, RecordId }`; `Metadata` = `{ EventName, RecordType, ClaimId, Reason (yalnız Manual), AllocationIdempotencyKey
(yalnız System: tty:{RecordId:N}) }`. Sistem kodunda DL-1'in Tablo A satırı **ayrıca** yazılır (kendi işleminde).

**Tablo F — gerekçeli değişiklik** (`RequestType = ChangeRecordCodeCommand`, `Operation = Update`): `BeforeState = { Code: eski }`;
`AfterState = { Code: yeni }`; `Metadata = { EventName, RecordType, Reason, RetiredClaimId, NewClaimId }`. Kişi = `ActorId` /
`ActorType` (+ kapının maskeli ad / e-posta alanları), zaman = `OccurredAtUtc` — RCS-001 §2 ve AUD-001 §3 satır 1, 7, 10 tek satırda.

**Tablo G — kilit** (`RequestType = LockRecordCodeCommand`, `Operation = Update`): `BeforeState = { LockedAtUtc: null }`;
`AfterState = { LockedAtUtc }`; `Metadata = { EventName, RecordType, Code }`.

**Tablo H — ret** (ret yazıcısı; `Operation` komutunkiyle aynı): `EntityId` = `RecordId` (biçimsizse yok); `Metadata` = `{ EventName,
ReasonCode, EvaluationStep, RecordType (adım 2'yi geçtiyse), AttemptedCode (yalnız adım 11'i geçmiş normalleştirilmiş kod) }`;
**gerekçe metni yazılmaz**. `Outcome`: 403 (`RECORD_CODE_TENANT_NOT_ACTIVE`, `RECORD_CODE_FORBIDDEN`, `RECORD_CODE_PERMISSION_MISSING`)
ve 404 `RECORD_CODE_NOT_ASSIGNED` (tek gövdeli gizleme) → `Denied`; diğer her ret → `Failed`. Port savunması (Bölüm 8.17) komutu
göndermediği için ret satırı yazmaz (uyarı logu). Sistem kodlu sahiplenmede DL-1 ayırmasının reddi DL-1'in kendi satırıdır (Tablo B);
motor ikinci satır yazmaz.

- K2 sınıfı değil (kimlik / yetki / kiracı durumu / GxP / KVKK-özel değil); yine de başarı yolu kayıt yoksa işlem yok biçimindedir
  (DL-1 ile aynı işlem içi kapı). BL-501 kullanılmaz.

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
- **DL-3 (r14) — İKİSİ DE (RCS-001 §6 kontrol listesinin İKİSİ DE maddeleri):** gerekçe alanı zorunlu, denetim kaydında eski / yeni
  kod + gerekçe (AUD-001) — AC-64, AC-68, Tablo F; OTOMATİK türde değiştirme API'de reddedilir — AC-69; benzersizlik kiracı + tür
  başına (Q-13 A önerisi; §6 "ya da gerekçeli kapsam"), emekli kodlar dolu — AC-67; elle kod otomatik desene uymaz (§5.9 varsayılanı)
  — AC-63; ASCII ve Türkçe harf çevirisi (§5.10) — AC-63; ortak servis (§5.12) — motor; testler: "değiştirme gerekçesiz reddedilir"
  AC-64, "arşivli kodu yeniden kullanılamaz" AC-67 / AC-68, eşzamanlı iki sahiplenme farklı sonuç (E11000 savunması) AC-67, sistem
  kodu DL-1 sayacından (999999 → 7 hane DL-1 AC-7). `TTY` §3'te İKİSİ DE, önek `TTY`, grup öneki (§5.3).

## 23. Teslim dilimleri

| dilim | ad | kapsadığı bölümler | kabul ölçütleri ve kapsam notu |
|---|---|---|---|
| DL-1 | v1 servis — ilk commit grubu aktör ölçümü; allocate iç ucu, atomik sayaç, LegalEntity ve Group kapsamları, OTOMATİK tür önek kaydı, şirket kısa kodu ve uygunluk okuması, kısa kod kullanım kilidi (ara dönem), idempotency, işlem içi denetim (aktör kullanıcı, kaynak servis, ayırma kimliğinden kutu anahtarı), ret kaydı, denetim yazıcısının eklemeli genişletmesi, kiracı izolasyonu ve askıdaki kiracı, kimlik bilgisi azaltıcıları ve hız sınırı, grup öneki hizalaması | Owned Objects, Entity Fields, Repo Scope, Dependencies, Runtime Constraints, Backend File Convention, Validation Rules, Failure Path to Verify, Authorization Convention, Gateway / API Routing Decision, Acceptance Criteria, Test Expectations, Audited Events, Record Codes | AC-1…AC-24 ve AC-26…AC-47; AC-41 ilk commit grubu (başarısızsa geri alınır, dilim durur); Platform kapsamı tür izni geçtiyse 422 (K-11); hedef main 2026-11-06; sözleşme 2026-11-03'te donar; **merge kapısı** A-10 CT defter kararı ve A-12; **kabul kapısı** A-1, A-11, A-16, A-17, sözleşme dondurma (Bölüm 18.1; r9 CT kararı 2026-11-02) — kod yazımını bekletmez |
| DL-2 | OTOMATİK mevcut modüllerin bağlanması — Organizasyon Birimi (ORG) ve Pozisyon (POS), süreç içi port | Ownership and Boundaries, Owned Objects, Entity Fields, Repo Scope, Protected Paths, Dependencies, Runtime Constraints, Layout & Shell Contract, Frontend File Contract, Validation Rules, Failure Path to Verify, Authorization Convention, Gateway / API Routing Decision, Acceptance Criteria, Test Expectations, Audited Events, Record Codes, Follow-up Items | **r12:** AC-48…AC-61; A-8 kapandı (K-15, 2026-11-10); süreç içi port `IInProcessRecordCodeAllocator` (Bölüm 8.14), sözleşme 1.0.0 değişmez (HTTP'de ORG / POS 403); MOD-0288 dosyaları XMC-001 (Bölüm 5 "DL-2"). **r13:** Q-1…Q-7 kapandı ve XMC-001 onaylı (K-16, K-17); künye `ready-for-dev`; **başlama kapısı** yalnız r13 inceleme + r13 sha256'sına onay satırı (S-49, Q-6 A) ve FU03 / FU04 sıra ölçümü (CT); **kabul kapısı** eski kod sayımı (AC-55); **canlıya alma kapısı** A-18 + Treasury CT / MDM sahibi bildirimi (Bölüm 18.1, K-17); kod izni onay satırına kadar yok |
| DL-3 | İKİSİ DE motoru — öneri, gerekçeli değişiklik, AUD-001 eski/yeni kod + gerekçe, RCS-001 §5.9 | Ownership and Boundaries, Owned Objects, Entity Fields, Repo Scope, Protected Paths, Dependencies, Runtime Constraints, Backend File Convention, Validation Rules, Failure Path to Verify, Authorization Convention, Gateway / API Routing Decision, Acceptance Criteria, Test Expectations, Audited Events, Record Codes, Follow-up Items | **r14:** AC-62…AC-75; süreç içi port `IInProcessRecordCodeAssignments` (Bölüm 8.17), sahiplik defteri `record_code_claims` (Bölüm 4.5), ölçüm türü `TTY` (Q-8 A önerisi; MOD-0024 dosyalarına dokunulmaz, XMC-001 satırı yok); sözleşme 1.0.0 değişmez; **başlama kapısı** DL-1 main'de (sağlandı), r14 inceleme + r14 sha256'sına onay satırı, Q-8…Q-16 sahip cevabı; **merge kapısı** A-20 CT defter kararı (Bölüm 18.1); r14 onayıyla kod izni |
| DL-4 | Platform kapsamı ve İKİSİ DE modüllerinin bağlanması — Abonelik Planı (PLN, öneksiz kod), Görev Türü (TTY), PPM (PFL/PRG/PRJ/INI/INV) | Ownership and Boundaries, Runtime Constraints, Acceptance Criteria, Record Codes, Follow-up Items | AC-25 (K-11'den taşındı) ve bu dilimin revizyonunda eklenecekler; kiracısız satır tabanı ve platform-global denetim yolu burada kararlaştırılır; kod izni yok |
| DL-5 | Tüzel Kişilik kısa kodu önerisi ve RCS-001 §5.4 kalıcı kullanım kilidi (MDM tarafı) — **bağımlılık**: MDM kodu bağlı paket MOD-0220'nin (revizyon mu FU mu: TBD), bu paketin payı yalnız Platform azaltıcısının (Bölüm 8.13) kilit gelince gözden geçirilmesi | Dependencies, Follow-up Items | **başlama kapısı** A-18 (MDM sahibi Ali + Beste, 2026-11-06; Bölüm 18.1); kabul ölçütleri bu dilimin paket revizyonunda; o güne kadar ara dönem azaltıcısı DL-1'de; kod izni yok |
| DL-6 | İçe aktarma ve sayaç yüksek su işareti (RCS-001 §5.11), WMS geçici sayacından devam | Runtime Constraints, Follow-up Items | kabul ölçütleri bu dilimin paket revizyonunda; FU-0178-RCS-01 önkoşulu; kod izni yok |
| DL-7 | RCS-001 §3 tür başına izinli kapsam sütunu ve CTR → AGR satırı — **bağımlılık**: kural dosyası değişikliği, bağlı paket yok (TBD, CT); bu paketin payı yalnız katalog testindeki adlı istisnanın kaldırılması | Record Codes, Açık kararlar | sahip Ali (.antigravity) + Treasury PR'ı (A-3); DL-1'i bekletmez |
| DL-8 | WMS türleri (PUT, PCK, OUT, PKG) kataloğa ve Inventory kimlik bilgisi | Entity Fields, Authorization Convention, Follow-up Items | kabul ölçütleri bu dilimin paket revizyonunda; FU-0178-RCS-02 önkoşul; kaynak servis kümesine Diten.InventoryService; kod izni yok |
| DL-9 | Arka plan çağıranları için servis kimliği token'ı (P3) — **bağımlılık**: Auth kodu bağlı paket MOD-0033-FU02'nin (revizyon mu yeni FU mu: TBD); bu paketin payı Platform yürütücüsünün P3 token'ını kabul etmesi | Runtime Constraints, Follow-up Items | kabul ölçütleri bu dilimin paket revizyonunda; Auth sahip rolleri MOD-0033-FU02 künyesindeki + Ali; kod izni yok |
| DL-10 | Platform yönetici ekranları (tür kataloğu, sayaç, defter, grup öneki) | Layout & Shell Contract, Frontend File Contract, Follow-up Items | kabul ölçütleri bu dilimin paket revizyonunda; 2 dil, `_LayoutPlatformAdmin`, SEC-002 kapsam cümlesi; kod izni yok |

Dilim sırası: DL-1 tek başına teslim edilebilir ve ilk commit grubu aktör ölçümüdür (AC-41). DL-5, DL-7 ve DL-9'un ana
işi başka sahiplerin kodudur ve bu paketin bağımlılığıdır (Bölüm 7, r4 F-8). DL-2, DL-3 ve DL-4 DL-1'i, DL-4 ayrıca DL-3'ü
bekler (r14: DL-3 DL-2'yi beklemez — Bölüm 2.5). DL-6, MOD-0178'in FU-0178-RCS-01 geçişinden önce main'de olmalıdır. DL-7 DL-1'i bekletmez. DL-8, DL-6 ve
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
| A-8 | `ORG` / `POS` izinli kapsamı | LegalEntity · Group · ikisi | **KAPANDI — K-15: LegalEntity** (Beste 2026-11-10, `docs/records/decisions/2026-11/mod0040-fu02-owner-decisions-02.md:8` — r13: dosya bu dalda da vardır, ilk commit `6609910c8`); r12'de işlendi (Bölüm 4.4, 8.14); POS'un şirket kaynağı Q-5 ile teyit edildi (K-16) | Beste | 2026-11-10 |
| A-9 | Tür kaydı kod mu koleksiyon mu | — | **KAPANDI — K-12 (b)**: kodda | Beste | 2026-10-30 |
| A-10 | `platform-islem-ici` iz satırına `RecordCodeAuditIntent.*` belirteci; CT-DECISIONS; `AuditTrailStandardTests` sabiti | — | açık; kabul önerisi | CT | DL-1 main'e girmeden önce |
| A-11 | Dondurma ayrıntıları: `idempotencyKey` biçimi; Treasury ve MDM portuna `prefixScope`; Bölüm 4.4 izin adlarının FU27 ile teyidi (`APD` alacak dekontu dahil) | — | açık; taslaktaki gibi | Beste + Treasury CT + MDM sahibi | 2026-11-03 |
| A-12 | Kayıt satırı (Bölüm 0), `module-id-registry.md` "`MOD-0040` is reserved…" notunun düzeltilmesi, domain-config In-Scope satırı | — | açık | CT | DL-1 merge'ünden önce (CT kararı 2026-11-02: ready-for-dev önkoşulu değil, Bölüm 18.1) |
| A-13 | İç uç kimlik deseni | — | **KAPANDI — K-12 (e)**: P2 + azaltıcılar (Bölüm 8.2) | Beste | 2026-10-30 |
| A-14 | Ret denetimi | — | **KAPANDI — K-10** (Bölüm 21.2) | Beste | 2026-10-30 |
| A-15 | AUD-001 §3 satır 3 ile aktör | — | **KAPANDI — K-8r**: aktör kullanıcı, `SourceService` çağıran; çelişki yok | Beste | 2026-10-30 |
| A-16 | Hız sınırı değeri (bölüm: tüketici servis + kiracı) ve eski sır penceresi | değer · gerekçe | açık; CT önerisi 600 istek/dakika/bölüm, kuyruk 0; `PreviousValidUntilUtc` en çok 24 saat | Beste | 2026-11-03 |
| A-17 | 200 yanıt zarfı: v0 `{code, sequence}`'i en üst düzeyde yazıyordu; D59/1 ve Y-8 "yanıt dahil yalnız ekleme" der; öneri 1.0.0'ı `Response<T>` zarfının `data`'sında dondurmak | zarf · en üst düzey | açık; öneri zarf — "v0 yanıt biçimi bağlayıcı değildi" bir **öneridir**, teyit gelmeden karar sayılmaz. **Varsayılan (r7, son bakış 01 F-12):** teyit sözleşme dondurma adımına (2026-11-03) kadar gelmezse 1.0.0 **`Response<T>` zarfıyla** donar (`data = { code, sequence }`) — gerekçe: Platform'un bütün uçları bu zarfı döner, v0 yazılı sözleşme değildi ve `703cbaec7`'de `services/` altında `IRecordCodeAllocator` ya da `record-codes/allocate` geçen tüketici kodu yok (ölçüldü; başka dallar ölçülmedi — Treasury dalında bağdaştırıcı varsa onu bu varsayılan değiştirir, Treasury CT'ye bildirilir); Treasury sonradan en üst düzey biçimi isterse bu bir **büyük sürüm** değişikliğidir (sözleşme §13), dondurma adımı varsayılanın uygulandığını kayda yazar | **Treasury CT** (teyit); varsayılanın uygulanması CT (dondurma) | 2026-11-03 (dondurma) |
| A-18 | RCS-001 §5.4 kısa kod kalıcı kullanım kilidi (MDM tarafı, DL-5) — MDM'de kısa kod bugün değiştirilebiliyor (`UpdateLegalEntityHandler.cs:36-37`) | MDM'de kilit · MDM'de değişiklikte uyarı · yalnız Platform azaltıcısı | açık; ara dönemde Platform azaltıcısı (Bölüm 8.13, 409 `LEGAL_ENTITY_SHORT_CODE_CHANGED`). **r13 (K-17, inceleme 04 F-4): DL-2'nin canlıya alma kapısıdır** — DL-2 kodunu bekletmez, canlıya almayı bekletir; merge için CT kararı açık (Bölüm 18.1); hedef tarih geçti, yeni tarih sahipten | MDM sahibi (Ali) + Beste | 2026-11-06 (geçti; DL-2 canlıya almadan önce) |
| A-19 | MDM lookup-validation v1.1 (`approvalStatus`) yayını ve bu serviste "alan yok → 503" geçişi | — | açık bağımlılık; o güne kadar TC-3 zorunlu (FU-RCS-15) | MOD-0220 sahibi + Beste | TBD |
| A-20 | (r14, DL-3) `platform-islem-ici` iz satırına `RecordCodeAuditIntent.EnqueueClaimedAsync/EnqueueChangedAsync/EnqueueLockedAsync/EnqueueAssignmentRefusalAsync` belirteci; CT-DECISIONS satırı; `AuditTrailStandardTests` yeşil | — | açık; kabul önerisi (A-10 emsali — üç komut işleyicide, iş işlemiyle aynı işlemde yazar) | CT | DL-3 main'e girmeden önce |

### 24.1 DL-2 soruları (r12) — **r13: hepsi karar** (K-16)

r12'de "bekletir" işaretli sorular sahip kararlarıyla kapandı (Beste 2026-11-10, `docs/records/decisions/2026-11/mod0040-fu02-owner-decisions-03.md`; Q-6 MVP-1 CT + Beste). Seçilen
cevapların hepsi r12'nin önerisiyle aynıdır; bu yüzden "Q-n A" işaretli AC'ler öz olarak değişmedi, r13 yalnız "öneri" dilini
"karar"a çevirdi ve inceleme 04 adımlarını ekledi. Hiçbir soru DL-2 kodunu artık bekletmez; kod izni yalnız r13 inceleme + onay
satırıyla açılır (Bölüm 18.1).

| # | Soru | Seçenekler | **Karar** (kaynak) | Gerekçe (r12'deki öneri; değişmedi) | Sahip | Durum |
|---|---|---|---|---|---|---|
| Q-1 | Bugün elle girilmiş ORG / POS kodları ne olacak (RCS-001 §7) | **A** koru + çakışma savunması · **B** göç (eski kod `LegacyCode`'a, kayda otomatik kod — "ikisi birden" budur) | **A** — korunur (RCS-001 §2); çakışma savunması paketteki gibi: silinmişler dahil arama, en çok 3 deneme, sonra 409 (`docs/records/decisions/2026-11/mod0040-fu02-owner-decisions-03.md:9`) | RCS-001 §2 "Mevcut kodlar değiştirilmez" bağlayıcıdır (`record-coding-standard.md:42-43`); B §2'ye aykırıdır, yönetim kararı + `.antigravity` değişikliği (Ali) + ayrı revizyon ister. Sayaç çakışması: silinmişler dahil arama + en çok 3 deneme + kabulde kiracı başına sayım; 3'ü aşan durumda DL-6 (Bölüm 8.15) | Beste | **kapandı** 2026-11-10 |
| Q-2 | Güncellemede kod değiştirilebilir mi | **A** hiçbir kod değişmez (eski elle kodlar dahil) · **B** eski kodlar elle değişebilir, otomatik kodlar değişmez | **A** — hiçbir kod değişmez, eski elle kodlar dahil; kullanıcı için davranış değişikliği, sürüm notuna yazılır (`:10`; Bölüm 8.16) | RCS-001 §6 "API de reddeder" (`:157`); eski kodu düzenlenebilir bırakmak §5.9 çakışma riskini açık tutar ve aynı türe iki kural getirir. Bugün güncelleme kodu serbestçe değiştiriyor (`UpdateOrganizationUnitCommandHandler.cs:145`, `UpdatePositionCommandHandler.cs:49`) | Beste | **kapandı** 2026-11-10 |
| Q-3 | Ekran metinleri kaç dilde | **A** 7 dil · **B** 2 dil (görev metni) | **A** — 7 dil (formlar kiracı kabuğunda); CT talimatındaki "2 dil" CT'nin hatasıydı (`:11`) | formlar kiracı kabuğunda (`_LayoutTenantShell`, Bölüm 9), kaynak dosyaları bugün 7 dilde (ölçüldü); `localization-standard.md:8-9` 2 dili yalnız Platform yönetici modüllerine verir | Beste | **kapandı** 2026-11-10 |
| Q-4 | Tüzel kişilik onay ön kontrolü (TC-3) ORG / POS için | **A** MDM v1.1'e kadar yalnız aktiflik — adlı istisna · **B** DL-2'yi MDM v1.1 yayınına (A-19) bağla | **A** — MDM lookup-validation v1.1'e kadar yalnız aktiflik; adlı istisna (`:12`) | Platform `approvalStatus`'u bugün göremez (lookup-validation 1.0.0, Bölüm 7); ORG bugün de yalnız "referenceable" (aktif) ister (`CreateOrganizationUnitCommandHandler.cs:45-49`, MDM `LegalEntity.cs:60`); v1.1'de FU-RCS-15 onay denetimini servis tarafında bütün çağıranlara uygular | Beste (+ MOD-0220 sahibi bilgi) | **kapandı** 2026-11-10 |
| Q-5 | POS'un şirket kaynağı | birimin `LegalEntityId`'si (oluşturma anı) | **teyit** — birimin oluşturma anındaki `LegalEntityId`'si (`:13`); okuma yolu K-17 F-2 (b): ikinci `GetByIdAsync` (Bölüm 8.14) | A-8 karar metni POS'un şirketini adlandırmaz; `Position`'da şirket alanı yok (`Position.cs:7-9`), başka kaynak yok; taşımada kod değişmez (§5.2) | Beste | **teyit edildi** 2026-11-10 |
| Q-6 | Künye `draft` ve onay sırası (S-49) | **A** inceleme sonrası künye `ready-for-dev`'e çevrilir (yeni sha256'ya onay) · **B** r12 onaylanır, ardından yalnız künye revizyonu + yeni onay (r8 → r9 emsali) | **A** — bağımsız inceleme sonrası künye `ready-for-dev`; onay o sürümün sha256'sına (CT kararı, sahip onayladı) (`:14`) → **r13 künyesi `ready-for-dev`, `approval_status: pending`** | r12 `draft`'ta onaylansaydı onay satırı DL-2 kodunu yetkilendirmezdi (`scripts/pack_approval.py:1473-1474`); A bir tur kısa | MVP-1 CT + Beste | **kapandı** 2026-11-10; uygulandı (r13 künyesi) |
| Q-7 | XMC-001: MOD-0288 dosyalarına dokunma onayı (Bölüm 5 "DL-2" XMC satırları) ve MOD-0288-FU03 / FU04 ile sıra | onay · ret · kısmi | **onay** — yalnız r12 Bölüm 5 "DL-2" XMC satırlarındaki MOD-0288 dosyaları, başka dosya yok; onaylayan merkez sahibi Beste (`:15`). **Ek onay** (K-17 F-1, `…/mod0040-fu02-owner-decisions-04.md:8`): `InMemoryTenantOrganizationRepositories.cs`, yalnız dar arayüzlerin uygulanması | MOD-0288 künyesi `owner: platform-team`, FU02–FU04 `organization-governance-owner` — kişi adı yoktu; onay merkez sahibinden geldi | Beste | **onay kapandı** 2026-11-10; FU03 / FU04 ile **sıra açık** (CT, DL-2 WP'sinden önce ölçer) |

### 24.2 DL-3 soruları (r14) — sahibe

DL-3'ün kabul ölçütleri her sorunun **önerilen** cevabıyla yazıldı ve "Q-n A önerisi" diye işaretlidir. Cevaplar DL-3 kodunun başlama
kapısıdır (Bölüm 18.1); sahip öneriyi seçerse r14 metni değişmez ve onay satırı r14'e yazılabilir; farklı seçerse yeni revizyon.

| # | Soru | Seçenekler | Öneri ve gerekçe | Etkilediği | Sahip | Durum |
|---|---|---|---|---|---|---|
| Q-8 | DL-3 motoru hangi üretim türüyle ölçülür (bugün katalogda kullanılabilir İKİSİ DE türü yok: `PLN` Platform kapsamı ve çağıransız) | **A** `TTY` DL-3'te kataloğa girer (İKİSİ DE, Group, çağıran `Diten.Platform`, izin `platform.tasks.task-types.manage`); MOD-0024 dosyalarına dokunulmaz, bağlanma DL-4 · **B** DL-3 ile DL-4'ün TTY kısmı tek dilimde (motor ilk tüketicisiyle) · **C** motor test kataloğuyla ölçülür, üretim kataloğu değişmez | **A** — her AC üretim kataloğuyla ölçülür, MOD-0024 XMC onayı gerekmez; bedeli DL-1'in iki katalog testinin `TTY` satırı ve iç uçta `TTY`'nin 422 yerine 403 alması (sözleşme §13: yeni tür sözleşme değişikliği değildir). B dilimi büyütür ve XMC-001 onayı ister; C "kendi kopyasıyla ölçülen kural"dır (AGENTS Çekirdek 10) | AC-62; Bölüm 2.5, 4.4 | Beste | açık |
| Q-9 | Kayıt **oluşturulurken** öneriden farklı (elle) kod yazmak gerekçe ister mi | **A** evet — elle kod her zaman gerekçeli · **B** hayır — gerekçe yalnız sonraki değişiklikte | **A** — RCS-001 §2: "Sistem önerir … Evet [değiştirebilir], ama gerekçe ZORUNLU"; önerilen kodu başka bir kodla değiştirmek oluşturma anında da bir değişikliktir; B, gerekçesiz elle kod için bir kapı açar (oluştur → hemen elle kodla oluştur) | AC-64, AC-67; Tablo E | Beste | açık |
| Q-10 | Gerekçenin sınırı | **A** kırpılmış 10–500 karakter · **B** yalnız boş olmasın · **C** başka değer | **A** — "x" gibi bir gerekçe RCS-001'in amacını (izlenebilir değişiklik) boşa çıkarır; 500 denetim satırını şişirmeden bir cümleyi taşır. Değer sahibin | AC-64 | Beste | açık |
| Q-11 | Elle kodun normalleştirilmesi (RCS-001 §5.10 "elle girişte Türkçe harf ASCII karşılığına çevrilir") | **A** kırp + Türkçe harf → ASCII + büyük harf; başka her işaret reddedilir · **B** hiç normalleştirme yok, birebir uymayan reddedilir (DL-1 / DL-2 dili) · **C** A + boşluğu `-`'ye çevir | **A** — §5.10 çeviriyi açıkça ister; boşluk / işaret çevrilmez çünkü sessiz dönüşüm kullanıcının yazdığını değiştirir (DL-2 `RECORD_CODE_SYSTEM_ASSIGNED` gerekçesi); cevap kaydedilen kodu döner | AC-63 | Beste | açık |
| Q-12 | Elle kodun uzunluğu | **A** 2–40 · **B** başka aralık | **A** — 40 MOD-0024'ün bugünkü sınırıdır (`TaskTypeRules.CodeMaxLength`); 2 RCS-001 §5.4 kısa kod alt sınırıyla aynı. PPM sınırı DL-4'te ölçülür; daha uzun gerekirse katalog tür başına sınır alır | AC-63 | Beste | açık |
| Q-13 | Benzersizliğin kapsamı (RCS-001 §6 "kiracı genelinde (ya da gerekçeli kapsam)") | **A** (kiracı, tür) — `TTY` `DENETIM` ile `PRJ` `DENETIM` birlikte olabilir · **B** (kiracı) — bütün İKİSİ DE türlerinde tek | **A** — kod her zaman türünün ekranında ve türüyle anılır; tüketici koleksiyonlarının kendi tekilliği de tür başınadır; sistem kodları türü taşıdığı için zaten çakışmaz. B, birbirinden bağımsız ekip adlarını (görev türü ↔ proje) gereksizce yarıştırır | AC-67, AC-71 | Beste | açık |
| Q-14 | Bir kayıt **kendi** emekli koduna geri dönebilir mi | **A** hayır — emekli kod herkes için dolu · **B** evet — yalnız aynı kaydın kendi emekli kodu yeniden etkinleşir (gerekçeyle) | **A** — RCS-001 §5.8 "asla yeniden kullanılmaz" en basit okumasıyla; B geçmişte aynı kodun iki dönemini doğurur ve emekli satırı yeniden etkinleştirme yolu ister (defter yalnız ekleme / emeklilik / kilit). Yanlış değişiklik yeni bir kodla düzeltilir | AC-68 | Beste | açık |
| Q-15 | DL-3'ün yüzeyi | **A** yalnız süreç içi port; HTTP, Gateway, ekran yok · **B** DL-3'te genel bir öneri ucu (`GET /api/platform/record-codes/preview`) + yeni izin anahtarı | **A** — DL-3'te tüketici yok; genel bir değişiklik ucu tüketici kaydını atlayıp kodu değiştirebilirdi (kod–kayıt tutarsızlığı); öneri ucu tüketici ekranının kendi controller'ında daha doğru yerde (kayıt kapsamı, `[HasPermission]`). B yeni anahtar → PKS-001 kataloğu, rol köprüsü ve 7 dil dosyası ister. Süreç dışı tüketiciler (PPM, MDM) için HTTP yüzeyi onları bağlayan dilimde, sözleşme ekiyle | Bölüm 2.5, 9, 14, 15; AC-73 | Beste | açık |
| Q-16 | Tüketici kaydı ile motor arasındaki tutarlılık (motor başarılı, tüketicinin kendi yazımı düştü) | **A** DL-4'te tüketiciyle birlikte karar (oturumlu port ya da onarım) · **B** DL-3'te oturum alan port aşırı yüklemeleri | **A** — DL-3'te tüketici olmadığı için B ölçülemez (kullanılmayan kod); DL-2 aynı soruyu tüketici yanında çözdü (iki yazım, sabit sıra) | Bölüm 8.17 | Beste | açık |
