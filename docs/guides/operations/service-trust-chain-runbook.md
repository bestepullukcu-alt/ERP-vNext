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
