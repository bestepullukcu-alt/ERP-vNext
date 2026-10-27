---
decision_id: TREASURY-INTAKE-OWNER-DECISIONS-02
status: approved — D9–D35 (D16–D31: 2026-10-25, D32–D35: 2026-10-26) itibarıyla yürürlükte; "Karar olmayanlar" bölümü karar değildir
decided_at_local: 2026-10-24
decided_by: depo sahibi (rol, K1); bu kayıt ad yazmaz
decision_source: Treasury Intake Control Tower oturumu (Claude Code); sahibin mesajları "treasury modüllerin çalışması için gereken değişiklikleri yapabiliriz" ve "tamam, başlayalım" (2026-10-24)
scope: Treasury intake için başka modüllerde gereken değişiklikler (XMC-001); ilk somut örnek MOD-0220
bound_to: dal `chore/ct/treasury-intake-ct-01`, taban origin/main 96bedc89374dd8fd06550c49ae2fc65ca07e6298
recorded_by: Treasury Intake Control Tower; bu kayıt kendi adına karar vermez, onay, paket ya da dağıtım izni değildir
---

# Treasury intake — sahip kararları 2 (2026-10-24)

## D9 — Treasury için başka modüllerde gereken değişiklikler (XMC-001 onayı)

Sahip, Treasury modüllerinin çalışması için başka modüllerde gereken değişikliklere onay verir. Şartlar:

1. Değişiklik **gereken en küçük ve eklemeli** değişikliktir; var olan tüketicilerin davranışı değişmez.
2. Önce o modülün **pack'ine** değişiklik önerisi olarak yazılır; pack değiştiği için bağımsız inceleme (CT-APR-02) ve
   sürüme bağlı onay (CT-APR-01) gerekir. Kod ancak o pack ve ilgili Treasury pack'i `ready-for-dev` olunca yazılır.
3. Başka bir domain'in sahibi varsa (ör. MDM) karar ona sorulur; bu onay, sahibin cevabının yerine geçmez, sorulmasını
   ve hazırlanmasını yetkilendirir.
4. Aynı dosyayı başka bir Control Tower değiştiriyorsa (ör. MOD-0140, MVP-2) yazılmaz; tek yazar kuralı (K15).

**İlk uygulama — MOD-0220** (TRM-S1, MOD-0126 ile birlikte):
- `GET /api/legal-entities/{id}/lookup-validation` yanıtına `ApprovalStatus` alanının **eklenmesi** (alan kayıtta var,
  `LegalEntity.cs:50`; 404 davranışı değişmez).
- MOD-0220 sözleşme belgesi (mevcut `GET` ve `lookup-validation` uçları) — belge, kod değil.
- Şirketler arası ilişki modeli **eklenmez** (bugün hiçbir Treasury modülü istemiyor; tedarikçi–şirket ilişkisi
  Recovery'de MOD-0140'tadır).

## D10 — Üç karar (2026-10-24, sahibin soru kutusundaki seçimleri; üçü de önerilen seçenek)

1. **Ödeme yapan şirket hem aktif hem onaylı olmalı.** Soru: "Aktif ama onay durumu 'onaylandı' olmayan bir şirket
   adına ödeme yapılabilsin mi?" — Cevap: "Hayır, onaylı olmalı". Kontrolü MOD-0126 yapar (`OperationalStatus = Active`
   ve `ApprovalStatus = Approved`); MOD-0126 pack'inde kabul ölçütüdür.
2. **MOD-0220 eki onaylandı.** Cevap: "Evet, ekle" — `lookup-validation` yanıtına `ApprovalStatus` alanı (yalnız ekleme;
   404 davranışı değişmez) + MOD-0220 sözleşme belgesi. MOD-0220 pack değişikliği önerisiyle, CT-APR-02/01'den geçer;
   kod MOD-0126 ile birlikte (TRM-14). Sahip burada MDM domain sahibinin kararını kendisi verdi (sahip K1).
3. **Port 5071.** Cevap: "5071". Ölçüm (2026-10-24, yapılandırma dosyaları): 5056–5070 kullanımda (5069
   `SafetyStockService` — AGENTS §3'te yok, ayrı kayma); 5071 ve sonrası hiçbir yapılandırmada yok. Sahip burada EA
   kararını kendisi verdi (AGENTS §3; geçmiş tahsisler de "kullanıcı onaylı"). **AGENTS.md §3 ve
   `.antigravity/rules/ports.md` (korunan yol; bant 5011–5064 yazıyor) değişikliği ayrı onaydır**; bu kayıt yapmaz.

## D11 — MOD-0126 pack kararları (2026-10-24, soru kutusu; dördü de önerilen seçenek)

1. **SoD tier-2 eşiği (O-4):** "Kiracı ayarı, para birimli" — her kiracı eşiğini ve para birimini ayarlar; ayar yoksa
   her onay ikinci kademe ister; farklı para birimindeki parti ikinci kademeye gider.
2. **AdminDirect (PP-05, O-1):** "Hayır, düşür" — tüm onaylar MOD-0023 iş akışı ve SoD kontrolünden geçer.
3. **K2 (O-6):** "Evet, K2" — onay, red ve muafiyet komutları fail-closed; denetim kaydı iş verisiyle aynı işlemde.
4. **İzin öneki (O-5):** "finance-treasury.*".

## D12 — MOD-0126 gap listesinden doğan kararlar (2026-10-24, soru kutusu; ikisi de önerilen seçenek)

1. **Muafiyet kapsamı (O-17):** "Yalnız tier-2 izni" — muafiyet yalnız R4'ü kaldırır; oluşturan, gönderen ve son
   güncelleyen asla onaylayamaz; aday seçimi, karar kaydı ve Release aynı modeli uygular.
2. **Idempotency (O-18):** "Tüm yazma komutlarında zorunlu" — anahtar yoksa 400; sunucu anahtar üretmez; makbuz kapsamı
   kiracı + şirket + aktör + işlem + hedef.

## D13 — Muafiyet adayı (2026-10-24, soru kutusu; önerilen seçenek)

"Yalnız tier-2 aday yoksa" — normalde yalnız tier-2 izni olanlar aday; R1–R3 dışlamasından sonra kiracıda hiç tier-2
aday kalmıyorsa `waive-sod` izni olanlar muafiyet adayı olarak eklenir ve yalnız muafiyetli onay verebilir (gerekçe +
kanıt). Normal onay ve muafiyet yetkisi ayrı değerlendirilir.

## D14 — Dört Treasury pack'i için ortak kararlar (2026-10-24, soru kutusu; dördü de önerilen seçenek)

1. **Veri kapsamı (SEC-002):** "Yetkili olduğu şirketler" — kapsam alanı `LegalEntityId`; kullanıcı yalnız yetkili olduğu
   şirketlerin kayıtlarını görür; kiracıdaki tüm şirketleri görmek ayrı, yalnız açık atanan `…read-tenant-wide` iznidir
   (MOD-0126, MOD-0119, MOD-0120, MOD-0124).
2. **Makbuz ve niyet tipi:** "Tek ortak tip" — servis genelinde tek `FinanceTreasuryCommandReceipt` ve ortak niyet
   altyapısı; dört pack bunu kullanır; kurallar MOD-0126'daki gibi (D12/2, gap listeleri 1–3).
3. **AP/AR K2:** "Evet" — AP ve AR'da parayı etkileyen kararlar (onay, red, iptal/silme, elle ödeme kaydı) K2.
   Cash Positioning yayımı K2 değildir (para yetkisi değil; yine aynı işlemde yazılır).
4. **Şirket kuralı (D10/1) genişletmesi:** "Evet, yazma işlemlerinde" — AP, AR ve Cash Positioning'de borç, alacak ya da
   nakit pozisyonu oluşturan/değiştiren her işlemde şirket aktif ve onaylı olmalı; okumalar etkilenmez.

## D15 — Pack düzeltme kararları (2026-10-24, soru kutusu; ikisi de önerilen seçenek)

1. **MOD-0120 teslim dilimleri:** "DL-4'ü DL-3 ile birleştir" — elle tahsilat kaydı (RecordPayment) DL-3'te, iptal/silme
   onayıyla birlikte; eski DL-5 (ekran) DL-4 oldu; O-9 (elle tahsilat bu sürümde mi) açık kalır.
2. **Reconcile muafiyeti:** "Evet, muaf" — belirsiz sonucu kapatan Reconcile komutları "şirket aktif ve onaylı" kuralından
   muaftır (D10/1, D14/4); yeni işlem başlatmazlar, kontrol edilmiş işlemin uzak sonucunu kaydederler (dört pack).

## D16 — AR elle tahsilat (O-9) (2026-10-25, soru kutusu)

Soru: "AR'de elle tahsilat kaydı (O-9) bu sürüme dahil olsun mu?" — Cevap: **"Evet, kurallarıyla dahil"** (önerilen
seçenek "Hayır, sonraki sürüm" idi; sahip dahil etmeyi seçti). Elle tahsilat AR v1'de (DL-3) kalır; şunlarla birlikte:
ödeme referansı için iş tekilliği (kiracı + şirket + müşteri + referans), mükerrer tahsilat kontrolü (farklı anahtar ya da
aktörle aynı ödeme) ve onaylı geri alma (reversal / karşı kayıt) akışı.

## D17 — MOD-0269 BANK-DISPATCH sözleşmesi (gap listesi 7) (2026-10-25, soru kutusu; ikisi de önerilen seçenek)

1. **Kaynak hesap:** "Talimat düzeyinde ref" — her `BankDispatchInstruction` kendi `sourceBankAccountRef`'ini taşır
   (MOD-0126 `PaymentInstruction.SourceBankAccountRef`, olduğu gibi); parti düzeyindeki alan kalkar; port ve bağdaştırıcı
   talimatları birleştirmez, gruplamaz, tek hesaba indirgemez.
2. **Beklenmeyen sonuç:** "Kaydet, kilitle, başarı sayma" — 0.1 tüketicisi `NotConfigured` dışı bir sonuç alırsa deneme
   alınan sonuçla kaydedilir, parti `Approved` kalır ve `ReleaseBlockedForReview` ile kilitlenir, Release 502
   `PAYMENT_BANK_DISPATCH_OUTCOME_UNSUPPORTED` döner; sonraki Release 409. Kilidi açma komutu Follow-up'tır.

## D18 — Onay PR'ının zamanı (2026-10-25, soru kutusu; ikisi de önerilen seçenek)

1. **PR zamanı:** "S1+S2 hazır olunca" — tek belge PR'ı (kod yok) MOD-0126, MOD-0119, MOD-0120, MOD-0124 pack'leri ve
   MOD-0269 BANK-DISPATCH sözleşmesi için, ancak TRM-14 (MOD-0220 eklemeli değişiklik) yazıldıktan ve MOD-0023 / MOD-0143
   üretici cevapları pack'lere işlendikten sonra açılır. MOD-0289, MOD-0217, MOD-0057 sonraki ayrı PR'dadır.
2. **Yedek push:** "Hayır, yerelde kalsın" — dal o zamana kadar push edilmez.

## D19 — MOD-0217 ve MOD-0057 (TRM-13) (2026-10-25, soru kutusu; ikisi de önerilen seçenek)

1. **MOD-0217 Contract Lifecycle Management (CLM):** "MDM'e yerleş, sonra pack" — yer `master-data-management`
   (kaynakta eski kimlik MDM-070; Blueprint yetenek grubu Legal Governance; kimlik `verify_module_id.py --check-id
   MOD-0217 --name "Contract Lifecycle Management (CLM)"` → exit 0, 2026-10-25). Pack ancak TRM-11 cevabı (kaynaktaki 6
   kırık ve 18 koşmayan işlem testinin nedeni, S mi R mi) geldikten ve 'Document Management' kapısı bir modüle
   çözüldükten sonra yazılır; MDM domain sahibinin onayı gerekir (MOD-0289 ile aynı yol, TRM-10). Treasury S1/S2 buna
   bağlı değildir.
2. **MOD-0057 Semantic Tagging & Taxonomy Management:** "Park et" — bu intake'ten çıkar; level-1 kaydı
   (`treasury-level1-acceptance-01/MOD-0057-v1.0.md`) kalır. MOD-0053 ve MOD-0056 Recovery'de olunca yeniden açılır
   (Blueprint W-4). Kimlik exit 0 (2026-10-25).

## D20 — MDM yerleşimi (TRM-10) (2026-10-25, soru kutusu; ikisi de önerilen seçenek)

Sahip (K1) burada MDM domain sahibinin kararını kendisi verdi (D10/2 ile aynı yol; depoda MDM sahibi kişi olarak
adlandırılmamış, MOD-0220 pack'inde `owner: mdm-domain-team`).

1. **MOD-0289 Bank Master & Bank Account Registry:** "MDM" — `master-data-management` / `Diten.MdmService`; yeni servis
   ve port yok. D7'deki "MDM sahibinin onayı" koşulu kapandı; kimlik kaydı satırı ve pack taslağı açılabilir
   (`MOD-0289-PLACEMENT-NOTE-v0.1.md`).
2. **MOD-0217 CLM:** "MDM, pack koşullu" — D19/1 teyit edildi; pack TRM-11 cevabı ve Document Management
   bağımlılığı çözülünce.

## D21 — MOD-0023 ve Treasury açık onayları (2026-10-25, soru kutusu; altısı önerilen seçenek, biri sahibe bırakıldı)

Sahip (K1), platform kod sahibine (Ali) gönderilen üç sorunun ikisini D9 yetkisiyle kendisi cevapladı; MOD-0143 sorusu
Cihan'da kaldı. Bunlar **politika kararıdır**; MOD-0023 kodunun bugün böyle davrandığını kanıtlamaz — kanıt testle gelir.

1. **MOD-0023 saklama:** "Hiç silinmez" — `WorkflowInstance` ve `WorkflowTransitionLog` silinmez, TTL konmaz; kural
   MOD-0023 pack'ine yazılır ve mimari testle korunur. Ölçüm (@0baa556d6): bu iki tip için silme yolu ve `ExpireAfter`
   yok; `WorkflowRepositories.cs:961-974`'teki soft delete yalnız `SlaEscalationRule` içindir.
2. **Silinmişi ayırt eden okuma:** "Gerekmez, politika yeter" — 1. madde mimari testle korunduğu sürece Treasury'nin
   yeniden yazma kapısı için ayrıca silinmiş okuması istenmez (gap 6 kaydındaki "daha gevşek seçenek"). Silinmiş kayıt
   yine de görülürse yeniden yazma yok, niyet `Unknown` (dört pack'teki kural aynen kalır).
3. **Okuma eşleme alanları:** "Evet, eklemeli" — `WorkflowInstanceStatusDto`'ya `IdempotencyKey`, geçiş okumasına
   `TaskId` + `Action` + `IdempotencyKey`; yalnız ekleme; MOD-0023 pack değişikliği (TRM-14 deseni), kod MOD-0126 DL-2 ile.
4. **Aynı anahtarlı yarış:** "Aynı kaydı dön" — kaybeden istek tekil indeks çakışmasını yakalar, var olan örneği /
   geçişi okuyup aynı sonucu 200 ile döner. Bugünkü kod sıralı tekrarda kayıtlı sonucu dönüyor
   (`StartWorkflowInstanceHandler.cs:76-85`); **eşzamanlı** yarış davranışı ölçülmedi — gerçek Mongo yarış testiyle kanıtlanır.
5. **Reconcile yeniden yazma aktörü:** "Evet, yalnız asıl aktör" — gap 4'te ajanın eklediği kural onaylandı
   (`…_RECONCILE_RESTART_ACTOR_MISMATCH` 409); okuyup sonucu kaydetme yolu herkese açık kalır.
6. **AR geri alma izni:** "Mevcut izinler" — `receivable-adjustments.*` ve SoD A7; ayrı izin anahtarı yok.
7. **"Kesin ret" tanımı:** "tx2 eşlemesi, yinelenen anahtar hariç" — yalnız pack'lerdeki tx2 tablosundaki kesin iş
   reddi kodları `Refused`; yinelenen anahtar, 404, zaman aşımı, 5xx hiçbir zaman `Refused` değildir (`Unknown`).
8. **MOD-0143 `totalAmount` net/brüt:** "Cihan'a bırak" — kod sahibinin ölçümü beklenir; o zamana kadar AP'de PO'lu
   faturanın onayı `AP_MATCH_AMOUNT_BASIS_UNCONFIRMED` (422) ile kapalı kalır.

## D22 — TRM-15 gap listesi 11 kararları (2026-10-25, soru kutusu; ikisi de önerilen seçenek)

1. **MOD-0126 Escalate:** "v1'den çıkar" — MOD-0023'te görev düzeyinde escalate ucu yok (ölçüldü: yalnız SLA işi
   `POST escalations/run`, PlatformAdmin). `EscalatePaymentBatchCommand`, izni, denetim olayı ve uçları v1'de yok;
   `Escalated` (7) enum'da korunur ama girişi yoktur; yükseltme MOD-0023 SLA kurallarıyla olur.
2. **Yarış testleri geçmezse:** "Ek bloke, kapsam ayrıca" — MOD-0023 §17 yazma yolu değişikliğine izin vermez; AC-W5…W8
   bugünkü kodla geçmezse §17 bloke kalır, gereken düzeltme ayrı bir §17 revizyonuyla (yeni onay) kapsamlanır; Treasury
   yeniden yazma kapısı o zamana kadar kapalı.
3. **D21/4 açıklaması (gap 11/P1, CT; karar değişikliği değil):** D21/4'teki "aynı sonucu 200 ile döner" **son yanıttır**:
   ara 503 "aynı anahtarla tekrar" son yanıt değildir; bütünleşmiş başlatmanın / kaydedilmiş geçişin tekrarı örnek
   sonradan kapansa da 200 ve aynı kayıttır; hiç bütünleşmemiş, kurtarmanın kapattığı başlatma ve aynı göreve farklı
   karar çatışması tanımlı 409'dur (MOD-0023 pack §17.3/4 tablosu). Sahip bu yorumu gap 11 kapanış incelemesinde onaylar.

## D23 — MOD-0289 pack kararları (2026-10-25, soru kutusu; dördü de önerilen seçenek)

1. **O-9 tedarikçi hesabı kapsamı:** "İlişkili şirketler" — liste, detay ve lookup yalnız tedarikçiyle aktif şirket ilişkisi
   (MOD-0140 sözleşmesi) olan şirketin kullanıcısına; ilişkisiz kullanıcı 404. Maskesiz okuma ayrıca izin + ilişki ister.
   `read-tenant-wide` izni tüm tedarikçi hesaplarını **maskeli** gösterir.
2. **O-1 onay akışı:** "Hayır, dört göz" — v1'de MOD-0023 akışı yok; hesabı oluşturan ya da son güncelleyen etkinleştiremez.
3. **O-2 şirket durumu:** "Aktif; etkinleştirmede +Onaylı" — oluşturmada şirket `Active`; etkinleştirmede ve lookup'ta
   `Active` + `Approved` (D10/1 ile aynı çizgi).
4. **O-3 maskesiz okuma:** "Ayrı izin + gerekçe" — `mdm.bank-accounts.read-unmasked`, yalnız açık atama (rol şablonunda
   varsayılan değil), her okumada gerekçe kodu, değer dönmeden önce denetim kaydı.

## D24 — MOD-0289 kalan açık kararlar (2026-10-25, soru kutusu; sekizi önerilen seçenek, O-10 sahibin kendi seçimi)

1. **O-4 tedarikçi hesabı doğrulama kanıtı:** "Doğrulama kodu + dört göz" — etkinleştirmede zorunlu kod
   (`CALLBACK_CONFIRMED` / `BANK_LETTER_ON_FILE` / `SUPPLIER_PORTAL_CONFIRMED`) ve farklı kişi; MOD-0031 kanıt bağlantısı
   MOD-0269 gerçek gönderiminden önce zorunlu kapı.
2. **O-5 denetim yolu:** "Güvenilir niyet yolu, K2" — MDM'in kapalı-başarısız `mdm-guvenilir-kaynak-niyeti` yolu genel
   `MasterDataAuditOperation` ile genişletilir; hesap yazmaları ve maskesiz okuma K2. Defter belirteci CT kararı (ayrı).
3. **O-6 tam tanımlayıcının saklanması:** "Uygulama düzeyi AES-GCM" — pack'teki tasarım (AES-256-GCM, rastgele 96-bit
   nonce, AAD = kiracı + hesap + sürüm, sürümlü anahtar, ayrı sürümlü HMAC özet, anahtar seti fence'i) **sahip tarafından**
   onaylandı. **Güvenlik onayı ve gerçek Mongo testleri ayrıca açıktır**; bu karar onları kapatmaz.
4. **O-7 kayıt kodları:** "BNK / BAC, OTOMATİK" — banka `BNK` (grup öneki), hesap `BAC` (şirket hesabında şirket öneki,
   tedarikçide grup öneki); şube kodu doğal anahtar. `.antigravity` satırı ayrı onaydır.
5. **O-8 ülke kodu:** "Yalnız ISO biçim kontrolü" — ISO 3166-1 alfa-2 biçimi; MDM iş ülkesi kaynağı gelince bağlanır.
6. **O-10 `AccountPurpose`:** "Kod listesi + kiracı ekler" (önerilen kapalı liste değil) — sabit çekirdek kodlar
   (`OPERATING`, `PAYROLL`, `COLLECTION`, `TAX`, `SUPPLIER_PAYMENT`, `OTHER`) + kiracının ekleyebildiği ek amaçlar
   (kiracı referans listesi, 7 dilde etiket).
7. **O-11 Bankalar ekranı:** "Compact" — Details'ta şube alt tablosu.
8. **O-12 küsurat hanesi:** "MOD-0048" — MOD-0289 yalnız ISO kodu saklar ve MOD-0048 listesinde olduğunu doğrular.
9. **Kiracısız anahtar seti hedef kaydının denetimi:** "Yerel geçmiş + kiracı fence kaydı" — hedef kaydın ekleme-yalnız
   geçmişi ve her kiracının fence oluşturma / ilerletme denetim kaydı hedef sürümünü, revizyonu ve `ApprovalRef`'i taşır;
   başka modül değişmez; merkezi kiracısız kayıt Follow-up.

## D25 — MOD-0023 üretici cevabına göre (2026-10-25, soru kutusu; üçü de önerilen seçenek)

Kaynak: `docs/records/audits/2026-10/treasury-intake-handover-review-01/PRODUCER-ANSWER-MOD0023-01.md`.

1. **§17 teslimi:** "MOD-0023 sahibinin WP'si" — MOD-0023 pack §17 (TRM-15) MOD-0023 sahibinin WP'sinin şartnamesidir;
   kodu (silmeme politikası + guard, eklemeli okuma alanları, fingerprint düzeltmesi + yarış testleri) sahip yazar ve merge
   eder. D22/2'deki "§17 yazma yolu değişikliğine izin vermez" kuralı bu WP için geçmez: fingerprint düzeltmesi sahibin
   WP'sinde, kendi onayıyla yapılır. Treasury DL-2 / DL-3 (MOD-0023 kullanan dilimler) bu WP main'e girmeden kabul edilmez.
2. **`isDeleted` okuma alanı:** "Evet, kabul" — by-objects ve history `isDeleted` döner (by-objects'te `includeDeleted=true`);
   D21/2 ("gerekmez") bununla genişler; Treasury Reconcile `includeDeleted=true` ile okur, silinmiş kayıt görülürse
   yeniden yazmaz.
3. **Geçişte gövde farkı:** "Evet, fark → 409" — aynı anahtar + farklı gövde geçişte de 409; yanıtta `isIdempotent` /
   `idempotentReplay` bayrağı.

CT uygulaması (karar değil, D21/7'nin uygulaması): eşzamanlı aynı anahtarlı geçişte 409 `WORKFLOW_TRANSITION_CONFLICT` ve
503 `WORKFLOW_TRANSACTION_UNAVAILABLE` Treasury'de kesin ret değildir → `Unknown`.

## D26 — MOD-0143 `totalAmount` tabanı ve AP kuralı (2026-10-25, soru kutusu; üçü de önerilen seçenek)

Kaynak: `docs/records/audits/2026-10/treasury-intake-handover-review-01/PRODUCER-ANSWER-MOD0143-01.md`.

1. **Tutar tabanı:** "Net" — MOD-0119'da PO'lu fatura kuralı `Σ Line.NetAmount = totalAmount` (MOD-0143 `Decimal`
   string'i `decimal`'a, yuvarlamasız); vergi (`TaxAmount`) AP'de tutulur. `AP_MATCH_AMOUNT_BASIS_UNCONFIRMED` kapısı kalkar.
2. **Kabul edilen MOD-0143 durumları:** "Yalnız Matched + MatchedWithinTolerance" — `ClearedForPayment` (tolerance-override ile
   gelmiş olabilir), `Captured`, `Exception`, `Rejected` → 422 `AP_MATCH_OUTCOME_NOT_ELIGIBLE`; sözleşme çözüm türünü
   (override mı) göstermeden `ClearedForPayment` açılmaz.
3. **Procurement değişiklikleri:** "Evet, ikisi de" — sahip, MOD-0143 sahibinin önerdiği iki değişikliği onayladı:
   `invoice-match.openapi.yaml` 1.1.0 → 1.1.1 (yalnız açıklama: `totalAmount` / `lineAmount` vergi hariç, vergi taşınmaz;
   %20 KDV'li örnek) ve MOD-0143 kodunda kayıt sırasında `lineAmount ≠ quantity × unitPrice` → 422 + sabotajlı test. Yapılışı
   procurement sahibinin kendi dalında; push / PR sahibin onayıyla. Bu Control Tower o dosyalara dokunmaz.

## D27 — Açık kapı kararları ve onaylar (2026-10-25, soru kutusu; on ikisi de önerilen seçenek)

1. **Karar 0.3 — MOD-0003 Data Contract Registry:** "Süreli T32 sapması" — Recovery'de MOD-0003 yok (kayıt satırı
   `planned / missing`, 0 dosya). Treasury sözleşmeleri `docs/analysis/contracts/` altında dondurulmuş dosya olarak tutulur;
   MOD-0003 gelince oraya kaydedilir. Süre: MOD-0003 çıkana kadar, en geç Treasury v1 sonu.
2. **Karar 0.5 — AR fatura sınırı:** "Kısıtlı MVP, yerel" — AR alacak faturasını yerel tutar; faturalama sisteminden
   (MOD-0169 / AR-BILLING-BUNDLE) almaz, müşteriye göndermez, yasal / e-fatura numarası üretmez (sapma S-2).
3. **Karar 0.6 — ERP-FIN-BUNDLE:** "v1'de yok, modül sözleşmeleri" (sahip EA kararını verdi) — her modül kendi sözleşmesini
   yayımlar; ortak paket GL (0.7) gelince ayrı karar.
4. **Karar 0.7 — GL (MOD-0118):** "Kapsam dışı" — Treasury v1 fiş / yevmiye üretmez, `VoucherReference` taşımaz.
5. **D1 kod alma izni:** "Dört Treasury modülü" — MOD-0126, MOD-0119, MOD-0120, MOD-0124 için kaynak kod (S `da4abb20`)
   doğrulanıp uyarlanarak alınabilir. Kod yine ancak pack `ready-for-dev` ve dilim sırasıyla yazılır. MOD-0289 ve MOD-0269
   için ayrı izin gerekir.
6. **Port 5071 — AGENTS.md §2/§3/§9 ve `.antigravity/rules/ports.md`:** "Yamayı hazırla, göster" — CT yamayı hazırlar,
   uygulamadan önce sahibe gösterir (korunan yol; uygulama ayrı onay).
7. **MOD-0269 BD-1 / BD-4:** "rail'i çıkar, 0.1.0'ı dondur" — `rail` 0.1'den çıkar (sonraki sürümde eklemeli gelir);
   sözleşme `docs/analysis/contracts/bank-dispatch.md` FROZEN 0.1.0 olarak yayımlanır.
8. **MOD-0269 BD-3 — bankacılık sahibi:** "Sahip (K1), şimdilik" — gerçek sağlayıcı seçimi (karar 0.1 B/C) yapılınca ayrı sahip.
9. **MOD-0289 O-13:** "Compact" — hesap amaçları ekranı Compact.
10. **RCS-001 sözlük kodları (CT teyidi, sahip onayıyla):** "Hayır, doğal anahtar" — kiracı sözlük kodları (ör. hesap amacı
    kodu, `LegalFormCode` gibi) RCS kodu almaz; RCS-001 kayıt kodları içindir.
11. **MOD-0289 O-5 denetim defteri (CT kararı, sahip onayıyla):** "Yeni defter satırı, K2" — MOD-0289 komutları audit-ledger'a
    K2 olarak, `mdm-guvenilir-kaynak-niyeti` yol belirteciyle eklenir; borç satırı yok; CT-DECISIONS kaydı kod aşamasında.
12. **MOD-0149 çelişkisi:** "Sahibine not yaz" — commercial-suite sahibine bildirim taslağı; Treasury kaydı değiştirmez.

## D28 — Treasury pack'lerinde sahibe ait açık kararlar (2026-10-25, soru kutusu; önerilen seçenek)

1. **Toplu karar:** "Tüm önerileri kabul et" — dört Treasury pack'inin "Açık kararlar" tablosunda sahibi yalnız sahip (K1)
   olan ve henüz karara bağlanmamış her satır, pack'te yazılı **öneriyle** karara bağlandı. Başka sahipli ya da ortak sahipli
   satırlar (MOD-0023, MOD-0018, MOD-0048, Platform, CT + Platform, MOD-0149, procurement) açık kalır. Sahip sonradan tek tek
   değiştirebilir; değişiklik yeni karar kaydıyla yapılır. Karara bağlanan satırlar:
   - MOD-0119: O-4, O-6, O-7, O-13, O-14, O-15, O-16, O-17, O-19
   - MOD-0120: O-5, O-7, O-10, O-11, O-12, O-13
   - MOD-0124: O-1, O-4, O-6, O-11, O-12, O-13, O-15, O-18, O-21
   - MOD-0126: O-2, O-3, O-12, O-13, O-14, O-15, O-16
2. **MOD-0120 sapma S-2 bitişi:** "MOD-0169 ready-for-dev ya da v1 sonu" — faturalama modülü (MOD-0169) `ready-for-dev`
   olunca ya da en geç Treasury v1 sonunda S-2 gözden geçirilir (T32 ile aynı çizgi).

## D29 — Ortak sahipli kararlar ve RCS önekleri (2026-10-25, soru kutusu; ikisi de önerilen seçenek)

1. **Ortak sahipli kararlar:** "Öneriyi kabul, diğerinin teyidi açık" — sahibin payı pack önerisiyle karara bağlandı;
   diğer sahibin (platform-shared-services, MOD-0023, MOD-0018, commercial-suite, tüketiciler) teyidi ready-for-dev engeli
   olarak kalır. MDM ortaklı kararlar tamamen kapanır (sahip MDM sahibi rolünü D20'de üstlendi).
2. **RCS-001 önekleri:** "Önekleri kabul, yama hazırla" — `PAY` (ödeme partisi), `APD` (AP belgesi), `ARI` (AR faturası),
   `CPS` (nakit pozisyonu), `CTP` (eşik politikası), `BNK` (banka), `BAC` (banka hesabı); OTOMATİK sınıf. `.antigravity`
   RCS-001 §3 tablosu yaması hazırlanır, uygulamadan önce sahibe gösterilir (korunan yol).
   Karara bağlanan satırlar:
   - MOD-0119: O-9, O-20
   - MOD-0120: O-3, O-6, O-15
   - MOD-0124: O-3, O-5, O-7, O-9, O-10, O-14, O-17
   - MOD-0126: O-7, O-9, O-10

## D30 — Korunan yol yamalarının uygulanması (2026-10-25, sahibin açık talimatı)

Sahibin mesajı: "Önce AGENTS/ports 5071 ve RCS-001 yamalarını uygula, sync_adapters.py çalıştır ve commit et; push yok."

1. **AGENTS.md:** §2 domain listesine `finance-treasury` (12), §3 port tablosuna `FinanceTreasury Service (planlı — kod yok) | 5071`,
   §9 `domain-kısa` listesine `fin` + örnek dal. `.antigravity/rules/ports.md`: bant 5011–5071, 5071 satırı (D10/3, D27/6).
2. **`.antigravity/rules/record-coding-standard.md` §3:** `PAY`, `APD`, `ARI`, `CPS`, `CTP`, `BNK`, `BAC` satırları (OTOMATİK; D29/2).
3. `python3 .antigravity/scripts/sync_adapters.py` koşuldu; `--check`: "OK 13 adapter(s) match their sources" (çekirdek blok
   değişmedi, üretilmiş kopyalarda fark yok). Uyarı: AGENTS.md 22 226 bayt, uyarı eşiği 22 000 (sınır değil; origin/main'de
   21 991 bayttı).
4. Bilinen sapma (düzeltilmedi, başka sahip): `scripts/evidence-kit/k03_ports.py` ("CANDIDATE — NOT ACTIVE", MVP-6) yasaklı
   bandı hâlâ 5011–5064 olarak kodluyor; şerit portları 5100+ olduğu için 5071 ile çakışmaz.

## D31 — Kalan açıkların kapatılma yolu (2026-10-25, soru kutusu)

1. **AR O-8 muafiyet:** "Muafiyet yok, 422" (önerilen) — AR düzeltme onayında tier-2 aday yoksa talep 422 ile durur;
   muafiyet komutu v1'de yok.
2. **CT-APR-02 bağımsız inceleme:** "Codex ile siz yapacaksınız" (önerilen seçenek ayrı Claude oturumuydu) — sahip incelemeyi
   Codex'te başlatır; CT inceleme prompt'unu, hedef pack'lerin SHA-256'larını ve `scripts/pack_approval.py review-row`
   komutunu hazırlar.
3. **Onay PR'ı zamanı:** "İncelemeden sonra" (önerilen) — CT-APR-02 bulgu dosyaları ve inceleme satırları bu dala commit
   edildikten sonra tek push + PR (inceleme altındaki PR'a push yok).
4. **Başka sahiplerin işleri:** "Ek önerileri + mesajlar" (önerilen) — MOD-0048-FU01 §21 (TRM-17: `payment-method` ve
   `currency-minor-units` kümeleri) eki yazılır; Ali, procurement, commercial-suite, MOD-0018, BL-565 ve güvenlik için
   gönderilmeye hazır mesajlar hazırlanır.

## D32 — MOD-0023 §17 sahibinin altı noktası ve onay turu (2026-10-26, soru kutusu; hepsi önerilen seçenek)

Kaynak: MOD-0023 sahibinin §17 için altı noktası (sahip iletti, 2026-10-26). Pack'teki karşılığı: MOD-0023 §17.8 (rev 2).

1. **Yazan ≠ merge eden:** MOD-0023 sahibi WP kodunu yazar, başka biri (K2 onaylayan) merge commit'iyle birleştirir
   (sahibin kendi önerisi; kabul edildi).
2. **`isDeleted` / `includeDeleted` anlamı:** yalnız savunma amaçlı; politika silmeyi yasaklar. Koddan ölçülen (okundu,
   çalıştırılmadı): üç tip `TenantRepository<T>` üzerinden miras `RepositoryBase<T>.DeleteAsync` ile soft delete edilebilir (`IsDeleted` yazar, `DeletedAt`
   yazmaz), `by-objects` ise `DeletedAt` ile süzer — tek silme işareti `IsDeleted` olur, miras silme üç repository'de override ile kapatılır; ortak taban sınıf
   değişmez (AC-W15, §17.8/9).
   Ortamlardaki silinmiş satır sayısını MOD-0023 sahibi ölçer.
3. **KVKK / GDPR:** "Kayıt kalır, kişi alanı anonimleşir" — satır silinmez; silme talebinde kişiyi tanıtan alanlar yerinde
   takma ad ile değiştirilir; anahtar, durum, sıra kalır. Kesin prosedür hukuk görüşüne bağlı açık madde **O-S17-1**
   (sahip K1 + hukuk / veri koruma); dilimleri bloklamaz, ilk silme talebinden önce karara bağlanır.
4. **DL-2 tüketici kontrolü:** kabul — AC-W17 (403 → 409, 200 → 409, `idempotentReplay`).
5. **MDM testi:** "MDM sahibi test yazsın" — istisna yok; AC-W3 ve AC-W17 MDM testi olmadan geçmez.
6. **`includeDeleted` yetkisi:** "Mevcut `instances.view` + kiracı testi" — yeni izin yok; AC-W16.
7. **Onay turu:** "Onaydan çıkar, §17'yi düzelt" — MOD-0023 §17 `a8df05e7…` sürümü K2 onay turundan çıkarıldı (diğer sekiz
   hedef sürer); rev 2 yeniden CT-APR-02 incelemesinden geçer, sonra ayrıca CT-APR-01 onayı istenir.
8. **Beste onay notu (aynı gün, soru kutusu):** not dosya + SHA-256 ile iletilir (push yok); `whole_pack=TRM-14`
   (MOD-0220 §22), `whole_pack=TRM-17` (MOD-0048-FU01 §21); DCP numarasız onaylanır, numara sonra (yol ve hash değişince yeni
   inceleme + onay).

## D33 — MOD-0289 güvenlik onayı (O-6), anahtar kaynağı, döndürme ve AC-50 eşiği (2026-10-26, soru kutusu; hepsi önerilen seçenek)

Sahip (K1) güvenlik onayını kendisi verir. Pack'e işlenmesi **ertelendi** (madde 5).

1. **O-6 güvenlik onayı:** "Onay, testler kapıda" — MOD-0289'daki tasarım (AES-256-GCM, CSPRNG 96-bit nonce, AAD = kiracı +
   hesap + anahtar sürümü, ayrı sürümlü HMAC özet, `BankDataKeyFence`, `BankDataKeySetTarget` + artan `ApprovalRevision`,
   maskesiz okuma için ayrı izin + gerekçe + değerden önce denetim) yazıldığı gibi onaylanır. Gerçek Mongo testleri ve AC-50
   DL-2 dilim kabul kapısında kalır; geçmezlerse dilim kabul edilmez.
2. **Anahtar kaynağı:** "Ortam gizli deposu" — şifreleme ve özet anahtarları her ortamın gizli deposundan / yapılandırmasından
   gelir (yerelde user-secrets), depoya asla yazılmaz; kod yalnız `Mdm:BankData:*` anahtarlarını okur
   (`docs/guides/operations/service-trust-chain-runbook.md` deseni). Harici KMS / Vault seçilmedi.
3. **Döndürme ve `ApprovalRef`:** anahtarlar yılda bir ve sızıntı şüphesinde hemen döndürülür. `ApprovalRef` = yapılandırma
   değişikliğini onaylayan PR ve merge commit'i; onaylayan K2, yazandan farklı.
4. **AC-50 kabul eşiği** (8 paralel yazıcı, 10 dakika, aynı sürede bir çit ilerletmesi): kayıp ve çift yazım 0; ilerletmeden
   sonra eski kümeli commit 0; yeniden deneme oranı ≤ %5; p95 ≤ 500 ms; p99 ≤ 1,5 sn.
5. **Pack'e işleme zamanı:** MOD-0289 pack'i (`63896f93…`) K2 onay turunda; değiştirilirse onaylanacak sürüm eskir. D33/1–4,
   K2 onay satırları main'e girdikten sonra **ayrı bir MOD-0289 revizyonuyla** işlenir (yeni CT-APR-02 + CT-APR-01). O zamana
   kadar pack'teki ilgili `ready-for-dev` maddeleri (güvenlik onayı, anahtar kaynağı / döndürme, AC-50 eşiği,
   `ApprovalRef` kaynağı) pack'te açık görünür; kararları bu kayıt taşır. (**D34/3 ile değişti.**)

## D34 — Onay turu S-49'a göre daraltıldı (2026-10-26, soru kutusu; önerilen seçenek)

Kaynak: main'e giren CT-APR-06 kuralı S-49 (`scripts/pack_approval.py` `DRAFT_WARNING`; kart): künyesi `status: draft` olan
paketin onay satırı yalnız uyarıyla kabul edilir, kodu yetkilendirmez; onay satırı künyesi `ready-for-dev` olan sürüme yazılır.

1. **Bu tur:** K2 yalnız künyesi `ready-for-dev` olan iki eki onaylar — MOD-0220 §22 (`91248a47…`, `whole_pack=TRM-14`) ve
   MOD-0048-FU01 §21 (`8fc68be8…`, `whole_pack=TRM-17`). D32/8'deki sekiz hedefli tur bununla değişir.
2. **Taslak paketler:** MOD-0126, MOD-0119, MOD-0120, MOD-0124, MOD-0289 ve DCP, künyeleri `ready-for-dev` olunca tek seferde
   onaya gelir (yeni CT-APR-02 + CT-APR-01). DCP numarası ayrıca ve önce istenir (yalnız numara).
3. **D33/5 yerine:** MOD-0289 bu turda olmadığı için D33/1–4 K2 satırlarını beklemez; bir sonraki MOD-0289 revizyonunda işlenir
   (yeni CT-APR-02).

## D35 — Diten.FinanceTreasuryService denetim defteri: Control Tower kararı (2026-10-26, soru kutusu; hepsi önerilen seçenek)

Kaynak: MOD-0126 "Repo Scope" ve "Ready-for-dev Checklist" ("Yeni servis defteri + CT-DECISIONS satırı için Control Tower
kararı (yol b izi kabulü)"), AUD-001, `tests/architecture/audit-ledger/README.md`, emsal: SupplyChain E-2
(`tests/architecture/audit-ledger/CT-DECISIONS.md` 2026-10-21 satırı, `Diten.SupplyChainService.md`).

1. **İz:** `finance-treasury-merkezi-iletim | b | işaret | ICentralAuditDeclaration` — ortak iletici
   (`Diten.BuildingBlocks.CentralAudit`, K4); `AddCentralAudit<FinanceTreasuryCentralAuditScope>(RequireEnlistment = true)`;
   her yazma komutu işaretli, olay iş verisiyle aynı Mongo işleminde `CentralAuditTransaction.EnlistAsync` ile yazılır (K2).
2. **Borç ve istisna sıfır:** `## Bilinen borç`, `## K2 borcu`, `## İstisnalar`, `## Dolaylı`, `## Yazan sorgular` boş başlar;
   `PinnedCounts` sıfır. Sonradan satır eklemek ayrı Control Tower kararıdır.
3. **Kategori:** Platform `AuditCategory`'ye yalnız ekleme olarak `Finance` değeri (MOD-0126 O-6 kalanı, D9). Platform sahibinin
   işidir (XMC-001); **MOD-0126 DL-1 bu değer main'e girmeden başlamaz**. `MasterData` ara çözümü seçilmedi.
4. **Uygulama:** defter dosyası `tests/architecture/audit-ledger/Diten.FinanceTreasuryService.md`, `CT-DECISIONS.md` satırı
   ("kararı veren": Natig, 2026-10-26 — Treasury CT önerisi, D35) ve `AuditTrailStandardTests.AcceptedTrails` / `PinnedCounts`
   MOD-0126 DL-1 değişikliğinde birlikte yazılır (CI aynı değişikliği ister); bu kayıt yalnız kararı taşır.

## Karar olmayanlar

5069 kayması (SafetyStockService sahibi).

## Bu kayıt ne değildir

Kod, pack onayı, push / PR ya da başka bir Control Tower'ın dosyasına yazma izni değildir.
