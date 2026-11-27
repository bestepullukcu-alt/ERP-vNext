# Demand v2 açık soruları — sade liste (2026-11-27)

- **Tür:** soru listesi. **Karar değildir;** cevaplar Beste'den gelince ayrı karar kaydına yazılır ve paket revizyonlarına işlenir.
- **Kaynaklar:** MOD-0188 r2, MOD-0189 r2, MOD-0190 r2 (commit `103c6b2fe`, üçü de taslak, onay bekliyor); bağlı koşu sözleşmesi
  `execution/domains/supply-chain-execution/work-packs/WP-MOD0189-BOUND-RUN-CONTRACT.md` 1.0.0 FROZEN §10 (A-1…A-8); sahip kararları
  K-1…K-12 (`mod0188-demand-v2-field-names-owner-decisions-01.md`).
- **Ölçüm tabanı:** `origin/main` `85e3b1eec`. "Gerekçe" sütunundaki dosya / satır atıfları bu commit'tendir. Ölçülmeyen yer açıkça
  "ölçülmedi" diye yazıldı.
- **Hazırlayan:** CT oturumu (Claude). **Cevaplayacak:** Beste (MVP-4 sahibi), aksi yazılmadıkça.

## MOD-0189 (MRP) — 4 soru

| # | Beste'ye sorulacak cümle | Önerilen cevap | Gerekçe (ölçüm) |
|---|---|---|---|
| Q-1 | "Run'ın sürüm numarası (`runVersion`), kaydın teknik kilit sayacıyla aynı şey mi olsun, yoksa yalnız durum değişince artan ayrı bir sayı mı?" | **Ayrı sayı.** Yalnız durum geçişinde +1 artar; teknik `Version` ayrıca kalır. | Sözleşme "her geçişte +1" diyor ve denetim satırı `runVersionBefore` / `runVersionAfter` taşıyor (§2, §5). Teknik sayaç (`EntityBase.cs:10` `Version`) durum dışı yazımlarda da artar; o zaman denetimdeki sürümler geçişleri değil yazımları sayardı. Talep tarafı aynı ayrımı yapıyor: `DemandRevisionDraft.cs:197` `StateVersion` ≠ `Version`. |
| Q-2 | "MRP paketine diğer paketlerdeki gibi bir 'teslim dilimleri' tablosu ekleyelim mi; dilimler şu sırayla olsun mu?" | **Evet.** DL-1 = bugün main'de olan r1 kapsamı (dar dilim + PR #160 takibi); DL-2 = bağlı koşu kodu (bugünkü öneri adı DL-R1); DL-3 = MRP izleme ekranı (DL-E1); DL-4 = teslim süresi ekranı (DL-E2). | Paket kuralı her pakette dilim tablosu ister (plan yetenek kimliği ve MVP sütunlarıyla); MOD-0188'de tablo var (`MOD-0188-demand-planning.md:34-39`), MOD-0189'da yok. Onay aracı yalnız `DL-n` satırlarını sayar; "DL-R1 / DL-E1" adları araçta görünmez. |
| Q-3 | "Run çakıştığında 409, talep durumu okunamadığında 503 mü dönelim?" | **Evet:** aynı anahtar + farklı içerik, uç durumdan geçiş, `Published` olmayan revizyona bağ → **409**; talep durumu / yetki kaynağı okunamıyor → **503**; kapsam dışı → **404**; gerekçesiz iptal → **422**. Kod adları: `IDEMPOTENCY_KEY_REUSED`, `RUN_STATE_CONFLICT`, `DEMAND_REVISION_NOT_PUBLISHED`, `DEMAND_STATUS_UNAVAILABLE`, `VALIDATION_FAILED`. | Üretici taslağı aynı aileyi kullanıyor (`demand-v2.openapi.yaml` uçları 400 / 401 / 403 / 404 / 409 / 503 bildiriyor, `:475-478`, `:505-511`, `:533-539`); bugünkü MRP ucu kaynak doğrulanamayınca 503 dönüyor (`MrpCalculationController.cs` son satırlar). `IDEMPOTENCY_KEY_REUSED` INVENTORY ve GRN'de aynı anlamla kullanılıyor. Kod adları önerimdir. |
| Q-4 | "Kalıcı run gelince bugünkü 'anlık hesap' ucu ne olsun: kalsın mı, kalksın mı?" | **Şimdilik kalsın,** `mrp.runs.read` altında, kayıt yazmayan "önizleme" olarak. MRP izleme ekranı (DL-3) gelince yeniden bakılır. | Uç yalnız okur, kayıt yazmaz (`MrpCalculationController.cs:9-29`). Gateway'de `mrp` rotası yok, Web'de tüketen yok (ölçüldü: `ocelot.json`'da `mrp` geçmiyor, `frontend/Diten.Web` içinde `mrp/calculations` yok); tek kullanıcısı test ve canlı kanıt (`MrpLiveCalculationTests.cs`, `MOD0189-pr160-live-evidence.md`). Kaldırmak bir şey kazandırmıyor, `create`'e taşımak okuma ucuna yazma izni ister. |

## MOD-0188 (Talep planlama) — 4 soru

| # | Beste'ye sorulacak cümle | Önerilen cevap | Gerekçe (ölçüm) |
|---|---|---|---|
| Q-5 | "MRP, run'a bağlı eski talep içeriğini okurken 'okuyan kim' sayılsın: isteği yapan kullanıcı mı, servisin kendisi mi?" | **İstek içindeyse isteyen kullanıcı** (`demand.plans.consume` ondan aranır). İstek dışında sistemin kendisi devam ettiriyorsa (askıdan dönüş) **servis içi sistem aktörü**, yalnız kalıcı bağdaki seri için; ayrı token yok. | Sözleşme §7 madde 1 "isteyen aktör" diyor. PlanningService'te izin bugün kullanıcı token'ından ölçülüyor (`RevisionReadController.cs:13-52` `[HasPermission("demand.plans.consume")]`); servis token'ı altyapısı (MOD-0033-FU02) yok. Bağ okuması servis içinde (K-7), HTTP ucu yok. |
| Q-6 | "Talep tarafında depo kimliği her yerde UUID biçiminde mi zorlansın?" (A-6 ile aynı soru) | **Önce say, sonra karar ver:** geliştirme ve test verisinde geçersiz UUID sayısı ölçülür; sıfırsa taslakta `format: uuid` + yazımda doğrulama, değilse metin kalır (tüketici zaten sınırda UUID'ye çeviriyor). | Üretici bugün metin taşıyor (`InternalSnapshotReadContracts.cs:18,34,40,47`; taslak `demand-v2.openapi.yaml:102` `type: string, minLength: 1`); MRP tarafı UUID (`MrpLiveCalculation.cs:27`, `LeadTimeContracts.cs:10`). Özet sıralaması depo kimliğinin metin sırasına bağlı (`demand-v2.openapi.yaml:27`, `:54-56`): biçim değişirse baytlar yeniden yazılmamalı. Verideki geçersiz sayısı **ölçülmedi**. |
| Q-7 | "Talep revizyonu geçersiz kılınırken girilen 'iş etkisi' açıklaması kayıtta kalsın mı? (Olaydan çıkarılıyor.)" | **Kayıtta kalsın,** yalnız olaydan çıksın. | Kod bugün iş etkisini zorunlu alıp kayda yazıyor (`InvalidationMongoStore.cs:59-64`, `:183`); bu, geçersiz kılmanın gerekçe kanıtıdır. K-9 yalnız olay şemasından çıkarıyor (`demand-v2.events.schema.json:154`, `:173`); kayıttan silmek ayrı bir veri kaybı kararı olurdu. |
| Q-8 | "Talep tarafının bağlı koşu kanıtları (5 test grubu) hangi dilimde yapılsın?" | **Yeni bir DL-3'te.** A-7 (olay şeması ve outbox çevirisi) aynı dilime girer. | MOD-0188 DL-1 ve DL-2 onaylı kapsamdır (`MOD-0188-demand-planning.md:38-39`); kanıtları DL-1'e eklemek kabul edilmiş dilimi yeniden açar. Kanıtlar MRP'nin DL-2'sinden önce yeşil olmalı (A-8 ortak kapı). |

## MOD-0190 (S&OP) — 4 soru

| # | Beste'ye sorulacak cümle | Önerilen cevap | Gerekçe (ölçüm) |
|---|---|---|---|
| G-2 | "S&OP'nin 'talep planı / sürüm / özet' alanları Demand v2'de hangi alanlara denk gelsin?" | `demandPlanId` = `revisionId`; `demandPlanVersion` = **`contentVersion`** (içerik sürümü, değişmez); `sourceChecksum` = özet (G-3'teki biçimle). `stateVersion` taşınmaz. | S&OP imzası "hangi içeriği onayladım" sorusunu cevaplar; içerik sürümü değişmez, durum sürümü ise `Published → Superseded` gibi geçişlerde artar ve imza zamanla "farklı" görünürdü. Bağlı koşu da içerik sürümünü ayrı taşır (`demandContentVersion`, sözleşme §2). |
| G-3 | "S&OP–Kapasite sözleşmesinde yeni sürüm açalım mı, yoksa özet tanımını mevcut metin alanına mı koyalım?" | **Yeni sürüm açmayalım.** `sourceChecksum` metnine tanım öneki yazılır: `MOD0188-CANONICAL-2/SHA-256:<digest>`. Tipli alan gerekirse sonra eklemeli sürümle. | Sözleşme 3.1.0 FROZEN (pakette "3.0.0" yazıyor; ölçüm `sandop-capacity.openapi.yaml:11-12` → 3.1.0) ve üç alan da sınırsız `string` (`:743-748`, `:797-805`); örnek zaten önekli (`sourceChecksum: sha256:ee56d4f9a3c8`, `:147`). Yeni sürüm MOD-0192 ile ortak değişiklik ister. |
| G-4 | "S&OP talep planını nasıl okusun: Gateway üzerinden kullanıcının kendi yetkisiyle mi, servisten servise mi?" | **Gateway üzerinden, kullanıcının token'ıyla,** izin `demand.plans.consume` (S&OP planlayıcı rolüne verilir). | Gateway'de `/api/v2/demand/{everything}` rotası var (`ocelot.json:11-17`). Aynı desen SupplyChain'de (`InventoryMovementClient`) ve MOD-0142-FU01 S-1 kararında kullanılıyor; servis token'ı altyapısı yok. Kullanıcı yetkisi zaten S&OP ekranındaki kişiye bağlı. |
| G-6 | "S&OP'de imzalanmış bir plan, kaynak talep revizyonu sonradan 'yerine yenisi geldi' ya da 'geçersiz' olursa ne anlama gelsin?" | **Yerine yenisi geldi (`Superseded`):** imza geçerli kalır, ekranda "daha yeni talep revizyonu var" bilgisi. **Geçersiz (`Invalidated`):** imza silinmez ama "kaynağı geçersiz" işaretlenir ve o plan bir sonraki adımda kullanılmadan önce yeniden imza ister. | S&OP seri saklamıyor, yalnız kaynak kimliği ve özeti tutuyor (MOD-0190 r2 §22). `Superseded` normal yaşam döngüsü, içerik hatalı değil; `Invalidated` içeriğin hatalı olduğunu söyler (neden sözlüğü `ContentIntegrity` vb., `demand-v2.openapi.yaml:1044`). Sessiz silme ya da sessiz geçerlilik ikisi de yanlış iz bırakır. |

## Bağlı koşu sözleşmesi açık noktaları A-1…A-8

A-1…A-4 ve A-8 sözleşmeye göre (K-12) **teknik tasarımda ölçülerek** gelir; sahipten sayı istenmez. Yine de sade cümleleri aşağıdadır,
ölçüm gelince bu cümlelerle sorulur.

| # | Sade cümle | Önerilen cevap | Gerekçe |
|---|---|---|---|
| A-1 | "MRP, talebin hâlâ geçerli olduğunu ne sıklıkla yeniden kontrol etsin?" | Sayı teknik tasarımda ölçülür (K-12); ölçüm gelene kadar sayı yazılmaz. Sahibe soru değil. | Olaylar yalnız işarettir, karar durum sorgusundan (§8, §9); sıklık durum servisinin yükünü belirler, ölçülmedi. |
| A-2 | "Talep durumu okunamazsa bir run en çok ne kadar beklesin?" | Teknik tasarımda ölçülür; süre dolunca run `Stopped` değil `Suspended` kalır ve görünür olur (öneri). | K-12. `Suspended → Stopped` geçişi sözleşmede yok (§3); süre yalnız alarm eşiğidir. |
| A-3 | "Sonucu güvenilmez olan bir run ekranda ne kadar görünsün?" | Teknik tasarımda ölçülür; silinmez, süre yalnız listeden düşme içindir (öneri). | K-12; denetim izi kalıcıdır (§5). |
| A-4 | "Aynı isteğin tekrarını tanımak için hangi alanlar karşılaştırılsın?" | Teknik: istek gövdesinin kanonik JSON'u (kiracı, şirket, çevrim, dönem, SKU, depo) + isteyen kullanıcı; SHA-256 küçük harf onaltılık. Sahibe soru değil. | Sözleşme adı ve davranışı sabitledi, alan kümesini bıraktı (§6). Kullanıcıyı katmak başka kişinin aynı anahtarla eski run'ı almasını engeller (GRN'deki kural, MOD-0142-FU01 Bölüm 4). |
| A-5 | "Run denetim kaydının alan listesi (sözleşme §5 tablosu) bu hâliyle uygun mu?" | **Evet.** Kişi yalnız UUID ile, ad / e-posta yok; talep satırı ve miktar yazılmıyor. | Tablo öneri listesinden alındı, numaralı karar değil (§5 son not); içeriği AUD-001'in kişisel veri sınırına uyuyor. **Bu, Beste'nin teyidini isteyen tek A maddesi.** |
| A-6 | (Q-6 ile aynı) | Q-6 | Q-6 |
| A-7 | "Olay şemasındaki eski neden adları ve iş etkisi alanı ne zaman düzeltilsin?" | MOD-0188'in yeni DL-3'ünde (Q-8), Demand v2 dondurulmadan önce. Sahibe soru değil; dilim seçimi Q-8'dir. | Şema bugün eski adları ve zorunlu `businessImpact`'i taşıyor (`demand-v2.events.schema.json:150-176`); outbox çevirisi de (`DemandRevisionOutboxPayload.cs:54`, `:74`). |
| A-8 | "Gerçek Mongo testleri (yarış, geri alma, yeniden başlatma, özet bozulması, olay kaybı) geçmeden eski içerik okuma yolu kapalı kalsın mı?" | **Evet** (sözleşme zaten böyle diyor). Testler MOD-0188 DL-3 ve MOD-0189 DL-2'de. | §10 A-8; bu testler olmadan `Superseded` içerik okuması açılmaz. |

## Beste'den istenen (özet)

Cevap bekleyen sahip soruları: **Q-1…Q-8, G-2, G-3, G-4, G-6 ve A-5** (13 madde; A-6 = Q-6). A-1…A-4, A-7 ve A-8 teknik tasarım /
dilim işidir. Cevaplar tek karar kaydına yazılır; ardından MOD-0188 r3, MOD-0189 r3 ve MOD-0190 r3 ile bağımsız inceleme ve onay
satırları gelir. Bu liste kod izni değildir.
