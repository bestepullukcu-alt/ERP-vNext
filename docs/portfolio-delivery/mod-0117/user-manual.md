# MOD-0117 Phase 2A PPM Kullanıcı Kılavuzu

> **İnceleme durumu — 29 Temmuz 2026:** Bu belge, hazırlanan Phase 2A ekranlarının hedef kullanımını
> açıklar. Sayfalar henüz uçtan uca kullanıcı incelemesine hazır değildir: MOD-0018 yetkilendirme
> entegrasyonu güvenli biçimde tüm istekleri reddetmektedir ve Gateway rota/runtime kanıtı
> tamamlanmamıştır. Bu nedenle çalışan bir inceleme adresi veya çalışan CRUD iddiası sunulmaz.

## Amaç ve kapsam

PPM çalışma alanı; portföy, girişim, program ve projeleri aynı tenant sınırı içinde kaydetmek ve yaşam
döngülerini izlemek içindir. Phase 2A kapsamındaki yüzeyler şunlardır:

- PPM ana sayfası
- Portföyler
- Girişimler
- Programlar
- Projeler

Yatırım kararları, fayda/değer bağlantıları, DWS entegrasyonu, görevler, iş akışları ve onaylar bu
sürümün kapsamında değildir.

## Liste ekranları

Dört kayıt ekranı aynı çalışma düzenini kullanır:

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

## Mevcut teslimat sınırı

Bu kılavuz ürün davranışını tarif eder; erişime hazır ortam ilanı değildir. MOD-0018 ortak
yetkilendirme entegrasyonu ve Gateway rota/runtime kanıtı tamamlanıp gerçek tarayıcı testi yapılmadan
sayfalar uçtan uca incelenebilir kabul edilmez. ExternalContextReference sağlayıcısı, DWS bağlantısı,
Phase 2B özellikleri, toplu işlemler ve onay davranışları ayrıca yetkilendirilmedikçe kullanılamaz.
