# Denetim defteri — Diten.AuthService

Kural: `.antigravity/rules/audit-trail-standard.md` (AUD-001) · Ölçen test: `AuditTrailStandardTests`

Bu dosyada yalnız **bildirim** durur; bir komutun denetlenip denetlenmediği buradan değil, `services/Diten.AuthService/src` altındaki
üretim kodundan okunur. Biçim ve bölümlerin anlamı: [README.md](README.md).

## İzler

| iz | yol | tür | belirteç |
|---|---|---|---|
| auth-kullanici-iletimi | b | yazıcı | IUserAuditRecorder |
| auth-gunlugu | aday | yazıcı | IAuthAuditService |
| auth-rbac-gunlugu | aday | yazıcı | IRbacAuditRecorder |

Ölçüm notları (2026-10-02; `yol = aday` kabul edilmemiş demektir ve o ize giden komut borçta kalır):

* **auth-kullanici-iletimi** — `authAuditLogs` + `IPlatformAuditForwarder` ile merkezi günlüğe. En iyi çaba: iletim hatası yutulur.
* **auth-gunlugu** — `authAuditLogs`: yazılıyor, güncelleme/silme yok — ama okuyan uç YOK; eşdeğer iz kabul koşulu (kural §5.c-3) sağlanmıyor.
* **auth-rbac-gunlugu** — `authAuditLogs` (rol/izin değişiklikleri): aynı gerekçe — okuyan uç yok.

## İstisnalar

| komut | sınıf | gerekçe |
|---|---|---|
| IssueServiceIdentityTokenCommand | İ5 | Hiçbir kalıcı iş kaydını değiştirmez: bir servis istemcisine kısa ömürlü belirteç verir ya da reddeder; verilen ve reddedilen her istek (istemci, kiracı, hedef kitle, UTC gün; ret için sonuç kodu) güvenlik olayı olarak `authAuditLogs`'a yazılır ve verilen belirtecin kaydı yazılamazsa belirteç verilmez. |

## Dolaylı

| komut | iz | üzerinden |
|---|---|---|

## Bilinen borç

Bu liste **yalnız küçülür**. Satır eklemek yasaktır; denetlenen ya da silinen komutun satırı çıkarılır (test bunu zorlar).

- AssignPermissionCommand
- ChangePasswordCommand
- CreatePermissionCommand
- CreateRoleCommand
- DeletePermissionCommand
- DeleteRoleCommand
- ForcedChangeTenantPasswordCommand
- LoginCommand
- LogoutCommand
- PlatformLoginCommand
- RefreshTokenCommand
- RegisterCommand
- ResendMfaCommand
- RevokePermissionCommand
- SetAccountKindCommand
- SetTenantPasswordCommand
- UpdateRoleCommand
- VerifyMfaCommand
