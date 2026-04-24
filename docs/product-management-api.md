# Product Management API

## Scope

`Product Management` modulu MDM altinda ticari urun kimligini yonetir. Ana route `/Products`, gateway base route `/api/products` olarak tanimlanmistir.

Bu fazda yalnizca Product ust kimligi kapsamdadir. Asagidaki alanlar bilerek disarida tutulur:

- `BaseUomId`
- `IsBatchTracked`
- `IsSerialTracked`
- SKU / Variant ownership
- Composition ownership
- Packaging ownership

## Collection

- `products`

`products` koleksiyonu tenant-aware ve soft-delete desteklidir. Compound unique index:

- `TenantId + Code + IsDeleted`

## Entity Shape

- `Code`
- `Name`
- `ShortName`
- `Description`
- `ProductType`
- `CategoryId`
- `LifecycleStateId`
- `IsSaleable`
- `IsPurchasable`
- `IsManufacturable`

`LifecycleStateId` urunun tek state kaynagidir. `IsActive` ve `Status` bulunmaz.

## Lookups

- `ProductType` enum/lookup olarak tanimlidir:
  - `FinishedGood`
  - `Service`
  - `Digital`
- `CategoryId` Faz 1 icin sabit katalog ile sunulur:
  - `STANDARD`
  - `REGULATED`
  - `PROFESSIONAL`
  - `SUPPORT`
  - `LICENSE`
  - `SUBSCRIPTION`
- Lifecycle lookup mevcut `GET /api/lifecycle-states` endpointi uzerinden alinır.

## Resources

- `GET /api/products`
- `GET /api/products/{id}`
- `POST /api/products`
- `PUT /api/products/{id}`
- `PATCH /api/products/{id}/lifecycle`
- `DELETE /api/products/{id}`
- `DELETE /api/products/bulk`

Gateway explicit base ve `{everything}` rotalari `GET`, `POST`, `PUT`, `PATCH`, `DELETE`, `OPTIONS` icin tanimlanmistir.

## Validation Rules

- `Code` tenant icinde benzersizdir.
- `ProductType` free text degil, enum/lookup ile sinirlandirilir.
- `CategoryId`, secilen `ProductType` ile uyumlu olmalidir.
- `LifecycleStateId` zorunludur ve lookup ile dogrulanir.
- Izinli lifecycle gecisleri:
  - `Draft -> Active`
  - `Active -> Blocked`
  - `Blocked -> Active`
  - `Active -> Obsolete`
  - `Blocked -> Obsolete`
- `Obsolete` final state'tir; geri acilmaz.

## Permissions

Permission sabitleri [ProductPermissions.cs](/Users/alitufanoglu/Desktop/ERP-vNext/services/Diten.MdmService/src/Diten.MdmService.Application/Authorization/ProductPermissions.cs) dosyasinda tanimlidir:

- `Modules.Products.Read`
- `Modules.Products.Create`
- `Modules.Products.Update`
- `Modules.Products.Delete`
- `Modules.Products.BulkDelete`
- `Modules.Products.Patch`

Mevcut repoda MDM servisi icin permission enforcement surface hazirdir; runtime authorization entegrasyonu mevcut yapinin geri kalanıyla ayni seviyededir.

## Architecture & Folders

- **Handlers:** `Diten.MdmService.Application/Features/Products/Handlers/`
  - `CommandHandlers/`: Create, Update, Delete, Patch işlemleri.
  - `QueryHandlers/`: GetAll, GetById işlemleri.
- **Logic Helper:** `Diten.MdmService.Application/Features/Products/ProductLogicHelper.cs` (Validation ve Mapping logic).
- **Frontend Controller:** `Diten.Web/Controllers/ProductsController.cs`
- **Frontend UI:** `Diten.Web/Views/MDM/Products/`

## Performance Optimization

- **Zero-Seed Overhead:** Handler katmanında seed data kontrolleri bulunmaz; istekler doğrudan veritabanı sorgusuna odaklanır.
- **Efficient Mapping:** Domain to DTO dönüşümleri `ProductMapping` üzerinden optimize edilmiştir.
- **Compound Indexing:** `TenantId + Code + IsDeleted` indeksi ile tenant-wide lookup hızı optimize edilmiştir.
- **Skeleton Loading:** Frontend listeleme sırasında DataTable v2 skeleton-loader standardını kullanır.
