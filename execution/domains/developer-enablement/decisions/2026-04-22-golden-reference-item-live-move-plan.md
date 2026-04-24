# Golden Reference Item Live Move Plan

## Goal
`GoldenReferenceItem` artik MDM altinda degil, dogrudan yeni domainin canli parcasi olarak yasamalidir.

## Final Target
### Frontend
- `frontend/Diten.Web/Views/DevEnablement/GoldenReferenceItem/**`
- `frontend/Diten.Web/wwwroot/assets/js/DevEnablement/GoldenReferenceItem/**`
- `frontend/Diten.Web/Resources/Views/DevEnablement/GoldenReferenceItem/**`
- Gerekirse controller route adi ayni kalabilir: `/GoldenReferenceItem`

### Backend
- `services/Diten.DevEnablementService/src/Diten.DevEnablementService.Api/**`
- `services/Diten.DevEnablementService/src/Diten.DevEnablementService.Application/**`
- `services/Diten.DevEnablementService/src/Diten.DevEnablementService.Domain/**`
- `services/Diten.DevEnablementService/src/Diten.DevEnablementService.Persistence/**`

### Governance
- `execution/domains/developer-enablement/**`

## Current MDM Footprint To Remove
### Frontend
- `frontend/Diten.Web/Views/MDM/GoldenReferenceItem/**`
- `frontend/Diten.Web/wwwroot/assets/js/MDM/GoldenReferenceItem/**`
- `frontend/Diten.Web/Resources/Views/MDM/GoldenReferenceItem/**`

### Backend
- `services/Diten.MdmService/src/Diten.MdmService.Api/Controllers/GoldenReferenceItemController.cs`
- `services/Diten.MdmService/src/Diten.MdmService.Application/Features/GoldenReferenceItems/**`
- `services/Diten.MdmService/src/Diten.MdmService.Domain/Entities/GoldenReferenceItem.cs`
- `services/Diten.MdmService/src/Diten.MdmService.Domain/Repositories/IGoldenReferenceItemRepository.cs`
- `services/Diten.MdmService/src/Diten.MdmService.Persistence/Repositories/GoldenReferenceItemRepository.cs`

### Gateway
- `gateway/Diten.ApiGateway/ocelot.json` icindeki `golden-reference-item` rotalari yeni servise yonlenmeli

## Recommended Move Order
1. Yeni servis iskeletini olustur: `DitenDevEnablementService`
2. Domain/Application/Persistence/API katmanlarina GoldenReferenceItem backend kodunu tasi
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
