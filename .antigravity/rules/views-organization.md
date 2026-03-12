---
description: "VIEW-001 — Diten.Web View Organizasyonu, Modüler Gruplama ve Layout Yönetim Standartları"
---

# View Organizasyon Kuralları (Diten ERP vNext)

Bu doküman, Diten.Web projesindeki klasör hiyerarşisini düzenlemek, yeni sayfaların doğru layout ile açılmasını sağlamak ve yükleme ekranı (UX) standartlarını belirlemek için oluşturulmuştur.

## 📁 1. Modül Tabanlı Gruplama

Projeyi modüler ve ölçeklenebilir tutmak için Views klasörü altında rastgele dosya oluşturulamaz. Her sayfa bağlı olduğu ana modüle göre gruplanmalıdır.

> ⚠️ **KRİTİK — `Areas/` Klasörü KULLANILMAZ!**
> Proje ASP.NET Core Areas routing KULLANMAZ. Tüm view'lar `Views/` klasörü altında modül gruplarına göre düzenlenir.
> `Areas/` klasörü ASP.NET'in özel bir routing özelliğidir ve Controller'da `[Area]` attribute gerektirir — bizde bu yapı YOKTUR.
>
> ❌ **YANLIŞ:** `Areas/MDM/Views/Countries/Index.cshtml`
> ✅ **DOĞRU:** `Views/MDM/Countries/Index.cshtml`
>
> ❌ **YANLIŞ Namespace:** `Diten.Web.Areas.MDM.Views.Countries`
> ✅ **DOĞRU Namespace:** `Diten.Web.Views.MDM.Countries`

- KURAL: Yeni bir View oluşturulmadan önce mutlaka bağlam kontrol edilmeli veya kullanıcıya modül sorulmalıdır.
- Standart Klasör Yapısı:
  - Views/MDM/ (Master Data Management - Altın Referans Katmanı)
  - Views/Identity/ (Kullanıcı, Rol ve Yetki Yönetimi)
  - Views/PPM/ (Project Portfolio Management)
  - Views/Other/ (Genel sayfalar için referans alanı)

---

## 🖼️ 2. Layout ve ViewStart Yönetimi (Dual-Layout)

Sistemde iki farklı dünya (Eski Archive ve Yeni vNext) aynı anda yaşamaktadır.

- Archive Sayfaları: _Layout.cshtml kullanır ve dokunulmazdır (Frozen).
- Yeni Modern Sayfalar: Mutlaka _LayoutBackbone.cshtml kullanmalıdır.
- Uygulama: _ViewStart.cshtml dosyasının varsayılan ayarı değiştirilmez. Yeni oluşturulan her modern Razor sayfasının en üstüne şu blok eklenmelidir:
  @{ Layout = "_LayoutBackbone"; }

---

## 💀 3. Skeleton Loader ve UX Standartları

Kullanıcının veri yüklenirken boş bir ekran görmesini engellemek için Skeleton Loader kullanımı zorunludur.

- Yerleşim: DataTable içeren her liste sayfasında `.card` içinde `#skeleton-loader` bloğu bulunmalıdır (tablodan önce).
- Davranış: `window.DtDefaults.create()` wrapper'ı AJAX başlangıcında skeleton'ı gösterir (`preXhr`) ve her draw sonunda otomatik kapatır (`drawCallback`).
- Kural: ID mutlaka `skeleton-loader` olmalıdır. Sayfa özelinde ekstra show/hide JS yazmak ancak özel UX ihtiyacı varsa kabul edilir.

---

## 🚨 Önemli Notlar
- Views root klasörüne doğrudan .cshtml dosyası eklemek kesinlikle yasaktır.
- Yeni modüller oluşturulurken klasör isimleri her zaman PascalCase olmalıdır (Örn: Finance, HumanResources).
- Her modül klasörü kendi içinde sayfa bazlı alt klasörlere (Örn: Views/MDM/LegalEntities/Index.cshtml) sahip olabilir.

---

## ✅ Kontrol Listesi
- [ ] Sayfa doğru modül klasörü (MDM, Identity vb.) altında mı?
- [ ] Razor bloğunda Layout = "_LayoutBackbone" tanımlandı mı?
- [ ] _ViewStart dosyasına dokunulmadı mı?
- [ ] Liste sayfasında #skeleton-loader yapısı kuruldu mu?
- [ ] Sayfa özelinde skeleton show/hide hack'i olmadan DtDefaults ile kapanıyor mu?

---
Diten ERP vNext View Organization Standard - VIEW-001
