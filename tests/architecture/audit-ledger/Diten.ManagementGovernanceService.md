# Denetim defteri — Diten.ManagementGovernanceService

Kural: `.antigravity/rules/audit-trail-standard.md` (AUD-001) · Ölçen test: `AuditTrailStandardTests`

Bu dosyada yalnız **bildirim** durur; bir komutun denetlenip denetlenmediği buradan değil, `services/Diten.ManagementGovernanceService/src` altındaki
üretim kodundan okunur. Biçim ve bölümlerin anlamı: [README.md](README.md).

## İzler

| iz | yol | tür | belirteç |
|---|---|---|---|

Bu serviste bugün hiçbir denetim izi yok: işaret, iletici, kendi günlüğü — hiçbiri (ölçüm 2026-10-02).

## İstisnalar

| komut | sınıf | gerekçe |
|---|---|---|

## Dolaylı

| komut | iz | üzerinden |
|---|---|---|

## Bilinen borç

Bu liste **yalnız küçülür**. Satır eklemek yasaktır; denetlenen ya da silinen komutun satırı çıkarılır (test bunu zorlar).

Ad kuralı (CT kararı 2026-10-02): bu serviste adı `Command` ile biten her tür yazma komutudur — MediatR isteği olmayan 10 yapı sözleşmesi (`Modules/Dws/DwsContracts.cs`) aynı adlı MediatR komutlarının yüküdür; o adlar zaten listededir, ikinci kez sayılmaz.

- AddStructuralDependencyCommand
- AddStructureNodeCommand
- ArchiveProcessArchitectureCommand
- ArchiveProcessDefinitionCommand
- ArchiveProcessDomainCommand
- ArchiveProcessFamilyCommand
- CreateNextStructureRevisionCommand
- CreateProcessArchitectureCommand
- CreateProcessDefinitionCommand
- CreateProcessDomainCommand
- CreateProcessFamilyCommand
- CreateStructureBaselineCommand
- CreateStructureCommand
- DwsDispatchRequest
- MoveStructureNodeCommand
- RemoveStructuralDependencyCommand
- RemoveStructureNodeCommand
- ReorderStructureNodeCommand
- UpdateProcessArchitectureCommand
- UpdateProcessDefinitionCommand
- UpdateProcessDomainCommand
- UpdateProcessFamilyCommand
- UpdateStructureMetadataCommand
