# Servis güven zinciri — işletim kılavuzu

> Bu belge **servisler arası güvenin** dağıtımda nasıl korunacağını anlatır: bir servisin
> kararını başka bir servisin cevabına dayandırdığı yerlerde hangi sırayla çıkılır, neyin
> geri alınamayacağı. Her bölüm bir zinciri anlatır; yeni bir zincir doğduğunda yeni bölüm
> eklenir.

---

## 1 · Platform yönetici kaydı → Auth belirteç basımı (BL-521)

**Zincir.** Auth, bir platform aktörüne belirteç bastığı her kapıda — giriş
(`POST /api/platform-auth/login`), zorunlu parola değişimi
(`POST /api/platform-auth/change-password/forced`), yenileme (`POST /api/auth/refresh-token`) —
belirtecin `actor_type`'ını (`platform_admin` / `partner_admin`) Platform'daki yönetici
**kaydından** alır: `GET /api/internal/platform-administrators/status` cevabındaki
`actorType` alanı. Auth'un kullanıcı üzerindeki kendi kopyası (`User.PlatformActorType`) tür
kararında kullanılmaz.

**Sıra: önce Platform (bütün kopyalar), sonra Auth.**

| adım | ne | neden |
|---|---|---|
| 1 | Platform'un **bütün kopyaları** yeni sürüme çıkar | Durum cevabındaki `actorType` alanını yalnız yeni Platform gönderir. |
| 2 | Platform'un her kopyasının durum ucuna `actorType` döndürdüğü doğrulanır | Yük dengeleyici arkasında tek bir eski kopya kalırsa Auth o kopyaya düşen her istekte türü okuyamaz. |
| 3 | Auth yeni sürüme çıkar | Yeni Auth, **gönderilmiş ama bilinmeyen** bir türü **kesin "hayır"** sayar (giriş ve yenileme reddedilir, yenilemede yöneticinin bütün oturumları iptal edilir). Türü **hiç gönderilmemiş** bir cevap ise (eski Platform) FIX3'ten beri **cevap yok** sayılır: 503, oturum iptali yok. |

**Ters sıra ne yapar.** Auth önce çıkarsa, eski Platform'un `actorType` taşımayan her durum
cevabı **cevap yok** sayılır (BL-521 FIX3 madde 5): hiçbir platform yöneticisi giriş ya da
yenileme yapamaz, Platform çıkana kadar her deneme 503 alır — bir **kesinti**dir, oturum silme
değil; Platform çıktığında kendiliğinden düzelir. (FIX2'de bu durum "bilinmeyen tür" sayılıyor
ve yenileme yapan her yöneticinin oturumlarını iptal ediyordu.)

**Geri alma.** Auth çıktıktan sonra Platform geri alınırsa, geri alınan Platform `actorType`
göndermez ve yukarıdaki kesinti yaşanır (oturumlar silinmez). Geri alma gerekiyorsa sıra yine:
önce Auth, sonra Platform.

**E-posta değiştirilemez (FIX4 madde 1; FIX3'ün e-posta taşıması kalktı).** Bir platform
yöneticisinin e-postası değiştirilemez: Platform 409 `PLATFORM_ADMINISTRATOR_EMAIL_IMMUTABLE` verir;
yeni adres yeni yönetici olarak davet edilir, eski kayıt kaldırılır. Senkron
(`POST /api/platform-auth/platform-admins/sync`) e-postayı yalnız hesabı **bulmak** için kullanır;
`previousEmail` gönderilmez, gönderilse de yeni Auth okumaz, hiçbir hesap başka adrese taşınmaz.
Bu madde için dağıtım sırası önemsizdir (eski Auth da hesabı aynı adresle arar).

**Durum ve roller (FIX4 madde 2 ve 5).** Senkron gövdesinde kaydın durumu (`status`) gider: yeni
Auth hesabı yalnız kayıt `Active` ise etkinleştirir, başka durumda (ya da `status` yoksa) hesabın
durumuna dokunmaz ve adresi hiçbir zaman onaylamaz (onay yalnız davet kabulünde). Eski Auth
`status`'u tanımaz, eskisi gibi her senkronda etkinleştirir — eski davranış, kilitlenme yok.
Durum cevabında kaydın rolleri (`roles`) gider: yeni Auth her giriş, yenileme ve zorunlu parola
değişiminde Auth'taki dört platform rolünü kayda yakınsatır ve belirteci onlardan basar; `roles`
göndermeyen eski Platform'da yakınsatma yapılmaz (eski davranış). Sıra yine: önce Platform, sonra Auth.

**Yakınsatma bekliyor işareti (FIX4 madde 2).** Platform güncellemede önce Auth'u eşitler, sonra
kaydı yazar. Kayıt yazımı reddedilir, hata verir ya da sonucu bilinmez kalırsa kayıt **yeniden
okunur** ve Auth kaydın **şimdiki** haline (roller, durum) yakınsatılır; ön görüntü geri
yüklenmez. Bu yakınsatma da başarısız olursa kayıtta `AuthSyncPendingSinceUtc` dolar ve Error
günlüğü yazılır (yalnız yönetici kimliği). Bekleyenleri bulmak (salt okunur):
`db.platform_administrators.find({AuthSyncPendingSinceUtc: {$ne: null}}, {_id: 1, AuthSyncPendingSinceUtc: 1})`.
Kaydın bir sonraki başarılı güncellemesi işareti temizler; giriş ve yenileme rolleri zaten kayıttan alır.

**Son tur (FIX5) — işletim etkisi.**

- **Yönlendirme izlenmez (madde 1).** Auth'un Platform durum istemcisi 3xx cevabını **izlemez**
  (`AllowAutoRedirect = false`): iç anahtar özel bir başlıkta gider ve yönlendirmede başka hedefe
  taşınırdı. 3xx = **cevap yok** (503, oturum iptali yok). Zaman aşımı `PlatformService:TimeoutSeconds`
  (1–30 sn, varsayılan 5). Platform ← Auth yönündeki çağrının (provision / sync) yönlendirmesiz
  istemciye taşınması e-posta dalındadır; o dal birleşirken CT uzlaştırır, bu dalda o satıra dokunulmadı.
- **Cevap istenen adresin cevabı olmalı (madde 2).** Durum cevabındaki `email` istenenle (normalize)
  aynı değilse ya da yoksa Auth cevabı **cevap yok** sayar (503). Yeni Platform `email`'i zaten
  gönderir; karışık sürümde `email` göndermeyen eski bir Platform da 503 alır (kesinti, oturum silme değil).
- **Girişte boş rol listesi (madde 3).** Durum cevabında `roles` **boş ya da yalnız tanınmayan adlardan**
  oluşuyorsa Auth giriş / yenileme / zorunlu değişimde hiçbir platform rolü vermez (ve hesabın
  platform rollerini geri alır); giriş eskisi gibi 403 olur. Davet / senkron (provision / sync)
  yolunun "rol yoksa `ReadOnly`" kuralı değişmedi.
- **Provision gövdesinde `status` (madde 11).** Yeni Auth var olan hesabı yalnız `status = Active`
  ise etkinleştirir ve var olan hesabın e-postasını **onaylamaz**. `status` göndermeyen eski Platform
  ile yeni Auth: var olan hesap etkinleştirilmez (güvenli yön). Eski Auth `status`'u tanımaz ve
  eskisi gibi etkinleştirip onaylar. Sıra önemsiz; yine önce Platform, sonra Auth önerilir.
- **Auth cevap vermiyorsa ikinci çağrı yok (madde 10).** Güncellemede ilk senkron zaman aşımı /
  bağlantı hatası / 5xx ile düşerse Platform aynı Auth'a ikinci (yakınsatma) çağrısı yapmaz;
  kayıtta `AuthSyncPendingSinceUtc` doldurulur ve ilk hata yüzeye çıkar. **Açık:** Platform → Auth
  çağrısının kendi zaman aşımı bu dalda ayarlanmadı (istemci satırı e-posta dalında).
- **Günlük (madde 6).** Platform'un Auth çağrısı hatasında günlüğe yalnız yönetici kimliği, durum
  kodu ve neden kodu yazılır; Auth'un cevap gövdesi ve e-posta yazılmaz.
- **İşaret korunur (madde 8, 9).** `AuthSyncPendingSinceUtc`'yi yalnız güncelleme ve başarılı
  yakınsatma temizler; öteki kayıt yazımları saklı işareti korur. Auth'un giriş bildirimi
  (`accept-login`) artık yalnız son giriş zamanını ve davet durumunu yazar: sürüm, `UpdatedBy` ve işaret değişmez.

**Kiracı kapısı ve platform hesapları (FIX6 madde 13; FIX5'in "bütün kiracı" yasağının yerine).** Platform sistem
kiracısı (`00000000-0000-0000-0000-000000000001`) aynı zamanda varsayılan **iş kiracısıdır**: sıradan kullanıcıları orada
yaşar. Bu yüzden kural kiracıya değil **hesaba** bağlıdır: Auth kaydında `PlatformActorType` taşıyan hesap, "platform hesabı".
- **Kiracı belirteci basılan her yerde platform hesabı reddedilir** (kiracı girişi, MFA doğrulaması, kiracı yenilemesi, kiracı
  zorunlu parola değişimi): kapının kendi genel cevabıyla (yanlış parola / yanlış kod / geçersiz yenileme belirteci), parola
  bakılmadan, belirteç, yenileme satırı ve denetim satırı yazılmadan. Sıradan kullanıcılar eskisi gibi girer. Platform kapısı
  (`/api/platform-auth/login`, yenileme) etkilenmez. Kayıt (register) yalnız YARATTIĞI hesaba belirteç basar; platform
  hesabının adresi her zamanki "kullanımda" 409'u alır.
- **Yalnız platform kapısı aktörü (`actor_type` = `platform_admin` / `partner_admin`) platform hesabını görür; kimliği
  doğrulanmış her öteki istek (kiracı aktörü `tenant_user`, ama aynı zamanda actor_type'ı olmayan, boş ya da bilinmeyen bir
  belirteç) görmez ve dokunamaz (FIX8 madde 2; FIX6 yalnız `tenant_user`'ı gizliyordu, ötekiler her hesabı görüyordu):** liste /
  arama / arama kutusu / dışa aktarım ve üst sayaçlar onu içermez; kullanıcı yönetimi uçları (oku, doğrula, onay, etiket, tür,
  güncelle, sil, askıya al, etkinleştir, parola sıfırla, davet yeniden gönder, rol ata / kaldır) ona **404** verir, olmayan bir
  kimliğin cevabıyla aynı. Kural tek yerde (`HttpPlatformAccountVisibility` → `PlatformAccountScopedUserRepository` +
  `UserListReader`). Kimliği doğrulanmamış istek (anonim kapılar: giriş, MFA doğrulama, yenileme, platform kapısı; iç anahtarla
  gelen iç servis çağrıları) ve istek dışı çalışma (arka plan) gizlenmez. Auth yalnız `tenant_user` ve platform kapısı türlerinde
  belirteç basar; başka bir türde belirteç gelirse platform hesabı ona görünmez (kapalı yönde).
- **"Platform hesabı" her yerde aynı tanımdır (FIX8 madde 3).** `PlatformActorType` en az bir boşluk-dışı karakter taşıyorsa
  hesap platform hesabıdır (`User.IsPlatformAccount`); boş, boşluklu ya da eksik değer platform hesabı **değildir** ve kiracı
  aktörü onu sıradan kullanıcı gibi görür, rol izin kaldırmada oturumu sonlanır, kiracı kapısından girer. Mongo süzgeçleri bu
  kuralın tek paylaşılan tanımını kullanır (`PlatformAccountFilter`, boşluk kümesi `char.IsWhiteSpace`'ten türetilir; liste, arama,
  sayaç ve rol sahipleri aynı süzgeçtir) ve bir testle bellek kuralıyla gerçek belgeler üzerinde karşılaştırılır.
- **Dev etkisi:** `admin@diten.com` (tohum, `PlatformActorType=platform_admin`) artık kiracı kapısından (`/account/login`)
  giremez; aynı kiracının öteki kullanıcıları girer.
- **İşaret platform kapısında onarılır (FIX7 madde 1).** Hesabı "platform hesabı" yapan şey Auth'un kullanıcı üzerindeki
  kopyasıdır (`PlatformActorType`); platform kapısı ise yalnız Platform'un yönetici kaydına güvenir. Kopyası eksik ya da farklı
  bir hesap (eski bir sağlama, kaybolmuş bir eşitleme) platform kapısında yönetici, kiracı kapısında sıradan kullanıcı
  görünürdü. Artık **platform belirtecinin basıldığı her yerde** (platform girişi, platform yenilemesi, platform zorunlu parola
  değişimi) kayıttaki tür (`platform_admin` / `partner_admin`) hesabın üstüne, eksik ya da farklıysa, yazılır: yalnız o alanı
  değiştiren hedefli yazım, yalnız platform sistem kiracısındaki hesap için. Yazım başarısız olursa o basım da başarısız olur
  (500, yeniden dene) — kiracı kapısının kabul edeceği bir hesap bırakılmaz. Sonuç: bir hesap platform kapısından **bir kez**
  geçtikten sonra kiracı kapısı ve kiracı aktörleri için platform hesabıdır; hiç geçmemiş bir hesap kaydı olsa bile işaretsiz
  kalır.
- **Rol düzeyi (FIX7 madde 3).** Kiracı aktörünün rol işlemleri platform hesabına uzanmaz: bir rolden izin kaldırırken o rolü
  tutan platform hesaplarının oturumları sonlandırılmaz; rol listesindeki kişi sayısı platform hesaplarını saymaz. Platform
  kapısı aktörü ve iç çağrılar hepsini görür.
- **Günlük (FIX7 madde 5).** Kiracı kapısının bir platform hesabını reddi tek bir uyarı satırı bırakır:
  `AUTH_TENANT_DOOR_PLATFORM_ACCOUNT_REFUSED`, kapı (`sign-in` / `mfa-verify` / `refresh` / `forced-change`) ve kiracı kimliği;
  e-posta ve kullanıcı kimliği yazılmaz, cevap değişmez. (Kiracı zorunlu parola değişimi HTTP'de bir kiracı belirteciyle çağrılır:
  hesabı daha önce depo gizlediği için 401'i oradan alır ve bu satırı yazmaz; satır, hiçbir şeyin hesabı gizlemediği çağrıda
  yazılır.) Platform hesabının işareti bir basımda düzeltilirse ayrıca `AUTH_PLATFORM_ACCOUNT_DISCRIMINATOR_HEALED` bilgi satırı
  yazılır.
- **Bilinen sınırlar (açıkça).**
  (a) **Bir platform hesabının adresiyle kullanıcı oluşturmak yine 409 "kullanımda" verir.** Adres benzersiz olduğu sürece bu
  yoldan bir hesabın VAR OLDUĞU yoklanabilir; kaçınılmazdır, gizlenmedi.
  (b) **Bu kural gelmeden bir platform hesabına basılmış kiracı belirteci iptal edilmedi.** `SessionTimeoutMinutes` (kiracı
  ayarı, varsayılan 60 dk) dolana kadar o belirteç **içine gömülü izinleriyle** sıradan kullanıcılar ve roller üzerinde işlem
  yapabilir ve öteki servisler tarafından kabul edilir. Bu bir **penceredir**; "hiçbir şeyi yönetemez" değildir. Kapanması için
  belirtecin süresinin dolması ya da o hesabın oturumlarının elle iptali gerekir.
  (c) **Anonim `POST api/tenant-auth/register`, bir platform hesabının adresine aynı 409'u verir** (`RegisterCommandHandler`: adres
  zaten kullanımda). Bu yoklama belirteç gerektirmez; (a)'daki yoklamanın belirteçsiz hâlidir ve aynı nedenle kaçınılmazdır.
  (d) **İşaretsiz bir hesabın kiracı oturumu yenilemede reddedilmez.** Kiracı yenilemesinin platform hesabını reddetmesi (A')
  işarete bağlıdır; işareti eksik hesabın kiracı oturumu, hesap bir platform basımından geçip işaretlenene kadar yenilenmeye devam
  eder ve böylece `SessionTimeoutMinutes`'ı aşar (yenileme ömrü kadar). İşaretin yalnız platform basımında onarılması (FIX7) ile
  birlikte okunmalıdır: hiç platform basımından geçmemiş hesap işaretsiz kalır (tek seferlik doldurma yapılırsa kapanır).
  (e) **Kiracı zorunlu parola değişimi kapısının ret satırı üretim yolunda yazılmaz.** Bu kapı HTTP'de bir kiracı belirteciyle
  çağrılır ve depo hesabı önce gizlediği için "kullanıcı yok" 401'ini verir; satır yalnız hiçbir şeyin hesabı gizlemediği çağrıda
  (handler düzeyinde) yazılır.
  (f) **İşaret yazımı fail-closed'dır (FIX8 madde 1):** yazım hata verirse ya da hiçbir hesapla eşleşmezse (hesap okunduktan sonra
  silinmiş ya da taşınmış) platform girişi / yenilemesi / zorunlu değişimi belirteç basmaz (500); yeniden denenir.

**İç anahtar reddi (FIX3 madde 7).** Platform durum çağrısına 401 / 403 dönerse (iç anahtar iki
serviste aynı değil) Auth bunu **Error** düzeyinde günlükler (anahtar ya da değeri yazılmaz) ve
her platform girişine 503 verir. Düzeltme: Auth'taki `PlatformService:InternalApiKey` ile
Platform'daki `AuthService:InternalApiKey` aynı değeri taşımalı (değerler bu belgeye yazılmaz).

**Geçici hata ile kesin "hayır" ayrımı (FIX2 madde 3).** Dağıtım sırasında Platform'un
verdiği 5xx, ağ hatası, zaman aşımı ya da okunamayan cevap **"hayır" değildir**: Auth o isteği
503 ile reddeder ve **hiçbir oturumu iptal etmez**; kullanıcı yeniden dener. Oturum iptal
eden yalnız Platform'un kesin cevabıdır (kendi 404 zarfı, etkin olmayan kayıt, bilinmeyen
tür). Platform tarafında da yönetici kaydı okunamazsa istek 503 alır, güvenlik olayı
yazılmaz.

**Doğrulama (dağıtımdan sonra).**

1. Bir platform yöneticisiyle giriş → belirteçte `actor_type = platform_admin`.
2. Bir iş ortağı yöneticisiyle giriş → `actor_type = partner_admin`; aynı hesapla zorunlu
   parola değişimi → yine `partner_admin`.
3. Auth günlüğünde `Platform administrator status answered 404 without its envelope` ya da
   `status could not be read` satırları dağıtım penceresi dışında görünmemeli.
