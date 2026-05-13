# GOLDEN RULE: Premium Alert & Modal Standard (MOD-0013)

Diten ERP vNext projesinde varsayılan (default) tarayıcı uyarıları (`alert`) veya standart, özelleştirilmemiş SweetAlert2 diyalogları kullanmak KESİNLİKLE YASAKTIR. 

Tüm hata, onay, başarı ve bilgilendirme modalleri projemizin lüks ve premium Sneat estetiği ile bütünleşik olmak zorundadır.

---

## 1. Tasarım Temelleri

Premium modal standardı (MOD-0013), aşağıdaki görsel ve yapısal standartları şart koşar:

*   **Kenarlık Yumuşaklığı (Rounding):** Tüm popuplar `rounded-4` (büyük yuvarlak kenarlı) ve `shadow-lg` (yoğun gölgeli) olmalıdır.
*   **İç Boşluklar (Padding):** İçeriğin sıkışmaması için popup padding değeri sabit `2.5rem 1.5rem 2rem` olarak ayarlanmalıdır.
*   **Özel Halka İkonlar (Custom Circle Icons):** SweetAlert2'nin varsayılan ikon animasyonları kapatılmalı; yerine hafif şeffaf arka planlı dairesel premium ikon hazneleri (`swal-icon-circle`) kullanılmalıdır.
*   **Standart Tipografi ve Butonlar:** Onay ve iptal butonları Sneat Bootstrap 5 sınıflarını (`btn btn-primary waves-effect waves-light px-5`) kullanmalıdır. `buttonsStyling: false` zorunludur.

---

## 2. İkon Renk ve HTML Standartları

Her mesaj tipi için kullanılacak dairesel ikon sarmalayıcı yapıları ve inline stilleri aşağıda tanımlanmıştır:

### A. Hata İkonu (Error)
Hafif şeffaf kırmızı arka plan, kırmızı ince kenarlık ve kırmızı hata ünlemi ikonu.
```html
<div class="swal-icon-circle bg-label-danger border-danger border-opacity-25" style="display: flex; align-items: center; justify-content: center; width: 80px; height: 80px; border-radius: 50%; background: rgba(255, 76, 81, 0.12); border: 2px solid rgba(255, 76, 81, 0.25); margin: 0 auto !important;">
    <i class="bx bx-error-circle text-danger" style="font-size: 2.5rem; color: #ff4c51;"></i>
</div>
```

### B. Başarı İkonu (Success)
Hafif şeffaf yeşil arka plan, yeşil ince kenarlık ve yeşil tik ikonu.
```html
<div class="swal-icon-circle bg-label-success border-success border-opacity-25" style="display: flex; align-items: center; justify-content: center; width: 80px; height: 80px; border-radius: 50%; background: rgba(113, 221, 55, 0.12); border: 2px solid rgba(113, 221, 55, 0.25); margin: 0 auto !important;">
    <i class="bx bx-check-circle text-success" style="font-size: 2.5rem; color: #71dd37;"></i>
</div>
```

### C. Onay / Yardım İkonu (Confirm / Question)
Hafif şeffaf mavi arka plan, mavi ince kenarlık ve mavi soru işareti ikonu.
```html
<div class="swal-icon-circle bg-label-primary border-primary border-opacity-25" style="display: flex; align-items: center; justify-content: center; width: 80px; height: 80px; border-radius: 50%; background: rgba(105, 108, 255, 0.12); border: 2px solid rgba(105, 108, 255, 0.25); margin: 0 auto !important;">
    <i class="bx bx-help-circle text-primary" style="font-size: 2.5rem; color: #696cff;"></i>
</div>
```

### D. Uyarı İkonu (Warning)
Hafif şeffaf turuncu arka plan, turuncu ince kenarlık ve turuncu ünlem ikonu.
```html
<div class="swal-icon-circle bg-label-warning border-warning border-opacity-25" style="display: flex; align-items: center; justify-content: center; width: 80px; height: 80px; border-radius: 50%; background: rgba(255, 171, 0, 0.12); border: 2px solid rgba(255, 171, 0, 0.25); margin: 0 auto !important;">
    <i class="bx bx-warning text-warning" style="font-size: 2.5rem; color: #ffab00;"></i>
</div>
```

---

## 3. JavaScript Uygulama Şablonu (Boilerplate)

Aşağıdaki şablon, SweetAlert2 üzerinde premium stil kurallarını tam olarak uygulamak için referans alınmalıdır:

```javascript
Swal.fire({
    title: 'Hata', // Veya dinamik yerelleştirilmiş başlık
    html: `<div class="mb-1 text-muted">${message}</div>`,
    iconHtml: '...[İlgili İkon HTML Kodu]...',
    confirmButtonText: 'Tamam',
    width: '400px',
    padding: '2.5rem 1.5rem 2rem',
    customClass: {
        popup: 'rounded-4 shadow-lg',
        title: 'fs-4 fw-bold text-heading mt-4 mb-2 d-block w-100 text-center',
        htmlContainer: 'mb-1 d-block w-100 text-center',
        actions: 'd-flex justify-content-center mt-4 w-100 gap-2',
        confirmButton: 'btn btn-primary waves-effect waves-light px-5',
        cancelButton: 'btn btn-label-secondary waves-effect px-5',
        icon: 'border-0 m-0 p-0 d-flex justify-content-center w-100'
    },
    buttonsStyling: false,
    scrollbarPadding: false,
    heightAuto: false,
    reverseButtons: true
});
```

---

## 4. Küresel Entegrasyonlar ve Yardımcılar

*   **Düzenli Sayfalar (Layout Kullananlar):** 
    Sistem genelinde `_GlobalConfirmation.cshtml` dosyası otomatik olarak yüklüdür. Onay pencereleri için her zaman `window.showConfirm(keyOrTitle, callback, options)` fonksiyonu çağrılmalıdır. Manuel SweetAlert çağrısı yapılmamalıdır.
*   **Bağımsız Sayfalar (Layout Kullanmayan Giriş/Kayıt vb.):**
    `Layout = null` olan veya SweetAlert default stillerini override edemeyen sayfalarda yukarıda Bölüm 3'te verilen `customClass` sarmalayıcı nesnesi ve Bölüm 2'deki inline stil kurallı ikon şablonları **birebir** el ile yazılmalıdır.
