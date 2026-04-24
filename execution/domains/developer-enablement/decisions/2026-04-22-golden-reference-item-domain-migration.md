# Decision: Golden Reference Item Domain Migration

## Status
Proposed

## Date
2026-04-22

## Context
`GoldenReferenceItem` su anda repo icinde calisan bir referans modul gibi davranmaktadir; ancak semantic olarak bir business module degil, development reference asset'tir. Buna ragmen mevcut izleri agirlikla MDM ownership'i altinda gorunmektedir.

Bu durum iki sorun yaratir:
- MDM domain'i ile gelistirme referansi kavrami birbirine karisir.
- Gelecekte eklenecek diger referans varyantlari icin yanlis ownership modeli olusur.

## Decision
`GoldenReferenceItem` icin mantiksal ownership, `master-data-management` altindan alinip `developer-enablement` domain'ine tasinacaktir. Nihai hedef, bu referans modulun hem ownership hem de canli runtime kodu olarak `DevEnablement` / `DitenDevEnablementService` altinda yasamasidir.

Bu tasima iki seviyede dusunulur:
1. **Execution ownership tasimasi**
   - Module pack ve yonetim dili `developer-enablement` altina alinacak.
   - Referans kataloglama bu domain altinda yapilacak.
2. **Runtime host reality**
   - Mevcut frontend, gateway ve backend kodlari kisa vadede gecici host lokasyonlarinda kalabilir.
   - Nihai hedef, canli modulun `Views/DevEnablement/GoldenReferenceItem` ve `DitenDevEnablementService` altina tasinmasidir.

## Why This Is Better
- Business domain ile engineering reference ayrisir.
- Gelecekte:
  - 8 alanli baseline
  - buyuk veri baseline'i
  - karmasik details/form baseline'i
  - lookup/read-heavy baseline

  gibi ornekler ayni katalog altinda toplanabilir.
- `.antigravity` kurallarina gecmeden once referanslar kontrollu sekilde olgunlastirilir.

## Current MDM-Coupled Footprint
Asagidaki izler bugun MDM veya MDM-hosted yollar altinda gorunmektedir:

### Governance / execution
- `execution/domains/master-data-management/module-packs/MOD-0000-golden-reference-item.md`

### Reference assets
- Eski `reference/golden-module-kit/**` kaynagi
- Eski `scripts/scaffold-from-golden.sh`
- Eski `scripts/README-golden-scaffold.md`

### Frontend
- `frontend/Diten.Web/Controllers/GoldenReferenceItemController.cs`
- `frontend/Diten.Web/Models/GoldenReferenceItem/**`
- `frontend/Diten.Web/Views/MDM/GoldenReferenceItem/**`
- `frontend/Diten.Web/wwwroot/assets/js/MDM/GoldenReferenceItem/**`
- `frontend/Diten.Web/Resources/Views/MDM/GoldenReferenceItem/**`

### Gateway
- `gateway/Diten.ApiGateway/ocelot.json`
  - `/api/golden-reference-item`
  - `/api/golden-reference-item/{everything}`

### Backend host
- `services/Diten.MdmService/src/Diten.MdmService.Api/Controllers/GoldenReferenceItemController.cs`
- `services/Diten.MdmService/src/Diten.MdmService.Application/Features/GoldenReferenceItems/**`
- `services/Diten.MdmService/src/Diten.MdmService.Domain/Entities/GoldenReferenceItem.cs`
- `services/Diten.MdmService/src/Diten.MdmService.Domain/Repositories/IGoldenReferenceItemRepository.cs`
- `services/Diten.MdmService/src/Diten.MdmService.Persistence/Repositories/GoldenReferenceItemRepository.cs`

## Migration Strategy
### Phase 1 — Governance correction
- `developer-enablement` domain olustur.
- `DEV-0000-golden-reference-item` module pack olustur.
- `MOD-0000` artik aktif ownership dosyasi olarak kullanilmasin.
- Eski `MOD-0000` dosyasi ileride `superseded` veya `archived-reference` durumuna cekilsin.

### Phase 2 — Live-target correction
- Yeni hedef alan `DevEnablement` olarak belirlenir.
- Yeni hedef servis `DitenDevEnablementService` olarak belirlenir.
- Frontend, backend ve resource klasorleri bu hedefe gore tasinir.
- Eski `reference/golden-module-kit` ve scaffold kaynagi kaldirilir.

### Phase 3 — Runtime decoupling
- Referans modul icin kalici host modeli secildi:
  - Frontend area: `DevEnablement`
  - Backend service: `DitenDevEnablementService`
- Fiziksel dosya tasima bu hedefe gore planlanir.

### Phase 4 — Rule extraction
- Referans modul tamamlandiginda, tekrar eden pattern'ler ayrica listelensin.
- Sadece olgunlasmis, tekrar kullanimi dogrulanmis pattern'ler `.antigravity` altina tasinsin.

## Explicit Non-Goals
- Bu asamada `.antigravity/**` degistirmek
- Referans modulu production business module gibi sunmak

## Decision Outcome
Kisa vadede:
- ownership dogru yerde olacak
- hedef runtime netlesecek

Orta vadede:
- yeni referanslar ayni domain altinda kataloglanabilecek
- canli referans moduller yeni domain altinda yasanabilecek
- `.antigravity` kurallari daha saglam bir kaynaktan turetilecek
