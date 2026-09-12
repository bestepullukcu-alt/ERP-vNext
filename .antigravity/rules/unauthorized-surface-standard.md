# Yetkisiz Ekran Standardı — UAS-001

> Bu kural 2026-09-07'de yazıldı çünkü sahip Roller sayfasını yetkisiz bir kullanıcıyla
> açtı ve ekranın yarısı çizildi: başlık, KPI kartları, **"+ Rol Ekle" butonu**, sonsuz
> dönen "Loading…", ve *"0 kayıttan 0-0 arasındaki kayıtlar"*. Üstüne İngilizce bir
> `Permission denied` bildirimi.
>
> Kullanıcı bundan **"yetkim yok"** sonucunu çıkaramaz. Çıkardığı sonuç **"veri yok"**
> veya **"sistem bozuk"** olur — ve tıklayabileceği bir buton durur.
>
> O gün ölçüldü:
>
>     58 controller
>       37  yalnız [Authorize]     → Roller gibi davranır
>       18  izin kontrolü yapar
>     "erişiminiz yok" mesajı: 2 sayfada
>
> Yani doğru desen 2 sayfada, yanlış desen 37 sayfada. Sebep tembellik değil, kural
> yokluğu: söyleyen bir şey olmayınca 37 ayrı yorum çıkar.

---

## 1. Kural

**Bir sayfa, göstereceği verinin iznine sahip olmayan kullanıcıya o sayfanın iskeletini
çizmez.** Ne başlık, ne kart, ne boş tablo, ne eylem butonu.

Yerine **tek bir açıklama** gösterir: ne olduğu ve ne yapılacağı.

    Görev Merkezi'ne erişiminiz yok
    Bu modüle erişim için yöneticinizden yetki isteyin.

## 2. Yönlendirme YAPILMAZ

Yetkisiz kullanıcıyı başka bir sayfaya atmak yanlıştır. Dört sebep:

1. Kullanıcı **nereye gittiğini anlamaz** — tıkladığı yer değil, başka bir yer açılır.
2. **Geri tuşu döngüye girer** — geri bas, yetkisiz sayfa, yine yönlendirme.
3. Yetki verildikten sonra kullanıcı **o adrese dönemez**, çünkü adresi kaybetmiştir.
4. "Yetkin yok" ile "sayfa yok" **ayırt edilemez** hale gelir.

Adres çubuğu doğru kalır, kullanıcı nerede olduğunu bilir, yetki verilince sayfayı
yenilemesi yeter. SAP, Oracle ve Workday de yetkisiz ekranı **kendi yerinde** açıklar.

## 3. Nerede gösterilir — oturum durumuna göre

| Durum | Kod | Nerede | Neden |
|---|---:|---|---|
| Oturum yok | 401 | **tam sayfa**, kabuksuz (`NotAuthorized.cshtml`) | kabuk çizilemez; menü kullanıcıya ait ve kullanıcı yok |
| Oturum var, izin yok | 403 | **kabuğun İÇİNDE**, sayfa gövdesinde | kullanıcı sistemdedir; menüsünü kaybetmemeli, başka yere gidebilmeli |

⚠ İkinci satır bugün eksik: `NotAuthorized.cshtml` `Layout = null` ile çalışıyor ve
kullanıcıyı kabuktan çıkarıyor. 403 için **kabuk içinde** çalışan paylaşılan bir partial
gerekir; Görev Merkezi bunu kendi içinde çözmüştür ve o çözüm paylaşıma çıkarılmalıdır.

## 4. Nasıl kontrol edilir — altyapı mevcut, yeniden yazılmaz

    @inject Diten.Web.Services.IPermissionSnapshot Perms

    @if (!Perms.Has("platform.roles.read"))
    {
        <partial name="_AccessDenied" model="..." />
        return;
    }

`_PermissionBootstrap.cshtml` istemci tarafı için aynı listeyi taşır
(`window.__permissionSnapshot`).

⚠ **Bu bir yetki kararı DEĞİL, bir görüntüleme kararıdır.** Yetkinin tek doğruluk
kaynağı backend'deki `[HasPermission]`'dır ve öyle kalır. Buradaki kontrol yalnız
"kullanıcıya ne gösterelim" sorusunu cevaplar. İkisi çift kontrol değildir: biri UX,
diğeri güvenliktir. Sunucu tarafı kontrolü kaldırılırsa güvenlik değil, yalnız ekran
bozulur — nitekim bugün 37 sayfada bozuk.

## 5. Metin kuralları

- **7 dil zorunlu** (`en·tr·fr·es·zh·ar·ru`), resx'ten gelir. Hardcoded metin yasak.
- ⚠ `NotAuthorized.cshtml` bugün **karışık dilli**: `"Oturum gerekli"` ile
  `"You don't have permission to access this page."` aynı dosyada. Bu düzeltilmelidir.
- Mesaj **ne yapılacağını** söyler: *"yöneticinizden yetki isteyin"*. Yalnız
  "erişim reddedildi" demek kullanıcıyı çıkmaza sokar.
- Teknik metin yasak: `403`, `Permission denied`, `Forbidden` kullanıcıya gösterilmez.

## 6. Yasaklar

⚠ **Kırmızı hata bildirimi (toast) ile yetkisizlik anlatılmaz.** Bildirim kaybolur,
sayfa yarım kalır, kullanıcı elinde açıklamasız bir ekranla kalır.

⚠ **Boş liste yetkisizlik anlamına gelmez.** *"0 kayıt"* gösteren bir tablo, kullanıcıya
verinin olmadığını söyler. Yetkisiz kullanıcı tabloyu hiç görmemelidir.

⚠ **Tıklanamayacak buton gösterilmez.** Yetkisiz kullanıcıya "+ Yeni" göstermek, onu
ikinci bir hataya yönlendirmektir.

## 7. Denetim

    # UAS-001 — yalnız [Authorize] tasiyan controller sayisi
    grep -l "^\[Authorize\]" frontend/Diten.Web/Controllers/*.cs | wc -l

    # izin kontrolu yapan sayfa sayisi (artmali)
    grep -rl "IPermissionSnapshot" frontend/Diten.Web/Views/ | wc -l

    # kullaniciya gosterilen teknik metin (0 olmali)
    grep -rn "Permission denied\|Forbidden" frontend/Diten.Web/Views/ frontend/Diten.Web/wwwroot/assets/js/

Sayı yalnız **iyileşme yönünde** değişir. 2026-09-07 taban değerleri: 37 / 17 / var.

## 8. Yeni sayfa açarken

Bu kural `add-module.md` Faz 4 ve `release-checklist.md` üzerinden bağlanır. Yeni bir
sayfa, yetkisiz kullanıcı senaryosu **canlı olarak denenmeden** kapanmaz — testler bu
kusuru göstermez, çünkü test kullanıcısı genellikle her yetkiye sahiptir.
