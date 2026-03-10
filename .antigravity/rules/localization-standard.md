# GOLDEN RULE: Strict Localization (L10n) Standard

Diten ERP vNext projesinde "Hardcoded" (elle yazılmış) metin kullanmak KESİNLİKLE YASAKTIR. Yeni bir modül veya sayfa eklendiğinde View dosyası oluşturulmadan ÖNCE aşağıdaki yerelleştirme adımları ZORUNLU olarak uygulanacaktır:

## 1. Dil Dosyalarının Eksiksiz Oluşturulması
Resmi desteklenen 8 dilin TAMAMI için `.resx` dosyaları oluşturulmalıdır. Sadece Türkçe (`tr`) oluşturup bırakmak kural ihlalidir.
Desteklenen Diller: `en, es, ka, kk, ru, tr, uk, uz`

**Dosya Yolu Standardı:** `Resources/Views/{AreaName}/{ModuleName}/Index.{lang}.resx`
*(Örn: `Resources/Views/MDM/Countries/Index.en.resx`, `Resources/Views/MDM/Countries/Index.ru.resx` vb.)*
**Kritik Kural:** Kaynak dosyası, hedeflediği `.cshtml` dosyasının adıyla birebir eşleşmelidir. Listeleme sayfaları için bu daima `Index`'tir. Klasörleme karmaşayı önlemek için ZORUNLUDUR.

## 2. Zorunlu Anahtarlar (Keys)
Her modülün dil dosyasında ŞART olan standart anahtarlar:
- `[ModuleName]Title` (Örn: CitiesTitle -> Şehirler / Cities / Города)
- `PageDescription` (Örn: Bu ekrandan şehirleri yönetebilirsiniz.)
- `AddNew[ModuleName]` (Örn: AddNewCity -> Yeni Şehir Ekle)
- Tablodaki (`<thead>`) modüle özel sütun başlıkları (Örn: CityName, PlateCode)

## 3. SharedResource vs. ViewResource Ayrımı (ÇOK ÖNEMLİ)
Dil verileri (çeviriler) kullanım sıklığına göre KESİN bir ayrıma tabidir:

**A. SharedResource (Tekrar Eden / Ortak Metinler):**
Birden fazla sayfada kullanılan genel ifadeler ASLA View (sayfa) dil dosyalarına eklenmez. Bunlar her zaman `SharedResource.{lang}.resx` dosyasından okunmalıdır.
*Örnekler:* "Save" (Kaydet), "Delete" (Sil), "Cancel" (İptal), "Yes/No", "Active/Passive", "Are you sure?", "Actions", DataTable standart metinleri ("No records found" vb.).

**B. View-Specific Resource (Sayfaya Özel Metinler):**
SADECE o sayfaya ve modüle ait olan, başka sayfalarda kullanılmayacak metinler `Resources/Views/{AreaName}/{ModuleName}.{lang}.resx` dosyalarına eklenmelidir.
*Örnekler:* Sayfa başlığı (`CountriesTitle`), Sayfa açıklaması (`CountriesDescription`), Modüle özel tablo sütun isimleri (`IsoCode`, `TaxNumber`), Modüle özel mesajlar (`CountryAddedSuccessfully`).