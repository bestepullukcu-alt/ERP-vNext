# Product Management User Guide

## Navigation

- Sol menu: `Product Management`
- Ana ekran route: `/Products`

## Product List

`/Products` ekrani DataTable v2 standardina gore calisir.

- Varsayilan kolonlar: `Code`, `Name`, `Product Type`, `Category`, `Lifecycle State`, `Actions`
- Inline filtreler: `Product Type`, `Category`, `Lifecycle State`
- Save View, column visibility ve column order desteklenir
- Quick View, details, edit, single delete ve bulk delete akislari mevcuttur

Liste filtreleri premium filtre hiyerarsisi ve Select2 toolbar standardina gore calisir.

## Create / Edit

Form uc karttan olusur:

- Identity
- Classification
- Commercial Flags

Kurallar:

- `Product Type` secimi kategori listesini filtreler
- `Lifecycle State` varsayilan olarak `Draft` gelir
- `IsSaleable`, `IsPurchasable`, `IsManufacturable` Faz 1'de soft flag olarak tutulur

## Details

Details sayfasi read-only kartlarla calisir:

- `Overview`
- `Commercial Flags`
- `Lifecycle`
- `Related SKU`
- `Related Composition`
- `Related Packaging`

`Related SKU`, `Related Composition` ve `Related Packaging` alanlari Faz 1'de yalnizca placeholder / read-only baglanma noktasidir.

## Lifecycle

Lifecycle aksiyonlari details ekranindan yonetilir.

- `Draft` urun aktive edilebilir
- `Active` urun bloklanabilir veya obsolete yapilabilir
- `Blocked` urun tekrar aktive edilebilir veya obsolete yapilabilir
- `Obsolete` urun tekrar acilamaz
