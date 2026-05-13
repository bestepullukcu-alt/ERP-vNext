# Golden Reference Slim Live Move Plan

## Goal
`GoldenReferenceSlim` artik MDM altinda degil, dogrudan yeni domainin canli parcasi olarak yasamalidir.

## Final Target
### Frontend
- `frontend/Diten.Web/Views/DevEnablement/GoldenReferenceSlim/**`
- `frontend/Diten.Web/wwwroot/assets/js/DevEnablement/GoldenReferenceSlim/**`
- `frontend/Diten.Web/Resources/Views/DevEnablement/GoldenReferenceSlim/**`
- Gerekirse controller route adi ayni kalabilir: `/GoldenReferenceSlim`

### Backend
- `services/Diten.DevEnablementService/src/Diten.DevEnablementService.Api/**`
- `services/Diten.DevEnablementService/src/Diten.DevEnablementService.Application/**`
- `services/Diten.DevEnablementService/src/Diten.DevEnablementService.Domain/**`
- `services/Diten.DevEnablementService/src/Diten.DevEnablementService.Persistence/**`

### Governance
- `execution/domains/developer-enablement/**`

## Current MDM Footprint To Remove
### Frontend
- `frontend/Diten.Web/Views/MDM/GoldenReferenceSlim/**`
- `frontend/Diten.Web/wwwroot/assets/js/MDM/GoldenReferenceSlim/**`
- `frontend/Diten.Web/Resources/Views/MDM/GoldenReferenceSlim/**`

### Backend
- `services/Diten.MdmService/src/Diten.MdmService.Api/Controllers/GoldenReferenceSlimController.cs`
- `services/Diten.MdmService/src/Diten.MdmService.Application/Features/GoldenReferenceSlim/**`
- `services/Diten.MdmService/src/Diten.MdmService.Domain/Entities/GoldenReferenceSlim.cs`
- `services/Diten.MdmService/src/Diten.MdmService.Domain/Repositories/IGoldenReferenceSlimRepository.cs`
- `services/Diten.MdmService/src/Diten.MdmService.Persistence/Repositories/GoldenReferenceSlimRepository.cs`

### Gateway
- `gateway/Diten.ApiGateway/ocelot.json` icindeki `golden-reference-slim` rotalari yeni servise yonlenmeli

## Recommended Move Order
1. Yeni servis iskeletini olustur: `DitenDevEnablementService`
2. Domain/Application/Persistence/API katmanlarina GoldenReferenceSlim backend kodunu tasi
3. Frontend view/js/resource klasorlerini `DevEnablement` altina tasi
4. Frontend controller'i yeni view/resource yollarina gore guncelle
5. Gateway route'larini yeni servise yonlendir
6. MDM altindaki eski dosyalari temizle
7. Eski `reference/golden-module-kit` klasorunu sil

## Reference Folder Decision
Bu karar modeline gore `reference/golden-module-kit` zorunlu degildir.
Canli modul tamamen tasindigi icin kaldirilabilir.

## Important Constraints
- `gateway/.../ocelot.json` protected path oldugu icin degisim kontrollu yapilmalidir
- Yeni servis acilmadan backend tasimasi tamamlanmis sayilmaz
- Fiziksel tasima tamamlanmadan "MDM ile iliskisi bitti" denmemelidir
