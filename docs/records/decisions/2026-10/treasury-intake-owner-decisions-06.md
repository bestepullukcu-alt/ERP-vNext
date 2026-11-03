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
2. **D125/2 — N-3:** uygun; sözleşme değişmez. Treasury şartı tüketici tarafındadır: finans paketleri istemciden 1–128 anahtar alır ve makbuz kapsamı kiracı + şirket + aktör + işlem + hedeftir; kod servisi yalnız (kiracı, servis, anahtar) ile eşler. Ham anahtar geçirilirse iki kullanıcının aynı anahtarı aynı kodu alabilir. Bu yüzden tüketici anahtarı ham geçirmez, özetten türetir. Özetin girdisi `recordType` ile makbuzun altı alanıdır: `TenantId`, `LegalEntityId`, `ActorId`, `Operation`, `TargetId` ve istemcinin `Key` değeri (MOD-0126 makbuz tanımı, D12/2). Girdi tek bir bayt dizisine bağlanır: yedi değer bu sırayla (`recordType`, `TenantId`, `LegalEntityId`, `ActorId`, `Operation`, `TargetId`, `Key`) bir JSON dizisi olarak, RFC 8785 (JCS) kurallarıyla yazılır. Pratikte: hiç boşluk yok (ayırıcı yalnız `,`), her değer JSON dizgesi, `null` `TargetId` çıplak `null`; Guid'ler küçük harfli `D` biçimi (`xxxxxxxx-xxxx-xxxx-xxxx-xxxxxxxxxxxx`); dizgede yalnız `"` → `\"`, `\` → `\\` ve U+0000…U+001F kaçırılır (JCS kısa biçimleri, diğerleri küçük harfli `\u00xx`), başka karakter kaçırılmaz. Girdilerin hepsi ASCII olduğundan (Guid, `Operation`, `recordType`, 1–128 yazdırılabilir ASCII `Key`) normalleştirme sorusu doğmaz. Dizinin UTF-8 baytlarının SHA-256'sı küçük harfli 64 hex olarak `idempotencyKey` olur (boşluksuz, §2 sınırının içinde). **Test vektörü** (örnek değerler; `Operation` adı örnektir): girdi `["PAY","00000000-0000-0000-0000-000000000001","11111111-1111-1111-1111-111111111111","22222222-2222-2222-2222-222222222222","mod0126:CreatePaymentBatch",null,"k-1\"\\x"]` (istemci anahtarı `k-1"\x`) → `7a9e9296fa48ef12c051a408f325e7142b61970de38b5fbb5a45bdd14c01f018`; aynı girdide `ActorId` `33333333-3333-3333-3333-333333333333` → `6c572bf9a17633185a65fab1a64e5bffbdf08ba45ef26f17af11e389fa50305a`; `Key` `k-2` → `e5206ed402288b5f4c6b141026529e9ed68f853797de0645b8194ec103209c76` (Python `json.dumps(…, separators=(",", ":"))` ve `shasum -a 256` ile ayrı ayrı hesaplandı, 2026-11-03). Vektör, türeticinin birim testine aynen girer. Bu kural ve üç sabotajlı kabul testi dört finans paketinin sonraki revizyonunda eklenir: (a) aynı kapsam, farklı istemci anahtarı → iki ayrı kod; (b) aynı makbuz tekrarı → aynı kod; (c) iki aktör, aynı istemci anahtarı → iki ayrı kod. Testler üretim türeticisine bağlanır; türetme ham anahtara çevrilince (c), istemci anahtarı girdiden çıkarılınca (a), türetilen anahtara çağrı başına değişen bir değer (ör. yeni Guid ya da zaman damgası) eklenince (b) kırmızı olur; vektör testi serileştirmedeki her bayt değişikliğinde kırmızı olur. Bugün risk paket testleriyle kapanmış değildir; bu revizyona kadar açık bir paket işidir.
3. **D125/3 — N-4:** uygun. `BNK` → `Group`; şirket `BAC` → `LegalEntity` + sahip şirket; tedarikçi `BAC` → `Group` (D93/1, D65/2, D95/1 ile aynı). MDM bağdaştırıcısı için MDM sahibi Ali'nin teyidi ayrıca istenir.
4. **D125/4 — N-10:** uygun. Sekiz izin adı FU27 taslağı ve MOD-0119, 0120, 0124, 0126, 0289, 0217 paketleriyle birebir aynı. Alacak dekontu MOD-0119'da `ApInvoice` belge türüdür, `APD` + `finance-treasury.ap-invoices.create` doğrudur. Adlar FU27 onayıyla kesinleşir.
5. **D125/5 — not:** sözleşme §9'daki Treasury port imzası (`AllocateAsync(legalEntityId, recordType, idempotencyKey)`) yalnız finans paketleri için geçerli. MDM paketleri tek imzalı `prefixScope`'lu portu kullanır (D95/1). Bu, dondurmayı bekletmez.
