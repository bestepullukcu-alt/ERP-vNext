---
description: Diten ERP vNext yerel geliştirme ortamı kurulumu, servis çalıştırma sırası ve sorun giderme rehberi.
---

# Local Development Runbook (Diten ERP vNext)

Bu rehber, projenin tüm mikroservis bileşenlerini yerel ortamda (Localhost) hatasız ve senkronize bir şekilde ayağa kaldırmak için gereken standart prosedürü tanımlar.

---

## 🛑 Ön Hazırlık (Terminal Temizliği)

Geliştirmeye başlamadan önce veya büyük bir kod değişikliği sonrası, port çakışmalarını önlemek için şu komutu çalıştırmak anayasa kuralıdır:

# KOMUT BAŞI
lsof -ti:5000,5001,5050,5056 | xargs kill -9
# KOMUT SONU

---

## 🚀 Çalıştırma Sırası (4-Tab Düzeni)

Projeyi tam fonksiyonel çalıştırmak için VS Code terminalinde 4 ayrı sekme açın ve servisleri KESİNLİKLE aşağıdaki sırayla başlatın:

### 1. TAB 1: Auth Service (Port: 5056)
- **Dizin:** src/Services/Diten.Auth.Api
- **Komut:** dotnet run
- **Neden:** Diğer tüm servislerin yetki kontrolü (JWT validation) yapabilmesi için kimlik servisinin ayakta olması gerekir.

### 2. TAB 2: MDM Service (Port: 5050)
- **Dizin:** src/Services/Diten.MDM.Api
- **Komut:** dotnet run
- **Kontrol:** MongoDB bağlantısının başarılı olduğunu loglardan doğrulayın.

### 3. TAB 3: API Gateway (Port: 5000)
- **Dizin:** src/Gateways/Diten.ApiGateway
- **Komut:** dotnet run
- **Önemli:** Auth ve MDM servisleri hazır olmadan Gateway'i başlatmayın.

### 4. TAB 4: Frontend Web (Port: 5001)
- **Dizin:** src/Web/Diten.Web
- **Komut:** dotnet run
- **Erişim:** http://localhost:5001 adresine giderek Sneat PRO arayüzüne giriş yapın.

---

## 🛠️ Önemli Geliştirme Notları

### 🌍 Dil Dosyaları (.resx) Hatırlatması
UI tarafındaki metinlerin (Örn: LegalEntities ekranları) 8 dilde doğru görünmesi için, .resx dosyalarında yapılan her değişiklikten sonra tüm çözümü yeniden derlemeniz gerekir:
- dotnet build veya run_all.sh betiğini kullanın.

### 🆔 Sabit Test Verisi
Giriş yaparken veya API çağrısı atarken kullanılan X-Tenant-Id anayasa gereği her zaman şudur:
00000000-0000-0000-0000-000000000001

---

## 📝 Otomasyon (Hızlı Başlatma)
Eğer sistemi tek komutla ayağa kaldırmak isterseniz, kök dizindeki otomasyon betiğini çalıştırın:

sh run_all.sh

---

> **Not:** Sistem orkestratörüne "Projeyi çalıştır" derseniz, arka planda bu 4 sekmeyi otomatik olarak yönetecektir.