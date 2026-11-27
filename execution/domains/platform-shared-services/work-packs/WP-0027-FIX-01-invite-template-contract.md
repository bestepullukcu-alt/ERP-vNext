# WP-0027-FIX-01 — Davet e-postası eski şablonla boş gidiyor (şablon ↔ gönderim sözleşmesi)

| Alan | Değer |
| :--- | :--- |
| Durum | **draft** |
| Onaylayıcı | Beste (merkezi ekip, Platform sahibi) |
| Açılış | 2026-11-25 — sahip onayı: "evet açalım" |
| Modül | **MOD-0027** Notification Service (Email/SMS/WhatsApp). Ön kontrol: `verify_module_id.py . --check-id MOD-0027 --name "Notification Service (Email/SMS/WhatsApp)"` → `OK … proven against Blueprint/registry` (exit 0, 2026-11-25) |
| Pack | `execution/domains/platform-shared-services/module-packs/MOD-0027-central-tenant-email-notification-service.md` (status `approved`) |
| Servis | `Diten.Platform` (yalnız Notifications alanı + davet üreticisi) |
| Kaynak gözlem | 2026-11-25 canlı (dev): en kiracısına giden davette "TEMPORARY PASSWORD" ve "Login URL" boş, düğme çalışmıyor |
| Kod tabanı | HEAD `c815d95d0` = origin/main `82af5c24a` = MVP-1 hattı `8c32f4d90` (ilgili dosyaların blob'ları aynı) |

## 0. Pack kapsamı (neden MOD-0027)

- Pack §"Primary outcome" + :64 — MOD-0027 bildirim orkestrasyonu, **şablon render'ı**, gönderim kaydı sahibidir.
- Pack :87, :95-96 — `IEmailTemplateRenderer`, `UpdateNotificationTemplateCommand` pack'in nesneleri.
- Pack :478-479 — "Template rendering validates required variables before dispatch queueing"; "Missing required template
  variables produce a controlled validation error". Bugünkü davranış bu AC'nin ruhuna aykırı: sağlanmayan değişken sessizce boş basılıyor.
- Pack Repo Scope :241-255 — `services/Diten.Platform/src/**` (Application, Infrastructure) bu işin tüm dokunduğu yerleri kapsar.
- Davet üreticisi (`AdminUserInvitationService`) MOD-0027-FU04C'nin taşıdığı üreticidir (registry satırı 259, pack
  `MOD-0027-FU04C-tenant-producer-eventcode-migration.md`); aynı modül ailesi, ayrı servis/domain değil.
- `NotificationTemplateSeed` BL-454 (WP-EMAIL-SHELL-01) ile MOD-0027 altında yazıldı (commit `4453ccce8`, `3176391aa`).

## 1. Kök neden — ölçüldü

### 1.1 Dev verisi (2026-11-25, salt okuma, `diten_personalization_dev`)

`notification_templates`, `TemplateKey = tenant.invite.email`, platform varsayılanı (`TenantId = null`), 7 satır:

| Dil | SemanticVersion | Version | UpdatedAt | UpdatedBy | Değişkenler | Shell |
| :--- | :--- | :--- | :--- | :--- | :--- | :--- |
| en, tr | 1.1.0 | 3 | 2026-07-08 06:59 UTC | `null` | RecipientName, TenantDisplayName (zorunlu), Email, TemporaryPassword, LoginUrl (hepsi isteğe bağlı) | yok (tam HTML gövde) |
| fr, es, zh, ar, ru | 1.2.0 | 1 | `null` | `null` | TenantId, TenantDisplayName, SetPasswordUrl, LinkExpiresAtUtc (hepsi zorunlu) | var, `ActionUrlVariable = SetPasswordUrl` |

`audit_events` (EntityType `NotificationTemplate`, en id `179da371…`, tr id `7bcc1159…`): her satır için **iki**
`UpdateNotificationTemplateCommand` (06:56 ve 06:59 UTC, 2026-07-08), `SourceModule = MOD-0027`, aktör
`11111111-1111-1111-1111-111111111111` = dev'in tohumlanmış platform yöneticisi (`PlatformAdministratorSeed.cs:23`,
`AdminAccountSeed.cs:29`). Gövde metni ("Hello {{RecipientName}} … Temporary password: {{TemporaryPassword}} Login: {{LoginUrl}}")
repo geçmişinde hiçbir commit'te yok (`git log --all -S/-G` boş) → API üzerinden elle yazılmış; içerik, `4453ccce8`'de silinen
kod-içi `AdminUserInvitationEmailTemplate.cs`'in değişkenli kopyası. Tarih MOD-0027-FU04A/FU04B kapanış smoke'u ile aynı gün.

`notification_dispatches` (`tenant.invite.email`): test kiracısı `62d31535…` için 4 adet `en / 1.1.0 / Status=1` gönderim;
son en önizlemesi `"… Temporary password:  Login:  Please sign in …"` (iki değer boş). Kiracı dili `es`'e çevrilince tek
`es / 1.2.0` gönderim. Dev'deki 11 kiracı kaydının 10'u en/tr.

### 1.2 Seed neden yükseltmedi — **tasarım gereği, karşılaştırma hatası değil**

- `NotificationTemplateSeed.cs:165-172` `IsUntouchedSeed`: `UpdatedAt is null || UpdatedBy == "system.seed"` şartı. en/tr
  satırlarında `UpdatedAt` dolu, `UpdatedBy = null` → **dokunulmuş** sayılır. Ayrıca içerik ne `TenantInviteV1` (:428) ne
  `TenantInviteV11` (:481) ile eşit; şart bu olmasa da eşleşmezdi.
- `UpdatedBy = null` nedeni: 2026-07-08'de `UpdateNotificationTemplateHandler` kaydı imzalamıyordu; imza (`:83`, `OperatorActor`)
  2026-10-04'te `0fc13fbff` ile geldi. Yani bu bir **operatör düzenlemesi** (paylaşılan dev yönetici hesabıyla, imzasız dönem).
- `:96-110` satırı `KeptModified` sayar ve geçer; tek iz `:45` log satırındaki sayaçtır. Hangi anahtar/dil, neden — söylenmez.

### 1.3 Asıl kusur — gönderim yolunda şablon ↔ üretici sözleşme denetimi yok

1. `AdminUserInvitationService.cs:110-122` 1.2.0 değişkenlerini gönderir (SetPasswordUrl, LinkExpiresAtUtc …);
   `:137` locale = `tenant.DefaultLanguage` → en/tr kiracı en/tr satırını (operatörün 1.1.0'ı) çözer.
2. Olay tanımı `NotificationEventSeeding.cs:284-288` (`tenant.user.invited`) yalnız `TenantDisplayName`'i zorunlu sayar.
3. `EmailTemplateRenderer.cs:16-26` yalnız **şablonun** `IsRequired` değişkenlerini denetler; operatör şablonunda
   TemporaryPassword/LoginUrl isteğe bağlı → denetim geçer.
4. `TemplateTokens.cs:18-20` sağlanmayan `{{token}}` boş metin basar.
5. Şablon davetin tek işlevsel öğesini (SetPasswordUrl) hiç kullanmıyor; buna rağmen gönderim `Sent`, `InviteAsync`
   `InvitationEmailSent: true` döner (`AdminUserInvitationService.cs:99-106`), UI "Davet gönderildi" der.

## 2. Canlı risk

- **Kilitlenen yönetici:** AuthService artık geçici parola üretmiyor (stage D); hesap şifre belirlenmeden kullanılamaz. en/tr
  kiracının yöneticisi **girişi olmayan** bir e-posta alır; operatör başarılı sanır. Operatör "Davet et" yolu mevcut hesabı
  sıfırlar (`AdminUserInvitationService.cs:191-198`) → yeniden davet, var olan yöneticinin parolasını da düşürür ve yine boş posta gider.
- **Kapsam:** dev'de en/tr kiracıların hepsi (11 kaydın 10'u). Temiz bir veritabanında 7 dil 1.2.0 eklenir → **yalnız**
  (a) platform varsayılanını 1.2.0'dan önce düzenlemiş ortamlar ve (b) `tenant.invite.email` kiracı ezmesi (`TenantId != null`,
  seed hiç okumaz) etkilenir. Diğer ortamların verisi bu pakette ölçülmedi (bkz. §8 soru 3).
- **Aynı sınıf:** sözleşmesi değişen her şablon anahtarı (görev, toplantı, zaman girişi) operatör/kiracı düzenlemesi varsa
  aynı sessiz boşluğa düşer.
- **Gizlilik:** sızıntı yok — eski şablonun gizli alanı (TemporaryPassword) hiç gönderilmiyor; SetPasswordUrl şablona girmediği
  için gövdede de yok.

## 3. Çözüm seçenekleri

| # | Seçenek | Artı | Eksi |
| :--- | :--- | :--- | :--- |
| A | **Gönderimde sözleşme kapısı (fail-closed):** üretici "şablonun mutlaka basması gereken" değişkenleri bildirir; çözülen şablon (subject + HTML + text + shell başlık/satır/dipnot + `ActionUrlVariable`) bunların her birine başvurmuyorsa **gönderme**, adlı ret kodu dön; davet yolu bunu operatöre "e-posta gitmedi" olarak söyler | Genel; kiracı ezmesini ve gelecekteki sürüm kaymalarını da yakalar; operatör düzenlemesine dokunmaz | Üreticiye küçük bir alan; yanlış şablon düzelene kadar davet e-postası gitmez (bu istenen: görünür hata > işe yaramaz posta) |
| B | Olayın `RequiredVariables`'ına SetPasswordUrl/LinkExpiresAtUtc eklemek | Tek satır | **Çözmez:** üretici bunları zaten gönderiyor; sorun şablonun basmaması |
| C | Seed, "uyumsuz düzenlenmiş" satırı arşivleyip 1.2.0 eklesin | Kendiliğinden düzelir | Operatör içeriğini sahibin onayı olmadan değiştirir (BL-454 ilkesine aykırı); kiracı ezmesini kapsamaz |
| D | Yalnız dev'de tek seferlik düzeltme (en/tr satırlarını arşivle → seed 1.2.0 ekler) | Hemen | Kök neden kalır; sonraki düzenlemede tekrarlar |

## 4. Önerilen tek çözüm: **A + seed uyarısı + D (dev adımı)**

**F-1 · Sözleşme kapısı (Application).**
- `NotificationEventDispatchRequest` (`NotificationEventDispatchAdapter.cs:20`) ve `QueueEmailNotificationCommand` isteğine
  isteğe bağlı `MustRenderVariables` (varsayılan boş → bugünkü tüm üreticiler aynen çalışır; geriye uyumlu).
- `QueueEmailNotificationHandler`, şablon bulunduktan sonra ve render'dan **önce**: `MustRenderVariables`'taki her ad,
  şablonun subject / HTML / text / shell (heading, info row değerleri, footnote) içinde `{{ad}}` olarak **ya da**
  `Shell.ActionUrlVariable` olarak geçmiyorsa → `Response.Fail(…, 422, ReasonTemplateContractMismatch = "TEMPLATE_CONTRACT_MISMATCH")`;
  **dispatch kaydı oluşmaz, sağlayıcı çağrılmaz**; log: anahtar, dil, şablon id, SemanticVersion, eksik adlar (değer yok).
  Token taraması `TemplateTokens`'ın aynı regex'iyle (tek tanım).
- Dil geri düşüşü (locale resolver) varsa kapı **çözülen** şablona uygulanır; başka dile kaçarak geçmez (bkz. §8 soru 1).

**F-2 · Davet üreticisi.**
- `AdminUserInvitationService.TryQueueInvitationEmailAsync` (`:132-137`) `MustRenderVariables = ["SetPasswordUrl"]` gönderir.
- Ret olduğunda `ReasonCode` kaybolmaz: `(false, null)` yerine kod taşınır; `AdminUserInvitationResult.EmailRefusalCode =
  TEMPLATE_CONTRACT_MISMATCH` (yeni sabit `AdminInvitationRefusals.TemplateOutdated`), `InvitationEmailSent = false`.
- UI zaten `emailSent === false` için uyarı gösteriyor (`details.js` `inviteAdminUser`); yeni kod için `inviteRefusalTexts`'e
  ad ve `TenantsIndex.{en,tr}.resx` anahtarı (`InviteRefusedTemplateOutdated`): "Davet şablonu bu dilde güncel değil; e-posta
  gönderilmedi. Bildirim şablonlarından {dil} şablonunu güncelleyin." (Platform ekranı: 2 dil.)

**F-3 · Seed uyarısı (Infrastructure, davranış değişmez).**
- `UpgradeUntouchedSeedsAsync` `KeptModified` saydığı her satır için (`NotificationTemplateSeed.cs:108`) ayrı bir uyarı satırı
  yazar: `notification.template.seed.kept_modified Key=… Locale=… SemanticVersion=… CurrentSeedVersion=… UpdatedBy=…`.
  Satır **yazılmaz**, içerik korunur.

**F-4 · Dev tek seferlik düzeltme (kod değil; CT/sahip adımı, bu paketin teslimi sonrası).**
- Platform yönetim ekranı (`Views/Platform/NotificationTemplates`) ya da `POST /api/platform/notifications/templates/{id}/archive`
  (`NotificationsController.cs:204`, denetlenen yol, `ArchiveNotificationTemplateHandler`)
  ile en (`179da371…`) ve tr (`7bcc1159…`) satırları **arşivlenir** (`NotificationTemplateRepository.ArchiveAsync`:
  `Status = Archived`, `IsDeleted = true` — yumuşak silme, satır ve operatör içeriği yerinde kalır; komut denetlenir,
  `AuditOperation.Delete`, `SourceModule = MOD-0027`) → Platform yeniden başlar → seed'in ekleme geçişi
  (`NotificationTemplateSeed.cs:22-41`, Active satır yok) 1.2.0 en/tr ekler. Benzersiz indeks
  `ux_notification_templates_active_scope_locale_channel_key` yalnız `Status = Active, IsDeleted = false` satırlarını kapsar
  (dev'de ölçüldü) → ekleme çakışmaz. Doğrulama: 7 dil 1.2.0,
  test kiracısını en'e alıp davet → e-postada "Parolamı belirle" düğmesi ve geçerlilik satırı.
- Doğrudan Mongo yazımı **yapılmaz** (denetim izi kalmaz).

## 5. KORU / YAPMA

- Seed'in "operatör düzenlemesine dokunma" kuralı (BL-454) **değişmez**; seed hiçbir satırı arşivlemez/ezmez.
- `EmailTemplateRenderer`'ın mevcut zorunlu-değişken davranışı, `TemplateTokens`'ın boş basma davranışı (diğer şablonlar için)
  değişmez; kapı yalnız `MustRenderVariables` bildiren üreticide etkilidir.
- FU04B adapter'ının `RequiredVariables` doğrulaması aynen kalır. Olay kataloğu (`NotificationEventSeeding.cs`) değişmez.
- Gizli değer (SetPasswordUrl) hiçbir log, ret mesajı ya da dispatch satırına girmez.
- `ocelot.json`, Auth servisi, başka modül: dokunma.
- **DUR:** locale resolver'ın çözülen şablonu yeniden seçtiği bir yol varsa (kapı bir dili denetleyip başka dili gönderirse) → raporla.

## 6. Kabul ölçütleri (AC)

1. AC-1 — `MustRenderVariables = [SetPasswordUrl]` ve çözülen şablon SetPasswordUrl'e başvurmuyorsa: yanıt 422 +
   `TEMPLATE_CONTRACT_MISMATCH`; `notification_dispatches`'e satır yazılmaz; sağlayıcı çağrılmaz.
2. AC-2 — Şablon SetPasswordUrl'i yalnız `Shell.ActionUrlVariable` olarak kullanıyorsa (1.2.0 biçimi) kapı geçer.
3. AC-3 — `MustRenderVariables` boş/verilmemişse davranış bugünküyle bayt-eşit (mevcut testler yeşil).
4. AC-4 — Davet: ret durumunda `InvitationEmailSent = false`, `EmailRefusalCode = TEMPLATE_CONTRACT_MISMATCH`; UI uyarısı en/tr metniyle.
5. AC-5 — Seed: düzenlenmiş satır içerik/versiyon olarak değişmez, adlı `kept_modified` log satırı yazılır.
6. AC-6 — Kiracı ezmesi (`TenantId != null`) eski şablonsa aynı kapı onu da reddeder.
7. AC-7 (E4, CT) — F-4 sonrası dev'de en ve tr kiracıya davet: düğme + "Bağlantının geçerlilik sonu (UTC)" satırı dolu;
   F-4 öncesi aynı davet: e-posta gitmez, operatöre adlı uyarı.

## 7. Testler ve sabotaj

| Test | Yer | Sabotaj (kırmızı beklenir) |
| :--- | :--- | :--- |
| Eski 1.1.0 (operatör içeriği, TemporaryPassword/LoginUrl) + MustRender SetPasswordUrl → 422, dispatch yok | `Diten.Platform.Application.Tests/Notifications/` (yeni `TemplateContractGateTests`) | Kapıdaki kontrolü kaldır |
| 1.2.0 shell (ActionUrlVariable) → geçer | aynı | Taramayı yalnız gövdeye daralt (shell'i atla) → kırmızı |
| MustRender boş → mevcut akış aynı | aynı + mevcut `EmailShellDispatchTests` | — |
| Davet servisi ret kodunu taşıyor | `AdminUserInvitationService` testleri | Kodu `(false, null)`'a geri al |
| Seed kept_modified log + satır değişmedi (Mongo) | `NotificationTemplateSeedUpgradeMongoTests` | Log çağrısını sil |
| Kiracı ezmesi reddi | `TemplateContractGateTests` | Kapıyı `IsPlatformDefault` ile sınırla |
| UI: yeni ret kodu en/tr metni | `frontend/Diten.Web/tests/` (jsdom, gerçek `details.js`) | `inviteRefusalTexts` satırını sil |
| resx eşliği en/tr | mevcut L10n eşlik testi | tr anahtarını sil |

Her sabotaj: kırmızı → bayt-eşit geri al → yeşil. Platform testleri filtresiz (makine geneli Mongo kilidi: aynı anda iki
Platform koşusu yok); Web testleri filtresiz.

## 8. Açık sorular (sahip: Beste)

1. Locale geri düşüşü: en şablonu kapıya takılırsa **başka dile düşüp göndermek** mi, **hiç göndermemek** mi? Bugünkü
   `QueueEmailNotificationHandler.FindTemplateAsync` yalnız şablon **bulunamazsa** düşer; öneri bunu korumak: göndermemek (operatör görür, düzeltir).
2. `MustRenderVariables` üretici isteğinde mi (öneri, geriye uyumlu, küçük) yoksa olay kataloğunda (`NotificationEventSeedDefinition`, MOD-0027-FU03A sözleşme değişikliği) mı tutulsun?
3. Dev dışındaki ortamlarda (varsa test/canlı) `tenant.invite.email` için `UpdatedAt` dolu platform satırı ya da kiracı ezmesi var mı? CT salt-okuma sayımı ister.
4. F-4 dev adımını kim yapar (CT mı, sahip mi) ve bu FIX'in birleşmesinden **önce** mi yapılsın? Öneri: önce yapılabilir (davetleri hemen düzeltir), kapı yine de gelir.
5. Kapının ikinci kuralı — "şablonun bastığı ama üreticinin göndermediği token" → ret — genel olarak açılsın mı? Diğer şablonlarda meşru isteğe bağlı token'lar var; önce ölçüm (takip: `WP-0027-FIX-02` adayı), bu pakette yok.
