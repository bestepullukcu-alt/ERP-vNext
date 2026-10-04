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

**E-posta değişikliği (FIX3 madde 2).** Platform bir yöneticinin e-postasını değiştirirken önce
Auth'u eşitler (`POST /api/platform-auth/platform-admins/sync`, gövdede `previousEmail` = kaydın
eski adresi), kaydı ancak Auth kabul ederse yazar; kayıt yazılamazsa Auth'u eski değerlere geri
çeker. Eski Auth `previousEmail`'i tanımaz, hesabı yeni adresle arar, bulamaz (404): Platform
kaydı **yazmaz**, istek reddedilir. Yani Platform önce çıktığında Auth çıkana kadar yalnız
e-posta değişikliği yapılamaz; kimse kilitlenmez.

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
