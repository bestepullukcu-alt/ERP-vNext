# Treasury intake — sahip kararları 06

Önceki dosyalar: `treasury-intake-owner-decisions-01…05.md`. Bu dosya #314'ten bağımsız bir PR'da gider; numara D105 oradan devam eder.
Y-39 ve D119 da burada durur; D119 numarası `chore/ct/treasury-intake-ct-05` dalındaki sıradandır, o dal main'e geldiğinde oradaki kopyası çıkarılır.
Bu kayıt onay satırı değildir.

## Y-31 — Beste, para birimi XMC-001 cevabı (2026-10-30; K1 aktardı)

> İkisini de tercih etmiyorum. Platform lookup'ına dokunmayalım, seed'e eski kod da eklemeyelim. Treasury para birimi listesini yalnız kendi setinden almalı:
> Treasury ekranlarında seçilebilecek para birimleri ve küsuratlar yalnız currency-minor-units setinden gelir.
> Başka modülden sette olmayan bir para birimi gelirse (ör. eski bir kayıttaki HRK) Treasury onu paketlerdeki …_CURRENCY_PRECISION_UNAVAILABLE hatasıyla reddeder; yuvarlama ya da varsayılan küsurat kullanılmaz.
> MOD-0048-FU01 §21'deki AC-R2'nin "Platform currencies listesinin hepsini kapsasın" kısmı kalksın. Yerine "sette olmayan kod reddedilir" testi gelsin, sabotajlı (kontrol kaldırılınca kırmızı). Bu, çalışma ortamının ICU verisine bağımlılığı da kaldırır.
> Bunun için küçük bir MOD-0048-FU01 §21 revizyonu hazırlayın: kısa doğrulama incelemesi ve onay satırı. Test değişikliğiyle aynı küçük PR'da olabilir. Onay ve merge bende.
> PlatformLookupProvider.cs'e dokunulmaz.

## D105 — `currency-minor-units` Treasury'nin tek para birimi listesi (2026-10-30)

Onay kanıtı: K1 / gmg-10 (aktarım); kaynak Y-31; oturum Treasury Intake CT (`f8880468-240a-4bd1-8ab8-bbdbecf875dd`).

1. **D105/1 — kural:** Beste'nin metni aynen. `PlatformLookupProvider.cs` değişmez, seed'e eski kod eklenmez. (Kısmen geçersiz: D119/1.)
2. **D105/2 — bu PR:** MOD-0048-FU01 §21 revizyonu (21.4, AC-R2) ve `Currency_minor_units_covers_every_code_the_currencies_lookup_returns` testinin kaldırılması. (Test kaldırma geçersiz: D119/1.)
3. **D105/3 — "sette olmayan kod reddedilir" testi (K1 / CT yorumu, Beste teyidine):** reddeden üretim kodu `Diten.FinanceTreasuryService`'te olacak ve bugün yok; Platform'da yazılacak test üretim kodu yerine kendi kopyasını ölçerdi (Çekirdek 10). Bu yüzden test §21.4 ile Treasury paketlerine bağlanır, bu PR'da yazılmaz.
4. **D105/4 — paket etkisi, bu PR'da değil:** dört finans paketinin ekran metinleri `GET /api/lookups/reference/currencies` diyor; #314 merge olunca ayrı revizyonla sete çevrilir. MOD-0217 ve MOD-0289 da bu ucu kullanıyor, Treasury ekranı sayılıp sayılmadıkları açık.

## Y-39 — Beste, para birimi listesi (2026-11-02; K1 aktardı)

> A geçerli. Y-31'in "PlatformLookupProvider.cs'e dokunulmaz, test §21 PR'ında kaldırılır" kısmını kaldırıyoruz; para birimi listesinin kaynağı Business Reference Data (currency-minor-units kümesi) olacak.
> - XMC-001: PlatformLookupProvider.cs için veriyoruz — yalnız para birimi listesinin currency-minor-units kümesiyle süzülmesi (BuildCurrencyOptions ve onun önbelleği); başka lookup'a dokunulmaz.
> - Test silinmez; currency-minor-units kümesini okuyacak şekilde uyarlanır. Sabotaj: süzme kaldırılınca ANG / BGN / HRK / SLL geri gelir ve test kırmızı olur.
> - Şart: currency-minor-units kümesi main'de olmalı. Değilse seed'i aynı §21 PR'ına alın ya da önce o PR'ı açın; main'in yeşile dönmesi bu PR'la olur.
> - Kümede olmayan bir para birimi gerekirse kümeye eklenir; liste koddan değil kümeden gelir.

## D119 — Y-31'in bir kısmı kaldırıldı; para birimi listesi kümeden süzülür (2026-11-02)

Onay kanıtı: K1 / gmg-10 (aktarım); kaynak Y-39; oturum Treasury Intake CT (`f8880468-240a-4bd1-8ab8-bbdbecf875dd`). Onay satırı değildir.

1. **D119/1 — değişen:** D105/1'in "`PlatformLookupProvider.cs` değişmez" kısmı ve D105/2'nin test kaldırma kısmı geçersiz. Treasury'nin setten okuma ve set dışı kodu reddetme kuralı yerinde kalır.
2. **D119/2 — XMC-001:** yalnız `PlatformLookupProvider.cs`, yalnız para birimi listesi ve önbelleği. Küme main'de.
3. **D119/3 — dal:** `fix/treasury/currency-lookup-set-01`. `chore/ct/treasury-mod0048-r2-01` (`a2d8afc2f`) testi kaldırdığı için bu haliyle gönderilmez; §21 PR'ı yeniden hazırlanır (bu dal: `chore/ct/treasury-mod0048-r2-02`).

## Y-40 — Beste, #346 sonrası cevap (2026-11-02; K1 aktardı)

> 1. #314: Dalı güncelleme izni veriyorum. Çakışmayı çöz, push et; onay düşerse yeniden onaylayıp merge edeceğim.
> 2. phase1: AuditTrailStandardTests'in kalan kırmızısı CRM'in 53 denetimsiz komutu; sahibi benim, düzeltme hazır, PR onayı bekliyoruz. DocsPathGuardTests main'de güncellendi; son main koşusuna bakar mısın, hâlâ kırmızıysa günlüğü gönder.
> 3. currency-minor-units: canlıda şu an bu küme yüklü değil, para birimi seçicileri 503 currency_set_unavailable döner. PR #346 açıklamasındaki yükleme adımı canlıya uygulanmalı; adımı ve kimin koşacağını netleştirelim.
> 4. ANG, BGN, HRK, SLL: şu an sistemde yoklar ve eski verilerde kullanılmamışlar; geçiş için ek bir iş gerekmiyor.
>
> Sizden beklediklerimiz: Platform XMC-001 öneri dosyasını ve aday modu kararını yeniden gönderin; ikisini aynı dosya üzerinden değerlendireceğiz. §21 PR'ını D105, D117/3 ve D119'a göre hazırlayıp gönderin; inceleyeceğiz. MOD-0289 onay PR'ını açın; açılınca inceleyip merge edeceğiz.

## D121 — Y-40 kaydı (2026-11-02)

Onay kanıtı: K1 / gmg-10 (aktarım); kaynak Y-40; oturum Treasury Intake CT (`f8880468-240a-4bd1-8ab8-bbdbecf875dd`). Onay satırı değildir.

1. **D121/1 — eski kayıtlar:** dört kod için geçiş işi yok; #346'daki açık iş sorusu kapandı.
2. **D121/2 — canlı küme:** küme canlıda yayımlı değil; yükleme adımı ve yürütücüsü açık, para birimi seçicileri o güne kadar 503 döner.
3. **D121/3 — sıra:** MOD-0289 onay PR'ı D117/2 revizyonu ve yeniden incelemeden sonra açılır; paket metni değişmeden açılırsa onay eski özete bağlanır.

## D122 — Küme dışı para birimi kodu için hata sözleşmesi (2026-11-02)

Onay kanıtı: K1 / gmg-10 (soru kutuları "MDM 400, finans 503", sonra düzeltme "Evet, düzelt"); kaynak Codex incelemesinin bulgu 1'i (K1 aktardı) ve Y-31; oturum Treasury Intake CT (`f8880468-240a-4bd1-8ab8-bbdbecf875dd`). Onay satırı değildir; §21 metni olduğu için Platform sahibi teyidine açıktı, D123/2 ile kapandı.

1. **D122/1 — kullanıcı girdisi:** kullanıcının gönderdiği küme dışı kod her tüketicide 400: MOD-0119, MOD-0120 ve MOD-0126'da `…_CURRENCY_CODE_INVALID`, MOD-0124'te `CASH_POSITION_INVALID_CURRENCY_CODE`, MOD-0217 ve MOD-0289'da doğrulama hatası.
2. **D122/2 — başka modül ya da eski kayıt:** bu yoldan gelen küme dışı kod dört finans paketinde 503 `…_CURRENCY_PRECISION_UNAVAILABLE` (Y-31).
3. **D122/3 — ortak:** küme okunamazsa her tüketicide 503.
4. **D122/4 — düzeltme notu:** ilk seçenek "finans 503" paketlerin 400 satırı ölçülmeden sunulmuştu; bu metin onun yerini alır.

## Y-41 — Beste, ICU kesişimi ve D122 (2026-11-02; K1 aktardı)

İki ayrı mesajdan alıntı; aynı mesajın D117/3 sorusu ve #353 notu olan kısımları buraya alınmadı.

> Y-39: Platform tarafı olduğu gibi kalıyor; seçici listesi ICU ile kümenin kesişimi (#346) yeterli kabul edildi. ICU kümeye kod ekleyemiyor, bu yüzden liste kümenin dışına çıkmıyor. Kümede olup ICU'da olmayan kodun seçicide görünmemesi kabul edilen sınır; finans kümeyi doğrudan okuduğu için doğrulama tarafı etkilenmiyor. Platform'da ek kod işi açılmayacak
>
> §21 teyitleri: D122 "uygun, kullanıcı girdisinde önce 400" şartıyla verildi; ICU kesişimi kabul (Platform değişmiyor).

## Y-42 — Beste, D117/3 (2026-11-02; K1 aktardı)

> D117/3 uygun. Aynı kural dört finans paketi için de geçerli: her tüketici kümeden okumayı ve küme dışı kodu reddetmeyi kendi paketinde, kendi üretim kontrolü üzerinde sabotajlı testle ölçer; Platform yalnız kümenin varlığını ve biçimini ölçer (MOD-0048-FU01 AC-R2). Finans testleri Diten.FinanceTreasuryService yazılırken gelir.

## D123 — §21 sahip teyitleri kapandı (2026-11-02)

Onay kanıtı: K1 / gmg-10 (aktarım); kaynak Y-41, Y-42; oturum Treasury Intake CT (`f8880468-240a-4bd1-8ab8-bbdbecf875dd`). Onay satırı değildir.

1. **D123/1 — ICU:** lookup ICU ile kümenin kesişimi kalır; kümede olup ICU'da olmayan kodun seçicide görünmemesi kabul edilen sınırdır. Platform'da ek kod işi yok.
2. **D123/2 — D122:** Platform sahibi teyit etti; kullanıcı girdisinde önce 400.
3. **D123/3 — D117/3:** teyit edildi ve dört finans paketini kapsar.

## D124 — Dört finans paketinde para birimi metni hizalandı (2026-11-03)

Onay kanıtı: K1 / gmg-10 (soru kutusu "Üçünü düzelt"); kaynak #346, D121/2, D123; oturum Treasury Intake CT (`f8880468-240a-4bd1-8ab8-bbdbecf875dd`). Onay satırı değildir.

1. **D124/1 — ölçüm:** D105/4'ün istediği revizyon #314 ile main'de (D112): dört paket para birimini yalnız `currency-minor-units` kümesinden okur. Kullanıcı girdisi 400 (D122/1), başka modül ya da eski kayıt 503 (D122/2) ve sabotajlı AC (D123/3) zaten pakette.
2. **D124/2 — paketler:** MOD-0119, MOD-0124 ve MOD-0126'nın Bağımlılık satırında gerekçe #346 sonrasına çekildi: lookup ICU ile kümenin kesişimidir, doğrulama kümeyi doğrudan okur. Dört pakete "küme canlıda henüz yayımlı değil (D121/2)" eklendi. MOD-0119, MOD-0124 ve MOD-0126'nın Gateway satırında para birimi ucu `/api/lookups/reference/*` yerine `/api/lookups/reference-data/consumable-sets/*` oldu.
3. **D124/3 — DL hazırlık dosyaları:** sekiz dosyaya tarihli not eklendi: seed ve izin listesi main'de (#297), `Finance = 18` main'de, küme canlıda yayımlı değil. Eski ölçüm satırları tarihçe olarak kalır.
4. **D124/4 — sınır:** kapsam, izin, AC ve `status: draft` değişmedi. Paketler CT-APR-02 için yeniden incelenir.

## D125 — RECORD-CODE-ALLOCATE 1.0.0 §12 Treasury teyitleri (2026-11-03)

Onay kanıtı: K1 / gmg-10 (soru kutuları); kaynak Beste'nin dondurma isteği (K1 aktardı) ve sözleşme taslağı `docs/analysis/contracts/drafts/record-code-allocate-v1.0-proposal.md@024e76fcd` (`origin/chore/ct/mod0178-dl1-accept-01`, rev 9) §1, §2, §3, §6, §8, §12; oturum Treasury Intake CT (`f8880468-240a-4bd1-8ab8-bbdbecf875dd`). Onay satırı değildir.

1. **D125/1 — N-2:** uygun. 200 yanıtı `Response<T>` zarfında, `data` = `{code, sequence}`. Treasury'de bağdaştırıcı kodu yok; paketlerdeki v0 satırı ("200 `{code, sequence}`") sonraki revizyonda zarfa çevrilir.
2. **D125/2 — N-3:** uygun; sözleşme değişmez. Treasury şartı tüketici tarafındadır: finans paketleri istemciden 1–128 anahtar alır ve makbuz kapsamı kiracı + şirket + aktör + işlem + hedeftir; kod servisi yalnız (kiracı, servis, anahtar) ile eşler. Ham anahtar geçirilirse iki kullanıcının aynı anahtarı aynı kodu alabilir. Bu yüzden tüketici anahtarı ham geçirmez, özetten türetir. Özetin girdisi `recordType` ile makbuzun altı alanıdır: `TenantId`, `LegalEntityId`, `ActorId`, `Operation`, `TargetId` ve istemcinin `Key` değeri (MOD-0126 makbuz tanımı, D12/2). Girdi tek bir bayt dizisine bağlanır: yedi değer bu sırayla (`recordType`, `TenantId`, `LegalEntityId`, `ActorId`, `Operation`, `TargetId`, `Key`) bir JSON dizisi olarak, RFC 8785 (JCS) kurallarıyla yazılır. Pratikte: hiç boşluk yok (ayırıcı yalnız `,`), her değer JSON dizgesi, `null` `TargetId` çıplak `null`; Guid'ler küçük harfli `D` biçimi (`xxxxxxxx-xxxx-xxxx-xxxx-xxxxxxxxxxxx`); dizgede yalnız `"` → `\"`, `\` → `\\` ve U+0000…U+001F kaçırılır (JCS kısa biçimleri, diğerleri küçük harfli `\u00xx`), başka karakter kaçırılmaz. Girdilerin hepsi ASCII olduğundan (Guid, `Operation`, `recordType`, 1–128 yazdırılabilir ASCII `Key`) normalleştirme sorusu doğmaz. Dizinin UTF-8 baytlarının SHA-256'sı küçük harfli 64 hex olarak `idempotencyKey` olur (boşluksuz, §2 sınırının içinde). **Test vektörü** (örnek değerler; `Operation` adı örnektir): girdi `["PAY","00000000-0000-0000-0000-000000000001","11111111-1111-1111-1111-111111111111","22222222-2222-2222-2222-222222222222","mod0126:CreatePaymentBatch",null,"k-1\"\\x"]` (istemci anahtarı `k-1"\x`) → `7a9e9296fa48ef12c051a408f325e7142b61970de38b5fbb5a45bdd14c01f018`; aynı girdide `ActorId` `33333333-3333-3333-3333-333333333333` → `6c572bf9a17633185a65fab1a64e5bffbdf08ba45ef26f17af11e389fa50305a`; `Key` `k-2` → `e5206ed402288b5f4c6b141026529e9ed68f853797de0645b8194ec103209c76` (Python `json.dumps(…, separators=(",", ":"))` ve `shasum -a 256` ile ayrı ayrı hesaplandı, 2026-11-03). Vektör, türeticinin birim testine aynen girer. Bu kural ve üç sabotajlı kabul testi dört finans paketinin sonraki revizyonunda eklenir: (a) aynı kapsam, farklı istemci anahtarı → iki ayrı kod; (b) aynı makbuzla yeniden deneme → aynı servis anahtarı: sahte allocator ilk çağrıda 503 `RECORD_CODE_UNAVAILABLE` döner, Create başarısız olur ve makbuz yazılmaz; aynı `Idempotency-Key` ile yeniden denenen Create türeticiyi yeniden çalıştırır (Create tek aşamalıdır, makbuz yalnız tx1'de yazılır; tx1 commit olmadıysa aynı anahtarla tekrar baştan çalışır — `execution/domains/finance-treasury/module-packs/MOD-0126-payments-approvals.md:896, 906, 912-913@35e973bd2`; allocator 503'ü Create'i başarısız kılar — aynı dosya `:914-918@35e973bd2`). Test, sahte allocator'ın iki çağrı aldığını ve iki çağrıdaki `idempotencyKey`'in birebir aynı olduğunu doğrular (tamamlanmış makbuzdan erken dönüş yolu bu testi karşılamaz); (c) iki aktör, aynı istemci anahtarı → iki ayrı kod. Testler üretim türeticisine bağlanır; türetme ham anahtara çevrilince (c), istemci anahtarı girdiden çıkarılınca (a), türetilen anahtara çağrı başına değişen bir değer (ör. yeni Guid ya da zaman damgası) eklenince (b) kırmızı olur; vektör testi serileştirmedeki her bayt değişikliğinde kırmızı olur. Bugün risk paket testleriyle kapanmış değildir; bu revizyona kadar açık bir paket işidir.
3. **D125/3 — N-4:** uygun. `BNK` → `Group`; şirket `BAC` → `LegalEntity` + sahip şirket; tedarikçi `BAC` → `Group` (D93/1, D65/2, D95/1 ile aynı). MDM bağdaştırıcısı için MDM sahibi Ali'nin teyidi ayrıca istenir.
4. **D125/4 — N-10:** uygun. Sekiz izin adı FU27 taslağı ve MOD-0119, 0120, 0124, 0126, 0289, 0217 paketleriyle birebir aynı. Alacak dekontu MOD-0119'da `ApInvoice` belge türüdür, `APD` + `finance-treasury.ap-invoices.create` doğrudur. Adlar FU27 onayıyla kesinleşir.
5. **D125/5 — not:** sözleşme §9'daki Treasury port imzası (`AllocateAsync(legalEntityId, recordType, idempotencyKey)`) yalnız finans paketleri için geçerli. MDM paketleri tek imzalı `prefixScope`'lu portu kullanır (D95/1). Bu, dondurmayı bekletmez.

## D126 — D125/2 türetme kuralı dört finans paketine yazıldı (2026-11-03)

Onay kanıtı: K1 / gmg-10 ("devam edelim"); kaynak D125/2; oturum Treasury Intake CT (`f8880468-240a-4bd1-8ab8-bbdbecf875dd`). Onay satırı değildir.

1. **D126/1 — tek tanım:** kural MOD-0126 "Kayıt kodu" maddesinde servis ortak olarak tanımlandı: `RecordCodeIdempotencyKey.Derive(recordType, receiptScope)`, `Application/Common/RecordCodes/`. Türetme port çağrısından önce Create işleyicisinde yapılır. MOD-0119, MOD-0120 ve MOD-0124 aynı maddeye atıf yapar ve kendi `Operation` adını yazar.
2. **D126/2 — işlem adları:** `mod0119:CreateApInvoice`, `mod0120:CreateReceivableInvoice`, `mod0124:CreateCashPosition` (`CPS`), `mod0124:CreateCashPositionThresholdPolicy` (`CTP`), `mod0126:CreatePaymentBatch`. Desen MOD-0126 makbuz tanımındaki `{modül}:{Komut}`.
3. **D126/3 — yeni AC:** MOD-0119 AC-45, MOD-0120 AC-45, MOD-0124 AC-39, MOD-0126 AC-47. Her biri vektör testi (v) ile D125/2'nin (a), (b) ve (c) senaryolarını ve dört sabotajı içerir. Test bölümlerine de eklendi. Her yeni AC kendi paketinin DL-1 "Bitiş" listesine eklendi; Create'ler DL-1'de teslim edildiği için anahtar kanıtı DL-1 kabulünde aranır.
4. **D126/4 — sınır:** kural ve testler paket metnindedir; üretim kodu yok (`Diten.FinanceTreasuryService` yazılmadı). D125/2'deki "bugün açık" durumu kodla kapanır. Zarf satırı (D125/1) sözleşme dondurulunca ayrı revizyonla gelir. Kapsam, izin ve `status: draft` değişmedi.

## D127 — MOD-0289 `ready-for-dev` (2026-11-03)

Kaynak: K1 (Natig) soru kutusunda "Evet, hazırla" ve "Alanı kaldır" (2026-11-03). Ölçüm: `origin/main` @ `73a90cc34`.

1. **D127/1 — paket kapısı:** MOD-0289 "Ready-for-dev Checklist"inde işaretsiz paket düzeyi madde olarak yalnız CT-APR-02 ve CT-APR-01 kalır; bu sürüm için kayıtları bu sürümün PR'ında gelir, kutular o yüzden metinde açıktır (Codex tur 1 ENGEL'i). İkisi önceki `draft` sürümü için main'de: inceleme `docs/records/reviews/MOD-0289/2026-11-02-691e9b05.md` (UYGUN), onay satırı `execution/approvals/approvals.tsv` (onaylayıcı `bestepullukcu-alt`, #354). Öbür işaretsiz maddeler "— DL-n için" dilim kapısıdır (D51/1) ve paketi bekletmez.
2. **D127/2 — künye:** `status: draft` → `ready-for-dev`. `approval_status` alanı kaldırıldı: paket standardında tanımlı değil, `ready-for-dev` olan MOD-0220 ve MOD-0140 künyesinde yok; onayın kaynağı `approvals.tsv`dir.
3. **D127/3 — yeni sürüm onayı:** onay satırı sürüme bağlıdır ve `pack_approval.py` S-49 uyarısı onay satırının künyesi `ready-for-dev` olan sürüme yazılmasını ister. Bu sürüm için yeni CT-APR-02 incelemesi (Codex) ve CT-APR-01 satırı (onaylayıcı `bestepullukcu-alt`) aynı PR'da gelir; onay, o PR'ın Beste tarafından birleştirilmesidir.
4. **D127/4 — sınır:** kapsam, izin, AC (AC-1…60), dilimler ve Açık kararlar değişmedi. Kod ancak yeni onay satırı main'de olunca ve dilim sırasıyla yazılır; DL-1 kabul kapısı (Auth kayıt kodu, SEC-002 eki kodu, RCS-001 servisi) açık.
5. **D127/5 — Codex tur 1 (`c4b970b13`, UYGUN DEĞİL):** ENGEL — CT-APR-02 / CT-APR-01 kutuları önceki sürümün kayıtlarıyla işaretlenmişti → açık bırakıldı, "bu sürüm için henüz yok" yazıldı. DÜŞÜK — domain-config maddesindeki "fark boş" ölçümü 2026-10-28'e bağlandı, D127 güncellemesi eklendi.
6. **D127/6 — Codex tur 2 (`cd2c0b6c6`, UYGUN DEĞİL):** ENGEL — paket kapısı kuralı ("hepsi kapanmadan `ready-for-dev` olmaz") açık CT-APR kutularıyla çelişiyordu → kurala tek istisna yazıldı: CT-APR-02 / CT-APR-01 S-49 gereği künyeden sonra kapanır; o ana kadar paket kod için onaylı değildir. ORTA — Ownership and Boundaries'teki domain-config cümlesi satırı "yok" diyordu → satırın var olduğu, yalnız frontend/gateway cümlesinin DL-4 için açık olduğu yazıldı.

## D128 — RECORD-CODE-ALLOCATE 1.0.0 dondurma isteği, 2. tur (2026-11-04)

Onay kanıtı: K1 / gmg-10 (soru kutusu, 2026-11-04: "Hepsi uygun, AGR notuyla" ve "Dondurmayı bekle"); kaynak Beste'nin 2. dondurma mesajı (K1 aktardı; 5 Kasım 2026 gün sonuna kadar itiraz yoksa sözleşme bu hâliyle dondurulur); oturum Treasury Intake CT (`f8880468-240a-4bd1-8ab8-bbdbecf875dd`). Onay satırı değildir.

1. **D128/1 — N-2, N-3, N-10:** D125/1, D125/2 ve D125/4 aynen geçerli. N-3'te "1–200 karakter, yazdırılabilir ASCII" finans türetmesiyle uyumludur: finans tüketicileri anahtarı 64 karakter küçük harf hex olarak türetir (D125/2, D126). MDM tüketicisinin (MOD-0289) anahtarı paketinde sunucuda Create denemesi başına üretilir; biçimi pakette yazılı değildir ve MDM bağdaştırıcısı yazılırken bu sınıra uyması gerekir (bu kayıt MDM için ayrıca teyit değildir).
2. **D128/2 — N-4 MDM:** Beste, Ali'nin bugünkü teyidini aktardı: `BNK` → `Group` (boş `legalEntityId`), şirket `BAC` → `LegalEntity` (hesabın sahibi şirket), tedarikçi `BAC` → `Group`, `AGR` → `LegalEntity` (yöneten şirket). D125/3'teki "Ali'nin teyidi ayrıca istenir" maddesi bununla kapanır (K1 aktarımı; Ali'nin kendi metni bu kayıtta yok).
3. **D128/3 — N-4 finans:** uygun. `PAY`, `APD`, `ARI`, `CPS`, `CTP` → `LegalEntity` (Create'in şirketi); finans port imzası `legalEntityId` taşır (D125/5). **AGR notu:** `AGR` → `LegalEntity` D94/1 ile aynıdır, ama RCS-001 §3 main'de hâlâ `CTR` yazıyor (`.antigravity/rules/record-coding-standard.md:70`); `AGR` satırı `chore/ct/treasury-rcs-agr-01` @ `fbbdcdff0`'da, Ali'nin push / PR izni bekleniyor (D104/4). Dondurmayı bekletmez; sözleşmenin tür listesi §3 ile hizalanınca kapanır.
4. **D128/4 — sıra:** MOD-0289 DL-1 WP prompt'u (TRM-WP-0289-DL1-P v1.0) dondurmadan sonra yazılır. MOD-0289 paketi RCS yanıtını v0 biçiminde ("200 `{code, sequence}`", Dependencies RCS-001 satırı) tanımlıyor; dondurulan 1.0.0 `Response<T>` zarfı döner (D125/1). Önce MOD-0289 ve dört finans paketinde zarf revizyonu (CT-APR-02 + yeni CT-APR-01), sonra prompt o sürüme bağlanır. Bugün hazırlananlar (keşif raporu, S-83 kabul betiği, kırmızı taban) dalda kalır.

## D129 — DCP-011 revizyonu: MOD-0217 üye, kod alanı yalnız MOD-0289 (2026-11-04)

Onay kanıtı: K1 / gmg-10 (soru kutuları, 2026-11-04: "Şimdi hazırla" ve "Evet, yalnız MOD-0289"); oturum Treasury Intake CT (`f8880468-240a-4bd1-8ab8-bbdbecf875dd`). Onay satırı değildir; DCP onayı Beste'nin bu revizyonun PR'ını birleştirmesidir.

1. **D129/1 — MOD-0217 üye:** DCP-011'de aday modül olan MOD-0217 üyeye alındı (TRM-S4; §5, §8 madde 8, §8.1). MOD-0217 paketinin "bir DCP'nin onaylı üyesi" maddesi bu revizyon main'e girince kapanır; paketin kendi metni sonraki MOD-0217 revizyonunda güncellenir. Üyelik ready-for-dev değildir: tam inceleme, onay, `AGR` §3 satırı (Ali), aday modu (Beste) ve MOD-0262-FU01 açık.
2. **D129/2 — kod alanı:** `runtime_code_allowed: true`, kapsam yalnız MOD-0289 (D127); bu DCP sürümünün CT-APR-01 satırı main'e girene kadar yürürlükte değildir, önceki onaylı sürüm (`runtime_code_allowed: false`) geçerlidir. CAP-001 §7'nin iki koşulu (DCP `approved`, sıradaki üyenin pack'i `ready-for-dev`) MOD-0289 için sağlanıyor; kod dilim sırasıyla ve her dilimin WP prompt'u kapıdan geçince yazılır. Diğer üyeler ready-for-dev oldukça alan yeni bir DCP revizyonuyla genişletilir.
3. **D129/3 — sınır:** teslim sırası, diğer üyeler, kapsam dışılar ve AC aralıkları değişmedi. Bu sürüm için yeni CT-APR-02 ve CT-APR-01 gerekir.
4. **D129/4 — Codex tur 1 (`f209c70a2`, UYGUN DEĞİL):** ENGEL — kod alanının yürürlüğü onaya bağlanmamıştı → künyede ve MOD-0289'un üç satırında "onay satırı main'e girene kadar kod yok" yazıldı. ORTA — TRM-S3 "draft pack" → `ready-for-dev`. ORTA — D128/1'deki 64 hex cümlesi finans tüketicileriyle sınırlandı; MDM anahtarı kendi paket kuralında.

## D130 — MOD-0120: CRM-ACCOUNT-LOOKUP hizalaması (2026-11-05)

Kaynak: CRM CT (MOD-0149) hizalama mesajı (K1 aktardı, 2026-11-05) ve K1 / gmg-10 soru kutusu ("Şimdi taslak, PR sonra"). Ölçüm: `origin/main` @ `a38bab855`; CRM dalı `docs/crm/mod0149-review-01` @ `1ec2d1385`. O dalda MOD-0149 paketi sha256 `2840220b…`, CT-APR-02 KOŞULLU (açık ENGEL 0), CT-APR-01 satırı HIGH (onaylayan GMG-ERP). Sözleşme taslağı `docs/analysis/contracts/drafts/crm-account-lookup-validation-v1.0-proposal.md` (K-1, K-2, K-8, K-11 kayıtlı). Mesajdaki MOD-0120 satır numaraları main'de tuttu.

1. **D130/1 — seçici:** müşteri seçicinin veri kaynağı U-3 `GET /api/crm/accounts/lookup` (K-8). Seçici same-origin proxy ile çalışır; seçicide görünmek uygunluk kararı değildir, seçilen kimlik kayıttan önce U-1 ile doğrulanır. `GET /api/crm/accounts?search=` seçeneği düştü.
2. **D130/2 — izin:** müşteri seçici ve doğrulama için yalnız `crm.account.lookup-validation` (K-1). O-3 (b) reddedildi; AR kullanıcısı `crm.account.read` almaz. Rol ataması MOD-0018-FU27'dedir.
3. **D130/3 — durum kümesi:** `account-status` beş değerdir, `archived` da yayımlanır (K-2). Faturalanabilir yalnız `active`; karar `isBillable` ile verilir.
4. **D130/4 — sınıflandırma:** sözleşme §5 ve §9 bu paketin testidir (AC-3 genişledi). Çelişkili yanıt (F-C4 dahil), U-1'in 400'ü ve 403 → 503. `tenant_id`'siz token reddi (K-11) AR'yi etkilemez, çünkü çağrı kullanıcının token'ıyla gider.
5. **D130/5 — teyit:** O-3'teki "commercial-suite sahibi teyidi açık" ifadesi kapandı. Sözleşme "yazılacak" yerine "taslak" yazıldı; sözleşme MOD-0149 DL-1 kabulüyle donar.
6. **D130/6 — değişmeyenler:** U-2 (`external-references/resolve`) ve K-3 (409 → ayrı 422 kodu) O-10'da kalır (D112/2). U-1'in 404'ü üç durumda aynı gövdedir (yok, silinmiş, başka kiracı).
7. **D130/7 — sınır ve sıra:**
   - Kapsam, AC numaraları ve `status: draft` değişmedi.
   - MOD-0149 önkoşulu açık kalır. Bunun iki koşulu var: ekin onayı (CRM onay PR'ı) main'e girmeli ve sözleşme MOD-0149 DL-1 kabulüyle donmalı.
   - Bu revizyon yerel dalda bekler. RECORD-CODE-ALLOCATE dondurulunca zarf revizyonu aynı sürüme eklenir. Ardından Codex tek tur inceler. PR, CRM onay PR'ı main'e girince açılır.

## D132 — SEC-002 kodu DL-1 kabul kapısına, RCS zarf revizyonu, kapanan iki madde (2026-11-05)

Onay kanıtı: K1 / gmg-10 (soru kutusu, 2026-11-05: "Taşı (Önerilen)", "Şimdi yaz (Önerilen)", "Evet, hazırla (Önerilen)"); oturum Treasury Intake CT (`f8880468-240a-4bd1-8ab8-bbdbecf875dd`). Ölçüm: `origin/main` @ `018f0119c`. Onay satırı değildir.

1. **D132/1 — SEC-002 kodu DL-1 kabul kapısına:** MOD-0119, MOD-0120, MOD-0124 ve MOD-0126'da SEC-002 maddesinin kod kısmı — Platform güvenilir kapsam ucunun FinanceTreasury ikinci tüketici kodu (D92/1) ve `read-tenant-wide`'ın Auth kodu (MOD-0018 Treasury Auth eki DL-1, Bölüm 14.1) — paket kapısından DL-1 kabul kapısına taşındı (D59/1, D82/2 deseni). Paket kapısında SEC-002'den yalnız Platform sahibinin (Beste) XMC-001 cevabı ve aday modu kararı (D92/3) kalır. Gerekçe: SEC-002 §4 (`.antigravity/rules/data-scope-enforcement.md:112-126`) paket için kapsam cümlesini ister (dört pakette işaretli); kod ve kanıt testi iş paketinin kabulündedir. DL-1 `IFinanceTreasuryDataScopeSource`'a karşı sahte uygulamayla ilerler; kanıt testleri (MOD-0119 AC-32, MOD-0120 AC-6, MOD-0124 AC-31, MOD-0126 AC-32) DL-1 "Bitiş"tedir. Auth paket metninin paket kapısı olması (D53, D70/3, D80/2) değişmedi.
2. **D132/2 — RCS zarf revizyonu (D125/1, D128/4):** MOD-0289 ve dört finans paketinde RCS-001 Dependencies ve Runtime Constraints satırları RECORD-CODE-ALLOCATE 1.0.0 `Response<T>` zarfına çevrildi: 200'de kod `data.code`, sıra `data.sequence`; `isSuccessful` true değilse ya da `data` boşsa yanıt 200 dışı sayılır (fail-closed). Kaynak: taslak `docs/analysis/contracts/drafts/record-code-allocate-v1.0-proposal.md@024e76fcd` (rev 10, sha256 `b7a2e0361b4b…`, §3, §4, §9). Dondurma metni main'de değil (ölçüldü); dondurulunca §3 / §4 / §9 farkı ölçülür, fark varsa paketler yeniden revize edilir. Tüketici kuralı (200 dışı her yanıt → Create 503, D59/1) değişmedi.
3. **D132/3 — kapanan maddeler:** MOD-0217 DCP üyeliği (DCP-011 D129, PR #371, `a38bab855`) ve MOD-0120 MOD-0149 eki (onay #372 `1e5aad3ff`; DL-1 #377 `38f7ea08d`; CRM-ACCOUNT-LOOKUP 1.0.0 U-1 + U-3 donduruldu #379 `3870486d6`, `docs/analysis/contracts/crm-account-lookup.md`, taslak `ec5a1062…` ile aynı metin — ölçüldü). MOD-0120 sözleşme satırları dondurulmuş dosyaya çekildi. Canlı için `crm.account.lookup-validation` izninin Auth kataloğuna girmesi ve AR rolüne atanması (Ali) DL-1 kabul kapısıdır.
4. **D132/4 — inceleme ve onay:** altı paketin bu sürümü için tek Codex turu (CT-APR-02): MOD-0217, MOD-0119, MOD-0120, MOD-0124, MOD-0126 tam inceleme; MOD-0289 zarf farkı. Ardından sürüme bağlı CT-APR-01 satırları (onaylayıcı `bestepullukcu-alt`). MOD-0289'un önceki onaylı sürümü `fafef5c4…` (#368); bu sürüm onaylanana kadar MOD-0289 DL-1 prompt'u o sürüme bağlıdır.
5. **D132/5 — sınır:** kapsam, izin, AC numaraları, dilimler ve `status` değişmedi; ürün kodu yok.

## D133 — kod servisinin iki izin hatası Create 403; Codex K1 tur 1 bulguları (2026-11-05)

Onay kanıtı: K1 / gmg-10 (soru kutusu, 2026-11-05: "İki izin hatası 403 (Önerilen)"); kaynak Codex K1 tur 1 (`73278eabc`, MOD-0217 ve MOD-0289 UYGUN DEĞİL; model ve effort çıktıda görünmüyor); oturum Treasury Intake CT (`f8880468-240a-4bd1-8ab8-bbdbecf875dd`). Onay satırı değildir.

1. **D133/1 — izin hataları:** kod servisi 403 `RECORD_CODE_PERMISSION_MISSING` ya da 403 `LEGAL_ENTITY_READ_PERMISSION_MISSING` döndüğünde Create 403 olur, kayıt ve kod yazılmaz, yeniden denenmez (RECORD-CODE-ALLOCATE 1.0.0 taslak §4 tüketici davranışı). Diğer 200 dışı her yanıt, zaman aşımı ya da bağlı olmayan port Create 503 kalır (D59/1). MOD-0119, MOD-0120, MOD-0124, MOD-0126, MOD-0217 ve MOD-0289'da Dependencies RCS-001 satırı, Runtime Constraints, Failure Path ve RCS AC'si (AC-43, AC-44, AC-38, AC-45, AC-20, AC-60; sabotajlı senaryo eklendi) buna çekildi. AC numaraları değişmedi.
2. **D133/2 — MOD-0217 RCS-001 §3 `AGR` (K1 ENGEL 1):** Checklist maddesi "— DL-1 için" (dilim kapısı) iken DL-1 Önkoşul onu paket düzeyi ENGELLER listesinde sayıyordu. Checklist geçerli: DL-1 Önkoşul'da ayrı dilim kapısı yazıldı. Satır sahibi D104/4 ile Ali'dir (`.antigravity` kural dosyaları); satır `chore/ct/treasury-rcs-agr-01` @ `fbbdcdff0` (D114); Checklist ve Dependencies'teki "Beste" düzeltildi.
3. **D133/3 — MOD-0217 zarf (K1 ENGEL 2):** MOD-0217'nin RCS-001 Dependencies ve Runtime Constraints satırları D132/2'de atlanmıştı; aynı zarf kuralı yazıldı.
4. **D133/4 — sınır:** kapsam, izin, dilimler ve `status` değişmedi; ürün kodu yok. Codex K1 tur 2 bu sürümle yapılır.
5. **D133/5 — `LEGAL_ENTITY_NOT_FOUND` (Codex K1 tur 2 ORTA; K1 / gmg-10 soru kutusu "422, paketin kendi kodu (Önerilen)"):** kod servisi 404 `LEGAL_ENTITY_NOT_FOUND` döndüğünde (şirket yok, silinmiş, başka kiracının ya da aktif değil) Create, paketin kendi şirket doğrulama yanıtıyla aynı 422'yi döner, kayıt ve kod yazılmaz, yeniden denenmez (taslak §4 "Create 422/404 (tüketicinin kodu)"): MOD-0119 `AP_LEGAL_ENTITY_UNKNOWN`, MOD-0120 `RECEIVABLE_LEGAL_ENTITY_NOT_ELIGIBLE`, MOD-0124 `CASH_POSITION_LEGAL_ENTITY_NOT_ELIGIBLE`, MOD-0126 `PAYMENT_LEGAL_ENTITY_NOT_ELIGIBLE`, MOD-0217 `MDM_CONTRACT_LEGAL_ENTITY_NOT_ELIGIBLE`, MOD-0289 `MDM_BANK_LEGAL_ENTITY_UNKNOWN`. Dependencies, Runtime Constraints, Failure Path (yeni madde; 503 maddesi iki izin hatasını ve 404'ü dışlar) ve RCS AC'si (sabotajlı) buna çekildi; Dependencies satırına geçersiz başarı yanıtı kuralı da yazıldı.
6. **D133/6 — MOD-0217 sahiplik (Codex K1 tur 2 ORTA):** `AGR` satırının sahibi her güncel cümlede Ali (D104/4): Entity Fields `Code`, Repo Scope, Protected Paths, Dependencies RCS-001 satırı ve Açık kararlar O-5. Tarihçe notları (revizyon blokları) değişmedi. Codex K1 tur 2 (`9d35226f2`): 6 ORTA, ENGEL yok; tur 3 bu sürümle.
7. **D133/7 — Codex K1 tur 3 (`61cdda6de`, 2 ENGEL + 2 ORTA):** ENGEL — RCS AC'lerinin (MOD-0217 AC-20, MOD-0289 AC-60; aynı metin MOD-0119 AC-43, MOD-0120 AC-44, MOD-0124 AC-38, MOD-0126 AC-45) 503 senaryosu genel "404"ü sayıyordu → 503 listesinden çıkarıldı, `LEGAL_ENTITY_NOT_FOUND` 422 senaryosu ayrı kaldı. ORTA — Failure Path "diğer 200 dışı" cümlesi ve D133 revizyon notu 404 istisnasını açıkça söylemiyordu → altı pakette eklendi. Tur 4 bu sürümle.

## D134 — MOD-0217 `ready-for-dev` (2026-11-05)

Onay kanıtı: K1 / gmg-10 (2026-11-05: "Readiness %100'e çıkartmamız gerekiyor"; D132 soru kutusu "Evet, hazırla"); oturum Treasury Intake CT (`f8880468-240a-4bd1-8ab8-bbdbecf875dd`). Onay satırı değildir.

1. **D134/1 — paket kapısı:** MOD-0217 Ready-for-dev Checklist'inde işaretsiz paket düzeyi madde olarak yalnız CT-APR-02 ve CT-APR-01 kalır (DCP üyeliği D132/3 ile kapandı; öbür işaretsizler "— DL-n için"). `draft` sürümü `a2714afa…` Codex K1 tur 4'te UYGUN (`docs/records/reviews/MOD-0217/2026-11-05-a2714afa.md`, reviews.tsv satırı).
2. **D134/2 — künye:** `status: draft` → `ready-for-dev`; `approval_status` kaldırıldı (D127/2 gerekçesi; Frontmatter maddesi buna çekildi); Durum paragrafı güncellendi; ENGELLER metnine S-49 onay istisnası yazıldı (MOD-0289 D127/3 deseni).
3. **D134/3 — sıra:** bu sürüm için CT-APR-02 fark incelemesi (Codex K1 tur 5) ve CT-APR-01 satırı aynı PR'da; onay, PR'ın birleştirilmesidir. Kod ayrıca DCP-011 `runtime_code_scope`'un MOD-0217'yi kapsamasını ister (bugün yalnız MOD-0289, D129).
4. **D134/4 — sınır:** kapsam, izin, AC, dilimler ve Açık kararlar değişmedi.

## D135 — Codex K2 tur 1 bulguları: ortak tanım sabitlemesi, DL-1 dilim kapıları (2026-11-05)

Kaynak: Codex K2 tur 1 (`a586738a0`; MOD-0119 ve MOD-0126 UYGUN DEĞİL — 1 ENGEL + 2 ORTA); K1 / gmg-10 D132 soru kutusu ("Evet, hazırla"); oturum Treasury Intake CT (`f8880468-240a-4bd1-8ab8-bbdbecf875dd`). Onay satırı değildir.

1. **D135/1 — ortak tanım sabitlemesi (ENGEL):** MOD-0119, MOD-0120 ve MOD-0124 servis ortak tanımları MOD-0126 `40140c6e…817e` sürümüne sabitliyordu; o sürüm D125/2 türetme tanımını (`RecordCodeIdempotencyKey.Derive`, D126) içermiyordu. Üç paket bu dalın MOD-0126 sürümüne (`e483272a14958fbdc183278f630026f6f03ee46d445a1cad2983b8db1eac539d`; D126, D132, D133, D135) yeniden sabitlendi ("Kaynak ve kanıt"; MOD-0124 Dependencies satırı). MOD-0126'nın her yeni revizyonu bu sabitlemeyi yeniden ister.
2. **D135/2 — DL-1 dilim kapıları (ORTA ×2):** DL-1 Önkoşul metni MOD-0126 DL-1'i (MOD-0119, 0120, 0124) ve TRM-14'ü (dört paket) "işaretsiz paket kapısı" listesinde sayıyordu; Checklist ikisini "— DL-1 için" dilim kapısı işaretliyor (D82/2). Dört pakette ikisi listeden çıkarıldı ve DL-1'in kendi dilim kapıları olarak ayrı yazıldı.
3. **D135/3 — sınır:** kapsam, izin, AC ve `status` değişmedi. Codex K2 tur 2 bu sürümle.
4. **D135/4 — Codex K2 tur 2 (`ef059d19a`, ORTA ×2):** DL-1 Önkoşul'daki genel cümle ("— DL-n için" işaretli maddeler DL-1'i bekletmez) aynı paragraftaki DL-1 dilim kapılarıyla çelişiyordu → dört pakette yalnız sonraki dilimlerin maddeleri ("— DL-2 için" ve sonrası) DL-1'i bekletmeyen madde sayıldı. MOD-0126 değiştiği için ortak tanım sabitlemesi MOD-0119, MOD-0120 ve MOD-0124'te `e8e5ce47332273ef6bd0d532ce51e52c533ea6762ad065f6687274ba8c8d1288`'e çekildi (D135/1'deki `e483272a…539d`'nin yerini alır). Tur 3 bu sürümle.

## D136 — Codex K3 tur 1: MOD-0120 güncel ölçüm, O-3 kapanışı, AC-3 F-OK (2026-11-05)

Kaynak: Codex K3 tur 1 (`de49ac68b`; MOD-0120 UYGUN DEĞİL — 3 ORTA; MOD-0124 UYGUN); K1 / gmg-10 D132 soru kutusu ("Evet, hazırla"); oturum Treasury Intake CT (`f8880468-240a-4bd1-8ab8-bbdbecf875dd`). Onay satırı değildir.

1. **D136/1 —** MOD-0120 Dependencies MOD-0149 "Recovery durumu" hücresi güncel ölçüme çekildi (ek onayı #372, U-1 + U-3 #377, CRM-ACCOUNT-LOOKUP 1.0.0 FROZEN #379; U-2 yok; canlı izin Auth'ta değil); D130 öncesi ölçüm tarihsel olarak işaretlendi.
2. **D136/2 —** Açık kararlar O-3: "MOD-0149 pack revizyonuyla gelecek (o gelene kadar önkoşul)" yerine D132/3 kapanışı ve kalan canlı koşul (`crm.account.lookup-validation` Auth kataloğu + AR rolü, Ali; DL-1 kabul kapısı).
3. **D136/3 —** AC-3: F-OK uygun sınıflanır ve Create / Issue ilerler; "kayıt yazılmaz" yalnız ret ve bağımlılık hatası fikstürleri için.
4. **D136/4 — sınır:** kapsam, izin, AC numaraları ve `status` değişmedi. MOD-0124 tur 1'de UYGUN (inceleme kaydı bu commit'le). MOD-0120 tur 2 bu sürümle.
