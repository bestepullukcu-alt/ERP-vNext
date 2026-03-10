# Orchestrator Prompt Şablonları

Aşağıdaki şablonları kopyalayıp ilgili `{{değişkenleri}}` doldurarak kullanın.

---

## 🆕 1. Yeni Modül (En Kapsamlı)

```
@[.antigravity/agents/orchestrator.md]

/add-module {{ModulName}} ({{AreaName}} servisi)

Alan tanımları:
- {{Alan1}}: {{Tip}} (zorunlu)
- {{Alan2}}: {{Tip}} (opsiyonel)

İş kuralları:
- {{KuralVarsa}}

UI tipi: DataTable (Liste/CRUD)
```

**Örnek:**
```
@[.antigravity/agents/orchestrator.md]

/add-module Currencies (MDM servisi)

Alan tanımları:
- Code: string (zorunlu, ISO 4217)
- Name: string (zorunlu)
- Symbol: string (zorunlu)
- IsActive: bool

UI tipi: DataTable (Liste/CRUD)
```

---

## 🔧 2. Mevcut Sayfayı Düzelt / Yeniden Yap

```
@[.antigravity/agents/orchestrator.md]

{{ModulName}} sayfasındaki şu sorunları düzelt:
1. {{Sorun1}}
2. {{Sorun2}}

Referans: {{DoğruSayfaAdı}} sayfası gibi olmalı.
Değiştirilmeyecekler: {{ElleMeDokun}}
```

**Örnek:**
```
@[.antigravity/agents/orchestrator.md]

Countries sayfasındaki şu sorunları düzelt:
1. DataTable DtDefaults.create() kullanmıyor, quality-gate-datatable kurallarına uygun hale getir
2. _Filter.cshtml eksik, oluştur ve bağla
3. Offcanvas içinde hardcoded İngilizce string var

Referans: LegalEntities sayfası gibi olmalı.
Değiştirilmeyecekler: Backend (controller, entity, repository)
```

---

## ➕ 3. Mevcut Modüle Endpoint Ekle

```
@[.antigravity/agents/orchestrator.md]

/add-endpoint-cqrs {{ModulName}} modülüne

Yeni endpoint: {{HTTP_Method}} /api/{{resource}}/{{action}}
İş mantığı: {{Açıklama}}
Validation: {{Kurallar}}
Auth: [HasPermission("Modules.{{ModulName}}.{{Action}}")]
```

**Örnek:**
```
@[.antigravity/agents/orchestrator.md]

/add-endpoint-cqrs Countries modülüne

Yeni endpoint: POST /api/countries/bulk-import
İş mantığı: JSON body içindeki country listesini toplu olarak ekle, varsa ISO2Code ile duplicate kontrolü yap
Validation: list boş olamaz, her item'da Name ve Iso2Code zorunlu
Auth: [HasPermission("Modules.Countries.Create")]
```

---

## 🎨 4. Sadece Frontend Değişikliği

```
@[.antigravity/agents/orchestrator.md]

{{ModulName}} modülünün frontend'ini güncelle:
- Değiştirilecek: {{NeDeğişecek}}
- Değiştirilmeyecek: Backend API'leri

frontend-ui-ux agent kurallarına göre yap.
```

**Örnek:**
```
@[.antigravity/agents/orchestrator.md]

Countries modülünün frontend'ini güncelle:
- Değiştirilecek: Offcanvas içindeki tüm hardcoded string'leri @Localizer ile değiştir, Filter offcanvas ekle
- Değiştirilmeyecek: Backend API'leri ve JS AJAX mantığı

frontend-ui-ux agent kurallarına göre yap.
```

---

## 🌍 5. Lokalizasyon Güncelleme

```
@[.antigravity/agents/orchestrator.md]

{{ModulName}} modülüne şu yeni L10n key'lerini ekle (8 dil):
- Key: {{Key1}} → TR: {{Türkçe}}, EN: {{İngilizce}}
- Key: {{Key2}} → TR: {{Türkçe}}, EN: {{İngilizce}}

SharedResource'a mı? ViewResource'a mı? → {{Seçim}}
```

**Örnek:**
```
@[.antigravity/agents/orchestrator.md]

Countries modülüne şu yeni L10n key'lerini ekle (8 dil):
- Key: BulkImportSuccess → TR: "{0} ülke başarıyla içe aktarıldı", EN: "{0} countries imported"
- Key: DuplicateIsoCode → TR: "Bu ISO kodu zaten mevcut", EN: "ISO code already exists"

ViewResource'a ekle (Resources/Views/MDM/Countries/Index.{lang}.resx)
```

---

## 🔒 6. Güvenlik / Permission Ekleme

```
@[.antigravity/agents/orchestrator.md]

{{ModulName}} modülündeki şu endpoint'lere RBAC ekle:
- GET /api/{{resource}} → [HasPermission("Modules.{{ModulName}}.Read")]
- POST /api/{{resource}} → [HasPermission("Modules.{{ModulName}}.Create")]
- DELETE /api/{{resource}}/{id} → [HasPermission("Modules.{{ModulName}}.Delete")]

[AllowAnonymous] varsa kaldır.
security-agent kurallarına göre yap.
```

---

## 🧪 7. Kod İnceleme / Audit

```
@[.antigravity/agents/orchestrator.md]

{{ModulName}} modülünü şu açılardan incele ve rapor ver:
- Multi-tenancy (TenantId filtresi eksiksiz mi?)
- Soft Delete (IsDeleted + DeletedAt doğru mu?)
- RBAC ([HasPermission] var mı?)
- Frontend (quality-gate-datatable kurallarına uyuyor mu?)

Kod yazma — sadece rapor ver.
```

---

## 🗑️ 8. Modül Temizleme / Silme (Yeniden Yapmadan Önce)

Bu senaryo, hatalı veya standart dışı yapılmış bir modülü **sıfırlayıp** baştan doğru yapmak istediğinde kullanılır.

> ⚠️ **Uyarı:** Bu prompt kodu siler. Önce `git commit` alındığını doğrula.

```
@[.antigravity/agents/orchestrator.md]

{{ModulName}} modülünü tamamen temizle (sil). Yeniden yazılacak, önce silinmesi gerekiyor.

Silinecekler:
BACKEND:
- services/Diten{{ServiceName}}Service/src/.../Features/{{ModulName}}/ (tüm klasör)
- services/Diten{{ServiceName}}Service/src/.../Repositories/{{ModulName}}Repository.cs
- services/Diten{{ServiceName}}Service/src/.../Controllers/{{ModulName}}Controller.cs
- services/Diten{{ServiceName}}Service/src/.../Domain/Entities/{{EntityName}}.cs

FRONTEND:
- frontend/Diten.Web/Controllers/{{ModulName}}Controller.cs
- frontend/Diten.Web/Views/{{AreaName}}/{{ModulName}}/ (tüm klasör)
- frontend/Diten.Web/wwwroot/assets/js/{{areaname}}/{{modulname}}/ (tüm klasör)
- frontend/Diten.Web/Resources/Views/{{AreaName}}/{{ModulName}}/ (tüm klasör)

GATEWAY:
- ocelot.json içindeki /api/{{resource}} ve /api/{{resource}}/{everything} rotaları

SIDEBAR:
- _LayoutBackbone.cshtml içindeki {{ModulName}} menü item'ı

Dokunulmayacaklar: {{KorunacakDosyalar}}

Silme tamamlandıktan sonra git commit al, kod yazma.
```

**Örnek:**
```
@[.antigravity/agents/orchestrator.md]

Countries modülünü tamamen temizle (sil). Yeniden yazılacak, önce silinmesi gerekiyor.

Silinecekler:
BACKEND:
- services/DitenMdmService/src/.../Features/Countries/ (tüm klasör)
- services/DitenMdmService/src/Diten.MdmService.Persistence/Repositories/CountryRepository.cs
- services/DitenMdmService/src/Diten.MdmService.Api/Controllers/CountriesController.cs
- services/DitenMdmService/src/Diten.MdmService.Domain/Entities/Country.cs

FRONTEND:
- frontend/Diten.Web/Controllers/CountriesController.cs
- frontend/Diten.Web/Views/MDM/Countries/ (tüm klasör)
- frontend/Diten.Web/wwwroot/assets/js/mdm/countries/ (tüm klasör)
- frontend/Diten.Web/Resources/Views/MDM/Countries/ (tüm klasör)

GATEWAY:
- ocelot.json içindeki /api/countries ve /api/countries/{everything} rotaları

SIDEBAR:
- _LayoutBackbone.cshtml içindeki Countries menü item'ı

Dokunulmayacaklar: SharedResource.*.resx dosyaları, Domain/Interfaces/ICountryRepository.cs
Silme tamamlandıktan sonra git commit al, kod yazma.
```

---

## 💡 Prompt Yazma Kuralları

| Kural | Açıklama |
|-------|----------|
| Her zaman `@orchestrator` ile başla | Agent dosyasını referans etmeden çalışmaz |
| `/add-module`, `/add-endpoint-cqrs` gibi slash komutları varsa kullan | Workflow'u otomatik tetikler |
| "Değiştirilmeyecekler" bölümünü yaz | Ajanın sınırını belirler |
| UI tipi belirt | DataTable mi, Form mu, Dashboard mu? |
| Referans modül varsa yaz | "LegalEntities gibi olmalı" ifadesi yaratıcılığı kapatır |
