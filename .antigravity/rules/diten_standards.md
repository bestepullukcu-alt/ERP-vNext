# Diten Operasyon Standartları

Bu kural seti, Diten ERP vNext projelerindeki tüm geliştirme süreçlerini bağlar.

## 1. Planlama Zorunluluğu (Stop & Plan)
Herhangi bir kod yazmadan veya dosya oluşturmadan önce kapsamlı bir **Analiz ve Uygulama Planı** sunulmalıdır.
Kullanıcıdan **'ONAY' (APPROVED)** alınmadan tek bir satır kod yazılması kesinlikle yasaktır.

## 2. Klasörleme Standardı
- **JS Dosyaları:** `wwwroot/js/MDM/{ModulAdi}/{dosya}.js` (veya projenin mevcut `wwwroot/assets/js/MDM/` yapısına uyarlanmış hali)
- **View Dosyaları:** `Views/{ModulAdi}/` altında toplanmalıdır.
- **Düzen**: Başıboş dosya bulunmamalı, her modül kendi klasöründe olmalıdır.

## 3. UI Altın Standart (Legal Entities)
- **Kıble:** `LegalEntities` sayfasıdır.
- **Gereksinimler:**
    - Buton yerleşimi
    - DataTable v2 konfigürasyonu
    - CSS sınıfları
    - Kart tasarımları
- Tüm yeni sayfalar `LegalEntities` ile %100 görsel ikiz olmalıdır.

## 4. Sidebar (Sol Menü) Kuralı
- Yeni sayfa eklerken `_LayoutBackbone.cshtml` dosyasındaki menü öğesi dinamik (`ViewBag.ActiveMenu` kontrollü) olmalıdır.
- İlgili Controller'ın `Index` metoduna `ViewBag.ActiveMenu` değeri atanmalıdır.
