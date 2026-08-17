# Developer Enablement — Domain Config

## Purpose
Developer Enablement domain'i, urun modullerinden bagimsiz olarak gelistirme surecinde tekrar kullanilacak referans kitleri, ornek modulleri ve scaffold baseline'larini tanimlar. Bu domain'in amaci, gelecekte yazilacak modullerin ayni kalite, klasor yapisi ve delivery kontrati ile baslamasini saglamaktir.

## In-Scope Modules
- `DEV-0000` — Golden Reference Slim (8 ve alti create/edit form alanli DataTable baseline)
- `DEV-0001` — Golden Reference Compact (8'den fazla create/edit form alanli DataTable baseline)
- `DEV-0002` — Complex Form and Details Reference Module (planlandi)

## Out-of-Scope
- Canli is kabiliyeti ureten business modulleri
- MDM/PSS/ESBP ownership altindaki gercek domain nesneleri
- Production menu, navigation ve domain katalog sahipligi
- `.antigravity` kurallarinin sahipligi (yalnizca kullanici acik talebiyle guncellenir)

## Ownership Boundaries
- Bu domain sadece gelistirme referansi olarak uretilecek artefaktlari sahiplenir.
- Referans moduller business-domain semantic'i tasimaz; delivery pattern tasir.
- Buradaki ciktilar, tamamlandiktan ve olgunlastiktan sonra `.antigravity` kural setlerine kaynak olabilir; ancak bu domain `.antigravity` yerine gecmez.
- Frontend projesi icinde UI-yerlesimsel nedenlerle `_reference` benzeri klasorler bulunabilir; bu istisna ownership'i degistirmez. Referansin yonetsel sahibi yine `developer-enablement` domain'idir.

## Shared Dependencies
- Repo kokundeki `AGENTS.md` authority order'u
- Domainler arasi ortak delivery standartlari

## Domain-Level Repo Scope
- `execution/domains/developer-enablement/**`
- Gerekirse gelecekte `docs/audits/**` altinda reference audit ciktilari
- Frontend icindeki `_reference` tabanli istisna yapilarin ownership notlari

## Protected Paths
- `.antigravity/**`
- `frontend/Diten.Web/Controllers/Archive/**`
- `frontend/Diten.Web/Views/Archive/**`
- `frontend/Diten.Web/Views/Shared/_Layout.cshtml`
- `gateway/Diten.ApiGateway/**/ocelot.json`
- `services/Diten.MdmService/**`
- `services/Diten.AuthService/**`
- `services/Diten.Platform/**`
- `services/Diten.EnterpriseStrategyService/**`

## Runtime and Delivery Notes
- Bu domain altindaki moduller "reference-first" mantigi ile ilerler.
- Reference module tamamlanmadan resmi golden rule'a donusturulmez.
- Her referans module pack'i, hedefledigi veri yogunlugu ve UI/CQRS karmasiklik seviyesini acikca yazmalidir.

## Bootstrap Notes
- Bu domain, mevcut 3 business/platform domainine alternatif degil; onlari destekleyen gelistirme enablement katmanidir.
- `.antigravity/agents/orchestrator.md` ve global workflow'lar bu domaini henuz tanimiyor olabilir; bu nedenle gecis sureci kontrollu yurutulmelidir.
