# View Organizasyon Kuralları

Projeyi daha modüler hale getirmek ve klasör yapısını düzenli tutmak için Views klasörü altındaki sayfalar belirli kurallara göre gruplanmalıdır.

## 1. Modül Tabanlı Gruplama
- `Views` klasörü altında her sayfa, doğrudan dizin köküne konulmak yerine **bağlı olduğu modül veya domain adına göre** gruplanmalıdır.
- Örnek Klasörleme:
  - `Views/MDM/` (Master Data Management için)
  - `Views/Identity/` (Kullanıcı, rol, yetkilendirme vb. için)
  - `Views/PPM/` (Project Portfolio Management için)
  - `Views/Finance/` vb.

## 2. Yeni Sayfa Üretimi
- Yeni bir sayfa veya view üretilmeden önce kullanıcıya **hangi modül klasörüne konulacağı sorulmalı** veya bağlamdan doğru klasör **çıkarım yapılmalı** ve uygulanmalıdır.
- Kesinlikle `Views/` root klasörüne doğrudan yeni sayfa oluşturulmamalıdır.

## 3. Mevcut Karmaşık Sayfalar
- Projede halihazırda bulunan ve belirli bir modüle uymayan veya generic olan karmaşık sayfalar, referans olarak `Views/Other` (veya uygun benzer bir genel klasör) mantığıyla ele alınmalı ve oraya taşınmalıdır.

## 4. Layout Atama Kuralı (Dual-Layout)
- **Yeni MDM ve modern sayfalar:** `Layout = "_LayoutBackbone"` kullanır.
- **Archive sayfaları:** `_Layout`'u kullanmaya devam eder (dokunulmaz).
- `_ViewStart.cshtml` dosyası **değiştirilmez** — Default `_Layout` olarak kalır.
- Yeni bir sayfa oluşturulduğunda Razor bloğuna `Layout = "_LayoutBackbone";` satırı eklenir.

## 5. Skeleton Loader Kullanımı
- DataTable içeren her yeni liste sayfasında `@await Html.PartialAsync("_SkeletonLoader")` çağrılır (veya manual manual `#skeleton-loader` bloğu eklenir).
- Skeleton, `card-datatable` div'inin **içine** yerleştirilir.
- **Overlay Kuralı:** Skeleton `position: absolute` olmalı ve tablonun Toolbar'ını (Search vb.) örtmemesi için üstten boşluk bırakmalıdır (`top: 72px`).
- Parent `card-datatable` div'e `style="position:relative; min-height:200px;"` eklenir. Bu hem shimmer alanı yaratır hem de düzen kaymasını (CLS) engeller.
