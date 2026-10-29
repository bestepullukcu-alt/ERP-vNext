# MOD-0251 — bağımsız inceleme isteği 04 (SOP §20.4 / CT-APR-02)

> Kayıt S-94'ten (kart, 2026-10-29) sonra yazıldı; istek Codex'e S-94 main'e girmeden önce gönderildi. Prompt bloğu
> sohbette verilen blokla birebir aynıdır; ZIP kullanılmadı, Codex dalı GitHub'dan sabit commit'le okudu.

- **Hedef:** `execution/domains/human-capital-management/module-packs/MOD-0251-core-hr-employee-master.md` @ sha256 `7d04a408051d79d4c0075522b7eda41dcc84860e1ab1f4770ee5f020c1633d9d` (revizyon 4)
- **Kaynak commit:** `6febba2d303cafeb6ba0c3822e127d4071f1c0b0` · dal `chore/ct/hcm-employee-ct-01` (push edildi)
- **Risk:** HIGH — paketin ilk onayı (kart: hiç onay satırı olmamış paketin ilk onayı HIGH); kiracı verisi, kişisel veri, yetki, HCM → MOD-0021 Audit Trail denetim sınırı
- **Önceki sürüm ve incelemeler:** revizyon 3 @ `03d9e80f…` (commit 939a30673); inceleme 01 (260d1151), 02 (3ebb4e8d), 03 (a9ac2123) — üçü UYGUN DEĞİL
- **Bu sürümün nedeni:** inceleme 03 F-26 (ENGEL): taslak yazımlarının merkezi denetimi DL-7'ye (E5) taşındı (sahip sözü `ayri-dilim`); DL-2 kod düzeltmeleri E4'te kaldı; ara inceleme düzeltmeleri; künyede paket sahibi `ali.tufanoglu`
- **Yetki:** sahibin sözü "push-6febba2" ve "codexden ıncelem mı mı sıtıyorsaun eger oylese bun ct kuralların gore dogru ver" (2026-10-29)
- **İsteyen / hazırlayan / tarih:** depo sahibi · CT · Çalışan kaydı · 2026-10-29
- **İnceleyen:** Codex oturumu · istenen en güçlü mevcut model, effort high · yanıtın başına model, effort, oturum kimliği (yanıt: "verilemiyor")
- **ZIP:** yok — kaynak GitHub dalı @ 6febba2d3
- **Bulgu dosyası:** `docs/records/reviews/MOD-0251/2026-10-29-7d04a408.md` (sonuç UYGUN)
- **Prompt bloğunun SHA-256'sı:** `fc39807ab15d91ea108092be531ecb3f72d0016a2ffa8d433262e6ab006d002a`

## İnceleme prompt'u (inceleyen oturuma olduğu gibi iletildi)

```text
HEDEF: Codex — MOD-0251 Core HR / Employee Master module pack bağımsız incelemesi 04 (SOP §20.4 / CT-APR-02; revizyon 4). İnceleyensin; paketi yazan ya da denetleyen oturum değilsin. Dosya düzenleme, kod yazma, staging, commit, push, PR yok. Tek çıktın aşağıdaki bulgu metnidir.

Kaynak: GitHub deposu bestepullukcu-alt/ERP-vNext, dal chore/ct/hcm-employee-ct-01 @ 6febba2d303cafeb6ba0c3822e127d4071f1c0b0. Önce fetch et; uzak dal ucu bu sha değilse dur ve bildir. Bütün belgeleri ve kodu bu sabit commit'ten oku (çalışma ağacından değil).
Hedef belge: execution/domains/human-capital-management/module-packs/MOD-0251-core-hr-employee-master.md @ sha256 7d04a408051d79d4c0075522b7eda41dcc84860e1ab1f4770ee5f020c1633d9d — tutmuyorsa dur ve bildir.
Önceki sürüm: revizyon 3 @ sha256 03d9e80fdf82af1daea3c518f3becbcd9ade25e6352d361a8795b25a0afb0122 (commit 939a30673). Farkı incele: git diff 939a30673 6febba2d3 -- <hedef paket>.
Model · Effort (istenen): en güçlü mevcut model · high — kullandığın modeli, effort'u ve oturum kimliğini yanıtın başına yaz; ortamın vermiyorsa "verilemiyor" yaz, uydurma.

Önceki resmî incelemeler: docs/records/reviews/MOD-0251/2026-10-27-edb2c321.md (özet 260d1151), 2026-10-29-47968562.md (3ebb4e8d), 2026-10-29-03d9e80f.md (a9ac2123). İnceleme 03'te tek açık bulgu F-26 ENGEL'di (DL-2'nin merkezi denetimi E4 ile kabul ediliyordu; HCM → MOD-0021 sınırı E5 ister).
Revizyon 4'ün yanıtı (sahip kararı ayri-dilim): taslak yazımlarının merkezi denetimi kendi dilimi DL-7'ye (E5) taşındı; DL-2 kod düzeltmeleri E4'te kaldı ve denetim kararlarını beklemiyor.
Ara notlar: docs/records/audits/2026-10/hcm-emp-03-interim-01/INTERIM-REVIEW-01.md — üç alt ajan ara incelemesi (AK-27…AK-50). Resmî inceleme DEĞİLDİR; AK numaralarını bulgu numarası olarak kullanma. Düzeltmelerinin doğru olup olmadığını bağımsız ölçebilirsin.

Önce oku: hedef paket (tamamı, özellikle künye, Revision 4 notu, Open Decisions, Audit Fail-Closed Rules, Teslim dilimleri ve kabul eşleme tablosu); docs/guides/operations/control-tower-sop.md §20.4 ve §23.1 (E1–E5; modüller arası sınır E5); execution/approvals/POLICY-REVIEW-START.md; .antigravity/rules/audit-trail-standard.md (§5 K4); AGENTS.md Çekirdek.
Kod: services/Diten.HcmService/** (özellikle TenantResolutionMiddleware.cs, DraftAuditService, komut işleyicileri); services/Diten.Building.Blocks/src/Diten.BuildingBlocks.CentralAudit/** (CentralAuditOutboxRecord.cs, CentralAuditRecorder.cs, PlatformCentralAuditClient.cs, ICentralAuditScope.cs); örnek kullanım services/Diten.InventoryService (InventoryCentralAuditScope.cs, AddCentralAudit çağrısı ve yapısal koruma testi); Platform AuditEnums.cs; gateway/Diten.ApiGateway/ocelot.json HCM rotaları.
Derleme ve test koşamazsın ya da koşmadıysan öyle yaz; ölç ve ÖLÇTÜKLERİNİ yanıtın sonunda tek "Ölçülenler:" satırında (| işareti kullanmadan) yaz.

İnceleme derinliği (risk HIGH — kiracı verisi, kişisel veri, yetki, modüller arası denetim sınırı):
- F-26 kapanışı: DL-7'nin gerekli kanıt düzeyi E5 mi; DL-7 çıkışı gerçek bir HCM yazımının Platform merkezi günlüğünde kiracı, aktör, korelasyon ve nesneyle görüldüğünü ve teslim kesintisinden sonra kurtarmayı ölçüyor mu; DL-2'de (E4) denetim teslimine dayanan bir kabul kalmış mı?
- DL-7 ölçülebilirliği: istenen alanlar ortak ileticinin gerçekten taşıdığı alanlarla (Category, Operation, EntityType, EntityId, RequestType, aktör, korelasyon) ölçülebilir mi; outbox durumları (Pending, AwaitingCommand, Delivered, Rejected, Abandoned) ve teslim sonuçları (Retry, Rejected) doğru adlandırılmış mı; RequireEnlistment açıkken enlist etmeyen başarı davranışı ve yapısal koruma testi doğru yazılmış mı?
- Kiracı ve korelasyon: HCM kiracıyı X-Tenant-Id başlığından alıyor, ICentralAuditScope kiracının imzalı belirteçten gelmesini istiyor; HCM'de korelasyon bağlamı yok. Paket bunu DL-7 kapsamına (HCM kapsam bağdaştırıcısı) ve açık karar BL-324'e doğru bağlıyor mu? Kiracı claim'i olmayan belirteç + başlık durumu ölçülüyor mu?
- Dilim sınırları: DL-2 ve DL-7 aynı yazma yoluna dokunuyor; P2 kapanışı için birleşik koşu kuralı yeterli mi; DL-7'nin DL-2'ye bağımlılığı doğru yazılmış mı?
- Açık kararlar: AE-1…AE-5, BL-324, K-1…K-9, F-5 / F-12 / F-13, OWN, HRF — her biri doğru sahip ve doğru dilime bağlı mı; AE-1 için paket K4'ün kalktığını iddia etmeden ilerliyor mu?
- Künye: owner satırı (ali.tufanoglu, sahip kararı 2026-10-29) ve data_mode; künye kendi içinde ve onay durumuyla tutarlı mı?
- Revizyon 3 → 4 farkında yeni bir çelişki, eskimiş ifade ya da ölçülemeyen kabul var mı?
- Önceki bulgular: inceleme 03'ten kapananları "Kapananlar" satırına yaz; hâlâ açık olanı yeni numarayla (F-27'den) yeniden yaz ve metninde önceki numarasını belirt.

Çıktı (başka bir şey yazma; kişi adı ve e-posta yok, rol yaz):
# MOD-0251 — bağımsız inceleme
Model: <model>. Effort: <effort>. Oturum kimliği: <kimlik ya da verilemiyor>.
hedef: execution/domains/human-capital-management/module-packs/MOD-0251-core-hr-employee-master.md @ 7d04a408051d79d4c0075522b7eda41dcc84860e1ab1f4770ee5f020c1633d9d
değerlendirilen olumsuz incelemeler: 260d1151, 3ebb4e8d, a9ac2123
Kapananlar (inceleme 03, a9ac2123): <kapanan önceki bulgu numaraları, virgülle; yoksa bu satırı yazma>
F-27 | ENGEL | <bulgu; paket satır numarası ve dayanak dosya:satır> | açık | - | -
Sonuç: <UYGUN ya da KOŞULLU ya da UYGUN DEĞİL> (tek değer, | işareti yok)
Ölçülenler: <tek satır, | işareti yok>
Kurallar: seviye adını (engel, orta, düşük) bulgu satırı dışında büyük harfle yazma; bulgu satırında seviye BÜYÜK harfle (ENGEL, ORTA, DÜŞÜK); `F-` ile başlayan satır yalnız bulgu satırıdır; `F-n@özet` biçimi yazma; kapanış yalnız açık / kapandı / kabul — onaylayan (ENGEL için yalnız açık); UYGUN sonucunda ENGEL satırı olamaz; bulgu yoksa F satırı yazma ve "Sonuç: UYGUN" de; açık ORTA ve DÜŞÜK için sahip (rol) ve son tarih (YYYY-AA-GG) yazılır; Sonuç satırı gerekçesiz; düz metin satırlarında | işareti kullanma.
```
