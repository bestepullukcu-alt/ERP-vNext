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
| 3 | Auth yeni sürüme çıkar | Yeni Auth, türü bilinmeyen (ya da hiç gönderilmemiş) bir cevabı **kesin "hayır"** sayar: girişi ve yenilemeyi reddeder, yenilemede yöneticinin **bütün oturumlarını iptal eder**. |

**Ters sıra ne yapar.** Auth önce çıkarsa, eski Platform'un `actorType` taşımayan her durum
cevabı "bilinmeyen tür" sayılır: hiçbir platform yöneticisi giriş yapamaz, yenileme yapan her
yöneticinin oturumları iptal edilir. Bu, bir kesinti değil, bir **oturum silme** olayıdır;
Platform çıktıktan sonra kendiliğinden düzelmez, herkes yeniden giriş yapar.

**Geri alma.** Auth çıktıktan sonra **Platform geri alınmaz** (ya da Auth ondan önce geri
alınır). Geri alınan Platform yine `actorType` göndermez ve yukarıdaki tablo tersinden
işler. Geri alma gerekiyorsa sıra: önce Auth, sonra Platform.

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
