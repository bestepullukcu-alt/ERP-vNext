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
> ❌ **YANLIŞ:** `Areas/MDM/Views/SampleModule/Index.cshtml`
> ✅ **DOĞRU:** `Views/MDM/SampleModule/Index.cshtml`
>
> ❌ **YANLIŞ Namespace:** `Diten.Web.Areas.MDM.Views.SampleModule`
> ✅ **DOĞRU Namespace:** `Diten.Web.Views.MDM.SampleModule`

- KURAL: Yeni bir View oluşturulmadan önce mutlaka bağlam kontrol edilmeli veya kullanıcıya modül sorulmalıdır.
- Standart Klasör Yapısı:
  - Views/MDM/ (Master Data Management - Altın Referans Katmanı)
  - Views/Identity/ (Kullanıcı, Rol ve Yetki Yönetimi)
  - Views/PPM/ (Project Portfolio Management)
  - Views/Other/ (Genel sayfalar için referans alanı)

---

## 🖼️ 2. Layout ve ViewStart Yönetimi (Shell-Aware Layout)

Sistemde eski Archive layout'u ve iki aktif vNext shell'i aynı anda yaşamaktadır.

- Archive Sayfaları: _Layout.cshtml kullanır ve dokunulmazdır (Frozen).
- Platform/admin modülleri: `Views/Platform/{Controller}/` altında olmalı ve `_LayoutPlatformAdmin.cshtml` kullanmalıdır.
- Tenant modülleri: `Views/{Controller}/` veya tenant domain klasörü altında olmalı ve `_LayoutTenantShell.cshtml` kullanmalıdır.
- Uygulama: `_ViewStart.cshtml` dosyasının varsayılan ayarı değiştirilmez. Yeni oluşturulan her Razor sayfasında shell tipine göre layout açıkça seçilir:
  - Admin: `@{ Layout = "_LayoutPlatformAdmin"; }`
  - Tenant: `@{ Layout = "_LayoutTenantShell"; }`

---

## 💀 3. Skeleton Loader ve UX Standartları

Kullanıcının veri yüklenirken boş bir ekran görmesini engellemek için Skeleton Loader kullanımı zorunludur.

- Yerleşim: DataTable içeren her liste sayfasında `.card` içinde `#skeleton-loader` bloğu bulunmalıdır (tablodan önce).
- Davranış: `window.DtDefaults.create()` wrapper'ı AJAX başlangıcında skeleton'ı gösterir (`preXhr`) ve her draw sonunda otomatik kapatır (`drawCallback`).
- Kural: ID mutlaka `skeleton-loader` olmalıdır. Sayfa özelinde ekstra show/hide JS yazmak ancak özel UX ihtiyacı varsa kabul edilir.

### 3.1 Kapsam: DataTable'a ÖZGÜ DEĞİL (ZORUNLU, 2026-09-04 ölçümüyle eklendi)

Yukarıdaki kural "DataTable içeren liste sayfası" diyor ve davranışı
`DtDefaults.create()`'in `preXhr`'ına bağlıyor. **Bu kapsam dardır ve boşluk
üretmiştir.**

⚠ ÖLÇÜLDÜ (2026-09-04): DataTable OLMAYAN veri sayfaları hiçbir yükleme
göstergesi taşımıyordu — `/Tasks/WorkReport` 18 boş kap çiziyor, kullanıcı
boş ekran görüyor; `/WorkCenterNext` yalnız çevrilmemiş "Loading" metni
gösteriyordu. İkisi de kural ihlali sayılmıyordu, çünkü DataTable değiller.
Aynı ölçümde projede **5 farklı yükleme deseni** ve **22 çevrilmemiş ham
"Loading" metni** bulundu.

**Genişletilmiş kural: VERİ BEKLEYEN HER SAYFA iskelet gösterir.**

| sayfa tipi | iskelet |
|---|---|
| DataTable listesi | satır iskeletleri (`#skeleton-loader`, yukarıdaki standart) |
| Kart / grafik sayfası | **kart iskeletleri** — kartlar yerinde, içleri gri blok; grafik kabı kendi yüksekliğinde gri dikdörtgen |
| Özel liste (DataTable değil) | **satır iskeletleri** — 5–6 sahte satır |

**İlke: iskelet, gelecek içeriğin ŞEKLİNDE olur.** Spinner "bir şey oluyor"
der; iskelet "ne geleceğini" gösterir ve içerik gelince sayfa ZIPLAMAZ.
Kart sayısı, yerleşim ve yükseklik baştan doğru olmalıdır.

**Üç yasak:**

1. **Boş kap gösterme.** "Yükleniyor", "veri yok" ve "hata/yetki yok" ÜÇ AYRI
   durumdur ve üçü farklı görünmelidir. Bugün üçü de boş ekran veriyorsa
   kullanıcı hangisinde olduğunu ayırt edemez.
2. **Yeni sayfada `spinner-border` kullanma.** İskelet varken spinner ikinci
   bir dildir. Mevcut 19 kullanım zamanla dönüştürülür.
3. **"Loading" / "Yükleniyor" METNİ yazma.** İskelet zaten bunu söylüyor, ve
   ölçüldü: bu metinlerin 22'si `Localizer` kullanmadan ham İngilizce yazılmış
   — tenant tarafında l10n ihlali.

**Yeniden kullan, icat etme:** `backbone-skeleton` sınıfı 81 sayfada zaten var.
İkinci bir iskelet dili açmak sorunu büyütür. Stil `backbone-custom.css`'te
tutulur (FG-003).

**Kabul ölçütü:** ağı yavaşlatarak (DevTools → Slow 3G) sayfayı aç. İskelet
görünmeli ve veri gelince yerleşim ZIPLAMAMALI.

---

## 🚨 Önemli Notlar
- Views root klasörüne doğrudan .cshtml dosyası eklemek kesinlikle yasaktır.
- Yeni modüller oluşturulurken klasör isimleri her zaman PascalCase olmalıdır (Örn: Finance, HumanResources).
- Her modül klasörü kendi içinde sayfa bazlı alt klasörlere (Örn: Views/MDM/SampleModule/Index.cshtml) sahip olabilir.

---

## ✅ Kontrol Listesi
- [ ] Sayfa doğru modül klasörü (MDM, Identity vb.) altında mı?
- [ ] Razor bloğunda shell tipine göre `Layout = "_LayoutPlatformAdmin"` veya `Layout = "_LayoutTenantShell"` tanımlandı mı?
- [ ] _ViewStart dosyasına dokunulmadı mı?
- [ ] Liste sayfasında #skeleton-loader yapısı kuruldu mu?
- [ ] Sayfa özelinde skeleton show/hide hack'i olmadan DtDefaults ile kapanıyor mu?

---
Diten ERP vNext View Organization Standard - VIEW-001
