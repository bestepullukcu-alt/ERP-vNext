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
   - **Ne zaman:** yalnız **bütün eski Auth örnekleri durduktan SONRA** (kayan dağıtımda eski bir örnek, işaret koymadan
     pasife alabilir). Tüm kiracılar kapsanır; **platform kiracısı dahil**.
   - **Önce kuru koşu (varsayılan):** hedef veritabanının adını, bulunan hesapların kimlik listesini (kiracı, kullanıcı;
     kişisel veri yok) ve ikinci bir liste olarak "bekleyen davet sayıldığı için atlanan, bağlantısı şu tarihe kadar
     geçerli" hesapları yazar; kullanıcılara hiçbir şey yazmaz. Kuru koşuyu, listeyi okuyup onaylayana dek tekrarlayın.
   - **Uygulama:** `--apply --expect N` (N = son kuru koşunun bulduğu sayı). Sayı tutmazsa araç hiçbir şey yazmaz (çıkış 5).
     Ardından kuru koşuyu **0 diyene kadar** tekrarlayın. Araç idempotenttir.
   - **Tam komut** (repo kökünden; bağlantı ortamdan okunur, komut satırından asla):

     Önce iki ortam değişkenini gizli kasadan yükleyin: `DITEN_AUTH_MONGO_CONNECTION` ve `DITEN_AUTH_MONGO_DATABASE`
     (değerleri komut satırına, geçmişe ya da bu belgeye asla yazılmaz). Sonra **kuru koşu** — aynı komut, argümansız:

     ```bash
     dotnet run --project services/Diten.AuthService/tools/Diten.AuthService.LegacyDeactivationMarker
     ```

     ve listeyi okuduktan sonra **uygulama** (N = son kuru koşunun bulduğu sayı):

     ```bash
     dotnet run --project services/Diten.AuthService/tools/Diten.AuthService.LegacyDeactivationMarker -- --apply --expect N
     ```
   - **Çıkış kodları:**
     - 0 tamam.
     - 1 bağlantı ya da beklenmeyen hata: veritabanına ulaşılmadı, hiçbir şey yazılmadı. Yalnız hata türü yazılır,
       bağlantı dizesi ya da hata metni asla.
     - 2 kullanım hatası.
     - 3 yarıda kaldı: yazılan ve **ulaşılmayan** kimlikler ayrı ayrı listelenir; kuru koşuyu tekrarlayın.
     - 4 liste okunduktan sonra değişen hesaplar atlandı (listelenir; dokunulmadı). Uyarı, "APPLIED" satırından önce yazılır.
     - 5 beklenen sayı tutmadı, hiçbir şey yazılmadı.
     - 6 iş bitti ama denetim satırı yazılamadı: kayıt eksik. Denetim kaydı sahibine bildirin.
   - **Denetim satırı:** veritabanına ulaşan her çalışma Auth denetim kaydına (`authAuditLogs`,
     `auth.legacy_deactivation_marker.run`) bir satır yazar: mod (`dry-run`, `apply`, `refused`), sayılar ve kimlikler;
     kişisel veri yok. Yarıda kalan çalışma da yazar (`stoppedPartWay`, ulaşılmayanlar). Çıkış 1 ve 2'de satır yazılmaz:
     veritabanına hiç ulaşılmadı.
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
2. **`Eventing:Transport=InMemory` ise yeni kiracı yöneticisi otomatik davet edilmez; operatör "Davet et"i kullanır.**
   Bugünkü taban ayar InMemory'dir (BL-536). Bu durumda kiracı açılışındaki olay tüketicisi koşmaz. Kiracı kaydındaki
   "Initial Admin Invitation" (`admin-invitation`) adımı **Bekliyor** kalır; operatör kiracı ekranında "Davet et"e basar.
   Development dışında Platform başlangıçta bir kez uyarı yazar: `tenant.admin_invitation.automatic_off`.
   RabbitMQ ile tüketici koşar ve adımı kendisi günceller: Tamamlandı ya da Başarısız + neden.
   Olay yolu yalnız YENİ hesap yaratır; var olan hesaba dokunmaz.
3. **Dağıtım sırası: önce Auth, sonra Platform.**
   - Yeni Platform, kiracı açılış olayında Auth'un yeni `internal/events/tenant-admin-created` kapısını çağırır. Bu kapı yalnız yaratır.
   - Eski bir Auth bu kapıya 404 döner: Platform kapalı başarısız olur, olay yeniden denenir. Hiçbir hesap sıfırlanmaz.
   - **404 yeniden denemeleri tükenirse** (`Eventing:RetryCount`, taban 5; 10 sn'den 300 sn'ye üstel): olay tüketicinin hata
     kuyruğunda bekler (MassTransit varsayılanı `TenantLifecycleNotification_error`; canlı adı ölçülmedi). Kiracının
     `admin-invitation` adımı **Bekliyor** kalır ve ayrı bir uyarı **yoktur**; günlükte yalnız `Tenant admin provisioning
     failed … StatusCode=404`. **Yeniden oynatma:** Auth güncellendikten sonra hata kuyruğundaki iletileri RabbitMQ yönetim
     ekranından ("Move messages") ana kuyruğa taşıyın, ya da kiracı ekranında "Davet et"e basın. Olay yolu yalnız yaratır;
     yeniden oynatmak var olan hesaba dokunmaz.
   - Ters ara durum **yalnız `tenant-admin-created` kapısını bilmeyen Platform sürümleri için geçerlidir** (S2D-FIX2
     `e3d3a7331`'den önceki her Platform). Yeni Auth ile böyle bir eski Platform birlikteyken, eski Platform'un "Davet et"i var
     olan bir hesap için `trigger` göndermez. Yeni Auth bunu yalnız yaratır sayar ve `setupToken: null` döner. Eski Platform bu
     cevabı okuyamaz ve operatöre 502 gösterir. Hiçbir hesap sıfırlanmaz; Platform da güncellenince düzelir.
4. **Mongo sürümü:** gönderim satırlarının "etkileri bekliyor" dizini (`ix_notification_dispatches_permanent_effects_pending`)
   dizi değerli bir alanda `$eq` kısmi filtresi kullanır (`DateTimeOffset` `[ticks, offset]` olarak saklanır). Bu yalnız
   dev Mongo **7.0.28**'de ölçüldü. Canlı Mongo sürümü farklıysa deploy'dan sonra dizinin var olduğunu
   (`db.notification_dispatches.getIndexes()`) ve başlangıç günlüğünde dizin hatası olmadığını kontrol edin.

**Belirti:** (1) kiracı açılışından sonra ilk yöneticinin e-postası gelmez ve Platform günlüğünde `INVITE_LINK_ROOT_…`.
(2) Başlangıçta `IndexOptionsConflict` ya da kısmi filtre hatası.

---

## Tamamlananlar

*(henüz yok)*
