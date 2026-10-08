# WORK PACKAGE — WP-VP-2 · Ziyaret planlama Faz 2: görünen adlar, temsilci = oturum + sahiplik, bölge evreni, sunucu türetmesi (CRM + Web + Auth katalog)

> **CT (SoR), 2026-10-06.**
> - **Kaynak:** [yol haritası](ROADMAP-visit-planning.md) Faz 2 (B-8, B-1, B-2, B-3) · [durum analizi](VISIT-PLANNING-current-state-analysis.md) (A2, A3, A4, A6, D4) · [mockup analizi](mockups/visit-planning/VISIT-PLANNING-mockup-analysis.md) · [mobil not](mobile/2026-10-06-visit-planning/MOBILE-NOTE-2026-10-06-visit-planning.md) + [yanıtlar](mobile/2026-10-06-visit-planning/MOBILE-ANSWERS-2026-10-06.md).
> - **Kullanıcı (2026-10-06):** "Dört işi tek pakette yaz".
> - **Kararlar:** K-1 temsilci kendi haftası · K-3 oyun / kampanya seçilmez · K-4 segment seçilmez · K-5 bölgesiz temsilci tüm hesaplar + uyarı · K-6 kullanıcı ↔ bölge ataması.
> - **Önceki iş:** WP-VP-FIX-1 (E2 + E4 kabul).
>
> **Çalışma yeri:** worktree `C:\tmp\vp-2`, dal `wp/vp-2`, taban `test/crm-content-visit-e2e`. Commit bu dala, push YOK.

## Bağlam (CT kod okuması, 2026-10-06)
- **Planlanan ziyaret DTO'ları** (`Features/PlannedVisit/PlannedVisitModels.cs`): hedef için yalnız `targetType`, `targetId`, `accountId`, `contactId`, `resourceDisplayName`. Ad yok.
- **Sahiplik yok:** planlanan ziyaret, planlama oturumu ve ziyaret raporu okumaları kiracı içinde herkese açık.
- **`resources/me`** (`Features/Resources/GetMyResourcesQuery.cs`): geçici "kullanıcı = kaynak", tek öğe, ad yok. `IUserDisplayNameResolver` (`Application/Common`) mevcut.
- **Bölge:**
  - Temsilci ataması `territory_resource_assignments`: `Resource.ResourceId` = kullanıcı kimliği, `CoverageScope` exact-territory / territory-subtree, `Status`, `ValidFrom` / `ValidTo`.
  - Kural `TerritoryPositionPolicy`: temsilci yalnız zone / microzone.
  - Hesap ataması `account_territory_assignments`. İstanbul ve Kocaeli artık ilçe düzeyinde (yol haritası 0.4), diğer iller il düzeyinde.
  - Okuma `AccountCurrentCoverageResolver.ResolveCoveredAccountIdsByNodesAsync` **tam düğüm** eşleşmesi yapıyor, alt ağaç yok. Kullananlar: Hesaplar ızgara filtresi, `VisitFrequencyTargetImpactCounter`.
  - Test verisi: Beste (`c5769c62-…`) Şişli, Kağıthane, Beyoğlu, Fatih ilçelerinde → 1.535 hesap.
- **Türetme:**
  - Sıklık, segment verilmese de doktorun aktif segment üyeliklerinden türetiliyor (`IVisitFrequencyPolicyResolver` DET-P, `DeriveContactSegmentsAsync`).
  - **İçerik türetilmiyor:** `VisitContentSequenceResolver.ResolveBindingsAsync` strateji kimliği ya da istek segmenti yoksa `no-strategy` döner (canlı önizlemede tüm ziyaretler `no-strategy`).
  - Planlama oturumu segmenti `EligibleContactSelector` (~75) ile segment dışı doktoru **sessizce düşürüyor**.
  - `CreatePlannedVisitHandler` / `UpdatePlannedVisitHandler` istemciden gelen `campaignId`'yi doğrulayıp yazıyor, `strategyTemplateId` / `segmentId`'yi kullanıyor. PUT tam değiştirmedir.
- **Web:**
  - Ziyaret Planlama formu: temsilci seçici (kullanıcı listesi) + segment.
  - Hedefler: hesap arama 43K'nın tamamında; "Seçilenler" GUID.
  - Planlanan Ziyaretler: liste "Contact f3b8f9ce", detay GUID; formda `ResourceId` serbest metin, `CampaignId` GUID metni.
  - Ziyaret Yürütme takvim kartı: yalnız ziyaret kodu.

## NE
### B-8 · Görünen adlar (mobil D1; D4)
1. **Planlanan ziyaret liste + detay** yanıtına **ek** alanlar:
   - `targetDisplayName`: hedef kişiyse doktor adı, hesap / eczaneyse kurum adı;
   - `accountDisplayName`, `contactDisplayName`;
   - `targetInactive` (bool): hedef pasif ya da arşivliyse.
   - Okuma anında, **toplu** çekilir (sayfadaki kimlikler için tek sorgu / hesap ve kişi deposunda kimlik listesiyle okuma). Kayda kopyalanmaz. Hedef bulunamazsa ad `null`, kimlik kalır.
2. **Planlama oturumu detay** yanıtına ek ad alanları:
   - `selectedContacts[]` için `contactDisplayName`, `accountDisplayName`;
   - `selectedAccounts[]` ve `selectedPharmacies[]` için `{id, displayName}` (mevcut kimlik dizileri değişmez, yanına ek dizi).
3. **Web:**
   - Planlanan Ziyaretler listesi: hedef sütunu ad + tür rozeti, kimlik kısaltması kalkar; ad ile aranabilir.
   - Planlanan Ziyaretler detayı: GUID yerine ad, kişi / hesap kartına bağlantı.
   - Ziyaret Yürütme takvim kartı: ziyaret kodunun altında hedef adı.
   - Ziyaret Planlama Hedefler "Seçilenler": ad + kurum + uzmanlık, hesaba göre gruplu; doktor tablosundaki "BAĞLANTI" GUID sütunu kalkar.
   - Yerelleştirme 7 dil.

### B-1 · Temsilci = oturum + sahiplik (mobil B01, R-M4; A2)
1. **Yeni izin anahtarları** (Tier-3, `platform.tasks.read-all` emsali, **yalnız açık grant**, hiçbir role varsayılan verilmez):
   - `crm.planned-visit.read-all`
   - `crm.visit-plan.read-all`

   İzin standardına göre kaydet: Auth katalog açıklaması **yalnız İngilizce**, manifest eylem tanımı, KP-5a emsali. Seed / grant YAZMA.
2. **Sahiplik kuralı.** Çağıranın kaynağı `resources/me` ile aynı mantıktan gelir (kullanıcı kimliği). `read-all` anahtarı olmayan çağıran için:
   - **Planlanan ziyaret:** liste / detay / takvim yalnız `ResourceId == çağıran`. Başkasının kaydına detay / güncelle / onayla / iptal / arşiv → **404** (varlığı sızdırma).
   - **Planlama oturumu:** liste / detay / önizleme / uygula / re-plan aynı kural.
   - **Ziyaret raporu:** okuma, kaydetme, düzeltme yalnız kendi planlanan ziyaretleri için.
   - `read-all` sahibi kiracı içindeki tüm kayıtları görür ve mevcut yazma izinleriyle işlem yapar (yönetici / onaylayan; onay SoD'si aynen kalır).
3. **Kaynak sunucuda belirlenir:**
   - Oturum oluştur / planlanan ziyaret oluştur: `read-all` yoksa `ResourceId` / `ResourceType` istemciden **alınmaz**, çağırandan yazılır. İstemci başka bir kaynak gönderirse 403 + makine kodu `resource_not_caller`.
   - `read-all` sahibi başka bir kaynak adına oluşturabilir.
   - `ResourceDisplayName` sunucuda `IUserDisplayNameResolver` ile doldurulur.
4. **`resources/me`:** öğeye `displayName` (ek alan). Tek öğe kuralı aynı.
5. **Web:**
   - Ziyaret Planlama formunda temsilci alanı **salt okunur** (oturumdaki kişinin adı); kullanıcı listesi proxy'si (`api/users`) kalkar.
   - Planlanan Ziyaretler formunda `ResourceId` / `ResourceType` / `ResourceDisplayName` salt okunur ve oturumdaki kişi olur (`read-all` varsa mevcut alanlar düzenlenebilir kalır).
   - Liste başlığı ve sütunları değişmez (yeniden tasarım Faz 4 / 6).
6. **Grant script'i** (`scripts/rbac/grant_visit_planning_read_all_97c5.py`, `grant_planned_visit_rbac_97c5.py` deseni):
   - varsayılan deneme; `--role <ad> --apply` kullanıcıda;
   - iki anahtarı verilen role ekler; mevcut olanı atlar; GUID subtype 4.

### B-2 · Hedef evreni = bölge ataması (A4, K-5, K-6)
1. **Alt ağaç kapsamı:** `AccountCurrentCoverageResolver`'a düğümlerin **alt ağacını** genişleten okuma (aynı model, aktif düğümler).
   - **Hesaplar ızgarası** bölge filtresi ve **`VisitFrequencyTargetImpactCounter`** alt ağaçla çalışır. "İstanbul" filtresi ilçelere taşınmış hesapları da bulur.
   - Kapsam yaşam döngüsü kuralı (`TerritoryCoverageLifecyclePolicy`) aynen uygulanır.
2. **"Benim hesaplarım" ucu** (yalnız okuma, yeni **sorgu**; yazma komutu değil): `GET /api/crm/visit-plan/my-accounts?search=&type=&page=&pageSize=`
   - Çağıranın (ya da `read-all` sahibi için `resourceId` parametresindeki kaynağın) **şu an geçerli** temsilci atamaları alınır.
   - `exact-territory` → yalnız düğüm; `territory-subtree` → düğüm + alt ağaç.
   - Bu düğümlerin **geçerli** hesap kapsamı döner (sayfalı; ad, tür, şehir / ilçe).
   - Yanıtta `territoryStatus`:
     - `assigned` + atama düğümleri (kod, ad);
     - ya da `unassigned`: **K-5** — tüm kiracı hesapları döner ve ekran uyarı gösterir.
3. **Ziyaret Planlama Hedefler (Web):**
   - "Klinik / hastane ekle" araması bu uçtan yapılır.
   - **"Bölge dışı ekle"** ayrı bir seçenek: mevcut tüm-hesap araması, eklenen satırda "bölge dışı" rozeti. Mevcut `TerritoryGate` önizleme uyarısı aynen kalır.
   - `unassigned` ise üstte sarı bant: "Size bölge atanmamış; tüm hesaplar gösteriliyor. Yöneticinize başvurun." (7 dil).
   - Seçili listede bölge dışı hesaplar rozetle işaretli.

### B-3 · Strateji / kampanya / segment sunucuda türetilir (A3, A6, K-3, K-4)
1. **Planlama oturumu:**
   - Oluştur / güncelle isteğindeki `segmentId`, `campaignId`, `strategyTemplateId` **yok sayılır** (saklanmaz; mevcut kayıtlardaki değerler okunur ama kullanılmaz).
   - `EligibleContactSelector`'daki segment süzgeci **kaldırılır**; segment dışı doktor artık düşmez.
2. **Doktor başına türetme** (planlayıcı + manuel planlanan ziyaret oluştur / güncelle):
   - **Segmentler:** doktorun aktif üyelikleri. Sıklıktaki DET-P türetmesinin aynı okuyucusu / sınırı; ortak yardımcıya çıkar, ikinci kopya yazma.
   - **Oyun:** bu segmentlere bağlı **aktif** strateji şablonları. Birden çoksa deterministik seçim: şablon kodu, sonra sürüm. Neden kodu `multiple-plays`; yoksa `no-strategy`. İçerik çözücüye bu kimlik verilir.
   - **Kampanya:** doktoru (ya da hesabını) hedef anlık görüntüsünde içeren, ziyaret tarihini kapsayan **aktif** kampanya. Birden çoksa başlangıcı en erken, sonra kod; yoksa `null`. Yalnız köken (provenance).
   - **Sıklık:** mevcut DET-P aynen (segment parametresi `null`).
3. **Planlanan ziyaret oluştur / güncelle (mobil + Web):**
   - İstemcinin `campaignId` / `strategyTemplateId` / `segmentId` değeri **alınmaz**, türetilen yazılır.
   - Güncellemede alan gönderilmese de köken **silinmez** (yeniden türetilir).
   - `contentSource = manual` + elle yolculuk / aşama seçimi mevcut davranışla kalır (Faz 6).
4. **Web:**
   - Ziyaret Planlama formunda **segment alanı kalkar** (form artık ülke, dönem, hafta, salt okunur temsilci).
   - Planlanan Ziyaretler formunda `CampaignId` girişi kalkar.
   - Segment proxy'si (`api/segments`) kullanılmıyorsa kalkar.

## KORU / YAPMA
- **Ekran tasarımı değişmez** (Faz 4 / 6): yalnız yukarıdaki alan / sütun / metin değişiklikleri. Rota sekmesi aynen kalır. Tarih biçimi aynen kalır.
- **Yeni yazma komutu YOK.** "Benim hesaplarım" bir sorgudur. AUD-001 denetimsiz komut sayısı **26'dan artmamalı**; gerekirse DUR + raporla.
- Hafta durumu, gün dengeleme, sıklık varsayılanı, "bu hafta görülmesi gerekenler", kapasite: **YAPMA** (Faz 3).
- RBAC seed / grant / canlı veri yazma YOK (grant script'i yalnız yazılır, deneme modunda çalıştırılır).
- Mevcut kayıtlar için göç YOK (okuma anında türetme).
- İzin açıklamaları Auth'ta **yalnız İngilizce** (kullanıcı kararı).
- Sahiplik ihlali **404**; bilgi sızdırma yok. Kiracı sınırı her sorguda.
- CRM class-map + GUID string; `esc()` / `textContent`.
- Mobil sözleşmesi yalnız **ekleme** (mevcut alan adları / zarf değişmez).

## Acceptance
### E2
- **Taban ölç, yalnız farkı raporla:** Web (666/0), CRM Application (2254/0/5; PII flake), Auth (taban ölç), Platform odaklı (194/3 ortam), mimari (38/1; **26 sabit**). Build 0 hata.
- **Yeni testler (üretim koduyla):**
  1. **B-8:** liste / detay adları toplu çekilir (N kayıt → sabit sayıda okuma); bilinmeyen hedef → ad `null`; `targetInactive`.
  2. **B-8:** oturum detayı ad dizileri.
  3. **B-1:** `read-all` yok → başkasının planlanan ziyareti / oturumu / raporu: liste dışı, detay 404, güncelle / onayla / iptal 404; `read-all` var → görünür.
  4. **B-1:** istemci başka `resourceId` gönderirse 403 `resource_not_caller`; boş gönderirse çağıran yazılır; `resources/me` `displayName`.
  5. **B-2:** alt ağaç kapsamı (ilçedeki hesap il filtresinde bulunur); "benim hesaplarım" exact vs subtree; geçerlilik penceresi dışı atama sayılmaz; `unassigned` → tüm hesaplar + durum.
  6. **B-3:** segment dışı doktor artık düşmez; doktor üyeliğinden oyun türetilir (`no-strategy` yerine çözülür); iki oyun → deterministik + `multiple-plays`; istemci `campaignId` yok sayılır; güncellemede köken korunur.
  7. **Web:** form alanları (temsilci salt okunur, segment / strateji / kullanıcı proxy'si yok); Hedefler bölge bandı anahtarları 7 dil; Planlanan Ziyaretler hedef adı sütunu.
- **Sabotaj (kırmızı kanıtla, geri al):**
  1. Sahiplik süzgecini kaldır → test 3 kırmızı.
  2. Alt ağaç genişletmesini kapat → test 5 kırmızı.
  3. Segment süzgecini geri koy → test 6 kırmızı.
  4. Ad toplu okumasını satır başına okumaya çevir → test 1'deki okuma sayısı kırmızı.
- **Grant script'i** deneme modunda çalıştırılır; çıktısı rapora eklenir.

### E4 (CT, fleet; kullanıcı giriş yapar; ayrı sekme)
- **Beste (Admin, `read-all` yok):**
  - Ziyaret Planlama Hedefler araması yalnız 4 ilçenin hesaplarını getirir (~1.535).
  - "Bölge dışı ekle" çalışır.
  - Planlanan Ziyaretler'de hedef adları görünür; yalnız kendi kayıtları listelenir.
- **Bölgesi atanmamış ikinci kullanıcı** (kullanıcı girişiyle): sarı bant + tüm hesaplar.
- **Hesaplar** ızgarasında "İstanbul" filtresi ilçe hesaplarını bulur.
- **Önizleme** (kaydetmeden): `contentStatus` segmenti olan doktorlarda `no-strategy` dışında bir değer verir (aktif oyun varsa).

---

## §36.1 Agent Prompt (paste-ready) — DISPATCH: owner
```text
@[.antigravity/agents/backend-architect.md] @[.antigravity/agents/frontend-ui-ux.md] @[.antigravity/agents/security-agent.md]
WP: WP-VP-2 · Ziyaret planlama Faz 2: görünen adlar, temsilci = oturum + sahiplik, bölge evreni, sunucu türetmesi (CRM + Web + Auth katalog)
Repository: C:\tmp\vp-2 (worktree) · Branch: wp/vp-2 · commit bu dala, push YOK

Paket belgesi + tam komut: execution/domains/commercial-suite/work-packs/WP-VP-2-rep-scope-names-territory-derivation.md — önce tamamını oku. Ayrıca: …/ROADMAP-visit-planning.md · …/VISIT-PLANNING-current-state-analysis.md · …/WP-VP-FIX-1-visit-planning-quick-fixes.md (§37) · …/mobile/2026-10-06-visit-planning/{MOBILE-NOTE…, MOBILE-ANSWERS…}.md · .antigravity/rules/{permission-key-standard.md, audit-trail-standard.md} · services/Diten.CrmService/src/**/Features/{PlannedVisit,VisitPlanning,VisitReport*,Resources,Territory,VisitContentSequence*,VisitFrequencyPolicy,Segmentation,Campaign*}/** · services/Diten.CrmService/src/**/Common/IUserDisplayNameResolver.cs · Auth izin kataloğu + KP-5a (crm.safety-text.*) kayıt deseni · scripts/rbac/grant_planned_visit_rbac_97c5.py · frontend/Diten.Web/{Controllers/CRM/{VisitPlanning,PlannedVisits,CrmVisitExecution,Accounts}Controller.cs, Views/CRM/{VisitPlanning,PlannedVisits,VisitExecution}/**, wwwroot/assets/js/CRM/{VisitPlanning,PlannedVisits,VisitExecution}/**, ilgili resx} · memory: crm-classmap-rejects-unknown-elements, crm-new-aggregate-classmap-guid, mongo-guid-subtype-write-recipe, rolepermission-guid-subtype-login-500, rep-facing-visit-play-campaign-invisibility, l10n-bridge-pascalcase-loader.

NE (ayrıntı WP'de):
B-8 ad: planlanan ziyaret liste+detay ek targetDisplayName/accountDisplayName/contactDisplayName/targetInactive (okuma anında TOPLU, kopyalama yok); oturum detayına ad dizileri; Web: Planlanan Ziyaretler hedef adı (liste+detay), Ziyaret Yürütme kartında hedef adı, Hedefler "Seçilenler" adlı + BAĞLANTI GUID sütunu kalkar.
B-1 sahiplik: yeni Tier-3 anahtarlar crm.planned-visit.read-all + crm.visit-plan.read-all (yalnız açık grant, Auth açıklaması yalnız İngilizce, manifest eylem); read-all yoksa planlanan ziyaret/oturum/rapor yalnız ResourceId==çağıran, başkasınınki 404; oluşturda kaynak sunucudan (başka resourceId → 403 resource_not_caller), ResourceDisplayName IUserDisplayNameResolver; resources/me ek displayName; Web: VP formunda temsilci salt okunur + api/users kalkar, PV formunda kaynak alanları salt okunur (read-all hariç); grant script'i scripts/rbac/grant_visit_planning_read_all_97c5.py (deneme varsayılan, --role --apply kullanıcıda).
B-2 bölge: AccountCurrentCoverageResolver alt ağaç (Hesaplar ızgara filtresi + VisitFrequencyTargetImpactCounter); yeni SORGU GET /api/crm/visit-plan/my-accounts (geçerli temsilci atamaları, exact/subtree, territoryStatus assigned|unassigned; unassigned → tüm hesaplar, K-5); Web Hedefler araması bu uçtan, "Bölge dışı ekle" ayrı + rozet, unassigned sarı bant (7 dil).
B-3 türetme: oturumda segment/kampanya/strateji istemciden alınmaz; EligibleContactSelector segment süzgeci kalkar; doktor başına aktif segment üyelikleri (DET-P okuyucusu, ortak yardımcı) → aktif oyun (deterministik: kod, sürüm; multiple-plays / no-strategy) → içerik çözücüye; kampanya = doktoru/hesabını hedefleyen aktif kampanya (deterministik) köken; planlanan ziyaret oluştur/güncelle istemci değerini almaz, güncellemede köken silinmez; Web VP formunda segment, PV formunda CampaignId kalkar.
KORU/YAPMA: tasarım değişmez (Faz 4/6), Rota + tarih biçimi aynen; YENİ YAZMA KOMUTU YOK (AUD-001 26 sabit; gerekirse DUR); Faz 3 işleri YOK; seed/grant/canlı veri YOK; göç YOK; sahiplik ihlali 404; kiracı sınırı; mobil sözleşmesi yalnız ekleme; class-map + GUID string; esc()/textContent.
DOĞRULA (E2): tabanı ölç, yalnız farkı raporla — Web (666/0) · CRM Application (2254/0/5, PII flake) · Auth (taban ölç) · Platform odaklı (194/3 ortam) · mimari (38/1, 26 SABİT); build 0 hata; fleet açıkken Web bin kilitliyse -o frontend/Diten.Web.Tests/bin/Debug/<ad>. Yeni testler WP Acceptance 1–7. Sabotaj 1–4 (kırmızı kanıtla, geri al). Grant script'ini deneme modunda çalıştır, çıktıyı rapora koy.
Commit: "feat(crm,web,auth): WP-VP-2 — visit planning phase 2 (display names, rep = caller + ownership, territory universe + subtree coverage, server-derived play/campaign/segment)" + son satır Co-Authored-By: Claude Opus 5.5 <noreply@anthropic.com>. Rapor: madde başına ne yapıldı + kanıt (dosya:satır, test adı), sahiplik uygulanan uçların listesi, mobil sözleşmesine eklenen alanlar (ad + tip), oyun/kampanya türetme kuralı, grant script'i deneme çıktısı. §22 TÜRKÇE. K13.
```

---

## §37 CT kabul — E2 ACCEPTED (2026-10-06)
**Commit:** `fecf231e` (ajan `fac1399d`, test dalı üzerine rebase + ff). Push: test dalı.

**CT K13 (kendi koşum):**
| Paket | Taban | Sonuç |
|---|---|---|
| Web (worktree) | 666/0 | **674/0** |
| Web (birleşik, CYC-UI-FIX-2 ile) | — | **697/0** |
| CRM Application | 2254/0/5 | **2270/0/5** |
| Auth Application | 1097/0 | **1099/0** |
| Platform odaklı | 194/3 | **196/3** (3 = yerel mongod, ortam) |
| Mimari | 38/1 (26) | **38/1 (26 sabit)** |

**Kod okuması (CT):**
- Sahiplik tek kural: `ICallerScope` + `VisitOwnership.MayAccess / ResolveWriteResource`. Kimlik yalnız principal'dan (`sub`), istekten okunmaz; kimlik yoksa fail-closed.
- `GetPlannedVisitById` başkasının kaydına 404; liste süzülüyor; ziyaret raporu listesi kendi planlanan ziyaretlerine daraltılıyor.
- `read-all` anahtarları Auth `ExplicitGrantOnlyPermissions`'ta: modül eşitlemesi / SuperAdmin vermez.
- Adlar sayfa başına toplu (`VisitTargetNameReader`, `$in`).

**CT sabotajı (ajanınkinden ayrı):**
1. `GetMyAccountsQuery` geçerlilik sonu kontrolünü kaldır → `My_accounts_ignores_an_assignment_outside_its_validity_window…` **kırmızı**.
2. `ResolveWriteResource` başka kaynağa izin versin → `A_planned_visit_for_another_resource_is_403…` + `A_session_for_another_resource_is_403…` **kırmızı**.

İkisi de geri alındı.

**Ajan notları → CT:**
- **Grant script'i** deneme modunda ABORT ("anahtarlar katalogda yok"), beklenen: AuthService yeni kodla yeniden başlayınca `DataSeeder` anahtarları oluşturur, sonra `--role <ad> --apply` kullanıcıda.
- **Kaydedilmiş hesaplarda bölge dışı rozeti** yalnız önizlemenin "hiçbir bölgede değil" uyarısına dayanıyor; "benim bölgem dışında" değil → **Faz 4 VP-UI-2'de** düzeltilecek (Hedefler sekmesi, `my-accounts` ile karşılaştırma).
- **⚠ D9 (yeni bulgu, VP-2 öncesinden):** Ziyaret Planlama **Düzenle** formu `selectedAccountIds / selectedPharmacyIds / selectedContacts` dizilerini **boş** gönderiyor (`form.js` `buildPayload`). Sunucu PUT'ta seçimi bunlarla değiştiriyor → taslak planda hafta / dönem değiştirmek **tüm hedefleri siler**. Yol haritasına eklendi, öncelikli küçük düzeltme.

**E4 (CT, bekliyor):** fleet yeniden başlatılmalı (Auth seeder yeni anahtarlar · CRM · Platform manifest · Web).
### §37 ek — E4 ACCEPTED (CT canlı, 2026-10-07; fleet yeniden başlatıldı, Beste girişi, ayrı sekme, kayıt yok)
| Kontrol | Sonuç |
|---|---|
| Form | ✓ Ülke / Dönem / Hafta / Temsilci (salt okunur "Admin User"); segment ve strateji yok |
| `resources/me` | ✓ `displayName: "Admin User"` |
| `my-accounts` | ✓ `territoryStatus = assigned`, 4 düğüm (Beyoğlu, Fatih, Kağıthane exact; Şişli subtree), **totalCount 1.535** |
| Hedefler | ✓ Arama bölge ucundan; "Bölge dışı hesap ekle" var; atanmış kullanıcıda sarı bant gizli; "Seçilenler" hesaba göre gruplu ve adlı; BAĞLANTI sütunu yok |
| Planlanan Ziyaretler | ✓ 176 / 176 kayıtta `targetDisplayName` (ör. "SİBEL GÜLÇİÇEK" · "İSTANBUL EĞT.VE ARAŞTIRMA HAS."); detayda da; hepsi Beste'nin (sahiplik süzgeci) |
| Hesaplar ızgarası | ✓ Alt ağaç: İstanbul **8.864** (= 8.843 ilçe + 21 il), Şişli 734, Marmara 14.001, Konya 1.189 |
| Önizleme türetmesi | ⚠ 156 `no-strategy` + 20 `not-applicable` (eczane). **Kod değil, veri:** tek aktif oyun `STR-TUTUKON-URO`, **arşivlenmiş** `SEG-URO-DOCTORS` segmentine bağlı → üyelik yok → oyun yok. Kural birim testlerle kanıtlı; canlı gösterim için aktif segment + oyun gerekir (veri). |

**Bulgular:**
- **F-1:** Hesap araması Türkçe büyük / küçük harfe duyarlı. "HAMİDİYE" 4 sonuç, "Hamidiye" 0 (İ / i). `my-accounts` ve muhtemelen genel hesap araması → küçük düzeltme.
- **F-2:** Kaydedilmiş bölge dışı hesaplarda rozet yok (Şanlıurfa, Konya) → Faz 4 VP-UI-2 (biliniyordu).
- **F-3:** Hedefler "Şehir / Bölge" sütunu ham kod ("SANLIURFA") → Faz 4.
- **Veri:** TUTUKON oyunu arşivli segmente bağlı → yeni aktif segment + oyun bağlama (kullanıcı / veri işi) olmadan içerik türetmesi canlıda görünmez.