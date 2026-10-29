# WP-0230-DL4B — DoR eki 01: Api csproj'unda üçüncü paket satırı (CT kararı)

> Control Tower kaydı, 2026-10-27. `DOR.md`'nin "CT okumaları" 2. maddesine ektir. `DOR.md` değiştirilmez (K4).

## Durum

İşçi, prompt'un "Api csproj'undaki iki satır dışında paket ihtiyacı → DUR" koşulu yüzünden durdu. Commit yok.

İşçinin ölçümü:
- `Diten.BuildingBlocks.Security.Secrets`, `Microsoft.IdentityModel.Tokens 8.16.0` paketini çekiyor.
- `Microsoft.AspNetCore.Authentication.JwtBearer 8.0.11` ise `JsonWebTokens 7.1.2` getiriyor.
- İki satırla çalışma anında `MissingMethodException` (`Base64UrlEncoder.UnsafeDecode`) oluşuyor. Elle imzalanmış geçerli bir token da `IDX14100` ile düşüyor, yani hiçbir geçerli token doğrulanmıyor.
- Üçüncü satır `System.IdentityModel.Tokens.Jwt 8.16.0` eklenince Api.Tests 287/287 yeşil.

CT ölçümü (CT dalı): `System.IdentityModel.Tokens.Jwt` sürüm `8.16.0`, JwtBearer `8.0.11` ile birlikte şu servislerin Api csproj'unda var: AuthService, CrmService, DevEnablementService, HcmService. Yani bu satır depodaki kalıbın aynısıdır.

## Karar

Üçüncü satıra izin verilir. Satır yalnız `System.IdentityModel.Tokens.Jwt` paketini, CrmService ile aynı sürümde (`8.16.0`) ekler. Bu, `DOR.md` CT okuması 2'deki "iki satır gerekli" ifadesinin genişletilmesidir. Gerekçe aynıdır: DL-4'ün AC-5'i bu bağımlılık olmadan çalışmaz. Başka paket eklenmez.

DOĞRULA 13(b)'deki "tam iki satır" ölçütü "tam üç satır: JwtBearer 8.0.11, Security.Secrets proje başvurusu, System.IdentityModel.Tokens.Jwt 8.16.0" olarak okunur. Bunu işçinin raporu ve CT ölçümü doğrular.

## İşçinin bildirdiği iki not (Known gaps)

- `PvgServiceApiHostTests.cs` içindeki `UnsafeResponseSamples` listesinde `"server-tenant-context"` değeri artık hiçbir yerde kurulmuyor; kiracı sızıntısı örneği olarak işlevsiz kaldı. Prompt'un "yalnız bağlam satırları" kısıtı yüzünden bu işte dokunulmadı. Liste, DL4C ya da bir sonraki API işinde güncellenir.
- N5 aktör türünü HTTP üzerinden gözleyemiyor, çünkü `PvgPermissionRequest` aktör türünü taşımıyor. Aktör türü `PvgCaseIntakeRequestContext.From` düzeyinde ölçülüyor. HTTP düzeyindeki N5 yalnız `ActorId`'yi ölçüyor.
