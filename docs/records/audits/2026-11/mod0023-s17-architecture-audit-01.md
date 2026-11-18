# MOD-0023 §17 — mimari denetim raporu 01 (taslak)

- Tarih: 2026-11-02 · durum: **taslak** (CT okuması ve sahip onayı bekler; onay, birleştirme ya da dağıtım izni değildir)
- Kapsam: WF-S17-DL1 (kayıtları silmeme, ek okuma) ve WF-S17-DL2 (aynı anahtar yanıtı) — Hazine isteği TRM-15
- Ölçüm tabanı: dal `chore/ct/pss-wf-ct-01` @b78dcb0b5 (DL-1 `d30e93c36` ve DL-2 `1e5615697` + `07caa63c1`, `3ddf6d8eb` birleşimiyle içeride)
- **Yöntem: yalnız okuma.** Bu raporun yazımında test, derleme ya da Mongo koşulmadı. "Sabit" denen her şey işçi ve doğrulayıcı kayıtlarının
  (`docs/records/audits/2026-10/mod0023-s17-inspection-01/`) ölçümüne dayanır; kod satırları bu rapor için yeniden okundu. Çıkarımlar "(okumadan çıkarım)" ile işaretlidir.

## Özet

| Başlık | Durum | Neye dayanır |
|---|---|---|
| Kiracı izolasyonu | Korunuyor | Yeni okuma kiracı koşulunu taşır; anahtar aramaları `ExecutionFilter`'dan geçer; başka kiracı 404 (geçişte) ya da boş liste (by-objects). Gerçek Mongo testleri; geçiş kiracı süzgeci sabotajı DL-2 sonunda kapandı. |
| Yetki | Değişmedi | Hiçbir rota, fiil ya da `[HasPermission]` değişmedi; yeni izin yok. |
| Silmeme | Üç tip korunuyor; dördüncü depo **açık** (S17-8) | Üç `DeleteAsync` reddi + davranış testleri + iki mimari bekçi. |
| Yeni bekçi | Eklendi (`WorkflowRetentionSourceGuardTests`), kaçış yolları belgeli | 40/40; G1…G8 sabotajı işçi tarafından kırmızı. |
| Mimari süit | 6 kırmızı **iş öncesinden var**, bu işin farkı onlara dokunmaz | taban 176: 170/6; son 216: 210/6; parmak izi tabanla aynı. |
| Kapanış | **TRM-15 açık** | AC-W3/W15/W17/W18 kısmi; O-S17-1 açık. |

## 1. Kiracı izolasyonu

Çekirdek kural: kiracı verisine dokunan her sorgu `TenantId` ile sınırlanır; kiracılar arası erişim 404 döner; sabit/varsayılan kiracı yok.

| Nokta | Okuma | Kanıt |
|---|---|---|
| Yeni okuma `ListByObjectIdsAllRowsAsync`: kiracı koşulu açıkça yazılı, yalnız `IsDeleted == false` düşer | `Filter.Eq(x => x.TenantId, TenantContext.TenantId)` | `services/Diten.Platform/src/Diten.Platform.Infrastructure/Persistence/Repositories/WorkflowRepositories.cs:194-209` |
| Çözülmemiş kiracıda iki okuma da aynı hatayı verir; `Guid.Empty`'de başka kiracı satırı gelmez | Gerçek Mongo, işçi sabotajı dışı doğrulama | `DL1-DOGRULAMA-01.md` "Kiracı koşulu" |
| Başlatma anahtar araması | `ExecutionFilter` (kiracı + `IsDeleted == false`) | `WorkflowRepositories.cs:277-283`; `services/Diten.Platform.Common/src/Diten.Platform.Common/Persistence/Repositories.cs:75-79` |
| Geçiş anahtar araması | `ExecutionFilter` | `WorkflowRepositories.cs:863-875` |
| Başka kiracı aynı görev kimliği ve anahtarla geçiş gönderir | **404 `NOT_FOUND_NON_LEAKAGE`**, 403/200/409 yok, satır yazılmaz | `services/Diten.Platform/src/Diten.Platform.Application/Features/Workflow/Handlers/CommandHandlers/WorkflowTaskTransitionSupport.cs:70-78`; test `WorkflowCandidateDecisionMongoTests.S17.cs:870` (4 eylem × aynı/başka aktör = 8 durum, boş olmama kontrollü) |
| `by-objects`: başka kiracının nesnesi | 200 ve **boş liste** (404 değil), bayrak açık/kapalı; ortak nesne kimliğinde başka kiracı satırı gelmez | `WorkflowRetentionReadMongoTests.cs:197, 216` |
| `history` / `instances/{id}`: başka kiracının örneği | 404 `NOT_FOUND_NON_LEAKAGE` | `WorkflowRetentionReadMongoTests.cs:230` |
| Sabit/varsayılan kiracı | Eklenen üretim kodunda görülmedi (fark okundu; otomatik tarama yok) | fark okundu |

**Bulgu K-1 (kapandı):** geçiş anahtar okumasındaki kiracı süzgecinin guard'ı yoktu (DL-2 doğrulama 01 B-2: süzgeç kaldırılınca sabotaj yeşildi; kod DL-2 öncesine aitti, DL-2 ona iki yeni çağrı ekledi). `07caa63c1` testi ekledi; işçinin sabotajında `ExecutionFilter` kaldırılınca 8 satır kırmızı (409 / 403 döner). **CT bu sabotajı yeniden koşmadı**, test metnini ve farkı okudu (`DL2-DOGRULAMA-02.md`).

**Not K-2 (karar değil, görünürlük):** `by-objects` ve `history` artık `idempotencyKey` döndürür. Anahtar önceden okunmuyordu. Anahtar serbest metin olabilir (Diten.Web'de düzenlenebilir alan; tüketici tablosu 11a) ve kişisel veri taşıyabilir; D32/3 anahtarı olduğu gibi bırakır, bu soru O-S17-1'dedir. Okuma `instances.view` izniyle ve aynı kiracıyla sınırlıdır.

## 2. Yetki

| Nokta | Okuma | Kanıt |
|---|---|---|
| `by-objects` (`includeDeleted` dahil), `history`, `instances/{id}` | `platform.workflow.instances.view`; parametre eklendi, izin ve rota aynı | `services/Diten.Platform/src/Diten.Platform.API/Controllers/WorkflowDefinitionsController.cs:121-131` |
| Görev eylemleri | `tasks.approve/reject/delegate/request-info/cancel`; aktör oturumdan bağlanır, başkası `WORKFLOW_ACTOR_MISMATCH` | `CTRL:197-292, 301-330` |
| `includeDeleted` | Yeni izin yok (D32/6): silinmiş satır, çağıranın zaten görebildiği aynı kiracı satırıdır | paket §17.8/7 |
| İzinsiz çağıran üç okumada reddedilir | tel üstünde test; `[HasPermission]` kaldırılınca kırmızı | `DL1-DOGRULAMA-01.md` (işçinin denemediği sabotaj: by-objects `[HasPermission]` yok → üç test kırmızı) |
| Başka aktör için aynı anahtar | 403 `WORKFLOW_ACTOR_DENIED`; **Cancel için yeni** (önce 200) | `WorkflowTaskTransitionSupport.cs:1087-1091, 651` |

Yetkisiz kullanıcıya sayfa iskeleti çizme kuralı (UAS-001) bu işte etkilenmez: ekran değişmedi.

## 3. Silmeme guard'ları

Politika (§17.7/2): `WorkflowInstance`, `ApprovalTask`, `WorkflowTransitionLog` silinmez, süresi dolmaz. Çekirdek kural 2 "silme yalnız soft delete" der; bu üç tip için politika **daha sıkıdır** (soft delete de reddedilir). Bu bir gevşeme değil daralmadır.

| Katman | Ne yapar | Kanıt |
|---|---|---|
| Üç `DeleteAsync` reddi | `NotSupportedException`, yazmadan | `WorkflowRepositories.cs:152, 486, 836` |
| `Platform.Common` | Değişmedi (`git diff` boş, ölçüldü); devralınan yol başka modüller için kalır | `Repositories.cs:35`; `DL1-DOGRULAMA-01.md`, `DL2-DOGRULAMA-01.md` |
| Arayüzler | Silme üyesi eklenmedi; tek yeni üye okuma, varsayılan gövdesi fırlatır | `IWorkflowRepositories.cs:172-176` |
| Davranış testleri (gerçek Mongo) | Her depo, türemiş ve taban başvurusu üzerinden reddeder, satır değişmez | `WorkflowRetentionReadMongoTests.cs:72, 81, 90` |
| Eski bekçi `WorkflowRetentionGuardTests` (DL-1) | Üç depo, arayüzler, manifest, `Platform.Common`; 30/30 | `tests/architecture/TenantArchitecture.ArchitectureTests/WorkflowRetentionGuardTests.cs` |
| Yeni bekçi `WorkflowRetentionSourceGuardTests` (DL-2) | Aşağıda §4; 40/40 | `.../WorkflowRetentionSourceGuardTests.cs` |

DL-1 sabotajları (işçinin denemediği, doğrulayıcı koştu, hepsi geri alındı): `WorkflowTransitionLogRepository` override yok → kırmızı; `WorkflowInstanceRepository` override yok → kırmızı; history `IdempotencyKey` null → kırmızı; by-objects `[HasPermission]` yok → kırmızı (`DL1-DOGRULAMA-01.md`).

**Bulgu S-1 (açık, kayıtlı, S17-8):** `WorkflowStartRecoveryStore` (dördüncü türemiş depo, `TenantRepository<WorkflowInstance>`) devralınan `DeleteAsync`'i taşır; gerçek Mongo'da `IsDeleted = true` yazar, üretimde çağıran yok (DL-1 doğrulama B-2). Override yazılmadı (paketin izinli yolu değil; sahip kararı `mod0023-s17-owner-decisions-05.md`). Yol, bekçiyle kapalı tutulur; kapatma paketin bir sonraki revizyonunda. **AC-W15 bu yüzden kısmi.** Gerileme değerlendirmesi: bugün Platform `src` altında üç tipe silme ya da işaret yazan üretim yolu bulunmadı (arama); yeni yol bekçi tarafından yakalanır.

**Bulgu S-2 (kapandı DL-2'de):** DL-1 bekçisi dördüncü deponun gövdesini ve `Features/Workflow` içindeki işaret yazımını taramıyordu (B-1 ORTA). `WorkflowRetentionSourceGuardTests` bunu kapatır.

## 4. Yeni mimari bekçi: `WorkflowRetentionSourceGuardTests`

Saf fonksiyonlar (metin girer, bulgu çıkar); sabotaj korunan dosyaya değil, bekçinin kaynak-metin girdisine geçici metin ekler. Kurallar (dosya başlığı, satır 8-39):

1. `WorkflowStartRecoveryStore`: silme çağrısı, `DeleteAsync` çağrısı ya da silme işareti yazımı yok.
2. Tarama kümesi (`Features/Workflow` altındaki her `.cs` + `Infrastructure` altında üç tipi ya da `PlatformCollections` üyelerini bütün sözcük olarak anan her `.cs`): `DeleteOne`/`DeleteMany`/`FindOneAndDelete`/`DeleteOneModel`/`DeleteManyModel`/`base.DeleteAsync` yok.
3. Aynı kümede silme-işareti yazımı yok (`.Set(x => x.IsDeleted`, `.Set(x => x.DeletedAt`, `Set("IsDeleted"`, `Set(nameof(…))`, `new BsonDocument("IsDeleted", true)`, `IsDeleted = true`, `DeletedAt =` null dışı).
4. `.Set(…)` işareti, en yakın önceki `Builders<T>` üç tipten biri ya da `BsonDocument` ise sayılır; başka tip sayılmaz (SLA kuralı, kiracı alanı gibi yasal yazımlar); öncesinde `Builders<T>` yoksa sayılır (kapalı kalır).
5. Bildirim ve okuma bulgu değildir (`bool IsDeleted = false`, `Filter.Eq(x => x.IsDeleted, false)`).
6. Yerinde takma ad güncellemesi serbesttir (§17.8/4): `.Set(x => x.ActorId, …)`, `UpdateOneAsync`, `ReplaceOneAsync`; pozitif kontrollerle sabitlenir.
7. `Infrastructure` altında üç tipi genel taban argümanı olarak alan her sınıf ve her açık genel `TenantRepository<T>`/`RepositoryBase<T>`/`HybridRepository<T>`/`GlobalRepository<T>` türevi listelenir; beklenen küme dört sınıftır, beşincisi kırmızıdır.

Kanıt: işçi, G1…G8 sabotajını bekçinin girdisine ekleyerek kırmızıya çevirdi ve geri aldı (sha aynı, geri alma sonrası yeşil); kalıcı `[Theory]` satırları ve pozitif kontroller yeşil. DL-2 doğrulaması bekçiyi 40/40, eskisini 30/30 ölçtü. **CT G sabotajlarını yeniden koşmadı.**

**Bekçinin bilinen kaçış yolları (kendi başlığında yazılı; metin okur, sözdizimi ağacı okumaz):** using takma adı; yansıma ya da genel yardımcıyla silme; üç ad için başka dosyada çıplak `GetCollection<BsonDocument>("workflow_instances")`; değişkende tutulan `Builders<T>` yüzünden yanlış tip okuması; `Features/Workflow` dışındaki Application yazımları (Tasks, TimeEntry, WorkAggregation taranmaz). DL-1 doğrulama B-3 (`Set("IsDeleted", true)` gibi yazımlar) DL-2 bekçisinde yazıldı. Eski B-4 (varsayılan dalda `DeletedAt` süzgeci `IsDeleted=false`+`DeletedAt` dolu satırı gizler) ve B-5 (`IsDeleted` alanı olmayan eski belge varsayılanda gizli, bayrakla `isDeleted=false`) ortam sayımına bağlıdır, sahibin §17.8/2 sayımına eklenir.

## 5. Mimari süit sonucu

| Ölçüm | DL-1 | DL-2 |
|---|---|---|
| Süit tabanı (kod değişmeden) | 146: 143 geçti, 3 kaldı | 176: 170 geçti, 6 kaldı (işçi, Expected HEAD `8698dbddc`) |
| Son | 176: 173 geçti, 3 kaldı | 216: 210 geçti, 6 kaldı, NotExecuted 0 |
| Yeni kural | `WorkflowRetentionGuardTests` 30/30 | `WorkflowRetentionSourceGuardTests` 40/40 |
| Parmak izi tabanla | aynı üç satır | aynı altı satır |
| `known_failures.py check` mimari | çıkış 1 (YENİ=0) | çıkış 1 (YENİ=3, YENİ-AYRINTI=2, KISMEN-DÜZELDİ=1) |

### Üç `CiServiceTestsGuardTests` kırmızısı bu işe ait değildir

Üç test: `Every_action_in_every_workflow_is_at_its_one_recorded_commit`, `No_workflow_uses_a_privileged_trigger_or_reads_a_secret`, `Nothing_can_turn_a_red_test_or_build_green`.

- **Taban kırmızı:** DL-2 işçisi kodu değiştirmeden tabanı ölçtü: 176/170/6, bu üçü içinde. Son koşuda parmak izi tabanla birebir aynı (`DL2-ISCI-RAPORU-01.md`). DL-2 doğrulayıcısı da aynı altı kırmızıyı ve işçinin tabanıyla aynı parmak izini ölçtü.
- **`check` çıktısındaki "YENİ" yanıltmaz:** "kayıtlı bilinen kırmızı listesinde yok" demektir, "bu değişiklik getirdi" demek değildir. Üçü, DL-1 tabanında (`3096aadda`) kırmızı değildi (yalnız 3 kırmızı vardı); yani DL-1 ile DL-2 tabanı arasında ana dala girdiler.
- **Okuma:** bu üç test `.github/workflows` metnini okur (dosya başlığı: `CiServiceTestsGuardTests.cs`, ilk 40 satır). DL-1 ve DL-2 farkı yalnız `services/**` ve `tests/**` altındadır (13 dosya, doğrulama 01 "Fark kapsamı"); hiçbir iş akışı dosyasına dokunmaz. Neden kırmızı oldukları **ölçülmedi** (okumadan çıkarım: ana dalda CI iş akışı dosyası değişti).
- Kalan üç kırmızı: iki `AuditTrailStandardTests` (e-imza komutları `RegisterSignableActionsCommand`, `VerifySignatureCommand`; iş akışı işi değil, XMC-001) ve `DocsPathGuardTests.NoCodeFilePointsIntoDocsOutsideTheFiveFolders` (mesaj parmak izi kayıttakinden farklı, hangi satırın yeni olduğu ölçülmedi). Üçü DL-1 tabanında da vardı.
- Sonuç: DL-1 ve DL-2 mimari süitte **yeni kırmızı getirmedi**; `check` çıkışı 1 olduğu için `ct-sop7-transitions.tsv` VERIFIED şartı (çıkış 0) ana dal yeşile dönene ya da kayıt tazelenene kadar karşılanmaz. CT bu şartı atlamaz (`DL1-DOGRULAMA-01.md`).

### Birleşik dal koşusu (CT, 2026-11-02, `b78dcb0b5` = CT dalı + DL-2 + `origin/main` `743f42d1c`)

- Mimari: 216 toplam, 213 geçti, 3 kaldı, NotExecuted 0. **Üç `CiServiceTestsGuardTests` testi geçti**; ana daldaki iş akışı dosyası düzeltmesiyle kalktılar (yukarıdaki okuma doğrulandı). Kalan üç kırmızı: iki `AuditTrailStandardTests` ve `DocsPathGuardTests.NoCodeFilePointsIntoDocsOutsideTheFiveFolders`.
- `check --suite TenantArchitecture.ArchitectureTests`: `BİLİNEN=0 KISMEN-DÜZELDİ=1 YENİ=0 YENİ-AYRINTI=2`, çıkış 1. YENİ-AYRINTI ikisi e-imza komutları (`RegisterSignableActionsCommand`, `VerifySignatureCommand`) ve DocsPath mesaj parmak izi; ikisi de bu işin farkı değil. VERIFIED şartı yine karşılanmaz.
- Platform tam süit: 8860 toplam, 8859 geçti, 1 kaldı: `TenantSubscriptionEndpointsHttpMongoTests.Two_assignments_at_once_leave_exactly_one_live_subscription_and_the_other_hears_a_code_never_a_500(differentStatuses: True)`. Neden: test gövdesinde sabit `trialEndDateUtc` `2026-11-02T00:00:00+00:00` (dosya satır 171), doğrulama "bugünden büyük olmalı" diyor; 2026-11-02'den itibaren her dalda kırmızıdır. Abonelik alanı, bu iş değil (XMC-001); düzeltilmedi.
- TRX: CT scratchpad `full/platform-app.trx`, `full/architecture.trx` (repoda değil).

## 6. Açık kalanlar

| # | Konu | Durum | Kaynak |
|---|---|---|---|
| 1 | **AC-W15 dördüncü depo** `WorkflowStartRecoveryStore` (S17-8) | Açık; bekçi korur; override bir sonraki paket revizyonunda | `mod0023-s17-owner-decisions-05.md` |
| 2 | **AC-W17** DL-2 tüketici uyumu | Kısmi: Platform içi tüketiciler geçti; servis dışı tüketici ve **sahip kaydı bekliyor** (MDM dahil, D32/5). B-4 okuması bu kayda yazılır. | `DL2-DOGRULAMA-01.md` |
| 3 | **AC-W18** Cancel tekrarı | Kısmi: üç test geçti; **sahip kaydı bekliyor** (Timesheet withdraw, Diten.Web `cancelTask`) | aynı |
| 4 | AC-W3 (DL-1) tüketici kanıtı | Kısmi: Platform satırları geçti; MDM `GatewayWorkflowClient` testi ve Web geçmiş gövdesi testi sahiplerde | `DL1-DOGRULAMA-01.md` |
| 5 | **O-S17-1** kişisel veri silme yöntemi | Açık; hukuk görüşü bekliyor. Kişi alanları yerinde takma adlandırılacak; yöntem bu işte kurulmadı. İlk silme isteğinden önce karar şart. | paket §17.8/4 |
| 6 | **TRM-15** | Açık; yalnız AC-W1…W18'in bu iş kapsamı geçti, sahip kayıtları ve yazar dışı birleştirme (D32/1) eksik | paket §17.9 kapanış |
| 7 | Cancel'ın geç ikizi | Değişmedi; 409 `WORKFLOW_TASK_INVALID_STATE` mümkün | `DL2-ISCI-RAPORU-01.md` "Açık kayıtlar" |
| 8 | Zaman çizelgesi onay geri çekmesi açığı (yarım başlatmada geri çekme "iptal edecek görev yok" diye başarılı döner) | Bu işin kapsamı dışı; MOD-0280-FU01 kulesinin işi; MOD-0023 "anahtarla kapat" yeteneği DL-2'den sonra, sahip kararıyla | `TIMESHEET-CANCEL-GAP.md` |
| 9 | Karar isteyen tüketici bulguları (Inventory yeniden gönderim, Task Center eşlemesi, Tasks son tarih testi, Web sözcüğü, HCM ölü istemci) | Karar bekliyor | sürüm notu; `DL2-TUKETICI-TABLOSU-01.md` |
| 10 | DL-2 işçisinin geçici dalı `feature/pss/wf-s17-dl2-r2-wip` (`1a43e453e`) | Yerelde duruyor; silme sahibin sözüyle (B-6) | `DL2-DOGRULAMA-01.md` |
| 11 | DL-2'nin main'e girişi | Henüz hiçbir uzak dalda değil (`git branch -r --contains 07caa63c1` boş) | bu rapor |

## 7. Ölçülmedi

- Raporun ilk taslağı test koşmadan yazıldı; birleşik dal koşusu (5. bölüm sonu) CT tarafından eklendi. Sabotaj ya da canlı çağrı koşulmadı.
- DL-2'nin S1–S19 ve G1–G8 sabotajlarının bağımsız yeniden koşusu. Doğrulayıcı altı sabotaj denedi: üçü kırmızı (`SameBody`'den ReasonCode, `TwinOrAsync` kaldırma, RequestInfo ham `comment`), üçü yeşil kaldı (neden kodu değeri B-1, `WholeStartAsync` bayrağı B-3, geçiş anahtarı kiracı süzgeci B-2); üçü sonradan `07caa63c1` ile test aldı ve bu üç yeni testi işçi sabotajla kırmızıya çevirdi, CT yeniden koşmadı.
- Gerçek servis, üretim `Program.cs`, Gateway geçişi, `~Workflow` dışı Platform testleri, tüketici gövde kanıtı (MDM, Web, Treasury), ortam verisi (`{IsDeleted:true}` ve `{IsDeleted:{$exists:false}}` sayımları).
- Kiracı süzgeci ve `[HasPermission]` için "eklenen kodda sabit kiracı yok" iddiası fark okumasıdır, otomatik taramayla doğrulanmadı.
