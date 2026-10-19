# WP-VW-W3L — Katmanlı ziyaret kanıtı ayarı (tüzel kişilik + iş birimi)

> **Kaynak:** WP-VW-W3-authoring "sonra" satırı (PLT-ORG-SEAT, W3-BE-d, W3-WEB-e) · yol haritası §8 sıra 4–5.
> **Tasarım:** `mockups/visit-workspace/BRIEF-W3-authoring-and-evidence-settings.md` §"Kapsamlı ayarlar" + `W3 Yazim Ekranlari (standalone).html` / `decoded-w3/` (C1–C2, ekran 7 "iş birimi seçili"). Kullanıcı 2026-10-19: **bu mockup ile devam**.
> **Kullanıcı kararı 2026-10-19:** iki katman birlikte (tüzel kişilik + iş birimi).

## Kararlar
| # | Karar |
|---|---|
| K-L1 | Öncelik: **iş birimi > tüzel kişilik > kiracı**; süreli istisna en üstte, temsilciye özel gevşetme olarak kalır (bugünkü gibi). |
| K-L2 | **Alan bazında miras:** kiracı katmanı bütün alanları tutar; tüzel kişilik / iş birimi katmanı yalnız değiştirdiği alanları tutar, boş alan üstten gelir. 10 alanın hepsi katmanlanabilir (konum, yarıçap, doğruluk, fotoğraf, doktor onayı, süre tutarlılığı, kısa ziyaret eşiği, konumsuz kurum, QR, cihaz). |
| K-L3 | **Tüzel kişilik kaynağı** = Platform'da temsilcinin **bugün yürürlükteki birincil (Primary) pozisyon ataması** → Position.OrganizationUnitId → OrganizationUnit.LegalEntityId. Platform'daki kural `TaskSeatDirectory` / `TenantOrganizationMapper.IsActiveNow` (kopyalanmaz, tüketilir). Ataması yoksa katman atlanır ("pozisyon ataması yok"). |
| K-L4 | **İş birimi kaynağı** = CRM'de temsilcinin bugün aktif modeldeki **birincil (IsPrimary) bölge ataması**: `TerritoryResourceAssignment.BusinessScopes` (business-unit); boşsa modelin `TerritoryModel.BusinessScopes`. Tek değer → o iş birimi; birden çok → **belirsiz**, katman atlanır ("birden çok iş birimi"); hiç yoksa atlanır. Tahmin YOK. |
| K-L5 | **Eksik veri ya da Platform erişilemez:** ilgili katman atlanır, bir üst katman geçerli olur; sebep `ScopeChain.Note`'ta ve ziyaret başlangıcının kanıt kaydında saklanır (sonradan "hangi kural uygulandı" sorusu cevaplanabilir). Ziyaret başlatma engellenmez. |
| K-L6 | Katman değişikliği bugünkü kiracı ayarı gibi **doğrudan** (Workflow yok), sürüm kontrollü; geçmiş (değişiklik günlüğü) kapsam bazında. "Kapsamı kaldır" = katman arşivlenir (silinmez), geçmişe yazılır. |
| K-L7 | **Pozisyon kodu kuralı (CRM TerritoryPositionPolicy):** bugün yalnız tam `medical-representative` / `area-manager` / `regional-manager` kabul ediliyor → tüm ülkeler tek pozisyonu paylaşmak zorunda → K-L3 herkesi aynı tüzel kişiliğe düşürür. Değişiklik: kod **tam eşit ya da `-<rol>` ile biten** (ör. `uz-medical-representative`, `tr-area-manager`) aynı kurala bağlanır. Ülke başına pozisyon ilgili ülkenin organizasyon biriminde (ör. UZRO-FF) açılır. |
| K-L8 | **Seat sözleşmesi (Platform, PLT-ORG-SEAT)** — BE-d paralel çalışabilsin diye sabit: `GET api/platform/organization/seats/{userId}` → `{ userId, primary: { positionId, positionCode, organizationUnitId, organizationUnitCode, legalEntityId, effectiveFrom } | null }`; `POST api/platform/organization/seats/lookup` gövde `{ userIds: [..] }` (≤ 100) → `{ items: [ aynı şekil ] }`. Yetki: istenen userId = çağıran kullanıcı ise herkes; değilse kiracıya atanabilir yeni anahtar `platform.organization-seats.read`. Kiracı sınırı; başka kiracının kullanıcısı = boş / 404. Gateway: kiracı kapsamlı yol izin listesi. |

**Veri ön koşulları (kullanıcı / İK / CRM yöneticisi):** temsilcilere ülke pozisyonuyla Platform pozisyon ataması; bölge modeli ya da temsilci atamasında iş birimi seçimi. Eksikse sistem bozulmaz, bir üst katmana düşer (K-L5).

**Sıra:** PLT-ORG-SEAT ∥ W3-BE-d (sözleşme K-L8'de sabit) → W3-WEB-e.

## §36.1 Agent Prompt — PLT-ORG-SEAT (paste-ready · yalnız Platform + Gateway)
```text
@[.antigravity/agents/backend-architect.md]
WP: WP-VW-W3L · PLT-ORG-SEAT — Platform'da kullanıcının bugünkü birincil pozisyon atamasını (pozisyon → organizasyon birimi → tüzel kişilik) veren dar okuma uçları; mantık mevcut TaskSeatDirectory'den TÜKETİLİR, kopyalanmaz
Repository: C:\tmp\vw-w3l-seat (worktree, test/crm-content-visit-e2e güncel başı) · Branch: wp/vw-w3l-seat · commit bu dala, push YOK

Paket belgesi: execution/domains/commercial-suite/work-packs/WP-VW-W3L-layered-evidence.md — önce oku: K-L3, K-L8 (sözleşme BİREBİR). Kurallar: .antigravity/rules/routes.md (Gateway kiracı / admin yolları, X-Tenant-Id), PKS-001 (izin anahtarı manifestte), tenant izolasyonu.
MEVCUT: services/Diten.Platform/src/Diten.Platform.Application/Features/Tasks/Services/TaskSeatDirectory.cs (ITaskSeatDirectory: ActiveAsync, kullanıcının bugünkü satırları, PositionIdsForUserAsync — "WHO SITS IN WHICH SEAT, the ONE place"; Primary satır ev birimini belirler) · TenantOrganizationMapper.IsActiveNow (yürürlük kuralı) · Domain/Entities/Organization: PositionAssignment (UserId, PositionId, EffectiveFrom/To, AssignmentType Primary/Secondary/Acting/Delegated, IsCancelled), Position (Code, OrganizationUnitId), OrganizationUnit (Code, LegalEntityId zorunlu) · Controllers/Platform/PositionAssignmentsController.cs (GET liste yalnız platform.position-assignments.read — CRM temsilcisine VERİLMEZ) · gateway/Diten.ApiGateway/ocelot.json · CrmManifestProvider / Platform manifest izin kaydı deseni.
NE:
(1) Uygulama: kullanıcının bugünkü birincil koltuğunu çözen sorgu — ITaskSeatDirectory üzerinden (kural ikinci kez yazılmaz; gerekirse ITaskSeatDirectory'e tek, adı açık bir üye eklenir ve TaskSeatDirectory'nin "türetilmiş üyeler" notuna uyulur). Birden çok Primary varsa en erken başlayan + raporla; Primary yoksa primary = null.
(2) API: K-L8'deki iki uç (tekil + toplu ≤ 100). Yetki: kendi userId'si için kimlik doğrulanmış her kiracı kullanıcısı; başkası için yeni kiracıya atanabilir anahtar platform.organization-seats.read (manifestte, açıklamasıyla; seed / grant YOK). Kiracı sınırı her sorguda; başka kiracının kullanıcısı bulunmaz.
(3) Gateway: iki yol kiracı kapsamlı izin listesinde (routes.md); admin yolu DEĞİL.
(4) Performans: toplu uç tek okuma turu (kullanıcı başına ayrı repository çağrısı YOK).
KORU/YAPMA: TaskSeatDirectory davranışı ve Görev Merkezi aynı; PositionAssignments CRUD / liste uçları aynı; pozisyon / OU / LE verisine YAZMA yok; CRM'e DOKUNMA; canlı / yerel veri yazma YOK; localhost:27017'ye BAĞLANMA.
DOĞRULA: Platform testleri — Primary yürürlükte → dolu; Primary gelecek / bitmiş / iptal → null; yalnız Secondary → null; iki Primary → en erken + rapor; başka kullanıcı yetkisiz → 403, yetkili → 200; başka kiracı → yok; toplu 101 → 400; toplu sonuç tekil ile aynı; Gateway yolu kiracı kapsamlı (OcelotConfigurationTests); TaskSeatDirectory testleri aynı. + 3 sabotaj (.bak + touch): (1) Primary süzgeci kalksın (Secondary de dönsün) → kırmızı; (2) yetki kontrolü kalksın → kırmızı; (3) kiracı sınırı kalksın → kırmızı. Platform tam takım + mimari + Gateway testleri (-o repo içi), taban karşılaştırması.
Commit: "feat(platform): WP-VW-W3L PLT-ORG-SEAT — a user's current primary seat (position, organization unit, legal entity) read through TaskSeatDirectory; self or platform.organization-seats.read; single + bulk" + son satır Co-Authored-By: Claude Opus 5.5 <noreply@anthropic.com>. Rapor TÜRKÇE (§22). K13.
```

## §36.1 Agent Prompt — W3-BE-d (paste-ready · CRM · PLT-ORG-SEAT ile paralel)
```text
@[.antigravity/agents/backend-architect.md]
WP: WP-VW-W3L · W3-BE-d — Ziyaret kanıtı ayarında tüzel kişilik ve iş birimi katmanları: saklama (alan bazında miras), çözümleyici (iş birimi > tüzel kişilik > kiracı + süreli istisna), kapsam API'leri, temsilci listesinde çözülmüş kapsam; TerritoryPositionPolicy ülke pozisyon kodlarını tanır
Repository: C:\tmp\vw-w3l-be-d (worktree, test/crm-content-visit-e2e güncel başı) · Branch: wp/vw-w3l-be-d · commit bu dala, push YOK

Paket belgesi: execution/domains/commercial-suite/work-packs/WP-VW-W3L-layered-evidence.md — önce oku: K-L1..K-L8. Tasarım içeriği: mockups/visit-workspace/BRIEF-W3-authoring-and-evidence-settings.md "Kapsamlı ayarlar" (alan bazında miras, kapsamı kaldır, etkin ayar kaynağı, "N temsilciye uygulanır"). Emsal: MOD-0165-FU07 CyclePeriod kapsamı (ScopeType ayrımlı, okuma anında göç, geri doldurma YOK).
MEVCUT: Domain/Entities/VisitEvidencePolicy.cs (kiracı başına TEK belge, 10 alan) · Domain/Repositories/IVisitExecutionRepository.cs IVisitEvidencePolicyRepository (GetAsync(tenant) / Insert / Replace sürümlü) + IVisitEvidencePolicyChangeRepository · Application/Features/VisitEvidence/VisitEvidencePolicyResolution.cs (VisitEvidencePolicyResolver.ResolveAsync + saf Combine; EvidenceScopeChain(LegalEntity, BusinessUnit, Note) "unresolved" — dikiş HAZIR) · VisitEvidencePolicyReads.cs (history, effective, reps, reps/roster) · ziyaret başlatma + yürütme sözleşmesi resolver'ı kullanıyor · Domain/Entities TerritoryResourceAssignment.BusinessScopes + IsPrimary, TerritoryModel.BusinessScopes · Features/Territory/ResourceAssignments/TerritoryPositionPolicy.cs (Rules sözlüğü: medical-representative / area-manager / regional-manager tam eşleşme) · CRM'in Platform'a Gateway üzerinden kullanıcı token'ıyla giden emsal istemcisi: Infrastructure/CycleCapacity/WorkingCalendarWorkingDayCounter.cs.
NE:
(1) Saklama: politika kapsamlı olur — ScopeType (tenant | legal-entity | business-unit) + ScopeRef (LE Guid / BU kodu); kiracı satırı bütün alanlar; diğerleri yalnız değiştirilen alanlar (nullable). Mevcut kiracı belgesi okuma anında tenant sayılır (geri doldurma YOK, FU07 deseni); depo (tenant, scopeType, scopeRef) anahtarlı, sürümlü; arşivleme (Kapsamı kaldır). BU kodu MOD-0048 business-unit yayımlanmış değerine karşı doğrulanır (bölge modeliyle aynı kural); LE kimliği biçim kontrolü.
(2) Platform istemcisi: K-L8 sözleşmesine göre seat okuyucu (tekil + toplu), kullanıcı token'ıyla Gateway üzerinden (WorkingCalendar emsali); zaman aşımı / hata → null + "platform-unavailable" (K-L5). Testlerde sahte; gerçek uç PLT-ORG-SEAT ile gelir.
(3) İş birimi okuyucu (K-L4): aktif modeldeki IsPrimary ataması → BusinessScopes, boşsa model; tek / belirsiz / yok.
(4) Çözümleyici: iş birimi katmanı → tüzel kişilik → kiracı, alan alan; sonra süreli istisna gevşetmesi (bugünkü Combine). Her alanın kaynağı (tenant / legal-entity:<id> / business-unit:<kod> / exception:<id>) ve ScopeChain (LegalEntity, BusinessUnit, Note: resolved / no-seat / no-territory / bu-ambiguous / platform-unavailable). Ziyaret başlangıcının kanıt kaydında ScopeChain saklanır.
(5) API (mevcut yetkilerle: policy read / manage): kapsam listesi (her kapsam: tür, ref, değiştirilen alan sayısı, son değişiklik, kaç temsilciye uygulandığı); kapsam oku / yaz (sürümlü) / kaldır; geçmiş kapsam süzgeçli; effective (temsilci için alan + kaynak + zincir); reps/roster satırlarına çözülmüş zincir (seat TOPLU okunur — temsilci başına Platform çağrısı YOK).
(6) K-L7: TerritoryPositionPolicy — kod tam eşit ya da "-<rol>" ile biten (uz-medical-representative, tr-area-manager) aynı kural; başka kodlar bugünkü gibi reddedilir.
(7) Denetim: yeni komutlar (kapsam yaz / kaldır) CRM denetim yayıncısıyla denetlenir — AUD-001 borcuna yeni satır EKLEME.
(8) Yetki betiği (CFG): scripts/rbac/ altında platform.organization-seats.read'i Admin ve isteğe bağlı yönetici rolüne veren betik, varsayılan dry-run (W3E-CFG emsali); ÇALIŞTIRMA.
KORU/YAPMA: kiracı katmanı ve süreli istisna davranışı, ziyaret başlatma sözleşmesi, mevcut uçların cevap şekli (yalnız ek alanlar) aynı; Platform koduna DOKUNMA; Web'e DOKUNMA; canlı / yerel veri yazma YOK; localhost:27017'ye BAĞLANMA.
DOĞRULA: CRM testleri — miras (LE 2 alan değiştirir, BU 1 alan; temsilci zincirinde doğru değer + kaynak); öncelik BU > LE > tenant; istisna hâlâ üstte; K-L4 tek / belirsiz / yok / atama boş → model; K-L5 seat null / Platform hata → üst katman + Note; eski kiracı belgesi okunur (geri doldurma yok, gerçek mongod); kapsam yaz sürüm çakışması 409; kaldır → arşiv + geçmiş; BU kodu yayımlanmamış → 400; "N temsilci" sayımı; roster toplu seat (çağrı sayısı ölçülür); TerritoryPositionPolicy uz-medical-representative kabul, "medical-representative-x" / "xmedical-representative" ret; kiracı izolasyonu. + 4 sabotaj (.bak + touch): (1) öncelik ters (LE > BU) → kırmızı; (2) alan mirası bozulsun (boş alan null yazılsın) → kırmızı; (3) belirsiz BU'da ilk değer seçilsin → kırmızı; (4) TerritoryPositionPolicy son ek kuralı "Contains" olsun → kırmızı. CRM tam takım + mimari (-o repo içi), taban karşılaştırması.
Commit: "feat(crm): WP-VW-W3L W3-BE-d — visit evidence policy layers (legal entity from the Platform primary seat, business unit from the primary territory assignment), field-level inheritance, scope API, layered resolver with sources; territory position policy accepts country position codes" + son satır Co-Authored-By: Claude Opus 5.5 <noreply@anthropic.com>. Rapor TÜRKÇE (§22). K13.
```

## §36.1 Agent Prompt — W3-WEB-e (paste-ready · yalnız Web · W3-BE-d test dalına alındıktan SONRA)
```text
@[.antigravity/agents/frontend-ui-ux.md] @[.antigravity/agents/l10n-agent.md]
WP: WP-VW-W3L · W3-WEB-e — Ziyaret kanıtı "Kapsamlı ayarlar": kapsam ağacı (kiracı + tüzel kişilik + iş birimi), alan bazında miras etiketleri, kapsam ekle / kaldır, "N temsilciye uygulanır", etkin ayar ve temsilciler tablosunda kaynak
Repository: C:\tmp\vw-w3l-web-e (worktree, test/crm-content-visit-e2e güncel başı — W3-BE-d dahil) · Branch: wp/vw-w3l-web-e · commit bu dala, push YOK

Paket belgesi: execution/domains/commercial-suite/work-packs/WP-VW-W3L-layered-evidence.md (K-L1..K-L8) + §37 W3-BE-d (API'nin son hali). Tasarım: mockups/visit-workspace/BRIEF-W3-authoring-and-evidence-settings.md "Kapsamlı ayarlar" + "W3 Yazim Ekranlari (standalone).html" / decoded-w3/ (C1, C2, ekran 7 "iş birimi seçili") — kullanıcı: bu mockup ile devam. Standartlar: K-STD, K-TABS (_TabCard), K-THEME, UXH-001 (.antigravity/rules/page-help-and-keyboard.md), frontend-form-template.md (.diten-field + ikon).
MEVCUT: Views/CRM/VisitEvidenceSettings/Index.cshtml (Golden başlık, dış + iç _TabCard; kapsam ağacında yalnız "Kiracı varsayılanı" + pasif "Tüzel kişilik ekle" ve ipucu; ayar formu; etkin ayar kartı ve-effective; Geçmiş / Temsilciler server-mode tablolar settings-tables.js) · visit-evidence-settings.js (form, önizleme, kaydet onayı) · Web vekili VisitEvidenceSettingsController · yardım metni Help.VISITEVIDENCESETTINGS.* (7 dil, W-UXH).
NE:
(1) Kapsam ağacı (mockup C1): kiracı kökü; altında kendi katmanı olan tüzel kişilikler ve iş birimleri (ad + "N alan değiştirildi · N temsilci" + son değişiklik); "Tüzel kişilik ekle" (MDM tüzel kişilik listesinden seçim) ve "İş birimi ekle" (BRD business-unit) etkin; seçili kapsam formu doldurur.
(2) Form (mockup C2): kiracıda bütün alanlar; tüzel kişilik / iş birimi kapsamında her alan "üstten gelir (kaynak)" ya da "bu kapsamda değiştirildi" etiketi + "üstten al" (alanı boşalt); önizleme seçili kapsamın etkin değerleriyle; kaydet onayı "N temsilciye uygulanır" + değişen satırlar.
(3) Kapsamı kaldır: onay ("Bu kapsamdaki temsilciler üst ayara döner"), data-dt-primary="chord" (geri alınamaz değil ama hemen etkili — UXH-001 tablosu); geçmişe düşer.
(4) Etkin ayar kartı: her alanın kaynağı kapsam adıyla (Kiracı / <tüzel kişilik adı> / <iş birimi adı> / süreli istisna); zincir notu (ör. "Pozisyon ataması yok — tüzel kişilik katmanı atlandı", "Birden çok iş birimi").
(5) Temsilciler tablosu: her satırda uygulanan kapsam çipi; Geçmiş tablosunda kapsam süzgeci + sütunu.
(6) Yardım metni: Help.VISITEVIDENCESETTINGS adımları katmanları anlatacak şekilde güncellenir (7 dil; tırnaklı adlar ekrandaki sözcükle birebir — W-UXH U2 testi yeşil kalır).
KORU/YAPMA: Golden başlık, K-TABS iç / dış sekmeler, istisna listesi (FIX-b) ve Geçmiş / Temsilciler tabloları (FIX-d) davranışı; K-THEME (yalnız --bs-primary; tema guard yeşil) + koyu blok; CRM / Platform koduna DOKUNMA; ortak dosyalar (diten-datatable.js, _TabCard, diten-tabs.js, backbone-custom.css) DEĞİŞMEZ; kalıcı veri yazma YOK (görsel kanıtta kaydetme denenmez); kullanıcının sekmesine harness yok.
DOĞRULA: Web.Tests (xUnit + RunNode; vitest bu makinede çalışmaz): ağaç düğümleri API'den; ekle / kaldır akışı (vekil çağrıları); miras etiketleri ve "üstten al" (alan null gönderilir); kaydet onayı N temsilci; etkin kart kaynak + not; temsilci çipi; geçmiş kapsam süzgeci; 7 dil; UAS-001; tema guard; yardım U1 / U2 yeşil + 3 sabotaj (.bak + touch): (1) "üstten al" alanı boş string göndersin (null değil) → kırmızı; (2) kaynak etiketi kapsam adını değil kodu göstersin → kırmızı; (3) Kapsamı kaldır chord → enter → kırmızı. Web tam takım (-o repo içi), taban karşılaştırması. Görsel kanıt (AYRI sekme; mockup C1 / C2 / ekran 7 ile yan yana; fleet kapalıysa kendi düzeneğin): ağaç, LE seçili form, BU seçili form, etkin kart, Arapça, koyu, telefon.
Commit: "feat(web): WP-VW-W3L W3-WEB-e — visit evidence scope tree (tenant, legal entity, business unit), field-level inheritance, add / remove scope, effective rules and rep table with their source" + son satır Co-Authored-By: Claude Opus 5.5 <noreply@anthropic.com>. Rapor TÜRKÇE (§22). K13.
```

## §37 CT kabul — W3-BE-d E2 ACCEPTED (2026-10-19)
Test dalına alındı: `ad254aef3` (ajan `4f3a3c5fe`), çakışmasız. 23 dosya.

**Ne geldi:**
- **Saklama:** katmanlar yeni koleksiyon `visit_evidence_policy_layers`'da: (TenantId, ScopeType, ScopeRef), aktif kapsam başına tekil kısmi indeks, sürümlü, arşivleme. Kiracı belgesi kendi koleksiyonunda aynı (indeks değişmedi, geri doldurma yok).
- **Platform istemcisi:** `PlatformOrganizationSeatReader` (K-L8; tekil + toplu ≤ 100; kullanıcı token'ı; 3 sn + 1 yeniden deneme; her hata → null / platform-unavailable, çağırana fırlatmaz).
- **İş birimi okuyucu:** K-L4.
- **Çözümleyici:** BU > LE > tenant alan alan, istisna üstte, alan kaynağı; ScopeChain ziyaret başlangıç kaydında (`EvidenceScope`).
- **API:** scopes listesi (değişen alan, son değişiklik, temsilci sayısı — koltuk toplu), kapsam GET / PUT (409) / DELETE (arşiv), geçmiş kapsam süzgeci, effective + roster zinciri (sayfa başına tek toplu Platform çağrısı).
- **TerritoryPositionPolicy:** tam eşit ya da "-<rol>" son eki (en uzun rol kazanır).
- **Denetim:** `IVisitEvidenceAuditPublisher`; varsayılan log, `Crm:Audit:Mode=http` ile `HttpCrmAuditPublisher` (SourceModule MOD-0155).
- **Betik:** `scripts/rbac/grant_w3l_organization_seats_97c5.py` (dry-run, çalıştırılmadı).

**Ajanın kararları (kabul):**
- LE katmanı yoksa Platform çağrılmaz (not `no-layer`).
- Ek not `no-business-unit`.
- İstisna kaynağı eski biçiminde (`exception` + ExceptionId) — yanıt şekli bozulmasın diye.
- Boş katman yazımı 400 (her şeyi devralmak isteyen kapsamı kaldırır).
- `VwW3EBeCTests.CT1` sahte depoları genişletildi.

**CT kontrolü:**
- **Diff:** katman deposu kiracı + aktif + silinmemiş süzgeçli; ortak `HttpCrmAuditPublisher`'a yalnız yeni arayüz uygulaması eklendi; Platform / Web dokunulmadı.
- **Testler (ajan):** yeni 18 test (L17 geçici mongod :27391); CRM 2911/1 (taban 2893/1, +18; tek kırmızı `PiiMasking`); mimari 125/13 = taban.
- **CT sabotajı (K-L5):** `PlatformOrganizationSeatReader` ağ / zaman aşımı hatasını yakalamasın (`HttpRequestException` / `TaskCanceledException` süzgeçten çıkarıldı) → `L15_the_seat_client…unavailable_for_every_failure` **kırmızı**; md5 ile geri alındı.

**Açık / dikkat:**
- **AUD-001:** `UpsertVisitEvidencePolicyScopeCommand` ve `ArchiveVisitEvidencePolicyScopeCommand` tabanda zaten kırmızı olan teste "denetimsiz" olarak ekleniyor (CRM'deki tüm izler "aday"; yayıncıya bağlı komut da denetlenmiş sayılmıyor). Borç satırı eklenmedi → Faz 8.
- **Yetki:** `platform.organization-seats.read` anahtarı PLT-ORG-SEAT gelince katalogda olur; yönetici rolüne verilmezse roster ve başka temsilcinin effective okumasında Platform 403 → platform-unavailable → LE katmanı atlanır (temsilcinin kendi ziyaret başlangıcında self izinli). Betik PLT-ORG-SEAT sonrası kullanıcı tarafından `--apply`.
- **Gecikme:** LE katmanı varken Platform kapalıysa ziyaret başlangıcı en çok ~3 sn bekleyebilir.
