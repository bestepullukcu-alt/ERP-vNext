---
id: DEV-0000
name: Golden Reference Slim
domain: developer-enablement
status: ready-for-dev
owner: ai-orchestrator
branch: feature/dev/dev-0000-golden-reference-slim
started: 2026-04-22
target: 2026-05-15
---

# DEV-0000 — Golden Reference Slim

## Module Summary
Bu modul bir urun ozelligi degildir. Amaci, gelistirme asamasinda gelecekte yazilacak moduller icin referans alinacak bir baseline olusturmaktir. Ilk varyant, yaklasik 8 ana veri alani iceren dusuk-orta karmasiklikta bir CRUD + DataTable + details akisini temsil eder.

## Why This Module Exists
- `.antigravity` kurallarina gecmeden once gercek repo kosullarinda bir referans modul olgunlastirmak
- Gelecekte yapilacak moduller icin tekrar kullanilabilir klasor yapisi ve delivery pattern'i cikarmak
- Frontend, backend, gateway ve localization entegrasyonunu tek bir ornek uzerinde netlestirmek

## Scope
- In-scope:
  - `execution/domains/developer-enablement/module-packs/DEV-0000-golden-reference-slim.md`
  - `frontend/Diten.Web/Views/DevEnablement/GoldenReferenceSlim/**`
  - `frontend/Diten.Web/wwwroot/assets/js/DevEnablement/GoldenReferenceSlim/**`
  - `frontend/Diten.Web/Resources/Views/DevEnablement/GoldenReferenceSlim/**`
  - `services/Diten.DevEnablementService/**`
  - Gerekirse bu referansi aciklayan audit ve karar dokumanlari
- Out-of-scope:
  - Production menu sahipligi
  - Gercek business capability iddiasi
  - `.antigravity/**` altinda rule ekleme veya guncelleme
  - Baska business domain modullerinin dogrudan refactor edilmesi

## Owned Objects
- Golden reference module baseline
- 8 alanli veri modeli icin CRUD referans akisi
- DataTable v2 listeleme baseline'i
- Create / Edit / Details referans sayfa yapisi
- Baslangic scaffold ve turetme mantigi
- Bu modulden tureyecek gelecekteki reference module ailesinin ilk uyesi

## Repo Scope
- `execution/domains/developer-enablement/**`
- `frontend/Diten.Web/Views/DevEnablement/GoldenReferenceSlim/**`
- `frontend/Diten.Web/wwwroot/assets/js/DevEnablement/GoldenReferenceSlim/**`
- `frontend/Diten.Web/Resources/Views/DevEnablement/GoldenReferenceSlim/**`
- `services/Diten.DevEnablementService/**`

## Current Reality Note
Bu referansin calisan kod izleri `DevEnablement` area + `Diten.DevEnablementService` altina tasinmistir. Hedef durum, bu module pack'in gelecekteki 8 ve alti form alanli DataTable modulleri icin resmi Slim referans olmasidir.

## Migration Intent
- Governance ownership: `developer-enablement`
- Runtime host: `DevEnablement` area + `Diten.DevEnablementService`
- Hedef durum: referans modulun tek canli kaynagi developer-enablement domain'i altinda bulunur
- Kural: frontend projesi icindeki `_reference` istisnasi haric, bu ve sonraki tum reference module'ler `developer-enablement` altinda yonetilir
- Ek karar: gecis tamamlandigi icin eski `reference/golden-module-kit` kaynagi kaldirilabilir

## Known Runtime Footprint
- Frontend controller ve view'ler su an `frontend/Diten.Web` altinda
- Gateway route'lari su an `ocelot.json` icinde
- Backend API ve CQRS katmani `services/Diten.DevEnablementService` altinda

## Target Runtime Footprint
- `frontend/Diten.Web/Views/DevEnablement/GoldenReferenceSlim/**`
- `frontend/Diten.Web/wwwroot/assets/js/DevEnablement/GoldenReferenceSlim/**`
- `frontend/Diten.Web/Resources/Views/DevEnablement/GoldenReferenceSlim/**`
- `services/Diten.DevEnablementService/src/Diten.DevEnablementService.Api/**`
- `services/Diten.DevEnablementService/src/Diten.DevEnablementService.Application/**`
- `services/Diten.DevEnablementService/src/Diten.DevEnablementService.Domain/**`
- `services/Diten.DevEnablementService/src/Diten.DevEnablementService.Persistence/**`

## Acceptance Criteria
- [ ] Golden Reference Slim artik MDM business module gibi degil, developer enablement reference asset olarak tanimlanmis olacak.
- [ ] Referansin hangi veri yogunlugu ve karmasiklik seviyesi icin ornek oldugu dokumante edilecek.
- [x] Canli kod artik MDM yerine `DevEnablement` + `Diten.DevEnablementService` altinda bulunacak.
- [x] Eski `reference/golden-module-kit` ve scaffold kaynagi kaldirildi.
- [ ] Tamamlandiginda, bundan tureyecek en az iki sonraki referans tipinin backlog'u tanimlanacak.
- [ ] `.antigravity` kurallarina tasinacak aday pattern'ler ayrica listelenecek.

## Test Expectations
- Scaffold script en az bir ornek modul uretimi icin calistirilabilir olmali.
- Frontend DataTable ve page structure referansi statik olarak dogrulanabilir olmali.
- Referans modul, "business domain ownership" ile karistirilmayacak kadar net dokumante edilmeli.

## Follow-up Candidates
- `DEV-0001` — Large Dataset Reference Module
- `DEV-0002` — Complex Form and Details Reference Module
- `DEV-0003` — Read-heavy Lookup Reference Module

## Protected Paths
- `.antigravity/**`
- `frontend/Diten.Web/Controllers/Archive/**`
- `frontend/Diten.Web/Views/Archive/**`
- `frontend/Diten.Web/Views/Shared/_Layout.cshtml`
