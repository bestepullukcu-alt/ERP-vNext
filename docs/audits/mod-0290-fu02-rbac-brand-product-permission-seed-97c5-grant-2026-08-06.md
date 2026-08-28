# MOD-0290-FU02-RBAC — Brand/Product Permission Catalog Seed + tenant-97c5 Grant

> **Tarih:** 2026-08-06 · **Tür:** RBAC seed (permission catalog + tenant-97c5 operator grant)
> **Tenant:** `97c59330-dbc4-4665-b29c-0c26dbb5cc93` · **Operatör:** bestepullukcu@gmail.com
> **Yetki:** Kullanıcı bu grant'i açıkça talep etti (MOD-0290-FU02 pack'inin **F3** follow-up'ı)
> **Verdict:** **PASS** — 8 key katalogda, operatörün 4 rolünde de grant'li, canlı doğrulandı

---

## 1. Neden ayrı bir task

MOD-0290-FU02 pack'i RBAC seed/grant'i açıkça yasaklamıştı (§14 "Seed/grant bu pack'te yapılmaz" → F3).
Bu nedenle FU02 kapanışında Master Data menüsü görünmüyor ve sayfalar 403 dönüyordu. Kullanıcı grant'i
bu tenant ve bu operatör için açıkça yetkilendirdi.

## 2. Yöntem — Mongo hand-edit YOK

Kanıtlanmış precedent izlendi ([MOD-0151 territory grant](./mod-0151-territory-permission-catalog-seed-97c5-grant-2026-07-23.md)):
değişiklik yalnız `Diten.AuthService.Persistence/Seed/DataSeeder.cs` içinde yapıldı; kayıtlar servis
başlangıcında entity constructor'ları üzerinden yazıldı.

Bu, `rolePermissions` içine elle GUID yazmanın bilinen tuzağını yapısal olarak önler: yanlış subtype ile
yazılan bir GUID o koleksiyonun tamamının okunmasını bozar ve **tüm login'leri** 500'e düşürür. Doğrulamada
yazılan tüm GUID'ler **subType 04** çıktı.

## 3. Katalog

`SeedPermissionsAsync` içine, `mdm.legal-entities.*` bloğundan sonra 8 key eklendi:

```text
mdm.brands.read   mdm.brands.create   mdm.brands.update   mdm.brands.archive
mdm.products.read mdm.products.create mdm.products.update mdm.products.archive
```

- `moduleOverride: "brand-product-master"` → `PlatformAdminModules` içinde olmadığı için `ClassifyScope`
  **Scope = Tenant (0)** üretir. Canlı doğrulama: 8 satırın hepsinde `Scope: 0`.
- **`delete` / `bulk-delete` key'i kasten YOK.** MOD-0290-FU01 §3/§4 hard delete'i yasaklıyor ve runtime
  bunun yerine archive sunuyor; olmayan bir yeteneği katalogda ilan etmek yanıltıcı olurdu.
- `brand-product-master`, `DefaultRolePermissionTemplate.AdminModules` listesine **eklenmedi** — eklenseydi
  grant bütün tenant'ların Admin rolüne yayılırdı. Bu task yalnız 97c5 içindir.

## 4. Grant

`SeedTenant97c5BrandProductGrantAsync` eklendi (workflow grant precedent'iyle aynı şekil): operatörün
**tenant 97c5'teki** rolleri + Admin rolü fallback'i. Kullanıcı sorgusu `Email && TenantId == 97c5` ile
yapılır; aynı e-posta diğer tenant'larda da mevcut olduğu için bu kısıt zorunludur.

Her (rol, permission) çifti için mevcut kayıt kontrol edilir; yoksa `RolePermission.SystemGrant(...)` eklenir
→ **idempotent**, tekrar çalıştırmada duplicate üretmez.

### Canlı doğrulama (Mongo, salt okuma)

| Rol | Tenant 97c5 | Key sayısı |
|---|---|---|
| **Admin** | ✅ | **8** |
| GQD | ✅ | 8 |
| QADocumentation | ✅ | 8 |
| DocumentMasterRegisterLinker | ✅ | 8 |
| SuperAdmin | — (platform tenant) | 8 · default rol şablonundan, bu task'ın ürünü değil |
| Viewer | — (platform tenant) | 2 (`*.read`) · default rol şablonundan |

Operatörün 97c5 rol bağlantıları: Admin, DocumentMasterRegisterLinker, GQD, QADocumentation — **dördü de**
8 key'e sahip. Toplam 42 `rolePermissions` satırı, hepsi `subType 04`.

Seeder çıktısı (`logs/Auth-restart-out.log`):

```text
Seeding tenant-97c5 Brand/Product master operator grant...
Granted 0 missing mdm.brands.*/mdm.products.* permission(s) across 4 tenant-97c5 role(s).
```

(`0 missing` = idempotent tekrar çalışma; grant'ler bir önceki seed turunda yazılmıştı.)

## 5. Operasyonel notlar / bu task sırasında öğrenilenler

1. **Fleet `dotnet watch run --no-hot-reload` ile koşuyor. Watch yalnız DOSYA DEĞİŞİMİNDE restart eder,
   process ölümünde değil.** AuthService process'i durdurulduğunda watch onu geri getirmedi; servis
   `dotnet run --launch-profile http` ile elle ayağa kaldırıldı. Bu, AuthService'i şu an fleet script'inin
   job'ı dışında bırakıyor — bir sonraki tam fleet restart'ı bunu normalize eder.
2. **Watch, çok adımlı bir düzenlemenin ortasında rebuild alabilir.** İlk turda katalog seed'lendi ama grant
   metodu henüz wire edilmemişti; log'da grant satırı yoktu. DB'ye güvenmeden önce ilgili seed satırının
   `logs/Auth-*.log` içinde gerçekten yazıldığı doğrulanmalı.

## 6. Fleet sağlığı (grant sonrası)

| Servis | Sonuç |
|---|---|
| Auth 5056 `/health` | **200** |
| Mdm 5059 `/health` | **200** |
| Web 5001 | **200** |
| Gateway 5000 `/api/mdm/brands` (anon) | **401** (route var, fail-closed) |
| `POST /api/tenant-auth/login` (geçersiz kimlik) | **400** — 500 değil, login sağlam |

## 7. Kapsam dışı — dokunulmadı

Runtime kod (MDM/CRM/frontend) · Gateway · registry · MOD-0048 publish · Mongo hand-edit ·
`AdminModules` listesi · diğer tenant'ların rolleri · aynı e-postanın diğer tenant'lardaki kayıtları.

## 8. Sonraki adım

Operatörün **oturumu kapatıp yeniden açması** gerekir: permission'lar login sırasında JWT `permission`
claim'ine yazılır, mevcut token eski claim setini taşır. Yeni token ile:

- Sol menüde **Master Data → Markalar / Ürünler** görünür
- `/MasterData/Brands` ve `/MasterData/Products` açılır
- `./scripts/smoke-mod0290-fu02-brand-product-authenticated.ps1` uçtan uca çalıştırılabilir
  → FU02 evidence §20/§21 kapanır, pack `review` → `done`
