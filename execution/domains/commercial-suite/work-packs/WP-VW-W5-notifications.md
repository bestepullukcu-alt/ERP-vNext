# WP-VW-W5 — Ziyaret bildirimleri (e-posta): rapor özeti, advers olay, endikasyon dışı talep, rapor süresi doldu

> **Durum:** paketlendi 2026-10-19 (CT). **Yol haritası:** [ROADMAP-visit-workspace](ROADMAP-visit-workspace.md) W5 · K-W8 (açık) · K-W9 (karar).
> **Tasarım:** [06-epostalar.html](mockups/visit-workspace/decoded/06-epostalar.html), ziyaret çalışma alanı mockup'ı. E-posta şablonları, alıcılar ve "E-postalar" listesi orada çizili. Sipariş e-postaları (`sip_*`) W6'ya aittir.
> **Sıra:** PLT-NOTIFY ∥ W5-BE (sözleşme bu belgede sabit) → W5-WEB.

## Kararlar

| # | Karar | Kaynak |
|---|---|---|
| K-N1 | **Gönderim yalnız Platform Notifications'tan.** CRM SMTP kullanmaz. Platform'a yeni bir iç uç eklenir: `POST api/internal/notifications/events/dispatch`, `X-Internal-Api-Key` ile, Platform'un mevcut iç uç deseni. Böylece istek bağlamı olmayan zamanlayıcı da gönderebilir. Bugün kiracı ya da iç servis çağırabilecek bir uç yok; `api/platform/notifications` yalnız platform yöneticisine açık | CT ölçümü 2026-10-19 |
| K-N2 | **Alıcı Platform'da çözülür (K-W9).** "X kullanıcısının yöneticisi": birincil koltuk → `ReportsToPositionId` → pozisyonu tutanlar (kişinin kendisi hariç). Koltuk boşsa bir üst pozisyona çıkılır (döngü koruması). Mevcut `ApproverResolver`'ın zincir yürüyüşü paylaşılan `ILineManagerResolver`'a çıkarılır. Zaman çizelgesinin "zaman yöneticisi havuzu" yedeği yalnız timesheet'te kalır; bildirimde yedek yoktur. Yönetici bulunamazsa e-posta gönderilmez, kayıt `no-recipient` olur. E-posta adresi ve dil `ITaskNotificationRecipientResolver`'dan gelir (Auth). CRM e-posta adresi tutmaz | K-W9, CT |
| K-N3 | **Olay kataloğu Platform'da.** CRM olayları Platform'daki `CrmManifestProvider.NotificationEvents`'e yazılır; uzaktan kayıt bildirim kataloğunu beslemez. Şablonlar Platform tohum verisinde 7 dilde (en, tr, fr, es, zh, ar, ru) bulunur. İçerik ve görünüm 06 mockup'ından alınır. Hasta kimliği yoktur (KVKK notu altbilgide) | CT ölçümü |
| K-N4 | **Olaylar ve alıcılar** (06 mockup'ı ile aynı): `crm.visit-report.submitted` → yönetici, yanıt adresi temsilci; `crm.visit-report.adverse-event` → PV (Kime) + yönetici (Bilgi), konu "ACİL", 24 saat; `crm.visit-report.off-label-request` → MSL (Kime) + yönetici (Bilgi); `crm.visit-report.overdue` → yönetici. Rapor düzeltmesi (amend) yeni e-posta göndermez | 06 mockup'ı |
| K-N5 | **PV / MSL ara çözümü (K-W8 açık):** CRM kiracı ayarında "PV güvenlik e-postası" ve "MSL e-postası" alanları olur. Doluysa olay o adrese gider, yönetici Bilgi'de. Boşsa yalnız yönetici alır ve kayıtta `pv-route-pending` / `msl-route-pending` notu düşer. K-W8 cevabı gelince PV bağlantısı bu adresin yerine ya da yanına eklenir; olay adı değişmez | CT önerisi, kullanıcı onayına açık |
| K-N6 | **CRM bildirim kaydı.** Kendi koleksiyonu `crm_visit_notifications` olur. Satır: kiracı, olay, ziyaret / rapor kimliği, temsilci, çözülen alıcı özeti (kullanıcı kimlikleri + rol; e-posta adresi YOK), Platform dispatch kimliği, durum (queued / sent / failed / no-recipient / route-pending), deneme sayısı, zamanlar. İki işi vardır: (a) tekillik: (kiracı, olay, konu) tekil indeksi, aynı rapor için ikinci e-posta yok; (b) uygulama içi "Gönderilen e-postalar" listesinin kaynağı. Platform'un dispatch listesi kiracıya açık değil, kaynak modül süzgeci de yok | CT |
| K-N7 | **Zamanlayıcı CRM'de, barındırılan servis olarak.** CRM'de Hangfire yok. `IHostedService` + `PeriodicTimer` (varsayılan 15 dk) iki iş yapar: (a) rapor süresi dolan ziyaretler (`VisitReportDeadline.IsPassed`, 48 saat; `report_missing` okuma anında türetilir) için `overdue` olayı; (b) `failed` satırların yeniden denenmesi (en çok 5 deneme, artan bekleme). Birden çok örnek çalışırsa satır atomik "claim" ile alınır (findOneAndUpdate). Kiracı ayarıyla kapatılabilir | CT ölçümü |
| K-N8 | **Asla iş akışını bozmaz.** Rapor gönderimi e-posta başarısız olsa da tamamlanır. Bildirim, rapor kaydedildikten sonra denenir; hata kayda `failed` olarak yazılır ve zamanlayıcı yeniden dener. Platform çağrısı 3 sn sınırlıdır, 1 kez yeniden denenir, istisna fırlatmaz (W3L koltuk istemcisi deseni) | CT |
| K-N9 | **Görünürlük ve yetki.** "Gönderilen e-postalar" listesinde temsilci kendi ziyaretlerini, yönetici ekibini görür (K-W7 deseni), bildirim ayarı yetkilisi hepsini görür. Yeni anahtarlar: `crm.visit-notifications.read`, `crm.visit-notifications.settings.manage`. Ayar değişikliği denetim kaydına yazılır (`IVisitEvidenceAuditPublisher` deseni). AUD-001 listesine eklenirse Faz 8'e gider | CT |

## Sözleşme eki (W5-BE'den sabitlendi, 2026-10-19) — PLT-NOTIFY bunlara uyar

- **Şablon değişkenleri** (adlar CRM'in gönderdiği; şablonlar ve RequiredVariables bunlarla):
  - ortak: `repName`, `doctorName`, `institutionName`, `visitDate` (yyyy-MM-dd), `reportUrl`;
  - `crm.visit-report.submitted`: + `products`, `note`, `adverseEvent`, `offLabelRequest`, `followUpDate`;
  - `crm.visit-report.adverse-event`: + `products`, `adverseEventNote`;
  - `crm.visit-report.off-label-request`: + `products`;
  - `crm.visit-report.overdue`: + `deadline` (ISO-8601, Z).
  `products` şimdilik ürün kodu listesi (MDM adı okunmuyor); şablon boş değerde bölümü gizler.
- **Yanıt adresi:** iç uç gövdesine isteğe bağlı `replyTo: { userId }` (K-N4: rapor özeti, advers, MSL ve süre doldu e-postalarında yanıt temsilciye gider); Platform adresi Auth'tan çözer, yanıtta adres dönmez. CRM bu alanı PLT-NOTIFY kabul edilince gönderir (W5-BE-FU).
- **Gateway:** `api/crm/visit-notifications` için ocelot rotası W5-BE'de yok → CT düzeltmesi (`/api/crm/visit-notifications` GET; `/{everything}` GET + PUT) + rota kapsama satırları.

---

## §36.1 Agent Prompt — PLT-NOTIFY (paste-ready · Platform; W5-BE ile paralel)

```text
@[.antigravity/agents/backend-architect.md]
WP: WP-VW-W5 · PLT-NOTIFY — Platform'da iç olay gönderim ucu (CRM ve zamanlayıcılar için), paylaşılan "yöneticisi" çözümleyicisi, CRM bildirim olayları + 7 dil şablonları
Repository: C:\tmp\vw-w5-plt (worktree, test/crm-content-visit-e2e güncel başı) · Branch: wp/vw-w5-plt · commit bu dala, push YOK. Kullanıcının C:\Users\user\Desktop\ERP-vNext checkout'una DOKUNMA. Platform sahibi kullanıcı (onay 2026-10-19, bu istemin dosya kapsamı); kapsam dışı bir şey gerekirse DUR ve sor (XMC-001).

Paket belgesi: execution/domains/commercial-suite/work-packs/WP-VW-W5-notifications.md — önce oku: K-N1, K-N2, K-N3, K-N4, K-N6. Tasarım: mockups/visit-workspace/decoded/06-epostalar.html (şablon metinleri, bantlar, alanlar; `rapor`, `advers`, `msl`, `sure` — `sip_*` W6'nın).
MEVCUT (CT ölçümü):
- Notifications: QueueEmailNotificationCommand / QueueEmailNotificationRequest (NotificationContracts.cs:110-137), DispatchNotificationByEventCodeCommand + NotificationEventDispatchAdapter (event tanımı, RequiredVariables, alıcıyı dile göre gruplar; diller en,tr,fr,es,zh,ar,ru), NotificationTemplate (TemplateKey + Channel + Locale; kiracı / platform varsayılanı; NotificationTemplateSeed.cs TenantLocales), NotificationDispatch (`notification_dispatches`; CorrelationId / CausationId; SourceModule YOK), EmailDispatchSweepJob (yeniden deneme).
- Olay kataloğu: yalnız süreç içi IModuleManifestProvider'lar senkronlanır (NotificationEventManifestSyncService.cs:17-45) → CRM olayları Platform'daki Features/Crm/SelfRegistration/CrmManifestProvider.cs NotificationEvents'e (ya da NotificationEventSeed.cs) yazılır.
- Yönetici zinciri: Features/TimeEntry/Services/ApproverResolver.cs (birincil koltuk → ReportsToPositionId → tutanlar, kendisi hariç → boşsa yukarı, döngü koruması, en çok 32; sonra timesheet'e özel zaman yöneticisi havuzu) — ITimeEntryOrgGateway / OrgGatewayAdapter → ITaskSeatDirectory.
- Alıcı adresi: ITaskNotificationRecipientResolver → AuthTaskNotificationRecipientClient (Email, DisplayName, PreferredLanguage).
- İç uç deseni: Internal/InternalTenantEmailIdentityController.cs (X-Internal-Api-Key ↔ AuthService:InternalApiKey). LoginOnlyEndpointGuardTests iç izin listesi (AllowAnonymous iç denetleyiciler listede olmalı).
NE:
(1) ILineManagerResolver (Application, paylaşılan): ResolveAsync(tenantId, userId) → { userIds, resolution: LineManager | ManagerChain | None, positionId? }; ApproverResolver'ın zincir yürüyüşü buraya taşınır; ApproverResolver bunu çağırır + KENDİ havuz yedeğini korur (timesheet davranışı ve testleri DEĞİŞMEZ).
(2) İç uç: POST api/internal/notifications/events/dispatch (X-Internal-Api-Key; iç izin listesine eklenir). Gövde: { tenantId, eventCode, sourceModule, recipients: { to: [ {userId} | {lineManagerOf: userId} | {email, displayName?, locale?} ], cc: [aynı biçim] }, replyTo?: { userId }, variables, correlationId, causationId? } — değişken adları ve replyTo için belgedeki "Sözleşme eki" bölümüne uy. Kurallar: kiracı var ve aktif değilse 404; olay kodu katalogda Active ve sahibi sourceModule değilse 403; userId / lineManagerOf yalnız o kiracıda çözülür (kiracı bağlamı gövdeden kurulur — repository'ler ITenantContext ile süzüyor); hiç alıcı çözülmezse gönderim yok, yanıt status=no-recipient. Yanıt: { dispatches: [{ dispatchId, locale, status }], resolved: { to: [{ userId?, via: direct|line-manager|manager-chain|email }], cc: [...] }, status } — e-posta adresleri yanıtta YOK.
(3) NotificationDispatch'e SourceModule (isteğe bağlı alan; eski kayıtlar null) + yönetici listesinde süzgeç olarak (GetNotificationDispatchListQuery'ye sourceModule).
(4) CRM olayları (K-N4): crm.visit-report.submitted, crm.visit-report.adverse-event, crm.visit-report.off-label-request, crm.visit-report.overdue — RequiredVariables, sahip modül MOD-0155; şablonlar platform varsayılanı olarak 7 dilde (konu + gövde; 06 mockup'ındaki alanlar: doktor / kurum, ziyaret tarihi, temsilci, ürünler, özet / not, "Raporu görüntüle" bağlantısı; advers için "ACİL" + "24 saat" + "Temsilci tıbbi değerlendirme yapmaz" notu; altbilgide KVKK notu). Hasta kimliği değişkeni YOK.
KORU/YAPMA: mevcut uçlar ve QueueEmail davranışı aynı; timesheet onaycı davranışı aynı (mevcut testler yeşil kalır, gevşetilmez); Gateway / ocelot DEĞİŞMEZ (iç uç gateway'den geçmez); localhost:27017'ye BAĞLANMA; gerçek e-posta GÖNDERME (Fake sağlayıcı).
DOĞRULA: yeni testler — iç uç: anahtarsız 401, yanlış anahtar 401, bilinmeyen kiracı 404, katalogda olmayan / başka modülün olayı 403, lineManagerOf: doğrudan yönetici / boş koltuk → üst / yönetici yok → no-recipient, başka kiracının kullanıcısı çözülmez, dil gruplama (tr + en alıcı → 2 dispatch), yanıtta e-posta adresi yok; ILineManagerResolver birim testleri (kendisi hariç, döngü, derinlik); ApproverResolver testleri değişmeden yeşil; şablonlar 7 dilde var (her olay × dil). 3 sabotaj (.bak + touch, geri al + md5): (i) kiracı denetimi kaldırılsın → kırmızı; (ii) "kendisi hariç" kaldırılsın → kırmızı; (iii) sahip modül denetimi kaldırılsın → kırmızı. Platform Application tam takım (DITEN_TEST_MONGOD + -o services/Diten.Platform içinde; taban: aynı test dalı başı, aynı adlar) + LoginOnlyEndpointGuardTests (taban: yalnız ESignature satırları) + mimari (known_failures.py, taban sınıfları).
Commit: "feat(platform): WP-VW-W5 PLT-NOTIFY — internal event dispatch for services and schedulers (tenant from the body, recipients by user / line manager / email, owner-module check), shared line-manager resolver extracted from the timesheet approver, dispatch source module, CRM visit report notification events with 7-language templates" + son satır Co-Authored-By: Claude Opus 5.5 <noreply@anthropic.com>. Rapor TÜRKÇE (§22). K13.
```

## §36.1 Agent Prompt — W5-BE (paste-ready · CRM; PLT-NOTIFY ile paralel)

```text
@[.antigravity/agents/backend-architect.md]
WP: WP-VW-W5 · W5-BE — CRM ziyaret bildirimleri: Platform iç olay ucuna istemci, bildirim kaydı (tekillik + liste), rapor gönderiminde rapor özeti / advers olay / endikasyon dışı, rapor süresi doldu zamanlayıcısı + yeniden deneme, kiracı bildirim ayarları, liste API'si
Repository: C:\tmp\vw-w5-be (worktree, test/crm-content-visit-e2e güncel başı) · Branch: wp/vw-w5-be · commit bu dala, push YOK.

Paket belgesi: execution/domains/commercial-suite/work-packs/WP-VW-W5-notifications.md — önce oku: K-N1…K-N9 ve PLT-NOTIFY istemindeki iç uç sözleşmesi (gövde / yanıt) — sözleşme BU belgede sabit; PLT-NOTIFY paralel yazılıyor, Platform'u sahte HTTP işleyiciyle test et.
MEVCUT (CT ölçümü):
- Rapor gönderimi: Features/VisitReport/V2/VisitReportV2Handlers.cs SubmitVisitReportV2Command (:66, işleyici :577, ReportStatus = Submitted :667); eski yol SubmitVisitReportHandler.cs (:248, :354); düzeltme AmendVisitReportV2Command (:69) → e-posta YOK.
- Güvenlik bayrakları: Domain/Entities/VisitReportV2.cs:232-237 VisitReportSafety { AdverseEvent, AdverseEventNote, OffLabelRequest } (eczane raporunda off-label null — VisitReportV2Rules.cs:373).
- Süre: VisitWorkStatus.ReportMissing / VisitReportDeadline.IsPassed (48 saat; okuma anında türetilir, saklanmaz).
- HTTP istemci deseni: Infrastructure/VisitEvidence/PlatformOrganizationSeatReader.cs (Gateway:BaseUrl, 3 sn + 1 yeniden deneme, hiç fırlatmaz) — ANCAK bu iş istek bağlamı olmadan da çalışmalı: iç uç doğrudan Platform'a gider (Platform taban adresi yapılandırmadan; gateway DEĞİL), başlık X-Internal-Api-Key = AuthService:InternalApiKey (CRM'de var: AuthServiceOptions, AuthUserDisplayNameClient deseni). Kullanıcı token'ı İLETİLMEZ.
- CRM'de Hangfire / barındırılan servis yok; denetim deseni IVisitEvidenceAuditPublisher.
NE:
(1) IPlatformNotificationClient + HTTP uygulaması (K-N1, K-N8): DispatchAsync(tenantId, eventCode, recipients, variables, correlationId) → sonuç (dispatchId'ler, durum, çözülen alıcı özeti) ya da "unavailable"; sourceModule = MOD-0155.
(2) crm_visit_notifications (K-N6): tekil indeks (TenantId, EventCode, SubjectId); durumlar queued / sent / failed / no-recipient / route-pending; e-posta adresi SAKLANMAZ; kiracı süzgeçli depo.
(3) Olaylar (K-N4, K-N5): rapor Submitted olduktan SONRA (rapor işlemi başarısız olmaz): submitted → lineManagerOf(temsilci); AdverseEvent=true → to PV e-postası (ayar) + cc lineManagerOf; ayar boşsa yalnız yönetici + route-pending notu; OffLabelRequest=true → MSL e-postası + cc yönetici (aynı kural). Değişkenler: doktor / kurum adı, ziyaret tarihi, temsilci adı, ürünler, not, rapor bağlantısı (Web yolu); hasta kimliği YOK. Eski Submit yolu da aynı servisi çağırır.
(4) Zamanlayıcı (K-N7): IHostedService + PeriodicTimer (ayar, varsayılan 15 dk; Crm:Notifications:Enabled=false ile kapanır — testlerde kapalı): (a) süresi dolan ziyaretler → overdue (yönetici) — tekil indeks sayesinde bir kez; (b) failed satırlar en çok 5 deneme, artan bekleme; satır atomik claim (findOneAndUpdate) ile alınır. Gerçek saat değil enjekte TimeProvider.
(5) Kiracı bildirim ayarları: { pvEmail?, mslEmail?, overdueReminders: bool, reportSummary: bool } — GET / PUT (sürümlü, 409), e-posta biçim doğrulaması, denetim kaydı; izin crm.visit-notifications.settings.manage (okuma için crm.visit-notifications.read de yeter).
(6) Liste API'si: GET api/crm/visit-notifications (sayfalı; süzgeç: tarih, olay, durum, temsilci) — görünürlük K-N9 (temsilci kendi, yönetici ekibi — mevcut ekip okuma deseni, ayar yetkilisi tümü); tekil GET (önizleme için değişkenler + olay + durum; e-posta adresi yok).
(7) İzin anahtarları CRM manifestine / izin kataloğuna mevcut desenle; Gateway: yeni CRM uçları ocelot'ta var mı ölç (api/crm/{everything} catch-all varsa yöntemler) + VwW3AuthoringRouteCoverageTests benzeri rota kapsama satırları — W3L dersi: yeni uç için gateway yöntemi testle sabitlenir.
KORU/YAPMA: rapor gönderiminin davranışı ve testleri aynı (e-posta hatası raporu düşürmez); localhost:27017'ye BAĞLANMA (geçici mongod deseni); gerçek Platform'a bağlanma.
DOĞRULA: testler — submit → tek submitted kaydı + doğru alıcı isteği; ikinci submit / amend → yeni e-posta yok; advers + PV ayarı dolu → to PV, cc yönetici; ayar boş → route-pending; eczane raporunda off-label yok; Platform kapalı → rapor yine Submitted, kayıt failed, zamanlayıcı yeniden dener; overdue bir kez; iki eşzamanlı claim → bir gönderim; liste görünürlüğü (temsilci / yönetici / yetkili / başka kiracı 404); ayar 409 + doğrulama; gövdede kullanıcı token'ı yok, X-Internal-Api-Key var. 3 sabotaj: (i) tekil indeks / tekillik denetimi kaldır → kırmızı; (ii) rapor işlemine e-posta hatası sızsın (try kaldır) → kırmızı; (iii) liste kiracı süzgeci kaldır → kırmızı. CRM Application tam takım (-o repo içi; taban: aynı test dalı başı — bilinen VisitPlanningFix2 -o) + mimari (known_failures.py; AUD-001'e eklenen yeni komutları raporla → Faz 8).
Commit: "feat(crm): WP-VW-W5 W5-BE — visit notifications: platform internal dispatch client, notification log (once per subject, list source), report summary / adverse event / off-label on submit, overdue scheduler with retries, tenant notification settings, list API" + son satır Co-Authored-By: Claude Opus 5.5 <noreply@anthropic.com>. Rapor TÜRKÇE (§22). K13.
```

## §36.1 Agent Prompt — W5-WEB (paste-ready · yalnız Web · PLT-NOTIFY + W5-BE test dalına alındıktan SONRA)

```text
@[.antigravity/agents/frontend-ui-ux.md]
WP: WP-VW-W5 · W5-WEB — "Gönderilen e-postalar" listesi + önizleme (06 mockup'ı) ve bildirim ayarları kartı
Repository: C:\tmp\vw-w5-web (worktree, test/crm-content-visit-e2e güncel başı — W5-BE ve PLT-NOTIFY içinde) · Branch: wp/vw-w5-web · commit bu dala, push YOK.

Paket belgesi: WP-VW-W5-notifications.md (K-N4, K-N5, K-N6, K-N9) · Tasarım: mockups/visit-workspace/decoded/06-epostalar.html (sol grup listesi + sağ e-posta önizlemesi; Kime / Bilgi / Tarih; bant; alanlar; altbilgi KVKK notu).
STANDARTLAR: Golden Compact DataTable (liste; süzgeç kartı + tablo), sağ çekmece hızlı görünüm, --bs-primary (sabit renk yok), ortak sekme kartı (_TabCard + diten-tabs.js) gerekiyorsa, başlık h5 + p, UXH-001 "?" yardım metni 7 dil (PageHelpResource + PageHelpCatalog), Enter / chord (data-dt-primary), L10n 7 dil (sayfa kaynağı + window.L10n köprüsü), yetkisiz kullanıcıya iskelet yok (UAS-001).
NE:
(1) CRM › Ziyaret › Gönderilen e-postalar sayfası: liste (tarih, olay, doktor / kurum, temsilci, alıcı rolü — yönetici / PV / MSL, durum rozeti: gönderildi / bekliyor / başarısız / alıcı yok / PV yönlendirmesi bekliyor); süzgeçler (tarih, olay, durum, temsilci — yönetici / yetkili için); satır → sağ çekmecede 06 mockup'ındaki e-posta önizlemesi (şablon görünümü, değişkenlerle; e-posta adresi gösterilmez, rol + ad gösterilir).
(2) Bildirim ayarları kartı (aynı sayfada ikinci sekme, yalnız crm.visit-notifications.settings.manage): PV güvenlik e-postası, MSL e-postası, rapor özeti açık / kapalı, süre hatırlatması açık / kapalı; boş PV / MSL için açıklama ("Boşsa yalnız yönetici bilgilendirilir; PV bağlantısı K-W8 kararından sonra"); sürüm çakışması (409) mesajı.
(3) Menü kaydı + izin manifestosu (mevcut CRM ziyaret menüsü deseni); Web proxy denetleyicisi (W3L VisitEvidenceSettingsController deseni; tanımsız durumda CRM çağrısı yok).
KORU/YAPMA: mevcut sayfalar aynı; mock / harness kullanıcının oturum sekmesine enjekte edilmez (ayrı sekme); localhost:27017 YOK.
DOĞRULA: xUnit + RunNode (vitest çalışmıyor): liste kolonları / süzgeçler / rozetler, önizleme e-posta adresi içermez, ayar kartı yetkisizde yok, 409 mesajı, 7 dil anahtarları eksiksiz (resx 7 dosya), yardım metni 7 dil, rota / proxy testleri; 2 sabotaj (önizlemede e-posta adresi sızsın → kırmızı; ayar kartı yetki denetimi kaldır → kırmızı). Diten.Web.Tests tam takım (taban: aynı başta 3 bilinen kırmızı — 2 ShipmentDetailsDateNode + WorkCenterMenu -o) + verify_datatable_page.py (proxy sayfa: --api-profile proxy). Görsel kanıt E4'te (06 mockup'ı ile yan yana).
Commit: "feat(web): WP-VW-W5 W5-WEB — sent visit notification list with email preview and notification settings" + son satır Co-Authored-By: Claude Opus 5.5 <noreply@anthropic.com>. Rapor TÜRKÇE (§22). K13.
```

## §37 CT kabul — W5-BE E2 ACCEPTED (2026-10-19 · Gateway rotası ve canlı E4 bekliyor)
Ajan commit'i `34c19673d` → test dalına cherry-pick. CRM + Platform `CrmManifestProvider` (2 ApiPermission).

**Yapılan:**
- **Platform istemcisi** doğrudan `PlatformService:BaseUrl`, `X-Internal-Api-Key`, kullanıcı token'ı yok, 3 sn + 1 deneme, istisna yok.
- **`crm_visit_notifications`**: tekil (TenantId, EventCode, SubjectId), durumlar, alıcı "tarif" olarak (adres yok).
- **Olaylar:** V2 + eski Submit yolu (dış sarmalayıcıda tek nokta) → özet / advers / MSL; amend e-posta yok; e-posta hatası raporu düşürmez.
- **Zamanlayıcı:** `BackgroundService` + `PeriodicTimer`; overdue = iş durumu `expired`, son 7 gün, yalnız ayarı açık kiracılar (yayın günü toplu e-posta yok); yeniden deneme 5 / 10 / 20 / 40 dk, en çok 5; atomik claim (gerçek mongod'da 8 eşzamanlı claim → 1).
- **Ayar** GET / PUT (409, e-posta doğrulaması, denetim — adres değil set / unset); **liste + önizleme API'si** (temsilci kendi, yönetici ekip, ayar yetkilisi tümü; dışı 404); izinler ApiPermission.

**CT kontrolü:**
- **CT sabotajı (ajanın yapmadığı):** overdue taramasında iş durumu denetimi kaldırıldı (`if (true)`) → `N1_an_expired_visit_reminds_the_line_manager_once…` **kırmızı**; md5 ile geri.
- **Testler (CT, birleşik ağaç):** CRM 2924/2 — `VisitPlanningFix2…` (-o) + `PiiMasking_Redacts…` (kararsız: rastgele GUID'de 10+ rakam telefon diye maskeleniyor — test kusuru, takip FU-CRM-PII-GUID-FLAKE). Mimari 127/17 = taban sınıfları; AUD-001'e +1 komut (`UpdateVisitNotificationSettingsCommand`) → Faz 8.
- **Ajan:** yeni + etkilenen 65/65; Platform manifest 236/236; Web nav koruyucusu 7/7.

**Açık / dikkat:**
- **Gateway rotası yok** (`api/crm/visit-notifications`) → CT düzeltmesi kullanıcı onayıyla.
- **PLT-NOTIFY** değişken adları + `replyTo` → "Sözleşme eki"; CRM `replyTo` gönderimi PLT-NOTIFY sonrası (W5-BE-FU).
- **Canlı yapılandırma:** `PlatformService:BaseUrl`, `Crm:Notifications:WebBaseUrl` (yerel varsayılan 5057 / 5001).
- Rapor gönderimi e-posta denemesini aynı istekte bekliyor (en kötü ~3 sn × olay sayısı) — canlı E4'te ölçülecek.
