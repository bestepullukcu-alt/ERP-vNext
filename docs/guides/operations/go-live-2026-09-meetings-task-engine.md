# Canlıya geçiş — Toplantılar + Görev motoru + Auth etiket okuma (Eylül 2026)

> Kime: canlıya alacak ekip ve yönetici. Ne işe yarar: bu turun main'e birleştirme sırasını, canlıdan önce
> yapılması gereken ayarları ve canlıdan sonra koşulacak kısa kontrolü tek yerde toplar.
> Hazırlayan: CONTROL TOWER, 2026-09-14. Değerler ölçüldü; ölçülmeyen yerde "ölçülmedi" yazar.

## 0. Bu turda ne var

| PR (sırayla) | Dal | İçerik |
| :-- | :-- | :-- |
| 1 | `feature/infra/auth-display-label` | Kullanıcı kimliğinden görünen ad okuma ucu; CI kapısını 2026-08-30'dan beri kırmızı tutan iki eski testin düzeltmesi (BL-393) |
| 2 | `feature/pss/mod-0024-review-meeting-policy` | Görev motoru: kapanış alanları, dosya ekleri, çıktı dosyası zorunlu tür, atama kapsamı güvenliği, görev detayını yalnız ilgili kişinin açması (BL-349), görev türü eşzamanlılığı (BL-375), inceleme toplantısı politikası, görev yorumlarında @ ile etiketleme |
| 3 | `feature/mg/mod-0357-management-review-cadence` | Toplantılar (MOD-0357): kayıt, ekranlar, görev köprüsü, davet + kabul/ret, `.ics`, tutanak, devam toplantısı, türler, seri toplantı; Görev Merkezi davet kartı; e-posta tekrar denemesi düzeltmeleri (BL-374, BL-385); çıkarılan katılımcıya posta (BL-386), terim, bilinmeyen kullanıcı ve tarih alanı düzeltmeleri |

## 1. Birleştirme

- Sıra **1 → 2 → 3**. Her PR **normal birleştirme (merge commit)** ile alınır, squash ile değil: 3 numara 2'yi, 2 numara 1'i
  içerir; squash aynı işi iki kez gösterir ve yeniden çakıştırır.
- Her PR'da `phase1-gates` yeşil olmalı. Yerel ölçüm (2026-09-13, temiz checkout): üç dalda da kiracı 3/3, mimari 18/18,
  Web 137/159/159, "gates passed".
- ⚠ Kapıyı yerelde **ana checkout'ta koşmayın**: içindeki `.claude/worktrees/` kopyaları mimari testlerde sahte kırmızı
  üretir (BL-383). Ayrı bir worktree ya da temiz kopya kullanın.
- Main bu arada ilerlerse: main'i 1'e, 1'i 2'ye, 2'yi 3'e alıp kapıyı yeniden koşun.

## 2. Canlıdan önce: yedek

- Platform veritabanının ve Hangfire deposunun (`BackgroundJobs:StorageDatabaseName`) yedeği alınır.
- **Geri alma yerine ileri düzeltme.** Eski sürüm, yeni sürümün yazdığı alanları okurken çökebilir (BL-384: 2026-09-13'te
  dev'de `RecordLink.IdempotencyKey` ile yaşandı). Bu turla gelen bilinmeyen alan toleransı (`4663682c`) yalnız **bu sürümden
  sonraki** geri almaları korur; bugünkü canlı sürüm bu toleransa sahip değildir, ona geri dönüş yine çöker.

## 3. Arka plan işleri (Hangfire)

`appsettings.json` varsayılanı: `BackgroundJobs:Enabled = false`, `RegisterStandardJobs = true`, `EnabledJobs = {}`.
Kapalı kalırsa aşağıdaki "bu tur için gerekli" işler **hiç çalışmaz**; hata vermez, sessizce durur.

| İş kimliği | Zamanlama | Bu tur için |
| :-- | :-- | :-- |
| `Diten.Platform.MOD-0027.EmailDispatchJob` | her dakika | **Gerekli** — başarısız e-postanın tekrar denemesi |
| `Diten.Platform.MOD-0357.MeetingSeriesSweepJob` | saatte bir | **Gerekli** — seri toplantı üretimi |
| `Diten.Platform.MOD-0024.TaskRecurrenceSweepJob` | saatte bir | **Gerekli** — tekrarlayan görevler |
| `Diten.Platform.MOD-0024.TaskDueSoonSweepJob` | saatte bir | **Gerekli** — "süresi yaklaşıyor" bildirimi |
| `Diten.Platform.MOD-0023.WorkflowEscalationSweepJob` | 5 dakikada bir | Bu turun konusu değil |
| `Diten.Platform.MOD-0297.TrialExpiryScanJob` · `SubscriptionRenewalJob` | günlük | Bu turun konusu değil |
| `Diten.Platform.MOD-0033.QuotaResetJob` · `MOD-0018.EntitlementCacheRefreshJob` · `MOD-0034.WebhookRetryJob` · `MOD-0021.AuditLogArchiveJob` · `MOD-0009.ProvisioningRetryJob` | çeşitli | Bu turun konusu değil |

Açmak için: `BackgroundJobs:Enabled = true` ve her iş için `BackgroundJobs:EnabledJobs:<iş kimliği> = true`.
Kapatılan bir iş Hangfire'dan artık siliniyor (`RemoveIfExists`), yani bayrağı geri almak işi gerçekten durdurur.
Dashboard canlıda kapalı kalır.

## 4. E-posta

- Gönderim `TenantMessagingSettings` platform varsayılan satırından çözülür; satır başlangıçta `Smtp` bloğundan türetilir.
- SMTP sağlayıcısı **her gönderimde kimlik doğrular**. Sunucu AUTH sunmuyorsa her posta `ProviderUnknown` ile düşer ve
  toplantı yine oluşur (posta hatası kaydı geri almaz) — kutu boş kalır, ekran sağlıklı görünür.
- Canlıdan sonra bir gerçek davetle doğrulanır (bkz. §7).

## 5. İzinler

- Bu turda main'e yeni giren izin anahtarları (ölçüldü): `platform.meetings.series-manage`, `platform.tasks.read-all`.
  Diğer `platform.meetings.*` anahtarları main'de zaten var.
- `platform.tasks.read-all` **yalnız açıkça verilir**: hiçbir varsayılan role, SuperAdmin'e ya da modül yetkilendirmesiyle
  otomatik gitmez. Vermediğiniz sürece herkes yalnız ilişkili olduğu görevleri açar.
- **Kiracı rolleri görev ve toplantı izinlerini kendiliğinden almaz (BL-410, dev'de ölçüldü).** Yeni kiracıya yalnız Admin ve Viewer
  kurulur; Admin'in hazır listesinde `platform.tasks.*` ve `platform.meetings.*` yok. TASKS, MEETINGS, WORK-AGGREGATION ve
  WORK-REPORT modülleri hiçbir abonelik planında yok. **Canlı kiracıya bu dört modül açıkça yetkilendirilir**; eşitleme o zaman
  Admin'e tüm anahtarları, Viewer'a okuma anahtarlarını verir. Yapılmazsa kiracıda kimse görev ve toplantı ekranlarını kullanamaz.
- **Viewer Görev Merkezi'ni açamaz:** `platform.work-aggregation.inbox.view` anahtarının eylemi `view`, Viewer yalnız `read`
  eylemlerini alır. Görev Merkezi'ni kullanacak her rol bu anahtarı almalı (sahip kararı, BL-410).
- **İki şablon ekranı kiracıya verilemiyor olabilir (BL-411, dev'de ölçüldü).** `platform.tasks.checklist-templates.manage` ve
  `platform.tasks.templates.manage` dev Auth kataloğunda "yalnız platform" kapsamında (`Scope: 1`): hiçbir kiracı rolüne
  atanamaz, elle atama 403 döner. Canlı kataloğu önce salt okunur ölç:
  `db.permissions.find({Key:{$in:["platform.tasks.checklist-templates.manage","platform.tasks.templates.manage"]}},{Key:1,Scope:1})`.
  `Scope: 1` çıkarsa kontrol listesi şablonları ve görev şablonları ekranları kiracıda kullanılamaz; düzeltme Auth veri adımı ister.
- Dev'deki "Task-Manager" ve "Task-User" rolleri 2026-09-08'de elle açılmış test rolleridir (BL-403); canlıda yoklar.
- `platform.tasks.work-report.read-tenant-wide` da artık **yalnız açıkça verilir** (BL-392, `aa96b147`). Bugün bu izni otomatik tutan roller
  kendiliğinden kaybetmez ama "açıkça verilmiş" hale çevrilmeleri bir veri adımı ister (sahip kararı). Canlıdan önce sahipleri
  listelemek için salt okunur sorgu (yazma yok; `AUTH_DB` canlı Auth veritabanı adına ayarlanır):

```js
// BL-392 pre-deploy holder listing — READ-ONLY (findOne/aggregate only; no writes).
// Run against the AuthService database. appsettings.json default name is diten_auth_v3 — use the DEPLOYED name.
// Lists, per tenant, every role that currently holds platform.tasks.work-report.read-tenant-wide and the grant
// source (stored int: 0=System, 1=Module, 2=Manual; a row without the field is legacy and reads as System).
const AUTH_DB = "diten_auth_v3";
const KEY = "platform.tasks.work-report.read-tenant-wide";
const authDb = db.getSiblingDB(AUTH_DB);

const perm = authDb.permissions.findOne(
  { Key: KEY },
  { _id: 1, Key: 1, Module: 1, Scope: 1, IsSystem: 1, IsDeleted: 1 });

if (!perm) {
  print(`Not in the catalog: ${KEY} — no role holds it.`);
} else {
  printjson({ permission: perm });
  const rows = authDb.rolePermissions.aggregate([
    { $match: { PermissionId: perm._id, IsDeleted: { $ne: true } } },
    { $lookup: { from: "roles", localField: "RoleId", foreignField: "_id", as: "role" } },
    { $unwind: { path: "$role", preserveNullAndEmptyArrays: true } },
    { $lookup: {
        from: "userRoles",
        let: { rid: "$RoleId", tid: "$TenantId" },
        pipeline: [
          { $match: { $expr: { $and: [ { $eq: ["$RoleId", "$$rid"] }, { $eq: ["$TenantId", "$$tid"] } ] },
                      IsDeleted: { $ne: true } } },
          { $lookup: { from: "users", localField: "UserId", foreignField: "_id", as: "user" } },
          { $match: { "user.0": { $exists: true }, "user.IsDeleted": { $ne: true } } },
          { $count: "n" }
        ],
        as: "activeHolders" } },
    { $project: {
        _id: 0,
        TenantId: 1,
        RoleId: 1,
        RoleName: { $ifNull: ["$role.Name", "(role missing)"] },
        RoleIsSystem: "$role.IsSystem",
        RoleIsDeleted: "$role.IsDeleted",
        GrantSource: { $switch: {
          branches: [
            { case: { $eq: ["$GrantSource", 1] }, then: "Module" },
            { case: { $eq: ["$GrantSource", 2] }, then: "Manual" }
          ],
          default: "System" } },
        SourceModuleCode: 1,
        AssignedBy: 1,
        AssignedAt: 1,
        ActiveUsersHoldingRole: { $ifNull: [ { $arrayElemAt: ["$activeHolders.n", 0] }, 0 ] }
    } },
    { $sort: { TenantId: 1, RoleName: 1 } }
  ]).toArray();

  print(`${rows.length} role grant(s) of ${KEY}:`);
  rows.forEach(r => printjson(r));

  const byTenant = {};
  rows.forEach(r => {
    const t = String(r.TenantId);
    (byTenant[t] = byTenant[t] || []).push(
      `${r.RoleName} [${r.GrantSource}${r.SourceModuleCode ? ":" + r.SourceModuleCode : ""}] activeUsers=${r.ActiveUsersHoldingRole}`);
  });
  printjson(byTenant);
}
```

**Aynı sorgu `platform.tasks.read-all` için de koşulur** (`KEY` değiştirilerek). Dev'de SuperAdmin bu anahtarı kural gelmeden
önceki Auth sürümünden otomatik almış (System, 2026-09-13). Yeni Auth sürümü var olan satırı silmez; kimde kalacağı BL-392 ile aynı
yöntemle (liste → kiracı yöneticisi onayı) belirlenir.

## 6. Organizasyon verisi

- Toplantı düzenleyen, katılımcı olan ya da başkasına görev atayan her kullanıcının **aktif bir pozisyonu** olmalı.
  Yoksa: katılımcı seçicisi boş, `MEETING_ORGANIZER_INVALID`, başkasına görev verilemez.
- Kiracının **aktif bir kök birimi** olmalı; yoksa görev açılamaz (`ORGANIZATION_UNIT_UNRESOLVED`). Yeni kiracı kurulumu bu
  birimi oluşturmuyor ve oluşturamaz: birim bir tüzel kişiliğe bağlı olmak zorunda (BL-366). Sıra: MDM'de tüzel kişilik oluştur ve
  aktifleştir → Organizasyon ekranından kök birim → pozisyonlar → atamalar.

## 7. Canlıdan sonra: 20 dakikalık kontrol

1. Giriş → Görev Merkezi açılır; "bazı kaynaklar yanıt vermedi" uyarısı yok.
2. Dosya ekiyle görev oluştur → görevi başlat → kapat: kapanış alanları görünür, değer görev detayında durur.
3. Görevden "İnceleme toplantısı planla" → toplantı oluşur, eylem "zaten planlandı" diye kapanır.
4. İkinci bir kullanıcıyla toplantı oluştur → davet postası `.ics` ekiyle gelir.
5. İkinci kullanıcı Görev Merkezi'nde daveti kabul eder → düzenleyen cevabı görür.
6. Toplantı saatini değiştir → güncelleme postası (aynı UID, daha yüksek SEQUENCE); iptal et → iptal postası.
7. Görevle ilgisi olmayan bir kullanıcı görevin adresini açar → "bulunamadı".
8. Görev yorumunda @ ile görevi gören birini etiketle → uygulama içi bildirim + e-posta; görevi görmeyen biri listede çıkmaz.
9. Toplantıdan bir katılımcıyı çıkar → yalnız ona "toplantıdan çıkarıldınız" postası; takviminden etkinlik kalkar.
10. Başka bir kullanıcı adına toplantı oluştur ve düzenleyeni değiştir → düzenleyen "takviminize eklendi" postasını alır (düz davet değil); düzenleyen kendi toplantısını değiştirince ona posta gitmez.
12. Toplantı raporu: `/Meetings/Report` → dönem seç → toplantılar, kararlar ve aksiyonlar dolar; bir indirme `audit_events`'te tek bir TenantUser DataExport satırı bırakır.
11. **Sağlık uçları — canlıdan ÖNCE teyit (BL-404).** `/health` ve `/health/ready` İş Referans Verisi sağlayıcı kontrolünü içerir;
    `BusinessReferenceData:Provider:ReferenceTenantId` ayarlanmamışsa ikisi de 503 döner (dev'de ölçüldü, kodda "pilot yoksa sağlıklı"
    kontrolünden önce bu ayar isteniyor). Bu uçları yoklayan bir yük dengeleyici bütün Platform'u servis dışı sayar; `/health/live`
    etkilenmez. Ya ortama `BusinessReferenceData__Provider__ReferenceTenantId` verilir (değer İş Referans Verisi sahibinden) ya da
    dengeleyicinin `/health/live` kullandığı teyit edilir. Canlıdan sonra üç ucun cevabı okunur.

## 8. Bilinen açıklar (bu turu engellemez)

| Kayıt | Konu |
| :-- | :-- |
| BL-388 | Görev alanı tanımında "Sıra" boşken kaydın düşmesi bu turda düzeltildi (`0d551337`); görev ayar ekranları `fddc01a6` ile düzeldi; aynı ham hata metni 17 ekranda (CRM, Roles, Platform) duruyor (BL-398) |
| BL-391 kalanı | Doğrudan tarih seçici kullanan 14 ekranda yanlış biçimde yazılan tarih hâlâ sessizce kayabilir |
| BL-414 | Görev bildirimleri (etiketleme dahil) eski /Tasks/{id} sayfasına götürüyor; o sayfada yorumlar yok — düzeltme sırada |
| BL-392 | İş Raporu kiracı geneli okuma izni artık yalnız açıkça verilir; bugün tutan roller §5'teki sorguyla listelenip kiracı yöneticisine onaylatılır (sahip kararı b) |
| BL-409 | Genel komut denetim hattı aktör türünü "Sistem" yazıyor; kullanıcı kimliği kayıtta var, türü yanlış — düzeltme yapılıyor |
| BL-411 | Kontrol listesi şablonları ve görev şablonları izinleri "yalnız platform" kapsamında olabilir (§5'teki ölçüm) |
| BL-412 | Auth'ta role izin atama kaydı `AssignedBy: "System"` yazıyor; atamayı yapan gerçek kişi yalnız Auth denetim günlüğünde |
| BL-393 notu | Tam Platform/Auth test paketleri ve ön yüz testleri CI'da koşmuyor; main'de önceden kırmızı olanlar: Doküman Yönetimi 15, İş Referans Verisi 53, Auth 3, ön yüz 25 |
