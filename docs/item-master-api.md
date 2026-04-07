# Item Master API

## Scope

`Items` modulu MDM altinda teknik olarak `Items`, kullanici tarafinda `Item Master` olarak sunulur. Ana route `/Items`, gateway base route `/api/items` olarak tanimlanmistir.

Bu surum yalnizca master data kapsamindadir. Asagidaki alanlar bilerek disarida tutulur:

- `quantity`
- `warehouse balance`
- `reservation`
- `movement`
- `count`

## Collections

Asagidaki koleksiyonlar tenant-aware ve soft-delete destekli olarak eklenmistir:

- `items`
- `item_categories`
- `item_types`
- `item_variant_models`
- `item_variants`
- `attribute_definitions`
- `attribute_templates`
- `item_attribute_values`
- `unit_of_measures`
- `uom_conversions`
- `tracking_policies`
- `lifecycle_states`

`items` koleksiyonu icin `TenantId + Code + IsDeleted` compound unique index kullanilir.

## Seed Lookups

Seed-backed lookup endpointleri:

- `GET /api/item-types`
- `GET /api/unit-of-measures`
- `GET /api/tracking-policies`
- `GET /api/lifecycle-states`

Varsayilan seed setleri:

- Item Types: `Finished Product`, `Semi Finished Product`, `Raw Material`, `Packaging Material`, `Excipient`, `Active Ingredient`, `Service Item`
- Tracking Policies: `None`, `Batch`, `Serial`, `BatchAndExpiry`
- Lifecycle States: `Draft`, `Active`, `Blocked`, `Obsolete`
- UoM: `EA`, `KG`, `G`, `L`, `ML`, `BOX`, `PACK`, `SERVICE`

## Resources

- `GET /api/items`
- `GET /api/items/{id}`
- `POST /api/items`
- `PUT /api/items/{id}`
- `PATCH /api/items/{id}/status`
- `DELETE /api/items/{id}`
- `DELETE /api/items/bulk`
- `GET /api/item-categories`
- `GET /api/item-categories/{id}`
- `POST /api/item-categories`
- `PUT /api/item-categories/{id}`
- `DELETE /api/item-categories/{id}`
- `DELETE /api/item-categories/bulk`
- `GET /api/item-variant-models`
- `GET /api/item-variant-models/{id}`
- `POST /api/item-variant-models`
- `PUT /api/item-variant-models/{id}`
- `DELETE /api/item-variant-models/{id}`
- `DELETE /api/item-variant-models/bulk`

Gateway explicit base ve `{everything}` rotalari `GET`, `POST`, `PUT`, `PATCH`, `DELETE`, `OPTIONS` icin tanimlanmistir.

## Validation Rules

- `TrackingPolicyId`, `LifecycleStateId`, `BaseUomId`, `CategoryId`, `ItemTypeId` referanslari zorunlu ve dogrulanir.
- Kategori ile kalem ayni `ItemType` kapsaminda olmalidir.
- `ItemCategory` parent-child cycle backend tarafinda bloklanir.
- `ServiceItem` secimi `Service Item` item type ile uyumsuzsa istek reddedilir.
- Variant model secildiyse yalniz ilgili template attribute ve variant eksenleri kabul edilir.

## Permissions

Permission set isimleri hazirlanmistir:

- `Modules.Items.*`
- `Modules.ItemCategories.*`
- `Modules.ItemVariantModels.*`

Konstantlar [ItemPermissions.cs](/Users/alitufanoglu/Desktop/ERP-vNext/services/DitenMdmService/src/Diten.MdmService.Application/Authorization/ItemPermissions.cs) dosyasinda bulunur. Mevcut repoda MDM servisine permission enforcement henuz bagli olmadigi icin bu surumde surface hazirdir, runtime enforcement sonraki auth entegrasyon adimina birakilmistir.
