# Item Master User Guide

## Navigation

- Sol menu: `Item Master`
- Secondary navigation:
  - `Item Master`
  - `Category Management`
  - `Variant Management`

## Item Master

`/Items` ekrani DataTable v2 standardina gore calisir.

- Varsayilan kolonlar: `Code`, `Name`, `ItemType`, `Category`, `BaseUom`, `TrackingPolicy`, `LifecycleState`, `Status`, `Actions`
- Inline filtreler: `ItemType`, `Category`, `LifecycleState`, `Status`
- Save View, column visibility ve column order desteklenir
- Quick View, details, edit ve bulk delete akislari mevcuttur

## Create / Edit

Form uc karttan olusur:

- Kimlik ve siniflandirma
- Operasyonel bayraklar
- Izlenebilirlik ve attribute/variant atamalari

Kurallar:

- Kalem tipi degistiginde kategori ve varyant modeli secenekleri filtrelenir
- `Service Item` tipi secildiginde `ServiceItem` flag'i uyumlu tutulur
- Variant modeli secildiginde ilgili attribute ve variant editorleri otomatik yuklenir

## Details

Details sayfasi read-only sekmelerle calisir:

- `Overview`
- `Attributes`
- `Variants`

## Category Management

`/ItemCategories` ekrani kategori hiyerarsisini offcanvas editor ile yonetir.

- Liste, filtre, quick view ve bulk delete desteklenir
- Parent-child cycle backend tarafinda engellenir
- Parent kategori farkli item type altindaysa kayit reddedilir

## Variant Management

`/ItemVariantModels` ekrani reusable varyant modellerini yonetir.

- Attribute listesi offcanvas editor icinde tanimlanir
- Attribute eksenleri `Variant Axis` olarak isaretlenebilir
- Item save akisinda yalniz secili model ile uyumlu attribute ve variant kombinasyonlari kabul edilir
