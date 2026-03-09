# Configuration & Dependency Safety Rules

### 🚫 Hardcoded Veri Yasağı
- `DependencyInjection.cs` veya başka hiçbir kod dosyasında varsayılan bağlantı adresi (connection string) veya şifre bulunamaz.
- Örn: `?? "mongodb://localhost"` kullanımı KESİNLİKLE YASAKTIR.

### 🛡️ Fail-Fast Prensibi
- Gerekli yapılandırma ayarları (`Mongo:ConnectionString` vb.) eksikse, uygulama varsayılan değerle devam etmek yerine `InvalidOperationException` fırlatarak durmalıdır.

### 🔄 Bağımlılık (Circular Dependency) Kuralı
- `Persistence` katmanı sadece `Application` interface'lerini ve `Domain`'i referans alabilir. 
- Katmanlar arası kayıtlar yapılırken `IServiceCollection` üzerinden `IConfiguration` parametre olarak geçilmeli, `Api` katmanına doğrudan bağımlılık (referans) oluşturulmamalıdır.