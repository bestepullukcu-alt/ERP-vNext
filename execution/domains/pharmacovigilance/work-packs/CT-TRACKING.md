# PVG İş Takibi

Son güncelleme: **2026-11-20 (Asia/Baku)**. Kaynak CT dalı `chore/ct/pvg-ct-01`, veri okuma HEAD `a53656cdeb8dff40839e5e86ed708d812ad00f19`; yerel son alınan main `2de950edcaeb9d8a153465e5f29d7c71eeb31144`. Bu dosya repo görünümüdür; çelişkide CT-QUEUE.tsv ve kesin kanıt/karar kaydı esas alınır.

Artifact/ArtifactData aracı bu ortamda yok; kullanıcının verdiği claude.ai bağlantısı web okuyucusunda erişilemedi, içeriği/rolü doğrulanmadı. Yeni bir site veya farklı tasarım açılmadı. SOP22.2/9 uyarınca aynı iş listesi bu Markdown dosyasında tutulur; uygun Artifact aracı geldiğinde mevcut tek sayfayla eşleştirilir. Onay kutuları durum gösterimidir; değişiklik için PVG Control Tower sohbetine yazılır.

## Güncel odak

- **Karar:** İki dar ek kapsam doğrudan insan onaylı; rev21 son kesin pack/domain iki bağımsız UYGUN aldı. İki geçerli review-row deftere eklendi; Gerçek HIGH card/row0; yazılı insan onayına bağlı yeni HIGH satırı eklendi, kesin check bekleniyor.
- **Test:** PREP-WEB işçi15791333 ve bağımsız62/62 PASS; veri koruma ana feature tamamlanmadı. Ortak kayıt kapısı2 nedeniyle henüz birleştirilmedi.
- **Test:** PREP-WIN eski ağaç değişmeden korunuyor:394 PASS/1 FAIL/0skip. Yeni tek ACL metodu için scopeonayı var; kesin HIGH ve yenilenmiş normal kapıdan önce yeni çalışma yok.
- **Kural:** Registry işçisi temizfff584a3 ağacında hazır; kesin v1.2 bağımsız UYGUN ve S-54 denemesi0. Normal dağıtım denetimi çalışıyor; ledger henüz değiştirilmedi.
- **Arka uç:** Ana DL-12 BLOCKED; her iki PREP bağımsız E2 ve entegrasyonunu bekliyor. CT ürün kodu yazmıyor; tek son PR hedefi korunuyor.
- **Karar:** Operasyon/yayın kapalı. Son PR Linux/Phase1 ve bağımsız insan main merge kapıları açık; hiçbir işçi ağacı silinmedi.

## Teslim dilimleri

| Dilim | Konu | Kaynak kuyruk görünümü |
| --- | --- | --- |
| DL-1 | Ortak çerçeve | Ayrı WP satırı yok; paket/backlog üzerinden izlenir |
| DL-2 | Alım sözleşmesi ve alanlar | 2 INTEGRATED |
| DL-3 | Host ve yapılandırma | 4 ACCEPTED, 1 SUPERSEDED |
| DL-4 | API ve kimlik/kiracı | 1 ACCEPTED, 3 INTEGRATED |
| DL-5 | Denetim kaydı | Ayrı WP satırı yok; paket/backlog üzerinden izlenir |
| DL-6 | İzin anahtarları | 1 INTEGRATED |
| DL-7 | Kiracı ekranı | 3 INTEGRATED, 1 REWORK |
| DL-8 | Gateway rotaları | 1 ACCEPTED |
| DL-9 | Operasyonel çalışma | Ayrı WP satırı yok; paket/backlog üzerinden izlenir |
| DL-10 | Mongo kalıcılık | 1 INTEGRATED, 1 REWORK |
| DL-11 | Kapalı katalog kaydı | 1 INTEGRATED |
| DL-12 | Düzenlemede veriyi koruma | 3 BLOCKED, 1 READY |

Bu özet alt işlerin durumunu sayar; tüm dilimlerin kabul edildiği veya operasyonun açıldığı sonucu çıkarılmaz.

## Kuyruk özeti

`ACCEPTED`:6, `BLOCKED`:3, `DISCOVERED`:10, `INSPECTED`:1, `INSPECTION_REQUIRED`:4, `INTEGRATED`:11, `READY`:1, `REWORK`:2, `SUPERSEDED`:1. Toplam **39** kaynak satırı. Yalnız `ACCEPTED` satırları işaretlidir; bütün modülün bittiği anlamına gelmez.

## Şimdi ve sırada

- [ ] **Arka uç: Edit sırasında kayıt verisini eksiksiz koruma** — `WP-0230-DL12` · `BLOCKED`
  - Kapsam/kuyruk kaydı: MOD-0230 DL-12 — insan rev20 kapsam/politika/ortak yol/build-test onayı alındı; resmî incelemenin uygulanabilirlik notları aynı kapsamda netleştiriliyor; kesin-sürüm HIGH/dağıtım kapıları ve iki PREP E2 entegrasyonu sonrası feature; kod başlamadı; BLOCKED
  - Kanıt/karar: `docs/records/decisions/2026-11/pvg-mod0230-rev20-owner-approval-01.md`.
- [ ] **Test: Windows Mongo ve tek erişim güvenliği testi hazırlığı** — `WP-0230-DL12-PREP-WIN` · `BLOCKED`
  - Kapsam/kuyruk kaydı: MOD-0230 DL-12 — sınırlı Windows Mongo test hazırlığı, yalnız iki onaylı harness/guard test yolu; feasibility kendi çıktısı, parent feature değişmez
  - Kanıt/karar: `docs/records/audits/2026-11/pvg-dl12-prep-win-01/STOP-v1.2.md`.
- [ ] **Test: Gerçek tarayıcı–MVC–servis–depo test bağlantısı** — `WP-0230-DL12-PREP-WEB` · `BLOCKED`
  - Kapsam/kuyruk kaydı: MOD-0230 DL-12 — sınırlı Web alias/Node-jsdom gerçek MVC-PVG-store test bağlantısı ve iki CI hazırlık adımı, yalnız üç onaylı yol; feasibility kendi çıktısı, parent veri-koruma feature değişmez
  - Kanıt/karar: `docs/records/audits/2026-11/pvg-dl12-prep-web-verify-01/CT-DECISION.md`.
- [ ] **Kural: Ortak regresyon kaydındaki beş eski XML tetikleyicisi** — `WP-0230-DL12-REGISTRY-01` · `READY`
  - Kapsam/kuyruk kaydı: MOD-0230 DL-12 — insan XMC onaylı ortak regresyon kaydında yalnız beş fixed rerun_trigger hücresinden ölü ,*.xml öğesini kaldırma; ürün/guard değişmez
  - Kanıt/karar: `docs/records/decisions/2026-11/pvg-mod0230-extra-scope-approval-01.md`.

## Karar bekleyenler

- [ ] **Karar: operasyonel runtime için açık sahip onayları listesi (RBAC, FieldSecurity, AuditEvent, WorkflowTransitionGate, EvidenceLin…** — `PVG-M1-03` · `INSPECTION_REQUIRED`
  - Kapsam/kuyruk kaydı: MOD-0230 Case Intake & Triage — operasyonel runtime için açık sahip onayları listesi (RBAC, FieldSecurity, AuditEvent, WorkflowTransitionGate, EvidenceLink, TraceBundle, Retention/LegalHold); onay değildir
  - Kanıt/karar: `docs/records/audits/2026-10/pvg-m1-01-discovery-01/RECORD.md`.
  - Tarihsel kuyruk envanteri; başka modülün güncel kod/runtime durumu bu oturumda yeniden ölçülmedi.
- [ ] **Karar: başlanmış (yalnız domain/uygulama sözleşme sınıfları ve testler; API, kalıcılık, UI yok): paket sahibi ataması (künyede TB…** — `PVG-M1-04` · `INSPECTION_REQUIRED`
  - Kapsam/kuyruk kaydı: MOD-0231 Case Processing — başlanmış (yalnız domain/uygulama sözleşme sınıfları ve testler; API, kalıcılık, UI yok): paket sahibi ataması (künyede TBD) ve Signal Minimum Scope sonraki iş paketi taslağı
  - Kanıt/karar: `docs/records/audits/2026-10/pvg-m1-01-discovery-01/RECORD.md`.
  - Tarihsel kuyruk envanteri; başka modülün güncel kod/runtime durumu bu oturumda yeniden ölçülmedi.
- [ ] **Karar: başlanmış (yalnız sözleşme sınıfları ve testler): MedDRA sözlük/sürüm kaynak sözleşmesi (lisans, içe aktarma sahipliği) ka…** — `PVG-M1-05` · `INSPECTION_REQUIRED`
  - Kapsam/kuyruk kaydı: MOD-0232 MedDRA Coding — başlanmış (yalnız sözleşme sınıfları ve testler): MedDRA sözlük/sürüm kaynak sözleşmesi (lisans, içe aktarma sahipliği) kararı ve paket sahibi ataması
  - Kanıt/karar: `docs/records/audits/2026-10/pvg-m1-01-discovery-01/RECORD.md`.
  - Tarihsel kuyruk envanteri; başka modülün güncel kod/runtime durumu bu oturumda yeniden ölçülmedi.

## Sonraki fazlar

Bu bölümde ayrı kuyruk satırı yok.

## Bizim takipler

- [ ] **Belge: Keşif kararları: MOD-0230 Case Intake & Triage, MOD-0231 Case Processing, MOD-0232 MedDRA Coding için verify_module_id.py …** — `PVG-M1-01` · `INSPECTION_REQUIRED`
  - Kapsam/kuyruk kaydı: Keşif kararları: MOD-0230 Case Intake & Triage, MOD-0231 Case Processing, MOD-0232 MedDRA Coding için verify_module_id.py --discover (2026-10-19, üçü de çıkış 3 — mevcut modül); raporların commit'i ve her bulgunun kararı
  - Kanıt/karar: `docs/records/audits/2026-10/pvg-m1-01-discovery-01/RECORD.md`.
  - Tarihsel kuyruk envanteri; başka modülün güncel kod/runtime durumu bu oturumda yeniden ölçülmedi.
- [ ] **Belge: yapılmış ama bitmemiş (API, bellek içi depo, Gateway route, tenant UI var; operasyonel runtime kapalı): kanıt envanteri, E…** — `PVG-M1-02` · `INSPECTED`
  - Kapsam/kuyruk kaydı: MOD-0230 Case Intake & Triage — yapılmış ama bitmemiş (API, bellek içi depo, Gateway route, tenant UI var; operasyonel runtime kapalı): kanıt envanteri, E3/E4 eksikleri, kabul kaydı olmayan işler; kod yok
  - Kanıt/karar: `docs/records/audits/2026-10/pvg-m1-02-inspection-01/INSPECTION-NOTE.md`.
  - Tarihsel kuyruk envanteri; başka modülün güncel kod/runtime durumu bu oturumda yeniden ölçülmedi.
- [ ] **Arka uç: kayıt sürümü ve eşzamanlılık: IPvgIntakeDraftStore.ReplaceAsync beklenen sürümü alır, bulunamadı ile sürüm çakışmasını ayı…** — `WP-0230-DL2A` · `INTEGRATED`
  - Kapsam/kuyruk kaydı: MOD-0230 DL-2 (kısım A) — kayıt sürümü ve eşzamanlılık: IPvgIntakeDraftStore.ReplaceAsync beklenen sürümü alır, bulunamadı ile sürüm çakışmasını ayırır; uygulama servisindeki üç ReplaceAsync çağrısı; bellek içi depo; kabul BG-12, BG-13; pvg-regpv-base 0 kırmızı; Mongo / migration / API / MVC kapsam dışı
  - Kanıt/karar: `docs/records/audits/2026-10/pvg-wave1-integration-01/INTEGRATION.md`.
  - Yalnız entegre; ayrı CT kabulü veya operasyon başarısı sayılmaz.
- [ ] **Arka uç: alım alanlarının ölçülebilir kısmı (AL-1…AL-7, EK-1…EK-5, KR-1…KR-5): alan tanımları, doğrulama ve neden kodları, Create /…** — `WP-0230-DL2B` · `INTEGRATED`
  - Kapsam/kuyruk kaydı: MOD-0230 DL-2 (kısım B) — alım alanlarının ölçülebilir kısmı (AL-1…AL-7, EK-1…EK-5, KR-1…KR-5): alan tanımları, doğrulama ve neden kodları, Create / Update / Triage eşlemesi; kabul AC-21, AC-23, AC-25, AC-27, AC-29; sözleşme bekleyen AC-22, AC-24, AC-26, AC-28, AC-30 kapsam dışı; MDM / CRM / kişi dizinine yazma yok (XMC-001)
  - Kanıt/karar: `docs/records/audits/2026-10/pvg-wave2-integration-01/INTEGRATION.md`.
  - Yalnız entegre; ayrı CT kabulü veya operasyon başarısı sayılmaz.
- [x] **Geçit: Gateway: ocelot.json içindeki dört MOD-0230 route girdisi (yalnız integration-agent); kabul BG-16, Gateway üzerinden smoke…** — `WP-0230-DL8` · `ACCEPTED`
  - Kapsam/kuyruk kaydı: MOD-0230 DL-8 — Gateway: ocelot.json içindeki dört MOD-0230 route girdisi (yalnız integration-agent); kabul BG-16, Gateway üzerinden smoke çağrısı servisin sonucunu döndürür; canlı deneme öncesi tek-CT kuralı (kart §7a/5); Gateway ara katmanı ve diğer route'lar kapsam dışı
  - Kanıt/karar: `docs/records/audits/2026-11/pvg-dl8-acceptance-06/RECORD.md@3ed5cb00a5cbdbb595f8a349cc67e3f9b3048053`.
- [x] **Arka uç: host açılışı: üretim benzeri açılışta beş zorunlu bağımlılıktan biri eksikse ret, sağlık hazırlığı, Program.cs'in yeni kur…** — `WP-0230-DL3A` · `ACCEPTED`
  - Kapsam/kuyruk kaydı: MOD-0230 DL-3 (kısım A) — host açılışı: üretim benzeri açılışta beş zorunlu bağımlılıktan biri eksikse ret, sağlık hazırlığı, Program.cs'in yeni kurulum yöntemini gerçekten çağırması; kabul BG-8; BG-9 (DL3B) ve BG-10 (OD-PVG-6) kapsam dışı; WP-0230-DL3'ün yerine
  - Kanıt/karar: `docs/records/audits/2026-11/pvg-dl3a-acceptance-02/RECORD.md@3ed5cb00a5cbdbb595f8a349cc67e3f9b3048053`.
- [x] **Arka uç: yapılandırma taraması: commit edilmiş yapılandırma, başlatma profili, iş akışı ve dağıtım dosyalarında operasyonel bayrakl…** — `WP-0230-DL3B` · `ACCEPTED`
  - Kapsam/kuyruk kaydı: MOD-0230 DL-3 (kısım B) — yapılandırma taraması: commit edilmiş yapılandırma, başlatma profili, iş akışı ve dağıtım dosyalarında operasyonel bayrakların ve üretim benzeri ortamda test adaptörünün yakalanması; kabul AC-9; DL3A'dan sonra, aynı test dosyası; WP-0230-DL3'ün yerine
  - Kanıt/karar: `docs/records/audits/2026-11/pvg-dl3b-acceptance-04/RECORD.md@f048aa327e4c5fb57aea69c40f2560794d961827`.
- [ ] **API: API: AC-7 yetkisiz çağıran doğrulama kodu almaz (doğrulama izin kapısından sonra), AC-19 sürüm taşıması (PUT / triage / ro…** — `WP-0230-DL4A` · `INTEGRATED`
  - Kapsam/kuyruk kaydı: MOD-0230 DL-4 (kısım A) — API: AC-7 yetkisiz çağıran doğrulama kodu almaz (doğrulama izin kapısından sonra), AC-19 sürüm taşıması (PUT / triage / route beklenen sürüm, GET ve liste sürüm döndürür, çakışma 409), AC-31 istek ve yanıt kayıtları revizyon 04 alanları ve seçim çifti; AC-5 (OD-PVG-2) ve AC-6 (OD-PVG-3) kapsam dışı (WP-0230-DL4B)
  - Kanıt/karar: `docs/records/audits/2026-10/pvg-wave3-integration-01/INTEGRATION.md`.
  - Yalnız entegre; ayrı CT kabulü veya operasyon başarısı sayılmaz.
- [x] **Arka uç: host: boş ASPNETCORE_ENVIRONMENT = Production (K3, DALGA2-01) ve Program'ın ASPNETCORE_URLS okumasının korunması; AC-10 (O…** — `WP-0230-DL3C` · `ACCEPTED`
  - Kapsam/kuyruk kaydı: MOD-0230 DL-3 (kısım C) — host: boş ASPNETCORE_ENVIRONMENT = Production (K3, DALGA2-01) ve Program'ın ASPNETCORE_URLS okumasının korunması; AC-10 (OD-PVG-6) kapsam dışı
  - Kanıt/karar: `docs/records/audits/2026-11/pvg-dl3c-acceptance-02/RECORD.md@3ed5cb00a5cbdbb595f8a349cc67e3f9b3048053`.
- [ ] **Arka uç: izin anahtarı ailesi: PvgIntakeDraftApplicationService.cs satır 7–11'deki beş sabit pvg.case-intake-triage.{create,update,…** — `WP-0230-DL6` · `INTEGRATED`
  - Kapsam/kuyruk kaydı: MOD-0230 DL-6 — izin anahtarı ailesi: PvgIntakeDraftApplicationService.cs satır 7–11'deki beş sabit pvg.case-intake-triage.{create,update,read,triage,route} ve PvgApplicationContractShapeTests; kabul AC-4 (MOD-0230 yüzeyine daraltılmış tarama); katalog/seed/grant ve denetleyici öznitelikleri kapsam dışı (DL-7)
  - Kanıt/karar: `docs/records/audits/2026-10/pvg-wave4-integration-01/INTEGRATION.md`.
  - Yalnız entegre; ayrı CT kabulü veya operasyon başarısı sayılmaz.
- [x] **Arka uç: host: Pvg:Runtime:OperationalRuntimeAuthorized=true iken Pvg:Runtime:ApprovalReference zorunlu, yoksa üretim benzeri açılı…** — `WP-0230-DL3D` · `ACCEPTED`
  - Kapsam/kuyruk kaydı: MOD-0230 DL-3 (kısım D) — host: Pvg:Runtime:OperationalRuntimeAuthorized=true iken Pvg:Runtime:ApprovalReference zorunlu, yoksa üretim benzeri açılış reddi; commit edilmiş her ApprovalReference değerinin onay kayıtlarında bulunduğunu ölçen CI denetimi; kabul AC-10 (ODPVG6-01)
  - Kanıt/karar: `docs/records/audits/2026-11/pvg-dl3d-acceptance-02/RECORD.md@3ed5cb00a5cbdbb595f8a349cc67e3f9b3048053`.
- [ ] **API: API: servis JWT doğrular, kiracı X-Tenant-Id = token TenantId, aktör konu claim'i, X-Diten-* yok sayılır; kabul AC-5 (ODPV…** — `WP-0230-DL4B` · `INTEGRATED`
  - Kapsam/kuyruk kaydı: MOD-0230 DL-4 (kısım B) — API: servis JWT doğrular, kiracı X-Tenant-Id = token TenantId, aktör konu claim'i, X-Diten-* yok sayılır; kabul AC-5 (ODPVG2-01); AC-6 (OD-PVG-3) kapsam dışı (WP-0230-DL4C)
  - Kanıt/karar: `docs/records/audits/2026-10/pvg-wave4-integration-01/INTEGRATION.md`.
  - Yalnız entegre; ayrı CT kabulü veya operasyon başarısı sayılmaz.
- [ ] **Ekran: ekran temeli: AC-1 _LayoutTenantShell, AC-2 [HasPermission] pvg.case-intake-triage.* (10/10 eylem), AC-3 UAS-001 yetkisiz …** — `WP-0230-DL7A` · `INTEGRATED`
  - Kapsam/kuyruk kaydı: MOD-0230 DL-7 (kısım A) — ekran temeli: AC-1 _LayoutTenantShell, AC-2 [HasPermission] pvg.case-intake-triage.* (10/10 eylem), AC-3 UAS-001 yetkisiz yanıt, AC-14 liste doğrulayıcısı (OD-PVG-10 bilinen sapma), AC-15 yedi dil resx, AC-20 sayfalama + toplam/süzülmüş sayı + sürüm taşıması ve 409 kontrollü hata
  - Kanıt/karar: `docs/records/audits/2026-11/pvg-wave7-integration-01/INTEGRATION.md`.
  - Yalnız entegre; ayrı CT kabulü veya operasyon başarısı sayılmaz.
- [x] **API: API: salt okunur GET /api/v1/pv-case-intake-triage/options; tek kaynak PvgIntakeOptionSets (rapor türü, ürün rolü, ülke 'u…** — `WP-0230-DL4D` · `ACCEPTED`
  - Kapsam/kuyruk kaydı: MOD-0230 DL-4 (kısım D) — API: salt okunur GET /api/v1/pv-case-intake-triage/options; tek kaynak PvgIntakeOptionSets (rapor türü, ürün rolü, ülke 'unknown'); JWT 401 + kiracı bağlamı (DL4B ile aynı); kiracı verisi ve izin anahtarı yok; 'options' ayrılmış yol parçası; kabul AC-34 (rev 13, SECENEK-01); ocelot.json değişmez (mevcut kayıt route'u)
  - Kanıt/karar: `docs/records/audits/2026-11/pvg-dl4d-acceptance-04/RECORD.md@3ed5cb00a5cbdbb595f8a349cc67e3f9b3048053`.
- [ ] **Ekran: kök neden: köprü camelCase üretiyor, index.l10n.js anahtarları çevirmeden yazıyor, ekran PascalCase okuyor; seçenek adları…** — `WP-0230-DL7B-R1` · `INTEGRATED`
  - Kapsam/kuyruk kaydı: MOD-0230 DL-7 (kısım B, düzeltme 1) — kök neden: köprü camelCase üretiyor, index.l10n.js anahtarları çevirmeden yazıyor, ekran PascalCase okuyor; seçenek adları ve iletiler ham kod görünüyor (AC-32). Yükleyici düzeltmesi, gerçek yükleyiciden geçen test, Options ve Detail yolunda kimlik başlığı testi; servis, Gateway, ortak dosya ve resx değişmez
  - Kanıt/karar: `docs/records/audits/2026-11/pvg-wave7-integration-01/INTEGRATION.md`.
  - Yalnız entegre; ayrı CT kabulü veya operasyon başarısı sayılmaz.
- [ ] **Ekran: form ekranı doğrulamasının açık kalan bulguları: sayfa bağlantısı testi (köprü, yükleyici, sayfa betiği sırası), RecordSav…** — `WP-0230-DL7C` · `INTEGRATED`
  - Kapsam/kuyruk kaydı: MOD-0230 DL-7 — form ekranı doğrulamasının açık kalan bulguları: sayfa bağlantısı testi (köprü, yükleyici, sayfa betiği sırası), RecordSaved köprü anahtarı, ReceivedAtUtc saniye adımı, liste ve yükleyici testleri; Edit sessiz silme servis değişikliği ister, kapsam dışı
  - Kanıt/karar: `docs/records/audits/2026-11/pvg-wave7-integration-01/INTEGRATION.md`.
  - Yalnız entegre; ayrı CT kabulü veya operasyon başarısı sayılmaz.
- [ ] **Arka uç: reddedilen kalıcı depo tesliminin dar düzeltmesi: bozuk bağlantı metninde parola sızıntısı, AC-35 (b) test adı ve ölçümü (…** — `WP-0230-DL10-R1` · `INTEGRATED`
  - Kapsam/kuyruk kaydı: MOD-0230 DL-10 (düzeltme 1) — reddedilen kalıcı depo tesliminin dar düzeltmesi: bozuk bağlantı metninde parola sızıntısı, AC-35 (b) test adı ve ölçümü (revizyon 15), doğrulamanın ucuz test boşlukları; WP-0230-DL10 commit'i üzerine; revizyon 15 onayı main'e girmeden dağıtılmaz; API sözleşmesi, 404 (AC-6) ve canlı yol kapsam dışı
  - Kanıt/karar: `docs/records/audits/2026-11/pvg-wave8-verify-02/VERIFY-DL10-R1.md`.
  - Yalnız entegre; ayrı CT kabulü veya operasyon başarısı sayılmaz.
- [ ] **Arka uç: kapalı katalog kaydı (AC-36, AC-37): PvgService açılışta gizli ve aboneliğe kapalı bildirimi (4 sayfa, 8 eylem, 5 anahtar)…** — `WP-0230-DL11` · `INTEGRATED`
  - Kapsam/kuyruk kaydı: MOD-0230 DL-11 — kapalı katalog kaydı (AC-36, AC-37): PvgService açılışta gizli ve aboneliğe kapalı bildirimi (4 sayfa, 8 eylem, 5 anahtar) Platform'un iç ucuna bir kez gönderir; değerler yalnız ortam değişkeninden, sınırlı deneme, yönlendirme izlenmez, üretim benzeri hostta çağrı yok; iki Nav çeviri anahtarı 7 dilde; AC-38 canlı ölçümü (E5) kabulde CT'nin; Platform ve Auth kodu, Gateway, seed, grant, menü, izin veren adaptör ve operasyonel kullanım kapsam dışı
  - Kanıt/karar: `docs/records/audits/2026-11/pvg-wave9-verify-01/VERIFY-DL11.md`.
  - Yalnız entegre; ayrı CT kabulü veya operasyon başarısı sayılmaz.
- [ ] **API: AC-39 (G-24): triyaj sonucu formun gönderdiği metinle (Triaged, Rejected, Duplicate; yalnız tam ad) bağlanır ve izin kapıs…** — `WP-0230-DL4E` · `INTEGRATED`
  - Kapsam/kuyruk kaydı: MOD-0230 DL-4 (kısım E) — AC-39 (G-24): triyaj sonucu formun gönderdiği metinle (Triaged, Rejected, Duplicate; yalnız tam ad) bağlanır ve izin kapısına ulaşır; enum dışı metin kapıdan önce 400, depo yazımı yok; JSON null ve sayı bugünkü gibi; değişiklik yalnız PvgCaseIntakeTriageEndpoints.cs içinde istek alanına bağlı dönüştürücü, host genelinde dönüştürücü yok; 404 ve hata zarfı (AC-6, WP-0230-DL4C), denetim (DL-5), izin kapısı adaptörleri (OD-PVG-14), G-25 ve frontend kapsam dışı
  - Kanıt/karar: `docs/records/audits/2026-11/pvg-wave10-verify-01/VERIFY-DL4E.md`.
  - Yalnız entegre; ayrı CT kabulü veya operasyon başarısı sayılmaz.

## Başka sahiplerin işi

- [ ] **Belge: Bağımlılık takibi — MOD-0019 Data Masking & Row/Field Security: kod yok (PVG'de yalnız hepsini reddeden yer tutucu); P0, k…** — `PVG-M1-06` · `DISCOVERED`
  - Kapsam/kuyruk kaydı: Bağımlılık takibi — MOD-0019 Data Masking & Row/Field Security: kod yok (PVG'de yalnız hepsini reddeden yer tutucu); P0, kişisel veri içeren PV akışları için durdurucu; PVG yazmaz (XMC-001)
  - Kanıt/karar: `-`.
  - Tarihsel kuyruk envanteri; başka modülün güncel kod/runtime durumu bu oturumda yeniden ölçülmedi.
- [ ] **Belge: Bağımlılık takibi — MOD-0018 RBAC / ABAC Authorization: büyük ölçüde yapılmış (rol/izin API ve ekranları); eksik: pv.* izi…** — `PVG-M1-07` · `DISCOVERED`
  - Kapsam/kuyruk kaydı: Bağımlılık takibi — MOD-0018 RBAC / ABAC Authorization: büyük ölçüde yapılmış (rol/izin API ve ekranları); eksik: pv.* izin kataloğu ve veri kapsamı (ABAC); PVG yazmaz (XMC-001)
  - Kanıt/karar: `-`.
  - Tarihsel kuyruk envanteri; başka modülün güncel kod/runtime durumu bu oturumda yeniden ölçülmedi.
- [ ] **Belge: Bağımlılık takibi — MOD-0021 Audit Trail Service: büyük ölçüde yapılmış (API, dışa aktarım, ekranlar); eksik: değiştirilem…** — `PVG-M1-08` · `DISCOVERED`
  - Kapsam/kuyruk kaydı: Bağımlılık takibi — MOD-0021 Audit Trail Service: büyük ölçüde yapılmış (API, dışa aktarım, ekranlar); eksik: değiştirilemezlik kanıtı, denetim kesintisinde fail-closed, kimlikli runtime testi; PVG yazmaz (XMC-001)
  - Kanıt/karar: `-`.
  - Tarihsel kuyruk envanteri; başka modülün güncel kod/runtime durumu bu oturumda yeniden ölçülmedi.
- [ ] **Belge: Bağımlılık takibi — MOD-0023 Workflow Designer (Approvals/SLAs/Escalations): büyük ölçüde yapılmış (API, tasarımcı ekranla…** — `PVG-M1-09` · `DISCOVERED`
  - Kapsam/kuyruk kaydı: Bağımlılık takibi — MOD-0023 Workflow Designer (Approvals/SLAs/Escalations): büyük ölçüde yapılmış (API, tasarımcı ekranları); eksik: PV iş akışı uçtan uca testi; PVG yazmaz (XMC-001)
  - Kanıt/karar: `-`.
  - Tarihsel kuyruk envanteri; başka modülün güncel kod/runtime durumu bu oturumda yeniden ölçülmedi.
- [ ] **Belge: Bağımlılık takibi — MOD-0028 Documentation & Evidence Management: büyük ölçüde yapılmış; FU06 başlangıç hatası kodda düzel…** — `PVG-M1-10` · `DISCOVERED`
  - Kapsam/kuyruk kaydı: Bağımlılık takibi — MOD-0028 Documentation & Evidence Management: büyük ölçüde yapılmış; FU06 başlangıç hatası kodda düzeltilmiş görünüyor ama runtime'da doğrulanmadı, paket hâlâ BLOCKED diyor; PVG yazmaz (XMC-001)
  - Kanıt/karar: `-`.
  - Tarihsel kuyruk envanteri; başka modülün güncel kod/runtime durumu bu oturumda yeniden ölçülmedi.
- [ ] **Belge: Bağımlılık takibi — MOD-0031 Evidence Linking Service: kısmi (backend dilim 1 ve Gateway route var, ekran yok); MOD-0231 ö…** — `PVG-M1-11` · `DISCOVERED`
  - Kapsam/kuyruk kaydı: Bağımlılık takibi — MOD-0031 Evidence Linking Service: kısmi (backend dilim 1 ve Gateway route var, ekran yok); MOD-0231 önkoşulu; PVG yazmaz (XMC-001)
  - Kanıt/karar: `-`.
  - Tarihsel kuyruk envanteri; başka modülün güncel kod/runtime durumu bu oturumda yeniden ölçülmedi.
- [ ] **Belge: Bağımlılık takibi — MOD-0030 Records Management (Retention/Legal Hold): kod yok; MOD-0031 önkoşulu; PVG yazmaz (XMC-001)** — `PVG-M1-12` · `DISCOVERED`
  - Kapsam/kuyruk kaydı: Bağımlılık takibi — MOD-0030 Records Management (Retention/Legal Hold): kod yok; MOD-0031 önkoşulu; PVG yazmaz (XMC-001)
  - Kanıt/karar: `-`.
  - Tarihsel kuyruk envanteri; başka modülün güncel kod/runtime durumu bu oturumda yeniden ölçülmedi.
- [ ] **Belge: Bağımlılık takibi — MOD-0041 Logging & Monitoring: kısmi (OpenTelemetry/Serilog altyapısı bazı servislerde); MOD-0230/0231…** — `PVG-M1-13` · `DISCOVERED`
  - Kapsam/kuyruk kaydı: Bağımlılık takibi — MOD-0041 Logging & Monitoring: kısmi (OpenTelemetry/Serilog altyapısı bazı servislerde); MOD-0230/0231/0232 önkoşulu; PVG yazmaz (XMC-001)
  - Kanıt/karar: `-`.
  - Tarihsel kuyruk envanteri; başka modülün güncel kod/runtime durumu bu oturumda yeniden ölçülmedi.
- [ ] **Belge: Bağımlılık takibi — MOD-0007 Decision & Rationale Log: kod yok, modül kayıt dosyasında satırı da yok; MOD-0031 önkoşulu; P…** — `PVG-M1-14` · `DISCOVERED`
  - Kapsam/kuyruk kaydı: Bağımlılık takibi — MOD-0007 Decision & Rationale Log: kod yok, modül kayıt dosyasında satırı da yok; MOD-0031 önkoşulu; PVG yazmaz (XMC-001)
  - Kanıt/karar: `-`.
  - Tarihsel kuyruk envanteri; başka modülün güncel kod/runtime durumu bu oturumda yeniden ölçülmedi.
- [ ] **Belge: Bağımlılık takibi — MOD-0262 Internal Document Repository Service: kısmi (FU01 binary store backend var; ana modülün paket…** — `PVG-M1-15` · `DISCOVERED`
  - Kapsam/kuyruk kaydı: Bağımlılık takibi — MOD-0262 Internal Document Repository Service: kısmi (FU01 binary store backend var; ana modülün paketi yok); MOD-0028 önkoşulu; PVG yazmaz (XMC-001)
  - Kanıt/karar: `-`.
  - Tarihsel kuyruk envanteri; başka modülün güncel kod/runtime durumu bu oturumda yeniden ölçülmedi.

## Ortak bileşenler

Bu bölümde ayrı kuyruk satırı yok.

## Duraklatılanlar

- [ ] **Arka uç: host açılışı ve yapılandırma: Program.cs, PvgServiceApiHost.cs, açılış testleri; kabul BG-8, BG-9 (BG-10 OD-PVG-6 Kurumsal…** — `WP-0230-DL3` · `SUPERSEDED`
  - Kapsam/kuyruk kaydı: MOD-0230 DL-3 — host açılışı ve yapılandırma: Program.cs, PvgServiceApiHost.cs, açılış testleri; kabul BG-8, BG-9 (BG-10 OD-PVG-6 Kurumsal Mimari onayından sonra); bir kayıt silinince adı belli test kırmızı (sabotaj); port tahsisi ve üretim açılışı kapsam dışı
  - Kanıt/karar: `docs/records/decisions/2026-10/pvg-mod0230-dl3-bolme-01.md`.
- [ ] **Ekran: AC-32 alan girdileri: ürün listesi (elle), farkına varma anı, olay ülkesi (yalnız 'unknown', ülke listesi yokken), rapor t…** — `WP-0230-DL7B` · `REWORK`
  - Kapsam/kuyruk kaydı: MOD-0230 DL-7 (kısım B) — AC-32 alan girdileri: ürün listesi (elle), farkına varma anı, olay ülkesi (yalnız 'unknown', ülke listesi yokken), rapor türü ve ürün rolü seçenekleri AC-34 ucundan MVC vekiliyle (11. eylem, AC-2), bildiren elle giriş, bağı kaldır; mevcut ekran metinlerinin altı dile çevirisi (CEVIRI-01, AC-15); triyaj dört ölçütü AC-28 işinde (SECENEK-01 S-5); seçiciler ve şirket alıcısı ön doldurma kapsam dışı (MOD-0290/0150/0288 sözleşmeleri)
  - Kanıt/karar: `docs/records/audits/2026-10/pvg-wave7-verify-01/VERIFY-DL7B.md@df44f6213c62aea002cd6cf330ee2c3a694b9cd8`.
- [ ] **Arka uç: yapı/test kalıcı deposu (AC-13, AC-35): ayrı Persistence projesinde Mongo deposu, tenant-first index, işlem, açılış retler…** — `WP-0230-DL10` · `REWORK`
  - Kapsam/kuyruk kaydı: MOD-0230 DL-10 — yapı/test kalıcı deposu (AC-13, AC-35): ayrı Persistence projesinde Mongo deposu, tenant-first index, işlem, açılış retleri, hazırlık, CreatedBy/UpdatedBy; revizyon 14 onayı main'e girmeden dağıtılmaz; denetim yazımı (DL-5), seed, grant ve operasyonel kullanım kapsam dışı
  - Kanıt/karar: `docs/records/audits/2026-10/pvg-wave8-verify-01/VERIFY-DL10.md@e9b8187ef93b374f7f61ca2e09ed84ed8074e709`.

## Kesin kaynaklar

- Görev kaynağı: `execution/domains/pharmacovigilance/work-packs/CT-QUEUE.tsv`.
- İnsan onayı: `docs/records/decisions/2026-11/pvg-mod0230-extra-scope-approval-01.md@a2586b89`.
- Son pack: `ea4ff430d0b2f5f07c8c2482f34311e7897452be597b69b16b822da277b6f7f4`; domain: `2411fa9500c7696b0751767be23661bc3db6c87468821ac43f7e9d56ba62ef8b`.
- Son inceleme ve CT karşılığı: `docs/records/audits/2026-11/pvg-rev21-formal-review-01/ROUND-02-DECISION.md@5976407f`.
- Dağıtım, kabul ve main merge birbirinden ayrı kapılardır; READY veya UYGUN tek başına tamamlanma değildir.
