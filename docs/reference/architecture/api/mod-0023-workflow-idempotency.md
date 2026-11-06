# MOD-0023 §17 — İş akışı: aynı anahtar, silmeme ve ek okuma alanları (bugün kodda olduğu gibi)

> **Yetki:** modül paketi `MOD-0023-workflow-config-approval-templates.md` §17 (en üstte §17.9, sonra §17.8, sonra §17.7).
> Şema koddan üretilir; bu sayfa `Diten.Platform` (port 5057) servisinin **bugün** ne yaptığını anlatır, sözleşmeyi yeniden
> tanımlamaz. Çakışırsa paket ve kod kazanır. Karar gerekçesi: `docs/records/decisions/2026-11/mod0023-s17-same-key-replay-adr-01.md`.
>
> **Kanıt tabanı:** her iddia `dosya:satır` ile verilir; satırlar dal `chore/ct/pss-wf-ct-01` @b78dcb0b5 içindir
> (DL-1 `d30e93c36`, DL-2 `1e5615697` + `07caa63c1`, CT dalına `3ddf6d8eb` ile birleşti). Bu sayfa yazılırken **hiçbir test ya da
> derleme koşulmadı**; "testle sabit" denen yerler işçi ve doğrulayıcı kayıtlarına dayanır
> (`docs/records/audits/2026-10/mod0023-s17-inspection-01/`). DL-1 `origin/main` içindedir (PR #324); DL-2 yazım anında
> hiçbir uzak dalda değildir (`git branch -r --contains 07caa63c1` boş).

Yol kısaltmaları (bu sayfada):

| Kısaltma | Yol |
|---|---|
| `CTRL` | `services/Diten.Platform/src/Diten.Platform.API/Controllers/WorkflowDefinitionsController.cs` |
| `APP` | `services/Diten.Platform/src/Diten.Platform.Application/Features/Workflow` |
| `CMD` | `APP/Handlers/CommandHandlers` |
| `QRY` | `APP/Handlers/QueryHandlers` |
| `INF` | `services/Diten.Platform/src/Diten.Platform.Infrastructure/Persistence/Repositories/WorkflowRepositories.cs` |
| `DOM` | `services/Diten.Platform/src/Diten.Platform.Domain/Repositories/IWorkflowRepositories.cs` |
| `COM` | `services/Diten.Platform.Common/src/Diten.Platform.Common/Persistence/Repositories.cs` |
| `TST` | `services/Diten.Platform/tests/Diten.Platform.Application.Tests/Workflow` |

## 1. Yüzey

Taban yol `api/v1/workflow` (`CTRL:21`). Hiçbir yol, fiil ya da izin eklenmedi; yalnız bir isteğe bağlı sorgu parametresi ve yanıtlara sonda alanlar geldi.

| Uç | Yöntem + yol | İzin | `CTRL` satırı |
|---|---|---|---|
| Başlat | `POST instances` | `platform.workflow.instances.start` | 87-93 |
| Örnek oku | `GET instances/{id}` | `platform.workflow.instances.view` | 95-101 |
| Geçmiş | `GET instances/{id}/history` | `platform.workflow.instances.view` | 104-110 |
| Nesnelere göre | `GET instances/by-objects?objectType&objectIds&includeDeleted` | `platform.workflow.instances.view` | 121-131 |
| Onayla / Reddet | `POST tasks/{taskId}/approve` · `.../reject` | `...tasks.approve` · `...tasks.reject` | 197, 222 |
| Başkasına devret | `POST tasks/{taskId}/delegate` | `...tasks.delegate` | 246 |
| Bilgi iste | `POST tasks/{taskId}/request-info` | `...tasks.request-info` | 262 |
| İptal | `POST tasks/{taskId}/cancel` | `...tasks.cancel` | 278 |

İzin adları: `APP/WorkflowModels.cs:9-24`. Hata gövdesi `reason_code` alanını taşır (ham gövdede `"reason_code":"…"`; `TST/WorkflowSameKeyHttpMongoTests.cs:294` bunu düz dizgeyle sabitler), başarı yanıtı `data` içindedir.

**Aktör.** Bir görev eyleminin aktörü her zaman oturumdaki kullanıcıdır; gövdedeki `actorId` boş olabilir ya da çağıranın kendisi olmalıdır, başkasını adlandırmak **403 `WORKFLOW_ACTOR_MISMATCH`** verir (`CTRL:301-330`). Bu kod, aşağıdaki **`WORKFLOW_ACTOR_DENIED`**'dan ayrıdır ve DL-2 onu değiştirmedi.

## 2. Başlatma: aynı anahtarla tekrar

İstek gövdesi alanı `idempotencyKey` (`APP/WorkflowModels.cs:302`); boş/boşluk anahtar "anahtarsız" sayılır, kırpılır (`CMD/StartWorkflowInstanceHandler.cs:76-78`).

Anahtar daha önce bağlıysa akış `ExistingStartResponseAsync`'e gider (`CMD/StartWorkflowInstanceHandler.cs:81-84, 372-408`). **İlk iş, olguların karşılaştırılmasıdır** (`:381-384`) ve örnek bugün hangi durumda olursa olsun yapılır.

| Durum | Yanıt | Kanıt |
|---|---|---|
| Anahtarın ilk yazımı | 200, `idempotentReplay: false` | `CMD/StartWorkflowInstanceHandler.cs:792-809` (varsayılan `false`) |
| Aynı olgular, aynı başlatan, başlatma bütün | **200**, `idempotentReplay: true`, aynı `workflowInstanceId` / `approvalTaskId` | `:392-399`; tel üstünde `TST/WorkflowSameKeyHttpMongoTests.cs:204` |
| Aynı olgular, yarım başlatma tamamlanır | 200, `idempotentReplay: true` | `:580` (`CompleteStartAsync`) |
| Bütün başlatma, "işlem kullanılamıyor" sonrası bulunur | 200, `idempotentReplay: true` | `:736-750` (`WholeStartAsync`); `TST/WorkflowStartRecoveryMongoTests.S17.cs:476` |
| **Farklı olgu ya da başka başlatan** | **409 `WORKFLOW_INSTANCE_START_CONFLICT`**, hiçbir şey değişmez | `:381-384`, `:590-609`, `:759-760`; `TST/WorkflowStartRecoveryMongoTests.S17.cs:320, 377` |
| Yarım ama tamamlanamaz (güvenilir başlatma ya da kapanmış örnek) | 409 aynı kod | `:402-406` |
| Başlatma, görev yazılmadan kapatılmış | 409 aynı kod | `:333`, `:729` |
| Eşzamanlı yarışta kaybeden, başlatma henüz bütün değil | **503** (kod `WORKFLOW_TRANSITION_CONFLICT`), aynı anahtarla yeniden gönderilir | `:752-757` |

`idempotentReplay` yanıtın **sondaki, isteğe bağlı** alanıdır (`APP/WorkflowModels.cs:309-324`; tel adı `idempotentReplay`). Anlamı: "bu yanıt, daha önce bağlı bir anahtardan verildi". **Bu bayrak "ben yazmadım" kanıtı değildir:** commit sonucu bilinmeyen ilk yazma da, sonra bütün bulunduğunda `true` alır (doğrulama 01 B-3; testle sabitlendi, `TST/WorkflowStartRecoveryMongoTests.S17.cs:476`).

**Karşılaştırılan olgular** (`CMD/StartWorkflowInstanceHandler.cs:590-609`, `:767-786`):

- Şablon (çözülmüş kimlik), `objectType`, `objectId`, `objectRef`.
- Başlatan: `StartedByUserId` oturumdaki kullanıcıyla aynı olmalı; alan yoksa başlatan adı (`:611-619`).
- Başlatma BL-549'dan sonra yazıldıysa **parmak izi** de: şablon, nesne, çağıranın adlandırdığı aday listesi (sıralı), neden kodu (kırpılmış), `commentRequired`, `evidenceRequired`, `dueAt` (UTC tick). Parmak izi ilk yazımda saklanır (`:219`).
- Parmak izi **olmayan** eski başlatma yalnız kimliğine (şablon, nesne, `objectRef`) ve başlatana bağlanır. Bu yüzden eski bir yarım başlatmada değişen son tarih ya da aday 409 vermez (`:605-608`).
- **Parmak izinde olmayan:** `displayContext` (başlık, alt başlık, rozetler, derin bağ). Değişmesi 409 vermez (okuma: `:767-786`).
- Güvenilir (trusted) bir başlatmanın parmak izi başka biçimdedir ve genel uçtan tekrarında **hiç eşleşmez**: genel uçtan gönderilen aynı anahtar 409 alır (`TST/WorkflowStartRecoveryMongoTests.S17.cs:454`). Güvenilir yolun kendi kodu `WORKFLOW_START_IDEMPOTENCY_CONFLICT`'tir (`APP/WorkflowModels.cs:42`) ve değişmedi.

**Önceki duruma göre fark (tüketici için):** bütün başlatma tekrarı eskiden olguya bakmadan 200 dönerdi; artık olgu ya da başlatan farklıysa 409. Yarım başlatmada aynı karşılaştırma **DL-2'den önce de vardı** (`CompleteStartAsync` parmak izi); DL-2 farkı yalnız bütün başlatma tekrarındadır (doğrulama 01 B-4).

## 3. Geçişler: aynı anahtarla tekrar

Anahtar, (görev, eylem, anahtar) üçlüsünün benzersiz dizinidir: `ux_workflow_transition_logs_task_action_idempotency_active`
(`services/Diten.Platform/src/Diten.Platform.Infrastructure/Persistence/Schema/PlatformSchemaManifest.WorkflowWorkCenter.cs:220`).
Anahtar araması kiracı süzgeçlidir (`INF:863-875`, `ExecutionFilter`).

### 3.1 Sıralı tekrar: aktör, sonra gövde, sonra kayıtlı yanıt

Approve, Reject, RequestInfo ve Cancel aynı yardımcıdan geçer: `BuildBodyBoundIdempotentResponseAsync` (`CMD/WorkflowTaskTransitionSupport.cs:1076-1103`).

| Sıra | Koşul | Yanıt | Kanıt |
|---|---|---|---|
| 1 | Kayıtlı satırın `ActorId` ya da `ActorRef` değeri istek yapana eşit değil | **403 `WORKFLOW_ACTOR_DENIED`** | `:1088-1091`, `:1041-1046` |
| 2 | Aynı aktör, gövde farklı | **409 `WORKFLOW_TRANSITION_IDEMPOTENCY_CONFLICT`**, yeni satır yok | `:1093-1100`; `APP/WorkflowModels.cs:84` |
| 3 | Aynı aktör, aynı gövde | **200**, `isIdempotent: true`, ilk geçişin yanıtı | `:1102` |

**Gövde karşılaştırması** tek yerde yapılır: `WorkflowTerminalTransitionWriter.SameBody` (`CMD/WorkflowTerminalTransitionWriter.cs:455-466`). Karşılaştırılanlar (ordinal, `null` ile `""` farklıdır): `ReasonCode`, `Comment`, `EvidenceRef`, `SignatureId` ve RequestInfo için `TargetPrincipalId`.

- `SignatureId` yalnız **Approve**'da gövdeye girer; Reject imza saklamaz (`CMD/WorkflowTaskTransitionSupport.cs:62`). Tekrar, imza kapısından **önce** yanıtlanır (`TST/WorkflowSameKeyHttpMongoTests.cs:294`: farklı imza → 409 `WORKFLOW_TRANSITION_IDEMPOTENCY_CONFLICT`, aynı imza → 200).
- `TargetPrincipalId`, `WorkflowTransitionLog`'a eklenen yeni alandır (`services/Diten.Platform/src/Diten.Platform.Domain/Entities/Workflow/WorkflowTransitionLog.cs:35`). RequestInfo satırı onu saklar: kırpılmış hedef ya da hedef yoksa `""`; öteki eylemlerde `null`. Hedef ayrıca açıklamaya da katıldığı için (`target:P`; `CMD/WorkflowTaskTransitionSupport.cs:557-562`) iki farklı istek aynı `Comment`'i saklayabilirdi; ayrı karşılaştırma bunu kapatır. **Okuma DTO'larına eklenmedi.**
- **Eski satır** (alan yok, `null`): hedef ayrı karşılaştırılmaz, yalnız `Comment` üzerinden bakılır; DL-2 öncesi gönderilmiş bir anahtarın gerçek tekrarı 409 almasın diye (`CMD/WorkflowTerminalTransitionWriter.cs:464-466`). Kabul edilen sınır: çarpışma yalnız eski satırlarda yakalanmaz.
- **Cancel:** eskiden aktörü bile karşılaştırmazdı; başka aktörün iptal tekrarı 200 alırdı. Artık diğer eylemlerle aynı (`CMD/WorkflowTaskTransitionSupport.cs:651`). Cancel gövdesinde kanıt ve imza yoktur (`null` geçilir). Cevabı değişen **tek** eylem budur: başka aktör 200 → 403.
- **Delegate** değişmedi: tekrarı yalnız aktöre bağlıdır, gövde karşılaştırılmaz (`:404` → `:1055-1070`). `TST` kanıtı: `Delegate_replay_with_a_different_body_stays_idempotent`.

### 3.2 Geç gelen ikiz (Approve, Reject) — sahip kararı O-S17-3 = (i)

Anahtar aramasından sonra, ama görev okumasından önce ikiz commit olduysa çağrı "sürüm bayat" ya da "karar verilemez" reddi alırdı. Artık ret verilmeden önce anahtar **bir kez daha** okunur; satır varsa yanıt §3.1'deki gibi (200 / 403 / 409) verilir, yoksa eski ret sürer (`CMD/WorkflowTaskTransitionSupport.cs:80-105`). Mutlu yol fazladan okuma yapmaz. Ret kodları değişmedi: 409 `WORKFLOW_TRANSITION_CONFLICT` (bayat sürüm) ve 409 `WORKFLOW_TASK_INVALID_STATE` (karar verilemez); ikincisi hâlâ **kesin ret**dir. Başka bir karar kaybedene yine `WORKFLOW_TASK_INVALID_STATE` ya da `WORKFLOW_TRANSITION_CONFLICT` döner, çünkü onun anahtarı altında satır yoktur.

### 3.3 RequestInfo yinelenen anahtar eşlemesi

Gecikmeli özdeş RequestInfo ikizi görevi hâlâ karar verilebilir bulur ve yazmaya gider; satırı benzersiz dizine çarpabilir. Yazıcı yalnız **bu dizinin** yinelenen anahtar reddini yakalar (`CMD/WorkflowTerminalTransitionWriter.cs:341-345`, `:469-483`) ve çağıran o satırı okuyup §3.1 gibi yanıtlar (`CMD/WorkflowTaskTransitionSupport.cs:741-760`): aynı aktör ve gövde 200, başka aktör 403, başka gövde 409. Satır bulunamazsa 409 `WORKFLOW_TRANSITION_CONFLICT`. Başka her yazma hatası değişmedi.

### 3.4 Aynı anda çakışma (değişmedi)

İşlem içi çakışmada kaybeden, gövdesi ne olursa olsun 409 `WORKFLOW_TRANSITION_CONFLICT` ya da 503 `WORKFLOW_TRANSACTION_UNAVAILABLE` alır; sürüm çiti gövdeleri ayıramaz. Yalnız "işlem kullanılamıyor" yeniden okuması gövdeleri ayırır (`SamePayload`, `CMD/WorkflowTerminalTransitionWriter.cs:442-446`, aynı `SameBody` ile). İşçi ölçümleri (`DL2-ISCI-RAPORU-01.md`, "Ölçümler"): Approve, Reject ve RequestInfo bariyer yarışlarında kaybeden her seferinde 409 `WORKFLOW_TRANSITION_CONFLICT`. Bu iki kod iş reddi değildir; tüketici niyeti "bilinmiyor" sayar, aynı anahtarı yeniden gönderir ya da okur (paket §17.7/6).

**Cancel'ın geç ikizi değişmedi:** `CancelAsync` anahtarı yeniden okumaz; ikiz anahtar aramasından sonra commit olduysa 409 `WORKFLOW_TASK_INVALID_STATE` alınabilir (işçi raporu "Açık kayıtlar"; `CMD/WorkflowTaskTransitionSupport.cs:629-655`, yeniden okuma yok).

### 3.5 Başka kiracı

Başka kiracının oturumu aynı `taskId` ve anahtarı gönderirse anahtar araması kiracı süzgecinden boş döner, görev bulunamaz: **404 `NOT_FOUND_NON_LEAKAGE`** (`CMD/WorkflowTaskTransitionSupport.cs:70-78`). 403 (satırın aktörü), 200 ya da 409 gelmez ve hiçbir satır yazılmaz. Dört eylem × aynı/başka aktör = 8 durum testle sabittir (`TST/WorkflowCandidateDecisionMongoTests.S17.cs:870`, kiracı süzgeci kaldırılınca 8 satır kırmızı — işçi sabotajı, doğrulama 02).

## 4. Okumalar

### 4.1 `GET instances/by-objects`

`APP/Handlers/QueryHandlers/GetWorkflowInstancesByObjectsHandler.cs:51-77`; sorgu `APP/Queries/GetWorkflowInstancesByObjectsQuery.cs:12-17`.

- En çok 100 nesne (`MaxObjectIds`), aşılırsa 400; en az bir `objectId` gerekir.
- **Varsayılan (`includeDeleted=false`):** silinmiş satır gizlidir. Gizleme depo süzgecinden gelir: `TenantRepository<T>.ExecutionFilter` = `TenantId` **ve** `IsDeleted == false` (`COM:75-79`). İşleyicideki `.Where(x => x.DeletedAt is null)` (`:55`) gerçek Mongo'da etkisizdir (paket §17.9/1).
- **`includeDeleted=true`:** yeni depo üyesi `ListByObjectIdsAllRowsAsync` aynı kiracının satırlarını **silinmişleri dahil** okur; kiracı koşulu kalır, yalnız `IsDeleted == false` düşer (`INF:194-209`; arayüz `DOM:172-176`). Satır `isDeleted` ile işaretlenir (tek işaret `IsDeleted`; `DeletedAt` ne derse desin).
- Arayüz varsayılan gövdesi **bilerek fırlatır** (`NotSupportedException`, `DOM:172-176`): süzen bir varsayılan, geçersiz kılmayan bir uygulamada bayrağı sessizce yok sayardı.
- İzin değişmedi (`platform.workflow.instances.view`, `CTRL:121-122`); yeni izin yok.
- **Başka kiracının nesnesi** 404 değil, **200 ve boş listedir**, bayrak açık ya da kapalı (`TST/WorkflowRetentionReadMongoTests.cs:197`); ortak nesne kimliğinde de başka kiracının satırı gelmez (`:216`).

`WorkflowInstanceStatusDto` (`APP/WorkflowModels.cs:205-214`) yeni alanları sonda ve isteğe bağlı taşır: `idempotencyKey` (anahtarsız başlatmada `null`) ve `isDeleted`.

### 4.2 `GET instances/{id}/history`

`APP/Handlers/QueryHandlers/GetWorkflowInstanceHistoryHandler.cs:51-59, 112-121`.

- Yeni sonda alanlar (`APP/WorkflowModels.cs:462-477`): `approvalTaskId`, `idempotencyKey`, `isDeleted`. `isDeleted` burada **her zaman `false`**: okuma silinmiş satırı zaten süzer.
- `history` **`includeDeleted` almaz** ve silinmiş bir örnek için **404 `NOT_FOUND_NON_LEAKAGE`** verir; bayrak olsun olmasın (sahip kararı O-S17-2 = (b), `docs/records/decisions/2026-10/mod0023-s17-owner-decisions-01.md`; `TST/WorkflowRetentionReadMongoTests.cs:165`). Silinmeyi `by-objects?includeDeleted=true` ile okuyun.
- Başka kiracının örnek kimliği de aynı 404'tür (`TST/WorkflowRetentionReadMongoTests.cs:230`).

`GET instances/{id}` de silinmiş örnekte 404 verir (aynı test). `instances/{id}` yanıtına anahtar alanı eklenmedi (paket: isteğe bağlıydı; okumada bu işte bulunmadı, ölçülmedi).

## 5. Silme reddi

- `WorkflowInstanceRepository`, `ApprovalTaskRepository`, `WorkflowTransitionLogRepository`: devralınan `DeleteAsync` geçersiz kılınır ve yazmadan `NotSupportedException` fırlatır (`INF:152, 486, 836`). Devralınan yol `COM:35`'tedir ve değişmedi (başka modüller kullanır). Bir HTTP ucu silme sunmaz; ret yalnız süreç içidir.
- Hiçbir arayüze silme üyesi eklenmedi; yeni üye tek okumadır (`DOM:172`).
- **Açık:** dördüncü türemiş depo `WorkflowStartRecoveryStore` (`TenantRepository<WorkflowInstance>`) devralınan `DeleteAsync`'i taşır; gerçek Mongo'da `IsDeleted = true` yazar, üretimde çağıran yoktur (doğrulama DL-1 B-2). Sahip kararı S17-8 (`docs/records/decisions/2026-10/mod0023-s17-owner-decisions-05.md`): override yazılmaz, mimari bekçi `WorkflowRetentionSourceGuardTests` yolu metin okuyarak kapalı tutar, kapatma paketin bir sonraki revizyonudur.
- Kişisel veri silme isteği (KVKK/GDPR) bu işte **kurulmadı**: satırlar silinmez, kişi alanları yerinde takma adlandırılır (paket §17.8/4). Yöntem O-S17-1'dir, hukuk görüşünü bekler, açıktır.

## 6. Tüketici için kısa kural

1. Başlatmada anahtarı **aynı olgularla** yeniden gönder; farklı olgu gerekiyorsa yeni anahtar kullan. Aynı anahtar + farklı olgu ya da başka kullanıcı artık 409 `WORKFLOW_INSTANCE_START_CONFLICT`.
2. Geçişte aynı anahtar **aynı aktör ve aynı gövde** demektir. Gövdeyi (neden, açıklama, kanıt, imza, hedef) değiştirmek 409 `WORKFLOW_TRANSITION_IDEMPOTENCY_CONFLICT` verir; bu **yeniden denenmez**, anahtar o gövdeye bağlıdır.
3. 409 `WORKFLOW_TRANSITION_CONFLICT` ve 503 `WORKFLOW_TRANSACTION_UNAVAILABLE` iş reddi değildir (niyet "bilinmiyor"; aynı anahtarla yeniden dene ya da `history`/`by-objects` ile oku).
4. Aynı anahtarla gelen `idempotentReplay`/`isIdempotent` bayrağına "benim yazmadığım" kanıtı olarak güvenme (§2).
5. Genel uçlar anahtarı **gövdeden** okur; `Idempotency-Key` başlığını okumaz (tüketici tablosu §0; okuma, doğrulanmadı bu sayfada).

Tüketici satırları (hangi tüketici hangi durumu gönderebilir) için: `docs/records/audits/2026-10/mod0023-s17-inspection-01/DL2-TUKETICI-TABLOSU-01.md`. Tablo okuyarak çıkarımdır, koşulmadı.

## 7. Ölçülmedi

- Bu sayfanın yazımında hiçbir test, derleme ya da canlı çağrı koşulmadı.
- DL-2 sabotajlarının (S1–S19, G1–G8) bağımsız yeniden koşusu yok; yalnız B-1…B-3 kapanışı CT'nin test metnini ve farkı okumasına dayanır (`DL2-DOGRULAMA-02.md`).
- Gerçek servis, üretim `Program.cs`, Gateway geçişi (`ocelot.json` yolları), canlı çağrı.
- Tüketici gövde kanıtı: MDM `GatewayWorkflowClient` testi ve Web geçmiş gövdesi testi yok ("sahibinde"); Treasury ve İzin (MOD-0280-FU02) için kod yok.
- Şablon bulunamazsa bütün başlatma tekrarının 200 yerine 409 verdiği (B-5) okumadan çıkarımdır.
