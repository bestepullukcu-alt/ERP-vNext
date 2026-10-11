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

### 3 · Global Ürün / GSKU / LSKU denetim işlemleri: Platform ÖNCE (BL-552, BL-566)

| | |
|---|---|
| **Modül** | MOD-0290 — Global Ürün düzeltme, emeklilik isteği ve kimlik onayı (BL-552); GSKU / LSKU (LSKU FIX1); Platform MOD-0021 merkezi denetim |
| **Ne zaman** | BL-552'yi (akış 2–3, `23fb2d85f` ve sonrası) ve LSKU FIX1'i taşıyan deploy'da — **Platform, MDM'den ÖNCE** |
| **Yapılmazsa** | MDM'nin yeni denetim işlemleri (79 `GlobalProductIdentityDecisionNotApplicable`, 80 `GlobalProductCorrectionEnded`, 81 `GlobalProductRetirementEnded`, 82 `GskuChildCreationAdmissionReleased`, 83 `LskuDraftCancelled`, 84 `LskuIdentityDecisionNotApplicable`, 85 `LskuRetirementManualReconciliationRequired`, 86 `LskuIdentitySubmitAbandoned`) Platform'da tanınmaz. Platform 409 `AUDIT_SOURCE_INTENT_MAPPING_UNSUPPORTED` döner. Canlı Platform'da bugün hiçbir MDM ürün işlemi eşlenmiş değil (harita `origin/main`'de yok), bu yüzden bu ilk sürümde MDM'nin ürettiği **bütün** ürün işlemleri için (üretilmeyen 43 / 47 hariç; kural ve taban tek yerde: `CentralMappingRollout`) en çok 2880 deneme (varsayılan 30 sn ile yaklaşık bir gün) bekler; süre geçerse **niyetler ölü mektuba düşer ve merkeze ulaşmaz**. Ölü mektuptan yeniden gönderme aracı yoktur. |
| **Kim** | Deploy operatörü |

**Kontrol:** MDM deploy edildikten sonra son teslim hatası `AUDIT_SOURCE_INTENT_MAPPING_UNSUPPORTED` olan niyet kalmamalı.
Kalıyorsa Platform bu işlemleri eşleyen sürümde değildir. Teslim işçisi o niyetleri yeniden deneyen her döngüde bir
Warning yazar: "Audit delivery: N intent(s) of … wait for Platform to map the operation". N toplam birikim değildir:
döngü kiracı başına en çok `BatchSize` (varsayılan 25) niyet dener, N o döngüde denenip bekletilenlerin sayısıdır.
Platform 409'un gövdesini zamanında vermezse (500 ms) bekleyebilen işlemler (bu sürümde üretilen bütün ürün işlemleri)
`AUDIT_SOURCE_INTENT_TIMEOUT` ile yeniden denenir; yalnız üretilmeyen 43 / 47 için 409 kalıcıdır.

### 4 · GP kimlik onayı ayarları artık başlatmayı durdurur (BL-552 akış 3)

| | |
|---|---|
| **Modül** | MOD-0290 — Global Ürün kimlik onayı işçisi (`ProductIdentityWorkflowWorker`) |
| **Ne zaman** | `1204887c3` ve sonrasını taşıyan deploy'dan ÖNCE yapılandırmaya bakılır |
| **Yapılmazsa** | Aralık dışı değer artık sessizce düzeltilmez; MDM **açılmaz** (`PRODUCT_IDENTITY_WORKFLOW_WORKER_CONFIGURATION_INVALID`). İşçi kapalı olsa da geçerlidir, çünkü etkileşimli gönderim de bu ayarları okur. |
| **Kim** | Deploy operatörü |

Sınırlar: `StartSettleSeconds` 1–600 (varsayılan 120; üç GP işçisinde de — işletim kuralı: Platform `WorkflowStartRecovery:AbandonAfterMinutes` × 60 ≥ MDM `StartSettleSeconds` + 300; Platform tabanı 15 dk olduğu için 600 her Platform ayarında güvenli; 600'ü aşan değer artık açılışı durdurur); `QuarantineAfterFailedAttempts` 0–1000 (varsayılan 30, 0 = hiç);
`StuckGaugeIntervalSeconds` 15–3600 (varsayılan 60). Aynı `StuckGaugeIntervalSeconds` sınırı GP düzeltme ve emeklilik
isteği işçilerinde de vardır; o iki işçi de artık aralık dışı ayarla **kapalıyken bile** açılmayı durdurur
(`GLOBAL_PRODUCT_CORRECTION_WORKER_CONFIGURATION_INVALID`, `GLOBAL_PRODUCT_RETIREMENT_WORKER_CONFIGURATION_INVALID`).

**GSKU / LSKU işçileri de (WP-MDM-SETTLE-CAP-01):** `GskuCorrectionWorkflowWorker`, `GskuRetirementRequestWorkflowWorker`,
`LskuIdentityWorkflowWorker`, `LskuRetirementRequestWorkflowWorker` bölümlerinde `StartSettleSeconds` artık 1–600
(dün 1–3600). Aşan değerde MDM **açılmaz**: `GSKU_CORRECTION_WORKER_CONFIGURATION_INVALID`,
`GSKU_RETIREMENT_REQUEST_WORKER_CONFIGURATION_INVALID`, `LSKU_IDENTITY_WORKFLOW_WORKER_CONFIGURATION_INVALID`,
`LSKU_RETIREMENT_WORKER_CONFIGURATION_INVALID`. İzlenen yapılandırmada değer yok (varsayılan 120); deploy operatörü canlı
ortam değişkenlerine bakar.

### 5 · Marka modülünün (`brand-product-master`) planlara eklenmesi

| | |
|---|---|
| **Modül** | MOD-0290 — Marka / Ürün ana verisi (WP-MDM-BRAND-ACCEPT-01 FIX2 madde 1, FIX3 R-1 / M-4 / M-7) |
| **Ne zaman** | Marka işi birleştikten sonraki İLK deploy'da, aşağıdaki dağıtım sırasıyla |
| **Yapılmazsa** | **Kiracı Markalar menüsünü ve Ctrl+K girişini kaybeder.** BRANDS sayfası artık `brand-product-master` modülündedir ve menü plan güdümlüdür; modülü içermeyen plandaki kiracının menüsünde Markalar yoktur. Yeni kiracının Admin rolü marka anahtarlarını (`mdm.brands.*`, `mdm.products.*`, `mdm.brands.export`) plan senkronundan almaz. |
| **Kim** | Platform yöneticisi. Dev'de sahip, canlıda sahibin onayıyla. Kod plan verisini **yazmaz**. |

**Adım (kesin):** Platform → Planlar'da `product-item-sku-master` içeren her plana
`brand-product-master` eklenir; eklenmezse kiracı Markalar menüsünü ve Ctrl+K girişini kaybeder.

Plan → modül üyeliği **veridir**: seed ya da Platform kodu değişmez (CONTROL TOWER kararı,
2026-10-06). Kural: bugün `product-item-sku-master` içeren HER plan; CRM'in marka tüketen
planları ayrıca değerlendirilmez — marka, ürün ana verisiyle aynı pakettir (SAP / Oracle'da
da marka, malzeme ana verisinin parçasıdır).

**Ölçüm (dev, salt okunur, 2026-10-07):** `diten_personalization_dev` içinde 5 plan var
(FREE, STARTER, PROFESSIONAL, ENTERPRISE, DEV); **hiçbiri** `PRODUCT-ITEM-SKU-MASTER` içermiyor
(`IncludedModuleKeys`; DEV planı yalnız GOLDENSLIM, LEGAL-ENTITY, WORKFLOW). Hiçbir kiracının bu
modüle `tenant_module_entitlements` satırı da yok. Yani dev'de eklenecek plan **yok**; canlıda
aynı sorgu deploy öncesi yeniden çalıştırılır (dev ölçümü canlıyı temsil etmez):

```
db.platform_subscription_plans.find({ IncludedModuleKeys: "PRODUCT-ITEM-SKU-MASTER" }, { Code: 1, Name: 1 })
```

**Dağıtım sırası (FIX3 M-7, FIX4 F4): Platform → Auth → MDM + Web (MDM ile Web BİRLİKTE).**

1. **Platform** (katalog) — önce. MDM, açılışta iki manifestini (`product-item-sku-master` ve
   yeni `brand-product-master`) Platform'a kaydeder; Platform yoksa kayıt yeniden denenir.
2. **Auth** — sonra. Açılışta `mdm.brands.export` kataloğa girer, `tenant-97c5`'in sabit hakkına
   eklenir ve **tek yönlü, kiracı başına bir kez** geri doldurma çalışır — dışa aktarma anahtarlarının
   TEK kuralı (`ExportGrantBackfill.Brands`, `ExportGrantBackfillRunner`; `auth.users.export` ve
   `auth.roles.export` ile aynı): anahtardan ESKİ bir kiracının, anahtardan eski ve `mdm.brands.read`
   tutan rolleri `mdm.brands.export` alır; anahtardan sonra kurulan kiracı ve rol almaz. Verilen izin
   Manual'dır (`brand-product-master` Admin şablonunda değil), yönetici Rol İzinleri ekranından geri
   alabilir ve geri alması kalıcıdır (işaretli kiracıya bir daha bakılmaz).
3. **MDM ve Web — aynı pencerede, birlikte.** Kayıt Platform'da yeni modülü oluşturur, Auth
   eşitlemesi izinleri işler. İkisi ayrı çıkarsa:
   - **yeni MDM + eski Web:** her ürün düzenlemesi 400 `expected_version_required` (eski form
     sürüm göndermez);
   - **yeni Web + eski MDM:** ürün Oluştur / Düzenle'de marka seçicisi boş (eski MDM
     `isLinkable` göndermez; yeni Web yalnız MDM'in bağlanabilir dediğini sunar).
   İkisi de veri kaybetmez ama ekranı kullanılamaz yapar: MDM ve Web aynı deploy'da çıkar.

Geçiş sırasında beklenen, **zararsız** eşitleme hataları:
- MDM, Platform'dan önce açılırsa: modül kaydı başarısız ve yeniden deneniyor uyarısı
  (Platform gelince düzelir).
- Auth eşitlemesi `brand-product-master` için boş ya da eksik tanımlayıcı görürse: o kiracı
  atlanır (boş çekme = atla); bir sonraki eşitlemede düzelir.
- Plan adımı yapılmadan: yeni kiracılarda marka anahtarı yok; mevcut kiracıların elle ya da
  seed'le verilmiş hakları **silinmez** (model A mevcut yetkiyi kaldırmaz).

⚠ **Geçiş riski 🟡.** Ekleme yapılana kadar plan güdümlü kiracılar Markalar menüsünü görmez
(sessiz, veri kaybı yok). `tenant-97c5`'in seed'deki sabit marka hakları (`DataSeeder`
`BrandProductPermissionKeys`) dokunulmadan kalır; ekleme sonrası o kiracıda aynı anahtar
hem sabit hem plan kaynaklı görünebilir — yinelenen yetki, fazladan erişim değildir.

### 6 · Kalem birim listesinde IU (ITEM-UNIVERSAL-V2): Platform ÖNCE

| | |
|---|---|
| **Modül** | MOD-0048-FU01 §4 evrensel kalem listeleri (Platform) → MOD-0290-FU04 kalem (MDM) — WP-PLT-ITEM-LISTS-02 |
| **Ne zaman** | IU'yu (ITEM-UNIVERSAL-V2) taşıyan deploy'da — **Platform, MDM'den ÖNCE** |
| **Yapılmazsa** | Eski Platform IU'yu tanımaz: MDM'nin sorusuna 404 `REFERENCE_VALUE_NOT_FOUND`, kullanıcıya 422 `ITEM_REFERENCE_VALUE_INVALID`. Kayıt saklanmaz (kapalı başarısızlık), yalnız IU'lu kalem / çevrim girilemez. IU'suz kalemler etkilenmez. |
| **Geri alma** | Güvenli. V1 kanıtı taşıyan kalemler V2'de geçerli kalır (değişmeyen listelerin kimliği aynı; item-uom V1 kodları aynı anlamda). V2'ye geçmiş Platform geri alınırsa yalnız IU yeniden reddedilir; IU ile kaydedilmiş kalemlerin saklı kanıtı yeniden çözülmez, kalem okunur. |
| **Kim** | Deploy operatörü |

**Kontrol:** Platform deploy edildikten sonra MDM'de IU'lu bir kalem (ör. GRM → IU açık çevrim) oluşturulabilmeli; 422
`ITEM_REFERENCE_VALUE_INVALID … item-uom:IU` alınıyorsa Platform henüz V2'de değildir.

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

### 7 · BL-592 — Stok ekibinin ürün okuma sözleşmesi: Platform güvenilir kapsam çiftine `mdm.product-master.read` (Platform KODU önce, yapılandırma sonra; Auth, manifestten ÖNCE)

| | |
|---|---|
| **Modül** | MOD-0290-FU04 S4 okuma sözleşmesi (`/api/product-master/*`, MDM) → Platform güvenilir tüzel kişi kapsamı; Auth ürün modülü hak profili (WP-MDM-PERMS-01) |
| **Ne zaman** | BL-592'yi taşıyan deploy'da; MDM'nin Enforced kapsam yayılımı açık her ortamda |
| **Sıra** | **1.** Platform KODU (BL-592: izin listesinde anahtar var, tavan 38). **2.** Platform yapılandırması: `TrustedLegalEntityScopeCredential:Mdm:AllowedPairs` listesine bir çift, `ModuleCode = product-item-sku-master`, `PermissionKey = mdm.product-master.read` (38 çift). **3.** Auth BL-592, kalem / `mdm.product-master.*` anahtarlarını **İLK beyan eden** MDM manifestinden ÖNCE. **4.** O MDM manifesti (S4 / S5 / S2 ve stock-gate hatta birleştikten sonraki küçük MDM adımı). |
| **Ne** | Yapılandırma depoda değil, her ortam kendisi verir. Sır değişmez, yalnız çift eklenir. |
| **Yapılmazsa / ters sırada** | (a) Çift hiç eklenmezse: Enforced kiracıda okuma sözleşmesinin her ürün satırı 403 `LEGAL_ENTITY_SCOPE_FORBIDDEN` (kapalı başarısızlık, sızıntı yok). Kalem satırları etkilenmez (kalem uçları kapsam sormaz). (b) **38 çiftli yapılandırma BL-592 ÖNCESİ Platform koduna verilirse** (tavan 37, anahtar listede yok): yapılandırma geçersiz sayılır, MDM kimlik bilgisi **tamamen** reddedilir, MDM'nin **her** güvenilir kapsam sorusu 403 — (a)'dan çok daha geniş. (c) **MDM manifesti Auth BL-592'den önce** anahtarları beyan ederse: eski Auth'ta bu iki anahtar düz "read" eylemidir; varsayılan Viewer tabanı (`RoleProvisioningService` / `DataSeeder`, yalnız ekler) `mdm.items.read`'i ve `mdm.product-master.read`'i **ürün modülü hakkı olmayan kiracılar dahil HER kiracının** Viewer'ına verir ve sonra hiçbir şey geri almaz (System hakkı eşitlemeyle silinmez). |
| **Geri alma** | Ters sırada: **önce** yapılandırma 37 çifte (`mdm.product-master.read` çifti çıkarılır), **sonra** Platform kodu. Kod önce geri alınırsa 38 çiftli yapılandırma (b)'deki gibi bütün kimlik bilgisini düşürür. Auth geri alınacaksa önce manifest adımı geri alınmalı; geri alınmış Auth'ta beyan edilmiş anahtar (c)'deki kalıcı Viewer hakkını doğurur. |
| **Kim** | Deploy operatörü (Platform kodu + yapılandırması, Auth) · MDM sahibi (manifest adımı) |

**Kontrol:**
- Liste 38'i aşmamalı ve her çift bir kez geçmeli; aksi hâlde Platform MDM kimlik bilgisini reddeder (bütün güvenilir
  kapsam soruları 403).
- Yeni çiftten sonra Enforced bir kiracıda `/api/product-master/…` bir ürün satırı döndürmeli. **Bu kontrol, MDM manifest
  adımı (4) anahtarı rollere taşıyana kadar GEÇEMEZ:** o adıma kadar hiçbir rol `mdm.product-master.read` tutmaz, çağrı
  yetkiyle düşer. Adım 2'den sonra bu kontrolün kırmızı olması tek başına hata değildir; adım 4'ten sonra kırmızıysa hatadır.
- Adım 3'ten sonra, adım 4'ten önce: ürün modülü hakkı OLMAYAN bir kiracının Viewer'ı `mdm.items.read` tutmamalı.

**Ön koşul (BL-592, CT kararı A):** anahtarlar Auth kataloğuna ve rollere yalnız manifest → Platform kataloğu → Auth
eşitlemesi yoluyla girer. Ürün modülü hakkıyla: Admin her beyan edilen kalem anahtarını (altı; S2 birleşince
`mdm.items.withdraw` ile yedi) ve `mdm.product-master.read`'i, Viewer yalnız `mdm.items.read` ve `mdm.product-master.read`'i
alır; hak yoksa kimse almaz. Manifest adımı yapılmadan stok ekibine "hazır" denmez.

---

## Tamamlananlar

*(henüz yok)*
