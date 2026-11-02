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

Onay kanıtı: K1 / gmg-10 (soru kutuları "MDM 400, finans 503", sonra düzeltme "Evet, düzelt"); kaynak Codex incelemesinin bulgu 1'i (K1 aktardı) ve Y-31; oturum Treasury Intake CT (`f8880468-240a-4bd1-8ab8-bbdbecf875dd`). Onay satırı değildir; §21 metni olduğu için Platform sahibi teyidine açıktır.

1. **D122/1 — kullanıcı girdisi:** kullanıcının gönderdiği küme dışı kod her tüketicide 400: MOD-0119, MOD-0120 ve MOD-0126'da `…_CURRENCY_CODE_INVALID`, MOD-0124'te `CASH_POSITION_INVALID_CURRENCY_CODE`, MOD-0217 ve MOD-0289'da doğrulama hatası.
2. **D122/2 — başka modül ya da eski kayıt:** bu yoldan gelen küme dışı kod dört finans paketinde 503 `…_CURRENCY_PRECISION_UNAVAILABLE` (Y-31).
3. **D122/3 — ortak:** küme okunamazsa her tüketicide 503.
4. **D122/4 — düzeltme notu:** ilk seçenek "finans 503" paketlerin 400 satırı ölçülmeden sunulmuştu; bu metin onun yerini alır.
