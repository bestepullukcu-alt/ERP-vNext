---
decision_id: PVG-PLAN-2026-10-19-OWNER-DECISIONS-01
status: approved — D1 ve D2 2026-10-20 itibarıyla yürürlükte; "Karar olmayanlar" bölümü karar değildir
decided_at_local: 2026-10-20 (D1: sahibin bu oturumdaki yanıtı; D2: 2026-10-19 tarihli CEO kararı, sahip tarafından 2026-10-20'de aktarıldı); yazma onayı 2026-10-21
decided_by: D1 — depo sahibi (rol; bu kayıt ad yazmaz). D2 — CEO (rol), sahip tarafından aktarıldı; CEO'nun kendi kaydı değildir
decision_source: PVG Control Tower oturumu (Claude Code, paylaşılan ana klasörden açılıp `.claude/worktrees/pvg-ct-01`'de sürdürüldü), sahip mesajları 2026-10-20 ve 2026-10-21; D2 için sahibin aktardığı plan dosyası (aşağıda kimliği)
scope: Pharmacovigilance (PVG) teslim sırası — MOD-0230, MOD-0231, MOD-0232, MOD-0234 ve planın saydığı enabler modüller; başka domain'in belgesi bu kayıtla değişmez (XMC-001)
bound_to: dal `chore/ct/pvg-ct-01` @ 1a8758e61 (origin/main 9d44e8a1f ile yerel birleşme; push edilmedi); kuyruk `execution/domains/pharmacovigilance/work-packs/CT-QUEUE.tsv` @ 1a8758e61; kayıt satırı `pvg` origin/main'de yürürlükte (PR #166, 4728cb804)
recorded_by: PVG Control Tower (Claude Code oturumu), sahibin 2026-10-21 tarihli yazma onayıyla; bu kayıt kendi adına karar vermez, onay, paket ya da dağıtım izni değildir
---

# PVG planı 2026-10-19 — sahip kararları (yürürlükte)

Alıntılar sahibin mesajından olduğu gibi kopyalanmıştır. Kayıt kişi adı ve e-posta yazmaz; roller kullanılır (sahip,
CEO, paket sahibi). Sahip şartı (2026-10-21): "decided_by ve diğer alanlarda kişi adı ya da e-posta yok; rol yaz
(sahip, CEO)."

## Plan dosyasının kimliği (depo dışı)

- Dosya adı: `Capability_blocks_planning_v_19_OCT_2026.xlsx` (sahibin bilgisayarında; depoya alınmadı — diğer
  domain'lerin planlarını da içerir).
- SHA-256: `88e961697b076cea8d517cf74b8b3ad12f90590ceff196276c1b756c22a79397` (2026-10-20'de iki kez ölçüldü, değişmedi).
- PV sayfaları: `PVG` (hazırlık düzeltmeli teslim sırası, Seq. 1–17; temel kapı Seq. 1 "BLOCKED") ve `PVG_MVP_Plan`
  (19 Ekim 2026 önerisi: MVP 1 Case Intake, Processing & Coding = 3 iş modülü + 10 enabler; MVP 2 Signal Management
  — acil hedef; MVP 3 Full Case Processing & Regulatory Reporting; MVP 4 PV Quality & Inspection Readiness; MVP 5
  Master kimliği olmayan 9 yetenek — her biri için önce Module Change Request).
- Yetki sırası değişmez: plan sahibin planlama girdisidir; Blueprint 8.8, module pack ve DCP-004 üstündedir.

## D1 — 3 Ekim sıra kararı PV için geçersiz; 19 Ekim planı geçerli

2026-10-20, sahip:

> 2. Hayır — 3 Ekim kararı geçerli değil; 19 Ekim planı (PVG / PVG_MVP_Plan) geçerli.

- Geçersiz kılınan karar: `docs/roadmap/plans/mdm-product-master-completion-plan-2026-10-02.md` satır 59 @ origin/main
  9d44e8a1f (commit d0460541c, 2026-10-03): "Farmakovijilans bu yedisinden sonra" (MDM P0 kabul sırası).
- Etki: PVG işlerinin sırası `PVG_MVP_Plan` sayfasındaki MVP sırasına göre planlanır (MVP 1 önce). MDM kabul
  sırasının kendisi bu kayıtla değişmez; o belge MDM alanınındır ve PVG tarafından düzenlenmez (XMC-001). MDM Control
  Tower'ına bilgi notu sahibin tercihine bırakılmıştır.
- Sınır: bu karar dağıtım izni değildir. Her kuyruk satırı kendi DoR'u, prompt kapısı ve kabul kaydı ile ilerler;
  operasyonel runtime kapısı (`operational_runtime_gate: closed`) dört pakette de kapalı kalır.

## D2 — MOD-0040 Seq. 1 kapısından MVP 3'e (CEO kararı, aktarılan)

2026-10-20, sahip:

> 3. MOD-0040'ın MVP 3'e taşınması CEO kararı (19 Ekim); kayda geçirilmesini öner, ama kaydı yazmadan önce bana sor.

- Kaynak: CEO kararı, sahip tarafından aktarıldı (2026-10-20). Bu kayıt CEO'nun kendi kaydı değildir; planın
  `PVG_MVP_Plan` sayfası satır 11 ve satır 48 aynı kararı "CEO decision 19 Oct 2026" olarak yazar.
- İçerik: MOD-0040 (Blueprint 8.8 "Canonical ID & Correlation Standard") `PVG` sayfasındaki Seq. 1 temel kapısından
  çıkarılıp MVP 3'e alınmıştır; planda yalnız MOD-0244 SAE Linkage'ın Master HARD önkoşuludur. MVP 1 ve MVP 2
  MOD-0040'a bağlı değildir.
- Bu kaydın çözmediği çatışma (olgu): `execution/registries/module-id-registry.md` satır 135 MOD-0040'ı
  "Tenant Organization Foundation — deprecated alias → MOD-0288" olarak tutar; Blueprint 8.8 `Blueprint_Data` satır 41
  ise "Canonical ID & Correlation Standard" der (planın CONF-01'i; 2026-08-09 fast-track planı bulgu F-D). MVP 3'te
  MOD-0040 işi açılmadan önce bu kimlik çatışması Enterprise Architecture / Master-first kuralıyla çözülür; PVG yeni
  kimlik açmaz, kayıt dosyasını değiştirmez.

## Karar olmayanlar — açık kalanlar

1. Planın `PVG_MVP_Plan` "Main status (10 Oct)" sütunu MOD-0230/0231 için "Reference only (1–2 files)" der; ölçülen
   main (2026-10-20): MOD-0230 API + Gateway 4 route + kiracı ekranı, MOD-0231 sözleşme sınıfları + testler. Sütun 10
   Ekim main'ine dayanır; düzeltme sahibin dosyasındadır, karar değildir.
2. PV sayfalarının depo içi özetinin (yalnız PV, diğer domain'ler hariç) eklenip eklenmeyeceği — sahip kararı bekler.
3. Paket künyeleri: MOD-0231/0232 `owner: TBD` alanının 2026-10-20 sahip kararına göre doldurulması ve MOD-0230
   paketi ile DCP-004'teki kişisel e-postaların rol etiketine çevrilmesi. Sahip notu (2026-10-21): bu onay satırları
   HIGH olur (ilk onay); MOD-0230 ayrıca bağımsız inceleme ister (SOP §20.4). Ayrı ele alınacak; bu kayıt yapmaz.
4. MOD-0234 paket sahibi `TBD` — karar bekler.
5. DCP-004 pvg `## 8.1 Delivery slices` tablosu yok; `ct_route` MOD-0230 için hedef bulamaz (çıkış 1). Paket
   değişikliği, onay satırı ister; şimdilik gerekmiyor.
6. MVP 5'in 9 yeteneği: Blueprint 8.8 `Module_Change_Requests` sayfasında yalnız MCR-001/002 (PV dışı) var; her biri
   için önce MCR (Master-first). PVG kimlik açmaz.
7. Kayıt dosyasında satırı olmayan kimlikler: MOD-0007, MOD-0233, MOD-0235, MOD-0244, MOD-0258, MOD-0272. MVP 3/4
   kuyruk satırları bu kimlikleri kullanmadan önce kayıt sahibinde çözülmelidir (katı kuyruk kayıtsız kimliği reddeder).

## Yerini alan / yerine geçen

- d0460541c (2026-10-03) "Farmakovijilans bu yedisinden sonra" → PV için D1 ile geçersiz; MDM kabul sırası için
  geçerliliği MDM alanının kararıdır.
