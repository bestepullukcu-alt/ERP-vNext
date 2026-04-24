---
description: "[Canlıya Alım Öncesi Kontrol Listesi — Diten ERP vNext]"
---
# Workflow: Release Checklist (Canlıya Çıkış Kontrol Listesi)

Her yeni sürüm, modül veya kritik hata düzeltmesi (hotfix) yayına alınmadan önce aşağıdaki kontrollerden geçmek zorundadır. Bu liste, "Sıfır Hata" prensibimizin son kontrol noktasıdır.

---

## 🏗️ 1. Derleme ve Temel Sağlık (Build & Health)
- [ ] **Build:** Tüm servisler (`Api`, `Application`, `Persistence` vb.) hatasız derleniyor mu?
- [ ] **Health Check:** `/health` endpoint'i tüm servislerde "OK" dönüyor mu?
- [ ] **Ocelot Sync:** Yeni route tanımları Gateway (port 5000) üzerinde küçük harf (lowercase) kuralına uygun mu?

## 🛡️ 2. Güvenlik ve İzolasyon (Security)
- [ ] **Tenant Enforcement:** Tüm `POST/PUT/DELETE` işlemlerinde `X-Tenant-Id` zorunluluğu ve veri sızıntısı kontrolü yapıldı mı?
- [ ] **JWT Validation:** Geçersiz veya süresi dolmuş token ile erişim engelleniyor mu?
- [ ] **Secret Leak:** `.appsettings` veya kod içinde temizlenmemiş şifre, API key veya bağlantı cümlesi (connection string) var mı?
- [ ] **Authorize Attribute:** Yeni eklenen Controller'larda `[Authorize]` veya `[HasPermission]` unutuldu mu?



## 🌍 3. Yerelleştirme ve UI (L10n & Frontend)
- [ ] **7 Dil Senkronizasyonu:** Yeni eklenen tüm Key'ler 7 dildeki (`.en, .fr, .es, .zh, .ar, .ru, .tr`) `.resx` dosyalarına eklendi mi?
- [ ] **L10n Bridge:** JavaScript tarafındaki metinler `window.L10n` üzerinden mi okunuyor?
- [ ] **Skeleton Loader:** Liste ve detay sayfalarında yükleme animasyonu (UX) çalışıyor mu?
- [ ] **Sneat 2.x:** DataTable yerleşimleri yeni `layout` API'sine uygun mu?

## 📊 4. Operasyonel (Logging & DB)
- [ ] **Structured Logging:** Loglarda `TenantId` ve `CorrelationId` düzgün basılıyor mu?
- [ ] **Mongo Index:** Yeni koleksiyonlar için `TenantId` ile başlayan Compound Index'ler oluşturuldu mu?
- [ ] **Async Safety:** Tüm I/O işlemlerinde `CancellationToken` kullanımı kontrol edildi mi?

## 🖥️ 5. Browser Doğrulama (Smoke Test)
- [ ] **Sayfa Yükleme:** Sayfa browser'da hatasız yükleniyor mu?
- [ ] **DataTable Toolbar:** Search, Export, Filter ve Add New butonları görünüyor mu?
- [ ] **Localization:** Tüm metinler çözümlenmiş durumda mı? (Raw key görünmüyor mu?)
- [ ] **Console:** Browser console'da JavaScript hatası yok mu?
- [ ] **Boş Durum:** Tablo boşsa "No records found" mesajı düzgün gösteriliyor mu?

## 📝 6. Dokümantasyon
- [ ] **API Dokümanı:** Yeni modülün Swagger/README dokümanı güncellenmiş mi?
- [ ] **Kullanıcı Kılavuzu:** Son kullanıcı için ekran rehberi hazırlanmış mı?
- [ ] **CHANGELOG:** Breaking change varsa `CHANGELOG.md`'ye kaydedilmiş mi?

---

## 📝 Çıktı Formatı (Report)

Her sürüm sonunda aşağıdaki özet rapor hazırlanmalıdır:

| Kategori | Durum (Geçti/Kaldı) | Notlar / Eksikler |
|---|---|---|
| Derleme & Sağlık | | |
| Güvenlik | | |
| Yerelleştirme (7 Dil) | | |
| Veritabanı (Index) | | |

**Final Karar:** [YAYINLANABİLİR / ERTELENDİ]

---
Diten ERP vNext Quality Gate - RELEASE-001