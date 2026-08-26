# MOD-0117 Phase 2A PPM Kullanıcı Kılavuzu

> **İnceleme durumu — 26 Ağustos 2026:** Phase 2A ile PPM-owned Gate L backend/frontend dilimleri ve
> Gateway eşlemesi bu izole branch'te uygulanmıştır. Geçici tam zincir üzerinden gerçek CRUD tarayıcı kanıtı
> ve ayrı PSS entegrasyon kanıtı vardır. Ancak üretim PPM grant/credential provisioning, normal `5000/5001`
> composition/activation ve kullanıcı parity kabulü tamamlanmamıştır. Bu nedenle belge bir üretim URL'si,
> production readiness veya uçtan uca tam 1.3 kabulü ilan etmez.

## Amaç ve kapsam

PPM çalışma alanı; portföy, girişim, program ve projeleri aynı tenant sınırı içinde kaydetmek ve yaşam
döngülerini izlemek içindir. Phase 2A kapsamındaki yüzeyler şunlardır:

- PPM ana sayfası
- Portföyler
- Girişimler
- Programlar
- Projeler
- Yatırım Vakaları (`InvestmentCase`)
- Fayda Taahhütleri (`BenefitCommitment`)

Gate L yalnız PPM-owned yatırım planlama ve planlı fayda kayıtlarını kapsar. MOD-0007 karar,
MOD-0023 onay sonucu, MOD-0136 bütçe, MOD-0138 senaryo ve MOD-0072 gerçekleşen sonuç bağlantıları ile DWS,
görev, iş akışı ve onay davranışları henüz kullanıcıya açılmış değildir.

## Liste ekranları

Altı kayıt ekranı aynı çalışma düzenini kullanır:

1. **Ara** alanıyla görünen kayıtları kod veya ad üzerinden daraltın.
2. **Filtre** seçeneğinden Yaşam Döngüsü ve Referanslanabilirlik ölçütlerini seçip **Uygula**'ya basın.
   **Sıfırla**, aramayı, filtreyi, sıralamayı ve sütun görünürlüğünü başlangıç durumuna getirir.
3. Sütun başlığına basarak sıralayın; sütun görünürlüğü aracından gerekli sütunları gösterin veya
   gizleyin.
4. **Dışa Aktar** menüsünden yalnız iş alanlarını dışa aktarın. Satır seçimi ve İşlemler sütunu
   dışa aktarılmaz.
5. Arama, filtre, sıralama veya sütun düzenini değiştirdikten sonra görünen **Görünümü Kaydet**
   seçeneğiyle kişisel varsayılan görünümünüzü saklayın.

[İmaj: PPM liste araç çubuğu, filtreler ve yaşam döngüsü rozetleri]

Phase 2A toplu silme veya toplu yaşam döngüsü işlemi sunmaz. Satır seçim kutuları böyle bir yetki
vermez. Toplu onay veya herhangi bir **Onayla** işlemi de yoktur.

## Kayıt oluşturma ve düzenleme

1. İlgili listeyi açın ve **Yeni Ekle** seçeneğini kullanın.
2. Sağdan açılan panelde yıldızlı alanları doldurun.
3. **Kaydet**'e basın. Düzenlemek için satırın İşlemler menüsünden **Düzenle**'yi seçin.

| Kayıt | Zorunlu alanlar | İsteğe bağlı alanlar | Başlangıç durumu |
|---|---|---|---|
| Portföy | Kod, Ad | Açıklama | Taslak |
| Girişim | Kod, Ad | Açıklama, Portföy | Önerildi |
| Program | Kod, Ad | Açıklama, Portföy | Taslak |
| Proje | Kod, Ad, Üst Tür, Üst Kayıt | Açıklama | Taslak |
| Yatırım Vakası | Kod, Başlık, Portföy | Açıklama, Planlanan Başlangıç/Bitiş | Taslak |
| Fayda Taahhüdü | Kod, Başlık, Yatırım Vakası, Hedef Açıklaması | Açıklama, Hedef Tarih | Taslak |

Görünürlük politikası alanı, merkezi yetkilendirme kaynağı tamamlanıncaya kadar devre dışıdır.
Yaşam döngüsü oluşturma/düzenleme formundan serbestçe değiştirilemez; yalnız izin verilen satır işlemleri
üzerinden ilerletilir.

### Proje üst kaydı

Bir proje tam olarak bir girişime veya programa bağlanır:

1. Önce **Üst Tür** alanından Girişim ya da Program seçin.
2. Ardından **Üst Kayıt** listesinden erişebildiğiniz ve yeni referanslara açık kaydı seçin.

Başka tenant'a ait, silinmiş, görünmeyen veya yeni referanslara kapalı kayıtlar seçilemez. Girişim ve
program için portföy bağlantısı isteğe bağlıdır.

## Detaylar ve göstergeler

Satırın İşlemler menüsündeki **Detayları Görüntüle**, sayfadan ayrılmadan hızlı detay panelini açar.
Panelde kod, ad, açıklama, üst kayıt, yaşam döngüsü ve referanslanabilirlik görülür.

- **Yeni referanslara açık:** Kayıt, mevcut yaşam döngüsü ve görünürlük koşullarında yeni bir yapısal
  bağlantıda kullanılabilir.
- **Yeni referanslara kapalı:** Yeni bağlantı kurulamaz. Bu gösterge kullanıcı tarafından değiştirilen
  bir alan değildir.

Portföylerde Taslak/Aktif; girişimlerde Önerildi/Aktif/Beklemede; programlarda
Taslak/Aktif/Beklemede; projelerde Taslak/Planlandı/Aktif/Beklemede durumları yeni referansa açıktır.

Yatırım Vakası yaşam döngüsü `Taslak → Analiz Altında → Kapalı` veya `Taslak|Analiz Altında → Geri
Çekildi`; Fayda Taahhüdü yaşam döngüsü `Taslak → Planlandı → Aktif → Kapalı` veya aktif yol üzerindeki
durumlardan `İptal Edildi` şeklindedir. Bu durumlar onay, fonlama veya gerçekleşmiş fayda anlamına gelmez.

## Yaşam döngüsü ve kaldırma işlemleri

Satırın İşlemler menüsü yalnız o kaydın mevcut durumundan izin verilen sonraki adımları gösterir.
Arşivleme, tamamlama ve iptal işlemlerinde onay penceresi açılır; devam etmeden önce kayıt ve hedef
durumu kontrol edin.

**Sil** işlemi de onay ister ve kaydı listeden kaldırır. Bu işlem geri alınabilir veri saklama
yaklaşımını kullanır; ancak kullanıcı arayüzünde doğrudan geri yükleme işlemi Phase 2A kapsamında
değildir.

## Karşılaşılabilecek durumlar

| Durum | Anlamı | Ne yapmalısınız? |
|---|---|---|
| 403 | Bu işlemi yapma yetkiniz yoktur. | Doğru tenant hesabıyla giriş yaptığınızı kontrol edin; yetki için yöneticinize başvurun. |
| 404 | Kayıt bulunamadı veya size görünür değildir. | Bağlantıyı ve seçili tenant'ı kontrol edin. Başka tenant'taki kaydın varlığı açıklanmaz. |
| 409 | Kayıt siz açtıktan sonra değişmiş veya yaşam döngüsü geçişi geçersizdir. | Listeyi yenileyin, güncel durumu inceleyin ve işlemi yeniden değerlendirin. |
| 503 | Gerekli güvenli hizmet geçici olarak kullanılamıyor. | İşlemin başarılı olduğunu varsaymayın; daha sonra yeniden deneyin veya destek ekibine bildirin. |

Geçersiz veya eksik alanlar form üzerinde gösterilir. Hata görüldüğünde sahte başarı ya da yerel örnek
veri gösterilmez.

## Dahili ExternalContextReference sağlayıcısı

`ppm.external-context-reference` `1.0` sağlayıcısı bu branch'te uygulanmıştır. Yalnız dahili servis
tüketicisidir; browser veya Gateway yüzeyi değildir ve varsayılan olarak kapalıdır. Production credential,
activation ve MOD-0354/DWS tüketimi ayrıca yetkilendirilip kanıtlanmadan kullanıcı davranışı sayılmaz.

## Eski ve yeni ekranların karşılaştırılması

Eski Enterprise Strategy / Delivery Execution ekranları bu teslimatla silinmez veya emekliye ayrılmaz.
Eski URL, entity, alan, komut, yaşam döngüsü, permission ve entegrasyonların yeni `/ppm/**` karşılıklarına
sayfa sayfa bağlandığı parity matrisi; farklı portlarda yan yana browser kanıtı ve kullanıcı kabulü henüz
tamamlanmamıştır. Bu kapılar kapanmadan “full parity complete” veya legacy retirement kararı verilemez.

## Mevcut teslimat sınırı

Bu kılavuz ürün davranışını tarif eder; erişime hazır production ortam ilanı değildir. Phase 2A ve PPM-owned
Gate L uygulaması ile geçici tarayıcı kanıtı mevcuttur. Buna karşılık Gate I-A Decision Trace, Gate I-B
Funding & Scenario ve Gate I-C Benefit Realization adapterları NON-EXECUTABLE durumdadır. Üç lane için ayrı
MOD-0117 consumer amendment/runtime yetkisi, ardından tek composition branch, gerçek grant/credential ve
normal-port activation, cross-service güvenlik/uyumluluk, entegre browser ve legacy parity kabulü gerekir.
Toplu işlemler, DWS tüketimi ve onay davranışları ayrıca yetkilendirilmedikçe kullanılamaz.
