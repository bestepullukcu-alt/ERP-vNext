# GOLDEN RULE: Strict Localization (L10n) Standard

Diten ERP vNext projesinde "Hardcoded" (elle yazılmış) metin kullanmak KESİNLİKLE YASAKTIR. Yeni bir modül veya sayfa eklendiğinde View dosyası oluşturulmadan ÖNCE aşağıdaki yerelleştirme adımları ZORUNLU olarak uygulanacaktır:

## 1. Dil Dosyalarının Eksiksiz Oluşturulması
Resmi desteklenen 9 dilin TAMAMI için `.resx` dosyaları oluşturulmalıdır. Sadece Türkçe (`tr`) oluşturup bırakmak kural ihlalidir.
Desteklenen Diller: `az, en, es, ka, kk, ru, tr, uk, uz`

> ⛔ **Placeholder YASAK:** Non-English (`az/es/ru/uk/ka/kk/uz/tr`) `.resx` dosyalarında değerlerin İngilizce (en) ile aynı bırakılması kabul edilmez.
> - Örn: `SaveView` → `es/ru/uk/...` dosyalarında `Save View` olarak bırakmak kural ihlalidir.
> - Çeviri belirsizse, geliştirme durdurulur ve kullanıcıdan doğru kurumsal metin istenir.

> ✅ **Casing Standardı (Tutarlılık):** UI aksiyon butonları için kullanılan SharedResource key'lerinde (örn: `SaveView`) değerler **tutarlı bir casing** ile yazılmalıdır.
> - Varsayılan: **Title Case** (kelimelerin ilk harfi büyük) — `Save View`, `Görünümü Kaydet`, `Guardar Vista`, `Сохранить Вид` vb.
> - Casing olmayan alfabelerde (örn: Gürcüce `ka`) bu kontrol uygulanmaz.
> - Aynı key için bazı dillerde cümle düzeni/harf yapısı farklı bırakılmaz; teslim öncesi kontrol zorunludur.

**Dosya Yolu Standardı:** `Resources/Views/{AreaName}/{ModuleName}/{MarkerClassName}.{lang}.resx`
*(Örn: `Resources/Views/MDM/Countries/CountriesIndex.en.resx`, `Resources/Views/MDM/LegalEntities/LegalEntitiesIndex.tr.resx` vb.)*
**Kritik Kural:** Kaynak dosyası, Razor view'daki `IHtmlLocalizer<T>` sınıf adıyla birebir eşleşmelidir. Marker class convention: `{ModuleName}Index` (bkz: `frontend-datatable-template.md`). `Index.{lang}.resx` adı KULLANILMAZ.

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

---

## 4. DataTable Toolbar / Filter Vocabulary (ZORUNLU)

DataTable liste sayfalarında toolbar ve filter UI için kullanılan temel kelimeler **SharedResource** üzerinden gelmelidir.

**En az zorunlu anahtarlar (SharedResource):**
- `Search`, `Export`, `Import`, `Filter`, `Apply`, `Reset`, `ShowAll`, `SaveView`, `ColumnVisibility`, `Status`, `Cancel`, `AreYouSure`

**Kritik kural (fallback yasak):**
- Toolbar/action metinlerinde “hardcoded fallback” (`|| 'Export'`) yaklaşımı **yasaktır**.
- Eksik L10n key varsa teslim durur; key 9 dilde tamamlanmadan feature bitmiş sayılmaz.
