# MOD-0018 ROLE1 — betikler (WP-0018-ROLE1)

Kanıt: `mod-0018-role1-evidence-2026-10-10.md`. Betikler repoda tutulmuyor; tekrar üretilebilsin diye burada aynen duruyor. Hiçbiri
sahibin filosuna (4010, 5000, 5001, 5056–5061, 5066) ya da 27017'ye bağlanmaz.

## 1. `sabotage.js` — K3 sabotajları (a)–(d)

Kullanım: `node sabotage.js <worktree> <a..d> apply|restore`.

Her koşu:
1. Sabotajı uygula.
2. AuthService Application test projesini derle; derleme hatası sayısını kontrol et (0 olmalı).
3. Şu filtreyle koş (17 test): `--filter "FullyQualifiedName~DefaultRoleTemplateOnceTests|FullyQualifiedName~RoleProvisioningServiceTests"`.
   `DITEN_ITEST_MONGOD_BIN_DIR` ayarlı olmalı.
4. `restore` yap.

Son adım: testler yeniden yeşil koşulur ve `git diff` özeti sabotaj öncesiyle karşılaştırılır.

```js
// WP-0018-ROLE1 K3 sabotages: node sabotage.js <worktree> <a..d> apply|restore. One textual change each (the anchor must occur
// exactly once); restore writes the backup back byte for byte.
const fs = require('fs');
const path = require('path');
const [wt, which, mode] = process.argv.slice(2);
const S = 'services/Diten.AuthService/src/';
const service = S + 'Diten.AuthService.Application/Common/Services/RoleProvisioningService.cs';
const plan = {
  // (a) tenant-admin-invited completes existing roles again (the old "add what is missing" path)
  a: [service, 'DefaultRoleProvisioningRule.GrantsFor(role.Name, catalog, created)',
      'DefaultRoleProvisioningRule.GrantsFor(role.Name, catalog, created || trigger == DefaultRoleProvisioningTriggers.TenantAdminInvited)'],
  // (b) the same for tenant-activated
  b: [service, 'DefaultRoleProvisioningRule.GrantsFor(role.Name, catalog, created)',
      'DefaultRoleProvisioningRule.GrantsFor(role.Name, catalog, created || trigger == DefaultRoleProvisioningTriggers.TenantActivated)'],
  // (c) every seed start-up completes the seed tenant's roles again
  c: [S + 'Diten.AuthService.Persistence/Seed/DataSeeder.cs', 'DefaultRoleProvisioningRule.GrantsFor(role.Name, catalog, created)',
      'DefaultRoleProvisioningRule.GrantsFor(role.Name, catalog, true)'],
  // (d) the "is it new" decision always says new
  d: [S + 'Diten.AuthService.Domain/Authorization/DefaultRoleProvisioningRule.cs',
      '=> roleCreatedByThisCall ? DefaultRolePermissionTemplate.SelectFor(roleName, catalog) : [];',
      '=> true ? DefaultRolePermissionTemplate.SelectFor(roleName, catalog) : [];']
};
const [rel, from, to] = plan[which];
const file = path.join(wt, rel);
const backup = path.join(__dirname, `sab-${which}.bak`);
if (mode === 'apply') {
  const raw = fs.readFileSync(file);
  fs.writeFileSync(backup, raw);
  const t = raw.toString('utf8');
  if (t.split(from).length !== 2) throw new Error('anchor not found exactly once for ' + which);
  fs.writeFileSync(file, t.split(from).join(to));
  console.log(`applied ${which} to ${path.basename(file)}`);
} else {
  fs.writeFileSync(file, fs.readFileSync(backup));
  fs.unlinkSync(backup);
  console.log(`restored ${which}`);
}
```

## 2. Yoklama (`probe/Program.cs` + `platform-stub.js`, scratchpad)

- Kendi EphemeralMongo replica set'ini açar; 27017 reddedilir.
- Kendi AuthService'ini 15056'da başlatır (Release, bu dal; `--urls` + `ASPNETCORE_URLS` + Kestrel).
- Rastgele JWT sırrı ve iç anahtar kullanır.
- Platform yerine yalnız kiracı giriş ayarlarını cevaplayan bir taklit çalıştırır (14057).

Adımlar:
1. İlk açılış: tohum denetimi ölçülür.
2. Yeni kiracı için `tenant-activated` gönderilir.
3. Bir Viewer anahtarı silinir (yöneticinin rol ekranında yaptığı işlemle aynı). `tenant-admin-invited` gönderilir, ardından ikinci `tenant-activated`.
4. Tohum Viewer'dan bir anahtar ve kiracı Admin'inden `auth.users.lookup` silinir. AuthService yeniden başlatılır.
5. Rol, grant ve denetim belgeleri okunur (`db-read.json`). Sonuçlar `probe-results.json`'a yazılır.

Davet yanıtındaki geçici şifre okunmadan atılır. Yoklama yalnız kendi PID'lerini kapatır.

```js
// WP-0018-ROLE1 probe: the ONE Platform read the invitation needs (tenant login settings). Everything else answers 404.
require('http').createServer((q, r) => {
  r.setHeader('content-type', 'application/json');
  const m = q.url.match(/^\/api\/internal\/tenants\/([0-9a-f-]{36})\/login-settings$/);
  if (!m) { r.statusCode = 404; r.end('{}'); return; }
  r.end(JSON.stringify({ succeeded: true, message: null, data: {
    tenantId: m[1], twoFactorEnabled: false, mfaRequired: false, emailLoginEnabled: true, phoneLoginEnabled: false, passwordMinLength: 12,
    passwordRequireUppercase: true, passwordRequireLowercase: true, passwordRequireDigit: true, passwordRequireSpecialChar: true,
    passwordExpirationDays: null, sessionTimeoutMinutes: 60, refreshTokenLifetimeDays: 7, maxFailedLoginAttempts: 5, lockoutDurationMinutes: 15 } }));
}).listen(14057, '127.0.0.1');
```
