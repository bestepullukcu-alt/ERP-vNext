# Draft — "Toplu geri alma" section for the Q358 / G7 runbook (asked by Ali, 2026-10-21)

> Target: `docs/guides/operations/auth-baseline-grant-retraction-runbook.md` — a **protected path**; CT cannot edit it.
> Insert the block below **between** the "## Geri dönüş" bullets and "## Sık görülen durumlar", and change the last
> bullet's "ayrı bir iş olarak yapılır" to "aşağıdaki adımlarla yapılır". Measured on `origin/main` `9d44e8a1f`
> (runner `Q358ShipmentGrantRetractionRunner.cs:136-169,186-224`; `RolePermission.GrantSource` is an int enum, System = 0,
> Module = 1; Auth stores Guids as standard UUIDs; `auth_role_assignment_versions` has `_id` = TenantId, `Version`).
> **Not run** — test the script on a non-prod copy (DryRun first) before production.

---

### Toplu geri alma (Apply'ı geri çevirmek)

**Ne zaman:** Apply sonrası yetkili bir kullanıcının erişimi beklenmedik şekilde kalktıysa ve tek tek entitlement ile
düzeltmek yetmiyorsa. **Karar:** ürün sahibinin yazılı onayı (kim, ne zaman, hangi kiracılar). Geri alma, Apply'ın
yazdığı her değişikliği **"önce" hâline** döndürür ve yalnız hâlâ Apply'ın bıraktığı durumda duran grant'lara dokunur.

| Apply'da | Denetim satırı (`EventName`) | Geri almada |
|---|---|---|
| Module'e çevrildi | `role_permission_source_corrected` | `GrantSource` tekrar **System** (`0`), `SourceModuleCode` = null |
| soft-delete edildi | `role_permission_revoked` | `IsDeleted` = false (ve varsa `DeletedAt` kaldırılır) |
| değişmedi | `role_permission_retraction_not_applied` | hiçbir şey (grant zaten dokunulmamış) |

**Dokunmaz:** Apply'dan sonra bir yöneticinin ya da entitlement senkronunun değiştirdiği grant (koruma filtresi
eşleşmez, satır "SKIP" olur); tanım dışı anahtarlar; Viewer, SuperAdmin ve özel roller.

**Ön koşullar:** (1) `Q358Retraction__Mode` / `G7Retraction__Mode` ortamda **yok** (Auth başlarken yeniden çalışmasın);
(2) Auth veritabanının yedeği alındı; (3) sahibin onayı yazılı.

**1. Planı gör (DryRun — hiçbir şey yazmaz).** `mongosh`, Auth veritabanında. `SRC` = `q358-shipment-grant-retraction`
(G7 için `g7-sandop-capacity-grant-retraction`). Yalnız bazı kiracılar geri alınacaksa `TENANTS`'a kimliklerini yazın;
boş liste = hepsi.

```
const SRC = "q358-shipment-grant-retraction";
const TENANTS = [];              // örn. ["5f2c1e0a-…"]; boş = bütün kiracılar
const APPLY = false;             // DryRun. Plan doğruysa true yapıp tekrar çalıştırın.
const ACTOR = "q358-rollback";   // UpdatedBy değeri

const want = new Set(TENANTS.map(t => UUID(t).toString("hex")));
const rows = db.authAuditLogs.find({ Metadata: { $regex: SRC },
    EventName: { $in: ["role_permission_revoked", "role_permission_source_corrected"] } }).toArray()
  .filter(r => want.size === 0 || want.has(r.TenantId.toString("hex")));
let planned = 0, reverted = 0, skipped = 0; const touched = new Map();
for (const r of rows) {
  const m = JSON.parse(r.Metadata);
  const revoked = r.EventName === "role_permission_revoked";
  const guard = revoked
    ? { _id: UUID(m.grantId), IsDeleted: true, GrantSource: 0 }
    : { _id: UUID(m.grantId), IsDeleted: false, GrantSource: 1, SourceModuleCode: m.after.sourceModuleCode };
  const change = revoked
    ? { $set: { IsDeleted: false, UpdatedBy: ACTOR }, $unset: { DeletedAt: "" } }
    : { $set: { GrantSource: 0, SourceModuleCode: null, UpdatedBy: ACTOR } };
  if (db.rolePermissions.countDocuments(guard) !== 1) {
    skipped++; print(`SKIP (changed since Apply)  ${m.permissionKey}  grant ${m.grantId}`); continue;
  }
  planned++; print(`${APPLY ? "REVERT" : "WOULD REVERT"}  ${m.permissionKey}  grant ${m.grantId}`);
  if (APPLY && db.rolePermissions.updateOne(guard, change).modifiedCount === 1) {
    reverted++; touched.set(r.TenantId.toString("hex"), r.TenantId);
  }
}
if (APPLY) for (const t of touched.values())
  db.auth_role_assignment_versions.updateOne({ _id: t }, { $inc: { Version: 1 } }, { upsert: true });
print(`planned: ${planned}, reverted: ${reverted}, skipped: ${skipped}, tenants bumped: ${touched.size}`);
```

Kontrol edin: `skipped` beklenen mi (yönetici sonradan değiştirdiyse doğaldır), satırlar doğru kiracılar mı.

**2. Uygula.** `APPLY = true` yapıp aynı betiği bir kez çalıştırın. Değişen her kiracının rol-atama sürümü bir artırılır
(`auth_role_assignment_versions.Version`); böylece token ve yetki önbellekleri yenilenir — Apply'ın yaptığının aynısı.
`reverted` = DryRun'daki `planned` olmalı; fark varsa arada biri grant'ı değiştirmiştir (o satır SKIP kalır).

**3. Doğrula.** Betiği `APPLY = false` ile tekrar çalıştırın: her satır `SKIP` olmalı (geri alınacak bir şey kalmadı).
Etkilenen bir kullanıcıyla ekranı açın: erişim geri gelmiş olmalı.

**4. Kaydet.** Kim onayladı, hangi kiracılar, `planned` / `reverted` / `skipped` ve betik çıktısı bir karar/denetim
kaydına (`docs/records/…`) yazılır. `authAuditLogs` elle yazılmaz. Geri alınan grant'lar yeniden System kaynaklı
olduğundan, Q358/G7 çalıştırıcısı onları yine aday görür — ayarı yeniden açmadan önce sahip kararı gerekir.
