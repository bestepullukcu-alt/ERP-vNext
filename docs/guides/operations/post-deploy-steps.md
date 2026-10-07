# Canlıya Çıkış Sonrası Operatör Adımları

> Bu belge **geliştirme kuyruğu değildir.** Buradaki maddeler kod değil, canlı ortamda
> **elle yapılacak işlerdir**: bir yetki satırının açılması, bir tanımın girilmesi, bir
> ayarın çevrilmesi. Kod deploy edildikten sonra biri bunları yapmazsa özellik oradadır
> ama çalışmaz.
>
> `docs/roadmap/backlog/product-backlog.md` yerine burada durmalarının sebebi: backlog'a
> yazılan madde "yapılacak geliştirme" diye okunur ve deploy günü kimse ona bakmaz.

## Nasıl kullanılır

1. **Deploy öncesi** bu dosyayı aç, ilgili modülün satırını oku.
2. Deploy sonrası adımı uygula.
3. Satırı "Tamamlananlar" bölümüne taşı — tarih ve kim yaptığıyla.
4. Yeni bir modül elle bir adım gerektiriyorsa, **o modülün pack'i kapanmadan önce**
   satırı buraya ekle. Pack kapanışında bu kontrol edilir.

⚠ Bir maddeyi silme. Tamamlanan madde aşağı taşınır; çünkü "bu neden böyle ayarlanmış"
sorusunun cevabı altı ay sonra yalnız burada kalır.

---

## Bekleyen adımlar

### 1 · WORK-REPORT yetki (entitlement) satırı

| | |
|---|---|
| **Modül** | MOD-0024 — İş Raporu ekranı |
| **Ne zaman** | İş Raporu canlıya çıktığı deploy'da |
| **Yapılmazsa** | Ekran vardır, **hiçbir kiracı göremez**. Menüde çıkmaz, doğrudan adres yazılsa da yetki reddi alınır. |
| **Kim** | Platform operatörü |

İş Raporu kendi modülü olarak kayıtlıdır (`work-report`) ve kiracıların bu modüle
erişimi entitlement satırıyla açılır. Kod PR #91 ile main'e girdi; satır açılmadı.

**Belirti:** kullanıcı "İş Raporu menüde yok" der, geliştirici koda bakar, kod yerindedir.
Sebep koddaki bir hata değil, açılmamış bir kapıdır.

### 2 · Organizasyon özel alan tanımları

| | |
|---|---|
| **Modül** | MOD-0288-FU02 / FU04 — organizasyon özel alanları |
| **Ne zaman** | FU04 canlıya çıktıktan **ve yönetişim onayı geldikten** sonra |
| **Yapılmazsa** | Ekran boş bir alan listesiyle açılır; birim formunda özel alan bölümü hiç görünmez. |
| **Kim** | Kiracı organizasyon yöneticisi (geliştirici değil) |

Alan tanımları **kod değil veridir** ve deploy ile taşınmaz. Test ortamında tanımlananlar
canlıya gelmez; canlıda **yeniden** tanımlanır. FU04'te içe/dışa aktarma yoktur.

Yöneticinin defterindeki (`GMG-CGV-LOG-0005`) yedi yönetişim alanı ve önerilen tipleri:

| Alan | Tip |
|---|---|
| Permanent OU ID | Metin |
| Regulatory role / independence requirement | Tek seçim |
| Accountable executive | Referans (pozisyon) |
| Approval reference | Metin |
| Charter reference | Metin |
| IT-directory mapping status | Evet / Hayır |
| Evidence ref | Metin |

⚠ **Tanım kodu sonradan değiştirilemez.** `regulatory.role` yazılıp kaydedildiyse
`regulatory-role` yapılamaz — girilmiş değerler o koda bağlıdır. Düzeltme yolu yeni alan
açıp eskisini pasife almaktır, yani canlıda ilk yazımda dikkat gerekir.

⚠ **Yönetişim kapısı açık.** Yöneticinin paketi kendi kapağında *"ERP kataloğu
hazırlanabilir, yüklenemez"* diyor: 38 doküman DRAFT, 50 birim *Proposed*, beş kapının
beşi de karşılanmamış. Kod canlıya çıkabilir; bu veri girişi o onayı bekler.

---

### 3 · BL-529 — Auth: güvenilir vekiller ve herkes bir kez yeniden oturum açar

| | |
|---|---|
| **Modül** | Auth (AuthService) — yöneticinin parola sıfırlaması, BL-529 |
| **Ne zaman** | BL-529'u (WP-AUTH-ADMIN-RESET-01) taşıyan deploy'da |
| **Yapılmazsa** | (1) Platform'un anonim parola kapılarında istemci başına hız sınırı **kapalı** kalır (e-posta başına sınır çalışır); Auth başlangıçta bir kez uyarı yazar. (2) Değişiklik değil, beklenen davranış: herkes bir kez yeniden oturum açar. |
| **Kim** | Altyapı / deploy operatörü |

1. **`ClientAddress:TrustedProxies`** (Auth yapılandırması): Auth'a doğrudan bağlanan **Web sunucusunun ve ağ
   geçidinin** IP adreslerini, her birini ayrı bir öğe olarak ve adresin kanonik yazımıyla yazın (IPv4 dört sayı,
   IPv6 sıkıştırılmış, ör. `::ffff:10.0.0.1`; aralık / CIDR ve `10.0.1` gibi kısaltmalar desteklenmez; geçersiz bir
   değer Auth'u başlangıçta adını söyleyen bir hatayla durdurur). Web son kullanıcının adresini `X-Forwarded-For` ile zaten
   iletiyor; Auth bu başlığı YALNIZ listedeki bir karşı taraftan gelirse okur. Liste boş kalırsa istemci başına sınır
   kapalı kalır — herkes için tek kova hiçbir zaman oluşmaz. **Liste yalnız Auth başlarken okunur: listeyi değiştirdikten
   sonra Auth yeniden başlatılmalıdır.** Listede olmayan bir karşı taraf `X-Forwarded-For` gönderirse Auth bunu
   (karşı taraf başına bir kez) karşı tarafın adresini (`Peer …`) söyleyen bir uyarı olarak yazar — o adres de listeye
   eklenmeli mi diye bakın. En çok 256 farklı karşı taraf adlandırılır; sonrası için bir kez "no further peers are
   named" satırı yazılır ve Auth yeniden başlayana dek susar. Not: ağ geçidi (Ocelot) son kullanıcının adresini
   başlığa kendisi eklemiyorsa, ağ geçidine doğrudan gelen isteklerde bu adres sahtelenebilir; Ocelot'a dokunulmadı.
2. **Herkes bir kez yeniden oturum açar.** BL-529'dan önce basılmış yenileme belirteçleri parolaya bağlı değil; ilk
   yenilemede 401 alırlar. Kullanıcı bir kez yeniden giriş yapar; destek ekibine önceden söyleyin.
   Aynısı e-posta doğrulama kodları (MFA) için de geçerli: deploy'dan önce gönderilmiş, henüz girilmemiş bir kod bir kez
   reddedilir; kullanıcı yeni bir kod ister (kodun ömrü en çok 15 dakika).
3. **BL-529'dan önce pasife alınmış hesaplar işaretlenir (tek seferlik).** BL-529'dan önce bir yöneticinin pasife
   aldığı hesapta "yönetici tarafından pasife alındı" işareti yok ve bekleyen parola bağlantısı silinmedi; süresi
   dolmamış böyle bir bağlantı (kiracıda 7 gün, platformda 24 saat) hesabı yeniden açabilir. Araç
   `services/Diten.AuthService/tools/Diten.AuthService.LegacyDeactivationMarker` bu hesaplara işareti koyar ve
   bağlantılarını siler:
   - **Hangi hesaplar:** silinmemiş, pasif (`IsActive=false`), işaretsiz ve **bekleyen davet olmayan** her hesap.
     "Bekleyen davet" kodun kendi tanımıdır (`User.IsInvitationPending`): parola değişikliği zorunlu, e-posta
     onaylanmamış ve hiç oturum açılmamış. **Bekleyen davetler belgelenmiş istisnadır:** hiç etkinleşmedikleri için
     pasiftirler ve pasife alınmış bir davetten mekanik olarak ayrılamazlar; araç onlara dokunmaz.
   - **Önce kuru koşu (varsayılan):** sayıyı ve kimlik listesini (kiracı kimliği, kullanıcı kimliği; kişisel veri yok)
     yazar, hiçbir şey yazmaz. Listeyi okuyun; sonra `--apply` ile yazın. Araç idempotenttir: ikinci koşu bir şey bulmaz.
   - **Bağlantı ortamdan okunur, komut satırından asla:** `DITEN_AUTH_MONGO_CONNECTION`, `DITEN_AUTH_MONGO_DATABASE`.
   - Gerçek ortamda çalıştırmak sahibin kararıdır.

**Belirti:** (1) Auth günlüğünde başlangıçta "`ClientAddress:TrustedProxies` is empty" uyarısı. (2) Deploy'dan sonra
herkesin bir kez oturum açma sayfasına düşmesi — hata değil. (3) Araç koşulmadıysa kuru koşusu sıfırdan büyük bir sayı
verir.

### 4 · BL-454 — E-posta dilim 2: kiracı yöneticisi davet bağlantısının kökü ve Mongo sürümü

| | |
|---|---|
| **Modül** | MOD-0027 bildirimler + Auth — kiracı yöneticisi daveti (WP-EMAIL-SHELL-01 dilim 2, aşama D) |
| **Ne zaman** | Dilim 2'yi taşıyan deploy'da, **ilk kiracı açılışından önce** |
| **Yapılmazsa** | Yeni kiracının ilk yöneticisine davet e-postası **gitmez**: Platform `tenant.admin_invitation.not_sent ReasonCode=INVITE_LINK_ROOT_LOOPBACK` (ya da `…_MISSING`) yazar. Hesap Auth'ta açılmış olur; kök girildikten sonra kiracı ekranındaki "Davet et" bağlantıyı yeniden gönderir. |
| **Kim** | Altyapı / deploy operatörü |

1. **`AuthService:FrontendBaseUrl`** (Platform yapılandırması): kullanıcıların açtığı web adresinin kökü, ör.
   `https://app.<alan-adı>`. Taban `appsettings.json` değeri `http://localhost:5001`'dir; Development dışında localhost
   ya da boş kök reddedilir, çünkü düğmesi okuyanın kendi makinesini gösteren bir davet kimseyi içeri almaz. Davet
   e-postası bu kökle BL-529'un tek kullanımlık "parola belirle" bağlantısını taşır (7 gün geçerli); parola içermez.
2. **Mongo sürümü:** gönderim satırlarının "etkileri bekliyor" dizini (`ix_notification_dispatches_permanent_effects_pending`)
   dizi değerli bir alanda `$eq` kısmi filtresi kullanır (`DateTimeOffset` `[ticks, offset]` olarak saklanır). Bu yalnız
   dev Mongo **7.0.28**'de ölçüldü. Canlı Mongo sürümü farklıysa deploy'dan sonra dizinin var olduğunu
   (`db.notification_dispatches.getIndexes()`) ve başlangıç günlüğünde dizin hatası olmadığını kontrol edin.

**Belirti:** (1) kiracı açılışından sonra ilk yöneticinin e-postası gelmez ve Platform günlüğünde `INVITE_LINK_ROOT_…`.
(2) Başlangıçta `IndexOptionsConflict` ya da kısmi filtre hatası.

---

## Tamamlananlar

*(henüz yok)*
