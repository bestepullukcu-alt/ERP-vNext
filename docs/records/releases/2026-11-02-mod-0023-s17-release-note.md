# MOD-0023 §17 — sürüm notu: kayıtlar silinmez, aynı anahtar aynı yanıtı verir (TRM-15)

- Modül: MOD-0023 İş Akışı Tasarımcısı (platform-shared-services) · servis `Diten.Platform` (port 5057)
- Tarih: 2026-11-02 · durum: **taslak** (sürüm numarası verilmedi; dağıtım izni değildir)
- Kapsam: iki dilim. **DL-1** kayıtları silmeme + ek okuma alanları. **DL-2** aynı anahtar yanıtı.
- Nerede: DL-1 `origin/main` içindedir (PR #324, commit `d30e93c36`). DL-2 yazım anında **hiçbir uzak dalda yoktur**: işçi dalı `feature/pss/wf-s17-dl2-r2`
  (`1e5615697` + `07caa63c1`) yerelde, CT dalına `3ddf6d8eb` ile birleşti (`git branch -r --contains 07caa63c1` boş).
- Ölçüm notu: bu notun yazımında test ve derleme koşulmadı; sayılar işçi ve doğrulayıcı kayıtlarındandır
  (`docs/records/audits/2026-10/mod0023-s17-inspection-01/`).
- **TRM-15 bu sürümle kapanmaz.** Açıklar: sonda.

## Ne değişti

### DL-1 — eklemeli, tüketiciyi bozmaz

| Değişiklik | Tüketici etkisi |
|---|---|
| Üç kayıt tipinin (örnek, görev, geçiş kaydı) `DeleteAsync`'i yazmadan reddeder | Servis içinde çağıran bulunmadı (arama); HTTP silme ucu yok. Dışarıdan görünür etkisi yok. |
| `by-objects` yanıtına sonda `idempotencyKey`, `isDeleted` | Var olan alanlar ve sıra aynı; eski okuyucular yeni alanı yok sayar. |
| `history` yanıtına sonda `approvalTaskId`, `idempotencyKey`, `isDeleted` (`isDeleted` hep `false`) | Aynı. |
| `by-objects?includeDeleted=true` (varsayılan `false`) | Varsayılan yanıt değişmez. Bayrakla silinmiş satırlar `isDeleted=true` ile gelir; yeni izin yok. |
| `history` silinmiş örnekte 404 | Bugün de 404'tü; değişmedi (O-S17-2 = b). |

### DL-2 — davranış değişir: 200'den 409'a (ve 403'e) dönen durumlar

| # | Durum | Önceki yanıt | Şimdiki yanıt |
|---|---|---|---|
| 1 | Başlatma: aynı anahtar, **bütün** başlatma, farklı olgu (şablon, nesne, aday, son tarih, neden, bayraklar) | 200, olgulara bakılmazdı | **409 `WORKFLOW_INSTANCE_START_CONFLICT`** |
| 2 | Başlatma: aynı anahtar, bütün başlatma, **başka kullanıcı** | 200, ilk kişinin örneği devralınırdı | **409** aynı kod |
| 3 | Genel uçtan gönderilen **güvenilir** başlatma tekrarı | 200 | **409** (pakette yazılı değil; testle sabit) |
| 4 | Geçiş: aynı anahtar, **aynı aktör**, farklı gövde (neden, açıklama, kanıt, imza, hedef) | 200, kayıtlı yanıt | **409 `WORKFLOW_TRANSITION_IDEMPOTENCY_CONFLICT`** (yeni kod, yeniden denenmez) |
| 5 | **Cancel**: aynı anahtar, **başka aktör** | 200 | **403 `WORKFLOW_ACTOR_DENIED`** |
| 6 | Approve/Reject: geç gelen özdeş ikiz (anahtar aramasından sonra commit) | 409 `WORKFLOW_TASK_INVALID_STATE` ya da `WORKFLOW_TRANSITION_CONFLICT` | **200** `isIdempotent=true` (aynı aktör ve gövde); başka aktör 403; başka gövde 409 |
| 7 | RequestInfo: gecikmeli özdeş ikiz, yinelenen anahtar dizini | pakete göre 503 `WORKFLOW_TRANSACTION_UNAVAILABLE` (okuma) | 200 / 403 / 409 (aktör ve gövdeye göre) |
| 8 | Başlatma tekrarında şablon artık bulunamıyor | 200 | 409 (okumadan çıkarım, **ölçülmedi**; doğrulama 01 B-5) |

Not (paket dili): paketin §17.8/5 kısaltması "aynı anahtar + farklı gövde 403 → 409" der. Eski kod yalnız aktörü karşılaştırdığı için aynı aktörün farklı gövdesi **200** alırdı;
farklı aktör zaten 403'tü (`WorkflowTaskTransitionSupport.cs:1018-1030` @4c08fc75a, INSPECTION.md DL-2). Satır 4'ün "önceki" sütunu bu okumaya dayanır.

**B-4 düzeltmesi.** Yarım başlatmada (örnek yazılmış, görev yazılmamış) son tarih, aday ya da başlatan farkının 409 vermesi **DL-2'den önce de vardı**
(`CompleteStartAsync` parmak izi karşılaştırması; `TaskApprovalService.cs:212`, doğrulama 01 B-4). Tüketici tablosunun ilk taslağı bunu DL-2'ye atfetmişti; DL-2'nin farkı yalnız
**bütün** başlatma tekrarındadır (satır 1-2). Tasks çağıranlarında bu durum yalnız eşzamanlı yarışta oluşur; kaybeden loglanır ya da "yeniden deneyin" alır.

**Yeni alan.** Başlatma yanıtına sonda, isteğe bağlı `idempotentReplay` (ilk yazım `false`, geri kalan `true`). Okunan hiçbir tüketici onu kullanmıyor; eklemek bunlar için güvenli
(tablo madde 8). `true`, "ben yazmadım" kanıtı değildir (B-3).

## Tüketici etkisi: kim neyi gönderebilir

Kaynak: `DL2-TUKETICI-TABLOSU-01.md` (işçinin alt ajanı okuyarak çıkardı, satır numaralarını işçi doğruladı; **CT tek tek okumadı**, hiçbir tüketici gövde kanıtı koşulmadı).
Aşağıda "gönderebilir" yalnız, kodun o durumu üretebileceği demektir.

| Tüketici (tablo satırı) | Gönderebilir mi | Sonuç | Karar gerekir mi |
|---|---|---|---|
| **Inventory karantina başlatma** (8a) | **Evet, yalnız** ilk başlatma commit olduysa ama başarısız bildirildiyse (zaman aşımı, 5xx) **ve** firma onaycı adayları ya da bekleme günü ayarı değiştiyse | Her yeniden gönderim 400 `WorkflowRejected`; onaycının kutusunda açık görevli örnek, karar kaydında `WorkflowStarted` yok. Önceden 200. | **Evet (bulgu 1).** Seçenekler: yeniden gönderim turu için anahtar eki; START_CONFLICT'i okunur duruma çevirmek; kabul edip belgelemek. |
| **Task Center eylemleri** (7) | **Evet, yalnız** ilk gönderim commit olup yanıt kaybolduysa ve kullanıcı aynı öğe sürümünde başka metin/imza/hedefle yeniden gönderirse | 409 `WORKFLOW_TRANSITION_IDEMPOTENCY_CONFLICT` (önceden kayıtlı 200). Anahtar `wcn-action-v1:` + hash(sağlayıcı, öğe, eylem, sürüm): **aktör, metin, imza yok**. Ön yüzde yeni kod için eşleme yok; "unmapped 409" loglanır, genel uyarı gösterilir, öğe yeniden yüklenmez (çıkarım). | **Evet (bulgu 2).** Kodu eşle ve yeniden yükle; ya da metni/imzayı anahtara kat; ya da kabul et. |
| **Tasks görev onayı ve incelemesi — son tarih** (1a, 2a, 4) | Evet, yalnız yarım başlatma varsa ve tekrardan önce görevin son tarihi/yöneticisi/inceleyicisi değiştiyse (**DL-2'den önce de vardı**, B-4) | Çağıran yarım başlatmayı kapatıp bir kez yeni tur açar. | Kısmen: çağıran düzeyinde son tarih durumunu sınayan test yok (bulgu 3). |
| Tasks yukarı istek başlatma (3), onay/inceleme iptali (1b, 2b), süpürge (5) | Hayır / yalnız eşzamanlı iki kişi (iptal: 200 → 403) | Düzenleme reddedilir, yeniden denemede geçer (çıkarım). | Hayır |
| **CRM / MDM başlatma** (9a, 10a) | Yalnız ilk gönderimin kayıt yazımı kaybolduysa ve **başka kişi** o turu gönderirse | 400 (önceden 200, ilk kişinin örneği devralınırdı). Amaçlanmış görünüyor (K1). | Tüketici testi yok (bulgu 4); sahip kaydı |
| **CRM / MDM karar** (9c, 10c) | Yalnız aynı karar için iki eşzamanlı istek farklı açıklamayla | 400 `WorkflowRequestRejected`/`workflow_request_rejected` | Sahip kaydı |
| CRM/MDM geri çekme (9b, 10b) | Hayır (aynı aktör, sabit gövde) | — | Hayır |
| **Diten.Web iş akışı konsolu** (11a) | Evet: anahtar alanı düzenlenebilir ve başarısız denemeden sonra aynı kalır | Düzenleyip yeniden gönderirse 409 (sunucu metni gösterilir, doğru). **Cancel'da 403 "Permission missing or not assigned" okunur** (`index.js:101-107`) | Yalnız sözcük (bulgu 5) |
| Timesheet başlatma/iptal (6a-6d) | Hayır (her gönderimde yeni anahtar; sabit gövde; aynı aktör) | — | Hayır. `TimesheetFinalizer.cs:239` `CancelAsync` dönüşünü okumaz; bu durumda bile hafta Draft'a döner (tablo 6d). |
| **HCM** `GatewayWorkflowStartClient` (12) | Hayır: DI'de kayıtsız, çağıranı yok, gövde ve yanıt motor sözleşmesiyle uyuşmuyor | — | HCM sahibine not; "uyumlu" yazılmaz (bulgu 6) |
| Treasury, İzin (MOD-0280-FU02) (13, 14) | — | **kod yok, koşmadı** | — |
| CRM'in öteki üç çağıranı (9d) | Ölçülmedi | Aynı istemci eşlemesi geçerli | Ölçülmedi (bulgu 7) |

Tüketici testleri (kod değişmedi; yeni yanıtla **gövde kanıtı değil**, sahte ağ geçidi): Platform içi Tasks/Timesheet/WorkCenter sınıfları geçti; CRM 71/71, Inventory 22/22, MDM 18/18, HCM 1/1, Web 24/24 (işçi raporu). MDM `GatewayWorkflowClient` tüketici testi ve Web geçmiş gövdesi testi **yok** ("sahibinde, koşmadı").

## Karar isteyen tüketici bulguları (özet)

1. Inventory karantina yeniden gönderimi (satır 8a): anahtar eki / okunur durum / kabul.
2. Task Center anahtarı ve ön yüz eşlemesi (satır 7): kodu eşle ve yeniden yükle / anahtara kat / kabul.
3. Tasks son tarih durumu için çağıran düzeyinde test (satır 4).
4. CRM/MDM başlatma ve karar: başka kişi artık 400; tüketici testi yok.
5. Web konsolu: Cancel `WORKFLOW_ACTOR_DENIED` "izin yok" gibi okunur; yalnız sözcük kararı.
6. HCM istemcisi ölü kod; HCM sahibine not.

AC-W17 ve AC-W18 **sahip kayıtları bekliyor**; B-4 okuması AC-W17 sahip kaydına yazılacak (doğrulama 01).

## Doğrulanan ve doğrulanmayan

| Konu | Sonuç | Kaynak |
|---|---|---|
| DL-1 Platform `~Workflow` | 902 geçti, 0 kaldı; `WorkflowRetentionReadMongoTests` 18/18 gerçek Mongo | `DL1-DOGRULAMA-01.md` |
| DL-1 mimari | 176: 173 geçti, 3 kaldı (işten önce var, ana dalda da); yeni kural 30/30 | aynı |
| DL-2 Platform `~Workflow` + Timesheet | 1046/1046 (işçinin son koşusu) | `DL2-DOGRULAMA-02.md` |
| DL-2 mimari | 216: 210 geçti, 6 kaldı (taban da 6); yeni bekçi 40/40 | `DL2-DOGRULAMA-01.md` |
| DL-2 B-1, B-2, B-3 test açıkları | Kapandı (CT test metnini ve farkı okudu; sabotajları yeniden koşmadı) | `DL2-DOGRULAMA-02.md` |
| Servis dışı tüketiciler, canlı çağrı, Gateway, üretim `Program.cs` | **Ölçülmedi** | `DL2-DOGRULAMA-01.md` |

## Geri alma ve gerileme

- DL-1 geri alınabilir ve eklemelidir. DL-2 tüketici sözleşmesini değiştirdi (satır 1-6); geri alınırsa 409'a göre yazılmış tüketiciler de gözden geçirilmelidir.
- Dağıtımdan önce karar bekleyen iki bulgu (1 ve 2) kullanıcı yüzünde görünür etki doğurabilir; ikisi de yalnız "ilk gönderim commit oldu ama yanıt görülmedi" koşulunda oluşur.

## Açık kalanlar

- TRM-15 açık: AC-W3/AC-W17/AC-W18 tüketici sahip kayıtları (MDM dahil, D32/5) ve dilimlerin yazar dışı birleştirmesi (D32/1).
- AC-W15 dördüncü depo (`WorkflowStartRecoveryStore`, S17-8) açık; bekçi korur.
- O-S17-1 (kişisel veri silme yöntemi, hukuk görüşü) açık.
- Mimari süitte iş öncesinden kırmızılar vardır, bu işe ait değildir. Üç `CiServiceTestsGuardTests` testi güncel ana dalla birleşik koşuda geçti; kalan üç kırmızı iki denetim kaydı testi ve belge yolu bekçisidir (bkz. mimari denetim raporu).
- Platform tam süitte 1 kırmızı, bu işe ait değil: abonelik testi gövdesinde sabit `2026-11-02` deneme bitiş tarihi bugünden itibaren doğrulamaya takılır.
